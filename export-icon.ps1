param(
    [string]$Source = 'img\SoyTemperature-icon.png',
    [string]$Destination = 'img\SoyTemperature.ico'
)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
Add-Type -AssemblyName System.Drawing
$iconSource = [System.Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot $Source))
$iconSizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$frames = [System.Collections.Generic.List[byte[]]]::new()
try {
    foreach ($iconSize in $iconSizes) {
        $bitmap = [System.Drawing.Bitmap]::new($iconSize, $iconSize, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $stream = [System.IO.MemoryStream]::new()
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.DrawImage($iconSource, [System.Drawing.Rectangle]::new(0, 0, $iconSize, $iconSize))
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            $frames.Add($stream.ToArray())
        }
        finally { $graphics.Dispose(); $bitmap.Dispose(); $stream.Dispose() }
    }
    $file = [System.IO.File]::Create((Join-Path $PSScriptRoot $Destination))
    $writer = [System.IO.BinaryWriter]::new($file)
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$iconSizes.Count)
        $offset = 6 + 16 * $iconSizes.Count
        for ($index = 0; $index -lt $iconSizes.Count; $index++) {
            $dimension = if ($iconSizes[$index] -eq 256) { 0 } else { $iconSizes[$index] }
            $writer.Write([byte]$dimension)
            $writer.Write([byte]$dimension)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$frames[$index].Length)
            $writer.Write([uint32]$offset)
            $offset += $frames[$index].Length
        }
        foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
    }
    finally { $writer.Dispose(); $file.Dispose() }
}
finally { $iconSource.Dispose() }
Write-Host "Icon exported: $Destination ($($iconSizes -join ', ') px)"
