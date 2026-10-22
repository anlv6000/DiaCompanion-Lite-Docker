# Tạo Python runtime "mang đi được" cho AI (chạy MỘT lần trên máy build, cần Internet).
# Dùng gói NuGet chính thức của python.org: bản Python đầy đủ dạng thư mục, không cần cài,
# không ghi registry, không bị ảnh hưởng bởi Python đã cài sẵn trên máy.
[CmdletBinding()]
param(
    [string]$Target = 'C:\Build\DiaCompanion\ai-runtime',
    [string]$PythonVersion = '3.11.9',
    [switch]$Force
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'   # Invoke-WebRequest nhanh hơn nhiều khi tắt thanh tiến độ
$lock = Join-Path $PSScriptRoot '..\requirements.lock.txt'
$py = Join-Path $Target 'python.exe'

function Check([string]$what) { if ($LASTEXITCODE -ne 0) { throw "$what lỗi (mã $LASTEXITCODE)." } }

if ($Force -and (Test-Path $Target)) { Remove-Item $Target -Recurse -Force }

if (-not (Test-Path $py)) {
    # Tải/giải nén cạnh thư mục đích, không dùng %TEMP%: khi tên tài khoản có dấu cách, %TEMP% là
    # đường dẫn 8.3 (C:\Users\KIENLE~1\...) và Remove-Item/Expand-Archive của PowerShell 5.1 lỗi với nó.
    $cache = Join-Path ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($Target))) '_cache'
    [void][IO.Directory]::CreateDirectory($cache)
    $pkg = Join-Path $cache "python.$PythonVersion.nupkg.zip"
    $tmp = Join-Path $cache "python-$PythonVersion-nuget"
    if (-not (Test-Path $pkg)) {
        Write-Host "Tải Python $PythonVersion (gói NuGet)..."
        Invoke-WebRequest "https://www.nuget.org/api/v2/package/python/$PythonVersion" -OutFile $pkg -UseBasicParsing
    }
    Write-Host "Giải nén vào $Target ..."
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
    Expand-Archive $pkg -DestinationPath $tmp -Force
    New-Item -ItemType Directory -Force -Path $Target | Out-Null
    Copy-Item (Join-Path $tmp 'tools\*') $Target -Recurse -Force
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
    if (-not (Test-Path $py)) { throw "Giải nén xong nhưng không thấy $py." }
    & $py -m ensurepip --upgrade; Check 'ensurepip'
} else {
    Write-Host "Đã có $py — chỉ cài/kiểm tra lại thư viện. Dùng -Force để làm lại từ đầu."
}

& $py --version; Check 'python'
& $py -m pip install --upgrade pip --no-warn-script-location; Check 'Nâng cấp pip'
& $py -m pip install -r $lock --extra-index-url https://download.pytorch.org/whl/cpu --no-warn-script-location
Check 'pip install requirements.lock.txt'
& $py -c "import fastapi, uvicorn, torch, tensorflow, cv2, segmentation_models_pytorch; print('OK torch', torch.__version__, '| tf', tensorflow.__version__)"
Check 'Kiểm tra import thư viện AI'
Write-Host "Runtime AI sẵn sàng: $Target" -ForegroundColor Green
