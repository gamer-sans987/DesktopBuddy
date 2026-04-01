using System;
using Elements.Assets;
using Elements.Core;
using Renderite.Shared;

namespace FrooxEngine;

[Category(new string[] { "Assets/Procedural Meshes" })]
public class CylinderMesh : ProceduralMesh
{
	public const int DEFAULT_SIDES = 16;

	public const float DEFAULT_RADIUS = 0.5f;

	public readonly Sync<float> Height;

	public readonly Sync<float> Radius;

	public readonly Sync<int> Sides;

	public readonly Sync<bool> Caps;

	public readonly Sync<bool> FlatShading;

	public readonly Sync<float2> UVScale;

	private ConicalFrustum cylinder;

	private int sides;

	public override int Version => 1;

	protected override void PrepareAssetUpdateData()
	{
		sides = Math.Max(3, Sides);
	}

	protected override void ClearMeshData()
	{
		cylinder = null;
	}

	protected override void UpdateMeshData(MeshX meshx)
	{
		bool value = false;
		if (cylinder == null || cylinder.Sides != sides || cylinder.Cap != Caps.Value || cylinder.FlatShading != FlatShading.Value)
		{
			cylinder?.Remove();
			cylinder = new ConicalFrustum(meshx, sides, FlatShading, Caps.Value);
			value = true;
		}
		cylinder.Radius = Radius;
		cylinder.RadiusTop = Radius;
		cylinder.Height = Height;
		cylinder.UVScale = UVScale.Value;
		cylinder.Update();
		uploadHint[MeshUploadHint.Flag.Geometry] = value;
	}

	protected override void OnAwake()
	{
		base.OnAwake();
		Height.Value = 1f;
		Radius.Value = 0.5f;
		Sides.Value = 16;
		Caps.Value = true;
		UVScale.Value = float2.One;
	}

	public override void CreateCollider()
	{
		CylinderCollider cylinderCollider = base.Slot.AttachComponent<CylinderCollider>();
		cylinderCollider.Radius.DriveFrom(Radius);
		cylinderCollider.Height.DriveFrom(Height);
	}

	protected override void OnLoading(DataTreeNode node, LoadControl control)
	{
		base.OnLoading(node, control);
		if (control.GetTypeVersion<CylinderMesh>() == 0)
		{
			RunSynchronously(delegate
			{
				UVScale.Value = new float2(-1f, 1f);
			});
		}
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		Height = new Sync<float>();
		Radius = new Sync<float>();
		Sides = new Sync<int>();
		Caps = new Sync<bool>();
		FlatShading = new Sync<bool>();
		UVScale = new Sync<float2>();
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
			7 => Height, 
			8 => Radius, 
			9 => Sides, 
			10 => Caps, 
			11 => FlatShading, 
			12 => UVScale, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static CylinderMesh __New()
	{
		return new CylinderMesh();
	}
}
