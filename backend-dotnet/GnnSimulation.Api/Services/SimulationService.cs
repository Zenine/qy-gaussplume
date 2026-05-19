using GnnSimulation.Api.Dtos;
using GnnSimulation.Core.Atmosphere;
using GnnSimulation.Data;
using GnnSimulation.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GnnSimulation.Api.Services;

public class SimulationService
{
    private readonly GnnDbContext _db;

    public SimulationService(GnnDbContext db) => _db = db;

    // 执行一次单风向模拟：
    // 1. 加载气象、排放源、受体点；首页框选时 SourceIds/ReceptorIds 会限制参与对象。
    // 2. 用参与对象构建模拟网格，保持 domain_size/grid_resolution 的统一语义。
    // 3. 按源类型派发点源、面源、等效面源、线源计算，并逐源叠加浓度场。
    // 4. 额外生成每污染物独立浓度场，供前端“显示污染物”下拉切换。
    // 5. 计算每受体×每污染物的贡献排名，供首页贡献卡片和抽屉展示。
    public async Task<SimulationResultDto> RunAsync(SimulationRequestDto request, CancellationToken ct = default)
    {
        var met = await _db.Meteorology.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.MeteorologyId, ct)
            ?? throw new SimulationNotFoundException("气象场未找到");

        var sources = await LoadSourcesAsync(request.SourceIds, ct);
        if (sources.Count == 0)
            throw new SimulationBadRequestException("没有可用的排放源");

        var receptors = await LoadReceptorsAsync(request.ReceptorIds, ct);

        var model = BuildModel(met);
        var grid = GridBuilder.Build(sources, receptors, request.GridResolution, request.DomainSize);

        var nLat = grid.Lat.Length;
        var nLon = grid.Lon.Length;
        var totalConc = new double[nLat, nLon];
        var pollutantConc = new Dictionary<string, double[,]>();
        var availablePollutants = new HashSet<string>();
        var sourceContributions = new List<SourceContributionDto>();

        foreach (var source in sources)
        {
            var (totalRate, perPollutantRate) = ComputeEmissionRates(source, request.PollutantType, model);
            if (totalRate <= 0) continue;

            foreach (var key in perPollutantRate.Keys) availablePollutants.Add(key);

            var srcField = ComputeSourceField(source, totalRate, grid, request, model, request.PollutantType);
            GridBuilder.AddInPlace(totalConc, srcField);

            // 每种污染物独立的浓度场（用于前端按污染物切换显示）。
            // 单风向 /run 路径选择重新计算每个污染物，而不是用比例缩放总场，
            // 这样能保留不同污染物沉降、化学衰减参数造成的差异。
            foreach (var kv in perPollutantRate)
            {
                if (kv.Value <= 0) continue;
                var pField = ComputeSourceField(source, kv.Value, grid, request, model, kv.Key);
                if (!pollutantConc.TryGetValue(kv.Key, out var acc))
                {
                    acc = new double[nLat, nLon];
                    pollutantConc[kv.Key] = acc;
                }
                GridBuilder.AddInPlace(acc, pField);
            }

            sourceContributions.Add(new SourceContributionDto
            {
                SourceId = source.Id,
                SourceName = source.Name,
                TotalConcentration = GridBuilder.Sum(srcField),
                MaxConcentration = GridBuilder.Max(srcField),
                Pollutants = perPollutantRate.Keys.Count > 0
                    ? perPollutantRate.Keys.ToList()
                    : new List<string> { "Unknown" },
            });
        }

        var receptorContribs = ComputeReceptorContributions(receptors, sources, availablePollutants, model);

        return new SimulationResultDto
        {
            Concentrations = GridBuilder.ToJagged(totalConc),
            GridLat = grid.Lat,
            GridLon = grid.Lon,
            Contributions = sourceContributions,
            ReceptorContributions = receptorContribs,
            PollutantConcentrations = pollutantConc.Count > 0
                ? pollutantConc.ToDictionary(kv => kv.Key, kv => GridBuilder.ToJagged(kv.Value))
                : null,
            AvailablePollutants = availablePollutants.Count > 0 ? availablePollutants.ToList() : null,
        };
    }

    // ---------- 气象→模型参数 ----------
    // 将数据库气象记录转成核心模型构造参数。null 值使用工程默认值，
    // 避免老数据库或脱敏数据缺字段时影响模拟入口。
    private static GaussianPlumeModel BuildModel(Meteorology m) => new(
        windSpeed: m.WindSpeed,
        windDirection: m.WindDirection,
        stabilityClass: m.StabilityClass ?? "D",
        temperature: m.Temperature ?? 293.15,
        boundaryLayerHeight: m.BoundaryLayerHeight ?? 1000.0,
        humidity: m.Humidity ?? 50.0,
        cloudCover: m.CloudCover ?? 0.0,
        precipitation: m.Precipitation ?? 0.0);

    // ---------- 数据加载 ----------
    // 未传 ids 时加载激活数据；传空数组时返回空集合，表示调用方明确选择了空范围。
    private async Task<List<EmissionSource>> LoadSourcesAsync(List<int>? ids, CancellationToken ct)
    {
        var q = _db.EmissionSources.AsNoTracking().Include(s => s.Pollutants);
        if (ids is not null && ids.Count == 0) return new List<EmissionSource>();
        var filtered = ids is not null ? q.Where(s => ids.Contains(s.Id)) : q.Where(s => s.IsActive);
        return await filtered.ToListAsync(ct);
    }

    private async Task<List<Receptor>> LoadReceptorsAsync(List<int>? ids, CancellationToken ct)
    {
        var q = _db.Receptors.AsNoTracking();
        if (ids is not null && ids.Count == 0) return new List<Receptor>();
        var filtered = ids is not null ? q.Where(r => ids.Contains(r.Id)) : q.Where(r => r.IsActive);
        return await filtered.ToListAsync(ct);
    }

    // ---------- 排放速率聚合 ----------
    // 等效面源：将实测浓度转换为等效排放速率；其他类型：直接累加 emission_rate。
    // 返回 Total 用于总浓度场，PerPollutant 用于污染物分场和贡献排名。
    private static (double Total, Dictionary<string, double> PerPollutant) ComputeEmissionRates(
        EmissionSource source, string? filterPollutant, GaussianPlumeModel model)
    {
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

    // 数据导入时 0 常表示未配置；这里将 null 或 0 统一替换为默认值。
    private static double OrDefault(double? v, double defaultValue) =>
        (v.HasValue && v.Value != 0) ? v.Value : defaultValue;

    // ---------- 源浓度场派发 ----------
    // 将数据库 source_type 映射到核心模型的点源、面源、等效面源、线源计算函数。
    // 这里是 API 层和 Core 层的主要边界：API 负责补默认值，Core 只做公式计算。
    private static double[,] ComputeSourceField(
        EmissionSource source, double rate,
        GridBuilder.Grid grid, SimulationRequestDto req, GaussianPlumeModel model,
        string? pollutant)
    {
        var pol = pollutant ?? "PM2.5";
        return source.SourceType switch
        {
            "point" => model.CalculateConcentrationField(
                sourceLat: source.Latitude, sourceLon: source.Longitude,
                sourceHeight: source.Height, emissionRate: rate,
                gridLat: grid.Lat, gridLon: grid.Lon,
                stackTemperature: source.Temperature ?? 400.0,
                velocity: source.Velocity ?? 15.0,
                diameter: source.Diameter ?? 2.0,
                receptorHeight: req.ReceptorHeight,
                pollutant: pol),
            "area" => model.CalculateAreaSourceConcentrationField(
                centerLat: source.Latitude, centerLon: source.Longitude,
                areaLength: OrDefault(source.AreaLength, 100),
                areaWidth: OrDefault(source.AreaWidth, 100),
                areaHeight: OrDefault(source.AreaHeight, 0),
                emissionRate: rate,
                gridLat: grid.Lat, gridLon: grid.Lon,
                sigmaZ0: source.SigmaZ0Area,
                receptorHeight: req.ReceptorHeight,
                pollutant: pol),
            "equivalent_area" => ComputeEquivalentAreaField(source, rate, grid, req, model, pol),
            "line" => model.CalculateLineSourceConcentrationField(
                startLat: source.StartLat ?? source.Latitude,
                startLon: source.StartLon ?? source.Longitude,
                endLat: source.EndLat ?? source.Latitude,
                endLon: source.EndLon ?? source.Longitude,
                lineWidth: OrDefault(source.LineWidth, 10),
                lineHeight: OrDefault(source.LineHeight, 0),
                emissionRate: rate,
                gridLat: grid.Lat, gridLon: grid.Lon,
                segmentLength: OrDefault(source.LineSegmentLength, 10),
                sigmaZ0: source.SigmaZ0Line,
                receptorHeight: req.ReceptorHeight,
                pollutant: pol),
            _ => model.CalculateConcentrationField(
                sourceLat: source.Latitude, sourceLon: source.Longitude,
                sourceHeight: source.Height, emissionRate: rate,
                gridLat: grid.Lat, gridLon: grid.Lon,
                stackTemperature: source.Temperature ?? 400.0,
                velocity: source.Velocity ?? 15.0,
                diameter: source.Diameter ?? 2.0,
                receptorHeight: req.ReceptorHeight,
                pollutant: pol),
        };
    }

    private static double[,] ComputeEquivalentAreaField(
        EmissionSource source, double rate,
        GridBuilder.Grid grid, SimulationRequestDto req, GaussianPlumeModel model,
        string pollutant)
    {
        // 取该污染物的实测浓度（可能 req.PollutantType=null 时取最大的那个）。
        // 等效面源需要用该浓度作为源区内部的最大浓度约束。
        double? maxConc = null;
        foreach (var p in source.Pollutants)
        {
            if (req.PollutantType is not null)
            {
                if (p.PollutantType == req.PollutantType && p.Concentration is { } c)
                {
                    maxConc = c;
                    break;
                }
            }
            else if (p.Concentration is { } c)
            {
                maxConc = maxConc is null ? c : Math.Max(maxConc.Value, c);
            }
        }

        if (maxConc is not > 0 || rate <= 0)
            return new double[grid.Lat.Length, grid.Lon.Length];

        return model.CalculateAreaSourceConcentrationField(
            centerLat: source.Latitude, centerLon: source.Longitude,
            areaLength: OrDefault(source.AreaLength, 100),
            areaWidth: OrDefault(source.AreaWidth, 100),
            areaHeight: OrDefault(source.AreaHeight, 0),
            emissionRate: rate,
            gridLat: grid.Lat, gridLon: grid.Lon,
            sigmaZ0: source.SigmaZ0Area,
            receptorHeight: req.ReceptorHeight,
            maxConcentration: maxConc,
            isEquivalent: true,
            pollutant: pollutant);
    }

    // ---------- 受体贡献聚合 ----------
    // 贡献排名按“受体点 -> 污染物 -> 排放源列表”组织。
    // 每个污染物内部先计算各源对该受体的浓度，再按总和转成百分比并降序排序。
    private static Dictionary<string, Dictionary<string, List<ReceptorContributionEntryDto>>>
        ComputeReceptorContributions(
            IReadOnlyList<Receptor> receptors,
            IReadOnlyList<EmissionSource> sources,
            HashSet<string> pollutants,
            GaussianPlumeModel model)
    {
        var result = new Dictionary<string, Dictionary<string, List<ReceptorContributionEntryDto>>>();
        foreach (var receptor in receptors)
        {
            var perPollutant = new Dictionary<string, List<ReceptorContributionEntryDto>>();
            foreach (var pollutant in pollutants)
            {
                var entries = new List<ReceptorContributionEntryDto>();
                var total = 0.0;

                foreach (var source in sources)
                {
                    var rate = GetSourceRateForPollutant(source, pollutant, model);
                    if (rate <= 0) continue;

                    var conc = ComputeReceptorContribution(source, rate, receptor, model, pollutant);
                    if (conc < 1e-6) conc = 0.0;
                    total += conc;
                    entries.Add(new ReceptorContributionEntryDto
                    {
                        SourceId = source.Id,
                        SourceName = source.Name,
                        Concentration = conc,
                        Pollutant = pollutant,
                    });
                }

                foreach (var e in entries)
                    e.Percentage = total > 0 ? e.Concentration / total * 100 : 0;
                entries.Sort((a, b) => b.Concentration.CompareTo(a.Concentration));

                perPollutant[pollutant] = entries;
            }
            result[receptor.Name] = perPollutant;
        }
        return result;
    }

    private static double GetSourceRateForPollutant(EmissionSource source, string pollutantType, GaussianPlumeModel model)
    {
        var rate = 0.0;
        foreach (var p in source.Pollutants)
        {
            if (p.PollutantType != pollutantType) continue;

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
                velocity: source.Velocity ?? 15.0,
                diameter: source.Diameter ?? 2.0,
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
            "line" => model.CalculateLineSourceReceptorConcentration(
                startLat: source.StartLat ?? source.Latitude,
                startLon: source.StartLon ?? source.Longitude,
                endLat: source.EndLat ?? source.Latitude,
                endLon: source.EndLon ?? source.Longitude,
                lineWidth: OrDefault(source.LineWidth, 10),
                lineHeight: OrDefault(source.LineHeight, 0),
                emissionRate: rate,
                receptorLat: receptor.Latitude, receptorLon: receptor.Longitude,
                segmentLength: OrDefault(source.LineSegmentLength, 10),
                sigmaZ0: source.SigmaZ0Line,
                pollutant: pollutant),
            "equivalent_area" => ComputeEquivalentAreaReceptor(source, rate, receptor, model, pollutant),
            _ => model.CalculateReceptorConcentration(
                sourceLat: source.Latitude, sourceLon: source.Longitude,
                sourceHeight: source.Height, emissionRate: rate,
                receptorLat: receptor.Latitude, receptorLon: receptor.Longitude,
                receptorHeight: receptor.Height,
                stackTemperature: source.Temperature ?? 400.0,
                velocity: source.Velocity ?? 15.0,
                diameter: source.Diameter ?? 2.0,
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
            if (p.PollutantType == pollutant && p.Concentration is { } c) { measured = c; break; }
        }
        if (measured is not > 0 || rate <= 0) return 0.0;

        return model.CalculateAreaSourceReceptorConcentration(
            centerLat: source.Latitude, centerLon: source.Longitude,
            areaLength: OrDefault(source.AreaLength, 100),
            areaWidth: OrDefault(source.AreaWidth, 100),
            areaHeight: OrDefault(source.AreaHeight, 0),
            emissionRate: rate,
            receptorLat: receptor.Latitude, receptorLon: receptor.Longitude,
            receptorHeight: receptor.Height,
            sigmaZ0: source.SigmaZ0Area,
            concentration: measured,
            isEquivalent: true,
            pollutant: pollutant);
    }
}
