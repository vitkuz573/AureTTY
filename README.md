# AureTTY

AureTTY is a standalone terminal engine with transport-agnostic control APIs.
It can be integrated from any language through the HTTP API and SSE event stream.

## Integration Modes

- REST API (`/api/v1/*`) + SSE (`/api/v1/viewers/{viewerId}/events`) for language-agnostic clients.
- Local IPC API (named pipe) for co-located integrations.
- Both transports can run together.

Current platform backend:

- Linux backend: `AureTTY.Linux` (pseudo-terminal launch through `script`, util-linux).
- Windows backend: `AureTTY.Windows` (ConPTY/Windows process launch).

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

## Repository Layout

- `src/` runtime and platform projects
- `tests/` unit tests
- `demos/` runnable transport demos (`demos/linux/run-linux-transport-smoke.sh`, `demos/windows/run-windows-transport-smoke.ps1`)

## License

Dual licensed:

- MIT (`LICENSE-MIT`)
- Apache-2.0 (`LICENSE-APACHE`)
