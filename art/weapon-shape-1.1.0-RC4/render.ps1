$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$items=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'data.json') -Raw | ConvertFrom-Json
$width=1360
$height=180+[int][Math]::Ceiling($items.Count/3.0)*354
$bitmap=[Drawing.Bitmap]::new($width,$height)
$graphics=[Drawing.Graphics]::FromImage($bitmap)
$graphics.TextRenderingHint=[Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$graphics.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
$title=[Drawing.Font]::new('Microsoft YaHei',24,[Drawing.FontStyle]::Bold)
$heading=[Drawing.Font]::new('Microsoft YaHei',17,[Drawing.FontStyle]::Bold)
$body=[Drawing.Font]::new('Microsoft YaHei',13)
$light=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#f6dfbd'))
$ink=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#4c301f'))
$paper=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#f5ce94'))
$canvas=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#eadfc6'))
$border=[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#a56c34'),4)
$marker=[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#e19a2e'),2)
try {
 $graphics.Clear([Drawing.ColorTranslator]::FromHtml('#32251d'))
 $graphics.DrawString('RC4 · 武器形状与重量对照',$title,$light,24,18)
 $graphics.DrawString('非游戏截图 · 当前代码实际计算 · 全部为铱材质 · 未使用 AI 生图',$body,$light,26,66)
 $graphics.DrawString('96豆银河剑轮廓：59～77 / 攻速8   |   满格32格剑：84～110 / 攻速−18 · 新制挥舞时长−5%',$body,$light,26,98)
 for($i=0;$i -lt $items.Count;$i++){
  $item=$items[$i];$x=24+($i%3)*444;$y=155+[int][Math]::Floor($i/3)*354
  $graphics.FillRectangle($paper,$x,$y,424,334);$graphics.DrawRectangle($border,$x,$y,424,334)
  $graphics.DrawString($item.Name,$heading,$ink,$x+16,$y+10)
  $left=$x+106;$top=$y+52;$scale=6;$size=[int]$item.Size
  $graphics.FillRectangle($canvas,$left-4,$top-4,$size*$scale+8,$size*$scale+8)
  for($py=0;$py -lt $size;$py++){for($px=0;$px -lt $size;$px++){
   $rgba=[uint32]$item.Pixels[$py*$size+$px]
   if(($rgba -band 255) -eq 0){continue}
   $r=[int](($rgba -shr 24) -band 255);$g=[int](($rgba -shr 16) -band 255);$b=[int](($rgba -shr 8) -band 255)
   $shadow=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb([int]($r*.67),[int]($g*.67),[int]($b*.67)))
   $color=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb($r,$g,$b))
   try{$graphics.FillEllipse($shadow,$left+$px*$scale,$top+$py*$scale,5.8,5.8);$graphics.FillEllipse($color,$left+$px*$scale+.5,$top+$py*$scale+.2,4.8,4.8);$graphics.FillRectangle($canvas,$left+$px*$scale+2.4,$top+$py*$scale+2,1.4,1.4)}finally{$shadow.Dispose();$color.Dispose()}
  }}
  $result=$item.Result;$stats=$result.Stats
  $graphics.DrawRectangle($marker,$left+$result.Geometry.RootX*$scale,$top+$result.Geometry.RootY*$scale,$scale,$scale)
  $line=('{0}～{1}  ·  攻速 {2}  ·  范围 ×{3:0.00}' -f $stats.MinDamage,$stats.MaxDamage,$stats.Speed,$result.Reach)
  $graphics.DrawString($line,$body,$ink,$x+14,$y+254)
  $effect=if($result.Serration -gt 0){'锯齿：普通命中后持续伤害'}elseif($result.Geometry.Wave -gt 0){'波刃：普通命中后持续伤害'}elseif($result.Geometry.Tip -gt 0){'尖端：暴击率 {0:0.0}%' -f ($stats.CritChance*100)}elseif($item.Use -eq 'Hammer'){'宽头：击退 {0:0.00}' -f $stats.Knockback}else{'保留原版武器动作'}
  $graphics.DrawString(('{0}颗豆 · {1}' -f $result.Geometry.Count,$effect),$body,$ink,$x+14,$y+291)
 }
 $bitmap.Save((Join-Path $PSScriptRoot 'comparison.png'),[Drawing.Imaging.ImageFormat]::Png)
}finally{$graphics.Dispose();$bitmap.Dispose();$title.Dispose();$heading.Dispose();$body.Dispose();$light.Dispose();$ink.Dispose();$paper.Dispose();$canvas.Dispose();$border.Dispose();$marker.Dispose()}
