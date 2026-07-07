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

    private async Task<EmissionSourceDto> CreateAreaSource(
        double lat = 39.9, double lon = 116.4,
        double length = 4000, double width = 2000) =>
        await (await _client.PostJsonAsync("/api/sources", new EmissionSourceCreateDto
        {
            Name = $"Area-{Guid.NewGuid():N}",
            SourceType = "area",
            Latitude = lat, Longitude = lon, Height = 10,
            AreaLength = length, AreaWidth = width, AreaHeight = 5,
            Pollutants = { new PollutantEmissionCreateDto("PM2.5", 1.0) },
        })).ReadJsonAsync<EmissionSourceDto>();

    private async Task<EmissionSourceDto> CreateLineSource(
        double startLat, double startLon, double endLat, double endLon) =>
        await (await _client.PostJsonAsync("/api/sources", new EmissionSourceCreateDto
        {
            Name = $"Line-{Guid.NewGuid():N}",
            SourceType = "line",
            Latitude = (startLat + endLat) / 2,
            Longitude = (startLon + endLon) / 2,
            Height = 5,
            StartLat = startLat, StartLon = startLon,
            EndLat = endLat, EndLon = endLon,
            LineWidth = 20,
            Pollutants = { new PollutantEmissionCreateDto("PM2.5", 1.0) },
        })).ReadJsonAsync<EmissionSourceDto>();

    private async Task<ReceptorDto> CreateReceptor(double lat, double lon, double h = 1.5) =>
        await (await _client.PostJsonAsync("/api/receptors", new ReceptorCreateDto
        {
            Name = $"Rec-{Guid.NewGuid():N}",
            Latitude = lat, Longitude = lon, Height = h,
        })).ReadJsonAsync<ReceptorDto>();

    private static double AxisCenter(IReadOnlyCollection<double> values) =>
        (values.Min() + values.Max()) / 2;

    private static double AxisSpan(IReadOnlyCollection<double> values) =>
        values.Max() - values.Min();

    private static void AssertNorthWindKeepsSourceAtPlumeStart(
        SimulationResultDto result,
        double anchorLat,
        double anchorLon)
    {
        var northRange = result.GridLat.Max() - anchorLat;
        var southRange = anchorLat - result.GridLat.Min();

        AxisCenter(result.GridLat).Should().BeLessThan(anchorLat);
        AxisCenter(result.GridLon).Should().BeApproximately(anchorLon, 1e-9);
        southRange.Should().BeGreaterThan(northRange * 2.5);
    }

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
    public async Task 公式说明_返回污染物参数和源类型说明()
    {
        var resp = await _client.GetAsync("/api/simulation/formulas");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await resp.ReadJsonAsync<SimulationFormulaInfoDto>();
        dto.GaussianPlumeFormula.Should().Contain("exp");
        dto.DecayFormula.Should().Contain("BLH");
        dto.DecayFormula.Should().Contain("cloud_factor");
        dto.WindAggregationFormula.Should().Contain("权重");
        dto.Pollutants.Should().Contain(p => p.Type == "PM2.5" && p.GravitationalSettlingVelocity > 0);
        dto.Pollutants.Should().Contain(p => p.Type == "NOx" && p.ChemicalEnhanced);
        dto.SourceTypes.Should().Contain(s => s.Type == "area" && s.Formula.Contains("σ_eff"));
        dto.SourceTypes.Should().Contain(s => s.Type == "line" && s.Formula.Contains("segment"));
        dto.SourceTypes.Should().Contain(s => s.Type == "equivalent_area" && s.Formula.Contains("concentration"));
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
        result.GridLat.Length.Should().BeGreaterThanOrEqualTo(50);
        result.GridLon.Length.Should().BeGreaterThanOrEqualTo(50);
        result.Concentrations.Length.Should().Be(result.GridLat.Length);
        result.Concentrations[0].Length.Should().Be(result.GridLon.Length);

        var northRange = result.GridLat.Max() - src.Latitude;
        var southRange = src.Latitude - result.GridLat.Min();
        southRange.Should().BeGreaterThan(northRange * 2.5);

        result.Contributions.Should().HaveCount(1);
        result.Contributions[0].SourceId.Should().Be(src.Id);
        result.Contributions[0].TotalConcentration.Should().BeGreaterThan(0);
        result.Contributions[0].Pollutants.Should().Contain("PM2.5");

        result.AvailablePollutants.Should().Contain("PM2.5");
        result.PollutantConcentrations.Should().NotBeNull();
        result.PollutantConcentrations!.Keys.Should().Contain("PM2.5");
    }

    [Fact]
    public async Task 单风向扩散网格_点源线源面源都以源几何为起点向下风向展开()
    {
        var met = await CreateMet(ws: 3.0, wd: 0.0);
        await AssertNorthWindSourceGeometryAsync(
            await CreatePointSource(lat: 39.9, lon: 116.4),
            minLat: 39.9,
            maxLat: 39.9,
            minLon: 116.4,
            maxLon: 116.4);

        await AssertNorthWindSourceGeometryAsync(
            await CreateLineSource(startLat: 39.88, startLon: 116.36, endLat: 39.92, endLon: 116.44),
            minLat: 39.88,
            maxLat: 39.92,
            minLon: 116.36,
            maxLon: 116.44);

        var areaLatHalf = 2_000.0 / 111_000.0;
        var areaLonHalf = 1_000.0 / (111_000.0 * Math.Cos(39.9 * Math.PI / 180.0));
        await AssertNorthWindSourceGeometryAsync(
            await CreateAreaSource(lat: 39.9, lon: 116.4, length: 4_000, width: 2_000),
            minLat: 39.9 - areaLatHalf,
            maxLat: 39.9 + areaLatHalf,
            minLon: 116.4 - areaLonHalf,
            maxLon: 116.4 + areaLonHalf);

        async Task AssertNorthWindSourceGeometryAsync(
            EmissionSourceDto source,
            double minLat,
            double maxLat,
            double minLon,
            double maxLon)
        {
            var resp = await _client.PostJsonAsync("/api/simulation/run", new SimulationRequestDto
            {
                MeteorologyId = met.Id,
                SourceIds = new List<int> { source.Id },
                ReceptorIds = new List<int>(),
                GridResolution = 100,
                DomainSize = 10_000,
            });
            resp.StatusCode.Should().Be(HttpStatusCode.OK);

            var result = await resp.ReadJsonAsync<SimulationResultDto>();
            result.GridLat.Min().Should().BeLessThan(minLat);
            result.GridLat.Max().Should().BeGreaterThan(maxLat);
            result.GridLon.Min().Should().BeLessThan(minLon);
            result.GridLon.Max().Should().BeGreaterThan(maxLon);

            var sourceCenterLat = (minLat + maxLat) / 2;
            var sourceCenterLon = (minLon + maxLon) / 2;
            var southSpace = minLat - result.GridLat.Min();
            var northSpace = result.GridLat.Max() - maxLat;
            southSpace.Should().BeGreaterThan(northSpace);
            AxisCenter(result.GridLat).Should().BeLessThan(sourceCenterLat);
            AxisCenter(result.GridLon).Should().BeApproximately(sourceCenterLon, 1e-9);
        }
    }


    [Fact]
    public async Task 模拟范围增大时_浓度场网格范围同步扩大()
    {
        var met = await CreateMet(ws: 3.0, wd: 0.0);
        await CreatePointSource(lat: 39.9, lon: 116.4, pm25Rate: 1.0);
        await CreateReceptor(lat: 39.89, lon: 116.4);

        var smallResp = await _client.PostJsonAsync("/api/simulation/run", new SimulationRequestDto
        {
            MeteorologyId = met.Id,
            GridResolution = 100,
            DomainSize = 5000,
        });
        var largeResp = await _client.PostJsonAsync("/api/simulation/run", new SimulationRequestDto
        {
            MeteorologyId = met.Id,
            GridResolution = 100,
            DomainSize = 20000,
        });

        smallResp.StatusCode.Should().Be(HttpStatusCode.OK);
        largeResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var small = await smallResp.ReadJsonAsync<SimulationResultDto>();
        var large = await largeResp.ReadJsonAsync<SimulationResultDto>();

        var smallLatSpan = small.GridLat.Max() - small.GridLat.Min();
        var largeLatSpan = large.GridLat.Max() - large.GridLat.Min();
        var smallLonSpan = small.GridLon.Max() - small.GridLon.Min();
        var largeLonSpan = large.GridLon.Max() - large.GridLon.Min();

        largeLatSpan.Should().BeGreaterThan(smallLatSpan * 2);
        largeLonSpan.Should().BeGreaterThan(smallLonSpan * 2);
    }

    [Fact]
    public async Task 单源远受体_单风向网格按下风向偏移且不被远受体拉偏()
    {
        var met = await CreateMet(ws: 3.0, wd: 0.0);
        await CreatePointSource(lat: 39.9, lon: 116.4, pm25Rate: 1.0);
        await CreateReceptor(lat: 39.7, lon: 116.8);

        var resp = await _client.PostJsonAsync("/api/simulation/run", new SimulationRequestDto
        {
            MeteorologyId = met.Id,
            GridResolution = 100,
            DomainSize = 10_000,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await resp.ReadJsonAsync<SimulationResultDto>();
        AssertNorthWindKeepsSourceAtPlumeStart(result, 39.9, 116.4);
    }

    [Fact]
    public async Task 单源调大模拟范围_网格扩大且下风向空间同步扩大()
    {
        var met = await CreateMet(ws: 3.0, wd: 0.0);
        await CreatePointSource(lat: 39.9, lon: 116.4, pm25Rate: 1.0);
        await CreateReceptor(lat: 39.7, lon: 116.8);

        var smallResp = await _client.PostJsonAsync("/api/simulation/run", new SimulationRequestDto
        {
            MeteorologyId = met.Id,
            GridResolution = 100,
            DomainSize = 5_000,
        });
        var largeResp = await _client.PostJsonAsync("/api/simulation/run", new SimulationRequestDto
        {
            MeteorologyId = met.Id,
            GridResolution = 100,
            DomainSize = 20_000,
        });
        smallResp.StatusCode.Should().Be(HttpStatusCode.OK);
        largeResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var small = await smallResp.ReadJsonAsync<SimulationResultDto>();
        var large = await largeResp.ReadJsonAsync<SimulationResultDto>();
        AssertNorthWindKeepsSourceAtPlumeStart(small, 39.9, 116.4);
        AssertNorthWindKeepsSourceAtPlumeStart(large, 39.9, 116.4);
        AxisSpan(large.GridLat).Should().BeGreaterThan(AxisSpan(small.GridLat) * 2);
        AxisSpan(large.GridLon).Should().BeGreaterThan(AxisSpan(small.GridLon) * 2);
    }

    [Fact]
    public async Task 多源远受体_单风向网格使用参与源外包框并向下风向留足空间()
    {
        var met = await CreateMet(ws: 3.0, wd: 0.0);
        await CreatePointSource(lat: 39.9, lon: 116.4, pm25Rate: 1.0);
        await CreatePointSource(lat: 39.94, lon: 116.5, pm25Rate: 1.0);
        await CreateReceptor(lat: 39.7, lon: 116.8);

        var resp = await _client.PostJsonAsync("/api/simulation/run", new SimulationRequestDto
        {
            MeteorologyId = met.Id,
            GridResolution = 100,
            DomainSize = 20_000,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await resp.ReadJsonAsync<SimulationResultDto>();
        AssertNorthWindKeepsSourceAtPlumeStart(result, (39.9 + 39.94) / 2, (116.4 + 116.5) / 2);
        AxisCenter(result.GridLon).Should().BeApproximately((116.4 + 116.5) / 2, 1e-9);
    }

    [Fact]
    public async Task 面源_网格范围使用面源几何外包框()
    {
        var met = await CreateMet(ws: 3.0, wd: 0.0);
        await CreateAreaSource(lat: 39.9, lon: 116.4, length: 4000, width: 2000);
        await CreateReceptor(lat: 39.7, lon: 116.8);

        var resp = await _client.PostJsonAsync("/api/simulation/run", new SimulationRequestDto
        {
            MeteorologyId = met.Id,
            GridResolution = 100,
            DomainSize = 1_000,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await resp.ReadJsonAsync<SimulationResultDto>();
        var latRangeMeters = AxisSpan(result.GridLat) * 111_000;
        var lonRangeMeters = AxisSpan(result.GridLon) * 111_000
            * Math.Cos(result.GridLat.Average() * Math.PI / 180.0);
        AxisCenter(result.GridLat).Should().BeLessThan(39.9);
        AxisCenter(result.GridLon).Should().BeApproximately(116.4, 1e-9);
        result.GridLat.Min().Should().BeLessThan(39.9 - 2_000.0 / 111_000.0);
        result.GridLat.Max().Should().BeGreaterThan(39.9 + 2_000.0 / 111_000.0);
        latRangeMeters.Should().BeGreaterThan(4_000);
        lonRangeMeters.Should().BeGreaterThan(4_000);
    }

    [Fact]
    public async Task 面源_长宽方向与主线保持一致()
    {
        var met = await CreateMet(ws: 3.0, wd: 0.0);
        await CreateAreaSource(lat: 39.9, lon: 116.4, length: 6000, width: 1000);
        await CreatePointSource(lat: 39.96, lon: 116.4);
        await CreateReceptor(lat: 39.7, lon: 116.8);

        var resp = await _client.PostJsonAsync("/api/simulation/run", new SimulationRequestDto
        {
            MeteorologyId = met.Id,
            GridResolution = 100,
            DomainSize = 1_000,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await resp.ReadJsonAsync<SimulationResultDto>();
        var expectedAreaSouthEdge = 39.9 - 3_000.0 / 111_000.0;
        var expectedCenterLat = (expectedAreaSouthEdge + 39.96) / 2;
        AxisCenter(result.GridLat).Should().BeLessThan(expectedCenterLat);
        AxisCenter(result.GridLon).Should().BeApproximately(116.4, 1e-9);
        result.GridLat.Max().Should().BeGreaterThan(39.96);
    }

    [Fact]
    public async Task 线源_单风向网格使用起终点外包框并向下风向留足空间()
    {
        var met = await CreateMet(ws: 3.0, wd: 0.0);
        await CreateLineSource(startLat: 39.88, startLon: 116.36, endLat: 39.92, endLon: 116.44);
        await CreateReceptor(lat: 39.7, lon: 116.8);

        var resp = await _client.PostJsonAsync("/api/simulation/run", new SimulationRequestDto
        {
            MeteorologyId = met.Id,
            GridResolution = 100,
            DomainSize = 10_000,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await resp.ReadJsonAsync<SimulationResultDto>();
        AssertNorthWindKeepsSourceAtPlumeStart(result, (39.88 + 39.92) / 2, (116.36 + 116.44) / 2);
        AxisCenter(result.GridLon).Should().BeApproximately((116.36 + 116.44) / 2, 1e-9);
    }

    [Fact]
    public async Task 单风向模拟_可临时覆盖风速风向()
    {
        var met = await CreateMet(ws: 3.0, wd: 0.0);
        await CreatePointSource(lat: 39.9, lon: 116.4, pm25Rate: 1.0);
        await CreateReceptor(lat: 39.89, lon: 116.4);

        var baseResp = await _client.PostJsonAsync("/api/simulation/run", new SimulationRequestDto
        {
            MeteorologyId = met.Id,
            GridResolution = 100,
            DomainSize = 5000,
        });
        var overrideResp = await _client.PostJsonAsync("/api/simulation/run", new SimulationRequestDto
        {
            MeteorologyId = met.Id,
            WindSpeed = 6.0,
            WindDirection = 180.0,
            GridResolution = 100,
            DomainSize = 5000,
        });

        baseResp.StatusCode.Should().Be(HttpStatusCode.OK);
        overrideResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var baseResult = await baseResp.ReadJsonAsync<SimulationResultDto>();
        var overrideResult = await overrideResp.ReadJsonAsync<SimulationResultDto>();

        overrideResult.Concentrations.SelectMany(row => row).Max()
            .Should().NotBeApproximately(baseResult.Concentrations.SelectMany(row => row).Max(), 1e-12);
    }

    [Fact]
    public async Task 单风向模拟_模拟高度进入浓度场计算()
    {
        var met = await CreateMet(ws: 3.0, wd: 0.0);
        await CreatePointSource(lat: 39.9, lon: 116.4, h: 35, pm25Rate: 1.0);
        await CreateReceptor(lat: 39.89, lon: 116.4);

        var groundResp = await _client.PostJsonAsync("/api/simulation/run", new SimulationRequestDto
        {
            MeteorologyId = met.Id,
            GridResolution = 100,
            DomainSize = 5000,
            ReceptorHeight = 0,
        });
        var elevatedResp = await _client.PostJsonAsync("/api/simulation/run", new SimulationRequestDto
        {
            MeteorologyId = met.Id,
            GridResolution = 100,
            DomainSize = 5000,
            ReceptorHeight = 20,
        });

        groundResp.StatusCode.Should().Be(HttpStatusCode.OK);
        elevatedResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var ground = await groundResp.ReadJsonAsync<SimulationResultDto>();
        var elevated = await elevatedResp.ReadJsonAsync<SimulationResultDto>();

        elevated.Concentrations.SelectMany(row => row).Max()
            .Should().NotBeApproximately(ground.Concentrations.SelectMany(row => row).Max(), 1e-12);
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

    [Fact]
    public async Task 网格范围_以污染源为起点且不小于用户模拟范围()
    {
        var met = await CreateMet();
        await CreatePointSource(lat: 39.900, lon: 116.400);
        await CreateReceptor(lat: 39.902, lon: 116.402);

        var resp = await _client.PostJsonAsync("/api/simulation/run", new SimulationRequestDto
        {
            MeteorologyId = met.Id,
            GridResolution = 100,
            DomainSize = 50_000,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await resp.ReadJsonAsync<SimulationResultDto>();
        var latRangeMeters = (result.GridLat.Max() - result.GridLat.Min()) * 111_000;
        var lonRangeMeters = (result.GridLon.Max() - result.GridLon.Min()) * 111_000
            * Math.Cos(result.GridLat.Average() * Math.PI / 180.0);

        AssertNorthWindKeepsSourceAtPlumeStart(result, 39.900, 116.400);
        latRangeMeters.Should().BeGreaterThan(49_000);
        lonRangeMeters.Should().BeGreaterThan(49_000);
        latRangeMeters.Should().BeLessThan(51_000);
        lonRangeMeters.Should().BeLessThan(51_000);
    }
}
