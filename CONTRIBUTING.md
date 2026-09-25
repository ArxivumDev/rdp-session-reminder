# Contributing

Bug reports and focused pull requests are welcome.

Before submitting a change:

1. Keep the runtime local, small, and dependency-free.
2. Do not add telemetry, credential handling, elevation, or remote-side software.
3. Run `.\build.ps1` on Windows and confirm all checks pass.
4. Avoid committing personal hostnames, addresses, RDP files, certificates, or logs.
5. Explain user-visible behavior and validation in the pull request.

Never ship a shared RDP signing private key. Publisher trust must use a unique,
non-exportable key created locally for the current Windows user, preserve
unrelated trusted-publisher entries, and provide a matching removal path.

Good future additions include editing existing profiles, configurable banner
placement, accessibility improvements, localization, code signing, and Windows
Package Manager packaging.
