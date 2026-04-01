using System;
using Elements.Core;
using Renderite.Shared;
using SkyFrost.Base;

namespace FrooxEngine;

public class WorldConfiguration : SyncObject, IUpdatable, IChangeable, IWorldElement
{
	public readonly Sync<string> WorldName;

	public readonly Sync<string> WorldDescription;

	public readonly SyncFieldList<string> WorldTags;

	[NonPersistent]
	public readonly Sync<string> SessionID;

	[NonPersistent]
	public readonly SyncFieldList<Uri> SessionURLs;

	[NonPersistent]
	public readonly Sync<string> CorrespondingWorldId;

	public readonly SyncFieldList<string> ParentSessionIds;

	[NonPersistent]
	public readonly Sync<string> BroadcastKey;

	[NonPersistent]
	public readonly Sync<string> UniverseID;

	public readonly Sync<bool> MobileFriendly;

	public readonly Sync<SessionAccessLevel> AccessLevel;

	public readonly Sync<bool> HideFromListing;

	[NonPersistent]
	public readonly Sync<bool> UseCustomJoinVerifier;

	public readonly SyncRef<IWorldUserJoinVerifier> CustomJoinVerifier;

	public readonly Sync<int> MaxUsers;

	public readonly Sync<bool> AwayKickEnabled;

	public readonly Sync<float> AwayKickMinutes;

	[NonPersistent]
	public readonly Sync<bool> AutoSaveEnabled;

	public readonly Sync<float> AutoSaveInterval;

	public readonly Sync<bool> AutoCleanupEnabled;

	public readonly Sync<float> AutoCleanupInterval;

	private SessionAccessLevel _lastAccessLevel;

	private bool _lastHidden;

	private string _lastWorldId;

	private string _lastSessionId;

	private DateTime _lastAutoSave;

	private DateTime _lastAssetCleanup;

	private WorldManager _cachedWorldManager;

	private DataTreeNode _lastValid;

	private bool _broadcastKeyInvalid = true;

	public string ActualBroadcastKey
	{
		get
		{
			if (AccessLevel.Value != SessionAccessLevel.ContactsPlus)
			{
				return null;
			}
			return BroadcastKey.Value;
		}
	}

	public bool IsStarted => true;

	public bool IsDestroyed => IsRemoved;

	public bool IsChangeDirty { get; private set; }

	public int LastChangeUpdateIndex { get; private set; }

	public int UpdateOrder => 0;

	public DateTime LastConfigurationUpdate { get; private set; }

	protected override void OnAwake()
	{
		base.OnAwake();
		_cachedWorldManager = base.Engine.WorldManager;
		SessionID.MarkHostOnly();
		SessionID.LocalFilter = SessionIdFilter;
		UniverseID.MarkHostOnly();
		MaxUsers.Value = 16;
		AwayKickEnabled.Value = true;
		AwayKickMinutes.Value = 5f;
		AutoSaveInterval.Value = 5f;
		AutoSaveEnabled.Changed += AutoSaveEnabled_Changed;
		AutoCleanupEnabled.Changed += AutoCleanupEnabled_Changed;
		AutoCleanupEnabled.Value = true;
		AutoCleanupInterval.Value = 300f;
		base.World.UpdateManager.RegisterForUpdates(this);
		_lastAssetCleanup = DateTime.UtcNow;
		AccessLevel.LocalFilter = AccessLevelFilter;
		if (base.World.IsAuthority)
		{
			AccessLevel.Changed += AccessLevel_Changed;
			HideFromListing.Changed += HideFromListing_Changed;
		}
		WorldName.LocalFilter = (string name, IField<string> field) => name.ClampLength(256);
		WorldDescription.LocalFilter = (string desc, IField<string> field) => desc.ClampLength(16384);
		CorrespondingWorldId.Changed += CorrespondingWorldId_Changed;
		SessionID.Changed += SessionID_Changed;
		WorldTags.ElementsAdded += MarkNonDrivable;
		ParentSessionIds.ElementsAdded += MarkProtected;
		SessionURLs.ElementsAdded += MarkProtected;
		BroadcastKey.MarkHostOnly();
		ForeachSyncMember(delegate(ConflictingSyncElement e)
		{
			e.MarkDirectAccessOnly();
		});
		ForeachSyncMember(delegate(SyncElement e)
		{
			e.MarkNonDrivable();
		});
		if (base.World.IsAuthority)
		{
			ForeachSyncMember(delegate(IField f)
			{
				f.Changed += FieldChanged;
			});
			base.World.UserLeft += World_UserLeft;
		}
	}

	private void HideFromListing_Changed(IChangeable obj)
	{
		if (!HideFromListing.Value && _lastHidden)
		{
			InvalidateThumbnail();
		}
		_lastHidden = HideFromListing.Value;
	}

	private void InvalidateThumbnail()
	{
		Userspace.InvalidateThumbnailData(base.World);
	}

	private void World_UserLeft(User obj)
	{
		if (AccessLevel.Value == SessionAccessLevel.ContactsPlus)
		{
			_broadcastKeyInvalid = true;
		}
	}

	private void AccessLevel_Changed(IChangeable obj)
	{
		if (AccessLevel.Value == SessionAccessLevel.ContactsPlus)
		{
			_broadcastKeyInvalid = true;
		}
		if (AccessLevel.Value > _lastAccessLevel)
		{
			InvalidateThumbnail();
		}
		_lastAccessLevel = AccessLevel.Value;
	}

	private void FieldChanged(IChangeable obj)
	{
		IField field = (IField)obj;
		ConflictingSyncElement conflictingSyncElement = (ConflictingSyncElement)obj;
		if (!base.World.IsUserspace() && base.World.UserCount > 1)
		{
			UniLog.Log($"{field.Name} set to {field.BoxedValue}. LastModifyingUser: {conflictingSyncElement.LastModifyingUser}", stackTrace: true);
		}
	}

	internal void SaveValidValues()
	{
		_lastValid = Save(new SaveControl(base.World, this, new ReferenceTranslator(), null));
	}

	internal void RestoreValidValues()
	{
		if (_lastValid != null)
		{
			Load(_lastValid, new LoadControl(base.World, new ReferenceTranslator(), FrooxEngine.Engine.Version, null));
		}
	}

	private void MarkNonDrivable<T>(SyncElementList<Sync<T>> list, int startIndex, int count)
	{
		for (int i = 0; i < count; i++)
		{
			list.GetElement(startIndex + i).MarkNonDrivable();
		}
	}

	private void MarkProtected<T>(SyncElementList<Sync<T>> list, int startIndex, int count)
	{
		for (int i = 0; i < count; i++)
		{
			Sync<T> element = list.GetElement(startIndex + i);
			element.MarkNonDrivable();
			element.MarkHostOnly();
		}
	}

	private void SessionID_Changed(IChangeable obj)
	{
		string value = SessionID.Value;
		if (!(value == _lastSessionId))
		{
			UnregisterLinkedSessionId();
			_lastSessionId = value;
			if (!string.IsNullOrWhiteSpace(value))
			{
				_cachedWorldManager.SessionIdLinked(base.World);
			}
		}
	}

	private void CorrespondingWorldId_Changed(IChangeable obj)
	{
		string value = CorrespondingWorldId.Value;
		if (!(value == _lastWorldId))
		{
			UnregisterLinkedWorldId();
			_lastWorldId = value;
			if (!string.IsNullOrWhiteSpace(value))
			{
				_cachedWorldManager.WorldIdLinked(base.World);
			}
		}
	}

	private void UnregisterLinkedWorldId()
	{
		if (!string.IsNullOrWhiteSpace(_lastWorldId))
		{
			_cachedWorldManager.WorldIdUnlinked(base.World, _lastWorldId);
			_lastWorldId = null;
		}
	}

	private void UnregisterLinkedSessionId()
	{
		if (!string.IsNullOrWhiteSpace(_lastSessionId))
		{
			_cachedWorldManager.SessionIdUnlinked(base.World, _lastSessionId);
			_lastSessionId = null;
		}
	}

	private string SessionIdFilter(string id, IField<string> field)
	{
		return field.Value ?? id;
	}

	private SessionAccessLevel AccessLevelFilter(SessionAccessLevel value, IField<SessionAccessLevel> field)
	{
		if (base.World.UnsafeMode && value > SessionAccessLevel.LAN)
		{
			value = SessionAccessLevel.LAN;
		}
		return value;
	}

	private void AutoCleanupEnabled_Changed(IChangeable obj)
	{
		if (base.World.IsAuthority && AutoCleanupEnabled.Value)
		{
			_lastAssetCleanup = DateTime.UtcNow;
		}
	}

	private void AutoSaveEnabled_Changed(IChangeable obj)
	{
		if (base.World.IsAuthority && AutoSaveEnabled.Value)
		{
			_lastAutoSave = DateTime.UtcNow;
		}
	}

	public void InternalRunApplyChanges(int changeUpdateIndex)
	{
		if (base.World.IsAuthority && !base.World.IsUserspace())
		{
			base.World.Permissions.RunAfterValidations(SaveValidValues);
		}
		IsChangeDirty = false;
		LastChangeUpdateIndex = changeUpdateIndex;
	}

	public void InternalRunDestruction()
	{
	}

	public void InternalRunStartup()
	{
	}

	public void InternalRunUpdate()
	{
		if (!base.World.IsAuthority)
		{
			return;
		}
		if (_broadcastKeyInvalid && base.Cloud.CurrentUserID != null)
		{
			BroadcastKey.Value = base.Cloud.CurrentUserID + ":" + Guid.CreateVersion7();
			_broadcastKeyInvalid = false;
			UniLog.Log("BroadcastKey changed: " + BroadcastKey.Value);
		}
		if (AutoSaveEnabled.Value && (DateTime.UtcNow - _lastAutoSave).TotalMinutes >= (double)AutoSaveInterval.Value)
		{
			_lastAutoSave = DateTime.UtcNow;
			Userspace.SaveWorldAuto(base.World, SaveType.Overwrite, exitOnSave: false);
		}
		if (AutoCleanupEnabled.Value && (DateTime.UtcNow - _lastAssetCleanup).TotalSeconds >= (double)AutoCleanupInterval.Value)
		{
			_lastAssetCleanup = DateTime.UtcNow;
			if (base.InputInterface.HeadOutputDevice == HeadOutputDevice.Headless)
			{
				MaterialOptimizer.DeduplicateMaterials(base.World);
				WorldOptimizer.DeduplicateStaticProviders(base.World);
			}
			WorldOptimizer.CleanupAssets(base.World, ignoreNonpersistentUsers: false, WorldOptimizer.CleanupMode.Destroy);
		}
	}

	protected override void SyncMemberChanged(IChangeable member)
	{
		base.SyncMemberChanged(member);
		MarkChangeDirty();
	}

	public void MarkChangeDirty()
	{
		if (!IsChangeDirty)
		{
			IsChangeDirty = true;
			LastConfigurationUpdate = DateTime.UtcNow;
			if (!IsDestroyed)
			{
				base.World?.UpdateManager.Changed(this);
			}
		}
	}

	protected override void OnDispose()
	{
		UnregisterLinkedWorldId();
		UnregisterLinkedSessionId();
		_cachedWorldManager = null;
		base.OnDispose();
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		WorldName = new Sync<string>();
		WorldDescription = new Sync<string>();
		WorldTags = new SyncFieldList<string>();
		SessionID = new Sync<string>();
		SessionID.MarkNonPersistent();
		SessionURLs = new SyncFieldList<Uri>();
		SessionURLs.MarkNonPersistent();
		CorrespondingWorldId = new Sync<string>();
		CorrespondingWorldId.MarkNonPersistent();
		ParentSessionIds = new SyncFieldList<string>();
		BroadcastKey = new Sync<string>();
		BroadcastKey.MarkNonPersistent();
		UniverseID = new Sync<string>();
		UniverseID.MarkNonPersistent();
		MobileFriendly = new Sync<bool>();
		AccessLevel = new Sync<SessionAccessLevel>();
		HideFromListing = new Sync<bool>();
		UseCustomJoinVerifier = new Sync<bool>();
		UseCustomJoinVerifier.MarkNonPersistent();
		CustomJoinVerifier = new SyncRef<IWorldUserJoinVerifier>();
		MaxUsers = new Sync<int>();
		AwayKickEnabled = new Sync<bool>();
		AwayKickMinutes = new Sync<float>();
		AutoSaveEnabled = new Sync<bool>();
		AutoSaveEnabled.MarkNonPersistent();
		AutoSaveInterval = new Sync<float>();
		AutoCleanupEnabled = new Sync<bool>();
		AutoCleanupInterval = new Sync<float>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => WorldName, 
			1 => WorldDescription, 
			2 => WorldTags, 
			3 => SessionID, 
			4 => SessionURLs, 
			5 => CorrespondingWorldId, 
			6 => ParentSessionIds, 
			7 => BroadcastKey, 
			8 => UniverseID, 
			9 => MobileFriendly, 
			10 => AccessLevel, 
			11 => HideFromListing, 
			12 => UseCustomJoinVerifier, 
			13 => CustomJoinVerifier, 
			14 => MaxUsers, 
			15 => AwayKickEnabled, 
			16 => AwayKickMinutes, 
			17 => AutoSaveEnabled, 
			18 => AutoSaveInterval, 
			19 => AutoCleanupEnabled, 
			20 => AutoCleanupInterval, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static WorldConfiguration __New()
	{
		return new WorldConfiguration();
	}
}
