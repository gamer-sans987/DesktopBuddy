using System;
using System.Collections.Generic;
using System.Linq;
using Elements.Assets;
using Elements.Core;
using Renderite.Shared;
using SkyFrost.Base;

namespace FrooxEngine;

public class User : ContainerWorker<UserComponent>, IUpdatable, IChangeable, IWorldElement
{
	public enum KickRequestState
	{
		None,
		Kick,
		KickAndRevokeInvite
	}

	public class UserModificationDeniedException : Exception
	{
		public UserModificationDeniedException(string property)
			: base("Only the owning user can modify their " + property)
		{
		}
	}

	public class AuthorityModificationDeniedException : Exception
	{
		public AuthorityModificationDeniedException(string property)
			: base("Only the world host can modify this user's " + property)
		{
		}
	}

	public class AuthorityOrUserModificationDeniedException : Exception
	{
		public AuthorityOrUserModificationDeniedException(string property)
			: base("Only the world host or owning user can modify this user's " + property)
		{
		}
	}

	protected readonly Sync<string> userName;

	protected readonly Sync<string> userId;

	protected readonly Sync<string> userSessionId;

	protected readonly Sync<string> machineId;

	protected readonly Sync<ulong> minAllocationCounterID;

	protected readonly Sync<byte> allocationID;

	protected readonly Sync<DateTime> sessionJoinTimestamp;

	protected readonly Sync<TimeSpan> utcOffset;

	protected readonly Sync<bool> isPatron;

	private bool _usernameInitialized;

	private bool _userIdInitialized;

	private bool _userSessionIdInitialized;

	private bool _machineIdInitialized;

	private bool _minAllocationCounterIDInitialized;

	private bool _allocationIDInitialized;

	private bool _receivingStreams;

	internal bool InitializingEnabled;

	public readonly Sync<bool> isMuted;

	public readonly Sync<VoiceMode> voiceMode;

	public readonly Sync<bool> recordingVoiceMessage;

	public readonly Sync<bool> isLive;

	public readonly Sync<bool> editMode;

	public readonly Sync<bool> kioskMode;

	protected readonly Sync<ulong> uploadedBytes;

	protected readonly Sync<float> downloadSpeed;

	protected readonly Sync<float> uploadSpeed;

	protected readonly Sync<float> downloadMax;

	protected readonly Sync<float> uploadMax;

	protected readonly Sync<int> queuedMessages;

	protected readonly SyncDictionary<string, SyncVar> networkStats;

	private DateTime lastBytesUpdate;

	public readonly ReadOnlyRef<PermissionSet> role;

	private PermissionSet _lastValidRole;

	protected readonly Sync<bool> presentInWorld;

	protected readonly Sync<bool> presentInHeadset;

	protected readonly Sync<bool> isLagging;

	protected readonly Sync<bool> appDashOpened;

	protected readonly Sync<bool> appFacetsOpened;

	protected readonly Sync<bool> platformDashOpened;

	protected readonly Sync<bool> vrActive;

	protected readonly Sync<Chirality> primaryHand;

	protected readonly Sync<bool> list;

	protected readonly Sync<float> fps;

	protected readonly Sync<int> ping;

	protected readonly Sync<ulong> downloadedBytes;

	protected readonly Sync<float> packetLoss;

	protected readonly Sync<int> generatedDeltaMessages;

	protected readonly Sync<int> generatedStreamMessages;

	protected readonly Sync<int> generatedControlMessages;

	protected readonly Sync<bool> defaultMute;

	protected readonly Sync<bool> defaultSpectator;

	protected readonly Sync<bool> mediaMetadataOptOut;

	protected readonly Sync<bool> hideInScreenshots;

	public readonly Sync<int> lnlWindowSize;

	protected readonly Sync<HeadOutputDevice> headDevice;

	protected readonly Sync<Platform> platform;

	protected readonly Sync<string> engineVersionNumber;

	protected readonly Sync<string> rendererName;

	protected readonly Sync<string> runtimeVersion;

	protected readonly LinkRef<UserRoot> userRoot;

	public readonly SyncList<SyncVar> Devices;

	public readonly SyncFieldList<BodyNode> BodyNodes;

	public readonly Sync<bool> EyeTracking;

	public readonly Sync<bool> PupilTracking;

	public readonly SyncFieldList<MouthParameterGroup> MouthTrackingParameters;

	public readonly SyncFieldDictionary<string, string> ExtraUserIds;

	public readonly SyncDictionary<string, SyncVar> Metadata;

	public readonly Sync<bool> silence;

	public readonly Sync<bool> disconnectRequest;

	public readonly Sync<bool> banRequest;

	public readonly Sync<bool> respawnRequest;

	public readonly Sync<string> accessKey;

	public readonly Sync<KickRequestState> kickRequest;

	[HideInInspector]
	protected readonly StreamBag streamBag;

	protected readonly SyncList<UserRef> blockUsers;

	protected readonly Sync<uint> streamConfiguration;

	protected readonly Sync<Uri> lastThumbnailUrl;

	protected readonly SyncTime lastThumbnailUpdate;

	private HashSet<IStream> justAddedStreams = new HashSet<IStream>();

	internal int MissingRootFrames;

	internal int ImmediateDeltaCount;

	internal int ImmediateStreamCount;

	internal int ImmediateControlCount;

	internal DateTime LastSyncMessage;

	private float _laggingEffectCounter;

	public VoiceMode? LocalVoiceModeOverride;

	private List<Slot> _proxyAvatar;

	internal string LastAvatarAccessKey;

	private bool _defaultMuted;

	internal bool ReceiveStreams => _receivingStreams;

	public bool MouthTracking => MouthTrackingParameters.Count > 0;

	public StreamGroupManager StreamGroupManager { get; private set; }

	public double AwaySince { get; private set; }

	public double AwayDuration
	{
		get
		{
			if (!IsPresentInWorld)
			{
				return base.Time.WorldTime - AwaySince;
			}
			return 0.0;
		}
	}

	public bool IsLocalUser => this == base.World?.LocalUser;

	public float DistanceToLocalUserHead { get; internal set; }

	public float LocalVolume
	{
		get
		{
			if (IsSilenced)
			{
				return 0f;
			}
			if (UserID != null && base.Engine.AudioSystem.TryGetCategoryVolume(UserID, out var volume))
			{
				return volume;
			}
			if (base.Engine.AudioSystem.TryGetCategoryVolume(MachineID, out volume))
			{
				return volume;
			}
			return 1f;
		}
		set
		{
			if (UserID != null)
			{
				base.Engine.AudioSystem.SetCategoryVolume(UserID, value);
			}
			base.Engine.AudioSystem.SetCategoryVolume(MachineID, value);
		}
	}

	public string DefaultRole { get; set; }

	public bool DefaultMute
	{
		get
		{
			return defaultMute.Value;
		}
		set
		{
			if (base.World.IsAuthority)
			{
				defaultMute.Value = value;
				return;
			}
			throw new AuthorityModificationDeniedException("DefaultMute");
		}
	}

	public bool DefaultSpectator
	{
		get
		{
			return defaultSpectator;
		}
		set
		{
			if (base.World.IsAuthority)
			{
				defaultSpectator.Value = value;
				return;
			}
			throw new AuthorityModificationDeniedException("DefaultSpectator");
		}
	}

	public Chirality Primaryhand => primaryHand.Value;

	public bool MediaMetadataOptOut => mediaMetadataOptOut.Value;

	public bool HideInScreenshots => hideInScreenshots.Value;

	public int LNL_WindowSize => lnlWindowSize;

	public bool IsLive => isLive.Value;

	public VoiceMode ActiveVoiceMode
	{
		get
		{
			if (LocalVoiceModeOverride == VoiceMode.Mute || (bool)isMuted)
			{
				return VoiceMode.Mute;
			}
			VoiceMode voiceMode = VoiceMode;
			if ((uint)voiceMode <= 1u)
			{
				return VoiceMode;
			}
			return LocalVoiceModeOverride ?? VoiceMode;
		}
	}

	public VoiceMode VoiceMode
	{
		get
		{
			return voiceMode.Value;
		}
		set
		{
			if (base.World.IsAuthority || IsLocalUser)
			{
				voiceMode.Value = value;
				return;
			}
			throw new AuthorityOrUserModificationDeniedException("voice mode");
		}
	}

	public bool RecordingVoiceMessage
	{
		get
		{
			return recordingVoiceMessage.Value;
		}
		set
		{
			if (base.World.IsAuthority || IsLocalUser)
			{
				recordingVoiceMessage.Value = value;
				return;
			}
			throw new AuthorityOrUserModificationDeniedException("RecordingVoiceMessage");
		}
	}

	public VoiceMode MaxAllowedVoiceMode { get; private set; }

	public string UserName
	{
		get
		{
			return userName.Value;
		}
		set
		{
			if (base.World.IsAuthority || base.World.LocalUser == this)
			{
				userName.Value = value;
				return;
			}
			throw new AuthorityOrUserModificationDeniedException("Username");
		}
	}

	/// <summary>
	/// A TimeSpan value which represents their Local TimeSpan offset from the UTC Timezone. This can be used to figure out the timezone and local time of this user.
	/// </summary>
	public TimeSpan UTCOffset
	{
		get
		{
			return utcOffset.Value;
		}
		set
		{
			if (base.World.LocalUser == this)
			{
				utcOffset.Value = value;
				return;
			}
			throw new UserModificationDeniedException("UTCOffset");
		}
	}

	public string SanitizedUsername => StringParsingHelper.SanitizeFormatTags(UserName);

	public IField<string> UserNameField => userName;

	public string UserID
	{
		get
		{
			return userId.Value;
		}
		set
		{
			if (base.World.IsAuthority || base.World.LocalUser == this)
			{
				userId.Value = value;
				return;
			}
			throw new AuthorityOrUserModificationDeniedException("UserID");
		}
	}

	public string UserSessionId
	{
		get
		{
			return userSessionId.Value;
		}
		set
		{
			if (base.World.IsAuthority || base.World.LocalUser == this)
			{
				userSessionId.Value = value;
				return;
			}
			throw new AuthorityOrUserModificationDeniedException("UserSessionId");
		}
	}

	public HeadOutputDevice HeadDevice
	{
		get
		{
			return headDevice.Value;
		}
		set
		{
			if (base.World.IsAuthority)
			{
				headDevice.Value = value;
				return;
			}
			throw new AuthorityModificationDeniedException("HeadDevice");
		}
	}

	public Platform Platform
	{
		get
		{
			return platform.Value;
		}
		set
		{
			if (base.World.IsAuthority)
			{
				platform.Value = value;
				return;
			}
			throw new AuthorityModificationDeniedException("Platform");
		}
	}

	public string EngineVersionNumber
	{
		get
		{
			return engineVersionNumber.Value;
		}
		set
		{
			if (base.World.IsAuthority)
			{
				engineVersionNumber.Value = value;
				return;
			}
			throw new AuthorityModificationDeniedException("EngineVersionNumber");
		}
	}

	public string RendererName
	{
		get
		{
			return rendererName.Value;
		}
		set
		{
			if (base.World.IsAuthority)
			{
				rendererName.Value = value;
				return;
			}
			throw new AuthorityModificationDeniedException("RendererName");
		}
	}

	public string RuntimeVersion
	{
		get
		{
			return runtimeVersion.Value;
		}
		set
		{
			if (base.World.IsAuthority)
			{
				runtimeVersion.Value = value;
				return;
			}
			throw new AuthorityModificationDeniedException("RuntimeVersion");
		}
	}

	public bool IsPresent
	{
		get
		{
			if (IsPresentInHeadset || !VR_Active)
			{
				return IsPresentInWorld;
			}
			return false;
		}
	}

	public bool IsPresentInWorld
	{
		get
		{
			return presentInWorld;
		}
		set
		{
			if (IsLocalUser)
			{
				presentInWorld.Value = value;
				return;
			}
			throw new UserModificationDeniedException("present state");
		}
	}

	public bool IsPresentInHeadset
	{
		get
		{
			return presentInHeadset;
		}
		set
		{
			if (IsLocalUser)
			{
				presentInHeadset.Value = value;
				return;
			}
			throw new UserModificationDeniedException("present in headset state");
		}
	}

	public bool IsAppDashOpened
	{
		get
		{
			return appDashOpened;
		}
		set
		{
			if (IsLocalUser)
			{
				appDashOpened.Value = value;
				return;
			}
			throw new UserModificationDeniedException("product dash opened state");
		}
	}

	public bool IsPlatformDashOpened
	{
		get
		{
			return platformDashOpened;
		}
		set
		{
			if (IsLocalUser)
			{
				platformDashOpened.Value = value;
				return;
			}
			throw new UserModificationDeniedException("platform dash opened state");
		}
	}

	public bool AreAppFacetsOpened
	{
		get
		{
			return appFacetsOpened;
		}
		set
		{
			if (IsLocalUser)
			{
				appFacetsOpened.Value = value;
				return;
			}
			throw new UserModificationDeniedException("AreAppFacetsOpened");
		}
	}

	public bool VR_Active
	{
		get
		{
			return vrActive;
		}
		set
		{
			if (IsLocalUser)
			{
				vrActive.Value = value;
				return;
			}
			throw new UserModificationDeniedException("VR_Active state");
		}
	}

	public bool IsListed
	{
		get
		{
			return list;
		}
		set
		{
			if (IsLocalUser)
			{
				list.Value = value;
				return;
			}
			throw new UserModificationDeniedException("listed state");
		}
	}

	public bool EditMode
	{
		get
		{
			return editMode;
		}
		set
		{
			if (IsLocalUser || base.World.IsAuthority)
			{
				editMode.Value = value;
				return;
			}
			throw new AuthorityOrUserModificationDeniedException("EditMode");
		}
	}

	public bool KioskMode
	{
		get
		{
			return kioskMode;
		}
		set
		{
			if (IsLocalUser || base.World.IsAuthority)
			{
				kioskMode.Value = value;
				return;
			}
			throw new AuthorityOrUserModificationDeniedException("KioskMode");
		}
	}

	public OutputDevice? OutputDevice
	{
		get
		{
			if (HeadDevice == HeadOutputDevice.Headless)
			{
				return null;
			}
			if (HeadDevice.IsCameraMode())
			{
				return SkyFrost.Base.OutputDevice.Camera;
			}
			if (!VR_Active)
			{
				return SkyFrost.Base.OutputDevice.Screen;
			}
			return SkyFrost.Base.OutputDevice.VR;
		}
	}

	public PermissionSet Role
	{
		get
		{
			return role.Target;
		}
		set
		{
			if (!base.World.IsAuthority)
			{
				throw new AuthorityModificationDeniedException("user role!");
			}
			role.ForceWrite(value);
		}
	}

	public bool IsSilenced
	{
		get
		{
			return silence.Value;
		}
		set
		{
			silence.Value = value;
		}
	}

	public bool IsRenderingLocallyBlocked { get; private set; }

	public bool IsAudioLocallyBlocked { get; private set; }

	public bool IsCollisionLocallyBlocked { get; private set; }

	public bool HasProxyAvatar => _proxyAvatar != null;

	public string AvatarAccessKey
	{
		get
		{
			return base.World.DeobfuscateString(accessKey.Value);
		}
		set
		{
			accessKey.Value = base.World.ObfuscateString(value);
		}
	}

	public bool HasNewAvatarAccessKey => LastAvatarAccessKey != AvatarAccessKey;

	public bool IsLagging
	{
		get
		{
			return isLagging.Value;
		}
		set
		{
			if (base.World.IsAuthority)
			{
				isLagging.Value = value;
				return;
			}
			throw new UserModificationDeniedException("isLagging");
		}
	}

	public float FPS
	{
		get
		{
			return fps.Value;
		}
		set
		{
			if (IsLocalUser)
			{
				fps.Value = value;
				return;
			}
			throw new UserModificationDeniedException("fps");
		}
	}

	public int Ping
	{
		get
		{
			return ping.Value;
		}
		set
		{
			if (base.World.IsAuthority)
			{
				ping.Value = value;
				return;
			}
			throw new UserModificationDeniedException("ping");
		}
	}

	public ulong DownloadedBytes
	{
		get
		{
			return downloadedBytes.Value;
		}
		set
		{
			if (IsLocalUser)
			{
				if (lastBytesUpdate != default(DateTime))
				{
					double totalSeconds = (DateTime.UtcNow - lastBytesUpdate).TotalSeconds;
					if (totalSeconds > 0.009999999776482582)
					{
						float num = (float)((double)(value - downloadedBytes.Value) / totalSeconds);
						downloadSpeed.Value = num;
						if (num > DownloadMax)
						{
							downloadMax.Value = num;
						}
					}
				}
				lastBytesUpdate = DateTime.UtcNow;
				downloadedBytes.Value = value;
				return;
			}
			throw new UserModificationDeniedException("downloadedBytes");
		}
	}

	public ulong UploadedBytes
	{
		get
		{
			return uploadedBytes.Value;
		}
		set
		{
			if (base.World.IsAuthority)
			{
				if (lastBytesUpdate != default(DateTime))
				{
					double totalSeconds = (DateTime.UtcNow - lastBytesUpdate).TotalSeconds;
					if (totalSeconds > 0.009999999776482582)
					{
						float num = (float)((double)(value - uploadedBytes.Value) / totalSeconds);
						uploadSpeed.Value = num;
						if (num > UploadMax)
						{
							uploadMax.Value = num;
						}
					}
				}
				lastBytesUpdate = DateTime.UtcNow;
				uploadedBytes.Value = value;
				return;
			}
			throw new UserModificationDeniedException("uploadedBytes");
		}
	}

	public int QueuedMessages
	{
		get
		{
			return queuedMessages.Value;
		}
		set
		{
			if (base.World.IsAuthority)
			{
				queuedMessages.Value = value;
				return;
			}
			throw new AuthorityModificationDeniedException("queuedMessages");
		}
	}

	public float PacketLoss
	{
		get
		{
			return packetLoss.Value;
		}
		set
		{
			if (base.World.IsAuthority)
			{
				packetLoss.Value = value;
				return;
			}
			throw new AuthorityModificationDeniedException("packetLoss");
		}
	}

	public int GeneratedDeltaMessages
	{
		get
		{
			return generatedDeltaMessages.Value;
		}
		set
		{
			if (base.World.IsAuthority)
			{
				generatedDeltaMessages.Value = value;
				return;
			}
			throw new AuthorityModificationDeniedException("generatedDeltaMessages");
		}
	}

	public int GeneratedStreamMessages
	{
		get
		{
			return generatedStreamMessages.Value;
		}
		set
		{
			if (base.World.IsAuthority)
			{
				generatedStreamMessages.Value = value;
				return;
			}
			throw new AuthorityModificationDeniedException("generatedStreamMessages");
		}
	}

	public int GeneratedControlMessages
	{
		get
		{
			return generatedControlMessages.Value;
		}
		set
		{
			if (base.World.IsAuthority)
			{
				generatedControlMessages.Value = value;
				return;
			}
			throw new AuthorityModificationDeniedException("generatedControlMessages");
		}
	}

	public float DownloadSpeed => downloadSpeed.Value;

	public float DownloadMax => downloadMax.Value;

	public float UploadSpeed => uploadSpeed.Value;

	public float UploadMax => uploadMax.Value;

	public IEnumerable<KeyValuePair<string, SyncVar>> NetworkStats => networkStats;

	public string MachineID
	{
		get
		{
			return machineId.Value;
		}
		set
		{
			if (base.World.IsAuthority)
			{
				machineId.Value = value;
				return;
			}
			throw new AuthorityModificationDeniedException("MachineID");
		}
	}

	public ulong AllocationIDStart
	{
		get
		{
			return minAllocationCounterID.Value;
		}
		set
		{
			if (base.World.IsAuthority)
			{
				minAllocationCounterID.ForceSet(value);
				return;
			}
			throw new AuthorityModificationDeniedException("AllocationIDStart");
		}
	}

	public byte AllocationID
	{
		get
		{
			return allocationID.Value;
		}
		set
		{
			if (base.World.IsAuthority)
			{
				allocationID.ForceSet(value);
				if (value == 0)
				{
					role.MarkHostOnly();
				}
				return;
			}
			throw new AuthorityModificationDeniedException("AllocationID");
		}
	}

	public bool IsHost => AllocationID == 0;

	public bool IsPatron
	{
		get
		{
			return isPatron;
		}
		set
		{
			if (!base.World.IsAuthority)
			{
				throw new AuthorityModificationDeniedException("IsPatron");
			}
			isPatron.Value = true;
		}
	}

	public DateTime SessionJoinTimestamp
	{
		get
		{
			return sessionJoinTimestamp;
		}
		set
		{
			if (!base.World.IsAuthority)
			{
				throw new AuthorityModificationDeniedException("SessionJoinTimestamp");
			}
			sessionJoinTimestamp.Value = value;
		}
	}

	public Uri ThumbnailUrl => lastThumbnailUrl;

	public double ThumbnailAge => lastThumbnailUpdate.CurrentTime;

	public IEnumerable<string> UniqueIds
	{
		get
		{
			yield return MachineID;
			if (!string.IsNullOrWhiteSpace(UserID))
			{
				yield return UserID;
			}
			foreach (KeyValuePair<string, Sync<string>> extraUserId in ExtraUserIds)
			{
				yield return base.World.DeobfuscateString(extraUserId.Key) + "-" + base.World.DeobfuscateString(extraUserId.Value);
			}
		}
	}

	public UserRoot Root
	{
		get
		{
			return userRoot.Target;
		}
		set
		{
			userRoot.Target = value;
		}
	}

	public bool DisconnectRequested => disconnectRequest.Value;

	public uint StreamConfigurationVersion => streamConfiguration;

	public override bool IsPersistent => false;

	public int StreamCount => streamBag.Count;

	public Dictionary<RefID, Stream>.ValueCollection Streams => streamBag.Values;

	public bool IsStarted => true;

	public bool IsChangeDirty { get; private set; }

	public int LastChangeUpdateIndex { get; private set; }

	public int UpdateOrder => 0;

	private Slot UserComponentsSlot
	{
		get
		{
			Slot slot = base.World.RootSlot.FindChild("UserComponents");
			if (slot == null)
			{
				slot = base.World.RootSlot.AddSlot("UserComponents");
				slot.PersistentSelf = false;
			}
			return slot;
		}
	}

	public event Action<User> StreamConfigurationChanged;

	public event Action<Stream> StreamAdded;

	public event Action<Stream> StreamRemoved;

	public event Action<IChangeable> Changed;

	internal void StartTransmittingStreamData()
	{
		_receivingStreams = true;
	}

	public bool SupportsMouthTrackingParameter(MouthParameterGroup parameter)
	{
		return MouthTrackingParameters.Contains(parameter);
	}

	public SyncVar GetNetworkStatistic(string name)
	{
		return networkStats.GetElement(name);
	}

	public T GetNetworkStatistic<T>(string name)
	{
		return GetNetworkStatistic(name).GetValue<T>(setIfDifferent: false);
	}

	public bool TryGetNetworkStatistic<T>(string name, out T value)
	{
		if (!networkStats.TryGetElement(name, out SyncVar element))
		{
			value = default(T);
			return false;
		}
		return element.TryGetValue<T>(out value);
	}

	public bool HasNetworkStatistic(string name)
	{
		return networkStats.ContainsKey(name);
	}

	public void SetNetworkStatistic<T>(string name, T value)
	{
		if (base.LocalUser != this && !base.World.IsAuthority)
		{
			throw new AuthorityOrUserModificationDeniedException("network statistics");
		}
		if (!networkStats.TryGetElement(name, out SyncVar element))
		{
			element = networkStats.Add(name);
		}
		element.SetValue(value);
	}

	internal void CommitMessageStats()
	{
		GeneratedDeltaMessages = ImmediateDeltaCount;
		GeneratedStreamMessages = ImmediateStreamCount;
		GeneratedControlMessages = ImmediateControlCount;
	}

	internal void UpdateThumbnail(Uri url)
	{
		if (!IsLocalUser)
		{
			throw new UserModificationDeniedException("thumbnail");
		}
		lastThumbnailUrl.Value = url;
		lastThumbnailUpdate.SetNow();
	}

	internal void SetupDeviceInfos(DataTreeList infos)
	{
		if (!base.World.IsAuthority)
		{
			throw new AuthorityModificationDeniedException("device infos");
		}
		foreach (DataTreeNode child in infos.Children)
		{
			Devices.Add().FromRawDataTreeNode(child);
		}
	}

	internal void SetupEyeTracking(bool eyeTracking, bool pupilTracking)
	{
		EyeTracking.Value = eyeTracking;
		PupilTracking.Value = pupilTracking;
	}

	internal void SetupMouthTracking(IEnumerable<MouthParameterGroup> paramters)
	{
		MouthTrackingParameters.AddRange(paramters);
	}

	internal void SetupExtraIds(Dictionary<string, string> ids)
	{
		base.World.ReferenceController.SkipAhead((uint)RandomX.Range(2, 16));
		foreach (KeyValuePair<string, string> id in ids)
		{
			ExtraUserIds.Add(base.World.ObfuscateString(id.Key), base.World.ObfuscateString(id.Value));
			base.World.ReferenceController.SkipAhead((uint)RandomX.Range(2, 16));
		}
	}

	public User()
	{
		StreamGroupManager = new StreamGroupManager(this);
	}

	internal void Initialize(SyncBagBase<RefID, User> userBag)
	{
		base.Initialize(userBag);
		disconnectRequest.OnValueChange += DisconnectChanged;
		if (base.World.IsAuthority)
		{
			kickRequest.OnValueChange += KickRequest_OnValueChange;
			banRequest.OnValueChange += BanRequest_OnValueChange;
			respawnRequest.OnValueChange += RespawnRequest_OnValueChange;
		}
		role.OnTargetChange += Role_OnTargetChange;
		silence.OnValueChange += Silence_OnValueChange;
		presentInWorld.OnValueChange += OnPresentChanged;
		AwaySince = base.Time.WorldTime;
		primaryHand.Value = (Chirality)(-1);
		voiceMode.Value = VoiceMode.Normal;
		streamBag.OnElementAdded += OnStreamAdded;
		streamBag.OnElementRemoved += OnStreamRemoved;
		streamBag.SyncDirtyCleared += StreamBagDirtyCleared;
		disconnectRequest.LocalFilter = delegate(bool value, IField<bool> field)
		{
			if (!base.World.IsAuthority)
			{
				return false;
			}
			return (!field.Value && value) || field.Value;
		};
		if (base.World != Userspace.UserspaceWorld)
		{
			userName.LocalFilter = (string value, IField<string> field) => InitializationFilter(value, field, ref _usernameInitialized);
			userId.LocalFilter = (string value, IField<string> field) => InitializationFilter(value, field, ref _userIdInitialized);
			userSessionId.LocalFilter = (string value, IField<string> field) => InitializationFilter(value, field, ref _userSessionIdInitialized);
		}
		machineId.LocalFilter = (string value, IField<string> field) => InitializationFilter(value, field, ref _machineIdInitialized);
		minAllocationCounterID.LocalFilter = (ulong value, IField<ulong> field) => InitializationFilter(value, field, ref _minAllocationCounterIDInitialized);
		allocationID.LocalFilter = (byte value, IField<byte> field) => InitializationFilter(value, field, ref _allocationIDInitialized);
		userName.MarkHostOnly();
		userId.MarkHostOnly();
		userSessionId.MarkHostOnly();
		machineId.MarkHostOnly();
		minAllocationCounterID.MarkHostOnly();
		allocationID.MarkHostOnly();
		sessionJoinTimestamp.LocalFilter = InitializingFilter;
		sessionJoinTimestamp.MarkHostOnly();
		isPatron.LocalFilter = InitializingFilter;
		isPatron.MarkHostOnly();
		defaultMute.LocalFilter = InitializingFilter;
		defaultMute.MarkHostOnly();
		defaultSpectator.LocalFilter = InitializingFilter;
		defaultSpectator.MarkHostOnly();
		headDevice.LocalFilter = InitializingFilter;
		headDevice.MarkHostOnly();
		platform.LocalFilter = InitializingFilter;
		platform.MarkHostOnly();
		engineVersionNumber.LocalFilter = InitializingFilter;
		engineVersionNumber.MarkHostOnly();
		rendererName.LocalFilter = InitializingFilter;
		rendererName.MarkHostOnly();
		runtimeVersion.LocalFilter = InitializingFilter;
		runtimeVersion.MarkHostOnly();
		EndInitializationStageForMembers();
		if (!base.World.IsAuthority && base.ReferenceID == base.World.Session.Connections.LocalUserID)
		{
			base.World.SetLocalUser(this);
		}
		base.World.UpdateManager.RegisterForStartup(this);
		base.World.UpdateManager.RegisterForUpdates(this);
		ForeachSyncMember(delegate(ConflictingSyncElement e)
		{
			e.MarkDirectAccessOnly();
			e.MarkNonDrivable();
		});
	}

	private void Role_OnTargetChange(SyncRef<PermissionSet> roleReference)
	{
		if (IsLocalUser && _lastValidRole != null)
		{
			NotificationPanel.RoleChangedNotification(_lastValidRole.RoleName.Value, role.Target?.RoleName.Value ?? "<NULL>", base.World);
		}
		if (!base.World.IsAuthority)
		{
			_lastValidRole = role.Target;
			return;
		}
		if (base.World.CanMakeSynchronousChanges)
		{
			_lastValidRole = role.Target;
			return;
		}
		User changingUser = role.LastModifyingUser ?? base.World.HostUser;
		bool flag = false;
		if (!changingUser.IsHost)
		{
			if (changingUser == this)
			{
				flag = true;
			}
			else if (role.Target == null)
			{
				flag = true;
			}
			else if (role.Target > changingUser.Role)
			{
				flag = true;
			}
			else if (_lastValidRole != null && _lastValidRole > changingUser.Role)
			{
				flag = true;
			}
		}
		if (!flag)
		{
			_lastValidRole = role.Target;
			return;
		}
		base.World.RunSynchronously(delegate
		{
			role.ForceWrite(_lastValidRole);
			BanManager.TempBanUser(changingUser.UserID ?? changingUser.MachineID);
			BanManager.TempBanUser(changingUser.MachineID);
			changingUser.Kick();
		});
	}

	private T InitializingFilter<T>(T value, IField<T> field)
	{
		if (InitializingEnabled)
		{
			return value;
		}
		return field.Value;
	}

	private T InitializationFilter<T>(T value, IField<T> field, ref bool _initialized)
	{
		if (!base.World.IsAuthority | _initialized)
		{
			return field.Value;
		}
		_initialized = true;
		return value;
	}

	private void RespawnRequest_OnValueChange(SyncField<bool> syncField)
	{
		if (syncField.Value && Root == null)
		{
			UniLog.Log($"Respawning user: {UserName} ({base.ReferenceID})");
			base.World.RequestSpawn(this);
			base.World.RunSynchronously(delegate
			{
				respawnRequest.Value = false;
			});
		}
	}

	private void Silence_OnValueChange(SyncField<bool> syncField)
	{
		if (base.World.State == FrooxEngine.World.WorldState.Running)
		{
			UniLog.Log($"Silence: {syncField.Value} for {this}. Changing User: {syncField.LastModifyingUser}", stackTrace: true);
		}
		PermissionController _permissions = base.Permissions;
		_permissions.RunAfterValidations(delegate
		{
			if (silence.Value)
			{
				_permissions.AddSilenced(this);
			}
			else
			{
				_permissions.RemoveSilenced(this);
			}
		});
	}

	internal void Destroy()
	{
		PrepareDestruction();
		Dispose();
	}

	public void Disconnect()
	{
		disconnectRequest.Value = true;
	}

	public void Kick(KickRequestState kickType = KickRequestState.KickAndRevokeInvite)
	{
		if (IsHost)
		{
			throw new Exception("Cannot Kick the Host");
		}
		if (!base.LocalUser.CanKick())
		{
			throw new Exception("Don't have permission to kick");
		}
		kickRequest.Value = kickType;
	}

	public void CancelKick()
	{
		if (IsHost)
		{
			throw new Exception("Cannot Kick the Host");
		}
		if (!base.LocalUser.CanKick())
		{
			throw new Exception("Don't have permission to kick");
		}
		kickRequest.Value = KickRequestState.None;
	}

	public void Ban()
	{
		if (IsHost)
		{
			throw new Exception("Cannot Ban the Host");
		}
		if (!base.LocalUser.CanBan())
		{
			throw new Exception("Don't have permission to ban");
		}
		banRequest.Value = true;
	}

	private void DisconnectChanged(SyncField<bool> syncField)
	{
		if (IsLocalUser && syncField.Value && !base.World.IsAuthority)
		{
			if (base.World.DisconnectRequestedHook != null)
			{
				base.World.DisconnectRequestedHook();
			}
			else
			{
				base.World.Destroy();
			}
		}
	}

	private void KickRequest_OnValueChange(SyncField<KickRequestState> syncField)
	{
		UniLog.Log($"KickRequest: {syncField.Value} for {this}. Changing User: {syncField.LastModifyingUser}, ScheduledForValidation: {base.IsScheduledForValidation}", stackTrace: true);
		base.World.RunAfterValidations(delegate
		{
			if (kickRequest.Value == KickRequestState.None)
			{
				return;
			}
			try
			{
				UniLog.Log($"Kicking user {this}. Last Changing User: {syncField.LastModifyingUser}");
				if (UserID != null && kickRequest.Value == KickRequestState.KickAndRevokeInvite)
				{
					base.World.RemoveAllowedUser(UserID);
				}
				base.World.Session.Connections.GetConnection(this)?.Close();
			}
			catch (Exception exception)
			{
				UniLog.Error($"Exception Kicking User {ToString()}\n{DebugManager.PreprocessException(exception)}");
			}
		});
	}

	private void BanRequest_OnValueChange(SyncField<bool> syncField)
	{
		UniLog.Log($"BanRequest: {syncField.Value} for {this}. Changing User: {syncField.LastModifyingUser}, ScheduledForValidation: {base.IsScheduledForValidation}", stackTrace: true);
		base.World.RunAfterValidations(delegate
		{
			if (!banRequest.Value)
			{
				return;
			}
			try
			{
				UniLog.Log($"Banning user {this}. Last Changing User: {syncField.LastModifyingUser}");
				if (UserID != null)
				{
					base.World.RemoveAllowedUser(UserID);
				}
				BanManager.Ban(new UserFingerprint(this));
				base.World.Session.Connections.GetConnection(this)?.Close();
			}
			catch (Exception exception)
			{
				UniLog.Error($"Exception Banning User {ToString()}\n{DebugManager.PreprocessException(exception)}");
			}
		});
	}

	public override DataTreeNode Save(SaveControl control)
	{
		throw new NotSupportedException("Users cannot be saved!");
	}

	public override void Load(DataTreeNode node, LoadControl control)
	{
		throw new NotSupportedException("Users cannot be loaded!");
	}

	public override string ToString()
	{
		return $"User {base.ReferenceID} (Alloc: {AllocationID}) - UserName: {UserName}, UserId: {UserID}, MachineId: {MachineID}, Role: {Role?.RoleName.Value}";
	}

	public IStream GetStream(RefID id)
	{
		return streamBag[id];
	}

	public S AddStream<S>() where S : Stream, new()
	{
		S val = TypeManager.Instantiate<S>();
		InternalAddStream(val);
		return val;
	}

	public S GetStream<S>(Predicate<S> predicate) where S : Stream
	{
		foreach (KeyValuePair<RefID, Stream> item in streamBag)
		{
			if (item.Value is S val && predicate(val))
			{
				return val;
			}
		}
		return null;
	}

	public S GetStreamOrAdd<S>(string name, Action<S> initialize) where S : Stream, new()
	{
		return GetStreamOrAdd((S s) => s.Name == name, initialize);
	}

	public S GetStreamOrAdd<S>(Predicate<S> predicate, Action<S> initialize) where S : Stream, new()
	{
		S stream = GetStream(predicate);
		if (stream != null)
		{
			return stream;
		}
		stream = AddStream<S>();
		initialize(stream);
		return stream;
	}

	public void RemoveStream(Stream stream)
	{
		if (stream.User != this)
		{
			throw new InvalidOperationException($"Stream doesn't belong to given user.\nStream:\n{stream}\nUser:\n{this}");
		}
		InternalRemoveStream(stream.ReferenceID);
	}

	private void InternalAddStream(Stream stream)
	{
		RefID key = base.World.ReferenceController.PeekID();
		streamBag.Add(key, stream, isNew: true);
	}

	private bool InternalRemoveStream(RefID id)
	{
		return streamBag.Remove(id);
	}

	private void OnStreamRemoved(SyncBagBase<RefID, Stream> bag, RefID key, Stream stream)
	{
		stream.Dispose();
		try
		{
			this.StreamRemoved?.Invoke(stream);
		}
		catch (Exception exception)
		{
			base.Debug.Error("Exception running StreamRemoved: " + DebugManager.PreprocessException(exception));
		}
	}

	private void OnStreamAdded(SyncBagBase<RefID, Stream> bag, RefID key, Stream stream, bool isNew)
	{
		base.World.ReferenceController.AllocationBlockBegin(in key);
		stream.Initialize(this);
		base.World.ReferenceController.AllocationBlockEnd();
		if (IsLocalUser)
		{
			base.World.RunSynchronously(delegate
			{
				stream.Group = "Default";
				stream.Active = true;
			});
		}
		else
		{
			justAddedStreams.Add(stream);
		}
		try
		{
			this.StreamAdded?.Invoke(stream);
		}
		catch (Exception exception)
		{
			base.Debug.Error("Exception running StreamAdded: " + DebugManager.PreprocessException(exception));
		}
	}

	internal bool WasStreamJustAdded(IStream stream)
	{
		return justAddedStreams.Contains(stream);
	}

	private void StreamBagDirtyCleared()
	{
		justAddedStreams.Clear();
	}

	public void NotifyStreamConfigurationChanged()
	{
		bool flag = false;
		if (IsLocalUser)
		{
			if (!streamConfiguration.IsSyncDirty)
			{
				streamConfiguration.Value++;
				flag = true;
			}
		}
		else
		{
			flag = true;
		}
		if (flag)
		{
			try
			{
				this.StreamConfigurationChanged?.Invoke(this);
			}
			catch (Exception exception)
			{
				base.Debug.Error($"Exception running StreamConfigurationChanged on {ToString()}\n{DebugManager.PreprocessException(exception)}");
			}
		}
	}

	protected override void SyncMemberChanged(IChangeable member)
	{
		MarkChangeDirty();
	}

	public void InternalRunStartup()
	{
		BanManager.OnAllBlockingChanged += OnAllBlockingChanged;
		BanManager.OnBlockingChanged += OnBlockingChanged;
		UpdateBlocking(isInitial: true);
	}

	private void OnAllBlockingChanged()
	{
		base.World.RunSynchronously(delegate
		{
			UpdateBlocking(isInitial: false);
		});
	}

	private void OnBlockingChanged(UserRestrictionsSettings.Entry entry)
	{
		if (entry.Matches(this))
		{
			base.World.RunSynchronously(delegate
			{
				UpdateBlocking(isInitial: false);
			});
		}
	}

	public void UpdateBlocking(bool isInitial)
	{
		UserFingerprint fingerprint = new UserFingerprint(this);
		bool isAudioLocallyBlocked = IsAudioLocallyBlocked;
		bool isRenderingLocallyBlocked = IsRenderingLocallyBlocked;
		bool isCollisionLocallyBlocked = IsCollisionLocallyBlocked;
		bool flag = false;
		bool flag2 = false;
		bool flag3 = blockUsers.Any((UserRef u) => u.Target == base.LocalUser);
		if (flag3 && base.LocalUser.IsHost)
		{
			Kick();
			return;
		}
		if (BanManager.IsMutuallyBlocked(fingerprint) || flag3)
		{
			IsAudioLocallyBlocked = true;
			IsRenderingLocallyBlocked = true;
			IsCollisionLocallyBlocked = true;
			if (!flag3)
			{
				flag = true;
			}
		}
		else if (BanManager.IsAvatarBlocked(fingerprint))
		{
			IsRenderingLocallyBlocked = true;
			IsAudioLocallyBlocked = false;
			IsCollisionLocallyBlocked = false;
			flag2 = true;
		}
		else
		{
			IsRenderingLocallyBlocked = false;
			IsAudioLocallyBlocked = false;
			IsCollisionLocallyBlocked = false;
		}
		if (!isInitial && (isAudioLocallyBlocked != IsAudioLocallyBlocked || isRenderingLocallyBlocked != IsRenderingLocallyBlocked || isCollisionLocallyBlocked != IsCollisionLocallyBlocked))
		{
			Root?.Slot.ForeachComponentInChildren(delegate(Component c)
			{
				c.MarkChangeDirty();
			});
		}
		if (flag)
		{
			if (!base.LocalUser.blockUsers.Any((UserRef u) => u.Target == this))
			{
				base.LocalUser.blockUsers.Add().Target = this;
			}
		}
		else
		{
			base.LocalUser.blockUsers.RemoveAll((UserRef u) => u.Target == this);
		}
		if (flag2 && (_proxyAvatar == null || _proxyAvatar.Any((Slot s) => s.IsRemoved)))
		{
			if (isInitial)
			{
				base.World.Coroutines.StartTask(async delegate
				{
					while (!IsRemoved && Root?.HeadSlot == null)
					{
						await default(NextUpdate);
					}
					if (!IsRemoved)
					{
						SetupProxyAvatar();
					}
				});
			}
			else
			{
				SetupProxyAvatar();
			}
		}
		if (!flag2 && _proxyAvatar != null)
		{
			CleanupProxyAvatar();
		}
	}

	private void SetupProxyAvatar()
	{
		CleanupProxyAvatar();
		_proxyAvatar = new List<Slot>();
		Slot slot = Root?.HeadSlot;
		if (slot != null)
		{
			Slot slot2 = slot.AddLocalSlot("ProxyAvatar");
			slot2.AttachSphere<PBS_Metallic>(0.1f);
			_proxyAvatar.Add(slot2);
		}
		Slot slot3 = Root?.LeftHandSlot;
		if (slot3 != null)
		{
			Slot slot4 = slot3.AddLocalSlot("ProxyAvatar");
			slot4.AttachSphere<PBS_Metallic>(0.03f);
			_proxyAvatar.Add(slot4);
		}
		Slot slot5 = Root?.RightHandSlot;
		if (slot5 != null)
		{
			Slot slot6 = slot5.AddLocalSlot("ProxyAvatar");
			slot6.AttachSphere<PBS_Metallic>(0.03f);
			_proxyAvatar.Add(slot6);
		}
		foreach (Slot item in _proxyAvatar)
		{
			item.ForeachComponentInChildren((IRenderable r) => r.RenderingLocallyUnblocked = true);
		}
	}

	private void CleanupProxyAvatar()
	{
		if (_proxyAvatar == null)
		{
			return;
		}
		foreach (Slot item in _proxyAvatar)
		{
			if (!item.IsRemoved)
			{
				item.Destroy();
			}
		}
		_proxyAvatar = null;
	}

	private void OnPresentChanged(SyncField<bool> syncField)
	{
		if (!IsPresentInWorld)
		{
			AwaySince = base.Time.WorldTime;
		}
	}

	public void InternalRunUpdate()
	{
		if (base.World.IsAuthority)
		{
			if (!IsHost)
			{
				IsLagging = IsPresentInWorld && (DateTime.UtcNow - LastSyncMessage).TotalSeconds >= 1.5;
				if (IsLagging && FrooxEngine.Engine.IsAprilFools)
				{
					_laggingEffectCounter -= base.Time.Delta;
					if (_laggingEffectCounter <= 0f)
					{
						if (Root != null)
						{
							HighlightHelper.FlashHighlight(Root.Slot, (IHighlightable h) => h is MeshRenderer || h is SkinnedMeshRenderer, colorX.Red.MulRGB(0.1f), 1f, excludeDisabled: true);
							StaticAudioClip sharedComponentOrCreate = base.World.GetSharedComponentOrCreate("LagHurtSound", delegate(StaticAudioClip c)
							{
								c.URL.Value = new Uri("resdb:///5161536e60a37fabbc71e92c3d1cb9d60d43052c7466cb5a91438ea9c32dccb3");
							});
							Slot headSlot = Root.HeadSlot;
							float speed = RandomX.Range(0.9f, 1.1f);
							headSlot.PlayOneShot(sharedComponentOrCreate, 1f, spatialize: true, null, speed);
						}
						_laggingEffectCounter = 1f;
					}
				}
				else
				{
					_laggingEffectCounter = 0f;
				}
			}
			if (DefaultMute && !_defaultMuted && !IsLocalUser)
			{
				IsSilenced = true;
				_defaultMuted = true;
			}
			IConnection connection = null;
			base.World.Session?.Connections.TryGetConnection(this, out connection);
			if (lnlWindowSize.Value > 0 && connection is LNL_Peer lNL_Peer)
			{
				lNL_Peer.Peer.SetDynamicWindowSize(MathX.Clamp(lnlWindowSize, 8, 512));
			}
		}
		else if (IsHost)
		{
			if (UserID != null && base.World.Session.Connections.VerifiedHostUserId != UserID)
			{
				UniLog.Warning("Host UserID " + UserID + " doesn't match verified host UserID: " + base.World.Session.Connections.VerifiedHostUserId);
				base.World.Destroy();
				return;
			}
			int num = Settings.GetActiveSetting<RealtimeNetworkingSettings>()?.LNL_WindowSize.Value ?? 0;
			IConnection connection2 = null;
			base.World.Session?.Connections.TryGetConnection(this, out connection2);
			if (connection2 is LNL_Connection lNL_Connection && num > 0)
			{
				lNL_Connection.Peer.SetDynamicWindowSize(MathX.Clamp(num, 8, 512));
			}
		}
		if (!IsLocalUser)
		{
			return;
		}
		if (IsPresentInWorld)
		{
			presentInHeadset.Value = base.InputInterface.IsUserPresentInHeadset;
			platformDashOpened.Value = base.InputInterface.PlatformDashboardOpened;
			appDashOpened.Value = base.InputInterface.AppDashOpened;
			appFacetsOpened.Value = base.InputInterface.AppFacetsOpened;
		}
		isMuted.Value = base.AudioSystem.IsMuted;
		isLive.Value = InteractiveCameraControl.IsLive;
		vrActive.Value = base.InputInterface.VR_Active;
		primaryHand.Value = base.InputInterface.PrimaryHand;
		MediaPrivacySettings activeSetting = Settings.GetActiveSetting<MediaPrivacySettings>();
		if (activeSetting != null)
		{
			mediaMetadataOptOut.Value = activeSetting.MediaMetadataOptOut.Value;
			hideInScreenshots.Value = activeSetting.HideInScreenshots.Value;
		}
		RealtimeNetworkingSettings activeSetting2 = Settings.GetActiveSetting<RealtimeNetworkingSettings>();
		if (activeSetting2 != null)
		{
			lnlWindowSize.Value = activeSetting2.LNL_WindowSize;
		}
		VoiceMode value = VoiceMode;
		List<VoicePermission> list = Pool.BorrowList<VoicePermission>();
		base.Permissions.GetValidators(this, list);
		VoiceMode voiceMode = VoiceMode.Broadcast;
		foreach (VoicePermission item in list)
		{
			if (value > item.MaxAllowedVoiceMode.Value)
			{
				value = item.MaxAllowedVoiceMode.Value;
			}
			if (voiceMode > (VoiceMode)item.MaxAllowedVoiceMode)
			{
				voiceMode = item.MaxAllowedVoiceMode;
			}
		}
		Pool.Return(ref list);
		VoiceMode = value;
		MaxAllowedVoiceMode = voiceMode;
		RecordingVoiceMessage = ContactsDialog.RecordingVoiceMessage;
		IsListed = base.Cloud.Status.OnlineStatus == OnlineStatus.Online || base.Cloud.Status.OnlineStatus == OnlineStatus.Sociable;
		utcOffset.Value = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);
	}

	public void InternalRunApplyChanges(int changeUpdateIndex)
	{
		IsChangeDirty = false;
		LastChangeUpdateIndex = changeUpdateIndex;
		if (!blockUsers.GetWasChangedAndClear())
		{
			return;
		}
		blockUsers.RemoveAll(delegate(UserRef u)
		{
			if (u.Target == null)
			{
				return true;
			}
			u.ReferenceID.ExtractIDs(out var _, out var user);
			return user != AllocationID;
		});
		UpdateBlocking(isInitial: false);
	}

	public void InternalRunDestruction()
	{
	}

	protected override void OnDispose()
	{
		BanManager.OnBlockingChanged -= OnBlockingChanged;
		base.World.UpdateManager.UnregisterFromUpdates(this);
		base.OnDispose();
	}

	public void MarkChangeDirty()
	{
		World world = base.World;
		if (world != null && !IsChangeDirty && IsStarted)
		{
			IsChangeDirty = true;
			if (!base.IsDestroyed)
			{
				world?.UpdateManager.Changed(this);
			}
			this.Changed?.Invoke(this);
		}
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		userName = new Sync<string>();
		userId = new Sync<string>();
		userSessionId = new Sync<string>();
		machineId = new Sync<string>();
		minAllocationCounterID = new Sync<ulong>();
		allocationID = new Sync<byte>();
		sessionJoinTimestamp = new Sync<DateTime>();
		utcOffset = new Sync<TimeSpan>();
		isPatron = new Sync<bool>();
		isMuted = new Sync<bool>();
		voiceMode = new Sync<VoiceMode>();
		recordingVoiceMessage = new Sync<bool>();
		isLive = new Sync<bool>();
		editMode = new Sync<bool>();
		kioskMode = new Sync<bool>();
		uploadedBytes = new Sync<ulong>();
		downloadSpeed = new Sync<float>();
		uploadSpeed = new Sync<float>();
		downloadMax = new Sync<float>();
		uploadMax = new Sync<float>();
		queuedMessages = new Sync<int>();
		networkStats = new SyncDictionary<string, SyncVar>();
		role = new ReadOnlyRef<PermissionSet>();
		presentInWorld = new Sync<bool>();
		presentInHeadset = new Sync<bool>();
		isLagging = new Sync<bool>();
		appDashOpened = new Sync<bool>();
		appFacetsOpened = new Sync<bool>();
		platformDashOpened = new Sync<bool>();
		vrActive = new Sync<bool>();
		primaryHand = new Sync<Chirality>();
		list = new Sync<bool>();
		fps = new Sync<float>();
		ping = new Sync<int>();
		downloadedBytes = new Sync<ulong>();
		packetLoss = new Sync<float>();
		generatedDeltaMessages = new Sync<int>();
		generatedStreamMessages = new Sync<int>();
		generatedControlMessages = new Sync<int>();
		defaultMute = new Sync<bool>();
		defaultSpectator = new Sync<bool>();
		mediaMetadataOptOut = new Sync<bool>();
		hideInScreenshots = new Sync<bool>();
		lnlWindowSize = new Sync<int>();
		headDevice = new Sync<HeadOutputDevice>();
		platform = new Sync<Platform>();
		engineVersionNumber = new Sync<string>();
		rendererName = new Sync<string>();
		runtimeVersion = new Sync<string>();
		userRoot = new LinkRef<UserRoot>();
		Devices = new SyncList<SyncVar>();
		BodyNodes = new SyncFieldList<BodyNode>();
		EyeTracking = new Sync<bool>();
		PupilTracking = new Sync<bool>();
		MouthTrackingParameters = new SyncFieldList<MouthParameterGroup>();
		ExtraUserIds = new SyncFieldDictionary<string, string>();
		Metadata = new SyncDictionary<string, SyncVar>();
		silence = new Sync<bool>();
		disconnectRequest = new Sync<bool>();
		banRequest = new Sync<bool>();
		respawnRequest = new Sync<bool>();
		accessKey = new Sync<string>();
		kickRequest = new Sync<KickRequestState>();
		streamBag = new StreamBag();
		blockUsers = new SyncList<UserRef>();
		streamConfiguration = new Sync<uint>();
		lastThumbnailUrl = new Sync<Uri>();
		lastThumbnailUpdate = new SyncTime();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => componentBag, 
			1 => userName, 
			2 => userId, 
			3 => userSessionId, 
			4 => machineId, 
			5 => minAllocationCounterID, 
			6 => allocationID, 
			7 => sessionJoinTimestamp, 
			8 => utcOffset, 
			9 => isPatron, 
			10 => isMuted, 
			11 => voiceMode, 
			12 => recordingVoiceMessage, 
			13 => isLive, 
			14 => editMode, 
			15 => kioskMode, 
			16 => uploadedBytes, 
			17 => downloadSpeed, 
			18 => uploadSpeed, 
			19 => downloadMax, 
			20 => uploadMax, 
			21 => queuedMessages, 
			22 => networkStats, 
			23 => role, 
			24 => presentInWorld, 
			25 => presentInHeadset, 
			26 => isLagging, 
			27 => appDashOpened, 
			28 => appFacetsOpened, 
			29 => platformDashOpened, 
			30 => vrActive, 
			31 => primaryHand, 
			32 => list, 
			33 => fps, 
			34 => ping, 
			35 => downloadedBytes, 
			36 => packetLoss, 
			37 => generatedDeltaMessages, 
			38 => generatedStreamMessages, 
			39 => generatedControlMessages, 
			40 => defaultMute, 
			41 => defaultSpectator, 
			42 => mediaMetadataOptOut, 
			43 => hideInScreenshots, 
			44 => lnlWindowSize, 
			45 => headDevice, 
			46 => platform, 
			47 => engineVersionNumber, 
			48 => rendererName, 
			49 => runtimeVersion, 
			50 => userRoot, 
			51 => Devices, 
			52 => BodyNodes, 
			53 => EyeTracking, 
			54 => PupilTracking, 
			55 => MouthTrackingParameters, 
			56 => ExtraUserIds, 
			57 => Metadata, 
			58 => silence, 
			59 => disconnectRequest, 
			60 => banRequest, 
			61 => respawnRequest, 
			62 => accessKey, 
			63 => kickRequest, 
			64 => streamBag, 
			65 => blockUsers, 
			66 => streamConfiguration, 
			67 => lastThumbnailUrl, 
			68 => lastThumbnailUpdate, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static User __New()
	{
		return new User();
	}
}
