param([string]$Dotnet = '.\.tools\dotnet\dotnet.exe')
$ErrorActionPreference='Stop'
Set-Location -LiteralPath (Split-Path $PSScriptRoot -Parent)
$node=(Get-Command node -ErrorAction SilentlyContinue).Source
if(!$node){throw 'Node.js is required for offline editor checks.'}
& $node --check 'BeadCircuits\offline\app.js'
if($LASTEXITCODE -ne 0){throw 'Offline editor JavaScript syntax invalid.'}
$browser=& $node -e "console.log(JSON.stringify(require('./BeadCircuits/offline/app.js').demoScore()))"
if($LASTEXITCODE -ne 0){throw 'Offline editor demo failed.'}
$core=& $Dotnet run --no-build --project 'BeadCircuits.Checks\BeadCircuits.Checks.csproj' -c Release -- --demo-json
if($LASTEXITCODE -ne 0){throw 'Core demo export failed.'}
if(($browser|ConvertFrom-Json|ConvertTo-Json -Depth 10 -Compress) -ne ($core|ConvertFrom-Json|ConvertTo-Json -Depth 10 -Compress))
{throw 'Browser score and core score differ.'}
Write-Host 'PASS 离线网页语法与 C# 64 步示范乐谱一致。'
