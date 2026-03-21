#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
BUILD_SCRIPT="$REPO_ROOT/scripts/openwrt/build.sh"

cd "$REPO_ROOT"

sudo apt-get update
sudo apt-get install -y musl-tools musl-dev zstd xz-utils curl binutils file ruby-full qemu-user

mkdir -p .tools/openwrt-toolchains

AARCH64_TARBALL=".tools/openwrt-toolchains/openwrt-toolchain-24.10.6-armsr-armv8_gcc-13.3.0_musl.Linux-x86_64.tar.zst"
AARCH64_DIR=".tools/openwrt-toolchains/openwrt-toolchain-24.10.6-armsr-armv8_gcc-13.3.0_musl.Linux-x86_64"
if [ ! -d "$AARCH64_DIR" ]; then
  if [ ! -f "$AARCH64_TARBALL" ]; then
    curl -fL --retry 3 --retry-all-errors -o "$AARCH64_TARBALL" "https://downloads.openwrt.org/releases/24.10.6/targets/armsr/armv8/openwrt-toolchain-24.10.6-armsr-armv8_gcc-13.3.0_musl.Linux-x86_64.tar.zst"
  fi
  tar --zstd -xf "$AARCH64_TARBALL" -C .tools/openwrt-toolchains
fi

ARMV7_TARBALL=".tools/openwrt-toolchains/openwrt-toolchain-24.10.6-ipq40xx-generic_gcc-13.3.0_musl_eabi.Linux-x86_64.tar.zst"
ARMV7_DIR=".tools/openwrt-toolchains/openwrt-toolchain-24.10.6-ipq40xx-generic_gcc-13.3.0_musl_eabi.Linux-x86_64"
if [ ! -d "$ARMV7_DIR" ]; then
  if [ ! -f "$ARMV7_TARBALL" ]; then
    curl -fL --retry 3 --retry-all-errors -o "$ARMV7_TARBALL" "https://downloads.openwrt.org/releases/24.10.6/targets/ipq40xx/generic/openwrt-toolchain-24.10.6-ipq40xx-generic_gcc-13.3.0_musl_eabi.Linux-x86_64.tar.zst"
  fi
  tar --zstd -xf "$ARMV7_TARBALL" -C .tools/openwrt-toolchains
fi

ARCH=x86_64 "$BUILD_SCRIPT"
ARCH=aarch64 "$BUILD_SCRIPT"
ARCH=armv7 "$BUILD_SCRIPT"

run_native_api_smoke() {
  local binary="$REPO_ROOT/artifacts/openwrt/x86_64/auretty"
  local log_file="$REPO_ROOT/artifacts/openwrt/x86_64/auretty-api-smoke-server.log"
  local pid

  "$binary" --transport http --http-listen-url "http://127.0.0.1:17850" --api-key "test-key" >"$log_file" 2>&1 &
  pid=$!

  cleanup() {
    if kill -0 "$pid" >/dev/null 2>&1; then
      kill "$pid" >/dev/null 2>&1 || true
      wait "$pid" >/dev/null 2>&1 || true
    fi
  }
  trap cleanup EXIT

  for _ in $(seq 1 60); do
    if curl -s -f -H "X-AureTTY-Key: test-key" "http://127.0.0.1:17850/api/v1/health" >/dev/null 2>&1; then
      break
    fi
    sleep 1
  done

  API_KEY="test-key" BASE_URL="http://127.0.0.1:17850/api/v1" "$REPO_ROOT/scripts/openwrt/test-api.sh"
  trap - EXIT
  cleanup
}

run_native_api_smoke
API_KEY="test-key" "$REPO_ROOT/scripts/openwrt/test-emulated-all.sh"
