using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Microsoft.Win32.SafeHandles;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

internal sealed class UpdatePreferences
{
    public bool AutomaticChecks;
    public DateTime LastCheckUtc;
}

internal static class UpdatePreferenceStore
{
    public const string FileName = "update-settings.ini";
    private static readonly TimeSpan AutomaticCheckInterval =
        TimeSpan.FromHours(24);

    public static string FilePath
    {
        get { return Path.Combine(AppPaths.InstallDirectory, FileName); }
    }

    public static UpdatePreferences Load()
    {
        UpdatePreferences preferences = new UpdatePreferences();
        if (!File.Exists(FilePath))
            return preferences;

        try
        {
            FileInfo information = new FileInfo(FilePath);
            if (information.Length < 0 || information.Length > 4096)
                return preferences;

            return Parse(File.ReadAllText(FilePath, Encoding.UTF8));
        }
        catch
        {
            return preferences;
        }
    }

    internal static UpdatePreferences Parse(string content)
    {
        UpdatePreferences preferences = new UpdatePreferences();
        if (string.IsNullOrEmpty(content))
            return preferences;

        foreach (string rawLine in content.Replace("\r\n", "\n").Split('\n'))
        {
            int separator = rawLine.IndexOf('=');
            if (separator <= 0)
                continue;
            string key = rawLine.Substring(0, separator).Trim();
            string value = rawLine.Substring(separator + 1).Trim();
            if (key.Equals("AutomaticChecks", StringComparison.OrdinalIgnoreCase))
            {
                preferences.AutomaticChecks = value == "1";
            }
            else if (key.Equals("LastCheckUtc", StringComparison.OrdinalIgnoreCase))
            {
                DateTime parsed;
                if (DateTime.TryParseExact(value, "o", CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal |
                        DateTimeStyles.AdjustToUniversal, out parsed))
                    preferences.LastCheckUtc = parsed;
            }
        }
        return preferences;
    }

    internal static string Serialize(UpdatePreferences preferences)
    {
        if (preferences == null)
            throw new ArgumentNullException("preferences");
        return
            "AutomaticChecks=" + (preferences.AutomaticChecks ? "1" : "0") +
            Environment.NewLine +
            "LastCheckUtc=" + (preferences.LastCheckUtc == DateTime.MinValue
                ? ""
                : preferences.LastCheckUtc.ToUniversalTime().ToString(
                    "o", CultureInfo.InvariantCulture)) +
            Environment.NewLine;
    }

    public static void Save(UpdatePreferences preferences)
    {
        Directory.CreateDirectory(AppPaths.InstallDirectory);
        string temporary = FilePath + ".new-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporary, Serialize(preferences),
                new UTF8Encoding(false));
            if (File.Exists(FilePath))
                File.Replace(temporary, FilePath, null, true);
            else
                File.Move(temporary, FilePath);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    public static bool IsAutomaticCheckDue(UpdatePreferences preferences,
        DateTime utcNow)
    {
        if (preferences == null || !preferences.AutomaticChecks)
            return false;
        if (preferences.LastCheckUtc == DateTime.MinValue)
            return true;
        TimeSpan elapsed = utcNow.ToUniversalTime() -
            preferences.LastCheckUtc.ToUniversalTime();
        return elapsed < TimeSpan.Zero || elapsed >= AutomaticCheckInterval;
    }
}

internal sealed class StableVersion : IComparable<StableVersion>
{
    private static readonly Regex Pattern = new Regex(
        @"\Av?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public readonly int Major;
    public readonly int Minor;
    public readonly int Patch;

    public StableVersion(int major, int minor, int patch)
    {
        if (major < 0 || minor < 0 || patch < 0)
            throw new ArgumentOutOfRangeException("Version parts cannot be negative.");
        Major = major;
        Minor = minor;
        Patch = patch;
    }

    public static bool TryParse(string value, out StableVersion version)
    {
        version = null;
        Match match = Pattern.Match(value ?? "");
        if (!match.Success)
            return false;

        int major;
        int minor;
        int patch;
        if (!int.TryParse(match.Groups[1].Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out major) ||
            !int.TryParse(match.Groups[2].Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out minor) ||
            !int.TryParse(match.Groups[3].Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out patch))
            return false;
        version = new StableVersion(major, minor, patch);
        return true;
    }

    public static StableVersion FromAssemblyVersion(Version version)
    {
        if (version == null)
            throw new ArgumentNullException("version");
        return new StableVersion(Math.Max(0, version.Major),
            Math.Max(0, version.Minor), Math.Max(0, version.Build));
    }

    public int CompareTo(StableVersion other)
    {
        if (other == null)
            return 1;
        int comparison = Major.CompareTo(other.Major);
        if (comparison != 0)
            return comparison;
        comparison = Minor.CompareTo(other.Minor);
        if (comparison != 0)
            return comparison;
        return Patch.CompareTo(other.Patch);
    }

    public override string ToString()
    {
        return Major.ToString(CultureInfo.InvariantCulture) + "." +
            Minor.ToString(CultureInfo.InvariantCulture) + "." +
            Patch.ToString(CultureInfo.InvariantCulture);
    }
}

internal sealed class UpdateRelease
{
    public StableVersion Version;
    public string TagName;
    public string Title;
    public string Notes;
    public Uri InstallerDownload;
    public Uri ChecksumsDownload;
    public string InstallerApiSha256;

    public Uri ReleasePage
    {
        get
        {
            return new Uri("https://github.com/ArxivumDev/rdp-session-reminder/" +
                "releases/tag/" + Uri.EscapeDataString(TagName));
        }
    }
}

internal sealed class UpdateCheckResult
{
    public StableVersion CurrentVersion;
    public UpdateRelease LatestRelease;
    public List<UpdateRelease> NewerReleases = new List<UpdateRelease>();

    public bool UpdateAvailable
    {
        get
        {
            return LatestRelease != null && LatestRelease.Version.CompareTo(
                CurrentVersion) > 0;
        }
    }

    public bool InstallerAvailable
    {
        get
        {
            return UpdateAvailable && LatestRelease.InstallerDownload != null &&
                LatestRelease.ChecksumsDownload != null &&
                !string.IsNullOrEmpty(LatestRelease.InstallerApiSha256) &&
                Regex.IsMatch(LatestRelease.InstallerApiSha256,
                    @"\A[0-9A-Fa-f]{64}\z", RegexOptions.CultureInvariant);
        }
    }

    public string GetCumulativeReleaseNotes()
    {
        if (NewerReleases.Count == 0)
            return "No newer release notes.";

        const int maximumOutputCharacters = 512 * 1024;
        const int maximumNotesPerRelease = 32 * 1024;
        StringBuilder builder = new StringBuilder();
        int includedReleases = 0;
        foreach (UpdateRelease release in NewerReleases)
        {
            if (builder.Length > maximumOutputCharacters - 2048)
                break;
            if (builder.Length > 0)
                builder.AppendLine().AppendLine();
            builder.Append("Version ").Append(release.Version.ToString());
            if (!string.IsNullOrWhiteSpace(release.Title) &&
                !string.Equals(release.Title.Trim(), release.TagName,
                    StringComparison.OrdinalIgnoreCase))
                builder.Append(" - ").Append(SafeDisplayText(release.Title).Trim());
            builder.AppendLine();
            string notes = SafeDisplayText(release.Notes).Trim();
            if (notes.Length > maximumNotesPerRelease)
                notes = notes.Substring(0, maximumNotesPerRelease) +
                    "\r\n[This release note was shortened in the app. Open the " +
                    "official release page for the complete text.]";
            builder.Append(notes.Length == 0
                ? "No release notes were provided."
                : notes);
            includedReleases++;
        }
        if (includedReleases < NewerReleases.Count)
            builder.AppendLine().AppendLine().Append("[")
                .Append((NewerReleases.Count - includedReleases).ToString(
                    CultureInfo.InvariantCulture))
                .Append(" additional release note(s) omitted from this view. ")
                .Append("Open the official releases page for the complete history.]");
        return builder.ToString();
    }

    private static string SafeDisplayText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        StringBuilder builder = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            if (character == '\r' || character == '\n' || character == '\t' ||
                !char.IsControl(character))
                builder.Append(character);
        }
        return builder.ToString();
    }
}

internal sealed class UpdateCatalogPage
{
    public int RawReleaseCount;
    public List<UpdateRelease> StableReleases = new List<UpdateRelease>();
}

internal static class UpdateCatalog
{
    public const string InstallerAssetName =
        "RdpSessionReminder-Installer.exe";
    public const string ChecksumsAssetName = "SHA256SUMS.txt";

    public static UpdateCatalogPage ParseReleasePage(string json)
    {
        object root = StrictJsonParser.Parse(json);
        IList releaseItems = root as IList;
        if (releaseItems == null)
            throw new InvalidDataException(
                "GitHub returned an unexpected release catalog.");

        UpdateCatalogPage page = new UpdateCatalogPage();
        page.RawReleaseCount = releaseItems.Count;
        foreach (object item in releaseItems)
        {
            IDictionary<string, object> releaseObject =
                item as IDictionary<string, object>;
            if (releaseObject == null)
                throw new InvalidDataException(
                    "GitHub returned an invalid release entry.");

            if (GetBoolean(releaseObject, "draft") ||
                GetBoolean(releaseObject, "prerelease"))
                continue;

            string tagName = GetString(releaseObject, "tag_name");
            StableVersion version;
            if (!StableVersion.TryParse(tagName, out version))
                continue;

            UpdateRelease release = new UpdateRelease();
            release.Version = version;
            release.TagName = tagName;
            release.Title = LimitText(
                GetOptionalString(releaseObject, "name"), 512);
            release.Notes = LimitText(
                GetOptionalString(releaseObject, "body"), 64 * 1024);

            IList assets = GetList(releaseObject, "assets");
            int installerMatches = 0;
            int checksumMatches = 0;
            string installerApiSha256 = "";
            if (assets != null)
            {
                foreach (object assetItem in assets)
                {
                    IDictionary<string, object> asset =
                        assetItem as IDictionary<string, object>;
                    if (asset == null)
                        continue;
                    string name = GetOptionalString(asset, "name");
                    if (string.Equals(name, InstallerAssetName,
                            StringComparison.Ordinal))
                    {
                        installerMatches++;
                        installerApiSha256 = GetOptionalSha256Digest(asset);
                    }
                    else if (string.Equals(name, ChecksumsAssetName,
                            StringComparison.Ordinal))
                        checksumMatches++;
                }
            }
            if (installerMatches == 1)
            {
                release.InstallerDownload = BuildAssetUri(
                    tagName, InstallerAssetName);
                release.InstallerApiSha256 = installerApiSha256;
            }
            if (checksumMatches == 1)
                release.ChecksumsDownload = BuildAssetUri(
                    tagName, ChecksumsAssetName);
            page.StableReleases.Add(release);
        }
        return page;
    }

    public static UpdateCheckResult SelectUpdate(StableVersion currentVersion,
        IEnumerable<UpdateRelease> releases)
    {
        if (currentVersion == null)
            throw new ArgumentNullException("currentVersion");
        if (releases == null)
            throw new ArgumentNullException("releases");

        Dictionary<string, UpdateRelease> byVersion =
            new Dictionary<string, UpdateRelease>(StringComparer.Ordinal);
        foreach (UpdateRelease release in releases)
        {
            if (release == null || release.Version == null)
                continue;
            string key = release.Version.ToString();
            if (byVersion.ContainsKey(key))
                throw new InvalidDataException(
                    "The release catalog contains the same stable version more than once.");
            byVersion.Add(key, release);
        }

        List<UpdateRelease> sorted = new List<UpdateRelease>(byVersion.Values);
        sorted.Sort(delegate(UpdateRelease left, UpdateRelease right)
        {
            return left.Version.CompareTo(right.Version);
        });

        UpdateCheckResult result = new UpdateCheckResult();
        result.CurrentVersion = currentVersion;
        if (sorted.Count > 0)
            result.LatestRelease = sorted[sorted.Count - 1];
        foreach (UpdateRelease release in sorted)
        {
            if (release.Version.CompareTo(currentVersion) > 0)
                result.NewerReleases.Add(release);
        }
        return result;
    }

    private static Uri BuildAssetUri(string tagName, string assetName)
    {
        return new Uri("https://github.com/ArxivumDev/rdp-session-reminder/" +
            "releases/download/" + Uri.EscapeDataString(tagName) + "/" +
            Uri.EscapeDataString(assetName));
    }

    private static string GetString(IDictionary<string, object> value,
        string key)
    {
        object item;
        string text;
        if (!value.TryGetValue(key, out item) ||
            (text = item as string) == null)
            throw new InvalidDataException(
                "The release catalog is missing " + key + ".");
        return text;
    }

    private static string GetOptionalString(IDictionary<string, object> value,
        string key)
    {
        object item;
        if (!value.TryGetValue(key, out item) || item == null)
            return "";
        string text = item as string;
        if (text == null)
            throw new InvalidDataException(
                "The release catalog has an invalid " + key + ".");
        return text;
    }

    private static bool GetBoolean(IDictionary<string, object> value,
        string key)
    {
        object item;
        if (!value.TryGetValue(key, out item) || !(item is bool))
            throw new InvalidDataException(
                "The release catalog has an invalid " + key + ".");
        return (bool)item;
    }

    private static IList GetList(IDictionary<string, object> value, string key)
    {
        object item;
        if (!value.TryGetValue(key, out item) || item == null)
            return null;
        IList list = item as IList;
        if (list == null)
            throw new InvalidDataException(
                "The release catalog has an invalid " + key + ".");
        return list;
    }

    private static string GetOptionalSha256Digest(
        IDictionary<string, object> asset)
    {
        object digestValue;
        if (!asset.TryGetValue("digest", out digestValue) || digestValue == null)
            return "";
        string digest = digestValue as string;
        if (digest == null || !Regex.IsMatch(digest,
                @"\Asha256:[0-9A-Fa-f]{64}\z", RegexOptions.CultureInvariant))
            return "";
        return digest.Substring("sha256:".Length).ToUpperInvariant();
    }

    private static string LimitText(string value, int maximumCharacters)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maximumCharacters)
            return value ?? "";
        return value.Substring(0, maximumCharacters);
    }
}

internal static class ChecksumFileParser
{
    private static readonly Regex ExactChecksumLine = new Regex(
        @"\A([0-9A-Fa-f]{64})  RdpSessionReminder-Installer\.exe\z",
        RegexOptions.CultureInvariant);

    public static string GetInstallerSha256(string content)
    {
        if (content == null)
            throw new ArgumentNullException("content");
        string found = null;
        foreach (string originalLine in content.Split('\n'))
        {
            string line = originalLine.EndsWith("\r", StringComparison.Ordinal)
                ? originalLine.Substring(0, originalLine.Length - 1)
                : originalLine;
            Match match = ExactChecksumLine.Match(line);
            if (match.Success)
            {
                if (found != null)
                    throw new InvalidDataException(
                        "SHA256SUMS.txt contains more than one installer checksum.");
                found = match.Groups[1].Value.ToUpperInvariant();
            }
            else if (line.IndexOf(UpdateCatalog.InstallerAssetName,
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new InvalidDataException(
                    "SHA256SUMS.txt contains a malformed or ambiguous installer line.");
            }
        }
        if (found == null)
            throw new InvalidDataException(
                "SHA256SUMS.txt does not contain the exact installer filename.");
        return found;
    }
}

internal static class UpdateIntegrity
{
    public static void ValidateExpectedDigests(string checksumSha256,
        string apiSha256)
    {
        ValidateHash(checksumSha256, "SHA256SUMS.txt");
        ValidateHash(apiSha256, "GitHub asset digest");
        if (!string.Equals(checksumSha256, apiSha256,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                "The GitHub asset digest does not match SHA256SUMS.txt. " +
                "The installer will not be downloaded or opened.");
    }

    public static void ValidateDownloadedInstaller(string checksumSha256,
        string apiSha256, string downloadedSha256)
    {
        ValidateExpectedDigests(checksumSha256, apiSha256);
        ValidateHash(downloadedSha256, "downloaded installer");
        if (!string.Equals(checksumSha256, downloadedSha256,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(apiSha256, downloadedSha256,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                "The downloaded installer SHA-256 does not match the verified " +
                "release metadata. The installer was deleted and will not be opened.");
    }

    private static void ValidateHash(string value, string source)
    {
        if (string.IsNullOrEmpty(value) || !Regex.IsMatch(value,
                @"\A[0-9A-Fa-f]{64}\z", RegexOptions.CultureInvariant))
            throw new InvalidDataException(source +
                " did not contain a valid SHA-256 value.");
    }
}

internal sealed class PreparedUpdate
{
    public string TemporaryDirectory;
    public string InstallerPath;
    public string ExpectedSha256;
    public string ApiSha256;
    public UpdateRelease Release;

    public string GetInstallerArguments(int setupProcessId)
    {
        if (setupProcessId <= 0)
            throw new ArgumentOutOfRangeException("setupProcessId");
        return "--temporary-update --wait-for-pid " +
            setupProcessId.ToString(CultureInfo.InvariantCulture);
    }
}

internal sealed class VerifiedUpdateLaunch : IDisposable
{
    private FileStream installerLock;
    private SafeFileHandle directoryLock;

    public string InstallerPath { get; private set; }
    public string WorkingDirectory { get; private set; }

    internal VerifiedUpdateLaunch(string installerPath, string workingDirectory,
        FileStream installerStream, SafeFileHandle directoryHandle)
    {
        if (installerStream == null)
            throw new ArgumentNullException("installerStream");
        if (directoryHandle == null)
            throw new ArgumentNullException("directoryHandle");
        InstallerPath = installerPath;
        WorkingDirectory = workingDirectory;
        installerLock = installerStream;
        directoryLock = directoryHandle;
    }

    public void Dispose()
    {
        FileStream stream = installerLock;
        installerLock = null;
        if (stream != null)
            stream.Dispose();

        SafeFileHandle directory = directoryLock;
        directoryLock = null;
        if (directory != null)
            directory.Dispose();
    }
}

internal sealed class UpdateClient
{
    private const string RepositoryApi =
        "https://api.github.com/repos/ArxivumDev/rdp-session-reminder/releases";
    private const int ReleasesPerPage = 100;
    private const int MaximumReleasePages = 5;
    private const int MaximumCatalogBytes = 4 * 1024 * 1024;
    private const int MaximumChecksumBytes = 64 * 1024;
    private const int MaximumInstallerBytes = 128 * 1024 * 1024;
    private const int ConnectTimeoutMilliseconds = 15000;
    private const int ReadTimeoutMilliseconds = 30000;
    private const string TemporaryPrefix = "RdpSessionReminderUpdate-";
    private const uint GenericRead = 0x80000000;
    private const uint FileReadAttributes = 0x00000080;
    private const uint OpenExisting = 3;
    private const uint FileAttributeDirectory = 0x00000010;
    private const uint FileAttributeReparsePoint = 0x00000400;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint FileFlagSequentialScan = 0x08000000;
    private const uint VolumeNameDos = 0;
    private readonly string userAgent;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeFileTime
    {
        public uint LowDateTime;
        public uint HighDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public NativeFileTime CreationTime;
        public NativeFileTime LastAccessTime;
        public NativeFileTime LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName, uint desiredAccess, FileShare shareMode,
        IntPtr securityAttributes, uint creationDisposition,
        uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file, out ByHandleFileInformation information);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(
        SafeFileHandle file, StringBuilder path, uint pathLength, uint flags);

    public UpdateClient(StableVersion currentVersion)
    {
        if (currentVersion == null)
            throw new ArgumentNullException("currentVersion");
        userAgent = "RdpSessionReminder/" + currentVersion.ToString() +
            " (+https://github.com/ArxivumDev/rdp-session-reminder)";
    }

    public UpdateCheckResult CheckForUpdates(StableVersion currentVersion)
    {
        if (currentVersion == null)
            throw new ArgumentNullException("currentVersion");
        List<UpdateRelease> releases = new List<UpdateRelease>();
        for (int pageNumber = 1; pageNumber <= MaximumReleasePages; pageNumber++)
        {
            Uri uri = new Uri(RepositoryApi + "?per_page=" +
                ReleasesPerPage.ToString(CultureInfo.InvariantCulture) +
                "&page=" + pageNumber.ToString(CultureInfo.InvariantCulture));
            byte[] bytes = DownloadBytes(uri, MaximumCatalogBytes,
                "application/vnd.github+json");
            string json = DecodeUtf8(bytes, "GitHub release catalog");
            UpdateCatalogPage page = UpdateCatalog.ParseReleasePage(json);
            releases.AddRange(page.StableReleases);
            if (page.RawReleaseCount < ReleasesPerPage)
                return UpdateCatalog.SelectUpdate(currentVersion, releases);
        }
        throw new InvalidDataException(
            "The release history is larger than the updater safety limit. " +
            "Open the official release page to update manually.");
    }

    public PreparedUpdate DownloadAndVerify(UpdateRelease release)
    {
        if (release == null)
            throw new ArgumentNullException("release");
        if (release.InstallerDownload == null ||
            release.ChecksumsDownload == null)
            throw new InvalidOperationException(
                "This release does not include both required update files.");
        if (string.IsNullOrEmpty(release.InstallerApiSha256) ||
            !Regex.IsMatch(release.InstallerApiSha256,
                @"\A[0-9A-Fa-f]{64}\z", RegexOptions.CultureInvariant))
            throw new InvalidDataException(
                "GitHub did not provide the required SHA-256 asset digest. " +
                "Automatic installation is disabled for this release.");

        CleanupStaleDownloads();
        string directory = Path.Combine(Path.GetTempPath(),
            TemporaryPrefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            byte[] checksumBytes = DownloadBytes(release.ChecksumsDownload,
                MaximumChecksumBytes, "text/plain");
            string checksumText = DecodeUtf8(checksumBytes, "SHA256SUMS.txt");
            string expected = ChecksumFileParser.GetInstallerSha256(checksumText);
            UpdateIntegrity.ValidateExpectedDigests(
                expected, release.InstallerApiSha256);
            string checksumPath = Path.Combine(directory,
                UpdateCatalog.ChecksumsAssetName);
            File.WriteAllBytes(checksumPath, checksumBytes);

            string installerPath = Path.Combine(directory,
                UpdateCatalog.InstallerAssetName);
            DownloadFile(release.InstallerDownload, installerPath,
                MaximumInstallerBytes, "application/octet-stream");
            string actual = ComputeSha256(installerPath);
            UpdateIntegrity.ValidateDownloadedInstaller(
                expected, release.InstallerApiSha256, actual);

            PreparedUpdate prepared = new PreparedUpdate();
            prepared.TemporaryDirectory = directory;
            prepared.InstallerPath = installerPath;
            prepared.ExpectedSha256 = expected;
            prepared.ApiSha256 = release.InstallerApiSha256;
            prepared.Release = release;
            return prepared;
        }
        catch
        {
            TryDeleteDirectory(directory);
            throw;
        }
    }

    public static VerifiedUpdateLaunch OpenVerifiedInstallerForLaunch(
        PreparedUpdate prepared)
    {
        if (prepared == null || string.IsNullOrEmpty(prepared.InstallerPath) ||
            string.IsNullOrEmpty(prepared.ExpectedSha256) ||
            string.IsNullOrEmpty(prepared.ApiSha256))
            throw new InvalidDataException(
                "The verified update installer is no longer available.");

        string directory;
        string installer;
        try
        {
            directory = Path.GetFullPath(prepared.TemporaryDirectory)
                .TrimEnd(Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
            installer = Path.GetFullPath(prepared.InstallerPath);
        }
        catch (Exception exception)
        {
            throw new InvalidDataException(
                "The temporary update location is no longer safe.", exception);
        }

        if (!IsSafeTemporaryUpdateDirectory(directory) ||
            !string.Equals(installer,
                Path.Combine(directory, UpdateCatalog.InstallerAssetName),
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                "The temporary update location is no longer safe.");

        SafeFileHandle directoryHandle = null;
        SafeFileHandle installerHandle = null;
        FileStream installerStream = null;
        try
        {
            directoryHandle = OpenPathWithoutFollowingReparsePoint(directory,
                true);
            ValidateOpenedPath(directoryHandle, directory, true,
                "temporary update directory");

            installerHandle = OpenPathWithoutFollowingReparsePoint(installer,
                false);
            ValidateOpenedPath(installerHandle, installer, false,
                "update installer");

            // FileShare.Read on this handle denies writes, deletes, and renames.
            // The directory handle also denies renaming the containing directory.
            // Both remain open until Process.Start returns in UpdateUi.
            installerStream = new FileStream(installerHandle, FileAccess.Read);
            installerHandle = null; // FileStream owns the native handle now.
            string actual = ComputeSha256(installerStream);
            UpdateIntegrity.ValidateDownloadedInstaller(
                prepared.ExpectedSha256, prepared.ApiSha256, actual);
            installerStream.Position = 0;

            VerifiedUpdateLaunch launch = new VerifiedUpdateLaunch(installer,
                directory, installerStream, directoryHandle);
            installerStream = null;
            directoryHandle = null;
            return launch;
        }
        finally
        {
            if (installerStream != null)
                installerStream.Dispose();
            if (installerHandle != null)
                installerHandle.Dispose();
            if (directoryHandle != null)
                directoryHandle.Dispose();
        }
    }

    public static void DeletePreparedUpdate(PreparedUpdate prepared)
    {
        if (prepared != null)
            TryDeleteDirectory(prepared.TemporaryDirectory);
    }

    private byte[] DownloadBytes(Uri uri, int maximumBytes, string accept)
    {
        using (MemoryStream output = new MemoryStream())
        {
            DownloadToStream(uri, output, maximumBytes, accept);
            return output.ToArray();
        }
    }

    private void DownloadFile(Uri uri, string path, int maximumBytes,
        string accept)
    {
        string temporary = path + ".download";
        try
        {
            using (FileStream output = new FileStream(temporary,
                FileMode.CreateNew, FileAccess.Write, FileShare.None))
                DownloadToStream(uri, output, maximumBytes, accept);
            File.Move(temporary, path);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private void DownloadToStream(Uri originalUri, Stream output,
        int maximumBytes, string accept)
    {
        // .NET Framework 4 can otherwise inherit an obsolete TLS default.
        // GitHub requires TLS 1.2 or newer; this app targets TLS 1.2 explicitly.
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
        Uri current = originalUri;
        for (int redirect = 0; redirect <= 5; redirect++)
        {
            ValidateDownloadUri(current);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(current);
            request.Method = "GET";
            request.UserAgent = userAgent;
            request.Accept = accept;
            request.AllowAutoRedirect = false;
            request.AutomaticDecompression = DecompressionMethods.GZip |
                DecompressionMethods.Deflate;
            request.Timeout = ConnectTimeoutMilliseconds;
            request.ReadWriteTimeout = ReadTimeoutMilliseconds;
            request.UseDefaultCredentials = false;
            request.PreAuthenticate = false;

            try
            {
                using (HttpWebResponse response =
                    (HttpWebResponse)request.GetResponse())
                {
                    int status = (int)response.StatusCode;
                    if (status == 301 || status == 302 || status == 303 ||
                        status == 307 || status == 308)
                    {
                        string location = response.Headers["Location"];
                        if (string.IsNullOrEmpty(location))
                            throw new InvalidDataException(
                                "GitHub returned a redirect without a destination.");
                        current = new Uri(current, location);
                        continue;
                    }
                    if (status < 200 || status >= 300)
                        throw new WebException(
                            "GitHub returned HTTP " + status.ToString(
                                CultureInfo.InvariantCulture) + ".");
                    if (response.ContentLength > maximumBytes)
                        throw new InvalidDataException(
                            "The update download is larger than the safety limit.");

                    using (Stream input = response.GetResponseStream())
                    {
                        if (input == null)
                            throw new IOException(
                                "GitHub returned an empty response stream.");
                        byte[] buffer = new byte[32768];
                        long total = 0;
                        while (true)
                        {
                            int count = input.Read(buffer, 0, buffer.Length);
                            if (count <= 0)
                                break;
                            total += count;
                            if (total > maximumBytes)
                                throw new InvalidDataException(
                                    "The update download is larger than the safety limit.");
                            output.Write(buffer, 0, count);
                        }
                    }
                    return;
                }
            }
            catch (WebException exception)
            {
                throw new IOException(
                    "The secure connection to GitHub failed. " + exception.Message,
                    exception);
            }
        }
        throw new InvalidDataException(
            "GitHub returned too many redirects for the update download.");
    }

    private static void ValidateDownloadUri(Uri uri)
    {
        if (uri == null || !uri.IsAbsoluteUri ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidDataException(
                "The update download did not use an approved HTTPS address.");

        string host = uri.DnsSafeHost;
        bool allowed =
            host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("release-assets.githubusercontent.com",
                StringComparison.OrdinalIgnoreCase) ||
            host.Equals("objects.githubusercontent.com",
                StringComparison.OrdinalIgnoreCase) ||
            host.Equals("github-releases.githubusercontent.com",
                StringComparison.OrdinalIgnoreCase);
        if (!allowed)
            throw new InvalidDataException(
                "GitHub redirected the update to an unapproved host.");
    }

    private static string DecodeUtf8(byte[] bytes, string description)
    {
        try
        {
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException(
                description + " is not valid UTF-8.", exception);
        }
    }

    private static string ComputeSha256(string path)
    {
        using (FileStream stream = new FileStream(path, FileMode.Open,
            FileAccess.Read, FileShare.Read))
            return ComputeSha256(stream);
    }

    private static string ComputeSha256(Stream stream)
    {
        using (SHA256 algorithm = SHA256.Create())
        {
            byte[] hash = algorithm.ComputeHash(stream);
            StringBuilder builder = new StringBuilder(hash.Length * 2);
            foreach (byte value in hash)
                builder.Append(value.ToString("X2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }

    private static SafeFileHandle OpenPathWithoutFollowingReparsePoint(
        string path, bool directory)
    {
        uint desiredAccess = directory ? FileReadAttributes : GenericRead;
        FileShare share = directory
            ? FileShare.Read | FileShare.Write
            : FileShare.Read;
        // BACKUP_SEMANTICS lets us open and reject a directory reparse point
        // even when it appears where the installer file should be.
        uint flags = FileFlagOpenReparsePoint | FileFlagBackupSemantics;
        if (!directory)
            flags |= FileFlagSequentialScan;

        SafeFileHandle handle = CreateFile(path, desiredAccess, share,
            IntPtr.Zero, OpenExisting, flags, IntPtr.Zero);
        if (handle == null || handle.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            if (handle != null)
                handle.Dispose();
            throw new IOException(
                "Windows could not lock the verified update location (error " +
                error.ToString(CultureInfo.InvariantCulture) + ").",
                new Win32Exception(error));
        }
        return handle;
    }

    private static void ValidateOpenedPath(SafeFileHandle handle,
        string expectedPath, bool expectDirectory, string description)
    {
        ByHandleFileInformation information;
        if (!GetFileInformationByHandle(handle, out information))
        {
            int error = Marshal.GetLastWin32Error();
            throw new IOException(
                "Windows could not inspect the " + description + " (error " +
                error.ToString(CultureInfo.InvariantCulture) + ").",
                new Win32Exception(error));
        }

        bool isDirectory =
            (information.FileAttributes & FileAttributeDirectory) != 0;
        bool isReparsePoint =
            (information.FileAttributes & FileAttributeReparsePoint) != 0;
        if (isReparsePoint || isDirectory != expectDirectory)
            throw new InvalidDataException(
                "The " + description +
                " is a reparse point or has the wrong file type.");

        string finalPath = GetCanonicalPath(handle, description);
        string expected = Path.GetFullPath(expectedPath).TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string actual = Path.GetFullPath(finalPath).TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                "The " + description + " resolved outside its expected location.");
    }

    private static string GetCanonicalPath(SafeFileHandle handle,
        string description)
    {
        StringBuilder buffer = new StringBuilder(512);
        while (true)
        {
            uint result = GetFinalPathNameByHandle(handle, buffer,
                (uint)buffer.Capacity, VolumeNameDos);
            if (result == 0)
            {
                int error = Marshal.GetLastWin32Error();
                throw new IOException(
                    "Windows could not resolve the " + description +
                    " (error " + error.ToString(CultureInfo.InvariantCulture) +
                    ").", new Win32Exception(error));
            }
            if (result < (uint)buffer.Capacity)
                return NormalizeExtendedPath(buffer.ToString());
            if (result > 32767)
                throw new InvalidDataException(
                    "The " + description + " path is too long.");
            buffer = new StringBuilder((int)result + 1);
        }
    }

    private static string NormalizeExtendedPath(string path)
    {
        const string uncPrefix = @"\\?\UNC\";
        const string localPrefix = @"\\?\";
        if (path.StartsWith(uncPrefix, StringComparison.OrdinalIgnoreCase))
            return @"\\" + path.Substring(uncPrefix.Length);
        if (path.StartsWith(localPrefix, StringComparison.OrdinalIgnoreCase))
            return path.Substring(localPrefix.Length);
        return path;
    }

    private static void CleanupStaleDownloads()
    {
        string temp = Path.GetTempPath();
        DateTime cutoff = DateTime.UtcNow.Subtract(TimeSpan.FromDays(2));
        try
        {
            foreach (string directory in Directory.GetDirectories(
                temp, TemporaryPrefix + "*", SearchOption.TopDirectoryOnly))
            {
                string name = Path.GetFileName(directory);
                string suffix = name.Substring(TemporaryPrefix.Length);
                Guid parsed;
                if (suffix.Length != 32 ||
                    !Guid.TryParseExact(suffix, "N", out parsed))
                    continue;
                try
                {
                    FileAttributes attributes = File.GetAttributes(directory);
                    if ((attributes & FileAttributes.ReparsePoint) == 0 &&
                        Directory.GetLastWriteTimeUtc(directory) < cutoff)
                        TryDeleteDirectory(directory);
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (IsSafeTemporaryUpdateDirectory(directory))
                Directory.Delete(directory, true);
        }
        catch
        {
        }
    }

    internal static bool IsSafeTemporaryUpdateDirectory(string directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return false;
        try
        {
            string fullDirectory = Path.GetFullPath(directory)
                .TrimEnd(Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
            string fullTemp = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
            DirectoryInfo information = new DirectoryInfo(fullDirectory);
            if (information.Parent == null ||
                !string.Equals(information.Parent.FullName.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar), fullTemp,
                    StringComparison.OrdinalIgnoreCase) ||
                (information.Attributes & FileAttributes.ReparsePoint) != 0)
                return false;
            string name = information.Name;
            if (!name.StartsWith(TemporaryPrefix, StringComparison.Ordinal))
                return false;
            string suffix = name.Substring(TemporaryPrefix.Length);
            Guid parsed;
            return suffix.Length == 32 &&
                Guid.TryParseExact(suffix, "N", out parsed);
        }
        catch
        {
            return false;
        }
    }
}

internal sealed class StrictJsonParser
{
    private readonly string text;
    private int position;
    private int depth;

    private StrictJsonParser(string json)
    {
        if (json == null)
            throw new ArgumentNullException("json");
        text = json;
    }

    public static object Parse(string json)
    {
        StrictJsonParser parser = new StrictJsonParser(json);
        object value = parser.ReadValue();
        parser.SkipWhitespace();
        if (parser.position != parser.text.Length)
            parser.Fail("Unexpected content after the JSON value.");
        return value;
    }

    private object ReadValue()
    {
        SkipWhitespace();
        if (position >= text.Length)
            Fail("Unexpected end of JSON.");
        char character = text[position];
        if (character == '{')
            return ReadObject();
        if (character == '[')
            return ReadArray();
        if (character == '"')
            return ReadString();
        if (character == 't')
        {
            ReadLiteral("true");
            return true;
        }
        if (character == 'f')
        {
            ReadLiteral("false");
            return false;
        }
        if (character == 'n')
        {
            ReadLiteral("null");
            return null;
        }
        if (character == '-' || (character >= '0' && character <= '9'))
            return ReadNumber();
        Fail("Unexpected JSON token.");
        return null;
    }

    private IDictionary<string, object> ReadObject()
    {
        EnterDepth();
        try
        {
            Dictionary<string, object> result =
                new Dictionary<string, object>(StringComparer.Ordinal);
            position++;
            SkipWhitespace();
            if (Consume('}'))
                return result;
            while (true)
            {
                SkipWhitespace();
                if (position >= text.Length || text[position] != '"')
                    Fail("A JSON object key must be a string.");
                string key = ReadString();
                if (result.ContainsKey(key))
                    Fail("A JSON object contains a duplicate key.");
                SkipWhitespace();
                Expect(':');
                result.Add(key, ReadValue());
                SkipWhitespace();
                if (Consume('}'))
                    return result;
                Expect(',');
            }
        }
        finally
        {
            depth--;
        }
    }

    private IList ReadArray()
    {
        EnterDepth();
        try
        {
            List<object> result = new List<object>();
            position++;
            SkipWhitespace();
            if (Consume(']'))
                return result;
            while (true)
            {
                result.Add(ReadValue());
                SkipWhitespace();
                if (Consume(']'))
                    return result;
                Expect(',');
            }
        }
        finally
        {
            depth--;
        }
    }

    private string ReadString()
    {
        Expect('"');
        StringBuilder builder = new StringBuilder();
        while (position < text.Length)
        {
            char character = text[position++];
            if (character == '"')
                return builder.ToString();
            if (character < 0x20)
                Fail("A JSON string contains an unescaped control character.");
            if (character != '\\')
            {
                builder.Append(character);
                continue;
            }
            if (position >= text.Length)
                Fail("A JSON escape is incomplete.");
            char escape = text[position++];
            switch (escape)
            {
                case '"': builder.Append('"'); break;
                case '\\': builder.Append('\\'); break;
                case '/': builder.Append('/'); break;
                case 'b': builder.Append('\b'); break;
                case 'f': builder.Append('\f'); break;
                case 'n': builder.Append('\n'); break;
                case 'r': builder.Append('\r'); break;
                case 't': builder.Append('\t'); break;
                case 'u':
                    if (position + 4 > text.Length)
                        Fail("A JSON Unicode escape is incomplete.");
                    int code = 0;
                    for (int index = 0; index < 4; index++)
                    {
                        int digit = HexValue(text[position++]);
                        if (digit < 0)
                            Fail("A JSON Unicode escape is invalid.");
                        code = (code << 4) | digit;
                    }
                    builder.Append((char)code);
                    break;
                default:
                    Fail("A JSON string escape is invalid.");
                    break;
            }
        }
        Fail("A JSON string is not terminated.");
        return null;
    }

    private object ReadNumber()
    {
        int start = position;
        if (Consume('-') && position >= text.Length)
            Fail("A JSON number is incomplete.");
        if (Consume('0'))
        {
            if (position < text.Length && char.IsDigit(text[position]))
                Fail("A JSON number has a leading zero.");
        }
        else
        {
            ReadDigits(true);
        }
        if (Consume('.'))
            ReadDigits(true);
        if (position < text.Length &&
            (text[position] == 'e' || text[position] == 'E'))
        {
            position++;
            if (position < text.Length &&
                (text[position] == '+' || text[position] == '-'))
                position++;
            ReadDigits(true);
        }
        double number;
        if (!double.TryParse(text.Substring(start, position - start),
                NumberStyles.Float, CultureInfo.InvariantCulture, out number) ||
            double.IsInfinity(number) || double.IsNaN(number))
            Fail("A JSON number is invalid.");
        return number;
    }

    private void ReadDigits(bool requireOne)
    {
        int start = position;
        while (position < text.Length && text[position] >= '0' &&
            text[position] <= '9')
            position++;
        if (requireOne && position == start)
            Fail("A JSON number is incomplete.");
    }

    private void ReadLiteral(string literal)
    {
        if (position + literal.Length > text.Length ||
            !string.Equals(text.Substring(position, literal.Length), literal,
                StringComparison.Ordinal))
            Fail("A JSON literal is invalid.");
        position += literal.Length;
    }

    private void EnterDepth()
    {
        depth++;
        if (depth > 64)
            Fail("The JSON nesting depth is too large.");
    }

    private void SkipWhitespace()
    {
        while (position < text.Length)
        {
            char character = text[position];
            if (character != ' ' && character != '\t' &&
                character != '\r' && character != '\n')
                break;
            position++;
        }
    }

    private bool Consume(char expected)
    {
        if (position >= text.Length || text[position] != expected)
            return false;
        position++;
        return true;
    }

    private void Expect(char expected)
    {
        if (!Consume(expected))
            Fail("Expected '" + expected + "'.");
    }

    private static int HexValue(char character)
    {
        if (character >= '0' && character <= '9')
            return character - '0';
        if (character >= 'a' && character <= 'f')
            return character - 'a' + 10;
        if (character >= 'A' && character <= 'F')
            return character - 'A' + 10;
        return -1;
    }

    private void Fail(string message)
    {
        throw new InvalidDataException(message + " Position " +
            position.ToString(CultureInfo.InvariantCulture) + ".");
    }
}
