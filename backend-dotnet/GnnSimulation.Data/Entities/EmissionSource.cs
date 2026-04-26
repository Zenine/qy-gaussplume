namespace GnnSimulation.Data.Entities;

public enum EmissionSourceType
{
    Point,
    Area,
    Line,
    EquivalentArea,
}

public class EmissionSource : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string SourceType { get; set; } = "point";

    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Height { get; set; }

    // 点源参数
    public double? Temperature { get; set; } = 400.0;
    public double? Velocity { get; set; } = 15.0;
    public double? Diameter { get; set; } = 2.0;

    // 面源参数
    public string? AreaShape { get; set; } = "rectangle";
    public double? AreaLength { get; set; } = 100.0;
    public double? AreaWidth { get; set; } = 100.0;
    public double? AreaHeight { get; set; } = 0.0;
    public double? AreaTemperature { get; set; } = 300.0;
    public double? SigmaZ0Area { get; set; }

    // 线源参数
    public string? LineType { get; set; } = "straight";
    public double? StartLon { get; set; }
    public double? StartLat { get; set; }
    public double? EndLon { get; set; }
    public double? EndLat { get; set; }
    public double? LineWidth { get; set; } = 10.0;
    public double? LineHeight { get; set; } = 0.0;
    public double? LineTemperature { get; set; } = 300.0;
    public double? SigmaZ0Line { get; set; }
    public double? LineSegmentLength { get; set; } = 10.0;

    // 标记
    public string MarkerSymbol { get; set; } = "factory";
    public string MarkerColor { get; set; } = "#FF5722";

    public bool IsActive { get; set; } = true;

    public List<PollutantEmission> Pollutants { get; set; } = new();
}
