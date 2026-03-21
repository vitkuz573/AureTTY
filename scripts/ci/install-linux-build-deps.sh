#!/usr/bin/env bash
set -euo pipefail

# Installs Linux CI dependencies used by ARM/OpenWRT build pipelines.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
STAMP_PATH="$REPO_ROOT/.tools/.ci-linux-build-deps-v1.stamp"

if [[ -f "$STAMP_PATH" ]]; then
    echo "Linux CI dependencies already installed (stamp: $STAMP_PATH)."
    exit 0
fi

APT_PREFIX=()
if [[ "$(id -u)" -ne 0 ]]; then
    if command -v sudo >/dev/null 2>&1; then
        APT_PREFIX=(sudo)
    else
        echo "Error: sudo is required to install CI dependencies." >&2
        exit 1
    fi
fi

"${APT_PREFIX[@]}" apt-get update
"${APT_PREFIX[@]}" apt-get install -y \
    qemu-user \
    gcc-aarch64-linux-gnu \
    gcc-arm-linux-gnueabihf \
    musl-tools \
    musl-dev \
    zstd \
    xz-utils \
    curl \
    binutils \
    file \
    ruby-full \
    unzip

mkdir -p "$REPO_ROOT/.tools"
touch "$STAMP_PATH"

echo "Installed Linux CI dependencies (stamp: $STAMP_PATH)."
