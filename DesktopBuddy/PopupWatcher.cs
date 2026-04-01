using System;
using System.Runtime.InteropServices;

namespace DesktopBuddy;

/// <summary>
/// Watches for popup/context menu windows appearing via SetWinEventHook.
/// Calls back with the popup HWND when detected.
/// </summary>
public sealed class PopupWatcher : IDisposable
{
    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    private const uint EVENT_SYSTEM_MENUPOPUPSTART = 0x0006;
    private const uint EVENT_SYSTEM_MENUPOPUPEND = 0x0007;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    private IntPtr _hookStart;
    private IntPtr _hookEnd;
    private WinEventDelegate _delegateStart; // prevent GC
    private WinEventDelegate _delegateEnd;

    public event Action<IntPtr, RECT> PopupOpened;
    public event Action<IntPtr> PopupClosed;

    public void Start()
    {
        _delegateStart = OnPopupStart;
        _delegateEnd = OnPopupEnd;
        _hookStart = SetWinEventHook(EVENT_SYSTEM_MENUPOPUPSTART, EVENT_SYSTEM_MENUPOPUPSTART,
            IntPtr.Zero, _delegateStart, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        _hookEnd = SetWinEventHook(EVENT_SYSTEM_MENUPOPUPEND, EVENT_SYSTEM_MENUPOPUPEND,
            IntPtr.Zero, _delegateEnd, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
    }

    private void OnPopupStart(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (hwnd == IntPtr.Zero || !IsWindowVisible(hwnd)) return;
        if (!GetWindowRect(hwnd, out RECT rect)) return;
        if (rect.Width <= 0 || rect.Height <= 0) return;

        ResoniteModLoader.ResoniteMod.Msg($"[PopupWatcher] Popup opened hwnd={hwnd} {rect.Width}x{rect.Height}");
        PopupOpened?.Invoke(hwnd, rect);
    }

    private void OnPopupEnd(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (hwnd == IntPtr.Zero) return;
        ResoniteModLoader.ResoniteMod.Msg($"[PopupWatcher] Popup closed hwnd={hwnd}");
        PopupClosed?.Invoke(hwnd);
    }

    public void Dispose()
    {
        if (_hookStart != IntPtr.Zero) { UnhookWinEvent(_hookStart); _hookStart = IntPtr.Zero; }
        if (_hookEnd != IntPtr.Zero) { UnhookWinEvent(_hookEnd); _hookEnd = IntPtr.Zero; }
    }
}
