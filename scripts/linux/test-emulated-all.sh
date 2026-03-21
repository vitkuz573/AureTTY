#!/bin/bash
set -euo pipefail

# Runs generic Linux ARM emulated API smoke for both ARM targets.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_KEY="${API_KEY:-test-key}"

for arch in arm64 arm; do
    case "$arch" in
        arm64) port="${PORT_ARM64:-17853}" ;;
        arm) port="${PORT_ARM:-17854}" ;;
    esac

    ARCH="$arch" PORT="$port" API_KEY="$API_KEY" "$SCRIPT_DIR/test-emulated-api.sh"
done

echo "All generic Linux ARM emulated API smoke tests passed."
