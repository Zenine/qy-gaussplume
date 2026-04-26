#!/usr/bin/env bash
# 停止前后端开发服务（macOS / Linux）
# 用法: ./scripts/stop.sh

set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
LOG_DIR="$ROOT/.run"

kill_pid() {
  local pidfile=$1
  local name=$2
  if [ -f "$pidfile" ]; then
    local pid=$(cat "$pidfile")
    if kill -0 "$pid" 2>/dev/null; then
      echo "停止 $name (PID $pid)"
      # dotnet/npm 可能 fork 子进程，杀进程组
      pkill -TERM -P "$pid" 2>/dev/null || true
      kill -TERM "$pid" 2>/dev/null || true
      sleep 1
      kill -KILL "$pid" 2>/dev/null || true
    fi
    rm -f "$pidfile"
  else
    echo "$name 未在运行"
  fi
}

kill_pid "$LOG_DIR/backend.pid" "后端"
kill_pid "$LOG_DIR/frontend.pid" "前端"

# 兜底：按进程名清理可能的残留
pkill -f "dotnet.*GnnSimulation.Api" 2>/dev/null || true
pkill -f "vite.*frontend-vue" 2>/dev/null || true

echo "✅ 已停止"
