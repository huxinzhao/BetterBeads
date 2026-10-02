param([string]$Tag='1.1.0-RC4-review')
$ErrorActionPreference='Stop'
if($Tag -notmatch '^[a-zA-Z0-9.-]+$'){throw 'Invalid source tag'}
$workspace=[IO.Path]::GetFullPath($PSScriptRoot)
$destination=Join-Path $workspace "dist/BetterBeads-$Tag-source.zip"
if(Test-Path -LiteralPath $destination){throw 'Source archive already exists; choose a distinct review tag.'}
Add-Type -AssemblyName System.IO.Compression
$files=[Collections.Generic.List[object]]::new()
function IncludeFile([string]$relative,[string]$entry=$relative){
 $path=[IO.Path]::GetFullPath((Join-Path $workspace $relative))
 if(!$path.StartsWith($workspace+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Path outside workspace'}
 if(!(Test-Path -LiteralPath $path -PathType Leaf)){throw "Missing review source: $relative"}
 $files.Add([pscustomobject]@{Source=$path;Entry=('BetterBeads-source/'+$entry.Replace('\','/'));Hash=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash})
}
foreach($folder in @('BetterBeads','checks/lite','tools','docs')){
 foreach($file in Get-ChildItem -LiteralPath (Join-Path $workspace $folder) -File -Recurse){
  $relative=[IO.Path]::GetRelativePath($workspace,$file.FullName)
  if($relative -match '[\\/](bin|obj|\.pixel-backups)[\\/]'){continue}
  if($folder -in @('checks/lite','tools') -and $file.Extension -notin @('.cs','.csproj')){continue}
  if($folder -eq 'BetterBeads' -and $relative -match '[\\/](imports|exports|draft-recovery)[\\/]' -and $file.Name -ne 'README.txt'){continue}
  IncludeFile $relative
 }
}
foreach($file in @('README.md','NuGet.Config','.gitignore','build.ps1','build-art.ps1','build-source-review.ps1','art/PATTERN_SOURCES.md',
 'art/weapon-shape-1.1.0-RC4/index.html','art/weapon-shape-1.1.0-RC4/data.js','art/weapon-shape-1.1.0-RC4/data.json','art/weapon-shape-1.1.0-RC4/render.ps1','art/weapon-shape-1.1.0-RC4/comparison.png',
 'art/weapon-shape-1.1.0-RC4/native-comparison.png','art/weapon-shape-1.1.0-RC4/native-summary.json','art/weapon-shape-1.1.0-RC4/render-native.ps1',
 'art/input-1.1.0-RC4/preview.png','art/polish-1.1.0-RC4/index.html')){IncludeFile $file}
foreach($name in @('editor.html','pixel-editor.css','pixel-assets.js','pixel-core.js','pixel-editor.js',
 'source-editor.html','source-core.js','source-data.js','source.json','export.mjs','export-output.mjs',
 'text-editor.html','text-editor.css','text-editor.js','text-core.js','text-data.js','README.md')){IncludeFile ('art/'+$name)}
foreach($name in @('editor-safety-check.mjs','pixel-editor-check.mjs','editor-safety-results.txt',
 'pixel-editor-results.txt','reliability-results.txt','reliability-runtime-results.txt')){IncludeFile ('checks/'+$name)}
IncludeFile '打开像素编辑器.cmd'
IncludeFile 'tools/apply-player-copy.mjs'
IncludeFile 'checks/player-experience-results.txt'
IncludeFile 'checks/player-ui-results.txt'
foreach($file in Get-ChildItem -LiteralPath (Join-Path $workspace 'art/player-experience-1.1.0-RC4') -File){
 IncludeFile ([IO.Path]::GetRelativePath($workspace,$file.FullName))
}
IncludeFile '打开文案编辑器.cmd'
foreach($name in @('UPDATE.zh.txt','UPDATE.en.txt')){IncludeFile ('release/publish-1.1.0-RC4/'+$name)}
IncludeFile 'release/publish-1.0.0-RC7/github/LICENSE.txt' 'LICENSE.txt'
IncludeFile 'release/publish-1.0.0-RC7/github/THIRD_PARTY_NOTICES.md' 'THIRD_PARTY_NOTICES.md'
if(($files.Entry | Sort-Object -Unique).Count -ne $files.Count){throw 'Duplicate archive entry'}
$stream=[IO.File]::Open($destination,[IO.FileMode]::CreateNew)
$archive=[IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Create)
try{
 foreach($file in $files | Sort-Object Entry){
  $entry=$archive.CreateEntry($file.Entry,[IO.Compression.CompressionLevel]::Optimal)
  $input=[IO.File]::OpenRead($file.Source);$output=$entry.Open()
  try{$input.CopyTo($output)}finally{$input.Dispose();$output.Dispose()}
 }
 $list=$archive.CreateEntry('BetterBeads-source/SOURCE_SHA256.txt')
 $writer=[IO.StreamWriter]::new($list.Open(),[Text.UTF8Encoding]::new($false))
 try{foreach($file in $files | Sort-Object Entry){$writer.WriteLine($file.Hash+'  '+$file.Entry.Substring('BetterBeads-source/'.Length))}}finally{$writer.Dispose()}
}finally{$archive.Dispose();$stream.Dispose()}
$read=[IO.Compression.ZipFile]::OpenRead($destination)
try{
 foreach($file in $files){
  $entry=$read.GetEntry($file.Entry);if($null -eq $entry){throw "Missing archive file: $($file.Entry)"}
  $data=$entry.Open();$hasher=[Security.Cryptography.SHA256]::Create()
  try{$hash=[Convert]::ToHexString($hasher.ComputeHash($data))}finally{$data.Dispose();$hasher.Dispose()}
  if($hash -ne $file.Hash){throw "Archive mismatch: $($file.Entry)"}
 }
 if($read.Entries | Where-Object { $_.FullName -match '/(bin|obj|\.tools|\.git)/|\.(dll|xnb|rgba|exe|pdb)$' }){throw 'Unexpected binary or extracted game asset'}
}finally{$read.Dispose()}
$zipHash=(Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
Set-Content -LiteralPath ($destination+'.sha256') -Value ($zipHash+'  '+[IO.Path]::GetFileName($destination)) -Encoding utf8
Write-Output "Review archive: $destination"
Write-Output "$($files.Count) source/resource/review files verified individually. SHA256 $zipHash"
