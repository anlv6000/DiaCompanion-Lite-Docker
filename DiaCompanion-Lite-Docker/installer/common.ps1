# Hàm dùng chung cho các script cài đặt / vận hành DiaCompanion Lite.
# Dot-source: . (Join-Path $PSScriptRoot 'common.ps1')

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:SqlServer = '.\SQLEXPRESS'
$script:DbName = 'DiaCompanion'
$script:Services = @('DiaCompanionBackend', 'DiaCompanionAI')

function Get-DataRoot { Join-Path $env:ProgramData 'DiaCompanion' }
function Get-ConfigRoot([string]$DataRoot = (Get-DataRoot)) { Join-Path $DataRoot 'config' }

function Test-Admin {
    $id = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
    return $id.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# Chỉ SYSTEM + Administrators đọc được thư mục cấu hình (chứa mật khẩu SA, khóa JWT).
function Protect-ConfigRoot([string]$Path) {
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
    icacls $Path /inheritance:r /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' | Out-Null
}

function Read-Json([string]$Path) { Get-Content -Raw -Encoding UTF8 $Path | ConvertFrom-Json }
function Write-Json($Object, [string]$Path) { $Object | ConvertTo-Json -Depth 8 | Set-Content -Path $Path -Encoding UTF8 }

function Get-Secrets([string]$DataRoot = (Get-DataRoot)) {
    $p = Join-Path (Get-ConfigRoot $DataRoot) 'secrets.json'
    if (-not (Test-Path $p)) { throw "Không tìm thấy $p. DiaCompanion chưa được cài trên máy này." }
    Read-Json $p
}

function Get-ConnectionString([string]$Password, [string]$Database = $script:DbName) {
    "Server=$($script:SqlServer);Database=$Database;User Id=sa;Password=$Password;TrustServerCertificate=True;Encrypt=False;Connect Timeout=10"
}

# Chạy SQL bằng System.Data.SqlClient có sẵn trong Windows PowerShell — không cần sqlcmd.
# Tách batch theo dòng "GO" nếu có.
function Invoke-Sql([string]$ConnectionString, [string]$Sql, [int]$TimeoutSeconds = 600) {
    $conn = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
    $conn.Open()
    try {
        foreach ($batch in [regex]::Split($Sql, '(?im)^\s*GO\s*;?\s*$')) {
            if (-not $batch.Trim()) { continue }
            $cmd = $conn.CreateCommand()
            $cmd.CommandText = $batch
            $cmd.CommandTimeout = $TimeoutSeconds
            [void]$cmd.ExecuteNonQuery()
        }
    } finally { $conn.Dispose() }
}
function Invoke-SqlFile([string]$ConnectionString, [string]$Path) {
    Invoke-Sql $ConnectionString ([IO.File]::ReadAllText($Path, [Text.Encoding]::UTF8))
}

# Tệp tạm đặt trong config\tmp (chỉ SYSTEM + Administrators đọc được), KHÔNG dùng %TEMP%:
# khi tên tài khoản Windows có dấu cách, %TEMP% là đường dẫn rút gọn 8.3 (vd C:\Users\KIENLE~1\...)
# và Remove-Item của PowerShell 5.1 ném lỗi "does not exist" trên đường dẫn đó.
function New-SecureTempFile([string]$Extension = '', [string]$DataRoot = (Get-DataRoot)) {
    $dir = Join-Path (Get-ConfigRoot $DataRoot) 'tmp'
    [void][IO.Directory]::CreateDirectory($dir)
    Join-Path $dir ([guid]::NewGuid().ToString('N') + $Extension)
}
function Remove-FileQuiet([string]$Path) {
    try { if ([IO.File]::Exists($Path)) { [IO.File]::Delete($Path) } }
    catch { Write-Warning "Không xóa được tệp tạm $Path : $($_.Exception.Message)" }
}

function Wait-SqlReady([string]$Password, [int]$TimeoutSeconds = 300) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ($true) {
        try { Invoke-Sql (Get-ConnectionString $Password 'master') 'SELECT 1' 30; return }
        catch { if ((Get-Date) -gt $deadline) { throw "SQL Server không sẵn sàng sau $TimeoutSeconds giây: $($_.Exception.Message)" } }
        Start-Sleep -Seconds 5
    }
}

function Get-LanUrls([int]$Port) {
    $ips = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } |
        Select-Object -ExpandProperty IPAddress
    @($ips | ForEach-Object { "http://${_}:$Port" })
}

function Get-SqlScalar([string]$ConnectionString, [string]$Sql, [hashtable]$Params = @{}) {
    $conn = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
    $conn.Open()
    try {
        $cmd = $conn.CreateCommand(); $cmd.CommandText = $Sql
        foreach ($k in $Params.Keys) { [void]$cmd.Parameters.AddWithValue($k, $Params[$k]) }
        $v = $cmd.ExecuteScalar()
        if ($v -is [DBNull]) { $null } else { $v }
    } finally { $conn.Dispose() }
}

# Windows đang chờ khởi động lại? (SQL Setup kiểm tra đúng các dấu hiệu này)
function Test-PendingReboot {
    if (Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending') { return $true }
    if (Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired') { return $true }
    $sm = Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager' -Name PendingFileRenameOperations -ErrorAction SilentlyContinue
    return [bool]($sm -and $sm.PendingFileRenameOperations)
}
