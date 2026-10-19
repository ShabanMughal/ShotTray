# Renders the installer wizard images from assets/icon-1024.png:
#   assets/installer-side-*.bmp   left panel on the Welcome and Finish pages
#   assets/installer-corner-*.bmp top-right logo on the other pages
# Each comes at 100%, 150% and 200% scaling. Run after changing the icon.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot
$icon = [Drawing.Image]::FromFile((Join-Path $root 'assets\icon-1024.png'))

function New-Canvas($w, $h) {
    $bmp = New-Object Drawing.Bitmap $w, $h, ([Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    return $bmp, $g
}

foreach ($scale in 100, 150, 200) {
    $f = $scale / 100

    # Side panel: icon-blue gradient, logo, app name and tagline.
    $w = [int](164 * $f); $h = [int](314 * $f)
    $bmp, $g = New-Canvas $w $h
    $rect = New-Object Drawing.Rectangle 0, 0, $w, $h
    $brush = New-Object Drawing.Drawing2D.LinearGradientBrush $rect, ([Drawing.Color]::FromArgb(91, 149, 245)), ([Drawing.Color]::FromArgb(31, 79, 216)), 60
    $g.FillRectangle($brush, $rect)
    $size = [int](112 * $f)
    $g.DrawImage($icon, [int](($w - $size) / 2), [int](70 * $f), $size, $size)
    $center = New-Object Drawing.StringFormat
    $center.Alignment = 'Center'
    $title = New-Object Drawing.Font 'Segoe UI Semibold', (20 * $f), ([Drawing.FontStyle]::Regular), ([Drawing.GraphicsUnit]::Pixel)
    $sub = New-Object Drawing.Font 'Segoe UI', (11 * $f), ([Drawing.FontStyle]::Regular), ([Drawing.GraphicsUnit]::Pixel)
    $g.DrawString('ShotTray', $title, [Drawing.Brushes]::White, (New-Object Drawing.RectangleF 0, (196 * $f), $w, (30 * $f)), $center)
    $soft = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(220, 232, 255))
    $g.DrawString("macOS-style screenshots`nfor Windows", $sub, $soft, (New-Object Drawing.RectangleF 0, (226 * $f), $w, (40 * $f)), $center)
    $g.Dispose()
    $bmp.Save((Join-Path $root "assets\installer-side-$scale.bmp"), [Drawing.Imaging.ImageFormat]::Bmp)
    $bmp.Dispose()

    # Corner logo on white, the wizard header's background.
    $s = [int](55 * $f)
    $bmp, $g = New-Canvas $s $s
    $g.Clear([Drawing.Color]::White)
    $g.DrawImage($icon, 0, 0, $s, $s)
    $g.Dispose()
    $bmp.Save((Join-Path $root "assets\installer-corner-$scale.bmp"), [Drawing.Imaging.ImageFormat]::Bmp)
    $bmp.Dispose()
}
$icon.Dispose()
Write-Host 'Wrote assets\installer-side-*.bmp and assets\installer-corner-*.bmp'
