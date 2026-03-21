#!/usr/bin/env bash
set -euo pipefail

# AureTTY generic Linux native API smoke test.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
COMMON_LIB="$SCRIPT_DIR/../lib/smoke-helpers.sh"

API_KEY="${API_KEY:-test-key}"
PORT="${PORT:-17855}"
BASE_URL="${BASE_URL:-http://127.0.0.1:${PORT}/api/v1}"
START_TIMEOUT_SECONDS="${START_TIMEOUT_SECONDS:-60}"
BINARY_PATH="${BINARY_PATH:-$REPO_ROOT/artifacts/publish/linux-x64-aot/AureTTY}"
SERVER_LOG="${SERVER_LOG:-$REPO_ROOT/artifacts/test-logs/linux/x64/auretty-native-server.log}"

source "$COMMON_LIB"

if [[ ! -x "$BINARY_PATH" ]]; then
    echo "Error: AureTTY binary not found: $BINARY_PATH" >&2
    echo "Build first: dotnet publish src/AureTTY/AureTTY.csproj -f net10.0 -c Release -r linux-x64 --self-contained true -p:PublishAot=true -o artifacts/publish/linux-x64-aot" >&2
    exit 1
fi

mkdir -p "$(dirname "$SERVER_LOG")"

echo "=========================================="
echo "AureTTY Linux x64 Native API Smoke"
echo "=========================================="
echo "Binary: $BINARY_PATH"
echo "Base URL: $BASE_URL"
echo "=========================================="

"$BINARY_PATH" \
    --transport http \
    --http-listen-url "http://127.0.0.1:${PORT}" \
    --api-key "$API_KEY" \
    >"$SERVER_LOG" 2>&1 &
SERVER_PID=$!

cleanup() {
    if kill -0 "$SERVER_PID" >/dev/null 2>&1; then
        kill "$SERVER_PID" >/dev/null 2>&1 || true
        wait "$SERVER_PID" >/dev/null 2>&1 || true
    fi
}
trap cleanup EXIT

ready=0
if wait_for_http_health "$BASE_URL/health" "$API_KEY" "$START_TIMEOUT_SECONDS" "$SERVER_PID"; then
    ready=1
fi

if [[ "$ready" -ne 1 ]]; then
    echo "Error: native linux-x64 AureTTY failed to become healthy." >&2
    echo "Server log: $SERVER_LOG" >&2
    tail -n 120 "$SERVER_LOG" >&2 || true
    exit 1
fi

API_KEY="$API_KEY" BASE_URL="$BASE_URL" TEST_SUITE_NAME="AureTTY Linux x64 API Test Suite" "$REPO_ROOT/scripts/lib/test-api-smoke.sh"

echo "Native API smoke passed for linux-x64."
