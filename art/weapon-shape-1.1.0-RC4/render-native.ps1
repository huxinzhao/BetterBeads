$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$all=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'native-shape-survey.json') -Raw | ConvertFrom-Json
$ids=@('4','23','29','8','9','10','44','45','47','53','66','51','61','5','14','58','38','65')
$items=@($ids | ForEach-Object { $id=$_;$all | Where-Object Id -eq $id })
$width=1440;$height=130+[int][Math]::Ceiling($items.Count/3.0)*258
$bmp=[Drawing.Bitmap]::new($width,$height);$g=[Drawing.Graphics]::FromImage($bmp)
$title=[Drawing.Font]::new('Microsoft YaHei',21,[Drawing.FontStyle]::Bold)
$head=[Drawing.Font]::new('Microsoft YaHei',15,[Drawing.FontStyle]::Bold)
$font=[Drawing.Font]::new('Microsoft YaHei',12)
$ink=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#422b21'))
$light=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#f5dfbe'))
$paper=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#f5ce94'))
$canvas=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#eadfc6'))
$marker=[Drawing.Pen]::new([Drawing.Color]::OrangeRed,2)
try{
 $g.Clear([Drawing.ColorTranslator]::FromHtml('#32251d'))
 $g.DrawString('原版武器图案 · 拼豆形状检查',$title,$light,24,18)
 $g.DrawString('非游戏截图 · 本机原版素材与实际计算 · 统一铱材质 · 攻速为内部属性 · 新制挥舞时长−5%',$font,$light,24,62)
 $g.DrawString('形状不会继承原武器的附魔、收割或其他专属效果；橙框标出分析根部。',$font,$light,24,89)
 for($i=0;$i -lt $items.Count;$i++){
  $row=$items[$i];$x=24+($i%3)*472;$y=122+[int][Math]::Floor($i/3)*258
  $g.FillRectangle($paper,$x,$y,448,242)
  $g.DrawString($row.Name,$head,$ink,$x+12,$y+8)
  $sx=$x+18;$sy=$y+51;$scale=9
  $g.FillRectangle($canvas,$sx,$sy,144,144)
  for($py=0;$py -lt 16;$py++){for($px=0;$px -lt 16;$px++){
   $rgba=[uint32]$row.Pixels[$py*16+$px];if(($rgba -band 255) -eq 0){continue}
   $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb([int](($rgba -shr 24) -band 255),[int](($rgba -shr 16) -band 255),[int](($rgba -shr 8) -band 255)))
   try{$g.FillRectangle($brush,$sx+$px*$scale,$sy+$py*$scale,$scale,$scale)}finally{$brush.Dispose()}
  }}
  $g.DrawRectangle($marker,$sx+$row.Geometry.RootX*$scale,$sy+$row.Geometry.RootY*$scale,$scale,$scale)
  $s=$row.BeadsStats;$tx=$x+177
  $g.DrawString(('{0}颗豆 · {1}' -f $row.Beads,$row.Type),$font,$ink,$tx,$y+57)
  $g.DrawString(('伤害 {0}～{1}' -f $s.MinDamage,$s.MaxDamage),$font,$ink,$tx,$y+84)
  $g.DrawString(('攻速 {0} · 范围 ×{1:0.00}' -f $s.Speed,$row.Range),$font,$ink,$tx,$y+111)
  $g.DrawString(('暴击 {0:0.00}% · 击退 {1:0.00}' -f ($s.CritChance*100),$s.Knockback),$font,$ink,$tx,$y+138)
  $effect=if($row.Serration -gt 0){'锯齿评分 {0:0.00}' -f $row.Serration}elseif($row.Geometry.Wave -gt 0){'波刃流血 · {0:0.0}%' -f ($row.Bleeding*30)}else{'无流血误触发'}
  $g.DrawString($effect,$font,$ink,$tx,$y+165)
  $g.DrawString(('原型伤害 {0}～{1} / 攻速 {2}' -f $row.Original.MinDamage,$row.Original.MaxDamage,$row.Original.Speed),$font,$ink,$x+14,$y+211)
 }
 $bmp.Save((Join-Path $PSScriptRoot 'native-comparison.png'),[Drawing.Imaging.ImageFormat]::Png)
}finally{$g.Dispose();$bmp.Dispose();$title.Dispose();$head.Dispose();$font.Dispose();$ink.Dispose();$light.Dispose();$paper.Dispose();$canvas.Dispose();$marker.Dispose()}
