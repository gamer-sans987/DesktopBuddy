# Physics and Interaction Reference

`RigidBody`, `CommonTool`, and `MaterialTip` are not covered (no source available).

---

## Collider (abstract base)

**Category:** All colliders inherit from this. Uses BepuPhysics under the hood.

### Key Synced Fields
| Field | Type | Default | Notes |
|---|---|---|---|
| `Offset` | `Sync<float3>` | Zero | Local-space offset. Filtered to reject NaN/Infinity. |
| `Type` | `Sync<ColliderType>` | `Static` | Determines physics behavior. See ColliderType table below. |
| `Mass` | `Sync<float>` | `1f` | Only used when `Type` is `CharacterController` (dynamic). Clamped [1e-6, 1e6]. |
| `CharacterCollider` | `Sync<bool>` | false | Enables collision with character controllers. |
| `IgnoreRaycasts` | `Sync<bool>` | false | Excludes from raycast queries. |

### ColliderType Behavior

| Type | Static? | Trigger? | EventSource? | Simulation |
|---|---|---|---|---|
| `NoCollision` | yes | no | no | Main |
| `Static` | yes | no | no | Main |
| `Trigger` | no (body) | yes | no | Main |
| `StaticTrigger` | yes | yes | no | Main |
| `StaticTriggerAuto` | yes | yes | no | Main (auto-upgraded from Trigger in v1) |
| `Active` | no (body) | no | yes | Main |
| `CharacterController` | no (body) | no | yes | Main |
| `HapticTrigger` | no (body) | yes | no | Haptic |
| `HapticStaticTrigger` | yes | yes | no | Haptic |
| `HapticStaticTriggerAuto` | yes | yes | no | Haptic |
| `HapticSampler` | no (body) | no | yes | Haptic |

### Constants
- `MIN_SIZE = 1e-6f`, `MAX_SIZE = 1e6f`, `MAX_POSITION = 1e8f`
- `DEFAULT_SPECULATIVE_MARGIN = 0.1f`

### Contact Events
- `ContactStart`, `ContactStay`, `ContactEnd` -- delegate type `ContactEvent(ICollider self, ICollider other)`
- Registering a listener auto-wakes sleeping bodies.
- `NoCollision` type cannot listen to events (`CanTypeListenToEvents` returns false).

### Gotchas
- `EntityShouldBeActive` checks: Enabled, Slot.IsActive, owner approval, AND whether the active user has collision locally blocked. A collider on a blocked user's hierarchy is silently deactivated.
- Shape updates happen in `OnChanges()`. If the collider type changes, the entire entity is unregistered and re-registered (full teardown/rebuild).
- `ProcessColliderSize` multiplies by `Slot.GlobalScale` -- non-uniform scale is applied.
- `ProcessColliderOffset` rotates the offset into world-aligned local space (inverse global rotation applied).
- Mass scaling (when owned by CharacterController) supports `Linear` and `Cubic` modes via `IColliderOwner.MassScaling`.
- Setting an invalid offset (NaN/Infinity) logs a warning and falls back to `float3.Zero`.

### Convenience Methods
- `SetStatic()`, `SetActive()`, `SetTrigger()`, `SetNoCollision()`, `SetCharacterCollider()` (sets CharacterCollider=true, Type=Static)

---

## BoxCollider

**Category:** `Physics/Colliders`

### Fields
| Field | Type | Default |
|---|---|---|
| `Size` | `Sync<float3>` | `float3.One` |

### Notes
- Speculative margin capped to `Max(size.x, size.y, size.z)`.
- Has `SetFromLocalBounds()`, `SetFromGlobalBounds()`, and precise variants that compute bounds from the slot hierarchy (excluding self).
- Implements `IHighlightable` -- generates a BoxMesh-based highlight driven from Size/Offset.

---

## SphereCollider

**Category:** `Physics/Colliders`

### Fields
| Field | Type | Default |
|---|---|---|
| `Radius` | `Sync<float>` | `0.5f` |

### Notes
- Radius is averaged across all 3 scaled axes: `(size.x + size.y + size.z) / 3`. Non-uniform scale produces an averaged sphere, not an ellipsoid.
- `SetFromLocalBounds` / `SetFromGlobalBounds` compute a bounding sphere. Precise variant uses Ritter's algorithm.
- Implements `IHighlightable`.

---

## MeshCollider

**Category:** `Physics/Colliders`

### Fields
| Field | Type | Default |
|---|---|---|
| `Mesh` | (inherited from MeshBasedCollider) | -- |
| `Sidedness` | `Sync<MeshColliderSidedness>` | `Front` |
| `ActualSpeculativeMargin` | `RawOutput<float>` | -- |

### Notes
- Default `Type` is `NoCollision` (not `Static`!). Must be explicitly set.
- `ListenToEvents` returns `false` -- mesh colliders cannot receive contact events.
- `PostprocessContactMask` strips the `ACTIVE_FLAG` bit -- mesh colliders cannot participate as Active colliders in contact pairs.
- If `Type` is `Active`, the v2 migration auto-downgrades it to `Static` with a log message.
- Uses `ComputeColliderScale()` for non-uniform mesh scaling.
- For large meshes (>10k vertices) without metadata, speculative margin is set to 0.
- Inspector provides "VHACD" (convex decomposition) and "Replace with Box" buttons.
- `SetCharacterCollider()` forces `Sidedness` to `Front`.

### Gotchas
- `EntityChanged` is triggered by `Offset.WasChanged` (not Type changes). Offset changes cause full entity re-registration.
- The offset is applied via `ProcessPose` (translation in orientation space), not via the shape itself.

---

## CharacterController

**Category:** `Physics`  
**Constraint:** `[SingleInstancePerSlot]`

### Key Synced Fields
| Field | Default | Notes |
|---|---|---|
| `SimulatingUser` | null | Only the assigned user runs physics for this character. |
| `CharacterRoot` | null | Slot moved by physics. Falls back to own Slot. |
| `HeadReference` | null | If set, physics pose is relative to head position/rotation. |
| `SimulateRotation` | false | If false, angular velocity is zeroed and inverse inertia tensor is zeroed. |
| `Gravity` | `(0, -9.81, 0)` | In GravitySpace coordinates. |
| `Speed` | `4f` | Traction speed. |
| `SlidingSpeed` | `3f` | Speed when on slope without traction. |
| `AirSpeed` | `1f` | Speed when airborne. |
| `TractionForce` | `1000f` | Horizontal force on ground with traction. |
| `SlidingForce` | `50f` | Horizontal force on slope. |
| `AirForce` | `250f` | Horizontal force in air. |
| `MaximumGlueForce` | `5000f` | Downward force to keep grounded. |
| `MaximumTractionSlope` | `45f` | Degrees. Max slope for full traction. |
| `MaximumSupportSlope` | `75f` | Degrees. Max slope to count as supported. |
| `JumpSpeed` | `6f` | Upward velocity on jump (traction). |
| `SlidingJumpSpeed` | `3f` | Upward velocity on jump (sliding). |
| `StepUpHeight` | `0.5f` | Max height for step-up behavior. |
| `LinearDamping` | 0 | Range [0,1]. |
| `AngularDamping` | 0 | Range [0,1]. |
| `MassScaling` | `Cubic` | How mass scales with slot scale. |
| `ForceScaling` | `Cubic` | How forces scale. |
| `SpeedScaling` | `Linear` | How speeds scale. |
| `JumpScaling` | `Linear` | How jump speed scales. |
| `GravityScaling` | `Linear` | How gravity scales. |

### Runtime Properties
| Property | Notes |
|---|---|
| `Simulate` | True if `SimulatingUser == LocalUser`. |
| `MoveDirection` | Set externally (e.g., by locomotion). XZ plane. |
| `Jump` | Set externally. Uses Digital state (pressed edge). |
| `ForceKinematic` | Overrides to kinematic, zeroes velocity. |
| `LinearVelocity` | Get/set. Buffered if collider not yet registered. Setting with significant delta triggers unglue. |
| `CurrentGround` | Returns ICollider of ground if has traction, null otherwise. |
| `IsReady` | True when RegisteredCollider is non-null. |

### Lifecycle
- `OnAttach`: if no collider with `Type=CharacterController` exists on the slot, auto-creates a `CapsuleCollider` + `SingleShapeCharacterControllerManager`.
- Scans for the first enabled collider of type `CharacterController` on its slot (sorted by ReferenceID).
- Only one collider is registered at a time.

### Physics Pipeline
1. `Physics_PreUpdate` (before simulation step): reads `MoveDirection`/`Jump`, sets character controller data, positions body at character root.
2. Physics simulation runs.
3. `Physics_PreContactDispatch` (after contacts): reads body pose back to `CharacterRoot.GlobalPosition/Rotation`.

### Gotchas
- `ShouldBeActive` for non-simulating users only returns true if `CharacterCollider` is set AND `SimulatingUser` is assigned. Otherwise the collider is deactivated for remote users.
- When `KillVerticalVelocityAfterStepUp` is true, vertical velocity is redistributed into horizontal after a step-up ends.
- Setting `LinearVelocity` with a large delta relative to current velocity triggers an "unglue" frame where `MaximumVerticalForce` is reduced to 1% -- this prevents the glue force from fighting velocity changes.
- April Fools: if difficulty is "Hard", gravity is doubled.
- `HeadReference` mode: the character body is placed at the head's XZ position projected onto the character root, with a look-rotation derived from the head forward. Physics deltas are applied as offsets rather than absolute positions.

---

## TouchSource (abstract base)

### Key Fields
| Field | Type | Default |
|---|---|---|
| `AutoUpdateUser` | `SyncRef<User>` | null |
| `OutOfSightAngle` | `Sync<float>` | `70f` |
| `MaxTouchPenetrationDistance` | `Sync<float>` | `0.05f` |

### Properties
- `TipPosition`, `TipDirection` -- abstract, defined by subclass.
- `CurrentTouchable` -- the ITouchable currently being touched.
- `LocalForceTouch` -- programmatic force-touch. Setting true latches onto `CurrentTouchable`.
- `IsTouchEventRunning` -- true during `OnTouch` callback dispatch.
- `SafeTouchSource` -- auto-cleared if slot's active user root doesn't match local user.

### Touch Lifecycle
1. `OnCommonUpdate` calls `UpdateTouch()` if `AutoUpdateUser == LocalUser` and world is focused.
2. Subclass `GetTouchable()` finds candidate.
3. If touchable changed, `EndTouch()` fires `EventState.End` on previous.
4. `SendTouchEvent` fires `OnTouch` with combined hover/touch state.
5. Touch is suppressed if angle to `directHitPoint` exceeds `OutOfSightAngle` (unless `CanTouchOutOfSight`).

### Gotchas
- Raycast filter (`_raycastFilter`) excludes colliders under a `UserRoot` -- user avatar colliders are not touchable.
- `EndTouch()` is called on destroy, deactivate, and disable. Touch events are always cleaned up.
- The touchable filter checks both `Enabled` and `CanTouchInteract(this)`.

---

## RaycastTouchSource (abstract)

Extends `TouchSource`. Adds raycast-based touchable detection.

### Key Fields
| Field | Type |
|---|---|
| `CustomFilter` | `SyncDelegate<Func<ICollider, int, bool>>` |

### Properties
- `RayOrigin`, `RayDirection`, `RayLength` -- abstract.
- `CurrentClosestHit` -- the closest RaycastHit from the last update.

### How GetTouchable Works
1. Calls `Physics.PortalRaycastAll` (supports portal traversal).
2. Iterates hits in distance order.
3. For each hit, checks `Distance - ClosestHit.Distance <= MaxTouchPenetrationDistance`.
4. Finds `ITouchable` via `GetComponentInParentsUntilBlock` on the hit collider's slot.
5. First valid touchable that passes `CanTouch(TouchType)` wins.
6. Portal hits modify the reported `direction` to the exit direction.

---

## PointTouchSource

**Category:** `Input/Interaction`  
Extends `RaycastTouchSource`.

### Fields
| Field | Default |
|---|---|
| `Offset` | `float3.Zero` |
| `Direction` | `float3.Forward` |
| `MaxDistance` | `float.MaxValue` |

### Notes
- `RayOrigin` = slot-local `Offset` transformed to global.
- `RayDirection` = slot-local `Direction` transformed to global.
- `TouchType` = `Remote`.
- `TipPosition` always returns `float3.Zero` (not useful for proximity checks).
- `IsTouching` only returns true when `IsForceTouching` -- it never auto-touches on its own.

---

## InteractionHandler

**Category:** (UserRootComponent)  
**Old name:** `CommonTool`

The primary input handler per hand/controller side. Manages laser, grabbing, tools, context menus, locomotion, and self-scaling.

### Key Synced Fields
| Field | Notes |
|---|---|
| `Side` | `Chirality` -- Left or Right. |
| `LocomotionController` | Relay ref to the locomotion system. |
| `GrabSmoothing` | Smoothing factor for laser grabs. |
| `EquippingEnabled` | Whether tool equipping is allowed. |
| `MenuEnabled` | Whether context menu is allowed. |
| `UserScalingEnabled` | Whether two-hand scaling is allowed. |
| `PointingGrab` | -- |
| `PointingTouch` | -- |
| `ShowInteractionHints` | -- |

### Key Properties
| Property | Notes |
|---|---|
| `ActiveTool` | Currently equipped ITool (via ActiveToolLink). |
| `Laser` | The InteractionLaser component. |
| `LaserEnabled` | True if explicitly enabled or active tool uses laser. |
| `Grabber` | The Grabber component for this hand. |
| `IsHoldingObjects` | Whether grabber is holding anything. |
| `IsHoldingObjectsWithLaser` | Holding via laser grab specifically. |
| `CurrentTip` | Active tool tip or raw tip position. |
| `IsContextMenuOpen` / `IsContextMenuVisible` | Context menu state. |
| `BlockPrimary` / `BlockSecondary` | Blocks primary/secondary input dispatch. |
| `CanScale` | Permission + locomotion allow scaling. |
| `HasGripEquippedTool` | Whether a tool is grip-equipped. |

### Enums
- `GrabType`: None, Hand, Laser, Touch
- `HandGrabType`: Palm, Precision, Auto, Off
- `LaserRotationType`: AxisX, AxisY, AxisZ, Unconstrained

### Constants
- `GRAB_RADIUS = 0.07f`
- `MENU_RADIUS = 0.05f`
- `PANIC_HOLD_SECONDS = 2f`
- `EDIT_MODE_PRESS_INTERVAL = 0.25f`
- `USERSPACE_MENU_TOGGLE_DISTANCE = 0.25f`

### Gotchas
- `OtherTool` fetches the other hand's InteractionHandler from the same UserRoot.
- Self-scaling requires BOTH hands gripping, neither holding objects or tools, neither already scaling, userspace grabbers idle, permission + locomotion allowing it, and `UserScalingEnabled` true.
- `MaxLaserDistance` is limited by userspace controller data when in UserspaceWorld (unless recording voice or hitting a laser-priority element).
- `SharesUserspaceToggleAndMenus` returns true when the UserspaceToggle input is not bound (fallback behavior).
- Screen mode (`InputInterface.ScreenActive`) forces projection interaction mode for DevTool.
- `IsNearHead` only returns true in VR, uses 0.25f distance threshold from local head position.

---

## DevTool

**Category:** `Tools`  
**Old name:** `DevToolTip`

The developer tool for selecting and manipulating slots via gizmos.

### Key Fields
| Field | Notes |
|---|---|
| `SelectionMode` | Single or Multi. |
| `InteractionMode` | Tip or Projection. |

### Properties
- `IsDevModeEnabled` -- reads/writes `DevModeController`.
- `UseProjection` -- true if screen mode or InteractionMode is Projection.
- `UsesLaser` -- always true.

### Interaction Flow
1. **Secondary press** (`OnSecondaryPress`): `TryOpenGizmo()` -- finds nearest slot within 0.1f radius of tip, creates/toggles a `SlotGizmo`.
2. **Primary press** (`OnPrimaryPress`): raycasts for `PointAnchor` or `Gizmo`, begins interaction.
3. **Primary hold** (`OnPrimaryHold`): updates gizmo interaction point.
4. **Primary release** (`OnPrimaryRelease`): ends gizmo interaction.

### Gizmo Selection Logic
1. First checks laser raycast hit (if not already a gizmo).
2. Then sphere overlap at tip (radius 0.025f).
3. Then iterates ALL world slots within 0.1f of tip, preferring slots with more components.
4. Skips slots under the user root and existing gizmo slots.
5. ProtoFlux nodes without active visuals are skipped.

### Visual
- Attaches a cone mesh with `OverlayFresnelMaterial` on attach.
- Green fresnel = dev mode on; Red fresnel = dev mode off.

### Context Menu Items
- Create New, Open Inspector, Gizmo Options, Dev Mode toggle, Selection mode, Interaction mode, Deselect All, Destroy Selected.
- Destroy requires confirmation dialog (unless disabled in EditSettings).

### Gotchas
- `TryOpenGizmo` iterates `World.AllSlots` -- potentially expensive in large worlds.
- Inspector creation: if grabbing a `ReferenceProxy`, inspects its target. Otherwise inspects `_currentGizmo.Target` or world root.
- Registers cursor unlock while a gizmo is active (for screen mode).
- `IsMovingTarget` returns true if the target slot is a child of the active gizmo's target (prevents moving what you're manipulating via other means).

---

## Interaction Block Components

| Component | Interface | Purpose |
|---|---|---|
| `DestroyBlock` | `IDestroyBlock` | Prevents destruction of the slot hierarchy |
| `DuplicateBlock` | `IDuplicateBlock` | Prevents duplication of the slot hierarchy |
| `GrabBlock` | `IGrabBlock` | Prevents grabbing of the slot |
| `GrabbableReparentBlock` | `IGrabbableReparentBlock` | Blocks or limits reparenting depth on grabbable release |
| `GrabbableSaveBlock` | `IGrabbableSaveBlock` | Prevents non-builder save options for an item |

All implement `IInteractionBlock` (root marker interface).

---

## Footstep System

### FootstepSoundDefinition
Defines footstep sound clips with velocity-based pitch/volume control and weighted random selection. Key nested type: `FootstepClip` with side/velocity/state matching and pitch/volume curves.

### FootstepEventRelay
Relays footstep events to all `IFootstepEventReceiver` components on a target slot.

### FootstepEventDebugVisualizer
Debug visualization of footstep events showing impact vectors and side info.
