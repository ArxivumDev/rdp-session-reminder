using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("RDP Session Reminder Uninstaller")]
[assembly: AssemblyDescription("Per-user uninstaller for RDP Session Reminder.")]
[assembly: AssemblyCompany("RDP Session Reminder contributors")]
[assembly: AssemblyProduct("RDP Session Reminder")]
[assembly: AssemblyCopyright("Copyright (c) 2026")]
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]

internal static class UninstallerProgram
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (HasArgument(args, "--self-test"))
            return UninstallerEngine.RunSelfTest();

        UninstallLayout layout = UninstallLayout.CreateForCurrentUser();
        string currentPath = Path.GetFullPath(Application.ExecutablePath);
        bool temporaryCopy = HasArgument(args, "--temporary-uninstaller");

        try
        {
            UninstallerEngine.ValidateUninstallRoots(layout);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                "The uninstaller could not start safely.\r\n\r\n" +
                exception.Message,
                UninstallProductInfo.ProductName, MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }

        if (!temporaryCopy && PathsEqual(currentPath, layout.UninstallerPath))
        {
            try
            {
                StartTemporaryCopy(currentPath);
                return 0;
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    "The uninstaller could not start safely.\r\n\r\n" +
                    exception.Message,
                    UninstallProductInfo.ProductName, MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return 1;
            }
        }

        if (temporaryCopy)
        {
            try
            {
                WaitForParentProcess(args);
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    "The uninstaller could not continue safely.\r\n\r\n" +
                    exception.Message,
                    UninstallProductInfo.ProductName, MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                ScheduleTemporaryCopyRemoval(currentPath);
                return 1;
            }
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        int exitCode = RunInteractive(layout);
        if (temporaryCopy)
            ScheduleTemporaryCopyRemoval(currentPath);
        return exitCode;
    }

    private static int RunInteractive(UninstallLayout layout)
    {
        bool removeSavedConnections;
        using (UninstallOptionsForm form = new UninstallOptionsForm(layout))
        {
            if (form.ShowDialog() != DialogResult.OK)
                return 0;
            removeSavedConnections = form.RemoveSavedConnections;
        }

        if (removeSavedConnections)
        {
            DialogResult confirmation = MessageBox.Show(
                "Remove app-managed saved connections too?\r\n\r\n" +
                "This deletes connection.rdp and settings.ini copies under the " +
                "app profiles folder, app-generated shortcut icons, abandoned " +
                "one-time profiles, and verified reminder shortcuts currently " +
                "in the Desktop folder.\r\n\r\n" +
                "Original imported .rdp files and unrelated shortcuts are not " +
                "deleted. Copies moved outside the Desktop folder are not searched " +
                "for, and the app will not search other folders or drives.",
                UninstallProductInfo.ProductName, MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (confirmation != DialogResult.Yes)
                return 0;
        }

        try
        {
            UninstallerEngine.Uninstall(
                layout, true, removeSavedConnections);
            string message = removeSavedConnections
                ? "RDP Session Reminder and its app-managed saved connection " +
                    "copies, generated icons, and abandoned one-time profiles " +
                    "were removed.\r\n\r\nOriginal imported .rdp files " +
                    "were never touched. Verified reminder shortcuts currently " +
                    "in the Desktop folder were removed. Copies moved outside " +
                    "the Desktop folder were not searched for and may remain."
                : "RDP Session Reminder was uninstalled.\r\n\r\n" +
                    "Your generated and imported profile copies remain in:\r\n" +
                    layout.ProfilesDirectory + "\r\n\r\n" +
                    "Generated shortcut icons and any abandoned one-time " +
                    "profiles also remain under the application folder.\r\n\r\n" +
                    "Original imported .rdp files were never touched. Desktop, " +
                    "moved, and copied reminder shortcuts remain too. Those " +
                    "shortcuts need the app to be reinstalled before they work " +
                    "again. A preserved connection.rdp file opens directly with " +
                    "Remote Desktop.\r\n\r\nCleanup instructions were saved here:\r\n" +
                    layout.SavedConnectionsNotePath;
            MessageBox.Show(message, UninstallProductInfo.ProductName,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                "Uninstall stopped before all application files were removed.\r\n\r\n" +
                exception.Message + "\r\n\r\n" +
                "Some items may already have been removed. Unrelated shortcuts " +
                "and original imported .rdp files were not changed.",
                UninstallProductInfo.ProductName, MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }
    }

    private static void StartTemporaryCopy(string currentPath)
    {
        string temporaryDirectory = Path.Combine(Path.GetTempPath(),
            "RdpSessionReminder-Uninstall-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        string temporaryPath = Path.Combine(
            temporaryDirectory, UninstallProductInfo.UninstallerFileName);
        File.Copy(currentPath, temporaryPath, false);

        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = temporaryPath;
        startInfo.Arguments = "--temporary-uninstaller --wait-for-process " +
            Process.GetCurrentProcess().Id.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
        startInfo.UseShellExecute = true;
        Process.Start(startInfo);
    }

    private static void WaitForParentProcess(string[] args)
    {
        int processId = 0;
        for (int index = 0; index + 1 < args.Length; index++)
        {
            if (!string.Equals(args[index], "--wait-for-process",
                    StringComparison.OrdinalIgnoreCase))
                continue;
            int.TryParse(args[index + 1],
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out processId);
            break;
        }
        if (processId <= 0 || processId == Process.GetCurrentProcess().Id)
            throw new InvalidDataException(
                "The temporary uninstaller did not receive a valid parent process.");

        try
        {
            using (Process parent = Process.GetProcessById(processId))
            {
                if (!parent.WaitForExit(30000))
                    throw new TimeoutException(
                        "The installed uninstaller did not finish in time.");
            }
        }
        catch (ArgumentException)
        {
            // The parent has already exited.
        }
    }

    private static void ScheduleTemporaryCopyRemoval(string currentPath)
    {
        try
        {
            string currentDirectory = Path.GetDirectoryName(currentPath);
            string script =
                "$p='" + EscapePowerShellLiteral(currentPath) + "';" +
                "$d='" + EscapePowerShellLiteral(currentDirectory) + "';" +
                "for($i=0;$i -lt 20;$i++){" +
                "Start-Sleep -Milliseconds 250;" +
                "try{Remove-Item -LiteralPath $p -Force -ErrorAction Stop;break}catch{}};" +
                "try{Remove-Item -LiteralPath $d -Force -ErrorAction SilentlyContinue}catch{}";
            string encoded = Convert.ToBase64String(
                Encoding.Unicode.GetBytes(script));
            string powerShell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                @"WindowsPowerShell\v1.0\powershell.exe");
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = powerShell;
            startInfo.Arguments = "-NoLogo -NoProfile -NonInteractive " +
                "-WindowStyle Hidden -EncodedCommand " + encoded;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            Process.Start(startInfo);
        }
        catch
        {
            // The installed application has already been removed. A temporary
            // uninstaller copy can be deleted from the user's temp folder later.
        }
    }

    private static string EscapePowerShellLiteral(string value)
    {
        return value.Replace("'", "''");
    }

    private static bool HasArgument(string[] args, string expected)
    {
        foreach (string argument in args)
        {
            if (string.Equals(argument, expected,
                    StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool PathsEqual(string first, string second)
    {
        return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second),
            StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class UninstallOptionsForm : Form
{
    private readonly CheckBox removeSavedConnectionsCheck;

    public bool RemoveSavedConnections
    {
        get { return removeSavedConnectionsCheck.Checked; }
    }

    public UninstallOptionsForm(UninstallLayout layout)
    {
        Text = "Uninstall RDP Session Reminder";
        Width = 590;
        Height = 440;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScroll = true;

        Label explanation = new Label();
        explanation.Left = 20;
        explanation.Top = 18;
        explanation.Width = 540;
        explanation.Height = 230;
        explanation.Text =
            "Uninstall the app binaries, Start menu entries, setup preferences, " +
            "and app-owned publisher trust.\r\n\r\n" +
            "The app takes extra care to remove and verify only the exact app-owned " +
            "certificate, its private key, and its SHA-256 trust entry. Unrelated " +
            "certificates, keys, and publisher entries remain. If verification " +
            "fails, uninstall stops before deleting app files so you can retry.\r\n\r\n" +
            "By default, generated and imported profile copies are preserved in:\r\n" +
            layout.ProfilesDirectory + "\r\n\r\n" +
            "A preserved connection.rdp file opens directly with Remote Desktop. " +
            "Reminder .lnk shortcuts need the app to be reinstalled before they " +
            "work again. Original imported .rdp files are never changed or deleted.";
        Controls.Add(explanation);

        removeSavedConnectionsCheck = new CheckBox();
        removeSavedConnectionsCheck.Left = 20;
        removeSavedConnectionsCheck.Top = 260;
        removeSavedConnectionsCheck.Width = 540;
        removeSavedConnectionsCheck.Height = 54;
        removeSavedConnectionsCheck.Text =
            "Also remove my app-managed saved connections, generated icons, " +
            "abandoned one-time profiles, and verified reminder shortcuts " +
            "currently in the Desktop folder";
        removeSavedConnectionsCheck.Checked = false;
        Controls.Add(removeSavedConnectionsCheck);

        Label scope = new Label();
        scope.Left = 40;
        scope.Top = 318;
        scope.Width = 520;
        scope.Height = 36;
        scope.Text =
            "Copies moved outside the Desktop folder are not searched for and " +
            "may remain after removal.";
        Controls.Add(scope);

        Button uninstallButton = new Button();
        uninstallButton.Left = 360;
        uninstallButton.Top = 360;
        uninstallButton.Width = 95;
        uninstallButton.Height = 30;
        uninstallButton.Text = "Uninstall";
        uninstallButton.DialogResult = DialogResult.OK;
        Controls.Add(uninstallButton);

        Button cancelButton = new Button();
        cancelButton.Left = 465;
        cancelButton.Top = 360;
        cancelButton.Width = 95;
        cancelButton.Height = 30;
        cancelButton.Text = "Cancel";
        cancelButton.DialogResult = DialogResult.Cancel;
        Controls.Add(cancelButton);

        AcceptButton = uninstallButton;
        CancelButton = cancelButton;
        Shown += FitToCurrentWorkingArea;
    }

    private void FitToCurrentWorkingArea(object sender, EventArgs eventArgs)
    {
        Rectangle workingArea = Screen.FromControl(this).WorkingArea;
        Size fittedSize = LimitWindowSizeToWorkingArea(Size, workingArea);
        if (Size != fittedSize)
            Size = fittedSize;

        int maximumLeft = Math.Max(workingArea.Left,
            workingArea.Right - Width);
        int maximumTop = Math.Max(workingArea.Top,
            workingArea.Bottom - Height);
        Location = new Point(
            Math.Max(workingArea.Left, Math.Min(Left, maximumLeft)),
            Math.Max(workingArea.Top, Math.Min(Top, maximumTop)));
    }

    internal static Size LimitWindowSizeToWorkingArea(Size renderedSize,
        Rectangle workingArea)
    {
        int width = workingArea.Width > 0
            ? Math.Min(renderedSize.Width, workingArea.Width)
            : renderedSize.Width;
        int height = workingArea.Height > 0
            ? Math.Min(renderedSize.Height, workingArea.Height)
            : renderedSize.Height;
        return new Size(Math.Max(1, width), Math.Max(1, height));
    }
}

internal static class UninstallProductInfo
{
    public const string ProductName = "RDP Session Reminder";
    public const string RuntimeFileName = "RdpSessionReminder.exe";
    public const string SetupFileName = "RdpSessionReminderSetup.exe";
    public const string UninstallerFileName =
        "RdpSessionReminder-Uninstaller.exe";
    public const string SetupShortcutName =
        "RDP Session Reminder Setup.lnk";
    public const string UninstallShortcutName =
        "Uninstall RDP Session Reminder.lnk";
    public const string UninstallRegistryPath =
        @"Software\Microsoft\Windows\CurrentVersion\Uninstall\RdpSessionReminder";
}

internal sealed class UninstallLayout
{
    public readonly string InstallDirectory;
    public readonly string StartMenuDirectory;
    private readonly string desktopDirectory;

    public UninstallLayout(string installDirectory, string startMenuDirectory)
        : this(installDirectory, startMenuDirectory,
            Environment.GetFolderPath(
                Environment.SpecialFolder.DesktopDirectory))
    {
    }

    internal UninstallLayout(string installDirectory, string startMenuDirectory,
        string desktopDirectoryValue)
    {
        InstallDirectory = Path.GetFullPath(installDirectory);
        StartMenuDirectory = Path.GetFullPath(startMenuDirectory);
        desktopDirectory = Path.GetFullPath(desktopDirectoryValue);
    }

    public string ProfilesDirectory
    {
        get { return Path.Combine(InstallDirectory, "profiles"); }
    }

    public string OneTimeProfilesDirectory
    {
        get { return Path.Combine(InstallDirectory, "one-time"); }
    }

    public string IconsDirectory
    {
        get { return Path.Combine(InstallDirectory, "icons"); }
    }

    public string DesktopDirectory
    {
        get { return desktopDirectory; }
    }

    public string RuntimePath
    {
        get
        {
            return Path.Combine(InstallDirectory,
                UninstallProductInfo.RuntimeFileName);
        }
    }

    public string SetupPath
    {
        get
        {
            return Path.Combine(InstallDirectory,
                UninstallProductInfo.SetupFileName);
        }
    }

    public string UninstallerPath
    {
        get
        {
            return Path.Combine(InstallDirectory,
                UninstallProductInfo.UninstallerFileName);
        }
    }

    public string PublisherIdentityPath
    {
        get { return Path.Combine(InstallDirectory, "publisher-trust.ini"); }
    }

    public string UpdateSettingsPath
    {
        get { return Path.Combine(InstallDirectory, "update-settings.ini"); }
    }

    public string UiSettingsPath
    {
        get { return Path.Combine(InstallDirectory, "ui-settings.ini"); }
    }

    public string SavedConnectionsNotePath
    {
        get
        {
            return Path.Combine(InstallDirectory,
                "README - Saved RDP Connections.txt");
        }
    }

    public string SetupShortcutPath
    {
        get
        {
            return Path.Combine(StartMenuDirectory,
                UninstallProductInfo.SetupShortcutName);
        }
    }

    public string UninstallShortcutPath
    {
        get
        {
            return Path.Combine(StartMenuDirectory,
                UninstallProductInfo.UninstallShortcutName);
        }
    }

    public static UninstallLayout CreateForCurrentUser()
    {
        string installDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RdpSessionReminder");
        string startMenuDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            "RDP Session Reminder");
        return new UninstallLayout(installDirectory, startMenuDirectory);
    }
}

internal static class UninstallerEngine
{
    private static readonly string[] GeneratedIconFileNames = new string[]
    {
        "reminder-v1.ico",
        "work-v1.ico",
        "production-v1.ico",
        "test-v1.ico",
        "personal-v1.ico"
    };

    internal static void ValidateUninstallRoots(UninstallLayout layout)
    {
        if (layout == null)
            throw new ArgumentNullException("layout");

        ValidateDirectoryRoot(layout.InstallDirectory,
            "The application folder");
        ValidateDirectoryRoot(layout.StartMenuDirectory,
            "The Start menu folder");
    }

    public static void Uninstall(UninstallLayout layout, bool removeRegistration,
        bool removeSavedConnections)
    {
        if (layout == null)
            throw new ArgumentNullException("layout");

        // Perform all root checks before publisher cleanup can launch a process
        // and before any file, shortcut, registry, or profile mutation occurs.
        ValidateUninstallRoots(layout);
        RemovePublisherTrust(layout);
        DeleteFileIfPresent(layout.UpdateSettingsPath);
        DeleteFileIfPresent(layout.UiSettingsPath);
        DeleteFileIfPresent(layout.RuntimePath);
        DeleteFileIfPresent(layout.SetupPath);
        DeleteFileIfPresent(layout.UninstallerPath);
        DeleteFileIfPresent(layout.SetupShortcutPath);
        DeleteFileIfPresent(layout.UninstallShortcutPath);

        TryDeleteEmptyManagedDirectory(layout.StartMenuDirectory);

        if (removeRegistration)
        {
            using (RegistryKey parent = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall", true))
            {
                if (parent != null)
                    parent.DeleteSubKeyTree("RdpSessionReminder", false);
            }
        }

        if (removeSavedConnections)
        {
            RemoveSavedConnections(layout);
            DeleteFileIfPresent(layout.SavedConnectionsNotePath);
            TryDeleteEmptyManagedDirectory(layout.ProfilesDirectory);
            TryDeleteEmptyManagedDirectory(layout.InstallDirectory);
        }
        else
        {
            WriteSavedConnectionsNote(layout);
        }
    }

    private static void RemoveSavedConnections(UninstallLayout layout)
    {
        string[] profileDirectories = IsRegularDirectory(
                layout.ProfilesDirectory)
            ? Directory.GetDirectories(layout.ProfilesDirectory, "*",
                SearchOption.TopDirectoryOnly)
            : new string[0];
        string[] desktopShortcuts = Directory.Exists(layout.DesktopDirectory)
            ? Directory.GetFiles(layout.DesktopDirectory, "*.lnk",
                SearchOption.TopDirectoryOnly)
            : new string[0];

        foreach (string shortcutPath in desktopShortcuts)
        {
            if (IsOwnedDesktopShortcut(shortcutPath, layout))
                File.Delete(shortcutPath);
        }

        foreach (string profileDirectory in profileDirectories)
        {
            string profileId = Path.GetFileName(profileDirectory);
            if (!IsCanonicalProfileId(profileId) ||
                !IsRegularDirectory(profileDirectory) ||
                !IsDirectChild(layout.ProfilesDirectory, profileDirectory))
                continue;

            DeleteOwnedRegularFileIfPresent(
                Path.Combine(profileDirectory, "settings.ini"));
            DeleteOwnedRegularFileIfPresent(
                Path.Combine(profileDirectory, "connection.rdp"));
            TryDeleteEmptyManagedDirectory(profileDirectory);
        }

        RemoveGeneratedIcons(layout);
        RemoveAbandonedOneTimeProfiles(layout);
    }

    private static void RemoveGeneratedIcons(UninstallLayout layout)
    {
        if (!IsRegularDirectory(layout.IconsDirectory))
            return;

        foreach (string fileName in GeneratedIconFileNames)
        {
            string iconPath = Path.GetFullPath(Path.Combine(
                layout.IconsDirectory, fileName));
            if (IsDirectFile(layout.IconsDirectory, iconPath))
                DeleteOwnedRegularFileIfPresent(iconPath);
        }
        TryDeleteEmptyManagedDirectory(layout.IconsDirectory);
    }

    private static void RemoveAbandonedOneTimeProfiles(
        UninstallLayout layout)
    {
        if (!IsRegularDirectory(layout.OneTimeProfilesDirectory))
            return;

        string[] profileDirectories = Directory.GetDirectories(
            layout.OneTimeProfilesDirectory, "*", SearchOption.TopDirectoryOnly);
        foreach (string profileDirectory in profileDirectories)
        {
            string profileId = Path.GetFileName(profileDirectory);
            if (!IsCanonicalProfileId(profileId) ||
                !IsRegularDirectory(profileDirectory) ||
                !IsDirectChild(layout.OneTimeProfilesDirectory,
                    profileDirectory))
                continue;

            DeleteOwnedRegularFileIfPresent(
                Path.Combine(profileDirectory, "settings.ini"));
            DeleteOwnedRegularFileIfPresent(
                Path.Combine(profileDirectory, "connection.rdp"));
            TryDeleteEmptyManagedDirectory(profileDirectory);
        }
        TryDeleteEmptyManagedDirectory(layout.OneTimeProfilesDirectory);
    }

    private static bool IsOwnedDesktopShortcut(string shortcutPath,
        UninstallLayout layout)
    {
        if (!IsRegularFile(shortcutPath) ||
            !IsRegularDirectory(layout.ProfilesDirectory))
            return false;

        string target;
        string arguments;
        if (!TryReadShortcut(shortcutPath, out target, out arguments) ||
            !PathsEqual(target, layout.RuntimePath))
            return false;

        const string prefix = "--profile ";
        string trimmed = arguments == null ? "" : arguments.Trim();
        if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;
        string profileId = trimmed.Substring(prefix.Length);
        if (!IsCanonicalProfileId(profileId) ||
            !string.Equals(trimmed, prefix + profileId,
                StringComparison.OrdinalIgnoreCase))
            return false;

        string profileDirectory = Path.Combine(
            layout.ProfilesDirectory, profileId);
        return IsDirectChild(layout.ProfilesDirectory, profileDirectory) &&
            IsRegularDirectory(profileDirectory);
    }

    private static bool TryReadShortcut(string path, out string target,
        out string arguments)
    {
        target = "";
        arguments = "";
        object shell = null;
        object shortcut = null;
        try
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
                return false;
            shell = Activator.CreateInstance(shellType);
            shortcut = shellType.InvokeMember("CreateShortcut",
                BindingFlags.InvokeMethod, null, shell, new object[] { path });
            Type shortcutType = shortcut.GetType();
            target = shortcutType.InvokeMember("TargetPath",
                BindingFlags.GetProperty, null, shortcut, null) as string ?? "";
            arguments = shortcutType.InvokeMember("Arguments",
                BindingFlags.GetProperty, null, shortcut, null) as string ?? "";
            return target.Length > 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            ReleaseComObject(shortcut);
            ReleaseComObject(shell);
        }
    }

    private static void ReleaseComObject(object value)
    {
        if (value != null &&
            System.Runtime.InteropServices.Marshal.IsComObject(value))
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(value);
    }

    private static bool IsCanonicalProfileId(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length != 32)
            return false;
        foreach (char character in value)
        {
            if (!((character >= '0' && character <= '9') ||
                  (character >= 'a' && character <= 'f') ||
                  (character >= 'A' && character <= 'F')))
                return false;
        }
        Guid parsed;
        return Guid.TryParseExact(value, "N", out parsed);
    }

    private static bool IsDirectChild(string parent, string child)
    {
        string actualParent = Path.GetDirectoryName(Path.GetFullPath(child));
        return PathsEqual(actualParent, Path.GetFullPath(parent));
    }

    private static bool IsDirectFile(string parent, string file)
    {
        string actualParent = Path.GetDirectoryName(Path.GetFullPath(file));
        return PathsEqual(actualParent, Path.GetFullPath(parent));
    }

    private static bool PathsEqual(string first, string second)
    {
        if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second))
            return false;
        return string.Equals(Path.GetFullPath(first).TrimEnd('\\', '/'),
            Path.GetFullPath(second).TrimEnd('\\', '/'),
            StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateDirectoryRoot(string path, string description)
    {
        FileAttributes attributes;
        if (!TryGetAttributes(path, out attributes))
            return;
        if ((attributes & FileAttributes.Directory) == 0)
            throw new InvalidOperationException(
                description + " is not a directory. No uninstall changes were made.");
        if ((attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException(
                description + " is a link or junction. No uninstall changes were made.");
    }

    private static bool TryGetAttributes(string path,
        out FileAttributes attributes)
    {
        try
        {
            attributes = File.GetAttributes(path);
            return true;
        }
        catch (FileNotFoundException)
        {
            attributes = 0;
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            attributes = 0;
            return false;
        }
    }

    private static bool IsRegularDirectory(string path)
    {
        FileAttributes attributes;
        return TryGetAttributes(path, out attributes) &&
            (attributes & FileAttributes.Directory) != 0 &&
            (attributes & FileAttributes.ReparsePoint) == 0;
    }

    private static bool IsRegularFile(string path)
    {
        FileAttributes attributes;
        return TryGetAttributes(path, out attributes) &&
            (attributes & (FileAttributes.Directory |
                FileAttributes.ReparsePoint)) == 0;
    }

    private static void DeleteOwnedRegularFileIfPresent(string path)
    {
        if (IsRegularFile(path))
            File.Delete(path);
    }

    private static void TryDeleteEmptyManagedDirectory(string path)
    {
        if (!IsRegularDirectory(path))
            return;
        try
        {
            Directory.Delete(path, false);
        }
        catch (IOException)
        {
            // Keep a directory that contains anything not explicitly removed.
        }
        catch (UnauthorizedAccessException)
        {
            // Keep a directory when Windows does not permit safe removal.
        }
    }

    private static void RemovePublisherTrust(UninstallLayout layout)
    {
        if (!File.Exists(layout.SetupPath))
        {
            if (File.Exists(layout.PublisherIdentityPath))
                throw new InvalidOperationException(
                    "The setup executable is missing, so app-owned publisher trust " +
                    "cannot be identified and removed safely. Reinstall the app, " +
                    "then run the uninstaller again.");
            return;
        }

        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = layout.SetupPath;
        startInfo.Arguments = "--remove-publisher-trust";
        startInfo.WorkingDirectory = layout.InstallDirectory;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;

        using (Process process = Process.Start(startInfo))
        {
            if (process == null)
                throw new InvalidOperationException(
                    "Windows could not start publisher trust cleanup.");
            if (!process.WaitForExit(120000))
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                }
                throw new TimeoutException(
                    "Publisher trust cleanup did not finish in time.");
            }
            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    "App-owned publisher trust could not be removed safely " +
                    "(setup exit code " + process.ExitCode.ToString() + "). " +
                    "Application files were left in place.");
        }
    }

    private static void DeleteFileIfPresent(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private static void WriteSavedConnectionsNote(UninstallLayout layout)
    {
        Directory.CreateDirectory(layout.InstallDirectory);
        string temporary = layout.SavedConnectionsNotePath + ".new-" +
            Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporary, GetSavedConnectionsNote(layout),
                new UTF8Encoding(false));
            if (File.Exists(layout.SavedConnectionsNotePath))
                File.Replace(temporary, layout.SavedConnectionsNotePath,
                    null, true);
            else
                File.Move(temporary, layout.SavedConnectionsNotePath);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    internal static string GetSavedConnectionsNote(UninstallLayout layout)
    {
        return
            "RDP Session Reminder was uninstalled." + Environment.NewLine +
            Environment.NewLine +
            "The uninstaller deliberately did not delete:" +
            Environment.NewLine +
            "- Generated and imported profile copies under: " +
            layout.ProfilesDirectory + Environment.NewLine +
            "- Every connection.rdp and settings.ini file in those profiles" +
            Environment.NewLine +
            "- Generated shortcut icons and any abandoned one-time profile " +
            "copies under the application folder" + Environment.NewLine +
            "- Desktop reminder shortcuts; copies moved outside the Desktop folder " +
            "were not searched for" +
            Environment.NewLine + Environment.NewLine +
            "Original imported .rdp files were never changed or deleted." +
            Environment.NewLine +
            "The app deliberately does not search other folders or drives for " +
            ".rdp or .lnk copies moved outside the Desktop folder." +
            Environment.NewLine +
            "Reminder .lnk shortcuts require RDP Session Reminder to be " +
            "reinstalled before they work again." + Environment.NewLine +
            "A preserved connection.rdp file can still be opened directly " +
            "with Windows Remote Desktop." + Environment.NewLine +
            Environment.NewLine +
            "After saving anything you want to keep, you can manually delete " +
            "the entire folder below, plus any desktop or moved copies:" +
            Environment.NewLine +
            layout.InstallDirectory + Environment.NewLine;
    }

    public static int RunSelfTest()
    {
        string root = Path.Combine(Path.GetTempPath(),
            "RdpSessionReminderUninstaller-" + Guid.NewGuid().ToString("N"));
        try
        {
            string installDirectory = Path.Combine(root, "app");
            string startMenuDirectory = Path.Combine(root, "start-menu");
            string desktopDirectory = Path.Combine(root, "desktop");
            string movedDirectory = Path.Combine(root, "moved-shortcuts");
            string copiedDirectory = Path.Combine(root, "copied-shortcuts");
            string externalDirectory = Path.Combine(root, "original-files");
            string generatedProfileDirectory = Path.Combine(
                installDirectory, "profiles", "generated-profile");
            string importedProfileDirectory = Path.Combine(
                installDirectory, "profiles", "imported-profile");
            Directory.CreateDirectory(generatedProfileDirectory);
            Directory.CreateDirectory(importedProfileDirectory);
            Directory.CreateDirectory(startMenuDirectory);
            Directory.CreateDirectory(desktopDirectory);
            Directory.CreateDirectory(movedDirectory);
            Directory.CreateDirectory(copiedDirectory);
            Directory.CreateDirectory(externalDirectory);

            UninstallLayout layout = new UninstallLayout(
                installDirectory, startMenuDirectory, desktopDirectory);
            File.WriteAllBytes(layout.RuntimePath,
                Encoding.ASCII.GetBytes("runtime"));
            // Deliberately omit setup so the self-test never launches a process.
            File.WriteAllBytes(layout.UninstallerPath,
                Encoding.ASCII.GetBytes("uninstaller"));
            File.WriteAllText(layout.UpdateSettingsPath,
                "AutomaticChecks=1\nLastCheckUtc=fixture\n");
            File.WriteAllText(layout.UiSettingsPath, "Theme=Light\n");
            File.WriteAllBytes(layout.SetupShortcutPath,
                Encoding.ASCII.GetBytes("setup shortcut"));
            File.WriteAllBytes(layout.UninstallShortcutPath,
                Encoding.ASCII.GetBytes("uninstall shortcut"));
            string otherStartMenuFile = Path.Combine(
                startMenuDirectory, "Keep.lnk");
            byte[] otherStartMenuBytes = Encoding.UTF8.GetBytes("keep-menu");
            File.WriteAllBytes(otherStartMenuFile, otherStartMenuBytes);

            string generatedSettingsPath = Path.Combine(
                generatedProfileDirectory, "settings.ini");
            string generatedRdpPath = Path.Combine(
                generatedProfileDirectory, "connection.rdp");
            string importedSettingsPath = Path.Combine(
                importedProfileDirectory, "settings.ini");
            string importedRdpCopyPath = Path.Combine(
                importedProfileDirectory, "connection.rdp");
            string originalImportedRdpPath = Path.Combine(
                externalDirectory, "original-imported.rdp");
            string retainedIconDirectory = layout.IconsDirectory;
            string retainedIconPath = Path.Combine(
                retainedIconDirectory, "reminder-v1.ico");
            const string retainedOneTimeId =
                "55555555555555555555555555555555";
            string retainedOneTimeDirectory = Path.Combine(
                layout.OneTimeProfilesDirectory, retainedOneTimeId);
            string retainedOneTimeSettings = Path.Combine(
                retainedOneTimeDirectory, "settings.ini");
            string retainedOneTimeRdp = Path.Combine(
                retainedOneTimeDirectory, "connection.rdp");
            string otherAppFile = Path.Combine(installDirectory, "keep.txt");
            string desktopShortcut = Path.Combine(
                desktopDirectory, "Remote generated.lnk");
            string movedShortcut = Path.Combine(
                movedDirectory, "Remote moved.lnk");
            string copiedShortcut = Path.Combine(
                copiedDirectory, "Remote copied.lnk");

            byte[] generatedSettingsBytes = Encoding.UTF8.GetBytes(
                "Computer=generated-host\nMarker=generated-settings\n");
            byte[] generatedRdpBytes = Encoding.Unicode.GetBytes(
                "full address:s:generated-host\r\n");
            byte[] importedSettingsBytes = Encoding.UTF8.GetBytes(
                "Computer=imported-host\nMarker=imported-settings\n");
            byte[] importedRdpBytes = Encoding.Unicode.GetBytes(
                "full address:s:imported-copy\r\nmarker:s:profile-copy\r\n");
            byte[] originalImportedRdpBytes = Encoding.Unicode.GetBytes(
                "full address:s:original-import\r\nmarker:s:external-original\r\n");
            byte[] retainedIconBytes = new byte[] { 0, 1, 0, 0, 9, 8, 7 };
            byte[] retainedOneTimeSettingsBytes = Encoding.UTF8.GetBytes(
                "retained-one-time-settings");
            byte[] retainedOneTimeRdpBytes = new byte[] {
                0, 255, 13, 10, 128, 42, 7, 0
            };
            byte[] otherAppBytes = new byte[] { 0, 1, 2, 127, 128, 254, 255 };
            byte[] desktopShortcutBytes = Encoding.UTF8.GetBytes(
                "desktop-shortcut-fixture");
            byte[] movedShortcutBytes = Encoding.UTF8.GetBytes(
                "moved-shortcut-fixture");
            byte[] copiedShortcutBytes = Encoding.UTF8.GetBytes(
                "copied-shortcut-fixture");

            File.WriteAllBytes(generatedSettingsPath, generatedSettingsBytes);
            File.WriteAllBytes(generatedRdpPath, generatedRdpBytes);
            File.WriteAllBytes(importedSettingsPath, importedSettingsBytes);
            File.WriteAllBytes(importedRdpCopyPath, importedRdpBytes);
            File.WriteAllBytes(originalImportedRdpPath,
                originalImportedRdpBytes);
            Directory.CreateDirectory(retainedIconDirectory);
            Directory.CreateDirectory(retainedOneTimeDirectory);
            File.WriteAllBytes(retainedIconPath, retainedIconBytes);
            File.WriteAllBytes(retainedOneTimeSettings,
                retainedOneTimeSettingsBytes);
            File.WriteAllBytes(retainedOneTimeRdp,
                retainedOneTimeRdpBytes);
            File.WriteAllBytes(otherAppFile, otherAppBytes);
            File.WriteAllBytes(desktopShortcut, desktopShortcutBytes);
            File.WriteAllBytes(movedShortcut, movedShortcutBytes);
            File.WriteAllBytes(copiedShortcut, copiedShortcutBytes);

            string expectedNote = GetSavedConnectionsNote(layout);

            Uninstall(layout, false, false);

            if (File.Exists(layout.RuntimePath) ||
                File.Exists(layout.SetupPath) ||
                File.Exists(layout.UninstallerPath) ||
                File.Exists(layout.UpdateSettingsPath) ||
                File.Exists(layout.UiSettingsPath) ||
                File.Exists(layout.SetupShortcutPath) ||
                File.Exists(layout.UninstallShortcutPath))
                return 50;

            if (!BytesEqual(generatedSettingsBytes,
                    File.ReadAllBytes(generatedSettingsPath)) ||
                !BytesEqual(generatedRdpBytes,
                    File.ReadAllBytes(generatedRdpPath)) ||
                !BytesEqual(importedSettingsBytes,
                    File.ReadAllBytes(importedSettingsPath)) ||
                !BytesEqual(importedRdpBytes,
                    File.ReadAllBytes(importedRdpCopyPath)) ||
                !BytesEqual(originalImportedRdpBytes,
                    File.ReadAllBytes(originalImportedRdpPath)) ||
                !BytesEqual(retainedIconBytes,
                    File.ReadAllBytes(retainedIconPath)) ||
                !BytesEqual(retainedOneTimeSettingsBytes,
                    File.ReadAllBytes(retainedOneTimeSettings)) ||
                !BytesEqual(retainedOneTimeRdpBytes,
                    File.ReadAllBytes(retainedOneTimeRdp)) ||
                !BytesEqual(otherAppBytes, File.ReadAllBytes(otherAppFile)) ||
                !BytesEqual(desktopShortcutBytes,
                    File.ReadAllBytes(desktopShortcut)) ||
                !BytesEqual(movedShortcutBytes,
                    File.ReadAllBytes(movedShortcut)) ||
                !BytesEqual(copiedShortcutBytes,
                    File.ReadAllBytes(copiedShortcut)) ||
                !BytesEqual(otherStartMenuBytes,
                    File.ReadAllBytes(otherStartMenuFile)))
                return 52;

            string exactNotePath = Path.Combine(installDirectory,
                "README - Saved RDP Connections.txt");
            if (!string.Equals(layout.SavedConnectionsNotePath, exactNotePath,
                    StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(exactNotePath))
                return 53;

            byte[] noteBytes = File.ReadAllBytes(exactNotePath);
            if (noteBytes.Length >= 3 && noteBytes[0] == 0xEF &&
                noteBytes[1] == 0xBB && noteBytes[2] == 0xBF)
                return 54;
            string note = new UTF8Encoding(false, true).GetString(noteBytes);
            if (note != expectedNote ||
                !note.Contains("Generated and imported profile copies under:") ||
                !note.Contains(
                    "Every connection.rdp and settings.ini file in those profiles") ||
                !note.Contains(
                    "Original imported .rdp files were never changed or deleted.") ||
                !note.Contains(
                    "Desktop reminder shortcuts; copies moved outside the Desktop folder") ||
                !note.Contains(
                    "does not search other folders or drives for .rdp or .lnk copies") ||
                !note.Contains(
                    "Reminder .lnk shortcuts require RDP Session Reminder to be reinstalled") ||
                !note.Contains(
                    "A preserved connection.rdp file can still be opened directly") ||
                !note.Contains(
                    "you can manually delete the entire folder below"))
                return 55;
            if (!RunRemoveSavedConnectionsSelfTest(root))
                return 56;
            if (!RunFailureOrderSelfTest(root, false) ||
                !RunFailureOrderSelfTest(root, true))
                return 57;
            return 0;
        }
        catch
        {
            return 51;
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    private static bool RunFailureOrderSelfTest(string root,
        bool removeSavedConnections)
    {
        string suffix = removeSavedConnections ? "remove" : "preserve";
        string caseRoot = Path.Combine(root, "failure-order-" + suffix);
        string installDirectory = Path.Combine(caseRoot, "app");
        string startMenuDirectory = Path.Combine(caseRoot, "start-menu");
        string desktopDirectory = Path.Combine(caseRoot, "desktop");
        Directory.CreateDirectory(installDirectory);
        Directory.CreateDirectory(startMenuDirectory);
        Directory.CreateDirectory(desktopDirectory);
        UninstallLayout layout = new UninstallLayout(
            installDirectory, startMenuDirectory, desktopDirectory);

        const string profileId = "44444444444444444444444444444444";
        string profile = Path.Combine(layout.ProfilesDirectory, profileId);
        Directory.CreateDirectory(profile);
        string settings = Path.Combine(profile, "settings.ini");
        string rdp = Path.Combine(profile, "connection.rdp");
        byte[] settingsBytes = Encoding.UTF8.GetBytes("failure-settings");
        byte[] rdpBytes = Encoding.Unicode.GetBytes(
            "full address:s:failure-order\r\n");
        File.WriteAllBytes(settings, settingsBytes);
        File.WriteAllBytes(rdp, rdpBytes);
        File.WriteAllText(layout.RuntimePath, "locked-runtime");
        string shortcut = Path.Combine(desktopDirectory, "Failure.lnk");
        CreateShortcutForSelfTest(shortcut, layout.RuntimePath,
            "--profile " + profileId);
        byte[] shortcutBytes = File.ReadAllBytes(shortcut);
        byte[] priorNoteBytes = Encoding.UTF8.GetBytes("prior-note-content");
        File.WriteAllBytes(layout.SavedConnectionsNotePath, priorNoteBytes);

        bool failed = false;
        using (FileStream lockedRuntime = new FileStream(layout.RuntimePath,
            FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            try
            {
                Uninstall(layout, false, removeSavedConnections);
            }
            catch (IOException)
            {
                failed = true;
            }
            catch (UnauthorizedAccessException)
            {
                failed = true;
            }
        }

        return failed &&
            BytesEqual(settingsBytes, File.ReadAllBytes(settings)) &&
            BytesEqual(rdpBytes, File.ReadAllBytes(rdp)) &&
            BytesEqual(shortcutBytes, File.ReadAllBytes(shortcut)) &&
            BytesEqual(priorNoteBytes,
                File.ReadAllBytes(layout.SavedConnectionsNotePath));
    }

    private static bool RunRemoveSavedConnectionsSelfTest(string root)
    {
        string caseRoot = Path.Combine(root, "remove-saved-case");
        string installDirectory = Path.Combine(caseRoot, "app");
        string startMenuDirectory = Path.Combine(caseRoot, "start-menu");
        string desktopDirectory = Path.Combine(caseRoot, "desktop");
        string movedDirectory = Path.Combine(caseRoot, "moved");
        string copiedDirectory = Path.Combine(caseRoot, "copied");
        string externalDirectory = Path.Combine(caseRoot, "external");
        Directory.CreateDirectory(installDirectory);
        Directory.CreateDirectory(startMenuDirectory);
        Directory.CreateDirectory(desktopDirectory);
        Directory.CreateDirectory(movedDirectory);
        Directory.CreateDirectory(copiedDirectory);
        Directory.CreateDirectory(externalDirectory);

        UninstallLayout layout = new UninstallLayout(
            installDirectory, startMenuDirectory, desktopDirectory);
        const string firstId = "11111111111111111111111111111111";
        const string secondId = "22222222222222222222222222222222";
        string firstProfile = Path.Combine(layout.ProfilesDirectory, firstId);
        string secondProfile = Path.Combine(layout.ProfilesDirectory, secondId);
        string nonProfile = Path.Combine(layout.ProfilesDirectory, "not-a-profile");
        Directory.CreateDirectory(firstProfile);
        Directory.CreateDirectory(secondProfile);
        Directory.CreateDirectory(nonProfile);

        File.WriteAllText(Path.Combine(firstProfile, "settings.ini"), "first");
        File.WriteAllText(Path.Combine(firstProfile, "connection.rdp"), "first");
        File.WriteAllText(Path.Combine(secondProfile, "settings.ini"), "second");
        File.WriteAllText(Path.Combine(secondProfile, "connection.rdp"), "second");
        byte[] profileSentinel = new byte[] { 4, 3, 2, 1, 0, 255 };
        string profileSentinelPath = Path.Combine(secondProfile, "keep.bin");
        File.WriteAllBytes(profileSentinelPath, profileSentinel);
        byte[] nonProfileBytes = Encoding.UTF8.GetBytes("unrelated-profile-dir");
        string nonProfilePath = Path.Combine(nonProfile, "settings.ini");
        File.WriteAllBytes(nonProfilePath, nonProfileBytes);

        string iconsDirectory = layout.IconsDirectory;
        Directory.CreateDirectory(iconsDirectory);
        string generatedIcon = Path.Combine(iconsDirectory,
            "reminder-v1.ico");
        File.WriteAllBytes(generatedIcon,
            new byte[] { 0, 1, 0, 0, 7, 6, 5, 4 });
        string unknownIcon = Path.Combine(iconsDirectory, "custom.ico");
        byte[] unknownIconBytes = new byte[] { 9, 8, 7, 6, 5 };
        File.WriteAllBytes(unknownIcon, unknownIconBytes);

        const string firstOneTimeId = "66666666666666666666666666666666";
        const string secondOneTimeId = "77777777777777777777777777777777";
        string firstOneTime = Path.Combine(layout.OneTimeProfilesDirectory,
            firstOneTimeId);
        string secondOneTime = Path.Combine(layout.OneTimeProfilesDirectory,
            secondOneTimeId);
        string unknownOneTime = Path.Combine(layout.OneTimeProfilesDirectory,
            "not-a-profile");
        Directory.CreateDirectory(firstOneTime);
        Directory.CreateDirectory(secondOneTime);
        Directory.CreateDirectory(unknownOneTime);
        File.WriteAllText(Path.Combine(firstOneTime, "settings.ini"),
            "temporary-settings");
        byte[] opaqueImportedRdp = new byte[] {
            0, 255, 254, 1, 13, 10, 128, 64, 0
        };
        File.WriteAllBytes(Path.Combine(firstOneTime, "connection.rdp"),
            opaqueImportedRdp);
        File.WriteAllText(Path.Combine(secondOneTime, "settings.ini"),
            "temporary-settings-with-extra");
        File.WriteAllBytes(Path.Combine(secondOneTime, "connection.rdp"),
            opaqueImportedRdp);
        byte[] oneTimeSentinel = Encoding.UTF8.GetBytes("keep-one-time-extra");
        string oneTimeSentinelPath = Path.Combine(secondOneTime, "keep.bin");
        File.WriteAllBytes(oneTimeSentinelPath, oneTimeSentinel);
        byte[] unknownOneTimeBytes = Encoding.UTF8.GetBytes(
            "keep-noncanonical-one-time");
        string unknownOneTimePath = Path.Combine(unknownOneTime,
            "connection.rdp");
        File.WriteAllBytes(unknownOneTimePath, unknownOneTimeBytes);

        File.WriteAllText(layout.RuntimePath, "runtime");
        File.WriteAllText(layout.UninstallerPath, "uninstaller");
        File.WriteAllText(layout.UpdateSettingsPath, "AutomaticChecks=1");
        File.WriteAllText(layout.UiSettingsPath, "Theme=Light");
        File.WriteAllText(layout.SetupShortcutPath, "setup");
        File.WriteAllText(layout.UninstallShortcutPath, "uninstall");
        string keepMenu = Path.Combine(startMenuDirectory, "Keep.lnk");
        File.WriteAllText(keepMenu, "keep-menu");

        string firstDesktop = Path.Combine(desktopDirectory, "First.lnk");
        string secondDesktop = Path.Combine(desktopDirectory, "Second.lnk");
        string copiedOnDesktop = Path.Combine(
            desktopDirectory, "Copied on Desktop.lnk");
        string unrelatedDesktop = Path.Combine(desktopDirectory, "Unrelated.lnk");
        string invalidDesktop = Path.Combine(desktopDirectory, "Invalid.lnk");
        string movedShortcut = Path.Combine(movedDirectory, "Moved.lnk");
        string copiedShortcut = Path.Combine(copiedDirectory, "Copied.lnk");
        CreateShortcutForSelfTest(firstDesktop, layout.RuntimePath,
            "--profile " + firstId);
        CreateShortcutForSelfTest(secondDesktop, layout.RuntimePath,
            "--profile " + secondId);
        CreateShortcutForSelfTest(copiedOnDesktop, layout.RuntimePath,
            "--profile " + firstId);
        CreateShortcutForSelfTest(unrelatedDesktop,
            Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.System), "cmd.exe"), "");
        CreateShortcutForSelfTest(invalidDesktop, layout.RuntimePath,
            "--profile not-a-canonical-id");
        CreateShortcutForSelfTest(movedShortcut, layout.RuntimePath,
            "--profile " + firstId);
        CreateShortcutForSelfTest(copiedShortcut, layout.RuntimePath,
            "--profile " + firstId);

        byte[] originalBytes = Encoding.Unicode.GetBytes(
            "full address:s:original-imported-source\r\n");
        string originalPath = Path.Combine(externalDirectory, "original.rdp");
        File.WriteAllBytes(originalPath, originalBytes);
        string rootSentinel = Path.Combine(installDirectory, "user-kept.txt");
        byte[] rootSentinelBytes = Encoding.UTF8.GetBytes("keep-root-file");
        File.WriteAllBytes(rootSentinel, rootSentinelBytes);
        File.WriteAllText(layout.SavedConnectionsNotePath, "old note");

        Uninstall(layout, false, true);
        if (File.Exists(firstDesktop) || File.Exists(secondDesktop) ||
            File.Exists(copiedOnDesktop) ||
            !File.Exists(unrelatedDesktop) || !File.Exists(invalidDesktop) ||
            !File.Exists(movedShortcut) || !File.Exists(copiedShortcut) ||
            Directory.Exists(firstProfile) ||
            File.Exists(Path.Combine(secondProfile, "settings.ini")) ||
            File.Exists(Path.Combine(secondProfile, "connection.rdp")) ||
            File.Exists(generatedIcon) ||
            !BytesEqual(unknownIconBytes, File.ReadAllBytes(unknownIcon)) ||
            Directory.Exists(firstOneTime) ||
            File.Exists(Path.Combine(secondOneTime, "settings.ini")) ||
            File.Exists(Path.Combine(secondOneTime, "connection.rdp")) ||
            !BytesEqual(oneTimeSentinel,
                File.ReadAllBytes(oneTimeSentinelPath)) ||
            !BytesEqual(unknownOneTimeBytes,
                File.ReadAllBytes(unknownOneTimePath)) ||
            !BytesEqual(profileSentinel,
                File.ReadAllBytes(profileSentinelPath)) ||
            !BytesEqual(nonProfileBytes, File.ReadAllBytes(nonProfilePath)) ||
            !BytesEqual(originalBytes, File.ReadAllBytes(originalPath)) ||
            !BytesEqual(rootSentinelBytes, File.ReadAllBytes(rootSentinel)) ||
            File.Exists(layout.SavedConnectionsNotePath) ||
            File.Exists(layout.UpdateSettingsPath) ||
            File.Exists(layout.UiSettingsPath) ||
            File.Exists(layout.RuntimePath) ||
            File.Exists(layout.UninstallerPath) ||
            File.Exists(layout.SetupShortcutPath) ||
            File.Exists(layout.UninstallShortcutPath) ||
            !File.Exists(keepMenu))
            return false;

        return RunCleanRemovalSelfTest(caseRoot, originalBytes);
    }

    private static bool RunCleanRemovalSelfTest(string caseRoot,
        byte[] originalBytes)
    {
        string installDirectory = Path.Combine(caseRoot, "clean-app");
        string startMenuDirectory = Path.Combine(caseRoot, "clean-start-menu");
        string desktopDirectory = Path.Combine(caseRoot, "clean-desktop");
        string externalDirectory = Path.Combine(caseRoot, "clean-external");
        Directory.CreateDirectory(installDirectory);
        Directory.CreateDirectory(startMenuDirectory);
        Directory.CreateDirectory(desktopDirectory);
        Directory.CreateDirectory(externalDirectory);
        UninstallLayout layout = new UninstallLayout(
            installDirectory, startMenuDirectory, desktopDirectory);

        const string profileId = "33333333333333333333333333333333";
        string profile = Path.Combine(layout.ProfilesDirectory, profileId);
        Directory.CreateDirectory(profile);
        File.WriteAllText(Path.Combine(profile, "settings.ini"), "clean");
        File.WriteAllText(Path.Combine(profile, "connection.rdp"), "clean");
        Directory.CreateDirectory(layout.IconsDirectory);
        File.WriteAllBytes(Path.Combine(layout.IconsDirectory,
            "personal-v1.ico"), new byte[] { 0, 1, 0, 0, 3, 2, 1 });
        const string oneTimeId = "88888888888888888888888888888888";
        string oneTimeProfile = Path.Combine(
            layout.OneTimeProfilesDirectory, oneTimeId);
        Directory.CreateDirectory(oneTimeProfile);
        File.WriteAllText(Path.Combine(oneTimeProfile, "settings.ini"),
            "clean-one-time");
        File.WriteAllBytes(Path.Combine(oneTimeProfile, "connection.rdp"),
            new byte[] { 0, 255, 1, 254, 2 });
        File.WriteAllText(layout.RuntimePath, "runtime");
        File.WriteAllText(layout.UninstallerPath, "uninstaller");
        File.WriteAllText(layout.UpdateSettingsPath, "AutomaticChecks=0");
        File.WriteAllText(layout.UiSettingsPath, "Theme=Dark");
        File.WriteAllText(layout.SetupShortcutPath, "setup");
        File.WriteAllText(layout.UninstallShortcutPath, "uninstall");
        string desktopShortcut = Path.Combine(desktopDirectory, "Clean.lnk");
        CreateShortcutForSelfTest(desktopShortcut, layout.RuntimePath,
            "--profile " + profileId);
        string original = Path.Combine(externalDirectory, "original.rdp");
        File.WriteAllBytes(original, originalBytes);

        Uninstall(layout, false, true);
        return !Directory.Exists(installDirectory) &&
            !Directory.Exists(startMenuDirectory) &&
            !File.Exists(desktopShortcut) &&
            BytesEqual(originalBytes, File.ReadAllBytes(original));
    }

    private static void CreateShortcutForSelfTest(string path, string target,
        string arguments)
    {
        object shell = null;
        object shortcut = null;
        try
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
                throw new InvalidOperationException(
                    "Windows Script Host is unavailable.");
            shell = Activator.CreateInstance(shellType);
            shortcut = shellType.InvokeMember("CreateShortcut",
                BindingFlags.InvokeMethod, null, shell, new object[] { path });
            Type shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty,
                null, shortcut, new object[] { target });
            shortcutType.InvokeMember("Arguments", BindingFlags.SetProperty,
                null, shortcut, new object[] { arguments });
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod,
                null, shortcut, null);
        }
        finally
        {
            ReleaseComObject(shortcut);
            ReleaseComObject(shell);
        }
    }

    private static bool BytesEqual(byte[] expected, byte[] actual)
    {
        if (expected == null || actual == null ||
            expected.Length != actual.Length)
            return false;
        for (int index = 0; index < expected.Length; index++)
        {
            if (expected[index] != actual[index])
                return false;
        }
        return true;
    }
}
