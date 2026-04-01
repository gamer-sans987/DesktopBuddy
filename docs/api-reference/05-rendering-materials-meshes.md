# FrooxEngine Rendering Reference

Covers mesh renderers, materials, procedural meshes, lights, cameras, and the render system.

---

## MeshRenderer

**Category:** Rendering
**Inherits:** `RenderableComponent` -> `Component`
**Implements:** `IBounded`, `IRenderable`, `IHighlightable`, `ICustomInspector`

### Key Fields

| Field | Type | Notes |
|---|---|---|
| `Mesh` | `AssetRef<Mesh>` | The mesh asset to render |
| `Materials` | `SyncAssetList<Material>` | Material list; one per submesh |
| `MaterialPropertyBlocks` | `SyncAssetList<MaterialPropertyBlock>` | Per-renderer property overrides |
| `ShadowCastMode` | `Sync<ShadowCastMode>` | Default: `On` |
| `MotionVectorMode` | `Sync<MotionVectorMode>` | Default: `Object` |
| `SortingOrder` | `Sync<int>` | Render order override |

### Key Properties/Methods

- **`Material`** (get) -- shortcut for `Materials[0]`. Auto-creates the first element if the list is empty.
- **`IsRenderable`** -- true only when `Enabled`, mesh is loaded, and the active user is not rendering-blocked.
- **`IsLoaded`** -- checks both mesh availability and that all `MaterialProvider`s report loaded.
- **`ReplaceAllMaterials(IAssetProvider<Material>)`** -- replaces every material slot.
- **`GetUniqueMaterial(int index = 0)`** -- if the material is shared (referenced elsewhere), duplicates it onto `World.AssetsSlot` and reassigns. Returns the (now unique) provider.
- **`SplitSubmeshes()` / `MergeByMaterial()`** -- async mesh processing. Splits into one renderer per submesh or merges submeshes sharing the same material reference.
- **`GenerateHighlight(Slot root, material, trackPosition)`** -- clones the renderer onto a highlight slot.
- **`LocalBoundingBox` / `GlobalBoundingBox`** -- derived from `Mesh.Asset.Bounds`, transformed by slot.

### Gotchas

- `ShouldBeEnabled` checks `Slot.ActiveUser.IsRenderingLocallyBlocked`. If a user is blocked, their renderers won't show unless `RenderingLocallyUnblocked` is set.
- `MaterialsOrPropertyBlocksChanged` is a bool flag set by change listeners, consumed by the render manager.
- Mesh processing (`SplitSubmeshes`, `MergeByMaterial`) runs as a global task with background thread transitions. The mesh is cloned via `MeshX` before mutation.

---

## SkinnedMeshRenderer

**Category:** Rendering
**Inherits:** `MeshRenderer`

### Additional Fields

| Field | Type | Notes |
|---|---|---|
| `Bones` | `SyncRefList<Slot>` | Slot references for each bone |
| `BlendShapeWeights` | `SyncFieldList<float>` | Range 0-1, one per blendshape |
| `BoundsComputeMethod` | `Sync<SkinnedBounds>` | How bounding box is calculated |
| `ProxyBoundsSource` | `SyncRef<SkinnedMeshRenderer>` | Proxy another renderer's bounds |
| `ExplicitLocalBounds` | `Sync<BoundingBox>` | Manual bounds override |

### Key Properties

- **`MeshBoneCount`** -- from mesh asset data; nullable.
- **`MeshBlendshapeCount`** -- from mesh asset; defaults to 0 if null.
- **`RenderableBlendshapeCount`** -- `Min(MeshBlendshapeCount, BlendShapeWeights.Count)`.
- **`ComputedBounds`** / **`ComputedBoundsSpace`** -- runtime-computed bounding box and its reference space slot.
- **`ComputedRendererRoot`** -- the common ancestor slot of all bones.

### Gotchas

- `IsBoundingBoxAvailable` requires mesh loaded AND `ComputedBounds.IsValid` AND `ComputedBoundsSpace` not removed.
- Bones are tracked via `IncrementRenderable`/`DecrementRenderable` on each bone slot.
- When the mesh changes, blendshape weights are re-synced to match the mesh's blendshape count.
- Internal flags `BonesChanged`, `BlendShapeWeightsChanged`, `UpdateAllBlendshapes` are consumed by `SkinnedMeshRendererManager`.
- `SetupBones()` auto-finds bones by name if Bones list is empty, searching from `ComputedRendererRoot`.

---

## UnlitMaterial

**Category:** Assets/Materials/Unlit
**Inherits:** `MaterialProvider`
**Implements:** `ICommonMaterial`, `IStereoMaterial`, `IBillboardMaterial`, `IBlendModeMaterial`, `ICullingMaterial`

### Key Fields

| Field | Type | Default | Notes |
|---|---|---|---|
| `TintColor` | `Sync<colorX>` | White | Multiplied with texture |
| `Texture` | `AssetRef<ITexture2D>` | null | Main texture |
| `TextureScale` / `TextureOffset` | `Sync<float2>` | (1,1) / (0,0) | UV transform |
| `MaskTexture` | `AssetRef<ITexture2D>` | null | Alpha mask |
| `MaskMode` | `Sync<MaskTextureMode>` | -- | `MultiplyAlpha` or `AlphaClip` |
| `BlendMode` | `Sync<BlendMode>` | Opaque | Alpha, Transparent, Cutout, Additive, Multiply |
| `AlphaCutoff` | `Sync<float>` | 0.5 | For cutout mode |
| `Sidedness` | `Sync<Sidedness>` | Auto | Front/Back/Double |
| `ZWrite` | `Sync<ZWrite>` | -- | Depth write control |
| `UseVertexColors` | `Sync<bool>` | true | Sample vertex color attribute |
| `RenderQueue` | `Sync<int>` | -1 | -1 = automatic |
| `DecodeAsNormalMap` | `Sync<bool>` | false | Reinterpret texture as normal |
| `UseBillboardGeometry` | `Sync<bool>` | false | Switches to billboard shader |
| `OffsetTexture` | `AssetRef<ITexture2D>` | null | Vertex offset |
| `PolarUVmapping` | `Sync<bool>` | false | Polar coordinate UVs |
| `StereoTextureTransform` | `Sync<bool>` | false | Per-eye UV transforms |

### Gotchas

- Shader swaps between `OfficialAssets.Shaders.Unlit` and `OfficialAssets.Shaders.BillboardUnlit` based on `UseBillboardGeometry`.
- Keyword `_MUL_RGB_BY_ALPHA` is enabled when `BlendMode == Additive`.
- The `_COLOR` keyword is only set when `TintColor != White` (optimization).
- `RenderQueue = -1` means the engine picks automatically based on blend mode.
- Legacy color management migration runs on load if the `ColorManagement` feature flag is absent.

---

## PBS_Metallic

**Category:** Assets/Materials
**Inherits:** `PBS_Material` (which inherits `MaterialProvider`)
**Implements:** `IPBS_Metallic`, `IPBS_Material`

### Fields (in addition to PBS_Material base)

| Field | Type | Default | Notes |
|---|---|---|---|
| `Metallic` | `Sync<float>` | 0.0 | Range 0-1 |
| `Smoothness` | `Sync<float>` | 0.25 | Range 0-1 |
| `MetallicMap` | `AssetRef<ITexture2D>` | null | R=metallic, A=smoothness |

### Inherited from PBS_Material (via sync member indices)

`TextureScale`, `TextureOffset`, `DetailTextureScale`, `DetailTextureOffset`, `AlbedoColor`, `AlbedoTexture`, `EmissiveColor`, `EmissiveMap`, `NormalScale`, `NormalMap`, `HeightMap`, `HeightScale`, `OcclusionMap`, `DetailAlbedoTexture`, `DetailNormalMap`, `DetailNormalScale`, `BlendMode`, `AlphaCutoff`, `OffsetFactor`, `OffsetUnits`, `RenderQueue`.

### Gotchas

- `MetallicMap` keyword `_METALLICGLOSSMAP` controls whether the map is sampled.
- Metallic map uses `ColorProfile.sRGB` when uploaded.
- Legacy loading converts `Metallic` with `LegacyLinearFloatColorFieldAdapter`.

---

## Procedural Meshes

All inherit from `ProceduralMesh`. Mesh data is built on a background thread via `PrepareAssetUpdateData()` -> `UpdateMeshData(MeshX)`. The `uploadHint[MeshUploadHint.Flag.Geometry]` flag controls whether topology is re-uploaded or only vertex positions change.

### QuadMesh

| Field | Type | Default | Notes |
|---|---|---|---|
| `Size` | `Sync<float2>` | (1,1) | Width/height |
| `Rotation` | `Sync<floatQ>` | Identity | Orientation |
| `UVScale` / `UVOffset` | `Sync<float2>` | (1,1)/(0,0) | UV transform |
| `ScaleUVWithSize` | `Sync<bool>` | false | Multiplies UV scale by size |
| `DualSided` | `Sync<bool>` | false | Adds a back-face quad |
| `UseVertexColors` | `Sync<bool>` | true | Enable vertex colors |
| `UpperLeftColor` / `LowerLeftColor` / `LowerRightColor` / `UpperRightColor` | `Sync<colorX>` | White | Per-corner colors |

- **`Facing`** property: gets/sets `Rotation` as a look direction (uses `float3.Backward`).
- **`Color`** setter: sets all four corner colors at once.

### BoxMesh

| Field | Type | Default |
|---|---|---|
| `Size` | `Sync<float3>` | (1,1,1) |
| `UVScale` | `Sync<float3>` | (1,1,1) |
| `ScaleUVWithSize` | `Sync<bool>` | false |

- `CreateCollider()` attaches a `BoxCollider` driven from `Size`.

### SphereMesh

| Field | Type | Default |
|---|---|---|
| `Radius` | `Sync<float>` | 0.5 |
| `Segments` | `Sync<int>` | 32 (clamped 3-4096) |
| `Rings` | `Sync<int>` | 16 (clamped 3-4096) |
| `Shading` | `Sync<UVSphereCapsule.Shading>` | -- |
| `UVScale` | `Sync<float2>` | (1,1) |
| `DualSided` | `Sync<bool>` | false |

- `CreateCollider()` attaches a `SphereCollider` driven from `Radius`.
- Topology is rebuilt if segments, rings, or shading change.

### CylinderMesh

| Field | Type | Default |
|---|---|---|
| `Height` | `Sync<float>` | 1.0 |
| `Radius` | `Sync<float>` | 0.5 |
| `Sides` | `Sync<int>` | 16 (min 3) |
| `Caps` | `Sync<bool>` | true |
| `FlatShading` | `Sync<bool>` | false |
| `UVScale` | `Sync<float2>` | (1,1) |

- Internally uses `ConicalFrustum` with `RadiusTop == Radius` (true cylinder).
- `CreateCollider()` attaches a `CylinderCollider` driven from `Radius` and `Height`.
- Topology rebuild if `Sides`, `Caps`, or `FlatShading` change.

---

## Light

**Category:** Rendering
**Inherits:** `ChangeHandlingRenderableComponent`

There are no separate PointLight/SpotLight/DirectionalLight components. The single `Light` component uses a `LightType` enum.

### Key Fields

| Field | Type | Default | Notes |
|---|---|---|---|
| `LightType` | `Sync<LightType>` | Point | Point, Spot, Directional |
| `Intensity` | `Sync<float>` | 1.0 | Light brightness |
| `Color` | `Sync<colorX>` | White | Light color |
| `Range` | `Sync<float>` | 10.0 | Point/Spot only |
| `SpotAngle` | `Sync<float>` | 60.0 | Spot only, degrees |
| `ShadowType` | `Sync<ShadowType>` | None (default) | Shadow mode |
| `ShadowStrength` | `Sync<float>` | 1.0 | Range 0-1 |
| `ShadowNearPlane` | `Sync<float>` | 0.2 | Shadow camera near clip |
| `ShadowMapResolution` | `Sync<int>` | 0 | 0 = automatic |
| `ShadowBias` | `Sync<float>` | 0.125 | Range 0-2 |
| `ShadowNormalBias` | `Sync<float>` | 0.6 | Range 0-3 |
| `Cookie` | `AssetRef<ITexture>` | null | Alpha modulates intensity |

### Gotchas

- `IsRenderable` checks same `ActiveUser.IsRenderingLocallyBlocked` pattern as MeshRenderer.
- Subscribes to `Slot.WorldTransformChanged` to mark dirty on every move.
- Legacy intensity conversion differs between Directional and Point/Spot lights (separate adapters).

---

## Camera

**Category:** Rendering
**Inherits:** `ChangeHandlingRenderableComponent`
**Implements:** `IUVToRayConverter`

### Key Fields

| Field | Type | Default | Notes |
|---|---|---|---|
| `Projection` | `Sync<CameraProjection>` | Perspective | Perspective or Orthographic |
| `FieldOfView` | `Sync<float>` | 60.0 | Vertical FOV, range 10-140 |
| `OrthographicSize` | `Sync<float>` | 8.0 | Half-height in world units |
| `NearClipping` | `Sync<float>` | 0.1 | |
| `FarClipping` | `Sync<float>` | 4096.0 | Was 1000 in v0, migrated |
| `UseTransformScale` | `Sync<bool>` | false | Scales ortho size by slot scale |
| `Clear` | `Sync<CameraClearMode>` | Skybox | |
| `ClearColor` | `Sync<colorX>` | Clear | Used when Clear != Skybox |
| `Viewport` | `Sync<Rect>` | (0,0)-(1,1) | Normalized viewport rect |
| `Depth` | `Sync<float>` | 0 | Render order for overlapping cameras |
| `RenderTexture` | `AssetRef<RenderTexture>` | null | Target texture; null = screen |
| `DoubleBuffered` | `Sync<bool>` | false | |
| `ForwardOnly` | `Sync<bool>` | false | |
| `Postprocessing` | `Sync<bool>` | true | |
| `ScreenSpaceReflections` | `Sync<bool>` | false | |
| `MotionBlur` | `Sync<bool>` | true | |
| `RenderShadows` | `Sync<bool>` | true | |
| `SelectiveRender` | `AutoSyncRefList<Slot>` | empty | Only render these hierarchies |
| `ExcludeRender` | `AutoSyncRefList<Slot>` | empty | Exclude these hierarchies |

### Key Methods

- **`RenderToTexture(int2 resolution, Slot root, string format, int quality)`** -- renders to a `StaticTexture2D`. Default format "webp", quality 200.
- **`RenderToAsset(int2 resolution, format, quality)`** -- renders and saves as a URI asset.
- **`RenderToBitmap(int2 resolution)`** -- renders to a `Bitmap2D`.
- **`GetRenderSettings(int2 resolution)`** -- builds a `RenderTask` from current camera state.
- **`UVToRay(float2 uv, out float3 origin, out float3 direction)`** -- converts normalized UV to world ray. Handles both perspective and orthographic.
- **`PointToUV(float3 point)`** -- inverse: world point to normalized UV.
- **`AspectRatio`** -- derived from `RenderTexture.Size`; returns 1.0 if no texture or zero-size.
- **`HorizontalFieldOfView`** -- computed from vertical FOV and aspect ratio.

### Gotchas

- `ActiveWhenDisabled` and `ActiveWhenDeactivated` are both true -- the camera component processes changes even when disabled/deactivated.
- `SelectiveRender` and `ExcludeRender` slots get `IncrementRenderable()`/`DecrementRenderable()` calls. Null entries are filtered out.
- `RenderPrivateUI` is a non-synced runtime property (not persisted).

---

## RenderSystem

The bridge between FrooxEngine and the Renderite renderer process. Manages asset upload, frame submission, and shared memory.

### Architecture

- FrooxEngine and Renderite run as **separate processes** communicating via `RenderiteMessagingHost` (shared memory + message queues).
- `SharedMemoryManager` handles memory allocation for textures, meshes, etc.
- Frame loop: engine calls `WaitForFrameBegin()` (blocks until renderer signals), processes world updates, then calls `SubmitFrame()`.

### Key Properties

| Property | Type | Notes |
|---|---|---|
| `HasRenderer` | `bool` | False in headless/no-render mode |
| `State` | `RendererState` | `StartingUp` -> `Rendering` |
| `FrameIndex` | `int` | Monotonic frame counter |
| `MaxTextureSize` | `int` | GPU limit, set after init |
| `IsGPUTexturePOTByteAligned` | `bool` | Affects RGB24->RGBA32 conversion |
| `MaterialColorProfile` | `ColorProfile` | Always `sRGB` |
| `RendererWindowHandle` | `nint` | OS window handle |

### Asset Managers

Each asset type has a `RenderAssetManager<T>`:
`Texture2Ds`, `Texture3Ds`, `Cubemaps`, `RenderTextures`, `VideoTextures`, `DesktopTextures`, `Meshes`, `Shaders`, `Materials` (uses `RenderMaterialManager`), `PointBuffers`, `TrailBuffers`, `GaussianSplats`, `LightBuffers`.

### Key Methods

- **`Initialize(...)`** -- starts the renderer process, sets up shared memory, sends `RendererInitData`.
- **`WaitForFrameBegin()`** -- blocks on `ManualResetEvent` until renderer sends `FrameStartData`. Returns null on shutdown.
- **`SubmitFrame()`** -- collects render updates from all worlds, sends `FrameSubmitData`. Increments `FrameIndex`.
- **`SendAssetUpdate(AssetCommand)`** -- sends asset data to renderer (background channel).
- **`SendMaterialUpdate(MaterialsUpdateBatch)`** -- batched material property updates.
- **`ParentWindow(nint)`** -- reparents the renderer window (sends handle to renderer).
- **`SetWindowIcon(Bitmap2D)` / `SetWindowOverlayIcon(...)` / `ClearOverlayIcon(...)`** -- async icon management.
- **`SupportsTextureFormat(TextureFormat)`** -- checks against formats reported by renderer at init.
- **`EnsureCompatibleFormat(TextureFormat)`** -- converts unsupported formats (e.g., RGB24 -> RGBA32 on aligned GPUs).
- **`AllocateBlock<T>(count, frameBound, owner)`** -- shared memory allocation. If `frameBound`, auto-freed after frame finalize.

### Frame Lifecycle

1. Renderer sends `FrameStartData` -> `_frameStartEvent.Set()`
2. Engine wakes in `WaitForFrameBegin()`, returns frame data
3. Engine processes worlds, collects render state
4. Engine calls `SubmitFrame()` -> sends `FrameSubmitData` with all world render spaces
5. At frame 120, sends `RendererEngineReady` signal

### Settings Listeners

Registered on `FinishInitialize()`:
- `PostProcessingSettings` -> bloom, AO, motion blur, AA, SSR
- `RenderingQualitySettings` -> shadow cascades/resolution/distance, skin weights, per-pixel lights
- `ResolutionSettings` -> fullscreen toggle, resolution changes
- `DesktopRenderSettings` -> vsync, background framerate cap
- `GaussianSplatQualitySettings` -> sort operations per camera
- `RendererDecouplingSettings` -> decouple activation framerate, asset processing time

### Gotchas

- Renderer crash (`HasExited`) triggers `Engine.ForceCrash()` via watchdog task.
- `ENGINE_READY_FRAMES = 120` -- the renderer is told the engine is ready only after 120 frames.
- Shared memory is the primary data transfer mechanism. `frameBound` allocations are automatically freed after the frame is consumed.
- The `HandleCommand` method is a giant switch over all possible `RendererCommand` subtypes (init results, asset results, frame data, etc.).
- `ReceivedPrimaryMessages` property has a bug: it reads `SentPrimaryMessages` instead of `ReceivedPrimaryMessages`.

---

## Additional Material Types

The following material types are documented from the full decompiled source. All inherit from `MaterialProvider` or `SingleShaderMaterialProvider` unless noted.

### Filter Materials (Category: Assets/Materials/Filters)

| Material | Purpose |
|---|---|
| `BlurMaterial` | Screen-space blur with configurable iterations, spread, Poisson disc, refraction, per-object mode |
| `ChannelMatrixMaterial` | RGB channel remapping via 3x3+offset matrix with clamping |
| `DepthMaterial` | Visualizes or clips based on scene depth values |
| `GammaMaterial` | Applies gamma correction to rendered image |
| `GrayscaleMaterial` | Converts to grayscale with per-channel weights and optional gradient remap |
| `HSV_Material` | Adjusts hue, saturation, value via offset and multiply |
| `InvertMaterial` | Inverts colors with controllable lerp blend |
| `LUT_Material` | 3D lookup table color grading with optional lerp between two LUTs |
| `PixelateMaterial` | Pixelates image to configurable resolution |
| `PosterizeMaterial` | Reduces color levels for posterization effect |
| `RefractMaterial` | Refraction distortion using a normal map |
| `ThresholdMaterial` | Brightness threshold with smooth transition |

### Special/Overlay Materials

| Material | Purpose |
|---|---|
| `DebugMaterial` | Debug visualization of mesh data (position, color, normals, tangents, UVs) as false-color |
| `DepthProjectionMaterial` | Projects color+depth textures into 3D space using FOV perspective |
| `FlatLitToonMaterial` | Toon/cel-shading with flat lighting, color mask, emission, outlines |
| `FresnelMaterial` | View-angle-dependent blending between near/far colors/textures |
| `FresnelLerpMaterial` | Blends between two Fresnel-based color/texture sets |
| `FurMaterial` | Shell-based fur rendering with configurable length, density, specular, rim lighting |
| `MatcapMaterial` | Matcap texture-based shading without scene lights |
| `OverlayFresnelMaterial` | Fresnel-based with separate behind/front rendering for overlay effects |
| `OverlayUnlitMaterial` | Unlit with separate behind/front tint and textures |
| `WireframeMaterial` | Wireframe rendering with configurable line/fill colors and fresnel |
| `XiexeToonMaterial` | Comprehensive toon material with rim lighting, outlines, matcap, subsurface scattering |
| `FogBoxVolumeMaterial` | Volumetric fog box with configurable density, color gradients, accumulation |
| `VolumeUnlitMaterial` | Volume rendering for 3D textures with raymarching, slicing, highlighting |

### PBS Material Variants

All PBS variants come in Metallic and Specular workflows:

| Base Type | Purpose |
|---|---|
| `PBS_Material` | Standard PBS (most commonly used) |
| `PBS_ColorMask` | Uses color mask texture to blend 4 color channels |
| `PBS_ColorSplat` | Terrain-like multi-texture splatting with height blending |
| `PBS_DisplaceMaterial` | Vertex displacement mapping |
| `PBS_DistanceLerpMaterial` | Distance-based property lerping from up to 16 world-space points |
| `PBS_DualSidedMaterial` | Dual-sided rendering with alpha handling |
| `PBS_Intersect` | Highlights mesh intersections with other geometry |
| `PBSLerpMaterial` | Lerps between two sets of PBS properties |
| `PBS_MultiUV_Material` | Multiple UV channels for different texture layers |
| `PBS_RimMaterial` | PBS with rim lighting effects |
| `PBS_Slice` | Slicing/clipping geometry along configurable planes |
| `PBS_StencilMaterial` | PBS with stencil buffer support for UI rendering |
| `PBS_TriplanarMaterial` | Triplanar projection (no UV dependency) |
| `PBS_VertexColor` | Uses vertex colors for tinting or replacement |
| `PBS_VoronoiCrystal` | Procedural Voronoi crystal pattern |

### UI Materials

| Material | Purpose |
|---|---|
| `UI_StencilMaterial` | Abstract base for all UI materials with stencil buffer and rect clipping |
| `SingleShaderUI_StencilMaterial` | Abstract base for UI stencil materials using a single shader |
| `UI_CircleSegment` | Renders circular segments (arcs/pies) |
| `UI_TextUnlitMaterial` | Text rendering with UI stencil support |
| `UI_UnlitMaterial` | General unlit with stencil/rect clip support |
| `TextUnlitMaterial` | Unlit text glyph rendering with outline and face configuration |

### Projection/Skybox Materials

| Material | Purpose |
|---|---|
| `Projection360Material` | 360-degree texture/cubemap projection with stereo support |
| `GradientSkyMaterial` | Gradient layers for sky rendering |
| `ProceduralSkyMaterial` | Procedural skybox with configurable sun and atmosphere |

### Material Infrastructure

- **`ShaderKeywords`** -- Manages shader keyword variant bitmask for permutation selection
- **`SingleShaderMaterialProvider`** -- Abstract base for materials using a single shader
- **`MaterialHelper`** (static) -- Utility methods for material property conversion, copying, and replacement
- **`MaterialProperty`** -- Named shader property with lazy index initialization
- **`MaterialPropertyBlockProvider`** -- Base for per-instance material property overrides

### Material Interfaces

| Interface | Purpose |
|---|---|
| `ICommonMaterial` | Standard properties: color, main texture, normal map, texture transforms |
| `IPBS_Material` | Standard PBR texture slots |
| `IPBS_Metallic` | Metallic workflow (metallic + smoothness) |
| `IPBS_Specular` | Specular workflow (specular color + smoothness) |
| `IBlendModeMaterial` | Configurable blend mode |
| `ICullingMaterial` | Face culling control |
| `IBillboardMaterial` | Billboard rendering support |
| `IStereoMaterial` | Per-eye texture transforms for VR |
| `ISkyboxMaterial` | Skybox configuration |
| `ITextMaterial` | Text glyph rendering |
| `IUIX_Material` | UI stencil and rect clipping |

---

## Additional Procedural Meshes

Beyond BoxMesh, SphereMesh, CylinderMesh, and QuadMesh (documented above), FrooxEngine includes:

| Mesh | Purpose |
|---|---|
| `ArrowMesh` | 3D arrow with configurable body/head dimensions and per-section colors |
| `BallisticPathMesh` | Mesh along a ballistic trajectory path |
| `BentTubeMesh` | Tube bent along a curve |
| `BevelBoxMesh` | Box with beveled edges |
| `BezierTubeMesh` | Tube along a Bezier curve |

All procedural meshes inherit from `ProceduralMesh` and generate data on a background thread via `PrepareAssetUpdateData()` -> `UpdateMeshData(MeshX)`.

---

## Procedural Cubemaps

| Type | Purpose |
|---|---|
| `CheckerboardCubemap` | Cubemap with checkerboard patterns (different colors per face) |
| `SimplexCubemap` | Cubemap filled with simplex noise |

Base: `ProceduralCubemapBase` -> `ProceduralAssetProvider<Cubemap>`.

---

## Gizmo Components

| Gizmo | Purpose |
|---|---|
| `LightGizmo` | Visual gizmo for Light showing type-specific icons and handles |
| `MaterialGizmo` | Floating inspector panel for a material provider |
| `MeshGizmo` | Floating inspector panel for a static mesh |
| `MeshRendererGizmo` | Auto-attaches MeshCollider in dev mode for interaction |
| `TextGizmo` | In-world edit icon and text editor for TextRenderer |

### Rendering Interfaces

| Interface | Purpose |
|---|---|
| `IBounded` | Components with computable bounding box |
| `IRenderable` | Components that can be rendered with local rendering control |
| `IUVToRayConverter` | UV-to-world-ray conversion |
