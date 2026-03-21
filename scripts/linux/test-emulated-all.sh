#!/bin/bash
set -euo pipefail

# Runs generic Linux ARM emulated API smoke for both ARM targets.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_KEY="${API_KEY:-test-key}"
START_TIMEOUT_SECONDS="${START_TIMEOUT_SECONDS:-90}"
ARM64_ATTEMPTS="${ARM64_ATTEMPTS:-5}"

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

run_with_retry "$ARM64_ATTEMPTS" env ARCH="arm64" PORT="${PORT_ARM64:-17853}" API_KEY="$API_KEY" START_TIMEOUT_SECONDS="$START_TIMEOUT_SECONDS" "$SCRIPT_DIR/test-emulated-api.sh"
env ARCH="arm" PORT="${PORT_ARM:-17854}" API_KEY="$API_KEY" START_TIMEOUT_SECONDS="$START_TIMEOUT_SECONDS" "$SCRIPT_DIR/test-emulated-api.sh"

echo "All generic Linux ARM emulated API smoke tests passed."
