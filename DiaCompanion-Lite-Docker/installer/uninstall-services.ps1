# Gỡ service (cả kiểu cũ tạo bằng sc.exe ở installer v1). Không đụng DB và dữ liệu.
[CmdletBinding()]
param([string]$InstallRoot = (Split-Path $PSScriptRoot -Parent))
. (Join-Path $PSScriptRoot 'common.ps1')
$ErrorActionPreference = 'Continue'

foreach ($id in $script:Services) {
    $svc = Get-Service $id -ErrorAction SilentlyContinue
    if (-not $svc) { continue }
    if ($svc.Status -ne 'Stopped') { Stop-Service $id -Force -ErrorAction SilentlyContinue }
    $wrapper = Join-Path $InstallRoot "services\$id.exe"
    if (Test-Path $wrapper) { & $wrapper uninstall | Out-Null }
    if (Get-Service $id -ErrorAction SilentlyContinue) { sc.exe delete $id | Out-Null }
}
# chờ SCM xóa hẳn trước khi đăng ký lại
for ($i = 0; $i -lt 20 -and (Get-Service $script:Services -ErrorAction SilentlyContinue); $i++) { Start-Sleep -Milliseconds 500 }
