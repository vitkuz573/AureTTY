# Generic Linux Support

This document describes AureTTY NativeAOT support for generic Linux targets (glibc), including native and emulated API smoke tests.

## Status

| Architecture | RID | Status | Validation |
|--------------|-----|--------|------------|
| x64 | `linux-x64` | ✅ Stable | Native API smoke |
| arm64 (aarch64) | `linux-arm64` | ⚠️ Preview | Build + `qemu-aarch64` API smoke |
| arm (armv7/armhf) | `linux-arm` | 🧪 Experimental | Build + `qemu-arm` API smoke |

Notes:
- These targets are for generic Linux (glibc), not OpenWRT musl images.
- For OpenWRT-specific builds, use docs in `docs/openwrt/`.

## Prerequisites

- .NET 10 SDK
- qemu-user (for ARM emulated smoke)
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
# x64
dotnet publish src/AureTTY/AureTTY.csproj -f net10.0 -c Release -r linux-x64 --self-contained true -p:PublishAot=true -p:OpenApiGenerateDocuments=false -p:OpenApiGenerateDocumentsOnBuild=false -o artifacts/publish/linux-x64-aot

# arm64
ARCH=arm64 ./scripts/linux/build-aot-arm.sh

# armv7/armhf
ARCH=arm ./scripts/linux/build-aot-arm.sh
```

Outputs:

```text
artifacts/publish/linux-x64-aot/AureTTY
artifacts/publish/linux-arm64-aot/AureTTY
artifacts/publish/linux-arm-aot/AureTTY
```

## API Smoke

```bash
# native x64
API_KEY=test-key ./scripts/linux/test-native-api.sh

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
bash ./scripts/ci/build-linux-x64-scd.sh
bash ./scripts/ci/build-linux-x64-aot.sh
bash ./scripts/ci/build-linux-arm-emulated.sh
```

These scripts are the reference workflow used for CI validation.
They also write deterministic artifact manifests:

```text
artifacts/publish/linux-x64-manifest.txt
artifacts/publish/linux-arm-manifest.txt
```

Both CI scripts call a shared dependency installer (`scripts/ci/install-linux-build-deps.sh`) that uses a repo-local stamp file to skip repeated `apt-get` runs in the same workspace.

In AppVeyor tagged releases these manifests are published as:

```text
artifacts/AureTTY-<tag>-linux-x64-manifest.txt
artifacts/AureTTY-<tag>-linux-x64-manifest.sha256
artifacts/AureTTY-<tag>-linux-arm-manifest.txt
artifacts/AureTTY-<tag>-linux-arm-manifest.sha256
```

AppVeyor Linux release builds also validate generated checksum files before upload:

```bash
bash ./scripts/ci/validate-sha256-files.sh artifacts/AureTTY-<tag>-*.sha256
```
