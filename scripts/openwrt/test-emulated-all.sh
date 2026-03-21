#!/bin/bash
set -euo pipefail

# Runs emulated API smoke tests for OpenWRT ARM targets.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_KEY="${API_KEY:-test-key}"

for arch in aarch64 armv7; do
    case "$arch" in
        aarch64) port="${PORT_AARCH64:-17851}" ;;
        armv7) port="${PORT_ARMV7:-17852}" ;;
    esac

    ARCH="$arch" PORT="$port" API_KEY="$API_KEY" "$SCRIPT_DIR/test-emulated-api.sh"
done

echo "All emulated OpenWRT API smoke tests passed."
