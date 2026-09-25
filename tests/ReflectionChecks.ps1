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

        $rdpFileText.Text = $rdpPath
        if ($computerText.Text -ne 'lab-b' -or
            -not $computerText.ReadOnly -or
            $fullScreenCheck.Enabled -or
            $reminderText.Text -ne 'REMOTE SESSION - LAB-B') {
            throw 'Setup did not enter the expected read-only RDP file mode.'
        }

        $rdpFileText.Text = ''
        if ($computerText.ReadOnly -or -not $fullScreenCheck.Enabled) {
            throw 'Setup did not return to editable direct-connection mode.'
        }
    }
    finally {
        if ($null -ne $form) {
            $form.Dispose()
        }
    }
}
finally {
    if ([IO.Directory]::Exists($temporaryDirectory)) {
        [IO.Directory]::Delete($temporaryDirectory, $true)
    }
}

Write-Host 'Reflection checks passed.'
