namespace GnnSimulation.Core.Atmosphere;

public record ContributionResult(int SourceId, string SourceName, double Concentration, double Percentage);

public record SourceInfo(int Id, string Name);

public static class ContributionAnalysis
{
    // 按贡献浓度降序排序；percentage = conc / total * 100
    public static IReadOnlyList<ContributionResult> Rank(
        IReadOnlyList<SourceInfo> sources,
        IReadOnlyList<double> concentrations)
    {
        if (sources.Count != concentrations.Count)
            throw new ArgumentException("sources 与 concentrations 长度必须一致");

        var total = 0.0;
        for (var i = 0; i < concentrations.Count; i++) total += concentrations[i];

        var items = new List<ContributionResult>(sources.Count);
        for (var i = 0; i < sources.Count; i++)
        {
            var pct = total > 0 ? concentrations[i] / total * 100 : 0;
            items.Add(new ContributionResult(sources[i].Id, sources[i].Name, concentrations[i], pct));
        }

        items.Sort((a, b) => b.Concentration.CompareTo(a.Concentration));
        return items;
    }
}
