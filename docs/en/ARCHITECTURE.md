<!-- Translation status:
Source file: docs/ARCHITECTURE.md
Source commit: (uncommitted)
Translated: 2026-06-03
Status: up-to-date
-->

# Architecture And Evolution

QY-GaussPlume is split into a Vue frontend, an ASP.NET Core API, a pure simulation core, and EF Core persistence. The boundary keeps the Gaussian plume model testable outside HTTP and database concerns.

## Overall Architecture

The browser talks to `frontend-vue`, which proxies `/api/*` to `backend-dotnet`. The backend coordinates source loading, grid building, simulation, contribution analysis, shapefile access, and Excel import/export.

## Four Backend Layers

The API layer handles HTTP and DTOs, services orchestrate data and algorithms, Core contains pure atmospheric calculations, and Data maps SQLite entities with EF Core.

## Data Flow: Single-Wind Simulation

`POST /api/simulation/run` loads meteorology, sources, and receptors; builds the grid; computes source fields; aggregates pollutant fields; and returns receptor contribution rankings.

## Data Flow: Multi-Wind Parallel

`POST /api/simulation/run_parallel` builds one shared context and evaluates each wind direction in parallel, then combines concentration fields and receptor contributions by weight.

## Frontend Layers

The frontend separates API clients, shared types, Pinia stores, routed views, map components, composables, and pure utilities for color scales, coordinates, selection, download, and error handling.

## 17-Stage Evolution History

The project moved from Python/FastAPI to ASP.NET Core 9 and Vue 3 through staged backend, core algorithm, frontend, map, dashboard, and management-page improvements.

## Key Tradeoffs

The design keeps Core free of API/Data dependencies, avoids default-value traps in EF Core, loads large shapefiles only on demand, and uses .NET threading for multi-wind parallelism.
