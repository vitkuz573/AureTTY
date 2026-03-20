# Building AureTTY for OpenWRT

This guide covers cross-compiling AureTTY NativeAOT binaries for OpenWRT targets.

## Supported Targets

| Architecture | RID | Status | Required Compiler |
|--------------|-----|--------|-------------------|
| x86_64 | linux-musl-x64 | ✅ Stable | `musl-gcc` (or compatible) |
| aarch64/arm64 | linux-musl-arm64 | ⚠️ Preview | `aarch64-linux-musl-gcc` or OpenWRT equivalent |
| armv7/armhf | linux-musl-arm | 🧪 Experimental | `arm-linux-musleabihf-gcc` or OpenWRT equivalent |
| mips/mipsel | - | ❌ Not supported | No supported musl RID in current pipeline |

## Prerequisites

- .NET 10 SDK
- Git
- musl toolchain for host x86_64 builds:

```bash
sudo apt-get install musl-tools musl-dev
```

For ARM cross-builds, provide appropriate musl compilers in `PATH` (for example from OpenWRT SDK toolchains).

## Quick Start

```bash
# x86_64
ARCH=x86_64 ./build-openwrt.sh

# ARM64 (requires musl cross-compiler)
ARCH=aarch64 ./build-openwrt.sh

# ARMv7 (experimental)
ARCH=armv7 ./build-openwrt.sh
```

Output layout:

```text
artifacts/openwrt/<arch>/auretty
```

## Environment Variables

```bash
ARCH=x86_64
CONFIG=Release

# Optional compiler overrides
X86_64_MUSL_CC=musl-gcc
AARCH64_MUSL_CC=/path/to/aarch64-openwrt-linux-musl-gcc
ARMV7_MUSL_CC=/path/to/arm-openwrt-linux-muslgnueabi-gcc

# Optional: disable interpreter validation (not recommended)
SKIP_INTERPRETER_CHECK=1
```

## What the Script Validates

`build-openwrt.sh` now fails early when configuration is invalid.

1. Required musl compiler exists for the selected architecture.
2. Output binary was produced.
3. ELF interpreter matches OpenWRT musl expectations:
- x86_64: `/lib/ld-musl-x86_64.so.1`
- aarch64: `/lib/ld-musl-aarch64.so.1`
- armv7: `/lib/ld-musl-armhf.so.1`

This prevents accidental glibc builds being published as OpenWRT artifacts.

## Binary Verification

```bash
file artifacts/openwrt/x86_64/auretty
readelf -l artifacts/openwrt/x86_64/auretty | grep 'Requesting program interpreter'
```

Expected example (`x86_64`):

```text
Requesting program interpreter: /lib/ld-musl-x86_64.so.1
```

## Run Locally (x86_64)

```bash
artifacts/openwrt/x86_64/auretty --version
artifacts/openwrt/x86_64/auretty \
  --transport http \
  --http-listen-url http://127.0.0.1:17850 \
  --api-key test-key
```

## Troubleshooting

### `Error: unable to find <arch> musl compiler in PATH`

Install or export a musl cross-compiler path via `*_MUSL_CC` env var.

### `binary interpreter mismatch`

A glibc compiler was used. Ensure your compiler is musl-based (`*-linux-musl-gcc`).

### `Error: Unsupported architecture: mips`

MIPS OpenWRT builds are currently blocked by unsupported musl RID in this pipeline.

### `script: command not found` at runtime

Install OpenWRT dependency:

```bash
opkg update
opkg install util-linux-script
```

## Performance Notes

Typical size (stripped):
- x86_64: ~15 MB
- aarch64: ~16 MB

Typical RAM (depends on limits):
- ~20-30 MB normal usage

## Related Docs

- [OpenWRT Overview](README.md)
- [Package Guide](PACKAGE.md)
- [QEMU Testing](QEMU_TESTING.md)
