using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

internal sealed class ManagedConnectionInfo
{
    public string ProfileId;
    public string ProfileDirectory;
    public string SettingsPath;
    public string RdpPath;
    public string DisplayName;
    public ReminderSettings Settings;
    public bool SettingsValid;
    public bool RdpExists;

    public bool Ready
    {
        get { return SettingsValid && RdpExists; }
    }

    public string StateText
    {
        get
        {
            if (!SettingsValid)
                return "Invalid settings";
            if (!RdpExists)
                return "Missing RDP copy";
            return "Ready";
        }
    }
}

internal static class ConnectionProfileCatalog
{
    public static IList<ManagedConnectionInfo> EnumerateProfiles()
    {
        return EnumerateProfilesUnderRoot(AppPaths.ProfilesDirectory);
    }

    // The explicit root is a test seam. Production callers use EnumerateProfiles,
    // DuplicateProfile, and DeleteProfile, which are fixed to AppPaths.
    public static IList<ManagedConnectionInfo> EnumerateProfilesUnderRoot(
        string profilesRoot)
    {
        List<ManagedConnectionInfo> profiles = new List<ManagedConnectionInfo>();
        string root;
        if (!TryNormalizeProfilesRoot(profilesRoot, out root) ||
            !Directory.Exists(root))
            return profiles;

        if (IsReparsePoint(root))
            throw new IOException("The profiles directory cannot be a reparse point.");

        string[] directories = Directory.GetDirectories(root, "*",
            SearchOption.TopDirectoryOnly);
        foreach (string directory in directories)
        {
            string profileId = Path.GetFileName(directory);
            string canonicalDirectory;
            if (!TryResolveDirectProfileDirectory(root, profileId,
                    out canonicalDirectory))
                continue;

            ManagedConnectionInfo profile = ReadProfile(
                root, profileId, canonicalDirectory);
            if (profile != null)
                profiles.Add(profile);
        }

        profiles.Sort(delegate(ManagedConnectionInfo first,
            ManagedConnectionInfo second)
        {
            int nameResult = string.Compare(first.DisplayName,
                second.DisplayName, StringComparison.CurrentCultureIgnoreCase);
            if (nameResult != 0)
                return nameResult;
            return string.CompareOrdinal(first.ProfileId, second.ProfileId);
        });
        return profiles;
    }

    public static bool IsCanonicalProfileId(string profileId)
    {
        Guid parsed;
        return !string.IsNullOrEmpty(profileId) &&
            profileId.Length == 32 &&
            Guid.TryParseExact(profileId, "N", out parsed) &&
            string.Equals(parsed.ToString("N"), profileId,
                StringComparison.Ordinal);
    }

    public static bool TryResolveDirectProfileDirectory(string profilesRoot,
        string profileId, out string profileDirectory)
    {
        profileDirectory = "";
        string root;
        if (!TryNormalizeProfilesRoot(profilesRoot, out root) ||
            !IsCanonicalProfileId(profileId))
            return false;

        string candidate;
        try
        {
            candidate = Path.GetFullPath(Path.Combine(root, profileId));
        }
        catch
        {
            return false;
        }

        string parent = Path.GetDirectoryName(candidate);
        if (!PathsEqual(parent, root) ||
            !string.Equals(Path.GetFileName(candidate), profileId,
                StringComparison.Ordinal))
            return false;

        if (Directory.Exists(candidate) && IsReparsePoint(candidate))
            return false;

        profileDirectory = candidate;
        return true;
    }

    public static ManagedConnectionInfo DuplicateProfile(
        string sourceProfileId, string requestedShortcutName)
    {
        return DuplicateProfileUnderRoot(AppPaths.ProfilesDirectory,
            sourceProfileId, requestedShortcutName);
    }

    public static ManagedConnectionInfo DuplicateProfileUnderRoot(
        string profilesRoot, string sourceProfileId,
        string requestedShortcutName)
    {
        string root;
        if (!TryNormalizeProfilesRoot(profilesRoot, out root) ||
            !Directory.Exists(root) || IsReparsePoint(root))
            throw new IOException("The profiles directory is unavailable or unsafe.");

        string sourceDirectory;
        if (!TryResolveDirectProfileDirectory(root, sourceProfileId,
                out sourceDirectory) || !Directory.Exists(sourceDirectory))
            throw new ArgumentException("The source profile is not a canonical app profile.");

        ManagedConnectionInfo source = ReadProfile(
            root, sourceProfileId, sourceDirectory);
        if (source == null || !source.Ready)
            throw new InvalidOperationException(
                "Only a complete app-owned profile can be duplicated.");
        if (IsReparsePoint(source.SettingsPath) || IsReparsePoint(source.RdpPath))
            throw new IOException("Profile files cannot be reparse points.");

        string newProfileId = "";
        string newDirectory = "";
        for (int attempt = 0; attempt < 20; attempt++)
        {
            string candidateId = Guid.NewGuid().ToString("N");
            string candidateDirectory;
            if (!TryResolveDirectProfileDirectory(root, candidateId,
                    out candidateDirectory))
                continue;
            if (Directory.Exists(candidateDirectory) || File.Exists(candidateDirectory))
                continue;

            Directory.CreateDirectory(candidateDirectory);
            if (IsReparsePoint(candidateDirectory))
                throw new IOException("The new profile directory is unsafe.");
            newProfileId = candidateId;
            newDirectory = candidateDirectory;
            break;
        }
        if (newProfileId.Length == 0)
            throw new IOException("A unique profile identifier could not be allocated.");

        try
        {
            string newRdpPath = Path.Combine(newDirectory,
                AppPaths.ConnectionFileName);
            string newSettingsPath = Path.Combine(newDirectory,
                AppPaths.SettingsFileName);
            File.Copy(source.RdpPath, newRdpPath, false);

            string shortcutName = SettingsStore.NormalizeShortcutName(
                requestedShortcutName,
                source.Settings == null ? "Remote Desktop" :
                    source.Settings.ComputerName);
            CopySettingsForDuplicate(source.SettingsPath, newSettingsPath,
                newRdpPath, shortcutName);

            ManagedConnectionInfo duplicate = ReadProfile(
                root, newProfileId, newDirectory);
            if (duplicate == null || !duplicate.Ready)
                throw new InvalidDataException(
                    "The duplicated profile did not pass validation.");
            return duplicate;
        }
        catch
        {
            DeleteKnownNewDirectory(newDirectory);
            throw;
        }
    }

    public static void DeleteProfile(string profileId)
    {
        DeleteProfileUnderRoot(AppPaths.ProfilesDirectory, profileId);
    }

    public static void DeleteProfileUnderRoot(string profilesRoot,
        string profileId)
    {
        string root;
        if (!TryNormalizeProfilesRoot(profilesRoot, out root) ||
            !Directory.Exists(root) || IsReparsePoint(root))
            throw new IOException("The profiles directory is unavailable or unsafe.");

        string profileDirectory;
        if (!TryResolveDirectProfileDirectory(root, profileId,
                out profileDirectory) || !Directory.Exists(profileDirectory))
            throw new ArgumentException("The profile is not a canonical app profile.");
        if (IsReparsePoint(profileDirectory))
            throw new IOException("A profile reparse point will not be deleted.");

        string settingsPath = Path.Combine(profileDirectory,
            AppPaths.SettingsFileName);
        string rdpPath = Path.Combine(profileDirectory,
            AppPaths.ConnectionFileName);
        foreach (string entry in Directory.GetFileSystemEntries(
            profileDirectory))
        {
            if (!PathsEqual(entry, settingsPath) && !PathsEqual(entry, rdpPath))
                throw new IOException(
                    "The profile contains an unrecognized file or folder and " +
                    "was not deleted. Remove that item manually after reviewing it.");
            if (Directory.Exists(entry) || IsReparsePoint(entry) ||
                !File.Exists(entry))
                throw new IOException(
                    "A managed profile entry is not a regular app-owned file and " +
                    "was not deleted.");
        }
        if (File.Exists(settingsPath))
            File.Delete(settingsPath);
        if (File.Exists(rdpPath))
            File.Delete(rdpPath);
        Directory.Delete(profileDirectory, false);
    }

    private static ManagedConnectionInfo ReadProfile(string root,
        string profileId, string profileDirectory)
    {
        string expectedDirectory;
        if (!TryResolveDirectProfileDirectory(root, profileId,
                out expectedDirectory) ||
            !PathsEqual(expectedDirectory, profileDirectory) ||
            !Directory.Exists(expectedDirectory))
            return null;

        string settingsPath = Path.Combine(expectedDirectory,
            AppPaths.SettingsFileName);
        string rdpPath = Path.Combine(expectedDirectory,
            AppPaths.ConnectionFileName);

        ReminderSettings settings;
        bool settingsValid = TryReadKnownSettings(
            settingsPath, rdpPath, out settings);
        bool rdpExists = File.Exists(rdpPath) && !IsReparsePoint(rdpPath);

        ManagedConnectionInfo profile = new ManagedConnectionInfo();
        profile.ProfileId = profileId;
        profile.ProfileDirectory = expectedDirectory;
        profile.SettingsPath = settingsPath;
        profile.RdpPath = rdpPath;
        profile.Settings = settings;
        profile.SettingsValid = settingsValid;
        profile.RdpExists = rdpExists;
        profile.DisplayName = settingsValid
            ? settings.ShortcutName
            : "Profile " + profileId.Substring(0, 8);
        return profile;
    }

    private static bool TryReadKnownSettings(string settingsPath,
        string canonicalRdpPath, out ReminderSettings settings)
    {
        settings = null;
        if (!File.Exists(settingsPath) || IsReparsePoint(settingsPath))
            return false;

        try
        {
            FileInfo info = new FileInfo(settingsPath);
            if (info.Length < 1 || info.Length > 1024 * 1024)
                return false;

            string computer = "";
            string shortcut = "";
            string display = "";
            string reminder = "";
            string shortLabel = "";
            string bannerPreset = "Default";
            string bannerBackground = "";
            string bannerForeground = "";
            string bannerCorner = "BottomRight";
            int bannerVerticalOffset = 64;
            string bannerSize = "Medium";
            int bannerOpacity = 97;
            bool idleDimming = false;
            bool fullScreen = true;
            foreach (string rawLine in File.ReadAllLines(settingsPath,
                Encoding.UTF8))
            {
                int separator = rawLine.IndexOf('=');
                if (separator <= 0)
                    continue;
                string key = rawLine.Substring(0, separator).Trim();
                string value = rawLine.Substring(separator + 1).Trim();
                if (key.Equals("Computer", StringComparison.OrdinalIgnoreCase))
                    computer = value;
                else if (key.Equals("FullScreen",
                        StringComparison.OrdinalIgnoreCase))
                    fullScreen = value != "0";
                else if (key.Equals("ShortcutName",
                        StringComparison.OrdinalIgnoreCase))
                    shortcut = value;
                else if (key.Equals("DisplayDevice",
                        StringComparison.OrdinalIgnoreCase))
                    display = SafeSingleLine(value, 512);
                else if (key.Equals("ReminderText",
                        StringComparison.OrdinalIgnoreCase))
                    reminder = value;
                else if (key.Equals("ShortLabel",
                        StringComparison.OrdinalIgnoreCase))
                    shortLabel = value;
                else if (key.Equals("BannerPreset",
                        StringComparison.OrdinalIgnoreCase))
                    bannerPreset = value;
                else if (key.Equals("BannerBackground",
                        StringComparison.OrdinalIgnoreCase))
                    bannerBackground = value;
                else if (key.Equals("BannerForeground",
                        StringComparison.OrdinalIgnoreCase))
                    bannerForeground = value;
                else if (key.Equals("BannerCorner",
                        StringComparison.OrdinalIgnoreCase))
                    bannerCorner = value;
                else if (key.Equals("BannerVerticalOffset",
                        StringComparison.OrdinalIgnoreCase))
                {
                    int parsedOffset;
                    if (int.TryParse(value, NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out parsedOffset))
                        bannerVerticalOffset = parsedOffset;
                }
                else if (key.Equals("BannerSize",
                        StringComparison.OrdinalIgnoreCase))
                    bannerSize = value;
                else if (key.Equals("BannerOpacity",
                        StringComparison.OrdinalIgnoreCase))
                {
                    int parsedOpacity;
                    if (int.TryParse(value, NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out parsedOpacity))
                        bannerOpacity = parsedOpacity;
                }
                else if (key.Equals("IdleDimming",
                        StringComparison.OrdinalIgnoreCase))
                    idleDimming = value == "1";
            }

            computer = SettingsStore.NormalizeComputerName(computer);
            if (!SettingsStore.IsValidComputerName(computer))
                return false;

            settings = new ReminderSettings();
            settings.ComputerName = computer;
            settings.FullScreen = fullScreen;
            settings.ShortcutName = SettingsStore.NormalizeShortcutName(
                shortcut, computer);
            settings.RdpFile = canonicalRdpPath;
            settings.DisplayDevice = display;
            settings.ReminderText = SettingsStore.NormalizeReminderText(
                reminder, computer);
            settings.ShortLabel = SettingsStore.NormalizeShortLabel(shortLabel);
            settings.BannerPreset = SettingsStore.NormalizeBannerPreset(
                bannerPreset);
            settings.BannerBackground = SettingsStore.NormalizeBannerColor(
                bannerBackground,
                SettingsStore.GetPresetBackground(settings.BannerPreset));
            settings.BannerForeground = SettingsStore.NormalizeBannerColor(
                bannerForeground,
                SettingsStore.GetPresetForeground(settings.BannerPreset));
            settings.BannerCorner = SettingsStore.NormalizeBannerCorner(
                bannerCorner);
            settings.BannerVerticalOffset =
                SettingsStore.NormalizeBannerVerticalOffset(
                    bannerVerticalOffset);
            settings.BannerSize = SettingsStore.NormalizeBannerSize(bannerSize);
            settings.BannerOpacity = SettingsStore.NormalizeBannerOpacity(
                bannerOpacity);
            settings.IdleDimming = idleDimming;
            return true;
        }
        catch
        {
            settings = null;
            return false;
        }
    }

    private static void CopySettingsForDuplicate(string sourcePath,
        string destinationPath, string destinationRdpPath,
        string shortcutName)
    {
        string[] sourceLines = File.ReadAllLines(sourcePath, Encoding.UTF8);
        List<string> result = new List<string>();
        bool wroteShortcut = false;
        bool wroteRdpFile = false;
        foreach (string rawLine in sourceLines)
        {
            int separator = rawLine.IndexOf('=');
            string key = separator <= 0
                ? ""
                : rawLine.Substring(0, separator).Trim();
            if (key.Equals("ShortcutName", StringComparison.OrdinalIgnoreCase))
            {
                if (!wroteShortcut)
                {
                    result.Add("ShortcutName=" + shortcutName);
                    wroteShortcut = true;
                }
            }
            else if (key.Equals("RdpFile", StringComparison.OrdinalIgnoreCase))
            {
                if (!wroteRdpFile)
                {
                    result.Add("RdpFile=" + destinationRdpPath);
                    wroteRdpFile = true;
                }
            }
            else
            {
                result.Add(rawLine);
            }
        }
        if (!wroteShortcut)
            result.Add("ShortcutName=" + shortcutName);
        if (!wroteRdpFile)
            result.Add("RdpFile=" + destinationRdpPath);

        string temporary = destinationPath + ".new";
        File.WriteAllLines(temporary, result.ToArray(),
            new UTF8Encoding(false));
        File.Move(temporary, destinationPath);
    }

    private static void ValidateTreeContainsNoReparsePoints(string directory)
    {
        if (IsReparsePoint(directory))
            throw new IOException("A profile reparse point will not be deleted.");

        foreach (string entry in Directory.GetFileSystemEntries(directory))
        {
            if (IsReparsePoint(entry))
                throw new IOException(
                    "A profile containing a reparse point will not be deleted.");
            if (Directory.Exists(entry))
                ValidateTreeContainsNoReparsePoints(entry);
        }
    }

    private static void DeleteKnownNewDirectory(string directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return;
        try
        {
            ValidateTreeContainsNoReparsePoints(directory);
            Directory.Delete(directory, true);
        }
        catch
        {
        }
    }

    private static bool TryNormalizeProfilesRoot(string profilesRoot,
        out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(profilesRoot))
            return false;
        try
        {
            normalized = Path.GetFullPath(profilesRoot).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.IsPathRooted(normalized) && normalized.Length > 2;
        }
        catch
        {
            return false;
        }
    }

    private static bool PathsEqual(string first, string second)
    {
        if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second))
            return false;
        try
        {
            string normalizedFirst = Path.GetFullPath(first).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedSecond = Path.GetFullPath(second).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(normalizedFirst, normalizedSecond,
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return true;
        }
    }

    private static string SafeSingleLine(string value, int maximumLength)
    {
        string result = string.IsNullOrEmpty(value)
            ? ""
            : value.Replace("\r", "").Replace("\n", "").Trim();
        if (result.Length > maximumLength)
            result = result.Substring(0, maximumLength);
        return result;
    }
}

internal sealed class ConnectionShortcutSnapshot
{
    public bool Inspected;
    public bool Exists;
    public string TargetPath;
    public string Arguments;
    public string IconLocation;
    public string WorkingDirectory;
}

internal sealed class ConnectionDiagnosticsContext
{
    public string ApplicationVersion;
    public string RuntimePath;
    public string MstscPath;
    public bool? ApplicationSignatureValid;
    public bool? RdpPublisherTrusted;
    public bool? UpdateAvailable;
    public string LatestVersion;
    public int[] SelectedMonitorIds;
    public int[] AvailableMonitorIds;
    public ConnectionShortcutSnapshot Shortcut;
}

internal static class ConnectionDiagnosticsBuilder
{
    public static string Build(ManagedConnectionInfo profile,
        ConnectionDiagnosticsContext context)
    {
        if (profile == null)
            throw new ArgumentNullException("profile");
        if (context == null)
            context = new ConnectionDiagnosticsContext();

        StringBuilder report = new StringBuilder();
        report.AppendLine("RDP Session Reminder diagnostic report");
        report.AppendLine("Generated (UTC): " +
            DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'",
                CultureInfo.InvariantCulture));
        report.AppendLine("Privacy: remote computer names, user names, reminder text, " +
            "and credential fields are intentionally omitted.");
        report.AppendLine();
        report.AppendLine("Application version: " +
            SafeVersion(context.ApplicationVersion));
        report.AppendLine("Canonical profile: " +
            YesNo(ConnectionProfileCatalog.IsCanonicalProfileId(
                profile.ProfileId)));
        report.AppendLine("Settings readable: " + YesNo(profile.SettingsValid));
        report.AppendLine("Copied RDP exists: " + YesNo(profile.RdpExists));
        report.AppendLine("Copied RDP path: %LOCALAPPDATA%\\RdpSessionReminder" +
            "\\profiles\\<PROFILE-ID>\\connection.rdp");
        report.AppendLine("Copied RDP privacy scan: selected-monitor IDs only; " +
            "endpoint, user, reminder, and credential fields are not read for diagnostics");
        report.AppendLine("Copied RDP SHA-256: " + GetRdpHash(profile));
        report.AppendLine("Reminder runtime: " +
            SanitizeExpectedRuntimePath(context.RuntimePath));
        report.AppendLine("Windows RDP client: " +
            SanitizeMstscPath(context.MstscPath));
        report.AppendLine("Reminder runtime locally trusted " +
            "(cached revocation check): " +
            NullableYesNo(context.ApplicationSignatureValid));
        report.AppendLine("RDP publisher trusted: " +
            NullableYesNo(context.RdpPublisherTrusted));
        if (context.UpdateAvailable.HasValue)
            report.AppendLine("Update available: " +
                NullableYesNo(context.UpdateAvailable));
        if (!string.IsNullOrWhiteSpace(context.LatestVersion))
            report.AppendLine("Latest version: " +
                SafeVersion(context.LatestVersion));
        AppendMonitorStatus(report, context.SelectedMonitorIds,
            context.AvailableMonitorIds);
        AppendShortcutStatus(report, profile, context);
        report.AppendLine();
        report.AppendLine("This report is created locally. It is copied or saved only " +
            "when you choose that action, and the app does not submit it.");
        return report.ToString();
    }

    private static void AppendShortcutStatus(StringBuilder report,
        ManagedConnectionInfo profile, ConnectionDiagnosticsContext context)
    {
        report.AppendLine();
        report.AppendLine("Shortcut");
        ConnectionShortcutSnapshot shortcut = context.Shortcut;
        if (shortcut == null || !shortcut.Inspected)
        {
            report.AppendLine("Inspected: No");
            return;
        }

        report.AppendLine("Inspected: Yes");
        report.AppendLine("Exists: " + YesNo(shortcut.Exists));
        if (!shortcut.Exists)
            return;

        bool expectedArguments = string.Equals(
            SafeSingleLine(shortcut.Arguments, 200),
            "--profile " + profile.ProfileId, StringComparison.Ordinal);
        bool expectedTarget = PathsEqual(shortcut.TargetPath,
            context.RuntimePath);
        report.AppendLine("Target: " + (expectedTarget
            ? SanitizeExpectedRuntimePath(shortcut.TargetPath)
            : "[unexpected target omitted]"));
        report.AppendLine("Arguments: " + (expectedArguments
            ? "--profile <PROFILE-ID>"
            : "[unexpected arguments omitted]"));
        report.AppendLine("Icon: " + SanitizeIconPath(shortcut.IconLocation));
        report.AppendLine("Start in: " +
            SanitizeWorkingDirectory(shortcut.WorkingDirectory,
                context.RuntimePath));
        report.AppendLine("Matches this app profile: " +
            YesNo(expectedTarget && expectedArguments));
    }

    private static void AppendMonitorStatus(StringBuilder report,
        int[] selected, int[] available)
    {
        report.AppendLine();
        report.AppendLine("Displays");
        if (selected == null)
        {
            report.AppendLine("Selected monitor IDs: Not supplied");
            return;
        }

        int[] safeSelected = NormalizeMonitorIds(selected);
        int[] safeAvailable = NormalizeMonitorIds(available);
        report.AppendLine("Selected monitor IDs: " + JoinMonitorIds(safeSelected));
        report.AppendLine("Available monitor IDs: " +
            (available == null ? "Not supplied" : JoinMonitorIds(safeAvailable)));
        if (available != null)
        {
            bool allAvailable = true;
            foreach (int selectedId in safeSelected)
            {
                if (Array.IndexOf(safeAvailable, selectedId) < 0)
                {
                    allAvailable = false;
                    break;
                }
            }
            report.AppendLine("Selected monitors currently available: " +
                YesNo(allAvailable));
        }
    }

    private static int[] NormalizeMonitorIds(int[] values)
    {
        if (values == null)
            return new int[0];
        List<int> result = new List<int>();
        foreach (int value in values)
        {
            if (value < 0 || value > 255 || result.Contains(value))
                continue;
            result.Add(value);
            if (result.Count == 32)
                break;
        }
        result.Sort();
        return result.ToArray();
    }

    private static string JoinMonitorIds(int[] values)
    {
        if (values == null || values.Length == 0)
            return "None";
        string[] text = new string[values.Length];
        for (int index = 0; index < values.Length; index++)
            text[index] = values[index].ToString(CultureInfo.InvariantCulture);
        return string.Join(", ", text);
    }

    private static string GetRdpHash(ManagedConnectionInfo profile)
    {
        if (!profile.RdpExists || string.IsNullOrEmpty(profile.RdpPath) ||
            !File.Exists(profile.RdpPath) || IsReparsePoint(profile.RdpPath))
            return "Unavailable";
        try
        {
            using (SHA256 algorithm = SHA256.Create())
            using (FileStream stream = new FileStream(profile.RdpPath,
                FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] hash = algorithm.ComputeHash(stream);
                StringBuilder text = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash)
                    text.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return text.ToString();
            }
        }
        catch
        {
            return "Unavailable";
        }
    }

    private static string SafeVersion(string value)
    {
        string safe = SafeSingleLine(value, 40);
        if (safe.Length == 0)
            return "Not supplied";
        foreach (char character in safe)
        {
            if (!char.IsLetterOrDigit(character) && character != '.' &&
                character != '-' && character != '+')
                return "[invalid version omitted]";
        }
        return safe;
    }

    private static string SanitizeExpectedRuntimePath(string path)
    {
        if (PathsEqual(path, AppPaths.RuntimePath))
            return "%LOCALAPPDATA%\\RdpSessionReminder\\" +
                AppPaths.RuntimeFileName;
        return string.IsNullOrEmpty(path)
            ? "Not supplied"
            : "[nonstandard path omitted]";
    }

    private static string SanitizeMstscPath(string path)
    {
        string expected = Path.Combine(Environment.SystemDirectory, "mstsc.exe");
        if (PathsEqual(path, expected))
            return "%SYSTEMROOT%\\System32\\mstsc.exe";
        return string.IsNullOrEmpty(path)
            ? "Not supplied"
            : "[nonstandard path omitted]";
    }

    private static string SanitizeIconPath(string iconLocation)
    {
        string safe = SafeSingleLine(iconLocation, 1024);
        if (safe.Length == 0)
            return "Not supplied";

        int comma = safe.LastIndexOf(',');
        string suffix = "";
        string path = safe;
        if (comma > 0)
        {
            path = safe.Substring(0, comma).Trim();
            int iconIndex;
            if (int.TryParse(safe.Substring(comma + 1).Trim(),
                    NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out iconIndex))
                suffix = "," + iconIndex.ToString(CultureInfo.InvariantCulture);
        }

        string mstsc = Path.Combine(Environment.SystemDirectory, "mstsc.exe");
        if (PathsEqual(path, mstsc))
            return "%SYSTEMROOT%\\System32\\mstsc.exe" + suffix;

        string installRoot = Path.GetFullPath(AppPaths.InstallDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch
        {
            return "[custom icon path omitted]";
        }
        string prefix = installRoot + Path.DirectorySeparatorChar;
        if (fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            string relative = fullPath.Substring(prefix.Length);
            if (relative.IndexOf("..", StringComparison.Ordinal) < 0 &&
                relative.IndexOfAny(new char[] { '\r', '\n' }) < 0)
                return "%LOCALAPPDATA%\\RdpSessionReminder\\" +
                    relative + suffix;
        }
        return "[custom icon path omitted]";
    }

    private static string SanitizeWorkingDirectory(string path,
        string runtimePath)
    {
        string safe = SafeSingleLine(path, 1024);
        if (safe.Length == 0)
            return "(empty; recommended)";
        string runtimeDirectory = string.IsNullOrEmpty(runtimePath)
            ? ""
            : Path.GetDirectoryName(runtimePath);
        if (PathsEqual(safe, runtimeDirectory) &&
            PathsEqual(runtimeDirectory, AppPaths.InstallDirectory))
            return "%LOCALAPPDATA%\\RdpSessionReminder";
        return "[nonstandard path omitted]";
    }

    private static string NullableYesNo(bool? value)
    {
        return value.HasValue ? YesNo(value.Value) : "Not supplied";
    }

    private static string YesNo(bool value)
    {
        return value ? "Yes" : "No";
    }

    private static string SafeSingleLine(string value, int maximumLength)
    {
        string safe = string.IsNullOrEmpty(value)
            ? ""
            : value.Replace("\r", "").Replace("\n", "").Trim();
        if (safe.Length > maximumLength)
            safe = safe.Substring(0, maximumLength);
        return safe;
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return true;
        }
    }

    private static bool PathsEqual(string first, string second)
    {
        if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second))
            return false;
        try
        {
            string normalizedFirst = Path.GetFullPath(first).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedSecond = Path.GetFullPath(second).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(normalizedFirst, normalizedSecond,
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}

internal sealed class ConnectionManagerOptions
{
    public string ApplicationVersion;
    public string RuntimePath;
    public bool CloseSetupAfterConnect;
    public Action<ManagedConnectionInfo> ConnectRequested;
    public Action<ManagedConnectionInfo> EditRequested;
    public Action<ManagedConnectionInfo> DuplicateCreated;
    public Action<ManagedConnectionInfo> RepairShortcutRequested;
    public Action<ManagedConnectionInfo> ProfileDeleting;
    public Action<ManagedConnectionInfo> ProfileDeleted;
    public Func<ManagedConnectionInfo, ConnectionDiagnosticsContext>
        DiagnosticsContextProvider;

    public ConnectionManagerOptions()
    {
        RuntimePath = AppPaths.RuntimePath;
        CloseSetupAfterConnect = true;
        try
        {
            ApplicationVersion = Assembly.GetExecutingAssembly()
                .GetName().Version.ToString();
        }
        catch
        {
            ApplicationVersion = "";
        }
    }
}

internal static class ConnectionManagerUi
{
    public static ConnectionManagerController Attach(Form owner,
        TabControl tabs, ConnectionManagerOptions options)
    {
        if (owner == null)
            throw new ArgumentNullException("owner");
        if (tabs == null)
            throw new ArgumentNullException("tabs");
        if (options == null)
            options = new ConnectionManagerOptions();
        return new ConnectionManagerController(owner, tabs, options);
    }
}

internal sealed class ConnectionManagerController
{
    private readonly Form owner;
    private readonly ConnectionManagerOptions options;
    private readonly TabPage page;
    private readonly ListView connections;
    private readonly Label status;
    private readonly Button connectButton;
    private readonly Button editButton;
    private readonly Button duplicateButton;
    private readonly Button moreButton;
    private readonly ToolStripMenuItem differentAccountMenuItem;
    private readonly ToolStripMenuItem repairMenuItem;
    private readonly ToolStripMenuItem diagnosticsMenuItem;
    private readonly ToolStripMenuItem deleteMenuItem;
    private bool busy;

    public TabPage Page
    {
        get { return page; }
    }

    public bool HasConnections
    {
        get { return connections != null && connections.Items.Count > 0; }
    }

    public ConnectionManagerController(Form ownerForm, TabControl tabs,
        ConnectionManagerOptions managerOptions)
    {
        owner = ownerForm;
        options = managerOptions;

        page = new TabPage("Saved connections");
        page.Padding = new Padding(10);
        tabs.TabPages.Add(page);

        Label explanation = new Label();
        explanation.AutoSize = false;
        explanation.Location = new Point(12, 10);
        explanation.Size = new Size(550, 48);
        explanation.Anchor = AnchorStyles.Top | AnchorStyles.Left |
            AnchorStyles.Right;
        explanation.Text =
            "Double-click a saved computer or select Connect for a direct RDP " +
            "session. Windows uses a saved account when available and prompts " +
            "if needed; this interface closes completely after starting it.";
        page.Controls.Add(explanation);

        connections = new ListView();
        connections.Location = new Point(12, 62);
        connections.Size = new Size(550, 210);
        connections.Anchor = AnchorStyles.Top | AnchorStyles.Left |
            AnchorStyles.Right;
        connections.View = View.Details;
        connections.FullRowSelect = true;
        connections.HideSelection = false;
        connections.MultiSelect = false;
        connections.Columns.Add("Connection", 220);
        connections.Columns.Add("Computer", 200);
        connections.Columns.Add("State", 100);
        connections.SelectedIndexChanged += SelectionChanged;
        connections.DoubleClick += ConnectClicked;
        connections.KeyDown += delegate(object sender, KeyEventArgs eventArgs)
        {
            if (eventArgs.KeyCode != Keys.Enter)
                return;
            ConnectClicked(sender, EventArgs.Empty);
            eventArgs.Handled = true;
            eventArgs.SuppressKeyPress = true;
        };
        page.Controls.Add(connections);

        FlowLayoutPanel actions = new FlowLayoutPanel();
        actions.Location = new Point(9, 280);
        actions.Size = new Size(556, 38);
        actions.Anchor = AnchorStyles.Top | AnchorStyles.Left |
            AnchorStyles.Right;
        actions.WrapContents = true;
        actions.AutoSize = false;
        page.Controls.Add(actions);

        connectButton = MakeButton("Connect", ConnectClicked, 80);
        editButton = MakeButton("Customize reminder...", EditClicked, 145);
        duplicateButton = MakeButton("Duplicate", DuplicateClicked, 82);
        moreButton = MakeButton("More...", ShowMoreMenu, 78);
        ContextMenuStrip moreMenu = new ContextMenuStrip();
        differentAccountMenuItem = new ToolStripMenuItem(
            "Connect with a different account once...");
        repairMenuItem = new ToolStripMenuItem("Repair desktop shortcut");
        diagnosticsMenuItem = new ToolStripMenuItem("Create sanitized diagnostics");
        deleteMenuItem = new ToolStripMenuItem("Delete app profile...");
        differentAccountMenuItem.Click += DifferentAccountClicked;
        repairMenuItem.Click += RepairClicked;
        diagnosticsMenuItem.Click += DiagnosticsClicked;
        deleteMenuItem.Click += DeleteClicked;
        moreMenu.Items.Add(differentAccountMenuItem);
        moreMenu.Items.Add(new ToolStripSeparator());
        moreMenu.Items.Add(repairMenuItem);
        moreMenu.Items.Add(diagnosticsMenuItem);
        moreMenu.Items.Add(new ToolStripSeparator());
        moreMenu.Items.Add(deleteMenuItem);
        moreButton.ContextMenuStrip = moreMenu;
        Button refreshButton = MakeButton("Refresh", RefreshClicked, 72);
        actions.Controls.Add(connectButton);
        actions.Controls.Add(editButton);
        actions.Controls.Add(duplicateButton);
        actions.Controls.Add(moreButton);
        actions.Controls.Add(refreshButton);

        status = new Label();
        status.AutoEllipsis = true;
        status.Location = new Point(12, 326);
        status.Size = new Size(550, 23);
        status.Anchor = AnchorStyles.Top | AnchorStyles.Left |
            AnchorStyles.Right;
        page.Controls.Add(status);

        page.Enter += delegate { RefreshProfiles(); };
        RefreshProfiles();
    }

    public void RefreshProfiles()
    {
        string selectedId = SelectedProfile == null
            ? ""
            : SelectedProfile.ProfileId;
        connections.BeginUpdate();
        try
        {
            connections.Items.Clear();
            IList<ManagedConnectionInfo> profiles =
                ConnectionProfileCatalog.EnumerateProfiles();
            foreach (ManagedConnectionInfo profile in profiles)
            {
                ListViewItem item = new ListViewItem(profile.DisplayName);
                item.SubItems.Add(profile.SettingsValid &&
                        profile.Settings != null
                    ? profile.Settings.ComputerName
                    : "Unavailable");
                item.SubItems.Add(profile.StateText);
                item.Tag = profile;
                connections.Items.Add(item);
                if (string.Equals(profile.ProfileId, selectedId,
                        StringComparison.Ordinal))
                    item.Selected = true;
            }
            status.Text = profiles.Count == 0
                ? "No app-created connections were found."
                : profiles.Count.ToString(CultureInfo.CurrentCulture) +
                    (profiles.Count == 1 ? " connection" : " connections");
        }
        catch (Exception exception)
        {
            connections.Items.Clear();
            status.Text = "Connections could not be read: " + exception.Message;
        }
        finally
        {
            connections.EndUpdate();
            UpdateActions();
        }
    }

    private Button MakeButton(string text, EventHandler handler, int width)
    {
        Button button = new Button();
        button.Text = text;
        button.Size = new Size(width, 28);
        button.Margin = new Padding(3);
        button.Click += handler;
        return button;
    }

    private ManagedConnectionInfo SelectedProfile
    {
        get
        {
            if (connections.SelectedItems.Count != 1)
                return null;
            return connections.SelectedItems[0].Tag as ManagedConnectionInfo;
        }
    }

    private void SelectionChanged(object sender, EventArgs eventArgs)
    {
        UpdateActions();
    }

    private void UpdateActions()
    {
        ManagedConnectionInfo selected = SelectedProfile;
        bool hasSelection = !busy && selected != null;
        connectButton.Enabled = hasSelection && selected.Ready;
        editButton.Enabled = hasSelection && options.EditRequested != null;
        duplicateButton.Enabled = hasSelection && selected.Ready;
        moreButton.Enabled = hasSelection;
        differentAccountMenuItem.Enabled = hasSelection && selected.Ready;
        repairMenuItem.Enabled = hasSelection && selected.Ready &&
            options.RepairShortcutRequested != null;
        diagnosticsMenuItem.Enabled = hasSelection;
        deleteMenuItem.Enabled = hasSelection;
    }

    private void ShowMoreMenu(object sender, EventArgs eventArgs)
    {
        if (!moreButton.Enabled || moreButton.ContextMenuStrip == null)
            return;
        moreButton.ContextMenuStrip.Show(moreButton,
            new Point(0, moreButton.Height));
    }

    private void ConnectClicked(object sender, EventArgs eventArgs)
    {
        ManagedConnectionInfo selected = SelectedProfile;
        if (selected == null || !selected.Ready)
            return;
        bool closeAfterStart = false;
        RunAction(delegate
        {
            if (options.ConnectRequested != null)
                options.ConnectRequested(selected);
            else
                StartReminder(selected, false);
            status.Text = "Connection started. Setup can close completely now.";
            if (options.CloseSetupAfterConnect)
                closeAfterStart = true;
        });
        if (closeAfterStart)
            owner.Close();
    }

    private void EditClicked(object sender, EventArgs eventArgs)
    {
        ManagedConnectionInfo selected = SelectedProfile;
        if (selected == null || options.EditRequested == null)
            return;
        RunAction(delegate { options.EditRequested(selected); });
    }

    private void DifferentAccountClicked(object sender, EventArgs eventArgs)
    {
        ManagedConnectionInfo selected = SelectedProfile;
        if (selected == null || !selected.Ready)
            return;

        bool closeAfterStart = false;
        RunAction(delegate
        {
            StartReminder(selected, true);
            status.Text = "Connection started. Windows Remote Desktop will ask " +
                "which account to use for this launch.";
            if (options.CloseSetupAfterConnect)
                closeAfterStart = true;
        });
        if (closeAfterStart)
            owner.Close();
    }

    private void DuplicateClicked(object sender, EventArgs eventArgs)
    {
        ManagedConnectionInfo selected = SelectedProfile;
        if (selected == null || !selected.Ready)
            return;

        string suggested = SettingsStore.NormalizeShortcutName(
            selected.DisplayName + " Copy", selected.Settings.ComputerName);
        string requested = DuplicateNameDialog.Show(owner, suggested);
        if (requested == null)
            return;

        RunAction(delegate
        {
            ManagedConnectionInfo duplicate =
                ConnectionProfileCatalog.DuplicateProfile(
                    selected.ProfileId, requested);
            if (options.DuplicateCreated != null)
                options.DuplicateCreated(duplicate);
            RefreshProfiles();
            SelectProfile(duplicate.ProfileId);
            status.Text = "Connection duplicated. You can edit or repair its shortcut.";
        });
    }

    private void RepairClicked(object sender, EventArgs eventArgs)
    {
        ManagedConnectionInfo selected = SelectedProfile;
        if (selected == null || options.RepairShortcutRequested == null)
            return;
        RunAction(delegate
        {
            options.RepairShortcutRequested(selected);
            status.Text = "Desktop shortcut and icon were recreated.";
        });
    }

    private void DeleteClicked(object sender, EventArgs eventArgs)
    {
        ManagedConnectionInfo selected = SelectedProfile;
        if (selected == null)
            return;

        DialogResult choice = MessageBox.Show(owner,
            "Delete the app-owned profile \"" + selected.DisplayName + "\"?\r\n\r\n" +
            "This removes its settings and copied RDP file. A shortcut that was " +
            "moved elsewhere may need to be deleted manually.",
            "Delete connection", MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (choice != DialogResult.Yes)
            return;

        RunAction(delegate
        {
            if (options.ProfileDeleting != null)
                options.ProfileDeleting(selected);
            ConnectionProfileCatalog.DeleteProfile(selected.ProfileId);
            if (options.ProfileDeleted != null)
                options.ProfileDeleted(selected);
            RefreshProfiles();
            status.Text = "The app-owned profile was deleted.";
        });
    }

    private void DiagnosticsClicked(object sender, EventArgs eventArgs)
    {
        ManagedConnectionInfo selected = SelectedProfile;
        if (selected == null)
            return;
        RunAction(delegate
        {
            ConnectionDiagnosticsContext context = options.DiagnosticsContextProvider == null
                ? BuildDefaultDiagnosticsContext(selected)
                : options.DiagnosticsContextProvider(selected);
            if (context == null)
                context = BuildDefaultDiagnosticsContext(selected);
            if (string.IsNullOrEmpty(context.ApplicationVersion))
                context.ApplicationVersion = options.ApplicationVersion;
            if (string.IsNullOrEmpty(context.RuntimePath))
                context.RuntimePath = options.RuntimePath;
            string report = ConnectionDiagnosticsBuilder.Build(selected, context);
            using (DiagnosticsReportForm dialog =
                new DiagnosticsReportForm(report))
                dialog.ShowDialog(owner);
        });
    }

    private void RefreshClicked(object sender, EventArgs eventArgs)
    {
        RefreshProfiles();
    }

    private void StartReminder(ManagedConnectionInfo profile,
        bool promptCredentials)
    {
        string runtime = string.IsNullOrEmpty(options.RuntimePath)
            ? AppPaths.RuntimePath
            : options.RuntimePath;
        if (!Path.IsPathRooted(runtime) || !File.Exists(runtime))
            throw new FileNotFoundException(
                "The reminder runtime is not installed.", runtime);

        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = runtime;
        startInfo.Arguments = "--profile " + profile.ProfileId +
            (promptCredentials ? " --prompt-credentials" : "");
        startInfo.UseShellExecute = false;
        Process.Start(startInfo);
    }

    private ConnectionDiagnosticsContext BuildDefaultDiagnosticsContext(
        ManagedConnectionInfo profile)
    {
        ConnectionDiagnosticsContext context = new ConnectionDiagnosticsContext();
        context.ApplicationVersion = options.ApplicationVersion;
        context.RuntimePath = options.RuntimePath;
        context.MstscPath = Path.Combine(Environment.SystemDirectory, "mstsc.exe");
        context.Shortcut = InspectExpectedDesktopShortcut(profile);
        return context;
    }

    private ConnectionShortcutSnapshot InspectExpectedDesktopShortcut(
        ManagedConnectionInfo profile)
    {
        ConnectionShortcutSnapshot snapshot = new ConnectionShortcutSnapshot();
        snapshot.Inspected = true;
        string desktop = Environment.GetFolderPath(
            Environment.SpecialFolder.DesktopDirectory);
        string shortcutPath = Path.Combine(desktop,
            profile.DisplayName + ".lnk");
        snapshot.Exists = File.Exists(shortcutPath);
        if (!snapshot.Exists)
            return snapshot;

        try
        {
            ReadShortcut(shortcutPath, snapshot);
        }
        catch
        {
            snapshot.TargetPath = "";
            snapshot.Arguments = "";
            snapshot.IconLocation = "";
            snapshot.WorkingDirectory = "";
        }
        return snapshot;
    }

    private static void ReadShortcut(string shortcutPath,
        ConnectionShortcutSnapshot snapshot)
    {
        Type shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null)
            return;
        object shell = Activator.CreateInstance(shellType);
        object shortcut = null;
        try
        {
            shortcut = shellType.InvokeMember("CreateShortcut",
                BindingFlags.InvokeMethod, null, shell,
                new object[] { shortcutPath });
            Type shortcutType = shortcut.GetType();
            snapshot.TargetPath = ReadShortcutProperty(
                shortcutType, shortcut, "TargetPath");
            snapshot.Arguments = ReadShortcutProperty(
                shortcutType, shortcut, "Arguments");
            snapshot.IconLocation = ReadShortcutProperty(
                shortcutType, shortcut, "IconLocation");
            snapshot.WorkingDirectory = ReadShortcutProperty(
                shortcutType, shortcut, "WorkingDirectory");
        }
        finally
        {
            if (shortcut != null && Marshal.IsComObject(shortcut))
                Marshal.FinalReleaseComObject(shortcut);
            if (Marshal.IsComObject(shell))
                Marshal.FinalReleaseComObject(shell);
        }
    }

    private static string ReadShortcutProperty(Type shortcutType,
        object shortcut, string propertyName)
    {
        object value = shortcutType.InvokeMember(propertyName,
            BindingFlags.GetProperty, null, shortcut, null);
        return value == null ? "" : value.ToString();
    }

    private void SelectProfile(string profileId)
    {
        foreach (ListViewItem item in connections.Items)
        {
            ManagedConnectionInfo profile = item.Tag as ManagedConnectionInfo;
            if (profile != null && string.Equals(profile.ProfileId, profileId,
                    StringComparison.Ordinal))
            {
                item.Selected = true;
                item.Focused = true;
                item.EnsureVisible();
                break;
            }
        }
    }

    private void RunAction(Action action)
    {
        if (busy)
            return;
        busy = true;
        UpdateActions();
        try
        {
            action();
        }
        catch (Exception exception)
        {
            status.Text = exception.Message;
            MessageBox.Show(owner, exception.Message,
                "RDP Session Reminder", MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            busy = false;
            UpdateActions();
        }
    }
}

internal static class DuplicateNameDialog
{
    public static string Show(IWin32Window owner, string suggestedName)
    {
        using (Form dialog = new Form())
        using (TextBox name = new TextBox())
        using (Button create = new Button())
        using (Button cancel = new Button())
        using (Label prompt = new Label())
        {
            dialog.Text = "Duplicate connection";
            dialog.StartPosition = FormStartPosition.CenterParent;
            dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
            dialog.MaximizeBox = false;
            dialog.MinimizeBox = false;
            dialog.ShowInTaskbar = false;
            dialog.ClientSize = new Size(430, 142);
            dialog.Font = new Font("Segoe UI", 9.5f);

            prompt.Text = "Name for the duplicated desktop shortcut";
            prompt.AutoSize = true;
            prompt.Location = new Point(18, 16);
            name.Location = new Point(20, 42);
            name.Size = new Size(390, 25);
            name.MaxLength = 80;
            name.Text = suggestedName;
            name.SelectAll();

            create.Text = "Duplicate";
            create.Location = new Point(230, 91);
            create.Size = new Size(86, 30);
            create.DialogResult = DialogResult.OK;
            cancel.Text = "Cancel";
            cancel.Location = new Point(324, 91);
            cancel.Size = new Size(86, 30);
            cancel.DialogResult = DialogResult.Cancel;
            dialog.AcceptButton = create;
            dialog.CancelButton = cancel;
            dialog.Controls.Add(prompt);
            dialog.Controls.Add(name);
            dialog.Controls.Add(create);
            dialog.Controls.Add(cancel);
            SetupVisualTheme.Apply(dialog);

            if (dialog.ShowDialog(owner) != DialogResult.OK)
                return null;
            return name.Text;
        }
    }
}

internal sealed class DiagnosticsReportForm : Form
{
    private readonly TextBox reportText;

    public DiagnosticsReportForm(string report)
    {
        Text = "Sanitized diagnostics";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(600, 460);
        ClientSize = new Size(720, 560);
        Font = new Font("Segoe UI", 9.5f);

        Label explanation = new Label();
        explanation.AutoSize = false;
        explanation.Location = new Point(16, 14);
        explanation.Size = new Size(688, 42);
        explanation.Anchor = AnchorStyles.Top | AnchorStyles.Left |
            AnchorStyles.Right;
        explanation.Text =
            "Remote computer names, user names, reminder text, and credential " +
            "fields are omitted. Nothing is sent automatically.";
        Controls.Add(explanation);

        reportText = new TextBox();
        reportText.Location = new Point(16, 62);
        reportText.Size = new Size(688, 440);
        reportText.Anchor = AnchorStyles.Top | AnchorStyles.Bottom |
            AnchorStyles.Left | AnchorStyles.Right;
        reportText.Multiline = true;
        reportText.ReadOnly = true;
        reportText.ScrollBars = ScrollBars.Both;
        reportText.WordWrap = false;
        reportText.Font = new Font("Consolas", 9f);
        reportText.Text = report;
        Controls.Add(reportText);

        Button copy = new Button();
        copy.Text = "Copy";
        copy.Location = new Point(446, 516);
        copy.Size = new Size(78, 30);
        copy.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        copy.Click += CopyClicked;
        Controls.Add(copy);

        Button save = new Button();
        save.Text = "Save...";
        save.Location = new Point(532, 516);
        save.Size = new Size(78, 30);
        save.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        save.Click += SaveClicked;
        Controls.Add(save);

        Button close = new Button();
        close.Text = "Close";
        close.Location = new Point(618, 516);
        close.Size = new Size(86, 30);
        close.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        close.DialogResult = DialogResult.OK;
        Controls.Add(close);
        AcceptButton = close;
        CancelButton = close;
        SetupVisualTheme.Apply(this);
    }

    private void CopyClicked(object sender, EventArgs eventArgs)
    {
        Clipboard.SetText(reportText.Text);
    }

    private void SaveClicked(object sender, EventArgs eventArgs)
    {
        using (SaveFileDialog dialog = new SaveFileDialog())
        {
            dialog.Title = "Save sanitized diagnostics";
            dialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
            dialog.FileName = "RdpSessionReminder-Diagnostics.txt";
            dialog.AddExtension = true;
            dialog.DefaultExt = "txt";
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;
            File.WriteAllText(dialog.FileName, reportText.Text,
                new UTF8Encoding(false));
        }
    }
}
