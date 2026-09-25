# Security policy

## Supported version

Security fixes are applied to the latest release.

## Reporting a vulnerability

Please use GitHub's private security-advisory feature for this repository. Do
not include passwords, RDP files, computer names, IP addresses, or other private
connection details in a public issue.

This project never needs a user's Remote Desktop password. Reports or builds
that request credentials outside the Windows Remote Desktop client should be
treated as suspicious.

## RDP publisher trust

The optional local publisher feature creates its signing certificate and private
key on the user's own PC. The key must be non-exportable and must never be
included in source control or a release package. Only the certificate's SHA-256
fingerprint is added to the current user's trusted `.rdp` publisher policy.

The application signs only the profiles it generates. The trusted-publisher pin
applies to any `.rdp` file signed by that locally held key, which is why the key
must remain protected. This mechanism is separate from Authenticode trust for
the application executable and from the TLS certificate used by a remote
computer. Imported `.rdp` files are untrusted input, remain byte-for-byte
unchanged, and should be reviewed before use.

Trust removal must delete the exact app-owned certificate and its associated
private-key container, verify that both are gone, and remove only the matching
SHA-256 policy pin. It must stop safely if ownership or cleanup cannot be
verified. Unrelated certificates, keys, and trusted-publisher entries must remain
unchanged.

## Updater

The updater reads only stable releases from the fixed public GitHub repository.
It must use approved HTTPS GitHub hosts, bounded downloads, exact release asset
names, and strict semantic-version and release-metadata parsing. Before launch,
an installer must match the exact SHA-256 entry in `SHA256SUMS.txt` and GitHub's
required asset digest, then pass a second hash check immediately before execution.
Release notes are displayed as plain text. Missing or failed verification must
delete the temporary download and must never open it.

Update checks require no GitHub token and send no RDP data or telemetry. The
release executables are not Authenticode-signed, so a verified download still
requires an explicit user confirmation before launch.
