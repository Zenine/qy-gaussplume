using GnnSimulation.Api.Services;
using GnnSimulation.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// 允许前端 (Vite dev server) 跨域访问
builder.Services.AddCors(options =>
{
    options.AddPolicy("GnnCors", policy =>
        policy.WithOrigins(
                "http://localhost:5173",
                "http://localhost:4173",
                "http://localhost:5174",
                "http://localhost:4174",
                "http://localhost:5207",
                "http://127.0.0.1:5174",
                "http://127.0.0.1:4174",
                "http://127.0.0.1:5207")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

// EF Core + SQLite
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=air_pollution.db";
builder.Services.AddDbContext<GnnDbContext>(options =>
    options.UseSqlite(connectionString));

// Simulation orchestration
builder.Services.AddScoped<GnnSimulation.Api.Services.SimulationService>();
builder.Services.AddScoped<GnnSimulation.Api.Services.ParallelSimulationService>();

// Shapefile: 单例共享缓存
builder.Services.AddSingleton<GnnSimulation.Api.Services.ShapefileService>();

var app = builder.Build();

// 启动自愈：把历史数据库里 is_active = NULL 的行修为 1，避免非空 bool 读取崩溃。
// 部分旧数据只有应用层默认值，没有数据库层 NOT NULL 约束。
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<GnnDbContext>();
    try
    {
        var meteorologyHasIsActive = db.Database
            .SqlQueryRaw<string>("SELECT name AS Value FROM pragma_table_info('meteorology') WHERE name = 'is_active'")
            .Any();
        if (!meteorologyHasIsActive)
            db.Database.ExecuteSqlRaw("ALTER TABLE meteorology ADD COLUMN is_active INTEGER NOT NULL DEFAULT 1");

        db.Database.ExecuteSqlRaw("UPDATE receptors SET is_active = 1 WHERE is_active IS NULL");
        db.Database.ExecuteSqlRaw("UPDATE emission_sources SET is_active = 1 WHERE is_active IS NULL");
        db.Database.ExecuteSqlRaw("UPDATE meteorology SET is_active = 1 WHERE is_active IS NULL");
        db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS regions (id INTEGER NOT NULL CONSTRAINT pk_regions PRIMARY KEY AUTOINCREMENT, key TEXT NOT NULL, name TEXT NOT NULL, sort_order INTEGER NOT NULL, created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP)");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS ix_regions_key ON regions (key)");
        db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS region_sources (region_id INTEGER NOT NULL, source_id INTEGER NOT NULL, CONSTRAINT pk_region_sources PRIMARY KEY (region_id, source_id), CONSTRAINT fk_region_sources_regions_region_id FOREIGN KEY (region_id) REFERENCES regions (id) ON DELETE CASCADE, CONSTRAINT fk_region_sources_emission_sources_source_id FOREIGN KEY (source_id) REFERENCES emission_sources (id) ON DELETE CASCADE)");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS ix_region_sources_source_id ON region_sources (source_id)");
        db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS region_receptors (region_id INTEGER NOT NULL, receptor_id INTEGER NOT NULL, CONSTRAINT pk_region_receptors PRIMARY KEY (region_id, receptor_id), CONSTRAINT fk_region_receptors_regions_region_id FOREIGN KEY (region_id) REFERENCES regions (id) ON DELETE CASCADE, CONSTRAINT fk_region_receptors_receptors_receptor_id FOREIGN KEY (receptor_id) REFERENCES receptors (id) ON DELETE CASCADE)");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS ix_region_receptors_receptor_id ON region_receptors (receptor_id)");
        db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS region_meteorology (region_id INTEGER NOT NULL, meteorology_id INTEGER NOT NULL, CONSTRAINT pk_region_meteorology PRIMARY KEY (region_id, meteorology_id), CONSTRAINT fk_region_meteorology_regions_region_id FOREIGN KEY (region_id) REFERENCES regions (id) ON DELETE CASCADE, CONSTRAINT fk_region_meteorology_meteorology_meteorology_id FOREIGN KEY (meteorology_id) REFERENCES meteorology (id) ON DELETE CASCADE)");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS ix_region_meteorology_meteorology_id ON region_meteorology (meteorology_id)");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "启动时历史库自愈失败（非致命）");
    }
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GnnDbContext>();
    await RegionCatalog.EnsureSeededAsync(db);
    await RegionCatalog.BackfillDefaultRegionAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("GnnCors");
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program { }
