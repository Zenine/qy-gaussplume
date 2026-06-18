using GnnSimulation.Data;
using GnnSimulation.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GnnSimulation.Api.Services;

public static class RegionCatalog
{
    public static readonly IReadOnlyList<(string Key, string Name, int SortOrder)> FixedRegions = new[]
    {
        ("nanhu", "南湖区", 1),
        ("xiuzhou", "秀洲区", 2),
        ("jiashan", "嘉善县", 3),
        ("tongxiang", "桐乡市", 4),
    };

    public static async Task EnsureSeededAsync(GnnDbContext db, CancellationToken ct = default)
    {
        foreach (var item in FixedRegions)
        {
            var existing = await db.Regions.FirstOrDefaultAsync(x => x.Key == item.Key, ct);
            if (existing is null)
            {
                db.Regions.Add(new Region { Key = item.Key, Name = item.Name, SortOrder = item.SortOrder });
            }
            else
            {
                existing.Name = item.Name;
                existing.SortOrder = item.SortOrder;
            }
        }
        await db.SaveChangesAsync(ct);
    }

    public static async Task<Region?> FindAsync(GnnDbContext db, string? key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        return await db.Regions.FirstOrDefaultAsync(x => x.Key == key, ct);
    }

    public static async Task<Region?> RequireValidAsync(GnnDbContext db, string? key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        return await FindAsync(db, key, ct);
    }

    public static bool IsInvalidRequestedRegion(string? key, Region? region) =>
        !string.IsNullOrWhiteSpace(key) && region is null;

    public static async Task BackfillDefaultRegionAsync(GnnDbContext db, CancellationToken ct = default)
    {
        var defaultRegion = await db.Regions.FirstOrDefaultAsync(x => x.Key == "nanhu", ct);
        if (defaultRegion is null) return;

        var sourceIds = await db.EmissionSources
            .Where(s => !db.RegionEmissionSources.Any(r => r.SourceId == s.Id))
            .Select(s => s.Id)
            .ToListAsync(ct);
        db.RegionEmissionSources.AddRange(sourceIds.Select(id => new RegionEmissionSource
        {
            RegionId = defaultRegion.Id,
            SourceId = id,
        }));

        var receptorIds = await db.Receptors
            .Where(r => !db.RegionReceptors.Any(rr => rr.ReceptorId == r.Id))
            .Select(r => r.Id)
            .ToListAsync(ct);
        db.RegionReceptors.AddRange(receptorIds.Select(id => new RegionReceptor
        {
            RegionId = defaultRegion.Id,
            ReceptorId = id,
        }));

        var meteorologyIds = await db.Meteorology
            .Where(m => !db.RegionMeteorologies.Any(rm => rm.MeteorologyId == m.Id))
            .Select(m => m.Id)
            .ToListAsync(ct);
        db.RegionMeteorologies.AddRange(meteorologyIds.Select(id => new RegionMeteorology
        {
            RegionId = defaultRegion.Id,
            MeteorologyId = id,
        }));

        await db.SaveChangesAsync(ct);
    }
}
