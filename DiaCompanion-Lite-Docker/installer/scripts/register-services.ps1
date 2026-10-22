# Đăng ký 2 Windows Service qua WinSW (python.exe và DRAIDiagnosis.exe không tự
# nói chuyện được với Service Control Manager, nên cần wrapper).
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$InstallRoot,
    [Parameter(Mandatory = $true)][string]$DataRoot,
    [int]$WebPort = 8080
)
. (Join-Path $PSScriptRoot 'common.ps1')

$svcDir = Join-Path $InstallRoot 'services'
$winsw = Join-Path $svcDir 'WinSW-x64.exe'
$aiDir = Join-Path $InstallRoot 'ai'
$backendDir = Join-Path $InstallRoot 'backend'
$storage = Join-Path $DataRoot 'storage'
$logs = Join-Path $DataRoot 'logs'
foreach ($p in @($winsw, (Join-Path $aiDir 'python.exe'), (Join-Path $backendDir 'DRAIDiagnosis.exe'), (Join-Path $backendDir 'wwwroot\index.html'))) {
    if (-not (Test-Path $p)) { throw "Thiếu tệp cần thiết: $p" }
}

& (Join-Path $PSScriptRoot 'uninstall-services.ps1') -InstallRoot $InstallRoot

function Esc([string]$s) { [Security.SecurityElement]::Escape($s) }
$common = @"
  <startmode>Automatic</startmode>
  <stoptimeout>20 sec</stoptimeout>
  <onfailure action="restart" delay="10 sec"/>
  <onfailure action="restart" delay="30 sec"/>
  <onfailure action="restart" delay="60 sec"/>
  <resetfailure>1 hour</resetfailure>
  <logpath>$(Esc $logs)</logpath>
  <log mode="roll-by-size"><sizeThreshold>10240</sizeThreshold><keepFiles>5</keepFiles></log>
"@

$defs = @{
    $script:ServiceAI = @"
<service>
  <id>$($script:ServiceAI)</id>
  <name>DR AI-Diagnosis AI</name>
  <description>DR AI-Diagnosis - dịch vụ AI (CPU), chỉ nghe 127.0.0.1:8000</description>
  <executable>$(Esc (Join-Path $aiDir 'python.exe'))</executable>
  <arguments>-m uvicorn app:app --app-dir "$(Esc $aiDir)" --host 127.0.0.1 --port 8000</arguments>
  <workingdirectory>$(Esc $aiDir)</workingdirectory>
  <env name="FUNDUS_ROOT" value="$(Esc $storage)"/>
  <env name="MASK_OUTPUT_ROOT" value="$(Esc (Join-Path $storage 'ai_masks'))"/>
  <env name="PYTHONUNBUFFERED" value="1"/>
  <env name="PYTHONIOENCODING" value="utf-8"/>
  <env name="DIACOMPANION_PRELOAD" value="1"/>
$common
</service>
"@
    $script:ServiceBackend = @"
<service>
  <id>$($script:ServiceBackend)</id>
  <name>DR AI-Diagnosis Backend</name>
  <description>DR AI-Diagnosis - web + API, cổng $WebPort</description>
  <executable>$(Esc (Join-Path $backendDir 'DRAIDiagnosis.exe'))</executable>
  <workingdirectory>$(Esc $backendDir)</workingdirectory>
  <env name="ASPNETCORE_ENVIRONMENT" value="Production"/>
  <depend>MSSQL`$SQLEXPRESS</depend>
$common
</service>
"@
}

foreach ($id in $script:Services) {
    $exe = Join-Path $svcDir "$id.exe"
    Copy-Item $winsw $exe -Force
    Set-Content -Path (Join-Path $svcDir "$id.xml") -Value $defs[$id] -Encoding UTF8
    & $exe install
    if ($LASTEXITCODE -ne 0) { throw "Không đăng ký được service $id (mã $LASTEXITCODE)." }
}

# Mạng bệnh viện hay bị Windows xếp vào Public: mở mọi profile nhưng chỉ cho mạng LAN cùng subnet.
Remove-NetFirewallRule -DisplayName $script:FirewallRule -ErrorAction SilentlyContinue
Remove-NetFirewallRule -DisplayName $script:LegacyFirewallRule -ErrorAction SilentlyContinue
New-NetFirewallRule -DisplayName $script:FirewallRule -Direction Inbound -Protocol TCP -LocalPort $WebPort `
    -Action Allow -Profile Any -RemoteAddress LocalSubnet | Out-Null

foreach ($id in @($script:ServiceAI, $script:ServiceBackend)) {
    Start-Service $id
}
Write-Host "Đã đăng ký và khởi động $($script:ServiceAI), $($script:ServiceBackend)."
