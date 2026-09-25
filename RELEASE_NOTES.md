# RDP Session Reminder v1.0.1

This maintenance release makes the reminder match the connection more reliably
and improves how it follows the Remote Desktop process from launch through exit.

## Fixed in this release

- Direct targets now accept canonical IPv4 addresses with ports, such as
  `10.0.0.25:3390`, while rejecting ambiguous whitespace, control characters,
  non-ASCII port digits, and leading-zero IPv4 forms.
- When an `.rdp` file is selected, setup uses its nonempty
  `alternate full address:s:` value when present and otherwise uses
  `full address:s:`. The Computer field becomes read-only in this mode so its
  displayed target cannot drift from the file's advertised target.
- Setup rechecks the target in the verified profile copy, and the runtime always
  launches that profile's own `connection.rdp`. The runtime also refreshes an
  older profile's auto-generated target label from that copy while preserving
  custom reminder text.
- Each launch now tracks the Remote Desktop processes created for that shortcut
  in a private Windows Job object. The reminder attaches only to a verified
  Remote Desktop process from that launch.
- Cancelling a Remote Desktop security or sign-in prompt now ends the pending
  reminder promptly. Leaving a legitimate prompt open for more than five minutes
  no longer causes the reminder to give up.
- Per-shortcut mutex ownership is handled correctly after stopped or abandoned
  launches.
- Banner dimensions and text now follow the selected display's scaling and
  refresh when that scaling changes.
- Shortcut names that are reserved Windows device names are rejected.

## `.rdp` files and custom labels

Setup reads the selected file locally and interprets only its advertised
connection address to choose the default Computer, shortcut, and reminder labels.
It then copies the `.rdp` file byte-for-byte into that shortcut's private profile
and does not modify the original or the copy. The Reminder text remains
customizable even though the Computer field is read-only in `.rdp` mode.

Every setup run creates a separate profile and desktop shortcut. Its target,
file, reminder text, and display apply only to that shortcut. To change any of
those values, rerun setup, test the replacement shortcut, and delete the old
shortcut when it is no longer needed.

## Local resource use and privacy

The banner is drawn by `RdpSessionReminder.exe` on the Windows PC that starts the
connection. Nothing from this project is installed or run on the remote computer,
so the reminder uses no memory there; the RDP session itself still uses normal
Windows resources on that computer. The setup app exits after creating the
shortcut, and the small reminder process closes when its associated RDP launch
ends.

RDP Session Reminder does not read credentials. Windows Credential Manager may
store credentials when requested in Remote Desktop, and Remote Desktop itself
may retain recently used targets in the current user's connection history. This
application does not access or manage either store.

## Update and uninstall

Close active sessions launched through reminder shortcuts before installing this
update so Windows releases the shared executable. Older file-mode profiles are
migrated from their trusted `.rdp` copy and have automatic labels refreshed at
launch. Existing direct profiles with canonical targets remain compatible;
recreate a direct shortcut if its old target uses an ambiguous form that v1.0.1
now rejects, such as a leading-zero IPv4 address. Each setup run creates a new
shortcut for the values entered.

It is safe to uninstall after the app has closed: delete its generated desktop
shortcuts and `%LOCALAPPDATA%\RdpSessionReminder`. Uninstalling does not change
original `.rdp` files or global Remote Desktop settings.

The release ZIP and SHA-256 checksum file are produced by the successful GitHub
Actions build for the v1.0.1 tag, which also creates GitHub build-provenance
attestations for both files. The executables are not code-signed, so Windows may
identify the publisher as unknown.
