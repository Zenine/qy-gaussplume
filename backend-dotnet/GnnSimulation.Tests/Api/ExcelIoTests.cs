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
    public async Task 点源模板_列头包含所有7种污染物()
    {
        var resp = await _client.GetAsync("/api/sources/template/point");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheet(1);

        var lastCol = ws.Row(1).LastCellUsed()!.Address.ColumnNumber;
        var headers = Enumerable.Range(1, lastCol).Select(c => ws.Cell(1, c).GetString()).ToList();

        headers.Should().Contain(new[] { "PM2.5", "PM10", "TSP", "VOCs", "NOx", "SO2", "O3" });
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
    public async Task 风向加权模板_包含三列表头和72方位示例()
    {
        var resp = await _client.GetAsync("/api/simulation/wind-profile/template");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.Should().Be(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        var bytes = await resp.Content.ReadAsByteArrayAsync();
        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheet(1);

        ws.Cell(1, 1).GetString().Should().Be("风向中心角度");
        ws.Cell(1, 2).GetString().Should().Be("平均风速(m/s)");
        ws.Cell(1, 3).GetString().Should().Be("加权值");
        ws.Cell(2, 1).GetDouble().Should().Be(0);
        ws.Cell(2, 2).GetDouble().Should().Be(2.45);
        ws.Cell(2, 3).GetDouble().Should().Be(0.0169);
        ws.Cell(73, 1).GetDouble().Should().Be(355);
        ws.Cell(73, 2).GetDouble().Should().Be(2.47);
        ws.Cell(73, 3).GetDouble().Should().Be(0.0143);
    }

    [Fact]
    public async Task 风向加权导入_解析每方位风速和权重并保持行顺序()
    {
        var bytes = BuildWindProfileXlsx(new[]
        {
            new[] { 0.0, 2.45, 0.0169 },
            new[] { 5.0, 2.23, 0.0159 },
            new[] { 355.0, 2.47, 0.0143 },
        });

        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(file, "file", "wind-profile.xlsx");

        var resp = await _client.PostAsync("/api/simulation/wind-profile/import", content);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await resp.ReadJsonAsync<WindProfileImportResultDto>();

        result.DirectionCount.Should().Be(3);
        result.WindDirections.Should().Equal(0, 5, 355);
        result.WindSpeeds.Should().Equal(2.45, 2.23, 2.47);
        result.Weights.Should().Equal(0.0169, 0.0159, 0.0143);
        result.WeightSum.Should().BeApproximately(0.0471, 1e-12);
    }

    [Fact]
    public async Task 风向加权导入_重复角度返回400并指出行号()
    {
        var bytes = BuildWindProfileXlsx(new[]
        {
            new[] { 0.0, 2.45, 0.5 },
            new[] { 0.0, 2.23, 0.5 },
        });

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(bytes), "file", "wind-profile.xlsx");

        var resp = await _client.PostAsync("/api/simulation/wind-profile/import", content);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("第3行").And.Contain("风向中心角度重复");
    }

    [Fact]
    public async Task 风向加权导入_文件超过大小上限返回400且不尝试解析()
    {
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(new byte[5 * 1024 * 1024 + 1]), "file", "oversized.xlsx");

        var resp = await _client.PostAsync("/api/simulation/wind-profile/import", content);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("5 MB");
    }

    [Fact]
    public async Task 风向加权导入_损坏文件返回通用错误且不暴露解析异常()
    {
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(new byte[] { 0x01, 0x02, 0x03 }), "file", "broken.xlsx");

        var resp = await _client.PostAsync("/api/simulation/wind-profile/import", content);
        var body = await resp.Content.ReadAsStringAsync();

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("请确认文件格式和模板表头正确");
        body.Should().NotContain("corrupted").And.NotContain("Exception");
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

    private static byte[] BuildWindProfileXlsx(IEnumerable<double[]> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("风向加权");
        ws.Cell(1, 1).Value = "风向中心角度";
        ws.Cell(1, 2).Value = "平均风速(m/s)";
        ws.Cell(1, 3).Value = "加权值";
        var rowNumber = 2;
        foreach (var row in rows)
        {
            ws.Cell(rowNumber, 1).Value = row[0];
            ws.Cell(rowNumber, 2).Value = row[1];
            ws.Cell(rowNumber, 3).Value = row[2];
            rowNumber++;
        }
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
