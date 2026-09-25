[CmdletBinding()]
param(
    [string]$RepoRoot
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
}
$resolvedRoot = [IO.Path]::GetFullPath($RepoRoot)
$sourcePath = Join-Path $resolvedRoot 'src\AdvancedSetupSupport.cs'
$commonPath = Join-Path $resolvedRoot 'src\Common.cs'
if (-not (Test-Path -LiteralPath $sourcePath) -or
    -not (Test-Path -LiteralPath $commonPath)) {
    throw 'Advanced setup source files were not found.'
}

$compilerCandidates = @(
    (Join-Path $env:SystemRoot 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:SystemRoot 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)
$compiler = $compilerCandidates |
    Where-Object { Test-Path -LiteralPath $_ } |
    Select-Object -First 1
if (-not $compiler) {
    throw '.NET Framework 4.x C# compiler was not found.'
}

$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) (
    'RdpSessionReminderAdvanced-' + [guid]::NewGuid().ToString('N'))
try {
    [IO.Directory]::CreateDirectory($temporaryDirectory) | Out-Null
    $assemblyPath = Join-Path $temporaryDirectory 'AdvancedSetupChecks.dll'
    & $compiler /nologo /target:library /optimize+ /platform:anycpu `
        /reference:System.dll /reference:System.Core.dll `
        /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
        /out:$assemblyPath $commonPath $sourcePath
    if ($LASTEXITCODE -ne 0) {
        throw 'Advanced setup support did not compile.'
    }

    # Load from bytes so Windows PowerShell does not keep the temporary DLL
    # locked through the cleanup in this process.
    $assembly = [Reflection.Assembly]::Load(
        [IO.File]::ReadAllBytes($assemblyPath))
    $monitorType = $assembly.GetType('MonitorDescriptor', $true)
    $selectionType = $assembly.GetType('MonitorSelection', $true)
    $resourceType = $assembly.GetType('RdpResourceOptions', $true)
    $previewType = $assembly.GetType('RdpImportPreview', $true)
    $iconCatalogType = $assembly.GetType('ShortcutIconCatalog', $true)
    $iconKindType = $assembly.GetType('ShortcutIconKind', $true)

    $monitorListType = [Collections.Generic.List``1].MakeGenericType($monitorType)
    $monitors = [Activator]::CreateInstance($monitorListType)
    $monitorConstructor = $monitorType.GetConstructors()[0]
    $firstMonitor = $monitorConstructor.Invoke(@(
        [int]0,
        [string]'DISPLAY-A',
        [Drawing.Rectangle]::new(0, 0, 1920, 1080),
        [Drawing.Rectangle]::new(0, 0, 1920, 1040),
        [bool]$true
    ))
    $secondMonitor = $monitorConstructor.Invoke(@(
        [int]1,
        [string]'DISPLAY-B',
        [Drawing.Rectangle]::new(1920, 0, 2560, 1440),
        [Drawing.Rectangle]::new(1920, 0, 2560, 1400),
        [bool]$false
    ))
    $monitors.Add($firstMonitor)
    $monitors.Add($secondMonitor)

    $selectedIds = [Collections.Generic.List[int]]::new()
    $selectedIds.Add(0)
    $selectedIds.Add(1)
    $selection = [Activator]::CreateInstance(
        $selectionType, @($monitors, $selectedIds, [int]1))
    if (-not $selection.UseMultimon -or
        $selection.SelectedMonitorsSetting -ne '0,1' -or
        $selection.ReminderMonitorId -ne 1 -or
        $firstMonitor.DisplayNumber -ne 1 -or
        $secondMonitor.DisplayNumber -ne 2) {
        throw 'Monitor selection did not preserve zero-based mstsc IDs and one-based labels.'
    }
    if ($selection.GetCompatibilityWarnings().Count -ne 0) {
        throw 'Adjacent monitors were incorrectly reported as disconnected.'
    }

    $reverseIds = [Collections.Generic.List[int]]::new()
    $reverseIds.Add(1)
    $reverseIds.Add(0)
    $reverseSelection = [Activator]::CreateInstance(
        $selectionType, @($monitors, $reverseIds, [int]0))
    if ($reverseSelection.SelectedMonitorsSetting -ne '1,0' -or
        $reverseSelection.RemotePrimaryMonitorId -ne 1) {
        throw 'Monitor selection lost the caller-selected remote primary display.'
    }

    $singleId = [Collections.Generic.List[int]]::new()
    $singleId.Add(1)
    $singleSelection = [Activator]::CreateInstance(
        $selectionType, @($monitors, $singleId, [int]0))
    if (-not $singleSelection.UseMultimon -or
        $singleSelection.SelectedMonitorsSetting -ne '1') {
        throw 'Explicit selection of one non-primary monitor must enable selectedmonitors.'
    }

    $defaultsMethod = $resourceType.GetMethod(
        'CreateRecommendedDefaults',
        [Reflection.BindingFlags]'Static,Public,NonPublic')
    $defaults = $defaultsMethod.Invoke($null, @())
    if (-not $defaults.RedirectClipboard -or
        -not $defaults.RedirectWebAuthn -or
        $defaults.RedirectDrives -or
        $defaults.RedirectPrinters -or
        $defaults.RedirectMicrophone -or
        $defaults.RedirectComPorts -or
        $defaults.RedirectSmartCards -or
        $defaults.RedirectLocation) {
        throw 'Resource defaults must keep sensitive redirections off.'
    }
    $defaultSettings = [string[]]$defaults.GetRdpSettings()
    foreach ($requiredSetting in @(
        'redirectclipboard:i:1',
        'redirectwebauthn:i:1',
        'redirectprinters:i:0',
        'audiocapturemode:i:0'
    )) {
        if ($defaultSettings -notcontains $requiredSetting) {
            throw "Resource defaults omitted '$requiredSetting'."
        }
    }

    $rdpPath = Join-Path $temporaryDirectory 'preview-source.rdp'
    $secretAddress = 'secret-host.example:3390'
    $secretGateway = 'secret-gateway.example'
    $secretPasswordBlob = '0102030405DEADBEEF'
    [IO.File]::WriteAllLines($rdpPath, @(
        'screen mode id:i:2',
        'use multimon:i:1',
        'selectedmonitors:s:0,1',
        'desktopwidth:i:1920',
        'desktopheight:i:1080',
        'session bpp:i:32',
        'displayconnectionbar:i:1',
        "full address:s:$secretAddress",
        'username:s:MicrosoftAccount\person@example.com',
        "password 51:b:$secretPasswordBlob",
        "gatewayhostname:s:$secretGateway",
        'redirectclipboard:i:1',
        'drivestoredirect:s:C:;',
        'redirectprinters:i:1',
        'audiocapturemode:i:1',
        'redirectcomports:i:1',
        'redirectsmartcards:i:1',
        'redirectwebauthn:i:1',
        'redirectlocation:i:1',
        'authentication level:i:0',
        'enablecredsspsupport:i:0'
    ), [Text.Encoding]::Unicode)
    $beforeHash = (Get-FileHash -LiteralPath $rdpPath -Algorithm SHA256).Hash
    $beforeWrite = (Get-Item -LiteralPath $rdpPath).LastWriteTimeUtc

    $readDefault = $previewType.GetMethods(
        [Reflection.BindingFlags]'Static,Public,NonPublic') |
        Where-Object {
            $_.Name -eq 'Read' -and $_.GetParameters().Count -eq 1
        }
    $preview = $readDefault.Invoke($null, @([string]$rdpPath))
    $display = [string]::Join("`n", [string[]]$preview.GetDisplayLines())
    if (-not $preview.FullAddressPresent -or
        $preview.FullAddressDisplay -ne 'Configured (hidden)' -or
        -not $preview.UsernamePresent -or
        -not $preview.Password51Present -or
        -not $preview.GatewayPresent -or
        $preview.GatewayDisplay -ne 'Configured (hidden)' -or
        -not $preview.Resources.RedirectPrinters -or
        -not $preview.Resources.RedirectMicrophone -or
        -not $preview.Resources.RedirectDrives -or
        $preview.AuthenticationWarnings.Count -lt 3) {
        throw 'RDP import preview did not summarize the requested settings.'
    }
    foreach ($secret in @($secretAddress, $secretGateway, $secretPasswordBlob)) {
        if ($display.Contains($secret)) {
            throw 'The default RDP import preview exposed a protected value.'
        }
    }
    if ((Get-FileHash -LiteralPath $rdpPath -Algorithm SHA256).Hash -ne
            $beforeHash -or
        (Get-Item -LiteralPath $rdpPath).LastWriteTimeUtc -ne $beforeWrite) {
        throw 'RDP import preview modified its source file.'
    }

    $readRevealed = $previewType.GetMethods(
        [Reflection.BindingFlags]'Static,Public,NonPublic') |
        Where-Object {
            $_.Name -eq 'Read' -and $_.GetParameters().Count -eq 2
        }
    $revealed = $readRevealed.Invoke(
        $null, @([string]$rdpPath, [bool]$true))
    $revealedDisplay = [string]::Join(
        "`n", [string[]]$revealed.GetDisplayLines())
    if (-not $revealedDisplay.Contains($secretAddress) -or
        -not $revealedDisplay.Contains($secretGateway) -or
        $revealedDisplay.Contains($secretPasswordBlob)) {
        throw 'Explicit endpoint reveal must never reveal a password value.'
    }

    $choices = $iconCatalogType.GetMethod(
        'GetChoices', [Reflection.BindingFlags]'Static,Public,NonPublic').Invoke(
            $null, @())
    if ($choices.Count -ne 6 -or
        -not $choices[0].IconPath.EndsWith('mstsc.exe',
            [StringComparison]::OrdinalIgnoreCase) -or
        $choices[0].IconIndex -ne 0) {
        throw 'Shortcut icon choices did not preserve the Windows RDP default.'
    }
    $isOwned = $iconCatalogType.GetMethod(
        'IsAppOwnedIconPath',
        [Reflection.BindingFlags]'Static,Public,NonPublic')
    for ($index = 1; $index -lt $choices.Count; $index++) {
        if (-not [bool]$isOwned.Invoke(
                $null, @([string]$choices[$index].IconPath))) {
            throw 'A generated shortcut icon escaped the app-owned directory.'
        }
    }

    $generatedIconPath = Join-Path $temporaryDirectory 'generated-work.ico'
    $writeIcon = $iconCatalogType.GetMethod(
        'WriteIcon', [Reflection.BindingFlags]'Static,NonPublic')
    $workIconKind = [Enum]::Parse($iconKindType, 'Work')
    $writeIcon.Invoke(
        $null, @([string]$generatedIconPath, $workIconKind)) | Out-Null
    if ((Get-Item -LiteralPath $generatedIconPath).Length -le 128) {
        throw 'The generated shortcut icon is unexpectedly small.'
    }
    $loadedIcon = [Drawing.Icon]::new($generatedIconPath)
    try {
        if ($loadedIcon.Width -le 0 -or $loadedIcon.Height -le 0) {
            throw 'The generated shortcut icon could not be decoded.'
        }
    }
    finally {
        $loadedIcon.Dispose()
    }

    $source = [IO.File]::ReadAllText($sourcePath)
    foreach ($forbidden in @(
        'System.Diagnostics.Process',
        'System.Threading.Thread',
        'System.Net.Http',
        'Microsoft.Win32.Registry',
        'SetWindowsHookEx',
        'AxMSTSCLib'
    )) {
        if ($source.Contains($forbidden)) {
            throw "Advanced setup support contains forbidden runtime behavior: $forbidden"
        }
    }

    Write-Host 'Advanced setup checks passed.'
}
finally {
    if (Test-Path -LiteralPath $temporaryDirectory) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }
}
