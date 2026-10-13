# Windows PowerShell 5.1 Script to generate DeeClipper application icons using System.Drawing
[CmdletBinding()]
param(
    [string]$OutputDir = "$PSScriptRoot\..\src\DeeClipper.App\Assets"
)

Add-Type -AssemblyName System.Drawing

if (-not (Test-Path -Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

function Create-RoundedRectanglePath {
    param(
        [System.Drawing.RectangleF]$rect,
        [float]$radius
    )
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diameter = $radius * 2

    $path.AddArc($rect.X, $rect.Y, $diameter, $diameter, 180, 90)
    $path.AddArc($rect.Right - $diameter, $rect.Y, $diameter, $diameter, 270, 90)
    $path.AddArc($rect.Right - $diameter, $rect.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function Render-ClipboardIconFrame {
    param(
        [int]$size,
        [bool]$isPaused
    )

    $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $scale = $size / 256.0

    # Colors
    if ($isPaused) {
        $bgBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 113, 113, 122)) # Zinc-500
        $accentBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 82, 82, 91)) # Zinc-600
    } else {
        $bgBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 13, 148, 136)) # Teal-600
        $accentBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 15, 118, 110)) # Teal-700
    }

    # Rounded background square
    $margin = 12.0 * $scale
    $bgRect = New-Object System.Drawing.RectangleF($margin, $margin, $size - 2 * $margin, $size - 2 * $margin)
    $bgRadius = 44.0 * $scale
    $bgPath = Create-RoundedRectanglePath $bgRect $bgRadius
    $g.FillPath($bgBrush, $bgPath)

    # Clipboard Board (white/off-white)
    $boardBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 248, 250, 252))
    $boardRect = New-Object System.Drawing.RectangleF(56.0 * $scale, 60.0 * $scale, 144.0 * $scale, 148.0 * $scale)
    $boardRadius = 14.0 * $scale
    $boardPath = Create-RoundedRectanglePath $boardRect $boardRadius
    $g.FillPath($boardBrush, $boardPath)

    # Clip at top
    $clipRect = New-Object System.Drawing.RectangleF(92.0 * $scale, 44.0 * $scale, 72.0 * $scale, 32.0 * $scale)
    $clipRadius = 8.0 * $scale
    $clipPath = Create-RoundedRectanglePath $clipRect $clipRadius
    $g.FillPath($accentBrush, $clipPath)

    # Inner Clip cutout / handle
    $cutoutBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 255, 255))
    $cutoutRect = New-Object System.Drawing.RectangleF(108.0 * $scale, 52.0 * $scale, 40.0 * $scale, 14.0 * $scale)
    $cutoutRadius = 4.0 * $scale
    $cutoutPath = Create-RoundedRectanglePath $cutoutRect $cutoutRadius
    $g.FillPath($cutoutBrush, $cutoutPath)

    # Text / lines on the clipboard
    $lineBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(200, 148, 163, 184))
    $line1 = New-Object System.Drawing.RectangleF(76.0 * $scale, 104.0 * $scale, 104.0 * $scale, 12.0 * $scale)
    $line2 = New-Object System.Drawing.RectangleF(76.0 * $scale, 128.0 * $scale, 88.0 * $scale, 12.0 * $scale)
    $line3 = New-Object System.Drawing.RectangleF(76.0 * $scale, 152.0 * $scale, 64.0 * $scale, 12.0 * $scale)
    $lineRadius = 4.0 * $scale

    $g.FillPath($lineBrush, (Create-RoundedRectanglePath $line1 $lineRadius))
    $g.FillPath($lineBrush, (Create-RoundedRectanglePath $line2 $lineRadius))
    $g.FillPath($lineBrush, (Create-RoundedRectanglePath $line3 $lineRadius))

    if ($isPaused) {
        # Draw small pause symbol on bottom right
        $pauseCircleBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 239, 68, 68)) # Red
        $pauseBarBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
        $pcRect = New-Object System.Drawing.RectangleF(160.0 * $scale, 160.0 * $scale, 72.0 * $scale, 72.0 * $scale)
        $g.FillEllipse($pauseCircleBrush, $pcRect)
        $pBar1 = New-Object System.Drawing.RectangleF(178.0 * $scale, 176.0 * $scale, 12.0 * $scale, 40.0 * $scale)
        $pBar2 = New-Object System.Drawing.RectangleF(202.0 * $scale, 176.0 * $scale, 12.0 * $scale, 40.0 * $scale)
        $g.FillRectangle($pauseBarBrush, $pBar1)
        $g.FillRectangle($pauseBarBrush, $pBar2)
        $pauseCircleBrush.Dispose()
        $pauseBarBrush.Dispose()
    }

    $g.Dispose()
    $bgBrush.Dispose()
    $accentBrush.Dispose()
    $boardBrush.Dispose()
    $cutoutBrush.Dispose()
    $lineBrush.Dispose()

    return $bitmap
}

function Save-MultiResolutionIco {
    param(
        [string]$outputPath,
        [int[]]$sizes,
        [bool]$isPaused
    )

    $pngStreams = @()
    foreach ($size in $sizes) {
        $bmp = Render-ClipboardIconFrame -size $size -isPaused $isPaused
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        $pngStreams += $ms
    }

    $fileStream = [System.IO.File]::Create($outputPath)
    $writer = New-Object System.IO.BinaryWriter($fileStream)

    try {
        # ICONDIR header: Reserved (0), Type (1 for icon), Count (N)
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$sizes.Length)

        $offset = 6 + ($sizes.Length * 16)

        # ICONDIRENTRY entries
        for ($i = 0; $i -lt $sizes.Length; $i++) {
            $s = $sizes[$i]
            $w = if ($s -ge 256) { 0 } else { [byte]$s }
            $h = if ($s -ge 256) { 0 } else { [byte]$s }
            $bytes = $pngStreams[$i].ToArray()

            $writer.Write([byte]$w)            # Width
            $writer.Write([byte]$h)            # Height
            $writer.Write([byte]0)             # ColorCount
            $writer.Write([byte]0)             # Reserved
            $writer.Write([uint16]1)           # Planes
            $writer.Write([uint16]32)          # BitCount
            $writer.Write([uint32]$bytes.Length) # BytesInRes
            $writer.Write([uint32]$offset)     # ImageOffset

            $offset += $bytes.Length
        }

        # Write PNG byte arrays
        for ($i = 0; $i -lt $sizes.Length; $i++) {
            $bytes = $pngStreams[$i].ToArray()
            $writer.Write($bytes)
            $pngStreams[$i].Dispose()
        }
    }
    finally {
        $writer.Dispose()
        $fileStream.Dispose()
    }

    Write-Host "Generated icon: $outputPath"
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$appIcoPath = Join-Path $OutputDir "app.ico"
$pausedIcoPath = Join-Path $OutputDir "paused.ico"

Save-MultiResolutionIco -outputPath $appIcoPath -sizes $sizes -isPaused $false
Save-MultiResolutionIco -outputPath $pausedIcoPath -sizes $sizes -isPaused $true

Write-Host "All DeeClipper icons successfully created."
