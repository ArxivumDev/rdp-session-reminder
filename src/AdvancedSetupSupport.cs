using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

// Configuration-time helpers only. Nothing in this file starts a service,
// background worker, tray process, or Remote Desktop client implementation.

internal sealed class MonitorDescriptor
{
    public int MstscId { get; private set; }
    public int DisplayNumber { get { return MstscId + 1; } }
    public string DeviceName { get; private set; }
    public Rectangle Bounds { get; private set; }
    public Rectangle WorkingArea { get; private set; }
    public bool IsPrimary { get; private set; }

    public MonitorDescriptor(int mstscId, string deviceName, Rectangle bounds,
        Rectangle workingArea, bool isPrimary)
    {
        if (mstscId < 0)
            throw new ArgumentOutOfRangeException("mstscId");
        if (bounds.Width <= 0 || bounds.Height <= 0)
            throw new ArgumentException("A monitor must have positive dimensions.",
                "bounds");

        MstscId = mstscId;
        DeviceName = string.IsNullOrWhiteSpace(deviceName)
            ? "Display " + DisplayNumber.ToString(CultureInfo.InvariantCulture)
            : deviceName.Trim();
        Bounds = bounds;
        WorkingArea = workingArea;
        IsPrimary = isPrimary;
    }

    public string GetDisplayLabel()
    {
        return string.Format(CultureInfo.CurrentCulture,
            "Display {0} - {1} x {2}{3} (RDP ID {4})", DisplayNumber,
            Bounds.Width, Bounds.Height, IsPrimary ? " (primary)" : "",
            MstscId);
    }

    public override string ToString()
    {
        return GetDisplayLabel();
    }
}

internal interface IMonitorTopologySource
{
    IList<MonitorDescriptor> GetMonitors();
}

internal sealed class ScreenMonitorTopologySource : IMonitorTopologySource
{
    private delegate bool MonitorEnumProcedure(IntPtr monitor,
        IntPtr deviceContext, IntPtr rectangle, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr deviceContext,
        IntPtr clipRectangle, MonitorEnumProcedure procedure, IntPtr data);

    public IList<MonitorDescriptor> GetMonitors()
    {
        List<MonitorDescriptor> monitors = new List<MonitorDescriptor>();
        HashSet<string> devices = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        MonitorEnumProcedure callback = delegate(IntPtr monitor,
            IntPtr deviceContext, IntPtr rectangle, IntPtr data)
        {
            Screen screen = Screen.FromHandle(monitor);
            if (screen == null || screen.Bounds.Width <= 0 ||
                screen.Bounds.Height <= 0 || !devices.Add(screen.DeviceName))
                return true;
            monitors.Add(new MonitorDescriptor(
                monitors.Count, screen.DeviceName,
                screen.Bounds, screen.WorkingArea, screen.Primary));
            return true;
        };
        bool enumerated = false;
        try
        {
            enumerated = EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                callback, IntPtr.Zero);
        }
        catch
        {
            enumerated = false;
        }
        GC.KeepAlive(callback);

        if (!enumerated || monitors.Count == 0)
        {
            monitors.Clear();
            Screen[] screens = Screen.AllScreens;
            for (int index = 0; index < screens.Length; index++)
            {
                Screen screen = screens[index];
                monitors.Add(new MonitorDescriptor(index, screen.DeviceName,
                    screen.Bounds, screen.WorkingArea, screen.Primary));
            }
        }
        return monitors;
    }

    // Kept as a small compatibility seam for tests and older callers. RDP IDs
    // are based on the native monitor enumeration order, never DISPLAYn text.
    internal static int GetMstscId(string deviceName, int fallback)
    {
        return Math.Max(0, fallback);
    }
}

internal sealed class FixedMonitorTopologySource : IMonitorTopologySource
{
    private readonly List<MonitorDescriptor> monitors;

    public FixedMonitorTopologySource(IEnumerable<MonitorDescriptor> monitors)
    {
        if (monitors == null)
            throw new ArgumentNullException("monitors");
        this.monitors = new List<MonitorDescriptor>(monitors);
        MonitorSelection.ValidateTopology(this.monitors);
    }

    public IList<MonitorDescriptor> GetMonitors()
    {
        return new List<MonitorDescriptor>(monitors);
    }
}

internal sealed class MonitorSelection
{
    private readonly List<MonitorDescriptor> topology;
    private readonly List<int> remoteMonitorIds;

    public IList<MonitorDescriptor> Topology
    {
        get { return new List<MonitorDescriptor>(topology); }
    }

    public IList<int> RemoteMonitorIds
    {
        get { return new List<int>(remoteMonitorIds); }
    }

    public int ReminderMonitorId { get; private set; }

    public string ReminderDeviceName
    {
        get { return FindMonitor(ReminderMonitorId).DeviceName; }
    }

    // selectedmonitors is effective only with multimon enabled. Enabling it for
    // a one-monitor selection is intentional: it lets a non-primary monitor be
    // selected explicitly rather than silently falling back to the primary one.
    public bool UseMultimon { get { return remoteMonitorIds.Count > 0; } }

    public string SelectedMonitorsSetting
    {
        get
        {
            string[] values = new string[remoteMonitorIds.Count];
            for (int index = 0; index < remoteMonitorIds.Count; index++)
            {
                values[index] = remoteMonitorIds[index].ToString(
                    CultureInfo.InvariantCulture);
            }
            return string.Join(",", values);
        }
    }

    public MonitorSelection(IEnumerable<MonitorDescriptor> topology,
        IEnumerable<int> remoteMonitorIds, int reminderMonitorId)
    {
        if (topology == null)
            throw new ArgumentNullException("topology");
        if (remoteMonitorIds == null)
            throw new ArgumentNullException("remoteMonitorIds");

        this.topology = new List<MonitorDescriptor>(topology);
        // Keep the caller's order. Windows makes the first selectedmonitors ID
        // the primary display inside the remote session.
        this.remoteMonitorIds = new List<int>(remoteMonitorIds);
        ReminderMonitorId = reminderMonitorId;
        Validate();
    }

    internal static void ValidateTopology(IList<MonitorDescriptor> monitors)
    {
        if (monitors == null || monitors.Count == 0)
            throw new InvalidOperationException("No local monitors were detected.");

        HashSet<int> ids = new HashSet<int>();
        foreach (MonitorDescriptor monitor in monitors)
        {
            if (monitor == null || !ids.Add(monitor.MstscId))
                throw new InvalidOperationException(
                    "Monitor identifiers must be present and unique.");
        }
    }

    private void Validate()
    {
        ValidateTopology(topology);
        if (remoteMonitorIds.Count == 0)
            throw new InvalidOperationException(
                "Select at least one monitor for the Remote Desktop session.");

        HashSet<int> available = new HashSet<int>();
        foreach (MonitorDescriptor monitor in topology)
            available.Add(monitor.MstscId);

        HashSet<int> selected = new HashSet<int>();
        foreach (int id in remoteMonitorIds)
        {
            if (!available.Contains(id))
                throw new InvalidOperationException(
                    "A selected Remote Desktop monitor is no longer available.");
            if (!selected.Add(id))
                throw new InvalidOperationException(
                    "A Remote Desktop monitor was selected more than once.");
        }

        if (!available.Contains(ReminderMonitorId))
            throw new InvalidOperationException(
                "The reminder monitor is no longer available.");
    }

    public string[] GetCompatibilityWarnings()
    {
        if (remoteMonitorIds.Count < 2)
            return new string[0];

        HashSet<int> selected = new HashSet<int>(remoteMonitorIds);
        HashSet<int> reached = new HashSet<int>();
        Queue<int> pending = new Queue<int>();
        pending.Enqueue(remoteMonitorIds[0]);
        reached.Add(remoteMonitorIds[0]);

        while (pending.Count > 0)
        {
            int current = pending.Dequeue();
            Rectangle currentBounds = FindMonitor(current).Bounds;
            foreach (int candidate in remoteMonitorIds)
            {
                if (reached.Contains(candidate))
                    continue;
                if (SharesAnEdge(currentBounds, FindMonitor(candidate).Bounds))
                {
                    reached.Add(candidate);
                    pending.Enqueue(candidate);
                }
            }
        }

        if (reached.Count != selected.Count)
        {
            return new string[] {
                "The selected monitors do not form one edge-connected group. " +
                "Windows Remote Desktop may reject this monitor layout."
            };
        }
        return new string[0];
    }

    public void ValidateForRdp()
    {
        string[] warnings = GetCompatibilityWarnings();
        if (warnings.Length > 0)
            throw new InvalidOperationException(warnings[0]);
    }

    public int RemotePrimaryMonitorId
    {
        get { return remoteMonitorIds[0]; }
    }

    private MonitorDescriptor FindMonitor(int id)
    {
        foreach (MonitorDescriptor monitor in topology)
        {
            if (monitor.MstscId == id)
                return monitor;
        }
        throw new InvalidOperationException("The monitor is unavailable.");
    }

    private static bool SharesAnEdge(Rectangle first, Rectangle second)
    {
        bool verticalEdge = (first.Right == second.Left ||
            second.Right == first.Left) &&
            Math.Min(first.Bottom, second.Bottom) >
                Math.Max(first.Top, second.Top);
        bool horizontalEdge = (first.Bottom == second.Top ||
            second.Bottom == first.Top) &&
            Math.Min(first.Right, second.Right) >
                Math.Max(first.Left, second.Left);
        return verticalEdge || horizontalEdge;
    }
}

internal sealed class MonitorLayoutControl : Control
{
    private readonly List<MonitorDescriptor> topology =
        new List<MonitorDescriptor>();
    private readonly List<int> remoteMonitorIds = new List<int>();
    private int reminderMonitorId = -1;

    public event EventHandler SelectionChanged;
    public event EventHandler ReminderMonitorChanged;

    public MonitorLayoutControl()
    {
        DoubleBuffered = true;
        MinimumSize = new Size(360, 180);
        BackColor = SetupPalette.Input;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    public IList<MonitorDescriptor> Topology
    {
        get { return new List<MonitorDescriptor>(topology); }
    }

    public IList<int> RemoteMonitorIds
    {
        get { return new List<int>(remoteMonitorIds); }
    }

    public int ReminderMonitorId { get { return reminderMonitorId; } }

    public void SetTopology(IEnumerable<MonitorDescriptor> monitors)
    {
        if (monitors == null)
            throw new ArgumentNullException("monitors");
        List<MonitorDescriptor> replacement =
            new List<MonitorDescriptor>(monitors);
        MonitorSelection.ValidateTopology(replacement);

        topology.Clear();
        topology.AddRange(replacement);
        remoteMonitorIds.Clear();
        MonitorDescriptor primary = GetPrimaryOrFirst();
        remoteMonitorIds.Add(primary.MstscId);
        foreach (MonitorDescriptor monitor in topology)
        {
            if (monitor.MstscId != primary.MstscId)
                remoteMonitorIds.Add(monitor.MstscId);
        }
        reminderMonitorId = primary.MstscId;
        Invalidate();
    }

    public void SetSelection(IEnumerable<int> remoteIds, int reminderId)
    {
        MonitorSelection selection = new MonitorSelection(topology, remoteIds,
            reminderId);
        remoteMonitorIds.Clear();
        foreach (int id in selection.RemoteMonitorIds)
            remoteMonitorIds.Add(id);
        reminderMonitorId = reminderId;
        Invalidate();
    }

    public MonitorSelection GetSelection()
    {
        return new MonitorSelection(topology, RemoteMonitorIds,
            reminderMonitorId);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        MonitorDescriptor hit = HitTest(e.Location);
        if (hit == null)
            return;

        if (e.Button == MouseButtons.Right)
        {
            reminderMonitorId = hit.MstscId;
            Invalidate();
            OnReminderMonitorChanged(EventArgs.Empty);
            return;
        }

        if (e.Button == MouseButtons.Left)
        {
            if (remoteMonitorIds.Contains(hit.MstscId))
            {
                if (remoteMonitorIds.Count > 1)
                    remoteMonitorIds.Remove(hit.MstscId);
            }
            else
            {
                remoteMonitorIds.Add(hit.MstscId);
            }
            Invalidate();
            OnSelectionChanged(EventArgs.Empty);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (topology.Count == 0)
            return;

        e.Graphics.SmoothingMode =
            System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        UiThemePalette palette = SetupPalette.Current;
        BackColor = palette.Input;
        foreach (MonitorDescriptor monitor in topology)
        {
            Rectangle rectangle = GetClientRectangle(monitor.Bounds);
            bool selected = remoteMonitorIds.Contains(monitor.MstscId);
            Color fillColor = selected
                ? palette.Selection
                : palette.Raised;
            Color borderColor = selected
                ? palette.Accent
                : palette.Border;
            using (Brush fill = new SolidBrush(fillColor))
            using (Pen border = new Pen(borderColor, selected ? 3f : 2f))
            {
                e.Graphics.FillRectangle(fill, rectangle);
                e.Graphics.DrawRectangle(border, rectangle);
            }

            string number = monitor.DisplayNumber.ToString(
                CultureInfo.InvariantCulture);
            float fontSize = Math.Max(18f,
                Math.Min(42f, rectangle.Height * 0.35f));
            using (Font numberFont = new Font(Font.FontFamily, fontSize,
                FontStyle.Bold, GraphicsUnit.Pixel))
            {
                TextRenderer.DrawText(e.Graphics, number, numberFont, rectangle,
                    selected ? palette.SelectionText : palette.MutedInk,
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding);
            }

            string details = monitor.Bounds.Width.ToString(
                CultureInfo.InvariantCulture) + " x " +
                monitor.Bounds.Height.ToString(CultureInfo.InvariantCulture) +
                (monitor.IsPrimary ? "  Primary" : "");
            Rectangle detailsArea = new Rectangle(rectangle.Left + 5,
                rectangle.Bottom - 24, rectangle.Width - 10, 19);
            TextRenderer.DrawText(e.Graphics, details, Font, detailsArea,
                selected ? palette.SelectionText : palette.MutedInk,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPadding);

            if (monitor.MstscId == reminderMonitorId)
            {
                Rectangle badge = new Rectangle(rectangle.Right - 31,
                    rectangle.Top + 6, 24, 20);
                using (Brush badgeBrush = new SolidBrush(
                    Color.FromArgb(240, 132, 35)))
                    e.Graphics.FillEllipse(badgeBrush, badge);
                TextRenderer.DrawText(e.Graphics, "R", Font, badge, Color.White,
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding);
            }
        }
    }

    private MonitorDescriptor HitTest(Point clientPoint)
    {
        foreach (MonitorDescriptor monitor in topology)
        {
            if (GetClientRectangle(monitor.Bounds).Contains(clientPoint))
                return monitor;
        }
        return null;
    }

    private Rectangle GetClientRectangle(Rectangle monitorBounds)
    {
        Rectangle virtualBounds = topology[0].Bounds;
        for (int index = 1; index < topology.Count; index++)
            virtualBounds = Rectangle.Union(virtualBounds, topology[index].Bounds);

        const int padding = 18;
        float scaleX = (ClientSize.Width - padding * 2f) / virtualBounds.Width;
        float scaleY = (ClientSize.Height - padding * 2f) / virtualBounds.Height;
        float scale = Math.Max(0.01f, Math.Min(scaleX, scaleY));
        float contentWidth = virtualBounds.Width * scale;
        float contentHeight = virtualBounds.Height * scale;
        float originX = (ClientSize.Width - contentWidth) / 2f;
        float originY = (ClientSize.Height - contentHeight) / 2f;

        return new Rectangle(
            (int)Math.Round(originX +
                (monitorBounds.Left - virtualBounds.Left) * scale),
            (int)Math.Round(originY +
                (monitorBounds.Top - virtualBounds.Top) * scale),
            Math.Max(20, (int)Math.Round(monitorBounds.Width * scale)),
            Math.Max(20, (int)Math.Round(monitorBounds.Height * scale)));
    }

    private MonitorDescriptor GetPrimaryOrFirst()
    {
        foreach (MonitorDescriptor monitor in topology)
        {
            if (monitor.IsPrimary)
                return monitor;
        }
        return topology[0];
    }

    private void OnSelectionChanged(EventArgs e)
    {
        EventHandler handler = SelectionChanged;
        if (handler != null)
            handler(this, e);
    }

    private void OnReminderMonitorChanged(EventArgs e)
    {
        EventHandler handler = ReminderMonitorChanged;
        if (handler != null)
            handler(this, e);
    }
}

internal sealed class MonitorNumberPreviewSession : IDisposable
{
    private readonly List<Form> windows = new List<Form>();
    private readonly Timer timer;
    private bool disposed;

    public MonitorNumberPreviewSession(IEnumerable<MonitorDescriptor> monitors,
        int durationMilliseconds)
    {
        if (monitors == null)
            throw new ArgumentNullException("monitors");
        if (durationMilliseconds < 250 || durationMilliseconds > 10000)
            throw new ArgumentOutOfRangeException("durationMilliseconds");

        foreach (MonitorDescriptor monitor in monitors)
        {
            Form window = CreateWindow(monitor);
            windows.Add(window);
            window.Show();
        }

        timer = new Timer();
        timer.Interval = durationMilliseconds;
        timer.Tick += delegate { Dispose(); };
        timer.Start();
    }

    private static Form CreateWindow(MonitorDescriptor monitor)
    {
        Form form = new Form();
        form.FormBorderStyle = FormBorderStyle.None;
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.Manual;
        form.Bounds = monitor.Bounds;
        form.TopMost = true;
        form.Opacity = 0.62d;
        form.BackColor = Color.FromArgb(18, 29, 41);

        Label number = new Label();
        number.Dock = DockStyle.Fill;
        number.TextAlign = ContentAlignment.MiddleCenter;
        number.ForeColor = Color.White;
        number.BackColor = Color.Transparent;
        number.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 120f,
            FontStyle.Bold, GraphicsUnit.Pixel);
        number.Text = monitor.DisplayNumber.ToString(
            CultureInfo.InvariantCulture);
        form.Controls.Add(number);
        return form;
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        timer.Stop();
        timer.Dispose();
        foreach (Form window in windows)
        {
            if (!window.IsDisposed)
                window.Close();
            window.Dispose();
        }
        windows.Clear();
    }
}

internal sealed class MonitorSelectionDialog : Form
{
    private readonly MonitorLayoutControl layout;
    private readonly CheckedListBox remoteList;
    private readonly ComboBox reminderList;
    private readonly Label warningLabel;
    private MonitorNumberPreviewSession previewSession;
    private bool synchronizing;

    public MonitorSelection Selection { get; private set; }

    public MonitorSelectionDialog(IMonitorTopologySource source,
        MonitorSelection initialSelection)
    {
        if (source == null)
            throw new ArgumentNullException("source");
        IList<MonitorDescriptor> monitors = source.GetMonitors();
        MonitorSelection.ValidateTopology(monitors);

        Text = "Choose displays";
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ClientSize = new Size(720, 570);

        Label instructions = new Label();
        instructions.AutoSize = false;
        instructions.Location = new Point(16, 14);
        instructions.Size = new Size(680, 40);
        instructions.Text = "Select the numbered displays used by the remote " +
            "session. The reminder display is a separate choice. RDP IDs follow " +
            "Windows' native monitor order.";

        layout = new MonitorLayoutControl();
        layout.Location = new Point(16, 58);
        layout.Size = new Size(680, 250);
        layout.Anchor = AnchorStyles.Top | AnchorStyles.Left |
            AnchorStyles.Right;
        layout.SetTopology(monitors);

        remoteList = new CheckedListBox();
        remoteList.CheckOnClick = true;
        remoteList.Location = new Point(16, 332);
        remoteList.Size = new Size(330, 112);
        remoteList.Anchor = AnchorStyles.Top | AnchorStyles.Left |
            AnchorStyles.Right;

        reminderList = new ComboBox();
        reminderList.DropDownStyle = ComboBoxStyle.DropDownList;
        reminderList.Location = new Point(372, 355);
        reminderList.Size = new Size(324, 24);
        reminderList.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        foreach (MonitorDescriptor monitor in monitors)
        {
            remoteList.Items.Add(monitor);
            reminderList.Items.Add(monitor);
        }

        Label remoteLabel = new Label();
        remoteLabel.AutoSize = true;
        remoteLabel.Location = new Point(16, 314);
        remoteLabel.Text = "Remote session monitors";

        Label reminderLabel = new Label();
        reminderLabel.AutoSize = true;
        reminderLabel.Location = new Point(372, 332);
        reminderLabel.Text = "Reminder appears on";

        Button identifyButton = new Button();
        identifyButton.Location = new Point(372, 394);
        identifyButton.Size = new Size(150, 29);
        identifyButton.Text = "Identify displays";
        identifyButton.Click += delegate
        {
            if (previewSession != null)
                previewSession.Dispose();
            previewSession = new MonitorNumberPreviewSession(monitors, 1800);
        };

        Button verifyIdsButton = new Button();
        verifyIdsButton.Location = new Point(530, 394);
        verifyIdsButton.Size = new Size(166, 29);
        verifyIdsButton.Text = "Show official RDP IDs";
        verifyIdsButton.Click += delegate
        {
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = Path.Combine(Environment.SystemDirectory,
                "mstsc.exe");
            start.Arguments = "/l";
            start.UseShellExecute = true;
            Process.Start(start);
        };

        warningLabel = new Label();
        warningLabel.AutoSize = false;
        warningLabel.Location = new Point(16, 452);
        warningLabel.Size = new Size(680, 42);
        warningLabel.ForeColor = Color.FromArgb(155, 80, 0);
        warningLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right |
            AnchorStyles.Bottom;

        Button okButton = new Button();
        okButton.Location = new Point(516, 516);
        okButton.Size = new Size(86, 30);
        okButton.Text = "OK";
        okButton.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        okButton.Click += OnAccept;

        Button cancelButton = new Button();
        cancelButton.Location = new Point(610, 516);
        cancelButton.Size = new Size(86, 30);
        cancelButton.Text = "Cancel";
        cancelButton.DialogResult = DialogResult.Cancel;
        cancelButton.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;

        Controls.Add(instructions);
        Controls.Add(layout);
        Controls.Add(remoteLabel);
        Controls.Add(remoteList);
        Controls.Add(reminderLabel);
        Controls.Add(reminderList);
        Controls.Add(identifyButton);
        Controls.Add(verifyIdsButton);
        Controls.Add(warningLabel);
        Controls.Add(okButton);
        Controls.Add(cancelButton);
        AcceptButton = okButton;
        CancelButton = cancelButton;

        layout.SelectionChanged += delegate { SyncFromLayout(); };
        layout.ReminderMonitorChanged += delegate { SyncFromLayout(); };
        remoteList.ItemCheck += delegate
        {
            if (!synchronizing && IsHandleCreated)
                BeginInvoke((MethodInvoker)delegate { SyncToLayout(); });
        };
        reminderList.SelectedIndexChanged += delegate { SyncToLayout(); };

        MonitorSelection selection = initialSelection;
        if (selection == null)
        {
            List<int> all = new List<int>();
            int reminder = monitors[0].MstscId;
            foreach (MonitorDescriptor monitor in monitors)
            {
                if (monitor.IsPrimary)
                    reminder = monitor.MstscId;
            }
            all.Add(reminder);
            foreach (MonitorDescriptor monitor in monitors)
            {
                if (monitor.MstscId != reminder)
                    all.Add(monitor.MstscId);
            }
            selection = new MonitorSelection(monitors, all, reminder);
        }
        ApplySelection(selection);
        SetupVisualTheme.Apply(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && previewSession != null)
        {
            previewSession.Dispose();
            previewSession = null;
        }
        base.Dispose(disposing);
    }

    private void ApplySelection(MonitorSelection selection)
    {
        synchronizing = true;
        try
        {
            HashSet<int> selected = new HashSet<int>(
                selection.RemoteMonitorIds);
            for (int index = 0; index < remoteList.Items.Count; index++)
            {
                MonitorDescriptor monitor =
                    (MonitorDescriptor)remoteList.Items[index];
                remoteList.SetItemChecked(index,
                    selected.Contains(monitor.MstscId));
                if (monitor.MstscId == selection.ReminderMonitorId)
                    reminderList.SelectedIndex = index;
            }
            layout.SetSelection(selection.RemoteMonitorIds,
                selection.ReminderMonitorId);
            UpdateWarning(selection);
        }
        finally
        {
            synchronizing = false;
        }
    }

    private void SyncFromLayout()
    {
        if (synchronizing)
            return;
        ApplySelection(layout.GetSelection());
    }

    private void SyncToLayout()
    {
        if (synchronizing || reminderList.SelectedItem == null)
            return;
        HashSet<int> checkedIds = new HashSet<int>();
        foreach (object item in remoteList.CheckedItems)
            checkedIds.Add(((MonitorDescriptor)item).MstscId);
        List<int> selected = new List<int>();
        foreach (int existing in layout.RemoteMonitorIds)
        {
            if (checkedIds.Remove(existing))
                selected.Add(existing);
        }
        foreach (MonitorDescriptor monitor in layout.Topology)
        {
            if (checkedIds.Remove(monitor.MstscId))
                selected.Add(monitor.MstscId);
        }
        if (selected.Count == 0)
            return;
        MonitorDescriptor reminder =
            (MonitorDescriptor)reminderList.SelectedItem;
        MonitorSelection selection = new MonitorSelection(layout.Topology,
            selected, reminder.MstscId);
        layout.SetSelection(selected, reminder.MstscId);
        UpdateWarning(selection);
    }

    private void UpdateWarning(MonitorSelection selection)
    {
        string[] warnings = selection.GetCompatibilityWarnings();
        warningLabel.Text = warnings.Length == 0 ? "" : warnings[0];
    }

    private void OnAccept(object sender, EventArgs e)
    {
        if (remoteList.CheckedItems.Count == 0)
        {
            MessageBox.Show(this,
                "Select at least one monitor for the Remote Desktop session.",
                AppPaths.ProductName, MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }
        SyncToLayout();
        Selection = layout.GetSelection();
        string[] warnings = Selection.GetCompatibilityWarnings();
        if (warnings.Length > 0)
        {
            warningLabel.Text = warnings[0];
            MessageBox.Show(this, warnings[0], AppPaths.ProductName,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Selection = null;
            return;
        }
        DialogResult = DialogResult.OK;
        Close();
    }
}

internal sealed class RdpResourceOptions
{
    public bool RedirectClipboard;
    public bool RedirectDrives;
    public bool RedirectPrinters;
    public bool RedirectMicrophone;
    public bool RedirectComPorts;
    public bool RedirectSmartCards;
    public bool RedirectWebAuthn;
    public bool RedirectLocation;

    public static RdpResourceOptions CreateRecommendedDefaults()
    {
        return new RdpResourceOptions
        {
            RedirectClipboard = true,
            RedirectDrives = false,
            RedirectPrinters = false,
            RedirectMicrophone = false,
            RedirectComPorts = false,
            RedirectSmartCards = false,
            RedirectWebAuthn = true,
            RedirectLocation = false
        };
    }

    // These are returned to the profile writer; this helper never edits an
    // imported RDP file itself.
    public string[] GetRdpSettings()
    {
        return new string[]
        {
            "redirectclipboard:i:" + BoolValue(RedirectClipboard),
            "drivestoredirect:s:" + (RedirectDrives ? "*" : ""),
            "redirectprinters:i:" + BoolValue(RedirectPrinters),
            "audiocapturemode:i:" + BoolValue(RedirectMicrophone),
            "redirectcomports:i:" + BoolValue(RedirectComPorts),
            "redirectsmartcards:i:" + BoolValue(RedirectSmartCards),
            "redirectwebauthn:i:" + BoolValue(RedirectWebAuthn),
            "redirectlocation:i:" + BoolValue(RedirectLocation),
            "devicestoredirect:s:",
            "camerastoredirect:s:",
            "usbdevicestoredirect:s:"
        };
    }

    public string[] GetEnabledResourceNames()
    {
        List<string> enabled = new List<string>();
        if (RedirectClipboard) enabled.Add("Clipboard");
        if (RedirectDrives) enabled.Add("Drives");
        if (RedirectPrinters) enabled.Add("Printers");
        if (RedirectMicrophone) enabled.Add("Microphone");
        if (RedirectComPorts) enabled.Add("Ports");
        if (RedirectSmartCards) enabled.Add("Smart cards / Windows Hello for Business");
        if (RedirectWebAuthn) enabled.Add("WebAuthn passkeys and security keys");
        if (RedirectLocation) enabled.Add("Location");
        return enabled.ToArray();
    }

    private static string BoolValue(bool value)
    {
        return value ? "1" : "0";
    }
}

internal sealed class RdpResourcePreview
{
    public bool? RedirectClipboard;
    public bool? RedirectDrives;
    public bool? RedirectPrinters;
    public bool? RedirectMicrophone;
    public bool? RedirectComPorts;
    public bool? RedirectSmartCards;
    public bool? RedirectWebAuthn;
    public bool? RedirectLocation;
    public bool? RedirectMtpPtpDevices;
    public bool? RedirectCameras;
    public bool? RedirectUsbDevices;
}

internal sealed class RdpImportSnapshot : IDisposable
{
    private const long MaximumBytes = 4L * 1024L * 1024L;
    private bool ownsFile = true;

    public string SnapshotPath { get; private set; }
    public string SourceFileName { get; private set; }

    private RdpImportSnapshot(string snapshotPath, string sourceFileName)
    {
        SnapshotPath = snapshotPath;
        SourceFileName = sourceFileName;
    }

    public static RdpImportSnapshot Create(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Choose an RDP file.", "sourcePath");
        string fullPath = Path.GetFullPath(sourcePath);
        if (!string.Equals(Path.GetExtension(fullPath), ".rdp",
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Choose a file ending in .rdp.");

        string snapshotPath = Path.Combine(Path.GetTempPath(),
            "RdpSessionReminder-import-" + Guid.NewGuid().ToString("N") +
            ".rdp");
        try
        {
            using (FileStream source = new FileStream(fullPath, FileMode.Open,
                FileAccess.Read, FileShare.Read, 81920,
                FileOptions.SequentialScan))
            {
                if (source.Length > MaximumBytes)
                    throw new InvalidDataException(
                        "The RDP file is larger than the 4 MB safety limit.");
                using (FileStream destination = new FileStream(snapshotPath,
                    FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    81920, FileOptions.SequentialScan))
                {
                    byte[] buffer = new byte[81920];
                    long total = 0;
                    int count;
                    while ((count = source.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        total += count;
                        if (total > MaximumBytes)
                            throw new InvalidDataException(
                                "The RDP file changed or exceeded the 4 MB safety limit.");
                        destination.Write(buffer, 0, count);
                    }
                    destination.Flush(true);
                }
            }
            return new RdpImportSnapshot(snapshotPath,
                Path.GetFileName(fullPath));
        }
        catch
        {
            try
            {
                if (File.Exists(snapshotPath))
                    File.Delete(snapshotPath);
            }
            catch
            {
            }
            throw;
        }
    }

    public string Detach()
    {
        ownsFile = false;
        return SnapshotPath;
    }

    public void Dispose()
    {
        if (!ownsFile || string.IsNullOrEmpty(SnapshotPath))
            return;
        ownsFile = false;
        try
        {
            if (File.Exists(SnapshotPath))
                File.Delete(SnapshotPath);
        }
        catch
        {
        }
    }
}

internal sealed class RdpImportPreview
{
    public string SourceFileName { get; private set; }
    public bool EndpointsRevealed { get; private set; }
    public bool FullAddressPresent { get; private set; }
    public string FullAddressDisplay { get; private set; }
    public bool AlternateAddressPresent { get; private set; }
    public string AlternateAddressDisplay { get; private set; }
    public bool UsernamePresent { get; private set; }
    public bool Password51Present { get; private set; }
    public bool GatewayPresent { get; private set; }
    public string GatewayDisplay { get; private set; }
    public RdpResourcePreview Resources { get; private set; }
    public int? ScreenModeId { get; private set; }
    public int? DesktopWidth { get; private set; }
    public int? DesktopHeight { get; private set; }
    public int? SessionBitsPerPixel { get; private set; }
    public bool? UseMultimon { get; private set; }
    public string SelectedMonitors { get; private set; }
    public bool? DisplayConnectionBar { get; private set; }
    public int? AuthenticationLevel { get; private set; }
    public string ServerAuthenticationSummary { get; private set; }
    public string[] AuthenticationWarnings { get; private set; }

    private RdpImportPreview()
    {
    }

    public static RdpImportPreview Read(string path)
    {
        return Read(path, false);
    }

    public static RdpImportPreview Read(string path, bool revealEndpoints)
    {
        return Read(path, Path.GetFileName(path), revealEndpoints);
    }

    internal static RdpImportPreview Read(string path, string displayFileName,
        bool revealEndpoints)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Choose an RDP file to preview.", "path");

        string fullPath = Path.GetFullPath(path);
        Dictionary<string, RdpSettingRecord> settings =
            new Dictionary<string, RdpSettingRecord>(
                StringComparer.OrdinalIgnoreCase);

        // Open first, deny writers/deletion, and then validate the exact handle
        // being parsed. StreamReader detects UTF-8/UTF-16 BOMs used by RDP files.
        using (FileStream stream = new FileStream(fullPath, FileMode.Open,
            FileAccess.Read, FileShare.Read))
        using (StreamReader reader = new StreamReader(stream, Encoding.Default,
            true))
        {
            if (stream.Length > 4 * 1024 * 1024)
                throw new InvalidDataException(
                    "The RDP file is too large to preview safely.");
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                RdpSettingRecord record;
                if (TryParseSetting(line, out record))
                {
                    // We only need to know that this field exists. Drop its
                    // data immediately so the preview model never retains a
                    // saved-credential blob.
                    if (record.Name.Equals("password 51",
                        StringComparison.OrdinalIgnoreCase))
                        record.Value = "";
                    settings[record.Name] = record;
                }
            }
        }

        RdpImportPreview preview = new RdpImportPreview();
        preview.SourceFileName = string.IsNullOrWhiteSpace(displayFileName)
            ? Path.GetFileName(fullPath)
            : Path.GetFileName(displayFileName);
        preview.EndpointsRevealed = revealEndpoints;

        string fullAddress = GetString(settings, "full address");
        string alternateAddress = GetString(settings, "alternate full address");
        string gateway = GetString(settings, "gatewayhostname");
        preview.FullAddressPresent = !string.IsNullOrWhiteSpace(fullAddress);
        preview.AlternateAddressPresent =
            !string.IsNullOrWhiteSpace(alternateAddress);
        preview.GatewayPresent = !string.IsNullOrWhiteSpace(gateway);
        preview.FullAddressDisplay = DisplayEndpoint(fullAddress,
            revealEndpoints);
        preview.AlternateAddressDisplay = DisplayEndpoint(alternateAddress,
            revealEndpoints);
        preview.GatewayDisplay = DisplayEndpoint(gateway, revealEndpoints);
        preview.UsernamePresent = HasNonEmptyValue(settings, "username");
        preview.Password51Present = settings.ContainsKey("password 51");

        RdpResourcePreview resources = new RdpResourcePreview();
        resources.RedirectClipboard = GetBoolean(settings, "redirectclipboard");
        resources.RedirectDrives = GetStringEnabled(settings,
            "drivestoredirect");
        if (!resources.RedirectDrives.HasValue)
            resources.RedirectDrives = GetBoolean(settings, "redirectdrives");
        resources.RedirectPrinters = GetBoolean(settings, "redirectprinters");
        resources.RedirectMicrophone = GetBoolean(settings, "audiocapturemode");
        resources.RedirectComPorts = GetBoolean(settings, "redirectcomports");
        resources.RedirectSmartCards = GetBoolean(settings,
            "redirectsmartcards");
        resources.RedirectWebAuthn = GetBoolean(settings, "redirectwebauthn");
        resources.RedirectLocation = GetBoolean(settings, "redirectlocation");
        resources.RedirectMtpPtpDevices = GetStringEnabled(settings,
            "devicestoredirect");
        resources.RedirectCameras = GetStringEnabled(settings,
            "camerastoredirect");
        resources.RedirectUsbDevices = GetStringEnabled(settings,
            "usbdevicestoredirect");
        preview.Resources = resources;

        preview.ScreenModeId = GetInteger(settings, "screen mode id");
        preview.DesktopWidth = GetInteger(settings, "desktopwidth");
        preview.DesktopHeight = GetInteger(settings, "desktopheight");
        preview.SessionBitsPerPixel = GetInteger(settings, "session bpp");
        preview.UseMultimon = GetBoolean(settings, "use multimon");
        preview.SelectedMonitors = GetSafeSelectedMonitors(
            GetString(settings, "selectedmonitors"));
        preview.DisplayConnectionBar = GetBoolean(settings,
            "displayconnectionbar");
        preview.AuthenticationLevel = GetInteger(settings,
            "authentication level");

        List<string> warnings = new List<string>();
        if (preview.Password51Present)
        {
            warnings.Add("The file contains a saved-credential field. Its value " +
                "is never displayed or retained by this preview.");
        }

        AddDefaultEnabledResourceWarning(warnings,
            resources.RedirectComPorts, "COM ports");
        AddDefaultEnabledResourceWarning(warnings,
            resources.RedirectSmartCards, "Smart cards");
        AddDefaultEnabledResourceWarning(warnings,
            resources.RedirectWebAuthn, "WebAuthn");
        AddDefaultEnabledResourceWarning(warnings,
            resources.RedirectMtpPtpDevices, "MTP/PTP devices");

        if (!preview.AuthenticationLevel.HasValue)
        {
            preview.ServerAuthenticationSummary =
                "The file does not state what to do when server authentication fails.";
            warnings.Add(preview.ServerAuthenticationSummary);
        }
        else if (preview.AuthenticationLevel.Value == 0)
        {
            preview.ServerAuthenticationSummary =
                "Connect even if server authentication fails.";
            warnings.Add("Server authentication failures will not stop or warn " +
                "before this connection is opened.");
        }
        else if (preview.AuthenticationLevel.Value == 1)
        {
            preview.ServerAuthenticationSummary =
                "Do not connect if server authentication fails.";
        }
        else if (preview.AuthenticationLevel.Value == 2)
        {
            preview.ServerAuthenticationSummary =
                "Warn if server authentication fails and let the user decide.";
        }
        else
        {
            preview.ServerAuthenticationSummary =
                "The server authentication setting is not recognized.";
            warnings.Add(preview.ServerAuthenticationSummary);
        }

        bool? credSsp = GetBoolean(settings, "enablecredsspsupport");
        if (credSsp.HasValue && !credSsp.Value)
        {
            warnings.Add("Network Level Authentication support is disabled in " +
                "this file.");
        }
        preview.AuthenticationWarnings = warnings.ToArray();
        return preview;
    }

    public string[] GetDisplayLines()
    {
        List<string> lines = new List<string>();
        lines.Add("File: " + SourceFileName);
        lines.Add("Computer: " + (FullAddressPresent
            ? FullAddressDisplay : "Not specified"));
        if (AlternateAddressPresent)
            lines.Add("Alternate computer: " + AlternateAddressDisplay);
        lines.Add("User name saved in file: " + YesNo(UsernamePresent));
        lines.Add("Saved-credential field present: " + YesNo(Password51Present));
        lines.Add("Gateway: " + (GatewayPresent
            ? GatewayDisplay : "Not configured"));
        lines.Add("");
        lines.Add("Display");
        lines.Add("  Mode: " + GetScreenModeText());
        lines.Add("  Resolution: " + GetResolutionText());
        lines.Add("  Color depth: " + (SessionBitsPerPixel.HasValue
            ? SessionBitsPerPixel.Value.ToString(CultureInfo.InvariantCulture) +
                " bpp" : "Not specified"));
        lines.Add("  Multiple monitors: " + NullableYesNo(UseMultimon));
        if (!string.IsNullOrEmpty(SelectedMonitors))
            lines.Add("  Selected monitor IDs: " + SelectedMonitors);
        lines.Add("  Full-screen connection bar: " +
            NullableYesNo(DisplayConnectionBar));
        lines.Add("");
        lines.Add("Requested local resources");
        AddResourceLine(lines, "Clipboard", Resources.RedirectClipboard, false);
        AddResourceLine(lines, "Drives", Resources.RedirectDrives, false);
        AddResourceLine(lines, "Printers", Resources.RedirectPrinters, false);
        AddResourceLine(lines, "Microphone", Resources.RedirectMicrophone, false);
        AddResourceLine(lines, "Ports", Resources.RedirectComPorts, true);
        AddResourceLine(lines, "Smart cards / Windows Hello for Business",
            Resources.RedirectSmartCards, true);
        AddResourceLine(lines, "WebAuthn passkeys and security keys",
            Resources.RedirectWebAuthn, true);
        AddResourceLine(lines, "Location", Resources.RedirectLocation, false);
        AddResourceLine(lines, "MTP/PTP devices",
            Resources.RedirectMtpPtpDevices, true);
        AddResourceLine(lines, "Cameras", Resources.RedirectCameras, false);
        AddResourceLine(lines, "USB devices", Resources.RedirectUsbDevices,
            false);
        lines.Add("");
        lines.Add("Server authentication: " + ServerAuthenticationSummary);
        foreach (string warning in AuthenticationWarnings)
            lines.Add("Warning: " + warning);
        return lines.ToArray();
    }

    private static bool TryParseSetting(string line,
        out RdpSettingRecord record)
    {
        record = null;
        if (string.IsNullOrEmpty(line) || line.Length > 1024 * 1024)
            return false;
        int first = line.IndexOf(':');
        if (first < 1)
            return false;
        int second = line.IndexOf(':', first + 1);
        if (second < 0)
            return false;
        string name = line.Substring(0, first).Trim();
        string type = line.Substring(first + 1, second - first - 1).Trim();
        if (name.Length == 0 || type.Length != 1)
            return false;
        record = new RdpSettingRecord(name, type[0],
            line.Substring(second + 1));
        return true;
    }

    private static string DisplayEndpoint(string value, bool reveal)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Not configured";
        return reveal ? value.Trim() : "Configured (hidden)";
    }

    private static string GetString(
        IDictionary<string, RdpSettingRecord> settings, string name)
    {
        RdpSettingRecord record;
        return settings.TryGetValue(name, out record) ? record.Value : "";
    }

    private static bool HasNonEmptyValue(
        IDictionary<string, RdpSettingRecord> settings, string name)
    {
        return !string.IsNullOrWhiteSpace(GetString(settings, name));
    }

    private static bool GetEnabled(
        IDictionary<string, RdpSettingRecord> settings, string name)
    {
        bool? value = GetBoolean(settings, name);
        return value.HasValue && value.Value;
    }

    private static bool? GetStringEnabled(
        IDictionary<string, RdpSettingRecord> settings, string name)
    {
        RdpSettingRecord record;
        if (!settings.TryGetValue(name, out record))
            return null;
        return !string.IsNullOrWhiteSpace(record.Value);
    }

    private static bool? GetBoolean(
        IDictionary<string, RdpSettingRecord> settings, string name)
    {
        int? value = GetInteger(settings, name);
        if (!value.HasValue)
            return null;
        if (value.Value == 0)
            return false;
        if (value.Value == 1)
            return true;
        return null;
    }

    private static int? GetInteger(
        IDictionary<string, RdpSettingRecord> settings, string name)
    {
        RdpSettingRecord record;
        int value;
        if (!settings.TryGetValue(name, out record) ||
            !int.TryParse(record.Value.Trim(), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out value))
            return null;
        return value;
    }

    private static string GetSafeSelectedMonitors(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        string[] pieces = value.Split(',');
        List<string> ids = new List<string>();
        foreach (string piece in pieces)
        {
            int id;
            if (!int.TryParse(piece.Trim(), NumberStyles.None,
                CultureInfo.InvariantCulture, out id) || id < 0 || id > 255)
                return "Unrecognized";
            ids.Add(id.ToString(CultureInfo.InvariantCulture));
        }
        return string.Join(",", ids.ToArray());
    }

    private string GetScreenModeText()
    {
        if (!ScreenModeId.HasValue)
            return "Not specified";
        return ScreenModeId.Value == 2 ? "Full screen" :
            ScreenModeId.Value == 1 ? "Window" : "Custom / unrecognized";
    }

    private string GetResolutionText()
    {
        if (!DesktopWidth.HasValue || !DesktopHeight.HasValue)
            return "Not specified";
        return DesktopWidth.Value.ToString(CultureInfo.InvariantCulture) +
            " x " + DesktopHeight.Value.ToString(CultureInfo.InvariantCulture);
    }

    private static void AddResourceLine(ICollection<string> lines,
        string name, bool? enabled, bool enabledByDefault)
    {
        string value = enabled.HasValue
            ? YesNo(enabled.Value)
            : "Not specified (Windows default is " +
                (enabledByDefault ? "enabled" : "disabled") + ")";
        lines.Add("  " + name + ": " + value);
    }

    private static void AddDefaultEnabledResourceWarning(
        ICollection<string> warnings, bool? value, string resourceName)
    {
        if (!value.HasValue)
            warnings.Add(resourceName + " are not specified; Windows' default " +
                "can enable this redirection.");
    }

    private static string YesNo(bool value)
    {
        return value ? "Yes" : "No";
    }

    private static string NullableYesNo(bool? value)
    {
        return value.HasValue ? YesNo(value.Value) : "Not specified";
    }

    private sealed class RdpSettingRecord
    {
        public string Name;
        public char Type;
        public string Value;

        public RdpSettingRecord(string name, char type, string value)
        {
            Name = name;
            Type = type;
            Value = value;
        }
    }
}

internal sealed class RdpImportPreviewDialog : Form
{
    private readonly string sourcePath;
    private readonly string sourceFileName;
    private readonly RichTextBox previewText;
    private readonly CheckBox revealEndpoints;

    public RdpImportPreviewDialog(string path)
        : this(path, Path.GetFileName(path), false)
    {
    }

    public RdpImportPreviewDialog(string path, string displayFileName,
        bool confirmUse)
    {
        sourcePath = Path.GetFullPath(path);
        sourceFileName = Path.GetFileName(displayFileName);
        Text = "RDP import preview";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(650, 610);
        MinimumSize = new Size(520, 450);
        MinimizeBox = false;
        MaximizeBox = true;

        Label explanation = new Label();
        explanation.AutoSize = false;
        explanation.Location = new Point(14, 12);
        explanation.Size = new Size(620, 44);
        explanation.Anchor = AnchorStyles.Top | AnchorStyles.Left |
            AnchorStyles.Right;
        explanation.Text = confirmUse
            ? "Review this locked snapshot before importing it. Endpoints stay " +
                "hidden unless you reveal them; saved password values are never shown."
            : "This read-only preview shows what the RDP file requests. It does " +
                "not change the file, and it never displays a saved password value.";

        revealEndpoints = new CheckBox();
        revealEndpoints.AutoSize = true;
        revealEndpoints.Location = new Point(14, 62);
        revealEndpoints.Text = "Show computer and gateway names";
        revealEndpoints.CheckedChanged += delegate { RefreshPreview(); };

        previewText = new RichTextBox();
        previewText.Location = new Point(14, 90);
        previewText.Size = new Size(622, 462);
        previewText.Anchor = AnchorStyles.Top | AnchorStyles.Bottom |
            AnchorStyles.Left | AnchorStyles.Right;
        previewText.ReadOnly = true;
        previewText.BackColor = SystemColors.Window;
        previewText.DetectUrls = false;
        previewText.Font = new Font(FontFamily.GenericMonospace, 9.5f);

        Button closeButton = new Button();
        closeButton.Location = new Point(546, 566);
        closeButton.Size = new Size(90, 30);
        closeButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        closeButton.Text = confirmUse ? "Use this file" : "Close";
        closeButton.DialogResult = DialogResult.OK;

        Controls.Add(explanation);
        Controls.Add(revealEndpoints);
        Controls.Add(previewText);
        Controls.Add(closeButton);
        if (confirmUse)
        {
            closeButton.Location = new Point(432, 566);
            closeButton.Size = new Size(110, 30);
            Button cancelButton = new Button();
            cancelButton.Location = new Point(546, 566);
            cancelButton.Size = new Size(90, 30);
            cancelButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            cancelButton.Text = "Cancel";
            cancelButton.DialogResult = DialogResult.Cancel;
            Controls.Add(cancelButton);
            CancelButton = cancelButton;
        }
        else
        {
            CancelButton = closeButton;
        }
        AcceptButton = closeButton;
        RefreshPreview();
        SetupVisualTheme.Apply(this);
    }

    private void RefreshPreview()
    {
        RdpImportPreview preview = RdpImportPreview.Read(sourcePath,
            sourceFileName, revealEndpoints.Checked);
        previewText.Lines = preview.GetDisplayLines();
        previewText.SelectionStart = 0;
        previewText.SelectionLength = 0;
    }
}

internal enum ShortcutIconKind
{
    WindowsRemoteDesktop,
    Reminder,
    Work,
    Production,
    Test,
    Personal
}

internal sealed class ShortcutIconChoice
{
    public ShortcutIconKind Kind { get; private set; }
    public string DisplayName { get; private set; }
    public string Description { get; private set; }
    public string IconPath { get; private set; }
    public int IconIndex { get; private set; }

    public string IconLocation
    {
        get
        {
            return IconPath + "," + IconIndex.ToString(
                CultureInfo.InvariantCulture);
        }
    }

    internal ShortcutIconChoice(ShortcutIconKind kind, string displayName,
        string description, string iconPath, int iconIndex)
    {
        Kind = kind;
        DisplayName = displayName;
        Description = description;
        IconPath = iconPath;
        IconIndex = iconIndex;
    }

    public override string ToString()
    {
        return DisplayName;
    }
}

internal static class ShortcutIconCatalog
{
    private const string IconDirectoryName = "icons";
    // The filename is part of Explorer's icon-cache key. Increment this when
    // the generated image or ICO encoding changes so repaired shortcuts do not
    // keep rendering a cached image from an older release.
    private const string CacheVersion = "v2";
    private static readonly int[] GeneratedIconSizes = new int[]
    {
        16, 20, 24, 32, 40, 48, 64, 128, 256
    };

    public static ShortcutIconChoice[] GetChoices()
    {
        string defaultPath = Path.Combine(Environment.SystemDirectory,
            "mstsc.exe");
        return new ShortcutIconChoice[]
        {
            new ShortcutIconChoice(ShortcutIconKind.WindowsRemoteDesktop,
                "Windows Remote Desktop (recommended)",
                "The familiar Windows Remote Desktop icon used by default.",
                defaultPath, 0),
            CreateGeneratedChoice(ShortcutIconKind.Reminder,
                "Remote session reminder", "Neutral reminder monitor icon."),
            CreateGeneratedChoice(ShortcutIconKind.Work,
                "Work - blue", "Blue monitor icon for everyday work systems."),
            CreateGeneratedChoice(ShortcutIconKind.Production,
                "Production - red", "Red monitor icon for production systems."),
            CreateGeneratedChoice(ShortcutIconKind.Test,
                "Test - amber", "Amber monitor icon for test systems."),
            CreateGeneratedChoice(ShortcutIconKind.Personal,
                "Personal - green", "Green monitor icon for personal systems.")
        };
    }

    public static ShortcutIconChoice Resolve(ShortcutIconKind kind)
    {
        ShortcutIconChoice match = null;
        foreach (ShortcutIconChoice choice in GetChoices())
        {
            if (choice.Kind == kind)
            {
                match = choice;
                break;
            }
        }
        if (match == null)
            throw new ArgumentOutOfRangeException("kind");

        if (kind != ShortcutIconKind.WindowsRemoteDesktop)
            EnsureGeneratedIcon(match);
        return match;
    }

    public static bool IsAppOwnedIconPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
        try
        {
            string root = Path.GetFullPath(Path.Combine(
                AppPaths.InstallDirectory, IconDirectoryName));
            string fullPath = Path.GetFullPath(path);
            return string.Equals(Path.GetDirectoryName(fullPath), root,
                StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Path.GetExtension(fullPath), ".ico",
                    StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static ShortcutIconChoice CreateGeneratedChoice(
        ShortcutIconKind kind, string name, string description)
    {
        string fileName = kind.ToString().ToLowerInvariant() + "-" +
            CacheVersion + ".ico";
        string path = Path.Combine(AppPaths.InstallDirectory,
            IconDirectoryName, fileName);
        if (!IsAppOwnedIconPath(path))
            throw new InvalidOperationException(
                "The generated icon path is outside the application folder.");
        return new ShortcutIconChoice(kind, name, description, path, 0);
    }

    private static void EnsureGeneratedIcon(ShortcutIconChoice choice)
    {
        if (!IsAppOwnedIconPath(choice.IconPath))
            throw new InvalidOperationException(
                "Refusing to write an icon outside the application folder.");
        string installDirectory = Path.GetFullPath(AppPaths.InstallDirectory);
        string directory = Path.GetDirectoryName(choice.IconPath);
        if (Directory.Exists(installDirectory) &&
            IsReparsePoint(installDirectory))
            throw new IOException(
                "The application folder is a reparse point; no icon was written.");
        Directory.CreateDirectory(directory);
        if (IsReparsePoint(directory))
            throw new IOException(
                "The application icon folder is a reparse point; no icon was written.");
        if (Directory.Exists(choice.IconPath) ||
            (File.Exists(choice.IconPath) && IsReparsePoint(choice.IconPath)))
            throw new IOException(
                "The generated icon destination is not a regular file.");
        if (File.Exists(choice.IconPath) &&
            HasExpectedIconFrames(choice.IconPath))
            return;

        string temporaryPath = Path.Combine(directory,
            ".icon-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            WriteIcon(temporaryPath, choice.Kind);
            if (File.Exists(choice.IconPath))
                File.Delete(choice.IconPath);
            File.Move(temporaryPath, choice.IconPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static bool IsReparsePoint(string path)
    {
        return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    }

    private static bool HasExpectedIconFrames(string path)
    {
        try
        {
            using (FileStream input = new FileStream(path, FileMode.Open,
                FileAccess.Read, FileShare.Read))
            using (BinaryReader reader = new BinaryReader(input))
            {
                if (input.Length < 6 || reader.ReadUInt16() != 0 ||
                    reader.ReadUInt16() != 1)
                    return false;

                int count = reader.ReadUInt16();
                if (count <= 0 || count > 64 ||
                    input.Length < 6L + (16L * count))
                    return false;

                HashSet<int> sizes = new HashSet<int>();
                long minimumOffset = 6L + (16L * count);
                for (int index = 0; index < count; index++)
                {
                    int width = reader.ReadByte();
                    int height = reader.ReadByte();
                    reader.ReadByte();
                    reader.ReadByte();
                    ushort planes = reader.ReadUInt16();
                    ushort bitCount = reader.ReadUInt16();
                    uint byteCount = reader.ReadUInt32();
                    uint imageOffset = reader.ReadUInt32();
                    width = width == 0 ? 256 : width;
                    height = height == 0 ? 256 : height;

                    ulong end = (ulong)imageOffset + (ulong)byteCount;
                    if (width != height || planes != 1 || bitCount != 32 ||
                        byteCount < 8 || imageOffset < minimumOffset ||
                        end > (ulong)input.Length)
                        return false;
                    sizes.Add(width);
                }

                foreach (int requiredSize in GeneratedIconSizes)
                {
                    if (!sizes.Contains(requiredSize))
                        return false;
                }
                return true;
            }
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void WriteIcon(string path, ShortcutIconKind kind)
    {
        Color accent;
        string label;
        switch (kind)
        {
            case ShortcutIconKind.Production:
                accent = Color.FromArgb(196, 43, 55);
                label = "P";
                break;
            case ShortcutIconKind.Test:
                accent = Color.FromArgb(224, 145, 0);
                label = "T";
                break;
            case ShortcutIconKind.Personal:
                accent = Color.FromArgb(35, 143, 76);
                label = "H";
                break;
            case ShortcutIconKind.Work:
                accent = Color.FromArgb(0, 103, 192);
                label = "W";
                break;
            default:
                accent = Color.FromArgb(0, 103, 192);
                label = "R";
                break;
        }

        List<byte[]> frames = new List<byte[]>();
        foreach (int size in GeneratedIconSizes)
        {
            using (Bitmap bitmap = DrawIconFrame(size, accent, label))
            using (MemoryStream encoded = new MemoryStream())
            {
                bitmap.Save(encoded, System.Drawing.Imaging.ImageFormat.Png);
                frames.Add(encoded.ToArray());
            }
        }

        using (FileStream output = new FileStream(path, FileMode.CreateNew,
            FileAccess.Write, FileShare.None))
        using (BinaryWriter writer = new BinaryWriter(output))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)frames.Count);
            int offset = 6 + (16 * frames.Count);
            for (int index = 0; index < frames.Count; index++)
            {
                int size = GeneratedIconSizes[index];
                writer.Write((byte)(size == 256 ? 0 : size));
                writer.Write((byte)(size == 256 ? 0 : size));
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write((uint)frames[index].Length);
                writer.Write((uint)offset);
                offset += frames[index].Length;
            }
            foreach (byte[] frame in frames)
                writer.Write(frame);
            output.Flush(true);
        }
    }

    private static Bitmap DrawIconFrame(int size, Color accent, string label)
    {
        Bitmap bitmap = new Bitmap(size, size,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                float scale = size / 64f;
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode =
                    System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.CompositingQuality =
                    System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.PixelOffsetMode =
                    System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                Rectangle monitor = ScaleRectangle(5, 7, 54, 40, scale);
                using (Brush frame = new SolidBrush(Color.FromArgb(47, 54, 61)))
                using (Brush screen = new SolidBrush(Color.FromArgb(240, 246, 252)))
                using (Brush accentBrush = new SolidBrush(accent))
                using (Pen stand = new Pen(Color.FromArgb(47, 54, 61),
                    Math.Max(1f, 5f * scale)))
                {
                    graphics.FillRoundedRectangle(frame, monitor,
                        Math.Max(1, (int)Math.Round(7 * scale)));
                    graphics.FillRectangle(screen,
                        ScaleRectangle(10, 12, 44, 26, scale));
                    graphics.FillRectangle(accentBrush,
                        ScaleRectangle(10, 31, 44, 7, scale));
                    graphics.DrawLine(stand, 32 * scale, 47 * scale,
                        32 * scale, 54 * scale);
                    graphics.DrawLine(stand, 22 * scale, 56 * scale,
                        42 * scale, 56 * scale);
                }
                using (Font labelFont = new Font(FontFamily.GenericSansSerif,
                    Math.Max(4f, 14f * scale), FontStyle.Bold,
                    GraphicsUnit.Pixel))
                {
                    TextRenderer.DrawText(graphics, label, labelFont,
                        ScaleRectangle(10, 12, 44, 20, scale), accent,
                        TextFormatFlags.HorizontalCenter |
                        TextFormatFlags.VerticalCenter |
                        TextFormatFlags.NoPadding);
                }
            }
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    private static Rectangle ScaleRectangle(int x, int y, int width,
        int height, float scale)
    {
        return new Rectangle(
            (int)Math.Round(x * scale),
            (int)Math.Round(y * scale),
            Math.Max(1, (int)Math.Round(width * scale)),
            Math.Max(1, (int)Math.Round(height * scale)));
    }

    private static void FillRoundedRectangle(this Graphics graphics, Brush brush,
        Rectangle bounds, int radius)
    {
        using (System.Drawing.Drawing2D.GraphicsPath path =
            new System.Drawing.Drawing2D.GraphicsPath())
        {
            int diameter = radius * 2;
            Rectangle arc = new Rectangle(bounds.Location,
                new Size(diameter, diameter));
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            graphics.FillPath(brush, path);
        }
    }
}
