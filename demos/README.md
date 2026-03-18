# Demos

This folder contains runnable integration demos for AureTTY transports.

## Prerequisites

- .NET SDK 10.

Linux extras:

- `script` binary (from `util-linux`)
- `curl`
- `jq`
- `bash`

Windows extras:

- PowerShell 7 (`pwsh`)

## Run Full Linux Smoke (HTTP + Pipe)

```bash
bash demos/linux/run-linux-transport-smoke.sh
```

The smoke script:

- starts AureTTY with both transports enabled,
- runs the HTTP demo,
- runs the pipe demo,
- verifies that all sessions are cleaned up.

## Run Full Windows Smoke (HTTP + Pipe)

```powershell
pwsh -NoLogo -NoProfile -File demos/windows/run-windows-transport-smoke.ps1
```

The smoke script:

- starts AureTTY with both transports enabled,
- runs the HTTP demo,
- runs the pipe demo,
- verifies that all sessions are cleaned up.

## Run HTTP Demo Only (Linux)

```bash
AURETTY_BASE_URL=http://127.0.0.1:17850 \
AURETTY_API_KEY=auretty-terminal \
bash demos/http/http-demo.sh
```

## Run HTTP Demo Only (Windows)

```powershell
pwsh -NoLogo -NoProfile -File demos/http/http-demo.ps1 `
  -BaseUrl http://127.0.0.1:17850 `
  -ApiKey auretty-terminal `
  -Shell pwsh
```

## Run Pipe Demo Only (Linux)

```bash
dotnet run --project demos/pipe/AureTTY.Demo.PipeClient/AureTTY.Demo.PipeClient.csproj -- \
  --pipe-name auretty-terminal \
  --pipe-token auretty-terminal \
  --shell bash
```

## Run Pipe Demo Only (Windows)

```powershell
pwsh -NoLogo -NoProfile -File demos/pipe/run-pipe-demo.ps1 `
  --pipe-name auretty-terminal `
  --pipe-token auretty-terminal `
  --shell pwsh
```
