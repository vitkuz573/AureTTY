#!/bin/bash
# Wrapper for musl-gcc that filters out --target flag

set -euo pipefail

args=()
for arg in "$@"; do
    if [[ "$arg" != --target=* ]]; then
        args+=("$arg")
    fi
done

compiler="${X86_64_CC:-musl-gcc}"
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
