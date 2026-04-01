using System;
using System.Collections.Generic;
using System.IO;
using Elements.Core;

namespace FrooxEngine;

public abstract class SyncElement : ISyncElement, IWorldElement, ISyncMember, IChangeable, ILinkable, IInitializable
{
	protected enum InternalFlags
	{
		IsInitialized,
		IsDisposed,
		IsLocal,
		IsSyncDirty,
		WasChanged,
		IsInInitPhase,
		HasInitializableChildren,
		NonPersistent,
		IsDrivable,
		IsWithinHookCallback,
		IsLoading,
		ModificationBlocked,
		DriveErrorLogged,
		END
	}

	protected uint _flags;

	private Worker _worker;

	private static Predicate<Worker> _workerFilter = WorkerParentFilter;

	private const int INVALIDATE_CHECK_FLAGS = 40;

	private ushort modificationLevel;

	private const int BEGIN_MODIFICATION_FLAGS = 1568;

	public World World { get; private set; }

	public Engine Engine => World.Engine;

	public TimeController Time => World.Time;

	protected bool GenerateSyncData
	{
		get
		{
			if (World.GenerateDeltaSyncData)
			{
				return !IsLocalElement;
			}
			return false;
		}
	}

	public string Name => Parent?.GetSyncMemberName(this);

	public string NameWithPath
	{
		get
		{
			string text = this.FindNearestParent<ICustomMemberNameSource>()?.GetMemberName(this);
			if (text != null)
			{
				return text;
			}
			text = Name;
			IWorldElement parent = Parent;
			while (parent != Worker && parent != null)
			{
				text = parent.Name + "." + text;
				parent = parent.Parent;
			}
			return text;
		}
	}

	public Worker Worker
	{
		get
		{
			if (_worker == null)
			{
				_worker = Parent.FindNearestParent(_workerFilter);
			}
			return _worker;
		}
	}

	public Slot Slot => Component?.Slot ?? (Worker as Slot);

	public Component Component => Worker as Component;

	public bool IsInitialized
	{
		get
		{
			return _flags.GetFlag(0);
		}
		private set
		{
			_flags.SetFlag(0, value);
		}
	}

	public bool IsDisposed
	{
		get
		{
			return _flags.GetFlag(1);
		}
		private set
		{
			_flags.SetFlag(1, value);
		}
	}

	public bool IsLocalElement
	{
		get
		{
			return _flags.GetFlag(2);
		}
		private set
		{
			_flags.SetFlag(2, value);
		}
	}

	public bool IsRemoved => IsDisposed;

	public bool IsSyncDirty
	{
		get
		{
			return _flags.GetFlag(3);
		}
		private set
		{
			_flags.SetFlag(3, value);
		}
	}

	public bool WasChanged
	{
		get
		{
			return _flags.GetFlag(4);
		}
		set
		{
			if (IsDisposed)
			{
				return;
			}
			if (World != null)
			{
				if (World.IsDisposed)
				{
					return;
				}
				World?.ConnectorManager.ThreadCheck();
			}
			SetWasChangedInternal(value);
		}
	}

	public bool DriveErrorLogged
	{
		get
		{
			return _flags.GetFlag(12);
		}
		set
		{
			_flags.SetFlag(12, value);
		}
	}

	public virtual bool CanWrite => true;

	public abstract bool IsConfirmed { get; }

	public RefID ReferenceID { get; private set; }

	public bool IsInInitPhase
	{
		get
		{
			return _flags.GetFlag(5);
		}
		private set
		{
			_flags.SetFlag(5, value);
		}
	}

	private bool HasInitializableChildren
	{
		get
		{
			return _flags.GetFlag(6);
		}
		set
		{
			_flags.SetFlag(6, value);
		}
	}

	public IWorldElement Parent { get; private set; }

	public bool IsPersistent
	{
		get
		{
			if (!NonPersistent)
			{
				return Parent.IsPersistent;
			}
			return false;
		}
	}

	internal bool NonPersistent
	{
		get
		{
			return _flags.GetFlag(7);
		}
		set
		{
			_flags.SetFlag(7, value);
		}
	}

	public abstract bool IsValid { get; }

	public bool IsDrivable
	{
		get
		{
			return _flags.GetFlag(8);
		}
		private set
		{
			_flags.SetFlag(8, value);
		}
	}

	public bool IsLinked => ActiveLink != null;

	public virtual bool IsDriven
	{
		get
		{
			ILinkRef activeLink = ActiveLink;
			if (activeLink != null && activeLink.IsDriving)
			{
				return IsDrivable;
			}
			return false;
		}
	}

	public bool IsHooked => ActiveLink?.IsHooking ?? false;

	public bool IsBlockedByDrive
	{
		get
		{
			if (IsDriven)
			{
				return !IsHooked;
			}
			return false;
		}
	}

	public ILinkRef DirectLink { get; protected set; }

	public ILinkRef InheritedLink { get; protected set; }

	public ILinkRef ActiveLink => InheritedLink ?? DirectLink;

	public abstract IEnumerable<ILinkable> LinkableChildren { get; }

	public bool IsWithinHookCallback
	{
		get
		{
			return _flags.GetFlag(9);
		}
		private set
		{
			_flags.SetFlag(9, value);
		}
	}

	public bool IsLoading
	{
		get
		{
			return _flags.GetFlag(10);
		}
		internal set
		{
			_flags.SetFlag(10, value);
		}
	}

	private bool ModificationBlocked
	{
		get
		{
			return _flags.GetFlag(11);
		}
		set
		{
			_flags.SetFlag(11, value);
		}
	}

	/// <remarks>
	/// This is an out of data model event and shouldn't be used for anything data model related
	/// </remarks>
	public event Action<IChangeable> Changed;

	private static bool WorkerParentFilter(Worker w)
	{
		if (!(w is Component) && !(w is UserComponent) && !(w is Stream))
		{
			return w is Slot;
		}
		return true;
	}

	private void SetWasChangedInternal(bool value)
	{
		_flags.SetFlag(4, value);
	}

	public bool GetWasChangedAndClear()
	{
		if (WasChanged)
		{
			WasChanged = false;
			return true;
		}
		return false;
	}

	public void EndInitPhase()
	{
		if (!IsInInitPhase)
		{
			throw new Exception("Initialization phase already ended");
		}
		if (HasInitializableChildren)
		{
			World.UpdateManager.EndInitPhaseInChildren(this);
			HasInitializableChildren = false;
		}
		IsInInitPhase = false;
	}

	protected void RegisterNewInitializable(IInitializable initializable)
	{
		HasInitializableChildren = true;
		World.UpdateManager.AddInitializableChild(this, initializable);
	}

	public SyncElement()
	{
		IsDrivable = true;
	}

	public virtual void Initialize(World owner, IWorldElement parent)
	{
		World = owner;
		Parent = parent;
		IsInInitPhase = true;
		ReferenceID = World.ReferenceController.AllocateID();
		if (ReferenceID.IsLocalID)
		{
			IsLocalElement = true;
		}
		World.ReferenceController.RegisterReference(this);
		IsInitialized = true;
		WasChanged = true;
	}

	public virtual void Dispose()
	{
		if (!IsDisposed)
		{
			World.ReferenceController.UnregisterReference(this);
			this.Changed = null;
			World = null;
			Parent = null;
			IsDisposed = true;
		}
	}

	public DataTreeNode Save(SaveControl control)
	{
		DataTreeDictionary dataTreeDictionary = new DataTreeDictionary();
		dataTreeDictionary.Add("ID", control.SaveReference(ReferenceID));
		dataTreeDictionary.Add("Data", InternalSave(control));
		return dataTreeDictionary;
	}

	public void Load(DataTreeNode node, LoadControl control)
	{
		DataTreeDictionary dataTreeDictionary = (DataTreeDictionary)node;
		control.AssociateReference(ReferenceID, dataTreeDictionary["ID"]);
		IsLoading = true;
		InternalLoad(dataTreeDictionary["Data"], control);
		IsLoading = false;
	}

	protected abstract DataTreeNode InternalSave(SaveControl control);

	protected abstract void InternalLoad(DataTreeNode node, LoadControl control);

	internal void MarkPersistent()
	{
		NonPersistent = false;
	}

	internal void MarkNonPersistent()
	{
		NonPersistent = true;
	}

	public abstract MessageValidity Validate(BinaryMessageBatch syncMessage, BinaryReader reader, List<ValidationGroup.Rule> rules);

	public abstract void Invalidate();

	public abstract void Confirm(ulong confirmSyncTime);

	protected abstract void InternalCopy(ISyncMember source, Action<ISyncMember, ISyncMember> copy);

	public abstract string GetSyncMemberName(ISyncMember member);

	public void CopyValues(ISyncMember target, Action<ISyncMember, ISyncMember> copy)
	{
		if (GetType() != target.GetType())
		{
			throw new ArgumentException($"Type mismatch for Sync Member copy operation. Source: {GetType()}, Target: {target.GetType()}");
		}
		InternalCopy(target, copy);
	}

	public void CopyValues(ISyncMember target)
	{
		CopyValues(target, delegate(ISyncMember from, ISyncMember to)
		{
			to.CopyValues(from);
		});
	}

	public virtual void EncodeFull(BinaryWriter writer, BinaryMessageBatch outboundMessage)
	{
		if (!World.IsAuthority)
		{
			throw new Exception("Guest shouldn't do a full encode!");
		}
		if (IsSyncDirty)
		{
			throw new Exception("Cannot do a full encode on a dirty element!");
		}
		InternalEncodeFull(writer, outboundMessage);
	}

	public virtual void DecodeFull(BinaryReader reader, BinaryMessageBatch inboundMessage)
	{
		if (World.IsAuthority)
		{
			throw new Exception("Host shouldn't do a full decode!");
		}
		IsLoading = true;
		InternalDecodeFull(reader, inboundMessage);
		InternalClearDirty();
		IsLoading = false;
		SyncElementChanged();
	}

	public virtual void EncodeDelta(BinaryWriter writer, BinaryMessageBatch outboundMessage)
	{
		InternalEncodeDelta(writer, outboundMessage);
		IsSyncDirty = false;
		InternalClearDirty();
	}

	public virtual void DecodeDelta(BinaryReader reader, BinaryMessageBatch inboundMessage)
	{
		if (IsSyncDirty)
		{
			throw new Exception("Cannot apply delta update to a dirty sync element!\n" + this.ParentHierarchyToString());
		}
		IsLoading = true;
		InternalDecodeDelta(reader, inboundMessage);
		IsLoading = false;
	}

	protected abstract void InternalEncodeFull(BinaryWriter writer, BinaryMessageBatch outboundMessage);

	protected abstract void InternalDecodeFull(BinaryReader reader, BinaryMessageBatch inboundMessage);

	protected abstract void InternalEncodeDelta(BinaryWriter writer, BinaryMessageBatch outboundMessage);

	protected abstract void InternalDecodeDelta(BinaryReader reader, BinaryMessageBatch inboundMessage);

	protected abstract void InternalClearDirty();

	internal void MarkNonDrivable()
	{
		IsDrivable = false;
	}

	public virtual void Link(ILinkRef link)
	{
		DirectLink = link;
		if (link == ActiveLink)
		{
			UpdateLinkHierarchy(link);
		}
		SyncElementChanged();
	}

	public virtual void InheritLink(ILinkRef link)
	{
		InheritedLink = link;
		UpdateLinkHierarchy(link);
		SyncElementChanged();
	}

	public virtual void ReleaseLink(ILinkRef link)
	{
		if (DirectLink == link)
		{
			DirectLink = null;
			UpdateLinkHierarchy(link);
			SyncElementChanged();
		}
	}

	public virtual void ReleaseInheritedLink(ILinkRef link)
	{
		if (InheritedLink != link)
		{
			throw new Exception("The link that's being released isn't the one that's currently inherited");
		}
		InheritedLink = null;
		UpdateLinkHierarchy(link);
		SyncElementChanged();
	}

	protected void UpdateLinkHierarchy(ILinkRef changedLink)
	{
		if (IsDisposed)
		{
			return;
		}
		if (changedLink.WasLinkGranted && changedLink.IsDriving)
		{
			World.LinkManager.DriveReleased(this);
			Invalidate();
		}
		IEnumerable<ILinkable> linkableChildren = LinkableChildren;
		if (linkableChildren == null)
		{
			return;
		}
		if (IsLinked)
		{
			foreach (ILinkable item in linkableChildren)
			{
				item.InheritLink(ActiveLink);
			}
			return;
		}
		foreach (ILinkable item2 in linkableChildren)
		{
			item2.ReleaseInheritedLink(changedLink);
		}
	}

	public void InvalidateSyncElement()
	{
		if ((0x28 & _flags) == 0 && IsValid && (!IsDriven || !ActiveLink.WasLinkGranted))
		{
			IsSyncDirty = true;
			World.SyncController.AddDirtySyncElement(this);
		}
	}

	public void SyncElementChanged(IChangeable member = null)
	{
		member = member ?? this;
		try
		{
			Parent?.ChildChanged(member);
			this.Changed?.Invoke(member);
		}
		catch (Exception value)
		{
			UniLog.Error($"Exception running SyncElementChanged. On element:\n{this.ParentHierarchyToString()}\nException:\n{value}");
		}
		if (member == this)
		{
			SetWasChangedInternal(value: true);
		}
	}

	protected void BeginHook()
	{
		if (IsWithinHookCallback)
		{
			throw new Exception("Already within a hook callback!");
		}
		IsWithinHookCallback = true;
	}

	protected void EndHook()
	{
		if (!IsWithinHookCallback)
		{
			throw new Exception("Not within a  hook callback!");
		}
		IsWithinHookCallback = false;
	}

	protected void BlockModification()
	{
		if (ModificationBlocked)
		{
			throw new Exception("Modification is already blocked, cannot block again!");
		}
		ModificationBlocked = true;
	}

	protected void UnblockModification()
	{
		if (!ModificationBlocked)
		{
			throw new Exception("Modification isn't blocked, cannot unblock!");
		}
		ModificationBlocked = false;
	}

	protected bool BeginModification(bool throwOnError = true)
	{
		if (ModificationBlocked)
		{
			throw new Exception("Modification of the element is currently blocked, cannot modify");
		}
		if (modificationLevel == 0)
		{
			if (IsDisposed)
			{
				string message = "Cannot modify disposed elements! Hierachy: \n" + this.ParentHierarchyToString();
				if (throwOnError)
				{
					throw new Exception(message);
				}
				UniLog.Error(message);
				return false;
			}
			World.ConnectorManager.ThreadCheck();
			if (IsDriven && ActiveLink.WasLinkGranted && (_flags & 0x620) == 0 && !ActiveLink.IsModificationAllowed)
			{
				SyncElement syncElement = ActiveLink as SyncElement;
				string text = null;
				if (throwOnError || !DriveErrorLogged)
				{
					text = $"The {Name} ({ReferenceID} - {GetType()}) element on {Component?.GetType()} ({Component?.ReferenceID}) is currently being driven by {ActiveLink?.Name} ({ActiveLink.ReferenceID} - {ActiveLink?.GetType()}) on {syncElement?.Component?.GetType()} ({syncElement?.Component?.ReferenceID})" + " and can be modified only through the drive reference.";
				}
				if (throwOnError)
				{
					throw new Exception(text);
				}
				if (text != null)
				{
					DriveErrorLogged = true;
					UniLog.Warning(text, stackTrace: true);
				}
				return false;
			}
		}
		modificationLevel++;
		return true;
	}

	protected void EndModification()
	{
		if (modificationLevel == 0)
		{
			throw new Exception("The element isn't currently in modification state, cannot end it.");
		}
		modificationLevel--;
	}

	protected void RegisterChildElement(ISyncMember element)
	{
		if (IsLinked)
		{
			element.InheritLink(ActiveLink);
		}
	}

	protected void UnregisterChildElement(ISyncMember element)
	{
	}

	private void ChildSyncElementChanged(IChangeable member)
	{
		SyncElementChanged(member);
	}

	void IWorldElement.ChildChanged(IWorldElement child)
	{
		SyncElementChanged(child as IChangeable);
	}

	internal virtual void PrepareDestroy()
	{
	}
}
