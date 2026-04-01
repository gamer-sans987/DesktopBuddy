using System;
using Elements.Core;

namespace FrooxEngine;

[Category(new string[] { "Input/Interaction" })]
public class PointTouchSource : RaycastTouchSource
{
	public readonly Sync<float3> Offset;

	public readonly Sync<float3> Direction;

	public readonly Sync<float> MaxDistance;

	public override float3 RayDirection => base.Slot.LocalDirectionToGlobal((float3)Direction);

	public override float RayLength => MaxDistance;

	public override float3 RayOrigin => base.Slot.LocalPointToGlobal((float3)Offset);

	public override TouchType TouchType => TouchType.Remote;

	public override float3 TipPosition => float3.Zero;

	protected override void OnAwake()
	{
		base.OnAwake();
		Direction.Value = float3.Forward;
		MaxDistance.Value = float.MaxValue;
	}

	public override bool IsTouching(ITouchable touchable, RaycastHit hit)
	{
		return IsForceTouching(touchable);
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		Offset = new Sync<float3>();
		Direction = new Sync<float3>();
		MaxDistance = new Sync<float>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => AutoUpdateUser, 
			4 => OutOfSightAngle, 
			5 => MaxTouchPenetrationDistance, 
			6 => CustomFilter, 
			7 => Offset, 
			8 => Direction, 
			9 => MaxDistance, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static PointTouchSource __New()
	{
		return new PointTouchSource();
	}
}
