using System;
using System.Runtime.InteropServices;

namespace DesktopBuddy;

/// <summary>
/// Captures a window's client area using BitBlt.
/// Returns raw BGRA pixel data (no conversion — caller handles format).
/// </summary>
public sealed class WindowCapture : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
        IntPtr hdcSrc, int xSrc, int ySrc, uint rop);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines,
        IntPtr lpvBits, ref BITMAPINFO lpbi, uint uUsage);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    private const uint PW_RENDERFULLCONTENT = 0x00000002;
    private const uint SRCCOPY = 0x00CC0020;
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
    }

    private IntPtr _hWnd;
    private GCHandle _pinnedHandle;
    private byte[] _buffer;

    private bool _isDesktop;

    public int ClientWidth { get; private set; }
    public int ClientHeight { get; private set; }
    public bool IsValid => _isDesktop || (_hWnd != IntPtr.Zero && IsWindow(_hWnd) && !IsIconic(_hWnd));

    public WindowCapture(IntPtr hWnd)
    {
        _hWnd = hWnd;
        _isDesktop = hWnd == IntPtr.Zero;
    }

    /// <summary>
    /// Captures the client area of the window.
    /// Returns raw BGRA pixel data (top-down). No format conversion.
    /// The returned array is an internal buffer — do not hold references across calls.
    /// </summary>
    public byte[] CaptureFrame()
    {
        if (!IsValid) return null;

        int width, height;
        if (_isDesktop)
        {
            width = GetSystemMetrics(SM_CXSCREEN);
            height = GetSystemMetrics(SM_CYSCREEN);
        }
        else
        {
            if (!GetClientRect(_hWnd, out RECT clientRect)) return null;
            width = clientRect.Width;
            height = clientRect.Height;
        }

        if (width <= 0 || height <= 0) return null;

        ClientWidth = width;
        ClientHeight = height;

        IntPtr hdcWindow = GetDC(_hWnd);
        if (hdcWindow == IntPtr.Zero) return null;

        IntPtr hdcMem = IntPtr.Zero;
        IntPtr hBitmap = IntPtr.Zero;
        IntPtr hOld = IntPtr.Zero;

        try
        {
            hdcMem = CreateCompatibleDC(hdcWindow);
            hBitmap = CreateCompatibleBitmap(hdcWindow, width, height);
            hOld = SelectObject(hdcMem, hBitmap);

            BitBlt(hdcMem, 0, 0, width, height, hdcWindow, 0, 0, SRCCOPY);

            var bmi = new BITMAPINFO
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = width,
                    biHeight = -height, // Negative = top-down
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0 // BI_RGB
                }
            };

            int bufferSize = width * height * 4;
            EnsureBuffer(bufferSize);

            GetDIBits(hdcMem, hBitmap, 0, (uint)height, _pinnedHandle.AddrOfPinnedObject(), ref bmi, 0);

            return _buffer;
        }
        finally
        {
            if (hOld != IntPtr.Zero) SelectObject(hdcMem, hOld);
            if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
            if (hdcMem != IntPtr.Zero) DeleteDC(hdcMem);
            ReleaseDC(_hWnd, hdcWindow);
        }
    }

    private void EnsureBuffer(int size)
    {
        if (_buffer != null && _buffer.Length == size) return;

        if (_pinnedHandle.IsAllocated)
            _pinnedHandle.Free();

        _buffer = new byte[size];
        _pinnedHandle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
    }

    public void Dispose()
    {
        if (_pinnedHandle.IsAllocated)
            _pinnedHandle.Free();
        _hWnd = IntPtr.Zero;
        _buffer = null;
    }
}
