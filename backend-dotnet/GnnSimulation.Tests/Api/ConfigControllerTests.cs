using System.Net;
using FluentAssertions;
using GnnSimulation.Api.Dtos;
using GnnSimulation.Tests.Infrastructure;

namespace GnnSimulation.Tests.Api;

public class ConfigControllerTests : IDisposable
{
    private readonly GnnWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public ConfigControllerTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task POST_创建标记配置_成功后能按type查询()
    {
        var resp = await _client.PostJsonAsync("/api/config", new MarkerConfigCreateDto
        {
            Type = "source", Symbol = "factory", Color = "#FF5722", Size = 12,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);

        var hit = await _client.GetAsync("/api/config/source");
        hit.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await hit.ReadJsonAsync<MarkerConfigDto>();
        dto.Type.Should().Be("source");
        dto.Size.Should().Be(12);
    }

    [Fact]
    public async Task POST_重复type_返回400()
    {
        await _client.PostJsonAsync("/api/config", new MarkerConfigCreateDto { Type = "source" });
        var dup = await _client.PostJsonAsync("/api/config", new MarkerConfigCreateDto { Type = "source" });
        dup.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GET_未知type_返回404()
    {
        var resp = await _client.GetAsync("/api/config/不存在");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PUT_按type部分更新()
    {
        await _client.PostJsonAsync("/api/config", new MarkerConfigCreateDto
        {
            Type = "receptor", Symbol = "monitor", Color = "#2196F3", Size = 8,
        });

        var put = await _client.PutJsonAsync("/api/config/receptor", new MarkerConfigUpdateDto { Size = 20 });
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await put.ReadJsonAsync<MarkerConfigDto>();
        dto.Size.Should().Be(20);
        dto.Symbol.Should().Be("monitor");
        dto.Color.Should().Be("#2196F3");
    }

    [Fact]
    public async Task GET_list_返回所有配置()
    {
        await _client.PostJsonAsync("/api/config", new MarkerConfigCreateDto { Type = "source" });
        await _client.PostJsonAsync("/api/config", new MarkerConfigCreateDto { Type = "receptor" });
        await _client.PostJsonAsync("/api/config", new MarkerConfigCreateDto { Type = "area" });

        var list = await (await _client.GetAsync("/api/config")).ReadJsonAsync<List<MarkerConfigDto>>();
        list.Should().HaveCount(3);
        list.Select(x => x.Type).Should().BeEquivalentTo(new[] { "source", "receptor", "area" });
    }
}
