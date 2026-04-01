using System;
using Elements.Core;
using Renderite.Shared;

namespace FrooxEngine;

[DefaultUpdateOrder(-20000000)]
public class ScreenController : UserRootComponent, IInputUpdateReceiver, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable
{
	public readonly Sync<float> TransitionSpeed;

	public readonly SyncRef<PointerInteractionController> PointerController;

	public readonly SyncRef<IViewTargettingController> ActiveTargetting;

	public readonly SyncRef<HeadSimulator> Head;

	public readonly SyncRef<HandSimulator> LeftHand;

	public readonly SyncRef<HandSimulator> RightHand;

	protected readonly SyncRef<IViewTargettingController> _previousTargetting;

	protected readonly SyncRef<FirstPersonTargettingController> _firstPerson;

	protected readonly SyncRef<ThirdPersonTargettingController> _thirdPerson;

	protected readonly SyncRef<UI_TargettingController> _uiCamera;

	protected readonly SyncRef<FreeformTargettingController> _freeformCamera;

	private IViewTargettingController _freeformCameraPreviousController;

	private bool _freeformActivatedByFocus;

	private float3 _lastViewPosition;

	private floatQ _lastViewRotation;

	private float _viewTransitionLerp;

	private bool _externalPrimaryActivity;

	private bool _externalSecondaryActivity;

	private IViewTargettingController _activatedTargetting;

	private float _screenActivationLerp;

	private bool _lastScreenActivated;

	private ScreenInputs _input;

	public LocomotionReference LocomotionReference => _activatedTargetting?.LocomotionReference ?? LocomotionReference.View;

	public float3 ViewPosition
	{
		get
		{
			if (_viewTransitionLerp <= 0f)
			{
				return base.Slot.LocalPointToGlobal(in _lastViewPosition);
			}
			Slot slot = _activatedTargetting?.ViewSpace ?? base.Slot;
			if (slot.IsRemoved)
			{
				slot = base.Slot;
			}
			float3 to = slot.LocalPointToGlobal(_activatedTargetting?.ViewPosition ?? float3.Zero);
			if (_viewTransitionLerp >= 1f)
			{
				return to;
			}
			return MathX.Lerp(base.Slot.LocalPointToGlobal(in _lastViewPosition), in to, _viewTransitionLerp);
		}
	}

	public floatQ ViewRotation
	{
		get
		{
			if (_viewTransitionLerp <= 0f)
			{
				return base.Slot.LocalRotationToGlobal(in _lastViewRotation);
			}
			Slot slot = _activatedTargetting?.ViewSpace ?? base.Slot;
			if (slot.IsRemoved)
			{
				slot = base.Slot;
			}
			floatQ floatQ = slot.LocalRotationToGlobal(_activatedTargetting?.ViewRotation ?? floatQ.Identity);
			if (_viewTransitionLerp >= 1f)
			{
				return floatQ;
			}
			return MathX.Slerp(base.Slot.LocalRotationToGlobal(in _lastViewRotation), floatQ, _viewTransitionLerp);
		}
	}

	public Slot ViewSpace => _activatedTargetting?.ViewSpace ?? base.Slot;

	public float ScreenActivationLerp => _screenActivationLerp;

	public float3 ActualHeadPosition => MathX.Lerp(base.Slot.ActiveUserRoot?.LocalHeadPosition ?? Head.Target.HeadPosition, Head.Target.HeadPosition, _screenActivationLerp);

	public floatQ ActualHeadRotation => MathX.Slerp(base.Slot.ActiveUserRoot?.LocalHeadRotation ?? Head.Target.HeadRotation, Head.Target.HeadRotation, _screenActivationLerp);

	public floatQ ActualForwardRotation => MathX.Slerp(ActualHeadRotation, floatQ.LookRotation(_activatedTargetting?.ForwardDirection ?? float3.Forward), _screenActivationLerp);

	public float3 ActualNeckPosition => MathX.Lerp(base.Slot.ActiveUserRoot?.LocalNeckPosition ?? Head.Target.NeckPosition, Head.Target.NeckPosition, _screenActivationLerp);

	public float3 ActualHipsPosition => base.Slot.ActiveUserRoot?.LocalHipsPosition ?? (ActualNeckPosition * new float3(1f, 0.5f, 1f));

	public float3 ActualLeftHandPosition => MathX.Lerp(base.Slot.ActiveUserRoot?.LocalLeftHandPosition ?? LeftHand.Target.HandPosition, LeftHand.Target.HandPosition, _screenActivationLerp);

	public float3 ActualRightHandPosition => MathX.Lerp(base.Slot.ActiveUserRoot?.LocalRightHandPosition ?? RightHand.Target.HandPosition, RightHand.Target.HandPosition, _screenActivationLerp);

	public floatQ ActualLeftHandRotation => MathX.Slerp(base.Slot.ActiveUserRoot?.LocalLeftHandRotation ?? LeftHand.Target.HandRotation, LeftHand.Target.HandRotation, _screenActivationLerp);

	public floatQ ActualRightHandRotation => MathX.Slerp(base.Slot.ActiveUserRoot?.LocalRightHandRotation ?? RightHand.Target.HandRotation, RightHand.Target.HandRotation, _screenActivationLerp);

	public HandSimulator PrimaryHand
	{
		get
		{
			if (base.InputInterface.PrimaryHand != Chirality.Left)
			{
				return RightHand.Target;
			}
			return LeftHand.Target;
		}
	}

	public HandSimulator SecondaryHand
	{
		get
		{
			if (base.InputInterface.PrimaryHand != Chirality.Right)
			{
				return RightHand.Target;
			}
			return LeftHand.Target;
		}
	}

	public float LastPrimaryHandInteractionLerp => PrimaryHand?.InteractionLerp ?? 0f;

	public float LastSecondaryHandInteractionLerp => SecondaryHand?.InteractionLerp ?? 0f;

	public double LastPrimaryActivity { get; private set; }

	public double LastSecondaryActivity { get; private set; }

	public bool CanUseCurrentTargetting => CanUseViewTargetting(ActiveTargetting.Target);

	public float? GetPointViewDistance(in float3 point)
	{
		return _activatedTargetting?.ComputeWorldDistance(in point);
	}

	public float3? GetWorldPoint(in float3 origin, in float3 direction, float? distance)
	{
		return _activatedTargetting?.ComputeWorldPoint(in origin, in direction, distance);
	}

	public float3? GetSpawnPoint()
	{
		if (_activatedTargetting == null)
		{
			return null;
		}
		Slot slot = _activatedTargetting.ViewSpace ?? base.Slot;
		InteractionLaser interactionLaser = base.LocalUserRoot.GetRegisteredComponent((InteractionHandler c) => c.Side.Value == base.InputInterface.PrimaryHand)?.Laser;
		float3 origin;
		float3 direction;
		if (interactionLaser != null && base.InputInterface.IsWindowFocused)
		{
			origin = interactionLaser.LastOrigin;
			direction = interactionLaser.LastDirection;
		}
		else
		{
			origin = slot.LocalPointToGlobal(_activatedTargetting.ViewPosition);
			direction = slot.LocalDirectionToGlobal(_activatedTargetting.ViewRotation * float3.Forward);
		}
		return GetWorldPoint(in origin, in direction, null);
	}

	public bool AlignItem(Slot root, out floatQ orientation, out float3? position, out float3? scale)
	{
		if (_activatedTargetting == null)
		{
			orientation = default(floatQ);
			position = null;
			scale = null;
			return false;
		}
		return _activatedTargetting.AlignItem(root, out orientation, out position, out scale);
	}

	public double GetLastActivity(Chirality side)
	{
		if (side != base.InputInterface.PrimaryHand)
		{
			return LastSecondaryActivity;
		}
		return LastPrimaryActivity;
	}

	public void NotifyOfActivity(Chirality chirality)
	{
		if (chirality == base.InputInterface.PrimaryHand)
		{
			NotifyOfPrimaryActivity();
		}
		else
		{
			NotifyOfSecondaryActivity();
		}
	}

	public void NotifyOfPrimaryActivity()
	{
		_externalPrimaryActivity = true;
	}

	public void NotifyOfSecondaryActivity()
	{
		_externalPrimaryActivity = true;
	}

	protected override void OnAwake()
	{
		base.OnAwake();
		TransitionSpeed.Value = 4f;
	}

	protected override void OnAttach()
	{
		base.OnAttach();
		PointerController.Target = base.Slot.AttachComponent<PointerInteractionController>();
		Head.Target = base.Slot.AttachComponent<HeadSimulator>();
		LeftHand.Target = base.Slot.AttachComponent<HandSimulator>();
		RightHand.Target = base.Slot.AttachComponent<HandSimulator>();
		LeftHand.Target.Side.Value = Chirality.Left;
		RightHand.Target.Side.Value = Chirality.Right;
		if (base.World.IsUserspace())
		{
			ActiveTargetting.Target = base.Slot.AttachComponent<UserspaceTargettingController>();
			return;
		}
		_firstPerson.Target = base.Slot.AttachComponent<FirstPersonTargettingController>();
		_thirdPerson.Target = base.Slot.AttachComponent<ThirdPersonTargettingController>();
		_uiCamera.Target = base.Slot.AttachComponent<UI_TargettingController>();
		_freeformCamera.Target = base.Slot.AttachComponent<FreeformTargettingController>();
		ActiveTargetting.Target = _firstPerson.Target;
	}

	protected override void OnStart()
	{
		base.OnStart();
		if (base.IsUnderLocalUser)
		{
			_screenActivationLerp = (base.InputInterface.VR_Active ? 0f : 1f);
			_input = new ScreenInputs();
			base.Input.RegisterInputGroup(_input, this, OnInputUpdate);
		}
	}

	public void FocusUI(IUIInterface ui)
	{
		if (CanUseViewTargetting(_uiCamera.Target) && _uiCamera.Target.FocusInterface(ui) && ActiveTargetting.Target != _uiCamera.Target)
		{
			_previousTargetting.Target = ActiveTargetting.Target;
			ActiveTargetting.Target = _uiCamera.Target;
		}
	}

	public void FocusFreecam(Slot target, bool toggle)
	{
		if (!CanUseViewTargetting(_freeformCamera.Target))
		{
			return;
		}
		if (toggle && ActiveTargetting.Target == _freeformCamera.Target && _freeformCamera.Target.FocusTarget.Target == target)
		{
			ActiveTargetting.Target = _freeformCameraPreviousController;
			return;
		}
		if (ActiveTargetting.Target != _freeformCamera.Target)
		{
			_freeformCameraPreviousController = ActiveTargetting.Target;
			ActiveTargetting.Target = _freeformCamera.Target;
		}
		_freeformCamera.Target.Focus(target);
	}

	public void UnfocusUI()
	{
		if (ActiveTargetting.Target == _uiCamera.Target)
		{
			ActiveTargetting.Target = _previousTargetting.Target;
			_previousTargetting.Target = null;
		}
	}

	public void FilterDeviceNode(ref float3 position, ref floatQ rotation, ref bool tracking, ref bool isActive, BodyNode node, ITrackedDevice device)
	{
		if (base.InputInterface.VR_Active)
		{
			return;
		}
		switch (node)
		{
		case BodyNode.Head:
		{
			float3 to = Head.Target.HeadPosition;
			floatQ floatQ = Head.Target.HeadRotation;
			if (tracking && _screenActivationLerp < 1f)
			{
				float num = MathX.SmootherStep(_screenActivationLerp);
				to = MathX.Lerp(in position, in to, num);
				floatQ = MathX.Slerp(in rotation, floatQ, num);
			}
			position = to;
			rotation = floatQ;
			tracking = true;
			isActive = true;
			break;
		}
		case BodyNode.LeftController:
		case BodyNode.LeftHand:
			FilterHandNode(ref position, ref rotation, ref tracking, ref isActive, LeftHand, device);
			break;
		case BodyNode.RightController:
		case BodyNode.RightHand:
			FilterHandNode(ref position, ref rotation, ref tracking, ref isActive, RightHand, device);
			break;
		default:
			tracking = false;
			break;
		}
	}

	private void FilterHandNode(ref float3 position, ref floatQ rotation, ref bool tracking, ref bool isActive, HandSimulator simulator, ITrackedDevice device)
	{
		float3 to = simulator.HandPosition;
		floatQ floatQ = simulator.HandRotation;
		if (tracking)
		{
			float num = MathX.SmootherStep(_screenActivationLerp * simulator.InteractionLerp);
			to = MathX.Lerp(in position, in to, num);
			floatQ = MathX.Slerp(in rotation, floatQ, num);
		}
		position = to;
		rotation = floatQ;
		tracking = true;
		isActive = true;
	}

	private void ActivateFreeformCamera()
	{
		_freeformCameraPreviousController = ActiveTargetting.Target;
		ActiveTargetting.Target = _freeformCamera.Target;
	}

	private void OnInputUpdate()
	{
		if (base.InputInterface.ScreenActive)
		{
			Mouse mouse = base.InputInterface.Mouse;
			if (mouse != null && mouse.AnyButtonPressed && base.LocalUser.HasActiveFocus() && !base.InputInterface.IsBlocked((MouseBlock b) => true))
			{
				InteractionHandler interactionHandler = base.LocalUser.Root?.GetRegisteredComponent((InteractionHandler t) => t.Side.Value == Chirality.Left);
				InteractionHandler interactionHandler2 = base.LocalUser.Root?.GetRegisteredComponent((InteractionHandler t) => t.Side.Value == Chirality.Right);
				if ((interactionHandler == null || interactionHandler.Laser.CurrentTouchable == null) && (interactionHandler2 == null || interactionHandler2.Laser.CurrentTouchable == null))
				{
					base.LocalUser.ClearFocus();
				}
			}
		}
		if (!base.World.IsUserspace())
		{
			if (_input.ToggleFirstAndThirdPerson.Pressed)
			{
				if (ActiveTargetting.Target == _firstPerson.Target)
				{
					ActiveTargetting.Target = _thirdPerson.Target;
				}
				else
				{
					ActiveTargetting.Target = _firstPerson.Target;
				}
			}
			if (_input.ToggleFreeformCamera.Pressed)
			{
				if (ActiveTargetting.Target == _freeformCamera.Target)
				{
					ActiveTargetting.Target = _freeformCameraPreviousController;
				}
				else
				{
					ActivateFreeformCamera();
					_freeformActivatedByFocus = false;
				}
			}
			if (_input.Focus.Pressed)
			{
				if (ActiveTargetting.Target != _freeformCamera.Target)
				{
					ActivateFreeformCamera();
					_freeformActivatedByFocus = true;
				}
				_freeformCamera.Target?.Focus();
			}
			if (_input.Unfocus.Pressed)
			{
				_freeformCamera.Target?.Unfocus();
				if (_freeformActivatedByFocus)
				{
					ActiveTargetting.Target = _freeformCameraPreviousController;
				}
			}
		}
		ValidateViewTargetting();
	}

	public void BeforeInputUpdate()
	{
		if (!base.InputInterface.VR_Active)
		{
			if (!_lastScreenActivated)
			{
				Head.Target.ScreenActivated(this);
				LeftHand.Target.ScreenActivated(this);
				RightHand.Target.ScreenActivated(this);
				_lastScreenActivated = true;
			}
		}
		else if (_lastScreenActivated)
		{
			Head.Target.ScreenDeactivated(this);
			_lastScreenActivated = false;
		}
		_screenActivationLerp = MathX.Progress01(_screenActivationLerp, base.Time.Delta * (float)TransitionSpeed, !base.InputInterface.VR_Active);
		IViewTargettingController viewTargettingController = (base.InputInterface.VR_Active ? null : ActiveTargetting.Target);
		if (!_lastViewRotation.IsValid)
		{
			_lastViewRotation = floatQ.Identity;
		}
		if (viewTargettingController != _activatedTargetting)
		{
			if (_activatedTargetting != null)
			{
				Slot space = _activatedTargetting.ViewSpace ?? base.Slot;
				_lastViewPosition = base.Slot.SpacePointToLocal(_activatedTargetting.ViewPosition, space);
				_lastViewRotation = base.Slot.SpaceRotationToLocal(_activatedTargetting.ViewRotation, space);
				_lastViewRotation = _lastViewRotation.FastNormalized;
				if (!_lastViewRotation.IsValid)
				{
					_lastViewRotation = floatQ.Identity;
				}
				_viewTransitionLerp = 0f;
			}
			else
			{
				_lastViewPosition = float3.Zero;
				_lastViewRotation = floatQ.Identity;
				_viewTransitionLerp = 1f;
			}
			IViewTargettingController activatedTargetting = _activatedTargetting;
			_activatedTargetting?.DeactivateController();
			_activatedTargetting = viewTargettingController;
			_activatedTargetting?.ActivateController(this, activatedTargetting, in _lastViewPosition, in _lastViewRotation);
		}
		_viewTransitionLerp = MathX.Progress01(_viewTransitionLerp, base.Time.Delta * 8f);
		if (!base.InputInterface.VR_Active)
		{
			ActiveTargetting.Target?.OnBeforeHeadUpdate();
			Head.Target.Update(this);
			ActiveTargetting.Target?.OnAfterHeadUpdate();
		}
		LeftHand.Target.Update(this);
		RightHand.Target.Update(this);
		bool flag = ActiveTargetting.Target?.LastPrimaryActivity ?? false;
		bool flag2 = ActiveTargetting.Target?.LastSecondaryActivity ?? false;
		flag |= _externalPrimaryActivity;
		flag2 |= _externalSecondaryActivity;
		_externalPrimaryActivity = false;
		_externalSecondaryActivity = false;
		if (!flag)
		{
			flag = PointerController.Target?.PrimaryPointer.pointer != null;
		}
		if (!flag2)
		{
			flag2 = PointerController.Target?.SecondaryPointer.pointer != null;
		}
		if (flag)
		{
			LastPrimaryActivity = base.Time.WorldTime;
		}
		if (flag2)
		{
			LastSecondaryActivity = base.Time.WorldTime;
		}
		ValidateViewTargetting();
	}

	public void AfterInputUpdate()
	{
	}

	protected override void OnDispose()
	{
		base.OnDispose();
		_activatedTargetting = null;
		_freeformCameraPreviousController = null;
	}

	public bool CanUseViewTargetting(IViewTargettingController view)
	{
		User user = base.Slot.ActiveUser;
		return base.Permissions.Check(this, (ScreenViewPermissions p) => p.CanUseViewTargetting(view, user));
	}

	public void ValidateViewTargetting()
	{
		if (!CanUseCurrentTargetting)
		{
			if (CanUseViewTargetting(_firstPerson.Target))
			{
				ActiveTargetting.Target = _firstPerson.Target;
			}
			else if (CanUseViewTargetting(_thirdPerson.Target))
			{
				ActiveTargetting.Target = _thirdPerson.Target;
			}
			else
			{
				ActiveTargetting.Target = null;
			}
		}
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		TransitionSpeed = new Sync<float>();
		PointerController = new SyncRef<PointerInteractionController>();
		ActiveTargetting = new SyncRef<IViewTargettingController>();
		Head = new SyncRef<HeadSimulator>();
		LeftHand = new SyncRef<HandSimulator>();
		RightHand = new SyncRef<HandSimulator>();
		_previousTargetting = new SyncRef<IViewTargettingController>();
		_firstPerson = new SyncRef<FirstPersonTargettingController>();
		_thirdPerson = new SyncRef<ThirdPersonTargettingController>();
		_uiCamera = new SyncRef<UI_TargettingController>();
		_freeformCamera = new SyncRef<FreeformTargettingController>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => TransitionSpeed, 
			4 => PointerController, 
			5 => ActiveTargetting, 
			6 => Head, 
			7 => LeftHand, 
			8 => RightHand, 
			9 => _previousTargetting, 
			10 => _firstPerson, 
			11 => _thirdPerson, 
			12 => _uiCamera, 
			13 => _freeformCamera, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static ScreenController __New()
	{
		return new ScreenController();
	}
}
