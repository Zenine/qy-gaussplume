namespace GnnSimulation.Api.Dtos;

public class MarkerConfigCreateDto
{
    public required string Type { get; set; }
    public string Symbol { get; set; } = "circle";
    public string Color { get; set; } = "#FF5722";
    public int Size { get; set; } = 10;
}

public class MarkerConfigUpdateDto
{
    public string? Symbol { get; set; }
    public string? Color { get; set; }
    public int? Size { get; set; }
}

public class MarkerConfigDto
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Symbol { get; set; }
    public string? Color { get; set; }
    public int? Size { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
