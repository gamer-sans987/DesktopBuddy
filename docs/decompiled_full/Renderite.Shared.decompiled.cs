using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cloudtoid.Interprocess;
using Elements.Data;
using EnumsNET;
using Microsoft.CodeAnalysis;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: ComVisible(false)]
[assembly: Guid("d355230c-9f38-4bdb-959f-618159dcab3d")]
[assembly: DataModelAssembly(DataModelAssemblyType.Core)]
[assembly: TargetFramework(".NETStandard,Version=v2.0", FrameworkDisplayName = ".NET Standard 2.0")]
[assembly: AssemblyCompany("Renderite.Shared")]
[assembly: AssemblyConfiguration("Release")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0+12b4cfcfc837805a57a4a75a11ff2017f926f49b")]
[assembly: AssemblyProduct("Renderite.Shared")]
[assembly: AssemblyTitle("Renderite.Shared")]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
[assembly: AssemblyVersion("1.0.0.0")]
[module: UnverifiableCode]
[module: System.Runtime.CompilerServices.RefSafetyRules(11)]
namespace Microsoft.CodeAnalysis
{
	[CompilerGenerated]
	[Microsoft.CodeAnalysis.Embedded]
	internal sealed class EmbeddedAttribute : Attribute
	{
	}
}
namespace System.Runtime.CompilerServices
{
	[CompilerGenerated]
	[Microsoft.CodeAnalysis.Embedded]
	internal sealed class IsReadOnlyAttribute : Attribute
	{
	}
	[CompilerGenerated]
	[Microsoft.CodeAnalysis.Embedded]
	internal sealed class IsUnmanagedAttribute : Attribute
	{
	}
	[CompilerGenerated]
	[Microsoft.CodeAnalysis.Embedded]
	internal sealed class IsByRefLikeAttribute : Attribute
	{
	}
	[CompilerGenerated]
	[Microsoft.CodeAnalysis.Embedded]
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Event | AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter, AllowMultiple = false, Inherited = false)]
	internal sealed class NullableAttribute : Attribute
	{
		public readonly byte[] NullableFlags;

		public NullableAttribute(byte P_0)
		{
			NullableFlags = new byte[1] { P_0 };
		}

		public NullableAttribute(byte[] P_0)
		{
			NullableFlags = P_0;
		}
	}
	[CompilerGenerated]
	[Microsoft.CodeAnalysis.Embedded]
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Interface | AttributeTargets.Delegate, AllowMultiple = false, Inherited = false)]
	internal sealed class NullableContextAttribute : Attribute
	{
		public readonly byte Flag;

		public NullableContextAttribute(byte P_0)
		{
			Flag = P_0;
		}
	}
	[CompilerGenerated]
	[Microsoft.CodeAnalysis.Embedded]
	[AttributeUsage(AttributeTargets.Module, AllowMultiple = false, Inherited = false)]
	internal sealed class RefSafetyRulesAttribute : Attribute
	{
		public readonly int Version;

		public RefSafetyRulesAttribute(int P_0)
		{
			Version = P_0;
		}
	}
}
namespace Renderite.Shared
{
	public class ArrayBackingMemoryBuffer : BackingMemoryBuffer
	{
		public byte[] Array { get; private set; }

		public override int SizeBytes => Array.Length;

		public override Span<byte> RawData => Array.AsSpan();

		public override Memory<byte> Memory => Array.AsMemory();

		public ArrayBackingMemoryBuffer(byte[] array)
		{
			Array = array;
		}

		protected override void ActuallyDispose()
		{
			Array = null;
		}
	}
	[DataModelType]
	[OldTypeName("Elements.Core.ColorProfile", "Elements.Core")]
	public enum ColorProfile
	{
		Linear,
		sRGB,
		sRGBAlpha
	}
	public static class GaussianCloudHelper
	{
		public const int CHUNK_ELEMENT_COUNT = 256;

		public const int CHUNK_SIZE = 64;
	}
	[DataModelType]
	[OldTypeName("Elements.Assets.GaussianVectorFormat", "Elements.Assets")]
	public enum GaussianVectorFormat
	{
		Float32,
		Norm16,
		Norm11,
		Norm6
	}
	[DataModelType]
	[OldTypeName("Elements.Assets.GaussianRotationFormat", "Elements.Assets")]
	public enum GaussianRotationFormat
	{
		PackedNorm10
	}
	[DataModelType]
	[OldTypeName("Elements.Assets.GaussianColorFormat", "Elements.Assets")]
	public enum GaussianColorFormat
	{
		Float32x4,
		Float16x4,
		Norm8x4,
		BC7
	}
	[DataModelType]
	[OldTypeName("Elements.Assets.GaussianSHFormat", "Elements.Assets")]
	public enum GaussianSHFormat
	{
		Float16,
		Norm11,
		Norm6,
		Cluster64k,
		Cluster32k,
		Cluster16k,
		Cluster8k,
		Cluster4k
	}
	[StructLayout(LayoutKind.Explicit, Size = 16)]
	public readonly struct BlendshapeBufferDescriptor : IEquatable<BlendshapeBufferDescriptor>
	{
		[FieldOffset(0)]
		public readonly int blendshapeIndex;

		[FieldOffset(4)]
		public readonly int frameIndex;

		[FieldOffset(8)]
		public readonly float frameWeight;

		[FieldOffset(12)]
		public readonly BlendshapeDataFlags dataFlags;

		public BlendshapeBufferDescriptor(BlendshapeDataFlags dataFlags, int blendshapeIndex, int frameIndex, float frameWeight)
		{
			this.dataFlags = dataFlags;
			this.blendshapeIndex = blendshapeIndex;
			this.frameIndex = frameIndex;
			this.frameWeight = frameWeight;
		}

		public bool Equals(BlendshapeBufferDescriptor other)
		{
			if (dataFlags == other.dataFlags && blendshapeIndex == other.blendshapeIndex && frameIndex == other.frameIndex)
			{
				return frameWeight == other.frameWeight;
			}
			return false;
		}

		public static bool operator ==(BlendshapeBufferDescriptor left, BlendshapeBufferDescriptor right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(BlendshapeBufferDescriptor left, BlendshapeBufferDescriptor right)
		{
			return !(left == right);
		}

		public override string ToString()
		{
			return $"Flags: {dataFlags}, BlendshapeIndex: {blendshapeIndex}, FrameIndex: {frameIndex}, Weight: {frameWeight}";
		}
	}
	[StructLayout(LayoutKind.Explicit, Size = 8)]
	public struct BoneWeight : IEquatable<BoneWeight>
	{
		[FieldOffset(0)]
		public float weight;

		[FieldOffset(4)]
		public int boneIndex;

		public BoneWeight(float weight, int boneIndex)
		{
			this.weight = weight;
			this.boneIndex = boneIndex;
		}

		public bool Equals(BoneWeight other)
		{
			if (weight == other.weight)
			{
				return boneIndex == other.boneIndex;
			}
			return false;
		}

		public override bool Equals(object obj)
		{
			if (obj is BoneWeight other)
			{
				return Equals(other);
			}
			return false;
		}

		public override int GetHashCode()
		{
			return (2127128854 * -1521134295 + weight.GetHashCode()) * -1521134295 + boneIndex.GetHashCode();
		}

		public static bool operator ==(BoneWeight left, BoneWeight right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(BoneWeight left, BoneWeight right)
		{
			return !(left == right);
		}
	}
	[Flags]
	public enum BlendshapeDataFlags
	{
		NONE = 0,
		Positions = 1,
		Normals = 2,
		Tangets = 4
	}
	public enum IndexBufferFormat
	{
		UInt16,
		UInt32
	}
	public class MeshBuffer
	{
		public const long MAX_BUFFER_SIZE = 2147483648L;

		public const int MAX_UV_CHANNEL_COUNT = 8;

		public int VertexCount;

		public int BoneWeightCount;

		public int BoneCount;

		public IndexBufferFormat IndexBufferFormat;

		private List<int> vertexAttributeOffsets = new List<int>();

		public int VertexAttributeCount => VertexAttributes?.Count ?? 0;

		public int SubmeshCount => Submeshes?.Count ?? 0;

		public List<VertexAttributeDescriptor> VertexAttributes { get; private set; }

		public List<SubmeshBufferDescriptor> Submeshes { get; private set; }

		public int BlendshapeBufferCount => BlendshapeBuffers?.Count ?? 0;

		public List<BlendshapeBufferDescriptor> BlendshapeBuffers { get; private set; }

		public IBackingMemoryBuffer Data { get; set; }

		public Span<byte> RawBuffer
		{
			get
			{
				if (Data != null)
				{
					return Data.RawData;
				}
				return Span<byte>.Empty;
			}
		}

		public int VertexStride { get; private set; }

		public int IndexBufferStart { get; private set; }

		public int IndexCount { get; private set; }

		public int IndexBufferLength { get; private set; }

		public int BoneCountsBufferStart { get; private set; }

		public int BoneCountsBufferLength { get; private set; }

		public int BoneWeightsBufferStart { get; private set; }

		public int BoneWeightsBufferLength { get; private set; }

		public int BindPosesBufferStart { get; private set; }

		public int BindPosesBufferLength { get; private set; }

		public int BlendshapeDataStart { get; private set; }

		public int TotalBufferLength { get; private set; }

		public MeshBuffer()
		{
			VertexAttributes = new List<VertexAttributeDescriptor>();
			Submeshes = new List<SubmeshBufferDescriptor>();
			BlendshapeBuffers = new List<BlendshapeBufferDescriptor>();
		}

		public MeshBuffer(MeshUploadData data)
		{
			VertexAttributes = data.vertexAttributes;
			Submeshes = data.submeshes;
			BlendshapeBuffers = data.blendshapeBuffers;
			VertexCount = data.vertexCount;
			BoneWeightCount = data.boneWeightCount;
			BoneCount = data.boneCount;
			IndexBufferFormat = data.indexBufferFormat;
			ComputeBufferLayout();
		}

		public void FillMeshBufferDefinition(MeshUploadData data)
		{
			data.vertexAttributes = VertexAttributes;
			data.submeshes = Submeshes;
			data.blendshapeBuffers = BlendshapeBuffers;
			data.vertexCount = VertexCount;
			data.boneWeightCount = BoneWeightCount;
			data.boneCount = BoneCount;
			data.indexBufferFormat = IndexBufferFormat;
		}

		public Span<byte> GetRawVertexBufferData()
		{
			return RawBuffer.Slice(0, IndexBufferStart);
		}

		public Span<byte> GetRawIndexBufferData()
		{
			return RawBuffer.Slice(IndexBufferStart, IndexBufferLength);
		}

		public Span<uint> GetIndexBufferUInt32()
		{
			if (IndexBufferFormat != IndexBufferFormat.UInt32)
			{
				throw new InvalidOperationException("Index buffer format is not UInt32.");
			}
			return MemoryMarshal.Cast<byte, uint>(RawBuffer.Slice(IndexBufferStart, IndexBufferLength));
		}

		public Span<ushort> GetIndexBufferUInt16()
		{
			if (IndexBufferFormat != IndexBufferFormat.UInt16)
			{
				throw new InvalidOperationException("Index buffer format is not UInt16.");
			}
			return MemoryMarshal.Cast<byte, ushort>(RawBuffer.Slice(IndexBufferStart, IndexBufferLength));
		}

		public Span<byte> GetBoneCountsBuffer()
		{
			return RawBuffer.Slice(BoneCountsBufferStart, BoneCountsBufferLength);
		}

		public Span<BoneWeight> GetBoneWeightsBuffer()
		{
			return MemoryMarshal.Cast<byte, BoneWeight>(RawBuffer.Slice(BoneWeightsBufferStart, BoneWeightsBufferLength));
		}

		public Span<T> GetBindPosesBuffer<T>() where T : unmanaged
		{
			return MemoryMarshal.Cast<byte, T>(RawBuffer.Slice(BindPosesBufferStart, BindPosesBufferLength));
		}

		public Span<T> GetBlendshapeBuffer<T>() where T : unmanaged
		{
			return MemoryMarshal.Cast<byte, T>(RawBuffer.Slice(BlendshapeDataStart));
		}

		public int VertexAttributeOffset(int attributeIndex)
		{
			return vertexAttributeOffsets[attributeIndex];
		}

		public void SetVertexAttribute<T>(int vertexIndex, int attributeIndex, T value) where T : unmanaged
		{
			SetVertexAttribute(RawBuffer, vertexIndex, attributeIndex, value);
		}

		public void SetVertexAttribute<T>(Span<byte> rawData, int vertexIndex, int attributeIndex, T value) where T : unmanaged
		{
			SetVertexAttributeAtOffset(rawData, vertexIndex, VertexAttributeOffset(attributeIndex), value);
		}

		public void SetVertexAttributeAtOffset<T>(Span<byte> rawData, int vertexIndex, int attributeOffset, T value) where T : unmanaged
		{
			rawData = rawData.Slice(vertexIndex * VertexStride + attributeOffset);
			MemoryMarshal.Write(rawData, ref value);
		}

		public void WriteVertexAttributeAtOffset(Span<byte> rawData, int vertexIndex, int attributeOffset, Span<byte> value)
		{
			rawData = rawData.Slice(vertexIndex * VertexStride + attributeOffset);
			value.CopyTo(rawData);
		}

		public unsafe void FillVertexAttributesRaw<T>(Span<byte> rawData, Span<T> values, int attributeOffset) where T : unmanaged
		{
			int vertexStride = VertexStride;
			Span<byte> span = MemoryMarshal.Cast<T, byte>(values);
			int num = sizeof(T);
			fixed (byte* ptr = span)
			{
				fixed (byte* ptr2 = rawData)
				{
					byte* ptr3 = ptr2 + attributeOffset;
					for (int i = 0; i < span.Length; i += num)
					{
						Buffer.MemoryCopy(ptr + i, ptr3, num, num);
						ptr3 += vertexStride;
					}
				}
			}
		}

		public unsafe void ComputeBufferLayout()
		{
			long num = 0L;
			VertexStride = ComputeVertexStride();
			num += VertexStride * VertexCount;
			IndexBufferStart = (int)num;
			IndexCount = ComputeIndexCount();
			IndexBufferLength = IndexCount;
			switch (IndexBufferFormat)
			{
			case IndexBufferFormat.UInt16:
				IndexBufferLength *= 2;
				break;
			case IndexBufferFormat.UInt32:
				IndexBufferLength *= 4;
				break;
			default:
				throw new NotSupportedException("Unsupported index buffer format: " + IndexBufferFormat);
			}
			num += IndexBufferLength;
			BoneCountsBufferStart = (int)num;
			BoneCountsBufferLength = VertexCount;
			num += BoneCountsBufferLength;
			BoneWeightsBufferStart = (int)num;
			BoneWeightsBufferLength = BoneWeightCount * sizeof(BoneWeight);
			num += BoneWeightsBufferLength;
			BindPosesBufferStart = (int)num;
			BindPosesBufferLength = BoneCount * sizeof(RenderMatrix4x4);
			num += BindPosesBufferLength;
			BlendshapeDataStart = (int)num;
			if (BlendshapeBuffers != null)
			{
				foreach (BlendshapeBufferDescriptor blendshapeBuffer in BlendshapeBuffers)
				{
					if (blendshapeBuffer.dataFlags.HasFlag(BlendshapeDataFlags.Positions))
					{
						num += 12 * VertexCount;
					}
					if (blendshapeBuffer.dataFlags.HasFlag(BlendshapeDataFlags.Normals))
					{
						num += 12 * VertexCount;
					}
					if (blendshapeBuffer.dataFlags.HasFlag(BlendshapeDataFlags.Tangets))
					{
						num += 12 * VertexCount;
					}
				}
			}
			if (num >= 2147483648u)
			{
				throw new Exception("Mesh buffer size exceeds maximum allowed size of 2 GB.");
			}
			TotalBufferLength = (int)num;
		}

		private int ComputeVertexStride()
		{
			vertexAttributeOffsets.Clear();
			int num = 0;
			if (VertexAttributes != null)
			{
				foreach (VertexAttributeDescriptor vertexAttribute in VertexAttributes)
				{
					vertexAttributeOffsets.Add(num);
					num += vertexAttribute.Size;
				}
			}
			return num;
		}

		private int ComputeIndexCount()
		{
			int num = 0;
			if (Submeshes != null)
			{
				foreach (SubmeshBufferDescriptor submesh in Submeshes)
				{
					num = Math.Max(num, submesh.EndIndex);
				}
			}
			return num;
		}

		public override string ToString()
		{
			return $"MeshBuffer. Verts: {VertexCount}, IndicieCount: {IndexCount} (Format: {IndexBufferFormat}), SubmeshCount: {SubmeshCount}, " + string.Format("Bones: {0}\nVertexLayout: {1}\n", BoneCount, string.Join("\n", VertexAttributes?.Select((VertexAttributeDescriptor a) => "- " + a) ?? Array.Empty<string>())) + "SubmeshLayout: " + string.Join("\n", Submeshes?.Select(delegate(SubmeshBufferDescriptor s)
			{
				SubmeshBufferDescriptor submeshBufferDescriptor = s;
				return "- " + submeshBufferDescriptor.ToString();
			}) ?? Array.Empty<string>());
		}
	}
	[StructLayout(LayoutKind.Explicit, Size = 4)]
	public struct MeshUploadHint
	{
		[Flags]
		public enum Flag
		{
			VertexLayout = 1,
			SubmeshLayout = 2,
			Geometry = 4,
			Positions = 8,
			Normals = 0x10,
			Tangents = 0x20,
			Colors = 0x40,
			UV0s = 0x80,
			UV1s = 0x100,
			UV2s = 0x200,
			UV3s = 0x400,
			UV4s = 0x800,
			UV5s = 0x1000,
			UV6s = 0x2000,
			UV7s = 0x4000,
			BindPoses = 0x8000,
			BoneWeights = 0x10000,
			Blendshapes = 0x20000,
			Dynamic = 0x40000,
			Readable = 0x80000,
			Debug = 0x100000
		}

		[FieldOffset(0)]
		private Flag _flags;

		public Flag Flags => _flags;

		public bool AnyVertexStreams
		{
			get
			{
				if (!this[Flag.Positions] && !this[Flag.Normals] && !this[Flag.Tangents] && !this[Flag.Colors] && !this[Flag.UV0s] && !this[Flag.UV1s] && !this[Flag.UV2s] && !this[Flag.UV3s] && !this[Flag.UV4s] && !this[Flag.UV5s] && !this[Flag.UV6s])
				{
					return this[Flag.UV7s];
				}
				return true;
			}
		}

		public bool this[Flag channel]
		{
			get
			{
				return _flags.HasAnyFlags(channel);
			}
			set
			{
				if (value)
				{
					_flags |= channel;
				}
				else
				{
					_flags &= ~channel;
				}
			}
		}

		public static Flag GetUVFlag(int uvChannel)
		{
			return uvChannel switch
			{
				0 => Flag.UV0s, 
				1 => Flag.UV1s, 
				2 => Flag.UV2s, 
				3 => Flag.UV3s, 
				4 => Flag.UV4s, 
				5 => Flag.UV5s, 
				6 => Flag.UV6s, 
				7 => Flag.UV7s, 
				_ => throw new ArgumentOutOfRangeException("uvChannel", "Invalid UV channel: " + uvChannel), 
			};
		}

		public MeshUploadHint(Flag flags)
		{
			_flags = flags;
		}

		public bool GetUVChannel(int uv)
		{
			return uv switch
			{
				0 => this[Flag.UV0s], 
				1 => this[Flag.UV1s], 
				2 => this[Flag.UV2s], 
				3 => this[Flag.UV3s], 
				4 => this[Flag.UV4s], 
				5 => this[Flag.UV5s], 
				6 => this[Flag.UV6s], 
				7 => this[Flag.UV7s], 
				_ => throw new Exception("Invalid UV channel: " + uv), 
			};
		}

		public void ResetAll()
		{
			_flags = (Flag)0;
		}

		public void SetAll(bool debug = false)
		{
			_flags = (Flag)(-1);
			this[Flag.Debug] = debug;
		}

		public override string ToString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			foreach (Flag value in Enums.GetValues<Flag>())
			{
				stringBuilder.Append($"{value}: {this[value]}, ");
			}
			stringBuilder.Length -= 2;
			return stringBuilder.ToString();
		}
	}
	[StructLayout(LayoutKind.Explicit, Size = 36)]
	public struct SubmeshBufferDescriptor : IEquatable<SubmeshBufferDescriptor>
	{
		[FieldOffset(0)]
		public SubmeshTopology topology;

		[FieldOffset(4)]
		public int indexStart;

		[FieldOffset(8)]
		public int indexCount;

		[FieldOffset(12)]
		public RenderBoundingBox bounds;

		public int EndIndex => indexStart + indexCount;

		public SubmeshBufferDescriptor(SubmeshTopology topology, int indexStart, int indexCount, RenderBoundingBox bounds)
		{
			this.topology = topology;
			this.indexStart = indexStart;
			this.indexCount = indexCount;
			this.bounds = bounds;
		}

		public bool Equals(SubmeshBufferDescriptor other)
		{
			if (topology == other.topology && indexStart == other.indexStart && indexCount == other.indexCount)
			{
				return bounds.Equals(other.bounds);
			}
			return false;
		}

		public static bool operator ==(SubmeshBufferDescriptor left, SubmeshBufferDescriptor right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(SubmeshBufferDescriptor left, SubmeshBufferDescriptor right)
		{
			return !(left == right);
		}

		public override string ToString()
		{
			return $"Topology: {topology}. Index Start: {indexStart}, Count: {indexCount},  EndIndex: {EndIndex}, Bounds: {bounds}";
		}
	}
	public enum SubmeshTopology
	{
		Points,
		Triangles
	}
	[StructLayout(LayoutKind.Explicit, Size = 8)]
	public readonly struct VertexAttributeDescriptor : IEquatable<VertexAttributeDescriptor>
	{
		[FieldOffset(0)]
		public readonly VertexAttributeType attribute;

		[FieldOffset(2)]
		public readonly VertexAttributeFormat format;

		[FieldOffset(4)]
		public readonly int dimensions;

		public int Size => format.GetSize() * dimensions;

		public VertexAttributeDescriptor(VertexAttributeType attribute, VertexAttributeFormat format, int dimensions)
		{
			this.attribute = attribute;
			this.format = format;
			this.dimensions = dimensions;
		}

		public bool Equals(VertexAttributeDescriptor other)
		{
			if (attribute == other.attribute && format == other.format)
			{
				return dimensions == other.dimensions;
			}
			return false;
		}

		public static bool operator ==(VertexAttributeDescriptor left, VertexAttributeDescriptor right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(VertexAttributeDescriptor left, VertexAttributeDescriptor right)
		{
			return !(left == right);
		}

		public override string ToString()
		{
			return $"Attribute: {attribute}, Format: {format} x {dimensions}";
		}
	}
	public enum VertexAttributeFormat : short
	{
		Float32,
		Half16,
		UNorm8,
		UNorm16,
		SInt8,
		SInt16,
		SInt32,
		UInt8,
		UInt16,
		UInt32
	}
	public static class VertexAttributeFormatHelper
	{
		public static int GetSize(this VertexAttributeFormat format)
		{
			return format switch
			{
				VertexAttributeFormat.Float32 => 4, 
				VertexAttributeFormat.Half16 => 2, 
				VertexAttributeFormat.UNorm8 => 1, 
				VertexAttributeFormat.UNorm16 => 2, 
				VertexAttributeFormat.UInt8 => 1, 
				VertexAttributeFormat.UInt16 => 2, 
				VertexAttributeFormat.UInt32 => 4, 
				VertexAttributeFormat.SInt8 => 1, 
				VertexAttributeFormat.SInt16 => 2, 
				VertexAttributeFormat.SInt32 => 4, 
				_ => throw new ArgumentOutOfRangeException("format", format, null), 
			};
		}
	}
	public enum VertexAttributeType : short
	{
		Position,
		Normal,
		Tangent,
		Color,
		UV0,
		UV1,
		UV2,
		UV3,
		UV4,
		UV5,
		UV6,
		UV7,
		BoneWeights,
		BoneIndicies
	}
	[StructLayout(LayoutKind.Explicit, Size = 48)]
	public struct LightData
	{
		[FieldOffset(0)]
		public RenderVector3 point;

		[FieldOffset(12)]
		public RenderQuaternion orientation;

		[FieldOffset(28)]
		public RenderVector3 color;

		[FieldOffset(40)]
		public float intensity;

		[FieldOffset(44)]
		public float range;

		[FieldOffset(48)]
		public float angle;
	}
	[StructLayout(LayoutKind.Explicit, Size = 16)]
	public struct TrailOffset
	{
		[FieldOffset(0)]
		public int offset;

		[FieldOffset(4)]
		public int capacity;

		[FieldOffset(8)]
		public int start;

		[FieldOffset(12)]
		public int count;

		public int LastPointSubIndex => GetSubIndex(count - 1);

		public int LastPointIndex => GetIndex(count - 1);

		public int SecondToLastPointSubIndex => GetSubIndex(count - 2);

		public int SecondToLastPointIndex => GetIndex(count - 2);

		public bool HasFreeCapacity => count < capacity;

		public bool WrapsAround => start + count > capacity;

		public int EndIndex => offset + capacity;

		public void RemovePointFromBeginning()
		{
			start++;
			if (start == capacity)
			{
				start = 0;
			}
			count--;
		}

		public int GetSubIndex(int pos)
		{
			return (start + pos) % capacity;
		}

		public int GetIndex(int pos)
		{
			return offset + GetSubIndex(pos);
		}

		public override string ToString()
		{
			return $"Trail (Offset: {offset}, Capacity: {capacity}, Start: {start}, Count: {count})";
		}
	}
	[StructLayout(LayoutKind.Explicit, Size = 1)]
	public struct Texture3DUploadHint
	{
		[FieldOffset(0)]
		public bool readable;

		public Texture3DUploadHint(bool readable)
		{
			this.readable = readable;
		}
	}
	public enum TextureAssetType
	{
		Texture2D,
		Texture3D,
		Cubemap,
		RenderTexture,
		VideoTexture,
		Desktop
	}
	[DataModelType]
	[OldTypeName("Elements.Assets.TextureFilterMode", "Elements.Assets")]
	[OldTypeName("FrooxEngine.TextureFilterMode", "FrooxEngine")]
	public enum TextureFilterMode
	{
		Point,
		Bilinear,
		Trilinear,
		Anisotropic
	}
	[DataModelType]
	[OldTypeName("Elements.Assets.TextureFormat", "Elements.Assets")]
	public enum TextureFormat
	{
		Unknown = 0,
		Alpha8 = 1,
		R8 = 2,
		RGB24 = 16,
		ARGB32 = 17,
		RGBA32 = 18,
		BGRA32 = 19,
		RGB565 = 24,
		BGR565 = 25,
		RGBAHalf = 32,
		ARGBHalf = 33,
		RHalf = 34,
		RGHalf = 35,
		RGBAFloat = 48,
		ARGBFloat = 49,
		RFloat = 50,
		RGFloat = 51,
		BC1 = 64,
		BC2 = 65,
		BC3 = 66,
		BC4 = 67,
		BC5 = 68,
		BC6H = 69,
		BC7 = 70,
		ETC2_RGB = 96,
		ETC2_RGBA1 = 97,
		ETC2_RGBA8 = 98,
		ASTC_4x4 = 128,
		ASTC_5x5 = 129,
		ASTC_6x6 = 130,
		ASTC_8x8 = 131,
		ASTC_10x10 = 132,
		ASTC_12x12 = 133
	}
	public static class TextureFormatExtensions
	{
		public readonly struct AlternateFormat
		{
			public readonly TextureFormat format;

			public readonly bool upgradeOnly;

			public AlternateFormat(TextureFormat format, bool upgradeOnly)
			{
				this.format = format;
				this.upgradeOnly = upgradeOnly;
			}

			public static implicit operator AlternateFormat(TextureFormat format)
			{
				return new AlternateFormat(format, upgradeOnly: false);
			}
		}

		private static List<List<AlternateFormat>> _compatibleFormatGroups = new List<List<AlternateFormat>>
		{
			new List<AlternateFormat>
			{
				TextureFormat.ARGB32,
				TextureFormat.RGBA32,
				TextureFormat.BGRA32,
				new AlternateFormat(TextureFormat.RGB24, upgradeOnly: true)
			},
			new List<AlternateFormat>
			{
				TextureFormat.RGB565,
				TextureFormat.BGR565
			}
		};

		public static bool SupportsRead(this TextureFormat format)
		{
			if (format == TextureFormat.Unknown)
			{
				return false;
			}
			if (format.IsBlockCompressed())
			{
				return false;
			}
			return true;
		}

		public static bool SupportsWrite(this TextureFormat format)
		{
			if (format == TextureFormat.Unknown)
			{
				return false;
			}
			if (format.IsBlockCompressed())
			{
				return false;
			}
			return true;
		}

		public static bool IsBlockCompressed(this TextureFormat format)
		{
			if ((uint)(format - 64) <= 6u || (uint)(format - 96) <= 2u || (uint)(format - 128) <= 5u)
			{
				return true;
			}
			return false;
		}

		public static RenderVector2i BlockSize(this TextureFormat format)
		{
			switch (format)
			{
			case TextureFormat.BC1:
			case TextureFormat.BC2:
			case TextureFormat.BC3:
			case TextureFormat.BC4:
			case TextureFormat.BC5:
			case TextureFormat.BC6H:
			case TextureFormat.BC7:
			case TextureFormat.ETC2_RGB:
			case TextureFormat.ETC2_RGBA1:
			case TextureFormat.ETC2_RGBA8:
			case TextureFormat.ASTC_4x4:
				return new RenderVector2i(4);
			case TextureFormat.ASTC_5x5:
				return new RenderVector2i(5);
			case TextureFormat.ASTC_6x6:
				return new RenderVector2i(6);
			case TextureFormat.ASTC_8x8:
				return new RenderVector2i(8);
			case TextureFormat.ASTC_10x10:
				return new RenderVector2i(10);
			case TextureFormat.ASTC_12x12:
				return new RenderVector2i(12);
			default:
				return new RenderVector2i(1);
			}
		}

		public static RenderVector3i BlockSize3D(this TextureFormat format)
		{
			RenderVector2i renderVector2i = format.BlockSize();
			return new RenderVector3i(renderVector2i.x, renderVector2i.y, 1);
		}

		public static bool IsHDR(this TextureFormat format)
		{
			switch (format)
			{
			case TextureFormat.RGBAHalf:
			case TextureFormat.ARGBHalf:
			case TextureFormat.RHalf:
			case TextureFormat.RGHalf:
			case TextureFormat.RGBAFloat:
			case TextureFormat.ARGBFloat:
			case TextureFormat.RFloat:
			case TextureFormat.RGFloat:
			case TextureFormat.BC6H:
				return true;
			case TextureFormat.Alpha8:
			case TextureFormat.R8:
			case TextureFormat.RGB24:
			case TextureFormat.ARGB32:
			case TextureFormat.RGBA32:
			case TextureFormat.BGRA32:
			case TextureFormat.BC1:
			case TextureFormat.BC2:
			case TextureFormat.BC3:
			case TextureFormat.BC4:
			case TextureFormat.BC5:
			case TextureFormat.BC7:
			case TextureFormat.ETC2_RGB:
			case TextureFormat.ETC2_RGBA1:
			case TextureFormat.ETC2_RGBA8:
			case TextureFormat.ASTC_4x4:
			case TextureFormat.ASTC_5x5:
			case TextureFormat.ASTC_6x6:
			case TextureFormat.ASTC_8x8:
			case TextureFormat.ASTC_10x10:
			case TextureFormat.ASTC_12x12:
				return false;
			case TextureFormat.Unknown:
				return false;
			default:
				throw new ArgumentException("Invalid texture format: " + format);
			}
		}

		public static double GetBitsPerPixel(this TextureFormat format)
		{
			switch (format)
			{
			case TextureFormat.Alpha8:
			case TextureFormat.R8:
				return 8.0;
			case TextureFormat.RGB24:
				return 24.0;
			case TextureFormat.RGB565:
			case TextureFormat.BGR565:
				return 16.0;
			case TextureFormat.ARGB32:
			case TextureFormat.RGBA32:
			case TextureFormat.BGRA32:
				return 32.0;
			case TextureFormat.RGBAHalf:
			case TextureFormat.ARGBHalf:
				return 64.0;
			case TextureFormat.RHalf:
				return 16.0;
			case TextureFormat.RGHalf:
				return 32.0;
			case TextureFormat.RGBAFloat:
			case TextureFormat.ARGBFloat:
				return 128.0;
			case TextureFormat.RGFloat:
				return 64.0;
			case TextureFormat.RFloat:
				return 32.0;
			case TextureFormat.BC1:
			case TextureFormat.BC4:
			case TextureFormat.ETC2_RGB:
			case TextureFormat.ETC2_RGBA1:
				return 4.0;
			case TextureFormat.BC2:
			case TextureFormat.BC3:
			case TextureFormat.BC5:
			case TextureFormat.BC6H:
			case TextureFormat.BC7:
			case TextureFormat.ETC2_RGBA8:
				return 8.0;
			case TextureFormat.ASTC_4x4:
			case TextureFormat.ASTC_5x5:
			case TextureFormat.ASTC_6x6:
			case TextureFormat.ASTC_8x8:
			case TextureFormat.ASTC_10x10:
			case TextureFormat.ASTC_12x12:
			{
				RenderVector2i renderVector2i = format.BlockSize();
				return 128.0 / (double)(renderVector2i.x * renderVector2i.y);
			}
			case TextureFormat.Unknown:
				return 0.0;
			default:
				throw new ArgumentException("Invalid texture format: " + format);
			}
		}

		public static int GetBytesPerPixel(this TextureFormat format)
		{
			switch (format)
			{
			case TextureFormat.Alpha8:
			case TextureFormat.R8:
				return 1;
			case TextureFormat.RGB565:
			case TextureFormat.BGR565:
				return 2;
			case TextureFormat.RGB24:
				return 3;
			case TextureFormat.ARGB32:
			case TextureFormat.RGBA32:
			case TextureFormat.BGRA32:
				return 4;
			case TextureFormat.RGBAHalf:
			case TextureFormat.ARGBHalf:
				return 8;
			case TextureFormat.RGBAFloat:
			case TextureFormat.ARGBFloat:
				return 16;
			case TextureFormat.RHalf:
				return 2;
			case TextureFormat.RGHalf:
				return 4;
			case TextureFormat.RFloat:
				return 4;
			case TextureFormat.RGFloat:
				return 8;
			case TextureFormat.BC1:
				throw new Exception("Bytes per pixel is less than 1, use GetBitsPerPixel");
			case TextureFormat.BC3:
			case TextureFormat.BC7:
			case TextureFormat.ASTC_4x4:
				return 1;
			default:
				throw new ArgumentException("Invalid texture format: " + format);
			}
		}

		public static int GetBytesPerChannel(this TextureFormat format)
		{
			switch (format)
			{
			case TextureFormat.Alpha8:
			case TextureFormat.R8:
			case TextureFormat.RGB24:
			case TextureFormat.ARGB32:
			case TextureFormat.RGBA32:
			case TextureFormat.BGRA32:
				return 1;
			case TextureFormat.RGBAHalf:
			case TextureFormat.ARGBHalf:
			case TextureFormat.RHalf:
			case TextureFormat.RGHalf:
				return 2;
			case TextureFormat.RGBAFloat:
			case TextureFormat.ARGBFloat:
			case TextureFormat.RFloat:
			case TextureFormat.RGFloat:
				return 4;
			default:
				throw new ArgumentException("Invalid texture format: " + format);
			}
		}

		public static int GetChannels(this TextureFormat format)
		{
			switch (format)
			{
			case TextureFormat.Alpha8:
			case TextureFormat.R8:
			case TextureFormat.RHalf:
			case TextureFormat.RFloat:
			case TextureFormat.BC4:
				return 1;
			case TextureFormat.RGHalf:
			case TextureFormat.RGFloat:
			case TextureFormat.BC5:
				return 2;
			case TextureFormat.RGB24:
			case TextureFormat.RGB565:
			case TextureFormat.BGR565:
			case TextureFormat.BC6H:
			case TextureFormat.ETC2_RGB:
				return 3;
			case TextureFormat.ARGB32:
			case TextureFormat.RGBA32:
			case TextureFormat.BGRA32:
			case TextureFormat.RGBAHalf:
			case TextureFormat.ARGBHalf:
			case TextureFormat.RGBAFloat:
			case TextureFormat.ARGBFloat:
			case TextureFormat.BC1:
			case TextureFormat.BC2:
			case TextureFormat.BC3:
			case TextureFormat.BC7:
			case TextureFormat.ETC2_RGBA1:
			case TextureFormat.ETC2_RGBA8:
			case TextureFormat.ASTC_4x4:
			case TextureFormat.ASTC_5x5:
			case TextureFormat.ASTC_6x6:
			case TextureFormat.ASTC_8x8:
			case TextureFormat.ASTC_10x10:
			case TextureFormat.ASTC_12x12:
				return 4;
			default:
				throw new ArgumentException("Invalid texture format: " + format);
			}
		}

		public static bool SupportsAlpha(this TextureFormat format)
		{
			switch (format)
			{
			case TextureFormat.Alpha8:
				return true;
			case TextureFormat.R8:
			case TextureFormat.RGB24:
			case TextureFormat.RGB565:
			case TextureFormat.BGR565:
			case TextureFormat.RHalf:
			case TextureFormat.RGHalf:
			case TextureFormat.RFloat:
			case TextureFormat.RGFloat:
				return false;
			case TextureFormat.ARGB32:
			case TextureFormat.RGBA32:
			case TextureFormat.BGRA32:
			case TextureFormat.RGBAHalf:
			case TextureFormat.ARGBHalf:
			case TextureFormat.RGBAFloat:
			case TextureFormat.ARGBFloat:
				return true;
			case TextureFormat.BC1:
			case TextureFormat.BC4:
			case TextureFormat.BC6H:
				return false;
			case TextureFormat.BC2:
			case TextureFormat.BC3:
			case TextureFormat.BC7:
			case TextureFormat.ASTC_4x4:
			case TextureFormat.ASTC_5x5:
			case TextureFormat.ASTC_6x6:
			case TextureFormat.ASTC_8x8:
			case TextureFormat.ASTC_10x10:
			case TextureFormat.ASTC_12x12:
				return true;
			case TextureFormat.ETC2_RGB:
				return false;
			case TextureFormat.ETC2_RGBA1:
			case TextureFormat.ETC2_RGBA8:
				return true;
			default:
				throw new ArgumentException("Invalid texture format: " + format);
			}
		}

		public static bool IsDisabled(this TextureFormat format)
		{
			if ((uint)(format - 128) <= 5u)
			{
				return true;
			}
			return false;
		}

		public static IReadOnlyList<AlternateFormat> GetCompatibleFormatGroup(this TextureFormat format)
		{
			foreach (List<AlternateFormat> compatibleFormatGroup in _compatibleFormatGroups)
			{
				if (compatibleFormatGroup.Any((AlternateFormat f) => f.format == format))
				{
					return compatibleFormatGroup;
				}
			}
			return null;
		}

		public static TextureFormat? FindCompatibleFormat(this TextureFormat format, Predicate<TextureFormat> filter)
		{
			IReadOnlyList<AlternateFormat> compatibleFormatGroup = format.GetCompatibleFormatGroup();
			if (compatibleFormatGroup == null)
			{
				return null;
			}
			foreach (AlternateFormat item in compatibleFormatGroup)
			{
				if (!item.upgradeOnly && filter(item.format))
				{
					return item.format;
				}
			}
			return null;
		}

		public static bool IsExpensiveToCompute(this TextureFormat f)
		{
			if ((uint)(f - 69) <= 1u)
			{
				return true;
			}
			return false;
		}
	}
	public enum TextureType
	{
		Texture2D,
		Cubemap,
		Texture3D
	}
	[StructLayout(LayoutKind.Explicit, Size = 20)]
	public struct TextureUploadHint
	{
		[FieldOffset(0)]
		public RenderIntRect regionData;

		[FieldOffset(16)]
		public bool readable;

		[FieldOffset(17)]
		public bool hasRegion;

		public bool IsEmptyRegion
		{
			get
			{
				if (!hasRegion)
				{
					return false;
				}
				if (regionData.width != 0)
				{
					return regionData.height == 0;
				}
				return true;
			}
		}

		public RenderIntRect? region
		{
			get
			{
				if (!hasRegion)
				{
					return null;
				}
				return regionData;
			}
			set
			{
				if (!value.HasValue)
				{
					hasRegion = false;
					regionData = default(RenderIntRect);
				}
				else
				{
					hasRegion = true;
					regionData = value.Value;
				}
			}
		}
	}
	[DataModelType]
	[OldTypeName("FrooxEngine.TextureWrapMode", "FrooxEngine")]
	public enum TextureWrapMode
	{
		Repeat,
		Clamp,
		Mirror,
		MirrorOnce
	}
	public class VideoTextureAudioWriter : IDisposable
	{
		private IPublisher _publisher;

		public VideoTextureAudioWriter(string queueName, int capacity)
		{
			QueueFactory queueFactory = new QueueFactory();
			QueueOptions options = new QueueOptions(queueName, capacity, deleteOnDispose: true);
			_publisher = queueFactory.CreatePublisher(options);
		}

		public bool Write(Span<float> samples)
		{
			Span<byte> span = MemoryMarshal.Cast<float, byte>(samples);
			return _publisher.TryEnqueue(span);
		}

		public void Dispose()
		{
			_publisher.Dispose();
			_publisher = null;
		}
	}
	public ref struct BitSpan
	{
		private const int BITS_IN_ELEMENT = 32;

		private Span<uint> data;

		public int Length => data.Length * 8;

		public bool this[int bitIndex]
		{
			get
			{
				int index = bitIndex / 32;
				bitIndex %= 32;
				return (data[index] & (uint)(1 << bitIndex)) != 0;
			}
			set
			{
				int index = bitIndex / 32;
				bitIndex %= 32;
				ref uint reference = ref data[index];
				uint num = (uint)(1 << bitIndex);
				if (value)
				{
					reference |= num;
				}
				else
				{
					reference &= ~num;
				}
			}
		}

		public static int ComputeMinimumBufferLength(int totalBits)
		{
			return (totalBits + 32 - 1) / 32;
		}

		public BitSpan(Span<uint> data)
		{
			this.data = data;
		}
	}
	public static class Helper
	{
		public const int EDITOR_PORT = 42512;

		public const string FOLDER_PATH = "Renderer";

		public const string PROCESS_NAME = "Renderite.Renderer";

		public const string QUEUE_NAME_ARGUMENT = "QueueName";

		public const string QUEUE_CAPACITY_ARGUMENT = "QueueCapacity";

		public const string PRIMARY_QUEUE = "Primary";

		public const string BACKGROUND_QUEUE = "Background";

		public static readonly bool IsWine;

		public static readonly string? WineVersion;

		static Helper()
		{
			try
			{
				WineVersion = wine_get_version();
				IsWine = true;
			}
			catch
			{
			}
		}

		public static string ComposeMemoryViewName(string prefix, int bufferId)
		{
			return $"{prefix}_{bufferId:X}";
		}

		[DllImport("ntdll.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		private static extern string wine_get_version();
	}
	public interface IBackingMemoryBuffer : IDisposable
	{
		int SizeBytes { get; }

		Span<byte> RawData { get; }

		Memory<byte> Memory { get; }

		bool IsDisposed { get; }

		bool TryLockUse();

		void Unlock();
	}
	public abstract class BackingMemoryBuffer : IBackingMemoryBuffer, IDisposable
	{
		private int _activeUses;

		private bool _dispose;

		public abstract int SizeBytes { get; }

		public abstract Span<byte> RawData { get; }

		public abstract Memory<byte> Memory { get; }

		public bool IsDisposed { get; private set; }

		public void Dispose()
		{
			IsDisposed = true;
			lock (this)
			{
				_dispose = true;
				if (_activeUses == 0)
				{
					ActuallyDispose();
				}
			}
		}

		public bool TryLockUse()
		{
			if (_dispose)
			{
				return false;
			}
			lock (this)
			{
				if (_dispose)
				{
					return false;
				}
				_activeUses++;
				return true;
			}
		}

		public void Unlock()
		{
			lock (this)
			{
				if (_activeUses == 0)
				{
					throw new InvalidOperationException("Trying to unlock buffer that has no active uses");
				}
				if (--_activeUses == 0 && _dispose)
				{
					ActuallyDispose();
				}
			}
		}

		protected abstract void ActuallyDispose();
	}
	[DataModelType]
	[OldTypeName("FrooxEngine.Key", "FrooxEngine")]
	public enum Key
	{
		None = 0,
		Backspace = 8,
		Tab = 9,
		Clear = 12,
		Return = 13,
		Pause = 19,
		Escape = 27,
		Space = 32,
		Exclaim = 33,
		DoubleQuote = 34,
		Hash = 35,
		Dollar = 36,
		Ampersand = 38,
		Quote = 39,
		LeftParenthesis = 40,
		RightParenthesis = 41,
		Asterisk = 42,
		Plus = 43,
		Comma = 44,
		Minus = 45,
		Period = 46,
		Slash = 47,
		Alpha0 = 48,
		Alpha1 = 49,
		Alpha2 = 50,
		Alpha3 = 51,
		Alpha4 = 52,
		Alpha5 = 53,
		Alpha6 = 54,
		Alpha7 = 55,
		Alpha8 = 56,
		Alpha9 = 57,
		Colon = 58,
		Semicolon = 59,
		Less = 60,
		Equals = 61,
		Greater = 62,
		Question = 63,
		At = 64,
		VerticalBar = 65,
		LeftBracket = 91,
		Backslash = 92,
		RightBracket = 93,
		Caret = 94,
		Underscore = 95,
		BackQuote = 96,
		A = 97,
		B = 98,
		C = 99,
		D = 100,
		E = 101,
		F = 102,
		G = 103,
		H = 104,
		I = 105,
		J = 106,
		K = 107,
		L = 108,
		M = 109,
		N = 110,
		O = 111,
		P = 112,
		Q = 113,
		R = 114,
		S = 115,
		T = 116,
		U = 117,
		V = 118,
		W = 119,
		X = 120,
		Y = 121,
		Z = 122,
		Percent = 123,
		Tilde = 124,
		LeftBrace = 125,
		RightBrace = 126,
		Delete = 127,
		Keypad0 = 256,
		Keypad1 = 257,
		Keypad2 = 258,
		Keypad3 = 259,
		Keypad4 = 260,
		Keypad5 = 261,
		Keypad6 = 262,
		Keypad7 = 263,
		Keypad8 = 264,
		Keypad9 = 265,
		KeypadPeriod = 266,
		KeypadDivide = 267,
		KeypadMultiply = 268,
		KeypadMinus = 269,
		KeypadPlus = 270,
		KeypadEnter = 271,
		KeypadEquals = 272,
		UpArrow = 273,
		DownArrow = 274,
		RightArrow = 275,
		LeftArrow = 276,
		Insert = 277,
		Home = 278,
		End = 279,
		PageUp = 280,
		PageDown = 281,
		F1 = 282,
		F2 = 283,
		F3 = 284,
		F4 = 285,
		F5 = 286,
		F6 = 287,
		F7 = 288,
		F8 = 289,
		F9 = 290,
		F10 = 291,
		F11 = 292,
		F12 = 293,
		F13 = 294,
		F14 = 295,
		F15 = 296,
		Numlock = 300,
		CapsLock = 301,
		ScrollLock = 302,
		RightShift = 303,
		LeftShift = 304,
		RightControl = 305,
		LeftControl = 306,
		RightAlt = 307,
		LeftAlt = 308,
		RightApple = 309,
		RightCommand = 309,
		LeftApple = 310,
		LeftCommand = 310,
		LeftWindows = 311,
		RightWindows = 312,
		AltGr = 313,
		Help = 315,
		Print = 316,
		SysReq = 317,
		Break = 318,
		Menu = 319,
		Shift = 512,
		Control = 513,
		Alt = 514,
		Windows = 515
	}
	public static class KeyHelper
	{
		public static bool IsModifier(this Key key)
		{
			if (!key.IsShift() && !key.IsAlt() && !key.IsWin())
			{
				return key.IsCtrl();
			}
			return true;
		}

		public static bool IsShift(this Key key)
		{
			if (key != Key.LeftShift && key != Key.RightShift)
			{
				return key == Key.Shift;
			}
			return true;
		}

		public static bool IsAlt(this Key key)
		{
			if (key != Key.LeftAlt && key != Key.RightAlt && key != Key.AltGr)
			{
				return key == Key.Alt;
			}
			return true;
		}

		public static bool IsWin(this Key key)
		{
			if (key != Key.LeftWindows && key != Key.RightWindows)
			{
				return key == Key.Windows;
			}
			return true;
		}

		public static bool IsCtrl(this Key key)
		{
			if (key != Key.LeftControl && key != Key.RightControl)
			{
				return key == Key.Control;
			}
			return true;
		}

		public static Key ToKey(this char ch, out bool shift)
		{
			shift = char.IsUpper(ch);
			if (shift)
			{
				ch = char.ToLower(ch);
			}
			if (ch >= 'a' && ch <= 'z')
			{
				return (Key)(97 + (ch - 97));
			}
			if (ch >= '0' && ch <= '9')
			{
				return (Key)(48 + (ch - 48));
			}
			return ch switch
			{
				'\t' => Key.Tab, 
				'\n' => Key.Return, 
				'\r' => Key.Return, 
				'\b' => Key.Backspace, 
				' ' => Key.Space, 
				'!' => Key.Exclaim, 
				'"' => Key.DoubleQuote, 
				'#' => Key.Hash, 
				'$' => Key.Dollar, 
				'&' => Key.Ampersand, 
				'\'' => Key.Quote, 
				'(' => Key.LeftParenthesis, 
				')' => Key.RightParenthesis, 
				'*' => Key.Asterisk, 
				'+' => Key.Plus, 
				',' => Key.Comma, 
				'-' => Key.Minus, 
				'.' => Key.Period, 
				'/' => Key.Slash, 
				':' => Key.Colon, 
				';' => Key.Semicolon, 
				'<' => Key.Less, 
				'=' => Key.Equals, 
				'>' => Key.Greater, 
				'?' => Key.Question, 
				'@' => Key.At, 
				'[' => Key.LeftBracket, 
				']' => Key.RightBracket, 
				'\\' => Key.Backslash, 
				'^' => Key.Caret, 
				'_' => Key.Underscore, 
				'`' => Key.BackQuote, 
				'%' => Key.Percent, 
				'~' => Key.Tilde, 
				'{' => Key.LeftBrace, 
				'}' => Key.RightBrace, 
				_ => Key.None, 
			};
		}
	}
	[DataModelType]
	[OldTypeName("Elements.Core.RectOrientation", "Elements.Core")]
	public enum RectOrientation
	{
		Default,
		Clockwise90,
		UpsideDown180,
		CounterClockwise90
	}
	public delegate void RenderCommandHandler(RendererCommand command, int messageSize);
	public class MessagingManager : IDisposable
	{
		public const int DEFAULT_CAPACITY = 8388608;

		public RenderCommandHandler CommandHandler;

		public Action<Exception> FailureHandler;

		public Action<string> WarningHandler;

		private IPublisher _publisher;

		private ISubscriber _subscriber;

		private object _lock = new object();

		private IMemoryPackerEntityPool _pool;

		private CancellationTokenSource cancellation;

		private Memory<byte> writerBuffer;

		private Memory<byte> receiverBuffer;

		private Thread receiverThread;

		public int ReceivedMessages { get; private set; }

		public int SentMessages { get; private set; }

		public MessagingManager(IMemoryPackerEntityPool pool)
		{
			_pool = pool;
		}

		public void Connect(string queueName, bool isAuthority, long capacity = 8388608L)
		{
			writerBuffer = new Memory<byte>(new byte[capacity]);
			receiverBuffer = new Memory<byte>(new byte[capacity]);
			QueueFactory queueFactory = new QueueFactory();
			QueueOptions options = new QueueOptions(queueName + (isAuthority ? "A" : "S"), capacity, isAuthority);
			QueueOptions options2 = new QueueOptions(queueName + (isAuthority ? "S" : "A"), capacity, isAuthority);
			_publisher = queueFactory.CreatePublisher(options);
			_subscriber = queueFactory.CreateSubscriber(options2);
			StartProcessing();
		}

		private void StartProcessing()
		{
			cancellation = new CancellationTokenSource();
			receiverThread = new Thread(ReceiverLogic);
			receiverThread.Priority = ThreadPriority.Highest;
			receiverThread.IsBackground = true;
			receiverThread.Start();
		}

		public void SendCommand(RendererCommand command)
		{
			if (command == null)
			{
				throw new ArgumentNullException("command");
			}
			try
			{
				if (_publisher == null)
				{
					throw new InvalidOperationException("Cannot send commands when publisher is gone!");
				}
				lock (_lock)
				{
					MemoryPacker packer = new MemoryPacker(writerBuffer.Span);
					command.Encode(ref packer);
					int num = packer.ComputeLength(writerBuffer.Span);
					if (num == 0)
					{
						throw new Exception($"Serializing command resulted in zero length. Command: {command}");
					}
					Span<byte> span = writerBuffer.Span.Slice(0, num);
					while (!_publisher.TryEnqueue(span))
					{
						WarningHandler("Queue doesn't have enough free capacity, stalling message writing!");
						Thread.Sleep(10);
					}
					SentMessages++;
				}
			}
			catch (Exception obj)
			{
				FailureHandler?.Invoke(obj);
				throw;
			}
		}

		public void StartKeepAlive(int intervalMilliseconds = 50, Action onSend = null)
		{
			Task.Run(async delegate
			{
				await KeepAliveHandler(intervalMilliseconds, onSend);
			});
		}

		private async Task KeepAliveHandler(int intervalMilliseconds, Action onSend = null)
		{
			while (!cancellation.IsCancellationRequested)
			{
				await Task.Delay(TimeSpan.FromMilliseconds(intervalMilliseconds)).ConfigureAwait(continueOnCapturedContext: false);
				SendCommand(new KeepAlive());
				onSend?.Invoke();
			}
		}

		private void ReceiverLogic()
		{
			try
			{
				while (!cancellation.IsCancellationRequested)
				{
					ReadOnlyMemory<byte> readOnlyMemory = _subscriber.Dequeue(receiverBuffer, cancellation.Token);
					ReceivedMessages++;
					if (cancellation.IsCancellationRequested)
					{
						break;
					}
					if (!readOnlyMemory.IsEmpty)
					{
						int length = readOnlyMemory.Length;
						MemoryUnpacker unpacker = new MemoryUnpacker(readOnlyMemory.Span, _pool);
						RendererCommand rendererCommand = null;
						try
						{
							rendererCommand = PolymorphicMemoryPackableEntity<RendererCommand>.Decode(ref unpacker);
							CommandHandler?.Invoke(rendererCommand, length);
						}
						catch (Exception innerException)
						{
							throw new Exception($"Failure processing message. Message length: {readOnlyMemory.Span.Length}. Remaining data: {unpacker.RemainingData}, Command: {rendererCommand}", innerException);
						}
					}
				}
			}
			catch (Exception obj)
			{
				FailureHandler?.Invoke(obj);
			}
			finally
			{
				_publisher.Dispose();
				_subscriber.Dispose();
				_publisher = null;
				_subscriber = null;
			}
		}

		public void Dispose()
		{
			cancellation?.Cancel();
		}
	}
	public interface IMemoryPackable
	{
		void Pack(ref MemoryPacker packer);

		void Unpack(ref MemoryUnpacker unpacker);
	}
	public interface IMemoryPackerEntityPool
	{
		T Borrow<T>() where T : class, IMemoryPackable, new();

		void Return<T>(T value) where T : class, IMemoryPackable, new();
	}
	public delegate void ListWriter<T>(ref MemoryPacker packer, List<T> list);
	public ref struct MemoryPacker
	{
		private Span<byte> buffer;

		public int ComputeLength(Span<byte> originalBuffer)
		{
			return originalBuffer.Length - buffer.Length;
		}

		public MemoryPacker(Span<byte> buffer)
		{
			this.buffer = buffer;
		}

		public unsafe Span<T> Access<T>(int count) where T : unmanaged
		{
			int start = count * sizeof(T);
			Span<T> result = MemoryMarshal.Cast<byte, T>(buffer).Slice(0, count);
			buffer = buffer.Slice(start);
			return result;
		}

		public void Write(string str)
		{
			if (str == null)
			{
				Write(-1);
				return;
			}
			ReadOnlySpan<char> readOnlySpan = str.AsSpan();
			Write(readOnlySpan.Length);
			Span<char> destination = Access<char>(readOnlySpan.Length);
			readOnlySpan.CopyTo(destination);
		}

		public void Write<T>(T? value) where T : unmanaged
		{
			if (!value.HasValue)
			{
				Write(value: false);
				return;
			}
			Write(value: true);
			Write(value.Value);
		}

		public unsafe void Write<T>(T value) where T : unmanaged
		{
			Unsafe.WriteUnaligned(ref buffer[0], value);
			buffer = buffer.Slice(sizeof(T));
		}

		public void Write(bool bit0, bool bit1, bool bit2 = false, bool bit3 = false, bool bit4 = false, bool bit5 = false, bool bit6 = false, bool bit7 = false)
		{
			Write((byte)((bit0 ? 1u : 0u) | ((bit1 ? 1u : 0u) << 1) | ((bit2 ? 1u : 0u) << 2) | ((bit3 ? 1u : 0u) << 3) | ((bit4 ? 1u : 0u) << 4) | ((bit5 ? 1u : 0u) << 5) | ((bit6 ? 1u : 0u) << 6) | ((bit7 ? 1u : 0u) << 7)));
		}

		public void WriteObject<T>(T? obj) where T : class, IMemoryPackable
		{
			if (obj == null)
			{
				Write(value: false);
				return;
			}
			Write(value: true);
			obj.Pack(ref this);
		}

		public void WriteNestedValueList<T>(List<List<T>> list) where T : unmanaged
		{
			WriteNestedList(list, delegate(ref MemoryPacker packer, List<T> sublist)
			{
				packer.WriteValueList(sublist);
			});
		}

		public void WriteNestedList<T>(List<List<T>> list, ListWriter<T> sublistWriter)
		{
			int num = list?.Count ?? 0;
			Write(num);
			for (int i = 0; i < num; i++)
			{
				sublistWriter(ref this, list[i]);
			}
		}

		public void WriteObjectList<T>(List<T> list) where T : IMemoryPackable
		{
			int num = list?.Count ?? 0;
			Write(num);
			for (int i = 0; i < num; i++)
			{
				list[i].Pack(ref this);
			}
		}

		public void WritePolymorphicList<T>(List<T> list) where T : PolymorphicMemoryPackableEntity<T>
		{
			int num = list?.Count ?? 0;
			Write(num);
			for (int i = 0; i < num; i++)
			{
				list[i].Encode(ref this);
			}
		}

		public void WriteValueList<T>(List<T> list) where T : unmanaged
		{
			WriteValueList<List<T>, T>(list);
		}

		public void WriteValueList<T>(HashSet<T> list) where T : unmanaged
		{
			WriteValueList<HashSet<T>, T>(list);
		}

		public void WriteValueList<C, T>(C list) where C : ICollection<T> where T : unmanaged
		{
			int value = list?.Count ?? 0;
			Write(value);
			if (list == null)
			{
				return;
			}
			foreach (T item in list)
			{
				Write(item);
			}
		}

		public void WriteStringList(List<string> list)
		{
			int num = list?.Count ?? 0;
			Write(num);
			for (int i = 0; i < num; i++)
			{
				Write(list[i]);
			}
		}
	}
	public delegate void ListReader<T>(ref MemoryUnpacker unpacker, ref List<T> list);
	public ref struct MemoryUnpacker
	{
		private ReadOnlySpan<byte> buffer;

		public IMemoryPackerEntityPool Pool { get; private set; }

		public int RemainingData => buffer.Length;

		public MemoryUnpacker(ReadOnlySpan<byte> buffer, IMemoryPackerEntityPool pool)
		{
			this.buffer = buffer;
			Pool = pool;
		}

		public unsafe ReadOnlySpan<T> Access<T>(int count) where T : unmanaged
		{
			int start = count * sizeof(T);
			ReadOnlySpan<T> result = MemoryMarshal.Cast<byte, T>(buffer).Slice(0, count);
			buffer = buffer.Slice(start);
			return result;
		}

		public void Read(ref string str)
		{
			str = ReadString();
		}

		public unsafe string ReadString()
		{
			int num = Read<int>();
			if (num < 0)
			{
				return null;
			}
			if (num == 0)
			{
				return "";
			}
			fixed (char* value = Access<char>(num))
			{
				return new string(value, 0, num);
			}
		}

		public T Read<T>() where T : unmanaged
		{
			return Access<T>(1)[0];
		}

		public void Read<T>(ref T? target) where T : unmanaged
		{
			if (Read<bool>())
			{
				target = Read<T>();
			}
			else
			{
				target = null;
			}
		}

		public void Read<T>(ref T target) where T : unmanaged
		{
			target = Access<T>(1)[0];
		}

		public void Read(out bool bit0, out bool bit1)
		{
			Read(out bit0, out bit1, out var _, out var _, out var _, out var _, out var _, out var _);
		}

		public void Read(out bool bit0, out bool bit1, out bool bit2)
		{
			Read(out bit0, out bit1, out bit2, out var _, out var _, out var _, out var _, out var _);
		}

		public void Read(out bool bit0, out bool bit1, out bool bit2, out bool bit3)
		{
			Read(out bit0, out bit1, out bit2, out bit3, out var _, out var _, out var _, out var _);
		}

		public void Read(out bool bit0, out bool bit1, out bool bit2, out bool bit3, out bool bit4)
		{
			Read(out bit0, out bit1, out bit2, out bit3, out bit4, out var _, out var _, out var _);
		}

		public void Read(out bool bit0, out bool bit1, out bool bit2, out bool bit3, out bool bit4, out bool bit5)
		{
			Read(out bit0, out bit1, out bit2, out bit3, out bit4, out bit5, out var _, out var _);
		}

		public void Read(out bool bit0, out bool bit1, out bool bit2, out bool bit3, out bool bit4, out bool bit5, out bool bit6)
		{
			Read(out bit0, out bit1, out bit2, out bit3, out bit4, out bit5, out bit6, out var _);
		}

		public void Read(out bool bit0, out bool bit1, out bool bit2, out bool bit3, out bool bit4, out bool bit5, out bool bit6, out bool bit7)
		{
			byte b = Read<byte>();
			bit0 = (b & 1) != 0;
			bit1 = (b & 2) != 0;
			bit2 = (b & 4) != 0;
			bit3 = (b & 8) != 0;
			bit4 = (b & 0x10) != 0;
			bit5 = (b & 0x20) != 0;
			bit6 = (b & 0x40) != 0;
			bit7 = (b & 0x80) != 0;
		}

		public void ReadObject<T>(ref T? obj) where T : class, IMemoryPackable, new()
		{
			if (Read<bool>())
			{
				if (obj == null)
				{
					obj = Pool.Borrow<T>();
				}
				obj.Unpack(ref this);
			}
			else
			{
				obj = null;
			}
		}

		public void ReadObjectList<T>(ref List<T> list) where T : class, IMemoryPackable, new()
		{
			int num = Read<int>();
			if (num > 0)
			{
				if (list == null)
				{
					list = new List<T>();
				}
				for (int i = 0; i < num; i++)
				{
					T val;
					if (list.Count == i)
					{
						val = Pool.Borrow<T>();
						list.Add(val);
					}
					else
					{
						val = list[i];
					}
					val.Unpack(ref this);
				}
			}
			if (list != null)
			{
				while (list.Count > num)
				{
					int index = list.Count - 1;
					Pool.Return(list[index]);
					list.RemoveAt(list.Count - 1);
				}
			}
		}

		public void ReadPolymorphicList<T>(ref List<T> list) where T : PolymorphicMemoryPackableEntity<T>
		{
			int num = Read<int>();
			if (num > 0)
			{
				if (list == null)
				{
					list = new List<T>();
				}
				for (int i = 0; i < num; i++)
				{
					if (list.Count == i)
					{
						list.Add(PolymorphicMemoryPackableEntity<T>.Decode(ref this));
					}
					else
					{
						list[i] = PolymorphicMemoryPackableEntity<T>.Decode(ref this, list[i]);
					}
				}
			}
			if (list != null)
			{
				while (list.Count > num)
				{
					int index = list.Count - 1;
					PolymorphicMemoryPackableEntity<T>.ReturnAuto(Pool, list[index]);
					list.RemoveAt(list.Count - 1);
				}
			}
		}

		public void ReadValueList<T>(ref List<T> list) where T : unmanaged
		{
			ReadValueList<List<T>, T>(ref list);
		}

		public void ReadValueList<T>(ref HashSet<T> list) where T : unmanaged
		{
			ReadValueList<HashSet<T>, T>(ref list);
		}

		public void ReadValueList<C, T>(ref C list) where C : ICollection<T>, new() where T : unmanaged
		{
			int num = Read<int>();
			C val;
			if (num > 0)
			{
				if (list == null)
				{
					list = new C();
				}
				list.Clear();
				ReadOnlySpan<T> readOnlySpan = Access<T>(num);
				for (int i = 0; i < readOnlySpan.Length; i++)
				{
					ref C reference = ref list;
					val = default(C);
					if (val == null)
					{
						val = reference;
						reference = ref val;
					}
					T item = readOnlySpan[i];
					reference.Add(item);
				}
				return;
			}
			ref C reference2 = ref list;
			val = default(C);
			if (val == null)
			{
				val = reference2;
				reference2 = ref val;
				if (val == null)
				{
					return;
				}
			}
			reference2.Clear();
		}

		public void ReadStringList(ref List<string> list)
		{
			int num = Read<int>();
			if (num > 0)
			{
				if (list == null)
				{
					list = new List<string>();
				}
				list.Clear();
				for (int i = 0; i < num; i++)
				{
					list.Add(ReadString());
				}
			}
			else
			{
				list?.Clear();
			}
		}

		public void ReadNestedValueList<T>(ref List<List<T>> list) where T : unmanaged
		{
			ReadNestedList(ref list, delegate(ref MemoryUnpacker unpacker, ref List<T> sublist)
			{
				unpacker.ReadValueList(ref sublist);
			});
		}

		public void ReadNestedList<T>(ref List<List<T>> list, ListReader<T> reader)
		{
			int num = Read<int>();
			if (num > 0)
			{
				if (list == null)
				{
					list = new List<List<T>>();
				}
				for (int i = 0; i < num; i++)
				{
					List<T> list2;
					if (list.Count == i)
					{
						list2 = null;
						list.Add(list2);
					}
					else
					{
						list2 = list[i];
					}
					reader(ref this, ref list2);
					list[i] = list2;
				}
			}
			if (list != null)
			{
				while (list.Count > num)
				{
					list.RemoveAt(list.Count - 1);
				}
			}
		}
	}
	[StructLayout(LayoutKind.Sequential, Pack = 16)]
	public struct SharedMemoryBufferDescriptor<T> where T : unmanaged
	{
		private SharedMemoryBufferDescriptor descriptor;

		public bool IsEmpty => length == 0;

		public int bufferId
		{
			get
			{
				return descriptor.bufferId;
			}
			set
			{
				descriptor.bufferId = value;
			}
		}

		public int bufferCapacity
		{
			get
			{
				return descriptor.bufferCapacity;
			}
			set
			{
				descriptor.bufferCapacity = value;
			}
		}

		public int offset
		{
			get
			{
				return descriptor.offset;
			}
			set
			{
				descriptor.offset = value;
			}
		}

		public int length
		{
			get
			{
				return descriptor.length;
			}
			set
			{
				descriptor.length = value;
			}
		}

		public SharedMemoryBufferDescriptor(int bufferId, int bufferCapacity, int offset, int length)
		{
			descriptor = default(SharedMemoryBufferDescriptor);
			this.bufferId = bufferId;
			this.bufferCapacity = bufferCapacity;
			this.offset = offset;
			this.length = length;
		}

		public SharedMemoryBufferDescriptor<O> As<O>() where O : unmanaged
		{
			return new SharedMemoryBufferDescriptor<O>(bufferId, bufferCapacity, offset, length);
		}

		public override string ToString()
		{
			return $"Shared Memory {typeof(T).Name}. BufferId: {bufferId}, Capacity: {bufferCapacity}, " + $"Offset: {offset}, Length: {length}";
		}
	}
	[StructLayout(LayoutKind.Explicit, Pack = 16)]
	public struct SharedMemoryBufferDescriptor
	{
		[FieldOffset(0)]
		public int bufferId;

		[FieldOffset(4)]
		public int bufferCapacity;

		[FieldOffset(8)]
		public int offset;

		[FieldOffset(12)]
		public int length;
	}
	public sealed class UnmanagedMemoryManager<T> : MemoryManager<T> where T : unmanaged
	{
		private unsafe readonly T* _pointer;

		private readonly int _length;

		public unsafe UnmanagedMemoryManager(Span<T> span)
		{
			fixed (T* reference = &MemoryMarshal.GetReference(span))
			{
				_pointer = reference;
				_length = span.Length;
			}
		}

		public unsafe UnmanagedMemoryManager(T* pointer, int length)
		{
			if (length < 0)
			{
				throw new ArgumentOutOfRangeException("length");
			}
			_pointer = pointer;
			_length = length;
		}

		public unsafe override Span<T> GetSpan()
		{
			return new Span<T>(_pointer, _length);
		}

		public unsafe override MemoryHandle Pin(int elementIndex = 0)
		{
			if (elementIndex < 0 || elementIndex >= _length)
			{
				throw new ArgumentOutOfRangeException("elementIndex");
			}
			return new MemoryHandle(_pointer + elementIndex);
		}

		public override void Unpin()
		{
		}

		protected override void Dispose(bool disposing)
		{
		}
	}
	public static class MathHelper
	{
		public static float FilterInvalid(float value, float fallback = 0f)
		{
			if (float.IsNaN(value) || float.IsInfinity(value))
			{
				return fallback;
			}
			return value;
		}

		public static int PixelsToBytes(int pixels, TextureFormat format)
		{
			return (int)BitsToBytes((double)pixels * format.GetBitsPerPixel());
		}

		public static double BitsToBytes(double bits)
		{
			return bits / 8.0;
		}

		public static double BytesToBits(double bytes)
		{
			return bytes * 8.0;
		}

		public static int BitsToBytes(int bits)
		{
			return bits >> 3;
		}

		public static int BytesToBits(int bytes)
		{
			return bytes << 3;
		}

		public static long BitsToBytes(long bits)
		{
			return bits >> 3;
		}

		public static long BytesToBits(long bytes)
		{
			return bytes << 3;
		}

		public static int AlignSize(int size, int blockSize)
		{
			return size + (blockSize - size % blockSize) % blockSize;
		}

		public static RenderVector2i AlignSize(RenderVector2i size, RenderVector2i blockSize)
		{
			return new RenderVector2i(AlignSize(size.x, blockSize.x), AlignSize(size.y, blockSize.y));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int CeilToInt(double val)
		{
			return (int)Math.Ceiling(val);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int RoundToInt(double val)
		{
			return (int)(val + ((val < 0.0) ? (-0.5) : 0.5));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long RoundToLong(double val)
		{
			return (long)(val + ((val < 0.0) ? (-0.5) : 0.5));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int FloorToInt(double val)
		{
			return (int)Math.Floor(val);
		}

		public static int AlignToNextMultiple(int value, int alignment)
		{
			return (value + alignment - 1) / alignment * alignment;
		}

		public static int NecessaryBits(ulong number)
		{
			int num = 0;
			while (number != 0L)
			{
				number >>= 1;
				num++;
			}
			return num;
		}

		public static bool HasFlag(this int flags, int index)
		{
			int num = 1 << index;
			return (flags & num) != 0;
		}

		public static void SetFlag(this ref int flags, int index, bool state)
		{
			int num = 1 << index;
			if (state)
			{
				flags |= num;
			}
			else
			{
				flags &= ~num;
			}
		}

		public static bool HasFlag(this ushort flags, int index)
		{
			int num = 1 << index;
			return (flags & num) != 0;
		}

		public static void SetFlag(this ref ushort flags, int index, bool state)
		{
			int num = 1 << index;
			if (state)
			{
				flags = (ushort)(flags | num);
			}
			else
			{
				flags = (ushort)(flags & ~num);
			}
		}

		public static bool HasFlag(this byte flags, int index)
		{
			int num = 1 << index;
			return (flags & num) != 0;
		}

		public static void SetFlag(this ref byte flags, int index, bool state)
		{
			int num = 1 << index;
			if (state)
			{
				flags = (byte)(flags | num);
			}
			else
			{
				flags = (byte)(flags & ~num);
			}
		}
	}
	public abstract class AssetCommand : RendererCommand
	{
		public int assetId = -1;

		public override void Pack(ref MemoryPacker packer)
		{
			packer.Write(assetId);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			packer.Read(ref assetId);
		}
	}
	public class GaussianSplatResult : AssetCommand
	{
		public bool instanceChanged;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(instanceChanged);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref instanceChanged);
		}
	}
	public abstract class GaussianSplatUpload : AssetCommand
	{
		public int splatCount;

		public RenderBoundingBox bounds;

		public SharedMemoryBufferDescriptor<byte> positionsBuffer;

		public SharedMemoryBufferDescriptor<byte> rotationsBuffer;

		public SharedMemoryBufferDescriptor<byte> scalesBuffer;

		public SharedMemoryBufferDescriptor<byte> colorsBuffer;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(splatCount);
			packer.Write(bounds);
			packer.Write(positionsBuffer);
			packer.Write(rotationsBuffer);
			packer.Write(scalesBuffer);
			packer.Write(colorsBuffer);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref splatCount);
			packer.Read(ref bounds);
			packer.Read(ref positionsBuffer);
			packer.Read(ref rotationsBuffer);
			packer.Read(ref scalesBuffer);
			packer.Read(ref colorsBuffer);
		}
	}
	public class GaussianSplatUploadEncoded : GaussianSplatUpload
	{
		public GaussianVectorFormat positionsFormat;

		public GaussianRotationFormat rotationsFormat;

		public GaussianVectorFormat scalesFormat;

		public GaussianColorFormat colorsFormat;

		public GaussianSHFormat shFormat;

		public int texture2DtextureAssetId;

		public int shIndexesOffset;

		public int chunkCount;

		public SharedMemoryBufferDescriptor<byte> shBuffer;

		public SharedMemoryBufferDescriptor<byte> chunksBuffer;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(positionsFormat);
			packer.Write(rotationsFormat);
			packer.Write(scalesFormat);
			packer.Write(colorsFormat);
			packer.Write(shFormat);
			packer.Write(texture2DtextureAssetId);
			packer.Write(shIndexesOffset);
			packer.Write(chunkCount);
			packer.Write(shBuffer);
			packer.Write(chunksBuffer);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref positionsFormat);
			packer.Read(ref rotationsFormat);
			packer.Read(ref scalesFormat);
			packer.Read(ref colorsFormat);
			packer.Read(ref shFormat);
			packer.Read(ref texture2DtextureAssetId);
			packer.Read(ref shIndexesOffset);
			packer.Read(ref chunkCount);
			packer.Read(ref shBuffer);
			packer.Read(ref chunksBuffer);
		}
	}
	public class GaussianSplatUploadRaw : GaussianSplatUpload
	{
		public SharedMemoryBufferDescriptor<byte> alphasBuffer;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(alphasBuffer);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref alphasBuffer);
		}
	}
	public class UnloadGaussianSplat : AssetCommand
	{
	}
	public class MaterialPropertyIdRequest : RendererCommand
	{
		public int requestId;

		public List<string> propertyNames = new List<string>();

		public override void Pack(ref MemoryPacker packer)
		{
			packer.Write(requestId);
			packer.WriteStringList(propertyNames);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			packer.Read(ref requestId);
			packer.ReadStringList(ref propertyNames);
		}
	}
	public class MaterialPropertyIdResult : RendererCommand
	{
		public int requestId;

		public List<int> propertyIDs = new List<int>();

		public override void Pack(ref MemoryPacker packer)
		{
			packer.Write(requestId);
			packer.WriteValueList(propertyIDs);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			packer.Read(ref requestId);
			packer.ReadValueList(ref propertyIDs);
		}
	}
	[StructLayout(LayoutKind.Explicit, Size = 8)]
	public struct MaterialPropertyUpdate
	{
		[FieldOffset(0)]
		public int propertyID;

		[FieldOffset(4)]
		public MaterialPropertyUpdateType updateType;

		public MaterialPropertyUpdate(int propertyId, MaterialPropertyUpdateType updateType)
		{
			propertyID = propertyId;
			this.updateType = updateType;
		}

		public override string ToString()
		{
			return $"{updateType} (PropertyID: {propertyID})";
		}
	}
	public enum MaterialPropertyUpdateType : byte
	{
		SelectTarget,
		SetShader,
		SetRenderQueue,
		SetInstancing,
		SetRenderType,
		SetFloat,
		SetFloat4,
		SetFloat4x4,
		SetFloatArray,
		SetFloat4Array,
		SetTexture,
		UpdateBatchEnd
	}
	public enum MaterialRenderType
	{
		Opaque,
		TransparentCutout,
		Transparent
	}
	public class MaterialsUpdateBatch : RendererCommand
	{
		public int updateBatchId = -1;

		public List<SharedMemoryBufferDescriptor<MaterialPropertyUpdate>> materialUpdates = new List<SharedMemoryBufferDescriptor<MaterialPropertyUpdate>>();

		public int materialUpdateCount;

		public List<SharedMemoryBufferDescriptor<int>> intBuffers = new List<SharedMemoryBufferDescriptor<int>>();

		public List<SharedMemoryBufferDescriptor<float>> floatBuffers = new List<SharedMemoryBufferDescriptor<float>>();

		public List<SharedMemoryBufferDescriptor<RenderVector4>> float4Buffers = new List<SharedMemoryBufferDescriptor<RenderVector4>>();

		public List<SharedMemoryBufferDescriptor<RenderMatrix4x4>> matrixBuffers = new List<SharedMemoryBufferDescriptor<RenderMatrix4x4>>();

		public SharedMemoryBufferDescriptor<uint> instanceChangedBuffer;

		public override void Pack(ref MemoryPacker packer)
		{
			packer.Write(updateBatchId);
			packer.WriteValueList(materialUpdates);
			packer.Write(materialUpdateCount);
			packer.WriteValueList(intBuffers);
			packer.WriteValueList(floatBuffers);
			packer.WriteValueList(float4Buffers);
			packer.WriteValueList(matrixBuffers);
			packer.Write(instanceChangedBuffer);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			packer.Read(ref updateBatchId);
			packer.ReadValueList(ref materialUpdates);
			packer.Read(ref materialUpdateCount);
			packer.ReadValueList(ref intBuffers);
			packer.ReadValueList(ref floatBuffers);
			packer.ReadValueList(ref float4Buffers);
			packer.ReadValueList(ref matrixBuffers);
			packer.Read(ref instanceChangedBuffer);
		}

		public override string ToString()
		{
			return $"BatchId: {updateBatchId}\n" + $"MaterialUpdateCount: {materialUpdateCount}\n" + "MaterialUpdates:\n\t" + string.Join("\n\t", materialUpdates) + "\nIntBuffers:\n\t" + string.Join("\n\t", intBuffers) + "\nFloatBuffers:\n\t" + string.Join("\n\t", floatBuffers) + "\nFloat4Buffers:\n\t" + string.Join("\n\t", float4Buffers) + "\nMatrixBuffers:\n\t" + string.Join("\n\t", matrixBuffers);
		}
	}
	public class MaterialsUpdateBatchResult : RendererCommand
	{
		public int updateBatchId = -1;

		public override void Pack(ref MemoryPacker packer)
		{
			packer.Write(updateBatchId);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			packer.Read(ref updateBatchId);
		}
	}
	public class UnloadMaterial : AssetCommand
	{
	}
	public class UnloadMaterialPropertyBlock : AssetCommand
	{
	}
	public class MeshUnload : AssetCommand
	{
	}
	public class MeshUploadData : AssetCommand
	{
		public bool highPriority;

		public SharedMemoryBufferDescriptor<byte> buffer;

		public int vertexCount;

		public int boneWeightCount;

		public int boneCount;

		public IndexBufferFormat indexBufferFormat;

		public List<VertexAttributeDescriptor> vertexAttributes;

		public List<SubmeshBufferDescriptor> submeshes;

		public List<BlendshapeBufferDescriptor> blendshapeBuffers;

		public MeshUploadHint uploadHint;

		public RenderBoundingBox bounds;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(highPriority);
			packer.Write(buffer);
			packer.Write(vertexCount);
			packer.Write(boneWeightCount);
			packer.Write(boneCount);
			packer.Write(indexBufferFormat);
			packer.WriteValueList(vertexAttributes);
			packer.WriteValueList(submeshes);
			packer.WriteValueList(blendshapeBuffers);
			packer.Write(uploadHint);
			packer.Write(bounds);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref highPriority);
			packer.Read(ref buffer);
			packer.Read(ref vertexCount);
			packer.Read(ref boneWeightCount);
			packer.Read(ref boneCount);
			packer.Read(ref indexBufferFormat);
			packer.ReadValueList(ref vertexAttributes);
			packer.ReadValueList(ref submeshes);
			packer.ReadValueList(ref blendshapeBuffers);
			packer.Read(ref uploadHint);
			packer.Read(ref bounds);
		}
	}
	public class MeshUploadResult : AssetCommand
	{
		public bool instanceChanged;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(instanceChanged);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref instanceChanged);
		}
	}
	public class PointRenderBufferConsumed : AssetCommand
	{
	}
	public class PointRenderBufferUnload : AssetCommand
	{
	}
	public class PointRenderBufferUpload : RenderBufferUpload
	{
		public int count;

		public int positionsOffset;

		public int rotationsOffset;

		public int sizesOffset;

		public int colorsOffset;

		public int frameIndexesOffset;

		public RenderVector2i frameGridSize;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(count);
			packer.Write(positionsOffset);
			packer.Write(rotationsOffset);
			packer.Write(sizesOffset);
			packer.Write(colorsOffset);
			packer.Write(frameIndexesOffset);
			packer.Write(frameGridSize);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref count);
			packer.Read(ref positionsOffset);
			packer.Read(ref rotationsOffset);
			packer.Read(ref sizesOffset);
			packer.Read(ref colorsOffset);
			packer.Read(ref frameIndexesOffset);
			packer.Read(ref frameGridSize);
		}
	}
	public abstract class RenderBufferUpload : AssetCommand
	{
		public SharedMemoryBufferDescriptor<byte> buffer;

		public bool IsEmpty => buffer.IsEmpty;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(buffer);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref buffer);
		}
	}
	public class TrailRenderBufferConsumed : AssetCommand
	{
	}
	public class TrailRenderBufferUnload : AssetCommand
	{
	}
	public class TrailRenderBufferUpload : RenderBufferUpload
	{
		public int trailsCount;

		public int trailPointCount;

		public int trailsOffset;

		public int positionsOffset;

		public int colorsOffset;

		public int sizesOffset;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(trailsCount);
			packer.Write(trailPointCount);
			packer.Write(trailsOffset);
			packer.Write(positionsOffset);
			packer.Write(colorsOffset);
			packer.Write(sizesOffset);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref trailsCount);
			packer.Read(ref trailPointCount);
			packer.Read(ref trailsOffset);
			packer.Read(ref positionsOffset);
			packer.Read(ref colorsOffset);
			packer.Read(ref sizesOffset);
		}
	}
	public class ShaderUnload : AssetCommand
	{
	}
	public class ShaderUpload : AssetCommand
	{
		public string file;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(file);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref file);
		}
	}
	public class ShaderUploadResult : AssetCommand
	{
		public bool instanceChanged;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(instanceChanged);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref instanceChanged);
		}
	}
	public class SetCubemapData : AssetCommand
	{
		public SharedMemoryBufferDescriptor<byte> data;

		public int startMipLevel;

		public List<RenderVector2i> mipMapSizes;

		public List<List<int>> mipStarts;

		public bool flipY;

		public bool highPriority;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(data);
			packer.Write(startMipLevel);
			packer.WriteValueList(mipMapSizes);
			packer.WriteNestedValueList(mipStarts);
			packer.Write(flipY);
			packer.Write(highPriority);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref data);
			packer.Read(ref startMipLevel);
			packer.ReadValueList(ref mipMapSizes);
			packer.ReadNestedValueList(ref mipStarts);
			packer.Read(ref flipY);
			packer.Read(ref highPriority);
		}
	}
	public class SetCubemapFormat : AssetCommand
	{
		public int size;

		public int mipmapCount;

		public TextureFormat format;

		public ColorProfile profile;

		public void Validate()
		{
			if (format.IsHDR() && profile != ColorProfile.Linear)
			{
				throw new InvalidOperationException($"Cubemap {assetId} uses HDR format {format}, but non-Linear color profile {profile}");
			}
		}

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(size);
			packer.Write(mipmapCount);
			packer.Write(format);
			packer.Write(profile);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref size);
			packer.Read(ref mipmapCount);
			packer.Read(ref format);
			packer.Read(ref profile);
		}
	}
	public class SetCubemapProperties : AssetCommand
	{
		public TextureFilterMode filterMode;

		public int anisoLevel;

		public float mipmapBias;

		public bool applyImmediatelly;

		public bool highPriority;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(filterMode);
			packer.Write(anisoLevel);
			packer.Write(mipmapBias);
			packer.Write(applyImmediatelly);
			packer.Write(highPriority);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref filterMode);
			packer.Read(ref anisoLevel);
			packer.Read(ref mipmapBias);
			packer.Read(ref applyImmediatelly);
			packer.Read(ref highPriority);
		}
	}
	public class SetCubemapResult : AssetCommand
	{
		public TextureUpdateResultType type;

		public bool instanceChanged;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(type);
			packer.Write(instanceChanged);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref type);
			packer.Read(ref instanceChanged);
		}
	}
	public class UnloadCubemap : AssetCommand
	{
	}
	public class DesktopTexturePropertiesUpdate : AssetCommand
	{
		public RenderVector2i size;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(size);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref size);
		}
	}
	public class SetDesktopTextureProperties : AssetCommand
	{
		public int displayIndex;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(displayIndex);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref displayIndex);
		}
	}
	public class UnloadDesktopTexture : AssetCommand
	{
	}
	public class RenderTextureResult : AssetCommand
	{
		public bool instanceChanged;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(instanceChanged);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref instanceChanged);
		}
	}
	public class SetRenderTextureFormat : AssetCommand
	{
		public RenderVector2i size;

		public int depth;

		public TextureFilterMode filterMode;

		public int anisoLevel;

		public TextureWrapMode wrapU;

		public TextureWrapMode wrapV;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(size);
			packer.Write(depth);
			packer.Write(filterMode);
			packer.Write(anisoLevel);
			packer.Write(wrapU);
			packer.Write(wrapV);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref size);
			packer.Read(ref depth);
			packer.Read(ref filterMode);
			packer.Read(ref anisoLevel);
			packer.Read(ref wrapU);
			packer.Read(ref wrapV);
		}
	}
	public class UnloadRenderTexture : AssetCommand
	{
	}
	public class SetTexture2DData : AssetCommand
	{
		public SharedMemoryBufferDescriptor<byte> data;

		public int startMipLevel;

		public List<RenderVector2i> mipMapSizes;

		public List<int> mipStarts;

		public bool flipY;

		public TextureUploadHint hint;

		public bool highPriority;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(data);
			packer.Write(startMipLevel);
			packer.WriteValueList(mipMapSizes);
			packer.WriteValueList(mipStarts);
			packer.Write(flipY);
			packer.Write(hint);
			packer.Write(highPriority);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref data);
			packer.Read(ref startMipLevel);
			packer.ReadValueList(ref mipMapSizes);
			packer.ReadValueList(ref mipStarts);
			packer.Read(ref flipY);
			packer.Read(ref hint);
			packer.Read(ref highPriority);
		}
	}
	public class SetTexture2DFormat : AssetCommand
	{
		public int width;

		public int height;

		public int mipmapCount;

		public TextureFormat format;

		public ColorProfile profile;

		public void Validate()
		{
			if (format.IsHDR() && profile != ColorProfile.Linear)
			{
				throw new InvalidOperationException($"2D Texture {assetId} uses HDR format {format}, but non-Linear color profile {profile}");
			}
		}

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(width);
			packer.Write(height);
			packer.Write(mipmapCount);
			packer.Write(format);
			packer.Write(profile);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref width);
			packer.Read(ref height);
			packer.Read(ref mipmapCount);
			packer.Read(ref format);
			packer.Read(ref profile);
		}
	}
	public class SetTexture2DProperties : AssetCommand
	{
		public TextureFilterMode filterMode;

		public int anisoLevel;

		public TextureWrapMode wrapU;

		public TextureWrapMode wrapV;

		public float mipmapBias;

		public bool applyImmediatelly;

		public bool highPriority;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(filterMode);
			packer.Write(anisoLevel);
			packer.Write(wrapU);
			packer.Write(wrapV);
			packer.Write(mipmapBias);
			packer.Write(applyImmediatelly);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref filterMode);
			packer.Read(ref anisoLevel);
			packer.Read(ref wrapU);
			packer.Read(ref wrapV);
			packer.Read(ref mipmapBias);
			packer.Read(ref applyImmediatelly);
		}
	}
	public class SetTexture2DResult : AssetCommand
	{
		public TextureUpdateResultType type;

		public bool instanceChanged;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(type);
			packer.Write(instanceChanged);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref type);
			packer.Read(ref instanceChanged);
		}
	}
	public class UnloadTexture2D : AssetCommand
	{
	}
	public class SetTexture3DData : AssetCommand
	{
		public SharedMemoryBufferDescriptor<byte> data;

		public Texture3DUploadHint hint;

		public bool highPriority;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(data);
			packer.Write(hint);
			packer.Write(highPriority);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref data);
			packer.Read(ref hint);
			packer.Read(ref highPriority);
		}
	}
	public class SetTexture3DFormat : AssetCommand
	{
		public int width;

		public int height;

		public int depth;

		public int mipmapCount;

		public TextureFormat format;

		public ColorProfile profile;

		public void Validate()
		{
			if (format.IsHDR() && profile != ColorProfile.Linear)
			{
				throw new InvalidOperationException($"3D Texture {assetId} uses HDR format {format}, but non-Linear color profile {profile}");
			}
		}

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(width);
			packer.Write(height);
			packer.Write(depth);
			packer.Write(mipmapCount);
			packer.Write(format);
			packer.Write(profile);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref width);
			packer.Read(ref height);
			packer.Read(ref depth);
			packer.Read(ref mipmapCount);
			packer.Read(ref format);
			packer.Read(ref profile);
		}
	}
	public class SetTexture3DProperties : AssetCommand
	{
		public TextureFilterMode filterMode;

		public int anisoLevel;

		public TextureWrapMode wrapU;

		public TextureWrapMode wrapV;

		public TextureWrapMode wrapW;

		public bool applyImmediatelly;

		public bool highPriority;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(filterMode);
			packer.Write(anisoLevel);
			packer.Write(wrapU);
			packer.Write(wrapV);
			packer.Write(wrapW);
			packer.Write(applyImmediatelly);
			packer.Write(highPriority);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref filterMode);
			packer.Read(ref anisoLevel);
			packer.Read(ref wrapU);
			packer.Read(ref wrapV);
			packer.Read(ref wrapW);
			packer.Read(ref applyImmediatelly);
			packer.Read(ref highPriority);
		}
	}
	public class SetTexture3DResult : AssetCommand
	{
		public TextureUpdateResultType type;

		public bool instanceChanged;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(type);
			packer.Write(instanceChanged);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref type);
			packer.Read(ref instanceChanged);
		}
	}
	public class UnloadTexture3D : AssetCommand
	{
	}
	[Flags]
	public enum TextureUpdateResultType
	{
		FormatSet = 1,
		PropertiesSet = 2,
		DataUpload = 4
	}
	public class UnloadVideoTexture : AssetCommand
	{
	}
	public class VideoAudioTrack : IMemoryPackable
	{
		public int index;

		public int channelCount;

		public int sampleRate;

		public string name;

		public string languageCode;

		public void Pack(ref MemoryPacker packer)
		{
			packer.Write(index);
			packer.Write(channelCount);
			packer.Write(sampleRate);
			packer.Write(name);
			packer.Write(languageCode);
		}

		public void Unpack(ref MemoryUnpacker unpacker)
		{
			unpacker.Read(ref index);
			unpacker.Read(ref channelCount);
			unpacker.Read(ref sampleRate);
			unpacker.Read(ref name);
			unpacker.Read(ref languageCode);
		}

		public override string ToString()
		{
			return $"Audio Track {index}. Channels: {channelCount}, SampleRate: {sampleRate}, Name: {name}, Language: {languageCode}";
		}
	}
	public class VideoTextureChanged : AssetCommand
	{
	}
	[StructLayout(LayoutKind.Explicit)]
	public struct VideoTextureClockErrorState
	{
		[FieldOffset(0)]
		public int assetId;

		[FieldOffset(4)]
		public float currentClockError;
	}
	public class VideoTextureLoad : AssetCommand
	{
		public string source;

		public string overrideEngine;

		public string mimeType;

		public bool isStream;

		public int audioSystemSampleRate;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(source);
			packer.Write(overrideEngine);
			packer.Write(mimeType);
			packer.Write(isStream);
			packer.Write(audioSystemSampleRate);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref source);
			packer.Read(ref overrideEngine);
			packer.Read(ref mimeType);
			packer.Read(ref isStream);
			packer.Read(ref audioSystemSampleRate);
		}
	}
	public class VideoTextureProperties : AssetCommand
	{
		public TextureFilterMode filterMode;

		public int anisoLevel;

		public TextureWrapMode wrapU;

		public TextureWrapMode wrapV;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(filterMode);
			packer.Write(anisoLevel);
			packer.Write(wrapU);
			packer.Write(wrapV);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref filterMode);
			packer.Read(ref anisoLevel);
			packer.Read(ref wrapU);
			packer.Read(ref wrapV);
		}
	}
	public class VideoTextureReady : AssetCommand
	{
		public double length;

		public RenderVector2i size;

		public bool hasAlpha;

		public string playbackEngine;

		public bool instanceChanged;

		public List<VideoAudioTrack> audioTracks;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(length);
			packer.Write(size);
			packer.Write(hasAlpha);
			packer.Write(playbackEngine);
			packer.Write(instanceChanged);
			packer.WriteObjectList(audioTracks);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref length);
			packer.Read(ref size);
			packer.Read(ref hasAlpha);
			packer.Read(ref playbackEngine);
			packer.Read(ref instanceChanged);
			packer.ReadObjectList(ref audioTracks);
		}
	}
	public class VideoTextureStartAudioTrack : AssetCommand
	{
		public int audioTrackIndex;

		public int queueCapacity;

		public string queueName;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(audioTrackIndex);
			packer.Write(queueCapacity);
			packer.Write(queueName);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref audioTrackIndex);
			packer.Read(ref queueCapacity);
			packer.Read(ref queueName);
		}
	}
	public class VideoTextureUpdate : AssetCommand
	{
		public double position;

		public bool play;

		public bool loop;

		public DateTime decodedTime;

		public double AdjustedPosition => position + (DateTime.UtcNow - decodedTime).TotalSeconds;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(position);
			packer.Write(play, loop);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref position);
			packer.Read(out play, out loop);
			decodedTime = DateTime.UtcNow;
		}
	}
	[DataModelType]
	[OldTypeName("FrooxEngine.AntiAliasingMethod", "FrooxEngine")]
	public enum AntiAliasingMethod
	{
		Off,
		FXAA,
		CTAA,
		SMAA,
		TAA
	}
	public class DesktopConfig : RendererCommand
	{
		public int? maximumBackgroundFramerate;

		public int? maximumForegroundFramerate;

		public bool vSync;

		public override void Pack(ref MemoryPacker packer)
		{
			packer.Write(maximumBackgroundFramerate);
			packer.Write(maximumForegroundFramerate);
			packer.Write(vSync);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			packer.Read(ref maximumBackgroundFramerate);
			packer.Read(ref maximumForegroundFramerate);
			packer.Read(ref vSync);
		}
	}
	public class GaussianSplatConfig : RendererCommand
	{
		public float sortingMegaOperationsPerCamera;

		public override void Pack(ref MemoryPacker packer)
		{
			packer.Write(sortingMegaOperationsPerCamera);
		}

		public override void Unpack(ref MemoryUnpacker unpacker)
		{
			unpacker.Read(ref sortingMegaOperationsPerCamera);
		}
	}
	public class PostProcessingConfig : RendererCommand
	{
		public float motionBlurIntensity;

		public float bloomIntensity;

		public float ambientOcclusionIntensity;

		public bool screenSpaceReflections;

		public AntiAliasingMethod antialiasing;

		public override void Pack(ref MemoryPacker packer)
		{
			packer.Write(motionBlurIntensity);
			packer.Write(bloomIntensity);
			packer.Write(ambientOcclusionIntensity);
			packer.Write(screenSpaceReflections);
			packer.Write(antialiasing);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			packer.Read(ref motionBlurIntensity);
			packer.Read(ref bloomIntensity);
			packer.Read(ref ambientOcclusionIntensity);
			packer.Read(ref screenSpaceReflections);
			packer.Read(ref antialiasing);
		}
	}
	public class QualityConfig : RendererCommand
	{
		public int perPixelLights;

		public ShadowCascadeMode shadowCascades;

		public ShadowResolutionMode shadowResolution;

		public float shadowDistance;

		public SkinWeightMode skinWeightMode;

		public override void Pack(ref MemoryPacker packer)
		{
			packer.Write(perPixelLights);
			packer.Write(shadowCascades);
			packer.Write(shadowResolution);
			packer.Write(shadowDistance);
			packer.Write(skinWeightMode);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			packer.Read(ref perPixelLights);
			packer.Read(ref shadowCascades);
			packer.Read(ref shadowResolution);
			packer.Read(ref shadowDistance);
			packer.Read(ref skinWeightMode);
		}
	}
	[DataModelType]
	[OldTypeName("FrooxEngine.ShadowCascadeMode", "FrooxEngine")]
	public enum ShadowCascadeMode
	{
		None,
		TwoCascades,
		FourCascades
	}
	[DataModelType]
	[OldTypeName("FrooxEngine.ShadowResolutionMode", "FrooxEngine")]
	public enum ShadowResolutionMode
	{
		Low,
		Medium,
		High,
		Ultra
	}
	[DataModelType]
	[OldTypeName("FrooxEngine.SkinWeightMode", "FrooxEngine")]
	public enum SkinWeightMode
	{
		OneBone,
		TwoBones,
		FourBones,
		Unlimited
	}
	public class RenderDecouplingConfig : RendererCommand
	{
		public float decoupleActivateInterval;

		public float decoupledMaxAssetProcessingTime;

		public int recoupleFrameCount;

		public override void Pack(ref MemoryPacker packer)
		{
			packer.Write(decoupleActivateInterval);
			packer.Write(decoupledMaxAssetProcessingTime);
			packer.Write(recoupleFrameCount);
		}

		public override void Unpack(ref MemoryUnpacker unpacker)
		{
			unpacker.Read(ref decoupleActivateInterval);
			unpacker.Read(ref decoupledMaxAssetProcessingTime);
			unpacker.Read(ref recoupleFrameCount);
		}
	}
	public class ResolutionConfig : RendererCommand
	{
		public RenderVector2i resolution;

		public bool fullscreen;

		public override void Pack(ref MemoryPacker packer)
		{
			packer.Write(resolution);
			packer.Write(fullscreen);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			packer.Read(ref resolution);
			packer.Read(ref fullscreen);
		}
	}
	[DataModelType]
	[OldTypeName("FrooxEngine.HeadOutputDevice", "FrooxEngine")]
	public enum HeadOutputDevice
	{
		Autodetect,
		Headless,
		Screen,
		Screen360,
		StaticCamera,
		StaticCamera360,
		SteamVR,
		WindowsMR,
		Oculus,
		OculusQuest,
		UNKNOWN
	}
	public static class HeadOutputDeviceExtension
	{
		public static bool IsScreenViewSupported(this HeadOutputDevice device)
		{
			if (device == HeadOutputDevice.OculusQuest)
			{
				return false;
			}
			return true;
		}

		public static bool IsVRViewSupported(this HeadOutputDevice device)
		{
			if ((uint)(device - 2) <= 3u)
			{
				return false;
			}
			return true;
		}

		public static bool IsNewInteractionMode(this HeadOutputDevice device)
		{
			return !device.IsCameraMode();
		}

		public static bool IsCameraMode(this HeadOutputDevice device)
		{
			if ((uint)(device - 4) <= 1u)
			{
				return true;
			}
			return false;
		}

		public static bool IsCamera(this HeadOutputDevice device)
		{
			if ((uint)(device - 4) <= 1u)
			{
				return true;
			}
			return false;
		}

		public static bool HasTwoControllers(this HeadOutputDevice device)
		{
			if ((uint)(device - 6) <= 3u)
			{
				return true;
			}
			return false;
		}

		public static bool HasVoice(this HeadOutputDevice device)
		{
			if (device.IsCamera())
			{
				return false;
			}
			return true;
		}

		public static bool IsVR(this HeadOutputDevice device)
		{
			if ((uint)(device - 6) <= 3u)
			{
				return true;
			}
			return false;
		}
	}
	public class CameraRenderParameters : IMemoryPackable
	{
		public RenderVector2i resolution;

		public TextureFormat textureFormat = TextureFormat.ARGB32;

		public CameraProjection projection;

		public float fov = 60f;

		public float orthographicSize = 8f;

		public CameraClearMode clearMode;

		public RenderVector4 clearColor;

		public float nearClip = 0.01f;

		public float farClip = 2048f;

		public bool renderPrivateUI;

		public bool postProcessing = true;

		public bool screenSpaceReflections;

		public void Pack(ref MemoryPacker packer)
		{
			packer.Write(resolution);
			packer.Write(textureFormat);
			packer.Write(projection);
			packer.Write(fov);
			packer.Write(orthographicSize);
			packer.Write(clearMode);
			packer.Write(clearColor);
			packer.Write(nearClip);
			packer.Write(farClip);
			packer.Write(renderPrivateUI);
			packer.Write(postProcessing);
			packer.Write(screenSpaceReflections);
		}

		public void Unpack(ref MemoryUnpacker packer)
		{
			packer.Read(ref resolution);
			packer.Read(ref textureFormat);
			packer.Read(ref projection);
			packer.Read(ref fov);
			packer.Read(ref orthographicSize);
			packer.Read(ref clearMode);
			packer.Read(ref clearColor);
			packer.Read(ref nearClip);
			packer.Read(ref farClip);
			packer.Read(ref renderPrivateUI);
			packer.Read(ref postProcessing);
			packer.Read(ref screenSpaceReflections);
		}
	}
	public class CameraRenderTask : IMemoryPackable
	{
		public int renderSpaceId;

		public RenderVector3 position;

		public RenderQuaternion rotation;

		public CameraRenderParameters parameters;

		public SharedMemoryBufferDescriptor<byte> resultData;

		public List<int> onlyRenderList;

		public List<int> excludeRenderList;

		public void Pack(ref MemoryPacker packer)
		{
			packer.Write(renderSpaceId);
			packer.Write(position);
			packer.Write(rotation);
			packer.WriteObject(parameters);
			packer.Write(resultData);
			packer.WriteValueList(onlyRenderList);
			packer.WriteValueList(excludeRenderList);
		}

		public void Unpack(ref MemoryUnpacker packer)
		{
			packer.Read(ref renderSpaceId);
			packer.Read(ref position);
			packer.Read(ref rotation);
			packer.ReadObject(ref parameters);
			packer.Read(ref resultData);
			packer.ReadValueList(ref onlyRenderList);
			packer.ReadValueList(ref excludeRenderList);
		}
	}
	public class FrameStartData : RendererCommand
	{
		public int lastFrameIndex;

		public PerformanceState performance;

		public InputState inputs;

		public List<ReflectionProbeChangeRenderResult> renderedReflectionProbes;

		public List<VideoTextureClockErrorState> videoClockErrors;

		public override void Pack(ref MemoryPacker packer)
		{
			packer.Write(lastFrameIndex);
			packer.WriteObject(performance);
			packer.WriteObject(inputs);
			packer.WriteValueList(renderedReflectionProbes);
			packer.WriteValueList(videoClockErrors);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			packer.Read(ref lastFrameIndex);
			packer.ReadObject(ref performance);
			packer.ReadObject(ref inputs);
			packer.ReadValueList(ref renderedReflectionProbes);
			packer.ReadValueList(ref videoClockErrors);
		}
	}
	public class FrameSubmitData : RendererCommand
	{
		public int frameIndex;

		public bool debugLog;

		public bool vrActive;

		public float nearClip;

		public float farClip;

		public float desktopFOV;

		public OutputState outputState;

		public List<RenderSpaceUpdate> renderSpaces = new List<RenderSpaceUpdate>();

		public List<CameraRenderTask> renderTasks;

		public override void Pack(ref MemoryPacker packer)
		{
			packer.Write(frameIndex);
			packer.Write(debugLog);
			packer.Write(vrActive);
			packer.Write(nearClip);
			packer.Write(farClip);
			packer.Write(desktopFOV);
			packer.WriteObject(outputState);
			packer.WriteObjectList(renderSpaces);
			packer.WriteObjectList(renderTasks);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			packer.Read(ref frameIndex);
			packer.Read(ref debugLog);
			packer.Read(ref vrActive);
			packer.Read(ref nearClip);
			packer.Read(ref farClip);
			packer.Read(ref desktopFOV);
			packer.ReadObject(ref outputState);
			packer.ReadObjectList(ref renderSpaces);
			packer.ReadObjectList(ref renderTasks);
		}
	}
	public class KeepAlive : RendererCommand
	{
		public override void Pack(ref MemoryPacker packer)
		{
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
		}
	}
	public class PerformanceState : IMemoryPackable
	{
		public float fps;

		public float immediateFPS;

		public float renderTime;

		public float externalUpdateTime;

		public int renderedFramesSinceLast;

		public float frameBeginToSubmitTime;

		public float frameProcessedToNextBeginTime;

		public float integrationProcessingTime;

		public float extraParticleProcessingTime;

		public int processedAssetIntegratorTasks;

		public int integrationHighPriorityTasks;

		public int integrationTasks;

		public int integrationRenderTasks;

		public int integrationParticleTasks;

		public int processingHandleWaits;

		public float frameUpdateHandleTime;

		public int renderedCameras;

		public int renderedCameraPortals;

		public int updatedTextures;

		public int textureSliceUploads;

		public void Pack(ref MemoryPacker packer)
		{
			packer.Write(fps);
			packer.Write(immediateFPS);
			packer.Write(renderTime);
			packer.Write(externalUpdateTime);
			packer.Write(renderedFramesSinceLast);
			packer.Write(frameBeginToSubmitTime);
			packer.Write(frameProcessedToNextBeginTime);
			packer.Write(integrationProcessingTime);
			packer.Write(extraParticleProcessingTime);
			packer.Write(processedAssetIntegratorTasks);
			packer.Write(integrationHighPriorityTasks);
			packer.Write(integrationTasks);
			packer.Write(integrationRenderTasks);
			packer.Write(integrationParticleTasks);
			packer.Write(processingHandleWaits);
			packer.Write(frameUpdateHandleTime);
			packer.Write(renderedCameras);
			packer.Write(renderedCameraPortals);
			packer.Write(updatedTextures);
			packer.Write(textureSliceUploads);
		}

		public void Unpack(ref MemoryUnpacker packer)
		{
			packer.Read(ref fps);
			packer.Read(ref immediateFPS);
			packer.Read(ref renderTime);
			packer.Read(ref externalUpdateTime);
			packer.Read(ref renderedFramesSinceLast);
			packer.Read(ref frameBeginToSubmitTime);
			packer.Read(ref frameProcessedToNextBeginTime);
			packer.Read(ref integrationProcessingTime);
			packer.Read(ref extraParticleProcessingTime);
			packer.Read(ref processedAssetIntegratorTasks);
			packer.Read(ref integrationHighPriorityTasks);
			packer.Read(ref integrationTasks);
			packer.Read(ref integrationRenderTasks);
			packer.Read(ref integrationParticleTasks);
			packer.Read(ref processingHandleWaits);
			packer.Read(ref frameUpdateHandleTime);
			packer.Read(ref renderedCameras);
			packer.Read(ref renderedCameraPortals);
			packer.Read(ref updatedTextures);
			packer.Read(ref textureSliceUploads);
		}
	}
	public class ReflectionProbeRenderResult : RendererCommand
	{
		public int renderTaskId = -1;

		public bool success;

		public override void Pack(ref MemoryPacker packer)
		{
			packer.Write(renderTaskId);
			packer.Write(success);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			packer.Read(ref renderTaskId);
			packer.Read(ref success);
		}
	}
	public class ReflectionProbeRenderTask : IMemoryPackable
	{
		public int renderableIndex;

		public int renderTaskId;

		public int size;

		public bool hdr;

		public List<List<int>> mipOrigins;

		public SharedMemoryBufferDescriptor<byte> resultData;

		public List<int> excludeTransformIds;

		public void Pack(ref MemoryPacker packer)
		{
			packer.Write(renderableIndex);
			packer.Write(renderTaskId);
			packer.Write(size);
			packer.Write(hdr);
			packer.WriteNestedValueList(mipOrigins);
			packer.Write(resultData);
			packer.WriteValueList(excludeTransformIds);
		}

		public void Unpack(ref MemoryUnpacker packer)
		{
			packer.Read(ref renderableIndex);
			packer.Read(ref renderTaskId);
			packer.Read(ref size);
			packer.Read(ref hdr);
			packer.ReadNestedValueList(ref mipOrigins);
			packer.Read(ref resultData);
			packer.ReadValueList(ref excludeTransformIds);
		}
	}
	public class RenderSpaceUpdate : IMemoryPackable
	{
		public int id;

		public bool isActive;

		public bool isOverlay;

		public bool isPrivate;

		public RenderTransform rootTransform;

		public bool viewPositionIsExternal;

		public bool overrideViewPosition;

		public int skyboxMaterialAssetId;

		public RenderSH2 ambientLight;

		public RenderTransform overridenViewTransform;

		public TransformsUpdate? transformsUpdate;

		public MeshRenderablesUpdate? meshRenderersUpdate;

		public SkinnedMeshRenderablesUpdate? skinnedMeshRenderersUpdate;

		public LightRenderablesUpdate? lightsUpdate;

		public CameraRenderablesUpdate? camerasUpdate;

		public CameraPortalsRenderablesUpdate? cameraPortalsUpdate;

		public ReflectionProbeRenderablesUpdate? reflectionProbesUpdate;

		public ReflectionProbeSH2Tasks? reflectionProbeSH2Taks;

		public LayerUpdate? layersUpdate;

		public BillboardRenderBufferUpdate? billboardBuffersUpdate;

		public MeshRenderBufferUpdate? meshRenderBuffersUpdate;

		public TrailsRendererUpdate? trailRenderersUpdate;

		public LightsBufferRendererUpdate? lightsBufferRenderersUpdate;

		public RenderTransformOverridesUpdate? renderTransformOverridesUpdate;

		public RenderMaterialOverridesUpdate? renderMaterialOverridesUpdate;

		public BlitToDisplayRenderablesUpdate? blitToDisplaysUpdate;

		public LODGroupRenderablesUpdate? lodGroupUpdate;

		public GaussianSplatRenderablesUpdate? gaussianSplatRenderersUpdate;

		public List<ReflectionProbeRenderTask> reflectionProbeRenderTasks;

		public void Pack(ref MemoryPacker packer)
		{
			packer.Write(id);
			packer.Write(isActive);
			packer.Write(isOverlay);
			packer.Write(isPrivate);
			packer.Write(rootTransform);
			packer.Write(viewPositionIsExternal);
			packer.Write(overrideViewPosition);
			packer.Write(skyboxMaterialAssetId);
			packer.Write(ambientLight);
			packer.Write(overridenViewTransform);
			packer.WriteObject(transformsUpdate);
			packer.WriteObject(meshRenderersUpdate);
			packer.WriteObject(skinnedMeshRenderersUpdate);
			packer.WriteObject(lightsUpdate);
			packer.WriteObject(camerasUpdate);
			packer.WriteObject(cameraPortalsUpdate);
			packer.WriteObject(reflectionProbesUpdate);
			packer.WriteObject(reflectionProbeSH2Taks);
			packer.WriteObject(layersUpdate);
			packer.WriteObject(billboardBuffersUpdate);
			packer.WriteObject(meshRenderBuffersUpdate);
			packer.WriteObject(trailRenderersUpdate);
			packer.WriteObject(lightsBufferRenderersUpdate);
			packer.WriteObject(renderTransformOverridesUpdate);
			packer.WriteObject(renderMaterialOverridesUpdate);
			packer.WriteObject(blitToDisplaysUpdate);
			packer.WriteObject(lodGroupUpdate);
			packer.WriteObject(gaussianSplatRenderersUpdate);
			packer.WriteObjectList(reflectionProbeRenderTasks);
		}

		public void Unpack(ref MemoryUnpacker packer)
		{
			packer.Read(ref id);
			packer.Read(ref isActive);
			packer.Read(ref isOverlay);
			packer.Read(ref isPrivate);
			packer.Read(ref rootTransform);
			packer.Read(ref viewPositionIsExternal);
			packer.Read(ref overrideViewPosition);
			packer.Read(ref skyboxMaterialAssetId);
			packer.Read(ref ambientLight);
			packer.Read(ref overridenViewTransform);
			packer.ReadObject(ref transformsUpdate);
			packer.ReadObject(ref meshRenderersUpdate);
			packer.ReadObject(ref skinnedMeshRenderersUpdate);
			packer.ReadObject(ref lightsUpdate);
			packer.ReadObject(ref camerasUpdate);
			packer.ReadObject(ref cameraPortalsUpdate);
			packer.ReadObject(ref reflectionProbesUpdate);
			packer.ReadObject(ref reflectionProbeSH2Taks);
			packer.ReadObject(ref layersUpdate);
			packer.ReadObject(ref billboardBuffersUpdate);
			packer.ReadObject(ref meshRenderBuffersUpdate);
			packer.ReadObject(ref trailRenderersUpdate);
			packer.ReadObject(ref lightsBufferRenderersUpdate);
			packer.ReadObject(ref renderTransformOverridesUpdate);
			packer.ReadObject(ref renderMaterialOverridesUpdate);
			packer.ReadObject(ref blitToDisplaysUpdate);
			packer.ReadObject(ref lodGroupUpdate);
			packer.ReadObject(ref gaussianSplatRenderersUpdate);
			packer.ReadObjectList(ref reflectionProbeRenderTasks);
		}
	}
	public class FreeSharedMemoryView : RendererCommand
	{
		public int bufferId;

		public override void Pack(ref MemoryPacker packer)
		{
			packer.Write(bufferId);
		}

		public override void Unpack(ref MemoryUnpacker unpacker)
		{
			unpacker.Read(ref bufferId);
		}
	}
	public class ViveHandState : IMemoryPackable
	{
		public float confidence;

		public RenderVector3 position;

		public RenderQuaternion rotation;

		public float pinchStrength;

		public List<RenderVector3> points = new List<RenderVector3>();

		public void Pack(ref MemoryPacker packer)
		{
			packer.Write(confidence);
			packer.Write(position);
			packer.Write(rotation);
			packer.Write(pinchStrength);
			packer.WriteValueList(points);
		}

		public void Unpack(ref MemoryUnpacker unpacker)
		{
			unpacker.Read(ref confidence);
			unpacker.Read(ref position);
			unpacker.Read(ref rotation);
			unpacker.Read(ref pinchStrength);
			unpacker.ReadValueList(ref points);
		}
	}
	public class ViveHandTrackingInputState : IMemoryPackable
	{
		public bool isTracking;

		public ViveHandState left;

		public ViveHandState right;

		public void Pack(ref MemoryPacker packer)
		{
			packer.Write(isTracking);
			if (isTracking)
			{
				packer.WriteObject(left);
				packer.WriteObject(right);
			}
		}

		public void Unpack(ref MemoryUnpacker unpacker)
		{
			unpacker.Read(ref isTracking);
			if (isTracking)
			{
				unpacker.ReadObject(ref left);
				unpacker.ReadObject(ref right);
			}
		}
	}
	public class DisplayState : IMemoryPackable
	{
		public int displayIndex;

		public RenderVector2i resolution;

		public RenderVector2i offset;

		public double refreshRate;

		public RectOrientation orientation;

		public RenderVector2 dpi;

		public bool isPrimary;

		public void Pack(ref MemoryPacker packer)
		{
			packer.Write(displayIndex);
			packer.Write(resolution);
			packer.Write(offset);
			packer.Write(refreshRate);
			packer.Write(orientation);
			packer.Write(dpi);
			packer.Write(isPrimary);
		}

		public void Unpack(ref MemoryUnpacker unpacker)
		{
			unpacker.Read(ref displayIndex);
			unpacker.Read(ref resolution);
			unpacker.Read(ref offset);
			unpacker.Read(ref refreshRate);
			unpacker.Read(ref orientation);
			unpacker.Read(ref dpi);
			unpacker.Read(ref isPrimary);
		}
	}
	public class DragAndDropEvent : IMemoryPackable
	{
		public List<string> paths;

		public RenderVector2i dropPoint;

		public void Pack(ref MemoryPacker packer)
		{
			packer.WriteStringList(paths);
			packer.Write(dropPoint);
		}

		public void Unpack(ref MemoryUnpacker unpacker)
		{
			unpacker.ReadStringList(ref paths);
			unpacker.Read(ref dropPoint);
		}
	}
	public class GamepadState : IMemoryPackable
	{
		public string displayName;

		public RenderVector2 leftThumbstick;

		public RenderVector2 rightThumbstick;

		public RenderVector2 dPad;

		public float leftTrigger;

		public float rightTrigger;

		public bool leftThumbstickClick;

		public bool rightThumbstickClick;

		public bool dPadUp;

		public bool dPadRight;

		public bool dPadDown;

		public bool dPadLeft;

		public bool leftBumper;

		public bool rightBumper;

		public bool start;

		public bool menu;

		public bool a;

		public bool b;

		public bool x;

		public bool y;

		public bool paddle0;

		public bool paddle1;

		public bool paddle2;

		public bool paddle3;

		public void Pack(ref MemoryPacker packer)
		{
			packer.Write(displayName);
			packer.Write(leftThumbstick);
			packer.Write(rightThumbstick);
			packer.Write(dPad);
			packer.Write(leftTrigger);
			packer.Write(rightTrigger);
			packer.Write(leftThumbstickClick, rightThumbstickClick, dPadUp, dPadRight, dPadDown, dPadLeft, leftBumper, rightBumper);
			packer.Write(start, menu);
			packer.Write(a, b, x, y, paddle0, paddle1, paddle2, paddle3);
		}

		public void Unpack(ref MemoryUnpacker unpacker)
		{
			unpacker.Read(ref displayName);
			unpacker.Read(ref leftThumbstick);
			unpacker.Read(ref rightThumbstick);
			unpacker.Read(ref dPad);
			unpacker.Read(ref leftTrigger);
			unpacker.Read(ref rightTrigger);
			unpacker.Read(out leftThumbstickClick, out rightThumbstickClick, out dPadUp, out dPadRight, out dPadDown, out dPadLeft, out leftBumper, out rightBumper);
			unpacker.Read(out start, out menu);
			unpacker.Read(out a, out b, out x, out y, out paddle0, out paddle1, out paddle2, out paddle3);
		}
	}
	public class HandState : IMemoryPackable
	{
		public string uniqueId;

		public int priority;

		public Chirality chirality;

		public bool isDeviceActive;

		public bool isTracking;

		public bool tracksMetacarpals;

		public float confidence;

		public RenderVector3 wristPosition;

		public RenderQuaternion wristRotation;

		public List<RenderVector3> segmentPositions;

		public List<RenderQuaternion> segmentRotations;

		public void Pack(ref MemoryPacker packer)
		{
			packer.Write(uniqueId);
			packer.Write(priority);
			packer.Write(chirality);
			packer.Write(isDeviceActive, isTracking, tracksMetacarpals);
			packer.Write(confidence);
			if (isTracking)
			{
				packer.Write(wristPosition);
				packer.Write(wristRotation);
				packer.WriteValueList(segmentPositions);
				packer.WriteValueList(segmentRotations);
			}
		}

		public void Unpack(ref MemoryUnpacker unpacker)
		{
			unpacker.Read(ref uniqueId);
			unpacker.Read(ref priority);
			unpacker.Read(ref chirality);
			unpacker.Read(out isDeviceActive, out isTracking, out tracksMetacarpals);
			unpacker.Read(ref confidence);
			if (isTracking)
			{
				unpacker.Read(ref wristPosition);
				unpacker.Read(ref wristRotation);
				unpacker.ReadValueList(ref segmentPositions);
				unpacker.ReadValueList(ref segmentRotations);
			}
		}
	}
	public class InputState : IMemoryPackable
	{
		public MouseState mouse = new MouseState();

		public KeyboardState keyboard = new KeyboardState();

		public WindowState window = new WindowState();

		public VR_InputsState vr = new VR_InputsState();

		public List<GamepadState> gamepads;

		public List<TouchState> touches;

		public List<DisplayState> displays;

		public void Pack(ref MemoryPacker packer)
		{
			packer.WriteObject(mouse);
			packer.WriteObject(keyboard);
			packer.WriteObject(window);
			packer.WriteObject(vr);
			packer.WriteObjectList(gamepads);
			packer.WriteObjectList(touches);
			packer.WriteObjectList(displays);
		}

		public void Unpack(ref MemoryUnpacker packer)
		{
			packer.ReadObject(ref mouse);
			packer.ReadObject(ref keyboard);
			packer.ReadObject(ref window);
			packer.ReadObject(ref vr);
			packer.ReadObjectList(ref gamepads);
			packer.ReadObjectList(ref touches);
			packer.ReadObjectList(ref displays);
		}
	}
	public class KeyboardState : IMemoryPackable
	{
		public string typeDelta;

		public HashSet<Key> heldKeys = new HashSet<Key>();

		public void Pack(ref MemoryPacker packer)
		{
			packer.Write(typeDelta);
			packer.WriteValueList(heldKeys);
		}

		public void Unpack(ref MemoryUnpacker packer)
		{
			packer.Read(ref typeDelta);
			packer.ReadValueList(ref heldKeys);
		}
	}
	public class MouseState : IMemoryPackable
	{
		public bool isActive;

		public bool leftButtonState;

		public bool rightButtonState;

		public bool middleButtonState;

		public bool button4State;

		public bool button5State;

		public RenderVector2 desktopPosition;

		public RenderVector2 windowPosition;

		public RenderVector2 directDelta;

		public RenderVector2 scrollWheelDelta;

		public void Pack(ref MemoryPacker packer)
		{
			packer.Write(isActive, leftButtonState, rightButtonState, middleButtonState, button4State, button5State);
			packer.Write(desktopPosition);
			packer.Write(windowPosition);
			packer.Write(directDelta);
			packer.Write(scrollWheelDelta);
		}

		public void Unpack(ref MemoryUnpacker packer)
		{
			packer.Read(out isActive, out leftButtonState, out rightButtonState, out middleButtonState, out button4State, out button5State);
			packer.Read(ref desktopPosition);
			packer.Read(ref windowPosition);
			packer.Read(ref directDelta);
			packer.Read(ref scrollWheelDelta);
		}
	}
	public class TouchState : IMemoryPackable
	{
		public int touchId;

		public RenderVector2 position;

		public bool isPressing;

		public float pressure;

		public void Pack(ref MemoryPacker packer)
		{
			packer.Write(touchId);
			packer.Write(position);
			packer.Write(isPressing);
			packer.Write(pressure);
		}

		public void Unpack(ref MemoryUnpacker unpacker)
		{
			unpacker.Read(ref touchId);
			unpacker.Read(ref position);
			unpacker.Read(ref isPressing);
			unpacker.Read(ref pressure);
		}
	}
	public class CosmosControllerState : VR_ControllerState
	{
		public bool joystickTouch;

		public bool joystickClick;

		public RenderVector2 joystickRaw;

		public bool triggerTouch;

		public bool triggerClick;

		public float trigger;

		public bool gripClick;

		public bool vive;

		public bool buttonAX;

		public bool buttonBY;

		public bool bumper;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(joystickTouch, joystickClick, triggerTouch, triggerClick, gripClick, vive, buttonAX, buttonBY);
			packer.Write(joystickRaw);
			packer.Write(trigger);
			packer.Write(bumper);
		}

		public override void Unpack(ref MemoryUnpacker unpacker)
		{
			base.Unpack(ref unpacker);
			unpacker.Read(out joystickTouch, out joystickClick, out triggerTouch, out triggerClick, out gripClick, out vive, out buttonAX, out buttonBY);
			unpacker.Read(ref joystickRaw);
			unpacker.Read(ref trigger);
			unpacker.Read(ref bumper);
		}
	}
	public class GenericControllerState : VR_ControllerState
	{
		public float strength;

		public RenderVector2 axis;

		public bool touchingStrength;

		public bool touchingAxis;

		public bool primary;

		public bool menu;

		public bool grab;

		public bool secondary;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(strength);
			packer.Write(axis);
			packer.Write(touchingStrength, touchingAxis, primary, menu, grab, secondary);
		}

		public override void Unpack(ref MemoryUnpacker unpacker)
		{
			base.Unpack(ref unpacker);
			unpacker.Read(ref strength);
			unpacker.Read(ref axis);
			unpacker.Read(out touchingStrength, out touchingAxis, out primary, out menu, out grab, out secondary);
		}
	}
	public class HP_ReverbControllerState : VR_ControllerState
	{
		public bool appMenu;

		public bool buttonYB;

		public bool buttonXA;

		public bool gripTouch;

		public bool gripClick;

		public float grip;

		public bool joystickClick;

		public RenderVector2 joystickRaw;

		public bool triggerHair;

		public bool triggerClick;

		public float trigger;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(appMenu, buttonYB, buttonXA, gripTouch, gripClick, joystickClick, triggerHair, triggerClick);
			packer.Write(grip);
			packer.Write(joystickRaw);
			packer.Write(trigger);
		}

		public override void Unpack(ref MemoryUnpacker unpacker)
		{
			base.Unpack(ref unpacker);
			unpacker.Read(out appMenu, out buttonYB, out buttonXA, out gripTouch, out gripClick, out joystickClick, out triggerHair, out triggerClick);
			unpacker.Read(ref grip);
			unpacker.Read(ref joystickRaw);
			unpacker.Read(ref trigger);
		}
	}
	public class IndexControllerState : VR_ControllerState
	{
		public float grip;

		public bool gripTouch;

		public bool gripClick;

		public bool buttonA;

		public bool buttonB;

		public bool buttonAtouch;

		public bool buttonBtouch;

		public float trigger;

		public bool triggerTouch;

		public bool triggerClick;

		public RenderVector2 joystickRaw;

		public bool joystickTouch;

		public bool joystickClick;

		public RenderVector2 touchpad;

		public bool touchpadTouch;

		public bool touchpadPress;

		public float touchpadForce;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(gripTouch, gripClick, buttonA, buttonB, buttonAtouch, buttonBtouch, triggerTouch, triggerClick);
			packer.Write(grip);
			packer.Write(trigger);
			packer.Write(joystickTouch, joystickClick, touchpadTouch, touchpadPress);
			packer.Write(joystickRaw);
			packer.Write(touchpad);
			packer.Write(touchpadForce);
		}

		public override void Unpack(ref MemoryUnpacker unpacker)
		{
			base.Unpack(ref unpacker);
			unpacker.Read(out gripTouch, out gripClick, out buttonA, out buttonB, out buttonAtouch, out buttonBtouch, out triggerTouch, out triggerClick);
			unpacker.Read(ref grip);
			unpacker.Read(ref trigger);
			unpacker.Read(out joystickTouch, out joystickClick, out touchpadTouch, out touchpadPress);
			unpacker.Read(ref joystickRaw);
			unpacker.Read(ref touchpad);
			unpacker.Read(ref touchpadForce);
		}
	}
	public class PicoNeo2ControllerState : VR_ControllerState
	{
		public bool app;

		public bool pico;

		public bool buttonYB;

		public bool buttonXA;

		public bool gripClick;

		public bool joystickTouch;

		public bool joystickClick;

		public RenderVector2 joystick;

		public bool triggerClick;

		public float trigger;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(app, pico, buttonYB, buttonXA, gripClick, joystickTouch, joystickClick, triggerClick);
			packer.Write(joystick);
			packer.Write(trigger);
		}

		public override void Unpack(ref MemoryUnpacker unpacker)
		{
			base.Unpack(ref unpacker);
			unpacker.Read(out app, out pico, out buttonYB, out buttonXA, out gripClick, out joystickTouch, out joystickClick, out triggerClick);
			unpacker.Read(ref joystick);
			unpacker.Read(ref trigger);
		}
	}
	[DataModelType]
	[OldTypeName("FrooxEngine.TouchController+Model", "FrooxEngine")]
	public enum TouchControllerModel : sbyte
	{
		CV1,
		QuestAndRiftS
	}
	public class TouchControllerState : VR_ControllerState
	{
		public TouchControllerModel model;

		public bool start;

		public bool buttonYB;

		public bool buttonXA;

		public bool buttonYB_touch;

		public bool buttonXA_touch;

		public bool thumbrestTouch;

		public float grip;

		public bool gripClick;

		public RenderVector2 joystickRaw;

		public bool joystickTouch;

		public bool joystickClick;

		public float trigger;

		public bool triggerTouch;

		public bool triggerClick;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(model);
			packer.Write(start, buttonYB, buttonXA, buttonYB_touch, buttonXA_touch, thumbrestTouch);
			packer.Write(gripClick, joystickTouch, joystickClick, triggerTouch, triggerClick);
			packer.Write(grip);
			packer.Write(joystickRaw);
			packer.Write(trigger);
		}

		public override void Unpack(ref MemoryUnpacker unpacker)
		{
			base.Unpack(ref unpacker);
			unpacker.Read(ref model);
			unpacker.Read(out start, out buttonYB, out buttonXA, out buttonYB_touch, out buttonXA_touch, out thumbrestTouch);
			unpacker.Read(out gripClick, out joystickTouch, out joystickClick, out triggerTouch, out triggerClick);
			unpacker.Read(ref grip);
			unpacker.Read(ref joystickRaw);
			unpacker.Read(ref trigger);
		}
	}
	public class ViveControllerState : VR_ControllerState
	{
		public bool grip;

		public bool app;

		public bool triggerHair;

		public bool triggerClick;

		public float trigger;

		public bool touchpadTouch;

		public bool touchpadClick;

		public RenderVector2 touchpad;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(grip, app, triggerHair, triggerClick, touchpadTouch, touchpadClick);
			packer.Write(trigger);
			packer.Write(touchpad);
		}

		public override void Unpack(ref MemoryUnpacker unpacker)
		{
			base.Unpack(ref unpacker);
			unpacker.Read(out grip, out app, out triggerHair, out triggerClick, out touchpadTouch, out touchpadClick);
			unpacker.Read(ref trigger);
			unpacker.Read(ref touchpad);
		}
	}
	public abstract class VR_ControllerState : PolymorphicMemoryPackableEntity<VR_ControllerState>, ITrackedDevice
	{
		public string deviceID;

		public string deviceModel;

		public Chirality side;

		public BodyNode bodyNode;

		public bool isDeviceActive;

		public bool isTracking;

		public RenderVector3 position;

		public RenderQuaternion rotation;

		public bool hasBoundHand;

		public RenderVector3 handPosition;

		public RenderQuaternion handRotation;

		public float batteryLevel;

		public bool batteryCharging;

		bool ITrackedDevice.IsTracking
		{
			get
			{
				return isTracking;
			}
			set
			{
				isTracking = value;
			}
		}

		RenderVector3 ITrackedDevice.Position
		{
			get
			{
				return position;
			}
			set
			{
				position = value;
			}
		}

		RenderQuaternion ITrackedDevice.Rotation
		{
			get
			{
				return rotation;
			}
			set
			{
				rotation = value;
			}
		}

		public override void Pack(ref MemoryPacker packer)
		{
			packer.Write(deviceID);
			packer.Write(deviceModel);
			packer.Write(side);
			packer.Write(bodyNode);
			packer.Write(isDeviceActive, isTracking, hasBoundHand);
			if (isTracking)
			{
				packer.Write(position);
				packer.Write(rotation);
				if (hasBoundHand)
				{
					packer.Write(handPosition);
					packer.Write(handRotation);
				}
				packer.Write(batteryLevel);
				packer.Write(batteryCharging);
			}
		}

		public override void Unpack(ref MemoryUnpacker unpacker)
		{
			unpacker.Read(ref deviceID);
			unpacker.Read(ref deviceModel);
			unpacker.Read(ref side);
			unpacker.Read(ref bodyNode);
			unpacker.Read(out isDeviceActive, out isTracking, out hasBoundHand);
			if (isTracking)
			{
				unpacker.Read(ref position);
				unpacker.Read(ref rotation);
				if (hasBoundHand)
				{
					unpacker.Read(ref handPosition);
					unpacker.Read(ref handRotation);
				}
				unpacker.Read(ref batteryLevel);
				unpacker.Read(ref batteryCharging);
			}
		}

		static VR_ControllerState()
		{
			PolymorphicMemoryPackableEntity<VR_ControllerState>.InitTypes(new List<Type>
			{
				typeof(CosmosControllerState),
				typeof(GenericControllerState),
				typeof(HP_ReverbControllerState),
				typeof(IndexControllerState),
				typeof(PicoNeo2ControllerState),
				typeof(TouchControllerState),
				typeof(ViveControllerState),
				typeof(WindowsMR_ControllerState)
			});
		}
	}
	public class WindowsMR_ControllerState : VR_ControllerState
	{
		public bool grip;

		public bool app;

		public bool triggerHair;

		public bool triggerClick;

		public float trigger;

		public bool touchpadTouch;

		public bool touchpadClick;

		public RenderVector2 touchpad;

		public bool joystickClick;

		public RenderVector2 joystickRaw;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(grip, app, triggerHair, triggerClick, touchpadTouch, touchpadClick, joystickClick);
			packer.Write(trigger);
			packer.Write(touchpad);
			packer.Write(joystickRaw);
		}

		public override void Unpack(ref MemoryUnpacker unpacker)
		{
			base.Unpack(ref unpacker);
			unpacker.Read(out grip, out app, out triggerHair, out triggerClick, out touchpadTouch, out touchpadClick, out joystickClick);
			unpacker.Read(ref trigger);
			unpacker.Read(ref touchpad);
			unpacker.Read(ref joystickRaw);
		}
	}
	public enum HeadsetConnection
	{
		Wired,
		WirelessGeneral,
		WirelessSteamLink
	}
	public class HeadsetState : IMemoryPackable, ITrackedDevice
	{
		public bool isTracking;

		public RenderVector3 position;

		public RenderQuaternion rotation;

		public float batteryLevel;

		public bool batteryCharging;

		public HeadsetConnection connectionType;

		public string headsetManufacturer;

		public string headsetModel;

		bool ITrackedDevice.IsTracking
		{
			get
			{
				return isTracking;
			}
			set
			{
				isTracking = value;
			}
		}

		RenderVector3 ITrackedDevice.Position
		{
			get
			{
				return position;
			}
			set
			{
				position = value;
			}
		}

		RenderQuaternion ITrackedDevice.Rotation
		{
			get
			{
				return rotation;
			}
			set
			{
				rotation = value;
			}
		}

		public void Pack(ref MemoryPacker packer)
		{
			packer.Write(isTracking, batteryCharging);
			if (isTracking)
			{
				packer.Write(position);
				packer.Write(rotation);
				packer.Write(batteryLevel);
			}
			packer.Write(connectionType);
			packer.Write(headsetManufacturer);
			packer.Write(headsetModel);
		}

		public void Unpack(ref MemoryUnpacker unpacker)
		{
			unpacker.Read(out isTracking, out batteryCharging);
			if (isTracking)
			{
				unpacker.Read(ref position);
				unpacker.Read(ref rotation);
				unpacker.Read(ref batteryLevel);
			}
			unpacker.Read(ref connectionType);
			unpacker.Read(ref headsetManufacturer);
			unpacker.Read(ref headsetModel);
		}
	}
	public interface ITrackedDevice
	{
		bool IsTracking { get; set; }

		RenderVector3 Position { get; set; }

		RenderQuaternion Rotation { get; set; }
	}
	public class TrackerState : IMemoryPackable, ITrackedDevice
	{
		public string uniqueId;

		public bool isTracking;

		public RenderVector3 position;

		public RenderQuaternion rotation;

		public float batteryLevel;

		public bool batteryCharging;

		bool ITrackedDevice.IsTracking
		{
			get
			{
				return isTracking;
			}
			set
			{
				isTracking = value;
			}
		}

		RenderVector3 ITrackedDevice.Position
		{
			get
			{
				return position;
			}
			set
			{
				position = value;
			}
		}

		RenderQuaternion ITrackedDevice.Rotation
		{
			get
			{
				return rotation;
			}
			set
			{
				rotation = value;
			}
		}

		public void Pack(ref MemoryPacker packer)
		{
			packer.Write(uniqueId);
			packer.Write(isTracking, batteryCharging);
			if (isTracking)
			{
				packer.Write(position);
				packer.Write(rotation);
			}
			packer.Write(batteryLevel);
		}

		public void Unpack(ref MemoryUnpacker unpacker)
		{
			unpacker.Read(ref uniqueId);
			unpacker.Read(out isTracking, out batteryCharging);
			if (isTracking)
			{
				unpacker.Read(ref position);
				unpacker.Read(ref rotation);
			}
			unpacker.Read(ref batteryLevel);
		}
	}
	public class TrackingReferenceState : IMemoryPackable, ITrackedDevice
	{
		public string uniqueId;

		public bool isTracking;

		public RenderVector3 position;

		public RenderQuaternion rotation;

		bool ITrackedDevice.IsTracking
		{
			get
			{
				return isTracking;
			}
			set
			{
				isTracking = value;
			}
		}

		RenderVector3 ITrackedDevice.Position
		{
			get
			{
				return position;
			}
			set
			{
				position = value;
			}
		}

		RenderQuaternion ITrackedDevice.Rotation
		{
			get
			{
				return rotation;
			}
			set
			{
				rotation = value;
			}
		}

		public void Pack(ref MemoryPacker packer)
		{
			packer.Write(uniqueId);
			packer.Write(isTracking);
			if (isTracking)
			{
				packer.Write(position);
				packer.Write(rotation);
			}
		}

		public void Unpack(ref MemoryUnpacker unpacker)
		{
			unpacker.Read(ref uniqueId);
			unpacker.Read(ref isTracking);
			if (isTracking)
			{
				unpacker.Read(ref position);
				unpacker.Read(ref rotation);
			}
		}
	}
	public class VR_InputsState : IMemoryPackable
	{
		public bool userPresentInHeadset;

		public bool dashboardOpen;

		public HeadsetState headsetState;

		public List<VR_ControllerState> controllers;

		public List<TrackerState> trackers;

		public List<TrackingReferenceState> trackingReferences;

		public List<HandState> hands;

		public ViveHandTrackingInputState viveHandTracking;

		public void Pack(ref MemoryPacker packer)
		{
			packer.Write(userPresentInHeadset, dashboardOpen);
			packer.WriteObject(headsetState);
			packer.WritePolymorphicList(controllers);
			packer.WriteObjectList(trackers);
			packer.WriteObjectList(trackingReferences);
			packer.WriteObjectList(hands);
			packer.WriteObject(viveHandTracking);
		}

		public void Unpack(ref MemoryUnpacker unpacker)
		{
			unpacker.Read(out userPresentInHeadset, out dashboardOpen);
			unpacker.ReadObject(ref headsetState);
			unpacker.ReadPolymorphicList(ref controllers);
			unpacker.ReadObjectList(ref trackers);
			unpacker.ReadObjectList(ref trackingReferences);
			unpacker.ReadObjectList(ref hands);
			unpacker.ReadObject(ref viveHandTracking);
		}
	}
	public class WindowState : IMemoryPackable
	{
		public bool isWindowFocused;

		public bool isFullscreen;

		public RenderVector2i windowResolution;

		public bool resolutionSettingsApplied;

		public DragAndDropEvent dragAndDropEvent;

		public void Pack(ref MemoryPacker packer)
		{
			packer.Write(isWindowFocused);
			packer.Write(isFullscreen);
			packer.Write(windowResolution);
			packer.Write(resolutionSettingsApplied);
			packer.WriteObject(dragAndDropEvent);
		}

		public void Unpack(ref MemoryUnpacker packer)
		{
			packer.Read(ref isWindowFocused);
			packer.Read(ref isFullscreen);
			packer.Read(ref windowResolution);
			packer.Read(ref resolutionSettingsApplied);
			packer.ReadObject(ref dragAndDropEvent);
		}
	}
	[StructLayout(LayoutKind.Explicit, Size = 16)]
	public struct HapticPointState
	{
		[FieldOffset(0)]
		public float force;

		[FieldOffset(4)]
		public float temperature;

		[FieldOffset(8)]
		public float pain;

		[FieldOffset(12)]
		public float vibration;
	}
	public class OutputState : IMemoryPackable
	{
		public bool lockCursor;

		public RenderVector2i? lockCursorPosition;

		public bool keyboardInputActive;

		public VR_OutputState vr;

		public void Pack(ref MemoryPacker packer)
		{
			packer.Write(lockCursor);
			packer.Write(lockCursorPosition);
			packer.Write(keyboardInputActive);
			packer.WriteObject(vr);
		}

		public void Unpack(ref MemoryUnpacker packer)
		{
			packer.Read(ref lockCursor);
			packer.Read(ref lockCursorPosition);
			packer.Read(ref keyboardInputActive);
			packer.ReadObject(ref vr);
		}
	}
	public class VR_ControllerOutputState : IMemoryPackable
	{
		public Chirality side;

		public double vibrateTime;

		public HapticPointState hapticState;

		public void Pack(ref MemoryPacker packer)
		{
			packer.Write(side);
			packer.Write(vibrateTime);
			packer.Write(hapticState);
		}

		public void Unpack(ref MemoryUnpacker unpacker)
		{
			unpacker.Read(ref side);
			unpacker.Read(ref vibrateTime);
			unpacker.Read(ref hapticState);
		}
	}
	public class VR_OutputState : IMemoryPackable
	{
		public VR_ControllerOutputState leftController;

		public VR_ControllerOutputState rightController;

		public bool useViveHandTracking;

		public void Pack(ref MemoryPacker packer)
		{
			packer.WriteObject(leftController);
			packer.WriteObject(rightController);
			packer.Write(useViveHandTracking);
		}

		public void Unpack(ref MemoryUnpacker unpacker)
		{
			unpacker.ReadObject(ref leftController);
			unpacker.ReadObject(ref rightController);
			unpacker.Read(ref useViveHandTracking);
		}
	}
	public abstract class PolymorphicMemoryPackableEntity<T> : IMemoryPackable where T : PolymorphicMemoryPackableEntity<T>
	{
		private static bool initialized;

		private static List<Type> types;

		private static Dictionary<Type, int> typeToIndex;

		private static List<Func<IMemoryPackerEntityPool, T>> poolBorrowers;

		private static List<Action<IMemoryPackerEntityPool, T>> poolReturners;

		private static T Allocate<A>(IMemoryPackerEntityPool pool) where A : T, new()
		{
			return (T)(PolymorphicMemoryPackableEntity<T>)pool.Borrow<A>();
		}

		private static void Return<A>(IMemoryPackerEntityPool pool, T instance) where A : T, new()
		{
			pool.Return((A)instance);
		}

		public static void ReturnAuto(IMemoryPackerEntityPool pool, T instance)
		{
			int index = typeToIndex[instance.GetType()];
			poolReturners[index](pool, instance);
		}

		protected static void InitTypes(List<Type> types)
		{
			PolymorphicMemoryPackableEntity<T>.types = types;
			typeToIndex = new Dictionary<Type, int>();
			poolBorrowers = new List<Func<IMemoryPackerEntityPool, T>>();
			poolReturners = new List<Action<IMemoryPackerEntityPool, T>>();
			MethodInfo method = typeof(PolymorphicMemoryPackableEntity<T>).GetMethod("Allocate", BindingFlags.Static | BindingFlags.NonPublic);
			MethodInfo method2 = typeof(PolymorphicMemoryPackableEntity<T>).GetMethod("Return", BindingFlags.Static | BindingFlags.NonPublic);
			for (int i = 0; i < types.Count; i++)
			{
				typeToIndex.Add(types[i], i);
				MethodInfo methodInfo = method.MakeGenericMethod(types[i]);
				MethodInfo methodInfo2 = method2.MakeGenericMethod(types[i]);
				Func<IMemoryPackerEntityPool, T> item = (Func<IMemoryPackerEntityPool, T>)methodInfo.CreateDelegate(typeof(Func<IMemoryPackerEntityPool, T>));
				Action<IMemoryPackerEntityPool, T> item2 = (Action<IMemoryPackerEntityPool, T>)methodInfo2.CreateDelegate(typeof(Action<IMemoryPackerEntityPool, T>));
				poolBorrowers.Add(item);
				poolReturners.Add(item2);
			}
			initialized = true;
		}

		public void Encode(ref MemoryPacker packer)
		{
			int value = typeToIndex[GetType()];
			packer.Write(value);
			Pack(ref packer);
		}

		public abstract void Pack(ref MemoryPacker packer);

		public abstract void Unpack(ref MemoryUnpacker unpacker);

		public static T Decode(ref MemoryUnpacker unpacker, T existingInstance = null)
		{
			if (!initialized)
			{
				RuntimeHelpers.RunClassConstructor(typeof(T).TypeHandle);
			}
			if (poolBorrowers == null)
			{
				throw new InvalidOperationException("Types were not initialized for polymorphic entity: " + typeof(T).FullName);
			}
			if (unpacker.Pool == null)
			{
				throw new ArgumentException("MemoryUnpacker doesn't have a Pool instance for polymorphic entity: " + typeof(T).FullName);
			}
			int num = unpacker.Read<int>();
			T val;
			if (existingInstance != null && existingInstance.GetType() == types[num])
			{
				val = existingInstance;
			}
			else
			{
				if (existingInstance != null)
				{
					poolReturners[num](unpacker.Pool, existingInstance);
				}
				val = (poolBorrowers[num] ?? throw new Exception($"Borrow is null for type with index {num} for polymorphic entity: {typeof(T).FullName}"))(unpacker.Pool);
			}
			if (val == null)
			{
				throw new Exception($"Failed to allocate instance with index {num} for polymorphic entity: {typeof(T).FullName}");
			}
			val.Unpack(ref unpacker);
			return val;
		}
	}
	[StructLayout(LayoutKind.Explicit, Size = 8)]
	public struct RenderableHandle
	{
		[FieldOffset(0)]
		public int renderSpaceId;

		[FieldOffset(4)]
		public int renderableIndex;

		public RenderableHandle(int renderSpaceId, int renderableIndex)
		{
			this.renderSpaceId = renderSpaceId;
			this.renderableIndex = renderableIndex;
		}
	}
	public abstract class RendererCommand : PolymorphicMemoryPackableEntity<RendererCommand>
	{
		static RendererCommand()
		{
			PolymorphicMemoryPackableEntity<RendererCommand>.InitTypes(new List<Type>
			{
				typeof(RendererInitData),
				typeof(RendererInitResult),
				typeof(RendererInitProgressUpdate),
				typeof(RendererInitFinalizeData),
				typeof(RendererEngineReady),
				typeof(RendererShutdownRequest),
				typeof(RendererShutdown),
				typeof(KeepAlive),
				typeof(RendererParentWindow),
				typeof(FreeSharedMemoryView),
				typeof(SetWindowIcon),
				typeof(SetWindowIconResult),
				typeof(SetTaskbarProgress),
				typeof(FrameStartData),
				typeof(FrameSubmitData),
				typeof(PostProcessingConfig),
				typeof(QualityConfig),
				typeof(ResolutionConfig),
				typeof(DesktopConfig),
				typeof(GaussianSplatConfig),
				typeof(RenderDecouplingConfig),
				typeof(MeshUploadData),
				typeof(MeshUnload),
				typeof(MeshUploadResult),
				typeof(ShaderUpload),
				typeof(ShaderUnload),
				typeof(ShaderUploadResult),
				typeof(MaterialPropertyIdRequest),
				typeof(MaterialPropertyIdResult),
				typeof(MaterialsUpdateBatch),
				typeof(MaterialsUpdateBatchResult),
				typeof(UnloadMaterial),
				typeof(UnloadMaterialPropertyBlock),
				typeof(SetTexture2DFormat),
				typeof(SetTexture2DProperties),
				typeof(SetTexture2DData),
				typeof(SetTexture2DResult),
				typeof(UnloadTexture2D),
				typeof(SetTexture3DFormat),
				typeof(SetTexture3DProperties),
				typeof(SetTexture3DData),
				typeof(SetTexture3DResult),
				typeof(UnloadTexture3D),
				typeof(SetCubemapFormat),
				typeof(SetCubemapProperties),
				typeof(SetCubemapData),
				typeof(SetCubemapResult),
				typeof(UnloadCubemap),
				typeof(SetRenderTextureFormat),
				typeof(RenderTextureResult),
				typeof(UnloadRenderTexture),
				typeof(SetDesktopTextureProperties),
				typeof(DesktopTexturePropertiesUpdate),
				typeof(UnloadDesktopTexture),
				typeof(PointRenderBufferUpload),
				typeof(PointRenderBufferConsumed),
				typeof(PointRenderBufferUnload),
				typeof(TrailRenderBufferUpload),
				typeof(TrailRenderBufferConsumed),
				typeof(TrailRenderBufferUnload),
				typeof(GaussianSplatUploadRaw),
				typeof(GaussianSplatUploadEncoded),
				typeof(GaussianSplatResult),
				typeof(UnloadGaussianSplat),
				typeof(LightsBufferRendererSubmission),
				typeof(LightsBufferRendererConsumed),
				typeof(ReflectionProbeRenderResult),
				typeof(VideoTextureLoad),
				typeof(VideoTextureUpdate),
				typeof(VideoTextureReady),
				typeof(VideoTextureChanged),
				typeof(VideoTextureProperties),
				typeof(VideoTextureStartAudioTrack),
				typeof(UnloadVideoTexture)
			});
		}
	}
	[StructLayout(LayoutKind.Explicit, Size = 24)]
	public struct BillboardRenderBufferState
	{
		[FieldOffset(0)]
		public int renderableIndex;

		[FieldOffset(4)]
		public int pointRenderBufferAssetId;

		[FieldOffset(8)]
		public int materialAssetId;

		[FieldOffset(12)]
		public float minBillboardScreenSize;

		[FieldOffset(16)]
		public float maxBillboardScreenSize;

		[FieldOffset(20)]
		public BillboardAlignment alignment;

		[FieldOffset(21)]
		public MotionVectorMode motionVectorMode;
	}
	public class BillboardRenderBufferUpdate : RenderablesStateUpdate<BillboardRenderBufferState>
	{
	}
	public class BlitToDisplayRenderablesUpdate : RenderablesStateUpdate<BlitToDisplayState>
	{
	}
	[StructLayout(LayoutKind.Explicit, Size = 28)]
	public struct BlitToDisplayState
	{
		[FieldOffset(0)]
		public int renderableIndex;

		[FieldOffset(4)]
		public int textureId;

		[FieldOffset(8)]
		public RenderVector4 backgroundColor;

		[FieldOffset(24)]
		public short displayIndex;

		[FieldOffset(26)]
		private byte _flags;

		public bool flipHorizontally
		{
			get
			{
				return _flags.HasFlag(0);
			}
			set
			{
				_flags.SetFlag(0, value);
			}
		}

		public bool flipVertically
		{
			get
			{
				return _flags.HasFlag(1);
			}
			set
			{
				_flags.SetFlag(1, value);
			}
		}
	}
	public class CameraPortalsRenderablesUpdate : RenderablesStateUpdate<CameraPortalState>
	{
	}
	[StructLayout(LayoutKind.Explicit, Size = 128)]
	public struct CameraPortalState
	{
		[FieldOffset(0)]
		public int renderableIndex;

		[FieldOffset(4)]
		public int meshRendererIndex;

		[FieldOffset(8)]
		public RenderVector3 planeNormal;

		[FieldOffset(20)]
		public float planeOffset;

		[FieldOffset(24)]
		public int renderTextureId;

		[FieldOffset(28)]
		public RenderMatrix4x4 portalTransform;

		[FieldOffset(92)]
		public RenderVector3 portalPlanePosition;

		[FieldOffset(104)]
		public RenderVector3 portalPlaneNormal;

		[FieldOffset(116)]
		private float overrideFarClipValue;

		[FieldOffset(120)]
		private CameraClearMode overrideClearFlagValue;

		[FieldOffset(124)]
		private int flags;

		public float? overrideFarClip
		{
			get
			{
				if (!HasFarClipValue)
				{
					return null;
				}
				return overrideFarClipValue;
			}
			set
			{
				HasFarClipValue = value.HasValue;
				overrideFarClipValue = value.GetValueOrDefault();
			}
		}

		public CameraClearMode? overrideClearFlag
		{
			get
			{
				if (!HasCameraClearMode)
				{
					return null;
				}
				return overrideClearFlagValue;
			}
			set
			{
				HasCameraClearMode = value.HasValue;
				overrideClearFlagValue = value.GetValueOrDefault();
			}
		}

		public bool HasFarClipValue
		{
			get
			{
				return flags.HasFlag(0);
			}
			set
			{
				flags.SetFlag(0, value);
			}
		}

		public bool HasCameraClearMode
		{
			get
			{
				return flags.HasFlag(1);
			}
			set
			{
				flags.SetFlag(1, value);
			}
		}

		public bool disablePerPixelLights
		{
			get
			{
				return flags.HasFlag(2);
			}
			set
			{
				flags.SetFlag(2, value);
			}
		}

		public bool disableShadows
		{
			get
			{
				return flags.HasFlag(3);
			}
			set
			{
				flags.SetFlag(3, value);
			}
		}

		public bool portalMode
		{
			get
			{
				return flags.HasFlag(4);
			}
			set
			{
				flags.SetFlag(4, value);
			}
		}
	}
	[DataModelType]
	[OldTypeName("FrooxEngine.CameraClearMode", "FrooxEngine")]
	public enum CameraClearMode : byte
	{
		Skybox,
		Color,
		Depth,
		Nothing
	}
	[DataModelType]
	[OldTypeName("FrooxEngine.CameraProjection", "FrooxEngine")]
	[OldTypeName("ElementsCore.CameraProjection", "Elements.Core")]
	public enum CameraProjection : byte
	{
		Perspective,
		Orthographic,
		Panoramic
	}
	public class CameraRenderablesUpdate : RenderablesStateUpdate<CameraState>
	{
		public SharedMemoryBufferDescriptor<int> transformIds;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(transformIds);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref transformIds);
		}
	}
	[StructLayout(LayoutKind.Explicit, Size = 72)]
	public struct CameraState
	{
		[FieldOffset(0)]
		public int renderableIndex;

		[FieldOffset(4)]
		public float fieldOfView;

		[FieldOffset(8)]
		public float orthographicSize;

		[FieldOffset(12)]
		public float nearClip;

		[FieldOffset(16)]
		public float farClip;

		[FieldOffset(20)]
		public RenderVector4 backgroundColor;

		[FieldOffset(36)]
		public RenderRect viewport;

		[FieldOffset(52)]
		public float depth;

		[FieldOffset(56)]
		public int renderTextureAssetId;

		[FieldOffset(60)]
		public int selectiveRenderCount;

		[FieldOffset(64)]
		public int excludeRenderCount;

		[FieldOffset(68)]
		public CameraClearMode clearMode;

		[FieldOffset(69)]
		public CameraProjection projection;

		[FieldOffset(70)]
		private ushort flags;

		public bool enabled
		{
			get
			{
				return flags.HasFlag(0);
			}
			set
			{
				flags.SetFlag(0, value);
			}
		}

		public bool useTransformScale
		{
			get
			{
				return flags.HasFlag(1);
			}
			set
			{
				flags.SetFlag(1, value);
			}
		}

		public bool doubleBuffered
		{
			get
			{
				return flags.HasFlag(2);
			}
			set
			{
				flags.SetFlag(2, value);
			}
		}

		public bool renderPrivateUI
		{
			get
			{
				return flags.HasFlag(3);
			}
			set
			{
				flags.SetFlag(3, value);
			}
		}

		public bool forwardOnly
		{
			get
			{
				return flags.HasFlag(4);
			}
			set
			{
				flags.SetFlag(4, value);
			}
		}

		public bool renderShadows
		{
			get
			{
				return flags.HasFlag(5);
			}
			set
			{
				flags.SetFlag(5, value);
			}
		}

		public bool postprocessing
		{
			get
			{
				return flags.HasFlag(6);
			}
			set
			{
				flags.SetFlag(6, value);
			}
		}

		public bool screenSpaceReflections
		{
			get
			{
				return flags.HasFlag(7);
			}
			set
			{
				flags.SetFlag(7, value);
			}
		}

		public bool motionBlur
		{
			get
			{
				return flags.HasFlag(8);
			}
			set
			{
				flags.SetFlag(8, value);
			}
		}
	}
	public interface IRenderContextOverrideState
	{
		RenderingContext Context { get; set; }
	}
	public struct MaterialOverrideState
	{
		public int materialSlotIndex;

		public int materialAssetId;
	}
	public abstract class RenderContextOverridesUpdate<TState> : RenderablesStateUpdate<TState> where TState : unmanaged, IRenderContextOverrideState
	{
	}
	[StructLayout(LayoutKind.Explicit, Size = 12)]
	public struct RenderMaterialOverrideState : IRenderContextOverrideState
	{
		[FieldOffset(0)]
		public int renderableIndex;

		[FieldOffset(4)]
		public int packedMeshRendererIndex;

		[FieldOffset(8)]
		public short materrialOverrideCount;

		[FieldOffset(10)]
		public RenderingContext context;

		RenderingContext IRenderContextOverrideState.Context
		{
			get
			{
				return context;
			}
			set
			{
				context = value;
			}
		}
	}
	public class RenderMaterialOverridesUpdate : RenderContextOverridesUpdate<RenderMaterialOverrideState>
	{
		public SharedMemoryBufferDescriptor<MaterialOverrideState> materialOverrideStates;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(materialOverrideStates);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref materialOverrideStates);
		}
	}
	[StructLayout(LayoutKind.Explicit, Size = 56)]
	public struct RenderTransformOverrideState : IRenderContextOverrideState
	{
		[FieldOffset(0)]
		public int renderableIndex;

		[FieldOffset(4)]
		public RenderVector3 positionOverride;

		[FieldOffset(20)]
		public RenderQuaternion rotationOverride;

		[FieldOffset(36)]
		public RenderVector3 scaleOverride;

		[FieldOffset(48)]
		public int skinnedMeshRendererCount;

		[FieldOffset(52)]
		public RenderingContext context;

		[FieldOffset(53)]
		public byte overrideFlags;

		RenderingContext IRenderContextOverrideState.Context
		{
			get
			{
				return context;
			}
			set
			{
				context = value;
			}
		}

		public RenderVector3? PositionOverride
		{
			get
			{
				if ((overrideFlags & 1) == 0)
				{
					return null;
				}
				return positionOverride;
			}
			set
			{
				if (!value.HasValue)
				{
					overrideFlags = (byte)(overrideFlags & -2);
					return;
				}
				overrideFlags |= 1;
				positionOverride = value.Value;
			}
		}

		public RenderQuaternion? RotationOverride
		{
			get
			{
				if ((overrideFlags & 2) == 0)
				{
					return null;
				}
				return rotationOverride;
			}
			set
			{
				if (!value.HasValue)
				{
					overrideFlags = (byte)(overrideFlags & -3);
					return;
				}
				overrideFlags |= 2;
				rotationOverride = value.Value;
			}
		}

		public RenderVector3? ScaleOverride
		{
			get
			{
				if ((overrideFlags & 4) == 0)
				{
					return null;
				}
				return scaleOverride;
			}
			set
			{
				if (!value.HasValue)
				{
					overrideFlags = (byte)(overrideFlags & -5);
					return;
				}
				overrideFlags |= 4;
				scaleOverride = value.Value;
			}
		}
	}
	public class RenderTransformOverridesUpdate : RenderContextOverridesUpdate<RenderTransformOverrideState>
	{
		public SharedMemoryBufferDescriptor<int> skinnedMeshRenderersIndexes;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(skinnedMeshRenderersIndexes);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref skinnedMeshRenderersIndexes);
		}
	}
	public class GaussianSplatRenderablesUpdate : RenderablesStateUpdate<GaussianSplatRendererState>
	{
	}
	public struct GaussianSplatRendererState
	{
		public int renderableIndex;

		public int gaussianSplatAssetId;

		public float sizeScale;

		public float opacityScale;

		public int maxSHOrder;

		public bool sphericalHamornicsOnly;
	}
	public class LayerUpdate : RenderablesUpdate
	{
		public SharedMemoryBufferDescriptor<LayerType> layerAssignments;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(layerAssignments);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref layerAssignments);
		}
	}
	public class LightsBufferRendererConsumed : RendererCommand
	{
		public int globalUniqueId;

		public override void Pack(ref MemoryPacker packer)
		{
			packer.Write(globalUniqueId);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			packer.Read(ref globalUniqueId);
		}
	}
	[StructLayout(LayoutKind.Explicit, Size = 36)]
	public struct LightsBufferRendererState
	{
		[FieldOffset(0)]
		public int renderableIndex;

		[FieldOffset(4)]
		public int globalUniqueId;

		[FieldOffset(8)]
		public float shadowStrength;

		[FieldOffset(12)]
		public float shadowNearPlane;

		[FieldOffset(16)]
		public int shadowMapResolution;

		[FieldOffset(20)]
		public float shadowBias;

		[FieldOffset(24)]
		public float shadowNormalBias;

		[FieldOffset(28)]
		public int cookieTextureAssetId;

		[FieldOffset(32)]
		public LightType lightType;

		[FieldOffset(33)]
		public ShadowType shadowType;
	}
	public class LightsBufferRendererSubmission : RendererCommand
	{
		public int lightsBufferUniqueId = -1;

		public int lightsCount;

		public SharedMemoryBufferDescriptor<LightData> lights;

		public override void Pack(ref MemoryPacker packer)
		{
			packer.Write(lightsBufferUniqueId);
			packer.Write(lightsCount);
			packer.Write(lights);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			packer.Read(ref lightsBufferUniqueId);
			packer.Read(ref lightsCount);
			packer.Read(ref lights);
		}
	}
	public class LightsBufferRendererUpdate : RenderablesStateUpdate<LightsBufferRendererState>
	{
	}
	public class LightRenderablesUpdate : RenderablesStateUpdate<LightState>
	{
	}
	[StructLayout(LayoutKind.Explicit, Size = 60)]
	public struct LightState
	{
		[FieldOffset(0)]
		public int renderableIndex;

		[FieldOffset(4)]
		public float intensity;

		[FieldOffset(8)]
		public float range;

		[FieldOffset(12)]
		public float spotAngle;

		[FieldOffset(16)]
		public RenderVector4 color;

		[FieldOffset(32)]
		public float shadowStrength;

		[FieldOffset(36)]
		public float shadowNearPlane;

		[FieldOffset(40)]
		public int shadowMapResolutionOverride;

		[FieldOffset(44)]
		public float shadowBias;

		[FieldOffset(48)]
		public float shadowNormalBias;

		[FieldOffset(52)]
		public int cookieTextureAssetId;

		[FieldOffset(56)]
		public LightType type;

		[FieldOffset(57)]
		public ShadowType shadowType;
	}
	public class LODGroupRenderablesUpdate : RenderablesStateUpdate<LODGroupState>
	{
		public SharedMemoryBufferDescriptor<LODState> lodStates;

		public SharedMemoryBufferDescriptor<int> packedMeshRendererIds;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(lodStates);
			packer.Write(packedMeshRendererIds);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref lodStates);
			packer.Read(ref packedMeshRendererIds);
		}
	}
	public struct LODGroupState
	{
		public int renderableIndex;

		public int lodCount;

		public bool crossFade;

		public bool animateCrossFading;
	}
	public struct LODState
	{
		public float screenRelativeTransitionHeight;

		public float fadeTransitionWidth;

		public int rendererCount;
	}
	[StructLayout(LayoutKind.Explicit, Size = 20)]
	public struct MeshRenderBufferState
	{
		[FieldOffset(0)]
		public int renderableIndex;

		[FieldOffset(4)]
		public int pointRenderBufferAssetId;

		[FieldOffset(8)]
		public int materialAssetId;

		[FieldOffset(12)]
		public int meshAssetId;

		[FieldOffset(16)]
		public MeshAlignment alignment;
	}
	public class MeshRenderBufferUpdate : RenderablesStateUpdate<MeshRenderBufferState>
	{
	}
	public class MeshRenderablesUpdate : RenderablesUpdate
	{
		public SharedMemoryBufferDescriptor<MeshRendererState> meshStates;

		public SharedMemoryBufferDescriptor<int> meshMaterialsAndPropertyBlocks;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(meshStates);
			packer.Write(meshMaterialsAndPropertyBlocks);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref meshStates);
			packer.Read(ref meshMaterialsAndPropertyBlocks);
		}
	}
	[StructLayout(LayoutKind.Explicit, Size = 24)]
	public struct MeshRendererState
	{
		[FieldOffset(0)]
		public int renderableIndex;

		[FieldOffset(4)]
		public int meshAssetId;

		[FieldOffset(8)]
		public int materialCount;

		[FieldOffset(12)]
		public int materialPropertyBlockCount;

		[FieldOffset(16)]
		public int sortingOrder;

		[FieldOffset(20)]
		public ShadowCastMode shadowCastMode;

		[FieldOffset(21)]
		public MotionVectorMode motionVectorMode;
	}
	[StructLayout(LayoutKind.Explicit, Size = 12)]
	public struct ReflectionProbeChangeRenderResult
	{
		[FieldOffset(0)]
		public int renderSpaceId;

		[FieldOffset(4)]
		public int renderProbeUniqueId;

		[FieldOffset(8)]
		public bool requireReset;
	}
	[StructLayout(LayoutKind.Explicit, Size = 8)]
	public struct ReflectionProbeChangeRenderTask
	{
		[FieldOffset(0)]
		public int renderableIndex;

		[FieldOffset(4)]
		public int uniqueId;
	}
	public class ReflectionProbeRenderablesUpdate : RenderablesStateUpdate<ReflectionProbeState>
	{
		public SharedMemoryBufferDescriptor<ReflectionProbeChangeRenderTask> changedProbesToRender;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(changedProbesToRender);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref changedProbesToRender);
		}
	}
	[StructLayout(LayoutKind.Explicit, Size = 68)]
	public struct ReflectionProbeState
	{
		[FieldOffset(0)]
		public int renderableIndex;

		[FieldOffset(4)]
		public int importance;

		[FieldOffset(8)]
		public float intensity;

		[FieldOffset(12)]
		public float blendDistance;

		[FieldOffset(16)]
		public RenderVector3 boxSize;

		[FieldOffset(28)]
		public int cubemapAssetId;

		[FieldOffset(32)]
		public int resolution;

		[FieldOffset(36)]
		public float shadowDistance;

		[FieldOffset(40)]
		public RenderVector4 backgroundColor;

		[FieldOffset(56)]
		public float nearClip;

		[FieldOffset(60)]
		public float farClip;

		[FieldOffset(64)]
		public ReflectionProbeType type;

		[FieldOffset(65)]
		public ReflectionProbeClear clearFlags;

		[FieldOffset(66)]
		public ReflectionProbeTimeSlicingMode timeSlicingMode;

		[FieldOffset(67)]
		private byte flags;

		public bool skyboxOnly
		{
			get
			{
				return flags.HasFlag(0);
			}
			set
			{
				flags.SetFlag(0, value);
			}
		}

		public bool HDR
		{
			get
			{
				return flags.HasFlag(1);
			}
			set
			{
				flags.SetFlag(1, value);
			}
		}

		public bool useBoxProjection
		{
			get
			{
				return flags.HasFlag(2);
			}
			set
			{
				flags.SetFlag(2, value);
			}
		}
	}
	public abstract class RenderablesStateUpdate<TState> : RenderablesUpdate where TState : unmanaged
	{
		public SharedMemoryBufferDescriptor<TState> states;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(states);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref states);
		}
	}
	public abstract class RenderablesUpdate : IMemoryPackable
	{
		public SharedMemoryBufferDescriptor<int> removals;

		public SharedMemoryBufferDescriptor<int> additions;

		public virtual void Pack(ref MemoryPacker packer)
		{
			packer.Write(removals);
			packer.Write(additions);
		}

		public virtual void Unpack(ref MemoryUnpacker packer)
		{
			packer.Read(ref removals);
			packer.Read(ref additions);
		}
	}
	[StructLayout(LayoutKind.Explicit, Size = 8)]
	public struct BlendshapeUpdate
	{
		[FieldOffset(0)]
		public int blendshapeIndex;

		[FieldOffset(4)]
		public float weight;
	}
	[StructLayout(LayoutKind.Explicit, Size = 8)]
	public struct BlendshapeUpdateBatch
	{
		[FieldOffset(0)]
		public int renderableIndex;

		[FieldOffset(4)]
		public int blendshapeUpdateCount;
	}
	[StructLayout(LayoutKind.Explicit, Size = 12)]
	public struct BoneAssignment
	{
		[FieldOffset(0)]
		public int renderableIndex;

		[FieldOffset(4)]
		public int rootBoneTransformId;

		[FieldOffset(8)]
		public int boneCount;
	}
	public struct SkinnedMeshBoundsUpdate
	{
		public int renderableIndex;

		public RenderBoundingBox localBounds;
	}
	public struct SkinnedMeshRealtimeBoundsUpdate
	{
		public int renderableIndex;

		public RenderBoundingBox computedGlobalBounds;
	}
	public class SkinnedMeshRenderablesUpdate : MeshRenderablesUpdate
	{
		public SharedMemoryBufferDescriptor<SkinnedMeshBoundsUpdate> boundsUpdates;

		public SharedMemoryBufferDescriptor<SkinnedMeshRealtimeBoundsUpdate> realtimeBoundsUpdates;

		public SharedMemoryBufferDescriptor<BoneAssignment> boneAssignments;

		public SharedMemoryBufferDescriptor<int> boneTransformIndexes;

		public SharedMemoryBufferDescriptor<BlendshapeUpdateBatch> blendshapeUpdateBatches;

		public SharedMemoryBufferDescriptor<BlendshapeUpdate> blendshapeUpdates;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(boundsUpdates);
			packer.Write(realtimeBoundsUpdates);
			packer.Write(boneAssignments);
			packer.Write(boneTransformIndexes);
			packer.Write(blendshapeUpdateBatches);
			packer.Write(blendshapeUpdates);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref boundsUpdates);
			packer.Read(ref realtimeBoundsUpdates);
			packer.Read(ref boneAssignments);
			packer.Read(ref boneTransformIndexes);
			packer.Read(ref blendshapeUpdateBatches);
			packer.Read(ref blendshapeUpdates);
		}
	}
	public struct ReflectionProbeSH2Task
	{
		public int renderableIndex;

		public int reflectionProbeRenderableIndex;

		public ComputeResult result;

		public RenderSH2 resultData;
	}
	public class ReflectionProbeSH2Tasks : RenderablesUpdate
	{
		public SharedMemoryBufferDescriptor<ReflectionProbeSH2Task> tasks;

		public override void Pack(ref MemoryPacker packer)
		{
			base.Pack(ref packer);
			packer.Write(tasks);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			base.Unpack(ref packer);
			packer.Read(ref tasks);
		}
	}
	[StructLayout(LayoutKind.Explicit, Size = 16)]
	public struct TrailsRendererState
	{
		[FieldOffset(0)]
		public int renderableIndex;

		[FieldOffset(4)]
		public int trailsRenderBufferAssetId;

		[FieldOffset(8)]
		public int materialAssetId;

		[FieldOffset(12)]
		public TrailTextureMode textureMode;

		[FieldOffset(13)]
		public MotionVectorMode motionVectorMode;

		[FieldOffset(14)]
		public bool generateLightingData;
	}
	public class TrailsRendererUpdate : RenderablesStateUpdate<TrailsRendererState>
	{
	}
	[StructLayout(LayoutKind.Explicit, Size = 8)]
	public struct TransformParentUpdate
	{
		[FieldOffset(0)]
		public int transformId;

		[FieldOffset(4)]
		public int newParentId;
	}
	[StructLayout(LayoutKind.Explicit, Size = 44)]
	public struct TransformPoseUpdate
	{
		[FieldOffset(0)]
		public int transformId;

		[FieldOffset(4)]
		public RenderTransform pose;
	}
	public class TransformsUpdate : IMemoryPackable
	{
		public int targetTransformCount;

		public SharedMemoryBufferDescriptor<int> removals;

		public SharedMemoryBufferDescriptor<TransformParentUpdate> parentUpdates;

		public SharedMemoryBufferDescriptor<TransformPoseUpdate> poseUpdates;

		public void Pack(ref MemoryPacker packer)
		{
			packer.Write(targetTransformCount);
			packer.Write(removals);
			packer.Write(parentUpdates);
			packer.Write(poseUpdates);
		}

		public void Unpack(ref MemoryUnpacker packer)
		{
			packer.Read(ref targetTransformCount);
			packer.Read(ref removals);
			packer.Read(ref parentUpdates);
			packer.Read(ref poseUpdates);
		}
	}
	public class RendererEngineReady : RendererCommand
	{
		public override void Pack(ref MemoryPacker packer)
		{
		}

		public override void Unpack(ref MemoryUnpacker unpacker)
		{
		}
	}
	public class RendererInitData : RendererCommand
	{
		public string sharedMemoryPrefix;

		public Guid uniqueSessionId;

		public int mainProcessId;

		public bool debugFramePacing;

		public HeadOutputDevice outputDevice;

		public string windowTitle;

		public SetWindowIcon setWindowIcon;

		public RendererSplashScreenOverride splashScreenOverride;

		public override void Pack(ref MemoryPacker packer)
		{
			packer.Write(sharedMemoryPrefix);
			packer.Write(uniqueSessionId);
			packer.Write(mainProcessId);
			packer.Write(debugFramePacing);
			packer.Write(outputDevice);
			packer.Write(windowTitle);
			packer.WriteObject(setWindowIcon);
			packer.WriteObject(splashScreenOverride);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			packer.Read(ref sharedMemoryPrefix);
			packer.Read(ref uniqueSessionId);
			packer.Read(ref mainProcessId);
			packer.Read(ref debugFramePacing);
			packer.Read(ref outputDevice);
			packer.Read(ref windowTitle);
			packer.ReadObject(ref setWindowIcon);
			packer.ReadObject(ref splashScreenOverride);
		}
	}
	public class RendererInitFinalizeData : RendererCommand
	{
		public override void Pack(ref MemoryPacker packer)
		{
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
		}
	}
	public class RendererInitProgressUpdate : RendererCommand
	{
		public float progress;

		public string phase;

		public string subPhase;

		public bool forceShow;

		public override void Pack(ref MemoryPacker packer)
		{
			packer.Write(progress);
			packer.Write(phase);
			packer.Write(subPhase);
			packer.Write(forceShow);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			packer.Read(ref progress);
			packer.Read(ref phase);
			packer.Read(ref subPhase);
			packer.Read(ref forceShow);
		}
	}
	public class RendererInitResult : RendererCommand
	{
		public HeadOutputDevice actualOutputDevice;

		public string rendererIdentifier;

		public long mainWindowHandlePtr;

		public string stereoRenderingMode;

		public int maxTextureSize;

		public bool isGPUTexturePOTByteAligned;

		public List<TextureFormat> supportedTextureFormats;

		public override string ToString()
		{
			return $"Renderer: {rendererIdentifier} (WindowPtr: 0x{mainWindowHandlePtr:X}, ActualOutputDevice: {actualOutputDevice}, StereoRenderingMode: {stereoRenderingMode}, " + $"MaxTextureSize: {maxTextureSize}, IsGPUTexturePOTByteAligned: {isGPUTexturePOTByteAligned}\n" + "Supported texture formats: " + string.Join(", ", supportedTextureFormats);
		}

		public override void Pack(ref MemoryPacker packer)
		{
			packer.Write(actualOutputDevice);
			packer.Write(rendererIdentifier);
			packer.Write(mainWindowHandlePtr);
			packer.Write(stereoRenderingMode);
			packer.Write(maxTextureSize);
			packer.Write(isGPUTexturePOTByteAligned);
			packer.WriteValueList(supportedTextureFormats);
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
			packer.Read(ref actualOutputDevice);
			packer.Read(ref rendererIdentifier);
			packer.Read(ref mainWindowHandlePtr);
			packer.Read(ref stereoRenderingMode);
			packer.Read(ref maxTextureSize);
			packer.Read(ref isGPUTexturePOTByteAligned);
			packer.ReadValueList(ref supportedTextureFormats);
		}
	}
	public class RendererParentWindow : RendererCommand
	{
		public long windowHandle;

		public override void Pack(ref MemoryPacker packer)
		{
			packer.Write(windowHandle);
		}

		public override void Unpack(ref MemoryUnpacker unpacker)
		{
			unpacker.Read(ref windowHandle);
		}
	}
	public class RendererShutdown : RendererCommand
	{
		public override void Pack(ref MemoryPacker packer)
		{
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
		}
	}
	public class RendererShutdownRequest : RendererCommand
	{
		public override void Pack(ref MemoryPacker packer)
		{
		}

		public override void Unpack(ref MemoryUnpacker packer)
		{
		}
	}
	public class RendererSplashScreenOverride : IMemoryPackable
	{
		public RenderVector2i textureSize;

		public SharedMemoryBufferDescriptor<byte> textureData;

		public RenderVector2 loadingBarOffset;

		public float textureRelativeScreenSize;

		public void Pack(ref MemoryPacker packer)
		{
			packer.Write(textureSize);
			packer.Write(textureData);
			packer.Write(loadingBarOffset);
			packer.Write(textureRelativeScreenSize);
		}

		public void Unpack(ref MemoryUnpacker unpacker)
		{
			unpacker.Read(ref textureSize);
			unpacker.Read(ref textureData);
			unpacker.Read(ref loadingBarOffset);
			unpacker.Read(ref textureRelativeScreenSize);
		}
	}
	public class SetTaskbarProgress : RendererCommand
	{
		public TaskbarProgressBarMode mode;

		public ulong completed;

		public ulong total;

		public override void Pack(ref MemoryPacker packer)
		{
			packer.Write(mode);
			packer.Write(completed);
			packer.Write(total);
		}

		public override void Unpack(ref MemoryUnpacker unpacker)
		{
			unpacker.Read(ref mode);
			unpacker.Read(ref completed);
			unpacker.Read(ref total);
		}
	}
	public class SetWindowIcon : RendererCommand
	{
		public int requestId;

		public bool isOverlay;

		public RenderVector2i size;

		public SharedMemoryBufferDescriptor<byte> iconData;

		public string overlayDescription;

		public override void Pack(ref MemoryPacker packer)
		{
			packer.Write(requestId);
			packer.Write(isOverlay);
			packer.Write(size);
			packer.Write(iconData);
			if (isOverlay)
			{
				packer.Write(overlayDescription);
			}
		}

		public override void Unpack(ref MemoryUnpacker unpacker)
		{
			unpacker.Read(ref requestId);
			unpacker.Read(ref isOverlay);
			unpacker.Read(ref size);
			unpacker.Read(ref iconData);
			if (isOverlay)
			{
				unpacker.Read(ref overlayDescription);
			}
		}
	}
	public class SetWindowIconResult : RendererCommand
	{
		public int requestId;

		public bool success;

		public override void Pack(ref MemoryPacker packer)
		{
			packer.Write(requestId);
			packer.Write(success);
		}

		public override void Unpack(ref MemoryUnpacker unpacker)
		{
			unpacker.Read(ref requestId);
			unpacker.Read(ref success);
		}
	}
	public enum TaskbarProgressBarMode
	{
		None,
		Indeterminate,
		Normal,
		Error,
		Paused
	}
	[StructLayout(LayoutKind.Explicit, Size = 8)]
	public struct RenderVector2
	{
		[FieldOffset(0)]
		public float x;

		[FieldOffset(4)]
		public float y;

		public RenderVector2(float x, float y)
		{
			this.x = x;
			this.y = y;
		}

		public override string ToString()
		{
			return $"X: {x}, Y: {y}";
		}
	}
	[StructLayout(LayoutKind.Explicit, Size = 12)]
	public struct RenderVector3
	{
		[FieldOffset(0)]
		public float x;

		[FieldOffset(4)]
		public float y;

		[FieldOffset(8)]
		public float z;

		public float this[int index] => index switch
		{
			0 => x, 
			1 => y, 
			2 => z, 
			_ => throw new ArgumentOutOfRangeException("index"), 
		};

		public RenderVector3(float x, float y, float z)
		{
			this.x = x;
			this.y = y;
			this.z = z;
		}

		public override string ToString()
		{
			return $"X: {x}, Y: {y}, Z: {z}";
		}
	}
	[StructLayout(LayoutKind.Explicit, Size = 16)]
	public struct RenderVector4
	{
		[FieldOffset(0)]
		public float x;

		[FieldOffset(4)]
		public float y;

		[FieldOffset(8)]
		public float z;

		[FieldOffset(12)]
		public float w;

		public RenderVector4(float x, float y, float z, float w)
		{
			this.x = x;
			this.y = y;
			this.z = z;
			this.w = w;
		}

		public override string ToString()
		{
			return $"X: {x}, Y: {y}, Z: {z}, W: {w}";
		}
	}
	[StructLayout(LayoutKind.Explicit, Size = 16)]
	public struct RenderQuaternion
	{
		[FieldOffset(0)]
		public float x;

		[FieldOffset(4)]
		public float y;

		[FieldOffset(8)]
		public float z;

		[FieldOffset(12)]
		public float w;

		public RenderQuaternion(float x, float y, float z, float w)
		{
			this.x = x;
			this.y = y;
			this.z = z;
			this.w = w;
		}

		public override string ToString()
		{
			return $"X: {x}, Y: {y}, Z: {z}, W: {w}";
		}
	}
	[StructLayout(LayoutKind.Explicit, Size = 64)]
	public struct RenderMatrix4x4
	{
		[FieldOffset(0)]
		public float m00;

		[FieldOffset(4)]
		public float m10;

		[FieldOffset(8)]
		public float m20;

		[FieldOffset(12)]
		public float m30;

		[FieldOffset(16)]
		public float m01;

		[FieldOffset(20)]
		public float m11;

		[FieldOffset(24)]
		public float m21;

		[FieldOffset(28)]
		public float m31;

		[FieldOffset(32)]
		public float m02;

		[FieldOffset(36)]
		public float m12;

		[FieldOffset(40)]
		public float m22;

		[FieldOffset(44)]
		public float m32;

		[FieldOffset(48)]
		public float m03;

		[FieldOffset(52)]
		public float m13;

		[FieldOffset(56)]
		public float m23;

		[FieldOffset(60)]
		public float m33;

		public RenderMatrix4x4(float m00, float m01, float m02, float m03, float m10, float m11, float m12, float m13, float m20, float m21, float m22, float m23, float m30, float m31, float m32, float m33)
		{
			this.m00 = m00;
			this.m01 = m01;
			this.m02 = m02;
			this.m03 = m03;
			this.m10 = m10;
			this.m11 = m11;
			this.m12 = m12;
			this.m13 = m13;
			this.m20 = m20;
			this.m21 = m21;
			this.m22 = m22;
			this.m23 = m23;
			this.m30 = m30;
			this.m31 = m31;
			this.m32 = m32;
			this.m33 = m33;
		}

		public override string ToString()
		{
			return $"[{m00}, {m01}, {m02}, {m03}], " + $"[{m10}, {m11}, {m12}, {m13}], " + $"[{m20}, {m21}, {m22}, {m23}], " + $"[{m30}, {m31}, {m32}, {m33}]";
		}
	}
	[StructLayout(LayoutKind.Explicit, Size = 40)]
	public struct RenderTransform
	{
		[FieldOffset(0)]
		public RenderVector3 position;

		[FieldOffset(12)]
		public RenderVector3 scale;

		[FieldOffset(24)]
		public RenderQuaternion rotation;

		public RenderTransform(RenderVector3 position, RenderQuaternion rotation, RenderVector3 scale)
		{
			this.position = position;
			this.rotation = rotation;
			this.scale = scale;
		}

		public override string ToString()
		{
			return $"Position: {position}, Rotation: {rotation}, Scale: {scale}";
		}
	}
	[StructLayout(LayoutKind.Explicit, Size = 8)]
	public struct RenderVector2i
	{
		[FieldOffset(0)]
		public int x;

		[FieldOffset(4)]
		public int y;

		public RenderVector2i(int x, int y)
		{
			this.x = x;
			this.y = y;
		}

		public RenderVector2i(int xy)
			: this(xy, xy)
		{
		}

		public override string ToString()
		{
			return $"X: {x}, Y: {y}";
		}
	}
	[StructLayout(LayoutKind.Explicit, Size = 12)]
	public struct RenderVector3i
	{
		[FieldOffset(0)]
		public int x;

		[FieldOffset(4)]
		public int y;

		[FieldOffset(8)]
		public int z;

		public RenderVector3i(int x, int y, int z)
		{
			this.x = x;
			this.y = y;
			this.z = z;
		}

		public RenderVector3i(int xyz)
			: this(xyz, xyz, xyz)
		{
		}

		public override string ToString()
		{
			return $"X: {x}, Y: {y}, Z: {z}";
		}
	}
	[StructLayout(LayoutKind.Explicit, Size = 24)]
	public struct RenderBoundingBox
	{
		[FieldOffset(0)]
		public RenderVector3 center;

		[FieldOffset(12)]
		public RenderVector3 extents;

		public RenderBoundingBox(RenderVector3 center, RenderVector3 extents)
		{
			this.center = center;
			this.extents = extents;
		}

		public override string ToString()
		{
			return $"Center: {center}, Extents: {extents}";
		}
	}
	[StructLayout(LayoutKind.Explicit, Size = 16)]
	public struct RenderRect
	{
		[FieldOffset(0)]
		public float x;

		[FieldOffset(4)]
		public float y;

		[FieldOffset(8)]
		public float width;

		[FieldOffset(12)]
		public float height;

		public RenderRect(float x, float y, float width, float height)
		{
			this.x = x;
			this.y = y;
			this.width = width;
			this.height = height;
		}

		public override string ToString()
		{
			return $"X: {x}, Y: {y}, Width: {width}, Height: {height}";
		}
	}
	[StructLayout(LayoutKind.Explicit, Size = 16)]
	public struct RenderIntRect
	{
		[FieldOffset(0)]
		public int x;

		[FieldOffset(4)]
		public int y;

		[FieldOffset(8)]
		public int width;

		[FieldOffset(12)]
		public int height;

		public RenderIntRect(int x, int y, int width, int height)
		{
			this.x = x;
			this.y = y;
			this.width = width;
			this.height = height;
		}

		public override string ToString()
		{
			return $"X: {x}, Y: {y}, Width: {width}, Height: {height}";
		}
	}
	[StructLayout(LayoutKind.Explicit, Size = 108)]
	public struct RenderSH2
	{
		[FieldOffset(0)]
		public RenderVector3 sh0;

		[FieldOffset(12)]
		public RenderVector3 sh1;

		[FieldOffset(24)]
		public RenderVector3 sh2;

		[FieldOffset(36)]
		public RenderVector3 sh3;

		[FieldOffset(48)]
		public RenderVector3 sh4;

		[FieldOffset(60)]
		public RenderVector3 sh5;

		[FieldOffset(72)]
		public RenderVector3 sh6;

		[FieldOffset(84)]
		public RenderVector3 sh7;

		[FieldOffset(96)]
		public RenderVector3 sh8;

		public RenderVector3 this[int index] => index switch
		{
			0 => sh0, 
			1 => sh1, 
			2 => sh2, 
			3 => sh3, 
			4 => sh4, 
			5 => sh5, 
			6 => sh6, 
			7 => sh7, 
			8 => sh8, 
			_ => throw new ArgumentOutOfRangeException("index"), 
		};

		public RenderSH2(RenderVector3 sh0, RenderVector3 sh1, RenderVector3 sh2, RenderVector3 sh3, RenderVector3 sh4, RenderVector3 sh5, RenderVector3 sh6, RenderVector3 sh7, RenderVector3 sh8)
		{
			this.sh0 = sh0;
			this.sh1 = sh1;
			this.sh2 = sh2;
			this.sh3 = sh3;
			this.sh4 = sh4;
			this.sh5 = sh5;
			this.sh6 = sh6;
			this.sh7 = sh7;
			this.sh8 = sh8;
		}

		public override string ToString()
		{
			return $"SH0: {sh0}\n" + $"SH1: {sh1}\n" + $"SH2: {sh2}\n" + $"SH3: {sh3}\n" + $"SH4: {sh4}\n" + $"SH5: {sh5}\n" + $"SH6: {sh6}\n" + $"SH7: {sh7}\n" + $"SH8: {sh8}";
		}
	}
	public enum ComputeResult
	{
		Scheduled,
		Computed,
		Postpone,
		Failed
	}
	public enum LayerType : byte
	{
		Hidden,
		Overlay
	}
	[DataModelType]
	[OldTypeName("FrooxEngine.LightTyle", "FrooxEngine")]
	public enum LightType : byte
	{
		Point,
		Directional,
		Spot
	}
	[DataModelType]
	[OldTypeName("FrooxEngine.ShadowType", "FrooxEngine")]
	public enum ShadowType : byte
	{
		None,
		Hard,
		Soft
	}
	[DataModelType]
	[OldTypeName("FrooxEngine.ShadowCastMode", "FrooxEngine")]
	public enum ShadowCastMode : byte
	{
		Off,
		On,
		ShadowOnly,
		DoubleSided
	}
	[DataModelType]
	[OldTypeName("FrooxEngine.MotionVectorMode", "FrooxEngine")]
	public enum MotionVectorMode : byte
	{
		Camera,
		Object,
		NoMotion
	}
	public enum MeshRendererType : byte
	{
		MeshRenderer,
		SkinnedMeshRenderer
	}
	[DataModelType]
	[OldTypeName("FrooxEngine.ReflectionProbe+Clear", "FrooxEngine")]
	public enum ReflectionProbeClear : byte
	{
		Skybox,
		Color
	}
	[DataModelType]
	[OldTypeName("FrooxEngine.ReflectionProbe+Type", "FrooxEngine")]
	public enum ReflectionProbeType : byte
	{
		Baked,
		OnChanges,
		Realtime
	}
	[DataModelType]
	[OldTypeName("FrooxEngine.ReflectionProbe+TimeSlicingMode", "FrooxEngine")]
	public enum ReflectionProbeTimeSlicingMode : byte
	{
		AllFacesAtOnce,
		IndividualFaces,
		NoTimeSlicing
	}
	[DataModelType]
	[OldTypeName("FrooxEngine.BillboardRenderBufferRenderer+BillboardAlignment", "FrooxEngine")]
	public enum BillboardAlignment : byte
	{
		View,
		Facing,
		Local,
		Global,
		Direction
	}
	[DataModelType]
	[OldTypeName("FrooxEngine.MeshRenderBufferRenderer+MeshAlignment", "FrooxEngine")]
	public enum MeshAlignment : byte
	{
		View,
		Facing,
		Local,
		Global
	}
	[DataModelType]
	[OldTypeName("FrooxEngine.ParticleTrailTextureMode", "FrooxEngine")]
	[OldTypeName("FrooxEngine.TrailTextureMode", "FrooxEngine")]
	public enum TrailTextureMode : byte
	{
		Stretch,
		Tile,
		DistributePerSegment,
		RepeatPerSegment
	}
	[DataModelType]
	[OldTypeName("FrooxEngine.RenderingContext", "FrooxEngine")]
	public enum RenderingContext : byte
	{
		UserView,
		ExternalView,
		Camera,
		Mirror,
		Portal,
		RenderToAsset
	}
	public readonly struct UnmanagedSpan<T> where T : unmanaged
	{
		public unsafe readonly T* data;

		public readonly int size;

		public int Length => size;

		public unsafe T this[int index]
		{
			get
			{
				CheckIndex(index);
				return data[index];
			}
			set
			{
				CheckIndex(index);
				data[index] = value;
			}
		}

		public unsafe UnmanagedSpan(T* data, int size)
		{
			this.data = data;
			this.size = size;
		}

		private void CheckIndex(int index)
		{
			if (index < 0 || index >= size)
			{
				throw new ArgumentOutOfRangeException("index");
			}
		}

		public unsafe UnmanagedSpan<T> Slice(int index)
		{
			if (index == size)
			{
				return new UnmanagedSpan<T>(null, 0);
			}
			CheckIndex(index);
			return new UnmanagedSpan<T>(data + index, size - index);
		}

		public unsafe UnmanagedSpan<T> Slice(int index, int count)
		{
			if (index == size)
			{
				if (count > 0)
				{
					throw new ArgumentOutOfRangeException("count");
				}
				return new UnmanagedSpan<T>(null, 0);
			}
			CheckIndex(index);
			if (index + count > size)
			{
				throw new ArgumentOutOfRangeException("count");
			}
			return new UnmanagedSpan<T>(data + index, count);
		}

		public unsafe UnmanagedSpan<O> As<O>() where O : unmanaged
		{
			int num = sizeof(T);
			int num2 = sizeof(O);
			int num3 = size * num / num2;
			return new UnmanagedSpan<O>((O*)data, num3);
		}
	}
	[DataModelType]
	[OldTypeName("FrooxEngine.ControllerNode", "FrooxEngine")]
	[OldTypeName("FrooxEngine.Chirality", "FrooxEngine")]
	public enum Chirality : sbyte
	{
		Left,
		Right
	}
	[DataModelType]
	[OldTypeName("FrooxEngine.BodyNode", "FrooxEngine")]
	public enum BodyNode
	{
		NONE = 0,
		Root = 1,
		View = 2,
		LeftController = 3,
		RightController = 4,
		Hips = 5,
		Spine = 6,
		Chest = 7,
		UpperChest = 8,
		Neck = 9,
		Head = 10,
		Jaw = 11,
		LeftEye = 12,
		RightEye = 13,
		LeftShoulder = 14,
		LeftUpperArm = 15,
		LeftLowerArm = 16,
		LeftHand = 17,
		LeftPalm = 18,
		LeftThumb_Metacarpal = 19,
		LeftThumb_Proximal = 20,
		LeftThumb_Distal = 21,
		LeftThumb_Tip = 22,
		LeftIndexFinger_Metacarpal = 23,
		LeftIndexFinger_Proximal = 24,
		LeftIndexFinger_Intermediate = 25,
		LeftIndexFinger_Distal = 26,
		LeftIndexFinger_Tip = 27,
		LeftMiddleFinger_Metacarpal = 28,
		LeftMiddleFinger_Proximal = 29,
		LeftMiddleFinger_Intermediate = 30,
		LeftMiddleFinger_Distal = 31,
		LeftMiddleFinger_Tip = 32,
		LeftRingFinger_Metacarpal = 33,
		LeftRingFinger_Proximal = 34,
		LeftRingFinger_Intermediate = 35,
		LeftRingFinger_Distal = 36,
		LeftRingFinger_Tip = 37,
		LeftPinky_Metacarpal = 38,
		LeftPinky_Proximal = 39,
		LeftPinky_Intermediate = 40,
		LeftPinky_Distal = 41,
		LeftPinky_Tip = 42,
		RightShoulder = 43,
		RightUpperArm = 44,
		RightLowerArm = 45,
		RightHand = 46,
		RightPalm = 47,
		RightThumb_Metacarpal = 48,
		RightThumb_Proximal = 49,
		RightThumb_Distal = 50,
		RightThumb_Tip = 51,
		RightIndexFinger_Metacarpal = 52,
		RightIndexFinger_Proximal = 53,
		RightIndexFinger_Intermediate = 54,
		RightIndexFinger_Distal = 55,
		RightIndexFinger_Tip = 56,
		RightMiddleFinger_Metacarpal = 57,
		RightMiddleFinger_Proximal = 58,
		RightMiddleFinger_Intermediate = 59,
		RightMiddleFinger_Distal = 60,
		RightMiddleFinger_Tip = 61,
		RightRingFinger_Metacarpal = 62,
		RightRingFinger_Proximal = 63,
		RightRingFinger_Intermediate = 64,
		RightRingFinger_Distal = 65,
		RightRingFinger_Tip = 66,
		RightPinky_Metacarpal = 67,
		RightPinky_Proximal = 68,
		RightPinky_Intermediate = 69,
		RightPinky_Distal = 70,
		RightPinky_Tip = 71,
		LeftUpperLeg = 72,
		LeftLowerLeg = 73,
		LeftFoot = 74,
		LeftToes = 75,
		RightUpperLeg = 76,
		RightLowerLeg = 77,
		RightFoot = 78,
		RightToes = 79,
		END = 80,
		LEFT_FINGER_START = 19,
		LEFT_FINGER_END = 42,
		RIGHT_FINGER_START = 48,
		RIGHT_FINGER_END = 71
	}
	public static class BodyNodeExtensions
	{
		public static Chirality GetOther(this Chirality side)
		{
			if (side != Chirality.Left)
			{
				return Chirality.Left;
			}
			return Chirality.Right;
		}

		public static int GetFingerNodeIndex(this BodyNode node, out Chirality chirality)
		{
			if (node >= BodyNode.LeftThumb_Metacarpal && node <= BodyNode.LeftPinky_Tip)
			{
				chirality = Chirality.Left;
				return (int)(node - 19);
			}
			if (node >= BodyNode.RightThumb_Metacarpal && node <= BodyNode.RightPinky_Tip)
			{
				chirality = Chirality.Right;
				return (int)(node - 48);
			}
			chirality = (Chirality)(-1);
			return -1;
		}

		public static BodyNode GetRelativeNode(this BodyNode node)
		{
			if (node == BodyNode.NONE)
			{
				return BodyNode.NONE;
			}
			if (node.IsFinger() || node.IsPalm())
			{
				return BodyNode.LeftHand.GetSide(node.GetChirality());
			}
			if (node.IsEye())
			{
				return BodyNode.Head;
			}
			return BodyNode.Root;
		}

		public static bool IsEye(this BodyNode node)
		{
			if (node != BodyNode.LeftEye)
			{
				return node == BodyNode.RightEye;
			}
			return true;
		}

		public static BodyNode GetLeftSide(this BodyNode node)
		{
			return node.GetSide(Chirality.Left);
		}

		public static BodyNode GetRightSide(this BodyNode node)
		{
			return node.GetSide(Chirality.Right);
		}

		public static BodyNode GetOtherSide(this BodyNode node)
		{
			if (node.GetChirality() == Chirality.Left)
			{
				return node.GetRightSide();
			}
			return node.GetLeftSide();
		}

		public static Chirality GetChirality(this BodyNode node)
		{
			switch (node)
			{
			case BodyNode.LeftController:
				return Chirality.Left;
			case BodyNode.RightController:
				return Chirality.Right;
			case BodyNode.LeftShoulder:
			case BodyNode.LeftUpperArm:
			case BodyNode.LeftLowerArm:
			case BodyNode.LeftHand:
			case BodyNode.LeftPalm:
			case BodyNode.LeftThumb_Metacarpal:
			case BodyNode.LeftThumb_Proximal:
			case BodyNode.LeftThumb_Distal:
			case BodyNode.LeftThumb_Tip:
			case BodyNode.LeftIndexFinger_Metacarpal:
			case BodyNode.LeftIndexFinger_Proximal:
			case BodyNode.LeftIndexFinger_Intermediate:
			case BodyNode.LeftIndexFinger_Distal:
			case BodyNode.LeftIndexFinger_Tip:
			case BodyNode.LeftMiddleFinger_Metacarpal:
			case BodyNode.LeftMiddleFinger_Proximal:
			case BodyNode.LeftMiddleFinger_Intermediate:
			case BodyNode.LeftMiddleFinger_Distal:
			case BodyNode.LeftMiddleFinger_Tip:
			case BodyNode.LeftRingFinger_Metacarpal:
			case BodyNode.LeftRingFinger_Proximal:
			case BodyNode.LeftRingFinger_Intermediate:
			case BodyNode.LeftRingFinger_Distal:
			case BodyNode.LeftRingFinger_Tip:
			case BodyNode.LeftPinky_Metacarpal:
			case BodyNode.LeftPinky_Proximal:
			case BodyNode.LeftPinky_Intermediate:
			case BodyNode.LeftPinky_Distal:
			case BodyNode.LeftPinky_Tip:
				return Chirality.Left;
			default:
				if (node >= BodyNode.RightShoulder && node <= BodyNode.RightPinky_Tip)
				{
					return Chirality.Right;
				}
				if (node >= BodyNode.LeftUpperLeg && node <= BodyNode.LeftToes)
				{
					return Chirality.Left;
				}
				if (node >= BodyNode.RightUpperLeg && node <= BodyNode.RightToes)
				{
					return Chirality.Right;
				}
				return node switch
				{
					BodyNode.LeftEye => Chirality.Left, 
					BodyNode.RightEye => Chirality.Right, 
					_ => (Chirality)(-1), 
				};
			}
		}

		public static BodyNode GetSide(this BodyNode node, Chirality chirality)
		{
			bool flag = chirality == Chirality.Left;
			switch (node)
			{
			case BodyNode.LeftController:
			case BodyNode.RightController:
				if (!flag)
				{
					return BodyNode.RightController;
				}
				return BodyNode.LeftController;
			case BodyNode.LeftShoulder:
			case BodyNode.LeftUpperArm:
			case BodyNode.LeftLowerArm:
			case BodyNode.LeftHand:
			case BodyNode.LeftPalm:
			case BodyNode.LeftThumb_Metacarpal:
			case BodyNode.LeftThumb_Proximal:
			case BodyNode.LeftThumb_Distal:
			case BodyNode.LeftThumb_Tip:
			case BodyNode.LeftIndexFinger_Metacarpal:
			case BodyNode.LeftIndexFinger_Proximal:
			case BodyNode.LeftIndexFinger_Intermediate:
			case BodyNode.LeftIndexFinger_Distal:
			case BodyNode.LeftIndexFinger_Tip:
			case BodyNode.LeftMiddleFinger_Metacarpal:
			case BodyNode.LeftMiddleFinger_Proximal:
			case BodyNode.LeftMiddleFinger_Intermediate:
			case BodyNode.LeftMiddleFinger_Distal:
			case BodyNode.LeftMiddleFinger_Tip:
			case BodyNode.LeftRingFinger_Metacarpal:
			case BodyNode.LeftRingFinger_Proximal:
			case BodyNode.LeftRingFinger_Intermediate:
			case BodyNode.LeftRingFinger_Distal:
			case BodyNode.LeftRingFinger_Tip:
			case BodyNode.LeftPinky_Metacarpal:
			case BodyNode.LeftPinky_Proximal:
			case BodyNode.LeftPinky_Intermediate:
			case BodyNode.LeftPinky_Distal:
			case BodyNode.LeftPinky_Tip:
				if (flag)
				{
					return node;
				}
				return node + 29;
			default:
				if (node >= BodyNode.LeftUpperLeg && node <= BodyNode.LeftToes)
				{
					if (flag)
					{
						return node;
					}
					return node + 4;
				}
				if (node >= BodyNode.RightShoulder && node <= BodyNode.RightPinky_Tip)
				{
					if (flag)
					{
						return node - 29;
					}
					return node;
				}
				if (node >= BodyNode.RightUpperLeg && node <= BodyNode.RightToes)
				{
					if (flag)
					{
						return node - 4;
					}
					return node;
				}
				if (node.IsEye())
				{
					if (!flag)
					{
						return BodyNode.RightEye;
					}
					return BodyNode.LeftEye;
				}
				return BodyNode.NONE;
			}
		}
	}
	public static class HandExtensions
	{
		public static bool IsPalm(this BodyNode node)
		{
			if (node != BodyNode.LeftPalm)
			{
				return node == BodyNode.RightPalm;
			}
			return true;
		}

		public static bool IsHand(this BodyNode node)
		{
			if (node != BodyNode.LeftHand)
			{
				return node == BodyNode.RightHand;
			}
			return true;
		}

		public static bool IsForearm(this BodyNode node)
		{
			if (node != BodyNode.LeftLowerArm)
			{
				return node == BodyNode.RightLowerArm;
			}
			return true;
		}

		public static bool IsUpperArm(this BodyNode node)
		{
			if (node != BodyNode.LeftUpperArm)
			{
				return node == BodyNode.RightUpperArm;
			}
			return true;
		}

		public static bool IsShoulder(this BodyNode node)
		{
			if (node != BodyNode.LeftShoulder)
			{
				return node == BodyNode.RightShoulder;
			}
			return true;
		}

		public static bool IsFoot(this BodyNode node)
		{
			if (node != BodyNode.LeftFoot)
			{
				return node == BodyNode.RightFoot;
			}
			return true;
		}

		public static Chirality GetHandChirality(this BodyNode node)
		{
			if (node >= BodyNode.LeftShoulder && node <= BodyNode.LeftPinky_Tip)
			{
				return Chirality.Left;
			}
			if (node >= BodyNode.RightShoulder && node <= BodyNode.RightPinky_Tip)
			{
				return Chirality.Right;
			}
			throw new Exception("Not a hand node");
		}
	}
	[DataModelType]
	[OldTypeName("FrooxEngine.FingerSegmentType", "FrooxEngine")]
	public enum FingerSegmentType
	{
		Metacarpal,
		Proximal,
		Intermediate,
		Distal,
		Tip
	}
	public static class FingerSegmentExtensions
	{
		public static int GetFingerSegmentIndex(this BodyNode node)
		{
			node = node.GetLeftSide();
			return (int)(node - 19);
		}

		public static FingerSegmentType GetFingerSegmentType(this BodyNode node, bool throwOnException = false)
		{
			node = node.GetLeftSide();
			switch (node)
			{
			case BodyNode.LeftThumb_Metacarpal:
				return FingerSegmentType.Metacarpal;
			case BodyNode.LeftThumb_Proximal:
				return FingerSegmentType.Proximal;
			case BodyNode.LeftThumb_Distal:
				return FingerSegmentType.Distal;
			case BodyNode.LeftThumb_Tip:
				return FingerSegmentType.Tip;
			default:
				if (throwOnException)
				{
					throw new Exception("Not a finger node");
				}
				return (FingerSegmentType)(-1);
			case BodyNode.LeftIndexFinger_Metacarpal:
			case BodyNode.LeftIndexFinger_Proximal:
			case BodyNode.LeftIndexFinger_Intermediate:
			case BodyNode.LeftIndexFinger_Distal:
			case BodyNode.LeftIndexFinger_Tip:
			case BodyNode.LeftMiddleFinger_Metacarpal:
			case BodyNode.LeftMiddleFinger_Proximal:
			case BodyNode.LeftMiddleFinger_Intermediate:
			case BodyNode.LeftMiddleFinger_Distal:
			case BodyNode.LeftMiddleFinger_Tip:
			case BodyNode.LeftRingFinger_Metacarpal:
			case BodyNode.LeftRingFinger_Proximal:
			case BodyNode.LeftRingFinger_Intermediate:
			case BodyNode.LeftRingFinger_Distal:
			case BodyNode.LeftRingFinger_Tip:
			case BodyNode.LeftPinky_Metacarpal:
			case BodyNode.LeftPinky_Proximal:
			case BodyNode.LeftPinky_Intermediate:
			case BodyNode.LeftPinky_Distal:
			case BodyNode.LeftPinky_Tip:
				return (FingerSegmentType)((int)(node - 23) % 5);
			}
		}
	}
	[DataModelType]
	[OldTypeName("FrooxEngine.FingerType", "FrooxEngine")]
	public enum FingerType
	{
		Thumb,
		Index,
		Middle,
		Ring,
		Pinky
	}
	public static class FingerExtensions
	{
		public static bool IsFinger(this BodyNode node)
		{
			node = node.GetLeftSide();
			if (node >= BodyNode.LeftThumb_Metacarpal)
			{
				return node <= BodyNode.LeftPinky_Tip;
			}
			return false;
		}

		public static FingerType GetFingerType(this BodyNode node, bool throwOnInvalid = false)
		{
			node = node.GetLeftSide();
			if (node >= BodyNode.LeftThumb_Metacarpal && node <= BodyNode.LeftThumb_Tip)
			{
				return FingerType.Thumb;
			}
			if (node >= BodyNode.LeftIndexFinger_Metacarpal && node <= BodyNode.LeftIndexFinger_Tip)
			{
				return FingerType.Index;
			}
			if (node >= BodyNode.LeftMiddleFinger_Metacarpal && node <= BodyNode.LeftMiddleFinger_Tip)
			{
				return FingerType.Middle;
			}
			if (node >= BodyNode.LeftRingFinger_Metacarpal && node <= BodyNode.LeftRingFinger_Tip)
			{
				return FingerType.Ring;
			}
			if (node >= BodyNode.LeftPinky_Metacarpal && node <= BodyNode.LeftPinky_Tip)
			{
				return FingerType.Pinky;
			}
			if (throwOnInvalid)
			{
				throw new Exception("Not a finger node");
			}
			return (FingerType)(-1);
		}

		public static bool IsValidFinger(this FingerType finger, FingerSegmentType segment)
		{
			if (finger == FingerType.Thumb)
			{
				return segment != FingerSegmentType.Intermediate;
			}
			return true;
		}

		public static BodyNode ComposeFinger(this FingerType finger, FingerSegmentType segment, Chirality chirality, bool throwOnInvalid = true)
		{
			BodyNode node;
			switch (finger)
			{
			case FingerType.Thumb:
				switch (segment)
				{
				case FingerSegmentType.Intermediate:
				case FingerSegmentType.Distal:
					node = BodyNode.LeftThumb_Distal;
					break;
				case FingerSegmentType.Metacarpal:
					node = BodyNode.LeftThumb_Metacarpal;
					break;
				case FingerSegmentType.Proximal:
					node = BodyNode.LeftThumb_Proximal;
					break;
				case FingerSegmentType.Tip:
					node = BodyNode.LeftThumb_Tip;
					break;
				default:
					if (throwOnInvalid)
					{
						throw new ArgumentException($"Invalid Thumb FingerSegment: {segment}");
					}
					return (BodyNode)(-1);
				}
				break;
			case FingerType.Index:
				node = (BodyNode)(23 + segment);
				break;
			case FingerType.Middle:
				node = (BodyNode)(28 + segment);
				break;
			case FingerType.Ring:
				node = (BodyNode)(33 + segment);
				break;
			case FingerType.Pinky:
				node = (BodyNode)(38 + segment);
				break;
			default:
				if (throwOnInvalid)
				{
					throw new ArgumentException("Invalid finger");
				}
				return (BodyNode)(-1);
			}
			return node.GetSide(chirality);
		}
	}
	public static class IdPacker<T> where T : struct, Enum
	{
		public static readonly int typeCount;

		public static readonly int typeBits;

		public static readonly int idBits;

		public static readonly int maxId;

		public static readonly int packTypeShift;

		public static readonly uint unpackMask;

		static IdPacker()
		{
			typeCount = Enums.GetValues<T>().Count;
			typeBits = MathHelper.NecessaryBits((ulong)typeCount);
			idBits = 32 - typeBits;
			maxId = int.MaxValue >> typeBits;
			packTypeShift = 32 - typeBits;
			unpackMask = uint.MaxValue >> typeBits;
		}

		public static int Pack(int assetId, T type)
		{
			if (assetId > maxId)
			{
				throw new NotSupportedException("AssetID exceeded maximum value");
			}
			return assetId | (int)(Enums.ToUInt32Unsafe(type) << packTypeShift);
		}

		public static void Unpack(int packed, out int id, out T type)
		{
			uint source = (uint)packed >> packTypeShift;
			type = Unsafe.As<uint, T>(ref source);
			id = packed & (int)unpackMask;
		}
	}
}
