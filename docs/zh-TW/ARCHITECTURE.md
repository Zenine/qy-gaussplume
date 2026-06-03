<!-- Translation status:
Source file: docs/ARCHITECTURE.md
Source commit: (uncommitted)
Translated: 2026-06-03
Status: up-to-date
-->

# 架構與演進

QY-GaussPlume 分為 Vue 前端、ASP.NET Core API、純模擬 Core 和 EF Core 持久化。這個邊界讓高斯煙羽模型可以脫離 HTTP 和資料庫單獨測試。

## 總體架構

瀏覽器連到 `frontend-vue`，`/api/*` 代理到 `backend-dotnet`。後端負責資料載入、網格構建、模擬、貢獻分析、Shapefile 與 Excel 匯入匯出。

## 四層後端

API 層處理 HTTP 和 DTO，服務層編排資料與演算法，Core 層承載大氣計算，Data 層以 EF Core 映射 SQLite。

## 資料流：一次單風向模擬

`POST /api/simulation/run` 載入氣象場、排放源和受體點，建立網格，計算各來源濃度場，聚合污染物場並返回受體點貢獻排名。

## 資料流：多風向並行

`POST /api/simulation/run_parallel` 建立共享上下文，並行評估每個風向，再按權重合併濃度場與受體點貢獻。

## 前端分層

前端分離 API client、型別、Pinia store、路由 view、地圖 component、composable，以及色階、座標、選擇、下載和錯誤處理 utility。

## 11 階段演進史

專案從 Python/FastAPI 逐步遷移到 ASP.NET Core 9 與 Vue 3，涵蓋後端、Core 演算法、前端、地圖、主控台和管理頁改善。

## 關鍵權衡

設計讓 Core 不依賴 API/Data，避開 EF Core 預設值陷阱，大型 Shapefile 僅按需載入，並使用 .NET 執行緒並行處理多風向模擬。
