# Renderite.Shared & Awwdio API Reference

## Renderite.Shared

Shared types for the out-of-process renderer (Renderite). Communication uses IPC queues with a custom binary serialization (`MemoryPacker`/`MemoryUnpacker`). The engine sends `RendererCommand` messages to the renderer process and receives results back.

---

### IPC / Messaging

#### `MessagingManager`
IPC bridge between engine and renderer using shared memory queues (Cloudtoid.Interprocess).
- Fields: `CommandHandler` (callback for received commands), `FailureHandler`, `WarningHandler`
- `Connect(queueName, isAuthority, capacity)` -- creates publisher/subscriber pair, starts receiver thread
- `SendCommand(RendererCommand)` -- serializes and enqueues a command
- `StartKeepAlive(intervalMs)` -- sends periodic `KeepAlive` commands
- Receiver thread runs at `ThreadPriority.Highest`, decodes `RendererCommand` polymorphically

#### `RendererCommand` (abstract)
Base class for all IPC messages. Extends `PolymorphicMemoryPackableEntity<RendererCommand>`. Statically registers all concrete command types for polymorphic deserialization.

#### `PolymorphicMemoryPackableEntity<T>`
Generic base for polymorphic serialization. Maintains a static type registry. Methods: `Encode(ref MemoryPacker)`, `Decode(ref MemoryUnpacker)`, `InitTypes(List<Type>)`.

#### `MemoryPacker` (ref struct)
Span-based binary writer. Writes primitives, strings, lists, nullable values, objects implementing `IMemoryPackable`, and polymorphic entities directly into a `Span<byte>`.

#### `MemoryUnpacker` (ref struct)
Span-based binary reader. Counterpart to `MemoryPacker`. Has `Pool` for borrowing reusable objects. Methods mirror `MemoryPacker` (Read, ReadString, ReadValueList, ReadObjectList, etc.).

#### `IMemoryPackable`
Interface: `Pack(ref MemoryPacker)`, `Unpack(ref MemoryUnpacker)`.

#### `IMemoryPackerEntityPool`
Object pool for `IMemoryPackable` types. `Borrow<T>()`, `Return<T>(value)`.

---

### Memory Management

#### `IBackingMemoryBuffer` / `BackingMemoryBuffer` (abstract)
Thread-safe disposable memory buffer with lock/unlock semantics for concurrent access. Properties: `SizeBytes`, `RawData` (Span), `Memory`, `IsDisposed`. Uses reference counting (`TryLockUse()`/`Unlock()`) to defer disposal until all users release.

#### `ArrayBackingMemoryBuffer`
Concrete `BackingMemoryBuffer` backed by a `byte[]`.

#### `SharedMemoryBufferDescriptor<T>` / `SharedMemoryBufferDescriptor`
Describes a shared memory region by `bufferId`, `offset`, and `count`. Used to pass data buffers between engine and renderer via shared memory views.

#### `FreeSharedMemoryView`
Command to release a shared memory view by `bufferId`.

#### `UnmanagedMemoryManager<T>`
`MemoryManager<T>` wrapping an unmanaged pointer + length. For creating `Memory<T>` over native memory.

#### `BitSpan` (ref struct)
Bit-addressable view over a `Span<uint>`. Indexed access to individual bits.

---

### Mesh Data

#### `MeshBuffer`
Central mesh data container. Holds vertex attributes, submeshes, blendshapes, bone weights, bind poses in a single contiguous `IBackingMemoryBuffer`.
- Key fields: `VertexCount`, `BoneWeightCount`, `BoneCount`, `IndexBufferFormat`, `VertexAttributes`, `Submeshes`, `BlendshapeBuffers`
- `ComputeBufferLayout()` -- computes all offsets (vertex, index, bones, blendshape) from the attribute list. Max 2 GB.
- Accessors: `GetRawVertexBufferData()`, `GetRawIndexBufferData()`, `GetIndexBufferUInt32()`, `GetBoneWeightsBuffer()`, etc.

#### `VertexAttributeDescriptor` (struct, 8 bytes)
Fields: `attribute` (VertexAttributeType), `format` (VertexAttributeFormat), `dimensions`. `Size` = format byte size * dimensions.

#### `SubmeshBufferDescriptor` (struct, 36 bytes)
Fields: `topology` (Points/Triangles), `indexStart`, `indexCount`, `bounds`.

#### `BlendshapeBufferDescriptor` (struct, 16 bytes)
Fields: `blendshapeIndex`, `frameIndex`, `frameWeight`, `dataFlags` (Positions/Normals/Tangents).

#### `BoneWeight` (struct, 8 bytes)
Fields: `weight` (float), `boneIndex` (int).

#### `MeshUploadHint` (struct)
Bitfield indicating which mesh components changed (VertexLayout, Geometry, Positions, Normals, UVs, Blendshapes, Dynamic, Readable, etc.).

---

### Asset Commands

All extend `AssetCommand` (which extends `RendererCommand`) and carry an `assetId`.

#### Mesh
- `MeshUploadData` -- uploads mesh buffer + layout. Fields: `buffer`, `vertexCount`, vertex/submesh/blendshape descriptors, `uploadHint`, `bounds`
- `MeshUploadResult` -- response with `instanceChanged`
- `MeshUnload` -- unload mesh by assetId

#### Shader
- `ShaderUpload` -- `file` (path)
- `ShaderUploadResult` -- `instanceChanged`
- `ShaderUnload`

#### Material
- `MaterialPropertyIdRequest` / `MaterialPropertyIdResult` -- resolve property names to IDs
- `MaterialsUpdateBatch` -- batched material property updates via shared memory buffers (floats, float4s, matrices, textures)
- `MaterialsUpdateBatchResult`
- `UnloadMaterial`, `UnloadMaterialPropertyBlock`

#### Texture2D
- `SetTexture2DFormat` -- width, height, mipmapCount, format, colorProfile
- `SetTexture2DProperties` -- filterMode, anisoLevel, wrapModes, mipmapBias
- `SetTexture2DData` -- pixel data via shared memory + mip info
- `SetTexture2DResult` -- type + instanceChanged
- `UnloadTexture2D`

#### Texture3D
- `SetTexture3DFormat`, `SetTexture3DProperties`, `SetTexture3DData`, `SetTexture3DResult`, `UnloadTexture3D`

#### Cubemap
- `SetCubemapFormat`, `SetCubemapProperties`, `SetCubemapData`, `SetCubemapResult`, `UnloadCubemap`

#### RenderTexture
- `SetRenderTextureFormat` -- size, depth, filterMode, anisoLevel, format, colorProfile, wrapModes, useMipmap
- `RenderTextureResult`, `UnloadRenderTexture`

#### Desktop Texture
- `SetDesktopTextureProperties` -- displayIndex
- `DesktopTexturePropertiesUpdate` -- size
- `UnloadDesktopTexture`

#### Video Texture
- `VideoTextureLoad` -- file path, loop, play
- `VideoTextureUpdate` -- position, play, loop
- `VideoTextureReady` -- duration, width, height, fps, hasAudio, audioSampleRate, audioChannels
- `VideoTextureProperties` -- filterMode, anisoLevel, wrapMode, playbackSpeed
- `VideoTextureStartAudioTrack` -- queueName, capacity
- `VideoTextureChanged`, `UnloadVideoTexture`

#### Gaussian Splat
- `GaussianSplatUpload` (abstract) -- splatCount, bounds, position/rotation/scale/color buffers
- `GaussianSplatUploadEncoded` -- encoded formats + SH data
- `GaussianSplatUploadRaw` -- raw alpha buffer
- `GaussianSplatResult`, `UnloadGaussianSplat`

#### Render Buffers
- `RenderBufferUpload` (abstract) -- shared memory buffer
- `PointRenderBufferUpload` -- count + offsets for positions/rotations/sizes/colors/frameIndexes
- `TrailRenderBufferUpload` -- trailsCount, trailPointCount, offsets
- Consumed/Unload commands for each

---

### Configuration Commands

- `DesktopConfig` -- `maximumBackgroundFramerate`, `maximumForegroundFramerate`, `vSync`
- `PostProcessingConfig` -- motionBlur, bloom, AO, SSR, antialiasing method
- `QualityConfig` -- perPixelLights, shadowCascades/resolution/distance, skinWeightMode
- `ResolutionConfig` -- resolution, fullscreen
- `RenderDecouplingConfig` -- decouple intervals, max asset processing time, recouple frame count
- `GaussianSplatConfig` -- sortingMegaOperationsPerCamera

---

### Frame Loop

#### `FrameStartData` (renderer -> engine)
Sent by renderer at frame start. Contains: `lastFrameIndex`, `performance` (PerformanceState), `inputs` (InputState), rendered reflection probes, video clock errors.

#### `FrameSubmitData` (engine -> renderer)
Sent by engine each frame. Contains: `frameIndex`, `vrActive`, `nearClip`, `farClip`, `desktopFOV`, `outputState`, list of `RenderSpaceUpdate`, optional `CameraRenderTask` list.

#### `PerformanceState`
Renderer perf metrics: `fps`, `immediateFPS`, `renderTime`, `renderedCameras`, `updatedTextures`, `textureSliceUploads`, etc.

#### `KeepAlive`
Empty heartbeat command.

---

### Render Space & Renderables

#### `RenderSpaceUpdate`
Per-frame update for one render space. Contains `TransformsUpdate` + updates for every renderable type: meshes, skinned meshes, lights, cameras, camera portals, reflection probes, billboards, trails, gaussian splats, LOD groups, material/transform overrides, blit-to-display.

#### `RenderablesUpdate` (abstract)
Base for renderable state updates. Fields: `removals`, `additions` (shared memory int buffers).

#### `RenderablesStateUpdate<TState>` (abstract)
Generic specialization adding a `states` shared memory buffer of the unmanaged state struct.

Key state structs (all unmanaged, packed layout):
- `MeshRendererState` -- renderableIndex, meshAssetId, materialCount, sortingOrder, shadowCastMode
- `LightState` -- intensity, range, spotAngle, color, shadow params, type
- `CameraState` -- FOV, orthographicSize, nearClip, farClip, viewport, clearMode, projection, flags (enabled, renderPrivateUI, postProcessing, etc.)
- `CameraPortalState` -- portal transform, plane, render texture, override far clip/clear
- `ReflectionProbeState` -- importance, intensity, box size, resolution, type, HDR, boxProjection
- `TrailsRendererState` -- buffer assetId, materialAssetId, textureMode
- `GaussianSplatRendererState` -- splatAssetId, sizeScale, opacityScale, maxSHOrder
- `BillboardRenderBufferState`, `MeshRenderBufferState`, `BlitToDisplayState`, `LODGroupState`

#### `TransformsUpdate`
Batched transform hierarchy updates: `removals`, `parentUpdates`, `poseUpdates`.

#### `SkinnedMeshRenderablesUpdate`
Extends `MeshRenderablesUpdate` with bounds, bone assignments, bone transform indexes, blendshape updates.

---

### Input State

#### `InputState`
Aggregates all input from renderer: `mouse`, `keyboard`, `window`, `vr`, `gamepads`, `touches`, `displays`.

#### `MouseState`
`isActive`, button states (left/right/middle/4/5), `desktopPosition`, `windowPosition`, `directDelta`, `scrollWheelDelta`.

#### `KeyboardState`
`typeDelta` (typed characters), `heldKeys` (HashSet of Key enum).

#### `WindowState`
`isWindowFocused`, `isFullscreen`, `windowResolution`, `dragAndDropEvent`.

#### `GamepadState`
Thumbsticks, triggers, dpad, bumpers, face buttons, paddles.

#### `VR_InputsState`
`headsetState`, controllers (polymorphic list), trackers, tracking references, hands, viveHandTracking.

#### `VR_ControllerState` (abstract)
Base for all VR controller types. Fields: `side` (Chirality), `bodyNode`, `isDeviceActive`, `isTracking`, `position`, `rotation`, `batteryLevel`. Concrete: `IndexControllerState`, `TouchControllerState`, `ViveControllerState`, `WindowsMR_ControllerState`, `CosmosControllerState`, `GenericControllerState`, `HP_ReverbControllerState`, `PicoNeo2ControllerState`.

#### `HeadsetState`
`isTracking`, `position`, `rotation`, `batteryLevel`, `connectionType`, manufacturer/model.

#### `HandState`
Hand tracking: `chirality`, segment positions/rotations, wrist position/rotation, confidence.

---

### Output State

#### `OutputState`
`lockCursor`, `lockCursorPosition`, `keyboardInputActive`, `vr` (VR_OutputState).

#### `VR_OutputState`
Left/right `VR_ControllerOutputState`, `useViveHandTracking`.

#### `VR_ControllerOutputState`
`side`, `vibrateTime`, `hapticState` (force/temperature/pain/vibration).

---

### Renderer Lifecycle

- `RendererInitData` -- sharedMemoryPrefix, sessionId, mainProcessId, outputDevice, windowTitle, icon, splash
- `RendererInitResult` -- actualOutputDevice, rendererIdentifier, windowHandle, maxTextureSize, supportedTextureFormats
- `RendererInitProgressUpdate` -- progress, phase, subPhase
- `RendererInitFinalizeData`, `RendererEngineReady`, `RendererShutdownRequest`, `RendererShutdown`
- `RendererParentWindow` -- windowHandle
- `SetWindowIcon`, `SetWindowIconResult`, `SetTaskbarProgress`

---

### Enums (selected)

- `TextureFormat` -- Alpha8, R8, RGB24, RGBA32, BGRA32, RGBAHalf, RGBAFloat, BC1-BC7, ETC2, ASTC variants
- `TextureFilterMode` -- Point, Bilinear, Trilinear, Anisotropic
- `TextureWrapMode` -- Repeat, Clamp, Mirror, MirrorOnce
- `ColorProfile` -- Linear, sRGB, sRGBAlpha
- `HeadOutputDevice` -- Headless, Screen, SteamVR, WindowsMR, Oculus, OculusQuest, etc.
- `Key` -- full keyboard enum (ASCII-based + function keys + numpad + modifiers)
- `BodyNode` -- full body skeleton nodes (Head, Hips, LeftHand, etc.)
- `Chirality` -- Left (-1), Right (1)
- `CameraClearMode` -- Skybox, Color, Depth, Nothing
- `CameraProjection` -- Perspective, Orthographic, Panoramic
- `ShadowCascadeMode`, `ShadowResolutionMode`, `SkinWeightMode`

---

### Utility

#### `Helper`
Constants: `EDITOR_PORT` (42512), `PROCESS_NAME` ("Renderite.Renderer"), `PRIMARY_QUEUE`, `BACKGROUND_QUEUE`. Detects Wine. `ComposeMemoryViewName(prefix, bufferId)`.

#### `MathHelper`
Math utilities for the renderer: `RemapUNORM`, `RemapSNORM`, `PackAndSwizzle11`, etc.

#### `TextureFormatExtensions`
Extension methods on `TextureFormat`: `BytesPerPixel()`, `IsCompressed()`, `IsHDR()`, `ComputeGPUBufferSize()`, `FindAlternateFormats()`.

---

## Awwdio

Resonite's audio simulation engine. Runs on a dedicated high-priority thread pool (work-stealing scheduler). Uses Steam Audio for HRTF binaural spatialization.

---

### Core Simulation

#### `AudioSimulator`
Central audio system. Manages audio spaces, render scheduling, and Steam Audio context.
- Constructor: `AudioSimulator(sampleRate, bufferSampleCount=1024, maxActiveOutputs=64)`
- Key properties: `SampleRate`, `FrameSize`, `BufferSize`, `DSP_Time`, `AudioFrameIndex`, `UpdateInProgress`, `LastUpdateTime`, perf counters
- `RenderAudio(onFinished)` / `TryRenderAudio()` -- kicks off one audio frame render (non-blocking, work-stealing parallelism)
- `AddSpace(startActive)` / `RemoveSpace()` / `ActivateSpace()` / `DeactivateSpace()` -- manage audio spaces
- `SetMaxActiveOutputs(n)`, `SetFrameSize(n)` -- deferred config changes (applied next frame)
- `RegisterPreRenderParallelTask()` / `UnregisterPreRenderParallelTask()` -- pre-render hooks
- Events: `RenderStarted`, `RenderFinished`, `FrameSizeChanged`
- Internal pools: `BufferPool` (AudioBufferPool), `SteamAudio` (SteamAudioContext)

#### `AudioSpace`
Represents one spatial audio environment (maps to a Resonite world/focus). Contains outputs, listeners, inlets.
- `AddOutput()` / `RemoveOutput()` -- create/destroy `AudioOutput`
- `AddListener()` / `RemoveListener()` -- create/destroy `Listener`
- `AddAudioInlet()` / `RemoveAudioInlet()` -- create/destroy `AudioInlet`
- `BeginChangesBatch()` / `FinishChangesBatch()` -- thread-safe batched parameter changes
- `SpeedOfSound` = 343 m/s
- Internal: maintains a `SpatialCollection3D<AudioOutput>` for spatial queries. `Render()` applies changes, collects visible outputs per listener, schedules render/mix jobs.

#### `AudioThreadLoopUpdater`
Drives `AudioSimulator.TryRenderAudio()` on a dedicated `ThreadPriority.Highest` background thread at the correct cadence (based on frame size / sample rate). Tracks `DroppedAudioFrames`.

---

### Audio Pipeline

#### `AudioOutput`
A positioned audio source in a space. Properties: `Transform`, `Volume`, `Priority`, `Spatialize`, `SpatialBlend`, `Pitch`, `DopplerStrength`, `Source` (IAudioDataSource), `Shape` (IAudioShape), `Inlet` (AudioInlet), `ExcludedListeners`.
- `Update(batch, ...)` -- batched parameter updates (transform, volume, source, shape, excluded listeners)
- `IsVisibleToListener(listener)` -- checks exclusion list
- Internal: manages per-listener `AudioOutputListenerContext` for doppler/pitch/binaural processing

#### `Listener` (extends `Mixer`)
A positioned audio receiver. Bound to an `AudioDeviceOutput` to produce audible output.
- Properties: `Transform`, `BoundDevice`, `CurrentBuffer`, `MixerMappings`
- `UpdateTransform(batch, transform)`, `MapMixer(batch, inlet, mixer)`, `BindToDevice(output)`
- On `FinishMix()`: sanitizes buffer, auto-ducks, stages result, forwards to bound device

#### `AudioInlet`
Routing tag for audio outputs. Associates an output with a specific DSP mixer chain on a listener. Each space has a default inlet.

#### `AudioDeviceOutput` (extends `Mixer`)
Final output stage that feeds audio to a hardware device (`IAudioDeviceOutput`). Applies volume with smooth interpolation and auto-ducking.

#### `Mixer` (abstract)
Concurrent audio buffer mixer. `PrepareMixing(trackCount)` prepares for N incoming buffers. `ConsumeAndMix(buffer)` atomically mixes a buffer in. When all tracks arrive, calls `FinishMix(result)`.

#### `ConcurrentBufferMixer`
Lock-free buffer accumulator using `Interlocked.Exchange`. Used internally by `Mixer`.

---

### Audio Shapes

#### `IAudioShape` (interface)
Defines spatial attenuation. Properties: `Output`, `Bounds`. Method: `ComputeAttenuation(listenerTransform) -> float`.

#### `GlobalAudioShape`
No attenuation (always returns 1.0). Infinite bounds.

#### `SphereAudioShape`
Distance-based attenuation. Properties: `MinDistance`, `MaxDistance`, `Curve` (AudioRolloffCurve).
- `Update(batch, minDist, maxDist, curve)` -- batched parameter change

#### `AudioRolloffCurve` (enum)
LogarithmicInfinite, LogarithmicClamped, LogarithmicFadeOff, Linear.

#### `AudioRolloffHelper`
Static methods: `ComputeAttenuation(curve, distance, minDist, maxDist)`, `RawLogarithmicFalloff()`.

---

### DSP / Filters

#### `DSP_Mixer`
Per-listener DSP effect chain node. Wraps an `IAudioDSP_Filter`. Has `Next` pointer for chaining.
- `PrepareMixing(listener, count)` -- creates/reuses `DSP_Context` per listener
- `ConsumeAndMix(listener, buffer)` -- passes mixed audio through the filter, then to `Next` or the listener

#### `DSP_Context` (internal, extends `Mixer`)
Per-listener instance of a DSP filter. Holds `FilterContext` (IAudioDSP_FilterContext). On `FinishMix`, processes buffer through filter, then forwards to next mixer or listener.

#### `IAudioDSP_Filter` (interface)
`Enabled`, `CreateContext() -> IAudioDSP_FilterContext`, `UpdateEnabled(batch, bool)`.

#### `IAudioDSP_FilterContext` (interface)
`Process(AudioBuffer)`. Disposable.

#### `IAudioDSP_BlendingFilterContext` (interface)
Extends filter context: `Process(AudioBuffer, float blend)`.

#### `FilterBlendWrapper`
Wraps another `IAudioDSP_Filter` with blend weight and enable/disable. Manages nested contexts. Validates against circular references.
- Properties: `Filter`, `BlendWeight`, `Enabled`
- `UpdateBlend(batch, weight)`, `UpdateFilter(batch, filter)`, `UpdateEnabled(batch, enabled)`

#### `ZitaReverbFilter`
Reverb effect using Zita reverb algorithm. Properties: `Parameters` (ZitaParameters), `Enabled`.
- `Update(batch, parameters)`, `UpdateEnabled(batch, enabled)`

---

### Binaural / Steam Audio

#### `SteamAudioContext` (internal)
Manages Steam Audio IPL context and HRTF. `LoadHRTF()`, `CreateBinauralEffect(listenerContext)`, `FrameSizeChanged()`.

#### `BinauralEffect` (internal)
Applies HRTF spatialization to a buffer for a specific output-listener pair. Uses `SpatialBlend` and `SpatializationStartDistance` to control blend. Converts to/from interleaved stereo for IPL processing.

#### `SteamAudioBufferPool` (internal)
Pools `IPL.AudioBuffer` objects (mono/stereo) to avoid allocation during rendering.

---

### Change Batching

#### `ChangesBatch`
Thread-safe batch of parameter changes for an `AudioSpace`. `BeginRecording()` / `FinishRecording()` bracket changes. `Submit()` applies all buffered changes. Captures timestamp.

#### `ChangesBuffer<T>` (abstract)
Typed change buffer. `Add(T)` during recording, `Apply(T)` on submit. Concrete implementations for every parameter type:
- `AudioOutputParametersChanges` -- transform, volume, priority, spatialize, pitch, doppler
- `AudioOutputReferencesChanges` -- source, shape, inlet
- `AudioOutputExcludedListenersChanges`
- `ListenerChanges` -- transform
- `ListenerMixerChanges` -- inlet-to-mixer mapping
- `DSP_MixerChanges` -- next chain pointer
- `SphereAudioShapeChanges` -- min/max distance, curve
- `FilterBlendWeightChanges`, `FilterBlendEnabledChanges`, `FilterBlendFilterChanges`
- `ZitaReverbChanges`, `ZitaReverbEnabledChanges`

---

### Data Types

#### `AudioBuffer`
Wrapper around `float[]` with `Length`, `Data`, `Clear()`, `Clone(simulator)`.

#### `AudioBufferPool`
`ConcurrentStack`-based pool of `AudioBuffer`. `BorrowBuffer()` / `ReturnBuffer()`.

#### `IAudioDataSource` (interface)
Audio sample provider. `IsActive`, `ChannelCount`, `Read<S>(Span<S>, AudioSimulator)`.

#### `IAudioDeviceOutput` (interface)
Hardware output callback: `AudioFrameRendered(float[] buffer, double dspTime)`.

---

### Debug Sources

- `DebugSineAudioSource` -- sine wave at given frequency
- `DebugModulatedSineAudioSource` -- frequency-modulated sine
- `DebugSimplexAudioSource` -- simplex noise at given frequency

---

### Utilities

#### `AudioClipRenderHelper`
`RenderAudioClip(system, lengthSeconds, onSetup, onUpdate)` -- renders an `AudioX` clip offline by running the simulator in a loop.

#### `BufferSanitizer`
`SanitizeBuffer(float[])` -- clamps to [-4096, 4096], clears buffer on NaN.
