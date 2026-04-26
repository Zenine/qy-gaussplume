namespace GnnSimulation.Core.Atmosphere;

// Pasquill-Gifford 经验公式参数: σy = ay·x^by, σz = az·x^bz
public readonly record struct PasquillGiffordParams(double Ay, double By, double Az, double Bz);

public static class PasquillGifford
{
    public static readonly IReadOnlyDictionary<string, PasquillGiffordParams> Params =
        new Dictionary<string, PasquillGiffordParams>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = new(0.527, 0.865, 0.28, 0.90),
            ["B"] = new(0.371, 0.866, 0.23, 0.85),
            ["C"] = new(0.209, 0.897, 0.22, 0.80),
            ["D"] = new(0.128, 0.905, 0.20, 0.76),
            ["E"] = new(0.098, 0.902, 0.15, 0.73),
            ["F"] = new(0.065, 0.902, 0.12, 0.67),
        };

    public static PasquillGiffordParams Get(string stabilityClass)
    {
        if (!Params.TryGetValue(stabilityClass, out var p))
            throw new ArgumentException($"无效的稳定度等级: {stabilityClass}", nameof(stabilityClass));
        return p;
    }

    // 混合层高度修正系数
    public static readonly IReadOnlyDictionary<string, double> MixingFactors =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = 1.0, ["B"] = 0.9, ["C"] = 0.8, ["D"] = 0.6, ["E"] = 0.4, ["F"] = 0.2,
        };

    // 干沉降空气动力学阻力系数（乘 Ra）
    public static readonly IReadOnlyDictionary<string, double> AerodynamicResistanceFactors =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = 0.5, ["B"] = 0.7, ["C"] = 1.0, ["D"] = 1.5, ["E"] = 2.0, ["F"] = 3.0,
        };

    // 最大扩散距离稳定度系数
    public static readonly IReadOnlyDictionary<string, double> MaxDistanceFactors =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = 1.5, ["B"] = 1.3, ["C"] = 1.1, ["D"] = 1.0, ["E"] = 0.8, ["F"] = 0.6,
        };
}
