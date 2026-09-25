using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("RDP Session Reminder Setup")]
[assembly: AssemblyDescription("Setup for RDP Session Reminder.")]
[assembly: AssemblyCompany("RDP Session Reminder contributors")]
[assembly: AssemblyProduct("RDP Session Reminder")]
[assembly: AssemblyCopyright("Copyright (c) 2026")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

internal static class SetupProgram
{
    [STAThread]
    private static int Main(string[] args)
    {
        foreach (string argument in args)
        {
            if (string.Equals(argument, "--self-test", StringComparison.OrdinalIgnoreCase))
                return RunSelfTest();
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new SetupForm());
        return 0;
    }

    private static int RunSelfTest()
    {
        string directory = null;
        try
        {
            if (!SettingsStore.IsValidComputerName("workstation-01"))
                return 20;

            directory = Path.Combine(Path.GetTempPath(),
                "RdpSessionReminderSetup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            string existing = Path.Combine(directory, "Remote Lab.lnk");
            File.WriteAllText(existing, "unrelated shortcut placeholder");
            string shortcut = ShortcutPathHelper.ReserveUniquePath(directory, "Remote Lab");
            if (!shortcut.EndsWith("Remote Lab (2).lnk", StringComparison.Ordinal) ||
                File.ReadAllText(existing) != "unrelated shortcut placeholder")
                return 21;

            const string arguments =
                "--profile 00000000000000000000000000000000";
            ShortcutWriter.Create(shortcut, Application.ExecutablePath, directory,
                arguments);
            return File.Exists(shortcut) && new FileInfo(shortcut).Length > 0 &&
                ShortcutWriter.ReadArguments(shortcut) == arguments ? 0 : 22;
        }
        catch
        {
            return 23;
        }
        finally
        {
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}

internal sealed class SetupForm : Form
{
    private readonly TextBox computerText;
    private readonly TextBox rdpFileText;
    private readonly ComboBox displayCombo;
    private readonly TextBox reminderText;
    private readonly TextBox shortcutText;
    private readonly CheckBox fullScreenCheck;
    private bool updatingShortcut;
    private bool updatingReminder;
    private bool shortcutManuallyEdited;
    private bool reminderManuallyEdited;

    public SetupForm()
    {
        Text = "RDP Session Reminder Setup";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(540, 640);
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;

        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
        }

        Label title = new Label();
        title.AutoSize = false;
        title.Location = new Point(28, 22);
        title.Size = new Size(484, 38);
        title.Font = new Font("Segoe UI Semibold", 17f, FontStyle.Bold);
        title.Text = "RDP Session Reminder";
        Controls.Add(title);

        Label introduction = new Label();
        introduction.AutoSize = false;
        introduction.Location = new Point(30, 66);
        introduction.Size = new Size(475, 50);
        introduction.Text =
            "Create one desktop shortcut for one Remote Desktop connection. " +
            "Its reminder runs on this PC and closes with that RDP session.";
        Controls.Add(introduction);

        Label computerLabel = new Label();
        computerLabel.AutoSize = true;
        computerLabel.Location = new Point(30, 126);
        computerLabel.Text = "Computer name or IP address";
        Controls.Add(computerLabel);

        computerText = new TextBox();
        computerText.Location = new Point(33, 150);
        computerText.Size = new Size(474, 25);
        computerText.MaxLength = 255;
        computerText.TabIndex = 0;
        computerText.TextChanged += ComputerTextChanged;
        Controls.Add(computerText);

        Label rdpFileLabel = new Label();
        rdpFileLabel.AutoSize = true;
        rdpFileLabel.Location = new Point(30, 189);
        rdpFileLabel.Text = "RDP connection file (optional)";
        Controls.Add(rdpFileLabel);

        rdpFileText = new TextBox();
        rdpFileText.Location = new Point(33, 213);
        rdpFileText.Size = new Size(385, 25);
        rdpFileText.TabIndex = 1;
        rdpFileText.TextChanged += delegate
        {
            fullScreenCheck.Enabled = string.IsNullOrWhiteSpace(rdpFileText.Text);
        };
        Controls.Add(rdpFileText);

        Button browseButton = new Button();
        browseButton.Location = new Point(426, 211);
        browseButton.Size = new Size(81, 29);
        browseButton.Text = "Browse...";
        browseButton.TabIndex = 2;
        browseButton.Click += BrowseForRdpFile;
        Controls.Add(browseButton);

        Label displayLabel = new Label();
        displayLabel.AutoSize = true;
        displayLabel.Location = new Point(30, 252);
        displayLabel.Text = "Local display for the reminder";
        Controls.Add(displayLabel);

        displayCombo = new ComboBox();
        displayCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        displayCombo.Location = new Point(33, 276);
        displayCombo.Size = new Size(474, 25);
        displayCombo.TabIndex = 3;
        Controls.Add(displayCombo);

        foreach (Screen screen in Screen.AllScreens)
        {
            DisplayChoice choice = new DisplayChoice(screen);
            int itemIndex = displayCombo.Items.Add(choice);
            if (screen.Primary)
                displayCombo.SelectedIndex = itemIndex;
        }
        if (displayCombo.SelectedIndex < 0 && displayCombo.Items.Count > 0)
            displayCombo.SelectedIndex = 0;

        Label reminderLabel = new Label();
        reminderLabel.AutoSize = true;
        reminderLabel.Location = new Point(30, 315);
        reminderLabel.Text = "Reminder text";
        Controls.Add(reminderLabel);

        reminderText = new TextBox();
        reminderText.Location = new Point(33, 339);
        reminderText.Size = new Size(474, 25);
        reminderText.MaxLength = 100;
        reminderText.TabIndex = 4;
        reminderText.TextChanged += delegate
        {
            if (!updatingReminder)
                reminderManuallyEdited = true;
        };
        Controls.Add(reminderText);

        Label shortcutLabel = new Label();
        shortcutLabel.AutoSize = true;
        shortcutLabel.Location = new Point(30, 378);
        shortcutLabel.Text = "Desktop shortcut name";
        Controls.Add(shortcutLabel);

        shortcutText = new TextBox();
        shortcutText.Location = new Point(33, 402);
        shortcutText.Size = new Size(474, 25);
        shortcutText.MaxLength = 80;
        shortcutText.TabIndex = 5;
        shortcutText.TextChanged += delegate
        {
            if (!updatingShortcut)
                shortcutManuallyEdited = true;
        };
        Controls.Add(shortcutText);

        fullScreenCheck = new CheckBox();
        fullScreenCheck.AutoSize = true;
        fullScreenCheck.Location = new Point(33, 442);
        fullScreenCheck.Text = "Open direct connections in full screen";
        fullScreenCheck.Checked = true;
        fullScreenCheck.TabIndex = 6;
        Controls.Add(fullScreenCheck);

        Label association = new Label();
        association.AutoSize = false;
        association.Location = new Point(33, 475);
        association.Size = new Size(474, 48);
        association.ForeColor = Color.FromArgb(70, 70, 70);
        association.Text =
            "This creates a separate shortcut for this connection only. Other RDP " +
            "files and shortcuts are not changed.";
        Controls.Add(association);

        Label privacy = new Label();
        privacy.AutoSize = false;
        privacy.Location = new Point(33, 527);
        privacy.Size = new Size(474, 42);
        privacy.ForeColor = Color.FromArgb(80, 80, 80);
        privacy.Text =
            "The app never requests credentials. Windows Remote Desktop handles sign-in.";
        Controls.Add(privacy);

        Button connectButton = new Button();
        connectButton.Location = new Point(320, 592);
        connectButton.Size = new Size(112, 30);
        connectButton.Text = "Save && Connect";
        connectButton.TabIndex = 7;
        connectButton.Click += SaveAndConnect;
        Controls.Add(connectButton);
        AcceptButton = connectButton;

        Button cancelButton = new Button();
        cancelButton.Location = new Point(440, 592);
        cancelButton.Size = new Size(67, 30);
        cancelButton.Text = "Cancel";
        cancelButton.TabIndex = 8;
        cancelButton.DialogResult = DialogResult.Cancel;
        Controls.Add(cancelButton);
        CancelButton = cancelButton;
    }

    private void BrowseForRdpFile(object sender, EventArgs eventArgs)
    {
        using (OpenFileDialog dialog = new OpenFileDialog())
        {
            dialog.Title = "Choose a Remote Desktop connection";
            dialog.Filter = "Remote Desktop files (*.rdp)|*.rdp|All files (*.*)|*.*";
            dialog.CheckFileExists = true;
            dialog.Multiselect = false;
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            rdpFileText.Text = dialog.FileName;
            string address = TryReadFullAddress(dialog.FileName);
            if (!string.IsNullOrEmpty(address) &&
                SettingsStore.IsValidComputerName(address))
                computerText.Text = address;
        }
    }

    private static string TryReadFullAddress(string path)
    {
        try
        {
            foreach (string line in File.ReadAllLines(path))
            {
                const string prefix = "full address:s:";
                if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return line.Substring(prefix.Length).Trim();
            }
        }
        catch
        {
        }
        return "";
    }

    private void ComputerTextChanged(object sender, EventArgs eventArgs)
    {
        string computer = SettingsStore.NormalizeComputerName(computerText.Text);
        if (!shortcutManuallyEdited)
            SetShortcutText(computer.Length == 0 ? "" : "Remote - " + computer);
        if (!reminderManuallyEdited)
            SetReminderText(computer.Length == 0
                ? ""
                : "REMOTE SESSION - " + computer.ToUpperInvariant());
    }

    private void SetShortcutText(string value)
    {
        updatingShortcut = true;
        shortcutText.Text = value;
        updatingShortcut = false;
    }

    private void SetReminderText(string value)
    {
        updatingReminder = true;
        reminderText.Text = value;
        updatingReminder = false;
    }

    private void SaveAndConnect(object sender, EventArgs eventArgs)
    {
        string computer = SettingsStore.NormalizeComputerName(computerText.Text);
        if (!SettingsStore.IsValidComputerName(computer))
        {
            MessageBox.Show(this,
                "Enter a valid computer name, DNS name, IP address, or optional port.",
                AppPaths.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            computerText.Focus();
            computerText.SelectAll();
            return;
        }

        string selectedRdpFile = rdpFileText.Text.Trim();
        if (selectedRdpFile.Length > 0 &&
            (!File.Exists(selectedRdpFile) ||
             !string.Equals(Path.GetExtension(selectedRdpFile), ".rdp",
                StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(this,
                "Choose an existing .rdp connection file, or leave the field empty.",
                AppPaths.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            rdpFileText.Focus();
            rdpFileText.SelectAll();
            return;
        }

        string profileId = Guid.NewGuid().ToString("N");
        string profileDirectory = AppPaths.GetProfileDirectory(profileId);
        ReminderSettings settings = new ReminderSettings();
        settings.ComputerName = computer;
        settings.FullScreen = fullScreenCheck.Checked;
        settings.ShortcutName = SettingsStore.NormalizeShortcutName(
            shortcutText.Text, computer);
        settings.ReminderText = SettingsStore.NormalizeReminderText(
            reminderText.Text, computer);
        DisplayChoice selectedDisplay = displayCombo.SelectedItem as DisplayChoice;
        settings.DisplayDevice = selectedDisplay == null ? "" : selectedDisplay.DeviceName;
        settings.RdpFile = selectedRdpFile.Length == 0
            ? ""
            : AppPaths.GetProfileConnectionPath(profileId);

        string reservedShortcutPath = "";
        bool shortcutCompleted = false;
        try
        {
            InstallApplicationFiles();
            Directory.CreateDirectory(profileDirectory);
            if (selectedRdpFile.Length > 0)
                CopyRdpFileVerified(selectedRdpFile, settings.RdpFile);

            string desktop = Environment.GetFolderPath(
                Environment.SpecialFolder.DesktopDirectory);
            using (Mutex shortcutMutex = new Mutex(
                false, "Local\\RdpSessionReminder-ShortcutCreation"))
            {
                bool ownsShortcutMutex = false;
                try
                {
                    try
                    {
                        ownsShortcutMutex = shortcutMutex.WaitOne(
                            TimeSpan.FromSeconds(30));
                    }
                    catch (AbandonedMutexException)
                    {
                        ownsShortcutMutex = true;
                    }
                    if (!ownsShortcutMutex)
                        throw new TimeoutException(
                            "Another setup window is creating a shortcut. Try again.");

                    reservedShortcutPath = ShortcutPathHelper.ReserveUniquePath(
                        desktop, settings.ShortcutName);
                    settings.ShortcutName = Path.GetFileNameWithoutExtension(
                        reservedShortcutPath);
                    SettingsStore.SaveTo(
                        AppPaths.GetProfileSettingsPath(profileId), settings);
                    ShortcutWriter.Create(reservedShortcutPath, AppPaths.RuntimePath,
                        AppPaths.InstallDirectory, "--profile " + profileId);
                    shortcutCompleted = true;
                }
                finally
                {
                    if (ownsShortcutMutex)
                        shortcutMutex.ReleaseMutex();
                }
            }

            ShellRefresh.NotifyItem(reservedShortcutPath);
            ShellRefresh.NotifyDirectory(desktop);

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = AppPaths.RuntimePath;
            startInfo.Arguments = "--profile " + profileId;
            startInfo.UseShellExecute = true;
            Process.Start(startInfo);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            CleanupFailedProfile(
                profileDirectory, reservedShortcutPath, shortcutCompleted);
            MessageBox.Show(this,
                "Setup could not be completed.\r\n\r\n" + exception.Message,
                AppPaths.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void CleanupFailedProfile(string profileDirectory,
        string reservedShortcutPath, bool shortcutCompleted)
    {
        if (shortcutCompleted)
            return;

        bool shortcutAbsent = string.IsNullOrEmpty(reservedShortcutPath) ||
            !File.Exists(reservedShortcutPath);
        try
        {
            if (!shortcutAbsent && File.Exists(reservedShortcutPath) &&
                new FileInfo(reservedShortcutPath).Length == 0)
            {
                File.Delete(reservedShortcutPath);
                shortcutAbsent = true;
            }
        }
        catch
        {
        }

        if (!shortcutAbsent)
            return;

        try
        {
            if (Directory.Exists(profileDirectory))
                Directory.Delete(profileDirectory, true);
        }
        catch
        {
        }
    }

    private static void InstallApplicationFiles()
    {
        Directory.CreateDirectory(AppPaths.InstallDirectory);
        string currentSetup = Application.ExecutablePath;
        string currentDirectory = Path.GetDirectoryName(currentSetup);
        string sourceRuntime = Path.Combine(currentDirectory, AppPaths.RuntimeFileName);
        if (!File.Exists(sourceRuntime) && !File.Exists(AppPaths.RuntimePath))
            throw new FileNotFoundException(
                "The runtime executable is missing from the release package.", sourceRuntime);

        CopyUnlessSame(sourceRuntime, AppPaths.RuntimePath);
        CopyUnlessSame(currentSetup, AppPaths.SetupPath);
    }

    private static void CopyUnlessSame(string source, string destination)
    {
        if (!File.Exists(source))
            return;
        if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(destination),
            StringComparison.OrdinalIgnoreCase))
            return;
        if (File.Exists(destination) && FilesMatch(source, destination))
            return;
        File.Copy(source, destination, true);
    }

    private static void CopyRdpFileVerified(string source, string destination)
    {
        File.Copy(source, destination, false);
        if (!FilesMatch(source, destination))
            throw new IOException("The copied RDP file could not be verified.");
    }

    private static bool FilesMatch(string first, string second)
    {
        if (new FileInfo(first).Length != new FileInfo(second).Length)
            return false;

        byte[] firstHash;
        byte[] secondHash;
        using (SHA256 algorithm = SHA256.Create())
        using (FileStream stream = File.OpenRead(first))
            firstHash = algorithm.ComputeHash(stream);
        using (SHA256 algorithm = SHA256.Create())
        using (FileStream stream = File.OpenRead(second))
            secondHash = algorithm.ComputeHash(stream);

        if (firstHash.Length != secondHash.Length)
            return false;
        for (int index = 0; index < firstHash.Length; index++)
        {
            if (firstHash[index] != secondHash[index])
                return false;
        }
        return true;
    }
}

internal static class ShortcutPathHelper
{
    public static string ReserveUniquePath(string desktop, string shortcutName)
    {
        for (int number = 1; number < 10000; number++)
        {
            string suffix = number == 1 ? "" : " (" + number.ToString() + ")";
            string path = Path.Combine(desktop, shortcutName + suffix + ".lnk");
            if (File.Exists(path) || Directory.Exists(path))
                continue;

            try
            {
                using (new FileStream(path, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None))
                {
                }
                return path;
            }
            catch (IOException)
            {
                if (!File.Exists(path) && !Directory.Exists(path))
                    throw;
            }
        }

        throw new IOException("A unique desktop shortcut name could not be reserved.");
    }
}

internal static class ShortcutWriter
{
    public static void Create(string shortcutPath, string targetPath,
        string workingDirectory, string arguments)
    {
        Type shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null)
            throw new InvalidOperationException("Windows Script Host is unavailable.");

        object shell = Activator.CreateInstance(shellType);
        object shortcut = null;
        try
        {
            shortcut = shellType.InvokeMember("CreateShortcut",
                BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
            Type shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty,
                null, shortcut, new object[] { targetPath });
            shortcutType.InvokeMember("Arguments", BindingFlags.SetProperty,
                null, shortcut, new object[] { arguments });
            shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty,
                null, shortcut, new object[] { workingDirectory });
            shortcutType.InvokeMember("Description", BindingFlags.SetProperty,
                null, shortcut, new object[] {
                    "Connect with a local Remote Desktop session reminder" });
            shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty,
                null, shortcut, new object[] {
                    Path.Combine(Environment.SystemDirectory, "mstsc.exe") + ",0" });
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod,
                null, shortcut, null);
        }
        finally
        {
            if (shortcut != null && Marshal.IsComObject(shortcut))
                Marshal.FinalReleaseComObject(shortcut);
            if (Marshal.IsComObject(shell))
                Marshal.FinalReleaseComObject(shell);
        }
    }

    public static string ReadArguments(string shortcutPath)
    {
        Type shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null)
            throw new InvalidOperationException("Windows Script Host is unavailable.");

        object shell = Activator.CreateInstance(shellType);
        object shortcut = null;
        try
        {
            shortcut = shellType.InvokeMember("CreateShortcut",
                BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
            return (string)shortcut.GetType().InvokeMember("Arguments",
                BindingFlags.GetProperty, null, shortcut, null);
        }
        finally
        {
            if (shortcut != null && Marshal.IsComObject(shortcut))
                Marshal.FinalReleaseComObject(shortcut);
            if (Marshal.IsComObject(shell))
                Marshal.FinalReleaseComObject(shell);
        }
    }
}

internal sealed class DisplayChoice
{
    public readonly string DeviceName;
    private readonly string label;

    public DisplayChoice(Screen screen)
    {
        DeviceName = screen.DeviceName;
        label = string.Format("{0}{1} - {2} x {3}",
            screen.DeviceName,
            screen.Primary ? " (Primary)" : "",
            screen.Bounds.Width,
            screen.Bounds.Height);
    }

    public override string ToString()
    {
        return label;
    }
}

internal static class ShellRefresh
{
    [DllImport("shell32.dll", EntryPoint = "SHChangeNotify", CharSet = CharSet.Unicode)]
    private static extern void NotifyPath(
        uint eventId, uint flags, string item, IntPtr secondItem);

    public static void NotifyItem(string path)
    {
        NotifyPath(0x00002000, 0x1005, path, IntPtr.Zero);
    }

    public static void NotifyDirectory(string path)
    {
        NotifyPath(0x00001000, 0x1005, path, IntPtr.Zero);
    }
}
