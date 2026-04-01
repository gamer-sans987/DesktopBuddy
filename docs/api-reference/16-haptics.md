# FrooxEngine Haptics System Reference

Decompiled from FrooxEngine. Covers the full haptics pipeline: drivers, managers, point mappers, filters, volumes, and samplers.

---

## Haptic Drivers

### BHapticsDriver
- **Implements:** `IInputDriver`
- Input driver for bHaptics haptic suit
- Initializes and drives haptic points for head, vest, forearm, and foot devices
- Supports force/pain/temperature/vibration feedback
- Nested: `HapticPointData` -- per-point state for animation phase tracking

### OWO Haptics

#### OWOMuscles (enum)
Identifies individual OWO haptic suit muscle zones: Pectoral_R/L, Abdominal_R/L, Arm_R/L, Dorsal_R/L, Lumbar_R/L.

#### OWOTCPClient
TCP client for communicating with the OWO haptic suit app. Sends sensation commands by ID and muscle target.

---

## HapticManager

**Base:** `UserRootComponent`, `ICustomInspector`

Central manager for the haptic system. Auto-injects haptic sources on avatars and coordinates point-to-mapper registration.

### Key Fields
- `InjectedBodyPartIntensity`, `InjectedHandIntensity`, `InjectedHeadIntensity` (float)
- `InjectedRadiusStartRatio`, `EndRatio`, `Power` (float)
- `ShowDebugVisuals` (bool)

### Key Methods
- `TryRegisterPoint(HapticPoint, IHapticPointMapper)` -- Register a point with a mapper
- `RegisterMapper(IHapticPointMapper)` / `UnregisterMapper()` -- Mapper lifecycle
- `Rebuild()` -- Rebuild all point mappings

---

## Haptic Point Mappers

All inherit from `HapticPointMapper` (abstract Component, `IHapticPointMapper`).

### IHapticPointMapper (interface)
- Properties: `Priority`
- Methods: `UpdateHapticPointMapping(HapticManager)`, `RemoveHapticPointMapping(HapticPoint)`

### Mapper Types

| Mapper | Purpose |
|---|---|
| `ControllerHapticPointMapper` | Maps points from controller hardware to a hand side |
| `HeadHapticPointMapper` | Maps head points onto an ellipsoidal head model using pitch/yaw |
| `TorsoHapticPointMapper` | Maps torso points along spine bone chain with width/offset |
| `LegHapticPointMapper` | Maps leg points with variable radius from thigh to ankle |
| `TagHapticPointMapper` | Maps tag-identified points to specific slot positions |

### BoneChainHapticPointMapper (abstract)
Places haptic points along a chain of bones using normalized parameterization. Fields: `BoneChain`, `NormalizedStart`, `NormalizedEnd`.

### LimbHapticPointMapper\<T\> (abstract)
Generic mapper for limb-type points with side, axis configuration, and radial offset.

---

## Haptic Filters

All inherit from `HapticFilter` (abstract Component). Each computes an intensity multiplier for a `HapticSampler`.

| Filter | Modulates By |
|---|---|
| `RadialDistanceHapticFilter` | Spherical distance from haptic volume center |
| `AxisDistanceHapticFilter` | Distance along a specified axis with power curve |
| `CylindricalDistanceHapticFilter` | Cylindrical distance (radius + axis offset) |
| `ImpactTimeHapticFilter` | Elapsed time since initial impact with decay |
| `VelocityHapticFilter` | Velocity of the sampler point with smoothing |
| `SineHapticFilter` | Sine wave based on time/distance/axis |
| `SimplexNoiseHapticFilter` | 3D simplex noise for spatial variation |
| `ValueNoiseHapticFilter` | Random noise between min/max values |

---

## Haptic Sources and Volumes

### HapticVolume
- **Implements:** `IHapticIntensity`, `IHapticSource`
- Defines a spatial haptic source volume
- Computes intensity using attached `HapticFilter` components
- Fields: `Sensation` (SensationClass), `Intensity` (float), `SensationHints` (string list)

### DirectTagHapticSource
- **Implements:** `IDirectHapticSource`
- Directly drives a haptic point by tag name
- Fields: `HapticTag`, `Force`, `Temperature`, `Pain`, `Vibration`

### HierarchyHapticsSource
- Periodically triggers vibration on a target hierarchy at configurable interval and intensity

### IHapticSource (interface)
- Properties: `LastGlobalImpactTime`
- Methods: `InformRegistered(HapticSampler)`, `InformUnregistered(HapticSampler)`

---

## Haptic Samplers

### HapticPointSamplerBase (abstract)
Manages sphere collider for detecting haptic volume contacts. Fields: `Radius`, `ShowDebugVisual`.

### HapticPointSampler
Samples haptic data for a specific hardware haptic point index, creating collision-based interactions.

### VirtualHapticPointSampler
Creates a virtual haptic point not tied to hardware, outputting sampled force/pain/temperature/vibration values.

### HapticPointData
Exposes haptic point sensor data as synced fields for a specific haptic point index.
