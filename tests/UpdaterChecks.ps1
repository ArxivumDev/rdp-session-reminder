[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$DistDirectory
)

$ErrorActionPreference = 'Stop'
$setupPath = Join-Path ([IO.Path]::GetFullPath($DistDirectory)) `
    'RdpSessionReminderSetup.exe'
$assembly = [Reflection.Assembly]::LoadFile($setupPath)
$allStatic = [Reflection.BindingFlags]'Static,Public,NonPublic'
$allInstance = [Reflection.BindingFlags]'Instance,Public,NonPublic'

$versionType = $assembly.GetType('StableVersion', $true)
$catalogType = $assembly.GetType('UpdateCatalog', $true)
$checksumType = $assembly.GetType('ChecksumFileParser', $true)
$integrityType = $assembly.GetType('UpdateIntegrity', $true)
$preferenceType = $assembly.GetType('UpdatePreferences', $true)
$preferenceStoreType = $assembly.GetType('UpdatePreferenceStore', $true)
$preparedType = $assembly.GetType('PreparedUpdate', $true)
$verifiedLaunchType = $assembly.GetType('VerifiedUpdateLaunch', $true)
$updateClientType = $assembly.GetType('UpdateClient', $true)

$json = @'
[
  {
    "tag_name": "v1.0.1",
    "name": "Patch one",
    "body": "Patch notes must be included.",
    "draft": false,
    "prerelease": false,
    "assets": [
      {"name": "RdpSessionReminder-Installer.exe", "digest": "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"},
      {"name": "SHA256SUMS.txt"}
    ]
  },
  {
    "tag_name": "v2.0.0",
    "name": "Second release",
    "body": "Version two changes.",
    "draft": false,
    "prerelease": false,
    "assets": [
      {"name": "RdpSessionReminder-Installer.exe", "digest": "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"},
      {"name": "SHA256SUMS.txt"}
    ]
  },
  {
    "tag_name": "v3.0.0",
    "name": "Newest release",
    "body": "Version three changes.",
    "draft": false,
    "prerelease": false,
    "assets": [
      {"name": "RdpSessionReminder-Installer.exe", "digest": "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"},
      {"name": "SHA256SUMS.txt"}
    ]
  },
  {
    "tag_name": "v3.1.0",
    "name": "Draft",
    "body": "Do not select.",
    "draft": true,
    "prerelease": false,
    "assets": []
  },
  {
    "tag_name": "v4.0.0-beta.1",
    "name": "Preview",
    "body": "Do not select.",
    "draft": false,
    "prerelease": true,
    "assets": []
  }
]
'@

$parsePage = $catalogType.GetMethod('ParseReleasePage', $allStatic)
$page = $parsePage.Invoke($null, @($json))
$stableReleases = $page.GetType().GetField(
    'StableReleases', $allInstance).GetValue($page)
if ($page.GetType().GetField('RawReleaseCount', $allInstance).GetValue($page) -ne 5 -or
    $stableReleases.Count -ne 3) {
    throw 'Release JSON parsing did not filter draft and prerelease entries correctly.'
}

$current = [Activator]::CreateInstance($versionType, @(1, 0, 0))
$selectUpdate = $catalogType.GetMethod('SelectUpdate', $allStatic)
$result = $selectUpdate.Invoke($null, @($current, $stableReleases))
$latest = $result.GetType().GetField(
    'LatestRelease', $allInstance).GetValue($result)
$latestVersion = $latest.GetType().GetField(
    'Version', $allInstance).GetValue($latest).ToString()
$latestApiDigest = $latest.GetType().GetField(
    'InstallerApiSha256', $allInstance).GetValue($latest)
$newer = $result.GetType().GetField(
    'NewerReleases', $allInstance).GetValue($result)
$updateAvailable = $result.GetType().GetProperty(
    'UpdateAvailable', $allInstance).GetValue($result, $null)
$installerAvailable = $result.GetType().GetProperty(
    'InstallerAvailable', $allInstance).GetValue($result, $null)
$notes = $result.GetType().GetMethod(
    'GetCumulativeReleaseNotes', $allInstance).Invoke($result, @())

if (-not $updateAvailable -or -not $installerAvailable -or
    $latestVersion -ne '3.0.0' -or $newer.Count -ne 3) {
    throw 'Direct latest-version selection failed.'
}
if ($latestApiDigest -ne
    '0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF') {
    throw 'The required GitHub SHA-256 asset digest was not parsed.'
}
$patchIndex = $notes.IndexOf('Patch notes must be included.',
    [StringComparison]::Ordinal)
$twoIndex = $notes.IndexOf('Version two changes.',
    [StringComparison]::Ordinal)
$threeIndex = $notes.IndexOf('Version three changes.',
    [StringComparison]::Ordinal)
if ($patchIndex -lt 0 -or $twoIndex -le $patchIndex -or
    $threeIndex -le $twoIndex) {
    throw 'Cumulative release notes did not include every newer version in order.'
}

$checksumMethod = $checksumType.GetMethod(
    'GetInstallerSha256', $allStatic)
$hash = '0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef'
$checksumText = "$hash  RdpSessionReminder-Installer.exe`n" +
    "$hash  RdpSessionReminder-Windows.zip`n"
$parsedHash = $checksumMethod.Invoke($null, @($checksumText))
if ($parsedHash -ne $hash.ToUpperInvariant()) {
    throw 'The exact installer checksum was not parsed.'
}

$badChecksums = @(
    "$hash RdpSessionReminder-Installer.exe`n",
    "$hash  rdpSessionReminder-Installer.exe`n",
    "$hash  RdpSessionReminder-Installer.exe`n$hash  RdpSessionReminder-Installer.exe`n"
)
foreach ($bad in $badChecksums) {
    try {
        $checksumMethod.Invoke($null, @($bad)) | Out-Null
        throw 'A malformed or duplicate installer checksum was accepted.'
    }
    catch {
        if ($_.Exception.InnerException -isnot [IO.InvalidDataException]) {
            throw
        }
    }
}

$validateIntegrity = $integrityType.GetMethod(
    'ValidateDownloadedInstaller', $allStatic)
$upperHash = $hash.ToUpperInvariant()
$validateIntegrity.Invoke($null, @($hash, $upperHash, $hash)) | Out-Null
foreach ($hashes in @(
    @($hash, ('F' * 64), $hash),
    @($hash, $upperHash, ('E' * 64)),
    @($hash, '', ('D' * 64))
)) {
    try {
        $validateIntegrity.Invoke($null, $hashes) | Out-Null
        throw 'Mismatched update integrity values were accepted.'
    }
    catch {
        if ($_.Exception.InnerException -isnot [IO.InvalidDataException]) {
            throw
        }
    }
}

try {
    $parsePage.Invoke($null, @('[{"tag_name":"v1.0.0","tag_name":"v2.0.0"}]')) |
        Out-Null
    throw 'Duplicate JSON object keys were accepted.'
}
catch {
    if ($_.Exception.InnerException -isnot [IO.InvalidDataException]) {
        throw
    }
}

$invalidDigestJson = @'
[
  {
    "tag_name": "v1.0.0",
    "name": "Bad digest",
    "body": "",
    "draft": false,
    "prerelease": false,
    "assets": [
      {"name": "RdpSessionReminder-Installer.exe", "digest": "sha256:not-a-hash"},
      {"name": "SHA256SUMS.txt"}
    ]
  }
]
'@
$invalidDigestPage = $parsePage.Invoke($null, @($invalidDigestJson))
$invalidDigestReleases = $invalidDigestPage.GetType().GetField(
    'StableReleases', $allInstance).GetValue($invalidDigestPage)
$older = [Activator]::CreateInstance($versionType, @(0, 9, 0))
$invalidDigestResult = $selectUpdate.Invoke(
    $null, @($older, $invalidDigestReleases))
if (-not $invalidDigestResult.GetType().GetProperty(
        'UpdateAvailable', $allInstance).GetValue($invalidDigestResult, $null) -or
    $invalidDigestResult.GetType().GetProperty(
        'InstallerAvailable', $allInstance).GetValue($invalidDigestResult, $null)) {
    throw 'A release with a malformed GitHub digest was enabled for installation.'
}

$preferences = [Activator]::CreateInstance($preferenceType, $true)
$preferenceType.GetField('AutomaticChecks', $allInstance).SetValue(
    $preferences, $true)
$lastCheck = [DateTime]::SpecifyKind(
    [DateTime]::Parse('2026-09-25T12:34:56'), [DateTimeKind]::Utc)
$preferenceType.GetField('LastCheckUtc', $allInstance).SetValue(
    $preferences, $lastCheck)
$serialize = $preferenceStoreType.GetMethod('Serialize', $allStatic)
$parsePreferences = $preferenceStoreType.GetMethod('Parse', $allStatic)
$serialized = $serialize.Invoke($null, @($preferences))
$roundTrip = $parsePreferences.Invoke($null, @($serialized))
if (-not $preferenceType.GetField(
        'AutomaticChecks', $allInstance).GetValue($roundTrip) -or
    $preferenceType.GetField(
        'LastCheckUtc', $allInstance).GetValue($roundTrip) -ne $lastCheck) {
    throw 'Update preferences did not round-trip.'
}

$prepared = [Activator]::CreateInstance($preparedType, $true)
$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) `
    'RdpSessionReminderUpdate-00000000000000000000000000000000'
$preparedType.GetField('TemporaryDirectory', $allInstance).SetValue(
    $prepared, $temporaryDirectory)
$arguments = $preparedType.GetMethod(
    'GetInstallerArguments', $allInstance).Invoke($prepared, @(1234))
if ($arguments -ne '--temporary-update --wait-for-pid 1234') {
    throw 'Updater-to-installer handoff arguments are incomplete.'
}

$safeDirectoryMethod = $updateClientType.GetMethod(
    'IsSafeTemporaryUpdateDirectory', $allStatic)
$safeDirectory = Join-Path ([IO.Path]::GetTempPath()) (
    'RdpSessionReminderUpdate-' + [guid]::NewGuid().ToString('N'))
$nestedDirectory = Join-Path $safeDirectory 'nested'
try {
    [IO.Directory]::CreateDirectory($nestedDirectory) | Out-Null
    [object[]]$safeDirectoryArgument = @([string]$safeDirectory)
    [object[]]$nestedDirectoryArgument = @([string]$nestedDirectory)
    if (-not $safeDirectoryMethod.Invoke($null, $safeDirectoryArgument) -or
        $safeDirectoryMethod.Invoke($null, $nestedDirectoryArgument)) {
        throw 'Temporary updater directory validation accepted an unsafe path.'
    }
}
finally {
    if ([IO.Directory]::Exists($safeDirectory)) {
        [IO.Directory]::Delete($safeDirectory, $true)
    }
}

$openVerifiedInstaller = $updateClientType.GetMethod(
    'OpenVerifiedInstallerForLaunch', $allStatic)
$installerName = 'RdpSessionReminder-Installer.exe'
$lockedDirectory = Join-Path ([IO.Path]::GetTempPath()) (
    'RdpSessionReminderUpdate-' + [guid]::NewGuid().ToString('N'))
$lockedInstaller = Join-Path $lockedDirectory $installerName
$replacementPath = Join-Path $lockedDirectory 'replacement.exe'
$replacementBackupPath = Join-Path $lockedDirectory 'replacement-backup.exe'
$renamedDirectory = $lockedDirectory + '-renamed'
$launchGuard = $null
try {
    [IO.Directory]::CreateDirectory($lockedDirectory) | Out-Null
    $originalBytes = [Text.Encoding]::UTF8.GetBytes(
        'verified installer bytes for launch-lock testing')
    [IO.File]::WriteAllBytes($lockedInstaller, $originalBytes)
    $originalHash = (Get-FileHash -LiteralPath $lockedInstaller `
        -Algorithm SHA256).Hash

    $lockedPrepared = [Activator]::CreateInstance($preparedType, $true)
    $preparedType.GetField('TemporaryDirectory', $allInstance).SetValue(
        $lockedPrepared, $lockedDirectory)
    $preparedType.GetField('InstallerPath', $allInstance).SetValue(
        $lockedPrepared, $lockedInstaller)
    $preparedType.GetField('ExpectedSha256', $allInstance).SetValue(
        $lockedPrepared, $originalHash)
    $preparedType.GetField('ApiSha256', $allInstance).SetValue(
        $lockedPrepared, $originalHash)

    $launchGuard = $openVerifiedInstaller.Invoke($null, @($lockedPrepared))
    if ($null -eq $launchGuard -or
        $launchGuard.GetType() -ne $verifiedLaunchType) {
        throw 'The updater did not return a verified launch guard.'
    }

    $writeWasBlocked = $false
    try {
        [IO.File]::WriteAllText($lockedInstaller, 'tampered')
    }
    catch [IO.IOException] {
        $writeWasBlocked = $true
    }
    catch [UnauthorizedAccessException] {
        $writeWasBlocked = $true
    }
    if (-not $writeWasBlocked) {
        throw 'The verified installer remained writable before launch.'
    }

    [IO.File]::WriteAllText($replacementPath, 'replacement')
    $replacementWasBlocked = $false
    try {
        [IO.File]::Replace(
            $replacementPath, $lockedInstaller, $replacementBackupPath)
    }
    catch [IO.IOException] {
        $replacementWasBlocked = $true
    }
    catch [UnauthorizedAccessException] {
        $replacementWasBlocked = $true
    }
    if (-not $replacementWasBlocked) {
        throw 'The verified installer could be replaced before launch.'
    }

    $directoryRenameWasBlocked = $false
    try {
        [IO.Directory]::Move($lockedDirectory, $renamedDirectory)
    }
    catch [IO.IOException] {
        $directoryRenameWasBlocked = $true
    }
    catch [UnauthorizedAccessException] {
        $directoryRenameWasBlocked = $true
    }
    if (-not $directoryRenameWasBlocked) {
        throw 'The verified installer directory could be replaced before launch.'
    }

    $launchGuard.Dispose()
    $launchGuard = $null
    [IO.File]::WriteAllText($lockedInstaller, 'tampered after verification')
    try {
        $openVerifiedInstaller.Invoke($null, @($lockedPrepared)) | Out-Null
        throw 'Post-verification installer tampering was accepted.'
    }
    catch {
        if ($_.Exception.InnerException -isnot [IO.InvalidDataException]) {
            throw
        }
    }
}
finally {
    if ($null -ne $launchGuard) {
        $launchGuard.Dispose()
    }
    if ([IO.Directory]::Exists($lockedDirectory)) {
        [IO.Directory]::Delete($lockedDirectory, $true)
    }
    if ([IO.Directory]::Exists($renamedDirectory)) {
        [IO.Directory]::Delete($renamedDirectory, $true)
    }
}

$launchTestDirectory = Join-Path ([IO.Path]::GetTempPath()) (
    'RdpSessionReminderUpdate-' + [guid]::NewGuid().ToString('N'))
$launchTestInstaller = Join-Path $launchTestDirectory $installerName
$launchTestGuard = $null
try {
    [IO.Directory]::CreateDirectory($launchTestDirectory) | Out-Null
    Copy-Item -LiteralPath (Join-Path ([IO.Path]::GetFullPath($DistDirectory)) `
        $installerName) -Destination $launchTestInstaller
    $launchTestHash = (Get-FileHash -LiteralPath $launchTestInstaller `
        -Algorithm SHA256).Hash
    $launchPrepared = [Activator]::CreateInstance($preparedType, $true)
    $preparedType.GetField('TemporaryDirectory', $allInstance).SetValue(
        $launchPrepared, $launchTestDirectory)
    $preparedType.GetField('InstallerPath', $allInstance).SetValue(
        $launchPrepared, $launchTestInstaller)
    $preparedType.GetField('ExpectedSha256', $allInstance).SetValue(
        $launchPrepared, $launchTestHash)
    $preparedType.GetField('ApiSha256', $allInstance).SetValue(
        $launchPrepared, $launchTestHash)
    $launchTestGuard = $openVerifiedInstaller.Invoke($null, @($launchPrepared))

    $processInfo = [Diagnostics.ProcessStartInfo]::new()
    $processInfo.FileName = $launchTestInstaller
    $processInfo.Arguments = '--self-test'
    $processInfo.WorkingDirectory = $launchTestDirectory
    $processInfo.UseShellExecute = $false
    $testProcess = [Diagnostics.Process]::Start($processInfo)
    try {
        if (-not $testProcess.WaitForExit(30000) -or $testProcess.ExitCode -ne 0) {
            throw 'Windows could not launch the installer while its verified handles were held.'
        }
    }
    finally {
        $testProcess.Dispose()
    }
}
finally {
    if ($null -ne $launchTestGuard) {
        $launchTestGuard.Dispose()
    }
    if ([IO.Directory]::Exists($launchTestDirectory)) {
        [IO.Directory]::Delete($launchTestDirectory, $true)
    }
}

$reparseDirectory = Join-Path ([IO.Path]::GetTempPath()) (
    'RdpSessionReminderUpdate-' + [guid]::NewGuid().ToString('N'))
$reparseInstaller = Join-Path $reparseDirectory $installerName
$reparseTarget = Join-Path ([IO.Path]::GetTempPath()) (
    'RdpSessionReminderUpdateTarget-' + [guid]::NewGuid().ToString('N'))
$reparseIsDirectory = $false
try {
    [IO.Directory]::CreateDirectory($reparseDirectory) | Out-Null
    [IO.File]::WriteAllText($reparseTarget, 'reparse target')
    try {
        New-Item -ItemType SymbolicLink -Path $reparseInstaller `
            -Target $reparseTarget -ErrorAction Stop | Out-Null
    }
    catch {
        if ([IO.File]::Exists($reparseTarget)) {
            [IO.File]::Delete($reparseTarget)
        }
        [IO.Directory]::CreateDirectory($reparseTarget) | Out-Null
        New-Item -ItemType Junction -Path $reparseInstaller `
            -Target $reparseTarget -ErrorAction Stop | Out-Null
        $reparseIsDirectory = $true
    }
    $attributes = [IO.File]::GetAttributes($reparseInstaller)
    if (($attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0) {
        throw 'The updater reparse test did not create a reparse point.'
    }

    $reparsePrepared = [Activator]::CreateInstance($preparedType, $true)
    $preparedType.GetField('TemporaryDirectory', $allInstance).SetValue(
        $reparsePrepared, $reparseDirectory)
    $preparedType.GetField('InstallerPath', $allInstance).SetValue(
        $reparsePrepared, $reparseInstaller)
    $preparedType.GetField('ExpectedSha256', $allInstance).SetValue(
        $reparsePrepared, $upperHash)
    $preparedType.GetField('ApiSha256', $allInstance).SetValue(
        $reparsePrepared, $upperHash)
    try {
        $openVerifiedInstaller.Invoke($null, @($reparsePrepared)) | Out-Null
        throw 'An installer-path reparse point was accepted for launch.'
    }
    catch {
        if ($_.Exception.InnerException -isnot [IO.InvalidDataException]) {
            throw
        }
    }
}
finally {
    if ($reparseIsDirectory -and [IO.Directory]::Exists($reparseInstaller)) {
        [IO.Directory]::Delete($reparseInstaller)
    }
    elseif ([IO.File]::Exists($reparseInstaller)) {
        [IO.File]::Delete($reparseInstaller)
    }
    if ([IO.Directory]::Exists($reparseDirectory)) {
        [IO.Directory]::Delete($reparseDirectory, $true)
    }
    if ([IO.Directory]::Exists($reparseTarget)) {
        [IO.Directory]::Delete($reparseTarget, $true)
    }
    elseif ([IO.File]::Exists($reparseTarget)) {
        [IO.File]::Delete($reparseTarget)
    }
}

Write-Host 'Updater checks passed.'
