#!/bin/bash
set -euo pipefail

# Runs emulated API smoke tests for OpenWRT ARM targets.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMMON_LIB="$SCRIPT_DIR/../lib/smoke-helpers.sh"
API_KEY="${API_KEY:-test-key}"
START_TIMEOUT_SECONDS="${START_TIMEOUT_SECONDS:-60}"
AARCH64_ATTEMPTS="${AARCH64_ATTEMPTS:-5}"

source "$COMMON_LIB"

run_with_retry "$AARCH64_ATTEMPTS" env ARCH="aarch64" PORT="${PORT_AARCH64:-17851}" API_KEY="$API_KEY" START_TIMEOUT_SECONDS="$START_TIMEOUT_SECONDS" "$SCRIPT_DIR/test-emulated-api.sh"
env ARCH="armv7" PORT="${PORT_ARMV7:-17852}" API_KEY="$API_KEY" START_TIMEOUT_SECONDS="$START_TIMEOUT_SECONDS" "$SCRIPT_DIR/test-emulated-api.sh"

echo "All emulated OpenWRT API smoke tests passed."
