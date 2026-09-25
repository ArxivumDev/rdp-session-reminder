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
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]

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
                !generatedContent.Contains("devicestoredirect:s:") ||
                !generatedContent.Contains("camerastoredirect:s:") ||
                !generatedContent.Contains("usbdevicestoredirect:s:") ||
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
    private readonly CheckBox redirectClipboardCheck;
    private readonly CheckBox redirectDrivesCheck;
    private readonly CheckBox redirectLocationCheck;
    private readonly CheckBox redirectComPortsCheck;
    private readonly CheckBox redirectWebAuthnCheck;
    private readonly CheckBox redirectSmartCardsCheck;
    private readonly CheckBox redirectPrintersCheck;
    private readonly CheckBox redirectMicrophoneCheck;
    private readonly Button previewImportButton;
    private readonly Button chooseMonitorsButton;
    private readonly Label monitorSummary;
    private readonly ComboBox shortcutIconCombo;
    private readonly CheckBox trustPublisherCheck;
    private readonly Label publisherTrustStatus;
    private readonly Button removePublisherTrustButton;
    private readonly TabControl tabs;
    private readonly Button connectButton;
    private readonly Button oneTimeButton;
    private readonly Button cancelButton;
    private readonly Button previousButton;
    private readonly Button nextButton;
    private readonly ComboBox themeCombo;
    private readonly Label workflowSummary;
    private readonly Label association;
    private readonly Label privacy;
    private readonly Label operationStatus;
    private readonly ProgressBar operationProgress;
    private readonly UpdatePageController updateController;
    private readonly BannerSetupPage bannerSetupPage;
    private readonly ConnectionManagerController connectionManager;
    private MonitorSelection monitorSelection;
    private bool updatingShortcut;
    private bool updatingReminder;
    private bool shortcutManuallyEdited;
    private bool reminderManuallyEdited;
    private bool operationRunning;
    private bool allowClose;
    private bool changingTheme;
    private bool systemPreferenceSubscribed;
    private readonly string themePreferencePath;

    public SetupForm()
        : this(null)
    {
    }

    internal SetupForm(string preferencePath)
    {
        themePreferencePath = preferencePath;
        SetupPalette.SetTheme(string.IsNullOrEmpty(themePreferencePath)
            ? UiPreferenceStore.Load()
            : UiPreferenceStore.LoadFrom(themePreferencePath));
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
        BackColor = SetupPalette.Canvas;

        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
        }

        SetupHeroPanel hero = new SetupHeroPanel();
        hero.Location = new Point(20, 12);
        hero.Size = new Size(620, 69);
        Controls.Add(hero);

        Label themeLabel = new Label();
        themeLabel.AutoSize = true;
        themeLabel.Location = new Point(496, 7);
        themeLabel.Font = new Font("Segoe UI Semibold", 8.25f,
            FontStyle.Bold);
        themeLabel.Text = "Appearance";
        hero.Controls.Add(themeLabel);

        themeCombo = new ComboBox();
        themeCombo.AccessibleName = "Appearance theme";
        themeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        themeCombo.Location = new Point(493, 27);
        themeCombo.Size = new Size(108, 25);
        themeCombo.Items.Add("Dark");
        themeCombo.Items.Add("Light");
        changingTheme = true;
        themeCombo.SelectedIndex = SetupPalette.RequestedTheme ==
            UiThemeMode.Light ? 1 : 0;
        changingTheme = false;
        themeCombo.SelectedIndexChanged += ThemeSelectionChanged;
        hero.Controls.Add(themeCombo);

        workflowSummary = new Label();
        workflowSummary.AutoSize = false;
        workflowSummary.Location = new Point(30, 88);
        workflowSummary.Size = new Size(595, 30);
        workflowSummary.Font = new Font("Segoe UI Semibold", 9.5f,
            FontStyle.Bold);
        workflowSummary.ForeColor = SetupPalette.MutedInk;
        Controls.Add(workflowSummary);

        tabs = new GuidedTabControl();
        tabs.Location = new Point(25, 122);
        tabs.Size = new Size(610, 424);
        tabs.TabIndex = 0;
        Controls.Add(tabs);

        TabPage connectionTab = new TabPage("1. Connection");
        connectionTab.Padding = new Padding(12);
        connectionTab.BackColor = SetupPalette.Surface;
        connectionTab.UseVisualStyleBackColor = false;
        tabs.TabPages.Add(connectionTab);

        TabPage displayTab = new TabPage("2. Displays");
        displayTab.Padding = new Padding(12);
        displayTab.BackColor = SetupPalette.Surface;
        displayTab.UseVisualStyleBackColor = false;
        tabs.TabPages.Add(displayTab);

        TabPage resourcesTab = new TabPage("3. Resources");
        resourcesTab.Padding = new Padding(12);
        resourcesTab.BackColor = SetupPalette.Surface;
        resourcesTab.UseVisualStyleBackColor = false;
        tabs.TabPages.Add(resourcesTab);

        bannerSetupPage = new BannerSetupPage(this, tabs);
        connectionManager = AttachConnectionManager();

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
        rdpFileLabel.Text =
            "Import an existing RDP connection file (optional, less common)";
        connectionTab.Controls.Add(rdpFileLabel);

        rdpFileText = new TextBox();
        rdpFileText.Location = new Point(21, 104);
        rdpFileText.Size = new Size(360, 25);
        rdpFileText.TabIndex = 1;
        connectionTab.Controls.Add(rdpFileText);

        Button browseButton = new Button();
        browseButton.Location = new Point(389, 102);
        browseButton.Size = new Size(87, 29);
        browseButton.Text = "Browse...";
        browseButton.TabIndex = 2;
        browseButton.Click += BrowseForRdpFile;
        connectionTab.Controls.Add(browseButton);

        previewImportButton = new Button();
        previewImportButton.Location = new Point(484, 102);
        previewImportButton.Size = new Size(87, 29);
        previewImportButton.Text = "Preview...";
        previewImportButton.TabIndex = 3;
        previewImportButton.Enabled = false;
        previewImportButton.Click += PreviewRdpFile;
        connectionTab.Controls.Add(previewImportButton);

        Label fileModeHelp = new Label();
        fileModeHelp.AutoSize = false;
        fileModeHelp.Location = new Point(21, 136);
        fileModeHelp.Size = new Size(550, 36);
        fileModeHelp.ForeColor = Color.FromArgb(80, 80, 80);
        fileModeHelp.Text =
            "Recommended: leave this empty for guided settings. If you select a file, " +
            "Preview shows its requests first and the original stays unchanged.";
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

        foreach (MonitorDescriptor monitor in
            new ScreenMonitorTopologySource().GetMonitors())
        {
            DisplayChoice choice = new DisplayChoice(monitor);
            int itemIndex = displayCombo.Items.Add(choice);
            if (monitor.IsPrimary)
                displayCombo.SelectedIndex = itemIndex;
        }
        if (displayCombo.SelectedIndex < 0 && displayCombo.Items.Count > 0)
            displayCombo.SelectedIndex = 0;

        Label reminderLabel = new Label();
        reminderLabel.AutoSize = true;
        reminderLabel.Location = new Point(18, 176);
        reminderLabel.Text = "Reminder text";
        connectionTab.Controls.Add(reminderLabel);

        reminderText = new TextBox();
        reminderText.Location = new Point(21, 200);
        reminderText.Size = new Size(550, 25);
        reminderText.MaxLength = 100;
        reminderText.TabIndex = 4;
        reminderText.TextChanged += delegate
        {
            if (!updatingReminder)
                reminderManuallyEdited = true;
        };
        connectionTab.Controls.Add(reminderText);

        Label shortcutLabel = new Label();
        shortcutLabel.AutoSize = true;
        shortcutLabel.Location = new Point(18, 235);
        shortcutLabel.Text =
            "Desktop shortcut name (for a reusable connection)";
        connectionTab.Controls.Add(shortcutLabel);

        shortcutText = new TextBox();
        shortcutText.Location = new Point(21, 259);
        shortcutText.Size = new Size(550, 25);
        shortcutText.MaxLength = 80;
        shortcutText.TabIndex = 5;
        shortcutText.TextChanged += delegate
        {
            if (!updatingShortcut)
                shortcutManuallyEdited = true;
        };
        connectionTab.Controls.Add(shortcutText);

        Label iconLabel = new Label();
        iconLabel.AutoSize = true;
        iconLabel.Location = new Point(18, 294);
        iconLabel.Text = "Desktop shortcut icon (for a reusable connection)";
        connectionTab.Controls.Add(iconLabel);

        shortcutIconCombo = new ComboBox();
        shortcutIconCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        shortcutIconCombo.Location = new Point(21, 318);
        shortcutIconCombo.Size = new Size(550, 25);
        shortcutIconCombo.TabIndex = 6;
        foreach (ShortcutIconChoice iconChoice in ShortcutIconCatalog.GetChoices())
            shortcutIconCombo.Items.Add(iconChoice);
        if (shortcutIconCombo.Items.Count > 0)
            shortcutIconCombo.SelectedIndex = 0;
        connectionTab.Controls.Add(shortcutIconCombo);

        Label shortcutIconHelp = new Label();
        shortcutIconHelp.AutoSize = false;
        shortcutIconHelp.Location = new Point(21, 346);
        shortcutIconHelp.Size = new Size(550, 20);
        shortcutIconHelp.ForeColor = SetupPalette.MutedInk;
        shortcutIconCombo.SelectedIndexChanged += delegate
        {
            ShortcutIconChoice selected =
                shortcutIconCombo.SelectedItem as ShortcutIconChoice;
            shortcutIconHelp.Text = selected == null ? "" :
                selected.Description;
        };
        connectionTab.Controls.Add(shortcutIconHelp);
        if (shortcutIconCombo.SelectedItem != null)
            shortcutIconHelp.Text = ((ShortcutIconChoice)
                shortcutIconCombo.SelectedItem).Description;

        Label signInHelp = new Label();
        signInHelp.AutoSize = false;
        signInHelp.Location = new Point(21, 369);
        signInHelp.Size = new Size(550, 20);
        signInHelp.Font = new Font("Segoe UI", 8.25f);
        signInHelp.ForeColor = SetupPalette.MutedInk;
        signInHelp.Text =
            "Sign-in: Windows uses a saved account when available; otherwise it prompts.";
        connectionTab.Controls.Add(signInHelp);

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
            if (allMonitorsCheck.Checked)
                monitorSelection = null;
            UpdateCustomOptionState();
            UpdateMonitorSummary();
        };
        displayTab.Controls.Add(allMonitorsCheck);

        chooseMonitorsButton = new Button();
        chooseMonitorsButton.Location = new Point(21, 220);
        chooseMonitorsButton.Size = new Size(190, 30);
        chooseMonitorsButton.Text = "Choose specific monitors...";
        chooseMonitorsButton.TabIndex = 4;
        chooseMonitorsButton.Click += ChooseSpecificMonitors;
        displayTab.Controls.Add(chooseMonitorsButton);

        monitorSummary = new Label();
        monitorSummary.AutoSize = false;
        monitorSummary.Location = new Point(224, 222);
        monitorSummary.Size = new Size(347, 36);
        monitorSummary.ForeColor = Color.FromArgb(70, 70, 70);
        displayTab.Controls.Add(monitorSummary);

        Label displayDefaults = new Label();
        displayDefaults.AutoSize = false;
        displayDefaults.Location = new Point(21, 270);
        displayDefaults.Size = new Size(550, 100);
        displayDefaults.ForeColor = Color.FromArgb(70, 70, 70);
        displayDefaults.Text =
            "Generated profiles use the recommended highest color quality (32 bit), show the " +
            "full-screen connection bar, send Windows key combinations only in full " +
            "screen, play remote audio on this PC, detect connection quality, keep a " +
            "persistent bitmap cache, reconnect after drops, and warn if server " +
            "authentication fails. Resolution is controlled by Windows when all " +
            "monitors are used.";
        displayTab.Controls.Add(displayDefaults);

        Label resourcesLabel = new Label();
        resourcesLabel.AutoSize = true;
        resourcesLabel.Location = new Point(18, 18);
        resourcesLabel.Text =
            "Resources available remotely (recommended defaults are preselected)";
        resourcesTab.Controls.Add(resourcesLabel);

        redirectClipboardCheck = CreateResourceCheckBox(
            resourcesTab, "Clipboard", 21, 49, true, 0);
        redirectDrivesCheck = CreateResourceCheckBox(
            resourcesTab, "All local drives", 300, 49, false, 1);
        redirectPrintersCheck = CreateResourceCheckBox(
            resourcesTab, "Printers", 21, 82, false, 2);
        redirectMicrophoneCheck = CreateResourceCheckBox(
            resourcesTab, "Microphone", 300, 82, false, 3);
        redirectWebAuthnCheck = CreateResourceCheckBox(
            resourcesTab,
            "WebAuthn passkeys and security keys",
            21, 115, true, 4);

        GroupBox lessCommonResources = new GroupBox();
        lessCommonResources.Location = new Point(21, 145);
        lessCommonResources.Size = new Size(550, 72);
        lessCommonResources.Text = "Less common resources";
        lessCommonResources.ForeColor = SetupPalette.MutedInk;
        resourcesTab.Controls.Add(lessCommonResources);

        redirectLocationCheck = CreateResourceCheckBox(
            lessCommonResources, "Location", 12, 23, false, 5);
        redirectComPortsCheck = CreateResourceCheckBox(
            lessCommonResources, "Serial and COM ports", 180, 23, false, 6);
        redirectSmartCardsCheck = CreateResourceCheckBox(
            lessCommonResources, "Smart cards or Windows Hello for Business",
            12, 46, false, 7);

        Label trustHeading = new Label();
        trustHeading.AutoSize = true;
        trustHeading.Font = new Font(Font, FontStyle.Bold);
        trustHeading.Location = new Point(18, 226);
        trustHeading.Text = "Optional RDP publisher trust (less common)";
        resourcesTab.Controls.Add(trustHeading);

        trustPublisherCheck = new CheckBox();
        trustPublisherCheck.AutoSize = false;
        trustPublisherCheck.Location = new Point(21, 249);
        trustPublisherCheck.Size = new Size(550, 44);
        trustPublisherCheck.Text =
            "Create or reuse a private, non-exportable certificate on this Windows " +
            "account, trust that RDP publisher for this user, and sign this generated profile";
        trustPublisherCheck.Checked = false;
        trustPublisherCheck.TabIndex = 8;
        resourcesTab.Controls.Add(trustPublisherCheck);

        publisherTrustStatus = new Label();
        publisherTrustStatus.AutoSize = false;
        publisherTrustStatus.Location = new Point(21, 296);
        publisherTrustStatus.Size = new Size(550, 39);
        publisherTrustStatus.ForeColor = Color.FromArgb(75, 75, 75);
        resourcesTab.Controls.Add(publisherTrustStatus);

        removePublisherTrustButton = new Button();
        removePublisherTrustButton.Location = new Point(21, 337);
        removePublisherTrustButton.Size = new Size(250, 30);
        removePublisherTrustButton.Text = "Remove and verify publisher trust";
        removePublisherTrustButton.TabIndex = 9;
        removePublisherTrustButton.Click += RemovePublisherTrust;
        resourcesTab.Controls.Add(removePublisherTrustButton);

        Label trustExplanation = new Label();
        trustExplanation.AutoSize = false;
        trustExplanation.Location = new Point(286, 335);
        trustExplanation.Size = new Size(285, 51);
        trustExplanation.ForeColor = Color.FromArgb(80, 80, 80);
        trustExplanation.Text =
            "Windows trusts any .rdp signed by this private local key for this user. " +
            "This does not code-sign or trust the application executable.";
        resourcesTab.Controls.Add(trustExplanation);

        rdpFileText.TextChanged += RdpFileTextChanged;
        updateController = UpdateUi.Attach(this, tabs);
        foreach (TabPage page in tabs.TabPages)
        {
            page.BackColor = SetupPalette.Surface;
            page.UseVisualStyleBackColor = false;
        }
        UpdatePublisherTrustStatus();
        UpdateCustomOptionState();
        UpdateMonitorSummary();

        association = new Label();
        association.AutoSize = false;
        association.Location = new Point(30, 554);
        association.Size = new Size(595, 38);
        association.ForeColor = Color.FromArgb(70, 70, 70);
        association.Text =
            "Create a reusable shortcut, or choose Connect once for a temporary " +
            "profile with no shortcut.";
        Controls.Add(association);

        privacy = new Label();
        privacy.AutoSize = false;
        privacy.Location = new Point(30, 594);
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

        connectButton = new DimensionalButton(true);
        connectButton.Text = "Create shortcut and connect";
        connectButton.Size = new Size(172, 30);
        connectButton.Location = new Point(386, 686);
        connectButton.TabIndex = 1;
        connectButton.Click += SaveAndConnect;
        Controls.Add(connectButton);
        AcceptButton = connectButton;

        oneTimeButton = new DimensionalButton(false);
        oneTimeButton.Location = new Point(248, 686);
        oneTimeButton.Size = new Size(128, 30);
        oneTimeButton.Text = "Connect once";
        oneTimeButton.TabIndex = 2;
        oneTimeButton.Click += SaveAndConnectOnce;
        Controls.Add(oneTimeButton);

        cancelButton = new DimensionalButton(false);
        cancelButton.Location = new Point(566, 686);
        cancelButton.Size = new Size(67, 30);
        cancelButton.Text = "Cancel";
        cancelButton.TabIndex = 3;
        cancelButton.DialogResult = DialogResult.Cancel;
        cancelButton.Click += delegate
        {
            if (!operationRunning)
            {
                allowClose = true;
                Close();
            }
        };
        Controls.Add(cancelButton);
        CancelButton = cancelButton;

        previousButton = new DimensionalButton(false);
        previousButton.Location = new Point(30, 686);
        previousButton.Size = new Size(86, 30);
        previousButton.Text = "< Back";
        previousButton.TabIndex = 4;
        previousButton.Click += PreviousStep;
        Controls.Add(previousButton);

        nextButton = new DimensionalButton(true);
        nextButton.Location = new Point(124, 686);
        nextButton.Size = new Size(116, 30);
        nextButton.Text = "Next >";
        nextButton.TabIndex = 5;
        nextButton.Click += NextStep;
        Controls.Add(nextButton);

        tabs.SelectedIndexChanged += WorkflowPageChanged;
        tabs.SelectedIndex = connectionManager.HasConnections ? 4 : 0;
        UpdateWorkflowNavigation();
        SetupVisualTheme.Apply(this);

        Shown += FitToCurrentWorkingArea;
        FormClosing += SetupFormClosing;
        SystemEvents.UserPreferenceChanged += SystemUserPreferenceChanged;
        systemPreferenceSubscribed = true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && systemPreferenceSubscribed)
        {
            SystemEvents.UserPreferenceChanged -= SystemUserPreferenceChanged;
            systemPreferenceSubscribed = false;
        }
        base.Dispose(disposing);
    }

    protected override void OnSystemColorsChanged(EventArgs eventArgs)
    {
        base.OnSystemColorsChanged(eventArgs);
        if (!IsDisposed && !Disposing && Controls.Count > 0)
            RefreshForSystemPreferences();
    }

    private void SystemUserPreferenceChanged(object sender,
        UserPreferenceChangedEventArgs eventArgs)
    {
        if (!systemPreferenceSubscribed || IsDisposed || Disposing ||
            !IsHandleCreated)
            return;

        try
        {
            if (InvokeRequired)
                BeginInvoke((MethodInvoker)RefreshForSystemPreferences);
            else
                RefreshForSystemPreferences();
        }
        catch (InvalidOperationException)
        {
            // The window can close while Windows is delivering the event.
        }
    }

    internal void RefreshForSystemPreferences()
    {
        if (IsDisposed || Disposing)
            return;
        SetupVisualTheme.Apply(this);
    }

    private void ThemeSelectionChanged(object sender, EventArgs eventArgs)
    {
        if (changingTheme || themeCombo.SelectedIndex < 0)
            return;

        UiThemeMode selected = themeCombo.SelectedIndex == 1
            ? UiThemeMode.Light
            : UiThemeMode.Dark;
        SetupPalette.SetTheme(selected);
        SetupVisualTheme.Apply(this);
        try
        {
            if (string.IsNullOrEmpty(themePreferencePath))
                UiPreferenceStore.Save(selected);
            else
                UiPreferenceStore.SaveTo(themePreferencePath, selected);
        }
        catch
        {
            // The selected theme still applies for this setup session. A future
            // launch falls back to the safe dark default if the preference
            // cannot be written.
        }
    }

    private void PreviousStep(object sender, EventArgs eventArgs)
    {
        if (!operationRunning && tabs.SelectedIndex > 0 &&
            tabs.SelectedIndex < 4)
            tabs.SelectedIndex--;
    }

    private void NextStep(object sender, EventArgs eventArgs)
    {
        if (!operationRunning && tabs.SelectedIndex >= 0 &&
            tabs.SelectedIndex < 3)
            tabs.SelectedIndex++;
    }

    private void WorkflowPageChanged(object sender, EventArgs eventArgs)
    {
        UpdateWorkflowNavigation();
    }

    private void UpdateWorkflowNavigation()
    {
        int pageIndex = tabs.SelectedIndex;
        bool workflowPage = pageIndex >= 0 && pageIndex < 4;
        bool finalStep = pageIndex == 3;

        previousButton.Visible = workflowPage;
        previousButton.Enabled = workflowPage && pageIndex > 0;
        nextButton.Visible = workflowPage && !finalStep;
        connectButton.Visible = workflowPage && finalStep;
        oneTimeButton.Visible = workflowPage && finalStep;
        privacy.Visible = workflowPage;
        cancelButton.Text = workflowPage ? "Cancel" : "Close";

        if (workflowPage)
        {
            string[] summaries = new string[]
            {
                "Step 1 of 4 - Choose the computer. Recommended values fill in as you type.",
                "Step 2 of 4 - Choose displays. The primary screen and full screen are recommended.",
                "Step 3 of 4 - Choose resources. Security-conscious defaults are already selected.",
                "Step 4 of 4 - Review the reminder style, then create the connection."
            };
            workflowSummary.Text = summaries[pageIndex];
            nextButton.Text = pageIndex == 0
                ? "Next: Displays >"
                : pageIndex == 1
                    ? "Next: Resources >"
                    : "Next: Reminder >";
            association.Text = finalStep
                ? "Create a reusable shortcut, or choose Connect once for a " +
                    "temporary profile with no shortcut."
                : "Recommended choices are already selected. Continue through " +
                    "the steps, or select a numbered step above.";
            association.Visible = true;
            AcceptButton = finalStep ? connectButton : nextButton;
        }
        else
        {
            bool savedConnections = pageIndex == 4;
            workflowSummary.Text = savedConnections
                ? "Saved connections - Connect, customize, duplicate, repair, or diagnose app-created profiles."
                : "Updates - Check the official release channel and review changes before installing.";
            association.Text =
                "Configuration only: closing this window ends the setup app and its memory use.";
            association.Visible = true;
            AcceptButton = null;
        }
    }

    private void FocusWorkflowControl(int pageIndex, Control control,
        bool selectAll)
    {
        if (pageIndex >= 0 && pageIndex < tabs.TabPages.Count)
            tabs.SelectedIndex = pageIndex;
        control.Focus();
        TextBox textBox = control as TextBox;
        if (selectAll && textBox != null)
            textBox.SelectAll();
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

    private ConnectionManagerController AttachConnectionManager()
    {
        ConnectionManagerOptions options = new ConnectionManagerOptions();
        options.RuntimePath = AppPaths.RuntimePath;
        options.CloseSetupAfterConnect = true;
        options.EditRequested = EditManagedConnection;
        options.DuplicateCreated = delegate(ManagedConnectionInfo profile)
        {
            RepairManagedShortcut(profile.ProfileId);
        };
        options.RepairShortcutRequested = delegate(ManagedConnectionInfo profile)
        {
            RepairManagedShortcut(profile.ProfileId);
        };
        options.ProfileDeleted = RemoveOwnedDesktopShortcut;
        options.DiagnosticsContextProvider = BuildDiagnosticsContext;
        return ConnectionManagerUi.Attach(this, tabs, options);
    }

    private void EditManagedConnection(ManagedConnectionInfo profile)
    {
        if (profile == null)
            return;
        using (ManagedProfileEditorForm editor =
            new ManagedProfileEditorForm(profile.ProfileId))
        {
            if (editor.ShowDialog(this) == DialogResult.OK)
                connectionManager.RefreshProfiles();
        }
    }

    internal static string RepairManagedShortcut(string profileId)
    {
        ReminderSettings settings;
        if (!SettingsStore.TryLoadProfile(profileId, out settings))
            throw new InvalidDataException(
                "The connection settings are missing or invalid.");
        if (!File.Exists(AppPaths.GetProfileConnectionPath(profileId)))
            throw new FileNotFoundException(
                "The connection's private RDP file is missing.");

        InstallApplicationFiles();
        string desktop = Environment.GetFolderPath(
            Environment.SpecialFolder.DesktopDirectory);
        string expectedPath = Path.Combine(desktop,
            settings.ShortcutName + ".lnk");
        string arguments = "--profile " + profileId;
        string shortcutPath = expectedPath;
        if (File.Exists(expectedPath) &&
            !IsOwnedShortcut(expectedPath, profileId))
        {
            shortcutPath = ShortcutPathHelper.ReserveUniquePath(
                desktop, settings.ShortcutName);
            settings.ShortcutName =
                Path.GetFileNameWithoutExtension(shortcutPath);
            SettingsStore.SaveTo(
                AppPaths.GetProfileSettingsPath(profileId), settings);
        }

        ShortcutWriter.Create(shortcutPath, AppPaths.RuntimePath, "",
            arguments, ResolveShortcutIcon(settings.ShortcutIcon));
        ShellRefresh.NotifyItem(shortcutPath);
        ShellRefresh.NotifyDirectory(desktop);
        return shortcutPath;
    }

    private static void RemoveOwnedDesktopShortcut(ManagedConnectionInfo profile)
    {
        if (profile == null || profile.Settings == null)
            return;
        string desktop = Environment.GetFolderPath(
            Environment.SpecialFolder.DesktopDirectory);
        string path = Path.Combine(desktop,
            profile.Settings.ShortcutName + ".lnk");
        if (!File.Exists(path) || !IsOwnedShortcut(path, profile.ProfileId))
            return;
        File.Delete(path);
        ShellRefresh.NotifyItem(path);
        ShellRefresh.NotifyDirectory(desktop);
    }

    internal static bool IsOwnedShortcut(string path, string profileId)
    {
        if (!SettingsStore.IsValidProfileId(profileId) ||
            string.IsNullOrEmpty(path) || !File.Exists(path))
            return false;
        try
        {
            return string.Equals(Path.GetFullPath(
                    ShortcutWriter.ReadTargetPath(path)),
                    Path.GetFullPath(AppPaths.RuntimePath),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ShortcutWriter.ReadArguments(path),
                    "--profile " + profileId, StringComparison.Ordinal) &&
                ShortcutWriter.ReadWorkingDirectory(path).Length == 0;
        }
        catch
        {
            return false;
        }
    }

    private ConnectionDiagnosticsContext BuildDiagnosticsContext(
        ManagedConnectionInfo profile)
    {
        ConnectionDiagnosticsContext context =
            new ConnectionDiagnosticsContext();
        context.ApplicationVersion = Assembly.GetExecutingAssembly()
            .GetName().Version.ToString();
        context.RuntimePath = AppPaths.RuntimePath;
        context.MstscPath = Path.Combine(
            Environment.SystemDirectory, "mstsc.exe");
        context.ApplicationSignatureValid =
            ExecutableSignatureStatus.IsTrusted(AppPaths.RuntimePath);
        context.RdpPublisherTrusted = LocalRdpPublisherTrust.IsInstalled();
        context.AvailableMonitorIds = GetAvailableMonitorIds();
        context.SelectedMonitorIds = ReadSelectedMonitorIds(profile.RdpPath);
        context.Shortcut = InspectManagedShortcut(profile);
        return context;
    }

    private static ConnectionShortcutSnapshot InspectManagedShortcut(
        ManagedConnectionInfo profile)
    {
        ConnectionShortcutSnapshot snapshot =
            new ConnectionShortcutSnapshot();
        snapshot.Inspected = true;
        if (profile == null || profile.Settings == null)
            return snapshot;
        string path = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.DesktopDirectory),
            profile.Settings.ShortcutName + ".lnk");
        snapshot.Exists = File.Exists(path);
        if (!snapshot.Exists)
            return snapshot;
        try
        {
            snapshot.TargetPath = ShortcutWriter.ReadTargetPath(path);
            snapshot.Arguments = ShortcutWriter.ReadArguments(path);
            snapshot.IconLocation = ShortcutWriter.ReadIconLocation(path);
            snapshot.WorkingDirectory =
                ShortcutWriter.ReadWorkingDirectory(path);
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

    private static int[] GetAvailableMonitorIds()
    {
        List<int> ids = new List<int>();
        foreach (MonitorDescriptor monitor in
            new ScreenMonitorTopologySource().GetMonitors())
            ids.Add(monitor.MstscId);
        return ids.ToArray();
    }

    private static int[] ReadSelectedMonitorIds(string rdpPath)
    {
        List<int> ids = new List<int>();
        try
        {
            using (FileStream stream = new FileStream(rdpPath, FileMode.Open,
                FileAccess.Read, FileShare.Read))
            using (StreamReader reader = new StreamReader(stream,
                Encoding.Default, true))
            {
                if (stream.Length > 4 * 1024 * 1024)
                    return ids.ToArray();
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    const string prefix = "selectedmonitors:s:";
                    if (!line.StartsWith(prefix,
                            StringComparison.OrdinalIgnoreCase))
                        continue;
                    string normalized =
                        RdpProfileWriter.NormalizeSelectedMonitors(
                            line.Substring(prefix.Length));
                    foreach (string piece in normalized.Split(','))
                    {
                        int value;
                        if (int.TryParse(piece, out value))
                            ids.Add(value);
                    }
                    break;
                }
            }
        }
        catch
        {
            ids.Clear();
        }
        return ids.ToArray();
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
        chooseMonitorsButton.Enabled = customProfile && !allMonitorsCheck.Checked;
        resolutionCombo.Enabled = customProfile && !allMonitorsCheck.Checked &&
            monitorSelection == null;
        redirectClipboardCheck.Enabled = customProfile;
        redirectDrivesCheck.Enabled = customProfile;
        redirectPrintersCheck.Enabled = customProfile;
        redirectMicrophoneCheck.Enabled = customProfile;
        redirectLocationCheck.Enabled = customProfile;
        redirectComPortsCheck.Enabled = customProfile;
        redirectWebAuthnCheck.Enabled = customProfile;
        redirectSmartCardsCheck.Enabled = customProfile;
        trustPublisherCheck.Enabled = customProfile;
        previewImportButton.Enabled = !customProfile &&
            File.Exists(rdpFileText.Text.Trim());
    }

    private void PreviewRdpFile(object sender, EventArgs eventArgs)
    {
        string path = rdpFileText.Text.Trim();
        try
        {
            using (RdpImportSnapshot snapshot = RdpImportSnapshot.Create(path))
            using (RdpImportPreviewDialog dialog =
                new RdpImportPreviewDialog(snapshot.SnapshotPath,
                    snapshot.SourceFileName, false))
                dialog.ShowDialog(this);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this,
                "The RDP file could not be previewed.\r\n\r\n" +
                exception.Message, AppPaths.ProductName,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ChooseSpecificMonitors(object sender, EventArgs eventArgs)
    {
        try
        {
            IMonitorTopologySource source = new ScreenMonitorTopologySource();
            IList<MonitorDescriptor> topology = source.GetMonitors();
            MonitorSelection initial = monitorSelection;
            if (initial == null)
            {
                int remoteId = topology[0].MstscId;
                int reminderId = remoteId;
                DisplayChoice selectedDisplay =
                    displayCombo.SelectedItem as DisplayChoice;
                foreach (MonitorDescriptor monitor in topology)
                {
                    if (monitor.IsPrimary)
                        remoteId = monitor.MstscId;
                    if (selectedDisplay != null && string.Equals(
                            selectedDisplay.DeviceName, monitor.DeviceName,
                            StringComparison.OrdinalIgnoreCase))
                        reminderId = monitor.MstscId;
                }
                initial = new MonitorSelection(topology,
                    new int[] { remoteId }, reminderId);
            }

            using (MonitorSelectionDialog dialog =
                new MonitorSelectionDialog(source, initial))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                dialog.Selection.ValidateForRdp();
                monitorSelection = dialog.Selection;
            }

            allMonitorsCheck.Checked = false;
            SelectReminderDisplay(monitorSelection.ReminderDeviceName);
            UpdateCustomOptionState();
            UpdateMonitorSummary();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, AppPaths.ProductName,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void SelectReminderDisplay(string deviceName)
    {
        for (int index = 0; index < displayCombo.Items.Count; index++)
        {
            DisplayChoice choice = displayCombo.Items[index] as DisplayChoice;
            if (choice != null && string.Equals(choice.DeviceName, deviceName,
                    StringComparison.OrdinalIgnoreCase))
            {
                displayCombo.SelectedIndex = index;
                return;
            }
        }
    }

    private void UpdateMonitorSummary()
    {
        if (allMonitorsCheck.Checked)
        {
            monitorSummary.Text = "Remote session: all monitors";
            return;
        }
        if (monitorSelection == null)
        {
            monitorSummary.Text =
                "Remote session: Windows default single monitor";
            return;
        }
        List<string> displays = new List<string>();
        foreach (int id in monitorSelection.RemoteMonitorIds)
            displays.Add("Display " + (id + 1).ToString());
        monitorSummary.Text = "Remote session: " +
            string.Join(", ", displays.ToArray()) +
            "; remote primary: Display " +
            (monitorSelection.RemotePrimaryMonitorId + 1).ToString();
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
        SaveAndConnectCore(false);
    }

    private void SaveAndConnectOnce(object sender, EventArgs eventArgs)
    {
        SaveAndConnectCore(true);
    }

    private void SaveAndConnectCore(bool oneTime)
    {
        RdpImportSnapshot importSnapshot = null;
        try
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
                FocusWorkflowControl(0, rdpFileText, true);
                return;
            }

            if (selectedRdpFile.Length > 0)
            {
                try
                {
                    importSnapshot = RdpImportSnapshot.Create(selectedRdpFile);
                    using (RdpImportPreviewDialog dialog =
                        new RdpImportPreviewDialog(importSnapshot.SnapshotPath,
                            importSnapshot.SourceFileName, true))
                    {
                        if (dialog.ShowDialog(this) != DialogResult.OK)
                            return;
                    }
                    selectedRdpFile = importSnapshot.SnapshotPath;
                }
                catch (Exception exception)
                {
                    MessageBox.Show(this,
                        "The RDP file could not be locked and previewed safely.\r\n\r\n" +
                        exception.Message, AppPaths.ProductName,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    FocusWorkflowControl(0, rdpFileText, true);
                    return;
                }
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
                FocusWorkflowControl(0, rdpFileText, true);
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
                FocusWorkflowControl(0, computerText, true);
                return;
            }
        }

        string profileId = Guid.NewGuid().ToString("N");
        string profileDirectory = oneTime
            ? AppPaths.GetOneTimeProfileDirectory(profileId)
            : AppPaths.GetProfileDirectory(profileId);
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
            FocusWorkflowControl(2, redirectLocationCheck, false);
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
        settings.RdpFile = oneTime
            ? AppPaths.GetOneTimeConnectionPath(profileId)
            : AppPaths.GetProfileConnectionPath(profileId);
        bannerSetupPage.ApplyTo(settings);
        ShortcutIconChoice selectedIcon =
            shortcutIconCombo.SelectedItem as ShortcutIconChoice;
        settings.ShortcutIcon = selectedIcon == null
            ? "WindowsRemoteDesktop"
            : selectedIcon.Kind.ToString();

        SetupOperationRequest request = new SetupOperationRequest();
        request.ProfileId = profileId;
        request.ProfileDirectory = profileDirectory;
        request.SelectedRdpFile = selectedRdpFile;
        request.Settings = settings;
        request.OneTime = oneTime;
        request.ShortcutNameWasAutomatic = !shortcutManuallyEdited;
        request.ReminderWasAutomatic = !reminderManuallyEdited;
        request.TrustPublisher = selectedRdpFile.Length == 0 &&
            trustPublisherCheck.Checked;
        if (selectedRdpFile.Length == 0)
        {
            request.ProfileOptions = new RdpProfileOptions(
                computer, fullScreenCheck.Checked,
                selectedResolution.Width, selectedResolution.Height,
                allMonitorsCheck.Checked || monitorSelection != null,
                monitorSelection == null ? "" :
                    monitorSelection.SelectedMonitorsSetting,
                false,
                redirectClipboardCheck.Checked, redirectDrivesCheck.Checked,
                redirectLocationCheck.Checked, redirectComPortsCheck.Checked,
                redirectWebAuthnCheck.Checked,
                redirectSmartCardsCheck.Checked,
                redirectPrintersCheck.Checked,
                redirectMicrophoneCheck.Checked);
        }
            request.DeleteSelectedRdpFileAfterUse = importSnapshot != null;
            BeginSetupOperation(request);
            if (importSnapshot != null)
                importSnapshot.Detach();
        }
        finally
        {
            if (importSnapshot != null)
                importSnapshot.Dispose();
        }
    }

    private void BeginSetupOperation(SetupOperationRequest request)
    {
        operationRunning = true;
        tabs.Enabled = false;
        connectButton.Enabled = false;
        oneTimeButton.Enabled = false;
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
                oneTimeButton.Enabled = true;
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
            string profileRdpFile = request.OneTime
                ? AppPaths.GetOneTimeConnectionPath(request.ProfileId)
                : AppPaths.GetProfileConnectionPath(request.ProfileId);
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

            if (!request.OneTime)
            {
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
                            AppPaths.RuntimePath, "", "--profile " + request.ProfileId,
                            ResolveShortcutIcon(request.Settings.ShortcutIcon));
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
            }
            else
            {
                worker.ReportProgress(4, "Saving temporary reminder settings...");
                SettingsStore.SaveTo(
                    AppPaths.GetOneTimeSettingsPath(request.ProfileId),
                    request.Settings);
                worker.ReportProgress(5,
                    "One-time mode selected; no desktop shortcut was created.");
            }

            worker.ReportProgress(6, "Starting Remote Desktop...");
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = AppPaths.RuntimePath;
            startInfo.Arguments = (request.OneTime
                ? "--one-time "
                : "--profile ") + request.ProfileId;
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
        finally
        {
            if (request.DeleteSelectedRdpFileAfterUse)
                DeleteImportSnapshot(request.SelectedRdpFile);
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
        public bool OneTime;
        public bool DeleteSelectedRdpFileAfterUse;
    }

    private sealed class SetupOperationResult
    {
        public bool ShortcutCompleted;
        public Exception Failure;
    }

    internal static string ResolveShortcutIcon(string value)
    {
        ShortcutIconKind kind = ShortcutIconKind.WindowsRemoteDesktop;
        try
        {
            kind = (ShortcutIconKind)Enum.Parse(typeof(ShortcutIconKind),
                SettingsStore.NormalizeShortcutIcon(value), true);
        }
        catch
        {
            kind = ShortcutIconKind.WindowsRemoteDesktop;
        }
        return ShortcutIconCatalog.Resolve(kind).IconLocation;
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

        TryDeleteKnownFailedProfile(profileDirectory);
    }

    private static void TryDeleteKnownFailedProfile(string profileDirectory)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(profileDirectory) ||
                !Directory.Exists(profileDirectory))
                return;
            string fullDirectory = Path.GetFullPath(profileDirectory);
            string name = Path.GetFileName(fullDirectory);
            if (!SettingsStore.IsValidProfileId(name))
                return;
            string profilesRoot = Path.GetFullPath(AppPaths.ProfilesDirectory);
            string oneTimeRoot = Path.GetFullPath(
                AppPaths.OneTimeProfilesDirectory);
            string parent = Path.GetDirectoryName(fullDirectory);
            string root = string.Equals(parent, profilesRoot,
                StringComparison.OrdinalIgnoreCase)
                ? profilesRoot
                : string.Equals(parent, oneTimeRoot,
                    StringComparison.OrdinalIgnoreCase)
                    ? oneTimeRoot
                    : "";
            if (root.Length == 0 || IsReparsePointForCleanup(root) ||
                IsReparsePointForCleanup(fullDirectory))
                return;
            string settings = Path.Combine(fullDirectory,
                AppPaths.SettingsFileName);
            string rdp = Path.Combine(fullDirectory,
                AppPaths.ConnectionFileName);
            foreach (string entry in Directory.GetFileSystemEntries(
                fullDirectory))
            {
                if ((!string.Equals(entry, settings,
                         StringComparison.OrdinalIgnoreCase) &&
                     !string.Equals(entry, rdp,
                         StringComparison.OrdinalIgnoreCase)) ||
                    Directory.Exists(entry) ||
                    IsReparsePointForCleanup(entry))
                    return;
            }
            string ignored;
            OneTimeProfileStore.TryDeleteExactFromRoot(root, name,
                out ignored);
        }
        catch
        {
        }
    }

    private static bool IsReparsePointForCleanup(string path)
    {
        return File.Exists(path) || Directory.Exists(path)
            ? (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0
            : false;
    }

    private static void DeleteImportSnapshot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        try
        {
            string fullPath = Path.GetFullPath(path);
            string temporaryRoot = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string name = Path.GetFileName(fullPath);
            if (!fullPath.StartsWith(temporaryRoot,
                    StringComparison.OrdinalIgnoreCase) ||
                !name.StartsWith("RdpSessionReminder-import-",
                    StringComparison.Ordinal) ||
                !string.Equals(Path.GetExtension(name), ".rdp",
                    StringComparison.OrdinalIgnoreCase) ||
                Directory.Exists(fullPath) ||
                IsReparsePointForCleanup(fullPath))
                return;
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch
        {
        }
    }

    private static void InstallApplicationFiles()
    {
        string currentSetup = Application.ExecutablePath;
        string currentDirectory = Path.GetDirectoryName(currentSetup);
        string sourceRuntime = Path.Combine(currentDirectory, AppPaths.RuntimeFileName);
        InstallApplicationFilesFrom(
            sourceRuntime, currentSetup, AppPaths.InstallDirectory);
    }

    internal static void InstallApplicationFilesFrom(string sourceRuntime,
        string currentSetup, string installDirectory)
    {
        if (string.IsNullOrWhiteSpace(sourceRuntime))
            throw new ArgumentException(
                "A runtime source path is required.", "sourceRuntime");
        if (string.IsNullOrWhiteSpace(currentSetup))
            throw new ArgumentException(
                "A setup source path is required.", "currentSetup");
        if (string.IsNullOrWhiteSpace(installDirectory))
            throw new ArgumentException(
                "An install directory is required.", "installDirectory");

        sourceRuntime = Path.GetFullPath(sourceRuntime);
        currentSetup = Path.GetFullPath(currentSetup);
        installDirectory = Path.GetFullPath(installDirectory);
        if (!File.Exists(sourceRuntime))
            throw new FileNotFoundException(
                "The runtime executable is missing. Extract and run setup from " +
                "the complete release package.", sourceRuntime);
        if (!File.Exists(currentSetup))
            throw new FileNotFoundException(
                "The setup executable is missing.", currentSetup);

        EnsureSafeInstallDirectory(installDirectory, false);
        Directory.CreateDirectory(installDirectory);
        EnsureSafeInstallDirectory(installDirectory, true);

        CopyUnlessSame(sourceRuntime, Path.Combine(installDirectory,
            AppPaths.RuntimeFileName), installDirectory);
        CopyUnlessSame(currentSetup, Path.Combine(installDirectory,
            AppPaths.SetupFileName), installDirectory);
        DeleteStaleUninstallNote(installDirectory);
    }

    internal static void DeleteStaleUninstallNote(string installDirectory)
    {
        if (string.IsNullOrEmpty(installDirectory))
            throw new ArgumentException("An install directory is required.");
        installDirectory = Path.GetFullPath(installDirectory);
        EnsureSafeInstallDirectory(installDirectory, true);
        string note = Path.Combine(installDirectory,
            "README - Saved RDP Connections.txt");
        if (File.Exists(note))
        {
            EnsureSafeInstallDirectory(installDirectory, true);
            File.Delete(note);
        }
    }

    private static void CopyUnlessSame(string source, string destination,
        string installDirectory)
    {
        EnsureSafeInstallDirectory(installDirectory, true);
        if (!File.Exists(source))
            return;
        EnsureSafeInstallFileDestination(destination);
        if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(destination),
            StringComparison.OrdinalIgnoreCase))
            return;
        if (File.Exists(destination) && FilesMatch(source, destination))
            return;

        string temporary = destination + ".installing-" +
            Guid.NewGuid().ToString("N");
        try
        {
            EnsureSafeInstallDirectory(installDirectory, true);
            File.Copy(source, temporary, false);
            if (!FilesMatch(source, temporary))
                throw new IOException(
                    "The copied application file could not be verified.");

            EnsureSafeInstallDirectory(installDirectory, true);
            EnsureSafeInstallFileDestination(destination);
            if (File.Exists(destination))
            {
                try
                {
                    File.Replace(temporary, destination, null, true);
                }
                catch (PlatformNotSupportedException)
                {
                    EnsureSafeInstallFileDestination(destination);
                    File.Delete(destination);
                    EnsureSafeInstallDirectory(installDirectory, true);
                    File.Move(temporary, destination);
                }
            }
            else
            {
                File.Move(temporary, destination);
            }
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private static void EnsureSafeInstallFileDestination(string path)
    {
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
                "An application file destination cannot be a reparse point " +
                "or file link: " + fullPath);
        if ((attributes & FileAttributes.Directory) != 0)
            throw new IOException(
                "An application file destination cannot be a directory: " +
                fullPath);
    }

    internal static void EnsureSafeInstallDirectory(string path,
        bool mustExist)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException(
                "An install directory is required.", "path");
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
                    "The application folder no longer exists: " + fullPath);
            return;
        }
        catch (DirectoryNotFoundException)
        {
            if (mustExist)
                throw new DirectoryNotFoundException(
                    "The application folder no longer exists: " + fullPath);
            return;
        }

        if ((attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException(
                "The application folder cannot be a reparse point or " +
                "directory link: " + fullPath);
        if ((attributes & FileAttributes.Directory) == 0)
            throw new IOException(
                "The application folder is not a directory: " + fullPath);
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
        Create(shortcutPath, targetPath, workingDirectory, arguments,
            Path.Combine(Environment.SystemDirectory, "mstsc.exe") + ",0");
    }

    public static void Create(string shortcutPath, string targetPath,
        string workingDirectory, string arguments, string iconLocation)
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
                    string.IsNullOrWhiteSpace(iconLocation)
                        ? Path.Combine(Environment.SystemDirectory, "mstsc.exe") + ",0"
                        : iconLocation });
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

    public static string ReadIconLocation(string shortcutPath)
    {
        return ReadStringProperty(shortcutPath, "IconLocation");
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

    public DisplayChoice(MonitorDescriptor monitor)
    {
        if (monitor == null)
            throw new ArgumentNullException("monitor");
        DeviceName = monitor.DeviceName;
        label = monitor.GetDisplayLabel();
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
