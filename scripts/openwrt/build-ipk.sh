#!/bin/bash
set -euo pipefail

# Build AureTTY OpenWRT .ipk from prebuilt binary.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

ARCH="${ARCH:-x86_64}"
PACKAGE_ARCH="${PACKAGE_ARCH:-auto}"
PKG_NAME="${PKG_NAME:-auretty}"
PKG_VERSION="${PKG_VERSION:-auto}"
PKG_BASE_VERSION="${PKG_BASE_VERSION:-0.0.0}"
PKG_RELEASE="${PKG_RELEASE:-1}"
PKG_MAINTAINER="${PKG_MAINTAINER:-Vitaly Kuzyaev <vitkuz573@gmail.com>}"
PKG_SECTION="${PKG_SECTION:-net}"
PKG_PRIORITY="${PKG_PRIORITY:-optional}"
PKG_DEPENDS="${PKG_DEPENDS:-util-linux-script}"
KEEP_WORK_DIR="${KEEP_WORK_DIR:-0}"
CLEAN_OLD_PACKAGES="${CLEAN_OLD_PACKAGES:-1}"

BINARY_PATH="${BINARY_PATH:-$REPO_ROOT/artifacts/openwrt/$ARCH/auretty}"
OUT_DIR="${OUT_DIR:-$REPO_ROOT/artifacts/openwrt/ipk/$ARCH}"
WORK_DIR="$OUT_DIR/${PKG_NAME}_ipk_root"
INIT_SCRIPT="$REPO_ROOT/package/auretty/files/auretty.init"
CONFIG_FILE="$REPO_ROOT/package/auretty/files/auretty.config"

detect_package_arch_from_toolchain() {
    local normalized_arch="$1"
    local root
    local candidate
    local base
    local package_arch

    for root in "$REPO_ROOT/.tools/openwrt-toolchains" "$REPO_ROOT/.tools/musl-cross"; do
        [[ -d "$root" ]] || continue
        case "$normalized_arch" in
            x86|i386|i686)
                while IFS= read -r candidate; do
                    base="$(basename "$candidate")"
                    package_arch="${base#toolchain-}"
                    package_arch="${package_arch%%_gcc-*}"
                    package_arch="${package_arch//+/_}"
                    if [[ "$package_arch" == i386* || "$package_arch" == i486* || "$package_arch" == i586* || "$package_arch" == i686* ]]; then
                        echo "$package_arch"
                        return 0
                    fi
                done < <(find "$root" -type d -name 'toolchain-i?86*_gcc-*' | sort)
                ;;
            aarch64|arm64)
                while IFS= read -r candidate; do
                    base="$(basename "$candidate")"
                    package_arch="${base#toolchain-}"
                    package_arch="${package_arch%%_gcc-*}"
                    package_arch="${package_arch//+/_}"
                    if [[ "$package_arch" == aarch64* ]]; then
                        echo "$package_arch"
                        return 0
                    fi
                done < <(find "$root" -type d -name 'toolchain-aarch64*_gcc-*' | sort)
                ;;
            armv7|armhf|arm)
                while IFS= read -r candidate; do
                    base="$(basename "$candidate")"
                    package_arch="${base#toolchain-}"
                    package_arch="${package_arch%%_gcc-*}"
                    package_arch="${package_arch//+/_}"
                    if [[ "$package_arch" == arm_* ]]; then
                        echo "$package_arch"
                        return 0
                    fi
                done < <(find "$root" -type d -name 'toolchain-arm*_gcc-*' | sort)
                ;;
        esac
    done

    return 1
}

if [[ "$PACKAGE_ARCH" == "auto" ]]; then
    if detected_package_arch="$(detect_package_arch_from_toolchain "$ARCH")"; then
        PACKAGE_ARCH="$detected_package_arch"
    else
        case "$ARCH" in
            x86|i386|i686) PACKAGE_ARCH="i386" ;;
            aarch64|arm64) PACKAGE_ARCH="aarch64" ;;
            armv7|armhf|arm) PACKAGE_ARCH="armv7" ;;
            *) PACKAGE_ARCH="$ARCH" ;;
        esac
    fi
fi

for required_tool in ar tar; do
    if ! command -v "$required_tool" >/dev/null 2>&1; then
        echo "Error: required tool not found: $required_tool" >&2
        exit 1
    fi
done

if [[ ! -x "$BINARY_PATH" ]]; then
    echo "Error: binary not found or not executable: $BINARY_PATH" >&2
    echo "Build first: ARCH=$ARCH ./scripts/openwrt/build.sh" >&2
    exit 1
fi

if [[ ! -f "$INIT_SCRIPT" ]]; then
    echo "Error: init script not found: $INIT_SCRIPT" >&2
    exit 1
fi

if [[ ! -f "$CONFIG_FILE" ]]; then
    echo "Error: config file not found: $CONFIG_FILE" >&2
    exit 1
fi

if [[ "$PKG_VERSION" == "auto" ]]; then
    if command -v git >/dev/null 2>&1 && git -C "$REPO_ROOT" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
        git_sha="$(git -C "$REPO_ROOT" rev-parse --short=10 HEAD)"
        PKG_VERSION="${PKG_BASE_VERSION}+${git_sha}"
    else
        echo "Warning: git metadata unavailable; using ${PKG_BASE_VERSION}" >&2
        PKG_VERSION="$PKG_BASE_VERSION"
    fi
fi

PKG_BASENAME="${PKG_NAME}_${PKG_VERSION}-${PKG_RELEASE}_${PACKAGE_ARCH}"
PKG_OUTPUT="$OUT_DIR/${PKG_BASENAME}.ipk"

mkdir -p "$OUT_DIR"
if [[ "$CLEAN_OLD_PACKAGES" == "1" ]]; then
    find "$OUT_DIR" -maxdepth 1 -type f -name "${PKG_NAME}_*.ipk" -delete
fi
rm -rf "$WORK_DIR"
mkdir -p "$WORK_DIR/usr/bin" "$WORK_DIR/etc/init.d" "$WORK_DIR/etc/config" "$WORK_DIR/CONTROL"

cp "$BINARY_PATH" "$WORK_DIR/usr/bin/auretty"
cp "$INIT_SCRIPT" "$WORK_DIR/etc/init.d/auretty"
cp "$CONFIG_FILE" "$WORK_DIR/etc/config/auretty"

chmod +x "$WORK_DIR/usr/bin/auretty"
chmod +x "$WORK_DIR/etc/init.d/auretty"

cat > "$WORK_DIR/CONTROL/control" <<EOF
Package: ${PKG_NAME}
Version: ${PKG_VERSION}-${PKG_RELEASE}
Architecture: ${PACKAGE_ARCH}
Maintainer: ${PKG_MAINTAINER}
Section: ${PKG_SECTION}
Priority: ${PKG_PRIORITY}
Depends: ${PKG_DEPENDS}
Description: Terminal runtime with HTTP/WebSocket API
 AureTTY provides HTTP REST API and WebSocket transport
 for terminal sessions with multiplexing and reconnection support.
EOF

cat > "$WORK_DIR/CONTROL/conffiles" <<EOF
/etc/config/auretty
EOF

if command -v opkg-build >/dev/null 2>&1; then
    opkg-build "$WORK_DIR" "$OUT_DIR" >/dev/null
else
    TMP_DIR="$OUT_DIR/.tmp-${PKG_BASENAME}"
    rm -rf "$TMP_DIR"
    mkdir -p "$TMP_DIR"

    echo "2.0" > "$TMP_DIR/debian-binary"
    tar -C "$WORK_DIR/CONTROL" -czf "$TMP_DIR/control.tar.gz" .
    tar -C "$WORK_DIR" --exclude=CONTROL -czf "$TMP_DIR/data.tar.gz" .
    ar r "$PKG_OUTPUT" "$TMP_DIR/debian-binary" "$TMP_DIR/control.tar.gz" "$TMP_DIR/data.tar.gz" >/dev/null

    rm -rf "$TMP_DIR"
fi

if [[ ! -f "$PKG_OUTPUT" ]]; then
    echo "Error: failed to produce package: $PKG_OUTPUT" >&2
    exit 1
fi

if ! ar t "$PKG_OUTPUT" | grep -qx 'debian-binary'; then
    echo "Error: invalid ipk (missing debian-binary): $PKG_OUTPUT" >&2
    exit 1
fi

if ! ar t "$PKG_OUTPUT" | grep -qx 'control.tar.gz'; then
    echo "Error: invalid ipk (missing control.tar.gz): $PKG_OUTPUT" >&2
    exit 1
fi

if ! ar t "$PKG_OUTPUT" | grep -qx 'data.tar.gz'; then
    echo "Error: invalid ipk (missing data.tar.gz): $PKG_OUTPUT" >&2
    exit 1
fi

if [[ "$KEEP_WORK_DIR" != "1" ]]; then
    rm -rf "$WORK_DIR"
fi

echo "Built package: $PKG_OUTPUT"
