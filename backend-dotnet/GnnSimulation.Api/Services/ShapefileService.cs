using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Esri;
using NetTopologySuite.IO.Esri.Shapefiles.Readers;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

namespace GnnSimulation.Api.Services;

public class ShapefileOptions
{
    public string? Path { get; set; }
    // 对齐 Python 原版默认行为：默认不加载（SHP 文件可能很大，前端按需启用）
    public bool LoadByDefault { get; set; } = false;
}

public record MapBoundsDto(double MinLat, double MinLon, double MaxLat, double MaxLon);

public record MapInfoDto(
    string Crs,
    int FeatureCount,
    IReadOnlyList<string> Columns,
    MapBoundsDto Bounds,
    string? Error = null);

// 读取 Esri Shapefile 并转换到 WGS84 (EPSG:4326)。
// 缓存已加载的 FeatureCollection 以避免重复 IO 与投影转换。
public class ShapefileService
{
    private static readonly MapBoundsDto DefaultChinaBounds = new(30.0, 100.0, 40.0, 120.0);

    private readonly ILogger<ShapefileService> _logger;
    private readonly ShapefileOptions _options;
    private readonly Lazy<LoadedShapefile?> _cache;

    public ShapefileService(IConfiguration config, ILogger<ShapefileService> logger)
    {
        _logger = logger;
        _options = new ShapefileOptions();
        config.GetSection("Shapefile").Bind(_options);
        _cache = new Lazy<LoadedShapefile?>(Load, isThreadSafe: true);
    }

    public bool Exists() => !string.IsNullOrWhiteSpace(_options.Path) && File.Exists(_options.Path);

    public MapBoundsDto GetBounds()
    {
        try
        {
            var shp = _cache.Value;
            return shp?.Bounds ?? DefaultChinaBounds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取 Shapefile 边界失败");
            return DefaultChinaBounds;
        }
    }

    public MapInfoDto GetInfo()
    {
        if (!Exists())
        {
            return new MapInfoDto(
                Crs: "EPSG:4326",
                FeatureCount: 0,
                Columns: Array.Empty<string>(),
                Bounds: DefaultChinaBounds,
                Error: "SHP 文件不存在");
        }

        try
        {
            var shp = _cache.Value;
            if (shp is null)
                return new MapInfoDto("EPSG:4326", 0, Array.Empty<string>(), DefaultChinaBounds, "加载失败");

            return new MapInfoDto(
                Crs: "EPSG:4326",
                FeatureCount: shp.Features.Count,
                Columns: shp.Columns,
                Bounds: shp.Bounds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取 Shapefile 元信息失败");
            return new MapInfoDto("EPSG:4326", 0, Array.Empty<string>(), DefaultChinaBounds, ex.Message);
        }
    }

    // 返回 GeoJSON FeatureCollection 的 JSON 字符串。forceLoad=true 可覆盖 LoadByDefault
    public string GetGeoJson(bool? forceLoad = null)
    {
        var shouldLoad = forceLoad ?? _options.LoadByDefault;
        if (!shouldLoad || !Exists())
            return """{"type":"FeatureCollection","features":[]}""";

        try
        {
            var shp = _cache.Value;
            return shp?.GeoJson ?? """{"type":"FeatureCollection","features":[]}""";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取 Shapefile GeoJSON 失败");
            return """{"type":"FeatureCollection","features":[]}""";
        }
    }

    private LoadedShapefile? Load()
    {
        if (!Exists())
        {
            _logger.LogInformation("Shapefile 不存在或未配置，跳过加载");
            return null;
        }

        var path = _options.Path!;
        _logger.LogInformation("开始加载 Shapefile: {Path}", path);

        var transform = BuildProjectionTransform(Path.ChangeExtension(path, ".prj"));

        // 读取所有要素 + 属性。SkipInvalidShapes 让投影前就有缺陷的几何被跳过，不抛异常
        var options = new ShapefileReaderOptions
        {
            GeometryBuilderMode = GeometryBuilderMode.SkipInvalidShapes,
        };
        var rawFeatures = Shapefile.ReadAllFeatures(path, options).ToList();

        var columns = rawFeatures.Count > 0
            ? rawFeatures[0].Attributes.GetNames().ToList()
            : new List<string>();

        double minLat = double.MaxValue, minLon = double.MaxValue;
        double maxLat = double.MinValue, maxLon = double.MinValue;

        var fc = new FeatureCollection();
        var invalidCount = 0;
        foreach (var raw in rawFeatures)
        {
            var geometry = raw.Geometry;
            if (geometry is null) continue;

            try
            {
                if (transform is not null)
                    geometry = ReprojectGeometry(geometry, transform);
            }
            catch (Exception ex)
            {
                invalidCount++;
                _logger.LogDebug(ex, "Shapefile 要素投影失败，跳过");
                continue;
            }

            fc.Add(new Feature(geometry, raw.Attributes));

            var env = geometry.EnvelopeInternal;
            if (env.MinY < minLat) minLat = env.MinY;
            if (env.MaxY > maxLat) maxLat = env.MaxY;
            if (env.MinX < minLon) minLon = env.MinX;
            if (env.MaxX > maxLon) maxLon = env.MaxX;
        }

        if (invalidCount > 0)
            _logger.LogWarning("Shapefile 投影过程中跳过 {Count} 个无效要素", invalidCount);

        var bounds = fc.Count == 0
            ? DefaultChinaBounds
            : new MapBoundsDto(minLat, minLon, maxLat, maxLon);

        var serializer = NetTopologySuite.IO.GeoJsonSerializer.Create();
        string geoJson;
        using (var sw = new StringWriter())
        using (var jw = new Newtonsoft.Json.JsonTextWriter(sw))
        {
            serializer.Serialize(jw, fc);
            geoJson = sw.ToString();
        }

        _logger.LogInformation("Shapefile 加载完成: {Count} 个要素", fc.Count);

        return new LoadedShapefile(fc, columns, bounds, geoJson);
    }

    // 解析 .prj，若非 WGS84 返回 Albers→WGS84 的坐标转换
    private static MathTransform? BuildProjectionTransform(string prjPath)
    {
        if (!File.Exists(prjPath)) return null;
        var wkt = File.ReadAllText(prjPath);

        // 如果已经是 WGS84 地理坐标系，无需转换
        if (wkt.Contains("GEOGCS", StringComparison.OrdinalIgnoreCase)
            && !wkt.Contains("PROJCS", StringComparison.OrdinalIgnoreCase))
            return null;

        var factory = new CoordinateSystemFactory();
        var source = factory.CreateFromWkt(wkt);
        var target = GeographicCoordinateSystem.WGS84;
        var ctFactory = new CoordinateTransformationFactory();
        var transformation = ctFactory.CreateFromCoordinateSystems(source, target);
        return transformation.MathTransform;
    }

    private static Geometry ReprojectGeometry(Geometry geometry, MathTransform transform)
    {
        var factory = geometry.Factory;

        switch (geometry)
        {
            case Point pt:
                return factory.CreatePoint(ReprojectCoord(pt.Coordinate, transform));
            case LineString ls:
                return factory.CreateLineString(ReprojectCoords(ls.Coordinates, transform));
            case Polygon poly:
                return factory.CreatePolygon(
                    CreateClosedRing(factory, ReprojectCoords(poly.ExteriorRing.Coordinates, transform)),
                    poly.InteriorRings
                        .Select(r => CreateClosedRing(factory, ReprojectCoords(r.Coordinates, transform)))
                        .ToArray());
            case MultiPolygon mp:
                var polys = new Polygon[mp.NumGeometries];
                for (var i = 0; i < mp.NumGeometries; i++)
                    polys[i] = (Polygon)ReprojectGeometry(mp.GetGeometryN(i), transform);
                return factory.CreateMultiPolygon(polys);
            case MultiLineString mls:
                var lines = new LineString[mls.NumGeometries];
                for (var i = 0; i < mls.NumGeometries; i++)
                    lines[i] = (LineString)ReprojectGeometry(mls.GetGeometryN(i), transform);
                return factory.CreateMultiLineString(lines);
            case MultiPoint mpt:
                var points = new Point[mpt.NumGeometries];
                for (var i = 0; i < mpt.NumGeometries; i++)
                    points[i] = (Point)ReprojectGeometry(mpt.GetGeometryN(i), transform);
                return factory.CreateMultiPoint(points);
            default:
                return geometry;
        }
    }

    // 投影后首末点可能因浮点漂移不再完全相等；强制复制首点到末位保证闭合
    private static LinearRing CreateClosedRing(GeometryFactory factory, Coordinate[] coords)
    {
        if (coords.Length == 0)
            return factory.CreateLinearRing();
        if (!coords[0].Equals2D(coords[^1]))
        {
            Array.Resize(ref coords, coords.Length + 1);
            coords[^1] = new Coordinate(coords[0].X, coords[0].Y);
        }
        else
        {
            // 哪怕 "相等" 也强制同一对象，避免 NTS 比较时浮点边界情况
            coords[^1] = new Coordinate(coords[0].X, coords[0].Y);
        }
        return factory.CreateLinearRing(coords);
    }

    private static Coordinate ReprojectCoord(Coordinate c, MathTransform transform)
    {
        var src = new[] { c.X, c.Y };
        var dst = transform.Transform(src);
        return new Coordinate(dst[0], dst[1]);
    }

    private static Coordinate[] ReprojectCoords(IReadOnlyList<Coordinate> coords, MathTransform transform)
    {
        var result = new Coordinate[coords.Count];
        for (var i = 0; i < coords.Count; i++)
            result[i] = ReprojectCoord(coords[i], transform);
        return result;
    }

    private sealed record LoadedShapefile(
        FeatureCollection Features,
        IReadOnlyList<string> Columns,
        MapBoundsDto Bounds,
        string GeoJson);
}
