# Tạo DB, áp schema, seed tài khoản + 3 model. Làm tiếp được nếu lần cài trước dừng giữa chừng.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$InstallRoot,
    [Parameter(Mandatory = $true)][string]$DataRoot,
    [Parameter(Mandatory = $true)][string]$DbPassword,
    [string]$AdminEmail = 'bacsi@benhvien.local',
    [string]$AdminName = 'Bác sĩ',
    [string]$License = 'NCKH-001',
    [string]$ResearchEmail = ''
)
. (Join-Path $PSScriptRoot 'common.ps1')

$python = Join-Path $InstallRoot 'ai\python.exe'
$credPath = Join-Path (Get-ConfigRoot $DataRoot) 'initial_credentials.txt'
Wait-SqlReady $DbPassword

$master = Get-ConnectionString $DbPassword 'master'
$db = Get-ConnectionString $DbPassword

# Mỗi bước chỉ chạy khi chưa xong.
if (-not (Get-SqlScalar $master "SELECT DB_ID(N'$($script:DbName)')")) {
    Write-Host 'Tạo database...'
    Invoke-Sql $master "CREATE DATABASE [$($script:DbName)];"
}

$tables = [int](Get-SqlScalar $db 'SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0')
if ($tables -eq 0) {
    Write-Host 'Áp schema...'
    Invoke-SqlFile $db (Join-Path $InstallRoot 'db\schema.sql')
} elseif (-not (Get-SqlScalar $db "SELECT OBJECT_ID(N'dbo.Users', N'U')")) {
    throw "Database $($script:DbName) có $tables bảng nhưng thiếu dbo.Users (schema áp dở). Xóa database này rồi chạy lại bộ cài."
} else {
    Write-Host 'Schema đã có, bỏ qua.'
}

# Seed chạy trong một transaction nên hoặc có đủ, hoặc chưa có gì.
if ([int](Get-SqlScalar $db 'SELECT COUNT(*) FROM dbo.Users WHERE Email = @e' @{ '@e' = $AdminEmail.Trim() }) -gt 0) {
    Write-Host "Tài khoản $AdminEmail đã có, bỏ qua seed."
    if (-not (Test-Path $credPath)) { Write-Warning 'Không còn tệp mật khẩu tạm: dùng Start Menu > Cấp lại mật khẩu sau khi cài xong.' }
} else {
    Write-Host 'Seed tài khoản và model...'
    $seedSql = New-SecureTempFile '.sql' $DataRoot
    $seedArgs = @((Join-Path $InstallRoot 'db\seed_lite.py'), 'init', '--email', $AdminEmail, '--full-name', $AdminName,
        '--license', $License, '--ai-service-dir', (Join-Path $InstallRoot 'ai'), '--out', $seedSql, '--password-out', $credPath)
    if ($ResearchEmail) { $seedArgs += @('--research-email', $ResearchEmail) }
    $env:PYTHONIOENCODING = 'utf-8'
    try {
        # Bỏ stdout: seed_lite.py in mật khẩu tạm, không để lọt vào installer.log. Mật khẩu nằm trong $credPath.
        & $python @seedArgs | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'seed_lite.py lỗi khi sinh dữ liệu khởi tạo.' }
        Invoke-SqlFile $db $seedSql
    } finally { Remove-FileQuiet $seedSql }
}
Write-Host "Database sẵn sàng. Mật khẩu tạm: $credPath"
