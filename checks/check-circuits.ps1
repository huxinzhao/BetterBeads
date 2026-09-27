param([string]$GamePath = 'D:\steam\steamapps\common\Stardew Valley')
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $GamePath 'smapi-internal\Mono.Cecil.dll')
$assembly=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GamePath 'Stardew Valley.dll'))
try{
    foreach($entry in @(
        @('StardewValley.Object','checkForAction',2),
        @('StardewValley.Objects.Furniture','draw',4),
        @('StardewValley.Item','getOne',0),
        @('StardewValley.ItemTypeDefinitions.ParsedItemData','GetTexture',0)
    )){
        $type=$assembly.MainModule.Types|Where-Object FullName -eq $entry[0]
        $method=@($type.Methods|Where-Object {$_.Name -eq $entry[1] -and $_.Parameters.Count -eq $entry[2]})
        if($method.Count -ne 1){throw "游戏方法不匹配：$($entry[0]).$($entry[1])"}
    }
    $object=$assembly.MainModule.Types|Where-Object FullName -eq 'StardewValley.Object'
    if(!($object.Fields|Where-Object Name -eq 'preservedParentSheetIndex')){throw '音乐块音高字段不存在。'}
    if(!($object.Fields|Where-Object Name -eq 'heldObject')){throw '展示台展品字段不存在。'}
    $furniture=$assembly.MainModule.Types|Where-Object FullName -eq 'StardewValley.Objects.Furniture'
    if(!($furniture.Methods|Where-Object Name -eq 'IsTable')){throw '展示台接口不存在。'}
    if(!($furniture.Methods|Where-Object Name -eq 'canBePlacedHere') -or !($furniture.Methods|Where-Object Name -eq 'placementAction')){throw '摆件放置接口不存在。'}
    $location=$assembly.MainModule.Types|Where-Object FullName -eq 'StardewValley.GameLocation'
    if(!($location.Methods|Where-Object Name -eq 'IsTileOccupiedBy') -or !($location.Methods|Where-Object Name -eq 'CanPlaceThisFurnitureHere')){throw '创造模式占地检查接口不存在。'}
    $adjacent=$object.Methods|Where-Object Name -eq 'farmerAdjacentAction'
    if(!($adjacent.Body.Instructions|Where-Object {$_.Operand -eq '(O)464'}) -or !($adjacent.Body.Instructions|Where-Object {$_.Operand -eq '(O)463'})){
        throw '原版长笛块或鼓块 ID 已变化。'
    }
    Write-Host 'PASS 游戏接口、长笛块与鼓块 ID、创造放置接口。未启动游戏。'
}finally{$assembly.Dispose()}
$litePath=Join-Path (Split-Path $PSScriptRoot -Parent) 'BetterBeads\bin\Lite\net6.0\BetterBeads.dll'
if(!(Test-Path -LiteralPath $litePath)){throw '请先编译 Lite 0.16.34。'}
$lite=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($litePath)
try{
    $bridge=$lite.MainModule.Types|Where-Object FullName -eq 'BetterBeads.Runtime.CircuitBridge'
    foreach($name in @('ListArtworks','GetArtwork','CreateCreativeController')){
        if(!($bridge.Methods|Where-Object Name -eq $name)){throw "Lite 电路接口缺少 $name。"}
    }
    Write-Host 'PASS Lite 只读图案与创造控制器接口。'
}finally{$lite.Dispose()}
