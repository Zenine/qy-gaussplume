namespace GnnSimulation.Data.Entities;

public class Meteorology : EntityBase
{
    public string Name { get; set; } = string.Empty;

    public double WindSpeed { get; set; } = 2.0;
    public double WindDirection { get; set; }

    public double? BoundaryLayerHeight { get; set; } = 1000.0;

    // 大气稳定度 A-F
    public string? StabilityClass { get; set; } = "D";

    public double? Temperature { get; set; } = 293.15;
    public double? Humidity { get; set; } = 50.0;
    public double? CloudCover { get; set; } = 0.0;
    public double? Precipitation { get; set; } = 0.0;

    public DateTime RecordTime { get; set; } = DateTime.UtcNow;
}
