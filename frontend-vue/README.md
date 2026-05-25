# QY-GaussPlume 前端

清源高斯烟羽扩散模拟平台前端。Vue 3 + TypeScript + Vite + Element Plus + Pinia + Vue Router + Leaflet。

## 当前版本

| 项目 | 内容 |
|---|---|
| 版本 | **3.0.2** |
| 更新日期 | **2026-05-19** |
| 主要范围 | GNN 首页悬浮操作区、矩形区域筛选、结果控制、受体贡献摘要 |
| 验证结果 | Vitest 65 个用例，`npm run build` 通过 |

## 本次 GNN 修改说明

- **主控台布局恢复**：首页改回地图主画布，恢复顶部紧凑工具条、左下范围控制、右侧功能卡片的悬浮布局。
- **运行前功能卡**：绘制选择区域、气象控制、数据统计都在首页右侧展示；矩形框选后统计会切换为当前区域内排放源和受体点数量。
- **运行后结果卡**：模拟完成后展示污染物筛选、色阶类型、浓度范围、透明度、渲染精度、图例和受体点贡献分析。
- **区域筛选请求**：运行模拟时把框选范围内的 `sourceIds` / `receptorIds` 写入请求；空受体点范围保持为空，便于后端返回真实筛选结果。
- **维护注释与测试**：补充主控台流程、地图框选、坐标转换、热力图渲染相关中文注释；新增首页功能和区域筛选回归测试。

## GNN 首页 Hero 图

![GNN 首页 Hero 图](../docs/assets/generated/qy-gnn-hero.png)

这张图对应当前 GNN 主控台：地图为主画布，顶部工具条负责图层、气象场、污染物和模拟动作，左下角控制模拟范围与网格分辨率，右侧悬浮卡片承载区域绘制、气象控制、数据统计和运行后结果分析。

## GNN 功能介绍图

![GNN 核心功能介绍图](../docs/assets/generated/qy-features.png)

功能介绍图覆盖当前前端五类核心入口：排放源管理、受体点管理、气象场管理、主控台模拟和贡献排名。后续如果首页控件或管理页流程继续变化，应同步更新此图和本 README 的版本说明。

## 快速开始

```bash
npm install --registry=https://registry.npmmirror.com   # 国内推荐
cp .env.example .env                                    # 如需改后端代理目标
npm run dev                                             # http://localhost:5173
```

前端通过 Vite dev proxy 把 `/api/*` 转发到 `VITE_API_PROXY_TARGET`（默认 `http://localhost:5207`）。先启动后端：

```bash
cd ../backend-dotnet && dotnet run --project GnnSimulation.Api
```

## 脚本

| 命令 | 作用 |
|---|---|
| `npm run dev` | Vite 开发服务器，热更新，`/api/*` 代理到后端 |
| `npm run build` | `vue-tsc -b`（类型检查） + `vite build`，输出 `dist/` |
| `npm run preview` | 预览生产构建 |
| `npm test` | Vitest 一次跑完全部 65 用例 |
| `npm run test:watch` | Vitest watch 模式 |

## 目录结构

```
frontend-vue/
├── vite.config.ts            # proxy /api → :5207；@ → src；vitest 配置
├── tsconfig.{app,node}.json
├── .env.example
├── src/
│   ├── main.ts               # createApp + Pinia + Router + ElementPlus (zh-CN)
│   ├── App.vue               # 侧边栏 + 顶栏 + router-view 布局
│   ├── router/index.ts       # 4 路由懒加载；afterEach 设 document.title
│   ├── stores/
│   │   ├── app.ts            # 侧边栏折叠
│   │   └── prefs.ts          # localStorage 持久化的用户偏好（色阶/透明度/...）
│   ├── types/index.ts        # 与 .NET DTO 一一对齐的 TS 类型
│   ├── api/
│   │   ├── client.ts         # axios 实例 + { detail } 透传到 error.message
│   │   ├── sources.ts receptors.ts meteorology.ts simulation.ts map.ts
│   │   └── index.ts
│   ├── views/
│   │   ├── DashboardView.vue # 主控台：地图悬浮工具条 + 框选 + 结果/贡献卡 + 并行对话框
│   │   ├── SourcesView.vue   # 排放源 CRUD（含污染物子表、按类型动态字段）
│   │   ├── ReceptorsView.vue # 受体点 CRUD + Excel 导入导出
│   │   └── MeteorologyView.vue # 气象场 CRUD
│   ├── components/
│   │   ├── MapPanel.vue      # Leaflet 地图 + 高德瓦片 + 源/受体标记 + 热力图叠加
│   │   ├── ColorLegend.vue   # 色阶图例条
│   │   ├── ContributionPanel.vue    # 抽屉式受体贡献排名
│   │   └── ParallelSimulationDialog.vue  # 8/16/32/72 风向并行模拟
│   ├── composables/
│   │   └── useHeatmapRenderer.ts   # 双线性插值 + Canvas 渲染 + 4096 自动降级
│   └── utils/
│       ├── coords.ts         # WGS84 ↔ GCJ02 坐标转换
│       ├── colorScale.ts     # Jet / Turbo / Viridis / Grayscale
│       ├── selection.ts      # 矩形区域筛选
│       ├── download.ts       # blob 触发下载
│       └── error.ts
└── tests/
    ├── App.spec.ts           # 1：应用外壳、图标化导航、折叠按钮
    ├── api.spec.ts           # 12：所有 API 函数 URL/payload 正确
    ├── coords.spec.ts        # 6：国内外检测、加密偏移、往返亚米精度
    ├── colorScale.spec.ts    # 9：归一化、对数、四种色阶、范围扫描
    ├── heatmap.spec.ts       # 4：Canvas 尺寸、4096 自动降级、GCJ02 bounds
    ├── prefs.spec.ts         # 5：localStorage 持久化 / 恢复 / reset
    ├── selection.spec.ts     # 2：矩形范围命中与实体筛选
    ├── router.spec.ts store.spec.ts
    ├── components/           # ColorLegend、ContributionPanel、ParallelSimulationDialog
    └── views/                # DashboardView、SourcesView、ReceptorsView、MeteorologyView
```

## 关键技术点

### 与后端契约对齐

- **camelCase JSON**：ASP.NET Core 默认序列化约定，TS 类型按此镜像
- **`/api/{sources|receptors|meteorology|simulation|map|config}/...`**：所有调用通过 `src/api/*.ts` 封装
- **错误透传**：后端统一返回 `{ detail: "..." }`，拦截器把 detail 塞进 `error.message`，页面可直接 `ElMessage.error(e.message)`

### 状态持久化（`stores/prefs.ts`）

以下偏好自动同步到 `localStorage.gnn.prefs.v1`：

```
scale · opacity · renderScale · tileLayer · selectedPollutant
gridResolution · domainSize · customMin · customMax · useLogScale
```

页面刷新后恢复。点"恢复默认"清空。

### 主控台悬浮操作区

- 顶部工具条提供地图图层、气象场、污染物、运行模拟和清除结果。
- 左下角用滑块控制模拟范围（km）和网格分辨率（m），并继续写入 `prefs`。
- 右侧初始卡片提供矩形区域绘制、气象参数预览、当前范围内排放源/受体点统计。
- 模拟完成后右侧切换为结果卡和受体点贡献分析卡，可调整色阶、透明度、渲染精度和浓度范围。
- 框选区域后，前端会把区域内 `sourceIds` / `receptorIds` 随模拟请求提交；空受体列表保持为空，不回退到全部受体点。

### 坐标系统（`utils/coords.ts`）

- GPS 原始数据为 WGS84，国内地图瓦片是 GCJ02（加密偏移）
- `wgs84ToGcj02` 正向加密；`gcj02ToWgs84` **迭代反算收敛到亚米级**
- 国外坐标检测（`isOutsideChina`）跳过转换

### 热力图渲染（`composables/useHeatmapRenderer.ts`）

1. 从后端拿到 `number[][]` + `gridLat/gridLon`
2. 新建离屏 Canvas，尺寸 = 网格 × `renderScale`
3. **Canvas > 4096×4096 自动降级** `renderScale` 直到安全尺寸
4. 逐像素双线性插值采样 + 色阶映射 + alpha 归一化
5. `canvas.toDataURL()` → `L.imageOverlay` 贴到 GCJ02 偏移后的 `LatLngBounds`

### 高德瓦片

街道 / 卫星 / 混合三种，四子域 `webrd0{1,2,3,4}` 负载均衡，`lang=zh_cn` 确保中文标注。

## 测试

```bash
npm test          # 17 文件 · 65 用例
npm run build     # 含 TS 类型检查
```

Vitest 用 jsdom，对 `<canvas>` 2D context 在 `tests/heatmap.spec.ts` 中做了最小 stub。

## 环境变量

| 变量 | 默认 | 说明 |
|---|---|---|
| `VITE_API_PROXY_TARGET` | `http://localhost:5207` | dev 时 `/api` 代理目标 |
| `VITE_API_BASE_URL` | `/` | 生产构建的 API 根路径（同源留空） |

## 与早期实现的差异

| 维度 | 早期实现 | 当前实现 |
|---|---|---|
| JSON 字段命名 | snake_case | camelCase |
| 状态管理 | 直接操作 DOM + localStorage | Pinia + 自动同步 |
| 热力图 | 内联大块 JS | 拆分 composable，可测 |
| 类型安全 | 无 | TypeScript 全覆盖 |
| 测试 | 几乎无 | 65 单测 + 组件测试 |

## 常见问题

**启动后点 "▶ 运行模拟" 弹 `Request failed with status code 500`？**
→ 多数是老数据库里某列为 NULL 但 C# 非空属性读不出来。后端 Program.cs 有启动自愈修复常见的 `is_active` NULL；其他字段按需在日志里看堆栈定位。

**图层切到卫星后不显示？**
→ 高德瓦片要求 `subdomains=['1','2','3','4']`，检查网络可访问 `webst01.is.autonavi.com`。

**保存的色阶/透明度丢了？**
→ 检查浏览器是否禁用 localStorage（隐私模式），或点过"恢复默认"。

## 维护者

**Zenine Xu** · <zeninexu@gmail.com>

前端运行日志写到 `.run/frontend.log`（`scripts/start.sh` 启动时）。
