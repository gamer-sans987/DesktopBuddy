namespace FrooxEngine;

public abstract class Component : ComponentBase<Component>, IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable
{
	public Slot Slot { get; private set; }

	public bool IsUnderLocalUser => Slot?.IsUnderLocalUser ?? false;

	protected override bool CanRunUpdates => Slot.IsActive;

	internal override void Initialize(ContainerWorker<Component> container, bool isNew)
	{
		Slot = (Slot)container;
		base.Initialize(container, isNew);
	}

	public bool AssignKey(string key, int version = 0, bool onlyFree = false)
	{
		return base.World.RequestKey(this, key, version, onlyFree);
	}

	public bool HasKey(string key)
	{
		return base.World.KeyOwner(key) == this;
	}

	public void RemoveKey(string key)
	{
		base.World.RemoveKey(this, key);
	}

	internal void KeyAssigned(string key)
	{
		if (base.IsStarted)
		{
			MarkChangeDirty();
		}
	}

	internal void KeyRemoved(string key)
	{
		if (base.IsStarted)
		{
			MarkChangeDirty();
		}
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
	}
}
