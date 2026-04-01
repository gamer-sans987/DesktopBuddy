using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Elements.Core;
using FrooxEngine.UIX;
using Renderite.Shared;

namespace FrooxEngine;

[Category(new string[] { "Physics/Dynamic Bones" })]
public class DynamicBoneChain : Component, ICustomInspector, IWorker, IWorldElement, IGrabbable, IComponent, IComponentBase, IDestroyable, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable, IInteractionTarget, IDestroyBlock, IInteractionBlock, IDuplicateBlock
{
	public delegate void CollisionHandler(BoneData[] bones, int boneIndex, IList colliderData);

	public delegate void CollisionEventHandler(int boneIndex);

	public class Bone : SyncObject, ICustomInspector, IWorker, IWorldElement
	{
		public readonly SyncRef<Slot> BoneSlot;

		public readonly Sync<float3> OrigPosition;

		public readonly Sync<floatQ> OrigRotation;

		[Range(0f, 1f, "0.00")]
		public readonly Sync<float> RadiusModifier;

		public readonly SyncRef<Bone> GrabOverride;

		public readonly Sync<bool> Collide;

		public readonly FieldDrive<float3> _posDrive;

		public readonly FieldDrive<floatQ> _rotDrive;

		private Slot _lastTarget;

		public bool IsValid
		{
			get
			{
				if (BoneSlot.Target != null && _posDrive.IsLinkValid)
				{
					return _rotDrive.IsLinkValid;
				}
				return false;
			}
		}

		public void Assign(Slot target)
		{
			BoneSlot.Target = target;
			OrigPosition.Value = target.LocalPosition;
			OrigRotation.Value = target.LocalRotation;
			_posDrive.ForceLink(target.Position_Field);
			_rotDrive.ForceLink(target.Rotation_Field);
		}

		protected override void OnAwake()
		{
			base.OnAwake();
			RadiusModifier.Value = 1f;
			Collide.Value = true;
			RadiusModifier.OnValueChange += Radius_OnValueChange;
			BoneSlot.OnTargetChange += Target_OnTargetChange;
		}

		private void Radius_OnValueChange(SyncField<float> syncField)
		{
			InvalidateData();
		}

		protected override void OnDispose()
		{
			Unregister();
			base.OnDispose();
		}

		private void Target_OnTargetChange(SyncRef<Slot> reference)
		{
			Unregister();
			_lastTarget = reference.Target;
			if (_lastTarget != null)
			{
				_lastTarget.ParentChanged += ParentChanged;
			}
			InvalidateData();
		}

		private void Unregister()
		{
			if (_lastTarget != null)
			{
				_lastTarget.ParentChanged -= ParentChanged;
				_lastTarget = null;
			}
		}

		private void ParentChanged(Slot slot)
		{
			InvalidateData();
		}

		private void InvalidateData()
		{
			this.FindNearestParent<DynamicBoneChain>().InvalidateData();
		}

		public void BuildInspectorUI(UIBuilder ui)
		{
			WorkerInspector.BuildInspectorUI(this, ui);
			ui.Button((LocaleString)"Setup from slot", OnSetupFromSlot);
		}

		[SyncMethod(typeof(Delegate), null)]
		private void OnSetupFromSlot(IButton button, ButtonEventData eventData)
		{
			if (BoneSlot.Target != null)
			{
				Assign(BoneSlot.Target);
			}
		}

		protected override void InitializeSyncMembers()
		{
			base.InitializeSyncMembers();
			BoneSlot = new SyncRef<Slot>();
			OrigPosition = new Sync<float3>();
			OrigRotation = new Sync<floatQ>();
			RadiusModifier = new Sync<float>();
			GrabOverride = new SyncRef<Bone>();
			Collide = new Sync<bool>();
			_posDrive = new FieldDrive<float3>();
			_rotDrive = new FieldDrive<floatQ>();
		}

		public override ISyncMember GetSyncMember(int index)
		{
			return index switch
			{
				0 => BoneSlot, 
				1 => OrigPosition, 
				2 => OrigRotation, 
				3 => RadiusModifier, 
				4 => GrabOverride, 
				5 => Collide, 
				6 => _posDrive, 
				7 => _rotDrive, 
				_ => throw new ArgumentOutOfRangeException(), 
			};
		}

		public static Bone __New()
		{
			return new Bone();
		}
	}

	public struct BoneData
	{
		public int parentIndex;

		public int hierarchyDepth;

		public bool isIK;

		public int rotationRoot;

		public float3 pos;

		public floatQ rot;

		public float3 prevPos;

		public float3 vel;

		public float length;

		public float radius;

		public float stretch;

		public float radiusModifier;

		public float3 restPos;

		public floatQ restRot;

		public float3 dir;

		public floatQ rotOffset;

		public Bone bone;

		public bool collide;

		public ushort childCount;
	}

	private struct SphereCollisionData
	{
		public float3 point;

		public float radius;

		public DynamicBoneCollisionDataType type;
	}

	internal int ManagerIndex = -1;

	[Range(0f, 1f, "0.00")]
	public readonly Sync<float> Inertia;

	[Range(-10f, 10f, "0.00")]
	public readonly Sync<float> InertiaForce;

	[Range(0f, 100f, "0.00")]
	public readonly Sync<float> Damping;

	[Range(0f, 1000f, "0.00")]
	public readonly Sync<float> Elasticity;

	[Range(0f, 1f, "0.0000")]
	public readonly Sync<float> Stiffness;

	public readonly Sync<bool> SimulateTerminalBones;

	public readonly Sync<float> BaseBoneRadius;

	public readonly Sync<bool> DynamicPlayerCollision;

	public readonly Sync<bool> CollideWithOwnBody;

	public readonly Sync<VibratePreset> HandCollisionVibration;

	public readonly Sync<bool> CollideWithHead;

	public readonly Sync<bool> CollideWithBody;

	public readonly Sync<bool> CollideWithLeftHand;

	public readonly Sync<bool> CollideWithRightHand;

	public readonly Sync<float3> Gravity;

	public readonly RootSpace GravitySpace;

	public readonly Sync<bool> UseUserGravityDirection;

	public readonly Sync<float3> LocalForce;

	[Range(0.1f, 2f, "0.00")]
	public readonly Sync<float> GlobalStretch;

	[Range(1f, 2f, "0.00")]
	public readonly Sync<float> MaxStretchRatio;

	public readonly RawOutput<float> CurrentStretchRatio;

	public readonly Sync<float> StretchRestoreSpeed;

	public readonly Sync<bool> UseLocalUserSpace;

	public readonly RootSpace SimulationSpace;

	public readonly SyncRefList<IDynamicBoneCollider> StaticColliders;

	[NonPersistent]
	public readonly Sync<bool> VisualizeColliders;

	[NonPersistent]
	public readonly Sync<bool> VisualizeBones;

	public readonly Sync<bool> IsGrabbable;

	public readonly Sync<bool> ActiveUserRootOnly;

	public readonly Sync<bool> AllowSteal;

	public readonly Sync<int> GrabPriority;

	public readonly Sync<bool> IgnoreGrabOnFirstBone;

	[Range(1f, 4f, "0.00")]
	public readonly Sync<float> GrabRadiusTolerance;

	public readonly Sync<float> GrabReleaseDistance;

	public readonly Sync<bool> GrabSlipping;

	public readonly Sync<bool> GrabTerminalBones;

	public readonly Sync<VibratePreset> GrabVibration;

	public readonly Sync<bool> IgnoreOwnLeftHand;

	public readonly Sync<bool> IgnoreOwnRightHand;

	public readonly SyncRef<Slot> EffectorTarget;

	public readonly Sync<int> EffectorBoneIndex;

	public readonly Sync<float3> EffectorBoneOffset;

	protected readonly SyncRef<Grabber> _activeGrabber;

	public readonly SyncList<Bone> Bones;

	internal BoundingBox GlobalBounds;

	private BoneData[] _data;

	private int _terminalBoneStartIndex;

	private bool _dataInvalid = true;

	private float4x4 _globalToSpace;

	private float _globalScaleToSpace;

	private RawValueList<SphereCollisionData> _collisions;

	private float _originalRootScale;

	private Slot _space;

	private LocomotionController _locomotion;

	private float _stretchRatio;

	private bool _dynamicGrabbable;

	private int _lastEffectorBoneIndex;

	private float _grabbedChainLength;

	private byte _collisionMask;

	private int lastLeftCollision;

	private int lastRightCollision;

	public virtual int InteractionTargetPriority => 0;

	public Slot ActualSimulationSpace
	{
		get
		{
			if (UseLocalUserSpace.Value)
			{
				Slot slot = base.Slot.ActiveUserRoot?.Slot.Parent;
				if (slot != null)
				{
					return slot;
				}
			}
			return SimulationSpace.Space;
		}
	}

	public bool IsGrabbed => Grabber != null;

	bool IGrabbable.Scalable => false;

	public bool Receivable => false;

	public bool AllowOnlyPhysicalGrab => true;

	int IGrabbable.GrabPriority => GrabPriority;

	public Grabber Grabber => _activeGrabber.Target;

	public event Action<IGrabbable> OnLocalGrabbed;

	public event Action<IGrabbable> OnLocalReleased;

	public InteractionDescription GetInteractionDescription(InteractionLaser laser)
	{
		return laser.GetGrabInteractionDescription(IsGrabbed);
	}

	public bool CanGrab(Grabber grabber)
	{
		if (!IsGrabbable.Value)
		{
			return false;
		}
		if (IsGrabbed && !AllowSteal.Value)
		{
			return false;
		}
		if (ActiveUserRootOnly.Value && base.Slot.ActiveUserRoot != null && base.LocalUser != base.Slot.ActiveUser)
		{
			return false;
		}
		return true;
	}

	private void InvalidateData()
	{
		_dataInvalid = true;
	}

	protected override void OnAwake()
	{
		base.OnAwake();
		Inertia.Value = 0.2f;
		Damping.Value = 5f;
		Elasticity.Value = 100f;
		Stiffness.Value = 0.2f;
		InertiaForce.Value = 2f;
		BaseBoneRadius.Value = 0.025f;
		UseLocalUserSpace.Value = true;
		GrabRadiusTolerance.Value = 1.25f;
		GrabReleaseDistance.Value = 1f;
		AllowSteal.Value = true;
		SimulateTerminalBones.Value = true;
		DynamicPlayerCollision.Value = true;
		CollideWithHead.Value = true;
		CollideWithBody.Value = true;
		CollideWithLeftHand.Value = true;
		CollideWithRightHand.Value = true;
		Bones.Changed += Bones_Changed;
		SimulateTerminalBones.Changed += SimulateTerminalBones_Changed;
		GlobalStretch.Value = 1f;
		MaxStretchRatio.Value = 1f;
		StretchRestoreSpeed.Value = 6f;
		HandCollisionVibration.Value = VibratePreset.None;
		UseUserGravityDirection.Value = true;
	}

	private void SimulateTerminalBones_Changed(IChangeable obj)
	{
		InvalidateData();
	}

	private void Bones_Changed(IChangeable obj)
	{
		InvalidateData();
	}

	internal void Prepare()
	{
		EnsureValidData();
		_collisionMask = 0;
		if (_data == null)
		{
			return;
		}
		_locomotion = base.Slot.ActiveUserRoot?.GetRegisteredComponent<LocomotionController>();
		if (DynamicPlayerCollision.Value)
		{
			_collisionMask |= 16;
			if (CollideWithHead.Value)
			{
				_collisionMask |= 1;
			}
			if (CollideWithBody.Value)
			{
				_collisionMask |= 8;
			}
			if (CollideWithLeftHand.Value)
			{
				_collisionMask |= 2;
			}
			if (CollideWithRightHand.Value)
			{
				_collisionMask |= 4;
			}
		}
	}

	internal void RunSimulation()
	{
		if (_data != null)
		{
			_globalToSpace = _space.GlobalToLocal;
			_globalScaleToSpace = _space.GlobalScaleToLocal(1f);
			EnsureValidEffectorHierarchy();
			Simulate();
		}
	}

	internal void FinishSimulation()
	{
		if (_dynamicGrabbable)
		{
			this.RegisterDynamicGrabbable();
		}
		CurrentStretchRatio.Value = _stretchRatio;
		for (int i = 0; i < _terminalBoneStartIndex; i++)
		{
			Bone bone = _data[i].bone;
			if (bone.IsValid)
			{
				Slot parent = bone.BoneSlot.Target.Parent;
				bone._posDrive.Target.Value = _space.LocalPointToSpace(in _data[i].pos, parent);
				bone._rotDrive.Target.Value = _space.LocalRotationToSpace(in _data[i].rot, parent);
			}
		}
		if (_collisions != null)
		{
			if (VisualizeColliders.Value && base.World.IsAuthority)
			{
				for (int j = 0; j < _collisions.Count; j++)
				{
					ref SphereCollisionData reference = ref _collisions.Elements[j];
					float3 point = _space.LocalPointToGlobal(in reference.point);
					float radius = _space.LocalScaleToGlobal(reference.radius);
					base.Debug.Sphere(in point, radius, colorX.Green.SetA(0.73f));
				}
			}
			Pool.Return(ref _collisions);
		}
		if ((bool)VisualizeBones && base.World.IsAuthority)
		{
			for (int k = 0; k < _data.Length; k++)
			{
				float3 point2 = _space.LocalPointToGlobal(in _data[k].pos);
				base.Debug.Sphere(in point2, _space.LocalScaleToGlobal(_data[k].radius), _data[k].isIK ? colorX.Magenta : ((_data[k].childCount == 0) ? colorX.Blue : colorX.Cyan));
				if (_data[k].parentIndex >= 0)
				{
					base.Debug.Line(in point2, _space.LocalPointToGlobal(in _data[_data[k].parentIndex].pos), colorX.Yellow);
				}
			}
		}
		HandleGrabbing();
	}

	internal void ScheduleCollision(byte mask, in float3 point, float radius, DynamicBoneCollisionDataType type, User user)
	{
		if (_data != null && (mask & _collisionMask) == mask && (user != base.Slot.ActiveUser || ((CollideWithOwnBody.Value || (mask & 9) == 0) && (!IgnoreOwnLeftHand.Value || (mask & 2) == 0) && (!IgnoreOwnRightHand.Value || (mask & 4) == 0))))
		{
			if (_collisions == null)
			{
				_collisions = Pool.BorrowRawValueList<SphereCollisionData>();
			}
			_collisions.Add(new SphereCollisionData
			{
				point = point,
				radius = radius,
				type = type
			});
		}
	}

	internal bool ApplySphereCollision(ref BoneData bone, in float3 point, float radius)
	{
		float3 @float = bone.pos - point;
		float num = radius + bone.radius;
		if (MathX.Abs(@float.x) > num || MathX.Abs(@float.y) > num || MathX.Abs(@float.z) > num)
		{
			return false;
		}
		float magnitude;
		float3 v = @float.GetNormalized(out magnitude);
		float num2 = num - magnitude;
		if (num2 > 0f)
		{
			ref float3 pos = ref bone.pos;
			pos += v * num2;
			return true;
		}
		return false;
	}

	private void HandleGrabbing()
	{
		Grabber grabber = Grabber;
		if (grabber == null || !grabber.IsUnderLocalUser || _lastEffectorBoneIndex < 0)
		{
			return;
		}
		Slot actualSimulationSpace = ActualSimulationSpace;
		float3 b = actualSimulationSpace.SpacePointToLocal((float3)EffectorBoneOffset, EffectorTarget);
		float num = MathX.Distance(in _data[_lastEffectorBoneIndex].pos, in b);
		if (GrabSlipping.Value)
		{
			int num2 = MathX.Min(_lastEffectorBoneIndex + 1, _data.Length - 1);
			int num3 = MathX.Max(_lastEffectorBoneIndex - 1, 1);
			float num4 = MathX.Distance(in _data[num2].pos, in b);
			float num5 = MathX.Distance(in _data[num3].pos, in b);
			if (num4 < num)
			{
				EffectorBoneIndex.Value = num2;
				num = num4;
			}
			if (num5 < num)
			{
				EffectorBoneIndex.Value = num3;
				num = num5;
			}
		}
		if (num > actualSimulationSpace.SpaceScaleToLocal(GrabReleaseDistance, _data[0].bone.BoneSlot.Target))
		{
			Release(Grabber);
		}
	}

	private void EnsureValidData()
	{
		Slot actualSimulationSpace = ActualSimulationSpace;
		if (actualSimulationSpace != _space)
		{
			_dataInvalid = true;
			_space = actualSimulationSpace;
		}
		if (!_dataInvalid || MathX.Approximately(MathX.MinComponent(MathX.Abs(base.Slot.GlobalScale)), 0f))
		{
			return;
		}
		_lastEffectorBoneIndex = -1;
		int num = Bones.Count((Bone bone3) => bone3.IsValid);
		if (num == 0)
		{
			_data = null;
			_dataInvalid = false;
			return;
		}
		_data = new BoneData[num];
		int num2 = 0;
		foreach (Bone bone3 in Bones)
		{
			if (bone3.IsValid)
			{
				bone3.BoneSlot.Target.LocalPosition = bone3.OrigPosition.Value;
				bone3.BoneSlot.Target.LocalRotation = bone3.OrigRotation.Value;
				_data[num2].bone = bone3;
				_data[num2].hierarchyDepth = bone3.BoneSlot.Target.HierachyDepth;
				_data[num2].childCount = 0;
				num2++;
			}
		}
		Array.Sort(_data, (BoneData boneData, BoneData boneData2) => boneData.hierarchyDepth.CompareTo(boneData2.hierarchyDepth));
		_data[0].parentIndex = -1;
		for (int num3 = 1; num3 < _data.Length; num3++)
		{
			_data[num3].parentIndex = 0;
			for (int num4 = num3 - 1; num4 >= 0; num4--)
			{
				if (_data[num3].bone.BoneSlot.Target.IsChildOf(_data[num4].bone.BoneSlot.Target))
				{
					_data[num4].childCount++;
					_data[num3].parentIndex = num4;
					break;
				}
			}
		}
		_terminalBoneStartIndex = _data.Length;
		if (SimulateTerminalBones.Value)
		{
			int num5 = 0;
			for (int num6 = 0; num6 < _data.Length; num6++)
			{
				if (_data[num6].childCount == 0)
				{
					num5++;
				}
			}
			_data = _data.EnsureExactSize(_data.Length + num5, keepData: true);
			int num7 = _terminalBoneStartIndex;
			for (int num8 = 0; num8 < _terminalBoneStartIndex; num8++)
			{
				if (_data[num8].childCount == 0)
				{
					_data[num8].childCount = 1;
					_data[num7].childCount = 0;
					_data[num7].parentIndex = num8;
					num7++;
				}
			}
		}
		Bone bone = _data[0].bone;
		Slot target = bone.BoneSlot.Target;
		floatQ q = actualSimulationSpace.SpaceRotationToLocal(bone.OrigRotation.Value, target.Parent);
		_originalRootScale = MathX.AvgComponent(actualSimulationSpace.SpaceScaleToLocal(target.LocalScale, target.Parent));
		for (int num9 = 0; num9 < _data.Length; num9++)
		{
			Bone bone2 = _data[num9].bone;
			bool collide = true;
			floatQ b;
			float radiusModifier;
			float3 a;
			if (bone2 != null)
			{
				a = actualSimulationSpace.SpacePointToLocal(bone2.OrigPosition.Value, bone2.BoneSlot.Target.Parent);
				b = actualSimulationSpace.SpaceRotationToLocal(bone2.OrigRotation.Value, bone2.BoneSlot.Target.Parent);
				radiusModifier = bone2.RadiusModifier.Value;
				collide = bone2.Collide.Value;
			}
			else
			{
				int parentIndex = _data[num9].parentIndex;
				int parentIndex2 = _data[parentIndex].parentIndex;
				radiusModifier = _data[parentIndex].radiusModifier;
				float3 b2;
				if (parentIndex2 >= 0)
				{
					b2 = _data[parentIndex].pos - _data[parentIndex2].pos;
				}
				else
				{
					Slot slot = _data[parentIndex].bone.BoneSlot.Target;
					while (MathX.Approximately(slot.LocalPosition.Magnitude, 0f) && !slot.IsRootSlot)
					{
						slot = slot.Parent;
					}
					b2 = ((!slot.IsRootSlot) ? actualSimulationSpace.SpaceDirectionToLocal(slot.LocalPosition, slot.Parent) : actualSimulationSpace.GlobalVectorToLocal(_data[parentIndex].bone.BoneSlot.Target.Forward));
				}
				a = _data[parentIndex].pos + b2;
				b = _data[parentIndex].rot;
			}
			_data[num9].pos = a;
			_data[num9].rot = b;
			_data[num9].prevPos = a;
			_data[num9].radiusModifier = radiusModifier;
			_data[num9].collide = collide;
			int parentIndex3 = _data[num9].parentIndex;
			if (parentIndex3 >= 0)
			{
				a -= _data[parentIndex3].pos;
				_data[num9].dir = floatQ.InvertedMultiply(in _data[parentIndex3].rot, a.Normalized);
				a = floatQ.InvertedMultiply(in q, in a);
				b = floatQ.InvertedMultiply(in _data[parentIndex3].rot, in b);
			}
			_data[num9].restPos = a;
			_data[num9].restRot = b;
			_data[num9].length = a.Magnitude;
		}
		_dataInvalid = false;
	}

	private void EnsureValidEffectorHierarchy()
	{
		int num = EffectorBoneIndex.Value;
		if (EffectorTarget.Target == null)
		{
			num = -1;
		}
		if (num >= _data.Length)
		{
			num = -1;
		}
		if (num == _lastEffectorBoneIndex)
		{
			return;
		}
		for (int i = 0; i < _data.Length; i++)
		{
			_data[i].isIK = false;
			_data[i].rotationRoot = 0;
		}
		if (num >= 0)
		{
			_grabbedChainLength = 0f;
			MarkBoneAsIK(num);
			for (int j = 1; j < _data.Length; j++)
			{
				if (_data[j].isIK)
				{
					continue;
				}
				int parentIndex = _data[j].parentIndex;
				if (parentIndex >= 0)
				{
					if (_data[parentIndex].isIK)
					{
						_data[j].rotationRoot = parentIndex;
					}
					else
					{
						_data[j].rotationRoot = _data[parentIndex].rotationRoot;
					}
				}
			}
		}
		_lastEffectorBoneIndex = num;
	}

	private void MarkBoneAsIK(int index)
	{
		_data[index].isIK = true;
		if (index > 0)
		{
			_grabbedChainLength += _data[index].length;
		}
		if (_data[index].parentIndex >= 0)
		{
			MarkBoneAsIK(_data[index].parentIndex);
		}
	}

	private void Simulate()
	{
		float value = Inertia.Value;
		float value2 = Damping.Value;
		_ = Elasticity.Value;
		float num = 1f - Stiffness.Value;
		float value3 = InertiaForce.Value;
		Bone bone = _data[0].bone;
		Slot target = bone.BoneSlot.Target;
		_data[0].prevPos = _data[0].pos;
		_data[0].pos = target.Parent.LocalPointToSpace(bone.OrigPosition.Value, _space);
		float3 a = target.Parent.LocalVectorToSpace((float3)LocalForce, _space);
		if (UseUserGravityDirection.Value && base.Slot.ActiveUserRoot != null)
		{
			CharacterController characterController = (_locomotion?.ActiveModule as PhysicalLocomotion)?.CharacterController;
			float3 to = characterController?.GravitySpace.Space.LocalVectorToGlobal(characterController.Gravity.Value) ?? base.Slot.ActiveUserRoot.Slot.Parent.LocalDirectionToGlobal(float3.Down);
			a += floatQ.FromToRotation(float3.Down, in to) * Gravity.Value;
		}
		else
		{
			a += GravitySpace.Space.LocalVectorToSpace((float3)Gravity, _space);
		}
		float3 v = _data[0].pos - _data[0].prevPos;
		float3 b = v * value;
		_data[0].rot = target.Parent.LocalRotationToSpace(bone.OrigRotation.Value, _space);
		float num2 = MathX.AvgComponent(target.Parent.LocalScaleToSpace(target.LocalScale, _space));
		float num3 = num2 / _originalRootScale;
		num3 *= GlobalStretch.Value;
		if (FrooxEngine.Engine.IsAprilFools)
		{
			MysterySettings? activeSetting = Settings.GetActiveSetting<MysterySettings>();
			if (activeSetting != null && activeSetting.Loooong.Value)
			{
				num3 *= 1f + MathX.Sin(base.Time.WorldTimeFloat) + 1f;
			}
		}
		float num4 = BaseBoneRadius.Value * num2;
		float smoothDelta = base.Time.SmoothDelta;
		float invertedSmoothDelta = base.Time.InvertedSmoothDelta;
		for (int i = 1; i < _data.Length; i++)
		{
			_data[i].prevPos = _data[i].pos + b;
			ref float3 pos = ref _data[i].pos;
			pos += b;
			_data[i].radius = num4 * _data[i].radiusModifier;
		}
		RawValueList<int> list = null;
		RawValueList<int> list2 = null;
		_dynamicGrabbable = false;
		if (_collisions != null)
		{
			if (CanGrab(null))
			{
				_dynamicGrabbable = true;
			}
			list = Pool.BorrowRawValueList<int>();
			list2 = Pool.BorrowRawValueList<int>();
			for (int j = 0; j < _collisions.Count; j++)
			{
				ref SphereCollisionData reference = ref _collisions.Elements[j];
				reference.point = _globalToSpace.TransformPoint3x4(in reference.point);
				reference.radius = _globalScaleToSpace * reference.radius;
			}
		}
		if (StaticColliders.Count > 0)
		{
			if (_collisions == null)
			{
				_collisions = Pool.BorrowRawValueList<SphereCollisionData>();
			}
			foreach (IDynamicBoneCollider staticCollider in StaticColliders)
			{
				if (staticCollider is DynamicBoneSphereCollider dynamicBoneSphereCollider)
				{
					Slot slot = dynamicBoneSphereCollider.Slot;
					RawValueList<SphereCollisionData> collisions = _collisions;
					SphereCollisionData item = new SphereCollisionData
					{
						point = slot.Parent.LocalPointToSpace(slot.LocalPosition, _space),
						radius = slot.LocalScaleToSpace(dynamicBoneSphereCollider.Radius, _space)
					};
					collisions.Add(in item);
				}
			}
		}
		for (int k = 1; k < _data.Length; k++)
		{
			int parentIndex = _data[k].parentIndex;
			float num5 = num3 + _data[k].stretch;
			floatQ b2 = _data[0].rot;
			if (_data[k].rotationRoot > 0)
			{
				b2 = _data[_data[k].rotationRoot].rotOffset * b2;
			}
			float3 b3 = b2 * _data[k].restPos * num5;
			float3 a2 = _data[parentIndex].pos + b3;
			if (_data[k].isIK)
			{
				float3 to2 = _data[k].pos - _data[parentIndex].pos;
				_data[k].rotOffset = floatQ.FromToRotation(in b3, in to2);
			}
			float3 a3 = (a2 - _data[k].pos) * Elasticity.Value;
			float3 b4 = MathX.ClampMagnitude(-_data[k].vel * value2, _data[k].vel.Magnitude * invertedSmoothDelta);
			float num6 = _data[k].length * num5;
			a3 = a3 + b4 + v * invertedSmoothDelta * value3 + a;
			ref float3 vel = ref _data[k].vel;
			vel += a3 * smoothDelta;
			ref float3 pos2 = ref _data[k].pos;
			pos2 += _data[k].vel * smoothDelta;
			if (!_data[k].isIK)
			{
				if (num < 1f)
				{
					float magnitude;
					float3 v2 = (a2 - _data[k].pos).GetNormalized(out magnitude);
					float num7 = num6 * num * 2f;
					if (magnitude > num7)
					{
						ref float3 pos3 = ref _data[k].pos;
						pos3 += v2 * (magnitude - num7);
					}
				}
				if (_data[k].collide && _collisions != null)
				{
					for (int l = 0; l < _collisions.Count; l++)
					{
						ref SphereCollisionData reference2 = ref _collisions.Elements[l];
						if (ApplySphereCollision(ref _data[k], in reference2.point, reference2.radius) && reference2.type != DynamicBoneCollisionDataType.Regular)
						{
							if (reference2.type == DynamicBoneCollisionDataType.LeftHand)
							{
								list.Add(in k);
							}
							else
							{
								list2.Add(in k);
							}
						}
					}
				}
			}
			FixLength(k, parentIndex, num6);
			float num8 = (_data[k].pos - _data[k].prevPos).Magnitude * invertedSmoothDelta;
			_data[k].vel = MathX.ClampMagnitude(in _data[k].vel, num8 * 2f);
		}
		float stretch = 0f;
		if (_lastEffectorBoneIndex >= 0)
		{
			float3 b5 = _space.SpacePointToLocal((float3)EffectorBoneOffset, EffectorTarget);
			_data[_lastEffectorBoneIndex].pos = b5;
			for (int num9 = _lastEffectorBoneIndex; num9 > 0; num9--)
			{
				if (_data[num9].isIK && _data[num9].parentIndex != 0)
				{
					FixLength(_data[num9].parentIndex, num9, _data[num9].length * (num3 + _data[num9].stretch));
				}
			}
			for (int m = 1; m < _data.Length; m++)
			{
				if (_collisions != null)
				{
					for (int n = 0; n < _collisions.Count; n++)
					{
						ref SphereCollisionData reference3 = ref _collisions.Elements[n];
						ApplySphereCollision(ref _data[m], in reference3.point, reference3.radius);
					}
				}
				FixLength(m, _data[m].parentIndex, _data[m].length * (num3 + _data[m].stretch));
			}
			float num10 = MathX.Distance(in _data[0].pos, in b5);
			float num11 = _grabbedChainLength * num3;
			stretch = num10 / num11;
			stretch = MathX.Min(val2: _stretchRatio = MathX.Max(stretch, 1f), val1: MaxStretchRatio) - 1f;
		}
		else
		{
			_stretchRatio = MathX.Max(_stretchRatio - base.Time.Delta * (float)StretchRestoreSpeed, 1f);
		}
		for (int num12 = 0; num12 < _data.Length; num12++)
		{
			if (_data[num12].isIK)
			{
				_data[num12].stretch = stretch;
			}
			else
			{
				_data[num12].stretch = MathX.Lerp(_data[num12].stretch, 0f, base.Time.Delta * (float)StretchRestoreSpeed);
			}
		}
		int closestCollision = GetClosestCollision(ref list, base.World.DynamicBones.LastLeftHandPositon);
		int closestCollision2 = GetClosestCollision(ref list2, base.World.DynamicBones.LastRightHandPosition);
		if (lastLeftCollision != closestCollision)
		{
			if (closestCollision >= 0)
			{
				base.LocalUserRoot.LeftHandSlot?.TryVibrate(HandCollisionVibration);
			}
			lastLeftCollision = closestCollision;
		}
		if (lastRightCollision != closestCollision2)
		{
			if (closestCollision2 >= 0)
			{
				base.LocalUserRoot.RightHandSlot?.TryVibrate(HandCollisionVibration);
			}
			lastRightCollision = closestCollision2;
		}
		BoundingBox boundingBox = BoundingBox.Empty();
		for (int num13 = 0; num13 < _data.Length; num13++)
		{
			boundingBox.Encapsulate(in _data[num13].pos, _data[num13].radius);
		}
		BoundingBox globalBounds = boundingBox.Transform(in _space.LocalToGlobal_Fast);
		globalBounds.Expand(globalBounds.Size * 0.05f);
		GlobalBounds = globalBounds;
		for (int num14 = 0; num14 < _data.Length; num14++)
		{
			int parentIndex2 = _data[num14].parentIndex;
			if (parentIndex2 >= 0)
			{
				if (_data[parentIndex2].childCount == 1)
				{
					floatQ a4 = floatQ.FromToRotation(_data[parentIndex2].rot * _data[num14].dir, _data[num14].pos - _data[parentIndex2].pos);
					_data[parentIndex2].rot = a4 * _data[parentIndex2].rot;
				}
				_data[num14].rot = _data[parentIndex2].rot * _data[num14].restRot;
			}
		}
	}

	private void FixLength(int index, int parentIndex, float boneLength)
	{
		float magnitude;
		float3 v = (_data[parentIndex].pos - _data[index].pos).GetNormalized(out magnitude);
		if (magnitude > 0f)
		{
			float num = magnitude - boneLength;
			float3 b = v * num;
			ref float3 pos = ref _data[index].pos;
			pos += b;
		}
	}

	private int GetClosestCollision(ref RawValueList<int> list, in float3 point)
	{
		if (list == null)
		{
			return -1;
		}
		int result = -1;
		float num = float.MaxValue;
		for (int i = 0; i < list.Count; i++)
		{
			float num2 = MathX.Distance(in point, in _data[list[i]].pos);
			if (num2 < num)
			{
				result = list[i];
				num = num2;
			}
		}
		Pool.Return(ref list);
		return result;
	}

	public void SetupFromChildren(Slot root, bool forceLink = false, Predicate<Slot> filter = null)
	{
		Bones.Clear();
		AddHiearchy(root, forceLink, filter);
	}

	public void AddHiearchy(Slot root, bool forceLink = false, Predicate<Slot> filter = null, Slot parent = null)
	{
		if ((filter == null || filter(root)) && ((!root.Position_Field.IsDriven && !root.Rotation_Field.IsDriven) || forceLink) && (parent == null || MathX.Distance(root.GlobalPosition, parent.GlobalPosition) > 0.0001f))
		{
			parent = root;
			Bones.Add().Assign(root);
		}
		foreach (Slot child in root.Children)
		{
			AddHiearchy(child, forceLink, filter, parent);
		}
	}

	public void BuildInspectorUI(UIBuilder ui)
	{
		WorkerInspector.BuildInspectorUI(this, ui);
		ui.Button("Inspector.DynamicBoneChain.SetupFromChildren".AsLocaleKey(), SetupFromChildren);
		ui.Button("Inspector.DynamicBoneChain.SetupFromChildrenAll".AsLocaleKey(), SetupFromAllChildren);
		ui.Button("Inspector.DynamicBoneChain.SetupFromChildrenRig".AsLocaleKey(), SetupFromAllRigChildren);
		ui.Button("Inspector.DynamicBoneChain.ReplaceSmoothTransforms".AsLocaleKey(), ReplaceSmoothTransforms);
		ui.Button("Inspector.DynamicBoneChain.ClearSmoothTransforms".AsLocaleKey(), CleanSmoothTransforms);
		ui.Text("Inspector.DynamicBoneChain.CollidersGrabbingHeader".AsLocaleKey());
		ui.Button("Inspector.DynamicBoneChain.AddFixedCollidersFromHierarchy".AsLocaleKey(), AddCollidersFromHierarchy);
		ui.Button("Inspector.DynamicBoneChain.AlwaysGrabLastBone".AsLocaleKey(), AlwaysGrabLastBone);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void SetupFromChildren(IButton button, ButtonEventData eventData)
	{
		SetupFromChildren(base.Slot);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void SetupFromAllChildren(IButton button, ButtonEventData eventData)
	{
		SetupFromChildren(base.Slot, forceLink: true);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void SetupFromAllRigChildren(IButton button, ButtonEventData eventData)
	{
		Rig rig = base.Slot.GetComponentInParentsOrChildren<Rig>();
		if (rig == null)
		{
			rig = base.Slot.GetObjectRoot().GetComponentInChildren<Rig>();
		}
		if (rig != null)
		{
			SetupFromChildren(base.Slot, forceLink: false, (Slot s) => rig.IsBone(s));
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void ReplaceSmoothTransforms(IButton button, ButtonEventData eventData)
	{
		Bones.Clear();
		List<Slot> list = Pool.BorrowList<Slot>();
		ClearSmoothTransforms(list);
		foreach (Slot item in list)
		{
			Bones.Add().Assign(item);
		}
		Pool.Return(ref list);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void AddCollidersFromHierarchy(IButton button, ButtonEventData eventData)
	{
		Slot objectRoot = base.Slot.GetObjectRoot();
		StaticColliders.AddRangeUnique(objectRoot.GetComponentsInChildren<IDynamicBoneCollider>());
	}

	[SyncMethod(typeof(Delegate), null)]
	private void AlwaysGrabLastBone(IButton button, ButtonEventData eventData)
	{
		for (int num = Bones.Count - 2; num >= 0; num--)
		{
			Bones[num].GrabOverride.Target = Bones[Bones.Count - 1];
		}
	}

	private void ClearSmoothTransforms(List<Slot> slots)
	{
		foreach (SmoothTransform componentsInChild in base.Slot.GetComponentsInChildren<SmoothTransform>())
		{
			slots?.Add(componentsInChild.Slot);
			float3 value = componentsInChild.TargetPosition.Value;
			floatQ value2 = componentsInChild.TargetRotation.Value;
			float3 value3 = componentsInChild.TargetScale.Value;
			IField<float3> target = componentsInChild.Position.Target;
			IField<floatQ> target2 = componentsInChild.Rotation.Target;
			IField<float3> target3 = componentsInChild.Scale.Target;
			componentsInChild.Destroy();
			if (target != null)
			{
				target.Value = value;
			}
			if (target2 != null)
			{
				target2.Value = value2;
			}
			if (target3 != null)
			{
				target3.Value = value3;
			}
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void CleanSmoothTransforms(IButton button, ButtonEventData eventData)
	{
		ClearSmoothTransforms(null);
	}

	public IGrabbable Grab(Grabber grabber, Slot holdSlot, bool supressEvents = false)
	{
		if (!CanGrab(grabber))
		{
			return null;
		}
		if (grabber.Slot.IsUnderLocalUser)
		{
			if (grabber.CorrespondingBodyNode.Value == BodyNode.LeftHand && (bool)IgnoreOwnLeftHand)
			{
				return null;
			}
			if (grabber.CorrespondingBodyNode.Value == BodyNode.RightHand && (bool)IgnoreOwnRightHand)
			{
				return null;
			}
		}
		float distance;
		int num = FindClosestBone(holdSlot.GlobalPosition, out distance);
		if (num < 0)
		{
			return null;
		}
		if (num == 0)
		{
			if (IgnoreGrabOnFirstBone.Value)
			{
				return null;
			}
			num = 1;
			if (_data.Length == 1)
			{
				return null;
			}
		}
		Slot actualSimulationSpace = ActualSimulationSpace;
		float num2 = grabber.Slot.LocalScaleToSpace(0.04f, actualSimulationSpace);
		if (distance > (_data[num].radius + num2) * (float)GrabRadiusTolerance)
		{
			return null;
		}
		GrabBone(num, grabber, holdSlot);
		grabber.Slot.TryVibrate(GrabVibration);
		return this;
	}

	private void GrabBone(int index, Grabber grabber, Slot grabbingSlot)
	{
		Slot actualSimulationSpace = ActualSimulationSpace;
		_activeGrabber.Target = grabber;
		EffectorTarget.Target = grabbingSlot;
		EffectorBoneIndex.Value = index;
		EffectorBoneOffset.Value = actualSimulationSpace.LocalPointToSpace(in _data[index].pos, grabbingSlot);
		RunGrabEvent(released: false);
	}

	public void Release(Grabber grabber, bool supressEvents = false)
	{
		if (grabber != null && grabber == Grabber)
		{
			_activeGrabber.Target = null;
			EffectorTarget.Target = null;
			EffectorBoneIndex.Value = -1;
			RunGrabEvent(released: true);
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

	private int FindClosestBone(float3 point, out float distance)
	{
		if (_data == null)
		{
			distance = float.MaxValue;
			return -1;
		}
		int result = -1;
		float num = float.MaxValue;
		point = ActualSimulationSpace.GlobalPointToLocal(in point);
		for (int i = 0; i < _data.Length; i++)
		{
			if (GrabTerminalBones.Value || _data[i].childCount != 0)
			{
				float num2 = MathX.Distance(in point, in _data[i].pos);
				if (num2 < num)
				{
					num = num2;
					result = i;
				}
			}
		}
		distance = num;
		return result;
	}

	protected override void OnChanges()
	{
		base.OnChanges();
		if (!base.Enabled)
		{
			RestoreOriginalTransforms();
			Unregister();
		}
		else if (base.Slot.IsActive && ManagerIndex < 0)
		{
			base.World.DynamicBones.RegisterChain(this);
		}
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();
		RestoreOriginalTransforms();
		Unregister();
	}

	protected override void OnActivated()
	{
		base.OnActivated();
		if (base.Enabled && ManagerIndex < 0)
		{
			base.World.DynamicBones.RegisterChain(this);
		}
	}

	protected override void OnDeactivated()
	{
		base.OnDeactivated();
		Unregister();
	}

	protected override void OnDestroy()
	{
		Unregister();
		base.OnDestroy();
	}

	protected override void OnDispose()
	{
		Unregister();
		base.OnDispose();
	}

	private void Unregister()
	{
		if (ManagerIndex >= 0)
		{
			base.World.DynamicBones.UnregisterChain(this);
		}
	}

	private void RestoreOriginalTransforms()
	{
		_dataInvalid = true;
		foreach (Bone bone in Bones)
		{
			Slot target = bone.BoneSlot.Target;
			if (target != null)
			{
				target.LocalPosition = bone.OrigPosition.Value;
				target.LocalRotation = bone.OrigRotation.Value;
			}
		}
	}

	protected override void OnLoading(DataTreeNode node, LoadControl control)
	{
		base.OnLoading(node, control);
		if (VisualizeColliders.Value)
		{
			RunSynchronously(delegate
			{
				VisualizeColliders.Value = false;
			});
		}
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		Inertia = new Sync<float>();
		InertiaForce = new Sync<float>();
		Damping = new Sync<float>();
		Elasticity = new Sync<float>();
		Stiffness = new Sync<float>();
		SimulateTerminalBones = new Sync<bool>();
		BaseBoneRadius = new Sync<float>();
		DynamicPlayerCollision = new Sync<bool>();
		CollideWithOwnBody = new Sync<bool>();
		HandCollisionVibration = new Sync<VibratePreset>();
		CollideWithHead = new Sync<bool>();
		CollideWithBody = new Sync<bool>();
		CollideWithLeftHand = new Sync<bool>();
		CollideWithRightHand = new Sync<bool>();
		Gravity = new Sync<float3>();
		GravitySpace = new RootSpace();
		UseUserGravityDirection = new Sync<bool>();
		LocalForce = new Sync<float3>();
		GlobalStretch = new Sync<float>();
		MaxStretchRatio = new Sync<float>();
		CurrentStretchRatio = new RawOutput<float>();
		StretchRestoreSpeed = new Sync<float>();
		UseLocalUserSpace = new Sync<bool>();
		SimulationSpace = new RootSpace();
		StaticColliders = new SyncRefList<IDynamicBoneCollider>();
		VisualizeColliders = new Sync<bool>();
		VisualizeColliders.MarkNonPersistent();
		VisualizeBones = new Sync<bool>();
		VisualizeBones.MarkNonPersistent();
		IsGrabbable = new Sync<bool>();
		ActiveUserRootOnly = new Sync<bool>();
		AllowSteal = new Sync<bool>();
		GrabPriority = new Sync<int>();
		IgnoreGrabOnFirstBone = new Sync<bool>();
		GrabRadiusTolerance = new Sync<float>();
		GrabReleaseDistance = new Sync<float>();
		GrabSlipping = new Sync<bool>();
		GrabTerminalBones = new Sync<bool>();
		GrabVibration = new Sync<VibratePreset>();
		IgnoreOwnLeftHand = new Sync<bool>();
		IgnoreOwnRightHand = new Sync<bool>();
		EffectorTarget = new SyncRef<Slot>();
		EffectorBoneIndex = new Sync<int>();
		EffectorBoneOffset = new Sync<float3>();
		_activeGrabber = new SyncRef<Grabber>();
		Bones = new SyncList<Bone>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => Inertia, 
			4 => InertiaForce, 
			5 => Damping, 
			6 => Elasticity, 
			7 => Stiffness, 
			8 => SimulateTerminalBones, 
			9 => BaseBoneRadius, 
			10 => DynamicPlayerCollision, 
			11 => CollideWithOwnBody, 
			12 => HandCollisionVibration, 
			13 => CollideWithHead, 
			14 => CollideWithBody, 
			15 => CollideWithLeftHand, 
			16 => CollideWithRightHand, 
			17 => Gravity, 
			18 => GravitySpace, 
			19 => UseUserGravityDirection, 
			20 => LocalForce, 
			21 => GlobalStretch, 
			22 => MaxStretchRatio, 
			23 => CurrentStretchRatio, 
			24 => StretchRestoreSpeed, 
			25 => UseLocalUserSpace, 
			26 => SimulationSpace, 
			27 => StaticColliders, 
			28 => VisualizeColliders, 
			29 => VisualizeBones, 
			30 => IsGrabbable, 
			31 => ActiveUserRootOnly, 
			32 => AllowSteal, 
			33 => GrabPriority, 
			34 => IgnoreGrabOnFirstBone, 
			35 => GrabRadiusTolerance, 
			36 => GrabReleaseDistance, 
			37 => GrabSlipping, 
			38 => GrabTerminalBones, 
			39 => GrabVibration, 
			40 => IgnoreOwnLeftHand, 
			41 => IgnoreOwnRightHand, 
			42 => EffectorTarget, 
			43 => EffectorBoneIndex, 
			44 => EffectorBoneOffset, 
			45 => _activeGrabber, 
			46 => Bones, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static DynamicBoneChain __New()
	{
		return new DynamicBoneChain();
	}
}
