# Render the existing Pulse tray mark into a multi-resolution Windows icon.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$destination = Join-Path $root 'src\Assets\PulseWidget.ico'
New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
$images = @()
foreach ($size in @(16, 24, 32, 48, 64, 128, 256)) {
    $bitmap = New-Object Drawing.Bitmap($size, $size)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $pen = New-Object Drawing.Pen([Drawing.ColorTranslator]::FromHtml('#9DE9D3'), ([single]($size * 0.075)))
    try {
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.Clear([Drawing.ColorTranslator]::FromHtml('#181D27'))
        $pen.StartCap = $pen.EndCap = [Drawing.Drawing2D.LineCap]::Round
        $pen.LineJoin = [Drawing.Drawing2D.LineJoin]::Round
        $points = [Drawing.PointF[]]@(
            (New-Object Drawing.PointF(($size * .13), ($size * .56))),
            (New-Object Drawing.PointF(($size * .29), ($size * .56))),
            (New-Object Drawing.PointF(($size * .41), ($size * .24))),
            (New-Object Drawing.PointF(($size * .59), ($size * .78))),
            (New-Object Drawing.PointF(($size * .72), ($size * .44))),
            (New-Object Drawing.PointF(($size * .87), ($size * .44)))
        )
        $graphics.DrawLines($pen, $points)
        $stream = New-Object IO.MemoryStream
        try { $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png); $images += ,@($size, $stream.ToArray()) }
        finally { $stream.Dispose() }
    } finally { $pen.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
}
$file = [IO.File]::Create($destination)
$writer = New-Object IO.BinaryWriter($file)
try {
    $writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]$images.Count)
    $offset = 6 + 16 * $images.Count
    foreach ($image in $images) {
        $dimension = if ($image[0] -eq 256) { 0 } else { $image[0] }
        $writer.Write([Byte]$dimension); $writer.Write([Byte]$dimension); $writer.Write([UInt16]0)
        $writer.Write([UInt16]1); $writer.Write([UInt16]32); $writer.Write([UInt32]$image[1].Length); $writer.Write([UInt32]$offset)
        $offset += $image[1].Length
    }
    foreach ($image in $images) { $writer.Write([byte[]]$image[1]) }
} finally { $writer.Dispose(); $file.Dispose() }
Write-Output $destination
