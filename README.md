# RDP Session Reminder

RDP Session Reminder is a lightweight Windows launcher for people who regularly
connect to remote computers. Choose an existing `.rdp` connection file or enter
a computer name, select the local display, and create a shortcut that keeps a
small reminder visible until that Remote Desktop session closes.

## What it solves

A full-screen Remote Desktop session can make it easy to forget which computer
you are controlling. That can lead to commands being run, files being saved, or
settings being changed on the wrong machine. This utility keeps the remote target
visible on the local screen for the lifetime of the RDP window.

## Features

- Accepts a computer name, DNS name, canonical IPv4 address, IPv6 address, and optional port
- Gives every generated shortcut its own connection profile, reminder text, and display
- Associates one shortcut with one `.rdp` file without editing the original file
- Reads the file's advertised target locally so the default reminder matches the connection
- Lets the user choose which local display shows the reminder
- Creates a uniquely named desktop shortcut with Windows' standard Remote Desktop icon
- Opens the connection through the built-in `mstsc.exe` client
- Shows a click-through, always-on-top reminder on the selected local display
- Keeps the reminder visible while RDP is full-screen, windowed, or minimized
- Closes the reminder automatically after the associated RDP window closes
- Uses a small .NET Framework runtime with Win32/GDI instead of a browser interface
- Stores each shortcut's profile under the current user's local application-data folder
- Requires no service, startup task, administrator rights, or remote installation

## Install

1. Download `RdpSessionReminder-Windows.zip` from
   [the latest release](../../releases/latest).
2. Extract the ZIP file.
3. Run `RdpSessionReminderSetup.exe`.
4. Enter the remote computer name or IP address, or choose an existing `.rdp`
   connection file.
5. Enter the reminder text and select a local display.
6. Choose **Save & Connect**.

Setup copies the two application files to
`%LOCALAPPDATA%\RdpSessionReminder`, saves a new profile, and creates a desktop
shortcut for that profile. Each profile contains its own computer, verified copy
of the selected `.rdp` file, reminder text, and display choice. It affects only
the Remote Desktop session launched by that shortcut; other `.rdp` files and RDP
sessions are not changed or monitored.

When a `.rdp` file is selected, setup reads its advertised connection address
locally. It uses a nonempty `alternate full address:s:` value when present and
otherwise uses `full address:s:`. This value supplies the default shortcut and
reminder labels. Setup then copies the selected file byte-for-byte into the new
profile and verifies the copy. It does not modify either the original file or
the profile copy. At launch, the runtime reads the same advertised target from
the trusted profile copy so older auto-generated labels stay aligned, then
passes that copy to `mstsc.exe`.

Setup always chooses a unique shortcut filename and never overwrites an unrelated
`.lnk` file. To change the target, reminder text, display, or `.rdp` file, run
setup again to create a new shortcut, then delete the old shortcut when you no
longer need it.

The release executables are not code-signed, so Windows may identify the
publisher as unknown. You can review the source and build the application
yourself with the included build script.

To verify the downloaded ZIP against the release checksum in PowerShell:

```powershell
$expected = (Select-String -Path .\SHA256SUMS.txt `
    -Pattern 'RdpSessionReminder-Windows\.zip$').Line.Substring(0, 64)
$actual = (Get-FileHash .\RdpSessionReminder-Windows.zip -Algorithm SHA256).Hash
$actual.ToLowerInvariant() -eq $expected
```

The result should be `True`. The other checksum lines cover the two executables
after the ZIP is extracted. A tagged v1.0.1 download can also be checked against
GitHub's build attestation:

```powershell
gh attestation verify .\RdpSessionReminder-Windows.zip `
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
- **Full screen:** whether a direct computer-name connection starts full-screen;
  a selected `.rdp` file keeps its own screen settings

The default reminder uses the computer name and a normal ASCII hyphen, for
example `REMOTE SESSION - WORKSTATION-01`. The Reminder text remains editable
even when the Computer field is locked by `.rdp` file mode, so the visible label
can say anything useful to you. Password entry and any decision to remember
credentials remain inside Windows Remote Desktop.

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

Before installing a newer version, close any reminder sessions launched through
the app. A running reminder can keep the shared executable open while setup is
trying to replace it.

## Privacy and security

RDP Session Reminder:

- does not request, read, or transmit passwords
- does not contain telemetry or an update service
- does not open a listening port or provide remote-control functionality
- does not change Remote Desktop security policy
- does not install anything on the remote computer
- saves each shortcut's profile only under the current user's local application-data folder
- reads a selected `.rdp` file locally during setup, and its trusted profile copy
  at launch, interpreting only the advertised connection address
- copies the selected file byte-for-byte into that profile
- never edits the original `.rdp` file or the verified profile copy
- applies the reminder only to an RDP session launched by its associated shortcut

Windows Credential Manager may remember credentials if the user chooses that
inside Remote Desktop Connection. Remote Desktop itself may also retain recently
used targets in the current Windows user's connection history. RDP Session
Reminder does not access or manage either store.

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
only that profile, uses the profile's own `connection.rdp` copy when file mode is
enabled, and launches `%SystemRoot%\System32\mstsc.exe` directly. It tracks the
Remote Desktop process created for that launch and displays the reminder text
with Win32/GDI instead of loading a browser interface or generated reminder
image. No web framework, background service, or remote-side helper is used.

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
built-in smoke tests, and writes executables, a release ZIP, and SHA-256 checksums
to `dist`.

GitHub Actions runs the same build script on Windows and uploads the release ZIP
and checksum file as build artifacts. Tagged releases should publish the files
from the successful workflow run for that tag so the downloadable package is
tied to the tagged source. Tagged workflows also create GitHub build-provenance
attestations for the ZIP and checksum file. The published SHA-256 file lets users
verify the download independently.

## Limitations

- The reminder tracks sessions launched through its generated shortcuts.
- If the saved display is unavailable, the banner falls back to the primary display.
- The application currently targets the classic `mstsc.exe` client.
- Release binaries are not code-signed.

## Uninstall

Close sessions launched through the app, delete its generated desktop shortcuts,
and remove:

```text
%LOCALAPPDATA%\RdpSessionReminder
```

This is safe to do at any time after the app has closed. The app never changes
the original `.rdp` files or global Remote Desktop settings. No service,
scheduled task, driver, or machine-wide setting is left behind.

## License

RDP Session Reminder is available under the [MIT License](LICENSE).
