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
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

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
    private struct StartupInfo
    {
        public int Size;
        public string Reserved;
        public string Desktop;
        public string Title;
        public int X;
        public int Y;
        public int XSize;
        public int YSize;
        public int XCountChars;
        public int YCountChars;
        public int FillAttribute;
        public int Flags;
        public short ShowWindow;
        public short Reserved2Bytes;
        public IntPtr Reserved2;
        public IntPtr StandardInput;
        public IntPtr StandardOutput;
        public IntPtr StandardError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr Process;
        public IntPtr Thread;
        public uint ProcessId;
        public uint ThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobBasicAccountingInformation
    {
        public long TotalUserTime;
        public long TotalKernelTime;
        public long ThisPeriodTotalUserTime;
        public long ThisPeriodTotalKernelTime;
        public uint TotalPageFaultCount;
        public uint TotalProcesses;
        public uint ActiveProcesses;
        public uint TotalTerminatedProcesses;
    }

    private sealed class RdpWindowCandidate
    {
        public IntPtr Window;
        public uint ProcessId;
        public IntPtr ProcessHandle;
    }

    private enum LifecycleDecision
    {
        Wait,
        Attach,
        KeepVisible,
        HideAndWait,
        Stop
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
    private static extern bool InvalidateRect(
        IntPtr hWnd, IntPtr rectangle, bool erase);

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
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr jobAttributes, string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(
        IntPtr job, IntPtr process);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool IsProcessInJob(
        IntPtr process, IntPtr job, out bool result);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool QueryInformationJobObject(
        IntPtr job, int informationClass,
        ref JobBasicAccountingInformation information,
        uint informationLength, IntPtr returnLength);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode,
        ExactSpelling = true, SetLastError = true)]
    private static extern bool CreateProcessW(
        string applicationName, StringBuilder commandLine,
        IntPtr processAttributes, IntPtr threadAttributes,
        bool inheritHandles, uint creationFlags, IntPtr environment,
        string currentDirectory, ref StartupInfo startupInfo,
        out ProcessInformation processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(IntPtr thread);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateProcess(IntPtr process, uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(
        uint desiredAccess, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageName(
        IntPtr process, uint flags, StringBuilder executableName,
        ref uint size);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadCursor(IntPtr instance, IntPtr cursorName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(
        IntPtr hWnd, string text, string caption, uint type);

    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(
        IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

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
    private const uint MessageDpiChanged = 0x02E0;
    private const uint LayeredAlpha = 0x00000002;
    private const uint SetPositionNoActivate = 0x0010;
    private const uint SetPositionShowWindow = 0x0040;
    private const uint GetWorkArea = 0x0030;
    private const uint DrawCenter = 0x00000001;
    private const uint DrawVerticalCenter = 0x00000004;
    private const uint DrawSingleLine = 0x00000020;
    private const uint DrawNoPrefix = 0x00000800;
    private const uint DrawEndEllipsis = 0x00008000;
    private const uint CreateSuspended = 0x00000004;
    private const uint ProcessQueryLimitedInformation = 0x00001000;
    private const uint Synchronize = 0x00100000;
    private const uint WaitTimeout = 0x00000102;
    private const uint ResumeThreadFailed = 0xFFFFFFFF;
    private const int JobObjectBasicAccountingInformation = 1;
    private const int MonitorDpiEffective = 0;
    private const int ShowWindowHide = 0;
    private const int TransparentBackground = 1;
    private const int FontWeightSemibold = 600;
    private static readonly IntPtr Topmost = new IntPtr(-1);
    private static readonly UIntPtr MonitorTimer = new UIntPtr(1);
    private static readonly WindowProc WindowProcedure = HandleWindowMessage;

    private static IntPtr jobHandle;
    private static IntPtr rootProcessHandle;
    private static IntPtr selectedProcessHandle;
    private static string canonicalMstscPath;
    private static int establishedProcessId;
    private static IntPtr targetWindow;
    private static IntPtr bannerWindow;
    private static IntPtr bannerFont;
    private static string bannerText;
    private static string displayDevice;
    private static bool targetSeen;
    private static int missingTicks;
    private static int bannerWidth;
    private static int bannerHeight;
    private static int bannerLogicalWidth;
    private static uint bannerDpi;
    private static int rightMargin;
    private static int bottomLift;
    private static Mutex instanceMutex;
    private static bool mutexAcquired;

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
        if (!string.IsNullOrEmpty(settings.RdpFile))
        {
            settings.RdpFile = AppPaths.GetProfileConnectionPath(profileId);
            RefreshTargetFromRdpFile(settings);
        }

        string mutexName = "Local\\RdpSessionReminder-" + profileId.ToUpperInvariant();
        mutexAcquired = false;
        try
        {
            instanceMutex = new Mutex(false, mutexName);
            try
            {
                mutexAcquired = instanceMutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                mutexAcquired = true;
            }
        }
        catch (Exception exception)
        {
            if (instanceMutex != null)
            {
                instanceMutex.Dispose();
                instanceMutex = null;
            }
            ShowError("The reminder could not reserve this shortcut profile.\r\n\r\n" +
                exception.Message);
            return 1;
        }

        if (!mutexAcquired)
        {
            instanceMutex.Dispose();
            instanceMutex = null;
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
            if (mutexAcquired && instanceMutex != null)
            {
                try
                {
                    instanceMutex.ReleaseMutex();
                }
                catch
                {
                }
            }
            mutexAcquired = false;
            if (instanceMutex != null)
            {
                instanceMutex.Dispose();
                instanceMutex = null;
            }
        }
    }

    private static int RunReminder(ReminderSettings settings)
    {
        try
        {
            try
            {
                SetProcessDpiAwarenessContext(new IntPtr(-4));
            }
            catch
            {
            }

            jobHandle = IntPtr.Zero;
            rootProcessHandle = IntPtr.Zero;
            selectedProcessHandle = IntPtr.Zero;
            establishedProcessId = 0;
            targetWindow = IntPtr.Zero;
            bannerWindow = IntPtr.Zero;
            bannerFont = IntPtr.Zero;
            targetSeen = false;
            missingTicks = 0;
            bannerText = SettingsStore.NormalizeReminderText(
                settings.ReminderText, settings.ComputerName);
            bannerLogicalWidth = Math.Max(350,
                Math.Min(650, 120 + bannerText.Length * 8));
            displayDevice = settings.DisplayDevice ?? "";
            ApplyBannerDpi(GetSelectedDisplayDpi());
            canonicalMstscPath = CanonicalizePath(
                Path.Combine(Environment.SystemDirectory, "mstsc.exe"));

            string launchArguments;
            if (!string.IsNullOrEmpty(settings.RdpFile))
            {
                if (!File.Exists(settings.RdpFile))
                {
                    ShowError("The copied .rdp connection file for this shortcut is " +
                        "missing. Run setup to create a new shortcut.");
                    return 1;
                }
                launchArguments = QuoteCommandLineArgument(settings.RdpFile);
            }
            else
            {
                launchArguments = BuildDirectArguments(settings);
            }

            string launchError;
            if (!LaunchMstscInPrivateJob(launchArguments, out launchError))
            {
                ShowError("Remote Desktop could not be started.\r\n\r\n" +
                    launchError);
                return 1;
            }

            IntPtr instance = GetModuleHandle(null);
            string className = "RdpSessionReminderBannerWindow";
            WindowClass windowClass = new WindowClass();
            windowClass.Size = (uint)Marshal.SizeOf(typeof(WindowClass));
            windowClass.WindowProcedure =
                Marshal.GetFunctionPointerForDelegate(WindowProcedure);
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
            bannerWindow = CreateWindowEx(extendedStyle, className,
                AppPaths.ProductName, WindowStylePopup, 0, 0,
                bannerWidth, bannerHeight, IntPtr.Zero, IntPtr.Zero,
                instance, IntPtr.Zero);
            if (bannerWindow == IntPtr.Zero)
            {
                ShowError("The reminder window could not be created.");
                return 1;
            }

            RecreateBannerFont();
            SetLayeredWindowAttributes(bannerWindow, 0, 247, LayeredAlpha);
            if (SetTimer(bannerWindow, MonitorTimer, 1000, IntPtr.Zero) ==
                UIntPtr.Zero)
            {
                ShowError("The reminder monitor could not be initialized.");
                return 1;
            }
            MonitorRemoteSession();

            NativeMessage message;
            while (GetMessage(out message, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref message);
                DispatchMessage(ref message);
            }
            return 0;
        }
        catch (Exception exception)
        {
            ShowError("The reminder stopped because of an unexpected error.\r\n\r\n" +
                exception.Message);
            return 1;
        }
        finally
        {
            CleanupRuntimeResources();
        }
    }

    private static string BuildDirectArguments(ReminderSettings settings)
    {
        if (settings == null ||
            !SettingsStore.IsValidComputerName(settings.ComputerName))
            throw new ArgumentException("A valid Remote Desktop target is required.");
        return "/v:" + SettingsStore.NormalizeComputerName(settings.ComputerName) +
            (settings.FullScreen ? " /f" : "");
    }

    private static void RefreshTargetFromRdpFile(ReminderSettings settings)
    {
        if (settings == null || string.IsNullOrEmpty(settings.RdpFile))
            return;

        string target = SettingsStore.NormalizeComputerName(
            RdpFileTargetReader.TryReadTarget(settings.RdpFile));
        if (!SettingsStore.IsValidComputerName(target))
            return;

        string oldDefault = SettingsStore.NormalizeReminderText(
            "", settings.ComputerName);
        bool usesAutomaticReminder = string.Equals(
            settings.ReminderText, oldDefault, StringComparison.Ordinal);
        settings.ComputerName = target;
        if (usesAutomaticReminder)
            settings.ReminderText = SettingsStore.NormalizeReminderText("", target);
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

            if (message == MessageDpiChanged)
            {
                uint dpi = (uint)(wParam.ToInt64() & 0xFFFF);
                ApplyBannerDpi(dpi);
                if (targetSeen)
                    PositionAndShowBanner();
                return IntPtr.Zero;
            }

            if (message == MessageDisplayChange || message == MessageSettingChange)
            {
                ApplyBannerDpi(GetSelectedDisplayDpi());
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
                if (hWnd == bannerWindow)
                    bannerWindow = IntPtr.Zero;
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
            bool processAlive = IsProcessHandleLive(selectedProcessHandle);
            bool currentWindowValid = processAlive &&
                IsPinnedSessionWindow(targetWindow);
            IntPtr replacement = IntPtr.Zero;
            if (processAlive && !currentWindowValid)
                replacement = FindRdpSessionWindowForPinnedProcess();

            int nextMissingTicks = currentWindowValid ||
                replacement != IntPtr.Zero ? 0 : missingTicks + 1;
            LifecycleDecision decision = DecideAttachedLifecycle(
                processAlive, currentWindowValid,
                replacement != IntPtr.Zero, nextMissingTicks);

            if (decision == LifecycleDecision.Stop)
            {
                DestroyWindow(bannerWindow);
                return;
            }

            if (decision == LifecycleDecision.KeepVisible)
            {
                if (replacement != IntPtr.Zero)
                    targetWindow = replacement;
                missingTicks = 0;
                PositionAndShowBanner();
                return;
            }

            ShowWindow(bannerWindow, ShowWindowHide);
            missingTicks = nextMissingTicks;
            return;
        }

        uint activeProcesses;
        if (!TryGetActiveJobProcessCount(out activeProcesses))
        {
            DestroyWindow(bannerWindow);
            return;
        }
        if (DecideInitialLifecycle(activeProcesses, 0) ==
            LifecycleDecision.Stop)
        {
            DestroyWindow(bannerWindow);
            return;
        }

        List<RdpWindowCandidate> candidates = FindInitialRdpSessionCandidates();
        try
        {
            LifecycleDecision decision = DecideInitialLifecycle(
                activeProcesses, candidates.Count);
            if (decision == LifecycleDecision.Stop)
            {
                DestroyWindow(bannerWindow);
                return;
            }

            if (decision != LifecycleDecision.Attach)
                return;

            RdpWindowCandidate candidate = candidates[0];
            if (!IsCandidateStillValid(candidate))
                return;

            targetWindow = candidate.Window;
            establishedProcessId = (int)candidate.ProcessId;
            selectedProcessHandle = candidate.ProcessHandle;
            candidate.ProcessHandle = IntPtr.Zero;
            targetSeen = true;
            missingTicks = 0;
            PositionAndShowBanner();
        }
        finally
        {
            CloseCandidates(candidates);
        }
    }

    private static LifecycleDecision DecideInitialLifecycle(
        uint activeProcesses, int candidateCount)
    {
        if (activeProcesses == 0)
            return LifecycleDecision.Stop;
        return candidateCount == 1
            ? LifecycleDecision.Attach
            : LifecycleDecision.Wait;
    }

    private static LifecycleDecision DecideAttachedLifecycle(
        bool processAlive, bool currentWindowValid,
        bool replacementFound, int missingWindowTicks)
    {
        if (!processAlive)
            return LifecycleDecision.Stop;
        if (currentWindowValid || replacementFound)
            return LifecycleDecision.KeepVisible;
        return missingWindowTicks >= 5
            ? LifecycleDecision.Stop
            : LifecycleDecision.HideAndWait;
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
        IntPtr selectedMonitor;
        MonitorInfo selectedInformation;
        if (!TryGetSelectedMonitor(out selectedMonitor, out selectedInformation))
            return false;

        workArea = selectedInformation.WorkArea;
        return true;
    }

    private static bool TryGetSelectedMonitor(
        out IntPtr selectedMonitor, out MonitorInfo selectedInformation)
    {
        selectedMonitor = IntPtr.Zero;
        selectedInformation = new MonitorInfo();
        IntPtr foundMonitor = IntPtr.Zero;

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
                        foundMonitor = monitor;
                        return false;
                    }
                    return true;
                }, IntPtr.Zero);
        }

        if (foundMonitor == IntPtr.Zero)
        {
            NativePoint origin = new NativePoint();
            foundMonitor = MonitorFromPoint(origin, 1);
        }
        if (foundMonitor == IntPtr.Zero)
            return false;

        selectedMonitor = foundMonitor;
        selectedInformation.Size = (uint)Marshal.SizeOf(typeof(MonitorInfo));
        if (!GetMonitorInfo(selectedMonitor, ref selectedInformation))
            return false;
        return true;
    }

    private static uint GetSelectedDisplayDpi()
    {
        try
        {
            IntPtr monitor;
            MonitorInfo information;
            uint dpiX;
            uint dpiY;
            if (TryGetSelectedMonitor(out monitor, out information) &&
                GetDpiForMonitor(monitor, MonitorDpiEffective,
                    out dpiX, out dpiY) == 0 && dpiX > 0)
                return dpiX;
        }
        catch
        {
        }

        try
        {
            uint systemDpi = GetDpiForSystem();
            return systemDpi == 0 ? 96 : systemDpi;
        }
        catch
        {
            return 96;
        }
    }

    private static void ApplyBannerDpi(uint dpi)
    {
        bannerDpi = dpi == 0 ? 96 : dpi;
        bannerWidth = Scale(bannerLogicalWidth, bannerDpi);
        bannerHeight = Scale(34, bannerDpi);
        rightMargin = Scale(20, bannerDpi);
        bottomLift = Scale(64, bannerDpi);
        if (bannerWindow != IntPtr.Zero)
        {
            RecreateBannerFont();
            InvalidateRect(bannerWindow, IntPtr.Zero, true);
        }
    }

    private static void RecreateBannerFont()
    {
        IntPtr newFont = CreateFont(-Scale(17, bannerDpi), 0, 0, 0,
            FontWeightSemibold, 0, 0, 0, 1, 0, 0, 5, 0,
            "Segoe UI Semibold");
        if (newFont == IntPtr.Zero)
            return;

        IntPtr oldFont = bannerFont;
        bannerFont = newFont;
        if (oldFont != IntPtr.Zero)
            DeleteObject(oldFont);
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

    private static List<RdpWindowCandidate> FindInitialRdpSessionCandidates()
    {
        List<RdpWindowCandidate> candidates = new List<RdpWindowCandidate>();
        EnumWindows(delegate(IntPtr hWnd, IntPtr parameter)
        {
            uint processId;
            if (!TryGetRdpWindowProcessId(hWnd, out processId))
                return true;

            IntPtr processHandle = OpenProcess(
                ProcessQueryLimitedInformation | Synchronize,
                false, processId);
            if (processHandle == IntPtr.Zero)
                return true;

            bool keepHandle = false;
            try
            {
                uint recheckedProcessId;
                bool inJob;
                if (!TryGetRdpWindowProcessId(hWnd, out recheckedProcessId) ||
                    recheckedProcessId != processId ||
                    !IsProcessHandleLive(processHandle) ||
                    !IsExpectedMstscProcess(processHandle) ||
                    !IsProcessInJob(processHandle, jobHandle, out inJob) ||
                    !inJob)
                    return true;

                RdpWindowCandidate candidate = new RdpWindowCandidate();
                candidate.Window = hWnd;
                candidate.ProcessId = processId;
                candidate.ProcessHandle = processHandle;
                candidates.Add(candidate);
                keepHandle = true;
                return true;
            }
            finally
            {
                if (!keepHandle)
                    CloseHandle(processHandle);
            }
        }, IntPtr.Zero);
        return candidates;
    }

    private static bool IsCandidateStillValid(RdpWindowCandidate candidate)
    {
        if (candidate == null || candidate.ProcessHandle == IntPtr.Zero ||
            !IsProcessHandleLive(candidate.ProcessHandle) ||
            !IsExpectedMstscProcess(candidate.ProcessHandle))
            return false;

        bool inJob;
        if (!IsProcessInJob(candidate.ProcessHandle, jobHandle, out inJob) ||
            !inJob)
            return false;

        uint recheckedProcessId;
        return TryGetRdpWindowProcessId(candidate.Window,
            out recheckedProcessId) &&
            recheckedProcessId == candidate.ProcessId;
    }

    private static void CloseCandidates(List<RdpWindowCandidate> candidates)
    {
        if (candidates == null)
            return;
        foreach (RdpWindowCandidate candidate in candidates)
        {
            if (candidate != null && candidate.ProcessHandle != IntPtr.Zero)
            {
                CloseHandle(candidate.ProcessHandle);
                candidate.ProcessHandle = IntPtr.Zero;
            }
        }
    }

    private static IntPtr FindRdpSessionWindowForPinnedProcess()
    {
        IntPtr result = IntPtr.Zero;
        if (establishedProcessId <= 0 ||
            !IsProcessHandleLive(selectedProcessHandle))
            return result;

        EnumWindows(delegate(IntPtr hWnd, IntPtr parameter)
        {
            uint candidateProcessId;
            if (TryGetRdpWindowProcessId(hWnd, out candidateProcessId) &&
                candidateProcessId == (uint)establishedProcessId)
            {
                result = hWnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return result;
    }

    private static bool IsPinnedSessionWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero ||
            !IsProcessHandleLive(selectedProcessHandle))
            return false;
        uint processId;
        return TryGetRdpWindowProcessId(hWnd, out processId) &&
            processId == (uint)establishedProcessId;
    }

    private static bool TryGetRdpWindowProcessId(
        IntPtr hWnd, out uint processId)
    {
        processId = 0;
        if (!IsWindow(hWnd) || !IsWindowVisible(hWnd))
            return false;

        StringBuilder className = new StringBuilder(128);
        if (GetClassName(hWnd, className, className.Capacity) == 0 ||
            !string.Equals(className.ToString(), "TscShellContainerClass",
                StringComparison.Ordinal))
            return false;

        GetWindowThreadProcessId(hWnd, out processId);
        return processId != 0;
    }

    private static bool IsProcessHandleLive(IntPtr processHandle)
    {
        return processHandle != IntPtr.Zero &&
            WaitForSingleObject(processHandle, 0) == WaitTimeout;
    }

    private static bool IsExpectedMstscProcess(IntPtr processHandle)
    {
        if (processHandle == IntPtr.Zero ||
            string.IsNullOrEmpty(canonicalMstscPath))
            return false;

        uint capacity = 32768;
        StringBuilder executablePath = new StringBuilder((int)capacity);
        if (!QueryFullProcessImageName(processHandle, 0,
                executablePath, ref capacity))
            return false;

        try
        {
            return string.Equals(CanonicalizePath(executablePath.ToString()),
                canonicalMstscPath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetActiveJobProcessCount(out uint activeProcesses)
    {
        activeProcesses = 0;
        if (jobHandle == IntPtr.Zero)
            return false;
        JobBasicAccountingInformation information =
            new JobBasicAccountingInformation();
        if (!QueryInformationJobObject(jobHandle,
                JobObjectBasicAccountingInformation, ref information,
                (uint)Marshal.SizeOf(typeof(JobBasicAccountingInformation)),
                IntPtr.Zero))
            return false;
        activeProcesses = information.ActiveProcesses;
        return true;
    }

    private static bool LaunchMstscInPrivateJob(
        string arguments, out string error)
    {
        error = "";
        IntPtr newJob = IntPtr.Zero;
        ProcessInformation processInformation = new ProcessInformation();
        bool processCreated = false;
        bool launchSucceeded = false;
        try
        {
            newJob = CreateJobObject(IntPtr.Zero, null);
            if (newJob == IntPtr.Zero)
            {
                error = LastWin32Error("Windows could not create the process group");
                return false;
            }

            StartupInfo startupInfo = new StartupInfo();
            startupInfo.Size = Marshal.SizeOf(typeof(StartupInfo));
            string command = QuoteCommandLineArgument(canonicalMstscPath);
            if (!string.IsNullOrEmpty(arguments))
                command += " " + arguments;
            StringBuilder commandLine = new StringBuilder(command);
            if (!CreateProcessW(canonicalMstscPath, commandLine,
                    IntPtr.Zero, IntPtr.Zero, false, CreateSuspended,
                    IntPtr.Zero, Environment.SystemDirectory,
                    ref startupInfo, out processInformation))
            {
                error = LastWin32Error("Windows could not create Remote Desktop");
                return false;
            }
            processCreated = true;

            if (!AssignProcessToJobObject(newJob, processInformation.Process))
            {
                error = LastWin32Error(
                    "Windows could not isolate the Remote Desktop process");
                return false;
            }

            if (ResumeThread(processInformation.Thread) == ResumeThreadFailed)
            {
                error = LastWin32Error("Windows could not start Remote Desktop");
                return false;
            }

            jobHandle = newJob;
            newJob = IntPtr.Zero;
            rootProcessHandle = processInformation.Process;
            processInformation.Process = IntPtr.Zero;
            launchSucceeded = true;
            return true;
        }
        finally
        {
            if (processInformation.Thread != IntPtr.Zero)
            {
                CloseHandle(processInformation.Thread);
                processInformation.Thread = IntPtr.Zero;
            }
            if (!launchSucceeded && processCreated &&
                processInformation.Process != IntPtr.Zero)
                TerminateProcess(processInformation.Process, 1);
            if (processInformation.Process != IntPtr.Zero)
                CloseHandle(processInformation.Process);
            if (newJob != IntPtr.Zero)
                CloseHandle(newJob);
        }
    }

    private static void CleanupRuntimeResources()
    {
        if (bannerWindow != IntPtr.Zero && IsWindow(bannerWindow))
            DestroyWindow(bannerWindow);
        bannerWindow = IntPtr.Zero;

        if (bannerFont != IntPtr.Zero)
        {
            DeleteObject(bannerFont);
            bannerFont = IntPtr.Zero;
        }

        if (selectedProcessHandle != IntPtr.Zero)
        {
            CloseHandle(selectedProcessHandle);
            selectedProcessHandle = IntPtr.Zero;
        }
        if (rootProcessHandle != IntPtr.Zero)
        {
            CloseHandle(rootProcessHandle);
            rootProcessHandle = IntPtr.Zero;
        }
        if (jobHandle != IntPtr.Zero)
        {
            CloseHandle(jobHandle);
            jobHandle = IntPtr.Zero;
        }
        targetWindow = IntPtr.Zero;
        targetSeen = false;
        establishedProcessId = 0;
    }

    private static string CanonicalizePath(string path)
    {
        string value = path ?? "";
        if (value.StartsWith("\\\\?\\", StringComparison.Ordinal))
            value = value.Substring(4);
        return Path.GetFullPath(value);
    }

    private static string QuoteCommandLineArgument(string value)
    {
        if (value == null)
            value = "";
        StringBuilder quoted = new StringBuilder();
        quoted.Append('"');
        int backslashes = 0;
        foreach (char character in value)
        {
            if (character == '\\')
            {
                backslashes++;
                continue;
            }
            if (character == '"')
            {
                quoted.Append('\\', backslashes * 2 + 1);
                quoted.Append('"');
                backslashes = 0;
                continue;
            }
            quoted.Append('\\', backslashes);
            backslashes = 0;
            quoted.Append(character);
        }
        quoted.Append('\\', backslashes * 2);
        quoted.Append('"');
        return quoted.ToString();
    }

    private static string LastWin32Error(string operation)
    {
        int errorCode = Marshal.GetLastWin32Error();
        string message = new System.ComponentModel.Win32Exception(errorCode).Message;
        return operation + " (" + errorCode + "): " + message;
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
                !SettingsStore.IsValidComputerName("10.0.0.25:3390") ||
                !SettingsStore.IsValidComputerName("server.example.com:3390") ||
                !SettingsStore.IsValidComputerName("[2001:db8::1]:3389") ||
                !SettingsStore.IsValidComputerName("2001:db8::1") ||
                SettingsStore.IsValidComputerName("bad name") ||
                SettingsStore.IsValidComputerName("server: 3389") ||
                SettingsStore.IsValidComputerName("010.0.0.1") ||
                SettingsStore.IsValidComputerName("0x7f000001") ||
                SettingsStore.IsValidComputerName("a\n.example") ||
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
            expected.RdpFile = Path.Combine(directory, "connection.rdp");
            expected.DisplayDevice = "\\\\.\\DISPLAY1";
            expected.ReminderText = "REMOTE SESSION - TEST LAB";
            SettingsStore.SaveTo(path, expected);

            ReminderSettings actual;
            bool loaded = SettingsStore.TryLoadFrom(path, out actual);
            if (!loaded || actual.ComputerName != expected.ComputerName ||
                !actual.FullScreen || actual.ShortcutName != expected.ShortcutName ||
                actual.RdpFile != expected.RdpFile ||
                actual.DisplayDevice != expected.DisplayDevice ||
                actual.ReminderText != expected.ReminderText)
                return 11;

            string rdpPath = expected.RdpFile;
            File.WriteAllLines(rdpPath, new string[] {
                "full address:s:lab-a",
                "alternate full address:s:lab-b"
            });
            ReminderSettings migrated = new ReminderSettings();
            migrated.ComputerName = "lab-a";
            migrated.RdpFile = rdpPath;
            migrated.ReminderText = "REMOTE SESSION - LAB-A";
            RefreshTargetFromRdpFile(migrated);
            if (migrated.ComputerName != "lab-b" ||
                migrated.ReminderText != "REMOTE SESSION - LAB-B")
                return 18;

            migrated.ComputerName = "lab-a";
            migrated.ReminderText = "CUSTOM REMINDER";
            RefreshTargetFromRdpFile(migrated);
            if (migrated.ComputerName != "lab-b" ||
                migrated.ReminderText != "CUSTOM REMINDER")
                return 19;

            string legacyPath = Path.Combine(directory, "legacy.ini");
            File.WriteAllText(legacyPath,
                "Computer=010.0.0.1" + Environment.NewLine +
                "FullScreen=1" + Environment.NewLine +
                "ShortcutName=Legacy" + Environment.NewLine +
                "RdpFile=C:\\legacy\\connection.rdp" + Environment.NewLine +
                "DisplayDevice=" + Environment.NewLine +
                "ReminderText=REMOTE SESSION - 010.0.0.1" + Environment.NewLine);
            ReminderSettings legacy;
            bool legacyLoaded = SettingsStore.TryLoadFrom(
                legacyPath, rdpPath, out legacy);
            Directory.Delete(directory, true);
            if (!legacyLoaded || legacy.ComputerName != "lab-b" ||
                legacy.ReminderText != "REMOTE SESSION - LAB-B")
                return 20;

            if (DecideInitialLifecycle(0, 1) != LifecycleDecision.Stop ||
                DecideInitialLifecycle(1, 0) != LifecycleDecision.Wait ||
                DecideInitialLifecycle(1, 2) != LifecycleDecision.Wait ||
                DecideInitialLifecycle(2, 1) != LifecycleDecision.Attach ||
                DecideAttachedLifecycle(false, true, true, 0) !=
                    LifecycleDecision.Stop ||
                DecideAttachedLifecycle(true, true, false, 0) !=
                    LifecycleDecision.KeepVisible ||
                DecideAttachedLifecycle(true, false, true, 1) !=
                    LifecycleDecision.KeepVisible ||
                DecideAttachedLifecycle(true, false, false, 4) !=
                    LifecycleDecision.HideAndWait ||
                DecideAttachedLifecycle(true, false, false, 5) !=
                    LifecycleDecision.Stop)
                return 15;

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
