namespace GnnSimulation.Api.Dtos;

public class MeteorologyCreateDto
{
    public required string Name { get; set; }
    public double WindSpeed { get; set; } = 2.0;
    public double WindDirection { get; set; } = 0.0;
    public double BoundaryLayerHeight { get; set; } = 1000.0;
    public string StabilityClass { get; set; } = "D";
    public double Temperature { get; set; } = 293.15;
    public double Humidity { get; set; } = 50.0;
    public double CloudCover { get; set; } = 0.0;
    public double Precipitation { get; set; } = 0.0;
    public DateTime? RecordTime { get; set; }
}

public class MeteorologyUpdateDto
{
    public string? Name { get; set; }
    public double? WindSpeed { get; set; }
    public double? WindDirection { get; set; }
    public double? BoundaryLayerHeight { get; set; }
    public string? StabilityClass { get; set; }
    public double? Temperature { get; set; }
    public double? Humidity { get; set; }
    public double? CloudCover { get; set; }
    public double? Precipitation { get; set; }
    public DateTime? RecordTime { get; set; }
}

public class MeteorologyDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double WindSpeed { get; set; }
    public double WindDirection { get; set; }
    public double? BoundaryLayerHeight { get; set; }
    public string? StabilityClass { get; set; }
    public double? Temperature { get; set; }
    public double? Humidity { get; set; }
    public double? CloudCover { get; set; }
    public double? Precipitation { get; set; }
    public DateTime RecordTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
