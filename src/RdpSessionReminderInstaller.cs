using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("RDP Session Reminder Installer")]
[assembly: AssemblyDescription("Per-user installer for RDP Session Reminder.")]
[assembly: AssemblyCompany("RDP Session Reminder contributors")]
[assembly: AssemblyProduct("RDP Session Reminder")]
[assembly: AssemblyCopyright("Copyright (c) 2026")]
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]

internal static class InstallerProgram
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (HasArgument(args, "--self-test"))
            return InstallerEngine.RunSelfTest();

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        InstallerOptions options = null;
        try
        {
            options = InstallerOptions.Parse(args, Application.ExecutablePath);
            if (options.WaitForProcessId > 0)
                InstallerOptions.WaitForProcessExit(options.WaitForProcessId);

            using (Mutex installerMutex = new Mutex(
                false, "Local\\RdpSessionReminder-Installer"))
            {
                bool ownsMutex = false;
                try
                {
                    try
                    {
                        ownsMutex = installerMutex.WaitOne(TimeSpan.FromSeconds(30));
                    }
                    catch (AbandonedMutexException)
                    {
                        ownsMutex = true;
                    }
                    if (!ownsMutex)
                        throw new TimeoutException(
                            "Another RDP Session Reminder installation is in progress.");

                    InstallerEngine.Install(InstallLayout.CreateForCurrentUser(), true);
                }
                finally
                {
                    if (ownsMutex)
                        installerMutex.ReleaseMutex();
                }
            }

            InstallLayout layout = InstallLayout.CreateForCurrentUser();
            DialogResult launch = MessageBox.Show(
                "RDP Session Reminder was installed for your Windows account.\r\n\r\n" +
                "Application files:\r\n" + layout.InstallDirectory + "\r\n\r\n" +
                "Open the shortcut setup now?",
                ProductInfo.ProductName, MessageBoxButtons.YesNo,
                MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
            if (launch == DialogResult.Yes)
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = layout.SetupPath;
                startInfo.UseShellExecute = true;
                InstallerEngine.EnsureSafeDirectoryRoot(
                    layout.InstallDirectory, "application folder", true);
                Process.Start(startInfo);
            }
            return 0;
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                "RDP Session Reminder could not be installed.\r\n\r\n" +
                exception.Message,
                ProductInfo.ProductName, MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }
        finally
        {
            if (options != null &&
                options.TemporaryUpdate &&
                !string.IsNullOrEmpty(options.CleanupSourceDirectory))
                InstallerOptions.ScheduleSourceCleanup(
                    options.CleanupSourceDirectory);
        }
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
}

internal sealed class InstallerOptions
{
    public int WaitForProcessId;
    public bool TemporaryUpdate;
    public string CleanupSourceDirectory;

    public static InstallerOptions Parse(string[] args, string executablePath)
    {
        InstallerOptions options = new InstallerOptions();
        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (string.Equals(argument, "--wait-for-pid",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(argument, "--wait-for-process",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (options.WaitForProcessId != 0 || index + 1 >= args.Length)
                    throw new ArgumentException(
                        "The installer wait process argument is invalid.");
                options.WaitForProcessId = ParsePositiveProcessId(
                    args[++index]);
            }
            else if (string.Equals(argument, "--cleanup-source-directory",
                StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(options.CleanupSourceDirectory) ||
                    index + 1 >= args.Length)
                    throw new ArgumentException(
                        "The installer cleanup directory argument is invalid.");
                options.CleanupSourceDirectory = args[++index];
            }
            else if (string.Equals(argument, "--temporary-update",
                StringComparison.OrdinalIgnoreCase))
            {
                if (options.TemporaryUpdate)
                    throw new ArgumentException(
                        "The temporary update argument was provided more than once.");
                options.TemporaryUpdate = true;
            }
            else
            {
                throw new ArgumentException(
                    "Unknown installer argument: " + argument);
            }
        }

        if (options.TemporaryUpdate)
        {
            string cleanupDirectory = string.IsNullOrEmpty(
                options.CleanupSourceDirectory)
                ? Path.GetDirectoryName(Path.GetFullPath(executablePath))
                : options.CleanupSourceDirectory;
            options.CleanupSourceDirectory = ValidateCleanupDirectory(
                cleanupDirectory, executablePath);
        }
        else if (!string.IsNullOrEmpty(options.CleanupSourceDirectory))
        {
            throw new ArgumentException(
                "--cleanup-source-directory requires --temporary-update.");
        }
        return options;
    }

    private static int ParsePositiveProcessId(string value)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException("A positive process ID is required.");
        foreach (char character in value)
        {
            if (character < '0' || character > '9')
                throw new ArgumentException("A positive process ID is required.");
        }
        int processId;
        if (!int.TryParse(value,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out processId) || processId <= 0)
            throw new ArgumentException("A positive process ID is required.");
        return processId;
    }

    private static string ValidateCleanupDirectory(string value,
        string executablePath)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException(
                "A valid updater cleanup directory is required.");

        string directory = Path.GetFullPath(value);
        string temporaryRoot = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string parent = Path.GetDirectoryName(directory);
        string name = Path.GetFileName(directory);
        string executable = Path.GetFullPath(executablePath);
        const string prefix = "RdpSessionReminderUpdate-";
        if (!PathsEqual(parent, temporaryRoot) ||
            name.Length != prefix.Length + 32 ||
            !name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !IsHex(name.Substring(prefix.Length)) ||
            !Directory.Exists(directory) ||
            (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0 ||
            !PathsEqual(Path.GetDirectoryName(executable), directory) ||
            !string.Equals(Path.GetFileName(executable),
                ProductInfo.InstallerFileName,
                StringComparison.OrdinalIgnoreCase) ||
            !HasOnlyExpectedUpdateFiles(directory, executable))
            throw new ArgumentException(
                "The updater cleanup directory is outside the expected temporary " +
                "location.");
        return directory;
    }

    private static bool HasOnlyExpectedUpdateFiles(string directory,
        string executable)
    {
        if (Directory.GetDirectories(directory, "*",
                SearchOption.TopDirectoryOnly).Length != 0)
            return false;
        bool foundInstaller = false;
        bool foundChecksums = false;
        foreach (string file in Directory.GetFiles(directory, "*",
            SearchOption.TopDirectoryOnly))
        {
            if (PathsEqual(file, executable))
                foundInstaller = true;
            else if (string.Equals(Path.GetFileName(file), "SHA256SUMS.txt",
                StringComparison.OrdinalIgnoreCase))
                foundChecksums = true;
            else
                return false;
        }
        return foundInstaller && foundChecksums;
    }

    private static bool IsHex(string value)
    {
        foreach (char character in value)
        {
            if (!((character >= '0' && character <= '9') ||
                  (character >= 'a' && character <= 'f') ||
                  (character >= 'A' && character <= 'F')))
                return false;
        }
        return value.Length > 0;
    }

    private static bool PathsEqual(string first, string second)
    {
        if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second))
            return false;
        return string.Equals(Path.GetFullPath(first).TrimEnd('\\', '/'),
            Path.GetFullPath(second).TrimEnd('\\', '/'),
            StringComparison.OrdinalIgnoreCase);
    }

    public static void WaitForProcessExit(int processId)
    {
        if (processId <= 0 || processId == Process.GetCurrentProcess().Id)
            throw new ArgumentException(
                "The installer cannot wait for that process ID.");
        try
        {
            using (Process process = Process.GetProcessById(processId))
            {
                if (!process.WaitForExit(120000))
                    throw new TimeoutException(
                        "The application being updated did not close in time.");
            }
        }
        catch (ArgumentException)
        {
            // The process already exited before the installer began waiting.
        }
    }

    public static void ScheduleSourceCleanup(string directory)
    {
        try
        {
            string script =
                "$d='" + directory.Replace("'", "''") + "';" +
                "$item=Get-Item -LiteralPath $d -Force -ErrorAction Stop;" +
                "if(($item.Attributes -band [IO.FileAttributes]::ReparsePoint) " +
                "-ne 0){exit};" +
                "for($i=0;$i -lt 20;$i++){Start-Sleep -Milliseconds 250;" +
                "try{Remove-Item -LiteralPath $d -Recurse -Force " +
                "-ErrorAction Stop;break}catch{}}";
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
            // A future update check can remove a stale validated temp folder.
        }
    }

    public static bool RunSelfTest()
    {
        string directory = Path.Combine(Path.GetTempPath(),
            "RdpSessionReminderUpdate-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            string executable = Path.Combine(directory,
                "RdpSessionReminder-Installer.exe");
            File.WriteAllText(executable, "fixture");
            File.WriteAllText(Path.Combine(directory, "SHA256SUMS.txt"),
                "fixture checksum");
            InstallerOptions parsed = Parse(new string[] {
                "--wait-for-pid", "2147483647",
                "--temporary-update",
                "--cleanup-source-directory", directory
            }, executable);
            if (parsed.WaitForProcessId != int.MaxValue ||
                !parsed.TemporaryUpdate ||
                !PathsEqual(parsed.CleanupSourceDirectory, directory))
                return false;

            WaitForProcessExit(int.MaxValue);
            bool rejectedCurrent = false;
            try
            {
                WaitForProcessExit(Process.GetCurrentProcess().Id);
            }
            catch (ArgumentException)
            {
                rejectedCurrent = true;
            }
            bool rejectedUnsafeDirectory = false;
            try
            {
                Parse(new string[] { "--temporary-update",
                    "--cleanup-source-directory", Path.GetTempPath() },
                    executable);
            }
            catch (ArgumentException)
            {
                rejectedUnsafeDirectory = true;
            }
            InstallerOptions inferred = Parse(new string[] {
                "--temporary-update"
            }, executable);
            InstallerOptions manual = Parse(new string[0], executable);
            bool rejectedUnexpectedFile = false;
            string unexpected = Path.Combine(directory, "unexpected.bin");
            File.WriteAllText(unexpected, "unexpected");
            try
            {
                Parse(new string[] { "--temporary-update" }, executable);
            }
            catch (ArgumentException)
            {
                rejectedUnexpectedFile = true;
            }
            File.Delete(unexpected);
            return rejectedCurrent && rejectedUnsafeDirectory &&
                rejectedUnexpectedFile &&
                inferred.TemporaryUpdate &&
                PathsEqual(inferred.CleanupSourceDirectory, directory) &&
                !manual.TemporaryUpdate &&
                string.IsNullOrEmpty(manual.CleanupSourceDirectory) &&
                manual.WaitForProcessId == 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}

internal static class ProductInfo
{
    public const string ProductName = "RDP Session Reminder";
    public const string Version = "1.2.0";
    public const string RuntimeFileName = "RdpSessionReminder.exe";
    public const string SetupFileName = "RdpSessionReminderSetup.exe";
    public const string InstallerFileName = "RdpSessionReminder-Installer.exe";
    public const string UninstallerFileName =
        "RdpSessionReminder-Uninstaller.exe";
    public const string StartMenuFolderName = "RDP Session Reminder";
    public const string SetupShortcutName =
        "RDP Session Reminder Setup.lnk";
    public const string UninstallShortcutName =
        "Uninstall RDP Session Reminder.lnk";
    public const string UninstallRegistryPath =
        @"Software\Microsoft\Windows\CurrentVersion\Uninstall\RdpSessionReminder";
}

internal sealed class InstallLayout
{
    public readonly string InstallDirectory;
    public readonly string StartMenuDirectory;

    public InstallLayout(string installDirectory, string startMenuDirectory)
    {
        InstallDirectory = Path.GetFullPath(installDirectory);
        StartMenuDirectory = Path.GetFullPath(startMenuDirectory);
    }

    public string RuntimePath
    {
        get { return Path.Combine(InstallDirectory, ProductInfo.RuntimeFileName); }
    }

    public string SetupPath
    {
        get { return Path.Combine(InstallDirectory, ProductInfo.SetupFileName); }
    }

    public string UninstallerPath
    {
        get
        {
            return Path.Combine(InstallDirectory,
                ProductInfo.UninstallerFileName);
        }
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
                ProductInfo.SetupShortcutName);
        }
    }

    public string UninstallShortcutPath
    {
        get
        {
            return Path.Combine(StartMenuDirectory,
                ProductInfo.UninstallShortcutName);
        }
    }

    public static InstallLayout CreateForCurrentUser()
    {
        string installDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RdpSessionReminder");
        string startMenuDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            ProductInfo.StartMenuFolderName);
        return new InstallLayout(installDirectory, startMenuDirectory);
    }
}

internal static class InstallerEngine
{
    private const string RuntimeResource =
        "RdpSessionReminder.Payload.Runtime";
    private const string SetupResource =
        "RdpSessionReminder.Payload.Setup";
    private const string UninstallerResource =
        "RdpSessionReminder.Payload.Uninstaller";

    public static void Install(InstallLayout layout, bool registerApplication)
    {
        if (layout == null)
            throw new ArgumentNullException("layout");

        EnsureSafeDirectoryRoot(
            layout.InstallDirectory, "application folder", false);
        EnsureSafeDirectoryRoot(
            layout.StartMenuDirectory, "Start menu folder", false);

        Directory.CreateDirectory(layout.InstallDirectory);
        EnsureSafeDirectoryRoot(
            layout.InstallDirectory, "application folder", true);
        WriteExecutableResource(RuntimeResource, layout.RuntimePath,
            layout.InstallDirectory);
        WriteExecutableResource(SetupResource, layout.SetupPath,
            layout.InstallDirectory);
        WriteExecutableResource(UninstallerResource, layout.UninstallerPath,
            layout.InstallDirectory);

        EnsureSafeDirectoryRoot(
            layout.StartMenuDirectory, "Start menu folder", false);
        Directory.CreateDirectory(layout.StartMenuDirectory);
        EnsureSafeDirectoryRoot(
            layout.StartMenuDirectory, "Start menu folder", true);
        EnsureSafeDirectoryRoot(
            layout.InstallDirectory, "application folder", true);
        InstallerShortcutWriter.Create(
            layout.SetupShortcutPath, layout.SetupPath,
            layout.InstallDirectory, "Create or update a Remote Desktop reminder shortcut");
        EnsureSafeDirectoryRoot(
            layout.StartMenuDirectory, "Start menu folder", true);
        EnsureSafeDirectoryRoot(
            layout.InstallDirectory, "application folder", true);
        InstallerShortcutWriter.Create(
            layout.UninstallShortcutPath, layout.UninstallerPath,
            layout.InstallDirectory, "Uninstall RDP Session Reminder");

        if (registerApplication)
        {
            EnsureSafeDirectoryRoot(
                layout.InstallDirectory, "application folder", true);
            RegisterApplication(layout);
        }

        // This exact file is an app-owned note created by the uninstaller.
        // A successful reinstall makes its uninstall message stale.
        EnsureSafeDirectoryRoot(
            layout.InstallDirectory, "application folder", true);
        if (File.Exists(layout.SavedConnectionsNotePath))
        {
            EnsureSafeDirectoryRoot(
                layout.InstallDirectory, "application folder", true);
            File.Delete(layout.SavedConnectionsNotePath);
        }
    }

    private static void WriteExecutableResource(string resourceName,
        string destination, string installDirectory)
    {
        EnsureSafeDirectoryRoot(
            installDirectory, "application folder", true);
        EnsureSafeFileDestination(destination, "application file");
        string temporary = destination + ".installing-" +
            Guid.NewGuid().ToString("N");
        try
        {
            using (Stream source = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(resourceName))
            {
                if (source == null)
                    throw new InvalidDataException(
                        "The installer payload is incomplete: " + resourceName);
                int first = source.ReadByte();
                int second = source.ReadByte();
                if (first != 'M' || second != 'Z')
                    throw new InvalidDataException(
                        "The installer payload is not a Windows executable: " +
                        resourceName);
                source.Position = 0;
                EnsureSafeDirectoryRoot(
                    installDirectory, "application folder", true);
                using (FileStream output = new FileStream(temporary,
                    FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    source.CopyTo(output);
            }

            EnsureSafeDirectoryRoot(
                installDirectory, "application folder", true);
            EnsureSafeFileDestination(destination, "application file");
            if (File.Exists(destination))
            {
                try
                {
                    EnsureSafeDirectoryRoot(
                        installDirectory, "application folder", true);
                    File.Replace(temporary, destination, null, true);
                }
                catch (PlatformNotSupportedException)
                {
                    EnsureSafeDirectoryRoot(
                        installDirectory, "application folder", true);
                    EnsureSafeFileDestination(destination,
                        "application file");
                    File.Delete(destination);
                    EnsureSafeDirectoryRoot(
                        installDirectory, "application folder", true);
                    File.Move(temporary, destination);
                }
            }
            else
            {
                EnsureSafeDirectoryRoot(
                    installDirectory, "application folder", true);
                File.Move(temporary, destination);
            }
        }
        finally
        {
            if (File.Exists(temporary))
            {
                EnsureSafeDirectoryRoot(
                    installDirectory, "application folder", true);
                File.Delete(temporary);
            }
        }
    }

    internal static void EnsureSafeDirectoryRoot(string path,
        string description, bool mustExist)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A directory path is required.", "path");
        string fullPath = Path.GetFullPath(path);
        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(fullPath);
        }
        catch (FileNotFoundException)
        {
            if (mustExist)
                throw new DirectoryNotFoundException(
                    "The " + description + " no longer exists: " + fullPath);
            return;
        }
        catch (DirectoryNotFoundException)
        {
            if (mustExist)
                throw new DirectoryNotFoundException(
                    "The " + description + " no longer exists: " + fullPath);
            return;
        }

        if ((attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException(
                "The " + description +
                " cannot be a reparse point or directory link: " + fullPath);
        if ((attributes & FileAttributes.Directory) == 0)
            throw new IOException(
                "The " + description + " is not a directory: " + fullPath);
    }

    internal static void EnsureSafeFileDestination(string path,
        string description)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A file path is required.", "path");
        string fullPath = Path.GetFullPath(path);
        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(fullPath);
        }
        catch (FileNotFoundException)
        {
            return;
        }
        catch (DirectoryNotFoundException)
        {
            return;
        }

        if ((attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException(
                "The " + description +
                " cannot be a reparse point or file link: " + fullPath);
        if ((attributes & FileAttributes.Directory) != 0)
            throw new IOException(
                "The " + description + " cannot be a directory: " + fullPath);
    }

    private static void RegisterApplication(InstallLayout layout)
    {
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(
            ProductInfo.UninstallRegistryPath))
        {
            if (key == null)
                throw new InvalidOperationException(
                    "Windows could not register the per-user uninstaller.");

            key.SetValue("DisplayName", ProductInfo.ProductName,
                RegistryValueKind.String);
            key.SetValue("DisplayVersion", ProductInfo.Version,
                RegistryValueKind.String);
            key.SetValue("Publisher", "RDP Session Reminder contributors",
                RegistryValueKind.String);
            key.SetValue("InstallLocation", layout.InstallDirectory,
                RegistryValueKind.String);
            key.SetValue("DisplayIcon", layout.SetupPath + ",0",
                RegistryValueKind.String);
            key.SetValue("UninstallString",
                QuoteCommandLineArgument(layout.UninstallerPath),
                RegistryValueKind.String);
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            long bytes = new FileInfo(layout.RuntimePath).Length +
                new FileInfo(layout.SetupPath).Length +
                new FileInfo(layout.UninstallerPath).Length;
            key.SetValue("EstimatedSize", (int)Math.Min(int.MaxValue,
                Math.Max(1L, (bytes + 1023L) / 1024L)), RegistryValueKind.DWord);
        }
    }

    private static string QuoteCommandLineArgument(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    public static int RunSelfTest()
    {
        if (!InstallerOptions.RunSelfTest())
            return 42;

        string root = Path.Combine(Path.GetTempPath(),
            "RdpSessionReminderInstaller-" + Guid.NewGuid().ToString("N"));
        try
        {
            string installDirectory = Path.Combine(root, "app");
            string startMenuDirectory = Path.Combine(root, "start-menu");
            string profileDirectory = Path.Combine(
                installDirectory, "profiles", "profile-a");
            Directory.CreateDirectory(profileDirectory);
            string settingsPath = Path.Combine(profileDirectory, "settings.ini");
            string rdpPath = Path.Combine(profileDirectory, "connection.rdp");
            string desktopDirectory = Path.Combine(root, "desktop");
            Directory.CreateDirectory(desktopDirectory);
            string desktopShortcut = Path.Combine(
                desktopDirectory, "Remote retained.lnk");
            byte[] settingsBytes = System.Text.Encoding.UTF8.GetBytes(
                "preserve-settings-after-uninstall");
            byte[] rdpBytes = System.Text.Encoding.Unicode.GetBytes(
                "full address:s:preserve-after-uninstall\r\n");
            byte[] shortcutBytes = System.Text.Encoding.UTF8.GetBytes(
                "preserve-shortcut-after-uninstall");
            File.WriteAllBytes(settingsPath, settingsBytes);
            File.WriteAllBytes(rdpPath, rdpBytes);
            File.WriteAllBytes(desktopShortcut, shortcutBytes);
            string uiSettingsPath = Path.Combine(
                installDirectory, "ui-settings.ini");
            byte[] uiSettingsBytes = System.Text.Encoding.UTF8.GetBytes(
                "Theme=Light\n");
            File.WriteAllBytes(uiSettingsPath, uiSettingsBytes);

            InstallLayout layout = new InstallLayout(
                installDirectory, startMenuDirectory);
            File.WriteAllText(layout.SavedConnectionsNotePath,
                "stale uninstall note");
            Install(layout, false);

            if (!IsExecutable(layout.RuntimePath) ||
                !IsExecutable(layout.SetupPath) ||
                !IsExecutable(layout.UninstallerPath) ||
                !File.Exists(layout.SetupShortcutPath) ||
                !File.Exists(layout.UninstallShortcutPath) ||
                !BytesEqual(settingsBytes, File.ReadAllBytes(settingsPath)) ||
                !BytesEqual(rdpBytes, File.ReadAllBytes(rdpPath)) ||
                 !BytesEqual(shortcutBytes,
                     File.ReadAllBytes(desktopShortcut)) ||
                 !BytesEqual(uiSettingsBytes,
                     File.ReadAllBytes(uiSettingsPath)) ||
                 File.Exists(layout.SavedConnectionsNotePath))
                return 40;
            return 0;
        }
        catch
        {
            return 41;
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    private static bool IsExecutable(string path)
    {
        if (!File.Exists(path))
            return false;
        using (FileStream stream = File.OpenRead(path))
            return stream.ReadByte() == 'M' && stream.ReadByte() == 'Z';
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

internal static class InstallerShortcutWriter
{
    public static void Create(string path, string target, string workingDirectory,
        string description)
    {
        string shortcutDirectory = Path.GetDirectoryName(
            Path.GetFullPath(path));
        InstallerEngine.EnsureSafeDirectoryRoot(
            shortcutDirectory, "Start menu folder", true);
        InstallerEngine.EnsureSafeFileDestination(path,
            "Start menu shortcut");
        string temporary = path + ".new-" + Guid.NewGuid().ToString("N") +
            ".lnk";
        object shell = null;
        object shortcut = null;
        try
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
                throw new InvalidOperationException(
                    "Windows Script Host is unavailable, so the Start menu shortcut " +
                    "could not be created.");
            shell = Activator.CreateInstance(shellType);
            shortcut = shellType.InvokeMember("CreateShortcut",
                BindingFlags.InvokeMethod, null, shell,
                new object[] { temporary });
            Type shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty,
                null, shortcut, new object[] { target });
            shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty,
                null, shortcut, new object[] { workingDirectory });
            shortcutType.InvokeMember("Description", BindingFlags.SetProperty,
                null, shortcut, new object[] { description });
            shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty,
                null, shortcut, new object[] { target + ",0" });
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod,
                null, shortcut, null);

            InstallerEngine.EnsureSafeDirectoryRoot(
                shortcutDirectory, "Start menu folder", true);
            InstallerEngine.EnsureSafeFileDestination(path,
                "Start menu shortcut");
            if (File.Exists(path))
            {
                try
                {
                    File.Replace(temporary, path, null, true);
                }
                catch (PlatformNotSupportedException)
                {
                    InstallerEngine.EnsureSafeFileDestination(path,
                        "Start menu shortcut");
                    File.Delete(path);
                    InstallerEngine.EnsureSafeDirectoryRoot(
                        shortcutDirectory, "Start menu folder", true);
                    File.Move(temporary, path);
                }
            }
            else
            {
                File.Move(temporary, path);
            }
        }
        finally
        {
            ReleaseComObject(shortcut);
            ReleaseComObject(shell);
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private static void ReleaseComObject(object value)
    {
        if (value != null && System.Runtime.InteropServices.Marshal.IsComObject(value))
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(value);
    }
}
