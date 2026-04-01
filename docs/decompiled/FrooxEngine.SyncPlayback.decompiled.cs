using System;
using System.Collections.Generic;
using System.IO;
using Elements.Core;

namespace FrooxEngine;

public class SyncPlayback : ConflictingSyncElement, IPlayable, IChangeable, IWorldElement
{
	private enum OffsetType
	{
		Stopped,
		Playback
	}

	public static readonly float DEFAULT_MAX_ERROR = 5f;

	private bool _play;

	private bool _loop;

	private float _offset;

	private float _speed = 1f;

	private float _maxError = DEFAULT_MAX_ERROR;

	private float _lastSpeed;

	private float _lastOffset;

	private float _lastMaxError = DEFAULT_MAX_ERROR;

	public double ClipLength { get; set; } = -1.0;

	public bool IsStreaming => double.IsPositiveInfinity(ClipLength);

	protected bool CanCurrentContextModify
	{
		get
		{
			if (base.IsHooked)
			{
				return base.ActiveLink.IsModificationAllowed;
			}
			return true;
		}
	}

	public float Offset
	{
		get
		{
			return _offset;
		}
		set
		{
			if (CanCurrentContextModify)
			{
				float? offset = value;
				InternalSetState(null, null, offset);
			}
		}
	}

	public float Position
	{
		get
		{
			return (float)ComputePosition();
		}
		set
		{
			if (!IsStreaming && !(ClipLength < 0.0) && CanCurrentContextModify)
			{
				if (!IsPlaying && _play)
				{
					Stop();
				}
				value = MathX.Clamp(MathX.FilterInvalid(value), 0f, (float)ClipLength);
				float? offset = ComputeOffset(value, _play ? OffsetType.Playback : OffsetType.Stopped, _speed);
				InternalSetState(null, null, offset);
			}
		}
	}

	public float NormalizedPosition
	{
		get
		{
			if (IsStreaming)
			{
				return -1f;
			}
			if (ClipLength <= 0.0 || ClipLength >= 3.4028234663852886E+38)
			{
				return -1f;
			}
			return (float)((double)Position / ClipLength);
		}
		set
		{
			if (!IsStreaming)
			{
				Position = (float)((double)value * ClipLength);
			}
		}
	}

	public float StartPosition
	{
		get
		{
			if (!(_speed >= 0f))
			{
				return (float)ClipLength;
			}
			return 0f;
		}
	}

	public double InstantPosition => ComputePosition(base.Time.TimeSinceLastUpdate);

	public double InstantDSP_Position => ComputePosition(base.Time.TimeSinceLastDSPSync);

	public double RawInstantPosition => ComputeRawPosition(base.Time.TimeSinceLastUpdate);

	public float Speed
	{
		get
		{
			return _speed;
		}
		set
		{
			value = MathX.FilterInvalid(value);
			if (!MathX.Approximately(_speed, value) && CanCurrentContextModify)
			{
				if (_play)
				{
					float? offset = ComputeOffset(ComputeRawPosition(), OffsetType.Playback, value);
					float? speed = value;
					InternalSetState(null, null, offset, speed);
				}
				else
				{
					float? speed = value;
					InternalSetState(null, null, null, speed);
				}
			}
		}
	}

	public bool IsPlaying => ComputeIsPlaying();

	public bool IsFinished
	{
		get
		{
			if (!IsPlaying)
			{
				return NormalizedPosition >= 1f;
			}
			return false;
		}
	}

	public bool Loop
	{
		get
		{
			return _loop;
		}
		set
		{
			if (IsStreaming || _loop == value || !CanCurrentContextModify)
			{
				return;
			}
			if (_play)
			{
				if (IsPlaying)
				{
					bool? loop = value;
					InternalSetState(null, loop);
				}
				else
				{
					InternalSetState(false, value, (float)ClipLength);
				}
			}
			else
			{
				bool? loop = value;
				InternalSetState(null, loop);
			}
		}
	}

	public override IEnumerable<ILinkable> LinkableChildren => null;

	public event SyncPlaybackEvent OnPlaybackChange;

	protected void InternalSetState(bool? play = null, bool? loop = null, float? offset = null, float? speed = null, float? maxError = null, bool sync = true, bool change = true)
	{
		BeginModification();
		_play = play ?? _play;
		_loop = loop ?? _loop;
		_offset = offset ?? _offset;
		_speed = speed ?? _speed;
		_maxError = maxError ?? _maxError;
		if (sync && !base.IsInInitPhase && base.GenerateSyncData)
		{
			InvalidateSyncElement();
		}
		if (change)
		{
			BlockModification();
			PlaybackChanged();
			UnblockModification();
		}
		EndModification();
	}

	private void PlaybackChanged()
	{
		SyncElementChanged();
		if (this.OnPlaybackChange != null)
		{
			this.OnPlaybackChange(this);
		}
	}

	private double ComputeRawPosition(float worldTimeOffset = 0f)
	{
		if (base.IsRemoved)
		{
			return double.NaN;
		}
		TimeController time = base.Time;
		if (time == null)
		{
			return double.NaN;
		}
		if (_play && !MathX.Approximately(_speed, 0f))
		{
			return (time.WorldTime + (double)worldTimeOffset + (double)_offset) * (double)_speed;
		}
		return _offset;
	}

	private float ComputeOffset(double position, OffsetType offsetType, float playbackSpeed)
	{
		if (offsetType == OffsetType.Playback && !MathX.Approximately(playbackSpeed, 0f))
		{
			return MathX.FilterInvalid((float)(position / (double)playbackSpeed - base.Time.WorldTime));
		}
		return (float)position;
	}

	public bool ComputeIsPlaying(float worldTimeOffset = 0f)
	{
		if (base.IsRemoved)
		{
			return false;
		}
		if (!_play)
		{
			return false;
		}
		if (_loop)
		{
			return true;
		}
		if (ClipLength < 0.0)
		{
			return _play;
		}
		double num = ComputeRawPosition(worldTimeOffset);
		if (double.IsNaN(num))
		{
			return false;
		}
		if (_speed >= 0f)
		{
			return num < ClipLength;
		}
		return num > 0.0;
	}

	public double ComputePosition(float worldTimeOffset = 0f)
	{
		if (IsStreaming)
		{
			return 0.0;
		}
		return ProcessRawPosition(ComputeRawPosition(worldTimeOffset));
	}

	public int LoopCount(float worldTimeOffset = 0f)
	{
		if (!_loop)
		{
			return 0;
		}
		if (ClipLength < 0.0)
		{
			return 0;
		}
		return (int)(ComputeRawPosition(worldTimeOffset) / ClipLength);
	}

	public void TrySetNormalizedPosition(float position, float maxError = 0f)
	{
		if (!IsStreaming)
		{
			float position2 = Position;
			float num = (float)((double)position * ClipLength);
			double num2 = ((!Loop) ? ((double)MathX.Abs(position2 - num)) : MathX.WrapAroundDistance(position2, num, ClipLength));
			if (float.IsNaN(Position) || num2 > (double)maxError)
			{
				Position = num;
			}
		}
	}

	public double ProcessRawPosition(double rawPosition)
	{
		if (IsStreaming)
		{
			return 0.0;
		}
		if (ClipLength <= 0.0)
		{
			return 0.0;
		}
		if (_loop)
		{
			return MathX.Repeat(rawPosition, ClipLength);
		}
		return MathX.Clamp(rawPosition, 0.0, ClipLength);
	}

	public void Play()
	{
		if (CanCurrentContextModify)
		{
			bool? play = true;
			float? offset = ComputeOffset(StartPosition, OffsetType.Playback, _speed);
			InternalSetState(play, null, offset);
		}
	}

	public void Stop()
	{
		if (CanCurrentContextModify)
		{
			bool? play = false;
			float? offset = StartPosition;
			InternalSetState(play, null, offset);
		}
	}

	public void Pause()
	{
		if (CanCurrentContextModify && _play)
		{
			bool? play = false;
			float? offset = (float)ComputeRawPosition();
			InternalSetState(play, null, offset);
		}
	}

	public void Resume()
	{
		if (CanCurrentContextModify && !IsPlaying)
		{
			if ((!_loop && MathX.Approximately(NormalizedPosition, 1f)) || MathX.Approximately(NormalizedPosition, 0f))
			{
				Play();
				return;
			}
			bool? play = true;
			float? offset = ComputeOffset(_offset, OffsetType.Playback, _speed);
			InternalSetState(play, null, offset);
		}
	}

	public void TogglePlayback()
	{
		if (!IsPlaying)
		{
			Resume();
		}
		else
		{
			Pause();
		}
	}

	protected override void InternalEncodeDelta(BinaryWriter writer, BinaryMessageBatch outboundMessage)
	{
		byte data = 0;
		bool flag = _lastOffset != _offset;
		bool flag2 = _lastSpeed != _speed;
		bool flag3 = _lastMaxError != _maxError;
		data.SetBits(_play, _loop, flag, flag2, flag3);
		writer.Write(data);
		if (flag)
		{
			writer.Write(_offset);
		}
		if (flag2)
		{
			writer.Write(_speed);
		}
		if (flag3)
		{
			writer.Write(_maxError);
		}
	}

	protected override void InternalDecodeDelta(BinaryReader reader, BinaryMessageBatch inboundMessage)
	{
		byte data = reader.ReadByte();
		int num = 0;
		bool bit = data.GetBit(num++);
		bool bit2 = data.GetBit(num++);
		bool bit3 = data.GetBit(num++);
		bool bit4 = data.GetBit(num++);
		bool bit5 = data.GetBit(num++);
		float? offset = null;
		float? speed = null;
		float? num2 = null;
		if (bit3)
		{
			offset = reader.ReadSingle();
		}
		if (bit4)
		{
			speed = reader.ReadSingle();
		}
		if (bit5)
		{
			num2 = reader.ReadSingle();
		}
		if (bit3 && bit)
		{
			offset = base.World.Time.AdjustTimeOffset(offset.Value, inboundMessage.SenderTime, num2 ?? _maxError);
		}
		InternalSetState(bit, bit2, offset, speed, num2, sync: false);
	}

	protected override void InternalEncodeFull(BinaryWriter writer, BinaryMessageBatch outboundMessage)
	{
		byte data = 0;
		data.SetBits(_play, _loop);
		writer.Write(data);
		writer.Write(_offset);
		writer.Write(_speed);
		writer.Write(_maxError);
	}

	protected override void InternalDecodeFull(BinaryReader reader, BinaryMessageBatch inboundMessage)
	{
		byte data = reader.ReadByte();
		int num = 0;
		bool bit = data.GetBit(num++);
		bool bit2 = data.GetBit(num++);
		float num2 = reader.ReadSingle();
		float value = reader.ReadSingle();
		float num3 = reader.ReadSingle();
		if (bit)
		{
			num2 = base.World.Time.AdjustTimeOffset(num2, inboundMessage.SenderTime, num3);
		}
		InternalSetState(bit, bit2, num2, value, num3, sync: false);
	}

	protected override DataTreeNode InternalSave(SaveControl control)
	{
		DataTreeDictionary dataTreeDictionary = new DataTreeDictionary();
		dataTreeDictionary.Add("Play", _play);
		dataTreeDictionary.Add("Loop", _loop);
		dataTreeDictionary.Add("RawPosition", ComputeRawPosition());
		dataTreeDictionary.Add("Speed", _speed);
		dataTreeDictionary.Add("MaxError", _maxError);
		dataTreeDictionary.Add("MaxDeviation", _maxError);
		return dataTreeDictionary;
	}

	protected override void InternalLoad(DataTreeNode node, LoadControl control)
	{
		DataTreeDictionary obj = (DataTreeDictionary)node;
		bool flag = obj.ExtractOrThrow<bool>("Play");
		bool value = obj.ExtractOrThrow<bool>("Loop");
		float num = obj.ExtractOrThrow<float>("RawPosition");
		float num2 = obj.ExtractOrThrow<float>("Speed");
		float value2 = obj.ExtractOrDefault("MaxError", DEFAULT_MAX_ERROR);
		float value3 = ComputeOffset(num, flag ? OffsetType.Playback : OffsetType.Stopped, num2);
		InternalSetState(flag, value, value3, num2, value2);
	}

	public override string GetSyncMemberName(ISyncMember member)
	{
		return null;
	}

	protected override void InternalClearDirty()
	{
		_lastOffset = _offset;
		_lastSpeed = _speed;
		_lastMaxError = _maxError;
	}

	protected override void InternalCopy(ISyncMember source, Action<ISyncMember, ISyncMember> copy)
	{
		SyncPlayback syncPlayback = (SyncPlayback)source;
		InternalSetState(syncPlayback._play, syncPlayback._loop, syncPlayback._offset, syncPlayback._speed, syncPlayback._maxError);
	}

	public string ToDebugString()
	{
		return $"_play: {_play}, _loop: {_loop}, _offset: {_offset}, _speed: {_speed}, _maxDeviation: {_maxError}";
	}
}
