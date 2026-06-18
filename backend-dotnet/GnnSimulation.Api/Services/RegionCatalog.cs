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
}
