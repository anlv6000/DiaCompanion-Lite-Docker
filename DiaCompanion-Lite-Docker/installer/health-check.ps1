[CmdletBinding()]
param(
    [int]$WebPort = 8080,
    [int]$TimeoutSeconds = 240   # lần đầu AI nạp PyTorch/TensorFlow khá lâu trên CPU
)
. (Join-Path $PSScriptRoot 'common.ps1')

function Wait-Http([string]$Name, [string]$Uri, [scriptblock]$Ok) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $last = ''
    do {
        try {
            $r = Invoke-WebRequest -Uri $Uri -UseBasicParsing -TimeoutSec 10
            if ($r.StatusCode -eq 200 -and (& $Ok $r)) { Write-Host "  [OK]  $Name"; return }
            $last = "HTTP $($r.StatusCode): $($r.Content)"
        } catch { $last = $_.Exception.Message }
        Start-Sleep -Seconds 3
    } while ((Get-Date) -lt $deadline)
    throw "$Name không phản hồi ($Uri): $last"
}

Write-Host 'Kiểm tra hệ thống...'
foreach ($id in $script:Services) {
    $s = Get-Service $id -ErrorAction SilentlyContinue
    Write-Host ("  [{0}]  Service {1}: {2}" -f $(if ($s -and $s.Status -eq 'Running') { 'OK' } else { '!!' }), $id, $(if ($s) { $s.Status } else { 'chưa đăng ký' }))
}
Wait-Http 'AI service' 'http://127.0.0.1:8000/health' { param($r) ($r.Content | ConvertFrom-Json).status -eq 'ok' }
Wait-Http 'Backend + database' "http://127.0.0.1:$WebPort/health" { param($r) ($r.Content | ConvertFrom-Json).status -eq 'healthy' }
Wait-Http 'Giao diện web' "http://127.0.0.1:$WebPort/" { param($r) $r.Content -match '<div id="root"' }
Write-Host "ĐẠT: hệ thống chạy tốt trên cổng $WebPort."
