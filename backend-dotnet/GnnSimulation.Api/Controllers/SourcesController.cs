using GnnSimulation.Api.Dtos;
using GnnSimulation.Api.Mapping;
using GnnSimulation.Api.Services;
using GnnSimulation.Data;
using GnnSimulation.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GnnSimulation.Api.Controllers;

[ApiController]
[Route("api/sources")]
public class SourcesController : ControllerBase
{
    private readonly GnnDbContext _db;

    public SourcesController(GnnDbContext db) => _db = db;

    [HttpGet]
    public async Task<IReadOnlyList<EmissionSourceDto>> List(
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 100,
        CancellationToken ct = default)
    {
        var items = await _db.EmissionSources
            .AsNoTracking()
            .Include(x => x.Pollutants)
            .OrderBy(x => x.Id)
            .Skip(skip)
            .Take(limit)
            .ToListAsync(ct);
        return items.Select(x => x.ToDto()).ToList();
    }

    [HttpGet("pollutant-types")]
    public IReadOnlyList<PollutantTypeInfoDto> PollutantTypes() =>
        PollutantCatalog.Pollutants
            .Select(kv => new PollutantTypeInfoDto(kv.Key, kv.Value.Name, kv.Value.Unit, kv.Value.Description))
            .ToList();

    [HttpGet("marker-symbols")]
    public IReadOnlyList<MarkerSymbolInfoDto> MarkerSymbols() =>
        PollutantCatalog.MarkerSymbols
            .Select(kv => new MarkerSymbolInfoDto(kv.Key, kv.Value.Name, kv.Value.Icon))
            .ToList();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmissionSourceDto>> Get(int id, CancellationToken ct)
    {
        var e = await _db.EmissionSources
            .AsNoTracking()
            .Include(x => x.Pollutants)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return e is null ? NotFound(new { detail = "排放源未找到" }) : e.ToDto();
    }

    [HttpPost]
    public async Task<ActionResult<EmissionSourceDto>> Create(
        [FromBody] EmissionSourceCreateDto dto,
        CancellationToken ct)
    {
        var entity = dto.ToEntity();
        _db.EmissionSources.Add(entity);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = entity.Id }, entity.ToDto());
    }

    [HttpPost("batch")]
    public async Task<ActionResult<IReadOnlyList<EmissionSourceDto>>> CreateBatch(
        [FromBody] List<EmissionSourceCreateDto> items,
        CancellationToken ct)
    {
        var entities = items.Select(x => x.ToEntity()).ToList();
        _db.EmissionSources.AddRange(entities);
        await _db.SaveChangesAsync(ct);
        return Ok(entities.Select(x => x.ToDto()).ToList());
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<EmissionSourceDto>> Update(
        int id,
        [FromBody] EmissionSourceUpdateDto dto,
        CancellationToken ct)
    {
        var entity = await _db.EmissionSources
            .Include(x => x.Pollutants)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(new { detail = "排放源未找到" });

        entity.ApplyUpdate(dto);

        // 非 null → 整体替换污染物列表
        if (dto.Pollutants is not null)
        {
            _db.PollutantEmissions.RemoveRange(entity.Pollutants);
            entity.Pollutants = dto.Pollutants.Select(p => p.ToEntity()).ToList();
        }

        await _db.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<object>> Delete(int id, CancellationToken ct)
    {
        var entity = await _db.EmissionSources.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(new { detail = "排放源未找到" });

        _db.EmissionSources.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return new { message = "排放源已删除", id };
    }

    [HttpPost("{id:int}/pollutants")]
    public async Task<ActionResult<PollutantEmissionDto>> AddPollutant(
        int id,
        [FromBody] PollutantEmissionCreateDto dto,
        CancellationToken ct)
    {
        var source = await _db.EmissionSources.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (source is null)
            return NotFound(new { detail = "排放源未找到" });

        if (!PollutantCatalog.Pollutants.ContainsKey(dto.PollutantType))
            return BadRequest(new { detail = "无效的污染物类型" });

        // 已存在同类型 → 覆盖
        var existing = await _db.PollutantEmissions
            .FirstOrDefaultAsync(p => p.SourceId == id && p.PollutantType == dto.PollutantType, ct);
        if (existing is not null)
        {
            existing.EmissionRate = dto.EmissionRate;
            existing.Concentration = dto.Concentration;
            await _db.SaveChangesAsync(ct);
            return existing.ToDto();
        }

        var entity = dto.ToEntity();
        entity.SourceId = id;
        _db.PollutantEmissions.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    [HttpDelete("{id:int}/pollutants/{pollutantId:int}")]
    public async Task<ActionResult<object>> DeletePollutant(int id, int pollutantId, CancellationToken ct)
    {
        var p = await _db.PollutantEmissions
            .FirstOrDefaultAsync(x => x.Id == pollutantId && x.SourceId == id, ct);
        if (p is null)
            return NotFound(new { detail = "污染物排放记录未找到" });

        _db.PollutantEmissions.Remove(p);
        await _db.SaveChangesAsync(ct);
        return new { message = "污染物排放记录已删除", id = pollutantId };
    }

    private const string XlsxMediaType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet("template/{sourceType}")]
    public IActionResult DownloadTemplate(string sourceType)
    {
        if (!ExcelService.IsValidSourceType(sourceType))
            return BadRequest(new { detail = "无效的排放源类型" });
        var bytes = ExcelService.BuildSourceTemplate(sourceType);
        return File(bytes, XlsxMediaType, $"{sourceType}_template.xlsx");
    }

    [HttpPost("import/{sourceType}")]
    public async Task<ActionResult<object>> Import(string sourceType, IFormFile file, CancellationToken ct)
    {
        if (!ExcelService.IsValidSourceType(sourceType))
            return BadRequest(new { detail = "无效的排放源类型" });
        if (file is null || file.Length == 0)
            return BadRequest(new { detail = "文件为空" });

        await using var stream = file.OpenReadStream();
        var (items, errors) = ExcelService.ParseSources(stream, sourceType);

        if (items.Count > 0)
        {
            _db.EmissionSources.AddRange(items);
            await _db.SaveChangesAsync(ct);
        }

        return new
        {
            imported_count = items.Count,
            errors = errors.Count > 0 ? errors : null,
            message = $"成功导入 {items.Count} 条记录",
        };
    }
}
