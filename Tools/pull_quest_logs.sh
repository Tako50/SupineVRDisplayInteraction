#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PACKAGE_NAME="${PACKAGE_NAME:-com.DefaultCompany.SupineVRDisplayInteraction}"
REMOTE_LOG_DIR="/sdcard/Android/data/${PACKAGE_NAME}/files/Logs"
LOCAL_LOG_DIR="${PROJECT_ROOT}/Logs/Quest"
OPEN_FINDER=false

for arg in "$@"; do
  case "$arg" in
    --open)
      OPEN_FINDER=true
      ;;
    --help|-h)
      echo "Usage: $0 [--open]"
      echo "Copies Quest logs from ${REMOTE_LOG_DIR} to ${LOCAL_LOG_DIR}."
      exit 0
      ;;
  esac
done

if ! command -v adb >/dev/null 2>&1; then
  echo "[pull_quest_logs] adb was not found. Install Android platform-tools or add adb to PATH." >&2
  exit 1
fi

if ! adb get-state >/dev/null 2>&1; then
  echo "[pull_quest_logs] Quest is not connected over adb." >&2
  exit 1
fi

mkdir -p "$LOCAL_LOG_DIR"

if ! adb shell "[ -d '${REMOTE_LOG_DIR}' ]" >/dev/null 2>&1; then
  echo "[pull_quest_logs] Remote log folder does not exist yet: ${REMOTE_LOG_DIR}" >&2
  exit 1
fi

echo "[pull_quest_logs] Pulling logs from Quest..."
echo "[pull_quest_logs] Remote: ${REMOTE_LOG_DIR}"
echo "[pull_quest_logs] Local:  ${LOCAL_LOG_DIR}"
adb pull "${REMOTE_LOG_DIR}/." "$LOCAL_LOG_DIR"

if [ "$OPEN_FINDER" = true ] && [ "$(uname)" = "Darwin" ]; then
  open "$LOCAL_LOG_DIR"
fi

echo "[pull_quest_logs] Done."
