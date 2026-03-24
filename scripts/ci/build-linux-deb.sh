#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

OUTPUT_DIR="$REPO_ROOT/artifacts/deb"
mkdir -p "$OUTPUT_DIR"

deb_amd64="$("$REPO_ROOT/scripts/linux/build-deb.sh" x64 "$OUTPUT_DIR")"
deb_arm64="$("$REPO_ROOT/scripts/linux/build-deb.sh" arm64 "$OUTPUT_DIR")"
deb_armhf="$("$REPO_ROOT/scripts/linux/build-deb.sh" arm "$OUTPUT_DIR")"

echo "Built Debian package: $deb_amd64"
echo "Built Debian package: $deb_arm64"
echo "Built Debian package: $deb_armhf"

"$REPO_ROOT/scripts/ci/write-artifact-manifest.sh" \
  "$REPO_ROOT/artifacts/deb/manifest.txt" \
  "$deb_amd64" \
  "$deb_arm64" \
  "$deb_armhf"

"$REPO_ROOT/scripts/ci/validate-artifact-manifest.sh" \
  "$REPO_ROOT/artifacts/deb/manifest.txt"
