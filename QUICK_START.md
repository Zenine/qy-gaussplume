# QY-GaussPlume 快速启动

## 项目定位

QY-GaussPlume 是一个本地可运行的大气污染物扩散模拟平台，包含 ASP.NET Core 9 后端、Vue 3 前端、Leaflet 地图和高斯烟羽模型。当前主线已修复管理页与主界面的风速风向、批量导入/删除、等效面源污染物双输入等问题。

## 首次进入

```bash
pwd
git status --short
sed -n '1,220p' README.md
sed -n '1,220p' docs/WORKFLOW.md
```

如需继续上次 Meridian 发布流程，读取 `checkpoint.md`。

## 本地运行

```bash
cd frontend-vue
npm install --registry=https://registry.npmmirror.com
cd ..
./scripts/start.sh
```

前端默认地址：`http://localhost:5173`。
后端默认地址：`http://localhost:5207`。

## 提交前验证

优先运行项目级完整入口：

```bash
./scripts/verify.sh
```

Meridian 文档层额外验证：

```bash
python3 scripts/check-i18n-drift.py
cd docs && npm run docs:build
python3 ../scripts/generate-llms-full.py --all-langs
```

## 给 AI 助手的一句话

```text
项目在 /Users/zeninexu/github/未命名文件夹/qy-gaussplume。先读 QUICK_START.md、README.md、docs/WORKFLOW.md 和 checkpoint.md，再按项目验证入口工作。
```
