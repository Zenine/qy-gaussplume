# C 语言平台集成说明

本文面向需要把“长三院源贡献计算模拟平台”集成到现有 C 语言开发平台的同事。当前项目不是 C 源码库，而是一个可独立运行的 Web/API 服务；推荐集成方式是 **C 平台通过 HTTP/JSON 调用本项目后端 API**。

## 1. 集成结论

### 推荐方式：服务化集成

```text
C 语言平台 / 既有业务系统
        │ HTTP + JSON
        ▼
长三院源贡献计算模拟服务（本项目后端 ASP.NET Core）
        │
        ▼
返回浓度场、污染物分场、受体点源贡献排名等结果
```

这种方式不需要把 C# 后端或 Vue 前端直接编译进 C 工程，适合现有 C 平台快速接入，也便于后续独立升级模型和界面。

### 不推荐直接内嵌源码

本项目当前技术栈如下：

- 后端：ASP.NET Core 9 / C# / EF Core / SQLite
- 前端：Vue 3 / TypeScript / Vite / Element Plus / Leaflet
- 通信：HTTP API + JSON

因此不能把当前源码“直接作为 C 代码”编译进 C 平台。如果必须做纯 C 内嵌，需要单独把高斯烟羽模型、网格构建、源贡献计算和数据结构移植为 C，并重新做数值一致性验证。

## 2. 部署与启动

### 2.1 后端服务

在源码根目录执行：

```bash
cd backend-dotnet
dotnet run --project GnnSimulation.Api
```

默认本地开发服务常用地址为：

```text
http://127.0.0.1:5207
```

如果部署到服务器，请由运维或集成方固定一个内网地址，例如：

```text
http://10.0.0.25:5207
```

C 平台只需要能访问这个地址即可。

### 2.2 前端页面（可选）

如果对方平台已有自己的界面，可以不集成 Vue 前端，只调用后端 API。

需要使用本项目页面时：

```bash
cd frontend-vue
npm install
npm run dev
```

生产部署可使用：

```bash
npm run build
```

## 3. C 平台调用方式

C 平台建议使用成熟 HTTP 客户端库，例如：

- libcurl
- 平台已有 HTTP Client
- 自研 HTTP/JSON 网关

请求头建议统一使用：

```http
Content-Type: application/json
Accept: application/json
```

## 4. 关键 API 说明

> 说明：以下为集成时最常用的接口口径。具体字段可参考 `docs/API.md` 和前端 `frontend-vue/src/api/` 下的封装。

### 4.1 查询排放源

```http
GET /api/sources?skip=0&limit=1000&regionKey=nanhu
```

用途：获取指定区域内排放源列表。前端和模拟默认只使用启用状态 `isActive=true` 的源参与地图显示与计算。

### 4.2 查询受体点

```http
GET /api/receptors?skip=0&limit=1000&regionKey=nanhu
```

用途：获取指定区域内受体点列表。前端和模拟默认只使用启用状态 `isActive=true` 的受体点参与地图显示与计算。

### 4.3 查询气象场

```http
GET /api/meteorology?skip=0&limit=1000&regionKey=nanhu
```

用途：获取气象场列表。主控台只展示启用的气象场。

### 4.4 运行单风向模拟

```http
POST /api/simulation/run
Content-Type: application/json
```

请求示例：

```json
{
  "meteorologyId": 1,
  "sourceIds": [1, 2, 3],
  "receptorIds": [1, 2],
  "pollutantType": "PM2.5",
  "windSpeed": 3.0,
  "windDirection": 45,
  "gridResolution": 100,
  "domainSize": 5000,
  "receptorHeight": 0
}
```

字段说明：

| 字段 | 说明 |
|---|---|
| `meteorologyId` | 气象场 ID，必填 |
| `sourceIds` | 指定参与模拟的排放源 ID；不传表示由后端按默认范围处理 |
| `receptorIds` | 指定参与贡献计算的受体点 ID；空数组表示明确不选择受体点 |
| `pollutantType` | 计算污染物；不传表示计算全部污染物 |
| `windSpeed` | 临时风速，单位 m/s |
| `windDirection` | 临时来风方向，单位度 |
| `gridResolution` | 浓度场网格分辨率，单位 m |
| `domainSize` | 模拟范围，单位 m，例如 5000 表示 5 km |
| `receptorHeight` | 模拟高度，单位 m |

返回结果主要包含：

| 字段 | 说明 |
|---|---|
| `concentrations` | 当前展示污染物或总浓度场二维数组 |
| `gridLat` / `gridLon` | 浓度场网格坐标 |
| `pollutantConcentrations` | 分污染物浓度场 |
| `availablePollutants` | 本次结果包含的污染物列表 |
| `receptorContributions` | 受体点维度的污染源贡献排名数据 |

### 4.5 运行多风向模拟

```http
POST /api/simulation/run_parallel
Content-Type: application/json
```

请求示例：

```json
{
  "meteorologyId": 1,
  "sourceIds": [1, 2, 3],
  "receptorIds": [1, 2],
  "pollutantType": "PM2.5",
  "windSpeed": 3.0,
  "windDirections": [0, 45, 90, 135, 180, 225, 270, 315],
  "windSpeeds": [2.45, 2.23, 2.24, 2.17, 1.92, 2.11, 1.87, 1.84],
  "weights": [0.125, 0.125, 0.125, 0.125, 0.125, 0.125, 0.125, 0.125],
  "gridResolution": 100,
  "domainSize": 5000,
  "receptorHeight": 0
}
```

多风向用于按多个来风方向加权聚合，不表示污染源方向。`windSpeeds` 可逐项给出每个风向的平均风速，省略时统一使用 `windSpeed`；`weights` 可不传，不传时按等权处理，提供时数量必须与风向一致、不得为负且总和必须大于 0。Web 前端还可通过 `/api/simulation/wind-profile/template` 和 `/api/simulation/wind-profile/import` 下载、解析三列 XLSX 风频表，上传上限为 5 MB。

## 5. C 语言调用示例（libcurl）

下面示例仅演示基本调用方式，实际项目中建议封装错误处理、超时、日志和 JSON 解析。

```c
#include <stdio.h>
#include <string.h>
#include <curl/curl.h>

static size_t write_cb(void *contents, size_t size, size_t nmemb, void *userp) {
    size_t total = size * nmemb;
    fwrite(contents, size, nmemb, stdout);
    return total;
}

int main(void) {
    CURL *curl = curl_easy_init();
    if (!curl) return 1;

    const char *url = "http://127.0.0.1:5207/api/simulation/run";
    const char *json =
        "{"
        "\"meteorologyId\":1,"
        "\"sourceIds\":[1,2,3],"
        "\"receptorIds\":[1,2],"
        "\"pollutantType\":\"PM2.5\","
        "\"windSpeed\":3.0,"
        "\"windDirection\":45,"
        "\"gridResolution\":100,"
        "\"domainSize\":5000,"
        "\"receptorHeight\":0"
        "}";

    struct curl_slist *headers = NULL;
    headers = curl_slist_append(headers, "Content-Type: application/json");
    headers = curl_slist_append(headers, "Accept: application/json");

    curl_easy_setopt(curl, CURLOPT_URL, url);
    curl_easy_setopt(curl, CURLOPT_HTTPHEADER, headers);
    curl_easy_setopt(curl, CURLOPT_POSTFIELDS, json);
    curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, write_cb);
    curl_easy_setopt(curl, CURLOPT_TIMEOUT, 60L);

    CURLcode res = curl_easy_perform(curl);
    if (res != CURLE_OK) {
        fprintf(stderr, "curl_easy_perform failed: %s\n", curl_easy_strerror(res));
    }

    curl_slist_free_all(headers);
    curl_easy_cleanup(curl);
    return res == CURLE_OK ? 0 : 1;
}
```

## 6. 集成建议

1. **优先固定服务地址**：C 平台配置后端 API 地址，不要把地址硬编码到业务逻辑里。
2. **接口调用加超时**：模拟计算可能比普通查询慢，建议请求超时至少 60 秒起步，按实际数据量调整。
3. **保存请求参数**：平台侧建议保存每次模拟的请求 JSON，方便复现和审计。
4. **先接单风向，再接多风向**：单风向参数更少，适合先验证数据链路。
5. **不要直接改 SQLite**：排放源、受体点、气象场建议通过 API 或平台导入流程维护，避免破坏区域关联和启用状态。
6. **坐标口径**：业务数据使用 WGS84 经纬度；前端地图叠加时会做高德 GCJ02 转换。

## 7. 验证方式

集成前建议先在本项目根目录执行：

```bash
./scripts/verify.sh
```

当前项目验证规模：

- 后端自动化测试：192 个
- 前端自动化测试：123 个

如果后续 C 平台只调用 API，也建议保留一组固定请求 JSON 和固定返回摘要，作为平台集成冒烟测试。
