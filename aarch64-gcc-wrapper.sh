#!/bin/bash
# Wrapper for AArch64 musl GCC that filters out incompatible flags

set -euo pipefail

args=()
for arg in "$@"; do
    # Skip --target flag (not supported by gcc)
    if [[ "$arg" == --target=* ]]; then
        continue
    fi
    # Replace -fuse-ld=gnu with -fuse-ld=bfd
    if [[ "$arg" == "-fuse-ld=gnu" ]]; then
        args+=("-fuse-ld=bfd")
        continue
    fi
    args+=("$arg")
done

compiler="${AARCH64_CC:-aarch64-linux-musl-gcc}"
if [[ "$compiler" == */* ]]; then
    if [[ ! -x "$compiler" ]]; then
        echo "Error: compiler '$compiler' is not executable." >&2
        exit 1
    fi
else
    if ! command -v "$compiler" >/dev/null 2>&1; then
        echo "Error: compiler '$compiler' not found in PATH." >&2
        exit 1
    fi
fi

exec "$compiler" "${args[@]}"
