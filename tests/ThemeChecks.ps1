[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$DistDirectory
)

$ErrorActionPreference = 'Stop'
$resolvedDist = [IO.Path]::GetFullPath($DistDirectory)
$setupPath = Join-Path $resolvedDist 'RdpSessionReminderSetup.exe'
$runtimePath = Join-Path $resolvedDist 'RdpSessionReminder.exe'
$setupAssembly = [Reflection.Assembly]::LoadFile($setupPath)
$runtimeAssembly = [Reflection.Assembly]::LoadFile($runtimePath)
$storeType = $setupAssembly.GetType('UiPreferenceStore', $true)
$themeType = $setupAssembly.GetType('UiThemeMode', $true)
$paletteType = $setupAssembly.GetType('SetupPalette', $true)
$visualThemeType = $setupAssembly.GetType('SetupVisualTheme', $true)
$cursorHaloType = $setupAssembly.GetType('SetupCursorHalo', $true)
$dimensionalButtonType = $setupAssembly.GetType('DimensionalButton', $true)
$setupFormType = $setupAssembly.GetType('SetupForm', $true)
$heroType = $setupAssembly.GetType('SetupHeroPanel', $true)
$guidedTabType = $setupAssembly.GetType('GuidedTabControl', $true)
$bannerTestFormType = $setupAssembly.GetType('BannerTestForm', $true)
$bannerSampleType = $setupAssembly.GetType('BannerSampleControl', $true)
$staticFlags = [Reflection.BindingFlags]'Static,Public,NonPublic'
$instanceFlags = [Reflection.BindingFlags]'Instance,Public,NonPublic'
$dark = [Enum]::Parse($themeType, 'Dark')
$light = [Enum]::Parse($themeType, 'Light')
$loadFrom = $storeType.GetMethod('LoadFrom', $staticFlags)
$saveTo = $storeType.GetMethod('SaveTo', $staticFlags)
$parse = $storeType.GetMethod('Parse', $staticFlags)
$serialize = $storeType.GetMethod('Serialize', $staticFlags)
$setTheme = $paletteType.GetMethod('SetTheme', $staticFlags)
$applyTheme = $visualThemeType.GetMethod('Apply', $staticFlags)
$getPrimaryColors = $dimensionalButtonType.GetMethod(
    'GetPrimaryColors', $staticFlags)
$composeCursorBitmap = $cursorHaloType.GetMethod(
    'ComposeCursorBitmap', $staticFlags)
$captureCursorBitmap = $cursorHaloType.GetMethod(
    'CaptureCursorBitmap', $staticFlags)
$isMeaningfulCursorBitmap = $cursorHaloType.GetMethod(
    'IsMeaningfulCursorBitmap', $staticFlags)
$isSupportedCursorSize = $cursorHaloType.GetMethod(
    'IsSupportedCursorSize', $staticFlags)
$isStockCursorSetting = $cursorHaloType.GetMethod(
    'IsStockCursorSetting', $staticFlags)
$forControlCursor = $cursorHaloType.GetMethod(
    'ForControl', $staticFlags)
$translateHotspot = $cursorHaloType.GetMethod(
    'TranslateHotspot', $staticFlags)
$getIconInfo = $cursorHaloType.GetMethod('GetIconInfo', $staticFlags)
$deleteObject = $cursorHaloType.GetMethod('DeleteObject', $staticFlags)
$iconInfoType = $cursorHaloType.GetNestedType(
    'IconInfo', [Reflection.BindingFlags]'NonPublic')
$getSubtitleColor = $heroType.GetMethod('GetSubtitleColor', $staticFlags)
$getTabTextColor = $guidedTabType.GetMethod(
    'GetTabTextColor', $staticFlags)
$createHighContrastPalette = $paletteType.GetMethod(
    'CreateHighContrastPalette', $staticFlags)

function Get-RelativeLuminance([Drawing.Color]$Color) {
    $linear = @()
    foreach ($component in @($Color.R, $Color.G, $Color.B)) {
        $value = $component / 255.0
        $linear += if ($value -le 0.03928) {
            $value / 12.92
        }
        else {
            [Math]::Pow(($value + 0.055) / 1.055, 2.4)
        }
    }
    return 0.2126 * $linear[0] + 0.7152 * $linear[1] +
        0.0722 * $linear[2]
}

function Get-ContrastRatio([Drawing.Color]$First, [Drawing.Color]$Second) {
    $firstLuminance = Get-RelativeLuminance $First
    $secondLuminance = Get-RelativeLuminance $Second
    $lighter = [Math]::Max($firstLuminance, $secondLuminance)
    $darker = [Math]::Min($firstLuminance, $secondLuminance)
    return ($lighter + 0.05) / ($darker + 0.05)
}

function Get-CursorHotspot([Windows.Forms.Cursor]$Cursor) {
    $nativeInfo = [Activator]::CreateInstance($iconInfoType)
    $arguments = [object[]]@($Cursor.Handle, $nativeInfo)
    if (-not $getIconInfo.Invoke($null, $arguments)) {
        throw 'Windows did not return cursor hotspot information.'
    }
    $nativeInfo = $arguments[1]
    $mask = $iconInfoType.GetField(
        'MaskBitmap', $instanceFlags).GetValue($nativeInfo)
    $color = $iconInfoType.GetField(
        'ColorBitmap', $instanceFlags).GetValue($nativeInfo)
    try {
        return [Drawing.Point]::new(
            $iconInfoType.GetField(
                'HotspotX', $instanceFlags).GetValue($nativeInfo),
            $iconInfoType.GetField(
                'HotspotY', $instanceFlags).GetValue($nativeInfo))
    }
    finally {
        if ($mask -ne [IntPtr]::Zero) {
            $deleteObject.Invoke($null, @($mask)) | Out-Null
        }
        if ($color -ne [IntPtr]::Zero) {
            $deleteObject.Invoke($null, @($color)) | Out-Null
        }
    }
}

if ($null -eq $getPrimaryColors) {
    throw 'The primary-button color contract is missing.'
}
$darkPalette = $paletteType.GetField(
    'DarkPalette', $staticFlags).GetValue($null)
$lightPalette = $paletteType.GetField(
    'LightPalette', $staticFlags).GetValue($null)
foreach ($paletteCase in @($darkPalette, $lightPalette)) {
    foreach ($state in @('normal', 'hot', 'pressed')) {
        $isHot = $state -ne 'normal'
        $isPressed = $state -eq 'pressed'
        $arguments = [object[]]@(
            $paletteCase, $isHot, $isPressed,
            [Drawing.Color]::Empty, [Drawing.Color]::Empty,
            [Drawing.Color]::Empty, [Drawing.Color]::Empty)
        $getPrimaryColors.Invoke($null, $arguments) | Out-Null
        foreach ($fill in @($arguments[3], $arguments[4])) {
            if ((Get-ContrastRatio $fill $arguments[6]) -lt 4.5) {
                throw 'Primary-button text contrast fell below 4.5:1.'
            }
        }
    }
}

if ($null -eq $getSubtitleColor -or $null -eq $getTabTextColor -or
    $null -eq $createHighContrastPalette) {
    throw 'The theme-specific text color contracts are missing.'
}
$lightSubtitle = $getSubtitleColor.Invoke($null, @($lightPalette))
foreach ($heroBackground in @(
    [Drawing.Color]::White,
    [Drawing.Color]::FromArgb(226, 235, 248)
)) {
    if ((Get-ContrastRatio $lightSubtitle $heroBackground) -lt 4.5) {
        throw 'The Light hero subtitle contrast fell below 4.5:1.'
    }
}
$highContrastPalette = $createHighContrastPalette.Invoke($null, @())
$selectedHighContrastTab = $getTabTextColor.Invoke(
    $null, @($highContrastPalette, $true))
$unselectedHighContrastTab = $getTabTextColor.Invoke(
    $null, @($highContrastPalette, $false))
if ($selectedHighContrastTab.ToArgb() -ne
        [Drawing.SystemColors]::HighlightText.ToArgb() -or
    $unselectedHighContrastTab.ToArgb() -ne
        [Drawing.SystemColors]::ControlText.ToArgb()) {
    throw 'High Contrast tab text does not preserve actionable system colors.'
}

if ($runtimeAssembly.GetType('UiPreferenceStore', $false) -ne $null -or
    $runtimeAssembly.GetType('SetupPalette', $false) -ne $null -or
    $runtimeAssembly.GetType('SetupVisualTheme', $false) -ne $null -or
    $runtimeAssembly.GetType('SetupCursorHalo', $false) -ne $null) {
    throw 'Setup theme code must not be included in the reminder runtime.'
}

if ($null -eq $composeCursorBitmap -or $null -eq $captureCursorBitmap -or
    $null -eq $isMeaningfulCursorBitmap -or
    $null -eq $isSupportedCursorSize -or
    $null -eq $isStockCursorSetting -or $null -eq $forControlCursor -or
    $null -eq $translateHotspot -or
    $null -eq $getIconInfo -or $null -eq $deleteObject -or
    $null -eq $iconInfoType) {
    throw 'The themed cursor composition contract is missing.'
}

$translated = $translateHotspot.Invoke($null, @([int]3, [int]5))
if ($translated.X -ne 29 -or $translated.Y -ne 31) {
    throw 'The themed cursor did not translate its source hotspot with padding.'
}

if (-not $isSupportedCursorSize.Invoke($null,
        @([Drawing.Size]::new(32, 32))) -or
    $isSupportedCursorSize.Invoke($null,
        @([Drawing.Size]::new(48, 48)))) {
    throw 'The cursor cue did not preserve oversized accessibility pointers.'
}

$windowsDirectory = [Environment]::GetEnvironmentVariable('WINDIR')
$stockArrow = Join-Path $windowsDirectory 'Cursors\aero_arrow.cur'
if (-not $isStockCursorSetting.Invoke($null,
        @([string]'Arrow', [string]'', [string]$windowsDirectory)) -or
    -not $isStockCursorSetting.Invoke($null,
        @([string]'Arrow', [string]$stockArrow,
            [string]$windowsDirectory)) -or
    $isStockCursorSetting.Invoke($null,
        @([string]'Arrow', [string]'C:\Pointers\custom.cur',
            [string]$windowsDirectory))) {
    throw 'The cursor cue did not distinguish stock and custom pointer files.'
}

$opaqueRectangle = [Drawing.Bitmap]::new(32, 32)
try {
    $opaqueGraphics = [Drawing.Graphics]::FromImage($opaqueRectangle)
    try {
        $opaqueGraphics.Clear([Drawing.Color]::White)
    }
    finally {
        $opaqueGraphics.Dispose()
    }
    if ($isMeaningfulCursorBitmap.Invoke($null, @($opaqueRectangle))) {
        throw 'An opaque cursor-capture rectangle was accepted as a pointer.'
    }
}
finally {
    $opaqueRectangle.Dispose()
}

$semanticSourcePaths = @()
$semanticCursorCases = [ordered]@{
    Arrow = [Windows.Forms.Cursors]::Default
    IBeam = [Windows.Forms.Cursors]::IBeam
    Hand = [Windows.Forms.Cursors]::Hand
}
foreach ($semanticCase in $semanticCursorCases.GetEnumerator()) {
    $semanticBitmap = $captureCursorBitmap.Invoke($null,
        @($semanticCase.Value))
    try {
        if (-not $isMeaningfulCursorBitmap.Invoke(
                $null, @($semanticBitmap))) {
            throw ("The {0} cursor did not yield a meaningful source shape." -f
                $semanticCase.Key)
        }
        $semanticPath = Join-Path ([IO.Path]::GetTempPath()) (
            'RdpSessionReminder-Cursor-Source-{0}.png' -f
            $semanticCase.Key)
        $semanticBitmap.Save($semanticPath,
            [Drawing.Imaging.ImageFormat]::Png)
        $semanticSourcePaths += $semanticPath
    }
    finally {
        $semanticBitmap.Dispose()
    }
}

$sourceCursor = [Windows.Forms.Cursors]::Default
$sourceBitmap = $captureCursorBitmap.Invoke($null, @($sourceCursor))
$darkComposite = $null
$lightComposite = $null
try {
    $darkComposite = $composeCursorBitmap.Invoke($null, @(
        $sourceCursor, $true, [int]0, [int]0))
    $lightComposite = $composeCursorBitmap.Invoke($null, @(
        $sourceCursor, $false, [int]0, [int]0))

    if (-not $isMeaningfulCursorBitmap.Invoke($null, @($sourceBitmap))) {
        throw 'The stock cursor capture did not produce a usable source shape.'
    }

    $cursorRenderDirectory = [IO.Path]::GetTempPath()
    $sourceRenderPath = Join-Path $cursorRenderDirectory (
        'RdpSessionReminder-Cursor-Source.png')
    $darkRenderPath = Join-Path $cursorRenderDirectory (
        'RdpSessionReminder-Cursor-DarkHalo.png')
    $lightRenderPath = Join-Path $cursorRenderDirectory (
        'RdpSessionReminder-Cursor-LightCloud.png')
    $sourceBitmap.Save($sourceRenderPath, [Drawing.Imaging.ImageFormat]::Png)
    $darkComposite.Save($darkRenderPath,
        [Drawing.Imaging.ImageFormat]::Png)
    $lightComposite.Save($lightRenderPath,
        [Drawing.Imaging.ImageFormat]::Png)

    $expectedWidth = $sourceCursor.Size.Width + 52
    $expectedHeight = $sourceCursor.Size.Height + 52
    if ($darkComposite.Width -ne $expectedWidth -or
        $darkComposite.Height -ne $expectedHeight -or
        $lightComposite.Width -ne $expectedWidth -or
        $lightComposite.Height -ne $expectedHeight) {
        throw 'The themed cursor composition did not preserve padded source dimensions.'
    }

    $sourceOpaque = 0
    $sourcePreserved = 0
    $sourceVisible = 0
    $maximumSourceAlpha = 0
    for ($y = 0; $y -lt $sourceBitmap.Height; $y++) {
        for ($x = 0; $x -lt $sourceBitmap.Width; $x++) {
            $sourcePixel = $sourceBitmap.GetPixel($x, $y)
            $maximumSourceAlpha = [Math]::Max(
                $maximumSourceAlpha, $sourcePixel.A)
            if ($sourcePixel.A -gt 0) {
                $sourceVisible++
            }
            if ($sourcePixel.A -lt 240) {
                continue
            }
            $sourceOpaque++
            if ($darkComposite.GetPixel($x + 26, $y + 26).ToArgb() -eq
                    $sourcePixel.ToArgb() -and
                $lightComposite.GetPixel($x + 26, $y + 26).ToArgb() -eq
                    $sourcePixel.ToArgb()) {
                $sourcePreserved++
            }
        }
    }
    if ($sourceOpaque -lt 5 -or
        $sourcePreserved -lt [Math]::Floor($sourceOpaque * 0.8)) {
        throw ('The themed cursor composition did not preserve the real ' +
            "system cursor pixels ($sourcePreserved of $sourceOpaque opaque " +
            "pixels matched; $sourceVisible visible; maximum alpha " +
            "$maximumSourceAlpha).")
    }

    $greenPixels = 0
    $cloudPixels = 0
    $differentPixels = 0
    for ($y = 0; $y -lt $darkComposite.Height; $y++) {
        for ($x = 0; $x -lt $darkComposite.Width; $x++) {
            $darkPixel = $darkComposite.GetPixel($x, $y)
            $lightPixel = $lightComposite.GetPixel($x, $y)
            if ($darkPixel.A -ge 40 -and
                $darkPixel.G -gt $darkPixel.R + 35 -and
                $darkPixel.G -gt $darkPixel.B + 35) {
                $greenPixels++
            }
            if ($lightPixel.A -ge 40 -and
                $lightPixel.B -gt $lightPixel.G + 20 -and
                $lightPixel.B -gt $lightPixel.R + 60) {
                $cloudPixels++
            }
            if ($darkPixel.ToArgb() -ne $lightPixel.ToArgb()) {
                $differentPixels++
            }
        }
    }
    if ($greenPixels -lt 20 -or $cloudPixels -lt 20 -or
        $differentPixels -lt 50) {
        throw 'The Dark halo and Light cloud cursor cues are not visibly distinct.'
    }

    Write-Host "Cursor source render: $sourceRenderPath"
    foreach ($semanticSourcePath in $semanticSourcePaths) {
        Write-Host "Semantic cursor render: $semanticSourcePath"
    }
    Write-Host "Dark cursor render: $darkRenderPath"
    Write-Host "Light cursor render: $lightRenderPath"
}
finally {
    $sourceBitmap.Dispose()
    if ($null -ne $darkComposite) {
        $darkComposite.Dispose()
    }
    if ($null -ne $lightComposite) {
        $lightComposite.Dispose()
    }
}

$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) (
    'RdpSessionReminderTheme-' + [guid]::NewGuid().ToString('N'))
try {
    [IO.Directory]::CreateDirectory($temporaryDirectory) | Out-Null
    $preferencePath = Join-Path $temporaryDirectory 'ui-settings.ini'

    if ($loadFrom.Invoke($null, @([string]$preferencePath)).ToString() -ne
        'Dark') {
        throw 'A missing setup-theme preference must default to Dark.'
    }

    foreach ($invalid in @(
        '',
        'Theme=',
        'Theme=Blue',
        "Theme=Light`nUnknown=value`n",
        "Theme=Light`nTheme=Dark`n"
    )) {
        [IO.File]::WriteAllText($preferencePath, $invalid,
            [Text.UTF8Encoding]::new($false))
        if ($loadFrom.Invoke($null, @([string]$preferencePath)).ToString() -ne
            'Dark') {
            throw "Invalid preference content did not fall back to Dark: $invalid"
        }
    }

    [IO.File]::WriteAllBytes($preferencePath, [byte[]]::new(4097))
    if ($loadFrom.Invoke($null, @([string]$preferencePath)).ToString() -ne
        'Dark') {
        throw 'An oversized setup-theme preference must default to Dark.'
    }

    $saveTo.Invoke($null, @([string]$preferencePath, $light)) | Out-Null
    if ($loadFrom.Invoke($null, @([string]$preferencePath)).ToString() -ne
            'Light' -or
        $parse.Invoke($null, @([string]'Theme=LIGHT')).ToString() -ne
            'Light' -or
        $serialize.Invoke($null, @($light)) -notmatch '^Theme=Light') {
        throw 'The Light setup-theme preference did not round-trip.'
    }
    $savedBytes = [IO.File]::ReadAllBytes($preferencePath)
    if ($savedBytes.Length -ge 3 -and $savedBytes[0] -eq 0xEF -and
        $savedBytes[1] -eq 0xBB -and $savedBytes[2] -eq 0xBF) {
        throw 'The setup-theme preference must be UTF-8 without a BOM.'
    }
    if (@(Get-ChildItem -LiteralPath $temporaryDirectory -Filter '*.new-*').Count) {
        throw 'The setup-theme preference left a temporary file behind.'
    }

    $saveTo.Invoke($null, @([string]$preferencePath, $dark)) | Out-Null
    if ($loadFrom.Invoke($null, @([string]$preferencePath)).ToString() -ne
        'Dark') {
        throw 'The Dark setup-theme preference did not round-trip.'
    }

    $formConstructor = $setupFormType.GetConstructor(
        [Reflection.BindingFlags]'Instance,NonPublic', $null,
        [Type[]]@([string]), $null)
    if ($null -eq $formConstructor) {
        throw 'The setup form theme-test constructor is missing.'
    }

    [IO.File]::Delete($preferencePath)
    $form = $formConstructor.Invoke(@([string]$preferencePath))
    try {
        $subscriptionField = $setupFormType.GetField(
            'systemPreferenceSubscribed', $instanceFlags)
        $refreshSystemTheme = $setupFormType.GetMethod(
            'RefreshForSystemPreferences', $instanceFlags)
        if (-not $subscriptionField.GetValue($form) -or
            $null -eq $refreshSystemTheme) {
            throw 'The setup form is not listening for live system-theme changes.'
        }
        $themeCombo = $setupFormType.GetField(
            'themeCombo', $instanceFlags).GetValue($form)
        $computerText = $setupFormType.GetField(
            'computerText', $instanceFlags).GetValue($form)
        $tabs = $setupFormType.GetField(
            'tabs', $instanceFlags).GetValue($form)
        if ($themeCombo.Items.Count -ne 2 -or
            $themeCombo.Items[0].ToString() -ne 'Dark' -or
            $themeCombo.Items[1].ToString() -ne 'Light' -or
            $themeCombo.SelectedItem.ToString() -ne 'Dark') {
            throw 'The setup appearance selector is not Dark-by-default.'
        }

        $computerText.Text = 'theme-state-test'
        $tabs.SelectedIndex = 1
        $themeCombo.SelectedIndex = 1
        if ($computerText.Text -ne 'theme-state-test' -or
            $tabs.SelectedIndex -ne 1 -or
            $loadFrom.Invoke($null,
                @([string]$preferencePath)).ToString() -ne 'Light') {
            throw 'Changing the setup theme lost form state or was not saved.'
        }

        $canvas = $paletteType.GetProperty(
            'Canvas', $staticFlags).GetValue($null, $null)
        $surface = $paletteType.GetProperty(
            'Surface', $staticFlags).GetValue($null, $null)
        $input = $paletteType.GetProperty(
            'Input', $staticFlags).GetValue($null, $null)
        if ($form.BackColor.ToArgb() -ne $canvas.ToArgb() -or
            $tabs.TabPages[0].BackColor.ToArgb() -ne $surface.ToArgb() -or
            $computerText.BackColor.ToArgb() -ne $input.ToArgb()) {
            throw 'The selected setup palette was not applied to representative controls.'
        }

        $form.BackColor = [Drawing.Color]::Magenta
        $tabs.TabPages[0].BackColor = [Drawing.Color]::Magenta
        $computerText.BackColor = [Drawing.Color]::Magenta
        $refreshSystemTheme.Invoke($form, @()) | Out-Null
        if ($form.BackColor.ToArgb() -ne $canvas.ToArgb() -or
            $tabs.TabPages[0].BackColor.ToArgb() -ne $surface.ToArgb() -or
            $computerText.BackColor.ToArgb() -ne $input.ToArgb()) {
            throw 'A live system-theme refresh did not repaint standard controls.'
        }

        $setTheme.Invoke($null, @($dark)) | Out-Null
        $expectedDarkCursor = $forControlCursor.Invoke($null, @(
            $computerText, $true,
            -not [Windows.Forms.SystemInformation]::HighContrast))
        $applyTheme.Invoke($null, @($form)) | Out-Null
        $sourceTextHotspot = Get-CursorHotspot (
            [Windows.Forms.Cursors]::IBeam)
        $darkCursor = $computerText.Cursor
        $darkCursorHandle = $darkCursor.Handle
        if ($darkCursorHandle -ne $expectedDarkCursor.Handle) {
            throw 'Dark setup mode did not apply its eligible cursor resource.'
        }
        $setTheme.Invoke($null, @($light)) | Out-Null
        $expectedLightCursor = $forControlCursor.Invoke($null, @(
            $computerText, $false,
            -not [Windows.Forms.SystemInformation]::HighContrast))
        $applyTheme.Invoke($null, @($form)) | Out-Null
        $lightCursor = $computerText.Cursor
        $lightCursorHandle = $lightCursor.Handle
        if ($lightCursorHandle -ne $expectedLightCursor.Handle) {
            throw 'Light setup mode did not apply its eligible cursor resource.'
        }
        if ($expectedDarkCursor.Handle -ne
                [Windows.Forms.Cursors]::IBeam.Handle) {
            if ($expectedLightCursor.Handle -eq
                    [Windows.Forms.Cursors]::IBeam.Handle -or
                $lightCursorHandle -eq $darkCursorHandle) {
                throw 'Eligible Dark and Light cursor resources were not distinct.'
            }
            $darkHotspot = Get-CursorHotspot $darkCursor
            $lightHotspot = Get-CursorHotspot $lightCursor
            if ($darkHotspot.X -ne $sourceTextHotspot.X + 26 -or
                $darkHotspot.Y -ne $sourceTextHotspot.Y + 26 -or
                $lightHotspot.X -ne $sourceTextHotspot.X + 26 -or
                $lightHotspot.Y -ne $sourceTextHotspot.Y + 26) {
                throw 'The themed cursor did not preserve and translate the system click hotspot.'
            }
        }

        $link = [Windows.Forms.LinkLabel]::new()
        $panel = [Windows.Forms.Panel]::new()
        try {
            $form.Controls.Add($link)
            $form.Controls.Add($panel)
            $expectedLinkCursor = $forControlCursor.Invoke($null, @(
                $link, $false,
                -not [Windows.Forms.SystemInformation]::HighContrast))
            $expectedPanelCursor = $forControlCursor.Invoke($null, @(
                $panel, $false,
                -not [Windows.Forms.SystemInformation]::HighContrast))
            $applyTheme.Invoke($null, @($form)) | Out-Null
            if ($link.Cursor.Handle -ne $expectedLinkCursor.Handle -or
                $panel.Cursor.Handle -ne $expectedPanelCursor.Handle -or
                $link.Cursor.Handle -eq $computerText.Cursor.Handle -or
                $panel.Cursor.Handle -eq $computerText.Cursor.Handle -or
                $link.Cursor.Handle -eq $panel.Cursor.Handle) {
                throw 'The Light cloud cue did not preserve hand, text, and default pointer semantics.'
            }
            if ((Get-ContrastRatio $link.ActiveLinkColor $form.BackColor) -lt
                    4.5) {
                throw 'The Light active-link contrast fell below 4.5:1.'
            }
        }
        finally {
            $form.Controls.Remove($link)
            $form.Controls.Remove($panel)
            $link.Dispose()
            $panel.Dispose()
        }
    }
    finally {
        if ($null -ne $form) {
            $form.Dispose()
            if ($subscriptionField.GetValue($form)) {
                throw 'Disposing setup did not release the system-theme event.'
            }
        }
    }

    $reopened = $formConstructor.Invoke(@([string]$preferencePath))
    try {
        $themeCombo = $setupFormType.GetField(
            'themeCombo', $instanceFlags).GetValue($reopened)
        if ($themeCombo.SelectedItem.ToString() -ne 'Light') {
            throw 'The setup theme was not restored for the current user.'
        }
    }
    finally {
        if ($null -ne $reopened) {
            $reopened.Dispose()
        }
    }

    $sample = [Activator]::CreateInstance($bannerSampleType, $true)
    try {
        $backgroundField = $bannerSampleType.GetField(
            'BannerBackColor', $instanceFlags)
        $foregroundField = $bannerSampleType.GetField(
            'BannerForeColor', $instanceFlags)
        $payloadBackground = [Drawing.Color]::FromArgb(18, 52, 86)
        $payloadForeground = [Drawing.Color]::FromArgb(250, 240, 230)
        $backgroundField.SetValue($sample, $payloadBackground)
        $foregroundField.SetValue($sample, $payloadForeground)
        $setTheme.Invoke($null, @($dark)) | Out-Null
        $applyTheme.Invoke($null, @($sample)) | Out-Null
        if ($backgroundField.GetValue($sample).ToArgb() -ne
                $payloadBackground.ToArgb() -or
            $foregroundField.GetValue($sample).ToArgb() -ne
                $payloadForeground.ToArgb()) {
            throw 'Applying a setup theme changed the configured banner colors.'
        }
    }
    finally {
        if ($null -ne $sample) {
            $sample.Dispose()
        }
    }

    $previewBackground = [Drawing.Color]::FromArgb(12, 34, 56)
    $previewForeground = [Drawing.Color]::FromArgb(245, 246, 247)
    $setTheme.Invoke($null, @($light)) | Out-Null
    $preview = [Activator]::CreateInstance($bannerTestFormType, @(
        [string]'REMOTE SESSION - TEST', $previewBackground,
        $previewForeground, [string]'Medium', [int]90,
        [string]'BottomRight', [int]20))
    try {
        $previewLabel = $preview.Controls[0]
        $expectedPreviewCursor = $forControlCursor.Invoke($null, @(
            $previewLabel, $false,
            -not [Windows.Forms.SystemInformation]::HighContrast))
        if ($preview.BackColor.ToArgb() -ne $previewBackground.ToArgb() -or
            $previewLabel.ForeColor.ToArgb() -ne $previewForeground.ToArgb()) {
            throw 'Applying the preview cursor cue changed reminder payload colors.'
        }
        if ($previewLabel.Cursor.Handle -ne $expectedPreviewCursor.Handle) {
            throw 'The reminder preview did not preserve cursor eligibility.'
        }
    }
    finally {
        if ($null -ne $preview) {
            $preview.Dispose()
        }
    }
}
finally {
    if ([IO.Directory]::Exists($temporaryDirectory)) {
        [IO.Directory]::Delete($temporaryDirectory, $true)
    }
}

Write-Host 'Theme checks passed.'
