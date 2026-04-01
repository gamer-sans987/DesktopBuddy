using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Elements.Assets;
using Elements.Core;
using FrooxEngine.ProtoFlux;
using FrooxEngine.Store;
using FrooxEngine.Weaver;
using Renderite.Shared;
using SDL3;
using SkyFrost.Base;

namespace FrooxEngine;

public class Engine : IDisposable
{
	public enum UpdateStage
	{
		UpdateBegin,
		FrameStart,
		InputUpdate,
		GlobalCoroutinesUpdate,
		WorldUpdate,
		RenderSubmit,
		AssetUpdate,
		Finished
	}

	public const string COLOR_MANAGEMENT = "ColorManagement";

	public int AutoReadyAfterUpdates = 90;

	public bool TimesliceAssetIntegration = true;

	public bool TimesliceParticleIntegration = true;

	public bool VerboseInit;

	public bool LogUpdateIntervals;

	public int MaxShutdownWaitMilliseconds = 1000;

	private string _uid;

	private CancellationTokenSource _globalCancellationToken;

	private FileSystemWatcher _localeChangeWatcher;

	private bool _SDLinitialized;

	private static readonly string[] BUILT_IN_LOCALES = new string[25]
	{
		"cs", "de", "en-gb", "en", "eo", "es", "et", "fi", "fr", "hu",
		"is", "ja", "ko", "mn", "nl", "no", "pl", "pt-br", "ru", "sv",
		"th", "tr", "uk", "zh-cn", "zh-tw"
	};

	private static readonly string[] BUILT_IN_LOCALE_NATIVE_NAMES = new string[25]
	{
		"čeština", "Deutsch", "English (United Kingdom)", "English", "esperanto", "español", "eesti", "suomi", "français", "magyar",
		"íslenska", "日本語", "한국어", "монгол", "Nederlands", "norsk", "polski", "português (Brasil)", "русский", "svenska",
		"ไทย", "Türkçe", "українська", "中文(中国)", "中文(台灣)"
	};

	private static readonly string[] BUILT_IN_LOCALE_ENGLISH_NAMES = new string[25]
	{
		"Czech", "German", "English (United Kingdom)", "English", "Esperanto", "Spanish", "Estonian", "Finnish", "French", "Hungarian",
		"Icelandic", "Japanese", "Korean", "Mongolian", "Dutch", "Norwegian", "Polish", "Portuguese (Brazil)", "Russian", "Swedish",
		"Thai", "Turkish", "Ukrainian", "Chinese (Simplified, Mainland China)", "Chinese (Traditional, Taiwan)"
	};

	private int _currentAssetUpdates;

	private int _currentCameraRenders;

	private int _currentPortalRenders;

	private int _currentBlitRenders;

	private int _currentDesktopRenders;

	private int _currentMaterialUpdates;

	private int _currentTextureUpdates;

	private int _currentTextureSliceUpdates;

	private int _currentMeshUpdates;

	private int _currentSpriteUpdates;

	private int _currentParticleUploads;

	private int _currentParticleUploadCount;

	private int _currentAudioReads;

	private int _currentEmptyAudioReads;

	private static string _currentVersion;

	private static string _versionString;

	private static string _versionNumber;

	private static VersionNumber _version;

	private FileStream _appInstanceLock;

	public string UsernameOverride;

	private List<Task> shutdownTasks = new List<Task>();

	private List<Action> _postInitActions = new List<Action>();

	public Action EnvironmentShutdownCallback;

	public Action EnvironmentCrashCallback;

	private UpdateStage stage;

	private Stopwatch stopwatch = new Stopwatch();

	private TimeSpan sum;

	private TimeSpan min;

	private TimeSpan max;

	private int sampleCount;

	private DateTime lastUpdateTime;

	private float deltaTime;

	private bool watchdogStarted;

	private Thread mainThread;

	private Stopwatch debugStopwatch = new Stopwatch();

	public static string[] UpdateStageNames = Enum.GetNames(typeof(UpdateStage));

	public double[] UpdateStageTimes = new double[Enum.GetValues(typeof(UpdateStage)).Length];

	public static bool IsAprilFools
	{
		get
		{
			if (IsAprilFools(DateTime.Now))
			{
				return true;
			}
			if (IsAprilFools(DateTime.UtcNow.AddHours(-9.0)))
			{
				return true;
			}
			return false;
			static bool IsAprilFools(DateTime time)
			{
				if (time.Day == 1)
				{
					return time.Month == 4;
				}
				return false;
			}
		}
	}

	public static Engine Current { get; private set; }

	public string AppName
	{
		get
		{
			if (Cloud.Engine.InUniverse)
			{
				return Cloud.Engine.Universe?.Name ?? Cloud.Platform.Name;
			}
			return Cloud.Platform.Name;
		}
	}

	/// <summary>
	/// Determines whether the engine should assume to use a Wine-based renderer when<br />
	/// executed in a Linux environment.
	///
	/// </summary>
	///
	/// <remarks>
	/// <b>Beware! This may be removed when dispatch of the renderer process is improved!</b>
	/// </remarks>
	public bool UseWineRenderer => Platform == Platform.Linux;

	public bool ShutdownRequested { get; private set; }

	public bool ShutdownCancelRequested { get; private set; }

	public CancellationToken EngineCancellationToken => _globalCancellationToken.Token;

	public bool IsReady { get; private set; }

	/// <summary>
	/// The Universe ID for this Engine.
	/// </summary>
	/// <remarks>If you're using this to check if the Engine is in a Universe,<br />
	/// then <see cref="P:FrooxEngine.Engine.InUniverse" /> is recommended as it makes Universe<br />
	/// checks easier to read.</remarks>
	public string UniverseId => Universe?.Id;

	public Guid UniqueSessionID { get; private set; }

	/// <summary>
	/// The full Universe details for this copy of the Engine.
	/// </summary>
	public Universe? Universe => Cloud.Apps.Universe;

	/// <summary>
	/// True, if this Engine is in a Universe.
	/// </summary>
	public bool InUniverse => Universe != null;

	public NetworkNodePreference NodePreference { get; set; }

	public string UID
	{
		get
		{
			return _uid;
		}
		set
		{
			_uid = value;
			HashedUID = CryptoHelper.HashIDToToken(value);
		}
	}

	public string HashedUID { get; private set; }

	public bool IsInitialized { get; private set; }

	public IPlatformProfile PlatformProfile { get; private set; }

	public static AppConfig Config { get; private set; }

	public IWebProxy WebProxy { get; private set; }

	public IEngineInitProgress InitProgress { get; private set; }

	public string InitPhase { get; internal set; }

	public string InitSubphase { get; internal set; }

	public string AppPath { get; private set; }

	public string DataPath { get; private set; }

	public string CachePath { get; private set; }

	public string LocalePath { get; private set; }

	public IReadOnlyList<string> AvailableLocales { get; private set; }

	public string ConfigFileRoot
	{
		get
		{
			if (Platform == Platform.Android)
			{
				return DataPath;
			}
			return AppPath;
		}
	}

	public string PrecachePath
	{
		get
		{
			if (Platform == Platform.Windows)
			{
				return Path.Combine(AppPath, "RuntimeData", "PreCache");
			}
			return null;
		}
	}

	public string AssemblyMetadataRoot
	{
		get
		{
			Platform platform = Platform;
			if (platform == Platform.Windows || platform == Platform.Linux)
			{
				return Path.Combine(AppPath, "RuntimeData", "AssemblyMetadata");
			}
			return null;
		}
	}

	public bool GeneratePrecache { get; private set; }

	public static string LatestVersion { get; private set; }

	public static List<string> ExtraAssemblies { get; private set; } = new List<string>();

	public float TotalEngineUpdateTime { get; private set; }

	public int UpdateTick { get; private set; }

	public int CameraRenders { get; private set; }

	public int PortalRenders { get; private set; }

	public int BlitRenders { get; private set; }

	public int DesktopRenders { get; private set; }

	public int AssetUpdates { get; private set; }

	public int MaterialUpdates { get; private set; }

	public int TextureUpdates { get; private set; }

	public int TextureSliceUpdates { get; private set; }

	public int MeshUpdates { get; private set; }

	public int SpriteUpdates { get; private set; }

	public int ParticleUploads { get; private set; }

	public int ParticleUploadCount { get; private set; }

	public int EmptyAudioReads { get; private set; }

	public int AudioReads { get; private set; }

	public int TotalStartedGatherJobs { get; private set; }

	public int TotalFailedGatherJobs { get; private set; }

	public int TotalCompletedGatherJobs { get; private set; }

	public static string CurrentVersion
	{
		get
		{
			if (_currentVersion == null)
			{
				_currentVersion = "Beta " + VersionNumber;
			}
			return _currentVersion;
		}
	}

	public static string VersionNumber
	{
		get
		{
			if (_versionNumber == null)
			{
				_versionNumber = Version.ToString();
			}
			return _versionNumber;
		}
	}

	public static VersionNumber Version
	{
		get
		{
			if (_version == default(VersionNumber))
			{
				_version = new VersionNumber(Assembly.GetExecutingAssembly().GetName().Version);
			}
			return _version;
		}
	}

	public string VersionString
	{
		get
		{
			if (_versionString == null)
			{
				if (ExtraAssemblies.Count == 0)
				{
					return VersionNumber;
				}
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.Append(VersionNumber);
				foreach (string extraAssembly in ExtraAssemblies)
				{
					stringBuilder.Append("+" + extraAssembly);
				}
				_versionString = stringBuilder.ToString();
			}
			return _versionString;
		}
	}

	public IEnumerable<FeatureFlag> FeatureFlags
	{
		get
		{
			yield return new FeatureFlag("ColorManagement", 0);
			yield return new FeatureFlag("ResetGUID", 0);
			yield return new FeatureFlag("ProtoFlux", 0);
			yield return new FeatureFlag("TEXTURE_QUALITY", 0);
			yield return new FeatureFlag("TypeManagement", 0);
			yield return new FeatureFlag("ALIGNER_FILTERING", 0);
			yield return new FeatureFlag("PhotonDust", 0);
			yield return new FeatureFlag("Awwdio", 0);
			yield return new FeatureFlag("NetCore", 0);
			yield return new FeatureFlag("RESONITE_LINK", 0);
		}
	}

	public InputInterface InputInterface { get; private set; }

	public WorldManager WorldManager { get; private set; }

	public SessionAnnouncer SessionAnnouncer { get; private set; }

	public NetworkManager NetworkManager { get; private set; }

	public RenderSystem RenderSystem { get; private set; }

	public AudioSystem AudioSystem { get; private set; }

	public AssetManager AssetManager { get; private set; }

	public LocalDB LocalDB { get; private set; }

	public WorkProcessor WorkProcessor { get; private set; }

	public EngineSkyFrostInterface Cloud { get; private set; }

	public SecurityManager Security { get; private set; }

	public CoroutineManager GlobalCoroutineManager { get; private set; }

	public RecordManager RecordManager { get; private set; }

	public PlatformInterface PlatformInterface { get; private set; }

	public ISystemInfo SystemInfo { get; private set; }

	public PerformanceStats PerfStats { get; private set; }

	public int ProcessorCount { get; private set; }

	public int? PhysicalProcessorCount { get; private set; }

	public Platform Platform => SystemInfo.Platform;

	public bool IsMobilePlatform => Platform.IsMobilePlatform();

	public bool IsWine { get; private set; }

	public string LocalUserName => UsernameOverride ?? Cloud.Session.CurrentUsername ?? PlatformInterface?.Username ?? Environment.MachineName;

	public bool IsRunningWithBootstrapper => SharedMemoryPrefix != null;

	public string SharedMemoryPrefix { get; private set; }

	public event Action LocalesUpdated;

	public event Action<string> OnShutdownRequest;

	public event Action OnReady;

	public event Action OnShutdown;

	public string GetLocaleNativeName(string locale)
	{
		int num = BUILT_IN_LOCALES.FindIndex((string s) => s == locale);
		if (num < 0)
		{
			return null;
		}
		return BUILT_IN_LOCALE_NATIVE_NAMES[num];
	}

	public string GetLocaleEnglishName(string locale)
	{
		int num = BUILT_IN_LOCALES.FindIndex((string s) => s == locale);
		if (num < 0)
		{
			return null;
		}
		return BUILT_IN_LOCALE_ENGLISH_NAMES[num];
	}

	public void CameraRendered()
	{
		_currentCameraRenders++;
	}

	public void PortalRendered()
	{
		_currentPortalRenders++;
	}

	public void BlitRendered()
	{
		_currentBlitRenders++;
	}

	public void DesktopRendered()
	{
		_currentDesktopRenders++;
	}

	public void MaterialUpdated()
	{
		_currentMaterialUpdates++;
	}

	public void TextureUpdated()
	{
		_currentTextureUpdates++;
	}

	public void TextureSliceUpdated()
	{
		_currentTextureSliceUpdates++;
	}

	public void MeshUpdated()
	{
		_currentMeshUpdates++;
	}

	public void SpriteUpdated()
	{
		_currentSpriteUpdates++;
	}

	public void ParticlesUploaded(int count)
	{
		_currentParticleUploads++;
		_currentParticleUploadCount += count;
	}

	public void EmptyAudioRead()
	{
		_currentEmptyAudioReads++;
	}

	public void AudioRead()
	{
		_currentAudioReads++;
	}

	public void AssetsUpdated(int count)
	{
		_currentAssetUpdates += count;
	}

	public void GatherJobStarted()
	{
		TotalStartedGatherJobs++;
	}

	public void GatherJobCompleted()
	{
		TotalCompletedGatherJobs++;
	}

	public void GatherJobFailed()
	{
		TotalFailedGatherJobs++;
	}

	private void AudioCycleFinished()
	{
		AudioReads = _currentAudioReads;
		EmptyAudioReads = _currentEmptyAudioReads;
		_currentAudioReads = 0;
		_currentEmptyAudioReads = 0;
	}

	public void BeginNewUpdate()
	{
		AssetUpdates = _currentAssetUpdates;
		CameraRenders = _currentCameraRenders;
		PortalRenders = _currentPortalRenders;
		BlitRenders = _currentBlitRenders;
		DesktopRenders = _currentDesktopRenders;
		MaterialUpdates = _currentMaterialUpdates;
		TextureUpdates = _currentTextureUpdates;
		TextureSliceUpdates = _currentTextureSliceUpdates;
		MeshUpdates = _currentMeshUpdates;
		SpriteUpdates = _currentSpriteUpdates;
		ParticleUploads = _currentParticleUploads;
		ParticleUploadCount = _currentParticleUploadCount;
		_currentAssetUpdates = 0;
		_currentCameraRenders = 0;
		_currentPortalRenders = 0;
		_currentBlitRenders = 0;
		_currentDesktopRenders = 0;
		_currentMaterialUpdates = 0;
		_currentTextureUpdates = 0;
		_currentTextureSliceUpdates = 0;
		_currentMeshUpdates = 0;
		_currentSpriteUpdates = 0;
		_currentParticleUploads = 0;
		_currentParticleUploadCount = 0;
	}

	[Conditional("PROFILE")]
	public void ProfilerRegisterThread(string name)
	{
		SystemInfo.RegisterThread(name ?? "UNNAMED");
	}

	[Conditional("PROFILE")]
	public void ProfilerBeginSample(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			name = "UNKNOWN";
		}
		SystemInfo.BeginSample(name);
	}

	[Conditional("PROFILE")]
	public void ProfilerNextSample(string name)
	{
		SystemInfo.EndSample();
		SystemInfo.BeginSample(name);
	}

	[Conditional("PROFILE")]
	public void ProfilerEndSample()
	{
		SystemInfo.EndSample();
	}

	[DllImport("ntdll.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "wine_get_version")]
	private static extern string GetWineVersion();

	public void RunPostInit(Action action)
	{
		lock (_postInitActions)
		{
			_postInitActions.Add(action);
		}
	}

	public async Task Initialize(string appPath, bool useRenderer, LaunchOptions options, ISystemInfo systemInfo, IEngineInitProgress progress)
	{
		if (IsInitialized)
		{
			throw new InvalidOperationException("Engine already initialized");
		}
		Stopwatch initStopwatch = Stopwatch.StartNew();
		Current = this;
		UniqueSessionID = Guid.NewGuid();
		InitProgress?.SetFixedPhase("Initializing Runtime Parameters...");
		AppPath = appPath;
		DataPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(options.DataDirectory));
		CachePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(options.CacheDirectory));
		if (string.IsNullOrWhiteSpace(options.LocaleDirectory))
		{
			LocalePath = Path.Combine(AppPath, "Locale");
		}
		else
		{
			LocalePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(options.LocaleDirectory));
		}
		UniLog.Log($"AppPath: {AppPath}\nDataPath: {DataPath}\nCachePath: {CachePath}\nLocalePath: {LocalePath}");
		InitProgress = progress;
		VerboseInit = options.VerboseInit;
		SystemInfo = systemInfo;
		PerfStats = new PerformanceStats(this);
		ProcessorCount = Environment.ProcessorCount;
		PhysicalProcessorCount = systemInfo.PhysicalCores;
		GeneratePrecache = options.GeneratePrecache;
		if (Platform == Platform.Android)
		{
			VerboseInit = true;
		}
		_globalCancellationToken = new CancellationTokenSource();
		UniLog.Log("Initializing App: " + CurrentVersion + "\nRuntime version: " + Environment.Version?.ToString() + "\n" + systemInfo.PrintSystemInfo() + "\nMax GC Generation: " + GC.MaxGeneration + ", IsLittleEndian: " + BitConverter.IsLittleEndian + $"\nSystem.Numerics.Vectors HW accelerated: {Vector.IsHardwareAccelerated}, Vector<float>.Count: {Vector<float>.Count}" + $"\nBrotli native encoding/decoding supported: {DataTreeConverter.NativeBrotliSupported}\n" + $"Unique Session ID: {UniqueSessionID}");
		await SetupExceptionHandler();
		SkyFrostConfig config = options.OverrideCloudProfile;
		if (config == null)
		{
			config = options.CloudProfile switch
			{
				CloudProfile.Production => SkyFrostConfig.DEFAULT_PRODUCTION, 
				CloudProfile.ProductionDirect => SkyFrostConfig.DEFAULT_PRODUCTION_DIRECT, 
				CloudProfile.Staging => SkyFrostConfig.DEFAULT_STAGING, 
				CloudProfile.Local => SkyFrostConfig.DEFAULT_LOCAL, 
				_ => throw new ArgumentException("Invalid cloud profile: " + options.CloudProfile), 
			};
		}
		if (options.ForceSignalRLongPolling)
		{
			config = config.WithSignalRLongPolling();
		}
		PlatformProfile = config.Platform;
		SharedMemoryPrefix = options.SharedMemoryPrefix;
		Bitmap2D bitmap2D = null;
		if (options.OverrideRendererIcon != null && File.Exists(options.OverrideRendererIcon))
		{
			bitmap2D = Bitmap2D.Load(Path.GetFullPath(Environment.ExpandEnvironmentVariables(options.OverrideRendererIcon)), false);
			if ((bitmap2D.Size != 128).Any())
			{
				bitmap2D = bitmap2D.GetRescaled(128, 128);
			}
		}
		await InitializeRenderSystem(useRenderer, options.OutputDevice, UniqueSessionID, bitmap2D, options.OverrideSplashScreen);
		await ProcessStartupCommands(options);
		await DetectWine();
		if (PrecachePath != null && !Directory.Exists(PrecachePath))
		{
			Directory.CreateDirectory(PrecachePath);
		}
		await InitializeLocales();
		await LoadConfig(options);
		await InitializeProtocolRegistration();
		await InitializeAssemblies(options);
		await LoadProxySettings();
		await InitializeFrooxEngine(options);
		await InitializeInputInterface(options);
		await InitializeLocalDB(options);
		await InitializeWorkProcessor(options);
		await InitializeAudioSystem(options);
		await InitializeGlobalCoroutineManager();
		await InitializeWorldManager();
		await InitializeNetworkManager();
		await InitializeSecurityManager();
		await InitializePlatformInterface();
		string text = Config.UniverseId ?? options.UniverseId ?? null;
		if (!string.IsNullOrEmpty(text))
		{
			UniLog.Log("Detected UniverseID: " + text);
			config = config.WithUniverse(text);
		}
		if (Config?.Proxy != null)
		{
			config = config.WithProxy(Config.Proxy);
		}
		await InitializeSkyFrost(options, config);
		await InitializeAssetManager();
		await InitializeRecordManager();
		await InitializeSessionAnnouncer();
		await InitializeInputDrivers(options);
		await FinishInitialization(options);
		UniLog.Log("FrooxEngine Initialized in " + initStopwatch.GetElapsedMilliseconds() + " ms");
	}

	public void EnsureInitializedSDL()
	{
		if (!_SDLinitialized)
		{
			_SDLinitialized = true;
			string text = string.Join('.', PlatformProfile.Domain.Split('.').Reverse());
			SDL.SetAppMetadata(PlatformProfile.Name, VersionNumber, text + "." + PlatformProfile.Name);
		}
	}

	private async Task InitializeProtocolRegistration()
	{
		if (ProtocolRegistration.ShouldRegister(Config, Platform, PlatformProfile))
		{
			UniLog.Log("Attempting to register Operating System protocols for " + PlatformProfile.Name);
			if (await ProtocolRegistration.Register(AppPath, PlatformProfile))
			{
				UniLog.Log("Successfully registered Operating System protocols for " + PlatformProfile.Name);
			}
			else
			{
				UniLog.Log("Failed to register Operating System protocols for " + PlatformProfile.Name);
			}
		}
	}

	private async Task SetupExceptionHandler()
	{
		AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
		try
		{
			if (!Directory.Exists(DataPath))
			{
				Directory.CreateDirectory(DataPath);
			}
			if (!Directory.Exists(CachePath))
			{
				Directory.CreateDirectory(CachePath);
			}
			_appInstanceLock = File.Open(Path.Combine(DataPath, "Instance.lock"), FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
		}
		catch (IOException)
		{
			UniLog.Warning("Another instance of is already running with same data path (" + DataPath + "), shutting down...");
			throw new DuplicateInstanceException();
		}
	}

	private async Task DetectWine()
	{
		if (Platform == Platform.Windows)
		{
			try
			{
				string wineVersion = GetWineVersion();
				UniLog.Log("Detected Wine version: " + wineVersion);
				IsWine = true;
			}
			catch
			{
			}
		}
	}

	private async Task ProcessStartupCommands(LaunchOptions options)
	{
		InitProgress?.SetFixedPhase("Processing startup commands...");
		if (options.AdditionalAssemblies.Count <= 0)
		{
			return;
		}
		string text = Directory.EnumerateFiles(AppPath, "*FrooxEngine.dll", SearchOption.AllDirectories).FirstOrDefault((string f) => Path.GetFileName(f) == "FrooxEngine.dll");
		if (text == null)
		{
			throw new FileNotFoundException("Could not find FrooxEngine.dll");
		}
		text = Path.GetDirectoryName(text);
		options.AdditionalAssemblies.RemoveAll((string f) => !File.Exists(f));
		foreach (string additionalAssembly in options.AdditionalAssemblies)
		{
			InitSubphase = "Processing " + Path.GetFileName(additionalAssembly) + "...";
			if (AssemblyPostProcessor.Process(additionalAssembly, text))
			{
				UniLog.Log("POSTX Processed Assembly: " + additionalAssembly);
			}
		}
	}

	private async Task InitializeLocales()
	{
		InitProgress?.SetFixedPhase("Scanning locales...");
		List<string> list = new List<string>();
		if (Directory.Exists(LocalePath))
		{
			foreach (string item in Directory.EnumerateFiles(LocalePath, "*.json"))
			{
				list.Add(Path.GetFileNameWithoutExtension(item));
			}
		}
		else
		{
			list.AddRange(BUILT_IN_LOCALES);
		}
		list.Sort();
		UniLog.Log("Available locales: " + string.Join(", ", list));
		AvailableLocales = list;
		try
		{
			_localeChangeWatcher = new FileSystemWatcher(LocalePath);
			_localeChangeWatcher.EnableRaisingEvents = true;
			_localeChangeWatcher.Changed += delegate(object sender, FileSystemEventArgs e)
			{
				if (e.ChangeType == WatcherChangeTypes.Changed)
				{
					UniLog.Log("Locale file changed, forcing reload: " + e.FullPath);
					this.LocalesUpdated?.Invoke();
				}
			};
		}
		catch (Exception)
		{
		}
	}

	private async Task LoadConfig(LaunchOptions options)
	{
		if (string.IsNullOrWhiteSpace(options.EngineConfigFile))
		{
			options.EngineConfigFile = "Config.json";
		}
		InitProgress?.SetFixedPhase("Loading " + options.EngineConfigFile + "...");
		if (!Path.IsPathRooted(options.EngineConfigFile))
		{
			options.EngineConfigFile = Path.Combine(ConfigFileRoot, options.EngineConfigFile);
		}
		if (File.Exists(options.EngineConfigFile))
		{
			try
			{
				UniLog.Log("Parsing Config file: " + options.EngineConfigFile);
				Config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(options.EngineConfigFile));
				UniLog.Log("Trigger Deadzone: " + Config?.Inputs?.TriggerDeadzone);
			}
			catch (Exception ex)
			{
				UniLog.Warning("Excepting parsing the config file: " + ex);
			}
		}
		if (Config == null)
		{
			Config = new AppConfig();
		}
		if (IsWine || Current.Platform != Platform.Windows)
		{
			Config.DisableDesktop = true;
		}
		NodePreference = Config.NodePreference;
		if (options.NeverSaveSettings)
		{
			SettingManagersManager.NeverSave = true;
		}
		SettingManagersManager.RestoreCloudSettings = options.RestoreCloudSettingsFile;
		if (options.NeverSaveDash)
		{
			UniLog.Log("Flagging dash to never be saved");
			UserspaceRadiantDash.NeverSave = true;
		}
		if (options.DisablePlatformInterfaces)
		{
			Config.DisablePlatformInterfaces = true;
		}
	}

	private async Task LoadProxySettings()
	{
		WebProxy webProxy = WebProxyUtility.CreateProxy(Config?.Proxy);
		if (webProxy == null)
		{
			UniLog.Log("Engine WebProxy initialization failed.");
			return;
		}
		UniLog.Log("Engine WebProxy initialization complete.");
		WebProxy = webProxy;
	}

	private async Task InitializeAssemblies(LaunchOptions options)
	{
		InitProgress?.SetFixedPhase("Computing compatibility hash...");
		options.AdditionalAssemblies.Sort();
		foreach (string additionalAssembly in options.AdditionalAssemblies)
		{
			try
			{
				InitSubphase = Path.GetFileName(additionalAssembly);
				Assembly.LoadFrom(additionalAssembly);
				UniLog.Log("Loaded Extra Assembly: " + additionalAssembly);
				ExtraAssemblies.Add(Path.GetFileName(additionalAssembly));
			}
			catch (Exception ex)
			{
				UniLog.Log("Could not load extra assembly: " + additionalAssembly + "\n\n" + ex);
			}
		}
	}

	private async Task InitializeFrooxEngine(LaunchOptions options)
	{
		if (!EngineInitializer.FrooxEngineInitialized)
		{
			InitProgress?.SetFixedPhase("Initializing FrooxEngine...");
			try
			{
				await EngineInitializer.InitializeFrooxEngine(this, options);
			}
			catch (Exception ex)
			{
				UniLog.Error("Exception initializing FrooxEngine:\n" + ex);
				throw;
			}
		}
	}

	private async Task InitializeInputInterface(LaunchOptions options)
	{
		InitProgress?.SetFixedPhase("Initializing InputInterface...");
		InputInterface = new InputInterface(this, (await RenderSystem.WaitForRendererInit()) ?? options.OutputDevice);
	}

	private async Task InitializeInputDrivers(LaunchOptions options)
	{
		InitProgress?.SetFixedPhase("Initializing Input Drivers...");
		if (InputInterface.HeadOutputDevice == HeadOutputDevice.Headless)
		{
			return;
		}
		InputInterface.RegisterPostInputStateTask(delegate
		{
			if (ViveProEyeTrackingDriver.ShouldRegister(options, InputInterface))
			{
				InputInterface.RegisterInputDriver(new ViveProEyeTrackingDriver());
			}
			if (OmniceptTrackingDriver.ShouldRegister(InputInterface))
			{
				InputInterface.RegisterInputDriver(new OmniceptTrackingDriver());
			}
		});
		if (LeapMotionDriver.IsSupported(this))
		{
			InputInterface.RegisterInputDriver(new LeapMotionDriver());
		}
		InputInterface.RegisterInputDriver(new BHapticsDriver());
		InputInterface.RegisterInputDriver(new OWO_Driver());
		InputInterface.RegisterInputDriver(new GiggleTechDriver());
		if (options.ForceBabble || BabbleOSC_Driver.ShouldInitialize)
		{
			UniLog.Log("Registering Babble driver");
			InputInterface.RegisterInputDriver(new BabbleOSC_Driver());
		}
	}

	private async Task InitializeLocalDB(LaunchOptions options)
	{
		InitProgress?.SetFixedPhase("Initializing LocalDB...");
		LocalDB = new LocalDB(null, DataPath, CachePath, new HashSet<string> { PrecachePath });
		await LocalDB.Initialize(new EngineInitProgressWrapper(InitProgress));
		AssetVariantHelper.TempFolder = LocalDB.AssetCachePath;
		InitProgress?.SetFixedPhase("Processing LocalDB export");
		if (options.LocalDatabaseExport != null)
		{
			LocalDatabaseAccountDataStore source = new LocalDatabaseAccountDataStore(LocalDB, PlatformProfile, options.LocalDatabaseExport.MachineOnly);
			LocalAccountDataStore target = new LocalAccountDataStore(PlatformProfile, LocalDB.MachineID, Path.Combine(options.LocalDatabaseExport.ExportPath, "Data"), Path.Combine(options.LocalDatabaseExport.ExportPath, "Assets"));
			AccountMigrationConfig accountMigrationConfig = new AccountMigrationConfig();
			accountMigrationConfig.ClearAll();
			accountMigrationConfig.MigrateUserRecords = true;
			accountMigrationConfig.MigrateGroups = true;
			AccountTransferController accountTransferController = new AccountTransferController(source, target, Guid.CreateVersion7().ToString(), accountMigrationConfig);
			accountTransferController.ProgressMessagePosted += delegate(string str)
			{
				InitProgress.SetSubphase(str, alwaysShow: true);
			};
			await accountTransferController.Transfer(CancellationToken.None);
		}
	}

	private async Task InitializeWorkProcessor(LaunchOptions options)
	{
		InitProgress?.SetFixedPhase("Initializing WorkProcessor...");
		int num = PhysicalProcessorCount ?? ProcessorCount;
		WorkProcessor = new WorkProcessor(MathX.Max(1, options.BackgroundWorkerCount ?? num), MathX.Max(1, options.PriorityWorkerCount ?? (num - 1)));
		UniLog.Log($"Initialized WorkProcessor. Background Workers: {WorkProcessor.WorkerCount}, Priority Workers: {WorkProcessor.HighPriorityWorkerCount}");
	}

	private async Task InitializeRenderSystem(bool useRenderer, HeadOutputDevice headOutputDevice, Guid uniqueSessionId, Bitmap2D rendererIcon = null, SplashScreenDescriptor splashScreenOverride = null)
	{
		InitProgress?.SetFixedPhase("Initializing RenderSystem...");
		RenderSystem = new RenderSystem();
		InitProgress = await RenderSystem.Initialize(this, headOutputDevice, uniqueSessionId, useRenderer, rendererIcon, splashScreenOverride, InitProgress);
	}

	private async Task InitializeAudioSystem(LaunchOptions options)
	{
		InitProgress?.SetFixedPhase("Initializing AudioSystem...");
		AudioSystem = new AudioSystem();
		await AudioSystem.Initialize(this, options);
		AudioSystem.AudioUpdate += AudioCycleFinished;
	}

	private async Task InitializeGlobalCoroutineManager()
	{
		InitProgress?.SetFixedPhase("Initializing GlobalCoroutineManager...");
		GlobalCoroutineManager = new CoroutineManager(this, null);
	}

	private async Task InitializeWorldManager()
	{
		InitProgress?.SetFixedPhase("Initializing WorldManager...");
		WorldManager = new WorldManager();
		await WorldManager.Initialize(this);
		AudioSystem.AudioUpdate += WorldManager.RunAudioUpdates;
	}

	private async Task InitializeNetworkManager()
	{
		InitProgress?.SetFixedPhase("Initializing NetworkManager...");
		NetworkManager = new NetworkManager(PlatformProfile, Config);
	}

	private async Task InitializeSecurityManager()
	{
		InitProgress?.SetFixedPhase("Initializing SecurityManager...");
		Security = new SecurityManager(this);
	}

	private async Task InitializePlatformInterface()
	{
		InitProgress?.SetFixedPhase("Initializing PlatformInterface...");
		PlatformInterface = new PlatformInterface();
		await PlatformInterface.Initialize(this);
		List<string> list = new List<string>();
		NetworkManager.GetSupportedSchemes(list);
		UniLog.Log("Supported network protocols: " + string.Join(", ", list));
	}

	private async Task InitializeSkyFrost(LaunchOptions options, SkyFrostConfig config)
	{
		InitProgress?.SetFixedPhase("Initializing SkyFrost Interface...");
		SkyFrostInterface.MemoryStreamAllocator = () => SyncMessage.StreamManager.GetStream();
		SkyFrostInterface.ProfilerBeginSampleCallback = delegate
		{
		};
		SkyFrostInterface.ProfilerEndSampleCallback = delegate
		{
		};
		if (SystemInfo.IsAOT)
		{
			UniLog.Log("Running on AOT platform, switching libraries to AOT mode");
			SkyFrostInterface.UseNewtonsoftJson = true;
			FreeTypeFont.UseStaticCallbacks = true;
		}
		if (config == null)
		{
			config = SkyFrostConfig.DEFAULT_PRODUCTION;
		}
		if (Platform == Platform.Linux)
		{
			config.GZip = false;
		}
		UID = await Task.Run(delegate
		{
			try
			{
				return SkyFrost.Base.UID.Compute();
			}
			catch (Exception value)
			{
				UniLog.Warning($"Exception computing UID, using fallback.\n{value}");
			}
			return CryptoHelper.HashIDToToken(LocalDB.MachineID);
		});
		config.NodePreference = NodePreference;
		Cloud = await EngineSkyFrostInterface.Create(this, UID, config);
		Cloud.Status.ForceInvisible = options.StartInvisible;
		LocalDB.AssignCloud(Cloud);
	}

	private async Task InitializeAssetManager()
	{
		InitProgress?.SetFixedPhase("Initializing AssetManager...");
		AssetManager = new AssetManager();
		await AssetManager.Initialize(this);
	}

	private async Task InitializeRecordManager()
	{
		InitProgress?.SetFixedPhase("Initializing RecordManager...");
		RecordManager = new RecordManager(this);
	}

	private async Task InitializeSessionAnnouncer()
	{
		InitProgress?.SetFixedPhase("Initializing SessionAnnouncer...");
		SessionAnnouncer = new SessionAnnouncer(this);
	}

	private async Task CloudUpdateLoop()
	{
		while (!EngineCancellationToken.IsCancellationRequested)
		{
			try
			{
				Cloud.Update();
			}
			catch (Exception ex)
			{
				UniLog.Error("Exception when updating Cloud Interface:\n" + ex, stackTrace: false);
			}
			await Task.Delay(100, EngineCancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	private async Task FinishInitialization(LaunchOptions options)
	{
		InitProgress?.SetFixedPhase("Finalizing initialization");
		await RenderSystem.FinishInitialize();
		Userspace.DoNotAutoLoadHome = options.DoNotAutoLoadHome;
		foreach (Action postInitAction in _postInitActions)
		{
			postInitAction();
		}
		_postInitActions = null;
		LocaleResource.UpdateGlobalLocaleArguments(this);
		Task.Run((Func<Task?>)CloudUpdateLoop);
		InitProgress?.SetFixedPhase("FrooxEngine Initialization Finished");
		SetReady();
		IsInitialized = true;
	}

	private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
	{
		if (e.ExceptionObject is FrooxEngineInitializationException ex)
		{
			UniLog.Error(ex.Format(Cloud?.Platform), stackTrace: false);
		}
		else
		{
			UniLog.Error("Unhandled Exception:\n\n" + e.ExceptionObject, stackTrace: false);
		}
	}

	public void RunUpdateLoop()
	{
		mainThread = Thread.CurrentThread;
		if (!watchdogStarted)
		{
			watchdogStarted = true;
			Task.Run(async delegate
			{
				CancellationToken token = EngineCancellationToken;
				int count = 0;
				while (!token.IsCancellationRequested)
				{
					await Task.Delay(TimeSpan.FromSeconds(1L), token).ConfigureAwait(continueOnCapturedContext: false);
					double totalSeconds = (DateTime.UtcNow - lastUpdateTime).TotalSeconds;
					if (totalSeconds >= 10.0)
					{
						World world = WorldManager?.FocusedWorld;
						if (IsReady && world != null)
						{
							UniLog.Log("Aborting ProtoFlux contexts in " + world.Name);
							world.ProtoFlux.AbortAllContexts();
						}
						UniLog.Warning($"Engine has been unresponsive for over {totalSeconds:F2} seconds.\nLastUpdateTime: {lastUpdateTime}\nFocusedWorld: {world}\nWorldStage: {world?.StageDEBUG}\nWorldSessionState: {world?.Session?.Sync.SyncLoopStage}\nWorldSessionStopProcessing: {world?.Session?.Sync.StopProcessingFlag}\nWorldMessagesToProcess: {world?.Session?.Sync.MessagesToProcessCount}\nWorldTotalProcessedMessages: {world?.Session?.Sync.TotalProcessedMessages}\nWorldMessagesToTransmit: {world?.Session?.Messages.Outgoing.MessagesToTransmitCount}\nCurrentlyProcessingSyncMessage: {world?.Session?.Sync.CurrentlyProcessingSyncMessage?.ToString()}\nCurrentlyDecodingStream: {world?.SyncController?.CurrentlyDecodingStream}");
						if (count++ >= 3 && mainThread != null)
						{
							SyncMessage syncMessage = world?.Session?.Sync.CurrentlyProcessingSyncMessage;
							if (syncMessage != null)
							{
								string tempFilePath = LocalDB.GetTempFilePath();
								UniLog.Log("Dumping current sync message to: " + tempFilePath);
								RawOutMessage rawOutMessage = syncMessage.Encode();
								File.WriteAllBytes(tempFilePath, rawOutMessage.Data.ToArray());
							}
						}
						await Task.Delay(TimeSpan.FromSeconds(10L), token).ConfigureAwait(continueOnCapturedContext: false);
					}
					else
					{
						count = 0;
					}
				}
			});
		}
		UpdateTick++;
		BeginNewUpdate();
		stopwatch.Restart();
		do
		{
			int num = (int)stage;
			debugStopwatch.Restart();
			try
			{
				UpdateStep();
			}
			catch (Exception value)
			{
				UniLog.Error($"Exception when running engine update step. Stage: {stage}\n{value}", stackTrace: false);
				throw;
			}
			debugStopwatch.Stop();
			UpdateStageTimes[num] = debugStopwatch.Elapsed.TotalSeconds;
		}
		while (stage != UpdateStage.Finished);
		stage = UpdateStage.UpdateBegin;
		stopwatch.Stop();
		TotalEngineUpdateTime = (float)(stopwatch.GetElapsedMilliseconds() * 0.0010000000474974513);
		if (!LogUpdateIntervals)
		{
			return;
		}
		if (sampleCount == 0)
		{
			sum = (min = (max = stopwatch.Elapsed));
		}
		else
		{
			sum += stopwatch.Elapsed;
			if (stopwatch.Elapsed < min)
			{
				min = stopwatch.Elapsed;
			}
			if (stopwatch.Elapsed > max)
			{
				max = stopwatch.Elapsed;
			}
		}
		sampleCount++;
		TimeSpan value2 = TimeSpan.FromTicks(sum.Ticks / sampleCount);
		UniLog.Log($"Update Time: {stopwatch.Elapsed}, Avg: {value2}, Min: {min}, Max: {max}");
	}

	public void SetReady()
	{
		if (!IsReady)
		{
			UniLog.Log("Engine Ready!");
			IsReady = true;
			this.OnReady?.Invoke();
			InitProgress?.EngineReady();
		}
	}

	public void RequestShutdown()
	{
		if (!ShutdownRequested)
		{
			UniLog.Log("Shutdown requested", stackTrace: true);
			UniLog.Flush();
			ShutdownRequested = true;
			this.OnShutdownRequest?.Invoke("Quitting");
			if (ShutdownCancelRequested)
			{
				ShutdownCancelRequested = false;
				ShutdownRequested = false;
				UniLog.Log("Shutdown canceled");
			}
			else
			{
				UniLog.Log("Shutting down");
				this.OnShutdown?.Invoke();
				Task.WhenAll(shutdownTasks).Wait(MaxShutdownWaitMilliseconds);
				UniLog.Log("Requesting environment shutdown");
				EnvironmentShutdownCallback();
			}
		}
	}

	public void ForceCrash()
	{
		UniLog.Log("<FORCE CRASH>");
		UniLog.Flush();
		EnvironmentCrashCallback?.Invoke();
	}

	public void CancelShutdown()
	{
		ShutdownCancelRequested = true;
	}

	public void RegisterShutdownTask(Task task)
	{
		shutdownTasks.Add(task);
	}

	public void Dispose()
	{
		_globalCancellationToken.Cancel();
		WorldManager.Dispose();
		LocalDB.Dispose();
		AssetManager.Dispose();
		WorkProcessor.Dispose();
		SessionAnnouncer.Dispose();
		InputInterface.Dispose();
		PlatformInterface.Dispose();
		_localeChangeWatcher?.Dispose();
		_appInstanceLock.Dispose();
	}

	private void HandleFrameStart(FrameStartData startData)
	{
		if (startData.inputs != null)
		{
			InputInterface.ScheduleInputStateProcessing(startData.inputs);
		}
		if (startData.performance != null)
		{
			PerfStats.Update(startData.performance);
		}
		if (startData.videoClockErrors != null)
		{
			RenderSystem.HandleVideoClockErrors(startData.videoClockErrors);
		}
		if (startData.renderedReflectionProbes != null)
		{
			HandleFinishedReflectionProbes(startData.renderedReflectionProbes);
		}
	}

	private void HandleFinishedReflectionProbes(List<ReflectionProbeChangeRenderResult> probes)
	{
		foreach (ReflectionProbeChangeRenderResult probe in probes)
		{
			WorldManager.GetWorld(probe.renderSpaceId)?.Render.ReflectionProbes.ProbeRenderFinished(probe.renderProbeUniqueId, probe.requireReset);
		}
	}

	private void UpdateStep()
	{
		switch (stage)
		{
		case UpdateStage.UpdateBegin:
			if (AutoReadyAfterUpdates >= 0 && --AutoReadyAfterUpdates < 0)
			{
				List<World> list = Pool.BorrowList<World>();
				WorldManager.GetWorlds(list);
				foreach (World item in list)
				{
					World _w = item;
					_w.RunSynchronously(delegate
					{
						ProtoFluxHelper.DynamicImpulseHandler.TriggerDynamicImpulse(_w.RootSlot, "EngineReady", excludeDisabled: false);
					});
				}
			}
			WorkProcessor.Update();
			AudioSystem.Update();
			SessionAnnouncer.Update();
			PlatformInterface.Update();
			RecordManager.Update();
			RenderSystem.Update();
			stage++;
			break;
		case UpdateStage.FrameStart:
		{
			FrameStartData frameStartData = RenderSystem.WaitForFrameBegin();
			if (frameStartData != null)
			{
				HandleFrameStart(frameStartData);
			}
			stage++;
			break;
		}
		case UpdateStage.InputUpdate:
		{
			DateTime utcNow = DateTime.UtcNow;
			deltaTime = (float)(utcNow - lastUpdateTime).TotalMilliseconds / 1000f;
			lastUpdateTime = utcNow;
			InputInterface.Update(deltaTime);
			stage++;
			break;
		}
		case UpdateStage.GlobalCoroutinesUpdate:
			GlobalCoroutineManager.ExecuteWorldQueue(deltaTime);
			stage++;
			break;
		case UpdateStage.WorldUpdate:
			if (WorldManager.RunUpdateLoop())
			{
				stage++;
			}
			break;
		case UpdateStage.RenderSubmit:
			RenderSystem.SubmitFrame();
			stage++;
			break;
		case UpdateStage.AssetUpdate:
			AssetManager.Update(TimesliceAssetIntegration ? 2.0 : double.MaxValue, TimesliceParticleIntegration ? 6.0 : double.MaxValue);
			stage++;
			break;
		}
	}
}
