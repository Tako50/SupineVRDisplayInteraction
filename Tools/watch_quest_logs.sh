#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PULL_SCRIPT="${PROJECT_ROOT}/Tools/pull_quest_logs.sh"

if ! command -v adb >/dev/null 2>&1; then
  echo "[watch_quest_logs] adb was not found. Install Android platform-tools or add adb to PATH." >&2
  exit 1
fi

echo "[watch_quest_logs] Waiting for Quest adb connection..."
echo "[watch_quest_logs] Logs will be copied to: ${PROJECT_ROOT}/Logs/Quest"
echo "[watch_quest_logs] Stop with Ctrl+C."

while true; do
  adb wait-for-device
  echo "[watch_quest_logs] Quest connected. Copying logs..."

  if "$PULL_SCRIPT" --open; then
    echo "[watch_quest_logs] Logs copied and Finder opened."
  else
    echo "[watch_quest_logs] Copy failed or no logs exist yet. Will retry on the next connection." >&2
  fi

  while adb get-state >/dev/null 2>&1; do
    sleep 5
  done

  echo "[watch_quest_logs] Quest disconnected. Waiting again..."
done
