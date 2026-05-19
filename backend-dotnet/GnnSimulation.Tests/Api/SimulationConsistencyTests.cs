using FluentAssertions;
using GnnSimulation.Api.Dtos;
using GnnSimulation.Api.Services;
using GnnSimulation.Core.Atmosphere;
using GnnSimulation.Data;
using GnnSimulation.Data.Entities;
using GnnSimulation.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GnnSimulation.Tests.Api;

// 验证 SimulationService 聚合与直接调用 Core 模型的结果一致，
// 防止中间派发逻辑（分派源类型、排放速率聚合）引入偏差。
public class SimulationConsistencyTests
{
    [Fact]
    public async Task 单点单源_服务聚合的受体浓度_等于Core直接计算()
    {
        using var fixture = new SqliteInMemoryFixture();
        await using var ctx = fixture.CreateContext();

        var met = new Meteorology
        {
            Name = "test",
            WindSpeed = 3.0,
            WindDirection = 0.0, // 北风 → 下风向为源正南
            StabilityClass = "D",
            Temperature = 293.15,
            BoundaryLayerHeight = 1000.0,
            Humidity = 50.0,
            CloudCover = 0.0,
            Precipitation = 0.0,
        };
        var src = new EmissionSource
        {
            Name = "S1",
            SourceType = "point",
            Latitude = 39.9, Longitude = 116.4, Height = 50,
            Temperature = 400, Velocity = 15, Diameter = 2,
            IsActive = true,
            Pollutants = new List<PollutantEmission>
            {
                new() { PollutantType = "PM2.5", EmissionRate = 2.5 },
            },
        };
        // 受体位于源正南下风向约 2km 处，位于烟羽轴线上，浓度显著
        var rec = new Receptor
        {
            Name = "R1",
            Latitude = 39.88, Longitude = 116.4, Height = 1.5,
            IsActive = true,
        };
        ctx.Meteorology.Add(met);
        ctx.EmissionSources.Add(src);
        ctx.Receptors.Add(rec);
        await ctx.SaveChangesAsync();

        var service = new SimulationService(ctx);
        var result = await service.RunAsync(new SimulationRequestDto
        {
            MeteorologyId = met.Id,
            GridResolution = 100,
            DomainSize = 5000,
        });

        // 与 Core 直接调用做对比
        var model = new GaussianPlumeModel(
            met.WindSpeed, met.WindDirection, met.StabilityClass!,
            met.Temperature!.Value, met.BoundaryLayerHeight!.Value,
            met.Humidity!.Value, met.CloudCover!.Value, met.Precipitation!.Value);

        var expected = model.CalculateReceptorConcentration(
            sourceLat: src.Latitude, sourceLon: src.Longitude,
            sourceHeight: src.Height, emissionRate: 2.5,
            receptorLat: rec.Latitude, receptorLon: rec.Longitude,
            receptorHeight: rec.Height,
            stackTemperature: src.Temperature!.Value,
            velocity: src.Velocity!.Value,
            diameter: src.Diameter!.Value,
            pollutant: "PM2.5");

        var entry = result.ReceptorContributions[rec.Name]["PM2.5"].Single();
        entry.Concentration.Should().BeApproximately(expected, 1e-10);
        entry.Percentage.Should().BeApproximately(100, 1e-10);
    }

    [Fact]
    public async Task 等效面源_受体浓度匹配Core计算()
    {
        using var fixture = new SqliteInMemoryFixture();
        await using var ctx = fixture.CreateContext();

        var met = new Meteorology { Name = "t", WindSpeed = 2.5, WindDirection = 180, StabilityClass = "D" };
        var src = new EmissionSource
        {
            Name = "EQ", SourceType = "equivalent_area",
            Latitude = 39.9, Longitude = 116.4, Height = 0,
            AreaLength = 300, AreaWidth = 150, AreaHeight = 8,
            IsActive = true,
            Pollutants = new List<PollutantEmission>
            {
                new() { PollutantType = "PM2.5", EmissionRate = 0, Concentration = 80 },
            },
        };
        var rec = new Receptor
        {
            Name = "R", Latitude = 39.92, Longitude = 116.405, Height = 1.5, IsActive = true,
        };
        ctx.Meteorology.Add(met);
        ctx.EmissionSources.Add(src);
        ctx.Receptors.Add(rec);
        await ctx.SaveChangesAsync();

        var service = new SimulationService(ctx);
        var result = await service.RunAsync(new SimulationRequestDto
        {
            MeteorologyId = met.Id, GridResolution = 50, DomainSize = 3000,
        });

        var model = new GaussianPlumeModel(
            met.WindSpeed, met.WindDirection, "D",
            293.15, 1000.0, 50.0, 0.0, 0.0);
        var eqRate = model.CalculateEquivalentEmissionRate(
            concentration: 80, areaLength: 300, areaWidth: 150, areaHeight: 8);
        var expected = model.CalculateAreaSourceReceptorConcentration(
            centerLat: 39.9, centerLon: 116.4,
            areaLength: 300, areaWidth: 150, areaHeight: 8,
            emissionRate: eqRate,
            receptorLat: rec.Latitude, receptorLon: rec.Longitude,
            receptorHeight: rec.Height,
            concentration: 80, isEquivalent: true,
            pollutant: "PM2.5");

        var entry = result.ReceptorContributions[rec.Name]["PM2.5"].Single();
        entry.Concentration.Should().BeApproximately(expected, 1e-10);
    }
}
