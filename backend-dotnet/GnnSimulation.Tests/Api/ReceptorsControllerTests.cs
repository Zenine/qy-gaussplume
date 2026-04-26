using System.Net;
using FluentAssertions;
using GnnSimulation.Api.Dtos;
using GnnSimulation.Tests.Infrastructure;

namespace GnnSimulation.Tests.Api;

public class ReceptorsControllerTests : IDisposable
{
    private readonly GnnWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public ReceptorsControllerTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task POST_创建受体_应使用默认标记样式()
    {
        var resp = await _client.PostJsonAsync("/api/receptors", new ReceptorCreateDto
        {
            Name = "学校", Latitude = 39.9, Longitude = 116.4, Height = 1.5,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);

        var dto = await resp.ReadJsonAsync<ReceptorDto>();
        dto.Name.Should().Be("学校");
        dto.MarkerSymbol.Should().Be("monitor");
        dto.MarkerColor.Should().Be("#2196F3");
        dto.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GET_未找到返回404()
    {
        var resp = await _client.GetAsync("/api/receptors/123456");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PUT_只改Name_坐标保持()
    {
        var create = await _client.PostJsonAsync("/api/receptors", new ReceptorCreateDto
        {
            Name = "原", Latitude = 39.9, Longitude = 116.4, Height = 2.0,
        });
        var dto = await create.ReadJsonAsync<ReceptorDto>();

        var put = await _client.PutJsonAsync($"/api/receptors/{dto.Id}", new ReceptorUpdateDto { Name = "新" });
        put.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await put.ReadJsonAsync<ReceptorDto>();
        updated.Name.Should().Be("新");
        updated.Latitude.Should().Be(39.9);
        updated.Height.Should().Be(2.0);
    }

    [Fact]
    public async Task DELETE_并刷新列表()
    {
        var c1 = await _client.PostJsonAsync("/api/receptors", new ReceptorCreateDto { Name = "A", Latitude = 0, Longitude = 0, Height = 0 });
        var a = await c1.ReadJsonAsync<ReceptorDto>();
        var c2 = await _client.PostJsonAsync("/api/receptors", new ReceptorCreateDto { Name = "B", Latitude = 0, Longitude = 0, Height = 0 });
        _ = await c2.ReadJsonAsync<ReceptorDto>();

        await _client.DeleteAsync($"/api/receptors/{a.Id}");

        var list = await (await _client.GetAsync("/api/receptors")).ReadJsonAsync<List<ReceptorDto>>();
        list.Should().HaveCount(1);
        list[0].Name.Should().Be("B");
    }

    [Fact]
    public async Task POST_batch_一次性导入多个()
    {
        var payload = Enumerable.Range(1, 5).Select(i => new ReceptorCreateDto
        {
            Name = $"R{i}",
            Latitude = 39.9 + i * 0.01,
            Longitude = 116.4 + i * 0.01,
            Height = i,
        }).ToList();

        var resp = await _client.PostJsonAsync("/api/receptors/batch", payload);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await resp.ReadJsonAsync<List<ReceptorDto>>();
        list.Should().HaveCount(5);
    }

    [Fact]
    public async Task GET_list_支持skip_limit()
    {
        var batch = Enumerable.Range(1, 10).Select(i => new ReceptorCreateDto
        {
            Name = $"R{i}", Latitude = 0, Longitude = 0, Height = 0,
        }).ToList();
        await _client.PostJsonAsync("/api/receptors/batch", batch);

        var page1 = await (await _client.GetAsync("/api/receptors?skip=0&limit=3")).ReadJsonAsync<List<ReceptorDto>>();
        page1.Should().HaveCount(3);

        var page2 = await (await _client.GetAsync("/api/receptors?skip=3&limit=3")).ReadJsonAsync<List<ReceptorDto>>();
        page2.Should().HaveCount(3);
        page2.Select(x => x.Id).Should().NotIntersectWith(page1.Select(x => x.Id));
    }
}
