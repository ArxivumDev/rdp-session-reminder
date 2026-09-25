# RDP Session Reminder v1.2.0

Released 2026-09-25.

Version 1.2 turns setup into a polished connection launcher and manager while
preserving the project's small, local runtime. New users follow one guided
workflow; returning users land on saved connections and can double-click a
computer to connect.

## Faster connection workflow

- Setup is organized as four numbered steps: Connection, Displays, Resources,
  and Reminder.
- Recommended values are preselected, less-common choices are labeled, and
  Back/Next navigation keeps the normal path focused.
- **Create shortcut and connect** produces a reusable direct-launch shortcut.
- **Connect once** creates no shortcut and removes its exact temporary profile
  after the session when safe cleanup can be verified.
- Setup shows progress for file installation, profile creation or verification,
  optional RDP publisher trust, settings, shortcut creation, and launch.
- When saved profiles exist, setup opens on **Saved connections**.
- A saved row connects by double-click, Enter, or **Connect**, then the full
  configuration interface closes.

## Simple credential model

The application has no credential profiles and does not request or store
passwords. New direct v1.2 profiles use a fixed `prompt for credentials:i:0`
default instead of offering a persistent account choice in the UI. Their
shortcut and saved-row connection let Windows Remote Desktop use a credential
saved for that exact target when available, or prompt when needed.

A different account is available only from **Saved connections > More > Connect
with a different account once...**. It invokes Windows' standard prompt for that
launch without changing the shortcut or copied RDP file. Windows owns the
prompt and any **Remember me** choice; the app does not edit Credential Manager
or promise that a user's Windows choice cannot affect later connections.

Windows can treat a hostname, FQDN, IP address, and DNS alias for one physical PC
as separate `TERMSRV` credential targets. The documentation now recommends one
consistent target name when one saved sign-in is wanted.

Imported files and profiles created by earlier releases retain their existing
RDP settings, including any prompt preference. Creating a new direct profile
adopts the v1.2 sign-in behavior without rewriting a retained connection.

## Professional setup appearance

- New static dimensional app branding, tabs, and buttons improve the setup
  hierarchy without animation or a background process.
- Dark and Light themes are available. Dark is the first-run default whenever
  the per-user `ui-settings.ini` preference is missing or invalid.
- The selected theme persists for the current Windows user.
- Windows High Contrast colors override the custom palette.
- Theme selection affects configuration windows only and never changes the
  reminder banner or connection-time memory use.
- Setup uses a subtle green cursor halo in Dark mode and a pale blue-and-white
  cloud behind the cursor in Light mode. The app-scoped resources retain usable
  system-cursor pixels and use matching stock arrow, text, or link shapes only
  when Windows exposes a blank cursor mask. High Contrast, oversized pointers,
  and custom cursor schemes remain unchanged. The cues use no polling timer,
  system-wide hook, overlay window, or background process and end with setup.
- The manager, editor, and dialogs live inside the visible setup process.
  Closing setup, or connecting through it, releases that complete process.

## Display and monitor control

- Direct generated profiles can use one monitor, all monitors, or an explicitly
  selected edge-connected subset.
- The chooser visualizes the current layout, numbers each monitor, and can show
  temporary full-screen display numbers.
- The reminder monitor is chosen separately from the displays used inside RDP.
- **Show official RDP IDs** runs `mstsc.exe /l`; those IDs are authoritative for
  the current topology.
- The app validates that selected displays form one edge-connected group.
- Windows uses the first selected ID as the primary monitor in the remote
  session.
- Imported RDP display settings remain unchanged.

Generated profiles continue to use 32-bit color, high visual quality, the
full-screen connection bar, full-screen keyboard handling, local audio playback,
automatic network detection, persistent bitmap caching, automatic reconnect,
and an authentication warning.

## Reminder customization

Each saved profile can now choose:

- an optional 24-character short label
- Default, Work, Personal, Test, Production, or custom colors
- any of the four screen corners
- vertical distance from the chosen screen edge
- Small, Medium, or Large text
- opacity from 35 to 100 percent
- optional dimming after local input has been idle

The preview shows placement before creation, and **Test reminder** displays the
result on the selected local monitor. The reminder remains click-through and
runs only for the associated RDP launch.

## Resource defaults

The guided defaults enable Clipboard and WebAuthn passkeys or security keys.
Drives, printers, microphone, location, serial or COM ports, and smart cards or
Windows Hello for Business start off. Generated profiles explicitly disable
unexposed MTP/PTP, camera, and USB device redirection.

Printer and microphone controls are now available in the normal resource step.
Server policy can still reject any requested redirection.

## Safer RDP import

- Setup creates a locked, bounded snapshot before importing.
- Files larger than 4 MiB are rejected.
- Preview reports display, gateway presence, credential-field presence,
  resources, and authentication behavior.
- Computer and gateway names remain hidden until the user asks to reveal them.
- A saved password field is never displayed or interpreted.
- The user must select **Use this file** before setup continues.
- The source and private profile copy remain byte-for-byte unchanged.

Because import preserves the file exactly, an opaque credential field already
inside the supplied RDP file is also preserved in its private copy. The app does
not place it in an application credential store.

## Saved-connection tools

The new manager can:

- connect directly
- customize reminder text, local reminder display, icon, and banner appearance
- duplicate an app profile
- repair its desktop shortcut and icon
- create a sanitized diagnostic report
- delete an app-owned profile after confirmation

Reminder customization updates `settings.ini` and the verified desktop shortcut.
It leaves `connection.rdp` unchanged. Files or shortcuts moved elsewhere are not
searched for.

Shortcut icon choices include Windows Remote Desktop, a neutral reminder,
Work-blue, Production-red, Test-amber, and Personal-green.

Diagnostics are local and omit connection names, endpoints, gateway names, user
names, reminder text, credential fields, and raw RDP data. Reports are never
uploaded automatically.

## Signing, updates, and cleanup

- The build can optionally Authenticode-sign the runtime, setup, uninstaller, and
  installer with a protected certificate and RFC 3161 timestamp.
- The default public GitHub Actions workflow supplies no signing material, so
  public CI artifacts remain unsigned unless protected signing is configured.
- The local RDP publisher certificate remains separate from executable signing
  and never signs imported profiles.
- Update checks still run only in setup, can be manual or at most once daily, and
  present cumulative notes before updating directly to the newest stable
  release.
- Downloads still require matching `SHA256SUMS.txt` and GitHub asset digests and
  are hashed through a held file handle. Reparse-point download paths are
  rejected, and verified directory and installer handles remain open through
  process creation.
- Installer and portable-setup replacements are staged so existing hard links
  are not written through, and app destinations reject directories and reparse
  points.
- Stable release tags are validated against the release-note version. Tagged CI
  builds are attested before the exact CI artifacts are published.
- Uninstall removes the per-user theme and update preferences, app binaries,
  Start menu entries, registration, and verified app-owned RDP publisher trust
  when its identity record is intact.
- Saved profiles, shortcuts, generated icons, and abandoned one-time profiles
  remain by default with an explanatory cleanup note. A separately confirmed
  option removes only canonical app profile and one-time copies, exact generated
  icons, and verified reminder shortcuts currently on the Desktop.

## Lightweight process model

The four-step workflow, manager, monitor chooser, preview, editor, and
diagnostics live in the visible setup process. Closing setup, or connecting
through it, ends that complete configuration process. The application installs
no service, tray process, startup item, scheduled task, browser runtime,
injected component, window hook, or remote-side helper.

During a connection, only the small Win32/GDI reminder runtime is added to
Windows' normal `mstsc.exe` client. It exits with the associated launch.

The organizational and presentation ideas were informed by TinyRDP, Taskbar
Marker, RoyalApps Community RDP, and MsRdpEx. This release contains none of
their code, dependencies, runtime components, injections, or hooks. Banner color
choices do not recolor the Windows taskbar.
