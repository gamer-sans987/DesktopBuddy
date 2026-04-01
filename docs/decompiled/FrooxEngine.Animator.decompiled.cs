using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Elements.Core;
using FrooxEngine.UIX;

namespace FrooxEngine;

[Category(new string[] { "Rendering" })]
public class Animator : Component, IPlayable, IChangeable, IWorldElement, ICustomInspector, IWorker
{
	private interface IFieldMapper
	{
		void Set(float time);

		void Setup(IField field, AnimationTrack track);
	}

	public class FieldMapper<T> : IFieldMapper
	{
		private IField<T> field;

		private IAnimationTrack<T> track;

		public void Setup(IField field, AnimationTrack track)
		{
			this.field = (IField<T>)field;
			this.track = track as IAnimationTrack<T>;
		}

		public void Set(float time)
		{
			if (!field.IsRemoved)
			{
				if (track != null)
				{
					field.Value = track.Sample(time);
				}
				else
				{
					field.Value = default(T);
				}
			}
		}
	}

	protected readonly SyncPlayback _playback;

	public readonly AssetRef<Animation> Clip;

	public readonly SyncList<DriveRef<IField>> Fields;

	private List<IFieldMapper> _fieldMappers = new List<IFieldMapper>();

	private bool _fieldMappersValid;

	private static Dictionary<Type, Func<IFieldMapper>> _instancers = new Dictionary<Type, Func<IFieldMapper>>();

	public bool IsPlaying => _playback.IsPlaying;

	public bool IsFinished => _playback.IsFinished;

	public bool Loop
	{
		get
		{
			return _playback.Loop;
		}
		set
		{
			_playback.Loop = value;
		}
	}

	public float NormalizedPosition
	{
		get
		{
			return _playback.NormalizedPosition;
		}
		set
		{
			_playback.NormalizedPosition = value;
		}
	}

	public float Position
	{
		get
		{
			return _playback.Position;
		}
		set
		{
			_playback.Position = value;
		}
	}

	public double ClipLength => _playback.ClipLength;

	public bool IsStreaming => _playback.IsStreaming;

	public float Speed
	{
		get
		{
			return _playback.Speed;
		}
		set
		{
			_playback.Speed = value;
		}
	}

	public Task SetupFieldsByName(Slot root)
	{
		HashSet<Slot> ignoreSlots = new HashSet<Slot>();
		return StartTask(async delegate
		{
			await SetupFieldsAsync(delegate(AnimationTrack track)
			{
				Slot slot = root.FindChild((Slot s) => s.Name == track.Node && !ignoreSlots.Contains(s));
				if (slot == null)
				{
					return (IField)null;
				}
				string[] array = track.Property.Split('.');
				if (array.Length == 1)
				{
					return slot.TryGetField(array[0]);
				}
				if (array.Length == 2)
				{
					return slot.GetComponent(array[0])?.TryGetField(array[1]);
				}
				throw new Exception("Invalid Property Path: " + track.Property);
			}, ignoreSlots);
		});
	}

	private async Task SetupFieldsAsync(Func<AnimationTrack, IField> mapper, HashSet<Slot> ignoreSlots)
	{
		Fields.Clear();
		while (!Clip.IsAssetAvailable)
		{
			await default(NextUpdate);
		}
		AnimX data = Clip.Asset.Data;
		for (int i = 0; i < data.TrackCount; i++)
		{
			AnimationTrack arg = data[i];
			IField field = mapper(arg);
			if (field == null)
			{
				continue;
			}
			if (field.IsDriven)
			{
				ignoreSlots.Add(field.FindNearestParent<Slot>());
				field = mapper(arg);
				if (field == null)
				{
					continue;
				}
			}
			Fields.Add().Target = field;
		}
	}

	protected override void OnAwake()
	{
		Clip.Changed += delegate
		{
			InvalidateFieldMappers();
		};
		Fields.Changed += delegate
		{
			InvalidateFieldMappers();
		};
	}

	private void InvalidateFieldMappers()
	{
		_fieldMappersValid = false;
	}

	protected override void OnCommonUpdate()
	{
		_playback.ClipLength = (Clip.Asset?.Data?.GlobalDuration).GetValueOrDefault();
		if (!_fieldMappersValid)
		{
			GenerateFieldMappers();
		}
		foreach (IFieldMapper fieldMapper in _fieldMappers)
		{
			fieldMapper.Set(_playback.Position);
		}
	}

	private void GenerateFieldMappers()
	{
		_fieldMappers.Clear();
		AnimX animX = Clip.Asset?.Data;
		for (int i = 0; i < Fields.Count; i++)
		{
			IField target = Fields[i].Target;
			AnimationTrack track = ((animX != null && i < animX.TrackCount) ? animX[i] : null);
			if (target != null)
			{
				IFieldMapper fieldMapper = CreateFieldMapper(target.ValueType);
				fieldMapper.Setup(target, track);
				_fieldMappers.Add(fieldMapper);
			}
		}
		_fieldMappersValid = true;
	}

	private static IFieldMapper CreateFieldMapper(Type fieldType)
	{
		if (!_instancers.TryGetValue(fieldType, out Func<IFieldMapper> value))
		{
			Type type = typeof(FieldMapper<>).MakeGenericType(fieldType);
			ConstructorInfo ctr = type.GetConstructor(new Type[0]);
			value = () => (IFieldMapper)ctr.Invoke(null);
			_instancers.Add(fieldType, value);
		}
		return value();
	}

	public void Pause()
	{
		_playback.Pause();
	}

	public void Play()
	{
		_playback.Play();
	}

	public void Resume()
	{
		_playback.Resume();
	}

	public void Stop()
	{
		_playback.Stop();
	}

	public void TogglePlayback()
	{
		_playback.TogglePlayback();
	}

	public void BuildInspectorUI(UIBuilder ui)
	{
		WorkerInspector.BuildInspectorUI(this, ui);
		ui.Button((LocaleString)"Setup fields by name", OnSetupFieldsByName);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnSetupFieldsByName(IButton button, ButtonEventData eventData)
	{
		SetupFieldsByName(base.Slot);
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		_playback = new SyncPlayback();
		Clip = new AssetRef<Animation>();
		Fields = new SyncList<DriveRef<IField>>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => _playback, 
			4 => Clip, 
			5 => Fields, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static Animator __New()
	{
		return new Animator();
	}
}
