# Changelog

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
