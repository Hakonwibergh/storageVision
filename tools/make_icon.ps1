param([string]$OutPath)
Add-Type -AssemblyName System.Drawing
$S = 1024
$bmp = New-Object System.Drawing.Bitmap $S, $S
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$g.TextRenderingHint = 'AntiAliasGridFit'
$g.Clear([System.Drawing.Color]::Transparent)

function C($hex, $a = 255) {
    $c = [System.Drawing.ColorTranslator]::FromHtml($hex)
    return [System.Drawing.Color]::FromArgb($a, $c.R, $c.G, $c.B)
}
function Poly([float[]]$xy) {
    $pts = New-Object 'System.Drawing.PointF[]' ($xy.Length / 2)
    for ($i = 0; $i -lt $pts.Length; $i++) { $pts[$i] = New-Object System.Drawing.PointF $xy[2 * $i], $xy[2 * $i + 1] }
    return , $pts
}
function RoundRect($x, $y, $w, $h, $r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

# Background: rounded square with a dark blue-grey gradient.
$bg = RoundRect 0 0 $S $S 170
$bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Point 0, 0), (New-Object System.Drawing.Point 0, $S), (C '#243042'), (C '#0D1219')
$g.FillPath($bgBrush, $bg)
$g.SetClip($bg)

# Faint storage racks in the background.
$rackBrush = New-Object System.Drawing.SolidBrush (C '#3A4658' 120)
foreach ($x in 70, 930) { $g.FillRectangle($rackBrush, $x, 120, 26, 820) }
$beamBrush = New-Object System.Drawing.SolidBrush (C '#C8862A' 70)
foreach ($y in 250) { $g.FillRectangle($beamBrush, 70, $y, 886, 22) }

# Shelf beam under the box.
$g.FillRectangle((New-Object System.Drawing.SolidBrush (C '#D9922E')), 40, 800, 944, 46)
$g.FillRectangle((New-Object System.Drawing.SolidBrush (C '#8F5C1C')), 40, 846, 944, 16)

# Box geometry (front face + top + right side).
$fx1 = 250; $fy1 = 420; $fx2 = 690; $fy2 = 800; $dx = 84; $dy = 84
$front = Poly @($fx1, $fy1, $fx2, $fy1, $fx2, $fy2, $fx1, $fy2)
$top = Poly @($fx1, $fy1, ($fx1 + $dx), ($fy1 - $dy), ($fx2 + $dx), ($fy1 - $dy), $fx2, $fy1)
$side = Poly @($fx2, $fy1, ($fx2 + $dx), ($fy1 - $dy), ($fx2 + $dx), ($fy2 - $dy), $fx2, $fy2)
$silhouette = Poly @($fx1, $fy2, $fx1, $fy1, ($fx1 + $dx), ($fy1 - $dy), ($fx2 + $dx), ($fy1 - $dy), ($fx2 + $dx), ($fy2 - $dy), $fx2, $fy2)

# Green glow behind the outline.
foreach ($pair in @(@(110, 22), @(80, 40), @(56, 70))) {
    $glow = New-Object System.Drawing.Pen (C '#3CF06A' $pair[1]), $pair[0]
    $glow.LineJoin = 'Round'
    $g.DrawPolygon($glow, $silhouette)
}

$g.FillPolygon((New-Object System.Drawing.SolidBrush (C '#C9975A')), $front)
$g.FillPolygon((New-Object System.Drawing.SolidBrush (C '#E2B97E')), $top)
$g.FillPolygon((New-Object System.Drawing.SolidBrush (C '#9E7140')), $side)

# Tape across the top and down the front.
$tapeTop = Poly @(($fx1 + ($fx2 - $fx1) / 2 - 34), $fy1, ($fx1 + ($fx2 - $fx1) / 2 - 34 + $dx), ($fy1 - $dy), ($fx1 + ($fx2 - $fx1) / 2 + 34 + $dx), ($fy1 - $dy), ($fx1 + ($fx2 - $fx1) / 2 + 34), $fy1)
$g.FillPolygon((New-Object System.Drawing.SolidBrush (C '#F0D9AA')), $tapeTop)
$g.FillRectangle((New-Object System.Drawing.SolidBrush (C '#E6C995')), ($fx1 + ($fx2 - $fx1) / 2 - 34), $fy1, 68, 120)

# Label on the front with an amount, like the in-game boxes.
$labelPath = RoundRect 330 600 280 130 22
$g.FillPath((New-Object System.Drawing.SolidBrush (C '#141A22' 225)), $labelPath)
$font = New-Object System.Drawing.Font 'Segoe UI', 74, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
$fmt = New-Object System.Drawing.StringFormat
$fmt.Alignment = 'Center'; $fmt.LineAlignment = 'Center'
$g.DrawString('x40', $font, (New-Object System.Drawing.SolidBrush (C '#5CFF84')), (New-Object System.Drawing.RectangleF 330, 600, 280, 130), $fmt)

# Crisp green outline.
$outline = New-Object System.Drawing.Pen (C '#3CF06A'), 26
$outline.LineJoin = 'Round'
$g.DrawPolygon($outline, $silhouette)

# Crosshair dot aimed at the box.
$cx = 380; $cy = 520
$g.FillEllipse((New-Object System.Drawing.SolidBrush (C '#000000' 140)), ($cx - 26), ($cy - 26), 52, 52)
$g.FillEllipse((New-Object System.Drawing.SolidBrush (C '#FFFFFF')), ($cx - 17), ($cy - 17), 34, 34)

$g.ResetClip()
$g.Dispose()

# Downscale to 256x256 for smooth edges.
$out = New-Object System.Drawing.Bitmap 256, 256
$g2 = [System.Drawing.Graphics]::FromImage($out)
$g2.InterpolationMode = 'HighQualityBicubic'
$g2.SmoothingMode = 'AntiAlias'
$g2.PixelOffsetMode = 'HighQuality'
$g2.DrawImage($bmp, 0, 0, 256, 256)
$g2.Dispose()
$out.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
$out.Dispose(); $bmp.Dispose()
Write-Host "Saved $OutPath"

