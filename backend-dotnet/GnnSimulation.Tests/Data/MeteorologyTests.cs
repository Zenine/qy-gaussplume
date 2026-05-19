using FluentAssertions;
using GnnSimulation.Data.Entities;
using GnnSimulation.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GnnSimulation.Tests.Data;

public class MeteorologyTests
{
    [Fact]
    public async Task 默认气象参数应符合Python版约定()
    {
        using var fixture = new SqliteInMemoryFixture();
        using var ctx = fixture.CreateContext();

        var m = new Meteorology { Name = "默认" };
        ctx.Meteorology.Add(m);
        await ctx.SaveChangesAsync();

        var saved = await ctx.Meteorology.AsNoTracking().SingleAsync();
        saved.WindSpeed.Should().Be(2.0);
        saved.WindDirection.Should().Be(0.0);
        saved.BoundaryLayerHeight.Should().Be(1000.0);
        saved.StabilityClass.Should().Be("D");
        saved.Temperature.Should().Be(293.15);
        saved.Humidity.Should().Be(50.0);
        saved.CloudCover.Should().Be(0.0);
        saved.Precipitation.Should().Be(0.0);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("B")]
    [InlineData("C")]
    [InlineData("D")]
    [InlineData("E")]
    [InlineData("F")]
    public async Task 稳定度类别_ABCDEF_全部可保存(string stability)
    {
        using var fixture = new SqliteInMemoryFixture();
        using var ctx = fixture.CreateContext();

        ctx.Meteorology.Add(new Meteorology { Name = $"Stab-{stability}", StabilityClass = stability });
        await ctx.SaveChangesAsync();

        (await ctx.Meteorology.AsNoTracking().SingleAsync()).StabilityClass.Should().Be(stability);
    }

    [Fact]
    public async Task 风向可覆盖0到360任意角度()
    {
        using var fixture = new SqliteInMemoryFixture();
        using var ctx = fixture.CreateContext();

        foreach (var deg in new[] { 0.0, 45.0, 90.0, 180.0, 270.0, 359.99 })
        {
            ctx.Meteorology.Add(new Meteorology { Name = $"Wind-{deg}", WindDirection = deg });
        }
        await ctx.SaveChangesAsync();

        var dirs = await ctx.Meteorology.AsNoTracking()
            .OrderBy(x => x.WindDirection)
            .Select(x => x.WindDirection)
            .ToListAsync();
        dirs.Should().Equal(0.0, 45.0, 90.0, 180.0, 270.0, 359.99);
    }
}
