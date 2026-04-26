using System.Net;
using FluentAssertions;
using GnnSimulation.Api.Services;
using GnnSimulation.Tests.Infrastructure;

namespace GnnSimulation.Tests.Api;

public class MapControllerTests : IDisposable
{
    private readonly GnnWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public MapControllerTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task GET_bounds_默认返回中国范围()
    {
        var resp = await _client.GetAsync("/api/map/bounds");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var b = await resp.ReadJsonAsync<MapBoundsDto>();
        // 测试环境没有 shp 文件 → 应返回默认中国范围
        b.MinLat.Should().BeGreaterThan(20);
        b.MaxLat.Should().BeLessThan(60);
        b.MinLon.Should().BeGreaterThan(70);
        b.MaxLon.Should().BeLessThan(140);
    }

    [Fact]
    public async Task GET_info_默认返回feature_count_0()
    {
        var resp = await _client.GetAsync("/api/map/info");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var info = await resp.ReadJsonAsync<MapInfoDto>();
        info.Crs.Should().Be("EPSG:4326");
        info.FeatureCount.Should().Be(0);
        info.Bounds.Should().NotBeNull();
    }

    [Fact]
    public async Task GET_geojson_默认返回空FeatureCollection()
    {
        var resp = await _client.GetAsync("/api/map/geojson");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("\"type\":\"FeatureCollection\"");
        body.Should().Contain("\"features\":[]");
    }
}
