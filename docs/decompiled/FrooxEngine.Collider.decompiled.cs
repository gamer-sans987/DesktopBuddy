using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BepuPhysics;
using BepuPhysics.Collidables;
using Elements.Core;

namespace FrooxEngine;

[DefaultUpdateOrder(1000000)]
public abstract class Collider : Component, ICollider, IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable, ICollidable, IBounded
{
	public const float MIN_SIZE = 1E-06f;

	public const float MAX_SIZE = 1000000f;

	public const float MAX_POSITION = 100000000f;

	public const float DEFAULT_SPECULATIVE_MARGIN = 0.1f;

	public const int NO_COLLISION_INDEX = -1;

	public const int STATIC_INDEX = 0;

	public const int TRIGGER_INDEX = 1;

	public const int ACTIVE_INDEX = 2;

	public const int CHARACTER_INDEX = 3;

	public const int HAPTIC_TRIGGER_INDEX = 0;

	public const int HAPTIC_SAMPLER_INDEX = 1;

	public const byte NO_COLLISION_FLAG = 0;

	public const byte STATIC_FLAG = 1;

	public const byte TRIGGER_FLAG = 2;

	public const byte ACTIVE_FLAG = 4;

	public const byte CHARACTER_FLAG = 8;

	public const byte PACKED_CHARACTER_FLAG = 128;

	public const byte HAPTIC_TRIGGER_FLAG = 1;

	public const byte HAPTIC_SAMPLER_FLAG = 2;

	public readonly Sync<float3> Offset;

	public readonly Sync<ColliderType> Type;

	public readonly Sync<float> Mass;

	public readonly Sync<bool> CharacterCollider;

	public readonly Sync<bool> IgnoreRaycasts;

	protected TypedIndex _shapeIndex;

	protected int _bepuHandle = -1;

	protected ColliderType _registeredType = (ColliderType)(-1);

	protected IColliderOwner _owner;

	protected bool _scaleChanged;

	private static Func<float3, IField<float3>, float3> _filterOffset = FilterOffset;

	public override int Version => 1;

	protected bool DEBUG => false;

	public IColliderOwner ColliderOwner => _owner;

	public float3 LocalBoundsOffset
	{
		get
		{
			float3 offset = Offset.Value;
			_owner?.PostprocessBoundsOffset(ref offset);
			return offset;
		}
	}

	internal int BepuHandle => _bepuHandle;

	internal ColliderType RegisteredType => _registeredType;

	internal bool BepuEntityAllocated => _bepuHandle >= 0;

	protected virtual bool EntityShouldBeActive
	{
		get
		{
			if (!base.Enabled)
			{
				return false;
			}
			if (!base.Slot.IsActive)
			{
				return false;
			}
			if (_owner != null && !_owner.ShouldBeActive(this))
			{
				return false;
			}
			User activeUser = base.Slot.ActiveUser;
			if (activeUser != null && activeUser.IsCollisionLocallyBlocked)
			{
				return false;
			}
			return true;
		}
	}

	protected virtual bool ShapeChanged
	{
		get
		{
			if (!_scaleChanged && !Offset.WasChanged && !Mass.WasChanged)
			{
				return _owner?.ShapeChanged ?? false;
			}
			return true;
		}
	}

	protected virtual bool MetadataChanged
	{
		get
		{
			if (!CharacterCollider.WasChanged && !IgnoreRaycasts.WasChanged)
			{
				return _owner?.MetadataChanged ?? false;
			}
			return true;
		}
	}

	protected virtual bool EntityChanged => _owner?.EntityChanged ?? false;

	protected virtual bool ListenToEvents => true;

	protected PhysicsSimulation TargetSimulation => GetSimulation(Type.Value);

	ColliderType ICollider.ColliderType => Type.Value;

	bool ICollider.CharacterCollider => CharacterCollider.Value;

	bool ICollider.CollidesWithCharacters
	{
		get
		{
			if (!CharacterCollider.Value)
			{
				return false;
			}
			return Type.Value switch
			{
				ColliderType.Static => true, 
				ColliderType.Active => true, 
				ColliderType.CharacterController => true, 
				_ => false, 
			};
		}
	}

	bool ICollider.IgnoreRaycasts => IgnoreRaycasts.Value;

	public abstract bool HasBoundingBox { get; }

	public abstract bool IsBoundingBoxAvailable { get; }

	public abstract BoundingBox GlobalBoundingBox { get; }

	public abstract BoundingBox LocalBoundingBox { get; }

	public bool HasAnyEventListeners
	{
		get
		{
			if (this._contactStart == null && this._contactStay == null)
			{
				return this._contactEnd != null;
			}
			return true;
		}
	}

	private event ContactEvent _contactStart;

	private event ContactEvent _contactStay;

	private event ContactEvent _contactEnd;

	public event ContactEvent ContactStart
	{
		add
		{
			CheckRegisterListener();
			_contactStart += value;
		}
		remove
		{
			if (this._contactStart != null)
			{
				_contactStart -= value;
				CheckUnregisterListener();
			}
		}
	}

	public event ContactEvent ContactStay
	{
		add
		{
			CheckRegisterListener();
			_contactStay += value;
		}
		remove
		{
			if (this._contactStay != null)
			{
				_contactStay -= value;
				CheckUnregisterListener();
			}
		}
	}

	public event ContactEvent ContactEnd
	{
		add
		{
			CheckRegisterListener();
			_contactEnd += value;
		}
		remove
		{
			if (this._contactEnd != null)
			{
				_contactEnd -= value;
				CheckUnregisterListener();
			}
		}
	}

	protected abstract bool EnsureShapeReady();

	protected virtual void PostprocessContactMask(ref byte mask)
	{
	}

	protected virtual void PostprocessResponseMask(ref byte mask)
	{
	}

	protected override void OnChanges()
	{
		base.OnChanges();
		UpdateCollider();
	}

	private void Validate(RigidPose rigidPose)
	{
		if (!MathX.IsValid(rigidPose.Position))
		{
			throw new Exception($"Invalid position {rigidPose.Position} on {this}");
		}
		if (!MathX.IsValid(rigidPose.Orientation))
		{
			throw new Exception($"Invalid orientation {rigidPose.Orientation} on {this}");
		}
	}

	protected virtual void ProcessPose(ref RigidPose rigidPose)
	{
	}

	private void Slot_PhysicsWorldScaleChanged(Slot slot)
	{
		if (BepuEntityAllocated)
		{
			_scaleChanged = true;
			MarkChangeDirty();
		}
	}

	private void Slot_PhysicsWorldTransformChanged(Slot slot)
	{
		if (!BepuEntityAllocated)
		{
			return;
		}
		PhysicsSimulation simulation = GetSimulation(_registeredType);
		Simulation simulation2 = simulation.Simulation;
		RigidPose rigidPose = new RigidPose((Vector3)ClampPosition(base.Slot.GlobalPosition), (Quaternion)MathX.FilterInvalid(base.Slot.GlobalRotation));
		ProcessPose(ref rigidPose);
		if (IsStatic(_registeredType))
		{
			if (_registeredType == ColliderType.StaticTriggerAuto || _registeredType == ColliderType.HapticStaticTriggerAuto)
			{
				base.Physics.RegisterMovedStaticAuto(this);
			}
			simulation.MovedStatic();
			StaticHandle handle = new StaticHandle(_bepuHandle);
			AwakeningFilter filter = new AwakeningFilter(simulation, _registeredType);
			simulation2.Statics.GetDescription(handle, out var description);
			description.Pose = rigidPose;
			simulation2.Statics.ApplyDescription(handle, in description, ref filter);
		}
		else
		{
			simulation.MovedBody();
			BodyHandle bodyHandle = new BodyHandle(_bepuHandle);
			BodyReference bodyReference = simulation2.Bodies.GetBodyReference(bodyHandle);
			if (!bodyReference.Awake)
			{
				bodyReference.Awake = true;
			}
			bodyReference.Pose = rigidPose;
			simulation2.Bodies.UpdateBounds(bodyHandle);
		}
	}

	protected virtual void ClearShapeChanged()
	{
		_scaleChanged = false;
		Offset.WasChanged = false;
		Mass.WasChanged = false;
		_owner?.ClearShapeChanged();
	}

	protected virtual void ClearMetadataChanged()
	{
		CharacterCollider.WasChanged = false;
		IgnoreRaycasts.WasChanged = false;
		_owner?.ClearMetadataChanged();
	}

	public static bool IsStatic(ColliderType type)
	{
		return type switch
		{
			ColliderType.Static => true, 
			ColliderType.StaticTrigger => true, 
			ColliderType.StaticTriggerAuto => true, 
			ColliderType.NoCollision => true, 
			ColliderType.HapticStaticTrigger => true, 
			ColliderType.HapticStaticTriggerAuto => true, 
			_ => false, 
		};
	}

	public static bool IsDynamic(ColliderType type)
	{
		if (type == ColliderType.CharacterController)
		{
			return true;
		}
		return false;
	}

	public static bool IsTrigger(ColliderType type)
	{
		return type switch
		{
			ColliderType.Trigger => true, 
			ColliderType.StaticTrigger => true, 
			ColliderType.StaticTriggerAuto => true, 
			ColliderType.HapticTrigger => true, 
			ColliderType.HapticStaticTrigger => true, 
			ColliderType.HapticStaticTriggerAuto => true, 
			_ => false, 
		};
	}

	public static bool IsEventSource(ColliderType type)
	{
		return type switch
		{
			ColliderType.Active => true, 
			ColliderType.CharacterController => true, 
			ColliderType.HapticSampler => true, 
			_ => false, 
		};
	}

	public static bool CanTypeListenToEvents(ColliderType type)
	{
		if (type == ColliderType.NoCollision)
		{
			return false;
		}
		return true;
	}

	public static int TypeIndex(ColliderType type)
	{
		return type switch
		{
			ColliderType.NoCollision => -1, 
			ColliderType.Static => 0, 
			ColliderType.Trigger => 1, 
			ColliderType.StaticTrigger => 1, 
			ColliderType.StaticTriggerAuto => 1, 
			ColliderType.Active => 2, 
			ColliderType.CharacterController => 3, 
			ColliderType.HapticTrigger => 0, 
			ColliderType.HapticStaticTrigger => 0, 
			ColliderType.HapticStaticTriggerAuto => 0, 
			ColliderType.HapticSampler => 1, 
			_ => throw new ArgumentException("Invalid type: " + type), 
		};
	}

	private static byte CharacterColliderFlag(bool state)
	{
		if (!state)
		{
			return 0;
		}
		return 8;
	}

	public static byte ContactMask(ColliderType type, bool characterCollider)
	{
		return type switch
		{
			ColliderType.NoCollision => 0, 
			ColliderType.Static => (byte)(CharacterColliderFlag(characterCollider) | 4), 
			ColliderType.Trigger => 12, 
			ColliderType.StaticTrigger => 12, 
			ColliderType.StaticTriggerAuto => ContactMask(ColliderType.StaticTrigger, characterCollider), 
			ColliderType.Active => (byte)(6 | (characterCollider ? 8 : 0) | 1), 
			ColliderType.CharacterController => (byte)((characterCollider ? 8 : 0) | 4 | 2 | 1), 
			ColliderType.HapticTrigger => 2, 
			ColliderType.HapticStaticTrigger => 2, 
			ColliderType.HapticStaticTriggerAuto => ContactMask(ColliderType.HapticStaticTrigger, characterCollider), 
			ColliderType.HapticSampler => 1, 
			_ => throw new ArgumentException("Invalid type: " + type), 
		};
	}

	public static byte ResponseMask(ColliderType type, bool characterCollider)
	{
		return type switch
		{
			ColliderType.NoCollision => 0, 
			ColliderType.Static => (byte)(characterCollider ? 8 : 0), 
			ColliderType.Trigger => 0, 
			ColliderType.StaticTrigger => 0, 
			ColliderType.StaticTriggerAuto => ResponseMask(ColliderType.StaticTrigger, characterCollider), 
			ColliderType.Active => (byte)(characterCollider ? 8 : 0), 
			ColliderType.CharacterController => (byte)((characterCollider ? 8 : 0) | 4 | 1), 
			ColliderType.HapticTrigger => 0, 
			ColliderType.HapticStaticTrigger => 0, 
			ColliderType.HapticStaticTriggerAuto => ResponseMask(ColliderType.HapticStaticTrigger, characterCollider), 
			ColliderType.HapticSampler => 0, 
			_ => throw new ArgumentException("Invalid type: " + type), 
		};
	}

	public static byte TypeMask(ColliderType type)
	{
		int num = TypeIndex(type);
		if (num < 0)
		{
			return 0;
		}
		return (byte)(1 << num);
	}

	private bool CanListenToEvents(ColliderType type)
	{
		if (ListenToEvents)
		{
			return CanTypeListenToEvents(type);
		}
		return false;
	}

	protected SimulationType GetSimulationType(ColliderType type)
	{
		switch (type)
		{
		case ColliderType.NoCollision:
		case ColliderType.Static:
		case ColliderType.Trigger:
		case ColliderType.StaticTrigger:
		case ColliderType.StaticTriggerAuto:
		case ColliderType.Active:
		case ColliderType.CharacterController:
			return SimulationType.Main;
		case ColliderType.HapticTrigger:
		case ColliderType.HapticStaticTrigger:
		case ColliderType.HapticStaticTriggerAuto:
		case ColliderType.HapticSampler:
			return SimulationType.Haptic;
		default:
			throw new ArgumentException("Invalid collider type: " + type);
		}
	}

	protected PhysicsSimulation GetSimulation(ColliderType type)
	{
		switch (type)
		{
		case ColliderType.NoCollision:
		case ColliderType.Static:
		case ColliderType.Trigger:
		case ColliderType.StaticTrigger:
		case ColliderType.StaticTriggerAuto:
		case ColliderType.Active:
		case ColliderType.CharacterController:
			return base.Physics.MainSimulation;
		case ColliderType.HapticTrigger:
		case ColliderType.HapticStaticTrigger:
		case ColliderType.HapticStaticTriggerAuto:
		case ColliderType.HapticSampler:
			return base.Physics.HapticSimulation;
		default:
			throw new ArgumentException("Invalid collider type: " + type);
		}
	}

	public abstract Task<BoundingBox> ComputeExactBounds(Slot space);

	public abstract Task ForeachExactBoundedPoint(Slot space, Action<float3> action);

	private void CheckRegisterListener()
	{
		if (HasAnyEventListeners || !CanListenToEvents(_registeredType) || !BepuEntityAllocated)
		{
			return;
		}
		PhysicsSimulation simulation = GetSimulation(_registeredType);
		RegisterListener(simulation);
		if (!IsStatic(_registeredType))
		{
			BodyHandle handle = new BodyHandle(_bepuHandle);
			BodyReference bodyReference = simulation.Simulation.Bodies.GetBodyReference(handle);
			if (!bodyReference.Awake)
			{
				bodyReference.Awake = true;
			}
		}
	}

	private void CheckUnregisterListener()
	{
		if (!HasAnyEventListeners && CanListenToEvents(_registeredType) && BepuEntityAllocated)
		{
			UnregisterListener(GetSimulation(_registeredType));
		}
	}

	protected void RegisterListener(PhysicsSimulation simulation)
	{
		if (IsStatic(_registeredType))
		{
			simulation.ContactEventManager.RegisterListener(new StaticHandle(_bepuHandle));
		}
		else
		{
			simulation.ContactEventManager.RegisterListener(new BodyHandle(_bepuHandle));
		}
	}

	protected void UnregisterListener(PhysicsSimulation simulation)
	{
		if (IsStatic(_registeredType))
		{
			simulation.ContactEventManager.UnregisterListener(new StaticHandle(_bepuHandle));
		}
		else
		{
			simulation.ContactEventManager.UnregisterListener(new BodyHandle(_bepuHandle));
		}
	}

	protected abstract TypedIndex RegisterShape(PhysicsSimulation simulation, ref float speculativeMargin, float? mass, out BodyInertia inertia);

	protected abstract void ComputeInertia(PhysicsSimulation simulation, ref float3 offset, float mass, out BodyInertia inertia);

	protected abstract void UnregisterShape(PhysicsSimulation simulation);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float3 ClampPosition(in float3 position)
	{
		return MathX.Clamp(in position, -100000000f, 100000000f);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected void ProcessColliderOffset(ref float3 offset)
	{
		offset = base.Slot.GlobalRotation.Inverted * base.Slot.LocalVectorToGlobal(in offset);
		offset = MathX.FilterInvalid(offset);
		offset = MathX.Clamp(in offset, -1000000f, 1000000f);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected void ProcessColliderSize(ref float3 size)
	{
		ProcessColliderSize(ref size, 1E-06f, 1000000f);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected void ProcessColliderSize(ref float3 size, float minSize, float maxSize)
	{
		size *= base.Slot.GlobalScale;
		size = MathX.FilterInvalid(size);
		size = MathX.Abs(in size);
		size = MathX.Clamp(in size, minSize, maxSize);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected float3 ComputeColliderScale()
	{
		float3 value = base.Slot.GlobalRotation.Inverted * base.Slot.LocalVectorToGlobal(float3.One);
		value = MathX.FilterInvalid(value);
		return MathX.Clamp(MathX.Abs(in value), 1E-06f, 1000000f) * MathX.Sign(in value);
	}

	public float? ComputeActualMass(ColliderType type)
	{
		if (IsDynamic(type))
		{
			float num = Mass.Value;
			switch (_owner?.MassScaling)
			{
			case PhysicsScalingMode.Linear:
				num *= MathX.Abs(MathX.AvgComponent(base.Slot.GlobalScale));
				break;
			case PhysicsScalingMode.Cubic:
			{
				float num2 = MathX.AvgComponent(base.Slot.GlobalScale);
				num *= MathX.Abs(num2 * num2 * num2);
				break;
			}
			}
			return MathX.Clamp(MathX.FilterInvalid(num), 1E-06f, 1000000f);
		}
		return null;
	}

	private void TryRegisterEntity()
	{
		if (!EnsureShapeReady())
		{
			return;
		}
		if (Type.Value == ColliderType.CharacterController)
		{
			base.Slot.ForeachComponent(delegate(CharacterController c)
			{
				c.NotifyColliderAdded(this);
			}, cacheItems: false, exludeDisabled: true);
		}
		else
		{
			RegisterEntity(TargetSimulation, Type.Value, null);
		}
	}

	internal void RegisterEntity(PhysicsSimulation simulation, ColliderType type, IColliderOwner owner)
	{
		_owner = owner;
		float? mass = ComputeActualMass(type);
		float speculativeMargin = 0.1f;
		_shapeIndex = RegisterShape(simulation, ref speculativeMargin, mass, out var inertia);
		_owner?.PostprocessInertia(ref inertia);
		RegisterBody(simulation, type, speculativeMargin, mass, ref inertia);
		if (HasAnyEventListeners && CanListenToEvents(_registeredType))
		{
			RegisterListener(simulation);
		}
		if (IsEventSource(_registeredType))
		{
			simulation.ContactEventManager.RegisterEventSource(new BodyHandle(_bepuHandle));
		}
		if (_owner == null || _owner.ListenToTransformChanges)
		{
			base.Slot.PhysicsWorldTransformChanged += Slot_PhysicsWorldTransformChanged;
		}
		if (_owner == null || _owner.ListenToScaleChanges)
		{
			base.Slot.PhysicsWorldScaleChanged += Slot_PhysicsWorldScaleChanged;
		}
		_owner?.EntityRegistered(this);
		_owner?.ClearEntityChanged();
	}

	private void RegisterBody(PhysicsSimulation simulation, ColliderType targetType, float speculativeMargin, float? mass, ref BodyInertia inertia)
	{
		_registeredType = targetType;
		RigidPose rigidPose = new RigidPose((Vector3)ClampPosition(base.Slot.GlobalPosition), (Quaternion)MathX.FilterInvalid(base.Slot.GlobalRotation));
		ProcessPose(ref rigidPose);
		if (IsStatic(_registeredType))
		{
			AwakeningFilter filter = new AwakeningFilter(simulation, _registeredType);
			StaticDescription description = new StaticDescription(in rigidPose.Position, in rigidPose.Orientation, _shapeIndex, speculativeMargin);
			StaticHandle handle = simulation.Simulation.Statics.Add(in description, ref filter);
			simulation.RegisterCollider(handle, this);
			_bepuHandle = handle.Value;
		}
		else
		{
			BodyDescription description2;
			if (targetType == ColliderType.CharacterController)
			{
				CollidableDescription collidable = new CollidableDescription(_shapeIndex, 0.1f, ContinuousDetectionSettings.Passive);
				BodyActivityDescription activity = new BodyActivityDescription(0.02f, 32);
				description2 = ((!_owner.Kinematic) ? BodyDescription.CreateDynamic(in rigidPose, in inertia, in collidable, in activity) : BodyDescription.CreateKinematic(in rigidPose, in collidable, in activity));
			}
			else
			{
				description2 = BodyDescription.CreateKinematic(in rigidPose, new CollidableDescription(_shapeIndex, 0f), new BodyActivityDescription(0.1f, 2));
			}
			BodyHandle handle2 = simulation.Simulation.Bodies.Add(in description2);
			simulation.RegisterCollider(handle2, this);
			_bepuHandle = handle2.Value;
		}
		UpdateMetadata();
	}

	private void UpdateMetadata()
	{
		bool flag = IsStatic(_registeredType);
		int index = TypeIndex(_registeredType);
		byte mask = ContactMask(_registeredType, CharacterCollider);
		byte mask2 = ResponseMask(_registeredType, CharacterCollider);
		PostprocessContactMask(ref mask);
		PostprocessResponseMask(ref mask2);
		if (_owner != null)
		{
			_owner.PostprocessContactMask(ref mask);
			_owner.PostprocessResponseMask(ref mask2);
		}
		switch (GetSimulationType(_registeredType))
		{
		case SimulationType.Main:
		{
			MainPhysicsSimulation mainSimulation = base.Physics.MainSimulation;
			if (flag)
			{
				StaticHandle handle3 = new StaticHandle(_bepuHandle);
				mainSimulation.SetIsTrigger(handle3, IsTrigger(_registeredType));
				mainSimulation.SetIgnoreRaycasts(handle3, IgnoreRaycasts);
				mainSimulation.SetMasks(handle3, index, mask, mask2);
			}
			else
			{
				BodyHandle handle4 = new BodyHandle(_bepuHandle);
				mainSimulation.SetIsTrigger(handle4, IsTrigger(_registeredType));
				mainSimulation.SetIgnoreRaycasts(handle4, IgnoreRaycasts);
				mainSimulation.SetMasks(handle4, index, mask, mask2);
			}
			break;
		}
		case SimulationType.Haptic:
		{
			HapticsPhysicsSimulation hapticSimulation = base.Physics.HapticSimulation;
			if (flag)
			{
				StaticHandle handle = new StaticHandle(_bepuHandle);
				hapticSimulation.SetMasks(handle, index, mask, mask2);
			}
			else
			{
				BodyHandle handle2 = new BodyHandle(_bepuHandle);
				hapticSimulation.SetMasks(handle2, index, mask, mask2);
			}
			break;
		}
		}
		ClearMetadataChanged();
	}

	private void UpdateCollider()
	{
		base.World.ColliderUpdated();
		if (!EntityShouldBeActive)
		{
			if (BepuEntityAllocated)
			{
				Unregister();
			}
		}
		else if (BepuEntityAllocated)
		{
			if (_registeredType != Type.Value || EntityChanged)
			{
				PhysicsSimulation simulation = GetSimulation(_registeredType);
				Unregister(simulation);
				TryRegisterEntity();
				return;
			}
			if (ShapeChanged && EnsureShapeReady())
			{
				PhysicsSimulation simulation2 = GetSimulation(_registeredType);
				UnregisterShape(simulation2);
				float? mass = ComputeActualMass(_registeredType);
				float speculativeMargin = 0.1f;
				_shapeIndex = RegisterShape(simulation2, ref speculativeMargin, mass, out var inertia);
				if (IsStatic(_registeredType))
				{
					AwakeningFilter filter = new AwakeningFilter(simulation2, _registeredType);
					StaticHandle handle = new StaticHandle(_bepuHandle);
					simulation2.Simulation.Statics.GetDescription(handle, out var description);
					description.Collidable.Shape = _shapeIndex;
					description.Collidable.SpeculativeMargin = speculativeMargin;
					simulation2.Simulation.Statics.ApplyDescription(handle, in description, ref filter);
				}
				else
				{
					BodyHandle handle2 = new BodyHandle(_bepuHandle);
					simulation2.Simulation.Bodies.SetShape(handle2, _shapeIndex);
					if (mass.HasValue && !_owner.Kinematic)
					{
						_owner.PostprocessInertia(ref inertia);
						simulation2.Simulation.Bodies.GetBodyReference(handle2).LocalInertia = inertia;
					}
				}
				ClearShapeChanged();
			}
			if (MetadataChanged)
			{
				UpdateMetadata();
			}
		}
		else
		{
			TryRegisterEntity();
		}
	}

	internal void SetKinematic()
	{
		BodyHandle handle = new BodyHandle(_bepuHandle);
		GetSimulation(_registeredType).Simulation.Bodies.GetBodyReference(handle).BecomeKinematic();
	}

	internal void SetDynamic()
	{
		PhysicsSimulation simulation = GetSimulation(_registeredType);
		BodyHandle handle = new BodyHandle(_bepuHandle);
		BodyReference bodyReference = simulation.Simulation.Bodies.GetBodyReference(handle);
		float? num = ComputeActualMass(_registeredType);
		float3 a = Offset.Value;
		if (a != float3.Zero)
		{
			ProcessColliderOffset(ref a);
		}
		ComputeInertia(simulation, ref a, num.Value, out var inertia);
		_owner?.PostprocessInertia(ref inertia);
		bodyReference.SetLocalInertia(in inertia);
	}

	private void UnregisterOwner()
	{
		_owner?.EntityUnregistered(this);
		_owner = null;
	}

	private void UnregisterEntity(PhysicsSimulation simulation)
	{
		if (IsStatic(_registeredType))
		{
			StaticHandle handle = new StaticHandle(_bepuHandle);
			simulation.ContactEventManager.CollidableRemoved(handle);
			if (HasAnyEventListeners && CanListenToEvents(_registeredType))
			{
				simulation.ContactEventManager.UnregisterListener(handle);
			}
			UnregisterOwner();
			AwakeningFilter filter = new AwakeningFilter(simulation, _registeredType);
			simulation.Simulation.Statics.Remove(handle, ref filter);
			simulation.UnregisterCollider(handle);
		}
		else
		{
			BodyHandle bodyHandle = new BodyHandle(_bepuHandle);
			if (IsEventSource(_registeredType))
			{
				simulation.ContactEventManager.UnregisterEventSource(bodyHandle);
			}
			else
			{
				simulation.ContactEventManager.CollidableRemoved(bodyHandle);
			}
			if (HasAnyEventListeners && CanListenToEvents(_registeredType))
			{
				simulation.ContactEventManager.UnregisterListener(bodyHandle);
			}
			UnregisterOwner();
			simulation.Simulation.Bodies.Remove(bodyHandle);
			simulation.UnregisterCollider(bodyHandle);
		}
		_bepuHandle = -1;
	}

	internal bool TryUnregister()
	{
		if (_bepuHandle >= 0)
		{
			Unregister();
			return true;
		}
		return false;
	}

	internal void Unregister()
	{
		Unregister(GetSimulation(_registeredType));
	}

	private void Unregister(PhysicsSimulation simulation)
	{
		base.Slot.PhysicsWorldTransformChanged -= Slot_PhysicsWorldTransformChanged;
		base.Slot.PhysicsWorldScaleChanged -= Slot_PhysicsWorldScaleChanged;
		UnregisterEntity(simulation);
		UnregisterShape(simulation);
	}

	public virtual void SetCharacterCollider()
	{
		CharacterCollider.Value = true;
		SetStatic();
	}

	public void SetActive()
	{
		Type.Value = ColliderType.Active;
	}

	public void SetTrigger()
	{
		Type.Value = ColliderType.Trigger;
	}

	public void SetStatic()
	{
		Type.Value = ColliderType.Static;
	}

	public void SetNoCollision()
	{
		Type.Value = ColliderType.NoCollision;
	}

	private static float3 FilterOffset(float3 value, IField<float3> field)
	{
		if (value.IsNaN || value.IsInfinity)
		{
			UniLog.Warning("Setting invalid collider offset: " + value, stackTrace: true);
			return float3.Zero;
		}
		return value;
	}

	protected override void OnAwake()
	{
		base.OnAwake();
		Type.Value = ColliderType.Static;
		Mass.Value = 1f;
		Offset.LocalFilter = _filterOffset;
		base.Slot.ActiveUserRootChanged += Slot_ActiveUserRootChanged;
	}

	private void Slot_ActiveUserRootChanged(Slot slot)
	{
		MarkChangeDirty();
	}

	protected override void OnActivated()
	{
		base.OnActivated();
		MarkChangeDirty();
	}

	protected override void OnDeactivated()
	{
		base.OnDeactivated();
		MarkChangeDirty();
	}

	public void RunContactStart(ICollider other)
	{
		try
		{
			this._contactStart?.Invoke(this, other);
		}
		catch (Exception exception)
		{
			base.Debug.Error($"Exception in ContactStart\n{this}\n" + DebugManager.PreprocessException(exception));
		}
	}

	public void RunContactStay(ICollider other)
	{
		try
		{
			this._contactStay?.Invoke(this, other);
		}
		catch (Exception exception)
		{
			base.Debug.Error($"Exception in ContactStay\n{this}\n" + DebugManager.PreprocessException(exception));
		}
	}

	public void RunContactEnd(ICollider other)
	{
		try
		{
			this._contactEnd?.Invoke(this, other);
		}
		catch (Exception exception)
		{
			base.Debug.Error($"Exception in ContactEnd\n{this}\n" + DebugManager.PreprocessException(exception));
		}
	}

	protected override void OnDispose()
	{
		if (BepuEntityAllocated)
		{
			Unregister();
		}
		base.Slot.ActiveUserRootChanged -= Slot_ActiveUserRootChanged;
		base.OnDispose();
	}

	protected void UpgradeTrigger()
	{
		switch (Type.Value)
		{
		case ColliderType.Trigger:
			Type.Value = ColliderType.StaticTriggerAuto;
			break;
		case ColliderType.HapticTrigger:
			Type.Value = ColliderType.HapticStaticTriggerAuto;
			break;
		}
	}

	public void SwitchToKinematicTriggers()
	{
		switch (Type.Value)
		{
		case ColliderType.StaticTriggerAuto:
			Type.Value = ColliderType.Trigger;
			break;
		case ColliderType.HapticStaticTriggerAuto:
			Type.Value = ColliderType.HapticTrigger;
			break;
		}
	}

	protected override void OnLoading(DataTreeNode node, LoadControl control)
	{
		base.OnLoading(node, control);
		if (control.GetTypeVersion(GetType()) == 0)
		{
			control.OnLoaded(this, UpgradeTrigger);
		}
	}

	public override string ToString()
	{
		return $"{GetType().Name} - BepuAllocated: {BepuEntityAllocated}, BepuHandle: {BepuHandle}, RegisteredType: {_registeredType}, Type: {Type.Value}, Owner: {_owner}\n{base.ToString()}";
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		Offset = new Sync<float3>();
		Type = new Sync<ColliderType>();
		Mass = new Sync<float>();
		CharacterCollider = new Sync<bool>();
		IgnoreRaycasts = new Sync<bool>();
	}
}
