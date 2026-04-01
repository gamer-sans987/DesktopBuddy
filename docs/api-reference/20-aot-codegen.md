# FrooxEngine AOT / Codegen Reference

Types related to Ahead-Of-Time compilation and generic type casting infrastructure. These are mostly auto-generated and exist to prevent IL2CPP stripping on AOT platforms.

---

## Caster\<I, O\>

Static class providing fast, AOT-safe generic type casting between any two supported value types.

### Key Members
- `CastDelegate<A, B>` -- Delegate `B CastDelegate<A, B>(A input)`
- `Cast(I input) -> O` -- Executes the stored cast delegate

### Static Constructor
Populates `_cast` with an appropriate conversion lambda based on runtime type names. Covers:
- All primitive numeric types (bool, byte, sbyte, char, short, ushort, int, uint, long, ulong, float, double, decimal)
- All Elements.Core math types (float2/3/4, floatQ, float2x2/3x3/4x4, double2/3/4, doubleQ, double2x2/3x3/4x4, int2/3/4, uint2/3/4, long2/3/4, ulong2/3/4, color, color32)
- Falls back to `IsCastableTo` reflection check for unknown types

---

## CoderAOT

### CoderAOT\<T\> (generic)
Calls `Coder<T>.Dummy()` to force AOT compilation of the `Coder<T>` specialization.

### CoderNullableAOT\<T\> (generic, struct-constrained)
Calls `CoderNullable<T>.Dummy()` for nullable value type AOT support.

### CoderAOT (non-generic)
Massive static class with a `Dummy()` method (~25,700 lines) that explicitly instantiates every generic component/driver/variable type with every supported value type.

#### Purpose
Ensures all generic type specializations used anywhere in FrooxEngine are preserved during IL2CPP/AOT compilation, preventing runtime `MissingMethodException` on platforms without JIT (Android, iOS).

#### Types Instantiated Per Value Type
Includes ~80+ generic FrooxEngine component types such as:
`ActiveUserCloudField<T>`, `CloudValueVariable<T>`, `ButtonValueSet<T>`, `DynamicValueVariable<T>`, `ValueField<T>`, `ValueDriver<T>`, `ValueCopy<T>`, `Tween<T>`, `SmoothValue<T>`, `ValueStream<T>`, and many more.

#### Value Types Covered
`int`, `uint`, `long`, `ulong`, `byte`, `sbyte`, `short`, `ushort`, `float`, `double`, `decimal`, `char`, `bool`, `string`, `float2/3/4`, `floatQ`, `float2x2/3x3/4x4`, `double2/3/4`, `doubleQ`, `double2x2/3x3/4x4`, `int2/3/4`, `uint2/3/4`, `long2/3/4`, `ulong2/3/4`, `color`, `color32`, `Uri`, `Type`, `NetPointer`, `DateTime`, `TimeSpan`, `RefID`, `Rect`, various `IAssetProvider<>` types, and many enum types.

---

## Field Adapters (Legacy)

| Adapter | Purpose |
|---|---|
| `LegacyLinearFloatColorFieldAdapter` | Converts float using inverse 2.2 gamma |
| `LegacyColorAsLinearAdapter` | Converts colorX from sRGB to linear |
| `LegacyColorToLinearReinterpret_sRGB_Adapter` | Converts color to linear, reinterprets as sRGB |
| `AdjustHDRInverseGamma22` | Adjusts HDR color values exceeding 1.0 |
