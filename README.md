# RDP Session Reminder

RDP Session Reminder is a lightweight Windows launcher and guided `.rdp` profile
creator for people who regularly connect to remote computers. Enter a computer
name or choose an existing `.rdp` file, select the local display, and create a
shortcut that keeps a small reminder visible until that Remote Desktop session
closes.

## What it solves

A full-screen Remote Desktop session can make it easy to forget which computer
you are controlling. That can lead to commands being run, files being saved, or
settings being changed on the wrong machine. This utility keeps the remote target
visible on the local screen for the lifetime of the RDP window.

## Features

- Accepts a computer name, DNS name, canonical IPv4 address, IPv6 address, and optional port
- Creates a private `.rdp` profile for each new direct connection
- Gives every generated shortcut its own connection profile, reminder text, and display
- Associates one shortcut with one `.rdp` file without editing the original file
- Reads the file's advertised target locally so the default reminder matches the connection
- Lets the user choose which local display shows the reminder
- Offers native/current and common smaller remote-desktop resolutions
- Supports full screen on one display or all local displays
- Keeps the full-screen connection bar available and applies sensible display,
  keyboard, audio, network-detection, bitmap-cache, reconnect, and authentication defaults
- Lets the user opt in to clipboard, drives, location, serial ports, WebAuthn,
  and smart-card or Windows Hello for Business redirection
- Can require Windows Remote Desktop to ask for credentials on every launch
- Can create a unique local RDP publisher certificate, trust its SHA-256
  fingerprint for the current Windows user, and sign newly generated `.rdp` profiles
- Creates a uniquely named desktop shortcut with Windows' standard Remote Desktop icon
- Shows each setup step while the profile, optional signature, settings, shortcut,
  and connection are prepared
- Checks the official GitHub releases on demand or, when enabled, at most once per
  day when setup opens; shows every release note after the installed version and
  updates directly to the newest stable release
- Opens the connection through the built-in `mstsc.exe` client
- Shows a click-through, always-on-top reminder on the selected local display
- Keeps the reminder visible while RDP is full-screen, windowed, or minimized
- Closes the reminder automatically after the associated RDP window closes
- Uses a small .NET Framework runtime with Win32/GDI instead of a browser interface
- Stores each shortcut's profile under the current user's local application-data folder
- Requires no service, startup task, administrator rights, or remote installation

## Install

For a normal per-user installation:

1. Download `RdpSessionReminder-Installer.exe` and `SHA256SUMS.txt` from
   [the latest release](../../releases/latest).
2. Optionally verify the installer against `SHA256SUMS.txt`.
3. Run `RdpSessionReminder-Installer.exe`. It installs the app for the current
   Windows account without administrator rights and adds setup and uninstall
   entries to the Start menu.
4. Open the setup screen when the installer offers to launch it.
5. Enter the remote computer name, full DNS name, or IP address. You can instead
   choose an existing `.rdp` connection file to preserve its settings unchanged.
6. Choose the remote display and resource preferences, then enter the reminder
   text and select the local display for the notice.
7. Optionally choose **Trust my generated RDP shortcuts**. This creates a local,
   non-exportable signing key and trusts only its SHA-256 fingerprint for your
   Windows account.
8. Choose **Create & Connect**.

For portable use, download and extract `RdpSessionReminder-Windows.zip`, then run
`RdpSessionReminderSetup.exe`. Portable setup copies the runtime and setup files
to `%LOCALAPPDATA%\RdpSessionReminder` when it creates the first shortcut, but it
does not register the managed uninstaller.

The installer places its three application files in
`%LOCALAPPDATA%\RdpSessionReminder` and creates a Start menu folder named
**RDP Session Reminder**. Setup saves each new profile separately and creates a
desktop shortcut for it. Each profile contains its own computer, private `.rdp`
file, reminder text, and display choice. It affects only the Remote Desktop
session launched by that shortcut; other `.rdp` files and RDP sessions are not
changed or monitored.

When a `.rdp` file is selected, setup reads its advertised connection address
locally. It uses a nonempty `alternate full address:s:` value when present and
otherwise uses `full address:s:`. This value supplies the default shortcut and
reminder labels. Setup then copies the selected file byte-for-byte into the new
profile and verifies the copy. It does not modify either the original file or
the profile copy. At launch, the runtime reads the same advertised target from
the trusted profile copy so older auto-generated labels stay aligned, then
passes that copy to `mstsc.exe`.

For a direct connection, setup writes a new profile `.rdp` file from the choices
shown in the setup window. For an imported `.rdp` file, setup keeps the original
and its private profile copy byte-for-byte unchanged; the custom connection
controls are disabled because those values already belong to the imported file.

Setup always chooses a unique shortcut filename and never overwrites an unrelated
`.lnk` file. To change the target, reminder text, display, or connection settings,
run setup again to create a new shortcut, then delete the old shortcut when you
no longer need it. Reminder shortcuts use a blank **Start in** field because all
required paths are absolute. Their Windows Properties tabs therefore remain
usable even if the application folder is later moved or removed.

The release executables are not Authenticode code-signed, so Windows may identify
the application publisher as unknown. The optional local certificate is used by
the app only to sign generated `.rdp` files, while Windows trusts any `.rdp` file
signed by that key. It does not change SmartScreen or establish trust in the
application executable. You can review the source and build the application
yourself with the included build script.

To verify a downloaded release file against the release checksum in PowerShell,
replace the filename below if you chose the portable ZIP or standalone
uninstaller:

```powershell
$expected = (Select-String -Path .\SHA256SUMS.txt `
    -Pattern 'RdpSessionReminder-Installer\.exe$').Line.Substring(0, 64)
$actual = (Get-FileHash .\RdpSessionReminder-Installer.exe -Algorithm SHA256).Hash
$actual.ToLowerInvariant() -eq $expected
```

The result should be `True`. The checksum file covers the installer, standalone
uninstaller, portable ZIP, and the two executables inside the ZIP. A tagged
download can also be checked against GitHub's build attestation:

```powershell
gh attestation verify .\RdpSessionReminder-Installer.exe `
    --repo ArxivumDev/rdp-session-reminder
```

## First run

The setup screen asks for:

- **Computer name or IP address:** for a direct connection, the same value you
  would enter in Remote Desktop Connection, optionally including a port. When a
  valid `.rdp` file is selected, setup fills this field from the file and makes
  it read-only so the displayed target cannot drift from the file's advertised
  target.
- **RDP connection file:** an optional existing `.rdp` file whose settings are
  copied and used exactly as saved. Setup prefers its nonempty
  `alternate full address:s:` value, then `full address:s:`, for the target shown
  in the Computer field.
- **Reminder text:** the short message shown while this shortcut's session is open
- **Display for the reminder:** the local monitor where the notice should appear
- **Desktop shortcut name:** the label shown on the local desktop
- **Full screen:** whether the generated direct connection starts full-screen
- **Remote desktop size:** the native/current display size by default, with
  common smaller sizes such as 1920 x 1080 available for windowed connections
- **Use all displays:** whether the remote session spans all local monitors;
  the connection bar is always available when full screen is used
- **Always ask for credentials:** whether Windows should prompt on every launch,
  which lets the user choose another account instead of automatically using a
  saved target credential
- **Local resources:** opt-in choices for clipboard, drives, location, serial
  ports, WebAuthn passkeys/security keys, and smart cards or Windows Hello for Business

The generated profile always uses 32-bit color and high visual settings with
automatic connection-quality and bandwidth detection. Persistent bitmap caching,
compression, multimedia playback optimization, and automatic reconnect are on.
Audio uses the Windows Remote Desktop defaults: playback on the connecting PC and
microphone redirection off. Server-authentication failure displays a warning.
Keyboard shortcuts are applied to the remote session only while it is full screen.

The default reminder uses the computer name and a normal ASCII hyphen, for
example `REMOTE SESSION - WORKSTATION-01`. The Reminder text remains editable
even when the Computer field is locked by `.rdp` file mode, so the visible label
can say anything useful to you. Password entry and any decision to remember
credentials remain inside Windows Remote Desktop.

### Saved usernames and choosing another account

Windows Remote Desktop stores remembered credentials by destination, usually as
`TERMSRV/computer-name` in Windows Credential Manager. When a saved credential
exists, the username box can be filled and read-only even when the `.rdp` file
does not contain a username. This behavior belongs to Windows and can affect
multiple shortcuts that use the same destination.

Choose **Always ask for credentials** when creating the shortcut if you want an
account choice each time. For an existing saved credential, use the **edit or
delete** link in Remote Desktop Connection or open Windows Credential Manager.
The application never reads, changes, or stores the password.

### Local RDP publisher trust

**Trust my generated RDP shortcuts** creates a unique self-signed code-signing
certificate in the current user's personal certificate store. Its private key is
non-exportable. Setup registers only that certificate's SHA-256 fingerprint in
the current user's trusted `.rdp` publisher policy and signs new profiles after
all connection settings have been written.

This trust applies to every `.rdp` file signed by that private key, so protect the
Windows account that holds it. It does not trust the remote server's TLS identity
and does not Authenticode-sign or change SmartScreen reputation for the application
executables. Imported `.rdp` files remain unchanged and are not re-signed.

The managed uninstaller invokes the same app-owned trust removal automatically.
For portable/manual cleanup, use **Remove local RDP publisher trust** in setup
before deleting the application if you enabled this option. Removal deletes only
the certificate, its associated private key, and the SHA-256 policy entry created
by this application. The app takes extra care to run a complete cleanup check,
verify those exact items are gone, and preserve every unrelated certificate, key,
and trusted-publisher entry. If verification fails, managed uninstall stops before
deleting the application files so cleanup can be retried safely.
On an older or unpatched Windows installation without SHA-256 RDP publisher policy
support, the standard publisher/resource confirmation can still appear; the app
does not fall back to deprecated SHA-1 trust.

Windows currently omits Location redirection from an `.rdp` publisher signature
on affected systems. Setup therefore asks you to turn off either **Location** or
local publisher trust before creating the shortcut; both choices remain available
individually.

## Create or change a shortcut

Run:

```powershell
& "$env:LOCALAPPDATA\RdpSessionReminder\RdpSessionReminderSetup.exe"
```

You can also start the runtime with `--configure`.

Every setup run creates a separate profile and a new, uniquely named desktop
shortcut. The profile is associated only with that generated shortcut; it does
not change other RDP shortcuts or sessions. To change its computer, `.rdp` file,
reminder text, or display, rerun setup with the desired values, test the new
shortcut, and then delete the old shortcut. Existing shortcuts and profiles
continue to work independently until removed.

## Updates

Open setup and select the **Updates** tab. **Check for updates now** reads stable
release metadata from this project's public GitHub repository. Automatic checks
are optional, occur only when setup opens, and run at most once per day. The app
installs no updater service, startup task, or resident background process.

When several versions are newer, setup lists the cumulative release notes for
every intervening stable release, including small patch releases, then downloads
one installer for the newest version. Installing that newest release includes all
earlier changes; the app does not run a chain of intermediate installers.

Before it offers to open an update, setup requires the downloaded installer hash
to match both the exact `RdpSessionReminder-Installer.exe` entry in
`SHA256SUMS.txt` and GitHub's SHA-256 asset digest. All three values must match.
The installer is checked again immediately before launch. Updates still require
a clear confirmation because the release executables are not Authenticode-signed.
No GitHub account, token, RDP credential, or telemetry is sent.

Before installing a newer version, close any reminder sessions launched through
the app. A running reminder can keep the shared executable open while setup is
trying to replace it.

## Privacy and security

RDP Session Reminder:

- does not request, read, or transmit passwords
- does not contain telemetry or an update service
- does not open a listening port or provide remote-control functionality
- does not install anything on the remote computer
- saves each shortcut's profile only under the current user's local application-data folder
- reads a selected `.rdp` file locally during setup, and its private profile copy
  at launch, interpreting only the advertised connection address
- creates a new `.rdp` file only for a direct connection configured in setup
- copies a selected existing file byte-for-byte and never edits the original or copy
- applies the reminder only to an RDP session launched by its associated shortcut
- changes the current user's trusted `.rdp` publisher policy only when the user
  explicitly installs local publisher trust, and provides a matching removal action

Windows Credential Manager may remember credentials if the user chooses that
inside Remote Desktop Connection. Remote Desktop itself may also retain recently
used targets in the current Windows user's connection history. RDP Session
Reminder can ask Windows to prompt on every connection and can open Credential
Manager, but it does not enumerate, read, delete, or store credentials.

An `.rdp` file is user-supplied content and may contain settings the user placed
in it. Review the file before selecting it. Apart from reading
`alternate full address:s:` and `full address:s:` locally to determine the
advertised target, the app copies it unchanged and passes the profile copy to
`mstsc.exe`. The app does not transmit the file itself.

The application does not enable Remote Desktop on the destination computer. The
remote system must already allow RDP connections, and the user must have
permission to connect.

## Where the reminder runs

The reminder is drawn by `RdpSessionReminder.exe` on the Windows PC you connect
**from**. `RdpSessionReminderSetup.exe` only creates the profile and desktop
shortcut, then exits. Windows' `mstsc.exe` client makes the Remote Desktop
connection. The banner is not created by `mstsc.exe`, and this project installs
or runs nothing on the computer you connect **to**.

This client-side design keeps the remote computer free of another resident
process. The small reminder process runs only on the connecting PC while its
associated RDP session is active. RDP Session Reminder therefore uses no memory
on the remote host; the Remote Desktop session itself still uses normal Windows
resources there. The reminder's local memory use is separate from the much larger
`mstsc.exe` process.

A remote-side reminder is a different design. It can be useful when the banner
must appear inside the remote desktop itself or for connections launched without
this project's shortcut. Such a companion must be installed on the remote PC,
detect whether its Windows session is connected through RDP, and show or hide
its own overlay there. That option consumes memory on the remote PC and is not
part of this release.

## How it works

The generated shortcut supplies its profile ID to the runtime. The runtime reads
only that profile, launches its private `connection.rdp` through
`%SystemRoot%\System32\mstsc.exe`, and tracks the Remote Desktop process created
for that launch. It displays the reminder text with Win32/GDI instead of loading
a browser interface or generated reminder image. No web framework, background
service, or remote-side helper is used.

The reminder avoids a browser framework, generated image, service, or remote-side
agent. Its actual memory and CPU use varies by Windows version and display
configuration. The memory used by `mstsc.exe` itself is separate from the
reminder.

## Command-line options

```text
RdpSessionReminder.exe --configure
RdpSessionReminder.exe --profile PROFILE_ID
```

Setup writes the `--profile` value into each generated shortcut. Users normally
start the shortcut and do not need to enter this command manually.

## Troubleshooting

- **No desktop icon:** confirm desktop icons are enabled and look on the local
  desktop rather than inside the remote session.
- **Reminder appears on another display:** rerun setup and choose the desired
  display, create the replacement shortcut, and delete the old shortcut. If the
  chosen display is disconnected, the app falls back to the primary one.
- **RDP file is missing:** rerun setup and select the file again. Setup keeps a
  verified byte-for-byte copy inside the new shortcut's profile.
- **Username is filled and cannot be edited:** Windows has a saved credential for
  that destination. Use the blue **edit or delete** link in Remote Desktop,
  manage the matching Windows Credential Manager entry, or create a shortcut
  with **Always ask for credentials** enabled.
- **Publisher/resource confirmation still appears:** confirm that local publisher
  trust is installed and that Windows has current security updates with SHA-256
  trusted-RDP-publisher support. Imported `.rdp` files are deliberately not signed.
- **Reminder text or target needs changing:** rerun setup to create a new shortcut,
  confirm it works, and then delete the old shortcut.
- **Updating fails because a file is in use:** close active RDP sessions launched
  from reminder shortcuts, allow their reminders to close, and run setup again.
- **Connection fails:** open Remote Desktop Connection directly and confirm the
  destination computer already accepts RDP connections.

## Requirements

- Windows 10 or Windows 11
- .NET Framework 4.8
- Microsoft Remote Desktop Connection (`mstsc.exe`)
- A destination computer already configured to accept RDP connections

## Build from source

Open PowerShell in the repository and run:

```powershell
.\build.ps1
```

The script uses the .NET Framework C# compiler included with Windows, runs the
built-in smoke tests, and writes the installer, standalone uninstaller, portable
ZIP, individual executables, and SHA-256 checksums to `dist`.

GitHub Actions runs the same build script on Windows and uploads the installer,
standalone uninstaller, portable ZIP, and checksum file as build artifacts.
Tagged releases should publish the files from the successful workflow run for
that tag so the downloads are tied to the tagged source. Tagged workflows also
create GitHub build-provenance attestations for those files. The published
SHA-256 file lets users verify each download independently.

## Limitations

- The reminder tracks sessions launched through its generated shortcuts.
- If the saved display is unavailable, the banner falls back to the primary display.
- The application currently targets the classic `mstsc.exe` client.
- Release binaries are not code-signed.

## Uninstall

Close sessions launched through the app, then use **Uninstall RDP Session
Reminder** in the Start menu or run `RdpSessionReminder-Uninstaller.exe` from
the release. The managed uninstaller removes the app binaries, Start menu
entries, uninstall registration, and only the publisher certificate and policy
entry owned by this app. Certificate cleanup also removes and verifies the
associated private key while preserving unrelated certificates, keys, and policy
entries.

By default, the uninstaller leaves generated and imported profile copies, their
`connection.rdp` and `settings.ini` files, and reminder shortcuts untouched. It
writes this UTF-8 cleanup note in the retained folder:

```text
%LOCALAPPDATA%\RdpSessionReminder\README - Saved RDP Connections.txt
```

Reminder `.lnk` shortcuts need the app to be reinstalled before they work again,
while any preserved `connection.rdp` can still be opened directly in Windows
Remote Desktop. The uninstaller does not scan the hard drive for `.rdp` files or
shortcuts that were copied or moved elsewhere. If you later want to remove all
retained connections, manually delete the entire
`%LOCALAPPDATA%\RdpSessionReminder` folder and any desktop or moved copies after
saving anything you want to keep.

The uninstall screen also offers **Also remove my app-managed saved connection
copies and verified original Desktop reminder shortcuts**. Selecting it requires
a second confirmation. It removes only `connection.rdp` and `settings.ini` from
canonical app profile folders and shortcuts currently in the Desktop folder whose
target and profile arguments prove they belong to this app. Original imported
`.rdp` files, unrelated shortcuts, extra files in profile folders, and copies
moved elsewhere are preserved. The app never searches other folders or drives.

For a portable installation without a registered uninstaller, first use setup's
**Remove local RDP publisher trust** action if it was enabled. After the app is
closed, manually remove the application folder and any shortcuts you no longer
want. The app installs no service, scheduled task, driver, or remote-side
component.

## License

RDP Session Reminder is available under the [MIT License](LICENSE).
