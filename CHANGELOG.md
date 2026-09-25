# Changelog

## 1.1.0 - 2026-09-25

- Generate one private `.rdp` connection profile for each new direct shortcut
- Add native/current and common smaller remote-desktop resolution choices
- Add an all-displays option while keeping the full-screen connection bar visible
- Apply 32-bit color, high visual settings, automatic network detection,
  persistent bitmap caching, default audio, full-screen keyboard handling,
  automatic reconnect, and server-authentication warnings
- Add explicit clipboard, drive, location, serial-port, WebAuthn, and smart-card
  or Windows Hello for Business redirection choices
- Add an **Always ask for credentials** option without reading or storing passwords
- Add optional per-user SHA-256 trusted RDP publisher setup, automatic signing of
  newly generated profiles, and matching trust removal
- Keep imported `.rdp` files byte-for-byte unchanged and do not re-sign them
- Clarify that RDP publisher trust is separate from executable signing,
  SmartScreen reputation, and remote-server TLS identity
- Add per-user installer and standalone uninstaller packages with Start menu
  entries and automatic removal of only app-owned publisher trust
- Preserve every generated or imported profile and reminder shortcut by default
  during managed uninstall, and leave a UTF-8 note explaining direct use,
  reinstall, and optional manual deletion without scanning the user's drives
- Add a separately confirmed cleanup option for canonical app profiles and
  verified reminder shortcuts in the original Desktop folder
- Remove and verify the exact app-owned RDP publisher certificate, private key,
  and SHA-256 policy pin while preserving unrelated trust material
- Show live setup progress and create reminder shortcuts with a blank **Start in**
  field so Windows shortcut Properties remains usable after folder changes
- Add manual and optional once-daily update checks, cumulative release notes,
  direct-to-latest upgrades, and SHA-256 verification before installer launch
- Keep existing v1.0.x profiles compatible and preserve the service-free,
  client-side reminder design

## 1.0.1 - 2026-09-25

- Accept canonical IPv4 addresses with ports and reject ambiguous targets with
  whitespace, control characters, non-ASCII port digits, or leading-zero IPv4 parts
- Keep `.rdp` connection labels consistent by preferring a nonempty
  `alternate full address:s:` value, then `full address:s:`, and making the
  resulting Computer field read-only
- Recheck the target after the selected `.rdp` file is copied and always launch
  the profile's own `connection.rdp` rather than a path stored in profile data;
  refresh older auto-generated labels while preserving custom reminder text
- Track the Remote Desktop processes created by each shortcut in a private
  Windows Job object and pin the reminder to a verified process in that job
- End a pending reminder promptly when Remote Desktop is cancelled, while
  allowing security or sign-in prompts to remain open longer than five minutes
- Correct per-profile mutex ownership so a stopped or abandoned launch does not
  leave the shortcut reported as already running
- Size and rescale the banner for the selected display's DPI
- Reject reserved Windows device names when generating shortcut names
- Clarify local `.rdp` address reading, per-shortcut settings, client-side memory
  use, Windows connection history, safe updates, and uninstall behavior
- Tie release packages to the successful tagged GitHub Actions build, publish
  SHA-256 checksums, and generate GitHub build-provenance attestations

## 1.0.0 - 2026-09-25

- Add a first-run setup interface for a computer name or IP address
- Give every generated shortcut its own connection profile, reminder text, and display
- Allow an existing `.rdp` file to be selected and copied byte-for-byte without
  changing the original
- Create a uniquely named desktop shortcut without overwriting unrelated `.lnk` files
- Launch connections through the built-in `mstsc.exe` client
- Show an independent, click-through reminder on the selected local display
- Limit each reminder to the RDP session launched by its associated shortcut
- Use a lightweight Win32/GDI runtime with no resident setup process
- Store no passwords, tokens, certificates, or remote-system configuration
- Add scripted builds, smoke tests, release packaging, and checksums
