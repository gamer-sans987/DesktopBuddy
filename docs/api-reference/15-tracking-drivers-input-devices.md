# FrooxEngine Tracking Drivers & Input Devices Reference

Decompiled from FrooxEngine. Covers VR tracking drivers, eye tracking, and input device integration.

---

## Eye Tracking Drivers

### OmniceptTrackingDriver
- **Implements:** `IInputDriver`
- Input driver for HP Omnicept eye tracking via the Glia SDK
- Reads eye gaze, openness, and pupil dilation
- Key methods: `ShouldRegister()`, `RegisterInputs()`, `UpdateInputs()`, `UpdateEye()`, `UpdateGaze()`

### ViveProEyeTrackingDriver
- **Implements:** `IInputDriver`
- Input driver for Vive Pro Eye and SRAnipal lip tracking
- Manages eye gaze, openness, widen/frown/squeeze expressions
- Full lip blend shape mapping (LipData_v2)
- Key methods: `InitializeEyes()`, `InitializeLip()`, `SRAnipalWorker()`, `UpdateEye()`

### HP.Omnicept.GliaHandler
- **Implements:** `IDisposable`
- Manages connection to HP Omnicept runtime
- Receives and caches biometric sensor data: eye tracking, heart rate, cognitive load, IMU, camera, PPG
- Events: `OnHeartRate`, `OnEyeTracking`, `OnCognitiveLoad`, `OnIMUEvent`, etc.

---

## Pointer/Touch Input

### uTouchInjection.Lib (static)
P/Invoke wrapper for the uTouchInjection native DLL. Provides simulated multi-touch input injection on Windows.
- Methods: `Initialize()`, `SetPosition()`, `Touch()`, `Hover()`, `Release()`

### uTouchInjection.Pointer
Represents a single simulated touch pointer. Object-oriented wrapper around native Lib calls.
- Properties: `areaSize`, `position` (int2)
- Methods: `Release()`, `Hover()`, `Touch()`

---

## Pointer Interaction

### PointerInteractionController
- **Base:** `UserRootComponent`, `IInputUpdateReceiver`
- Routes pointer/mouse input to touch interactions in userspace and focused worlds via raycasting
- Manages primary/secondary passthrough pointers

### StaticCameraDevice
- **Base:** `Component`
- Bridges a StaticCamera input device, syncing FOV and aspect ratio for a specific user

---

## Screen/View Controller

### ScreenController
- **Base:** `UserRootComponent`, `IInputUpdateReceiver`
- Desktop/screen-mode view controller managing camera targeting modes
- View modes: First Person, Third Person, UI Camera, Freeform, Userspace
- Key methods: `FocusUI()`, `FocusFreecam()`, `FilterDeviceNode()`, `BeforeInputUpdate()`

### ViewReferenceController
- **Base:** `UserRootComponent`
- Controls the remote view reference (camera icon) shown for desktop users
- Streams position/rotation and manages voice activation based on proximity

---

## Vibration Relay

### VibrationDeviceRelay
- **Implements:** `IVibrationDeviceComponent`
- Relays vibration calls to a target component or dynamically-looked-up component

---

## OfficialAssets Registry

### OfficialAssets (static)
Central registry of all official asset URIs (textures, models, shaders, sounds, skyboxes) used by the engine. Organized into nested static classes. Each property lazily initializes a `resdb:///` URI.

Key nested classes:
- `Common` -- Icons, Indicators, Noise, Particles, Sound_Effect
- `DeviceModels` -- VR controller models (HP Reverb, Index, Oculus, Pico, Vive, WindowsMR)
- `Graphics` -- Fonts, Gradients, Logos, Patterns, ProtoFlux icons, Settings icons, UI elements
- `Shaders`, `Skyboxes`, `Sounds`, `Testing`, `Lib`

### SettingCategoryDefinitions (static)
Defines official setting categories (Audio, Controls, Devices, Graphics, etc.) with icons and sort order.

---

## Twitch Integration

### TwitchInterface
- **Base:** `Component`
- Full Twitch chat/pubsub integration component
- Supports messages, raids, subs, follows, rewards, and stream state
- Fields: `TargetUser`, `Channel`, `Connected`, `StreamLive`, `ViewerCount`
