[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$InstallRoot,
    [Parameter(Mandatory = $true)][string]$DataRoot,
    [int]$WebPort = 8080
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$logRoot = Join-Path $DataRoot 'logs'
New-Item -ItemType Directory -Force -Path $logRoot | Out-Null

$aiPython = Join-Path $InstallRoot 'ai\python.exe'
$aiWork = Join-Path $InstallRoot 'ai'
$backendExe = Join-Path $InstallRoot 'backend\DiaCompanion.exe'
foreach ($path in @($aiPython, $backendExe)) { if (-not (Test-Path $path)) { throw "Required runtime file missing: $path" } }

sc.exe stop DiaCompanionBackend | Out-Null
sc.exe stop DiaCompanionAI | Out-Null
sc.exe delete DiaCompanionBackend | Out-Null
sc.exe delete DiaCompanionAI | Out-Null

sc.exe create DiaCompanionAI binPath= "`"$aiPython`" -m uvicorn app:app --app-dir `"$aiWork`" --host 127.0.0.1 --port 8000" start= auto DisplayName= "DiaCompanion AI" | Out-Null
sc.exe description DiaCompanionAI "DiaCompanion Lite CPU AI inference service" | Out-Null
sc.exe failure DiaCompanionAI reset= 86400 actions= restart/5000/restart/15000/restart/60000 | Out-Null

sc.exe create DiaCompanionBackend binPath= "`"$backendExe`"" start= auto DisplayName= "DiaCompanion Backend" | Out-Null
sc.exe description DiaCompanionBackend "DiaCompanion Lite web and API service" | Out-Null
sc.exe failure DiaCompanionBackend reset= 86400 actions= restart/5000/restart/15000/restart/60000 | Out-Null
sc.exe config DiaCompanionBackend depend= 'DiaCompanionAI/MSSQL$SQLEXPRESS' | Out-Null

New-NetFirewallRule -DisplayName 'DiaCompanion Lite Web' -Direction Inbound -Protocol TCP -LocalPort $WebPort -Action Allow -Profile Domain,Private -ErrorAction SilentlyContinue | Out-Null
Remove-NetFirewallRule -DisplayName 'DiaCompanion Lite AI' -ErrorAction SilentlyContinue

sc.exe start DiaCompanionAI | Out-Null
sc.exe start DiaCompanionBackend | Out-Null
Write-Host 'DiaCompanion services registered and started.'
