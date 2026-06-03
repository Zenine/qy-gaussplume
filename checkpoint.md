# Meridian 发布检查点

日期：2026-06-03

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
- `./scripts/verify.sh`：通过，后端 138 个测试、前端 70 个测试、前端生产构建均成功。

## 剩余风险

- `npm audit --audit-level=moderate` 报告 VitePress 1.6.4 依赖链中的 `vite/esbuild` 开发服务器 moderate 告警，npm 标记为 `No fix available`；当前未切换到不稳定替代版本。

## 恢复口令

```text
Read checkpoint.md and continue Meridian validation for qy-gaussplume.
```
