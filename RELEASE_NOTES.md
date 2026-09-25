# RDP Session Reminder v1.0.0

The first public release makes repeat Remote Desktop connections easier to
recognize and launch.

- Enter a computer name or IP address through a simple setup interface
- Choose an existing `.rdp` file, custom reminder text, and the local display used
  for the reminder
- Create separate profiles for separate shortcuts, computers, reminder text, and displays
- Copy a selected `.rdp` file byte-for-byte while leaving the original unchanged
- Create a uniquely named desktop shortcut with the standard Windows Remote
  Desktop icon without overwriting unrelated `.lnk` files
- Display an independent session reminder without tracking the RDP window's position
- Keep the reminder present while the session is full-screen, windowed, or minimized
- Apply each reminder only to the session launched by its associated shortcut
- Use a lightweight .NET Framework reminder with Win32/GDI and minimal idle resource usage
- Keep hostnames and credentials out of the distributed source
- Include setup, profile replacement, safe uninstall, privacy, troubleshooting,
  and build documentation

To change a shortcut's target, reminder text, display, or `.rdp` file, run setup
again to create a new shortcut and then delete the old one. Uninstalling by
deleting the generated shortcuts and `%LOCALAPPDATA%\RdpSessionReminder` does not
change original `.rdp` files or global Remote Desktop settings.

The executables are not code-signed. SHA-256 checksums are published alongside
the release downloads.
