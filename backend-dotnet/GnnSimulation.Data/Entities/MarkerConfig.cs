namespace GnnSimulation.Data.Entities;

public class MarkerConfig : EntityBase
{
    // 对应类型：source / receptor / area / line 等
    public string Type { get; set; } = string.Empty;

    public string? Symbol { get; set; } = "circle";
    public string? Color { get; set; } = "#FF5722";
    public int? Size { get; set; } = 10;
}
