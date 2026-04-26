namespace GnnSimulation.Data.Entities;

public class PollutantEmission : EntityBase
{
    public int SourceId { get; set; }

    public string PollutantType { get; set; } = string.Empty;
    public double EmissionRate { get; set; }

    // 仅等效面源使用：实测浓度
    public double? Concentration { get; set; }

    public EmissionSource Source { get; set; } = null!;
}
