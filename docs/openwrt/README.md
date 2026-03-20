# OpenWRT Support for AureTTY

AureTTY supports OpenWRT with optimized NativeAOT builds and OpenWRT-native service packaging.

## Current Status

| Architecture | Status | Notes |
|--------------|--------|-------|
| x86_64 | ✅ Stable | Fully validated in local build + API smoke tests |
| ARM64 (aarch64) | ⚠️ Preview | Requires musl cross-compiler and on-device validation |
| ARMv7 (armhf) | 🧪 Experimental | Build pipeline available, requires external musl toolchain |
| MIPS | ❌ Not supported | No supported musl RID in current .NET pipeline |

## Features

- NativeAOT binary optimized for size (`~15 MB` on x86_64, stripped)
- Low runtime footprint for embedded devices
- OpenWRT package layout (`ipk`), UCI config, and procd init script
- Automated HTTP API smoke tests (`test-openwrt-api.sh`)
- QEMU test workflow for x86_64

## Quick Start

### 1. Build x86_64 Binary

```bash
# Install build prerequisites (Debian/Ubuntu)
sudo apt-get install musl-tools musl-dev

# Build
ARCH=x86_64 ./build-openwrt.sh

# Output
# artifacts/openwrt/x86_64/auretty
```

For ARM builds, `build-openwrt.sh` auto-discovers toolchains from:
- `.tools/openwrt-toolchains/**/bin`
- `.tools/musl-cross/**/bin`

### 2. Deploy to Device

```bash
scp artifacts/openwrt/x86_64/auretty root@router:/usr/bin/
ssh root@router 'chmod +x /usr/bin/auretty && /usr/bin/auretty --version'
```

### 3. Run Service

```bash
auretty \
  --transport http \
  --http-listen-url http://0.0.0.0:17850 \
  --api-key your-key
```

Notes:
- For Linux/OpenWRT clients, prefer `"shell":"sh"` in session create requests.
- If `shell` is omitted, Linux/OpenWRT defaults to `sh`.

## ARM64 Build Notes

ARM64 builds are strict musl-only now. The script fails fast if no musl compiler is available.

Supported ARM64 compiler names (auto-detected):
- `aarch64-linux-musl-gcc`
- `aarch64-openwrt-linux-musl-gcc`

You can override compiler path/name:

```bash
AARCH64_MUSL_CC=/path/to/aarch64-openwrt-linux-musl-gcc ARCH=aarch64 ./build-openwrt.sh
```

## ARMv7 Build Notes

ARMv7 builds are supported with musl toolchains (`arm-linux-musleabihf-gcc` or OpenWRT equivalents).
The wrapper enables a linker workaround for mixed ABI attributes in current .NET `linux-musl-arm` NativeAOT libs.
To disable it explicitly:

```bash
ARMV7_ALLOW_ABI_MISMATCH=0 ARCH=armv7 ./build-openwrt.sh
```

## OpenWRT Package

Package files live in `package/auretty/`:
- `Makefile`
- `files/auretty.init`
- `files/auretty.config`

Install example:

```bash
opkg install auretty_0.0.0-1_x86_64.ipk
uci set auretty.config.enabled='1'
uci set auretty.config.api_key='your-secret-key'
uci commit auretty
/etc/init.d/auretty enable
/etc/init.d/auretty start
```

## Testing

### API Smoke Tests

```bash
# Start service first, then:
./test-openwrt-api.sh
```

### QEMU (x86_64)

See [QEMU Testing](QEMU_TESTING.md).

## Troubleshooting

### `Error: unable to find aarch64 musl compiler in PATH`

Install/provide a musl cross-compiler and retry:

```bash
AARCH64_MUSL_CC=/path/to/aarch64-openwrt-linux-musl-gcc ARCH=aarch64 ./build-openwrt.sh
```

### `binary interpreter mismatch`

`build-openwrt.sh` verifies the ELF interpreter. This error means a glibc compiler was used by mistake.

Expected interpreters:
- x86_64: `/lib/ld-musl-x86_64.so.1`
- aarch64: `/lib/ld-musl-aarch64.so.1`
- armv7: `/lib/ld-musl-armhf.so.1`

### `... STAGING_DIR not defined` during ARM builds

This warning is emitted by OpenWRT GCC wrapper scripts in standalone mode and is non-fatal.

### Binary does not start on device

```bash
file /usr/bin/auretty
/usr/bin/auretty --version
```

Validate architecture and musl runtime on target.

## Documentation

- [Build Guide](BUILD.md)
- [Package Guide](PACKAGE.md)
- [QEMU Testing](QEMU_TESTING.md)

## License

Dual licensed under MIT and Apache-2.0.
