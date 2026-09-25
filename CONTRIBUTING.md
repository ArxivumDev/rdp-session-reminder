# Contributing

Bug reports and focused pull requests are welcome.

Before submitting a change:

1. Keep the reminder runtime local, small, and dependency-free.
2. Keep the full setup and management UI nonresident; it must release its memory
   when its visible window closes.
3. Do not add telemetry, credential storage, elevation, a service, startup task,
   remote-side software, DLL injection, or a system-wide input/window hook.
4. Leave password and account handling in Windows Remote Desktop. A one-launch
   `mstsc.exe /prompt` action must not become a stored credential profile or
   permanent shortcut setting.
5. Preserve imported `.rdp` files byte-for-byte unless a future feature makes an
   explicit, separately reviewed replacement rather than a silent edit.
6. Validate app-owned paths and reparse points before profile, shortcut, icon,
   one-time, or uninstall cleanup.
7. Run `.\build.ps1` on Windows and confirm all checks pass.
8. Avoid committing personal hostnames, addresses, RDP files, credentials,
   certificates, private keys, diagnostics, or logs.
9. Explain user-visible behavior, process lifetime, privacy effects, and
   validation in the pull request.

UI contributions should preserve the guided common path, label less-common
options plainly, support Dark and Light themes, and honor Windows High Contrast.
Decorative effects must remain static or event-driven inside the visible setup
process; they must not add a polling process or affect the reminder runtime.

Monitor-selection changes must treat `mstsc.exe /l` as the authoritative source
of RDP IDs, account for topology changes, and preserve imported display settings.
The reminder-monitor choice remains separate from the monitors used inside RDP.

Never ship a shared RDP signing private key. Local publisher trust must use a
unique, non-exportable key created for the current Windows user, preserve
unrelated trust entries, and provide a matching verified removal path.

Authenticode release signing must use protected signing material, SHA-256 file
digests, and an RFC 3161 timestamp. Do not pass a PFX password on a process
command line or commit signing material. Checksums and packages must be created
after signing.

Good future additions include localization, Windows Package Manager packaging,
additional keyboard accessibility, and further diagnostics that remain local and
redacted.
