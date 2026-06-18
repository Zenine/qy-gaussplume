using GnnSimulation.Api.Dtos;
using GnnSimulation.Core.Atmosphere;
using GnnSimulation.Data.Entities;

namespace GnnSimulation.Api.Services;

// 单个风向的完整计算。线程安全（无共享可变状态），可被 Parallel.ForEach 并发调用。
// 与单风向 /run 不同，多风向入口会重复跑多个风向并在 ParallelSimulationService 中聚合结果。
internal static class WindDirectionWorker
{
    public record Context(
        Meteorology Meteorology,
        double OverrideWindSpeed,
        IReadOnlyList<EmissionSource> Sources,
        IReadOnlyList<Receptor> Receptors,
        double GridResolution,
        double DomainSize,
        string? PollutantType,
        double ReceptorHeight);

    public static WindDirectionResultDto Run(double windDirection, Context ctx)
    {
        // Worker 不向外抛异常，而是把单个风向失败记录在结果中，
        // 这样并行模拟可以保留其他成功风向的数据。
        try
        {
            var result = Compute(windDirection, ctx);
            result.WindDirection = windDirection;
            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            return new WindDirectionResultDto
            {
                WindDirection = windDirection,
                Success = false,
                Error = ex.Message,
            };
        }
    }

    private static WindDirectionResultDto Compute(double windDirection, Context ctx)
    {
        var met = ctx.Meteorology;
        var model = new GaussianPlumeModel(
            windSpeed: ctx.OverrideWindSpeed,
            windDirection: windDirection,
            stabilityClass: met.StabilityClass ?? "D",
            temperature: met.Temperature ?? 293.15,
            boundaryLayerHeight: met.BoundaryLayerHeight ?? 1000.0,
            humidity: met.Humidity ?? 50.0,
            cloudCover: met.CloudCover ?? 0.0,
            precipitation: met.Precipitation ?? 0.0);

        // 多风向与单风向复用同一源/受体外包框网格，避免并行结果向源中心外无限铺开。
        var grid = GridBuilder.Build(ctx.Sources, ctx.Receptors, ctx.GridResolution, ctx.DomainSize);
        var gridLat = grid.Lat;
        var gridLon = grid.Lon;

        var nLat = gridLat.Length;
        var nLon = gridLon.Length;
        var totalConc = new double[nLat, nLon];
        var pollutantConc = new Dictionary<string, double[,]>();
        var availablePollutants = new HashSet<string>();
        var contributions = new List<SourceContributionDto>();

        foreach (var source in ctx.Sources)
        {
            var (rate, perPollutant) = AggregateRates(source, ctx.PollutantType, model);
            if (rate <= 0) continue;

            foreach (var key in perPollutant.Keys) availablePollutants.Add(key);

            var srcField = new double[nLat, nLon];

            // 每种污染物独立计算，保留 PM/NOx/O3 等不同沉降、湿清除和化学参数差异。
            foreach (var kv in perPollutant)
            {
                if (kv.Value <= 0) continue;
                var pField = DispatchSourceField(source, kv.Value, gridLat, gridLon, model, ctx, kv.Key);
                GridBuilder.AddInPlace(srcField, pField);
                if (!pollutantConc.TryGetValue(kv.Key, out var acc))
                {
                    acc = new double[nLat, nLon];
                    pollutantConc[kv.Key] = acc;
                }
                GridBuilder.AddInPlace(acc, pField);
            }
            GridBuilder.AddInPlace(totalConc, srcField);

            contributions.Add(new SourceContributionDto
            {
                SourceId = source.Id,
                SourceName = source.Name,
                TotalConcentration = Average(srcField),
                MaxConcentration = GridBuilder.Max(srcField),
                Pollutants = perPollutant.Keys.Count > 0
                    ? perPollutant.Keys.ToList()
                    : new List<string> { "Unknown" },
            });
        }

        var receptorContribs = ComputeReceptorContributions(
            ctx.Receptors, ctx.Sources, availablePollutants, model);

        return new WindDirectionResultDto
        {
            Concentrations = GridBuilder.ToJagged(totalConc),
            GridLat = gridLat,
            GridLon = gridLon,
            Contributions = contributions,
            PollutantConcentrations = pollutantConc.Count > 0
                ? pollutantConc.ToDictionary(kv => kv.Key, kv => GridBuilder.ToJagged(kv.Value))
                : null,
            AvailablePollutants = availablePollutants.Count > 0 ? availablePollutants.ToList() : null,
            ReceptorContributions = receptorContribs,
        };
    }

    private static double Average(double[,] m)
    {
        var n0 = m.GetLength(0);
        var n1 = m.GetLength(1);
        if (n0 == 0 || n1 == 0) return 0;
        return GridBuilder.Sum(m) / (n0 * n1);
    }

    private static double OrDefault(double? v, double defaultValue) =>
        (v.HasValue && v.Value != 0) ? v.Value : defaultValue;

    private static (double Total, Dictionary<string, double> PerPollutant) AggregateRates(
        EmissionSource source, string? filterPollutant, GaussianPlumeModel model)
    {
        // 与 SimulationService.ComputeEmissionRates 同源：
        // 等效面源用浓度反算排放速率，其他源类型累加 emission_rate。
        var perPollutant = new Dictionary<string, double>();
        var total = 0.0;

        foreach (var p in source.Pollutants)
        {
            if (filterPollutant is not null && p.PollutantType != filterPollutant) continue;

            double rate;
            if (string.Equals(source.SourceType, "equivalent_area", StringComparison.Ordinal)
                && p.Concentration is > 0)
            {
                rate = model.CalculateEquivalentEmissionRate(
                    concentration: p.Concentration.Value,
                    areaLength: OrDefault(source.AreaLength, 100),
                    areaWidth: OrDefault(source.AreaWidth, 100),
                    areaHeight: OrDefault(source.AreaHeight, 0));
            }
            else
            {
                rate = p.EmissionRate;
            }

            total += rate;
            perPollutant[p.PollutantType] = perPollutant.GetValueOrDefault(p.PollutantType) + rate;
        }

        return (total, perPollutant);
    }

    private static double[,] DispatchSourceField(
        EmissionSource source, double rate,
        double[] gridLat, double[] gridLon,
        GaussianPlumeModel model, Context ctx, string pollutant)
    {
        return source.SourceType switch
        {
            "point" => model.CalculateConcentrationField(
                sourceLat: source.Latitude, sourceLon: source.Longitude,
                sourceHeight: source.Height, emissionRate: rate,
                gridLat: gridLat, gridLon: gridLon,
                stackTemperature: source.Temperature ?? 400.0,
                velocity: source.Velocity ?? 10.0,
                diameter: source.Diameter ?? 1.0,
                receptorHeight: ctx.ReceptorHeight,
                pollutant: pollutant),
            "area" => model.CalculateAreaSourceConcentrationField(
                centerLat: source.Latitude, centerLon: source.Longitude,
                areaLength: OrDefault(source.AreaLength, 100),
                areaWidth: OrDefault(source.AreaWidth, 100),
                areaHeight: OrDefault(source.AreaHeight, 0),
                emissionRate: rate,
                gridLat: gridLat, gridLon: gridLon,
                sigmaZ0: source.SigmaZ0Area,
                receptorHeight: ctx.ReceptorHeight,
                pollutant: pollutant),
            "equivalent_area" => BuildEquivalentAreaField(source, rate, gridLat, gridLon, model, ctx, pollutant),
            "line" => model.CalculateLineSourceConcentrationField(
                startLat: source.StartLat ?? source.Latitude,
                startLon: source.StartLon ?? source.Longitude,
                endLat: source.EndLat ?? source.Latitude,
                endLon: source.EndLon ?? source.Longitude,
                lineWidth: OrDefault(source.LineWidth, 10),
                lineHeight: OrDefault(source.LineHeight, 0),
                emissionRate: rate,
                gridLat: gridLat, gridLon: gridLon,
                segmentLength: OrDefault(source.LineSegmentLength, 10),
                receptorHeight: ctx.ReceptorHeight,
                pollutant: pollutant),
            _ => model.CalculateConcentrationField(
                sourceLat: source.Latitude, sourceLon: source.Longitude,
                sourceHeight: source.Height, emissionRate: rate,
                gridLat: gridLat, gridLon: gridLon,
                stackTemperature: source.Temperature ?? 400.0,
                velocity: source.Velocity ?? 10.0,
                diameter: source.Diameter ?? 1.0,
                receptorHeight: ctx.ReceptorHeight,
                pollutant: pollutant),
        };
    }

    private static double[,] BuildEquivalentAreaField(
        EmissionSource source, double rate,
        double[] gridLat, double[] gridLon,
        GaussianPlumeModel model, Context ctx, string pollutant)
    {
        double? maxConc = null;
        foreach (var p in source.Pollutants)
        {
            if (p.PollutantType == pollutant && p.Concentration is { } c)
            {
                maxConc = c;
                break;
            }
        }

        if (maxConc is not > 0 || rate <= 0)
            return new double[gridLat.Length, gridLon.Length];

        return model.CalculateAreaSourceConcentrationField(
            centerLat: source.Latitude, centerLon: source.Longitude,
            areaLength: OrDefault(source.AreaLength, 100),
            areaWidth: OrDefault(source.AreaWidth, 100),
            areaHeight: OrDefault(source.AreaHeight, 0),
            emissionRate: rate,
            gridLat: gridLat, gridLon: gridLon,
            sigmaZ0: source.SigmaZ0Area,
            receptorHeight: ctx.ReceptorHeight,
            maxConcentration: maxConc,
            isEquivalent: true,
            pollutant: pollutant);
    }

    private static Dictionary<string, Dictionary<string, List<ReceptorContributionEntryDto>>>
        ComputeReceptorContributions(
            IReadOnlyList<Receptor> receptors,
            IReadOnlyList<EmissionSource> sources,
            HashSet<string> pollutants,
            GaussianPlumeModel model)
    {
        var result = new Dictionary<string, Dictionary<string, List<ReceptorContributionEntryDto>>>();
        var pollutantList = pollutants.Count > 0 ? pollutants.ToList() : new List<string> { "PM2.5" };

        foreach (var receptor in receptors)
        {
            var perPollutant = new Dictionary<string, List<ReceptorContributionEntryDto>>();
            foreach (var pollutant in pollutantList)
            {
                var entries = new List<ReceptorContributionEntryDto>();
                var total = 0.0;

                foreach (var source in sources)
                {
                    var rate = GetSourceRateForPollutant(source, pollutant, model);
                    if (rate <= 0) continue;

                    var conc = ComputeReceptorContribution(source, rate, receptor, model, pollutant);
                    if (conc > 0)
                    {
                        total += conc;
                        entries.Add(new ReceptorContributionEntryDto
                        {
                            SourceId = source.Id,
                            SourceName = source.Name,
                            Concentration = conc,
                            Pollutant = pollutant,
                        });
                    }
                }

                if (entries.Count == 0) continue;

                entries.Sort((a, b) => b.Concentration.CompareTo(a.Concentration));
                foreach (var e in entries)
                    e.Percentage = total > 0 ? e.Concentration / total * 100 : 0;
                perPollutant[pollutant] = entries;
            }

            if (perPollutant.Count > 0)
                result[receptor.Name] = perPollutant;
        }
        return result;
    }

    private static double GetSourceRateForPollutant(EmissionSource source, string pollutant, GaussianPlumeModel model)
    {
        var rate = 0.0;
        foreach (var p in source.Pollutants)
        {
            if (p.PollutantType != pollutant) continue;
            if (string.Equals(source.SourceType, "equivalent_area", StringComparison.Ordinal)
                && p.Concentration is > 0)
            {
                rate = model.CalculateEquivalentEmissionRate(
                    concentration: p.Concentration.Value,
                    areaLength: OrDefault(source.AreaLength, 100),
                    areaWidth: OrDefault(source.AreaWidth, 100),
                    areaHeight: OrDefault(source.AreaHeight, 0));
            }
            else
            {
                rate += p.EmissionRate;
            }
        }
        return rate;
    }

    private static double ComputeReceptorContribution(
        EmissionSource source, double rate, Receptor receptor,
        GaussianPlumeModel model, string pollutant)
    {
        return source.SourceType switch
        {
            "point" => model.CalculateReceptorConcentration(
                sourceLat: source.Latitude, sourceLon: source.Longitude,
                sourceHeight: source.Height, emissionRate: rate,
                receptorLat: receptor.Latitude, receptorLon: receptor.Longitude,
                receptorHeight: receptor.Height,
                stackTemperature: source.Temperature ?? 400.0,
                velocity: source.Velocity ?? 10.0,
                diameter: source.Diameter ?? 1.0,
                pollutant: pollutant),
            "area" => model.CalculateAreaSourceReceptorConcentration(
                centerLat: source.Latitude, centerLon: source.Longitude,
                areaLength: OrDefault(source.AreaLength, 100),
                areaWidth: OrDefault(source.AreaWidth, 100),
                areaHeight: OrDefault(source.AreaHeight, 0),
                emissionRate: rate,
                receptorLat: receptor.Latitude, receptorLon: receptor.Longitude,
                sigmaZ0: source.SigmaZ0Area,
                receptorHeight: receptor.Height,
                pollutant: pollutant),
            "equivalent_area" => ComputeEquivalentAreaReceptor(source, rate, receptor, model, pollutant),
            "line" => model.CalculateLineSourceReceptorConcentration(
                startLat: source.StartLat ?? source.Latitude,
                startLon: source.StartLon ?? source.Longitude,
                endLat: source.EndLat ?? source.Latitude,
                endLon: source.EndLon ?? source.Longitude,
                lineWidth: OrDefault(source.LineWidth, 10),
                lineHeight: OrDefault(source.LineHeight, 0),
                emissionRate: rate,
                receptorLat: receptor.Latitude, receptorLon: receptor.Longitude,
                receptorHeight: receptor.Height,
                pollutant: pollutant),
            _ => model.CalculateReceptorConcentration(
                sourceLat: source.Latitude, sourceLon: source.Longitude,
                sourceHeight: source.Height, emissionRate: rate,
                receptorLat: receptor.Latitude, receptorLon: receptor.Longitude,
                receptorHeight: receptor.Height,
                stackTemperature: source.Temperature ?? 400.0,
                velocity: source.Velocity ?? 10.0,
                diameter: source.Diameter ?? 1.0,
                pollutant: pollutant),
        };
    }

    private static double ComputeEquivalentAreaReceptor(
        EmissionSource source, double rate, Receptor receptor,
        GaussianPlumeModel model, string pollutant)
    {
        double? measured = null;
        foreach (var p in source.Pollutants)
        {
            if (p.PollutantType == pollutant && p.Concentration is { } c)
            {
                measured = c;
                break;
            }
        }

        if (measured is not > 0 || rate <= 0) return 0.0;

        return model.CalculateAreaSourceReceptorConcentration(
            centerLat: source.Latitude,
            centerLon: source.Longitude,
            areaLength: OrDefault(source.AreaLength, 100),
            areaWidth: OrDefault(source.AreaWidth, 100),
            areaHeight: OrDefault(source.AreaHeight, 0),
            emissionRate: rate,
            receptorLat: receptor.Latitude,
            receptorLon: receptor.Longitude,
            sigmaZ0: source.SigmaZ0Area,
            receptorHeight: receptor.Height,
            concentration: measured,
            isEquivalent: true,
            pollutant: pollutant);
    }
}
