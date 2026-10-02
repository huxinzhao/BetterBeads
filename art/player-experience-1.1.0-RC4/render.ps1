$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$layouts=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'layouts.json') -Raw | ConvertFrom-Json
$shape=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'shape.json') -Raw | ConvertFrom-Json
$copy=Get-Content -LiteralPath (Join-Path $root 'BetterBeads/i18n/zh.json') -Raw | ConvertFrom-Json
if(-not ('PreviewRawPixels' -as [type])){
 Add-Type -ReferencedAssemblies System.Drawing.Common,System.Drawing.Primitives -TypeDefinition @"
using System; using System.IO; using System.Drawing; using System.Drawing.Imaging; using System.Runtime.InteropServices;
public static class PreviewRawPixels {
 public static Bitmap Load(string file,int width,int height) {
  var bytes=File.ReadAllBytes(file); if(bytes.Length!=width*height*4)throw new InvalidDataException();
  for(int i=0;i<bytes.Length;i+=4){byte r=bytes[i];bytes[i]=bytes[i+2];bytes[i+2]=r;}
  var bitmap=new Bitmap(width,height,PixelFormat.Format32bppArgb);
  var data=bitmap.LockBits(new Rectangle(0,0,width,height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
  try{Marshal.Copy(bytes,0,data.Scan0,bytes.Length);}finally{bitmap.UnlockBits(data);}return bitmap;
 }
}
"@
}
$menuInfo=Get-Content -LiteralPath (Join-Path $root '.tools/ui-reference/menu.json') -Raw | ConvertFrom-Json
$cursorInfo=Get-Content -LiteralPath (Join-Path $root '.tools/ui-reference/cursors.json') -Raw | ConvertFrom-Json
$menu=[PreviewRawPixels]::Load((Join-Path $root '.tools/ui-reference/menu.rgba'),$menuInfo.width,$menuInfo.height)
$cursors=[PreviewRawPixels]::Load((Join-Path $root '.tools/ui-reference/cursors.rgba'),$cursorInfo.width,$cursorInfo.height)
$pattern=[Drawing.Bitmap]::new((Join-Path $root 'BetterBeads/assets/patterns/starter-sprout.png'))
$font=[Drawing.Font]::new('Microsoft YaHei',20,[Drawing.FontStyle]::Regular,[Drawing.GraphicsUnit]::Pixel)
$ink=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#392D32'))
$muted=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#55493D'))
$light=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#F6DFBD'))
$center=[Drawing.StringFormat]::new();$center.Alignment=[Drawing.StringAlignment]::Center;$center.LineAlignment=[Drawing.StringAlignment]::Center
$format=[Drawing.StringFormat]::new([Drawing.StringFormat]::GenericTypographic)
function Box($x,$y,$w,$h){[pscustomobject]@{X=[int]$x;Y=[int]$y;Width=[int]$w;Height=[int]$h;Right=[int]($x+$w);Bottom=[int]($y+$h)}}
function Fill($r,[string]$hex){$brush=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($hex));try{$g.FillRectangle($brush,$r.X,$r.Y,$r.Width,$r.Height)}finally{$brush.Dispose()}}
function Panel($r,[string]$style='panel'){
 if($style -eq 'inset'){
  Fill $r '#ECBA77';Fill (Box $r.X $r.Y $r.Width 1) '#B8864C';Fill (Box $r.X ($r.Bottom-1) $r.Width 1) '#F9DFAE';return
 }
 $edge=if($style -eq 'primary'){6}elseif($r.Width -lt 500){12}else{16}
 $dx=@($r.X,($r.X+$edge),($r.Right-$edge));$dy=@($r.Y,($r.Y+$edge),($r.Bottom-$edge));$dw=@($edge,($r.Width-2*$edge),$edge);$dh=@($edge,($r.Height-2*$edge),$edge)
 for($y=0;$y -lt 3;$y++){for($x=0;$x -lt 3;$x++){
  $g.DrawImage($menu,[Drawing.Rectangle]::new($dx[$x],$dy[$y],$dw[$x],$dh[$y]),$x*20,256+$y*20,20,20,[Drawing.GraphicsUnit]::Pixel)
 }}
}
function Label([string]$text,$r,[bool]$middle=$false,$brush=$ink){
 $rect=[Drawing.RectangleF]::new($r.X,$r.Y,$r.Width,$r.Height)
 if($middle){$g.DrawString($text,$font,$brush,$rect,$center)}else{$g.DrawString($text,$font,$brush,[Drawing.PointF]::new($r.X,($r.Y+($r.Height-$font.GetHeight($g))/2)),$format)}
}
function Wrapped([string]$text,[int]$width){
 $rows=[Collections.Generic.List[string]]::new()
 foreach($paragraph in $text.Split("`n")){
  $remaining=$paragraph.Trim();if($remaining.Length -eq 0){$rows.Add('');continue}
  while($remaining.Length -gt 0){
   if($g.MeasureString($remaining,$font,100000,$format).Width -le $width){$rows.Add($remaining);break}
   $fit=0;for($i=1;$i -le $remaining.Length;$i++){if($g.MeasureString($remaining.Substring(0,$i),$font,100000,$format).Width -gt $width){break};$fit=$i}
   if($fit -eq 0){$fit=1};$space=$remaining.LastIndexOf(' ',$fit-1,$fit)
   if($space -gt 0 -and $space+1 -lt $remaining.Length -and ([char]::IsDigit($remaining[$space+1]) -or $remaining[$space+1] -eq '×')){
    $earlier=$remaining.LastIndexOf(' ',$space-1);if($earlier -gt 0){$space=$earlier}
   }
   $end=if($space -gt 0){$space}else{$fit}
   $rows.Add($remaining.Substring(0,$end).TrimEnd());$remaining=$remaining.Substring($end).TrimStart()
  }
 }
 ,$rows.ToArray()
}
function Paragraph([string]$text,$r,$brush=$ink){$lines=Wrapped $text $r.Width;for($i=0;$i -lt [Math]::Min($lines.Length,[int]($r.Height/24));$i++){Label $lines[$i] (Box $r.X ($r.Y+$i*24) $r.Width 24) $false $brush}}
function Paged([string]$text,[int]$width,[int]$capacity){
 $pages=[Collections.Generic.List[object]]::new();$current=[Collections.Generic.List[string]]::new()
 foreach($paragraph in $text.Split("`n")){
  $rows=Wrapped $paragraph $width
  if($rows.Length -le $capacity -and $current.Count+$rows.Length -gt $capacity){$pages.Add($current.ToArray());$current.Clear()}
  foreach($line in $rows){if($current.Count -eq $capacity){$pages.Add($current.ToArray());$current.Clear()};$current.Add($line)}
 }
 if($current.Count -gt 0){$pages.Add($current.ToArray())};,$pages.ToArray()
}
function Button($r,[string]$text,[string]$kind='normal'){
 Fill (Box ($r.X+3) ($r.Y+5) ($r.Width-3) ($r.Height-5)) '#85613C'
 if($kind -eq 'primary'){Panel $r 'primary'}else{
  Fill $r $(if($kind -eq 'selected'){'#5B361C'}else{'#9C6633'})
  Fill (Box ($r.X+2) ($r.Y+2) ($r.Width-4) ($r.Height-4)) $(if($kind -eq 'selected'){'#E0A543'}elseif($kind -eq 'disabled'){'#D5B78C'}else{'#F9CB8D'})
  Fill (Box ($r.X+4) ($r.Y+2) ($r.Width-8) 1) '#FFE6AC'
  if($kind -eq 'selected'){
   for($i=0;$i -lt 3;$i++){Fill (Box ($r.X+3+$i) ($r.Bottom-9+$i) 2 2) '#4A2B18'}
   for($i=0;$i -lt 5;$i++){Fill (Box ($r.X+5+$i) ($r.Bottom-7-$i) 2 2) '#4A2B18'}
  }
 }
 Label $text (Box ($r.X+12) ($r.Y+8) ($r.Width-24) ($r.Height-16)) $true $(if($kind -eq 'disabled'){$muted}else{$ink})
}
function Close($r){$g.DrawImage($cursors,[Drawing.Rectangle]::new($r.X,$r.Y,$r.Width,$r.Height),337,494,12,12,[Drawing.GraphicsUnit]::Pixel)}
try{
 foreach($mode in @('recovery-small','recovery-wide','move-small','move-wide','shape-small','shape-wide','shape-page2-small','seams-wide')){
  $small=$mode.EndsWith('small');$layout=$layouts | Where-Object Width -eq $(if($small){480}else{1280}) | Select-Object -First 1
  $bitmap=[Drawing.Bitmap]::new([int]$layout.Width,[int]$layout.Height+40);$g=[Drawing.Graphics]::FromImage($bitmap)
  try{
   $g.Clear([Drawing.ColorTranslator]::FromHtml('#32251D'));$g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
   $g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half;$g.TextRenderingHint=[Drawing.Text.TextRenderingHint]::AntiAliasGridFit
   $g.ScaleTransform([single]$layout.Scale.Factor,[single]$layout.Scale.Factor)
   if($mode.StartsWith('recovery')){
    $p=$layout.Recovery;Panel $p.Dialog;Label '发现上次未保存作品' (Box ($p.Dialog.X+16) ($p.Dialog.Y+16) ($p.Dialog.Width-88) 40);Close $p.Close
    Panel $p.Preview 'inset';$g.DrawImage($pattern,[Drawing.Rectangle]::new($p.Preview.X+8,$p.Preview.Y+8,$p.Preview.Width-16,$p.Preview.Height-16))
    Paragraph '我的未保存作品' $p.Name
    $noteHeight=[Math]::Min($p.Description.Height,(Wrapped $copy.'simple.recovery-note' $p.Description.Width).Length*24)
    Paragraph $copy.'simple.recovery-note' (Box $p.Description.X ($p.Description.Y+($p.Description.Height-$noteHeight)/2) $p.Description.Width $noteHeight) $muted
    Button $p.Delete '删除恢复缓存';Button $p.Restore '恢复作品' 'primary'
   }elseif($mode.StartsWith('move')){
    $p=$layout.Move;Panel $p.Dialog;Label '移动与复制' (Box ($p.Dialog.X+16) ($p.Dialog.Y+16) ($p.Dialog.Width-88) 40);Close $p.Close;Panel $p.Preview 'inset'
    $side=$p.Preview.Height-16;$x=$p.Preview.X+($p.Preview.Width-$side)/2;$y=$p.Preview.Y+8
    $g.DrawImage($pattern,[Drawing.Rectangle]::new($x,$y,$side,$side))
    Label '位移：横向 2，纵向 0' (Box $p.Description.X $p.Description.Y $p.Description.Width 24)
    Paragraph $copy.'simple.pattern-safe' (Box $p.Description.X ($p.Description.Y+24) $p.Description.Width 48) $muted
    Button $p.Up '▲';Button $p.Left '◀';Button $p.Down '▼';Button $p.Right '▶';Button $p.Mode '移动' 'selected';Button $p.Copy '复制'
    Button $p.Cancel '取消';Button $p.Apply '应用' 'primary'
   }else{
    $p=$layout.Scene;Panel $p.Dialog;Label '成品效果预览' (Box ($p.Dialog.X+16) ($p.Dialog.Y+16) ($p.Dialog.Width-88) 40);Close $p.Close
    if($mode.StartsWith('shape')){
     $e=$layout.Explanation;Panel $e.Body 'inset';$grid=$shape.Design.Views.front
     $zoom=[Math]::Min($e.Picture.Width/$grid.Width,$e.Picture.Height/$grid.Height);$left=$e.Picture.X;$top=$e.Picture.Y
     for($i=0;$i -lt $grid.Cells.Count;$i++){
      $cell=$grid.Cells[$i];if($null -eq $cell){continue};$rgba=[uint32]$cell.Rgba
      $color=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb($(if($shape.Main -contains $i){255}else{64}),[int](($rgba -shr 24) -band 255),[int](($rgba -shr 16) -band 255),[int](($rgba -shr 8) -band 255)))
      try{$g.FillRectangle($color,[single]($left+($i%$grid.Width)*$zoom),[single]($top+[Math]::Floor($i/$grid.Width)*$zoom),[single][Math]::Max(1,$zoom),[single][Math]::Max(1,$zoom))}finally{$color.Dispose()}
     }
     $pen=[Drawing.Pen]::new([Drawing.Color]::Gold,2);$cx=$left+($shape.Shape.Geometry.RootX+.5)*$zoom;$cy=$top+($shape.Shape.Geometry.RootY+.5)*$zoom
     try{$g.DrawLine($pen,[single]($cx-4),[single]$cy,[single]($cx+4),[single]$cy);$g.DrawLine($pen,[single]$cx,[single]($cy-4),[single]$cx,[single]($cy+4))}finally{$pen.Dispose()}
     Paragraph $copy.'simple.shape-markers' $e.Legend $muted
     $text=@(('总豆数 {0} · 主体 {1} · 孤立 {2}' -f $shape.Shape.Geometry.Count,$shape.Shape.Geometry.MainCount,($shape.Shape.Geometry.Count-$shape.Shape.Geometry.MainCount)),('主体长度约 {0:0.#}px' -f $shape.Shape.Geometry.Length),('伤害 {0}～{1} · 攻速 {2} · 范围 ×{3:0.##}' -f $shape.Shape.Stats.MinDamage,$shape.Shape.Stats.MaxDamage,$shape.Shape.Stats.Speed,$shape.Shape.Reach),$copy.'simple.shape-weight-note',$copy.'simple.shape-range-note') -join "`n"
     $capacity=[int][Math]::Floor($e.Text.Height/24);$textPages=Paged $text $e.Text.Width $capacity;$pages=$textPages.Length;$page=if($mode.Contains('page2')){[Math]::Min(1,$pages-1)}else{0}
     for($i=0;$i -lt $textPages[$page].Length;$i++){Label $textPages[$page][$i] (Box $e.Text.X ($e.Text.Y+$i*24) $e.Text.Width 24)}
     Button $e.Previous '◀' $(if($page -eq 0){'disabled'}else{'normal'});Button $e.Next '▶' $(if($page+1 -eq $pages){'disabled'}else{'normal'});Label "$($page+1) / $pages" $e.Page $true $muted
     $bw=[int](($p.Toggle.Width-8)/2);Button (Box $p.Toggle.X $p.Toggle.Y $bw $p.Toggle.Height) '返回场景';Button (Box ($p.Toggle.X+$bw+8) $p.Toggle.Y ($p.Toggle.Width-$bw-8) $p.Toggle.Height) '判定说明' 'selected'
    }else{
     Panel $p.Stage 'inset';$scale=[Math]::Min($p.Stage.Width/96,$p.Stage.Height/80);$x=$p.Stage.X+($p.Stage.Width-96*$scale)/2;$y=$p.Stage.Y+($p.Stage.Height-80*$scale)/2
     for($ty=0;$ty -lt 80;$ty+=16){for($tx=0;$tx -lt 96;$tx+=16){$g.DrawImage($pattern,[Drawing.Rectangle]::new([int]($x+$tx*$scale),[int]($y+$ty*$scale),[int](16*$scale),[int](16*$scale)))}}
     $pen=[Drawing.Pen]::new([Drawing.Color]::Gold,1)
     try{for($tx=0;$tx -le 96;$tx+=16){$g.DrawLine($pen,[single]($x+$tx*$scale),[single]$y,[single]($x+$tx*$scale),[single]($y+80*$scale))};for($ty=0;$ty -le 80;$ty+=16){$g.DrawLine($pen,[single]$x,[single]($y+$ty*$scale),[single]($x+96*$scale),[single]($y+$ty*$scale))}}finally{$pen.Dispose()}
     Label '16×16px · 连续纹样接缝检查' $p.Info;Label $copy.'simple.scene-seam-note' $p.Note $false $muted
     $bw=[int](($p.Toggle.Width-8)/2);Button (Box $p.Toggle.X $p.Toggle.Y $bw $p.Toggle.Height) '放大细节';Button (Box ($p.Toggle.X+$bw+8) $p.Toggle.Y ($p.Toggle.Width-$bw-8) $p.Toggle.Height) '接缝标记' 'selected'
    }
   }
   $g.ResetTransform();$caption=[Drawing.Font]::new('Microsoft YaHei',14,[Drawing.FontStyle]::Regular,[Drawing.GraphicsUnit]::Pixel)
   try{$g.DrawString('非游戏截图 · 原版/模组素材 · 字体与手感待实测',$caption,$light,8,[int]$layout.Height+8)}finally{$caption.Dispose()}
   $bitmap.Save((Join-Path $PSScriptRoot ($mode+'.png')),[Drawing.Imaging.ImageFormat]::Png)
  }finally{$g.Dispose();$bitmap.Dispose()}
 }
}finally{$menu.Dispose();$cursors.Dispose();$pattern.Dispose();$font.Dispose();$ink.Dispose();$muted.Dispose();$light.Dispose();$center.Dispose();$format.Dispose()}
