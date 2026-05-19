using FluentAssertions;
using GnnSimulation.Data.Entities;
using GnnSimulation.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GnnSimulation.Tests.Data;

public class PollutantEmissionTests
{
    [Fact]
    public async Task 多污染物可以挂在同一个源下()
    {
        using var fixture = new SqliteInMemoryFixture();
        using var ctx = fixture.CreateContext();

        var source = new EmissionSource
        {
            Name = "多污染物源",
            Latitude = 0,
            Longitude = 0,
            Height = 0,
            Pollutants = new List<PollutantEmission>
            {
                new() { PollutantType = "PM2.5", EmissionRate = 1.0 },
                new() { PollutantType = "PM10",  EmissionRate = 2.0 },
                new() { PollutantType = "NOx",   EmissionRate = 3.0 },
                new() { PollutantType = "VOCs",  EmissionRate = 0.5 },
            },
        };
        ctx.EmissionSources.Add(source);
        await ctx.SaveChangesAsync();

        var pollutants = await ctx.PollutantEmissions.AsNoTracking().ToListAsync();
        pollutants.Should().HaveCount(4);
        pollutants.Select(x => x.PollutantType).Should().Contain(new[] { "PM2.5", "PM10", "NOx", "VOCs" });
    }

    [Fact]
    public void PollutantCatalog中应包含全部6种污染物()
    {
        PollutantCatalog.Pollutants.Should().HaveCount(6);
        PollutantCatalog.Pollutants.Keys.Should().BeEquivalentTo(new[] { "PM2.5", "PM10", "TSP", "VOCs", "NOx", "O3" });
        PollutantCatalog.Pollutants["PM2.5"].Unit.Should().Be("g/s");
    }

    [Fact]
    public async Task Concentration字段可为空_用于等效面源()
    {
        using var fixture = new SqliteInMemoryFixture();
        using var ctx = fixture.CreateContext();

        var source = new EmissionSource { Name = "等效面源", Latitude = 0, Longitude = 0, Height = 0 };
        source.Pollutants.Add(new PollutantEmission
        {
            PollutantType = "PM2.5",
            EmissionRate = 0.0,
            Concentration = 75.0,
        });
        ctx.EmissionSources.Add(source);
        await ctx.SaveChangesAsync();

        var saved = await ctx.PollutantEmissions.AsNoTracking().SingleAsync();
        saved.Concentration.Should().Be(75.0);
    }

    [Fact]
    public async Task 通过导航属性_Source可反向访问Pollutants()
    {
        using var fixture = new SqliteInMemoryFixture();

        using (var ctx = fixture.CreateContext())
        {
            var s = new EmissionSource
            {
                Name = "反向引用",
                Latitude = 0,
                Longitude = 0,
                Height = 0,
                Pollutants =
                {
                    new PollutantEmission { PollutantType = "NOx", EmissionRate = 1.0 },
                },
            };
            ctx.EmissionSources.Add(s);
            await ctx.SaveChangesAsync();
        }

        using (var ctx = fixture.CreateContext())
        {
            var source = await ctx.EmissionSources
                .Include(x => x.Pollutants)
                .AsNoTracking()
                .SingleAsync();
            source.Pollutants.Should().HaveCount(1);
            source.Pollutants[0].SourceId.Should().Be(source.Id);
        }
    }
}
