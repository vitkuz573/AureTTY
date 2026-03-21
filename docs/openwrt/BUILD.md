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

For cross-builds, provide appropriate musl compilers in `PATH`.
`scripts/openwrt/build.sh` auto-discovers local toolchains from:
- `.tools/openwrt-toolchains/**/bin`
- `.tools/musl-cross/**/bin`

For host-side ARM runtime emulation tests:

```bash
sudo apt-get install qemu-user
```

Example local setup (repo-local, no global PATH edits needed):

```bash
# OpenWRT aarch64 toolchain
mkdir -p .tools/openwrt-toolchains
curl -fL -o .tools/openwrt-toolchains/openwrt-toolchain-24.10.6-armsr-armv8_gcc-13.3.0_musl.Linux-x86_64.tar.zst \
  https://downloads.openwrt.org/releases/24.10.6/targets/armsr/armv8/openwrt-toolchain-24.10.6-armsr-armv8_gcc-13.3.0_musl.Linux-x86_64.tar.zst
tar --zstd -xf .tools/openwrt-toolchains/openwrt-toolchain-24.10.6-armsr-armv8_gcc-13.3.0_musl.Linux-x86_64.tar.zst -C .tools/openwrt-toolchains

# OpenWRT armv7 toolchain
curl -fL -o .tools/openwrt-toolchains/openwrt-toolchain-24.10.6-ipq40xx-generic_gcc-13.3.0_musl_eabi.Linux-x86_64.tar.zst \
  https://downloads.openwrt.org/releases/24.10.6/targets/ipq40xx/generic/openwrt-toolchain-24.10.6-ipq40xx-generic_gcc-13.3.0_musl_eabi.Linux-x86_64.tar.zst
tar --zstd -xf .tools/openwrt-toolchains/openwrt-toolchain-24.10.6-ipq40xx-generic_gcc-13.3.0_musl_eabi.Linux-x86_64.tar.zst -C .tools/openwrt-toolchains

# Optional generic armhf musl cross-toolchain (for arm-linux-musleabihf-gcc fallback)
mkdir -p .tools/musl-cross
curl -fL -o .tools/musl-cross/arm-linux-musleabihf-cross.tgz https://musl.cc/arm-linux-musleabihf-cross.tgz
tar -xzf .tools/musl-cross/arm-linux-musleabihf-cross.tgz -C .tools/musl-cross
```

## Quick Start

```bash
# x86_64
ARCH=x86_64 ./scripts/openwrt/build.sh

# ARM64 (requires musl cross-compiler)
ARCH=aarch64 ./scripts/openwrt/build.sh

# ARMv7 (experimental)
ARCH=armv7 ./scripts/openwrt/build.sh
```

Output layout:

```text
artifacts/openwrt/<arch>/auretty
```

Optional runtime smoke tests after build:

```bash
# Native host (x86_64)
./scripts/openwrt/test-api.sh

# Emulated ARM binaries on x86_64 host
ARCH=aarch64 ./scripts/openwrt/test-emulated-api.sh
ARCH=armv7 ./scripts/openwrt/test-emulated-api.sh

# Run all emulated non-host tests
./scripts/openwrt/test-emulated-all.sh
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

# ARMv7: disable linker ABI mismatch workaround (advanced/debug only)
ARMV7_ALLOW_ABI_MISMATCH=0
```

## What the Script Validates

`scripts/openwrt/build.sh` now fails early when configuration is invalid.

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

### `... STAGING_DIR not defined` from OpenWRT GCC wrappers

This warning comes from OpenWRT wrapper scripts and is expected in standalone toolchain usage.
It does not block the build.

### `binary interpreter mismatch`

A glibc compiler was used. Ensure your compiler is musl-based (`*-linux-musl-gcc`).

### `Error: Unsupported architecture: mips`

MIPS OpenWRT builds are currently blocked by unsupported musl RID in this pipeline.

### `NativeAOT is not supported for linux-musl-x86`

OpenWRT x86/i386 builds are currently blocked by unsupported NativeAOT RID in this pipeline (`NETSDK1203`).

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
