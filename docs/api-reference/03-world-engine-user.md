# FrooxEngine World, Engine & User Reference

Reference for World, WorldManager, Engine, User, UserRoot, and Userspace in FrooxEngine (Resonite).

---

## Engine

**Class:** `FrooxEngine.Engine`

Singleton-style top-level object. Owns all subsystems. One instance per Resonite process.

### Key Fields / Properties

| Member | Type | Notes |
|---|---|---|
| `Current` | `Engine` (static) | The running engine instance. |
| `WorldManager` | `WorldManager` | Manages all loaded worlds. |
| `InputInterface` | `InputInterface` | Raw input system. |
| `AudioSystem` | `AudioSystem` | Global audio. |
| `AssetManager` | `AssetManager` | Asset loading/caching. |
| `Cloud` | `CloudInterface` | Resonite cloud API access. |
| `LocalDB` | `LocalDB` | Local database for records/settings. |
| `Platform` | `Platform` | Current platform (VR headset type, desktop, etc.). |
| `SystemInfo` | `SystemInfo` | Hardware/OS info. |
| `SecurityManager` | `SecurityManager` | Permission checks. |
| `CoroutineManager` | `CoroutineManager` | Engine-level coroutines. |
| `GlobalCoroutineManager` | `CoroutineManager` | Coroutines not tied to a specific world. |
| `VersionString` | `string` | Current Resonite version. |
| `IsReady` | `bool` | True after full initialization completes. |
| `IsShuttingDown` | `bool` | True during shutdown sequence. |

### Lifecycle

1. `Initialize(...)` -- Sets up all subsystems, loads assemblies, registers types via `GlobalTypeRegistry` / `WorkerInitializer`.
2. `RunUpdateLoop(...)` -- Main loop. Calls `WorldManager.Update()` each tick.
3. `RequestShutdown()` -- Begins graceful shutdown. Sets `IsShuttingDown`, saves worlds, disposes subsystems.

### Key Methods

- `RunPostInit(Action)` -- Schedule work to run after init completes.
- `ConnectorPool<T>` -- Object pool for render connectors (internal).

### Gotchas

- **Do not cache `Engine.Current` across async boundaries.** It can be null during shutdown.
- **`IsReady` may be false during `OnEngineInit()`.** Some subsystems are still initializing. If you need a fully-ready engine, subscribe to post-init or check `IsReady`.

---

## WorldManager

**Class:** `FrooxEngine.WorldManager`

Manages the collection of loaded worlds, focus state, and the update loop.

### Key Fields / Properties

| Member | Type | Notes |
|---|---|---|
| `Worlds` | `IReadOnlyList<World>` | All currently loaded worlds. |
| `FocusedWorld` | `World` | The world the user is currently viewing/interacting with. |
| `Engine` | `Engine` | Back-reference to the owning engine. |

### Key Methods

- `Update()` -- Called each frame by Engine. Updates all worlds in sequence.
- `GetWorld(string sessionId)` -- Look up a world by session ID.
- `GetWorldByName(string name)` -- Look up by name (first match).

### Events

- `WorldAdded` (`Action<World>`) -- Fired when a world is loaded/created.
- `WorldRemoved` (`Action<World>`) -- Fired when a world is destroyed/closed.
- `WorldFocused` (`Action<World>`) -- Fired when focus changes.

### Gotchas

- **`FocusedWorld` can be null** if the user is in a transition state or no world is focused.
- **World update order is not guaranteed** across worlds. Do not assume one world updates before another.

---

## World

**Class:** `FrooxEngine.World`

A single networked session/space. Contains the slot hierarchy, users, and all synchronized state.

### Key Fields / Properties

| Member | Type | Notes |
|---|---|---|
| `RootSlot` | `Slot` | The top of the slot hierarchy. |
| `AssetsSlot` | `Slot` | Dedicated slot for asset storage. |
| `Name` | `string` | World display name. |
| `Engine` | `Engine` | Back-reference. |
| `LocalUser` | `User` | The local user in this world. Null if not yet joined. |
| `LocalUserRoot` | `UserRoot` | Shortcut for `LocalUser.Root`. |
| `LocalUserSpace` | `Slot` | The local user's private space slot. |
| `AllUsers` | `IReadOnlyList<User>` | All users currently in the session. |
| `HostUser` | `User` | The session host. |
| `UserCount` | `int` | Number of users. |
| `Time` | `WorldTime` | World time (`.WorldTime`, `.Delta`). |
| `Focus` | `WorldFocus` | Focus state: `Focused`, `Background`, `Overlay`, etc. |
| `IsAuthority` | `bool` | True if local user is the host/authority. |
| `IsUserspace` | `bool` | True if this is the userspace (dash/menu) world. |
| `IsDestroyed` | `bool` | True after `Destroy()`. |
| `SessionId` | `string` | Unique session identifier. |
| `CorrespondingWorldId` | `string` | The record URI this world was loaded from (if any). |
| `Configuration` | `WorldConfiguration` | World settings (name, max users, permissions, etc.). |
| `Permissions` | `PermissionSet` | Current permission configuration. |
| `KeyOwner(string)` | `IComponent` | Looks up which component owns a key. |
| `UpdateManager` | `UpdateManager` | Internal: manages component update registration. |

### Key Methods

**Scheduling:**
- `RunSynchronously(Action, bool immediatelyIfPossible)` -- Run on the world's main thread. Safe to call from any thread.
- `RunInUpdates(int updates, Action)` -- Run after N update ticks. **This is the primary way to schedule recurring work from a mod** (re-schedule at the end of the callback for a loop).
- `RunInSeconds(float seconds, Action)` -- Run after a delay.
- `StartCoroutine(IEnumerator<Context>)` -- World-level coroutine.

**Slot Creation:**
- `AddSlot(string name)` -- Shortcut for `RootSlot.AddSlot(name)`.

**User Queries:**
- `GetUser(string userId)` -- Find user by cloud user ID.
- `GetUserByAllocationID(ushort)` -- Find user by their allocation ID (used in RefIDs).
- `GetUserByIndex(int)` -- Index into `AllUsers`.

**Save/Load:**
- `Save(...)` -- Serializes the world to a DataTreeDictionary.
- `Load(...)` -- Deserializes world state.
- `SaveWorld(...)` -- Full save including record metadata.

**Lifecycle:**
- `Destroy()` -- Destroys the world. Fires `WorldDestroyed`. Disposes all slots/components.

### Events

- `UserJoined` (`Action<User>`) -- Fired when a user joins.
- `UserLeft` (`Action<User>`) -- Fired when a user leaves.
- `WorldDestroyed` / `WorldDestroying` -- Fired during destruction.
- `FocusChanged` (`Action<World.WorldFocus>`) -- Fired when focus state changes.

### WorldFocus Enum

| Value | Meaning |
|---|---|
| `Focused` | Active foreground world. |
| `Background` | Loaded but not displayed. |
| `Overlay` | Overlay/dash world. |
| `Private` | Private/hidden world. |

### Gotchas

- **`LocalUser` is null until join completes.** If you spawn UI in `OnEngineInit`, the user may not be in the world yet.
- **`IsAuthority` determines who runs destructive operations.** Many things (component destruction, key assignment) only execute on the authority.
- **`RunInUpdates(1, callback)` is the standard pattern for a per-frame loop.** Re-schedule inside the callback. Check `world.IsDestroyed` to stop.
- **`RootSlot` children are synced.** Everything under `RootSlot` is networked to all users. Use `LocalUserSpace` for local-only content.
- **`IsUserspace` worlds are special.** They run the dash/menu. Components marked `[UserspaceOnly]` are only valid here.

---

## User

**Class:** `FrooxEngine.User`

Represents a single user in a World. Inherits from `Worker` -- it is a synced object.

### Key Fields / Properties

| Member | Type | Notes |
|---|---|---|
| `UserID` | `string` | Cloud user ID (e.g., `U-username`). |
| `UserName` | `string` (Sync field) | Display name. |
| `Root` | `UserRoot` | The user's `UserRoot` component (avatar root). Null until spawned. |
| `World` | `World` | The world this user belongs to. |
| `AllocationID` | `ushort` | The ID space allocation for this user's created objects. |
| `IsLocalUser` | `bool` | True if this is the local machine's user. |
| `IsHost` | `bool` | True if this user is the session host. |
| `IsPresentInHeadset` | `bool` | True if user is in VR (not AFK/desktop-away). |
| `HeadDevice` | `HeadOutputDevice` | VR headset type. `Headless` / `Screen` for non-VR. |
| `Platform` | `Platform` | User's platform. |
| `Metadata` | `UserMetadata` | Arbitrary key-value metadata synced across the session. |
| `IsMachineOwner` | `bool` | True if this user owns the physical machine (relevant for headless). |

### Key Methods

- `GetUserPermission<T>()` -- Get the effective permission level for this user.
- `SetUserPermission<T>(T value)` -- Set a permission for this user (authority only).

### Gotchas

- **`User.Root` can be null.** The `UserRoot` is attached asynchronously after join. Always null-check.
- **`IsLocalUser` is per-world.** The same cloud account is a different `User` object in each world.
- **`AllocationID` is used to partition RefID space.** Each user gets a range so objects they create don't collide.

---

## UserRoot

**Class:** `FrooxEngine.UserRoot`

Component attached to the root slot of a user's avatar. Provides position/orientation and locomotion state.

### Key Fields / Properties

| Member | Type | Notes |
|---|---|---|
| `Slot` | `Slot` | The slot this component is on (the user's root slot). |
| `User` | `User` | Back-reference via a `SyncRef<User>`. |
| `HeadPosition` | `float3` (computed) | World-space head position. |
| `HeadRotation` | `floatQ` (computed) | World-space head rotation. |
| `GlobalPosition` | `float3` | World-space position of the user root. Shortcut for `Slot.GlobalPosition`. |
| `GlobalRotation` | `floatQ` | World-space rotation of the user root. |
| `GlobalScale` | `float3` | World-space scale. |
| `LocalUser` | `bool` | Whether this is the local user's root. |

### Key Sync Fields

| Field | Type | Notes |
|---|---|---|
| `ActiveLocomotionModule` | `SyncRef<LocomotionModule>` | Current movement mode. |
| `_headProxy` | `SyncRef<Slot>` | The slot used as head proxy (for position/rotation reads). |
| `_silenced` | `Sync<bool>` | Whether the user is muted. |

### Gotchas

- **`HeadPosition`/`HeadRotation` read from `_headProxy`.** If the head proxy slot is null or destroyed, these return the root slot's transform instead.
- **UserRoot is a Component**, so all component lifecycle rules apply.
- **One UserRoot per user per world.** Found via `User.Root`.

---

## Userspace

**Class:** `FrooxEngine.Userspace`

Static class providing access to the userspace world -- the always-loaded overlay world for the dash, menus, notifications, etc.

### Key Static Properties

| Member | Type | Notes |
|---|---|---|
| `UserspaceWorld` | `World` | The userspace world instance. |
| `IsUserspace(World)` | `bool` | Check if a given world is the userspace world. |

### Key Static Methods

- `SetupUserspace(Engine)` -- Called during engine init to create the userspace world.
- `RegisterDash(...)` -- Registers the dash overlay system.

### Gotchas

- **Userspace world always exists** after engine init. It is never destroyed during normal operation.
- **Userspace components run even when the user is in another world.** The userspace world updates continuously regardless of focus.
- **`world.IsUserspace` is the canonical check.** Do not compare world references directly -- use the property.

---

## Common Patterns for Mods

### Getting the Local User's Head Position (used in this project)

```csharp
var localUser = world.LocalUser;        // may be null
var userRoot = localUser?.Root;          // may be null
var headPos = userRoot.HeadPosition;     // world-space float3
var headRot = userRoot.HeadRotation;     // world-space floatQ
```

### Scheduling a Per-Frame Update Loop

```csharp
world.RunInUpdates(1, () => MyLoop(world));

void MyLoop(World world)
{
    if (world.IsDestroyed) return;
    // ... do work ...
    world.RunInUpdates(1, () => MyLoop(world));  // re-schedule
}
```

### Spawning UI in Front of the User

```csharp
var root = world.RootSlot.AddSlot("MyPanel");
var forward = headRot * float3.Forward;
root.GlobalPosition = headPos + forward * 0.8f;
root.GlobalRotation = floatQ.LookRotation(forward, float3.Up);
```

### Cross-Thread Safety

```csharp
world.RunSynchronously(() => {
    // safe to touch world objects here
});
```

---

## Relationship Diagram

```
Engine
  +-- WorldManager
  |     +-- World (userspace)
  |     |     +-- RootSlot -> Slot hierarchy
  |     |     +-- Users[] -> User -> UserRoot
  |     +-- World (focused session)
  |     |     +-- RootSlot -> Slot hierarchy
  |     |     +-- Users[] -> User -> UserRoot
  |     +-- World (background session) ...
  +-- InputInterface
  +-- AudioSystem
  +-- AssetManager
  +-- Cloud
  +-- LocalDB
  +-- SecurityManager
```

- `Engine` is the process-level singleton.
- `WorldManager` owns all `World` instances.
- Each `World` has its own slot tree, user list, and update loop.
- Each `User` has one `UserRoot` (once spawned).
- `Userspace` is a static accessor for the special userspace `World`.

---

## World Infrastructure Types

### ConnectorManager
Thread-safe lock manager for connector modifications in a world. Provides `DataModelLock()`, `ImplementerLock()`, `ThreadCheck()`.

### DebugManager
Visual debugging tools for drawing shapes, text, vectors in-world. Methods: `Log()`, `Warning()`, `Error()`, `Text()`, `Vector()`, `Line()`, `Sphere()`, `Box()`, etc.

### LinkManager
Manages drive/link relationships between sync members. Methods: `RequestLink()`, `DriveReleased()`, `GrantLinks()`.

### ReferenceController
Allocates and manages RefIDs for all world elements. Property: `AllObjects` (all registered elements).

### WorldExtensions (static)
Extension methods: `IsUserspace(World)`, `IsPriviledged(World)`.

### WorldAssetReport (static)
Generates text reports of all static assets in a world, grouped by type with size/availability stats.

### FileReceiveJob / FileTransmitJob
Handles receiving/transmitting file assets in chunks over network connections.

### PrioritySyncRefList\<T\>
Ordered list of references sorted by priority.
