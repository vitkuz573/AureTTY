#!/bin/bash
set -euo pipefail

# AureTTY OpenWRT Emulated API Smoke Test
# Runs ARM OpenWRT binary via qemu-user + musl sysroot and executes HTTP API tests.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
COMMON_LIB="$SCRIPT_DIR/../lib/smoke-helpers.sh"

ARCH="${ARCH:-aarch64}"
API_KEY="${API_KEY:-test-key}"
START_TIMEOUT_SECONDS="${START_TIMEOUT_SECONDS:-60}"
APP_DOTNET_PROCESSOR_COUNT="${APP_DOTNET_PROCESSOR_COUNT:-}"

source "$COMMON_LIB"

QEMU_BIN="${QEMU_BIN:-}"
LOADER_NAME=""
DEFAULT_PORT=""

case "$ARCH" in
    aarch64|arm64)
        ARCH="aarch64"
        LOADER_NAME="ld-musl-aarch64.so.1"
        DEFAULT_PORT="17851"
        QEMU_BIN="${QEMU_BIN:-qemu-aarch64}"
        APP_DOTNET_PROCESSOR_COUNT="${APP_DOTNET_PROCESSOR_COUNT:-1}"
        ;;
    armv7|armhf)
        ARCH="armv7"
        LOADER_NAME="ld-musl-armhf.so.1"
        DEFAULT_PORT="17852"
        QEMU_BIN="${QEMU_BIN:-qemu-arm}"
        ;;
    *)
        echo "Unsupported ARCH: $ARCH" >&2
        echo "Supported: aarch64|arm64, armv7|armhf" >&2
        exit 1
        ;;
esac

PORT="${PORT:-$DEFAULT_PORT}"
BASE_URL="${BASE_URL:-http://127.0.0.1:${PORT}/api/v1}"
BINARY_PATH="${BINARY_PATH:-$REPO_ROOT/artifacts/openwrt/$ARCH/auretty}"
SERVER_LOG="${SERVER_LOG:-$REPO_ROOT/artifacts/test-logs/openwrt/${ARCH}/auretty-emulated-server.log}"

find_sysroot() {
    local roots=("$REPO_ROOT/.tools/openwrt-toolchains" "$REPO_ROOT/.tools/musl-cross")
    local root
    local loader_path

    for root in "${roots[@]}"; do
        [[ -d "$root" ]] || continue
        while IFS= read -r loader_path; do
            dirname "$(dirname "$loader_path")"
            return 0
        done < <(find "$root" \( -type f -o -type l \) -name "$LOADER_NAME" | sort)
    done

    return 1
}

if ! command -v "$QEMU_BIN" >/dev/null 2>&1; then
    echo "Error: $QEMU_BIN not found." >&2
    echo "Install qemu-user (for example: sudo apt-get install qemu-user)." >&2
    exit 1
fi

if [[ ! -x "$BINARY_PATH" ]]; then
    echo "Error: AureTTY binary not found: $BINARY_PATH" >&2
    echo "Build first: ARCH=$ARCH ./scripts/openwrt/build.sh" >&2
    exit 1
fi

SYSROOT="${SYSROOT:-}"
if [[ -z "$SYSROOT" ]]; then
    if ! SYSROOT="$(find_sysroot)"; then
        echo "Error: unable to find musl sysroot for $ARCH (loader $LOADER_NAME)." >&2
        echo "Install OpenWRT toolchain under .tools/openwrt-toolchains or set SYSROOT explicitly." >&2
        exit 1
    fi
fi

if [[ ! -d "$SYSROOT/lib" ]]; then
    echo "Error: invalid SYSROOT '$SYSROOT' (missing lib directory)." >&2
    exit 1
fi

if [[ ! -f "$SYSROOT/lib/$LOADER_NAME" ]]; then
    echo "Error: expected loader not found: $SYSROOT/lib/$LOADER_NAME" >&2
    exit 1
fi

mkdir -p "$(dirname "$SERVER_LOG")"

echo "=========================================="
echo "AureTTY OpenWRT Emulated API Smoke"
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
if wait_for_http_health "$BASE_URL/health" "$API_KEY" "$START_TIMEOUT_SECONDS" "$SERVER_PID"; then
    ready=1
fi

if [[ "$ready" -ne 1 ]]; then
    echo "Error: emulated AureTTY failed to become healthy for $ARCH." >&2
    echo "Server log: $SERVER_LOG" >&2
    tail -n 120 "$SERVER_LOG" >&2 || true
    exit 1
fi

API_KEY="$API_KEY" BASE_URL="$BASE_URL" TEST_SUITE_NAME="AureTTY OpenWRT API Test Suite" "$REPO_ROOT/scripts/lib/test-api-smoke.sh"

echo "Emulated API smoke passed for $ARCH."
