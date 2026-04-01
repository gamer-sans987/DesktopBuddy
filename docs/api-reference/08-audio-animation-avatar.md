# Audio, Animation, and Avatar Reference

Covers audio pipeline, animation, dynamic bones, and avatar systems.

---

## Audio

### AudioOutput

Category: `Audio`. Spatializes and outputs audio from a source in 3D space.

**Key Fields:**
- `Volume` (float, 0-1) -- final volume also factors in `AudioTypeGroup` volume and `ActiveUser.LocalVolume`
- `Source` (DestroyRelayRef\<IWorldAudioDataSource\>) -- the audio data source to play
- `SpatialBlend` (float, 0-1) -- 0 = 2D/global, 1 = fully spatialized
- `Spatialize` (bool, default true)
- `DopplerLevel` (float, 0-1)
- `Pitch` (float, 0.5-2.0)
- `Global` (bool?) -- if null, inferred from `SpatialBlend ~= 0`
- `RolloffMode` (AudioRolloffCurve, default LogarithmicFadeOff)
- `MinDistance` / `MaxDistance` (float, defaults 1 / 500)
- `Priority` (int, default 128)
- `AudioTypeGroup` (default SoundEffect)
- `DistanceSpace` (AudioDistanceSpace, default Local) -- `Local` scales min/max distances by slot's global scale
- `MinScale` / `MaxScale` -- clamp for scale factor when `DistanceSpace == Local`
- `IgnoreAudioEffects` (bool) -- bypasses reverb/effects chain
- `ExcludedListeners` (SyncRefList\<AudioListener\>)
- `excludedUsers` (SyncRefList\<User\>, protected)

**Key Behavior:**
- `ShouldBeEnabled` checks: not removed, enabled, slot active, source non-null, local user not excluded, active user not audio-blocked.
- `ActualVolume` = `Volume * AudioTypeGroupVolume * ActiveUser.LocalVolume`, clamped and filtered for NaN/Inf.
- `GetActualDistances()` -- when `DistanceSpace == Local`, multiplies distances by `AvgComponent(Slot.GlobalScale)` clamped to `[MinScale, MaxScale]`.
- `SetupAsUI()` -- sets AudioTypeGroup to UI, zeroes DopplerLevel, enables IgnoreAudioEffects.
- User exclusion methods: `ExludeUser`, `ExludeLocalUser`, `RemoveExludedUser`, `ClearExludedUsers`, `IsUserExluded` (note: typo "Exlude" is in the actual API).
- Legacy loading: adapts old DopplerLevel/IgnoreReverbZones fields via converters.

**Gotchas:**
- The typo `Exlude` (missing 'c') is baked into the API -- use it as-is.
- `DistanceSpace` defaults to `Local`, meaning distances scale with slot. Old data without `DistanceSpace` key gets migrated to `Global`.
- Setting `MaxDistance` to `float.PositiveInfinity` makes audio audible everywhere (skips scale multiplication).

---

### AudioClipPlayer

Category: `Audio`. Extends `AudioClipPlayerBase`. Plays an `AudioClip` asset.

**Key Fields:**
- `Clip` (AssetRef\<AudioClip\>) -- the audio clip to play
- Inherits `playback` (SyncPlayback) from base class

**Key Properties:**
- `ClipLength` -- duration from clip data, 0 if unavailable
- `ChannelCount` -- from clip data
- `BaseRate` -- from clip asset
- `SampleRate` -- from clip data
- `CanBeActive` -- true when `Clip.Asset.Data != null`

**Key Behavior:**
- Implements `IItemMetadataSource` -- exposes `ItemName` (slot name) and `ItemTags` (includes AudioClip tag, asset URL, clip length).
- `Read(float[], offset, count)` dispatches to mono or stereo read paths based on channel config.
- Updates clip length on target change, start, and changes.

---

### StaticAudioClip

Category: `Assets`. Extends `StaticAssetProvider<AudioClip, ...>`. Hosts an audio clip from a URL with processing tools.

**Key Fields:**
- `URL` (inherited) -- the asset URI
- `LoadMode` (AudioLoadMode, default Automatic)
- `SampleRateMode` (SampleRateMode, default Conform)

**Processing Methods (all return Task\<bool\>):**
- `Normalize()`, `AdjustVolume(ratio)`, `ExtractSides()`, `Denoise()` (RNNoise)
- `TrimSilence()`, `TrimStartSilence()`, `TrimEndSilence()` -- with optional amplitude threshold
- `TrimStart(duration)`, `TrimEnd(duration)`
- `FadeIn(duration)`, `FadeOut(duration)`, `MakeFadeLoop(duration)`
- `ConvertToWAV()`, `ConvertToVorbis()`, `ConvertToFLAC()`
- `ApplyZitaReverb(ZitaParameters)`

**Key Behavior:**
- Processing: downloads original audio data, runs transform, saves to LocalDB, updates URL.
- Inspector UI builds buttons for all processing operations plus format info display.
- Button-triggered processing uses undo batches.

**Gotchas:**
- Processing waits for asset to load (`while (Asset == null) await NextUpdate`).
- On error, button label changes to red error text; check logs for details.

---

## Animation

### Animator

Category: `Rendering`. Drives fields from an animation clip over time.

**Key Fields:**
- `_playback` (SyncPlayback) -- controls play state, position, speed, loop
- `Clip` (AssetRef\<Animation\>) -- the AnimX asset
- `Fields` (SyncList\<DriveRef\<IField\>\>) -- target fields, mapped 1:1 to animation tracks by index

**Key Properties (from SyncPlayback):**
- `IsPlaying`, `IsFinished`, `Loop`, `NormalizedPosition`, `Position`, `Speed`, `ClipLength`, `IsStreaming`

**Key Methods:**
- `Play()`, `Pause()`, `Resume()`, `Stop()`, `TogglePlayback()`
- `SetupFieldsByName(Slot root)` -- auto-maps tracks to fields by matching `track.Node` to slot names and `track.Property` to field names. Property format: `"fieldName"` or `"ComponentType.fieldName"`.

**Key Behavior:**
- `OnCommonUpdate` updates `ClipLength` from asset, regenerates field mappers if invalid, then samples all fields at current position.
- `FieldMapper<T>` samples the track at the current time and writes to the driven field.
- Field mappers are invalidated when `Clip` or `Fields` change.
- `SetupFieldsByName` skips already-driven fields (jumps to next matching slot via `ignoreSlots`).

**Gotchas:**
- Fields list must align by index with animation tracks -- mismatched order produces wrong results.
- If a track type doesn't match the field type, the mapper sets `default(T)`.
- `SetupFieldsByName` waits for clip to be available before proceeding.

---

## Dynamic Bones

### DynamicBoneChain

Category: `Physics/Dynamic Bones`. Physics-based bone chain simulation with collision, grabbing, and stretch.

**Key Fields (Physics):**
- `Inertia` (float, 0-1, default 0.2)
- `InertiaForce` (float, -10 to 10, default 2)
- `Damping` (float, 0-100, default 5)
- `Elasticity` (float, 0-1000, default 100)
- `Stiffness` (float, 0-1, default 0.2)
- `Gravity` (float3) + `GravitySpace` + `UseUserGravityDirection` (default true)
- `LocalForce` (float3)
- `GlobalStretch` (float, 0.1-2, default 1) / `MaxStretchRatio` (1-2, default 1) / `StretchRestoreSpeed` (default 6)
- `SimulateTerminalBones` (bool, default true) -- adds virtual end bones for leaf nodes

**Key Fields (Collision):**
- `DynamicPlayerCollision` (bool, default true)
- `CollideWithOwnBody`, `CollideWithHead`, `CollideWithBody`, `CollideWithLeftHand`, `CollideWithRightHand` (all default true except own body)
- `StaticColliders` (SyncRefList\<IDynamicBoneCollider\>)
- `BaseBoneRadius` (float, default 0.025)
- `HandCollisionVibration` (VibratePreset, default None)

**Key Fields (Grabbing):**
- `IsGrabbable` (bool)
- `ActiveUserRootOnly` (bool) -- restricts grab to avatar owner
- `AllowSteal` (bool, default true)
- `GrabPriority` (int)
- `IgnoreGrabOnFirstBone` (bool)
- `GrabRadiusTolerance` (float, 1-4, default 1.25)
- `GrabReleaseDistance` (float, default 1)
- `GrabSlipping` (bool) -- effector can slide along chain
- `GrabTerminalBones` (bool)

**Key Fields (Space):**
- `UseLocalUserSpace` (bool, default true)
- `SimulationSpace` (RootSpace)

**Bone Inner Class Fields:**
- `BoneSlot` (SyncRef\<Slot\>)
- `OrigPosition` / `OrigRotation` -- rest pose
- `RadiusModifier` (float, 0-1, default 1)
- `GrabOverride` (SyncRef\<Bone\>)
- `Collide` (bool, default true)
- `_posDrive` / `_rotDrive` (FieldDrive) -- force-linked to slot transform

**Key Behavior:**
- Simulation runs in 3 phases: `Prepare()` (validates data, builds collision mask), `RunSimulation()` (physics step in simulation space), `FinishSimulation()` (writes back to slots, handles grab).
- Bones are sorted by hierarchy depth; parent indices computed from slot hierarchy.
- Terminal bones (leaf nodes) get virtual extensions when `SimulateTerminalBones` is true.
- Collision mask is a bitmask: head=1, left hand=2, right hand=4, body=8, dynamic=16.
- `ScheduleCollision` filters by mask and own-body/hand ignore settings.
- Grab slipping: effector slides to nearest bone if `GrabSlipping` is enabled.
- Debug visualization: `VisualizeColliders` and `VisualizeBones` (both NonPersistent, authority-only).

**Gotchas:**
- `Bone.Assign()` uses `ForceLink` on position/rotation drives -- will override existing drives.
- Data invalidation triggers full rebuild on next `Prepare()` (bones re-sorted, parent indices recomputed).
- Chain won't simulate if `GlobalScale` has any near-zero component.
- `IsGrabbable` implements `IGrabbable` but `Scalable` is always false and `AllowOnlyPhysicalGrab` is always true.

---

## Avatar System

### AvatarCreator

Globally registered component. Spawns the avatar creation wizard with anchor points for head, hands, feet, pelvis.

**Constants:**
- `HEADSET_DETECTION_RADIUS` = 0.2
- `CONTROLLER_DETECTION_RADIUS` = 0.15
- `CONTROLLER_SEPARATION_DISTANCE` = 0.5
- `FEET_SEPARATION_DISTANCE` = 0.3

**Key Fields:**
- `_headsetPoint`, `_leftPoint`, `_rightPoint`, `_leftFootPoint`, `_rightFootPoint`, `_pelvisPoint` -- anchor slots
- `_useSymmetry` (default true) -- mirrors left anchors to right via `MirrorTransform`
- `_setupEyes`, `_setupFaceTracking`, `_setupProtection`, `_setupVolumeMeter` -- creation options
- `_calibrateFeet`, `_calibratePelvis` -- optional tracker calibration
- `_scale` (float, default 1)

**Key Behavior:**
- `OnAttach` builds the full wizard: spawns headset/hand/foot/pelvis models, tool anchors (Tooltip, Grabber, Shelf), and a UI panel.
- `RunCreate()` uses sphere overlaps at each anchor point to find objects, then calls `CreateAvatar(...)`.
- `CreateAvatar` (static): if a `BipedRig` is found, sets up VRIK-based avatar; otherwise creates a simple node-based avatar. Attaches `ObjectRoot`, `AvatarGroup`, `AvatarRoot`, `Grabbable`, and optionally `SimpleAvatarProtection`.
- `CreateBipedAvatar` (public static) -- simplified entry point for programmatic avatar creation.
- Eye setup: searches blendshapes by name heuristics (English: blink/wink/eye close; Japanese: unicode names for blink/wink). Scores candidates and picks best match.
- Symmetry: when enabled, left-side anchors get `MirrorTransform` mirroring from right side.
- Protection requires non-empty `UserID`; UI disables creation button if protection requested but unavailable.

**Gotchas:**
- Avatar creator destroys itself (`base.Slot.Destroy()`) after creation.
- `IMaterialApplyPolicy.CanApplyMaterial` returns false -- blocks material drops on the wizard.
- Japanese blendshape names are hardcoded for VRChat model compatibility.

### SimpleAvatarProtection

Likely a simple marker component attached to avatar slots and mesh renderers to prevent unauthorized modification. No source available.

### AvatarAnchor

No source available.

---

## Locomotion

### Locomotion

No source available. Referenced by `DynamicBoneChain` as `LocomotionController` (retrieved via `ActiveUserRoot.GetRegisteredComponent<LocomotionController>()`).

---

## ParticleSystem

No source available.

---

## BoneChain

No source available. `DynamicBoneChain` does not extend it -- it extends `Component` directly.

---

## Procedural Audio Clips

All inherit from `ProceduralAudioClip` -> `ProceduralAssetProvider<AudioClip>`.

### Periodic Waveforms (base: `PeriodicWaveClip`)

| Type | Waveform |
|---|---|
| `SineWaveClip` | Sine wave |
| `SquareWaveClip` | Square wave |
| `SawtoothWaveClip` | Sawtooth wave |
| `TriangleWaveClip` | Triangle wave |
| `SimplexNoiseClip` | Simplex noise-based periodic audio |

Fields: `Frequency`, `Amplitude`.

### ValueNoiseClip
Generates random noise audio with configurable `Duration` and `Seed`.

---

## Procedural Animations

### ProceduralAnimation (abstract)
Base for procedurally generated animation assets. Extends `ProceduralAssetProvider<Animation>`.

### DynamicSubtitleProvider
Generates `AnimX` animation data from subtitle files (SRT/VTT) via URL.

---

## Procedural Fonts

### ProceduralFont (abstract)
Base for procedurally generated font assets. Extends `ProceduralAssetProvider<Font>`.

### DynamicSpriteFont
Dynamically builds a bitmap font from individual sprite images. Each sprite glyph has a Unicode codepoint, bearing, advance, size, and tintable flag.
