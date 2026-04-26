using System.Net;
using FluentAssertions;
using GnnSimulation.Api.Dtos;
using GnnSimulation.Tests.Infrastructure;

namespace GnnSimulation.Tests.Api;

public class MeteorologyControllerTests : IDisposable
{
    private readonly GnnWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public MeteorologyControllerTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task POST_创建气象场_默认值正确()
    {
        var resp = await _client.PostJsonAsync("/api/meteorology", new MeteorologyCreateDto { Name = "冬季北风" });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);

        var dto = await resp.ReadJsonAsync<MeteorologyDto>();
        dto.WindSpeed.Should().Be(2.0);
        dto.WindDirection.Should().Be(0.0);
        dto.StabilityClass.Should().Be("D");
        dto.BoundaryLayerHeight.Should().Be(1000.0);
        dto.Temperature.Should().Be(293.15);
    }

    [Fact]
    public async Task PUT_风速风向独立更新()
    {
        var create = await _client.PostJsonAsync("/api/meteorology", new MeteorologyCreateDto
        {
            Name = "基础", WindSpeed = 3.0, WindDirection = 90,
        });
        var dto = await create.ReadJsonAsync<MeteorologyDto>();

        var put = await _client.PutJsonAsync($"/api/meteorology/{dto.Id}", new MeteorologyUpdateDto
        {
            WindSpeed = 5.5, // 只改风速
        });
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await put.ReadJsonAsync<MeteorologyDto>();
        updated.WindSpeed.Should().Be(5.5);
        updated.WindDirection.Should().Be(90);
    }

    [Fact]
    public async Task DELETE_不存在_返回404()
    {
        var resp = await _client.DeleteAsync("/api/meteorology/99999");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("F")]
    public async Task 稳定度类别_端到端持久化(string stab)
    {
        var resp = await _client.PostJsonAsync("/api/meteorology", new MeteorologyCreateDto
        {
            Name = $"Stab-{stab}", StabilityClass = stab,
        });
        var dto = await resp.ReadJsonAsync<MeteorologyDto>();
        dto.StabilityClass.Should().Be(stab);
    }

    [Fact]
    public async Task POST_batch_返回id列表()
    {
        var payload = new List<MeteorologyCreateDto>
        {
            new() { Name = "春", WindSpeed = 2.5, WindDirection = 45 },
            new() { Name = "夏", WindSpeed = 1.5, WindDirection = 180 },
        };
        var resp = await _client.PostJsonAsync("/api/meteorology/batch", payload);
        var list = await resp.ReadJsonAsync<List<MeteorologyDto>>();
        list.Should().HaveCount(2);
        list.All(x => x.Id > 0).Should().BeTrue();
    }
}
