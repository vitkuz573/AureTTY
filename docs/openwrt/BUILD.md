# Building AureTTY for OpenWRT

This guide explains how to build AureTTY for OpenWRT routers and embedded devices.

## Prerequisites

- .NET 10 SDK
- musl toolchain: `sudo apt-get install musl-tools musl-dev`
- Git

## Quick Start

```bash
# Build for x86_64 (most common OpenWRT target)
ARCH=x86_64 ./build-openwrt.sh

# Build for ARM64
ARCH=aarch64 ./build-openwrt.sh

# Output: artifacts/openwrt/{arch}/auretty
```

## Supported Architectures

| Architecture | RID | OpenWRT Devices |
|--------------|-----|-----------------|
| x86_64 | linux-musl-x64 | x86 routers, QEMU |
| aarch64/arm64 | linux-musl-arm64 | ARM Cortex-A routers |
| mips/mipsel | linux-musl-mips64 | MIPS routers (experimental) |

## Binary Size

- **Optimized build**: ~15 MB (stripped)
- **With UPX compression**: ~8-10 MB (optional)
- **Memory usage**: ~20-30 MB RAM at runtime

## Build Options

### Environment Variables

```bash
ARCH=x86_64          # Target architecture
CONFIG=Release       # Build configuration
```

### Manual Build

```bash
# Using the OpenWRT project file directly
dotnet publish src/AureTTY/AureTTY.OpenWRT.csproj \
  -c Release \
  -r linux-musl-x64 \
  --self-contained true \
  -p:PublishAot=true \
  -p:IlcOptimizationPreference=Size \
  -p:StripSymbols=true \
  -o artifacts/openwrt/x86_64
```

## Size Optimization

The build script applies these optimizations:

1. **NativeAOT compilation** - Ahead-of-time compilation
2. **Aggressive trimming** - Remove unused code
3. **Size-optimized IL** - `IlcOptimizationPreference=Size`
4. **Disabled features**:
   - Stack traces (`IlcGenerateStackTraceData=false`)
   - Event sources (`EventSourceSupport=false`)
   - Debugger support (`DebuggerSupport=false`)
   - HTTP activity propagation (`HttpActivityPropagationSupport=false`)
5. **Symbol stripping** - Remove debug symbols
6. **Invariant globalization** - No locale data

### Optional: UPX Compression

```bash
# Install UPX
sudo apt-get install upx-ucl

# Compress binary (reduces size by ~40%)
upx --best --lzma artifacts/openwrt/x86_64/auretty
```

**Warning**: UPX may cause issues on some OpenWRT devices. Test before deploying.

## Testing the Binary

### Local Testing (x86_64)

```bash
# Check binary info
file artifacts/openwrt/x86_64/auretty
# Output: ELF 64-bit LSB pie executable, x86-64, dynamically linked, interpreter /lib/ld-musl-x86_64.so.1

# Test execution
artifacts/openwrt/x86_64/auretty --version
artifacts/openwrt/x86_64/auretty --help

# Run service (requires musl runtime)
artifacts/openwrt/x86_64/auretty \
  --transport http \
  --http-listen-url http://0.0.0.0:17850 \
  --api-key test-key
```

### QEMU Testing

See [docs/openwrt/QEMU_TESTING.md](QEMU_TESTING.md) for testing in QEMU VM.

### On-Device Testing

```bash
# Copy to OpenWRT device
scp artifacts/openwrt/x86_64/auretty root@192.168.1.1:/tmp/

# SSH to device
ssh root@192.168.1.1

# Test
/tmp/auretty --version
/tmp/auretty --help
```

## Deployment

### Manual Installation

```bash
# Copy binary
scp artifacts/openwrt/x86_64/auretty root@router:/usr/bin/

# SSH to router
ssh root@router

# Make executable
chmod +x /usr/bin/auretty

# Test
auretty --version

# Run
auretty --transport http --http-listen-url http://0.0.0.0:17850 --api-key your-key
```

### OpenWRT Package

See [docs/openwrt/PACKAGE.md](PACKAGE.md) for creating an OpenWRT package.

## Troubleshooting

### Build Errors

**Error: `musl-gcc: command not found`**
```bash
sudo apt-get install musl-tools musl-dev
```

**Error: `unrecognized command-line option '--target=x86_64-linux-musl'`**
- The build script uses a wrapper (`musl-gcc-wrapper.sh`) to filter this flag
- Ensure the wrapper is executable: `chmod +x musl-gcc-wrapper.sh`

**Error: Duplicate assembly attributes**
- Clean build: `rm -rf src/AureTTY/obj src/AureTTY/bin`
- Rebuild: `./build-openwrt.sh`

### Runtime Errors

**Error: `error while loading shared libraries: /lib/ld-musl-x86_64.so.1`**
- The device doesn't have musl runtime
- OpenWRT uses musl by default, this should work on real devices
- For testing on glibc systems, use the regular linux-x64 build

**Error: `Illegal instruction`**
- Architecture mismatch
- Rebuild for correct architecture (x86_64, aarch64, etc.)

**Error: `Out of memory`**
- Device has insufficient RAM
- Reduce session limits: `--max-concurrent-sessions 4 --max-sessions-per-viewer 2`
- Reduce buffer sizes: `--replay-buffer-capacity 1024 --sse-subscription-buffer-capacity 512`

## Performance Tuning

### Memory-Constrained Devices (64-128 MB RAM)

```bash
auretty \
  --transport http \
  --http-listen-url http://0.0.0.0:17850 \
  --api-key your-key \
  --max-concurrent-sessions 4 \
  --max-sessions-per-viewer 2 \
  --replay-buffer-capacity 1024 \
  --max-pending-input-chunks 2048 \
  --sse-subscription-buffer-capacity 512
```

### High-Performance Devices (256+ MB RAM)

```bash
auretty \
  --transport http \
  --http-listen-url http://0.0.0.0:17850 \
  --api-key your-key \
  --max-concurrent-sessions 32 \
  --max-sessions-per-viewer 8 \
  --replay-buffer-capacity 4096 \
  --max-pending-input-chunks 8192 \
  --sse-subscription-buffer-capacity 2048
```

## Size Comparison

| Build Type | Size | Notes |
|------------|------|-------|
| Regular linux-x64 | 17 MB | glibc, not stripped |
| OpenWRT (musl) | 15 MB | musl, stripped |
| OpenWRT + UPX | 8-10 MB | compressed, may have issues |
| OpenWRT + optimizations | 12-13 MB | future improvements |

## Next Steps

- [Create OpenWRT Package](PACKAGE.md)
- [Test in QEMU](QEMU_TESTING.md)
- [Deploy to Device](DEPLOYMENT.md)

## See Also

- [OpenWRT Documentation](https://openwrt.org/docs/start)
- [.NET NativeAOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
- [musl libc](https://musl.libc.org/)
