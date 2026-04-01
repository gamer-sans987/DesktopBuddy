# DesktopBuddy - Resonite Mod

## Project
Resonite mod that spawns a world-space desktop/window viewer with touch input. Uses context menu patch to add "Spawn Desktop" option.

## Build & Deploy
```
dotnet build DesktopBuddy/DesktopBuddy.csproj
# Copy to: C:\Program Files (x86)\Steam\steamapps\common\Resonite\rml_mods\
# Kill Resonite first if DLL locked: taskkill /F /IM Renderite.Host.exe
```

## Architecture
- No custom components (engine doesn't support mod component types properly)
- Uses `SolidColorTexture` (built-in ProceduralTexture) + `SetFromCurrentBitmap` for direct GPU texture upload
- Capture thread → frame buffer → `World.RunInUpdates` update loop → copy into shared-memory Bitmap2D → upload
- Context menu via Harmony patch on `ContextMenu.OpenMenu`
- Window picker via `UIBuilder` with `Button.LocalPressed` events (NOT synced delegates — lambdas crash synced delegates)

## Decompiled Source Reference
Full decompiled docs in `docs/` folder. Originals at `/tmp/decompiled/*.cs`.

### Key Patterns Learned

**Custom components DON'T work from mods.** `WorkerInitializer.IsValidField` requires `readonly` fields, but the engine's JIT/reflection breaks for cross-assembly types. Use built-in components + Harmony patches + static state instead.

**Sync member fields MUST be `readonly`.** `IsValidField` checks `field.IsInitOnly`.

**Button events for mod code:** Use `button.LocalPressed +=` (C# event), NOT the `ButtonEventHandler action` parameter on `AddItem`/`UIBuilder.Button` (those assign to synced delegates which reject non-data-model lambdas).

**Texture update pipeline (ProceduralTexture):**
1. `SolidColorTexture` creates `Texture2D` asset + shared-memory `Bitmap2D` on first update
2. Set `LocalManualUpdate = true` after first update to stop auto-overwrite
3. Access `tex2D` via reflection: `typeof(ProceduralTextureBase).GetProperty("tex2D", NonPublic|Instance)`
4. Copy frame data into `bitmap.RawData` (must use shared memory allocator from `Engine.RenderSystem`)
5. Call `SetFromCurrentBitmap(hint, null)` via reflection to upload to GPU

**`Bitmap2D` shared memory requirement:** When rendering is active, `Bitmap2D` buffer must use shared memory. Get allocator from `Engine.RenderSystem` (which implements `IBackingBufferAllocator`). The `ProceduralTextureBase` uses `base.Allocator` which returns `Engine.RenderSystem`.

**UIX Canvas interaction pipeline:**
```
Canvas.OnTouch → ProcessTouchEvent → hit-test RectTransform tree 
→ GetComponentInParents<IUIInteractable> → ProcessEvent(InteractionData)
→ InteractionElement handles hover/press → Button fires LocalPressed
```

**DesktopTextureProvider (built-in desktop capture):**
- Userspace only: `if (World != Userspace.UserspaceWorld) return`
- Uses `DynamicRendererAsset<DesktopTexture>` which talks directly to the renderer
- Sends `SetDesktopTextureProperties` message to render system
- Cannot be used in world space — that's why this mod exists

**DesktopInteractionRelay (built-in desktop input):**
- Implements `IUIInteractable` — receives `ProcessEvent(Canvas.InteractionData)`
- Maps canvas coordinates to display pixels via `Display.Rect.GetPoint(normalized)`
- Injects OS pointer via `InputInterface.InjectTouch()`
- Also userspace only

**RawImage vs Image:**
- `RawImage.Texture` takes `IAssetProvider<ITexture2D>` — use for dynamic textures
- `RawImage.Material` takes `IAssetProvider<Material>` — optional override
- `Image.Sprite` takes `IAssetProvider<Sprite>` — use for static UI graphics
- `Image` with null sprite + color tint = solid color background

**FFmpeg streaming (alternative approach, ~2s latency):**
- `gdigrab` captures desktop directly, NVENC H.264 encodes, MPEG-TS output
- Serve via HTTP, use `VideoTextureProvider` with `Stream=true`
- libVLC handles playback. Supported formats: mp4 mpeg avi mov mkv flv webm
- `-bf 0 -flush_packets 1 -muxdelay 0` required for live streaming
- Latency from libVLC input buffer, not controllable from mod side

**Context menu patching:**
```csharp
[HarmonyPatch(typeof(ContextMenu), nameof(ContextMenu.OpenMenu))]
// In Postfix: use AddItem(label, uri, color) then item.Button.LocalPressed += handler
```

**Texture provider hierarchy:**
```
AssetProvider<A>
  ├─ DynamicAssetProvider<A> → ProceduralAssetProvider<A> → ProceduralTextureBase → ProceduralTexture → SolidColorTexture
  ├─ StaticTextureProvider → StaticTexture2D (URL-based, disk save)
  ├─ DesktopTextureProvider (userspace only, renderer-owned)
  └─ VideoTextureProvider (libVLC/Unity, URL-based streaming)
```

**ProceduralAssetProvider update pipeline:**
```
OnChanges → RefreshAssetState → UpdateAsset (if refs > 0)
→ DynamicAssetProvider.RunAssetUpdate: creates Texture2D asset if null
→ ProceduralAssetProvider.UpdateAsset: RequestWriteLock → background thread
→ UpdateAssetData (SolidColorTexture fills bitmap) → FinishAssetUpdate
→ UploadAssetData → SetFromCurrentBitmap → Texture2D.SetFromBitmap2D (GPU upload)
→ AssetIntegrated callback → AssetCreated/AssetUpdated notifications
```

**ProceduralAssetProvider.Allocator:** Returns `Engine.RenderSystem` (implements `IBackingBufferAllocator`). This is what provides shared memory for Bitmap2D. Without it, `SetFromBitmap2D` throws "Bitmap Buffer must use shared memory".

**DynamicAssetProvider.LocalManualUpdate:** When true, `UpdateAsset()` skips. Call `RunManualUpdate()` to trigger manually. The asset must exist first (created on first normal update).

**ProceduralAssetProvider error is sticky:** Once `_error` is set from an exception in `UpdateAssetData`, no more updates run. Must recreate the component.

## API Reference (`docs/api-reference/`)
01. Worker, Component, ComponentBase, ContainerWorker, WorkerInitializer
02. Sync data model: SyncField, SyncRef, SyncList, drivers, RefID
03. World, Engine, WorldManager, User, UserRoot, Userspace
04. Texture providers: AssetProvider chain, ProceduralTexture, StaticTexture2D, DesktopTextureProvider, VideoTextureProvider
05. Rendering: MeshRenderer, materials, meshes, lights, Camera, RenderSystem
06. UIX interaction: Canvas, RawImage, Image, Button, UIBuilder, InteractionElement, DesktopInteractionRelay
07. Physics: colliders, CharacterController, TouchSource, InteractionHandler, DevTool
08. Audio, animation, avatar, DynamicBoneChain
09. Tools, inspectors, utility, managers
10. Slot, SlotMeshes, TypeManager, GlobalTypeRegistry
11. Elements.Core: math, vectors, colors, animation, serialization, threading
12. Elements.Assets, SkyFrost.Base cloud API, FrooxEngine.Store, Commands
13. ProtoFlux runtime + 2575 core nodes + FrooxEngine nodes
14. Renderite.Shared IPC/render, Awwdio spatial audio/DSP
15-27. Full FrooxEngine.dll class-by-class (all 6209 types, chunked)

### Decompiled Sources
- `docs/decompiled_full/FrooxEngine.full.cs` — 585,342 lines, 6209 types
- `docs/decompiled_full/Elements.Core.decompiled.cs` — 267,335 lines (math, color, vectors, animation, serialization)
- `docs/decompiled_full/Elements.Assets.decompiled.cs` — Bitmap2D, MeshX, AudioX, asset metadata
- `docs/decompiled_full/SkyFrost.Base.decompiled.cs` — Cloud API, records, storage, variables
- `docs/decompiled_full/FrooxEngine.Store.decompiled.cs` — LocalDB, asset records
- `docs/decompiled_full/ProtoFlux.Core.decompiled.cs` — Execution runtime, node base classes
- `docs/decompiled_full/ProtoFlux.Nodes.Core.decompiled.cs` — 2575 platform-independent nodes
- `docs/decompiled_full/ProtoFlux.Nodes.FrooxEngine.decompiled.cs` — Resonite-specific nodes
- `docs/decompiled_full/ProtoFluxBindings.decompiled.cs` — 22MB auto-generated bindings
- `docs/decompiled_full/Renderite.Shared.decompiled.cs` — IPC, shared memory, render commands
- `docs/decompiled_full/Awwdio.decompiled.cs` — Audio simulation, spatial audio, DSP
- `docs/decompiled/*.cs` — 133 individually decompiled key types

## Files
- `DesktopBuddyMod.cs` — Main mod, context menu patch, window picker UI, streaming setup, update loop
- `DesktopStreamer.cs` — Background capture thread, frame buffer with TakeFrame()
- `WindowCapture.cs` — Win32 BitBlt/GetDC capture, BGRA→RGBA conversion, desktop support (hwnd=0)
- `WindowEnumerator.cs` — EnumWindows to list visible windows
- `WindowInput.cs` — Win32 mouse/touch injection (SetCursorPos, mouse_event, InjectTouchInput)
- `ContextMenuPatch.cs` — Harmony postfix on ContextMenu.OpenMenu
- `MjpegServer.cs` — HTTP server + FFmpeg gdigrab streaming (commented out, kept for future remote user support)
