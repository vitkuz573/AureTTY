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

run_with_retry() {
  local attempts="$1"
  shift
  local attempt=1

  while true; do
    if "$@"; then
      return 0
    fi
    if (( attempt >= attempts )); then
      return 1
    fi
    echo "Retrying ($((attempt + 1))/$attempts): $*"
    attempt=$((attempt + 1))
    sleep 2
  done
}

run_with_retry 5 env ARCH="aarch64" API_KEY="test-key" START_TIMEOUT_SECONDS="90" "$REPO_ROOT/scripts/openwrt/test-emulated-api.sh"
env ARCH="armv7" API_KEY="test-key" "$REPO_ROOT/scripts/openwrt/test-emulated-api.sh"

"$REPO_ROOT/scripts/openwrt/build-ipk-all.sh"

validate_ipk() {
  local arch="$1"
  local pkg_dir="$REPO_ROOT/artifacts/openwrt/ipk/$arch"
  local pkg_file

  pkg_file="$(ls -1t "$pkg_dir"/auretty_*.ipk | head -n 1)"
  if [[ -z "$pkg_file" || ! -f "$pkg_file" ]]; then
    echo "Error: missing .ipk package for architecture: $arch" >&2
    exit 1
  fi

  if ! ar t "$pkg_file" | grep -qx 'debian-binary'; then
    echo "Error: invalid .ipk (debian-binary missing): $pkg_file" >&2
    exit 1
  fi
  if ! ar t "$pkg_file" | grep -qx 'control.tar.gz'; then
    echo "Error: invalid .ipk (control.tar.gz missing): $pkg_file" >&2
    exit 1
  fi
  if ! ar t "$pkg_file" | grep -qx 'data.tar.gz'; then
    echo "Error: invalid .ipk (data.tar.gz missing): $pkg_file" >&2
    exit 1
  fi

  if ! ar p "$pkg_file" data.tar.gz | tar -tz | grep -qx './usr/bin/auretty'; then
    echo "Error: invalid .ipk (binary missing): $pkg_file" >&2
    exit 1
  fi
  if ! ar p "$pkg_file" data.tar.gz | tar -tz | grep -qx './etc/init.d/auretty'; then
    echo "Error: invalid .ipk (init script missing): $pkg_file" >&2
    exit 1
  fi
  if ! ar p "$pkg_file" data.tar.gz | tar -tz | grep -qx './etc/config/auretty'; then
    echo "Error: invalid .ipk (config missing): $pkg_file" >&2
    exit 1
  fi

  echo "Validated .ipk: $pkg_file"
}

validate_ipk x86_64
validate_ipk aarch64
validate_ipk armv7
