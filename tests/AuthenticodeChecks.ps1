[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$RepoRoot
)

$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path ([IO.Path]::GetFullPath($RepoRoot)) `
    'tools\Authenticode.ps1'
if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
    throw 'The optional Authenticode signing helper is missing.'
}

. $scriptPath

if (-not (Test-RdpReminderCertificateThumbprint `
        -Thumbprint '0123456789abcdef0123456789ABCDEF01234567') -or
    (Test-RdpReminderCertificateThumbprint -Thumbprint '') -or
    (Test-RdpReminderCertificateThumbprint -Thumbprint ('A' * 39)) -or
    (Test-RdpReminderCertificateThumbprint -Thumbprint ('A' * 41)) -or
    (Test-RdpReminderCertificateThumbprint `
        -Thumbprint '0123456789abcdef0123456789abcdef0123456Z')) {
    throw 'Authenticode certificate thumbprint validation is not strict.'
}

$source = [IO.File]::ReadAllText($scriptPath)
foreach ($required in @(
    '/fd SHA256',
    '/tr $TimestampUrl',
    '/td SHA256',
    'verify /pa /all',
    '/sha1 $normalizedThumbprint /s My'
)) {
    if (-not $source.Contains($required)) {
        throw "The signing helper is missing the required control: $required"
    }
}
if ($source -match '(?i)/p\s+\$|CertificatePassword|\.pfx') {
    throw 'The signing helper must not put a PFX password on a process command line.'
}

Write-Host 'Authenticode signing checks passed.'
