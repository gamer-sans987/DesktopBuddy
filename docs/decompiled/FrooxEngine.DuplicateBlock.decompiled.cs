using System;

namespace FrooxEngine;

[Category(new string[] { "Transform/Tagging" })]
public class DuplicateBlock : Component, IDuplicateBlock, IInteractionBlock, IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable
{
	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static DuplicateBlock __New()
	{
		return new DuplicateBlock();
	}
}
