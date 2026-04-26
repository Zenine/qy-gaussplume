using GnnSimulation.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GnnSimulation.Data;

public class GnnDbContext : DbContext
{
    public GnnDbContext(DbContextOptions<GnnDbContext> options) : base(options) { }

    public DbSet<EmissionSource> EmissionSources => Set<EmissionSource>();
    public DbSet<PollutantEmission> PollutantEmissions => Set<PollutantEmission>();
    public DbSet<Receptor> Receptors => Set<Receptor>();
    public DbSet<Meteorology> Meteorology => Set<Meteorology>();
    public DbSet<MarkerConfig> MarkerConfigs => Set<MarkerConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureEmissionSource(modelBuilder);
        ConfigurePollutantEmission(modelBuilder);
        ConfigureReceptor(modelBuilder);
        ConfigureMeteorology(modelBuilder);
        ConfigureMarkerConfig(modelBuilder);
    }

    public override int SaveChanges()
    {
        StampTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void StampTimestamps()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<EntityBase>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Property(nameof(EntityBase.CreatedAt)).IsModified = false;
            }
        }
    }

    private static void ConfigureEmissionSource(ModelBuilder b)
    {
        var e = b.Entity<EmissionSource>();
        e.ToTable("emission_sources");
        e.HasKey(x => x.Id);
        e.HasIndex(x => x.Id).HasDatabaseName("ix_emission_sources_id");
        e.Property(x => x.Id).HasColumnName("id");
        // 不用 HasDefaultValue：原生 SQLite schema 无 DB 默认；CLR 默认会被 EF 跳过 INSERT 导致写入 NULL
        e.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        e.Property(x => x.SourceType).HasColumnName("source_type").HasMaxLength(20);
        e.Property(x => x.Latitude).HasColumnName("latitude").IsRequired();
        e.Property(x => x.Longitude).HasColumnName("longitude").IsRequired();
        e.Property(x => x.Height).HasColumnName("height").IsRequired();

        e.Property(x => x.Temperature).HasColumnName("temperature");
        e.Property(x => x.Velocity).HasColumnName("velocity");
        e.Property(x => x.Diameter).HasColumnName("diameter");

        e.Property(x => x.AreaShape).HasColumnName("area_shape").HasMaxLength(20);
        e.Property(x => x.AreaLength).HasColumnName("area_length");
        e.Property(x => x.AreaWidth).HasColumnName("area_width");
        e.Property(x => x.AreaHeight).HasColumnName("area_height");
        e.Property(x => x.AreaTemperature).HasColumnName("area_temperature");
        e.Property(x => x.SigmaZ0Area).HasColumnName("sigma_z0_area");

        e.Property(x => x.LineType).HasColumnName("line_type").HasMaxLength(20);
        e.Property(x => x.StartLon).HasColumnName("start_lon");
        e.Property(x => x.StartLat).HasColumnName("start_lat");
        e.Property(x => x.EndLon).HasColumnName("end_lon");
        e.Property(x => x.EndLat).HasColumnName("end_lat");
        e.Property(x => x.LineWidth).HasColumnName("line_width");
        e.Property(x => x.LineHeight).HasColumnName("line_height");
        e.Property(x => x.LineTemperature).HasColumnName("line_temperature");
        e.Property(x => x.SigmaZ0Line).HasColumnName("sigma_z0_line");
        e.Property(x => x.LineSegmentLength).HasColumnName("line_segment_length");

        e.Property(x => x.MarkerSymbol).HasColumnName("marker_symbol").HasMaxLength(50);
        e.Property(x => x.MarkerColor).HasColumnName("marker_color").HasMaxLength(20);

        e.Property(x => x.IsActive).HasColumnName("is_active");
        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

        e.HasMany(x => x.Pollutants)
            .WithOne(x => x.Source)
            .HasForeignKey(x => x.SourceId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigurePollutantEmission(ModelBuilder b)
    {
        var e = b.Entity<PollutantEmission>();
        e.ToTable("pollutant_emissions");
        e.HasKey(x => x.Id);
        e.HasIndex(x => x.Id).HasDatabaseName("ix_pollutant_emissions_id");
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.SourceId).HasColumnName("source_id").IsRequired();
        e.Property(x => x.PollutantType).HasColumnName("pollutant_type").HasMaxLength(50).IsRequired();
        // 同 Meteorology：避免 CLR 默认被 EF 跳过
        e.Property(x => x.EmissionRate).HasColumnName("emission_rate").IsRequired();
        e.Property(x => x.Concentration).HasColumnName("concentration");
        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
    }

    private static void ConfigureReceptor(ModelBuilder b)
    {
        var e = b.Entity<Receptor>();
        e.ToTable("receptors");
        e.HasKey(x => x.Id);
        e.HasIndex(x => x.Id).HasDatabaseName("ix_receptors_id");
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        e.Property(x => x.Latitude).HasColumnName("latitude").IsRequired();
        e.Property(x => x.Longitude).HasColumnName("longitude").IsRequired();
        e.Property(x => x.Height).HasColumnName("height").IsRequired();
        e.Property(x => x.MarkerSymbol).HasColumnName("marker_symbol").HasMaxLength(50);
        e.Property(x => x.MarkerColor).HasColumnName("marker_color").HasMaxLength(20);
        e.Property(x => x.IsActive).HasColumnName("is_active");
        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
    }

    private static void ConfigureMeteorology(ModelBuilder b)
    {
        var e = b.Entity<Meteorology>();
        e.ToTable("meteorology");
        e.HasKey(x => x.Id);
        e.HasIndex(x => x.Id).HasDatabaseName("ix_meteorology_id");
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        // 不用 HasDefaultValue：原生 SQLite schema 无 DB 默认；双精度 0.0 被 EF 视作 CLR 默认会跳过 INSERT
        e.Property(x => x.WindSpeed).HasColumnName("wind_speed").IsRequired();
        e.Property(x => x.WindDirection).HasColumnName("wind_direction").IsRequired();
        e.Property(x => x.BoundaryLayerHeight).HasColumnName("boundary_layer_height");
        e.Property(x => x.StabilityClass).HasColumnName("stability_class").HasMaxLength(1);
        e.Property(x => x.Temperature).HasColumnName("temperature");
        e.Property(x => x.Humidity).HasColumnName("humidity");
        e.Property(x => x.CloudCover).HasColumnName("cloud_cover");
        e.Property(x => x.Precipitation).HasColumnName("precipitation");
        e.Property(x => x.RecordTime).HasColumnName("record_time").HasDefaultValueSql("CURRENT_TIMESTAMP");
        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
    }

    private static void ConfigureMarkerConfig(ModelBuilder b)
    {
        var e = b.Entity<MarkerConfig>();
        e.ToTable("marker_configs");
        e.HasKey(x => x.Id);
        e.HasIndex(x => x.Id).HasDatabaseName("ix_marker_configs_id");
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.Type).HasColumnName("type").HasMaxLength(20).IsRequired();
        e.Property(x => x.Symbol).HasColumnName("symbol").HasMaxLength(50);
        e.Property(x => x.Color).HasColumnName("color").HasMaxLength(20);
        e.Property(x => x.Size).HasColumnName("size");
        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}
