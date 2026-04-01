using System;
using System.Threading.Tasks;
using BepuPhysics;
using BepuPhysics.Collidables;
using Elements.Core;
using FrooxEngine.UIX;

namespace FrooxEngine;

[Category(new string[] { "Physics/Colliders" })]
public class BoxCollider : PrimitiveShapeCollider<Box>, IHighlightable, IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable
{
	public readonly Sync<float3> Size;

	public override bool HasBoundingBox => true;

	public override bool IsBoundingBoxAvailable => true;

	public override BoundingBox LocalBoundingBox => BoundingBox.CenterSize(base.LocalBoundsOffset, (float3)Size);

	public override BoundingBox GlobalBoundingBox => LocalBoundingBox.Transform(base.Slot.LocalToGlobal);

	protected override bool ShapeChanged
	{
		get
		{
			if (!base.ShapeChanged)
			{
				return Size.WasChanged;
			}
			return true;
		}
	}

	protected override void OnAwake()
	{
		base.OnAwake();
		Size.Value = float3.One;
	}

	public override Task ForeachExactBoundedPoint(Slot space, Action<float3> action)
	{
		BoundingBox boundingBox = BoundingBox.CenterSize((float3)Offset, (float3)Size);
		for (int i = 0; i < 8; i++)
		{
			float3 localPoint = boundingBox.GetVertexPoint(i);
			localPoint = base.Slot.LocalPointToSpace(in localPoint, space);
			action(localPoint);
		}
		return Task.CompletedTask;
	}

	protected override void ClearShapeChanged()
	{
		base.ClearShapeChanged();
		Size.WasChanged = false;
	}

	protected override Box CreateShape(PhysicsSimulation simulation, ref float3 offset, ref float speculativeMargin, float? mass, out BodyInertia inertia)
	{
		float3 size = Size.Value;
		ProcessColliderSize(ref size);
		speculativeMargin = MathX.Min(speculativeMargin, MathX.Max(size.x, size.y, size.z));
		Box result = new Box(size.x, size.y, size.z);
		if (!mass.HasValue)
		{
			inertia = default(BodyInertia);
		}
		else
		{
			result.ComputeInertia(mass.Value, out inertia);
		}
		return result;
	}

	protected override void ComputeInertia(PhysicsSimulation simulation, ref float3 offset, float mass, out BodyInertia inertia)
	{
		float speculativeMargin = 0f;
		CreateShape(null, ref offset, ref speculativeMargin, mass, out inertia);
	}

	public Slot GenerateHighlight(Slot root, IAssetProvider<Material> highlightMaterial, bool trackPosition = true)
	{
		Slot slot = root.AddSlot(base.Slot.Name);
		if (trackPosition)
		{
			slot.SetupCopyTransform(base.Slot);
		}
		else
		{
			slot.CopyTransform(base.Slot);
		}
		Slot slot2 = slot.AddSlot("Offset");
		slot2.AttachMesh<BoxMesh>(highlightMaterial).Size.DriveFrom(Size);
		slot2.Position_Field.DriveFrom(Offset);
		return slot;
	}

	public override void BuildInspectorUI(UIBuilder ui)
	{
		base.BuildInspectorUI(ui);
		ui.Button("Inspector.Collider.SetFromLocalBounds".AsLocaleKey(), SetFromLocalBounds);
		ui.Button("Inspector.Collider.SetFromGlobalBounds".AsLocaleKey(), SetFromGlobalBounds);
		ui.Button("Inspector.Collider.SetFromLocalBoundsPrecise".AsLocaleKey(), SetFromLocalBounds);
		ui.Button("Inspector.Collider.SetFromGlobalBoundsPrecise".AsLocaleKey(), SetFromGlobalBounds);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void SetFromLocalBounds(IButton button, ButtonEventData eventData)
	{
		SetFromLocalBounds();
	}

	[SyncMethod(typeof(Delegate), null)]
	private void SetFromGlobalBounds(IButton button, ButtonEventData eventData)
	{
		SetFromGlobalBounds();
	}

	[SyncMethod(typeof(Action), new string[] { })]
	public void SetFromLocalBounds()
	{
		SetFromBounds(base.Slot);
	}

	[SyncMethod(typeof(Action), new string[] { })]
	public void SetFromGlobalBounds()
	{
		SetFromBounds(base.World.RootSlot);
	}

	[SyncMethod(typeof(Action), new string[] { })]
	public void SetFromLocalBoundsPrecise()
	{
		StartTask(SetFromPreciseBounds, base.Slot);
	}

	[SyncMethod(typeof(Action), new string[] { })]
	public void SetFromGlobalBoundsPrecise()
	{
		StartTask(SetFromPreciseBounds, base.World.RootSlot);
	}

	private void SetFromBounds(Slot root)
	{
		BoundingBox boundingBox = base.Slot.ComputeBoundingBox(includeInactive: false, root, (IBounded b) => b != this);
		if (boundingBox.IsValid)
		{
			if (root != base.Slot)
			{
				boundingBox.Transform(root.GetLocalToSpaceMatrix(base.Slot));
			}
			Offset.Value = boundingBox.Center;
			Size.Value = boundingBox.Size;
		}
	}

	private async Task SetFromPreciseBounds(Slot root)
	{
		await default(ToBackground);
		BoundingBox bounds = await base.Slot.ComputeExactBounds(includeInactive: false, root, (IBounded b) => b != this).ConfigureAwait(continueOnCapturedContext: false);
		if (bounds.IsValid)
		{
			await default(ToWorld);
			if (root != base.Slot)
			{
				bounds.Transform(root.GetLocalToSpaceMatrix(base.Slot));
			}
			Offset.Value = bounds.Center;
			Size.Value = bounds.Size;
		}
	}

	public override void DebugVisualize(in colorX color, float duration = 0f)
	{
		base.Debug.Box(base.Slot.LocalPointToGlobal((float3)Offset), base.Slot.LocalScaleToGlobal((float3)Size), in color, base.Slot.GlobalRotation, duration);
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		Size = new Sync<float3>();
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
			8 => Size, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static BoxCollider __New()
	{
		return new BoxCollider();
	}
}
