using System;
using System.Runtime.InteropServices;

namespace DesktopBuddy;

/// <summary>
/// Sends mouse input to a target window.
/// Moves real cursor + mouse_event for everything.
/// </summary>
public static class WindowInput
{
    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, int dx, int dy, int dwData, IntPtr dwExtraInfo);

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    private static POINT UvToScreen(IntPtr hWnd, float u, float v, int clientW, int clientH)
    {
        int px = (int)(u * clientW);
        int py = (int)(v * clientH);
        var pt = new POINT { X = px, Y = py };
        ClientToScreen(hWnd, ref pt);
        return pt;
    }

    private static void EnsureFocused(IntPtr hWnd)
    {
        if (hWnd != IntPtr.Zero)
            SetForegroundWindow(hWnd);
    }

    public static void SendHover(IntPtr hWnd, float u, float v, int clientW, int clientH)
    {
        EnsureFocused(hWnd);
        var pt = UvToScreen(hWnd, u, v, clientW, clientH);
        SetCursorPos(pt.X, pt.Y);
    }

    public static void SendMouseDown(IntPtr hWnd, float u, float v, int clientW, int clientH)
    {
        EnsureFocused(hWnd);
        var pt = UvToScreen(hWnd, u, v, clientW, clientH);
        SetCursorPos(pt.X, pt.Y);
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
    }

    public static void SendMouseMove(IntPtr hWnd, float u, float v, int clientW, int clientH)
    {
        var pt = UvToScreen(hWnd, u, v, clientW, clientH);
        SetCursorPos(pt.X, pt.Y);
    }

    public static void SendMouseUp(IntPtr hWnd, float u, float v, int clientW, int clientH)
    {
        var pt = UvToScreen(hWnd, u, v, clientW, clientH);
        SetCursorPos(pt.X, pt.Y);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
    }

    public static void SendScroll(IntPtr hWnd, float u, float v, int clientW, int clientH, int clicks)
    {
        EnsureFocused(hWnd);
        var pt = UvToScreen(hWnd, u, v, clientW, clientH);
        SetCursorPos(pt.X, pt.Y);
        int wheelDelta = clicks * 120;
        mouse_event(MOUSEEVENTF_WHEEL, 0, 0, wheelDelta, IntPtr.Zero);
    }
}
