> **语言 / Language**: **简体中文** · [English](README.en.md) · [日本語](README.ja.md) · [繁體中文](README.zh-TW.md)

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-9.0-512bd4?style=flat-square)](backend-dotnet/)
[![Vue](https://img.shields.io/badge/Vue-3-42b883?style=flat-square)](frontend-vue/)
[![Powered by Meridian](https://img.shields.io/badge/Powered%20by-Meridian-8b5cf6?style=flat-square)](https://github.com/lordmos/meridian)

# QY-GaussPlume（清源高斯烟羽扩散模拟平台）

QY-GaussPlume 是给科研团队、环评工程师和方案评估人员使用的大气污染物扩散模拟平台。它把排放源、受体点、气象场和地图可视化放在同一个工作台里，让团队可以快速比较污染扩散影响、受体贡献和不同风场条件下的工程方案。

## Quick Start

```bash
git clone git@github.com:Zenine/qy-gaussplume.git
cd qy-gaussplume
./scripts/start.sh
```

打开 <http://localhost:5173>。前端会把 `/api/*` 代理到后端 <http://localhost:5207>。

提交前运行完整验证：

```bash
./scripts/verify.sh
```

## Why It Matters

- 让污染扩散评估从脚本试算变成可操作的地图工作流。
- 支持点源、面源、线源和等效面源，适合工程现场常见数据形态。
- 支持单风向和多风向加权并行模拟，便于做风场敏感性分析。
- 对每个受体点输出污染源贡献排名，帮助解释“影响来自哪里”。
- 内置匿名演示数据与完整测试入口，便于交付、审阅和二次开发。

## What You Can Do

- 在主控台选择气象场、污染物、模拟范围和网格分辨率。
- 直接调节临时风速和风向运行单风向模拟，不覆盖保存的气象记录。
- 在主控台打开公式说明，查看高斯烟羽、衰减、多风向聚合和不同污染因子的计算参数。
- 在地图上框选区域，只模拟区域内排放源对受体点的影响。
- 通过 Excel 模板批量导入排放源和受体点。
- 查看 PM2.5、PM10、TSP、VOCs、NOx、O3 的浓度场和贡献分析。

## Screenshots

| 主控台模拟 | 受体点贡献分析 |
|---|---|
| ![主控台浓度热力图](docs/assets/screenshots/dashboard-simulation.png) | ![受体点贡献分析抽屉](docs/assets/screenshots/contribution-analysis.png) |

| 排放源管理 | 受体点管理 | 气象场管理 |
|---|---|---|
| ![排放源管理](docs/assets/screenshots/sources-management.png) | ![受体点管理](docs/assets/screenshots/receptors-management.png) | ![气象场管理](docs/assets/screenshots/meteorology-management.png) |

## Architecture

```text
frontend-vue (Vue 3 + TypeScript + Leaflet)
        |
        | HTTP JSON /api/*
        v
backend-dotnet (ASP.NET Core 9)
        |
        +-- GnnSimulation.Core  高斯烟羽模型与贡献分析
        +-- GnnSimulation.Data  EF Core + SQLite
        +-- Shapefile / Excel   地图边界与批量导入导出
```

## Data And Privacy

仓库内的 `backend/air_pollution.db` 是匿名演示数据库，只用于本地运行和功能演示。当前演示库使用少量离散工程示例源，不代表污染源需要按网格布点；网格只用于计算并输出浓度场。公开仓库不得提交真实项目名称、真实客户数据、密钥、账号凭证或未获授权的监测数据。

## Documentation

| 文档 | 说明 |
|---|---|
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | 分层架构、数据流、演进说明 |
| [docs/API.md](docs/API.md) | API 端点参考 |
| [docs/WORKFLOW.md](docs/WORKFLOW.md) | 日常开发、验证、常见陷阱 |
| [backend-dotnet/README.md](backend-dotnet/README.md) | 后端结构、配置、测试与技术决策 |
| [frontend-vue/README.md](frontend-vue/README.md) | 前端结构、状态管理、坐标与测试 |

## Verification

当前验证规模：后端 147 个 xUnit 用例、前端 83 个 Vitest 用例，并包含前端生产构建与类型检查。

```bash
./scripts/verify.sh
```

## Version

当前版本为 3.0.9，最近更新聚焦风向指针圆心坐标、数据管理批量删除、多污染因子独立计算、公式说明展示和多风向聚合一致性。详见 [CHANGELOG.md](CHANGELOG.md)。

## License

本项目采用 [MIT License](LICENSE)。

<sub>Built with [Meridian](https://github.com/lordmos/meridian)</sub>
