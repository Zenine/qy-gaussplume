#!/usr/bin/env bash
# 项目级完整验证入口：后端测试 + Vue3 前端测试/构建 + Vue2 前端构建

set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"

export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"

echo "==> 后端测试"
(cd "$ROOT/backend-dotnet" && dotnet test --nologo)

echo "==> Vue3 前端测试"
(cd "$ROOT/frontend-vue" && npm test)

echo "==> Vue3 前端构建"
(cd "$ROOT/frontend-vue" && npm run build)

echo "==> Vue2 前端静态回归检查"
(cd "$ROOT/frontend-vue2" && npm run test:static)

echo "==> Vue2 前端构建"
(cd "$ROOT/frontend-vue2" && npm run build)

echo "==> 验证完成"
