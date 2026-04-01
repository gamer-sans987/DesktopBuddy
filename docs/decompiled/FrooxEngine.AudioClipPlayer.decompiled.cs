using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Awwdio;
using Elements.Assets;
using SkyFrost.Base;

namespace FrooxEngine;

[Category(new string[] { "Audio" })]
public class AudioClipPlayer : AudioClipPlayerBase, IItemMetadataSource, IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable
{
	public readonly AssetRef<AudioClip> Clip;

	public override double ClipLength
	{
		get
		{
			if (Clip.IsAssetAvailable)
			{
				return (Clip.Asset?.Data?.Duration).GetValueOrDefault();
			}
			return 0.0;
		}
	}

	protected override bool CanBeActive => Clip.Asset?.Data != null;

	protected override double BaseRate => Clip.Asset?.BaseRate ?? 0.0;

	protected override int SampleRate => (Clip.Asset?.Data?.SampleRate).GetValueOrDefault();

	public override int ChannelCount
	{
		get
		{
			if (Clip.IsAssetAvailable)
			{
				return Clip.Asset.Data?.ChannelCount ?? 0;
			}
			return 0;
		}
	}

	public string ItemName => base.Slot.Name;

	public IEnumerable<string> ItemTags
	{
		get
		{
			yield return RecordTags.AudioClip;
			StaticAudioClip staticAudioClip = Clip.Target as StaticAudioClip;
			if (staticAudioClip?.URL.Value != null)
			{
				yield return RecordTags.ClipAsset(staticAudioClip.URL.Value.ToString());
			}
			if (ClipLength > 0.0)
			{
				yield return RecordTags.ClipLength(ClipLength);
			}
		}
	}

	protected override string DebugString => $"audio clip {Clip?.Asset?.AssetURL} (sample count: {Clip?.Asset?.Data?.SampleCount})";

	protected override void OnAwake()
	{
		base.OnAwake();
		Clip.OnTargetChange += Clip_OnTargetChange;
	}

	private void Clip_OnTargetChange(SyncRef<IAssetProvider<AudioClip>> reference)
	{
		UpdateClipLength();
	}

	protected override void OnStart()
	{
		UpdateClipLength();
	}

	protected override void OnChanges()
	{
		UpdateClipLength();
	}

	public void Read(float[] samples, int offset, int count)
	{
		switch ((Clip.Asset?.Data?.Channels).GetValueOrDefault())
		{
		case ChannelConfiguration.Mono:
			Read(MemoryMarshal.Cast<float, MonoSample>(samples.AsSpan().Slice(offset, count)), base.Audio.Simulator);
			break;
		case ChannelConfiguration.Stereo:
			Read(MemoryMarshal.Cast<float, StereoSample>(samples.AsSpan().Slice(offset, count)), base.Audio.Simulator);
			break;
		}
	}

	protected override int Read<S>(Span<S> buffer, double position, double rate, bool loop, AudioSimulator simulator)
	{
		return (Clip.Asset?.Data)?.Read(buffer, position, rate, loop) ?? 0;
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		Clip = new AssetRef<AudioClip>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => playback, 
			4 => Clip, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static AudioClipPlayer __New()
	{
		return new AudioClipPlayer();
	}
}
