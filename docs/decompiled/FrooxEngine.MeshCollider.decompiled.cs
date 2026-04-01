using System;
using System.Linq;
using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using Elements.Assets;
using Elements.Core;
using FrooxEngine.UIX;

namespace FrooxEngine;

[Category(new string[] { "Physics/Colliders" })]
public class MeshCollider : MeshBasedCollider<BepuPhysics.Collidables.Mesh, MeshColliderData>, ICustomInspector, IWorker, IWorldElement
{
	public readonly Sync<MeshColliderSidedness> Sidedness;

	public readonly RawOutput<float> ActualSpeculativeMargin;

	private Vector3 _offset;

	public override int Version => 2;

	protected override bool ShapeChanged
	{
		get
		{
			if (!base.ShapeChanged)
			{
				return Sidedness.WasChanged;
			}
			return true;
		}
	}

	protected override bool EntityChanged => Offset.WasChanged;

	protected override bool ListenToEvents => false;

	protected override MeshColliderData Data => Sidedness.Value switch
	{
		MeshColliderSidedness.Front => Mesh.Asset?.ColliderData, 
		_ => Mesh.Asset?.DualSidedColliderData, 
	};

	protected override void PostprocessContactMask(ref byte mask)
	{
		mask = (byte)(mask & -5);
	}

	protected override void ClearShapeChanged()
	{
		base.ClearShapeChanged();
		Sidedness.WasChanged = false;
	}

	protected override void RequestData(Mesh mesh)
	{
		switch (Sidedness.Value)
		{
		case MeshColliderSidedness.Front:
			mesh.RequestColliderData(this);
			break;
		default:
			mesh.RequestDualSidedColliderData(this);
			break;
		}
	}

	protected override void OnAwake()
	{
		base.OnAwake();
		Type.Value = ColliderType.NoCollision;
		Sidedness.Value = MeshColliderSidedness.Front;
	}

	public override void SetCharacterCollider()
	{
		base.SetCharacterCollider();
		Sidedness.Value = MeshColliderSidedness.Front;
	}

	public void BuildInspectorUI(UIBuilder ui)
	{
		WorkerInspector.BuildInspectorUI(this, ui);
		ui.Button("Inspector.MeshCollider.VHACD".AsLocaleKey(), (colorX?)null, ConvexDecomposition);
		ui.Button("Inspector.MeshCollider.ReplaceBox".AsLocaleKey(), (colorX?)null, ReplaceWithBox);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void ConvexDecomposition(IButton button, ButtonEventData eventData)
	{
		if (Mesh.Asset != null)
		{
			Slot slot = base.LocalUserSpace.AddSlot("V-HACD Wizard");
			VHACD_Dialog vHACD_Dialog = slot.AttachComponent<VHACD_Dialog>();
			slot.PositionInFrontOfUser(float3.Backward);
			vHACD_Dialog.TargetCollider.Target = this;
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void ReplaceWithBox(IButton button, ButtonEventData eventData)
	{
		if (Mesh.Asset != null)
		{
			BoxCollider boxCollider = base.Slot.AttachComponent<BoxCollider>();
			boxCollider.Size.Value = Mesh.Asset.Bounds.Size;
			boxCollider.Offset.Value = Mesh.Asset.Bounds.Center;
			boxCollider.Type.Value = Type.Value;
			boxCollider.CharacterCollider.Value = CharacterCollider.Value;
			boxCollider.Mass.Value = Mass.Value;
			boxCollider.IgnoreRaycasts.Value = IgnoreRaycasts.Value;
			Destroy();
		}
	}

	protected override void OnLoading(DataTreeNode node, LoadControl control)
	{
		base.OnLoading(node, control);
		int typeVersion = control.GetTypeVersion<MeshCollider>();
		if (typeVersion == 0 && !CharacterCollider.Value)
		{
			control.OnLoaded(this, delegate
			{
				Type.Value = ColliderType.NoCollision;
			});
		}
		if (typeVersion >= 2)
		{
			return;
		}
		control.OnLoaded(this, delegate
		{
			if (Type.Value == ColliderType.Active)
			{
				Type.Value = ColliderType.Static;
				UniLog.Log($"Switching active mesh collider to static:\n{this}");
			}
			UpgradeTrigger();
			Sidedness.Value = MeshColliderSidedness.DualSided;
			MeshRenderer component = base.Slot.GetComponent((MeshRenderer m) => m.Mesh.Target == Mesh.Target);
			if ((component == null || !component.Materials.Any((IAssetProvider<Material> m) => m is ICullingMaterial cullingMaterial && cullingMaterial.Culling == Culling.Off)) && !(base.Slot.GlobalRotation.Inverted * base.Slot.LocalVectorToGlobal(float3.One) < 0f).Any())
			{
				Sidedness.Value = MeshColliderSidedness.Front;
			}
		});
	}

	protected override void CreateMeshShape(PhysicsSimulation simulation, out MeshColliderData data, out BepuPhysics.Collidables.Mesh shape, out int version, ref float3 offset, ref float speculativeMargin, float? mass, out BodyInertia inertia)
	{
		data = Data;
		data.GetData(out shape, out version);
		float3 v = ComputeColliderScale();
		shape.Scale = v;
		MeshMetadata metadata = data.Mesh.Metadata;
		if (metadata != null && metadata.LargestTriangleSize > 0f)
		{
			speculativeMargin = MathX.Min(speculativeMargin, MathX.Max(0f, metadata.MedianTriangleSize * MathX.AvgComponent(in v) - 0.1f));
		}
		else if (data.Mesh.Data.VertexCount > 10000)
		{
			speculativeMargin = 0f;
		}
		ActualSpeculativeMargin.Value = speculativeMargin;
		_offset = MathX.FilterInvalid(offset);
		offset = float3.Zero;
		inertia = default(BodyInertia);
	}

	protected override void ProcessPose(ref RigidPose rigidPose)
	{
		rigidPose.Position += Vector3.Transform(_offset, rigidPose.Orientation);
	}

	protected override void ComputeInertia(PhysicsSimulation simulation, ref float3 offset, float mass, out BodyInertia inertia)
	{
		inertia = default(BodyInertia);
	}

	public override string ToString()
	{
		return $"TargetMesh: {Mesh.Target}\n{base.ToString()}";
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		Sidedness = new Sync<MeshColliderSidedness>();
		ActualSpeculativeMargin = new RawOutput<float>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => Offset, 
			4 => Type, 
			5 => Mass, 
			6 => CharacterCollider, 
			7 => IgnoreRaycasts, 
			8 => Mesh, 
			9 => Sidedness, 
			10 => ActualSpeculativeMargin, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static MeshCollider __New()
	{
		return new MeshCollider();
	}
}
