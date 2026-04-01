using System;
using System.Runtime.InteropServices;

namespace DesktopBuddy;

/// <summary>
/// Extracts window icons as RGBA pixel data via Win32.
/// </summary>
public static class WindowIconExtractor
{
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

    [DllImport("gdi32.dll")]
    private static extern int GetObject(IntPtr hgdiobj, int cbBuffer, ref BITMAP lpvObject);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines,
        byte[] lpvBits, ref BITMAPINFO lpbi, uint uUsage);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    private const uint WM_GETICON = 0x007F;
    private const int ICON_BIG = 1;
    private const int ICON_SMALL2 = 2;
    private const int GCL_HICON = -14;
    private const int GCL_HICONSM = -34;

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        public bool fIcon;
        public int xHotspot, yHotspot;
        public IntPtr hbmMask, hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType, bmWidth, bmHeight, bmWidthBytes;
        public ushort bmPlanes, bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth, biHeight;
        public ushort biPlanes, biBitCount;
        public uint biCompression, biSizeImage;
        public int biXPelsPerMeter, biYPelsPerMeter;
        public uint biClrUsed, biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
    }

    /// <summary>
    /// Extract a window's icon as RGBA pixels. Returns null if no icon found.
    /// </summary>
    public static byte[] GetIconRGBA(IntPtr hwnd, out int width, out int height)
    {
        width = height = 0;

        IntPtr hIcon = SendMessage(hwnd, WM_GETICON, (IntPtr)ICON_BIG, IntPtr.Zero);
        if (hIcon == IntPtr.Zero)
            hIcon = SendMessage(hwnd, WM_GETICON, (IntPtr)ICON_SMALL2, IntPtr.Zero);
        if (hIcon == IntPtr.Zero)
            hIcon = GetClassLongPtr(hwnd, GCL_HICON);
        if (hIcon == IntPtr.Zero)
            hIcon = GetClassLongPtr(hwnd, GCL_HICONSM);
        if (hIcon == IntPtr.Zero)
            return null;

        if (!GetIconInfo(hIcon, out ICONINFO iconInfo))
            return null;

        try
        {
            if (iconInfo.hbmColor == IntPtr.Zero)
                return null;

            var bmp = new BITMAP();
            GetObject(iconInfo.hbmColor, Marshal.SizeOf<BITMAP>(), ref bmp);
            if (bmp.bmWidth <= 0 || bmp.bmHeight <= 0)
                return null;

            width = bmp.bmWidth;
            height = bmp.bmHeight;

            var bmi = new BITMAPINFO
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = width,
                    biHeight = -height, // top-down
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0
                }
            };

            byte[] pixels = new byte[width * height * 4];
            IntPtr hdc = CreateCompatibleDC(IntPtr.Zero);
            try
            {
                GetDIBits(hdc, iconInfo.hbmColor, 0, (uint)height, pixels, ref bmi, 0);
            }
            finally
            {
                DeleteDC(hdc);
            }

            // BGRA → RGBA
            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte tmp = pixels[i];
                pixels[i] = pixels[i + 2];
                pixels[i + 2] = tmp;
            }

            return pixels;
        }
        finally
        {
            if (iconInfo.hbmColor != IntPtr.Zero) DeleteObject(iconInfo.hbmColor);
            if (iconInfo.hbmMask != IntPtr.Zero) DeleteObject(iconInfo.hbmMask);
        }
    }
}
