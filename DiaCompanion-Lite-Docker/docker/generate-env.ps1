# Sinh .env với mật khẩu/khóa ngẫu nhiên (Windows PowerShell 5.1+ / PowerShell 7).
param([string]$AdminEmail = "bacsi@benhvien.local", [string]$ResearchEmail = "nghiencuu@benhvien.local")
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")
if (Test-Path .env) { Write-Error ".env đã tồn tại — không ghi đè."; exit 1 }
function New-Rand([int]$n) {
  $chars = [char[]]([char]'A'..[char]'Z' + [char]'a'..[char]'z' + [char]'0'..[char]'9')
  $bytes = New-Object byte[] $n
  [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
  -join ($bytes | ForEach-Object { $chars[$_ % $chars.Length] })
}
$lines = @(
  "MSSQL_SA_PASSWORD=Aa1$(New-Rand 21)",
  "JWT_SIGNING_KEY=$(New-Rand 64)",
  "LITE_ADMIN_EMAIL=$AdminEmail",
  "LITE_ADMIN_NAME=Bác sĩ",
  "LITE_LICENSE=NCKH-001",
  "LITE_RESEARCH_EMAIL=$ResearchEmail",
  "WEB_PORT=8080",
  "ALLOW_MISSING_WEIGHTS=0"
)
# UTF-8 không BOM để docker compose đọc đúng tiếng Việt.
[System.IO.File]::WriteAllLines((Join-Path (Get-Location) ".env"), $lines, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Đã tạo .env"
