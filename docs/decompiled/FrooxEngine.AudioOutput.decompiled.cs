using System;
using System.Collections.Generic;
using System.Linq;
using Awwdio;
using Elements.Core;

namespace FrooxEngine;

[Category(new string[] { "Audio" })]
public class AudioOutput : Component
{
	public const int DEFAULT_PRIORITY = 128;

	public const float DEFAULT_MIN_DISTANCE = 1f;

	public const float DEFAULT_MAX_DISTANCE = 500f;

	public const AudioRolloffCurve DEFAULT_ROLLOFF = AudioRolloffCurve.LogarithmicFadeOff;

	[Range(0f, 1f, "0.##")]
	public readonly Sync<float> Volume;

	public readonly DestroyRelayRef<IWorldAudioDataSource> Source;

	[Range(0f, 1f, "0.##")]
	public readonly Sync<float> SpatialBlend;

	public readonly Sync<bool> Spatialize;

	public readonly Sync<float> SpatializationStartDistance;

	public readonly Sync<float> SpatializationTransitionRange;

	[Range(0f, 1f, "0.##")]
	public readonly Sync<float> DopplerLevel;

	[Range(0.5f, 2f, "0.##")]
	public readonly Sync<float> Pitch;

	public readonly Sync<bool?> Global;

	public readonly Sync<AudioRolloffCurve> RolloffMode;

	[Range(0f, 500f, "0.##")]
	public readonly Sync<float> MinDistance;

	[Range(0f, 500f, "0.##")]
	public readonly Sync<float> MaxDistance;

	public readonly Sync<int> Priority;

	public readonly Sync<AudioTypeGroup> AudioTypeGroup;

	public readonly Sync<AudioDistanceSpace> DistanceSpace;

	public readonly Sync<float> MinScale;

	public readonly Sync<float> MaxScale;

	public readonly Sync<bool> IgnoreAudioEffects;

	public readonly SyncRefList<AudioListener> ExcludedListeners;

	protected readonly SyncRefList<User> excludedUsers;

	private IAudioShape _audioShape;

	private bool _additionRemovalRegistered;

	private bool _updateRegistered;

	private bool _excludedListenersChanged;

	public bool ShouldBeEnabled
	{
		get
		{
			if (IsRemoved)
			{
				return false;
			}
			if (!base.Enabled)
			{
				return false;
			}
			if (!base.Slot.IsActive)
			{
				return false;
			}
			if (Source.Target == null)
			{
				return false;
			}
			if (IsLocalUserExluded)
			{
				return false;
			}
			User activeUser = base.Slot.ActiveUser;
			if (activeUser != null && activeUser.IsAudioLocallyBlocked)
			{
				return false;
			}
			return true;
		}
	}

	public bool IsRegistered => NativeOutput != null;

	public float ActualVolume
	{
		get
		{
			float num = MathX.Clamp01(Volume);
			num *= base.Engine.AudioSystem.GetAudioTypeGroupVolume(AudioTypeGroup);
			User activeUser = base.Slot.ActiveUser;
			if (activeUser != null)
			{
				num *= activeUser.LocalVolume;
			}
			return MathX.FilterInvalid(num);
		}
	}

	internal Awwdio.AudioOutput NativeOutput { get; private set; }

	public bool IsLocalUserExluded => IsUserExluded(base.World.LocalUser);

	public void GetActualDistances(out float minDistance, out float maxDistance, out float spatializationStartDistance, out float spatializationTransitionRange)
	{
		minDistance = MinDistance.Value;
		maxDistance = MaxDistance.Value;
		spatializationStartDistance = SpatializationStartDistance.Value;
		spatializationTransitionRange = SpatializationTransitionRange.Value;
		bool flag = float.IsPositiveInfinity(maxDistance);
		if (DistanceSpace.Value == AudioDistanceSpace.Local)
		{
			float val = MathX.AvgComponent(base.Slot.GlobalScale);
			val = MathX.Clamp(val, MinScale, MaxScale);
			minDistance *= val;
			if (!flag)
			{
				maxDistance *= val;
			}
			spatializationStartDistance *= val;
			spatializationTransitionRange *= val;
		}
		minDistance = MathX.Max(0f, MathX.FilterInvalid(minDistance));
		if (!flag)
		{
			maxDistance = MathX.Max(0f, MathX.FilterInvalid(maxDistance));
		}
		spatializationStartDistance = MathX.Max(0f, MathX.FilterInvalid(spatializationStartDistance));
		spatializationTransitionRange = MathX.Max(0f, MathX.FilterInvalid(spatializationTransitionRange));
		if (minDistance > maxDistance)
		{
			minDistance = maxDistance;
			maxDistance += minDistance * 0.0001f;
		}
	}

	protected override void OnAwake()
	{
		Volume.Value = 1f;
		SpatialBlend.Value = 1f;
		Spatialize.Value = true;
		SpatializationStartDistance.Value = 0.01f;
		SpatializationTransitionRange.Value = 0.01f;
		Pitch.Value = 1f;
		DopplerLevel.Value = 1f;
		RolloffMode.Value = AudioRolloffCurve.LogarithmicFadeOff;
		MinDistance.Value = 1f;
		MaxDistance.Value = 500f;
		Priority.Value = 128;
		AudioTypeGroup.Value = FrooxEngine.AudioTypeGroup.SoundEffect;
		DistanceSpace.Value = AudioDistanceSpace.Local;
		ExcludedListeners.Changed += ExcludedListeners_Changed;
		ExcludedListeners.ElementsAdded += ExcludedListeners_ElementsAdded;
		base.Slot.ActiveUserRootChanged += Slot_ActiveUserRootChanged;
		base.Slot.WorldTransformChanged += Slot_WorldTransformChanged;
		MinScale.Value = 0f;
		MaxScale.Value = float.PositiveInfinity;
		base.Audio.RegisterOutput(this);
	}

	private void ExcludedListeners_Changed(IChangeable obj)
	{
		_excludedListenersChanged = true;
		MarkChangeDirty();
	}

	private void ExcludedListeners_ElementsAdded(SyncElementList<SyncRef<AudioListener>> list, int startIndex, int count)
	{
		for (int i = 0; i < count; i++)
		{
			list[i + startIndex].OnTargetChange += AudioOutput_OnTargetChange;
		}
	}

	private void AudioOutput_OnTargetChange(SyncRef<AudioListener> reference)
	{
		_excludedListenersChanged = true;
		MarkChangeDirty();
	}

	private bool EnsureRegisteredOrUnregistered()
	{
		bool shouldBeEnabled = ShouldBeEnabled;
		if (!_additionRemovalRegistered && shouldBeEnabled != IsRegistered)
		{
			_additionRemovalRegistered = true;
			base.Audio.RegisterOutputToAddRemove(this);
		}
		return shouldBeEnabled;
	}

	protected override void OnChanges()
	{
		base.OnChanges();
		if (EnsureRegisteredOrUnregistered() && !_updateRegistered)
		{
			_updateRegistered = true;
			base.Audio.RegisterUpdatedOutput(this);
		}
	}

	internal void LinkNativeOutput(Awwdio.AudioOutput nativeOutput)
	{
		if (NativeOutput != null)
		{
			throw new InvalidOperationException("Native output has already been linked");
		}
		NativeOutput = nativeOutput;
		_additionRemovalRegistered = false;
		_excludedListenersChanged = true;
	}

	internal void ClearNativeOutput()
	{
		if (NativeOutput == null)
		{
			throw new InvalidOperationException("Native output has already been cleared");
		}
		NativeOutput = null;
		_audioShape = null;
		_additionRemovalRegistered = false;
	}

	internal void ClearAdditionRemovalFlag()
	{
		_additionRemovalRegistered = false;
	}

	internal void UpdateNativeOutput(ChangesBatch batch, bool updateExcludedListeners)
	{
		_updateRegistered = false;
		if (NativeOutput == null || IsRemoved)
		{
			return;
		}
		bool num = Global.Value ?? MathX.Approximately(SpatialBlend.Value, 0f);
		GetActualDistances(out var minDistance, out var maxDistance, out var spatializationStartDistance, out var spatializationTransitionRange);
		if (num)
		{
			if (!(_audioShape is GlobalAudioShape))
			{
				_audioShape = new GlobalAudioShape(NativeOutput);
			}
		}
		else
		{
			SphereAudioShape sphereAudioShape = _audioShape as SphereAudioShape;
			if (sphereAudioShape == null)
			{
				sphereAudioShape = (SphereAudioShape)(_audioShape = new SphereAudioShape(NativeOutput));
			}
			sphereAudioShape.Update(batch, minDistance, maxDistance, RolloffMode.Value);
		}
		NativeOutput.Update(batch, base.Slot.GlobalRigidTransform, ActualVolume, Priority, Spatialize, MathX.Clamp01(SpatialBlend), spatializationStartDistance, spatializationTransitionRange, MathX.Max(0f, MathX.FilterInvalid(Pitch)), MathX.Max(0f, MathX.FilterInvalid(DopplerLevel)));
		NativeOutput.Update(batch, Source.Target, _audioShape, IgnoreAudioEffects.Value ? null : base.Audio.EffectsInlet);
		if (_excludedListenersChanged && updateExcludedListeners)
		{
			UpdateExcludedListeners(batch);
		}
	}

	internal void UpdateExcludedListeners(ChangesBatch batch)
	{
		HashSet<Listener> hashSet = null;
		foreach (AudioListener excludedListener in ExcludedListeners)
		{
			if (excludedListener?.NativeListener != null)
			{
				if (hashSet == null)
				{
					hashSet = new HashSet<Listener>();
				}
				hashSet.Add(excludedListener.NativeListener);
			}
		}
		NativeOutput.Update(batch, hashSet);
		_excludedListenersChanged = false;
	}

	private void Slot_WorldTransformChanged(Slot slot)
	{
		MarkChangeDirty();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		EnsureRemoved();
	}

	protected override void OnDispose()
	{
		base.OnDispose();
		EnsureRemoved();
		base.Slot.ActiveUserRootChanged -= Slot_ActiveUserRootChanged;
		base.Slot.WorldTransformChanged -= Slot_WorldTransformChanged;
		base.Audio.UnregisterOutput(this);
	}

	private void EnsureRemoved()
	{
		if (!_additionRemovalRegistered)
		{
			_additionRemovalRegistered = true;
			base.Audio.RegisterOutputToAddRemove(this);
		}
	}

	private void Slot_ActiveUserRootChanged(Slot slot)
	{
		MarkChangeDirty();
	}

	protected override void OnAudioConfigurationChanged()
	{
		MarkChangeDirty();
	}

	protected override void OnActivated()
	{
		EnsureRegisteredOrUnregistered();
		MarkChangeDirty();
	}

	protected override void OnDeactivated()
	{
		EnsureRegisteredOrUnregistered();
	}

	protected override void OnEnabled()
	{
		EnsureRegisteredOrUnregistered();
		MarkChangeDirty();
	}

	protected override void OnDisabled()
	{
		EnsureRegisteredOrUnregistered();
	}

	public void SetupAsUI()
	{
		AudioTypeGroup.Value = FrooxEngine.AudioTypeGroup.UI;
		DopplerLevel.Value = 0f;
		IgnoreAudioEffects.Value = true;
	}

	[SyncMethod(typeof(Action<User>), new string[] { })]
	public void ExludeUser(User user)
	{
		if (!IsUserExluded(user))
		{
			excludedUsers.Add(user);
		}
	}

	[SyncMethod(typeof(Action), new string[] { })]
	public void ExludeLocalUser()
	{
		ExludeUser(base.LocalUser);
	}

	[SyncMethod(typeof(Action), new string[] { })]
	public void RemoveLocalExcludedUser()
	{
		RemoveExludedUser(base.LocalUser);
	}

	[SyncMethod(typeof(Action<User>), new string[] { })]
	public void RemoveExludedUser(User user)
	{
		excludedUsers.RemoveAll(user);
	}

	[SyncMethod(typeof(Action), new string[] { })]
	public void ClearExludedUsers()
	{
		excludedUsers.Clear();
	}

	[SyncMethod(typeof(Func<User, bool>), new string[] { })]
	public bool IsUserExluded(User user)
	{
		return excludedUsers.Any((User u) => u == user);
	}

	protected override void OnLoading(DataTreeNode node, LoadControl control)
	{
		base.OnLoading(node, control);
		DataTreeDictionary obj = node as DataTreeDictionary;
		if (obj == null || !obj.ContainsKey("DistanceSpace"))
		{
			control.OnLoaded(this, delegate
			{
				DistanceSpace.Value = AudioDistanceSpace.Global;
			});
		}
		if (control.GetFeatureFlag("Awwdio").HasValue)
		{
			return;
		}
		LegacyAudioOutputDopplerAdapter legacyAudioOutputDopplerAdapter = control.Convert<LegacyAudioOutputDopplerAdapter, float>(DopplerLevel);
		legacyAudioOutputDopplerAdapter.SpatialBlend.Target = SpatialBlend;
		legacyAudioOutputDopplerAdapter.Spatialize.Target = Spatialize;
		LegacyFeatureSettings? activeSetting = Settings.GetActiveSetting<LegacyFeatureSettings>();
		if (activeSetting != null && activeSetting.PreserveLegacyReverbZoneHandling.Value)
		{
			LegacyAudioOutputIgnoreReverbAdapter legacyAudioOutputIgnoreReverbAdapter = control.LoadConvert<LegacyAudioOutputIgnoreReverbAdapter, bool>(IgnoreAudioEffects, "IgnoreReverbZones", node);
			if (legacyAudioOutputIgnoreReverbAdapter != null)
			{
				legacyAudioOutputIgnoreReverbAdapter.Spatialize.Target = Spatialize;
			}
			else
			{
				IgnoreAudioEffects.DriveFrom(Spatialize);
			}
		}
		else
		{
			DataTreeNode dataTreeNode = ((DataTreeDictionary)node).TryGetNode("IgnoreReverbZones");
			if (dataTreeNode != null)
			{
				IgnoreAudioEffects.Load(dataTreeNode, control);
			}
		}
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		Volume = new Sync<float>();
		Source = new DestroyRelayRef<IWorldAudioDataSource>();
		SpatialBlend = new Sync<float>();
		Spatialize = new Sync<bool>();
		SpatializationStartDistance = new Sync<float>();
		SpatializationTransitionRange = new Sync<float>();
		DopplerLevel = new Sync<float>();
		Pitch = new Sync<float>();
		Global = new Sync<bool?>();
		RolloffMode = new Sync<AudioRolloffCurve>();
		MinDistance = new Sync<float>();
		MaxDistance = new Sync<float>();
		Priority = new Sync<int>();
		AudioTypeGroup = new Sync<AudioTypeGroup>();
		DistanceSpace = new Sync<AudioDistanceSpace>();
		MinScale = new Sync<float>();
		MaxScale = new Sync<float>();
		IgnoreAudioEffects = new Sync<bool>();
		ExcludedListeners = new SyncRefList<AudioListener>();
		excludedUsers = new SyncRefList<User>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => Volume, 
			4 => Source, 
			5 => SpatialBlend, 
			6 => Spatialize, 
			7 => SpatializationStartDistance, 
			8 => SpatializationTransitionRange, 
			9 => DopplerLevel, 
			10 => Pitch, 
			11 => Global, 
			12 => RolloffMode, 
			13 => MinDistance, 
			14 => MaxDistance, 
			15 => Priority, 
			16 => AudioTypeGroup, 
			17 => DistanceSpace, 
			18 => MinScale, 
			19 => MaxScale, 
			20 => IgnoreAudioEffects, 
			21 => ExcludedListeners, 
			22 => excludedUsers, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static AudioOutput __New()
	{
		return new AudioOutput();
	}
}
