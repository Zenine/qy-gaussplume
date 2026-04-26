using System.Net;
using FluentAssertions;
using GnnSimulation.Api.Dtos;
using GnnSimulation.Tests.Infrastructure;

namespace GnnSimulation.Tests.Api;

public class ParallelSimulationTests : IDisposable
{
    private readonly GnnWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public ParallelSimulationTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<MeteorologyDto> CreateMet() =>
        await (await _client.PostJsonAsync("/api/meteorology", new MeteorologyCreateDto
        {
            Name = $"Met-{Guid.NewGuid():N}",
            WindSpeed = 3.0, StabilityClass = "D",
        })).ReadJsonAsync<MeteorologyDto>();

    private async Task<EmissionSourceDto> CreatePoint(double pm25 = 1.0, double lat = 39.9, double lon = 116.4) =>
        await (await _client.PostJsonAsync("/api/sources", new EmissionSourceCreateDto
        {
            Name = $"S-{Guid.NewGuid():N}",
            SourceType = "point",
            Latitude = lat, Longitude = lon, Height = 50,
            Temperature = 400, Velocity = 15, Diameter = 2,
            Pollutants = { new PollutantEmissionCreateDto("PM2.5", pm25) },
        })).ReadJsonAsync<EmissionSourceDto>();

    private async Task<ReceptorDto> CreateReceptor(double lat, double lon) =>
        await (await _client.PostJsonAsync("/api/receptors", new ReceptorCreateDto
        {
            Name = $"R-{Guid.NewGuid():N}",
            Latitude = lat, Longitude = lon, Height = 1.5,
        })).ReadJsonAsync<ReceptorDto>();

    [Fact]
    public async Task 空风向列表_返回400()
    {
        var met = await CreateMet();
        await CreatePoint();
        var resp = await _client.PostJsonAsync("/api/simulation/run_parallel", new ParallelSimulationRequestDto
        {
            MeteorologyId = met.Id,
            WindSpeed = 3.0,
            WindDirections = new List<double>(),
        });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task 气象场不存在返回404()
    {
        var resp = await _client.PostJsonAsync("/api/simulation/run_parallel", new ParallelSimulationRequestDto
        {
            MeteorologyId = 99999,
            WindSpeed = 3.0,
            WindDirections = new List<double> { 0 },
        });
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task 详细模式_返回每风向结果()
    {
        var met = await CreateMet();
        await CreatePoint();
        await CreateReceptor(39.88, 116.4);

        var resp = await _client.PostJsonAsync("/api/simulation/run_parallel", new ParallelSimulationRequestDto
        {
            MeteorologyId = met.Id,
            WindSpeed = 3.0,
            WindDirections = new List<double> { 0, 90, 180, 270 },
            GridResolution = 200, DomainSize = 4000,
            ReturnAggregatedOnly = false,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await resp.ReadJsonAsync<ParallelSimulationResultDto>();
        result.Mode.Should().Be("detailed");
        result.TotalWindDirections.Should().Be(4);
        result.SuccessfulSimulations.Should().Be(4);
        result.Results.Should().NotBeNull();
        result.Results!.Should().HaveCount(4);
        result.Results!.Select(r => r.WindDirection).Should().Equal(0, 90, 180, 270);
        result.Results!.All(r => r.Success).Should().BeTrue();
        result.Results![0].Concentrations.Should().NotBeNull();
    }

    [Fact]
    public async Task 聚合模式_只返回合成浓度场()
    {
        var met = await CreateMet();
        await CreatePoint();
        await CreateReceptor(39.88, 116.4);

        var resp = await _client.PostJsonAsync("/api/simulation/run_parallel", new ParallelSimulationRequestDto
        {
            MeteorologyId = met.Id,
            WindSpeed = 3.0,
            WindDirections = new List<double> { 0, 45, 90, 135, 180, 225, 270, 315 },
            GridResolution = 200, DomainSize = 4000,
            ReturnAggregatedOnly = true,
        });
        var result = await resp.ReadJsonAsync<ParallelSimulationResultDto>();

        result.Mode.Should().Be("aggregated");
        result.SuccessfulSimulations.Should().Be(8);
        result.Concentrations.Should().NotBeNull();
        result.Results.Should().BeNull(); // 聚合模式下不返回明细
        result.AvailablePollutants.Should().Contain("PM2.5");
    }

    [Fact]
    public async Task 等权重_聚合结果等于各风向浓度场的均值()
    {
        var met = await CreateMet();
        await CreatePoint();
        await CreateReceptor(39.88, 116.4);

        var dirs = new List<double> { 0, 90, 180, 270 };

        var detailedResp = await _client.PostJsonAsync("/api/simulation/run_parallel", new ParallelSimulationRequestDto
        {
            MeteorologyId = met.Id,
            WindSpeed = 3.0,
            WindDirections = dirs,
            GridResolution = 200, DomainSize = 4000,
            ReturnAggregatedOnly = false,
        });
        var detailed = await detailedResp.ReadJsonAsync<ParallelSimulationResultDto>();

        var aggResp = await _client.PostJsonAsync("/api/simulation/run_parallel", new ParallelSimulationRequestDto
        {
            MeteorologyId = met.Id,
            WindSpeed = 3.0,
            WindDirections = dirs,
            GridResolution = 200, DomainSize = 4000,
            ReturnAggregatedOnly = true,
        });
        var agg = await aggResp.ReadJsonAsync<ParallelSimulationResultDto>();

        // 手动对每风向结果求平均，应与服务端聚合一致
        var nLat = agg.Concentrations!.Length;
        var nLon = agg.Concentrations[0].Length;

        for (var i = 0; i < nLat; i++)
        {
            for (var j = 0; j < nLon; j++)
            {
                var avg = detailed.Results!.Sum(r => r.Concentrations![i][j]) / detailed.Results!.Count;
                agg.Concentrations[i][j].Should().BeApproximately(avg, 1e-9);
            }
        }
    }

    [Fact]
    public async Task 非等权重_加权结果正确()
    {
        var met = await CreateMet();
        await CreatePoint();
        await CreateReceptor(39.88, 116.4);

        var dirs = new List<double> { 0, 180 };
        var weights = new List<double> { 3.0, 1.0 }; // 0° 权重是 180° 的 3 倍

        var detailedResp = await _client.PostJsonAsync("/api/simulation/run_parallel", new ParallelSimulationRequestDto
        {
            MeteorologyId = met.Id,
            WindSpeed = 3.0,
            WindDirections = dirs,
            GridResolution = 200, DomainSize = 4000,
            ReturnAggregatedOnly = false,
        });
        var detailed = await detailedResp.ReadJsonAsync<ParallelSimulationResultDto>();

        var aggResp = await _client.PostJsonAsync("/api/simulation/run_parallel", new ParallelSimulationRequestDto
        {
            MeteorologyId = met.Id,
            WindSpeed = 3.0,
            WindDirections = dirs,
            Weights = weights,
            GridResolution = 200, DomainSize = 4000,
            ReturnAggregatedOnly = true,
        });
        var agg = await aggResp.ReadJsonAsync<ParallelSimulationResultDto>();

        // 预期: (3/4) * dir0 + (1/4) * dir180
        var nLat = agg.Concentrations!.Length;
        var nLon = agg.Concentrations[0].Length;
        var r0 = detailed.Results!.First(r => r.WindDirection == 0);
        var r180 = detailed.Results!.First(r => r.WindDirection == 180);

        for (var i = 0; i < nLat; i++)
        {
            for (var j = 0; j < nLon; j++)
            {
                var expected = 0.75 * r0.Concentrations![i][j] + 0.25 * r180.Concentrations![i][j];
                agg.Concentrations[i][j].Should().BeApproximately(expected, 1e-9);
            }
        }
    }

    [Fact]
    public async Task 聚合模式_受体贡献按聚合浓度排名()
    {
        var met = await CreateMet();
        var sBig = await CreatePoint(pm25: 10.0, lat: 39.90, lon: 116.40);
        var sSmall = await CreatePoint(pm25: 0.1, lat: 39.90, lon: 116.41);
        var rec = await CreateReceptor(39.88, 116.405);

        var resp = await _client.PostJsonAsync("/api/simulation/run_parallel", new ParallelSimulationRequestDto
        {
            MeteorologyId = met.Id,
            WindSpeed = 3.0,
            WindDirections = new List<double> { 0, 90, 180, 270 },
            GridResolution = 200, DomainSize = 4000,
        });
        var result = await resp.ReadJsonAsync<ParallelSimulationResultDto>();

        result.ReceptorContributions.Should().NotBeNull();
        var list = result.ReceptorContributions![rec.Name]["PM2.5"];
        list.Should().NotBeEmpty();
        // 大源应排第一
        list.First().SourceId.Should().Be(sBig.Id);
        // 百分比总和 = 100
        list.Sum(x => x.Percentage).Should().BeApproximately(100, 0.01);
    }

    [Fact]
    public async Task 单风向_并行服务_结果与单风向等效()
    {
        // 并行服务在 1 个风向下的输出，浓度场应与把该风向直接写入气象场的 /run 输出非常接近
        // （存在差异的原因：/run 的网格是基于源+受体外包盒，/run_parallel 的网格基于源中心+domain 大小）
        // 这里只做结构性验证：1 个成功结果 + 浓度场非空
        var met = await CreateMet();
        await CreatePoint();
        await CreateReceptor(39.88, 116.4);

        var resp = await _client.PostJsonAsync("/api/simulation/run_parallel", new ParallelSimulationRequestDto
        {
            MeteorologyId = met.Id,
            WindSpeed = 3.0,
            WindDirections = new List<double> { 0 },
            GridResolution = 200, DomainSize = 4000,
            ReturnAggregatedOnly = true,
        });
        var result = await resp.ReadJsonAsync<ParallelSimulationResultDto>();
        result.SuccessfulSimulations.Should().Be(1);
        result.FailedSimulations.Should().Be(0);

        var nonZero = result.Concentrations!.SelectMany(r => r).Count(x => x > 0);
        nonZero.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task 网格过大_自动切换聚合模式()
    {
        var met = await CreateMet();
        await CreatePoint();

        // 10km / 10m = 1001 点; 72 风向 → ~1.6GB → 超过 0.5GB 阈值应强制聚合
        // 实际跑会非常慢，换小一点但仍超阈值的配置：500 点 * 500 * 8B * 3 * 10 风向 ≈ 57MB，不够
        // 用 1200 点 * 1200 * 8B * 3 * 10 = 345MB，仍 < 0.5GB
        // 用 1200 * 1200 * 8 * 3 * 20 = 691MB，超过阈值
        // 但这样会很慢。改为验证只要 ReturnAggregatedOnly=false 且 Mode=aggregated，说明自动切换生效
        var resp = await _client.PostJsonAsync("/api/simulation/run_parallel", new ParallelSimulationRequestDto
        {
            MeteorologyId = met.Id,
            WindSpeed = 3.0,
            WindDirections = Enumerable.Range(0, 20).Select(i => (double)(i * 18)).ToList(),
            GridResolution = 5,  // 1200x1200 近似
            DomainSize = 6000,
            ReturnAggregatedOnly = false,
        });
        var result = await resp.ReadJsonAsync<ParallelSimulationResultDto>();
        // 虽然请求 detailed，但被自动切换为 aggregated（因内存超阈值）
        result.Mode.Should().Be("aggregated");
    }
}
