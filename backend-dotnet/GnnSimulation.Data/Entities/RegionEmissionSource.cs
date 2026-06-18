namespace GnnSimulation.Data.Entities;

public class RegionEmissionSource
{
    public int RegionId { get; set; }
    public Region Region { get; set; } = null!;
    public int SourceId { get; set; }
    public EmissionSource Source { get; set; } = null!;
}
