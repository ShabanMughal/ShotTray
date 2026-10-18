# Regenerates assets\icon-1024.png and assets\ShotTray.ico from assets\icon.svg.
# Uses Microsoft Edge (headless) to render the SVG and tools\MakeIcon.cs to build the .ico.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root 'assets'
$work = Join-Path ([IO.Path]::GetTempPath()) 'shottray-icon'
New-Item -ItemType Directory -Force $work | Out-Null

$edge = @("${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
          "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe") |
        Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $edge) { throw 'Microsoft Edge not found.' }

$svgUrl = 'file:///' + ((Join-Path $assets 'icon.svg') -replace '\\', '/')
$html = Join-Path $work 'icon.html'
[IO.File]::WriteAllText($html, "<html><body style='margin:0;background:transparent'><img src='$svgUrl' width='1024' height='1024' style='display:block'></body></html>")

$png = Join-Path $assets 'icon-1024.png'
# Edge logs harmless noise to stderr; don't let PowerShell treat that as a failure.
$ErrorActionPreference = 'Continue'
& $edge --headless=new --disable-gpu --hide-scrollbars --allow-file-access-from-files `
    --default-background-color=00000000 --window-size=1024,1024 `
    "--screenshot=$png" ('file:///' + ($html -replace '\\', '/')) 2>$null | Out-Null
$ErrorActionPreference = 'Stop'
for ($i = 0; $i -lt 20 -and -not (Test-Path $png); $i++) { Start-Sleep -Milliseconds 250 }
if (-not (Test-Path $png)) { throw 'Edge did not produce the PNG.' }

$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$tool = Join-Path $work 'MakeIcon.exe'
& $csc /nologo "/out:$tool" /r:System.Drawing.dll (Join-Path $PSScriptRoot 'MakeIcon.cs')
if ($LASTEXITCODE -ne 0) { throw 'MakeIcon build failed.' }
& $tool $png (Join-Path $assets 'ShotTray.ico')
