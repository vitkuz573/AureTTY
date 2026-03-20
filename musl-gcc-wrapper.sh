#!/bin/bash
# Wrapper for musl-gcc that filters out --target flag

args=()
for arg in "$@"; do
    if [[ "$arg" != --target=* ]]; then
        args+=("$arg")
    fi
done

exec musl-gcc "${args[@]}"
