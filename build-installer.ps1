param([string]$Compiler = (Join-Path $PSScriptRoot 'tools\nsis-3.13\Bin\makensis.exe'), [string]$AppDirectory = (Join-Path $PSScriptRoot 'dist'))
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Compiler)) {
    throw 'Download the official NSIS 3.13 portable ZIP from https://nsis.sourceforge.io/Download and extract it into tools, or pass -Compiler.'
}
New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot 'releases') -Force | Out-Null
& $Compiler /INPUTCHARSET UTF8 /V3 ("/DAPP_DIST=" + [IO.Path]::GetFullPath($AppDirectory)) (Join-Path $PSScriptRoot 'installer\ShuffleDrop.nsi')
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
$taskSetup = Join-Path $PSScriptRoot 'releases\ShuffleDrop-Setup-0.3.1.exe'
$taskHash = (Get-FileHash -LiteralPath $taskSetup -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($taskSetup + '.sha256'), ($taskHash + '  ShuffleDrop-Setup-0.3.1.exe' + [Environment]::NewLine))
Write-Output $taskSetup
