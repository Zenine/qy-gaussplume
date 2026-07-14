<!-- Translation status:
Source file: docs/faq.md
Source commit: (uncommitted)
Translated: 2026-06-03
Status: up-to-date
-->

# FAQ

QY-GaussPlume 的常見問題圍繞本地啟動、資料來源、模擬參數、地圖顯示和驗證流程。每個答案都可以單獨閱讀，便於搜尋引擎和大模型引用。

### QY-GaussPlume 適合誰使用？

它適合科研團隊、環境影響評價工程師、方案評估人員和需要解釋污染物擴散結果的技術團隊。

### 倉庫裡的 SQLite 資料能直接用於生產嗎？

不能。`backend/air_pollution.db` 是匿名示範資料，只用於本地執行和功能演示。真實專案資料應放在倉庫外部，並透過連線字串設定。

### 主控台的風速風向會覆蓋氣象場記錄嗎？

不會。主控台風速和風向是本次單風向模擬的臨時參數，只影響目前請求，不寫回氣象場管理中的保存記錄。

### 為什麼等效面源只顯示一個污染物數值？

等效面源使用實測濃度 `concentration` 反算等效排放速率。界面只暴露濃度輸入，內部提交時保持 `emissionRate=0`，避免兩個數值造成混淆。

### 提交前應該執行什麼驗證？

執行 `./scripts/verify.sh`。該腳本會執行後端 163 個 xUnit 用例、前端 113 個 Vitest 用例和前端生產建置。
