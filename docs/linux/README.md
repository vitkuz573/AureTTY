# Generic Linux ARM Support

This document describes AureTTY NativeAOT support for generic Linux ARM targets (glibc), including host-side emulated API smoke tests.

## Status

| Architecture | RID | Status | Validation |
|--------------|-----|--------|------------|
| arm64 (aarch64) | `linux-arm64` | ⚠️ Preview | Build + `qemu-aarch64` API smoke |
| arm (armv7/armhf) | `linux-arm` | 🧪 Experimental | Build + `qemu-arm` API smoke |

Notes:
- These targets are for generic Linux (glibc), not OpenWRT musl images.
- For OpenWRT-specific builds, use docs in `docs/openwrt/`.

## Prerequisites

- .NET 10 SDK
- qemu-user
- cross-compilers:
  - `gcc-aarch64-linux-gnu`
  - `gcc-arm-linux-gnueabihf`

Debian/Ubuntu:

```bash
sudo apt-get update
sudo apt-get install -y qemu-user gcc-aarch64-linux-gnu gcc-arm-linux-gnueabihf
```

## Build

```bash
# arm64
ARCH=arm64 ./scripts/linux/build-aot-arm.sh

# armv7/armhf
ARCH=arm ./scripts/linux/build-aot-arm.sh
```

Outputs:

```text
artifacts/publish/linux-arm64-aot/AureTTY
artifacts/publish/linux-arm-aot/AureTTY
```

## Emulated API Smoke

```bash
# One architecture
ARCH=arm64 API_KEY=test-key ./scripts/linux/test-emulated-api.sh
ARCH=arm API_KEY=test-key ./scripts/linux/test-emulated-api.sh

# Both architectures
API_KEY=test-key ./scripts/linux/test-emulated-all.sh
```

Notes:
- `arm64` emulated smoke defaults to `DOTNET_PROCESSOR_COUNT=1` for startup stability under `qemu-user`.
- `scripts/linux/test-emulated-all.sh` retries `arm64` smoke up to 5 times by default.
- default server logs are written to `artifacts/test-logs/linux/<arch>/auretty-emulated-server.log`.
- API smoke curl tuning can be overridden with `CURL_CONNECT_TIMEOUT_SECONDS` and `CURL_MAX_TIME_SECONDS`.
- Override when needed:

```bash
APP_DOTNET_PROCESSOR_COUNT=2 ARCH=arm64 API_KEY=test-key ./scripts/linux/test-emulated-api.sh

# Override retry count for arm64 in all-architectures run
ARM64_ATTEMPTS=3 API_KEY=test-key ./scripts/linux/test-emulated-all.sh
```

Override log path:

```bash
SERVER_LOG=/tmp/auretty-linux-arm64.log ARCH=arm64 API_KEY=test-key ./scripts/linux/test-emulated-api.sh
```

## CI Script

Use the consolidated script to install prerequisites, build both binaries, and run emulated API smoke tests:

```bash
bash ./scripts/ci/build-linux-arm-emulated.sh
```

This script is the reference workflow used for CI validation.
It also writes a deterministic artifact manifest:

```text
artifacts/publish/linux-arm-manifest.txt
```
