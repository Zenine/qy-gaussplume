namespace GnnSimulation.Api.Dtos;

// 与 Python 版字段一一对应，便于前端迁移和人工比对
public class EmissionSourceCreateDto
{
    public required string Name { get; set; }
    public string SourceType { get; set; } = "point";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Height { get; set; }
    public double Temperature { get; set; } = 400.0;
    public double Velocity { get; set; } = 15.0;
    public double Diameter { get; set; } = 2.0;

    public string AreaShape { get; set; } = "rectangle";
    public double AreaLength { get; set; } = 100.0;
    public double AreaWidth { get; set; } = 100.0;
    public double AreaHeight { get; set; } = 0.0;
    public double AreaTemperature { get; set; } = 300.0;
    public double? SigmaZ0Area { get; set; }

    public string LineType { get; set; } = "straight";
    public double? StartLon { get; set; }
    public double? StartLat { get; set; }
    public double? EndLon { get; set; }
    public double? EndLat { get; set; }
    public double LineWidth { get; set; } = 10.0;
    public double LineHeight { get; set; } = 0.0;
    public double LineTemperature { get; set; } = 300.0;
    public double? SigmaZ0Line { get; set; }
    public double LineSegmentLength { get; set; } = 10.0;

    public string MarkerSymbol { get; set; } = "factory";
    public string MarkerColor { get; set; } = "#FF5722";
    public bool IsActive { get; set; } = true;

    public List<PollutantEmissionCreateDto> Pollutants { get; set; } = new();
}

// 所有字段可空 → PATCH 风格部分更新
public class EmissionSourceUpdateDto
{
    public string? Name { get; set; }
    public string? SourceType { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? Height { get; set; }
    public double? Temperature { get; set; }
    public double? Velocity { get; set; }
    public double? Diameter { get; set; }
    public string? AreaShape { get; set; }
    public double? AreaLength { get; set; }
    public double? AreaWidth { get; set; }
    public double? AreaHeight { get; set; }
    public double? AreaTemperature { get; set; }
    public double? SigmaZ0Area { get; set; }
    public string? LineType { get; set; }
    public double? StartLon { get; set; }
    public double? StartLat { get; set; }
    public double? EndLon { get; set; }
    public double? EndLat { get; set; }
    public double? LineWidth { get; set; }
    public double? LineHeight { get; set; }
    public double? LineTemperature { get; set; }
    public double? SigmaZ0Line { get; set; }
    public double? LineSegmentLength { get; set; }
    public string? MarkerSymbol { get; set; }
    public string? MarkerColor { get; set; }
    public bool? IsActive { get; set; }

    // null = 不动；非 null = 完全替换
    public List<PollutantEmissionCreateDto>? Pollutants { get; set; }
}

public class EmissionSourceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Height { get; set; }
    public double? Temperature { get; set; }
    public double? Velocity { get; set; }
    public double? Diameter { get; set; }
    public string? AreaShape { get; set; }
    public double? AreaLength { get; set; }
    public double? AreaWidth { get; set; }
    public double? AreaHeight { get; set; }
    public double? AreaTemperature { get; set; }
    public double? SigmaZ0Area { get; set; }
    public string? LineType { get; set; }
    public double? StartLon { get; set; }
    public double? StartLat { get; set; }
    public double? EndLon { get; set; }
    public double? EndLat { get; set; }
    public double? LineWidth { get; set; }
    public double? LineHeight { get; set; }
    public double? LineTemperature { get; set; }
    public double? SigmaZ0Line { get; set; }
    public double? LineSegmentLength { get; set; }
    public string MarkerSymbol { get; set; } = string.Empty;
    public string MarkerColor { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<PollutantEmissionDto> Pollutants { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
