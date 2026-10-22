# Điều phối cài đặt, gọi từ Setup.exe sau khi chép tệp.
#  - Cài mới : VC++ runtime → SQL Express → cấu hình → DB + seed → service → kiểm tra
#  - Cài đè  : dùng lại mật khẩu/khóa đã lưu, giữ nguyên DB và ảnh → cấu hình → service → kiểm tra
# Thoát mã 0 = thành công. Tóm tắt (URL, mật khẩu tạm) ghi vào config\install-summary.txt.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$InstallRoot,
    [string]$DataRoot = "$env:ProgramData\DiaCompanion",
    [int]$WebPort = 8080,
    [string]$AdminEmail = 'bacsi@benhvien.local',
    [string]$AdminName = 'Bác sĩ',
    [string]$License = 'NCKH-001',
    [string]$ResearchEmail = '',
    # Setup.exe ghi cấu hình wizard vào tệp UTF-8 (4 dòng: email bác sĩ, họ tên, email nghiên cứu, cổng)
    # để tên tiếng Việt không đi qua dòng lệnh. Chạy tay thì bỏ qua tham số này.
    [string]$WizardFile = ''
)
# Mọi bước đều nằm trong try: lỗi ở đâu cũng có log và tệp tóm tắt cho Setup.exe hiển thị.
$ErrorActionPreference = 'Stop'
$logDir = Join-Path $DataRoot 'logs'
$configRoot = Join-Path $DataRoot 'config'
$logPath = Join-Path $logDir 'installer.log'
$summaryPath = Join-Path $configRoot 'install-summary.txt'
$secretsPath = Join-Path $configRoot 'secrets.json'
$dbMarker = Join-Path $configRoot 'db-initialized'
[void][IO.Directory]::CreateDirectory($logDir)
[void][IO.Directory]::CreateDirectory($configRoot)
try { [IO.File]::Delete($summaryPath) } catch { }
try { Start-Transcript -Path $logPath -Append | Out-Null } catch { }
$exit = 0
try {
    . (Join-Path $PSScriptRoot 'common.ps1')

    if ($WizardFile -and [IO.File]::Exists($WizardFile)) {
        $w = @([IO.File]::ReadAllLines($WizardFile, [Text.Encoding]::UTF8))
        Remove-FileQuiet $WizardFile
        $port = 0
        if ($w.Count -ge 1 -and $w[0].Trim()) { $AdminEmail = $w[0].Trim() }
        if ($w.Count -ge 2 -and $w[1].Trim()) { $AdminName = $w[1].Trim() }
        if ($w.Count -ge 3) { $ResearchEmail = $w[2].Trim() }
        if ($w.Count -ge 4 -and [int]::TryParse($w[3].Trim(), [ref]$port) -and $port -gt 0) { $WebPort = $port }
    }
    Protect-ConfigRoot $configRoot

    Write-Host "=== DiaCompanion Lite: cài đặt $(Get-Date -Format s) ==="

    $hasSecrets = Test-Path $secretsPath
    $upgrade = $hasSecrets -and (Test-Path $dbMarker)   # đã cài xong trước đây
    $sqlSvc = Get-Service 'MSSQL$SQLEXPRESS' -ErrorAction SilentlyContinue

    if ($hasSecrets) {
        $s = Read-Json $secretsPath
        $dbPassword = $s.DbPassword; $jwt = $s.JwtSigningKey
        $prevPath = Join-Path $configRoot 'installer.json'
        if ($upgrade -and (Test-Path $prevPath)) {
            Write-Host 'Phát hiện bản cài trước: CÀI ĐÈ, giữ nguyên database, ảnh và tài khoản.'
            $prev = Read-Json $prevPath
            $WebPort = [int]$prev.WebPort; $AdminEmail = $prev.AdminEmail; $AdminName = $prev.AdminName
            $License = $prev.License; $ResearchEmail = $prev.ResearchEmail
        } else {
            Write-Host 'Lần cài trước chưa xong: làm tiếp với mật khẩu đã lưu.'
        }
        if (-not $sqlSvc -and $upgrade) { throw 'Có cấu hình DiaCompanion nhưng không thấy SQL Server SQLEXPRESS. Không thể cài đè an toàn.' }
    } else {
        if ($sqlSvc) {
            throw "Máy đã có SQL Server SQLEXPRESS nhưng không có cấu hình DiaCompanion ($secretsPath). Bộ cài không biết mật khẩu SA của instance này. Dùng máy sạch, hoặc gỡ instance SQLEXPRESS cũ (sao lưu trước) rồi cài lại."
        }
        $rand = [Security.Cryptography.RandomNumberGenerator]::Create()
        $b = New-Object byte[] 32; $rand.GetBytes($b)
        $dbPassword = 'Dc' + ([Convert]::ToBase64String($b) -replace '[^A-Za-z0-9]', '') + '9x'
        $b = New-Object byte[] 48; $rand.GetBytes($b)
        $jwt = [Convert]::ToBase64String($b)
        # Lưu trước khi cài SQL: nếu các bước sau lỗi, lần chạy lại vẫn dùng đúng mật khẩu.
        Write-Json ([ordered]@{ DbPassword = $dbPassword; JwtSigningKey = $jwt; CreatedAtUtc = [DateTime]::UtcNow.ToString('o') }) $secretsPath
    }

    if (-not $sqlSvc) {
        $sqlPkg = Join-Path $InstallRoot 'sqlserver\SQLEXPR_x64_ENU.exe'
        if (-not (Test-Path $sqlPkg)) { throw "Thiếu bộ cài SQL Server: $sqlPkg" }
        $pendingReboot = Test-PendingReboot
        if ($pendingReboot) { Write-Host 'Lưu ý: Windows đang chờ khởi động lại; SQL Setup có thể từ chối cài.' }
        # Giải nén trước rồi mới chạy setup.exe: tránh bộ tự giải nén phụ thuộc thư mục hiện hành / %TEMP%
        # và lấy đúng mã thoát của SQL Setup.
        $sqlDir = Join-Path $DataRoot 'sqlsetup'
        if (-not (Test-Path (Join-Path $sqlDir 'SETUP.EXE'))) {
            Write-Host 'Giải nén bộ cài SQL Server Express...'
            [void][IO.Directory]::CreateDirectory($sqlDir)
            $p = Start-Process $sqlPkg -ArgumentList '/q', "/x:`"$sqlDir`"" -Wait -PassThru -WorkingDirectory $sqlDir
            if (-not (Test-Path (Join-Path $sqlDir 'SETUP.EXE'))) { throw "Giải nén bộ cài SQL Server lỗi (mã $($p.ExitCode)), không thấy $sqlDir\SETUP.EXE." }
        }
        Write-Host 'Cài SQL Server Express (10–20 phút, không có thông báo trong lúc cài)...'
        $admin = [Security.Principal.WindowsIdentity]::GetCurrent().Name
        $p = Start-Process (Join-Path $sqlDir 'SETUP.EXE') -Wait -PassThru -WorkingDirectory $sqlDir -ArgumentList @(
            '/Q', '/ACTION=Install', '/FEATURES=SQLEngine', '/INSTANCENAME=SQLEXPRESS', '/SQLSVCSTARTUPTYPE=Automatic',
            '/SECURITYMODE=SQL', "/SAPWD=$dbPassword", "/SQLSYSADMINACCOUNTS=`"$admin`"", '/IACCEPTSQLSERVERLICENSETERMS',
            '/UPDATEENABLED=0', '/TCPENABLED=0')
        $code = $p.ExitCode
        Write-Host "SQL Setup kết thúc, mã $code."
        if ($code -eq -2067919934) { throw 'SQL Setup yêu cầu khởi động lại máy trước khi cài. Khởi động lại rồi chạy lại Setup.exe.' }
        if ($code -notin 0, 3010) {
            $summary = Get-ChildItem "$env:ProgramFiles\Microsoft SQL Server\*\Setup Bootstrap\Log\Summary.txt" -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($summary) { Write-Host '--- Summary.txt của SQL Setup ---'; Get-Content $summary.FullName -Tail 40 | Write-Host }
            $hint = if ($pendingReboot) { ' Windows đang chờ khởi động lại: khởi động lại máy rồi chạy lại Setup.exe.' } else { '' }
            throw "Cài SQL Server Express lỗi, mã $code.$hint Chi tiết: $env:ProgramFiles\Microsoft SQL Server\<phiên bản>\Setup Bootstrap\Log\Summary.txt"
        }
        $sqlSvc = Get-Service 'MSSQL$SQLEXPRESS' -ErrorAction SilentlyContinue
        if (-not $sqlSvc) { throw 'Cài xong nhưng không thấy service MSSQL$SQLEXPRESS.' }
        Remove-Item $sqlDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    if ($sqlSvc.Status -ne 'Running') { Start-Service $sqlSvc.Name }

    # VC++ cài SAU SQL: cài trước có thể để lại cờ "chờ khởi động lại" khiến SQL Setup từ chối.
    $vc = Join-Path $InstallRoot 'prereq\vc_redist.x64.exe'
    if (Test-Path $vc) {
        Write-Host 'Cài Visual C++ runtime (PyTorch/TensorFlow cần)...'
        $p = Start-Process $vc -ArgumentList '/install', '/quiet', '/norestart' -Wait -PassThru
        if ($p.ExitCode -notin 0, 1638, 3010) { throw "Cài VC++ runtime lỗi, mã $($p.ExitCode)." }
    }

    & (Join-Path $PSScriptRoot 'configure.ps1') -InstallRoot $InstallRoot -DataRoot $DataRoot -DbPassword $dbPassword -JwtSigningKey $jwt `
        -WebPort $WebPort -AdminEmail $AdminEmail -AdminName $AdminName -License $License -ResearchEmail $ResearchEmail

    if (-not (Test-Path $dbMarker)) {
        & (Join-Path $PSScriptRoot 'install-db.ps1') -InstallRoot $InstallRoot -DataRoot $DataRoot -DbPassword $dbPassword `
            -AdminEmail $AdminEmail -AdminName $AdminName -License $License -ResearchEmail $ResearchEmail
        Set-Content -Path $dbMarker -Value ([DateTime]::UtcNow.ToString('o')) -Encoding ASCII
    } else {
        Wait-SqlReady $dbPassword
    }

    & (Join-Path $PSScriptRoot 'register-services.ps1') -InstallRoot $InstallRoot -DataRoot $DataRoot -WebPort $WebPort
    & (Join-Path $PSScriptRoot 'health-check.ps1') -WebPort $WebPort

    $lines = @('CÀI ĐẶT THÀNH CÔNG', '', "Mở trên máy này:  http://localhost:$WebPort")
    $lan = Get-LanUrls $WebPort
    if ($lan) { $lines += 'Máy khác trong mạng LAN:'; $lines += ($lan | ForEach-Object { '  ' + $_ }) }
    $cred = Join-Path $configRoot 'initial_credentials.txt'
    if (-not $upgrade -and (Test-Path $cred)) {
        $lines += @('', 'TÀI KHOẢN BAN ĐẦU (bắt buộc đổi mật khẩu ở lần đăng nhập đầu):')
        $lines += Get-Content -Encoding UTF8 $cred
        $lines += @('', "Ghi lại rồi XÓA tệp: $cred")
    } elseif ($upgrade) {
        $lines += @('', 'Đã cài đè: giữ nguyên tài khoản, database và ảnh.')
    }
    $lines += @('', 'Công cụ vận hành: Start Menu > DiaCompanion Lite (kiểm tra, sao lưu, cấp lại mật khẩu, khởi động lại).', "Log: $(Join-Path $DataRoot 'logs')")
    Set-Content -Path $summaryPath -Value $lines -Encoding UTF8
} catch {
    $exit = 1
    $msg = $_.Exception.Message
    $where = $_.InvocationInfo.PositionMessage
    Write-Host "LỖI: $msg"
    Write-Host $where
    Write-Host $_.ScriptStackTrace
    $text = @('CÀI ĐẶT CHƯA HOÀN TẤT', '', "Lỗi: $msg", '', "Vị trí: $where", '', "Chi tiết: $logPath",
        '', 'Sửa nguyên nhân rồi chạy lại Setup.exe: bộ cài dùng lại mật khẩu đã tạo và làm tiếp các bước còn thiếu.')
    try { [IO.File]::WriteAllLines($summaryPath, [string[]]$text, (New-Object Text.UTF8Encoding $true)) }
    catch { [IO.File]::WriteAllLines((Join-Path $logDir 'install-summary.txt'), [string[]]$text, (New-Object Text.UTF8Encoding $true)) }
} finally {
    try { Stop-Transcript | Out-Null } catch { }
}
exit $exit
