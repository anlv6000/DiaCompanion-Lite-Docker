# Công cụ vận hành (mở từ Start Menu > DR AI-Diagnosis). Tự xin quyền Administrator.
#   -Action health | backup | reset-password | restart
[CmdletBinding()]
param([ValidateSet('health', 'backup', 'reset-password', 'restart', 'add-grader', 'add-research')][string]$Action = 'health')
. (Join-Path $PSScriptRoot 'common.ps1')

if (-not (Test-Admin)) {
    Start-Process powershell.exe -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -Action $Action"
    exit
}

$dataRoot = Get-DataRoot
$installRoot = Split-Path $PSScriptRoot -Parent
$info = Read-Json (Join-Path (Get-ConfigRoot $dataRoot) 'installer.json')
$port = [int]$info.WebPort

function Pause-End { Write-Host ''; Read-Host 'Nhấn Enter để đóng' | Out-Null }

try {
    switch ($Action) {
        'health' {
            & (Join-Path $PSScriptRoot 'health-check.ps1') -WebPort $port -TimeoutSeconds 20
            Write-Host ''; Write-Host "Mở trên máy này: http://localhost:$port"
            Get-LanUrls $port | ForEach-Object { Write-Host "Máy khác trong LAN: $_" }
        }
        'restart' {
            foreach ($id in $script:Services) { Stop-Service $id -Force -ErrorAction SilentlyContinue }
            Start-Service $script:ServiceAI; Start-Service $script:ServiceBackend
            & (Join-Path $PSScriptRoot 'health-check.ps1') -WebPort $port
        }
        'backup' {
            $pw = (Get-Secrets $dataRoot).DbPassword
            $stamp = Get-Date -Format 'yyyyMMdd-HHmm'
            $dir = Join-Path $dataRoot "backups\$stamp"
            New-Item -ItemType Directory -Force -Path $dir | Out-Null
            # SQL Server ghi tệp .bak bằng tài khoản service của nó.
            icacls $dir /grant 'NT SERVICE\MSSQL$SQLEXPRESS:(OI)(CI)M' | Out-Null
            $bak = Join-Path $dir "$($script:DbName).bak"
            Write-Host "Sao lưu database → $bak"
            Invoke-Sql (Get-ConnectionString $pw 'master') "BACKUP DATABASE [$($script:DbName)] TO DISK = N'$($bak.Replace("'", "''"))' WITH INIT, CHECKSUM"
            $tgz = Join-Path $dir 'storage.tar.gz'
            Write-Host "Nén ảnh + mask → $tgz"
            tar.exe -czf $tgz -C (Join-Path $dataRoot 'storage') .
            if ($LASTEXITCODE -ne 0) { throw 'Nén thư mục ảnh lỗi.' }
            Write-Host ''; Write-Host "XONG. Chép cả thư mục này ra ổ ngoài: $dir"
            Start-Process explorer.exe $dir
        }
        { $_ -in 'add-grader', 'add-research' } {
            $isGrader = $Action -eq 'add-grader'
            $email = (Read-Host ('Email tài khoản ' + $(if ($isGrader) { 'người chấm độc lập' } else { 'nghiên cứu' }))).Trim()
            if ($email -notmatch '^[^@\s]+@[^@\s]+$') { throw 'Email không hợp lệ.' }
            $pw = (Get-Secrets $dataRoot).DbPassword
            $tmp = New-SecureTempFile '' $dataRoot
            $env:PYTHONIOENCODING = 'utf-8'
            $py = Join-Path $installRoot 'ai\python.exe'
            $seed = Join-Path $installRoot 'db\seed_lite.py'
            try {
                if ($isGrader) {
                    $name = (Read-Host 'Họ tên người chấm (hiện trong nhật ký, không hiện với người khác)').Trim()
                    if (-not $name) { $name = 'Người chấm' }
                    & $py $seed add-grader --email $email --full-name $name --out "$tmp.sql" --password-out "$tmp.txt" | Out-Null
                } else {
                    & $py $seed add-research --email $email --out "$tmp.sql" --password-out "$tmp.txt" | Out-Null
                }
                if ($LASTEXITCODE -ne 0) { throw 'seed_lite.py lỗi.' }
                Invoke-SqlFile (Get-ConnectionString $pw) "$tmp.sql"
                Write-Host ''; Write-Host 'TÀI KHOẢN MỚI (bắt buộc đổi mật khẩu khi đăng nhập lần đầu):'
                Get-Content -Encoding UTF8 "$tmp.txt" | Write-Host
                Write-Host 'Nếu email đã có tài khoản, vai trò được thêm vào tài khoản đó và mật khẩu cũ giữ nguyên.'
            } finally { Remove-FileQuiet "$tmp.sql"; Remove-FileQuiet "$tmp.txt" }
        }
        'reset-password' {
            $email = Read-Host "Email tài khoản cần cấp lại mật khẩu [Enter = $($info.AdminEmail)]"
            if (-not $email) { $email = $info.AdminEmail }
            $pw = (Get-Secrets $dataRoot).DbPassword
            $n = Get-SqlScalar (Get-ConnectionString $pw) 'SELECT COUNT(*) FROM dbo.Users WHERE Email = @e AND IsVoided = 0' @{ '@e' = $email.Trim() }
            if (-not $n) { throw "Không có tài khoản đang hoạt động với email $email." }
            $tmp = New-SecureTempFile '' $dataRoot
            $env:PYTHONIOENCODING = 'utf-8'
            try {
                & (Join-Path $installRoot 'ai\python.exe') (Join-Path $installRoot 'db\seed_lite.py') reset-password --email $email --out "$tmp.sql" --password-out "$tmp.txt" | Out-Null
                if ($LASTEXITCODE -ne 0) { throw 'seed_lite.py lỗi.' }
                Invoke-SqlFile (Get-ConnectionString $pw) "$tmp.sql"
                Write-Host ''; Write-Host 'MẬT KHẨU TẠM MỚI (bắt buộc đổi khi đăng nhập):'
                Get-Content -Encoding UTF8 "$tmp.txt" | Write-Host
            } finally { Remove-FileQuiet "$tmp.sql"; Remove-FileQuiet "$tmp.txt" }
        }
    }
} catch {
    Write-Host ''; Write-Host "LỖI: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Log dịch vụ: $(Join-Path $dataRoot 'logs')"
}
Pause-End
