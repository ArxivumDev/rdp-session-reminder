# Process and privacy model

RDP Session Reminder separates configuration from connection-time work so the
larger interface is never resident in the background.

| Component | When it runs | What it does | When it exits |
| --- | --- | --- | --- |
| Setup and connection manager | Only while its visible window is open | Creates, previews, edits, duplicates, repairs, and removes app-owned profiles and shortcuts | When the user closes it, or after a create-and-connect action completes |
| Reminder runtime | Once for a shortcut or one-time connection | Starts the Windows Remote Desktop client, draws the small local banner, and watches only the RDP processes from that launch | When that launch ends or is cancelled |
| `mstsc.exe` | While Windows Remote Desktop is open | Owns the network connection, authentication, credential prompts, audio, display, and resource redirection | When the Remote Desktop connection closes |

There is no service, tray application, scheduled task, startup item, hidden
connection manager, browser runtime, injected DLL, window hook, or remote-side
agent. Closing setup releases all memory used by the full configuration
interface. During a connection, only the small Win32/GDI reminder runtime is
added to the normal memory used by Windows' `mstsc.exe` client.

## Connection ownership

The manager reads only immediate profile folders under
`%LOCALAPPDATA%\RdpSessionReminder\profiles`. A profile folder name must be a
canonical GUID, and its settings and connection file must resolve inside that
exact folder. The app does not search unrelated folders or drives.

Every permanent shortcut starts the reminder runtime with one profile GUID.
The shortcut still gives the normal double-click connection experience. A
one-time connection instead uses a random GUID under the app's `one-time`
directory, creates no desktop shortcut, and removes that exact temporary profile
after its associated RDP launch ends.

## Imported RDP files

Import preview is local and read-only. It reports the requested display,
gateway, credential-field presence, and redirected resources before a private
copy is made. Password data is never displayed. The original file is never
changed. An imported profile copy remains byte-for-byte unchanged unless the
user explicitly replaces it by creating a different connection.

## Diagnostics

Diagnostics are generated only after the user requests them. Reports describe
app version, profile validity, file hashes, shortcut wiring, monitor
availability, `mstsc.exe`, updater state, and signature state. Connection names,
addresses, user names, gateway names, credentials, and raw RDP file content are
redacted by default. The app can copy or save a report; it never uploads or
sends one.

## Related projects

The connection list, visual labels, monitor chooser, and diagnostic organization
are original implementations informed by public user-interface ideas in
TinyRDP, Taskbar Marker, RoyalApps Community RDP, and MsRdpEx. Their code and
runtime components are not bundled. RDP Session Reminder continues to use the
supported Windows Remote Desktop client rather than replacing or injecting into
it.
