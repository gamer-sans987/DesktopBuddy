# Elements.Core API Reference

Core math, type, serialization, and utility library for Resonite (Yellow Dog Man Studios).
Assembly: `Elements.Core`, namespace: `Elements.Core`.

---

## Table of Contents

- [Vector Types](#vector-types)
- [Matrix Types](#matrix-types)
- [Quaternion Types](#quaternion-types)
- [Color Types](#color-types)
- [Geometry Types](#geometry-types)
- [Transform Types](#transform-types)
- [Animation System](#animation-system)
- [Coder System](#coder-system)
- [Data Tree / Serialization](#data-tree--serialization)
- [Math Utilities (MathX)](#math-utilities-mathx)
- [Noise](#noise)
- [Collections and Data Structures](#collections-and-data-structures)
- [Threading and Async](#threading-and-async)
- [Streams and IO](#streams-and-io)
- [String Processing](#string-processing)
- [Identifiers and Versioning](#identifiers-and-versioning)
- [Utility Classes](#utility-classes)
- [Interfaces](#interfaces)
- [Enums](#enums)

---

## Vector Types

All vector types are `readonly struct`, implement `IVector<T>`, `IEquatable`, and `IFormattable`. Numeric vectors also implement `INumericVector`. Each has a corresponding `Extensions` static class with Save/Load/Write/Read helpers for serialization.

### IVector / INumericVector

```csharp
public interface IVector {
    int Dimensions { get; }
    Type ElementType { get; }
    object GetBoxedElement(int n);
    object GetWithElementsAveraged();
    object GetWithAllElementsSetTo(int n);
}
public interface INumericVector : IVector {
    object Normalized { get; }
}
public interface IVector<T> : IVector { }
```

### bool2 / bool3 / bool4

Boolean vector types. Fields: `x`, `y` (and `z`, `w` for 3/4). Supports component-wise AND/OR/NOT/XOR, `Any()`, `All()`, `None()`, swizzle properties (e.g. `xy`, `yx`, `xyz`, etc.).

### Numeric Vectors (2D, 3D, 4D)

Pattern: `{type}{dim}` where type is `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double` and dim is `2`, `3`, `4`.

Examples: `float2`, `float3`, `float4`, `int2`, `int3`, `int4`, `double3`, etc.

**Common members across all numeric vectors:**

- **Fields:** `x`, `y` (+ `z` for 3D, + `w` for 4D) -- all `readonly`
- **Statics:** `Zero`, `One`, `MinValue`, `MaxValue`; float/double also have `NaN`, `PositiveInfinity`, `NegativeInfinity`
- **Properties:** `Dimensions`, `ElementType`, `Magnitude`, `SqrMagnitude`, `Normalized` (float/double only)
- **Operators:** `+`, `-`, `*`, `/`, `%`, `==`, `!=`, `<`, `>`, `<=`, `>=` (component-wise)
- **Swizzle properties:** All permutations (e.g. `float3.xzy`, `float4.wzyx`, etc.)
- **Casts:** Explicit casts between vector types of same dimension (e.g. `int3` to `float3`)
- **Constructors:** scalar broadcast `(T v)`, per-component `(T x, T y, ...)`, mixed `(vec2, T z)`, etc.

### half2 / half3 / half4

Half-precision float vectors. Implement `IVector<Half>`. Same pattern as numeric vectors but using `System.Half`.

**Fields:** `x`, `y` (+ `z`, `w`)

---

## Matrix Types

### float2x2 / double2x2

2x2 matrix. Implements `IMatrix<float>` / `IMatrix<double>`.

- **Fields:** `m00`, `m01`, `m10`, `m11`
- **Statics:** `Identity`, `Zero`
- **Methods:** `Determinant`, `IsIdentity`
- **Operators:** `+`, `-`, `*`, `==`, `!=`

### float3x3 / double3x3

3x3 matrix.

- **Fields:** `m00`..`m22` (9 components)
- **Statics:** `Identity`, `Zero`
- **Methods:** `Determinant`, `IsIdentity`, `Transpose`
- **Operators:** `+`, `-`, `*` (matrix and vector), `==`, `!=`

### float4x4 / double4x4

4x4 matrix. The main transform matrix type.

- **Fields:** `m00`..`m33` (16 components)
- **Statics:** `Identity`, `Zero`
- **Methods:** `Determinant`, `Inverse`, `Transpose`, `IsIdentity`
- **Operators:** `+`, `-`, `*` (matrix, vector, scalar), `==`, `!=`
- **Static constructors:** TRS construction, projection matrices, etc.
- **Extensions:** `Float4x4Extensions` / `Double4x4Extensions` for Save/Load/Write/Read

---

## Quaternion Types

### IQuaternion / IQuaternion\<T\>

```csharp
public interface IQuaternion {
    Type ElementType { get; }
}
public interface IQuaternion<T> : IQuaternion {
    T x { get; }
    T y { get; }
    T z { get; }
    T w { get; }
}
```

### floatQ

Single-precision quaternion. Readonly struct.

- **Fields:** `x`, `y`, `z`, `w` (readonly float)
- **Statics:** `Identity` (0,0,0,1), `MinValue`, `MaxValue`
- **Properties:** `SqrMagnitude`, `Magnitude`, `Normalized`, `Conjugate`, `Inverse`, `EulerAngles`
- **Operators:** `*` (quaternion composition and vector rotation), `==`, `!=`
- **Static methods:** `LookRotation(forward, up)`, `FromToRotation(from, to)`, `Euler(pitch, yaw, roll)`, `AxisAngle(axis, angle)`, `Slerp`, `Lerp`, `LerpUnclamped`, `Dot`, `Angle`, `AngleRad`
- **Extensions:** `FloatQExtensions` for Save/Load/Write/Read

### doubleQ

Double-precision quaternion. Same API as `floatQ` but with `double` precision.

---

## Color Types

### color

Readonly struct. Linear RGBA color with float channels.

- **Fields:** `r`, `g`, `b`, `a` (readonly float)
- **Properties:** `Luminance`, `Inverted`, `MagnitudeRGB`, `MaxComponent`, `MinComponent`
- **Named colors:** `Clear`, `White`, `Black`, `Red`, `Green`, `Blue`, `Yellow`, `Cyan`, `Magenta`, `Orange`, `Purple`, `LightGray`, `Gray`, `DarkGray`, `Azure`, etc.
- **Operators:** `+`, `-`, `*`, `/`, `==`, `!=`
- **Methods:** `SetR/G/B/A(v)`, `SetValue(v)`, `SetSaturation(v)`, `SetHue(v)`, `ToHexString()`, `MulRGB(f)`, `AddRGB(f)`, `NormalizeHDR(out gain)`, `AlphaBlend(over)`
- **Constructors:** `(float r, float g, float b, float a = 1)`, `(float3 rgb, float a)`, from hex string

### colorX

Readonly struct. Color-profile-aware color. Wraps a `color` with a `ColorProfile`.

- **Fields:** `baseColor` (color), `profile` (ColorProfile)
- **Properties:** `LinearColor` (converts to linear), `Luminance`, `Inverted`
- **Named colors:** Same as `color`, all default to linear profile
- **Operators:** `*`, `==`, `!=`
- **Methods:** `SetR/G/B/A(v)`, `SetProfile(p)`, `ConvertTo(profile)`, `SetComponent(channel, value)`
- **Implicit cast:** `color` -> `colorX` (assumes linear profile)

### color32

Readonly struct. 32-bit RGBA color (byte per channel).

- **Fields:** `r`, `g`, `b`, `a` (readonly byte)
- **Operators:** `==`, `!=`
- **Conversions:** Explicit cast to/from `color`

### ColorHSL

Mutable struct. HSL color representation.

- **Fields:** `h`, `s`, `l`, `a` (float)
- **Methods:** `FromRGB(color)`, `ToRGB()` -- conversion to/from RGB

### ColorHSV

Mutable struct. HSV color representation.

- **Fields:** `h`, `s`, `v`, `a` (float)
- **Methods:** `FromRGB(color)`, `ToRGB()` -- conversion to/from RGB

### ColorProfile (enum, in colorX context)

Used by `colorX`. Values: `Linear`, `sRGB`, `sRGBAlpha`.

### ColorProfileHelper

Static class. Converts colors between profiles (linear, sRGB, sRGBAlpha).

- `ConvertProfile(color, inProfile, outProfile)`
- `ToLinear(color, profile)`, `FromLinear(color, profile)`
- Constants: `GAMMA_22`, `INVERSE_GAMMA_22`

### ColorProfileAwareOperation (enum)

Controls which color profile is used during math operations on `colorX`. Values: `UseLHS`, `UseRHS`, `UseLinear`, `LinearIfUnequal`.

### ColorChannel (enum)

`R`, `G`, `B`, `A` -- for indexing color components.

---

## Geometry Types

### BoundingBox

Mutable struct. Axis-aligned bounding box in 3D.

- **Fields:** `min`, `max` (float3)
- **Properties:** `IsEmpty`, `IsInfinite`, `IsInfiniteOnX/Y/Z`, `Center`, `Size`, `Extents`, `Volume`, `SurfaceArea`
- **Methods:** `Encapsulate(point/box)`, `Contains(point)`, `Intersects(box)`, `ClosestPoint(point)`, `Transform(float4x4)`, `VertexPoint(index)`, `Expand(amount)`
- **Statics:** `Empty()`, `Infinite()`, `CenterExtents(center, extents)`, `CenterSize(center, size)`, `MinMax(min, max)`
- **Constant:** `VERTEX_POINT_COUNT = 8`

### BoundingBox2D

Mutable struct. 2D AABB.

- **Fields:** `min`, `max` (float2)
- **Properties:** `Center`, `Size`
- **Methods:** `Encapsulate(float2)`, `Contains(float2)`, enumerable over corners

### BoundingSphere

Mutable struct. Sphere in 3D.

- **Fields:** `center` (float3), `radius` (float)
- **Methods:** `Encapsulate(point/sphere)`, `Contains(point)`, `Intersects(sphere/box/ray)`, `Transform(float4x4)`
- **Statics:** `Empty()`, `MinimalBounding(points)`

### Ray

Readonly struct. 3D ray.

- **Fields:** `origin`, `direction` (float3)

### Rect

Mutable struct. 2D rectangle.

- **Fields:** `position`, `size` (float2)
- **Properties:** `xmin`, `xmax`, `ymin`, `ymax`, `center`, `width`, `height`
- **Methods:** `Contains(float2)`, `Encapsulate(float2)`, `Intersects(Rect)`
- **Statics:** `MinMaxRect(xmin, ymin, xmax, ymax)`

### IntRect

Mutable struct. Integer 2D rectangle.

- **Fields:** `x`, `y`, `width`, `height` (int)
- **Properties:** `Center`, `Area`, `Left`, `Right`, `Top`, `Bottom`
- **Methods:** `Contains(int2)`, `Intersects(IntRect)`, `Encapsulate(int2/IntRect)`, `Clip(IntRect)`

---

## Transform Types

### Transform

Readonly struct. Full 3D transform (position + rotation + scale).

- **Fields:** `position` (float3), `rotation` (floatQ), `scale` (float3)
- **Implicit casts:** to/from `RenderTransform`

### RigidTransform

Readonly struct. Position + rotation only (no scale).

- **Fields:** `position` (float3), `rotation` (floatQ)

---

## Animation System

### AnimX

Main animation container class. Stores named animation tracks.

- **Fields:** `Name` (string), `GlobalDuration` (float), `Readonly` (bool)
- **Properties:** `TrackCount`, indexer `[int]` returns `AnimationTrack`
- **Methods:** `AddTrack(type)`, `GetMaxTrackDuration()`, `Encode(stream)` / `Decode(stream)`, `MakeReadOnly()`
- **Constants:** `ANIMX_BINARY_VERSION = 1`, `MAGIC_STRING = "AnimX"`
- **Encoding enum:** `Plain`, `LZ4`, `LZMA`

### AnimationTrack (abstract)

Base class for all animation tracks.

- **Fields:** `Node` (string), `Property` (string)
- **Properties:** `Duration`, `FrameCount`, `FrameType`, `TrackType`, `Animation`

### TrackType (enum)

`Raw`, `Discrete`, `Curve`, `Bezier`

### CurveAnimationTrack\<T\>

Interpolated keyframe track. Supports keyframe insertion, removal, evaluation at time.

- **Key methods:** `InsertKeyFrame(time, value, interpolation)`, `EvaluateAtTime(time)`, `RemoveKeyFrame(index)`
- **Concrete types:** `CurveBoolAnimationTrack`, `CurveFloatAnimationTrack`, `CurveFloat3AnimationTrack`, `CurveFloatQAnimationTrack`, `CurveColorAnimationTrack`, `CurveColorXAnimationTrack`, etc. (one per supported engine type)

### DiscreteAnimationTrack\<T\>

Step-function track. No interpolation between keyframes.

- **Concrete types:** `DiscreteBoolAnimationTrack`, `DiscreteStringAnimationTrack`, `DiscreteFloatAnimationTrack`, etc.

### RawAnimationTrack\<T\>

Fixed-interval sampled track. Data stored as raw arrays.

- **Concrete types:** `RawBoolAnimationTrack`, `RawFloatAnimationTrack`, `RawFloat3AnimationTrack`, etc.

### Keyframe\<T\> (readonly struct)

- **Fields:** `time` (float), `value` (T), `interpolation` (KeyframeInterpolation)

### DiscreteKeyframe\<T\> (readonly struct)

- **Fields:** `time` (float), `value` (T)

### KeyframeInterpolation (enum)

`Hold`, `Linear`, `Tangent`

### IAnimationTrack / IAnimationTrack\<T\>

```csharp
public interface IAnimationTrack {
    AnimX Animation { get; }
    string Node { get; }
    string Property { get; }
    float Duration { get; }
    int FrameCount { get; }
    Type FrameType { get; }
    TrackType TrackType { get; }
}
public interface IAnimationTrack<T> : IAnimationTrack {
    T EvaluateAtTime(float time);
}
```

---

## Coder System

### Coder (static)

Reflection-based type system for engine primitives. Determines if types can be used in the data model.

- **Properties:** `BaseEnginePrimitiveCount`, `BaseEnginePrimitives` (IEnumerable\<Type\>)
- **Extension methods on Type:** `IsBaseEnginePrimitive()`, `IsEnginePrimitive()`, `SupportsScale()`, `SupportsConstantLerp()`, `SupportsSmoothLerp()`, `GetIdentity()`, `GetDefault()`
- **Base engine primitives include:** all C# numeric types, string, Uri, bool, char, all vector types (bool2..double4), all matrix types, floatQ, doubleQ, DateTime, TimeSpan, color, colorX, color32, RefID, Half, Rect, BoundingBox, dummy

### Coder\<T\> (static generic)

Per-type encoding/decoding/math operations. Provides a uniform interface for generic math on engine types.

- **Properties:** `Identity`, `Default`, `MinValue`, `MaxValue`, `IsSupported`, `IsEnginePrimitive`
- **Capability checks:** `SupportsApproximateComparison`, `SupportsComparison`, `SupportsDistance`, `SupportsAddSub`, `SupportsNegate`, `SupportsMul`, `SupportsScale`, `SupportsDiv`, `SupportsMod`, `SupportsMinMax`, `SupportsAbs`, `SupportsEncoding`, `SupportsStringCoding`, `SupportsLerp`, `SupportsConstantLerp`, `SupportsSmoothLerp`
- **Delegates (internal):** `_equaler`, `_approximately`, `_comparer`, `_distance`, `_neg`, `_abs`, `_round`, `_add`, `_sub`, `_mul`, `_div`, `_mod`, `_min`, `_max`, `_clamp`, `_shift`, `_scale`, `_power`, `_lerper`, `_inverseLerper`, `_constantLerper`, `_smoothLerper`, `_encoder`, `_decoder`, `_saver`, `_loader`, `_strEncoder`, `_strDecoder`, `_tryParser`

### CoderNullable / CoderNullable\<T\>

Extension of the Coder system for nullable value types.

---

## Data Tree / Serialization

### DataTreeNode (abstract)

Base class for all data tree nodes.

- **Methods:** `EnumerateTree()` -- yields all descendant nodes

### DataTreeDictionary : DataTreeNode

Key-value node. Primary container for saved object data.

- **Properties:** `Children` (Dictionary\<string, DataTreeNode\>), indexer `[string]`
- **Methods:** `Add(key, value)`, `AddOrUpdate(key, node)`, `Remove(key)`, `TryExtract<T>(key, ref value)`, `ExtractOrDefault<T>(key, def)`, `ExtractOrThrow<T>(key)`, `TryGetNode(key)`, `TryGetList(key)`, `TryGetDictionary(key)`, `HasChild(key)`, `CountNodes()`

### DataTreeList : DataTreeNode

Ordered list node.

- **Properties:** `Count`, `Children` (List\<DataTreeNode\>), indexer `[int]`
- **Methods:** `Add(node)`

### DataTreeValue : DataTreeNode

Leaf node holding an `IConvertible` value.

- **Properties:** `Value` (IConvertible), `IsNull`, `IsURL`
- **Static helpers:** `Null()`, value extraction methods (`LoadInt()`, `LoadFloat()`, `LoadString()`, etc.)

### DataTreeConverter (static)

Serializes/deserializes DataTree to binary formats.

- **Compression enum:** `None`, `LZ4`, `LZMA`, `Brotli`
- **Methods:** `Load(file, ext)`, `Save(dict, file, compression)`, `IsSupportedFormat(file)`
- **Supported formats:** `.7zbson`, `.lz4bson`, `.brson`, `.frdt`
- **Constants:** `HEADER = "FrDT"`, `VERSION = 0`

### DataCoding (static)

Low-level encoding helpers for DataTree types.

### SavedGraph

Wraps a `DataTreeDictionary` root with a list of URL nodes extracted from it.

- **Fields:** `Root` (DataTreeDictionary), `URLNodes` (List\<DataTreeValue\>)

### LoadSaveHelper (static)

Helpers for loading/saving data trees and Resonite objects.

### IEncodable (interface)

Binary and DataTree serialization contract.

```csharp
public interface IEncodable {
    void Encode(BinaryWriter writer);
    DataTreeNode Save();
    void Decode(BinaryReader reader);
    void Load(DataTreeNode node);
}
```

---

## Math Utilities (MathX)

Massive static class. Provides math operations for all engine types.

### Scalar Math

Standard math wrappers (float and double overloads):

- **Trig:** `Sin`, `Cos`, `Tan`, `Asin`, `Acos`, `Atan`, `Atan2`, `Sinh`, `Cosh`, `Tanh`, `Asinh`, `Acosh`, `Atanh`
- **Exponential:** `Exp`, `Log`, `Log2`, `Log10`, `Pow`, `Sqrt`, `Cbrt`
- **Rounding:** `Floor`, `Ceil`, `Round`, `Truncate`, `RoundToInt`, `FloorToInt`, `CeilToInt`, `RoundToLong`, etc.
- **Clamping:** `Clamp`, `Clamp01`, `ClampMagnitude`, `Min`, `Max`, `Abs`, `Sign`
- **Interpolation:** `Lerp`, `LerpUnclamped`, `InverseLerp`, `CubicLerp`, `SmoothDamp`, `ConstantLerp`, `DampingFactor`, `Remap`
- **Wrapping:** `Repeat`, `Repeat01`, `PingPong`, `PingPongSafe`, `DeltaAngle`, `WrapAroundDistance`
- **Utility:** `Approximately(a, b, epsilon)`, `IsPowerOfTwo`, `NearestPowerOfTwo`, `CeilToPowerOfTwo`, `Snap`, `Frac`, `PowMagnitude`, `Deadzone`, `FactorialFloat/Double`, `LeastCommonMultiple`, `GreatestCommonDivisor`, `Median`
- **Bits/Bytes:** `BitsToBytes`, `BytesToBits`, `MaxValueForBits`, `NecessaryBits`, `BitRangeMask`
- **Division safety:** `CanDivide(dividend, divisor)`, `CanDivideBy(divisor)` -- for int/uint/long/ulong

### Easing Functions

Full set of easing curves (float and double):

`EaseIn/Out/InOut` + `Sine`, `Quadratic`, `Cubic`, `Quartic`, `Quintic`, `Exponential`, `Circular`, `Rebound`, `Elastic`, `Bounce`

### Vector/Matrix/Quaternion Math

Overloads of `Lerp`, `LerpUnclamped`, `ConstantLerp`, `SmoothLerp`, `Abs`, `Round`, `FilterInvalid`, `BezierCurve`, `BezierTangent`, `Approximately`, `Min`, `Max`, `Clamp`, etc. for:

- All vector types (float2..double4)
- All matrix types (float2x2..double4x4)
- floatQ/doubleQ (also: `Slerp`, `ConstantSlerp`, `SmoothSlerp`, `RotateTowards`, `RotateByAngle`, `LimitSwing`, `LimitTwist`, `DecomposeSwingTwist`, `DecomposeAxisAngle`, `ReflectRotation`, `AngleAroundAxis`)
- color/colorX/color32 (also: `WavelengthColor`, `BlackBodyColor`, color arithmetic)
- SphericalHarmonics types
- DateTime/TimeSpan

### Color-Specific

- `WavelengthColor(nanometers)` / `WavelengthColorX(nanometers)` -- visible spectrum
- `BlackBodyColor(temperature)` / `BlackBodyColorX(temperature)` -- color temperature
- Lookup table: `BlackBodyRadiation` (391 entries, CIE 1964 10-degree CMFs)

### Geometry Helpers

- `SimplexNoise(pos)` -- 1D simplex noise
- `HorizontalFOVFromVertical(fov, aspect)`
- `ArcCircumference(arc, radius)`
- `MinSpherePointDistance(radius, fov)`
- `NormalizeSum(values, targetSum)`
- `BounceLerp01`, `BounceLerp`, `MultiLerp`, `MultiInverseLerp`
- `LineInverseLerp`, `RotationInverseLerp`
- `DoIntervalsIntersect`
- `Gap01`, `ScaleLerp01`, `Progress01`

### Nested Types

- **ArrayWrap (enum):** `Clamp`, `Repeat` -- controls array index wrapping
- **LinePoint (struct):** `position` (float3), `rotation` (floatQ), `distanceFromStart` (float)

---

## Noise

### FastNoise

Full-featured noise generator class.

- **Noise types (enum):** `Value`, `ValueFractal`, `Perlin`, `PerlinFractal`, `Simplex`, `SimplexFractal`, `Cellular`, `WhiteNoise`
- **Interpolation (enum):** `Linear`, `Hermite`, `Quintic`
- **Fractal types (enum):** `FBM`, `Billow`, `RigidMulti`
- **Cellular distance (enum):** `Euclidean`, `Manhattan`, `Natural`
- **Cellular return (enum):** `CellValue`, `NoiseLookup`, `Distance`, `Distance2`, `Distance2Add`, `Distance2Sub`, `Distance2Mul`, `Distance2Div`
- **Configuration:** `m_seed`, `m_frequency`, `m_noiseType`, `m_interp`, `m_fractalType`, `m_octaves`, `m_lacunarity`, `m_gain`
- **Methods:** `GetNoise(x, y)`, `GetNoise(x, y, z)` and variants per noise type

### Noise (static class)

Simpler Perlin noise implementation using a permutation table. Provides 2D/3D/4D noise sampling.

---

## Collections and Data Structures

### Pool / Pool\<T\>

Object pooling system.

- **Pool (static):** `ActivePool` struct, `Borrow<T>()`, `Return<T>(item)`
- **Pool\<T\> (static):** `Borrow()`, `Return(item)`, `Count`, typed pool for `new()` types
- **IPoolable:** Interface with `OnReturnToPool()` callback

### RawList\<T\> / RawValueList\<T\>

High-performance list backed by raw arrays. `RawValueList<T>` constrains T to struct.

- **Key members:** `Count`, `Capacity`, `Add(item)`, `RemoveAt(index)`, `Insert(index, item)`, `Clear()`, `Sort()`, `TrimExcess()`, `AsSpan()`, indexer

### SlimList\<T\> / SlimPoolList\<T\>

Memory-efficient list. `SlimList` starts with inline storage (0 heap allocs for small counts). `SlimPoolList` uses pooled backing arrays.

- **Key members:** Same IList\<T\> interface as standard list

### BiDictionary\<A, B\>

Bidirectional dictionary. Lookups in both directions.

- **Methods:** `Add(a, b)`, `GetByA(a)`, `GetByB(b)`, `RemoveByA(a)`, `RemoveByB(b)`, `ContainsA(a)`, `ContainsB(b)`

### DictionaryList\<K, T\>

Dictionary mapping keys to lists of values.

- **Methods:** `Add(key, value)`, `Remove(key, value)`, `GetList(key)`, `GetEnumerable(key)`

### DictionaryHashSet\<K, T\>

Dictionary mapping keys to hash sets of values.

### PathDictionary\<K, T\>

Trie-like dictionary keyed by paths (sequences of K).

### KeyCounter\<T\>

Counts occurrences per key.

- **Methods:** `Increment(key)`, `Decrement(key)`, `GetCount(key)`, `HasCount(key)`

### Indexer\<T\>

Assigns sequential integer indices to items.

### WeightedList\<T\>

List with per-item weights for weighted random selection.

### SpinQueue\<T\> / SpinStack\<T\> / SpinHashSet\<T\>

Thread-safe lock-free (spinlock) collections.

### BitQueue / BitStack

Bit-level queue/stack. Store individual bits efficiently.

### Bit2DArray

2D array of bits.

### PrimitiveCircularBuffer\<T\>

Fixed-size circular buffer for value types.

### InterleavedArray\<T\>

Array with interleaved access patterns. Stride-based element access.

### BoundingBoxTree

Spatial acceleration structure for 3D objects. Wraps BepuPhysics `Tree`.

- **Methods:** `Add(id, bounds)`, `Remove(id)`, `Update(id, bounds)`, `Query(bounds, callback)`, `Dispose()`

### SpatialCollection3D\<T\>

Spatial collection using BoundingBoxTree. Associates items with 3D bounds.

### ArrayPool\<T\>

Simple array pooling.

### DataSegmentChain / DataSegment

Chain of data segments for incremental binary data building.

- **DataSegment.Type (enum):** `Null`, `Bool`, `Byte`, `Short`, `Int`, `Long`, `Float`, `Double`, `Decimal`, `Char`, `String`, `DateTime`, `TimeSpan`, `ByteArray`, `List`, `Dictionary`
- **DataSegment:** `ReadAs<T>()`, `Type`, `IsNull`

### Enumerable Wrappers

- `EnumerableWrapper<T>`, `EnumerableWrapper<T, E>` -- wrap enumerators as IEnumerable
- `ListEnumerableWrapper<T>`, `SlimListEnumerableWrapper<T>` -- wrap lists
- `HashSetEnumerableWrapper<T>` -- wrap hash sets
- `DictionaryEnumerableWrapper<K, T>` -- wrap dictionaries
- `SingleItemEnumerable<T>` -- single-element enumerable
- `EmptyEnumerator<T>` -- always-empty enumerable

### RingBufferHelper (static)

Utility methods for ring buffer index calculations.

---

## Threading and Async

### WorkProcessor

`SynchronizationContext` implementation with thread pool. Dispatches work to worker threads.

- **WorkType (enum):** `Background`, `Foreground`
- **Key methods:** `Enqueue(action, workType)`, `Dispose()`
- **Internal:** `ThreadWorker` manages individual threads

### AsyncLock

Async-compatible mutex.

- **Methods:** `Lock()` returns `IDisposable` lock handle
- **Internal:** `LockObject` implements the disposable pattern

### WorkStealingTaskScheduler

Custom `TaskScheduler` with work-stealing queues for efficient parallel execution.

### Job / Job\<T\>

Awaitable job system. Implements `INotifyCompletion`.

- **Methods:** `GetAwaiter()`, `IsCompleted`, `GetResult()`

### ParallelEx (static)

Extensions for parallel operations.

---

## Streams and IO

### BinaryReaderX / BinaryWriterX

Extended binary reader/writer with support for engine types.

- **BinaryReaderX methods:** `ReadFloat2()`, `ReadFloat3()`, `ReadColor()`, etc.
- **BinaryWriterX methods:** `Write(float2)`, `Write(float3)`, `Write(color)`, etc.

### BitBinaryReaderX / BitBinaryWriterX

Bit-level binary reader/writer. Extends BinaryReaderX/WriterX.

### BitReaderStream / BitWriterStream

Stream wrappers for bit-level I/O.

### ConcatenatedStream

Reads sequentially from multiple streams as one.

### ExceptionWrapperStream

Wraps a stream and converts exceptions.

### WrappedStream

Base class for stream decorators. Delegates all operations to inner stream.

### StreamProgressWrapper

Stream that reports progress via `IProgressIndicator`.

### NullStream

Stream that discards all writes and returns empty reads.

### ContinuousHasher\<T\>

Stream that computes a rolling hash (SHA256, etc.) as data passes through.

### BinaryReaderExtensions / BinaryWriterExtensions (static)

Extension methods for `BinaryReader`/`BinaryWriter`: read/write enums, engine types, compressed data, etc.

### FileUtil (static)

File I/O helpers.

- **FileHash (enum):** `SHA256`, `MD5`
- **Methods:** `ComputeHash(file, algorithm)`, `CopyDirectory(source, dest)`, `WaitForFile(path)`, file size formatting

### PathUtility (static)

Path manipulation helpers. Normalization, extension checks, etc.

### LZMAHelper (static)

LZMA compression/decompression helpers.

### DataCoding (static)

Low-level binary encoding helpers for engine types.

---

## String Processing

### StringSegment (readonly struct)

Lightweight string slice (offset + length into a source string). Avoids allocation.

- **Fields:** source string, offset, length
- **Methods:** `ToString()`, `Equals()`, comparison, substring operations

### StringSegmentToken

Token parsed from a string segment. Used in rich text parsing.

### StringToken / StringContent / StringOpeningTag / StringClosingTag

Rich text tokenizer output. `StringToken` is the abstract base; content and tags are concrete types.

### StringTokenizer (static)

Parses rich text markup into a tree of `StringToken` objects.

### TagTracker

Tracks open/close tag nesting during string tokenization.

### CharSpanParser / StringSegmentHelper (static)

Low-level parsing helpers for character spans.

### StringHelper (static)

General string utilities.

### StringExtensions (static)

Extension methods on strings.

---

## Identifiers and Versioning

### RefID (readonly struct)

Reference identifier used throughout the data model. Encodes a user index (low 8 bits) and a position (upper 56 bits) into a `ulong`.

- **Fields:** `id` (ulong, private)
- **Properties:** `User` (byte), `Position` (ulong), `IsLocalID`
- **Statics:** `Null`, `MinValue`, `MaxValue`, `Construct(position, user)`
- **Methods:** `ExtractIDs(out position, out user)`, `ToString()` (hex format "ID...")
- **Constants:** `USER_BITS = 8`, `MAX_USERS = 256`, `LOCAL_ID = 255`

### VersionNumber (struct)

Date-based version number.

- **Fields:** `Year`, `Month`, `Day`, `Minute` (int)
- **Properties:** `UTC` (DateTime), `IsValid`
- **Constructors:** from ints, from `DateTime`, from `System.Version`
- **Implements:** `IComparable<VersionNumber>`, `IEquatable<VersionNumber>`

---

## Utility Classes

### RandomX (static) / RandomXGenerator

Thread-safe random number generation.

- **RandomX properties:** `Value` (float 0-1), `Double`, `Int`, `Bool`
- **RandomX methods:** `Range(min, max)`, `Chance(probability)`, `OnUnitSphere`, `InsideUnitSphere`, `OnUnitCircle`, `EulerAngles`, `Rotation`, `String(length, alphabet)`, `Guid`
- **RandomXGenerator:** Instance-based. Same methods, not thread-safe. Seedable.

### SignalX (static)

Audio/signal processing utilities.

- **Interpolation (enum):** `NearestNeighbor`, `Linear`, `Floor`, `Ceil`
- **Methods:** `Normalize(input, output)`, `Gain(input, output, gain)`, `Resample(input, output, inRate, outRate, interpolation)`, `ResampledLength(length, inRate, outRate)`, `Process(input, output, func)`

### BallisticStepper

Simple ballistic physics simulation.

- **Fields:** `Position`, `Velocity`, `Gravity` (float3), `Drag` (float)
- **Methods:** `StepTime(delta)`, `StepDistance(units)`

### UniLog (static)

Unified logging system.

- **Events:** `OnLog`, `OnWarning`, `OnError`, `OnFlush`
- **Methods:** `Log(message)`, `Warning(message)`, `Error(message)`, `Flush()`
- **Config:** `FlushEveryMessage`, `MessagePrefix`

### CryptoHelper (static)

Cryptographic utilities.

- **Methods:** `GenerateCryptoBlob(length)`, `GenerateCryptoToken(length)`, `GenerateReadableCryptoToken(length)`, `GenerateSalt()`, `HashID(string)`, `HashData(byte[])`, `ValidatePassword(password)`
- **Properties:** `PasswordRuleDescription`, `PasswordRequirements`

### RobustParser (static)

Tolerant parsing for all engine primitive types from strings. Handles various formats.

### PrimitiveTryParsers (static)

`TryParse` delegates for all primitive types.

### NiceTypeParser / LegacyTypeParser (static)

Convert between human-readable type names and .NET type names. Handles generic types, arrays, nested types.

### ReflectionExtensions (static)

Reflection helpers: `GetNestedTypes`, `FindAttributeMethods`, `IsGenericTypeOf`, etc.

- **NestedTypeEntry (struct):** `name`, `type` pair
- **AttributeMethod\<A, D\> (struct):** method with attribute of type A and delegate type D

### TypeHelper (static)

Type system utilities: `GetNiceName(type)`, `FindType(name)`, `IsNumeric(type)`, `GetDefaultValue(type)`, type compatibility checks.

### SizeOfHelper (static)

Gets `sizeof` for unmanaged types via reflection/Marshal.

### DynamicStructFieldAccess (static)

Dynamic field access on structs via reflection.

### StructMemberAccessor / StructFieldProxy / NullableFieldProxy / StructPropertyMethodProxy

Reflection-based access to struct fields. Used by the Coder system to decompose structs into elements.

### EnumUtil (static)

Enum utilities: parsing, formatting, flag operations.

### FlagExtensions (static)

Extension methods for enum flags: `HasFlag`, `SetFlag`, `ClearFlag`, `ToggleFlag`.

### CollectionsExtensions (static)

Extension methods for collections: `AddUnique`, `RemoveFirst`, `Shuffle`, `GetOrAdd`, batch operations, etc.

### DataTypeExtensions (static)

Helpers for working with data model types.

### DateTimeExtensions (static)

Extension methods for DateTime: `ToUnixEpoch`, `FromUnixEpoch`, etc.

### SpanExtensions (static)

Extension methods for `Span<T>` / `ReadOnlySpan<T>`.

### ConvexHullHelper (static)

Computes 2D/3D convex hulls from point sets.

### PointMerger (static)

Merges nearby points within a threshold distance.

### KMeansClustering (static)

K-means clustering algorithm implementation.

### RectPackNode

Rectangle packing (bin packing) tree node. Used for texture atlas packing.

- **Methods:** `Insert(width, height)`, `rect`, `child[]`

### ColorProfileHelper (static)

See [Color Types](#color-types).

### BepuConversions (static)

Conversion helpers between Elements.Core types and BepuPhysics types.

### AsyncExtensions (static)

Async/Task utility extensions.

### ExceptionHelper (static)

Exception formatting and handling.

### PrimitiveCodeHelper (static)

Helpers for primitive type encoding.

### PrimitivesUtility (static)

Utility methods for working with C# primitive types.

### MonoVersion (static)

Mono runtime version detection.

### WebClientEx : WebClient

Extended WebClient with timeout support.

---

## Interfaces

### IAddable\<T\> / ISubtractable\<T\> / IScalable\<T\> / ILerpable\<T\>

Arithmetic/interpolation interfaces used by spherical harmonics and other composite types.

### IMatrix / IMatrix\<T\>

Marker interface for matrix types.

### IProgressIndicator

Progress reporting interface.

- **Methods:** `UpdateProgress(float progress, string status, string details)`

### IScalableElement

Element that can be scaled.

### IPatternProducer

Generates patterns (used by PatternGenerator system).

### IPoolable

Object that can be returned to a pool: `OnReturnToPool()`.

### IStructMemberProxy

Proxy for accessing struct members dynamically.

---

## Enums

### Axis2D / Axis3D / Axis4D

`Axis2D`: `X`, `Y`
`Axis3D`: `X`, `Y`, `Z`
`Axis4D`: `X`, `Y`, `Z`, `W`

### Alignment

Layout alignment values: `TopLeft`, `TopCenter`, `TopRight`, `MiddleLeft`, `MiddleCenter`, `MiddleRight`, `BottomLeft`, `BottomCenter`, `BottomRight`.

### PanoramicProjection

`Equirectangular`

### StereoLayout

`None`, `Horizontal_LR`, `Vertical_LR`, `Horizontal_RL`, `Vertical_RL`

### DummyEnum

Placeholder enum used in generic contexts.

---

## Misc Types

### Number (struct)

Boxed numeric value that can hold int, long, or double.

- **NumberType (enum):** `Int`, `Long`, `Double`
- **Properties:** `InternalType`
- **Implicit casts from:** `int`, `long`, `double`
- **Explicit casts to:** `int`, `long`, `double`

### LocaleString (readonly struct)

Localization-aware string.

- **Fields:** `content` (string), `format` (string), `isLocaleKey` (bool), `isContinuous` (bool), `arguments` (Dictionary\<string, object\>)

### dummy / dummy\<T\> (readonly struct)

Zero-size placeholder types. Implement `IEquatable`, `IConvertible`, `IComparable`. Used as generic type parameters when no actual data is needed.

### RefVal\<T\>

Reference wrapper for a value type. Boxes a struct on the heap.

### Result / Result\<T\>

Operation result container with success/failure state and optional error message.

### SphericalHarmonicsL1\<T\> through L4\<T\>

Spherical harmonics coefficient storage at levels 1-4.

- **L1:** 4 coefficients
- **L2:** 9 coefficients
- **L3:** 16 coefficients
- **L4:** 25 coefficients
- **Implements:** `ISphericalHarmonics<T>`, `IEncodable`, `IAddable`, `ISubtractable`, `IScalable`, `ILerpable`

### SphericalHarmonicsHelper (static)

Utilities for spherical harmonics: evaluation, projection, rotation.

### StringNode types (IStringNode, StringNodeElement, StringNodeList, StringNodeMap)

Simple tree structure for string-based data (used in serialization contexts).

- **StringNodeElement:** wraps a string
- **StringNodeList:** ordered list of IStringNode
- **StringNodeMap:** key-value map of IStringNode

### PatternGenerator / PatternGroup / PatternSequence / PatternRandom / PatternForcedLength / PatternMaxLength / PatternLengthSplicer

Pattern generation system. Produces sequences via `IPatternProducer` interface.

### AbstractLinePainter

Base for line/stroke painting utilities.

### BatchQuery\<Query, Result\>

Abstract batched query processor. Groups queries and processes them together.

### LockCounter

Tracks lock/unlock count for reentrant-style locking.

### StaticConstructor\<T\>

Forces static constructor execution for type T.

### RecyclableMemoryStream / RecyclableMemoryStreamManager

Memory-efficient stream implementation that reuses buffers. Reduces GC pressure.

### StructFieldMissingException : Exception

Thrown when a required struct field is not found during reflection-based access.
