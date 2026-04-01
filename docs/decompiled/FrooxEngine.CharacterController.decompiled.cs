using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using BepuPhysics;
using BepuUtilities;
using Elements.Core;
using Elements.Data;

namespace FrooxEngine;

[Category(new string[] { "Physics" })]
[SingleInstancePerSlot]
[DefaultUpdateOrder(1000000)]
[InspectorHeader("Inspector.CharacterController.Warning", 300)]
public class CharacterController : Component, IColliderOwner, IBounded, IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable
{
	public const float MAX_SLOPE = 3.138451f;

	public readonly SyncRef<User> SimulatingUser;

	public readonly SyncRef<Slot> CharacterRoot;

	public readonly SyncRef<Slot> HeadReference;

	public readonly Sync<bool> SimulateRotation;

	public readonly Sync<PhysicsScalingMode> MassScaling;

	public readonly Sync<PhysicsScalingMode> ForceScaling;

	public readonly Sync<PhysicsScalingMode> SpeedScaling;

	public readonly Sync<PhysicsScalingMode> JumpScaling;

	public readonly Sync<PhysicsScalingMode> GravityScaling;

	[Range(0f, 1f, "0.0000")]
	[OldName("Damping")]
	public readonly Sync<float> LinearDamping;

	[Range(0f, 1f, "0.0000")]
	public readonly Sync<float> AngularDamping;

	public readonly Sync<float> Margin;

	public readonly Sync<float> StepUpHeight;

	public readonly Sync<float> StepUpCheckDistance;

	public readonly Sync<bool> KillVerticalVelocityAfterStepUp;

	public readonly Sync<float> EdgeDetectionDepth;

	public readonly Sync<float> Speed;

	public readonly Sync<float> SlidingSpeed;

	public readonly Sync<float> AirSpeed;

	public readonly Sync<float> TractionForce;

	public readonly Sync<float> SlidingForce;

	public readonly Sync<float> AirForce;

	public readonly Sync<float> MaximumGlueForce;

	public readonly Sync<float> MaximumTractionSlope;

	public readonly Sync<float> MaximumSupportSlope;

	public readonly Sync<float> JumpSpeed;

	public readonly Sync<float> SlidingJumpSpeed;

	public readonly Sync<float3> Gravity;

	public readonly RootSpace GravitySpace;

	[NonPersistent]
	public readonly Sync<float?> DebugVisualDuration;

	public float3? OverrideGravity;

	[OldName("Height")]
	[NonPersistent]
	protected readonly Sync<float> __height;

	[OldName("Radius")]
	[NonPersistent]
	protected readonly Sync<float> __radius;

	[OldName("Mass")]
	[NonPersistent]
	protected readonly Sync<float> __mass;

	[OldName("CollideWithOtherCharacters")]
	[NonPersistent]
	protected readonly Sync<bool> __collideWithOtherCharacters;

	[OldName("IgnoreRaycasts")]
	[NonPersistent]
	protected readonly Sync<bool> __ignoreRaycasts;

	[OldName("RootAtBottom")]
	[NonPersistent]
	protected readonly Sync<bool> __rootAtBottom;

	private Digital _jump = new Digital();

	private float3 _lastReferencePos;

	private floatQ _lastReferenceRot;

	private float3? _bufferedVelocity;

	private BodyHandle _bodyHandle;

	private bool _kinematic;

	private bool _unglue;

	private bool _wasSteppingUp;

	public override int Version => 1;

	public bool HasBoundingBox => true;

	public bool IsBoundingBoxAvailable => true;

	public Elements.Core.BoundingBox GlobalBoundingBox => LocalBoundingBox.Transform(base.Slot.LocalToGlobal);

	public Elements.Core.BoundingBox LocalBoundingBox
	{
		get
		{
			if (RegisteredCollider != null)
			{
				return RegisteredCollider.LocalBoundingBox;
			}
			float3 offset = float3.Zero;
			PostprocessBoundsOffset(ref offset);
			return Elements.Core.BoundingBox.CenterSize(in offset, float3.Zero);
		}
	}

	public bool Simulate => SimulatingUser.Target == base.LocalUser;

	public Slot CharacterSpace => CharacterRoot.Target.Parent;

	public Slot ActualCharacterRoot => CharacterRoot.Target ?? base.Slot;

	public bool ShapeChanged => SimulateRotation.WasChanged;

	public bool MetadataChanged => false;

	public bool EntityChanged
	{
		get
		{
			if (!SimulatingUser.WasChanged)
			{
				return RegisteredCollider?.CharacterCollider.WasChanged ?? false;
			}
			return true;
		}
	}

	public bool ListenToTransformChanges => false;

	public bool ListenToScaleChanges => true;

	public bool IsReady => RegisteredCollider != null;

	public float3 ActualGravity { get; private set; }

	public float ActualLinearDamping { get; private set; }

	public float ActualAngularDamping { get; private set; }

	public float ActualMass => MathX.Max(1E-10f, (RegisteredCollider?.ComputeActualMass(RegisteredCollider.Type)).GetValueOrDefault());

	public Collider RegisteredCollider { get; private set; }

	PhysicsScalingMode IColliderOwner.MassScaling => MassScaling.Value;

	bool IColliderOwner.Kinematic
	{
		get
		{
			if (!_kinematic)
			{
				return !Simulate;
			}
			return true;
		}
	}

	public float3 MoveDirection { get; set; }

	public bool Jump { get; set; }

	public bool ForceKinematic
	{
		get
		{
			return _kinematic;
		}
		set
		{
			if (_kinematic == value)
			{
				return;
			}
			_kinematic = value;
			if (RegisteredCollider != null)
			{
				if (_kinematic)
				{
					RegisteredCollider.SetKinematic();
					BodyReference currentBodyReference = GetCurrentBodyReference();
					currentBodyReference.Velocity.Linear = default(Vector3);
					currentBodyReference.Velocity.Angular = default(Vector3);
				}
				else
				{
					RegisteredCollider.SetDynamic();
				}
			}
		}
	}

	public ICollider CurrentGround
	{
		get
		{
			if (RegisteredCollider == null)
			{
				return null;
			}
			ref CharacterControllerData characterByBodyHandle = ref base.Physics.MainSimulation.CharacterControllerManager.GetCharacterByBodyHandle(_bodyHandle);
			if (characterByBodyHandle.Traction)
			{
				return base.Physics.MainSimulation.GetCollider(characterByBodyHandle.Support);
			}
			return null;
		}
	}

	public float3 LinearVelocity
	{
		get
		{
			if (RegisteredCollider == null)
			{
				return _bufferedVelocity ?? float3.Zero;
			}
			return new BodyReference(_bodyHandle, base.Physics.MainSimulation.Simulation.Bodies).Velocity.Linear;
		}
		set
		{
			if (RegisteredCollider == null)
			{
				if (value.IsValid())
				{
					_bufferedVelocity = value;
				}
				return;
			}
			BodyReference bodyReference = new BodyReference(_bodyHandle, base.Physics.MainSimulation.Simulation.Bodies);
			value = MathX.FilterInvalid(value);
			float num = MathX.MaxComponent(MathX.Abs((float3)(bodyReference.Velocity.Linear - (Vector3)value)));
			bodyReference.Velocity.Linear = value;
			if (num / base.Time.Delta > 0.1f)
			{
				_unglue = true;
			}
			if (value.SqrMagnitude > 1E-06f && !bodyReference.Awake)
			{
				bodyReference.Awake = true;
			}
		}
	}

	public bool ShouldBeActive(Collider collider)
	{
		if (base.Slot.IsActive && base.Enabled)
		{
			if (!Simulate)
			{
				if (collider.CharacterCollider.Value)
				{
					return SimulatingUser.Target != null;
				}
				return false;
			}
			return true;
		}
		return false;
	}

	public Task<Elements.Core.BoundingBox> ComputeExactBounds(Slot space)
	{
		if (RegisteredCollider != null)
		{
			return RegisteredCollider.ComputeExactBounds(space);
		}
		return Task.FromResult(Elements.Core.BoundingBox.Empty());
	}

	public Task ForeachExactBoundedPoint(Slot space, Action<float3> point)
	{
		if (RegisteredCollider != null)
		{
			return RegisteredCollider.ForeachExactBoundedPoint(space, point);
		}
		return Task.CompletedTask;
	}

	public void ClearShapeChanged()
	{
		SimulateRotation.WasChanged = false;
	}

	public void ClearMetadataChanged()
	{
	}

	public void ClearEntityChanged()
	{
		SimulatingUser.WasChanged = false;
	}

	protected override void OnAwake()
	{
		Margin.Value = 0.05f;
		StepUpHeight.Value = 0.5f;
		StepUpCheckDistance.Value = 0.25f;
		EdgeDetectionDepth.Value = 0.25f;
		Gravity.Value = float3.Down * 9.81f;
		Speed.Value = 4f;
		SlidingSpeed.Value = 3f;
		AirSpeed.Value = 1f;
		TractionForce.Value = 1000f;
		SlidingForce.Value = 50f;
		AirForce.Value = 250f;
		MaximumGlueForce.Value = 5000f;
		MaximumTractionSlope.Value = 45f;
		MaximumSupportSlope.Value = 75f;
		JumpSpeed.Value = 6f;
		SlidingJumpSpeed.Value = 3f;
		MassScaling.Value = PhysicsScalingMode.Cubic;
		ForceScaling.Value = PhysicsScalingMode.Cubic;
		SpeedScaling.Value = PhysicsScalingMode.Linear;
		JumpScaling.Value = PhysicsScalingMode.Linear;
		GravityScaling.Value = PhysicsScalingMode.Linear;
	}

	protected override void OnAttach()
	{
		base.OnAttach();
		if (base.Slot.GetComponent((ICollider c) => c.ColliderType == ColliderType.CharacterController) == null)
		{
			SetupDefaultCollider(out CapsuleCollider _, out SingleShapeCharacterControllerManager _);
		}
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();
		RescanColliders();
	}

	protected override void OnStart()
	{
		base.OnStart();
		RescanColliders();
	}

	private void SetupDefaultCollider(out CapsuleCollider capsule, out SingleShapeCharacterControllerManager manager)
	{
		capsule = base.Slot.AttachComponent<CapsuleCollider>();
		capsule.Type.Value = ColliderType.CharacterController;
		capsule.Mass.Value = 10f;
		capsule.IgnoreRaycasts.Value = true;
		manager = base.Slot.AttachComponent<SingleShapeCharacterControllerManager>();
		manager.TargetHeight.Target = capsule.Height;
		manager.TargetWidth.Target = capsule.Radius;
		manager.TargetOffset.Target = capsule.Offset;
	}

	protected override void OnChanges()
	{
		base.OnChanges();
		if (RegisteredCollider != null)
		{
			if (ShapeChanged || MetadataChanged || EntityChanged)
			{
				RegisteredCollider.MarkChangeDirty();
			}
		}
		else
		{
			RescanColliders();
		}
	}

	private void Physics_PreContactDispatch()
	{
		if (Simulate && IsReady && ShouldBeActive(RegisteredCollider) && !ForceKinematic)
		{
			BodyReference currentBodyReference = GetCurrentBodyReference();
			ref CharacterControllerData currentCharacter = ref GetCurrentCharacter();
			if (KillVerticalVelocityAfterStepUp.Value && !currentCharacter.SteppingUp && _wasSteppingUp)
			{
				float3 globalVector = currentBodyReference.Velocity.Linear;
				globalVector = base.Slot.GlobalVectorToLocal(in globalVector);
				float num = MathX.Abs(globalVector.y);
				globalVector = globalVector.x_z;
				globalVector += globalVector.Normalized * num;
				globalVector += new float3(0f, 0f - globalVector.y);
				globalVector = base.Slot.LocalVectorToGlobal(in globalVector);
				currentBodyReference.Velocity.Linear = globalVector;
			}
			_wasSteppingUp = currentCharacter.SteppingUp;
			Slot actualCharacterRoot = ActualCharacterRoot;
			if (HeadReference.Target == null)
			{
				actualCharacterRoot.GlobalPosition = currentBodyReference.Pose.Position;
				actualCharacterRoot.GlobalRotation = currentBodyReference.Pose.Orientation;
			}
			else
			{
				actualCharacterRoot.GlobalRotation = (Quaternion)actualCharacterRoot.GlobalRotation * ((Quaternion)_lastReferenceRot.Inverted * currentBodyReference.Pose.Orientation);
				actualCharacterRoot.GlobalPosition = (Vector3)actualCharacterRoot.GlobalPosition + (currentBodyReference.Pose.Position - (Vector3)_lastReferencePos);
			}
			base.World.Physics.RegisterPhysicsMoved(actualCharacterRoot);
		}
	}

	protected override void OnDispose()
	{
		if (RegisteredCollider != null)
		{
			RegisteredCollider.Unregister();
		}
		base.OnDispose();
	}

	private void GetCurrentScale(out float scale, out float cubicScale)
	{
		scale = MathX.AvgComponent(ActualCharacterRoot.GlobalScale);
		cubicScale = scale * scale * scale;
	}

	private void Physics_PreUpdate()
	{
		_jump.UpdateState(Jump);
		if (!base.Enabled && RegisteredCollider != null)
		{
			RegisteredCollider.Unregister();
		}
		else
		{
			if (!IsReady || !ShouldBeActive(RegisteredCollider))
			{
				return;
			}
			Slot actualCharacterRoot = ActualCharacterRoot;
			BodyReference currentBodyReference = GetCurrentBodyReference();
			bool flag = false;
			float3 v;
			floatQ value;
			if (HeadReference.Target == null)
			{
				v = MathX.FilterInvalid(actualCharacterRoot.GlobalPosition);
				value = MathX.FilterInvalid(actualCharacterRoot.GlobalRotation);
			}
			else
			{
				Slot target = HeadReference.Target;
				v = float3.Zero;
				value = floatQ.Identity;
				if (target != null)
				{
					v = actualCharacterRoot.SpacePointToLocal(target.LocalPosition, target.Parent).x_z;
					value = floatQ.LookRotation(actualCharacterRoot.SpaceDirectionToLocal(float3.Forward, HeadReference.Target).x_z.Normalized, float3.Up);
				}
				v = actualCharacterRoot.LocalPointToGlobal(in v);
				value = actualCharacterRoot.LocalRotationToGlobal(in value);
				v = MathX.FilterInvalid(v);
				value = MathX.FilterInvalid(in value);
				_lastReferencePos = v;
				_lastReferenceRot = value;
			}
			if (Simulate)
			{
				GetCurrentScale(out var scale, out var cubicScale);
				float3 v2;
				if (OverrideGravity.HasValue)
				{
					v2 = OverrideGravity.Value;
					OverrideGravity = null;
				}
				else
				{
					v2 = GravitySpace.Space.LocalVectorToGlobal((float3)Gravity);
				}
				v2 *= GetScale(GravityScaling, scale, cubicScale);
				if (FrooxEngine.Engine.IsAprilFools)
				{
					MysterySettings? activeSetting = Settings.GetActiveSetting<MysterySettings>();
					if (activeSetting != null && activeSetting.Difficulty.Value == MysterySettings.ResoniteDifficulty.Hard)
					{
						v2 *= 2;
					}
				}
				ActualGravity = v2;
				ActualLinearDamping = MathX.Clamp01(MathX.FilterInvalid(LinearDamping));
				ActualAngularDamping = MathX.Clamp01(MathX.FilterInvalid(AngularDamping));
				ref CharacterControllerData currentCharacter = ref GetCurrentCharacter();
				UpdateCharacterProperties(ref currentCharacter, scale, cubicScale, _unglue);
				_unglue = false;
				float num = (currentCharacter.Traction ? Speed.Value : ((!currentCharacter.Supported) ? AirSpeed.Value : SlidingSpeed.Value));
				num *= GetScale(SpeedScaling, scale, cubicScale);
				v = Collider.ClampPosition(MathX.FilterInvalid(v));
				value = MathX.FilterInvalid(in value);
				_ = value * ILSpyHelper_AsRefReadOnly(float3.Up);
				float3 v3 = value * ILSpyHelper_AsRefReadOnly(float3.Forward);
				currentCharacter.LocalUp = float3.Up;
				currentCharacter.ViewDirection = v3;
				if (_bufferedVelocity.HasValue)
				{
					if (!_kinematic)
					{
						currentBodyReference.Velocity.Linear = _bufferedVelocity.Value;
						_unglue = true;
					}
					_bufferedVelocity = null;
				}
				if (!_kinematic)
				{
					currentCharacter.TargetDirection = MathX.FilterInvalid(MoveDirection.xz * new float2(-1f, 1f));
					currentCharacter.TargetSpeed = num;
				}
				if (_kinematic)
				{
					currentCharacter.TargetDirection = default(Vector2);
					currentCharacter.TargetSpeed = 0f;
					currentCharacter.TryJump = false;
				}
				else if (currentCharacter.Traction)
				{
					currentCharacter.TryJump = _jump.Pressed;
				}
				else
				{
					currentCharacter.TryJump = _jump.Pressed && currentCharacter.Supported;
					float3 v4 = MoveDirection;
					if (v4.SqrMagnitude > 0.001f)
					{
						v4 = value * v4;
						float num2 = MathX.Dot((float3)currentBodyReference.Velocity.Linear, in v4);
						float num3 = currentBodyReference.LocalInertia.InverseMass * currentCharacter.MaximumHorizontalForce * base.Time.Delta;
						float num4 = MathX.Min(num2 + num3, num * v4.Magnitude);
						float num5 = MathX.Max(0f, num4 - num2);
						currentBodyReference.Velocity.Linear += (Vector3)(v4 * num5);
						flag = true;
					}
				}
				if (!SimulateRotation.Value)
				{
					currentBodyReference.Velocity.Angular = default(Vector3);
				}
			}
			else
			{
				currentBodyReference.Velocity.Linear = default(Vector3);
				currentBodyReference.Velocity.Angular = default(Vector3);
			}
			if (!currentBodyReference.Awake && (flag || (v - (float3)currentBodyReference.Pose.Position).SqrMagnitude > 0.01f || MathX.Dot(in value, (floatQ)currentBodyReference.Pose.Orientation) > 0.01f))
			{
				currentBodyReference.Awake = true;
			}
			currentBodyReference.Pose = new RigidPose((Vector3)v, (Quaternion)value);
			base.Physics.MainSimulation.Simulation.Bodies.UpdateBounds(_bodyHandle);
			OverrideGravity = null;
		}
		static ref readonly T ILSpyHelper_AsRefReadOnly<T>(in T temp)
		{
			//ILSpy generated this function to help ensure overload resolution can pick the overload using 'in'
			return ref temp;
		}
	}

	private static float GetScale(PhysicsScalingMode mode, float scale, float cubicScale)
	{
		return mode switch
		{
			PhysicsScalingMode.None => 1f, 
			PhysicsScalingMode.Linear => scale, 
			PhysicsScalingMode.Cubic => cubicScale, 
			_ => 0f, 
		};
	}

	public void NotifyColliderAdded(Collider collider)
	{
		RescanColliders();
	}

	private void RescanColliders()
	{
		if (!base.Slot.IsActive || !base.Enabled)
		{
			return;
		}
		List<Collider> list = Pool.BorrowList<Collider>();
		base.Slot.GetComponents(list, (Collider c) => c.Enabled && c.Type.Value == ColliderType.CharacterController);
		if (list.Count == 0)
		{
			return;
		}
		if (list.Count > 1)
		{
			list.Sort((Collider a, Collider b) => a.ReferenceID.CompareTo(b.ReferenceID));
		}
		Collider collider = list[0];
		if (collider != RegisteredCollider && ShouldBeActive(collider))
		{
			if (RegisteredCollider != null)
			{
				RegisteredCollider.Unregister();
			}
			if (collider.BepuEntityAllocated)
			{
				collider.Unregister();
			}
			collider.RegisterEntity(base.Physics.MainSimulation, ColliderType.CharacterController, this);
		}
	}

	internal BodyReference GetCurrentBodyReference()
	{
		return base.Physics.MainSimulation.Simulation.Bodies.GetBodyReference(_bodyHandle);
	}

	internal ref CharacterControllerData GetCurrentCharacter()
	{
		return ref base.Physics.MainSimulation.CharacterControllerManager.GetCharacterByBodyHandle(_bodyHandle);
	}

	public void EntityRegistered(Collider collider)
	{
		RegisteredCollider = collider;
		_bodyHandle = new BodyHandle(collider.BepuHandle);
		base.Physics.MainSimulation.RegisterCharacterController(_bodyHandle, this);
		base.Physics.PreUpdate += Physics_PreUpdate;
		base.Physics.MainSimulation.PreContactDispatch += Physics_PreContactDispatch;
		GetCurrentScale(out var scale, out var cubicScale);
		UpdateCharacterProperties(ref base.Physics.MainSimulation.CharacterControllerManager.AllocateCharacter(_bodyHandle), scale, cubicScale, unglue: false);
	}

	private void UnregisterCollider()
	{
		base.Physics.PreUpdate -= Physics_PreUpdate;
		base.Physics.MainSimulation.PreContactDispatch -= Physics_PreContactDispatch;
		base.Physics.MainSimulation.CharacterControllerManager.RemoveCharacterByBodyHandle(_bodyHandle);
		base.Physics.MainSimulation.UnregisterCharacterController(_bodyHandle, this);
		RegisteredCollider = null;
		_bodyHandle = default(BodyHandle);
	}

	public void EntityUnregistered(Collider collider)
	{
		if (collider == RegisteredCollider)
		{
			UnregisterCollider();
		}
	}

	private void UpdateCharacterProperties(ref CharacterControllerData data, float scale, float cubedScale, bool unglue)
	{
		data.LocalUp = float3.Up;
		data.DebugVisualDuration = DebugVisualDuration.Value ?? (-1f);
		data.MinimumSupportDepth = -0.05f * scale;
		data.MinimumSupportContinuationDepth = -0.1f * scale;
		data.StepUpHeight = MathX.FilterInvalid(StepUpHeight.Value * scale);
		data.StepUpCheckDistance = MathX.FilterInvalid(StepUpCheckDistance.Value * scale);
		data.EdgeDetectionDepth = MathX.FilterInvalid(EdgeDetectionDepth.Value * scale);
		data.CosMaximumSupportSlope = MathX.Cos(MathX.Clamp(MaximumSupportSlope.Value * (MathF.PI / 180f), 0f, 3.138451f));
		data.CosMaximumTractionSlope = MathX.Cos(MathX.Clamp(MaximumTractionSlope.Value * (MathF.PI / 180f), 0f, 3.138451f));
		float scale2 = GetScale(ForceScaling, scale, cubedScale);
		float scale3 = GetScale(JumpScaling, scale, cubedScale);
		data.MaximumVerticalForce = MaximumGlueForce.Value * scale2 * (unglue ? 0.01f : 1f);
		if (data.Traction)
		{
			data.JumpVelocity = scale3 * JumpSpeed.Value;
			data.MaximumHorizontalForce = scale2 * TractionForce.Value;
		}
		else if (data.Supported)
		{
			data.JumpVelocity = scale3 * SlidingJumpSpeed.Value;
			data.MaximumHorizontalForce = scale2 * SlidingForce.Value;
		}
		else
		{
			data.JumpVelocity = 0f;
			data.MaximumHorizontalForce = scale2 * AirForce.Value;
		}
	}

	public void PostprocessInertia(ref BodyInertia inertia)
	{
		if (ForceKinematic)
		{
			inertia = default(BodyInertia);
		}
		else if (!SimulateRotation.Value)
		{
			inertia.InverseInertiaTensor = default(Symmetric3x3);
		}
	}

	public void PostprocessContactMask(ref byte mask)
	{
		if (!Simulate)
		{
			mask = (byte)(mask & -2);
		}
	}

	public void PostprocessResponseMask(ref byte mask)
	{
		if (!Simulate)
		{
			mask = (byte)(mask & -2);
		}
	}

	public void PostprocessBoundsOffset(ref float3 offset)
	{
		if (HeadReference.Target != null)
		{
			offset += ActualCharacterRoot.SpacePointToLocal(HeadReference.Target.LocalPosition, HeadReference.Target.Parent).x_z;
		}
	}

	protected override void OnLoading(DataTreeNode node, LoadControl control)
	{
		base.OnLoading(node, control);
		if (control.GetTypeVersion(GetType()) != 0)
		{
			return;
		}
		control.OnLoaded(this, delegate
		{
			SetupDefaultCollider(out CapsuleCollider capsule, out SingleShapeCharacterControllerManager manager);
			control.TransferField(__ignoreRaycasts, capsule.IgnoreRaycasts);
			control.TransferField(__collideWithOtherCharacters, capsule.CharacterCollider);
			control.TransferField(__mass, capsule.Mass);
			control.TransferField(__height, manager.DefaultHeight);
			control.TransferField(__radius, manager.DefaultWidth);
			control.TransferField(__rootAtBottom, manager.RootAtBottom);
			if (manager.DefaultWidth.Value == 0.2f)
			{
				manager.DefaultWidth.Value = 0.275f;
			}
			if (base.Slot.GetComponent<ILocomotionModule>() == null)
			{
				manager.UseUserHeadHeightWhenAvailable.Value = false;
			}
		});
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		SimulatingUser = new SyncRef<User>();
		CharacterRoot = new SyncRef<Slot>();
		HeadReference = new SyncRef<Slot>();
		SimulateRotation = new Sync<bool>();
		MassScaling = new Sync<PhysicsScalingMode>();
		ForceScaling = new Sync<PhysicsScalingMode>();
		SpeedScaling = new Sync<PhysicsScalingMode>();
		JumpScaling = new Sync<PhysicsScalingMode>();
		GravityScaling = new Sync<PhysicsScalingMode>();
		LinearDamping = new Sync<float>();
		AngularDamping = new Sync<float>();
		Margin = new Sync<float>();
		StepUpHeight = new Sync<float>();
		StepUpCheckDistance = new Sync<float>();
		KillVerticalVelocityAfterStepUp = new Sync<bool>();
		EdgeDetectionDepth = new Sync<float>();
		Speed = new Sync<float>();
		SlidingSpeed = new Sync<float>();
		AirSpeed = new Sync<float>();
		TractionForce = new Sync<float>();
		SlidingForce = new Sync<float>();
		AirForce = new Sync<float>();
		MaximumGlueForce = new Sync<float>();
		MaximumTractionSlope = new Sync<float>();
		MaximumSupportSlope = new Sync<float>();
		JumpSpeed = new Sync<float>();
		SlidingJumpSpeed = new Sync<float>();
		Gravity = new Sync<float3>();
		GravitySpace = new RootSpace();
		DebugVisualDuration = new Sync<float?>();
		DebugVisualDuration.MarkNonPersistent();
		__height = new Sync<float>();
		__height.MarkNonPersistent();
		__radius = new Sync<float>();
		__radius.MarkNonPersistent();
		__mass = new Sync<float>();
		__mass.MarkNonPersistent();
		__collideWithOtherCharacters = new Sync<bool>();
		__collideWithOtherCharacters.MarkNonPersistent();
		__ignoreRaycasts = new Sync<bool>();
		__ignoreRaycasts.MarkNonPersistent();
		__rootAtBottom = new Sync<bool>();
		__rootAtBottom.MarkNonPersistent();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => SimulatingUser, 
			4 => CharacterRoot, 
			5 => HeadReference, 
			6 => SimulateRotation, 
			7 => MassScaling, 
			8 => ForceScaling, 
			9 => SpeedScaling, 
			10 => JumpScaling, 
			11 => GravityScaling, 
			12 => LinearDamping, 
			13 => AngularDamping, 
			14 => Margin, 
			15 => StepUpHeight, 
			16 => StepUpCheckDistance, 
			17 => KillVerticalVelocityAfterStepUp, 
			18 => EdgeDetectionDepth, 
			19 => Speed, 
			20 => SlidingSpeed, 
			21 => AirSpeed, 
			22 => TractionForce, 
			23 => SlidingForce, 
			24 => AirForce, 
			25 => MaximumGlueForce, 
			26 => MaximumTractionSlope, 
			27 => MaximumSupportSlope, 
			28 => JumpSpeed, 
			29 => SlidingJumpSpeed, 
			30 => Gravity, 
			31 => GravitySpace, 
			32 => DebugVisualDuration, 
			33 => __height, 
			34 => __radius, 
			35 => __mass, 
			36 => __collideWithOtherCharacters, 
			37 => __ignoreRaycasts, 
			38 => __rootAtBottom, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static CharacterController __New()
	{
		return new CharacterController();
	}
}
