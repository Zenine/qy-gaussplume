using System.Net;
using FluentAssertions;
using GnnSimulation.Api.Dtos;
using GnnSimulation.Tests.Infrastructure;

namespace GnnSimulation.Tests.Api;

public class SourcesControllerTests : IDisposable
{
    private readonly GnnWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public SourcesControllerTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task GET_空列表_返回200与空数组()
    {
        var resp = await _client.GetAsync("/api/sources");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await resp.ReadJsonAsync<List<EmissionSourceDto>>();
        list.Should().BeEmpty();
    }

    [Fact]
    public async Task POST_创建点源含污染物_返回完整信息()
    {
        var payload = new EmissionSourceCreateDto
        {
            Name = "钢厂烟囱",
            SourceType = "point",
            Latitude = 39.9,
            Longitude = 116.4,
            Height = 60,
            Temperature = 420,
            Pollutants =
            {
                new PollutantEmissionCreateDto("PM2.5", 1.5),
                new PollutantEmissionCreateDto("NOx", 3.0),
            },
        };

        var resp = await _client.PostJsonAsync("/api/sources", payload);
        resp.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await resp.ReadJsonAsync<EmissionSourceDto>();
        created.Id.Should().BeGreaterThan(0);
        created.Name.Should().Be("钢厂烟囱");
        created.Temperature.Should().Be(420);
        created.Pollutants.Should().HaveCount(2);
        created.Pollutants.Select(p => p.PollutantType).Should().BeEquivalentTo(new[] { "PM2.5", "NOx" });
        created.Pollutants.All(p => p.SourceId == created.Id).Should().BeTrue();
    }

    [Fact]
    public async Task GET_by_id_命中返回200_未命中返回404()
    {
        var create = await _client.PostJsonAsync("/api/sources", new EmissionSourceCreateDto
        {
            Name = "A", Latitude = 0, Longitude = 0, Height = 0,
        });
        var created = await create.ReadJsonAsync<EmissionSourceDto>();

        var hit = await _client.GetAsync($"/api/sources/{created.Id}");
        hit.StatusCode.Should().Be(HttpStatusCode.OK);

        var miss = await _client.GetAsync("/api/sources/999999");
        miss.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PUT_部分更新_只动传入字段_其余保持()
    {
        var create = await _client.PostJsonAsync("/api/sources", new EmissionSourceCreateDto
        {
            Name = "原始",
            Latitude = 39.9,
            Longitude = 116.4,
            Height = 50,
            Temperature = 400,
        });
        var created = await create.ReadJsonAsync<EmissionSourceDto>();

        var put = await _client.PutJsonAsync($"/api/sources/{created.Id}", new EmissionSourceUpdateDto
        {
            Name = "改名", // 只改名
        });
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await put.ReadJsonAsync<EmissionSourceDto>();
        updated.Name.Should().Be("改名");
        updated.Height.Should().Be(50);
        updated.Temperature.Should().Be(400);
    }

    [Fact]
    public async Task PUT_污染物列表非null_整体替换()
    {
        var create = await _client.PostJsonAsync("/api/sources", new EmissionSourceCreateDto
        {
            Name = "源",
            Latitude = 0,
            Longitude = 0,
            Height = 0,
            Pollutants =
            {
                new PollutantEmissionCreateDto("PM2.5", 1.0),
                new PollutantEmissionCreateDto("NOx", 2.0),
            },
        });
        var created = await create.ReadJsonAsync<EmissionSourceDto>();

        var put = await _client.PutJsonAsync($"/api/sources/{created.Id}", new EmissionSourceUpdateDto
        {
            Pollutants = new List<PollutantEmissionCreateDto>
            {
                new("O3", 0.5),
            },
        });
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await put.ReadJsonAsync<EmissionSourceDto>();
        updated.Pollutants.Should().HaveCount(1);
        updated.Pollutants[0].PollutantType.Should().Be("O3");
    }

    [Fact]
    public async Task DELETE_成功删除_再次GET得404()
    {
        var create = await _client.PostJsonAsync("/api/sources", new EmissionSourceCreateDto
        {
            Name = "待删", Latitude = 0, Longitude = 0, Height = 0,
        });
        var created = await create.ReadJsonAsync<EmissionSourceDto>();

        var del = await _client.DeleteAsync($"/api/sources/{created.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.OK);

        var hit = await _client.GetAsync($"/api/sources/{created.Id}");
        hit.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task POST_batch_批量创建()
    {
        var payload = new List<EmissionSourceCreateDto>
        {
            new() { Name = "s1", Latitude = 0, Longitude = 0, Height = 0 },
            new() { Name = "s2", Latitude = 0, Longitude = 0, Height = 0 },
            new() { Name = "s3", Latitude = 0, Longitude = 0, Height = 0 },
        };
        var resp = await _client.PostJsonAsync("/api/sources/batch", payload);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await resp.ReadJsonAsync<List<EmissionSourceDto>>();
        list.Should().HaveCount(3);
        list.All(x => x.Id > 0).Should().BeTrue();
    }

    [Fact]
    public async Task GET_pollutant_types_返回6种()
    {
        var resp = await _client.GetAsync("/api/sources/pollutant-types");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await resp.ReadJsonAsync<List<PollutantTypeInfoDto>>();
        list.Should().HaveCount(6);
        list.Select(x => x.Type).Should().Contain(new[] { "PM2.5", "PM10", "TSP", "VOCs", "NOx", "O3" });
    }

    [Fact]
    public async Task GET_marker_symbols_返回12种()
    {
        var resp = await _client.GetAsync("/api/sources/marker-symbols");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await resp.ReadJsonAsync<List<MarkerSymbolInfoDto>>();
        list.Should().HaveCount(12);
    }

    [Fact]
    public async Task 子资源_添加同类型污染物_应覆盖而非重复()
    {
        var create = await _client.PostJsonAsync("/api/sources", new EmissionSourceCreateDto
        {
            Name = "S", Latitude = 0, Longitude = 0, Height = 0,
        });
        var created = await create.ReadJsonAsync<EmissionSourceDto>();

        await _client.PostJsonAsync(
            $"/api/sources/{created.Id}/pollutants",
            new PollutantEmissionCreateDto("PM2.5", 1.0));
        var second = await _client.PostJsonAsync(
            $"/api/sources/{created.Id}/pollutants",
            new PollutantEmissionCreateDto("PM2.5", 9.9));
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        var get = await _client.GetAsync($"/api/sources/{created.Id}");
        var dto = await get.ReadJsonAsync<EmissionSourceDto>();
        dto.Pollutants.Should().HaveCount(1);
        dto.Pollutants[0].EmissionRate.Should().Be(9.9);
    }

    [Fact]
    public async Task 子资源_未知污染物类型_返回400()
    {
        var create = await _client.PostJsonAsync("/api/sources", new EmissionSourceCreateDto
        {
            Name = "S", Latitude = 0, Longitude = 0, Height = 0,
        });
        var created = await create.ReadJsonAsync<EmissionSourceDto>();

        var resp = await _client.PostJsonAsync(
            $"/api/sources/{created.Id}/pollutants",
            new PollutantEmissionCreateDto("二氧化硅", 1.0));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task 子资源_删除污染物()
    {
        var create = await _client.PostJsonAsync("/api/sources", new EmissionSourceCreateDto
        {
            Name = "S",
            Latitude = 0,
            Longitude = 0,
            Height = 0,
            Pollutants = { new PollutantEmissionCreateDto("NOx", 1.0) },
        });
        var created = await create.ReadJsonAsync<EmissionSourceDto>();
        var pid = created.Pollutants[0].Id;

        var del = await _client.DeleteAsync($"/api/sources/{created.Id}/pollutants/{pid}");
        del.StatusCode.Should().Be(HttpStatusCode.OK);

        var get = await _client.GetAsync($"/api/sources/{created.Id}");
        var dto = await get.ReadJsonAsync<EmissionSourceDto>();
        dto.Pollutants.Should().BeEmpty();
    }
}
