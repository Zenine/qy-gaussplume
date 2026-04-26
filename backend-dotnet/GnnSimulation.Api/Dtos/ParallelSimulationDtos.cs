namespace GnnSimulation.Api.Dtos;

public class ParallelSimulationRequestDto
{
    public int MeteorologyId { get; set; }
    public List<int>? SourceIds { get; set; }
    public List<int>? ReceptorIds { get; set; }
    public string? PollutantType { get; set; }

    public double GridResolution { get; set; } = 10.0;
    public double DomainSize { get; set; } = 10000.0;

    // 覆盖气象场的风速，便于统一 72 风向场景
    public double WindSpeed { get; set; }
    public List<double> WindDirections { get; set; } = new();

    // 风向对应的权重；null 或长度不匹配时视为等权重
    public List<double>? Weights { get; set; }

    public double ReceptorHeight { get; set; } = 0.0;
    public int? NumWorkers { get; set; }

    // true（默认）= 只返回加权聚合结果（省内存）；false = 返回每风向的明细
    public bool ReturnAggregatedOnly { get; set; } = true;
}

public class WindDirectionErrorDto
{
    public double WindDirection { get; set; }
    public string Error { get; set; } = string.Empty;
}

// 详细模式下每风向的完整结果
public class WindDirectionResultDto
{
    public double WindDirection { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }

    public double[][]? Concentrations { get; set; }
    public double[]? GridLat { get; set; }
    public double[]? GridLon { get; set; }
    public List<SourceContributionDto>? Contributions { get; set; }
    public Dictionary<string, double[][]>? PollutantConcentrations { get; set; }
    public List<string>? AvailablePollutants { get; set; }
    public Dictionary<string, Dictionary<string, List<ReceptorContributionEntryDto>>>? ReceptorContributions { get; set; }
}

public class ParallelSimulationResultDto
{
    public bool Success { get; set; } = true;
    public string Mode { get; set; } = "aggregated";
    public int TotalWindDirections { get; set; }
    public int SuccessfulSimulations { get; set; }
    public int FailedSimulations { get; set; }
    public List<WindDirectionErrorDto>? Errors { get; set; }
    public int NumWorkersUsed { get; set; }
    public double ComputationTimeSeconds { get; set; }
    public double SpeedupFactor { get; set; }

    // 聚合模式专用
    public double[][]? Concentrations { get; set; }
    public double[]? GridLat { get; set; }
    public double[]? GridLon { get; set; }
    public Dictionary<string, double[][]>? PollutantConcentrations { get; set; }
    public List<string>? AvailablePollutants { get; set; }
    public List<object> Contributions { get; set; } = new(); // Python 原版返回 []
    public Dictionary<string, Dictionary<string, List<ReceptorContributionEntryDto>>>? ReceptorContributions { get; set; }

    // 详细模式专用
    public List<WindDirectionResultDto>? Results { get; set; }
}
