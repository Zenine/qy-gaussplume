# QY-GaussPlume 后端

清源高斯烟羽扩散模拟平台的 .NET 9 后端。支持四类源（点/面/等效面/线）+ 六种污染物，提供单风向与多风向并行两种模拟入口。

## qy 项目架构图

![qy 项目架构图](../docs/assets/generated/qy-architecture.png)

架构图展示当前 qy 后端与 GNN 前端的协作关系：前端通过 HTTP/JSON 调用 API 服务，API 层组织模拟请求、参数校验和结果返回，Core 层负责高斯烟羽模型计算，Data 层管理 SQLite 数据，地图瓦片和坐标转换服务用于前端渲染，`scripts/verify.sh` 串联后端测试、前端测试和构建验证。

## 项目结构

```
backend-dotnet/
├── GnnSimulation.sln
├── GnnSimulation.Api/              # Web API 入口层
│   ├── Controllers/                # 所有 /api/* 端点
│   ├── Dtos/                       # 请求/响应 DTO（camelCase JSON）
│   ├── Mapping/                    # Entity ↔ DTO 手写映射
│   ├── Services/                   # SimulationService、ParallelSimulationService、
│   │                                 WindDirectionWorker、GridBuilder、ShapefileService、ExcelService
│   ├── appsettings.json            # 连接串、Shapefile 路径
│   └── Program.cs                  # DI、CORS、启动自愈
├── GnnSimulation.Core/             # 无依赖核心算法库
│   └── Atmosphere/
│       ├── GaussianPlumeModel.cs           # 高斯烟羽方程 + 四源派发
│       ├── PasquillGifford.cs              # A-F 稳定度 σ 参数
│       ├── PollutantProperties.cs          # 八种污染物沉降/化学参数
│       ├── StabilityClassifier.cs          # 风速+辐照分类
│       └── ContributionAnalysis.cs         # 排名
├── GnnSimulation.Data/             # EF Core 持久化
│   ├── Entities/                   # 5 张表：EmissionSource / PollutantEmission /
│   │                                 Receptor / Meteorology / MarkerConfig
│   ├── GnnDbContext.cs             # Fluent snake_case 映射 + 自动时间戳 + 级联删除
│   ├── Migrations/                 # 初始 Migration（新库用）
│   └── Design/DesignTimeDbContextFactory.cs
└── GnnSimulation.Tests/            # xUnit 测试（137 用例）
    ├── Core/                       # 算法单测 + 黄金值对齐
    ├── Data/                       # 实体 + DbContext
    ├── Api/                        # WebApplicationFactory 集成测试
    ├── Infrastructure/             # SqliteInMemoryFixture 等共享
    └── Data/golden/
        ├── generate_golden.py      # 用 Python 原版跑出的黄金值生成器
        └── golden_values.json      # 72 个黄金场景
```

## 快速运行

```bash
# 从仓库根开始
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"
cd backend-dotnet

dotnet restore                                   # 首次
dotnet build                                     # 可选
dotnet test                                      # 跑全部 137 个测试
dotnet run --project GnnSimulation.Api           # 启动 API @ http://localhost:5207
```

Swagger OpenAPI: <http://localhost:5207/openapi/v1.json>

## 配置

`GnnSimulation.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Default": "Data Source=../../backend/air_pollution.db"
  },
  "Shapefile": {
    "Path": "../../shp/县（等积投影）.shp",
    "LoadByDefault": false
  }
}
```

- 默认使用仓库内匿名演示库 `backend/air_pollution.db`
- Shapefile `LoadByDefault: false`，避免 60 MB → 100+ MB GeoJSON 的爆炸响应；`/api/map/geojson?force=true` 可临时强制加载

## 测试矩阵

| 测试类别 | 文件 | 数量 |
|---|---|---|
| **Data** | `DbContextShapeTests`、`EmissionSourceTests`、`PollutantEmissionTests`、`ReceptorTests`、`MeteorologyTests` | 28 |
| **Core** | `GaussianPlumeModelTests`（物理性质）、`StabilityClassifierTests`、`GoldenValueTests`（JSON 黄金值逐场景对齐） | 45 |
| **Api** | `SourcesControllerTests`、`ReceptorsControllerTests`、`MeteorologyControllerTests`、`ConfigControllerTests`、`SimulationControllerTests`、`SimulationConsistencyTests`、`ParallelSimulationTests`、`MapControllerTests`、`ShapefileServiceTests`、`ExcelIoTests` | 64 |
| **合计** | | **137** |

## 黄金值对齐

Core 层算法在 IEEE754 双精度下**按 1e-9 误差对齐**。.NET 测试读取 `golden_values.json` 逐项比对。

```bash
# 重新生成黄金值（需本地有 Python 3 + numpy）
cd GnnSimulation.Tests/Data/golden
python3 generate_golden.py    # 产出 golden_values.json
```

JSON 会被 csproj 的 `<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>` 拷到测试 bin 目录。

## API 端点速览

| 资源 | 端点 |
|---|---|
| 排放源 | `GET/POST/PUT/DELETE /api/sources` + `/batch` + `/pollutant-types` + `/marker-symbols` + `/{id}/pollutants` + `/template/{type}` + `/import/{type}` |
| 受体点 | `GET/POST/PUT/DELETE /api/receptors` + `/batch` + `/template` + `/import` + `/export` |
| 气象场 | `GET/POST/PUT/DELETE /api/meteorology` + `/batch` |
| 标记配置 | `GET /api/config` + `GET/POST/PUT /api/config/{type}` |
| 模拟 | `POST /api/simulation/run` · `POST /api/simulation/run_parallel` |
| 地图 | `GET /api/map/geojson` · `/bounds` · `/info` |

详见 [../docs/API.md](../docs/API.md)。

## 关键技术决策与陷阱

### EF Core `HasDefaultValue` 陷阱

EF 对 `HasDefaultValue(x)` 的 CLR 值 == `x` 的属性**跳过 INSERT**，让 DB 层 DEFAULT 生效。但原 Python SQLite 表**只有时间戳列**有 DB-level DEFAULT，结果其他列被写 NULL，读回崩。

**修复**：全部清理 `HasDefaultValue(...)`，保留 `HasDefaultValueSql("CURRENT_TIMESTAMP")`。依赖 C# 实体类的 CLR 默认：

```csharp
// 实体
public string SourceType { get; set; } = "point";  // CLR 默认生效

// Fluent（不再用 HasDefaultValue）
e.Property(x => x.SourceType).HasColumnName("source_type").HasMaxLength(20);
```

### 老 DB 历史 NULL 兼容

`Program.cs` 启动时执行一次 `UPDATE receptors SET is_active = 1 WHERE is_active IS NULL`（及 `emission_sources`），避免历史数据 NULL 导致 `GetBoolean` 崩溃。测试环境（`ASPNETCORE_ENVIRONMENT=Testing`）跳过。

### ID 过滤语义

`SimulationRequestDto.SourceIds` / `ReceptorIds` 使用明确的三态语义：

- `null`：未指定过滤条件，加载所有激活数据。
- `[]`：调用方明确选择空范围，返回空集合。
- `[1, 2, ...]`：只加载指定 ID。

这用于支持前端地图矩形框选，避免区域内无受体点时回退到全部受体点。

### 并行模拟：线程替代进程

Python 版用 `ProcessPoolExecutor`（GIL 限制下必须），.NET 无 GIL，直接用 `Parallel.ForEach` + `MaxDegreeOfParallelism`，零拷贝共享网格数据。

### Shapefile 坐标转换

原数据为 **Krasovsky 1940 Albers 等积投影**（中国特殊 CRS）。运行时解析 `.prj` + ProjNet 建变换，**SkipInvalidShapes** 跳过残缺几何，`CreateClosedRing` 防浮点漂移导致 LinearRing 不闭合。

## 迁移

新环境首次部署可用 EF Core Migrations 从零建库：

```bash
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"
dotnet ef database update --project GnnSimulation.Data --startup-project GnnSimulation.Data
```

## 依赖包

| 包 | 用途 |
|---|---|
| `Microsoft.EntityFrameworkCore.Sqlite` 9.0 | ORM + SQLite |
| `NetTopologySuite.IO.Esri.Shapefile` 1.2 | SHP 读取 |
| `NetTopologySuite.IO.GeoJSON` 4.0 | GeoJSON 序列化 |
| `ProjNET` 2.1 | Albers → WGS84 坐标变换 |
| `ClosedXML` 0.104 | Excel 读写 |
| **测试** | `xunit` · `FluentAssertions` · `Microsoft.AspNetCore.Mvc.Testing` |

## 维护者

**Zenine Xu** · <zeninexu@gmail.com>

日志位于 `.run/backend.log`（`scripts/start.sh` 启动时输出）。HTTP 请求日志默认 `Information` 级别，见 `appsettings.json`。
