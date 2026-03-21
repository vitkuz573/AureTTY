#!/bin/bash
set -euo pipefail

# Runs generic Linux ARM emulated API smoke for both ARM targets.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMMON_LIB="$SCRIPT_DIR/../lib/smoke-helpers.sh"
API_KEY="${API_KEY:-test-key}"
START_TIMEOUT_SECONDS="${START_TIMEOUT_SECONDS:-90}"
ARM64_ATTEMPTS="${ARM64_ATTEMPTS:-5}"

source "$COMMON_LIB"

run_with_retry "$ARM64_ATTEMPTS" env ARCH="arm64" PORT="${PORT_ARM64:-17853}" API_KEY="$API_KEY" START_TIMEOUT_SECONDS="$START_TIMEOUT_SECONDS" "$SCRIPT_DIR/test-emulated-api.sh"
env ARCH="arm" PORT="${PORT_ARM:-17854}" API_KEY="$API_KEY" START_TIMEOUT_SECONDS="$START_TIMEOUT_SECONDS" "$SCRIPT_DIR/test-emulated-api.sh"

echo "All generic Linux ARM emulated API smoke tests passed."
