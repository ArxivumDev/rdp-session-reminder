using System;
using System.IO;
using System.Runtime.InteropServices;

internal static class ExecutableSignatureStatus
{
    private static readonly Guid GenericVerifyAction =
        new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustFileInfo
    {
        public uint Size;
        public IntPtr FilePath;
        public IntPtr FileHandle;
        public IntPtr KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustData
    {
        public uint Size;
        public IntPtr PolicyCallbackData;
        public IntPtr SipClientData;
        public uint UiChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr FileInfo;
        public uint StateAction;
        public IntPtr StateData;
        public IntPtr UrlReference;
        public uint ProviderFlags;
        public uint UiContext;
        public IntPtr SignatureSettings;
    }

    [DllImport("wintrust.dll", ExactSpelling = true,
        PreserveSig = true, SetLastError = false)]
    private static extern int WinVerifyTrust(
        IntPtr window, [In] ref Guid actionId,
        [In] ref WinTrustData trustData);

    // Uses only the local Windows trust cache. Diagnostics must never cause a
    // network request or change certificate state.
    public static bool IsTrusted(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !File.Exists(path) || !Path.IsPathRooted(path))
            return false;

        IntPtr pathPointer = IntPtr.Zero;
        IntPtr filePointer = IntPtr.Zero;
        try
        {
            pathPointer = Marshal.StringToCoTaskMemUni(
                Path.GetFullPath(path));
            WinTrustFileInfo file = new WinTrustFileInfo();
            file.Size = (uint)Marshal.SizeOf(typeof(WinTrustFileInfo));
            file.FilePath = pathPointer;
            filePointer = Marshal.AllocCoTaskMem(
                Marshal.SizeOf(typeof(WinTrustFileInfo)));
            Marshal.StructureToPtr(file, filePointer, false);

            WinTrustData data = new WinTrustData();
            data.Size = (uint)Marshal.SizeOf(typeof(WinTrustData));
            data.UiChoice = 2; // WTD_UI_NONE
            data.RevocationChecks = 1; // WTD_REVOKE_WHOLECHAIN
            data.UnionChoice = 1; // WTD_CHOICE_FILE
            data.FileInfo = filePointer;
            data.StateAction = 0; // WTD_STATEACTION_IGNORE
            // WTD_CACHE_ONLY_URL_RETRIEVAL |
            // WTD_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT. This remains an
            // offline diagnostic and fails closed if cached revocation data is
            // insufficient.
            data.ProviderFlags = 0x00001080;
            Guid action = GenericVerifyAction;
            return WinVerifyTrust(IntPtr.Zero, ref action, ref data) == 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (filePointer != IntPtr.Zero)
            {
                Marshal.DestroyStructure(filePointer,
                    typeof(WinTrustFileInfo));
                Marshal.FreeCoTaskMem(filePointer);
            }
            if (pathPointer != IntPtr.Zero)
                Marshal.FreeCoTaskMem(pathPointer);
        }
    }
}
