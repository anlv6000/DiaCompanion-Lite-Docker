# Chạy bộ kiểm thử DR AI-Diagnosis trên Docker, trong project RIÊNG "dr-ai-diagnosis-test".
# Không đụng tới bản dev (project dr-ai-diagnosis, cổng 8080) hay máy cài production.
#
#   .\docker\test\run-tests.ps1                    dựng môi trường sạch -> chạy test -> sinh báo cáo -> dọn
#   .\docker\test\run-tests.ps1 -Keep              giữ môi trường (http://localhost:8090) để làm checklist giao diện
#   .\docker\test\run-tests.ps1 -PlanOnly          chỉ sinh "Kế hoạch và kịch bản kiểm thử" (không cần dựng hệ thống)
#   .\docker\test\run-tests.ps1 -Tester "Họ tên"   ghi tên người thực hiện vào báo cáo
#   .\docker\test\run-tests.ps1 -Clean             chỉ dọn môi trường test còn sót
[CmdletBinding()]
param(
    [switch]$Keep,
    [switch]$PlanOnly,
    [switch]$Clean,
    [switch]$NoBuild,
    [string]$Tester = '',
    [string]$Version = '1.3.1',
    [int]$Port = 8090,
    [int]$StartTimeoutMinutes = 20
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repo
$envFile = Join-Path $PSScriptRoot '.test.env'
$runDir = Join-Path $repo 'test-run'
$compose = @('compose', '-p', 'dr-ai-diagnosis-test', '--env-file', $envFile,
    '-f', 'docker-compose.yml', '-f', 'docker/test/docker-compose.test.yml')

function Step([string]$m) { Write-Host ''; Write-Host "==> $m" -ForegroundColor Cyan }
function DC { & docker @compose @args; if ($LASTEXITCODE -ne 0) { throw "docker compose $($args -join ' ') lỗi (mã $LASTEXITCODE)." } }
function Rand([int]$n) {
    $chars = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789'
    -join (1..$n | ForEach-Object { $chars[(Get-Random -Maximum $chars.Length)] })
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Chưa cài Docker hoặc Docker Desktop chưa chạy.' }

# Tệp env của môi trường test: sinh mới mỗi lần (mật khẩu ngẫu nhiên, chỉ dùng cho lần chạy này). UTF-8 KHÔNG BOM.
$envText = @(
    "MSSQL_SA_PASSWORD=Aa1$(Rand 21)"
    "JWT_SIGNING_KEY=$(Rand 64)"
    'LITE_ADMIN_EMAIL=bacsi.test@test.local'
    'LITE_ADMIN_NAME=Bác sĩ Kiểm thử'
    'LITE_LICENSE=TEST-001'
    'LITE_RESEARCH_EMAIL=nghiencuu.test@test.local'
    "WEB_PORT=$Port"
    'ALLOW_MISSING_WEIGHTS=0'
) -join "`n"
[IO.File]::WriteAllText($envFile, $envText + "`n", (New-Object Text.UTF8Encoding($false)))

$env:APP_VERSION = $Version
$env:TESTER_NAME = $Tester
$env:GIT_COMMIT = ''
if ((Get-Command git -ErrorAction SilentlyContinue) -and (Test-Path (Join-Path $repo '.git'))) {
    # PS 5.1 + ErrorActionPreference=Stop: stderr của lệnh ngoài bị coi là lỗi dừng, nên bọc try/catch.
    try { $c = (& git rev-parse --short HEAD); if ($LASTEXITCODE -eq 0 -and $c) { $env:GIT_COMMIT = "$c".Trim() } } catch { }
}

if ($Clean) {
    Step 'Dọn môi trường test'
    DC down -v --remove-orphans
    Write-Host 'Đã xóa container và dữ liệu của project dr-ai-diagnosis-test.'
    return
}

New-Item -ItemType Directory -Force -Path (Join-Path $runDir 'report') | Out-Null

if ($PlanOnly) {
    Step 'Sinh kế hoạch và kịch bản kiểm thử (không chạy)'
    DC build tester
    DC run --rm --no-deps tester --plan-only
    Write-Host "Tài liệu: $(Join-Path $runDir 'report')" -ForegroundColor Green
    return
}

foreach ($m in 'model_1', 'model_2', 'model_3') {
    if (-not (Get-ChildItem (Join-Path $repo "ai-weights\$m") -File -ErrorAction SilentlyContinue | Where-Object Name -ne '.gitkeep')) {
        throw "Thiếu trọng số AI trong ai-weights\$m (bộ test chạy AI thật)."
    }
}

Step 'Dọn lần chạy trước (CSDL và ảnh của môi trường test)'
DC down -v --remove-orphans
Remove-Item (Join-Path $runDir 'secrets'), (Join-Path $runDir 'exports') -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path (Join-Path $runDir 'secrets') | Out-Null

Step 'Dựng môi trường test (dùng lại cache image của bản dev; lần đầu có thể mất vài phút)'
if ($NoBuild) { DC up -d db dbinit ai backend web } else { DC up -d --build db dbinit ai backend web }
# Image bộ test luôn build lại (vài giây, có cache): mã test nằm trong image, -NoBuild chỉ áp cho hệ thống.
DC build tester

Step 'Chờ CSDL khởi tạo xong và backend sẵn sàng'
$deadline = (Get-Date).AddMinutes($StartTimeoutMinutes)
while ($true) {
    $init = (& docker @compose ps -a -q dbinit | Select-Object -First 1)
    if ($init) {
        $st = (& docker inspect -f '{{.State.Status}} {{.State.ExitCode}}' $init)
        if ($st -match '^exited (\d+)$' -and $Matches[1] -ne '0') { & docker @compose logs --tail 40 dbinit; throw 'dbinit lỗi (xem log phía trên).' }
    }
    $be = (& docker @compose ps -q backend | Select-Object -First 1)
    if ($be) {
        $h = (& docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' $be)
        if ($h -eq 'healthy') { break }
    }
    if ((Get-Date) -gt $deadline) { & docker @compose ps; throw "Quá $StartTimeoutMinutes phút backend vẫn chưa sẵn sàng." }
    Start-Sleep -Seconds 5
}
Write-Host 'Backend sẵn sàng.'

Step 'Tạo hai tài khoản người chấm độc lập'
foreach ($n in 1, 2) {
    DC run --rm -e "GRADER_EMAIL=cham$n.test@test.local" -e "GRADER_NAME=Grader Test $n" dbinit add-grader
    Copy-Item (Join-Path $runDir 'secrets\grader_credentials.txt') (Join-Path $runDir "secrets\grader$n.txt") -Force
}

Step 'Chạy bộ test (bước AI trên CPU có thể mất vài phút)'
& docker @compose run --rm tester
$code = $LASTEXITCODE

$report = Get-ChildItem (Join-Path $runDir 'report') -Filter 'Bao-cao-kiem-thu-*.docx' | Sort-Object LastWriteTime | Select-Object -Last 1
Write-Host ''
if ($report) { Write-Host "Tài liệu kiểm thử: $($report.FullName)" -ForegroundColor Green }

if ($Keep) {
    Write-Host "Môi trường test đang chạy: http://localhost:$Port" -ForegroundColor Yellow
    Write-Host "Tài khoản (dữ liệu giả): $(Join-Path $runDir 'secrets\test_accounts.txt')"
    Write-Host 'Dọn khi xong: .\docker\test\run-tests.ps1 -Clean'
} else {
    Step 'Dọn môi trường test'
    DC down -v --remove-orphans
}
if ($code -ne 0) { Write-Host 'KẾT QUẢ: CÓ CASE LỖI — xem mục 6 của tài liệu.' -ForegroundColor Red } else { Write-Host 'KẾT QUẢ: TẤT CẢ ĐẠT.' -ForegroundColor Green }
exit $code
