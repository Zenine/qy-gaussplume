using GnnSimulation.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// 允许前端 (Vite dev server) 跨域访问
builder.Services.AddCors(options =>
{
    options.AddPolicy("GnnCors", policy =>
        policy.WithOrigins("http://localhost:5173", "http://localhost:4173")
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
        db.Database.ExecuteSqlRaw("UPDATE receptors SET is_active = 1 WHERE is_active IS NULL");
        db.Database.ExecuteSqlRaw("UPDATE emission_sources SET is_active = 1 WHERE is_active IS NULL");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "启动时 is_active 自愈失败（非致命）");
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("GnnCors");
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
