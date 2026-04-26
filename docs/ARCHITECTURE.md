# 架构与演进

## 总体架构

```
                       ┌─────────────────────────────────┐
                       │   用户浏览器                      │
                       └──────────────┬──────────────────┘
                                      │
                                      ▼
    ┌──────────────────────────────────────────────────────────┐
    │   frontend-vue (Vite dev :5173 / 生产静态资源)            │
    │                                                          │
    │  Vue 3 SFC ──────────── Pinia (持久化到 localStorage)    │
    │      │                       │                           │
    │      ▼                       ▼                           │
    │  Element Plus UI      axios + TS 类型 (DTO 对齐)          │
    │      │                       │                           │
    │      ▼                       ▼                           │
    │  Leaflet + 高德瓦片 ────► Canvas 热力图 (双线性插值)      │
    │      │                                                   │
    │      └─► WGS84 ↔ GCJ02 转换                              │
    └──────────────────┬───────────────────────────────────────┘
                       │ /api/* (Vite proxy → dev; Ingress → prod)
                       ▼
    ┌──────────────────────────────────────────────────────────┐
    │   backend-dotnet (ASP.NET Core :5207)                    │
    │                                                          │
    │   GnnSimulation.Api ── Controllers (7 个)                 │
    │         │                                                │
    │         ▼                                                │
    │   Services:                                              │
    │     SimulationService  ───► GridBuilder                  │
    │     ParallelSimulationService ─► WindDirectionWorker     │
    │     ShapefileService (SHP → GeoJSON, Albers→WGS84)       │
    │     ExcelService (模板/导入/导出)                         │
    │         │                                                │
    │         ▼                                                │
    │   GnnSimulation.Core (无依赖)                            │
    │     GaussianPlumeModel · PasquillGifford ·               │
    │     PollutantProperties · StabilityClassifier ·          │
    │     ContributionAnalysis                                 │
    │         │                                                │
    │         ▼                                                │
    │   GnnSimulation.Data ── EF Core 9 ─────┐                 │
    │     实体 × 5 · Fluent 映射 · 自动时间戳│                 │
    └──────────────────────────────────────┬─┘                 │
                                           │                   │
                              ┌────────────┴────────────┐      │
                              ▼                         ▼      │
                         SQLite                    Shapefile   │
                    air_pollution.db          县（等积投影）.shp│
                    (匿名演示库)                  60 MB, Albers│
```

## 四层后端

| 层 | 项目 | 职责 | 无依赖上游？ |
|---|---|---|---|
| API | `GnnSimulation.Api` | HTTP、DTO、DI、CORS、路由 | 依赖 Core + Data |
| 服务 | `GnnSimulation.Api/Services` | 业务编排：加载数据→调用算法→聚合→返回 | 依赖 Core + Data |
| 算法 | `GnnSimulation.Core` | 高斯烟羽 + 衰减 + 贡献排名（纯函数） | ✅ 零依赖，无数据库、无 HTTP |
| 持久化 | `GnnSimulation.Data` | 实体 + DbContext + Migrations | 依赖 EF Core |

**设计原则**：Core 不依赖 Data/Api，方便单独测试、复用到其他 UI（如 CLI 批处理）、甚至移植到其他语言。

## 数据流：一次单风向模拟

```
POST /api/simulation/run
   │
   ▼
SimulationController.Run
   │
   ▼
SimulationService.RunAsync
   ├─► 加载 Meteorology (by id)
   ├─► 加载 EmissionSource 列表（IsActive 过滤）+ Include(Pollutants)
   ├─► 加载 Receptor 列表（IsActive 过滤）
   ├─► GridBuilder.Build → 外包框 + domain_size 构建方形网格 (clamp 50-500 点)
   │
   ├─► 遍历每个源：
   │     ComputeEmissionRates (合并污染物速率；等效面源: 浓度→等效速率)
   │     DispatchSourceField (按 source_type 派发到 Core 层):
   │       point           → CalculateConcentrationField
   │       area            → CalculateAreaSourceConcentrationField
   │       equivalent_area → 带 isEquivalent=true 的面源，浓度夹紧
   │       line            → CalculateLineSourceConcentrationField (分段点源法)
   │     AddInPlace → 累加到总浓度场
   │     每污染物独立再算一次 → pollutantConcentrations dict
   │
   ├─► 遍历每个受体：
   │     对每个污染物 对每个源：
   │       CalculateReceptor*Concentration (点/面/线/等效面分派)
   │       < 1e-6 清零
   │     按浓度降序排 + 计算 percentage
   │
   └─► 返回 SimulationResultDto (concentrations 2D + receptorContributions 嵌套字典 + ...)
```

## 数据流：多风向并行

```
POST /api/simulation/run_parallel
   │
   ▼
ParallelSimulationService.RunAsync
   ├─► 加载数据（同上，但一次性共享）
   ├─► 构建 WindDirectionWorker.Context (无状态上下文)
   ├─► Task.Run(() => Parallel.ForEach(windDirections, ctx =>
   │       WindDirectionWorker.Run(wind, ctx)    // 每个风向独立计算
   │   ))
   │     → ConcurrentBag<WindDirectionResultDto>
   │
   ├─► 估算内存 > 0.5 GB ⇒ 强制 returnAggregatedOnly = true（防响应爆）
   │
   └─► 聚合：
         权重归一化 (nullable → 等权)
         总浓度场 = Σ (风向结果 × 权重)
         污染物浓度场 = Σ (风向结果 × 权重) 按类别独立累加
         受体贡献聚合 = Σ (同 source_id 加权合并) → 排序 + 百分比
         附计算时间、加速比、失败列表
```

**与 Python 原版的关键差异**：

| | Python | .NET |
|---|---|---|
| 并发模型 | `ProcessPoolExecutor` (进程) | `Parallel.ForEach` (线程) |
| 数据传递 | pickle 序列化 | 零拷贝共享 |
| 网格中心 | 源经纬度均值 | 同 |
| 网格点数 | `int(domain/res)+1`，不夹 | 同 |
| 每污染物场 | `source_conc × p_rate/total` | 同（p_fraction 优化） |

## 前端分层

```
src/
├── api/          ← Axios 封装，错误拦截器透传 { detail }
├── types/        ← 手写 TS 类型镜像后端 DTO（camelCase）
├── stores/       ← Pinia：app（折叠）+ prefs（localStorage 持久化）
├── router/       ← Vue Router，懒加载 + document.title 自动设置
├── views/        ← 4 个页面（Dashboard/Sources/Receptors/Meteorology）
├── components/   ← 可复用：MapPanel、HeatmapCanvas（通过 composable）、
│                    ColorLegend、ContributionPanel、ParallelSimulationDialog
├── composables/  ← useHeatmapRenderer（Canvas 渲染与 LatLng 计算）
└── utils/        ← coords、colorScale、download、error（纯函数，好测）
```

**设计原则**：
- 一切可纯函数化的逻辑进 `utils/` 或 `composables/`
- 组件只负责渲染 + 用户交互，不直接管理持久化状态
- Pinia store 仅作全局状态枢纽；业务数据放在 view 的 `ref`

## 10 阶段演进史

| 阶段 | 产出 | 后端测试 | 前端测试 |
|---|---|---|---|
| **P1** | .NET 骨架 + EF Core 实体 + Migration | 28 | — |
| **P2** | 4 个 Controller CRUD + DTO + 映射 + 集成测试 | 57 | — |
| **P3** | Core 层高斯烟羽算法完整移植 + Python 黄金值对齐 | 102 | — |
| **P4** | `/api/simulation/run` 单风向编排 | 113 | — |
| **P5** | `/api/simulation/run_parallel` 多风向并行 + 加权聚合 | 122 | — |
| **P6** | Shapefile (ProjNet Albers→WGS84) + Excel IO | 136 | — |
| **P7** | Vue 骨架 + Router + Pinia + API client + Vitest | 136 | 16 |
| **P8** | 三页 CRUD UI（含排放源四种源类型动态表单） | 136 | 24 |
| **P9** | Leaflet 地图 + Canvas 热力图 + 色阶 + GCJ02 | 136 | 44 |
| **P10** | Prefs 持久化 + 贡献面板 + 并行对话框 + 图例 + E2E 联调 | 136 | 55 |

**最终**：191 个自动化测试全绿，真实数据 500×500 网格模拟验证通过。

## 关键权衡

### 为什么用 ProjNet 而不是 NetTopologySuite 内置

NTS 本身不做 CRS 变换。ProjNet 是微软系维护的轻量投影库，支持 WKT → 坐标系解析 + Albers 等复杂投影。比 DotSpatial 小很多。

### 为什么 Map 的 `/geojson` 默认不加载

60 MB shp 转成 GeoJSON 约 100+ MB 响应，浏览器解析卡死。沿用 Python 原版 `LOAD_SHP_BY_DEFAULT=False`，按需 `?force=true`。

### 为什么并行模拟用线程不是进程

.NET 无 GIL，线程共享内存，`Parallel.ForEach` 零拷贝。Python 必须进程是因为 GIL 限制 CPU 并行 + numpy 释放 GIL 有限。

### 为什么污染物浓度场用 `p_fraction × total_field`

避免每污染物独立重新算一次浓度场。单风向 `/run` 重算（精确），多风向 `/run_parallel` 比例拆分（快 N 倍，误差来自衰减的非线性叠加，但测试验证聚合结果一致）。
