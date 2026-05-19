namespace GnnSimulation.Core.Atmosphere;

// (Rb 层流底层阻力, Rc 冠层阻力) for dry deposition resistance model
public readonly record struct ResistanceParams(double Rb, double Rc);

// (a, b) for Λ = a·P^b wet scavenging
public readonly record struct ScavengingParams(double A, double B);

public static class PollutantProperties
{
    // 重力沉降速度 (m/s)
    public static readonly IReadOnlyDictionary<string, double> GravitationalSettlingVelocity =
        new Dictionary<string, double>
        {
            ["PM2.5"] = 0.0002,
            ["PM10"] = 0.018,
            ["TSP"] = 0.05,
            ["VOCs"] = 0.0,
            ["NOx"] = 0.0,
            ["SO2"] = 0.0,
            ["CO"] = 0.0,
            ["O3"] = 0.0,
        };

    // (Rb, Rc) 阻力参数
    public static readonly IReadOnlyDictionary<string, ResistanceParams> DryResistance =
        new Dictionary<string, ResistanceParams>
        {
            ["PM2.5"] = new(100, 200),
            ["PM10"] = new(50, 100),
            ["TSP"] = new(30, 80),
            ["VOCs"] = new(200, 800),
            ["NOx"] = new(150, 500),
            ["SO2"] = new(150, 400),
            ["CO"] = new(200, 600),
            ["O3"] = new(150, 600),
        };

    // 湿清除 (a, b)
    public static readonly IReadOnlyDictionary<string, ScavengingParams> WetScavenging =
        new Dictionary<string, ScavengingParams>
        {
            ["PM2.5"] = new(1e-5, 0.8),
            ["PM10"] = new(2e-5, 0.8),
            ["TSP"] = new(3e-5, 0.8),
            ["VOCs"] = new(1e-6, 0.7),
            ["NOx"] = new(5e-6, 0.7),
            ["SO2"] = new(8e-6, 0.7),
            ["CO"] = new(1e-7, 0.6),
            ["O3"] = new(5e-6, 0.7),
        };

    // 化学转化基础速率 k_base
    public static readonly IReadOnlyDictionary<string, double> ChemicalRates =
        new Dictionary<string, double>
        {
            ["PM2.5"] = 2e-5,
            ["PM10"] = 1e-5,
            ["TSP"] = 5e-6,
            ["VOCs"] = 3e-4,
            ["NOx"] = 1.5e-4,
            ["SO2"] = 8e-5,
            ["CO"] = 1e-6,
            ["O3"] = 1e-4,
        };

    // 化学转化增强对应的污染物（VOCs/NOx/O3 受温度湿度影响更强）
    public static readonly HashSet<string> ChemicalEnhancedPollutants =
        new() { "VOCs", "NOx", "O3" };

    // 干沉降温度修正对应的污染物
    public static readonly HashSet<string> TempCorrectedPollutants =
        new() { "VOCs", "NOx", "SO2" };

    public static double GetGravitationalSettling(string pollutant) =>
        GravitationalSettlingVelocity.TryGetValue(pollutant, out var v) ? v : 0.0002;

    public static ResistanceParams GetDryResistance(string pollutant) =>
        DryResistance.TryGetValue(pollutant, out var v) ? v : new(100, 200);

    public static ScavengingParams GetWetScavenging(string pollutant) =>
        WetScavenging.TryGetValue(pollutant, out var v) ? v : new(1e-5, 0.8);

    public static double GetChemicalRate(string pollutant) =>
        ChemicalRates.TryGetValue(pollutant, out var v) ? v : 2e-5;
}
