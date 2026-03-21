#!/bin/bash
set -euo pipefail

# Runs emulated API smoke tests for OpenWRT ARM targets.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_KEY="${API_KEY:-test-key}"
START_TIMEOUT_SECONDS="${START_TIMEOUT_SECONDS:-60}"
AARCH64_ATTEMPTS="${AARCH64_ATTEMPTS:-5}"

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

run_with_retry "$AARCH64_ATTEMPTS" env ARCH="aarch64" PORT="${PORT_AARCH64:-17851}" API_KEY="$API_KEY" START_TIMEOUT_SECONDS="$START_TIMEOUT_SECONDS" "$SCRIPT_DIR/test-emulated-api.sh"
env ARCH="armv7" PORT="${PORT_ARMV7:-17852}" API_KEY="$API_KEY" START_TIMEOUT_SECONDS="$START_TIMEOUT_SECONDS" "$SCRIPT_DIR/test-emulated-api.sh"

echo "All emulated OpenWRT API smoke tests passed."
