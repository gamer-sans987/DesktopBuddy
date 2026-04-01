using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Awwdio;
using Elements.Assets;
using Elements.Core;
using FrooxEngine.UIX;
using MimeDetective;
using NYoutubeDL;
using NYoutubeDL.Helpers;
using NYoutubeDL.Models;
using Renderite.Shared;
using SkyFrost.Base;

namespace FrooxEngine;

[Category(new string[] { "Assets" })]
public class VideoTextureProvider : AssetProvider<VideoTexture>, ITexture2DProvider, IAssetProvider<ITexture2D>, IAssetProvider, IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable, ITextureProvider, IAssetProvider<ITexture>, IPlayable, IWorldAudioDataSource, Awwdio.IAudioDataSource, IStaticAssetProvider
{
	private static bool youtubeDLupdated;

	private YoutubeDL youtubeDL;

	protected readonly SyncPlayback Playback;

	public readonly Sync<Uri> URL;

	public readonly Sync<bool> Stream;

	[DefaultValue(1f)]
	public readonly Sync<float> Volume;

	public readonly Sync<string> ForcePlaybackEngine;

	public readonly Sync<bool> ForceVideoStreamingServiceParsing;

	public readonly RawOutput<string> VideoTitle;

	public readonly RawOutput<string> CurrentPlaybackEngine;

	public readonly RawOutput<float> CurrentClockError;

	public readonly Sync<TextureFilterMode> FilterMode;

	public readonly Sync<int> AnisotropicLevel;

	public readonly Sync<TextureWrapMode> WrapModeU;

	public readonly Sync<TextureWrapMode> WrapModeV;

	public readonly Sync<int?> AudioTrackIndex;

	public readonly Sync<bool> PreferAudioOnly;

	public readonly Sync<int?> MaxWidth;

	public readonly Sync<int?> MaxHeight;

	private VideoTexture _videoTex;

	private Uri _lastURL;

	private bool _lastStream;

	private string _lastPlaybackEngine;

	private AssetLoadingData _loadingData;

	private bool _loaded;

	private CancellationTokenSource _loadCancellationToken;

	public override VideoTexture Asset => _videoTex;

	ITexture2D IAssetProvider<ITexture2D>.Asset => Asset;

	ITexture IAssetProvider<ITexture>.Asset => Asset;

	public override bool IsAssetAvailable => _loaded;

	public float Position
	{
		get
		{
			return Playback.Position;
		}
		set
		{
			Playback.Position = value;
		}
	}

	public float NormalizedPosition
	{
		get
		{
			return Playback.NormalizedPosition;
		}
		set
		{
			Playback.NormalizedPosition = value;
		}
	}

	public double ClipLength => Playback.ClipLength;

	public bool IsStreaming => Playback.IsStreaming;

	public float Speed
	{
		get
		{
			return Playback.Speed;
		}
		set
		{
			Playback.Speed = value;
		}
	}

	public bool IsPlaying => Playback.IsPlaying;

	public bool IsFinished => Playback.IsFinished;

	public bool Loop
	{
		get
		{
			return Playback.Loop;
		}
		set
		{
			Playback.Loop = value;
		}
	}

	public bool IsActive
	{
		get
		{
			if (IsAssetAvailable)
			{
				return IsPlaying;
			}
			return false;
		}
	}

	public int ChannelCount => 2;

	Uri IStaticAssetProvider.URL
	{
		get
		{
			return URL;
		}
		set
		{
			URL.Value = value;
		}
	}

	Asset IStaticAssetProvider.Asset => Asset;

	bool IStaticAssetProvider.IsAvailable => IsAssetAvailable;

	EngineAssetClass IStaticAssetProvider.AssetClass => EngineAssetClass.Video;

	public void Play()
	{
		Playback.Play();
	}

	public void Stop()
	{
		Playback.Stop();
	}

	public void Pause()
	{
		Playback.Pause();
	}

	public void Resume()
	{
		Playback.Resume();
	}

	private void UnregisterLoadingData()
	{
		if (_loadingData != null)
		{
			_loadingData?.UnregisterProvider(this);
			_loadingData = null;
		}
	}

	protected override void OnAwake()
	{
		base.OnAwake();
		FilterMode.Value = TextureFilterMode.Bilinear;
		AnisotropicLevel.Value = 8;
		WrapModeU.Value = TextureWrapMode.Repeat;
		WrapModeV.Value = TextureWrapMode.Repeat;
	}

	protected override void OnCommonUpdate()
	{
		base.OnCommonUpdate();
		VideoTexture asset = Asset;
		if (asset != null && asset.IsLoaded)
		{
			Asset.UpdateState(base.Enabled && Playback.IsPlaying, Playback.Loop, Playback.InstantPosition);
		}
		CurrentClockError.Value = Asset?.CurrentClockError ?? 0f;
	}

	public override void OnFocusChanged(World.WorldFocus focus)
	{
		if (focus == FrooxEngine.World.WorldFocus.Background)
		{
			VideoTexture asset = Asset;
			if (asset != null && asset.IsLoaded)
			{
				Asset.UpdateState(play: false, Playback.Loop, Playback.InstantPosition);
			}
		}
	}

	protected override void FreeAsset()
	{
		UnregisterLoadingData();
		_loadCancellationToken?.Cancel();
		_videoTex?.Unload();
		_videoTex = null;
		_loadCancellationToken = null;
		_lastURL = null;
		_loaded = false;
		_lastPlaybackEngine = null;
		RunInUpdateScope(delegate
		{
			VideoTitle.Value = null;
		});
		AssetRemoved();
	}

	protected override void UpdateAsset()
	{
		bool flag = false;
		bool flag2 = false;
		bool flag3 = false;
		if (URL.Value != null)
		{
			flag = AssetHelper.IsStreamingProtocol(URL.Value);
			flag2 = AssetHelper.IsVideoStreamingService(URL.Value) || ForceVideoStreamingServiceParsing.Value;
		}
		Uri assetURL;
		if (URL.Value != null && (flag || flag2))
		{
			assetURL = URL.Value;
			flag3 = true;
		}
		else
		{
			assetURL = ProcessURL(URL.Value);
		}
		bool flag4 = Stream.Value || flag3;
		if (!base.Enabled && _lastURL == null)
		{
			assetURL = null;
		}
		if (assetURL == null)
		{
			FreeAsset();
		}
		else if (assetURL != _lastURL || _lastStream != flag4 || _lastPlaybackEngine != ForcePlaybackEngine || MaxWidth.WasChanged || MaxHeight.WasChanged || PreferAudioOnly.WasChanged)
		{
			MaxWidth.WasChanged = false;
			MaxHeight.WasChanged = false;
			PreferAudioOnly.WasChanged = false;
			FreeAsset();
			if (_loadingData == null && base.World.AssetManager.TrackAssetLoading)
			{
				_loadingData = base.World.AssetManager.RegisterLoadingAsset(assetURL, this);
			}
			_lastURL = assetURL;
			_lastStream = flag4;
			_lastPlaybackEngine = ForcePlaybackEngine;
			if (flag2)
			{
				_loadCancellationToken = new CancellationTokenSource();
				StartTask(async delegate
				{
					await LoadFromVideoService(assetURL, _loadCancellationToken.Token).ConfigureAwait(continueOnCapturedContext: false);
				});
			}
			else if (assetURL.Scheme == "local" || !flag4)
			{
				StartTask(async delegate
				{
					await LoadFromAsset(assetURL).ConfigureAwait(continueOnCapturedContext: false);
				});
			}
			else
			{
				StartTask(async delegate
				{
					await LoadFromStreamURL(assetURL).ConfigureAwait(continueOnCapturedContext: false);
				});
			}
		}
		_videoTex?.SetTextureProperties(FilterMode, AnisotropicLevel, WrapModeU, WrapModeV);
		_videoTex?.SetPlaybackProperties(AudioTrackIndex.Value);
	}

	private static bool HasCodec(string codec)
	{
		if (string.IsNullOrWhiteSpace(codec))
		{
			return false;
		}
		if (codec.IndexOf("none", StringComparison.InvariantCultureIgnoreCase) == 0)
		{
			return false;
		}
		return true;
	}

	private static int VideoCodecRank(string codec)
	{
		if (string.IsNullOrEmpty(codec))
		{
			return int.MinValue;
		}
		StringComparison comparisonType = StringComparison.InvariantCultureIgnoreCase;
		if (codec.StartsWith("none", comparisonType))
		{
			return int.MinValue;
		}
		if (codec.StartsWith("avc1", comparisonType) || codec.IndexOf("h264", comparisonType) >= 0)
		{
			return 10;
		}
		return 0;
	}

	private static int AudioCodecRank(string codec)
	{
		if (string.IsNullOrEmpty(codec))
		{
			return int.MinValue;
		}
		StringComparison comparisonType = StringComparison.InvariantCultureIgnoreCase;
		if (codec.StartsWith("none", comparisonType))
		{
			return int.MinValue;
		}
		if (codec.Contains("mp4a"))
		{
			return 10;
		}
		return 0;
	}

	private static int AudioDubRank(FormatDownloadInfo info)
	{
		if (string.IsNullOrEmpty(info.FormatNote))
		{
			return int.MinValue;
		}
		if (info.FormatNote.Contains("original"))
		{
			return 10;
		}
		return 0;
	}

	private static bool HasSound(FormatDownloadInfo info)
	{
		if (!HasCodec(info.Acodec))
		{
			return info.Asr > 0.0;
		}
		return true;
	}

	private static bool HasVideo(FormatDownloadInfo info)
	{
		if (!HasCodec(info.Vcodec) && !(info.Fps > 0.0) && !(info.Width > 0))
		{
			return info.Height > 0;
		}
		return true;
	}

	private async Task LoadFromVideoService(Uri url, CancellationToken cancellationToken)
	{
		try
		{
			VideoTexture videoTex = CreateVideoTexture();
			_videoTex = videoTex;
			if (!(await LoadFromVideoServiceIntern(url, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)) && !cancellationToken.IsCancellationRequested)
			{
				_videoTex.SetFailedToLoad();
				_loadingData?.RegisterFailed(this, _videoTex);
				_loadingData = null;
			}
		}
		catch (Exception ex)
		{
			UniLog.Error($"Exception trying to load video URL {url} from video service:\n" + ex);
			youtubeDL = null;
			if (!cancellationToken.IsCancellationRequested)
			{
				_loadingData?.RegisterFailed(this, _videoTex);
				_loadingData = null;
				_videoTex.SetFailedToLoad();
			}
		}
	}

	private async Task<bool> LoadFromVideoServiceIntern(Uri url, CancellationToken cancellationToken)
	{
		if (youtubeDL == null)
		{
			string path = ((base.Engine.Platform == Platform.Windows) ? "yt-dlp.exe" : "yt-dlp_linux");
			string path2 = Path.Combine(base.Engine.AppPath, "RuntimeData", path);
			youtubeDL = new YoutubeDL(path2);
			youtubeDL.StandardOutputEvent += delegate(object? _, string message)
			{
				UniLog.Log("[yt-dlp] " + message);
			};
			youtubeDL.StandardErrorEvent += delegate(object? _, string message)
			{
				UniLog.Error("[yt-dlp] " + message);
			};
		}
		VideoStreamingServicesSettings settings = await Settings.GetActiveSettingAsync<VideoStreamingServicesSettings>();
		if (!youtubeDLupdated)
		{
			UniLog.Log("Updating yt-dlp");
			string backupPath = youtubeDL.YoutubeDlPath + ".backup";
			File.Copy(youtubeDL.YoutubeDlPath, backupPath, overwrite: true);
			await youtubeDL.Update("nightly");
			FileInfo fileInfo = new FileInfo(youtubeDL.YoutubeDlPath);
			if (fileInfo.Length < 1048576)
			{
				UniLog.Log($"Detected yt-dlp update corruption, size too small: {fileInfo.Length}. Restoring backup version");
				File.Copy(backupPath, youtubeDL.YoutubeDlPath, overwrite: true);
			}
			youtubeDLupdated = true;
		}
		youtubeDL.Options.VideoSelectionOptions.NoPlaylist = true;
		youtubeDL.Options.WorkaroundsOptions.CookiesFromBrowser = settings.UseCookiesFromBrowser.Value;
		youtubeDL.Options.GeneralOptions.JsRuntime = Enums.JsRuntime.quickjs;
		Stopwatch stopwatch = new Stopwatch();
		stopwatch.Start();
		DownloadInfo downloadInfo = await youtubeDL.GetDownloadInfoAsync(url.OriginalString).ConfigureAwait(continueOnCapturedContext: false);
		stopwatch.Stop();
		UniLog.Log($"[yt-dlp] Command executed in {stopwatch.ElapsedMilliseconds}ms: {youtubeDL.RunCommand}");
		if (cancellationToken.IsCancellationRequested)
		{
			return false;
		}
		VideoDownloadInfo downloadInfo2 = downloadInfo as VideoDownloadInfo;
		if (downloadInfo2 == null && downloadInfo is PlaylistDownloadInfo playlistDownloadInfo)
		{
			downloadInfo2 = playlistDownloadInfo.CurrentVideo;
		}
		if (downloadInfo2 == null)
		{
			return false;
		}
		_videoTex.SetPartiallyLoaded();
		RunSynchronously(delegate
		{
			VideoTitle.Value = downloadInfo2.Title;
		});
		Uri result;
		if (downloadInfo2.Formats.Any())
		{
			FormatDownloadInfo formatDownloadInfo = null;
			downloadInfo2.Formats.Sort((FormatDownloadInfo a, FormatDownloadInfo b) => a.Width.GetValueOrDefault().CompareTo(b.Width.GetValueOrDefault()));
			StringBuilder stringBuilder = Pool.BorrowStringBuilder();
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(23, 1, stringBuilder2);
			handler.AppendLiteral("Available formats for ");
			handler.AppendFormatted(url);
			handler.AppendLiteral(":");
			stringBuilder2.AppendLine(ref handler);
			foreach (FormatDownloadInfo format in downloadInfo2.Formats)
			{
				stringBuilder.AppendLine(InfoToString(format));
			}
			UniLog.Log(stringBuilder.ToString());
			Pool.Return(ref stringBuilder);
			bool value = PreferAudioOnly.Value;
			foreach (FormatDownloadInfo format2 in downloadInfo2.Formats)
			{
				if (formatDownloadInfo == null)
				{
					formatDownloadInfo = format2;
					continue;
				}
				bool flag = HasSound(formatDownloadInfo);
				bool flag2 = HasVideo(formatDownloadInfo);
				int valueOrDefault = formatDownloadInfo.Width.GetValueOrDefault();
				int valueOrDefault2 = formatDownloadInfo.Height.GetValueOrDefault();
				_ = formatDownloadInfo.Abr ?? (-1.0);
				bool flag3 = HasSound(format2);
				bool flag4 = HasVideo(format2);
				int valueOrDefault3 = format2.Width.GetValueOrDefault();
				int valueOrDefault4 = format2.Height.GetValueOrDefault();
				_ = format2.Abr ?? (-1.0);
				int num = MaxWidth.Value ?? 1920;
				int num2 = MaxHeight.Value ?? 1080;
				if (!MaxWidth.Value.HasValue && MaxHeight.Value.HasValue)
				{
					num2 = int.MaxValue;
				}
				if (!MaxHeight.Value.HasValue && MaxWidth.Value.HasValue)
				{
					num = int.MaxValue;
				}
				UniLog.Log($"Codec {format2.Vcodec} + {format2.Acodec} vs {formatDownloadInfo.Vcodec} + {format2.Acodec}. HasAudio: {flag3}, HasVideo: {flag4}, AudioOnly: {value}, FormatNote: {format2.FormatNote}");
				if (value)
				{
					if (flag3 && !flag4)
					{
						int num3 = AudioCodecRank(format2.Acodec);
						int num4 = AudioCodecRank(formatDownloadInfo.Acodec);
						if (flag2 || num3 > num4 || (num3 == num4 && format2.Asr > formatDownloadInfo.Asr))
						{
							formatDownloadInfo = format2;
						}
						continue;
					}
					if (flag && !flag2 && flag4)
					{
						continue;
					}
				}
				else if (flag2 && !flag4)
				{
					continue;
				}
				if (flag && !flag3)
				{
					continue;
				}
				if (flag3 && !flag)
				{
					formatDownloadInfo = format2;
					continue;
				}
				if (AudioDubRank(format2) > AudioDubRank(formatDownloadInfo))
				{
					formatDownloadInfo = format2;
					continue;
				}
				bool flag5 = valueOrDefault == 0 && valueOrDefault2 == 0;
				if (valueOrDefault3 > valueOrDefault && (flag5 || valueOrDefault3 <= num))
				{
					formatDownloadInfo = format2;
				}
				else if (valueOrDefault4 > valueOrDefault2 && (flag5 || valueOrDefault4 <= num2))
				{
					formatDownloadInfo = format2;
				}
				else if (valueOrDefault3 == valueOrDefault)
				{
					if (VideoCodecRank(format2.Vcodec) > VideoCodecRank(formatDownloadInfo.Vcodec))
					{
						formatDownloadInfo = format2;
					}
					else if (format2.Abr > formatDownloadInfo.Abr)
					{
						formatDownloadInfo = format2;
					}
				}
			}
			if (formatDownloadInfo == null)
			{
				return false;
			}
			UniLog.Log("Best Format: " + InfoToString(formatDownloadInfo) + ", FormatNote: " + formatDownloadInfo.FormatNote);
			string text = ForcePlaybackEngine.Value;
			if (text == null)
			{
				StringComparison comparisonType = StringComparison.InvariantCultureIgnoreCase;
				bool num5 = HasCodec(formatDownloadInfo.Vcodec) && (formatDownloadInfo.Vcodec.StartsWith("avc1", comparisonType) || formatDownloadInfo.Vcodec.IndexOf("h264", comparisonType) >= 0);
				bool flag6 = HasCodec(formatDownloadInfo.Acodec) && formatDownloadInfo.Acodec.StartsWith("mp4a", comparisonType);
				if (!num5 || !flag6)
				{
					text = "libVLC";
				}
			}
			if (cancellationToken.IsCancellationRequested)
			{
				return false;
			}
			string mime = null;
			_videoTex.Load(formatDownloadInfo.Url, text, mime, AudioTrackIndex, delegate(bool changed)
			{
				VideoLoaded(_videoTex, changed);
			}, VideoTextureChanged);
		}
		else if (Uri.TryCreate(downloadInfo2.Url, UriKind.Absolute, out result))
		{
			UniLog.Log("Single-Stream URL: " + result);
			if (cancellationToken.IsCancellationRequested)
			{
				return false;
			}
			_videoTex.Load(downloadInfo2.Url, "libVLC", null, AudioTrackIndex, delegate(bool changed)
			{
				VideoLoaded(_videoTex, changed);
			}, VideoTextureChanged);
		}
		return true;
	}

	private static string InfoToString(FormatDownloadInfo info)
	{
		return $"Video: {info.Vcodec}, Audio: {info.Acodec}, Container: {info.Container}, Size: {info.Width}x{info.Height}, Sample Rate: {info.Asr}, Url: {info.Url}";
	}

	protected override Uri ProcessURL(Uri assetURL)
	{
		if (assetURL == null)
		{
			return null;
		}
		return base.ProcessURL(assetURL);
	}

	private VideoTexture CreateVideoTexture()
	{
		VideoTexture videoTexture = new VideoTexture();
		videoTexture.InitializeDynamic(base.AssetManager);
		if (_loadingData != null && !_loadingData.RegisterAsset(this, videoTexture))
		{
			UnregisterLoadingData();
		}
		return videoTexture;
	}

	private string GetPlaybackEngine(string mime)
	{
		if (mime == null || !mime.Contains("mp4"))
		{
			return "libVLC";
		}
		return ForcePlaybackEngine.Value;
	}

	private async ValueTask LoadFromAsset(Uri assetURL)
	{
		VideoTexture tex = CreateVideoTexture();
		_videoTex = tex;
		await default(ToBackground);
		GatherResult gatherResult = await base.AssetManager.GatherAsset(assetURL, 0f, DB_Endpoint.Video).ConfigureAwait(continueOnCapturedContext: false);
		if (gatherResult.gatherJob != null)
		{
			gatherResult.gatherJob.ProgressUpdated += delegate(float progress)
			{
				tex.UpdateDownloadProgress(progress);
			};
		}
		string file = await gatherResult.GetFile().ConfigureAwait(continueOnCapturedContext: false);
		string mime = null;
		if (File.Exists(file))
		{
			mime = new FileInfo(file).GetFileType()?.Mime;
		}
		await default(ToWorld);
		if (assetURL == _lastURL)
		{
			_videoTex.Load(file, GetPlaybackEngine(mime), mime, AudioTrackIndex, delegate(bool changed)
			{
				VideoLoaded(tex, changed);
			}, VideoTextureChanged);
		}
	}

	private async ValueTask LoadFromStreamURL(Uri streamURL)
	{
		VideoTexture tex = CreateVideoTexture();
		_videoTex = tex;
		string mime = null;
		if (streamURL.OriginalString.Contains(".mp4"))
		{
			mime = "video/mp4";
		}
		if (mime == null && streamURL.Scheme == base.Cloud.Assets.DBScheme)
		{
			CloudResult cloudResult = await base.Cloud.Assets.GetAssetMime(streamURL);
			if (cloudResult.IsOK)
			{
				mime = cloudResult.Content;
			}
		}
		if (!(streamURL != _lastURL))
		{
			string playbackEngine = GetPlaybackEngine(mime);
			if (streamURL.Scheme == base.Cloud.Assets.DBScheme)
			{
				streamURL = base.Cloud.Assets.DBToHttp(streamURL, DB_Endpoint.Video);
			}
			_videoTex.Load(streamURL.OriginalString, playbackEngine, mime, AudioTrackIndex, delegate(bool changed)
			{
				VideoLoaded(tex, changed);
			}, VideoTextureChanged);
		}
	}

	private void VideoTextureChanged()
	{
		AssetCreated();
	}

	private void VideoLoaded(VideoTexture texture, bool assetInstanceChanged)
	{
		if (texture != _videoTex)
		{
			texture.Unload();
			return;
		}
		_loaded = true;
		Playback.ClipLength = _videoTex.Length;
		RunSynchronously(delegate
		{
			CurrentPlaybackEngine.Value = _videoTex.PlaybackEngine;
		});
		if (assetInstanceChanged)
		{
			_videoTex.SetFullyLoaded();
			AssetCreated();
			_loadingData?.RegisterFullyLoaded(this, texture);
			_loadingData = null;
		}
		else
		{
			AssetUpdated();
		}
	}

	public void Read<S>(Span<S> buffer, AudioSimulator simulator) where S : unmanaged, IAudioSample<S>
	{
		VideoTexture asset = Asset;
		if (asset != null)
		{
			asset?.AudioRead(buffer, simulator);
		}
		else
		{
			buffer.Clear();
		}
	}

	public override void BuildInspectorUI(UIBuilder ui)
	{
		base.BuildInspectorUI(ui);
		ui.Button((LocaleString)"UseAutomatic Engine", UseAutomatic);
		ui.Button((LocaleString)"Use Unity Native Playback Engine", UseUnityNative);
		ui.Button((LocaleString)"Use libVLC Playback Engine", UseLibVLC);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void UseAutomatic(IButton button, ButtonEventData eventData)
	{
		ForcePlaybackEngine.Value = null;
	}

	[SyncMethod(typeof(Delegate), null)]
	private void UseUnityNative(IButton button, ButtonEventData eventData)
	{
		ForcePlaybackEngine.Value = "Unity";
	}

	[SyncMethod(typeof(Delegate), null)]
	private void UseLibVLC(IButton button, ButtonEventData eventData)
	{
		ForcePlaybackEngine.Value = "libVLC";
	}

	protected override void OnDispose()
	{
		youtubeDL = null;
		base.OnDispose();
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		Playback = new SyncPlayback();
		URL = new Sync<Uri>();
		Stream = new Sync<bool>();
		Volume = new Sync<float>();
		ForcePlaybackEngine = new Sync<string>();
		ForceVideoStreamingServiceParsing = new Sync<bool>();
		VideoTitle = new RawOutput<string>();
		CurrentPlaybackEngine = new RawOutput<string>();
		CurrentClockError = new RawOutput<float>();
		FilterMode = new Sync<TextureFilterMode>();
		AnisotropicLevel = new Sync<int>();
		WrapModeU = new Sync<TextureWrapMode>();
		WrapModeV = new Sync<TextureWrapMode>();
		AudioTrackIndex = new Sync<int?>();
		PreferAudioOnly = new Sync<bool>();
		MaxWidth = new Sync<int?>();
		MaxHeight = new Sync<int?>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => Playback, 
			4 => URL, 
			5 => Stream, 
			6 => Volume, 
			7 => ForcePlaybackEngine, 
			8 => ForceVideoStreamingServiceParsing, 
			9 => VideoTitle, 
			10 => CurrentPlaybackEngine, 
			11 => CurrentClockError, 
			12 => FilterMode, 
			13 => AnisotropicLevel, 
			14 => WrapModeU, 
			15 => WrapModeV, 
			16 => AudioTrackIndex, 
			17 => PreferAudioOnly, 
			18 => MaxWidth, 
			19 => MaxHeight, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static VideoTextureProvider __New()
	{
		return new VideoTextureProvider();
	}
}
