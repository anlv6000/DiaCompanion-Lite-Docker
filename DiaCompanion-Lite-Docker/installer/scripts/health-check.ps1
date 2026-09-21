[CmdletBinding()]
param(
    [int]$WebPort = 8080,
    [int]$TimeoutSeconds = 30
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Wait-Http([string]$Uri) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        try {
            $response = Invoke-WebRequest -Uri $Uri -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -eq 200) { return $response }
        } catch { }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)
    throw "Health check failed: $Uri"
}

$ai = Wait-Http 'http://127.0.0.1:8000/health'
$backend = Wait-Http "http://127.0.0.1:$WebPort/health"
$web = Wait-Http "http://127.0.0.1:$WebPort/"
$aiBody = $ai.Content | ConvertFrom-Json
$backendBody = $backend.Content | ConvertFrom-Json
if ($aiBody.status -ne 'ok') { throw 'AI health status is not ok.' }
if ($backendBody.status -ne 'healthy') { throw 'Backend health status is not healthy.' }
Write-Host "PASS: AI, database-backed Backend, and Web are healthy on port $WebPort."
