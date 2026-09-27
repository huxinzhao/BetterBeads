param([string]$GamePath = 'D:\steam\steamapps\common\Stardew Valley', [switch]$SkipPackage)
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $GamePath 'smapi-internal\Mono.Cecil.dll')
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GamePath 'Stardew Valley.dll'))
try {
    $required = @{
        'StardewValley.Game1' = @('shouldTimePass')
        'StardewValley.Object' = @('checkForAction','salePrice','sellToStorePrice')
        'StardewValley.Objects.Furniture' = @('drawInMenu','draw','drawWhenHeld','maximumStackSize','salePrice')
        'StardewValley.Objects.Hat' = @('drawInMenu','draw','maximumStackSize')
        'StardewValley.Item' = @('canStackWith','getOne','salePrice')
        'StardewValley.FarmerRenderer' = @('drawHairAndAccesories')
        'StardewValley.ItemTypeDefinitions.ParsedItemData' = @('GetTexture','GetSourceRect')
        'StardewValley.ItemRegistry' = @('GetData')
        'StardewValley.Farmer' = @('GetDisplayShirt','GetDisplayPants','addQuest','removeQuest','changeFriendship')
        'StardewValley.NPC' = @('checkAction')
        'StardewValley.Quests.Quest' = @('getQuestFromId')
        'StardewValley.Objects.Clothing' = @('drawInMenu','maximumStackSize')

        'StardewValley.Tools.MeleeWeapon' = @('ReloadData','drawInMenu','maximumStackSize','salePrice','drawTooltip','setFarmerAnimating','animateSpecialMove','DoDamage','CanForge','CanAddEnchantment','Forge')
    }
    foreach ($name in $required.Keys) {
        $type = $assembly.MainModule.Types | Where-Object FullName -EQ $name
        foreach ($method in $required[$name]) {
            $matches = @($type.Methods | Where-Object Name -EQ $method)
            if ($matches.Count -ne 1) { throw "补丁入口缺失或有歧义：$name.$method" }
            Write-Output "PASS $($matches[0].FullName)"
        }
    }
    $questType=$assembly.MainModule.Types | Where-Object FullName -eq 'StardewValley.Quests.Quest'
    foreach($field in @('_questTitle','_questDescription','_currentObjective','canBeCancelled')) {
        if(-not ($questType.Fields | Where-Object Name -eq $field)){throw "Quest field missing: $field"}
    }
    $npcType=$assembly.MainModule.Types | Where-Object FullName -eq 'StardewValley.NPC'
    if((($npcType.Methods | Where-Object Name -eq 'checkAction').Parameters.ParameterType.FullName -join ',') -ne 'StardewValley.Farmer,StardewValley.GameLocation'){throw 'NPC interaction signature changed'}
    if(-not (($questType.Methods | Where-Object Name -eq 'getQuestFromId').Body.Instructions | Where-Object { $_.Operand -eq 'Basic' })){throw 'Basic native quest unavailable'}
    Write-Output 'PASS 原生Basic任务、NPC交互、友好度与任务文本字段；实际剧情交付未运行。'
    $renderer = $assembly.MainModule.Types | Where-Object FullName -EQ 'StardewValley.FarmerRenderer'
    $menuType=$assembly.MainModule.Types | Where-Object FullName -eq 'StardewValley.Menus.IClickableMenu'
    $nativeFrame=@($menuType.Methods | Where-Object { $_.Name -eq 'drawTextureBox' -and $_.Parameters.Count -eq 11 })
    if($nativeFrame.Count -ne 1 -or $nativeFrame[0].Parameters[1].ParameterType.FullName -ne 'Microsoft.Xna.Framework.Graphics.Texture2D'){throw 'Native menu frame drawing interface changed'}
    $gameType=$assembly.MainModule.Types | Where-Object FullName -eq 'StardewValley.Game1'
    if(-not ($gameType.Fields | Where-Object { $_.Name -eq 'menuTexture' -and $_.IsPublic -and $_.IsStatic })){throw 'Native menu texture field missing'}
    if(@($gameType.Methods | Where-Object { $_.Name -eq 'getSourceRectForStandardTileSheet' -and $_.Parameters.Count -eq 4 }).Count -ne 1){throw 'Inventory slot source rectangle interface changed'}
    Write-Output 'PASS 原生菜单边框、菜单贴图及背包物品格取图接口。未启动游戏。'
    $draw = $renderer.Methods | Where-Object Name -EQ 'drawHairAndAccesories'
    if (!($draw.Parameters | Where-Object { $_.Name -eq 'who' -and $_.ParameterType.FullName -eq 'StardewValley.Farmer' })) { throw '穿戴补丁参数不匹配' }
    $calls = @($draw.Body.Instructions | Where-Object { $_.Operand -and $_.Operand.ToString() -match 'ParsedItemData::GetTexture' })
    if ($calls.Count -eq 0) { throw '原生帽子绘制不再经过预期的取纹理入口' }
    $objectType = $assembly.MainModule.Types | Where-Object FullName -EQ 'StardewValley.Object'
    $action = $objectType.Methods | Where-Object Name -EQ 'checkForAction'
    if (($action.Parameters.Name -join ',') -ne 'who,justCheckingForActivity') { throw '工作台交互补丁参数不匹配' }
    $shopType = $assembly.MainModule.Types | Where-Object FullName -EQ 'StardewValley.Menus.ShopMenu'
    $purchase = $shopType.Methods | Where-Object Name -EQ 'tryToPurchaseItem'
    if (!($purchase.Body.Instructions | Where-Object { $_.Operand -and $_.Operand.ToString() -match 'Item::LearnRecipe' })) {
        throw '商店购买不再经过已核对的原生配方学习入口'
    }
    Write-Output 'PASS 工作台交互参数与商店配方学习调用。'
    $chestType = $assembly.MainModule.Types | Where-Object FullName -eq 'StardewValley.Objects.Chest'
    $getItems = @($chestType.Methods | Where-Object { $_.Name -eq 'GetItemsForPlayer' -and $_.Parameters.Count -eq 0 })
    if ($getItems.Count -ne 1 -or $getItems[0].ReturnType.FullName -ne 'StardewValley.Inventories.IInventory') { throw '附近箱子库存入口发生变化' }
    foreach($methodName in @('GetActualCapacity','GetMutex')) {
        if (@($chestType.Methods | Where-Object Name -eq $methodName).Count -ne 1) { throw "附近箱子接口变化：$methodName" }
    }
    foreach($fieldName in @('playerChest','fridge','giftbox','globalInventoryId','specialChestType')) {
        if (!($chestType.Fields | Where-Object Name -eq $fieldName)) { throw "箱子类型过滤字段缺失：$fieldName" }
    }
    $mutexType=$assembly.MainModule.Types | Where-Object FullName -eq 'StardewValley.Network.NetMutex'
    if (@($mutexType.Methods | Where-Object Name -eq 'IsLocked').Count -ne 1) { throw '箱子锁状态入口变化' }
    Write-Output 'PASS 普通箱子库存、容量、特殊容器过滤及锁状态接口；实际跨容器扣料仍待游戏验收。'
    $farmer = $assembly.MainModule.Types | Where-Object FullName -eq 'StardewValley.Farmer'
    foreach ($methodName in @('GetDisplayShirt','GetDisplayPants')) {
        $method = $farmer.Methods | Where-Object Name -eq $methodName
        if (($method.Parameters.ParameterType.FullName -join ',') -ne 'Microsoft.Xna.Framework.Graphics.Texture2D&,System.Int32&') { throw '服装纹理替换参数不匹配' }
        if (!($renderer.Methods.Body.Instructions | Where-Object { $_.Operand -and $_.Operand.ToString() -match "Farmer::$methodName" })) { throw '原生渲染未调用服装纹理入口' }
    }
    $furnitureType = $assembly.MainModule.Types | Where-Object FullName -eq 'StardewValley.Objects.Furniture'
    $furnitureDraw = $furnitureType.Methods | Where-Object Name -eq 'draw'
    $frontLoads = @($furnitureDraw.Body.Instructions | Where-Object { $_.Operand -and $_.Operand.ToString() -eq 'T StardewValley.LocalizedContentManager::Load<Microsoft.Xna.Framework.Graphics.Texture2D>(System.String)' })
    if ($frontLoads.Count -ne 1 -or !($furnitureType.Fields | Where-Object Name -eq 'sourceIndexOffset')) { throw '椅子遮挡或家具帧读取入口变化' }
    Write-Output 'PASS 服装取纹理参数、渲染调用与单个椅子前景加载入口。穿戴动画和坐姿仍待实测。'
    $weapon = $assembly.MainModule.Types | Where-Object FullName -EQ 'StardewValley.Tools.MeleeWeapon'
    $weaponDraws = @($weapon.Methods | Where-Object Name -EQ 'drawDuringUse')
    $weaponStaticDraw = @($weaponDraws | Where-Object IsStatic)
    if ($weaponDraws.Count -ne 2 -or $weaponStaticDraw.Count -ne 1) { throw '武器绘制重载结构变化' }
    if (($weaponStaticDraw[0].Parameters.Name -join ',') -ne 'frameOfFarmerAnimation,facingDirection,spriteBatch,playerPosition,f,weaponItemId,type,isOnSpecial') { throw '武器绘制补丁参数不匹配' }
    foreach ($entry in @('GetTexture','GetSourceRect')) {
        if (!($weaponStaticDraw[0].Body.Instructions | Where-Object { $_.Operand -and $_.Operand.ToString() -match "ParsedItemData::$entry" })) { throw "原生武器绘制缺少预期入口：$entry" }
    }
    foreach ($pair in @(@('defenseSword',3),@('dagger',1),@('club',2))) {
        if (($weapon.Fields | Where-Object Name -EQ $pair[0]).Constant -ne $pair[1]) { throw '原生武器类型常量变化' }
    }
    Write-Output 'PASS 武器绘制上下文参数、取图路径及原生类型。未执行游戏绘制或战斗。'
    $locationType = $assembly.MainModule.Types | Where-Object FullName -eq 'StardewValley.GameLocation'
    $damageMethod = @($locationType.Methods | Where-Object { $_.Name -eq 'damageMonster' -and $_.Parameters.Count -eq 11 })
    if ($damageMethod.Count -ne 1) { throw '吸血入口的伤害重载不匹配' }
    $hitCalls = @($damageMethod[0].Body.Instructions | Where-Object { $_.Operand -and $_.Operand.ToString() -eq 'System.Int32 StardewValley.Monsters.Monster::takeDamage(System.Int32,System.Int32,System.Int32,System.Boolean,System.Double,StardewValley.Farmer)' })
    if ($hitCalls.Count -ne 1) { throw '原生怪物受击调用不匹配，不能安全注入实际伤害统计' }
    if (@($weapon.Methods | Where-Object Name -eq 'getDescription').Count -ne 1) { throw '武器效果说明入口不匹配' }
    Write-Output 'PASS 效果说明入口与单次怪物实际扣血调用。未执行游戏战斗。'
    $forgeMenu = $assembly.MainModule.Types | Where-Object FullName -eq 'StardewValley.Menus.ForgeMenu'
    $validateCraft = $forgeMenu.Methods | Where-Object Name -eq '_ValidateCraft'
    if (!($validateCraft.Body.Instructions | Where-Object { $_.Operand -and $_.Operand.ToString() -match 'Item::getOne' })) { throw '锻造预览不再复制原件' }
    $cost = $forgeMenu.Methods | Where-Object Name -eq 'GetForgeCost'
    if (!($cost.Body.Instructions | Where-Object { $_.Operand -and $_.Operand.ToString() -eq '(O)896' })) { throw '原生锻造不再识别银河之魂成本' }
    $craft = $forgeMenu.Methods | Where-Object Name -eq 'CraftItem'
    if (!($craft.Body.Instructions | Where-Object { $_.Operand -and $_.Operand.ToString() -match 'Tool::Forge' })) { throw '锻造不再调用武器强化入口' }
    $delegate = $assembly.MainModule.Types | Where-Object FullName -eq 'StardewValley.Delegates.MachineOutputDelegate'
    $invoke = $delegate.Methods | Where-Object Name -eq 'Invoke'
    $expectedParameters = 'StardewValley.Object,StardewValley.Item,System.Boolean,StardewValley.GameData.Machines.MachineItemOutput,StardewValley.Farmer,System.Nullable`1<System.Int32>&'
    if (($invoke.Parameters.ParameterType.FullName -join ',') -ne $expectedParameters) { throw '精炼产物回调参数不匹配' }
    Write-Output 'PASS 锻造预览复制、银河之魂入口与精炼产物委托签名。'
} finally { $assembly.Dispose() }
$root = Split-Path $PSScriptRoot -Parent
$manifest = Get-Content -LiteralPath "$root\BetterBeads\manifest.json" -Raw | ConvertFrom-Json
$default = Get-Content -LiteralPath "$root\BetterBeads\i18n\default.json" -Raw | ConvertFrom-Json
$zh = Get-Content -LiteralPath "$root\BetterBeads\i18n\zh.json" -Raw | ConvertFrom-Json
if (Compare-Object @($default.PSObject.Properties.Name) @($zh.PSObject.Properties.Name)) { throw '翻译键不一致' }
$sources = Get-ChildItem -LiteralPath "$root\BetterBeads" -Filter '*.cs' -Recurse | Where-Object FullName -NotMatch '[\\/]obj[\\/]'
foreach ($file in $sources) {
    $content = Get-Content -LiteralPath $file.FullName -Raw
    foreach ($match in [regex]::Matches($content, '(?:Translation|text)\.Get\("([^"\r\n]+)"\)')) {
        if (!$default.PSObject.Properties[$match.Groups[1].Value]) { throw "缺少翻译：$($match.Groups[1].Value)" }
    }
}
if ($SkipPackage) { Write-Output '开发中：仅检查源码与接口，未验证旧安装包。'; return }
$zip = [System.IO.Compression.ZipFile]::OpenRead("$root\dist\BetterBeads-$($manifest.Version).zip")
try {
    $expected = @('BetterBeads/BetterBeads.dll','BetterBeads/manifest.json','BetterBeads/README.txt','BetterBeads/i18n/default.json','BetterBeads/i18n/zh.json')
    $assetRoot = Join-Path $root 'BetterBeads\assets'
    foreach ($assetFile in Get-ChildItem -LiteralPath $assetRoot -File -Recurse) {
        if($assetFile.FullName -match '[\\/]\.pixel-backups[\\/]'){continue}
        $relativeAsset = [System.IO.Path]::GetRelativePath($assetRoot,$assetFile.FullName).Replace('\','/')
        $expected += 'BetterBeads/assets/'+$relativeAsset
    }
    if (Compare-Object $expected @($zip.Entries.FullName)) { throw '安装包内容不符合白名单' }
    foreach ($entry in $zip.Entries) {
        $relative = $entry.FullName.Substring('BetterBeads/'.Length)
        $current = Join-Path "$root\BetterBeads\bin\Release\net6.0" $relative
        $stream = $entry.Open()
        $sha = [System.Security.Cryptography.SHA256]::Create()
        try { $hash = [Convert]::ToHexString($sha.ComputeHash($stream)) }
        finally { $stream.Dispose(); $sha.Dispose() }
        if ($hash -ne (Get-FileHash -LiteralPath $current -Algorithm SHA256).Hash) {
            throw "安装包不是当前构建：$relative"
        }
    }
    Write-Output "PASS 发布包 $($manifest.Version)：入口、两份翻译与正式美术资源，无游戏程序集或开发文件。"
} finally { $zip.Dispose() }
