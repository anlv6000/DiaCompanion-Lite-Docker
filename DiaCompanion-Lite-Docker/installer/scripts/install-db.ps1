[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SchemaPath,
    [Parameter(Mandatory = $true)][string]$SeedScript,
    [Parameter(Mandatory = $true)][string]$AiServiceDir,
    [Parameter(Mandatory = $true)][string]$PythonPath,
    [Parameter(Mandatory = $true)][string]$DbPassword,
    [string]$AdminEmail = 'bacsi@benhvien.local',
    [string]$AdminName = 'Bac si',
    [string]$License = 'NCKH-001',
    [string]$ResearchEmail = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Find-SqlCmd {
    $candidates = @(
        (Get-Command sqlcmd.exe -ErrorAction SilentlyContinue).Source,
        "$env:ProgramFiles\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\SQLCMD.EXE",
        "$env:ProgramFiles\Microsoft SQL Server\160\Tools\Binn\SQLCMD.EXE",
        "$env:ProgramFiles\Microsoft SQL Server\150\Tools\Binn\SQLCMD.EXE"
    ) | Where-Object { $_ -and (Test-Path $_) }
    if (-not $candidates) { throw 'sqlcmd.exe was not found. Install SQL Server Express tools with the package.' }
    return $candidates[0]
}

$sqlcmd = Find-SqlCmd
$server = '.\SQLEXPRESS'
$base = @('-S', $server, '-U', 'sa', '-P', $DbPassword, '-C', '-b')
for ($attempt = 1; $attempt -le 60; $attempt++) {
    & $sqlcmd @base '-Q' 'SELECT 1' *> $null
    if ($LASTEXITCODE -eq 0) { break }
    if ($attempt -eq 60) { throw 'SQL Server did not become ready within 5 minutes.' }
    Start-Sleep -Seconds 5
}

& $sqlcmd @base '-Q' 'IF DB_ID(N''DiaCompanion'') IS NULL CREATE DATABASE [DiaCompanion];'
if ($LASTEXITCODE -ne 0) { throw 'Could not create the DiaCompanion database.' }

& $sqlcmd @base '-d' 'DiaCompanion' '-i' $SchemaPath
if ($LASTEXITCODE -ne 0) { throw 'Could not apply the database schema.' }

$seedOutput = Join-Path $env:TEMP 'diacompanion-lite-seed.sql'
$credentialOutput = Join-Path (Split-Path $AiServiceDir -Parent) 'initial_credentials.txt'
$seedArgs = @($SeedScript, 'init', '--email', $AdminEmail, '--full-name', $AdminName, '--license', $License, '--ai-service-dir', $AiServiceDir, '--out', $seedOutput, '--password-out', $credentialOutput)
if ($ResearchEmail) { $seedArgs += @('--research-email', $ResearchEmail) }
& $PythonPath @seedArgs
if ($LASTEXITCODE -ne 0) { throw 'Could not generate the initial Lite seed.' }
& $sqlcmd @base '-d' 'DiaCompanion' '-i' $seedOutput
if ($LASTEXITCODE -ne 0) { throw 'Could not apply the Lite seed.' }
Remove-Item $seedOutput -Force -ErrorAction SilentlyContinue
Write-Host "Database initialized. Initial credentials: $credentialOutput"
