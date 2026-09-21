[CmdletBinding()]
param(
    [string]$Version = '1.0.0',
    [Parameter(Mandatory = $true)][string]$AiRuntime,
    [Parameter(Mandatory = $true)][string]$SqlServerInstaller,
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$OutputRoot = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $OutputRoot) { $OutputRoot = Join-Path $RepoRoot 'release' }
$installerRoot = Join-Path $RepoRoot 'installer'
$stage = Join-Path $installerRoot 'build\stage'
$backendProject = Join-Path $RepoRoot 'Backend\DiaCompanion\DiaCompanion.csproj'
$frontendRoot = Join-Path $RepoRoot 'Frontend'
$aiSource = Join-Path $RepoRoot 'ai_service'
$weightsRoot = Join-Path $RepoRoot 'ai-weights'

foreach ($path in @($AiRuntime, $SqlServerInstaller, $backendProject, $aiSource, $weightsRoot)) {
    if (-not (Test-Path $path)) { throw "Required build input missing: $path" }
}
$iss = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if (-not $iss) { throw 'ISCC.exe was not found. Install Inno Setup 6 on the build machine.' }

Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $stage, (Join-Path $stage 'backend'), (Join-Path $stage 'ai'), (Join-Path $stage 'db'), (Join-Path $stage 'scripts'), (Join-Path $stage 'sqlserver') | Out-Null

Write-Host 'Publishing self-contained Backend...'
dotnet publish $backendProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o (Join-Path $stage 'backend')

Write-Host 'Building Lite frontend...'
Push-Location $frontendRoot
try { npm ci --no-audit --no-fund; npm run build:lite }
finally { Pop-Location }
Copy-Item (Join-Path $frontendRoot 'dist-lite\*') (Join-Path $stage 'backend') -Recurse -Force
Copy-Item (Join-Path $frontendRoot 'public\config.js') (Join-Path $stage 'backend\config.js') -Force

Write-Host 'Copying prebuilt offline AI runtime and source...'
Copy-Item (Join-Path $AiRuntime '*') (Join-Path $stage 'ai') -Recurse -Force
Copy-Item (Join-Path $aiSource 'app.py'), (Join-Path $aiSource 'model_runners.py') (Join-Path $stage 'ai') -Force
Copy-Item (Join-Path $aiSource 'models') (Join-Path $stage 'ai') -Recurse -Force
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'ai\models\model_1\weights'), (Join-Path $stage 'ai\models\model_2\weights'), (Join-Path $stage 'ai\models\model_3\weights') | Out-Null
Copy-Item (Join-Path $weightsRoot 'model_1\*') (Join-Path $stage 'ai\models\model_1\weights') -Force
Copy-Item (Join-Path $weightsRoot 'model_2\*') (Join-Path $stage 'ai\models\model_2\weights') -Force
Copy-Item (Join-Path $weightsRoot 'model_3\*') (Join-Path $stage 'ai\models\model_3\weights') -Force

$manifest = Get-ChildItem (Join-Path $stage 'ai\models') -File -Recurse | ForEach-Object {
    [ordered]@{ file = $_.FullName.Substring((Join-Path $stage 'ai').Length + 1); sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
}
$manifest | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $stage 'ai\model-manifest.json') -Encoding UTF8

Write-Host 'Generating EF schema...'
dotnet ef dbcontext script --project $backendProject --configuration Release --output (Join-Path $stage 'db\schema.sql')
Copy-Item (Join-Path $RepoRoot 'Lite\seed_lite.py') (Join-Path $stage 'db\seed_lite.py') -Force
Copy-Item (Join-Path $RepoRoot 'Lite\export_dataset.sql') (Join-Path $stage 'db\export_dataset.sql') -Force
Copy-Item (Join-Path $SqlServerInstaller) (Join-Path $stage 'sqlserver\SQLEXPR_x64_ENU.exe') -Force
Copy-Item (Join-Path $installerRoot 'scripts\*.ps1') (Join-Path $stage 'scripts') -Force

$payloadJson = [ordered]@{ ApplicationVersion = $Version; BuiltAtUtc = [DateTime]::UtcNow.ToString('o'); ModelFiles = $manifest }
$payloadJson | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $stage 'payload-manifest.json') -Encoding UTF8

$releaseDir = Join-Path $OutputRoot ''
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
& $iss.Source "/DAppVersion=$Version" "/DStageDir=$stage" "/DOutputDir=$releaseDir" (Join-Path $installerRoot 'DiaCompanion.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }
Write-Host "Installer created in $releaseDir"
