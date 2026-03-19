# AureTTY

[![Build](https://ci.appveyor.com/api/projects/status/github/vitkuz573/AureTTY?svg=true)](https://ci.appveyor.com/project/vitkuz573/AureTTY)
[![Latest Release](https://img.shields.io/github/v/release/vitkuz573/AureTTY?sort=semver)](https://github.com/vitkuz573/AureTTY/releases/latest)
[![Coverage](https://img.shields.io/badge/coverage-87.9%25-brightgreen)](#test-coverage)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE-MIT)
[![License: Apache-2.0](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE-APACHE)

AureTTY is a standalone terminal runtime with HTTP+SSE and local pipe transports.

## Integration Modes

- REST API (`/api/v1/*`) + SSE (`/api/v1/viewers/{viewerId}/events`) for language-agnostic clients.
- Local IPC API (named pipe) for co-located integrations.
- Both transports can run together.

Platform backends:

- Linux backend: `AureTTY.Linux` (PTY launch through `script` from `util-linux`).
- Windows backend: `AureTTY.Windows` (ConPTY/native Windows process launch).
- Host multi-targeting:
  - Linux: `net10.0`
  - Windows: `net10.0-windows` and `net10.0`

## Breaking Security Model

- HTTP API key is mandatory when HTTP transport is enabled.
- Pipe token is mandatory when pipe transport is enabled.
- API key is accepted only via `X-AureTTY-Key` header by default.
- Query parameter auth (`api_key`) is disabled by default.

## Quick Start

Run with both transports:

```powershell
dotnet run --project src/AureTTY/AureTTY.csproj -f net10.0-windows -c Debug -- `
  --transport pipe --transport http `
  --pipe-name auretty-terminal `
  --pipe-token auretty-terminal-token `
  --http-listen-url http://127.0.0.1:17850 `
  --api-key auretty-terminal-token
```

Linux host uses the same arguments, with `-f net10.0`.

## Defaults

- `--transport` default: `pipe,http`
- `--pipe-name` default: `auretty-terminal`
- `--http-listen-url` default: `http://127.0.0.1:17850`
- `--pipe-token`: no default (required when pipe is enabled)
- `--api-key`: no default (required when HTTP is enabled)

Runtime limits defaults:

- `--max-concurrent-sessions`: `32`
- `--max-sessions-per-viewer`: `8`
- `--replay-buffer-capacity`: `4096`
- `--max-pending-input-chunks`: `8192`
- `--sse-subscription-buffer-capacity`: `2048`

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

- `GET /openapi/v1.json` (requires `X-AureTTY-Key` when HTTP auth is enabled)

SSE backpressure notice:

- Stream may emit `TerminalSessionEventType.Dropped` system events when a subscriber is too slow and events are dropped.

## CLI / Environment

- `--transport pipe|http` (repeatable) / `AURETTY_TRANSPORTS=pipe,http`
- `--pipe-name` / `AURETTY_PIPE_NAME`
- `--pipe-token` / `AURETTY_PIPE_TOKEN`
- `--http-listen-url` / `AURETTY_HTTP_LISTEN_URL`
- `--api-key` / `AURETTY_API_KEY`
- `--max-concurrent-sessions` / `AURETTY_MAX_CONCURRENT_SESSIONS`
- `--max-sessions-per-viewer` / `AURETTY_MAX_SESSIONS_PER_VIEWER`
- `--replay-buffer-capacity` / `AURETTY_REPLAY_BUFFER_CAPACITY`
- `--max-pending-input-chunks` / `AURETTY_MAX_PENDING_INPUT_CHUNKS`
- `--sse-subscription-buffer-capacity` / `AURETTY_SSE_SUBSCRIPTION_BUFFER_CAPACITY`
- `--allow-api-key-query` / `AURETTY_ALLOW_API_KEY_QUERY`

At least one transport must be enabled.

## Platform Notes

Linux:

- Install `script` binary (`util-linux`).
- Explicit credential switching on Linux uses `sudo -S`; host must provide `sudo`.

Windows:

- Run demos with PowerShell 7 (`pwsh`) or Windows PowerShell (`powershell`).
- Windows transport smoke demo: `demos/windows/run-windows-transport-smoke.ps1`.
- Windows NativeAOT smoke demo: `demos/windows/run-windows-aot-smoke.ps1`.

Linux NativeAOT:

- Linux NativeAOT smoke demo: `demos/linux/run-linux-aot-smoke.sh`.

## Repository Layout

- `src/` runtime and platform projects
- `tests/` unit tests
- `demos/` runnable transport demos

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

## NativeAOT

Windows AOT publish:

```powershell
dotnet publish src/AureTTY/AureTTY.csproj -f net10.0-windows -c Release -r win-x64 --self-contained true -p:PublishAot=true -p:OpenApiGenerateDocuments=false -p:OpenApiGenerateDocumentsOnBuild=false -o artifacts/publish/win-x64-aot
pwsh -NoLogo -NoProfile -File demos/windows/run-windows-aot-smoke.ps1 -AureTTYExecutable artifacts/publish/win-x64-aot/AureTTY.exe
```

Linux AOT publish:

```bash
dotnet publish src/AureTTY/AureTTY.csproj -f net10.0 -c Release -r linux-x64 --self-contained true -p:PublishAot=true -p:OpenApiGenerateDocuments=false -p:OpenApiGenerateDocumentsOnBuild=false -o artifacts/publish/linux-x64-aot
bash demos/linux/run-linux-aot-smoke.sh
```

## License

Dual licensed:

- MIT (`LICENSE-MIT`)
- Apache-2.0 (`LICENSE-APACHE`)
