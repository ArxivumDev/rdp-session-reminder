using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

[assembly: AssemblyTitle("RDP Session Reminder")]
[assembly: AssemblyDescription("A lightweight local reminder for Microsoft Remote Desktop sessions.")]
[assembly: AssemblyCompany("RDP Session Reminder contributors")]
[assembly: AssemblyProduct("RDP Session Reminder")]
[assembly: AssemblyCopyright("Copyright (c) 2026")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

internal static class RdpSessionReminder
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr parameter);
    private delegate bool MonitorEnumProc(
        IntPtr monitor, IntPtr deviceContext, IntPtr rectangle, IntPtr parameter);
    private delegate IntPtr WindowProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Size;
        public uint Style;
        public IntPtr WindowProcedure;
        public int ClassExtra;
        public int WindowExtra;
        public IntPtr Instance;
        public IntPtr Icon;
        public IntPtr Cursor;
        public IntPtr BackgroundBrush;
        [MarshalAs(UnmanagedType.LPWStr)] public string MenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string ClassName;
        public IntPtr SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr Window;
        public uint Id;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public NativePoint Position;
        public uint Private;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PaintStruct
    {
        public IntPtr DeviceContext;
        public bool Erase;
        public NativeRect PaintRectangle;
        public bool Restore;
        public bool IncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] Reserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public uint Size;
        public NativeRect MonitorArea;
        public NativeRect WorkArea;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public IntPtr DefaultHeapId;
        public uint ModuleId;
        public uint Threads;
        public uint ParentProcessId;
        public int PriorityBase;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string ExecutableFile;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder className, int count);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WindowClass windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(uint extendedStyle, string className,
        string windowName, uint style, int x, int y, int width, int height,
        IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr DefWindowProc(
        IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetMessage(out NativeMessage message,
        IntPtr hWnd, uint minimum, uint maximum);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref NativeMessage message);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr DispatchMessage(ref NativeMessage message);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int exitCode);

    [DllImport("user32.dll")]
    private static extern UIntPtr SetTimer(
        IntPtr hWnd, UIntPtr timerId, uint interval, IntPtr callback);

    [DllImport("user32.dll")]
    private static extern bool KillTimer(IntPtr hWnd, UIntPtr timerId);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter,
        int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int command);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr deviceContext,
        IntPtr clipRectangle, MonitorEnumProc callback, IntPtr parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo information);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("user32.dll")]
    private static extern bool SystemParametersInfo(
        uint action, uint parameter, out NativeRect value, uint update);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(
        IntPtr hWnd, uint colorKey, byte alpha, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr BeginPaint(IntPtr hWnd, ref PaintStruct paint);

    [DllImport("user32.dll")]
    private static extern bool EndPaint(IntPtr hWnd, ref PaintStruct paint);

    [DllImport("user32.dll")]
    private static extern int FillRect(
        IntPtr deviceContext, ref NativeRect rectangle, IntPtr brush);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int DrawText(IntPtr deviceContext, string text,
        int count, ref NativeRect rectangle, uint format);

    [DllImport("gdi32.dll")]
    private static extern uint SetTextColor(IntPtr deviceContext, uint color);

    [DllImport("gdi32.dll")]
    private static extern int SetBkMode(IntPtr deviceContext, int mode);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateSolidBrush(uint color);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFont(int height, int width, int escapement,
        int orientation, int weight, uint italic, uint underline, uint strikeOut,
        uint characterSet, uint outputPrecision, uint clipPrecision,
        uint quality, uint pitchAndFamily, string faceName);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr deviceContext, IntPtr value);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr value);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string moduleName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry entry);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadCursor(IntPtr instance, IntPtr cursorName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(
        IntPtr hWnd, string text, string caption, uint type);

    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    private const uint WindowStylePopup = 0x80000000;
    private const uint ExStyleTopmost = 0x00000008;
    private const uint ExStyleTransparent = 0x00000020;
    private const uint ExStyleToolWindow = 0x00000080;
    private const uint ExStyleLayered = 0x00080000;
    private const uint ExStyleNoActivate = 0x08000000;
    private const uint MessageDestroy = 0x0002;
    private const uint MessageSettingChange = 0x001A;
    private const uint MessagePaint = 0x000F;
    private const uint MessageEraseBackground = 0x0014;
    private const uint MessageNcHitTest = 0x0084;
    private const uint MessageDisplayChange = 0x007E;
    private const uint MessageTimer = 0x0113;
    private const uint LayeredAlpha = 0x00000002;
    private const uint SetPositionNoActivate = 0x0010;
    private const uint SetPositionShowWindow = 0x0040;
    private const uint GetWorkArea = 0x0030;
    private const uint DrawCenter = 0x00000001;
    private const uint DrawVerticalCenter = 0x00000004;
    private const uint DrawSingleLine = 0x00000020;
    private const uint DrawNoPrefix = 0x00000800;
    private const uint DrawEndEllipsis = 0x00008000;
    private const uint SnapshotProcesses = 0x00000002;
    private const int ShowWindowHide = 0;
    private const int TransparentBackground = 1;
    private const int FontWeightSemibold = 600;
    private static readonly IntPtr Topmost = new IntPtr(-1);
    private static readonly UIntPtr MonitorTimer = new UIntPtr(1);
    private static readonly WindowProc WindowProcedure = HandleWindowMessage;

    private static int preferredProcessId;
    private static long preferredProcessStartTicks;
    private static int establishedProcessId;
    private static long establishedProcessStartTicks;
    private static long minimumNewProcessStartTicks;
    private static DateTime startupDeadlineUtc;
    private static IntPtr targetWindow;
    private static IntPtr bannerWindow;
    private static IntPtr bannerFont;
    private static string bannerText;
    private static string displayDevice;
    private static bool targetSeen;
    private static int missingTicks;
    private static int bannerWidth;
    private static int bannerHeight;
    private static int rightMargin;
    private static int bottomLift;
    private static Mutex instanceMutex;

    [STAThread]
    private static int Main(string[] args)
    {
        if (HasArgument(args, "--self-test"))
            return RunSelfTest();

        if (HasArgument(args, "--configure"))
        {
            StartSetup();
            return 0;
        }

        int profileIndex = FindArgument(args, "--profile");
        if (profileIndex < 0)
        {
            StartSetup();
            return 0;
        }

        if (profileIndex + 1 >= args.Length ||
            !SettingsStore.IsValidProfileId(args[profileIndex + 1]))
        {
            ShowError("This shortcut does not contain a valid reminder profile. " +
                "Run setup to create a new shortcut.");
            return 1;
        }

        string profileId = args[profileIndex + 1];
        ReminderSettings settings;
        if (!SettingsStore.TryLoadProfile(profileId, out settings))
        {
            ShowError("This shortcut's reminder profile is missing or invalid. " +
                "Run setup to create a new shortcut.");
            return 1;
        }

        bool ownsMutex;
        string mutexName = "Local\\RdpSessionReminder-" + profileId.ToUpperInvariant();
        instanceMutex = new Mutex(true, mutexName, out ownsMutex);
        if (!ownsMutex)
        {
            MessageBox(IntPtr.Zero,
                "This Remote Desktop shortcut is already running.",
                AppPaths.ProductName, 0x00000040);
            return 0;
        }

        try
        {
            return RunReminder(settings);
        }
        finally
        {
            instanceMutex.ReleaseMutex();
            instanceMutex.Dispose();
        }
    }

    private static int RunReminder(ReminderSettings settings)
    {
        try
        {
            SetProcessDpiAwarenessContext(new IntPtr(-4));
        }
        catch
        {
        }

        uint dpi = 96;
        try
        {
            dpi = GetDpiForSystem();
            if (dpi == 0)
                dpi = 96;
        }
        catch
        {
            dpi = 96;
        }

        bannerText = SettingsStore.NormalizeReminderText(
            settings.ReminderText, settings.ComputerName);
        int logicalWidth = Math.Max(350, Math.Min(650, 120 + bannerText.Length * 8));
        bannerWidth = Scale(logicalWidth, dpi);
        bannerHeight = Scale(34, dpi);
        rightMargin = Scale(20, dpi);
        bottomLift = Scale(64, dpi);
        displayDevice = settings.DisplayDevice ?? "";

        preferredProcessId = 0;
        preferredProcessStartTicks = 0;
        establishedProcessId = 0;
        establishedProcessStartTicks = 0;
        targetWindow = IntPtr.Zero;
        targetSeen = false;
        missingTicks = 0;
        DateTime launchStartedUtc = DateTime.UtcNow;
        minimumNewProcessStartTicks = launchStartedUtc.AddSeconds(-10).Ticks;
        startupDeadlineUtc = launchStartedUtc.AddMinutes(5);

        string mstscPath = Path.Combine(Environment.SystemDirectory, "mstsc.exe");
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = mstscPath;
            if (!string.IsNullOrEmpty(settings.RdpFile))
            {
                if (!File.Exists(settings.RdpFile))
                {
                    ShowError("The copied .rdp connection file for this shortcut is " +
                        "missing. Run setup to create a new shortcut.");
                    return 1;
                }
                startInfo.Arguments = "\"" + settings.RdpFile + "\"";
            }
            else
            {
                startInfo.Arguments = "/v:" + settings.ComputerName +
                    (settings.FullScreen ? " /f" : "");
            }
            startInfo.UseShellExecute = true;
            using (Process launchedProcess = Process.Start(startInfo))
            {
                if (launchedProcess == null)
                    throw new InvalidOperationException(
                        "Windows did not return a Remote Desktop process.");
                preferredProcessId = launchedProcess.Id;
                TryGetProcessStartTicks(
                    preferredProcessId, out preferredProcessStartTicks);
            }
        }
        catch (Exception exception)
        {
            ShowError("Remote Desktop could not be started.\r\n\r\n" + exception.Message);
            return 1;
        }

        IntPtr instance = GetModuleHandle(null);
        string className = "RdpSessionReminderBannerWindow";
        WindowClass windowClass = new WindowClass();
        windowClass.Size = (uint)Marshal.SizeOf(typeof(WindowClass));
        windowClass.WindowProcedure = Marshal.GetFunctionPointerForDelegate(WindowProcedure);
        windowClass.Instance = instance;
        windowClass.Cursor = LoadCursor(IntPtr.Zero, new IntPtr(32512));
        windowClass.ClassName = className;
        if (RegisterClassEx(ref windowClass) == 0)
        {
            ShowError("The reminder window could not be initialized.");
            return 1;
        }

        uint extendedStyle = ExStyleTopmost | ExStyleTransparent |
            ExStyleToolWindow | ExStyleLayered | ExStyleNoActivate;
        bannerWindow = CreateWindowEx(extendedStyle, className, AppPaths.ProductName,
            WindowStylePopup, 0, 0, bannerWidth, bannerHeight,
            IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);
        if (bannerWindow == IntPtr.Zero)
        {
            ShowError("The reminder window could not be created.");
            return 1;
        }

        bannerFont = CreateFont(-Scale(17, dpi), 0, 0, 0, FontWeightSemibold,
            0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI Semibold");
        SetLayeredWindowAttributes(bannerWindow, 0, 247, LayeredAlpha);
        SetTimer(bannerWindow, MonitorTimer, 1000, IntPtr.Zero);
        MonitorRemoteSession();

        NativeMessage message;
        while (GetMessage(out message, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }
        return 0;
    }

    private static IntPtr HandleWindowMessage(
        IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (message == MessageTimer && wParam.ToInt64() == 1)
            {
                MonitorRemoteSession();
                return IntPtr.Zero;
            }

            if (message == MessagePaint)
            {
                PaintBanner(hWnd);
                return IntPtr.Zero;
            }

            if (message == MessageDisplayChange || message == MessageSettingChange)
            {
                if (targetSeen)
                    PositionAndShowBanner();
            }

            if (message == MessageEraseBackground)
                return new IntPtr(1);
            if (message == MessageNcHitTest)
                return new IntPtr(-1);

            if (message == MessageDestroy)
            {
                KillTimer(hWnd, MonitorTimer);
                if (bannerFont != IntPtr.Zero)
                {
                    DeleteObject(bannerFont);
                    bannerFont = IntPtr.Zero;
                }
                PostQuitMessage(0);
                return IntPtr.Zero;
            }
        }
        catch
        {
            DestroyWindow(hWnd);
            return IntPtr.Zero;
        }
        return DefWindowProc(hWnd, message, wParam, lParam);
    }

    private static void MonitorRemoteSession()
    {
        if (targetSeen)
        {
            if (IsEstablishedSessionWindow(targetWindow))
            {
                missingTicks = 0;
                PositionAndShowBanner();
                return;
            }

            IntPtr replacement = FindRdpSessionWindowForProcess(
                establishedProcessId, establishedProcessStartTicks);
            if (replacement != IntPtr.Zero)
            {
                targetWindow = replacement;
                missingTicks = 0;
                PositionAndShowBanner();
                return;
            }

            ShowWindow(bannerWindow, ShowWindowHide);
            missingTicks++;
            if (ShouldCloseReminder(true, missingTicks,
                    DateTime.UtcNow, startupDeadlineUtc))
                DestroyWindow(bannerWindow);
            return;
        }

        IntPtr initialWindow = FindInitialRdpSessionWindow(
            preferredProcessId, preferredProcessStartTicks,
            minimumNewProcessStartTicks);
        if (initialWindow != IntPtr.Zero)
        {
            uint processId;
            long processStartTicks;
            if (IsRdpSessionWindow(initialWindow, out processId) &&
                TryGetProcessStartTicks((int)processId, out processStartTicks))
            {
                targetWindow = initialWindow;
                establishedProcessId = (int)processId;
                establishedProcessStartTicks = processStartTicks;
                targetSeen = true;
                missingTicks = 0;
                PositionAndShowBanner();
                return;
            }
        }

        if (ShouldCloseReminder(false, 0, DateTime.UtcNow, startupDeadlineUtc))
            DestroyWindow(bannerWindow);
    }

    private static bool ShouldCloseReminder(bool sessionWasSeen, int lostTicks,
        DateTime currentUtc, DateTime deadlineUtc)
    {
        return sessionWasSeen ? lostTicks >= 5 : currentUtc >= deadlineUtc;
    }

    private static void PositionAndShowBanner()
    {
        NativeRect workArea;
        if (!TryGetSelectedWorkArea(out workArea) &&
            !SystemParametersInfo(GetWorkArea, 0, out workArea, 0))
        {
            workArea.Left = 0;
            workArea.Top = 0;
            workArea.Right = 1920;
            workArea.Bottom = 1080;
        }

        int x = workArea.Right - bannerWidth - rightMargin;
        int y = workArea.Bottom - bannerHeight - bottomLift;
        SetWindowPos(bannerWindow, Topmost, x, y, bannerWidth, bannerHeight,
            SetPositionNoActivate | SetPositionShowWindow);
    }

    private static bool TryGetSelectedWorkArea(out NativeRect workArea)
    {
        workArea = new NativeRect();
        IntPtr selectedMonitor = IntPtr.Zero;

        if (!string.IsNullOrEmpty(displayDevice))
        {
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                delegate(IntPtr monitor, IntPtr context, IntPtr rectangle, IntPtr parameter)
                {
                    MonitorInfo information = new MonitorInfo();
                    information.Size = (uint)Marshal.SizeOf(typeof(MonitorInfo));
                    if (GetMonitorInfo(monitor, ref information) &&
                        string.Equals(information.DeviceName, displayDevice,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        selectedMonitor = monitor;
                        return false;
                    }
                    return true;
                }, IntPtr.Zero);
        }

        if (selectedMonitor == IntPtr.Zero)
        {
            NativePoint origin = new NativePoint();
            selectedMonitor = MonitorFromPoint(origin, 1);
        }
        if (selectedMonitor == IntPtr.Zero)
            return false;

        MonitorInfo selectedInformation = new MonitorInfo();
        selectedInformation.Size = (uint)Marshal.SizeOf(typeof(MonitorInfo));
        if (!GetMonitorInfo(selectedMonitor, ref selectedInformation))
            return false;

        workArea = selectedInformation.WorkArea;
        return true;
    }

    private static void PaintBanner(IntPtr hWnd)
    {
        PaintStruct paint = new PaintStruct();
        paint.Reserved = new byte[32];
        IntPtr deviceContext = BeginPaint(hWnd, ref paint);
        if (deviceContext == IntPtr.Zero)
            return;

        NativeRect rectangle = new NativeRect();
        rectangle.Right = bannerWidth;
        rectangle.Bottom = bannerHeight;
        IntPtr brush = CreateSolidBrush(Color(24, 35, 53));
        FillRect(deviceContext, ref rectangle, brush);
        DeleteObject(brush);

        SetBkMode(deviceContext, TransparentBackground);
        SetTextColor(deviceContext, Color(255, 255, 255));
        IntPtr previousFont = IntPtr.Zero;
        if (bannerFont != IntPtr.Zero)
            previousFont = SelectObject(deviceContext, bannerFont);

        DrawText(deviceContext, bannerText, -1, ref rectangle,
            DrawCenter | DrawVerticalCenter | DrawSingleLine |
            DrawNoPrefix | DrawEndEllipsis);
        if (previousFont != IntPtr.Zero)
            SelectObject(deviceContext, previousFont);
        EndPaint(hWnd, ref paint);
    }

    private static IntPtr FindInitialRdpSessionWindow(
        int launchedProcessId, long launchedStartTicks, long earliestStartTicks)
    {
        IntPtr exact = IntPtr.Zero;
        List<IntPtr> related = new List<IntPtr>();
        Dictionary<int, int> processParents = GetProcessParents();
        EnumWindows(delegate(IntPtr hWnd, IntPtr parameter)
        {
            uint processId;
            if (!IsRdpSessionWindow(hWnd, out processId))
                return true;

            long processStartTicks;
            if (!TryGetProcessStartTicks((int)processId, out processStartTicks))
                return true;

            if (launchedStartTicks > 0 &&
                processId == (uint)launchedProcessId &&
                processStartTicks == launchedStartTicks)
            {
                exact = hWnd;
                return false;
            }

            if (processStartTicks < earliestStartTicks ||
                !IsDescendantProcess(
                    (int)processId, launchedProcessId, processParents))
                return true;

            related.Add(hWnd);
            return true;
        }, IntPtr.Zero);

        if (exact != IntPtr.Zero)
            return exact;
        return related.Count == 1 ? related[0] : IntPtr.Zero;
    }

    private static Dictionary<int, int> GetProcessParents()
    {
        Dictionary<int, int> parents = new Dictionary<int, int>();
        IntPtr snapshot = CreateToolhelp32Snapshot(SnapshotProcesses, 0);
        if (snapshot == new IntPtr(-1))
            return parents;

        try
        {
            ProcessEntry entry = new ProcessEntry();
            entry.Size = (uint)Marshal.SizeOf(typeof(ProcessEntry));
            if (!Process32First(snapshot, ref entry))
                return parents;

            do
            {
                parents[(int)entry.ProcessId] = (int)entry.ParentProcessId;
                entry.Size = (uint)Marshal.SizeOf(typeof(ProcessEntry));
            }
            while (Process32Next(snapshot, ref entry));
        }
        finally
        {
            CloseHandle(snapshot);
        }
        return parents;
    }

    private static bool IsDescendantProcess(
        int candidateId, int ancestorId, Dictionary<int, int> parents)
    {
        if (candidateId <= 0 || ancestorId <= 0 || candidateId == ancestorId)
            return false;

        int current = candidateId;
        HashSet<int> visited = new HashSet<int>();
        for (int depth = 0; depth < 16 && visited.Add(current); depth++)
        {
            int parent;
            if (!parents.TryGetValue(current, out parent) || parent <= 0)
                return false;
            if (parent == ancestorId)
                return true;
            current = parent;
        }
        return false;
    }

    private static IntPtr FindRdpSessionWindowForProcess(
        int processId, long processStartTicks)
    {
        IntPtr result = IntPtr.Zero;
        if (processId <= 0 || processStartTicks <= 0)
            return result;

        EnumWindows(delegate(IntPtr hWnd, IntPtr parameter)
        {
            uint candidateProcessId;
            long candidateStartTicks;
            if (IsRdpSessionWindow(hWnd, out candidateProcessId) &&
                candidateProcessId == (uint)processId &&
                TryGetProcessStartTicks(processId, out candidateStartTicks) &&
                candidateStartTicks == processStartTicks)
            {
                result = hWnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return result;
    }

    private static bool IsEstablishedSessionWindow(IntPtr hWnd)
    {
        uint processId;
        long processStartTicks;
        return hWnd != IntPtr.Zero &&
            IsRdpSessionWindow(hWnd, out processId) &&
            processId == (uint)establishedProcessId &&
            TryGetProcessStartTicks(establishedProcessId, out processStartTicks) &&
            processStartTicks == establishedProcessStartTicks;
    }

    private static bool IsRdpSessionWindow(IntPtr hWnd, out uint processId)
    {
        processId = 0;
        if (!IsWindow(hWnd) || !IsWindowVisible(hWnd))
            return false;

        StringBuilder className = new StringBuilder(128);
        GetClassName(hWnd, className, className.Capacity);
        if (!string.Equals(className.ToString(), "TscShellContainerClass",
            StringComparison.Ordinal))
            return false;

        GetWindowThreadProcessId(hWnd, out processId);
        if (processId == 0)
            return false;

        try
        {
            using (Process process = Process.GetProcessById((int)processId))
            {
                return string.Equals(process.ProcessName, "mstsc",
                    StringComparison.OrdinalIgnoreCase);
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetProcessStartTicks(int processId, out long startTicks)
    {
        startTicks = 0;
        try
        {
            using (Process process = Process.GetProcessById(processId))
            {
                if (process.HasExited ||
                    !string.Equals(process.ProcessName, "mstsc",
                        StringComparison.OrdinalIgnoreCase))
                    return false;
                startTicks = process.StartTime.ToUniversalTime().Ticks;
                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool HasArgument(string[] args, string value)
    {
        foreach (string argument in args)
        {
            if (string.Equals(argument, value, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static int FindArgument(string[] args, string value)
    {
        for (int index = 0; index < args.Length; index++)
        {
            if (string.Equals(args[index], value, StringComparison.OrdinalIgnoreCase))
                return index;
        }
        return -1;
    }

    private static void StartSetup()
    {
        string currentDirectory = Path.GetDirectoryName(
            Process.GetCurrentProcess().MainModule.FileName);
        string nearbySetup = Path.Combine(currentDirectory, AppPaths.SetupFileName);
        string setupPath = File.Exists(AppPaths.SetupPath)
            ? AppPaths.SetupPath
            : nearbySetup;

        if (!File.Exists(setupPath))
        {
            ShowError("Setup was not found. Download the complete release package and run " +
                AppPaths.SetupFileName + ".");
            return;
        }

        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = setupPath;
        startInfo.UseShellExecute = true;
        Process.Start(startInfo);
    }

    private static int RunSelfTest()
    {
        try
        {
            if (!SettingsStore.IsValidComputerName("workstation-01") ||
                !SettingsStore.IsValidComputerName("10.0.0.25") ||
                !SettingsStore.IsValidComputerName("server.example.com:3390") ||
                !SettingsStore.IsValidComputerName("[2001:db8::1]:3389") ||
                !SettingsStore.IsValidComputerName("2001:db8::1") ||
                SettingsStore.IsValidComputerName("bad name") ||
                SettingsStore.IsValidComputerName("-bad.example") ||
                SettingsStore.IsValidComputerName("bad-.example") ||
                SettingsStore.IsValidComputerName("bad..example") ||
                SettingsStore.IsValidComputerName("999.999.999.999") ||
                SettingsStore.IsValidComputerName("[not-ipv6]") ||
                SettingsStore.IsValidComputerName("server:") ||
                SettingsStore.IsValidComputerName("server:70000") ||
                SettingsStore.IsValidComputerName("/v:other"))
                return 10;

            string profileId = Guid.NewGuid().ToString("N");
            if (!SettingsStore.IsValidProfileId(profileId) ||
                SettingsStore.IsValidProfileId("../profile"))
                return 14;

            string directory = Path.Combine(Path.GetTempPath(),
                "RdpSessionReminder-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "settings.ini");
            ReminderSettings expected = new ReminderSettings();
            expected.ComputerName = "workstation-01";
            expected.FullScreen = true;
            expected.ShortcutName = "Remote Workstation";
            expected.RdpFile = "";
            expected.DisplayDevice = "\\\\.\\DISPLAY1";
            expected.ReminderText = "REMOTE SESSION - TEST LAB";
            SettingsStore.SaveTo(path, expected);

            ReminderSettings actual;
            bool loaded = SettingsStore.TryLoadFrom(path, out actual);
            Directory.Delete(directory, true);
            if (!loaded || actual.ComputerName != expected.ComputerName ||
                !actual.FullScreen || actual.ShortcutName != expected.ShortcutName ||
                actual.RdpFile != expected.RdpFile ||
                actual.DisplayDevice != expected.DisplayDevice ||
                actual.ReminderText != expected.ReminderText)
                return 11;

            DateTime deadline = DateTime.UtcNow.AddMinutes(1);
            if (ShouldCloseReminder(false, 0, DateTime.UtcNow, deadline) ||
                ShouldCloseReminder(true, 4, DateTime.UtcNow, deadline) ||
                !ShouldCloseReminder(true, 5, DateTime.UtcNow, deadline) ||
                !ShouldCloseReminder(false, 0, deadline, deadline))
                return 15;

            Dictionary<int, int> parents = new Dictionary<int, int>();
            parents[300] = 200;
            parents[200] = 100;
            parents[400] = 50;
            if (!IsDescendantProcess(300, 100, parents) ||
                !IsDescendantProcess(200, 100, parents) ||
                IsDescendantProcess(400, 100, parents) ||
                IsDescendantProcess(100, 100, parents))
                return 16;

            Dictionary<int, int> liveParents = GetProcessParents();
            if (!liveParents.ContainsKey(Process.GetCurrentProcess().Id))
                return 17;

            displayDevice = "";
            NativeRect workArea;
            if (!TryGetSelectedWorkArea(out workArea) ||
                workArea.Right <= workArea.Left || workArea.Bottom <= workArea.Top)
                return 13;
            return 0;
        }
        catch
        {
            return 12;
        }
    }

    private static string StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char character in value.ToUpperInvariant())
            {
                hash ^= character;
                hash *= 16777619;
            }
            return hash.ToString("X8");
        }
    }

    private static int Scale(int value, uint dpi)
    {
        return (int)Math.Round(value * dpi / 96.0);
    }

    private static uint Color(byte red, byte green, byte blue)
    {
        return (uint)(red | (green << 8) | (blue << 16));
    }

    private static void ShowError(string message)
    {
        MessageBox(IntPtr.Zero, message, AppPaths.ProductName, 0x00000010);
    }
}
