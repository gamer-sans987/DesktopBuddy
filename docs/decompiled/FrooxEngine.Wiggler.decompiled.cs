using System;
using Elements.Core;

namespace FrooxEngine;

[Category(new string[] { "Transform/Drivers" })]
public class Wiggler : Component
{
	protected readonly FieldDrive<floatQ> _target;

	protected readonly Sync<floatQ> _offset;

	protected readonly Sync<float3> _speed;

	protected readonly Sync<float3> _magnitude;

	protected readonly Sync<float3> _seed;

	public Slot WiggleTarget
	{
		set
		{
			_target.Target = value.Rotation_Field;
		}
	}

	public floatQ Wiggle
	{
		get
		{
			float num = (float)base.Time.WorldTime;
			return floatQ.Euler(MathX.SimplexNoise(num * Speed.x + Seed.x) * Magnitude.x, MathX.SimplexNoise(num * Speed.y + Seed.y) * Magnitude.y, MathX.SimplexNoise(num * Speed.z + Seed.z) * Magnitude.z);
		}
	}

	public floatQ Rotation
	{
		get
		{
			return Wiggle * _offset.Value;
		}
		set
		{
			_offset.Value = Wiggle.Inverted * value;
		}
	}

	public float3 Seed
	{
		get
		{
			return _seed;
		}
		set
		{
			floatQ rotation = Rotation;
			_seed.Value = value;
			Rotation = rotation;
		}
	}

	public float3 Magnitude
	{
		get
		{
			return _magnitude;
		}
		set
		{
			floatQ rotation = Rotation;
			_magnitude.Value = value;
			Rotation = rotation;
		}
	}

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

	protected override void OnAttach()
	{
		WiggleTarget = base.Slot;
		Rotation = base.Slot.LocalRotation;
		_seed.Value = RandomX.Float3 * 1024f;
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
		_target = new FieldDrive<floatQ>();
		_offset = new Sync<floatQ>();
		_speed = new Sync<float3>();
		_magnitude = new Sync<float3>();
		_seed = new Sync<float3>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => _target, 
			4 => _offset, 
			5 => _speed, 
			6 => _magnitude, 
			7 => _seed, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static Wiggler __New()
	{
		return new Wiggler();
	}
}
