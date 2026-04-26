using GnnSimulation.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GnnSimulation.Tests.Infrastructure;

// 每个测试方法通过这个工厂创建一个独立的 in-memory SQLite 实例，
// 避免并发测试互相污染，同时使用真实 SQLite 引擎保证约束/默认值行为与生产一致。
public sealed class SqliteInMemoryFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<GnnDbContext> _options;

    public SqliteInMemoryFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<GnnDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var ctx = new GnnDbContext(_options);
        ctx.Database.EnsureCreated();
    }

    public GnnDbContext CreateContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
