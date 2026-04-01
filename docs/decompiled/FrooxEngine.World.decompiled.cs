using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Elements.Assets;
using Elements.Core;
using EnumsNET;
using FrooxEngine.PhotonDust;
using FrooxEngine.ProtoFlux;
using FrooxEngine.Store;
using FrooxEngine.Undo;
using Renderite.Shared;
using SkyFrost.Base;

namespace FrooxEngine;

public class World : IWorldElement
{
	public enum WorldFocus
	{
		Background,
		Focused,
		Overlay,
		PrivateOverlay
	}

	public enum WorldState
	{
		Initializing,
		Running,
		Failed
	}

	public enum InitializationState
	{
		Created,
		InitializingNetwork,
		WaitingForJoinGrant,
		InitializingDataModel,
		Finished,
		Failed
	}

	public enum FailReason
	{
		None,
		NetworkError,
		JoinRejected,
		AuthenticationError,
		SecurityIssue,
		CompatibilityError,
		UnhandledError
	}

	public enum WorldEvent
	{
		OnFocusChanged,
		OnUserJoined,
		OnUserSpawn,
		OnUserLeft,
		OnWorldSaved,
		OnWorldDestroy
	}

	private readonly struct SynchronousAction
	{
		public readonly IUpdatable updatable;

		public readonly Action action;

		public readonly bool evenDisposed;

		public SynchronousAction(Action action, IUpdatable updatable, bool evenDisposed)
		{
			this.action = action;
			this.updatable = updatable;
			this.evenDisposed = evenDisposed;
		}
	}

	public enum RefreshStage
	{
		RefreshBegin,
		UpdatingStreams,
		PhysicsSync,
		PhysicsMoved,
		PhysicsUpdate,
		RunningStartups,
		WorldEvents,
		RunningEvents,
		Input,
		Coroutines,
		Updates,
		ProtoFluxRebuild,
		ProtoFluxEvents,
		ProtoFluxUpdates,
		ProtoFluxContinuousChanges,
		ProtoFluxDiscreteChangesPre,
		Changes,
		Destructions,
		ProtoFluxDiscreteChangesPost,
		PhysicsSchleduleRefine,
		MovedSlots,
		UserPose,
		AudioSystem,
		ParticleSystems,
		MaterialUpdate,
		RenderUpdate,
		ValidatingPermissions,
		Finished,
		SynchronousActions
	}

	private const string FIXED_SALT = "06a92b0d-57b3-426f-9cc3-2f812a765554";

	public const int UPDATE_TIMES_HISTORY = 90;

	private object stateLock = new object();

	public readonly int LocalWorldHandle;

	public readonly WorldManager WorldManager;

	public readonly WorldConfiguration Configuration;

	private ushort[][] _randomizationTables;

	public bool ForceFullUpdateCycle;

	public bool IsRunningLongBlockingTask;

	public readonly bool UnsafeMode;

	public bool ForceAnnounceOnWAN;

	public bool SaveOnExit;

	public string AllowUserCloudVariable;

	public string DenyUserCloudVariable;

	public string RequiredUserJoinCloudVariable;

	public string RequiredUserJoinCloudVariableDenyMessage;

	private HashSet<string> _allowedUsers = new HashSet<string>();

	private HashSet<string> _inviteRequestHandlers = new HashSet<string>();

	private SpinQueue<SynchronousAction> synchronousActions = new SpinQueue<SynchronousAction>();

	private WorldFocus _focus;

	private List<Uri> _sourceURLs;

	private World _parent;

	private FrooxEngine.Store.Record _record;

	public bool BlockAutoRespawn;

	private string _lastName;

	private string _lastUnstrippedName;

	private string _lastStrippedName;

	private SlotBag _slots;

	private UserBag _users;

	private SyncRefDictionary<string, Component> _keys;

	private SyncFieldDictionary<string, int> _keyVersions;

	private List<Slot> _localSlots = new List<Slot>();

	private WorldAction worldInitAction;

	private DataTreeNode worldInitLoad;

	private ReferenceTranslator refTranslator;

	private Dictionary<Type, HashSet<Worker>> _globallyRegisteredComponents = new Dictionary<Type, HashSet<Worker>>();

	private int _graceFullUpdateCycles;

	private int _stageUpdateTimeCount;

	private double[] _minStageUpdateTimeOngoing = new double[Enum.GetValues(typeof(RefreshStage)).Length];

	private double[] _maxStageUpdateTimeOngoing = new double[Enum.GetValues(typeof(RefreshStage)).Length];

	private double[] _avgStageUpdateTimeOngoing = new double[Enum.GetValues(typeof(RefreshStage)).Length];

	private int _lastAudioStreamUnderruns;

	private double sumUpdateTime;

	private volatile int audioConfigurationChanged;

	private Stopwatch stopwatch = new Stopwatch();

	private Stopwatch stageStopwatch = new Stopwatch();

	private Stopwatch audioStopwatch = new Stopwatch();

	internal bool debugLogUpdateTimes;

	private static string[] updateStageNames = Enum.GetNames(typeof(RefreshStage));

	private DateTime _lastStatsUpdate;

	private Dictionary<string, Component> _locallyRegisteredComponents = new Dictionary<string, Component>();

	private Dictionary<ulong, Dictionary<RefID, ISyncMember>> trashbin;

	private Dictionary<Type, Action<Slot, Component>> _componentAddedEvents = new Dictionary<Type, Action<Slot, Component>>();

	private Dictionary<Type, Action<Slot, Component>> _componentRemovedEvents = new Dictionary<Type, Action<Slot, Component>>();

	private List<IWorldEventReceiver>[] worldEventReceivers;

	private bool isInitialJoinBatch = true;

	private List<User> joinedUsers = new List<User>();

	private HashSet<User> spawnUsers = new HashSet<User>();

	private List<User> leftUsers = new List<User>();

	private bool worldSaved;

	private WorldFocus _lastFocus;

	private static int worldEventTypeCount = Enum.GetValues(typeof(WorldEvent)).Length;

	public Action DisconnectRequestedHook;

	public Action HostConnectionClosedHook;

	private Dictionary<RefID, User> _userSnapshot = new Dictionary<RefID, User>();

	private User _hostUser;

	private Slot _assets;

	private Slot _localAssets;

	private volatile bool _disposed;

	public Engine Engine => WorldManager.Engine;

	internal int RandomizationSeed { get; private set; }

	internal byte[] Obfuscation_KEY { get; private set; }

	internal byte[] Obfuscation_IV { get; private set; }

	public SessionAccessLevel AccessLevel
	{
		get
		{
			return Configuration.AccessLevel;
		}
		set
		{
			RunSynchronously(delegate
			{
				Configuration.AccessLevel.Value = value;
			});
		}
	}

	public bool HideFromListing
	{
		get
		{
			return Configuration.HideFromListing.Value;
		}
		set
		{
			RunSynchronously(delegate
			{
				Configuration.HideFromListing.Value = value;
			});
		}
	}

	public bool UseCustomJoinVerifier
	{
		get
		{
			return Configuration.UseCustomJoinVerifier.Value;
		}
		set
		{
			RunSynchronously(delegate
			{
				Configuration.UseCustomJoinVerifier.Value = value;
			});
		}
	}

	public bool AnnounceOnWAN
	{
		get
		{
			if (UnsafeMode)
			{
				return false;
			}
			if (AccessLevel >= SessionAccessLevel.Contacts)
			{
				return true;
			}
			lock (_allowedUsers)
			{
				if (_allowedUsers.Count > 0)
				{
					return true;
				}
			}
			if (!string.IsNullOrEmpty(AllowUserCloudVariable))
			{
				return true;
			}
			if (!string.IsNullOrEmpty(RequiredUserJoinCloudVariable))
			{
				return true;
			}
			return ForceAnnounceOnWAN;
		}
	}

	public bool AwayKickEnabled
	{
		get
		{
			return Configuration.AwayKickEnabled.Value;
		}
		set
		{
			RunSynchronously(delegate
			{
				Configuration.AwayKickEnabled.Value = value;
			});
		}
	}

	public float AwayKickMinutes
	{
		get
		{
			return Configuration.AwayKickMinutes;
		}
		set
		{
			RunSynchronously(delegate
			{
				Configuration.AwayKickMinutes.Value = value;
			});
		}
	}

	public TimeSpan AwayKickInterval
	{
		get
		{
			return TimeSpan.FromMinutes(AwayKickMinutes);
		}
		set
		{
			AwayKickMinutes = (float)value.TotalMinutes;
		}
	}

	public bool IsPublic => AccessLevel >= SessionAccessLevel.RegisteredUsers;

	public bool AnnounceOnLAN
	{
		get
		{
			SessionAccessLevel accessLevel = AccessLevel;
			if (accessLevel == SessionAccessLevel.LAN)
			{
				return true;
			}
			return accessLevel >= SessionAccessLevel.RegisteredUsers;
		}
	}

	public WorldFocus Focus
	{
		get
		{
			return _focus;
		}
		set
		{
			if (_focus != value)
			{
				_focus = value;
				LastFocusChange = DateTime.UtcNow;
			}
		}
	}

	public DateTime LastFocusChange { get; private set; }

	public World Parent
	{
		get
		{
			return _parent;
		}
		set
		{
			if (value != this)
			{
				_parent = value;
			}
		}
	}

	public IWorldLink SourceLink { get; set; }

	public Uri RecordURL => CorrespondingRecord?.GetUrl(Engine.Cloud.Platform);

	public Uri RecordWebURL => CorrespondingRecord?.GetWebUrl(Engine.Cloud.Platform);

	public FrooxEngine.Store.Record CorrespondingRecord
	{
		get
		{
			return _record;
		}
		set
		{
			_record = value;
			RunSynchronously(delegate
			{
				if (_record == null || !_record.IsPublic)
				{
					Configuration.CorrespondingWorldId.Value = null;
				}
				else
				{
					Configuration.CorrespondingWorldId.Value = _record.CombinedRecordId.ToString();
				}
			});
		}
	}

	public IEnumerable<Uri> SourceURLs
	{
		get
		{
			if (_sourceURLs == null)
			{
				_sourceURLs = new List<Uri>();
				Uri recordURL = RecordURL;
				if (recordURL != null)
				{
					_sourceURLs.Add(recordURL);
				}
			}
			return _sourceURLs;
		}
		set
		{
			_sourceURLs = value.ToList();
		}
	}

	public Uri SourceURL
	{
		set
		{
			List<Uri> list = new List<Uri>();
			list.Add(value);
			SourceURLs = list;
		}
	}

	public IEnumerable<Uri> SessionURLs
	{
		get
		{
			IEnumerable<Uri> enumerable = Configuration?.SessionURLs;
			return enumerable ?? EmptyEnumerator<Uri>.Instance;
		}
	}

	public bool AllGlobalUrisRegistered { get; private set; }

	public WorldState State { get; private set; }

	public bool IsInitializingOrLoading { get; private set; }

	public bool RefreshRunning { get; private set; }

	public InitializationState InitState { get; private set; }

	public FailReason FailState { get; private set; }

	public string FailReasonDescription { get; private set; }

	public bool IsDestroyed { get; private set; }

	/// <summary>
	/// Owner of the World/Session.
	/// </summary>
	/// <remarks>Analgous to "Host".</remarks>
	public bool IsAuthority { get; private set; }

	public ulong SyncTick { get; private set; }

	public ulong StateVersion { get; private set; }

	public bool GenerateDeltaSyncData { get; private set; }

	public User LocalUser { get; private set; }

	public int MaxUsers
	{
		get
		{
			return MathX.Clamp(Configuration.MaxUsers.Value, 1, 256);
		}
		set
		{
			if (value < 1 || value > 256)
			{
				throw new ArgumentOutOfRangeException("Invalid MaxUsers number: " + value);
			}
			if (State == WorldState.Initializing)
			{
				Configuration.MaxUsers.Value = value;
			}
			else if (State == WorldState.Running)
			{
				RunSynchronously(delegate
				{
					Configuration.MaxUsers.Value = value;
				});
			}
		}
	}

	public Slot LocalUserSpace => LocalUser.Root?.Slot.Parent ?? RootSlot;

	public float3 LocalUserGlobalPosition { get; private set; }

	public floatQ LocalUserGlobalRotation { get; private set; }

	public float3 LocalUserGlobalScale { get; private set; }

	public Transform LocalUserTransform => new Transform(LocalUserGlobalPosition, LocalUserGlobalRotation, LocalUserGlobalScale);

	public bool OverrideViewPosition { get; private set; }

	public float3 LocalUserViewPosition { get; private set; }

	public floatQ LocalUserViewRotation { get; private set; }

	public float3 LocalUserViewScale { get; private set; }

	public Transform LocalUserViewTransform => new Transform(LocalUserViewPosition, LocalUserViewRotation, LocalUserViewScale);

	public bool ViewPositionIsExternal { get; private set; }

	public float LocalUserDesktopFOV { get; private set; } = 75f;

	public Material ActiveSkyboxMaterial { get; private set; }

	public SphericalHarmonicsL2<colorX> AmbientLight { get; private set; }

	public IRenderSettingsSource LocalUserRenderSettings { get; private set; }

	public SyncController SyncController { get; private set; }

	public ReferenceController ReferenceController { get; private set; }

	public WorldAssetManager AssetManager { get; private set; }

	public ConnectorManager ConnectorManager { get; private set; }

	public Session Session { get; private set; }

	public UpdateManager UpdateManager { get; private set; }

	public LinkManager LinkManager { get; private set; }

	public TimeController Time { get; private set; }

	public InputBindingManager Input { get; private set; }

	public PhysicsManager Physics { get; private set; }

	public CullingManager Culling { get; private set; }

	public RenderManager Render { get; private set; }

	public CoroutineManager Coroutines { get; private set; }

	public DebugManager Debug { get; private set; }

	public ProtoFluxController ProtoFlux { get; private set; }

	public PermissionController Permissions { get; private set; }

	public DynamicBoneChainManager DynamicBones { get; private set; }

	public ParticleSystemManager ParticleSystems { get; private set; }

	public SpatialVariableManager SpatialVariables { get; private set; }

	public TypeManager Types { get; private set; }

	public InputInterface InputInterface => Engine.InputInterface;

	public AudioManager Audio { get; private set; }

	public ChangedHierarchyEventManager ChangedHierarchyEvents { get; private set; }

	public GeneralMovedHierarchyEventManager GeneralMovedHierarchyEvents { get; private set; }

	public PhysicsMovedHierarchyEventManager PhysicsMovedHierarchyEvents { get; private set; }

	public ResoniteLinkHost ResoniteLink { get; private set; }

	public string Name
	{
		get
		{
			if (Configuration != null)
			{
				return Configuration.WorldName.Value;
			}
			return _lastName;
		}
		set
		{
			RunSynchronously(delegate
			{
				Configuration.WorldName.Value = value;
			});
		}
	}

	public string RawName
	{
		get
		{
			if (Name != _lastUnstrippedName)
			{
				_lastUnstrippedName = Name;
				if (string.IsNullOrWhiteSpace(_lastUnstrippedName))
				{
					_lastStrippedName = _lastUnstrippedName;
				}
				else
				{
					StringRenderTree stringRenderTree = new StringRenderTree(_lastUnstrippedName);
					_lastStrippedName = stringRenderTree.GetRawString();
				}
			}
			return _lastStrippedName;
		}
	}

	public string Description
	{
		get
		{
			return Configuration.WorldDescription.Value;
		}
		set
		{
			RunSynchronously(delegate
			{
				Configuration.WorldDescription.Value = value;
			});
		}
	}

	public IEnumerable<string> Tags
	{
		get
		{
			return Configuration.WorldTags;
		}
		set
		{
			List<string> _tags = value?.ToList();
			RunSynchronously(delegate
			{
				Configuration.WorldTags.Clear();
				if (_tags != null)
				{
					HashSet<string> automaticTags = AutomaticTags.ToHashSet();
					Configuration.WorldTags.AddRange(_tags.Where((string t) => !automaticTags.Contains(t)));
				}
			});
		}
	}

	public IEnumerable<string> AllTags
	{
		get
		{
			foreach (string tag in Tags)
			{
				yield return tag;
			}
			foreach (string automaticTag in AutomaticTags)
			{
				yield return automaticTag;
			}
		}
	}

	public IEnumerable<string> AutomaticTags
	{
		get
		{
			foreach (IWorldMetadataSource componentsInChild in RootSlot.GetComponentsInChildren<IWorldMetadataSource>())
			{
				foreach (string worldTag in componentsInChild.WorldTags)
				{
					yield return worldTag;
				}
			}
		}
	}

	public IEnumerable<string> ParentSessionIds
	{
		get
		{
			return Configuration.ParentSessionIds;
		}
		set
		{
			List<string> _ids = value?.ToList();
			RunSynchronously(delegate
			{
				Configuration.ParentSessionIds.Clear();
				Configuration.ParentSessionIds.AddRange(_ids);
			});
		}
	}

	public string SessionId => Configuration.SessionID.Value;

	public string NormalizedSessionId => SessionId.ToLower();

	public string CorrespondingWorldId
	{
		get
		{
			string text = Configuration.CorrespondingWorldId.Value;
			if (text == null)
			{
				FrooxEngine.Store.Record correspondingRecord = CorrespondingRecord;
				if (correspondingRecord == null)
				{
					return null;
				}
				RecordId combinedRecordId = correspondingRecord.CombinedRecordId;
				if ((object)combinedRecordId == null)
				{
					return null;
				}
				text = combinedRecordId.ToString();
			}
			return text;
		}
		set
		{
			RunSynchronously(delegate
			{
				if (string.IsNullOrWhiteSpace(value))
				{
					Configuration.CorrespondingWorldId.Value = null;
				}
				else
				{
					Configuration.CorrespondingWorldId.Value = value.Trim();
				}
			});
		}
	}

	public string UniverseId
	{
		get
		{
			return Configuration.UniverseID.Value;
		}
		set
		{
			RunSynchronously(delegate
			{
				Configuration.UniverseID.Value = value;
			});
		}
	}

	public bool MobileFriendly
	{
		get
		{
			return Configuration.MobileFriendly.Value;
		}
		set
		{
			RunSynchronously(delegate
			{
				Configuration.MobileFriendly.Value = value;
			});
		}
	}

	public Slot RootSlot { get; private set; }

	public bool RunFullUpdateCycle
	{
		get
		{
			if (_graceFullUpdateCycles <= 0)
			{
				return ShouldRunFullUpdateCycle;
			}
			return true;
		}
	}

	public bool ShouldRunFullUpdateCycle
	{
		get
		{
			if (State != WorldState.Running)
			{
				return false;
			}
			if (ForceFullUpdateCycle)
			{
				return true;
			}
			if (LocalUser.HeadDevice == HeadOutputDevice.Headless)
			{
				if (UserCount <= 1 && !Userspace.IsExitingApp && Focus != WorldFocus.Overlay)
				{
					return Focus == WorldFocus.PrivateOverlay;
				}
				return true;
			}
			return Focus != WorldFocus.Background;
		}
	}

	public double LastUpdateTime { get; private set; }

	public double MinUpdateTime { get; private set; } = double.MaxValue;

	public double MaxUpdateTime { get; private set; }

	public double[] StageUpdateTime { get; private set; } = new double[Enum.GetValues(typeof(RefreshStage)).Length];

	public double AudioUpdateTime { get; private set; }

	public double[] MinStageUpdateTime { get; private set; } = new double[Enum.GetValues(typeof(RefreshStage)).Length];

	public double[] MaxStageUpdateTime { get; private set; } = new double[Enum.GetValues(typeof(RefreshStage)).Length];

	public double[] AvgStageUpdateTime { get; private set; } = new double[Enum.GetValues(typeof(RefreshStage)).Length];

	public int LastCoroutines { get; internal set; }

	public int LastCommonUpdates { get; internal set; }

	public int LastChanges { get; internal set; }

	public int LastConnectorUpdates { get; internal set; }

	public int LastMovedSlots { get; internal set; }

	public int LastSynchronousActions { get; internal set; }

	public int LastColliderUpdates { get; internal set; }

	public int LastPhysicsActiveBodies { get; internal set; }

	public int LastPhysicsStatics { get; internal set; }

	public int LastRebuiltNodeGroups { get; internal set; }

	public int LastDirtyNodes { get; internal set; }

	public int LastUpdatedNodeGroups { get; internal set; }

	public int LastContinuousChangeNodeGroups { get; internal set; }

	public int LastChangedNodeGroups { get; internal set; }

	public int LastUpdatedNodes { get; internal set; }

	public int LastChangedNodes { get; internal set; }

	public int LastNodeEvents { get; internal set; }

	public int LastUpdatedDriveNodes { get; internal set; }

	public int LastAudioStreamUnderruns { get; private set; }

	public int LastTotalSlots { get; internal set; }

	public int TotalUpdates { get; private set; }

	public double AverageUpdateTime => sumUpdateTime / (double)TotalUpdates;

	internal string StageDEBUG => Stage.ToString();

	public RefreshStage Stage { get; private set; }

	public bool CanMakeSynchronousChanges
	{
		get
		{
			if (ConnectorManager.CanCurrentThreadModify)
			{
				return ConnectorManager.Lock == FrooxEngine.ConnectorManager.LockOwner.Implementer;
			}
			return false;
		}
	}

	public bool CanCurrentThreadModify => ConnectorManager.CanCurrentThreadModify;

	public IEnumerable<KeyValuePair<string, SyncRef<Component>>> Keys => _keys;

	public int SlotCount => _slots.Count;

	public IEnumerable<Slot> AllSlots => _slots.Select<KeyValuePair<RefID, Slot>, Slot>((KeyValuePair<RefID, Slot> s) => s.Value);

	IWorldElement IWorldElement.Parent => null;

	World IWorldElement.World => this;

	bool IWorldElement.IsPersistent => true;

	bool IWorldElement.IsRemoved => IsDestroyed;

	bool IWorldElement.IsLocalElement => false;

	RefID IWorldElement.ReferenceID => RefID.Null;

	public int UserCount
	{
		get
		{
			lock (_userSnapshot)
			{
				return _userSnapshot.Count;
			}
		}
	}

	public int ActiveUserCount
	{
		get
		{
			lock (_userSnapshot)
			{
				return _userSnapshot.Where<KeyValuePair<RefID, User>>((KeyValuePair<RefID, User> u) => !u.Value.IsInInitPhase).Count((KeyValuePair<RefID, User> u) => u.Value.IsPresentInWorld);
			}
		}
	}

	public User HostUser
	{
		get
		{
			if (_hostUser == null)
			{
				_hostUser = GetHost();
			}
			return _hostUser;
		}
	}

	public Dictionary<RefID, User>.ValueCollection AllUsers => _users.Values;

	public bool HasFreeUserCapacity => _users.Count < 256;

	public bool HasFreeUserSpots
	{
		get
		{
			int num = _users.Count;
			if (InputInterface.HeadOutputDevice == HeadOutputDevice.Headless)
			{
				num--;
			}
			return num < MaxUsers;
		}
	}

	public Slot AssetsSlot
	{
		get
		{
			if (_assets == null || _assets.IsDestroyed)
			{
				_assets = RootSlot.FindChildOrAdd("Assets");
			}
			_assets.MarkProtected(forcePersistent: false);
			return _assets;
		}
	}

	public Slot LocalAssetsSlot
	{
		get
		{
			if (_localAssets == null || _localAssets.IsDestroyed)
			{
				_localAssets = RootSlot.FindLocalChildOrAdd("LocalAssets");
			}
			_localAssets.MarkProtected(forcePersistent: false);
			return _localAssets;
		}
	}

	public bool IsDisposed => _disposed;

	public DateTime LastUserUpdate { get; private set; }

	public DateTime LastInviteListUpdate { get; private set; }

	public event Action FinalUserPoseUpdated;

	private event Action<World> _worldRunning;

	public event Action<World> WorldRunning
	{
		add
		{
			if (State == WorldState.Running)
			{
				value(this);
				return;
			}
			lock (stateLock)
			{
				if (State == WorldState.Running)
				{
					value(this);
				}
				else
				{
					_worldRunning += value;
				}
			}
		}
		remove
		{
			lock (stateLock)
			{
				_worldRunning -= value;
			}
		}
	}

	private event Action<World> _worldDestroyed;

	public event Action<World> WorldDestroyed
	{
		add
		{
			if (IsDestroyed)
			{
				value(this);
				return;
			}
			lock (stateLock)
			{
				if (IsDestroyed)
				{
					value(this);
				}
				else
				{
					_worldDestroyed += value;
				}
			}
		}
		remove
		{
			lock (stateLock)
			{
				_worldDestroyed -= value;
			}
		}
	}

	public event Action<Slot> SlotAdded;

	public event Action<Slot> SlotRemoved;

	public event Action<Slot, Component> ComponentAdded;

	public event Action<Slot, Component> ComponentRemoved;

	public event Action<User> UserJoined;

	public event Action<User> UserSpawn;

	public event Action<User> UserLeft;

	public ushort[] GetRandomizationTable(int count)
	{
		if (_randomizationTables == null)
		{
			return null;
		}
		if (count > _randomizationTables.Length)
		{
			InitializeRandomizationTables(count);
		}
		return _randomizationTables[count - 1];
	}

	private void InitializeRandomizationTables(int maxCount)
	{
		Random random = new Random(RandomizationSeed);
		_randomizationTables = new ushort[maxCount][];
		for (int i = 0; i < maxCount; i++)
		{
			ushort[] array = new ushort[i + 1];
			for (int j = 0; j <= i; j++)
			{
				array[j] = (ushort)j;
			}
			array.Shuffle(random);
			_randomizationTables[i] = array;
		}
	}

	public void AllowUserToJoin(string userId)
	{
		lock (_allowedUsers)
		{
			_allowedUsers.Add(userId);
		}
		LastInviteListUpdate = DateTime.UtcNow;
	}

	public void RemoveAllowedUser(string userId)
	{
		lock (_allowedUsers)
		{
			_allowedUsers.Remove(userId);
		}
		LastInviteListUpdate = DateTime.UtcNow;
	}

	public bool IsUserAllowed(string userId)
	{
		if (userId == null)
		{
			return false;
		}
		lock (_allowedUsers)
		{
			return _allowedUsers.Contains(userId);
		}
	}

	public List<string> GetAllowedUserList()
	{
		lock (_allowedUsers)
		{
			return new List<string>(_allowedUsers);
		}
	}

	public void AddInviteRequestHandler(string userId)
	{
		lock (_inviteRequestHandlers)
		{
			_inviteRequestHandlers.Add(userId);
		}
	}

	public void RemoveInviteRequestHandler(string userId)
	{
		lock (_inviteRequestHandlers)
		{
			_inviteRequestHandlers.Remove(userId);
		}
	}

	public HashSet<string> GetInviteHandlerUsers()
	{
		HashSet<string> hashSet = new HashSet<string>();
		PermissionSet highestRole = Permissions.HighestRole;
		List<User> list = new List<User>();
		GetUsers(list, (User u) => u.Role == highestRole && u.UserID != null && !u.IsHost);
		foreach (User item in list)
		{
			hashSet.Add(item.UserID);
		}
		lock (_inviteRequestHandlers)
		{
			foreach (string inviteRequestHandler in _inviteRequestHandlers)
			{
				hashSet.Add(inviteRequestHandler);
			}
			return hashSet;
		}
	}

	public FrooxEngine.Store.Record CreateNewRecord(string ownerId, string recordId)
	{
		return new FrooxEngine.Store.Record
		{
			OwnerId = ownerId,
			RecordId = recordId,
			RecordType = "world",
			CreationTime = DateTime.UtcNow,
			LastModificationTime = DateTime.UtcNow
		};
	}

	public FrooxEngine.Store.Record CreateNewRecord(string ownerId = null)
	{
		return CreateNewRecord(ownerId ?? GetCorrespondingOwnerId(), RecordHelper.GenerateRecordID());
	}

	public void AssignNewRecord(string ownerId, string recordId)
	{
		CorrespondingRecord = CreateNewRecord(ownerId, recordId);
	}

	public FrooxEngine.Store.Record GetRecordOrCreate(string ownerId = null)
	{
		if (CorrespondingRecord == null)
		{
			CorrespondingRecord = CreateNewRecord(ownerId);
		}
		return CorrespondingRecord;
	}

	public string GetCorrespondingOwnerId()
	{
		return Parent?.CorrespondingRecord?.OwnerId ?? ("M-" + Engine.LocalDB.MachineID);
	}

	public void AudioStreamUnderrun()
	{
		Interlocked.Increment(ref _lastAudioStreamUnderruns);
	}

	public void ColliderUpdated()
	{
		LastColliderUpdates++;
	}

	private void UpdateUpdateTime(double time)
	{
		LastUpdateTime = time;
		MinUpdateTime = MathX.Min(time, MinUpdateTime);
		MaxUpdateTime = MathX.Max(time, MaxUpdateTime);
		TotalUpdates++;
		sumUpdateTime += time;
		for (int i = 0; i < StageUpdateTime.Length; i++)
		{
			double num = StageUpdateTime[i];
			_minStageUpdateTimeOngoing[i] = MathX.Min(num, _minStageUpdateTimeOngoing[i]);
			_maxStageUpdateTimeOngoing[i] = MathX.Max(num, _maxStageUpdateTimeOngoing[i]);
			_avgStageUpdateTimeOngoing[i] += num;
			if (_stageUpdateTimeCount == 89)
			{
				MinStageUpdateTime[i] = _minStageUpdateTimeOngoing[i];
				MaxStageUpdateTime[i] = _maxStageUpdateTimeOngoing[i];
				AvgStageUpdateTime[i] = _avgStageUpdateTimeOngoing[i] / 90.0;
				_minStageUpdateTimeOngoing[i] = double.MaxValue;
				_maxStageUpdateTimeOngoing[i] = double.MinValue;
				_avgStageUpdateTimeOngoing[i] = 0.0;
			}
		}
		_stageUpdateTimeCount++;
		_stageUpdateTimeCount %= 90;
	}

	public static World LocalWorld(WorldManager manager, WorldAction init, DataTreeNode load = null, bool unsafeMode = false, IEnumerable<AssemblyTypeRegistry> assemblies = null)
	{
		World world = new World(manager, isAuthority: true, unsafeMode);
		world.Types.InitializeAssemblies(assemblies ?? GlobalTypeRegistry.CoreAssemblies);
		world.InitializeRandomization();
		world.MaxUsers = 1;
		world.Configuration.MaxUsers.LocalFilter = (int value, IField<int> field) => 1;
		world.IsAuthority = true;
		world.worldInitAction = init;
		world.worldInitLoad = load;
		world.CreateHostUser();
		world.StartRunning();
		return world;
	}

	public static World StartSession(WorldManager manager, WorldAction init, ushort port = 0, string forceSessionId = null, DataTreeNode load = null, FrooxEngine.Store.Record record = null, bool unsafeMode = false, IEnumerable<AssemblyTypeRegistry> assemblies = null)
	{
		if (forceSessionId != null && !SessionInfo.IsValidSessionId(forceSessionId))
		{
			throw new ArgumentException("Invalid session ID: " + forceSessionId);
		}
		World world = new World(manager, isAuthority: true, unsafeMode);
		world.Types.InitializeAssemblies(assemblies ?? GlobalTypeRegistry.CoreAssemblies);
		world.InitializeRandomization();
		world.IsAuthority = true;
		world.worldInitAction = init;
		world.worldInitLoad = load;
		world.CorrespondingRecord = record;
		if (forceSessionId == null)
		{
			forceSessionId = "S-" + Guid.CreateVersion7();
		}
		world.Configuration.SessionID.Value = forceSessionId;
		world.Session = FrooxEngine.Session.NewSession(world, port);
		if (world.Engine.InUniverse)
		{
			world.UniverseId = world.Engine.UniverseId;
		}
		return world;
	}

	public static World JoinSession(WorldManager manager, IEnumerable<Uri> addresses)
	{
		UniLog.Log("Joining session: " + string.Join(",", addresses), stackTrace: true);
		World world = new World(manager, isAuthority: false, unsafeMode: false);
		world.Session = FrooxEngine.Session.JoinSession(world, addresses);
		return world;
	}

	private World(WorldManager manager, bool isAuthority, bool unsafeMode)
	{
		LocalWorldHandle = manager.AllocateWorldHandle();
		WorldManager = manager;
		IsAuthority = isAuthority;
		UnsafeMode = unsafeMode;
		GenerateDeltaSyncData = !isAuthority;
		trashbin = new Dictionary<ulong, Dictionary<RefID, ISyncMember>>();
		int length = Enum.GetValues(typeof(WorldEvent)).Length;
		worldEventReceivers = new List<IWorldEventReceiver>[length];
		for (int i = 0; i < length; i++)
		{
			worldEventReceivers[i] = new List<IWorldEventReceiver>();
		}
		SyncTick = 1uL;
		StateVersion = 1uL;
		ReferenceController = new ReferenceController(this);
		SyncController = new SyncController(this);
		AssetManager = new WorldAssetManager(this);
		ConnectorManager = new ConnectorManager(this);
		UpdateManager = new UpdateManager(this);
		LinkManager = new LinkManager(this);
		Time = new TimeController(this);
		Input = new InputBindingManager(this);
		Physics = new PhysicsManager(this);
		Culling = new CullingManager(this);
		Render = new RenderManager(this);
		Coroutines = new CoroutineManager(Engine, this);
		Debug = new DebugManager(this);
		ProtoFlux = new ProtoFluxController(this);
		_slots = new SlotBag();
		_users = new UserBag();
		RootSlot = new Slot(startInInitPhase: false);
		Configuration = new WorldConfiguration();
		_keys = new SyncRefDictionary<string, Component>();
		_keyVersions = new SyncFieldDictionary<string, int>();
		Permissions = new PermissionController();
		Audio = new AudioManager(this);
		DynamicBones = new DynamicBoneChainManager(this);
		ParticleSystems = new ParticleSystemManager(this);
		SpatialVariables = new SpatialVariableManager(this);
		Types = new TypeManager(this);
		_slots.Initialize(this, this);
		_users.Initialize(this, this);
		Configuration.Initialize(this, this);
		_keys.Initialize(this, this);
		_keyVersions.Initialize(this, this);
		Permissions.Initialize(this, this);
		RootSlot.Initialize(this, isRoot: true);
		if (IsAuthority && Userspace.ForceLANOnly)
		{
			Configuration.AccessLevel.Value = SessionAccessLevel.LAN;
		}
		_slots.EndInitPhase();
		_users.EndInitPhase();
		Configuration.EndInitPhase();
		_keys.EndInitPhase();
		_keyVersions.EndInitPhase();
		Permissions.EndInitPhase();
		RootSlot.EndInitPhase();
		if (IsAuthority)
		{
			RootSlot.Name = "Root";
		}
		_slots.OnElementAdded += OnSlotAdded;
		_slots.OnElementRemoved += OnSlotRemoved;
		_users.OnElementAdded += UserAdded;
		_users.OnElementRemoved += UserRemoved;
		State = WorldState.Initializing;
		InitState = InitializationState.Created;
		_keys.ElementAdded += KeyAdded;
		_keys.ElementRemoved += KeyRemoved;
		_keys.BeforeClear += KeysBeforeClear;
		Focus = WorldFocus.Background;
		Engine.AudioSystem.GlobalVolumesChanged += AudioSystem_GlobalVolumesChanged;
		Engine.AudioSystem.DefaultAudioInputChanged += InputInterface_DefaultAudioInputChanged;
		ChangedHierarchyEvents = new ChangedHierarchyEventManager(this);
		GeneralMovedHierarchyEvents = new GeneralMovedHierarchyEventManager(this);
		PhysicsMovedHierarchyEvents = new PhysicsMovedHierarchyEventManager(this);
	}

	public string ObfuscateString(string str)
	{
		return str?.SimpleEncrypt(Obfuscation_KEY, Obfuscation_IV);
	}

	public string DeobfuscateString(string str)
	{
		return str?.SimpleDecrypt(Obfuscation_KEY, Obfuscation_IV);
	}

	internal void InitializeRandomization(int seed = 0, byte[] obfuscationKey = null, byte[] obfuscationIV = null)
	{
		if (RandomizationSeed != 0)
		{
			throw new InvalidOperationException("Randomization Already Initialized!");
		}
		bool num = seed == 0 || obfuscationKey == null || obfuscationIV == null;
		bool flag = seed != 0 || obfuscationKey != null || obfuscationIV != null;
		if (num && flag)
		{
			throw new ArgumentException("Cannot mix initialized and uninitialized parameters");
		}
		if (seed == 0)
		{
			seed = RandomX.Range(1, int.MaxValue);
		}
		RandomizationSeed = seed;
		InitializeRandomizationTables(256);
		if (obfuscationKey != null && obfuscationIV != null)
		{
			Obfuscation_KEY = obfuscationKey;
			Obfuscation_IV = obfuscationIV;
			return;
		}
		Obfuscation_KEY = new byte[8];
		Obfuscation_IV = new byte[8];
		Random random = new Random();
		random.NextBytes(Obfuscation_KEY);
		random.NextBytes(Obfuscation_IV);
	}

	private void InputInterface_DefaultAudioInputChanged(AudioInput obj)
	{
		audioConfigurationChanged = 1;
	}

	private void AudioSystem_GlobalVolumesChanged()
	{
		audioConfigurationChanged = 1;
	}

	public void NetworkInitStart()
	{
		InitState = InitializationState.InitializingNetwork;
		UniLog.Log("NetworkInitStart");
	}

	public void WaitForJoinGrant()
	{
		if (IsAuthority)
		{
			throw new Exception("Authority cannot wait for session join grant");
		}
		InitState = InitializationState.WaitingForJoinGrant;
		UniLog.Log("Waiting for join grant");
	}

	public void StartDataModelInit(int randomizationSeed, byte[] obfuscationKey, byte[] obfuscationIV)
	{
		if (IsAuthority)
		{
			throw new Exception("Authority cannot do a data model full sync init.");
		}
		InitializeRandomization(randomizationSeed, obfuscationKey, obfuscationIV);
		InitState = InitializationState.InitializingDataModel;
		UniLog.Log("Join Granted, Begin Data Model Init");
	}

	public void InitializeAllocationForLocalUser()
	{
		if (LocalUser.AllocationID != 0)
		{
			ReferenceController.AllocationBlockBegin(LocalUser.AllocationID, LocalUser.AllocationIDStart);
		}
	}

	public void StartRunning()
	{
		if (LocalUser == null)
		{
			throw new Exception("LocalUser hasn't been configured!");
		}
		State = WorldState.Running;
		InitState = InitializationState.Finished;
		UniLog.Log("Starting running world: " + RawName);
		lock (stateLock)
		{
			this._worldRunning?.Invoke(this);
		}
		RunSynchronously(delegate
		{
			Engine.PlatformInterface.NotifyOfLocalUser(LocalUser);
			Engine.Cloud.Moderation.OnUserPublicBanned += Cloud_OnUserPublicBanned;
		});
		if (IsAuthority)
		{
			RunSynchronously(delegate
			{
				Configuration.SessionURLs.Clear();
				UpdateSessionURLs();
				Engine.Cloud.Moderation.OnUserSpectatorBanned += Cloud_OnUserSpectatorBanned;
				Engine.Cloud.Moderation.OnUserMuted += Cloud_OnUserMuted;
			});
		}
	}

	private void UpdateSessionURLs()
	{
		if (Session == null)
		{
			return;
		}
		bool sessionUrlsChanged = false;
		HashSet<Uri> toRemove = Pool.BorrowHashSet<Uri>();
		foreach (Uri sessionURL in Configuration.SessionURLs)
		{
			if (sessionURL != null)
			{
				toRemove.Add(sessionURL);
			}
		}
		bool flag = false;
		AppConfig config = FrooxEngine.Engine.Config;
		flag = ((config == null || !config.AnnounceLocalIPs.HasValue) ? Engine.InUniverse : FrooxEngine.Engine.Config.AnnounceLocalIPs.Value);
		if (AccessLevel == SessionAccessLevel.LAN)
		{
			flag = true;
		}
		bool allGlobalUrisRegistered = true;
		foreach (IListener listener in Session.Connections.Listener.Listeners)
		{
			if (!listener.GlobalUrisReady)
			{
				allGlobalUrisRegistered = false;
			}
			foreach (Uri globalUri in listener.GlobalUris)
			{
				RegisterUrl(globalUri);
			}
			if (!flag)
			{
				continue;
			}
			foreach (Uri localUri in listener.LocalUris)
			{
				RegisterUrl(localUri);
			}
		}
		foreach (Uri item in toRemove)
		{
			Configuration.SessionURLs.Remove(item);
			sessionUrlsChanged = true;
		}
		Pool.Return(ref toRemove);
		AllGlobalUrisRegistered = allGlobalUrisRegistered;
		if (sessionUrlsChanged)
		{
			Userspace.AssignSessionURLs(SourceLink, this);
		}
		void RegisterUrl(Uri url)
		{
			if (!(url == null) && !toRemove.Remove(url))
			{
				sessionUrlsChanged = true;
				Configuration.SessionURLs.Add(url);
			}
		}
	}

	private void Cloud_OnUserMuted(string userId)
	{
		if (!IsDisposed)
		{
			RunBanAction(userId, delegate(User u)
			{
				UniLog.Log($"User {u} Silenced due to global mute ban");
				u.InitializingEnabled = true;
				u.DefaultMute = true;
				u.InitializingEnabled = false;
			});
		}
	}

	private void Cloud_OnUserSpectatorBanned(string userId)
	{
		if (!IsDisposed)
		{
			RunBanAction(userId, delegate(User u)
			{
				UniLog.Log($"User {u} set to Spectator due to global spectator ban");
				u.InitializingEnabled = true;
				u.DefaultSpectator = true;
				u.InitializingEnabled = false;
				u.Role = Permissions.LowestRole;
			});
		}
	}

	private void Cloud_OnUserPublicBanned(string userId)
	{
		if (IsDisposed)
		{
			return;
		}
		if (IsAuthority)
		{
			RunBanAction(userId, delegate(User u)
			{
				UniLog.Log($"User {u} Kicked due to global public ban");
				u.Kick();
			});
		}
		else if (userId == HostUser.UserID)
		{
			Userspace.ExitWorld(this);
		}
	}

	private void RunBanAction(string userId, Action<User> action)
	{
		if (IsDisposed)
		{
			return;
		}
		User user = GetUserByUserId(userId);
		if (user != null && !user.IsHost)
		{
			RunSynchronously(delegate
			{
				action(user);
			});
		}
	}

	public void InitializationFailed(FailReason reason, string reasonDescription)
	{
		InitState = InitializationState.Failed;
		State = WorldState.Failed;
		FailState = reason;
		FailReasonDescription = reasonDescription;
		Destroy();
	}

	public void IncrementStateVersion()
	{
		if (!IsAuthority)
		{
			throw new Exception("Only the host can increment the state version!");
		}
		StateVersion++;
	}

	public void UpdateStateVersion(ulong version)
	{
		if (IsAuthority)
		{
			throw new Exception("Host cannot set state version, only increment it");
		}
		if (version < StateVersion)
		{
			throw new Exception("Cannot decrease the last state version! Last state version" + StateVersion + ", Trying to set: " + version);
		}
		StateVersion = version;
	}

	public bool Refresh()
	{
		if (IsDestroyed)
		{
			throw new Exception("Cannot run refresh on a destroyed world");
		}
		if (State != WorldState.Running)
		{
			throw new Exception("Cannot run refresh on a world that's not in the running state!");
		}
		stopwatch.Restart();
		bool flag = false;
		_lastName = Configuration.WorldName.Value;
		StageUpdateTime[28] = 0.0;
		while (!flag && !IsDisposed)
		{
			RefreshStage stage = Stage;
			stageStopwatch.Restart();
			bool num = RefreshStep();
			stageStopwatch.Stop();
			StageUpdateTime[(int)stage] = stageStopwatch.Elapsed.TotalSeconds;
			if (debugLogUpdateTimes)
			{
				UniLog.Log($"Stage Update Time: {stage}\t{stageStopwatch.Elapsed}");
			}
			if (num)
			{
				stageStopwatch.Restart();
				NextRefreshStage();
				stageStopwatch.Stop();
				StageUpdateTime[28] += stageStopwatch.Elapsed.TotalSeconds;
			}
			if (Stage == RefreshStage.Finished)
			{
				flag = true;
				FinishRefresh();
			}
		}
		stopwatch.Stop();
		UpdateUpdateTime(stopwatch.Elapsed.TotalSeconds);
		return flag;
	}

	public void RunAudioUpdates()
	{
		LastAudioStreamUnderruns = _lastAudioStreamUnderruns;
		_lastAudioStreamUnderruns = 0;
		audioStopwatch.Restart();
		UpdateManager.RunAudioUpdates();
		audioStopwatch.Stop();
		AudioUpdateTime = audioStopwatch.Elapsed.TotalSeconds;
	}

	private void RunSynchronousActions()
	{
		AssetManager.SendAssetProviderChanges();
		UpdateManager.RunActiveStateChangedEvents();
		int num = synchronousActions.Count;
		LastSynchronousActions += num;
		while (--num >= 0)
		{
			if (!synchronousActions.TryDequeue(out var val))
			{
				continue;
			}
			bool flag = false;
			try
			{
				if (val.updatable == null)
				{
					goto IL_007e;
				}
				if (val.updatable.IsRemoved && !val.evenDisposed)
				{
					continue;
				}
				UpdateManager.NestCurrentlyUpdating(val.updatable);
				flag = true;
				goto IL_007e;
				IL_007e:
				val.action();
			}
			catch (FatalWorldException)
			{
				throw;
			}
			catch (Exception exception)
			{
				Debug.Error($"Exception when running synchronous action (Stage: {Stage}):\n" + DebugManager.PreprocessException(exception), stackTrace: false);
			}
			finally
			{
				if (flag)
				{
					UpdateManager.PopCurrentlyUpdating(val.updatable);
				}
			}
		}
	}

	private void NextRefreshStage()
	{
		Stage++;
		bool flag;
		do
		{
			flag = false;
			if ((Stage == RefreshStage.Updates || Stage == RefreshStage.MaterialUpdate) && !RunFullUpdateCycle)
			{
				Stage++;
				flag = true;
			}
			if (Stage == RefreshStage.Input && !ShouldRunFullUpdateCycle)
			{
				Stage++;
				flag = true;
			}
		}
		while (flag);
		if (Stage < RefreshStage.Finished && Stage > RefreshStage.RefreshBegin && State == WorldState.Running)
		{
			RunSynchronousActions();
		}
		switch (Stage)
		{
		case RefreshStage.Updates:
			UpdateManager.PrepareUpdateCycle();
			break;
		case RefreshStage.Changes:
			UpdateManager.PrepareChangesCycle();
			break;
		}
	}

	private void HandleAwayKick()
	{
		if (!IsAuthority || !Configuration.AwayKickEnabled)
		{
			return;
		}
		float num = Configuration.AwayKickMinutes.Value * 60f;
		foreach (User allUser in AllUsers)
		{
			if (allUser.AwayDuration >= (double)num && !allUser.IsHost && allUser.Role != Permissions.Roles[0])
			{
				allUser.Kick(User.KickRequestState.Kick);
			}
		}
	}

	private bool RefreshStep()
	{
		switch (Stage)
		{
		case RefreshStage.RefreshBegin:
			RefreshRunning = true;
			LastCoroutines = 0;
			LastCommonUpdates = 0;
			LastChanges = 0;
			LastConnectorUpdates = 0;
			LastMovedSlots = 0;
			LastSynchronousActions = 0;
			LastColliderUpdates = 0;
			LastRebuiltNodeGroups = 0;
			LastDirtyNodes = 0;
			LastUpdatedNodes = 0;
			LastChangedNodes = 0;
			LastUpdatedDriveNodes = 0;
			if (Session != null)
			{
				Session.Sync.StopProcessing();
			}
			else
			{
				if (ConnectorManager.Lock == FrooxEngine.ConnectorManager.LockOwner.DataModel)
				{
					ConnectorManager.DataModelUnlock();
				}
				ConnectorManager.ImplementerLock(Thread.CurrentThread);
			}
			Time.Update();
			if (worldInitLoad != null)
			{
				IsInitializingOrLoading = true;
				Stopwatch stopwatch = Stopwatch.StartNew();
				refTranslator = refTranslator ?? new ReferenceTranslator();
				LoadControl loadControl = new LoadControl(this, refTranslator, default(VersionNumber), CorrespondingRecord);
				loadControl.SetLoadRoot(this);
				Load(worldInitLoad, loadControl);
				loadControl.StoreUnresolvedReferences(this);
				loadControl.FinishLoad();
				stopwatch.Stop();
				UniLog.Log("World " + RawName + " Loaded in: " + stopwatch.Elapsed);
				worldInitLoad = null;
				IsInitializingOrLoading = false;
			}
			if (worldInitAction != null)
			{
				IsInitializingOrLoading = true;
				worldInitAction(this);
				worldInitAction = null;
				IsInitializingOrLoading = false;
			}
			LinkManager.GrantLinks();
			if (LocalUser.Root != null)
			{
				float3 a = LocalUser.Root.HeadPosition;
				foreach (User allUser in AllUsers)
				{
					if (allUser.Root != null)
					{
						allUser.DistanceToLocalUserHead = MathX.Distance(in a, allUser.Root.HeadPosition);
					}
				}
			}
			HandleAwayKick();
			if ((DateTime.UtcNow - _lastStatsUpdate).TotalSeconds >= 1.0)
			{
				LocalUser.FPS = Engine.PerfStats.FPS;
				if (FrooxEngine.Engine.IsAprilFools)
				{
					MysterySettings? activeSetting = Settings.GetActiveSetting<MysterySettings>();
					if (activeSetting != null && activeSetting.MoreFPS.Value)
					{
						LocalUser.FPS *= 2f;
					}
				}
				if (IsAuthority)
				{
					LocalUser.CommitMessageStats();
					UpdateSessionURLs();
				}
				_lastStatsUpdate = DateTime.UtcNow;
			}
			if (ShouldRunFullUpdateCycle)
			{
				_graceFullUpdateCycles = 4;
			}
			else if (_graceFullUpdateCycles > 0)
			{
				_graceFullUpdateCycles--;
			}
			AssetManager.Update();
			ResoniteLink?.Update();
			return true;
		case RefreshStage.PhysicsMoved:
			if (RunFullUpdateCycle)
			{
				PhysicsMovedHierarchyEvents.RunMovedEvents();
			}
			return true;
		case RefreshStage.PhysicsUpdate:
			if (RunFullUpdateCycle)
			{
				Physics.Update();
			}
			return true;
		case RefreshStage.PhysicsSchleduleRefine:
			if (RunFullUpdateCycle)
			{
				Physics.DispatchRefine();
			}
			return true;
		case RefreshStage.Coroutines:
			LastCoroutines = Coroutines.ExecuteWorldQueue(Time.Delta);
			return true;
		case RefreshStage.WorldEvents:
			RunWorldEvents();
			return true;
		case RefreshStage.UpdatingStreams:
			UpdateManager.UpdateStreams();
			return true;
		case RefreshStage.PhysicsSync:
			Physics.Sync();
			return true;
		case RefreshStage.RunningStartups:
			return UpdateManager.RunStartups();
		case RefreshStage.RunningEvents:
			if (Interlocked.CompareExchange(ref audioConfigurationChanged, 0, 1) == 1)
			{
				UpdateManager.RunAudioConfigurationChanged();
			}
			return true;
		case RefreshStage.Input:
			Input.Update();
			UpdateLocalUserOutputPositions(LocalUser.Root);
			return true;
		case RefreshStage.Updates:
			if (UpdateManager.RunUpdates())
			{
				UpdateManager.FinishUpdateCycle();
				DynamicBones.Update();
				return true;
			}
			return false;
		case RefreshStage.ProtoFluxRebuild:
			ProtoFlux.RebuildAndCleanup();
			return true;
		case RefreshStage.ProtoFluxEvents:
			ProtoFlux.RunNodeEvents();
			return true;
		case RefreshStage.ProtoFluxUpdates:
			if (ShouldRunFullUpdateCycle)
			{
				ProtoFlux.RunNodeUpdates();
			}
			return true;
		case RefreshStage.ProtoFluxContinuousChanges:
			ProtoFlux.RunContinuousChanges();
			return true;
		case RefreshStage.ProtoFluxDiscreteChangesPre:
		case RefreshStage.ProtoFluxDiscreteChangesPost:
			ProtoFlux.RunDiscreteChanges();
			return true;
		case RefreshStage.Destructions:
			return UpdateManager.RunDestructions();
		case RefreshStage.Changes:
			if (UpdateManager.RunChangeApplications())
			{
				UpdateManager.FinishChangeUpdateCycle();
				return true;
			}
			return false;
		case RefreshStage.MovedSlots:
			LastMovedSlots = GeneralMovedHierarchyEvents.RunMovedEvents();
			return true;
		case RefreshStage.UserPose:
			UpdateLocalUserOutputPositions(LocalUser.Root);
			try
			{
				this.FinalUserPoseUpdated?.Invoke();
			}
			catch (Exception ex)
			{
				UniLog.Log("Exception running final user pose update: " + ex);
			}
			return true;
		case RefreshStage.AudioSystem:
			Audio.Update();
			return true;
		case RefreshStage.ParticleSystems:
			ParticleSystems.Update();
			return true;
		case RefreshStage.MaterialUpdate:
			if (Render.IsRenderingSupported)
			{
				Render.Materials.ProcessUpdate();
			}
			return true;
		case RefreshStage.RenderUpdate:
			if (Engine.RenderSystem.HasRenderer)
			{
				Render.ComputeRenderUpdate();
			}
			return true;
		case RefreshStage.ValidatingPermissions:
			Permissions.RunValidations();
			return true;
		default:
			throw new NotSupportedException("Unsupported Refresh Stage: " + Stage);
		}
	}

	private void UpdateLocalUserOutputPositions(UserRoot root)
	{
		if (root == null)
		{
			return;
		}
		Slot slot = root.OverrideRoot.Target ?? root.Slot;
		LocalUserGlobalPosition = slot.LocalPointToGlobal(InputInterface.GlobalTrackingOffset);
		LocalUserGlobalRotation = slot.GlobalRotation;
		LocalUserGlobalScale = slot.GlobalScale;
		LocalUserRenderSettings = root.RenderSettings.Target;
		Slot target = root.OverrideView.Target;
		ViewPositionIsExternal = false;
		if (target != null)
		{
			OverrideViewPosition = true;
			LocalUserViewPosition = target.GlobalPosition;
			LocalUserViewRotation = target.GlobalRotation;
			LocalUserViewScale = target.GlobalScale;
		}
		else if (!InputInterface.VR_Active)
		{
			OverrideViewPosition = true;
			ScreenController target2 = root.ScreenController.Target;
			Slot headSlot = root.HeadSlot;
			if (target2 != null)
			{
				LocalUserViewPosition = target2.ViewPosition;
				LocalUserViewRotation = target2.ViewRotation;
				LocalUserViewScale = slot?.GlobalScale ?? float3.One;
				ViewPositionIsExternal = !(target2.ActiveTargetting.Target is FirstPersonTargettingController);
			}
			else if (headSlot != null)
			{
				LocalUserViewPosition = headSlot.GlobalPosition;
				LocalUserViewRotation = headSlot.GlobalRotation;
				LocalUserViewScale = headSlot.GlobalScale;
			}
		}
		else
		{
			Slot headSlot2 = root.HeadSlot;
			if (headSlot2 != null)
			{
				LocalUserViewPosition = headSlot2.GlobalPosition;
				LocalUserViewRotation = headSlot2.GlobalRotation;
				LocalUserViewScale = headSlot2.GlobalScale;
			}
			OverrideViewPosition = false;
		}
		if (InputInterface.ScreenActive)
		{
			LocalUserDesktopFOV = MathX.Clamp(0.0001f, root.DesktopFOV, 179f);
		}
		ActiveSkyboxMaterial = (KeyOwner(Skybox.ACTIVE_SKYBOX_KEY) as Skybox)?.Material?.Asset;
		AmbientLight = (KeyOwner(AmbientLightSH2.ACTIVE_AMBIENT_LIGHT) as AmbientLightSH2)?.AmbientLight.Value ?? default(SphericalHarmonicsL2<colorX>);
	}

	private void FinishRefresh()
	{
		LastTotalSlots = _slots.Count;
		Debug.FinalizeFrame();
		UserRoot root = LocalUser.Root;
		if (root != null)
		{
			LocalUser.MissingRootFrames = 0;
			LocalUser.respawnRequest.Value = false;
			UpdateLocalUserOutputPositions(root);
			if (!LocalUser.Root.Slot.ActiveSelf)
			{
				RunSynchronously(delegate
				{
					LocalUser.Root.Slot.ActiveSelf_Field.ActiveLink?.ReleaseLink();
					LocalUser.Root.Slot.ActiveSelf = true;
				});
			}
		}
		else if (!this.IsUserspace() && !BlockAutoRespawn && LocalUser.HeadDevice != HeadOutputDevice.Headless && LocalUser.MissingRootFrames++ == 10 && !LocalUser.respawnRequest)
		{
			LocalUser.respawnRequest.Value = true;
			LocalUser.MissingRootFrames = 0;
			Coroutines.StartTask(async delegate
			{
				await CommonAvatarBuilder.SetupAvatarAccessKey(this);
			});
		}
		if (State == WorldState.Running && !Permissions.PermissionHandlingInitialized)
		{
			Permissions.InitializePermissionHandling();
		}
		Stage = RefreshStage.RefreshBegin;
		ConnectorManager.ImplementerUnlock();
		Session?.Sync.ResumeProcessing();
		RefreshRunning = false;
	}

	public void FinishSyncCycle()
	{
		IncrementSyncTick();
	}

	public void IncrementSyncTick()
	{
		SyncTick++;
		if (IsAuthority)
		{
			IncrementStateVersion();
		}
	}

	/// <summary>
	/// Runs an action in sync with the world update from the update thread, when changes to the data model are allowed by the running code.
	/// This allows you to schedule modifications to the world from background threads and other worlds.
	/// The action is ran as soon as possible, usually between the world's own update stages.
	/// This action is not guaranteed to run if the world is destroyed.
	/// </summary>
	/// <param name="action">The action to run synchronously</param>
	/// <param name="immediatellyIfPossible">If true, will run the action immediatelly if called from the world update thread, rather than scheduling it in a queue.</param>
	public void RunSynchronously(Action action, bool immediatellyIfPossible = false, IUpdatable updatable = null, bool evenIfDisposed = false)
	{
		if (IsDisposed)
		{
			if (evenIfDisposed)
			{
				try
				{
					action();
				}
				catch (Exception ex)
				{
					UniLog.Error("Exception running on disposal synchronous action on disposed world:\n" + ex);
				}
			}
		}
		else if (immediatellyIfPossible && CanMakeSynchronousChanges)
		{
			if (updatable == null || !updatable.IsRemoved || evenIfDisposed)
			{
				action();
			}
		}
		else
		{
			synchronousActions.Enqueue(new SynchronousAction(action, updatable, evenIfDisposed));
		}
	}

	public void RunInBackground(Action action, WorkType workType = WorkType.Background)
	{
		Engine.WorkProcessor.Enqueue(action, workType);
	}

	public void RunAfterValidations(Action action)
	{
		Permissions.RunAfterValidations(action);
	}

	public Component KeyOwner(string key)
	{
		if (_keys.TryGetElement(key, out SyncRef<Component> element))
		{
			return element.Target;
		}
		return null;
	}

	public int KeyVersion(string key)
	{
		if (_keyVersions.TryGetValue(key, out var value))
		{
			return value;
		}
		return 0;
	}

	public bool IsKeyInUse(string key)
	{
		return KeyOwner(key) != null;
	}

	internal bool RequestKey(Component component, string key, int version, bool onlyFree)
	{
		if (_keys.TryGetTarget(key, out Component target))
		{
			if (target == component)
			{
				if (version > 0)
				{
					if (_keyVersions.TryGetValue(key, out var value))
					{
						if (value < version)
						{
							_keyVersions[key] = version;
						}
					}
					else
					{
						_keyVersions.Add(key, version);
					}
				}
				return true;
			}
			if (onlyFree)
			{
				return false;
			}
			_keys.Remove(key);
			_keyVersions.Remove(key);
		}
		_keys.Add(key, component);
		if (version > 0)
		{
			_keyVersions.Add(key, version);
		}
		return true;
	}

	internal void RemoveKey(Component component, string key)
	{
		if (_keys.TryGetTarget(key, out Component target) && target == component)
		{
			_keys.Remove(key);
			_keyVersions.Remove(key);
		}
	}

	internal void InformOfKeyAssignment(Component component, string key)
	{
		component.KeyAssigned(key);
	}

	internal void InformOfKeyRemoval(Component component, string key)
	{
		component.KeyRemoved(key);
	}

	private void KeyRemoved(SyncDictionary<string, SyncRef<Component>> dict, string key, SyncRef<Component> syncRef)
	{
		if (syncRef.Target != null)
		{
			InformOfKeyRemoval(syncRef.Target, key);
		}
	}

	private void KeyAdded(SyncDictionary<string, SyncRef<Component>> dict, string key, SyncRef<Component> syncRef)
	{
		if (syncRef.Target != null)
		{
			InformOfKeyAssignment(syncRef.Target, key);
			return;
		}
		syncRef.OnObjectAvailable += delegate(SyncRef<Component> r)
		{
			InformOfKeyAssignment(r.Target, key);
		};
	}

	private void KeysBeforeClear(SyncDictionary<string, SyncRef<Component>> dict)
	{
		foreach (KeyValuePair<string, SyncRef<Component>> item in dict)
		{
			if (item.Value.Target != null)
			{
				InformOfKeyRemoval(item.Value.Target, item.Key);
			}
		}
	}

	public T GetLocalRegisteredComponent<T>(string key, Action<T> onCreate, bool replaceExisting, bool updateExisting) where T : Component, new()
	{
		T typedComponent;
		if (_locallyRegisteredComponents.TryGetValue(key, out Component value))
		{
			typedComponent = value as T;
			if (typedComponent != null)
			{
				if (updateExisting)
				{
					onCreate?.Invoke(typedComponent);
				}
				return typedComponent;
			}
			if (!replaceExisting)
			{
				throw new Exception("An incompatible component is stored under the key " + key);
			}
		}
		Slot localAssetsSlot = LocalAssetsSlot;
		typedComponent = localAssetsSlot.AttachComponent<T>();
		onCreate?.Invoke(typedComponent);
		_locallyRegisteredComponents.Add(key, typedComponent);
		typedComponent.Destroyed += delegate
		{
			if (_locallyRegisteredComponents.TryGetValue(key, out Component value2) && typedComponent == value2)
			{
				_locallyRegisteredComponents.Remove(key);
			}
		};
		return typedComponent;
	}

	public Slot AddSlot(string name = "Slot", bool persistent = true)
	{
		return AddSlot(RootSlot, name, persistent);
	}

	public Slot AddSlot(Slot parent, string name = "Slot", bool persistent = true)
	{
		Slot slot = InternalAddSlot(parent.IsInInitPhase);
		slot.Name = name;
		slot.SetParent(parent, keepGlobalTransform: false);
		if (!persistent)
		{
			slot.PersistentSelf = false;
		}
		slot.Tag = parent.Tag;
		return slot;
	}

	public Slot AddLocalSlot(string name = "Local Slot", bool persistent = false)
	{
		return AddLocalSlot(RootSlot, name, persistent);
	}

	public Slot AddLocalSlot(Slot parent, string name = "Local Slot", bool persistent = false)
	{
		Slot slot = InternalAddLocalSlot();
		slot.Name = name;
		slot.SetParent(parent, keepGlobalTransform: false);
		if (!persistent)
		{
			slot.PersistentSelf = false;
		}
		slot.Tag = parent.Tag;
		return slot;
	}

	public void RemoveSlot(Slot slot)
	{
		if (!slot.IsDestroyed)
		{
			if (slot.IsLocalElement)
			{
				InternalRemoveLocalSlot(slot);
			}
			else
			{
				InternalRemoveSlot(slot.ReferenceID);
			}
		}
	}

	private Slot InternalAddLocalSlot()
	{
		Slot slot = new Slot(startInInitPhase: false);
		ReferenceController.LocalAllocationBlockBegin();
		slot.Initialize(this);
		ReferenceController.LocalAllocationBlockEnd();
		_localSlots.Add(slot);
		return slot;
	}

	private Slot InternalAddSlot(bool inInitializationPhase)
	{
		if (IsDisposed)
		{
			throw new Exception("Cannot add new slots to a disposed world");
		}
		Slot slot = new Slot(inInitializationPhase);
		RefID key = ReferenceController.PeekID();
		_slots.Add(key, slot, inInitializationPhase);
		return slot;
	}

	private void InternalRemoveLocalSlot(Slot slot)
	{
		_localSlots.Remove(slot);
		slot.PrepareDestruction();
	}

	private void InternalRemoveSlot(RefID key)
	{
		if (IsDisposed)
		{
			throw new Exception("Cannot remove Slot when the world is Disposed (or disposing)");
		}
		_slots.Remove(key);
	}

	private void OnSlotAdded(SyncBagBase<RefID, Slot> bag, RefID key, Slot slot, bool isNew)
	{
		ReferenceController.AllocationBlockBegin(in key);
		slot.Initialize(this);
		ReferenceController.AllocationBlockEnd();
		RunSlotAdded(slot);
	}

	private void OnSlotRemoved(SyncBagBase<RefID, Slot> bag, RefID key, Slot slot)
	{
		RunSlotRemoved(slot);
		slot.PrepareDestruction();
	}

	public int ForeachWorldElement<T>(Action<T> callback, Slot root = null) where T : class, ISyncMember
	{
		return ForeachWorldElement(root ?? RootSlot, callback);
	}

	public int ForeachWorldElement<T>(Slot root, Action<T> callback, Predicate<Slot> slotFilter = null, Predicate<Component> componentFilter = null) where T : class, ISyncMember
	{
		if (root.GetComponent<WorkerInspector>() != null || root.GetComponent<SceneInspector>() != null)
		{
			return 0;
		}
		if (slotFilter != null && !slotFilter(root))
		{
			return 0;
		}
		int num = 0;
		num += root.ForeachSyncMember(callback);
		foreach (Component component in root.Components)
		{
			if (componentFilter == null || componentFilter(component))
			{
				num += component.ForeachSyncMember(callback);
			}
		}
		foreach (Slot child in root.Children)
		{
			num += ForeachWorldElement(child, callback, slotFilter, componentFilter);
		}
		foreach (Slot localChild in root.LocalChildren)
		{
			num += ForeachWorldElement(localChild, callback, slotFilter, componentFilter);
		}
		return num;
	}

	public int ReplaceReferenceTargets(Dictionary<IWorldElement, IWorldElement> replacements, bool nullIfIncompatible, Slot root = null, HashSet<IWorldElement> replaced = null)
	{
		int changedCount = 0;
		ForeachWorldElement(delegate(ISyncRef syncRef)
		{
			if (syncRef.Target != null && !syncRef.IsDriven)
			{
				IWorldElement target = syncRef.Target;
				if (replacements.TryGetValue(target, out IWorldElement value))
				{
					if (syncRef.TrySet(value))
					{
						replaced?.Add(target);
						changedCount++;
					}
					else if (nullIfIncompatible)
					{
						changedCount++;
						syncRef.Target = null;
					}
				}
			}
		}, root);
		return changedCount;
	}

	public int ReplaceReferenceTargets(IWorldElement currentTarget, IWorldElement newTarget, bool nullIfIncompatible, Slot root = null)
	{
		int changedCount = 0;
		ForeachWorldElement(delegate(ISyncRef syncRef)
		{
			if (syncRef.Target == currentTarget)
			{
				if (syncRef.TrySet(newTarget))
				{
					changedCount++;
				}
				else if (nullIfIncompatible)
				{
					changedCount++;
					syncRef.Target = null;
				}
			}
		}, root);
		return changedCount;
	}

	public int ReplaceInvalidReferenceTargets(IWorldElement originalInvalidTarget, IWorldElement newTarget, Slot root = null)
	{
		if (originalInvalidTarget == null)
		{
			throw new ArgumentNullException("originalInvalidTarget");
		}
		int changedCount = 0;
		ForeachWorldElement(delegate(ISyncRef syncRef)
		{
			if (syncRef.State == ReferenceState.Invalid && !(syncRef.Value != originalInvalidTarget.ReferenceID) && syncRef.TrySet(newTarget))
			{
				changedCount++;
			}
		}, root);
		return changedCount;
	}

	public void MoveToTrash(ISyncMember element)
	{
		if (SyncTick == 0L)
		{
			throw new Exception("SyncTick is zero! Cannot move to trash");
		}
		if (IsAuthority)
		{
			element.Dispose();
			return;
		}
		if (!trashbin.TryGetValue(SyncTick, out Dictionary<RefID, ISyncMember> value))
		{
			value = Pool.BorrowDictionary<RefID, ISyncMember>();
			trashbin.Add(SyncTick, value);
		}
		value.Add(element.ReferenceID, element);
	}

	public ISyncMember TryRetrieveFromTrash(ulong tick, RefID id)
	{
		if (tick == 0L)
		{
			return null;
		}
		while (tick <= SyncTick)
		{
			if (trashbin.TryGetValue(tick, out Dictionary<RefID, ISyncMember> value) && value.TryGetValue(id, out var value2))
			{
				value.Remove(id);
				return value2;
			}
			tick++;
		}
		return null;
	}

	internal bool IsInTrash(RefID id, out ulong tick)
	{
		foreach (KeyValuePair<ulong, Dictionary<RefID, ISyncMember>> item in trashbin)
		{
			if (item.Value.ContainsKey(id))
			{
				tick = item.Key;
				return true;
			}
		}
		tick = 0uL;
		return false;
	}

	public void DestroyTrash(ulong time)
	{
		if (!trashbin.TryGetValue(time, out Dictionary<RefID, ISyncMember> value))
		{
			return;
		}
		foreach (KeyValuePair<RefID, ISyncMember> item in value)
		{
			item.Value.Dispose();
		}
		trashbin.Remove(time);
		Pool.Return(ref value);
	}

	private void RunSlotAdded(Slot slot)
	{
		this.SlotAdded?.Invoke(slot);
	}

	private void RunSlotRemoved(Slot slot)
	{
		this.SlotRemoved?.Invoke(slot);
	}

	internal void RunComponentAdded(Slot slot, Component component)
	{
		this.ComponentAdded?.Invoke(slot, component);
	}

	internal void RunComponentRemoved(Slot slot, Component component)
	{
		this.ComponentRemoved?.Invoke(slot, component);
	}

	internal void RunUserJoined(User user)
	{
		this.UserJoined?.Invoke(user);
	}

	internal void RunUserSpawn(User user)
	{
		this.UserSpawn?.Invoke(user);
	}

	internal void RunUserLeft(User user)
	{
		this.UserLeft?.Invoke(user);
	}

	public void RegisterComponentAddedListener<T>(Action<Slot, Component> callback, bool callForExistingComponents = false) where T : Component
	{
		RegisterTypeEvent<T, Action<Slot, Component>>(_componentAddedEvents, callback);
		if (!callForExistingComponents)
		{
			return;
		}
		foreach (T componentsInChild in RootSlot.GetComponentsInChildren<T>())
		{
			callback(componentsInChild.Slot, componentsInChild);
		}
	}

	public void UnregisterComponentAddedListener<T>(Action<Slot, Component> callback)
	{
		UnregisterTypeEvent<T, Action<Slot, Component>>(_componentAddedEvents, callback);
	}

	public void RegisterComponentRemovedListener<T>(Action<Slot, Component> callback) where T : Component
	{
		RegisterTypeEvent<T, Action<Slot, Component>>(_componentRemovedEvents, callback);
	}

	public void UnregisterComponentRemovedListener<T>(Action<Slot, Component> callback)
	{
		UnregisterTypeEvent<T, Action<Slot, Component>>(_componentRemovedEvents, callback);
	}

	private void RegisterTypeEvent<T, D>(Dictionary<Type, D> dictionary, D callback) where D : Delegate
	{
		Type typeFromHandle = typeof(T);
		lock (dictionary)
		{
			if (dictionary.TryGetValue(typeFromHandle, out D value))
			{
				dictionary[typeFromHandle] = (D)Delegate.Combine(value, callback);
			}
			else
			{
				dictionary.Add(typeFromHandle, callback);
			}
		}
	}

	private void UnregisterTypeEvent<T, D>(Dictionary<Type, D> dictionary, D callback) where D : Delegate
	{
		Type typeFromHandle = typeof(T);
		lock (dictionary)
		{
			if (dictionary.TryGetValue(typeFromHandle, out D value))
			{
				Delegate obj = Delegate.Remove(value, callback);
				if ((object)obj == null)
				{
					dictionary.Remove(typeFromHandle);
				}
				else
				{
					dictionary[typeFromHandle] = (D)obj;
				}
				return;
			}
			throw new InvalidOperationException($"Given callback isn't registered for type {typeFromHandle}");
		}
	}

	internal void RegisterGlobalWorker(Worker worker)
	{
		Type type = worker.GetType();
		if (!_globallyRegisteredComponents.TryGetValue(type, out HashSet<Worker> value))
		{
			value = new HashSet<Worker>();
			_globallyRegisteredComponents.Add(type, value);
		}
		value.Add(worker);
	}

	internal void UnregisterGlobalWorker(Worker worker)
	{
		Type type = worker.GetType();
		HashSet<Worker> hashSet = _globallyRegisteredComponents[type];
		hashSet.Remove(worker);
		if (hashSet.Count == 0)
		{
			_globallyRegisteredComponents.Remove(type);
		}
	}

	public List<T> GetGloballyRegisteredComponents<T>(Predicate<T> filter = null) where T : class
	{
		List<T> list = new List<T>();
		GetGloballyRegisteredComponents(list, filter);
		return list;
	}

	public void GetGloballyRegisteredComponents<T>(List<T> list, Predicate<T> filter = null) where T : class
	{
		foreach (KeyValuePair<Type, HashSet<Worker>> globallyRegisteredComponent in _globallyRegisteredComponents)
		{
			if (!typeof(T).IsAssignableFrom(globallyRegisteredComponent.Key))
			{
				continue;
			}
			foreach (Worker item in globallyRegisteredComponent.Value)
			{
				T val = item as T;
				if (filter == null || filter(val))
				{
					list.Add(val);
				}
			}
		}
	}

	public T GetGloballyRegisteredComponent<T>(Predicate<T> filter = null) where T : class
	{
		foreach (KeyValuePair<Type, HashSet<Worker>> globallyRegisteredComponent in _globallyRegisteredComponents)
		{
			if (!typeof(T).IsAssignableFrom(globallyRegisteredComponent.Key))
			{
				continue;
			}
			foreach (Worker item in globallyRegisteredComponent.Value)
			{
				T val = item as T;
				if (filter == null || filter(val))
				{
					return val;
				}
			}
		}
		return null;
	}

	internal void RequestSpawn(User user)
	{
		spawnUsers.Add(user);
	}

	public void RegisterEventReceiver(IWorldEventReceiver receiver)
	{
		foreach (WorldEvent value in Enums.GetValues<WorldEvent>())
		{
			if (receiver.HasEventHandler(value))
			{
				worldEventReceivers[(int)value].Add(receiver);
			}
		}
	}

	public void UnregisterEventReceiver(IWorldEventReceiver receiver)
	{
		foreach (WorldEvent value in Enums.GetValues<WorldEvent>())
		{
			if (receiver.HasEventHandler(value))
			{
				worldEventReceivers[(int)value].Remove(receiver);
			}
		}
	}

	private void SortReceivers(List<IWorldEventReceiver> receivers)
	{
		receivers.Sort((IWorldEventReceiver a, IWorldEventReceiver b) => a.UpdateOrder.CompareTo(b.UpdateOrder));
	}

	private void RunWorldEvents()
	{
		for (int i = 0; i < worldEventTypeCount; i++)
		{
			switch ((WorldEvent)i)
			{
			case WorldEvent.OnUserJoined:
				foreach (User joinedUser in joinedUsers)
				{
					UniLog.Log($"User Joined {RawName}. Username: {joinedUser.UserName}, UserID: {joinedUser.UserID}, AllocID: {joinedUser.AllocationID}, AllocIDstart: {joinedUser.AllocationIDStart},  MachineID: {joinedUser.MachineID}");
					if (!isInitialJoinBatch && Focus == WorldFocus.Focused)
					{
						NotificationPanel.Current?.UserJoined(joinedUser.UserID, joinedUser.UserName, Userspace.GetThumbnailData(this)?.PublicThumbnailUrl);
					}
					RunUserJoined(joinedUser);
					List<IWorldEventReceiver> list = worldEventReceivers[1];
					SortReceivers(list);
					foreach (IWorldEventReceiver item in list)
					{
						if (!item.IsRemoved)
						{
							try
							{
								item.OnUserJoined(joinedUser);
							}
							catch (Exception exception4)
							{
								Debug.Error("Exception running OnUserJoined on: " + item?.ToString() + "\n" + DebugManager.PreprocessException(exception4));
							}
						}
					}
				}
				if (joinedUsers.Count > 0)
				{
					isInitialJoinBatch = false;
				}
				joinedUsers.Clear();
				break;
			case WorldEvent.OnUserSpawn:
				if (this.IsUserspace())
				{
					spawnUsers.Clear();
					break;
				}
				if (spawnUsers.Count > 0)
				{
					SimpleUserSpawn simpleUserSpawn = RootSlot.GetComponentInChildren<SimpleUserSpawn>();
					if (simpleUserSpawn == null)
					{
						simpleUserSpawn = AddSlot("INJECTED AVATAR BUILDER").AttachComponent<SimpleUserSpawn>();
					}
					if (simpleUserSpawn.Slot.GetComponent<IAvatarBuilder>() == null)
					{
						simpleUserSpawn.Slot.AttachComponent<CommonAvatarBuilder>();
					}
				}
				foreach (User spawnUser in spawnUsers)
				{
					UniLog.Log($"User Spawn {RawName}. Username: {spawnUser.UserName}, UserID: {spawnUser.UserID}, MachineID: {spawnUser.MachineID}");
					if (spawnUser.Role == null)
					{
						spawnUser.Role = Permissions.GetDefaultRole(spawnUser);
					}
					RunUserSpawn(spawnUser);
					List<IWorldEventReceiver> list = worldEventReceivers[2];
					SortReceivers(list);
					foreach (IWorldEventReceiver item2 in list)
					{
						if (!item2.IsRemoved)
						{
							UpdateManager.NestCurrentlyUpdating(item2);
							try
							{
								item2.OnUserSpawn(spawnUser);
							}
							catch (Exception exception5)
							{
								Debug.Error("Exception running OnUserSpawn on: " + item2?.ToString() + "\n" + DebugManager.PreprocessException(exception5));
							}
							UpdateManager.PopCurrentlyUpdating(item2);
						}
					}
				}
				spawnUsers.Clear();
				break;
			case WorldEvent.OnUserLeft:
				foreach (User leftUser in leftUsers)
				{
					UniLog.Log($"User Left {RawName}. Username: {leftUser.UserName}, UserID: {leftUser.UserID}, AllocId: {leftUser.AllocationID}, AllocIDstart: {leftUser.AllocationIDStart}, MachineID: {leftUser.MachineID}");
					if (Focus == WorldFocus.Focused)
					{
						NotificationPanel.Current?.UserLeft(leftUser.UserID, leftUser.UserName, Userspace.GetThumbnailData(this)?.PublicThumbnailUrl);
					}
					RunUserLeft(leftUser);
					List<IWorldEventReceiver> list = worldEventReceivers[3];
					SortReceivers(list);
					foreach (IWorldEventReceiver item3 in list)
					{
						if (!item3.IsRemoved)
						{
							UpdateManager.NestCurrentlyUpdating(item3);
							try
							{
								item3.OnUserLeft(leftUser);
							}
							catch (Exception exception2)
							{
								Debug.Error("Exception running OnUserLeft on: " + item3?.ToString() + "\n" + DebugManager.PreprocessException(exception2));
							}
							UpdateManager.PopCurrentlyUpdating(item3);
						}
					}
					DisposeUser(leftUser);
				}
				leftUsers.Clear();
				break;
			case WorldEvent.OnWorldSaved:
			{
				if (!worldSaved)
				{
					break;
				}
				List<IWorldEventReceiver> list = worldEventReceivers[4];
				SortReceivers(list);
				foreach (IWorldEventReceiver item4 in list)
				{
					if (!item4.IsRemoved)
					{
						try
						{
							UpdateManager.NestCurrentlyUpdating(item4);
							item4.OnWorldSaved();
						}
						catch (Exception exception3)
						{
							Debug.Error("Exception when running OnWorldSaved on: " + item4?.ToString() + "\n" + DebugManager.PreprocessException(exception3));
						}
						UpdateManager.PopCurrentlyUpdating(item4);
					}
				}
				worldSaved = false;
				break;
			}
			case WorldEvent.OnFocusChanged:
			{
				if (Focus == _lastFocus)
				{
					break;
				}
				_lastFocus = Focus;
				List<IWorldEventReceiver> list = worldEventReceivers[0];
				SortReceivers(list);
				foreach (IWorldEventReceiver item5 in list)
				{
					if (!item5.IsRemoved)
					{
						try
						{
							UpdateManager.NestCurrentlyUpdating(item5);
							item5.OnFocusChanged(Focus);
						}
						catch (Exception exception)
						{
							Debug.Error("Exception when running OnFocusChanged on: " + item5?.ToString() + "\n" + DebugManager.PreprocessException(exception));
						}
						UpdateManager.PopCurrentlyUpdating(item5);
					}
				}
				break;
			}
			}
		}
	}

	void IWorldElement.ChildChanged(IWorldElement child)
	{
	}

	public string GetSyncMemberName(ISyncMember member)
	{
		if (member == _slots)
		{
			return "Slots";
		}
		if (member == _users)
		{
			return "Users";
		}
		if (member == _keys)
		{
			return "Keys";
		}
		if (member == _keyVersions)
		{
			return "KeyVersions";
		}
		if ((object)member == RootSlot)
		{
			return "RootSlot";
		}
		if (member == Configuration)
		{
			return "Configuration";
		}
		if (member == Permissions)
		{
			return "Permissions";
		}
		return null;
	}

	public SavedGraph SaveWorld()
	{
		refTranslator = refTranslator ?? new ReferenceTranslator();
		SaveControl control = new SaveControl(this, this, refTranslator, RootSlot.GetComponent<UnresolvedReferences>());
		worldSaved = true;
		return new SavedGraph((DataTreeDictionary)Save(control));
	}

	public DataTreeNode Save(SaveControl control)
	{
		try
		{
			IsRunningLongBlockingTask = true;
			RootSlot.RunOnSaving(control);
			control.StartSaving();
			if (IsAuthority && CanMakeSynchronousChanges)
			{
				this.SetUnsavedChanges(state: false);
			}
			DataTreeDictionary dataTreeDictionary = new DataTreeDictionary();
			dataTreeDictionary.Add("VersionNumber", FrooxEngine.Engine.Version.ToString());
			dataTreeDictionary.Add("FeatureFlags", control.StoreFeatureFlags(Engine));
			DataTreeList dataTreeList = new DataTreeList();
			dataTreeDictionary.Add("Types", dataTreeList);
			DataTreeDictionary dataTreeDictionary2 = new DataTreeDictionary();
			dataTreeDictionary.Add("TypeVersions", dataTreeDictionary2);
			dataTreeDictionary.Add("Version String", FrooxEngine.Engine.CurrentVersion);
			dataTreeDictionary.Add("SystemCompatibilityHash", GlobalTypeRegistry.SystemCompatibilityHash);
			DataTreeList dataTreeList2 = new DataTreeList();
			foreach (AssemblyTypeRegistry allowedAssembly in Types.AllowedAssemblies)
			{
				if (!allowedAssembly.IsDependency)
				{
					DataTreeDictionary dataTreeDictionary3 = new DataTreeDictionary();
					dataTreeDictionary3.Add("Name", allowedAssembly.AssemblyName);
					dataTreeDictionary3.Add("Version", allowedAssembly.AssemblyVersion.ToString());
					dataTreeDictionary3.Add("CompatibilityHash", allowedAssembly.CompatibilityHash);
					dataTreeList2.Add(dataTreeDictionary3);
				}
			}
			dataTreeDictionary.Add("Assemblies", dataTreeList2);
			dataTreeDictionary.Add("Configuration", Configuration.Save(control));
			dataTreeDictionary.Add("Name", Configuration.WorldName.Save(control));
			dataTreeDictionary.Add("Access Level", Configuration.AccessLevel.Save(control));
			dataTreeDictionary.Add("Mobile Friendly", Configuration.MobileFriendly.Save(control));
			dataTreeDictionary.Add("Keys", _keys.Save(control));
			dataTreeDictionary.Add("Permissions", Permissions.Save(control));
			dataTreeDictionary.Add("Slots", RootSlot.Save(control));
			if (control.nonpersistentAssets.Count > 0)
			{
				HashSet<IAssetProvider> hashSet = new HashSet<IAssetProvider>(control.nonpersistentAssets);
				foreach (IAssetProvider nonpersistentAsset in control.nonpersistentAssets)
				{
					CollectNonpersistentAssetRefs((Worker)nonpersistentAsset, hashSet);
				}
				bool saveNonPersistent = control.SaveNonPersistent;
				control.SaveNonPersistent = true;
				DataTreeList dataTreeList3 = new DataTreeList();
				foreach (IAssetProvider item in hashSet)
				{
					dataTreeList3.Add(WorkerSaveLoad.SaveWorker(item, control));
				}
				control.SaveNonPersistent = saveNonPersistent;
				dataTreeDictionary.Add("Assets", dataTreeList3);
			}
			control.StoreTypeData(dataTreeList, dataTreeDictionary2);
			control.FinishSave();
			return dataTreeDictionary;
		}
		finally
		{
			IsRunningLongBlockingTask = false;
		}
	}

	public void Load(DataTreeNode node, LoadControl control)
	{
		try
		{
			IsRunningLongBlockingTask = true;
			DataTreeDictionary dataTreeDictionary = node as DataTreeDictionary;
			control.TryLoadVersion(dataTreeDictionary);
			DataTreeDictionary dataTreeDictionary2 = dataTreeDictionary.TryGetDictionary("FeatureFlags");
			if (dataTreeDictionary2 != null)
			{
				control.LoadFeatureFlags(dataTreeDictionary2);
			}
			control.InitializeLoaders();
			if (!control.GetFeatureFlag("ColorManagement").HasValue)
			{
				Coroutines.RunInSeconds(1f, delegate
				{
					switch (IdUtil.GetOwnerType(CorrespondingRecord.OwnerId))
					{
					case OwnerType.User:
						if (CorrespondingRecord.OwnerId != Engine.Cloud.CurrentUserID)
						{
							return;
						}
						break;
					case OwnerType.Group:
						if (!Engine.Cloud.Groups.IsCurrentUserMemberOfGroup(CorrespondingRecord.OwnerId))
						{
							return;
						}
						break;
					}
					this.DisplayNotice("General.Notice".AsLocaleKey(), "Migration.ColorManagement".AsLocaleKey());
				});
			}
			DataTreeList typeList = dataTreeDictionary.TryGetList("Types");
			DataTreeDictionary versionsDict = dataTreeDictionary.TryGetDictionary("TypeVersions");
			control.LoadTypeData(typeList, versionsDict);
			DataTreeNode dataTreeNode = dataTreeDictionary.TryGetNode("Configuration");
			if (dataTreeNode != null)
			{
				Configuration.Load(dataTreeNode, control);
			}
			else
			{
				Configuration.WorldName.Load(dataTreeDictionary["Name"], control);
				DataTreeNode dataTreeNode2 = dataTreeDictionary.TryGetNode("Access Level");
				if (dataTreeNode2 != null)
				{
					Configuration.AccessLevel.Load(dataTreeNode2, control);
				}
				else if (CorrespondingRecord?.RecordId != "R-Home" && !Engine.Cloud.Status.OnlineStatus.DefaultPrivate())
				{
					AccessLevel = SessionAccessLevel.Anyone;
				}
				DataTreeNode dataTreeNode3 = dataTreeDictionary.TryGetNode("Mobile Friendly");
				if (dataTreeNode3 != null)
				{
					Configuration.MobileFriendly.Load(dataTreeNode3, control);
				}
			}
			_keys.Load(dataTreeDictionary["Keys"], control);
			DataTreeNode dataTreeNode4 = dataTreeDictionary.TryGetNode("Permissions");
			if (dataTreeNode4 != null)
			{
				Permissions.Load(dataTreeNode4, control);
			}
			RootSlot.Load(dataTreeDictionary["Slots"], control);
			DataTreeList dataTreeList = dataTreeDictionary.TryGetList("Assets");
			if (dataTreeList != null)
			{
				Slot slot = RootSlot.FindChildOrAdd("Assets").AddSlot("From Nonpersistent");
				foreach (DataTreeNode item in dataTreeList)
				{
					WorkerSaveLoad.WorkerData workerData = WorkerSaveLoad.ExtractWorker(item, control);
					if (workerData.IsValid)
					{
						try
						{
							slot.AttachComponent(workerData.workerType, runOnAttachBehavior: false)?.Load(workerData.loadNode, control);
						}
						catch (Exception ex)
						{
							UniLog.Error("Exception instantiating worker type: " + workerData.workerType?.ToString() + "\n" + ex);
						}
					}
					else
					{
						MissingComponent missingComponent = slot.AttachComponent<MissingComponent>();
						missingComponent.Type.Value = workerData.workerTypename;
						missingComponent.Data.FromRawDataTreeNode(workerData.loadNode);
					}
				}
			}
			if (dataTreeNode4 == null)
			{
				PermissionHelper.SetupCommonRoles(this);
			}
			else if (!control.GetFeatureFlag("RESONITE_LINK").HasValue)
			{
				PermissionHelper.UpgradeResoniteLink(this);
			}
			if (!control.GetFeatureFlag("ResetGUID").HasValue)
			{
				refTranslator = null;
			}
		}
		finally
		{
			IsRunningLongBlockingTask = false;
		}
	}

	private void CollectNonpersistentAssetRefs(Worker assetProvider, HashSet<IAssetProvider> assets)
	{
		List<IAssetRef> list = Pool.BorrowList<IAssetRef>();
		assetProvider.GetSyncMembers(list);
		foreach (IAssetRef item in list)
		{
			if (item.Target != null && !item.Target.IsPersistent)
			{
				IAssetProvider assetProvider2 = (IAssetProvider)item.Target;
				if (assets.Add(assetProvider2))
				{
					CollectNonpersistentAssetRefs((Worker)assetProvider2, assets);
				}
			}
		}
		Pool.Return(ref list);
	}

	public User GetUser(RefID id)
	{
		lock (_userSnapshot)
		{
			return _users[id];
		}
	}

	public User FindUser(Predicate<User> filter)
	{
		lock (_userSnapshot)
		{
			return _users.FirstOrDefault<KeyValuePair<RefID, User>>((KeyValuePair<RefID, User> u) => filter(u.Value)).Value;
		}
	}

	public List<User> FindUsers(Predicate<User> filter)
	{
		lock (_userSnapshot)
		{
			return (from u in _users
				where filter(u.Value)
				select u.Value).ToList();
		}
	}

	public User TryGetUser(RefID id)
	{
		lock (_userSnapshot)
		{
			_users.TryGetValue(id, out User value);
			return value;
		}
	}

	public User GetUserByAllocationID(byte allocationID)
	{
		lock (_userSnapshot)
		{
			return _users.FirstOrDefault<KeyValuePair<RefID, User>>((KeyValuePair<RefID, User> u) => u.Value.AllocationID == allocationID).Value;
		}
	}

	public User GetUserByMachineId(string machineId)
	{
		lock (_userSnapshot)
		{
			return _users.FirstOrDefault<KeyValuePair<RefID, User>>((KeyValuePair<RefID, User> u) => u.Value.MachineID == machineId).Value;
		}
	}

	public User GetUserByUserId(string userId)
	{
		lock (_userSnapshot)
		{
			return _users.FirstOrDefault<KeyValuePair<RefID, User>>((KeyValuePair<RefID, User> u) => u.Value.UserID == userId).Value;
		}
	}

	public User TryGetUser(Predicate<User> predicate)
	{
		lock (_userSnapshot)
		{
			return _users.FirstOrDefault<KeyValuePair<RefID, User>>((KeyValuePair<RefID, User> u) => predicate(u.Value)).Value;
		}
	}

	public void GetUsers(List<User> users, Predicate<User> filter = null)
	{
		lock (_userSnapshot)
		{
			foreach (KeyValuePair<RefID, User> user in _users)
			{
				if (filter == null || filter(user.Value))
				{
					users.Add(user.Value);
				}
			}
		}
	}

	public User GetHost()
	{
		lock (_userSnapshot)
		{
			return _users.First<KeyValuePair<RefID, User>>((KeyValuePair<RefID, User> u) => u.Value.IsHost).Value;
		}
	}

	public void SwapForOthers(List<User> userList, Predicate<User> filter = null)
	{
		int count = userList.Count;
		lock (_userSnapshot)
		{
			foreach (User allUser in AllUsers)
			{
				if (allUser == LocalUser || (filter != null && !filter(allUser)))
				{
					continue;
				}
				bool flag = false;
				for (int i = 0; i < count; i++)
				{
					if (userList[i] == allUser)
					{
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					userList.Add(allUser);
				}
			}
		}
		userList.RemoveRange(0, count);
	}

	public void GetOthers(List<User> excludeList, List<User> others)
	{
		lock (_userSnapshot)
		{
			foreach (User allUser in AllUsers)
			{
				if (!excludeList.Contains(allUser) && allUser != LocalUser)
				{
					others.Add(allUser);
				}
			}
		}
	}

	public User CreateHostUser()
	{
		lock (_userSnapshot)
		{
			User user = InternalCreateUser();
			SetLocalUser(user);
			user.InitializingEnabled = true;
			user.UserName = Engine.LocalUserName;
			user.UserID = Engine.Cloud.CurrentUserID;
			user.UserSessionId = Engine.Cloud.Status.UserSessionId;
			user.AllocationIDStart = 1uL;
			user.AllocationID = 0;
			user.MachineID = Engine.LocalDB.MachineID;
			user.HeadDevice = Engine.InputInterface.HeadOutputDevice;
			user.Platform = Engine.Platform;
			user.EngineVersionNumber = FrooxEngine.Engine.VersionNumber.ToString();
			user.RendererName = Engine.RenderSystem?.RendererName ?? "<null>";
			user.RuntimeVersion = Environment.Version.ToString();
			user.KioskMode = Userspace.KioskMode;
			user.SessionJoinTimestamp = DateTime.UtcNow;
			user.SetupDeviceInfos(InputInterface.CollectDeviceInfos());
			InputInterface.GetEyeTracking(out var hasEyeTracking, out var hasPupilTracking);
			user.SetupEyeTracking(hasEyeTracking, hasPupilTracking);
			user.SetupMouthTracking(InputInterface.GetMouthTrackingParameters());
			user.SetupExtraIds(ProcessExtraIds(Engine.PlatformInterface.GetExtraUserIds()));
			user.InitializingEnabled = false;
			RunSynchronously(delegate
			{
				user.AddStream<DummyAsyncStream>().Name = "TimeSync";
			});
			return user;
		}
	}

	internal static Dictionary<string, string> ProcessExtraIds(Dictionary<string, string> extraIds)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		foreach (KeyValuePair<string, string> extraId in extraIds)
		{
			string key = extraId.Key;
			string id = extraId.Value;
			ProcessExtraId(ref key, ref id);
			dictionary.Add(key, id);
		}
		return dictionary;
	}

	internal static void ProcessExtraId(ref string key, ref string id)
	{
		if (key != "UID")
		{
			id = CryptoHelper.HashIDToToken("06a92b0d-57b3-426f-9cc3-2f812a765554" + id);
		}
		key = CryptoHelper.HashIDToToken("06a92b0d-57b3-426f-9cc3-2f812a765554" + key);
	}

	internal void SetLocalUser(User user)
	{
		if (LocalUser != null)
		{
			throw new Exception("Local user is already assigned!");
		}
		LocalUser = user;
	}

	internal User CreateGuestUser()
	{
		lock (_userSnapshot)
		{
			if (!HasFreeUserCapacity)
			{
				throw new Exception("User capacity at maximum!");
			}
			User user = InternalCreateUser();
			user.AllocationID = FindFreeUserAllocationID();
			user.AllocationIDStart = ReferenceController.GetLatestPosition(user.AllocationID) + 1;
			user.SessionJoinTimestamp = DateTime.UtcNow;
			return user;
		}
	}

	internal void RemoveUser(User user)
	{
		lock (_userSnapshot)
		{
			if (!IsAuthority)
			{
				throw new Exception("Only host can directly remove users");
			}
			_users.Remove(user.ReferenceID);
		}
	}

	internal void HostConnectionClosed()
	{
		if (HostConnectionClosedHook != null)
		{
			Engine.GlobalCoroutineManager.RunInUpdates(0, HostConnectionClosedHook);
		}
		else
		{
			Destroy();
		}
	}

	private byte FindFreeUserAllocationID()
	{
		lock (_userSnapshot)
		{
			byte id;
			for (id = 1; id < 256; id++)
			{
				if (!_users.Any<KeyValuePair<RefID, User>>((KeyValuePair<RefID, User> u) => u.Value.AllocationID == id))
				{
					return id;
				}
			}
			throw new Exception("Cannot find free allocation ID for the user!");
		}
	}

	private User InternalCreateUser()
	{
		User user = new User();
		RefID key = ReferenceController.PeekID();
		_users.Add(key, user, isNew: true);
		return user;
	}

	private void UserAdded(SyncBagBase<RefID, User> bag, RefID key, User user, bool isNew)
	{
		lock (_userSnapshot)
		{
			_userSnapshot.Add(key, user);
		}
		ReferenceController.AllocationBlockBegin(in key);
		user.Initialize(bag);
		ReferenceController.AllocationBlockEnd();
		if (IsAuthority)
		{
			GenerateDeltaSyncData = _users.Count > 1;
		}
		joinedUsers.Add(user);
		RequestSpawn(user);
		LastUserUpdate = DateTime.UtcNow;
	}

	private void UserRemoved(SyncBagBase<RefID, User> bag, RefID key, User user)
	{
		lock (_userSnapshot)
		{
			_userSnapshot.Remove(key);
		}
		if (IsAuthority)
		{
			GenerateDeltaSyncData = _users.Count > 1;
		}
		leftUsers.Add(user);
		LastUserUpdate = DateTime.UtcNow;
	}

	private void DisposeUser(User user)
	{
		foreach (Stream stream in user.Streams)
		{
			stream.Dispose();
		}
		user.Destroy();
	}

	internal async Task<JoinGrant> VerifyJoinRequest(SessionConnection connection)
	{
		UniLog.Log($"Verifying Join Request. UserID: {connection.UserID}, Username: {connection.Username}, AccessLevel: {AccessLevel}");
		UserFingerprint fingerprint = new UserFingerprint(connection);
		if (BanManager.IsBanned(fingerprint))
		{
			return JoinGrant.Deny("World.Error.AccessDenied");
		}
		RecordId recordId = CorrespondingRecord?.CombinedRecordId;
		if (recordId != null && BanManager.IsBanned(fingerprint, recordId))
		{
			return JoinGrant.Deny("World.Error.AccessDenied");
		}
		if (BanManager.IsTempBanned(connection.UserID ?? connection.MachineID))
		{
			return JoinGrant.Deny("World.Error.SecurityViolation");
		}
		if (connection.UserID != null)
		{
			if (connection.CloudUser.IsSpectatorBanned)
			{
				connection.SpectatorBan = true;
			}
			if (connection.CloudUser.IsMuteBanned && AccessLevel >= SessionAccessLevel.RegisteredUsers)
			{
				connection.MuteBan = true;
			}
			if (connection.CloudUser.IsPublicBanned && connection.CloudUser.PublicBanType != PublicBanType.Soft)
			{
				return JoinGrant.Deny("World.Error.AccessDenied");
			}
		}
		if (Configuration.UseCustomJoinVerifier.Value)
		{
			IWorldUserJoinVerifier target = Configuration.CustomJoinVerifier.Target;
			if (target != null)
			{
				JoinGrant? joinGrant = await target.VerifyJoinRequest(connection);
				if (joinGrant.HasValue)
				{
					return joinGrant.Value;
				}
			}
		}
		await Permissions.ProcessJoinRequest(connection).ConfigureAwait(continueOnCapturedContext: false);
		if (connection.UserID != null)
		{
			if (CloudVariableHelper.IsValidPath(DenyUserCloudVariable))
			{
				CloudResult<bool> cloudResult = await Engine.Cloud.Variables.Read<bool>(connection.UserID, DenyUserCloudVariable).ConfigureAwait(continueOnCapturedContext: false);
				if ((cloudResult.IsError && cloudResult.State != HttpStatusCode.NotFound) || cloudResult.Entity)
				{
					UniLog.Log("User " + connection.UserID + " join denied through cloud variable: " + DenyUserCloudVariable);
					return JoinGrant.Deny("World.Error.AccessDenied");
				}
			}
			if (IsUserAllowed(connection.UserID))
			{
				return JoinGrant.Allow();
			}
			if (CloudVariableHelper.IsValidPath(AllowUserCloudVariable))
			{
				CloudResult<bool> cloudResult2 = await Engine.Cloud.Variables.Read<bool>(connection.UserID, AllowUserCloudVariable).ConfigureAwait(continueOnCapturedContext: false);
				if (cloudResult2.IsOK && cloudResult2.Entity)
				{
					UniLog.Log("User " + connection.UserID + " join allowed through cloud variable: " + AllowUserCloudVariable);
					return JoinGrant.Allow();
				}
			}
			if (connection.CloudUser.IsPublicBanned && connection.CloudUser.PublicBanType == PublicBanType.Soft)
			{
				return JoinGrant.Deny("World.Error.AccessDenied");
			}
			if (CloudVariableHelper.IsValidPath(RequiredUserJoinCloudVariable))
			{
				CloudResult<bool> cloudResult3 = await Engine.Cloud.Variables.Read<bool>(connection.UserID, RequiredUserJoinCloudVariable).ConfigureAwait(continueOnCapturedContext: false);
				if (!cloudResult3.IsOK || !cloudResult3.Entity)
				{
					UniLog.Log("User " + connection.UserID + " join denied through cloud variable: " + RequiredUserJoinCloudVariable);
					return JoinGrant.Deny(RequiredUserJoinCloudVariableDenyMessage ?? "World.Error.AccessDenied");
				}
			}
		}
		else
		{
			if (CloudVariableHelper.IsValidPath(RequiredUserJoinCloudVariable))
			{
				return JoinGrant.Deny("World.Error.OnlyRegisteredUsers");
			}
			if (!connection.ExtraIDs.TryGetValue(CryptoHelper.HashIDToToken("06a92b0d-57b3-426f-9cc3-2f812a765554UID"), out string value))
			{
				UniLog.Log("User " + connection.MachineID + " doesn't have valid UID, cannot verify join.");
				return JoinGrant.Deny("World.Error.AccessDenied");
			}
			if ((await Engine.Cloud.Moderation.IsPublicBanned(value).ConfigureAwait(continueOnCapturedContext: false)).Entity)
			{
				UniLog.Log("User " + connection.MachineID + " is publicly banned.");
				return JoinGrant.Deny("World.Error.AccessDenied");
			}
		}
		if (!HasFreeUserSpots)
		{
			return JoinGrant.Deny("World.Error.UserLimitReached");
		}
		switch (AccessLevel)
		{
		case SessionAccessLevel.Anyone:
			return JoinGrant.Allow();
		case SessionAccessLevel.RegisteredUsers:
			if (string.IsNullOrEmpty(connection.UserID))
			{
				return JoinGrant.Deny("World.Error.OnlyRegisteredUsers");
			}
			return JoinGrant.Allow();
		case SessionAccessLevel.ContactsPlus:
		{
			if (string.IsNullOrEmpty(connection.UserID))
			{
				return JoinGrant.Deny("World.Error.OnlyRegisteredUsers");
			}
			if (Engine.Cloud.Contacts.IsContact(connection.UserID))
			{
				return JoinGrant.Allow();
			}
			string text = await connection.RequestContactCheckKey().ConfigureAwait(continueOnCapturedContext: false);
			if (!OneTimeVerificationKey.IsValidId(text))
			{
				return JoinGrant.Deny("World.Error.OnlyContactsOfContacts");
			}
			CheckContactData checkContactData = new CheckContactData();
			checkContactData.OwnerId = connection.UserID;
			checkContactData.VerificationKey = text;
			checkContactData.Contacts = new List<string>();
			List<User> list = Pool.BorrowList<User>();
			GetUsers(list);
			foreach (User item in list)
			{
				if (!item.IsHost && item.UserID != null)
				{
					checkContactData.Contacts.Add(item.UserID);
				}
			}
			Pool.Return(ref list);
			if ((await Engine.Cloud.Security.CheckContact(checkContactData).ConfigureAwait(continueOnCapturedContext: false)).Entity)
			{
				return JoinGrant.Allow();
			}
			return JoinGrant.Deny("World.Error.OnlyContactsOfContacts");
		}
		case SessionAccessLevel.Contacts:
			if (connection.UserID != null && Engine.Cloud.Contacts.IsContact(connection.UserID))
			{
				return JoinGrant.Allow();
			}
			return JoinGrant.Deny("World.Error.OnlyContacts");
		case SessionAccessLevel.LAN:
			return JoinGrant.Allow();
		case SessionAccessLevel.Private:
			return JoinGrant.Deny("World.Error.Private");
		default:
			return JoinGrant.Deny("Invalid Access Level: " + AccessLevel);
		}
	}

	public Coroutine RunInSeconds(float seconds, Action action)
	{
		return Coroutines.RunInSeconds(seconds, action);
	}

	public Coroutine RunInUpdates(int updates, Action action)
	{
		return Coroutines.RunInUpdates(updates, action);
	}

	public void Destroy()
	{
		if (IsDestroyed)
		{
			return;
		}
		IsDestroyed = true;
		WorldManager.DestroyWorld(this);
		lock (stateLock)
		{
			if (this._worldDestroyed == null)
			{
				return;
			}
			Delegate[] invocationList = this._worldDestroyed.GetInvocationList();
			foreach (Delegate obj in invocationList)
			{
				try
				{
					((Action<World>)obj)(this);
				}
				catch (Exception exception)
				{
					UniLog.Error($"Exception calling WorldDestroyed Event on listener: {obj}:\n" + DebugManager.PreprocessException(exception));
				}
			}
		}
	}

	internal void FatalError(string error)
	{
		UniLog.Error("Fatal Error in updating world " + RawName + ":\n" + error);
		WorldLoadProgress.ShowMessage(Name, "World.Error.WorldCrash".AsLocaleKey("<color=#f00>{0}</color>"), "World.Error.WorldCrashDetail".AsLocaleKey("<color=#f00>{0}</color>"), ProgressStage.Failed);
		Destroy();
	}

	public void Dispose()
	{
		if (_disposed)
		{
			throw new Exception("World is already disposed");
		}
		_disposed = true;
		if (IsAuthority)
		{
			Engine.Cloud.Moderation.OnUserSpectatorBanned -= Cloud_OnUserSpectatorBanned;
			Engine.Cloud.Moderation.OnUserMuted -= Cloud_OnUserMuted;
		}
		Engine.Cloud.Moderation.OnUserPublicBanned -= Cloud_OnUserPublicBanned;
		SynchronousAction val;
		while (synchronousActions.TryDequeue(out val))
		{
			if (val.evenDisposed)
			{
				try
				{
					val.action();
				}
				catch (Exception ex)
				{
					UniLog.Error("Exception running OnWorldDispose synchronous action:\n" + ex);
				}
			}
		}
		Engine.AudioSystem.GlobalVolumesChanged -= AudioSystem_GlobalVolumesChanged;
		Engine.AudioSystem.DefaultAudioInputChanged -= InputInterface_DefaultAudioInputChanged;
		if (Session != null)
		{
			DisposeSystem(Session);
		}
		try
		{
			try
			{
				UpdateManager.RunDestructions();
				foreach (KeyValuePair<RefID, User> user in _users)
				{
					foreach (Stream stream in user.Value.Streams)
					{
						stream.Dispose();
					}
					foreach (UserComponent component in user.Value.Components)
					{
						component.Dispose();
					}
					user.Value.Dispose();
				}
				foreach (Component component2 in RootSlot.Components)
				{
					component2.Dispose();
				}
				foreach (KeyValuePair<RefID, Slot> slot in _slots)
				{
					foreach (Component component3 in slot.Value.Components)
					{
						component3.Dispose();
					}
				}
				foreach (Slot localSlot in _localSlots)
				{
					foreach (Component component4 in localSlot.Components)
					{
						component4.Dispose();
					}
				}
				foreach (KeyValuePair<RefID, Slot> slot2 in _slots)
				{
					slot2.Value.Dispose();
				}
				foreach (Slot localSlot2 in _localSlots)
				{
					localSlot2.Dispose();
				}
				_slots.Dispose();
				_users.Dispose();
				Configuration.Dispose();
				_keys.Dispose();
				_keyVersions.Dispose();
				Permissions.Dispose();
				RootSlot.Dispose();
				Types.Dispose();
			}
			catch (Exception ex2)
			{
				UniLog.Error("Exception disposing world objects:\n" + ex2);
			}
			if (ResoniteLink != null)
			{
				DisposeSystem(ResoniteLink);
			}
			DisposeSystem(Audio);
			DisposeSystem(Physics);
			DisposeSystem(Render);
			DisposeSystem(Debug);
			DisposeSystem(ConnectorManager);
			DisposeSystem(AssetManager);
			DisposeSystem(SyncController);
			DisposeSystem(LinkManager);
			DisposeSystem(ProtoFlux);
			DisposeSystem(UpdateManager);
			DisposeSystem(Coroutines);
			DisposeSystem(Input);
			DisposeSystem(ReferenceController);
			DisposeSystem(ParticleSystems);
			_locallyRegisteredComponents.Clear();
			_locallyRegisteredComponents = null;
			foreach (KeyValuePair<Type, HashSet<Worker>> globallyRegisteredComponent in _globallyRegisteredComponents)
			{
				globallyRegisteredComponent.Value.Clear();
			}
			_globallyRegisteredComponents.Clear();
			_globallyRegisteredComponents = null;
		}
		catch (Exception exception)
		{
			UniLog.Error("Exception Disposing World:\n" + DebugManager.PreprocessException(exception));
		}
		synchronousActions.Clear();
		synchronousActions = null;
		static void DisposeSystem(IDisposable system)
		{
			try
			{
				system.Dispose();
			}
			catch (Exception value)
			{
				UniLog.Error($"Exception disposing {system?.GetType()?.Name}:\n{value}");
			}
		}
	}

	public SessionInfo GenerateSessionInfo()
	{
		SessionInfo sessionInfo = new SessionInfo();
		UpdateSessionInfo(sessionInfo);
		return sessionInfo;
	}

	private static T UpdateField<T>(T current, T value, ref bool updated) where T : IEquatable<T>
	{
		if (current == null)
		{
			if (value == null)
			{
				return value;
			}
		}
		else if (current.Equals(value))
		{
			return value;
		}
		UniLog.Log($"Updated: {current} -> {value}");
		updated = true;
		return value;
	}

	private static T UpdateEnum<T>(T current, T value, ref bool updated) where T : struct, Enum
	{
		if (Enums.Equals(current, value))
		{
			return value;
		}
		UniLog.Log($"Updated: {current} -> {value}");
		updated = true;
		return value;
	}

	public bool UpdateSessionInfo(SessionInfo info)
	{
		if (State == WorldState.Initializing)
		{
			throw new InvalidOperationException("Cannot update session info on a world that's not initialized yet");
		}
		if (State == WorldState.Failed)
		{
			throw new InvalidOperationException("Cannot update session info on a world that's failed");
		}
		if (IsDestroyed || IsDisposed)
		{
			throw new InvalidOperationException("Cannot update session info on ended worlds");
		}
		bool flag = info.ExpirationProgress >= 0.8f;
		bool flag2 = info.LastInviteListUpdate != LastInviteListUpdate;
		bool flag3 = flag || flag2 || info.LastWorldConfigurationUpdate != Configuration.LastConfigurationUpdate || info.LastWorldUserUpdate != LastUserUpdate;
		string text = Userspace.GetThumbnailData(this)?.PublicThumbnailUrl?.OriginalString;
		if (text != info.ThumbnailUrl)
		{
			flag3 = true;
		}
		if (DateTime.UtcNow - info.LastUpdate >= SessionInfo.SESSION_UPDATE_INTERVAL)
		{
			flag3 = true;
		}
		if (!flag3)
		{
			return false;
		}
		bool flag4 = false;
		flag4 = flag4 || flag2;
		flag4 = flag4 || flag;
		info.SessionBeginTime = UpdateField(info.SessionBeginTime, Time.LocalSessionBeginTime.ToUniversalTime(), ref flag4);
		info.Name = UpdateField(info.Name, Name?.ClampLength(256), ref flag4);
		info.Description = UpdateField(info.Description, Description?.ClampLength(16384), ref flag4);
		info.SessionId = UpdateField(info.SessionId, SessionId, ref flag4);
		info.BroadcastKey = UpdateField(info.BroadcastKey, Configuration.ActualBroadcastKey, ref flag4);
		info.UniverseId = UpdateField(info.UniverseId, UniverseId, ref flag4);
		info.MobileFriendly = UpdateField(info.MobileFriendly, MobileFriendly, ref flag4);
		info.HideFromListing = UpdateField(info.HideFromListing, HideFromListing, ref flag4);
		info.AccessLevel = UpdateEnum(info.AccessLevel, AccessLevel, ref flag4);
		info.AwayKickEnabled = UpdateField(info.AwayKickEnabled, AwayKickEnabled, ref flag4);
		info.AwayKickMinutes = UpdateField(info.AwayKickMinutes, (float)AwayKickInterval.TotalMinutes, ref flag4);
		info.HostUserId = UpdateField(info.HostUserId, HostUser.UserID, ref flag4);
		info.HostUserSessionId = UpdateField(info.HostUserSessionId, HostUser.UserSessionId, ref flag4);
		info.HostUsername = UpdateField(info.HostUsername, HostUser.UserName, ref flag4);
		info.HostMachineId = UpdateField(info.HostMachineId, HostUser.MachineID, ref flag4);
		info.HeadlessHost = UpdateField(info.HeadlessHost, HostUser.HeadDevice == HeadOutputDevice.Headless, ref flag4);
		if (info.CompatibilityHash == null)
		{
			info.AppVersion = Engine.VersionString;
			info.CompatibilityHash = Types.CompatibilityHash;
			info.SystemCompatibilityHash = GlobalTypeRegistry.SystemCompatibilityHash;
			info.DataModelAssemblies = new List<AssemblyInfo>();
			foreach (AssemblyTypeRegistry allowedAssembly in Types.AllowedAssemblies)
			{
				if (!allowedAssembly.IsDependency)
				{
					info.DataModelAssemblies.Add(new AssemblyInfo
					{
						Name = allowedAssembly.AssemblyName,
						CompatibilityHash = allowedAssembly.CompatibilityHash
					});
				}
			}
			flag4 = true;
		}
		int num = UserCount;
		if (info.HeadlessHost)
		{
			num--;
		}
		if (info.JoinedUsers != num || info.ActiveUsers != ActiveUserCount || info.MaximumUsers != MaxUsers)
		{
			info.JoinedUsers = num;
			info.ActiveUsers = ActiveUserCount;
			info.MaximumUsers = MaxUsers;
			flag4 = true;
		}
		info.ThumbnailUrl = UpdateField(info.ThumbnailUrl, text, ref flag4);
		FrooxEngine.Store.Record correspondingRecord = CorrespondingRecord;
		RecordId recordId = RecordId.TryParse(CorrespondingWorldId);
		RecordId recordId2 = correspondingRecord?.CombinedRecordId;
		RecordId recordId3 = null;
		RecordId recordId4;
		if (recordId != recordId2)
		{
			recordId4 = recordId;
		}
		else if (correspondingRecord != null && correspondingRecord.IsPublic)
		{
			recordId4 = recordId2;
		}
		else
		{
			recordId4 = null;
			recordId3 = recordId2;
		}
		if (info.CorrespondingWorldId != recordId4)
		{
			info.CorrespondingWorldId = recordId4;
			flag4 = true;
		}
		if (info.PrivateCorrespondingWorldId != recordId3)
		{
			info.PrivateCorrespondingWorldId = recordId3;
			flag4 = true;
		}
		if (info.Tags == null)
		{
			info.Tags = new HashSet<string>();
		}
		HashSet<string> hashSet = Pool.BorrowHashSet<string>();
		foreach (string tag in info.Tags)
		{
			hashSet.Add(tag);
		}
		foreach (string tag2 in Tags)
		{
			if (tag2 != null && tag2.Length <= 128 && !hashSet.Remove(tag2))
			{
				info.Tags.Add(tag2);
				flag4 = true;
			}
		}
		if (hashSet.Count > 0)
		{
			flag4 = true;
			foreach (string item in hashSet)
			{
				info.Tags.Remove(item);
			}
			hashSet.Clear();
		}
		if (Session?.Connections.Listener != null)
		{
			foreach (IListener listener in Session.Connections.Listener.Listeners)
			{
				foreach (Uri localUri in listener.LocalUris)
				{
					if (!(localUri == null))
					{
						ushort num2 = (ushort)localUri.Port;
						if (info.LAN_Port != num2)
						{
							info.LAN_Port = num2;
							flag4 = true;
						}
						break;
					}
				}
			}
		}
		if (info.SessionURLs == null)
		{
			info.SessionURLs = new List<string>();
		}
		foreach (string sessionURL in info.SessionURLs)
		{
			hashSet.Add(sessionURL);
		}
		foreach (Uri sessionURL2 in SessionURLs)
		{
			if (!(sessionURL2 == null) && !hashSet.Remove(sessionURL2.OriginalString))
			{
				info.SessionURLs.Add(sessionURL2.OriginalString);
				flag4 = true;
			}
		}
		if (hashSet.Count > 0)
		{
			flag4 = true;
			foreach (string item2 in hashSet)
			{
				info.SessionURLs.Remove(item2);
			}
			hashSet.Clear();
		}
		if (info.ParentSessionIds == null)
		{
			info.ParentSessionIds = new List<string>();
		}
		foreach (string parentSessionId in info.ParentSessionIds)
		{
			hashSet.Add(parentSessionId);
		}
		foreach (string parentSessionId2 in Configuration.ParentSessionIds)
		{
			if (!string.IsNullOrEmpty(parentSessionId2) && SessionInfo.IsValidSessionId(parentSessionId2) && !hashSet.Remove(parentSessionId2))
			{
				info.ParentSessionIds.Add(parentSessionId2);
				flag4 = true;
			}
		}
		if (hashSet.Count > 0)
		{
			flag4 = true;
			foreach (string item3 in hashSet)
			{
				info.ParentSessionIds.Remove(item3);
			}
			hashSet.Clear();
		}
		if (info.SessionUsers == null)
		{
			info.SessionUsers = new List<SessionUser>();
		}
		List<User> list = Pool.BorrowList<User>();
		GetUsers(list);
		list.Sort((User a, User b) => a.AllocationID.CompareTo(b.AllocationID));
		int num3 = 0;
		foreach (User item4 in list)
		{
			if (item4.IsListed)
			{
				if (info.SessionUsers.Count == num3)
				{
					info.SessionUsers.Add(new SessionUser());
				}
				SessionUser sessionUser = info.SessionUsers[num3++];
				if (sessionUser.IsPresent != item4.IsPresent || sessionUser.Username != item4.UserName || sessionUser.UserID != item4.UserID || sessionUser.OutputDevice != item4.OutputDevice)
				{
					sessionUser.IsPresent = item4.IsPresent;
					sessionUser.Username = item4.UserName;
					sessionUser.UserID = item4.UserID;
					sessionUser.OutputDevice = item4.OutputDevice;
					flag4 = true;
				}
			}
		}
		Pool.Return(ref list);
		flag4 |= info.SessionUsers.Count > num3;
		while (info.SessionUsers.Count > num3)
		{
			info.SessionUsers.RemoveAt(info.SessionUsers.Count - 1);
		}
		if (IsAuthority)
		{
			List<SessionInfo> list2 = Pool.BorrowList<SessionInfo>();
			Engine.Cloud.Sessions.GetNestedSessions(info.SessionId, list2);
			list2.Sort((SessionInfo a, SessionInfo b) => string.Compare(a.SessionId, b.SessionId));
			int num4 = info.JoinedUsers;
			int num5 = info.ActiveUsers;
			if (info.NestedSessionIds == null)
			{
				info.NestedSessionIds = new List<string>();
			}
			foreach (string nestedSessionId in info.NestedSessionIds)
			{
				hashSet.Add(nestedSessionId);
			}
			foreach (SessionInfo item5 in list2)
			{
				if (!(item5.HostUserId != info.HostUserId) || Engine.Cloud.Contacts.IsContact(item5.HostUserId))
				{
					num4 += item5.TotalJoinedUsers;
					num5 += item5.TotalActiveUsers;
					if (!hashSet.Remove(item5.SessionId))
					{
						flag4 = true;
						info.NestedSessionIds.Add(item5.SessionId);
					}
				}
			}
			if (num4 != info.TotalJoinedUsers || num5 != info.TotalActiveUsers)
			{
				info.TotalJoinedUsers = num4;
				info.TotalActiveUsers = num5;
			}
			if (hashSet.Count > 0)
			{
				flag4 = true;
				foreach (string item6 in hashSet)
				{
					info.NestedSessionIds.Remove(item6);
				}
			}
			Pool.Return(ref list2);
		}
		else
		{
			info.TotalActiveUsers = info.ActiveUsers;
			info.TotalJoinedUsers = info.JoinedUsers;
		}
		Pool.Return(ref hashSet);
		if (flag4)
		{
			info.LastUpdate = DateTime.UtcNow;
			info.LastWorldConfigurationUpdate = Configuration.LastConfigurationUpdate;
			info.LastWorldUserUpdate = LastUserUpdate;
			info.LastInviteListUpdate = LastInviteListUpdate;
		}
		return flag4;
	}

	public string EmergencyDump()
	{
		try
		{
			SavedGraph savedGraph = SaveWorld();
			string tempFilePath = Engine.LocalDB.GetTempFilePath(".lz4bson");
			DataTreeConverter.Save(savedGraph.Root, tempFilePath, DataTreeConverter.Compression.LZ4);
			return tempFilePath;
		}
		catch (Exception value)
		{
			return $"EXCEPTION: {value}";
		}
	}

	public bool StartResoniteLink(int? port = null)
	{
		if (ResoniteLink != null)
		{
			throw new InvalidOperationException("ResoniteLink is already started");
		}
		if (!IsAuthority)
		{
			throw new InvalidOperationException("ResoniteLink is only supported on the host");
		}
		if (!this.IsAllowedToRunResoniteLink())
		{
			throw new InvalidOperationException("ResoniteLink is not allowed by the permissions in this world");
		}
		ResoniteLink = new ResoniteLinkHost(this);
		bool num = ResoniteLink.Start(port);
		if (!num)
		{
			ResoniteLink = null;
		}
		return num;
	}

	public override string ToString()
	{
		return $"World {RawName} (Handle: {LocalWorldHandle}. Focus: {Focus}, State: {State}, InitState: {InitState}, FailState: {FailState}, FailReason: {FailReasonDescription}, IsDestroyed: {IsDestroyed}, IsAuthority: {IsAuthority}, SyncTick: {SyncTick}, StateVersion: {StateVersion}, Time: {Time?.WorldTime}, TimeSinceLastUpdate: {Time?.TimeSinceLastUpdate}";
	}
}
