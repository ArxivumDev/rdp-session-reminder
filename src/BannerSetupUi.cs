using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

// This page exists only in the visible setup process. It never installs a
// resident process, service, tray icon, or background worker.
internal sealed class BannerSetupPage
{
    private readonly Form owner;
    private readonly TextBox shortLabel;
    private readonly ComboBox preset;
    private readonly TextBox background;
    private readonly TextBox foreground;
    private readonly ComboBox corner;
    private readonly NumericUpDown verticalOffset;
    private readonly ComboBox size;
    private readonly NumericUpDown opacity;
    private readonly CheckBox idleDimming;
    private readonly BannerSampleControl sample;

    public BannerSetupPage(Form owner, TabControl tabs)
    {
        if (owner == null)
            throw new ArgumentNullException("owner");
        if (tabs == null)
            throw new ArgumentNullException("tabs");
        this.owner = owner;

        TabPage page = new TabPage("Reminder appearance");
        page.Padding = new Padding(12);
        tabs.TabPages.Add(page);

        Label labelHelp = MakeLabel(18, 15, 550, 35,
            "Optional short label (24 characters). Leave blank to use the full " +
            "Reminder text from the Connection tab.");
        page.Controls.Add(labelHelp);

        shortLabel = new TextBox();
        shortLabel.Location = new Point(21, 51);
        shortLabel.Size = new Size(250, 25);
        shortLabel.MaxLength = 24;
        shortLabel.TextChanged += RefreshSample;
        page.Controls.Add(shortLabel);

        Label presetLabel = MakeLabel(298, 20, 100, 22, "Color preset");
        page.Controls.Add(presetLabel);
        preset = new ComboBox();
        preset.DropDownStyle = ComboBoxStyle.DropDownList;
        preset.Location = new Point(401, 17);
        preset.Size = new Size(170, 25);
        preset.Items.AddRange(new object[] {
            "Default", "Work", "Personal", "Test", "Production", "Custom"
        });
        preset.SelectedIndex = 0;
        preset.SelectedIndexChanged += PresetChanged;
        page.Controls.Add(preset);

        Label backgroundLabel = MakeLabel(18, 94, 116, 22, "Background color");
        page.Controls.Add(backgroundLabel);
        background = CreateColorBox(page, 138, 91, "#182335");
        Button backgroundButton = CreateColorButton(page, 232, 90, "Choose...",
            delegate { ChooseColor(background); });

        Label foregroundLabel = MakeLabel(298, 94, 92, 22, "Text color");
        page.Controls.Add(foregroundLabel);
        foreground = CreateColorBox(page, 401, 91, "#FFFFFF");
        Button foregroundButton = CreateColorButton(page, 495, 90, "Choose...",
            delegate { ChooseColor(foreground); });

        Label cornerLabel = MakeLabel(18, 139, 116, 22, "Screen corner");
        page.Controls.Add(cornerLabel);
        corner = new ComboBox();
        corner.DropDownStyle = ComboBoxStyle.DropDownList;
        corner.Location = new Point(138, 136);
        corner.Size = new Size(135, 25);
        corner.Items.AddRange(new object[] {
            "BottomRight", "BottomLeft", "TopRight", "TopLeft"
        });
        corner.SelectedIndex = 0;
        corner.SelectedIndexChanged += RefreshSample;
        page.Controls.Add(corner);

        Label offsetLabel = MakeLabel(298, 139, 100, 22, "Vertical offset");
        page.Controls.Add(offsetLabel);
        verticalOffset = new NumericUpDown();
        verticalOffset.Location = new Point(401, 136);
        verticalOffset.Size = new Size(95, 25);
        verticalOffset.Minimum = 0;
        verticalOffset.Maximum = 600;
        verticalOffset.Value = 64;
        verticalOffset.Increment = 8;
        verticalOffset.ValueChanged += RefreshSample;
        page.Controls.Add(verticalOffset);
        page.Controls.Add(MakeLabel(501, 139, 70, 22, "pixels"));

        Label sizeLabel = MakeLabel(18, 183, 116, 22, "Text size");
        page.Controls.Add(sizeLabel);
        size = new ComboBox();
        size.DropDownStyle = ComboBoxStyle.DropDownList;
        size.Location = new Point(138, 180);
        size.Size = new Size(135, 25);
        size.Items.AddRange(new object[] { "Small", "Medium", "Large" });
        size.SelectedIndex = 1;
        size.SelectedIndexChanged += RefreshSample;
        page.Controls.Add(size);

        Label opacityLabel = MakeLabel(298, 183, 100, 22, "Opacity");
        page.Controls.Add(opacityLabel);
        opacity = new NumericUpDown();
        opacity.Location = new Point(401, 180);
        opacity.Size = new Size(95, 25);
        opacity.Minimum = 35;
        opacity.Maximum = 100;
        opacity.Value = 97;
        opacity.ValueChanged += RefreshSample;
        page.Controls.Add(opacity);
        page.Controls.Add(MakeLabel(501, 183, 70, 22, "percent"));

        idleDimming = new CheckBox();
        idleDimming.AutoSize = true;
        idleDimming.Location = new Point(21, 224);
        idleDimming.Text =
            "Dim the reminder after local input is idle (it remains visible)";
        idleDimming.Checked = false;
        page.Controls.Add(idleDimming);

        sample = new BannerSampleControl();
        sample.Location = new Point(21, 258);
        sample.Size = new Size(414, 96);
        page.Controls.Add(sample);

        Button testButton = new Button();
        testButton.Location = new Point(449, 279);
        testButton.Size = new Size(122, 32);
        testButton.Text = "Test reminder";
        testButton.Click += ShowTestReminder;
        page.Controls.Add(testButton);

        Label lifecycle = MakeLabel(21, 363, 550, 31,
            "These choices are saved to this shortcut only. Setup exits after " +
            "creation; only the small reminder runs while that RDP launch is active.");
        lifecycle.ForeColor = Color.FromArgb(75, 75, 75);
        page.Controls.Add(lifecycle);

        background.TextChanged += CustomColorChanged;
        foreground.TextChanged += CustomColorChanged;
        RefreshSample(null, EventArgs.Empty);
    }

    public void ApplyTo(ReminderSettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException("settings");
        settings.ShortLabel = SettingsStore.NormalizeShortLabel(shortLabel.Text);
        settings.BannerPreset = SettingsStore.NormalizeBannerPreset(
            Convert.ToString(preset.SelectedItem, CultureInfo.InvariantCulture));
        settings.BannerBackground = SettingsStore.NormalizeBannerColor(
            background.Text,
            SettingsStore.GetPresetBackground(settings.BannerPreset));
        settings.BannerForeground = SettingsStore.NormalizeBannerColor(
            foreground.Text,
            SettingsStore.GetPresetForeground(settings.BannerPreset));
        settings.BannerCorner = SettingsStore.NormalizeBannerCorner(
            Convert.ToString(corner.SelectedItem, CultureInfo.InvariantCulture));
        settings.BannerVerticalOffset = SettingsStore.NormalizeBannerVerticalOffset(
            Decimal.ToInt32(verticalOffset.Value));
        settings.BannerSize = SettingsStore.NormalizeBannerSize(
            Convert.ToString(size.SelectedItem, CultureInfo.InvariantCulture));
        settings.BannerOpacity = SettingsStore.NormalizeBannerOpacity(
            Decimal.ToInt32(opacity.Value));
        settings.IdleDimming = idleDimming.Checked;
    }

    public void LoadFrom(ReminderSettings settings)
    {
        if (settings == null)
            return;
        shortLabel.Text = SettingsStore.NormalizeShortLabel(settings.ShortLabel);
        SelectValue(preset, SettingsStore.NormalizeBannerPreset(
            settings.BannerPreset));
        background.Text = SettingsStore.NormalizeBannerColor(
            settings.BannerBackground,
            SettingsStore.GetPresetBackground(settings.BannerPreset));
        foreground.Text = SettingsStore.NormalizeBannerColor(
            settings.BannerForeground,
            SettingsStore.GetPresetForeground(settings.BannerPreset));
        SelectValue(corner, SettingsStore.NormalizeBannerCorner(
            settings.BannerCorner));
        verticalOffset.Value = SettingsStore.NormalizeBannerVerticalOffset(
            settings.BannerVerticalOffset);
        SelectValue(size, SettingsStore.NormalizeBannerSize(settings.BannerSize));
        opacity.Value = SettingsStore.NormalizeBannerOpacity(
            settings.BannerOpacity);
        idleDimming.Checked = settings.IdleDimming;
        RefreshSample(null, EventArgs.Empty);
    }

    private static Label MakeLabel(int left, int top, int width, int height,
        string text)
    {
        Label label = new Label();
        label.AutoSize = false;
        label.Location = new Point(left, top);
        label.Size = new Size(width, height);
        label.Text = text;
        return label;
    }

    private static TextBox CreateColorBox(Control parent, int left, int top,
        string value)
    {
        TextBox box = new TextBox();
        box.Location = new Point(left, top);
        box.Size = new Size(88, 25);
        box.MaxLength = 7;
        box.Text = value;
        parent.Controls.Add(box);
        return box;
    }

    private static Button CreateColorButton(Control parent, int left, int top,
        string text, EventHandler handler)
    {
        Button button = new Button();
        button.Location = new Point(left, top);
        button.Size = new Size(65, 28);
        button.Text = text;
        button.Click += handler;
        parent.Controls.Add(button);
        return button;
    }

    private void ChooseColor(TextBox target)
    {
        Color fallback = ParseColor(target.Text, Color.White);
        using (ColorDialog dialog = new ColorDialog())
        {
            dialog.Color = fallback;
            dialog.FullOpen = true;
            if (dialog.ShowDialog(owner) != DialogResult.OK)
                return;
            target.Text = "#" + dialog.Color.R.ToString("X2") +
                dialog.Color.G.ToString("X2") +
                dialog.Color.B.ToString("X2");
            SelectValue(preset, "Custom");
        }
    }

    private void PresetChanged(object sender, EventArgs eventArgs)
    {
        string value = Convert.ToString(preset.SelectedItem,
            CultureInfo.InvariantCulture);
        if (!string.Equals(value, "Custom", StringComparison.Ordinal))
        {
            background.Text = SettingsStore.GetPresetBackground(value);
            foreground.Text = SettingsStore.GetPresetForeground(value);
        }
        RefreshSample(sender, eventArgs);
    }

    private void CustomColorChanged(object sender, EventArgs eventArgs)
    {
        sample.BackColor = ParseColor(background.Text,
            Color.FromArgb(24, 35, 53));
        sample.ForeColor = ParseColor(foreground.Text, Color.White);
        sample.Invalidate();
    }

    private void RefreshSample(object sender, EventArgs eventArgs)
    {
        if (sample == null)
            return;
        sample.SampleText = shortLabel.Text.Trim().Length == 0
            ? "REMOTE SESSION - COMPUTER"
            : shortLabel.Text.Trim();
        sample.SampleSize = Convert.ToString(size.SelectedItem,
            CultureInfo.InvariantCulture);
        sample.SampleOpacity = Decimal.ToInt32(opacity.Value);
        sample.Corner = Convert.ToString(corner.SelectedItem,
            CultureInfo.InvariantCulture);
        CustomColorChanged(sender, eventArgs);
    }

    private void ShowTestReminder(object sender, EventArgs eventArgs)
    {
        using (BannerTestForm form = new BannerTestForm(
            sample.SampleText, sample.BackColor, sample.ForeColor,
            sample.SampleSize, sample.SampleOpacity))
        {
            form.ShowDialog(owner);
        }
    }

    private static void SelectValue(ComboBox combo, string value)
    {
        for (int index = 0; index < combo.Items.Count; index++)
        {
            if (string.Equals(Convert.ToString(combo.Items[index],
                    CultureInfo.InvariantCulture), value,
                    StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedIndex = index;
                return;
            }
        }
        if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    private static Color ParseColor(string value, Color fallback)
    {
        try
        {
            string normalized = SettingsStore.NormalizeBannerColor(value,
                "#" + fallback.R.ToString("X2") +
                fallback.G.ToString("X2") + fallback.B.ToString("X2"));
            return ColorTranslator.FromHtml(normalized);
        }
        catch
        {
            return fallback;
        }
    }
}

internal sealed class BannerSampleControl : Control
{
    public string SampleText = "REMOTE SESSION - COMPUTER";
    public string SampleSize = "Medium";
    public int SampleOpacity = 97;
    public string Corner = "BottomRight";

    public BannerSampleControl()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(24, 35, 53);
        ForeColor = Color.White;
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        int fontSize = SampleSize == "Small" ? 9 :
            SampleSize == "Large" ? 14 : 11;
        using (Font font = new Font("Segoe UI Semibold", fontSize,
            FontStyle.Bold))
        using (Brush brush = new SolidBrush(Color.FromArgb(
            Math.Max(35, Math.Min(100, SampleOpacity)) * 255 / 100,
            ForeColor)))
        {
            StringFormat format = new StringFormat();
            format.Alignment = StringAlignment.Center;
            format.LineAlignment = StringAlignment.Center;
            eventArgs.Graphics.DrawString(SampleText, font, brush,
                ClientRectangle, format);
            format.Dispose();
        }
    }
}

internal sealed class BannerTestForm : Form
{
    public BannerTestForm(string text, Color background, Color foreground,
        string size, int opacity)
    {
        Text = "Reminder preview";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        ShowInTaskbar = false;
        TopMost = true;
        ClientSize = new Size(size == "Large" ? 440 : 390,
            size == "Small" ? 48 : size == "Large" ? 68 : 56);
        BackColor = background;
        Opacity = Math.Max(35, Math.Min(100, opacity)) / 100.0;

        Label label = new Label();
        label.Dock = DockStyle.Fill;
        label.TextAlign = ContentAlignment.MiddleCenter;
        label.ForeColor = foreground;
        label.Font = new Font("Segoe UI Semibold",
            size == "Small" ? 9f : size == "Large" ? 14f : 11f,
            FontStyle.Bold);
        label.Text = text;
        label.Click += delegate { Close(); };
        Controls.Add(label);

        ToolTip tip = new ToolTip();
        tip.SetToolTip(label, "Click to close this setup-only preview.");
    }
}
