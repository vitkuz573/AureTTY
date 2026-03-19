param(
    [string]$BaseUrl = $(if ([string]::IsNullOrWhiteSpace($env:AURETTY_BASE_URL)) { "http://127.0.0.1:17850" } else { $env:AURETTY_BASE_URL }),
    [string]$ApiKey = $(if ([string]::IsNullOrWhiteSpace($env:AURETTY_API_KEY)) { "auretty-terminal-token" } else { $env:AURETTY_API_KEY }),
    [string]$ViewerId = $(if ([string]::IsNullOrWhiteSpace($env:AURETTY_VIEWER_ID)) { "demo-http-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())" } else { $env:AURETTY_VIEWER_ID }),
    [string]$SessionId = $(if ([string]::IsNullOrWhiteSpace($env:AURETTY_SESSION_ID)) { "demo-http-session-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())-$([System.Guid]::NewGuid().ToString('N').Substring(0, 6))" } else { $env:AURETTY_SESSION_ID }),
    [ValidateSet("cmd", "powershell", "pwsh", "bash")]
    [string]$Shell = $(if ([string]::IsNullOrWhiteSpace($env:AURETTY_SHELL)) { "pwsh" } else { $env:AURETTY_SHELL })
)

$ErrorActionPreference = "Stop"

function Get-ShellEnumValue {
    param([string]$ShellName)

    switch ($ShellName.ToLowerInvariant()) {
        "cmd" { return 0 }
        "powershell" { return 1 }
        "pwsh" { return 2 }
        "bash" { return 3 }
        default { throw "Unsupported shell '$ShellName'." }
    }
}

function Get-DemoInputText {
    param([string]$ShellName)

    switch ($ShellName.ToLowerInvariant()) {
        "cmd" { return "echo demo-http && ver`r`n" }
        "powershell" { return "Write-Output demo-http`r`n`$PSVersionTable.PSEdition`r`n" }
        "pwsh" { return "Write-Output demo-http`r`n`$PSVersionTable.PSEdition`r`n" }
        "bash" { return "echo demo-http && uname -s`n" }
        default { throw "Unsupported shell '$ShellName'." }
    }
}

$headers = @{
    "X-AureTTY-Key" = $ApiKey
}

Write-Host "[http-demo] Health check..."
$healthJson = Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/v1/health" -Headers $headers
Write-Host ("[http-demo] Health: {0}" -f ($healthJson | ConvertTo-Json -Compress))

Write-Host ("[http-demo] Creating session '{0}' for viewer '{1}'..." -f $SessionId, $ViewerId)
$createPayload = @{
    sessionId = $SessionId
    shell = Get-ShellEnumValue -ShellName $Shell
} | ConvertTo-Json
$createJson = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/v1/viewers/$ViewerId/sessions" -Headers $headers -ContentType "application/json" -Body $createPayload
$createdSessionId = $createJson.sessionId
if ([string]::IsNullOrWhiteSpace($createdSessionId)) {
    throw "Session creation response is invalid: $($createJson | ConvertTo-Json -Compress)"
}

Write-Host "[http-demo] Sending input..."
$inputPayload = @{
    text = Get-DemoInputText -ShellName $Shell
    sequence = 1
} | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/v1/viewers/$ViewerId/sessions/$createdSessionId/inputs" -Headers $headers -ContentType "application/json" -Body $inputPayload | Out-Null

Write-Host "[http-demo] Reading diagnostics..."
$diagnosticsJson = Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/v1/viewers/$ViewerId/sessions/$createdSessionId/input-diagnostics" -Headers $headers
$diagnosticsPreview = $diagnosticsJson | Select-Object sessionId, viewerId, state, nextExpectedSequence, lastAcceptedSequence
Write-Host ("[http-demo] Diagnostics: {0}" -f ($diagnosticsPreview | ConvertTo-Json -Compress))

Write-Host "[http-demo] Closing viewer sessions..."
Invoke-RestMethod -Method Delete -Uri "$BaseUrl/api/v1/viewers/$ViewerId/sessions" -Headers $headers | Out-Null

Write-Host "[http-demo] Completed successfully."
