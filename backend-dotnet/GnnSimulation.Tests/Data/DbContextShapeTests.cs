using FluentAssertions;
using GnnSimulation.Data;
using GnnSimulation.Data.Entities;
using GnnSimulation.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GnnSimulation.Tests.Data;

// 这些测试验证模型与 Python SQLAlchemy 原版的表结构一致性
public class DbContextShapeTests
{
    private static GnnDbContext BuildContext()
    {
        var fixture = new SqliteInMemoryFixture();
        return fixture.CreateContext();
    }

    [Theory]
    [InlineData(typeof(EmissionSource), "emission_sources")]
    [InlineData(typeof(PollutantEmission), "pollutant_emissions")]
    [InlineData(typeof(Receptor), "receptors")]
    [InlineData(typeof(Meteorology), "meteorology")]
    [InlineData(typeof(MarkerConfig), "marker_configs")]
    public void 实体应映射到预期的snake_case表名(Type entityType, string expectedTable)
    {
        using var ctx = BuildContext();
        var mapping = ctx.Model.FindEntityType(entityType)!;
        mapping.GetTableName().Should().Be(expectedTable);
    }

    [Fact]
    public void EmissionSource所有列应为snake_case()
    {
        using var ctx = BuildContext();
        var storeObject = StoreObjectIdentifier.Table("emission_sources");
        var columns = ctx.Model
            .FindEntityType(typeof(EmissionSource))!
            .GetProperties()
            .Select(p => p.GetColumnName(storeObject))
            .ToList();

        columns.Should().Contain(new[]
        {
            "id", "name", "source_type", "latitude", "longitude", "height",
            "temperature", "velocity", "diameter",
            "area_shape", "area_length", "area_width", "area_height", "area_temperature", "sigma_z0_area",
            "line_type", "start_lon", "start_lat", "end_lon", "end_lat",
            "line_width", "line_height", "line_temperature", "sigma_z0_line", "line_segment_length",
            "marker_symbol", "marker_color", "is_active", "created_at", "updated_at",
        });
    }

    [Fact]
    public void PollutantEmission外键应指向EmissionSource主键()
    {
        using var ctx = BuildContext();
        var fk = ctx.Model
            .FindEntityType(typeof(PollutantEmission))!
            .GetForeignKeys()
            .Single();
        fk.PrincipalEntityType.ClrType.Should().Be(typeof(EmissionSource));
        fk.Properties.Single().Name.Should().Be(nameof(PollutantEmission.SourceId));
        fk.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
    }

    [Fact]
    public void 所有实体应有5张表()
    {
        using var ctx = BuildContext();
        var tables = ctx.Model.GetEntityTypes().Select(t => t.GetTableName()).Distinct().ToList();
        tables.Should().HaveCount(5);
    }
}
