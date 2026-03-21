#!/bin/bash
set -euo pipefail

# AureTTY generic Linux ARM emulated API smoke test.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

ARCH="${ARCH:-arm64}"
API_KEY="${API_KEY:-test-key}"
START_TIMEOUT_SECONDS="${START_TIMEOUT_SECONDS:-90}"
APP_DOTNET_PROCESSOR_COUNT="${APP_DOTNET_PROCESSOR_COUNT:-}"

QEMU_BIN="${QEMU_BIN:-}"
SYSROOT="${SYSROOT:-}"
DEFAULT_PORT=""
BINARY_PATH=""

case "$ARCH" in
    arm64|aarch64)
        ARCH="arm64"
        QEMU_BIN="${QEMU_BIN:-qemu-aarch64}"
        SYSROOT="${SYSROOT:-/usr/aarch64-linux-gnu}"
        DEFAULT_PORT="17853"
        BINARY_PATH="${BINARY_PATH:-$REPO_ROOT/artifacts/publish/linux-arm64-aot/AureTTY}"
        if [[ -z "$APP_DOTNET_PROCESSOR_COUNT" ]]; then
            APP_DOTNET_PROCESSOR_COUNT="1"
        fi
        ;;
    arm|armv7|armhf)
        ARCH="arm"
        QEMU_BIN="${QEMU_BIN:-qemu-arm}"
        SYSROOT="${SYSROOT:-/usr/arm-linux-gnueabihf}"
        DEFAULT_PORT="17854"
        BINARY_PATH="${BINARY_PATH:-$REPO_ROOT/artifacts/publish/linux-arm-aot/AureTTY}"
        ;;
    *)
        echo "Unsupported ARCH: $ARCH" >&2
        echo "Supported: arm64|aarch64, arm|armv7|armhf" >&2
        exit 1
        ;;
esac

PORT="${PORT:-$DEFAULT_PORT}"
BASE_URL="${BASE_URL:-http://127.0.0.1:${PORT}/api/v1}"
SERVER_LOG="${SERVER_LOG:-$REPO_ROOT/artifacts/publish/linux-${ARCH}-aot/auretty-emulated-server.log}"

if ! command -v "$QEMU_BIN" >/dev/null 2>&1; then
    echo "Error: $QEMU_BIN not found." >&2
    echo "Install qemu-user (for example: sudo apt-get install qemu-user)." >&2
    exit 1
fi

if [[ ! -d "$SYSROOT/lib" ]]; then
    echo "Error: invalid SYSROOT '$SYSROOT' (expected '$SYSROOT/lib')." >&2
    exit 1
fi

if [[ ! -x "$BINARY_PATH" ]]; then
    echo "Error: AureTTY binary not found: $BINARY_PATH" >&2
    echo "Build first: ARCH=$ARCH ./scripts/linux/build-aot-arm.sh" >&2
    exit 1
fi

mkdir -p "$(dirname "$SERVER_LOG")"

echo "=========================================="
echo "AureTTY Linux ARM Emulated API Smoke"
echo "=========================================="
echo "Architecture: $ARCH"
echo "QEMU binary: $QEMU_BIN"
echo "Sysroot: $SYSROOT"
echo "Binary: $BINARY_PATH"
echo "Base URL: $BASE_URL"
if [[ -n "$APP_DOTNET_PROCESSOR_COUNT" ]]; then
    echo "DOTNET_PROCESSOR_COUNT: $APP_DOTNET_PROCESSOR_COUNT"
fi
echo "=========================================="

if [[ -n "$APP_DOTNET_PROCESSOR_COUNT" ]]; then
    DOTNET_PROCESSOR_COUNT="$APP_DOTNET_PROCESSOR_COUNT" \
        "$QEMU_BIN" -L "$SYSROOT" \
        "$BINARY_PATH" \
        --transport http \
        --http-listen-url "http://127.0.0.1:${PORT}" \
        --api-key "$API_KEY" \
        >"$SERVER_LOG" 2>&1 &
else
    "$QEMU_BIN" -L "$SYSROOT" \
        "$BINARY_PATH" \
        --transport http \
        --http-listen-url "http://127.0.0.1:${PORT}" \
        --api-key "$API_KEY" \
        >"$SERVER_LOG" 2>&1 &
fi
SERVER_PID=$!

cleanup() {
    if kill -0 "$SERVER_PID" >/dev/null 2>&1; then
        kill "$SERVER_PID" >/dev/null 2>&1 || true
        wait "$SERVER_PID" >/dev/null 2>&1 || true
    fi
}
trap cleanup EXIT

ready=0
for _ in $(seq 1 "$START_TIMEOUT_SECONDS"); do
    if curl -s -f -H "X-AureTTY-Key: $API_KEY" "$BASE_URL/health" >/dev/null 2>&1; then
        ready=1
        break
    fi

    if ! kill -0 "$SERVER_PID" >/dev/null 2>&1; then
        break
    fi

    sleep 1
done

if [[ "$ready" -ne 1 ]]; then
    echo "Error: emulated AureTTY failed to become healthy for $ARCH." >&2
    echo "Server log: $SERVER_LOG" >&2
    tail -n 120 "$SERVER_LOG" >&2 || true
    exit 1
fi

API_KEY="$API_KEY" BASE_URL="$BASE_URL" "$REPO_ROOT/scripts/openwrt/test-api.sh"

echo "Emulated API smoke passed for linux-$ARCH."
