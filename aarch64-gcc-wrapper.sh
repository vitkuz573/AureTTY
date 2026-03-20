#!/bin/bash
# Wrapper for aarch64-linux-gnu-gcc that filters out incompatible flags

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

exec aarch64-linux-gnu-gcc "${args[@]}"
