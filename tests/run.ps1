$ErrorActionPreference = 'Stop'
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskProject = Split-Path $PSScriptRoot
& $taskCompiler /nologo /target:exe /utf8output /r:System.Core.dll "/r:$taskProject\lib\TagLibSharp.dll" "/out:$PSScriptRoot\Tests.exe" "$taskProject\src\Database.cs" "$taskProject\src\Mp3.cs" "$taskProject\src\Transfer.cs" "$taskProject\src\Library.cs" "$PSScriptRoot\Tests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
Copy-Item -LiteralPath "$taskProject\lib\TagLibSharp.dll" -Destination $PSScriptRoot -Force
& "$PSScriptRoot\Tests.exe" @args
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
