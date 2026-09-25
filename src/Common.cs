using System;
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

internal static class SettingsStore
{
    private static readonly Regex DnsLabelPattern = new Regex(
        @"^[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?$",
        RegexOptions.CultureInvariant);

    public static bool IsValidComputerName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string computer = value.Trim();
        if (computer.Length > 255)
            return false;

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

        IPAddress address;
        if (IPAddress.TryParse(computer, out address))
            return true;

        int firstColon = computer.IndexOf(':');
        int lastColon = computer.LastIndexOf(':');
        if (firstColon >= 0 && firstColon == lastColon)
        {
            if (firstColon == 0 || firstColon == computer.Length - 1)
                return false;
            return IsValidHost(computer.Substring(0, firstColon)) &&
                IsValidPort(computer.Substring(firstColon + 1));
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

        bool numericDotsOnly = host.IndexOf('.') >= 0;
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
        int port;
        return int.TryParse(value, out port) && port >= 1 && port <= 65535;
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
        if (result.Length == 0)
            result = "RDP Session Reminder";
        if (result.Length > 80)
            result = result.Substring(0, 80).TrimEnd('.', ' ');
        return result;
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
        settings = null;
        return IsValidProfileId(profileId) &&
            TryLoadFrom(AppPaths.GetProfileSettingsPath(profileId), out settings);
    }

    public static bool TryLoadFrom(string path, out ReminderSettings settings)
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
            return false;

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
