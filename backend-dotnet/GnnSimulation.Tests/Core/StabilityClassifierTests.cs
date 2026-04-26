using FluentAssertions;
using GnnSimulation.Core.Atmosphere;

namespace GnnSimulation.Tests.Core;

public class StabilityClassifierTests
{
    [Theory]
    [InlineData(1.5, 800, "A")]
    [InlineData(1.5, 500, "A-B")]
    [InlineData(1.5, 200, "B")]
    [InlineData(2.5, 800, "A-B")]
    [InlineData(4.0, 800, "B")]
    [InlineData(4.0, 400, "B-C")]
    [InlineData(5.5, 800, "C")]
    [InlineData(5.5, 300, "C-D")]
    [InlineData(7.0, 999, "D")]
    public void 辐照场景下的分类(double windSpeed, double solar, string expected)
    {
        StabilityClassifier.Classify(windSpeed, solarRadiation: solar).Should().Be(expected);
    }

    [Theory]
    [InlineData(2.0, 0.1, false, "F")]
    [InlineData(2.0, 0.5, false, "E")]
    [InlineData(2.0, 0.8, false, "D")]
    public void 夜间按云量分类(double windSpeed, double cloud, bool day, string expected)
    {
        StabilityClassifier.Classify(windSpeed, cloudCover: cloud, isDaytime: day).Should().Be(expected);
    }

    [Fact]
    public void 完全无信息时默认D()
    {
        StabilityClassifier.Classify(3.0).Should().Be("D");
    }
}
