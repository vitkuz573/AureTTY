#!/bin/bash
# Wrapper for ARMv7 musl GCC that filters out incompatible flags.

set -euo pipefail

args=()
for arg in "$@"; do
    # Skip --target flag (not supported by gcc frontends).
    if [[ "$arg" == --target=* ]]; then
        continue
    fi
    # GNU ld alias is not always available across cross-toolchains.
    if [[ "$arg" == "-fuse-ld=gnu" ]]; then
        args+=("-fuse-ld=bfd")
        continue
    fi
    args+=("$arg")
done

compiler="${ARMV7_CC:-arm-linux-musleabihf-gcc}"
if ! command -v "$compiler" >/dev/null 2>&1; then
    echo "Error: compiler '$compiler' not found in PATH." >&2
    exit 1
fi

exec "$compiler" "${args[@]}"
