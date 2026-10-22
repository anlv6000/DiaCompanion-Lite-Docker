# 1.3.1: chuyển một máy đang chạy DiaCompanion Lite (≤ 1.3.0) sang tên mới DR AI-Diagnosis.
# Gọi từ install-runtime.ps1 TRƯỚC khi cấu hình. Không xóa dữ liệu nào:
#   1. Dừng và gỡ 2 dịch vụ cũ (DiaCompanionAI, DiaCompanionBackend), bỏ luật tường lửa cũ.
#   2. SAO LƯU database DiaCompanion (.bak, CHECKSUM) rồi đổi tên database thành DRAIDiagnosis.
#   3. Chuyển thư mục ảnh/mask (storage) và bản sao lưu sang C:\ProgramData\DRAIDiagnosis (cùng ổ đĩa:
#      đổi tên thư mục, không chép), chép cấu hình (mật khẩu SA, khóa JWT, cổng) và log cũ.
#   4. Đổi tên thư mục cũ thành DiaCompanion.da-chuyen-<thời gian> (GIỮ LẠI, người quản trị tự xóa khi đã kiểm tra).
#   5. Ghi lại thư mục cài đặt cũ (C:\Program Files\DiaCompanion Lite) để install-runtime.ps1 xóa SAU KHI cài xong.
# Chạy lại an toàn: bước nào đã xong thì bỏ qua. Trả $true nếu có chuyển dữ liệu.
[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$DataRoot)
. (Join-Path $PSScriptRoot 'common.ps1')

$legacy = Get-LegacyDataRoot
$legacyConfig = Join-Path $legacy 'config'
$legacySecrets = Join-Path $legacyConfig 'secrets.json'
$configRoot = Join-Path $DataRoot 'config'
$marker = Join-Path $configRoot 'migrated-from-diacompanion.txt'

if (-not (Test-Path $legacySecrets)) { return $false }
if (Test-Path (Join-Path $configRoot 'secrets.json')) {
    if (Test-Path $marker) { return $false }   # đã chuyển ở lần chạy trước
    Write-Warning "Có cả $legacy và $DataRoot. Giữ nguyên cả hai, KHÔNG chuyển dữ liệu. Kiểm tra bằng tay nếu cần."
    return $false
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
Write-Host "=== Phát hiện DiaCompanion Lite (bản 1.3.0 trở về trước) tại ${legacy}, chuyển sang $($script:AppName) ==="
$secrets = Read-Json $legacySecrets
$pw = $secrets.DbPassword
$oldInstall = $null
$oldInfoPath = Join-Path $legacyConfig 'installer.json'
if (Test-Path $oldInfoPath) { $oldInstall = (Read-Json $oldInfoPath).InstallRoot }

# 1. dịch vụ cũ ------------------------------------------------------------------------------
foreach ($id in $script:LegacyServices) {
    $svc = Get-Service $id -ErrorAction SilentlyContinue
    if (-not $svc) { continue }
    Write-Host "Dừng và gỡ dịch vụ cũ $id..."
    if ($svc.Status -ne 'Stopped') { Stop-Service $id -Force -ErrorAction SilentlyContinue }
    $wrapper = if ($oldInstall) { Join-Path $oldInstall "services\$id.exe" } else { $null }
    if ($wrapper -and (Test-Path $wrapper)) { & $wrapper uninstall | Out-Null }
    if (Get-Service $id -ErrorAction SilentlyContinue) { sc.exe delete $id | Out-Null }
}
for ($i = 0; $i -lt 40 -and (Get-Service $script:LegacyServices -ErrorAction SilentlyContinue); $i++) { Start-Sleep -Milliseconds 500 }
if (Get-Service $script:LegacyServices -ErrorAction SilentlyContinue) { throw 'Không gỡ được dịch vụ DiaCompanion cũ. Khởi động lại máy rồi chạy lại Setup.exe.' }
Remove-NetFirewallRule -DisplayName $script:LegacyFirewallRule -ErrorAction SilentlyContinue

# 2. database: sao lưu rồi đổi tên ---------------------------------------------------------------
$sqlSvc = Get-Service 'MSSQL$SQLEXPRESS' -ErrorAction SilentlyContinue
if (-not $sqlSvc) { throw 'Có cấu hình DiaCompanion cũ nhưng không thấy SQL Server SQLEXPRESS. Không thể chuyển dữ liệu an toàn.' }
if ($sqlSvc.Status -ne 'Running') { Start-Service $sqlSvc.Name }
Wait-SqlReady $pw
$master = Get-ConnectionString $pw 'master'
$hasOld = Get-SqlScalar $master "SELECT DB_ID(N'$($script:LegacyDbName)')"
$hasNew = Get-SqlScalar $master "SELECT DB_ID(N'$($script:DbName)')"
if ($hasOld -and $hasNew) {
    throw "SQL Server có cả database $($script:LegacyDbName) và $($script:DbName). Không biết dùng bản nào: sao lưu và xóa một trong hai rồi chạy lại Setup.exe."
}
if ($hasOld) {
    $bakDir = Join-Path $legacy "backups\truoc-doi-ten-$stamp"
    New-Item -ItemType Directory -Force -Path $bakDir | Out-Null
    icacls $bakDir /grant 'NT SERVICE\MSSQL$SQLEXPRESS:(OI)(CI)M' | Out-Null
    $bak = Join-Path $bakDir "$($script:LegacyDbName).bak"
    Write-Host "Sao lưu database $($script:LegacyDbName) → $bak"
    Invoke-Sql $master "BACKUP DATABASE [$($script:LegacyDbName)] TO DISK = N'$($bak.Replace("'", "''"))' WITH INIT, CHECKSUM"
    if (-not (Test-Path $bak)) { throw "Sao lưu không tạo được tệp $bak. Dừng chuyển dữ liệu." }
    Write-Host "Đổi tên database $($script:LegacyDbName) → $($script:DbName)"
    Invoke-Sql $master @"
ALTER DATABASE [$($script:LegacyDbName)] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
ALTER DATABASE [$($script:LegacyDbName)] MODIFY NAME = [$($script:DbName)];
ALTER DATABASE [$($script:DbName)] SET MULTI_USER;
"@
} elseif ($hasNew) {
    Write-Host "Database đã mang tên $($script:DbName) (lần chạy trước đã đổi)."
} else {
    throw "Không thấy database $($script:LegacyDbName) trên SQLEXPRESS. Dừng chuyển dữ liệu để tránh mất ảnh/kết quả."
}

# 3. thư mục dữ liệu -------------------------------------------------------------------------------
New-Item -ItemType Directory -Force -Path $DataRoot, $configRoot, (Join-Path $DataRoot 'logs') | Out-Null
foreach ($name in 'storage', 'backups') {
    $src = Join-Path $legacy $name
    if (-not (Test-Path $src)) { continue }
    $dst = Join-Path $DataRoot $name
    if (-not (Test-Path $dst)) {
        Write-Host "Chuyển $src → $dst"
        Move-Item -LiteralPath $src -Destination $dst
    } else {
        Write-Host "Gộp $src vào $dst"
        Get-ChildItem -LiteralPath $src -Force | ForEach-Object {
            $t = Join-Path $dst $_.Name
            if (Test-Path $t) { throw "Trùng tên khi gộp thư mục: $t. Kiểm tra bằng tay." }
            Move-Item -LiteralPath $_.FullName -Destination $t
        }
    }
}
Write-Host "Chép cấu hình $legacyConfig → $configRoot"
Get-ChildItem -LiteralPath $legacyConfig -Force | Where-Object { $_.Name -ne 'tmp' } |
    ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $configRoot -Recurse -Force }
$oldLogs = Join-Path $legacy 'logs'
if (Test-Path $oldLogs) {
    Copy-Item -LiteralPath $oldLogs -Destination (Join-Path $DataRoot 'logs\diacompanion-1.3.0') -Recurse -Force -ErrorAction SilentlyContinue
}

# 4. đánh dấu, giữ thư mục cũ -----------------------------------------------------------------------
$kept = "$legacy.da-chuyen-$stamp"
Set-Content -Path $marker -Encoding UTF8 -Value @(
    "Chuyển từ DiaCompanion Lite lúc $(Get-Date -Format s).",
    "Database $($script:LegacyDbName) đổi tên thành $($script:DbName); bản sao lưu trước khi đổi: backups\truoc-doi-ten-$stamp.",
    "Thư mục cũ giữ lại tại: $kept (xóa khi đã kiểm tra hệ thống mới chạy đúng).")
if ($oldInstall) { Set-Content -Path (Join-Path $configRoot 'legacy-install-root.txt') -Value $oldInstall -Encoding UTF8 }
try { Rename-Item -LiteralPath $legacy -NewName (Split-Path $kept -Leaf) }
catch { Write-Warning "Không đổi tên được thư mục cũ $legacy ($($_.Exception.Message)). Dữ liệu đã chuyển xong; xóa thư mục cũ bằng tay sau khi kiểm tra." }
Write-Host 'Chuyển dữ liệu xong.'
return $true
