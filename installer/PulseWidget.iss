; Built with Inno Setup 7.1.1. Keep AppId stable for future upgrades.
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef PayloadDir
  #error PayloadDir must point to the self-contained build output
#endif
#ifdef TestInstall
  #define ProductId "PulseWidget.InstallerTest"
  #define ProductName "Pulse Widget Installer Test"
  #define RunValueName "PulseWidget.InstallerTest"
  #define WidgetMutex "Local\PulseWidget.InstallerTest"
#else
  #define ProductId "PulseWidget.5C447C04-1587-45C0-8D87-509812EA7DA1"
  #define ProductName "Pulse Widget"
  #define RunValueName "PulseWidget"
  #define WidgetMutex "Local\PulseWidget.UI"
#endif

[Setup]
AppId={#ProductId}
AppName={#ProductName}
AppVersion={#AppVersion}
AppPublisher=Pulse Widget
DefaultDirName={localappdata}\Programs\{#ProductName}
DefaultGroupName={#ProductName}
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
SetupArchitecture=x64
WizardStyle=modern
DisableProgramGroupPage=yes
DisableDirPage=no
DisableWelcomePage=no
AllowNoIcons=yes
Compression=lzma2/normal
SolidCompression=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=PulseWidget-{#AppVersion}-Setup-x64
SetupIconFile=..\src\Assets\PulseWidget.ico
UninstallDisplayIcon={app}\PulseWidget.exe
UninstallDisplayName={#ProductName}
VersionInfoProductName={#ProductName}
VersionInfoVersion={#AppVersion}
AppMutex={#WidgetMutex}
SetupMutex=Local\{#ProductId}.Setup
CloseApplications=no
RestartApplications=no
InfoAfterFile=InstallNotes.txt

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
korean.StartupTask=Windows 로그인 시 자동 실행
english.StartupTask=Start automatically when I sign in to Windows
korean.DesktopTask=바탕 화면 바로가기 만들기
english.DesktopTask=Create a desktop shortcut
korean.RunWidget=Pulse Widget 실행
english.RunWidget=Launch Pulse Widget
korean.UninstallWidget=Pulse Widget 제거
english.UninstallWidget=Uninstall Pulse Widget

[Messages]
korean.SetupAppRunningError=Pulse Widget이 실행 중입니다.%n%n위젯 또는 트레이 아이콘을 우클릭하여 '종료'한 뒤 확인을 눌러 설치를 계속하세요. 위치와 테마 설정은 유지됩니다.
korean.UninstallAppRunningError=Pulse Widget이 실행 중입니다.%n%n위젯 또는 트레이 아이콘을 우클릭하여 '종료'한 뒤 확인을 눌러 제거를 계속하세요.

[Tasks]
Name: "startup"; Description: "{cm:StartupTask}"
Name: "desktopicon"; Description: "{cm:DesktopTask}"; Flags: unchecked

[Files]
; Only application payload, never user settings, tokens, logs or compiler files.
Source: "{#PayloadDir}\PulseWidget.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\bridge\PulseBridge.exe"; DestDir: "{app}\bridge"; Flags: ignoreversion
Source: "{#PayloadDir}\scripts\Install-Claude.ps1"; DestDir: "{app}\scripts"; Flags: ignoreversion
Source: "{#PayloadDir}\scripts\Uninstall-Claude.ps1"; DestDir: "{app}\scripts"; Flags: ignoreversion
Source: "{#PayloadDir}\scripts\Claude-Integration.ps1"; DestDir: "{app}\scripts"; Flags: ignoreversion

[Icons]
Name: "{group}\{#ProductName}"; Filename: "{app}\PulseWidget.exe"; WorkingDir: "{app}"
Name: "{group}\{cm:UninstallWidget}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#ProductName}"; Filename: "{app}\PulseWidget.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[InstallDelete]
Type: files; Name: "{autodesktop}\{#ProductName}.lnk"; Tasks: not desktopicon

[Run]
Filename: "{app}\PulseWidget.exe"; WorkingDir: "{app}"; Description: "{cm:RunWidget}"; Flags: nowait postinstall skipifsilent

[Code]
const
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  RunName = '{#RunValueName}';

var
  HadPreviousInstall, PriorStartup, StartupChoiceInitialized: Boolean;

function ExplicitTasks: Boolean;
var
  I: Integer;
  Arg: String;
begin
  Result := False;
  for I := 1 to ParamCount do begin
    Arg := Lowercase(ParamStr(I));
    if (Pos('/tasks=', Arg) = 1) or (Pos('/mergetasks=', Arg) = 1) then Result := True;
  end;
end;

procedure InitializeWizard;
begin
  HadPreviousInstall := GetPreviousData('Installed', '') = '1';
  PriorStartup := RegValueExists(HKCU64, RunKey, RunName);
end;

procedure RestoreStartupChoice;
begin
  if HadPreviousInstall and not ExplicitTasks then begin
    if PriorStartup then WizardSelectTasks('startup')
    else WizardSelectTasks('!startup');
  end;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  { Task restoration happens after InitializeWizard. Apply the live preference
    once on first display, then preserve any choices made in this wizard. }
  if (CurPageID = wpSelectTasks) and not StartupChoiceInitialized then begin
    RestoreStartupChoice;
    StartupChoiceInitialized := True;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  if WizardSilent then RestoreStartupChoice;
  Result := '';
end;

procedure RegisterPreviousData(PreviousDataKey: Integer);
begin
  SetPreviousData(PreviousDataKey, 'Installed', '1');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then begin
    if WizardIsTaskSelected('startup') then begin
      if not RegWriteStringValue(HKCU64, RunKey, RunName, '"' + ExpandConstant('{app}\PulseWidget.exe') + '"') then
        RaiseException('Could not save the Windows startup setting.');
    end else RegDeleteValue(HKCU64, RunKey, RunName);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Command: String;
begin
  { A portable copy may have taken over startup since installation. Preserve it. }
  if CurUninstallStep = usPostUninstall then
    if RegQueryStringValue(HKCU64, RunKey, RunName, Command) then begin
      if CompareText(Command, '"' + ExpandConstant('{app}\PulseWidget.exe') + '"') = 0 then begin
        RegDeleteValue(HKCU64, RunKey, RunName);
        Log('Removed startup entry owned by this installation.');
      end;
    end else Log('No startup entry to remove.');
end;
