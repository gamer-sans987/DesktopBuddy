using System;
using System.Threading.Tasks;
using Elements.Assets;
using Elements.Core;
using Elements.Data;
using FrooxEngine.UIX;
using FrooxEngine.Undo;
using SharpPipe;

namespace FrooxEngine;

[Category(new string[] { "Assets" })]
[OldTypeName("FrooxEngine.StaticAudioClipProvider", null)]
public class StaticAudioClip : StaticAssetProvider<AudioClip, DummyMetadata, AudioClipVariantDescriptor>
{
	public readonly Sync<AudioLoadMode> LoadMode;

	public readonly Sync<SampleRateMode> SampleRateMode;

	public override EngineAssetClass AssetClass => EngineAssetClass.Audio;

	protected override void OnAwake()
	{
		base.OnAwake();
		LoadMode.Value = AudioLoadMode.Automatic;
		SampleRateMode.Value = FrooxEngine.SampleRateMode.Conform;
	}

	public override void BuildInspectorUI(UIBuilder ui)
	{
		base.BuildInspectorUI(ui);
		AudioClipAssetMetadata audioClipAssetMetadata = ui.Root.AttachComponent<AudioClipAssetMetadata>();
		audioClipAssetMetadata.AudioClip.Target = this;
		ui.Text("Inspector.Audio.FormatInfo".AsLocaleKey(("rate", audioClipAssetMetadata.SampleRate), ("channels", audioClipAssetMetadata.Channels), ("channel_count", audioClipAssetMetadata.ChannelCount)));
		ui.Text("Inspector.Audio.Duration".AsLocaleKey(("duration", audioClipAssetMetadata.Duration), ("samples", audioClipAssetMetadata.SampleCount)));
		ui.Text("Inspector.Audio.EncodingInfo".AsLocaleKey(("info", audioClipAssetMetadata.CodecInfo), ("decoded", audioClipAssetMetadata.FullyDecoded)));
		ui.Button("Inspector.Audio.Normalize".AsLocaleKey(), Normalize);
		ui.Button("Inspector.Audio.ExtractSides".AsLocaleKey(), ExtractSides);
		ui.Button("Inspector.Audio.DenoiseRNNoise".AsLocaleKey(), Denoise);
		ui.HorizontalLayout(4f);
		ui.Text("Inspector.Audio.AmplitudeThreshold".AsLocaleKey());
		FloatTextEditorParser floatTextEditorParser = ui.FloatField(0f, 1f, 4);
		floatTextEditorParser.ParsedValue.Value = 0.002f;
		ui.NestOut();
		ui.HorizontalLayout(4f);
		ui.ButtonRef("Inspector.Audio.TrimSilence".AsLocaleKey(), (colorX?)null, TrimSilence, floatTextEditorParser);
		ui.ButtonRef("Inspector.Audio.TrimStartSilence".AsLocaleKey(), (colorX?)null, TrimStartSilence, floatTextEditorParser);
		ui.ButtonRef("Inspector.Audio.TrimEndSilence".AsLocaleKey(), (colorX?)null, TrimEndSilence, floatTextEditorParser);
		ui.NestOut();
		ui.HorizontalLayout(4f);
		ui.Text("Inspector.Audio.PositionDuration".AsLocaleKey());
		float minWidth = ui.Style.MinWidth;
		ui.Style.MinWidth = 64f;
		FloatTextEditorParser floatTextEditorParser2 = ui.FloatField(0f, float.MaxValue, 4);
		floatTextEditorParser2.ParsedValue.Value = 0.1f;
		ui.Style.MinWidth = minWidth;
		ui.NestOut();
		ui.HorizontalLayout(4f);
		ui.ButtonRef("Inspector.Audio.TrimStart".AsLocaleKey(), (colorX?)null, TrimStart, floatTextEditorParser2);
		ui.ButtonRef("Inspector.Audio.TrimEnd".AsLocaleKey(), (colorX?)null, TrimEnd, floatTextEditorParser2);
		ui.NestOut();
		ui.HorizontalLayout(4f);
		ui.ButtonRef("Inspector.Audio.FadeIn".AsLocaleKey(), (colorX?)null, FadeIn, floatTextEditorParser2);
		ui.ButtonRef("Inspector.Audio.FadeOut".AsLocaleKey(), (colorX?)null, FadeOut, floatTextEditorParser2);
		ui.NestOut();
		ui.ButtonRef("Inspector.Audio.MakeLoopable".AsLocaleKey(), (colorX?)null, MakeLoopable, floatTextEditorParser2);
		ui.HorizontalLayout(4f);
		ui.ButtonRef("Inspector.Audio.ToWAV".AsLocaleKey(), (colorX?)null, ConvertToWAV, floatTextEditorParser2);
		ui.ButtonRef("Inspector.Audio.ToVorbis".AsLocaleKey(), (colorX?)null, ConvertToVorbis, floatTextEditorParser2);
		ui.ButtonRef("Inspector.Audio.ToFLAC".AsLocaleKey(), (colorX?)null, ConvertToFLAC, floatTextEditorParser2);
		ui.NestOut();
	}

	protected override async ValueTask<AudioClipVariantDescriptor> UpdateVariantDescriptor(DummyMetadata metadata, AudioClipVariantDescriptor currentVariant)
	{
		if (currentVariant == null || currentVariant.LoadMode != LoadMode.Value || currentVariant.SampleRateMode != SampleRateMode.Value)
		{
			return new AudioClipVariantDescriptor(LoadMode, SampleRateMode);
		}
		return null;
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> Normalize()
	{
		return Process(delegate(AudioX a)
		{
			a.Normalize();
		}, null);
	}

	[SyncMethod(typeof(Func<float, Task<bool>>), new string[] { })]
	public Task<bool> AdjustVolume(float ratio)
	{
		return Process(delegate(AudioX a)
		{
			a.AdjustVolume(ratio);
		}, null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> ExtractSides()
	{
		return Process(delegate(AudioX a)
		{
			a.ExtractSides();
		}, null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> Denoise()
	{
		return Process(delegate(AudioX a)
		{
			a.Denoise();
		}, null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> TrimSilence()
	{
		return Process(delegate(AudioX a)
		{
			a.TrimSilence();
		}, null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> TrimStartSilence()
	{
		return Process(delegate(AudioX a)
		{
			a.TrimStartSilence();
		}, null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> TrimEndSilence()
	{
		return Process(delegate(AudioX a)
		{
			a.TrimEndSilence();
		}, null);
	}

	[SyncMethod(typeof(Func<float, Task<bool>>), new string[] { })]
	public Task<bool> TrimStart(float duration)
	{
		return Process(delegate(AudioX a)
		{
			a.TrimStart(duration);
		}, null);
	}

	[SyncMethod(typeof(Func<float, Task<bool>>), new string[] { })]
	public Task<bool> TrimEnd(float duration)
	{
		return Process(delegate(AudioX a)
		{
			a.TrimEnd(duration);
		}, null);
	}

	[SyncMethod(typeof(Func<float, Task<bool>>), new string[] { })]
	public Task<bool> FadeIn(float duration)
	{
		return Process(delegate(AudioX a)
		{
			a.FadeIn(duration);
		}, null);
	}

	[SyncMethod(typeof(Func<float, Task<bool>>), new string[] { })]
	public Task<bool> FadeOut(float duration)
	{
		return Process(delegate(AudioX a)
		{
			a.FadeOut(duration);
		}, null);
	}

	[SyncMethod(typeof(Func<float, Task<bool>>), new string[] { })]
	public Task<bool> MakeFadeLoop(float duration)
	{
		return Process(delegate(AudioX a)
		{
			a.MakeFadeLoop(duration);
		}, null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> ConvertToWAV()
	{
		return Process((AudioX a) => a, null, new WavEncodeSettings());
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> ConvertToVorbis()
	{
		return Process((AudioX a) => a, null, new VorbisEncodeSettings());
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> ConvertToFLAC()
	{
		return Process((AudioX a) => a, null, new FlacEncodeSettings());
	}

	[SyncMethod(typeof(Func<ZitaParameters, Task<bool>>), new string[] { })]
	public Task<bool> ApplyZitaReverb(ZitaParameters filter)
	{
		return Process(delegate(AudioX a)
		{
			a.ApplyZitaFilter(filter);
		}, null);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void Normalize(IButton button, ButtonEventData eventData)
	{
		Process(delegate(AudioX a)
		{
			a.Normalize();
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void ExtractSides(IButton button, ButtonEventData eventData)
	{
		Process(delegate(AudioX a)
		{
			a.ExtractSides();
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void Denoise(IButton button, ButtonEventData eventData)
	{
		Process(delegate(AudioX a)
		{
			a.Denoise();
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void TrimSilence(IButton button, ButtonEventData eventData, FloatTextEditorParser textField)
	{
		Process(delegate(AudioX a)
		{
			a.TrimSilence(textField.ParsedValue);
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void TrimStartSilence(IButton button, ButtonEventData eventData, FloatTextEditorParser textField)
	{
		Process(delegate(AudioX a)
		{
			a.TrimStartSilence(textField.ParsedValue);
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void TrimEndSilence(IButton button, ButtonEventData eventData, FloatTextEditorParser textField)
	{
		Process(delegate(AudioX a)
		{
			a.TrimEndSilence(textField.ParsedValue);
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void TrimStart(IButton button, ButtonEventData eventData, FloatTextEditorParser textField)
	{
		Process(delegate(AudioX a)
		{
			a.TrimStart(textField.ParsedValue);
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void TrimEnd(IButton button, ButtonEventData eventData, FloatTextEditorParser textField)
	{
		Process(delegate(AudioX a)
		{
			a.TrimEnd(textField.ParsedValue);
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void FadeIn(IButton button, ButtonEventData eventData, FloatTextEditorParser textField)
	{
		Process(delegate(AudioX a)
		{
			a.FadeIn(textField.ParsedValue);
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void FadeOut(IButton button, ButtonEventData eventData, FloatTextEditorParser textField)
	{
		Process(delegate(AudioX a)
		{
			a.FadeOut(textField.ParsedValue);
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void MakeLoopable(IButton button, ButtonEventData eventData, FloatTextEditorParser textField)
	{
		Process(delegate(AudioX a)
		{
			a.MakeFadeLoop(textField.ParsedValue);
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void ConvertToWAV(IButton button, ButtonEventData eventData, FloatTextEditorParser textField)
	{
		Process((AudioX a) => a, button, new WavEncodeSettings());
	}

	[SyncMethod(typeof(Delegate), null)]
	private void ConvertToVorbis(IButton button, ButtonEventData eventData, FloatTextEditorParser textField)
	{
		Process((AudioX a) => a, button, new VorbisEncodeSettings());
	}

	[SyncMethod(typeof(Delegate), null)]
	private void ConvertToFLAC(IButton button, ButtonEventData eventData, FloatTextEditorParser textField)
	{
		Process((AudioX a) => a, button, new FlacEncodeSettings());
	}

	private Task<bool> Process(Action<AudioX> processFunc, IButton button)
	{
		return Process(delegate(AudioX a)
		{
			processFunc(a);
			return a;
		}, button);
	}

	private Task<bool> Process(Func<AudioX, AudioX> processFunc, IButton button, AudioEncodeSettings encodeSettings = null)
	{
		return StartGlobalTask(async () => await ProcessAsync(processFunc, button, encodeSettings));
	}

	private async Task<bool> ProcessAsync(Func<AudioX, AudioX> processFunc, IButton button, AudioEncodeSettings encodeSettings = null)
	{
		if (URL.Value == null)
		{
			return false;
		}
		while (Asset == null)
		{
			await default(NextUpdate);
		}
		string _description = button?.LabelText;
		if (button != null)
		{
			button.LabelText = this.GetLocalized("General.Processing");
			button.Enabled = false;
		}
		Uri uri;
		try
		{
			AudioX audio = processFunc(await Asset.GetOriginalAudioData().ConfigureAwait(continueOnCapturedContext: false));
			uri = await base.Engine.LocalDB.SaveAssetAsync(audio, encodeSettings).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex)
		{
			UniLog.Error($"Exception processing audio clip {URL.Value}:\n" + ex);
			await default(ToWorld);
			if (button != null && !button.IsDestroyed)
			{
				button.LabelText = "<color=#f00>Error! Check log.</color>";
			}
			throw;
		}
		await default(ToWorld);
		if (button != null && !button.IsDestroyed)
		{
			button.LabelText = _description;
			button.Enabled = true;
		}
		if (uri == null)
		{
			return false;
		}
		if (button != null)
		{
			base.World.BeginUndoBatch(_description);
			URL.UndoableSet(uri, forceNew: true);
			base.World.EndUndoBatch();
		}
		else
		{
			URL.Value = uri;
		}
		return true;
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		LoadMode = new Sync<AudioLoadMode>();
		SampleRateMode = new Sync<SampleRateMode>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => URL, 
			4 => LoadMode, 
			5 => SampleRateMode, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static StaticAudioClip __New()
	{
		return new StaticAudioClip();
	}
}
