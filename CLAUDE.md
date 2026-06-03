# CLAUDE.md

本文件给 AI 编程助手提供项目上下文。所有面向用户的输出默认使用简体中文。

## 先读

1. `QUICK_START.md`
2. `README.md`
3. `docs/WORKFLOW.md`
4. `checkpoint.md`
5. `AGENTS.md`

## 验证

提交前必须运行：

```bash
./scripts/verify.sh
```

文档和 Meridian 发布层变更必须额外运行：

```bash
python3 scripts/check-i18n-drift.py
cd docs && npm run docs:build
python3 ../scripts/generate-llms-full.py --all-langs
```

## 约束

- 不要绕过失败的测试或 pre-commit。
- 不要提交密钥、token、OAuth 凭证、真实项目数据或本机私有配置。
- 若遇到未由自己产生的工作区改动，先读懂并保留，不要回滚。
