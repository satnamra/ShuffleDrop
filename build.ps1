param([string]$OutputDirectory = 'dist')
$ErrorActionPreference = 'Stop'
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskOutput = Join-Path $PSScriptRoot $OutputDirectory
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskSources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
$taskReferences = @('TagLibSharp.dll','Krypton.Toolkit.dll','NAudio.Core.dll','NAudio.Wasapi.dll')
$taskReferenceArgs = @($taskReferences | ForEach-Object { '/r:' + (Join-Path $PSScriptRoot ('lib\' + $_)) })
$taskNetstandard = Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL\netstandard\v4.0_2.0.0.0__cc7b13ffcd2ddd51\netstandard.dll'
$taskIcon = Join-Path $PSScriptRoot 'assets\ShuffleDrop.ico'
$taskIconArgs = @(); if (Test-Path -LiteralPath $taskIcon) { $taskIconArgs = @('/win32icon:' + $taskIcon) }
& $taskCompiler /nologo /target:winexe /platform:anycpu /optimize+ /utf8output "/out:$taskOutput\ShuffleDrop.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll "/r:$taskNetstandard" @taskReferenceArgs @taskIconArgs @taskSources
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
foreach ($taskReference in $taskReferences) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('lib\' + $taskReference)) -Destination $taskOutput -Force }
if (Test-Path -LiteralPath $taskIcon) { Copy-Item -LiteralPath $taskIcon -Destination $taskOutput -Force }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'packaging\ShuffleDrop.exe.config') -Destination $taskOutput -Force
foreach ($taskDoc in @('README.md','LICENSE','SOURCES.md')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $taskDoc) -Destination $taskOutput -Force }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'third-party') -Destination $taskOutput -Recurse -Force
New-Item -ItemType Directory -Path (Join-Path $taskOutput 'docs') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\README.lt.md') -Destination (Join-Path $taskOutput 'docs\README.lt.md') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'installer\Start-here.txt') -Destination (Join-Path $taskOutput 'START-HERE.txt') -Force
Write-Output "Built: $taskOutput\ShuffleDrop.exe"
