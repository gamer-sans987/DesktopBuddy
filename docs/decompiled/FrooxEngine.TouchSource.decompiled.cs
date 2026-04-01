using System;
using Elements.Core;

namespace FrooxEngine;

public abstract class TouchSource : Component
{
	public bool SafeTouchSource;

	public readonly SyncRef<User> AutoUpdateUser;

	public readonly Sync<float> OutOfSightAngle;

	public readonly Sync<float> MaxTouchPenetrationDistance;

	private bool _touch;

	private ITouchable _forceTouchingTouchable;

	private ITouchable lastTouchable;

	private float3 lastTouchPoint;

	private bool isTouching;

	private TouchType lastTouchType;

	private float3 lastTouchDirection;

	private static Func<ICollider, bool> _raycastFilter = RaycastFilter;

	public abstract float3 TipPosition { get; }

	public abstract float3 TipDirection { get; }

	public ITouchable CurrentTouchable => lastTouchable;

	public float3 CurrentTouchPoint
	{
		get
		{
			if (CurrentTouchable != null)
			{
				return lastTouchPoint;
			}
			return float3.Zero;
		}
	}

	public bool IsTouchEventRunning { get; private set; }

	public bool LocalForceTouch
	{
		get
		{
			return _touch;
		}
		set
		{
			if (value && !_touch)
			{
				_forceTouchingTouchable = CurrentTouchable;
			}
			if (!value)
			{
				_forceTouchingTouchable = null;
			}
			_touch = value;
		}
	}

	public abstract TouchType TouchType { get; }

	protected Predicate<ITouchable> _touchableFilter { get; private set; }

	public bool IsForceTouching(ITouchable touchable)
	{
		if (touchable != null)
		{
			return touchable == _forceTouchingTouchable;
		}
		return false;
	}

	protected override void OnAwake()
	{
		MaxTouchPenetrationDistance.Value = 0.05f;
		OutOfSightAngle.Value = 70f;
		_touchableFilter = TouchableFilter;
	}

	protected override void OnCommonUpdate()
	{
		if (SafeTouchSource && base.Slot.ActiveUserRoot?.ActiveUser != base.LocalUser)
		{
			SafeTouchSource = false;
		}
		if (AutoUpdateUser.Target == base.World.LocalUser && base.World.Focus != FrooxEngine.World.WorldFocus.Background)
		{
			UpdateTouch();
		}
	}

	protected override void OnDestroy()
	{
		EndTouch();
	}

	protected override void OnDeactivated()
	{
		EndTouch();
	}

	protected override void OnDisabled()
	{
		EndTouch();
	}

	private static bool RaycastFilter(ICollider collider)
	{
		return collider.Slot.GetComponentInParentsUntilBlock<UserRoot>() == null;
	}

	private bool TouchableFilter(ITouchable touchable)
	{
		if (touchable.Enabled)
		{
			return touchable.CanTouchInteract(this);
		}
		return false;
	}

	protected abstract ITouchable GetTouchable(out float3 point, out float3 direction, out float3 directHitPoint, out bool touch);

	public void UpdateTouch()
	{
		float3 point;
		float3 direction;
		float3 directHitPoint;
		bool touch;
		ITouchable touchable = GetTouchable(out point, out direction, out directHitPoint, out touch);
		if (touchable != null)
		{
			UpdateCurrentTouchable(touchable, in point, in direction, in directHitPoint, touch);
		}
		else
		{
			EndTouch();
		}
		if (CurrentTouchable != _forceTouchingTouchable)
		{
			_forceTouchingTouchable = null;
		}
	}

	private void UpdateCurrentTouchable(ITouchable touchable, in float3 point, in float3 direction, in float3 directHitPoint, bool touch)
	{
		if (_touch && touchable.AcceptsExistingTouch && _forceTouchingTouchable != touchable)
		{
			_forceTouchingTouchable = touchable;
			isTouching = true;
		}
		if (touch && MathX.Angle(base.World.LocalUserViewRotation * float3.Forward, directHitPoint - base.World.LocalUserViewPosition) > (float)OutOfSightAngle && !touchable.CanTouchOutOfSight)
		{
			touch = false;
		}
		bool flag = isTouching;
		if (lastTouchable != touchable || lastTouchType != TouchType)
		{
			EndTouch();
		}
		if (touchable.AcceptsExistingTouch)
		{
			isTouching = flag;
		}
		SendTouchEvent(touchable, new TouchEventInfo((touchable != lastTouchable) ? EventState.Begin : EventState.Stay, (!touch) ? (isTouching ? EventState.End : EventState.None) : ((!isTouching) ? EventState.Begin : EventState.Stay), in point, TipPosition, in direction, TouchType, this));
		lastTouchable = touchable;
		lastTouchPoint = point;
		isTouching = touch;
		lastTouchType = TouchType;
		lastTouchDirection = direction;
	}

	private void SendTouchEvent(ITouchable touchable, in TouchEventInfo touchInfo)
	{
		try
		{
			IsTouchEventRunning = true;
			base.World.UpdateManager.NestCurrentlyUpdating(touchable);
			touchable.OnTouch(in touchInfo);
		}
		catch (Exception exception)
		{
			base.Debug.Error($"Exception in OnTouch() event on {touchable.ParentHierarchyToString()}\n\n{DebugManager.PreprocessException(exception)}");
		}
		finally
		{
			base.World.UpdateManager.PopCurrentlyUpdating(touchable);
			IsTouchEventRunning = false;
		}
	}

	public void EndTouch()
	{
		if (lastTouchable != null && !lastTouchable.IsDestroyed)
		{
			TouchEventInfo touchInfo = new TouchEventInfo(EventState.End, isTouching ? EventState.End : EventState.None, TipPosition, TipPosition, in lastTouchDirection, lastTouchType, this);
			SendTouchEvent(lastTouchable, in touchInfo);
		}
		lastTouchable = null;
		isTouching = false;
		_forceTouchingTouchable = null;
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		AutoUpdateUser = new SyncRef<User>();
		OutOfSightAngle = new Sync<float>();
		MaxTouchPenetrationDistance = new Sync<float>();
	}
}
