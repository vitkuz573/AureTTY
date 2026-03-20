#!/usr/bin/env bash
set -euo pipefail

sudo apt-get update
sudo apt-get install -y musl-tools musl-dev zstd xz-utils curl binutils file ruby-full

mkdir -p .tools/openwrt-toolchains

AARCH64_TARBALL=".tools/openwrt-toolchains/openwrt-toolchain-24.10.6-armsr-armv8_gcc-13.3.0_musl.Linux-x86_64.tar.zst"
AARCH64_DIR=".tools/openwrt-toolchains/openwrt-toolchain-24.10.6-armsr-armv8_gcc-13.3.0_musl.Linux-x86_64"
if [ ! -d "$AARCH64_DIR" ]; then
  if [ ! -f "$AARCH64_TARBALL" ]; then
    curl -fL --retry 3 --retry-all-errors -o "$AARCH64_TARBALL" "https://downloads.openwrt.org/releases/24.10.6/targets/armsr/armv8/openwrt-toolchain-24.10.6-armsr-armv8_gcc-13.3.0_musl.Linux-x86_64.tar.zst"
  fi
  tar --zstd -xf "$AARCH64_TARBALL" -C .tools/openwrt-toolchains
fi

ARMV7_TARBALL=".tools/openwrt-toolchains/openwrt-toolchain-24.10.6-ipq40xx-generic_gcc-13.3.0_musl_eabi.Linux-x86_64.tar.zst"
ARMV7_DIR=".tools/openwrt-toolchains/openwrt-toolchain-24.10.6-ipq40xx-generic_gcc-13.3.0_musl_eabi.Linux-x86_64"
if [ ! -d "$ARMV7_DIR" ]; then
  if [ ! -f "$ARMV7_TARBALL" ]; then
    curl -fL --retry 3 --retry-all-errors -o "$ARMV7_TARBALL" "https://downloads.openwrt.org/releases/24.10.6/targets/ipq40xx/generic/openwrt-toolchain-24.10.6-ipq40xx-generic_gcc-13.3.0_musl_eabi.Linux-x86_64.tar.zst"
  fi
  tar --zstd -xf "$ARMV7_TARBALL" -C .tools/openwrt-toolchains
fi

ARCH=x86_64 ./build-openwrt.sh
ARCH=aarch64 ./build-openwrt.sh
ARCH=armv7 ./build-openwrt.sh
