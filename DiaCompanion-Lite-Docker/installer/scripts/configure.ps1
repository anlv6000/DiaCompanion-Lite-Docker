[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$InstallRoot,
    [Parameter(Mandatory = $true)][string]$DataRoot,
    [Parameter(Mandatory = $true)][string]$DbPassword,
    [Parameter(Mandatory = $true)][string]$JwtSigningKey,
    [int]$WebPort = 8080,
    [string]$AdminEmail = 'bacsi@benhvien.local',
    [string]$AdminName = 'Bac si',
    [string]$License = 'NCKH-001',
    [string]$ResearchEmail = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$configRoot = Join-Path $DataRoot 'config'
$storageRoot = Join-Path $DataRoot 'storage'
$logRoot = Join-Path $DataRoot 'logs'
New-Item -ItemType Directory -Force -Path $configRoot, $storageRoot, $logRoot, (Join-Path $storageRoot 'fundus'), (Join-Path $storageRoot 'ai_masks') | Out-Null

$connectionString = "Server=.\SQLEXPRESS;Database=DiaCompanion;User Id=sa;Password=$DbPassword;TrustServerCertificate=True;Encrypt=False"
$backendConfig = [ordered]@{
    ConnectionStrings = @{ Default = $connectionString }
    Jwt = @{ Issuer = 'diacompanion'; Audience = 'diacompanion-web'; SigningKey = $JwtSigningKey; ExpiryMinutes = 480 }
    Storage = @{ FundusRoot = (Join-Path $storageRoot 'fundus'); AiMasksRoot = (Join-Path $storageRoot 'ai_masks'); MaxUploadBytes = 10485760 }
    AiService = @{ BaseUrl = 'http://127.0.0.1:8000'; TimeoutSeconds = 300 }
    Clinic = @{ TimeZone = 'SE Asia Standard Time' }
    Urls = "http://0.0.0.0:$WebPort"
}
$backendConfigPath = Join-Path $InstallRoot 'backend\appsettings.Production.json'
$backendConfig | ConvertTo-Json -Depth 8 | Set-Content -Path $backendConfigPath -Encoding UTF8

$installerConfig = [ordered]@{
    WebPort = $WebPort
    AdminEmail = $AdminEmail
    AdminName = $AdminName
    License = $License
    ResearchEmail = $ResearchEmail
    DataRoot = $DataRoot
    InstalledAtUtc = [DateTime]::UtcNow.ToString('o')
}
$installerConfig | ConvertTo-Json -Depth 4 | Set-Content -Path (Join-Path $configRoot 'installer.json') -Encoding UTF8

icacls $configRoot /inheritance:r /grant:r 'SYSTEM:(OI)(CI)F' 'Administrators:(OI)(CI)F' | Out-Null
Write-Host "Configuration written to $configRoot"
