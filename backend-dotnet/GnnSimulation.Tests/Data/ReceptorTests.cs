using FluentAssertions;
using GnnSimulation.Data.Entities;
using GnnSimulation.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GnnSimulation.Tests.Data;

public class ReceptorTests
{
    [Fact]
    public async Task 插入受体点_应使用默认图标与颜色()
    {
        using var fixture = new SqliteInMemoryFixture();
        using var ctx = fixture.CreateContext();

        var r = new Receptor
        {
            Name = "学校操场",
            Latitude = 39.91,
            Longitude = 116.41,
            Height = 1.5,
        };
        ctx.Receptors.Add(r);
        await ctx.SaveChangesAsync();

        var saved = await ctx.Receptors.AsNoTracking().SingleAsync();
        saved.MarkerSymbol.Should().Be("monitor");
        saved.MarkerColor.Should().Be("#2196F3");
        saved.IsActive.Should().BeTrue();
        saved.Height.Should().Be(1.5);
    }

    [Fact]
    public async Task 批量插入多个受体_按IsActive过滤应正确()
    {
        using var fixture = new SqliteInMemoryFixture();
        using var ctx = fixture.CreateContext();

        ctx.Receptors.AddRange(
            new Receptor { Name = "A", Latitude = 0, Longitude = 0, Height = 0, IsActive = true },
            new Receptor { Name = "B", Latitude = 0, Longitude = 0, Height = 0, IsActive = false },
            new Receptor { Name = "C", Latitude = 0, Longitude = 0, Height = 0, IsActive = true }
        );
        await ctx.SaveChangesAsync();

        (await ctx.Receptors.CountAsync(x => x.IsActive)).Should().Be(2);
        (await ctx.Receptors.CountAsync()).Should().Be(3);
    }
}
