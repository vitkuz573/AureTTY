#!/usr/bin/env bash
set -euo pipefail

if [[ "$#" -lt 1 ]]; then
    echo "Usage: $0 <package.deb> [<package.deb> ...]" >&2
    exit 1
fi

if ! command -v dpkg-deb >/dev/null 2>&1; then
    echo "Error: dpkg-deb is required to validate .deb packages." >&2
    exit 1
fi

fail() {
    echo "Error: $1" >&2
    exit 1
}

assert_data_entry() {
    local package="$1"
    local path="$2"
    local file_list
    file_list="$(dpkg-deb --fsys-tarfile "$package" | tar -tf -)"
    if ! grep -Fxq "$path" <<< "$file_list"; then
        fail "Missing payload '$path' in package: $package"
    fi
}

validate_package() {
    local package="$1"
    local package_name
    local architecture
    local version
    local control_dir

    [[ -f "$package" ]] || fail "Package not found: $package"

    package_name="$(dpkg-deb -f "$package" Package || true)"
    architecture="$(dpkg-deb -f "$package" Architecture || true)"
    version="$(dpkg-deb -f "$package" Version || true)"

    [[ "$package_name" == "auretty" ]] || fail "Unexpected package name '$package_name' in $package"
    [[ -n "$version" ]] || fail "Version is empty in package: $package"
    case "$architecture" in
        amd64|arm64|armhf)
            ;;
        *)
            fail "Unexpected architecture '$architecture' in package: $package"
            ;;
    esac

    assert_data_entry "$package" "./usr/lib/auretty/AureTTY"
    assert_data_entry "$package" "./usr/bin/auretty"
    assert_data_entry "$package" "./lib/systemd/system/auretty.service"
    assert_data_entry "$package" "./etc/auretty/auretty.env"

    control_dir="$(mktemp -d)"
    trap 'rm -rf "$control_dir"' RETURN
    dpkg-deb --control "$package" "$control_dir"

    [[ -f "$control_dir/postinst" ]] || fail "Missing postinst in package: $package"
    [[ -f "$control_dir/prerm" ]] || fail "Missing prerm in package: $package"
    [[ -f "$control_dir/postrm" ]] || fail "Missing postrm in package: $package"
    [[ -f "$control_dir/conffiles" ]] || fail "Missing conffiles in package: $package"

    if ! grep -Fxq '/etc/auretty/auretty.env' "$control_dir/conffiles"; then
        fail "Conffiles entry '/etc/auretty/auretty.env' missing in package: $package"
    fi

    rm -rf "$control_dir"
    trap - RETURN

    echo "Validated Debian package: $package"
}

for package in "$@"; do
    validate_package "$package"
done
