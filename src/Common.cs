using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

internal static class AppPaths
{
    public const string ProductName = "RDP Session Reminder";
    public const string RuntimeFileName = "RdpSessionReminder.exe";
    public const string SetupFileName = "RdpSessionReminderSetup.exe";
    public const string SettingsFileName = "settings.ini";
    public const string ConnectionFileName = "connection.rdp";

    public static string InstallDirectory
    {
        get
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RdpSessionReminder");
        }
    }

    public static string ProfilesDirectory
    {
        get { return Path.Combine(InstallDirectory, "profiles"); }
    }

    public static string RuntimePath
    {
        get { return Path.Combine(InstallDirectory, RuntimeFileName); }
    }

    public static string SetupPath
    {
        get { return Path.Combine(InstallDirectory, SetupFileName); }
    }

    public static string GetProfileDirectory(string profileId)
    {
        return Path.Combine(ProfilesDirectory, profileId);
    }

    public static string GetProfileSettingsPath(string profileId)
    {
        return Path.Combine(GetProfileDirectory(profileId), SettingsFileName);
    }

    public static string GetProfileConnectionPath(string profileId)
    {
        return Path.Combine(GetProfileDirectory(profileId), ConnectionFileName);
    }
}

internal sealed class ReminderSettings
{
    public string ComputerName;
    public bool FullScreen;
    public string ShortcutName;
    public string RdpFile;
    public string DisplayDevice;
    public string ReminderText;
}

internal static class RdpFileTargetReader
{
    public static string TryReadTarget(string path)
    {
        string fullAddress = "";
        string alternateAddress = "";
        try
        {
            foreach (string line in File.ReadAllLines(path))
            {
                const string fullPrefix = "full address:s:";
                const string alternatePrefix = "alternate full address:s:";
                if (line.StartsWith(alternatePrefix,
                    StringComparison.OrdinalIgnoreCase))
                    alternateAddress = line.Substring(alternatePrefix.Length).Trim();
                else if (line.StartsWith(fullPrefix,
                    StringComparison.OrdinalIgnoreCase))
                    fullAddress = line.Substring(fullPrefix.Length).Trim();
            }
        }
        catch
        {
            return "";
        }
        return alternateAddress.Length > 0 ? alternateAddress : fullAddress;
    }
}

internal sealed class RdpProfileOptions
{
    public string ComputerName;
    public bool FullScreen;
    public int DesktopWidth;
    public int DesktopHeight;
    public bool UseAllMonitors;
    public bool AlwaysAskForCredentials;
    public bool RedirectClipboard;
    public bool RedirectDrives;
    public bool RedirectLocation;
    public bool RedirectComPorts;
    public bool RedirectWebAuthn;
    public bool RedirectSmartCards;

    public RdpProfileOptions(string computerName, bool fullScreen,
        int desktopWidth, int desktopHeight, bool useAllMonitors,
        bool alwaysAskForCredentials, bool redirectClipboard,
        bool redirectDrives, bool redirectLocation, bool redirectComPorts,
        bool redirectWebAuthn, bool redirectSmartCards)
    {
        ComputerName = computerName;
        FullScreen = fullScreen;
        DesktopWidth = desktopWidth;
        DesktopHeight = desktopHeight;
        UseAllMonitors = useAllMonitors;
        AlwaysAskForCredentials = alwaysAskForCredentials;
        RedirectClipboard = redirectClipboard;
        RedirectDrives = redirectDrives;
        RedirectLocation = redirectLocation;
        RedirectComPorts = redirectComPorts;
        RedirectWebAuthn = redirectWebAuthn;
        RedirectSmartCards = redirectSmartCards;
    }
}

internal static class RdpProfileWriter
{
    public static void Write(string path, RdpProfileOptions options)
    {
        if (options == null ||
            !SettingsStore.IsValidComputerName(options.ComputerName))
            throw new ArgumentException("A valid Remote Desktop target is required.");
        if (options.DesktopWidth < 200 || options.DesktopWidth > 8192 ||
            options.DesktopHeight < 200 || options.DesktopHeight > 8192)
            throw new ArgumentOutOfRangeException(
                "The Remote Desktop resolution must be between 200 and 8192 pixels.");

        string computer = SettingsStore.NormalizeComputerName(options.ComputerName);
        List<string> lines = new List<string>();
        lines.Add("screen mode id:i:" + (options.FullScreen ? "2" : "1"));
        lines.Add("use multimon:i:" + (options.UseAllMonitors ? "1" : "0"));
        lines.Add("desktopwidth:i:" + options.DesktopWidth.ToString(
            CultureInfo.InvariantCulture));
        lines.Add("desktopheight:i:" + options.DesktopHeight.ToString(
            CultureInfo.InvariantCulture));
        lines.Add("session bpp:i:32");
        lines.Add("compression:i:1");
        lines.Add("keyboardhook:i:2");
        lines.Add("audiocapturemode:i:0");
        lines.Add("videoplaybackmode:i:1");
        lines.Add("connection type:i:7");
        lines.Add("networkautodetect:i:1");
        lines.Add("bandwidthautodetect:i:1");
        lines.Add("displayconnectionbar:i:1");
        lines.Add("enableworkspacereconnect:i:0");
        lines.Add("disable wallpaper:i:0");
        lines.Add("allow font smoothing:i:1");
        lines.Add("allow desktop composition:i:1");
        lines.Add("disable full window drag:i:0");
        lines.Add("disable menu anims:i:0");
        lines.Add("disable themes:i:0");
        lines.Add("disable cursor setting:i:0");
        lines.Add("bitmapcachepersistenable:i:1");
        lines.Add("full address:s:" + computer);
        lines.Add("audiomode:i:0");
        lines.Add("redirectprinters:i:0");
        if (options.RedirectLocation)
            lines.Add("redirectlocation:i:1");
        lines.Add("redirectcomports:i:" + (options.RedirectComPorts ? "1" : "0"));
        lines.Add("redirectsmartcards:i:" +
            (options.RedirectSmartCards ? "1" : "0"));
        lines.Add("redirectwebauthn:i:" +
            (options.RedirectWebAuthn ? "1" : "0"));
        lines.Add("redirectclipboard:i:" +
            (options.RedirectClipboard ? "1" : "0"));
        lines.Add("redirectposdevices:i:0");
        lines.Add("autoreconnection enabled:i:1");
        lines.Add("authentication level:i:2");
        lines.Add("prompt for credentials:i:" +
            (options.AlwaysAskForCredentials ? "1" : "0"));
        lines.Add("negotiate security layer:i:1");
        lines.Add("enablecredsspsupport:i:1");
        lines.Add("remoteapplicationmode:i:0");
        lines.Add("alternate shell:s:");
        lines.Add("shell working directory:s:");
        lines.Add("gatewayhostname:s:");
        lines.Add("gatewayusagemethod:i:4");
        lines.Add("gatewaycredentialssource:i:4");
        lines.Add("gatewayprofileusagemethod:i:0");
        lines.Add("promptcredentialonce:i:0");
        lines.Add("gatewaybrokeringtype:i:0");
        lines.Add("use redirection server name:i:0");
        lines.Add("rdgiskdcproxy:i:0");
        lines.Add("kdcproxyname:s:");
        lines.Add("enablerdsaadauth:i:0");
        lines.Add("drivestoredirect:s:" + (options.RedirectDrives ? "*" : ""));
        lines.Add("remoteappmousemoveinject:i:1");
        lines.Add("alternate full address:s:" + computer);

        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllLines(path, lines.ToArray(), Encoding.Unicode);
    }
}

internal static class SettingsStore
{
    private static readonly Regex DnsLabelPattern = new Regex(
        @"\A[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?\z",
        RegexOptions.CultureInvariant);

    public static bool IsValidComputerName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string computer = value.Trim();
        if (computer.Length > 255)
            return false;
        foreach (char character in computer)
        {
            if (char.IsWhiteSpace(character) || char.IsControl(character))
                return false;
        }

        if (computer.StartsWith("[", StringComparison.Ordinal))
        {
            int closingBracket = computer.IndexOf(']');
            if (closingBracket <= 1)
                return false;

            IPAddress bracketedAddress;
            if (!IPAddress.TryParse(computer.Substring(1, closingBracket - 1),
                    out bracketedAddress) ||
                bracketedAddress.AddressFamily != AddressFamily.InterNetworkV6)
                return false;

            if (closingBracket == computer.Length - 1)
                return true;
            if (computer[closingBracket + 1] != ':' ||
                closingBracket + 2 >= computer.Length)
                return false;
            return IsValidPort(computer.Substring(closingBracket + 2));
        }

        if (IsCanonicalIpv4Address(computer))
            return true;

        IPAddress address;
        if (IPAddress.TryParse(computer, out address))
            return computer.IndexOf(':') >= 0 &&
                address.AddressFamily == AddressFamily.InterNetworkV6;

        int firstColon = computer.IndexOf(':');
        int lastColon = computer.LastIndexOf(':');
        if (firstColon >= 0 && firstColon == lastColon)
        {
            if (firstColon == 0 || firstColon == computer.Length - 1)
                return false;
            string host = computer.Substring(0, firstColon);
            if (!IsCanonicalIpv4Address(host))
            {
                IPAddress parsedHost;
                if (IPAddress.TryParse(host, out parsedHost) ||
                    !IsValidHost(host))
                    return false;
            }
            return IsValidPort(computer.Substring(firstColon + 1));
        }

        if (firstColon >= 0)
            return false;

        return IsValidHost(computer);
    }

    private static bool IsValidHost(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 253)
            return false;

        string host = value.EndsWith(".", StringComparison.Ordinal)
            ? value.Substring(0, value.Length - 1)
            : value;
        if (host.Length == 0)
            return false;

        bool numericDotsOnly = true;
        foreach (char character in host)
        {
            if (character != '.' && (character < '0' || character > '9'))
            {
                numericDotsOnly = false;
                break;
            }
        }
        if (numericDotsOnly)
            return false;

        string[] labels = host.Split('.');
        foreach (string label in labels)
        {
            if (label.Length == 0 || label.Length > 63 ||
                !DnsLabelPattern.IsMatch(label))
                return false;
        }
        return true;
    }

    private static bool IsValidPort(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 5)
            return false;
        foreach (char character in value)
        {
            if (character < '0' || character > '9')
                return false;
        }

        int port;
        return int.TryParse(value, NumberStyles.None,
            CultureInfo.InvariantCulture, out port) && port >= 1 && port <= 65535;
    }

    private static bool IsCanonicalIpv4Address(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        string[] parts = value.Split('.');
        if (parts.Length != 4)
            return false;

        foreach (string part in parts)
        {
            if (part.Length == 0 || part.Length > 3 ||
                (part.Length > 1 && part[0] == '0'))
                return false;

            foreach (char character in part)
            {
                if (character < '0' || character > '9')
                    return false;
            }

            int number;
            if (!int.TryParse(part, NumberStyles.None,
                    CultureInfo.InvariantCulture, out number) || number > 255)
                return false;
        }
        return true;
    }

    public static string NormalizeComputerName(string value)
    {
        return value == null ? "" : value.Trim();
    }

    public static string NormalizeShortcutName(string value, string computer)
    {
        string result = string.IsNullOrWhiteSpace(value)
            ? "Remote - " + computer
            : value.Trim();

        foreach (char invalid in Path.GetInvalidFileNameChars())
            result = result.Replace(invalid, '-');

        result = result.Trim().TrimEnd('.', ' ');
        if (result.Length > 80)
            result = result.Substring(0, 80).TrimEnd('.', ' ');
        if (result.Length == 0)
            result = "RDP Session Reminder";
        if (IsReservedWindowsFileName(result))
            result = "Remote - " + result;
        if (result.Length > 80)
            result = result.Substring(0, 80).TrimEnd('.', ' ');
        return result;
    }

    private static bool IsReservedWindowsFileName(string value)
    {
        string stem = value;
        int dot = stem.IndexOf('.');
        if (dot >= 0)
            stem = stem.Substring(0, dot);
        stem = stem.TrimEnd(' ', '.').ToUpperInvariant();
        if (stem == "CON" || stem == "PRN" || stem == "AUX" ||
            stem == "NUL" || stem == "CONIN$" || stem == "CONOUT$")
            return true;
        if (stem.Length == 4 &&
            (stem.StartsWith("COM", StringComparison.Ordinal) ||
             stem.StartsWith("LPT", StringComparison.Ordinal)) &&
            ((stem[3] >= '1' && stem[3] <= '9') ||
             stem[3] == '\u00B9' || stem[3] == '\u00B2' ||
             stem[3] == '\u00B3'))
            return true;
        return false;
    }

    public static string NormalizeReminderText(string value, string computer)
    {
        string result = SafeSingleLine(value)
            .Replace('\u2013', '-')
            .Replace('\u2014', '-')
            .Replace('\u2212', '-');
        if (result.Length == 0)
            result = "REMOTE SESSION - " + NormalizeComputerName(computer).ToUpperInvariant();
        if (result.Length > 100)
            result = result.Substring(0, 100).Trim();
        return result;
    }

    public static bool IsValidProfileId(string value)
    {
        Guid parsed;
        return !string.IsNullOrEmpty(value) &&
            Guid.TryParseExact(value, "N", out parsed);
    }

    public static bool TryLoadProfile(string profileId, out ReminderSettings settings)
    {
        if (!IsValidProfileId(profileId))
        {
            settings = null;
            return false;
        }
        return TryLoadFrom(AppPaths.GetProfileSettingsPath(profileId),
            AppPaths.GetProfileConnectionPath(profileId), out settings);
    }

    public static bool TryLoadFrom(string path, out ReminderSettings settings)
    {
        return TryLoadFrom(path, null, out settings);
    }

    internal static bool TryLoadFrom(string path, string trustedRdpPath,
        out ReminderSettings settings)
    {
        settings = null;
        if (!File.Exists(path))
            return false;

        string computer = "";
        string shortcut = "";
        string rdpFile = "";
        string displayDevice = "";
        string reminderText = "";
        bool fullScreen = true;

        try
        {
            foreach (string rawLine in File.ReadAllLines(path, Encoding.UTF8))
            {
                int separator = rawLine.IndexOf('=');
                if (separator <= 0)
                    continue;

                string key = rawLine.Substring(0, separator).Trim();
                string value = rawLine.Substring(separator + 1).Trim();
                if (key.Equals("Computer", StringComparison.OrdinalIgnoreCase))
                    computer = value;
                else if (key.Equals("FullScreen", StringComparison.OrdinalIgnoreCase))
                    fullScreen = value != "0";
                else if (key.Equals("ShortcutName", StringComparison.OrdinalIgnoreCase))
                    shortcut = value;
                else if (key.Equals("RdpFile", StringComparison.OrdinalIgnoreCase))
                    rdpFile = value;
                else if (key.Equals("DisplayDevice", StringComparison.OrdinalIgnoreCase))
                    displayDevice = value;
                else if (key.Equals("ReminderText", StringComparison.OrdinalIgnoreCase))
                    reminderText = value;
            }
        }
        catch
        {
            return false;
        }

        computer = NormalizeComputerName(computer);
        if (!IsValidComputerName(computer))
        {
            if (string.IsNullOrEmpty(rdpFile) ||
                string.IsNullOrEmpty(trustedRdpPath))
                return false;

            string migratedTarget = NormalizeComputerName(
                RdpFileTargetReader.TryReadTarget(trustedRdpPath));
            if (!IsValidComputerName(migratedTarget))
                return false;

            string oldDefault = NormalizeReminderText("", computer);
            if (string.Equals(NormalizeReminderText(reminderText, computer),
                    oldDefault, StringComparison.Ordinal))
                reminderText = "";
            computer = migratedTarget;
        }

        settings = new ReminderSettings();
        settings.ComputerName = computer;
        settings.FullScreen = fullScreen;
        settings.ShortcutName = NormalizeShortcutName(shortcut, computer);
        settings.RdpFile = rdpFile;
        settings.DisplayDevice = displayDevice;
        settings.ReminderText = NormalizeReminderText(reminderText, computer);
        return true;
    }

    public static void SaveTo(string path, ReminderSettings settings)
    {
        if (settings == null || !IsValidComputerName(settings.ComputerName))
            throw new ArgumentException("A valid computer name is required.");

        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        string computer = NormalizeComputerName(settings.ComputerName);
        string shortcut = NormalizeShortcutName(settings.ShortcutName, computer);
        string content =
            "Computer=" + computer + Environment.NewLine +
            "FullScreen=" + (settings.FullScreen ? "1" : "0") + Environment.NewLine +
            "ShortcutName=" + shortcut + Environment.NewLine +
            "RdpFile=" + SafeSingleLine(settings.RdpFile) + Environment.NewLine +
            "DisplayDevice=" + SafeSingleLine(settings.DisplayDevice) + Environment.NewLine +
            "ReminderText=" + NormalizeReminderText(
                settings.ReminderText, computer) + Environment.NewLine;

        string temporary = path + ".new";
        File.WriteAllText(temporary, content, new UTF8Encoding(false));
        if (File.Exists(path))
            File.Replace(temporary, path, null);
        else
            File.Move(temporary, path);
    }

    private static string SafeSingleLine(string value)
    {
        return string.IsNullOrEmpty(value)
            ? ""
            : value.Replace("\r", "").Replace("\n", "").Trim();
    }
}
