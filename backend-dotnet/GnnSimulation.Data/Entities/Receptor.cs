namespace GnnSimulation.Data.Entities;

public class Receptor : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Height { get; set; }

    public string MarkerSymbol { get; set; } = "monitor";
    public string MarkerColor { get; set; } = "#2196F3";

    public bool IsActive { get; set; } = true;
}
