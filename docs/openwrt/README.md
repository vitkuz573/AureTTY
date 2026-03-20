# OpenWRT Support for AureTTY

AureTTY now supports OpenWRT routers and embedded devices with optimized NativeAOT builds.

## Features

- **Small binary size**: ~15 MB (stripped, musl libc)
- **Low memory footprint**: ~20-30 MB RAM at runtime
- **Multiple architectures**: x86_64, ARM64, MIPS (experimental)
- **UCI configuration**: Native OpenWRT configuration system
- **Init script**: Automatic startup with procd
- **Resource limits**: Configurable for low-memory devices

## Quick Start

### Build for OpenWRT

```bash
# Install musl toolchain
sudo apt-get install musl-tools musl-dev

# Build for x86_64
ARCH=x86_64 ./build-openwrt.sh

# Output: artifacts/openwrt/x86_64/auretty (15 MB)
```

### Install on Device

```bash
# Copy binary
scp artifacts/openwrt/x86_64/auretty root@router:/usr/bin/

# SSH to router
ssh root@router

# Make executable
chmod +x /usr/bin/auretty

# Run
auretty --transport http --http-listen-url http://0.0.0.0:17850 --api-key your-key
```

## Supported Architectures

| Architecture | Status | Devices |
|--------------|--------|---------|
| x86_64 | ✅ Stable | x86 routers, QEMU |
| ARM64 (aarch64) | ✅ Stable | ARM Cortex-A routers, RPi 3/4 |
| MIPS | 📋 Planned | MIPS routers (experimental) |

## Documentation

- **[Build Guide](docs/openwrt/BUILD.md)** - Building for OpenWRT
- **[Package Guide](docs/openwrt/PACKAGE.md)** - Creating OpenWRT packages
- **[QEMU Testing](docs/openwrt/QEMU_TESTING.md)** - Testing in QEMU VM

## Configuration

### Low-Memory Devices (64-128 MB RAM)

```bash
auretty \
  --transport http \
  --http-listen-url http://0.0.0.0:17850 \
  --api-key your-key \
  --max-concurrent-sessions 4 \
  --max-sessions-per-viewer 2 \
  --replay-buffer-capacity 1024 \
  --sse-subscription-buffer-capacity 512
```

### Standard Devices (256+ MB RAM)

```bash
auretty \
  --transport http \
  --http-listen-url http://0.0.0.0:17850 \
  --api-key your-key \
  --max-concurrent-sessions 16 \
  --max-sessions-per-viewer 4 \
  --replay-buffer-capacity 2048 \
  --sse-subscription-buffer-capacity 1024
```

## OpenWRT Package

### Install Package

```bash
# Install from IPK
opkg install auretty_0.0.0-1_x86_64.ipk

# Configure via UCI
uci set auretty.config.enabled='1'
uci set auretty.config.api_key='your-secret-key'
uci commit auretty

# Start service
/etc/init.d/auretty start
/etc/init.d/auretty enable
```

### UCI Configuration

```bash
# View configuration
uci show auretty

# Modify settings
uci set auretty.config.http_url='http://0.0.0.0:8080'
uci set auretty.config.max_sessions='8'
uci commit auretty

# Restart service
/etc/init.d/auretty restart
```

## Testing in QEMU

```bash
# Download OpenWRT image
wget https://downloads.openwrt.org/releases/23.05.0/targets/x86/64/openwrt-23.05.0-x86-64-generic-ext4-combined.img.gz
gunzip openwrt-23.05.0-x86-64-generic-ext4-combined.img.gz

# Start QEMU
qemu-system-x86_64 \
  -enable-kvm \
  -m 256M \
  -nographic \
  -device e1000,netdev=net0 \
  -netdev user,id=net0,hostfwd=tcp::2222-:22,hostfwd=tcp::17850-:17850 \
  -drive file=openwrt-23.05.0-x86-64-generic-ext4-combined.img,format=raw

# Transfer binary
scp -P 2222 artifacts/openwrt/x86_64/auretty root@localhost:/usr/bin/

# Test
ssh -p 2222 root@localhost
auretty --version
```

## Performance

### Binary Size Comparison

| Build Type | Size | Notes |
|------------|------|-------|
| Regular linux-x64 | 17 MB | glibc, not stripped |
| OpenWRT (musl) | 15 MB | musl, stripped |
| OpenWRT + UPX | 8-10 MB | compressed (optional) |

### Memory Usage

| Configuration | RAM Usage | Max Sessions |
|---------------|-----------|--------------|
| Minimal | ~20 MB | 4 sessions |
| Standard | ~30 MB | 16 sessions |
| High | ~50 MB | 32 sessions |

## Requirements

### System Requirements

- OpenWRT 23.05+ (kernel 5.15+)
- 64 MB RAM minimum (128 MB recommended)
- 20-30 MB flash storage
- util-linux-script package (for PTY support)

### Dependencies

```bash
# Install required packages
opkg update
opkg install util-linux-script
```

## Troubleshooting

### Binary Won't Run

```bash
# Check binary type
file /usr/bin/auretty
# Should show: ELF 64-bit, dynamically linked, interpreter /lib/ld-musl-x86_64.so.1

# Check musl runtime
ls -l /lib/ld-musl-x86_64.so.1

# Test execution
/usr/bin/auretty --version
```

### Out of Memory

```bash
# Reduce limits
auretty \
  --max-concurrent-sessions 2 \
  --max-sessions-per-viewer 1 \
  --replay-buffer-capacity 512 \
  --sse-subscription-buffer-capacity 256
```

### Permission Denied

```bash
# Fix permissions
chmod +x /usr/bin/auretty

# Check ownership
ls -l /usr/bin/auretty
```

## Roadmap

- [x] x86_64 support
- [x] Build system and scripts
- [x] OpenWRT package structure
- [x] QEMU testing guide
- [ ] ARM64 support
- [ ] MIPS support (experimental)
- [ ] LuCI web interface
- [ ] Package repository

## Contributing

See [CONTRIBUTING.md](../../CONTRIBUTING.md) for development guidelines.

## License

Dual licensed under MIT and Apache-2.0. See [LICENSE-MIT](../../LICENSE-MIT) and [LICENSE-APACHE](../../LICENSE-APACHE).
