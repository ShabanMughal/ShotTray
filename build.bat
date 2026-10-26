@echo off
rem Builds bin\ShotTray.exe (tray app) and bin\shot.exe (CLI) with the C# compiler that ships with Windows.
setlocal
cd /d "%~dp0"

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo C# compiler not found. Install .NET Framework 4.x.
  exit /b 1
)

if not exist bin mkdir bin

"%CSC%" /nologo /target:winexe /optimize+ /out:bin\ShotTray.exe ^
  /win32icon:assets\ShotTray.ico /resource:assets\ShotTray.ico,ShotTray.ico ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  src\AssemblyInfo.cs src\Program.cs src\TrayApp.cs src\SelectionOverlay.cs src\ThumbnailForm.cs src\DragGhost.cs ^
  src\EditorForm.cs src\Shapes.cs src\Clip.cs src\Native.cs src\Store.cs ^
  src\CaptureBar.cs src\RecordingUI.cs src\Recorder.cs src\Settings.cs
if errorlevel 1 exit /b 1

"%CSC%" /nologo /target:exe /optimize+ /out:bin\shot.exe ^
  /win32icon:assets\ShotTray.ico ^
  /r:System.dll /r:System.Core.dll ^
  src\AssemblyInfo.cs src\Cli.cs src\Store.cs
if errorlevel 1 exit /b 1

echo Built bin\ShotTray.exe and bin\shot.exe
