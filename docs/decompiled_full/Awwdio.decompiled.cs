using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Permissions;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Threading.Tasks.Schedulers;
using Elements.Assets;
using Elements.Core;
using Elements.Data;
using SharpPipe;
using SteamAudio;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: AssemblyTitle("Awwdio")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("Awwdio")]
[assembly: AssemblyCopyright("Copyright ©  2025")]
[assembly: AssemblyTrademark("")]
[assembly: ComVisible(false)]
[assembly: Guid("d208f15d-f13c-440d-94bd-21ffe1cc9fb7")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: DataModelAssembly(DataModelAssemblyType.Core)]
[assembly: TargetFramework(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
[assembly: AssemblyVersion("1.0.0.0")]
[module: UnverifiableCode]
[module: RefSafetyRules(11)]
namespace Awwdio;

public class AudioDeviceOutput : Mixer
{
	private AutoDucker<StereoSample> ducker = new AutoDucker<StereoSample>();

	private float? _lastVolume;

	public IAudioDeviceOutput Device { get; set; }

	public float Volume { get; set; } = 1f;

	public AudioDeviceOutput(AudioSimulator simulator)
		: base(simulator)
	{
	}

	protected override void FinishMix(AudioBuffer buffer)
	{
		ducker.AutoDuck(buffer.Data.AsStereoBuffer());
		float volume = Volume;
		if (!_lastVolume.HasValue || _lastVolume == volume)
		{
			for (int i = 0; i < buffer.Data.Length; i++)
			{
				buffer.Data[i] *= volume;
			}
		}
		else
		{
			float num = 1f / (float)buffer.Data.Length;
			for (int j = 0; j < buffer.Data.Length; j++)
			{
				buffer.Data[j] *= MathX.LerpUnclamped(_lastVolume.Value, volume, (float)j * num);
			}
		}
		_lastVolume = volume;
		Device?.AudioFrameRendered(buffer.Data, base.Simulator.DSP_Time);
		base.Simulator.BufferPool.ReturnBuffer(buffer);
	}
}
public class AudioInlet
{
	public AudioSpace Space { get; private set; }

	public AudioInlet(AudioSpace space)
	{
		Space = space;
	}
}
public class AudioOutput : IDisposable
{
	private ConcurrentDictionary<Listener, AudioOutputListenerContext> listenerContexts = new ConcurrentDictionary<Listener, AudioOutputListenerContext>();

	public AudioSimulator System => Space.System;

	public AudioSpace Space { get; private set; }

	public bool IsValid
	{
		get
		{
			if (Source != null && Shape != null && !IsDisposed)
			{
				return !IsCorrupted;
			}
			return false;
		}
	}

	public bool IsDisposed { get; private set; }

	internal bool RegisteredInTree { get; private set; }

	internal bool ShapeDirty { get; private set; }

	public RigidTransform Transform { get; internal set; }

	public float Volume { get; internal set; }

	public int Priority { get; internal set; }

	public bool Spatialize { get; internal set; }

	public float SpatialBlend { get; internal set; }

	public float SpatializationStartDistnce { get; internal set; }

	public float SpatializationTransitionRange { get; internal set; }

	public float Pitch { get; internal set; } = 1f;

	public float DopplerStrength { get; internal set; }

	public bool IsCorrupted { get; internal set; }

	public IAudioDataSource Source { get; internal set; }

	public IAudioShape Shape { get; internal set; }

	public AudioInlet Inlet { get; internal set; }

	public HashSet<Listener> ExcludedListeners { get; internal set; }

	public AudioOutput(AudioSpace space)
	{
		Space = space;
		Transform = new RigidTransform(float3.Zero, floatQ.Identity);
	}

	public void Update(ChangesBatch batch, RigidTransform transform, float volume, int priority, bool spatialize, float spatialBlend, float spatializationStartDistance, float spatializationTransitionRange, float pitch, float dopplerStrength)
	{
		batch.Add<AudioOutputParametersChanges.Change, AudioOutputParametersChanges>(new AudioOutputParametersChanges.Change
		{
			output = this,
			transform = transform,
			volume = volume,
			priority = priority,
			spatialize = spatialize,
			spatialBlend = spatialBlend,
			spatializationStartDistance = spatializationStartDistance,
			spatializationTransitionRange = spatializationTransitionRange,
			pitch = pitch,
			dopplerStrength = dopplerStrength
		});
	}

	public void Update(ChangesBatch batch, IAudioDataSource source, IAudioShape shape, AudioInlet inlet)
	{
		if (shape.Output != this)
		{
			throw new ArgumentException("Shape must belong to this output");
		}
		if (inlet != null && inlet.Space != Space)
		{
			throw new ArgumentException("AudioInlet must belong to the same space");
		}
		batch.Add<AudioOutputReferencesChanges.Change, AudioOutputReferencesChanges>(new AudioOutputReferencesChanges.Change
		{
			output = this,
			source = source,
			shape = shape,
			inlet = inlet
		});
	}

	public void Update(ChangesBatch batch, HashSet<Listener> excludedListeners)
	{
		batch.Add<AudioOutputExcludedListenersChanges.Change, AudioOutputExcludedListenersChanges>(new AudioOutputExcludedListenersChanges.Change
		{
			output = this,
			excludedListeners = excludedListeners
		});
	}

	public bool IsVisibleToListener(Listener listener)
	{
		if (ExcludedListeners == null)
		{
			return true;
		}
		return !ExcludedListeners.Contains(listener);
	}

	internal void MarkShapeChanged()
	{
		if (!ShapeDirty && !IsDisposed)
		{
			Space.MarkAudioOutputShapeUpdated(this);
			ShapeDirty = true;
		}
	}

	internal void ClearShapeChanged()
	{
		ShapeDirty = false;
	}

	internal void MarkRegisteredInTree()
	{
		if (RegisteredInTree)
		{
			throw new InvalidOperationException("Output is already registered in the tree");
		}
		RegisteredInTree = true;
	}

	internal void UpdateContext(Listener listener)
	{
		if (!listenerContexts.TryGetValue(listener, out var value))
		{
			value = new AudioOutputListenerContext(this, listener);
			if (!listenerContexts.TryAdd(listener, value))
			{
				throw new Exception("Failed to add listener context");
			}
		}
		value.UpdateParameters();
	}

	internal void ProcessAndMix(AudioBuffer buffer, Listener listener, float volume)
	{
		if (!listenerContexts.TryGetValue(listener, out var value))
		{
			throw new Exception("Listener context does not exist");
		}
		value.ConsumeProcessAndMix(buffer, volume);
	}

	internal void ListenerRemoved(Listener listener)
	{
		if (listenerContexts.TryRemove(listener, out var value))
		{
			value.Dispose();
		}
	}

	internal void ClearSteamAudioData()
	{
		foreach (KeyValuePair<Listener, AudioOutputListenerContext> listenerContext in listenerContexts)
		{
			listenerContext.Value.ClearSteamAudioData();
		}
	}

	public void Dispose()
	{
		if (IsDisposed)
		{
			return;
		}
		foreach (KeyValuePair<Listener, AudioOutputListenerContext> listenerContext in listenerContexts)
		{
			listenerContext.Value.Dispose();
		}
		IsDisposed = true;
		RegisteredInTree = false;
	}
}
internal class AudioOutputListenerContext : IDisposable
{
	private float? lastDistance;

	private DateTime? lastTimestamp;

	private float lastVolume = -1f;

	private BinauralEffect binauralEffect;

	private AutoDucker<StereoSample> ducker = new AutoDucker<StereoSample>();

	private BufferPitcher<StereoSample> bufferPitcher;

	private bool pitchShiftActive;

	public AudioSimulator System => Output.System;

	public AudioSpace Space => Output.Space;

	public AudioOutput Output { get; private set; }

	public Listener Listener { get; private set; }

	public float Velocity { get; private set; }

	public float DopplerPitch { get; private set; }

	public float FinalPitch { get; private set; }

	public double DopplerTimeOffset { get; private set; }

	public AudioOutputListenerContext(AudioOutput output, Listener listener)
	{
		Output = output;
		Listener = listener;
	}

	public void UpdateParameters()
	{
		if (lastTimestamp != Space.LatestIntegratedChangeTimestamp)
		{
			RigidTransform transform = Listener.Transform;
			ref readonly float3 position = ref transform.position;
			RigidTransform transform2 = Output.Transform;
			float num = MathX.Distance(in position, in transform2.position);
			if (lastDistance.HasValue)
			{
				double totalSeconds = (Space.LatestIntegratedChangeTimestamp - lastTimestamp.Value).TotalSeconds;
				Velocity = (float)((double)(lastDistance.Value - num) / totalSeconds);
			}
			else
			{
				Velocity = 0f;
			}
			lastDistance = num;
			lastTimestamp = Space.LatestIntegratedChangeTimestamp;
		}
		DopplerPitch = 1f + Velocity * Space.InverseSpeedOfSound * Output.DopplerStrength;
		DopplerPitch = MathX.Max(0.125f, DopplerPitch);
		DopplerTimeOffset += System.DeltaTime - System.DeltaTime * (double)DopplerPitch;
		FinalPitch = DopplerPitch * Output.Pitch;
		FinalPitch = MathX.Clamp(FinalPitch, 0.01f, 100f);
	}

	public void ConsumeProcessAndMix(AudioBuffer buffer, float targetVolume)
	{
		bool flag = MathX.Abs(1f - FinalPitch) > 0.001f;
		if (flag || pitchShiftActive)
		{
			if (bufferPitcher == null)
			{
				bufferPitcher = new BufferPitcher<StereoSample>(System.SampleRate);
			}
			bufferPitcher.WindowSize = System.FrameSize;
			bufferPitcher.RelativeSpeed = FinalPitch;
			bool num = flag != pitchShiftActive;
			Span<StereoSample> buffer2 = buffer.Data.AsStereoBuffer();
			if (num)
			{
				Span<StereoSample> destination = stackalloc StereoSample[buffer2.Length];
				buffer2.CopyTo(destination);
				bufferPitcher.ApplyShift(buffer2);
				float num2 = 1f / (float)buffer2.Length;
				if (flag)
				{
					for (int i = 0; i < buffer2.Length; i++)
					{
						buffer2[i] = destination[i].LerpTo(buffer2[i], (float)i * num2);
					}
				}
				else
				{
					for (int j = 0; j < buffer2.Length; j++)
					{
						buffer2[j] = buffer2[j].LerpTo(destination[j], (float)j * num2);
					}
				}
			}
			else
			{
				bufferPitcher.ApplyShift(buffer2);
			}
			pitchShiftActive = flag;
		}
		if (Output.SpatialBlend > 0f && Output.Spatialize)
		{
			EnsureBinauralEffect();
			binauralEffect.Spatialize(buffer.Data);
		}
		if (lastVolume < 0f)
		{
			lastVolume = targetVolume;
		}
		float num3 = 1f / (float)buffer.Length;
		float num4 = 0f;
		for (int k = 0; k < buffer.Length; k++)
		{
			buffer.Data[k] *= MathX.LerpUnclamped(lastVolume, targetVolume, num4);
			num4 += num3;
		}
		lastVolume = targetVolume;
		BufferSanitizer.SanitizeBuffer(buffer.Data);
		ducker.AutoDuck(buffer.Data.AsStereoBuffer());
		DSP_Mixer mixer = Listener.GetMixer(Output.Inlet);
		if (mixer != null)
		{
			mixer.ConsumeAndMix(Listener, buffer);
		}
		else
		{
			Listener.ConsumeAndMix(buffer);
		}
	}

	private void EnsureBinauralEffect()
	{
		if (binauralEffect == null)
		{
			binauralEffect = System.SteamAudio.CreateBinauralEffect(this);
		}
	}

	public void Dispose()
	{
		ClearSteamAudioData();
	}

	public void ClearSteamAudioData()
	{
		binauralEffect?.Dispose();
		binauralEffect = null;
	}
}
[DataModelType]
[OldTypeName("FrooxEngine.AudioRolloffMode", "FrooxEngine")]
public enum AudioRolloffCurve
{
	LogarithmicInfinite = 0,
	LogarithmicClamped = 1,
	LogarithmicFadeOff = 2,
	Linear = 3,
	[Obsolete]
	Logarithmic = 0
}
public static class AudioRolloffHelper
{
	public static bool HasInfiniteRange(this AudioRolloffCurve curve)
	{
		switch (curve)
		{
		case AudioRolloffCurve.LogarithmicInfinite:
		case AudioRolloffCurve.LogarithmicClamped:
			return true;
		case AudioRolloffCurve.LogarithmicFadeOff:
		case AudioRolloffCurve.Linear:
			return false;
		default:
			throw new ArgumentException("Invalid rolloff curve: " + curve);
		}
	}

	public static float RawLogarithmicFalloff(float distance, float minDistance)
	{
		return minDistance / (minDistance + (distance - minDistance));
	}

	public static float ComputeAttenuation(this AudioRolloffCurve curve, float distance, float minDistance, float maxDistance)
	{
		switch (curve)
		{
		case AudioRolloffCurve.Linear:
		{
			float val3 = MathX.InverseLerp(minDistance, maxDistance, distance);
			val3 = MathX.Clamp01(val3);
			return 1f - val3;
		}
		case AudioRolloffCurve.LogarithmicFadeOff:
		{
			minDistance = MathX.Max(minDistance, 1E-06f);
			if (distance <= minDistance)
			{
				return 1f;
			}
			if (distance >= maxDistance)
			{
				return 0f;
			}
			float val = RawLogarithmicFalloff(distance, minDistance);
			float val2 = AudioRolloffCurve.Linear.ComputeAttenuation(distance, minDistance, maxDistance);
			return MathX.Min(val, val2);
		}
		case AudioRolloffCurve.LogarithmicClamped:
			minDistance = MathX.Max(minDistance, 1E-06f);
			if (distance <= minDistance)
			{
				return 1f;
			}
			distance = MathX.Min(maxDistance, distance);
			return RawLogarithmicFalloff(distance, minDistance);
		case AudioRolloffCurve.LogarithmicInfinite:
			minDistance = MathX.Max(minDistance, 1E-06f);
			if (distance <= minDistance)
			{
				return 1f;
			}
			return RawLogarithmicFalloff(distance, minDistance);
		default:
			return 0f;
		}
	}
}
public class GlobalAudioShape : IAudioShape
{
	public AudioOutput Output { get; private set; }

	public BoundingBox Bounds => BoundingBox.Infinite();

	public float ComputeAttenuation(RigidTransform listenerTransform)
	{
		return 1f;
	}

	public GlobalAudioShape(AudioOutput output)
	{
		Output = output;
	}
}
public class SphereAudioShape : IAudioShape
{
	public AudioOutput Output { get; private set; }

	public float MinDistance { get; internal set; }

	public float MaxDistance { get; internal set; }

	public AudioRolloffCurve Curve { get; internal set; }

	public BoundingBox Bounds
	{
		get
		{
			if (MaxDistance >= float.MaxValue || Curve.HasInfiniteRange())
			{
				return BoundingBox.Infinite();
			}
			RigidTransform transform = Output.Transform;
			return BoundingBox.CenterRadius(in transform.position, MaxDistance);
		}
	}

	public SphereAudioShape(AudioOutput output)
	{
		Output = output;
	}

	public float ComputeAttenuation(RigidTransform listenerTransform)
	{
		ref readonly float3 position = ref listenerTransform.position;
		RigidTransform transform = Output.Transform;
		(position - transform.position).GetNormalized(out var magnitude);
		return Curve.ComputeAttenuation(magnitude, MinDistance, MaxDistance);
	}

	public void Update(ChangesBatch batch, float minDistance, float maxDistance, AudioRolloffCurve curve)
	{
		batch.Add<SphereAudioShapeChanges.Change, SphereAudioShapeChanges>(new SphereAudioShapeChanges.Change
		{
			shape = this,
			minDistance = minDistance,
			maxDistance = maxDistance,
			curve = curve
		});
	}
}
public class AudioSimulator : IDisposable
{
	private enum AudioSpaceUpdateType
	{
		Added,
		Removed,
		Activated,
		Deactivated
	}

	private readonly struct AudioSpaceUpdate
	{
		public readonly AudioSpace space;

		public readonly AudioSpaceUpdateType update;

		public AudioSpaceUpdate(AudioSpace space, AudioSpaceUpdateType update)
		{
			this.space = space;
			this.update = update;
		}
	}

	private readonly struct ListenerDeviceBinding
	{
		public readonly Listener listener;

		public readonly AudioDeviceOutput device;

		public ListenerDeviceBinding(Listener listener, AudioDeviceOutput device)
		{
			this.listener = listener;
			this.device = device;
		}
	}

	private int _updateInProgress;

	private List<Action<AudioSimulator>> _preRenderParallelTasks = new List<Action<AudioSimulator>>();

	private Action onRenderFinished;

	private DateTime updateStartTime;

	private List<AudioSpace> spaces = new List<AudioSpace>();

	private WorkStealingTaskScheduler taskScheduler;

	private ConcurrentQueue<AudioSpaceUpdate> spaceUpdates = new ConcurrentQueue<AudioSpaceUpdate>();

	private ConcurrentQueue<ListenerDeviceBinding> listenerBindings = new ConcurrentQueue<ListenerDeviceBinding>();

	private int preRenderTaskCount;

	private ActionBlock<Action<AudioSimulator>> preRenderTasksProcessor;

	private ActionBlock<AudioSpace> audioSpaceUpdateProcessor;

	private ActionBlock<AudioRenderJob> renderJobProcessor;

	private ActionBlock<EmptyMixerJob> emptyMixerJobProcessor;

	private ActionBlock<AudioMixJob> mixJobProcessor;

	private KeyCounter<AudioDeviceOutput> deviceOutputListenerCounts = new KeyCounter<AudioDeviceOutput>();

	private int audioSpacesToUpdate;

	private int renderedListeners;

	private int renderedOutputs;

	private int renderedDSP_Effects;

	private int? newMaxActiveOutputs;

	private int? newFrameSize;

	public bool IsDisposed { get; private set; }

	public int SampleRate { get; private set; }

	public double InvSampleRate { get; private set; }

	public ChannelConfiguration ChannelConfiguration => ChannelConfiguration.Stereo;

	public int ChannelCount => ChannelConfiguration.ChannelCount();

	public int FrameSize { get; private set; }

	public int BufferSize => FrameSize * ChannelCount;

	public int MaxActiveOutputs { get; private set; }

	public double DeltaTime => (double)FrameSize * InvSampleRate;

	public double DSP_Time { get; private set; }

	public int AudioFrameIndex { get; private set; }

	public bool UpdateInProgress => _updateInProgress > 0;

	public float LastUpdateTime { get; private set; }

	public int LastActiveSpaces { get; private set; }

	public int LastRenderedListeners { get; private set; }

	public int LastRenderedOutputs { get; private set; }

	public int LastRenderedDSP_Effects { get; private set; }

	internal AudioBufferPool BufferPool { get; private set; }

	internal SteamAudioContext SteamAudio { get; private set; }

	public event Action<AudioSimulator> RenderStarted;

	public event Action<AudioSimulator> RenderFinished;

	public event Action<AudioSimulator> FrameSizeChanged;

	private void CheckDisposed()
	{
		if (IsDisposed)
		{
			throw new InvalidOperationException("AudioSimulator has been disposed");
		}
	}

	public AudioSimulator(int sampleRate, int bufferSampleCount = 1024, int maxActiveOutputs = 64)
	{
		SampleRate = sampleRate;
		InvSampleRate = 1.0 / (double)sampleRate;
		FrameSize = bufferSampleCount;
		MaxActiveOutputs = maxActiveOutputs;
		taskScheduler = new WorkStealingTaskScheduler(Environment.ProcessorCount, ThreadPriority.Highest, "Awwdio");
		preRenderTasksProcessor = new ActionBlock<Action<AudioSimulator>>((Action<Action<AudioSimulator>>)RunPreRenderTask, new ExecutionDataflowBlockOptions
		{
			MaxDegreeOfParallelism = -1,
			EnsureOrdered = false,
			TaskScheduler = taskScheduler
		});
		audioSpaceUpdateProcessor = new ActionBlock<AudioSpace>((Action<AudioSpace>)UpdateAudioSpace, new ExecutionDataflowBlockOptions
		{
			MaxDegreeOfParallelism = -1,
			EnsureOrdered = false,
			TaskScheduler = taskScheduler
		});
		renderJobProcessor = new ActionBlock<AudioRenderJob>((Action<AudioRenderJob>)ProcessRenderJob, new ExecutionDataflowBlockOptions
		{
			MaxDegreeOfParallelism = -1,
			EnsureOrdered = false,
			TaskScheduler = taskScheduler
		});
		emptyMixerJobProcessor = new ActionBlock<EmptyMixerJob>((Action<EmptyMixerJob>)ProcessEmptyMixerJob, new ExecutionDataflowBlockOptions
		{
			MaxDegreeOfParallelism = -1,
			EnsureOrdered = false,
			TaskScheduler = taskScheduler
		});
		mixJobProcessor = new ActionBlock<AudioMixJob>((Action<AudioMixJob>)ProcessMixJob, new ExecutionDataflowBlockOptions
		{
			MaxDegreeOfParallelism = -1,
			EnsureOrdered = false,
			TaskScheduler = taskScheduler
		});
		BufferPool = new AudioBufferPool(this);
		SteamAudio = new SteamAudioContext(this);
		SteamAudio.LoadHRTF();
	}

	public void RegisterPreRenderParallelTask(Action<AudioSimulator> action)
	{
		CheckDisposed();
		lock (_preRenderParallelTasks)
		{
			_preRenderParallelTasks.Add(action);
		}
	}

	public void UnregisterPreRenderParallelTask(Action<AudioSimulator> action)
	{
		CheckDisposed();
		lock (_preRenderParallelTasks)
		{
			_preRenderParallelTasks.Remove(action);
		}
	}

	public void RenderAudio(Action onRenderFinished = null)
	{
		if (!TryRenderAudio(onRenderFinished))
		{
			throw new InvalidOperationException("Cannot start another audio render while the current one is still running");
		}
	}

	public bool TryRenderAudio(Action onRenderFinished = null)
	{
		CheckDisposed();
		if (Interlocked.CompareExchange(ref _updateInProgress, 1, 0) != 0)
		{
			return false;
		}
		this.onRenderFinished = onRenderFinished;
		updateStartTime = DateTime.UtcNow;
		if (newMaxActiveOutputs.HasValue)
		{
			MaxActiveOutputs = newMaxActiveOutputs.Value;
			newMaxActiveOutputs = null;
		}
		if (newFrameSize.HasValue)
		{
			if (newFrameSize.Value != FrameSize)
			{
				FrameSize = newFrameSize.Value;
				HandleFrameSizeChange();
				this.FrameSizeChanged?.Invoke(this);
			}
			newFrameSize = null;
		}
		renderedOutputs = 0;
		renderedListeners = 0;
		renderedDSP_Effects = 0;
		this.RenderStarted?.Invoke(this);
		DSP_Time += DeltaTime;
		AudioFrameIndex++;
		lock (_preRenderParallelTasks)
		{
			if (_preRenderParallelTasks.Count == 0)
			{
				ScheduleRender();
			}
			else
			{
				preRenderTaskCount = _preRenderParallelTasks.Count;
				foreach (Action<AudioSimulator> preRenderParallelTask in _preRenderParallelTasks)
				{
					preRenderTasksProcessor.Post(preRenderParallelTask);
				}
			}
		}
		return true;
	}

	private void ScheduleRender()
	{
		ApplySpaceChanges();
		ApplyListenerChanges();
		PrepareAudioDeviceMixing();
		ScheduleAudioSpaceUpdates();
	}

	public void SetMaxActiveOutputs(int maxActiveOutputs)
	{
		if (maxActiveOutputs <= 0)
		{
			throw new ArgumentOutOfRangeException("maxActiveOutputs");
		}
		newMaxActiveOutputs = maxActiveOutputs;
	}

	public void SetFrameSize(int frameSize)
	{
		if (frameSize < 128 || frameSize > 32768)
		{
			throw new ArgumentOutOfRangeException("frameSize");
		}
		newFrameSize = frameSize;
	}

	public AudioSpace AddSpace(bool startActive)
	{
		AudioSpace audioSpace = new AudioSpace(this);
		spaceUpdates.Enqueue(new AudioSpaceUpdate(audioSpace, AudioSpaceUpdateType.Added));
		if (startActive)
		{
			ActivateSpace(audioSpace);
		}
		return audioSpace;
	}

	public void RemoveSpace(AudioSpace space)
	{
		if (space.System != this)
		{
			throw new InvalidOperationException("This space doesn't belong to this system");
		}
		spaceUpdates.Enqueue(new AudioSpaceUpdate(space, AudioSpaceUpdateType.Removed));
	}

	public void ActivateSpace(AudioSpace space)
	{
		if (space.System != this)
		{
			throw new InvalidOperationException("This space doesn't belong to this system");
		}
		spaceUpdates.Enqueue(new AudioSpaceUpdate(space, AudioSpaceUpdateType.Activated));
	}

	public void DeactivateSpace(AudioSpace space)
	{
		if (space.System != this)
		{
			throw new InvalidOperationException("This space doesn't belong to this system");
		}
		spaceUpdates.Enqueue(new AudioSpaceUpdate(space, AudioSpaceUpdateType.Deactivated));
	}

	private void ApplySpaceChanges()
	{
		AudioSpaceUpdate result;
		while (spaceUpdates.TryDequeue(out result))
		{
			switch (result.update)
			{
			case AudioSpaceUpdateType.Added:
				spaces.Add(result.space);
				break;
			case AudioSpaceUpdateType.Removed:
				spaces.Remove(result.space);
				break;
			case AudioSpaceUpdateType.Activated:
				result.space.IsActive = true;
				break;
			case AudioSpaceUpdateType.Deactivated:
				result.space.IsActive = false;
				break;
			default:
				throw new NotImplementedException("Unsupported update type: " + result.update);
			}
		}
	}

	private void ApplyListenerChanges()
	{
		foreach (AudioSpace space in spaces)
		{
			space.ApplyListenerChanges();
		}
		ListenerDeviceBinding result;
		while (listenerBindings.TryDequeue(out result))
		{
			result.listener.BoundDevice = result.device;
		}
	}

	private void PrepareAudioDeviceMixing()
	{
		foreach (AudioSpace space in spaces)
		{
			if (!space.IsActive)
			{
				continue;
			}
			foreach (Listener listener in space.Listeners)
			{
				if (listener.BoundDevice != null)
				{
					deviceOutputListenerCounts.Increment(listener.BoundDevice);
				}
			}
		}
		foreach (KeyValuePair<AudioDeviceOutput, int> deviceOutputListenerCount in deviceOutputListenerCounts)
		{
			deviceOutputListenerCount.Key.PrepareMixing(deviceOutputListenerCount.Value);
		}
		deviceOutputListenerCounts.Clear();
	}

	private void ScheduleAudioSpaceUpdates()
	{
		audioSpacesToUpdate = spaces.Count((AudioSpace s) => s.IsActive);
		if (audioSpacesToUpdate == 0)
		{
			FinishUpdate();
			return;
		}
		foreach (AudioSpace space in spaces)
		{
			if (space.IsActive)
			{
				audioSpaceUpdateProcessor.Post(space);
			}
		}
	}

	private void FinishUpdate()
	{
		LastUpdateTime = (float)(DateTime.UtcNow - updateStartTime).TotalSeconds;
		LastActiveSpaces = spaces.Count((AudioSpace s) => s.IsActive);
		LastRenderedListeners = renderedListeners;
		LastRenderedOutputs = renderedOutputs;
		LastRenderedDSP_Effects = renderedDSP_Effects;
		_updateInProgress = 0;
		this.RenderFinished?.Invoke(this);
		Action action = onRenderFinished;
		onRenderFinished = null;
		action?.Invoke();
	}

	internal void AudioSpaceUpdateFinished(AudioSpace space, int listenerCount, int outputCount, int dspEffectCount)
	{
		Interlocked.Add(ref renderedOutputs, outputCount);
		Interlocked.Add(ref renderedListeners, listenerCount);
		Interlocked.Add(ref renderedDSP_Effects, dspEffectCount);
		if (Interlocked.Decrement(ref audioSpacesToUpdate) <= 0)
		{
			FinishUpdate();
		}
	}

	internal void EnqueueListenerBinding(Listener listener, AudioDeviceOutput output)
	{
		listenerBindings.Enqueue(new ListenerDeviceBinding(listener, output));
	}

	internal void EnqueueRenderJob(AudioRenderJob job)
	{
		renderJobProcessor.Post(job);
	}

	internal void EnqueueEmptyMixerJob(EmptyMixerJob job)
	{
		emptyMixerJobProcessor.Post(job);
	}

	internal void EnqueueMixJob(AudioMixJob job)
	{
		mixJobProcessor.Post(job);
	}

	private void RunPreRenderTask(Action<AudioSimulator> task)
	{
		task(this);
		if (Interlocked.Decrement(ref preRenderTaskCount) == 0)
		{
			ScheduleRender();
		}
	}

	private void UpdateAudioSpace(AudioSpace audioSpace)
	{
		try
		{
			audioSpace.Render();
		}
		catch (Exception ex)
		{
			UniLog.Error("Exception rendering audio space:\n" + ex);
		}
	}

	private void ProcessRenderJob(AudioRenderJob job)
	{
		AudioBuffer audioBuffer = null;
		try
		{
			foreach (ListenerRenderData listener in job.listeners)
			{
				job.output.UpdateContext(listener.listener);
			}
			audioBuffer = BufferPool.BorrowBuffer();
			job.output.Source.Read(audioBuffer.Data.AsSpan().AsStereoBuffer(), this);
		}
		catch (Exception ex)
		{
			UniLog.Error($"Exception in render job. Output: {job.output}\nListeners: {job.listeners.Count}\n" + ex);
			audioBuffer?.Clear();
			job.output.IsCorrupted = true;
		}
		try
		{
			if (audioBuffer == null)
			{
				audioBuffer = BufferPool.BorrowBuffer();
				audioBuffer.Clear();
			}
			ScheduleMix(job, audioBuffer);
		}
		catch (Exception ex2)
		{
			UniLog.Error($"Exception in render job. Output: {job.output}\nListeners: {job.listeners.Count}\n" + ex2);
		}
	}

	private void ProcessEmptyMixerJob(EmptyMixerJob job)
	{
		try
		{
			AudioBuffer audioBuffer = BufferPool.BorrowBuffer();
			audioBuffer.Clear();
			job.mixer.ConsumeAndMix(job.listener, audioBuffer);
		}
		catch (Exception ex)
		{
			UniLog.Error("Exception in empty mixer job:\n" + ex);
		}
	}

	private void ScheduleMix(AudioRenderJob job, AudioBuffer buffer)
	{
		for (int i = 0; i < job.listeners.Count; i++)
		{
			AudioBuffer audioBuffer;
			if (i == job.listeners.Count - 1)
			{
				audioBuffer = buffer;
			}
			else
			{
				audioBuffer = BufferPool.BorrowBuffer();
				Array.Copy(buffer.Data, audioBuffer.Data, buffer.Length);
			}
			AudioMixJob job2 = new AudioMixJob
			{
				output = job.output,
				renderData = job.listeners[i],
				buffer = audioBuffer
			};
			EnqueueMixJob(job2);
		}
	}

	private void ProcessMixJob(AudioMixJob job)
	{
		try
		{
			job.output.ProcessAndMix(job.buffer, job.renderData.listener, job.renderData.volume);
		}
		catch (Exception ex)
		{
			UniLog.Error("Exception in mix job:\n" + ex);
		}
	}

	private void HandleFrameSizeChange()
	{
		foreach (AudioSpace space in spaces)
		{
			space.ClearSteamAudioData();
		}
		SteamAudio.FrameSizeChanged();
	}

	public void Dispose()
	{
		CheckDisposed();
		preRenderTasksProcessor.Complete();
		audioSpaceUpdateProcessor.Complete();
		renderJobProcessor.Complete();
		emptyMixerJobProcessor.Complete();
		mixJobProcessor.Complete();
		taskScheduler.Dispose();
		IsDisposed = true;
	}
}
public class AudioSpace
{
	private readonly struct AudioOutputUpdate
	{
		public readonly AudioOutput output;

		public readonly bool added;

		public AudioOutputUpdate(AudioOutput output, bool added)
		{
			this.output = output;
			this.added = added;
		}
	}

	private readonly struct ListenerUpdate
	{
		public readonly Listener listener;

		public readonly bool added;

		public ListenerUpdate(Listener listener, bool added)
		{
			this.listener = listener;
			this.added = added;
		}
	}

	private readonly struct AudioInletUpdate
	{
		public readonly AudioInlet inlet;

		public readonly bool added;

		public AudioInletUpdate(AudioInlet inlet, bool added)
		{
			this.inlet = inlet;
			this.added = added;
		}
	}

	private struct AudioOutputCandidate : IComparable<AudioOutputCandidate>
	{
		public AudioOutput output;

		public float volume;

		public int CompareTo(AudioOutputCandidate other)
		{
			int num = output.Priority.CompareTo(other.output.Priority);
			if (num != 0)
			{
				return num;
			}
			return -volume.CompareTo(other.volume);
		}
	}

	private readonly AudioInlet defaultInlet;

	private List<Listener> listeners = new List<Listener>();

	private List<AudioOutput> outputs = new List<AudioOutput>();

	private List<AudioInlet> inlets = new List<AudioInlet>();

	private ConcurrentQueue<AudioOutputUpdate> audioOutputUpdates = new ConcurrentQueue<AudioOutputUpdate>();

	private ConcurrentQueue<ListenerUpdate> listenerUpdates = new ConcurrentQueue<ListenerUpdate>();

	private ConcurrentQueue<AudioInletUpdate> inletUpdates = new ConcurrentQueue<AudioInletUpdate>();

	private ConcurrentQueue<ChangesBatch> queuedChanges = new ConcurrentQueue<ChangesBatch>();

	private SpatialCollection3D<AudioOutput> outputsTree = new SpatialCollection3D<AudioOutput>();

	private List<AudioOutput> updateAudioOutputShapes = new List<AudioOutput>();

	private int listenersToUpdate;

	private int outputsToRender;

	private int effectsToRender;

	public bool IsActive { get; internal set; }

	public AudioSimulator System { get; private set; }

	public float SpeedOfSound => 343f;

	public float InverseSpeedOfSound => 1f / SpeedOfSound;

	public DateTime LatestIntegratedChangeTimestamp { get; private set; }

	internal IReadOnlyList<Listener> Listeners => listeners;

	internal AudioSpace(AudioSimulator system)
	{
		System = system;
		defaultInlet = new AudioInlet(this);
	}

	public AudioOutput AddOutput()
	{
		AudioOutput audioOutput = new AudioOutput(this);
		audioOutputUpdates.Enqueue(new AudioOutputUpdate(audioOutput, added: true));
		return audioOutput;
	}

	public void RemoveOutput(AudioOutput output)
	{
		if (output.Space != this)
		{
			throw new ArgumentException("AudioOutput doesn't belong to this space");
		}
		audioOutputUpdates.Enqueue(new AudioOutputUpdate(output, added: false));
	}

	public Listener AddListener()
	{
		Listener listener = new Listener(this);
		listenerUpdates.Enqueue(new ListenerUpdate(listener, added: true));
		return listener;
	}

	public void RemoveListener(Listener listener)
	{
		if (listener.Space != this)
		{
			throw new ArgumentException("Listener doesn't belong to this space");
		}
		listenerUpdates.Enqueue(new ListenerUpdate(listener, added: false));
	}

	public AudioInlet AddAudioInlet()
	{
		AudioInlet audioInlet = new AudioInlet(this);
		inletUpdates.Enqueue(new AudioInletUpdate(audioInlet, added: true));
		return audioInlet;
	}

	public void RemoveAudioInlet(AudioInlet inlet)
	{
		if (inlet.Space != this)
		{
			throw new ArgumentException("AudioInlet doesn't belong to this space");
		}
		inletUpdates.Enqueue(new AudioInletUpdate(inlet, added: false));
	}

	public ChangesBatch BeginChangesBatch()
	{
		ChangesBatch changesBatch = new ChangesBatch(this);
		changesBatch.BeginRecording();
		return changesBatch;
	}

	public void FinishChangesBatch(ChangesBatch batch)
	{
		if (batch.Space != this)
		{
			throw new ArgumentException("Changes batch does not belong to this space");
		}
		batch.FinishRecording();
		queuedChanges.Enqueue(batch);
	}

	internal void MarkAudioOutputShapeUpdated(AudioOutput output)
	{
		updateAudioOutputShapes.Add(output);
	}

	internal void Render()
	{
		ApplyChanges();
		CollectAndScheduleAudioOutputs();
	}

	internal void ApplyListenerChanges()
	{
		ListenerUpdate result;
		while (listenerUpdates.TryDequeue(out result))
		{
			if (result.added)
			{
				listeners.Add(result.listener);
				continue;
			}
			listeners.Remove(result.listener);
			foreach (AudioOutput output in outputs)
			{
				output.ListenerRemoved(result.listener);
			}
		}
	}

	private void ApplyChanges()
	{
		AudioOutputUpdate result;
		while (audioOutputUpdates.TryDequeue(out result))
		{
			if (result.added)
			{
				outputs.Add(result.output);
				continue;
			}
			outputs.Remove(result.output);
			outputsTree.Remove(result.output);
			result.output.Dispose();
		}
		ChangesBatch result2;
		while (queuedChanges.TryDequeue(out result2))
		{
			result2.Submit();
			LatestIntegratedChangeTimestamp = result2.CaptureTimestamp;
		}
		AudioInletUpdate result3;
		while (inletUpdates.TryDequeue(out result3))
		{
			if (result3.added)
			{
				inlets.Add(result3.inlet);
			}
			else
			{
				if (!inlets.Remove(result3.inlet))
				{
					continue;
				}
				foreach (AudioOutput output in outputs)
				{
					if (output.Inlet == result3.inlet)
					{
						output.Inlet = null;
					}
				}
				foreach (Listener listener in listeners)
				{
					listener.RemoveMixerMapping(result3.inlet);
				}
			}
		}
		UpdateSpatialTree();
	}

	private void UpdateSpatialTree()
	{
		if (updateAudioOutputShapes.Count == 0)
		{
			return;
		}
		foreach (AudioOutput updateAudioOutputShape in updateAudioOutputShapes)
		{
			BoundingBox bounds = updateAudioOutputShape.Shape.Bounds;
			if (updateAudioOutputShape.RegisteredInTree)
			{
				outputsTree.UpdateBounds(updateAudioOutputShape, bounds);
			}
			else
			{
				updateAudioOutputShape.MarkRegisteredInTree();
				outputsTree.Add(updateAudioOutputShape, bounds);
			}
			updateAudioOutputShape.ClearShapeChanged();
		}
		outputsTree.Refit();
		updateAudioOutputShapes.Clear();
	}

	private void CollectAndScheduleAudioOutputs()
	{
		List<AudioOutput> list = Pool.BorrowList<AudioOutput>();
		List<List<AudioOutputCandidate>> list2 = Pool.BorrowList<List<AudioOutputCandidate>>();
		listenersToUpdate = listeners.Count;
		if (listenersToUpdate == 0)
		{
			FinishUpdate();
			return;
		}
		foreach (Listener listener2 in listeners)
		{
			RigidTransform transform = listener2.Transform;
			BoundingBox bounds = new BoundingBox(in transform.position);
			outputsTree.GetOverlaps(bounds, list);
			list.RemoveAll((AudioOutput o) => !o.IsValid || !o.Source.IsActive);
			List<AudioOutputCandidate> list3 = Pool.BorrowList<AudioOutputCandidate>();
			foreach (AudioOutput item in list)
			{
				if (item.IsVisibleToListener(listener2))
				{
					float b = item.Shape.ComputeAttenuation(listener2.Transform);
					b = MathX.Lerp(1f, b, item.SpatialBlend);
					float num = item.Volume * b;
					if (!(num <= 0f))
					{
						list3.Add(new AudioOutputCandidate
						{
							output = item,
							volume = num
						});
					}
				}
			}
			if (list3.Count > System.MaxActiveOutputs)
			{
				list3.Sort();
				list3.EnsureMaxCount(System.MaxActiveOutputs);
			}
			list2.Add(list3);
			list.Clear();
		}
		Pool.Return(ref list);
		Dictionary<AudioOutput, AudioRenderJob> dictionary = Pool.BorrowDictionary<AudioOutput, AudioRenderJob>();
		List<EmptyMixerJob> list4 = Pool.BorrowList<EmptyMixerJob>();
		effectsToRender = 0;
		for (int num2 = 0; num2 < listeners.Count; num2++)
		{
			Listener listener = listeners[num2];
			List<AudioOutputCandidate> list5 = list2[num2];
			foreach (AudioOutputCandidate item2 in list5)
			{
				if (!dictionary.TryGetValue(item2.output, out var value))
				{
					value = new AudioRenderJob
					{
						output = item2.output
					};
				}
				value.listeners.Add(new ListenerRenderData(listener, item2.volume));
				dictionary[item2.output] = value;
			}
			KeyCounter<AudioInlet> counter = Pool.BorrowKeyCounter<AudioInlet>();
			foreach (AudioOutputCandidate item3 in list5)
			{
				counter.Increment(item3.output.Inlet ?? defaultInlet);
			}
			int num3 = 0;
			KeyCounter<DSP_Mixer> counter2 = Pool.BorrowKeyCounter<DSP_Mixer>();
			foreach (KeyValuePair<AudioInlet, DSP_Mixer> mixerMapping in listener.MixerMappings)
			{
				int amount = counter.Take(mixerMapping.Key);
				counter2.Add(mixerMapping.Value, amount);
				DSP_Mixer next = mixerMapping.Value.Next;
				effectsToRender++;
				while (next != null)
				{
					effectsToRender++;
					counter2.Increment(next);
					listener.RegisterActiveMixer(next);
					next = next.Next;
				}
				num3++;
				listener.RegisterActiveMixer(mixerMapping.Value);
			}
			foreach (KeyValuePair<AudioInlet, DSP_Mixer> mixerMapping2 in listener.MixerMappings)
			{
				int num4 = counter2.Take(mixerMapping2.Value);
				if (num4 == 0)
				{
					list4.Add(new EmptyMixerJob
					{
						listener = listener,
						mixer = mixerMapping2.Value
					});
					num4 = 1;
				}
				mixerMapping2.Value.PrepareMixing(listener, num4);
			}
			foreach (KeyValuePair<DSP_Mixer, int> item4 in counter2)
			{
				item4.Key.PrepareMixing(listener, item4.Value);
			}
			Pool.Return(ref counter2);
			num3 += counter.Sum;
			Pool.Return(ref counter);
			listener.CleanupOldMixers();
			listener.PrepareMixing(num3);
		}
		foreach (List<AudioOutputCandidate> item5 in list2)
		{
			Pool.ReturnUnsafe(item5);
		}
		Pool.Return(ref list2);
		outputsToRender = dictionary.Count;
		foreach (KeyValuePair<AudioOutput, AudioRenderJob> item6 in dictionary)
		{
			System.EnqueueRenderJob(item6.Value);
		}
		Pool.Return(ref dictionary);
		foreach (EmptyMixerJob item7 in list4)
		{
			System.EnqueueEmptyMixerJob(item7);
		}
		Pool.Return(ref list4);
	}

	internal void ListenerFinished()
	{
		if (Interlocked.Decrement(ref listenersToUpdate) <= 0)
		{
			FinishUpdate();
		}
	}

	private void FinishUpdate()
	{
		foreach (Listener listener in listeners)
		{
			listener.CommitStagedBuffer();
		}
		System.AudioSpaceUpdateFinished(this, listeners.Count, outputsToRender, effectsToRender);
	}

	internal void ClearSteamAudioData()
	{
		foreach (AudioOutput output in outputs)
		{
			output.ClearSteamAudioData();
		}
	}
}
public class AudioThreadLoopUpdater
{
	private Thread thread;

	private bool run;

	public AudioSimulator Simulator { get; private set; }

	public int DroppedAudioFrames { get; private set; }

	public bool Supress { get; set; }

	public AudioThreadLoopUpdater(AudioSimulator simulator)
	{
		Simulator = simulator;
	}

	public void StartThread()
	{
		if (thread != null)
		{
			throw new InvalidOperationException("Thread is already running");
		}
		run = true;
		thread = new Thread(RunUpdate);
		thread.Priority = ThreadPriority.Highest;
		thread.IsBackground = true;
		thread.Start();
	}

	public void StopThread()
	{
		run = false;
	}

	private void RunUpdate()
	{
		try
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			double num = 0.0;
			while (run)
			{
				_ = DateTime.UtcNow;
				if (!Supress && !Simulator.TryRenderAudio())
				{
					DroppedAudioFrames++;
				}
				double totalSeconds = stopwatch.Elapsed.TotalSeconds;
				num += Simulator.DeltaTime;
				double val = num - totalSeconds;
				val = MathX.Max(0.0, val);
				Thread.Sleep((int)(val * 1000.0));
			}
			thread = null;
		}
		catch (Exception ex)
		{
			UniLog.Error("Fatal error when updating audio: " + ex);
		}
	}
}
public class AudioOutputParametersChanges : ChangesBuffer<AudioOutputParametersChanges.Change>
{
	public struct Change
	{
		public AudioOutput output;

		public RigidTransform transform;

		public float volume;

		public int priority;

		public bool spatialize;

		public float spatialBlend;

		public float spatializationStartDistance;

		public float spatializationTransitionRange;

		public float pitch;

		public float dopplerStrength;
	}

	protected override void Apply(Change change)
	{
		if (change.transform != change.output.Transform)
		{
			change.output.MarkShapeChanged();
			change.output.Transform = change.transform;
		}
		change.output.Volume = change.volume;
		change.output.Priority = change.priority;
		change.output.Spatialize = change.spatialize;
		change.output.SpatialBlend = change.spatialBlend;
		change.output.SpatializationStartDistnce = change.spatializationStartDistance;
		change.output.SpatializationTransitionRange = change.spatializationTransitionRange;
		change.output.Pitch = change.pitch;
		change.output.DopplerStrength = change.dopplerStrength;
	}
}
public class AudioOutputReferencesChanges : ChangesBuffer<AudioOutputReferencesChanges.Change>
{
	public struct Change
	{
		public AudioOutput output;

		public IAudioDataSource source;

		public IAudioShape shape;

		public AudioInlet inlet;
	}

	protected override void Apply(Change change)
	{
		if (change.output.Shape != change.shape)
		{
			change.output.MarkShapeChanged();
		}
		change.output.Source = change.source;
		change.output.Shape = change.shape;
		change.output.Inlet = change.inlet;
	}
}
public class AudioOutputExcludedListenersChanges : ChangesBuffer<AudioOutputExcludedListenersChanges.Change>
{
	public struct Change
	{
		public AudioOutput output;

		public HashSet<Listener> excludedListeners;
	}

	protected override void Apply(Change change)
	{
		change.output.ExcludedListeners = change.excludedListeners;
	}
}
public class ChangesBatch
{
	private Dictionary<Type, ChangesBuffer> changes = new Dictionary<Type, ChangesBuffer>();

	public AudioSpace Space { get; private set; }

	public bool IsRecording { get; private set; }

	public DateTime CaptureTimestamp { get; private set; }

	public ChangesBatch(AudioSpace space)
	{
		Space = space;
	}

	internal void BeginRecording()
	{
		if (IsRecording)
		{
			throw new InvalidOperationException("Batch is already recording changes");
		}
		IsRecording = true;
		CaptureTimestamp = DateTime.UtcNow;
	}

	internal void FinishRecording()
	{
		if (!IsRecording)
		{
			throw new InvalidOperationException("Batch is not recording changes");
		}
		IsRecording = false;
	}

	public B GetChangesBuffer<B, T>() where B : ChangesBuffer<T>, new() where T : struct
	{
		B val;
		if (!changes.TryGetValue(typeof(B), out var value))
		{
			val = new B();
			val.Initialize(Space);
			changes.Add(typeof(B), val);
		}
		else
		{
			val = (B)value;
		}
		return val;
	}

	public void Add<T, B>(T change) where T : struct where B : ChangesBuffer<T>, new()
	{
		if (!IsRecording)
		{
			throw new InvalidOperationException("The changes batch isn't recording chagnes");
		}
		GetChangesBuffer<B, T>().Add(change);
	}

	public void Submit()
	{
		if (IsRecording)
		{
			throw new InvalidOperationException("This batch is recording changes, it cannot be submitted");
		}
		foreach (KeyValuePair<Type, ChangesBuffer> change in changes)
		{
			change.Value.Submit();
		}
	}
}
public abstract class ChangesBuffer
{
	public AudioSpace Space { get; private set; }

	public abstract void Submit();

	internal void Initialize(AudioSpace space)
	{
		if (space == null)
		{
			throw new ArgumentNullException("space");
		}
		if (Space != null)
		{
			throw new InvalidOperationException("This buffer has already been initialized");
		}
		Space = space;
	}
}
public abstract class ChangesBuffer<T> : ChangesBuffer where T : struct
{
	private List<T> changes = new List<T>();

	public void Add(T change)
	{
		changes.Add(change);
	}

	public override void Submit()
	{
		foreach (T change in changes)
		{
			Apply(change);
		}
		changes.Clear();
	}

	protected abstract void Apply(T change);
}
public class DSP_MixerChanges : ChangesBuffer<DSP_MixerChanges.Change>
{
	public struct Change
	{
		public DSP_Mixer mixer;

		public DSP_Mixer next;
	}

	protected override void Apply(Change change)
	{
		change.mixer.Next = change.next;
	}
}
public class FilterBlendWeightChanges : ChangesBuffer<FilterBlendWeightChanges.Change>
{
	public struct Change
	{
		public FilterBlendWrapper filter;

		public float blendWeight;
	}

	protected override void Apply(Change change)
	{
		change.filter.BlendWeight = change.blendWeight;
	}
}
public class FilterBlendEnabledChanges : ChangesBuffer<FilterBlendEnabledChanges.Change>
{
	public struct Change
	{
		public FilterBlendWrapper filter;

		public bool enabled;
	}

	protected override void Apply(Change change)
	{
		change.filter.Enabled = change.enabled;
	}
}
public class FilterBlendFilterChanges : ChangesBuffer<FilterBlendFilterChanges.Change>
{
	public struct Change
	{
		public FilterBlendWrapper filter;

		public IAudioDSP_Filter nestedFilter;
	}

	protected override void Apply(Change change)
	{
		change.filter.SetFilter(change.nestedFilter);
	}
}
public class ListenerChanges : ChangesBuffer<ListenerChanges.Change>
{
	public struct Change
	{
		public Listener listener;

		public RigidTransform transform;
	}

	protected override void Apply(Change change)
	{
		change.listener.Transform = change.transform;
	}
}
public class ListenerMixerChanges : ChangesBuffer<ListenerMixerChanges.Change>
{
	public struct Change
	{
		public Listener listener;

		public AudioInlet inlet;

		public DSP_Mixer mixer;
	}

	protected override void Apply(Change change)
	{
		if (change.mixer == null)
		{
			change.listener.RemoveMixerMapping(change.inlet);
		}
		else
		{
			change.listener.SetMixerMapping(change.inlet, change.mixer);
		}
	}
}
public class SphereAudioShapeChanges : ChangesBuffer<SphereAudioShapeChanges.Change>
{
	public struct Change
	{
		public SphereAudioShape shape;

		public float minDistance;

		public float maxDistance;

		public AudioRolloffCurve curve;
	}

	protected override void Apply(Change change)
	{
		change.shape.MinDistance = change.minDistance;
		change.shape.MaxDistance = change.maxDistance;
		change.shape.Curve = change.curve;
		change.shape.Output.MarkShapeChanged();
	}
}
public class ZitaReverbChanges : ChangesBuffer<ZitaReverbChanges.Change>
{
	public struct Change
	{
		public ZitaReverbFilter filter;

		public ZitaParameters parameters;
	}

	protected override void Apply(Change change)
	{
		change.filter.Parameters = change.parameters;
	}
}
public class ZitaReverbEnabledChanges : ChangesBuffer<ZitaReverbEnabledChanges.Change>
{
	public struct Change
	{
		public ZitaReverbFilter filter;

		public bool enabled;
	}

	protected override void Apply(Change change)
	{
		change.filter.Enabled = change.enabled;
	}
}
public class ConcurrentBufferMixer
{
	private AudioBuffer _resultBuffer;

	public AudioSimulator Simulator { get; private set; }

	public int Size { get; private set; }

	public ConcurrentBufferMixer(AudioSimulator simulator, int size)
	{
		Simulator = simulator;
		Size = size;
	}

	public AudioBuffer RetrieveResult()
	{
		return Interlocked.Exchange(ref _resultBuffer, null);
	}

	public void ConsumeAndMix(AudioBuffer buffer)
	{
		if (buffer.Length != Size)
		{
			throw new ArgumentException("Expected buffer of size " + Size);
		}
		do
		{
			AudioBuffer audioBuffer = Interlocked.Exchange(ref _resultBuffer, null);
			if (audioBuffer != null)
			{
				for (int i = 0; i < buffer.Length; i++)
				{
					buffer.Data[i] += audioBuffer.Data[i];
				}
				Simulator.BufferPool.ReturnBuffer(audioBuffer);
			}
			buffer = Interlocked.Exchange(ref _resultBuffer, buffer);
		}
		while (buffer != null);
	}
}
public class DebugModulatedSineAudioSource : IAudioDataSource
{
	public float MinFrequency = 100f;

	public float MaxFrequency = 200f;

	public float ModulateFrequency = 1f;

	private double _phi;

	private double _phiModulate;

	public bool IsActive => true;

	public int ChannelCount => 1;

	public DebugModulatedSineAudioSource(int minFreq = 100, int maxFreq = 200, int modulateFreq = 1)
	{
		MinFrequency = minFreq;
		MaxFrequency = maxFreq;
		ModulateFrequency = modulateFreq;
	}

	public void Read<S>(Span<S> buffer, AudioSimulator system) where S : unmanaged, IAudioSample<S>
	{
		Span<float> span = buffer.AsSampleBuffer();
		int channelCount = default(S).ChannelCount;
		float num = (float)Math.PI * 2f * ModulateFrequency / (float)system.SampleRate;
		for (int i = 0; i < buffer.Length; i++)
		{
			float value = (float)MathX.Sin(_phiModulate);
			_phiModulate += num;
			float num2 = MathX.LerpUnclamped(MinFrequency, MaxFrequency, MathX.Remap11_01(value));
			float num3 = (float)MathX.Sin(_phi);
			int num4 = channelCount * i;
			for (int j = 0; j < channelCount; j++)
			{
				span[num4 + j] = num3;
			}
			float num5 = (float)Math.PI * 2f * num2 / (float)system.SampleRate;
			_phi += num5;
		}
	}
}
public class DebugSimplexAudioSource : IAudioDataSource
{
	public int Frequency = 440;

	private double _phi;

	public bool IsActive => true;

	public int ChannelCount => 1;

	public DebugSimplexAudioSource(int frequency = 440)
	{
		Frequency = frequency;
	}

	public void Read<S>(Span<S> buffer, AudioSimulator system) where S : unmanaged, IAudioSample<S>
	{
		Span<float> span = buffer.AsSampleBuffer();
		int channelCount = default(S).ChannelCount;
		double num = (double)Frequency / (double)system.SampleRate;
		for (int i = 0; i < buffer.Length; i++)
		{
			float num2 = MathX.SimplexNoise((float)_phi);
			int num3 = channelCount * i;
			for (int j = 0; j < channelCount; j++)
			{
				span[num3 + j] = num2;
			}
			_phi += num;
		}
	}
}
public class DebugSineAudioSource : IAudioDataSource
{
	public float Frequency = 440f;

	private double _phi;

	public bool IsActive => true;

	public int ChannelCount => 1;

	public DebugSineAudioSource(float frequency = 440f)
	{
		Frequency = frequency;
	}

	public void Read<S>(Span<S> buffer, AudioSimulator system) where S : unmanaged, IAudioSample<S>
	{
		Span<float> span = buffer.AsSampleBuffer();
		int channelCount = default(S).ChannelCount;
		float num = (float)Math.PI * 2f * Frequency / (float)system.SampleRate;
		for (int i = 0; i < buffer.Length; i++)
		{
			float num2 = (float)MathX.Sin(_phi);
			int num3 = channelCount * i;
			for (int j = 0; j < channelCount; j++)
			{
				span[num3 + j] = num2;
			}
			_phi += num;
		}
	}
}
internal class DSP_Context : Mixer, IDisposable
{
	public Listener Listener { get; private set; }

	public DSP_Mixer Mixer { get; private set; }

	public IAudioDSP_FilterContext FilterContext { get; private set; }

	public DSP_Context(Listener listener, DSP_Mixer mixer, IAudioDSP_FilterContext filterInstance)
		: base(listener.Simulator)
	{
		Listener = listener;
		Mixer = mixer;
		FilterContext = filterInstance;
	}

	protected override void FinishMix(AudioBuffer result)
	{
		try
		{
			FilterContext.Process(result);
		}
		catch (Exception ex)
		{
			UniLog.Error("Exception running DSP processing effect:\n" + ex, stackTrace: false);
		}
		if (Mixer.Next != null)
		{
			Mixer.Next.ConsumeAndMix(Listener, result);
		}
		else
		{
			Listener.ConsumeAndMix(result);
		}
	}

	public void Dispose()
	{
		FilterContext.Dispose();
	}
}
public class DSP_Mixer
{
	private Dictionary<Listener, DSP_Context> contexts = new Dictionary<Listener, DSP_Context>();

	public AudioSpace Space { get; private set; }

	public DSP_Mixer Next { get; internal set; }

	public IAudioDSP_Filter Filter { get; private set; }

	public DSP_Mixer(AudioSpace space, IAudioDSP_Filter filter)
	{
		Space = space;
		Filter = filter;
	}

	public void SetNext(ChangesBatch batch, DSP_Mixer next)
	{
		batch.Add<DSP_MixerChanges.Change, DSP_MixerChanges>(new DSP_MixerChanges.Change
		{
			mixer = this,
			next = next
		});
	}

	internal void PrepareMixing(Listener listener, int count)
	{
		if (!contexts.TryGetValue(listener, out var value))
		{
			IAudioDSP_FilterContext filterInstance = Filter.CreateContext();
			value = new DSP_Context(listener, this, filterInstance);
			contexts.Add(listener, value);
		}
		value.PrepareMixing(count);
	}

	internal void RemoveContext(Listener listener)
	{
		if (!contexts.TryGetValue(listener, out var value))
		{
			throw new InvalidOperationException("There's no context for given listener");
		}
		contexts.Remove(listener);
		value.Dispose();
	}

	internal void ConsumeAndMix(Listener listener, AudioBuffer buffer)
	{
		if (!contexts.TryGetValue(listener, out var value))
		{
			throw new InvalidOperationException("There's no context for given listener");
		}
		value.ConsumeAndMix(buffer);
	}
}
public class FilterBlendWrapper : IAudioDSP_Filter
{
	private class Context : IAudioDSP_FilterContext, IDisposable
	{
		private readonly FilterBlendWrapper wrapper;

		private IAudioDSP_Filter filter;

		private IAudioDSP_FilterContext context;

		private IAudioDSP_BlendingFilterContext blendingContext;

		public Context(FilterBlendWrapper wrapper)
		{
			this.wrapper = wrapper;
			UpdateContext();
		}

		public void Dispose()
		{
			wrapper.ContextRemoved(this);
			DisposeContext();
		}

		internal void UpdateContext()
		{
			if (filter != wrapper.Filter)
			{
				DisposeContext();
				filter = wrapper.Filter;
				IAudioDSP_FilterContext audioDSP_FilterContext = filter?.CreateContext();
				if (audioDSP_FilterContext is IAudioDSP_BlendingFilterContext audioDSP_BlendingFilterContext)
				{
					blendingContext = audioDSP_BlendingFilterContext;
				}
				else
				{
					context = audioDSP_FilterContext;
				}
			}
		}

		private void DisposeContext()
		{
			context?.Dispose();
			blendingContext?.Dispose();
			context = null;
			blendingContext = null;
		}

		public void Process(AudioBuffer buffer)
		{
			if (!wrapper.Enabled)
			{
				return;
			}
			if (blendingContext != null)
			{
				blendingContext.Process(buffer, wrapper.BlendWeight);
			}
			else
			{
				if (context == null)
				{
					return;
				}
				if (MathX.Approximately(wrapper.BlendWeight, 1f))
				{
					context.Process(buffer);
					return;
				}
				Span<float> destination = stackalloc float[buffer.Length];
				buffer.Data.CopyTo(destination);
				context.Process(buffer);
				if (MathX.Approximately(wrapper.BlendWeight, 0f))
				{
					destination.CopyTo(buffer.Data);
					return;
				}
				for (int i = 0; i < buffer.Length; i++)
				{
					buffer.Data[i] = MathX.LerpUnclamped(destination[i], buffer.Data[i], wrapper.BlendWeight);
				}
			}
		}
	}

	private List<Context> contexts = new List<Context>();

	public AudioSimulator Simulator { get; private set; }

	public IAudioDSP_Filter Filter { get; private set; }

	public float BlendWeight { get; internal set; }

	public bool Enabled { get; internal set; } = true;

	public FilterBlendWrapper(AudioSimulator simulator)
	{
		Simulator = simulator;
	}

	public IAudioDSP_FilterContext CreateContext()
	{
		Context context = new Context(this);
		contexts.Add(context);
		return context;
	}

	private void ContextRemoved(Context context)
	{
		contexts.Remove(context);
	}

	internal void SetFilter(IAudioDSP_Filter filter)
	{
		if (filter == Filter || !IsFilterValid(filter))
		{
			return;
		}
		Filter = filter;
		foreach (Context context in contexts)
		{
			context.UpdateContext();
		}
	}

	public bool IsFilterValid(IAudioDSP_Filter filter)
	{
		HashSet<IAudioDSP_Filter> hashSet = Pool.BorrowHashSet<IAudioDSP_Filter>();
		hashSet.Add(this);
		try
		{
			do
			{
				if (!hashSet.Add(filter))
				{
					return false;
				}
				filter = (filter as FilterBlendWrapper)?.Filter;
			}
			while (filter != null);
			return true;
		}
		finally
		{
			Pool.Return(ref hashSet);
		}
	}

	public void UpdateBlend(ChangesBatch batch, float blendWeight)
	{
		batch.Add<FilterBlendWeightChanges.Change, FilterBlendWeightChanges>(new FilterBlendWeightChanges.Change
		{
			filter = this,
			blendWeight = blendWeight
		});
	}

	public void UpdateFilter(ChangesBatch batch, IAudioDSP_Filter filter)
	{
		batch.Add<FilterBlendFilterChanges.Change, FilterBlendFilterChanges>(new FilterBlendFilterChanges.Change
		{
			filter = this,
			nestedFilter = filter
		});
	}

	public void UpdateEnabled(ChangesBatch batch, bool enabled)
	{
		batch.Add<FilterBlendEnabledChanges.Change, FilterBlendEnabledChanges>(new FilterBlendEnabledChanges.Change
		{
			filter = this,
			enabled = enabled
		});
	}
}
public interface IAudioDSP_Filter
{
	bool Enabled { get; }

	IAudioDSP_FilterContext CreateContext();

	void UpdateEnabled(ChangesBatch batch, bool enabled);
}
public interface IAudioDSP_FilterContext : IDisposable
{
	void Process(AudioBuffer buffer);
}
public interface IAudioDSP_BlendingFilterContext : IAudioDSP_FilterContext, IDisposable
{
	void Process(AudioBuffer buffer, float blend);
}
public class ZitaReverbFilter : IAudioDSP_Filter
{
	private class Context : IAudioDSP_FilterContext, IDisposable
	{
		private readonly ZitaReverbFilter filter;

		private BufferReverber<StereoSample> reverber;

		public Context(ZitaReverbFilter filter)
		{
			this.filter = filter;
			reverber = new BufferReverber<StereoSample>(filter.Simulator.SampleRate);
		}

		public void Dispose()
		{
			reverber.Dispose();
			reverber = null;
		}

		public void Process(AudioBuffer buffer)
		{
			if (filter.Enabled)
			{
				Span<StereoSample> buffer2 = buffer.Data.AsStereoBuffer();
				ZitaHelpers.FromOther(reverber, filter.Parameters);
				if (reverber.Mix > 0f)
				{
					reverber.ApplyReverb(buffer2);
				}
			}
		}
	}

	public AudioSimulator Simulator { get; private set; }

	public ZitaParameters Parameters { get; internal set; }

	public bool Enabled { get; internal set; } = true;

	public ZitaReverbFilter(AudioSimulator simulator)
	{
		Simulator = simulator;
	}

	public IAudioDSP_FilterContext CreateContext()
	{
		return new Context(this);
	}

	public void Update(ChangesBatch batch, ZitaParameters parameters)
	{
		batch.Add<ZitaReverbChanges.Change, ZitaReverbChanges>(new ZitaReverbChanges.Change
		{
			filter = this,
			parameters = parameters
		});
	}

	public void UpdateEnabled(ChangesBatch batch, bool enabled)
	{
		batch.Add<ZitaReverbEnabledChanges.Change, ZitaReverbEnabledChanges>(new ZitaReverbEnabledChanges.Change
		{
			filter = this,
			enabled = enabled
		});
	}
}
public interface IAudioDataSource
{
	bool IsActive { get; }

	int ChannelCount { get; }

	void Read<S>(Span<S> buffer, AudioSimulator system) where S : unmanaged, IAudioSample<S>;
}
public interface IAudioDeviceOutput
{
	void AudioFrameRendered(float[] buffer, double dspTime);
}
public interface IAudioShape
{
	AudioOutput Output { get; }

	BoundingBox Bounds { get; }

	float ComputeAttenuation(RigidTransform listenerTransform);
}
public class AudioBuffer
{
	public int Length => Data.Length;

	public float[] Data { get; private set; }

	public AudioBuffer(float[] buffer)
	{
		Data = buffer;
	}

	public AudioBuffer Clone(AudioSimulator simulator)
	{
		AudioBuffer audioBuffer = simulator.BufferPool.BorrowBuffer();
		Data.CopyTo(audioBuffer.Data, 0);
		return audioBuffer;
	}

	public void Clear()
	{
		Array.Clear(Data, 0, Length);
	}
}
public class AudioBufferPool
{
	private ConcurrentStack<AudioBuffer> _buffers = new ConcurrentStack<AudioBuffer>();

	public AudioSimulator Simulator { get; private set; }

	public int BufferSize => Simulator.BufferSize;

	public AudioBufferPool(AudioSimulator simulator)
	{
		Simulator = simulator;
	}

	public AudioBuffer BorrowBuffer()
	{
		if (_buffers.TryPop(out var result) && result.Length == BufferSize)
		{
			return result;
		}
		return new AudioBuffer(new float[BufferSize]);
	}

	public void ReturnBuffer(AudioBuffer buffer)
	{
		if (buffer.Length == BufferSize)
		{
			_buffers.Push(buffer);
		}
	}
}
internal struct ListenerRenderData
{
	public Listener listener;

	public float volume;

	public ListenerRenderData(Listener listener, float volume)
	{
		this.listener = listener;
		this.volume = volume;
	}
}
internal struct AudioRenderJob
{
	public AudioOutput output;

	public SlimPoolList<ListenerRenderData> listeners;
}
internal struct EmptyMixerJob
{
	public Listener listener;

	public DSP_Mixer mixer;
}
internal struct AudioMixJob
{
	public AudioOutput output;

	public ListenerRenderData renderData;

	public AudioBuffer buffer;
}
public class Listener : Mixer
{
	private Dictionary<AudioInlet, DSP_Mixer> mixerMapping = new Dictionary<AudioInlet, DSP_Mixer>();

	private HashSet<DSP_Mixer> activeMixers = new HashSet<DSP_Mixer>();

	private HashSet<DSP_Mixer> lastActiveMixers = new HashSet<DSP_Mixer>();

	private AudioBuffer currentBuffer;

	private AudioBuffer stagedBuffer;

	private AutoDucker<StereoSample> ducker = new AutoDucker<StereoSample>();

	public AudioSpace Space { get; private set; }

	public RigidTransform Transform { get; internal set; }

	public AudioDeviceOutput BoundDevice { get; internal set; }

	public Span<float> CurrentBuffer => currentBuffer.Data;

	public IReadOnlyDictionary<AudioInlet, DSP_Mixer> MixerMappings => mixerMapping;

	public Listener(AudioSpace space)
		: base(space.System)
	{
		Space = space;
		Transform = new RigidTransform(float3.Zero, floatQ.Identity);
		currentBuffer = base.Simulator.BufferPool.BorrowBuffer();
		currentBuffer.Clear();
	}

	internal void SetMixerMapping(AudioInlet inlet, DSP_Mixer mixer)
	{
		mixerMapping[inlet] = mixer;
	}

	internal void RemoveMixerMapping(AudioInlet inlet)
	{
		mixerMapping.Remove(inlet);
	}

	internal DSP_Mixer GetMixer(AudioInlet inlet)
	{
		if (inlet != null && mixerMapping.TryGetValue(inlet, out var value))
		{
			return value;
		}
		return null;
	}

	internal void RegisterActiveMixer(DSP_Mixer mixer)
	{
		activeMixers.Add(mixer);
	}

	internal void CleanupOldMixers()
	{
		foreach (DSP_Mixer lastActiveMixer in lastActiveMixers)
		{
			if (!activeMixers.Contains(lastActiveMixer))
			{
				lastActiveMixer.RemoveContext(this);
			}
		}
		lastActiveMixers.Clear();
		HashSet<DSP_Mixer> hashSet = lastActiveMixers;
		HashSet<DSP_Mixer> hashSet2 = activeMixers;
		activeMixers = hashSet;
		lastActiveMixers = hashSet2;
	}

	public void UpdateTransform(ChangesBatch batch, RigidTransform transform)
	{
		batch.Add<ListenerChanges.Change, ListenerChanges>(new ListenerChanges.Change
		{
			listener = this,
			transform = transform
		});
	}

	public void MapMixer(ChangesBatch batch, AudioInlet inlet, DSP_Mixer mixer)
	{
		batch.Add<ListenerMixerChanges.Change, ListenerMixerChanges>(new ListenerMixerChanges.Change
		{
			listener = this,
			inlet = inlet,
			mixer = mixer
		});
	}

	public void BindToDevice(AudioDeviceOutput output)
	{
		base.Simulator.EnqueueListenerBinding(this, output);
	}

	protected override void FinishMix(AudioBuffer result)
	{
		BufferSanitizer.SanitizeBuffer(result.Data);
		ducker.AutoDuck(result.Data.AsStereoBuffer());
		stagedBuffer = result;
		BoundDevice?.ConsumeAndMix(result.Clone(base.Simulator));
		Space.ListenerFinished();
	}

	internal void CommitStagedBuffer()
	{
		if (stagedBuffer == null)
		{
			throw new InvalidOperationException("No staged buffer to commit");
		}
		if (currentBuffer != null)
		{
			base.Simulator.BufferPool.ReturnBuffer(currentBuffer);
		}
		currentBuffer = stagedBuffer;
		stagedBuffer = null;
	}
}
public abstract class Mixer
{
	private ConcurrentBufferMixer mixer;

	private int remainingTracksToMix;

	public AudioSimulator Simulator { get; private set; }

	public Mixer(AudioSimulator simulator)
	{
		Simulator = simulator;
	}

	internal void PrepareMixing(int tracksToMix)
	{
		if (remainingTracksToMix > 0)
		{
			throw new InvalidOperationException("This mixer already has tracks to mix");
		}
		if (tracksToMix == 0)
		{
			AudioBuffer audioBuffer = Simulator.BufferPool.BorrowBuffer();
			audioBuffer.Clear();
			FinishMix(audioBuffer);
			return;
		}
		remainingTracksToMix = tracksToMix;
		if (mixer == null || mixer.Size != Simulator.BufferSize)
		{
			mixer = new ConcurrentBufferMixer(Simulator, Simulator.BufferSize);
		}
	}

	public void ConsumeAndMix(AudioBuffer buffer)
	{
		mixer.ConsumeAndMix(buffer);
		if (Interlocked.Decrement(ref remainingTracksToMix) <= 0)
		{
			AudioBuffer result = mixer.RetrieveResult();
			FinishMix(result);
		}
	}

	protected abstract void FinishMix(AudioBuffer result);
}
internal class BinauralEffect : IDisposable
{
	private IPL.BinauralEffect effect;

	public SteamAudioContext SteamAudio => System.SteamAudio;

	public AudioSimulator System => ListenerContext.System;

	public AudioOutput Output => ListenerContext.Output;

	public Listener Listener => ListenerContext.Listener;

	public AudioOutputListenerContext ListenerContext { get; private set; }

	public BinauralEffect(AudioOutputListenerContext listenerContext, IPL.BinauralEffect effect)
	{
		ListenerContext = listenerContext;
		this.effect = effect;
	}

	public unsafe void Spatialize(float[] buffer)
	{
		Span<StereoSample> span = buffer.AsStereoBuffer();
		IPL.AudioBuffer @in = SteamAudio.BufferPool.BorrowStereoBuffer();
		float* data = *(float**)@in.Data;
		float* pointer = *(float**)(@in.Data + sizeof(float*));
		Span<float> span2 = new Span<float>(data, System.FrameSize);
		Span<float> span3 = new Span<float>(pointer, System.FrameSize);
		for (int i = 0; i < span.Length; i++)
		{
			span2[i] = span[i].left;
			span3[i] = span[i].right;
		}
		RigidTransform transform = Output.Transform;
		ref readonly float3 position = ref transform.position;
		RigidTransform transform2 = Listener.Transform;
		float3 a = (position - transform2.position).GetNormalized(out var magnitude);
		a = ((!MathX.Approximately(in a, float3.Zero)) ? (Listener.Transform.rotation.Inverted * a) : float3.Forward);
		float spatialBlend = Output.SpatialBlend;
		float num = magnitude - Output.SpatializationStartDistnce;
		spatialBlend = ((!(num < 0f)) ? (spatialBlend * MathX.Clamp01(num / Output.SpatializationTransitionRange)) : 0f);
		IPL.BinauralEffectParams @params = new IPL.BinauralEffectParams
		{
			Hrtf = SteamAudio.HRTF,
			Direction = new IPL.Vector3(a.x, a.y, 0f - a.z),
			Interpolation = IPL.HrtfInterpolation.Bilinear,
			SpatialBlend = spatialBlend
		};
		IPL.AudioBuffer @out = SteamAudio.BufferPool.BorrowStereoBuffer();
		lock (System.SteamAudio)
		{
			IPL.BinauralEffectApply(effect, ref @params, ref @in, ref @out);
		}
		SteamAudio.BufferPool.ReturnStereoBuffer(@in);
		float* data2 = *(float**)@out.Data;
		float* pointer2 = *(float**)(@out.Data + sizeof(float*));
		Span<float> span4 = new Span<float>(data2, System.FrameSize);
		Span<float> span5 = new Span<float>(pointer2, System.FrameSize);
		for (int j = 0; j < span.Length; j++)
		{
			span[j] = new StereoSample(span4[j], span5[j]);
		}
		SteamAudio.BufferPool.ReturnStereoBuffer(@out);
	}

	public void Dispose()
	{
		IPL.BinauralEffectRelease(ref effect);
	}
}
internal class SteamAudioBufferPool
{
	private ConcurrentStack<IPL.AudioBuffer> monoBuffers = new ConcurrentStack<IPL.AudioBuffer>();

	private ConcurrentStack<IPL.AudioBuffer> stereoBuffers = new ConcurrentStack<IPL.AudioBuffer>();

	public AudioSimulator System => SteamAudio.System;

	public SteamAudioContext SteamAudio { get; private set; }

	public SteamAudioBufferPool(SteamAudioContext steamAudio)
	{
		SteamAudio = steamAudio;
	}

	public IPL.AudioBuffer BorrowMonoBuffer()
	{
		return BorrowBuffer(monoBuffers, 1);
	}

	public IPL.AudioBuffer BorrowStereoBuffer()
	{
		return BorrowBuffer(stereoBuffers, 2);
	}

	public void ReturnMonoBuffer(IPL.AudioBuffer buffer)
	{
		ReturnBuffer(monoBuffers, buffer);
	}

	public void ReturnStereoBuffer(IPL.AudioBuffer buffer)
	{
		ReturnBuffer(stereoBuffers, buffer);
	}

	private IPL.AudioBuffer BorrowBuffer(ConcurrentStack<IPL.AudioBuffer> pool, int channels)
	{
		if (pool.TryPop(out var result) && result.NumSamples != System.FrameSize)
		{
			IPL.AudioBufferFree(SteamAudio.Context, ref result);
			result = default(IPL.AudioBuffer);
		}
		if (result.Data == 0)
		{
			IPL.AudioBufferAllocate(SteamAudio.Context, channels, System.FrameSize, ref result);
		}
		return result;
	}

	private void ReturnBuffer(ConcurrentStack<IPL.AudioBuffer> pool, IPL.AudioBuffer buffer)
	{
		if (buffer.NumSamples != System.FrameSize)
		{
			IPL.AudioBufferFree(SteamAudio.Context, ref buffer);
		}
		else
		{
			pool.Push(buffer);
		}
	}
}
internal class SteamAudioContext
{
	private IPL.Context context;

	private IPL.Hrtf hrtf;

	private IPL.AudioSettings audioSettings;

	public AudioSimulator System { get; private set; }

	public bool IsReady
	{
		get
		{
			if (context != default(IPL.Context))
			{
				return HRTF_Loaded;
			}
			return false;
		}
	}

	public bool HRTF_Loaded => hrtf != default(IPL.Hrtf);

	public int InitializedSampleRate { get; private set; }

	public int InitializedFrameSize { get; private set; }

	internal SteamAudioBufferPool BufferPool { get; private set; }

	internal IPL.Context Context => context;

	internal IPL.Hrtf HRTF => hrtf;

	public SteamAudioContext(AudioSimulator system)
	{
		System = system;
		Initialize();
		BufferPool = new SteamAudioBufferPool(this);
	}

	private void Initialize()
	{
		IPL.ContextSettings settings = new IPL.ContextSettings
		{
			Version = 263427u,
			LogCallback = OnLog
		};
		IPL.ContextCreate(in settings, out context);
	}

	internal void FrameSizeChanged()
	{
		IPL.HrtfRelease(ref hrtf);
		LoadHRTF();
	}

	internal void LoadHRTF()
	{
		audioSettings = new IPL.AudioSettings
		{
			SamplingRate = System.SampleRate,
			FrameSize = System.FrameSize
		};
		IPL.HrtfSettings hrtfSettings = new IPL.HrtfSettings
		{
			Type = IPL.HrtfType.Default,
			Volume = 1f,
			NormType = IPL.HrtfNormType.Rms
		};
		IPL.HrtfCreate(context, in audioSettings, in hrtfSettings, out hrtf);
	}

	public BinauralEffect CreateBinauralEffect(AudioOutputListenerContext listenerContext)
	{
		IPL.BinauralEffectSettings effectSettings = new IPL.BinauralEffectSettings
		{
			Hrtf = hrtf
		};
		IPL.BinauralEffectCreate(context, in audioSettings, in effectSettings, out var effect);
		return new BinauralEffect(listenerContext, effect);
	}

	private void OnLog(IPL.LogLevel level, string message)
	{
		switch (level)
		{
		case IPL.LogLevel.Info:
			UniLog.Log("[SteamAudio] " + message);
			break;
		case IPL.LogLevel.Warning:
			UniLog.Warning("[SteamAudio] " + message);
			break;
		case IPL.LogLevel.Error:
			UniLog.Error("[SteamAudio] " + message, stackTrace: false);
			break;
		}
	}
}
public static class AudioClipRenderHelper
{
	public static async Task<AudioX> RenderAudioClip(this AudioSimulator system, float lengthSeconds, Action<Listener, ChangesBatch> onSetup, Action<Listener, ChangesBatch> onUpdate)
	{
		AudioX clip = new AudioX(system.ChannelConfiguration, system.SampleRate);
		AudioSpace space = system.AddSpace(startActive: true);
		Listener listener = space.AddListener();
		ChangesBatch changesBatch = space.BeginChangesBatch();
		onSetup(listener, changesBatch);
		space.FinishChangesBatch(changesBatch);
		TaskCompletionSource<bool> taskCompletion = new TaskCompletionSource<bool>();
		system.RenderStarted += RunUpdate;
		system.RenderFinished += PostUpdate;
		system.RenderAudio();
		await taskCompletion.Task.ConfigureAwait(continueOnCapturedContext: false);
		system.RenderStarted -= RunUpdate;
		system.RemoveSpace(space);
		return clip;
		void PostUpdate(AudioSimulator s)
		{
			clip.WriteSamples(listener.CurrentBuffer);
			if (clip.Duration < (double)lengthSeconds)
			{
				system.RenderAudio();
			}
			else
			{
				taskCompletion.SetResult(result: true);
			}
		}
		void RunUpdate(AudioSimulator s)
		{
			ChangesBatch changesBatch2 = space.BeginChangesBatch();
			onUpdate(listener, changesBatch2);
			space.FinishChangesBatch(changesBatch2);
		}
	}
}
public static class BufferSanitizer
{
	public static void SanitizeBuffer(float[] buffer)
	{
		bool flag = false;
		for (int i = 0; i < buffer.Length; i++)
		{
			flag |= float.IsNaN(buffer[i]);
			buffer[i] = MathX.Clamp(buffer[i], -4096f, 4096f);
		}
		if (flag)
		{
			Array.Clear(buffer, 0, buffer.Length);
		}
	}
}
