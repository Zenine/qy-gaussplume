# QY-GaussPlume 工作规则

- 用户可见输出默认使用简体中文。
- 先读 `QUICK_START.md`、`README.md`、`docs/WORKFLOW.md`、`checkpoint.md`。
- 行为改动优先先写或更新测试。
- 提交前必须运行 `./scripts/verify.sh`。
- 文档发布层改动还要运行 `python3 scripts/check-i18n-drift.py` 和 `cd docs && npm run docs:build`。
- 不提交密钥、token、OAuth 凭证、真实项目数据或个人机器私有路径。
