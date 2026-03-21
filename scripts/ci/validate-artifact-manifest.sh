#!/usr/bin/env bash
set -euo pipefail

# Validates artifact manifest produced by write-artifact-manifest.sh.
# Manifest format:
#   # sha256 size_bytes path
#   <sha256>\t<size>\t<absolute path>

if [[ "$#" -ne 1 ]]; then
    echo "Usage: $0 <manifest-path>" >&2
    exit 1
fi

if ! command -v sha256sum >/dev/null 2>&1; then
    echo "Error: sha256sum is required." >&2
    exit 1
fi

MANIFEST_PATH="$1"
[[ -f "$MANIFEST_PATH" ]] || {
    echo "Error: manifest not found: $MANIFEST_PATH" >&2
    exit 1
}

first_line="$(head -n 1 "$MANIFEST_PATH" || true)"
if [[ "$first_line" != "# sha256 size_bytes path" ]]; then
    echo "Error: invalid manifest header in $MANIFEST_PATH" >&2
    echo "Expected: # sha256 size_bytes path" >&2
    echo "Actual:   ${first_line:-<empty>}" >&2
    exit 1
fi

entries=0
prev_path=""

while IFS=$'\t' read -r expected_sha expected_size artifact_path; do
    [[ -z "${expected_sha}${expected_size}${artifact_path}" ]] && continue
    [[ "$expected_sha" == \#* ]] && continue

    entries=$((entries + 1))

    if [[ -z "$expected_sha" || -z "$expected_size" || -z "$artifact_path" ]]; then
        echo "Error: malformed manifest entry in $MANIFEST_PATH" >&2
        exit 1
    fi

    if [[ -n "$prev_path" && "$artifact_path" < "$prev_path" ]]; then
        echo "Error: manifest entries are not sorted by path in $MANIFEST_PATH" >&2
        exit 1
    fi
    prev_path="$artifact_path"

    if [[ ! -f "$artifact_path" ]]; then
        echo "Error: manifest artifact file missing: $artifact_path" >&2
        exit 1
    fi

    actual_size="$(stat -c '%s' "$artifact_path")"
    if [[ "$actual_size" != "$expected_size" ]]; then
        echo "Error: size mismatch for $artifact_path" >&2
        echo "Expected: $expected_size" >&2
        echo "Actual:   $actual_size" >&2
        exit 1
    fi

    actual_sha="$(sha256sum "$artifact_path" | awk '{print $1}')"
    if [[ "$actual_sha" != "$expected_sha" ]]; then
        echo "Error: sha256 mismatch for $artifact_path" >&2
        echo "Expected: $expected_sha" >&2
        echo "Actual:   $actual_sha" >&2
        exit 1
    fi
done <"$MANIFEST_PATH"

if [[ "$entries" -lt 1 ]]; then
    echo "Error: manifest has no artifact entries: $MANIFEST_PATH" >&2
    exit 1
fi

echo "Validated artifact manifest: $MANIFEST_PATH"

