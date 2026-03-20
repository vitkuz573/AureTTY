#!/bin/bash
set -e

# AureTTY OpenWRT QEMU Test Script
# Downloads OpenWRT image and tests AureTTY in QEMU VM

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OPENWRT_VERSION="23.05.0"
OPENWRT_TARGET="x86/64"
OPENWRT_IMAGE="openwrt-${OPENWRT_VERSION}-x86-64-generic-ext4-combined.img"
OPENWRT_URL="https://downloads.openwrt.org/releases/${OPENWRT_VERSION}/targets/${OPENWRT_TARGET}/${OPENWRT_IMAGE}.gz"
QEMU_DIR="$SCRIPT_DIR/qemu-test"
BINARY_PATH="$SCRIPT_DIR/artifacts/openwrt/x86_64/auretty"

echo "=========================================="
echo "AureTTY OpenWRT QEMU Test"
echo "=========================================="

# Check prerequisites
if ! command -v qemu-system-x86_64 &> /dev/null; then
    echo "Error: qemu-system-x86_64 not found"
    echo "Install: sudo apt-get install qemu-system-x86"
    exit 1
fi

if [ ! -f "$BINARY_PATH" ]; then
    echo "Error: AureTTY binary not found at $BINARY_PATH"
    echo "Build first: ARCH=x86_64 ./build-openwrt.sh"
    exit 1
fi

# Create QEMU directory
mkdir -p "$QEMU_DIR"
cd "$QEMU_DIR"

# Download OpenWRT image if not exists
if [ ! -f "$OPENWRT_IMAGE" ]; then
    echo "Downloading OpenWRT image..."
    wget -q --show-progress "$OPENWRT_URL"
    gunzip "${OPENWRT_IMAGE}.gz"
fi

echo "=========================================="
echo "Starting QEMU VM..."
echo "=========================================="
echo "Port forwarding:"
echo "  SSH: localhost:2222 -> VM:22"
echo "  AureTTY: localhost:17850 -> VM:17850"
echo ""
echo "Login credentials:"
echo "  Username: root"
echo "  Password: (press Enter)"
echo ""
echo "To exit QEMU: Ctrl+A, then X"
echo "=========================================="

# Start QEMU with port forwarding
qemu-system-x86_64 \
  -enable-kvm \
  -m 256M \
  -nographic \
  -device e1000,netdev=net0 \
  -netdev user,id=net0,hostfwd=tcp::2222-:22,hostfwd=tcp::17850-:17850 \
  -drive file="$OPENWRT_IMAGE",format=raw
