# FrooxEngine Texture Provider Hierarchy

## Inheritance Chain Overview

```
Component
  └─ AssetProvider<A>
       ├─ DynamicAssetProvider<A>
       │    └─ ProceduralAssetProvider<A>
       │         └─ ProceduralTextureBase          (A = Texture2D)
       │              └─ ProceduralTexture          (not shown, adds Size/Mipmaps/Format syncs)
       │                   └─ SolidColorTexture
       ├─ StaticTextureProvider<A,B,M,D>           (abstract, URL-based)
       │    └─ StaticTexture2D                     (A=Texture2D, B=Bitmap2D)
       ├─ DesktopTextureProvider                   (A = DesktopTexture)
       └─ VideoTextureProvider                     (A = VideoTexture)
```

Asset classes (non-Component):
```
DynamicRendererAsset<T>
  └─ DesktopTexture    (implements ITexture2D)

Texture<...>
  └─ Texture2D         (implements ITexture2D, wraps Bitmap2D)

VideoTexture           (implements ITexture2D, not shown)
```

---

## 1. AssetProvider\<A\>

**Base:** `Component`
**Interfaces:** `IAssetProvider<A>`, `ICustomInspector`
**Constraint:** `A : Asset, new()`

The root base class for all asset-providing components. Manages reference counting and lazy load/unload lifecycle.

### Key Fields
- `HashSet<IAssetRef> references` -- tracks all `AssetRef<>` fields pointing to this provider
- `HashSet<IAssetRef> updateListeners` -- listeners notified on asset content change (not just create/remove)

### Key Properties
- `abstract A Asset { get; }` -- the loaded asset instance (null if unloaded)
- `abstract bool IsAssetAvailable { get; }` -- whether asset is loaded
- `virtual bool AlwaysLoad` -- if true, asset stays loaded even with 0 references
- `virtual bool ForceUnload` -- if true, asset is always freed (used by DesktopTextureProvider when disabled)

### Lifecycle (RefreshAssetState, called from OnChanges)
1. If `ForceUnload` -> `FreeAsset()`
2. If `AlwaysLoad` -> `UpdateAsset()`
3. If 0 references and loaded -> schedule `TryFreeAsset()` in 8 updates (grace period)
4. If >0 references -> `UpdateAsset()`

### Key Methods
- `abstract UpdateAsset()` -- derived classes implement to create/update their asset
- `abstract FreeAsset()` -- derived classes implement to destroy their asset
- `AssetCreated()` / `AssetUpdated()` / `AssetRemoved()` -- queue notifications to referencing components via `AssetManager`
- `ProcessURL(Uri)` -- validates and migrates legacy URLs; returns `null` for unsupported/invalid URIs

### Gotcha
- `ProcessURL` returns `null` for unsupported schemes or invalid DB URIs. Many providers check this before loading.

---

## 2. DynamicAssetProvider\<A\>

**Base:** `AssetProvider<A>`

Adds a concrete `_asset` field and the concept of manual vs automatic updates.

### Key Fields
- `A _asset` -- the single asset instance
- `Sync<bool> HighPriorityIntegration` -- if true, render integration gets priority scheduling
- `bool LocalManualUpdate` -- when true, `UpdateAsset()` is a no-op; caller must invoke `RunManualUpdate()` explicitly

### Update Flow
`UpdateAsset()` -> `RunAssetUpdate()`:
1. Creates `_asset` if null, calls `InitializeDynamic` + `SetOwner` + `AssetCreated(asset)`
2. Sets `HighPriorityIntegration` on asset
3. Calls `abstract UpdateAsset(A asset)` (the derived-class hook)

### Abstract Hooks
- `AssetCreated(A asset)` -- called once when asset is first created
- `UpdateAsset(A asset)` -- called on every update cycle
- `ClearAsset()` -- called when asset is freed, for derived cleanup

---

## 3. ProceduralAssetProvider\<A\>

**Base:** `DynamicAssetProvider<A>`

Adds thread-safe background asset generation with write-lock coordination.

### Key Fields
- `SpinLock updateLock` -- guards `updateRunning`/`runUpdate` flags
- `volatile bool updateRunning` / `runUpdate` -- coalesces rapid updates
- `int _updateCount` -- incremented after each successful integration
- `bool _error` -- sticky error flag, stops further updates

### Property
- `Allocator` -- returns `Engine.RenderSystem` as `IBackingBufferAllocator` (null if no renderer). **This is the shared memory allocator required for GPU-uploadable Bitmap2Ds.**

### Update Pipeline
```
UpdateAsset(A asset)
  -> PrepareAssetUpdateData()     [main thread, snapshot sync fields]
  -> asset.RequestWriteLock()
  -> WriteLockGranted()
     -> (background thread or async)
        -> UpdateAssetData(A) or UpdateAssetDataAsync(A)   [ABSTRACT - do work here]
     -> FinishAssetUpdate()
        -> asset.ReleaseWriteLock()
        -> UploadAssetData(integratedCallback)             [ABSTRACT - send to GPU]
           -> AssetIntegrated callback
              -> AssetIntegrationUpdated()
              -> OnAssetIntegrated()
              -> _updateCount++
              -> if runUpdate was set, MarkChangeDirty() to re-run
```

### Abstract Methods
- `PrepareAssetUpdateData()` -- snapshot data from Sync fields (main thread)
- `UpdateAssetData(A)` / `UpdateAssetDataAsync(A)` -- generate asset data (background thread)
- `UploadAssetData(AssetIntegrated)` -- send data to renderer
- `GenerateErrorIndication()` -- called on exception

### Gotchas
- The `SpinLock` coalesces updates: if a change arrives while an update is running, it sets `runUpdate=true` and re-triggers after integration completes.
- `_error` is sticky -- once set, no more updates run. Only way to recover is recreation.
- `FreeAsset()` is also guarded by the lock -- if an update is running, it defers to `runUpdate`.

---

## 4. ProceduralTextureBase

**Base:** `ProceduralAssetProvider<Texture2D>`
**Interfaces:** `ITexture2DProvider`

The base for all procedurally-generated 2D textures.

### Key Sync Fields
- `Sync<TextureFilterMode> FilterMode` (default: Bilinear)
- `Sync<int> AnisotropicLevel` (default: 8)
- `Sync<TextureWrapMode> WrapModeU`, `WrapModeV` (default: Repeat)
- `Sync<float> MipmapBias` (range -1..1)
- `Sync<ColorProfile> Profile` (default: sRGB)

### Key Properties
- `TextureUploadHint uploadHint` -- mutable struct, `region` field for partial uploads
- `Bitmap2D tex2D` -- the CPU-side bitmap being generated
- `abstract int2 GenerateSize` -- size to allocate
- `abstract bool GenerateMipmaps`
- `abstract TextureFormat GenerateFormat`
- `virtual bool AutoGenerateMipmaps` (default: true)

### Texture Update Pipeline (implements ProceduralAssetProvider's hooks)
```
UpdateAssetData(Texture2D asset)
  -> PrepareBitmap()
     - Allocates new Bitmap2D if size/format/profile changed
     - Uses `base.Allocator` (shared memory!) for the buffer
     - Clears raw data if allocator is present
     - Resets uploadHint.region to null (full upload)
  -> UpdateTextureData(Bitmap2D)   [ABSTRACT - derived classes fill pixels here]
  -> PostprocessTexture()
     - Generates mipmaps if HasMipMaps && AutoGenerateMipmaps

UploadAssetData(AssetIntegrated callback)
  -> SetFromCurrentBitmap(uploadHint, callback)
     -> Asset.SetFromBitmap2D(tex2D, hint, FilterMode, Aniso, WrapU, WrapV, MipmapBias, callback)
```

### BakeTexture
Can convert a procedural texture into a `StaticTexture2D` by saving bitmap data to LocalDB and replacing all references.

### Gotcha: Bitmap2D Allocation
- `PrepareBitmap()` creates `Bitmap2D` with `base.Allocator` (the render system's shared memory).
- **If rendering is active, the bitmap buffer MUST be a `SharedMemoryBlockLease<byte>`** -- `Texture2D.SetFromBitmap2D` enforces this with an exception.
- Minimum dimension is 4 pixels per axis.
- `GenerateFormat` must support write (`SupportsWrite()`).

---

## 5. SolidColorTexture

**Base:** `ProceduralTexture` (which extends `ProceduralTextureBase`)
**Category:** `Assets/Procedural Textures`

Simplest procedural texture -- fills every pixel with a single color.

### Key Fields
- `Sync<colorX> Color` (default: Black)
- Inherits `Size` (default: 4x4), `Mipmaps`, `Format` from `ProceduralTexture`

### Pipeline
- `PrepareAssetUpdateData()` -- converts `Color.Value` to the current `Profile` color space
- `UpdateTextureData(Bitmap2D)` -- nested loop setting every pixel to the converted color

---

## 6. Texture2D (Asset Class)

**Base:** `Texture<Texture2D, Texture2DVariantDescriptor, ...>`
**Interfaces:** `ITexture2D`, `IRendererAsset`

The actual GPU-side texture asset. Not a Component -- this is owned by providers.

### Key Fields
- `Bitmap2D _data` -- CPU-side data (disposed on reassignment)
- `int2 Size`, `int MipMapCount`
- `AssetIntegrated _assetIntegrated` -- callback for when GPU integration completes
- `TextureUpdateResultType FireIntegratedOnResultType` -- bitmask tracking which GPU operations are pending

### Critical Method: SetFromBitmap2D
```csharp
void SetFromBitmap2D(Bitmap2D bitmap, TextureUploadHint hint,
    TextureFilterMode, int anisoLevel, TextureWrapMode wrapU/V,
    float mipmapBias, AssetIntegrated onLoaded)
```
1. **Validates** bitmap buffer is `SharedMemoryBlockLease<byte>` when renderer is active (throws otherwise)
2. Stores bitmap as `Data`
3. Sends three render commands: `SetTexture2DProperties`, `SetTexture2DFormat`, `SetTexture2DData`
4. Uses shared memory descriptor for zero-copy GPU upload
5. Calls `onLoaded` callback when both FormatSet and DataUpload complete

### HandleResult
Called by render system when GPU operations finish. Uses atomic operations on `_fireOnIntegratedResultTypeFlags` to track completion of multiple pending operations, fires `_assetIntegrated` only when all pending ops are done.

### EnsureRendererCompatible
- Rescales textures exceeding `RenderSystem.MaxTextureSize`
- Flips Y if needed
- Converts incompatible formats

### Gotcha
- `Data` setter disposes the old buffer when replacing -- do not hold references to old `Bitmap2D`.
- `AssignUploadLayout` casts the buffer to `SharedMemoryBlockLease<byte>` -- will crash if bitmap wasn't allocated with shared memory.

---

## 7. DesktopTextureProvider (Component)

**Base:** `AssetProvider<DesktopTexture>`
**Interfaces:** `ITexture2DProvider`
**Category:** `Assets`

Provides a live desktop capture texture. **Userspace-only.**

### Key Fields
- `Sync<int> DisplayIndex` -- which monitor to capture
- `DesktopTexture _desktopTex` -- the asset instance
- `bool _created` -- set after first successful texture creation

### ForceUnload Override
Returns `true` when slot is inactive OR component is disabled. This means the desktop texture is freed immediately when the component/slot is deactivated (no grace period).

### UpdateAsset Flow
1. **Guard: `World != Userspace.UserspaceWorld`** -- silently returns if not in Userspace. This texture cannot work in regular worlds.
2. **Guard: `Engine.Config.DisableDesktop`** -- returns if desktop access is disabled
3. Creates `DesktopTexture` if null, calls `InitializeDynamic`
4. Calls `_desktopTex.Update(DisplayIndex, OnTextureCreated)`
5. On callback: sets `_created = true`, calls `AssetCreated()`

### Key Pattern
Does NOT use `DynamicAssetProvider` or `ProceduralAssetProvider`. Directly extends `AssetProvider<DesktopTexture>` and manually manages the asset lifecycle. This is because the desktop texture is entirely renderer-managed (no CPU-side Bitmap2D).

---

## 8. DesktopTexture (Asset Class)

**Base:** `DynamicRendererAsset<DesktopTexture>`
**Interfaces:** `ITexture2D`

The actual desktop capture asset. Entirely renderer-side -- no CPU bitmap data.

### Key Properties
- `int2 Size` -- updated from renderer callback
- `bool HasAlpha` -- always `false`
- `Manager` -> `RenderSystem.DesktopTextures`

### Update Flow
`Update(int index, Action onUpdated)`:
1. If no renderer (Manager == null), immediately calls `onUpdated()` and returns
2. Sends `SetDesktopTextureProperties` with `displayIndex` to render system
3. Stores `onUpdated` callback

`HandlePropertiesUpdate(DesktopTexturePropertiesUpdate update)`:
- Increments `Version`
- Updates `Size` from the renderer
- Calls stored `onUpdated` callback

### Unload
Sends `UnloadDesktopTexture` command to render system.

### Key Pattern: DynamicRendererAsset
Unlike `Texture2D`, there is no CPU-side `Bitmap2D`. The render system owns the texture data entirely. The component just tells the renderer which display to capture, and the renderer updates the texture directly.

---

## 9. VideoTextureProvider

**Base:** `AssetProvider<VideoTexture>`
**Interfaces:** `ITexture2DProvider`, `IPlayable`, `IWorldAudioDataSource`, `IStaticAssetProvider`
**Category:** `Assets`

Loads and plays video from URLs (local files, cloud assets, streaming services).

### Key Sync Fields
- `SyncPlayback Playback` -- play/pause/seek/loop/speed state
- `Sync<Uri> URL` -- the video source
- `Sync<bool> Stream` -- force streaming mode
- `Sync<float> Volume` (default: 1)
- `Sync<string> ForcePlaybackEngine` -- "Unity" or "libVLC" or null (auto)
- `Sync<bool> ForceVideoStreamingServiceParsing`
- `RawOutput<string> VideoTitle`, `CurrentPlaybackEngine`
- Texture properties: `FilterMode`, `AnisotropicLevel`, `WrapModeU/V`
- `Sync<int?> AudioTrackIndex`, `Sync<bool> PreferAudioOnly`
- `Sync<int?> MaxWidth`, `MaxHeight` -- cap resolution for streaming service formats

### Three Loading Paths
1. **Video streaming service** (YouTube, etc.) -- uses `yt-dlp` to resolve format URLs, selects best format by codec/resolution, prefers h264+mp4a for Unity, falls back to libVLC
2. **Local/cloud asset** -- gathers file via `AssetManager`, detects MIME, loads via playback engine
3. **Stream URL** -- loads directly from HTTP/RTSP URL

### yt-dlp Integration
- Auto-updates `yt-dlp` on first use (with corruption recovery)
- Format selection ranks by: has audio > has video > resolution <= max > codec quality (h264/mp4a preferred)
- Falls back to libVLC for non-h264/mp4a codecs

### Audio
Implements `IAudioDataSource.Read<S>()` -- delegates to `VideoTexture.AudioRead()`.

---

## 10. StaticTexture2D

**Base:** `StaticTextureProvider<Texture2D, Bitmap2D, BitmapMetadata, Texture2DVariantDescriptor>`
**Category:** `Assets`

The standard URL-based texture component. Loads textures from cloud/local URIs with automatic variant selection (compression, resizing, mipmap generation).

### Key Sync Fields (in addition to inherited)
- `Sync<bool> IsNormalMap` -- uses BC3nm compression
- `Sync<TextureWrapMode> WrapModeU`, `WrapModeV`
- `Sync<float> PowerOfTwoAlignThreshold` (default: 0.05)
- `Sync<bool> CrunchCompressed` (default: true)
- `Sync<int?> MinSize`, `MaxSize`
- `Sync<bool> MipMaps` (default: true)
- `Sync<bool> KeepOriginalMipMaps`
- `Sync<Filtering> MipMapFilter` (default: Box)
- `Sync<bool> Readable` -- keeps CPU data after GPU upload

### Inherited from StaticTextureProvider
- `Sync<TextureFilterMode?> FilterMode` -- nullable, falls back to engine default
- `Sync<int?> AnisotropicLevel` -- nullable, falls back to engine default
- `Sync<bool> Uncompressed`, `DirectLoad`, `ForceExactVariant`
- `Sync<TextureCompression?> PreferredFormat`
- `Sync<ColorProfile?> PreferredProfile`
- `Sync<float> MipMapBias`

### Variant Descriptor Logic (`UpdateTextureVariantDescriptor`)
Determines GPU format based on platform, metadata, and user settings:
- **Windows/Linux:** BC1/BC3 (crunch or LZMA), BC3nm for normals, BC6H for HDR
- **Android:** ETC2 RGB/RGBA
- Size: capped by `MaxSize`, engine max texture size, `MinSize`; snapped to nearest power-of-two if within threshold; aligned to mip boundaries
- Profile: Linear for normals/HDR, sRGB otherwise

### Texture Processing
Extensive `Process()` / `ProcessBitmap()` API for in-place texture editing:
- Requires `URL.Value != null` (returns false otherwise)
- Loads original texture data, applies transform, saves to LocalDB, updates URL
- Supports undo when triggered from UI buttons
- Operations: flip, rotate, crop, resize, tile, color manipulation, normalize, K-means clustering, etc.

### Gotcha: ProcessBitmap requires URL
`ProcessAsync` returns `false` immediately if `URL.Value == null`. The texture must be backed by a URL to be processed.

---

## Summary: Texture Update Pipelines

### Procedural Textures (SolidColorTexture, etc.)
```
OnChanges -> RefreshAssetState -> UpdateAsset
  -> RunAssetUpdate -> (create asset if needed)
  -> UpdateAsset(Texture2D) [ProceduralAssetProvider]
     -> PrepareAssetUpdateData()         [main thread: snapshot sync fields]
     -> RequestWriteLock -> WriteLockGranted
     -> UpdateAssetData(Texture2D)       [background thread]
        -> PrepareBitmap()               [allocate Bitmap2D w/ shared memory]
        -> UpdateTextureData(Bitmap2D)   [fill pixels]
        -> PostprocessTexture()          [generate mipmaps]
     -> ReleaseWriteLock
     -> UploadAssetData()
        -> SetFromBitmap2D()             [send to GPU via render commands]
        -> AssetIntegrated callback
```

### Desktop Texture
```
OnChanges -> RefreshAssetState -> UpdateAsset
  -> (guard: userspace only)
  -> DesktopTexture.Update(displayIndex, callback)
     -> Send SetDesktopTextureProperties to renderer
     -> Renderer captures desktop, calls HandlePropertiesUpdate
     -> callback -> AssetCreated()
```
No CPU-side bitmap. Entirely renderer-managed.

### Static Texture (StaticTexture2D)
```
OnChanges -> UpdateAsset
  -> ProcessURL(URL) -> UpdateVariantDescriptor
  -> (asset system fetches/compresses variant)
  -> DecodeFile -> EnsureRendererCompatible
  -> InitializeTexture (set format on GPU)
  -> UploadTextureData (send bitmap to GPU via shared memory)
```

### Video Texture
```
OnChanges -> UpdateAsset
  -> Resolve URL (yt-dlp for streaming services, direct for others)
  -> VideoTexture.Load(url, engine, mime, ...)
  -> Renderer handles playback, calls back on load/change
```

---

## Additional Asset Provider Types

### FontAtlasTexture
- **Base:** `AssetProvider<Texture2D>`, `ITexture2DProvider`
- Provides a font glyph atlas as a 2D texture; listens to atlas update/unload events.

### LocalMeshProvider
- **Base:** `LocalAssetProvider<Mesh>`
- Provides a locally-generated mesh from a `MeshX` object; supports async update with task/callback completion.

### LocalPointRenderBufferProvider / LocalTrailRenderBufferProvider
- Provide locally-generated point/trail render buffers for point cloud and trail rendering.

### AssetProxy\<A\>
- Component that acts as an indirect reference to an asset provider; used for reference proxying.

### AssetRef\<A\>
- Sealed class extending `SyncRef<IAssetProvider<A>>`. Manages provider registration/deregistration and asset availability tracking.

### Asset Provider Interfaces

| Interface | Purpose |
|---|---|
| `IAssetProvider` | Non-generic base for any asset-providing component |
| `IAssetProvider<A>` | Generic typed interface for specific asset types |
| `IAssetRef` | Synchronized reference to asset providers |
| `ITexture2DProvider` | Marker for 2D texture providers |
| `ITexture3DProvider` | Marker for 3D texture providers |
| `IStaticAssetProvider` | Components loading assets from a static URL |
