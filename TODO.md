# TODO

## 原 Python 主界面转译差异复核

以下事项已对照 `扩散注释/backend/templates/index.html`、当前 Vue 主控台和 .NET API 复核。按对计算精度和业务使用影响排序。

### 当前状态

- [x] 原 Python 主界面转译差异复核项已处理完毕；当前没有遗留待办。

### 已复核但暂不列为缺陷

- 主控台风速/风向改为临时参数，不写回气象场：这是当前产品规则明确要求，属于设计变化，不按缺陷处理。
- 右侧贡献排名强化为“空气站点污染源贡献排名”：符合当前业务目标，保留；展示层级为“空气站点 → 污染物指标 → 污染源”，且不应影响下一次计算污染物筛选。
- QY 使用 Vue 3 独立前端，GNN 使用 Vue 2 集成前端；两边同步功能语义和回归覆盖，不要求文件实现一致。GNN 的 Vue 2 `before-upload` 重复上传缺陷不适用于 QY。

## 2026-07-14 GNN → QY 同源缺陷复核

- [x] 线源浓度场与受体贡献改为连续 Gauss-Legendre 积分，并消除逐求积点分配完整网格矩阵的问题。
- [x] 多风向显式权重增加数量、有限性、非负性和非零总和校验。
- [x] 结果污染物下拉框只展示当前响应实际包含的污染物分场。
- [x] Vue 3 地图线源改为连续圆角线段，不再叠加点源式端点标记。
- [x] 复核 GNN 的 Vue 2 上传缺陷在 QY 中无对应入口，不移植无关修复。
- [x] 回归测试、README、API、架构、开发工作流和 CHANGELOG 已同步；完整验证入口为 `./scripts/verify.sh`。

## 依赖与格式维护

- [ ] 等待 `SQLitePCLRaw.lib.e_sqlite3` 发布不受 `GHSA-2m69-gcr7-jv3q` 影响的版本，或评估切换系统 SQLite provider；当前 NuGet 最新版 2.1.11 仍被审计命令标记。复查：`cd backend-dotnet && dotnet list GnnSimulation.sln package --vulnerable --include-transitive`。
- [ ] 等待 VitePress 稳定版升级其 Vite/esbuild 依赖链；当前 `docs` 的 `npm audit --audit-level=high` 报告 3 项且标记无可用修复，不使用 `--force` 切换不稳定版本。复查：`cd docs && npm audit --audit-level=high`。
- [ ] 逐步清理仓库既有 .NET 空白格式债；当前 `dotnet format GnnSimulation.sln --verify-no-changes --no-restore` 会在多个历史文件报告格式差异，避免在功能提交中一次性制造大范围无关改动。
