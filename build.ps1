[CmdletBinding()]
param(
    [string]$SigningCertificateThumbprint =
        $env:RDP_REMINDER_SIGN_CERT_SHA1,
    [string]$SigningTimestampUrl =
        $env:RDP_REMINDER_SIGN_TIMESTAMP_URL
)

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
$updateSupportSource = Join-Path $sourceDirectory 'UpdateSupport.cs'
$updateUiSource = Join-Path $sourceDirectory 'UpdateUi.cs'
$advancedSetupSource = Join-Path $sourceDirectory 'AdvancedSetupSupport.cs'
$bannerSetupSource = Join-Path $sourceDirectory 'BannerSetupUi.cs'
$connectionManagerSource = Join-Path $sourceDirectory 'ConnectionManagerUi.cs'
$profileEditorSource = Join-Path $sourceDirectory 'ManagedProfileEditorForm.cs'
$signatureStatusSource = Join-Path $sourceDirectory 'ExecutableSignatureStatus.cs'
$setupVisualsSource = Join-Path $sourceDirectory 'SetupVisuals.cs'
$installerSource = Join-Path $sourceDirectory 'RdpSessionReminderInstaller.cs'
$uninstallerSource = Join-Path $sourceDirectory 'RdpSessionReminderUninstaller.cs'
$iconPath = Join-Path $assetDirectory 'RdpSessionReminder.ico'
$manifestPath = Join-Path $assetDirectory 'app.manifest'
$runtimeOutput = Join-Path $distDirectory 'RdpSessionReminder.exe'
$setupOutput = Join-Path $distDirectory 'RdpSessionReminderSetup.exe'
$installerOutput = Join-Path $distDirectory 'RdpSessionReminder-Installer.exe'
$uninstallerOutput = Join-Path $distDirectory 'RdpSessionReminder-Uninstaller.exe'

$signingRequested =
    -not [string]::IsNullOrWhiteSpace($SigningCertificateThumbprint) -or
    -not [string]::IsNullOrWhiteSpace($SigningTimestampUrl)
if ($signingRequested -and
    ([string]::IsNullOrWhiteSpace($SigningCertificateThumbprint) -or
     [string]::IsNullOrWhiteSpace($SigningTimestampUrl))) {
    throw 'Authenticode signing requires both a certificate thumbprint and an HTTPS RFC 3161 timestamp URL.'
}
if ($signingRequested) {
    . (Join-Path $repoRoot 'tools\Authenticode.ps1')
}

function Invoke-OptionalSigning {
    param([Parameter(Mandatory)][string[]]$Path)

    if (-not $signingRequested) {
        return
    }
    Invoke-RdpReminderAuthenticodeSigning -Path $Path `
        -CertificateThumbprint $SigningCertificateThumbprint `
        -TimestampUrl $SigningTimestampUrl
}

& $compiler /nologo /target:winexe /optimize+ /platform:anycpu `
    /reference:System.dll `
    /win32icon:$iconPath /win32manifest:$manifestPath `
    /out:$runtimeOutput $commonSource $runtimeSource
if ($LASTEXITCODE -ne 0) {
    throw 'The reminder runtime failed to compile.'
}
Invoke-OptionalSigning -Path $runtimeOutput

& $compiler /nologo /target:winexe /optimize+ /platform:anycpu `
    /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    /win32icon:$iconPath /win32manifest:$manifestPath `
    /out:$setupOutput $commonSource $updateSupportSource $updateUiSource `
    $advancedSetupSource $bannerSetupSource $connectionManagerSource `
    $profileEditorSource $signatureStatusSource $setupVisualsSource $setupSource
if ($LASTEXITCODE -ne 0) {
    throw 'The setup application failed to compile.'
}
Invoke-OptionalSigning -Path $setupOutput

& $compiler /nologo /target:winexe /optimize+ /platform:anycpu `
    /reference:System.dll /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    /win32icon:$iconPath /win32manifest:$manifestPath `
    /out:$uninstallerOutput $uninstallerSource
if ($LASTEXITCODE -ne 0) {
    throw 'The uninstaller failed to compile.'
}
Invoke-OptionalSigning -Path $uninstallerOutput

$runtimeResourceArgument = "/resource:$runtimeOutput,RdpSessionReminder.Payload.Runtime"
$setupResourceArgument = "/resource:$setupOutput,RdpSessionReminder.Payload.Setup"
$uninstallerResourceArgument =
    "/resource:$uninstallerOutput,RdpSessionReminder.Payload.Uninstaller"

& $compiler /nologo /target:winexe /optimize+ /platform:anycpu `
    /reference:System.dll /reference:System.Windows.Forms.dll `
    /win32icon:$iconPath /win32manifest:$manifestPath `
    $runtimeResourceArgument $setupResourceArgument $uninstallerResourceArgument `
    /out:$installerOutput $installerSource
if ($LASTEXITCODE -ne 0) {
    throw 'The installer failed to compile.'
}
Invoke-OptionalSigning -Path $installerOutput

& (Join-Path $repoRoot 'tests\SmokeTests.ps1') -DistDirectory $distDirectory
& (Join-Path $repoRoot 'tests\RuntimeCustomizationChecks.ps1') `
    -DistDirectory $distDirectory
& (Join-Path $repoRoot 'tests\AdvancedSetupChecks.ps1') -RepoRoot $repoRoot
& (Join-Path $repoRoot 'tests\ConnectionManagerChecks.ps1') -RepoRoot $repoRoot
& (Join-Path $repoRoot 'tests\ReflectionChecks.ps1') -DistDirectory $distDirectory
& (Join-Path $repoRoot 'tests\ThemeChecks.ps1') -DistDirectory $distDirectory
& (Join-Path $repoRoot 'tests\UpdaterChecks.ps1') -DistDirectory $distDirectory
& (Join-Path $repoRoot 'tests\InstallerChecks.ps1') `
    -RepoRoot $repoRoot -DistDirectory $distDirectory
& (Join-Path $repoRoot 'tests\AuthenticodeChecks.ps1') `
    -RepoRoot $repoRoot

$packageDirectory = Join-Path $distDirectory 'package'
New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null
Copy-Item -LiteralPath $runtimeOutput, $setupOutput -Destination $packageDirectory
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination $packageDirectory
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination $packageDirectory

$archivePath = Join-Path $distDirectory 'RdpSessionReminder-Windows.zip'
Compress-Archive -Path (Join-Path $packageDirectory '*') `
    -DestinationPath $archivePath -CompressionLevel Optimal
Remove-Item -LiteralPath $packageDirectory -Recurse -Force

$checksumFiles = @(
    $runtimeOutput,
    $setupOutput,
    $installerOutput,
    $uninstallerOutput,
    $archivePath
)
$checksumLines = foreach ($file in $checksumFiles) {
    $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([IO.Path]::GetFileName($file))"
}
$checksumPath = Join-Path $distDirectory 'SHA256SUMS.txt'
[IO.File]::WriteAllLines($checksumPath, $checksumLines, [Text.UTF8Encoding]::new($false))

& (Join-Path $repoRoot 'tests\PackageChecks.ps1') `
    -RepoRoot $repoRoot -DistDirectory $distDirectory

[pscustomobject]@{
    Runtime = $runtimeOutput
    Setup = $setupOutput
    Installer = $installerOutput
    Uninstaller = $uninstallerOutput
    Package = $archivePath
    Checksums = $checksumPath
}
