<!-- Translation status:
Source file: docs/API.md
Source commit: (uncommitted)
Translated: 2026-06-03
Status: up-to-date
-->

# API Reference

QY-GaussPlume exposes HTTP JSON endpoints under `/api/*`. JSON fields use camelCase and errors return `{ "detail": "..." }` with standard HTTP status codes.

## Emission Sources `/api/sources`

Source endpoints support list, get, create, batch create, update, delete, pollutant metadata, marker symbols, Excel templates, and Excel import. Equivalent-area sources submit `emissionRate=0` with measured `concentration`.

## Receptors `/api/receptors`

Receptor endpoints support CRUD, batch create, Excel template download, Excel import, selected export, and selected deletion through repeated delete requests.

## Meteorology `/api/meteorology`

Meteorology endpoints manage saved wind speed, wind direction, stability class, boundary layer height, temperature, humidity, cloud cover, and precipitation.

## Simulation `/api/simulation`

`POST /api/simulation/run` performs a single-wind simulation. Optional `windSpeed` and `windDirection` override saved meteorology only for the current request. `POST /api/simulation/run_parallel` performs weighted multi-direction simulation.

## Marker Config `/api/config`

Marker configuration endpoints manage source and receptor marker styles used by the map UI.

## Map `/api/map`

Map endpoints expose optional GeoJSON boundary loading, map bounds, and shapefile metadata. Large shapefiles are not loaded by default.

## Explore API In Browser

Start the backend and open `http://localhost:5207/openapi/v1.json` for the OpenAPI document.
