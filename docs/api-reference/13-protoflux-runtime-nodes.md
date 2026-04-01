# ProtoFlux Core & FrooxEngine Node Reference

Documentation for the ProtoFlux execution runtime, core nodes, and FrooxEngine-specific nodes.

---

## ProtoFlux.Core

The execution runtime: stack machine, context management, node base classes, DSP pipeline, and change tracking.

### Namespace: `ProtoFlux.Runtimes.Execution`

#### `ExecutionContext`
Central execution state for ProtoFlux evaluation and impulse flow.

- **Fields/Props**: `Values` (ValueStack), `Objects` (ObjectStack), `SharedScope` (SharedExecutionScope), `MaxDepth` (default 256), `AutoYieldSafetyDepth` (default 128), `CurrentDepth`, `AbortExecution`, `CurrentRuntime`, `CurrentNestedNode`
- **Inner structs**: `StackFrame` (valueBottom, objectBottom, pinCount, sourceFrame, runtime, nestedNode, sharedScope), `StackLayout` (layout array, valueSize, objectSize)
- **Key methods**: `ReadValue<T>(index)`, `ReadObject<T>(index)`, `PopInputs()`, `AllocateFrame()`, `DeallocateFrame()`, `PinFrame()`, `UnpinFrame()`, `EnterExecution()`, `ExitExecution()`, `TryEnterAsyncExecution()`, `CaptureContextFrom()`, `CaptureContextPath()`
- Stack overflow throws at `MaxDepth`. Async paths auto-yield near `AutoYieldSafetyDepth`.

#### `ExtendedExecutionContext<C>`
Extends `ExecutionContext` with event dispatching, update dispatching, and change tracking.

- **Props**: `Updates` (ExecutionUpdateDispatcher), `Changes` (ExecutionChangesDispatcher), `ScheduledEventCount`
- **Methods**: `DispatchEvents()`, `SetEventDispatcher()`

#### `ValueStack`
10KB byte-array stack for unmanaged value types. Push/Pop/Read/Write by byte offset. Supports dual-ended allocation (Top grows up, Bottom grows down from end).

#### `ObjectStack`
10K-slot object-reference stack. Same dual-ended design as ValueStack but for heap objects.

#### `SharedExecutionScope`
Manages shared persistent storage across execution contexts.

- **Fields**: `RootScope` (ScopePoint), `ValuesStore` (16KB byte[]), `ObjectsStore` (4K object[])
- **Methods**: `GetNestedScopeOrAllocate()`, `CaptureScopeAndSwap()`, `Clear()`

#### `ScopePoint`
A node in the scope hierarchy tree. Each runtime/nested-node pair gets its own scope.

- **Props**: `Parent`, `Key` (ScopeKey), `Depth`, `ValuesStoreOffset`, `ObjectsStoreOffset`, `AreGlobalsMapped`
- **Methods**: `GetNestedScope()`, `AllocateScope()`, `ReadGlobal<T>()`, `WriteGlobal<T>()`

#### `ScopeKey`
Readonly struct identifying a scope: `(IExecutionRuntime runtime, IExecutionNestedNode nestedNode)`.

#### `SimpleGlobalValue<T>`
Implements `IGlobalValue<T>`. A simple read/write global variable.

#### `ExecutionContextExtensions` (static)
Extension methods that bridge nodes to the context:
- `ReadValue<T>()` / `ReadObject<T>()` on ValueArgument/ObjectArgument
- `Evaluate<T>()` on ValueInput/ObjectInput (null-source returns default)
- `Write<T>()` on ValueOutput/ObjectOutput
- `Execute()` / `ExecuteAsync()` on Call/AsyncCall/IImpulse

---

### Namespace: `ProtoFlux.Runtimes.Execution` (continued) -- Node Base Classes

#### `ExecutionNode<C>` (abstract)
Base for all executable ProtoFlux nodes. Holds `ExecutionMetadata` with input/output/local/store layouts.

- **Key overridables**: `Evaluate(C context)`, `CanBeEvaluated`
- **Metadata**: `FixedValueStackSize`, `FixedObjectStackSize`, `FixedLocalsCount`, `FixedStoresCount`

#### `ValueFunctionNode<C, T>` : ExecutionNode
Computes a single unmanaged value output. Override `Compute(C context)` to return T. Result pushed onto value stack.

#### `ObjectFunctionNode<C, T>` : ExecutionNode
Computes a single object output. Override `Compute(C context)`. Result pushed onto object stack.

#### `VoidNode<C>` : ExecutionNode
Multiple outputs. Override `ComputeOutputs(C context)`.

#### `ActionNode<C>` : ExecutionNode
Sync impulse handler. Override `Run(C context) -> IOperation`. Cannot be evaluated (data-only).

#### `ActionFlowNode<C>` : ActionNode
Has `Continuation Next`. Override `Do(C context)`, automatically continues to Next.

#### `ActionBreakableFlowNode<C>` : ActionNode
Has `Continuation Next`. Override `Do(C context) -> bool`. Returns false to break flow (null operation).

#### `AsyncActionNode<C>` : ExecutionNode
Async impulse handler. Override `RunAsync(C context) -> Task<IOperation>`.

#### `AsyncActionFlowNode<C>` : AsyncActionNode
Has `Continuation Next`. Override `Do(C context) -> Task`.

#### `AsyncActionBreakableFlowNode<C>` : AsyncActionNode
Has `Continuation Next`. Override `Do(C context) -> Task<bool>`.

#### `NestedNode<C>` : VoidNode
Represents a sub-graph (e.g., If/Loop body). Implements `IExecutionNestedNode`. Manages target frame allocation for nested execution.

---

### `IExecutionRuntime` / `ExecutionRuntime<C>`
Manages a set of nodes, their evaluation sequences, operation sequences, and local/store memory.

- **Key methods**: `EvaluateValue<T>()`, `EvaluateObject<T>()`, `Execute()`, `ExecuteAsync()`, `Rebuild()`
- Rebuild computes local/store offsets, builds evaluation DAGs, and operation chains.
- Tracks `RequiresScopeData`, `ValueStoreSize`, `ObjectStoreSize`, `TotalValueStackSize`, `TotalObjectStackSize`.

### `ExecutionNodeMetadata`
Reflects on node fields to discover inputs, outputs, operations, locals, stores, and global refs. Generates `DefaultFixedStackLayout`.

---

### Event/Update/Change Dispatchers

#### `ExecutionEventDispatcher<C>`
Schedules and dispatches events (e.g., UserJoined, collision) into the ProtoFlux execution loop. Uses `NodeContextPath` to route events to the correct scope.

#### `ExecutionUpdateDispatcher<C>`
Manages per-frame update callbacks for nodes (e.g., SmoothLerp updates).

#### `ExecutionChangesDispatcher<C>`
Tracks output changes and notifies listeners. Used by the change-tracking system to determine which nodes need re-evaluation.

---

### Namespace: `ProtoFlux.Runtimes.Execution.Nodes`

#### Delay Nodes
- `DelayTime` (abstract) -- async delay with `OnTriggered` callback before wait
- `DelayTimeSpan`, `DelaySecondsInt`, `DelaySecondsFloat`, `DelaySecondsDouble` -- concrete variants
- `DelayTimeWithValue<T>` / `DelayTimeWithObject<T>` -- carry data through the delay
- `ValueConstant<T>` / `ObjectConstant<T>` -- emit a fixed value
- `Box<T>` -- boxes an unmanaged value to object
- `ContinuouslyChangingValueRelay<T>` / `ContinuouslyChangingObjectRelay<T>` -- passthrough that marks output as continuously changing
- `ValueDemultiplex<T>` / `ObjectDemultiplex<T>` -- route a value to one of N outputs by index
- `ExternalValueDisplay<C,T>` / `ExternalObjectDisplay<C,T>` -- display nodes that fire `OnDisplay` on change
- `ExternalValueInput<C,T>` / `ExternalObjectInput<C,T>` -- external input nodes with change notification

### Namespace: `ProtoFlux.Runtimes.Execution.Nodes.Casts`

- `ValueCast<I, O>` -- abstract base for unmanaged type casts
- `ObjectCast<I, O>` -- reference type cast (`as` semantics)
- `ValueToObjectCast<I>` -- box unmanaged to object
- `NullableToObjectCast<I>` -- nullable to object

---

### Namespace: `ProtoFlux.Runtimes.DSP`

Audio/DSP pipeline built on ProtoFlux. Separate from the impulse execution model.

- `DSP_Action<...>` -- a step in a DSP sequence; manages buffer dependencies
- `DSP_Buffer` -- abstract audio buffer
- `DSP_BuildContext<...>` -- collects nodes into sequences, resolves dependencies
- `DSP_Sequence<...>` -- ordered chain of DSP actions with dependency tracking
- `DSP_Context<...>` -- runtime DSP context managing input/output buffers

---

### Namespace: `ProtoFlux.Core`

#### `NodeGroup`
Container for multiple `NodeRuntime` instances. Manages change tracking across the group.

- **Key methods**: `RebuildChangeTrackingData()`, `OutputChanged()`, `AllChanged()`, `MarkChangeTrackingDirty()`
- Tracks `ContinuousChanges`, `ChangeListeners` (output -> listener nodes), `NestedGroups`

#### `CastHelper`
Discovers and caches cast node types at runtime via reflection (ValueCastAttribute, ObjectCastAttribute). `GetCastNode(from, to, runtime)` finds the appropriate cast node.

#### Attributes
- `ContinuouslyChangingAttribute` -- marks a node/output as changing every frame
- `ChangeListenerAttribute` -- marks a node as listening to changes
- `ChangeSourceAttribute` -- marks a node as producing changes

---

## ProtoFlux.Nodes.Core

Platform-independent computation nodes. All use `ExecutionContext` (not FrooxEngineContext).

### `ProtoFlux.Nodes.Core`

- `IToStringWrapper` / `ICultureProvider` -- interfaces for culture-aware string formatting
- `Configuration` -- static holder for `ToStringWrapper` and `ActiveCultureProvider`

### Parsing/Formatting
Massive set of `ToString_<Type>` and `Parse_<Type>` nodes for every primitive and vector type (bool, byte, ushort, uint, ulong, sbyte, short, int, long, float, double, decimal, char, bool2..4, byte2..4, ..., float4x4, double4x4, floatQ, doubleQ, colorX, DateTime, TimeSpan, Guid, etc.).

Pattern: `ToString_X : ObjectFunctionNode<ExecutionContext, string>` with format/provider inputs. `Parse_X : VoidNode<ExecutionContext>` with `Value` and `Parsed` outputs.

### Strings (`Nodes.Strings`)
String manipulation: `Substring`, `IndexOf`, `Replace`, `Split`, `Join`, `Trim`, `ToUpper`, `ToLower`, `Contains`, `StartsWith`, `EndsWith`, `Format`, `Concat`, `NewLine`, `StringLength`, `IsNullOrEmpty`, `IsNullOrWhiteSpace`, `Reverse`, `PadLeft`, `PadRight`, `RegexReplace`, `RegexIsMatch`, etc.

### Strings.Characters
Character-level operations: `NewLine`, `ToUpper`, `ToLower`, `IsDigit`, `IsLetter`, `CharToInt`, `IntToChar`, etc.

### Operators (`Nodes.Operators`)
Boolean: `AND`, `OR`, `XOR`, `NOT`, `NAND`, `NOR` (single and multi-input variants).
Arithmetic: `Add`, `Sub`, `Mul`, `Div`, `Mod` for all numeric/vector types.
Comparison: `Equals`, `NotEquals`, `Less`, `LessOrEqual`, `Greater`, `GreaterOrEqual`.
Bitwise: `BitwiseAND`, `BitwiseOR`, `BitwiseXOR`, `BitwiseNOT`, `LeftShift`, `RightShift`.
Conditional: `Conditional` (ternary), `ValueMultiplexer`, `ObjectMultiplexer`.
Null: `IsNull`, `IsNotNull`, `NullCoalesce`.
Packing: `Pack/Unpack` for vector types (float2/3/4, int2/3/4, etc.).

### Math (`Nodes.Math`)
Scalar: `Abs`, `Sign`, `Min`, `Max`, `Clamp`, `Lerp`, `InverseLerp`, `Remap`, `Pow`, `Sqrt`, `Log`, `Exp`, `Floor`, `Ceil`, `Round`, `Fract`.
Trig: `Sin`, `Cos`, `Tan`, `Asin`, `Acos`, `Atan`, `Atan2`.
Vector: `Dot`, `Cross`, `Normalize`, `Magnitude`, `Distance`, `Reflect`, `Project`.
Matrix: operations on float2x2..float4x4, double variants.

### Math Sub-namespaces
- **Math.Quantity** -- unit conversions
- **Math.SphericalHarmonics** -- SH evaluation/composition
- **Math.Rects** -- rect operations (Contains, Intersect, Union)
- **Math.Quaternions** -- Slerp, FromEuler, ToEuler, LookRotation, AxisAngle, Inverse, Multiply
- **Math.Physics** -- FrenetFrame
- **Math.Random** -- Random value/vector generation (RandomBool, RandomInt, RandomFloat, RandomFloat2/3/4, RandomColor, etc.)
- **Math.Geometry3D** -- ray/plane/sphere intersections, ClosestPoint, RayPlaneIntersection
- **Math.Geometry2D** -- 2D line/circle intersections
- **Math.Easing** -- full set of easing functions (EaseIn/Out/InOut for Quad, Cubic, Quart, Quint, Sine, Expo, Circ, Elastic, Back, Bounce)
- **Math.Constants** -- Pi, Tau, E, Infinity, NaN, Epsilon
- **Math.Bounds** -- BoundingBox operations

### Enums (`Nodes.Enums`)
Generic enum operations: `EnumToInt`, `IntToEnum`, `EnumToString`, `ParseEnum`, enum flag operations.

### TimeAndDate (`Nodes.TimeAndDate`)
DateTime/TimeSpan construction, decomposition, arithmetic, formatting.

### Color (`Nodes.Color`)
Color space conversions (HSV, HSL, RGB), component extraction, blending.

### Casts (`Nodes.Casts`)
Exhaustive numeric casts between all primitive types. Each tagged with `[ValueCast(from, to)]`.

### Binary (`Nodes.Binary`)
Bitwise operations on integers.

### Actions (`Nodes.Actions`)
- `WriteBase<C, T>` -- base for writing to IVariable
- `IncrementBase` / `DecrementBase` -- ++/-- on variables
- `ValueIncrement<T>` / `ValueDecrement<T>` -- direct reference
- `ValueIndirectIncrement<T>` / `ValueIndirectDecrement<T>` -- via ObjectInput
- `PulseRandom` -- randomly fires one of N continuations

### Utility
- `IndexOfFirstValueMatch<T>` / `IndexOfFirstObjectMatch<T>` -- find first match in a list
- `PickRandomValue<T>` / `PickRandomObject<T>` -- random selection
- `InvariantCulture`, `CurrentCulture`, `EmptyGUID`, `RandomGUID`, `ParseGUID`
- `NiceTypeName` -- human-readable type name
- `StringToAbsoluteURI` -- string to Uri conversion

---

## ProtoFlux.Nodes.FrooxEngine

Resonite-specific nodes using `FrooxEngineContext`.

### `ProtoFlux.Nodes.FrooxEngine`

#### `ProtoFluxMapper`
Maps legacy node names to current types. Contains `genericMappings` dictionary and a large `MapNode(name, namespace)` switch for backward compatibility. Also handles register name -> `DataModelValueFieldStore<>` / `DataModelObjectFieldStore<>` resolution.

#### Display Nodes
- `ValueDisplay<T>`, `GenericValueDisplay<T>`, `ObjectDisplay<T>` -- FrooxEngine-bound display nodes

### Math / Interpolation
- `SmoothLerpBase<T>` -- per-frame smooth interpolation with `_current`, `_intermediate` stores. Input: `Input`, `Speed`.
- `ValueSmoothLerp<T>`, `SmoothSlerp_floatQ`, `SmoothSlerp_doubleQ`
- `ConstantLerpBase<T>` -- per-frame constant-speed interpolation
- `ValueConstantLerp<T>`, `ConstantSlerp_floatQ`, `ConstantSlerp_doubleQ`
- `MulDeltaTime<T>`, `DivDeltaTime<T>` -- multiply/divide by delta time
- `ValueDelta<T>` -- computes per-frame delta of a value

### Math.Bounds (FrooxEngine)
- `TransformBounds` -- transform BoundingBox between coordinate spaces
- `ComputeBoundingBox` -- compute bounds of a Slot hierarchy

### Worlds
- `WorldURLActionNode` (abstract) -- base for async world actions with URL/WorldLink inputs
- `OpenWorld` -- opens a world (URL or link), outputs SessionID/SessionURL, fires OnOpenStart/OnOpenDone/OnWorldReady/OnOpenFail
- `FocusWorld` -- focuses an existing world, optional close current
- `WorldSaved` -- event node, fires on world save
- `UserJoined` / `UserLeft` / `UserSpawn` -- event nodes with proxy components, OnlyHost filter
- World info nodes: `WorldName`, `WorldDescription`, `WorldSessionID`, `WorldSessionURL`, `WorldSessionWebURL`, `WorldMobileFriendly`, `WorldMaxUsers`, `WorldUserCount`, `WorldActiveUserCount`, `WorldAccessLevel`, `WorldHideFromListing`, `WorldAwayKickEnabled`, `WorldAwayKickMinutes`, `WorldAwayKickInterval`, `WorldWebURL`, `WorldRecordURL`, `WorldPath`

### Transform
- `GlobalTransform` / `LocalTransform` -- read position/rotation/scale of a Slot
- Direction getters: `GetForward`, `GetUp`, `GetRight`, `GetBackward`, `GetDown`, `GetLeft`
- `TransformSetter` (abstract base) -- all setters extend this
- Setters: `SetGlobalPosition`, `SetGlobalRotation`, `SetGlobalScale`, `SetGlobalPositionRotation`, `SetGlobalTransform`, `SetGlobalTransformMatrix`, `SetLocalPosition`, `SetLocalRotation`, `SetLocalScale`, `SetLocalPositionRotation`, `SetLocalTransform`, `SetTRS`, `SetForward`, `SetUp`, `SetRight`, `SetBackward`, `SetDown`, `SetLeft`
- Space conversions: `GlobalPointToLocal`, `LocalPointToGlobal`, `TransformPoint`, `GlobalDirectionToLocal`, `LocalDirectionToGlobal`, `TransformDirection`, `GlobalVectorToLocal`, `LocalVectorToGlobal`, `TransformVector`, `GlobalRotationToLocal`, `LocalRotationToGlobal`, `TransformRotation`, `GlobalScaleToLocal`, `LocalScaleToGlobal`, `TransformScale`
- `SetUserScale` -- async, sets user's avatar scale

### Users
- `LocalUser`, `HostUser`, `LocalUserRoot`, `LocalUserSlot`, `LocalUserSpace`
- `UserUserID`, `UserMachineID`
- `IsUserHost`, `IsLocalUser`, `IsUserPresent`, `IsUserPresentInHeadset`, `IsUserPresentInWorld`, `IsUserLagging`
- `IsAppDashOpened`, `IsPlatformDashOpened`, `AreAppFacetsOpened`

### Users.Roots, Users.LocalScreen, Users.LocalOutput
User root access, local screen info, local output configuration nodes.

### Security
Permission and access control nodes.

### Undo
Undo system integration nodes.

### Interactions
- `NotifyModified` -- notifies a component was modified

### Utility (FrooxEngine)
- `DelayBase<T>` / `DelayValue<T>` / `DelayObject<T>` -- time-delayed value buffers (queue-based)
- `TypeColor` -- gets the display color for a Type

### Rendering
- `RenderToTextureAsset` -- renders a Camera to a texture asset (async). Max 8192 resolution. Outputs `RenderedAssetURL`.
- Additional rendering nodes for screenshot/baking operations.

### Nodes (FrooxEngine.Nodes)
Component/slot manipulation nodes.

### Physics
Collision, raycasting, rigidbody, and force application nodes.

### Operators (FrooxEngine)
FrooxEngine-specific operator nodes.

### Network
HTTP request nodes, WebSocket nodes.

### Interaction
Grab, touch, pointer, laser, and interaction event nodes.

### Interaction.Tools
Tool equipping, tooltip, and tool-related nodes.

### Interaction.Focusing
Focus and context menu nodes.

### Input
- **Mouse**: `MousePosition`, `MouseButtonState`, `MouseScrollDelta`
- **Keyboard**: key state, text input nodes
- **Headsets**: HMD tracking nodes
- **Haptics**: vibration/haptic feedback nodes
- **Display**: display/screen info nodes
- **Controllers**: `GenericController`, `IndexController`, `ViveController`, `WindowsMRController`, etc.

### Experimental
Experimental/preview feature nodes.

### Debugging
Debug logging, breakpoint, and diagnostic nodes.

### Elements
UI element manipulation nodes.

### Slots (large section)
Slot hierarchy operations: `FindChildByName`, `FindChildByTag`, `GetParent`, `GetChild`, `GetChildCount`, `GetActiveChild`, `DuplicateSlot`, `DestroySlot`, `SetSlotActive`, `GetSlotActive`, `GetSlotName`, `SetSlotName`, `GetSlotTag`, `SetSlotTag`, `GetObjectRoot`, `GetSlotPersistent`, `SetSlotPersistent`, `SetSlotOrderOffset`, `GetSlotOrderOffset`, etc.

### References
Reference manipulation nodes.

### Playback
- `PlaybackState`, `ClipLengthFloat`, `Toggle` (pause/resume), and other media playback control nodes.

### Time
- `WorldTimeFloat`, `WorldTime10Float`, `WorldTimeHalfFloat`, `WorldTimeTenthFloat`
- `ElapsedTimeFloat` -- elapsed time since node was active
- `Stopwatch` -- start/stop/reset stopwatch
- `SecondsTimer` -- fires impulse at interval
- `LocalImpulseTimeoutSeconds` -- throttle impulse frequency

### Variables
- `DataModelValueFieldStore<T>` -- persistent value register (backed by `Sync<T>` on a proxy component). Implements `IVariable<FrooxEngineContext, T>`. Fires change events.
- `DataModelObjectFieldStore<T>` -- same for object types
- `DataModelObjectRefStore<T>` -- reference register
- `DataModelUserRefStore` -- user reference register
- `DataModelBooleanToggle` -- boolean latch with Set/Reset/Toggle operations
- `ReadDynamicValueVariable<T>` / `ReadDynamicObjectVariable<T>` -- read dynamic variables
- `WriteDynamicValueVariable<T>` / `WriteDynamicObjectVariable<T>` -- write dynamic variables
- `CreateDynamicValueVariable<T>` / `CreateDynamicObjectVariable<T>` -- create dynamic variables
- `WriteOrCreateDynamicValueVariable<T>` / `WriteOrCreateDynamicObjectVariable<T>`
- `DynamicVariableValueInput<T>` / `DynamicVariableObjectInput<T>` -- input from dynamic variable
- `DynamicVariableValueInputWithEvents<T>` / `DynamicVariableObjectInputWithEvents<T>` -- with change events
- `FireOnValueChange<T>` / `FireOnObjectValueChange<T>` / `FireOnRefChange<T>` -- fire impulse on value change
- `FireOnLocalValueChange<T>` / `FireOnLocalObjectChange<T>` / `FireOnLocalTrue` / `FireOnLocalFalse`
- `FireOnTypeChange`
- `DynamicImpulseReceiverWithValue<T>` / `DynamicImpulseTriggerWithValue<T>` / `DynamicImpulseReceiverWithObject<T>` / `DynamicImpulseTriggerWithObject<T>`

### Components
Component access and manipulation nodes.

### Cloud
- Cloud variable read/write nodes: `ReadValueCloudVariable<T>`, `ReadObjectCloudVariable<T>`, `WriteValueCloudVariable<T>`, `WriteObjectCloudVariable<T>`

### Cloud.Twitch
Twitch integration nodes (chat, subscriptions, PubSub events).

### Locomotion
Locomotion control nodes.

### Avatar
Avatar and body tracking nodes.

### Avatar.BodyNodes
Body node classification: `IsEye`, `GetSide`, `OtherSide`, etc.

### Avatar.Anchors
Anchor attachment/detachment nodes.

### Audio
Audio playback, volume, and spatial audio nodes.

### Async (FrooxEngine)
- `DelayUpdates` -- delay by N update frames
- `DelayUpdatesWithValue<T>` / `DelayUpdatesWithObject<T>` -- with data
- `DelayUpdatesOrTime` -- delay by whichever is longer: N updates or T seconds
- Various duration type variants (TimeSpan, int, float, double)
- `StartAsyncTask` -- start a task on the FrooxEngine coroutine system

### Assets
Asset loading, baking, and management nodes.
- `SampleValueAnimationTrack<T>`, `SampleObjectAnimationTrack<T>` -- sample animation tracks
- `SampleColorX` -- sample color values

### Animation
Animation playback and sampling nodes.

---

## ProtoFluxBindings

Auto-generated binding code. Not documented here. Contains the glue that connects ProtoFlux nodes to the FrooxEngine data model (component proxies, field bindings, weaved methods). Referenced by the FrooxEngine node implementations but not useful to read directly.

---

## Key Patterns

1. **Value vs Object**: Unmanaged types use `Value*` (ValueInput, ValueOutput, ValueArgument, ValueStore). Heap types use `Object*`. This distinction runs through every layer.

2. **FrooxEngineContext vs ExecutionContext**: Core nodes use `ExecutionContext`. FrooxEngine nodes use `FrooxEngineContext` which adds access to `World`, `Engine`, `Cloud`, `Time`, etc.

3. **Proxy Pattern**: FrooxEngine event nodes (UserJoined, FireOnChange, etc.) use proxy components (`ProtoFluxEngineProxy` subclasses) attached to the world. The proxy receives engine events and schedules them into ProtoFlux via the `ExecutionEventDispatcher`.

4. **Store vs Local**: `ValueStore<T>` / `ObjectStore<T>` persist across evaluations in the SharedExecutionScope. `ValueLocal<T>` / `ObjectLocal<T>` are per-evaluation temporaries.

5. **NodeWeaver (Fody)**: Many methods throw `NotImplementedException("This method must be replaced by NodeWeaver")`. The Fody weaver rewrites IL at build time to generate efficient stack read/write code.

6. **Change Tracking**: Nodes marked `[ContinuouslyChanging]` re-evaluate every frame. Others use `[ChangeSource]` / `[ChangeListener]` for event-driven re-evaluation via `ChangeTrackingData`.

---

## ProtoFlux Method Proxy System

Files 26-27 of the decompiled source contain auto-generated ProtoFlux proxy node variants for calling data model methods from ProtoFlux. Each variant differs in which type parameters are value types vs reference types.

### Proxy Patterns

| Pattern | Purpose |
|---|---|
| `SyncMethodProxy_XXXX` | Synchronously calls an N-parameter void method |
| `SyncValueFunctionProxy_XXXX` | Synchronously calls a function returning a value type |
| `SyncObjectFunctionProxy_XXXX` | Synchronously calls a function returning a reference type |
| `AsyncMethodProxy_XXXX` | Asynchronously calls an N-parameter void method |
| `AsyncValueFunctionProxy_XXXX` | Async function returning value type |
| `AsyncObjectFunctionProxy_XXXX` | Async function returning reference type |

The hex suffix encodes which of the 8 type parameters are value types via a bitmask. All extend `DataModelMethodProxy` or `DataModelAsyncMethodProxy`.
