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
}
