# UIX, Interaction, and Rendering

Covers UIX rendering, touch/interaction pipeline, texture display, context menus, grabbables, and playback in FrooxEngine.

---

## 1. Canvas

**Class:** `Canvas : Component, ITouchable, ITouchGrabbable, IBounded, IInteractionTarget, IRenderable, ...`

The root of all UIX rendering. Owns a `BoxCollider`, a `RectTransform` tree, and manages `GraphicsChunk` objects that produce meshes.

### Key Fields

| Field | Type | Purpose |
|---|---|---|
| `Size` | `Sync<float2>` | Canvas pixel dimensions (default 1920x1080) |
| `AcceptRemoteTouch` | `Sync<bool>` | Accept laser touches (default true) |
| `AcceptPhysicalTouch` | `Sync<bool>` | Accept finger/hand touches (default true) |
| `AcceptExistingTouch` | `Sync<bool>` | Accept touches already interacting with something |
| `IgnoreTouchesFromBehind` | `Sync<bool>` | Reject touches from behind the canvas plane (default true) |
| `LaserPassThrough` | `Sync<bool>` | If true, laser only hits where a Graphic exists |
| `BlockAllInteractions` | `Sync<bool>` | Reject all touch events |
| `PixelScale` / `UnitScale` | `Sync<float>` | Scale factors for rendering |
| `StartingOffset` | `Sync<int>` | Render order offset (default -32000) |
| `Collider` | `SyncRef<BoxCollider>` | Auto-attached collider for raycasting |
| `EditModeOnly` | `Sync<bool>` | Only interactable in edit mode |
| `HighPriorityIntegration` | `Sync<bool>` | Prioritize mesh uploads |

### Rendering Pipeline

1. `OnChanges()` triggers `StartCanvasUpdate()` which increments the cycle index.
2. `PrepareCanvasUpdate()` processes dirty `RectTransform` list, sorts children, handles removed transforms.
3. `ComputeCanvasUpdate()` runs on a background thread:
   - Processes structure changes, creates/removes `GraphicsChunk` objects.
   - Runs pre-layout and pre-graphics compute on dirty transforms.
   - Computes rect layout starting from root: `_root.ComputeRect(ref parentRect, null)`.
   - Each `GraphicsChunk` calls `ComputeGraphics()` which invokes `Graphic.ComputeGraphic()` on its children.
   - Updates bounds.
4. `FinishCanvasUpdate()` runs on main thread: calls `SubmitGraphics()` which uploads meshes and sets render order.

### Touch/Interaction Pipeline (the critical path)

```
ITouchable.OnTouch(TouchEventInfo) 
  -> Canvas.ProcessTouchEvent(TouchEventInfo, filters)
```

**ProcessTouchEvent** does the following:

1. **Rejects** if: not enabled, touch from behind, blocked, wrong type, or `EventState.End`.
2. **Converts** the global touch point to local canvas coordinates: `Slot.GlobalPointToLocal(point).xy * UnitScale`.
3. **Gets or creates** an `InteractionData` per `TouchSource` (pooled via `Pool<InteractionData>`).
4. **Hit-tests** via `GetIntersectingTransformsIntern(point, rects)` which walks the `RectTransform` tree.
5. **Updates hovers**: calls `UpdateHovers()` on hoverable components found at hit rects.
6. **Gets interactables**: walks hit rects upward via `GetComponentInParents<IUIInteractable>()`, collecting unique interactables.
7. **Iterates interactables** front-to-back:
   - Sets `hover` and `touch` states on the `InteractionData`.
   - Manages `touchLock` -- once a touch begins on an element, it stays locked to it (`TouchExitLock`).
   - Calls `iUIInteractable.ProcessEvent(interactionData)`.
   - If `ProcessEvent` returns `true`, the element consumes the event and becomes the `CurrentInteractable`.
   - If it returns `false`, the next interactable in the list is tried.
8. If no interactable consumed the event, `FinishCurrentInteraction()` sends End events to the previous interactable.

### InteractionData (inner class)

Holds per-source interaction state:

- `position`, `lastPosition` -- current and previous canvas-local coordinates
- `hover`, `touch` -- `EventState` enum: `None`, `Begin`, `Stay`, `End`
- `initialTouchPosition`, `initialTouchTime` -- where/when the touch started
- `source` -- the `TouchSource` (laser, finger, etc.)
- `touchLock` -- the `IUIInteractable` that owns the current touch
- `currentInteractables` -- list of interactables at current point
- `filters` -- predicates to filter interactables

---

## 2. UIController

**Class:** `UIController : UIComputeComponent` (abstract)

Base class for components that participate in UIX layout/interaction but may not render anything.

- `InteractionTarget` -- virtual, default `false`. Override to `true` to receive interaction.
- `OnComputingBounds(float2 offset)` -- called during bounds computation.
- `OnPostComputeSelfRect()`, `OnPostComputeRectChildren()` -- layout hooks.
- `FlagChanges(RectTransform)`, `PrepareCompute()` -- abstract hooks for change tracking.

---

## 3. InteractionElement

**Class:** `InteractionElement : UIController, IUIInteractable` (abstract)

Base for all interactive UIX elements (buttons, sliders, etc.). Implements `IUIInteractable.ProcessEvent()`.

### Key Fields

- `BaseColor` (`Sync<colorX>`) -- base tint for color drivers
- `ColorDrivers` (`SyncList<ColorDriver>`) -- list of color drivers that tint targets based on state
- `IsPressed`, `IsHovering` (`Sync<bool>`) -- current interaction state

### ProcessEvent Logic

```csharp
bool ProcessEvent(Canvas.InteractionData eventData)
```

1. If disabled: force end hover/press, return `false`.
2. Track previous `IsHovering`/`IsPressed` state.
3. Update `IsHovering` from `eventData.hover` (Begin/Stay = true, else false).
4. Update `IsPressed` from `eventData.touch`.
5. **Lock-in detection**: If not locked in and pressed, check if movement exceeds `PASS_THRESHOLD` (16 pixels):
   - If movement exceeds threshold in an axis where `PassOnVerticalMovement`/`PassOnHorizontalMovement` is true: **return false** (pass to parent scroll, etc.).
   - If movement exceeds threshold in a non-pass axis: lock in.
6. Fire virtual callbacks based on state transitions:
   - `OnHoverBegin`, `OnHoverStay`, `OnHoverEnd`
   - `OnPressBegin`, `OnPressStay`, `OnPressEnd`
7. Call `ProcessInteractionEvent()` (abstract, Button returns `true`).

### ColorDriver (nested class)

Drives a `colorX` field through Normal/Highlight/Press/Disabled colors with modes: Explicit (default), Multiply, Additive, Direct.

`SetColors(colorX c)` auto-generates highlight/press colors using HSV shifts.

---

## 4. Button

**Class:** `Button : InteractionElement, IButton`

### Key Fields

- `Pressed`, `Pressing`, `Released` -- `SyncDelegate<ButtonEventHandler>` (networked handlers)
- `HoverEnter`, `HoverStay`, `HoverLeave` -- `SyncDelegate<ButtonEventHandler>` (networked)
- `RequireLockInToPress` -- must drag past threshold before press fires
- `RequireInitialPress` -- `TouchEnterLock` = true, meaning touch must begin on this button
- `PassThroughHorizontalMovement`, `PassThroughVerticalMovement` -- default `true`
- `ClearFocusOnPress` -- default `true`
- `SendSlotEvents` -- default `true`, fires `IButtonPressReceiver`/`IButtonHoverReceiver` on slot

### Local Events (critical for mods)

```csharp
public event ButtonEventHandler LocalPressed;
public event ButtonEventHandler LocalPressing;
public event ButtonEventHandler LocalReleased;
public event ButtonEventHandler LocalHoverEnter;
public event ButtonEventHandler LocalHoverStay;
public event ButtonEventHandler LocalHoverLeave;
```

**These are C# events, NOT synced.** They fire only on the local client. Use `LocalPressed` for mod-injected lambdas that should not be serialized/synced.

### Event Firing Order (e.g. RunPressed)

1. `Pressed.Target?.Invoke(this, eventData)` -- synced delegate
2. `this.LocalPressed?.Invoke(this, eventData)` -- local C# event
3. If `SendSlotEvents`: `Slot.ForeachComponent<IButtonPressReceiver>(r => r.Pressed(this, eventData))`

### Helper Properties

- `Label` -- finds first `Text` component in children
- `LabelText` -- get/set the label string
- `Icon` -- finds first `Image` in child slots

---

## 5. RawImage

**Class:** `RawImage : Graphic`

Displays a texture directly as a quad. The simplest way to show a texture in UIX.

### Key Fields

| Field | Type | Purpose |
|---|---|---|
| `Texture` | `AssetRef<ITexture2D>` | The texture to display |
| `Material` | `AssetRef<Material>` | Optional override material (null = default UI material) |
| `Tint` | `Sync<colorX>` | Color tint (default White) |
| `UVRect` | `Sync<Rect>` | UV sub-rectangle (default 0,0 to 1,1) |
| `Orientation` | `Sync<RectOrientation>` | UV rotation |
| `PreserveAspect` | `Sync<bool>` | Maintain texture aspect ratio |
| `InteractionTarget` | `Sync<bool>` | Whether this graphic blocks interaction (default true) |

### How to Display a Texture

```
// Simplest: just set Texture
rawImage.Texture.Target = myTexture;  // IAssetProvider<ITexture2D>

// For opaque content (no alpha blending):
rawImage.UseOpaqueMaterial();

// Custom material:
rawImage.Material.Target = myMaterial;
```

### Rendering

`ComputeGraphic()` generates a quad (4 vertices, 2 triangles) into the `MeshX`. The submesh is keyed by `(_material, _textureProvider, TextureMaterialMapper)` -- meaning textures with the same material share a submesh.

`PrepareCompute()` snapshots all field values to local variables for thread-safe background computation.

`IsPointInside()` returns `InteractionTarget.Value` -- if false, clicks pass through.

---

## 6. Image

**Class:** `Image : ImageBase` (ImageBase : Graphic)

Like RawImage but uses a `Sprite` instead of a raw texture. Supports nine-slice, fill rect.

### Key Fields (inherited from ImageBase + own)

- `Sprite` (`AssetRef<Sprite>`) -- the sprite asset (from ImageBase)
- `Material` (`AssetRef<Material>`) -- override material (from ImageBase)
- `Tint` (`Sync<colorX>`) -- color tint
- `PreserveAspect`, `NineSliceSizing`, `FlipHorizontally`, `FlipVertically` -- from ImageBase
- `InteractionTarget` (`Sync<bool>`) -- from ImageBase
- `FillRect` (`Sync<Rect>`) -- from ImageBase

### Texture Display: RawImage vs Image

| | RawImage | Image |
|---|---|---|
| Input | `ITexture2D` directly | `Sprite` (wraps a texture + metadata) |
| Nine-slice | No | Yes |
| Fill rect | No | Yes |
| Simpler for dynamic textures | Yes | No |

**For mod desktop textures, use `RawImage.Texture.Target`** -- it's the simplest path for a dynamically-updating texture.

---

## 7. DesktopInteractionRelay

**Class:** `DesktopInteractionRelay : UIController, IUIInteractable, IFocusable, IUISecondaryActionReceiver`

Bridges UIX touch events to desktop OS input. This is how in-game desktop screens work.

### Key Fields

- `DisplayIndex` (`Sync<int>`) -- which OS display to target
- `UseLegacyTextInput` (`Sync<bool>`) -- use per-character key injection vs string injection

### ProcessEvent (IUIInteractable)

```csharp
bool ProcessEvent(Canvas.InteractionData eventData)
```

1. Check `ShouldProcessEvent()`: must be in Userspace world, desktop not disabled, screen not active.
2. Compute display point: normalize `eventData.position` within `CurrentGlobalRect`, flip Y, map to `Display.Rect`.
3. On `touch == Begin`: call `this.Focus()`.
4. On `hover == End`: remove injected touch pointer.
5. Otherwise: create or update an injected `Pointer` via `InputInterface.InjectTouch()`, updating position and pressed state.
6. Returns `true` (consumes the interaction).

### Touch Injection

- Maintains `Dictionary<TouchSource, Pointer> _activeTouches`.
- Each laser/finger interacting with the relay gets its own injected OS pointer.
- `pointer.Update(displayPoint, isPressed, timeDelta)` moves the OS cursor.
- On hover end or dispose: `InputInterface.RemoveInjectedTouch(pointer)`.

### Right Click

`TriggerSecondary()` maps the point and calls `InputInterface.InjectRightClick(displayPoint)`.

### Keyboard Input

In `OnChanges()`, if this relay has focus:
- `_typeAppend` string -> either `InjectKeyPress` per character (legacy) or `InjectWrite` (modern).
- `_injectKeys` list -> `InjectKeyPress`.

---

## 8. TouchEventRelay

**Class:** `TouchEventRelay : Component, ITouchable`

Relays touch events to other `ITouchable` targets and fires delegates.

### Fields

- `Touched` (`SyncDelegate<TouchEvent>`) -- networked handler
- `AcceptOutOfSightTouch` (`Sync<bool>`)
- `TouchableTargets` (`SyncRefList<ITouchable>`) -- targets to forward to
- `LocalTouched` (C# event) -- local-only handler

### OnTouch Flow

1. Fire `LocalTouched`.
2. Fire `Touched.Target`.
3. For each target in `TouchableTargets`: call `target.OnTouch(touchInfo)`.
4. Has infinite-loop guard via `_isRelaying` flag.

---

## 9. Grabbable

**Class:** `Grabbable : Component, IGrabbable, IInteractionTarget, IObjectRoot`

Makes a slot grabbable by users.

### Key Fields

| Field | Default | Purpose |
|---|---|---|
| `ReparentOnRelease` | true | Return to original parent on release |
| `PreserveUserSpace` | true | If was in user space, return there |
| `DestroyOnRelease` | false | Destroy slot when released |
| `Scalable` | false | Allow two-handed scaling |
| `AllowSteal` | false | Allow other users to grab from current holder |
| `EditModeOnly` | false | Only grabbable in edit mode |
| `DropOnDisable` | true | Release when component is disabled |
| `GrabPriority` | 0 | Higher = preferred when overlapping |
| `ActiveUserFilter` | default | Filter by active user presence |
| `OnlyUsers` | empty | Whitelist of allowed users |
| `Receivable` | true | Can receive dropped items |
| `AllowOnlyPhysicalGrab` | false | Reject laser grabs |

### CanGrab Logic

Returns false if: removed, disabled, already grabbed (unless AllowSteal and not by local user), edit mode mismatch, user filter fail, OnlyUsers miss, position/rotation/scale driven (not hooked), custom check fails, permissions fail.

### Grab/Release Flow

**Grab:**
1. If already grabbed, release first.
2. Save `_lastParent` and `_lastParentIsUserSpace`.
3. Set `_grabber.Target = grabber`.
4. Reparent slot to `holdSlot`.
5. Fire `IGrabEventReceiver.OnGrabbed()` on slot and world root.
6. Fire `OnLocalGrabbed` C# event.

**Release:**
1. Clear `_grabber`.
2. Determine reparent target: `_lastParent` if `ReparentOnRelease`, or `LocalUserSpace` if `PreserveUserSpace`.
3. Check `IGrabbableReparentBlock`.
4. Reparent.
5. Fire events.
6. If `DestroyOnRelease`, destroy slot.

### Local Events

```csharp
public event Action<IGrabbable> OnLocalGrabbed;
public event Action<IGrabbable> OnLocalReleased;
```

---

## 10. ContextMenu

**Class:** `ContextMenu : UserRootComponent`

Radial context menu system. Owned by a single user.

### Key Fields

- `Owner` -- the user who owns this menu
- `Pointer` -- slot used for pointer direction
- `_canvas` -- the Canvas for the radial UI
- `_itemsRoot` -- slot where menu items are parented
- `_state` -- `Closed`, `Opening`, `Opened`
- `_lerp` -- animation progress 0-1
- `_flickEnabled`, `_flickModeActive` -- flick-select mode

### Opening

```csharp
Task<bool> OpenMenu(IWorldElement summoner, Slot pointer, ContextMenuOptions options)
```

1. If already open, close and wait for lerp to reach 0.
2. Set pointer, summoner, state = Opening.
3. Destroy old items, create new `UIBuilder(_itemsRoot.Target)`.

### AddItem (the key API)

```csharp
ContextMenuItem AddItem(in LocaleString label, Uri? icon, in colorX? color, ButtonEventHandler action)
```

Internally calls the private overload which:

1. Creates an `ArcData` via `_ui.Arc(label)` -- this creates an `OutlinedArc` + `ArcSegmentLayout` + `Button` + `Image`.
2. Sets up sprite from icon URI or texture.
3. Configures arc geometry (inner radius, outline, etc.) and materials.
4. Attaches a `ContextMenuItem` component.
5. Sets up 3 `ColorDriver`s on the button (fill, outline, icon).
6. Calls `contextMenuItem.Button.SetupAction(action)` -- **this is a synced delegate**.

**For mod lambdas, do NOT use the `action` parameter** (it requires a `[SyncMethod]`). Instead:

```csharp
var item = menu.AddItem("My Item", iconUri, color, (ButtonEventHandler)null);
// or pass a dummy action, then:
item.Button.LocalPressed += (btn, data) => { /* your lambda here */ };
```

The `LocalPressed` event is a plain C# event, not serialized, perfect for mod code.

### ContextMenuItem

**Class:** `ContextMenuItem : Component, IButtonHoverReceiver, IButtonPressReceiver`

- `Button` -- reference to the arc's Button
- `Label`, `Sprite`, `Icon`, `Color` -- references to the visual elements
- Handles hover highlighting: `HoverEnter` -> `_menu.ItemSelected(this)`, `Pressed` -> `_menu.ItemPressed(this)`

---

## 11. SyncPlayback

**Class:** `SyncPlayback : ConflictingSyncElement, IPlayable`

Network-synchronized playback state for audio/video.

### Key Properties

| Property | Description |
|---|---|
| `IsPlaying` | Computed: `_play && (_loop || position < clipLength)` |
| `Position` | Current playback position in seconds (computed from offset + worldTime * speed) |
| `NormalizedPosition` | 0-1 range |
| `Speed` | Playback speed multiplier |
| `Loop` | Whether to loop |
| `ClipLength` | Duration; set externally. `double.PositiveInfinity` = streaming. |
| `Offset` | Raw time offset used in computation |

### Core Math

Position is computed as:
```
if playing: position = (WorldTime + offset) * speed
if stopped: position = offset
```

This means changing speed requires recomputing offset to maintain the same position.

### Methods

- `Play()` -- sets play=true, offset from start position
- `Stop()` -- sets play=false, offset=start
- `Pause()` -- sets play=false, offset=current raw position
- `Resume()` -- sets play=true, recomputes offset
- `TogglePlayback()` -- pause/resume toggle

### Network Sync

Uses delta encoding. When playing, the offset is adjusted for network latency via `World.Time.AdjustTimeOffset()` with a `maxError` tolerance.

### Events

```csharp
public event SyncPlaybackEvent OnPlaybackChange;
```

Fires on any state change (play, pause, seek, speed change).

---

## 12. UIBuilder

**Class:** `UIBuilder` (not a Component -- a helper/builder pattern)

Constructs UIX element trees. Manages a stack of root slots and a style stack.

### Construction

```csharp
// From existing canvas slot:
var ui = new UIBuilder(canvasSlot);

// Create new canvas:
var ui = new UIBuilder(slot, canvasWidth, canvasHeight, canvasScale);
```

### Nesting Model

UIBuilder maintains a **root stack** and a **Current** slot:

- `Next(name)` -- creates a new child slot under `Root`, sets it as `Current`. Attaches `RectTransform`. If root has a layout, attaches `LayoutElement` with style metrics.
- `Nest()` -- pushes `Current` onto the root stack. Subsequent `Next()` calls create children of what was `Current`.
- `NestOut()` -- pops the root stack, restoring the previous parent context.
- `NestInto(slot)` -- pushes an arbitrary slot as the new root.

### Key Builder Methods

| Method | Creates | Notes |
|---|---|---|
| `Text(text)` | `Text` component | Attaches to new slot, applies style font/color/size |
| `Image(color)` | `Image` component | Flat colored rectangle |
| `Image(sprite, tint)` | `Image` with sprite | |
| `RawImage(texture)` | `RawImage` component | **Use for dynamic textures** |
| `Button(text)` | `Image` + `Button` + nested `Text` | Returns the `Button` component |
| `Button(icon, text)` | Split layout with icon + text | |
| `Panel()` | Empty nested container | Auto-nests |
| `Panel(tint)` | `Image` + nest | Returns the Image |
| `HorizontalLayout()` | `HorizontalLayout` | Auto-nests |
| `VerticalLayout()` | `VerticalLayout` | Auto-nests |
| `GridLayout()` | `GridLayout` | Auto-nests |
| `ScrollArea()` | `ScrollRect` + `Mask` | Auto-nests |
| `Slider(height)` | `Slider<float>` | Complex: background + fill + handle |
| `TextField()` | `Button` + `TextField` + `Text` | |
| `Checkbox(label)` | `Button` + `Checkbox` | |
| `Spacer(size)` | Empty element with fixed size | |
| `Mask(color)` | `Image` + `Mask` | |

### Layout Helpers

- `SplitHorizontally(proportions)` / `SplitVertically(proportions)` -- creates proportional anchor-based splits
- `HorizontalHeader/Footer(size, ...)` -- fixed-size header/footer with content area
- `VerticalHeader/Footer(size, ...)` -- same, vertical
- `FitContent()` -- attaches `ContentSizeFitter`

### Style System

`UIBuilder.Style` returns the current `UIStyle` (stack-based via `PushStyle()`/`PopStyle()`). Style fields include:

- `MinWidth`, `MinHeight`, `PreferredWidth`, `PreferredHeight`, `FlexibleWidth`, `FlexibleHeight`
- `ButtonColor`, `TextColor`, `ButtonSpriteColor`
- `TextAlignment`, `TextAutoSizeMin`, `TextAutoSizeMax`
- `Font`, `ButtonSprite`, `NineSliceSizing`
- `PassThroughHorizontalMovement`, `PassThroughVerticalMovement`
- `RequireLockInToPress`

### Button Creation Detail

`Button(text, sprite, spriteUrl, tint, spriteTint)`:

1. `Next("Button")` -- new slot
2. Attach `Image` (background) with tint and optional sprite
3. Attach `Button` component (configures pass-through, lock-in from style)
4. `Nest()` into button slot
5. If has icon+text: `SplitHorizontally()` for icon/text areas
6. Create icon `Image` and/or `Text`
7. `NestOut()`
8. Returns the `Button` component

---

## Complete Interaction Flow: Touch to Button Press

```
User points laser at Canvas
  -> Engine raycasts BoxCollider, calls Canvas.OnTouch(TouchEventInfo)
    -> Canvas.ProcessTouchEvent():
      1. Convert global point to canvas-local coords
      2. Hit-test RectTransform tree
      3. Find IUIInteractable (Button/DesktopInteractionRelay) via GetComponentInParents
      4. Create InteractionData with hover=Begin, touch=Begin/Stay/End
      5. Call interactable.ProcessEvent(interactionData)
        -> InteractionElement.ProcessEvent():
          a. Update IsHovering/IsPressed
          b. Check lock-in threshold (16px)
          c. Fire OnPressBegin -> Button.OnPressBegin()
            -> Button.RunPressed(eventData)
              1. Pressed.Target?.Invoke()     [synced delegate]
              2. LocalPressed?.Invoke()       [local C# event]
              3. IButtonPressReceiver.Pressed  [slot components]
          d. Return true (consumed)
```

## Complete Interaction Flow: Desktop Click Injection

```
User points laser at Canvas containing DesktopInteractionRelay
  -> Canvas.ProcessTouchEvent() as above
    -> DesktopInteractionRelay.ProcessEvent(interactionData):
      1. Map canvas position to OS display coordinates
      2. On touch Begin: Focus() the relay (captures keyboard)
      3. InjectTouch() -> creates OS pointer
      4. pointer.Update(displayPoint, isPressed, delta)
        -> OS receives mouse move + click
      5. Return true
```

---

## Button Interaction Components

FrooxEngine provides many `IButtonPressReceiver` / `IButtonHoverReceiver` components for wiring button events to actions:

### Action Triggers

| Component | Purpose |
|---|---|
| `ButtonActionTrigger` | Invokes arbitrary delegates on press/release |
| `ButtonValueActionTrigger<T>` | Invokes typed delegates with a value argument |
| `ButtonDestroy` | Destroys a target object on press |
| `ButtonToggle` | Toggles a boolean field on press |
| `ButtonValueSet<T>` | Sets a field to a specific value on press |
| `ButtonValueShift<T>` | Increments/decrements a numeric field with clamping/wrapping |
| `ButtonValueCycle<T>` | Cycles a field through a list of values |
| `ButtonEnumShift<E>` | Shifts an enum field by a delta |
| `ButtonReferenceSet<T>` | Sets a reference to a specific target |
| `ButtonReferenceCycle<T>` | Cycles a reference through a list of targets |
| `ButtonStringAppend` | Appends a string to a target field |
| `ButtonStringErase` | Erases characters from a target field |

### Dynamic Impulse Triggers

| Component | Purpose |
|---|---|
| `ButtonDynamicImpulseTrigger` | Fires dynamic impulses with string tags on button/hover events |
| `ButtonDynamicImpulseTriggerWithValue<T>` | Fires impulses with a typed value argument |
| `ButtonDynamicImpulseTriggerWithReference<T>` | Fires impulses with a reference argument |

### Media Controls

| Component | Purpose |
|---|---|
| `ButtonPlaybackAction` | Performs playback actions (play/pause/stop) on button events |
| `ButtonPlaybackSeeker` | Seeks playback position based on press point |
| `ButtonLoopSet` | Toggles/sets loop state on a playable |
| `ButtonAudioClipPlayer` | Plays random audio clips on button events |

### Specialized

| Component | Purpose |
|---|---|
| `ButtonWorldLink` | Opens a world link on press |
| `ButtonOpenHome` | Opens user's/group's home world |
| `ButtonParentUnderUser` | Parents/unparents a slot under local user root |
| `ButtonClipboardCopyText` | Copies string to clipboard |
| `ButtonEditColorX` | Spawns a color picker dialog |
| `ButtonEquipFavoriteAvatar` | Equips favorite avatar from cloud |

### Relay Components

| Component | Purpose |
|---|---|
| `ButtonPressEventRelay` | Relays press events to all receivers on a target slot |
| `ButtonHoverEventRelay` | Relays hover events to all receivers on a target slot |

---

## Common UI Interfaces

| Interface | Purpose |
|---|---|
| `IButton` | Interactive button with press/release events and local C# events |
| `IButtonPressReceiver` | Receives button press events |
| `IButtonHoverReceiver` | Receives button hover events |
| `ICheckbox` | Checkbox UI element |
| `ISlider` | Slider UI element with min/max/increment |
| `ITextField` | Text field element |
| `IRadio` / `IRadioGroup` | Radio button and group |
| `INumericUpDown` | Numeric up/down control |
| `IUIContainer` | Closeable UI container |
| `IFocusable` | Component that can receive keyboard/input focus |

### ButtonEventData (struct)

Carries contextual data for button events: `source` (Component), `globalPoint` (float3), `localPoint` (float2), `normalizedPressPoint` (float2).

### FocusManager

Per-user keyboard/input focus manager. Blocks keyboard input when a focusable element is active.

---

## UI Utility Components

| Component | Purpose |
|---|---|
| `ConfirmationHandler` | Temporarily changes button label/color for confirmation, then reverts |
| `GenericUIContainer` | Generic closeable UI container that destroys a root slot on close |
| `PagingControl` | Manages pagination state and UI for paged item lists |

---

## UI Driver Components

| Component | Purpose |
|---|---|
| `ValueOptionDescriptionDriver<T>` | Drives label/color/sprite based on matching value option |
| `ReferenceOptionDescriptionDriver<T>` | Drives label/color/sprite based on matching reference option |
| `Perspective360Panner` | Pans a 360-degree perspective view based on hover position |

---

## Touch Interaction Components

### PhysicalButton
Physical push button with depth tracking, hold mode, vibration feedback, and press/release events.

### TouchButton
Simple touch-activated button (no depth tracking) with press/release events.

### TouchToggle
Toggles a boolean state on configurable touch event conditions.

### TouchableData
Exposes hover/touch state as synced booleans with configurable vibration feedback.

### TouchValueOption\<T\>
Sets a target field to a specific value on touch; drives an active indicator when matched.

### ScaleElement / ScaleGroup
Selectable elements that smoothly animate between selected/idle/background scales.

### KnobControl
Converts slot rotation changes around an axis into a float value, driving a target field like a knob.
