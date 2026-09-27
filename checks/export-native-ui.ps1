param([string]$GamePath='D:\steam\steamapps\common\Stardew Valley')
$ErrorActionPreference='Stop'
# Read-only reference extraction for offline previews. Never included in the runtime or art source ZIP.
Add-Type -Path (Join-Path $GamePath 'MonoGame.Framework.dll')
$bytes=[IO.File]::ReadAllBytes((Join-Path $GamePath 'Content/Maps/MenuTiles.xnb'))
if(($bytes[5] -band 128) -eq 0){throw 'This reference exporter expects the local LZX-compressed MenuTiles XNB.'}
$size=[BitConverter]::ToInt32($bytes,10)
$inputStream=[IO.MemoryStream]::new($bytes);$inputStream.Position=14
$type=[Microsoft.Xna.Framework.Color].Assembly.GetType('MonoGame.Framework.Utilities.LzxDecoderStream')
$decoder=[Activator]::CreateInstance($type,@($inputStream,[int]$size,[int]($bytes.Length-14)))
$decoded=[IO.MemoryStream]::new()
try{$decoder.CopyTo($decoded)}finally{$decoder.Dispose();$inputStream.Dispose()}
$decoded.Position=0;$reader=[IO.BinaryReader]::new($decoded)
try{
    $count=$reader.Read7BitEncodedInt()
    for($i=0;$i -lt $count;$i++){[void]$reader.ReadString();[void]$reader.ReadInt32()}
    if($reader.Read7BitEncodedInt() -ne 0){throw 'Unexpected shared resources'}
    [void]$reader.Read7BitEncodedInt()
    if($reader.ReadInt32() -ne 0){throw 'Expected RGBA Color texture'}
    $texWidth=$reader.ReadInt32();$texHeight=$reader.ReadInt32();[void]$reader.ReadInt32();$length=$reader.ReadInt32()
    if($length -ne $texWidth*$texHeight*4){throw 'Unexpected texture length'}
    $target=Join-Path (Split-Path $PSScriptRoot -Parent) '.tools/ui-reference'
    New-Item -ItemType Directory -Force $target | Out-Null
    [IO.File]::WriteAllBytes((Join-Path $target 'menu.rgba'),$reader.ReadBytes($length))
    [IO.File]::WriteAllText((Join-Path $target 'menu.json'),(@{width=$texWidth;height=$texHeight} | ConvertTo-Json))
    "PASS local menu texture reference exported to .tools only ($texWidth x $texHeight)."
}finally{$reader.Dispose();$decoded.Dispose()}
