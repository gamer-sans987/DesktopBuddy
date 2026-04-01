# FrooxEngine Context Menu, Dash & Data Feeds Reference

Decompiled from FrooxEngine. Covers the context menu system, dash screens, data feeds, and interaction cursors.

---

## Context Menu Components

### ContextMenuItemSource
- **Implements:** `IButton`, `IButtonPressReceiver`, `IButtonHoverReceiver`
- Provides a reusable data source for context menu items
- Fields: `Label` (string), `Color` (colorX), `Sprite`, `ButtonEnabled`, `AllowDrag`, `CloseMenuOnPress`
- Delegates: `Pressed`, `Pressing`, `Released`

### ContextMenuSubmenu
- **Implements:** `IButtonPressReceiver`
- Opens a submenu context menu populated from `ContextMenuItemSource` children
- Fields: `ItemsRoot` (Slot), `SearchWholeHierarchy`, `DisableFlick`, `SpeedOverride`, `CounterClockwise`, `KeepPosition`, `Hidden`

### RootContextMenuItem
- **Base:** `UserRootComponent`
- Defines a user-root context menu item with hand/tool filtering
- Fields: `OnlyForSide` (Chirality?), `ExcludeOnTools`, `ExcludePrimaryHand`, `ExcludeSecondaryHand`

### DataFeedResettableGroup
- **Base:** `DataFeedGroup`
- A data feed group that supports a reset action callback

---

## Interaction Cursors

### InteractionCursorType (enum)
`Default`, `Grab`, `Interact`, `Slide`, `Text`

### InteractionDirection (enum)
`Horizontal`, `Vertical`, `Both`

### BaseInteractionCursorFactory (abstract)
Factory for creating cursor instances appropriate to user settings and interaction type. Methods: `Default()`, `Pointer()`, `Text()`, `Grab()`, `Slide()`, `Interact()`.

### DefaultInteractionCursorFactory
Default implementation using official Resonite cursor assets.

---

## Dash System

### RadiantDashScreen
- **Base:** `Component`, `IUIContainer`
- Base class for screens on the Radiant dash (main menu system)
- Fields: `Icon` (Uri), `ActiveColor` (colorX?), `Label`, `ScreenEnabled`, `BaseResolution`
- Methods: `Show()`, `Hide()`, `CloseContainer()`, `SetResolution()`

(Covered in more detail in file 09-tools-inspectors-utility.md)

---

## Enums Referenced

| Enum | Values | Purpose |
|---|---|---|
| `AlphaHandling` | Opaque, AlphaClip, AlphaBlend | Alpha handling in dual-sided PBS |
| `ColorMask` (byte) | A, B, G, R, None, RGB, RGBA | Color channel write mask |
| `StencilComparison` | Disabled..Always | Stencil buffer comparison |
| `StencilOperation` | Keep..DecrementWrap | Stencil buffer write operation |
| `Sidedness` | Auto, Front, Back, Double | Face rendering control |
| `ZWrite` | Auto, Off, On | Depth buffer write control |
| `BlendMode` | Opaque, Cutout, Alpha, Transparent, Additive, Multiply | Material blending |
| `AudioLoadMode` | Automatic, StreamFromFile, StreamFromMemory, FullyDecode | Audio loading |
| `ImageProjection` | Perspective, Equirectangular180/360 | Image projection type |
| `AssetLoadState` | Created..Unloaded | Asset loading lifecycle |
| `EventState` | None, Begin, Stay, End | Touch/hover event lifecycle |
| `TouchType` | Physical, Remote | Touch interaction type |
| `TouchEventType` | Hover, Touch | Event type distinction |
