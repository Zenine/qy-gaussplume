<!-- Translation status:
Source file: README.md
Source commit: (uncommitted)
Translated: 2026-06-03
Status: up-to-date
-->

> **語言 / Language**: [简体中文](README.md) · [English](README.en.md) · [日本語](README.ja.md) · **繁體中文**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-9.0-512bd4?style=flat-square)](backend-dotnet/)
[![Vue](https://img.shields.io/badge/Vue-3-42b883?style=flat-square)](frontend-vue/)
[![Powered by Meridian](https://img.shields.io/badge/Powered%20by-Meridian-8b5cf6?style=flat-square)](https://github.com/lordmos/meridian)

# QY-GaussPlume（清源高斯煙羽擴散模擬平台）

QY-GaussPlume 是給科研團隊、環評工程師和方案評估人員使用的大氣污染物擴散模擬平台。它把排放源、受體點、氣象場和地圖視覺化放在同一個工作台裡，讓團隊可以快速比較污染擴散影響、受體貢獻和不同風場條件下的工程方案。

## Quick Start

```bash
git clone git@github.com:Zenine/qy-gaussplume.git
cd qy-gaussplume
./scripts/start.sh
```

打開 <http://localhost:5173>。前端會把 `/api/*` 代理到後端 <http://localhost:5207>。

提交前請執行完整驗證入口：

```bash
./scripts/verify.sh
```

## Why It Matters

- 讓污染擴散評估從腳本試算變成可操作的地圖工作流。
- 支援點源、面源、線源和等效面源，適合工程現場常見資料形態。
- 支援單風向和多風向加權並行模擬，便於做風場敏感性分析。
- 對每個受體點輸出污染源貢獻排名，幫助解釋影響來自哪裡。
- 內建匿名示範資料與完整測試入口，便於交付、審閱和二次開發。

## What You Can Do

- 在主控台選擇氣象場、污染物、模擬範圍和網格解析度。
- 直接調整臨時風速和風向執行單風向模擬，不覆蓋儲存的氣象記錄。
- 在地圖上框選區域，只模擬區域內排放源對受體點的影響。
- 透過 Excel 模板批量匯入排放源和受體點。
- 查看 PM2.5、PM10、TSP、VOCs、NOx、O3 的濃度場和貢獻分析。

## Screenshots

| 主控台模擬 | 受體點貢獻分析 |
|---|---|
| ![主控台濃度熱力圖](docs/assets/screenshots/dashboard-simulation.png) | ![受體點貢獻分析抽屜](docs/assets/screenshots/contribution-analysis.png) |

| 排放源管理 | 受體點管理 | 氣象場管理 |
|---|---|---|
| ![排放源管理](docs/assets/screenshots/sources-management.png) | ![受體點管理](docs/assets/screenshots/receptors-management.png) | ![氣象場管理](docs/assets/screenshots/meteorology-management.png) |

## Architecture

```text
frontend-vue (Vue 3 + TypeScript + Leaflet)
        |
        | HTTP JSON /api/*
        v
backend-dotnet (ASP.NET Core 9)
        |
        +-- GnnSimulation.Core  高斯煙羽模型與貢獻分析
        +-- GnnSimulation.Data  EF Core + SQLite
        +-- Shapefile / Excel   地圖邊界與批量匯入匯出
```

## Data And Privacy

倉庫內的 `backend/air_pollution.db` 是匿名示範資料庫，只用於本地執行和功能展示。公開倉庫不得提交真實專案名稱、真實客戶資料、密鑰、帳號憑證或未獲授權的監測資料。

## Documentation

| 文件 | 說明 |
|---|---|
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | 分層架構、資料流、演進說明 |
| [docs/API.md](docs/API.md) | API 端點參考 |
| [docs/WORKFLOW.md](docs/WORKFLOW.md) | 日常開發、驗證、常見陷阱 |
| [backend-dotnet/README.md](backend-dotnet/README.md) | 後端結構、設定、測試與技術決策 |
| [frontend-vue/README.md](frontend-vue/README.md) | 前端結構、狀態管理、座標與測試 |

## Verification

目前驗證規模：後端 138 個 xUnit 用例、前端 71 個 Vitest 用例，並包含前端生產建置與型別檢查。

```bash
./scripts/verify.sh
```

## Version

目前版本為 3.0.5，最近更新聚焦主界面氣象控制風向指針偏移修復。詳見 [CHANGELOG.md](CHANGELOG.md)。

## License

本專案採用 [MIT License](LICENSE)。

<sub>Built with [Meridian](https://github.com/lordmos/meridian)</sub>
