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

    private const double MetersPerDegree = 111_000.0;

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

        // 网格中心 = 所有源的经纬度均值（不考虑受体）。
        // 多风向场景通常关注排放源周边的平均影响，因此不使用 GridBuilder 的外包框逻辑。
        var centerLat = ctx.Sources.Count > 0 ? ctx.Sources.Average(s => s.Latitude) : 39.9;
        var centerLon = ctx.Sources.Count > 0 ? ctx.Sources.Average(s => s.Longitude) : 116.4;

        var gridPoints = (int)(ctx.DomainSize / ctx.GridResolution) + 1;
        var latOffset = ctx.DomainSize / MetersPerDegree / 2;
        var lonOffset = ctx.DomainSize / (MetersPerDegree * Math.Cos(centerLat * Math.PI / 180.0)) / 2;

        var gridLat = GridBuilder.Linspace(centerLat - latOffset, centerLat + latOffset, gridPoints);
        var gridLon = GridBuilder.Linspace(centerLon - lonOffset, centerLon + lonOffset, gridPoints);

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

            var srcField = DispatchSourceField(source, rate, gridLat, gridLon, model, ctx);
            GridBuilder.AddInPlace(totalConc, srcField);

            // 性能优化路径：用 p_fraction 而不是每污染物独立重新计算。
            // 多风向路径使用比例缩放；单风向 /run 则重新计算每污染物浓度场。
            foreach (var kv in perPollutant)
            {
                if (kv.Value <= 0) continue;
                var fraction = rate > 0 ? kv.Value / rate : 0;
                if (!pollutantConc.TryGetValue(kv.Key, out var acc))
                {
                    acc = new double[nLat, nLon];
                    pollutantConc[kv.Key] = acc;
                }
                AddScaled(acc, srcField, fraction);
            }

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

    private static void AddScaled(double[,] target, double[,] source, double scale)
    {
        // 将某污染物的排放占比投影到总浓度场上，用于多风向聚合的快速污染物分场。
        var n0 = target.GetLength(0);
        var n1 = target.GetLength(1);
        for (var i = 0; i < n0; i++)
            for (var j = 0; j < n1; j++)
                target[i, j] += source[i, j] * scale;
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
        GaussianPlumeModel model, Context ctx)
    {
        var pol = ctx.PollutantType ?? "PM2.5";

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
                pollutant: pol),
            "area" => model.CalculateAreaSourceConcentrationField(
                centerLat: source.Latitude, centerLon: source.Longitude,
                areaLength: OrDefault(source.AreaLength, 100),
                areaWidth: OrDefault(source.AreaWidth, 100),
                areaHeight: OrDefault(source.AreaHeight, 0),
                emissionRate: rate,
                gridLat: gridLat, gridLon: gridLon,
                receptorHeight: ctx.ReceptorHeight,
                pollutant: pol),
            "equivalent_area" => BuildEquivalentAreaField(source, rate, gridLat, gridLon, model, ctx),
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
                pollutant: pol),
            _ => model.CalculateConcentrationField(
                sourceLat: source.Latitude, sourceLon: source.Longitude,
                sourceHeight: source.Height, emissionRate: rate,
                gridLat: gridLat, gridLon: gridLon,
                stackTemperature: source.Temperature ?? 400.0,
                velocity: source.Velocity ?? 10.0,
                diameter: source.Diameter ?? 1.0,
                receptorHeight: ctx.ReceptorHeight,
                pollutant: pol),
        };
    }

    private static double[,] BuildEquivalentAreaField(
        EmissionSource source, double rate,
        double[] gridLat, double[] gridLon,
        GaussianPlumeModel model, Context ctx)
    {
        double? maxConc = null;
        foreach (var p in source.Pollutants)
        {
            if (ctx.PollutantType is not null)
            {
                if (p.PollutantType == ctx.PollutantType && p.Concentration is { } c) { maxConc = c; break; }
            }
            else if (p.Concentration is { } c)
            {
                maxConc = maxConc is null ? c : Math.Max(maxConc.Value, c);
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
            receptorHeight: ctx.ReceptorHeight,
            maxConcentration: maxConc,
            isEquivalent: true,
            pollutant: ctx.PollutantType ?? "PM2.5");
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
            "area" or "equivalent_area" => model.CalculateAreaSourceReceptorConcentration(
                centerLat: source.Latitude, centerLon: source.Longitude,
                areaLength: OrDefault(source.AreaLength, 100),
                areaWidth: OrDefault(source.AreaWidth, 100),
                areaHeight: OrDefault(source.AreaHeight, 0),
                emissionRate: rate,
                receptorLat: receptor.Latitude, receptorLon: receptor.Longitude,
                receptorHeight: receptor.Height,
                pollutant: pollutant),
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
}
