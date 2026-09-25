[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$DistDirectory
)

$ErrorActionPreference = 'Stop'
$runtimePath = Join-Path ([IO.Path]::GetFullPath($DistDirectory)) `
    'RdpSessionReminder.exe'
$assembly = [Reflection.Assembly]::LoadFile($runtimePath)
$settingsType = $assembly.GetType('SettingsStore', $true)
$runtimeType = $assembly.GetType('RdpSessionReminder', $true)
$oneTimeType = $assembly.GetType('OneTimeProfileStore', $true)
$flags = [Reflection.BindingFlags]'Static,Public,NonPublic'

$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) (
    'RdpSessionReminderRuntime-' + [guid]::NewGuid().ToString('N'))
try {
    [IO.Directory]::CreateDirectory($temporaryDirectory) | Out-Null
    $legacyPath = Join-Path $temporaryDirectory 'legacy.ini'
    [IO.File]::WriteAllText($legacyPath, @"
Computer=lab.example.com
FullScreen=1
ShortcutName=Lab
RdpFile=
DisplayDevice=
ReminderText=REMOTE SESSION - LAB
"@, [Text.UTF8Encoding]::new($false))

    $tryLoad = $settingsType.GetMethods($flags) | Where-Object {
        $_.Name -eq 'TryLoadFrom' -and $_.GetParameters().Count -eq 2
    } | Select-Object -First 1
    [object[]]$loadArguments = @([string]$legacyPath, $null)
    if (-not [bool]$tryLoad.Invoke($null, $loadArguments)) {
        throw 'A legacy settings file could not be loaded.'
    }
    $settings = $loadArguments[1]
    $settingsFields = $settings.GetType().GetFields(
        [Reflection.BindingFlags]'Instance,Public,NonPublic')
    $values = @{}
    foreach ($field in $settingsFields) {
        $values[$field.Name] = $field.GetValue($settings)
    }
    if ($values.ShortLabel -ne '' -or
        $values.BannerPreset -ne 'Default' -or
        $values.BannerBackground -ne '#182335' -or
        $values.BannerForeground -ne '#FFFFFF' -or
        $values.BannerCorner -ne 'BottomRight' -or
        $values.BannerVerticalOffset -ne 64 -or
        $values.BannerSize -ne 'Medium' -or
        $values.BannerOpacity -ne 97 -or
        $values.IdleDimming) {
        throw 'Legacy banner defaults no longer reproduce the original banner.'
    }

    $buildText = $runtimeType.GetMethod('BuildBannerText', $flags)
    [object[]]$plainTextArguments = @('', 'REMOTE SESSION - LAB')
    [object[]]$labelTextArguments = @('WORK', 'REMOTE SESSION - LAB')
    if ($buildText.Invoke($null, $plainTextArguments) -ne
            'REMOTE SESSION - LAB' -or
        $buildText.Invoke($null, $labelTextArguments) -ne
            'WORK  |  REMOTE SESSION - LAB') {
        throw 'Short-label banner composition is incorrect.'
    }

    $position = $runtimeType.GetMethod('CalculateBannerPosition', $flags)
    [object[]]$topLeftArguments = @(
        'TopLeft', 0, 0, 1920, 1080, 350, 34, 20, 64)
    [object[]]$bottomRightArguments = @(
        'BottomRight', 0, 0, 1920, 1080, 350, 34, 20, 64)
    $topLeft = [int[]]$position.Invoke($null, $topLeftArguments)
    $bottomRight = [int[]]$position.Invoke($null, $bottomRightArguments)
    if ($topLeft[0] -ne 20 -or $topLeft[1] -ne 64 -or
        $bottomRight[0] -ne 1550 -or $bottomRight[1] -ne 982) {
        throw 'Banner corner positioning is incorrect.'
    }

    $oneTimeRoot = Join-Path $temporaryDirectory 'one-time'
    $profileId = [guid]::NewGuid().ToString('N')
    $profileDirectory = Join-Path $oneTimeRoot $profileId
    [IO.Directory]::CreateDirectory($profileDirectory) | Out-Null
    $settingsPath = Join-Path $profileDirectory 'settings.ini'
    $connectionPath = Join-Path $profileDirectory 'connection.rdp'
    $unrelatedPath = Join-Path $profileDirectory 'unrelated.txt'
    [IO.File]::WriteAllText($settingsPath, 'owned')
    [IO.File]::WriteAllText($connectionPath, 'owned')
    [IO.File]::WriteAllText($unrelatedPath, 'preserve')

    $deleteExact = $oneTimeType.GetMethod(
        'TryDeleteExactFromRoot', $flags)
    [object[]]$deleteArguments = @(
        [string]$oneTimeRoot, [string]$profileId, [string]'')
    if (-not [bool]$deleteExact.Invoke($null, $deleteArguments) -or
        [IO.File]::Exists($settingsPath) -or
        [IO.File]::Exists($connectionPath) -or
        -not [IO.File]::Exists($unrelatedPath) -or
        -not [IO.Directory]::Exists($profileDirectory)) {
        throw 'One-time cleanup did not restrict deletion to its two owned files.'
    }

    [IO.File]::Delete($unrelatedPath)
    [object[]]$secondDeleteArguments = @(
        [string]$oneTimeRoot, [string]$profileId, [string]'')
    if (-not [bool]$deleteExact.Invoke($null, $secondDeleteArguments) -or
        [IO.Directory]::Exists($profileDirectory)) {
        throw 'One-time cleanup did not remove its empty GUID directory.'
    }

    [object[]]$escapeArguments = @(
        [string]$oneTimeRoot, [string]'..\escape', [string]'')
    if ([bool]$deleteExact.Invoke($null, $escapeArguments)) {
        throw 'One-time cleanup accepted a non-GUID path.'
    }
}
finally {
    if ([IO.Directory]::Exists($temporaryDirectory)) {
        [IO.Directory]::Delete($temporaryDirectory, $true)
    }
}

Write-Host 'Runtime customization checks passed.'
