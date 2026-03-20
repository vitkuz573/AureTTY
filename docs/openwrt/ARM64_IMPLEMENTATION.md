# ARM64 OpenWRT Notes

## Current Status (2026-03-20)

ARM64 support is available in **preview** mode:

- Build RID: `linux-musl-arm64`
- Expected loader: `/lib/ld-musl-aarch64.so.1`
- Requires musl cross-compiler in PATH (or via `AARCH64_MUSL_CC`)
- Repo-local toolchains are auto-detected from `.tools/openwrt-toolchains/**/bin` and `.tools/musl-cross/**/bin`
- Not yet validated on real ARM64 OpenWRT device in CI

## Required Compiler

`build-openwrt.sh` auto-detects one of:

- `aarch64-linux-musl-gcc`
- `aarch64-openwrt-linux-musl-gcc`

Or explicitly:

```bash
AARCH64_MUSL_CC=/path/to/aarch64-openwrt-linux-musl-gcc ARCH=aarch64 ./build-openwrt.sh
```

## Important Change

The build now fails fast when the compiler is missing or when the output binary has the wrong interpreter.

This prevents accidentally publishing glibc-linked ARM64 binaries as OpenWRT artifacts.

## Next Steps

1. Validate runtime on real ARM64 OpenWRT hardware
2. Add ARM64 cross-build verification to CI
3. Publish ARM64 package artifacts once runtime validation is complete
