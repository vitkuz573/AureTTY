#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

cd "$REPO_ROOT"

dotnet publish src/AureTTY/AureTTY.csproj \
  -f net10.0 \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishAot=true \
  -p:OpenApiGenerateDocuments=false \
  -p:OpenApiGenerateDocumentsOnBuild=false \
  -o artifacts/publish/linux-x64-aot

API_KEY="test-key" START_TIMEOUT_SECONDS="60" "$REPO_ROOT/scripts/linux/test-native-api.sh"

"$REPO_ROOT/scripts/ci/write-artifact-manifest.sh" \
  "$REPO_ROOT/artifacts/publish/linux-x64-manifest.txt" \
  "$REPO_ROOT/artifacts/publish/linux-x64-aot/AureTTY"

"$REPO_ROOT/scripts/ci/validate-artifact-manifest.sh" \
  "$REPO_ROOT/artifacts/publish/linux-x64-manifest.txt"
