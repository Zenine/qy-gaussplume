using FluentAssertions;
using GnnSimulation.Data.Entities;
using GnnSimulation.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GnnSimulation.Tests.Data;

public class EmissionSourceTests
{
    [Fact]
    public async Task 插入点源_应保留所有默认值()
    {
        using var fixture = new SqliteInMemoryFixture();
        using var ctx = fixture.CreateContext();

        var source = new EmissionSource
        {
            Name = "1号烟囱",
            SourceType = "point",
            Latitude = 39.9,
            Longitude = 116.4,
            Height = 50.0,
        };
        ctx.EmissionSources.Add(source);
        await ctx.SaveChangesAsync();

        var saved = await ctx.EmissionSources.AsNoTracking().SingleAsync();
        saved.Name.Should().Be("1号烟囱");
        saved.SourceType.Should().Be("point");
        saved.Temperature.Should().Be(400.0);
        saved.Velocity.Should().Be(15.0);
        saved.Diameter.Should().Be(2.0);
        saved.AreaShape.Should().Be("rectangle");
        saved.LineType.Should().Be("straight");
        saved.MarkerSymbol.Should().Be("factory");
        saved.MarkerColor.Should().Be("#FF5722");
        saved.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task 创建时_时间戳应被自动填充()
    {
        using var fixture = new SqliteInMemoryFixture();
        using var ctx = fixture.CreateContext();

        var before = DateTime.UtcNow.AddSeconds(-2);
        var source = new EmissionSource { Name = "A", Latitude = 0, Longitude = 0, Height = 0 };
        ctx.EmissionSources.Add(source);
        await ctx.SaveChangesAsync();

        var saved = await ctx.EmissionSources.AsNoTracking().SingleAsync();
        saved.CreatedAt.Should().BeAfter(before);
        saved.UpdatedAt.Should().BeAfter(before);
    }

    [Fact]
    public async Task 更新时_UpdatedAt应刷新_但CreatedAt保持不变()
    {
        using var fixture = new SqliteInMemoryFixture();

        int id;
        DateTime originalCreated;
        using (var ctx = fixture.CreateContext())
        {
            var source = new EmissionSource { Name = "A", Latitude = 0, Longitude = 0, Height = 0 };
            ctx.EmissionSources.Add(source);
            await ctx.SaveChangesAsync();
            id = source.Id;
            originalCreated = source.CreatedAt;
        }

        await Task.Delay(50);

        using (var ctx = fixture.CreateContext())
        {
            var source = await ctx.EmissionSources.SingleAsync(x => x.Id == id);
            source.Name = "A-修改";
            await ctx.SaveChangesAsync();
        }

        using (var ctx = fixture.CreateContext())
        {
            var saved = await ctx.EmissionSources.AsNoTracking().SingleAsync();
            saved.Name.Should().Be("A-修改");
            saved.CreatedAt.Should().BeCloseTo(originalCreated, TimeSpan.FromMilliseconds(1));
            saved.UpdatedAt.Should().BeAfter(originalCreated);
        }
    }

    [Fact]
    public async Task Name是必填_空字符串可以但Null会异常()
    {
        using var fixture = new SqliteInMemoryFixture();
        using var ctx = fixture.CreateContext();

        // Name 非空约束在 EF Core 层设置了 IsRequired()
        var source = new EmissionSource { Name = null!, Latitude = 0, Longitude = 0, Height = 0 };
        ctx.EmissionSources.Add(source);

        // SQLite 会因 NOT NULL 约束抛出异常
        Func<Task> act = () => ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task 删除源时_关联的污染物排放应级联删除()
    {
        using var fixture = new SqliteInMemoryFixture();
        int sourceId;

        using (var ctx = fixture.CreateContext())
        {
            var source = new EmissionSource
            {
                Name = "测试源",
                Latitude = 0,
                Longitude = 0,
                Height = 0,
                Pollutants = new List<PollutantEmission>
                {
                    new() { PollutantType = "PM2.5", EmissionRate = 1.5 },
                    new() { PollutantType = "NOx", EmissionRate = 2.0 },
                },
            };
            ctx.EmissionSources.Add(source);
            await ctx.SaveChangesAsync();
            sourceId = source.Id;
        }

        using (var ctx = fixture.CreateContext())
        {
            (await ctx.PollutantEmissions.CountAsync()).Should().Be(2);
            var source = await ctx.EmissionSources.SingleAsync(x => x.Id == sourceId);
            ctx.EmissionSources.Remove(source);
            await ctx.SaveChangesAsync();
        }

        using (var ctx = fixture.CreateContext())
        {
            (await ctx.PollutantEmissions.CountAsync()).Should().Be(0);
            (await ctx.EmissionSources.CountAsync()).Should().Be(0);
        }
    }

    [Fact]
    public async Task 线源字段_起止点坐标应正确持久化()
    {
        using var fixture = new SqliteInMemoryFixture();
        using var ctx = fixture.CreateContext();

        var source = new EmissionSource
        {
            Name = "干道线源",
            SourceType = "line",
            Latitude = 39.9,
            Longitude = 116.4,
            Height = 0,
            LineType = "straight",
            StartLon = 116.40,
            StartLat = 39.90,
            EndLon = 116.45,
            EndLat = 39.92,
            LineWidth = 15.0,
            LineSegmentLength = 20.0,
        };
        ctx.EmissionSources.Add(source);
        await ctx.SaveChangesAsync();

        var saved = await ctx.EmissionSources.AsNoTracking().SingleAsync();
        saved.SourceType.Should().Be("line");
        saved.StartLat.Should().Be(39.90);
        saved.EndLon.Should().Be(116.45);
        saved.LineWidth.Should().Be(15.0);
        saved.LineSegmentLength.Should().Be(20.0);
    }
}
