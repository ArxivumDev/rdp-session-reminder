[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$DistDirectory
)

$ErrorActionPreference = 'Stop'
$resolvedDist = [IO.Path]::GetFullPath($DistDirectory)
$executables = @(
    (Join-Path $resolvedDist 'RdpSessionReminder.exe'),
    (Join-Path $resolvedDist 'RdpSessionReminderSetup.exe')
)

foreach ($executable in $executables) {
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
        throw "Missing build output: $executable"
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

function Assert-ReparseRootRejected {
    param(
        [Parameter(Mandatory)]
        [scriptblock]$Action,

        [Parameter(Mandatory)]
        [string]$Context
    )

    $caught = $null
    try {
        & $Action
    }
    catch {
        $caught = $_.Exception
    }

    if ($null -eq $caught) {
        throw "$Context unexpectedly accepted a reparse-point root."
    }

    while ($caught -isnot [IO.IOException] -and
        $null -ne $caught.InnerException) {
        $caught = $caught.InnerException
    }
    if ($caught -isnot [IO.IOException] -or
        $caught.Message -notmatch 'reparse point|directory link') {
        throw "$Context failed with the wrong exception: $caught"
    }
}

function Assert-BytesEqual {
    param(
        [Parameter(Mandatory)]
        [byte[]]$Expected,

        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Context
    )

    if (-not [IO.File]::Exists($Path)) {
        throw "$Context sentinel was deleted: $Path"
    }
    $actual = [IO.File]::ReadAllBytes($Path)
    if ($actual.Length -ne $Expected.Length) {
        throw "$Context sentinel length changed: $Path"
    }
    for ($index = 0; $index -lt $Expected.Length; $index++) {
        if ($actual[$index] -ne $Expected[$index]) {
            throw "$Context sentinel contents changed: $Path"
        }
    }
}

$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) `
    ("RdpSessionReminder-root-safety-" + [Guid]::NewGuid().ToString('N'))
$junctions = New-Object 'System.Collections.Generic.List[string]'
[IO.Directory]::CreateDirectory($fixtureRoot) | Out-Null
try {
    $installerAssembly = [Reflection.Assembly]::Load(
        [IO.File]::ReadAllBytes(
            (Join-Path $resolvedDist 'RdpSessionReminder-Installer.exe')))
    $layoutType = $installerAssembly.GetType('InstallLayout', $true)
    $layoutConstructor = $layoutType.GetConstructors(
        [Reflection.BindingFlags]'Instance, Public, NonPublic') |
        Where-Object { $_.GetParameters().Length -eq 2 } |
        Select-Object -First 1
    if ($null -eq $layoutConstructor) {
        throw 'Installer layout constructor was not found.'
    }
    $installMethod = $installerAssembly.GetType(
        'InstallerEngine', $true).GetMethod(
            'Install', [Reflection.BindingFlags]'Public, Static')

    $installTarget = Join-Path $fixtureRoot 'installer-target'
    $installLink = Join-Path $fixtureRoot 'installer-link'
    $installStartMenu = Join-Path $fixtureRoot 'installer-start-menu'
    [IO.Directory]::CreateDirectory($installTarget) | Out-Null
    [byte[]]$installerSentinel = 17, 29, 43, 71
    $installerSentinelPath = Join-Path $installTarget `
        'RdpSessionReminder.exe'
    [IO.File]::WriteAllBytes($installerSentinelPath, $installerSentinel)
    New-Item -ItemType Junction -Path $installLink `
        -Target $installTarget | Out-Null
    $junctions.Add($installLink)
    $installLayoutArguments = New-Object 'object[]' 2
    $installLayoutArguments[0] = [string]$installLink
    $installLayoutArguments[1] = [string]$installStartMenu
    $installLayout = $layoutConstructor.Invoke($installLayoutArguments)
    Assert-ReparseRootRejected -Context 'Installer application folder' `
        -Action {
            $installArguments = New-Object 'object[]' 2
            $installArguments[0] = $installLayout
            $installArguments[1] = $false
            $installMethod.Invoke($null, $installArguments) | Out-Null
        }
    Assert-BytesEqual -Expected $installerSentinel `
        -Path $installerSentinelPath `
        -Context 'Installer application folder'
    if ([IO.Directory]::GetFileSystemEntries($installTarget).Length -ne 1 -or
        [IO.Directory]::Exists($installStartMenu)) {
        throw 'Installer application-folder rejection wrote outside its fixture.'
    }

    $menuTarget = Join-Path $fixtureRoot 'start-menu-target'
    $menuLink = Join-Path $fixtureRoot 'start-menu-link'
    $menuInstall = Join-Path $fixtureRoot 'start-menu-install'
    [IO.Directory]::CreateDirectory($menuTarget) | Out-Null
    [byte[]]$menuSentinel = 83, 97, 102, 101
    $menuSentinelPath = Join-Path $menuTarget `
        'RDP Session Reminder Setup.lnk'
    [IO.File]::WriteAllBytes($menuSentinelPath, $menuSentinel)
    New-Item -ItemType Junction -Path $menuLink -Target $menuTarget | Out-Null
    $junctions.Add($menuLink)
    $menuLayoutArguments = New-Object 'object[]' 2
    $menuLayoutArguments[0] = [string]$menuInstall
    $menuLayoutArguments[1] = [string]$menuLink
    $menuLayout = $layoutConstructor.Invoke($menuLayoutArguments)
    Assert-ReparseRootRejected -Context 'Installer Start menu folder' `
        -Action {
            $menuInstallArguments = New-Object 'object[]' 2
            $menuInstallArguments[0] = $menuLayout
            $menuInstallArguments[1] = $false
            $installMethod.Invoke(
                $null, $menuInstallArguments) | Out-Null
        }
    Assert-BytesEqual -Expected $menuSentinel -Path $menuSentinelPath `
        -Context 'Installer Start menu folder'
    if ([IO.Directory]::GetFileSystemEntries($menuTarget).Length -ne 1 -or
        [IO.Directory]::Exists($menuInstall)) {
        throw 'Installer Start-menu rejection wrote outside its fixture.'
    }

    # Reinstalling must replace app-owned directory entries instead of writing
    # through a hard link to an unrelated file.
    $hardLinkCase = Join-Path $fixtureRoot 'installer-child-hard-links'
    $hardLinkInstall = Join-Path $hardLinkCase 'app'
    $hardLinkMenu = Join-Path $hardLinkCase 'start-menu'
    $hardLinkOutside = Join-Path $hardLinkCase 'outside'
    [IO.Directory]::CreateDirectory($hardLinkInstall) | Out-Null
    [IO.Directory]::CreateDirectory($hardLinkMenu) | Out-Null
    [IO.Directory]::CreateDirectory($hardLinkOutside) | Out-Null
    [byte[]]$outsideRuntimeBytes = 151, 157, 163, 167
    [byte[]]$outsideShortcutBytes = 173, 179, 181, 191
    $outsideRuntime = Join-Path $hardLinkOutside 'unrelated-runtime.bin'
    $outsideShortcut = Join-Path $hardLinkOutside 'unrelated-shortcut.bin'
    [IO.File]::WriteAllBytes($outsideRuntime, $outsideRuntimeBytes)
    [IO.File]::WriteAllBytes($outsideShortcut, $outsideShortcutBytes)
    $linkedRuntime = Join-Path $hardLinkInstall 'RdpSessionReminder.exe'
    $linkedShortcut = Join-Path $hardLinkMenu `
        'RDP Session Reminder Setup.lnk'
    New-Item -ItemType HardLink -Path $linkedRuntime `
        -Target $outsideRuntime | Out-Null
    New-Item -ItemType HardLink -Path $linkedShortcut `
        -Target $outsideShortcut | Out-Null
    $hardLinkLayoutArguments = New-Object 'object[]' 2
    $hardLinkLayoutArguments[0] = [string]$hardLinkInstall
    $hardLinkLayoutArguments[1] = [string]$hardLinkMenu
    $hardLinkLayout = $layoutConstructor.Invoke($hardLinkLayoutArguments)
    $hardLinkInstallArguments = New-Object 'object[]' 2
    $hardLinkInstallArguments[0] = $hardLinkLayout
    $hardLinkInstallArguments[1] = $false
    $installMethod.Invoke($null, $hardLinkInstallArguments) | Out-Null
    Assert-BytesEqual -Expected $outsideRuntimeBytes -Path $outsideRuntime `
        -Context 'Installer runtime hard-link target'
    Assert-BytesEqual -Expected $outsideShortcutBytes -Path $outsideShortcut `
        -Context 'Installer shortcut hard-link target'
    $installedRuntimeBytes = [IO.File]::ReadAllBytes($linkedRuntime)
    if ($installedRuntimeBytes.Length -lt 2 -or
        $installedRuntimeBytes[0] -ne [byte][char]'M' -or
        $installedRuntimeBytes[1] -ne [byte][char]'Z' -or
        (Get-Item -LiteralPath $linkedShortcut).Length -le
            $outsideShortcutBytes.Length) {
        throw 'Installer hard-link destinations were not safely replaced.'
    }

    $setupAssembly = [Reflection.Assembly]::Load(
        [IO.File]::ReadAllBytes(
            (Join-Path $resolvedDist 'RdpSessionReminderSetup.exe')))
    $portableInstallMethod = $setupAssembly.GetType(
        'SetupForm', $true).GetMethod(
            'InstallApplicationFilesFrom',
            [Reflection.BindingFlags]'NonPublic, Static')
    if ($null -eq $portableInstallMethod) {
        throw 'Portable install test seam was not found.'
    }

    $portableSource = Join-Path $fixtureRoot 'portable-source'
    $portableTarget = Join-Path $fixtureRoot 'portable-target'
    $portableLink = Join-Path $fixtureRoot 'portable-link'
    [IO.Directory]::CreateDirectory($portableSource) | Out-Null
    [IO.Directory]::CreateDirectory($portableTarget) | Out-Null
    $sourceRuntime = Join-Path $portableSource 'RdpSessionReminder.exe'
    $sourceSetup = Join-Path $portableSource 'RdpSessionReminderSetup.exe'
    [IO.File]::WriteAllBytes($sourceRuntime, [byte[]](1, 2, 3))
    [IO.File]::WriteAllBytes($sourceSetup, [byte[]](4, 5, 6))
    [byte[]]$portableRuntimeSentinel = 101, 103, 107
    [byte[]]$portableSetupSentinel = 109, 113, 127
    [byte[]]$portableNoteSentinel = 131, 137, 139
    $portableRuntimePath = Join-Path $portableTarget `
        'RdpSessionReminder.exe'
    $portableSetupPath = Join-Path $portableTarget `
        'RdpSessionReminderSetup.exe'
    $portableNotePath = Join-Path $portableTarget `
        'README - Saved RDP Connections.txt'
    [IO.File]::WriteAllBytes(
        $portableRuntimePath, $portableRuntimeSentinel)
    [IO.File]::WriteAllBytes($portableSetupPath, $portableSetupSentinel)
    [IO.File]::WriteAllBytes($portableNotePath, $portableNoteSentinel)
    New-Item -ItemType Junction -Path $portableLink `
        -Target $portableTarget | Out-Null
    $junctions.Add($portableLink)
    Assert-ReparseRootRejected -Context 'Portable application folder' `
        -Action {
            $portableArguments = New-Object 'object[]' 3
            $portableArguments[0] = [string]$sourceRuntime
            $portableArguments[1] = [string]$sourceSetup
            $portableArguments[2] = [string]$portableLink
            $portableInstallMethod.Invoke(
                $null, $portableArguments) | Out-Null
        }
    Assert-BytesEqual -Expected $portableRuntimeSentinel `
        -Path $portableRuntimePath -Context 'Portable runtime'
    Assert-BytesEqual -Expected $portableSetupSentinel `
        -Path $portableSetupPath -Context 'Portable setup'
    Assert-BytesEqual -Expected $portableNoteSentinel `
        -Path $portableNotePath -Context 'Portable uninstall note'
    if ([IO.Directory]::GetFileSystemEntries($portableTarget).Length -ne 3) {
        throw 'Portable application-folder rejection wrote outside its fixture.'
    }

    $portableHardLinkCase = Join-Path $fixtureRoot `
        'portable-child-hard-link'
    $portableHardLinkSource = Join-Path $portableHardLinkCase 'source'
    $portableHardLinkTarget = Join-Path $portableHardLinkCase 'app'
    [IO.Directory]::CreateDirectory($portableHardLinkSource) | Out-Null
    [IO.Directory]::CreateDirectory($portableHardLinkTarget) | Out-Null
    $portableHardLinkRuntimeSource = Join-Path $portableHardLinkSource `
        'RdpSessionReminder.exe'
    $portableHardLinkSetupSource = Join-Path $portableHardLinkSource `
        'RdpSessionReminderSetup.exe'
    [byte[]]$portableRuntimeSourceBytes = 193, 197, 199, 211, 223
    [byte[]]$portableSetupSourceBytes = 227, 229, 233, 239
    [IO.File]::WriteAllBytes(
        $portableHardLinkRuntimeSource, $portableRuntimeSourceBytes)
    [IO.File]::WriteAllBytes(
        $portableHardLinkSetupSource, $portableSetupSourceBytes)
    $portableOutside = Join-Path $portableHardLinkCase 'unrelated.bin'
    [byte[]]$portableOutsideBytes = 241, 251, 253, 255
    [IO.File]::WriteAllBytes($portableOutside, $portableOutsideBytes)
    $portableLinkedRuntime = Join-Path $portableHardLinkTarget `
        'RdpSessionReminder.exe'
    New-Item -ItemType HardLink -Path $portableLinkedRuntime `
        -Target $portableOutside | Out-Null
    $portableHardLinkArguments = New-Object 'object[]' 3
    $portableHardLinkArguments[0] = [string]$portableHardLinkRuntimeSource
    $portableHardLinkArguments[1] = [string]$portableHardLinkSetupSource
    $portableHardLinkArguments[2] = [string]$portableHardLinkTarget
    $portableInstallMethod.Invoke(
        $null, $portableHardLinkArguments) | Out-Null
    Assert-BytesEqual -Expected $portableOutsideBytes -Path $portableOutside `
        -Context 'Portable runtime hard-link target'
    Assert-BytesEqual -Expected $portableRuntimeSourceBytes `
        -Path $portableLinkedRuntime -Context 'Portable installed runtime'

    # A linked child destination is rejected even when the application folder
    # itself is a normal directory.
    $portableChildLinkCase = Join-Path $fixtureRoot `
        'portable-child-reparse'
    $portableChildLinkSource = Join-Path $portableChildLinkCase 'source'
    $portableChildLinkTarget = Join-Path $portableChildLinkCase 'app'
    $portableChildLinkOutside = Join-Path $portableChildLinkCase 'outside'
    [IO.Directory]::CreateDirectory($portableChildLinkSource) | Out-Null
    [IO.Directory]::CreateDirectory($portableChildLinkTarget) | Out-Null
    [IO.Directory]::CreateDirectory($portableChildLinkOutside) | Out-Null
    $portableChildSourceRuntime = Join-Path $portableChildLinkSource `
        'RdpSessionReminder.exe'
    $portableChildSourceSetup = Join-Path $portableChildLinkSource `
        'RdpSessionReminderSetup.exe'
    [IO.File]::WriteAllBytes(
        $portableChildSourceRuntime, [byte[]](13, 17, 19))
    [IO.File]::WriteAllBytes(
        $portableChildSourceSetup, [byte[]](23, 29, 31))
    $portableChildSentinel = Join-Path $portableChildLinkOutside `
        'sentinel.bin'
    [byte[]]$portableChildSentinelBytes = 37, 41, 43
    [IO.File]::WriteAllBytes(
        $portableChildSentinel, $portableChildSentinelBytes)
    $portableChildLink = Join-Path $portableChildLinkTarget `
        'RdpSessionReminder.exe'
    New-Item -ItemType Junction -Path $portableChildLink `
        -Target $portableChildLinkOutside | Out-Null
    $junctions.Add($portableChildLink)
    Assert-ReparseRootRejected -Context 'Portable application file' `
        -Action {
            $portableChildArguments = New-Object 'object[]' 3
            $portableChildArguments[0] = [string]$portableChildSourceRuntime
            $portableChildArguments[1] = [string]$portableChildSourceSetup
            $portableChildArguments[2] = [string]$portableChildLinkTarget
            $portableInstallMethod.Invoke(
                $null, $portableChildArguments) | Out-Null
        }
    Assert-BytesEqual -Expected $portableChildSentinelBytes `
        -Path $portableChildSentinel `
        -Context 'Portable child reparse target'
}
finally {
    $allJunctionsRemoved = $true
    foreach ($junction in $junctions) {
        try {
            if ([IO.Directory]::Exists($junction)) {
                [IO.Directory]::Delete($junction, $false)
            }
        }
        catch {
            $allJunctionsRemoved = $false
        }
        if ([IO.Directory]::Exists($junction)) {
            $allJunctionsRemoved = $false
        }
    }
    if ($allJunctionsRemoved -and [IO.Directory]::Exists($fixtureRoot)) {
        [IO.Directory]::Delete($fixtureRoot, $true)
    }
}

Write-Host 'Smoke tests passed.'
