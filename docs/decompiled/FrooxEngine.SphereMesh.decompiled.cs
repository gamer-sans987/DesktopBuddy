using System;
using Elements.Assets;
using Elements.Core;
using Renderite.Shared;

namespace FrooxEngine;

[Category(new string[] { "Assets/Procedural Meshes" })]
public class SphereMesh : ProceduralMesh
{
	[Range(0f, 10f, "0.00")]
	public readonly Sync<float> Radius;

	[Range(3f, 128f, "0.00")]
	public readonly Sync<int> Segments;

	[Range(3f, 128f, "0.00")]
	public readonly Sync<int> Rings;

	public readonly Sync<UVSphereCapsule.Shading> Shading;

	public readonly Sync<float2> UVScale;

	public readonly Sync<bool> DualSided;

	private UVSphereCapsule uvsphere;

	private int segments;

	private int rings;

	private int baseVerts;

	private int baseTrigs;

	public override int Version => 1;

	protected override void OnAwake()
	{
		base.OnAwake();
		Radius.Value = 0.5f;
		Segments.Value = 32;
		Rings.Value = 16;
		UVScale.Value = float2.One;
	}

	protected override void PrepareAssetUpdateData()
	{
		segments = MathX.Clamp(Segments.Value, 3, 4096);
		rings = MathX.Clamp(Rings.Value, 3, 4096);
	}

	protected override void ClearMeshData()
	{
		uvsphere = null;
	}

	protected override void UpdateMeshData(MeshX meshx)
	{
		bool value = false;
		if (uvsphere == null || uvsphere.Rings != rings || uvsphere.Segments != segments || uvsphere.ShadingType != (UVSphereCapsule.Shading)Shading)
		{
			if (uvsphere != null)
			{
				uvsphere.Remove();
			}
			uvsphere = new UVSphereCapsule(meshx, rings, segments, Shading);
			value = true;
		}
		uvsphere.UVScale = UVScale.Value;
		uvsphere.Radius = Radius;
		uvsphere.Update();
		uploadHint[MeshUploadHint.Flag.Geometry] = value;
	}

	public override void CreateCollider()
	{
		base.Slot.AttachComponent<SphereCollider>().Radius.DriveFrom(Radius);
	}

	protected override void OnLoading(DataTreeNode node, LoadControl control)
	{
		base.OnLoading(node, control);
		if (control.GetTypeVersion(typeof(SphereMesh)) == 0)
		{
			RunSynchronously(delegate
			{
				Sync<float2> uVScale = UVScale;
				uVScale.Value *= new float2(-1f, 1f);
			});
		}
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		Radius = new Sync<float>();
		Segments = new Sync<int>();
		Rings = new Sync<int>();
		Shading = new Sync<UVSphereCapsule.Shading>();
		UVScale = new Sync<float2>();
		DualSided = new Sync<bool>();
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
			7 => Radius, 
			8 => Segments, 
			9 => Rings, 
			10 => Shading, 
			11 => UVScale, 
			12 => DualSided, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static SphereMesh __New()
	{
		return new SphereMesh();
	}
}
