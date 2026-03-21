#!/usr/bin/env bash
set -euo pipefail

# Validates .sha256 files and referenced payloads.
# Expected format per file:
#   <64-hex-sha256><two spaces><file-name>

if [[ "$#" -lt 1 ]]; then
    echo "Usage: $0 <checksum.sha256> [<checksum.sha256> ...]" >&2
    exit 1
fi

if ! command -v sha256sum >/dev/null 2>&1; then
    echo "Error: sha256sum is required." >&2
    exit 1
fi

validate_checksum_file() {
    local checksum_file="$1"
    local checksum_dir
    local checksum_name
    local target_name
    local line_count

    if [[ ! -f "$checksum_file" ]]; then
        echo "Error: checksum file not found: $checksum_file" >&2
        exit 1
    fi

    if [[ ! -s "$checksum_file" ]]; then
        echo "Error: checksum file is empty: $checksum_file" >&2
        exit 1
    fi

    line_count="$(wc -l < "$checksum_file" | tr -d ' ')"
    if [[ "$line_count" != "1" ]]; then
        echo "Error: checksum file must contain exactly one line: $checksum_file" >&2
        exit 1
    fi

    if ! grep -Eq '^[0-9a-fA-F]{64}  .+$' "$checksum_file"; then
        echo "Error: invalid checksum format in: $checksum_file" >&2
        echo "Expected: <64-hex-sha256><two spaces><file-name>" >&2
        exit 1
    fi

    checksum_dir="$(cd "$(dirname "$checksum_file")" && pwd)"
    checksum_name="$(basename "$checksum_file")"
    target_name="$(cut -d' ' -f3- "$checksum_file")"

    if [[ -z "$target_name" ]]; then
        echo "Error: missing referenced filename in: $checksum_file" >&2
        exit 1
    fi

    if [[ ! -f "$checksum_dir/$target_name" ]]; then
        echo "Error: referenced file does not exist: $checksum_dir/$target_name" >&2
        exit 1
    fi

    (
        cd "$checksum_dir"
        sha256sum -c "$checksum_name"
    )

    echo "Validated checksum: $checksum_file -> $target_name"
}

for checksum_file in "$@"; do
    validate_checksum_file "$checksum_file"
done
