param([ValidateSet('editor.html','text-editor.html')][string]$Page='editor.html')
$ErrorActionPreference='Stop'
$editorPage = [Uri]::new((Join-Path $PSScriptRoot $Page))
$editorBrowsers = @(
    (Join-Path ${env:ProgramFiles(x86)} 'Microsoft/Edge/Application/msedge.exe'),
    (Join-Path $env:ProgramFiles 'Microsoft/Edge/Application/msedge.exe'),
    (Join-Path $env:ProgramFiles 'Google/Chrome/Application/chrome.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Google/Chrome/Application/chrome.exe'),
    (Join-Path $env:LOCALAPPDATA 'Google/Chrome/Application/chrome.exe')
)
$editorBrowser = $editorBrowsers | Where-Object {Test-Path -LiteralPath $_} | Select-Object -First 1
if($editorBrowser){Start-Process -FilePath $editorBrowser -ArgumentList $editorPage.AbsoluteUri}
else{Start-Process -FilePath $editorPage.AbsoluteUri}
