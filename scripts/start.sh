#!/usr/bin/env bash
# 一键启动前后端开发服务（macOS / Linux）
# 用法: ./scripts/start.sh

set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
LOG_DIR="$ROOT/.run"
mkdir -p "$LOG_DIR"

BACKEND_LOG="$LOG_DIR/backend.log"
FRONTEND_LOG="$LOG_DIR/frontend.log"
BACKEND_PID="$LOG_DIR/backend.pid"
FRONTEND_PID="$LOG_DIR/frontend.pid"

# 确保 .NET 环境变量可用
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"

# 轮转上一次日志，避免累加到一个大文件
rotate() {
  local f=$1
  if [ -f "$f" ] && [ -s "$f" ]; then
    mv "$f" "${f}.1"
  fi
}
rotate "$BACKEND_LOG"
rotate "$FRONTEND_LOG"

# 清残留僵尸进程（端口冲突常见原因）
if pgrep -f "dotnet.*GnnSimulation.Api" >/dev/null 2>&1; then
  echo "清理旧 dotnet 进程..."
  pkill -f "dotnet.*GnnSimulation.Api" 2>/dev/null || true
fi
if pgrep -f "vite.*frontend-vue" >/dev/null 2>&1; then
  echo "清理旧 vite 进程..."
  pkill -f "vite.*frontend-vue" 2>/dev/null || true
fi
rm -f "$BACKEND_PID" "$FRONTEND_PID"
sleep 1

echo "启动后端 → http://localhost:5207  (日志: $BACKEND_LOG)"
nohup bash -c '
  cd "$1" &&
  ASPNETCORE_URLS="http://localhost:5207" \
  ASPNETCORE_ENVIRONMENT="Development" \
  dotnet run --launch-profile http
' _ "$ROOT/backend-dotnet/GnnSimulation.Api" > "$BACKEND_LOG" 2>&1 &
echo $! > "$BACKEND_PID"

echo "启动前端 → http://localhost:5173  (日志: $FRONTEND_LOG)"
nohup bash -c '
  cd "$1" &&
  npm run dev
' _ "$ROOT/frontend-vue" > "$FRONTEND_LOG" 2>&1 &
echo $! > "$FRONTEND_PID"

echo ""
echo "等待服务就绪..."
for i in $(seq 1 60); do
  if curl -s -o /dev/null -w "%{http_code}" http://localhost:5207/api/map/bounds 2>/dev/null | grep -q 200 && \
     curl -s -o /dev/null -w "%{http_code}" http://localhost:5173/ 2>/dev/null | grep -q 200; then
    echo "✅ 就绪 (${i}s)"
    echo ""
    echo "  前端:    http://localhost:5173"
    echo "  后端:    http://localhost:5207"
    echo "  OpenAPI: http://localhost:5207/openapi/v1.json"
    echo ""
    echo "  实时日志:"
    echo "    tail -f $BACKEND_LOG"
    echo "    tail -f $FRONTEND_LOG"
    echo ""
    echo "  停止: ./scripts/stop.sh"
    exit 0
  fi
  sleep 1
done

echo "❌ 服务启动超时，查看日志:"
echo "  $BACKEND_LOG"
echo "  $FRONTEND_LOG"
echo ""
echo "最后几行后端日志:"
tail -15 "$BACKEND_LOG" 2>/dev/null || true
exit 1
