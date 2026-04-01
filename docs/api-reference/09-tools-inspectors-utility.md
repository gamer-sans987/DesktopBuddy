# Tools and Utility Reference

---

## SceneInspector

**Extends:** `InspectorPanel` | **Implements:** `INoDestroyUndo`, `IDeveloperInterface`, `IObjectRoot`

The in-world hierarchy + component inspector panel.

### Key Fields

| Field | Type | Purpose |
|---|---|---|
| `Root` | `RelayRef<Slot>` | Root slot of the hierarchy view |
| `ComponentView` | `SyncRef<Slot>` | Currently selected slot (detail pane) |
| `_hierarchyContentRoot` | `SyncRef<Slot>` | Container for hierarchy UI |
| `_componentsContentRoot` | `SyncRef<Slot>` | Container for component list UI |

### Behavior

- `OnChanges()` rebuilds UI when `Root` or `ComponentView` changes. Runs only on authority.
- Changing `ComponentView` triggers `SetActiveSlotGizmo` on all local user DevTools (only when synchronous changes are allowed).
- Destroying the inspector removes the gizmo from `_currentComponent`.
- "Object Root" button navigates up to the nearest `IObjectRoot` or `GetObjectRoot()`, whichever is deeper.
- "Attach Component" opens a `ComponentSelector` in local user space, linked via `DestroyProxy` so it dies with the inspector.
- If `Root.IsTargetRemoved`, the entire inspector slot self-destructs.

### Gotchas

- `Root` is a `RelayRef`, not a `SyncRef` -- it relays from elsewhere and should not be set arbitrarily.
- All button handlers are `[SyncMethod]` -- they execute on all clients.
- Undo integration: `InsertParent` wraps multiple operations in `BeginUndoBatch`/`EndUndoBatch`.

---

## WorkerInspector

**Extends:** `Component` | **Implements:** `IDeveloperInterface`

**Old type name:** `FrooxEngine.ComponentInspector`

Builds and maintains the property inspector UI for a worker (slot, component, user, stream).

### Key Fields

| Field | Type | Purpose |
|---|---|---|
| `_targetContainer` | `SyncRef<Worker>` | The slot/user whose components are displayed |
| `_workerFilter` | `SyncDelegate<Predicate<Worker>>` | Optional filter for which workers to show |
| `_targetWorker` | `SyncRef<Worker>` | Single worker target (non-container mode) |

### Methods

- `Create(Slot, Worker, Predicate<ISyncMember>)` -- static factory. Sets up a panel (660x1600), scales to 0.0005.
- `SetupContainer(Worker, ...)` -- populates UI for all components on a slot/user. Skips `GizmoLink` components.
- `Setup(Worker, ...)` -- single-worker mode.
- `BuildInspectorUI(Worker, UIBuilder, Predicate<ISyncMember>)` -- static. Iterates `SyncMemberCount`, skips `[HideInInspector]`, calls `SyncMemberEditorBuilder.Build`. Also builds sync methods.

### Behavior

- Registers for `ComponentAdded`/`ComponentRemoved` events on the target slot (or user component/stream events).
- When a component is added/removed, UI is rebuilt via `RunSynchronously`.
- Component removal respects `EditSettings.ConfirmComponentDestroy` -- if true, shows a context menu confirm.
- If `_targetWorker` is removed, destroys the parent `LegacyPanel`.
- Workers implementing `ICustomInspector` get `BuildInspectorUI` called instead of the default member iteration.

### Gotchas

- `OnWorkerTypePressed` handler is empty -- the button label exists but does nothing on click.
- Exception during `ICustomInspector.BuildInspectorUI` is caught and displayed as text.

---

## ComponentAttacher

No source available.

---

## UndoManager

No source available.

---

## LocalDB

No source available.

---

## AssetManager

Engine-level asset fetching, variant generation, and caching system. Not a Component -- standalone class.

### Key Properties

| Property | Type | Notes |
|---|---|---|
| `UnloadDelaySeconds` | `float` | 15s on Windows, 5s on Android |
| `WhiteTexture` / `BlackTexture` / `ClearTexture` / `DarkCheckerTexture` | `Texture2D` | Built-in fallback textures (4x4 or 128x128) |
| `TotalBytesPerSecond` | `long` | From the internal `EngineAssetGatherer` |
| `TextureSettings` | `TextureQualitySettings` | Updated via `Settings.RegisterComponentChanges` |

### Key Methods

- `GatherAsset(Uri, float priority, DB_Endpoint?)` -- fetches an asset. Returns `ValueTask<GatherResult>`.
- `GatherAssetFile(...)` -- convenience, returns file path string.
- `RequestAsset<A>(Uri, descriptor, requester, metadata)` -- requests a typed asset variant via `AssetVariantManager`.
- `RequestVariant(Uri, variantId, descriptor, generateVariant, waitForCloud)` -- full variant pipeline: local check, cloud fetch, local generation fallback.
- `RequestMetadata<T>(Uri, waitOnCloud)` -- deduplicates concurrent metadata requests per (type, url) pair.
- `ForceUpdateAllTextures()` -- marks all `StaticTexture2D` components dirty across all worlds.

### Supported Schemes

`local`, `http`, `https`, `ftp`, and the cloud DB scheme.

### Variant Seeding

On Windows, automatically seeds mobile (ETC2) texture variants, mesh collider variants (LZ4/LZMA), Gaussian splat quality variants, and normal map variants to the cloud. Seeding is delayed 1-2 minutes.

### Gotchas

- `ForceGenerateVariants` static flag exists for debugging but explicitly warned against for production.
- `RequestAsset` has a bug: condition `variantDescriptor == null && variantDescriptor.CorrespondingAssetType != typeof(A)` should be `!=` on the first check (null guard is wrong, will NPE).
- Disposed check throws if accessed after disposal.

---

## UpdateManager

Per-world update orchestrator. Manages startup, update, change application, destruction, and audio update queues.

### Key Properties

| Property | Type | Notes |
|---|---|---|
| `World` | `World` | Owning world |
| `CurrentlyUpdatingUser` | `User` | Falls back to `World.LocalUser` if null |
| `CurrentlyUpdating` | `IUpdatable` | The element currently being processed |

### Update Pipeline

1. `RunStartups()` -- slots first, then other updatables. Dequeues from separate queues.
2. `PrepareUpdateCycle()` / `RunUpdates()` / `FinishUpdateCycle()` -- sorted by `UpdateOrder` (int key buckets).
3. `PrepareChangesCycle()` / `RunChangeApplications()` / `FinishChangeUpdateCycle()` -- processes dirty elements, also bucketed by order.
4. `RunDestructions()` -- queued destruction.
5. `RunAudioUpdates()` -- copies list under spinlock, then updates outside lock.

### Exception Handling

Components that throw during update are handled per `[ExceptionHandling]` attribute:
- `Disable` (default) -- disables the component
- `DeactivateSlot` -- disables component + slot (unless root/protected)
- `Destroy` -- destroys the component
- `DestroySlot` -- destroys the slot (falls back to component if root/protected)
- `DestroyUserRoot` -- destroys the `ActiveUserRoot` or object root

### Gotchas

- `Changed()` can be called from background threads -- uses `SpinLock` and `SpinQueue` buffer.
- `OutOfMemoryException` triggers `Engine.ForceCrash()`.
- `NestCurrentlyUpdating` / `PopCurrentlyUpdating` must be balanced -- mismatch throws `InvalidOperationException`.
- `UpdateBucketChanged` defers moves during active update iteration to avoid collection mutation.
- Method name typo: `ActiveStateChagned` (in the actual codebase).

---

## CoroutineManager

**Extends:** `SynchronizationContext` | Per-world coroutine and async task scheduler.

### Key Methods

- `StartCoroutine(IEnumerator<Context>, onDone, updatable)` -- returns `Coroutine` handle.
- `StartTask(Func<Task>, updatable)` / `StartTask<T>(...)` -- starts an async task on the world context. Sets up `SynchronizationContext` so awaits resume on world thread.
- `StartBackgroundTask(...)` -- runs via `Task.Run`, does NOT set sync context (awaits resume on thread pool).
- `RunInSeconds(float, Action)` / `RunInUpdates(int, Action)` -- convenience wrappers around coroutines.
- `PostDelayed(callback, state, updates)` -- delayed post by update count.
- `PostToSync(callback, state)` -- posts to the sync queue (separate from world queue).

### Coroutine Context System

Coroutines yield `Context` values:
- `Context.TargetContext.World` -- resume on world thread
- `Context.TargetContext.Background` -- resume on background thread
- `Context.TargetContext.Coroutine` -- start a sub-coroutine
- `Context.TargetContext.Job` -- wait for a job, optionally continue in background

Delays: `Context.WorldDelay.Updates` (count), `Context.WorldDelay.Seconds` (real time).

### Gotchas

- Uses `AsyncLocal<CoroutineManager>` and `AsyncLocal<IUpdatable>` for ambient context.
- All started tasks get `ContinueWith` for exception logging.
- `ShouldStop` on the handle cancels immediately, returning it to pool.
- Coroutine exceptions are caught and logged but do NOT propagate -- the whole chain is finished.

---

## SecurityManager

Engine-level host access control. Not a Component.

### Access Scopes

`HostAccessScope`: `HTTP`, `Websocket`, `OSC_Sender`, `OSC_Receiver`, `Everything`

### Key Methods

- `CanAccess(host, port, scope)` -- returns `bool?` (null = not configured, needs user prompt).
- `RequestAccessPermission(host, port, scope, reason)` -- async. Shows `HostAccessDialog` in userspace if not configured.
- `TemporarilyAllowHTTP(host)` / `TemporarilyAllowWebsocket(host, port)` / `TemporarilyAllowOSC_*` -- runtime overrides.

### Behavior

- Max 5 concurrent access request dialogs (`MAX_ACTIVE_REQUESTS`). Beyond that, denied automatically.
- Headless and camera modes auto-deny all requests.
- User decisions are persisted via `Settings.UpdateActiveSetting<HostAccessSettings>`.
- `Everything` scope checks all sub-scopes; returns null if any is null, false if any is false.

### Gotchas

- `TemporarilyAllowOSC_Receiver` stores key as host=`"localhost"` with scope `OSC_Sender` -- appears to be a copy-paste inconsistency.
- For HTTP scope, port is forced to 0 in the access key (port-agnostic).

---

## PhotoCaptureManager

**Extends:** `Component` | **Old type name:** `FrooxEngine.FingerPhotoGesture`

Handles finger-gesture and input-binding photo capture with preview, timer, and stereo support.

### Key Fields

| Field | Type | Default |
|---|---|---|
| `FingerGestureEnabled` | `Sync<bool>` | true |
| `MinDistance` / `MaxDistance` | `Sync<float>` | 0.1 / 0.5 |
| `MinFOV` / `MaxFOV` | `Sync<float>` | 20 / 90 |
| `PreviewResolution` | `Sync<int2>` | 1920x1080 |
| `NormalResolution` | `Sync<int2>` | 1920x1080 |
| `TimerResolution` | `Sync<int2>` | 2560x1440 |
| `TimerSeconds` | `Sync<float>` | 10 |
| `CaptureStereo` | `Sync<bool>` | false |
| `StereoSeparation` | `Sync<float>` | 0.065 |
| `EncodeFormat` | `Sync<PhotoEncodeFormat>` | WebP |

### Capture Flow

1. Finger gesture or input binding charges `_fingerGestureCharge` / `_bindingCaptureCharge` to 1.0.
2. At full charge, take-photo or start-timer triggers.
3. `TakePhoto(rootSpace, resolution, addTemporaryHolder)` renders to bitmap, saves via `LocalDB`, spawns a textured quad with `PhotoMetadata`, `Grabbable`, `BoxCollider`.
4. Timer mode: preview scales up, countdown displayed, fast/slow audio transition at T-2s.

### Gotchas

- Photos in worlds where `CanSpawnObjects()` is false are saved under local user root at Y=-10000, then immediately destroyed (for cloud save only).
- Temporary holder slots are named `"PhotoTempHolder"` and excluded from renders.
- `HideAllNameplates` collects all badge roots for exclusion.
- Stereo creates a side-by-side bitmap (2x width).
- Settings sync is done in `RunSynchronously` callback, not directly.

---

## CloudSpawner

No source available.

---

## InventoryBrowser

**Extends:** `BrowserDialog` | `[GloballyRegistered]`

In-world inventory browsing UI.

### Key Fields

| Field | Type | Purpose |
|---|---|---|
| `CustomItemSpawn` | `SyncDelegate<Action<Record>>` | Override for item spawn behavior |
| `_currentPath` | `Sync<string>` | Current directory path |
| `_currentOwnerId` | `Sync<string>` | Owner of current directory |
| `_user` | `UserRef` | Linked user |

### Special Item Types

`Avatar`, `World`, `VirtualKeyboard`, `InteractiveCamera`, `Facet`, `AudioPlayer`, `VideoPlayer`, `TextDisplay`, `UrlDisplay`, `DocumentDisplay`, `AudioStreamController`, `ProgressBar`, `WorldLoadingIndicator`, `ColorDialog`

### Access Control

`CanInteract(User)` -- strict: must be the linked user, on the local machine, with matching MachineID and CloudId. Always allowed in userspace.

### Static

- `INVENTORY_ROOT` = `"Inventory"`
- `CurrentUserspaceInventory` -- singleton reference to the userspace inventory browser.

### Gotchas

- Cannot write to directories owned by the platform group (`Cloud.Platform.GroupId`).
- Listens for `RecordManager.RecordSaved` to live-update the current view.
- Registers favorite change listeners for all `FavoriteEntity` enum values on awake.

---

## RecordDirectory

Data model for cloud/local inventory directory structure. Not a Component.

### Load States

`NotLoaded` -> `LocalCache` -> `FullyLoaded`

### Key Properties

| Property | Type | Notes |
|---|---|---|
| `OwnerId` | `string` | Owner user/group ID |
| `Path` | `string` | Backslash-delimited path |
| `CanWrite` | `bool` | True if owner is current user or a group the user belongs to |
| `IsLink` | `bool` | True if backed by a link record |
| `Subdirectories` | `IReadOnlyList<RecordDirectory>` | |
| `Records` | `IReadOnlyList<Record>` | |

### Key Methods

- `EnsureFullyLoaded()` -- async, fetches from cloud if needed.
- `TryLocalCacheLoad()` -- loads from LocalDB cache.
- `AddSubdirectory(name, dummyOnly)` -- creates directory record and saves unless `dummyOnly`.
- `AddItem(name, objectData, thumbnail, tags)` -- creates object record, saves + uploads async.
- `DeleteSubdirectory(dir)` -- recursive async deletion.
- `SetPublicRecursively(bool, followLinks)` -- updates `IsPublic` on all nested records.
- `GetSubdirectoryAtPath(path)` -- async, fully loads each level.

### Gotchas

- Uses `ActionBlock` with `MaxDegreeOfParallelism=1` for cache writes (serialized).
- Record caching is delayed 5 seconds after cloud fetch.
- Duplicate subdirectory/record names are deduplicated by `HashSet<string>`.
- Path separator is backslash (`\`), not forward slash.
- `ReplaceRecord` returns false (skip) if the old record `CanOverwrite` the new one or is the same version.

---

## ObjectRoot

**Category:** `Transform` | `[SingleInstancePerSlot]`

Marks a slot as the root of a logical object.

### Key Concepts

- `IsPure` -- true if the slot has no components besides ObjectRoot and "consolidated" types (currently just `Grabbable`).
- `MergeInto(roots, rootName)` -- merges multiple ObjectRoots under a single pure root.
- `MergeWith(target)` -- moves this object under target; if pure, destroys own slot into target.
- `EnsurePure(name)` -- if not pure, wraps in a new parent slot, transfers consolidated components.

### Custom Inspector

Adds a "Remove all children object roots" button via `ICustomInspector`.

---

## DestroyOnUserLeave

**Category:** `Transform`

Destroys the slot when the targeted user leaves the session.

### Fields

| Field | Type |
|---|---|
| `TargetUser` | `UserRef` |

### Gotchas

- Only runs on authority (`World.IsAuthority`).
- Checks `user == TargetUser.RawTarget` (reference equality on the raw target, not resolved user).

---

## DuplicateBlock

**Category:** `Transform/Tagging` | `[SingleInstancePerSlot]` (implied by interface)

Implements `IDuplicateBlock` / `IInteractionBlock`. Has no fields beyond the base component fields. Presence on a slot prevents duplication.

---

## Comment

Simple metadata component. Single field:

| Field | Type |
|---|---|
| `Text` | `Sync<string>` |

No category attribute. No behavior beyond storing the string.

---

## Bookmark

No source available.

---

## AnchoredPopup

No source available.

---

## WorldOrb

**Category:** `World` | **Implements:** `ITouchable`, `IWorldLink`, `IMaterialApplyPolicy`, `IItemMetadataSource`

Represents a joinable world as a 3D orb with thumbnail, metadata, and interaction.

### Key Fields

| Field | Type | Notes |
|---|---|---|
| `URL_Field` | `Sync<Uri>` | World record URL |
| `ActiveSessionURLs_Field` | `SyncFieldList<Uri>` | Live session URLs |
| `CreateIfNotExists_Field` | `SyncDelegate<WorldCreator>` | Factory for new sessions |
| `OpenActionOverride` | `SyncDelegate<Action<WorldOrb>>` | Custom open behavior |
| `Visit` | `Sync<VisitState>` | `Visited`, `Updated`, `New` |
| `ActiveUsers` | `Sync<int>` | Current user count |
| `IsPublic` | `Sync<bool>` | |
| `LongPressTime` | `Sync<float>` | Seconds for long-press action |

### Constants

- `RADIUS` = 0.05
- Color constants: `ACTIVE_SESSION_COLOR` (pink), `EMPTY_SESSION_COLOR` (purple), `NEW_COLOR` (warm), `OPENED_COLOR` (orange), `FOCUSED_COLOR` (cyan)

### Gotchas

- `IMaterialApplyPolicy.CanApplyMaterial` returns false -- you cannot drag materials onto world orbs.
- Version = 5, indicating migration history.
- `LocalOpenedWorld` and `LocalFocused` are not synced (plain fields).

---

## Spinner

**Category:** `Transform/Drivers`

Continuously rotates a slot based on world time.

### Fields

| Field | Type | Notes |
|---|---|---|
| `Range` | `Sync<float3>` | Rotation range limit (default `float3.MaxValue`) |
| `_speed` | `Sync<float3>` | Degrees per second per axis |
| `_offset` | `Sync<floatQ>` | Base rotation offset |
| `_target` | `FieldDrive<floatQ>` | Driven rotation field |

### Behavior

- `RawRotation = Euler(speed * WorldTime % Range)`
- `Rotation = offset * RawRotation`
- Setting `Speed` preserves current visual rotation by recalculating offset.
- On disable, resets target to offset (stops spinning, holds last offset).
- Uses `SetupValueSetHook` to intercept external writes to the driven field.

---

## Wiggler

**Category:** `Transform/Drivers`

Applies simplex-noise-based rotation wobble to a slot.

### Fields

| Field | Type | Notes |
|---|---|---|
| `_speed` | `Sync<float3>` | Noise frequency per axis |
| `_magnitude` | `Sync<float3>` | Noise amplitude (degrees) per axis |
| `_seed` | `Sync<float3>` | Noise seed (randomized on attach, range 0-1024) |
| `_offset` | `Sync<floatQ>` | Base rotation |
| `_target` | `FieldDrive<floatQ>` | Driven field |

### Behavior

- `Wiggle = Euler(SimplexNoise(WorldTime * Speed.x + Seed.x) * Magnitude.x, ...)`
- Setting Speed, Magnitude, or Seed preserves the current visual rotation.
- Same disable/hook pattern as Spinner.

---

## Panner

No source available.

---

## SessionControlDialog

**Extends:** `Component` | `[ExceptionHandling(ExceptionAction.DestroySlot)]`

Userspace-only dialog for session settings, user management, and permissions.

### Tabs

`Settings`, `Users`, `Permissions`

### Key Fields

References to synced UI controls for: world name, description, max users, away kick, autosave, auto cleanup, mobile friendly, hide from listing, access level, custom join verifier, edit mode, Resonite link.

Uses `WorldValueSync<T>` components to bridge world configuration values into the dialog UI.

### Behavior

- `UserspaceOnly = true` -- cannot exist in normal worlds.
- Tab switching rebuilds UI via `SlideSwapRegion.Swap`.
- `[ExceptionHandling(DestroySlot)]` -- if the dialog throws, the entire slot is destroyed.

---

## RadiantDashScreen

**Extends:** `Component` | **Implements:** `IUIContainer`

Base class for screens on the Radiant dash (main menu system).

### Key Fields

| Field | Type | Notes |
|---|---|---|
| `Icon` | `Sync<Uri>` | Screen icon URL |
| `ActiveColor` | `Sync<colorX?>` | Override color; if null, computed from icon's average HSV |
| `Label` | `Sync<string>` | Screen label |
| `ScreenEnabled` | `Sync<bool>` | Default true |
| `BaseResolution` | `Sync<float2>` | Default 1920x1080 |

### Key Properties

- `ScreenRoot` -- the slot containing the screen content
- `ScreenCanvas` -- the canvas for the screen
- `IsShown` -- whether ScreenRoot is active
- `AlwaysSwitchable` -- virtual, default false

### Methods

- `Show()` / `Hide()` -- activates/deactivates ScreenRoot.
- `CloseContainer()` -- switches dash away from this screen, then destroys after it's hidden.
- `SetResolution(float2)` -- updates base resolution and aspect ratio.
- `BuildBackground(UIBuilder)` -- applies circular pattern texture (or flat color in Universe mode).

### Gotchas

- Has a `SetupModalOverlay` retry with `RunInSeconds(1f, ...)` if canvas is null at setup time.
- `OnLoading` migration: if `BaseResolution` is missing from save data, reads it from the canvas size.
- `CurrentColor` computation: saturates HSV if s > 0.5, forces v=1.

---

## ScreenController

**Extends:** `UserRootComponent` | **Implements:** `IInputUpdateReceiver` | `[DefaultUpdateOrder(-20000000)]`

Desktop/screen-mode view controller. Manages camera targeting modes and simulated head/hand positions.

### View Targetting Modes

| Mode | Controller Type | Purpose |
|---|---|---|
| First Person | `FirstPersonTargettingController` | Default desktop view |
| Third Person | `ThirdPersonTargettingController` | Over-the-shoulder |
| UI Camera | `UI_TargettingController` | Focus on a UI panel |
| Freeform | `FreeformTargettingController` | Free-flying camera |
| Userspace | `UserspaceTargettingController` | For userspace world |

### Key Properties

- `ViewPosition` / `ViewRotation` -- interpolated between old and new view during transitions.
- `ScreenActivationLerp` -- 0 in VR, 1 in screen mode, with smooth transition.
- `ActualHeadPosition` / `ActualHeadRotation` -- blended between UserRoot tracking and simulated.
- `LocomotionReference` -- delegated to active targeting controller.

### Input Bindings

- Toggle first/third person
- Toggle freeform camera
- Focus/Unfocus (activates freeform, auto-deactivates on unfocus if focus-activated)

### Key Methods

- `FocusUI(IUIInterface)` -- switches to UI camera mode, saves previous.
- `FocusFreecam(Slot, toggle)` -- focuses freeform camera on a slot.
- `FilterDeviceNode(...)` -- rewrites head/hand tracking data in screen mode using simulators and `_screenActivationLerp` blend.
- `BeforeInputUpdate()` -- main update: activates/deactivates screen mode, transitions view, updates simulators.
- `ValidateViewTargetting()` -- enforces `ScreenViewPermissions`.

### Gotchas

- `TransitionSpeed` default = 4. View transition lerp rate = 8 (hardcoded, separate from TransitionSpeed).
- `NotifyOfSecondaryActivity` sets `_externalPrimaryActivity` (copy-paste bug -- should set secondary).
- Permission validation falls back through first-person -> third-person -> null.
- `GetSpawnPoint()` checks if window is focused before using laser direction.

---

## GizmoHelper

Static utility class for managing component gizmos.

### Constants

- `RADIUS` = 0.05

### Key Methods

- `GetGizmoType(Type componentType)` -- looks up the gizmo type registered via `[GizmoForComponent]` attribute.
- `GetGizmo(Worker, Type, isExplicit)` -- creates gizmo if not present. Attaches `GizmoLink`. If explicit, notifies all DevTools.
- `TryGetGizmo(Worker, Type)` -- returns existing gizmo or null. Returns null for local elements.
- `RemoveGizmo(Worker)` -- destroys the gizmo slot.
- `IsGizmoActive(Worker)` / `ActivateGizmo` / `DeactivateGizmo` -- activation state management.
- `SetupMaterial(OverlayFresnelMaterial, colorX)` -- configures a gizmo overlay material with fresnel-based coloring (front/behind, near/far).

### Gotchas

- `Initialize(Type[])` should only be called once (checks `_componentGizmos != null`).
- Multiple gizmos for the same component type logs an error but silently drops the duplicate.
- `ShouldSpawnOnDevModeEnabled` checks `[SpawnOnDevModeEnabled]` attribute.

---

## WorldConfiguration

**Extends:** `SyncObject` | **Implements:** `IUpdatable`

Persisted + runtime session configuration. Attached to every world.

### Persisted Fields

| Field | Type | Default |
|---|---|---|
| `WorldName` | `Sync<string>` | (clamped to 256 chars) |
| `WorldDescription` | `Sync<string>` | (clamped to 16384 chars) |
| `WorldTags` | `SyncFieldList<string>` | |
| `MobileFriendly` | `Sync<bool>` | |
| `AccessLevel` | `Sync<SessionAccessLevel>` | |
| `HideFromListing` | `Sync<bool>` | |
| `MaxUsers` | `Sync<int>` | 16 |
| `AwayKickEnabled` | `Sync<bool>` | true |
| `AwayKickMinutes` | `Sync<float>` | 5 |
| `AutoSaveInterval` | `Sync<float>` | 5 (minutes) |
| `AutoCleanupEnabled` | `Sync<bool>` | true |
| `AutoCleanupInterval` | `Sync<float>` | 300 (seconds) |
| `ParentSessionIds` | `SyncFieldList<string>` | |

### Non-Persisted Fields

`SessionID`, `SessionURLs`, `CorrespondingWorldId`, `BroadcastKey`, `UniverseID`, `AutoSaveEnabled`, `UseCustomJoinVerifier`

### Behavior

- **Auto-save:** On authority, saves world every `AutoSaveInterval` minutes when enabled.
- **Auto-cleanup:** On authority, runs `MaterialOptimizer.DeduplicateMaterials`, `WorldOptimizer.DeduplicateStaticProviders`, and `WorldOptimizer.CleanupAssets` every `AutoCleanupInterval` seconds. Deduplication only on headless.
- **Broadcast key:** Regenerated (GUIDv7) when access level is `ContactsPlus` and a user leaves or access changes.
- **Access level filter:** In unsafe mode, caps at `LAN`.
- **Validation:** `SaveValidValues` / `RestoreValidValues` used by permission system to rollback invalid changes.

### Gotchas

- All sync members are marked `MarkNonDrivable` and `MarkDirectAccessOnly` (cannot be driven by LogiX/ProtoFlux).
- `SessionID` has a local filter that always returns the current value (rejects remote changes).
- `BroadcastKey` and `SessionURLs` are host-only.
- Field changes in multi-user non-userspace worlds are logged with stack traces.
- Unhiding from listing triggers thumbnail invalidation.

---

## Data Conversion Drivers

Generic components that convert between types using `IConvertible`:

| Component | Target Type |
|---|---|
| `ConvertibleBoolDriver<T>` | bool |
| `ConvertibleByteDriver<T>` | byte |
| `ConvertibleShortDriver<T>` | short |
| `ConvertibleIntDriver<T>` | int |
| `ConvertibleLongDriver<T>` | long |
| `ConvertibleFloatDriver<T>` | float |
| `ConvertibleDoubleDriver<T>` | double |
| `ConvertibleDecimalDriver<T>` | decimal |
| `ConvertibleCharDriver<T>` | char |
| `ConvertibleUshortDriver<T>` | ushort |
| `ConvertibleUintDriver<T>` | uint |
| `ConvertibleUlongDriver<T>` | ulong |
| `ConvertibleSbyteDriver<T>` | sbyte |

### DateTime Converters
- `LocalDateTimeConvertor` -- Converts UTC DateTime to local time
- `LocalNullableDateTimeConvertor` -- Same for nullable DateTime

---

## Additional Utility Components

| Component | Purpose |
|---|---|
| `AppVersion` | Exposes current application version information |
| `AssetMultiplexer<A>` | Selects asset from list by index |
| `BooleanAssetDriver<A>` | Drives asset reference based on boolean |
| `CallbackValueArgument<A>` | Holds value and invokes callback when triggered |
| `MissingComponent` | Placeholder for deserialized types not found in current assembly |
| `SavedReferenceTable` | Stores GUID-to-reference mappings for cross-save resolution |
| `UnresolvedReferences` | Manages unresolved reference strings for deserialization |

---

## Serialization Utility Types

### MissingComponent
Placeholder component for types that cannot be resolved during deserialization. Stores `Type` (string) and `Data` (SyncVar).

### SavedReferenceTable
Stores GUID-to-reference mappings via `SyncRefDictionary<string, IWorldElement>`. Used for preserving references across save/load.
