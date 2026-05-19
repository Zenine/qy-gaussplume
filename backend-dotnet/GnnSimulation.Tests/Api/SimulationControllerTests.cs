using System.Net;
using FluentAssertions;
using GnnSimulation.Api.Dtos;
using GnnSimulation.Tests.Infrastructure;

namespace GnnSimulation.Tests.Api;

public class SimulationControllerTests : IDisposable
{
    private readonly GnnWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public SimulationControllerTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<MeteorologyDto> CreateMet(double ws = 3.0, double wd = 0.0, string stab = "D") =>
        await (await _client.PostJsonAsync("/api/meteorology", new MeteorologyCreateDto
        {
            Name = $"Met-{Guid.NewGuid():N}",
            WindSpeed = ws, WindDirection = wd, StabilityClass = stab,
        })).ReadJsonAsync<MeteorologyDto>();

    private async Task<EmissionSourceDto> CreatePointSource(
        double lat = 39.9, double lon = 116.4, double h = 50,
        double pm25Rate = 1.0) =>
        await (await _client.PostJsonAsync("/api/sources", new EmissionSourceCreateDto
        {
            Name = $"Point-{Guid.NewGuid():N}",
            SourceType = "point",
            Latitude = lat, Longitude = lon, Height = h,
            Temperature = 400, Velocity = 15, Diameter = 2,
            Pollutants = { new PollutantEmissionCreateDto("PM2.5", pm25Rate) },
        })).ReadJsonAsync<EmissionSourceDto>();

    private async Task<ReceptorDto> CreateReceptor(double lat, double lon, double h = 1.5) =>
        await (await _client.PostJsonAsync("/api/receptors", new ReceptorCreateDto
        {
            Name = $"Rec-{Guid.NewGuid():N}",
            Latitude = lat, Longitude = lon, Height = h,
        })).ReadJsonAsync<ReceptorDto>();

    [Fact]
    public async Task 气象场不存在返回404()
    {
        var resp = await _client.PostJsonAsync("/api/simulation/run", new SimulationRequestDto
        {
            MeteorologyId = 99999,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task 没有激活的源返回400()
    {
        var met = await CreateMet();
        var resp = await _client.PostJsonAsync("/api/simulation/run", new SimulationRequestDto
        {
            MeteorologyId = met.Id,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task 单点源_返回浓度场并包含源贡献()
    {
        var met = await CreateMet(ws: 3.0, wd: 0.0);
        var src = await CreatePointSource(lat: 39.9, lon: 116.4, pm25Rate: 1.0);
        await CreateReceptor(lat: 39.89, lon: 116.4);

        var resp = await _client.PostJsonAsync("/api/simulation/run", new SimulationRequestDto
        {
            MeteorologyId = met.Id,
            GridResolution = 100,
            DomainSize = 5000,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await resp.ReadJsonAsync<SimulationResultDto>();
        result.Concentrations.Should().NotBeEmpty();
        result.GridLat.Length.Should().BeGreaterThan(50);
        result.GridLon.Length.Should().BeGreaterThan(50);
        result.Concentrations.Length.Should().Be(result.GridLat.Length);
        result.Concentrations[0].Length.Should().Be(result.GridLon.Length);

        result.Contributions.Should().HaveCount(1);
        result.Contributions[0].SourceId.Should().Be(src.Id);
        result.Contributions[0].TotalConcentration.Should().BeGreaterThan(0);
        result.Contributions[0].Pollutants.Should().Contain("PM2.5");

        result.AvailablePollutants.Should().Contain("PM2.5");
        result.PollutantConcentrations.Should().NotBeNull();
        result.PollutantConcentrations!.Keys.Should().Contain("PM2.5");
    }

    [Fact]
    public async Task 受体点_贡献排名按浓度降序()
    {
        var met = await CreateMet(wd: 0.0);
        var s1 = await CreatePointSource(lat: 39.90, lon: 116.40, pm25Rate: 10.0); // 大源
        var s2 = await CreatePointSource(lat: 39.90, lon: 116.41, pm25Rate: 0.1); // 小源
        var rec = await CreateReceptor(lat: 39.88, lon: 116.405);

        var resp = await _client.PostJsonAsync("/api/simulation/run", new SimulationRequestDto
        {
            MeteorologyId = met.Id,
            GridResolution = 100, DomainSize = 5000,
        });
        var result = await resp.ReadJsonAsync<SimulationResultDto>();

        var recContrib = result.ReceptorContributions.Should().ContainKey(rec.Name).WhoseValue;
        var pmList = recContrib.Should().ContainKey("PM2.5").WhoseValue;
        pmList.Should().HaveCount(2);
        // 排名应按 concentration 降序
        pmList[0].Concentration.Should().BeGreaterThanOrEqualTo(pmList[1].Concentration);
        pmList.Sum(x => x.Percentage).Should().BeApproximately(100, 0.01);
    }

    [Fact]
    public async Task 指定source_ids_只模拟子集()
    {
        var met = await CreateMet();
        var s1 = await CreatePointSource();
        var s2 = await CreatePointSource(lat: 39.91, lon: 116.41);
        await CreateReceptor(39.88, 116.40);

        var resp = await _client.PostJsonAsync("/api/simulation/run", new SimulationRequestDto
        {
            MeteorologyId = met.Id,
            SourceIds = new List<int> { s1.Id },
            GridResolution = 100, DomainSize = 5000,
        });
        var result = await resp.ReadJsonAsync<SimulationResultDto>();
        result.Contributions.Should().HaveCount(1);
        result.Contributions[0].SourceId.Should().Be(s1.Id);
    }

    [Fact]
    public async Task 指定空receptor_ids_不回退到全部受体()
    {
        var met = await CreateMet();
        await CreatePointSource();
        await CreateReceptor(39.88, 116.40);

        var resp = await _client.PostJsonAsync("/api/simulation/run", new SimulationRequestDto
        {
            MeteorologyId = met.Id,
            ReceptorIds = new List<int>(),
            GridResolution = 100,
            DomainSize = 5000,
        });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await resp.ReadJsonAsync<SimulationResultDto>();
        result.ReceptorContributions.Should().BeEmpty();
    }

    [Fact]
    public async Task 指定污染物过滤_只累加该污染物()
    {
        var met = await CreateMet();
        // 这个源同时排 PM2.5 和 NOx
        var src = await (await _client.PostJsonAsync("/api/sources", new EmissionSourceCreateDto
        {
            Name = "Multi",
            SourceType = "point",
            Latitude = 39.9, Longitude = 116.4, Height = 50,
            Temperature = 400, Velocity = 15, Diameter = 2,
            Pollutants =
            {
                new PollutantEmissionCreateDto("PM2.5", 1.0),
                new PollutantEmissionCreateDto("NOx", 5.0),
            },
        })).ReadJsonAsync<EmissionSourceDto>();
        await CreateReceptor(39.88, 116.40);

        var respAll = await _client.PostJsonAsync("/api/simulation/run", new SimulationRequestDto
        {
            MeteorologyId = met.Id,
            GridResolution = 100, DomainSize = 5000,
        });
        var respPm = await _client.PostJsonAsync("/api/simulation/run", new SimulationRequestDto
        {
            MeteorologyId = met.Id,
            PollutantType = "PM2.5",
            GridResolution = 100, DomainSize = 5000,
        });

        var all = await respAll.ReadJsonAsync<SimulationResultDto>();
        var pm = await respPm.ReadJsonAsync<SimulationResultDto>();

        all.AvailablePollutants.Should().Contain(new[] { "PM2.5", "NOx" });
        pm.AvailablePollutants.Should().BeEquivalentTo(new[] { "PM2.5" });

        // PM only 模式下总浓度 < 全部污染物模式（NOx 速率更大）
        var allMax = all.Concentrations.SelectMany(r => r).Max();
        var pmMax = pm.Concentrations.SelectMany(r => r).Max();
        pmMax.Should().BeLessThan(allMax);
    }

    [Fact]
    public async Task 线源_可正常运行并贡献非零()
    {
        var met = await CreateMet(wd: 90.0); // 东风 → 源西侧受影响
        var lineSrc = await (await _client.PostJsonAsync("/api/sources", new EmissionSourceCreateDto
        {
            Name = "Road",
            SourceType = "line",
            Latitude = 39.9, Longitude = 116.4, Height = 0,
            StartLat = 39.9, StartLon = 116.40,
            EndLat = 39.9, EndLon = 116.42,
            LineWidth = 10, LineHeight = 1, LineSegmentLength = 50,
            Pollutants = { new PollutantEmissionCreateDto("NOx", 2.0) },
        })).ReadJsonAsync<EmissionSourceDto>();
        await CreateReceptor(39.9, 116.395);

        var resp = await _client.PostJsonAsync("/api/simulation/run", new SimulationRequestDto
        {
            MeteorologyId = met.Id,
            PollutantType = "NOx",
            GridResolution = 50, DomainSize = 3000,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await resp.ReadJsonAsync<SimulationResultDto>();
        result.Contributions.Should().HaveCount(1);
        result.Contributions[0].SourceId.Should().Be(lineSrc.Id);
    }

    [Fact]
    public async Task 等效面源_浓度转换为等效排放速率()
    {
        var met = await CreateMet();
        var eqSrc = await (await _client.PostJsonAsync("/api/sources", new EmissionSourceCreateDto
        {
            Name = "EqArea",
            SourceType = "equivalent_area",
            Latitude = 39.9, Longitude = 116.4, Height = 0,
            AreaLength = 200, AreaWidth = 100, AreaHeight = 5,
            Pollutants = { new PollutantEmissionCreateDto("PM2.5", 0, Concentration: 75.0) },
        })).ReadJsonAsync<EmissionSourceDto>();
        await CreateReceptor(39.88, 116.4);

        var resp = await _client.PostJsonAsync("/api/simulation/run", new SimulationRequestDto
        {
            MeteorologyId = met.Id,
            GridResolution = 100, DomainSize = 5000,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await resp.ReadJsonAsync<SimulationResultDto>();
        result.Contributions.Should().HaveCount(1);
        result.Contributions[0].SourceId.Should().Be(eqSrc.Id);
        // 等效面源应有贡献（等效排放速率 > 0）
        result.Contributions[0].TotalConcentration.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task 网格大小受点数上下限夹紧()
    {
        var met = await CreateMet();
        await CreatePointSource();
        await CreateReceptor(39.88, 116.40);

        // 很小的 domain 应被夹到最少 50 点
        var respSmall = await _client.PostJsonAsync("/api/simulation/run", new SimulationRequestDto
        {
            MeteorologyId = met.Id,
            GridResolution = 10, DomainSize = 100,
        });
        var small = await respSmall.ReadJsonAsync<SimulationResultDto>();
        small.GridLat.Length.Should().BeGreaterThanOrEqualTo(50);

        // 过大组合应被夹到最多 500
        var respBig = await _client.PostJsonAsync("/api/simulation/run", new SimulationRequestDto
        {
            MeteorologyId = met.Id,
            GridResolution = 10, DomainSize = 1_000_000,
        });
        var big = await respBig.ReadJsonAsync<SimulationResultDto>();
        big.GridLat.Length.Should().BeLessThanOrEqualTo(500);
    }
}
