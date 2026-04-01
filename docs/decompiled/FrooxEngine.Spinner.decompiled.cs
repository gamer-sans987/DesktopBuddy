using System;
using Elements.Core;

namespace FrooxEngine;

[Category(new string[] { "Transform/Drivers" })]
public class Spinner : Component
{
	public readonly Sync<float3> Range;

	protected readonly FieldDrive<floatQ> _target;

	protected readonly Sync<floatQ> _offset;

	protected readonly Sync<float3> _speed;

	public Slot SpinTarget
	{
		set
		{
			_target.Target = value.Rotation_Field;
		}
	}

	public floatQ Rotation
	{
		get
		{
			return _offset.Value * RawRotation;
		}
		set
		{
			_offset.Value = value * RawRotation.Inverted;
		}
	}

	public floatQ RawRotation => floatQ.Euler(_speed.Value * (float)base.Time.WorldTime % (float3)Range);

	public float3 Speed
	{
		get
		{
			return _speed;
		}
		set
		{
			floatQ rotation = Rotation;
			_speed.Value = value;
			Rotation = rotation;
		}
	}

	protected override void OnAwake()
	{
		Range.Value = float3.MaxValue;
	}

	protected override void OnAttach()
	{
		SpinTarget = base.Slot;
		Rotation = base.Slot.LocalRotation;
	}

	protected override void OnStart()
	{
		_target.SetupValueSetHook(OnFieldSet);
	}

	private void OnFieldSet(IField<floatQ> field, floatQ value)
	{
		Rotation = value;
		_target.Target.Value = value;
	}

	protected override void OnCommonUpdate()
	{
		if (_target.IsLinkValid)
		{
			_target.Target.Value = Rotation;
		}
	}

	protected override void OnDisabled()
	{
		if (_target.IsLinkValid)
		{
			_target.Target.Value = _offset.Value;
		}
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		Range = new Sync<float3>();
		_target = new FieldDrive<floatQ>();
		_offset = new Sync<floatQ>();
		_speed = new Sync<float3>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => Range, 
			4 => _target, 
			5 => _offset, 
			6 => _speed, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static Spinner __New()
	{
		return new Spinner();
	}
}
