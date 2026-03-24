#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
OUTPUT_DIR="${1:-$REPO_ROOT/artifacts/nuget}"

mkdir -p "$OUTPUT_DIR"
rm -f "$OUTPUT_DIR"/*.nupkg "$OUTPUT_DIR"/*.snupkg 2>/dev/null || true

projects=(
    "$REPO_ROOT/src/AureTTY.Contracts/AureTTY.Contracts.csproj"
    "$REPO_ROOT/src/AureTTY.Execution/AureTTY.Execution.csproj"
    "$REPO_ROOT/src/AureTTY.Core/AureTTY.Core.csproj"
    "$REPO_ROOT/src/AureTTY.Protocol/AureTTY.Protocol.csproj"
    "$REPO_ROOT/src/AureTTY.Linux/AureTTY.Linux.csproj"
    "$REPO_ROOT/src/AureTTY.Windows/AureTTY.Windows.csproj"
)

for project in "${projects[@]}"; do
    dotnet pack "$project" \
        -c Release \
        --no-build \
        --include-symbols \
        --include-source \
        -o "$OUTPUT_DIR"
done

package_count="$(find "$OUTPUT_DIR" -maxdepth 1 -type f -name '*.nupkg' ! -name '*.symbols.nupkg' | wc -l | tr -d ' ')"
if [[ "$package_count" -lt 6 ]]; then
    echo "Error: expected at least 6 NuGet packages, produced: $package_count" >&2
    exit 1
fi

echo "Packed NuGet artifacts into: $OUTPUT_DIR"
