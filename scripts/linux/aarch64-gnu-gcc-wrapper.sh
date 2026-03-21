#!/bin/bash
# Wrapper for glibc aarch64 GCC that filters flags not accepted by GCC frontends.

set -euo pipefail

args=()
for arg in "$@"; do
    # GCC cross frontend does not accept clang-style --target=...
    if [[ "$arg" == --target=* ]]; then
        continue
    fi

    # Some environments do not expose GNU linker alias.
    if [[ "$arg" == "-fuse-ld=gnu" ]]; then
        args+=("-fuse-ld=bfd")
        continue
    fi

    args+=("$arg")
done

compiler="${AARCH64_GNU_CC:-aarch64-linux-gnu-gcc}"
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
