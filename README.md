# AureTTY

AureTTY is a standalone terminal engine with transport-agnostic control APIs.
It can be integrated from any language through the HTTP API and SSE event stream.

## Integration Modes

- HTTP API (`/v1/*`) + SSE (`/v1/viewers/{viewerId}/events`) for language-agnostic clients.
- Local IPC API (named pipe) for legacy/hosted integrations on Windows.
- Both transports can run together.

## Quick Start

Run `AureTTY.exe` (Windows service or foreground) and connect over HTTP:

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

- `GET /v1/health`
- `GET /v1/viewers/{viewerId}/events` (SSE)
- `POST /v1/viewers/{viewerId}/sessions/start`
- `POST /v1/viewers/{viewerId}/sessions/resume`
- `POST /v1/viewers/{viewerId}/sessions/input`
- `GET /v1/viewers/{viewerId}/sessions/{sessionId}/input-diagnostics`
- `POST /v1/viewers/{viewerId}/sessions/resize`
- `POST /v1/viewers/{viewerId}/sessions/{sessionId}/signal/{signal}`
- `DELETE /v1/viewers/{viewerId}/sessions/{sessionId}`
- `DELETE /v1/viewers/{viewerId}/sessions`
- `DELETE /v1/sessions`

OpenAPI document:

- `GET /openapi/v1.json`

## CLI/Environment Configuration

- `--transport pipe|http` (repeatable) / `AURETTY_TRANSPORTS=pipe,http`
- `--http-listen-url` / `AURETTY_HTTP_LISTEN_URL`
- `--api-key` / `AURETTY_API_KEY`
- `--pipe-name` / `AURETTY_PIPE_NAME`
- `--pipe-token` / `AURETTY_PIPE_TOKEN`

At least one transport must be enabled.

## Repository Layout

- `src/` runtime and platform projects
- `tests/` unit tests

## License

Dual licensed:

- MIT (`LICENSE-MIT`)
- Apache-2.0 (`LICENSE-APACHE`)
