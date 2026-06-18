using GnnSimulation.Data.Entities;

namespace GnnSimulation.Api.Services;

internal static class GridBuilder
{
    private const double MetersPerDegree = 111_000.0;
    private const double MinimumPaddingMeters = 500.0;
    private const double MinimumRangeMeters = 1_000.0;

    public record Grid(double[] Lat, double[] Lon);

    // 基于源/受体的外包框 + 有限余量构建网格。
    // domain_size 保留为请求参数和结果过期判断口径，但不强制铺满；
    // 点位很近时只多覆盖受体点周边一些面积，点位更远时优先覆盖全部点位。
    // 这里保留 50-500 的单轴网格点夹紧，避免极小分辨率导致响应过大。
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
        var lonMeter = MetersPerDegree * Math.Cos(centerLat * Math.PI / 180.0);
        var latSpanMeters = Math.Max(0, (maxLat - minLat) * MetersPerDegree);
        var lonSpanMeters = Math.Max(0, (maxLon - minLon) * lonMeter);
        var maxSpanMeters = Math.Max(latSpanMeters, lonSpanMeters);
        var paddingMeters = Math.Max(
            MinimumPaddingMeters,
            Math.Max(gridResolution * 5, maxSpanMeters * 0.2));
        var desiredLatRangeMeters = Math.Max(MinimumRangeMeters, latSpanMeters + paddingMeters * 2);
        var desiredLonRangeMeters = Math.Max(MinimumRangeMeters, lonSpanMeters + paddingMeters * 2);
        var requiredLatRange = desiredLatRangeMeters / MetersPerDegree;
        var requiredLonRange = desiredLonRangeMeters / lonMeter;

        var latPoints = Math.Clamp((int)(desiredLatRangeMeters / gridResolution) + 1, 50, 500);
        var lonPoints = Math.Clamp((int)(desiredLonRangeMeters / gridResolution) + 1, 50, 500);

        return new Grid(
            Linspace(centerLat - requiredLatRange / 2, centerLat + requiredLatRange / 2, latPoints),
            Linspace(centerLon - requiredLonRange / 2, centerLon + requiredLonRange / 2, lonPoints));
    }

    // np.linspace 等价实现（包含两端点）。多风向并行和单风向网格都复用它，
    // 以避免不同路径生成的坐标轴存在浮点步长差异。
    public static double[] Linspace(double start, double stop, int num)
    {
        if (num <= 0) return Array.Empty<double>();
        if (num == 1) return new[] { start };
        var result = new double[num];
        var step = (stop - start) / (num - 1);
        for (var i = 0; i < num; i++) result[i] = start + step * i;
        return result;
    }

    // double[,] → double[][] 转换，便于 System.Text.Json 序列化成前端期望的二维数组。
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
        // 多个排放源的浓度场按网格逐点线性叠加，符合高斯烟羽稳态叠加假设。
        var n0 = target.GetLength(0);
        var n1 = target.GetLength(1);
        for (var i = 0; i < n0; i++)
            for (var j = 0; j < n1; j++)
                target[i, j] += source[i, j];
    }

    public static double Sum(double[,] m)
    {
        // 用于源贡献统计：表示该源在整个模拟域上的总浓度量级。
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
        // 用于前端结果面板的峰值展示；空矩阵或全未赋值时返回 0。
        var best = double.MinValue;
        var n0 = m.GetLength(0);
        var n1 = m.GetLength(1);
        for (var i = 0; i < n0; i++)
            for (var j = 0; j < n1; j++)
                if (m[i, j] > best) best = m[i, j];
        return best == double.MinValue ? 0 : best;
    }
}
