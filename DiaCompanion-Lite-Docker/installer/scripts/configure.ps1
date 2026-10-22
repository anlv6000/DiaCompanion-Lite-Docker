# Ghi cấu hình backend + thông tin cài đặt. Gọi từ install-runtime.ps1.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$InstallRoot,
    [Parameter(Mandatory = $true)][string]$DataRoot,
    [Parameter(Mandatory = $true)][string]$DbPassword,
    [Parameter(Mandatory = $true)][string]$JwtSigningKey,
    [int]$WebPort = 8080,
    [string]$AdminEmail = 'bacsi@benhvien.local',
    [string]$AdminName = 'Bác sĩ',
    [string]$License = 'NCKH-001',
    [string]$ResearchEmail = ''
)
. (Join-Path $PSScriptRoot 'common.ps1')

$configRoot = Get-ConfigRoot $DataRoot
$storageRoot = Join-Path $DataRoot 'storage'
New-Item -ItemType Directory -Force -Path (Join-Path $DataRoot 'logs'), (Join-Path $storageRoot 'fundus'), (Join-Path $storageRoot 'ai_masks') | Out-Null
Protect-ConfigRoot $configRoot

$backendConfig = [ordered]@{
    ConnectionStrings = @{ Default = (Get-ConnectionString $DbPassword) }
    # 1.3.1: issuer/audience theo tên mới; phiên đăng nhập cũ hết hiệu lực, mọi người đăng nhập lại.
    Jwt = @{ Issuer = 'dr-ai-diagnosis'; Audience = 'dr-ai-diagnosis-web'; SigningKey = $JwtSigningKey; ExpiryMinutes = 480 }
    Storage = @{ FundusRoot = (Join-Path $storageRoot 'fundus'); AiMasksRoot = (Join-Path $storageRoot 'ai_masks'); MaxUploadBytes = 10485760 }
    AiService = @{ BaseUrl = 'http://127.0.0.1:8000'; TimeoutSeconds = 900 }   # máy CPU yếu: phân tích có thể > 5 phút
    Clinic = @{ TimeZone = 'SE Asia Standard Time' }
    Urls = "http://0.0.0.0:$WebPort"
}
$appsettings = Join-Path $InstallRoot 'backend\appsettings.Production.json'
Write-Json $backendConfig $appsettings
# File này chứa mật khẩu SA: chỉ SYSTEM + Administrators đọc được.
icacls $appsettings /inheritance:r /grant:r '*S-1-5-18:F' '*S-1-5-32-544:F' | Out-Null

Write-Json ([ordered]@{
    WebPort = $WebPort; AdminEmail = $AdminEmail; AdminName = $AdminName; License = $License
    ResearchEmail = $ResearchEmail; DataRoot = $DataRoot; InstallRoot = $InstallRoot
    ConfiguredAtUtc = [DateTime]::UtcNow.ToString('o')
}) (Join-Path $configRoot 'installer.json')
# Inno Setup đọc cổng từ đây khi cài đè (không phải phân tích JSON).
Set-Content -Path (Join-Path $configRoot 'port.txt') -Value $WebPort -Encoding ASCII -NoNewline
Write-Host "Đã ghi cấu hình vào $configRoot"
