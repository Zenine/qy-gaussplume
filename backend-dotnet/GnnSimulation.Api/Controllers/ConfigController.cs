using GnnSimulation.Api.Dtos;
using GnnSimulation.Api.Mapping;
using GnnSimulation.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GnnSimulation.Api.Controllers;

[ApiController]
[Route("api/config")]
public class ConfigController : ControllerBase
{
    private readonly GnnDbContext _db;

    public ConfigController(GnnDbContext db) => _db = db;

    [HttpGet]
    public async Task<IReadOnlyList<MarkerConfigDto>> List(CancellationToken ct)
    {
        var items = await _db.MarkerConfigs
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
        return items.Select(x => x.ToDto()).ToList();
    }

    [HttpGet("{type}")]
    public async Task<ActionResult<MarkerConfigDto>> Get(string type, CancellationToken ct)
    {
        var e = await _db.MarkerConfigs.AsNoTracking().FirstOrDefaultAsync(x => x.Type == type, ct);
        return e is null ? NotFound(new { detail = "配置未找到" }) : e.ToDto();
    }

    [HttpPost]
    public async Task<ActionResult<MarkerConfigDto>> Create(
        [FromBody] MarkerConfigCreateDto dto,
        CancellationToken ct)
    {
        var exists = await _db.MarkerConfigs.AnyAsync(x => x.Type == dto.Type, ct);
        if (exists)
            return BadRequest(new { detail = "该类型配置已存在" });

        var entity = dto.ToEntity();
        _db.MarkerConfigs.Add(entity);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { type = entity.Type }, entity.ToDto());
    }

    [HttpPut("{type}")]
    public async Task<ActionResult<MarkerConfigDto>> Update(
        string type,
        [FromBody] MarkerConfigUpdateDto dto,
        CancellationToken ct)
    {
        var entity = await _db.MarkerConfigs.FirstOrDefaultAsync(x => x.Type == type, ct);
        if (entity is null)
            return NotFound(new { detail = "配置未找到" });

        entity.ApplyUpdate(dto);
        await _db.SaveChangesAsync(ct);
        return entity.ToDto();
    }
}
