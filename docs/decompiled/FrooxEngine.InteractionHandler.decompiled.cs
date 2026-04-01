using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Elements.Core;
using Elements.Data;
using FrooxEngine.CommonAvatar;
using FrooxEngine.UIX;
using FrooxEngine.Undo;
using Renderite.Shared;

namespace FrooxEngine;

[DefaultUpdateOrder(-1000)]
[ExceptionHandling(ExceptionAction.DestroyUserRoot)]
[OldTypeName("FrooxEngine.CommonTool", null)]
public class InteractionHandler : UserRootComponent, IVibrationDeviceComponent, IVibrationDevice, IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable, ILocomotionReference, IInputUpdateReceiver, IHandTargetInfoSource
{
	public enum GrabType
	{
		None,
		Hand,
		Laser,
		Touch
	}

	public enum HandGrabType
	{
		Palm,
		Precision,
		Auto,
		Off
	}

	public enum LaserRotationType
	{
		AxisX,
		AxisY,
		AxisZ,
		Unconstrained
	}

	private enum MenuOptions
	{
		Default,
		Locomotion,
		Grabbing,
		LaserGrab,
		HandGrab
	}

	public static readonly float3 SHELF_DEFAULT_POSITION_LEFT = new float3(-0.02f, 0.01f, -0.14f);

	public static readonly float3 SHELF_DEFAULT_POSITION_RIGHT = new float3(0.02f, 0.01f, -0.14f);

	public static readonly floatQ SHELF_DEFAULT_ROTATION = floatQ.AxisAngle(float3.Right, -90f);

	public const float GRAB_ALIGN_TIME = 0.1f;

	public const float DEFAULT_SNAP_TURN_ANGLE = 45f;

	public const float DEFAULT_MOVEMENT_EXP = 2f;

	public const float DEFAULT_MOVEMENT_THRESHOLD = 0.15f;

	public const float GRAB_RADIUS = 0.07f;

	public const float DEFAULT_TELEPORT_ACTIVATE_INTERVAL = 0.25f;

	public const float MENU_RADIUS = 0.05f;

	public const float PANIC_HOLD_SECONDS = 2f;

	public const float EDIT_MODE_PRESS_INTERVAL = 0.25f;

	public const int EDIT_MODE_SWITCH_VIBRATIONS = 6;

	public const float USERSPACE_MENU_TOGGLE_DISTANCE = 0.25f;

	public readonly Sync<Chirality> Side;

	public readonly RelayRef<LocomotionController> LocomotionController;

	public readonly Sync<float> GrabSmoothing;

	protected readonly SyncRef<InteractionHandlerStreamDriver> _streamDriver;

	protected readonly SyncRef<ContextMenuItem> _undoItem;

	protected readonly SyncRef<ContextMenuItem> _redoItem;

	public readonly SyncRef<ContextMenu> ContextMenu;

	public readonly Sync<bool> EquippingEnabled;

	public readonly Sync<bool> MenuEnabled;

	public readonly Sync<bool> UserScalingEnabled;

	public readonly Sync<bool> VisualEnabled;

	public readonly Sync<bool> PointingGrab;

	public readonly Sync<bool> PointingTouch;

	internal Action<InteractionHandler> _userspaceToggle;

	protected readonly SyncRef<Slot> _toolRoot;

	protected readonly SyncRef<Slot> _laserSlot;

	protected readonly FieldDrive<float3> _laserPosition;

	protected readonly FieldDrive<floatQ> _laserRotation;

	protected readonly SyncRef<InteractionLaser> _interactionLaser;

	protected readonly Sync<bool> _laserEnabled;

	protected readonly Sync<HandGrabType> _handGrabType;

	protected readonly Sync<bool> _grabToggle;

	protected readonly FieldDrive<float3> _holderPos;

	protected readonly FieldDrive<floatQ> _holderRot;

	protected readonly Sync<LaserRotationType> _laserRotationType;

	protected readonly Sync<float> _holderAxisOffset;

	protected readonly Sync<floatQ> _holderRotationOffset;

	protected readonly Sync<floatQ?> _holderRotationReference;

	protected readonly Sync<float> _originalTwistOffset;

	protected readonly SyncRef<RingMesh> _userspaceToggleIndicator;

	private bool _setupForLocalUser;

	private InteractionHandlerInputs _inputs;

	private CommonActionsInputs _actionsInputs;

	private LaserHoldInputs _laserHoldInputs;

	private Digital _primary;

	private Digital _secondary;

	private Analog _strength;

	private Analog2D _axis;

	private float _userspaceOpenCharge;

	private bool _blockUserspaceOpen;

	private double _lastUserspacePress;

	private ItemShelf _laserItemShelf;

	private ITool _laserTool;

	private IInteractionOriginSource _interactionOriginSource;

	private InteractionOrigin? _interactionOrigin;

	private bool? _interactionOriginGrabbing;

	private Dictionary<Uri, ITool> _stashedTools;

	private Uri _currentToolUri;

	public readonly SyncRef<Slot> ToolHolder;

	public readonly Sync<bool> ShowInteractionHints;

	protected readonly FieldDrive<bool> _grabberSphereActive;

	protected readonly SyncRef<Slot> _grabIgnoreRoot;

	protected readonly SyncRef<Grabber> _grabber;

	protected readonly Sync<GrabType> _currentGrabType;

	private float? _laserGrabDistance;

	private float _grabLaserLerp;

	private float3 _lastLocalPoint;

	private float3? _spaceHolderPosition;

	private Slot _grabHitSlot;

	private IInteractionTarget _grabInteractionTarget;

	private bool _isScaling;

	private bool _lastGrabHeld;

	private bool _isToggleGrabHolding;

	protected readonly LinkTargetRef<ITool> ActiveToolLink;

	protected readonly SyncRef<GripPoseReference> _activeToolGripPoseReference;

	protected readonly Sync<bool> _toolLocked;

	protected readonly SyncRef<FresnelMaterial> _grabMaterial;

	protected readonly SyncRef<Slot> _itemShelfSlot;

	protected readonly SyncRef<ItemShelf> _itemShelf;

	private double lastEditModeTogglePress;

	private double lastMenuActionPress;

	private double? _lastToolGrabTime;

	private ITool _lastGrabbedTool;

	private double _lastLaserAlignPress;

	private bool _grabBlockLaser;

	private bool _grabBlockActions;

	private bool _undoInvalidated;

	private List<ITool> currentlyHeldTools = new List<ITool>();

	private float selfScaleReferenceDist;

	private float originalSelfScale;

	private bool isSelfScaling;

	private float panicCharge;

	private ITool _lastEquippedTool;

	private bool lastPrimary;

	private bool lastSecondary;

	private bool lastGrab;

	private bool lastMenu;

	private bool lastToolPress;

	private Action _beforeHandUpdate;

	private double lastSlideEquipPermissionMessage;

	public static float DoubleClickInterval { get; private set; }

	public static bool DoubleGripEquipEnabled { get; private set; }

	public Chirality OtherSide => Side.Value.GetOther();

	public IStandardController Controller
	{
		get
		{
			if (!base.IsUnderLocalUser)
			{
				return null;
			}
			return base.InputInterface.GetControllerNode(Side);
		}
	}

	public double ShortInterval => (Controller as IVibrationDevice)?.ShortInterval ?? 0.0;

	public double MediumInterval => (Controller as IVibrationDevice)?.MediumInterval ?? 0.0;

	public double LongInterval => (Controller as IVibrationDevice)?.LongInterval ?? 0.0;

	public bool IsWorldGrabberHolding => Userspace.GetControllerData(Side).worldHoldingThings;

	public bool SharesUserspaceToggleAndMenus => !_inputs.UserspaceToggle.IsBound;

	public bool UserspaceButtonHeld
	{
		get
		{
			if (base.World == Userspace.UserspaceWorld)
			{
				if (!_inputs.UserspaceToggle.Held)
				{
					return _userspaceOpenCharge >= 1f;
				}
				return true;
			}
			return false;
		}
	}

	public Slot PointReference => _laserSlot.Target;

	public Slot DirectionReference => _toolRoot.Target;

	public Slot GripReference => Grabber.Slot;

	public bool AnyLocomotionEnabled => LocomotionController.Target?.CanUseAnyLocomotion() ?? false;

	public Digital LocomotionGrip { get; private set; } = new Digital();

	Chirality ILocomotionReference.Side => Side.Value;

	public InteractionHandlerStreamDriver StreamDriver => _streamDriver.Target;

	public InteractionLaser Laser => _interactionLaser.Target;

	public bool LaserEnabled
	{
		get
		{
			if (!_laserEnabled.Value)
			{
				return ActiveTool?.UsesLaser ?? false;
			}
			return true;
		}
	}

	public LaserRotationType LaserRotation => _laserRotationType.Value;

	public float MaxLaserDistance
	{
		get
		{
			if (base.World == Userspace.UserspaceWorld && !base.LocalUser.RecordingVoiceMessage && Laser.CurrentHit?.GetComponent((IUserspaceLaserPriority p) => p.IsLaserPriority) == null)
			{
				return Userspace.GetControllerData(Side).distance;
			}
			return float.MaxValue;
		}
	}

	public bool WorldHasToolInUse => Userspace.GetControllerData(Side).toolInUse;

	public InteractionHandlerInputs Inputs => _inputs;

	public Slot GrabIgnore => _grabIgnoreRoot.Target;

	public bool IsNearHead
	{
		get
		{
			if (base.InputInterface.VR_Active)
			{
				return MathX.Distance(base.LocalUserRoot.Slot.GlobalPointToLocal(base.Slot.GlobalPosition), base.LocalUserRoot.LocalHeadPosition) < 0.25f;
			}
			return false;
		}
	}

	public bool LaserActive => Laser.LaserActive;

	public bool IsUserspaceLaserActive
	{
		get
		{
			if (Userspace.IsUserspaceLaserActive(Side))
			{
				return base.World != Userspace.UserspaceWorld;
			}
			return false;
		}
	}

	public bool HasUserspaceLaserHitTarget
	{
		get
		{
			if (Userspace.HasUserspaceLaserHitTarget(Side))
			{
				return base.World != Userspace.UserspaceWorld;
			}
			return false;
		}
	}

	public bool IsUserspaceHoldingObjects
	{
		get
		{
			if (Userspace.GetControllerData(Side).userspaceHoldingThings)
			{
				return base.World != Userspace.UserspaceWorld;
			}
			return false;
		}
	}

	public InteractionHandler OtherTool => UserRoot?.GetRegisteredComponent((InteractionHandler c) => c != this);

	public UserRoot UserRoot => base.Slot.ActiveUserRoot;

	public float TwistAngle
	{
		get
		{
			Slot slot = base.LocalUserRoot?.Slot;
			if (slot == null)
			{
				return 0f;
			}
			float3 from = slot.GlobalDirectionToLocal(CurrentTipForward);
			float3 v = slot.GlobalDirectionToLocal(CurrentTipUp);
			float3 to = from.x_z.Normalized;
			v = floatQ.FromToRotation(in from, in to) * v;
			return (0f - MathX.Atan2((floatQ.FromToRotation(in to, float3.Forward) * v).xy)) * 57.29578f;
		}
	}

	public float UserRootDistanceToOtherTool
	{
		get
		{
			UserRoot userRoot = UserRoot;
			InteractionHandler otherTool = OtherTool;
			if (userRoot == null || otherTool == null)
			{
				return 0f;
			}
			return MathX.Distance(userRoot.Slot.GlobalPointToLocal(base.Slot.GlobalPosition), userRoot.Slot.GlobalPointToLocal(otherTool.Slot.GlobalPosition));
		}
	}

	public bool IsOwnedByLocalUser => base.IsUnderLocalUser;

	public User Owner => base.Slot.ActiveUser;

	public HeadOutputDevice OwnerHeadDevice => Owner?.HeadDevice ?? HeadOutputDevice.UNKNOWN;

	public Grabber Grabber => _grabber.Target;

	public bool IsHoldingObjects => Grabber?.IsHoldingObjects ?? false;

	public bool IsScalingObjects => Grabber?.IsScaling ?? false;

	public bool IsHoldingObjectsWithLaser
	{
		get
		{
			if (IsHoldingObjects)
			{
				return (GrabType)_currentGrabType == GrabType.Laser;
			}
			return false;
		}
	}

	public bool BlockPrimary
	{
		get
		{
			return _streamDriver.Target.IsPrimaryBlocked;
		}
		private set
		{
			_streamDriver.Target.IsPrimaryBlocked = value;
		}
	}

	public bool BlockSecondary
	{
		get
		{
			return _streamDriver.Target.IsSecondaryBlocked;
		}
		private set
		{
			_streamDriver.Target.IsSecondaryBlocked = value;
		}
	}

	public bool PermissionAllowsScaling => base.Permissions.Check(this, (LocomotionPermissions p) => p.CanScale(Owner));

	public bool LocomotionAllowsScaling => LocomotionController.Target?.CanScale ?? false;

	public bool CanScale
	{
		get
		{
			if (PermissionAllowsScaling)
			{
				return LocomotionAllowsScaling;
			}
			return false;
		}
	}

	public bool ScalingEnabled => LocomotionController.Target?.ScalingEnabled.Value ?? true;

	private bool ShouldSelfScale
	{
		get
		{
			if (!UserScalingEnabled.Value)
			{
				return false;
			}
			InteractionHandler otherTool = OtherTool;
			if (otherTool == null)
			{
				return false;
			}
			if (!otherTool.Inputs.Grab.Held)
			{
				return false;
			}
			if (otherTool.isSelfScaling)
			{
				return false;
			}
			if (IsHoldingObjects || HasGripEquippedTool || otherTool.IsHoldingObjects || otherTool.HasGripEquippedTool)
			{
				return false;
			}
			Grabber grabber = Userspace.TryGetUserspaceGrabber(Grabber?.LinkingKey);
			Grabber grabber2 = Userspace.TryGetUserspaceGrabber(otherTool.Grabber?.LinkingKey);
			if ((grabber != null && grabber.IsHoldingObjects) || (grabber2 != null && grabber2.IsHoldingObjects))
			{
				return false;
			}
			if (!CanScale)
			{
				return false;
			}
			if (!ScalingEnabled)
			{
				return false;
			}
			return true;
		}
	}

	public bool UseRawTipReference
	{
		get
		{
			if (ActiveTool != null && ActiveTool.HasTipReference)
			{
				if (!ActiveTool.UsesLaser)
				{
					if (!base.InputInterface.VR_Active)
					{
						return IsContextMenuVisible;
					}
					return true;
				}
				return false;
			}
			return true;
		}
	}

	public float3 CurrentTip
	{
		get
		{
			if (UseRawTipReference)
			{
				return RawCurrentTip;
			}
			return ActiveTool.Tip;
		}
	}

	public float3 RawCurrentTip => _toolRoot.Target?.LocalPointToGlobal(float3.Forward * 0.075f) ?? base.Slot.GlobalPosition;

	public float3 CurrentTipForward
	{
		get
		{
			if (UseRawTipReference)
			{
				return RawCurrentTipForward;
			}
			return ActiveTool.TipForward;
		}
	}

	public float3 RawCurrentTipForward => _toolRoot.Target?.Forward ?? base.Slot.Forward;

	public float3 CurrentTipUp
	{
		get
		{
			if (UseRawTipReference)
			{
				return _toolRoot.Target.Up;
			}
			return ActiveTool.Slot.Up;
		}
	}

	public bool IsActiveToolPointing
	{
		get
		{
			if (!base.IsUnderLocalUser || !IsContextMenuOpen)
			{
				return ActiveTool?.UsesLaser ?? false;
			}
			return true;
		}
	}

	public bool IsContextMenuOpen
	{
		get
		{
			if (ContextMenu.Target?.CurrentSummoner == this)
			{
				return ContextMenu.Target.IsOpened;
			}
			return false;
		}
	}

	public bool IsContextMenuVisible
	{
		get
		{
			if (ContextMenu.Target?.CurrentSummoner == this)
			{
				return ContextMenu.Target.IsVisible;
			}
			return false;
		}
	}

	public ITool ActiveTool => ActiveToolLink.Target?.Component as ITool;

	public bool HasGripEquippedTool
	{
		get
		{
			if (ActiveTool != null)
			{
				return !_toolLocked.Value;
			}
			return false;
		}
	}

	public bool HasLockedInTool
	{
		get
		{
			if (ActiveTool != null)
			{
				return _toolLocked.Value;
			}
			return false;
		}
	}

	public bool TipUsesSecondary => ActiveTool?.UsesSecondary ?? false;

	Chirality IHandTargetInfoSource.HandSide => Side;

	bool IHandTargetInfoSource.IsHandReadyToInteract
	{
		get
		{
			if (!Laser.LaserVisible)
			{
				return ActiveTool != null;
			}
			return true;
		}
	}

	bool IHandTargetInfoSource.IsHandInteracting
	{
		get
		{
			if (ActiveTool == null && !Laser.ShowLaserToOthers && Laser.CurrentTouchable == null)
			{
				return Laser.CurrentInteractionTarget != null;
			}
			return true;
		}
	}

	bool IHandTargetInfoSource.EmptyInteraction
	{
		get
		{
			if (Laser.CurrentInteractionTarget == null && ActiveTool == null)
			{
				return _primary.Held;
			}
			return false;
		}
	}

	float3 IHandTargetInfoSource.InteractionTargetPoint => Laser.LastInteractionTargetPoint;

	Slot IHandTargetInfoSource.InteractionTargetHit => Laser.CurrentHit;

	float3 IHandTargetInfoSource.InteractionOriginPoint => Laser.Slot.GlobalPosition;

	float3 IHandTargetInfoSource.InteractionOriginDirection => Laser.Slot.Forward;

	float3? IHandTargetInfoSource.OverrideHandPosition
	{
		get
		{
			if (base.World == Userspace.UserspaceWorld)
			{
				return null;
			}
			if (ActiveTool == null || IsContextMenuOpen)
			{
				return null;
			}
			ScreenController registeredComponent = base.LocalUserRoot.GetRegisteredComponent<ScreenController>();
			if (registeredComponent == null)
			{
				return null;
			}
			float3 a = registeredComponent.ActualHeadPosition;
			floatQ q = registeredComponent.ActualHeadRotation;
			int num = ((Side.Value != Chirality.Left) ? 1 : (-1));
			return a + q * ILSpyHelper_AsRefReadOnly(new float3(0.25f * (float)num, -0.22f, 0.2f));
			static ref readonly T ILSpyHelper_AsRefReadOnly<T>(in T temp)
			{
				//ILSpy generated this function to help ensure overload resolution can pick the overload using 'in'
				return ref temp;
			}
		}
	}

	float IHandTargetInfoSource.InteractionLerp
	{
		get
		{
			if (Laser.ShowLaserToOthers || ActiveTool != null)
			{
				return 1f;
			}
			if (IsContextMenuVisible)
			{
				return 0.9f;
			}
			return 0.5f;
		}
	}

	float? IHandTargetInfoSource.InteractionTransitionSpeed
	{
		get
		{
			if (Laser.ShowLaserToOthers || IsContextMenuVisible || ActiveTool != null)
			{
				return 50f;
			}
			return 20f;
		}
	}

	private static colorX GetColor(HandGrabType grabType)
	{
		return grabType switch
		{
			HandGrabType.Palm => colorX.Orange, 
			HandGrabType.Precision => colorX.Yellow, 
			HandGrabType.Auto => colorX.Green, 
			HandGrabType.Off => colorX.Red, 
			_ => colorX.Clear, 
		};
	}

	private static Uri? GetIcon(HandGrabType grabType)
	{
		return grabType switch
		{
			HandGrabType.Palm => InteractionCursor.DEFAULT_ICON, 
			HandGrabType.Precision => OfficialAssets.Graphics.Icons.Laser.GrabCursor, 
			HandGrabType.Auto => OfficialAssets.Graphics.Icons.Laser.GrabHoverCursor, 
			HandGrabType.Off => OfficialAssets.Graphics.Icons.General.Cancel, 
			_ => null, 
		};
	}

	public void Vibrate(double time)
	{
		if (base.World.Focus != FrooxEngine.World.WorldFocus.Background)
		{
			(Controller as IVibrationDevice)?.Vibrate(time);
		}
	}

	protected override void OnAwake()
	{
		base.OnAwake();
		EquippingEnabled.Value = true;
		MenuEnabled.Value = true;
		UserScalingEnabled.Value = true;
		VisualEnabled.Value = true;
		GrabSmoothing.Value = 2.5f;
		_laserEnabled.Value = true;
		_laserRotationType.Value = LaserRotationType.AxisY;
		_holderRotationOffset.Value = floatQ.Identity;
	}

	private void OnUserInterfaceEditModeChanged(bool active)
	{
		RunSynchronously(delegate
		{
			if (base.IsUnderLocalUser && ActiveTool != null && _activeToolGripPoseReference.Target != null)
			{
				if (active)
				{
					GripPoseReferenceEditor.Setup(_activeToolGripPoseReference, ActiveTool.Slot);
				}
				else
				{
					ActiveTool.Slot.GetComponentInChildren<GripPoseReferenceEditor>()?.Destroy();
				}
			}
		});
	}

	protected override void OnStart()
	{
		base.OnStart();
		if (IsOwnedByLocalUser)
		{
			_handGrabType.Value = Settings.GetActiveSetting<GrabbingSettings>()?.DefaultHandGrabType.Value ?? HandGrabType.Palm;
			_stashedTools = new Dictionary<Uri, ITool>();
			_primary = new Digital();
			_secondary = new Digital();
			_strength = new Analog();
			_axis = new Analog2D();
			_setupForLocalUser = true;
			_inputs = new InteractionHandlerInputs(Side.Value);
			base.Input.RegisterInputGroup(_inputs, this, OnInputUpdate, OnInputEvaluate);
			_actionsInputs = new CommonActionsInputs(Side.Value);
			base.Input.RegisterInputGroup(_actionsInputs, this, OnActionInputUpdate);
			_laserHoldInputs = new LaserHoldInputs(Side.Value);
			base.Input.RegisterInputGroup(_laserHoldInputs, this, OnLaserHoldInputUpdate);
			base.World.GetUndoManager().LocalUndoChanged += UpdateUndoButtons;
			UpdateUndoButtons();
			if (base.World != Userspace.UserspaceWorld)
			{
				Userspace.UserInterfaceEditModeChanged += OnUserInterfaceEditModeChanged;
			}
			Laser.LocalCurrentHitChanged += OnCurrentHitChanged;
			_ = Side.Value;
			if (_itemShelf.Target != null)
			{
				bool flag = Side.Value == Chirality.Left;
				_itemShelf.Target.GrowDirection.Value = (flag ? ItemShelf.Direction.Right : ItemShelf.Direction.Left);
				_itemShelf.Target.Slot.LocalPosition = (flag ? SHELF_DEFAULT_POSITION_LEFT : SHELF_DEFAULT_POSITION_RIGHT);
			}
			switch (Side.Value)
			{
			case Chirality.Left:
				Grabber.CorrespondingBodyNode.Value = BodyNode.LeftHand;
				break;
			case Chirality.Right:
				Grabber.CorrespondingBodyNode.Value = BodyNode.RightHand;
				break;
			default:
				Grabber.CorrespondingBodyNode.Value = BodyNode.NONE;
				break;
			}
			LocomotionController.Target = base.Slot.GetComponentInParents<LocomotionController>();
			Grabber?.RegisterKey((Side.Value == Chirality.Left) ? FrooxEngine.Grabber.LEFT_HAND_KEY : FrooxEngine.Grabber.RIGHT_HAND_KEY);
			Grabber.BeforeUserspaceTransfer = delegate
			{
				RestoreGripEquippedTool();
			};
			Settings.RegisterValueChanges<GeneralControlsSettings>(OnControlSettingsChanged);
			Settings.RegisterValueChanges<LegacyFeatureSettings>(OnLegacySettingsChanged);
		}
	}

	private void OnControlSettingsChanged(GeneralControlsSettings settings)
	{
		DoubleClickInterval = settings.DoubleClickInterval;
	}

	private void OnLegacySettingsChanged(LegacyFeatureSettings settings)
	{
		DoubleGripEquipEnabled = settings.UseLegacyGripEquip;
	}

	public void BeforeInputUpdate()
	{
		_actionsInputs.Active = IsHoldingObjects || _grabBlockActions;
		_laserHoldInputs.Active = IsHoldingObjectsWithLaser || _grabBlockLaser;
		_laserHoldInputs.Align.Active = !IsContextMenuOpen;
		DigitalAction align = _laserHoldInputs.Align;
		ITool activeTool = ActiveTool;
		align.RegisterBlocks = (activeTool == null || !activeTool.UsesLaserGrip) && Laser.CurrentTouchable == null;
		_inputs.Secondary.RegisterBlocks = ActiveTool?.UsesSecondary ?? false;
		_inputs.Axis.RegisterBlocks = ActiveTool?.UsesSecondary ?? false;
	}

	public void AfterInputUpdate()
	{
	}

	private void UpdateInteractionOrigin()
	{
		_interactionOriginSource = null;
		_interactionOrigin = null;
		List<IInteractionOriginSource> list = Pool.BorrowList<IInteractionOriginSource>();
		base.LocalUserRoot?.GetRegisteredComponents(list);
		foreach (IInteractionOriginSource item in list)
		{
			InteractionOrigin? interactionOrigin = item.GetInteractionOrigin(Side);
			if (interactionOrigin.HasValue)
			{
				_interactionOriginSource = item;
				_interactionOrigin = interactionOrigin;
			}
		}
		Pool.Return(ref list);
	}

	private void UpdateInteractionAndLaser(bool targettingOnly)
	{
		UpdateInteractionOrigin();
		if (!targettingOnly)
		{
			UpdateUserspaceToolOffsets();
			if (base.World != Userspace.UserspaceWorld)
			{
				UpdateLaserRoot();
			}
		}
		Laser.UpdateLaser(IsContextMenuOpen ? ((float?)null) : _laserGrabDistance, (_currentGrabType.Value == GrabType.Laser && !IsContextMenuOpen && (Laser.CurrentTouchable == null || Laser.CurrentTouchable.Slot.IsChildOf(Grabber.HolderSlot))) ? new float?(GrabSmoothing.Value) : ((float?)null), _interactionOrigin, targettingOnly);
		StreamDriver.GrabDistance = ((IsContextMenuOpen && _currentGrabType.Value == GrabType.Laser) ? _laserGrabDistance.GetValueOrDefault() : 0f);
	}

	private void OnActionInputUpdate()
	{
		if (_actionsInputs.Duplicate.Pressed)
		{
			DuplicateGrabbed();
		}
		if (_actionsInputs.Destroy.Pressed)
		{
			DestroyGrabbed();
		}
		if (_actionsInputs.Save.Pressed)
		{
			SaveGrabbed();
		}
		if ((bool)_actionsInputs.Save || (bool)_actionsInputs.Duplicate || (bool)_actionsInputs.Destroy)
		{
			_actionsInputs.BlockInputs();
			_grabBlockActions = true;
		}
	}

	private void OnInputEvaluate()
	{
		UpdateInteractionAndLaser(targettingOnly: false);
		UpdateGrabberTransform();
		if (_interactionOrigin.HasValue)
		{
			if (_interactionOrigin.Value.primaryInteraction)
			{
				if (!_interactionOriginGrabbing.HasValue)
				{
					_interactionOriginGrabbing = Laser.CurrentTouchable == null;
				}
				if (_interactionOriginGrabbing.Value)
				{
					_inputs.Grab.ExternalInput = _interactionOrigin.Value.primaryInteraction;
				}
				else
				{
					_inputs.Interact.ExternalInput = _interactionOrigin.Value.primaryInteraction;
				}
			}
			else
			{
				_interactionOriginGrabbing = null;
			}
		}
		else
		{
			_interactionOriginGrabbing = null;
		}
	}

	private void OnInputUpdate()
	{
		if (ActiveTool != null && !CanKeepEquipped(ActiveTool))
		{
			Dequip(popOff: true);
		}
		if (_lastEquippedTool != null && (ActiveTool != _lastEquippedTool || ActiveTool.Slot.ActiveUserRoot != base.Slot.ActiveUserRoot))
		{
			ToolDequipped(_lastEquippedTool, popOff: false);
		}
		if (_userspaceToggle != null)
		{
			bool flag = OtherTool?.UserspaceButtonHeld ?? false;
			if (_inputs.UserspaceToggle.Pressed && flag)
			{
				UserspaceEditPressed();
			}
			if (_inputs.UserspaceToggle.Released)
			{
				if (!_blockUserspaceOpen && !flag)
				{
					_userspaceToggle(this);
				}
				_blockUserspaceOpen = false;
			}
		}
		if ((_grabHitSlot != null && _grabHitSlot.IsRemoved) || (_grabInteractionTarget != null && _grabInteractionTarget.IsRemoved))
		{
			EndGrab();
		}
		if (!IsContextMenuOpen && _grabHitSlot != null && Laser.CurrentHit != _grabHitSlot)
		{
			if (_grabInteractionTarget != null && _grabInteractionTarget.IsRemoved)
			{
				_grabInteractionTarget = null;
			}
			Laser.SetNewTarget(_grabHitSlot, _grabInteractionTarget);
			if (_spaceHolderPosition.HasValue)
			{
				Laser.LocalCurrentPoint = _spaceHolderPosition.Value;
			}
		}
		if (_laserGrabDistance.HasValue && Laser.StickPointSpace.Target != null && _isScaling && !Grabber.IsScaling)
		{
			EndGrab();
		}
		_grabber.Target?.Update();
		lastSecondary = false;
		lastPrimary = false;
		lastGrab = false;
		lastMenu = false;
		if (_inputs.FocusUI.Pressed)
		{
			IUIInterface iUIInterface = Laser.CurrentHit?.GetComponentInParents<IUIInterface>();
			if (iUIInterface != null)
			{
				iUIInterface.FocusUI();
			}
			else
			{
				base.World.UnfocusUI();
			}
		}
		if (_inputs.ToggleEditMode.Pressed && base.LocalUser.CanEnableEditMode())
		{
			base.LocalUser.EditMode = !base.LocalUser.EditMode;
		}
		if (_inputs.Interact.Pressed)
		{
			StartInteraction();
		}
		else if (_inputs.Interact.Held)
		{
			HoldInteraction();
		}
		else if (_inputs.Interact.Released)
		{
			EndInteraction();
		}
		if (_inputs.Secondary.Pressed)
		{
			StartSecondaryInteraction();
		}
		else if (_inputs.Secondary.Held)
		{
			HoldSecondaryInteraction();
		}
		else if (_inputs.Secondary.Released)
		{
			EndSecondaryInteraction();
		}
		bool lastGrabHeld = _lastGrabHeld;
		if (base.InputInterface.VR_Active)
		{
			_lastGrabHeld = false;
		}
		else if (!base.World.IsUserspace() && !base.InputInterface.AppDashOpened)
		{
			_lastGrabHeld = _inputs.Grab.Held;
		}
		if (_grabToggle.Value && base.InputInterface.VR_Active)
		{
			if (_inputs.Grab.Pressed)
			{
				if (!_isToggleGrabHolding)
				{
					_isToggleGrabHolding = true;
					StartGrab();
				}
				else
				{
					_isToggleGrabHolding = false;
					EndGrab();
					_grabBlockActions = false;
				}
			}
			else if (_isToggleGrabHolding)
			{
				if (IsHoldingObjects || HasGripEquippedTool || IsScalingObjects)
				{
					HoldGrab();
				}
				else
				{
					_isToggleGrabHolding = false;
					EndGrab();
					_grabBlockActions = false;
				}
			}
		}
		else if (_isToggleGrabHolding)
		{
			EndGrab();
			_grabBlockActions = false;
		}
		else if (_inputs.Grab.Pressed)
		{
			StartGrab();
		}
		else if (_inputs.Grab.Held || _lastGrabHeld)
		{
			HoldGrab();
		}
		else if (_inputs.Grab.Released || (lastGrabHeld && !_lastGrabHeld))
		{
			EndGrab();
			_grabBlockActions = false;
		}
		if (_inputs.Menu.Pressed)
		{
			StartMenu();
		}
		else if (_inputs.Menu.Held)
		{
			HoldMenu();
		}
		else if (_inputs.Menu.Released)
		{
			EndMenu();
		}
		if (base.InputInterface.ScreenActive && (bool)_inputs.Interact && LaserActive)
		{
			base.LocalUser.Root.GetRegisteredComponent<ScreenController>()?.NotifyOfActivity(Side);
		}
		if (base.World != Userspace.UserspaceWorld)
		{
			VirtualController virtualController = base.InputInterface.GetVirtualController(Side);
			virtualController.ActionPrimary.UpdateState(_inputs.Interact);
			virtualController.ActionSecondary.UpdateState(_inputs.Secondary);
			virtualController.ActionGrab.UpdateState(_inputs.Grab);
			virtualController.ActionMenu.UpdateState(_inputs.Menu);
			virtualController.Strength.UpdateValue(_inputs.Strength, base.Time.Delta);
			virtualController.Axis.UpdateValue(_inputs.Axis, base.Time.Delta);
		}
		if (isSelfScaling && !ShouldSelfScale)
		{
			isSelfScaling = false;
		}
		if (IsUserspaceLaserActive)
		{
			if (IsContextMenuOpen)
			{
				CloseContextMenu();
			}
			BlockPrimary = true;
		}
		float num = (BlockPrimary ? 0f : ((float)_inputs.Strength));
		float2 @float = (BlockSecondary ? float2.Zero : ((float2)_inputs.Axis));
		if (IsContextMenuOpen)
		{
			num = 0f;
		}
		if (Laser.CurrentInteractionTarget is ItemShelf)
		{
			num = 0f;
		}
		if (lastToolPress && BlockPrimary)
		{
			RunToolRelease();
		}
		_primary.UpdateState(lastPrimary && !BlockPrimary);
		_secondary.UpdateState(lastSecondary && !BlockSecondary);
		_strength.UpdateValue(num, base.Time.Delta);
		_axis.UpdateValue(@float, base.Time.Delta);
		if (ActiveTool != null)
		{
			try
			{
				base.World.UpdateManager.NestCurrentlyUpdating(ActiveTool);
				ActiveTool.Update(num, @float, _primary, _secondary);
			}
			catch (Exception exception)
			{
				UniLog.Error($"Exception Updating ActiveTool: {ActiveTool}.\n\n{DebugManager.PreprocessException(exception)}", stackTrace: false);
			}
			finally
			{
				base.World.UpdateManager.PopCurrentlyUpdating(ActiveTool);
			}
		}
		else if (!IsContextMenuOpen)
		{
			foreach (ITool currentlyHeldTool in currentlyHeldTools)
			{
				if (currentlyHeldTool.IsDestroyed)
				{
					continue;
				}
				try
				{
					base.World.UpdateManager.NestCurrentlyUpdating(ActiveTool);
					currentlyHeldTool.Update(num, @float, _primary, _secondary);
				}
				catch (Exception exception2)
				{
					UniLog.Error($"Exception Updating held tool: {currentlyHeldTool}.\n\n{DebugManager.PreprocessException(exception2)}", stackTrace: false);
				}
				finally
				{
					base.World.UpdateManager.PopCurrentlyUpdating(ActiveTool);
				}
			}
		}
		if (BlockPrimary && !lastPrimary && (float)_inputs.Strength <= 0.02f && !IsUserspaceLaserActive && !IsContextMenuOpen)
		{
			BlockPrimary = false;
		}
		if (BlockSecondary && !lastSecondary)
		{
			BlockSecondary = false;
		}
		if (LocomotionController.Target?.ActiveModule?.UsingGrip(Side) == true)
		{
			_lastToolGrabTime = null;
			_lastGrabbedTool = null;
		}
		LocomotionGrip.UpdateState(_inputs.Grab.Held && !IsHoldingObjects && !HasGripEquippedTool);
		if (Laser.CurrentTouchable is IAxisActionReceiver axisActionReceiver)
		{
			axisActionReceiver.ProcessAxis(Laser.TouchSource, _inputs.TouchAxis);
			if (base.InputInterface.ScreenActive && !MathX.Approximately(_inputs.TouchAxis.Value.Value.Magnitude, 0f))
			{
				base.LocalUser.Root.GetRegisteredComponent<ScreenController>()?.NotifyOfActivity(Side);
			}
		}
		if (base.World == Userspace.UserspaceWorld || !base.InputInterface.ScreenActive || Side.Value != base.InputInterface.PrimaryHand)
		{
			return;
		}
		for (int i = 0; i < 11; i++)
		{
			Key key;
			if (i < 10)
			{
				key = (Key)(48 + i);
			}
			else
			{
				if (i != 10)
				{
					throw new NotImplementedException($"Unsupported key index: {i}");
				}
				key = Key.Minus;
			}
			if (base.Input.BlockManager.IsBlocked((KeyboardBlock b) => b.IsBlocked(key)) || !base.InputInterface.GetKeyDown(key))
			{
				continue;
			}
			string name = i switch
			{
				2 => "Dev Tool", 
				3 => "ProtoFlux Tool", 
				4 => "Material Tool", 
				5 => "Shape Tool", 
				6 => "Light Tool", 
				7 => "Grabbable Setter Tool", 
				8 => "Character Collider Setter Tool", 
				9 => "Microphone Tool", 
				0 => "Glue Tool", 
				10 => "Component Clone Tool", 
				_ => null, 
			};
			if (name == null)
			{
				StashCurrentToolOrDequip();
				continue;
			}
			StartTask(async delegate
			{
				await SpawnAndEquip(base.Cloud.Platform.GetSpawnObjectUri("ShortcutTools/" + name));
			});
		}
	}

	private void OnLaserHoldInputUpdate()
	{
		SlideGrabbed(_laserHoldInputs.Slide, _laserHoldInputs.Rotate);
		if (_laserHoldInputs.Align.Pressed)
		{
			if (base.Time.WorldTime - _lastLaserAlignPress < 0.5)
			{
				if (_laserRotationType.Value == LaserRotationType.AxisY)
				{
					SwitchLaserRotation(LaserRotationType.Unconstrained);
				}
				else
				{
					SwitchLaserRotation(LaserRotationType.AxisY);
				}
				RunSynchronously(delegate
				{
					AlignHeldItem();
				});
				_lastLaserAlignPress = -1.0;
			}
			else
			{
				RunSynchronously(delegate
				{
					AlignHeldItem();
				});
				_lastLaserAlignPress = base.Time.WorldTime;
			}
		}
		if (_laserHoldInputs.FreezeCursor.Pressed)
		{
			base.Input.RegisterCursorLock(this, (int2)base.InputInterface.Mouse.WindowPosition.Value);
		}
		if (_laserHoldInputs.FreezeCursor.Released)
		{
			base.Input.UnregisterCursorLock(this);
		}
		float3 rotation = _laserHoldInputs.FreeformRotateDelta.Value.Value;
		if (!MathX.Approximately(rotation.Magnitude, 0f))
		{
			floatQ a = floatQ.Euler(in rotation);
			if (Grabber.HolderSlot.ChildrenCount > 0)
			{
				floatQ a2 = base.World.LocalUserViewRotation;
				floatQ a3 = a2.Inverted;
				foreach (Slot child in Grabber.HolderSlot.Children)
				{
					floatQ globalRotation = a2 * (a * (a3 * Grabber.HolderSlot.LocalRotationToGlobal(child.LocalRotation)));
					child.LocalRotation = Grabber.HolderSlot.GlobalRotationToLocal(in globalRotation);
				}
			}
			else
			{
				_holderRotationOffset.Value = a * _holderRotationOffset.Value;
			}
		}
		if (Grabber.CanScaleHeldObjects)
		{
			Analog value = _laserHoldInputs.ScaleDelta.Value;
			if (!MathX.Approximately(value, 0f))
			{
				Slot holderSlot = Grabber.HolderSlot;
				holderSlot.LocalScale *= 1f + (float)value;
			}
		}
		if (base.World == Userspace.UserspaceWorld)
		{
			_laserHoldInputs.SubmitBlocksToGlobal = true;
		}
		if (_grabBlockLaser && MathX.Approximately(_laserHoldInputs.Slide.Value.Value, 0f))
		{
			_grabBlockLaser = false;
		}
	}

	private void OnCurrentHitChanged(Slot hit)
	{
		if (IsOwnedByLocalUser)
		{
			_laserItemShelf = hit?.GetComponentInParents<ItemShelf>(ItemShelfFilter);
			_laserTool = hit?.GetComponentInParents((ITool t) => !t.IsEquipped);
		}
	}

	protected override void OnDispose()
	{
		_stashedTools?.Clear();
		if (_setupForLocalUser)
		{
			if (!base.World.IsDisposed)
			{
				base.World.GetUndoManager().LocalUndoChanged -= UpdateUndoButtons;
			}
			Userspace.UserInterfaceEditModeChanged -= OnUserInterfaceEditModeChanged;
			Settings.UnregisterValueChanges<GeneralControlsSettings>(OnControlSettingsChanged);
			Settings.UnregisterValueChanges<LegacyFeatureSettings>(OnLegacySettingsChanged);
		}
		base.OnDispose();
	}

	protected override void OnAttach()
	{
		InitializeTool(base.Slot.ActiveUserRoot.GetRegisteredComponent((InteractionHandlerStreamDriver d) => d.Side.Value == (Chirality)Side));
	}

	private bool ItemShelfFilter(ItemShelf shelf)
	{
		return shelf.Slot.ActiveUserRoot == base.Slot.ActiveUserRoot;
	}

	public void InitializeTool(InteractionHandlerStreamDriver streamDriver, bool itemShelf = true, bool grabSphere = true)
	{
		_streamDriver.Target = streamDriver;
		_grabIgnoreRoot.Target = base.Slot.AddSlot("Grab Ignore");
		_toolRoot.Target = base.Slot.AddSlot("Tooltip Root");
		_toolRoot.Target.AttachComponent<GrabBlock>();
		_toolRoot.Target.AttachComponent<InteractionHandlerRelay>().CommonTool.Target = this;
		_toolRoot.Target.AttachComponent<VibrationDeviceRelay>().DynamicLookupTarget.Target = base.Slot;
		Slot slot = base.Slot.AddSlot("Grabber");
		slot.AttachComponent<InteractionHandlerRelay>().CommonTool.Target = this;
		slot.AttachComponent<SearchBlock>();
		slot.AttachComponent<VibrationDeviceRelay>().DynamicLookupTarget.Target = base.Slot;
		_grabber.Target = slot.AttachComponent<Grabber>();
		Slot holderSlot = _grabber.Target.HolderSlot;
		_holderPos.Target = holderSlot.Position_Field;
		_holderRot.Target = holderSlot.Rotation_Field;
		_grabMaterial.Target = base.Slot.AttachComponent<FresnelMaterial>();
		_grabMaterial.Target.BlendMode.Value = BlendMode.Additive;
		_grabMaterial.Target.NearColor.Value = RadiantUI_Constants.Dark.YELLOW;
		_grabMaterial.Target.FarColor.Value = RadiantUI_Constants.Hero.YELLOW.SetValue(0.5f);
		if (grabSphere && base.World != Userspace.UserspaceWorld)
		{
			Slot slot2 = slot.AddSlot("Grab Sphere");
			slot2.AttachMesh<SphereMesh>(_grabMaterial.Target).Radius.Value = 0.07f;
			_grabberSphereActive.Target = slot2.ActiveSelf_Field;
		}
		_laserSlot.Target = _toolRoot.Target.AddSlot("Laser");
		_laserPosition.Target = _laserSlot.Target.Position_Field;
		_laserRotation.Target = _laserSlot.Target.Rotation_Field;
		_interactionLaser.Target = _laserSlot.Target.AttachComponent<InteractionLaser>();
		_interactionLaser.Target.Setup(this);
		if (base.World != Userspace.UserspaceWorld)
		{
			if (itemShelf)
			{
				_itemShelfSlot.Target = base.Slot.AddSlot("Shelf");
				_itemShelfSlot.Target.LocalRotation = SHELF_DEFAULT_ROTATION;
				_itemShelf.Target = _itemShelfSlot.Target.AttachComponent<ItemShelf>();
				_itemShelf.Target.IgnoreGrabber.Target = Grabber;
			}
		}
		else
		{
			Slot slot3 = _laserSlot.Target.AddSlot("DashOpenIndicator");
			slot3.LocalPosition = float3.Backward * 0.01f;
			AttachedModel<RingMesh, PBS_Metallic> attachedModel = slot3.AttachMesh<RingMesh, PBS_Metallic>();
			attachedModel.material.EmissiveColor.Value = new colorX(1.25f, 1.1f, 0.9f) * 2f;
			attachedModel.material.AlbedoColor.Value = new colorX(0f, 0.1f, 0.1f);
			attachedModel.material.Smoothness.Value = 0.9f;
			attachedModel.material.Metallic.Value = 0.2f;
			_userspaceToggleIndicator.Target = attachedModel.mesh;
			attachedModel.mesh.Segments.Value = 48;
			attachedModel.mesh.InnerRadius.Value = 0.05f;
			attachedModel.mesh.OuterRadius.Value = 0.055f;
			attachedModel.mesh.Arc.Value = 0f;
		}
		Slot slot4 = _toolRoot.Target.AddSlot("Tooltip Holder");
		slot4.AttachComponent<DestroyBlock>();
		ToolHolder.Target = slot4;
	}

	private void UpdateLaserRoot()
	{
		float3 currentTip = CurrentTip;
		float3 forward = CurrentTipForward;
		Slot target = _laserSlot.Target;
		target.GlobalPosition = currentTip;
		target.GlobalRotation = floatQ.LookRotation(in forward, float3.Up);
	}

	protected override void OnCommonUpdate()
	{
		bool num = base.World == Userspace.UserspaceWorld;
		bool isOwnedByLocalUser = IsOwnedByLocalUser;
		bool value = Owner?.EditMode ?? false;
		if (!isOwnedByLocalUser)
		{
			UpdateLaserRoot();
			UpdateGrabberTransform();
		}
		if (num)
		{
			_userspaceToggleIndicator.Target.Arc.Value = 360f * MathX.Clamp01(MathX.Lerp(-0.5f, 1f, _userspaceOpenCharge));
		}
		if (_grabberSphereActive.IsLinkValid)
		{
			_grabberSphereActive.Target.Value = value;
		}
	}

	private void UpdateUserspaceToolOffsets()
	{
		bool num = base.World == Userspace.UserspaceWorld;
		Userspace.ControllerData controllerData = Userspace.GetControllerData(Side);
		if (num)
		{
			controllerData.userspaceController = this;
			controllerData.userspaceHoldingThings = IsHoldingObjects;
			if (controllerData.worldHoldingThings)
			{
				EndGrab();
			}
		}
		else
		{
			controllerData.worldHoldingThings = IsHoldingObjects;
			if (controllerData.userspaceToggleHeld)
			{
				CloseContextMenu();
			}
		}
		float3 globalPoint = CurrentTip;
		float3 globalDirection = CurrentTipForward;
		if (!num)
		{
			float3 offset = base.Slot.GlobalPointToLocal(in globalPoint);
			float3 forward = base.Slot.GlobalDirectionToLocal(in globalDirection);
			Userspace.SetWorldControllerData(Side, ActiveTool?.IsInUse ?? false, offset, forward, Laser.Slot.LocalScaleToSpace(Laser.CurrentPointDistance, base.LocalUserRoot.Slot));
			return;
		}
		float3 b = base.Slot.GlobalPointToLocal(RawCurrentTip);
		float3 b2 = base.Slot.GlobalDirectionToLocal(RawCurrentTipForward);
		float3 a = controllerData.pointOffset;
		float3 a2 = controllerData.forward;
		float num2 = MathX.Distance(in a, in b);
		float num3 = MathX.Angle(in a2, in b2);
		if (num2 > 0.3f || num3 > 45f)
		{
			a = b;
			a2 = b2;
		}
		floatQ localRotation = floatQ.LookRotation(in a2, float3.Up);
		_laserSlot.Target.LocalPosition = base.Slot.LocalPointToSpace(in a, _laserSlot.Target.Parent);
		_laserSlot.Target.LocalRotation = base.Slot.LocalRotationToSpace(in localRotation, _laserSlot.Target.Parent);
		Userspace.SetUserspaceLaserActive(Side, Laser.LaserActive, Laser.CurrentHit != null);
	}

	void IHandTargetInfoSource.BeforeHandUpdate()
	{
		if (!IsContextMenuOpen && !IsHoldingObjectsWithLaser && base.World != Userspace.UserspaceWorld)
		{
			if (_beforeHandUpdate == null)
			{
				_beforeHandUpdate = RunBeforeHandUpdate;
			}
			RunInUpdateScope(_beforeHandUpdate);
		}
	}

	private void RunBeforeHandUpdate()
	{
		UpdateInteractionAndLaser(targettingOnly: true);
	}

	public bool IsMovingTarget(Slot slot)
	{
		if (ActiveTool != null && ActiveTool.IsMovingTarget(slot))
		{
			return true;
		}
		foreach (IGrabbable grabbedObject in Grabber.GrabbedObjects)
		{
			if (slot.IsChildOf(grabbedObject.Slot, includeSelf: true))
			{
				return true;
			}
		}
		return false;
	}

	private void StartInteraction()
	{
		lastPrimary = true;
		Canvas obj = Laser.CurrentTouchable as Canvas;
		ITriggerActionReceiver triggerActionReceiver = null;
		if (obj == null)
		{
			triggerActionReceiver = Grabber.HolderSlot.GetComponentInChildren<ITriggerActionReceiver>(null, includeLocal: false, excludeDisabled: true);
		}
		if (IsUserspaceLaserActive)
		{
			BlockPrimary = true;
		}
		else if (_laserItemShelf != null)
		{
			if (ActiveTool != null && _laserItemShelf.VerifyItemSize(ActiveTool.Slot, out var _))
			{
				ITool activeTool = ActiveTool;
				Dequip(popOff: false);
				if (_laserTool != null)
				{
					activeTool.Slot.SetParent(_laserTool.Slot.Parent, keepGlobalTransform: false);
					activeTool.Slot.CopyTransform(_laserTool.Slot);
				}
				else
				{
					activeTool.Slot.SetParent(_laserItemShelf.ContentSlot);
					activeTool.Slot.GlobalPosition = Laser.LastHitPoint;
					activeTool.Slot.GlobalRotation = floatQ.LookRotation(Laser.LastHitNormal);
				}
			}
			if (_laserTool != null)
			{
				Equip(_laserTool, lockEquip: true);
			}
			BlockPrimary = true;
		}
		else if (triggerActionReceiver != null)
		{
			triggerActionReceiver.Trigger();
			BlockPrimary = true;
		}
		else
		{
			bool touch = true;
			if (IsHoldingObjectsWithLaser && Laser.CurrentTouchable != null && Laser.CurrentTouchable.Slot.IsChildOf(Grabber.HolderSlot))
			{
				touch = false;
			}
			Laser.Touch = touch;
			if (Laser.IsTouching)
			{
				ITool activeTool2 = ActiveTool;
				if (activeTool2 == null || activeTool2.BlocksPrimaryWhenTouching || IsContextMenuOpen)
				{
					BlockPrimary = true;
					Laser.ResetLaserTimeout();
				}
			}
		}
		if (BlockPrimary)
		{
			return;
		}
		if (ActiveTool != null)
		{
			lastToolPress = true;
			try
			{
				ActiveTool.OnPrimaryPress();
				return;
			}
			catch (Exception exception)
			{
				UniLog.Error($"Exception Updating ActiveToolTip: {ActiveTool}.\n\n{DebugManager.PreprocessException(exception)}", stackTrace: false);
				return;
			}
		}
		if (currentlyHeldTools.Count > 0)
		{
			foreach (ITool currentlyHeldTool in currentlyHeldTools)
			{
				if (!currentlyHeldTool.IsDestroyed)
				{
					lastToolPress = true;
					try
					{
						currentlyHeldTool.OnPrimaryPress();
					}
					catch (Exception exception2)
					{
						UniLog.Error($"Exception Updating heldTooltip: {currentlyHeldTool}.\n\n{DebugManager.PreprocessException(exception2)}", stackTrace: false);
					}
				}
			}
			return;
		}
		if (base.World == Userspace.UserspaceWorld)
		{
			return;
		}
		bool flag = false;
		if (!LaserEnabled)
		{
			flag = true;
		}
		if (IsHoldingObjectsWithLaser)
		{
			flag = true;
		}
		if (!flag)
		{
			if (Laser.LaserActive && Laser.LocalLaserTimeout > 0f)
			{
				Laser.ClearLaserTimeout();
			}
			else
			{
				Laser.ResetLaserTimeout();
			}
		}
	}

	private void HoldInteraction()
	{
		lastPrimary = true;
		if (BlockPrimary)
		{
			return;
		}
		if (ActiveTool != null)
		{
			try
			{
				ActiveTool.OnPrimaryHold();
				return;
			}
			catch (Exception exception)
			{
				UniLog.Error($"Exception Updating ActiveToolTip: {ActiveTool}.\n\n{DebugManager.PreprocessException(exception)}", stackTrace: false);
				return;
			}
		}
		if (currentlyHeldTools.Count > 0)
		{
			{
				foreach (ITool currentlyHeldTool in currentlyHeldTools)
				{
					if (!currentlyHeldTool.IsDestroyed)
					{
						try
						{
							currentlyHeldTool.OnPrimaryHold();
						}
						catch (Exception exception2)
						{
							UniLog.Error($"Exception Updating heldTooltip: {currentlyHeldTool}.\n\n{DebugManager.PreprocessException(exception2)}", stackTrace: false);
						}
					}
				}
				return;
			}
		}
		if (Laser.IsTouching)
		{
			ITool activeTool = ActiveTool;
			if (activeTool == null || activeTool.BlocksPrimaryWhenTouching)
			{
				goto IL_013b;
			}
		}
		if (!IsContextMenuOpen)
		{
			return;
		}
		goto IL_013b;
		IL_013b:
		BlockPrimary = true;
	}

	private void EndInteraction()
	{
		Laser.Touch = false;
		if (!BlockPrimary)
		{
			RunToolRelease();
		}
	}

	private void StartSecondaryInteraction()
	{
		if (BlockSecondary)
		{
			return;
		}
		TryTriggerSecondaryAction();
		if (TipUsesSecondary)
		{
			try
			{
				ActiveTool.OnSecondaryPress();
			}
			catch (Exception exception)
			{
				UniLog.Error($"Exception Updating ActiveToolTip: {ActiveTool}.\n\n{DebugManager.PreprocessException(exception)}", stackTrace: false);
			}
		}
	}

	private void HoldSecondaryInteraction()
	{
		if (!BlockSecondary && TipUsesSecondary)
		{
			try
			{
				ActiveTool.OnSecondaryHold();
			}
			catch (Exception exception)
			{
				UniLog.Error($"Exception Updating ActiveToolTip: {ActiveTool}.\n\n{DebugManager.PreprocessException(exception)}", stackTrace: false);
			}
		}
		lastSecondary = true;
	}

	private void EndSecondaryInteraction()
	{
		if (!BlockSecondary && TipUsesSecondary)
		{
			try
			{
				ActiveTool.OnSecondaryRelease();
			}
			catch (Exception exception)
			{
				UniLog.Error($"Exception Updating ActiveToolTip: {ActiveTool}.\n\n{DebugManager.PreprocessException(exception)}", stackTrace: false);
			}
		}
	}

	private void StartMenu()
	{
		lastMenu = true;
		lastMenuActionPress = base.Time.WorldTime;
		if (IsNearHead)
		{
			bool flag = OtherTool?.IsNearHead ?? false;
			if (base.World != Userspace.UserspaceWorld && flag && OtherTool.Inputs.Menu.Held && base.LocalUser.CanEnableEditMode())
			{
				if (base.Time.WorldTime - lastEditModeTogglePress < 0.25)
				{
					base.LocalUser.EditMode = !base.LocalUser.EditMode;
					lastEditModeTogglePress = -1.0;
					for (int i = 0; i < 6; i++)
					{
						RunInSeconds((float)i * 0.1f, delegate
						{
							VibratePreset preset = (base.LocalUser.EditMode ? VibratePreset.Long : VibratePreset.Medium);
							base.Slot.TryVibrate(preset);
							OtherTool.Slot.TryVibrate(preset);
						});
					}
				}
				else
				{
					lastEditModeTogglePress = base.Time.WorldTime;
					base.Slot.TryVibrateMedium();
				}
			}
		}
		else if (!SharesUserspaceToggleAndMenus)
		{
			TryOpenContextMenu();
		}
		else if (OtherTool.UserspaceButtonHeld)
		{
			UserspaceEditPressed();
		}
		panicCharge = 0f;
	}

	private void HoldMenu()
	{
		lastMenu = true;
		if (base.World == Userspace.UserspaceWorld && IsNearHead && OtherTool != null && Side.Value == Chirality.Left && OtherTool.Inputs.Menu.Held && OtherTool.IsNearHead)
		{
			panicCharge += base.Time.Delta;
			if (panicCharge >= 2f)
			{
				panicCharge = float.MinValue;
				if (Inputs.Grab.Held || OtherTool.Inputs.Grab.Held)
				{
					World focusedWorld = base.World.Engine.WorldManager.FocusedWorld;
					focusedWorld?.RunSynchronously(delegate
					{
						focusedWorld.RootSlot.GetComponentsInChildren((UserRoot r) => r.ActiveUser == focusedWorld.LocalUser).ForEach(delegate(UserRoot r)
						{
							r.Slot.DestroyPreservingAssets();
						});
					});
				}
				else if (base.Engine.WorldManager.FocusedWorld == Userspace.LocalHome)
				{
					Userspace.ExitApp(saveHomes: false);
				}
				else
				{
					Userspace.ExitWorld(base.Engine.WorldManager.FocusedWorld);
				}
			}
			else if (panicCharge >= 1.6f)
			{
				base.Slot.TryVibrateLong();
				OtherTool.Slot.TryVibrateLong();
			}
			else if (panicCharge >= 1.2f)
			{
				base.Slot.TryVibrateMedium();
				OtherTool.Slot.TryVibrateMedium();
			}
			else if (panicCharge >= 0.8f)
			{
				base.Slot.TryVibrateShort();
				OtherTool.Slot.TryVibrateShort();
			}
		}
		if (IsNearHead || !SharesUserspaceToggleAndMenus)
		{
			return;
		}
		Userspace.ControllerData controllerData = Userspace.GetControllerData(Side);
		if (!(_userspaceOpenCharge <= 1f) || controllerData.userspaceToggleHeld)
		{
			return;
		}
		_userspaceOpenCharge += base.Time.Delta * 2f;
		if (base.World != Userspace.UserspaceWorld || !(_userspaceOpenCharge >= 1f))
		{
			return;
		}
		controllerData.userspaceToggleHeld = true;
		CloseContextMenu();
		base.Slot.TryVibrateMedium();
		InteractionHandler otherTool = OtherTool;
		if (otherTool != null && otherTool._userspaceOpenCharge <= 0f)
		{
			_userspaceToggle?.Invoke(this);
			_blockUserspaceOpen = true;
			_userspaceOpenCharge = 0f;
			return;
		}
		InteractionHandler otherTool2 = OtherTool;
		if (otherTool2 != null && otherTool2.UserspaceButtonHeld)
		{
			Userspace.UserInterfaceEditMode = !Userspace.UserInterfaceEditMode;
			OtherTool.Slot.TryVibrateMedium();
			base.Slot.TryVibrateMedium();
			_blockUserspaceOpen = true;
			OtherTool._blockUserspaceOpen = true;
			OtherTool._userspaceOpenCharge = 0f;
			_blockUserspaceOpen = true;
			_userspaceOpenCharge = 0f;
		}
	}

	private void EndMenu()
	{
		Userspace.ControllerData controllerData = Userspace.GetControllerData(Side);
		Userspace.ControllerData controllerData2 = Userspace.GetControllerData(OtherSide);
		if (SharesUserspaceToggleAndMenus && !controllerData2.userspaceToggleHeld && !_blockUserspaceOpen)
		{
			if (_userspaceOpenCharge <= 0.75f)
			{
				TryOpenContextMenu();
			}
			else if (base.World == Userspace.UserspaceWorld && _userspaceOpenCharge >= 1f)
			{
				CloseContextMenu();
				base.Slot.TryVibrateMedium();
			}
		}
		_userspaceOpenCharge = 0f;
		_blockUserspaceOpen = false;
		if (base.World == Userspace.UserspaceWorld && IsNearHead && _userspaceToggle != null)
		{
			InteractionHandler otherTool = OtherTool;
			if ((otherTool == null || !otherTool.Inputs.Menu.Pressed) && base.Time.WorldTime - lastMenuActionPress < 0.25)
			{
				_userspaceToggle(this);
				base.Slot.TryVibrateMedium();
			}
		}
		controllerData.userspaceToggleHeld = false;
	}

	private void StartGrab()
	{
		lastGrab = true;
		bool flag = !(_lastGrabbedTool?.IsRemoved ?? true);
		bool isDoubleGrip = DoubleGripEquipEnabled && base.Time.WorldTime - _lastToolGrabTime < (double)DoubleClickInterval;
		bool flag2 = ActiveTool != null && _toolLocked.Value;
		if (EquippingEnabled.Value && isDoubleGrip && (flag || flag2))
		{
			if (flag)
			{
				Equip(_lastGrabbedTool, lockEquip: true);
			}
			else
			{
				Dequip(popOff: true);
			}
			_lastToolGrabTime = null;
			_lastGrabbedTool = null;
			return;
		}
		if (base.World == Userspace.UserspaceWorld)
		{
			RunInUpdates(2, delegate
			{
				RunGrab(isDoubleGrip);
			});
		}
		else
		{
			RunGrab(isDoubleGrip);
		}
		if (ShouldSelfScale)
		{
			originalSelfScale = base.World.LocalUser.Root.GlobalScale;
			selfScaleReferenceDist = UserRootDistanceToOtherTool;
			isSelfScaling = true;
		}
	}

	private void HoldGrab()
	{
		lastGrab = true;
		if (ShouldSelfScale)
		{
			float num = selfScaleReferenceDist / UserRootDistanceToOtherTool;
			if (num > 0f)
			{
				float globalScale = originalSelfScale * num;
				LocomotionController.Target?.SetGlobalScale(globalScale);
			}
		}
	}

	private void UpdateGrabberTransform()
	{
		if (_currentGrabType.Value == GrabType.Laser)
		{
			Slot holderSlot = Grabber.HolderSlot;
			Slot slot = base.Slot.ActiveUserRoot.Slot;
			float grabDistance = StreamDriver.GrabDistance;
			float3 to;
			float t;
			if (grabDistance > 0f)
			{
				to = ((!base.InputInterface.VR_Active) ? slot.LocalPointToGlobal(in _lastLocalPoint) : Laser.Slot.LocalPointToGlobal(float3.Forward * grabDistance));
				t = base.Time.Delta * 6f;
				_grabLaserLerp = 0f;
			}
			else
			{
				_grabLaserLerp += base.Time.Delta * 16f;
				_grabLaserLerp = MathX.Min(_grabLaserLerp, 1f);
				to = Laser.CurrentActualPoint;
				t = _grabLaserLerp;
			}
			float3 globalPoint = MathX.Lerp((!_spaceHolderPosition.HasValue) ? to : slot.LocalPointToGlobal(_spaceHolderPosition.Value), in to, t);
			_spaceHolderPosition = slot.GlobalPointToLocal(in globalPoint);
			holderSlot.GlobalPosition = globalPoint;
			_lastLocalPoint = slot.GlobalPointToLocal(in globalPoint);
			UpdateHolderRotation();
		}
		else
		{
			_grabLaserLerp = 1f;
			_spaceHolderPosition = null;
			_holderPos.Target.Value = float3.Zero;
			_holderRot.Target.Value = floatQ.Identity;
		}
	}

	private void UpdateHolderRotation()
	{
		UserRoot activeUserRoot = base.Slot.ActiveUserRoot;
		if (activeUserRoot == null)
		{
			return;
		}
		float3 b = activeUserRoot.HeadPosition;
		floatQ floatQ = activeUserRoot.HeadRotation;
		if (base.IsUnderLocalUser)
		{
			ScreenController registeredComponent = activeUserRoot.GetRegisteredComponent<ScreenController>();
			if (registeredComponent != null)
			{
				b = activeUserRoot.Slot.LocalPointToGlobal(registeredComponent.ActualHeadPosition);
				floatQ = activeUserRoot.Slot.LocalRotationToGlobal(registeredComponent.ActualHeadRotation);
			}
		}
		floatQ globalRotation = floatQ;
		globalRotation = floatQ.LookRotation((activeUserRoot.Slot.GlobalRotationToLocal(in globalRotation) * float3.Forward).x_z.Normalized);
		globalRotation = activeUserRoot.Slot.LocalRotationToGlobal(in globalRotation);
		Slot holderSlot = Grabber.HolderSlot;
		Slot slot = activeUserRoot.Slot;
		if (_laserRotationType.Value == LaserRotationType.Unconstrained)
		{
			holderSlot.GlobalRotation = base.Slot.GlobalRotation;
			return;
		}
		float3 axis = slot.LocalDirectionToGlobal(float3.Up);
		float3 axis2 = (_holderRotationReference.Value ?? globalRotation) * float3.Right;
		float3 localDirection = base.Slot.GlobalDirectionToLocal(holderSlot.GlobalPosition - b);
		float3 v = ((!base.Slot.ActiveUser.VR_Active) ? ((_holderRotationReference.Value ?? globalRotation) * float3.Forward) : (slot.LocalDirectionToGlobal(base.Slot.LocalDirectionToSpace(in localDirection, slot).x_z.Normalized) + slot.LocalDirectionToGlobal(slot.GlobalDirectionToLocal(CurrentTipForward).x_z.Normalized))).Normalized;
		float num = 0f;
		if (base.InputInterface.VR_Active)
		{
			num = MathX.DeltaAngle(_originalTwistOffset.Value, TwistAngle) * 0.5f;
		}
		float angle = _holderAxisOffset.Value + num;
		switch (_laserRotationType.Value)
		{
		case LaserRotationType.AxisX:
			v = floatQ.AxisAngle(in axis2, angle) * v;
			break;
		case LaserRotationType.AxisY:
			v = floatQ.AxisAngle(in axis, angle) * v;
			break;
		case LaserRotationType.AxisZ:
			axis = floatQ.AxisAngle(in v, angle) * axis;
			break;
		}
		holderSlot.GlobalRotation = _holderRotationOffset.Value * floatQ.LookRotation(in v, in axis);
	}

	private void TryOpenContextMenu()
	{
		if (!MenuEnabled.Value)
		{
			return;
		}
		bool flag = base.World == Userspace.UserspaceWorld;
		if (!flag || (flag && IsHoldingObjects))
		{
			if (IsContextMenuOpen)
			{
				CloseContextMenu();
			}
			else
			{
				OpenContextMenu(MenuOptions.Default);
			}
		}
		else
		{
			TryTriggerContextMenuAction();
		}
	}

	private void UserspaceEditPressed()
	{
		if (base.Time.WorldTime - _lastUserspacePress <= (double)DoubleClickInterval)
		{
			Userspace.UserInterfaceEditMode = !Userspace.UserInterfaceEditMode;
			OtherTool.Slot.TryVibrateMedium();
			base.Slot.TryVibrateMedium();
			_blockUserspaceOpen = true;
			OtherTool._blockUserspaceOpen = true;
			OtherTool._userspaceOpenCharge = 0f;
			_lastUserspacePress = -1.0;
		}
		else
		{
			_lastUserspacePress = base.Time.WorldTime;
		}
	}

	private void RunGrab(bool isDoubleGrip)
	{
		if (base.World == Userspace.UserspaceWorld && Userspace.GetControllerData(Side).worldHoldingThings)
		{
			return;
		}
		bool flag = Grab();
		if ((HasLockedInTool && flag) || (_lastToolGrabTime.HasValue && !isDoubleGrip) || (!HasLockedInTool && !flag))
		{
			_lastToolGrabTime = null;
			_lastGrabbedTool = null;
			return;
		}
		_lastToolGrabTime = base.Time.WorldTime;
		if (ActiveTool != null && _toolLocked.Value)
		{
			_lastGrabbedTool = null;
		}
		else
		{
			_lastGrabbedTool = ActiveTool ?? TryGetGrabbedTool(out IGrabbable _);
		}
	}

	private void RunToolRelease()
	{
		lastToolPress = false;
		if (ActiveTool != null)
		{
			try
			{
				ActiveTool.OnPrimaryRelease();
				return;
			}
			catch (Exception exception)
			{
				UniLog.Error($"Exception Updating ActiveToolTip: {ActiveTool}.\n\n{DebugManager.PreprocessException(exception)}", stackTrace: false);
				return;
			}
		}
		foreach (ITool currentlyHeldTool in currentlyHeldTools)
		{
			if (!currentlyHeldTool.IsDestroyed)
			{
				try
				{
					currentlyHeldTool.OnPrimaryRelease();
				}
				catch (Exception exception2)
				{
					UniLog.Error($"Exception Updating heldTooltip: {currentlyHeldTool}.\n\n{DebugManager.PreprocessException(exception2)}", stackTrace: false);
				}
			}
		}
	}

	/// <summary>
	/// Checks if a tool can be equipped by this InteractionHandler.
	/// </summary>
	/// <param name="tool"></param>
	/// <remarks>See Remarks on <see cref="M:FrooxEngine.InteractionHandler.CanKeepEquipped(FrooxEngine.ITool)" /></remarks>
	/// <returns></returns>
	public bool CanEquip(ITool tool)
	{
		if (!tool.IsEquipped)
		{
			return CanKeepEquipped(tool);
		}
		return false;
	}

	/// <summary>
	/// Checks if an InteractionHandler can continue to have a tool equipped.
	/// </summary>
	/// <remarks>
	/// The difference from <see cref="M:FrooxEngine.InteractionHandler.CanEquip(FrooxEngine.ITool)">CanEquip</see> is that here we don't care about if the tool is already equipped.
	/// It is used in permission related systems which check if a user can continue having this tool equipped.
	/// A good example is in <see cref="M:FrooxEngine.InteractionHandler.OnInputUpdate" /> where we de-equip the tool if it cannot continue to be equipped.
	/// </remarks>
	/// <param name="tool"></param>
	/// <returns></returns>
	public bool CanKeepEquipped(ITool tool)
	{
		if (tool.Enabled)
		{
			return CheckPermission((InteractionHandlerPermissions p) => p.CanEquip(tool), base.Slot.ActiveUser ?? base.LocalUser);
		}
		return false;
	}

	public void StashCurrentToolOrDequip()
	{
		if (ActiveTool != null)
		{
			Uri currentToolUri = _currentToolUri;
			ITool activeTool = ActiveTool;
			Dequip(popOff: false);
			if (currentToolUri != null && (!_stashedTools.TryGetValue(currentToolUri, out ITool value) || value.IsRemoved))
			{
				activeTool.Slot.Parent = base.Slot;
				activeTool.Slot.ActiveSelf = false;
				_stashedTools[currentToolUri] = activeTool;
			}
		}
	}

	public async Task SpawnAndEquip(Uri uri)
	{
		if (_currentToolUri == uri)
		{
			return;
		}
		Slot toolSlot = null;
		bool cleanup = false;
		try
		{
			if (_stashedTools.TryGetValue(uri, out ITool value) && !value.IsRemoved)
			{
				value.Slot.ActiveSelf = true;
				toolSlot = value.Slot;
			}
			else
			{
				toolSlot = base.LocalUserRoot.Slot.AddSlot("Tooltip");
				await toolSlot.LoadObjectAsync(uri);
				toolSlot = toolSlot.UnpackInventoryItem();
				value = toolSlot.GetComponentInChildren<ITool>();
			}
			_stashedTools.Remove(uri);
			if (value == null)
			{
				cleanup = true;
			}
			else if (!CanEquip(value))
			{
				cleanup = true;
				NotificationMessage.SpawnTextMessage(value.Slot, "You don't have permission to equip this.", colorX.Red, 3f, 0.15f, 0.5f, 0.1f);
			}
			else
			{
				StashCurrentToolOrDequip();
				Equip(value, lockEquip: true);
				_currentToolUri = uri;
			}
		}
		catch (Exception ex)
		{
			UniLog.Error("Exception trying to spawn and equip tooltip:\n" + ex, stackTrace: false);
			cleanup = true;
		}
		if (cleanup)
		{
			toolSlot?.Destroy();
		}
	}

	public bool Equip(ITool tool, bool lockEquip)
	{
		if (!CanEquip(tool))
		{
			if (lockEquip)
			{
				NotificationMessage.SpawnTextMessage(tool.Slot, "You don't have permission to equip this.", colorX.Red, 3f, 0.15f, 0.5f, 0.1f);
			}
			return false;
		}
		if (ActiveTool != null)
		{
			Dequip(popOff: true);
		}
		_currentToolUri = null;
		if (lockEquip)
		{
			base.World.ClearUndoPoints((UpdateTransform u) => u.Target.Target == tool.Slot, allUsers: true);
		}
		base.Slot.TryVibrateMedium();
		ActiveToolLink.Target = tool.EquipLink;
		_toolLocked.Value = lockEquip;
		_lastEquippedTool = tool;
		ToolHolder.Target.SetIdentityTransform();
		tool.Slot.SetParent(ToolHolder.Target);
		try
		{
			tool.OnEquipped();
		}
		catch (Exception value)
		{
			UniLog.Error($"Exception running OnEquipped on a tooltip: {tool}\n{value}");
			Dequip(popOff: true);
			return false;
		}
		StartTask(async delegate
		{
			await AlignTool(tool);
		});
		return true;
	}

	[SyncMethod(typeof(Delegate), null)]
	private void Dequip(IButton button, ButtonEventData eventData)
	{
		BlockPrimary = true;
		_lastToolGrabTime = null;
		_lastGrabbedTool = null;
		Dequip(popOff: true);
		CloseContextMenu();
	}

	private ITool TryGetGrabbedTool(out IGrabbable grabbable)
	{
		if (!IsHoldingObjects)
		{
			grabbable = null;
			return null;
		}
		foreach (IGrabbable grabbedObject in Grabber.GrabbedObjects)
		{
			ITool component = grabbedObject.Slot.GetComponent<ITool>();
			if (component != null)
			{
				grabbable = grabbedObject;
				return component;
			}
		}
		grabbable = null;
		return null;
	}

	[SyncMethod(typeof(Delegate), null)]
	private void EquipGrabbed(IButton button, ButtonEventData eventData)
	{
		BlockPrimary = true;
		ResetGrab();
		_lastToolGrabTime = null;
		_lastGrabbedTool = null;
		if (ActiveTool != null)
		{
			_toolLocked.Value = true;
		}
		else
		{
			IGrabbable grabbable;
			ITool tool = TryGetGrabbedTool(out grabbable);
			if (tool != null)
			{
				Grabber.Release(grabbable, supressEvents: true);
				Equip(tool, lockEquip: true);
			}
		}
		CloseContextMenu();
	}

	public bool Dequip(bool popOff)
	{
		_currentToolUri = null;
		if (ActiveTool != null)
		{
			ToolDequipped(ActiveTool, popOff);
			return true;
		}
		return false;
	}

	private void ToolDequipped(ITool tooltip, bool popOff)
	{
		if (IsOwnedByLocalUser)
		{
			BlockPrimary = true;
			BlockSecondary = true;
		}
		ActiveToolLink.Target = null;
		_lastEquippedTool = null;
		_activeToolGripPoseReference.Target = null;
		if (tooltip != null && !tooltip.IsDestroyed)
		{
			float3 b = CurrentTipForward * base.Slot.LocalScaleToGlobal(0.05f);
			tooltip.Slot.Parent = UserRoot.Slot.Parent;
			if (popOff)
			{
				Slot slot = tooltip.Slot;
				slot.GlobalPosition += b;
				tooltip.Slot.Forward = MathX.Lerp(tooltip.Slot.Forward, (UserRoot.HeadPosition - tooltip.Slot.GlobalPosition).Normalized, 0.25f);
			}
			try
			{
				tooltip.OnDequipped();
			}
			catch (Exception value)
			{
				UniLog.Error($"Exception running OnDequipped on a tooltip: {tooltip}\n{value}");
			}
			Grabbable component = tooltip.Slot.GetComponent<Grabbable>();
			if (component != null)
			{
				component.Enabled = true;
			}
		}
		ToolHolder.Target.SetIdentityTransform();
	}

	private bool Grab()
	{
		bool flag = false;
		if ((bool)PointingGrab && LaserActive)
		{
			flag = Grab(laserGrab: true);
		}
		else
		{
			if (base.InputInterface.VR_Active)
			{
				flag = Grab(laserGrab: false);
			}
			if (!flag)
			{
				flag = TryPointGrab();
			}
		}
		if (ActiveTool == null)
		{
			foreach (IGrabbable grabbedObject in Grabber.GrabbedObjects)
			{
				ITool component = grabbedObject.Slot.GetComponent((ITool t) => !t.IsEquipped);
				if (component != null && CanEquip(component) && component.CanUseWhenHolding)
				{
					currentlyHeldTools.Add(component);
				}
			}
		}
		return flag;
	}

	private bool TryPointGrab()
	{
		if (!LaserActive)
		{
			return false;
		}
		if (IsNearHead)
		{
			if (MathX.Angle(CurrentTipForward, UserRoot.HeadSlot.Up) < 30f)
			{
				return false;
			}
			if (MathX.Angle(CurrentTipForward, UserRoot.HeadSlot.Backward) < 45f)
			{
				return false;
			}
		}
		return Grab(laserGrab: true);
	}

	private bool FilterPhysicalGrabbable(IGrabbable grabbable)
	{
		if (grabbable.AllowOnlyPhysicalGrab)
		{
			return false;
		}
		return FilterGrabbable(grabbable);
	}

	private bool FilterGrabbable(IGrabbable grabbable)
	{
		ITool component = grabbable.Slot.GetComponent<ITool>();
		if (component != null && component.IsEquipped && (!(grabbable is Slider) || grabbable.IsPersistent || !Userspace.UserInterfaceEditMode))
		{
			return false;
		}
		if (grabbable.Slot.IsChildOf(_toolRoot.Target) || (_itemShelfSlot.Target != null && grabbable.Slot.IsChildOf(_itemShelfSlot.Target)))
		{
			return false;
		}
		if (grabbable.Slot.IsChildOf(GrabIgnore))
		{
			return false;
		}
		return true;
	}

	private bool RunActiveTipOnGrabbing(bool laserGrab, List<ICollider> colliders)
	{
		try
		{
			return ActiveTool.OnGrabbing(laserGrab, colliders);
		}
		catch (Exception exception)
		{
			UniLog.Error($"Exception running OnGrabbing on ActiveToolTip: {ActiveTool}.\n\n{DebugManager.PreprocessException(exception)}", stackTrace: false);
			RunSynchronously(delegate
			{
				Dequip(popOff: true);
			});
			return false;
		}
	}

	private void PrecisionGrab(List<ICollider> colliders)
	{
		HandPoser registeredComponent = base.LocalUserRoot.GetRegisteredComponent((HandPoser p) => p.Side.Value == Side.Value);
		if (registeredComponent == null)
		{
			return;
		}
		Slot target = registeredComponent.Index.FarthestSegment.Root.Target;
		Slot target2 = registeredComponent.Thumb.FarthestSegment.Root.Target;
		if (target != null && target2 != null)
		{
			float num = base.LocalUserRoot?.GlobalScale ?? 1f;
			float3 origin = MathX.Lerp(target.GlobalPosition, target2.GlobalPosition, 0.5f);
			for (float num2 = 0.005f; num2 < 0.05f; num2 += 0.01f)
			{
				base.World.Physics.SphereOverlap(in origin, num * num2, colliders);
			}
		}
	}

	private bool Grab(bool laserGrab, ICollider overrideCollider = null)
	{
		if (!laserGrab && _handGrabType.Value == HandGrabType.Off)
		{
			return false;
		}
		List<ICollider> list = Pool.BorrowList<ICollider>();
		_grabMaterial.Target.NearColor.Value = RadiantUI_Constants.Dark.RED;
		_grabMaterial.Target.FarColor.Value = RadiantUI_Constants.Hero.RED.SetValue(0.5f);
		_ = float3.Zero;
		bool singleItem = false;
		if (overrideCollider != null)
		{
			list.Add(overrideCollider);
		}
		else if (ActiveTool == null || !HasLockedInTool || !RunActiveTipOnGrabbing(laserGrab, list))
		{
			if (laserGrab)
			{
				ICollider collider = Laser.CurrentHit?.GetComponent<ICollider>();
				if (collider != null)
				{
					list.Add(collider);
				}
			}
			else
			{
				bool flag = false;
				if (_handGrabType.Value == HandGrabType.Precision)
				{
					flag = true;
				}
				else if (_handGrabType.Value == HandGrabType.Auto)
				{
					flag = MathX.Abs(MathX.Dot(base.LocalUserRoot.Slot.GlobalDirectionToLocal(CurrentTipUp), float3.Up)) < 0.75f;
				}
				if (flag)
				{
					PrecisionGrab(list);
					singleItem = true;
				}
				else
				{
					base.World.Physics.SphereOverlap(Grabber.Slot.GlobalPosition, 0.07f * (base.LocalUserRoot?.GlobalScale ?? 1f), list);
				}
			}
		}
		ITouchGrabbable touchGrabbable = null;
		IGrabbable grabbable = null;
		if (laserGrab)
		{
			try
			{
				touchGrabbable = Laser.CurrentTouchable as ITouchGrabbable;
				grabbable = touchGrabbable?.TryGrab(Laser.TouchSource, Laser.CurrentActualPoint);
			}
			catch (Exception value)
			{
				UniLog.Error($"Exception running TryGrab on touchgrabbable: {touchGrabbable}\n{value}");
			}
		}
		Slot holderSlot = Grabber.HolderSlot;
		if (laserGrab && grabbable == null)
		{
			holderSlot.GlobalPosition = Laser.CurrentActualPoint;
			_holderRotationOffset.Value = floatQ.Identity;
			IViewTargettingController viewTargettingController = base.World.GetScreen()?.ActiveTargetting.Target;
			if (base.InputInterface.ScreenActive && (viewTargettingController is UI_TargettingController || viewTargettingController is FreeformTargettingController))
			{
				_holderRotationReference.Value = base.World.LocalUserViewRotation;
			}
			else
			{
				_holderRotationReference.Value = null;
			}
			_laserRotationType.Value = LaserRotationType.AxisY;
			_originalTwistOffset.Value = TwistAngle;
			UpdateHolderRotation();
		}
		else
		{
			holderSlot.LocalPosition = float3.Zero;
			holderSlot.LocalRotation = floatQ.Identity;
		}
		holderSlot.GlobalScale = float3.One;
		bool flag2 = ((grabbable == null) ? Grabber.Grab(list, laserGrab ? new Predicate<IGrabbable>(FilterPhysicalGrabbable) : new Predicate<IGrabbable>(FilterGrabbable), includeDynamic: true, singleItem) : Grabber.Grab(grabbable));
		Pool.Return(ref list);
		if (flag2)
		{
			if (grabbable != null)
			{
				_currentGrabType.Value = GrabType.Touch;
			}
			else
			{
				_currentGrabType.Value = ((!laserGrab) ? GrabType.Hand : GrabType.Laser);
			}
			if (_currentGrabType.Value == GrabType.Laser)
			{
				float? num = base.World.GetScreen()?.GetPointViewDistance(Laser.GlobalCurrentPoint);
				if (num.HasValue)
				{
					_laserGrabDistance = Laser.Slot.GlobalScaleToLocal(num.Value);
				}
				else
				{
					_laserGrabDistance = Laser.CurrentPointDistance;
				}
				_grabHitSlot = Laser.CurrentHit;
				_grabInteractionTarget = Laser.CurrentInteractionTarget;
				Slot slot;
				if (Grabber.IsScaling)
				{
					slot = Grabber.ScaleReference.HolderSlot;
					_isScaling = true;
				}
				else
				{
					slot = Grabber.GrabbedObjects?.FirstOrDefault()?.Slot;
					if (slot != null && slot.Parent == Grabber.HolderSlot)
					{
						slot = null;
					}
					_isScaling = false;
				}
				if (slot != null)
				{
					Laser.StickCurrentPoint(slot);
				}
			}
			if (!laserGrab && Grabber.GrabbedObjects.Count == 1 && ActiveTool == null)
			{
				ITool component = Grabber.GrabbedObjects[0].Slot.GetComponent((ITool t) => !t.IsEquipped);
				if (component != null)
				{
					GripEquip(component);
				}
			}
			if (_currentGrabType.Value != GrabType.Laser)
			{
				TryAlignGrabbed();
			}
		}
		return flag2;
	}

	private void TryAlignGrabbed()
	{
		if (Grabber.GrabbedObjects.Count == 1)
		{
			Slot slot = Grabber.GrabbedObjects[0].Slot;
			IGrabAlignable component = slot.GetComponent<IGrabAlignable>();
			if (component != null && component.GetGrabAlignmentPose(Grabber, out var alignedPosition, out var alignedRotation, out var alignedScale))
			{
				slot.Position_Field.TweenTo(alignedPosition, 0.1f, CurvePreset.Sine, null, null, slot.Parent);
				slot.Rotation_Field.TweenTo(alignedRotation, 0.1f, CurvePreset.Sine, null, null, slot.Parent);
				slot.Scale_Field.TweenTo(alignedScale, 0.1f, CurvePreset.Sine, null, null, slot.Parent);
			}
		}
	}

	private void GripEquip(ITool tooltip)
	{
		if (CanEquip(tooltip) && !tooltip.BlockGripEquip)
		{
			Grabber.Release(tooltip.Slot.GetComponent<IGrabbable>(), supressEvents: true);
			Equip(tooltip, lockEquip: false);
			Grabber.LocalExternallyHeldItem = tooltip.Slot;
		}
	}

	private ITool RestoreGripEquippedTool()
	{
		if (ActiveTool != null && !_toolLocked.Value)
		{
			ITool activeTool = ActiveTool;
			Dequip(popOff: false);
			Grabber.Grab(activeTool.Slot.GetComponent<IGrabbable>());
			Grabber.LocalExternallyHeldItem = null;
			return activeTool;
		}
		return null;
	}

	private void ResetGrab()
	{
		Grabber.LocalExternallyHeldItem = null;
		_currentGrabType.Value = GrabType.None;
		_laserGrabDistance = null;
		_grabHitSlot = null;
		_grabInteractionTarget = null;
		_isScaling = false;
		Laser.ClearStickPoint();
	}

	private void EndGrab(bool supressEvents = false)
	{
		RestoreGripEquippedTool();
		ResetGrab();
		(Laser.CurrentTouchable as ITouchGrabbable)?.Release(Grabber.GrabbedObjects, Laser.TouchSource, Laser.CurrentActualPoint);
		_grabber.Target.Release(supressEvents);
		_grabMaterial.Target.NearColor.Value = RadiantUI_Constants.Dark.YELLOW;
		_grabMaterial.Target.FarColor.Value = RadiantUI_Constants.Hero.YELLOW.SetValue(0.5f);
		if (_inputs.Interact.Held)
		{
			foreach (ITool currentlyHeldTool in currentlyHeldTools)
			{
				if (!currentlyHeldTool.IsDestroyed)
				{
					try
					{
						currentlyHeldTool.OnPrimaryRelease();
					}
					catch (Exception exception)
					{
						UniLog.Error($"Exception Updating Release: {currentlyHeldTool}.\n\n{DebugManager.PreprocessException(exception)}", stackTrace: false);
					}
				}
			}
		}
		currentlyHeldTools.Clear();
		CloseContextMenu();
	}

	public static void GetGripPoseReferences(ITool root, Slot current, List<GripPoseReference> references, Predicate<GripPoseReference> filter)
	{
		if (!current.IsActive)
		{
			return;
		}
		ITool component = current.GetComponent<ITool>();
		if ((component != null && component != root) || current.GetComponent<Canvas>() != null)
		{
			return;
		}
		current.GetComponents(references, filter);
		foreach (Slot child in current.Children)
		{
			GetGripPoseReferences(root, child, references, filter);
		}
	}

	private async Task AlignTool(ITool tool)
	{
		if (tool.Slot.Position_Field.IsDriven || tool.Slot.Rotation_Field.IsDriven || tool.Slot.Scale_Field.IsDriven)
		{
			await new Updates(2);
		}
		HandPoser registeredComponent = base.Slot.ActiveUserRoot.GetRegisteredComponent((HandPoser p) => p.Enabled && p.Slot.IsActive && p.Side.Value == (Chirality)Side && p.PoseSource.Target != null);
		Slot slot = ((registeredComponent == null) ? base.Slot.ActiveUserRoot.GetHandSlot(Side) : registeredComponent.CurrentHandRoot);
		Slot slot2 = tool.Slot;
		List<GripPoseReference> list = Pool.BorrowList<GripPoseReference>();
		GetGripPoseReferences(tool, slot2, list, (GripPoseReference g) => g.HandSide.Value == (Chirality)Side);
		if (list.Count == 0)
		{
			return;
		}
		Slot handSlot = base.Slot.ActiveUserRoot.GetHandSlot(Side);
		Slot holder = tool.Slot.Parent;
		Slot parent = holder.Parent;
		float num = float.MaxValue;
		GripPoseReference gripPoseReference = null;
		for (int num2 = 0; num2 < list.Count; num2++)
		{
			GripPoseReference gripPoseReference2 = list[num2];
			float num3 = MathX.Distance(gripPoseReference2.Slot.GlobalPosition, handSlot.GlobalPosition);
			if (num3 < num)
			{
				num = num3;
				gripPoseReference = gripPoseReference2;
			}
		}
		Pool.Return(ref list);
		GripPoseReference reference = gripPoseReference;
		float3 localPoint;
		floatQ localRotation;
		float num4;
		float num5;
		floatQ globalRotation;
		float3 globalPoint2;
		if (registeredComponent != null)
		{
			float3 a = reference.LocalForwardDirection;
			float3 up = MathX.Cross(in a, reference.LocalRightDirection);
			localPoint = slot2.SpacePointToLocal(reference.LocalFingerStartPosition, reference.Slot);
			localRotation = slot2.SpaceRotationToLocal(floatQ.LookRotation(in a, in up), reference.Slot);
			num4 = reference.Slot.LocalScaleToGlobal(reference.LocalScale);
			float3 globalPoint = GripPoseReference.ComputeFingerStart(registeredComponent);
			float3 @float = slot.GlobalPointToLocal(in globalPoint);
			float magnitude = @float.Magnitude;
			float3 a2 = @float.Normalized;
			float3 up2 = MathX.Cross(in a2, registeredComponent.HandRight.Value);
			globalPoint2 = globalPoint;
			globalRotation = slot.LocalRotationToGlobal(floatQ.LookRotation(in a2, in up2));
			num5 = slot.LocalScaleToGlobal(magnitude);
		}
		else
		{
			localPoint = slot2.SpacePointToLocal(reference.Slot.LocalPosition, reference.Slot.Parent);
			localRotation = slot2.SpaceRotationToLocal(reference.Slot.LocalRotation, reference.Slot.Parent);
			num4 = reference.Slot.LocalScaleToGlobal(1f);
			globalPoint2 = slot.GlobalPosition;
			globalRotation = slot.GlobalRotation;
			num5 = slot.LocalScaleToGlobal(1f);
		}
		Slot target = reference.TipReference.Target;
		Slot target2 = _toolRoot.Target;
		floatQ floatQ;
		if (target == null)
		{
			globalRotation = parent.GlobalRotationToLocal(in globalRotation);
			floatQ = parent.GlobalRotationToLocal(slot2.LocalRotationToGlobal(in localRotation));
		}
		else
		{
			globalRotation = parent.GlobalRotationToLocal(target2.GlobalRotation);
			floatQ = parent.GlobalRotationToLocal(target.GlobalRotation);
		}
		floatQ q = floatQ.FromToRotation(floatQ, globalRotation);
		float num6 = num4;
		float num7 = num5 / num6;
		float3 b;
		if (target == null)
		{
			globalPoint2 = parent.GlobalPointToLocal(in globalPoint2);
			b = q * ILSpyHelper_AsRefReadOnly(parent.GlobalPointToLocal(slot2.LocalPointToGlobal(in localPoint))) * num7;
		}
		else
		{
			globalPoint2 = parent.GlobalPointToLocal(target2.GlobalPosition);
			b = q * ILSpyHelper_AsRefReadOnly(parent.GlobalPointToLocal(target.GlobalPosition)) * num7;
		}
		float3 b2 = globalPoint2 - b;
		float3 fromPos = holder.LocalPosition;
		floatQ fromRot = holder.LocalRotation;
		float3 fromScale = holder.LocalScale;
		float3 toPos = fromPos + b2;
		floatQ toRot = fromRot * q;
		float3 toScale = fromScale * num7;
		float lerp = 0f;
		while (lerp < 1f && !tool.IsRemoved && tool.Slot.Parent == holder && !holder.IsRemoved)
		{
			lerp += base.Time.Delta * 8f;
			lerp = MathX.Min(lerp, 1f);
			float num8 = MathX.SmootherStep(lerp * 0.5f) * 2f;
			holder.LocalPosition = MathX.Lerp(in fromPos, in toPos, num8);
			holder.LocalRotation = MathX.Slerp(in fromRot, toRot, num8);
			holder.LocalScale = MathX.Lerp(in fromScale, in toScale, num8);
			await default(NextUpdate);
		}
		_activeToolGripPoseReference.Target = reference;
		if (Userspace.UserInterfaceEditMode)
		{
			GripPoseReferenceEditor.Setup(reference, tool.Slot);
		}
		static ref readonly T ILSpyHelper_AsRefReadOnly<T>(in T temp)
		{
			//ILSpy generated this function to help ensure overload resolution can pick the overload using 'in'
			return ref temp;
		}
	}

	private bool TouchFilter(ICollider collider)
	{
		if (collider.Slot.ActiveUserRoot == base.Slot.ActiveUserRoot)
		{
			if (collider.Slot.IsChildOf(base.Slot))
			{
				return false;
			}
			if (collider.Slot.IsChildOf(ToolHolder.Target))
			{
				return false;
			}
		}
		return true;
	}

	public bool AcceptLaserHit(ICollider collider)
	{
		if (IsContextMenuOpen)
		{
			return collider.Slot.Parent.GetComponent<ContextMenu>() != null;
		}
		return true;
	}

	public static void GetLaserRoots(IEnumerable<User> users, List<Slot> list)
	{
		foreach (User user in users)
		{
			GetLaserRoots(user, list);
		}
	}

	public static void GetLaserRoots(User user, List<Slot> list)
	{
		List<InteractionHandler> list2 = user?.Root?.GetRegisteredComponents<InteractionHandler>();
		if (list2 == null)
		{
			return;
		}
		foreach (InteractionHandler item in list2)
		{
			Slot slot = item.Laser?.Slot;
			if (slot != null)
			{
				list.Add(slot);
			}
		}
	}

	private bool TryTriggerContextMenuAction()
	{
		IContextMenuActionReceiver contextMenuActionReceiver = Laser.CurrentTouchable?.Slot.GetComponentInParents<IContextMenuActionReceiver>();
		if (contextMenuActionReceiver != null && contextMenuActionReceiver.TriggerContextMenu(Laser.TouchSource))
		{
			return true;
		}
		return false;
	}

	private bool TryTriggerSecondaryAction()
	{
		ISecondaryActionReceiver secondaryActionReceiver = Laser.CurrentTouchable?.Slot.GetComponentInParents<ISecondaryActionReceiver>();
		if (secondaryActionReceiver != null && secondaryActionReceiver.TriggerSecondary(Laser.TouchSource))
		{
			return true;
		}
		return false;
	}

	private void OpenContextMenu(MenuOptions options, float? speedOverride = null)
	{
		ContextMenu menu = ContextMenu.Target;
		if (menu == null)
		{
			return;
		}
		if (IsUserspaceLaserActive)
		{
			if (base.World == Userspace.UserspaceWorld)
			{
				TryTriggerContextMenuAction();
			}
			return;
		}
		StartTask(async delegate
		{
			if (!TryTriggerContextMenuAction() && await menu.OpenMenu(this, PointReference, new ContextMenuOptions
			{
				speedOverride = speedOverride
			}))
			{
				switch (options)
				{
				case MenuOptions.Default:
				{
					bool flag3 = ActiveTool != null;
					IGrabbable grabbable;
					ITool tool = TryGetGrabbedTool(out grabbable);
					Userspace.ControllerData controllerData = Userspace.GetControllerData(Side);
					if (IsHoldingObjects || controllerData.userspaceHoldingThings || (ActiveTool != null && !_toolLocked.Value))
					{
						menu.AddItem(Elements.Core.LocaleHelper.AsLocaleKey("Interaction.Destroy", continuous: false), OfficialAssets.Graphics.Icons.General.Cancel, new colorX?(new colorX(1f, 0.3f, 0.3f)), DestroyGrabbed);
						menu.AddItem(Elements.Core.LocaleHelper.AsLocaleKey("Interaction.Duplicate", continuous: false), OfficialAssets.Graphics.Icons.Item.Duplicate, new colorX?(new colorX(0.3f, 1f, 0.4f)), DuplicateGrabbed);
						if (CanSaveItem(Grabber))
						{
							string str = "Interaction.SaveToInventory";
							if (base.Cloud.Session.CurrentUserID == null)
							{
								str = "Interaction.SaveToInventory.NotLoggedIn";
							}
							else if (!(InventoryBrowser.CurrentUserspaceInventory?.CanWriteToCurrentDirectory ?? false))
							{
								str = "Interaction.SaveToInventory.NoWritePermission";
							}
							menu.AddItem(Elements.Core.LocaleHelper.AsLocaleKey(str, continuous: false), OfficialAssets.Graphics.Icons.General.Save, new colorX?(new colorX(0.25f, 0.5f, 1f)), SaveGrabbed).Button.Enabled = InventoryBrowser.CurrentUserspaceInventory?.CanWriteToCurrentDirectory ?? false;
						}
						List<IContextMenuItemSource> list = Pool.BorrowList<IContextMenuItemSource>();
						foreach (IGrabbable grabbedObject in Grabber.GrabbedObjects)
						{
							grabbedObject.Slot.GetComponentsInChildren(list);
						}
						KeyCounter<string> keyCounter = new KeyCounter<string>();
						foreach (IContextMenuItemSource item in list)
						{
							if (item.SingleItemKey != null)
							{
								keyCounter.Increment(item.SingleItemKey);
							}
						}
						foreach (KeyValuePair<string, int> key in keyCounter)
						{
							if (key.Value > 1)
							{
								bool firstItemKept = false;
								list.RemoveAll(delegate(IContextMenuItemSource s)
								{
									if (s.SingleItemKey != key.Key)
									{
										return false;
									}
									if (s.KeepFirstSingleItem && !firstItemKept)
									{
										firstItemKept = true;
										return false;
									}
									return true;
								});
							}
						}
						foreach (IContextMenuItemSource item2 in list)
						{
							item2.GenerateMenuItems(ContextMenu, list);
						}
						Pool.Return(ref list);
					}
					if ((bool)EquippingEnabled && (flag3 || tool != null))
					{
						if (_toolLocked.Value && flag3)
						{
							menu.AddItem(Elements.Core.LocaleHelper.AsLocaleKey("Interaction.DequipTool", continuous: false), OfficialAssets.Graphics.Icons.General.HandDropping, new colorX?(new colorX(0.8f, 0.8f, 0.8f)), Dequip);
						}
						else
						{
							menu.AddItem(Elements.Core.LocaleHelper.AsLocaleKey("Interaction.EquipTool", continuous: false), OfficialAssets.Graphics.Icons.General.Fist, new colorX?(new colorX(0.8f, 0.8f, 0.8f)), EquipGrabbed);
						}
					}
					if (!IsHoldingObjects || HasGripEquippedTool)
					{
						_undoItem.Target = menu.AddItem(Elements.Core.LocaleHelper.AsLocaleKey("Interaction.Undo", continuous: false), OfficialAssets.Graphics.Icons.General.Undo, new colorX?(new colorX(1f, 0.1f, 0.1f)), Undo);
						_redoItem.Target = menu.AddItem(Elements.Core.LocaleHelper.AsLocaleKey("Interaction.Redo", continuous: false), OfficialAssets.Graphics.Icons.General.Redo, new colorX?(new colorX(0.2f, 0.4f, 1f)), Redo);
						UpdateUndoRedoItems();
					}
					if (!flag3 || base.InputInterface.ScreenActive)
					{
						LocomotionController target = LocomotionController.Target;
						ILocomotionModule locomotionModule = target?.ActiveModule;
						if (target != null && !target.IsSupressed && target.CanUseAnyLocomotion())
						{
							string text2 = this.GetLocalized(locomotionModule?.LocomotionName) ?? this.GetLocalized("Interaction.Locomotion.None");
							string text3 = this.GetLocalized("Interaction.Locomotion") + "\n<size=75%>" + text2 + "</size>";
							menu.AddItem((LocaleString)text3, locomotionModule?.LocomotionIcon, new colorX?(locomotionModule?.LocomotionColor ?? colorX.Black), OpenLocomotionMenu);
						}
						if (CanScale)
						{
							if (base.Slot.ActiveUserRoot.IsAtScale(base.Slot.ActiveUserRoot.GetDefaultScale()))
							{
								menu.AddToggleItem(target.ScalingEnabled, Elements.Core.LocaleHelper.AsLocaleKey("Interaction.ScalingEnabled", continuous: false), Elements.Core.LocaleHelper.AsLocaleKey("Interaction.ScalingDisabled", continuous: false), colorX.Green, colorX.Red, OfficialAssets.Graphics.Icons.Tool.SetScalable, OfficialAssets.Graphics.Icons.Tool.LockScale);
							}
							else
							{
								menu.AddItem(Elements.Core.LocaleHelper.AsLocaleKey("Interaction.ResetScale", continuous: false), OfficialAssets.Graphics.Icons.General.ResetScale, new colorX?(new colorX(1f, 0.4f, 0.2f)), ResetUserScale);
							}
						}
						if (!flag3)
						{
							menu.AddToggleItem(_laserEnabled, Elements.Core.LocaleHelper.AsLocaleKey("Interaction.LaserEnabled", continuous: false), Elements.Core.LocaleHelper.AsLocaleKey("Interaction.LaserDisabled", continuous: false), new colorX(0.3f, 0.5f, 1f), colorX.Gray, OfficialAssets.Graphics.Icons.Tool.EnableLasers, OfficialAssets.Graphics.Icons.Tool.DisableLasers);
							if (base.InputInterface.VR_Active)
							{
								menu.AddItem("Interaction.Grabbing".AsLocaleKey(), (Uri?)null, new colorX?(RadiantUI_Constants.Hero.ORANGE), (ButtonEventHandler)OpenGrabbingMenu);
							}
						}
					}
					ActiveTool?.GenerateMenuItems(this, menu);
					List<RootContextMenuItem> list2 = Pool.BorrowList<RootContextMenuItem>();
					base.LocalUserRoot.GetRegisteredComponents(list2);
					foreach (RootContextMenuItem item3 in list2)
					{
						ContextMenuItemSource target2 = item3.Item.Target;
						if (target2 != null && target2.Enabled && (!item3.OnlyForSide.Value.HasValue || item3.OnlyForSide.Value == Side.Value) && (!item3.ExcludeOnTools.Value || ActiveTool == null) && (!item3.ExcludePrimaryHand.Value || Side.Value != base.InputInterface.PrimaryHand) && (!item3.ExcludeSecondaryHand.Value || Side.Value == base.InputInterface.PrimaryHand))
						{
							target2.SetupItem(menu);
						}
					}
					Pool.Return(ref list2);
					break;
				}
				case MenuOptions.Locomotion:
					if (LocomotionController.Target == null)
					{
						CloseContextMenu();
						return;
					}
					foreach (ILocomotionModule locomotionModule2 in LocomotionController.Target.LocomotionModules)
					{
						if (locomotionModule2 != null)
						{
							bool flag2 = locomotionModule2 == LocomotionController.Target.ActiveModule;
							LocaleString locomotionName = locomotionModule2.LocomotionName;
							string text = this.GetLocalized(locomotionName);
							colorX a = locomotionModule2.LocomotionColor;
							if (flag2)
							{
								text = "<b>" + text + "</b>";
							}
							else
							{
								a = MathX.Lerp(in a, colorX.White, 0.3f);
							}
							ContextMenuItem contextMenuItem = menu.AddItem((LocaleString)text, locomotionModule2.LocomotionIcon, new colorX?(a));
							contextMenuItem.Highlight.Value = flag2;
							contextMenuItem.Button.SetupRefAction(SetLocomotion, locomotionModule2);
							UniLog.Log($"Module: {locomotionModule2.LocomotionName}, CanUse: {LocomotionController.Target.CanUseModule(locomotionModule2)}, World: {base.World.Name}");
							if (!LocomotionController.Target.CanUseModule(locomotionModule2))
							{
								contextMenuItem.Button.Enabled = false;
							}
						}
					}
					break;
				case MenuOptions.Grabbing:
					menu.AddItem(("Interaction.Grab." + _handGrabType.Value).AsLocaleKey(), GetIcon(_handGrabType.Value), new colorX?(GetColor(_handGrabType.Value)), OpenHandGrabMenu);
					menu.AddToggleItem(_grabToggle, "Interaction.Grab.StickyGrab".AsLocaleKey(), "Interaction.Grab.HoldToHold".AsLocaleKey(), in RadiantUI_Constants.Hero.CYAN, in RadiantUI_Constants.Hero.YELLOW);
					break;
				case MenuOptions.LaserGrab:
					menu.AddItem((LocaleString)"Straighten", OfficialAssets.Common.Icons.Bang, new colorX?(colorX.Yellow), OnStraighten);
					menu.AddItem((LocaleString)"Rotate Up", OfficialAssets.Common.Icons.Up_Arrow, new colorX?(colorX.Green), OnRotateUp);
					menu.AddItem((LocaleString)"Rotate Right", OfficialAssets.Common.Icons.Right_Arrow, new colorX?(colorX.Red), OnRotateRight);
					menu.AddItem((LocaleString)"Rotate Forward", OfficialAssets.Common.Icons.Reload, new colorX?(colorX.Blue), OnRotateForward);
					menu.AddItem((LocaleString)"Unconstrained Rotation", OfficialAssets.Common.Icons.Circle, new colorX?(colorX.White), OnRotateUnconstrained);
					break;
				case MenuOptions.HandGrab:
					foreach (HandGrabType value in Enum.GetValues(typeof(HandGrabType)))
					{
						bool flag = value == _handGrabType.Value;
						menu.AddItem(("Interaction.Grab." + value).AsLocaleKey(flag ? "<b>{0}</b>" : ""), GetIcon(value), new colorX?(GetColor(value))).Button.SetupAction(SetGrabType, value);
					}
					break;
				}
				await PositionContextMenu(menu);
			}
		});
	}

	private bool CanSaveItem(Grabber grabber)
	{
		if (!base.World.CanSaveItems() || !ItemHelper.CanSave(grabber.HolderSlot))
		{
			return false;
		}
		if (HasGripEquippedTool)
		{
			return false;
		}
		if (grabber.IsHoldingInteractionBlock<IGrabbableSaveBlock>())
		{
			return false;
		}
		return true;
	}

	public async Task PositionContextMenu(ContextMenu menu)
	{
		IWorldElement summoner = menu.CurrentSummoner;
		ScreenController screen = null;
		if (base.InputInterface.ScreenActive)
		{
			screen = base.LocalUserRoot.GetRegisteredComponent<ScreenController>();
		}
		while (menu.CurrentSummoner == summoner && (menu.MenuState == FrooxEngine.ContextMenu.State.Opening || screen?.ActiveTargetting.Target?.IsViewTransitioning == true))
		{
			if (base.InputInterface.VR_Active)
			{
				float3 to = (menu.Slot.GlobalPosition - base.LocalUserRoot.HeadPosition).Normalized;
				float3 from = CurrentTip + CurrentTipForward * base.Slot.LocalScaleToGlobal(0.075f);
				floatQ a = floatQ.LookRotation(MathX.Lerp(CurrentTipForward, in to, 0.5f), base.LocalUserRoot?.Slot.Up ?? float3.Up);
				float num = MathX.Pow(menu.Lerp, 6f);
				menu.Slot.GlobalPosition = MathX.Lerp(in from, menu.Slot.GlobalPosition, num);
				menu.Slot.GlobalRotation = MathX.Slerp(in a, menu.Slot.GlobalRotation, num);
				menu.Slot.LocalScale = float3.One;
			}
			else
			{
				float3 a2 = base.World.LocalUserViewPosition;
				floatQ q = base.World.LocalUserViewRotation;
				float3? @float = base.World.GetScreen()?.GetWorldPoint(Laser.LastOrigin, Laser.LastDirection, null);
				if (@float.HasValue)
				{
					menu.Slot.GlobalPosition = @float.Value;
				}
				else
				{
					float num2 = MathX.Distance(in a2, base.LocalUserRoot.HeadPosition);
					menu.Slot.GlobalPosition = a2 + q * ILSpyHelper_AsRefReadOnly(float3.Forward) * (0.5f * base.LocalUserRoot.GlobalScale + num2);
				}
				menu.Slot.GlobalRotation = q;
				menu.Slot.GlobalScale = float3.One * MathX.Distance(menu.Slot.GlobalPosition, in a2) * 1.5f;
			}
			await default(NextUpdate);
		}
		static ref readonly T ILSpyHelper_AsRefReadOnly<T>(in T temp)
		{
			//ILSpy generated this function to help ensure overload resolution can pick the overload using 'in'
			return ref temp;
		}
	}

	public void CloseContextMenu()
	{
		ContextMenu target = ContextMenu.Target;
		if (target != null && target.CurrentSummoner == this)
		{
			target.Close();
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OpenLocomotionMenu(IButton button, ButtonEventData eventData)
	{
		OpenContextMenu(MenuOptions.Locomotion, 12f);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OpenGrabbingMenu(IButton button, ButtonEventData eventData)
	{
		OpenContextMenu(MenuOptions.Grabbing, 12f);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OpenHandGrabMenu(IButton button, ButtonEventData eventData)
	{
		OpenContextMenu(MenuOptions.HandGrab, 12f);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void SetLocomotion(IButton button, ButtonEventData eventData, ILocomotionModule module)
	{
		LocomotionController.Target.ActiveModule = module;
		CloseContextMenu();
	}

	[SyncMethod(typeof(Delegate), null)]
	private void SetGrabType(IButton button, ButtonEventData eventData, HandGrabType type)
	{
		_handGrabType.Value = type;
		CloseContextMenu();
	}

	[SyncMethod(typeof(Delegate), null)]
	private void DestroyGrabbed(IButton button, ButtonEventData eventData)
	{
		DestroyGrabbed();
	}

	[SyncMethod(typeof(Delegate), null)]
	private void DuplicateGrabbed(IButton button, ButtonEventData eventData)
	{
		ITool tool = RestoreGripEquippedTool();
		DuplicateGrabbed();
		if (tool != null)
		{
			GripEquip(tool);
		}
		if (base.World != Userspace.UserspaceWorld)
		{
			InteractionHandler userspaceController = Userspace.GetControllerData(Side)?.userspaceController;
			userspaceController?.RunSynchronously(delegate
			{
				userspaceController.DuplicateGrabbed();
			});
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void SaveGrabbed(IButton button, ButtonEventData eventData)
	{
		SaveGrabbed();
	}

	private void DestroyGrabbed()
	{
		RestoreGripEquippedTool();
		_grabber.Target.DestroyGrabbed();
		if (base.World != Userspace.UserspaceWorld)
		{
			InteractionHandler userspaceController = Userspace.GetControllerData(Side)?.userspaceController;
			userspaceController?.RunSynchronously(delegate
			{
				userspaceController.Grabber?.DestroyGrabbed();
			});
		}
		CloseContextMenu();
	}

	private void SaveGrabbed()
	{
		InventoryBrowser.CurrentUserspaceInventory?.StartTask(async delegate
		{
			await default(ToWorld);
			InventoryBrowser.CurrentUserspaceInventory.SaveItemFromGrabber(Grabber);
		});
		CloseContextMenu();
	}

	private void UpdateUndoButtons()
	{
		if (Owner == base.LocalUser && !_undoInvalidated)
		{
			_undoInvalidated = true;
			RunSynchronously(delegate
			{
				UpdateUndoRedoItems();
			});
		}
	}

	private string TrimUndoDescription(string description)
	{
		if (description.Length <= 32)
		{
			return description;
		}
		return description.Substring(0, 29) + "...";
	}

	private void UpdateUndoRedoItems()
	{
		ContextMenuItem target = _undoItem.Target;
		ContextMenuItem target2 = _redoItem.Target;
		if ((target == null && target2 == null) || ContextMenu.Target.MenuState == FrooxEngine.ContextMenu.State.Closed)
		{
			return;
		}
		UndoManager undoManager = base.World.GetUndoManager();
		IUndoable undoStep = undoManager.GetUndoStep();
		IUndoable redoStep = undoManager.GetRedoStep();
		if (target != null)
		{
			target.Button.Enabled = undoStep != null;
			string localized = this.GetLocalized("Interaction.Undo");
			if (undoStep != null)
			{
				target.LabelText = localized + " <size=75%>" + TrimUndoDescription(undoStep.Description) + "</size>";
			}
			else
			{
				target.LabelText = localized;
			}
		}
		if (target2 != null)
		{
			target2.Button.Enabled = redoStep != null;
			string localized2 = this.GetLocalized("Interaction.Redo");
			if (redoStep != null)
			{
				target2.LabelText = localized2 + " <size=75%>" + TrimUndoDescription(redoStep.Description) + "</size>";
			}
			else
			{
				target2.LabelText = localized2;
			}
		}
		_undoInvalidated = false;
	}

	private void DuplicateGrabbed()
	{
		base.World.BeginUndoBatch("Undo.DuplicateGrabbed".AsLocaleKey());
		try
		{
			foreach (IGrabbable grabbedObject in _grabber.Target.GrabbedObjects)
			{
				if (Grabber.ItemHasInteractionBlock<IDuplicateBlock>(grabbedObject.Slot))
				{
					continue;
				}
				Slot slot = grabbedObject.Slot.GetObjectRoot(_grabber.Target.Slot).Duplicate();
				slot.CreateSpawnUndoPoint();
				slot.GetComponentsInChildren<IGrabbable>().ForEach(delegate(IGrabbable g)
				{
					if (g.IsGrabbed)
					{
						g.Release(g.Grabber);
					}
				});
			}
		}
		catch (Exception ex)
		{
			base.Debug.Error("Exception duplicating items!\n" + ex);
		}
		base.World.EndUndoBatch();
	}

	private void SwitchLocomotionMode(LegacySegmentCircleMenuController.Item item)
	{
		LocomotionController.Target?.NextLocomotionModule();
	}

	[SyncMethod(typeof(Delegate), null)]
	private void Undo(IButton button, ButtonEventData data)
	{
		base.World.Undo();
	}

	[SyncMethod(typeof(Delegate), null)]
	private void Redo(IButton button, ButtonEventData data)
	{
		base.World.Redo();
	}

	[SyncMethod(typeof(Delegate), null)]
	private void ResetUserScale(IButton button, ButtonEventData eventData)
	{
		if (CanScale)
		{
			base.Slot.ActiveUserRoot.SetUserScale(base.Slot.ActiveUserRoot.GetDefaultScale(), 0.25f);
			CloseContextMenu();
		}
	}

	private void AlignHeldItem()
	{
		ScreenController screen = base.World.GetScreen();
		bool flag = false;
		if (screen != null)
		{
			foreach (Slot child in Grabber.HolderSlot.Children)
			{
				if (screen.AlignItem(child, out var orientation, out var position, out var scale))
				{
					flag = true;
					orientation = child.Parent.GlobalRotationToLocal(in orientation);
					child.Rotation_Field.TweenTo(orientation, 0.25f, CurvePreset.Sine, null, null, child.Parent);
					if (position.HasValue)
					{
						position = child.Parent.GlobalPointToLocal(position.Value);
						child.Position_Field.TweenTo(position.Value, 0.25f, CurvePreset.Sine, null, null, child.Parent);
					}
					if (scale.HasValue)
					{
						scale = child.Parent.GlobalScaleToLocal(scale.Value);
						child.Scale_Field.TweenTo(scale.Value, 0.25f, CurvePreset.Sine, null, null, child.Parent);
					}
					continue;
				}
				break;
			}
		}
		if (!flag)
		{
			AlignHeldItem(float3.Up);
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnStraighten(IButton button, ButtonEventData eventData)
	{
		AlignHeldItem(GetLaserRotationAxis());
	}

	private void OnStraightenUp(IButton button, ButtonEventData eventData)
	{
		AlignHeldItem(float3.Up);
	}

	private void OnStraightenRight(IButton button, ButtonEventData eventData)
	{
		AlignHeldItem(float3.Right);
	}

	private void OnStraightenForward(IButton button, ButtonEventData eventData)
	{
		AlignHeldItem(float3.Forward);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnRotateUp(IButton button, ButtonEventData eventData)
	{
		SwitchLaserRotation(LaserRotationType.AxisY);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnRotateRight(IButton button, ButtonEventData eventData)
	{
		SwitchLaserRotation(LaserRotationType.AxisX);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnRotateForward(IButton button, ButtonEventData eventData)
	{
		SwitchLaserRotation(LaserRotationType.AxisZ);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnRotateUnconstrained(IButton button, ButtonEventData eventData)
	{
		SwitchLaserRotation(LaserRotationType.Unconstrained);
	}

	private void AlignHeldItem(in float3 referenceAxis)
	{
		foreach (Slot child in Grabber.HolderSlot.Children)
		{
			AlignHeldItem(child, new float3?(referenceAxis));
		}
	}

	private void SwitchLaserRotation(LaserRotationType type)
	{
		UpdateHolderRotation();
		List<float3> list = Pool.BorrowList<float3>();
		List<floatQ> list2 = Pool.BorrowList<floatQ>();
		Slot holderSlot = Grabber.HolderSlot;
		for (int i = 0; i < holderSlot.ChildrenCount; i++)
		{
			Slot slot = holderSlot[i];
			slot.Rotation_Field.ClearTweens();
			list.Add(slot.GlobalPosition);
			list2.Add(slot.GlobalRotation);
		}
		_laserRotationType.Value = type;
		UpdateHolderRotation();
		for (int j = 0; j < holderSlot.ChildrenCount; j++)
		{
			Slot slot2 = holderSlot[j];
			slot2.GlobalPosition = list[j];
			slot2.GlobalRotation = list2[j];
		}
		Pool.Return(ref list);
		Pool.Return(ref list2);
	}

	private float3 GetLaserRotationAxis()
	{
		return _laserRotationType.Value switch
		{
			LaserRotationType.AxisX => float3.Right, 
			LaserRotationType.AxisY => float3.Up, 
			LaserRotationType.AxisZ => float3.Forward, 
			_ => float3.Zero, 
		};
	}

	private void SlideGrabbed(float slide, float rotate)
	{
		float num = MathX.Abs(slide);
		float num2 = MathX.Abs(rotate);
		if (MathX.Approximately(num, 0f) && MathX.Approximately(num2, 0f))
		{
			return;
		}
		if (num2 > num)
		{
			_holderAxisOffset.Value += rotate * base.Time.Delta * 360f;
		}
		else
		{
			if (!_laserGrabDistance.HasValue)
			{
				return;
			}
			float value = _laserGrabDistance.Value;
			float num3 = MathX.Max(1f, _laserGrabDistance.Value) * 4f;
			value += slide * num3 * base.Time.Delta;
			_laserGrabDistance = MathX.Clamp(value, base.InputInterface.VR_Active ? 0f : 0.05f, 10000f);
			if (!(value < 0f) || !base.InputInterface.VR_Active)
			{
				return;
			}
			ITool componentInChildren = Grabber.HolderSlot.GetComponentInChildren<ITool>();
			if (ActiveTool == null && componentInChildren != null)
			{
				_grabBlockLaser = true;
				if (componentInChildren != null)
				{
					if (CanEquip(componentInChildren))
					{
						EndGrab(supressEvents: true);
						Equip(componentInChildren, lockEquip: true);
						BlockSecondary = true;
					}
					else if (base.Time.WorldTime - lastSlideEquipPermissionMessage > 1.0)
					{
						lastSlideEquipPermissionMessage = base.Time.WorldTime;
						Equip(componentInChildren, lockEquip: true);
					}
				}
				return;
			}
			ICollider componentInChildren2 = Grabber.HolderSlot.GetComponentInChildren<ICollider>();
			if (componentInChildren2 != null)
			{
				_grabBlockLaser = true;
				EndGrab(supressEvents: true);
				Laser.ClearLaserTimeout();
				Laser.SetNewTarget(null);
				base.Slot.TryVibrateMedium();
				if (!componentInChildren2.IsRemoved)
				{
					Grab(laserGrab: false, componentInChildren2);
				}
			}
		}
	}

	private void AlignHeldItem(Slot target, in float3? overrideAxis = null)
	{
		float3 b = base.LocalUserRoot.Slot.LocalDirectionToSpace(overrideAxis ?? GetLaserRotationAxis(), target);
		float3 localDirection = float3.Zero;
		float num = -1f;
		for (int i = 0; i < 6; i++)
		{
			float3 a = float3.Zero.SetComponent((i <= 2) ? 1 : (-1), i % 3);
			float num2 = MathX.Dot(in a, in b);
			if (num2 > num)
			{
				localDirection = a;
				num = num2;
			}
		}
		b = target.LocalDirectionToParent(in b);
		floatQ a2 = floatQ.FromToRotation(target.LocalDirectionToParent(in localDirection), in b);
		target.Rotation_Field.TweenTo(a2 * target.LocalRotation, 0.25f).OnlyUnderParent.Target = target.Parent;
	}

	public void SetToolAnchor(Slot anchor)
	{
		anchor = anchor ?? base.Slot;
		_toolRoot.Target.SetParent(anchor, keepGlobalTransform: false);
	}

	public void SetToolshelfAnchor(Slot anchor)
	{
		Slot target = _itemShelfSlot.Target;
		if (target != null)
		{
			if (anchor != null)
			{
				target.SetParent(anchor, keepGlobalTransform: false);
				target.LocalPosition = float3.Zero;
				target.LocalRotation = SHELF_DEFAULT_ROTATION;
			}
			else
			{
				target.SetParent(base.Slot, keepGlobalTransform: false);
				bool flag = Side.Value == Chirality.Left;
				target.LocalPosition = (flag ? SHELF_DEFAULT_POSITION_LEFT : SHELF_DEFAULT_POSITION_RIGHT);
				target.LocalRotation = SHELF_DEFAULT_ROTATION;
			}
		}
	}

	public void SetGrabberAnchor(Slot anchor)
	{
		anchor = anchor ?? base.Slot;
		_grabber.Target.Slot.SetParent(anchor, keepGlobalTransform: false);
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		Side = new Sync<Chirality>();
		LocomotionController = new RelayRef<LocomotionController>();
		GrabSmoothing = new Sync<float>();
		_streamDriver = new SyncRef<InteractionHandlerStreamDriver>();
		_undoItem = new SyncRef<ContextMenuItem>();
		_redoItem = new SyncRef<ContextMenuItem>();
		ContextMenu = new SyncRef<ContextMenu>();
		EquippingEnabled = new Sync<bool>();
		MenuEnabled = new Sync<bool>();
		UserScalingEnabled = new Sync<bool>();
		VisualEnabled = new Sync<bool>();
		PointingGrab = new Sync<bool>();
		PointingTouch = new Sync<bool>();
		_toolRoot = new SyncRef<Slot>();
		_laserSlot = new SyncRef<Slot>();
		_laserPosition = new FieldDrive<float3>();
		_laserRotation = new FieldDrive<floatQ>();
		_interactionLaser = new SyncRef<InteractionLaser>();
		_laserEnabled = new Sync<bool>();
		_handGrabType = new Sync<HandGrabType>();
		_grabToggle = new Sync<bool>();
		_holderPos = new FieldDrive<float3>();
		_holderRot = new FieldDrive<floatQ>();
		_laserRotationType = new Sync<LaserRotationType>();
		_holderAxisOffset = new Sync<float>();
		_holderRotationOffset = new Sync<floatQ>();
		_holderRotationReference = new Sync<floatQ?>();
		_originalTwistOffset = new Sync<float>();
		_userspaceToggleIndicator = new SyncRef<RingMesh>();
		ToolHolder = new SyncRef<Slot>();
		ShowInteractionHints = new Sync<bool>();
		_grabberSphereActive = new FieldDrive<bool>();
		_grabIgnoreRoot = new SyncRef<Slot>();
		_grabber = new SyncRef<Grabber>();
		_currentGrabType = new Sync<GrabType>();
		ActiveToolLink = new LinkTargetRef<ITool>();
		_activeToolGripPoseReference = new SyncRef<GripPoseReference>();
		_toolLocked = new Sync<bool>();
		_grabMaterial = new SyncRef<FresnelMaterial>();
		_itemShelfSlot = new SyncRef<Slot>();
		_itemShelf = new SyncRef<ItemShelf>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => Side, 
			4 => LocomotionController, 
			5 => GrabSmoothing, 
			6 => _streamDriver, 
			7 => _undoItem, 
			8 => _redoItem, 
			9 => ContextMenu, 
			10 => EquippingEnabled, 
			11 => MenuEnabled, 
			12 => UserScalingEnabled, 
			13 => VisualEnabled, 
			14 => PointingGrab, 
			15 => PointingTouch, 
			16 => _toolRoot, 
			17 => _laserSlot, 
			18 => _laserPosition, 
			19 => _laserRotation, 
			20 => _interactionLaser, 
			21 => _laserEnabled, 
			22 => _handGrabType, 
			23 => _grabToggle, 
			24 => _holderPos, 
			25 => _holderRot, 
			26 => _laserRotationType, 
			27 => _holderAxisOffset, 
			28 => _holderRotationOffset, 
			29 => _holderRotationReference, 
			30 => _originalTwistOffset, 
			31 => _userspaceToggleIndicator, 
			32 => ToolHolder, 
			33 => ShowInteractionHints, 
			34 => _grabberSphereActive, 
			35 => _grabIgnoreRoot, 
			36 => _grabber, 
			37 => _currentGrabType, 
			38 => ActiveToolLink, 
			39 => _activeToolGripPoseReference, 
			40 => _toolLocked, 
			41 => _grabMaterial, 
			42 => _itemShelfSlot, 
			43 => _itemShelf, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static InteractionHandler __New()
	{
		return new InteractionHandler();
	}
}
