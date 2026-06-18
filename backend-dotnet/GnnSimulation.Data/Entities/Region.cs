namespace GnnSimulation.Data.Entities;

public class Region : EntityBase
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public List<RegionEmissionSource> Sources { get; set; } = new();
    public List<RegionReceptor> Receptors { get; set; } = new();
    public List<RegionMeteorology> Meteorologies { get; set; } = new();
}
