[CmdletBinding()]
param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$resolvedRepo = [IO.Path]::GetFullPath($RepoRoot)
$sourcePath = Join-Path $resolvedRepo 'src\ConnectionManagerUi.cs'
$commonPath = Join-Path $resolvedRepo 'src\Common.cs'
if (-not [IO.File]::Exists($sourcePath) -or
    -not [IO.File]::Exists($commonPath)) {
    throw 'Connection-manager source files were not found.'
}

$source = [IO.File]::ReadAllText($sourcePath)
$requiredContracts = @(
    'SearchOption.TopDirectoryOnly',
    'IsCanonicalProfileId',
    'TryResolveDirectProfileDirectory',
    'FileAttributes.ReparsePoint',
    'DuplicateProfileUnderRoot',
    'DeleteProfileUnderRoot',
    'ConnectionDiagnosticsBuilder',
    'unexpected arguments omitted',
    'credential fields are intentionally omitted',
    'Nothing is sent automatically',
    'RepairShortcutRequested',
    'CloseSetupAfterConnect'
)
foreach ($contract in $requiredContracts) {
    if ($source.IndexOf($contract, [StringComparison]::Ordinal) -lt 0) {
        throw "Connection-manager contract is missing '$contract'."
    }
}

$forbiddenContracts = @(
    'Directory.GetLogicalDrives',
    'SearchOption.AllDirectories',
    'FileSystemWatcher',
    'System.Threading.Timer',
    'System.Timers.Timer',
    'NotifyIcon',
    'ServiceBase',
    'HttpClient',
    'WebClient',
    'WebRequest',
    'password 51:b:',
    'username:s:'
)
foreach ($contract in $forbiddenContracts) {
    if ($source.IndexOf($contract, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw "Connection-manager source contains forbidden behavior '$contract'."
    }
}

$compilerCandidates = @(
    (Join-Path $env:SystemRoot 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:SystemRoot 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)
$compiler = $compilerCandidates | Where-Object {
    [IO.File]::Exists($_)
} | Select-Object -First 1
if (-not $compiler) {
    throw '.NET Framework 4.x C# compiler was not found.'
}

$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) (
    'RdpSessionReminder-ConnectionManager-' + [guid]::NewGuid().ToString('N'))
$compileOutput = Join-Path $temporaryRoot 'ConnectionManagerChecks.dll'
try {
    [IO.Directory]::CreateDirectory($temporaryRoot) | Out-Null
    $compilerArguments = @(
        '/nologo',
        '/target:library',
        '/optimize+',
        '/reference:System.dll',
        '/reference:System.Core.dll',
        '/reference:System.Drawing.dll',
        '/reference:System.Windows.Forms.dll',
        "/out:$compileOutput",
        $commonPath,
        $sourcePath
    )
    & $compiler $compilerArguments
    if ($LASTEXITCODE -ne 0 -or -not [IO.File]::Exists($compileOutput)) {
        throw 'The connection-manager component failed to compile.'
    }

    # Load from bytes so the temporary compiler output is not locked during cleanup.
    $assembly = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes($compileOutput))
    $catalogType = $assembly.GetType('ConnectionProfileCatalog', $true)
    $diagnosticsType = $assembly.GetType('ConnectionDiagnosticsBuilder', $true)
    $contextType = $assembly.GetType('ConnectionDiagnosticsContext', $true)
    $shortcutType = $assembly.GetType('ConnectionShortcutSnapshot', $true)
    $flags = [Reflection.BindingFlags]'Static,Public,NonPublic'
    $instanceFlags = [Reflection.BindingFlags]'Instance,Public,NonPublic'

    $isCanonical = $catalogType.GetMethod('IsCanonicalProfileId', $flags)
    $resolve = $catalogType.GetMethod(
        'TryResolveDirectProfileDirectory', $flags)
    $enumerate = $catalogType.GetMethod('EnumerateProfilesUnderRoot', $flags)
    $duplicate = $catalogType.GetMethod('DuplicateProfileUnderRoot', $flags)
    $delete = $catalogType.GetMethod('DeleteProfileUnderRoot', $flags)

    $profileId = [guid]::NewGuid().ToString('N')
    $uppercaseId = [guid]::NewGuid().ToString('N').ToUpperInvariant()
    if (-not [bool]$isCanonical.Invoke($null, @($profileId)) -or
        [bool]$isCanonical.Invoke($null, @($uppercaseId)) -or
        [bool]$isCanonical.Invoke($null, @('../' + $profileId))) {
        throw 'Canonical profile ID validation is not strict.'
    }

    $profilesRoot = Join-Path $temporaryRoot 'profiles'
    $profileDirectory = Join-Path $profilesRoot $profileId
    $ignoredDirectory = Join-Path $profilesRoot 'not-a-profile'
    $uppercaseDirectory = Join-Path $profilesRoot $uppercaseId
    [IO.Directory]::CreateDirectory($profileDirectory) | Out-Null
    [IO.Directory]::CreateDirectory($ignoredDirectory) | Out-Null
    [IO.Directory]::CreateDirectory($uppercaseDirectory) | Out-Null

    $settingsPath = Join-Path $profileDirectory 'settings.ini'
    $rdpPath = Join-Path $profileDirectory 'connection.rdp'
    $secretHost = 'secret-host.example.test'
    $secretUser = 'user@example.test'
    [IO.File]::WriteAllLines($settingsPath, @(
        "Computer=$secretHost",
        'FullScreen=1',
        'ShortcutName=Private Lab',
        "RdpFile=$rdpPath",
        'DisplayDevice=DISPLAY1',
        'ReminderText=REMOTE SESSION - PRIVATE LAB',
        'ShortLabel=REMOTE',
        'BannerPreset=Default',
        'BannerBackground=#B71C1C',
        'IdleDimming=1',
        'FutureSetting=preserve-me'
    ), [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllLines($rdpPath, @(
        "full address:s:$secretHost",
        "username:s:$secretUser",
        'password 51:b:deadbeef'
    ), [Text.Encoding]::Unicode)

    [object[]]$resolvedArguments = @(
        [string]$profilesRoot, [string]$profileId, [string]'')
    if (-not [bool]$resolve.Invoke($null, $resolvedArguments) -or
        [IO.Path]::GetFullPath([string]$resolvedArguments[2]) -ne
            [IO.Path]::GetFullPath($profileDirectory)) {
        throw 'A direct canonical profile path was not resolved correctly.'
    }
    [object[]]$traversalArguments = @(
        [string]$profilesRoot, [string]('../' + $profileId), [string]'')
    if ([bool]$resolve.Invoke($null, $traversalArguments)) {
        throw 'A profile path traversal was accepted.'
    }

    [object[]]$enumerateArguments = @([string]$profilesRoot)
    $profiles = @($enumerate.Invoke($null, $enumerateArguments))
    if ($profiles.Count -ne 1) {
        throw "Expected one canonical profile, found $($profiles.Count)."
    }
    $profile = $profiles[0].PSObject.BaseObject
    if (-not [bool]$profile.GetType().GetProperty(
            'Ready', $instanceFlags).GetValue($profile, $null)) {
        throw 'The complete canonical profile was not marked ready.'
    }
    $loadedSettings = $profile.GetType().GetField(
        'Settings', $instanceFlags).GetValue($profile).PSObject.BaseObject
    if ($loadedSettings.GetType().GetField(
            'BannerBackground', $instanceFlags).GetValue($loadedSettings) -ne
            '#B71C1C' -or
        -not [bool]$loadedSettings.GetType().GetField(
            'IdleDimming', $instanceFlags).GetValue($loadedSettings)) {
        throw 'Connection management did not retain banner preferences.'
    }

    [object[]]$duplicateArguments = @(
        [string]$profilesRoot,
        [string]$profileId,
        [string]'Private Lab Copy'
    )
    $duplicateProfile = $duplicate.Invoke(
        $null, $duplicateArguments).PSObject.BaseObject
    $duplicateId = [string]$duplicateProfile.GetType().GetField(
        'ProfileId', $instanceFlags).GetValue($duplicateProfile)
    if (-not [bool]$isCanonical.Invoke($null, @($duplicateId)) -or
        $duplicateId -eq $profileId) {
        throw 'Duplication did not allocate a new canonical profile ID.'
    }
    $duplicateDirectory = Join-Path $profilesRoot $duplicateId
    $duplicateRdp = Join-Path $duplicateDirectory 'connection.rdp'
    $duplicateSettings = Join-Path $duplicateDirectory 'settings.ini'
    $sourceHash = [Convert]::ToBase64String(
        [Security.Cryptography.SHA256]::Create().ComputeHash(
            [IO.File]::ReadAllBytes($rdpPath)))
    $duplicateHash = [Convert]::ToBase64String(
        [Security.Cryptography.SHA256]::Create().ComputeHash(
            [IO.File]::ReadAllBytes($duplicateRdp)))
    if ($sourceHash -ne $duplicateHash) {
        throw 'Duplication changed the copied RDP file.'
    }
    $duplicateSettingsText = [IO.File]::ReadAllText($duplicateSettings)
    if ($duplicateSettingsText -notmatch '(?m)^ShortcutName=Private Lab Copy\r?$' -or
        $duplicateSettingsText -notmatch '(?m)^FutureSetting=preserve-me\r?$' -or
        $duplicateSettingsText -notmatch '(?m)^BannerBackground=#B71C1C\r?$' -or
        $duplicateSettingsText.IndexOf(
            "RdpFile=$duplicateRdp", [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw 'Duplication did not preserve extension settings or retarget its RDP copy.'
    }

    $context = [Activator]::CreateInstance(
        $contextType, $true).PSObject.BaseObject
    $runtimePath = Join-Path (
        [Environment]::GetFolderPath(
            [Environment+SpecialFolder]::LocalApplicationData)) (
        'RdpSessionReminder\RdpSessionReminder.exe')
    $mstscPath = Join-Path ([Environment]::SystemDirectory) 'mstsc.exe'
    $contextType.GetField('ApplicationVersion', $instanceFlags).SetValue(
        $context, '1.2.0')
    $contextType.GetField('RuntimePath', $instanceFlags).SetValue(
        $context, $runtimePath)
    $contextType.GetField('MstscPath', $instanceFlags).SetValue(
        $context, $mstscPath)
    $contextType.GetField('SelectedMonitorIds', $instanceFlags).SetValue(
        $context, [int[]]@(1, 2))
    $contextType.GetField('AvailableMonitorIds', $instanceFlags).SetValue(
        $context, [int[]]@(0, 1, 2))

    $shortcut = [Activator]::CreateInstance(
        $shortcutType, $true).PSObject.BaseObject
    $shortcutType.GetField('Inspected', $instanceFlags).SetValue($shortcut, $true)
    $shortcutType.GetField('Exists', $instanceFlags).SetValue($shortcut, $true)
    $shortcutType.GetField('TargetPath', $instanceFlags).SetValue(
        $shortcut, $runtimePath)
    $shortcutType.GetField('Arguments', $instanceFlags).SetValue(
        $shortcut, '--profile ' + $profileId)
    $shortcutType.GetField('IconLocation', $instanceFlags).SetValue(
        $shortcut, $mstscPath + ',0')
    $shortcutType.GetField('WorkingDirectory', $instanceFlags).SetValue(
        $shortcut, '')
    $contextType.GetField('Shortcut', $instanceFlags).SetValue(
        $context, $shortcut)

    $buildReport = $diagnosticsType.GetMethod('Build', $flags)
    [object[]]$reportArguments = @($profile, $context)
    $report = [string]$buildReport.Invoke($null, $reportArguments)
    foreach ($secret in @($secretHost, $secretUser, 'deadbeef',
            'REMOTE SESSION - PRIVATE LAB')) {
        if ($report.IndexOf($secret, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "Sanitized diagnostics exposed '$secret'."
        }
    }
    foreach ($expected in @(
            'credential fields are intentionally omitted',
            'Copied RDP SHA-256:',
            '--profile <PROFILE-ID>',
            'Selected monitors currently available: Yes',
            'the app does not submit it')) {
        if ($report.IndexOf($expected, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw "Sanitized diagnostics omitted '$expected'."
        }
    }

    $unrelated = Join-Path $profilesRoot 'keep-this-folder'
    [IO.Directory]::CreateDirectory($unrelated) | Out-Null
    [IO.File]::WriteAllText((Join-Path $unrelated 'keep.txt'), 'keep')
    [object[]]$deleteArguments = @(
        [string]$profilesRoot, [string]$duplicateId)
    $delete.Invoke($null, $deleteArguments) | Out-Null
    if ([IO.Directory]::Exists($duplicateDirectory) -or
        -not [IO.Directory]::Exists($profileDirectory) -or
        -not [IO.File]::Exists((Join-Path $unrelated 'keep.txt'))) {
        throw 'Profile deletion escaped the exact canonical profile directory.'
    }

    $deleteRejectedTraversal = $false
    try {
        [object[]]$badDeleteArguments = @(
            [string]$profilesRoot, [string]('../' + $profileId))
        $delete.Invoke($null, $badDeleteArguments) | Out-Null
    }
    catch {
        if ($_.Exception.InnerException -is [ArgumentException]) {
            $deleteRejectedTraversal = $true
        }
        else {
            throw
        }
    }
    if (-not $deleteRejectedTraversal -or
        -not [IO.Directory]::Exists($profileDirectory)) {
        throw 'Profile deletion accepted a traversal value.'
    }
}
finally {
    if ([IO.Directory]::Exists($temporaryRoot)) {
        $resolvedTemporary = [IO.Path]::GetFullPath($temporaryRoot)
        $resolvedSystemTemporary = [IO.Path]::GetFullPath(
            [IO.Path]::GetTempPath()).TrimEnd(
                [IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        if (-not $resolvedTemporary.StartsWith(
                $resolvedSystemTemporary, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Refusing to remove a test directory outside the system temp folder.'
        }
        [IO.Directory]::Delete($resolvedTemporary, $true)
    }
}

Write-Host 'Connection manager checks passed.'
