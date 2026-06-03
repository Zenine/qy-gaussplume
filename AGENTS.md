# AGENTS.md

## 项目规则

- 面向用户的计划、报告、文档和提交说明默认使用简体中文。
- 修改行为前优先补充或更新测试；若无法测试，必须说明替代验证方式。
- 提交前必须运行 `./scripts/verify.sh`，失败不得提交。
- 文档发布相关改动还需运行：
  - `python3 scripts/check-i18n-drift.py`
  - `cd docs && npm run docs:build`
  - `python3 scripts/generate-llms-full.py --all-langs`
- 不提交密钥、token、OAuth 凭证、个人机器绝对密钥路径或真实项目数据。

## 代码入口

- 后端：`backend-dotnet/AirPollution.Api`
- 前端：`frontend-vue/src`
- 文档：`docs`
- 多语术语表：`i18n/glossary.md`
- Meridian 检查点：`checkpoint.md`

## 当前产品重点

- 主界面需要可直接控制单风向模拟的风速与风向。
- 排放源管理应支持 Excel 批量导入。
- 等效面源污染物只显示一个有效输入框，避免同时出现 `emissionRate` 与 `concentration` 两个可编辑值。
- 受体点管理应支持批量导入与批量删除。
