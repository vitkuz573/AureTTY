# Demos

This folder contains runnable integration demos for AureTTY transports.

## Prerequisites

- Linux host with `script` binary (from `util-linux`).
- .NET SDK 10.
- `curl` and `jq`.

## Run Full Linux Smoke (HTTP + Pipe)

```bash
bash demos/linux/run-linux-transport-smoke.sh
```

The smoke script:

- starts AureTTY with both transports enabled,
- runs the HTTP demo,
- runs the pipe demo,
- verifies that all sessions are cleaned up.

## Run HTTP Demo Only

```bash
AURETTY_BASE_URL=http://127.0.0.1:17850 \
AURETTY_API_KEY=auretty-terminal \
bash demos/http/http-demo.sh
```

## Run Pipe Demo Only

```bash
dotnet run --project demos/pipe/AureTTY.Demo.PipeClient/AureTTY.Demo.PipeClient.csproj -- \
  --pipe-name auretty-terminal \
  --pipe-token auretty-terminal \
  --shell bash
```
