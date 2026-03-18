#!/usr/bin/env bash
set -euo pipefail

dotnet run --project demos/pipe/AureTTY.Demo.PipeClient/AureTTY.Demo.PipeClient.csproj -- "$@"
