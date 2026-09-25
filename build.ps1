[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$sourceDirectory = Join-Path $repoRoot 'src'
$assetDirectory = Join-Path $repoRoot 'assets'
$distDirectory = [IO.Path]::GetFullPath((Join-Path $repoRoot 'dist'))

if (-not $distDirectory.StartsWith($repoRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The output directory is outside the repository.'
}

if (Test-Path -LiteralPath $distDirectory) {
    Remove-Item -LiteralPath $distDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $distDirectory -Force | Out-Null

$compilerCandidates = @(
    (Join-Path $env:SystemRoot 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:SystemRoot 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } |
    Select-Object -First 1
if (-not $compiler) {
    throw '.NET Framework 4.x C# compiler was not found.'
}

$commonSource = Join-Path $sourceDirectory 'Common.cs'
$runtimeSource = Join-Path $sourceDirectory 'RdpSessionReminder.cs'
$setupSource = Join-Path $sourceDirectory 'RdpSessionReminderSetup.cs'
$iconPath = Join-Path $assetDirectory 'RdpSessionReminder.ico'
$manifestPath = Join-Path $assetDirectory 'app.manifest'
$runtimeOutput = Join-Path $distDirectory 'RdpSessionReminder.exe'
$setupOutput = Join-Path $distDirectory 'RdpSessionReminderSetup.exe'

& $compiler /nologo /target:winexe /optimize+ /platform:anycpu `
    /reference:System.dll `
    /win32icon:$iconPath /win32manifest:$manifestPath `
    /out:$runtimeOutput $commonSource $runtimeSource
if ($LASTEXITCODE -ne 0) {
    throw 'The reminder runtime failed to compile.'
}

& $compiler /nologo /target:winexe /optimize+ /platform:anycpu `
    /reference:System.dll /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    /win32icon:$iconPath /win32manifest:$manifestPath `
    /out:$setupOutput $commonSource $setupSource
if ($LASTEXITCODE -ne 0) {
    throw 'The setup application failed to compile.'
}

& (Join-Path $repoRoot 'tests\SmokeTests.ps1') -DistDirectory $distDirectory

$packageDirectory = Join-Path $distDirectory 'package'
New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null
Copy-Item -LiteralPath $runtimeOutput, $setupOutput -Destination $packageDirectory
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination $packageDirectory
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination $packageDirectory

$archivePath = Join-Path $distDirectory 'RdpSessionReminder-Windows.zip'
Compress-Archive -Path (Join-Path $packageDirectory '*') `
    -DestinationPath $archivePath -CompressionLevel Optimal
Remove-Item -LiteralPath $packageDirectory -Recurse -Force

$checksumFiles = @($runtimeOutput, $setupOutput, $archivePath)
$checksumLines = foreach ($file in $checksumFiles) {
    $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([IO.Path]::GetFileName($file))"
}
$checksumPath = Join-Path $distDirectory 'SHA256SUMS.txt'
[IO.File]::WriteAllLines($checksumPath, $checksumLines, [Text.UTF8Encoding]::new($false))

[pscustomobject]@{
    Runtime = $runtimeOutput
    Setup = $setupOutput
    Package = $archivePath
    Checksums = $checksumPath
}
