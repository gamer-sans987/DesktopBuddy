using System;
using System.Collections.Generic;
using Elements.Core;
using Elements.Data;
using FrooxEngine.Undo;

namespace FrooxEngine;

[Category(new string[] { "Transform/Interaction" })]
[SingleInstancePerSlot]
public class Grabbable : Component, IGrabbable, IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable, IInteractionTarget, IObjectRoot
{
	public readonly Sync<bool> ReparentOnRelease;

	public readonly Sync<bool> PreserveUserSpace;

	public readonly Sync<bool> DestroyOnRelease;

	public readonly Sync<int> GrabPriority;

	public readonly Sync<int?> GrabPriorityWhenGrabbed;

	public readonly SyncDelegate<GrabCheck> CustomCanGrabCheck;

	public readonly Sync<bool> EditModeOnly;

	public readonly Sync<bool> AllowSteal;

	public readonly Sync<bool> DropOnDisable;

	public readonly Sync<ActiveUserHandling> ActiveUserFilter;

	public readonly SyncList<UserRef> OnlyUsers;

	[OldName("_scalable")]
	public readonly Sync<bool> Scalable;

	public readonly Sync<bool> Receivable;

	[OldName("Physical")]
	public readonly Sync<bool> AllowOnlyPhysicalGrab;

	protected readonly SyncRef<Grabber> _grabber;

	protected readonly SyncRef<Slot> _lastParent;

	protected readonly Sync<bool> _lastParentIsUserSpace;

	[OldName("ActiveUserRootOnly")]
	[NonPersistent]
	protected readonly Sync<bool> __legacyActiveUserRootOnly;

	public override int Version => 2;

	int IGrabbable.GrabPriority
	{
		get
		{
			if (!IsGrabbed || !GrabPriorityWhenGrabbed.Value.HasValue)
			{
				return GrabPriority.Value;
			}
			return GrabPriorityWhenGrabbed.Value.Value;
		}
	}

	bool IGrabbable.Scalable => Scalable;

	bool IGrabbable.Receivable => Receivable;

	bool IGrabbable.AllowOnlyPhysicalGrab => AllowOnlyPhysicalGrab;

	public Grabber Grabber => _grabber.Target;

	public bool IsGrabbed => Grabber != null;

	public virtual int InteractionTargetPriority => 0;

	public event Action<IGrabbable> OnLocalGrabbed;

	public event Action<IGrabbable> OnLocalReleased;

	public InteractionDescription GetInteractionDescription(InteractionLaser laser)
	{
		return laser.GetGrabInteractionDescription(IsGrabbed);
	}

	public bool CanGrab(Grabber grabber)
	{
		if (IsRemoved)
		{
			return false;
		}
		if (!base.Enabled)
		{
			return false;
		}
		if (IsGrabbed && (!AllowSteal.Value || Grabber.Slot.ActiveUserRoot?.ActiveUser == base.LocalUser))
		{
			return false;
		}
		if (EditModeOnly.Value && !base.LocalUser.EditMode)
		{
			return false;
		}
		if (!ActiveUserFilter.Value.CanGrab(base.Slot))
		{
			return false;
		}
		if (OnlyUsers.Count > 0)
		{
			bool flag = false;
			foreach (UserRef onlyUser in OnlyUsers)
			{
				if (onlyUser.Target == base.LocalUser)
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				return false;
			}
		}
		if (base.Slot.Position_Field.IsDriven && !base.Slot.Position_Field.IsHooked)
		{
			return false;
		}
		if (base.Slot.Rotation_Field.IsDriven && !base.Slot.Rotation_Field.IsHooked)
		{
			return false;
		}
		if ((bool)Scalable && base.Slot.Scale_Field.IsDriven && !base.Slot.Scale_Field.IsHooked)
		{
			return false;
		}
		if (CustomCanGrabCheck.Target != null && !CustomCanGrabCheck.Target(this, grabber))
		{
			return false;
		}
		if (!base.Permissions.Check(this, (GrabbablePermissions p) => p.CanGrab(this)))
		{
			return false;
		}
		return true;
	}

	public IGrabbable Grab(Grabber grabber, Slot holdSlot, bool supressEvents = false)
	{
		if (CanGrab(grabber))
		{
			if (FrooxEngine.Engine.IsAprilFools)
			{
				MysterySettings? activeSetting = Settings.GetActiveSetting<MysterySettings>();
				if (activeSetting != null && activeSetting.Difficulty.Value == MysterySettings.ResoniteDifficulty.Hard && RandomX.Chance(0.333333f))
				{
					Slot slot = base.Slot;
					slot.LocalPosition += RandomX.OnUnitSphere * 0.25f;
					return null;
				}
			}
			if (IsGrabbed)
			{
				Release(Grabber);
			}
			base.Slot.CreateTransformUndoState(parent: true);
			_lastParent.Target = base.Slot.Parent;
			_lastParentIsUserSpace.Value = base.Slot.Parent == base.LocalUserSpace;
			_grabber.Target = grabber;
			base.Slot.SetParent(holdSlot);
			if (!supressEvents)
			{
				RunGrabEvent(released: false);
			}
			return this;
		}
		return null;
	}

	public void Release(Grabber grabber, bool supressEvents = false)
	{
		if (_grabber.Target == grabber)
		{
			_grabber.Target = null;
			Slot slot = null;
			if (ReparentOnRelease.Value)
			{
				slot = _lastParent.Target;
			}
			if ((bool)PreserveUserSpace && _lastParentIsUserSpace.Value)
			{
				slot = base.LocalUserSpace;
			}
			if (slot != null && slot.IsChildOf(base.Slot))
			{
				slot = base.LocalUserSpace;
			}
			IGrabbableReparentBlock grabbableReparentBlock = slot?.GetComponentInParentsUntilBlock<IGrabbableReparentBlock>();
			if (grabbableReparentBlock != null && grabbableReparentBlock.DontReparent && slot.HierachyDepth - grabbableReparentBlock.Slot.HierachyDepth <= grabbableReparentBlock.MaxDepth)
			{
				slot = base.LocalUserSpace;
			}
			base.Slot.SetParent(slot);
			if (!supressEvents)
			{
				RunGrabEvent(released: true);
			}
			if (DestroyOnRelease.Value)
			{
				base.Slot.Destroy();
			}
		}
	}

	private void RunGrabEvent(bool released)
	{
		try
		{
			List<IGrabEventReceiver> list = Pool.BorrowList<IGrabEventReceiver>();
			base.Slot.GetComponents(list);
			base.World.RootSlot.GetComponents(list);
			foreach (IGrabEventReceiver item in list)
			{
				if (released)
				{
					item.OnReleased(this);
				}
				else
				{
					item.OnGrabbed(this);
				}
			}
			Pool.Return(ref list);
			if (released)
			{
				this.OnLocalReleased?.Invoke(this);
			}
			else
			{
				this.OnLocalGrabbed?.Invoke(this);
			}
		}
		catch (Exception value)
		{
			base.Debug.Error($"Exception running {(released ? "OnReleased" : "OnGrabbed")} event on {this}:\n{value}");
		}
	}

	protected override void OnAwake()
	{
		ReparentOnRelease.Value = true;
		PreserveUserSpace.Value = true;
		DropOnDisable.Value = true;
		Receivable.Value = true;
	}

	protected override void OnPaste()
	{
		base.Slot.ForeachComponent(delegate(IGrabEventReceiver r)
		{
			r.OnReleased(this);
		});
		if (DestroyOnRelease.Value)
		{
			RunSynchronously(base.Slot.Destroy);
		}
	}

	protected override void OnDuplicate()
	{
		base.OnDuplicate();
		if (IsGrabbed)
		{
			Release(Grabber);
		}
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();
		if (IsGrabbed && (bool)DropOnDisable)
		{
			Release(Grabber);
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	public static bool UserRootGrabCheck(IGrabbable grabbable, Grabber grabber)
	{
		return grabbable.Slot.ActiveUserRoot == null;
	}

	protected override void OnLoading(DataTreeNode node, LoadControl control)
	{
		base.OnLoading(node, control);
		if (control.GetTypeVersion<Grabbable>() == 0 && !Receivable.Value)
		{
			RunSynchronously(delegate
			{
				Receivable.Value = true;
			});
		}
		if (__legacyActiveUserRootOnly.Value)
		{
			RunSynchronously(delegate
			{
				ActiveUserFilter.Value = ActiveUserHandling.ActiveUserWhenPresent;
			});
		}
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		ReparentOnRelease = new Sync<bool>();
		PreserveUserSpace = new Sync<bool>();
		DestroyOnRelease = new Sync<bool>();
		GrabPriority = new Sync<int>();
		GrabPriorityWhenGrabbed = new Sync<int?>();
		CustomCanGrabCheck = new SyncDelegate<GrabCheck>();
		EditModeOnly = new Sync<bool>();
		AllowSteal = new Sync<bool>();
		DropOnDisable = new Sync<bool>();
		ActiveUserFilter = new Sync<ActiveUserHandling>();
		OnlyUsers = new SyncList<UserRef>();
		Scalable = new Sync<bool>();
		Receivable = new Sync<bool>();
		AllowOnlyPhysicalGrab = new Sync<bool>();
		_grabber = new SyncRef<Grabber>();
		_lastParent = new SyncRef<Slot>();
		_lastParentIsUserSpace = new Sync<bool>();
		__legacyActiveUserRootOnly = new Sync<bool>();
		__legacyActiveUserRootOnly.MarkNonPersistent();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => ReparentOnRelease, 
			4 => PreserveUserSpace, 
			5 => DestroyOnRelease, 
			6 => GrabPriority, 
			7 => GrabPriorityWhenGrabbed, 
			8 => CustomCanGrabCheck, 
			9 => EditModeOnly, 
			10 => AllowSteal, 
			11 => DropOnDisable, 
			12 => ActiveUserFilter, 
			13 => OnlyUsers, 
			14 => Scalable, 
			15 => Receivable, 
			16 => AllowOnlyPhysicalGrab, 
			17 => _grabber, 
			18 => _lastParent, 
			19 => _lastParentIsUserSpace, 
			20 => __legacyActiveUserRootOnly, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static Grabbable __New()
	{
		return new Grabbable();
	}
}
