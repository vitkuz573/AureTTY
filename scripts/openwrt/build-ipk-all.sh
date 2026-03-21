#!/bin/bash
set -euo pipefail

# Build AureTTY .ipk packages for all supported OpenWRT architectures.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
IPK_ARCHES="${IPK_ARCHES:-x86_64 aarch64 armv7}"

for arch in $IPK_ARCHES; do
    ARCH="$arch" "$SCRIPT_DIR/build-ipk.sh"
done

echo "All OpenWRT packages built."
