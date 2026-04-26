using GnnSimulation.Api.Dtos;
using GnnSimulation.Data.Entities;

namespace GnnSimulation.Api.Mapping;

// 手工映射：避免为这点工作量引入 AutoMapper 依赖
internal static class EntityMapping
{
    // -------- EmissionSource --------
    public static EmissionSource ToEntity(this EmissionSourceCreateDto dto) => new()
    {
        Name = dto.Name,
        SourceType = dto.SourceType,
        Latitude = dto.Latitude,
        Longitude = dto.Longitude,
        Height = dto.Height,
        Temperature = dto.Temperature,
        Velocity = dto.Velocity,
        Diameter = dto.Diameter,
        AreaShape = dto.AreaShape,
        AreaLength = dto.AreaLength,
        AreaWidth = dto.AreaWidth,
        AreaHeight = dto.AreaHeight,
        AreaTemperature = dto.AreaTemperature,
        SigmaZ0Area = dto.SigmaZ0Area,
        LineType = dto.LineType,
        StartLon = dto.StartLon,
        StartLat = dto.StartLat,
        EndLon = dto.EndLon,
        EndLat = dto.EndLat,
        LineWidth = dto.LineWidth,
        LineHeight = dto.LineHeight,
        LineTemperature = dto.LineTemperature,
        SigmaZ0Line = dto.SigmaZ0Line,
        LineSegmentLength = dto.LineSegmentLength,
        MarkerSymbol = dto.MarkerSymbol,
        MarkerColor = dto.MarkerColor,
        IsActive = dto.IsActive,
        Pollutants = dto.Pollutants.Select(p => p.ToEntity()).ToList(),
    };

    public static void ApplyUpdate(this EmissionSource target, EmissionSourceUpdateDto dto)
    {
        if (dto.Name is not null) target.Name = dto.Name;
        if (dto.SourceType is not null) target.SourceType = dto.SourceType;
        if (dto.Latitude.HasValue) target.Latitude = dto.Latitude.Value;
        if (dto.Longitude.HasValue) target.Longitude = dto.Longitude.Value;
        if (dto.Height.HasValue) target.Height = dto.Height.Value;
        if (dto.Temperature.HasValue) target.Temperature = dto.Temperature.Value;
        if (dto.Velocity.HasValue) target.Velocity = dto.Velocity.Value;
        if (dto.Diameter.HasValue) target.Diameter = dto.Diameter.Value;
        if (dto.AreaShape is not null) target.AreaShape = dto.AreaShape;
        if (dto.AreaLength.HasValue) target.AreaLength = dto.AreaLength.Value;
        if (dto.AreaWidth.HasValue) target.AreaWidth = dto.AreaWidth.Value;
        if (dto.AreaHeight.HasValue) target.AreaHeight = dto.AreaHeight.Value;
        if (dto.AreaTemperature.HasValue) target.AreaTemperature = dto.AreaTemperature.Value;
        if (dto.SigmaZ0Area.HasValue) target.SigmaZ0Area = dto.SigmaZ0Area.Value;
        if (dto.LineType is not null) target.LineType = dto.LineType;
        if (dto.StartLon.HasValue) target.StartLon = dto.StartLon.Value;
        if (dto.StartLat.HasValue) target.StartLat = dto.StartLat.Value;
        if (dto.EndLon.HasValue) target.EndLon = dto.EndLon.Value;
        if (dto.EndLat.HasValue) target.EndLat = dto.EndLat.Value;
        if (dto.LineWidth.HasValue) target.LineWidth = dto.LineWidth.Value;
        if (dto.LineHeight.HasValue) target.LineHeight = dto.LineHeight.Value;
        if (dto.LineTemperature.HasValue) target.LineTemperature = dto.LineTemperature.Value;
        if (dto.SigmaZ0Line.HasValue) target.SigmaZ0Line = dto.SigmaZ0Line.Value;
        if (dto.LineSegmentLength.HasValue) target.LineSegmentLength = dto.LineSegmentLength.Value;
        if (dto.MarkerSymbol is not null) target.MarkerSymbol = dto.MarkerSymbol;
        if (dto.MarkerColor is not null) target.MarkerColor = dto.MarkerColor;
        if (dto.IsActive.HasValue) target.IsActive = dto.IsActive.Value;
    }

    public static EmissionSourceDto ToDto(this EmissionSource e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        SourceType = e.SourceType,
        Latitude = e.Latitude,
        Longitude = e.Longitude,
        Height = e.Height,
        Temperature = e.Temperature,
        Velocity = e.Velocity,
        Diameter = e.Diameter,
        AreaShape = e.AreaShape,
        AreaLength = e.AreaLength,
        AreaWidth = e.AreaWidth,
        AreaHeight = e.AreaHeight,
        AreaTemperature = e.AreaTemperature,
        SigmaZ0Area = e.SigmaZ0Area,
        LineType = e.LineType,
        StartLon = e.StartLon,
        StartLat = e.StartLat,
        EndLon = e.EndLon,
        EndLat = e.EndLat,
        LineWidth = e.LineWidth,
        LineHeight = e.LineHeight,
        LineTemperature = e.LineTemperature,
        SigmaZ0Line = e.SigmaZ0Line,
        LineSegmentLength = e.LineSegmentLength,
        MarkerSymbol = e.MarkerSymbol,
        MarkerColor = e.MarkerColor,
        IsActive = e.IsActive,
        Pollutants = e.Pollutants.Select(p => p.ToDto()).ToList(),
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt,
    };

    // -------- PollutantEmission --------
    public static PollutantEmission ToEntity(this PollutantEmissionCreateDto dto) => new()
    {
        PollutantType = dto.PollutantType,
        EmissionRate = dto.EmissionRate,
        Concentration = dto.Concentration,
    };

    public static PollutantEmissionDto ToDto(this PollutantEmission e) => new(
        Id: e.Id,
        SourceId: e.SourceId,
        PollutantType: e.PollutantType,
        EmissionRate: e.EmissionRate,
        Concentration: e.Concentration,
        CreatedAt: e.CreatedAt,
        UpdatedAt: e.UpdatedAt
    );

    // -------- Receptor --------
    public static Receptor ToEntity(this ReceptorCreateDto dto) => new()
    {
        Name = dto.Name,
        Latitude = dto.Latitude,
        Longitude = dto.Longitude,
        Height = dto.Height,
        MarkerSymbol = dto.MarkerSymbol,
        MarkerColor = dto.MarkerColor,
        IsActive = dto.IsActive,
    };

    public static void ApplyUpdate(this Receptor target, ReceptorUpdateDto dto)
    {
        if (dto.Name is not null) target.Name = dto.Name;
        if (dto.Latitude.HasValue) target.Latitude = dto.Latitude.Value;
        if (dto.Longitude.HasValue) target.Longitude = dto.Longitude.Value;
        if (dto.Height.HasValue) target.Height = dto.Height.Value;
        if (dto.MarkerSymbol is not null) target.MarkerSymbol = dto.MarkerSymbol;
        if (dto.MarkerColor is not null) target.MarkerColor = dto.MarkerColor;
        if (dto.IsActive.HasValue) target.IsActive = dto.IsActive.Value;
    }

    public static ReceptorDto ToDto(this Receptor e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Latitude = e.Latitude,
        Longitude = e.Longitude,
        Height = e.Height,
        MarkerSymbol = e.MarkerSymbol,
        MarkerColor = e.MarkerColor,
        IsActive = e.IsActive,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt,
    };

    // -------- Meteorology --------
    public static Meteorology ToEntity(this MeteorologyCreateDto dto) => new()
    {
        Name = dto.Name,
        WindSpeed = dto.WindSpeed,
        WindDirection = dto.WindDirection,
        BoundaryLayerHeight = dto.BoundaryLayerHeight,
        StabilityClass = dto.StabilityClass,
        Temperature = dto.Temperature,
        Humidity = dto.Humidity,
        CloudCover = dto.CloudCover,
        Precipitation = dto.Precipitation,
        RecordTime = dto.RecordTime ?? DateTime.UtcNow,
    };

    public static void ApplyUpdate(this Meteorology target, MeteorologyUpdateDto dto)
    {
        if (dto.Name is not null) target.Name = dto.Name;
        if (dto.WindSpeed.HasValue) target.WindSpeed = dto.WindSpeed.Value;
        if (dto.WindDirection.HasValue) target.WindDirection = dto.WindDirection.Value;
        if (dto.BoundaryLayerHeight.HasValue) target.BoundaryLayerHeight = dto.BoundaryLayerHeight.Value;
        if (dto.StabilityClass is not null) target.StabilityClass = dto.StabilityClass;
        if (dto.Temperature.HasValue) target.Temperature = dto.Temperature.Value;
        if (dto.Humidity.HasValue) target.Humidity = dto.Humidity.Value;
        if (dto.CloudCover.HasValue) target.CloudCover = dto.CloudCover.Value;
        if (dto.Precipitation.HasValue) target.Precipitation = dto.Precipitation.Value;
        if (dto.RecordTime.HasValue) target.RecordTime = dto.RecordTime.Value;
    }

    public static MeteorologyDto ToDto(this Meteorology e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        WindSpeed = e.WindSpeed,
        WindDirection = e.WindDirection,
        BoundaryLayerHeight = e.BoundaryLayerHeight,
        StabilityClass = e.StabilityClass,
        Temperature = e.Temperature,
        Humidity = e.Humidity,
        CloudCover = e.CloudCover,
        Precipitation = e.Precipitation,
        RecordTime = e.RecordTime,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt,
    };

    // -------- MarkerConfig --------
    public static MarkerConfig ToEntity(this MarkerConfigCreateDto dto) => new()
    {
        Type = dto.Type,
        Symbol = dto.Symbol,
        Color = dto.Color,
        Size = dto.Size,
    };

    public static void ApplyUpdate(this MarkerConfig target, MarkerConfigUpdateDto dto)
    {
        if (dto.Symbol is not null) target.Symbol = dto.Symbol;
        if (dto.Color is not null) target.Color = dto.Color;
        if (dto.Size.HasValue) target.Size = dto.Size.Value;
    }

    public static MarkerConfigDto ToDto(this MarkerConfig e) => new()
    {
        Id = e.Id,
        Type = e.Type,
        Symbol = e.Symbol,
        Color = e.Color,
        Size = e.Size,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt,
    };
}
