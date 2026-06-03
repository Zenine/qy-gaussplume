---
titleTemplate: ':title'
layout: home
hero:
  name: QY-GaussPlume
  text: 讓大氣擴散評估成為可操作的地圖工作流
  tagline: 面向科研與工程評估團隊，把排放源、受體點、氣象場和貢獻分析整合到一個可驗證的平台。
  image:
    src: /hero.svg
    alt: QY-GaussPlume
  actions:
    - theme: brand
      text: 快速開始
      link: /zh-TW/quick-start
    - theme: alt
      text: 查看架構
      link: /zh-TW/ARCHITECTURE
features:
  - icon:
      src: /icons/globe.svg
    title: 在地圖上完成影響判斷
    details: 在同一工作台查看排放源、受體點、模擬範圍和濃度熱力圖。
  - icon:
      src: /icons/settings.svg
    title: 快速試算風場
    details: 直接使用臨時風速和風向執行，不覆蓋已儲存氣象場。
  - icon:
      src: /icons/bar-chart.svg
    title: 解釋受體點貢獻
    details: 按污染物輸出各受體點的來源貢獻排名。
  - icon:
      src: /icons/file-text.svg
    title: 批量管理資料
    details: 透過 Excel 模板維護排放源和受體點。
  - icon:
      src: /icons/check-circle.svg
    title: 帶驗證交付
    details: 內建後端、前端和建置驗證，目前覆蓋 208 個自動化測試。
---

<!-- Translation status:
Source file: docs/index.md
Source commit: (uncommitted)
Translated: 2026-06-03
Status: up-to-date
-->

# QY-GaussPlume

QY-GaussPlume 是面向科研和工程評估的大氣污染物擴散模擬平台。它用 ASP.NET Core 9、Vue 3、Leaflet 和高斯煙羽模型，把排放源管理、氣象場選擇、地圖模擬和貢獻分析串成可執行、可驗證、可交付的工作流。

## 適用場景

- 環評專案中快速比較不同排放源和風場條件。
- 工程方案討論時查看污染物擴散範圍和受體點影響。
- 科研或教學中重現實驗資料、測試模型參數和解釋貢獻排名。

## 繼續閱讀

- [快速開始](quick-start.md)
- [架構與演進](ARCHITECTURE.md)
- [API 參考](API.md)
- [開發工作流](WORKFLOW.md)
- [FAQ](faq.md)
