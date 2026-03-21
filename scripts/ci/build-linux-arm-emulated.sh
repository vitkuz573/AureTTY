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

API_KEY="test-key" "$REPO_ROOT/scripts/linux/test-emulated-all.sh"
