# RDP Session Reminder v1.1.0

This release adds a guided custom `.rdp` profile creator while keeping the
reminder local, lightweight, and scoped to one generated shortcut.

## Custom connection profiles

- Enter a computer name, full DNS name, IP address, or optional port and setup
  writes a private `.rdp` profile for that shortcut.
- Choose the native/current display size or a common smaller resolution.
- Start full screen on one display or span all local displays.
- The connection bar is available in full screen, keyboard shortcuts follow the
  full-screen behavior, and audio keeps the Windows Remote Desktop defaults.
- Profiles use 32-bit color and high visual settings with automatic network and
  bandwidth detection, compression, multimedia optimization, persistent bitmap
  caching, automatic reconnect, and a warning if server authentication fails.

## Resource and sign-in choices

Setup now offers explicit controls for clipboard, drives, location, serial
ports, WebAuthn passkeys or security keys, and smart cards or Windows Hello for
Business. The destination computer or its policy can still refuse a requested
redirection.

An **Always ask for credentials** option tells Windows Remote Desktop to prompt
on every launch, which makes it possible to choose another account instead of
silently using a credential already saved for that destination. The application
does not read, change, or store passwords.

## Optional local RDP publisher trust

Setup can create a unique, non-exportable signing key for the current Windows
user, register only its SHA-256 fingerprint in that user's trusted `.rdp`
publisher policy, and sign newly generated profiles after all settings are
written. Its removal action deletes and verifies the exact app-owned certificate,
associated private key, and SHA-256 pin while preserving unrelated certificates,
keys, and trusted-publisher entries.

This is `.rdp` publisher trust. It does not Authenticode-sign the application,
change SmartScreen reputation, or establish the remote server's TLS identity.
Imported `.rdp` files remain byte-for-byte unchanged and are not re-signed. On
older or unpatched Windows installations that do not support SHA-256 trusted RDP
publisher policy, the standard publisher/resource confirmation can still appear;
the app does not fall back to SHA-1.

Windows omits Location redirection from the protected RDP signature scope on
affected systems. Setup detects the Location-plus-trust combination before it
creates a certificate and asks the user to turn off one of those options.

## Guided progress and updates

Setup now shows progress while it installs its files, writes or verifies the RDP
profile, applies optional publisher trust, saves settings, creates the desktop
shortcut, and starts Remote Desktop. Reminder shortcuts have an empty **Start
in** field, preventing a stale-folder error in Windows shortcut Properties.

The new **Updates** tab supports manual checks and optional checks at most once a
day when setup opens. It shows cumulative notes for every stable release newer
than the installed version, then updates directly to the newest release rather
than running intermediate installers. Before launch, the downloaded installer
must match both `SHA256SUMS.txt` and GitHub's required asset digest. The installer
is checked again immediately before it is opened, and the user must confirm. No
background updater service or scheduled task is installed.

## Compatibility and resource use

Existing v1.0.x shortcuts and profiles remain readable. New direct connections
use their own generated `.rdp` file. Imported profiles keep their original
settings and disable the custom connection controls in setup.

The reminder still runs only on the connecting PC and only while its associated
RDP launch is active. No service, scheduled task, browser runtime, or remote-side
agent is added.

## Installer and preserved connections

The release now includes a per-user installer, a standalone uninstaller, and the
portable ZIP. The installer needs no administrator rights and adds setup and
uninstall entries to the Start menu.

Managed uninstall removes the app binaries, Start menu entries, update settings,
registration, and app-owned RDP publisher trust. By default it preserves all
generated and imported profile copies, settings, and reminder shortcuts. A UTF-8
`README - Saved RDP Connections.txt` note explains that preserved `.lnk`
launchers require a reinstall, preserved `connection.rdp` files can be opened
directly, and users can manually delete the retained app-data folder and moved
copies.

An explicitly selected and separately confirmed option removes canonical app
profile connection/settings files and verified reminder shortcuts currently in
the Desktop folder. Original imported files, unrelated shortcuts, extra profile
files, and copies moved elsewhere remain untouched. The uninstaller never scans
other folders or drives for connection files or shortcuts.

The installer, standalone uninstaller, portable ZIP, and SHA-256 checksum file
are produced by the successful GitHub Actions build for the v1.1.0 tag. Tagged
workflows also create GitHub build-provenance attestations. The release
executables remain unsigned, so Windows can identify the application publisher
as unknown.
