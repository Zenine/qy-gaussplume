# API 参考

所有 HTTP 端点一览。详细 schema 见 [OpenAPI](http://localhost:5207/openapi/v1.json)（启动后端后访问）。

**基础信息**
- 协议：HTTP
- 默认端口：5207（dev）
- JSON：**camelCase** 字段命名
- 错误格式：`{ "detail": "错误描述" }` + 标准 HTTP 状态码（404 / 400 / 500）
- 字符集：UTF-8（含中文）

## 排放源 `/api/sources`

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/api/sources?skip=0&limit=100` | 列表（含污染物 include） |
| GET | `/api/sources/{id}` | 单个 |
| POST | `/api/sources` | 创建，body `EmissionSourceCreateDto` |
| POST | `/api/sources/batch` | 批量，body `EmissionSourceCreateDto[]` |
| PUT | `/api/sources/{id}` | 部分更新（PATCH 语义：传 null 不改；`pollutants` 非 null 整体替换） |
| DELETE | `/api/sources/{id}` | 删除（级联污染物） |
| GET | `/api/sources/pollutant-types` | 六种污染物元数据 |
| GET | `/api/sources/marker-symbols` | 十二种图标元数据 |
| POST | `/api/sources/{id}/pollutants` | 追加或覆盖一个污染物排放 |
| DELETE | `/api/sources/{id}/pollutants/{pid}` | 移除一个污染物 |
| GET | `/api/sources/template/{type}` | 下载 Excel 模板（type ∈ point/area/equivalent_area/line） |
| POST | `/api/sources/import/{type}` | 上传 Excel 导入，multipart/form-data `file` |

**`EmissionSourceCreateDto` 关键字段**：

```json
{
  "name": "...",
  "sourceType": "point|area|equivalent_area|line",
  "latitude": 39.9,
  "longitude": 116.4,
  "height": 50,
  "temperature": 400, "velocity": 15, "diameter": 2,
  "areaLength": 100, "areaWidth": 100, "areaHeight": 10, "areaTemperature": 300,
  "startLat": ..., "startLon": ..., "endLat": ..., "endLon": ...,
  "lineWidth": 10, "lineHeight": 1, "lineSegmentLength": 10,
  "markerSymbol": "factory",
  "markerColor": "#FF5722",
  "isActive": true,
  "pollutants": [
    { "pollutantType": "PM2.5", "emissionRate": 1.5, "concentration": null }
  ]
}
```

对等效面源：`emissionRate=0` + `concentration=实测值`，后端自动调 `CalculateEquivalentEmissionRate` 反算。

## 受体点 `/api/receptors`

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/api/receptors?skip=0&limit=100` | 列表 |
| GET | `/api/receptors/{id}` | 单个 |
| POST | `/api/receptors` | 创建 |
| POST | `/api/receptors/batch` | 批量 |
| PUT | `/api/receptors/{id}` | 部分更新 |
| DELETE | `/api/receptors/{id}` | 删除 |
| GET | `/api/receptors/template` | 下载 Excel 模板 |
| POST | `/api/receptors/import` | 上传 Excel 导入 |
| POST | `/api/receptors/export` | 导出所选 id 的 xlsx（body：`int[]`） |

```json
{
  "name": "学校",
  "latitude": 39.9,
  "longitude": 116.4,
  "height": 1.5,
  "markerSymbol": "monitor",
  "markerColor": "#2196F3",
  "isActive": true
}
```

## 气象场 `/api/meteorology`

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/api/meteorology` | 列表 |
| GET | `/api/meteorology/{id}` | 单个 |
| POST | `/api/meteorology` | 创建 |
| POST | `/api/meteorology/batch` | 批量 |
| PUT | `/api/meteorology/{id}` | 部分更新 |
| DELETE | `/api/meteorology/{id}` | 删除 |

```json
{
  "name": "冬季北风",
  "windSpeed": 3.0,
  "windDirection": 0,
  "boundaryLayerHeight": 1000,
  "stabilityClass": "D",
  "temperature": 293.15,
  "humidity": 50,
  "cloudCover": 0,
  "precipitation": 0
}
```

## 模拟 `/api/simulation`

### `POST /api/simulation/run` - 单风向

**请求** `SimulationRequestDto`:

```json
{
  "meteorologyId": 1,
  "sourceIds": null,           // null = 所有 isActive；[] = 空范围；[id] = 指定集合
  "receptorIds": null,         // 同上，供地图框选等场景使用
  "pollutantType": null,       // null = 所有污染物
  "gridResolution": 100,       // 网格分辨率 (m)
  "domainSize": 10000,         // 域大小 (m)
  "receptorHeight": 0
}
```

`sourceIds` / `receptorIds` 使用三态语义：`null` 表示未指定过滤条件，空数组表示调用方明确选择空范围，非空数组表示只模拟指定 ID。

**响应** `SimulationResultDto`:

```json
{
  "concentrations": [[...], ...],      // 2D [lat][lon] 网格
  "gridLat": [...],                    // 1D 网格纬度
  "gridLon": [...],
  "contributions": [                   // 每个源的统计
    { "sourceId": 1, "sourceName": "...", "totalConcentration": 0, "maxConcentration": 0, "pollutants": [...] }
  ],
  "receptorContributions": {
    "受体名称": {
      "PM2.5": [
        { "sourceId": 1, "sourceName": "...", "concentration": 0, "pollutant": "PM2.5", "percentage": 0 }
      ]
    }
  },
  "pollutantConcentrations": {         // 每种污染物独立的网格（可选）
    "PM2.5": [[...]], "NOx": [[...]]
  },
  "availablePollutants": ["PM2.5", "NOx"]
}
```

**错误**:
- 404: 气象场未找到
- 400: 没有可用的排放源

### `POST /api/simulation/run_parallel` - 多风向并行

**请求** `ParallelSimulationRequestDto`:

```json
{
  "meteorologyId": 1,
  "windSpeed": 3.0,                    // 覆盖气象场风速
  "windDirections": [0, 22.5, 45, ...],  // 任意数量
  "weights": null,                     // null = 等权
  "sourceIds": null, "receptorIds": null, "pollutantType": null,
  "gridResolution": 10, "domainSize": 10000, "receptorHeight": 0,
  "numWorkers": null,                  // null = min(CPU 核数, 风向数)
  "returnAggregatedOnly": true         // false = 详细模式返回每风向
}
```

**响应** `ParallelSimulationResultDto`:

聚合模式（默认 / 内存超 0.5 GB 强制）：

```json
{
  "success": true,
  "mode": "aggregated",
  "totalWindDirections": 72,
  "successfulSimulations": 72,
  "failedSimulations": 0,
  "errors": null,
  "numWorkersUsed": 8,
  "computationTimeSeconds": 15.3,
  "speedupFactor": 281.7,
  "concentrations": [[...]],
  "gridLat": [...], "gridLon": [...],
  "pollutantConcentrations": {...},
  "availablePollutants": [...],
  "receptorContributions": {...}
}
```

详细模式（`returnAggregatedOnly=false`）：

```json
{
  "success": true,
  "mode": "detailed",
  "results": [
    { "windDirection": 0, "success": true, "concentrations": [[...]], ... },
    ...
  ]
}
```

## 标记配置 `/api/config`

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/api/config` | 全部标记配置 |
| GET | `/api/config/{type}` | 按 type 查询 |
| POST | `/api/config` | 创建（type 唯一，冲突返回 400） |
| PUT | `/api/config/{type}` | 更新 |

## 地图 `/api/map`

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/api/map/geojson?force=false` | 县级边界 GeoJSON；`force=true` 强制加载（约 100 MB） |
| GET | `/api/map/bounds` | 地图边界 WGS84 `{ minLat, minLon, maxLat, maxLon }` |
| GET | `/api/map/info` | 元信息：CRS / featureCount / columns / bounds |

**默认 `/geojson` 返回空** `{"type":"FeatureCollection","features":[]}`，对齐 Python 原版 `LOAD_SHP_BY_DEFAULT=False`。生产环境如需加载，改 `appsettings.json` 的 `Shapefile:LoadByDefault`。

## 在浏览器中探索 API

开发环境启动后打开：
```
http://localhost:5207/openapi/v1.json
```

可以直接喂给 Postman/Bruno/OpenAPI Generator。
