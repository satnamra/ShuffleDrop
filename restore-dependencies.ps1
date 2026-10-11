$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$taskLib = Join-Path $PSScriptRoot 'lib'
New-Item -ItemType Directory -Path $taskLib -Force | Out-Null
$taskPackages = @(Get-Content -LiteralPath (Join-Path $PSScriptRoot 'dependencies.json') -Raw | ConvertFrom-Json)
$taskHashes = @(Get-Content -LiteralPath (Join-Path $PSScriptRoot 'dependency-hashes.json') -Raw | ConvertFrom-Json)
foreach ($taskPackage in $taskPackages) {
    $taskFile = $taskPackage.Id + '.dll'
    $taskDestination = Join-Path $taskLib $taskFile
    $taskExpected = @($taskHashes | Where-Object { $_.File -eq $taskFile })
    if ($taskExpected.Count -ne 1) { throw ('Missing pinned hash: ' + $taskFile) }
    if ((Test-Path -LiteralPath $taskDestination) -and ((Get-FileHash -LiteralPath $taskDestination -Algorithm SHA256).Hash -eq $taskExpected[0].Hash)) {
        Write-Output ('Verified: ' + $taskFile)
        continue
    }
    $taskId = $taskPackage.Id.ToLowerInvariant()
    $taskVersion = $taskPackage.Version
    $taskTempPackage = Join-Path $taskLib ($taskId + '.' + $taskVersion + '.download')
    $taskTempDll = Join-Path $taskLib ($taskFile + '.download')
    try {
        $taskUrl = 'https://api.nuget.org/v3-flatcontainer/' + $taskId + '/' + $taskVersion + '/' + $taskId + '.' + $taskVersion + '.nupkg'
        Invoke-WebRequest -UseBasicParsing -Uri $taskUrl -OutFile $taskTempPackage
        $taskZip = [IO.Compression.ZipFile]::OpenRead($taskTempPackage)
        try {
            $taskEntry = $taskZip.GetEntry('lib/' + $taskPackage.Framework + '/' + $taskFile)
            if ($null -eq $taskEntry) { throw ('Runtime library missing from package: ' + $taskFile) }
            [IO.Compression.ZipFileExtensions]::ExtractToFile($taskEntry, $taskTempDll, $true)
        } finally { $taskZip.Dispose() }
        if ((Get-FileHash -LiteralPath $taskTempDll -Algorithm SHA256).Hash -ne $taskExpected[0].Hash) {
            throw ('Downloaded library failed SHA-256 verification: ' + $taskFile)
        }
        Move-Item -LiteralPath $taskTempDll -Destination $taskDestination -Force
        Write-Output ('Restored and verified: ' + $taskFile)
    } finally {
        foreach ($taskTempFile in @($taskTempPackage, $taskTempDll)) {
            if (Test-Path -LiteralPath $taskTempFile) { Remove-Item -LiteralPath $taskTempFile }
        }
    }
}
