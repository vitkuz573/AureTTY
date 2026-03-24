#!/bin/bash
set -euo pipefail

# Build AureTTY NativeAOT for generic Linux ARM targets (glibc toolchains).

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

ARCH="${ARCH:-arm64}"
CONFIG="${CONFIG:-Release}"

PROJECT_FILE="$REPO_ROOT/src/AureTTY/AureTTY.csproj"

case "$ARCH" in
    arm64|aarch64)
        ARCH="arm64"
        RID="linux-arm64"
        OUTPUT_DIR="$REPO_ROOT/artifacts/publish/linux-arm64-aot"
        WRAPPER="$SCRIPT_DIR/aarch64-gnu-gcc-wrapper.sh"
        DEFAULT_COMPILER="aarch64-linux-gnu-gcc"
        DEFAULT_OBJCOPY="aarch64-linux-gnu-objcopy"
        ;;
    arm|armv7|armhf)
        ARCH="arm"
        RID="linux-arm"
        OUTPUT_DIR="$REPO_ROOT/artifacts/publish/linux-arm-aot"
        WRAPPER="$SCRIPT_DIR/armv7-gnu-gcc-wrapper.sh"
        DEFAULT_COMPILER="arm-linux-gnueabihf-gcc"
        DEFAULT_OBJCOPY="arm-linux-gnueabihf-objcopy"
        ;;
    *)
        echo "Unsupported ARCH: $ARCH" >&2
        echo "Supported: arm64|aarch64, arm|armv7|armhf" >&2
        exit 1
        ;;
esac

if [[ "$ARCH" == "arm64" ]]; then
    compiler="${AARCH64_GNU_CC:-$DEFAULT_COMPILER}"
    objcopy="${AARCH64_GNU_OBJCOPY:-$DEFAULT_OBJCOPY}"
    export AARCH64_GNU_CC="$compiler"
    export AARCH64_GNU_OBJCOPY="$objcopy"
else
    compiler="${ARMV7_GNU_CC:-$DEFAULT_COMPILER}"
    objcopy="${ARMV7_GNU_OBJCOPY:-$DEFAULT_OBJCOPY}"
    export ARMV7_GNU_CC="$compiler"
    export ARMV7_GNU_OBJCOPY="$objcopy"
fi

if ! command -v "$compiler" >/dev/null 2>&1; then
    echo "Error: compiler '$compiler' not found in PATH." >&2
    echo "Install cross compiler (for example: sudo apt-get install gcc-aarch64-linux-gnu gcc-arm-linux-gnueabihf)." >&2
    exit 1
fi

if ! command -v "$objcopy" >/dev/null 2>&1; then
    echo "Error: objcopy '$objcopy' not found in PATH." >&2
    echo "Install cross binutils (for example: sudo apt-get install binutils-aarch64-linux-gnu binutils-arm-linux-gnueabihf)." >&2
    exit 1
fi

echo "=========================================="
echo "AureTTY Linux ARM AOT Build"
echo "=========================================="
echo "Architecture: $ARCH"
echo "RID: $RID"
echo "Configuration: $CONFIG"
echo "Compiler: $compiler"
echo "ObjCopy: $objcopy"
echo "Wrapper: $WRAPPER"
echo "Output: $OUTPUT_DIR"
echo "=========================================="

rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

dotnet publish "$PROJECT_FILE" \
    -f net10.0 \
    -c "$CONFIG" \
    -r "$RID" \
    --self-contained true \
    -p:PublishAot=true \
    -p:InvariantGlobalization=true \
    -p:OpenApiGenerateDocuments=false \
    -p:OpenApiGenerateDocumentsOnBuild=false \
    -p:CppCompilerAndLinker="$WRAPPER" \
    -p:ObjCopyName="$objcopy" \
    -o "$OUTPUT_DIR"

BINARY_PATH="$OUTPUT_DIR/AureTTY"
if [[ ! -x "$BINARY_PATH" ]]; then
    echo "Error: expected binary not found: $BINARY_PATH" >&2
    exit 1
fi

echo "Build complete: $BINARY_PATH"
