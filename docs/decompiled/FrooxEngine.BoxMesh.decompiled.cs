using System;
using Elements.Assets;
using Elements.Core;
using Renderite.Shared;

namespace FrooxEngine;

[Category(new string[] { "Assets/Procedural Meshes" })]
public class BoxMesh : ProceduralMesh
{
	public readonly Sync<float3> Size;

	public readonly Sync<float3> UVScale;

	public readonly Sync<bool> ScaleUVWithSize;

	private Box box;

	private float3 _size;

	private float3 _uvScale;

	private bool _scaleUVwithSize;

	protected override void PrepareAssetUpdateData()
	{
		_size = Size.Value;
		_uvScale = UVScale.Value;
		_scaleUVwithSize = ScaleUVWithSize.Value;
	}

	protected override void ClearMeshData()
	{
		box = null;
	}

	protected override void UpdateMeshData(MeshX meshx)
	{
		uploadHint[MeshUploadHint.Flag.Geometry] = box == null;
		if (box == null)
		{
			box = new Box(meshx);
		}
		box.Size = _size;
		float3 a = _uvScale;
		if (_scaleUVwithSize)
		{
			a *= _size;
		}
		box.UVScale = a;
		box.Update();
	}

	protected override void OnAwake()
	{
		base.OnAwake();
		Size.Value = float3.One;
		UVScale.Value = float3.One;
	}

	public override void CreateCollider()
	{
		CreateBoxCollider();
	}

	public BoxCollider CreateBoxCollider()
	{
		BoxCollider boxCollider = base.Slot.AttachComponent<BoxCollider>();
		boxCollider.Size.DriveFrom(Size);
		return boxCollider;
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		Size = new Sync<float3>();
		UVScale = new Sync<float3>();
		ScaleUVWithSize = new Sync<bool>();
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
			7 => Size, 
			8 => UVScale, 
			9 => ScaleUVWithSize, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static BoxMesh __New()
	{
		return new BoxMesh();
	}
}
