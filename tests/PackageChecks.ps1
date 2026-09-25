[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$RepoRoot,
    [Parameter(Mandatory)]
    [string]$DistDirectory
)

$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath($RepoRoot)
$dist = [IO.Path]::GetFullPath($DistDirectory)
$archive = Join-Path $dist 'RdpSessionReminder-Windows.zip'
$checksumFile = Join-Path $dist 'SHA256SUMS.txt'
$expectedFiles = @(
    'RdpSessionReminder.exe',
    'RdpSessionReminderSetup.exe',
    'RdpSessionReminder-Installer.exe',
    'RdpSessionReminder-Uninstaller.exe',
    'RdpSessionReminder-Windows.zip'
)

$checksums = @{}
foreach ($line in [IO.File]::ReadAllLines($checksumFile)) {
    if ($line -notmatch '^([0-9a-f]{64})  (.+)$') {
        throw "Invalid checksum line: $line"
    }
    $checksums[$Matches[2]] = $Matches[1]
}

$installerAssembly = [Reflection.Assembly]::LoadFile(
    (Join-Path $dist 'RdpSessionReminder-Installer.exe'))
$payloads = @{
    'RdpSessionReminder.Payload.Runtime' = 'RdpSessionReminder.exe'
    'RdpSessionReminder.Payload.Setup' = 'RdpSessionReminderSetup.exe'
    'RdpSessionReminder.Payload.Uninstaller' =
        'RdpSessionReminder-Uninstaller.exe'
}
foreach ($resourceName in $payloads.Keys) {
    $stream = $installerAssembly.GetManifestResourceStream($resourceName)
    if ($null -eq $stream) {
        throw "Installer payload is missing: $resourceName"
    }
    try {
        $algorithm = [Security.Cryptography.SHA256]::Create()
        try {
            $payloadHash = -join ($algorithm.ComputeHash($stream) |
                ForEach-Object { $_.ToString('x2') })
        }
        finally {
            $algorithm.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
    $fileName = $payloads[$resourceName]
    if ($payloadHash -ne $checksums[$fileName]) {
        throw "Embedded installer payload differs from build output: $fileName"
    }
}
if ($checksums.Count -ne $expectedFiles.Count) {
    throw "Expected $($expectedFiles.Count) checksums, found $($checksums.Count)."
}
foreach ($name in $expectedFiles) {
    $path = Join-Path $dist $name
    if (-not [IO.File]::Exists($path) -or -not $checksums.ContainsKey($name)) {
        throw "Missing package output or checksum: $name"
    }
    $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $checksums[$name]) {
        throw "Checksum mismatch: $name"
    }
}

$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) (
    'RdpSessionReminderPackage-' + [guid]::NewGuid().ToString('N'))
try {
    Expand-Archive -LiteralPath $archive -DestinationPath $temporaryDirectory
    $actualNames = @(
        Get-ChildItem -LiteralPath $temporaryDirectory -File |
            Sort-Object Name |
            Select-Object -ExpandProperty Name
    )
    $packageNames = @(
        'LICENSE',
        'README.md',
        'RdpSessionReminder.exe',
        'RdpSessionReminderSetup.exe'
    )
    if (($actualNames -join '|') -ne (($packageNames | Sort-Object) -join '|')) {
        throw "Unexpected ZIP contents: $($actualNames -join ', ')"
    }

    foreach ($name in 'RdpSessionReminder.exe', 'RdpSessionReminderSetup.exe') {
        $actual = (Get-FileHash -LiteralPath (
            Join-Path $temporaryDirectory $name) -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $checksums[$name]) {
            throw "Packaged executable checksum mismatch: $name"
        }
    }
    foreach ($name in 'README.md', 'LICENSE') {
        $sourceHash = (Get-FileHash -LiteralPath (
            Join-Path $repo $name) -Algorithm SHA256).Hash
        $packageHash = (Get-FileHash -LiteralPath (
            Join-Path $temporaryDirectory $name) -Algorithm SHA256).Hash
        if ($sourceHash -ne $packageHash) {
            throw "Packaged file differs from the repository copy: $name"
        }
    }
}
finally {
    if ([IO.Directory]::Exists($temporaryDirectory)) {
        [IO.Directory]::Delete($temporaryDirectory, $true)
    }
}

Write-Host 'Package checks passed.'
