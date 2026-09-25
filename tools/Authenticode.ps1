Set-StrictMode -Version 2.0

function Test-RdpReminderCertificateThumbprint {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string]$Thumbprint
    )

    return $Thumbprint -match '\A[0-9A-Fa-f]{40}\z'
}

function Find-RdpReminderSignTool {
    [CmdletBinding()]
    param()

    $candidates = [Collections.Generic.List[string]]::new()
    foreach ($registryPath in @(
        'HKLM:\SOFTWARE\Microsoft\Windows Kits\Installed Roots',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows Kits\Installed Roots'
    )) {
        try {
            $kitsRoot = (Get-ItemProperty -LiteralPath $registryPath `
                -Name KitsRoot10 -ErrorAction Stop).KitsRoot10
            if (-not [string]::IsNullOrWhiteSpace($kitsRoot)) {
                $binDirectory = Join-Path $kitsRoot 'bin'
                if (Test-Path -LiteralPath $binDirectory -PathType Container) {
                    Get-ChildItem -LiteralPath $binDirectory -Directory |
                        Sort-Object Name -Descending |
                        ForEach-Object {
                            $candidate = Join-Path $_.FullName 'x64\signtool.exe'
                            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                                $candidates.Add($candidate)
                            }
                        }
                }
            }
        }
        catch {
        }
    }

    $fromPath = Get-Command signtool.exe -CommandType Application `
        -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $fromPath) {
        $candidates.Add($fromPath.Source)
    }

    $result = $candidates | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($result)) {
        throw 'SignTool was not found. Install the Windows SDK signing tools.'
    }
    return [IO.Path]::GetFullPath($result)
}

function Invoke-RdpReminderAuthenticodeSigning {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string[]]$Path,

        [Parameter(Mandatory)]
        [string]$CertificateThumbprint,

        [Parameter(Mandatory)]
        [ValidatePattern('^https://')]
        [string]$TimestampUrl,

        [string]$SignToolPath
    )

    if (-not (Test-RdpReminderCertificateThumbprint `
            -Thumbprint $CertificateThumbprint)) {
        throw 'The Authenticode certificate thumbprint must contain exactly 40 hexadecimal characters.'
    }

    if ([string]::IsNullOrWhiteSpace($SignToolPath)) {
        $SignToolPath = Find-RdpReminderSignTool
    }
    $SignToolPath = [IO.Path]::GetFullPath($SignToolPath)
    if (-not (Test-Path -LiteralPath $SignToolPath -PathType Leaf)) {
        throw "SignTool does not exist: $SignToolPath"
    }

    $normalizedThumbprint = $CertificateThumbprint.ToUpperInvariant()
    foreach ($item in $Path) {
        $resolvedPath = [IO.Path]::GetFullPath($item)
        if (-not (Test-Path -LiteralPath $resolvedPath -PathType Leaf)) {
            throw "The file to sign does not exist: $resolvedPath"
        }

        & $SignToolPath sign /sha1 $normalizedThumbprint /s My /fd SHA256 `
            /tr $TimestampUrl /td SHA256 /d 'RDP Session Reminder' $resolvedPath
        if ($LASTEXITCODE -ne 0) {
            throw "Authenticode signing failed for $resolvedPath."
        }

        & $SignToolPath verify /pa /all $resolvedPath
        if ($LASTEXITCODE -ne 0) {
            throw "Authenticode verification failed for $resolvedPath."
        }
    }
}
