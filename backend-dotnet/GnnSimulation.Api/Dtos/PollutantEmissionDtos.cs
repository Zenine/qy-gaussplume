namespace GnnSimulation.Api.Dtos;

public record PollutantEmissionCreateDto(
    string PollutantType,
    double EmissionRate = 0.0,
    double? Concentration = null
);

public record PollutantEmissionUpdateDto(
    string? PollutantType,
    double? EmissionRate,
    double? Concentration
);

public record PollutantEmissionDto(
    int Id,
    int SourceId,
    string PollutantType,
    double EmissionRate,
    double? Concentration,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record PollutantTypeInfoDto(string Type, string Name, string Unit, string Description);

public record MarkerSymbolInfoDto(string Symbol, string Name, string Icon);
