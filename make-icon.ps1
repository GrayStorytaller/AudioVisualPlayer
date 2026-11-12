Add-Type -AssemblyName System.Drawing

$outDir = Join-Path $PSScriptRoot "AudioVisualPlayer.App\Assets"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$out = Join-Path $outDir "app.ico"

function RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $p.AddArc($x, $y, $r*2, $r*2, 180, 90)
    $p.AddArc($x+$w-$r*2, $y, $r*2, $r*2, 270, 90)
    $p.AddArc($x+$w-$r*2, $y+$h-$r*2, $r*2, $r*2, 0, 90)
    $p.AddArc($x, $y+$h-$r*2, $r*2, $r*2, 90, 90)
    $p.CloseFigure()
    return $p
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$entries = @()

foreach ($s in $sizes) {
    $bmp = [System.Drawing.Bitmap]::new($s, $s)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

    $d = [float]$s
    $bg = RoundRect 0 0 $d $d ($d*0.22)
    $rect = [System.Drawing.RectangleF]::new(0, 0, $d, $d)
    $grad = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        $rect,
        [System.Drawing.Color]::FromArgb(255, 255, 61, 129),
        [System.Drawing.Color]::FromArgb(255, 34, 211, 238),
        [float]45)
    $g.FillPath($grad, $bg)

    $barBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(235, 11, 14, 20))
    $heights = @(0.30, 0.52, 0.74, 0.46, 0.62)
    $bw = $d*0.085; $gap = $d*0.06
    $total = 5*$bw + 4*$gap
    $x = ($d-$total)/2
    foreach ($h in $heights) {
        $bh = $d*$h
        $bar = RoundRect $x (($d-$bh)/2) $bw $bh ($bw/2)
        $g.FillPath($barBrush, $bar)
        $bar.Dispose()
        $x += $bw + $gap
    }

    $ms = [System.IO.MemoryStream]::new()
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $entries += ,@($s, $ms.ToArray())

    $barBrush.Dispose(); $grad.Dispose(); $bg.Dispose(); $ms.Dispose(); $g.Dispose(); $bmp.Dispose()
}

$fs = [System.IO.File]::Create($out)
$w = [System.IO.BinaryWriter]::new($fs)
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$entries.Count)
$offset = 6 + 16*$entries.Count
foreach ($e in $entries) {
    $s = $e[0]; $data = $e[1]
    $b = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$b); $w.Write([byte]$b); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$data.Length); $w.Write([uint32]$offset)
    $offset += $data.Length
}
foreach ($e in $entries) { $w.Write([byte[]]$e[1]) }
$w.Close(); $fs.Close()
Write-Host "Icon created: $out"