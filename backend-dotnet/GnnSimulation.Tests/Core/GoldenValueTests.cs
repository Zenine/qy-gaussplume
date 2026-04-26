using System.Text.Json;
using FluentAssertions;
using GnnSimulation.Core.Atmosphere;

namespace GnnSimulation.Tests.Core;

// 将 C# 输出与 Python 原版生成的 JSON 黄金值做对比，确保数值对齐。
// 相对/绝对误差阈值选择为 1e-9（IEEE754 双精度浮点下，数学相同运算序列应完全一致）。
public class GoldenValueTests
{
    private const double Tolerance = 1e-9;

    private static readonly JsonDocument Golden = LoadGolden();

    private static JsonDocument LoadGolden()
    {
        var dir = AppContext.BaseDirectory;
        var path = Path.Combine(dir, "Data", "golden", "golden_values.json");
        var json = File.ReadAllText(path);
        return JsonDocument.Parse(json);
    }

    private static GaussianPlumeModel MakeDefault(
        double windSpeed = 3.0, double windDirection = 0.0, string stability = "D",
        double temperature = 293.15, double blh = 1000.0,
        double humidity = 50.0, double cloudCover = 0.0, double precipitation = 0.0)
        => new(windSpeed, windDirection, stability, temperature, blh, humidity, cloudCover, precipitation);

    [Fact]
    public void Golden_Sigma_全部条目对齐()
    {
        foreach (var item in Golden.RootElement.GetProperty("sigma").EnumerateArray())
        {
            var stab = item.GetProperty("stability").GetString()!;
            var dist = item.GetProperty("distance").GetDouble();
            var expSy = item.GetProperty("sigma_y").GetDouble();
            var expSz = item.GetProperty("sigma_z").GetDouble();

            var m = MakeDefault(stability: stab);
            var (sy, sz) = m.CalculateSigma(dist);
            sy.Should().BeApproximately(expSy, Tolerance, $"sigma_y {stab}@{dist}");
            sz.Should().BeApproximately(expSz, Tolerance, $"sigma_z {stab}@{dist}");
        }
    }

    [Fact]
    public void Golden_干沉降速度对齐()
    {
        foreach (var item in Golden.RootElement.GetProperty("dry_deposition").EnumerateArray())
        {
            var pol = item.GetProperty("pollutant").GetString()!;
            var expected = item.GetProperty("vd").GetDouble();

            var stab = item.TryGetProperty("stability", out var s) ? s.GetString()! : "D";
            var ws = item.TryGetProperty("wind_speed", out var w) ? w.GetDouble() : 3.0;
            var hum = item.TryGetProperty("humidity", out var h) ? h.GetDouble() : 50.0;

            var m = MakeDefault(windSpeed: ws, stability: stab, humidity: hum);
            m.CalculateDryDepositionVelocity(pol).Should().BeApproximately(expected, Tolerance);
        }
    }

    [Fact]
    public void Golden_湿清除对齐()
    {
        foreach (var item in Golden.RootElement.GetProperty("wet_scavenging").EnumerateArray())
        {
            var pol = item.GetProperty("pollutant").GetString()!;
            var prec = item.GetProperty("precipitation").GetDouble();
            var cc = item.GetProperty("cloud_cover").GetDouble();
            var expected = item.GetProperty("lambda_").GetDouble();

            var m = MakeDefault(precipitation: prec, cloudCover: cc);
            m.CalculateWetScavengingCoefficient(pol).Should().BeApproximately(expected, Tolerance);
        }
    }

    [Fact]
    public void Golden_衰减系数对齐()
    {
        foreach (var item in Golden.RootElement.GetProperty("decay").EnumerateArray())
        {
            var dist = item.GetProperty("distance").GetDouble();
            var m = MakeDefault();
            m.CalculateDepositionCoefficient(dist, "PM2.5")
                .Should().BeApproximately(item.GetProperty("deposition").GetDouble(), Tolerance);
            m.CalculateChemicalDecay(dist, "PM2.5")
                .Should().BeApproximately(item.GetProperty("chemical").GetDouble(), Tolerance);
            m.CalculateTotalDecay(dist, "PM2.5")
                .Should().BeApproximately(item.GetProperty("total").GetDouble(), Tolerance);
        }
    }

    [Fact]
    public void Golden_有效高度对齐()
    {
        var m = MakeDefault();
        foreach (var item in Golden.RootElement.GetProperty("effective_height").EnumerateArray())
        {
            var result = m.CalculateEffectiveHeight(
                stackHeight: item.GetProperty("stack_height").GetDouble(),
                emissionRate: item.GetProperty("emission_rate").GetDouble(),
                stackTemperature: item.GetProperty("stack_temp").GetDouble(),
                velocity: item.GetProperty("velocity").GetDouble(),
                diameter: item.GetProperty("diameter").GetDouble());
            result.Should().BeApproximately(item.GetProperty("effective").GetDouble(), Tolerance);
        }
    }

    [Fact]
    public void Golden_最大扩散距离对齐()
    {
        foreach (var item in Golden.RootElement.GetProperty("max_distance").EnumerateArray())
        {
            var m = MakeDefault(stability: item.GetProperty("stability").GetString()!);
            m.CalculateMaxDiffusionDistance()
                .Should().BeApproximately(item.GetProperty("max_distance").GetDouble(), Tolerance);
        }
    }

    [Fact]
    public void Golden_单点浓度对齐()
    {
        var m = MakeDefault();
        foreach (var item in Golden.RootElement.GetProperty("point_concentration").EnumerateArray())
        {
            var result = m.CalculateConcentration(
                x: item.GetProperty("x").GetDouble(),
                y: item.GetProperty("y").GetDouble(),
                z: item.GetProperty("z").GetDouble(),
                sourceHeight: item.GetProperty("h").GetDouble(),
                emissionRate: item.GetProperty("Q").GetDouble());
            var expected = item.GetProperty("concentration").GetDouble();
            // 浓度可能很大，使用相对误差
            AssertClose(result, expected, "point concentration");
        }
    }

    [Fact]
    public void Golden_受体点浓度对齐()
    {
        foreach (var item in Golden.RootElement.GetProperty("receptor_concentration").EnumerateArray())
        {
            var wd = item.GetProperty("wind_direction").GetDouble();
            var m = MakeDefault(windDirection: wd);
            var result = m.CalculateReceptorConcentration(
                sourceLat: item.GetProperty("src_lat").GetDouble(),
                sourceLon: item.GetProperty("src_lon").GetDouble(),
                sourceHeight: item.GetProperty("src_h").GetDouble(),
                emissionRate: item.GetProperty("Q").GetDouble(),
                receptorLat: item.GetProperty("rec_lat").GetDouble(),
                receptorLon: item.GetProperty("rec_lon").GetDouble(),
                receptorHeight: item.GetProperty("rec_h").GetDouble(),
                stackTemperature: item.GetProperty("stack_temperature").GetDouble(),
                velocity: item.GetProperty("velocity").GetDouble(),
                diameter: item.GetProperty("diameter").GetDouble());
            var expected = item.GetProperty("concentration").GetDouble();
            AssertClose(result, expected, $"receptor {item.GetProperty("label").GetString()}");
        }
    }

    [Fact]
    public void Golden_浓度场采样点对齐()
    {
        var root = Golden.RootElement.GetProperty("concentration_field_samples");
        var gridLat = root.GetProperty("grid_lat").EnumerateArray().Select(x => x.GetDouble()).ToArray();
        var gridLon = root.GetProperty("grid_lon").EnumerateArray().Select(x => x.GetDouble()).ToArray();

        var m = MakeDefault(windDirection: 0.0);
        var field = m.CalculateConcentrationField(
            sourceLat: 39.9, sourceLon: 116.4,
            sourceHeight: 50, emissionRate: 1.0,
            gridLat: gridLat, gridLon: gridLon,
            stackTemperature: 400.0, velocity: 15.0, diameter: 2.0);

        foreach (var s in root.GetProperty("samples").EnumerateArray())
        {
            var i = s.GetProperty("i").GetInt32();
            var j = s.GetProperty("j").GetInt32();
            var expected = s.GetProperty("concentration").GetDouble();
            AssertClose(field[i, j], expected, $"field[{i},{j}]");
        }
    }

    [Fact]
    public void Golden_面源浓度场对齐()
    {
        var root = Golden.RootElement.GetProperty("area_source_field_samples");
        var area = root.GetProperty("area");
        var gridLat = root.GetProperty("grid_lat").EnumerateArray().Select(x => x.GetDouble()).ToArray();
        var gridLon = root.GetProperty("grid_lon").EnumerateArray().Select(x => x.GetDouble()).ToArray();

        var m = MakeDefault(windDirection: 0.0);
        var field = m.CalculateAreaSourceConcentrationField(
            centerLat: area.GetProperty("center_lat").GetDouble(),
            centerLon: area.GetProperty("center_lon").GetDouble(),
            areaLength: area.GetProperty("length").GetDouble(),
            areaWidth: area.GetProperty("width").GetDouble(),
            areaHeight: area.GetProperty("height").GetDouble(),
            emissionRate: area.GetProperty("Q").GetDouble(),
            gridLat: gridLat, gridLon: gridLon);

        foreach (var s in root.GetProperty("samples").EnumerateArray())
        {
            var i = s.GetProperty("i").GetInt32();
            var j = s.GetProperty("j").GetInt32();
            var expected = s.GetProperty("concentration").GetDouble();
            AssertClose(field[i, j], expected, $"area[{i},{j}]");
        }
    }

    [Fact]
    public void Golden_等效面源排放速率对齐()
    {
        var item = Golden.RootElement.GetProperty("equivalent_emission_rate");
        var m = MakeDefault();
        var result = m.CalculateEquivalentEmissionRate(
            concentration: item.GetProperty("concentration").GetDouble(),
            areaLength: item.GetProperty("length").GetDouble(),
            areaWidth: item.GetProperty("width").GetDouble(),
            areaHeight: item.GetProperty("height").GetDouble());
        result.Should().BeApproximately(item.GetProperty("rate").GetDouble(), Tolerance);
    }

    [Fact]
    public void Golden_反推排放速率对齐()
    {
        var item = Golden.RootElement.GetProperty("reverse_emission");
        var m = MakeDefault();
        var result = m.CalculateEmissionRateFromConcentration(
            x: item.GetProperty("x").GetDouble(),
            y: item.GetProperty("y").GetDouble(),
            z: item.GetProperty("z").GetDouble(),
            sourceHeight: item.GetProperty("source_height").GetDouble(),
            concentration: item.GetProperty("forward_no_decay").GetDouble());
        result.Should().BeApproximately(item.GetProperty("Q_expected").GetDouble(), 1e-8);
    }

    [Fact]
    public void Golden_稳定度分类器对齐()
    {
        foreach (var item in Golden.RootElement.GetProperty("stability_classifier").EnumerateArray())
        {
            var ws = item.GetProperty("wind_speed").GetDouble();
            double? sol = item.GetProperty("solar").ValueKind == JsonValueKind.Null ? null : item.GetProperty("solar").GetDouble();
            double? cc = item.GetProperty("cloud").ValueKind == JsonValueKind.Null ? null : item.GetProperty("cloud").GetDouble();
            var day = item.GetProperty("daytime").GetBoolean();
            var expected = item.GetProperty("result").GetString()!;
            StabilityClassifier.Classify(ws, sol, cc, day).Should().Be(expected);
        }
    }

    [Fact]
    public void Golden_化学衰减对齐()
    {
        foreach (var item in Golden.RootElement.GetProperty("chemical_decay_extras").EnumerateArray())
        {
            var m = MakeDefault(
                temperature: item.GetProperty("temperature").GetDouble(),
                humidity: item.GetProperty("humidity").GetDouble(),
                cloudCover: item.GetProperty("cloud_cover").GetDouble());
            var result = m.CalculateChemicalDecay(
                distance: item.GetProperty("distance").GetDouble(),
                pollutant: item.GetProperty("pollutant").GetString()!);
            result.Should().BeApproximately(item.GetProperty("decay").GetDouble(), Tolerance);
        }
    }

    private static void AssertClose(double actual, double expected, string context)
    {
        // 小量用绝对误差，大量用相对误差
        if (Math.Abs(expected) < 1)
            actual.Should().BeApproximately(expected, Tolerance, context);
        else
            (Math.Abs(actual - expected) / Math.Abs(expected))
                .Should().BeLessThan(1e-10, $"{context}: actual={actual}, expected={expected}");
    }
}
