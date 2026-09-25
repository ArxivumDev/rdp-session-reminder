# Security policy

## Supported version

Security fixes are applied to the latest public release.

## Reporting a vulnerability

Use GitHub's private security-advisory feature for this repository. Do not put
passwords, RDP files, computer names, IP addresses, gateway names, private
diagnostic data, or certificate material in a public issue.

A build or report that asks for a Remote Desktop password outside the Windows
Remote Desktop client should be treated as suspicious.

## Credential boundary

RDP Session Reminder has no credential database, password field, or
multiple-account profile. It does not request, enumerate, read, delete, transmit,
or log Windows credentials. New direct v1.2 profiles use a fixed
`prompt for credentials:i:0` setting and expose no user-selectable persistent
prompt choice. Normal launches let `mstsc.exe` use a credential Windows has
saved for the exact destination when available, or prompt when needed.

**Connect with a different account once** is available only from the saved
connection manager. It adds Windows' `/prompt` option to that process launch and
does not rewrite the shortcut, profile, or RDP file. Windows owns the prompt and
any **Remember me** choice; the app does not edit Credential Manager or prevent
that Windows choice from affecting a later launch.

Imported files and profiles made by older releases preserve their existing RDP
settings, including any prompt preference. The app does not silently rewrite
those connection files.

Windows Credential Manager can treat a hostname, FQDN, IP address, and DNS alias
as different `TERMSRV` targets even when they reach the same physical computer.
That Windows behavior is outside this application's credential model.

An imported RDP file may already contain a user-name or opaque `password 51`
field. Preview reports only whether the field exists and never displays its
value. Import preserves the complete supplied file byte-for-byte, so the private
copy also retains that opaque field. The app does not interpret it or move it
into an application credential store.

## Untrusted RDP input

Imported `.rdp` files are untrusted input. Preview uses a locked, size-bounded
snapshot and bounded line parsing. Computer and gateway names are hidden by
default, and password values are never rendered. The user must explicitly accept
the preview.

The original and private profile copy remain byte-for-byte unchanged. Imported
profiles are not signed by the app and are not rewritten by generated-profile
display, resource, or reminder-editor controls. Users should review every
imported profile before launch.

## Profile and shortcut ownership

App-managed profile directories must be canonical direct children of the app's
profile root. Profile IDs must be canonical GUIDs. Reads, writes, one-time
cleanup, icon creation, diagnostics, profile deletion, and uninstall cleanup
validate paths and reject unsafe reparse points or unexpected objects where
applicable.

Installer and portable-setup payloads are staged and replace destination
directory entries instead of writing through an existing hard link. Application
and Start menu roots, executable destinations, and shortcut destinations reject
directories or reparse points where applicable. Regression tests cover root and
child junctions plus executable and shortcut hard links.

The manager and uninstaller do not scan unrelated folders or drives. Verified
Desktop shortcuts are matched by runtime target and exact profile argument.
Moved copies and unknown files remain for manual review.

## Local RDP publisher trust

The optional local publisher feature creates its signing certificate and
non-exportable private key on the user's own PC. The key must never be committed
or included in a package. Only the certificate's SHA-256 fingerprint is added to
the current user's trusted RDP publisher policy.

The application signs only generated profiles. The trusted-publisher pin applies
to any RDP file signed by that locally held key, so the Windows account holding
it must remain protected. This mechanism is separate from Authenticode trust for
the executable and from the TLS certificate used by a remote computer.

When the app-owned publisher identity record is intact, trust removal deletes
the exact certificate and associated private-key container, verifies both are
gone, and removes only the matching SHA-256 policy pin. It stops safely if
ownership or cleanup cannot be verified. Unrelated certificates, keys, and
publisher entries remain unchanged. If the identity record was removed outside
the app, the uninstaller cannot identify orphaned trust and leaves it for manual
review.

## Executable signing

The build has an optional Authenticode path that selects a code-signing
certificate by thumbprint, requires an HTTPS RFC 3161 timestamp service, signs
before checksums and packaging, and verifies the result with the Windows default
Authenticode policy.

No PFX password is accepted on a process command line. Release builders must use
a protected certificate store or managed signing service. Public trust requires
a certificate chaining to a Windows-trusted code-signing CA.

The default public GitHub Actions workflow provides no signing material.
Therefore its artifacts remain unsigned unless protected signing is explicitly
configured. The self-signed local RDP publisher certificate must never be
presented as executable publisher trust.

## Updater

The updater reads only stable releases from the fixed public GitHub repository.
It uses approved HTTPS GitHub hosts, bounded downloads, exact asset names, and
strict semantic-version and release-metadata parsing. Release notes are displayed
as plain text.

Before launch, an installer must match the exact SHA-256 entry in
`SHA256SUMS.txt` and GitHub's asset digest. The updater rejects reparse-point
download paths, hashes through a held file handle, denies writes, deletion, and
renames, and retains verified directory and installer handles through process
creation. Missing or failed verification deletes the temporary download and
never opens it.

Update checks require no GitHub token and send no RDP data or telemetry. They run
only while setup is open; no updater service, task, or resident process exists.

## Diagnostics

Sanitized reports are created only after a user request. They omit connection
names, endpoint and gateway values, user names, reminder text, credential fields,
and raw RDP data. Reports remain local unless the user separately chooses to
copy or save them.

Executable signature status uses Windows verification with cached revocation
data only, so diagnostic generation does not initiate certificate-network
traffic. An unavailable cache or any invalid status is not reported as trusted.

## Process boundary

The manager, editor, and dialogs live in the visible setup process. Closing an
editor disposes that window; closing setup, or connecting through it, ends the
complete configuration process. The reminder runtime starts only for an app
launch, tracks only the `mstsc.exe` processes created for that launch, and exits
with the launch. No service, remote-side agent, injected DLL, system-wide hook,
listening port, or telemetry is installed.
