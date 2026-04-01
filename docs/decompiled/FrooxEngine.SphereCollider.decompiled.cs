using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BepuPhysics;
using BepuPhysics.Collidables;
using Elements.Core;
using FrooxEngine.UIX;

namespace FrooxEngine;

[Category(new string[] { "Physics/Colliders" })]
public class SphereCollider : PrimitiveShapeCollider<Sphere>, IHighlightable, IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable
{
	public readonly Sync<float> Radius;

	public override bool HasBoundingBox => true;

	public override bool IsBoundingBoxAvailable => true;

	public override BoundingBox LocalBoundingBox => BoundingBox.FromBoundingSphere(new BoundingSphere(base.LocalBoundsOffset, Radius));

	public override BoundingBox GlobalBoundingBox => BoundingBox.FromBoundingSphere(new BoundingSphere(base.Slot.LocalPointToGlobal((float3)Offset), base.Slot.LocalScaleToGlobal(Radius)));

	protected override bool ShapeChanged
	{
		get
		{
			if (!base.ShapeChanged)
			{
				return Radius.WasChanged;
			}
			return true;
		}
	}

	protected override void OnAwake()
	{
		base.OnAwake();
		Radius.Value = 0.5f;
	}

	public override Task ForeachExactBoundedPoint(Slot space, Action<float3> action)
	{
		for (float num = 0f; num < 1f; num += 1f / 128f)
		{
			for (float num2 = 0f; num2 < 1f; num2 += 1f / 128f)
			{
				float3 localPoint = MathX.PointOnUVSphere(new float2(num2, num), Radius) + (float3)Offset;
				localPoint = base.Slot.LocalPointToSpace(in localPoint, space);
				action(localPoint);
			}
		}
		return Task.CompletedTask;
	}

	protected override void ClearShapeChanged()
	{
		base.ClearShapeChanged();
		Radius.WasChanged = false;
	}

	protected override Sphere CreateShape(PhysicsSimulation simulation, ref float3 offset, ref float speculativeMargin, float? mass, out BodyInertia inertia)
	{
		float3 size = new float3(Radius.Value, Radius.Value, Radius.Value);
		ProcessColliderSize(ref size);
		float num = (size.x + size.y + size.z) * (1f / 3f);
		speculativeMargin = MathX.Min(num, speculativeMargin);
		Sphere result = new Sphere(num);
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
		slot2.AttachMesh<SphereMesh>(highlightMaterial).Radius.DriveFrom(Radius);
		slot2.Position_Field.DriveFrom(Offset);
		return slot;
	}

	public override void BuildInspectorUI(UIBuilder ui)
	{
		base.BuildInspectorUI(ui);
		ui.Button("Inspector.Collider.SetFromLocalBounds".AsLocaleKey(), SetFromLocalBounds);
		ui.Button("Inspector.Collider.SetFromGlobalBounds".AsLocaleKey(), SetFromGlobalBounds);
		ui.Button("Inspector.Collider.SetFromPreciseBounds".AsLocaleKey(), SetFromPreciseBounds);
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

	[SyncMethod(typeof(Delegate), null)]
	private void SetFromPreciseBounds(IButton button, ButtonEventData eventData)
	{
		SetFromPreciseBounds();
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
	public void SetFromPreciseBounds()
	{
		StartTask(SetFromPreciseBoundsAsync);
	}

	private void SetFromBounds(Slot root)
	{
		BoundingBox box = base.Slot.ComputeBoundingBox(includeInactive: false, root, (IBounded b) => b != this);
		if (box.IsValid)
		{
			if (root != base.Slot)
			{
				box.Transform(root.GetLocalToSpaceMatrix(base.Slot));
			}
			BoundingSphere boundingSphere = BoundingSphere.FromBoundingBox(box);
			Offset.Value = boundingSphere.center;
			Radius.Value = boundingSphere.radius;
		}
	}

	private async Task SetFromPreciseBoundsAsync()
	{
		await default(ToBackground);
		List<float3> points = new List<float3>();
		await base.Slot.ForeachExactBoundedPoint(delegate(float3 p)
		{
			points.Add(p);
		}, includeInactive: false, base.Slot, (IBounded b) => b != this).ConfigureAwait(continueOnCapturedContext: false);
		BoundingSphere sphere = BoundingSphere.RitterBoundingSphere(points);
		if (sphere.IsValid)
		{
			await default(ToWorld);
			Offset.Value = sphere.Center;
			Radius.Value = sphere.Radius;
		}
	}

	public override void DebugVisualize(in colorX color, float duration = 0f)
	{
		base.Debug.Sphere(base.Slot.LocalPointToGlobal((float3)Offset), base.Slot.LocalScaleToGlobal(Radius), in color, 2, duration);
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		Radius = new Sync<float>();
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
			8 => Radius, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static SphereCollider __New()
	{
		return new SphereCollider();
	}
}
