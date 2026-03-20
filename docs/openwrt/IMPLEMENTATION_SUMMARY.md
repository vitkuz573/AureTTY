# OpenWRT Support Implementation Summary

## Status (2026-03-20)

OpenWRT support is now split by architecture maturity:

- `x86_64`: production-ready in this repository
- `aarch64`: preview (musl-only toolchain required, on-device validation pending)
- `armv7`: experimental (toolchain-dependent)
- `mips`: not supported in current .NET musl RID pipeline

## What Is Implemented

### Build Pipeline

- Dedicated OpenWRT project: `src/AureTTY/AureTTY.OpenWRT.csproj`
- NativeAOT and size optimization flags enabled
- Architecture-aware build script: `scripts/openwrt/build.sh`
- Toolchain wrappers:
  - `scripts/openwrt/musl-gcc-wrapper.sh` (x86_64)
  - `scripts/openwrt/aarch64-gcc-wrapper.sh`
  - `scripts/openwrt/armv7-gcc-wrapper.sh`

### Safety Improvements

- Strict musl compiler detection by architecture
- Auto-discovery of repo-local toolchains from `.tools/openwrt-toolchains/**/bin` and `.tools/musl-cross/**/bin`
- Fast fail when required cross-compiler is missing
- ELF interpreter verification after build:
  - x86_64 -> `/lib/ld-musl-x86_64.so.1`
  - aarch64 -> `/lib/ld-musl-aarch64.so.1`
  - armv7 -> `/lib/ld-musl-armhf.so.1`
- Removed false-positive "successful" ARM64 glibc outputs from normal flow
- ARMv7 linker wrapper includes controlled ABI mismatch workaround for current .NET `linux-musl-arm` runtime static libs

### API/Runtime Compatibility Improvements

- Added explicit `Shell.Sh` option in shell enum
- Linux/OpenWRT default shell changed to `sh` when omitted
- String shell aliases accepted in HTTP JSON (`sh`, `ash`, `bash`, `pwsh`, `powershell`, `cmd`)
- Existing numeric enum wire format remains intact for backward compatibility

### Packaging and Service Integration

- OpenWRT package skeleton retained (`package/auretty`)
- Makefile improved:
  - explicit missing-binary check
  - overridable artifact folder selector: `AURETTY_ARCH_DIR`
- procd init and UCI config remain in place

### Testing and CI

- `scripts/openwrt/test-api.sh` fixed and now passes end-to-end (9/9)
- Full unit/integration test suites pass:
  - `AureTTY.Tests`
  - `AureTTY.Core.Tests`
- `AureTTY.OpenWRT.csproj` added to solution
- AppVeyor Linux branch now builds solution before tests (broader compile coverage)

## Verified Results in This Repository

- x86_64 OpenWRT build completes successfully (`~15 MB` stripped)
- aarch64 OpenWRT build completes successfully (`~16 MB` stripped)
- armv7 OpenWRT build completes successfully (`~14 MB` stripped)
- Built x86_64 binary starts and serves API
- OpenWRT API smoke suite passes fully against built binary
- Generated interpreters match expected OpenWRT musl loaders for all three supported targets

## Remaining Work

1. Validate ARM64 on real OpenWRT hardware (or reproducible emulator path)
2. Validate ARMv7 on real OpenWRT hardware (especially long-running stability)
3. Add CI job with musl cross toolchains for ARM targets
4. Add package release automation (`ipk` artifacts per target)

## Conclusion

OpenWRT support for `x86_64` is solid and reproducible.

Cross-arch behavior is now explicit and safe: unsupported or misconfigured toolchains fail fast instead of producing misleading artifacts.
