param([ValidatePattern('^[a-zA-Z0-9.-]+$')][string]$Tag='1.1.0-RC6-NoAppearance')
$ErrorActionPreference='Stop'
$sourceRoot=[IO.Path]::GetFullPath($PSScriptRoot)
$distRoot=Join-Path $sourceRoot 'dist'
New-Item -ItemType Directory -Force -Path $distRoot | Out-Null
$destination=Join-Path $distRoot "BetterBeads-$Tag-source.zip"
if(Test-Path -LiteralPath $destination){throw 'Archive exists; choose a new review tag.'}
Add-Type -AssemblyName System.IO.Compression
$files=@(Get-ChildItem -LiteralPath $sourceRoot -Recurse -File | Where-Object {
 $relative=[IO.Path]::GetRelativePath($sourceRoot,$_.FullName)
 $relative -notmatch '(^|[\\/])(bin|obj|dist|\.tools|\.git|\.pixel-backups|\.text-backups)([\\/]|$)' -and
 $relative -ne 'SOURCE_SHA256.txt' -and
 ($relative -notmatch '[\\/](imports|exports|draft-recovery)[\\/]' -or $_.Name -eq 'README.txt')
} | Sort-Object FullName)
$hashes=@{}
$stream=[IO.File]::Open($destination,[IO.FileMode]::CreateNew)
$archive=[IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Create)
try {
 foreach($file in $files){
  $relative=[IO.Path]::GetRelativePath($sourceRoot,$file.FullName).Replace('\','/')
  $hashes[$relative]=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
  $entry=$archive.CreateEntry('BetterBeads-source/'+$relative,[IO.Compression.CompressionLevel]::Optimal)
  $input=[IO.File]::OpenRead($file.FullName);$output=$entry.Open()
  try{$input.CopyTo($output)}finally{$input.Dispose();$output.Dispose()}
 }
 $writer=[IO.StreamWriter]::new($archive.CreateEntry('BetterBeads-source/SOURCE_SHA256.txt').Open(),[Text.UTF8Encoding]::new($false))
 try{foreach($relative in $hashes.Keys | Sort-Object){$writer.WriteLine($hashes[$relative]+'  '+$relative)}}finally{$writer.Dispose()}
}finally{$archive.Dispose();$stream.Dispose()}
$verify=[IO.Compression.ZipFile]::OpenRead($destination)
try {
 foreach($relative in $hashes.Keys){
  $input=$verify.GetEntry('BetterBeads-source/'+$relative).Open();$hasher=[Security.Cryptography.SHA256]::Create()
  try{$actual=[Convert]::ToHexString($hasher.ComputeHash($input))}finally{$input.Dispose();$hasher.Dispose()}
  if($actual -ne $hashes[$relative]){throw "Archive mismatch: $relative"}
 }
 if($verify.Entries.FullName -match '/(bin|obj|\.tools|\.git)/|\.(dll|exe|xnb|rgba|pdb)$'){throw 'Unexpected build or game binary'}
}finally{$verify.Dispose()}
$hash=(Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
Set-Content -LiteralPath ($destination+'.sha256') -Value ($hash+'  '+[IO.Path]::GetFileName($destination)) -Encoding utf8
Write-Output "$($files.Count) source files verified: $destination"
