#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
BUILD_SCRIPT="$REPO_ROOT/scripts/openwrt/build.sh"
COMMON_LIB="$SCRIPT_DIR/../lib/smoke-helpers.sh"

source "$COMMON_LIB"

cd "$REPO_ROOT"

"$REPO_ROOT/scripts/ci/install-linux-build-deps.sh"

mkdir -p .tools/openwrt-toolchains

verify_tarball_sha256() {
  local tarball="$1"
  local expected_sha="$2"
  local actual_sha

  actual_sha="$(sha256sum "$tarball" | awk '{print $1}')"
  if [[ "$actual_sha" != "$expected_sha" ]]; then
    echo "Error: checksum mismatch for toolchain tarball: $tarball" >&2
    echo "Expected: $expected_sha" >&2
    echo "Actual:   $actual_sha" >&2
    echo "Delete the tarball and rerun to download a clean copy." >&2
    exit 1
  fi

  echo "Verified toolchain tarball checksum: $tarball"
}

AARCH64_TARBALL=".tools/openwrt-toolchains/openwrt-toolchain-24.10.6-armsr-armv8_gcc-13.3.0_musl.Linux-x86_64.tar.zst"
AARCH64_DIR=".tools/openwrt-toolchains/openwrt-toolchain-24.10.6-armsr-armv8_gcc-13.3.0_musl.Linux-x86_64"
# Source: https://downloads.openwrt.org/releases/24.10.6/targets/armsr/armv8/sha256sums
AARCH64_TARBALL_SHA256="683599de1f5741d7ad7deabaa4e967e55323128897da4660b2efa5080d79f9e2"
if [ -f "$AARCH64_TARBALL" ]; then
  verify_tarball_sha256 "$AARCH64_TARBALL" "$AARCH64_TARBALL_SHA256"
fi
if [ ! -d "$AARCH64_DIR" ]; then
  if [ ! -f "$AARCH64_TARBALL" ]; then
    curl -fL --retry 3 --retry-all-errors -o "$AARCH64_TARBALL" "https://downloads.openwrt.org/releases/24.10.6/targets/armsr/armv8/openwrt-toolchain-24.10.6-armsr-armv8_gcc-13.3.0_musl.Linux-x86_64.tar.zst"
    verify_tarball_sha256 "$AARCH64_TARBALL" "$AARCH64_TARBALL_SHA256"
  fi
  tar --zstd -xf "$AARCH64_TARBALL" -C .tools/openwrt-toolchains
fi

ARMV7_TARBALL=".tools/openwrt-toolchains/openwrt-toolchain-24.10.6-ipq40xx-generic_gcc-13.3.0_musl_eabi.Linux-x86_64.tar.zst"
ARMV7_DIR=".tools/openwrt-toolchains/openwrt-toolchain-24.10.6-ipq40xx-generic_gcc-13.3.0_musl_eabi.Linux-x86_64"
# Source: https://downloads.openwrt.org/releases/24.10.6/targets/ipq40xx/generic/sha256sums
ARMV7_TARBALL_SHA256="37a533e8a978164b8403593d9761ad57deef918fcf96ecfd17ce6ec90c99b826"
if [ -f "$ARMV7_TARBALL" ]; then
  verify_tarball_sha256 "$ARMV7_TARBALL" "$ARMV7_TARBALL_SHA256"
fi
if [ ! -d "$ARMV7_DIR" ]; then
  if [ ! -f "$ARMV7_TARBALL" ]; then
    curl -fL --retry 3 --retry-all-errors -o "$ARMV7_TARBALL" "https://downloads.openwrt.org/releases/24.10.6/targets/ipq40xx/generic/openwrt-toolchain-24.10.6-ipq40xx-generic_gcc-13.3.0_musl_eabi.Linux-x86_64.tar.zst"
    verify_tarball_sha256 "$ARMV7_TARBALL" "$ARMV7_TARBALL_SHA256"
  fi
  tar --zstd -xf "$ARMV7_TARBALL" -C .tools/openwrt-toolchains
fi

ARCH=x86_64 "$BUILD_SCRIPT"
ARCH=aarch64 "$BUILD_SCRIPT"
ARCH=armv7 "$BUILD_SCRIPT"

run_native_api_smoke() {
  local binary="$REPO_ROOT/artifacts/openwrt/x86_64/auretty"
  local log_file="$REPO_ROOT/artifacts/test-logs/openwrt/x86_64/auretty-api-smoke-server.log"
  local pid

  mkdir -p "$(dirname "$log_file")"

  "$binary" --transport http --http-listen-url "http://127.0.0.1:17850" --api-key "test-key" >"$log_file" 2>&1 &
  pid=$!

  cleanup() {
    if kill -0 "$pid" >/dev/null 2>&1; then
      kill "$pid" >/dev/null 2>&1 || true
      wait "$pid" >/dev/null 2>&1 || true
    fi
  }
  trap cleanup EXIT

  if ! wait_for_http_health "http://127.0.0.1:17850/api/v1/health" "test-key" "60" "$pid"; then
    echo "Error: native x86_64 AureTTY failed to become healthy." >&2
    echo "Server log: $log_file" >&2
    tail -n 120 "$log_file" >&2 || true
    return 1
  fi

  API_KEY="test-key" BASE_URL="http://127.0.0.1:17850/api/v1" TEST_SUITE_NAME="AureTTY OpenWRT API Test Suite" "$REPO_ROOT/scripts/lib/test-api-smoke.sh"
  trap - EXIT
  cleanup
}

run_native_api_smoke

API_KEY="test-key" AARCH64_ATTEMPTS="5" START_TIMEOUT_SECONDS="90" "$REPO_ROOT/scripts/openwrt/test-emulated-all.sh"

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

  local pkg_arch
  pkg_arch="$(ar p "$pkg_file" control.tar.gz | tar -xzO ./control | sed -n 's/^Architecture: //p' | head -n 1)"
  if [[ -z "$pkg_arch" ]]; then
    echo "Error: invalid .ipk (Architecture field missing): $pkg_file" >&2
    exit 1
  fi

  case "$arch" in
    aarch64)
      if [[ "$pkg_arch" == "aarch64" ]]; then
        echo "Error: package architecture is too generic for aarch64 target: $pkg_arch" >&2
        echo "Expected target-specific arch (for example aarch64_generic)." >&2
        exit 1
      fi
      ;;
    armv7)
      if [[ "$pkg_arch" == "armv7" || "$pkg_arch" == "arm" ]]; then
        echo "Error: package architecture is too generic for armv7 target: $pkg_arch" >&2
        echo "Expected target-specific arch (for example arm_cortex-a7_neon-vfpv4)." >&2
        exit 1
      fi
      ;;
  esac

  echo "Validated .ipk: $pkg_file"
}

validate_ipk x86_64
validate_ipk aarch64
validate_ipk armv7

X86_64_IPK="$(ls -1t "$REPO_ROOT/artifacts/openwrt/ipk/x86_64"/auretty_*.ipk | head -n 1)"
AARCH64_IPK="$(ls -1t "$REPO_ROOT/artifacts/openwrt/ipk/aarch64"/auretty_*.ipk | head -n 1)"
ARMV7_IPK="$(ls -1t "$REPO_ROOT/artifacts/openwrt/ipk/armv7"/auretty_*.ipk | head -n 1)"

"$REPO_ROOT/scripts/ci/write-artifact-manifest.sh" \
  "$REPO_ROOT/artifacts/openwrt/manifest.txt" \
  "$REPO_ROOT/artifacts/openwrt/x86_64/auretty" \
  "$REPO_ROOT/artifacts/openwrt/aarch64/auretty" \
  "$REPO_ROOT/artifacts/openwrt/armv7/auretty" \
  "$X86_64_IPK" \
  "$AARCH64_IPK" \
  "$ARMV7_IPK"

"$REPO_ROOT/scripts/ci/validate-artifact-manifest.sh" \
  "$REPO_ROOT/artifacts/openwrt/manifest.txt"
