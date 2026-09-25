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
    if ($version -ne '1.2.0.0') {
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

$engineType = $uninstallerAssembly.GetType('UninstallerEngine', $true)
$uninstallMethod = $engineType.GetMethod('Uninstall', $allStatic)
$layoutConstructor = $layoutType.GetConstructor(
    [Reflection.BindingFlags]'Instance,Public,NonPublic', $null,
    [type[]]@([string], [string], [string]), $null)
if (-not $uninstallMethod -or -not $layoutConstructor) {
    throw 'Could not load the isolated uninstaller test entry points.'
}

function New-UninstallTestLayout {
    param(
        [Parameter(Mandatory)][string]$InstallDirectory,
        [Parameter(Mandatory)][string]$StartMenuDirectory,
        [Parameter(Mandatory)][string]$DesktopDirectory
    )
    return $layoutConstructor.Invoke(@(
        [string]$InstallDirectory,
        [string]$StartMenuDirectory,
        [string]$DesktopDirectory
    ))
}

function Invoke-IsolatedUninstall {
    param(
        [Parameter(Mandatory)][object]$Layout,
        [Parameter(Mandatory)][bool]$RemoveSavedConnections
    )
    try {
        $uninstallMethod.Invoke($null, @(
            $Layout,
            [bool]$false,
            [bool]$RemoveSavedConnections
        )) | Out-Null
        return $null
    }
    catch {
        return $_.Exception
    }
}

$isolationRoot = Join-Path ([IO.Path]::GetTempPath()) (
    'RdpReminder-Uninstaller-Isolated-' + [guid]::NewGuid().ToString('N'))
$junctions = [Collections.Generic.List[string]]::new()
try {
    [IO.Directory]::CreateDirectory($isolationRoot) | Out-Null

    # An install-root junction must be rejected before publisher cleanup can
    # launch or any app-owned file can be changed.
    $installLinkCase = Join-Path $isolationRoot 'install-root-link'
    $installTarget = Join-Path $installLinkCase 'target'
    $installLink = Join-Path $installLinkCase 'app'
    $installStartMenu = Join-Path $installLinkCase 'start-menu'
    $installDesktop = Join-Path $installLinkCase 'desktop'
    [IO.Directory]::CreateDirectory($installTarget) | Out-Null
    [IO.Directory]::CreateDirectory($installStartMenu) | Out-Null
    [IO.Directory]::CreateDirectory($installDesktop) | Out-Null
    $installSentinel = Join-Path $installTarget 'sentinel.bin'
    $installRuntime = Join-Path $installTarget 'RdpSessionReminder.exe'
    [IO.File]::WriteAllBytes($installSentinel, [byte[]](11, 22, 33, 44))
    [IO.File]::WriteAllText($installRuntime, 'runtime-must-remain')
    New-Item -ItemType Junction -Path $installLink -Target $installTarget |
        Out-Null
    $junctions.Add($installLink)
    $installLinkLayout = New-UninstallTestLayout -InstallDirectory $installLink `
        -StartMenuDirectory $installStartMenu -DesktopDirectory $installDesktop
    $installLinkError = Invoke-IsolatedUninstall -Layout $installLinkLayout `
        -RemoveSavedConnections $true
    if (-not $installLinkError -or
        -not [IO.File]::Exists($installSentinel) -or
        -not [IO.File]::Exists($installRuntime)) {
        throw 'The uninstaller mutated or accepted a junction install root.'
    }

    # A Start-menu junction is also rejected before the normal install root is
    # touched, even when the app binary is otherwise removable.
    $menuLinkCase = Join-Path $isolationRoot 'start-menu-root-link'
    $menuInstall = Join-Path $menuLinkCase 'app'
    $menuTarget = Join-Path $menuLinkCase 'target'
    $menuLink = Join-Path $menuLinkCase 'start-menu'
    $menuDesktop = Join-Path $menuLinkCase 'desktop'
    [IO.Directory]::CreateDirectory($menuInstall) | Out-Null
    [IO.Directory]::CreateDirectory($menuTarget) | Out-Null
    [IO.Directory]::CreateDirectory($menuDesktop) | Out-Null
    $menuRuntime = Join-Path $menuInstall 'RdpSessionReminder.exe'
    $menuSentinel = Join-Path $menuTarget 'sentinel.bin'
    [IO.File]::WriteAllText($menuRuntime, 'runtime-must-remain')
    [IO.File]::WriteAllBytes($menuSentinel, [byte[]](55, 66, 77))
    New-Item -ItemType Junction -Path $menuLink -Target $menuTarget | Out-Null
    $junctions.Add($menuLink)
    $menuLinkLayout = New-UninstallTestLayout -InstallDirectory $menuInstall `
        -StartMenuDirectory $menuLink -DesktopDirectory $menuDesktop
    $menuLinkError = Invoke-IsolatedUninstall -Layout $menuLinkLayout `
        -RemoveSavedConnections $false
    if (-not $menuLinkError -or
        -not [IO.File]::Exists($menuRuntime) -or
        -not [IO.File]::Exists($menuSentinel)) {
        throw 'The uninstaller mutated or accepted a junction Start-menu root.'
    }

    # Managed child roots that have become junctions are preserved instead of
    # followed. Normal app files can still be uninstalled around them.
    $childLinkCase = Join-Path $isolationRoot 'managed-child-links'
    $childInstall = Join-Path $childLinkCase 'app'
    $childStartMenu = Join-Path $childLinkCase 'start-menu'
    $childDesktop = Join-Path $childLinkCase 'desktop'
    $profileTarget = Join-Path $childLinkCase 'profile-target'
    $iconTarget = Join-Path $childLinkCase 'icon-target'
    $oneTimeTarget = Join-Path $childLinkCase 'one-time-target'
    [IO.Directory]::CreateDirectory($childInstall) | Out-Null
    [IO.Directory]::CreateDirectory($childStartMenu) | Out-Null
    [IO.Directory]::CreateDirectory($childDesktop) | Out-Null
    [IO.Directory]::CreateDirectory($profileTarget) | Out-Null
    [IO.Directory]::CreateDirectory($iconTarget) | Out-Null
    [IO.Directory]::CreateDirectory($oneTimeTarget) | Out-Null
    $childRuntime = Join-Path $childInstall 'RdpSessionReminder.exe'
    [IO.File]::WriteAllText($childRuntime, 'remove-runtime')
    $iconSentinel = Join-Path $iconTarget 'reminder-v1.ico'
    [IO.File]::WriteAllBytes($iconSentinel, [byte[]](1, 3, 5, 7, 9))
    $profileId = [guid]::NewGuid().ToString('N')
    $linkedProfile = Join-Path $profileTarget $profileId
    [IO.Directory]::CreateDirectory($linkedProfile) | Out-Null
    $profileSentinel = Join-Path $linkedProfile 'settings.ini'
    [IO.File]::WriteAllBytes($profileSentinel, [byte[]](2, 4, 6, 8))
    $oneTimeId = [guid]::NewGuid().ToString('N')
    $oneTimeProfile = Join-Path $oneTimeTarget $oneTimeId
    [IO.Directory]::CreateDirectory($oneTimeProfile) | Out-Null
    $oneTimeSentinel = Join-Path $oneTimeProfile 'connection.rdp'
    [IO.File]::WriteAllBytes(
        $oneTimeSentinel, [byte[]](0, 255, 13, 10, 128, 0))
    $profileLink = Join-Path $childInstall 'profiles'
    $iconLink = Join-Path $childInstall 'icons'
    $oneTimeLink = Join-Path $childInstall 'one-time'
    New-Item -ItemType Junction -Path $profileLink -Target $profileTarget |
        Out-Null
    New-Item -ItemType Junction -Path $iconLink -Target $iconTarget | Out-Null
    New-Item -ItemType Junction -Path $oneTimeLink -Target $oneTimeTarget |
        Out-Null
    $junctions.Add($profileLink)
    $junctions.Add($iconLink)
    $junctions.Add($oneTimeLink)
    $childLinkLayout = New-UninstallTestLayout `
        -InstallDirectory $childInstall -StartMenuDirectory $childStartMenu `
        -DesktopDirectory $childDesktop
    $childLinkError = Invoke-IsolatedUninstall -Layout $childLinkLayout `
        -RemoveSavedConnections $true
    if ($childLinkError -or [IO.File]::Exists($childRuntime) -or
        -not [IO.File]::Exists($profileSentinel) -or
        -not [IO.File]::Exists($iconSentinel) -or
        -not [IO.File]::Exists($oneTimeSentinel) -or
        -not [IO.Directory]::Exists($profileLink) -or
        -not [IO.Directory]::Exists($iconLink) -or
        -not [IO.Directory]::Exists($oneTimeLink)) {
        throw 'Managed junction content was followed or removed during uninstall.'
    }
}
finally {
    for ($index = $junctions.Count - 1; $index -ge 0; $index--) {
        $junction = $junctions[$index]
        if ([IO.Directory]::Exists($junction)) {
            [IO.Directory]::Delete($junction, $false)
        }
    }
    $fullIsolationRoot = [IO.Path]::GetFullPath($isolationRoot)
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if ($fullIsolationRoot.StartsWith(
            $temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Directory]::Exists($fullIsolationRoot)) {
        [IO.Directory]::Delete($fullIsolationRoot, $true)
    }
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
    'reminder-v1.ico',
    'reminder-v2.ico',
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
if ($boundedEnumerationCalls.Count -ne 3 -or
    $source -notmatch 'Directory\.GetDirectories\(layout\.ProfilesDirectory, "\*",\s*SearchOption\.TopDirectoryOnly\)' -or
    $source -notmatch 'Directory\.GetDirectories\(\s*layout\.OneTimeProfilesDirectory, "\*", SearchOption\.TopDirectoryOnly\)' -or
    $source -notmatch 'Directory\.GetFiles\(layout\.DesktopDirectory, "\*\.lnk",\s*SearchOption\.TopDirectoryOnly\)') {
    throw 'Destructive uninstall enumeration must stay bounded to the two app profile roots and current Desktop folder.'
}

$uninstallStart = $source.IndexOf('public static void Uninstall(')
$uninstallEnd = $source.IndexOf(
    'private static void RemoveSavedConnections(', $uninstallStart)
if ($uninstallStart -lt 0 -or $uninstallEnd -le $uninstallStart) {
    throw 'Could not inspect the uninstall operation order.'
}
$uninstallBody = $source.Substring(
    $uninstallStart, $uninstallEnd - $uninstallStart)
$validationIndex = $uninstallBody.IndexOf('ValidateUninstallRoots(layout);')
$trustIndex = $uninstallBody.IndexOf('RemovePublisherTrust(layout);')
$runtimeIndex = $uninstallBody.IndexOf(
    'DeleteFileIfPresent(layout.RuntimePath);')
$startMenuIndex = $uninstallBody.IndexOf(
    'DeleteFileIfPresent(layout.SetupShortcutPath);')
$registrationIndex = $uninstallBody.IndexOf(
    'parent.DeleteSubKeyTree("RdpSessionReminder", false);')
$savedRemovalIndex = $uninstallBody.IndexOf('RemoveSavedConnections(layout);')
$noteIndex = $uninstallBody.IndexOf('WriteSavedConnectionsNote(layout);')
if ($validationIndex -lt 0 -or $trustIndex -le $validationIndex -or
    $runtimeIndex -le $trustIndex -or
    $startMenuIndex -le $runtimeIndex -or
    $registrationIndex -le $startMenuIndex -or
    $savedRemovalIndex -le $registrationIndex -or
    $noteIndex -le $registrationIndex) {
    throw 'Publisher trust and app-owned files must be removed before saved-profile deletion or preservation-note creation.'
}

Write-Host 'Installer and uninstaller checks passed.'
