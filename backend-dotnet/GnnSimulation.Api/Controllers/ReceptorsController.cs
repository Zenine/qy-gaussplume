using GnnSimulation.Api.Dtos;
using GnnSimulation.Api.Mapping;
using GnnSimulation.Api.Services;
using GnnSimulation.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GnnSimulation.Api.Controllers;

[ApiController]
[Route("api/receptors")]
public class ReceptorsController : ControllerBase
{
    private readonly GnnDbContext _db;

    public ReceptorsController(GnnDbContext db) => _db = db;

    [HttpGet]
    public async Task<IReadOnlyList<ReceptorDto>> List(
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 100,
        CancellationToken ct = default)
    {
        var items = await _db.Receptors
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Skip(skip)
            .Take(limit)
            .ToListAsync(ct);
        return items.Select(x => x.ToDto()).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ReceptorDto>> Get(int id, CancellationToken ct)
    {
        var e = await _db.Receptors.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return e is null ? NotFound(new { detail = "受体点未找到" }) : e.ToDto();
    }

    [HttpPost]
    public async Task<ActionResult<ReceptorDto>> Create(
        [FromBody] ReceptorCreateDto dto,
        CancellationToken ct)
    {
        var entity = dto.ToEntity();
        _db.Receptors.Add(entity);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = entity.Id }, entity.ToDto());
    }

    [HttpPost("batch")]
    public async Task<ActionResult<IReadOnlyList<ReceptorDto>>> CreateBatch(
        [FromBody] List<ReceptorCreateDto> items,
        CancellationToken ct)
    {
        var entities = items.Select(x => x.ToEntity()).ToList();
        _db.Receptors.AddRange(entities);
        await _db.SaveChangesAsync(ct);
        return Ok(entities.Select(x => x.ToDto()).ToList());
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ReceptorDto>> Update(
        int id,
        [FromBody] ReceptorUpdateDto dto,
        CancellationToken ct)
    {
        var entity = await _db.Receptors.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(new { detail = "受体点未找到" });

        entity.ApplyUpdate(dto);
        await _db.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<object>> Delete(int id, CancellationToken ct)
    {
        var entity = await _db.Receptors.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(new { detail = "受体点未找到" });

        _db.Receptors.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return new { message = "受体点已删除", id };
    }

    private const string XlsxMediaType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet("template")]
    public IActionResult DownloadTemplate()
    {
        var bytes = ExcelService.BuildReceptorTemplate();
        return File(bytes, XlsxMediaType, "receptors_template.xlsx");
    }

    [HttpPost("import")]
    public async Task<ActionResult<object>> Import(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { detail = "文件为空" });

        await using var stream = file.OpenReadStream();
        var (items, errors) = ExcelService.ParseReceptors(stream);

        if (items.Count > 0)
        {
            _db.Receptors.AddRange(items);
            await _db.SaveChangesAsync(ct);
        }

        return new
        {
            imported_count = items.Count,
            errors = errors.Count > 0 ? errors : null,
            message = errors.Count == 0
                ? $"导入成功！共导入 {items.Count} 个受体点"
                : $"导入完成：成功 {items.Count} 个，失败 {errors.Count} 个",
        };
    }

    [HttpPost("export")]
    public async Task<IActionResult> Export([FromBody] List<int> ids, CancellationToken ct)
    {
        var list = await _db.Receptors.AsNoTracking()
            .Where(r => ids.Contains(r.Id))
            .ToListAsync(ct);
        if (list.Count == 0)
            return NotFound(new { detail = "未找到指定的受体点" });

        var bytes = ExcelService.ExportReceptors(list);
        return File(bytes, XlsxMediaType, "receptors_export.xlsx");
    }
}
