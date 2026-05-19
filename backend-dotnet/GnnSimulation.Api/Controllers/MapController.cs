using GnnSimulation.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace GnnSimulation.Api.Controllers;

[ApiController]
[Route("api/map")]
public class MapController : ControllerBase
{
    private readonly ShapefileService _shapefile;

    public MapController(ShapefileService shapefile) => _shapefile = shapefile;

    // GET /api/map/geojson?force=true 可强制返回要素，否则遵循配置 LoadByDefault
    [HttpGet("geojson")]
    public ContentResult GetGeoJson([FromQuery] bool? force = null)
    {
        var json = _shapefile.GetGeoJson(force);
        return Content(json, "application/json");
    }

    [HttpGet("bounds")]
    public MapBoundsDto GetBounds() => _shapefile.GetBounds();

    [HttpGet("info")]
    public MapInfoDto GetInfo() => _shapefile.GetInfo();
}
