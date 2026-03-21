#!/bin/bash
set -euo pipefail

# AureTTY OpenWRT QEMU Test Script
# Downloads OpenWRT image and tests AureTTY in QEMU VM

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
OPENWRT_VERSION="23.05.0"
OPENWRT_TARGET="x86/64"
OPENWRT_IMAGE="openwrt-${OPENWRT_VERSION}-x86-64-generic-ext4-combined.img"
OPENWRT_URL="https://downloads.openwrt.org/releases/${OPENWRT_VERSION}/targets/${OPENWRT_TARGET}/${OPENWRT_IMAGE}.gz"
QEMU_DIR="$REPO_ROOT/artifacts/openwrt/qemu-test"
BINARY_PATH="$REPO_ROOT/artifacts/openwrt/x86_64/auretty"

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
    echo "Build first: ARCH=x86_64 ./scripts/openwrt/build.sh"
    exit 1
fi

# Create QEMU directory
mkdir -p "$QEMU_DIR"
cd "$QEMU_DIR"

# Download OpenWRT image if not exists
if [ ! -f "$OPENWRT_IMAGE" ]; then
    echo "Downloading OpenWRT image..."
    wget -q --show-progress "$OPENWRT_URL"

    echo "Extracting OpenWRT image..."
    if ! gzip -dc "${OPENWRT_IMAGE}.gz" > "$OPENWRT_IMAGE"; then
        if [ -s "$OPENWRT_IMAGE" ]; then
            echo "Warning: gzip reported non-fatal issues; continuing with extracted image."
        else
            echo "Error: failed to extract OpenWRT image"
            exit 1
        fi
    fi
    rm -f "${OPENWRT_IMAGE}.gz"
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

# Use KVM when available, otherwise fallback to software emulation.
QEMU_ACCEL_ARGS=()
if [ -c /dev/kvm ] && [ -r /dev/kvm ] && [ -w /dev/kvm ]; then
  QEMU_ACCEL_ARGS+=(-enable-kvm)
  echo "KVM acceleration: enabled"
else
  echo "KVM acceleration: unavailable, using software emulation (TCG)"
fi

# Start QEMU with port forwarding
qemu-system-x86_64 \
  "${QEMU_ACCEL_ARGS[@]}" \
  -m 256M \
  -nographic \
  -device e1000,netdev=net0 \
  -netdev user,id=net0,hostfwd=tcp::2222-:22,hostfwd=tcp::17850-:17850 \
  -drive file="$OPENWRT_IMAGE",format=raw
