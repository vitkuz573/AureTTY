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

# .NET linux-musl-arm NativeAOT runtime package currently ships mixed ARM EABI
# attributes across native static libraries (hard-float objects plus zlib objects
# marked without VFP args). Allow the linker to proceed in this known case.
if [[ "${ARMV7_ALLOW_ABI_MISMATCH:-1}" == "1" ]]; then
    args+=("-Wl,--no-warn-mismatch")
fi

compiler="${ARMV7_CC:-arm-linux-musleabihf-gcc}"
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
