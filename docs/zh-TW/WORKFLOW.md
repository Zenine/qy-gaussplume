<!-- Translation status:
Source file: docs/WORKFLOW.md
Source commit: (uncommitted)
Translated: 2026-06-03
Status: up-to-date
-->

# 開發工作流

QY-GaussPlume 使用倉庫內腳本處理本地啟動、停止、測試和完整驗證。應優先使用專案腳本，而不是本地 Git hook。

## 起停

使用 `./scripts/start.sh` 同時啟動後端與前端，使用 `./scripts/stop.sh` 停止。後端埠為 5207，前端埠為 5173。

## 跑測試

提交前執行 `./scripts/verify.sh`。它會執行後端測試、前端測試和前端生產建置。

## 常見變更模板

新增資料實體、API、後端演算法、前端頁面、主控台氣象控制或管理頁時，先更新測試，並同步 DTO、型別、文件和工作流說明。

## 已知陷阱

避免對沒有資料庫預設值的欄位使用 EF Core `HasDefaultValue`，處理歷史 NULL 值，在 jsdom 測試中 stub Canvas，避免並發 `npm install`，並同步後端埠設定。

## 驗證全棧正常

測試通過後啟動應用，使用 `curl` 檢查後端和 Vite proxy endpoint 是否正確連通。

## 其他常用命令

常用命令包括匯出 OpenAPI JSON、查看前端 bundle 體積、檢查 SQLite table、追蹤執行時日誌。

## 執行時日誌

`scripts/start.sh` 會把後端日誌寫入 `.run/backend.log`，前端日誌寫入 `.run/frontend.log`，每次啟動會輪轉舊日誌。

## 問題回饋

如需回報 bug、提出功能建議或詢問問題，請聯絡維護者或建立 GitHub issue。
