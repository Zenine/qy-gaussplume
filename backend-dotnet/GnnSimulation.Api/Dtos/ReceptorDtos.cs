namespace GnnSimulation.Api.Dtos;

public class ReceptorCreateDto
{
    public required string Name { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Height { get; set; }
    public string MarkerSymbol { get; set; } = "monitor";
    public string MarkerColor { get; set; } = "#2196F3";
    public bool IsActive { get; set; } = true;
}

public class ReceptorUpdateDto
{
    public string? Name { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? Height { get; set; }
    public string? MarkerSymbol { get; set; }
    public string? MarkerColor { get; set; }
    public bool? IsActive { get; set; }
}

public class ReceptorDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Height { get; set; }
    public string MarkerSymbol { get; set; } = string.Empty;
    public string MarkerColor { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
