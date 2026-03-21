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
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=false \
  -p:OpenApiGenerateDocuments=false \
  -p:OpenApiGenerateDocumentsOnBuild=false \
  -o artifacts/publish/linux-x64

API_KEY="test-key" PORT="17856" START_TIMEOUT_SECONDS="60" BINARY_PATH="$REPO_ROOT/artifacts/publish/linux-x64/AureTTY" "$REPO_ROOT/scripts/linux/test-native-api.sh"
