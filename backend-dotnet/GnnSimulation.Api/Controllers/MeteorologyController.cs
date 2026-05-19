using GnnSimulation.Api.Dtos;
using GnnSimulation.Api.Mapping;
using GnnSimulation.Data;
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
    public async Task<IReadOnlyList<MeteorologyDto>> List(
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 100,
        CancellationToken ct = default)
    {
        var items = await _db.Meteorology
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Skip(skip)
            .Take(limit)
            .ToListAsync(ct);
        return items.Select(x => x.ToDto()).ToList();
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
        CancellationToken ct)
    {
        var entity = dto.ToEntity();
        _db.Meteorology.Add(entity);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = entity.Id }, entity.ToDto());
    }

    [HttpPost("batch")]
    public async Task<ActionResult<IReadOnlyList<MeteorologyDto>>> CreateBatch(
        [FromBody] List<MeteorologyCreateDto> items,
        CancellationToken ct)
    {
        var entities = items.Select(x => x.ToEntity()).ToList();
        _db.Meteorology.AddRange(entities);
        await _db.SaveChangesAsync(ct);
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
