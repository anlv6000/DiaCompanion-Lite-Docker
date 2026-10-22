; DR AI-Diagnosis (trước 1.3.1: DiaCompanion Lite) - bộ cài Windows offline cho bệnh viện.
; Build: installer\scripts\build-installer.ps1 (không chạy ISCC trực tiếp).
; Lưu tệp này dạng UTF-8 có BOM để giữ tiếng Việt.
; AppId GIỮ NGUYÊN như bản DiaCompanion Lite: Windows coi đây là cùng một sản phẩm đã đổi tên, mục gỡ cài đặt cũ
; được thay bằng mục mới. Thư mục cài đặt mới (UsePreviousAppDir=no); dữ liệu cũ do migrate-legacy.ps1 chuyển.

#define AppName "DR AI-Diagnosis"
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
AppVerName={#AppName} {#AppVersion}
AppPublisher=DR AI-Diagnosis
DefaultDirName={autopf}\DR AI-Diagnosis
DefaultGroupName=DR AI-Diagnosis
UsePreviousAppDir=no
UsePreviousGroup=no
DisableProgramGroupPage=yes
DisableDirPage=auto
OutputDir={#OutputDir}
OutputBaseFilename=DR-AI-Diagnosis-Setup-x64-{#AppVersion}
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
MinVersion=10.0
PrivilegesRequired=admin
Compression=lzma2/max
SolidCompression=yes
CloseApplications=no
UninstallDisplayName={#AppName}
WizardStyle=modern

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#StageDir}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Dirs]
Name: "{commonappdata}\DRAIDiagnosis\logs"

; Lối tắt của tên cũ (DiaCompanion Lite) trên Desktop và Start Menu.
[InstallDelete]
Type: filesandordirs; Name: "{autoprograms}\DiaCompanion Lite"
Type: files; Name: "{autodesktop}\DiaCompanion Lite.url"
Type: files; Name: "{autodesktop}\DiaCompanion Lite.lnk"

[Icons]
Name: "{autodesktop}\DR AI-Diagnosis"; Filename: "http://localhost:{code:GetPort}"
Name: "{group}\Mở DR AI-Diagnosis"; Filename: "http://localhost:{code:GetPort}"
Name: "{group}\Kiểm tra hệ thống"; Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\tools.ps1"" -Action health"
Name: "{group}\Sao lưu dữ liệu"; Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\tools.ps1"" -Action backup"
Name: "{group}\Thêm người chấm độc lập"; Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\tools.ps1"" -Action add-grader"
Name: "{group}\Thêm tài khoản nghiên cứu"; Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\tools.ps1"" -Action add-research"
Name: "{group}\Cấp lại mật khẩu"; Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\tools.ps1"" -Action reset-password"
Name: "{group}\Khởi động lại dịch vụ"; Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\tools.ps1"" -Action restart"
Name: "{group}\Mở thư mục log"; Filename: "{commonappdata}\DRAIDiagnosis\logs"

[Run]
Filename: "http://localhost:{code:GetPort}"; Description: "Mở DR AI-Diagnosis"; Flags: postinstall shellexec skipifsilent nowait; Check: RuntimeOk

[UninstallRun]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\uninstall-services.ps1"" -InstallRoot ""{app}"""; Flags: runhidden waituntilterminated; RunOnceId: "RemoveServices"

[UninstallDelete]
Type: files; Name: "{app}\wizard-input.txt"
Type: filesandordirs; Name: "{app}\services"
Type: files; Name: "{app}\backend\appsettings.Production.json"

[Code]
var
  CfgPage: TInputQueryWizardPage;
  ResultPage: TOutputMsgMemoWizardPage;
  IsUpgrade, RuntimeSucceeded: Boolean;

function DataDir(): String;
begin
  Result := ExpandConstant('{commonappdata}\DRAIDiagnosis');
end;

{ Thư mục dữ liệu của bản cũ DiaCompanion Lite (<= 1.3.0). }
function LegacyDataDir(): String;
begin
  Result := ExpandConstant('{commonappdata}\DiaCompanion');
end;

function Installed(Dir: String): Boolean;
begin
  Result := FileExists(Dir + '\config\secrets.json') and FileExists(Dir + '\config\db-initialized');
end;

function InitializeSetup(): Boolean;
begin
  { Đã cài xong trước đây (tên mới hoặc DiaCompanion Lite cũ): cài đè, giữ DB/ảnh/tài khoản, không hỏi lại cấu hình. }
  IsUpgrade := Installed(DataDir()) or Installed(LegacyDataDir());
  Result := True;
end;

procedure InitializeWizard();
begin
  CfgPage := CreateInputQueryPage(wpSelectDir,
    'Cấu hình ban đầu', 'Tài khoản đăng nhập và cổng web',
    'Mật khẩu tạm được tạo tự động và hiện ở cuối quá trình cài. Lần đăng nhập đầu bắt buộc đổi mật khẩu.');
  CfgPage.Add('Email tài khoản bác sĩ:', False);
  CfgPage.Add('Họ tên bác sĩ:', False);
  CfgPage.Add('Email tài khoản nghiên cứu (để trống nếu không cần):', False);
  CfgPage.Add('Cổng web:', False);
  CfgPage.Values[0] := 'bacsi@benhvien.local';
  CfgPage.Values[1] := 'Bác sĩ';
  CfgPage.Values[2] := 'nghiencuu@benhvien.local';
  CfgPage.Values[3] := '8080';

  ResultPage := CreateOutputMsgMemoPage(wpInstalling,
    'Kết quả cài đặt', 'Ghi lại thông tin dưới đây trước khi đóng.', '', '');
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := IsUpgrade and (PageID = CfgPage.ID);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Port: Integer;
begin
  Result := True;
  if CurPageID = CfgPage.ID then begin
    if Pos('@', Trim(CfgPage.Values[0])) < 2 then begin
      MsgBox('Email tài khoản bác sĩ không hợp lệ.', mbError, MB_OK); Result := False; Exit;
    end;
    if Trim(CfgPage.Values[1]) = '' then begin
      MsgBox('Nhập họ tên bác sĩ.', mbError, MB_OK); Result := False; Exit;
    end;
    if (Trim(CfgPage.Values[2]) <> '') and (Pos('@', CfgPage.Values[2]) < 2) then begin
      MsgBox('Email tài khoản nghiên cứu không hợp lệ.', mbError, MB_OK); Result := False; Exit;
    end;
    Port := StrToIntDef(Trim(CfgPage.Values[3]), 0);
    if (Port < 1) or (Port > 65535) then begin
      MsgBox('Cổng web phải là số từ 1 đến 65535.', mbError, MB_OK); Result := False; Exit;
    end;
  end;
end;

function GetPort(Param: String): String;
var
  S: AnsiString;
begin
  Result := '8080';
  if IsUpgrade then begin
    if LoadStringFromFile(DataDir() + '\config\port.txt', S) and (StrToIntDef(Trim(String(S)), 0) > 0) then
      Result := Trim(String(S))
    else if LoadStringFromFile(LegacyDataDir() + '\config\port.txt', S) and (StrToIntDef(Trim(String(S)), 0) > 0) then
      Result := Trim(String(S));
  end else if Assigned(CfgPage) then
    Result := Trim(CfgPage.Values[3]);
end;

function RuntimeOk(): Boolean;
begin
  Result := RuntimeSucceeded;
end;

function Q(S: String): String;
begin
  StringChangeEx(S, '"', '''', True);
  Result := '"' + Trim(S) + '"';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Params, LogPath, WizardFile, ConsoleLog, CmdLine: String;
  Wiz: TArrayOfString;
  Code, I, FirstLine: Integer;
  Lines: TArrayOfString;
  Text: String;
begin
  if CurStep <> ssPostInstall then Exit;
  WizardFile := '';
  LogPath := DataDir() + '\logs\installer.log';
  Params := '-NoProfile -ExecutionPolicy Bypass -File ' + Q(ExpandConstant('{app}\scripts\install-runtime.ps1')) +
    ' -InstallRoot ' + Q(ExpandConstant('{app}')) + ' -WebPort ' + GetPort('');
  if not IsUpgrade then begin
    { Tên tiếng Việt đi qua tệp UTF-8, không qua dòng lệnh. }
    WizardFile := ExpandConstant('{app}\wizard-input.txt');
    SetArrayLength(Wiz, 4);
    Wiz[0] := Trim(CfgPage.Values[0]);
    Wiz[1] := Trim(CfgPage.Values[1]);
    Wiz[2] := Trim(CfgPage.Values[2]);
    Wiz[3] := Trim(CfgPage.Values[3]);
    if SaveStringsToUTF8File(WizardFile, Wiz, False) then
      Params := Params + ' -WizardFile ' + Q(WizardFile)
    else
      Params := Params + ' -AdminEmail ' + Q(Wiz[0]) + ' -AdminName ' + Q(Wiz[1]) + ' -ResearchEmail ' + Q(Wiz[2]);
  end;

  WizardForm.StatusLabel.Caption := 'Đang cài SQL Server, database và dịch vụ (có thể mất 10–25 phút)...';
  WizardForm.ProgressGauge.Style := npbstMarquee;
  { Chạy qua cmd để bắt MỌI đầu ra (kể cả lỗi PowerShell không nạp được script) vào setup-console.log. }
  ConsoleLog := DataDir() + '\logs\setup-console.log';
  ForceDirectories(DataDir() + '\logs');
  DeleteFile(ConsoleLog);
  CmdLine := '/S /C ""' + ExpandConstant('{sys}') + '\WindowsPowerShell\v1.0\powershell.exe" ' + Params +
    ' > "' + ConsoleLog + '" 2>&1"';
  try
    if not Exec(ExpandConstant('{cmd}'), CmdLine, ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, Code) then Code := -1;
  finally
    WizardForm.ProgressGauge.Style := npbstNormal;
  end;
  RuntimeSucceeded := (Code = 0);
  if (WizardFile <> '') and FileExists(WizardFile) then DeleteFile(WizardFile);

  Text := '';
  if LoadStringsFromFile(DataDir() + '\config\install-summary.txt', Lines) then
    for I := 0 to GetArrayLength(Lines) - 1 do Text := Text + Lines[I] + #13#10;
  if Text = '' then begin
    { Script không kịp ghi tóm tắt: hiện 40 dòng cuối đầu ra của PowerShell. }
    Text := 'Không có tệp tóm tắt. Đầu ra cuối của trình cài (' + ConsoleLog + '):' + #13#10 + #13#10;
    if LoadStringsFromFile(ConsoleLog, Lines) then begin
      FirstLine := GetArrayLength(Lines) - 40;
      if FirstLine < 0 then FirstLine := 0;
      for I := FirstLine to GetArrayLength(Lines) - 1 do Text := Text + Lines[I] + #13#10;
    end else
      Text := Text + '(không có đầu ra — PowerShell không chạy được)' + #13#10;
  end;
  if not RuntimeSucceeded then
    Text := Text + #13#10 + '(Mã thoát: ' + IntToStr(Code) + ')';
  ResultPage.RichEditViewer.Text := Text;

  if not RuntimeSucceeded then begin
    MsgBox('Cài đặt chưa hoàn tất. Xem chi tiết ở trang tiếp theo và trong log:' + #13#10 + LogPath, mbError, MB_OK);
    if FileExists(LogPath) then
      ShellExec('open', 'notepad.exe', '"' + LogPath + '"', '', SW_SHOWNORMAL, ewNoWait, Code)
    else
      ShellExec('open', 'notepad.exe', '"' + ConsoleLog + '"', '', SW_SHOWNORMAL, ewNoWait, Code);
  end;
end;
