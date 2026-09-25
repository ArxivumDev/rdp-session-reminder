using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using Microsoft.Win32;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("RDP Session Reminder Setup")]
[assembly: AssemblyDescription("Setup for RDP Session Reminder.")]
[assembly: AssemblyCompany("RDP Session Reminder contributors")]
[assembly: AssemblyProduct("RDP Session Reminder")]
[assembly: AssemblyCopyright("Copyright (c) 2026")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

internal static class SetupProgram
{
    [STAThread]
    private static int Main(string[] args)
    {
        foreach (string argument in args)
        {
            if (string.Equals(argument, "--self-test", StringComparison.OrdinalIgnoreCase))
                return RunSelfTest();
            if (string.Equals(argument, "--remove-publisher-trust",
                    StringComparison.OrdinalIgnoreCase))
                return RemovePublisherTrustForUninstall();
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new SetupForm());
        return 0;
    }

    private static int RemovePublisherTrustForUninstall()
    {
        try
        {
            LocalRdpPublisherTrust.Remove();
            return 0;
        }
        catch
        {
            return 30;
        }
    }

    private static int RunSelfTest()
    {
        string directory = null;
        try
        {
            if (!SettingsStore.IsValidComputerName("workstation-01") ||
                !SettingsStore.IsValidComputerName("10.0.0.25:3390") ||
                !SettingsStore.IsValidComputerName("[2001:db8::1]:3389") ||
                SettingsStore.IsValidComputerName("010.0.0.1") ||
                SettingsStore.IsValidComputerName("server: 3389") ||
                SettingsStore.IsValidComputerName("server:\uFF11\uFF12\uFF13") ||
                SettingsStore.IsValidComputerName("a\n.example") ||
                SettingsStore.IsValidComputerName("0x7f000001") ||
                SettingsStore.IsValidComputerName("1234"))
                return 20;

            if (string.Equals(SettingsStore.NormalizeShortcutName(
                    "CON", "workstation-01"), "CON", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(SettingsStore.NormalizeShortcutName(
                    "LPT1.txt", "workstation-01"), "LPT1.txt",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(SettingsStore.NormalizeShortcutName(
                    "COM\u00B9", "workstation-01"), "COM\u00B9",
                    StringComparison.OrdinalIgnoreCase) ||
                SettingsStore.NormalizeShortcutName(
                    new string('.', 80) + "X", "workstation-01").Length == 0 ||
                string.Equals(SettingsStore.NormalizeShortcutName(
                    "CON" + new string(' ', 77) + "X", "workstation-01"),
                    "CON", StringComparison.OrdinalIgnoreCase))
                return 25;

            directory = Path.Combine(Path.GetTempPath(),
                "RdpSessionReminderSetup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            string rdpPath = Path.Combine(directory, "target.rdp");
            File.WriteAllLines(rdpPath, new string[] {
                "FuLl AdDrEsS:s:lab-a",
                "AlTeRnAtE FuLl AdDrEsS:s:lab-b"
            });
            if (SetupForm.TryReadRdpTarget(rdpPath) != "lab-b")
                return 24;

            File.WriteAllLines(rdpPath, new string[] {
                "full address:s:lab-full"
            });
            if (SetupForm.TryReadRdpTarget(rdpPath) != "lab-full")
                return 26;

            File.WriteAllLines(rdpPath, new string[] {
                "full address:s:lab-fallback",
                "alternate full address:s:   "
            });
            if (SetupForm.TryReadRdpTarget(rdpPath) != "lab-fallback")
                return 27;

            File.WriteAllLines(rdpPath, new string[] {
                "screen mode id:i:2"
            });
            if (SetupForm.TryReadRdpTarget(rdpPath).Length != 0)
                return 28;

            string generatedRdpPath = Path.Combine(directory, "generated.rdp");
            RdpProfileOptions generatedOptions = new RdpProfileOptions(
                "desktop.example.test", true, 1920, 1080, true, true,
                true, true, true, true, true, true);
            RdpProfileWriter.Write(generatedRdpPath, generatedOptions);
            byte[] generatedBytes = File.ReadAllBytes(generatedRdpPath);
            string generatedContent = string.Join("\n",
                File.ReadAllLines(generatedRdpPath));
            if (generatedBytes.Length < 2 || generatedBytes[0] != 0xFF ||
                generatedBytes[1] != 0xFE ||
                !generatedContent.Contains("full address:s:desktop.example.test") ||
                !generatedContent.Contains("screen mode id:i:2") ||
                !generatedContent.Contains("desktopwidth:i:1920") ||
                !generatedContent.Contains("desktopheight:i:1080") ||
                !generatedContent.Contains("use multimon:i:1") ||
                !generatedContent.Contains("session bpp:i:32") ||
                !generatedContent.Contains("displayconnectionbar:i:1") ||
                !generatedContent.Contains("keyboardhook:i:2") ||
                !generatedContent.Contains("audiomode:i:0") ||
                !generatedContent.Contains("networkautodetect:i:1") ||
                !generatedContent.Contains("bandwidthautodetect:i:1") ||
                !generatedContent.Contains("bitmapcachepersistenable:i:1") ||
                !generatedContent.Contains("autoreconnection enabled:i:1") ||
                !generatedContent.Contains("authentication level:i:2") ||
                !generatedContent.Contains("enablecredsspsupport:i:1") ||
                !generatedContent.Contains("prompt for credentials:i:1") ||
                !generatedContent.Contains("drivestoredirect:s:*") ||
                !generatedContent.Contains("redirectlocation:i:1") ||
                !generatedContent.Contains("redirectcomports:i:1") ||
                !generatedContent.Contains("redirectwebauthn:i:1") ||
                !generatedContent.Contains("redirectsmartcards:i:1") ||
                !generatedContent.Contains("redirectclipboard:i:1") ||
                generatedContent.Contains("signscope:s:"))
                return 29;

            string existing = Path.Combine(directory, "Remote Lab.lnk");
            File.WriteAllText(existing, "unrelated shortcut placeholder");
            string shortcut = ShortcutPathHelper.ReserveUniquePath(directory, "Remote Lab");
            if (!shortcut.EndsWith("Remote Lab (2).lnk", StringComparison.Ordinal) ||
                File.ReadAllText(existing) != "unrelated shortcut placeholder")
                return 21;

            const string arguments =
                "--profile 00000000000000000000000000000000";
            ShortcutWriter.Create(shortcut, Application.ExecutablePath, "",
                arguments);
            if (!File.Exists(shortcut) || new FileInfo(shortcut).Length == 0 ||
                ShortcutWriter.ReadArguments(shortcut) != arguments ||
                ShortcutWriter.ReadWorkingDirectory(shortcut).Length != 0 ||
                !Path.IsPathRooted(ShortcutWriter.ReadTargetPath(shortcut)))
                return 22;

            string staleNote = Path.Combine(directory,
                "README - Saved RDP Connections.txt");
            string retainedFile = Path.Combine(directory, "retained-profile.rdp");
            File.WriteAllText(staleNote, "stale uninstall note");
            File.WriteAllText(retainedFile, "retained profile");
            SetupForm.DeleteStaleUninstallNote(directory);
            if (File.Exists(staleNote) ||
                File.ReadAllText(retainedFile) != "retained profile")
                return 31;
            return 0;
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
    private readonly ComboBox resolutionCombo;
    private readonly CheckBox allMonitorsCheck;
    private readonly CheckBox alwaysAskCredentialsCheck;
    private readonly CheckBox redirectClipboardCheck;
    private readonly CheckBox redirectDrivesCheck;
    private readonly CheckBox redirectLocationCheck;
    private readonly CheckBox redirectComPortsCheck;
    private readonly CheckBox redirectWebAuthnCheck;
    private readonly CheckBox redirectSmartCardsCheck;
    private readonly CheckBox trustPublisherCheck;
    private readonly Label publisherTrustStatus;
    private readonly Button removePublisherTrustButton;
    private readonly TabControl tabs;
    private readonly Button connectButton;
    private readonly Button cancelButton;
    private readonly Label operationStatus;
    private readonly ProgressBar operationProgress;
    private readonly UpdatePageController updateController;
    private bool updatingShortcut;
    private bool updatingReminder;
    private bool shortcutManuallyEdited;
    private bool reminderManuallyEdited;
    private bool operationRunning;
    private bool allowClose;

    public SetupForm()
    {
        Text = "RDP Session Reminder Setup";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        AutoScroll = true;
        ClientSize = new Size(660, 730);
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
        title.Size = new Size(604, 38);
        title.Font = new Font("Segoe UI Semibold", 17f, FontStyle.Bold);
        title.Text = "RDP Session Reminder";
        Controls.Add(title);

        Label introduction = new Label();
        introduction.AutoSize = false;
        introduction.Location = new Point(30, 66);
        introduction.Size = new Size(595, 42);
        introduction.Text =
            "Create a guided Remote Desktop profile and its reminder shortcut, or " +
            "use an existing .rdp file unchanged.";
        Controls.Add(introduction);

        tabs = new TabControl();
        tabs.Location = new Point(25, 116);
        tabs.Size = new Size(610, 424);
        tabs.TabIndex = 0;
        Controls.Add(tabs);

        TabPage connectionTab = new TabPage("Connection");
        connectionTab.Padding = new Padding(12);
        tabs.TabPages.Add(connectionTab);

        TabPage displayTab = new TabPage("Display");
        displayTab.Padding = new Padding(12);
        tabs.TabPages.Add(displayTab);

        TabPage resourcesTab = new TabPage("Local resources && trust");
        resourcesTab.Padding = new Padding(12);
        tabs.TabPages.Add(resourcesTab);

        updateController = UpdateUi.Attach(this, tabs);

        Label computerLabel = new Label();
        computerLabel.AutoSize = true;
        computerLabel.Location = new Point(18, 18);
        computerLabel.Text = "Full computer name (FQDN), computer name, or IP address";
        connectionTab.Controls.Add(computerLabel);

        computerText = new TextBox();
        computerText.Location = new Point(21, 42);
        computerText.Size = new Size(550, 25);
        computerText.MaxLength = 255;
        computerText.TabIndex = 0;
        computerText.TextChanged += ComputerTextChanged;
        connectionTab.Controls.Add(computerText);

        Label rdpFileLabel = new Label();
        rdpFileLabel.AutoSize = true;
        rdpFileLabel.Location = new Point(18, 80);
        rdpFileLabel.Text = "Existing RDP connection file (optional)";
        connectionTab.Controls.Add(rdpFileLabel);

        rdpFileText = new TextBox();
        rdpFileText.Location = new Point(21, 104);
        rdpFileText.Size = new Size(455, 25);
        rdpFileText.TabIndex = 1;
        connectionTab.Controls.Add(rdpFileText);

        Button browseButton = new Button();
        browseButton.Location = new Point(484, 102);
        browseButton.Size = new Size(87, 29);
        browseButton.Text = "Browse...";
        browseButton.TabIndex = 2;
        browseButton.Click += BrowseForRdpFile;
        connectionTab.Controls.Add(browseButton);

        Label fileModeHelp = new Label();
        fileModeHelp.AutoSize = false;
        fileModeHelp.Location = new Point(21, 136);
        fileModeHelp.Size = new Size(550, 36);
        fileModeHelp.ForeColor = Color.FromArgb(80, 80, 80);
        fileModeHelp.Text =
            "Selecting a file copies it byte-for-byte. Custom display, resource, and " +
            "publisher settings below apply only when this field is empty.";
        connectionTab.Controls.Add(fileModeHelp);

        Label displayLabel = new Label();
        displayLabel.AutoSize = true;
        displayLabel.Location = new Point(18, 18);
        displayLabel.Text = "Local display for the reminder";
        displayTab.Controls.Add(displayLabel);

        displayCombo = new ComboBox();
        displayCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        displayCombo.Location = new Point(21, 42);
        displayCombo.Size = new Size(550, 25);
        displayCombo.TabIndex = 0;
        displayTab.Controls.Add(displayCombo);

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
        reminderLabel.Location = new Point(18, 184);
        reminderLabel.Text = "Reminder text";
        connectionTab.Controls.Add(reminderLabel);

        reminderText = new TextBox();
        reminderText.Location = new Point(21, 208);
        reminderText.Size = new Size(550, 25);
        reminderText.MaxLength = 100;
        reminderText.TabIndex = 3;
        reminderText.TextChanged += delegate
        {
            if (!updatingReminder)
                reminderManuallyEdited = true;
        };
        connectionTab.Controls.Add(reminderText);

        Label shortcutLabel = new Label();
        shortcutLabel.AutoSize = true;
        shortcutLabel.Location = new Point(18, 247);
        shortcutLabel.Text = "Desktop shortcut name";
        connectionTab.Controls.Add(shortcutLabel);

        shortcutText = new TextBox();
        shortcutText.Location = new Point(21, 271);
        shortcutText.Size = new Size(550, 25);
        shortcutText.MaxLength = 80;
        shortcutText.TabIndex = 4;
        shortcutText.TextChanged += delegate
        {
            if (!updatingShortcut)
                shortcutManuallyEdited = true;
        };
        connectionTab.Controls.Add(shortcutText);

        alwaysAskCredentialsCheck = new CheckBox();
        alwaysAskCredentialsCheck.AutoSize = true;
        alwaysAskCredentialsCheck.Location = new Point(21, 311);
        alwaysAskCredentialsCheck.Text =
            "Always ask for credentials (allows a different user name)";
        alwaysAskCredentialsCheck.Checked = true;
        alwaysAskCredentialsCheck.TabIndex = 5;
        connectionTab.Controls.Add(alwaysAskCredentialsCheck);

        fullScreenCheck = new CheckBox();
        fullScreenCheck.AutoSize = true;
        fullScreenCheck.Location = new Point(21, 84);
        fullScreenCheck.Text = "Open the remote session in full screen";
        fullScreenCheck.Checked = true;
        fullScreenCheck.TabIndex = 1;
        displayTab.Controls.Add(fullScreenCheck);

        Label resolutionLabel = new Label();
        resolutionLabel.AutoSize = true;
        resolutionLabel.Location = new Point(18, 121);
        resolutionLabel.Text = "Remote desktop resolution";
        displayTab.Controls.Add(resolutionLabel);

        resolutionCombo = new ComboBox();
        resolutionCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        resolutionCombo.Location = new Point(21, 145);
        resolutionCombo.Size = new Size(365, 25);
        resolutionCombo.TabIndex = 2;
        foreach (ResolutionChoice choice in ResolutionChoice.ForPrimaryDisplay())
            resolutionCombo.Items.Add(choice);
        if (resolutionCombo.Items.Count > 0)
            resolutionCombo.SelectedIndex = 0;
        displayTab.Controls.Add(resolutionCombo);

        allMonitorsCheck = new CheckBox();
        allMonitorsCheck.AutoSize = true;
        allMonitorsCheck.Location = new Point(21, 188);
        allMonitorsCheck.Text = "Use all local monitors for the remote session";
        allMonitorsCheck.Checked = false;
        allMonitorsCheck.TabIndex = 3;
        allMonitorsCheck.CheckedChanged += delegate
        {
            UpdateCustomOptionState();
        };
        displayTab.Controls.Add(allMonitorsCheck);

        Label displayDefaults = new Label();
        displayDefaults.AutoSize = false;
        displayDefaults.Location = new Point(21, 229);
        displayDefaults.Size = new Size(550, 95);
        displayDefaults.ForeColor = Color.FromArgb(70, 70, 70);
        displayDefaults.Text =
            "Generated profiles always use highest color quality (32 bit), show the " +
            "full-screen connection bar, send Windows key combinations only in full " +
            "screen, play remote audio on this PC, detect connection quality, keep a " +
            "persistent bitmap cache, reconnect after drops, and warn if server " +
            "authentication fails. Resolution is controlled by Windows when all " +
            "monitors are used.";
        displayTab.Controls.Add(displayDefaults);

        Label resourcesLabel = new Label();
        resourcesLabel.AutoSize = true;
        resourcesLabel.Location = new Point(18, 18);
        resourcesLabel.Text = "Resources available inside the remote session";
        resourcesTab.Controls.Add(resourcesLabel);

        redirectClipboardCheck = CreateResourceCheckBox(
            resourcesTab, "Clipboard", 21, 49, true, 0);
        redirectDrivesCheck = CreateResourceCheckBox(
            resourcesTab, "All local drives", 300, 49, false, 1);
        redirectLocationCheck = CreateResourceCheckBox(
            resourcesTab, "Location", 21, 82, false, 2);
        redirectComPortsCheck = CreateResourceCheckBox(
            resourcesTab, "Serial and COM ports", 300, 82, false, 3);
        redirectWebAuthnCheck = CreateResourceCheckBox(
            resourcesTab,
            "WebAuthn (Windows Hello for Business and security keys)",
            21, 115, true, 4);
        redirectSmartCardsCheck = CreateResourceCheckBox(
            resourcesTab, "Smart cards", 21, 148, false, 5);

        Label trustHeading = new Label();
        trustHeading.AutoSize = true;
        trustHeading.Font = new Font(Font, FontStyle.Bold);
        trustHeading.Location = new Point(18, 196);
        trustHeading.Text = "Optional RDP publisher trust";
        resourcesTab.Controls.Add(trustHeading);

        trustPublisherCheck = new CheckBox();
        trustPublisherCheck.AutoSize = false;
        trustPublisherCheck.Location = new Point(21, 225);
        trustPublisherCheck.Size = new Size(550, 44);
        trustPublisherCheck.Text =
            "Create or reuse a private, non-exportable certificate on this Windows " +
            "account, trust that RDP publisher for this user, and sign this generated profile";
        trustPublisherCheck.Checked = false;
        trustPublisherCheck.TabIndex = 6;
        resourcesTab.Controls.Add(trustPublisherCheck);

        publisherTrustStatus = new Label();
        publisherTrustStatus.AutoSize = false;
        publisherTrustStatus.Location = new Point(21, 277);
        publisherTrustStatus.Size = new Size(550, 39);
        publisherTrustStatus.ForeColor = Color.FromArgb(75, 75, 75);
        resourcesTab.Controls.Add(publisherTrustStatus);

        removePublisherTrustButton = new Button();
        removePublisherTrustButton.Location = new Point(21, 322);
        removePublisherTrustButton.Size = new Size(250, 30);
        removePublisherTrustButton.Text = "Remove and verify publisher trust";
        removePublisherTrustButton.TabIndex = 7;
        removePublisherTrustButton.Click += RemovePublisherTrust;
        resourcesTab.Controls.Add(removePublisherTrustButton);

        Label trustExplanation = new Label();
        trustExplanation.AutoSize = false;
        trustExplanation.Location = new Point(286, 320);
        trustExplanation.Size = new Size(285, 51);
        trustExplanation.ForeColor = Color.FromArgb(80, 80, 80);
        trustExplanation.Text =
            "Windows trusts any .rdp signed by this private local key for this user. " +
            "This does not code-sign or trust the application executable.";
        resourcesTab.Controls.Add(trustExplanation);

        rdpFileText.TextChanged += RdpFileTextChanged;
        UpdatePublisherTrustStatus();
        UpdateCustomOptionState();

        Label association = new Label();
        association.AutoSize = false;
        association.Location = new Point(30, 550);
        association.Size = new Size(595, 38);
        association.ForeColor = Color.FromArgb(70, 70, 70);
        association.Text =
            "This creates a separate shortcut for this connection only. Other RDP " +
            "files and shortcuts are not changed.";
        Controls.Add(association);

        Label privacy = new Label();
        privacy.AutoSize = false;
        privacy.Location = new Point(30, 590);
        privacy.Size = new Size(595, 30);
        privacy.ForeColor = Color.FromArgb(80, 80, 80);
        privacy.Text =
            "The app never requests credentials. Windows Remote Desktop handles sign-in.";
        Controls.Add(privacy);

        operationStatus = new Label();
        operationStatus.AutoSize = false;
        operationStatus.Location = new Point(30, 623);
        operationStatus.Size = new Size(595, 20);
        operationStatus.ForeColor = Color.FromArgb(55, 55, 55);
        operationStatus.Visible = false;
        Controls.Add(operationStatus);

        operationProgress = new ProgressBar();
        operationProgress.Location = new Point(30, 647);
        operationProgress.Size = new Size(595, 12);
        operationProgress.Minimum = 0;
        operationProgress.Maximum = 7;
        operationProgress.Style = ProgressBarStyle.Continuous;
        operationProgress.Visible = false;
        Controls.Add(operationProgress);

        connectButton = new Button();
        connectButton.Location = new Point(430, 686);
        connectButton.Size = new Size(128, 30);
        connectButton.Text = "Create && Connect";
        connectButton.TabIndex = 1;
        connectButton.Click += SaveAndConnect;
        Controls.Add(connectButton);
        AcceptButton = connectButton;

        cancelButton = new Button();
        cancelButton.Location = new Point(566, 686);
        cancelButton.Size = new Size(67, 30);
        cancelButton.Text = "Cancel";
        cancelButton.TabIndex = 2;
        cancelButton.DialogResult = DialogResult.Cancel;
        Controls.Add(cancelButton);
        CancelButton = cancelButton;

        Shown += FitToCurrentWorkingArea;
        FormClosing += SetupFormClosing;
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

    private static CheckBox CreateResourceCheckBox(Control parent, string text,
        int left, int top, bool isChecked, int tabIndex)
    {
        CheckBox checkBox = new CheckBox();
        checkBox.AutoSize = true;
        checkBox.Location = new Point(left, top);
        checkBox.Text = text;
        checkBox.Checked = isChecked;
        checkBox.TabIndex = tabIndex;
        parent.Controls.Add(checkBox);
        return checkBox;
    }

    private void UpdateCustomOptionState()
    {
        bool customProfile = rdpFileText.Text.Trim().Length == 0;
        fullScreenCheck.Enabled = customProfile;
        allMonitorsCheck.Enabled = customProfile;
        resolutionCombo.Enabled = customProfile && !allMonitorsCheck.Checked;
        alwaysAskCredentialsCheck.Enabled = customProfile;
        redirectClipboardCheck.Enabled = customProfile;
        redirectDrivesCheck.Enabled = customProfile;
        redirectLocationCheck.Enabled = customProfile;
        redirectComPortsCheck.Enabled = customProfile;
        redirectWebAuthnCheck.Enabled = customProfile;
        redirectSmartCardsCheck.Enabled = customProfile;
        trustPublisherCheck.Enabled = customProfile;
    }

    private void UpdatePublisherTrustStatus()
    {
        bool installed = LocalRdpPublisherTrust.IsInstalled();
        bool artifacts = LocalRdpPublisherTrust.HasOwnedArtifacts();
        publisherTrustStatus.Text = installed
            ? "The exact app-owned certificate, private key, and SHA-256 trust entry " +
                "are installed for this Windows account."
            : artifacts
                ? "An incomplete app-owned publisher setup was found. Verified cleanup " +
                    "can be retried."
                : "No local RDP publisher certificate is installed. This option is not required.";
        removePublisherTrustButton.Enabled = artifacts;
    }

    private void RemovePublisherTrust(object sender, EventArgs eventArgs)
    {
        if (MessageBox.Show(this,
                "The app takes extra care to run a complete cleanup check and " +
                "remove only the exact app-owned certificate, its associated " +
                "private key, and its SHA-256 trust entry. Unrelated certificates, " +
                "keys, and publisher entries are left alone. Existing signed " +
                "profiles will show the Windows publisher warning again. Continue?",
                AppPaths.ProductName, MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) !=
            DialogResult.Yes)
            return;

        try
        {
            LocalRdpPublisherTrust.Remove();
            trustPublisherCheck.Checked = false;
            UpdatePublisherTrustStatus();
            MessageBox.Show(this,
                "Removal was verified for the exact app-owned certificate, its " +
                "private key, and its SHA-256 trust entry. Unrelated certificates, " +
                "keys, and publisher entries were left alone.",
                AppPaths.ProductName, MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this,
                "Cleanup was not fully verified and can be retried. The app-owned " +
                "identity record was retained; unrelated certificates, keys, and " +
                "publisher entries were left alone.\r\n\r\n" + exception.Message,
                AppPaths.ProductName, MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
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
        }
    }

    private void RdpFileTextChanged(object sender, EventArgs eventArgs)
    {
        string path = rdpFileText.Text.Trim();
        bool hasRdpFile = path.Length > 0;
        computerText.ReadOnly = false;
        UpdateCustomOptionState();

        if (!hasRdpFile || !File.Exists(path))
            return;

        string address = TryReadRdpTarget(path);
        if (SettingsStore.IsValidComputerName(address))
        {
            computerText.Text = SettingsStore.NormalizeComputerName(address);
            computerText.ReadOnly = true;
        }
    }

    internal static string TryReadRdpTarget(string path)
    {
        return RdpFileTargetReader.TryReadTarget(path);
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
        if (updateController.IsBusy)
        {
            MessageBox.Show(this,
                "Wait for the current update check or download to finish, then " +
                "create the connection.",
                AppPaths.ProductName, MessageBoxButtons.OK,
                MessageBoxIcon.Information);
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

        string computer;
        if (selectedRdpFile.Length > 0)
        {
            computer = SettingsStore.NormalizeComputerName(
                TryReadRdpTarget(selectedRdpFile));
            if (!SettingsStore.IsValidComputerName(computer))
            {
                MessageBox.Show(this,
                    "The selected .rdp file does not contain a valid " +
                    "alternate full address or full address.",
                    AppPaths.ProductName, MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                rdpFileText.Focus();
                rdpFileText.SelectAll();
                return;
            }
            computerText.Text = computer;
            computerText.ReadOnly = true;
            if (!reminderManuallyEdited)
                SetReminderText("REMOTE SESSION - " + computer.ToUpperInvariant());
        }
        else
        {
            computer = SettingsStore.NormalizeComputerName(computerText.Text);
            if (!SettingsStore.IsValidComputerName(computer))
            {
                MessageBox.Show(this,
                    "Enter a valid computer name, DNS name, IP address, or optional port.",
                    AppPaths.ProductName, MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                computerText.Focus();
                computerText.SelectAll();
                return;
            }
        }

        string profileId = Guid.NewGuid().ToString("N");
        string profileDirectory = AppPaths.GetProfileDirectory(profileId);
        ResolutionChoice selectedResolution =
            resolutionCombo.SelectedItem as ResolutionChoice;
        if (selectedRdpFile.Length == 0 && selectedResolution == null)
        {
            MessageBox.Show(this, "Choose a Remote Desktop resolution.",
                AppPaths.ProductName, MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }
        if (selectedRdpFile.Length == 0 &&
            LocalRdpPublisherTrust.HasLocationSigningConflict(
                trustPublisherCheck.Checked, redirectLocationCheck.Checked))
        {
            MessageBox.Show(this,
                LocalRdpPublisherTrust.LocationSigningConflictMessage,
                AppPaths.ProductName, MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            redirectLocationCheck.Focus();
            return;
        }

        ReminderSettings settings = new ReminderSettings();
        settings.ComputerName = computer;
        settings.FullScreen = fullScreenCheck.Checked;
        settings.ShortcutName = SettingsStore.NormalizeShortcutName(
            shortcutText.Text, computer);
        settings.ReminderText = SettingsStore.NormalizeReminderText(
            reminderText.Text, computer);
        DisplayChoice selectedDisplay = displayCombo.SelectedItem as DisplayChoice;
        settings.DisplayDevice = selectedDisplay == null ? "" : selectedDisplay.DeviceName;
        settings.RdpFile = AppPaths.GetProfileConnectionPath(profileId);

        SetupOperationRequest request = new SetupOperationRequest();
        request.ProfileId = profileId;
        request.ProfileDirectory = profileDirectory;
        request.SelectedRdpFile = selectedRdpFile;
        request.Settings = settings;
        request.ShortcutNameWasAutomatic = !shortcutManuallyEdited;
        request.ReminderWasAutomatic = !reminderManuallyEdited;
        request.TrustPublisher = selectedRdpFile.Length == 0 &&
            trustPublisherCheck.Checked;
        if (selectedRdpFile.Length == 0)
        {
            request.ProfileOptions = new RdpProfileOptions(
                computer, fullScreenCheck.Checked,
                selectedResolution.Width, selectedResolution.Height,
                allMonitorsCheck.Checked, alwaysAskCredentialsCheck.Checked,
                redirectClipboardCheck.Checked, redirectDrivesCheck.Checked,
                redirectLocationCheck.Checked, redirectComPortsCheck.Checked,
                redirectWebAuthnCheck.Checked,
                redirectSmartCardsCheck.Checked);
        }
        BeginSetupOperation(request);
    }

    private void BeginSetupOperation(SetupOperationRequest request)
    {
        operationRunning = true;
        tabs.Enabled = false;
        connectButton.Enabled = false;
        cancelButton.Enabled = false;
        UseWaitCursor = true;
        operationStatus.Text = "Preparing the connection...";
        operationStatus.Visible = true;
        operationProgress.Value = 0;
        operationProgress.Visible = true;

        BackgroundWorker worker = new BackgroundWorker();
        worker.WorkerReportsProgress = true;
        worker.ProgressChanged += delegate(object sender, ProgressChangedEventArgs args)
        {
            int value = Math.Max(operationProgress.Minimum,
                Math.Min(operationProgress.Maximum, args.ProgressPercentage));
            operationProgress.Value = value;
            operationStatus.Text = args.UserState as string ?? "Working...";
        };
        worker.DoWork += delegate(object sender, DoWorkEventArgs args)
        {
            args.Result = ExecuteSetupOperation(request,
                (BackgroundWorker)sender);
        };
        worker.RunWorkerCompleted += delegate(object sender,
            RunWorkerCompletedEventArgs args)
        {
            SetupOperationResult result = args.Error == null
                ? args.Result as SetupOperationResult
                : new SetupOperationResult { Failure = args.Error };
            if (result == null)
                result = new SetupOperationResult {
                    Failure = new InvalidOperationException(
                        "Setup ended without reporting a result.")
                };

            if (result.Failure != null)
            {
                operationRunning = false;
                tabs.Enabled = true;
                connectButton.Enabled = true;
                cancelButton.Enabled = true;
                UseWaitCursor = false;
                operationStatus.Text = "Setup stopped. Review the error and try again.";
                operationProgress.Value = 0;
                string message = result.ShortcutCompleted
                    ? "The shortcut was created, but the connection could not be " +
                        "started. Open the new desktop shortcut to try again."
                    : "Setup could not be completed.";
                MessageBox.Show(this,
                    message + "\r\n\r\n" + result.Failure.Message,
                    AppPaths.ProductName, MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            operationStatus.Text = "Completed. Remote Desktop is starting...";
            operationProgress.Value = operationProgress.Maximum;
            UseWaitCursor = false;
            System.Windows.Forms.Timer closeTimer =
                new System.Windows.Forms.Timer();
            closeTimer.Interval = 500;
            closeTimer.Tick += delegate
            {
                closeTimer.Stop();
                closeTimer.Dispose();
                allowClose = true;
                DialogResult = DialogResult.OK;
                Close();
            };
            closeTimer.Start();
        };
        worker.RunWorkerAsync();
    }

    private static SetupOperationResult ExecuteSetupOperation(
        SetupOperationRequest request, BackgroundWorker worker)
    {
        SetupOperationResult result = new SetupOperationResult();
        string reservedShortcutPath = "";
        try
        {
            worker.ReportProgress(1, "Installing application files...");
            InstallApplicationFiles();

            worker.ReportProgress(2, request.SelectedRdpFile.Length > 0
                ? "Copying and verifying the selected RDP profile..."
                : "Writing the Remote Desktop profile...");
            Directory.CreateDirectory(request.ProfileDirectory);
            string profileRdpFile = AppPaths.GetProfileConnectionPath(
                request.ProfileId);
            if (request.SelectedRdpFile.Length > 0)
                CopyRdpFileVerified(request.SelectedRdpFile, profileRdpFile);
            else
                RdpProfileWriter.Write(profileRdpFile, request.ProfileOptions);

            if (request.TrustPublisher)
            {
                worker.ReportProgress(3,
                    "Creating or reusing publisher trust and signing the profile...");
                LocalRdpPublisherTrust.EnsureInstalledAndSign(profileRdpFile);
            }
            else
            {
                worker.ReportProgress(3,
                    "Publisher signing was not selected; continuing...");
            }

            string copiedTarget = SettingsStore.NormalizeComputerName(
                TryReadRdpTarget(profileRdpFile));
            if (!SettingsStore.IsValidComputerName(copiedTarget))
                throw new InvalidDataException(
                    "The profile .rdp file does not contain a valid " +
                    "alternate full address or full address.");

            request.Settings.ComputerName = copiedTarget;
            if (request.ShortcutNameWasAutomatic)
                request.Settings.ShortcutName = SettingsStore.NormalizeShortcutName(
                    "Remote - " + copiedTarget, copiedTarget);
            if (request.ReminderWasAutomatic)
                request.Settings.ReminderText = SettingsStore.NormalizeReminderText(
                    "REMOTE SESSION - " + copiedTarget.ToUpperInvariant(),
                    copiedTarget);

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
                        desktop, request.Settings.ShortcutName);
                    request.Settings.ShortcutName =
                        Path.GetFileNameWithoutExtension(reservedShortcutPath);

                    worker.ReportProgress(4, "Saving the reminder settings...");
                    SettingsStore.SaveTo(
                        AppPaths.GetProfileSettingsPath(request.ProfileId),
                        request.Settings);

                    worker.ReportProgress(5, "Creating the desktop shortcut...");
                    ShortcutWriter.Create(reservedShortcutPath,
                        AppPaths.RuntimePath, "", "--profile " + request.ProfileId);
                    result.ShortcutCompleted = true;
                }
                finally
                {
                    if (ownsShortcutMutex)
                        shortcutMutex.ReleaseMutex();
                }
            }

            ShellRefresh.NotifyItem(reservedShortcutPath);
            ShellRefresh.NotifyDirectory(desktop);

            worker.ReportProgress(6, "Starting Remote Desktop...");
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = AppPaths.RuntimePath;
            startInfo.Arguments = "--profile " + request.ProfileId;
            startInfo.UseShellExecute = true;
            Process.Start(startInfo);
            worker.ReportProgress(7, "Completed. Remote Desktop is starting...");
        }
        catch (Exception exception)
        {
            CleanupFailedProfile(request.ProfileDirectory,
                reservedShortcutPath, result.ShortcutCompleted);
            result.Failure = exception;
        }
        return result;
    }

    private void SetupFormClosing(object sender, FormClosingEventArgs eventArgs)
    {
        if (!operationRunning || allowClose)
            return;
        eventArgs.Cancel = true;
        operationStatus.Text =
            "Please wait for the current connection setup step to finish.";
    }

    private sealed class SetupOperationRequest
    {
        public string ProfileId;
        public string ProfileDirectory;
        public string SelectedRdpFile;
        public ReminderSettings Settings;
        public RdpProfileOptions ProfileOptions;
        public bool ShortcutNameWasAutomatic;
        public bool ReminderWasAutomatic;
        public bool TrustPublisher;
    }

    private sealed class SetupOperationResult
    {
        public bool ShortcutCompleted;
        public Exception Failure;
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
        if (!File.Exists(sourceRuntime))
            throw new FileNotFoundException(
                "The runtime executable is missing. Extract and run setup from " +
                "the complete release package.", sourceRuntime);

        CopyUnlessSame(sourceRuntime, AppPaths.RuntimePath);
        CopyUnlessSame(currentSetup, AppPaths.SetupPath);
        DeleteStaleUninstallNote(AppPaths.InstallDirectory);
    }

    internal static void DeleteStaleUninstallNote(string installDirectory)
    {
        if (string.IsNullOrEmpty(installDirectory))
            throw new ArgumentException("An install directory is required.");
        string note = Path.Combine(Path.GetFullPath(installDirectory),
            "README - Saved RDP Connections.txt");
        if (File.Exists(note))
            File.Delete(note);
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
        return ReadStringProperty(shortcutPath, "Arguments");
    }

    public static string ReadWorkingDirectory(string shortcutPath)
    {
        return ReadStringProperty(shortcutPath, "WorkingDirectory");
    }

    public static string ReadTargetPath(string shortcutPath)
    {
        return ReadStringProperty(shortcutPath, "TargetPath");
    }

    private static string ReadStringProperty(string shortcutPath,
        string propertyName)
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
            return (string)shortcut.GetType().InvokeMember(propertyName,
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

internal sealed class ResolutionChoice
{
    public readonly int Width;
    public readonly int Height;
    private readonly string label;

    public ResolutionChoice(int width, int height, string suffix)
    {
        Width = width;
        Height = height;
        label = width.ToString() + " x " + height.ToString() + suffix;
    }

    public static ResolutionChoice[] ForPrimaryDisplay()
    {
        Screen primary = Screen.PrimaryScreen;
        int nativeWidth = primary == null ? 1920 : primary.Bounds.Width;
        int nativeHeight = primary == null ? 1080 : primary.Bounds.Height;
        List<ResolutionChoice> choices = new List<ResolutionChoice>();
        choices.Add(new ResolutionChoice(nativeWidth, nativeHeight,
            " (Native/current, recommended)"));

        int[,] presets = new int[,] {
            { 1920, 1080 },
            { 1680, 1050 },
            { 1600, 900 },
            { 1440, 900 },
            { 1366, 768 },
            { 1280, 1024 },
            { 1280, 800 },
            { 1280, 720 },
            { 1024, 768 },
            { 800, 600 }
        };
        for (int index = 0; index < presets.GetLength(0); index++)
        {
            int width = presets[index, 0];
            int height = presets[index, 1];
            if ((width == nativeWidth && height == nativeHeight) ||
                width > nativeWidth || height > nativeHeight)
                continue;
            choices.Add(new ResolutionChoice(width, height, ""));
        }
        return choices.ToArray();
    }

    public override string ToString()
    {
        return label;
    }
}

internal static class LocalRdpPublisherTrust
{
    public const string LocationSigningConflictMessage =
        "Windows cannot include Location redirection in an RDP file's " +
        "publisher signature on this computer. Turn off either Location or " +
        "local publisher trust, then try again.";

    private const string PublisherSubjectPrefix =
        "CN=RDP Session Reminder Local RDP Publisher ";
    private const string PublisherFriendlyNamePrefix =
        "RDP Session Reminder Local RDP Publisher ";
    private const string PrivateKeyContainerPrefix =
        "RdpSessionReminder-LocalPublisher-";
    private const string PrivateKeyProvider =
        "Microsoft Software Key Storage Provider";
    private const string IdentityFileName = "publisher-trust.ini";
    private const string PolicyPath =
        @"Software\Policies\Microsoft\Windows NT\Terminal Services";
    private const string PolicyValue = "TrustedCertThumbprints";
    private const string PolicyMutexName =
        "RdpSessionReminder-PublisherTrustPolicy";
    private const string OwnershipMutexName =
        "RdpSessionReminder-PublisherCertificate";

    private static string IdentityPath
    {
        get { return Path.Combine(AppPaths.InstallDirectory, IdentityFileName); }
    }

    public static bool HasLocationSigningConflict(bool trustPublisher,
        bool redirectLocation)
    {
        return trustPublisher && redirectLocation;
    }

    public static bool IsInstalled()
    {
        try
        {
            TrustIdentity identity;
            if (!TryLoadIdentity(out identity) || identity.Sha256.Length != 64)
                return false;
            X509Certificate2 certificate = FindOwnedCertificate(identity, true);
            if (certificate == null ||
                certificate.NotBefore > DateTime.Now.AddMinutes(5) ||
                certificate.NotAfter <= DateTime.Now)
                return false;
            RequireExpectedPrivateKey(CapturePrivateKeyDescriptor(certificate),
                GetExpectedPrivateKeyDescriptor(identity));
            return PolicyContains("sha256:" + identity.Sha256);
        }
        catch
        {
            return false;
        }
    }

    public static bool HasOwnedArtifacts()
    {
        return File.Exists(IdentityPath);
    }

    public static void EnsureInstalledAndSign(string rdpPath)
    {
        WithNamedMutex(OwnershipMutexName, delegate
        {
            EnsureInstalledAndSignCore(rdpPath);
        });
    }

    private static void EnsureInstalledAndSignCore(string rdpPath)
    {
        if (string.IsNullOrEmpty(rdpPath) || !File.Exists(rdpPath))
            throw new FileNotFoundException(
                "The generated RDP profile is missing.", rdpPath);

        TrustIdentity identity;
        if (!TryLoadIdentity(out identity))
        {
            identity = new TrustIdentity();
            identity.Id = Guid.NewGuid().ToString("N");
            identity.Sha256 = "";
            SaveIdentity(identity);
        }

        X509Certificate2 certificate = FindOwnedCertificate(identity, true);
        if (certificate != null &&
            (certificate.NotBefore > DateTime.Now.AddMinutes(5) ||
             certificate.NotAfter <= DateTime.Now))
        {
            throw new InvalidOperationException(
                "The app-owned RDP publisher certificate is not currently valid. " +
                "Remove the local publisher trust explicitly, then create a new " +
                "signed shortcut. Existing signed profiles are left unchanged.");
        }

        if (certificate == null)
        {
            if (identity.Sha256.Length == 64)
                throw new InvalidOperationException(
                    "The app-owned RDP publisher certificate is missing. Remove the " +
                    "incomplete local publisher trust explicitly, then try again.");
            identity.Sha256 = CreateCertificate(identity);
            SaveIdentity(identity);
            certificate = FindOwnedCertificate(identity, true);
        }
        else if (identity.Sha256.Length != 64)
        {
            identity.Sha256 = GetSha256Hex(certificate);
            SaveIdentity(identity);
        }

        if (certificate == null || !certificate.HasPrivateKey ||
            !string.Equals(GetSha256Hex(certificate), identity.Sha256,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "The local RDP publisher certificate could not be created.");
        RequireExpectedPrivateKey(CapturePrivateKeyDescriptor(certificate),
            GetExpectedPrivateKeyDescriptor(identity));

        string sha256Identifier = "sha256:" + identity.Sha256;
        SignRdpFile(rdpPath, certificate.Thumbprint);
        AddPolicyIdentifier(sha256Identifier);
    }

    public static void Remove()
    {
        WithNamedMutex(OwnershipMutexName, RemoveCore);
    }

    private static void RemoveCore()
    {
        TrustIdentity identity;
        if (!File.Exists(IdentityPath))
            return;
        if (!TryLoadIdentity(out identity))
            throw new InvalidDataException(
                "The app-owned publisher identity record is missing or corrupt, so " +
                "the certificate cannot be identified safely for removal.");

        List<OwnedCertificateDescriptor> certificates =
            CollectValidatedOwnedCertificates(identity);
        List<string> identifiers = new List<string>();
        bool identityHashProven = identity.Sha256.Length == 0;
        foreach (OwnedCertificateDescriptor certificate in certificates)
        {
            AddUnique(identifiers, "sha256:" + certificate.Sha256);
            if (identity.Sha256.Length == 64 &&
                string.Equals(identity.Sha256, certificate.Sha256,
                    StringComparison.OrdinalIgnoreCase))
                identityHashProven = true;
        }
        if (!identityHashProven &&
            PolicyContains("sha256:" + identity.Sha256))
            throw new InvalidDataException(
                "The publisher identity SHA-256 entry cannot be proven from a " +
                "validated app-owned certificate, so cleanup stopped without " +
                "changing publisher policy.");
        RemovePolicyIdentifiers(identifiers);
        foreach (string identifier in identifiers)
        {
            if (PolicyContains(identifier))
                throw new InvalidOperationException(
                    "The app-owned SHA-256 publisher trust removal was not verified. " +
                    "The cleanup can be retried.");
        }
        RemoveOwnedCertificates(identity, certificates);
        if (HasAnyOwnedCertificate(identity))
            throw new InvalidOperationException(
                "The app-owned certificate and private-key cleanup was not verified. " +
                "The cleanup can be retried.");
        if (File.Exists(IdentityPath))
            File.Delete(IdentityPath);
    }

    private static X509Certificate2 FindOwnedCertificate(TrustIdentity identity,
        bool requirePrivateKey)
    {
        using (X509Store store = new X509Store(StoreName.My,
            StoreLocation.CurrentUser))
        {
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            foreach (X509Certificate2 certificate in store.Certificates)
            {
                if (!IsOwnedCertificate(certificate, identity) ||
                    (requirePrivateKey && !certificate.HasPrivateKey))
                    continue;
                if (identity.Sha256.Length == 64 &&
                    !string.Equals(GetSha256Hex(certificate), identity.Sha256,
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                return new X509Certificate2(certificate);
            }
        }
        return null;
    }

    private static bool IsOwnedCertificate(X509Certificate2 certificate,
        TrustIdentity identity)
    {
        string subject = PublisherSubjectPrefix + identity.Id;
        string friendlyName = PublisherFriendlyNamePrefix + identity.Id;
        if (certificate == null ||
            !string.Equals(certificate.Subject, subject,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(certificate.Issuer, subject,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(certificate.FriendlyName, friendlyName,
                StringComparison.Ordinal))
            return false;

        foreach (X509Extension extension in certificate.Extensions)
        {
            X509EnhancedKeyUsageExtension usages =
                extension as X509EnhancedKeyUsageExtension;
            if (usages == null)
                continue;
            foreach (Oid usage in usages.EnhancedKeyUsages)
            {
                if (usage.Value == "1.3.6.1.5.5.7.3.3")
                    return true;
            }
        }
        return false;
    }

    private static bool HasAnyOwnedCertificate(TrustIdentity identity)
    {
        using (X509Store store = new X509Store(StoreName.My,
            StoreLocation.CurrentUser))
        {
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            foreach (X509Certificate2 certificate in store.Certificates)
            {
                if (IsOwnedCertificate(certificate, identity))
                    return true;
            }
        }
        return false;
    }

    private static string CreateCertificate(TrustIdentity identity)
    {
        PrivateKeyDescriptor expectedPrivateKey =
            GetExpectedPrivateKeyDescriptor(identity);
        CngProvider provider = new CngProvider(expectedPrivateKey.ProviderName);
        if (CngKey.Exists(expectedPrivateKey.KeyName, provider,
                CngKeyOpenOptions.None))
            throw new InvalidOperationException(
                "The exact app-owned private-key container already exists without " +
                "a usable publisher certificate. Remove and verify the incomplete " +
                "local publisher setup, then try again.");
        string subject = PublisherSubjectPrefix + identity.Id;
        string friendlyName = PublisherFriendlyNamePrefix + identity.Id;
        string script =
            "$ErrorActionPreference='Stop'\r\n" +
            "if (-not (Get-PSDrive -Name Cert -PSProvider Certificate " +
            "-ErrorAction SilentlyContinue)) {" +
            "New-PSDrive -Name Cert -PSProvider Certificate -Root '\\' " +
            "-ErrorAction Stop|Out-Null}\r\n" +
            "if (-not (Get-PSDrive -Name Cert -PSProvider Certificate " +
            "-ErrorAction SilentlyContinue)) {throw 'Certificate provider unavailable.'}\r\n" +
            "$certificate=New-SelfSignedCertificate " +
            "-Type CodeSigningCert " +
            "-Subject '" + subject + "' " +
            "-FriendlyName '" + friendlyName + "' " +
            "-CertStoreLocation 'Cert:\\CurrentUser\\My' " +
            "-Provider '" + PrivateKeyProvider + "' " +
            "-Container '" + expectedPrivateKey.KeyName + "' " +
            "-KeyAlgorithm RSA -KeyLength 2048 -HashAlgorithm SHA256 " +
            "-KeyExportPolicy NonExportable -NotAfter (Get-Date).AddYears(3)\r\n" +
            "$hasher=[Security.Cryptography.SHA256]::Create()\r\n" +
            "try {$hash=$hasher.ComputeHash($certificate.RawData)} " +
            "finally {$hasher.Dispose()}\r\n" +
            "($hash|ForEach-Object {$_.ToString('X2')}) -join ''\r\n";
        string encoded = Convert.ToBase64String(
            Encoding.Unicode.GetBytes(script));
        ProcessResult result = RunProcess(GetWindowsPowerShellPath(),
            "-NoLogo -NoProfile -NonInteractive -EncodedCommand " + encoded,
            60000, true);
        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                "Windows could not create the local RDP publisher certificate.\r\n" +
                result.Error.Trim());

        string[] outputLines = result.Output.Split(
            new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        for (int index = outputLines.Length - 1; index >= 0; index--)
        {
            string value = NormalizeHex(outputLines[index]);
            if (value.Length == 64)
                return value;
        }
        throw new InvalidOperationException(
            "Windows created a certificate but did not return its SHA-256 identifier.");
    }

    private static string GetWindowsPowerShellPath()
    {
        string windowsDirectory = Environment.GetFolderPath(
            Environment.SpecialFolder.Windows);
        string systemFolder = Environment.Is64BitOperatingSystem &&
            !Environment.Is64BitProcess ? "Sysnative" : "System32";
        string powershell = Path.Combine(windowsDirectory, systemFolder,
            @"WindowsPowerShell\v1.0\powershell.exe");
        if (!File.Exists(powershell))
            throw new FileNotFoundException(
                "Windows PowerShell was not found at its trusted system path.",
                powershell);
        return powershell;
    }

    private static void SignRdpFile(string path, string storeThumbprint)
    {
        string rdpsign = Path.Combine(Environment.SystemDirectory, "rdpsign.exe");
        if (!File.Exists(rdpsign))
            throw new FileNotFoundException(
                "Windows RDP signing tool (rdpsign.exe) was not found.", rdpsign);

        string certificateSelector = NormalizeHex(storeThumbprint);
        if (certificateSelector.Length != 40)
            throw new InvalidDataException(
                "Windows did not provide a valid certificate-store selector.");
        ProcessResult result = RunProcess(rdpsign,
            "/sha256 " + certificateSelector + " /q " + QuoteArgument(path),
            60000, false);
        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                "Windows could not sign the generated RDP file.\r\n" +
                (result.Error + "\r\n" + result.Output).Trim());

        bool hasSignature = false;
        string signScope = "";
        string[] signedLines = File.ReadAllLines(path);
        foreach (string line in signedLines)
        {
            if (line.StartsWith("signature:s:",
                    StringComparison.OrdinalIgnoreCase))
                hasSignature = true;
            else if (line.StartsWith("signscope:s:",
                    StringComparison.OrdinalIgnoreCase))
                signScope = line.Substring("signscope:s:".Length);
        }
        if (!hasSignature)
            throw new InvalidDataException(
                "Windows reported success but the generated RDP file is not signed.");
        if (signScope.Length == 0)
            throw new InvalidDataException(
                "Windows signed the RDP file without reporting its protected settings.");

        RequireSignedSetting(signedLines, "full address:s:",
            "Full Address", signScope);
        RequireSignedSetting(signedLines, "alternate full address:s:",
            "Alternate Full Address", signScope);
        RequireSignedSetting(signedLines, "enablecredsspsupport:i:",
            "EnableCredSspSupport", signScope);
        RequireSignedSetting(signedLines, "redirectclipboard:i:",
            "RedirectClipboard", signScope);
        RequireSignedSetting(signedLines, "drivestoredirect:s:",
            "DrivesToRedirect", signScope);
        RequireSignedSetting(signedLines, "redirectcomports:i:",
            "RedirectCOMPorts", signScope);
        RequireSignedSetting(signedLines, "redirectsmartcards:i:",
            "RedirectSmartCards", signScope);
        RequireSignedSetting(signedLines, "redirectwebauthn:i:",
            "RedirectWebAuthn", signScope);
        RequireSignedSetting(signedLines, "redirectlocation:i:1",
            "RedirectLocation", signScope);
    }

    private static void RequireSignedSetting(string[] lines, string settingPrefix,
        string scopeName, string signScope)
    {
        bool settingPresent = false;
        foreach (string line in lines)
        {
            if (line.StartsWith(settingPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                settingPresent = true;
                break;
            }
        }
        if (!settingPresent)
            return;

        foreach (string entry in signScope.Split(','))
        {
            if (string.Equals(entry.Trim(), scopeName,
                    StringComparison.OrdinalIgnoreCase))
                return;
        }
        throw new InvalidDataException(
            "Windows did not include the RDP setting '" + scopeName +
            "' in the file signature. Turn off that resource or local publisher " +
            "trust and create the shortcut again.");
    }

    private static void AddPolicyIdentifier(string sha256Identifier)
    {
        WithPolicyMutex(delegate
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(PolicyPath))
            {
                if (key == null)
                    throw new InvalidOperationException(
                        "Windows could not open the per-user RDP publisher trust policy.");
                List<string> entries = ReadPolicyEntries(key);
                AddUnique(entries, sha256Identifier);
                key.SetValue(PolicyValue, string.Join(",", entries.ToArray()),
                    RegistryValueKind.String);
            }
        });
    }

    private static bool PolicyContains(string identifier)
    {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(PolicyPath, false))
        {
            if (key == null)
                return false;
            foreach (string entry in ReadPolicyEntries(key))
            {
                if (string.Equals(entry, identifier,
                        StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }

    private static List<string> ReadPolicyEntries(RegistryKey key)
    {
        List<string> entries = new List<string>();
        string value = key.GetValue(PolicyValue, "") as string;
        if (string.IsNullOrEmpty(value))
            return entries;
        foreach (string part in value.Split(','))
        {
            string entry = part.Trim();
            if (entry.Length > 0)
                AddUnique(entries, entry);
        }
        return entries;
    }

    private static void AddUnique(List<string> values, string value)
    {
        foreach (string existing in values)
        {
            if (string.Equals(existing, value, StringComparison.OrdinalIgnoreCase))
                return;
        }
        values.Add(value);
    }

    private static List<OwnedCertificateDescriptor>
        CollectValidatedOwnedCertificates(TrustIdentity identity)
    {
        PrivateKeyDescriptor expectedPrivateKey =
            GetExpectedPrivateKeyDescriptor(identity);
        List<OwnedCertificateDescriptor> matches =
            new List<OwnedCertificateDescriptor>();
        using (X509Store store = new X509Store(StoreName.My,
            StoreLocation.CurrentUser))
        {
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            foreach (X509Certificate2 certificate in store.Certificates)
            {
                if (!IsOwnedCertificate(certificate, identity))
                    continue;
                string rawThumbprint = certificate.Thumbprint;
                string sha256 = GetSha256Hex(certificate);
                if (!IsExactHex(rawThumbprint, 40) ||
                    !IsExactHex(sha256, 64) || !certificate.HasPrivateKey)
                    throw new InvalidDataException(
                        "An app-marker certificate does not have the exact " +
                        "validated certificate and private-key identity required " +
                        "for automatic cleanup.");
                OwnedCertificateDescriptor descriptor =
                    new OwnedCertificateDescriptor();
                descriptor.StoreThumbprint = rawThumbprint.ToUpperInvariant();
                descriptor.Sha256 = sha256.ToUpperInvariant();
                descriptor.PrivateKey = CapturePrivateKeyDescriptor(certificate);
                RequireExpectedPrivateKey(
                    descriptor.PrivateKey, expectedPrivateKey);
                matches.Add(descriptor);
            }
        }
        return matches;
    }

    private static void RemoveOwnedCertificates(TrustIdentity identity,
        List<OwnedCertificateDescriptor> matches)
    {
        PrivateKeyDescriptor expectedPrivateKey =
            GetExpectedPrivateKeyDescriptor(identity);
        bool requestedKeyDeletion = false;
        foreach (OwnedCertificateDescriptor certificate in matches)
        {
            PrivateKeyDescriptor keyForCertificate =
                certificate.PrivateKey.Kind != PrivateKeyKind.None &&
                !requestedKeyDeletion
                    ? expectedPrivateKey
                    : new PrivateKeyDescriptor { Kind = PrivateKeyKind.None };
            DeleteOwnedCertificateAndKey(
                certificate.StoreThumbprint, certificate.Sha256,
                keyForCertificate);
            if (keyForCertificate.Kind != PrivateKeyKind.None)
                requestedKeyDeletion = true;
        }
        RemoveExactPrivateKeyIfPresent(expectedPrivateKey);
        VerifyPrivateKeyRemoved(expectedPrivateKey);
    }

    private static void DeleteOwnedCertificateAndKey(string storeThumbprint,
        string expectedSha256, PrivateKeyDescriptor privateKey)
    {
        if (!IsExactHex(storeThumbprint, 40) ||
            !IsExactHex(expectedSha256, 64))
            throw new InvalidDataException(
                "The app-owned certificate identifiers are invalid.");
        string thumbprint = storeThumbprint.ToUpperInvariant();
        string sha256 = expectedSha256.ToUpperInvariant();
        if (privateKey == null)
            throw new InvalidDataException(
                "The app-owned private-key descriptor is missing.");
        string deleteKeySwitch = privateKey.Kind == PrivateKeyKind.None
            ? ""
            : " -DeleteKey";

        string script =
            "$ErrorActionPreference='Stop'\r\n" +
            "if (-not (Get-PSDrive -Name Cert -PSProvider Certificate " +
            "-ErrorAction SilentlyContinue)) {" +
            "New-PSDrive -Name Cert -PSProvider Certificate -Root '\\' " +
            "-ErrorAction Stop|Out-Null}\r\n" +
            "if (-not (Get-PSDrive -Name Cert -PSProvider Certificate " +
            "-ErrorAction SilentlyContinue)) {throw 'Certificate provider unavailable.'}\r\n" +
            "$path='Cert:\\CurrentUser\\My\\" + thumbprint + "'\r\n" +
            "$certificate=Get-Item -LiteralPath $path -ErrorAction Stop\r\n" +
            "$hasher=[Security.Cryptography.SHA256]::Create()\r\n" +
            "try {$hash=$hasher.ComputeHash($certificate.RawData)} " +
            "finally {$hasher.Dispose()}\r\n" +
            "$actual=($hash|ForEach-Object {$_.ToString('X2')}) -join ''\r\n" +
            "if ($actual -ne '" + sha256 +
            "') {throw 'Certificate identity changed before removal.'}\r\n" +
            "Remove-Item -Path $path" + deleteKeySwitch +
            " -Force -Confirm:$false " +
            "-ErrorAction Stop\r\n" +
            "if (Test-Path -LiteralPath $path) {" +
            "throw 'Certificate removal could not be verified.'}\r\n";
        string encoded = Convert.ToBase64String(
            Encoding.Unicode.GetBytes(script));
        ProcessResult result = RunProcess(GetWindowsPowerShellPath(),
            "-NoLogo -NoProfile -NonInteractive -EncodedCommand " + encoded,
            60000, true);
        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                "Windows could not remove the app-owned RDP publisher certificate " +
                "and its private key.\r\n" +
                (result.Error + "\r\n" + result.Output).Trim());

        using (X509Store store = new X509Store(StoreName.My,
            StoreLocation.CurrentUser))
        {
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            X509Certificate2Collection remaining = store.Certificates.Find(
                X509FindType.FindByThumbprint, thumbprint, false);
            if (remaining.Count != 0)
                throw new InvalidOperationException(
                    "The app-owned certificate removal could not be verified.");
        }
    }

    private static PrivateKeyDescriptor CapturePrivateKeyDescriptor(
        X509Certificate2 certificate)
    {
        PrivateKeyDescriptor descriptor = new PrivateKeyDescriptor();
        if (!certificate.HasPrivateKey)
        {
            descriptor.Kind = PrivateKeyKind.None;
            return descriptor;
        }

        AsymmetricAlgorithm key = null;
        try
        {
#pragma warning disable 618
            key = certificate.PrivateKey;
#pragma warning restore 618
            RSACng cng = key as RSACng;
            if (cng != null)
            {
                descriptor.Kind = PrivateKeyKind.Cng;
                descriptor.KeyName = cng.Key.KeyName;
                descriptor.ProviderName = cng.Key.Provider.Provider;
                descriptor.MachineKey = cng.Key.IsMachineKey;
                if (string.IsNullOrEmpty(descriptor.KeyName) ||
                    string.IsNullOrEmpty(descriptor.ProviderName))
                    throw new InvalidDataException(
                        "The app-owned CNG private-key identity is incomplete.");
                return descriptor;
            }

            RSACryptoServiceProvider csp =
                key as RSACryptoServiceProvider;
            if (csp != null)
            {
                CspKeyContainerInfo information = csp.CspKeyContainerInfo;
                descriptor.Kind = PrivateKeyKind.Csp;
                descriptor.KeyName = information.KeyContainerName;
                descriptor.ProviderName = information.ProviderName;
                descriptor.ProviderType = information.ProviderType;
                descriptor.KeyNumber = (int)information.KeyNumber;
                descriptor.MachineKey = information.MachineKeyStore;
                if (string.IsNullOrEmpty(descriptor.KeyName) ||
                    string.IsNullOrEmpty(descriptor.ProviderName))
                    throw new InvalidDataException(
                        "The app-owned CSP private-key identity is incomplete.");
                return descriptor;
            }

            throw new InvalidDataException(
                "The app-owned certificate uses an unsupported private-key provider.");
        }
        finally
        {
            if (key != null)
                key.Dispose();
        }
    }

    private static PrivateKeyDescriptor GetExpectedPrivateKeyDescriptor(
        TrustIdentity identity)
    {
        Guid parsed;
        if (identity == null ||
            !Guid.TryParseExact(identity.Id, "N", out parsed))
            throw new InvalidDataException(
                "The app-owned publisher identity is invalid.");
        PrivateKeyDescriptor descriptor = new PrivateKeyDescriptor();
        descriptor.Kind = PrivateKeyKind.Cng;
        descriptor.KeyName = PrivateKeyContainerPrefix +
            parsed.ToString("N");
        descriptor.ProviderName = PrivateKeyProvider;
        descriptor.MachineKey = false;
        ValidatePrivateKeyDescriptor(descriptor);
        return descriptor;
    }

    private static void RequireExpectedPrivateKey(
        PrivateKeyDescriptor actual, PrivateKeyDescriptor expected)
    {
        ValidatePrivateKeyDescriptor(actual);
        ValidatePrivateKeyDescriptor(expected);
        if (actual.Kind != PrivateKeyKind.Cng ||
            !string.Equals(actual.KeyName, expected.KeyName,
                StringComparison.Ordinal) ||
            !string.Equals(actual.ProviderName, expected.ProviderName,
                StringComparison.Ordinal) ||
            actual.MachineKey)
            throw new InvalidDataException(
                "The app-owned certificate is not bound to the expected exact " +
                "per-user private-key container, so cleanup stopped safely.");
    }

    private static void VerifyPrivateKeyRemoved(PrivateKeyDescriptor descriptor)
    {
        if (descriptor.Kind == PrivateKeyKind.None)
            return;
        if (descriptor.Kind == PrivateKeyKind.Cng)
        {
            if (!CngKey.Exists(descriptor.KeyName,
                    new CngProvider(descriptor.ProviderName),
                    GetCngOpenOptions(descriptor)))
                return;
            throw new InvalidOperationException(
                "The app-owned CNG private-key removal was not verified. " +
                "The cleanup can be retried.");
        }
        if (descriptor.Kind == PrivateKeyKind.Csp)
        {
            if (!CspKeyExists(descriptor))
                return;
            throw new InvalidOperationException(
                "The app-owned CSP private-key removal was not verified. " +
                "The cleanup can be retried.");
        }
        throw new InvalidDataException(
            "The app-owned private-key provider is invalid.");
    }

    private static void RemoveExactPrivateKeyIfPresent(
        PrivateKeyDescriptor descriptor)
    {
        if (descriptor == null || descriptor.Kind == PrivateKeyKind.None)
            return;
        ValidatePrivateKeyDescriptor(descriptor);
        if (descriptor.Kind == PrivateKeyKind.Cng)
        {
            CngProvider provider = new CngProvider(descriptor.ProviderName);
            CngKeyOpenOptions options = GetCngOpenOptions(descriptor);
            if (!CngKey.Exists(descriptor.KeyName, provider, options))
                return;
            using (CngKey key = CngKey.Open(
                descriptor.KeyName, provider, options))
                key.Delete();
            return;
        }
        if (descriptor.Kind == PrivateKeyKind.Csp)
        {
            RSACryptoServiceProvider key = OpenExistingCspKey(descriptor);
            if (key == null)
                return;
            using (key)
            {
                key.PersistKeyInCsp = false;
                key.Clear();
            }
            return;
        }
        throw new InvalidDataException(
            "The app-owned private-key provider is invalid.");
    }

    private static CngKeyOpenOptions GetCngOpenOptions(
        PrivateKeyDescriptor descriptor)
    {
        return descriptor.MachineKey
            ? CngKeyOpenOptions.MachineKey
            : CngKeyOpenOptions.None;
    }

    private static bool CspKeyExists(PrivateKeyDescriptor descriptor)
    {
        using (RSACryptoServiceProvider key = OpenExistingCspKey(descriptor))
            return key != null;
    }

    private static RSACryptoServiceProvider OpenExistingCspKey(
        PrivateKeyDescriptor descriptor)
    {
        CspParameters parameters = new CspParameters(
            descriptor.ProviderType, descriptor.ProviderName,
            descriptor.KeyName);
        parameters.KeyNumber = descriptor.KeyNumber;
        parameters.Flags = CspProviderFlags.UseExistingKey;
        if (descriptor.MachineKey)
            parameters.Flags |= CspProviderFlags.UseMachineKeyStore;
        try
        {
            return new RSACryptoServiceProvider(parameters);
        }
        catch (CryptographicException exception)
        {
            const int NteBadKeyset = unchecked((int)0x80090016);
            if (exception.HResult == NteBadKeyset)
                return null;
            throw;
        }
    }

    private static void RemovePolicyIdentifiers(List<string> identifiers)
    {
        WithPolicyMutex(delegate
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                PolicyPath, true))
            {
                if (key == null)
                    return;
                List<string> kept = new List<string>();
                foreach (string entry in ReadPolicyEntries(key))
                {
                    bool remove = false;
                    foreach (string identifier in identifiers)
                    {
                        if (string.Equals(entry, identifier,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            remove = true;
                            break;
                        }
                    }
                    if (!remove)
                        kept.Add(entry);
                }
                if (kept.Count == 0)
                    key.DeleteValue(PolicyValue, false);
                else
                    key.SetValue(PolicyValue, string.Join(",", kept.ToArray()),
                        RegistryValueKind.String);
            }
        });
    }

    private static bool TryLoadIdentity(out TrustIdentity identity)
    {
        identity = null;
        if (!File.Exists(IdentityPath))
            return false;

        string id = "";
        string sha256 = "";
        try
        {
            foreach (string line in File.ReadAllLines(IdentityPath, Encoding.UTF8))
            {
                int separator = line.IndexOf('=');
                if (separator <= 0)
                    continue;
                string key = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1).Trim();
                if (key.Equals("Id", StringComparison.OrdinalIgnoreCase))
                    id = value;
                else if (key.Equals("Sha256", StringComparison.OrdinalIgnoreCase))
                    sha256 = value;
            }
        }
        catch
        {
            return false;
        }

        Guid parsed;
        if (!Guid.TryParseExact(id, "N", out parsed))
            return false;
        string normalizedSha256 = NormalizeHex(sha256);
        if (sha256.Length != 64 || normalizedSha256.Length != 64)
            normalizedSha256 = "";

        identity = new TrustIdentity();
        identity.Id = parsed.ToString("N");
        identity.Sha256 = normalizedSha256;
        return true;
    }

    private static void SaveIdentity(TrustIdentity identity)
    {
        Guid parsed;
        if (identity == null ||
            !Guid.TryParseExact(identity.Id, "N", out parsed) ||
            (identity.Sha256.Length != 0 &&
             NormalizeHex(identity.Sha256).Length != 64))
            throw new InvalidDataException(
                "The local RDP publisher identity is invalid.");

        Directory.CreateDirectory(AppPaths.InstallDirectory);
        StringBuilder content = new StringBuilder();
        content.Append("Id=").Append(parsed.ToString("N"))
            .Append(Environment.NewLine)
            .Append("Sha256=").Append(NormalizeHex(identity.Sha256))
            .Append(Environment.NewLine);
        string temporary = IdentityPath + ".new";
        File.WriteAllText(temporary, content.ToString(), new UTF8Encoding(false));
        if (File.Exists(IdentityPath))
            File.Replace(temporary, IdentityPath, null);
        else
            File.Move(temporary, IdentityPath);
    }

    private static void WithPolicyMutex(Action action)
    {
        WithNamedMutex(PolicyMutexName, action);
    }

    private static void WithNamedMutex(string mutexName, Action action)
    {
        string user = WindowsIdentity.GetCurrent().User.Value.Replace('-', '_');
        string globalName = "Global\\" + mutexName + "-" + user;
        using (Mutex operationMutex = new Mutex(false, globalName))
        {
            bool ownsMutex = false;
            try
            {
                try
                {
                    ownsMutex = operationMutex.WaitOne(TimeSpan.FromSeconds(30));
                }
                catch (AbandonedMutexException)
                {
                    ownsMutex = true;
                }
                if (!ownsMutex)
                    throw new TimeoutException(
                        "Another setup window is updating RDP publisher trust. Try again.");
                action();
            }
            finally
            {
                if (ownsMutex)
                    operationMutex.ReleaseMutex();
            }
        }
    }

    private static string GetSha256Hex(X509Certificate2 certificate)
    {
        using (SHA256 algorithm = SHA256.Create())
            return ToHex(algorithm.ComputeHash(certificate.RawData));
    }

    private static string ToHex(byte[] bytes)
    {
        StringBuilder builder = new StringBuilder(bytes.Length * 2);
        foreach (byte value in bytes)
            builder.Append(value.ToString("X2"));
        return builder.ToString();
    }

    private static string NormalizeHex(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        StringBuilder builder = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            if ((character >= '0' && character <= '9') ||
                (character >= 'A' && character <= 'F') ||
                (character >= 'a' && character <= 'f'))
                builder.Append(char.ToUpperInvariant(character));
        }
        return builder.ToString();
    }

    private static bool IsExactHex(string value, int length)
    {
        if (string.IsNullOrEmpty(value) || value.Length != length)
            return false;
        foreach (char character in value)
        {
            if (!((character >= '0' && character <= '9') ||
                  (character >= 'A' && character <= 'F') ||
                  (character >= 'a' && character <= 'f')))
                return false;
        }
        return true;
    }

    private static void ValidatePrivateKeyDescriptor(
        PrivateKeyDescriptor descriptor)
    {
        if (descriptor == null ||
            (descriptor.Kind != PrivateKeyKind.Cng &&
             descriptor.Kind != PrivateKeyKind.Csp) ||
            string.IsNullOrEmpty(descriptor.KeyName) ||
            descriptor.KeyName.Length > 1024 ||
            string.IsNullOrEmpty(descriptor.ProviderName) ||
            descriptor.ProviderName.Length > 1024 ||
            descriptor.KeyName.IndexOf('\0') >= 0 ||
            descriptor.ProviderName.IndexOf('\0') >= 0)
            throw new InvalidDataException(
                "The app-owned private-key descriptor is invalid.");
        if (descriptor.Kind == PrivateKeyKind.Csp &&
            (descriptor.ProviderType <= 0 ||
             (descriptor.KeyNumber != (int)KeyNumber.Exchange &&
              descriptor.KeyNumber != (int)KeyNumber.Signature)))
            throw new InvalidDataException(
                "The app-owned CSP private-key descriptor is invalid.");
        if (descriptor.Kind == PrivateKeyKind.Cng &&
            (descriptor.ProviderType != 0 || descriptor.KeyNumber != 0))
            throw new InvalidDataException(
                "The app-owned CNG private-key descriptor is invalid.");
    }


    private static string QuoteArgument(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private static ProcessResult RunProcess(string fileName, string arguments,
        int timeoutMilliseconds, bool clearPowerShellModulePath)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = fileName;
        startInfo.Arguments = arguments;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        if (clearPowerShellModulePath)
            startInfo.EnvironmentVariables.Remove("PSModulePath");

        using (Process process = Process.Start(startInfo))
        {
            if (process == null)
                throw new InvalidOperationException(
                    "Windows could not start " + Path.GetFileName(fileName) + ".");
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(timeoutMilliseconds))
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                }
                throw new TimeoutException(
                    Path.GetFileName(fileName) + " did not finish in time.");
            }
            ProcessResult result = new ProcessResult();
            result.ExitCode = process.ExitCode;
            result.Output = output;
            result.Error = error;
            return result;
        }
    }

    private sealed class ProcessResult
    {
        public int ExitCode;
        public string Output;
        public string Error;
    }

    private sealed class TrustIdentity
    {
        public string Id;
        public string Sha256;
    }

    private sealed class OwnedCertificateDescriptor
    {
        public string StoreThumbprint;
        public string Sha256;
        public PrivateKeyDescriptor PrivateKey;
    }

    private enum PrivateKeyKind
    {
        None,
        Cng,
        Csp
    }

    private sealed class PrivateKeyDescriptor
    {
        public PrivateKeyKind Kind;
        public string KeyName;
        public string ProviderName;
        public int ProviderType;
        public int KeyNumber;
        public bool MachineKey;
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
