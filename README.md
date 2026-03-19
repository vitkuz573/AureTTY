# AureTTY

[![Build](https://ci.appveyor.com/api/projects/status/github/vitkuz573/AureTTY?svg=true)](https://ci.appveyor.com/project/vitkuz573/AureTTY)
[![Latest Release](https://img.shields.io/github/v/release/vitkuz573/AureTTY?sort=semver)](https://github.com/vitkuz573/AureTTY/releases/latest)
[![Coverage](https://img.shields.io/badge/coverage-87.9%25-brightgreen)](#test-coverage)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE-MIT)
[![License: Apache-2.0](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE-APACHE)

AureTTY is a standalone terminal engine with transport-agnostic control APIs.
It can be integrated from any language through the HTTP API and SSE event stream.

## Integration Modes

- REST API (`/api/v1/*`) + SSE (`/api/v1/viewers/{viewerId}/events`) for language-agnostic clients.
- Local IPC API (named pipe) for co-located integrations.
- Both transports can run together.

Current platform backend:

- Linux backend: `AureTTY.Linux` (pseudo-terminal launch through `script`, util-linux).
- Windows backend: `AureTTY.Windows` (ConPTY/Windows process launch).
- Host multi-targeting: `net10.0` (Linux backend only) and `net10.0-windows` (Windows backend only).
- Direct host run/publish from project file should specify framework explicitly:
  - Linux: `-f net10.0`
  - Windows: `-f net10.0-windows`

## Quick Start

Run `AureTTY` as a foreground process or service and connect over HTTP:

- Base URL: `http://127.0.0.1:17850`
- API version: `v1`
- Auth header: `X-AureTTY-Key: <api-key>`

Defaults:

- `--http-listen-url` default: `http://127.0.0.1:17850`
- `--api-key` default: value of `--pipe-token`
- `--pipe-name` default: `auretty-terminal`
- `--pipe-token` default: `auretty-terminal`
- `--transport` default: `pipe,http`

## HTTP Endpoints

- `GET /api/v1/health`
- `GET /api/v1/sessions`
- `DELETE /api/v1/sessions`
- `GET /api/v1/viewers/{viewerId}/events` (SSE)
- `GET /api/v1/viewers/{viewerId}/sessions`
- `POST /api/v1/viewers/{viewerId}/sessions`
- `GET /api/v1/viewers/{viewerId}/sessions/{sessionId}`
- `POST /api/v1/viewers/{viewerId}/sessions/{sessionId}/attachments`
- `POST /api/v1/viewers/{viewerId}/sessions/{sessionId}/inputs`
- `GET /api/v1/viewers/{viewerId}/sessions/{sessionId}/input-diagnostics`
- `PUT /api/v1/viewers/{viewerId}/sessions/{sessionId}/terminal-size`
- `POST /api/v1/viewers/{viewerId}/sessions/{sessionId}/signals`
- `DELETE /api/v1/viewers/{viewerId}/sessions/{sessionId}`
- `DELETE /api/v1/viewers/{viewerId}/sessions`

OpenAPI document:

- `GET /openapi/v1.json`

## CLI/Environment Configuration

- `--transport pipe|http` (repeatable) / `AURETTY_TRANSPORTS=pipe,http`
- `--http-listen-url` / `AURETTY_HTTP_LISTEN_URL`
- `--api-key` / `AURETTY_API_KEY`
- `--pipe-name` / `AURETTY_PIPE_NAME`
- `--pipe-token` / `AURETTY_PIPE_TOKEN`

At least one transport must be enabled.

Linux notes:

- Install `script` binary (usually from `util-linux`).
- Explicit credential switching (`UserName`/`Password`) is not implemented yet on Linux.

Windows notes:

- Run with PowerShell 7 (`pwsh`) or Windows PowerShell (`powershell`) for demos.
- Full Windows transport smoke demo: `demos/windows/run-windows-transport-smoke.ps1`.
- NativeAOT Windows smoke demo (published binary): `demos/windows/run-windows-aot-smoke.ps1`.

Linux NativeAOT notes:

- NativeAOT Linux smoke demo (published binary): `demos/linux/run-linux-aot-smoke.sh`.

## Repository Layout

- `src/` runtime and platform projects
- `tests/` unit tests
- `demos/` runnable transport demos (`demos/linux/run-linux-transport-smoke.sh`, `demos/linux/run-linux-aot-smoke.sh`, `demos/windows/run-windows-transport-smoke.ps1`, `demos/windows/run-windows-aot-smoke.ps1`)

## Test Coverage

Current local baseline (2026-03-18):

- Line coverage: `87.9%`
- Branch coverage: `72.2%`

Recompute locally:

```powershell
dotnet test tests/AureTTY.Tests/AureTTY.Tests.csproj -c Debug --collect:"XPlat Code Coverage" --settings coverlet.runsettings --results-directory coverage-results/tests
dotnet test tests/AureTTY.Core.Tests/AureTTY.Core.Tests.csproj -c Debug --collect:"XPlat Code Coverage" --settings coverlet.runsettings --results-directory coverage-results/core
.\.tools\reportgenerator.exe -reports:"coverage-results\**\coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:"HtmlInline;TextSummary;Cobertura;Badges"
```

## NativeAOT (Preview)

Windows AOT publish (experimental):

```powershell
dotnet publish src/AureTTY/AureTTY.csproj -f net10.0-windows -c Release -r win-x64 --self-contained true -p:PublishAot=true -p:OpenApiGenerateDocuments=false -p:OpenApiGenerateDocumentsOnBuild=false -o artifacts/publish/win-x64-aot
pwsh -NoLogo -NoProfile -File demos/windows/run-windows-aot-smoke.ps1 -AureTTYExecutable artifacts/publish/win-x64-aot/AureTTY.exe
```

Linux AOT publish (experimental):

```bash
dotnet publish src/AureTTY/AureTTY.csproj -f net10.0 -c Release -r linux-x64 --self-contained true -p:PublishAot=true -p:OpenApiGenerateDocuments=false -p:OpenApiGenerateDocumentsOnBuild=false -o artifacts/publish/linux-x64-aot
bash demos/linux/run-linux-aot-smoke.sh
```

Notes:

- `PublishAot=true` build path switches HTTP routing to AOT-friendly minimal endpoints (no MVC controllers in AOT binary).
- Pipe transport JSON serialization uses source-generated metadata.
- OpenAPI document generation is disabled for AOT publish commands above.

## License

Dual licensed:

- MIT (`LICENSE-MIT`)
- Apache-2.0 (`LICENSE-APACHE`)
