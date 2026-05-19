using System.Diagnostics;
using GnnSimulation.Api.Dtos;
using GnnSimulation.Data;
using GnnSimulation.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GnnSimulation.Api.Services;

public class ParallelSimulationService
{
    private readonly GnnDbContext _db;

    public ParallelSimulationService(GnnDbContext db) => _db = db;

    public async Task<ParallelSimulationResultDto> RunAsync(
        ParallelSimulationRequestDto request,
        CancellationToken ct = default)
    {
        if (request.WindDirections.Count == 0)
            throw new SimulationBadRequestException("风向列表不能为空");

        var stopwatch = Stopwatch.StartNew();

        var met = await _db.Meteorology.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.MeteorologyId, ct)
            ?? throw new SimulationNotFoundException("气象场未找到");

        var sources = await LoadSourcesAsync(request.SourceIds, ct);
        if (sources.Count == 0)
            throw new SimulationBadRequestException("没有可用的排放源");

        var receptors = await LoadReceptorsAsync(request.ReceptorIds, ct);

        var workerCtx = new WindDirectionWorker.Context(
            Meteorology: met,
            OverrideWindSpeed: request.WindSpeed,
            Sources: sources,
            Receptors: receptors,
            GridResolution: request.GridResolution,
            DomainSize: request.DomainSize,
            PollutantType: request.PollutantType,
            ReceptorHeight: request.ReceptorHeight);

        var numWorkers = request.NumWorkers
            ?? Math.Min(Environment.ProcessorCount, request.WindDirections.Count);
        numWorkers = Math.Max(1, numWorkers);

        // 自动启用聚合模式：估算内存 > 0.5GB 时强制聚合，避免响应超大
        var gridPoints = (int)(request.DomainSize / request.GridResolution) + 1;
        var estimatedGb = (double)gridPoints * gridPoints * 8 * 3 * request.WindDirections.Count
                          / (1024.0 * 1024.0 * 1024.0);
        var aggregated = request.ReturnAggregatedOnly || estimatedGb > 0.5;

        // 并发执行；使用 Task.Run 把 CPU 密集工作从请求线程挪到线程池
        var results = await Task.Run(() =>
        {
            var bag = new System.Collections.Concurrent.ConcurrentBag<WindDirectionResultDto>();
            Parallel.ForEach(
                request.WindDirections,
                new ParallelOptions { MaxDegreeOfParallelism = numWorkers, CancellationToken = ct },
                windDir => bag.Add(WindDirectionWorker.Run(windDir, workerCtx)));
            return bag.OrderBy(r => r.WindDirection).ToList();
        }, ct);

        stopwatch.Stop();

        var successful = results.Where(r => r.Success).ToList();
        var failed = results.Where(r => !r.Success)
            .Select(r => new WindDirectionErrorDto { WindDirection = r.WindDirection, Error = r.Error ?? "未知错误" })
            .ToList();

        var elapsed = stopwatch.Elapsed.TotalSeconds;
        var speedup = elapsed > 0 ? request.WindDirections.Count * 60.0 / elapsed : 0;

        if (aggregated)
        {
            return BuildAggregatedResponse(successful, failed, request, numWorkers, elapsed, speedup);
        }

        return new ParallelSimulationResultDto
        {
            Success = true,
            Mode = "detailed",
            TotalWindDirections = request.WindDirections.Count,
            SuccessfulSimulations = successful.Count,
            FailedSimulations = failed.Count,
            Errors = failed.Count > 0 ? failed : null,
            NumWorkersUsed = numWorkers,
            ComputationTimeSeconds = Math.Round(elapsed, 2),
            SpeedupFactor = Math.Round(speedup, 1),
            Results = results,
        };
    }

    private static ParallelSimulationResultDto BuildAggregatedResponse(
        IReadOnlyList<WindDirectionResultDto> successful,
        IReadOnlyList<WindDirectionErrorDto> failed,
        ParallelSimulationRequestDto request,
        int numWorkers,
        double elapsed,
        double speedup)
    {
        var n = successful.Count;

        // 权重归一化；缺失/长度不对 → 等权重
        var weights = request.Weights is { } w && w.Count == n
            ? w.ToArray()
            : Enumerable.Repeat(1.0 / Math.Max(n, 1), n).ToArray();
        var totalWeight = weights.Sum();
        var normalized = totalWeight > 0
            ? weights.Select(x => x / totalWeight).ToArray()
            : weights;

        double[,]? aggregated = null;
        var aggregatedPollutants = new Dictionary<string, double[,]>();
        double[]? gridLat = null;
        double[]? gridLon = null;
        var availablePollutants = new HashSet<string>();

        for (var i = 0; i < n; i++)
        {
            var r = successful[i];
            var weight = normalized[i];
            var conc2d = JaggedTo2D(r.Concentrations!);

            if (aggregated is null)
            {
                aggregated = new double[conc2d.GetLength(0), conc2d.GetLength(1)];
                gridLat = r.GridLat;
                gridLon = r.GridLon;
            }
            AddScaled(aggregated, conc2d, weight);

            if (r.PollutantConcentrations is not null)
            {
                foreach (var kv in r.PollutantConcentrations)
                {
                    availablePollutants.Add(kv.Key);
                    var p2d = JaggedTo2D(kv.Value);
                    if (!aggregatedPollutants.TryGetValue(kv.Key, out var acc))
                    {
                        acc = new double[p2d.GetLength(0), p2d.GetLength(1)];
                        aggregatedPollutants[kv.Key] = acc;
                    }
                    AddScaled(acc, p2d, weight);
                }
            }
        }

        // 受体贡献度加权聚合：同一 source_id 合并加权浓度
        var aggregatedReceptor = new Dictionary<string, Dictionary<string, List<ReceptorContributionEntryDto>>>();
        for (var i = 0; i < n; i++)
        {
            var weight = normalized[i];
            var receptorData = successful[i].ReceptorContributions;
            if (receptorData is null) continue;

            foreach (var rKv in receptorData)
            {
                if (!aggregatedReceptor.TryGetValue(rKv.Key, out var pMap))
                {
                    pMap = new Dictionary<string, List<ReceptorContributionEntryDto>>();
                    aggregatedReceptor[rKv.Key] = pMap;
                }
                foreach (var pKv in rKv.Value)
                {
                    if (!pMap.TryGetValue(pKv.Key, out var list))
                    {
                        list = new List<ReceptorContributionEntryDto>();
                        pMap[pKv.Key] = list;
                    }
                    foreach (var entry in pKv.Value)
                    {
                        var existing = list.FirstOrDefault(e => e.SourceId == entry.SourceId);
                        var weightedConc = entry.Concentration * weight;
                        if (existing is not null)
                        {
                            existing.Concentration += weightedConc;
                        }
                        else
                        {
                            list.Add(new ReceptorContributionEntryDto
                            {
                                SourceId = entry.SourceId,
                                SourceName = entry.SourceName,
                                Concentration = weightedConc,
                                Pollutant = entry.Pollutant,
                            });
                        }
                    }
                }
            }
        }

        foreach (var rKv in aggregatedReceptor)
            foreach (var pKv in rKv.Value)
            {
                var list = pKv.Value;
                var total = list.Sum(x => x.Concentration);
                list.Sort((a, b) => b.Concentration.CompareTo(a.Concentration));
                foreach (var e in list)
                    e.Percentage = total > 0 ? e.Concentration / total * 100 : 0;
            }

        return new ParallelSimulationResultDto
        {
            Success = true,
            Mode = "aggregated",
            TotalWindDirections = request.WindDirections.Count,
            SuccessfulSimulations = n,
            FailedSimulations = failed.Count,
            Errors = failed.Count > 0 ? failed.ToList() : null,
            NumWorkersUsed = numWorkers,
            ComputationTimeSeconds = Math.Round(elapsed, 2),
            SpeedupFactor = Math.Round(speedup, 1),
            Concentrations = aggregated is null
                ? Array.Empty<double[]>()
                : GridBuilder.ToJagged(aggregated),
            GridLat = gridLat,
            GridLon = gridLon,
            PollutantConcentrations = aggregatedPollutants.Count > 0
                ? aggregatedPollutants.ToDictionary(kv => kv.Key, kv => GridBuilder.ToJagged(kv.Value))
                : null,
            AvailablePollutants = availablePollutants.Count > 0 ? availablePollutants.ToList() : null,
            ReceptorContributions = aggregatedReceptor,
        };
    }

    private async Task<List<EmissionSource>> LoadSourcesAsync(List<int>? ids, CancellationToken ct)
    {
        var q = _db.EmissionSources.AsNoTracking().Include(s => s.Pollutants);
        var filtered = ids is { Count: > 0 } ? q.Where(s => ids.Contains(s.Id)) : q.Where(s => s.IsActive);
        return await filtered.ToListAsync(ct);
    }

    private async Task<List<Receptor>> LoadReceptorsAsync(List<int>? ids, CancellationToken ct)
    {
        var q = _db.Receptors.AsNoTracking();
        var filtered = ids is { Count: > 0 } ? q.Where(r => ids.Contains(r.Id)) : q.Where(r => r.IsActive);
        return await filtered.ToListAsync(ct);
    }

    private static void AddScaled(double[,] target, double[,] source, double scale)
    {
        var n0 = target.GetLength(0);
        var n1 = target.GetLength(1);
        for (var i = 0; i < n0; i++)
            for (var j = 0; j < n1; j++)
                target[i, j] += source[i, j] * scale;
    }

    private static double[,] JaggedTo2D(double[][] jagged)
    {
        var n0 = jagged.Length;
        var n1 = n0 > 0 ? jagged[0].Length : 0;
        var m = new double[n0, n1];
        for (var i = 0; i < n0; i++)
            for (var j = 0; j < n1; j++)
                m[i, j] = jagged[i][j];
        return m;
    }
}
