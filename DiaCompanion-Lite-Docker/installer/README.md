# DiaCompanion Lite Hospital Installer

This directory is the Windows deployment target for DiaCompanion Lite. Docker Compose remains supported and is not modified by this package.

## Build prerequisites

Install these tools on the build machine only:

- .NET 8 SDK
- Node.js 20 LTS and npm
- Python 3.11 x64
- Inno Setup 6 (`ISCC.exe`)
- SQL Server Express media, copied to `installer/payload/sqlserver/`

The AI payload is intentionally supplied by the build machine. It must contain a portable Python runtime with all packages already installed. The hospital machine never runs pip.

## Build

From the repository root:

```powershell
.\installer\scripts\build-installer.ps1 `
  -Version 1.0.0 `
  -AiRuntime C:\Build\DiaCompanion\ai-runtime `
  -SqlServerInstaller C:\Build\SQLEXPR_x64_ENU.exe
```

The output is written to `release/DiaCompanion-Lite-Hospital-Setup-x64-1.0.0.exe`.

`-AiRuntime` must contain `python.exe`, the AI source files, and installed packages. The model weights are copied from `ai-weights/` and are hash-checked during staging.

## Offline contract

The generated Setup.exe contains the Backend publish, frontend static files, AI runtime, model weights, schema, seed generator, and SQL Server Express media. The installer does not invoke Docker, Node, npm, pip, NuGet, or an Internet URL.

## Runtime layout

- `C:\Program Files\DiaCompanion Lite\backend`
- `C:\Program Files\DiaCompanion Lite\ai`
- `C:\ProgramData\DiaCompanion\storage`
- `C:\ProgramData\DiaCompanion\logs`
- `C:\ProgramData\DiaCompanion\config`

Services:

- `DiaCompanionAI` on `127.0.0.1:8000`
- `DiaCompanionBackend` on the configured web port, default `8080`

The installer keeps the Docker deployment independent. It also keeps the database and patient storage when the application is uninstalled.
