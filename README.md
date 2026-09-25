# RDP Session Reminder

RDP Session Reminder is a lightweight Windows launcher and guided Remote Desktop
profile creator. It gives each connection a normal double-click shortcut and
shows a small reminder on the local PC while that Remote Desktop launch is
active.

A full-screen RDP session can make it easy to forget which computer is under
control. The reminder keeps the target visible on the computer you connect from,
without installing software on the remote computer.

## Quick start

1. Download and run `RdpSessionReminder-Installer.exe` from the
   [latest release](../../releases/latest).
2. Open **RDP Session Reminder Setup**.
3. Enter the computer name, FQDN, or IP address.
4. Follow the four steps. Recommended values are already selected.
5. Choose **Create shortcut and connect**.

When app-created connections exist, setup next opens on **Saved connections**.
Double-click a row or select **Connect** to start RDP directly. Setup then closes
completely.

New direct shortcuts provide the same experience without opening setup. Windows
Remote Desktop uses a saved credential for that exact target when one is
available and prompts when needed. The app has no credential profiles, account
vault, or password field.

## Highlights

- A professional four-step workflow for connection, display, resources, and
  reminder choices
- Dark and Light setup themes, with Dark selected for a new installation
- Static dimensional branding and controls with no animation or resident UI
- Theme-matched pointer cues inside setup: a green halo in Dark mode and a
  pale blue-and-white cloud in Light mode
- A saved-connections list for direct double-click or **Connect** launches
- One reusable desktop shortcut per app-created profile
- An optional one-time connection that creates no shortcut
- Full screen, native or smaller resolutions, all monitors, or a selected
  edge-connected subset of monitors
- A separate local monitor choice for the reminder
- Banner corner, vertical distance from the chosen edge, text size, opacity,
  color preset, custom colors, optional short label, and optional idle dimming
- Recommended resource defaults plus clearly labeled less-common choices
- A locked, local preview before an existing `.rdp` file is imported
- Windows Remote Desktop, neutral reminder, Work, Production, Test, and Personal
  shortcut icons
- Local sanitized diagnostics that can be copied or saved by the user
- Manual and optional once-daily update checks with cumulative release notes
- Optional local RDP publisher trust for generated profiles
- Optional Authenticode signing support for release builders
- No service, tray process, startup item, scheduled task, browser runtime,
  injected DLL, window hook, telemetry, or remote-side component

## Guided setup

The main workflow keeps common choices visible and labels less-common settings
without making them part of the normal path.

### 1. Connection

Enter a computer name, FQDN, canonical IP address, and optional port. Setup
suggests the reminder text, shortcut name, and familiar Windows Remote Desktop
icon.

Importing an existing `.rdp` file is optional. Setup stages a locked snapshot and
shows a read-only preview before accepting it. The original remains unchanged.

### 2. Displays

Choose the local display for the reminder, whether RDP should open full screen,
and the remote desktop resolution. The default uses the current native size.

A direct generated profile can use one display, all displays, or a selected
subset. **Choose specific monitors** shows the physical layout, numbers every
display, lets the user identify displays on screen, and keeps the reminder
monitor as a separate choice.

Generated profiles always use 32-bit color, keep the full-screen connection bar
available, apply Windows key combinations remotely only in full screen, play
remote audio on the connecting PC, detect connection quality, use persistent
bitmap caching, reconnect after a dropped connection, and warn when server
authentication fails.

### 3. Resources

The defaults favor a useful connection with limited redirection:

| Resource | Default |
| --- | --- |
| Clipboard | On |
| WebAuthn passkeys and security keys | On |
| Local drives | Off |
| Printers | Off |
| Microphone | Off |
| Location | Off |
| Serial and COM ports | Off |
| Smart cards or Windows Hello for Business | Off |

Generated profiles also explicitly keep MTP/PTP devices, cameras, and other USB
device redirection off. The destination computer or its policy can still reject
a requested resource.

Local RDP publisher trust is available here as a less-common option. It is
separate from trust in the app executable.

### 4. Reminder

Use the full reminder text or an optional short label. Choose Default, Work,
Personal, Test, Production, or custom colors; any screen corner; a vertical
distance from the chosen edge; Small, Medium, or Large text; 35–100 percent
opacity; and optional dimming after local input has been idle. A preview and
**Test reminder** action show the result before creation.

The suggested reminder is:

    REMOTE SESSION - COMPUTER-NAME

It uses a normal ASCII hyphen.

At the final step, choose:

- **Create shortcut and connect** for a reusable shortcut and saved app profile.
- **Connect once** for a temporary profile with no shortcut. The runtime removes
  the exact temporary profile after RDP closes when its safety checks can verify
  the path. If safe cleanup cannot be verified, it leaves the files and reports
  the problem.

Setup shows progress while it installs or updates files, writes or verifies the
profile, applies optional publisher trust, saves reminder settings, creates the
shortcut when requested, and starts Windows Remote Desktop.

## Saved connections

When saved profiles exist, returning users land on **Saved connections**. Select
one row and use:

- **Connect**, Enter, or a double-click to start the session immediately
- **Customize reminder...** to change the shortcut name, local reminder display,
  shortcut icon, and banner appearance
- **Duplicate** to create an independent copy
- **More > Connect with a different account once...** to ask Windows for an
  account for that launch only
- **More > Repair desktop shortcut** to recreate its shortcut and selected icon
- **More > Create sanitized diagnostics** to inspect or save a local report
- **More > Delete app profile...** to remove the app-owned profile

The reminder editor changes `settings.ini` and recreates the verified desktop
shortcut. It leaves the profile's `connection.rdp` unchanged. A moved shortcut
is left alone because the app does not search the drive for copies.

The manager, editor, and dialogs are part of the visible setup process. Closing
the editor disposes that window; closing setup, or connecting through it,
releases the complete configuration process. There is no hidden manager process.

## Credentials and account choice

RDP Session Reminder does not ask for, enumerate, read, delete, transmit, or
store Windows credentials. New direct v1.2 profiles use a fixed
`prompt for credentials:i:0` default rather than offering a persistent account
choice in the app. Their shortcut and normal **Connect** action let Windows
Remote Desktop use a credential saved for that exact destination when one is
available, commonly under a `TERMSRV/target` entry in Windows Credential
Manager. Otherwise, Windows prompts for sign-in.

The app does not create several account choices for one saved connection. If a
different account is needed, open the main interface and use **Saved connections
> More > Connect with a different account once...**. That action adds Windows'
standard prompt option only to the current launch. It does not alter the
shortcut or profile. Windows owns the prompt and any **Remember me** choice, so
the app does not prevent Windows from saving or changing a credential for later
connections.

Imported RDP files and profiles created by older releases keep their existing
`connection.rdp` settings unchanged, including a saved prompt preference. Create
a new direct profile to adopt the streamlined v1.2 sign-in behavior.

Windows treats the exact target text as the credential target. A short host
name, FQDN, IP address, and DNS alias can all reach the same physical computer
while still appearing as different `TERMSRV` targets to Windows. Use one
consistent target name when one saved sign-in is desired.

An imported `.rdp` file can contain a user-name field or an opaque `password 51`
field. Preview reports only whether those fields are present and never displays
their values. Because import preserves the supplied file byte-for-byte, its
private profile copy also preserves any such field; the app does not interpret
it or add it to an app credential store.

## Monitor selection details

The monitor chooser labels each local display with a friendly number and an RDP
ID. **Identify displays** briefly shows the friendly number on every monitor.
**Show official RDP IDs** runs `mstsc.exe /l`.

The IDs reported by `mstsc.exe /l` are authoritative for the current Windows
display topology. The visual chooser follows native monitor enumeration, but
monitor IDs can change when displays, docks, graphics drivers, or topology
change. Verify the IDs with `mstsc.exe /l` when exact placement matters.

Windows requires a selected multi-monitor set to form one edge-connected group.
The app validates that layout. Windows treats the first selected ID as the
primary display inside the remote session.

Selected-monitor settings are written only to direct profiles generated by the
app. An imported `.rdp` file keeps its existing display settings unchanged. If
the saved local reminder display is unavailable at launch, the reminder falls
back to the primary display.

## Importing an RDP file

Import preview is local and read-only. Before setup uses a selected file, it:

1. Opens a bounded snapshot with changes and deletion denied during the copy.
2. Rejects files larger than 4 MiB.
3. Summarizes display, gateway presence, credential-field presence, resource
   redirection, and authentication behavior.
4. Hides computer and gateway names until **Show computer and gateway names** is
   selected.
5. Requires **Use this file** before continuing.

Setup then copies the staged bytes into a private profile and verifies the copy.
It never modifies the original. It reads only the advertised target needed for
the label, preferring a nonempty `alternate full address:s:` and then
`full address:s:`. The imported profile copy remains byte-for-byte unchanged,
and generated-profile controls do not rewrite it.

An imported RDP file is user-supplied content. Review the preview before use.

## Themes and accessibility

The setup interface offers **Dark** and **Light**. When the per-user
`%LOCALAPPDATA%\RdpSessionReminder\ui-settings.ini` file is missing or invalid,
setup starts in Dark mode. A valid choice is saved for the current Windows user
and restored the next time setup opens.

When Windows High Contrast is active, system High Contrast colors override the
custom palette. Theme changes affect setup, the manager, the editor, and their
dialogs only. They do not change banner colors or add work to the reminder
runtime.

The dimensional app mark, headings, tabs, and buttons are painted statically.
Setup uses a green cursor halo in Dark mode and a pale blue-and-white cloud
behind the cursor in Light mode. These app-scoped resources keep the arrow,
text, and link pointer roles distinct. They retain usable system-cursor pixels
and use matching stock shapes only when Windows exposes a blank cursor mask.
Windows High Contrast, oversized pointers, and custom cursor schemes remain
unchanged. The cues use no polling timer, system-wide hook, overlay window, or
background process, end when setup closes, and add no work to the RDP session or
reminder runtime.

No animation timer or background theme process remains after the window closes.

## Where the reminder runs

`RdpSessionReminderSetup.exe` creates and manages profiles, then exits.
`RdpSessionReminder.exe` is the small Win32/GDI reminder runtime. Windows'
`mstsc.exe` owns the RDP connection, authentication, display, audio, and
resource redirection.

The reminder runs only on the PC used to start the connection and only for that
launch. It tracks the `mstsc.exe` processes created for the launch, shows the
click-through local banner while the session is active, and exits when that RDP
launch ends or is cancelled. It remains visible on the local desktop whether
the RDP window is full screen, windowed, or minimized.

Nothing runs on the remote computer. Setup uses no memory after it closes.
During a session, reminder memory is separate from the larger Windows Remote
Desktop client and from resources used inside the remote session.

## Desktop shortcuts and app files

Each permanent connection is stored under:

    %LOCALAPPDATA%\RdpSessionReminder\profiles\<profile-id>

Its shortcut contains only the app runtime path and profile ID. It has a blank
**Start in** field so Windows shortcut Properties remain usable if a prior
working folder is removed.

Generated icon choices are written only to the app-owned icon directory after
path and reparse-point checks. The standard Windows Remote Desktop icon is the
recommended default.

Setup reserves a unique shortcut name and does not overwrite an unrelated
`.lnk` file. Each profile affects only its shortcut and manager row; other RDP
files and sessions are not monitored.

## Updates

Open **Updates** in setup. **Check for updates now** reads stable release
metadata from this public GitHub repository. Automatic checks are optional, run
only when setup opens, and occur at most once per day. There is no updater
service, startup task, or resident process.

When several stable versions are newer, setup shows bounded cumulative notes
and a link to the complete history, then downloads one installer for the newest
version. Before launch, the installer must match both the exact
`SHA256SUMS.txt` entry and GitHub's SHA-256 asset digest. The updater rejects
reparse-point download paths, hashes through a held file handle, and keeps the
verified directory and installer handles open through process creation. No
GitHub account, token, RDP credential, or telemetry is sent.

Close reminder sessions before updating so their executable is not in use.

## Publisher trust and executable signing

**Trust my generated RDP shortcuts** creates a unique self-signed code-signing
certificate in the current user's personal certificate store. Its private key
is non-exportable. Setup pins only that certificate's SHA-256 fingerprint in the
current user's trusted RDP publisher policy and signs newly generated profiles.

This does not establish the remote server's TLS identity, Authenticode-sign the
application, change SmartScreen reputation, or sign imported profiles. On a
Windows version without SHA-256 trusted-RDP-publisher support, the standard
confirmation can still appear; the app does not fall back to SHA-1.

Affected Windows versions omit Location redirection from the protected RDP
signature scope. Setup therefore does not allow Location and local publisher
trust together for a generated profile.

The build supports optional Authenticode signing with a suitable certificate and
RFC 3161 timestamp. See [docs/AUTHENTICODE.md](docs/AUTHENTICODE.md). The
default public GitHub Actions workflow receives no signing material, so its
artifacts are unsigned unless protected signing is explicitly configured. Local
RDP publisher trust cannot replace executable signing.

To verify a release installer against the checksum:

    $expected = (Select-String -Path .\SHA256SUMS.txt `
        -Pattern 'RdpSessionReminder-Installer\.exe$').Line.Substring(0, 64)
    $actual = (Get-FileHash .\RdpSessionReminder-Installer.exe `
        -Algorithm SHA256).Hash
    $actual.ToLowerInvariant() -eq $expected

A tagged download can also be checked against its GitHub build attestation:

    gh attestation verify .\RdpSessionReminder-Installer.exe `
        --repo ArxivumDev/rdp-session-reminder

## Diagnostics and privacy

Sanitized diagnostics are created only on request. A report can include app
version, profile validity, hashes, shortcut wiring, monitor availability,
`mstsc.exe` status, update state, and the reminder runtime's offline
cached-signature result.

Reports omit connection names, computer and gateway addresses, user names,
reminder text, credential fields, and raw RDP contents. A report is shown
locally and can be copied or saved; the app never uploads it.

The application opens no listening port, does not enable RDP on a destination,
sends no telemetry, does not search unrelated folders, and installs no service,
driver, scheduled task, or remote helper. The destination must already allow
Remote Desktop, and the account must have permission to sign in.

## Install and portable use

The normal installer is per-user and needs no administrator rights. It installs
under `%LOCALAPPDATA%\RdpSessionReminder` and adds setup and uninstall shortcuts
to the current user's Start menu.

For portable use, extract `RdpSessionReminder-Windows.zip` and run
`RdpSessionReminderSetup.exe`. When a connection is created, setup copies the
runtime and setup files to the local app-data folder. Portable use does not
register the managed uninstaller.

## Uninstall

Close app-launched RDP sessions, then use **Uninstall RDP Session Reminder** in
the Start menu or run `RdpSessionReminder-Uninstaller.exe`.

Uninstall removes app binaries, Start menu entries, update and theme
preferences, and registration. When the app-owned publisher identity record is
present, it removes only the matching certificate, private key, and SHA-256
policy entry; if exact trust removal cannot be verified, uninstall stops before
deleting application files. If that identity record was removed outside the
app, uninstall cannot identify orphaned trust and leaves it for manual review.

By default, uninstall preserves generated and imported profile copies,
`connection.rdp`, `settings.ini`, generated shortcut icons, abandoned one-time
profiles, and reminder shortcuts. It writes:

    %LOCALAPPDATA%\RdpSessionReminder\README - Saved RDP Connections.txt

The note explains why they remain. Preserved reminder shortcuts need the app to
be reinstalled before they work again; a preserved `connection.rdp` can still be
opened directly with Windows Remote Desktop.

The uninstall screen can also remove app-managed connection copies, exact
app-generated icon files, canonical one-time profile copies, and verified
reminder shortcuts currently on the Desktop after a second confirmation. It
removes only recognized app files and shortcuts whose target and profile
argument prove ownership. It preserves original imported files, unrelated or
extra files, reparse-point contents, and shortcuts copied or moved elsewhere.
It never scans other folders or drives.

After saving anything wanted, retained files can be removed manually by deleting
the app folder and any known moved shortcut copies.

## Troubleshooting

- **A username is filled and cannot be changed:** Windows may have a saved
  credential for that exact target, or an imported profile may contain a
  `username:s:` field. Use **Saved connections > More > Connect with a different
  account once...** for one launch, manage the Windows credential, or review the
  imported profile.
- **The same PC has different saved sign-ins:** hostname, FQDN, IP, and alias can
  be different credential targets. Recreate connections with one consistent
  target name if that is not desired.
- **A selected-monitor layout fails:** use **Show official RDP IDs** and compare
  against `mstsc.exe /l`. Confirm the selected screens share edges.
- **The reminder appears elsewhere:** customize the saved connection and choose
  the display again. A missing display falls back to the primary display.
- **No desktop icon appears:** use **Saved connections > More > Repair desktop
  shortcut**.
- **Shortcut Properties reports a Start in error:** repair the shortcut.
  App-created shortcuts use a blank **Start in** field.
- **An imported profile differs from guided options:** imported RDP settings are
  preserved and are not rewritten by those controls.
- **Publisher confirmation appears:** confirm local publisher trust is installed
  and Windows supports SHA-256 trusted RDP publishers. Imported profiles are not
  signed.
- **An update says a file is in use:** close app-launched sessions and wait for
  their reminder processes to exit.
- **The destination does not connect:** open Windows Remote Desktop directly and
  confirm the target already accepts RDP.

## Requirements

- Windows 10 or Windows 11
- .NET Framework 4.8
- Microsoft Remote Desktop Connection (`mstsc.exe`)
- A destination already configured to accept RDP

## Build from source

Run:

    .\build.ps1

The build runs project checks and writes the installer, standalone uninstaller,
portable ZIP, individual executables, and `SHA256SUMS.txt` to `dist`. For a
validated stable version tag, GitHub Actions attests the successful CI build and
publishes those exact CI artifacts as the release. The build is unsigned by
default. Signing material is never stored in this repo.

## Related projects

The saved-connection organization, visual labels, selected-monitor workflow, and
diagnostic presentation were informed by public interface ideas in TinyRDP,
Taskbar Marker, RoyalApps Community RDP, and MsRdpEx. No code, dependency,
runtime component, injection, or hook from those projects is included.

Color choices apply to this app's reminder banner. This release does not recolor
or modify the Windows taskbar.

## Limitations

- The reminder tracks only sessions launched by this app.
- Selected-monitor settings are generated only for new direct profiles.
- Imported RDP profiles retain all supplied settings.
- Monitor IDs can change; `mstsc.exe /l` is authoritative.
- The app targets the classic `mstsc.exe` client.
- Public CI artifacts remain unsigned unless protected signing is configured.

## License

RDP Session Reminder is available under the [MIT License](LICENSE).
