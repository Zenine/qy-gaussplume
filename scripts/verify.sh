#!/usr/bin/env bash
# 项目级完整验证入口：后端测试 + 前端测试 + 前端生产构建

set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"

export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"

echo "==> 后端测试"
(cd "$ROOT/backend-dotnet" && dotnet test --nologo)

echo "==> 前端测试"
(cd "$ROOT/frontend-vue" && npm test)

echo "==> 前端构建"
(cd "$ROOT/frontend-vue" && npm run build)

echo "==> 验证完成"
