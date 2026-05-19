using FluentAssertions;
using GnnSimulation.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace GnnSimulation.Tests.Api;

// 针对真实 shp/县（等积投影）.shp 的集成测试。
// 若文件不存在则跳过，避免 CI 环境失败。
public class ShapefileServiceTests
{
    private static readonly string RealShpPath =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "shp", "县（等积投影）.shp"));

    private static bool HasRealShp => File.Exists(RealShpPath);

    private static ShapefileService Build(string? path, bool loadByDefault)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Shapefile:Path"] = path,
            ["Shapefile:LoadByDefault"] = loadByDefault ? "true" : "false",
        }).Build();
        return new ShapefileService(config, NullLogger<ShapefileService>.Instance);
    }

    [Fact]
    public void 无配置时_Exists为false_bounds为中国默认范围()
    {
        var svc = Build(path: null, loadByDefault: true);
        svc.Exists().Should().BeFalse();
        var b = svc.GetBounds();
        b.MinLat.Should().Be(30.0);
        b.MaxLat.Should().Be(40.0);
    }

    [Fact]
    public void 文件不存在时_info报告错误信息()
    {
        var svc = Build(path: "/does/not/exist.shp", loadByDefault: true);
        svc.Exists().Should().BeFalse();
        var info = svc.GetInfo();
        info.Error.Should().NotBeNull();
        info.FeatureCount.Should().Be(0);
    }

    [Fact]
    public void LoadByDefault为false时_GeoJson返回空FeatureCollection()
    {
        var svc = Build(path: RealShpPath, loadByDefault: false);
        var json = svc.GetGeoJson();
        json.Should().Contain("\"features\":[]");
    }

    [Fact]
    public void 真实shp_info报告合理的要素数与边界()
    {
        if (!HasRealShp)
        {
            Assert.True(true, "跳过：shp 文件不存在");
            return;
        }

        var svc = Build(path: RealShpPath, loadByDefault: true);
        var info = svc.GetInfo();
        // 先检查错误信息，便于诊断
        info.Error.Should().BeNull($"Load error: {info.Error}");
        info.FeatureCount.Should().BeGreaterThan(100);
        info.Columns.Should().NotBeEmpty();

        // 投影成 WGS84 后，经度应在中国境内，纬度范围要覆盖大陆 + 南海
        info.Bounds.MinLon.Should().BeGreaterThan(70);
        info.Bounds.MaxLon.Should().BeLessThan(140);
        info.Bounds.MinLat.Should().BeGreaterThan(0); // 含南海岛礁
        info.Bounds.MaxLat.Should().BeLessThan(60);
    }

    [Fact]
    public void 真实shp_GeoJson_force_true时返回非空要素集()
    {
        if (!HasRealShp)
        {
            Assert.True(true, "跳过：shp 文件不存在");
            return;
        }

        var svc = Build(path: RealShpPath, loadByDefault: false);
        var json = svc.GetGeoJson(forceLoad: true);
        json.Should().Contain("\"type\":\"FeatureCollection\"");
        json.Should().NotContain("\"features\":[]");
        json.Length.Should().BeGreaterThan(1000);
    }
}
