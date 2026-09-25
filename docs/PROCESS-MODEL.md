# Process and privacy model

RDP Session Reminder separates configuration from connection-time work so the
full interface is never resident in the background.

| Component | When it runs | What it does | When it exits |
| --- | --- | --- | --- |
| Setup and connection manager | Only while its visible window is open | Guides new profile creation and manages app-owned profiles and shortcuts | When closed, or after a create-and-connect or saved-connect launch |
| Reminder editor and dialogs | Only while visibly opened inside setup | Changes reminder settings, displays, icons, and other requested configuration | The window is disposed when it closes; the setup process ends when setup closes or connects |
| Reminder runtime | Once for a shortcut, manager launch, or one-time connection | Starts Windows Remote Desktop, draws the small local banner, and watches only the RDP processes from that launch | When that launch ends or is cancelled |
| `mstsc.exe` | While Windows Remote Desktop is open | Owns the network connection, authentication, credential prompt, display, audio, and resource redirection | When Remote Desktop closes |

There is no service, tray application, scheduled task, startup item, hidden
connection manager, browser runtime, injected DLL, window hook, or remote-side
agent. Setup can use a worker while a visible creation operation is in progress,
but closing setup releases the complete configuration interface. During a
connection, only the small Win32/GDI reminder runtime is added to the normal
memory used by Windows' `mstsc.exe` client.

## Setup appearance

The setup UI has Dark and Light palettes. The per-user preference is stored in
`%LOCALAPPDATA%\RdpSessionReminder\ui-settings.ini`. Missing, malformed, or
oversized preference data resolves to Dark. Windows High Contrast overrides the
custom palette with system colors.

Theme application is configuration-time work. The dimensional logo, tabs, and
buttons are painted statically. Setup assigns app-scoped cursor resources: a
subtle green halo in Dark mode and a pale blue-and-white cloud in Light mode.
Usable system-cursor pixels are retained; a matching stock arrow, text, or link
shape is used only when Windows exposes a blank cursor mask. High Contrast,
oversized pointers, and custom cursor schemes remain unchanged. The cues have no
polling timer, system-wide hook, overlay window, or background process, and end
when setup closes. The reminder runtime and RDP session are unaffected. The
reminder banner keeps its per-profile colors independently.

## Connection ownership

The manager reads only immediate profile folders under
`%LOCALAPPDATA%\RdpSessionReminder\profiles`. A profile folder name must be a
canonical GUID, and its settings and connection file must resolve inside that
exact folder. The app does not search unrelated folders or drives.

Every permanent shortcut starts the reminder runtime with one profile GUID. The
shortcut gives the normal double-click connection experience. When saved
profiles exist, setup starts on the manager page; double-click, Enter, or
**Connect** starts that same profile and closes setup.

A one-time connection uses a random GUID under the app's `one-time` directory
and creates no shortcut. After its associated RDP launch ends, the runtime
removes only that exact temporary profile when path, type, and ownership safety
checks succeed. If those checks cannot prove the deletion is safe, the runtime
leaves the files and reports the failure.

**Customize reminder** updates the app's `settings.ini` and recreates a verified
desktop shortcut. It does not alter `connection.rdp`. Shortcut copies moved
elsewhere are not searched for or modified.

## Credentials and sign-in

The app has no credential database or multiple-account list. It does not request,
read, enumerate, delete, or transmit Windows credentials. A new direct v1.2
profile uses the fixed `prompt for credentials:i:0` default and offers no
user-selectable persistent prompt choice. Normal shortcuts and manager
connections allow `mstsc.exe` to use a credential Windows has saved for the
exact target when available, or prompt when needed.

**More > Connect with a different account once...** adds `mstsc.exe /prompt` to
that manager launch only. It does not modify the profile, shortcut, or copied
RDP file. Windows owns the prompt and any **Remember me** choice, so the app
does not guarantee that Windows credential state or a later launch stays
unchanged. This account-choice action is not placed in generated desktop
shortcuts.

Imported profiles and profiles created by older releases keep their existing
RDP settings, including any prompt preference. The manager does not silently
rewrite those files.

Windows stores remembered RDP credentials under the exact destination, commonly as
`TERMSRV/target`. A hostname, FQDN, IP address, and DNS alias can address one
physical PC but still be separate Windows credential targets. The app does not
merge those aliases.

An imported RDP file can contain a user-name field or opaque `password 51`
field. The preview reports presence only and never displays the values. A
byte-for-byte imported copy necessarily retains every supplied field; the app
does not interpret the credential field or place it in its own store.

## Monitor selection

For direct generated profiles, setup can write `selectedmonitors:s:` together
with multiple-monitor mode. The chosen displays must form one edge-connected
group, and the first selected RDP ID becomes the primary display inside the
remote session.

The visual chooser assigns friendly display numbers and follows native Windows
monitor enumeration. Its **Show official RDP IDs** action launches
`mstsc.exe /l`; that Windows output is authoritative. RDP IDs are tied to the
current topology and can change after displays, docks, or drivers change.

The display that hosts the local reminder is selected separately from the
displays used by the remote session. Imported RDP files retain their supplied
monitor settings and are not rewritten by the chooser.

## Imported RDP files

Before import, setup opens a size-bounded snapshot while denying source changes
and deletion during the copy. The preview is local and read-only. It reports
display behavior, gateway presence, credential-field presence, resource
redirection, and authentication behavior. Endpoint names are hidden until the
user explicitly reveals them; saved credential values are never shown.

The user must accept the preview. Setup then copies the staged bytes to the new
profile and verifies the copy. The original and private profile copy remain
byte-for-byte unchanged. Setup reads only the advertised target needed for
labeling, preferring a nonempty `alternate full address:s:` and then
`full address:s:`.

## Diagnostics

Diagnostics are generated only after the user requests them. Reports can
describe app version, profile validity, file hashes, shortcut wiring, monitor
availability, `mstsc.exe`, updater state, and the runtime executable's offline
cached-signature result.

Connection names, computer and gateway addresses, user names, reminder text,
credential fields, and raw RDP file content are omitted. The app can copy or
save a report locally; it never uploads or sends one.

## Trust, signing, and updates

Optional local RDP publisher trust creates a unique non-exportable key for the
current Windows user and pins only its SHA-256 fingerprint in that user's RDP
publisher policy. It signs generated profiles only. Imported profiles remain
unchanged.

That local certificate is separate from Authenticode and remote-server TLS. The
build supports optional Authenticode signing when protected signing material is
provided. The default public CI workflow supplies none, so its artifacts are
unsigned.

Update checks run only in the visible setup process. Automatic checking is
optional and occurs at most once per day when setup opens. There is no updater
service. An installer must match both the release checksum entry and GitHub
asset digest. Reparse-point download paths are rejected; hashing uses a held
file handle with write, delete, and rename sharing denied, and verified directory
and installer handles remain open through process creation.

Stable release tags must match the release-note version. The tagged CI build is
attested first, and the release job publishes those exact CI artifacts. GitHub's
semantic-version selection determines the latest stable release.

## Uninstall boundaries

Managed uninstall removes the binaries, Start menu entries, update and UI
preferences, and registration. When the publisher identity record is intact,
exact app-owned RDP publisher trust is removed and verified before application
files are deleted. If that record was removed externally, any orphaned trust is
left for manual review.

Profiles, generated icons, abandoned one-time profiles, and reminder shortcuts
are preserved by default, and a note explains how to open retained RDP files or
remove the folder manually. Optional saved-connection cleanup removes only
canonical app profile and one-time copies, exact generated icon files, and
Desktop shortcuts whose target and profile argument prove ownership. Original
imported files, unknown or extra files, reparse-point contents, and moved copies
remain. No drive-wide scan is used.

Installer and portable-setup payloads are staged before destination replacement,
so an existing hard link is unlinked rather than written through. Application
and Start menu roots plus child executable and shortcut destinations reject
unexpected directories and reparse points.

## Related projects

The connection list, visual labels, monitor chooser, and diagnostic organization
are original implementations informed by public interface ideas in TinyRDP,
Taskbar Marker, RoyalApps Community RDP, and MsRdpEx. Their code, dependencies,
runtime components, injections, and hooks are not bundled.

Banner colors belong only to the local reminder window. The application does not
recolor or modify the Windows taskbar.
