using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using ResoniteModLoader;
using FrooxEngine;
using FrooxEngine.UIX;
using Elements.Core;
using Elements.Assets;

namespace DesktopBuddy;

public class DesktopBuddyMod : ResoniteMod
{
    public override string Name => "DesktopBuddy";
    public override string Author => "DesktopBuddy";
    public override string Version => "1.0.0";
    public override string Link => "https://github.com/DesktopBuddy/DesktopBuddy";

    internal static ModConfiguration? Config;

    // Streaming server kept for future remote user support
    // internal static MjpegServer? Server;

    [AutoRegisterConfigKey]
    internal static readonly ModConfigurationKey<int> FrameRate =
        new("frameRate", "Target capture frame rate", () => 30);

    internal static readonly List<DesktopSession> ActiveSessions = new();

    public override void OnEngineInit()
    {
        Config = GetConfiguration();
        Config!.Save(true);

        Harmony harmony = new("com.desktopbuddy.mod");
        harmony.PatchAll();

        Msg("DesktopBuddy initialized!");
    }

    internal static void SpawnStreaming(World world, IntPtr hwnd, string title)
    {
        try
        {
            var localUser = world.LocalUser;
            if (localUser == null) return;
            var userRoot = localUser.Root;
            if (userRoot == null) return;

            var root = world.RootSlot.AddSlot("Desktop Buddy");

            var headPos = userRoot.HeadPosition;
            var headRot = userRoot.HeadRotation;
            var forward = headRot * float3.Forward;
            root.GlobalPosition = headPos + forward * 0.8f;
            root.GlobalRotation = floatQ.LookRotation(forward, float3.Up);

            StartStreaming(root, hwnd, title);
        }
        catch (Exception ex)
        {
            Msg($"ERROR in SpawnStreaming: {ex}");
        }
    }

    private static void StartStreaming(Slot root, IntPtr hwnd, string title)
    {
        Msg($"[StartStreaming] Window: {title} (hwnd={hwnd})");

        var streamer = new DesktopStreamer(hwnd);
        if (!streamer.TryInitialCapture())
        {
            Msg($"[StartStreaming] Failed initial capture for: {title}");
            streamer.Dispose();
            return;
        }

        int fps = Config!.GetValue(FrameRate);
        int w = streamer.Width;
        int h = streamer.Height;

        Msg($"[StartStreaming] Window size: {w}x{h}, target {fps}fps");

        // SolidColorTexture as our procedural texture host
        var texSlot = root.AddSlot("Texture");
        var procTex = texSlot.AttachComponent<SolidColorTexture>();
        procTex.Size.Value = new int2(w, h);
        procTex.Format.Value = Renderite.Shared.TextureFormat.RGBA32;
        procTex.Mipmaps.Value = false;
        procTex.FilterMode.Value = Renderite.Shared.TextureFilterMode.Bilinear;

        // Canvas with RawImage pointing at the texture
        float canvasScale = 0.001f;
        var ui = new UIBuilder(root, w, h, canvasScale);
        var rawImage = ui.RawImage(procTex);

        // Opaque material so the texture isn't transparent
        var mat = root.AttachComponent<UI_UnlitMaterial>();
        mat.BlendMode.Value = BlendMode.Opaque;
        rawImage.Material.Target = mat;

        // Attach Button to the RawImage's slot for touch input
        var btn = rawImage.Slot.AttachComponent<Button>();
        btn.PassThroughHorizontalMovement.Value = false;
        btn.PassThroughVerticalMovement.Value = false;

        btn.LocalPressed += (IButton b, ButtonEventData data) =>
        {
            float u = data.normalizedPressPoint.x;
            float v = 1f - data.normalizedPressPoint.y;
            Msg($"[Click] Down u={u:F3} v={v:F3}");
            WindowInput.SendMouseDown(hwnd, u, v, streamer.Width, streamer.Height);
        };
        btn.LocalPressing += (IButton b, ButtonEventData data) =>
        {
            float u = data.normalizedPressPoint.x;
            float v = 1f - data.normalizedPressPoint.y;
            WindowInput.SendMouseMove(hwnd, u, v, streamer.Width, streamer.Height);
        };
        btn.LocalReleased += (IButton b, ButtonEventData data) =>
        {
            float u = data.normalizedPressPoint.x;
            float v = 1f - data.normalizedPressPoint.y;
            Msg($"[Click] Up u={u:F3} v={v:F3}");
            WindowInput.SendMouseUp(hwnd, u, v, streamer.Width, streamer.Height);
        };

        // Hover: send mouse position to window + handle scroll
        btn.LocalHoverStay += (IButton b, ButtonEventData data) =>
        {
            float hu = data.normalizedPressPoint.x;
            float hv = 1f - data.normalizedPressPoint.y;

            // Send hover position (no focus steal)
            WindowInput.SendHover(hwnd, hu, hv, streamer.Width, streamer.Height);

            // Scroll
            var mouse = root.World.InputInterface.Mouse;
            if (mouse == null) return;
            float scrollY = mouse.ScrollWheelDelta.Value.y;
            if (scrollY == 0) return;
            int clicks = scrollY > 0 ? 1 : -1;
            WindowInput.SendScroll(hwnd, hu, hv, streamer.Width, streamer.Height, clicks);
        };

        // Keyboard toggle button — below the canvas (positions in canvas pixel space since root is scaled)
        var kbBtnSlot = root.AddSlot("KeyboardButton");
        kbBtnSlot.LocalPosition = new float3(0f, -(h / 2f) - 30f, 0f);
        kbBtnSlot.LocalScale = float3.One; // don't double-scale
        var kbCanvas = kbBtnSlot.AttachComponent<Canvas>();
        kbCanvas.Size.Value = new float2(200, 40);
        var kbUi = new UIBuilder(kbCanvas);
        var kbBtn = kbUi.Button("Keyboard");
        Slot keyboardSlot = null;
        kbBtn.LocalPressed += (IButton b, ButtonEventData d) =>
        {
            if (keyboardSlot != null && !keyboardSlot.IsDestroyed)
            {
                keyboardSlot.ActiveSelf = !keyboardSlot.ActiveSelf;
                return;
            }
            keyboardSlot = root.AddSlot("Virtual Keyboard");
            keyboardSlot.LocalPosition = new float3(0f, -(h / 2f) - 130f, 0f);
            keyboardSlot.AttachComponent<SimpleVirtualKeyboard>();
        };

        root.AttachComponent<Grabbable>().Scalable.Value = false;
        root.PersistentSelf = false;
        root.Name = $"Desktop: {title}";

        var session = new DesktopSession
        {
            Streamer = streamer,
            Texture = procTex,
            Canvas = ui.Canvas,
            Root = root,
            TargetInterval = 1.0 / fps,
        };
        ActiveSessions.Add(session);

        // Start update loop in this world
        Msg("[StartStreaming] Scheduling update loop...");
        ScheduleUpdate(root.World);
        Msg("[StartStreaming] Update loop scheduled.");

        Msg($"[StartStreaming] Streaming started for: {title}");
    }

    private static readonly HashSet<World> _scheduledWorlds = new();

    internal static void ScheduleUpdate(World world)
    {
        if (_scheduledWorlds.Contains(world)) return;
        _scheduledWorlds.Add(world);
        world.RunInUpdates(1, () => UpdateLoop(world));
    }

    private static int _updateCount;

    // Cached reflection — looked up once, used every frame
    private static readonly PropertyInfo _tex2DProp = typeof(ProceduralTextureBase)
        .GetProperty("tex2D", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly MethodInfo _setFromBitmapMethod = typeof(ProceduralTextureBase)
        .GetMethod("SetFromCurrentBitmap", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly object[] _uploadArgs = new object[] { new Renderite.Shared.TextureUploadHint(), null };

    private static readonly Stopwatch _perfSw = new();

    private static void UpdateLoop(World world)
    {
        _updateCount++;
        double dt = world.Time.Delta;

        if (world.IsDestroyed)
        {
            _scheduledWorlds.Remove(world);
            return;
        }

        try
        {
            for (int i = ActiveSessions.Count - 1; i >= 0; i--)
            {
                var session = ActiveSessions[i];

                if (session.Root == null || session.Root.IsDestroyed ||
                    session.Texture == null || session.Texture.IsDestroyed)
                {
                    session.Streamer?.Dispose();
                    ActiveSessions.RemoveAt(i);
                    continue;
                }

                if (session.Root.World != world) continue;
                if (session.UpdateInProgress) continue;

                // Window closed — destroy viewer
                if (!session.Streamer.IsValid)
                {
                    Msg($"[UpdateLoop] Window closed (IsValid=false), destroying viewer");
                    session.Streamer.Dispose();
                    session.Root.Destroy();
                    ActiveSessions.RemoveAt(i);
                    continue;
                }

                // Throttle to target FPS using engine time
                session.TimeSinceLastCapture += dt;
                if (session.TimeSinceLastCapture < session.TargetInterval)
                    continue;
                session.TimeSinceLastCapture = 0;

                // Wait for the asset to be created by the first normal update
                if (!session.Texture.IsAssetAvailable)
                {
                    if (_updateCount <= 5) Msg("[UpdateLoop] Asset not available yet, waiting...");
                    continue;
                }

                // Switch to manual mode after first update so we control the data
                if (!session.ManualModeSet)
                {
                    session.Texture.LocalManualUpdate = true;
                    session.ManualModeSet = true;
                    Msg("[UpdateLoop] Set LocalManualUpdate = true");
                }

                // Get frame
                var frame = session.Streamer.CaptureFrame(out int w, out int h);
                if (frame == null) continue;

                // Window resized — update texture + canvas size, reset manual mode so bitmap gets recreated
                if (session.Texture.Size.Value.x != w || session.Texture.Size.Value.y != h)
                {
                    Msg($"[UpdateLoop] Resize {session.Texture.Size.Value.x}x{session.Texture.Size.Value.y} -> {w}x{h}");
                    session.Texture.Size.Value = new int2(w, h);
                    if (session.Canvas != null)
                        session.Canvas.Size.Value = new float2(w, h);
                    session.Texture.LocalManualUpdate = false;
                    session.ManualModeSet = false;
                    continue; // Skip this frame, let texture recreate
                }

                var bitmap = _tex2DProp?.GetValue(session.Texture) as Bitmap2D;
                if (bitmap == null || bitmap.Size.x != w || bitmap.Size.y != h)
                {
                    if (_updateCount <= 10) Msg($"[UpdateLoop] Bitmap null or size mismatch, waiting...");
                    continue;
                }

                _perfSw.Restart();

                // WGC: already BGRA + Y-flipped from callback, straight memcpy
                frame.AsSpan(0, w * h * 4).CopyTo(bitmap.RawData);

                _setFromBitmapMethod?.Invoke(session.Texture, _uploadArgs);

                _perfSw.Stop();
                if (_updateCount <= 5 || _updateCount % 300 == 0)
                {
                    Msg($"[UpdateLoop] tick #{_updateCount}, sessions={ActiveSessions.Count}, " +
                        $"captured={session.Streamer.FramesCaptured}, {w}x{h}, " +
                        $"copy+upload={_perfSw.Elapsed.TotalMilliseconds:F1}ms, wgc={session.Streamer.UsingWgc}");
                }
            }
        }
        catch (Exception ex)
        {
            Msg($"ERROR in UpdateLoop: {ex}");
        }

        // Check if any sessions left for this world
        bool hasSessionsInWorld = ActiveSessions.Any(s => s.Root?.World == world);
        if (hasSessionsInWorld)
        {
            world.RunInUpdates(1, () => UpdateLoop(world));
        }
        else
        {
            _scheduledWorlds.Remove(world);
        }
    }

    private static void HandleTouch(IntPtr hwnd, int clientW, int clientH, Slot canvasSlot, float canvasScale, in TouchEventInfo info)
    {
        // Convert world-space touch point to canvas UV
        var localPoint = canvasSlot.GlobalPointToLocal(info.point);
        float canvasW = clientW * canvasScale;
        float canvasH = clientH * canvasScale;

        // Canvas is centered at origin, size = canvasW x canvasH
        float u = (localPoint.x / canvasW) + 0.5f;
        float v = 1f - ((localPoint.y / canvasH) + 0.5f);
        u = MathX.Clamp(u, 0f, 1f);
        v = MathX.Clamp(v, 0f, 1f);

        if (info.touch == EventState.Begin)
        {
            WindowInput.SendMouseDown(hwnd, u, v, clientW, clientH);
        }
        else if (info.touch == EventState.Stay)
        {
            WindowInput.SendMouseMove(hwnd, u, v, clientW, clientH);
        }
        else if (info.touch == EventState.End)
        {
            WindowInput.SendMouseUp(hwnd, u, v, clientW, clientH);
        }
    }

    internal new static void Msg(string msg) => ResoniteMod.Msg(msg);
    internal new static void Error(string msg) => ResoniteMod.Error(msg);
}

internal class DesktopSession
{
    public DesktopStreamer Streamer;
    public SolidColorTexture Texture;
    public Canvas Canvas;
    public Slot Root;
    public bool UpdateInProgress;
    public bool ManualModeSet;
    public double TimeSinceLastCapture;
    public double TargetInterval;
}
