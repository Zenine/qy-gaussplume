<!-- Translation status:
Source file: docs/API.md
Source commit: (uncommitted)
Translated: 2026-06-03
Status: up-to-date
-->

# API 參考

QY-GaussPlume 在 `/api/*` 下提供 HTTP JSON 端點。JSON 欄位使用 camelCase，錯誤以 `{ "detail": "..." }` 搭配標準 HTTP 狀態碼返回。

## 排放源 `/api/sources`

排放源 API 支援列表、取得、建立、批量建立、更新、刪除、污染物 metadata、標記符號、Excel 模板與 Excel 匯入。等效面源會提交 `emissionRate=0` 和實測 `concentration`。

## 受體點 `/api/receptors`

受體點 API 支援 CRUD、批量建立、Excel 模板下載、Excel 匯入、所選項匯出，以及透過逐項刪除完成所選項刪除。

## 氣象場 `/api/meteorology`

氣象場 API 管理已儲存的風速、風向、穩定度、邊界層高度、溫度、濕度、雲量和降水量。

## 模擬 `/api/simulation`

`POST /api/simulation/run` 執行單風向模擬。可選的 `windSpeed` 與 `windDirection` 只會在目前請求中臨時覆蓋已儲存氣象場。`POST /api/simulation/run_parallel` 執行加權多風向模擬。

## 標記設定 `/api/config`

標記設定 API 管理地圖 UI 使用的排放源與受體點樣式。

## 地圖 `/api/map`

地圖 API 提供可選 GeoJSON 邊界載入、地圖範圍和 Shapefile metadata。大型 Shapefile 預設不載入。

## 在瀏覽器中探索 API

啟動後端後打開 `http://localhost:5207/openapi/v1.json` 可查看 OpenAPI 文件。
