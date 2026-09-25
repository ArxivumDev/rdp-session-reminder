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
    if ($version -ne '1.0.0.0') {
        throw "Unexpected file version for $([IO.Path]::GetFileName($executable)): $version"
    }
}

Write-Host 'Smoke tests passed.'
