#!/bin/bash
set -e

# AureTTY OpenWRT Build Script
# Builds optimized NativeAOT binary for OpenWRT targets

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_FILE="$SCRIPT_DIR/src/AureTTY/AureTTY.OpenWRT.csproj"
OUTPUT_DIR="$SCRIPT_DIR/artifacts/openwrt"

# Default values
ARCH="${ARCH:-x86_64}"
CONFIG="${CONFIG:-Release}"

# Architecture to RID mapping
case "$ARCH" in
    x86_64)
        RID="linux-musl-x64"
        ;;
    aarch64|arm64)
        RID="linux-musl-arm64"
        ;;
    armv7|armhf)
        RID="linux-musl-arm"
        ;;
    mips|mipsel)
        RID="linux-musl-mips64"
        ;;
    *)
        echo "Unsupported architecture: $ARCH"
        echo "Supported: x86_64, aarch64, arm64, armv7, armhf, mips, mipsel"
        exit 1
        ;;
esac

echo "=========================================="
echo "AureTTY OpenWRT Build"
echo "=========================================="
echo "Architecture: $ARCH"
echo "RID: $RID"
echo "Configuration: $CONFIG"
echo "Output: $OUTPUT_DIR/$ARCH"
echo "=========================================="

# Clean previous build
rm -rf "$OUTPUT_DIR/$ARCH"
mkdir -p "$OUTPUT_DIR/$ARCH"

# Restore dependencies
echo "Restoring dependencies..."
dotnet restore "$PROJECT_FILE" -r "$RID"

# Build and publish
echo "Building NativeAOT binary..."

# Set linker to appropriate cross-compiler for musl targets
if [[ "$RID" == *"musl"* ]]; then
    case "$ARCH" in
        x86_64)
            export CppCompilerAndLinker="$SCRIPT_DIR/musl-gcc-wrapper.sh"
            ;;
        aarch64|arm64)
            export CppCompilerAndLinker="$SCRIPT_DIR/aarch64-gcc-wrapper.sh"
            ;;
        armv7|armhf)
            export CppCompilerAndLinker="arm-linux-gnueabihf-gcc"
            ;;
        *)
            export CppCompilerAndLinker="$SCRIPT_DIR/musl-gcc-wrapper.sh"
            ;;
    esac
fi

dotnet publish "$PROJECT_FILE" \
    -c "$CONFIG" \
    -r "$RID" \
    --self-contained true \
    -p:PublishAot=true \
    -p:PublishSingleFile=false \
    -p:PublishTrimmed=true \
    -p:TrimMode=full \
    -p:IlcOptimizationPreference=Size \
    -p:IlcGenerateStackTraceData=false \
    -p:EventSourceSupport=false \
    -p:UseSystemResourceKeys=true \
    -p:DebuggerSupport=false \
    -p:EnableUnsafeBinaryFormatterSerialization=false \
    -p:EnableUnsafeUTF7Encoding=false \
    -p:HttpActivityPropagationSupport=false \
    -p:MetadataUpdaterSupport=false \
    -p:UseNativeHttpHandler=true \
    -p:StripSymbols=true \
    -p:OpenApiGenerateDocuments=false \
    -p:OpenApiGenerateDocumentsOnBuild=false \
    -o "$OUTPUT_DIR/$ARCH"

# Strip binary (additional size reduction)
if command -v strip &> /dev/null; then
    echo "Stripping debug symbols..."
    strip "$OUTPUT_DIR/$ARCH/auretty"
fi

# Compress with UPX if available (optional)
if command -v upx &> /dev/null; then
    echo "Compressing with UPX..."
    upx --best --lzma "$OUTPUT_DIR/$ARCH/auretty" || true
fi

# Show binary size
BINARY_SIZE=$(du -h "$OUTPUT_DIR/$ARCH/auretty" | cut -f1)
echo "=========================================="
echo "Build complete!"
echo "Binary size: $BINARY_SIZE"
echo "Location: $OUTPUT_DIR/$ARCH/auretty"
echo "=========================================="

# Test binary (basic check)
if [ "$ARCH" = "x86_64" ]; then
    echo "Testing binary..."
    "$OUTPUT_DIR/$ARCH/auretty" --version || echo "Warning: Binary test failed (may need musl runtime)"
fi
