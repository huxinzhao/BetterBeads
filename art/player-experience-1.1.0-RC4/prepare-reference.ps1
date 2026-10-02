param([Parameter(Mandatory=$true)][string]$GamePath)
$ErrorActionPreference='Stop'
# Preview only. Read the locally installed game; never launch it or copy textures into the mod package.
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$target=Join-Path $root '.tools/ui-reference'
[IO.Directory]::CreateDirectory($target)|Out-Null
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $GamePath 'MonoGame.Framework.dll'))
$decoderType=$assembly.GetType('MonoGame.Framework.Utilities.LzxDecoderStream',$true)
foreach($entry in @(@('menu','Maps/MenuTiles.xnb'),@('cursors','LooseSprites/Cursors.xnb'))){
 $path=Join-Path (Join-Path $GamePath 'Content') $entry[1]
 $bytes=[IO.File]::ReadAllBytes($path)
 if($bytes.Length -lt 14 -or $bytes[0] -ne 88 -or $bytes[1] -ne 78 -or $bytes[2] -ne 66 -or ($bytes[5] -band 128) -eq 0){throw 'Expected LZX game texture'}
 $stream=[IO.MemoryStream]::new($bytes);$stream.Position=14
 $decoder=[Activator]::CreateInstance($decoderType,@($stream,[BitConverter]::ToInt32($bytes,10),($bytes.Length-14)))
 $decoded=[IO.MemoryStream]::new()
 try{
  $decoder.CopyTo($decoded);$decoded.Position=0;$reader=[IO.BinaryReader]::new($decoded)
  $count=$reader.Read7BitEncodedInt()
  for($i=0;$i -lt $count;$i++){$null=$reader.ReadString();$null=$reader.ReadInt32()}
  if($reader.Read7BitEncodedInt() -ne 0){throw 'Unexpected shared texture data'}
  $null=$reader.Read7BitEncodedInt()
  if($reader.ReadInt32() -ne 0){throw 'Unexpected pixel format'}
  $width=$reader.ReadInt32();$height=$reader.ReadInt32();$null=$reader.ReadInt32();$length=$reader.ReadInt32()
  if($width -lt 1 -or $height -lt 1 -or $length -ne [long]$width*$height*4){throw 'Unexpected texture length'}
  $pixels=$reader.ReadBytes($length);if($pixels.Length -ne $length){throw 'Incomplete texture'}
  [IO.File]::WriteAllBytes((Join-Path $target ($entry[0]+'.rgba')),$pixels)
  @{width=$width;height=$height}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $target ($entry[0]+'.json')) -Encoding utf8
 }finally{$decoded.Dispose();$decoder.Dispose();$stream.Dispose()}
}
Write-Output 'Prepared local menu/cursor preview references. No game launched; no Mods or save files changed.'
