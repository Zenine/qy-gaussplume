<!-- Translation status:
Source file: docs/quick-start.md
Source commit: (uncommitted)
Translated: 2026-06-03
Status: up-to-date
-->

# 快速開始

QY-GaussPlume 可以在本機用一個腳本啟動完整前後端環境，適合先體驗主控台模擬、資料管理和貢獻分析，再進入二次開發。

## 三步上手

1. 準備 .NET SDK 9.0.x 和 Node.js 20+。
2. 複製倉庫並安裝前端依賴。
3. 執行啟動腳本並打開瀏覽器。

```bash
git clone git@github.com:Zenine/qy-gaussplume.git
cd qy-gaussplume
cd frontend-vue && npm install --registry=https://registry.npmmirror.com && cd ..
./scripts/start.sh
```

## 一句話模板

如果你想讓 AI 助手繼續開發這個專案，可以使用：

```text
QY-GaussPlume 的源碼在 /Users/zeninexu/github/未命名文件夹/qy-gaussplume。請讀 QUICK_START.md，然後向我提問。沒有問題就開始工作。
```

## 驗證

```bash
./scripts/verify.sh
```

該命令會執行後端 xUnit、前端 Vitest 和前端生產建置。

## 中斷後恢復

```text
請讀 checkpoint.md，繼續上次未完成的工作。
```
