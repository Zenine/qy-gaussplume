using GnnSimulation.Data.Entities;

namespace GnnSimulation.Api.Services;

internal static class GridBuilder
{
    private const double MetersPerDegree = 111_000.0;

    public record Grid(double[] Lat, double[] Lon);

    // 基于源/受体的外包框 + domain_size + grid_resolution 构建方形网格
    // 对齐 Python simulation.py 中的网格构建逻辑
    public static Grid Build(
        IReadOnlyList<EmissionSource> sources,
        IReadOnlyList<Receptor> receptors,
        double gridResolution,
        double domainSize)
    {
        var lats = new List<double>();
        var lons = new List<double>();
        foreach (var s in sources) { lats.Add(s.Latitude); lons.Add(s.Longitude); }
        foreach (var r in receptors) { lats.Add(r.Latitude); lons.Add(r.Longitude); }
        if (lats.Count == 0)
            throw new ArgumentException("没有有效的坐标数据");

        var minLat = lats.Min(); var maxLat = lats.Max();
        var minLon = lons.Min(); var maxLon = lons.Max();
        var centerLat = (minLat + maxLat) / 2;
        var centerLon = (minLon + maxLon) / 2;
        var latSpan = maxLat - minLat;
        var lonSpan = maxLon - minLon;

        var requiredLatRange = Math.Max(domainSize / MetersPerDegree, latSpan * 1.5 + 0.1);
        var requiredLonRange = Math.Max(
            domainSize / (MetersPerDegree * Math.Cos(centerLat * Math.PI / 180.0)),
            lonSpan * 1.5 + 0.1);

        var gridPoints = (int)(Math.Max(requiredLatRange, requiredLonRange) * MetersPerDegree / gridResolution);
        gridPoints = Math.Clamp(gridPoints, 50, 500);

        return new Grid(
            Linspace(centerLat - requiredLatRange / 2, centerLat + requiredLatRange / 2, gridPoints),
            Linspace(centerLon - requiredLonRange / 2, centerLon + requiredLonRange / 2, gridPoints));
    }

    // np.linspace 等价实现（包含两端点）
    public static double[] Linspace(double start, double stop, int num)
    {
        if (num <= 0) return Array.Empty<double>();
        if (num == 1) return new[] { start };
        var result = new double[num];
        var step = (stop - start) / (num - 1);
        for (var i = 0; i < num; i++) result[i] = start + step * i;
        return result;
    }

    // double[,] → double[][] 转换，便于 JSON 序列化
    public static double[][] ToJagged(double[,] src)
    {
        var n0 = src.GetLength(0);
        var n1 = src.GetLength(1);
        var result = new double[n0][];
        for (var i = 0; i < n0; i++)
        {
            var row = new double[n1];
            for (var j = 0; j < n1; j++) row[j] = src[i, j];
            result[i] = row;
        }
        return result;
    }

    public static void AddInPlace(double[,] target, double[,] source)
    {
        var n0 = target.GetLength(0);
        var n1 = target.GetLength(1);
        for (var i = 0; i < n0; i++)
            for (var j = 0; j < n1; j++)
                target[i, j] += source[i, j];
    }

    public static double Sum(double[,] m)
    {
        var acc = 0.0;
        var n0 = m.GetLength(0);
        var n1 = m.GetLength(1);
        for (var i = 0; i < n0; i++)
            for (var j = 0; j < n1; j++)
                acc += m[i, j];
        return acc;
    }

    public static double Max(double[,] m)
    {
        var best = double.MinValue;
        var n0 = m.GetLength(0);
        var n1 = m.GetLength(1);
        for (var i = 0; i < n0; i++)
            for (var j = 0; j < n1; j++)
                if (m[i, j] > best) best = m[i, j];
        return best == double.MinValue ? 0 : best;
    }
}
