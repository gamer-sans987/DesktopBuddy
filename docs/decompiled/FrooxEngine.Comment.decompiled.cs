using System;

namespace FrooxEngine;

public class Comment : Component
{
	public readonly Sync<string> Text;

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		Text = new Sync<string>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => Text, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static Comment __New()
	{
		return new Comment();
	}
}
