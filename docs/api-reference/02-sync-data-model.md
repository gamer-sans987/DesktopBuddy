# FrooxEngine Sync Data Model Reference

Core synchronized data primitives in FrooxEngine (Resonite).

---

## Type Hierarchy

```
SyncElement (abstract)
  implements ISyncElement, IWorldElement, ISyncMember, IChangeable, ILinkable, IInitializable
  |
  +-- SyncField<T>         value field (int, float, bool, string, etc.)
  |     implements IField<T>
  |
  +-- SyncRef<T>           reference to another IWorldElement by RefID
  |     implements ISyncRef
  |     where T : class, IWorldElement
  |
  +-- SyncList<T>          ordered list of sync members
  |     implements ISyncList
  |
  +-- SyncBag<T>           unordered bag of sync members (keyed by RefID)
  |     implements ISyncBag
  |
  +-- SyncDelegate<D>      reference to a method on a Worker
  |     implements ISyncDelegate, ISyncRef
  |
  +-- SyncObject           composite of multiple sync members (inline struct)
```

Non-generic shorthands:
- `SyncRef` = `SyncRef<IWorldElement>`
- `Sync<T>` = alias commonly seen in component fields, maps to `SyncField<T>`

---

## RefID

```csharp
public struct RefID : IEquatable<RefID>
```

- 64-bit identifier (`ulong Position`) for every `IWorldElement` in a `World`.
- `RefID.Null` -- the zero/invalid ID.
- `IsLocalID` -- true when the ID was allocated from the local range (not synced to other users).
- Used as the key in `World.ReferenceController` to look up any element.
- Serialized as `"ID"` in save data; remapped on load via `LoadControl.AssociateReference`.

---

## SyncElement (base class)

The abstract base for all synchronized data members. Declared as fields on `Worker`/`Component` subclasses.

### Key Properties

| Property | Type | Notes |
|---|---|---|
| `ReferenceID` | `RefID` | Unique within the World, allocated on `Initialize` |
| `Parent` | `IWorldElement` | The owning Worker or parent SyncElement |
| `Worker` | `Worker` | Nearest parent Component/Slot/Stream |
| `Slot` | `Slot` | Convenience -- `Component?.Slot` |
| `World` | `World` | |
| `Name` | `string` | Field name on the Worker (via reflection) |
| `IsPersistent` | `bool` | Saved to disk; false if `NonPersistent` attribute or parent is non-persistent |
| `IsLocalElement` | `bool` | ID is in the local range; data not synced |
| `IsDriven` | `bool` | Currently being written by a driver/link |
| `IsHooked` | `bool` | Driven but allows write-through (hook mode) |
| `IsBlockedByDrive` | `bool` | `IsDriven && !IsHooked` -- manual writes will throw/warn |
| `IsDrivable` | `bool` | Default true; set false by `[NonDrivable]` attribute |
| `ActiveLink` | `ILinkRef` | The link currently controlling this element (inherited or direct) |
| `IsLoading` | `bool` | True during `Load`/`DecodeFull`/`DecodeDelta` |
| `WasChanged` | `bool` | Sticky flag, cleared by `GetWasChangedAndClear()` |

### Key Methods

- **`Initialize(World, IWorldElement parent)`** -- allocates RefID, registers in World, sets `IsInInitPhase = true`.
- **`Dispose()`** -- unregisters from World, nulls parent/world.
- **`Save / Load`** -- serializes to/from `DataTreeNode` (dictionary with `"ID"` + `"Data"`).
- **`EncodeFull / DecodeFull`** -- binary full-state sync (host-to-guest).
- **`EncodeDelta / DecodeDelta`** -- binary incremental sync.
- **`InvalidateSyncElement()`** -- marks dirty for next sync tick; no-op if driven or disposed.
- **`SyncElementChanged()`** -- propagates change up via `Parent.ChildChanged`, fires `Changed` event.
- **`CopyValues(ISyncMember target)`** -- deep-copies data to a same-typed target.

### Link / Drive System

- **`Link(ILinkRef)`** / **`ReleaseLink(ILinkRef)`** -- sets `DirectLink`.
- **`InheritLink(ILinkRef)`** / **`ReleaseInheritedLink(ILinkRef)`** -- sets `InheritedLink` (propagated from parent).
- `ActiveLink = InheritedLink ?? DirectLink` -- inherited takes priority.
- Links propagate to `LinkableChildren` recursively.

### Modification Guard

`BeginModification()` / `EndModification()` bracket writes. Throws if:
- Element is disposed.
- Called from wrong thread (fails `ConnectorManager.ThreadCheck()`).
- Element is driven and modification is not allowed through the drive (`IsBlockedByDrive`).
  - First violation logs a warning; subsequent violations are silent unless `throwOnError` is true.

Re-entrant: uses a `modificationLevel` counter.

### Changed Event

```csharp
public event Action<IChangeable> Changed;
```

Fires **outside** the data model update loop. The XML remark in source warns: "out of data model event -- shouldn't be used for anything data model related." Use for UI/connector updates only.

---

## Sync\<T\> / SyncField\<T\>

```csharp
public class Sync<T> : SyncField<T> where T : unmanaged
public class SyncField<T> : SyncElement, IField<T>
```

Holds a single synchronized value.

- **`Value`** -- get/set the stored value. Setter calls `BeginModification` + `InvalidateSyncElement` + `SyncElementChanged`.
- **`BoxedValue`** -- get/set as `object` (used by inspectors, serialization).
- **Implicit operators** -- `SyncField<T>` implicitly converts to `T`.
- `Changed` event fires on every write (even if value unchanged -- no equality check in the base path).

Common usage in components:
```csharp
public readonly Sync<float3> Position;  // value type
public readonly Sync<bool> Enabled;
public readonly Sync<string> Label;     // reference type variant: SyncField<string>
```

---

## SyncRef\<T\>

```csharp
public class SyncRef<T> : SyncElement, ISyncRef where T : class, IWorldElement
```

Holds a reference to another world element by `RefID`.

- **`Target`** -- get/set the referenced `T`. Setter resolves/stores the RefID internally.
- **`Value`** (RefID) -- the raw ID; can be set directly.
- Resolves lazily: the target may not exist yet during load (forward references). The engine resolves after `LoadControl` finishes remapping.
- `SyncRef` (non-generic) = `SyncRef<IWorldElement>`.

Common usage:
```csharp
public readonly SyncRef<Slot> TargetSlot;
public readonly SyncRef<IAssetProvider<Material>> Material;
```

---

## SyncList\<T\>

```csharp
public class SyncList<T> : SyncElement, ISyncList where T : ISyncMember, new()
```

Ordered, indexed collection of sync members. Each element gets its own `RefID`.

- **`Add()`** -- creates and returns a new `T` at the end.
- **`Insert(int index)`** -- creates at position.
- **`RemoveAt(int index)`** / **`Remove(T)`** -- removes element (disposes it).
- **`this[int index]`** -- indexer.
- **`Count`** -- element count.
- **`GetElement(int index)`** -- returns as `ISyncMember`.
- Elements are full sync members -- if `T` is `SyncRef<Slot>`, each list entry is independently drivable.

Common usage:
```csharp
public readonly SyncList<SyncRef<Slot>> Targets;
public readonly SyncList<Sync<float>> Weights;
```

---

## SyncBag\<T\>

```csharp
public class SyncBag<T> : SyncElement, ISyncBag where T : ISyncMember, new()
```

Unordered collection keyed by `RefID`. Unlike `SyncList`, insertion order is not guaranteed.

- **`Add()`** -- creates a new element, returns it.
- **`Remove(RefID)`** / **`Remove(T)`** -- removes by key or value.
- **`Values`** -- enumerable of elements.
- **`TryGetValue(RefID, out T)`** -- lookup by element's own RefID.
- Useful when stable ordering doesn't matter and you need fast add/remove.

---

## SyncDelegate\<D\>

```csharp
public class SyncDelegate<D> : SyncElement, ISyncDelegate, ISyncRef where D : Delegate
```

A synchronized reference to a method on a Worker.

- **`Target`** -- the `IWorldElement` (Worker) the method belongs to (stored as RefID like `SyncRef`).
- **`MethodName`** -- the string name of the method.
- **`IsStaticReference`** -- true if target is null but method name is set (static method).
- Used extensively by ProtoFlux and UIX callbacks (e.g., `ButtonEventHandler`).

---

## Component Wrapper Types

These are full `Component` subclasses that expose a single sync field to the scene graph.

### ValueField\<T\>

```csharp
public class ValueField<T> : Component where T : unmanaged
{
    public readonly Sync<T> Value;
}
```

A component holding one value. Attach to a Slot to store arbitrary data.

### ReferenceField\<T\>

```csharp
public class ReferenceField<T> : Component where T : class, IWorldElement
{
    public readonly SyncRef<T> Reference;
}
```

A component holding one reference.

---

## Driver Components

### ValueCopy\<T\>

Copies a value from `Source` field to `Target` field every update.

| Field | Type | Notes |
|---|---|---|
| `Source` | `IField<T>` (driven-from) | Read each frame |
| `Target` | `IField<T>` (driven) | Written each frame |
| `WriteBack` | `Sync<bool>` | If true, also copies Target back to Source (bidirectional) |

### ReferenceCopy\<T\>

Same pattern for references: copies `Source.Target` into `Target.Target`.

### ValueMultiDriver\<T\>

Drives multiple target fields from a single source value.

| Field | Type |
|---|---|
| `Value` | `Sync<T>` |
| `Drives` | `SyncList<FieldDrive<T>>` |

All entries in `Drives` are set to `Value` each frame.

### ReferenceMultiDriver\<T\>

Same pattern for references: one `Reference` source, multiple `Drives` targets.

### BooleanReferenceDriver\<T\>

Drives a `SyncRef<T>` based on a boolean condition.

| Field | Type |
|---|---|
| `State` | `Sync<bool>` |
| `TrueTarget` | `SyncRef<T>` |
| `FalseTarget` | `SyncRef<T>` |
| `TargetReference` | `RefDrive<T>` (driven output) |

When `State` is true, `TargetReference` is set to `TrueTarget`; otherwise `FalseTarget`.

### DriveMember\<T\>

Low-level component that drives a single `ISyncMember` field.

| Field | Type |
|---|---|
| `Target` | field drive pointing at the member to control |
| `Value` | the value to write |

---

## Gotchas and Practical Notes

1. **Thread safety**: All sync member writes must happen on the World's update thread. `BeginModification` calls `ConnectorManager.ThreadCheck()` and will throw if violated.

2. **Drive conflicts**: Writing to a driven field (where `IsBlockedByDrive` is true) silently fails with a warning log on first occurrence. It does NOT throw by default (`throwOnError` defaults to false in internal paths). Check `IsDriven` before attempting writes.

3. **Hook vs Drive**: A hooked field (`IsHooked = true`) allows write-through -- the driver reads the manually-written value and processes it. A pure drive blocks all external writes.

4. **Changed event ordering**: `SyncElementChanged` fires synchronously during the setter. It bubbles up through `Parent.ChildChanged` before firing the `Changed` event. Do not modify other sync members inside a `Changed` handler -- it can cause re-entrancy issues.

5. **Local elements**: If `ReferenceID.IsLocalID` is true, the element is not synced to other users. `GenerateSyncData` returns false for local elements. Useful for per-user state.

6. **NonPersistent**: Marked with `[NonPersistent]` attribute. These fields are not saved but ARE synced (unless also local). Example: `ComponentBase.persistent` field itself is non-persistent.

7. **Modification nesting**: `BeginModification`/`EndModification` is reentrant (counter-based). Safe to call from within a change handler if you really need to, but avoid if possible.

8. **SyncList vs SyncBag**: Use `SyncList` when order matters (UI lists, ordered drives). Use `SyncBag` when you need fast add/remove and don't care about order.

9. **Forward references on load**: During `Load`, `SyncRef` targets may not exist yet. The engine resolves them after all elements are loaded via `LoadControl.AssociateReference` remapping. Don't access `SyncRef.Target` during `OnLoading` and expect it to be set.

10. **CopyValues type check**: `CopyValues` throws `ArgumentException` if source and target types don't match. Use `Worker.CopyProperties` for cross-type copies matched by field name.

11. **Disposed element writes**: Writing to a disposed element throws. Always check `IsDisposed` or `IsRemoved` if the element's lifetime is uncertain.

12. **InitPhase**: After `Initialize`, elements are in init phase (`IsInInitPhase = true`). Call `EndInitPhase()` to complete. During init phase, certain change propagation may be deferred.
