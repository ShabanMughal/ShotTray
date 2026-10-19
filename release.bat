@echo off
rem Builds and packages a release: release.bat 0.1.0  ->  dist\ShotTray-v0.1.0.zip and dist\ShotTray-Setup-v0.1.0.exe
setlocal
cd /d "%~dp0"

if "%~1"=="" (
  echo Usage: release.bat VERSION   e.g. release.bat 0.1.0
  exit /b 2
)
set VERSION=%~1
set STAGE=dist\ShotTray-v%VERSION%

findstr /c:"AssemblyInformationalVersion(\"%VERSION%\")" src\AssemblyInfo.cs >nul
if errorlevel 1 (
  echo src\AssemblyInfo.cs does not say version %VERSION%. Update it first.
  exit /b 1
)

call "%~dp0build.bat" || exit /b 1

if exist "%STAGE%" rmdir /s /q "%STAGE%"
if exist "%STAGE%.zip" del "%STAGE%.zip"
mkdir "%STAGE%"
copy /y bin\ShotTray.exe "%STAGE%" >nul
copy /y bin\shot.exe "%STAGE%" >nul
copy /y README.md "%STAGE%" >nul
copy /y LICENSE "%STAGE%" >nul

powershell -NoProfile -Command "Compress-Archive -Path '%STAGE%\*' -DestinationPath '%STAGE%.zip' -Force" || exit /b 1
rmdir /s /q "%STAGE%"
echo Created %STAGE%.zip

rem Installer: needs Inno Setup 6 (winget install JRSoftware.InnoSetup).
set ISCC=
for %%P in ("%LOCALAPPDATA%\Programs\Inno Setup 6" "%ProgramFiles(x86)%\Inno Setup 6" "%ProgramFiles%\Inno Setup 6") do (
  if not defined ISCC if exist "%%~P\ISCC.exe" set "ISCC=%%~P\ISCC.exe"
)
if not defined ISCC (
  echo Inno Setup 6 not found, skipping the installer. Install it with: winget install JRSoftware.InnoSetup
  exit /b 0
)
"%ISCC%" /Q /DAppVersion=%VERSION% installer\ShotTray.iss || exit /b 1
echo Created dist\ShotTray-Setup-v%VERSION%.exe
