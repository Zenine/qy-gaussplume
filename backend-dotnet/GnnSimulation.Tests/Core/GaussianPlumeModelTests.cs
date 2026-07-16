using FluentAssertions;
using GnnSimulation.Core.Atmosphere;

namespace GnnSimulation.Tests.Core;

// 独立单元测试：验证关键物理性质（单调性、边界、退化情形），与黄金对齐测试互补
public class GaussianPlumeModelTests
{
    [Fact]
    public void 构造_无效稳定度_抛异常()
    {
        var act = () => new GaussianPlumeModel(3.0, 0.0, "X");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void 构造_最低风速夹紧为0_1()
    {
        var m = new GaussianPlumeModel(0.01, 0.0, "D");
        m.WindSpeed.Should().Be(0.1);
    }

    [Fact]
    public void Sigma_距离越远扩散越宽()
    {
        var m = new GaussianPlumeModel(3.0, 0.0, "D");
        var (sy100, sz100) = m.CalculateSigma(100);
        var (sy1000, sz1000) = m.CalculateSigma(1000);
        sy1000.Should().BeGreaterThan(sy100);
        sz1000.Should().BeGreaterThan(sz100);
    }

    [Fact]
    public void Sigma_BLH限制垂直扩散()
    {
        var mShallow = new GaussianPlumeModel(3.0, 0.0, "D", boundaryLayerHeight: 200);
        var mDeep = new GaussianPlumeModel(3.0, 0.0, "D", boundaryLayerHeight: 5000);
        var (_, szShallow) = mShallow.CalculateSigma(5000);
        var (_, szDeep) = mDeep.CalculateSigma(5000);
        szShallow.Should().BeLessThan(szDeep);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("F")]
    public void Sigma_下限1_很短距离_sy下限为1_sz因BLH夹紧略低于1(string stab)
    {
        // Python 原版行为：sigma_y 先取 max(·, 1)，sigma_z 取 max 后再应用 BLH soft-clip
        // 当 sigma_z = 1 时，1/sqrt(1 + (1/BLH)²) ≈ 1 - 0.5/BLH² ≈ 0.9999995 for BLH=1000
        var m = new GaussianPlumeModel(3.0, 0.0, stab);
        var (sy, sz) = m.CalculateSigma(0.01);
        sy.Should().BeGreaterThanOrEqualTo(1.0);
        sz.Should().BeGreaterThan(0.99); // BLH 修正后的下限
    }

    [Fact]
    public void 上风向单点浓度返回0()
    {
        var m = new GaussianPlumeModel(3.0, 0.0, "D");
        m.CalculateConcentration(x: -100, y: 0, z: 0, sourceHeight: 50, emissionRate: 1.0).Should().Be(0);
        m.CalculateConcentration(x: 0, y: 0, z: 0, sourceHeight: 50, emissionRate: 1.0).Should().Be(0);
    }

    [Fact]
    public void 超过最大扩散距离返回0()
    {
        var m = new GaussianPlumeModel(3.0, 0.0, "D");
        var maxD = m.CalculateMaxDiffusionDistance();
        m.CalculateConcentration(x: maxD * 1.01, y: 0, z: 0, sourceHeight: 50, emissionRate: 1.0)
            .Should().Be(0);
    }

    [Fact]
    public void 风速越大浓度越低()
    {
        var weak = new GaussianPlumeModel(1.0, 0.0, "D");
        var strong = new GaussianPlumeModel(5.0, 0.0, "D");
        var cWeak = weak.CalculateConcentration(1000, 0, 0, 50, 1.0);
        var cStrong = strong.CalculateConcentration(1000, 0, 0, 50, 1.0);
        cWeak.Should().BeGreaterThan(cStrong);
    }

    [Fact]
    public void 干沉降速度_随湿度增加()
    {
        var dry = new GaussianPlumeModel(3.0, 0.0, "D", humidity: 20);
        var wet = new GaussianPlumeModel(3.0, 0.0, "D", humidity: 90);
        dry.CalculateDryDepositionVelocity("PM2.5").Should().BeLessThan(wet.CalculateDryDepositionVelocity("PM2.5"));
    }

    [Fact]
    public void 湿清除_无降水只有背景项()
    {
        var noRain = new GaussianPlumeModel(3.0, 0.0, "D", precipitation: 0);
        var withRain = new GaussianPlumeModel(3.0, 0.0, "D", precipitation: 10);
        // 无降水：只有 background (1e-5) × cloud_factor (=1)
        noRain.CalculateWetScavengingCoefficient("PM2.5").Should().BeApproximately(1e-5, 1e-12);
        withRain.CalculateWetScavengingCoefficient("PM2.5").Should().BeGreaterThan(1e-5);
    }

    [Fact]
    public void 衰减_距离越远衰减越强()
    {
        var m = new GaussianPlumeModel(3.0, 0.0, "D", precipitation: 2.0);
        var near = m.CalculateTotalDecay(500, "PM2.5");
        var far = m.CalculateTotalDecay(5000, "PM2.5");
        near.Should().BeGreaterThan(far);
        near.Should().BeLessThanOrEqualTo(1.0);
        far.Should().BeLessThanOrEqualTo(1.0);
        far.Should().BeGreaterThan(0);
    }

    [Fact]
    public void 不同污染因子_使用各自沉降湿清除和化学参数()
    {
        var m = new GaussianPlumeModel(
            windSpeed: 3.0,
            windDirection: 0.0,
            stabilityClass: "D",
            temperature: 303.15,
            humidity: 75,
            cloudCover: 4,
            precipitation: 3);

        m.CalculateGravitationalSettlingVelocity("PM2.5")
            .Should().NotBe(m.CalculateGravitationalSettlingVelocity("PM10"));
        m.CalculateWetScavengingCoefficient("PM10")
            .Should().NotBeApproximately(m.CalculateWetScavengingCoefficient("NOx"), 1e-12);
        m.CalculateChemicalDecay(1000, "NOx")
            .Should().NotBeApproximately(m.CalculateChemicalDecay(1000, "PM2.5"), 1e-12);
        m.CalculateTotalDecay(1000, "PM2.5")
            .Should().NotBeApproximately(m.CalculateTotalDecay(1000, "O3"), 1e-12);
    }

    [Fact]
    public void SO2_使用指定沉降化学参数及温湿度增强系数()
    {
        PollutantProperties.GetGravitationalSettling("SO2").Should().Be(0);
        PollutantProperties.GetDryResistance("SO2").Should().Be(new ResistanceParams(150, 400));
        PollutantProperties.GetWetScavenging("SO2").Should().Be(new ScavengingParams(8e-6, 0.7));
        PollutantProperties.GetChemicalRate("SO2").Should().Be(4.81e-5);
        PollutantProperties.ChemicalEnhancedPollutants.Should().Contain("SO2");

        var model = new GaussianPlumeModel(
            windSpeed: 3,
            windDirection: 0,
            temperature: 298,
            humidity: 50,
            cloudCover: 0);
        var expected = Math.Exp(-(4.81e-5 * 1.5 * 1.3) * (1000.0 / 3.0));
        model.CalculateChemicalDecay(1000, "SO2").Should().BeApproximately(expected, 1e-12);
    }

    [Fact]
    public void 浓度场_同等排放速率下不同污染因子结果不同()
    {
        var m = new GaussianPlumeModel(
            windSpeed: 3.0,
            windDirection: 0.0,
            stabilityClass: "D",
            temperature: 303.15,
            humidity: 75,
            cloudCover: 4,
            precipitation: 3);
        var gridLat = new[] { 39.88, 39.90 };
        var gridLon = new[] { 116.40 };

        var pm25 = m.CalculateConcentrationField(39.90, 116.40, 50, 1.0, gridLat, gridLon, pollutant: "PM2.5");
        var pm10 = m.CalculateConcentrationField(39.90, 116.40, 50, 1.0, gridLat, gridLon, pollutant: "PM10");

        pm25[0, 0].Should().BeGreaterThan(0);
        pm10[0, 0].Should().BeGreaterThan(0);
        pm25[0, 0].Should().NotBeApproximately(pm10[0, 0], 1e-9);
    }

    [Fact]
    public void Briggs_有效高度_浮力正则产生抬升()
    {
        var m = new GaussianPlumeModel(3.0, 0.0, "D", temperature: 293.15);
        var effective = m.CalculateEffectiveHeight(stackHeight: 50, emissionRate: 1.0,
            stackTemperature: 500, velocity: 15, diameter: 2);
        effective.Should().BeGreaterThan(50);
    }

    [Fact]
    public void 浓度场_只有下风向网格点有浓度()
    {
        // 风向 0° = 风来自北方，下风向为南。源在网格中心，纬度更小的为下风。
        var m = new GaussianPlumeModel(3.0, 0.0, "D");
        var gridLat = new[] { 39.85, 39.88, 39.90, 39.92, 39.95 };
        var gridLon = new[] { 116.35, 116.38, 116.40, 116.42, 116.45 };
        var field = m.CalculateConcentrationField(39.90, 116.40, 50, 1.0, gridLat, gridLon);

        // 中心列（lon 116.40）：源下风向是纬度更小的点
        field[4, 2].Should().Be(0); // 最北 → 上风向
        field[3, 2].Should().Be(0);
        // field[2, 2] 是源本身（x=0） → 0
        field[1, 2].Should().BeGreaterThan(0);
        field[0, 2].Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(0.0, 0, 2, 4, 2)]   // 北风：向南扩散
    [InlineData(90.0, 2, 0, 2, 4)]  // 东风：向西扩散
    [InlineData(180.0, 4, 2, 0, 2)] // 南风：向北扩散
    [InlineData(270.0, 2, 4, 2, 0)] // 西风：向东扩散
    public void 浓度场_以污染源为原点沿气象下风向扩散(
        double windDirection,
        int downwindLatIndex,
        int downwindLonIndex,
        int upwindLatIndex,
        int upwindLonIndex)
    {
        var m = new GaussianPlumeModel(3.0, windDirection, "D");
        var gridLat = new[] { 39.88, 39.89, 39.90, 39.91, 39.92 };
        var gridLon = new[] { 116.38, 116.39, 116.40, 116.41, 116.42 };

        var field = m.CalculateConcentrationField(39.90, 116.40, 50, 1.0, gridLat, gridLon);

        field[2, 2].Should().Be(0);
        field[downwindLatIndex, downwindLonIndex].Should().BeGreaterThan(0);
        field[upwindLatIndex, upwindLonIndex].Should().Be(0);
    }

    [Fact]
    public void 线源浓度场_非整倍数长度使用连续积分且对步长收敛()
    {
        var m = new GaussianPlumeModel(3.0, 90.0, "D");
        var startLat = 39.90;
        var startLon = 116.4000;
        var endLat = 39.90;
        var endLon = startLon + MetersToLonDegrees(25, startLat);
        var gridLat = new[] { 39.8998, 39.9000, 39.9002 };
        var gridLon = new[] { 116.3990, 116.3995, 116.4000, 116.4005 };

        var actual = m.CalculateLineSourceConcentrationField(
            startLat, startLon,
            endLat, endLon,
            lineWidth: 5,
            lineHeight: 1,
            emissionRate: 9,
            gridLat: gridLat,
            gridLon: gridLon,
            segmentLength: 10,
            sigmaZ0: null,
            receptorHeight: 0,
            pollutant: "NOx");

        var expected = m.CalculateLineSourceConcentrationField(
            startLat, startLon,
            endLat, endLon,
            lineWidth: 5,
            lineHeight: 1,
            emissionRate: 9,
            gridLat: gridLat,
            gridLon: gridLon,
            segmentLength: 2.5,
            sigmaZ0: null,
            receptorHeight: 0,
            pollutant: "NOx");

        AssertMatrixApproximately(actual, expected, 1e-2);
    }

    [Fact]
    public void 线源受体贡献_非整倍数长度使用连续积分且对步长收敛()
    {
        var m = new GaussianPlumeModel(3.0, 90.0, "D");
        var startLat = 39.90;
        var startLon = 116.4000;
        var endLat = 39.90;
        var endLon = startLon + MetersToLonDegrees(25, startLat);

        var actual = m.CalculateLineSourceReceptorConcentration(
            startLat, startLon,
            endLat, endLon,
            lineWidth: 5,
            lineHeight: 1,
            emissionRate: 9,
            receptorLat: 39.90,
            receptorLon: 116.3995,
            segmentLength: 10,
            sigmaZ0: null,
            receptorHeight: 0,
            pollutant: "NOx");

        var expected = m.CalculateLineSourceReceptorConcentration(
            startLat, startLon,
            endLat, endLon,
            lineWidth: 5,
            lineHeight: 1,
            emissionRate: 9,
            receptorLat: 39.90,
            receptorLon: 116.3995,
            segmentLength: 2.5,
            sigmaZ0: null,
            receptorHeight: 0,
            pollutant: "NOx");

        actual.Should().BeApproximately(expected, Math.Max(1e-9, Math.Abs(expected) * 1e-3));
    }

    [Fact]
    public void 线源受体贡献_连续积分不应随粗细积分步长出现点源式跳变()
    {
        var model = new GaussianPlumeModel(3.0, 0.0, "D");
        const double centerLat = 39.90;
        const double centerLon = 116.40;
        var startLon = centerLon - MetersToLonDegrees(100, centerLat);
        var endLon = centerLon + MetersToLonDegrees(100, centerLat);
        var receptorLat = centerLat - 100.0 / 111_000.0;

        var coarse = model.CalculateLineSourceReceptorConcentration(
            centerLat, startLon, centerLat, endLon,
            lineWidth: 5, lineHeight: 1, emissionRate: 10,
            receptorLat: receptorLat, receptorLon: centerLon,
            segmentLength: 200, pollutant: "PM2.5");
        var fine = model.CalculateLineSourceReceptorConcentration(
            centerLat, startLon, centerLat, endLon,
            lineWidth: 5, lineHeight: 1, emissionRate: 10,
            receptorLat: receptorLat, receptorLon: centerLon,
            segmentLength: 5, pollutant: "PM2.5");

        coarse.Should().BeGreaterThan(0);
        fine.Should().BeGreaterThan(0);
        coarse.Should().BeApproximately(fine, fine * 0.01,
            "连续线积分不应因分段中点离散而显示成多个相连点源");
    }

    [Fact]
    public void 线源受体贡献_超过最大扩散距离时与网格路径一致返回0()
    {
        var model = new GaussianPlumeModel(3.0, 90.0, "D");
        const double centerLat = 39.90;
        const double centerLon = 116.40;
        var startLon = centerLon - MetersToLonDegrees(50, centerLat);
        var endLon = centerLon + MetersToLonDegrees(50, centerLat);
        var receptorLon = centerLon - MetersToLonDegrees(
            model.CalculateMaxDiffusionDistance() + 1_000,
            centerLat);

        var receptor = model.CalculateLineSourceReceptorConcentration(
            centerLat, startLon, centerLat, endLon,
            lineWidth: 5, lineHeight: 1, emissionRate: 10,
            receptorLat: centerLat, receptorLon: receptorLon,
            segmentLength: 10, pollutant: "NOx");
        var field = model.CalculateLineSourceConcentrationField(
            centerLat, startLon, centerLat, endLon,
            lineWidth: 5, lineHeight: 1, emissionRate: 10,
            gridLat: [centerLat], gridLon: [receptorLon],
            segmentLength: 10, pollutant: "NOx");

        receptor.Should().Be(0);
        field[0, 0].Should().Be(0);
    }

    [Fact]
    public void 线源浓度场_求积点不应逐个分配完整网格矩阵()
    {
        var model = new GaussianPlumeModel(3.0, 0.0, "D");
        const double centerLat = 39.90;
        const double centerLon = 116.40;
        var startLon = centerLon - MetersToLonDegrees(100, centerLat);
        var endLon = centerLon + MetersToLonDegrees(100, centerLat);
        var gridLat = Enumerable.Range(0, 40)
            .Select(index => centerLat - 0.002 + index * 0.0001)
            .ToArray();
        var gridLon = Enumerable.Range(0, 40)
            .Select(index => centerLon - 0.002 + index * 0.0001)
            .ToArray();

        // 先预热 JIT，再只统计一次浓度场计算的线程分配量。
        _ = model.CalculateLineSourceConcentrationField(
            centerLat, startLon, centerLat, endLon,
            lineWidth: 5, lineHeight: 1, emissionRate: 10,
            gridLat: gridLat, gridLon: gridLon,
            segmentLength: 25, pollutant: "PM2.5");
        var before = GC.GetAllocatedBytesForCurrentThread();

        var field = model.CalculateLineSourceConcentrationField(
            centerLat, startLon, centerLat, endLon,
            lineWidth: 5, lineHeight: 1, emissionRate: 10,
            gridLat: gridLat, gridLon: gridLon,
            segmentLength: 25, pollutant: "PM2.5");

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        field.Should().NotBeNull();
        allocated.Should().BeLessThan(100_000,
            "线源积分应直接累加到结果矩阵，不应为每个 Gauss-Legendre 求积点分配一个完整网格");
    }

    [Fact]
    public void 反推_等价于正推的逆运算()
    {
        var m = new GaussianPlumeModel(3.0, 0.0, "D");
        const double x = 1000, y = 30, z = 1.5, H = 50, Q = 2.5;

        // 正向（无衰减）浓度：term1*term2*term3
        var (sy, sz) = m.CalculateSigma(x);
        var qUg = Q * 1e6;
        var t1 = qUg / (2 * Math.PI * m.WindSpeed * sy * sz);
        var t2 = Math.Exp(-y * y / (2 * sy * sy));
        var t3 = Math.Exp(-(z - H) * (z - H) / (2 * sz * sz))
               + Math.Exp(-(z + H) * (z + H) / (2 * sz * sz));
        var concNoDecay = t1 * t2 * t3;

        var qBack = m.CalculateEmissionRateFromConcentration(x, y, z, H, concNoDecay);
        qBack.Should().BeApproximately(Q, 1e-10);
    }

    [Fact]
    public void 等效面源反算_排放速率线性于浓度()
    {
        var m = new GaussianPlumeModel(3.0, 0.0, "D");
        var q1 = m.CalculateEquivalentEmissionRate(50, 200, 100, 10);
        var q2 = m.CalculateEquivalentEmissionRate(100, 200, 100, 10);
        q2.Should().BeApproximately(q1 * 2, 1e-15);
    }

    [Fact]
    public void 贡献排名_按浓度降序()
    {
        var sources = new[]
        {
            new SourceInfo(1, "A"),
            new SourceInfo(2, "B"),
            new SourceInfo(3, "C"),
        };
        var conc = new[] { 3.0, 7.0, 2.0 };
        var ranked = ContributionAnalysis.Rank(sources, conc);
        ranked.Select(r => r.SourceName).Should().Equal("B", "A", "C");
        ranked.Sum(r => r.Percentage).Should().BeApproximately(100, 1e-10);
    }

    [Fact]
    public void 贡献排名_总浓度为0时所有百分比为0()
    {
        var sources = new[] { new SourceInfo(1, "A"), new SourceInfo(2, "B") };
        var ranked = ContributionAnalysis.Rank(sources, new[] { 0.0, 0.0 });
        ranked.Should().OnlyContain(r => r.Percentage == 0);
    }

    private static void AssertMatrixApproximately(double[,] actual, double[,] expected, double relativeTolerance)
    {
        actual.GetLength(0).Should().Be(expected.GetLength(0));
        actual.GetLength(1).Should().Be(expected.GetLength(1));
        for (var i = 0; i < actual.GetLength(0); i++)
            for (var j = 0; j < actual.GetLength(1); j++)
                actual[i, j].Should().BeApproximately(
                    expected[i, j],
                    Math.Max(1e-12, Math.Abs(expected[i, j]) * relativeTolerance),
                    $"cell [{i},{j}]");
    }

    private static double LineLengthMeters(double startLat, double startLon, double endLat, double endLon)
    {
        var lonToM = 111_000.0 * Math.Cos((startLat + endLat) / 2 * Math.PI / 180.0);
        var dx = (endLon - startLon) * lonToM;
        var dy = (endLat - startLat) * 111_000.0;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double MetersToLonDegrees(double meters, double latitude) =>
        meters / (111_000.0 * Math.Cos(latitude * Math.PI / 180.0));
}
