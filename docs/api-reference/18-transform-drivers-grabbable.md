# FrooxEngine Transform Drivers & Grabbable Reference

Decompiled from FrooxEngine. Covers transform manipulation components and grab interaction.

---

## Grabbable (Extended)

The `Grabbable` component is documented in file 06. Additional grab-related types:

### GrabToolSnapper
Snaps a tool to the interaction handler's tip position when grabbed.

### GripPoseReference
Defines hand grip pose reference for tools, with heuristic auto-positioning and visual preview. Fields: `HandSide` (Chirality), `TipReference`, `ShowVisual`.

### GripPoseReferenceEditor
Runtime editor for adjusting grip pose references via slider manipulation.

### IGrabAlignable (interface)
Provides an aligned pose when grabbed: `GetGrabAlignmentPose(Grabber, out float3, out floatQ, out float3)`.

### ITouchGrabbable (interface)
Touch-based grabbing: `TryGrab(Component, float3)`, `Release(...)`.

---

## Interaction Handler (Extended)

### InteractionHandlerPermissions
Permission component controlling which tools a user's InteractionHandler can equip, using a whitelist/rule system.

### InteractionHandlerRelay
Simple relay that references an InteractionHandler for indirect lookups.

### InteractionHandlerStreamDriver
Streams interaction handler state (laser, grab distance, blocked) to other users via ValueStreams.

---

## Reference/Value Source Interfaces

| Interface | Purpose |
|---|---|
| `IReferenceSource` / `IReferenceSource<T>` | Components providing a world element reference |
| `IValueSource` / `IValueSource<T>` | Components providing a typed or boxed value |

---

## Tool Interaction Types

### InteractionHandlerPermissions.ToolRule
Nested SyncObject matching a tool type and specifying allow/deny.

### ISecondaryActionReceiver (interface)
Components receiving secondary action triggers.

### InteractiveCameraObjectExcludeTool
Tool that adds/removes objects from an InteractiveCamera's exclude render list.
