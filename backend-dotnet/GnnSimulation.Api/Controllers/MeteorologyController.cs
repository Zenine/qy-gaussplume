using GnnSimulation.Api.Dtos;
using GnnSimulation.Api.Mapping;
using GnnSimulation.Api.Services;
using GnnSimulation.Data;
using GnnSimulation.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GnnSimulation.Api.Controllers;

[ApiController]
[Route("api/meteorology")]
public class MeteorologyController : ControllerBase
{
    private readonly GnnDbContext _db;

    public MeteorologyController(GnnDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MeteorologyDto>>> List(
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 100,
        [FromQuery] string? regionKey = null,
        CancellationToken ct = default)
    {
        var q = _db.Meteorology.AsNoTracking();
        var region = await RegionCatalog.FindAsync(_db, regionKey, ct);
        if (RegionCatalog.IsInvalidRequestedRegion(regionKey, region))
            return BadRequest(new { detail = "无效的区域" });
        if (region is not null)
        {
            q = q.Where(x => _db.RegionMeteorologies.Any(r => r.RegionId == region.Id && r.MeteorologyId == x.Id));
        }
        var items = await q.OrderBy(x => x.Id).Skip(skip).Take(limit).ToListAsync(ct);
        return Ok(items.Select(x => x.ToDto()).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MeteorologyDto>> Get(int id, CancellationToken ct)
    {
        var e = await _db.Meteorology.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return e is null ? NotFound(new { detail = "气象场未找到" }) : e.ToDto();
    }

    [HttpPost]
    public async Task<ActionResult<MeteorologyDto>> Create(
        [FromBody] MeteorologyCreateDto dto,
        [FromQuery] string? regionKey = null,
        CancellationToken ct = default)
    {
        var region = await RegionCatalog.FindAsync(_db, regionKey, ct);
        if (RegionCatalog.IsInvalidRequestedRegion(regionKey, region))
            return BadRequest(new { detail = "无效的区域" });

        var entity = dto.ToEntity();
        _db.Meteorology.Add(entity);
        await _db.SaveChangesAsync(ct);
        await BindToRegionAsync(entity.Id, region, ct);
        return CreatedAtAction(nameof(Get), new { id = entity.Id }, entity.ToDto());
    }

    [HttpPost("batch")]
    public async Task<ActionResult<IReadOnlyList<MeteorologyDto>>> CreateBatch(
        [FromBody] List<MeteorologyCreateDto> items,
        [FromQuery] string? regionKey = null,
        CancellationToken ct = default)
    {
        var region = await RegionCatalog.FindAsync(_db, regionKey, ct);
        if (RegionCatalog.IsInvalidRequestedRegion(regionKey, region))
            return BadRequest(new { detail = "无效的区域" });

        var entities = items.Select(x => x.ToEntity()).ToList();
        _db.Meteorology.AddRange(entities);
        await _db.SaveChangesAsync(ct);
        foreach (var entity in entities) await BindToRegionAsync(entity.Id, region, ct);
        return Ok(entities.Select(x => x.ToDto()).ToList());
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<MeteorologyDto>> Update(
        int id,
        [FromBody] MeteorologyUpdateDto dto,
        CancellationToken ct)
    {
        var entity = await _db.Meteorology.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(new { detail = "气象场未找到" });

        entity.ApplyUpdate(dto);
        await _db.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    private async Task BindToRegionAsync(int meteorologyId, Region? region, CancellationToken ct)
    {
        if (region is null) return;
        _db.RegionMeteorologies.Add(new RegionMeteorology { RegionId = region.Id, MeteorologyId = meteorologyId });
        await _db.SaveChangesAsync(ct);
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<object>> Delete(int id, CancellationToken ct)
    {
        var entity = await _db.Meteorology.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(new { detail = "气象场未找到" });

        _db.Meteorology.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return new { message = "气象场已删除", id };
    }
}
