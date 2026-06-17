<!-- Translation status:
Source file: README.md
Source commit: (uncommitted)
Translated: 2026-06-03
Status: up-to-date
-->

> **Language**: [简体中文](README.md) · **English** · [日本語](README.ja.md) · [繁體中文](README.zh-TW.md)

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-9.0-512bd4?style=flat-square)](backend-dotnet/)
[![Vue](https://img.shields.io/badge/Vue-3-42b883?style=flat-square)](frontend-vue/)
[![Powered by Meridian](https://img.shields.io/badge/Powered%20by-Meridian-8b5cf6?style=flat-square)](https://github.com/lordmos/meridian)

# QY-GaussPlume

QY-GaussPlume is an air-pollution dispersion simulation platform for research teams, environmental assessment engineers, and scenario evaluators. It brings emission sources, receptors, meteorology, and map visualization into one workspace so teams can compare dispersion impact, receptor contribution, and engineering options under different wind conditions.

## Quick Start

```bash
git clone git@github.com:Zenine/qy-gaussplume.git
cd qy-gaussplume
./scripts/start.sh
```

Open <http://localhost:5173>. The frontend proxies `/api/*` to <http://localhost:5207>.

Run the full verification entrypoint before committing:

```bash
./scripts/verify.sh
```

## Why It Matters

- Turns dispersion assessment from script-based trials into an operable map workflow.
- Supports point, area, line, and equivalent-area sources for common field data.
- Supports single-wind and weighted multi-wind parallel simulations for sensitivity analysis.
- Produces source contribution rankings for each receptor so teams can explain where impact comes from.
- Includes anonymized demo data and a full verification entrypoint for delivery and review.

## What You Can Do

- Choose meteorology, pollutant, simulation range, and grid resolution from the dashboard.
- Open the formula drawer to inspect Gaussian plume, decay, multi-wind aggregation, and pollutant-specific parameters.
- Adjust temporary wind speed and direction for a single-wind run without overwriting saved meteorology.
- Draw a rectangle on the map and simulate only the selected sources and receptors.
- Batch import emission sources and receptors through Excel templates.
- Inspect concentration fields and contribution analysis for PM2.5, PM10, TSP, VOCs, NOx, and O3.

## Screenshots

| Dashboard simulation | Receptor contribution |
|---|---|
| ![Dashboard concentration heatmap](docs/assets/screenshots/dashboard-simulation.png) | ![Receptor contribution drawer](docs/assets/screenshots/contribution-analysis.png) |

| Source management | Receptor management | Meteorology management |
|---|---|---|
| ![Source management](docs/assets/screenshots/sources-management.png) | ![Receptor management](docs/assets/screenshots/receptors-management.png) | ![Meteorology management](docs/assets/screenshots/meteorology-management.png) |

## Architecture

```text
frontend-vue (Vue 3 + TypeScript + Leaflet)
        |
        | HTTP JSON /api/*
        v
backend-dotnet (ASP.NET Core 9)
        |
        +-- GnnSimulation.Core  Gaussian plume model and contribution analysis
        +-- GnnSimulation.Data  EF Core + SQLite
        +-- Shapefile / Excel   map boundary and batch import/export
```

## Data And Privacy

The repository includes `backend/air_pollution.db` as anonymized demo data for local use and feature demonstrations. Do not commit real project names, customer data, secrets, account credentials, or unauthorized monitoring data.

## Documentation

| Document | Description |
|---|---|
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Layered architecture, data flow, and evolution notes |
| [docs/API.md](docs/API.md) | API endpoint reference |
| [docs/WORKFLOW.md](docs/WORKFLOW.md) | Development workflow, verification, and pitfalls |
| [backend-dotnet/README.md](backend-dotnet/README.md) | Backend structure, configuration, tests, and decisions |
| [frontend-vue/README.md](frontend-vue/README.md) | Frontend structure, state, coordinates, and tests |

## Verification

The current verification suite contains 147 backend xUnit tests, 83 frontend Vitest tests, and a production frontend build with type checking.

```bash
./scripts/verify.sh
```

## Version

The current version is 3.0.9. The latest update focuses on the wind-rose pointer, batch deletion in data management, independent pollutant calculations, formula visibility, and multi-wind aggregation consistency. See [CHANGELOG.md](CHANGELOG.md).

## License

This project is licensed under the [MIT License](LICENSE).

<sub>Built with [Meridian](https://github.com/lordmos/meridian)</sub>
