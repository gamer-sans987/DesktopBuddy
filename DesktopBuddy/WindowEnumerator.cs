using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DesktopBuddy;

/// <summary>
/// Enumerates visible top-level windows via Win32.
/// </summary>
public static class WindowEnumerator
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out bool pvAttribute, int cbAttribute);

    private const int DWMWA_CLOAKED = 14;

    public record WindowInfo(IntPtr Handle, string Title, uint ProcessId);

    public static List<WindowInfo> GetOpenWindows()
    {
        var windows = new List<WindowInfo>();
        var shellWindow = GetShellWindow();

        EnumWindows((hWnd, _) =>
        {
            if (hWnd == shellWindow) return true;
            if (!IsWindowVisible(hWnd)) return true;

            // Skip cloaked windows (UWP hidden windows, etc.)
            DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out bool cloaked, Marshal.SizeOf<bool>());
            if (cloaked) return true;

            int length = GetWindowTextLength(hWnd);
            if (length == 0) return true;

            var sb = new StringBuilder(length + 1);
            GetWindowTextW(hWnd, sb, sb.Capacity);
            string title = sb.ToString();

            if (string.IsNullOrWhiteSpace(title)) return true;

            GetWindowThreadProcessId(hWnd, out uint pid);
            windows.Add(new WindowInfo(hWnd, title, pid));
            return true;
        }, IntPtr.Zero);

        return windows;
    }
}
