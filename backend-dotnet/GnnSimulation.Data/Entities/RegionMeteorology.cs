namespace GnnSimulation.Data.Entities;

public class RegionMeteorology
{
    public int RegionId { get; set; }
    public Region Region { get; set; } = null!;
    public int MeteorologyId { get; set; }
    public Meteorology Meteorology { get; set; } = null!;
}
