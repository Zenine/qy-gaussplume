using System.Net;
using System.Net.Http.Headers;
using ClosedXML.Excel;
using FluentAssertions;
using GnnSimulation.Api.Dtos;
using GnnSimulation.Tests.Infrastructure;

namespace GnnSimulation.Tests.Api;

public class ExcelIoTests : IDisposable
{
    private readonly GnnWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public ExcelIoTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task 受体点模板下载_xlsx头与示例行正确()
    {
        var resp = await _client.GetAsync("/api/receptors/template");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.Should().Be(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        var bytes = await resp.Content.ReadAsByteArrayAsync();
        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheet(1);
        ws.Cell(1, 1).GetString().Should().Be("名称");
        ws.Cell(1, 2).GetString().Should().Be("纬度");
        ws.Cell(1, 3).GetString().Should().Be("经度");
        ws.Cell(1, 4).GetString().Should().Be("高度");
        ws.Cell(1, 5).GetString().Should().Be("标记符号");
        ws.Cell(1, 6).GetString().Should().Be("标记颜色");
        ws.Cell(2, 1).GetString().Should().Be("示例受体点");
        ws.Cell(2, 2).GetDouble().Should().Be(39.9);
    }

    [Fact]
    public async Task 受体点_导出后再导入_内容保持一致()
    {
        // 先创建两个受体点
        foreach (var r in new[]
        {
            new ReceptorCreateDto { Name = "A", Latitude = 39.9, Longitude = 116.4, Height = 1.5 },
            new ReceptorCreateDto { Name = "B", Latitude = 40.0, Longitude = 116.5, Height = 2.5 },
        })
        {
            await _client.PostJsonAsync("/api/receptors", r);
        }

        var listResp = await _client.GetAsync("/api/receptors");
        var original = await listResp.ReadJsonAsync<List<ReceptorDto>>();

        // 导出
        var exportResp = await _client.PostJsonAsync("/api/receptors/export", original.Select(x => x.Id).ToList());
        exportResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var xlsxBytes = await exportResp.Content.ReadAsByteArrayAsync();

        // 删光再重新导入
        foreach (var r in original)
            await _client.DeleteAsync($"/api/receptors/{r.Id}");
        (await (await _client.GetAsync("/api/receptors")).ReadJsonAsync<List<ReceptorDto>>())
            .Should().BeEmpty();

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(xlsxBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", "receptors.xlsx");

        var importResp = await _client.PostAsync("/api/receptors/import", content);
        importResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var refetched = await (await _client.GetAsync("/api/receptors")).ReadJsonAsync<List<ReceptorDto>>();
        refetched.Should().HaveCount(original.Count);
        refetched.Select(x => x.Name).Should().BeEquivalentTo(original.Select(x => x.Name));
        refetched[0].Latitude.Should().Be(original[0].Latitude);
    }

    [Fact]
    public async Task 点源模板_列头包含所有6种污染物()
    {
        var resp = await _client.GetAsync("/api/sources/template/point");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheet(1);

        var lastCol = ws.Row(1).LastCellUsed()!.Address.ColumnNumber;
        var headers = Enumerable.Range(1, lastCol).Select(c => ws.Cell(1, c).GetString()).ToList();

        headers.Should().Contain(new[] { "PM2.5", "PM10", "TSP", "VOCs", "NOx", "O3" });
        headers.Should().Contain(new[] { "名称", "纬度", "经度", "高度", "标记符号", "标记颜色" });
    }

    [Fact]
    public async Task 线源模板_返回线源专属表头()
    {
        var resp = await _client.GetAsync("/api/sources/template/line");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheet(1);

        ws.Cell(1, 2).GetString().Should().Be("起点纬度");
        ws.Cell(1, 3).GetString().Should().Be("起点经度");
        ws.Cell(1, 4).GetString().Should().Be("终点纬度");
        ws.Cell(1, 5).GetString().Should().Be("终点经度");
    }

    [Fact]
    public async Task 非法source_type_模板返回400()
    {
        var resp = await _client.GetAsync("/api/sources/template/invalid");
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task 点源导入_行数正确_污染物被正确分配()
    {
        // 构造一个点源导入文件：两行数据，每行填 PM2.5 和 NOx
        var bytes = BuildPointSourceXlsx(new[]
        {
            new object?[] { "源1", 39.9, 116.4, 50, 400, 15, 2,   1.5, null, null, null, 2.0, null, "factory", "#FF5722" },
            new object?[] { "源2", 39.8, 116.3, 30, 380, 12, 1.5, null, 3.0, null, null, null, null, "industry", "#FF8800" },
        });

        using var content = new MultipartFormDataContent();
        var fc = new ByteArrayContent(bytes);
        fc.Headers.ContentType = MediaTypeHeaderValue.Parse(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fc, "file", "sources.xlsx");

        var resp = await _client.PostAsync("/api/sources/import/point", content);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await (await _client.GetAsync("/api/sources")).ReadJsonAsync<List<EmissionSourceDto>>();
        list.Should().HaveCount(2);
        list.Single(s => s.Name == "源1").Pollutants.Should().HaveCount(2); // PM2.5 + NOx
        list.Single(s => s.Name == "源2").Pollutants.Should().HaveCount(1); // 仅 PM10
        list.Single(s => s.Name == "源2").Pollutants[0].PollutantType.Should().Be("PM10");
    }

    private static byte[] BuildPointSourceXlsx(IEnumerable<object?[]> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("source");
        var headers = new[]
        {
            "名称", "纬度", "经度", "高度", "烟气温度(K)", "烟气速度", "烟囱直径",
            "PM2.5", "PM10", "TSP", "VOCs", "NOx", "O3",
            "标记符号", "标记颜色",
        };
        for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];

        var r = 2;
        foreach (var row in rows)
        {
            for (var c = 0; c < row.Length; c++)
            {
                ws.Cell(r, c + 1).Value = row[c] switch
                {
                    null => XLCellValue.FromObject(string.Empty),
                    string s => s,
                    double d => d,
                    int i => i,
                    _ => XLCellValue.FromObject(row[c]),
                };
            }
            r++;
        }
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
