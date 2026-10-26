; Inno Setup script for ShotTray. Build with release.bat, or:
;   ISCC.exe /DAppVersion=0.1.0 installer\ShotTray.iss
; Installs per user (no admin prompt) into %LOCALAPPDATA%\Programs\ShotTray.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#define AppName "ShotTray"
#define AppExe "ShotTray.exe"
#define AppPublisher "ShabanMughal"
#define AppURL "https://github.com/ShabanMughal/ShotTray"

[Setup]
; Never change AppId: Windows uses it to find the existing install on upgrade.
AppId={{6F1C2B7A-4E8D-4B5A-9C3E-2D7F1A8B5E40}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
VersionInfoVersion={#AppVersion}
VersionInfoDescription={#AppName} Setup
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=lowest
MinVersion=10.0
LicenseFile=..\LICENSE
SetupIconFile=..\assets\ShotTray.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
OutputDir=..\dist
OutputBaseFilename=ShotTray-Setup-v{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Logo artwork, made by tools\make-installer-images.ps1. Setup picks the size closest to the screen scaling.
WizardImageFile=..\assets\installer-side-100.bmp,..\assets\installer-side-150.bmp,..\assets\installer-side-200.bmp
WizardSmallImageFile=..\assets\installer-corner-100.bmp,..\assets\installer-corner-150.bmp,..\assets\installer-corner-200.bmp
DisableWelcomePage=no
; Shown after installing: how to use every feature.
InfoAfterFile=QuickStart.rtf
ChangesEnvironment=yes
; Lets setup and the uninstaller detect a running ShotTray and close it.
AppMutex=ShotTray.SingleInstance
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
WelcomeLabel2=This will install [name/ver] on your computer.%n%nmacOS-style area screenshots for Windows:%n%n•  Ctrl+Shift+4, drag a box, done%n•  Ctrl+Shift+5: capture a screen or window, or record the screen to MP4%n•  Floating thumbnail you can drag into any app or folder%n•  Markup: pen, highlighter, arrows, boxes, ellipses, text%n•  Every capture is copied to the clipboard and saved automatically%n•  "shot" command prints the latest screenshot path%n•  Old screenshots are cleaned up after 7 days%n%nA quick guide on how to use everything is shown at the end.
WizardInfoAfter=How to use ShotTray
InfoAfterLabel=A quick tour of everything ShotTray can do.
InfoAfterClickLabel=When you're done reading, click Next.
FinishedLabel=Setup has finished installing [name].%n%nPress Ctrl+Shift+4 any time to take a screenshot. ShotTray waits in the system tray, next to the clock.

[Tasks]
Name: "startup"; Description: "Start ShotTray when I sign in to Windows"; GroupDescription: "Options:"
Name: "addtopath"; Description: "Add the ""shot"" command to PATH"; GroupDescription: "Options:"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\bin\ShotTray.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\bin\shot.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{#AppName} Guide"; Filename: "{#AppURL}#usage"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Same value the tray menu's "Start with Windows" toggle reads and writes.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ShotTray"; ValueData: """{app}\{#AppExe}"""; Tasks: startup
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "ShotTray"; Flags: dontcreatekey uninsdeletevalue
; Capture toolbar choices (Settings.cs).
Root: HKCU; Subkey: "Software\ShotTray"; Flags: dontcreatekey uninsdeletekey

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[Code]
const
  EnvKey = 'Environment';

function PathContains(Paths, Dir: string): Boolean;
begin
  Result := Pos(';' + Uppercase(Dir) + ';', ';' + Uppercase(Paths) + ';') > 0;
end;

procedure AddToPath(Dir: string);
var
  Paths: string;
begin
  if not RegQueryStringValue(HKCU, EnvKey, 'Path', Paths) then
    Paths := '';
  if PathContains(Paths, Dir) then
    exit;
  if (Paths <> '') and (Copy(Paths, Length(Paths), 1) <> ';') then
    Paths := Paths + ';';
  RegWriteExpandStringValue(HKCU, EnvKey, 'Path', Paths + Dir);
end;

procedure RemoveFromPath(Dir: string);
var
  Paths: string;
  P: Integer;
begin
  if not RegQueryStringValue(HKCU, EnvKey, 'Path', Paths) then
    exit;
  Paths := ';' + Paths + ';';
  P := Pos(';' + Uppercase(Dir) + ';', Uppercase(Paths));
  if P = 0 then
    exit;
  Delete(Paths, P, Length(Dir) + 1);
  Paths := Copy(Paths, 2, Length(Paths) - 2);
  RegWriteExpandStringValue(HKCU, EnvKey, 'Path', Paths);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if WizardIsTaskSelected('addtopath') then
      AddToPath(ExpandConstant('{app}'))
    else
      RemoveFromPath(ExpandConstant('{app}'));
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: string;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    RemoveFromPath(ExpandConstant('{app}'));
    DataDir := ExpandConstant('{localappdata}\ShotTray');
    if DirExists(DataDir) and not UninstallSilent and
       (MsgBox('Also delete your saved screenshots in ' + DataDir + '?',
               mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES) then
      DelTree(DataDir, True, True, True);
  end;
end;
