# FrooxEngine Worker Hierarchy Reference

Core Worker/Component class hierarchy in FrooxEngine (Resonite).

---

## Inheritance Chain

```
Worker (abstract)
  implements IWorker, IWorldElement
  |
  +-- ContainerWorker<C> (abstract)
  |     where C : ComponentBase<C>
  |
  +-- ComponentBase<C> (abstract)
  |     where C : ComponentBase<C>
  |     implements IComponentBase, IDestroyable, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable, IWorldEventReceiver
  |
  +-- Component (abstract)
        extends ComponentBase<Component>
        implements IComponent
```

A typical mod component inherits from `Component` (or a subclass like `ProceduralTexture`). `Slot` inherits from `ContainerWorker<Component>`.

---

## Worker

The root base class for all synchronized objects in FrooxEngine. Manages sync members, serialization, coroutines, and reference IDs.

### Key Fields

| Field | Type | Notes |
|---|---|---|
| `InitInfo` | `WorkerInitInfo` (readonly, protected) | Cached reflection data from `WorkerInitializer`. Contains field arrays, names, attributes. |
| `World` | `World` (property) | The world this worker lives in. Set during `InitializeWorker`. Nulled on `Dispose`. |
| `Parent` | `IWorldElement` (property) | The parent element (e.g., Slot for components). |
| `ReferenceID` | `RefID` (property) | Unique ID allocated on init. Used for networking/serialization. |
| `IsLocalElement` | `bool` (property) | True if `ReferenceID` is a local-only ID. |
| `IsDisposed` | `bool` (property) | Set in `Dispose()`. |
| `IsRemoved` | `virtual bool` (property) | Returns `IsDisposed`. Overridden in subclasses. |
| `SyncMemberCount` | `int` (property) | Number of sync member fields (from `InitInfo`). |

### Convenience Accessors

These all delegate to `World`:
- `Engine`, `Physics`, `Cloud`, `Time`, `Audio`, `AudioSystem`, `InputInterface`, `Input`, `Debug`, `Permissions`, `LocalUser`, `LocalUserRoot`, `LocalUserSpace`

### Key Methods

**Initialization:**
- `InitializeWorker(IWorldElement parent)` -- Core init: sets World/Parent, calls `InitializeSyncMembers()`, allocates `ReferenceID`, initializes each sync member, applies default values, registers reference.
- `InitializeSyncMembers()` (virtual) -- Uses reflection (`InitInfo.syncMemberFields`) to instantiate each `ISyncMember` via `Activator.CreateInstance` and set field values. Marks non-persistent/non-drivable as needed.
- `InitializeSyncMemberDefaults()` -- Applies `[DefaultValue]` attribute values to fields.
- `PostInitializeWorker()` (virtual) -- Hook called after worker init completes. Empty by default.

**Sync Members:**
- `GetSyncMember(int index)` / `GetSyncMember(string name)` -- Access sync members by index or name.
- `GetSyncMemberName(int index)` / `GetSyncMemberName(ISyncMember)` -- Get the display name (respects `[NameOverride]`).
- `GetSyncMemberFieldInfo(int index)` -- Get the `FieldInfo` for a sync member.
- `IndexOfMember(ISyncMember member)` -- Linear scan to find index.
- `TryGetField(string name)` / `TryGetField<T>(string name)` -- Look up a field by name.
- `SyncMembers` (property) -- Enumerates all sync members.

**Sync Methods:**
- `GetSyncMethod(int index)` / `GetSyncMethod(string name)` -- Get delegate for `[SyncMethod]`-attributed methods.
- `GetSyncMethodData(int index, ...)` -- Returns `SyncMethodInfo` + delegate, handles generic type mapping.

**Copying:**
- `CopyValues(Worker source)` -- Copies all sync member values from same-type worker. Skips `[DontCopy]` members.
- `CopyValues(Worker source, Action<ISyncMember, ISyncMember> copy, bool allowTypeMismatch)` -- Custom copy with callback.
- `CopyProperties(Worker source, bool includePrivate, Predicate<ISyncMember> filter)` -- Cross-type copy by matching member names.

**Serialization:**
- `Save(SaveControl control)` -- Serializes to `DataTreeDictionary`. Saves ID + all persistent members. Respects `[DontCopy]`, `SaveNonPersistent`.
- `Load(DataTreeNode node, LoadControl control)` -- Deserializes. Supports `[OldName]` fallback for renamed fields.
- `OnSaving(SaveControl)`, `OnBeforeLoad(...)`, `OnLoading(...)` -- Virtual hooks for custom save/load behavior.
- `SaveMember(ISyncMember, SaveControl)` -- Virtual filter; return false to skip a member.
- `MemberSaved(ISyncMember, DataTreeNode, SaveControl)` -- Hook after a member is saved.

**Coroutines & Tasks:**
- `StartCoroutine(IEnumerator<Context>)` -- Starts a coroutine tracked by this worker. Auto-removed on finish.
- `StopAllCoroutines()` -- Stops all coroutines owned by this worker.
- `StartTask(Func<Task>)` / `StartTask<T>(Func<Task<T>>)` -- Starts an async task associated with this worker.
- `StartGlobalTask(Func<Task>)` -- Task not associated with any specific updatable.
- `DelaySeconds(float)` / `DelayTimeSpan(TimeSpan)` -- Async delay that resumes on the world thread.

**Lifecycle:**
- `Dispose()` -- Called during destruction. Fires `OnDispose()`, `Disposing` event, stops coroutines, disposes all sync members, unregisters reference, nulls World/Parent.
- `PrepareMembersForDestroy()` -- Stops coroutines, unregisters global worker, calls `PrepareDestroy` on all sync members.
- `OnDispose()` (virtual) -- Guaranteed to run on disposal. Use this for cleanup that must happen.

**Other:**
- `SyncMemberChanged(IChangeable)` (virtual) -- Called when a child sync member changes. Overridden in `ComponentBase`.
- `CheckPermission<T>(Predicate<T>, User)` -- Permission check helper.
- `PublicMembersEqual(Worker)` -- Deep equality check on public members.

### Inner Class: InternalReferences

Used during copy/duplicate operations to track reference mappings between original and copy. Tracks `ISyncRef` -> target pairs, registers copies, then transfers references at the end.

---

## ComponentBase\<C\>

The generic base for all components. Provides the full component lifecycle, change tracking, update registration, and destruction logic.

### Key Fields

| Field | Type | Notes |
|---|---|---|
| `persistent` | `Sync<bool>` (readonly, `[NonPersistent]`) | Whether the component persists on save. Defaults `true`. |
| `updateOrder` | `Sync<int>` (readonly, `[NameOverride("UpdateOrder")]`) | Controls update execution order. |
| `EnabledField` | `Sync<bool>` (readonly, public, `[NameOverride("Enabled")]`) | Whether the component is enabled. Defaults `true`. |
| `Container` | `ContainerWorker<C>` (internal property) | The slot (or other container) this component belongs to. |
| `IsStarted` | `bool` (property) | True after `OnStart()` has run. |
| `IsDestroyed` | `bool` (property) | True after `PrepareDestruction()`. |
| `IsValid` | `bool` (property) | False if `[SingleInstancePerSlot]` and a duplicate exists. |
| `IsChangeDirty` | `bool` (property) | True when changes are pending. |
| `IsInInitPhase` | `bool` (property) | True during initialization. |
| `DirectLink` | `ILinkRef` (property) | The link driving/hooking this component. |

### Computed Properties

- `IsRemoved` -- `IsDestroyed || IsDisposed`
- `Enabled` / `Persistent` / `UpdateOrder` -- Convenience wrappers around their sync fields.
- `IsPersistent` -- `persistent.Value && Parent.IsPersistent`
- `UserspaceOnly` (virtual) -- Override to `true` to restrict to userspace world.
- `CanRunUpdates` (virtual) -- Override to control when updates run. Default: `true`.
- `IsLinked` / `IsDriven` / `IsHooked` -- Link state queries.

### Lifecycle Methods (in execution order)

1. **`OnAwake()`** -- Called during `Initialize()`. Reference allocations are blocked. Use for setup that must happen before anything else.
2. **`OnInit()`** -- Called only when `isNew` is true (not on load). Good for setting initial field values.
3. **`OnStart()`** -- Called via `InternalRunStartup()`. Registers for updates/audio. Marks `IsStarted = true`.
4. **`OnAttach()`** -- Called when component is freshly attached to a slot (not on load/duplicate).
5. **`OnDuplicate()`** -- Called when the component was created via duplication.
6. **`OnPaste()`** -- Called when the component was pasted.
7. **`OnEnabled()` / `OnDisabled()`** -- Called when `EnabledField` value changes.
8. **`OnActivated()` / `OnDeactivated()`** -- Called when the slot's active state changes.
9. **`OnLinked()` / `OnUnlinked()`** -- Called when a link ref drives/releases this component.
10. **`OnCommonUpdate()`** -- Called every frame (if enabled + active). Runs after `OnBehaviorUpdate`.
11. **`OnBehaviorUpdate()`** -- Called every frame (if enabled + active). Runs before `OnCommonUpdate`.
12. **`OnChanges()`** -- Called when `IsChangeDirty` is true, during the change processing phase.
13. **`OnImmediateChanged()`** -- Called synchronously when `MarkChangeDirty()` fires (before batched `OnChanges`).
14. **`OnAudioUpdate()`** -- Called on the audio thread. Protected by `destroyLock`.
15. **`OnAudioConfigurationChanged()`** -- Called when audio config changes.
16. **`OnDestroying()`** -- Called before destruction begins. **Not guaranteed to run.**
17. **`OnPrepareDestroy()`** -- Called during `PrepareDestruction()`.
18. **`OnDestroy()`** -- Called during final destruction. **Not guaranteed** -- use `OnDispose()` for must-run cleanup.

### World Event Handlers

Override these to receive world events (only called if override detected by `WorkerInitializer`):
- `OnFocusChanged(World.WorldFocus)`, `OnWorldDestroy()`, `OnUserJoined(User)`, `OnUserSpawn(User)`, `OnUserLeft(User)`, `OnWorldSaved()`

### Key Methods

- `Initialize(ContainerWorker<C>, bool isNew)` -- Full initialization: sets Container, calls `InitializeWorker`, sets defaults, calls `OnAwake`/`OnInit`, registers for startup.
- `MarkChangeDirty()` -- Triggers change processing. Thread-safe: if called off-thread, schedules via `RunSynchronously`. Fires `OnImmediateChanged` + `Changed` event.
- `Destroy()` / `Destroy(bool sendDestroyingEvent)` -- Initiates component destruction via `Container.RemoveComponent`.
- `PrepareDestruction()` -- Marks destroyed, stops coroutines, prepares members, registers for final destroy.
- `InternalRunUpdate()` -- Called by update manager. Checks `Enabled && CanRunUpdates`, then calls `OnBehaviorUpdate` + `OnCommonUpdate`.
- `InternalRunApplyChanges(int updateIndex)` -- Clears dirty flag, calls `OnChanges()`.
- `InternalRunDestruction()` -- Calls `OnDestroy`, unregisters from all update systems, disposes, fires `Destroyed` event.

**Scheduling helpers:**
- `RunSynchronously(Action, bool immediatelyIfPossible)` -- Run on the world's synchronous thread.
- `RunSynchronouslyAsync(Action)` / `RunSynchronouslyAsync<T>(Func<T>)` -- Async version.
- `RunInSeconds(float, Action)` / `RunInUpdates(int, Action)` -- Delayed execution (auto-cancelled if destroyed).
- `RunInBackground(Action, WorkType)` -- Run on a background thread.
- `RunInUpdateScope(Action)` -- Wraps an action to run under this component's update context.

**Linking:**
- `Link(ILinkRef)` / `ReleaseLink(ILinkRef)` -- Set/clear `DirectLink`, propagate to children.
- `InheritLink` / `ReleaseInheritedLink` -- Throws; components cannot inherit links.

### Events

- `Changed` (`Action<IChangeable>`) -- Fired when component changes.
- `Destroyed` (`Action<IDestroyable>`) -- Fired on destruction.

---

## Component

Concrete (but still abstract) specialization of `ComponentBase<Component>`. This is the base class mod developers typically inherit from.

### Key Additions

| Member | Notes |
|---|---|
| `Slot` (property) | The `Slot` this component is attached to. Set during `Initialize` by casting `container` to `Slot`. |
| `IsUnderLocalUser` | Delegates to `Slot.IsUnderLocalUser`. |
| `CanRunUpdates` (override) | Returns `Slot.IsActive` -- updates only run when the slot hierarchy is active. |

### Key Methods

- `AssignKey(string key, int version, bool onlyFree)` -- Requests a world key for this component.
- `HasKey(string key)` -- Checks if this component owns a key.
- `RemoveKey(string key)` -- Releases a key.

---

## ContainerWorker\<C\>

A Worker that contains a bag of components. `Slot` extends this class with `C = Component`.

### Key Fields

| Field | Type | Notes |
|---|---|---|
| `componentBag` | `WorkerBag<C>` (readonly, `[HideInInspector]`) | Dictionary-based storage of components by `RefID`. |
| `childInitializables` | `SlimList<IInitializable>` (protected) | Components pending init phase completion. |
| `Components` | `ComponentEnumerable` (property) | Struct-based enumerator over all components. Allocation-free. |
| `ComponentCount` | `int` (property) | Number of components. |
| `IsDestroyed` | `bool` (property) | Set in `PrepareDestruction()`. |

### Events

- `ComponentAdded` (`ComponentEvent<C>`) -- Fired after a component is added and initialized.
- `ComponentRemoved` (`ComponentEvent<C>`) -- Fired after a component is removed.

### Key Methods

**Attaching:**
- `AttachComponent<T>(bool runOnAttachBehavior, Action<T> beforeAttach)` -- Creates and attaches a new component. `beforeAttach` runs after init but before `OnAttach`.
- `AttachComponent(Type, bool runOnAttachBehavior, Action<C> beforeAttach)` -- Non-generic version.
- `CopyComponent<T>(T source)` / `CopyComponent(C source)` -- Attach + copy values.
- `MoveComponent<T>(T original)` / `MoveComponent(C original)` -- Copy + redirect all references + destroy original.

**Querying:**
- `GetComponent<T>(Predicate<T>, bool excludeDisabled)` -- Find first matching component.
- `GetComponents<T>(Predicate<T>, bool excludeDisabled)` -- Find all matching components (returns new list).
- `GetComponents<T>(List<T>, Predicate<T>, bool excludeDisabled)` -- Append to existing list.
- `GetComponent(Type, bool exactTypeOnly)` -- Find by runtime type.
- `GetComponent(Predicate<C>)` -- Find by predicate on the base type.
- `EnumerateComponents(Predicate<C>)` / `EnumerateComponents(Type)` -- Lazy enumeration.
- `ForeachComponent<T>(Action<T>, ...)` / `ForeachComponent<T>(Func<T, bool>, ...)` -- Iterate with optional early-stop.

**Ensuring:**
- `GetComponentOrAttach<T>(Predicate<T>)` -- Get existing or create new.
- `EnsureSingleComponent<T>(Predicate<T>)` -- Get/create, destroy duplicates.

**Removing:**
- `RemoveComponent(C)` / `RemoveComponent(RefID)` -- Remove a specific component.
- `RemoveAllComponents(Predicate<C>)` -- Remove all matching.

**Lifecycle:**
- `Initialize(IWorldElement element)` -- Calls `InitializeWorker`, hooks component add/remove events.
- `PrepareDestruction()` -- Marks destroyed, prepares all child components for destruction.
- `EndInitPhase()` -- Ends init phase for all pending child initializables.
- `RecursivelyMapElements(ISyncMember source, ISyncMember target, Dictionary<...> map)` -- Deep-maps sync member trees for move operations.

---

## WorkerInitializer

Static class that performs reflection-based initialization of Worker types. Caches `WorkerInitInfo` per type. Builds the component library category tree.

### Key Members

| Member | Type | Notes |
|---|---|---|
| `ComponentLibrary` | `CategoryNode<Type>` (static property) | The component browser category tree. |
| `Workers` | `IEnumerable<Type>` (static property) | All non-abstract Worker types found at startup. |

### Key Methods

- `GetInitInfo(Type)` / `GetInitInfo(IWorker)` -- Returns cached `WorkerInitInfo`, creating it on first access via `Initialize(Type)`.
- `Initialize(List<Type>, bool verbose)` -- One-time global init. Scans all types, extracts workers, builds `ComponentLibrary`, processes `[Category]` and `[FeatureUpgradeReplacement]` attributes.
- `GetCommonGenericTypes(Type)` -- Returns concrete generic instantiations from `[GenericTypes]` attribute.
- `ResolveReplacement(Type, VersionNumber, IReadOnlyDictionary<string, int>)` -- Resolves feature-flag-based type replacements (for versioned upgrades).

### IsValidField (critical for mod developers)

```csharp
private static bool IsValidField(FieldInfo field, Type workerType)
```

A field is recognized as a sync member if and only if:
1. It implements `ISyncMember`
2. It is **not** an interface type
3. **It is `IsInitOnly` (i.e., declared `readonly`)**
4. It is not abstract

**This is the #1 gotcha for mod developers:** sync member fields (`Sync<T>`, `SyncRef<T>`, `SyncList<T>`, etc.) **must be declared `readonly`** or they will be silently ignored by the engine.

### GatherWorkerFields

Recursively walks the type hierarchy (up to `Worker`) collecting valid sync member fields in declaration order, base class first. Uses `BindingFlags.DeclaredOnly | Instance | Public | NonPublic`.

### Initialize(Type) -- Per-Type Init

Produces a `WorkerInitInfo` containing:
- `syncMemberFields` -- `FieldInfo[]` of all valid sync member fields
- `syncMemberNames` -- Display names (field name with `_Field` suffix stripped, or `[NameOverride]` value)
- `syncMemberNonpersitent` / `syncMemberNondrivable` / `syncMemberDontCopy` -- Per-member attribute flags
- `defaultValues` -- From `[DefaultValue]` attributes
- `syncMemberNameToIndex` -- Name-to-index lookup
- `oldSyncMemberNames` -- From `[OldName]` attributes for migration
- `HasUpdateMethods` -- True if `OnCommonUpdate` or `OnBehaviorUpdate` is overridden
- `HasLinkedMethod` / `HasUnlinkedMethod` / `HasAudioUpdateMethod` / `HasAudioConfigurationChangedMethod`
- `ReceivesWorldEvent` / `ReceivesAnyWorldEvent` -- Per-event override detection
- `syncMethods` / `staticSyncMethods` -- Methods with `[SyncMethod]`
- `SingleInstancePerSlot`, `DontDuplicate`, `PreserveWithAssets`, `RegisterGlobally` -- Type-level attributes
- `DefaultUpdateOrder` -- From `[DefaultUpdateOrder]`
- `CategoryPath`, `GroupingName` -- For component browser organization

---

## Gotchas for Mod Developers

1. **Sync member fields MUST be `readonly`.** `WorkerInitializer.IsValidField` checks `field.IsInitOnly`. If you forget `readonly`, the field is invisible to the engine -- no serialization, no networking, no inspector display.

2. **`OnDestroy` is NOT guaranteed to run.** Use `OnDispose()` (from `Worker`) for cleanup that absolutely must happen.

3. **`OnAwake` vs `OnInit` vs `OnAttach`:**
   - `OnAwake` -- always runs (load or new). Reference allocations are blocked.
   - `OnInit` -- only on fresh creation (not load/paste/duplicate).
   - `OnAttach` -- only on fresh attach (not load).

4. **Update registration is automatic but conditional.** `WorkerInitializer` checks if you override `OnCommonUpdate`/`OnBehaviorUpdate`. If you don't override them, you're never registered for updates (no wasted cycles).

5. **`CanRunUpdates` for Components returns `Slot.IsActive`.** Your `OnCommonUpdate`/`OnBehaviorUpdate` won't run if the slot or any ancestor is inactive.

6. **`MarkChangeDirty()` is thread-safe.** If called off the main thread, it schedules itself via `RunSynchronously`.

7. **`[SingleInstancePerSlot]`** -- If a second instance of the same type is added, `IsValid` is set to false and it's destroyed (if authority) or waits for the duplicate to be removed.

8. **Field naming:** A field named `FooBar_Field` is displayed as `FooBar` (suffix stripped). Use `[NameOverride("X")]` for custom names.

9. **`UserspaceOnly` components** are auto-destroyed if attached in a non-userspace world.

10. **`[NonPersistent]` fields** are not saved. `[DontCopy]` fields are skipped during copy/save. `[NonDrivable]` fields cannot be driven.

11. **Audio updates (`OnAudioUpdate`)** run on a separate thread and are protected by a `SpinLock` to prevent races with destruction.
