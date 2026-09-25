[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$DistDirectory
)

$ErrorActionPreference = 'Stop'
$resolvedDist = [IO.Path]::GetFullPath($DistDirectory)
$runtimePath = Join-Path $resolvedDist 'RdpSessionReminder.exe'
$setupPath = Join-Path $resolvedDist 'RdpSessionReminderSetup.exe'

$runtimeAssembly = [Reflection.Assembly]::LoadFile($runtimePath)
$settingsType = $runtimeAssembly.GetType('SettingsStore', $true)
$validateTarget = $settingsType.GetMethod(
    'IsValidComputerName',
    [Reflection.BindingFlags]'Static,Public,NonPublic')

$targetCases = @(
    [pscustomobject]@{ Value = '10.0.0.25:3390'; Expected = $true },
    [pscustomobject]@{ Value = '[2001:db8::1]:3389'; Expected = $true },
    [pscustomobject]@{ Value = 'server: 3389'; Expected = $false },
    [pscustomobject]@{ Value = '010.0.0.1'; Expected = $false },
    [pscustomobject]@{ Value = '0x7f000001'; Expected = $false },
    [pscustomobject]@{ Value = "a`n.example"; Expected = $false },
    [pscustomobject]@{
        Value = 'server:' + [char]0xFF11 + [char]0xFF12 + [char]0xFF13
        Expected = $false
    }
)

foreach ($case in $targetCases) {
    [object[]]$arguments = @([string]$case.Value)
    $actual = [bool]$validateTarget.Invoke($null, $arguments)
    if ($actual -ne $case.Expected) {
        throw "Target validation mismatch for '$($case.Value)': expected $($case.Expected), got $actual."
    }
}

$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) (
    'RdpSessionReminderReflection-' + [guid]::NewGuid().ToString('N'))
try {
    [IO.Directory]::CreateDirectory($temporaryDirectory) | Out-Null
    $rdpPath = Join-Path $temporaryDirectory 'mixed-case-utf16.rdp'
    [IO.File]::WriteAllLines($rdpPath, @(
        'FuLl AdDrEsS:s:lab-a',
        'AlTeRnAtE FuLl AdDrEsS:s:lab-b'
    ), [Text.Encoding]::Unicode)

    $setupAssembly = [Reflection.Assembly]::LoadFile($setupPath)
    $setupFormType = $setupAssembly.GetType('SetupForm', $true)
    $publisherTrustType = $setupAssembly.GetType(
        'LocalRdpPublisherTrust', $true)
    $locationConflict = $publisherTrustType.GetMethod(
        'HasLocationSigningConflict',
        [Reflection.BindingFlags]'Static,Public,NonPublic')
    [object[]]$bothEnabled = @($true, $true)
    [object[]]$trustOnly = @($true, $false)
    [object[]]$locationOnly = @($false, $true)
    if (-not [bool]$locationConflict.Invoke($null, $bothEnabled) -or
        [bool]$locationConflict.Invoke($null, $trustOnly) -or
        [bool]$locationConflict.Invoke($null, $locationOnly)) {
        throw 'Publisher trust must reject Location redirection before certificate creation.'
    }
    $readTarget = $setupFormType.GetMethod(
        'TryReadRdpTarget',
        [Reflection.BindingFlags]'Static,Public,NonPublic')
    [object[]]$arguments = @([string]$rdpPath)
    $target = [string]$readTarget.Invoke($null, $arguments)
    if ($target -ne 'lab-b') {
        throw "RDP target parser mismatch: expected 'lab-b', got '$target'."
    }

    $form = [Activator]::CreateInstance($setupFormType, $true)
    try {
        $instanceFields = [Reflection.BindingFlags]'Instance,NonPublic'
        $rdpFileText = $setupFormType.GetField(
            'rdpFileText', $instanceFields).GetValue($form)
        $computerText = $setupFormType.GetField(
            'computerText', $instanceFields).GetValue($form)
        $reminderText = $setupFormType.GetField(
            'reminderText', $instanceFields).GetValue($form)
        $fullScreenCheck = $setupFormType.GetField(
            'fullScreenCheck', $instanceFields).GetValue($form)
        $resolutionCombo = $setupFormType.GetField(
            'resolutionCombo', $instanceFields).GetValue($form)
        $allMonitorsCheck = $setupFormType.GetField(
            'allMonitorsCheck', $instanceFields).GetValue($form)
        $alwaysAskCredentialsCheck = $setupFormType.GetField(
            'alwaysAskCredentialsCheck', $instanceFields).GetValue($form)
        $redirectClipboardCheck = $setupFormType.GetField(
            'redirectClipboardCheck', $instanceFields).GetValue($form)
        $redirectDrivesCheck = $setupFormType.GetField(
            'redirectDrivesCheck', $instanceFields).GetValue($form)
        $redirectLocationCheck = $setupFormType.GetField(
            'redirectLocationCheck', $instanceFields).GetValue($form)
        $redirectComPortsCheck = $setupFormType.GetField(
            'redirectComPortsCheck', $instanceFields).GetValue($form)
        $redirectWebAuthnCheck = $setupFormType.GetField(
            'redirectWebAuthnCheck', $instanceFields).GetValue($form)
        $redirectSmartCardsCheck = $setupFormType.GetField(
            'redirectSmartCardsCheck', $instanceFields).GetValue($form)
        $trustPublisherCheck = $setupFormType.GetField(
            'trustPublisherCheck', $instanceFields).GetValue($form)
        $tabs = $setupFormType.GetField(
            'tabs', $instanceFields).GetValue($form)
        $operationStatus = $setupFormType.GetField(
            'operationStatus', $instanceFields).GetValue($form)
        $operationProgress = $setupFormType.GetField(
            'operationProgress', $instanceFields).GetValue($form)

        $updatePage = $tabs.TabPages | Where-Object { $_.Text -eq 'Updates' }
        $installExplanation = $updatePage.Controls | Where-Object {
            $_ -is [Windows.Forms.Label] -and
            $_.Text -like 'Updates go directly to the newest stable version.*'
        }
        if ($null -eq $updatePage -or -not $form.AutoScroll -or
            $operationStatus.Visible -or
            $operationProgress.Visible) {
            throw 'Setup update/progress controls were not initialized correctly.'
        }
        if ($null -eq $installExplanation -or
            $installExplanation.Height -lt $installExplanation.GetPreferredSize(
                [Drawing.Size]::new($installExplanation.Width, 0)).Height) {
            throw 'The update-install explanation is clipped.'
        }

        $fitWindow = $setupFormType.GetMethod(
            'LimitWindowSizeToWorkingArea',
            [Reflection.BindingFlags]'Static,Public,NonPublic')
        [object[]]$fitArguments = @(
            [Drawing.Size]::new(900, 900),
            [Drawing.Rectangle]::new(0, 0, 800, 600)
        )
        $fittedSize = $fitWindow.Invoke($null, $fitArguments)
        if ($fittedSize.Width -ne 800 -or $fittedSize.Height -ne 600) {
            throw 'Setup window size was not capped to the current screen work area.'
        }

        if (-not $fullScreenCheck.Checked -or
            $resolutionCombo.SelectedItem.ToString() -notlike '*Native/current*' -or
            $allMonitorsCheck.Checked -or
            -not $alwaysAskCredentialsCheck.Checked -or
            -not $redirectClipboardCheck.Checked -or
            $redirectDrivesCheck.Checked -or
            $redirectLocationCheck.Checked -or
            $redirectComPortsCheck.Checked -or
            -not $redirectWebAuthnCheck.Checked -or
            $redirectSmartCardsCheck.Checked -or
            $trustPublisherCheck.Checked) {
            throw 'Setup custom-profile defaults do not match the guided configuration.'
        }

        $rdpFileText.Text = $rdpPath
        if ($computerText.Text -ne 'lab-b' -or
            -not $computerText.ReadOnly -or
            $fullScreenCheck.Enabled -or
            $resolutionCombo.Enabled -or
            $allMonitorsCheck.Enabled -or
            $alwaysAskCredentialsCheck.Enabled -or
            $redirectClipboardCheck.Enabled -or
            $redirectDrivesCheck.Enabled -or
            $redirectLocationCheck.Enabled -or
            $redirectComPortsCheck.Enabled -or
            $redirectWebAuthnCheck.Enabled -or
            $redirectSmartCardsCheck.Enabled -or
            $trustPublisherCheck.Enabled -or
            $reminderText.Text -ne 'REMOTE SESSION - LAB-B') {
            throw 'Setup did not enter the expected read-only RDP file mode.'
        }

        $rdpFileText.Text = ''
        if ($computerText.ReadOnly -or -not $fullScreenCheck.Enabled -or
            -not $resolutionCombo.Enabled -or -not $allMonitorsCheck.Enabled -or
            -not $alwaysAskCredentialsCheck.Enabled -or
            -not $redirectClipboardCheck.Enabled -or
            -not $redirectDrivesCheck.Enabled -or
            -not $redirectLocationCheck.Enabled -or
            -not $redirectComPortsCheck.Enabled -or
            -not $redirectWebAuthnCheck.Enabled -or
            -not $redirectSmartCardsCheck.Enabled -or
            -not $trustPublisherCheck.Enabled) {
            throw 'Setup did not return to editable direct-connection mode.'
        }
    }
    finally {
        if ($null -ne $form) {
            $form.Dispose()
        }
    }

    $shortcutWriterType = $setupAssembly.GetType('ShortcutWriter', $true)
    $shortcutPath = Join-Path $temporaryDirectory 'empty-working-directory.lnk'
    $absoluteTarget = [IO.Path]::GetFullPath($setupPath)
    $profileArguments = '--profile 00000000000000000000000000000000'
    $createShortcut = $shortcutWriterType.GetMethod(
        'Create', [Reflection.BindingFlags]'Static,Public,NonPublic')
    [object[]]$createShortcutArguments = @(
        [string]$shortcutPath,
        [string]$absoluteTarget,
        [string]'',
        [string]$profileArguments
    )
    $createShortcut.Invoke($null, $createShortcutArguments) | Out-Null
    $readWorkingDirectory = $shortcutWriterType.GetMethod(
        'ReadWorkingDirectory',
        [Reflection.BindingFlags]'Static,Public,NonPublic')
    $readShortcutTarget = $shortcutWriterType.GetMethod(
        'ReadTargetPath', [Reflection.BindingFlags]'Static,Public,NonPublic')
    $readShortcutArguments = $shortcutWriterType.GetMethod(
        'ReadArguments', [Reflection.BindingFlags]'Static,Public,NonPublic')
    [object[]]$shortcutPathArgument = @([string]$shortcutPath)
    if ($readWorkingDirectory.Invoke($null, $shortcutPathArgument) -ne '' -or
        -not [IO.Path]::IsPathRooted(
            $readShortcutTarget.Invoke($null, $shortcutPathArgument)) -or
        $readShortcutArguments.Invoke($null, $shortcutPathArgument) -ne
            $profileArguments) {
        throw 'Reminder shortcuts must use an empty Start in value and absolute target.'
    }
}
finally {
    if ([IO.Directory]::Exists($temporaryDirectory)) {
        [IO.Directory]::Delete($temporaryDirectory, $true)
    }
}

Write-Host 'Reflection checks passed.'
