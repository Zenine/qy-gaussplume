# QY-GaussPlume 前端

清源高斯烟羽扩散模拟平台前端。Vue 3 + TypeScript + Vite + Element Plus + Pinia + Vue Router + Leaflet。

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
| `npm test` | Vitest 一次跑完全部 60 用例 |
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
│   │   ├── DashboardView.vue # 主控台：两行控制面板 + 地图 + 图例 + 抽屉 + 并行对话框
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
│       ├── download.ts       # blob 触发下载
│       └── error.ts
└── tests/
    ├── App.spec.ts           # 1：应用外壳、图标化导航、折叠按钮
    ├── api.spec.ts           # 12：所有 API 函数 URL/payload 正确
    ├── coords.spec.ts        # 6：国内外检测、加密偏移、往返亚米精度
    ├── colorScale.spec.ts    # 9：归一化、对数、四种色阶、范围扫描
    ├── heatmap.spec.ts       # 3：Canvas 尺寸、4096 自动降级、GCJ02 bounds
    ├── prefs.spec.ts         # 5：localStorage 持久化 / 恢复 / reset
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
npm test          # 15 文件 · 60 用例 · ~7s
npm run build     # 含 TS 类型检查
```

Vitest 用 jsdom，对 `<canvas>` 2D context 在 `tests/heatmap.spec.ts` 中做了最小 stub。

## 环境变量

| 变量 | 默认 | 说明 |
|---|---|---|
| `VITE_API_PROXY_TARGET` | `http://localhost:5207` | dev 时 `/api` 代理目标 |
| `VITE_API_BASE_URL` | `/` | 生产构建的 API 根路径（同源留空） |

## 与旧 Python 栈的差异

| 维度 | 旧 (FastAPI + 原生 HTML) | 新 (ASP.NET Core + Vue) |
|---|---|---|
| JSON 字段命名 | snake_case | camelCase |
| 状态管理 | 直接操作 DOM + localStorage | Pinia + 自动同步 |
| 热力图 | 内联大块 JS | 拆分 composable，可测 |
| 类型安全 | 无 | TypeScript 全覆盖 |
| 测试 | 几乎无 | 60 单测 + 组件测试 |

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
