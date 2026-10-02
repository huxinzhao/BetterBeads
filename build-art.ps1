$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$artVersion = (Get-Content -LiteralPath 'BetterBeads/manifest.json' -Raw | ConvertFrom-Json).Version
$artDestination = Join-Path $PSScriptRoot "dist/BetterBeads-Art-$artVersion.zip"
New-Item -ItemType Directory -Force (Join-Path $PSScriptRoot 'dist') | Out-Null
# Only artwork sources, runtime PNG/JSON files and their guide are packaged.
$artEntries = @()
foreach ($artFolder in @('art','BetterBeads/assets','BetterBeads/i18n')) {
    foreach ($artFile in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot $artFolder) -File -Recurse) {
        if($artFile.FullName -match '[\\/]\.(pixel|text)-backups[\\/]'){continue}
        $artEntries += [PSCustomObject]@{ Full = $artFile.FullName; Relative = [System.IO.Path]::GetRelativePath($PSScriptRoot,$artFile.FullName).Replace('\','/') }
    }
}
foreach ($artDoc in @('打开文案编辑器.cmd','docs/TEXT_EDITOR.md','打开像素编辑器.cmd','docs/PIXEL_EDITOR.md','docs/ART_GUIDE.md','docs/CONTENT_EDITING.md','docs/FEATURES.md','docs/GAMEPLAY_016.md','docs/GAMEPLAY_REVIEW_0163.md')) {
    $artEntries += [PSCustomObject]@{ Full = (Join-Path $PSScriptRoot $artDoc); Relative = $artDoc }
}
$artStream = [System.IO.File]::Open($artDestination,[System.IO.FileMode]::Create)
$artZip = [System.IO.Compression.ZipArchive]::new($artStream,[System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($artEntry in $artEntries) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($artZip,$artEntry.Full,$artEntry.Relative,[System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $artZip.Dispose(); $artStream.Dispose() }
$artZip = [System.IO.Compression.ZipFile]::OpenRead($artDestination)
try {
    if (Compare-Object @($artEntries.Relative) @($artZip.Entries.FullName)) { throw '美术包缺失或包含额外文件。' }
    foreach ($artEntry in $artZip.Entries) {
        $artHashAlgorithm = [System.Security.Cryptography.SHA256]::Create()
        $artEntryStream = $artEntry.Open()
        try { $artHash = [Convert]::ToHexString($artHashAlgorithm.ComputeHash($artEntryStream)) }
        finally { $artEntryStream.Dispose(); $artHashAlgorithm.Dispose() }
        if ($artHash -ne (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot $artEntry.FullName) -Algorithm SHA256).Hash) { throw "美术包文件不一致：$($artEntry.FullName)" }
    }
    Write-Output "PASS 美术源文件包 $artVersion，$($artZip.Entries.Count)个文件逐项校验一致：$artDestination"
} finally { $artZip.Dispose() }
