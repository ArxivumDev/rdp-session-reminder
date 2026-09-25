# Contributing

Bug reports and focused pull requests are welcome.

Before submitting a change:

1. Keep the runtime local, low-memory, and dependency-free.
2. Do not add telemetry, credential handling, elevation, or remote-side software.
3. Run `.\build.ps1` on Windows and confirm both self-tests pass.
4. Avoid committing personal hostnames, addresses, RDP files, certificates, or logs.
5. Explain user-visible behavior and validation in the pull request.

Good future additions include editing existing profiles, configurable banner
placement, accessibility improvements, localization, code signing, and Windows
Package Manager packaging.
