using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using HarmonyLib;
using FrooxEngine;
using Elements.Core;
using Elements.Assets;

namespace DesktopBuddy;

[HarmonyPatch(typeof(ContextMenu), nameof(ContextMenu.OpenMenu))]
public static class ContextMenuPatch
{
    private const int PAGE_SIZE = 8;

    // Cache: SHA256 of icon pixel data → local:// asset URI
    private static readonly ConcurrentDictionary<string, Uri> _iconCache = new();

    private static readonly string[] IgnoredTitles = { "Resonite", "vrmonitor", "SteamVR Status" };
    private static bool ShouldIgnore(string title)
    {
        foreach (var ignored in IgnoredTitles)
            if (title.Contains(ignored, StringComparison.OrdinalIgnoreCase)) return true;
        if (title.Contains("rainmeter", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static readonly FieldInfo _itemsRootField = typeof(ContextMenu)
        .GetField("_itemsRoot", BindingFlags.NonPublic | BindingFlags.Instance);

    private static void ClearMenu(ContextMenu menu)
    {
        var itemsRoot = _itemsRootField?.GetValue(menu) as SyncRef<Slot>;
        itemsRoot?.Target?.DestroyChildren();
    }

    // Monitor enumeration
    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);
    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor, rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    private record MonitorInfo(IntPtr Handle, string Name, int Width, int Height);

    private static List<MonitorInfo> GetMonitors()
    {
        var monitors = new List<MonitorInfo>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref RECT rc, IntPtr data) =>
        {
            var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            GetMonitorInfo(hMon, ref info);
            int w = info.rcMonitor.Right - info.rcMonitor.Left;
            int h = info.rcMonitor.Bottom - info.rcMonitor.Top;
            monitors.Add(new MonitorInfo(hMon, info.szDevice, w, h));
            return true;
        }, IntPtr.Zero);
        return monitors;
    }

    /// <summary>
    /// Get a local:// URI for a window icon. Cached by pixel hash.
    /// Blocks until saved on first call, instant on subsequent calls.
    /// </summary>
    private static Uri GetIconUri(IntPtr hwnd, Engine engine)
    {
        try
        {
            var iconData = WindowIconExtractor.GetIconRGBA(hwnd, out int w, out int h);
            if (iconData == null || w <= 0 || h <= 0) return null;

            var hash = Convert.ToHexString(SHA256.HashData(iconData));
            if (_iconCache.TryGetValue(hash, out var cached))
                return cached;

            var bitmap = new Bitmap2D(iconData, w, h,
                Renderite.Shared.TextureFormat.RGBA32, false, Renderite.Shared.ColorProfile.sRGB, false);
            var uri = engine.LocalDB.SaveAssetAsync(bitmap).GetAwaiter().GetResult();
            if (uri != null)
            {
                _iconCache[hash] = uri;
                DesktopBuddyMod.Msg($"[Icon] Cached {w}x{h} -> {uri}");
            }
            return uri;
        }
        catch (Exception ex)
        {
            DesktopBuddyMod.Msg($"[Icon] Error for hwnd={hwnd}: {ex.Message}");
            return null;
        }
    }

    public static void Postfix(ContextMenu __instance)
    {
        LocaleString label = "Desktop";
        colorX? color = colorX.Cyan;
        var item = __instance.AddItem(in label, (Uri)null!, in color);
        item.Button.LocalPressed += (IButton btn, ButtonEventData data) =>
        {
            ShowPickerPage(__instance, 0);
        };
    }

    private static void ShowPickerPage(ContextMenu menu, int page)
    {
        ClearMenu(menu);
        var world = menu.World;
        var engine = world.Engine;

        // Build combined list: monitors first, then windows
        var entries = new List<(string label, colorX color, Action action, IntPtr hwnd)>();

        // Monitors
        var monitors = GetMonitors();
        for (int i = 0; i < monitors.Count; i++)
        {
            var mon = monitors[i];
            int idx = i;
            entries.Add(($"Monitor {idx + 1} ({mon.Width}x{mon.Height})",
                new colorX(0.1f, 0.25f, 0.4f, 1f),
                () => { menu.Close(); DesktopBuddyMod.SpawnStreaming(world, IntPtr.Zero, $"Monitor {idx + 1}"); },
                IntPtr.Zero));
        }

        // Windows
        var allWindows = WindowEnumerator.GetOpenWindows();
        foreach (var win in allWindows)
        {
            if (ShouldIgnore(win.Title)) continue;
            var handle = win.Handle;
            var title = win.Title;
            string display = title.Length > 30 ? title[..27] + "..." : title;
            entries.Add((display,
                new colorX(0.15f, 0.15f, 0.25f, 1f),
                () => { menu.Close(); DesktopBuddyMod.SpawnStreaming(world, handle, title); },
                handle));
        }

        int totalPages = (entries.Count + PAGE_SIZE - 1) / PAGE_SIZE;
        int start = page * PAGE_SIZE;
        int end = Math.Min(start + PAGE_SIZE, entries.Count);

        for (int i = start; i < end; i++)
        {
            var entry = entries[i];
            LocaleString lbl = entry.label;
            colorX? c = entry.color;
            var act = entry.action;

            // Use cached icon URI if available, kick off background cache if not
            Uri iconUri = null;
            if (entry.hwnd != IntPtr.Zero)
                iconUri = GetIconUri(entry.hwnd, engine);

            var mi = menu.AddItem(in lbl, iconUri, in c);
            mi.Button.LocalPressed += (IButton b, ButtonEventData d) => act();
        }

        // Pagination controls
        if (page > 0)
        {
            LocaleString lbl = $"< Prev (Page {page}/{totalPages})";
            colorX? c = new colorX(0.3f, 0.3f, 0.1f, 1f);
            var mi = menu.AddItem(in lbl, (Uri)null!, in c);
            int prev = page - 1;
            mi.Button.LocalPressed += (IButton b, ButtonEventData d) => ShowPickerPage(menu, prev);
        }
        if (page < totalPages - 1)
        {
            LocaleString lbl = $"Next > (Page {page + 2}/{totalPages})";
            colorX? c = new colorX(0.3f, 0.3f, 0.1f, 1f);
            var mi = menu.AddItem(in lbl, (Uri)null!, in c);
            int next = page + 1;
            mi.Button.LocalPressed += (IButton b, ButtonEventData d) => ShowPickerPage(menu, next);
        }
    }
}
