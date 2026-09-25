[CmdletBinding()]
param(
    [string]$Output,
    [string]$Preview
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Output)) {
    $Output = Join-Path $repoRoot 'assets\RdpSessionReminder.ico'
}
if ([string]::IsNullOrWhiteSpace($Preview)) {
    $Preview = Join-Path $repoRoot 'assets\RdpSessionReminder-logo.png'
}
Add-Type -AssemblyName System.Drawing

$source = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class RdpReminderBrandIconGenerator
{
    public static void Write(string iconPath, string previewPath)
    {
        using (Bitmap master = Draw(512))
        {
            master.Save(previewPath, ImageFormat.Png);
            int[] sizes = new int[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
            List<byte[]> frames = new List<byte[]>();
            foreach (int size in sizes)
            {
                using (Bitmap frame = Resize(master, size))
                using (MemoryStream stream = new MemoryStream())
                {
                    frame.Save(stream, ImageFormat.Png);
                    frames.Add(stream.ToArray());
                }
            }

            using (FileStream output = new FileStream(iconPath,
                FileMode.Create, FileAccess.Write, FileShare.None))
            using (BinaryWriter writer = new BinaryWriter(output))
            {
                writer.Write((ushort)0);
                writer.Write((ushort)1);
                writer.Write((ushort)sizes.Length);
                int offset = 6 + sizes.Length * 16;
                for (int index = 0; index < sizes.Length; index++)
                {
                    int size = sizes[index];
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
            }
        }
    }

    private static Bitmap Resize(Bitmap source, int size)
    {
        Bitmap result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(result))
        {
            graphics.Clear(Color.Transparent);
            graphics.CompositingMode = CompositingMode.SourceOver;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.DrawImage(source, new Rectangle(0, 0, size, size));
        }
        return result;
    }

    private static Bitmap Draw(int size)
    {
        Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.CompositingQuality = CompositingQuality.HighQuality;

            Rectangle shadow = new Rectangle(38, 48, 436, 430);
            using (GraphicsPath shadowPath = Rounded(shadow, 98))
            using (SolidBrush shadowBrush = new SolidBrush(
                Color.FromArgb(92, 8, 20, 43)))
                graphics.FillPath(shadowBrush, shadowPath);

            Rectangle tile = new Rectangle(28, 28, 438, 438);
            using (GraphicsPath tilePath = Rounded(tile, 98))
            using (LinearGradientBrush tileBrush = new LinearGradientBrush(
                tile, Color.FromArgb(39, 105, 200),
                Color.FromArgb(8, 27, 66), 55f))
            using (Pen edge = new Pen(Color.FromArgb(235, 140, 219, 255), 8f))
            {
                graphics.FillPath(tileBrush, tilePath);
                graphics.DrawPath(edge, tilePath);
            }

            Rectangle glowBounds = new Rectangle(78, 64, 350, 350);
            using (GraphicsPath glowPath = new GraphicsPath())
            {
                glowPath.AddEllipse(glowBounds);
                using (PathGradientBrush glow = new PathGradientBrush(glowPath))
                {
                    glow.CenterColor = Color.FromArgb(140, 60, 194, 255);
                    glow.SurroundColors = new Color[] {
                        Color.FromArgb(0, 60, 194, 255)
                    };
                    graphics.FillEllipse(glow, glowBounds);
                }
            }

            DrawPlane(graphics, new Rectangle(105, 102, 270, 198),
                Color.FromArgb(61, 118, 211), Color.FromArgb(13, 43, 103), 0);
            DrawPlane(graphics, new Rectangle(129, 129, 270, 198),
                Color.FromArgb(70, 174, 240), Color.FromArgb(17, 72, 153), 1);
            DrawPlane(graphics, new Rectangle(153, 156, 270, 198),
                Color.FromArgb(111, 227, 255), Color.FromArgb(26, 99, 199), 2);

            using (Pen orbitShadow = new Pen(Color.FromArgb(120, 4, 21, 49), 20f))
            using (Pen orbit = new Pen(Color.FromArgb(235, 107, 236, 255), 11f))
            {
                graphics.DrawArc(orbitShadow, 62, 91, 390, 318, 203, 246);
                graphics.DrawArc(orbit, 55, 82, 390, 318, 203, 246);
            }

            Rectangle badge = new Rectangle(332, 327, 112, 112);
            using (SolidBrush badgeShadow = new SolidBrush(
                Color.FromArgb(130, 5, 19, 43)))
                graphics.FillEllipse(badgeShadow, badge.X + 8, badge.Y + 12,
                    badge.Width, badge.Height);
            using (LinearGradientBrush badgeBrush = new LinearGradientBrush(
                badge, Color.FromArgb(255, 207, 78),
                Color.FromArgb(227, 91, 22), LinearGradientMode.Vertical))
            using (Pen badgeEdge = new Pen(Color.FromArgb(255, 246, 220, 143), 5f))
            {
                graphics.FillEllipse(badgeBrush, badge);
                graphics.DrawEllipse(badgeEdge, badge);
            }
            using (Font font = new Font("Segoe UI", 66f, FontStyle.Bold,
                GraphicsUnit.Pixel))
            using (StringFormat format = new StringFormat())
            using (SolidBrush textShadow = new SolidBrush(
                Color.FromArgb(110, 65, 25, 5)))
            using (SolidBrush text = new SolidBrush(Color.White))
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                RectangleF shadowText = badge;
                shadowText.Offset(3f, 5f);
                graphics.DrawString("R", font, textShadow, shadowText, format);
                graphics.DrawString("R", font, text, badge, format);
            }

            using (Pen topLight = new Pen(Color.FromArgb(155, 255, 255, 255), 6f))
                graphics.DrawArc(topLight, 54, 51, 385, 385, 205, 120);
        }
        return bitmap;
    }

    private static void DrawPlane(Graphics graphics, Rectangle bounds,
        Color top, Color bottom, int depth)
    {
        Rectangle shadow = bounds;
        shadow.Offset(12, 16);
        using (GraphicsPath shadowPath = Rounded(shadow, 24))
        using (SolidBrush shadowBrush = new SolidBrush(
            Color.FromArgb(88, 4, 18, 43)))
            graphics.FillPath(shadowBrush, shadowPath);

        using (GraphicsPath path = Rounded(bounds, 24))
        using (LinearGradientBrush fill = new LinearGradientBrush(
            bounds, top, bottom, LinearGradientMode.ForwardDiagonal))
        using (Pen rim = new Pen(Color.FromArgb(225, 219, 246, 255), 6f))
        {
            graphics.FillPath(fill, path);
            graphics.DrawPath(rim, path);
        }
        Rectangle screen = Rectangle.Inflate(bounds, -24, -24);
        screen.Height -= 22;
        using (LinearGradientBrush screenFill = new LinearGradientBrush(
            screen, Color.FromArgb(230, 247, 255),
            Color.FromArgb(54 + depth * 9, 136 + depth * 6, 224),
            LinearGradientMode.ForwardDiagonal))
            graphics.FillRectangle(screenFill, screen);
        using (Pen shine = new Pen(Color.FromArgb(220, 255, 255, 255), 5f))
            graphics.DrawLine(shine, screen.Left + 10, screen.Top + 10,
                screen.Right - 28, screen.Top + 10);
    }

    private static GraphicsPath Rounded(Rectangle rectangle, int radius)
    {
        int diameter = radius * 2;
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
'@

if (-not ('RdpReminderBrandIconGenerator' -as [type])) {
    Add-Type -TypeDefinition $source -ReferencedAssemblies @(
        [Drawing.Bitmap].Assembly.Location,
        [Drawing.Point].Assembly.Location
    )
}

$iconPath = [IO.Path]::GetFullPath($Output)
$previewPath = [IO.Path]::GetFullPath($Preview)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($iconPath)) | Out-Null
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($previewPath)) | Out-Null
[RdpReminderBrandIconGenerator]::Write($iconPath, $previewPath)
Write-Host "Brand icon: $iconPath"
Write-Host "Logo preview: $previewPath"
