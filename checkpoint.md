# Meridian 发布检查点

日期：2026-07-14

## 已完成

- 品牌命名：保留 `QY-GaussPlume`，中文说明为“清源高斯烟羽扩散模拟平台”。
- README：完成简体中文主 README，并补充 English、日本語、繁體中文版本。
- 文档站：新增 VitePress 站点、企业风格主题、首页、快速开始、FAQ、API、架构、工作流与三语对应页面。
- GitHub Pages：新增 `.github/workflows/docs.yml`。
- 视觉资产：新增 `Q` 字母 hero、Open Graph 图片和 Mermaid SVG 图标资源。
- AI 上下文：新增 `AGENTS.md`、`CLAUDE.md`、Cursor/Windsurf 规则和 `QUICK_START.md`。
- 多语检查：新增 `i18n/glossary.md` 与 `scripts/check-i18n-drift.py`。
- SEO/GEO：新增 `robots.txt`、VitePress metadata、JSON-LD 和 llms 生成脚本。

## 已验证

- `npm install`：已在 `docs/` 安装 VitePress 依赖并生成 `package-lock.json`。
- `python3 scripts/check-i18n-drift.py`：通过；仅有本轮未提交翻译的 `Source commit: (uncommitted)` 警告。
- `cd docs && npm run docs:build`：通过。
- `python3 scripts/generate-llms-full.py --all-langs`：已生成根目录和 `docs/public` 两份 `llms-full.txt`。
- `python3 scripts/verify-visual.py --skip-dev`：通过静态构建、SEO/GEO 与 llms 资源检查。
- `./scripts/verify.sh`：通过，后端 163 个测试、前端 113 个测试、前端生产构建均成功。
- `frontend-vue` 生产依赖审计：`npm audit --omit=dev --audit-level=high` 为 0 漏洞。

## 剩余风险

- `docs` 的 `npm audit --audit-level=high` 报告 VitePress 1.6.4 依赖链 3 项告警且无可用稳定修复；当前未使用 `--force` 切换不稳定版本。
- NuGet 最新 `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 仍被 `GHSA-2m69-gcr7-jv3q` 标记；等待上游修复或评估系统 SQLite provider，详见 `TODO.md`。

## 恢复口令

```text
Read checkpoint.md and continue Meridian validation for qy-gaussplume.
```
