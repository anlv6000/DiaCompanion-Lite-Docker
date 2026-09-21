[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$InstallRoot,
    [string]$DataRoot = "$env:ProgramData\DiaCompanion",
    [int]$WebPort = 8080,
    [string]$AdminEmail = 'bacsi@benhvien.local',
    [string]$AdminName = 'Bac si',
    [string]$License = 'NCKH-001',
    [string]$ResearchEmail = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$scriptRoot = Join-Path $InstallRoot 'scripts'
$logPath = Join-Path $DataRoot 'logs\installer.log'
New-Item -ItemType Directory -Force -Path (Split-Path $logPath -Parent) | Out-Null
Start-Transcript -Path $logPath -Append | Out-Null
try {
    $sqlInstaller = Join-Path $InstallRoot 'sqlserver\SQLEXPR_x64_ENU.exe'
    $random = [Security.Cryptography.RandomNumberGenerator]::GetBytes(32)
    $dbPassword = 'Dc!' + ([Convert]::ToBase64String($random) -replace '[^A-Za-z0-9]', '') + '9x'
    $sqlService = Get-Service 'MSSQL$SQLEXPRESS' -ErrorAction SilentlyContinue
    $sqlWasInstalled = [bool]$sqlService
    if (-not $sqlService -and (Test-Path $sqlInstaller)) {
        $admin = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
        & $sqlInstaller /Q /ACTION=Install /FEATURES=SQL /INSTANCENAME=SQLEXPRESS /SQLSVCSTARTUPTYPE=Automatic /SECURITYMODE=SQL /SAPWD=$dbPassword /SQLSYSADMINACCOUNTS=$admin /IACCEPTSQLSERVERLICENSETERMS
        if ($LASTEXITCODE -ne 0) { throw "SQL Server Express installation failed with exit code $LASTEXITCODE." }
        $sqlService = Get-Service 'MSSQL$SQLEXPRESS' -ErrorAction SilentlyContinue
    }
    if ($sqlWasInstalled) {
        throw 'An existing SQLEXPRESS instance was found. Installer v1 supports fresh database installation only; use a clean SQL Express instance or prepare an upgrade procedure.'
    }
    if (-not $sqlService) { throw 'SQL Server Express SQLEXPRESS service was not found.' }

    $jwtBytes = [Security.Cryptography.RandomNumberGenerator]::GetBytes(48)
    $jwt = [Convert]::ToBase64String($jwtBytes)
    $python = Join-Path $InstallRoot 'ai\python.exe'

    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $scriptRoot 'configure.ps1') -InstallRoot $InstallRoot -DataRoot $DataRoot -DbPassword $dbPassword -JwtSigningKey $jwt -WebPort $WebPort -AdminEmail $AdminEmail -AdminName $AdminName -License $License -ResearchEmail $ResearchEmail
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $scriptRoot 'install-db.ps1') -SchemaPath (Join-Path $InstallRoot 'db\schema.sql') -SeedScript (Join-Path $InstallRoot 'db\seed_lite.py') -AiServiceDir (Join-Path $InstallRoot 'ai') -PythonPath $python -DbPassword $dbPassword -AdminEmail $AdminEmail -AdminName $AdminName -License $License -ResearchEmail $ResearchEmail
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $scriptRoot 'register-services.ps1') -InstallRoot $InstallRoot -DataRoot $DataRoot -WebPort $WebPort
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $scriptRoot 'health-check.ps1') -WebPort $WebPort
} finally {
    Stop-Transcript | Out-Null
}
