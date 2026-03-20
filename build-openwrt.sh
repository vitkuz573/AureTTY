#!/bin/bash
set -euo pipefail

# AureTTY OpenWRT Build Script
# Builds optimized NativeAOT binary for OpenWRT targets.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_FILE="$SCRIPT_DIR/src/AureTTY/AureTTY.OpenWRT.csproj"
OUTPUT_DIR="$SCRIPT_DIR/artifacts/openwrt"

# Default values
ARCH="${ARCH:-x86_64}"
CONFIG="${CONFIG:-Release}"
SKIP_INTERPRETER_CHECK="${SKIP_INTERPRETER_CHECK:-0}"

compiler_exists() {
    local candidate="$1"
    if [[ -z "$candidate" ]]; then
        return 1
    fi

    if [[ "$candidate" == */* ]]; then
        local absolute_candidate="$candidate"
        if [[ "$absolute_candidate" != /* ]]; then
            absolute_candidate="$PWD/$absolute_candidate"
        fi
        [[ -x "$absolute_candidate" ]]
        return
    fi

    command -v "$candidate" >/dev/null 2>&1
}

normalize_compiler_path() {
    local candidate="$1"
    if [[ "$candidate" == */* && "$candidate" != /* ]]; then
        echo "$PWD/$candidate"
        return
    fi
    echo "$candidate"
}

append_path_once() {
    local dir="$1"
    [[ -d "$dir" ]] || return 0
    dir="$(cd "$dir" && pwd)"
    case ":$PATH:" in
        *":$dir:"*) ;;
        *) PATH="$PATH:$dir" ;;
    esac
}

load_local_toolchain_paths() {
    local toolchain_root
    for toolchain_root in "$SCRIPT_DIR/.tools/openwrt-toolchains" "$SCRIPT_DIR/.tools/musl-cross"; do
        [[ -d "$toolchain_root" ]] || continue
        while IFS= read -r bin_dir; do
            append_path_once "$bin_dir"
        done < <(find "$toolchain_root" -type d -path '*/bin' | sort)
    done
}

choose_compiler() {
    local description="$1"
    shift

    local candidate
    for candidate in "$@"; do
        if compiler_exists "$candidate"; then
            normalize_compiler_path "$candidate"
            return 0
        fi
    done

    echo "Error: unable to find $description compiler in PATH." >&2
    echo "Checked candidates:" >&2
    for candidate in "$@"; do
        [[ -n "$candidate" ]] && echo "  - $candidate" >&2
    done
    return 1
}

# Auto-discover local toolchains installed into repository-local .tools directory.
load_local_toolchain_paths

# Architecture to RID mapping and toolchain setup.
EXPECTED_INTERPRETER=""
case "$ARCH" in
    x86_64)
        RID="linux-musl-x64"
        EXPECTED_INTERPRETER="/lib/ld-musl-x86_64.so.1"
        X86_64_CC="${X86_64_MUSL_CC:-}"
        X86_64_CC="$(choose_compiler "x86_64 musl" "$X86_64_CC" musl-gcc x86_64-linux-musl-gcc x86_64-openwrt-linux-musl-gcc)"
        export X86_64_CC
        export CppCompilerAndLinker="$SCRIPT_DIR/musl-gcc-wrapper.sh"
        ;;
    aarch64|arm64)
        ARCH="aarch64"
        RID="linux-musl-arm64"
        EXPECTED_INTERPRETER="/lib/ld-musl-aarch64.so.1"
        AARCH64_CC="${AARCH64_MUSL_CC:-}"
        AARCH64_CC="$(choose_compiler "aarch64 musl" "$AARCH64_CC" aarch64-linux-musl-gcc aarch64-openwrt-linux-musl-gcc)"
        export AARCH64_CC
        export CppCompilerAndLinker="$SCRIPT_DIR/aarch64-gcc-wrapper.sh"
        ;;
    armv7|armhf)
        ARCH="armv7"
        RID="linux-musl-arm"
        EXPECTED_INTERPRETER="/lib/ld-musl-armhf.so.1"
        ARMV7_CC="${ARMV7_MUSL_CC:-}"
        ARMV7_CC="$(choose_compiler "armv7 musl" "$ARMV7_CC" arm-linux-musleabihf-gcc arm-openwrt-linux-musleabihf-gcc arm-openwrt-linux-muslgnueabi-gcc)"
        export ARMV7_CC
        export CppCompilerAndLinker="$SCRIPT_DIR/armv7-gcc-wrapper.sh"
        ;;
    mips|mipsel)
        echo "Unsupported architecture: $ARCH" >&2
        echo ".NET 10 does not provide a recognized musl RID for MIPS in this build pipeline." >&2
        echo "Use x86_64, aarch64/arm64, or armv7/armhf." >&2
        exit 1
        ;;
    *)
        echo "Unsupported architecture: $ARCH" >&2
        echo "Supported: x86_64, aarch64, arm64, armv7, armhf" >&2
        exit 1
        ;;
esac

echo "=========================================="
echo "AureTTY OpenWRT Build"
echo "=========================================="
echo "Architecture: $ARCH"
echo "RID: $RID"
echo "Configuration: $CONFIG"
echo "Compiler wrapper: $CppCompilerAndLinker"
echo "Output: $OUTPUT_DIR/$ARCH"
echo "=========================================="

# Clean previous build.
rm -rf "$OUTPUT_DIR/$ARCH"
mkdir -p "$OUTPUT_DIR/$ARCH"

echo "Restoring dependencies..."
dotnet restore "$PROJECT_FILE" -r "$RID"

echo "Building NativeAOT binary..."
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

BINARY_PATH="$OUTPUT_DIR/$ARCH/auretty"
if [[ ! -f "$BINARY_PATH" ]]; then
    echo "Error: expected binary was not produced: $BINARY_PATH" >&2
    exit 1
fi

# Strip binary (additional size reduction).
if command -v strip >/dev/null 2>&1; then
    echo "Stripping debug symbols..."
    strip "$BINARY_PATH"
fi

# Compress with UPX if available (optional).
if command -v upx >/dev/null 2>&1; then
    echo "Compressing with UPX..."
    upx --best --lzma "$BINARY_PATH" || true
fi

if [[ "$SKIP_INTERPRETER_CHECK" != "1" ]] && command -v readelf >/dev/null 2>&1; then
    DETECTED_INTERPRETER="$(readelf -l "$BINARY_PATH" | sed -n 's/.*Requesting program interpreter: \(.*\)]/\1/p' | head -n 1)"
    if [[ -n "$EXPECTED_INTERPRETER" && "$DETECTED_INTERPRETER" != "$EXPECTED_INTERPRETER" ]]; then
        echo "Error: binary interpreter mismatch for $ARCH." >&2
        echo "Expected: $EXPECTED_INTERPRETER" >&2
        echo "Detected: ${DETECTED_INTERPRETER:-<none>}" >&2
        echo "This usually means a glibc cross-compiler was used instead of a musl toolchain." >&2
        exit 1
    fi
fi

BINARY_SIZE="$(du -h "$BINARY_PATH" | cut -f1)"
echo "=========================================="
echo "Build complete!"
echo "Binary size: $BINARY_SIZE"
echo "Location: $BINARY_PATH"
echo "=========================================="

# Test binary (basic check on host-compatible architecture).
if [[ "$ARCH" == "x86_64" ]]; then
    echo "Testing binary..."
    "$BINARY_PATH" --version || echo "Warning: binary test failed (musl runtime may be unavailable on host)."
fi
