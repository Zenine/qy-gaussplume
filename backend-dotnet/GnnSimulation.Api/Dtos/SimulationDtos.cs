namespace GnnSimulation.Api.Dtos;

public class SimulationRequestDto
{
    public int MeteorologyId { get; set; }
    public List<int>? SourceIds { get; set; }
    public List<int>? ReceptorIds { get; set; }
    public string? PollutantType { get; set; }
    public double GridResolution { get; set; } = 100.0;
    public double DomainSize { get; set; } = 10000.0;
    public double ReceptorHeight { get; set; } = 0.0;
}

public class SourceContributionDto
{
    public int SourceId { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public double TotalConcentration { get; set; }
    public double MaxConcentration { get; set; }
    public List<string> Pollutants { get; set; } = new();
}

public class ReceptorContributionEntryDto
{
    public int SourceId { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public double Concentration { get; set; }
    public string Pollutant { get; set; } = string.Empty;
    public double Percentage { get; set; }
}

// 浓度场：用交错数组 double[][] 表示，便于 System.Text.Json 序列化为 JSON 嵌套数组
public class SimulationResultDto
{
    public double[][] Concentrations { get; set; } = Array.Empty<double[]>();
    public double[] GridLat { get; set; } = Array.Empty<double>();
    public double[] GridLon { get; set; } = Array.Empty<double>();
    public List<SourceContributionDto> Contributions { get; set; } = new();

    // receptor_name → pollutant_type → ranked contributions
    public Dictionary<string, Dictionary<string, List<ReceptorContributionEntryDto>>> ReceptorContributions { get; set; }
        = new();

    public Dictionary<string, double[][]>? PollutantConcentrations { get; set; }
    public List<string>? AvailablePollutants { get; set; }
}
