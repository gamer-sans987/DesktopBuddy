using System;
using Elements.Core;

namespace FrooxEngine;

[Category(new string[] { "Transform/Interaction" })]
public class TouchEventRelay : Component, ITouchable, IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable
{
	public readonly SyncDelegate<TouchEvent> Touched;

	public readonly Sync<bool> AcceptOutOfSightTouch;

	public readonly SyncRefList<ITouchable> TouchableTargets;

	private bool _isRelaying;

	public bool AcceptsExistingTouch => false;

	public bool CanTouchOutOfSight => AcceptOutOfSightTouch;

	public event TouchEvent LocalTouched;

	public bool CanTouchInteract(TouchSource touchSource)
	{
		return true;
	}

	public void OnTouch(in TouchEventInfo touchInfo)
	{
		if (_isRelaying)
		{
			UniLog.Warning("Infinite TouchEventRelay Loop on " + this.ParentHierarchyToString(), stackTrace: true);
			return;
		}
		try
		{
			_isRelaying = true;
			this.LocalTouched?.Invoke(this, in touchInfo);
			Touched.Target?.Invoke(this, in touchInfo);
			foreach (ITouchable touchableTarget in TouchableTargets)
			{
				if (touchableTarget == null)
				{
					continue;
				}
				try
				{
					base.World.UpdateManager.NestCurrentlyUpdating(touchableTarget);
					touchableTarget.OnTouch(in touchInfo);
				}
				catch (Exception exception)
				{
					base.Debug.Error($"Exception in OnTouch() event on {touchableTarget.ParentHierarchyToString()}\n\n{DebugManager.PreprocessException(exception)}");
				}
				finally
				{
					base.World.UpdateManager.PopCurrentlyUpdating(touchableTarget);
				}
			}
		}
		finally
		{
			_isRelaying = false;
		}
	}

	void ITouchable.OnTouch(in TouchEventInfo eventInfo)
	{
		OnTouch(in eventInfo);
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		Touched = new SyncDelegate<TouchEvent>();
		AcceptOutOfSightTouch = new Sync<bool>();
		TouchableTargets = new SyncRefList<ITouchable>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => Touched, 
			4 => AcceptOutOfSightTouch, 
			5 => TouchableTargets, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static TouchEventRelay __New()
	{
		return new TouchEventRelay();
	}
}
