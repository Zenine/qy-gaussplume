# Changelog

项目所有显著变更按时间倒序记录。维护者：**Zenine Xu** <zeninexu@gmail.com>

格式遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)。

---

## [3.0.3] - 2026-05-25

### 新增
- 排放源管理页明确展示“批量导入”入口，继续复用现有 Excel 模板与导入 API。
- 受体点管理页新增“批量导入”和“批量删除”入口，可对已选受体点批量操作。
- 前端新增主控台气象控件、等效面源污染物显示/提交、批量导入按钮和受体点批量删除回归测试。

### 变更
- 等效面源污染物表单只保留一个污染物数值输入框，前端统一把该值写入 `concentration`，内部 `emissionRate` 固定为 0。
- 受体点批量删除改为等待所有删除请求完成；如果部分失败，仍会刷新列表并提示失败数量。
- 测试规模更新为后端 137 个、前端 69 个，合计 206 个自动化测试。

### 修复
- 修复主控台显示模拟结果后风速、风向控制框消失的问题。
- 修复等效面源污染物列表读取 `emissionRate=0`，导致 PM10 等污染物显示为 0 而非实测 `concentration` 的问题。
- 修复受体点批量删除部分失败后列表不刷新的问题，现在会刷新列表并提示失败数量。

## [3.0.2] - 2026-05-19

### 新增
- 主控台恢复地图悬浮式操作区：图层/风场/污染物工具条、左下模拟范围与网格分辨率、右侧绘制区域、气象控制、数据统计、模拟结果和受体点贡献分析卡片。
- 地图支持原生 Leaflet 矩形框选；框选后运行模拟只提交区域内排放源与受体点。
- 新增区域筛选工具与前端回归测试，覆盖框选过滤、首页悬浮卡片、结果清除与贡献卡片。
- 后端新增空 `receptorIds` 回归测试，保证空选择范围不会回退到全部受体点。
- 新增 GNN 首页 Hero 图、qy 项目架构图和核心功能介绍图，并在根 README、GNN README、qy 后端 README 中引用。

### 变更
- 首页结果卡补充透明度和渲染精度控制，快捷按钮补充提示。
- 注释聚焦当前公式、数据流和边界行为说明，移除生产代码中的历史来源表述。
- 测试规模更新为后端 137 个、前端 65 个，合计 202 个自动化测试。
- README 补充图片说明，便于汇报材料和交付文档复用。

### 修复
- 修复框选区域内无受体点时，后端把空数组当作未筛选并加载全部受体点的问题。
- 修正绘制区域文案，明确当前支持矩形区域。

## [3.0.1] - 2026-04-26

### 变更
- 确定开源项目名：**QY-GaussPlume（清源高斯烟羽扩散模拟平台）**。
- 更新根 README 为开源入口页，补充使用场景、示例数据说明和验证入口。
- 新增 MIT `LICENSE`。
- 新增项目级验证脚本 `scripts/verify.sh`。
- 将前端包名和页面标题更新为项目名。
- 匿名化内置 SQLite 演示数据，避免公开真实项目名称。
- 新增 README 运行截图，展示主控台模拟、贡献分析和三类数据管理页面。
- 统一前端侧边栏与浏览器标题中的开源项目名。
- 修正 `scripts/start.sh` 与 `scripts/stop.sh` 的可执行位，保证 README 启停命令可直接运行。

## [3.0.0] - 2026-04-20

### 🚀 技术栈完全重写：.NET + Vue

从 FastAPI + 原生 HTML 迁移到 ASP.NET Core 9 + Vue 3，共 10 个阶段增量交付。

#### 新增
- **.NET 9 后端** (`backend-dotnet/`)：4 项目分层（Api/Core/Data/Tests）+ 191 自动化测试
- **Vue 3 前端** (`frontend-vue/`)：TypeScript + Element Plus + Pinia + Leaflet + Vitest
- **文档体系**：根 README、子项目 README、docs/{ARCHITECTURE,WORKFLOW,API}、CHANGELOG
- **跨平台启停脚本**：`scripts/start.sh` + `scripts/stop.sh`（macOS/Linux）
- **Canvas 热力图双线性插值**，4096 自动降级防浏览器崩
- **WGS84 ↔ GCJ02 坐标转换**（含迭代反算，亚米精度）
- **状态持久化**：10 个可视化偏好字段同步到 `localStorage.gnn.prefs.v1`
- **色阶图例 + 自定义浓度范围**：Jet / Turbo / Viridis / Grayscale
- **贡献度抽屉面板**：受体×污染物两级下拉 + 排名 + 百分比
- **并行模拟对话框**：8 / 16 / 32 / 72 风向预设 + 自定义权重
- **Shapefile 读取**：Krasovsky 1940 Albers → WGS84 投影转换（ProjNet）
- **Excel 模板与导入导出**（ClosedXML）：受体点 + 四种源类型

#### 变更
- API JSON 字段命名：`snake_case` → **`camelCase`**（ASP.NET Core 默认约定）
- 并发模型：`ProcessPoolExecutor` → **`Parallel.ForEach`**（.NET 无 GIL，零拷贝）
- 浓度场渲染：内联 JS → **Vue composable + Canvas + 自适应 renderScale**
- 数据库：SQLite `air_pollution.db` 沿用，但 EF Core 映射层重写
- 启动端口：Python 8000 → **.NET :5207**（默认） + Vite **:5173**

#### 修复
- **EF Core `HasDefaultValue` 陷阱**：CLR 值匹配默认会跳过 INSERT；全部清理改用 C# 属性初始化器
- **历史数据库的 `is_active = NULL`**：`Program.cs` 启动时自愈 UPDATE
- **Shapefile LinearRing 浮点漂移**：`CreateClosedRing` 强制首末点一致
- **jsdom 无 Canvas 2D context**：测试用最小 stub 覆盖

#### 性能
- .NET 黄金值与参考数据按 **1e-9 绝对误差对齐**（72 个场景全覆盖）
- 多风向并行聚合：**逐格点 1e-9 精度** vs 算术平均期望值

### 代码规模
| | 后端 | 前端 | 测试 |
|---|---|---|---|
| 源码行数 | ~5500 | ~3500 | — |
| 测试用例 | 136 | 55 | 191 ✅ |
| 测试时长 | ~30s | ~7s | |

---

## [2.0.0] - 2025-01

### 🚀 性能革命性升级（Python 版）

- **NumPy 向量化重构**：消除所有 Python 循环，单风向 60s → 2-3s（25×）
- **多进程并行**：32 核同时计算，72 风向 72 分钟 → 15-30s（150-300×）
- **后端智能聚合**：72 风向数据传输 2GB → 20MB（99% 减少）
- **UTF-8 编码修复**：解决中文界面乱码
- **完整受体点贡献分析**：每风向独立计算 + 权重聚合
- **实时进度反馈**：显示计算耗时、加速比、节省的内存量
- **极限配置支持**：10m 分辨率 × 10km 范围 × 72 风向

---

## [1.x] - 2024 及更早

初版 FastAPI + 原生 HTML/JavaScript 实现。详细历史见 git log。
