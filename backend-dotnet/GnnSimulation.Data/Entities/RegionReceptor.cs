namespace GnnSimulation.Data.Entities;

public class RegionReceptor
{
    public int RegionId { get; set; }
    public Region Region { get; set; } = null!;
    public int ReceptorId { get; set; }
    public Receptor Receptor { get; set; } = null!;
}
