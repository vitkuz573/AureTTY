#!/usr/bin/env bash
set -euo pipefail

# Builds generic Linux ARM NativeAOT artifacts and validates them via qemu-user.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

cd "$REPO_ROOT"

sudo apt-get update
sudo apt-get install -y qemu-user gcc-aarch64-linux-gnu gcc-arm-linux-gnueabihf

ARCH=arm64 "$REPO_ROOT/scripts/linux/build-aot-arm.sh"
ARCH=arm "$REPO_ROOT/scripts/linux/build-aot-arm.sh"

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

run_with_retry 5 env ARCH="arm64" API_KEY="test-key" START_TIMEOUT_SECONDS="90" "$REPO_ROOT/scripts/linux/test-emulated-api.sh"
env ARCH="arm" API_KEY="test-key" "$REPO_ROOT/scripts/linux/test-emulated-api.sh"

echo "All generic Linux ARM emulated API smoke tests passed."
