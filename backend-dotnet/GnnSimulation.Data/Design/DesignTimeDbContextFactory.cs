using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GnnSimulation.Data.Design;

// 仅供 dotnet-ef 工具在设计时（迁移生成等）使用
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<GnnDbContext>
{
    public GnnDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<GnnDbContext>()
            .UseSqlite("Data Source=air_pollution.db")
            .Options;
        return new GnnDbContext(options);
    }
}
