using System;
using Elements.Assets;
using Elements.Core;
using Renderite.Shared;

namespace FrooxEngine;

[Category(new string[] { "Assets/Procedural Meshes" })]
public class QuadMesh : ProceduralMesh
{
	public readonly Sync<floatQ> Rotation;

	public readonly Sync<float2> Size;

	public readonly Sync<float2> UVOffset;

	public readonly Sync<float2> UVScale;

	public readonly Sync<bool> ScaleUVWithSize;

	public readonly Sync<bool> DualSided;

	public readonly Sync<bool> UseVertexColors;

	public readonly Sync<colorX> UpperLeftColor;

	public readonly Sync<colorX> LowerLeftColor;

	public readonly Sync<colorX> LowerRightColor;

	public readonly Sync<colorX> UpperRightColor;

	private Quad quad;

	private Quad otherSide;

	private floatQ _rotation;

	private float2 _size;

	private float2 _uvOffset;

	private float2 _uvScale;

	private bool _scaleUvWithSize;

	private bool _dualSided;

	private bool _useColors;

	private colorX _ulColor;

	private colorX _urColor;

	private colorX _llColor;

	private colorX _lrColor;

	public override int Version => 1;

	public colorX Color
	{
		get
		{
			return UpperLeftColor.Value;
		}
		set
		{
			UpperLeftColor.Value = value;
			LowerLeftColor.Value = value;
			LowerRightColor.Value = value;
			UpperRightColor.Value = value;
		}
	}

	public float3 Facing
	{
		get
		{
			return Rotation.Value * float3.Backward;
		}
		set
		{
			Rotation.Value = floatQ.LookRotation(-value);
		}
	}

	protected override void PrepareAssetUpdateData()
	{
		_rotation = Rotation;
		_size = Size;
		_uvOffset = UVOffset;
		_uvScale = UVScale;
		_scaleUvWithSize = ScaleUVWithSize;
		_dualSided = DualSided;
		_useColors = UseVertexColors;
		_ulColor = UpperLeftColor;
		_urColor = UpperRightColor;
		_llColor = LowerLeftColor;
		_lrColor = LowerRightColor;
	}

	protected override void ClearMeshData()
	{
		quad = null;
		otherSide = null;
	}

	protected override void UpdateMeshData(MeshX meshx)
	{
		bool value = false;
		if (quad == null)
		{
			quad = new Quad(meshx);
			value = true;
		}
		if (quad.UseColors != _useColors)
		{
			quad.UseColors = _useColors;
			value = true;
		}
		quad.Size = _size;
		quad.Rotation = _rotation;
		float2 a = _uvScale;
		if (_scaleUvWithSize)
		{
			a *= _size;
		}
		quad.UVOffset = _uvOffset;
		quad.UVScale = a;
		quad.LowerLeftColor = _llColor;
		quad.LowerRightColor = _lrColor;
		quad.UpperLeftColor = _ulColor;
		quad.UpperRightColor = _urColor;
		quad.Update();
		if (_dualSided)
		{
			if (otherSide == null)
			{
				otherSide = new Quad(meshx);
				value = true;
			}
			if (otherSide.UseColors != _useColors)
			{
				otherSide.UseColors = _useColors;
				value = true;
			}
			otherSide.Size = _size;
			otherSide.Rotation = _rotation * floatQ.AxisAngleRad(float3.Up, MathF.PI);
			otherSide.UVOffset = _uvOffset;
			otherSide.UVScale = a;
			otherSide.LowerLeftColor = _llColor;
			otherSide.LowerRightColor = _lrColor;
			otherSide.UpperLeftColor = _ulColor;
			otherSide.UpperRightColor = _urColor;
			otherSide.Update();
		}
		else if (otherSide != null)
		{
			otherSide.Remove();
			otherSide = null;
			value = true;
		}
		uploadHint[MeshUploadHint.Flag.Geometry] = value;
	}

	protected override void OnAwake()
	{
		base.OnAwake();
		Size.Value = float2.One;
		Rotation.Value = floatQ.Identity;
		UVScale.Value = float2.One;
		Color = colorX.White;
		UseVertexColors.Value = true;
	}

	protected override void OnLoading(DataTreeNode node, LoadControl control)
	{
		if (control.GetTypeVersion(typeof(QuadMesh)) == 0)
		{
			RunSynchronously(delegate
			{
				float3 v = Rotation.Value * float3.Forward;
				float3 up = Rotation.Value * float3.Up;
				Rotation.Value = floatQ.LookRotation(-v, in up);
			});
		}
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		Rotation = new Sync<floatQ>();
		Size = new Sync<float2>();
		UVOffset = new Sync<float2>();
		UVScale = new Sync<float2>();
		ScaleUVWithSize = new Sync<bool>();
		DualSided = new Sync<bool>();
		UseVertexColors = new Sync<bool>();
		UpperLeftColor = new Sync<colorX>();
		LowerLeftColor = new Sync<colorX>();
		LowerRightColor = new Sync<colorX>();
		UpperRightColor = new Sync<colorX>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => HighPriorityIntegration, 
			4 => OverrideBoundingBox, 
			5 => OverridenBoundingBox, 
			6 => Profile, 
			7 => Rotation, 
			8 => Size, 
			9 => UVOffset, 
			10 => UVScale, 
			11 => ScaleUVWithSize, 
			12 => DualSided, 
			13 => UseVertexColors, 
			14 => UpperLeftColor, 
			15 => LowerLeftColor, 
			16 => LowerRightColor, 
			17 => UpperRightColor, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static QuadMesh __New()
	{
		return new QuadMesh();
	}
}
