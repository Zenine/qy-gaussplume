# API 参考

所有 HTTP 端点一览。详细 schema 见 [OpenAPI](http://localhost:5207/openapi/v1.json)（启动后端后访问）。

**基础信息**
- 协议：HTTP
- 默认端口：5207（dev）
- JSON：**camelCase** 字段命名
- 错误格式：`{ "detail": "错误描述" }` + 标准 HTTP 状态码（404 / 400 / 500）
- 字符集：UTF-8（含中文）

## 排放源 `/api/sources`

排放源、受体点和气象场都支持固定区域隔离。列表、创建、批量创建和导入接口可传 `regionKey`：

- `nanhu`：南湖区
- `xiuzhou`：秀洲区
- `jiashan`：嘉善县
- `tongxiang`：桐乡市

未传 `regionKey` 时保留兼容口径，返回全量数据；传入非法 `regionKey` 时返回 `400`，不会退化为全量列表。历史无区域归属数据会在迁移或启动自愈时默认绑定到 `nanhu`，避免升级后页面看不到原有数据。

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/api/sources?skip=0&limit=100&regionKey=nanhu` | 列表（含污染物 include），可按区域过滤 |
| GET | `/api/sources/{id}` | 单个 |
| POST | `/api/sources?regionKey=nanhu` | 创建并绑定区域，body `EmissionSourceCreateDto` |
| POST | `/api/sources/batch?regionKey=nanhu` | 批量创建并绑定区域，body `EmissionSourceCreateDto[]` |
| PUT | `/api/sources/{id}` | 部分更新（PATCH 语义：传 null 不改；`pollutants` 非 null 整体替换） |
| DELETE | `/api/sources/{id}` | 删除（级联污染物） |
| GET | `/api/sources/pollutant-types` | 七种业务污染物元数据，包含 SO2 |
| GET | `/api/sources/marker-symbols` | 十三种图标元数据，包含排放源默认 `factory` 和受体点默认 `monitor` |
| POST | `/api/sources/{id}/pollutants` | 追加或覆盖一个污染物排放 |
| DELETE | `/api/sources/{id}/pollutants/{pid}` | 移除一个污染物 |
| GET | `/api/sources/template/{type}` | 下载 Excel 模板（type ∈ point/area/equivalent_area/line） |
| POST | `/api/sources/import/{type}?regionKey=nanhu` | 上传 Excel 导入并绑定区域，multipart/form-data `file` |

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
| GET | `/api/receptors?skip=0&limit=100&regionKey=nanhu` | 列表，可按区域过滤 |
| GET | `/api/receptors/{id}` | 单个 |
| POST | `/api/receptors?regionKey=nanhu` | 创建并绑定区域 |
| POST | `/api/receptors/batch?regionKey=nanhu` | 批量创建并绑定区域 |
| PUT | `/api/receptors/{id}` | 部分更新 |
| DELETE | `/api/receptors/{id}` | 删除 |
| GET | `/api/receptors/template` | 下载 Excel 模板 |
| POST | `/api/receptors/import?regionKey=nanhu` | 上传 Excel 导入并绑定区域 |
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
| GET | `/api/meteorology?regionKey=nanhu` | 列表，可按区域过滤 |
| GET | `/api/meteorology/{id}` | 单个 |
| POST | `/api/meteorology?regionKey=nanhu` | 创建并绑定区域 |
| POST | `/api/meteorology/batch?regionKey=nanhu` | 批量创建并绑定区域 |
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
  "precipitation": 0,
  "isActive": true
}
```

## 固定区域 `/api/regions`

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/api/regions` | 返回固定区域列表，按 `sortOrder` 排序 |

响应示例：

```json
[
  { "id": 1, "key": "nanhu", "name": "南湖区", "sortOrder": 1 },
  { "id": 2, "key": "xiuzhou", "name": "秀洲区", "sortOrder": 2 },
  { "id": 3, "key": "jiashan", "name": "嘉善县", "sortOrder": 3 },
  { "id": 4, "key": "tongxiang", "name": "桐乡市", "sortOrder": 4 }
]
```

## 模拟 `/api/simulation`

### `POST /api/simulation/run` - 单风向

**请求** `SimulationRequestDto`:

```json
{
  "meteorologyId": 1,
  "windSpeed": 3.0,            // 可选：临时覆盖气象场风速，不写回气象场
  "windDirection": 277,        // 可选：临时覆盖气象场风向，不写回气象场
  "sourceIds": null,           // null = 所有 isActive；[] = 空范围；[id] = 指定集合
  "receptorIds": null,         // 同上，供地图框选等场景使用
  "pollutantType": null,       // null = 所有污染物
  "gridResolution": 100,       // 网格分辨率 (m)
  "domainSize": 10000,         // 域大小 (m)
  "receptorHeight": 0
}
```

`sourceIds` / `receptorIds` 使用三态语义：`null` 表示未指定过滤条件，空数组表示调用方明确选择空范围，非空数组表示只模拟指定 ID。

`windSpeed` / `windDirection` 为主控台临时调参入口：传入时只影响本次单风向模拟，不修改气象场管理中的已保存记录；省略时使用 `meteorologyId` 对应气象场的保存值。

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
  "windSpeed": 3.0,                    // 未传 windSpeeds 时使用的统一风速
  "windDirections": [0, 22.5, 45, ...],  // 任意数量
  "windSpeeds": [2.45, 2.23, 2.24, ...], // 可选；与风向逐项对应
  "weights": null,                     // null = 等权
  "sourceIds": null, "receptorIds": null, "pollutantType": null,
  "gridResolution": 10, "domainSize": 10000, "receptorHeight": 0,
  "numWorkers": null,                  // null = min(CPU 核数, 风向数)
  "returnAggregatedOnly": true         // false = 详细模式返回每风向
}
```

当 `windSpeeds` 存在时，其数量必须与 `windDirections` 一致，每项必须大于 0；否则使用统一 `windSpeed`。`weights` 按风向原始顺序绑定，提供时数量必须一致、每项必须为非负有限数值且总和大于 0；聚合前自动归一化，不要求权重和恰好等于 1。

#### 风向加权 XLSX

| 方法 | 端点 | 说明 |
|---|---|---|
| GET | `/api/simulation/wind-profile/template` | 下载 72 方位模板，三列为“风向中心角度、平均风速(m/s)、加权值” |
| POST | `/api/simulation/wind-profile/import` | 上传 `.xlsx`，multipart/form-data 字段名 `file`；返回 `windDirections`、`windSpeeds`、`weights` 和权重和 |

上传文件不得超过 5 MB。导入会拒绝缺列、非数值、重复风向、超出 `[0, 360)` 的角度、非正风速、负权重和权重总和为 0 的文件，并在业务校验错误中指出行号；损坏或非 XLSX 文件只返回通用格式错误，不回传底层解析异常。

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
    { "windDirection": 0, "windSpeed": 2.45, "success": true, "concentrations": [[...]], ... },
    ...
  ]
}
```

### `GET /api/simulation/formulas` - 公式说明

返回前端公式抽屉展示所需的算法说明。污染因子参数来自后端 `PollutantProperties`，用于确认 PM2.5、PM10、SO2、NOx、CO、O3 等污染物分别使用各自的沉降、湿清除、化学衰减和温度修正参数；前端不应复制这些参数。

SO2 当前参数：重力沉降速度 0 m/s，干沉降阻力 Rb/Rc=150/400，湿清除 a=8×10⁻⁶、b=0.7，化学转化基础速率 k=4.81×10⁻⁵；化学有效速率在通用环境因子之外再乘温度增强 1.5 和湿度增强 1.3。

源类型说明中的线源公式为有限长线源积分法（FLSI）：`C_line = ∫₀ᴸ q′K(s)ds`；`segmentLength` 是兼容旧数据的字段名，只控制 Gauss-Legendre 数值积分面板的最大步长，不表示多个离散点源。

**响应** `SimulationFormulaInfoDto`:

```json
{
  "gaussianPlumeFormula": "C = Q / (2πuσyσz) × exp(...)",
  "decayFormula": "C_final = C_plume × dry × wet × chemical × temperature",
  "windAggregationFormula": "C_agg = Σ(C_wind × normalized_weight)",
  "pollutants": [
    {
      "type": "PM2.5",
      "name": "细颗粒物",
      "gravitationalSettlingVelocity": 0.0002,
      "dryResistanceRb": 100,
      "dryResistanceRc": 200,
      "wetScavengingA": 0.00001,
      "wetScavengingB": 0.8,
      "chemicalRate": 0.00002,
      "chemicalEnhanced": false,
      "chemicalTemperatureMultiplier": 1.0,
      "chemicalHumidityMultiplier": 1.0,
      "temperatureCorrected": false
    }
  ],
  "sourceTypes": [
    {
      "type": "equivalent_area",
      "name": "等效面源",
      "formula": "concentration-clamped area source",
      "notes": "使用实测浓度约束面源贡献"
    }
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
