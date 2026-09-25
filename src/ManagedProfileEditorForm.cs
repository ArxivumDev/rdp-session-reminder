using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

internal sealed class ManagedProfileEditorForm : Form
{
    private readonly string profileId;
    private readonly string settingsPath;
    private readonly ReminderSettings settings;
    private readonly TextBox shortcutName;
    private readonly TextBox reminderText;
    private readonly ComboBox display;
    private readonly ComboBox icon;
    private readonly BannerSetupPage banner;
    private readonly Label status;
    private readonly string originalShortcutName;

    public ManagedProfileEditorForm(string managedProfileId)
    {
        if (!SettingsStore.IsValidProfileId(managedProfileId) ||
            !SettingsStore.TryLoadProfile(managedProfileId, out settings))
            throw new InvalidDataException(
                "The selected connection profile is missing or invalid.");
        profileId = managedProfileId;
        settingsPath = AppPaths.GetProfileSettingsPath(profileId);
        originalShortcutName = settings.ShortcutName;

        Text = "Edit connection reminder";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(670, 688);
        ClientSize = new Size(670, 650);
        Font = new Font("Segoe UI", 9.5f);
        ShowInTaskbar = false;

        Label explanation = new Label();
        explanation.AutoSize = false;
        explanation.Location = new Point(22, 18);
        explanation.Size = new Size(626, 42);
        explanation.Text =
            "Change this connection's reminder, display, colors, and shortcut " +
            "icon. The saved RDP connection file is left unchanged.";
        Controls.Add(explanation);

        TabControl tabs = new TabControl();
        tabs.Location = new Point(18, 67);
        tabs.Size = new Size(634, 475);
        tabs.Anchor = AnchorStyles.Top | AnchorStyles.Bottom |
            AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(tabs);

        TabPage general = new TabPage("Connection label");
        general.Padding = new Padding(12);
        tabs.TabPages.Add(general);

        AddLabel(general, 18, 18, 570, 22,
            "Remote target (read-only in the reminder editor)");
        TextBox target = new TextBox();
        target.Location = new Point(21, 42);
        target.Size = new Size(570, 25);
        target.ReadOnly = true;
        target.Text = settings.ComputerName;
        general.Controls.Add(target);

        AddLabel(general, 18, 85, 570, 22, "Desktop shortcut name");
        shortcutName = new TextBox();
        shortcutName.Location = new Point(21, 109);
        shortcutName.Size = new Size(570, 25);
        shortcutName.MaxLength = 80;
        shortcutName.Text = settings.ShortcutName;
        general.Controls.Add(shortcutName);

        AddLabel(general, 18, 153, 570, 22, "Full reminder text");
        reminderText = new TextBox();
        reminderText.Location = new Point(21, 177);
        reminderText.Size = new Size(570, 25);
        reminderText.MaxLength = 100;
        reminderText.Text = settings.ReminderText;
        general.Controls.Add(reminderText);

        AddLabel(general, 18, 221, 570, 22,
            "Local display for the reminder");
        display = new ComboBox();
        display.DropDownStyle = ComboBoxStyle.DropDownList;
        display.Location = new Point(21, 245);
        display.Size = new Size(570, 25);
        foreach (MonitorDescriptor monitor in
            new ScreenMonitorTopologySource().GetMonitors())
        {
            DisplayChoice choice = new DisplayChoice(monitor);
            int index = display.Items.Add(choice);
            if (string.Equals(choice.DeviceName, settings.DisplayDevice,
                    StringComparison.OrdinalIgnoreCase) ||
                (display.SelectedIndex < 0 && monitor.IsPrimary))
                display.SelectedIndex = index;
        }
        if (display.SelectedIndex < 0 && display.Items.Count > 0)
            display.SelectedIndex = 0;
        general.Controls.Add(display);

        AddLabel(general, 18, 289, 570, 22, "Desktop shortcut icon");
        icon = new ComboBox();
        icon.DropDownStyle = ComboBoxStyle.DropDownList;
        icon.Location = new Point(21, 313);
        icon.Size = new Size(570, 25);
        foreach (ShortcutIconChoice choice in ShortcutIconCatalog.GetChoices())
        {
            int index = icon.Items.Add(choice);
            if (string.Equals(choice.Kind.ToString(),
                    SettingsStore.NormalizeShortcutIcon(settings.ShortcutIcon),
                    StringComparison.OrdinalIgnoreCase))
                icon.SelectedIndex = index;
        }
        if (icon.SelectedIndex < 0 && icon.Items.Count > 0)
            icon.SelectedIndex = 0;
        general.Controls.Add(icon);

        Label note = AddLabel(general, 21, 354, 570, 52,
            "Saving recreates this app's desktop shortcut with a blank Start in " +
            "field. If its old shortcut was moved elsewhere, that moved copy is " +
            "left alone and can be deleted manually.");
        note.ForeColor = Color.FromArgb(75, 75, 75);

        banner = new BannerSetupPage(this, tabs, "Reminder appearance");
        banner.LoadFrom(settings);

        status = new Label();
        status.Location = new Point(22, 551);
        status.Size = new Size(410, 50);
        status.Anchor = AnchorStyles.Left | AnchorStyles.Right |
            AnchorStyles.Bottom;
        status.ForeColor = Color.FromArgb(65, 65, 65);
        Controls.Add(status);

        Button save = new Button();
        save.Location = new Point(438, 598);
        save.Size = new Size(128, 32);
        save.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        save.Text = "Save changes";
        save.Click += SaveChanges;
        Controls.Add(save);

        Button cancel = new Button();
        cancel.Location = new Point(574, 598);
        cancel.Size = new Size(78, 32);
        cancel.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        cancel.Text = "Cancel";
        cancel.DialogResult = DialogResult.Cancel;
        Controls.Add(cancel);
        AcceptButton = save;
        CancelButton = cancel;
        SetupVisualTheme.Apply(this);
    }

    private static Label AddLabel(Control parent, int left, int top,
        int width, int height, string text)
    {
        Label label = new Label();
        label.AutoSize = false;
        label.Location = new Point(left, top);
        label.Size = new Size(width, height);
        label.Text = text;
        parent.Controls.Add(label);
        return label;
    }

    private void SaveChanges(object sender, EventArgs eventArgs)
    {
        string oldShortcutPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            originalShortcutName + ".lnk");
        bool removeOldShortcut = File.Exists(oldShortcutPath) &&
            SetupForm.IsOwnedShortcut(oldShortcutPath, profileId);
        byte[] originalSettings = File.ReadAllBytes(settingsPath);

        try
        {
            settings.ShortcutName = SettingsStore.NormalizeShortcutName(
                shortcutName.Text, settings.ComputerName);
            settings.ReminderText = SettingsStore.NormalizeReminderText(
                reminderText.Text, settings.ComputerName);
            DisplayChoice selectedDisplay = display.SelectedItem as DisplayChoice;
            settings.DisplayDevice = selectedDisplay == null
                ? ""
                : selectedDisplay.DeviceName;
            ShortcutIconChoice selectedIcon =
                icon.SelectedItem as ShortcutIconChoice;
            settings.ShortcutIcon = selectedIcon == null
                ? "WindowsRemoteDesktop"
                : selectedIcon.Kind.ToString();
            banner.ApplyTo(settings);

            SettingsStore.SaveTo(settingsPath, settings);
            string newShortcutPath =
                SetupForm.RepairManagedShortcut(profileId);
            if (removeOldShortcut && !string.Equals(
                    Path.GetFullPath(oldShortcutPath),
                    Path.GetFullPath(newShortcutPath),
                    StringComparison.OrdinalIgnoreCase) &&
                SetupForm.IsOwnedShortcut(oldShortcutPath, profileId))
                File.Delete(oldShortcutPath);

            status.Text = "Saved. The full configuration window uses no memory " +
                "after it closes.";
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            try
            {
                File.WriteAllBytes(settingsPath, originalSettings);
            }
            catch
            {
            }
            status.Text = "Changes were not completed.";
            MessageBox.Show(this, exception.Message, AppPaths.ProductName,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
