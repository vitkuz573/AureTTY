#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

ARCH_INPUT="${1:-x64}"
OUTPUT_DIR="${2:-$REPO_ROOT/artifacts/deb}"

if ! command -v dpkg-deb >/dev/null 2>&1; then
    echo "Error: dpkg-deb is required to build .deb packages." >&2
    echo "Install package: dpkg-dev" >&2
    exit 1
fi

resolve_arch() {
    local arch="$1"
    case "$arch" in
        x64|amd64)
            echo "linux-x64-aot amd64"
            ;;
        arm64|aarch64)
            echo "linux-arm64-aot arm64"
            ;;
        arm|armhf|armv7)
            echo "linux-arm-aot armhf"
            ;;
        *)
            echo "Unsupported architecture: $arch" >&2
            exit 1
            ;;
    esac
}

sanitize_version() {
    local raw="$1"
    local normalized

    normalized="${raw#v}"
    normalized="$(echo "$normalized" | sed -E 's/[^0-9A-Za-z.+:~-]/-/g')"
    normalized="$(echo "$normalized" | sed -E 's/^-+//; s/-+$//')"
    normalized="$(echo "$normalized" | sed -E 's/-{2,}/-/g')"

    if [[ -z "$normalized" ]]; then
        echo "Error: failed to derive a valid Debian package version from '$raw'." >&2
        exit 1
    fi

    echo "$normalized"
}

read -r publish_dir_name deb_arch <<< "$(resolve_arch "$ARCH_INPUT")"
publish_dir="$REPO_ROOT/artifacts/publish/$publish_dir_name"
binary_path="$publish_dir/AureTTY"

if [[ ! -f "$binary_path" ]]; then
    echo "Error: publish binary not found: $binary_path" >&2
    echo "Build required artifact first (for example scripts/ci/build-linux-x64-aot.sh)." >&2
    exit 1
fi

raw_version="${AURETTY_DEB_VERSION:-${APPVEYOR_REPO_TAG_NAME:-}}"
if [[ -z "$raw_version" ]]; then
    short_sha="$(git -C "$REPO_ROOT" rev-parse --short HEAD)"
    raw_version="0.0.0+$short_sha"
fi

deb_version="$(sanitize_version "$raw_version")"
deb_release="${AURETTY_DEB_RELEASE:-1}"
package_version="${deb_version}-${deb_release}"
package_file_name="auretty_${package_version}_${deb_arch}.deb"

mkdir -p "$OUTPUT_DIR"

work_dir="$(mktemp -d)"
cleanup() {
    rm -rf "$work_dir"
}
trap cleanup EXIT

staging_dir="$work_dir/root"
debian_dir="$staging_dir/DEBIAN"

mkdir -p \
    "$debian_dir" \
    "$staging_dir/usr/lib/auretty" \
    "$staging_dir/usr/bin" \
    "$staging_dir/etc/auretty" \
    "$staging_dir/lib/systemd/system"

cp -a "$publish_dir/." "$staging_dir/usr/lib/auretty/"
find "$staging_dir/usr/lib/auretty" -maxdepth 1 -type f -name '*.pdb' -delete
chmod 0755 "$staging_dir/usr/lib/auretty/AureTTY"
ln -s /usr/lib/auretty/AureTTY "$staging_dir/usr/bin/auretty"

install -m 0644 "$REPO_ROOT/package/debian/files/auretty.service" "$staging_dir/lib/systemd/system/auretty.service"
install -m 0644 "$REPO_ROOT/package/debian/files/auretty.env" "$staging_dir/etc/auretty/auretty.env"

cat > "$debian_dir/control" <<EOF
Package: auretty
Version: ${package_version}
Section: net
Priority: optional
Architecture: ${deb_arch}
Maintainer: Vitaly Kuzyaev <vitkuz573@gmail.com>
Homepage: https://github.com/vitkuz573/AureTTY
Depends: util-linux
Recommends: systemd
Description: AureTTY terminal runtime service
 AureTTY is a high-performance standalone terminal runtime with HTTP REST,
 WebSocket, and local IPC transports for terminal session management.
EOF

cat > "$debian_dir/conffiles" <<EOF
/etc/auretty/auretty.env
EOF

install -m 0755 "$REPO_ROOT/package/debian/scripts/postinst" "$debian_dir/postinst"
install -m 0755 "$REPO_ROOT/package/debian/scripts/prerm" "$debian_dir/prerm"
install -m 0755 "$REPO_ROOT/package/debian/scripts/postrm" "$debian_dir/postrm"

package_path="$OUTPUT_DIR/$package_file_name"
dpkg-deb --root-owner-group --build "$staging_dir" "$package_path" >/dev/null

echo "$package_path"
