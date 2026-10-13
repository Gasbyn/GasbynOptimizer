param(
    [Parameter(Mandatory = $true)][string]$InputPng,
    [Parameter(Mandatory = $true)][string]$OutputIco
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$image = $null
$bitmap = $null
$graphics = $null
$stream = $null
try {
    $image = [System.Drawing.Image]::FromFile($InputPng)
    $bitmap = New-Object System.Drawing.Bitmap 256, 256
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.DrawImage($image, 0, 0, 256, 256)

    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngBytes = $stream.ToArray()

    # ICO directory header followed by a 256x256 PNG payload (supported by modern Windows).
    $header = New-Object byte[] 22
    $header[2] = 1
    $header[4] = 1
    $header[6] = 0
    $header[7] = 0
    $header[8] = 1
    $header[10] = 32
    [BitConverter]::GetBytes([int]$pngBytes.Length).CopyTo($header, 14)
    [BitConverter]::GetBytes([int]22).CopyTo($header, 18)

    $icoBytes = New-Object byte[] ($header.Length + $pngBytes.Length)
    [Array]::Copy($header, 0, $icoBytes, 0, $header.Length)
    [Array]::Copy($pngBytes, 0, $icoBytes, $header.Length, $pngBytes.Length)
    [System.IO.File]::WriteAllBytes($OutputIco, $icoBytes)
}
finally {
    if ($stream) { $stream.Dispose() }
    if ($graphics) { $graphics.Dispose() }
    if ($bitmap) { $bitmap.Dispose() }
    if ($image) { $image.Dispose() }
}
