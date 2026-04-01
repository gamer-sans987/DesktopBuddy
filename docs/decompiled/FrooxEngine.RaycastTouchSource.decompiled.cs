using System;
using System.Collections.Generic;
using Elements.Core;

namespace FrooxEngine;

public abstract class RaycastTouchSource : TouchSource
{
	public readonly SyncDelegate<Func<ICollider, int, bool>> CustomFilter;

	public RaycastHit CurrentClosestHit { get; private set; }

	public abstract float3 RayOrigin { get; }

	public abstract float3 RayDirection { get; }

	public abstract float RayLength { get; }

	public override float3 TipDirection => RayDirection;

	public abstract bool IsTouching(ITouchable touchable, RaycastHit hit);

	protected override ITouchable GetTouchable(out float3 point, out float3 direction, out float3 directHitPoint, out bool touch)
	{
		float3 origin = RayOrigin;
		float3 direction2 = RayDirection;
		float rayLength = RayLength;
		List<RaycastHit> list = Pool.BorrowList<RaycastHit>();
		List<RaycastPortalHit> list2 = Pool.BorrowList<RaycastPortalHit>();
		try
		{
			if (PhysicsManager.IsValidRaycast(in origin, in direction2, rayLength))
			{
				base.Physics.PortalRaycastAll(in origin, in direction2, rayLength, list, list2, CustomFilter.Target);
			}
		}
		catch (Exception ex)
		{
			base.Debug.Warning("Exception during portal raycast on " + this.ParentHierarchyToString() + ": " + ex);
		}
		CurrentClosestHit = default(RaycastHit);
		ITouchable result = null;
		point = float3.Zero;
		directHitPoint = float3.Zero;
		direction = float3.Zero;
		touch = false;
		if (list.Count > 0)
		{
			CurrentClosestHit = list[0];
		}
		for (int i = 0; i < list.Count; i++)
		{
			RaycastHit hit = list[i];
			if (hit.Distance - CurrentClosestHit.Distance > (float)MaxTouchPenetrationDistance)
			{
				break;
			}
			ITouchable componentInParentsUntilBlock = hit.Collider.Slot.GetComponentInParentsUntilBlock(base._touchableFilter);
			if (componentInParentsUntilBlock != null && componentInParentsUntilBlock.CanTouch(TouchType))
			{
				result = componentInParentsUntilBlock;
				point = hit.Point;
				direction = direction2;
				if (list2.Count > 0)
				{
					directHitPoint = list2[0].hit.Point;
					direction = list2[list2.Count - 1].exitDirection;
				}
				else
				{
					directHitPoint = point;
				}
				touch = IsTouching(componentInParentsUntilBlock, hit);
				break;
			}
		}
		Pool.Return(ref list);
		Pool.Return(ref list2);
		return result;
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		CustomFilter = new SyncDelegate<Func<ICollider, int, bool>>();
	}
}
