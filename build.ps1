param([string]$GamePath = 'D:\steam\steamapps\common\Stardew Valley', [ValidatePattern('^[a-zA-Z0-9.-]*$')][string]$PackageTag = '')
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.tools\cli-home'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot '.tools\packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = '0'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$dotnetExe = Join-Path $PSScriptRoot '.tools\dotnet\dotnet.exe'
if (!(Test-Path -LiteralPath $dotnetExe)) { $dotnetExe = 'dotnet' }
& $dotnetExe build 'BetterBeads\BetterBeads.csproj' -c Release --nologo "-p:GamePath=$GamePath" '-p:LiteBuild=true'
if ($LASTEXITCODE -ne 0) { throw '构建失败，未生成发布包。' }
$output = Join-Path $PSScriptRoot 'BetterBeads\bin\Current\net6.0'
$distRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'dist'))
$stagingRoot = Join-Path $distRoot ('.staging-' + [Guid]::NewGuid().ToString('N'))
$package = Join-Path $stagingRoot 'BetterBeads'
try {
    New-Item -ItemType Directory -Force "$package\i18n" | Out-Null
    foreach ($name in @('BetterBeads.dll', 'manifest.json','README.txt')) {
        Copy-Item -LiteralPath "$output\$name" -Destination $package -Force
    }
    foreach ($name in @('default.json', 'zh.json')) {
        Copy-Item -LiteralPath "$output\i18n\$name" -Destination "$package\i18n" -Force
    }
    # Enumerate current sources so stale files in an incremental build can't enter the ZIP.
    $assetSources = Join-Path $PSScriptRoot 'BetterBeads\assets'
    foreach ($assetFile in Get-ChildItem -LiteralPath $assetSources -File -Recurse) {
        if($assetFile.FullName -match '[\\/]\.pixel-backups[\\/]'){continue}
        if($assetFile.FullName -match '[\\/]patterns[\\/]'){continue}
        if($assetFile.Name -in @('BeadMask.png','ScrollTrack.png','ScrollThumb.png')){continue}
        $relativeAsset = [IO.Path]::GetRelativePath($assetSources, $assetFile.FullName)
        $assetDestination = Join-Path "$package\assets" $relativeAsset
        New-Item -ItemType Directory -Force (Split-Path $assetDestination -Parent) | Out-Null
        Copy-Item -LiteralPath (Join-Path "$output\assets" $relativeAsset) -Destination $assetDestination
    }
    $manifest = Get-Content -LiteralPath "$package\manifest.json" -Raw | ConvertFrom-Json
    if ($manifest.UniqueID -ne 'xinzh.BetterBeads' -or !(Test-Path -LiteralPath "$package\$($manifest.EntryDll)")) {
        throw '发布包入口或唯一 ID 错误。'
    }
    if ($manifest.Version -ne '1.0.0' -or $manifest.Name -ne 'Better Beads') { throw '发布包版本或名称错误。' }
    New-Item -ItemType Directory -Force "$package\imports" | Out-Null
    Copy-Item -LiteralPath 'BetterBeads\imports\README.txt' -Destination "$package\imports\README.txt" -Force
    foreach ($locale in @('zh','default')) {
        $copy = Get-Content -LiteralPath "$package\i18n\$locale.json" -Raw | ConvertFrom-Json -AsHashtable
        foreach ($key in @($copy.Keys)) {
            if ($key -match '^copy\.(WorkshopService|WorkshopProgress|WorkshopGuidance|ReferencePatterns|PatternDetailMenu)\.' -or $key -in @('editor.patterns','editor.commissions')) { $copy.Remove($key) }
        }
        $copy | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath "$package\i18n\$locale.json" -Encoding utf8
    }
    $suffix = if ($PackageTag) { "-$PackageTag" } else { '' }
    $zipPath = "dist\BetterBeads-$($manifest.Version)$suffix.zip"
    Compress-Archive -LiteralPath $package -DestinationPath $zipPath -Force
    $verify = [System.IO.Compression.ZipFile]::OpenRead((Join-Path $PSScriptRoot $zipPath))
    try {
        foreach ($entry in $verify.Entries | Where-Object { $_.Name }) {
            $stream = $entry.Open(); $hasher = [Security.Cryptography.SHA256]::Create()
            try { $hash = [Convert]::ToHexString($hasher.ComputeHash($stream)) } finally { $stream.Dispose(); $hasher.Dispose() }
            if ($hash -ne (Get-FileHash -LiteralPath (Join-Path $stagingRoot $entry.FullName)).Hash) { throw "包内文件校验失败：$($entry.FullName)" }
        }
        Write-Host "发布包：$zipPath；$($verify.Entries.Count) 个条目逐项校验通过。"
    } finally { $verify.Dispose() }
} finally {
    # Delete only this build's temporary staging directory, never a shared output.
    if (Test-Path -LiteralPath $stagingRoot) {
        $resolvedStaging = (Resolve-Path -LiteralPath $stagingRoot).Path
        if (![IO.Path]::GetFullPath($resolvedStaging).StartsWith($distRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
            [IO.Path]::GetFileName($resolvedStaging) -notmatch '^\.staging-[0-9a-f]{32}$') {
            throw "Unsafe staging cleanup path: $resolvedStaging"
        }
        Remove-Item -LiteralPath $resolvedStaging -Recurse -Force
    }
}
