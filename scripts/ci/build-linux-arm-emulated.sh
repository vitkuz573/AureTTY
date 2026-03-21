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

API_KEY="test-key" ARM64_ATTEMPTS="5" START_TIMEOUT_SECONDS="90" "$REPO_ROOT/scripts/linux/test-emulated-all.sh"

"$REPO_ROOT/scripts/ci/write-artifact-manifest.sh" \
  "$REPO_ROOT/artifacts/publish/linux-arm-manifest.txt" \
  "$REPO_ROOT/artifacts/publish/linux-arm64-aot/AureTTY" \
  "$REPO_ROOT/artifacts/publish/linux-arm-aot/AureTTY"

"$REPO_ROOT/scripts/ci/validate-artifact-manifest.sh" \
  "$REPO_ROOT/artifacts/publish/linux-arm-manifest.txt"
