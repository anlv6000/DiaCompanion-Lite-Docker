# Build Setup.exe offline cho bệnh viện (DR AI-Diagnosis). Chạy trên máy build (cần Internet lần đầu).
# Đường dẫn AI runtime mặc định giữ tên thư mục cũ C:\Build\DiaCompanion\ai-runtime để máy build đã có sẵn không phải làm lại.
[CmdletBinding()]
param(
    [string]$Version = '1.0.0',
    [string]$AiRuntime = 'C:\Build\DiaCompanion\ai-runtime',
    [string]$SqlServerInstaller = '',
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$OutputRoot = ''
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Step([string]$m) { Write-Host ''; Write-Host "==> $m" -ForegroundColor Cyan }
function Check([string]$what) { if ($LASTEXITCODE -ne 0) { throw "$what lỗi (mã $LASTEXITCODE)." } }
function Get-Payload([string]$Path, [string]$Url) {
    if (Test-Path $Path) { return }
    New-Item -ItemType Directory -Force -Path (Split-Path $Path -Parent) | Out-Null
    Write-Host "Tải $Url"
    Invoke-WebRequest $Url -OutFile $Path -UseBasicParsing
}

$installerRoot = Join-Path $RepoRoot 'installer'
$payload = Join-Path $installerRoot 'payload'
if (-not $OutputRoot) { $OutputRoot = Join-Path $RepoRoot 'release' }
if (-not $SqlServerInstaller) { $SqlServerInstaller = Join-Path $payload 'sqlserver\SQLEXPR_x64_ENU.exe' }
$stage = Join-Path $installerRoot 'build\stage'
$backendProject = Join-Path $RepoRoot 'Backend\DiaCompanion\DiaCompanion.csproj'
$frontendRoot = Join-Path $RepoRoot 'Frontend'
$aiSource = Join-Path $RepoRoot 'ai_service'
$weightsRoot = Join-Path $RepoRoot 'ai-weights'

Step 'Kiểm tra đầu vào'
$required = @{
    'AI runtime (chạy prepare-ai-runtime.ps1 trước)' = (Join-Path $AiRuntime 'python.exe')
    'Bộ cài SQL Server Express'                    = $SqlServerInstaller
    'Backend project'                               = $backendProject
    'Trọng số model 1'                              = (Join-Path $weightsRoot 'model_1')
    'Trọng số model 2'                              = (Join-Path $weightsRoot 'model_2')
    'Trọng số model 3'                              = (Join-Path $weightsRoot 'model_3')
}
foreach ($k in $required.Keys) { if (-not (Test-Path $required[$k])) { throw "Thiếu ${k}: $($required[$k])" } }
foreach ($m in 'model_1', 'model_2', 'model_3') {
    if (-not (Get-ChildItem (Join-Path $weightsRoot $m) -File | Where-Object Name -ne '.gitkeep')) { throw "ai-weights\$m đang trống." }
}
foreach ($t in 'dotnet', 'npm') { if (-not (Get-Command $t -ErrorAction SilentlyContinue)) { throw "Chưa cài $t trên máy build." } }
$iscc = @((Get-Command ISCC.exe -ErrorAction SilentlyContinue | ForEach-Object Source),
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe") | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) { throw 'Không tìm thấy ISCC.exe. Cài Inno Setup 6 trên máy build.' }

Step 'Tải payload phụ (chỉ lần đầu)'
$winsw = Join-Path $payload 'winsw\WinSW-x64.exe'
$vcredist = Join-Path $payload 'prereq\vc_redist.x64.exe'
Get-Payload $winsw 'https://github.com/winsw/winsw/releases/download/v2.12.0/WinSW-x64.exe'
Get-Payload $vcredist 'https://aka.ms/vs/17/release/vc_redist.x64.exe'

Step 'Dọn thư mục stage'
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
foreach ($d in 'backend', 'ai', 'db', 'scripts', 'sqlserver', 'services', 'prereq') { New-Item -ItemType Directory -Force -Path (Join-Path $stage $d) | Out-Null }

Step 'Publish backend (.NET 8, self-contained)'
# Luôn biên dịch sạch: mã nguồn chép từ zip giữ giờ sửa CŨ hơn obj\ của lần build trước,
# khiến MSBuild bỏ qua biên dịch và đóng gói lại DLL cũ.
$backendDir = Split-Path $backendProject -Parent
Remove-Item (Join-Path $backendDir 'obj'), (Join-Path $backendDir 'bin') -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish $backendProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o (Join-Path $stage 'backend')
Check 'dotnet publish'
Remove-Item (Join-Path $stage 'backend\appsettings.Development.json') -Force -ErrorAction SilentlyContinue

Step 'Build giao diện Lite'
Push-Location $frontendRoot
try { npm ci --no-audit --no-fund; Check 'npm ci'; npm run build:lite; Check 'npm run build:lite' }
finally { Pop-Location }
# ASP.NET Core chỉ phục vụ file tĩnh trong wwwroot.
$wwwroot = Join-Path $stage 'backend\wwwroot'
New-Item -ItemType Directory -Force -Path $wwwroot | Out-Null
Copy-Item (Join-Path $frontendRoot 'dist-lite\*') $wwwroot -Recurse -Force
Copy-Item (Join-Path $RepoRoot 'docker\web\config.js') (Join-Path $wwwroot 'config.js') -Force

Step 'Chép AI runtime, mã nguồn và trọng số'
Copy-Item (Join-Path $AiRuntime '*') (Join-Path $stage 'ai') -Recurse -Force
Copy-Item (Join-Path $aiSource 'app.py'), (Join-Path $aiSource 'model_runners.py') (Join-Path $stage 'ai') -Force
Copy-Item (Join-Path $aiSource 'models') (Join-Path $stage 'ai') -Recurse -Force
foreach ($m in 'model_1', 'model_2', 'model_3') {
    $dst = Join-Path $stage "ai\models\$m\weights"
    New-Item -ItemType Directory -Force -Path $dst | Out-Null
    Get-ChildItem (Join-Path $weightsRoot $m) -File | Where-Object Name -ne '.gitkeep' | Copy-Item -Destination $dst -Force
}
# Bỏ file chỉ dùng khi build / ảnh mẫu
Get-ChildItem (Join-Path $stage 'ai\models') -Directory -Recurse -Filter outputs | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
$site = Join-Path $stage 'ai\Lib\site-packages'
Remove-Item (Join-Path $site 'tensorflow\include') -Recurse -Force -ErrorAction SilentlyContinue
# Chỉ bỏ __pycache__. KHÔNG xóa thư mục test/tests: TensorFlow import tensorflow._api.v2.__internal__.test
# ngay khi khởi động, xóa đi là model 3 (vessel/fractal) lỗi circular import.
Get-ChildItem $site -Directory -Recurse -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -eq '__pycache__' } | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

Write-Host 'Kiểm tra runtime AI sau khi dọn...'
& (Join-Path $stage 'ai\python.exe') -c "import tensorflow as tf, torch, fastapi, uvicorn, cv2, segmentation_models_pytorch; print('OK tf', tf.__version__, '| torch', torch.__version__)"
Check 'Import thư viện AI trong thư mục stage'

$manifest = @(Get-ChildItem (Join-Path $stage 'ai\models') -File -Recurse | ForEach-Object {
    [ordered]@{ file = $_.FullName.Substring((Join-Path $stage 'ai').Length + 1); sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
})
$manifest | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $stage 'ai\model-manifest.json') -Encoding UTF8

Step 'Sinh schema database từ EF Core'
if (-not (Get-Command dotnet-ef -ErrorAction SilentlyContinue)) {
    dotnet tool install --global dotnet-ef --version 8.0.8; Check 'Cài dotnet-ef'
    $env:PATH = "$env:USERPROFILE\.dotnet\tools;$env:PATH"
}
$schema = Join-Path $RepoRoot 'Backend\DiaCompanion\Database\lite_schema.sql'
if (Test-Path $schema) {
    Write-Host "Dùng schema xuất tay: $schema"
    Copy-Item $schema (Join-Path $stage 'db\schema.sql') -Force
} else {
    dotnet ef dbcontext script --project $backendProject --configuration Release --output (Join-Path $stage 'db\schema.sql')
    Check 'dotnet ef dbcontext script'
}

Step 'Chép script, seed, SQL Server, WinSW, VC++'
Copy-Item (Join-Path $RepoRoot 'Lite\seed_lite.py'), (Join-Path $RepoRoot 'Lite\export_dataset.sql') (Join-Path $stage 'db') -Force
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'db\migrations') | Out-Null
Copy-Item (Join-Path $RepoRoot 'Lite\migrations\*.sql') (Join-Path $stage 'db\migrations') -Force
Copy-Item $SqlServerInstaller (Join-Path $stage 'sqlserver\SQLEXPR_x64_ENU.exe') -Force
Copy-Item (Join-Path $installerRoot 'scripts\*.ps1') (Join-Path $stage 'scripts') -Force
Remove-Item (Join-Path $stage 'scripts\build-installer.ps1'), (Join-Path $stage 'scripts\prepare-ai-runtime.ps1') -Force

Step 'Chuẩn hóa mã hóa và kiểm tra cú pháp script (Windows PowerShell 5.1)'
# PowerShell 5.1 đọc .ps1 KHÔNG có BOM theo mã ANSI: chữ Việt UTF-8 bị đọc sai, có byte thành dấu nháy cong
# làm hỏng chuỗi -> script không chạy được, không kịp ghi log. Luôn ghi lại dạng UTF-8 CÓ BOM rồi parse thử.
$utf8Strict = New-Object Text.UTF8Encoding($false, $true)
$utf8Bom = New-Object Text.UTF8Encoding($true)
foreach ($f in Get-ChildItem (Join-Path $stage 'scripts') -Filter *.ps1) {
    try { $text = $utf8Strict.GetString([IO.File]::ReadAllBytes($f.FullName)) }
    catch { throw "$($f.Name) không phải UTF-8 (có thể đã bị lưu dạng ANSI). Mở bằng VS Code, Save with Encoding > UTF-8 with BOM, rồi build lại." }
    [IO.File]::WriteAllText($f.FullName, $text.TrimStart([char]0xFEFF), $utf8Bom)
    $errs = $null
    [void][Management.Automation.Language.Parser]::ParseFile($f.FullName, [ref]$null, [ref]$errs)
    if ($errs) { throw "Lỗi cú pháp trong $($f.Name), dòng $($errs[0].Extent.StartLineNumber): $($errs[0].Message)" }
}
Write-Host 'Script OK.'
Copy-Item $winsw (Join-Path $stage 'services\WinSW-x64.exe') -Force
Copy-Item $vcredist (Join-Path $stage 'prereq\vc_redist.x64.exe') -Force

[ordered]@{ ApplicationVersion = $Version; BuiltAtUtc = [DateTime]::UtcNow.ToString('o'); ModelFiles = $manifest } |
    ConvertTo-Json -Depth 6 | Set-Content (Join-Path $stage 'payload-manifest.json') -Encoding UTF8

Step 'Đóng gói bằng Inno Setup'
$releaseDir = [IO.Path]::GetFullPath($OutputRoot)
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
# Inno Setup cũng cần UTF-8 có BOM để giữ tiếng Việt: biên dịch từ bản đã chuẩn hóa.
$issBuild = Join-Path $installerRoot 'build\DiaCompanion.iss'
$issText = $utf8Strict.GetString([IO.File]::ReadAllBytes((Join-Path $installerRoot 'DiaCompanion.iss'))).TrimStart([char]0xFEFF)
[IO.File]::WriteAllText($issBuild, $issText, $utf8Bom)
& $iscc "/DAppVersion=$Version" "/DStageDir=$stage" "/DOutputDir=$releaseDir" $issBuild
Check 'Inno Setup'
$exe = Join-Path $releaseDir "DR-AI-Diagnosis-Setup-x64-$Version.exe"
Write-Host ''
Write-Host "XONG: $exe" -ForegroundColor Green
Write-Host ("Dung lượng: {0:N0} MB · SHA256: {1}" -f ((Get-Item $exe).Length / 1MB), (Get-FileHash $exe).Hash)
