# Authenticode release signing

The build supports optional Authenticode signing with a code-signing certificate
already installed in the current user's `My` certificate store. The certificate
thumbprint and an HTTPS RFC 3161 timestamp service are required together:

```powershell
.\build.ps1 `
  -SigningCertificateThumbprint '0123456789ABCDEF0123456789ABCDEF01234567' `
  -SigningTimestampUrl 'https://timestamp.example.com'
```

The same values can be provided through
`RDP_REMINDER_SIGN_CERT_SHA1` and
`RDP_REMINDER_SIGN_TIMESTAMP_URL` on a protected release runner.

The script uses the Windows SDK `SignTool`, SHA-256 file digests, an RFC 3161
SHA-256 timestamp, and the default Authenticode verification policy. It signs
the runtime, setup, and uninstaller before they are embedded in the installer,
then signs the installer. Checksums and the portable ZIP are created only after
signing, so published hashes describe the exact signed files.

No PFX password option is provided because passing one to `SignTool` would put a
secret on a process command line. Import a certificate securely into an
ephemeral release runner or use a managed signing service, then select it by
thumbprint. Public releases need a certificate that chains to a code-signing CA
trusted by Windows. A self-signed test certificate can prove that signing works,
but it does not establish public publisher trust or SmartScreen reputation.

The local certificate created by the application's optional RDP publisher-trust
feature is deliberately separate. It signs generated `.rdp` files for one
Windows user and must never be presented as Authenticode trust for the
application executables.

Microsoft documents `SignTool` at
<https://learn.microsoft.com/windows/win32/seccrypto/signtool> and recommends
SHA-256 with RFC 3161 timestamping at
<https://learn.microsoft.com/windows/win32/seccrypto/time-stamping-authenticode-signatures>.
Microsoft Artifact Signing Public Trust is one managed option for an eligible,
validated publisher:
<https://learn.microsoft.com/azure/artifact-signing/concept-trust-models>.
