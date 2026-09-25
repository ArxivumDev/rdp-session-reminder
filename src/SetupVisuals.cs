using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

// Visuals in this file exist only while the setup window is open. They use
// normal WinForms painting and do not create a timer, service, tray process, or
// background animation.
internal enum UiThemeMode
{
    Dark,
    Light
}

internal static class UiPreferenceStore
{
    private const int MaximumPreferenceBytes = 4096;
    private const string FileName = "ui-settings.ini";

    public static string PreferencePath
    {
        get { return Path.Combine(AppPaths.InstallDirectory, FileName); }
    }

    public static UiThemeMode Load()
    {
        return LoadFrom(PreferencePath);
    }

    public static UiThemeMode LoadFrom(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return UiThemeMode.Dark;

        try
        {
            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
                return UiThemeMode.Dark;

            using (FileStream stream = new FileStream(fullPath, FileMode.Open,
                FileAccess.Read, FileShare.Read))
            {
                if (stream.Length <= 0 || stream.Length > MaximumPreferenceBytes)
                    return UiThemeMode.Dark;
                using (StreamReader reader = new StreamReader(stream,
                    Encoding.UTF8, true))
                    return Parse(reader.ReadToEnd());
            }
        }
        catch
        {
            // Appearance preferences are optional. A damaged or inaccessible
            // file must never prevent the configuration interface from opening.
            return UiThemeMode.Dark;
        }
    }

    public static void Save(UiThemeMode mode)
    {
        SaveTo(PreferencePath, mode);
    }

    public static void SaveTo(string path, UiThemeMode mode)
    {
        ValidateMode(mode);
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A UI preference path is required.", "path");

        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(directory))
            throw new ArgumentException(
                "The UI preference path must include a directory.", "path");

        Directory.CreateDirectory(directory);
        string temporary = fullPath + ".new-" + Guid.NewGuid().ToString("N");
        try
        {
            using (FileStream stream = new FileStream(temporary,
                FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (StreamWriter writer = new StreamWriter(stream,
                new UTF8Encoding(false)))
                writer.Write(Serialize(mode));

            if (File.Exists(fullPath))
            {
                try
                {
                    File.Replace(temporary, fullPath, null, true);
                }
                catch (PlatformNotSupportedException)
                {
                    File.Copy(temporary, fullPath, true);
                    File.Delete(temporary);
                }
            }
            else
            {
                File.Move(temporary, fullPath);
            }
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    internal static UiThemeMode Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return UiThemeMode.Dark;

        bool themeFound = false;
        UiThemeMode result = UiThemeMode.Dark;
        string normalized = content.Replace("\r\n", "\n").Replace('\r', '\n');
        foreach (string rawLine in normalized.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            int separator = line.IndexOf('=');
            if (separator <= 0 || separator == line.Length - 1 || themeFound)
                return UiThemeMode.Dark;

            string key = line.Substring(0, separator).Trim();
            string value = line.Substring(separator + 1).Trim();
            if (!key.Equals("Theme", StringComparison.OrdinalIgnoreCase))
                return UiThemeMode.Dark;

            if (value.Equals("Dark", StringComparison.OrdinalIgnoreCase))
                result = UiThemeMode.Dark;
            else if (value.Equals("Light", StringComparison.OrdinalIgnoreCase))
                result = UiThemeMode.Light;
            else
                return UiThemeMode.Dark;
            themeFound = true;
        }
        return themeFound ? result : UiThemeMode.Dark;
    }

    internal static string Serialize(UiThemeMode mode)
    {
        ValidateMode(mode);
        return "Theme=" + (mode == UiThemeMode.Light ? "Light" : "Dark") +
            Environment.NewLine;
    }

    private static void ValidateMode(UiThemeMode mode)
    {
        if (mode != UiThemeMode.Dark && mode != UiThemeMode.Light)
            throw new ArgumentOutOfRangeException("mode");
    }
}

internal sealed class UiThemePalette
{
    public readonly bool IsDark;
    public readonly bool IsHighContrast;
    public readonly Color Canvas;
    public readonly Color Surface;
    public readonly Color Input;
    public readonly Color Raised;
    public readonly Color RaisedHot;
    public readonly Color Pressed;
    public readonly Color Border;
    public readonly Color Accent;
    public readonly Color AccentDark;
    public readonly Color AccentLight;
    public readonly Color AccentText;
    public readonly Color Ink;
    public readonly Color MutedInk;
    public readonly Color DisabledSurface;
    public readonly Color DisabledInk;
    public readonly Color Warning;
    public readonly Color Selection;
    public readonly Color SelectionText;

    public UiThemePalette(bool isDark, bool isHighContrast, Color canvas,
        Color surface, Color input, Color raised, Color raisedHot,
        Color pressed, Color border, Color accent, Color accentDark,
        Color accentLight, Color accentText, Color ink, Color mutedInk,
        Color disabledSurface, Color disabledInk, Color warning,
        Color selection, Color selectionText)
    {
        IsDark = isDark;
        IsHighContrast = isHighContrast;
        Canvas = canvas;
        Surface = surface;
        Input = input;
        Raised = raised;
        RaisedHot = raisedHot;
        Pressed = pressed;
        Border = border;
        Accent = accent;
        AccentDark = accentDark;
        AccentLight = accentLight;
        AccentText = accentText;
        Ink = ink;
        MutedInk = mutedInk;
        DisabledSurface = disabledSurface;
        DisabledInk = disabledInk;
        Warning = warning;
        Selection = selection;
        SelectionText = selectionText;
    }
}

internal static class SetupPalette
{
    private static readonly UiThemePalette DarkPalette =
        new UiThemePalette(true, false,
            Color.FromArgb(13, 20, 32), Color.FromArgb(21, 31, 46),
            Color.FromArgb(15, 24, 37), Color.FromArgb(34, 49, 73),
            Color.FromArgb(45, 66, 96), Color.FromArgb(20, 31, 47),
            Color.FromArgb(86, 111, 146), Color.FromArgb(88, 180, 255),
            Color.FromArgb(21, 91, 155), Color.FromArgb(150, 215, 255),
            Color.White, Color.FromArgb(244, 247, 251),
            Color.FromArgb(183, 195, 211), Color.FromArgb(40, 49, 62),
            Color.FromArgb(143, 154, 168), Color.FromArgb(255, 200, 87),
            Color.FromArgb(36, 87, 132), Color.White);

    private static readonly UiThemePalette LightPalette =
        new UiThemePalette(false, false,
            Color.FromArgb(239, 243, 249), Color.FromArgb(252, 253, 255),
            Color.White, Color.FromArgb(224, 231, 241),
            Color.FromArgb(250, 252, 255), Color.FromArgb(208, 218, 231),
            Color.FromArgb(130, 147, 170), Color.FromArgb(32, 104, 196),
            Color.FromArgb(19, 67, 133), Color.FromArgb(102, 191, 255),
            Color.White, Color.FromArgb(25, 36, 52),
            Color.FromArgb(78, 91, 110), Color.FromArgb(228, 231, 236),
            Color.FromArgb(119, 125, 135), Color.FromArgb(155, 80, 0),
            Color.FromArgb(218, 235, 255), Color.FromArgb(0, 72, 132));

    private static UiThemeMode requestedTheme = UiThemeMode.Dark;

    public static UiThemeMode RequestedTheme
    {
        get { return requestedTheme; }
    }

    public static UiThemePalette Current
    {
        get
        {
            if (SystemInformation.HighContrast)
                return CreateHighContrastPalette();
            return requestedTheme == UiThemeMode.Light ? LightPalette : DarkPalette;
        }
    }

    public static Color Canvas { get { return Current.Canvas; } }
    public static Color Surface { get { return Current.Surface; } }
    public static Color Input { get { return Current.Input; } }
    public static Color Raised { get { return Current.Raised; } }
    public static Color RaisedHot { get { return Current.RaisedHot; } }
    public static Color Pressed { get { return Current.Pressed; } }
    public static Color Border { get { return Current.Border; } }
    public static Color Accent { get { return Current.Accent; } }
    public static Color AccentDark { get { return Current.AccentDark; } }
    public static Color AccentLight { get { return Current.AccentLight; } }
    public static Color AccentText { get { return Current.AccentText; } }
    public static Color Ink { get { return Current.Ink; } }
    public static Color MutedInk { get { return Current.MutedInk; } }
    public static Color DisabledSurface { get { return Current.DisabledSurface; } }
    public static Color DisabledInk { get { return Current.DisabledInk; } }
    public static Color Warning { get { return Current.Warning; } }
    public static Color Selection { get { return Current.Selection; } }
    public static Color SelectionText { get { return Current.SelectionText; } }

    public static void SetTheme(UiThemeMode mode)
    {
        if (mode != UiThemeMode.Dark && mode != UiThemeMode.Light)
            throw new ArgumentOutOfRangeException("mode");
        requestedTheme = mode;
    }

    private static UiThemePalette CreateHighContrastPalette()
    {
        return new UiThemePalette(false, true,
            SystemColors.Control, SystemColors.Control, SystemColors.Window,
            SystemColors.Control, SystemColors.ControlLight,
            SystemColors.ControlDark, SystemColors.WindowFrame,
            SystemColors.Highlight, SystemColors.Highlight,
            SystemColors.HotTrack, SystemColors.HighlightText,
            SystemColors.ControlText, SystemColors.GrayText,
            SystemColors.Control, SystemColors.GrayText,
            SystemColors.ControlText, SystemColors.Highlight,
            SystemColors.HighlightText);
    }
}

// Gives each setup theme an easy-to-follow pointer cue: a green halo in Dark
// mode and a pale blue-and-white cloud in Light mode. It retains usable system
// cursor pixels and has stock semantic fallbacks for hosts that expose a blank
// cursor mask. It does not poll, install a mouse hook, or create a window, and
// Windows releases the cached cursor resources when Setup exits. High Contrast,
// oversized pointers, and custom cursor schemes remain unchanged.
internal static class SetupCursorHalo
{
    // The cue is painted around the source cursor rather than replacing it.
    // Enough transparent padding is reserved for the cue even when a custom
    // cursor places its hotspot at an outer edge.
    private const int CursorPadding = 26;
    private const int CueOffset = 10;

    private enum CursorKind
    {
        Arrow,
        Text,
        Hand,
        Unknown
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfo
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool IsIcon;
        public int HotspotX;
        public int HotspotY;
        public IntPtr MaskBitmap;
        public IntPtr ColorBitmap;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeBitmap
    {
        public int Type;
        public int Width;
        public int Height;
        public int WidthBytes;
        public ushort Planes;
        public ushort BitsPixel;
        public IntPtr Bits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ColorsUsed;
        public uint ColorsImportant;
    }

    private static readonly Dictionary<IntPtr, Cursor> DarkHaloCursors =
        new Dictionary<IntPtr, Cursor>();
    private static readonly Dictionary<IntPtr, Cursor> LightCloudCursors =
        new Dictionary<IntPtr, Cursor>();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetIconInfo(IntPtr iconHandle,
        out IconInfo iconInfo);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateIconIndirect(ref IconInfo iconInfo);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CopyIcon(IntPtr iconHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr iconHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyCursor(IntPtr cursorHandle);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(IntPtr objectHandle);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int GetObject(IntPtr objectHandle, int bufferSize,
        out NativeBitmap bitmap);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int GetDIBits(IntPtr deviceContext,
        IntPtr bitmapHandle, uint startScan, uint scanLines,
        [Out] byte[] bits, ref BitmapInfoHeader bitmapInfo, uint usage);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int GetBitmapBits(IntPtr bitmapHandle,
        int byteCount, [Out] byte[] bits);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetDC(IntPtr windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int ReleaseDC(IntPtr windowHandle,
        IntPtr deviceContext);

    public static Cursor ForControl(Control control, bool isDark, bool enabled)
    {
        CursorKind kind;
        Cursor normal = BaseCursor(control, out kind);
        if (!enabled || !CanTheme(normal, kind))
            return normal;

        Dictionary<IntPtr, Cursor> cache = isDark
            ? DarkHaloCursors
            : LightCloudCursors;
        Cursor themedCursor;
        if (cache.TryGetValue(normal.Handle, out themedCursor))
            return themedCursor;

        themedCursor = Create(normal, isDark);
        cache[normal.Handle] = themedCursor;
        return themedCursor;
    }

    private static Cursor BaseCursor(Control control, out CursorKind kind)
    {
        if (control is TextBoxBase)
        {
            kind = CursorKind.Text;
            return Cursors.IBeam;
        }
        if (control is LinkLabel)
        {
            kind = CursorKind.Hand;
            return Cursors.Hand;
        }
        kind = CursorKind.Arrow;
        return Cursors.Default;
    }

    private static bool CanTheme(Cursor cursor, CursorKind kind)
    {
        if (cursor == null || kind == CursorKind.Unknown ||
            !IsSupportedCursorSize(cursor.Size))
            return false;

        // The themed cue is deliberately limited to the ordinary Windows
        // pointers. A configured pointer file can carry accessibility sizing,
        // colors, animation, or a carefully chosen custom shape. Keep it
        // completely untouched rather than trying to reinterpret it.
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                @"Control Panel\Cursors", false))
            {
                if (key == null)
                    return false;
                string valueName = CursorRegistryValueName(kind);
                string configured = Convert.ToString(key.GetValue(valueName,
                    null, RegistryValueOptions.DoNotExpandEnvironmentNames));
                string windowsDirectory = Environment.GetEnvironmentVariable(
                    "WINDIR");
                return IsStockCursorSetting(valueName, configured,
                    windowsDirectory);
            }
        }
        catch
        {
            // If the current cursor choice cannot be identified confidently,
            // preserving it is safer than applying an app-specific resource.
            return false;
        }
    }

    internal static bool IsSupportedCursorSize(Size size)
    {
        return size.Width > 0 && size.Height > 0 &&
            size.Width <= 32 && size.Height <= 32;
    }

    internal static bool IsStockCursorSetting(string valueName,
        string configuredValue, string windowsDirectory)
    {
        if (string.IsNullOrWhiteSpace(valueName) ||
            string.IsNullOrWhiteSpace(windowsDirectory))
            return false;
        if (string.IsNullOrWhiteSpace(configuredValue))
            return true;

        string expectedFile;
        if (valueName.Equals("Arrow", StringComparison.OrdinalIgnoreCase))
            expectedFile = "aero_arrow.cur";
        else if (valueName.Equals("IBeam",
                StringComparison.OrdinalIgnoreCase))
            expectedFile = "aero_ibeam.cur";
        else if (valueName.Equals("Hand",
                StringComparison.OrdinalIgnoreCase))
            expectedFile = "aero_link.cur";
        else
            return false;

        try
        {
            string expanded = Environment.ExpandEnvironmentVariables(
                configuredValue.Trim());
            string actualPath = Path.GetFullPath(expanded);
            string expectedPath = Path.GetFullPath(Path.Combine(
                windowsDirectory, "Cursors", expectedFile));
            return actualPath.Equals(expectedPath,
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string CursorRegistryValueName(CursorKind kind)
    {
        if (kind == CursorKind.Text)
            return "IBeam";
        if (kind == CursorKind.Hand)
            return "Hand";
        return "Arrow";
    }

    private static Cursor Create(Cursor source, bool isDark)
    {
        IconInfo sourceInfo;
        if (!GetIconInfo(source.Handle, out sourceInfo))
            return source;

        try
        {
            Point translatedHotspot = TranslateHotspot(
                sourceInfo.HotspotX, sourceInfo.HotspotY);
            using (Bitmap bitmap = ComposeCursorBitmap(source, isDark,
                sourceInfo.HotspotX, sourceInfo.HotspotY))
            {
                return CreateCursor(bitmap, translatedHotspot.X,
                    translatedHotspot.Y, source);
            }
        }
        catch
        {
            return source;
        }
        finally
        {
            DeleteObject(sourceInfo.MaskBitmap);
            DeleteObject(sourceInfo.ColorBitmap);
        }
    }

    internal static Point TranslateHotspot(int sourceHotspotX,
        int sourceHotspotY)
    {
        return new Point(sourceHotspotX + CursorPadding,
            sourceHotspotY + CursorPadding);
    }

    internal static Bitmap ComposeCursorBitmap(Cursor source, bool isDark,
        int sourceHotspotX, int sourceHotspotY)
    {
        if (source == null)
            throw new ArgumentNullException("source");

        using (Bitmap sourceBitmap = CaptureCursorBitmap(source))
        {
            Bitmap bitmap = new Bitmap(
                sourceBitmap.Width + CursorPadding * 2,
                sourceBitmap.Height + CursorPadding * 2);
            try
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.Transparent);
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;

                    Point translatedHotspot = TranslateHotspot(
                        sourceHotspotX, sourceHotspotY);
                    int centerX = translatedHotspot.X + CueOffset;
                    int centerY = translatedHotspot.Y + CueOffset;
                    if (isDark)
                        DrawDarkHalo(graphics, centerX, centerY);
                    else
                        DrawLightCloud(graphics, centerX, centerY);

                    // Draw the user's real system cursor last and at its
                    // native size. This preserves its shape, scale, colors,
                    // and click point while the setup-only cue stays behind.
                    graphics.DrawImageUnscaled(sourceBitmap, CursorPadding,
                        CursorPadding);
                }
                return bitmap;
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
        }
    }

    internal static Bitmap CaptureCursorBitmap(Cursor source)
    {
        if (source == null)
            throw new ArgumentNullException("source");

        IntPtr copiedCursor = CopyIcon(source.Handle);
        IntPtr readableCursor = copiedCursor == IntPtr.Zero
            ? source.Handle : copiedCursor;
        IconInfo sourceInfo;
        if (!GetIconInfo(readableCursor, out sourceInfo))
        {
            if (copiedCursor != IntPtr.Zero)
                DestroyCursor(copiedCursor);
            throw new InvalidOperationException(
                "Windows could not read the source cursor.");
        }
        try
        {
            Bitmap captured;
            if (sourceInfo.ColorBitmap == IntPtr.Zero)
                captured = CaptureMonochromeCursor(sourceInfo.MaskBitmap);
            else
                captured = CaptureColorCursor(sourceInfo.ColorBitmap,
                    sourceInfo.MaskBitmap);

            if (IsMeaningfulCursorBitmap(captured))
                return captured;
            captured.Dispose();

            CursorKind kind = StockCursorKind(source);
            if (kind != CursorKind.Unknown &&
                IsSupportedCursorSize(source.Size))
                return CreateSemanticFallback(source.Size, kind,
                    sourceInfo.HotspotX, sourceInfo.HotspotY);

            throw new InvalidOperationException(
                "Windows returned an unusable cursor bitmap.");
        }
        finally
        {
            if (sourceInfo.MaskBitmap != IntPtr.Zero)
                DeleteObject(sourceInfo.MaskBitmap);
            if (sourceInfo.ColorBitmap != IntPtr.Zero)
                DeleteObject(sourceInfo.ColorBitmap);
            if (copiedCursor != IntPtr.Zero)
                DestroyCursor(copiedCursor);
        }
    }

    private static CursorKind StockCursorKind(Cursor source)
    {
        if (source.Handle == Cursors.Default.Handle)
            return CursorKind.Arrow;
        if (source.Handle == Cursors.IBeam.Handle)
            return CursorKind.Text;
        if (source.Handle == Cursors.Hand.Handle)
            return CursorKind.Hand;
        return CursorKind.Unknown;
    }

    internal static bool IsMeaningfulCursorBitmap(Bitmap bitmap)
    {
        if (bitmap == null || bitmap.Width <= 0 || bitmap.Height <= 0)
            return false;

        int visible = 0;
        int transparent = 0;
        int minimumX = bitmap.Width;
        int minimumY = bitmap.Height;
        int maximumX = -1;
        int maximumY = -1;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).A < 16)
                {
                    transparent++;
                    continue;
                }
                visible++;
                minimumX = Math.Min(minimumX, x);
                minimumY = Math.Min(minimumY, y);
                maximumX = Math.Max(maximumX, x);
                maximumY = Math.Max(maximumY, y);
            }
        }

        int total = checked(bitmap.Width * bitmap.Height);
        if (visible < 4 || transparent < Math.Max(8, total / 10) ||
            maximumX <= minimumX || maximumY <= minimumY)
            return false;

        // A cursor has an irregular silhouette. This also rejects the common
        // failed-capture result: an opaque white rectangle surrounded by a
        // small transparent margin.
        int boundsArea = checked((maximumX - minimumX + 1) *
            (maximumY - minimumY + 1));
        return boundsArea - visible >= Math.Max(2, boundsArea / 50);
    }

    private static Bitmap CreateSemanticFallback(Size size, CursorKind kind,
        int hotspotX, int hotspotY)
    {
        Bitmap bitmap = new Bitmap(size.Width, size.Height);
        try
        {
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                float scale = Math.Min(size.Width, size.Height) / 32.0f;
                if (kind == CursorKind.Text)
                {
                    // Some hosts return a valid stock I-beam handle but no
                    // usable hotspot metadata along with its blank mask. Keep
                    // the resource hotspot unchanged while placing the visual
                    // glyph far enough inside the bitmap to avoid clipping.
                    float textX = Clamp(hotspotX, 6 * scale,
                        size.Width - 7 * scale);
                    float textY = Clamp(hotspotY, 14 * scale,
                        size.Height - 15 * scale);
                    DrawTextFallback(graphics, textX, textY, scale);
                }
                else if (kind == CursorKind.Hand)
                {
                    float handX = Clamp(hotspotX, 2 * scale,
                        size.Width - 22 * scale);
                    float handY = Clamp(hotspotY, 0,
                        size.Height - 29 * scale);
                    DrawHandFallback(graphics, handX, handY, scale);
                }
                else
                    DrawArrowFallback(graphics, hotspotX, hotspotY, scale);
            }

            if (!IsMeaningfulCursorBitmap(bitmap))
                throw new InvalidOperationException(
                    "The semantic cursor fallback was not usable.");
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    private static void DrawArrowFallback(Graphics graphics, int hotspotX,
        int hotspotY, float scale)
    {
        PointF[] points = new PointF[]
        {
            RelativePoint(hotspotX, hotspotY, 0, 0, scale),
            RelativePoint(hotspotX, hotspotY, 2, 23, scale),
            RelativePoint(hotspotX, hotspotY, 7, 18, scale),
            RelativePoint(hotspotX, hotspotY, 12, 29, scale),
            RelativePoint(hotspotX, hotspotY, 17, 27, scale),
            RelativePoint(hotspotX, hotspotY, 12, 16, scale),
            RelativePoint(hotspotX, hotspotY, 20, 16, scale)
        };
        using (SolidBrush fill = new SolidBrush(Color.White))
        using (Pen outline = new Pen(Color.Black,
            Math.Max(1.0f, 1.35f * scale)))
        {
            outline.LineJoin = LineJoin.Round;
            graphics.FillPolygon(fill, points);
            graphics.DrawPolygon(outline, points);
        }
    }

    private static void DrawTextFallback(Graphics graphics, float hotspotX,
        float hotspotY, float scale)
    {
        float left = hotspotX - 5 * scale;
        float right = hotspotX + 5 * scale;
        float top = hotspotY - 13 * scale;
        float bottom = hotspotY + 13 * scale;
        using (Pen light = new Pen(Color.White,
            Math.Max(3.0f, 4.5f * scale)))
        using (Pen dark = new Pen(Color.Black,
            Math.Max(1.0f, 1.5f * scale)))
        {
            light.StartCap = light.EndCap = LineCap.Round;
            dark.StartCap = dark.EndCap = LineCap.Round;
            DrawIBeamLines(graphics, light, left, right, top, bottom,
                hotspotX);
            DrawIBeamLines(graphics, dark, left, right, top, bottom,
                hotspotX);
        }
    }

    private static void DrawIBeamLines(Graphics graphics, Pen pen,
        float left, float right, float top, float bottom, float centerX)
    {
        graphics.DrawLine(pen, left, top, right, top);
        graphics.DrawLine(pen, centerX, top, centerX, bottom);
        graphics.DrawLine(pen, left, bottom, right, bottom);
    }

    private static void DrawHandFallback(Graphics graphics, float hotspotX,
        float hotspotY, float scale)
    {
        PointF[] points = new PointF[]
        {
            RelativePoint(hotspotX, hotspotY, 0, 0, scale),
            RelativePoint(hotspotX, hotspotY, 4, 0, scale),
            RelativePoint(hotspotX, hotspotY, 4, 11, scale),
            RelativePoint(hotspotX, hotspotY, 6, 5, scale),
            RelativePoint(hotspotX, hotspotY, 9, 5, scale),
            RelativePoint(hotspotX, hotspotY, 10, 12, scale),
            RelativePoint(hotspotX, hotspotY, 11, 7, scale),
            RelativePoint(hotspotX, hotspotY, 14, 7, scale),
            RelativePoint(hotspotX, hotspotY, 15, 13, scale),
            RelativePoint(hotspotX, hotspotY, 16, 9, scale),
            RelativePoint(hotspotX, hotspotY, 19, 9, scale),
            RelativePoint(hotspotX, hotspotY, 21, 20, scale),
            RelativePoint(hotspotX, hotspotY, 17, 28, scale),
            RelativePoint(hotspotX, hotspotY, 7, 28, scale),
            RelativePoint(hotspotX, hotspotY, 3, 21, scale),
            RelativePoint(hotspotX, hotspotY, -2, 16, scale),
            RelativePoint(hotspotX, hotspotY, 0, 13, scale),
            RelativePoint(hotspotX, hotspotY, 4, 17, scale)
        };
        using (SolidBrush fill = new SolidBrush(Color.White))
        using (Pen outline = new Pen(Color.Black,
            Math.Max(1.0f, 1.25f * scale)))
        {
            outline.LineJoin = LineJoin.Round;
            graphics.FillPolygon(fill, points);
            graphics.DrawPolygon(outline, points);
        }
    }

    private static PointF RelativePoint(float originX, float originY,
        float offsetX, float offsetY, float scale)
    {
        return new PointF(originX + offsetX * scale,
            originY + offsetY * scale);
    }

    private static float Clamp(float value, float minimum, float maximum)
    {
        return Math.Max(minimum, Math.Min(maximum, value));
    }

    private static Bitmap CaptureColorCursor(IntPtr colorHandle,
        IntPtr maskHandle)
    {
        int width;
        int height;
        byte[] colorBits = ReadColorBitmapBits(colorHandle, out width,
            out height);
        int maskWidth = 0;
        int maskHeight = 0;
        byte[] maskBits = maskHandle == IntPtr.Zero
            ? null
            : ReadMaskBits(maskHandle, out maskWidth, out maskHeight);
        if (maskBits != null && (maskWidth != width || maskHeight < height))
            throw new InvalidOperationException(
                "The Windows cursor mask dimensions are invalid.");

        bool hasAlpha = false;
        for (int offset = 3; offset < colorBits.Length; offset += 4)
        {
            if (colorBits[offset] != 0)
            {
                hasAlpha = true;
                break;
            }
        }

        Bitmap captured = new Bitmap(width, height);
        try
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int offset = (y * width + x) * 4;
                    int alpha = hasAlpha
                        ? colorBits[offset + 3]
                        : MaskIsTransparent(maskBits, maskWidth, x, y)
                            ? 0 : 255;
                    if (alpha == 0)
                    {
                        captured.SetPixel(x, y, Color.Transparent);
                        continue;
                    }

                    int blue = colorBits[offset];
                    int green = colorBits[offset + 1];
                    int red = colorBits[offset + 2];
                    if (hasAlpha && alpha < 255)
                    {
                        red = Math.Min(255,
                            (red * 255 + alpha / 2) / alpha);
                        green = Math.Min(255,
                            (green * 255 + alpha / 2) / alpha);
                        blue = Math.Min(255,
                            (blue * 255 + alpha / 2) / alpha);
                    }
                    captured.SetPixel(x, y,
                        Color.FromArgb(alpha, red, green, blue));
                }
            }
            return captured;
        }
        catch
        {
            captured.Dispose();
            throw;
        }
    }

    private static Bitmap CaptureMonochromeCursor(IntPtr maskHandle)
    {
        int width;
        int combinedHeight;
        byte[] maskBits = ReadMaskBits(maskHandle, out width,
            out combinedHeight);
        if (combinedHeight < 2 || (combinedHeight % 2) != 0)
            throw new InvalidOperationException(
                "The monochrome Windows cursor mask is invalid.");
        int height = combinedHeight / 2;
        Bitmap captured = new Bitmap(width, height);
        try
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool andSet = MaskIsTransparent(maskBits, width, x, y);
                    bool xorSet = MaskIsTransparent(maskBits, width, x,
                        y + height);
                    if (andSet && !xorSet)
                        captured.SetPixel(x, y, Color.Transparent);
                    else
                        captured.SetPixel(x, y,
                            xorSet ? Color.White : Color.Black);
                }
            }
            return captured;
        }
        catch
        {
            captured.Dispose();
            throw;
        }
    }

    private static bool MaskIsTransparent(byte[] bits, int width, int x,
        int y)
    {
        if (bits == null)
            return false;
        int offset = y * width + x;
        return bits[offset] >= 128;
    }

    private static byte[] ReadBitmapBits(IntPtr bitmapHandle, out int width,
        out int height)
    {
        if (bitmapHandle == IntPtr.Zero)
            throw new ArgumentException("A bitmap handle is required.");

        NativeBitmap nativeBitmap;
        if (GetObject(bitmapHandle, Marshal.SizeOf(typeof(NativeBitmap)),
                out nativeBitmap) == 0 || nativeBitmap.Width <= 0 ||
            nativeBitmap.Height == 0)
            throw new InvalidOperationException(
                "Windows could not read a cursor bitmap.");

        width = nativeBitmap.Width;
        height = Math.Abs(nativeBitmap.Height);
        BitmapInfoHeader bitmapInfo = new BitmapInfoHeader();
        bitmapInfo.Size = (uint)Marshal.SizeOf(typeof(BitmapInfoHeader));
        bitmapInfo.Width = width;
        bitmapInfo.Height = -height;
        bitmapInfo.Planes = 1;
        bitmapInfo.BitCount = 32;
        bitmapInfo.Compression = 0;
        byte[] bits = new byte[checked(width * height * 4)];
        IntPtr deviceContext = GetDC(IntPtr.Zero);
        if (deviceContext == IntPtr.Zero)
            throw new InvalidOperationException(
                "Windows could not provide a cursor bitmap context.");
        try
        {
            if (GetDIBits(deviceContext, bitmapHandle, 0, (uint)height,
                    bits, ref bitmapInfo, 0) == 0)
                throw new InvalidOperationException(
                    "Windows could not copy a cursor bitmap.");
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, deviceContext);
        }
        return bits;
    }

    private static byte[] ReadColorBitmapBits(IntPtr bitmapHandle,
        out int width, out int height)
    {
        NativeBitmap nativeBitmap;
        if (GetObject(bitmapHandle, Marshal.SizeOf(typeof(NativeBitmap)),
                out nativeBitmap) == 0 || nativeBitmap.Width <= 0 ||
            nativeBitmap.Height == 0)
            throw new InvalidOperationException(
                "Windows could not read a cursor color bitmap.");

        width = nativeBitmap.Width;
        height = Math.Abs(nativeBitmap.Height);
        if (nativeBitmap.BitsPixel != 32 || nativeBitmap.WidthBytes == 0)
            return ReadBitmapBits(bitmapHandle, out width, out height);

        int sourceStride = Math.Abs(nativeBitmap.WidthBytes);
        if (sourceStride < checked(width * 4))
            throw new InvalidOperationException(
                "The Windows cursor color bitmap stride is invalid.");
        byte[] source = new byte[checked(sourceStride * height)];
        if (GetBitmapBits(bitmapHandle, source.Length, source) == 0)
            return ReadBitmapBits(bitmapHandle, out width, out height);

        byte[] topDown = new byte[checked(width * height * 4)];
        for (int y = 0; y < height; y++)
        {
            int sourceY = nativeBitmap.Height > 0 ? height - 1 - y : y;
            Buffer.BlockCopy(source, sourceY * sourceStride, topDown,
                y * width * 4, width * 4);
        }
        return topDown;
    }

    private static byte[] ReadMaskBits(IntPtr bitmapHandle, out int width,
        out int height)
    {
        NativeBitmap nativeBitmap;
        if (GetObject(bitmapHandle, Marshal.SizeOf(typeof(NativeBitmap)),
                out nativeBitmap) == 0 || nativeBitmap.Width <= 0 ||
            nativeBitmap.Height == 0)
            throw new InvalidOperationException(
                "Windows could not read a cursor mask bitmap.");

        width = nativeBitmap.Width;
        height = Math.Abs(nativeBitmap.Height);
        int sourceStride = Math.Abs(nativeBitmap.WidthBytes);
        if (nativeBitmap.BitsPixel != 1 || sourceStride == 0)
        {
            byte[] expanded = ReadBitmapBits(bitmapHandle, out width,
                out height);
            byte[] collapsed = new byte[checked(width * height)];
            for (int pixel = 0; pixel < collapsed.Length; pixel++)
                collapsed[pixel] = expanded[pixel * 4];
            return collapsed;
        }

        byte[] source = new byte[checked(sourceStride * height)];
        if (GetBitmapBits(bitmapHandle, source.Length, source) == 0)
            throw new InvalidOperationException(
                "Windows could not copy a cursor mask bitmap.");
        byte[] topDown = new byte[checked(width * height)];
        for (int y = 0; y < height; y++)
        {
            int sourceY = nativeBitmap.Height > 0 ? height - 1 - y : y;
            int row = sourceY * sourceStride;
            for (int x = 0; x < width; x++)
            {
                int mask = 0x80 >> (x & 7);
                topDown[y * width + x] =
                    (source[row + x / 8] & mask) != 0
                        ? (byte)255 : (byte)0;
            }
        }
        return topDown;
    }

    private static void DrawDarkHalo(Graphics graphics, int centerX,
        int centerY)
    {
        Rectangle glow = new Rectangle(centerX - 14, centerY - 14, 28, 28);
        Rectangle core = new Rectangle(centerX - 10, centerY - 10, 20, 20);
        using (SolidBrush outer = new SolidBrush(
            Color.FromArgb(62, 62, 255, 126)))
        using (SolidBrush inner = new SolidBrush(
            Color.FromArgb(150, 48, 224, 103)))
        using (Pen rim = new Pen(Color.FromArgb(220, 145, 255, 177), 1.25f))
        {
            graphics.FillEllipse(outer, glow);
            graphics.FillEllipse(inner, core);
            graphics.DrawEllipse(rim, core);
        }
    }

    private static void DrawLightCloud(Graphics graphics, int centerX,
        int centerY)
    {
        // The overlapping puffs keep the cue recognizable as a cloud while
        // the low-opacity blue glow prevents it from hiding nearby text.
        using (SolidBrush glow = new SolidBrush(
            Color.FromArgb(68, 107, 184, 255)))
        using (SolidBrush cloud = new SolidBrush(
            Color.FromArgb(204, 250, 253, 255)))
        using (Pen rim = new Pen(Color.FromArgb(190, 91, 161, 224), 1.15f))
        {
            graphics.FillEllipse(glow,
                new Rectangle(centerX - 16, centerY - 12, 32, 25));
            graphics.FillEllipse(cloud,
                new Rectangle(centerX - 13, centerY - 4, 26, 13));
            graphics.FillEllipse(cloud,
                new Rectangle(centerX - 11, centerY - 8, 12, 12));
            graphics.FillEllipse(cloud,
                new Rectangle(centerX - 4, centerY - 12, 16, 16));
            graphics.FillEllipse(cloud,
                new Rectangle(centerX + 5, centerY - 6, 10, 10));
            graphics.DrawArc(rim,
                new Rectangle(centerX - 11, centerY - 8, 12, 12),
                165.0f, 205.0f);
            graphics.DrawArc(rim,
                new Rectangle(centerX - 4, centerY - 12, 16, 16),
                185.0f, 190.0f);
            graphics.DrawArc(rim,
                new Rectangle(centerX + 5, centerY - 6, 10, 10),
                205.0f, 155.0f);
            graphics.DrawArc(rim,
                new Rectangle(centerX - 13, centerY - 4, 26, 13),
                0.0f, 180.0f);
        }
    }

    private static Cursor CreateCursor(Bitmap bitmap, int hotspotX,
        int hotspotY, Cursor fallback)
    {
        IntPtr iconHandle = IntPtr.Zero;
        IconInfo cursorInfo = new IconInfo();
        try
        {
            iconHandle = bitmap.GetHicon();
            if (!GetIconInfo(iconHandle, out cursorInfo))
                return fallback;
            cursorInfo.IsIcon = false;
            cursorInfo.HotspotX = hotspotX;
            cursorInfo.HotspotY = hotspotY;
            IntPtr cursorHandle = CreateIconIndirect(ref cursorInfo);
            return cursorHandle == IntPtr.Zero
                ? fallback
                : new Cursor(cursorHandle);
        }
        finally
        {
            if (cursorInfo.MaskBitmap != IntPtr.Zero)
                DeleteObject(cursorInfo.MaskBitmap);
            if (cursorInfo.ColorBitmap != IntPtr.Zero)
                DeleteObject(cursorInfo.ColorBitmap);
            if (iconHandle != IntPtr.Zero)
                DestroyIcon(iconHandle);
        }
    }
}

internal static class SetupVisualTheme
{
    public static void Apply(Control root)
    {
        if (root == null)
            throw new ArgumentNullException("root");

        UiThemePalette palette = SetupPalette.Current;
        ApplyControl(root, palette);
        root.Invalidate(true);
    }

    public static void ApplyCursorCue(Control root)
    {
        if (root == null)
            throw new ArgumentNullException("root");

        UiThemePalette palette = SetupPalette.Current;
        ApplyCursorCueControl(root, palette);
    }

    private static void ApplyCursorCueControl(Control control,
        UiThemePalette palette)
    {
        control.Cursor = SetupCursorHalo.ForControl(control, palette.IsDark,
            !palette.IsHighContrast);
        foreach (Control child in control.Controls)
            ApplyCursorCueControl(child, palette);
    }

    private static void ApplyControl(Control control, UiThemePalette palette)
    {
        control.Cursor = SetupCursorHalo.ForControl(control, palette.IsDark,
            !palette.IsHighContrast);

        // These controls deliberately show the actual reminder payload. Their
        // configured colors must not be replaced by setup-window colors.
        if (IsPayloadPreview(control))
        {
            control.Invalidate();
            return;
        }

        SetupHeroPanel hero = control as SetupHeroPanel;
        GuidedTabControl guidedTabs = control as GuidedTabControl;
        DimensionalButton dimensionalButton = control as DimensionalButton;

        if (hero != null)
        {
            hero.BackColor = palette.Canvas;
            hero.ForeColor = palette.Ink;
            hero.Invalidate();
        }
        else if (guidedTabs != null)
        {
            guidedTabs.BackColor = palette.Canvas;
            guidedTabs.ForeColor = palette.Ink;
            guidedTabs.Invalidate();
        }
        else if (dimensionalButton != null)
        {
            dimensionalButton.BackColor = palette.Raised;
            dimensionalButton.ForeColor = palette.Ink;
            dimensionalButton.Invalidate();
        }
        else if (control is Form)
        {
            control.BackColor = palette.Canvas;
            control.ForeColor = palette.Ink;
        }
        else if (control is TabPage)
        {
            TabPage page = (TabPage)control;
            page.UseVisualStyleBackColor = false;
            page.BackColor = palette.Surface;
            page.ForeColor = palette.Ink;
        }
        else if (control is TextBoxBase)
        {
            control.BackColor = palette.Input;
            control.ForeColor = palette.Ink;
        }
        else if (control is UpDownBase)
        {
            control.BackColor = palette.Input;
            control.ForeColor = palette.Ink;
        }
        else if (control is ComboBox)
        {
            ComboBox comboBox = (ComboBox)control;
            comboBox.BackColor = palette.Input;
            comboBox.ForeColor = palette.Ink;
            comboBox.FlatStyle = palette.IsHighContrast
                ? FlatStyle.System
                : (palette.IsDark ? FlatStyle.Flat : FlatStyle.Standard);
        }
        else if (control is ListBox)
        {
            ListBox listBox = (ListBox)control;
            listBox.BackColor = palette.Input;
            listBox.ForeColor = palette.Ink;
        }
        else if (control is ListView)
        {
            ListView list = (ListView)control;
            list.BackColor = palette.Input;
            list.ForeColor = palette.Ink;
        }
        else if (control is TreeView)
        {
            TreeView tree = (TreeView)control;
            tree.BackColor = palette.Input;
            tree.ForeColor = palette.Ink;
            tree.LineColor = palette.Border;
        }
        else if (control is DataGridView)
        {
            ApplyDataGridView((DataGridView)control, palette);
        }
        else if (control is LinkLabel)
        {
            LinkLabel link = (LinkLabel)control;
            link.BackColor = Color.Transparent;
            link.ForeColor = palette.Ink;
            link.LinkColor = palette.Accent;
            link.ActiveLinkColor = palette.IsHighContrast
                ? SystemColors.HotTrack
                : (palette.IsDark ? palette.AccentLight : palette.AccentDark);
            link.VisitedLinkColor = palette.Accent;
        }
        else if (control is GroupBox)
        {
            control.BackColor = palette.Surface;
            control.ForeColor = IsMutedHint(control.ForeColor)
                ? palette.MutedInk : palette.Ink;
        }
        else if (control is Button)
        {
            ApplyButton((Button)control, palette);
        }
        else if (control is CheckBox)
        {
            CheckBox checkBox = (CheckBox)control;
            checkBox.UseVisualStyleBackColor = false;
            checkBox.BackColor = ParentSurface(checkBox, palette);
            checkBox.ForeColor = palette.Ink;
            checkBox.FlatStyle = palette.IsHighContrast
                ? FlatStyle.System : FlatStyle.Standard;
        }
        else if (control is RadioButton)
        {
            RadioButton radioButton = (RadioButton)control;
            radioButton.UseVisualStyleBackColor = false;
            radioButton.BackColor = ParentSurface(radioButton, palette);
            radioButton.ForeColor = palette.Ink;
            radioButton.FlatStyle = palette.IsHighContrast
                ? FlatStyle.System : FlatStyle.Standard;
        }
        else if (control is ToolStrip)
        {
            ApplyToolStrip((ToolStrip)control, palette);
        }
        else if (control is Label)
        {
            Label label = (Label)control;
            label.BackColor = Color.Transparent;
            if (IsWarningHint(label.ForeColor))
                label.ForeColor = palette.Warning;
            else if (IsMutedHint(label.ForeColor))
                label.ForeColor = palette.MutedInk;
            else
                label.ForeColor = palette.Ink;
        }
        else if (control is Panel || control is FlowLayoutPanel ||
                 control is TableLayoutPanel || control is UserControl)
        {
            control.BackColor = palette.Surface;
            control.ForeColor = palette.Ink;
        }
        else if (control is TabControl)
        {
            control.BackColor = palette.Canvas;
            control.ForeColor = palette.Ink;
        }
        else
        {
            control.ForeColor = palette.Ink;
        }

        if (control.ContextMenuStrip != null)
            ApplyToolStrip(control.ContextMenuStrip, palette);

        foreach (Control child in control.Controls)
            ApplyControl(child, palette);
    }

    private static void ApplyButton(Button button, UiThemePalette palette)
    {
        button.UseVisualStyleBackColor = false;
        button.ForeColor = palette.Ink;
        if (palette.IsHighContrast)
        {
            button.FlatStyle = FlatStyle.System;
            button.BackColor = SystemColors.Control;
            return;
        }

        button.FlatStyle = palette.IsDark ? FlatStyle.Flat : FlatStyle.Standard;
        button.BackColor = palette.Raised;
        button.FlatAppearance.BorderColor = palette.Border;
        button.FlatAppearance.MouseOverBackColor = palette.RaisedHot;
        button.FlatAppearance.MouseDownBackColor = palette.Pressed;
    }

    private static void ApplyDataGridView(DataGridView grid,
        UiThemePalette palette)
    {
        grid.BackgroundColor = palette.Surface;
        grid.GridColor = palette.Border;
        grid.ForeColor = palette.Ink;
        grid.DefaultCellStyle.BackColor = palette.Input;
        grid.DefaultCellStyle.ForeColor = palette.Ink;
        grid.DefaultCellStyle.SelectionBackColor = palette.Selection;
        grid.DefaultCellStyle.SelectionForeColor = palette.SelectionText;
        grid.ColumnHeadersDefaultCellStyle.BackColor = palette.Raised;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = palette.Ink;
        grid.RowHeadersDefaultCellStyle.BackColor = palette.Raised;
        grid.RowHeadersDefaultCellStyle.ForeColor = palette.Ink;
        grid.EnableHeadersVisualStyles = palette.IsHighContrast;
    }

    private static void ApplyToolStrip(ToolStrip strip,
        UiThemePalette palette)
    {
        strip.BackColor = palette.Raised;
        strip.ForeColor = palette.Ink;
        strip.Renderer = palette.IsHighContrast
            ? (ToolStripRenderer)new ToolStripSystemRenderer()
            : new ToolStripProfessionalRenderer(
                new SetupProfessionalColorTable(palette));

        foreach (ToolStripItem item in strip.Items)
            ApplyToolStripItem(item, palette);
    }

    private static void ApplyToolStripItem(ToolStripItem item,
        UiThemePalette palette)
    {
        item.BackColor = palette.Raised;
        item.ForeColor = palette.Ink;
        ToolStripDropDownItem dropDown = item as ToolStripDropDownItem;
        if (dropDown == null || dropDown.DropDownItems.Count == 0)
            return;
        ApplyToolStrip(dropDown.DropDown, palette);
    }

    private static Color ParentSurface(Control control,
        UiThemePalette palette)
    {
        if (control.Parent == null)
            return palette.Surface;
        Color parentColor = control.Parent.BackColor;
        return parentColor == Color.Transparent ? palette.Surface : parentColor;
    }

    private static bool IsPayloadPreview(Control control)
    {
        string typeName = control.GetType().Name;
        return string.Equals(typeName, "BannerSampleControl",
                   StringComparison.Ordinal) ||
               string.Equals(typeName, "BannerTestForm",
                   StringComparison.Ordinal);
    }

    private static bool IsMutedHint(Color color)
    {
        int value = color.ToArgb();
        return value == Color.FromArgb(78, 91, 110).ToArgb() ||
            value == Color.FromArgb(183, 195, 211).ToArgb() ||
            value == SystemColors.GrayText.ToArgb() ||
            value == Color.FromArgb(55, 55, 55).ToArgb() ||
            value == Color.FromArgb(65, 65, 65).ToArgb() ||
            value == Color.FromArgb(70, 70, 70).ToArgb() ||
            value == Color.FromArgb(75, 75, 75).ToArgb() ||
            value == Color.FromArgb(80, 80, 80).ToArgb();
    }

    private static bool IsWarningHint(Color color)
    {
        int value = color.ToArgb();
        return value == Color.FromArgb(155, 80, 0).ToArgb() ||
            value == Color.FromArgb(255, 200, 87).ToArgb();
    }

    private sealed class SetupProfessionalColorTable : ProfessionalColorTable
    {
        private readonly UiThemePalette palette;

        public SetupProfessionalColorTable(UiThemePalette selectedPalette)
        {
            palette = selectedPalette;
            UseSystemColors = false;
        }

        public override Color ToolStripDropDownBackground
        {
            get { return palette.Raised; }
        }

        public override Color ImageMarginGradientBegin
        {
            get { return palette.Surface; }
        }

        public override Color ImageMarginGradientMiddle
        {
            get { return palette.Surface; }
        }

        public override Color ImageMarginGradientEnd
        {
            get { return palette.Surface; }
        }

        public override Color MenuItemSelected
        {
            get { return palette.Selection; }
        }

        public override Color MenuItemBorder
        {
            get { return palette.Border; }
        }

        public override Color MenuItemSelectedGradientBegin
        {
            get { return palette.Selection; }
        }

        public override Color MenuItemSelectedGradientEnd
        {
            get { return palette.Selection; }
        }

        public override Color MenuItemPressedGradientBegin
        {
            get { return palette.Pressed; }
        }

        public override Color MenuItemPressedGradientEnd
        {
            get { return palette.Pressed; }
        }

        public override Color SeparatorDark
        {
            get { return palette.Border; }
        }

        public override Color SeparatorLight
        {
            get { return palette.Surface; }
        }

        public override Color ToolStripBorder
        {
            get { return palette.Border; }
        }
    }
}

internal sealed class SetupHeroPanel : Control
{
    public SetupHeroPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint, true);
        AccessibleName = "RDP Session Reminder";
        AccessibleDescription =
            "Lightweight guided setup for Remote Desktop connections.";
        BackColor = SetupPalette.Canvas;
        ForeColor = SetupPalette.Ink;
        TabStop = false;
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        UiThemePalette palette = SetupPalette.Current;
        Graphics graphics = eventArgs.Graphics;
        graphics.SmoothingMode = palette.IsHighContrast
            ? SmoothingMode.None : SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = palette.IsHighContrast
            ? System.Drawing.Text.TextRenderingHint.SystemDefault
            : System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        Rectangle card = new Rectangle(3, 2,
            Math.Max(1, ClientSize.Width - 8),
            Math.Max(1, ClientSize.Height - 8));
        if (!palette.IsHighContrast)
        {
            Rectangle shadow = card;
            shadow.Offset(3, 4);
            using (GraphicsPath shadowPath = RoundedRectangle(shadow, 13))
            using (SolidBrush shadowBrush = new SolidBrush(palette.IsDark
                ? Color.FromArgb(130, 0, 0, 0)
                : Color.FromArgb(42, 41, 57, 78)))
                graphics.FillPath(shadowBrush, shadowPath);
        }

        using (GraphicsPath cardPath = RoundedRectangle(card,
            palette.IsHighContrast ? 1 : 13))
        using (Pen borderPen = new Pen(palette.Border))
        {
            if (palette.IsHighContrast)
            {
                using (SolidBrush cardBrush = new SolidBrush(palette.Surface))
                    graphics.FillPath(cardBrush, cardPath);
            }
            else
            {
                Color top = palette.IsDark
                    ? Color.FromArgb(34, 49, 72) : Color.White;
                Color bottom = palette.IsDark
                    ? Color.FromArgb(18, 29, 44)
                    : Color.FromArgb(226, 235, 248);
                using (LinearGradientBrush cardBrush = new LinearGradientBrush(
                    card, top, bottom, LinearGradientMode.Vertical))
                    graphics.FillPath(cardBrush, cardPath);
            }
            graphics.DrawPath(borderPen, cardPath);
        }

        DrawMark(graphics, new Rectangle(14, 7, 58, 50), palette);

        using (Font titleFont = new Font("Segoe UI Semibold", 17.5f,
                   FontStyle.Bold, GraphicsUnit.Point))
        using (Font smallFont = new Font("Segoe UI Semibold", 8.25f,
                   FontStyle.Bold, GraphicsUnit.Point))
        {
            if (!palette.IsHighContrast)
            {
                using (SolidBrush deepShadow = new SolidBrush(palette.IsDark
                    ? Color.FromArgb(150, 0, 0, 0)
                    : Color.FromArgb(65, 16, 37, 68)))
                    graphics.DrawString("RDP Session Reminder", titleFont,
                        deepShadow, 87f, 10.5f);

                Color titleTop = palette.IsDark
                    ? Color.FromArgb(220, 241, 255)
                    : Color.FromArgb(21, 43, 78);
                Color titleBottom = palette.IsDark
                    ? palette.Accent : Color.FromArgb(38, 116, 204);
                using (LinearGradientBrush titleBrush = new LinearGradientBrush(
                    new Rectangle(86, 10, 430, 30), titleTop, titleBottom,
                    LinearGradientMode.Vertical))
                    graphics.DrawString("RDP Session Reminder", titleFont,
                        titleBrush, 85f, 8.5f);
            }
            else
            {
                using (SolidBrush titleBrush = new SolidBrush(palette.Ink))
                    graphics.DrawString("RDP Session Reminder", titleFont,
                        titleBrush, 85f, 8.5f);
            }

            using (SolidBrush accentBrush = new SolidBrush(
                GetSubtitleColor(palette)))
                graphics.DrawString("GUIDED SETUP  |  LIGHTWEIGHT RUNTIME",
                    smallFont, accentBrush, 87f, 39f);
        }

        if (!palette.IsHighContrast)
        {
            using (Pen highlight = new Pen(palette.IsDark
                ? Color.FromArgb(90, 150, 215, 255)
                : Color.FromArgb(150, 255, 255, 255)))
                graphics.DrawLine(highlight, card.Left + 14, card.Top + 1,
                    card.Right - 14, card.Top + 1);
        }
    }

    internal static Color GetSubtitleColor(UiThemePalette palette)
    {
        if (palette == null)
            throw new ArgumentNullException("palette");
        return palette.IsHighContrast
            ? palette.Ink
            : (palette.IsDark ? palette.AccentLight : palette.Accent);
    }

    private static void DrawMark(Graphics graphics, Rectangle bounds,
        UiThemePalette palette)
    {
        if (!palette.IsHighContrast)
        {
            Rectangle glow = new Rectangle(bounds.Left + 1, bounds.Top + 4,
                bounds.Width - 2, bounds.Height - 2);
            using (GraphicsPath glowPath = new GraphicsPath())
            {
                glowPath.AddEllipse(glow);
                using (PathGradientBrush glowBrush =
                    new PathGradientBrush(glowPath))
                {
                    glowBrush.CenterColor = palette.IsDark
                        ? Color.FromArgb(145, 65, 180, 255)
                        : Color.FromArgb(115, 76, 178, 255);
                    glowBrush.SurroundColors = new Color[] {
                        Color.FromArgb(0, 76, 178, 255)
                    };
                    graphics.FillEllipse(glowBrush, glow);
                }
            }
        }

        Rectangle rear = new Rectangle(bounds.Left + 8, bounds.Top + 5,
            36, 27);
        Rectangle front = new Rectangle(bounds.Left + 17, bounds.Top + 15,
            37, 28);
        DrawMonitor(graphics, rear,
            palette.IsHighContrast ? palette.Accent :
                Color.FromArgb(45, 119, 210),
            palette.IsHighContrast ? palette.Accent :
                Color.FromArgb(18, 52, 104), palette);
        DrawMonitor(graphics, front,
            palette.IsHighContrast ? palette.Accent :
                Color.FromArgb(89, 210, 255),
            palette.IsHighContrast ? palette.Accent :
                Color.FromArgb(23, 91, 181), palette);

        using (Pen orbit = new Pen(palette.IsHighContrast
            ? palette.Ink : Color.FromArgb(220, 90, 224, 255),
            palette.IsHighContrast ? 2f : 1.6f))
            graphics.DrawArc(orbit, bounds.Left + 2, bounds.Top + 8,
                bounds.Width - 3, bounds.Height - 12, 205, 245);

        Rectangle badge = new Rectangle(bounds.Right - 19,
            bounds.Bottom - 19, 17, 17);
        using (SolidBrush badgeBrush = new SolidBrush(palette.IsHighContrast
            ? palette.Accent : Color.FromArgb(243, 145, 35)))
        using (Pen badgeBorder = new Pen(palette.IsHighContrast
            ? palette.Border : Color.FromArgb(143, 67, 8)))
        {
            graphics.FillEllipse(badgeBrush, badge);
            graphics.DrawEllipse(badgeBorder, badge);
        }
        using (Font badgeFont = new Font("Segoe UI", 8f, FontStyle.Bold))
            TextRenderer.DrawText(graphics, "R", badgeFont, badge,
                palette.IsHighContrast ? palette.AccentText : Color.White,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private static void DrawMonitor(Graphics graphics, Rectangle rectangle,
        Color top, Color bottom, UiThemePalette palette)
    {
        if (!palette.IsHighContrast)
        {
            Rectangle shadow = rectangle;
            shadow.Offset(2, 3);
            using (GraphicsPath shadowPath = RoundedRectangle(shadow, 4))
            using (SolidBrush shadowBrush = new SolidBrush(
                Color.FromArgb(92, 13, 28, 53)))
                graphics.FillPath(shadowBrush, shadowPath);
        }

        using (GraphicsPath body = RoundedRectangle(rectangle,
            palette.IsHighContrast ? 1 : 4))
        using (Pen rim = new Pen(palette.IsHighContrast
            ? palette.Border : Color.FromArgb(225, 220, 241, 255), 1f))
        {
            if (palette.IsHighContrast)
            {
                using (SolidBrush bodyBrush = new SolidBrush(top))
                    graphics.FillPath(bodyBrush, body);
            }
            else
            {
                using (LinearGradientBrush bodyBrush = new LinearGradientBrush(
                    rectangle, top, bottom, LinearGradientMode.ForwardDiagonal))
                    graphics.FillPath(bodyBrush, body);
            }
            graphics.DrawPath(rim, body);
        }
        Rectangle screen = new Rectangle(rectangle.Left + 4,
            rectangle.Top + 4, rectangle.Width - 8, rectangle.Height - 10);
        using (SolidBrush screenBrush = new SolidBrush(palette.IsHighContrast
            ? palette.Input : Color.FromArgb(110, 185, 236)))
            graphics.FillRectangle(screenBrush, screen);
        using (Pen stand = new Pen(palette.IsHighContrast
            ? palette.Ink : Color.FromArgb(34, 66, 108), 2f))
        {
            int center = rectangle.Left + rectangle.Width / 2;
            graphics.DrawLine(stand, center, rectangle.Bottom,
                center, rectangle.Bottom + 4);
            graphics.DrawLine(stand, center - 6, rectangle.Bottom + 4,
                center + 6, rectangle.Bottom + 4);
        }
    }

    private static GraphicsPath RoundedRectangle(Rectangle rectangle, int radius)
    {
        int diameter = Math.Max(2, radius * 2);
        GraphicsPath path = new GraphicsPath();
        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Top,
            diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter,
            diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter,
            diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class GuidedTabControl : TabControl
{
    public GuidedTabControl()
    {
        DrawMode = TabDrawMode.OwnerDrawFixed;
        SizeMode = TabSizeMode.Normal;
        Padding = new Point(12, 5);
        BackColor = SetupPalette.Canvas;
        ForeColor = SetupPalette.Ink;
        SetStyle(ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint, true);
    }

    protected override void OnDrawItem(DrawItemEventArgs eventArgs)
    {
        if (eventArgs.Index < 0 || eventArgs.Index >= TabPages.Count)
            return;
        UiThemePalette palette = SetupPalette.Current;
        Graphics graphics = eventArgs.Graphics;
        Rectangle tab = GetTabRect(eventArgs.Index);
        bool selected = eventArgs.Index == SelectedIndex;
        bool workflowStep = eventArgs.Index < 4;

        Color top;
        Color bottom;
        if (palette.IsHighContrast)
        {
            top = selected ? SystemColors.Highlight : SystemColors.Control;
            bottom = top;
        }
        else if (palette.IsDark)
        {
            top = selected
                ? Color.FromArgb(43, 61, 88)
                : Color.FromArgb(28, 40, 59);
            bottom = selected
                ? Color.FromArgb(27, 40, 59)
                : Color.FromArgb(19, 29, 43);
        }
        else
        {
            top = selected ? Color.White : Color.FromArgb(232, 237, 245);
            bottom = selected
                ? Color.FromArgb(247, 250, 254)
                : Color.FromArgb(211, 220, 233);
        }

        using (Pen border = new Pen(palette.Border))
        {
            if (palette.IsHighContrast)
            {
                using (SolidBrush fill = new SolidBrush(top))
                    graphics.FillRectangle(fill, tab);
            }
            else
            {
                using (LinearGradientBrush fill = new LinearGradientBrush(
                    tab, top, bottom, LinearGradientMode.Vertical))
                    graphics.FillRectangle(fill, tab);
            }
            graphics.DrawRectangle(border, tab.Left, tab.Top,
                Math.Max(0, tab.Width - 1), Math.Max(0, tab.Height - 1));
        }
        if (selected && !palette.IsHighContrast)
        {
            Color accent = workflowStep ? palette.Accent : palette.MutedInk;
            using (Pen accentPen = new Pen(accent, 3f))
                graphics.DrawLine(accentPen, tab.Left + 2, tab.Top + 1,
                    tab.Right - 3, tab.Top + 1);
        }

        Color textColor = GetTabTextColor(palette, selected);
        TextRenderer.DrawText(graphics, TabPages[eventArgs.Index].Text, Font,
            tab, textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        if (Focused && selected)
        {
            Rectangle focus = Rectangle.Inflate(tab, -5, -5);
            ControlPaint.DrawFocusRectangle(graphics, focus, textColor,
                palette.IsHighContrast && selected
                    ? SystemColors.Highlight : bottom);
        }
    }

    internal static Color GetTabTextColor(UiThemePalette palette,
        bool selected)
    {
        if (palette == null)
            throw new ArgumentNullException("palette");
        if (palette.IsHighContrast)
            return selected
                ? SystemColors.HighlightText : SystemColors.ControlText;
        return selected ? palette.Ink : palette.MutedInk;
    }
}

internal sealed class DimensionalButton : Button
{
    private readonly bool primary;
    private bool hot;
    private bool pressed;

    public DimensionalButton(bool isPrimary)
    {
        primary = isPrimary;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        BackColor = SetupPalette.Raised;
        ForeColor = SetupPalette.Ink;
        SetStyle(ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint, true);
    }

    protected override void OnMouseEnter(EventArgs eventArgs)
    {
        hot = true;
        Invalidate();
        base.OnMouseEnter(eventArgs);
    }

    protected override void OnMouseLeave(EventArgs eventArgs)
    {
        hot = false;
        pressed = false;
        Invalidate();
        base.OnMouseLeave(eventArgs);
    }

    protected override void OnMouseDown(MouseEventArgs eventArgs)
    {
        if (eventArgs.Button == MouseButtons.Left)
            pressed = true;
        Invalidate();
        base.OnMouseDown(eventArgs);
    }

    protected override void OnMouseUp(MouseEventArgs eventArgs)
    {
        pressed = false;
        Invalidate();
        base.OnMouseUp(eventArgs);
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        UiThemePalette palette = SetupPalette.Current;
        Graphics graphics = eventArgs.Graphics;
        Rectangle outer = new Rectangle(0, 0,
            Math.Max(1, Width - 1), Math.Max(1, Height - 1));

        if (palette.IsHighContrast)
        {
            ButtonState state = !Enabled
                ? ButtonState.Inactive
                : pressed ? ButtonState.Pushed : ButtonState.Normal;
            ControlPaint.DrawButton(graphics, outer, state);
            Color systemText = Enabled
                ? SystemColors.ControlText : SystemColors.GrayText;
            TextRenderer.DrawText(graphics, Text, Font, ClientRectangle,
                systemText, TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis);
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(graphics,
                    Rectangle.Inflate(ClientRectangle, -4, -4));
            return;
        }

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Color top;
        Color bottom;
        Color border;
        Color textColor;
        if (!Enabled)
        {
            top = palette.DisabledSurface;
            bottom = palette.DisabledSurface;
            border = palette.Border;
            textColor = palette.DisabledInk;
        }
        else if (primary)
        {
            GetPrimaryColors(palette, hot, pressed, out top, out bottom,
                out border, out textColor);
        }
        else
        {
            top = hot ? palette.RaisedHot : palette.Raised;
            bottom = pressed ? palette.Pressed : palette.Surface;
            border = palette.Border;
            textColor = palette.Ink;
        }
        using (LinearGradientBrush fill = new LinearGradientBrush(
            outer, top, bottom, LinearGradientMode.Vertical))
        using (Pen borderPen = new Pen(border))
        {
            graphics.FillRectangle(fill, outer);
            graphics.DrawRectangle(borderPen, outer);
        }
        if (Enabled && !pressed)
        {
            using (Pen highlight = new Pen(palette.IsDark
                ? Color.FromArgb(80, 255, 255, 255)
                : Color.FromArgb(155, 255, 255, 255)))
                graphics.DrawLine(highlight, 2, 1, Width - 3, 1);
        }
        Rectangle textBounds = ClientRectangle;
        if (pressed)
            textBounds.Offset(0, 1);
        TextRenderer.DrawText(graphics, Text, Font, textBounds, textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        if (Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(graphics,
                Rectangle.Inflate(ClientRectangle, -4, -4), textColor,
                Color.Transparent);
    }

    private static void GetPrimaryColors(UiThemePalette palette, bool isHot,
        bool isPressed, out Color top, out Color bottom, out Color border,
        out Color text)
    {
        if (palette.IsDark)
        {
            top = isHot
                ? Color.FromArgb(32, 113, 191)
                : Color.FromArgb(27, 104, 177);
            bottom = isPressed
                ? Color.FromArgb(15, 70, 125)
                : Color.FromArgb(20, 91, 155);
            border = Color.FromArgb(104, 191, 255);
        }
        else
        {
            top = isHot
                ? Color.FromArgb(31, 112, 201)
                : Color.FromArgb(24, 99, 181);
            bottom = isPressed
                ? Color.FromArgb(19, 68, 137)
                : palette.AccentDark;
            border = Color.FromArgb(12, 53, 111);
        }
        text = palette.AccentText;
    }
}
