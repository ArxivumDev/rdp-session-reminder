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

- Accepts a computer name, DNS name, IPv4 address, IPv6 address, and optional port
- Gives every generated shortcut its own connection profile, reminder text, and display
- Associates a shortcut with one `.rdp` file without editing the original file
- Lets the user choose which local display shows the reminder
- Creates a uniquely named desktop shortcut with Windows' standard Remote Desktop icon
- Opens the connection through the built-in `mstsc.exe` client
- Shows a click-through, always-on-top reminder on the selected local display
- Keeps the reminder visible while RDP is full-screen, windowed, or minimized
- Closes the reminder automatically after the associated RDP window closes
- Uses a lightweight .NET Framework runtime with Win32/GDI and low idle CPU in testing
- Stores each shortcut's profile under the current user's local application-data folder
- Requires no service, startup task, administrator rights, or remote installation

## Install

1. Download `RdpSessionReminder-Windows.zip` from
   [the latest release](../../releases/latest).
2. Extract the ZIP file.
3. Run `RdpSessionReminderSetup.exe`.
4. Enter the remote computer name or IP address.
5. Optionally choose an existing `.rdp` file, enter the reminder text, and select
   a local display.
6. Choose **Save & Connect**.

Setup copies the two application files to
`%LOCALAPPDATA%\RdpSessionReminder`, saves a new profile, and creates a desktop
shortcut for that profile. Each profile contains its own computer, verified copy
of the selected `.rdp` file, reminder text, and display choice. It affects only
the Remote Desktop session launched by that shortcut; other `.rdp` files and RDP
sessions are not changed or monitored.

Setup always chooses a unique shortcut filename and never overwrites an unrelated
`.lnk` file. To change the target, reminder text, display, or `.rdp` file, run
setup again to create a new shortcut, then delete the old shortcut when you no
longer need it.

The release executables are not code-signed, so Windows may identify the
publisher as unknown. You can review the source and build the application
yourself with the included build script.

## First run

The setup screen asks for:

- **Computer name or IP address:** the same value you would enter in Remote
  Desktop Connection, optionally including a port
- **RDP connection file:** an optional existing `.rdp` file whose settings should
  be used exactly as saved
- **Reminder text:** the short message shown while this shortcut's session is open
- **Display for the reminder:** the local monitor where the notice should appear
- **Desktop shortcut name:** the label shown on the local desktop
- **Full screen:** whether a direct computer-name connection starts full-screen;
  a selected `.rdp` file keeps its own screen settings

The default reminder uses the computer name and a normal ASCII hyphen, for
example `REMOTE SESSION - WORKSTATION-01`. You can replace it with another short
message during setup. Password entry and any decision to remember credentials
remain inside Windows Remote Desktop.

## Create or change a shortcut

Run:

```powershell
& "$env:LOCALAPPDATA\RdpSessionReminder\RdpSessionReminderSetup.exe"
```

You can also start the runtime with `--configure`.

Every setup run creates a separate profile and a new, uniquely named desktop
shortcut. To change a shortcut's computer, `.rdp` file, reminder text, or display,
create its replacement with setup and then delete the old shortcut. Existing
shortcuts and profiles continue to work independently until removed.

## Privacy and security

RDP Session Reminder:

- does not request, read, or transmit passwords
- does not contain telemetry or an update service
- does not open a listening port or provide remote-control functionality
- does not change Remote Desktop security policy
- does not install anything on the remote computer
- saves each shortcut's profile only under the current user's local application-data folder
- copies a selected `.rdp` file byte-for-byte into that profile and never edits
  the original file, its content, or its signature
- applies the reminder only to an RDP session launched by its associated shortcut

Windows Credential Manager may remember credentials if the user chooses that
inside Remote Desktop Connection. This application never accesses those
credentials.

An `.rdp` file is user-supplied content and may contain settings the user placed
in it. Review the file before selecting it. The app copies it unchanged and does
not interpret or transmit its contents beyond passing it to `mstsc.exe`.

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
associated RDP session is active. Its memory use is separate from the much
larger `mstsc.exe` process.

A remote-side reminder is a different design. It can be useful when the banner
must appear inside the remote desktop itself or for connections launched without
this project's shortcut. Such a companion must be installed on the remote PC,
detect whether its Windows session is connected through RDP, and show or hide
its own overlay there. That option consumes memory on the remote PC and is not
part of this release.

## How it works

The generated shortcut supplies its profile ID to the runtime. The runtime reads
only that profile, launches `%SystemRoot%\System32\mstsc.exe` directly with its
format-checked target or copied `.rdp` file, and tracks the newly created
`mstsc.exe` session window. It displays the reminder text with Win32/GDI instead
of loading a browser interface or generated reminder image. No web framework,
background service, or remote-side helper is used.

On a Windows 11 test system, the warmed-up reminder used about 3–4 MB of private
working set and approximately 0% idle CPU. Actual usage varies by Windows
version and display configuration. The memory used by `mstsc.exe` itself is
separate from the reminder.

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
