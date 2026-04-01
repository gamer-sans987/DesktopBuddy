using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Elements.Core;
using FrooxEngine.CommonAvatar;
using FrooxEngine.FinalIK;
using Renderite.Shared;

namespace FrooxEngine;

[Category(new string[] { "Users" })]
public class UserRoot : Component
{
	public enum UserNode
	{
		None,
		Root,
		GroundProjectedHead,
		Head,
		Hips,
		Feet,
		View
	}

	public const float MAX_DISTANCE = 100000f;

	private ILinkRef _lastLink;

	private User _lastUser;

	private Slot _localNameplate;

	private AvatarManager _avatarManager;

	private int _avatarManagerVersion;

	private Slot _rootSpaceHint;

	public readonly SyncRef<IRenderSettingsSource> RenderSettings;

	public readonly SyncRef<ScreenController> ScreenController;

	public readonly SyncRef<Slot> OverrideRoot;

	public readonly SyncRef<Slot> OverrideView;

	public readonly SyncRef<AudioListener> PrimaryListener;

	private BoundingBox _cachedPlayerBounds;

	private int _cachedPlayerBoundsFrame = -1;

	private bool _settingRegistered;

	private DesktopRenderSettings _renderSettings;

	private Slot _cachedHeadSlot;

	private Slot _cachedLeftHandSlot;

	private Slot _cachedRightHandSlot;

	private Slot _cachedLeftControllerSlot;

	private Slot _cachedRightControllerSlot;

	private Slot _cachedLeftFootSlot;

	private Slot _cachedRightFootSlot;

	private TrackedDevicePositioner _hipsPositioner;

	private TrackedDevicePositioner _leftFootPositioner;

	private TrackedDevicePositioner _rightFootPositioner;

	private HashSet<Component> _registeredComponents = new HashSet<Component>();

	private Dictionary<Type, IList> _perTypeComponents = new Dictionary<Type, IList>();

	private CancellationTokenSource _scaleAnimCancel;

	public static float3 DEFAULT_NECK_OFFSET => new float3(0f, -0.12f, -0.1f);

	public Slot LocalNameplate => _localNameplate;

	public User ActiveUser
	{
		get
		{
			if (base.DirectLink != _lastLink)
			{
				_lastLink = base.DirectLink;
				_lastUser = _lastLink.FindNearestParent<User>();
			}
			return _lastUser;
		}
	}

	public Slot RootSpaceHint
	{
		get
		{
			if (_rootSpaceHint != null && (_rootSpaceHint.IsRemoved || _rootSpaceHint.IsChildOf(base.Slot, includeSelf: true)))
			{
				_rootSpaceHint = null;
			}
			return _rootSpaceHint;
		}
		set
		{
			_rootSpaceHint = value;
		}
	}

	public bool IsPrimaryListenerActive => PrimaryListener.Target?.EnabledAndActive ?? false;

	public BoundingBox PlayerBounds
	{
		get
		{
			if (_cachedPlayerBoundsFrame != base.Time.LocalUpdateIndex)
			{
				BoundingBox cachedPlayerBounds = BoundingBox.Empty();
				float size = base.Slot.LocalScaleToGlobal(0.1f);
				cachedPlayerBounds.Encapsulate(FeetPosition, size);
				cachedPlayerBounds.Encapsulate(HeadPosition, size);
				cachedPlayerBounds.Encapsulate(LeftControllerPosition, size);
				cachedPlayerBounds.Encapsulate(RightControllerPosition, size);
				_cachedPlayerBounds = cachedPlayerBounds;
				_cachedPlayerBoundsFrame = base.Time.LocalUpdateIndex;
			}
			return _cachedPlayerBounds;
		}
	}

	public float DesktopFOV
	{
		get
		{
			float fov = _renderSettings?.FieldOfView.Value ?? 75f;
			Sync<bool> sync = _renderSettings?.SprintFieldOfViewZoom;
			if (sync == null || (bool)sync)
			{
				ForeachRegisteredComponent(delegate(IFieldOfViewModifier m)
				{
					fov = m.ProcessFOV(fov);
				});
			}
			fov = MathX.FilterInvalid(fov);
			fov = MathX.Clamp(fov, 1f, 179f);
			return fov;
		}
	}

	public bool ReceivedFirstPositionalData
	{
		get
		{
			if (HeadSlot == null || (!(HeadSlot.LocalPosition != float3.Zero) && !(HeadSlot.LocalRotation != floatQ.Identity)))
			{
				if (ActiveUser != null)
				{
					return ActiveUser.HeadDevice.IsCamera();
				}
				return false;
			}
			return true;
		}
	}

	public float GlobalScale
	{
		get
		{
			return base.Slot.GlobalScale.x;
		}
		set
		{
			base.Slot.GlobalScale = float3.One * value;
		}
	}

	public float LocalScale
	{
		get
		{
			return base.Slot.LocalScale.x;
		}
		set
		{
			base.Slot.LocalScale = float3.One * value;
		}
	}

	public Slot HeadSlot
	{
		get
		{
			if ((_cachedHeadSlot == null || _cachedHeadSlot.IsDestroyed || _cachedHeadSlot.IsDisposed) && !IsRemoved)
			{
				_cachedHeadSlot = base.Slot.FindChild("Head") ?? base.Slot.FindChild("Body Nodes")?.FindChild("Head");
			}
			return _cachedHeadSlot;
		}
	}

	public float3 LeftHandPosition => LeftHandSlot?.GlobalPosition ?? float3.Zero;

	public float3 RightHandPosition => RightHandSlot?.GlobalPosition ?? float3.Zero;

	public floatQ LeftHandRotation => LeftHandSlot?.GlobalRotation ?? floatQ.Identity;

	public floatQ RightHandRotation => RightHandSlot?.GlobalRotation ?? floatQ.Identity;

	public float3 LeftControllerPosition => LeftControllerSlot?.GlobalPosition ?? float3.Zero;

	public float3 RightControllerPosition => RightControllerSlot?.GlobalPosition ?? float3.Zero;

	public float3 LeftHipPosition => (GetRegisteredComponent<VRIKAvatar>()?.IK?.Target?.Solver?.BoneReferences?.leftThigh?.Target)?.GlobalPosition ?? (HipsPosition + HipsRotation * float3.Left * 0.2f * GlobalScale);

	public float3 RightHipPosition => (GetRegisteredComponent<VRIKAvatar>()?.IK?.Target?.Solver?.BoneReferences?.rightThigh?.Target)?.GlobalPosition ?? (HipsPosition + HipsRotation * float3.Right * 0.2f * GlobalScale);

	public float3 LeftShoulderPosition
	{
		get
		{
			VRIKAvatar registeredComponent = GetRegisteredComponent<VRIKAvatar>();
			return (registeredComponent?.IK?.Target?.Solver?.BoneReferences?.leftShoulder?.Target ?? registeredComponent?.IK?.Target?.Solver?.BoneReferences?.leftUpperArm?.Target)?.GlobalPosition ?? (NeckPosition + HeadFacingRotation * float3.Left * 0.25f * GlobalScale);
		}
	}

	public float3 RightShoulderPosition
	{
		get
		{
			VRIKAvatar registeredComponent = GetRegisteredComponent<VRIKAvatar>();
			return (registeredComponent?.IK?.Target?.Solver?.BoneReferences?.rightShoulder?.Target ?? registeredComponent?.IK?.Target?.Solver?.BoneReferences?.rightUpperArm?.Target)?.GlobalPosition ?? (NeckPosition + HeadFacingRotation * float3.Right * 0.25f * GlobalScale);
		}
	}

	public float3 LeftUpperArmPosition => (GetRegisteredComponent<VRIKAvatar>()?.IK?.Target?.Solver?.BoneReferences?.leftUpperArm?.Target)?.GlobalPosition ?? (NeckPosition + HeadFacingRotation * float3.Left * 0.3f * GlobalScale);

	public float3 RightUpperArmPosition => (GetRegisteredComponent<VRIKAvatar>()?.IK?.Target?.Solver?.BoneReferences?.rightUpperArm?.Target)?.GlobalPosition ?? (NeckPosition + HeadFacingRotation * float3.Right * 0.3f * GlobalScale);

	public float3 LeftFootPosition => LeftFootSlot?.GlobalPosition ?? (GetRegisteredComponent<VRIKAvatar>()?.LeftFootNode.Slot)?.GlobalPosition ?? (FeetPosition + HeadFacingRotation * float3.Left * 0.2f * GlobalScale);

	public float3 RightFootPosition => RightFootSlot?.GlobalPosition ?? (GetRegisteredComponent<VRIKAvatar>()?.RightFootNode.Slot)?.GlobalPosition ?? (FeetPosition + HeadFacingRotation * float3.Right * 0.2f * GlobalScale);

	public floatQ LeftFootRotation => LeftFootSlot?.GlobalRotation ?? floatQ.Identity;

	public floatQ RightFootRotation => RightFootSlot?.GlobalRotation ?? floatQ.Identity;

	public float3 LeftKneePosition => (GetRegisteredComponent<VRIKAvatar>()?.IK?.Target?.Solver?.BoneReferences?.leftCalf?.Target)?.GlobalPosition ?? MathX.Lerp(LeftHipPosition, LeftFootPosition, 0.5f);

	public float3 RightKneePosition => (GetRegisteredComponent<VRIKAvatar>()?.IK?.Target?.Solver?.BoneReferences?.rightCalf?.Target)?.GlobalPosition ?? MathX.Lerp(RightHipPosition, RightFootPosition, 0.5f);

	public float3 LeftElbowPosition => (GetRegisteredComponent<VRIKAvatar>()?.IK?.Target?.Solver?.BoneReferences?.leftForearm?.Target)?.GlobalPosition ?? MathX.Lerp(LeftShoulderPosition, LeftHandPosition, 0.5f);

	public float3 RightElbowPosition => (GetRegisteredComponent<VRIKAvatar>()?.IK?.Target?.Solver?.BoneReferences?.rightForearm?.Target)?.GlobalPosition ?? MathX.Lerp(RightShoulderPosition, RightHandPosition, 0.5f);

	public float3 LocalLeftHandPosition
	{
		get
		{
			Slot leftHandSlot = LeftHandSlot;
			if (leftHandSlot == null)
			{
				return float3.Zero;
			}
			return base.Slot.SpacePointToLocal(leftHandSlot.LocalPosition, leftHandSlot.Parent);
		}
	}

	public float3 LocalRightHandPosition
	{
		get
		{
			Slot rightHandSlot = RightHandSlot;
			if (rightHandSlot == null)
			{
				return float3.Zero;
			}
			return base.Slot.SpacePointToLocal(rightHandSlot.LocalPosition, rightHandSlot.Parent);
		}
	}

	public floatQ LocalLeftHandRotation
	{
		get
		{
			Slot leftHandSlot = LeftHandSlot;
			if (leftHandSlot == null)
			{
				return floatQ.Identity;
			}
			return base.Slot.SpaceRotationToLocal(leftHandSlot.LocalRotation, leftHandSlot.Parent);
		}
	}

	public floatQ LocalRightHandRotation
	{
		get
		{
			Slot rightHandSlot = RightHandSlot;
			if (rightHandSlot == null)
			{
				return floatQ.Identity;
			}
			return base.Slot.SpaceRotationToLocal(rightHandSlot.LocalRotation, rightHandSlot.Parent);
		}
	}

	private TrackedDevicePositioner HipsDevice
	{
		get
		{
			if ((_hipsPositioner == null || _hipsPositioner.CorrespondingBodyNode.Value != BodyNode.Hips) && !IsRemoved)
			{
				_hipsPositioner = GetRegisteredComponent((TrackedDevicePositioner p) => (BodyNode)p.CorrespondingBodyNode == BodyNode.Hips);
			}
			return _hipsPositioner;
		}
	}

	private TrackedDevicePositioner LeftFootDevice
	{
		get
		{
			if ((_leftFootPositioner == null || _leftFootPositioner.CorrespondingBodyNode.Value != BodyNode.LeftFoot) && !IsRemoved)
			{
				_leftFootPositioner = GetRegisteredComponent((TrackedDevicePositioner p) => (BodyNode)p.CorrespondingBodyNode == BodyNode.LeftFoot);
			}
			return _leftFootPositioner;
		}
	}

	private TrackedDevicePositioner RightFootDevice
	{
		get
		{
			if ((_rightFootPositioner == null || _rightFootPositioner.CorrespondingBodyNode.Value != BodyNode.RightFoot) && !IsRemoved)
			{
				_rightFootPositioner = GetRegisteredComponent((TrackedDevicePositioner p) => (BodyNode)p.CorrespondingBodyNode == BodyNode.RightFoot);
			}
			return _rightFootPositioner;
		}
	}

	public float3 LocalNeckPosition => base.Slot.GlobalPointToLocal(NeckPosition);

	public float3 NeckPosition
	{
		get
		{
			Slot slot = GetRegisteredComponent((VRIKAvatar avatar) => avatar.IsEquipped)?.IK.Target?.Solver.BoneReferences.neck.Target;
			if (slot != null)
			{
				return slot.GlobalPosition;
			}
			float3 v = GetNeckOffset();
			v = LocalHeadRotation * v;
			return base.Slot.LocalPointToGlobal(LocalHeadPosition + v);
		}
		set
		{
			SetProxyPosition(NeckPosition, in value);
		}
	}

	public float3 HipsPosition
	{
		get
		{
			TrackedDevicePositioner hipsDevice = HipsDevice;
			if (hipsDevice != null && hipsDevice.IsTracking.Value && hipsDevice.IsActive.Value && hipsDevice.BodyNodeRoot.Target != null)
			{
				return hipsDevice.BodyNodeRoot.Target.GlobalPosition;
			}
			VRIKAvatar registeredComponent = GetRegisteredComponent((VRIKAvatar avatar) => avatar.IsEquipped);
			Slot slot = registeredComponent?.IK.Target?.Solver.BoneReferences.pelvis.Target;
			if (slot != null && registeredComponent.PelvisCalibrated.Value)
			{
				_ = registeredComponent.Slot;
				Slot pelvisProxy = registeredComponent.PelvisProxy;
				return registeredComponent.PelvisNode.Slot.GetTransformedByAnother(pelvisProxy, slot.GlobalPosition, slot.GlobalRotation, registeredComponent.Slot.GlobalScale).DecomposedPosition;
			}
			return MathX.Lerp(FeetPosition, HeadPosition, 0.6f) + base.Slot.LocalVectorToGlobal(LocalHeadFacingDirection * -0.1f);
		}
		set
		{
			SetProxyPosition(HipsPosition, in value);
		}
	}

	public float3 LocalHipsPosition => base.Slot.GlobalPointToLocal(HipsPosition);

	public floatQ HipsRotation
	{
		get
		{
			TrackedDevicePositioner hipsDevice = HipsDevice;
			if (hipsDevice?.BodyNodeRoot.Target != null && hipsDevice.IsTracking.Value)
			{
				return hipsDevice.BodyNodeRoot.Target.GlobalRotation;
			}
			return HeadFacingRotation;
		}
		set
		{
			SetProxyRotation(HipsRotation, in value);
		}
	}

	public Slot LeftHandSlot
	{
		get
		{
			if ((_cachedLeftHandSlot == null || _cachedLeftHandSlot.IsDestroyed || _cachedLeftHandSlot.IsDisposed) && !IsRemoved)
			{
				TrackedDevicePositioner registeredComponent = GetRegisteredComponent((TrackedDevicePositioner p) => p.AutoBodyNode.Value == BodyNode.LeftHand);
				_cachedLeftHandSlot = registeredComponent?.BodyNodeRoot.Target ?? registeredComponent?.Slot;
			}
			return _cachedLeftHandSlot;
		}
	}

	public Slot RightHandSlot
	{
		get
		{
			if ((_cachedRightHandSlot == null || _cachedRightHandSlot.IsDestroyed || _cachedRightHandSlot.IsDisposed) && !IsRemoved)
			{
				TrackedDevicePositioner registeredComponent = GetRegisteredComponent((TrackedDevicePositioner p) => p.AutoBodyNode.Value == BodyNode.RightHand);
				_cachedRightHandSlot = registeredComponent?.BodyNodeRoot.Target ?? registeredComponent?.Slot;
			}
			return _cachedRightHandSlot;
		}
	}

	public Slot LeftControllerSlot
	{
		get
		{
			if ((_cachedLeftControllerSlot == null || _cachedLeftControllerSlot.IsDestroyed || _cachedLeftControllerSlot.IsDisposed) && !IsRemoved)
			{
				TrackedDevicePositioner registeredComponent = GetRegisteredComponent((TrackedDevicePositioner p) => p.AutoBodyNode.Value == BodyNode.LeftController);
				_cachedLeftControllerSlot = registeredComponent?.BodyNodeRoot.Target ?? registeredComponent?.Slot;
			}
			return _cachedLeftControllerSlot;
		}
	}

	public Slot RightControllerSlot
	{
		get
		{
			if ((_cachedRightControllerSlot == null || _cachedRightControllerSlot.IsDestroyed || _cachedRightControllerSlot.IsDisposed) && !IsRemoved)
			{
				TrackedDevicePositioner registeredComponent = GetRegisteredComponent((TrackedDevicePositioner p) => p.AutoBodyNode.Value == BodyNode.RightController);
				_cachedRightControllerSlot = registeredComponent?.BodyNodeRoot.Target ?? registeredComponent?.Slot;
			}
			return _cachedRightControllerSlot;
		}
	}

	public Slot LeftFootSlot
	{
		get
		{
			if ((_cachedLeftFootSlot == null || _cachedLeftFootSlot.IsDestroyed || _cachedLeftFootSlot.IsDisposed) && !IsRemoved)
			{
				TrackedDevicePositioner registeredComponent = GetRegisteredComponent((TrackedDevicePositioner p) => p.AutoBodyNode.Value == BodyNode.LeftFoot);
				_cachedLeftFootSlot = registeredComponent?.BodyNodeRoot.Target ?? registeredComponent?.Slot;
			}
			return _cachedLeftFootSlot;
		}
	}

	public Slot RightFootSlot
	{
		get
		{
			if ((_cachedRightFootSlot == null || _cachedRightFootSlot.IsDestroyed || _cachedRightFootSlot.IsDisposed) && !IsRemoved)
			{
				TrackedDevicePositioner registeredComponent = GetRegisteredComponent((TrackedDevicePositioner p) => p.AutoBodyNode.Value == BodyNode.RightFoot);
				_cachedRightFootSlot = registeredComponent?.BodyNodeRoot.Target ?? registeredComponent?.Slot;
			}
			return _cachedRightFootSlot;
		}
	}

	public float3 HeadPosition
	{
		get
		{
			return HeadSlot?.GlobalPosition ?? base.Slot.GlobalPosition;
		}
		set
		{
			SetProxyPosition(HeadPosition, in value);
		}
	}

	public float3 GroundProjectedHeadPosition
	{
		get
		{
			return base.Slot.LocalPointToGlobal(LocalHeadPosition.x_z);
		}
		set
		{
			SetProxyPosition(GroundProjectedHeadPosition, in value);
		}
	}

	public float3 ViewPosition
	{
		get
		{
			Slot target = OverrideView.Target;
			if (target != null)
			{
				return target.GlobalPosition;
			}
			if (ActiveUser == base.LocalUser)
			{
				return base.World.LocalUserViewPosition;
			}
			ViewReferenceController registeredComponent = GetRegisteredComponent<ViewReferenceController>();
			if (registeredComponent != null && registeredComponent.AreStreamsActive)
			{
				return registeredComponent.Slot.GlobalPosition;
			}
			return HeadPosition;
		}
		set
		{
			SetProxyPosition(ViewPosition, in value);
		}
	}

	public floatQ ViewRotation
	{
		get
		{
			Slot target = OverrideView.Target;
			if (target != null)
			{
				return target.GlobalRotation;
			}
			if (ActiveUser == base.LocalUser)
			{
				return base.World.LocalUserViewRotation;
			}
			ViewReferenceController registeredComponent = GetRegisteredComponent<ViewReferenceController>();
			if (registeredComponent != null && registeredComponent.AreStreamsActive)
			{
				return registeredComponent.Slot.GlobalRotation;
			}
			return HeadRotation;
		}
		set
		{
			SetProxyRotation(ViewRotation, in value);
		}
	}

	public float3 LocalHeadPosition
	{
		get
		{
			Slot headSlot = HeadSlot;
			if (headSlot == null)
			{
				return float3.Zero;
			}
			return base.Slot.SpacePointToLocal(headSlot.LocalPosition, headSlot.Parent);
		}
	}

	public floatQ LocalHeadRotation
	{
		get
		{
			Slot headSlot = HeadSlot;
			if (headSlot == null)
			{
				return floatQ.Identity;
			}
			return base.Slot.SpaceRotationToLocal(headSlot.LocalRotation, headSlot.Parent);
		}
	}

	public float3 LocalHeadFacingDirection => ((HeadSlot?.LocalRotation ?? floatQ.Identity) * float3.Forward).x_z.Normalized;

	public float3 HeadFacingDirection
	{
		get
		{
			return base.Slot.LocalDirectionToGlobal(LocalHeadFacingDirection);
		}
		set
		{
			float3 @float = base.Slot.GlobalDirectionToLocal(in value);
			float3 to = base.Slot.LocalDirectionToGlobal(@float.x_z);
			float3 headPosition = HeadPosition;
			base.Slot.GlobalRotation = base.Slot.GlobalRotation * floatQ.FromToRotation(HeadFacingDirection, in to);
			HeadPosition = headPosition;
		}
	}

	public floatQ HeadFacingRotation
	{
		get
		{
			return floatQ.LookRotation(HeadFacingDirection, base.Slot.Up);
		}
		set
		{
			SetProxyRotation(HeadFacingRotation, in value);
		}
	}

	public floatQ HeadRotation
	{
		get
		{
			return HeadSlot?.GlobalRotation ?? base.Slot.GlobalRotation;
		}
		set
		{
			float3 headPosition = HeadPosition;
			base.Slot.GlobalRotation = base.Slot.GlobalRotation * floatQ.FromToRotation(HeadRotation, value);
			HeadPosition = headPosition;
		}
	}

	public float3 FeetPosition
	{
		get
		{
			TrackedDevicePositioner leftFootDevice = LeftFootDevice;
			TrackedDevicePositioner rightFootDevice = RightFootDevice;
			if (leftFootDevice?.BodyNodeRoot.Target != null && rightFootDevice?.BodyNodeRoot.Target != null && leftFootDevice.IsTracking.Value && rightFootDevice.IsTracking.Value)
			{
				return (leftFootDevice.BodyNodeRoot.Target.GlobalPosition + rightFootDevice.BodyNodeRoot.Target.GlobalPosition) * 0.5f;
			}
			VRIKAvatar registeredComponent;
			if ((registeredComponent = GetRegisteredComponent<VRIKAvatar>()) != null && registeredComponent.LeftFootNode != null && registeredComponent.RightFootNode != null)
			{
				return (registeredComponent.LeftFootNode.Slot.GlobalPosition + registeredComponent.RightFootNode.Slot.GlobalPosition) * 0.5f;
			}
			Slot headSlot = HeadSlot;
			return headSlot?.Parent.LocalPointToGlobal(headSlot.LocalPosition.x_z + LocalHeadFacingDirection * -0.1f) ?? base.Slot.GlobalPosition;
		}
		set
		{
			SetProxyPosition(FeetPosition, in value);
		}
	}

	public floatQ FeetRotation
	{
		get
		{
			TrackedDevicePositioner leftFootDevice = LeftFootDevice;
			TrackedDevicePositioner rightFootDevice = RightFootDevice;
			if (leftFootDevice?.BodyNodeRoot.Target != null && rightFootDevice?.BodyNodeRoot.Target != null && leftFootDevice.IsTracking.Value && rightFootDevice.IsTracking.Value)
			{
				float3 globalDirection = leftFootDevice.BodyNodeRoot.Target.Forward + rightFootDevice.BodyNodeRoot.Target.Forward;
				globalDirection = base.Slot.GlobalDirectionToLocal(in globalDirection).x_z.Normalized;
				if (MathX.Dot(LocalHeadFacingDirection, in globalDirection) < 0f)
				{
					globalDirection *= -1;
				}
				floatQ localRotation = floatQ.LookRotation(in globalDirection, float3.Up);
				return base.Slot.LocalRotationToGlobal(in localRotation);
			}
			return HeadFacingRotation;
		}
		set
		{
			SetProxyRotation(FeetRotation, in value);
		}
	}

	public float3 GetGlobalPosition(UserNode node)
	{
		return node switch
		{
			UserNode.None => float3.Zero, 
			UserNode.Root => base.Slot.GlobalPosition, 
			UserNode.GroundProjectedHead => GroundProjectedHeadPosition, 
			UserNode.Head => HeadSlot?.GlobalPosition ?? base.Slot.GlobalPosition, 
			UserNode.View => ViewPosition, 
			UserNode.Hips => HipsPosition, 
			UserNode.Feet => FeetPosition, 
			_ => throw new Exception("Invalid UserNode: " + node), 
		};
	}

	public floatQ GetGlobalRotation(UserNode node)
	{
		return node switch
		{
			UserNode.None => floatQ.Identity, 
			UserNode.Root => base.Slot.GlobalRotation, 
			UserNode.GroundProjectedHead => HeadFacingRotation, 
			UserNode.Head => HeadSlot?.GlobalRotation ?? base.Slot.GlobalRotation, 
			UserNode.View => ViewRotation, 
			UserNode.Hips => HipsRotation, 
			UserNode.Feet => FeetRotation, 
			_ => throw new Exception("Invalid UserNode: " + node), 
		};
	}

	public void SetGlobalPosition(UserNode node, in float3 position)
	{
		switch (node)
		{
		case UserNode.Root:
			base.Slot.GlobalPosition = position;
			break;
		case UserNode.GroundProjectedHead:
			GroundProjectedHeadPosition = position;
			break;
		case UserNode.Head:
			HeadPosition = position;
			break;
		case UserNode.View:
			ViewPosition = position;
			break;
		case UserNode.Hips:
			HipsPosition = position;
			break;
		case UserNode.Feet:
			FeetPosition = position;
			break;
		default:
			throw new Exception("Invalid UserNode: " + node);
		case UserNode.None:
			break;
		}
	}

	public void SetGlobalRotation(UserNode node, in floatQ rotation)
	{
		switch (node)
		{
		case UserNode.Root:
			base.Slot.GlobalRotation = rotation;
			break;
		case UserNode.GroundProjectedHead:
			HeadFacingRotation = rotation;
			break;
		case UserNode.Head:
			HeadRotation = rotation;
			break;
		case UserNode.View:
			ViewRotation = rotation;
			break;
		case UserNode.Hips:
			HipsRotation = rotation;
			break;
		case UserNode.Feet:
			FeetRotation = rotation;
			break;
		default:
			throw new Exception("Invalid UserNode: " + node);
		case UserNode.None:
			break;
		}
	}

	public void JumpToPoint(float3 targetPoint, float distance = 1.5f)
	{
		float3 v = (targetPoint - HeadPosition).Normalized;
		HeadPosition = targetPoint - v * 1.5f;
		HeadFacingDirection = v;
	}

	public Slot GetControllerSlot(Chirality node, bool throwOnInvalid = true)
	{
		switch (node)
		{
		case Chirality.Left:
			return LeftControllerSlot;
		case Chirality.Right:
			return RightControllerSlot;
		default:
			if (throwOnInvalid)
			{
				throw new ArgumentException("Invalid node: " + node);
			}
			return null;
		}
	}

	public Slot GetHandSlot(Chirality chirality, bool throwOnInvalid = true)
	{
		switch (chirality)
		{
		case Chirality.Left:
			return LeftHandSlot;
		case Chirality.Right:
			return RightHandSlot;
		default:
			if (throwOnInvalid)
			{
				throw new ArgumentException("Invalid chirality: " + chirality);
			}
			return null;
		}
	}

	private void SetProxyPosition(in float3 currentPosition, in float3 newPosition)
	{
		_ = base.Slot.GlobalPosition;
		float3 b = newPosition - currentPosition;
		Slot slot = base.Slot;
		slot.GlobalPosition += b;
	}

	private void SetProxyRotation(in floatQ currentRotation, in floatQ newRotation)
	{
		floatQ a = floatQ.FromToRotation(currentRotation, newRotation);
		base.Slot.GlobalRotation = a * base.Slot.GlobalRotation;
	}

	public void PositionReliably(Action positioner)
	{
		StartTask(async delegate
		{
			await RunPositionReliably(positioner);
		});
	}

	private async Task RunPositionReliably(Action positioner)
	{
		bool isFirst = true;
		int positionTimes = 2;
		do
		{
			if (!isFirst)
			{
				await default(NextUpdate);
			}
			else
			{
				isFirst = false;
			}
			if (!IsRemoved)
			{
				try
				{
					positioner();
				}
				catch (Exception)
				{
					UniLog.Error("Exception running reliable positioner:\n{ex}", stackTrace: false);
					break;
				}
				continue;
			}
			break;
		}
		while (!ReceivedFirstPositionalData || positionTimes-- > 0);
	}

	public float3 GetNeckOffset()
	{
		float3 result = DEFAULT_NECK_OFFSET;
		List<INeckOffsetSource> list = Pool.BorrowList<INeckOffsetSource>();
		GetRegisteredComponents(list);
		if (list.Count > 0)
		{
			int num = int.MinValue;
			foreach (INeckOffsetSource item in list)
			{
				if (item.NeckOffset.HasValue && item.NeckOffsetPriority > num)
				{
					num = item.NeckOffsetPriority;
					result = item.NeckOffset.Value;
				}
			}
		}
		Pool.Return(ref list);
		return result;
	}

	public bool IsAtScale(float scale)
	{
		scale = base.Slot.Parent.GlobalScaleToLocal(scale);
		return MathX.Approximately(MathX.AvgComponent(base.Slot.LocalScale), scale);
	}

	public Task SetUserScale(float scale, float time)
	{
		_scaleAnimCancel?.Cancel();
		_scaleAnimCancel = new CancellationTokenSource();
		return StartTask(async delegate
		{
			await SetUserScaleAnim(scale, time, _scaleAnimCancel.Token).ConfigureAwait(continueOnCapturedContext: false);
		});
	}

	private async ValueTask SetUserScaleAnim(float scale, float time, CancellationToken cancellationToken)
	{
		List<LocomotionPermissions> list = Pool.BorrowList<LocomotionPermissions>();
		base.World.Permissions.GetValidators(typeof(LocomotionController), list, ActiveUser);
		foreach (LocomotionPermissions item in list)
		{
			scale = item.ClampScale(scale, ActiveUser);
		}
		Pool.Return(ref list);
		float3 from = base.Slot.LocalScale;
		float3 to = base.Slot.Parent.GlobalScaleToLocal(scale) * float3.One;
		float3 feetPosition;
		if (time > 0f)
		{
			for (float f = 0f; f < 1f; f += base.Time.Delta / time)
			{
				feetPosition = FeetPosition;
				base.Slot.LocalScale = MathX.Lerp(in from, in to, f);
				FeetPosition = feetPosition;
				await default(NextUpdate);
				if (cancellationToken.IsCancellationRequested)
				{
					return;
				}
			}
		}
		feetPosition = FeetPosition;
		base.Slot.LocalScale = to;
		FeetPosition = feetPosition;
		_scaleAnimCancel = null;
	}

	protected override void OnAwake()
	{
		base.OnAwake();
		base.Slot.RegisterUserRoot(this);
		base.Slot.OnPrepareDestroy += Slot_OnPrepareDestroy;
	}

	protected override void OnStart()
	{
		base.OnStart();
		_localNameplate = base.Slot.AddLocalSlot("Local Name Badge");
		NameplateHelper.SetupDefaultNameBadge(_localNameplate, ActiveUser);
		NameplateHelper.SetupDefaultIconBadge(_localNameplate, ActiveUser);
		NameplateHelper.SetupDefaultLiveIndicator(_localNameplate, ActiveUser);
		_avatarManager = GetRegisteredComponent<AvatarManager>();
		UpdateLocalNameplate();
	}

	private void UpdateLocalNameplate()
	{
		if (_avatarManager != null && _localNameplate != null)
		{
			_localNameplate.ForeachComponentInChildren(delegate(AvatarNameTagAssigner a)
			{
				a.UpdateTags(_avatarManager);
			});
			_localNameplate.ForeachComponentInChildren(delegate(AvatarBadgeManager a)
			{
				a.UpdateBadges(_avatarManager);
			});
			_avatarManagerVersion = _avatarManager.UpdateVersion;
		}
	}

	private void Slot_OnPrepareDestroy(Slot slot)
	{
		UniLog.Log($"Destroying User: {ActiveUser}\nCurrently updating user: {base.World.UpdateManager.CurrentlyUpdatingUser}", stackTrace: true);
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		base.Slot.UnregisterUserRoot(this);
	}

	private void OnRenderSettingChanged(DesktopRenderSettings settings)
	{
		_renderSettings = settings;
	}

	private void UnregisterSettings()
	{
		if (_settingRegistered)
		{
			Settings.UnregisterComponentChanges<DesktopRenderSettings>(OnRenderSettingChanged);
			_settingRegistered = false;
		}
	}

	protected override void OnCommonUpdate()
	{
		if (_avatarManager == null || _avatarManager.IsRemoved)
		{
			_avatarManager = GetRegisteredComponent<AvatarManager>();
		}
		if (_avatarManager != null && _avatarManagerVersion != _avatarManager.UpdateVersion)
		{
			UpdateLocalNameplate();
		}
		if (base.LocalUser != ActiveUser)
		{
			UnregisterSettings();
			return;
		}
		if (!_settingRegistered)
		{
			Settings.RegisterComponentChanges<DesktopRenderSettings>(OnRenderSettingChanged);
			_settingRegistered = true;
		}
		float3 v = base.Slot.GlobalPosition;
		floatQ globalRotation = base.Slot.GlobalRotation;
		float3 v2 = base.Slot.GlobalScale;
		if (MathX.MinComponent(MathX.Abs(in v2)) <= 0f || v2.IsInfinity || v2.IsNaN)
		{
			UniLog.Warning("UserRoot Global Scale was invalid (zero, NaN or infinity), reseting user transform.\n" + base.Slot.ParentHierarchyToString());
			base.Slot.Parent = base.World.RootSlot;
			base.Slot.SetIdentityTransform();
			base.Slot.LocalScale = this.GetDefaultScale() * float3.One;
		}
		v2 = base.Slot.GlobalScale;
		if (v.IsInfinity || v.IsNaN || globalRotation.IsInfinity || globalRotation.IsNaN || v2.IsInfinity || v2.IsNaN || MathX.MaxComponent(MathX.Abs(in v)) > 100000f)
		{
			base.Slot.Destroy();
			return;
		}
		float3 localScale = base.Slot.LocalScale;
		if (MathX.Abs(localScale.x - localScale.y) + MathX.Abs(localScale.y - localScale.z) > 1E-05f)
		{
			base.Slot.LocalScale = MathX.AvgComponent(base.Slot.LocalScale) * float3.One;
		}
	}

	public T GetRegisteredComponent<T>(Predicate<T> filter = null) where T : class, IComponent
	{
		foreach (T item in GetComponentsOfType<T>())
		{
			if (!item.IsRemoved && (filter == null || filter(item)))
			{
				return item;
			}
		}
		return null;
	}

	public void GetRegisteredComponents<T>(List<T> list, Predicate<T> filter = null) where T : class, IComponent
	{
		foreach (T item in GetComponentsOfType<T>())
		{
			if (!item.IsRemoved && (filter == null || filter(item)))
			{
				list.Add(item);
			}
		}
	}

	public List<T> GetRegisteredComponents<T>(Predicate<T> filter = null) where T : class, IComponent
	{
		List<T> list = new List<T>();
		GetRegisteredComponents(list, filter);
		return list;
	}

	public void ForeachRegisteredComponent<T>(Action<T> action) where T : class, IComponent
	{
		foreach (T item in GetComponentsOfType<T>())
		{
			action(item);
		}
	}

	private List<T> GetComponentsOfType<T>() where T : class, IComponent
	{
		if (_perTypeComponents.TryGetValue(typeof(T), out IList value))
		{
			return (List<T>)value;
		}
		List<T> list = new List<T>();
		foreach (Component registeredComponent in _registeredComponents)
		{
			if (registeredComponent is T item)
			{
				list.Add(item);
			}
		}
		_perTypeComponents.Add(typeof(T), list);
		return list;
	}

	internal void RegisterComponent(Component component)
	{
		if (!_registeredComponents.Add(component))
		{
			throw new Exception("Component already registered: " + component);
		}
		Type type = component.GetType();
		foreach (KeyValuePair<Type, IList> perTypeComponent in _perTypeComponents)
		{
			if (perTypeComponent.Key.IsAssignableFrom(type))
			{
				perTypeComponent.Value.Add(component);
			}
		}
	}

	internal void UnregisterComponent(Component component)
	{
		if (_registeredComponents == null)
		{
			return;
		}
		if (!_registeredComponents.Remove(component))
		{
			throw new Exception("Component is not registered: " + component);
		}
		Type type = component.GetType();
		foreach (KeyValuePair<Type, IList> perTypeComponent in _perTypeComponents)
		{
			if (perTypeComponent.Key.IsAssignableFrom(type))
			{
				perTypeComponent.Value.Remove(component);
			}
		}
	}

	protected override void OnDispose()
	{
		base.Slot.OnPrepareDestroy -= Slot_OnPrepareDestroy;
		RootSpaceHint = null;
		_lastLink = null;
		_lastUser = null;
		_cachedHeadSlot = null;
		_cachedLeftHandSlot = null;
		_cachedRightHandSlot = null;
		_cachedLeftControllerSlot = null;
		_cachedRightControllerSlot = null;
		_hipsPositioner = null;
		_leftFootPositioner = null;
		_rightFootPositioner = null;
		_registeredComponents.Clear();
		_registeredComponents = null;
		foreach (KeyValuePair<Type, IList> perTypeComponent in _perTypeComponents)
		{
			perTypeComponent.Value.Clear();
		}
		_perTypeComponents.Clear();
		_perTypeComponents = null;
		base.OnDispose();
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		RenderSettings = new SyncRef<IRenderSettingsSource>();
		ScreenController = new SyncRef<ScreenController>();
		OverrideRoot = new SyncRef<Slot>();
		OverrideView = new SyncRef<Slot>();
		PrimaryListener = new SyncRef<AudioListener>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => RenderSettings, 
			4 => ScreenController, 
			5 => OverrideRoot, 
			6 => OverrideView, 
			7 => PrimaryListener, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static UserRoot __New()
	{
		return new UserRoot();
	}
}
