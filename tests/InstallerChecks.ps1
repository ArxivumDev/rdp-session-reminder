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
$installer = Join-Path $dist 'RdpSessionReminder-Installer.exe'
$uninstaller = Join-Path $dist 'RdpSessionReminder-Uninstaller.exe'

foreach ($executable in @($installer, $uninstaller)) {
    if (-not [IO.File]::Exists($executable)) {
        throw "Missing installation package: $executable"
    }
    $process = Start-Process -FilePath $executable -ArgumentList '--self-test' `
        -PassThru -Wait -WindowStyle Hidden
    if ($process.ExitCode -ne 0) {
        throw "Self-test failed for $([IO.Path]::GetFileName($executable)) with exit code $($process.ExitCode)."
    }
    $version = (Get-Item -LiteralPath $executable).VersionInfo.FileVersion
    if ($version -ne '1.1.0.0') {
        throw "Unexpected file version for $([IO.Path]::GetFileName($executable)): $version"
    }
}

$uninstallerAssembly = [Reflection.Assembly]::LoadFile($uninstaller)
$layoutType = $uninstallerAssembly.GetType('UninstallLayout', $true)
$formType = $uninstallerAssembly.GetType('UninstallOptionsForm', $true)
$allStatic = [Reflection.BindingFlags]'Static,Public,NonPublic'
$layout = $layoutType.GetMethod(
    'CreateForCurrentUser', $allStatic).Invoke($null, @())
$form = [Activator]::CreateInstance($formType, @($layout))
try {
    $fitWindow = $formType.GetMethod(
        'LimitWindowSizeToWorkingArea', $allStatic)
    [object[]]$fitArguments = @(
        [Drawing.Size]::new(900, 900),
        [Drawing.Rectangle]::new(0, 0, 800, 600)
    )
    $fittedSize = $fitWindow.Invoke($null, $fitArguments)
    if (-not $form.AutoScroll -or $fittedSize.Width -ne 800 -or
        $fittedSize.Height -ne 600) {
        throw 'The uninstaller window cannot stay reachable on a short work area.'
    }
}
finally {
    $form.Dispose()
}

$source = [IO.File]::ReadAllText(
    (Join-Path $repo 'src\RdpSessionReminderUninstaller.cs'))
foreach ($requiredText in @(
    'README - Saved RDP Connections.txt',
    '--remove-publisher-trust',
    'connection.rdp',
    'settings.ini',
    'Generated and imported profile copies under:',
    'Every connection.rdp and settings.ini file in those profiles',
    'Original imported .rdp files were never changed or deleted.',
    'Desktop reminder shortcuts; copies moved outside the Desktop folder',
    'The app takes extra care to remove and verify only the exact app-owned',
    'uninstall stops before deleting app files so you can retry.',
    'does not search other folders or drives',
    'reinstalled before they work again.',
    'with Windows Remote Desktop.',
    'you can manually delete the entire folder below'
)) {
    if (-not $source.Contains($requiredText)) {
        throw "Uninstaller preservation notice is missing: $requiredText"
    }
}

if ($source -match '(?i)Directory\.(EnumerateFiles|EnumerateDirectories|GetFileSystemEntries|EnumerateFileSystemEntries)' -or
    $source -match '(?i)\bDirectoryInfo\b' -or
    $source -match '(?i)SearchOption\.AllDirectories' -or
    $source -match '(?i)\bDriveInfo\b|Environment\.GetLogicalDrives') {
    throw 'The uninstaller must not recursively enumerate folders or scan drives.'
}

$boundedEnumerationCalls = [regex]::Matches(
    $source, '(?i)Directory\.(GetFiles|GetDirectories)\(')
if ($boundedEnumerationCalls.Count -ne 2 -or
    $source -notmatch 'Directory\.GetDirectories\(layout\.ProfilesDirectory, "\*",\s*SearchOption\.TopDirectoryOnly\)' -or
    $source -notmatch 'Directory\.GetFiles\(layout\.DesktopDirectory, "\*\.lnk",\s*SearchOption\.TopDirectoryOnly\)') {
    throw 'Destructive uninstall enumeration must stay bounded to the app profiles root and current Desktop folder.'
}

$uninstallStart = $source.IndexOf('public static void Uninstall(')
$uninstallEnd = $source.IndexOf(
    'private static void RemoveSavedConnections(', $uninstallStart)
if ($uninstallStart -lt 0 -or $uninstallEnd -le $uninstallStart) {
    throw 'Could not inspect the uninstall operation order.'
}
$uninstallBody = $source.Substring(
    $uninstallStart, $uninstallEnd - $uninstallStart)
$trustIndex = $uninstallBody.IndexOf('RemovePublisherTrust(layout);')
$runtimeIndex = $uninstallBody.IndexOf(
    'DeleteFileIfPresent(layout.RuntimePath);')
$startMenuIndex = $uninstallBody.IndexOf(
    'DeleteFileIfPresent(layout.SetupShortcutPath);')
$registrationIndex = $uninstallBody.IndexOf(
    'parent.DeleteSubKeyTree("RdpSessionReminder", false);')
$savedRemovalIndex = $uninstallBody.IndexOf('RemoveSavedConnections(layout);')
$noteIndex = $uninstallBody.IndexOf('WriteSavedConnectionsNote(layout);')
if ($trustIndex -lt 0 -or $runtimeIndex -le $trustIndex -or
    $startMenuIndex -le $runtimeIndex -or
    $registrationIndex -le $startMenuIndex -or
    $savedRemovalIndex -le $registrationIndex -or
    $noteIndex -le $registrationIndex) {
    throw 'Publisher trust and app-owned files must be removed before saved-profile deletion or preservation-note creation.'
}

Write-Host 'Installer and uninstaller checks passed.'
