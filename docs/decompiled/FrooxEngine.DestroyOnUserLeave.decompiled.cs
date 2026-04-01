using System;

namespace FrooxEngine;

[Category(new string[] { "Transform" })]
public class DestroyOnUserLeave : Component
{
	public readonly UserRef TargetUser;

	public override void OnUserLeft(User user)
	{
		if (base.World.IsAuthority && user == TargetUser.RawTarget)
		{
			base.Slot.Destroy();
		}
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		TargetUser = new UserRef();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => TargetUser, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static DestroyOnUserLeave __New()
	{
		return new DestroyOnUserLeave();
	}
}
