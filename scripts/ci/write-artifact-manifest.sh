#!/usr/bin/env bash
set -euo pipefail

# Writes a deterministic manifest with SHA256 + size for selected artifacts.
#
# Usage:
#   ./scripts/ci/write-artifact-manifest.sh <manifest-path> <file> [<file> ...]

if [[ "$#" -lt 2 ]]; then
    echo "Usage: $0 <manifest-path> <file> [<file> ...]" >&2
    exit 1
fi

if ! command -v sha256sum >/dev/null 2>&1; then
    echo "Error: sha256sum is required." >&2
    exit 1
fi

MANIFEST_PATH="$1"
shift

mkdir -p "$(dirname "$MANIFEST_PATH")"
TMP_MANIFEST="$(mktemp)"

{
    echo "# sha256 size_bytes path"
    for file_path in "$@"; do
        if [[ ! -f "$file_path" ]]; then
            echo "Error: artifact file not found: $file_path" >&2
            rm -f "$TMP_MANIFEST"
            exit 1
        fi

        sha="$(sha256sum "$file_path" | awk '{print $1}')"
        size_bytes="$(stat -c '%s' "$file_path")"
        printf '%s\t%s\t%s\n' "$sha" "$size_bytes" "$file_path"
    done | sort -k3
} >"$TMP_MANIFEST"

mv "$TMP_MANIFEST" "$MANIFEST_PATH"
echo "Wrote artifact manifest: $MANIFEST_PATH"

