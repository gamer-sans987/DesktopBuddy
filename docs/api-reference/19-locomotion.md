# FrooxEngine Locomotion Reference

Decompiled from FrooxEngine. Covers locomotion systems and related types.

---

## Locomotion Overview

The locomotion system was largely empty (0 bytes) in the decompilation output. The following information is gathered from references in other components.

### Key References

- `UserRoot.ActiveLocomotionModule` (`SyncRef<LocomotionModule>`) -- Current movement mode
- `InteractionHandler.LocomotionController` -- Relay ref to the locomotion system
- `CharacterController.MoveDirection` / `Jump` -- Set externally by locomotion modules
- `ScreenController.LocomotionReference` -- Delegated to active targeting controller

### LocomotionState (enum)
State machine states for user locomotion (walking, running, etc.).

### LocomotionMetricsSpace (enum)
Coordinate space for locomotion animation metrics.

### ILocomotionAnimationMetricSource (interface)
Components providing locomotion animation metrics.

### ProtoFlux Locomotion Nodes
The ProtoFlux system includes locomotion control nodes in the `ProtoFlux.Nodes.FrooxEngine.Locomotion` namespace.

---

## CharacterController (Cross-Reference)

The `CharacterController` component (documented in file 07-physics-colliders-touch.md) is the primary physics-based locomotion driver. Key locomotion-related fields:

- `Speed`, `SlidingSpeed`, `AirSpeed` -- Movement speeds
- `TractionForce`, `SlidingForce`, `AirForce` -- Physics forces
- `JumpSpeed`, `SlidingJumpSpeed` -- Jump velocities
- `StepUpHeight` -- Step-up behavior
- `SpeedScaling`, `JumpScaling`, `GravityScaling` -- Scale-dependent behavior
