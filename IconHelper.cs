using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Nocturne
{
    public static class IconHelper
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_SETICON = 0x0080;
        private static readonly IntPtr ICON_SMALL = new(0);
        private static readonly IntPtr ICON_BIG = new(1);

        private static Icon? _cachedDrawingIcon;
        private static ImageSource? _cachedWpfIcon;

        public static string TrimmedIcoPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nocturne",
            "nocturne_trimmed.ico");

        public static void EnsureTrimmedIcoFile()
        {
            try
            {
                string targetPath = TrimmedIcoPath;
                string? dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                int[] sizes = { 16, 24, 32, 48, 64, 256 };
                var pngLayers = new List<(int Size, byte[] Data)>();

                foreach (int s in sizes)
                {
                    using var bmp = LoadAndTrimIconBitmap(s);
                    if (bmp == null)
                        return;

                    using var ms = new MemoryStream();
                    bmp.Save(ms, ImageFormat.Png);
                    pngLayers.Add((s, ms.ToArray()));
                }

                using var fs = new FileStream(targetPath, FileMode.Create, FileAccess.Write);
                using var bw = new BinaryWriter(fs);

                // ICO Header
                bw.Write((short)0);
                bw.Write((short)1);
                bw.Write((short)pngLayers.Count);

                int offset = 6 + (16 * pngLayers.Count);
                foreach (var layer in pngLayers)
                {
                    bw.Write((byte)(layer.Size >= 256 ? 0 : layer.Size));
                    bw.Write((byte)(layer.Size >= 256 ? 0 : layer.Size));
                    bw.Write((byte)0);
                    bw.Write((byte)0);
                    bw.Write((short)1);
                    bw.Write((short)32);
                    bw.Write(layer.Data.Length);
                    bw.Write(offset);
                    offset += layer.Data.Length;
                }

                foreach (var layer in pngLayers)
                {
                    bw.Write(layer.Data);
                }
            }
            catch { }
        }

        public static void ApplyWindowIcons(Window window)
        {
            try
            {
                window.Icon = GetWindowIcon();
                IntPtr hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero)
                    return;

                using var bigBmp = LoadAndTrimIconBitmap(48);
                using var smallBmp = LoadAndTrimIconBitmap(16);

                if (bigBmp != null)
                {
                    IntPtr hBig = bigBmp.GetHicon();
                    SendMessage(hwnd, WM_SETICON, ICON_BIG, hBig);
                }

                if (smallBmp != null)
                {
                    IntPtr hSmall = smallBmp.GetHicon();
                    SendMessage(hwnd, WM_SETICON, ICON_SMALL, hSmall);
                }
            }
            catch { }
        }

        public static Icon GetTrayIcon()
        {
            if (_cachedDrawingIcon != null)
                return _cachedDrawingIcon;

            using var trimmedBmp = LoadAndTrimIconBitmap(64);
            if (trimmedBmp != null)
            {
                _cachedDrawingIcon = BitmapToIcon(trimmedBmp);
                return _cachedDrawingIcon;
            }

            _cachedDrawingIcon = CreateProceduralIcon(64);
            return _cachedDrawingIcon;
        }

        public static ImageSource GetWindowIcon()
        {
            if (_cachedWpfIcon != null)
                return _cachedWpfIcon;

            using var trimmedBmp = LoadAndTrimIconBitmap(256);
            if (trimmedBmp != null)
            {
                using var icon = BitmapToIcon(trimmedBmp);
                var source = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                _cachedWpfIcon = source;
                return _cachedWpfIcon;
            }

            using var fallbackIcon = CreateProceduralIcon(128);
            var fallbackSource = Imaging.CreateBitmapSourceFromHIcon(
                fallbackIcon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            fallbackSource.Freeze();
            _cachedWpfIcon = fallbackSource;
            return _cachedWpfIcon;
        }

        private static Bitmap? LoadAndTrimIconBitmap(int targetSize)
        {
            string customIcoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "nocturne.ico");
            if (!File.Exists(customIcoPath))
                return null;

            try
            {
                using var rawIcon = new Icon(customIcoPath, 256, 256);
                using var src = rawIcon.ToBitmap();

                int minX = src.Width, minY = src.Height, maxX = -1, maxY = -1;
                for (int y = 0; y < src.Height; y++)
                {
                    for (int x = 0; x < src.Width; x++)
                    {
                        if (src.GetPixel(x, y).A > 20)
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                    }
                }

                if (maxX < minX || maxY < minY)
                    return new Bitmap(src, targetSize, targetSize);

                int cropW = maxX - minX + 1;
                int cropH = maxY - minY + 1;
                int side = Math.Max(cropW, cropH);

                int cx = minX + cropW / 2;
                int cy = minY + cropH / 2;
                var cropRect = new Rectangle(cx - side / 2, cy - side / 2, side, side);

                var result = new Bitmap(targetSize, targetSize);
                using (var g = Graphics.FromImage(result))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(System.Drawing.Color.Transparent);

                    // Windows 11 görev çubuğu boyutuna tam oturması için kenar boşluğunu sıfıra indir
                    var destRect = new Rectangle(0, 0, targetSize, targetSize);
                    g.DrawImage(src, destRect, cropRect, GraphicsUnit.Pixel);
                }

                return result;
            }
            catch
            {
                return null;
            }
        }

        private static Icon BitmapToIcon(Bitmap bmp)
        {
            IntPtr hIcon = bmp.GetHicon();
            try
            {
                using var tempIcon = Icon.FromHandle(hIcon);
                return (Icon)tempIcon.Clone();
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }

        private static Icon CreateProceduralIcon(int size)
        {
            using var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(System.Drawing.Color.Transparent);

                using var bgBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
                    new Rectangle(0, 0, size, size),
                    System.Drawing.Color.FromArgb(255, 24, 24, 34),
                    System.Drawing.Color.FromArgb(255, 12, 12, 18),
                    45f);
                g.FillEllipse(bgBrush, 1, 1, size - 2, size - 2);

                using var ringPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(255, 162, 56, 255), size * 0.06f);
                g.DrawEllipse(ringPen, 2, 2, size - 4, size - 4);

                using var accentBrush = new SolidBrush(System.Drawing.Color.FromArgb(255, 178, 86, 255));
                PointF[] triangle =
                {
                    new PointF(size * 0.40f, size * 0.30f),
                    new PointF(size * 0.40f, size * 0.70f),
                    new PointF(size * 0.70f, size * 0.50f)
                };
                g.FillPolygon(accentBrush, triangle);
            }

            return BitmapToIcon(bmp);
        }
    }
}