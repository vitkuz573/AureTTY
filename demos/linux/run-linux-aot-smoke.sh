#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/../.." && pwd)"
auretty_executable="${AURETTY_EXECUTABLE:-${repo_root}/artifacts/publish/linux-x64-aot/AureTTY}"

if [[ ! -f "$auretty_executable" ]]; then
  echo "[linux-aot-smoke] AureTTY executable not found: ${auretty_executable}" >&2
  echo "[linux-aot-smoke] Publish first:" >&2
  echo "  dotnet publish src/AureTTY/AureTTY.csproj -f net10.0 -c Release -r linux-x64 --self-contained true -p:PublishAot=true -p:OpenApiGenerateDocuments=false -p:OpenApiGenerateDocumentsOnBuild=false -o artifacts/publish/linux-x64-aot" >&2
  exit 1
fi

if [[ ! -x "$auretty_executable" ]]; then
  echo "[linux-aot-smoke] AureTTY executable is not executable: ${auretty_executable}" >&2
  exit 1
fi

(
  cd "$repo_root"
  AURETTY_EXECUTABLE="$auretty_executable" bash demos/linux/run-linux-transport-smoke.sh
)
