param(
    [string]$BaseUrl = $(if ([string]::IsNullOrWhiteSpace($env:AURETTY_BASE_URL)) { "http://127.0.0.1:17850" } else { $env:AURETTY_BASE_URL }),
    [string]$ApiKey = $(if ([string]::IsNullOrWhiteSpace($env:AURETTY_API_KEY)) { "demo-windows-api-key" } else { $env:AURETTY_API_KEY }),
    [string]$PipeName = $(if ([string]::IsNullOrWhiteSpace($env:AURETTY_PIPE_NAME)) { "demo-windows-pipe-$([System.Guid]::NewGuid().ToString('N').Substring(0, 8))" } else { $env:AURETTY_PIPE_NAME }),
    [string]$PipeToken = $(if ([string]::IsNullOrWhiteSpace($env:AURETTY_PIPE_TOKEN)) { "demo-windows-pipe-token-$([System.Guid]::NewGuid().ToString('N').Substring(0, 8))" } else { $env:AURETTY_PIPE_TOKEN }),
    [string]$HttpViewerId = $(if ([string]::IsNullOrWhiteSpace($env:AURETTY_HTTP_VIEWER_ID)) { "demo-http-viewer-$([System.Guid]::NewGuid().ToString('N').Substring(0, 8))" } else { $env:AURETTY_HTTP_VIEWER_ID }),
    [string]$PipeViewerId = $(if ([string]::IsNullOrWhiteSpace($env:AURETTY_PIPE_VIEWER_ID)) { "demo-pipe-viewer-$([System.Guid]::NewGuid().ToString('N').Substring(0, 8))" } else { $env:AURETTY_PIPE_VIEWER_ID }),
    [string]$PipeSessionId = $(if ([string]::IsNullOrWhiteSpace($env:AURETTY_PIPE_SESSION_ID)) { "demo-pipe-session-$([System.Guid]::NewGuid().ToString('N'))" } else { $env:AURETTY_PIPE_SESSION_ID }),
    [string]$LogDir = $(if ([string]::IsNullOrWhiteSpace($env:AURETTY_SMOKE_LOG_DIR)) { (Join-Path $env:TEMP "auretty-demos") } else { $env:AURETTY_SMOKE_LOG_DIR })
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "[windows-smoke] dotnet SDK is required."
}

if (-not (Get-Command pwsh -ErrorAction SilentlyContinue)) {
    throw "[windows-smoke] pwsh is required."
}

if (-not (Test-Path $LogDir)) {
    New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
}

$serverStdOutPath = Join-Path $LogDir "auretty-windows-smoke-server.out.log"
$serverStdErrPath = Join-Path $LogDir "auretty-windows-smoke-server.err.log"
$server = $null

try {
    Write-Host "[windows-smoke] Starting AureTTY (http + pipe)..."
    $serverArgs = @(
        "run",
        "--project", "src/AureTTY/AureTTY.csproj",
        "-f", "net10.0-windows",
        "-c", "Debug",
        "--",
        "--transport", "pipe",
        "--transport", "http",
        "--http-listen-url", $BaseUrl,
        "--api-key", $ApiKey,
        "--pipe-name", $PipeName,
        "--pipe-token", $PipeToken
    )
    $server = Start-Process -FilePath "dotnet" -ArgumentList $serverArgs -RedirectStandardOutput $serverStdOutPath -RedirectStandardError $serverStdErrPath -PassThru

    Write-Host "[windows-smoke] Waiting for health endpoint..."
    $healthReady = $false
    for ($attempt = 0; $attempt -lt 120; $attempt++) {
        if ($server.HasExited) {
            throw "[windows-smoke] AureTTY exited during startup. Logs: $serverStdOutPath ; $serverStdErrPath"
        }

        try {
            Invoke-WebRequest -Method Get -Uri "$BaseUrl/api/v1/health" -Headers @{ "X-AureTTY-Key" = $ApiKey } | Out-Null
            $healthReady = $true
            break
        }
        catch {
            Start-Sleep -Milliseconds 250
        }
    }

    if (-not $healthReady) {
        throw "[windows-smoke] Health endpoint did not become ready. Logs: $serverStdOutPath ; $serverStdErrPath"
    }

    Write-Host "[windows-smoke] Running HTTP demo..."
    & pwsh -NoLogo -NoProfile -File "demos/http/http-demo.ps1" `
        -BaseUrl $BaseUrl `
        -ApiKey $ApiKey `
        -ViewerId $HttpViewerId `
        -Shell "pwsh"
    if ($LASTEXITCODE -ne 0) {
        throw "[windows-smoke] HTTP demo failed with exit code $LASTEXITCODE."
    }

    Write-Host "[windows-smoke] Running pipe demo..."
    dotnet run --project demos/pipe/AureTTY.Demo.PipeClient/AureTTY.Demo.PipeClient.csproj -- `
        --pipe-name $PipeName `
        --pipe-token $PipeToken `
        --viewer-id $PipeViewerId `
        --session-id $PipeSessionId `
        --shell pwsh
    if ($LASTEXITCODE -ne 0) {
        throw "[windows-smoke] Pipe demo failed with exit code $LASTEXITCODE."
    }

    Write-Host "[windows-smoke] Verifying no leaked sessions..."
    $allSessions = Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/v1/sessions" -Headers @{ "X-AureTTY-Key" = $ApiKey }
    $remainingSessions = @($allSessions).Count
    if ($remainingSessions -ne 0) {
        throw "[windows-smoke] Expected zero active sessions, got $remainingSessions."
    }

    Write-Host "[windows-smoke] Completed successfully. Logs: $serverStdOutPath ; $serverStdErrPath"
}
finally {
    if ($null -ne $server -and -not $server.HasExited) {
        Stop-Process -Id $server.Id -Force
    }
}
