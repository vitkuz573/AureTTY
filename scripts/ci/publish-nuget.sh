#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
PACKAGE_DIR="${1:-$REPO_ROOT/artifacts/nuget}"

NUGET_SOURCE="${NUGET_SOURCE:-https://api.nuget.org/v3/index.json}"
NUGET_SYMBOL_SOURCE="${NUGET_SYMBOL_SOURCE:-https://api.nuget.org/v3/index.json}"
NUGET_SYMBOL_API_KEY="${NUGET_SYMBOL_API_KEY:-${NUGET_API_KEY:-}}"

if [[ -z "${NUGET_API_KEY:-}" ]]; then
    echo "Error: NUGET_API_KEY is required for NuGet publishing." >&2
    exit 1
fi

if [[ ! -d "$PACKAGE_DIR" ]]; then
    echo "Error: package directory not found: $PACKAGE_DIR" >&2
    exit 1
fi

shopt -s nullglob
packages=("$PACKAGE_DIR"/*.nupkg)
shopt -u nullglob

if [[ "${#packages[@]}" -eq 0 ]]; then
    echo "Error: no .nupkg files found in $PACKAGE_DIR" >&2
    exit 1
fi

for package in "${packages[@]}"; do
    if [[ "$package" == *.symbols.nupkg ]]; then
        continue
    fi

    dotnet nuget push "$package" \
        --api-key "$NUGET_API_KEY" \
        --source "$NUGET_SOURCE" \
        --skip-duplicate \
        --symbol-source "$NUGET_SYMBOL_SOURCE" \
        --symbol-api-key "$NUGET_SYMBOL_API_KEY"

    echo "Published NuGet package: $(basename "$package")"
done
