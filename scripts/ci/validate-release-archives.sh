#!/usr/bin/env bash
set -euo pipefail

# Validates release zip archives produced by AppVeyor Linux build branch.
# Ensures expected payload is present and smoke/log artifacts are not leaked.

if [[ "$#" -lt 1 ]]; then
    echo "Usage: $0 <archive.zip> [<archive.zip> ...]" >&2
    exit 1
fi

if ! command -v zipinfo >/dev/null 2>&1; then
    echo "Error: zipinfo is required to validate release archives." >&2
    echo "Install package: unzip" >&2
    exit 1
fi

fail() {
    echo "Error: $1" >&2
    exit 1
}

archive_contains_regex() {
    local archive="$1"
    local regex="$2"
    zipinfo -1 "$archive" | grep -E -q "$regex"
}

assert_contains_regex() {
    local archive="$1"
    local regex="$2"
    local description="$3"
    if ! archive_contains_regex "$archive" "$regex"; then
        echo "Archive content dump for diagnostics: $archive" >&2
        zipinfo -1 "$archive" >&2 || true
        fail "Missing expected payload ($description) in archive: $archive"
    fi
}

assert_not_contains_regex() {
    local archive="$1"
    local regex="$2"
    local description="$3"
    if archive_contains_regex "$archive" "$regex"; then
        echo "Archive content dump for diagnostics: $archive" >&2
        zipinfo -1 "$archive" >&2 || true
        fail "Unexpected payload ($description) in archive: $archive"
    fi
}

validate_archive() {
    local archive="$1"
    local base

    [[ -f "$archive" ]] || fail "Archive not found: $archive"

    base="$(basename "$archive")"

    # Never ship smoke logs in release archives.
    assert_not_contains_regex "$archive" '(^|/)auretty-(api-smoke|emulated-server)\.log$' "smoke server log"
    assert_not_contains_regex "$archive" '(^|/)test-logs/' "test log directory"
    assert_not_contains_regex "$archive" '(^|/).*\.log$' "generic log file"

    case "$base" in
        AureTTY-*-linux-x64.zip|AureTTY-*-linux-x64-aot.zip|AureTTY-*-linux-arm64-aot.zip|AureTTY-*-linux-arm-aot.zip)
            assert_contains_regex "$archive" '(^|/)AureTTY$' "AureTTY binary"
            ;;
        AureTTY-*-openwrt-x86_64.zip|AureTTY-*-openwrt-aarch64.zip|AureTTY-*-openwrt-armv7.zip)
            assert_contains_regex "$archive" '(^|/)auretty$' "auretty binary"
            ;;
        AureTTY-*-openwrt-x86_64-ipk.zip|AureTTY-*-openwrt-aarch64-ipk.zip|AureTTY-*-openwrt-armv7-ipk.zip)
            assert_contains_regex "$archive" '\.ipk$' ".ipk payload"
            local ipk_count
            ipk_count="$(zipinfo -1 "$archive" | grep -E '\.ipk$' | wc -l | tr -d ' ')"
            if [[ "$ipk_count" != "1" ]]; then
                fail "Expected exactly one .ipk in archive ($archive), found: $ipk_count"
            fi
            ;;
        *)
            fail "Unsupported archive naming pattern: $base"
            ;;
    esac

    echo "Validated release archive: $archive"
}

for archive in "$@"; do
    validate_archive "$archive"
done
