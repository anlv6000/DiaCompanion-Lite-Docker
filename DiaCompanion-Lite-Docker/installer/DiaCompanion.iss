#define AppName "DiaCompanion Lite Hospital"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef StageDir
  #define StageDir "build\stage"
#endif
#ifndef OutputDir
  #define OutputDir "..\release"
#endif

[Setup]
AppId={{B9E4B8E7-8D1C-4A24-AB03-DA6B3A0D1F35}
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={autopf}\DiaCompanion Lite
DefaultGroupName=DiaCompanion Lite
OutputDir={#OutputDir}
OutputBaseFilename=DiaCompanion-Lite-Hospital-Setup-x64-{#AppVersion}
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
PrivilegesRequired=admin
Compression=lzma2/max
SolidCompression=yes
DisableProgramGroupPage=yes
Uninstallable=yes

[Files]
Source: "{#StageDir}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Dirs]
Name: "{commonappdata}\DiaCompanion\storage\fundus"
Name: "{commonappdata}\DiaCompanion\storage\ai_masks"
Name: "{commonappdata}\DiaCompanion\logs"
Name: "{commonappdata}\DiaCompanion\config"

[Icons]
Name: "{autodesktop}\DiaCompanion Lite"; Filename: "http://localhost:8080"; WorkingDir: "{app}"
Name: "{group}\DiaCompanion Lite"; Filename: "http://localhost:8080"; WorkingDir: "{app}"

[Run]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File \"{app}\scripts\install-runtime.ps1\" -InstallRoot \"{app}\""; StatusMsg: "Initializing DiaCompanion Lite services..."; Flags: runhidden waituntilterminated
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -Command \"Start-Process 'http://localhost:8080'\""; Description: "Open DiaCompanion Lite"; Flags: postinstall shellexec skipifsilent

[UninstallRun]
Filename: "sc.exe"; Parameters: "stop DiaCompanionBackend"; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "stop DiaCompanionAI"; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "delete DiaCompanionBackend"; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "delete DiaCompanionAI"; Flags: runhidden waituntilterminated