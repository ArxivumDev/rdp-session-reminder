# Changelog

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
