using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

internal static class UpdateUi
{
    public static UpdatePageController Attach(Form owner, TabControl tabs)
    {
        if (owner == null)
            throw new ArgumentNullException("owner");
        if (tabs == null)
            throw new ArgumentNullException("tabs");
        UpdatePageController controller = new UpdatePageController(owner, tabs);
        controller.Page.Tag = controller;
        return controller;
    }
}

internal sealed class UpdatePageController
{
    private readonly Form owner;
    private readonly TabControl tabs;
    private readonly StableVersion currentVersion;
    private readonly CheckBox automaticChecks;
    private readonly Label status;
    private readonly TextBox releaseNotes;
    private readonly Button checkButton;
    private readonly Button installButton;
    private readonly Button releasesButton;
    private UpdateCheckResult currentResult;
    private bool busy;
    private bool loadingPreference;

    public readonly TabPage Page;

    public bool IsBusy
    {
        get { return busy; }
    }

    public UpdatePageController(Form ownerForm, TabControl tabControl)
    {
        owner = ownerForm;
        tabs = tabControl;
        currentVersion = StableVersion.FromAssemblyVersion(
            Assembly.GetExecutingAssembly().GetName().Version);

        Page = new TabPage("Updates");
        Page.Padding = new Padding(12);
        tabs.TabPages.Add(Page);

        Label version = new Label();
        version.AutoSize = true;
        version.Location = new Point(18, 18);
        version.Font = new Font(owner.Font, FontStyle.Bold);
        version.Text = "Installed version: " + currentVersion.ToString();
        Page.Controls.Add(version);

        automaticChecks = new CheckBox();
        automaticChecks.AutoSize = true;
        automaticChecks.Location = new Point(21, 53);
        automaticChecks.Text =
            "Automatically check for updates and notify me (at most once per day)";
        automaticChecks.TabIndex = 0;
        Page.Controls.Add(automaticChecks);

        Label privacy = new Label();
        privacy.AutoSize = false;
        privacy.Location = new Point(21, 81);
        privacy.Size = new Size(550, 38);
        privacy.ForeColor = Color.FromArgb(75, 75, 75);
        privacy.Text =
            "Checks contact only this project's public GitHub release page. " +
            "No account, token, credentials, or telemetry are sent.";
        Page.Controls.Add(privacy);

        checkButton = new Button();
        checkButton.Location = new Point(21, 124);
        checkButton.Size = new Size(150, 30);
        checkButton.Text = "Check for updates now";
        checkButton.TabIndex = 1;
        checkButton.Click += delegate { BeginCheck(false); };
        Page.Controls.Add(checkButton);

        releasesButton = new Button();
        releasesButton.Location = new Point(181, 124);
        releasesButton.Size = new Size(174, 30);
        releasesButton.Text = "Open official releases";
        releasesButton.TabIndex = 2;
        releasesButton.Click += OpenOfficialReleases;
        Page.Controls.Add(releasesButton);

        status = new Label();
        status.AutoSize = false;
        status.Location = new Point(21, 166);
        status.Size = new Size(550, 42);
        status.Text = "Select Check for updates now to scan stable releases.";
        Page.Controls.Add(status);

        Label changesLabel = new Label();
        changesLabel.AutoSize = true;
        changesLabel.Location = new Point(18, 211);
        changesLabel.Text = "Changes after your installed version";
        Page.Controls.Add(changesLabel);

        releaseNotes = new TextBox();
        releaseNotes.Location = new Point(21, 235);
        releaseNotes.Size = new Size(550, 90);
        releaseNotes.Multiline = true;
        releaseNotes.ReadOnly = true;
        releaseNotes.MaxLength = 512 * 1024;
        releaseNotes.ScrollBars = ScrollBars.Vertical;
        releaseNotes.BackColor = SystemColors.Window;
        releaseNotes.Text = "Release notes appear here after a check.";
        releaseNotes.TabStop = false;
        Page.Controls.Add(releaseNotes);

        installButton = new Button();
        installButton.Location = new Point(357, 334);
        installButton.Size = new Size(214, 32);
        installButton.Text = "Download verified update";
        installButton.Enabled = false;
        installButton.TabIndex = 3;
        installButton.Click += DownloadAndInstall;
        Page.Controls.Add(installButton);

        Label installExplanation = new Label();
        installExplanation.AutoSize = false;
        installExplanation.Location = new Point(21, 332);
        installExplanation.Size = new Size(326, 56);
        installExplanation.ForeColor = Color.FromArgb(75, 75, 75);
        installExplanation.Text =
            "Updates go directly to the newest stable version. The app verifies " +
            "the installer SHA-256, then asks before opening it.";
        Page.Controls.Add(installExplanation);

        UpdatePreferences preferences = UpdatePreferenceStore.Load();
        loadingPreference = true;
        automaticChecks.Checked = preferences.AutomaticChecks;
        loadingPreference = false;
        automaticChecks.CheckedChanged += AutomaticChecksChanged;
        owner.Shown += delegate
        {
            UpdatePreferences current = UpdatePreferenceStore.Load();
            if (UpdatePreferenceStore.IsAutomaticCheckDue(
                    current, DateTime.UtcNow))
                BeginCheck(true);
        };
    }

    private void AutomaticChecksChanged(object sender, EventArgs eventArgs)
    {
        if (loadingPreference)
            return;
        try
        {
            UpdatePreferences preferences = UpdatePreferenceStore.Load();
            preferences.AutomaticChecks = automaticChecks.Checked;
            UpdatePreferenceStore.Save(preferences);
        }
        catch (Exception exception)
        {
            MessageBox.Show(owner,
                "The automatic update preference could not be saved.\r\n\r\n" +
                exception.Message, AppPaths.ProductName, MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void BeginCheck(bool automatic)
    {
        if (busy)
            return;
        busy = true;
        SetBusyState("Checking the official GitHub releases...", false);
        RecordCheckAttempt();

        ThreadPool.QueueUserWorkItem(delegate
        {
            UpdateCheckResult result = null;
            Exception failure = null;
            try
            {
                UpdateClient client = new UpdateClient(currentVersion);
                result = client.CheckForUpdates(currentVersion);
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            Post(delegate
            {
                busy = false;
                if (failure != null)
                {
                    currentResult = null;
                    Page.Text = "Updates";
                    status.Text =
                        "The update check failed. You can retry or open the official releases.";
                    releaseNotes.Text = failure.Message;
                    SetControlState();
                    if (!automatic)
                        MessageBox.Show(owner,
                            "The update check could not be completed.\r\n\r\n" +
                            failure.Message,
                            AppPaths.ProductName, MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    return;
                }

                currentResult = result;
                if (!result.UpdateAvailable)
                {
                    Page.Text = "Updates";
                    status.Text = "Version " + currentVersion.ToString() +
                        " is the newest stable release.";
                    releaseNotes.Text = "No newer release notes.";
                    SetControlState();
                    return;
                }

                Page.Text = "Updates (available)";
                status.Text = result.InstallerAvailable
                    ? "Version " + result.LatestRelease.Version.ToString() +
                        " is available. One installer updates directly to that version."
                    : "Version " + result.LatestRelease.Version.ToString() +
                        " is available, but its required GitHub SHA-256 asset digest " +
                        "is missing or invalid. Automatic installation is disabled; " +
                        "open the official release page.";
                releaseNotes.Text = result.GetCumulativeReleaseNotes();
                releaseNotes.SelectionStart = 0;
                releaseNotes.SelectionLength = 0;
                SetControlState();

                if (automatic && MessageBox.Show(owner,
                        "RDP Session Reminder " +
                        result.LatestRelease.Version.ToString() +
                        " is available.\r\n\r\nView the cumulative changes and " +
                        "verified update option?",
                        AppPaths.ProductName, MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information,
                        MessageBoxDefaultButton.Button1) == DialogResult.Yes)
                    tabs.SelectedTab = Page;
            });
        });
    }

    private void RecordCheckAttempt()
    {
        try
        {
            UpdatePreferences preferences = UpdatePreferenceStore.Load();
            preferences.AutomaticChecks = automaticChecks.Checked;
            preferences.LastCheckUtc = DateTime.UtcNow;
            UpdatePreferenceStore.Save(preferences);
        }
        catch
        {
            // A failed timestamp write must not block a user-requested check.
        }
    }

    private void DownloadAndInstall(object sender, EventArgs eventArgs)
    {
        if (busy || currentResult == null || !currentResult.UpdateAvailable)
            return;
        if (!currentResult.InstallerAvailable)
        {
            ShowReleaseFallback(
                "The newest release does not contain both required update files.");
            return;
        }

        busy = true;
        SetBusyState("Downloading and verifying the newest installer...", false);
        UpdateRelease release = currentResult.LatestRelease;
        ThreadPool.QueueUserWorkItem(delegate
        {
            PreparedUpdate prepared = null;
            Exception failure = null;
            try
            {
                UpdateClient client = new UpdateClient(currentVersion);
                prepared = client.DownloadAndVerify(release);
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            bool posted = Post(delegate
            {
                busy = false;
                if (failure != null)
                {
                    status.Text =
                        "The verified installer could not be prepared. Nothing was opened.";
                    SetControlState();
                    ShowReleaseFallback(failure.Message);
                    return;
                }

                status.Text = "The installer for version " +
                    release.Version.ToString() + " passed SHA-256 verification.";
                SetControlState();
                DialogResult confirmation = MessageBox.Show(owner,
                    "Version " + release.Version.ToString() +
                    " was downloaded from the official GitHub release and its " +
                    "exact SHA-256 entry was verified.\r\n\r\n" +
                    "Open the installer now? Setup will close so the installer can " +
                    "replace the current application files. Windows may still show " +
                    "an unsigned-app warning.",
                    AppPaths.ProductName, MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2);
                if (confirmation != DialogResult.Yes)
                {
                    UpdateClient.DeletePreparedUpdate(prepared);
                    return;
                }

                try
                {
                    using (VerifiedUpdateLaunch launch =
                        UpdateClient.OpenVerifiedInstallerForLaunch(prepared))
                    {
                        ProcessStartInfo startInfo = new ProcessStartInfo();
                        startInfo.FileName = launch.InstallerPath;
                        startInfo.Arguments = prepared.GetInstallerArguments(
                            Process.GetCurrentProcess().Id);
                        startInfo.WorkingDirectory = launch.WorkingDirectory;
                        startInfo.UseShellExecute = false;
                        Process installerProcess = Process.Start(startInfo);
                        if (installerProcess == null)
                            throw new InvalidOperationException(
                                "Windows did not start the verified installer.");
                        installerProcess.Dispose();
                    }
                    owner.Close();
                }
                catch (Exception exception)
                {
                    UpdateClient.DeletePreparedUpdate(prepared);
                    status.Text = "The verified installer was not opened.";
                    SetControlState();
                    ShowReleaseFallback(exception.Message);
                }
            });
            if (!posted && prepared != null)
                UpdateClient.DeletePreparedUpdate(prepared);
        });
    }

    private void SetBusyState(string message, bool installEnabled)
    {
        status.Text = message;
        checkButton.Enabled = false;
        installButton.Enabled = installEnabled;
        automaticChecks.Enabled = false;
    }

    private void SetControlState()
    {
        checkButton.Enabled = !busy;
        automaticChecks.Enabled = !busy;
        installButton.Enabled = !busy && currentResult != null &&
            currentResult.InstallerAvailable;
    }

    private void ShowReleaseFallback(string reason)
    {
        MessageBox.Show(owner,
            reason + "\r\n\r\nNo installer was opened. Use Open official releases " +
            "to review or download the release manually.",
            AppPaths.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void OpenOfficialReleases(object sender, EventArgs eventArgs)
    {
        try
        {
            Uri destination = currentResult != null &&
                currentResult.LatestRelease != null
                ? currentResult.LatestRelease.ReleasePage
                : new Uri(
                    "https://github.com/ArxivumDev/rdp-session-reminder/releases");
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = destination.AbsoluteUri;
            startInfo.UseShellExecute = true;
            Process.Start(startInfo);
        }
        catch (Exception exception)
        {
            MessageBox.Show(owner,
                "The official releases page could not be opened.\r\n\r\n" +
                exception.Message,
                AppPaths.ProductName, MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private bool Post(MethodInvoker action)
    {
        try
        {
            if (owner.IsDisposed || owner.Disposing || !owner.IsHandleCreated)
                return false;
            owner.BeginInvoke(action);
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
