using FluentAssertions;
using GnnSimulation.Api.Services;
using GnnSimulation.Data.Entities;
using GnnSimulation.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GnnSimulation.Tests.Api;

public class RegionCatalogTests
{
    [Fact]
    public async Task 启动回填_历史无区域归属数据默认绑定到南湖区()
    {
        using var fixture = new SqliteInMemoryFixture();
        await using var db = fixture.CreateContext();

        db.EmissionSources.Add(new EmissionSource { Name = "历史源", Latitude = 30, Longitude = 120, Height = 10 });
        db.Receptors.Add(new Receptor { Name = "历史受体", Latitude = 30, Longitude = 120, Height = 1.5 });
        db.Meteorology.Add(new Meteorology { Name = "历史气象", WindSpeed = 3, WindDirection = 0 });
        await db.SaveChangesAsync();

        await RegionCatalog.EnsureSeededAsync(db);
        await RegionCatalog.BackfillDefaultRegionAsync(db);

        var nanhu = await db.Regions.SingleAsync(x => x.Key == "nanhu");
        (await db.RegionEmissionSources.CountAsync(x => x.RegionId == nanhu.Id)).Should().Be(1);
        (await db.RegionReceptors.CountAsync(x => x.RegionId == nanhu.Id)).Should().Be(1);
        (await db.RegionMeteorologies.CountAsync(x => x.RegionId == nanhu.Id)).Should().Be(1);
    }

    [Fact]
    public async Task 启动回填_已有任意区域归属的数据不会重复绑定到南湖区()
    {
        using var fixture = new SqliteInMemoryFixture();
        await using var db = fixture.CreateContext();

        await RegionCatalog.EnsureSeededAsync(db);
        var xiuzhou = await db.Regions.SingleAsync(x => x.Key == "xiuzhou");
        var source = new EmissionSource { Name = "秀洲源", Latitude = 30, Longitude = 120, Height = 10 };
        db.EmissionSources.Add(source);
        await db.SaveChangesAsync();
        db.RegionEmissionSources.Add(new RegionEmissionSource { RegionId = xiuzhou.Id, SourceId = source.Id });
        await db.SaveChangesAsync();

        await RegionCatalog.BackfillDefaultRegionAsync(db);

        var nanhu = await db.Regions.SingleAsync(x => x.Key == "nanhu");
        (await db.RegionEmissionSources.CountAsync(x => x.SourceId == source.Id)).Should().Be(1);
        (await db.RegionEmissionSources.AnyAsync(x => x.SourceId == source.Id && x.RegionId == nanhu.Id)).Should().BeFalse();
    }
}
