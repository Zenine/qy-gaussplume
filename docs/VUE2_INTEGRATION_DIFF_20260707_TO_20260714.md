# Vue2 完整版集成差异说明（2026-07-07 → 2026-07-14）

## 1. 文档目的与对比基线

本文用于帮助平台集成同事将 2026-07-07 的 Vue2 完整版升级到 2026-07-14 交付版。

- 对比基线：`qy-gaussplume-vue2-platform-full-20260707-1345.zip`
- 目标版本：`qy-gaussplume-vue2-platform-full-20260714-1506.zip`
- 目标包 SHA-256：`10fc0482930dc5ec3eb4b95e954cd448cdf7096f924d0dec6afd2cb27b2cb125`
- 目标包内容：Vue2 源码和 `dist`、.NET 后端源码与测试、匿名演示数据库、行政边界、文档及 Vue2 专用启停/验证脚本。

如果同事手中的 7 月 7 日包不是上述 `1345` 版本，建议以本文的“目标文件”和“接口变化”作为升级依据，不要只按文件时间覆盖。

## 2. 总体兼容性结论

| 项目 | 结论 | 集成影响 |
|---|---|---|
| 数据库表结构 | **没有变化** | 两个交付包的 SQLite `.schema` 完全一致，不需要执行迁移脚本 |
| Vue2 npm 依赖 | **没有变化** | `package.json` 没有依赖差异，仍建议重新执行 `npm ci` |
| 原有 API 路径 | **保留** | 原 CRUD、单风向和多风向接口路径不变 |
| 多风向请求 DTO | **向后兼容扩展** | 新增可选 `windSpeeds`；不传时继续使用统一 `windSpeed` |
| 多风向权重校验 | **行为收紧** | 以前数量不匹配可能退回等权；现在返回 400，调用方必须保证数组长度一致 |
| 污染物目录 | **新增 SO2** | 平台若缓存固定污染物列表，需要加入 SO2 |
| 线源数据库字段 | **保留** | `lineSegmentLength` 不改名，但界面语义改为“FLSI 积分步长” |
| 前端路由和四个主页面 | **路径不变** | 主控台、排放源、受体点、气象场页面可原位替换 |
| Vue2 集成环境变量 | **新增明确配置样例** | 建议使用 `.env.example` 配置 API 根地址、前缀、API Key 和路由模式 |

## 3. 主要功能变化

### 3.1 多风向风频 XLSX 导入

7 月 14 日版在 Vue2 主控台增加独立的“导入风频 XLSX”对话框，支持：

1. 下载 72 方位模板。
2. 导入三列表格：`风向中心角度`、`平均风速(m/s)`、`加权值`。
3. 预览全部导入行、方位数量和权重总和。
4. 全局模拟时逐方位传递对应风速和权重，不再只能使用一个统一风速。
5. 清除导入数据后恢复原来的预设方向数和统一风速模式。

新增接口：

```http
GET /api/simulation/wind-profile/template
POST /api/simulation/wind-profile/import
```

上传接口使用 `multipart/form-data`，字段名为 `file`，只接收 XLSX，文件最大 5 MB。

多风向请求新增可选字段：

```json
{
  "windSpeed": 2.5,
  "windDirections": [0, 5, 10],
  "windSpeeds": [2.45, 2.23, 2.24],
  "weights": [0.0169, 0.0159, 0.0151]
}
```

兼容规则：

- 不传 `windSpeeds`：继续对所有方向使用 `windSpeed`。
- 传 `windSpeeds`：数量必须与 `windDirections` 相同，每项必须是大于 0 的有限数值。
- 传 `weights`：数量必须与 `windDirections` 相同，每项必须非负且总和大于 0。
- 后端会归一化权重；部分方向失败时，只对成功方向对应的原始权重重新归一化。

> **需要特别注意：**7 月 7 日版对错误权重可能静默退回等权，7 月 14 日版会直接返回 400。这是本次最需要调用方检查的行为变化。

### 3.2 新增 SO2 业务污染物

SO2 已加入后端污染物目录、排放源 Excel 模板、Vue2 计算污染物下拉、多污染物结果、公式说明和单/多风向计算。

| 参数 | 数值 |
|---|---:|
| 重力沉降速度 | 0 |
| 干沉降 Rb / Rc | 150 / 400 |
| 湿沉降 a / b | `8×10⁻⁶` / `0.7` |
| 化学转化 k | `4.81×10⁻⁵` |
| 温度增强 | `×1.5` |
| 湿度增强 | `×1.3` |

集成方如果在宿主平台中硬编码了污染物白名单、下拉选项、颜色或单位，需要同步加入 `SO2`。数据库不需要增加字段，污染物仍通过污染物记录表保存。

### 3.3 线源计算与显示

计算侧：

- 7 月 7 日版按短面源分段累加。
- 7 月 14 日版使用有限长线源积分法（FLSI）：`C = ∫₀ᴸ q′K(s)ds`，`q′=Q/L`。
- 使用 Gauss-Legendre 数值求积；`lineSegmentLength` 仅作为积分面板最大步长，不再表示物理点源或短面源。
- 请求字段和数据库字段名称保持不变，不需要迁移。

显示侧：

- 移除线源起终点的点源式圆点。
- 使用连续双层圆角线带，并保证线源几何图层位于浓度热力图之上。
- 对线源热力图做轻微视觉柔化，减少网格峰值形成的“多个点连成线”观感。
- 显示柔化不修改后端返回的浓度矩阵和受体贡献数值。

### 3.4 排放源和受体点标记

- 排放源“标记图标”从自由文本框改为图标目录选择器。
- 受体点使用同一图标目录，并增加 `monitor`（监测点）图标。
- 地图不再忽略 `markerSymbol`，点源和受体点会显示选中的图标与颜色。
- 后端原标记目录接口继续复用，没有新增数据库表。

### 3.5 等效面源污染物输入

- 7 月 14 日版使用严格互斥的 `v-if/v-else`，等效面源只显示“实测浓度”输入框。
- 普通源只显示“排放速率”输入框。
- 解决 Vue2 组件复用导致等效面源同时出现两列数值框的问题。

### 3.6 主控台布局与污染物切换

- 多风向主控台工具栏改为上下两行，空间不足时行内换行，不再依赖横向拖动才能看到导入按钮。
- 计算污染物与结果显示污染物继续分离：计算下拉影响下一次请求，结果下拉只切换本次已有结果。
- 结果污染物下拉只展示后端本次实际返回的污染物分场，避免选择未计算污染物后界面没有变化。
- 风频上传修复 Element UI 默认二次上传问题，文件只提交一次。

## 4. API 和数据结构变化

### 4.1 `POST /api/simulation/run_parallel`

请求新增：

```ts
windSpeeds?: number[]
```

响应的逐风向详情新增：

```ts
windSpeed: number
```

原字段未删除。宿主平台若使用严格 DTO 反序列化，需要允许新增响应字段。

### 4.2 `GET /api/simulation/formulas`

污染物公式信息新增：

```ts
chemicalTemperatureMultiplier: number
chemicalHumidityMultiplier: number
```

线源公式说明改为 FLSI。宿主平台如果自行定义公式 DTO，需要加入上述两个数值字段或允许忽略未知字段。

### 4.3 风频导入响应

```json
{
  "directionCount": 72,
  "windDirections": [0, 5, 10],
  "windSpeeds": [2.45, 2.23, 2.24],
  "weights": [0.0169, 0.0159, 0.0151],
  "weightSum": 1.0
}
```

## 5. 需要合并或替换的重点文件

### Vue2 前端

- `frontend-vue2/src/views/DashboardView.vue`
- `frontend-vue2/src/components/ParallelSimulationDialog.vue`
- `frontend-vue2/src/components/MapPanel.vue`
- `frontend-vue2/src/views/SourcesView.vue`
- `frontend-vue2/src/views/ReceptorsView.vue`
- `frontend-vue2/src/components/FormulaDrawer.vue`
- `frontend-vue2/src/api/simulation.ts`
- `frontend-vue2/src/types/index.ts`
- `frontend-vue2/src/utils/markerSymbols.ts`（新增）
- `frontend-vue2/src/style.css`
- `frontend-vue2/scripts/check-dashboard-regression.cjs`
- `frontend-vue2/scripts/check-management-parity.cjs`
- `frontend-vue2/.env.example`（新增）

这些文件存在相互依赖，建议整体替换 `frontend-vue2/` 后再移植宿主平台定制，而不是只复制单个 Vue 文件。

### .NET 后端

- `GnnSimulation.Api/Controllers/SimulationController.cs`
- `GnnSimulation.Api/Dtos/ParallelSimulationDtos.cs`
- `GnnSimulation.Api/Dtos/SimulationDtos.cs`
- `GnnSimulation.Api/Services/ExcelService.cs`
- `GnnSimulation.Api/Services/ParallelSimulationService.cs`
- `GnnSimulation.Api/Services/WindDirectionWorker.cs`
- `GnnSimulation.Core/Atmosphere/GaussianPlumeModel.cs`
- `GnnSimulation.Core/Atmosphere/PollutantProperties.cs`
- `GnnSimulation.Data/Entities/PollutantCatalog.cs`

测试文件也应一起更新，避免平台后续修改时丢失回归保护。

## 6. 推荐升级步骤

1. 备份当前宿主平台中的旧 Vue2 集成目录和平台特有改动。
2. 解压 7 月 14 日完整包，先单独运行，不要直接覆盖生产目录。
3. 执行 `cd frontend-vue2 && npm ci`。
4. 根据平台网关复制 `.env.example` 为本地环境文件；密钥只通过环境变量注入。
5. 平台静态目录或子路径部署优先使用 `VITE_ROUTER_MODE=hash`。
6. 整体合并 Vue2 前端重点文件；保留宿主平台自己的登录、菜单、权限和网关封装。
7. 更新后端重点文件；无需执行数据库结构迁移。
8. 执行 `./scripts/verify.sh`，或至少执行后端测试、Vue2 静态回归和 Vue2 构建。
9. 按第 7 节清单做联调验收。

## 7. 集成验收清单

- [ ] 原有点源、面源、线源、等效面源列表和编辑功能正常。
- [ ] 等效面源污染物区域只显示一个实测浓度输入框。
- [ ] 排放源和受体点可以选择图标，地图显示与选择一致。
- [ ] SO2 可以创建、导入、计算，并能在结果污染物中切换。
- [ ] 72 方位模板可以下载。
- [ ] 自定义 XLSX 可以导入，页面显示方位数和权重和。
- [ ] 多风向请求中的 `windDirections`、`windSpeeds`、`weights` 数量一致。
- [ ] 错误权重、重复方向、非正风速和超过 5 MB 文件能收到明确错误。
- [ ] 主控台在 1024、1280、1440 px 宽度下无需横向拖动即可看到风频导入和运行按钮。
- [ ] 模拟后切换结果污染物，热力图和受体贡献同步变化；计算污染物选择不会错误切换旧结果。
- [ ] 线源地图显示为连续线带，不显示起终点圆点；显示柔化不改变接口原始浓度值。
- [ ] 单风向与多风向的受体贡献、污染物分场均能正常返回。

## 8. 验证记录

7 月 14 日目标版本已执行完整 `./scripts/verify.sh`：

- 后端 xUnit：192 项通过。
- Vue3 仓库基准：123 项通过。
- Vue2 静态回归检查：通过。
- Vue2 生产构建：通过。
- Vue3 生产构建：通过。

## 9. 交付下载

7 月 14 日 Vue2 完整包临时下载地址：

<https://share.niaite.com/f/6379ba87b6a861384f4bb080dba80dec>

链接有效期为 7 天。过期后需要重新生成分享链接。
