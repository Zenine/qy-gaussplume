using GnnSimulation.Api.Dtos;
using GnnSimulation.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GnnSimulation.Api.Controllers;

[ApiController]
[Route("api/regions")]
public class RegionsController : ControllerBase
{
    private readonly GnnDbContext _db;

    public RegionsController(GnnDbContext db) => _db = db;

    [HttpGet]
    public async Task<IReadOnlyList<RegionDto>> List(CancellationToken ct)
    {
        var regions = await _db.Regions.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync(ct);
        return regions.Select(x => new RegionDto(x.Id, x.Key, x.Name, x.SortOrder)).ToList();
    }
}
