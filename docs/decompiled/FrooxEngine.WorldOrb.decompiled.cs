using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Elements.Core;
using FrooxEngine.Store;
using FrooxEngine.UIX;
using SkyFrost.Base;

namespace FrooxEngine;

[Category(new string[] { "World" })]
public class WorldOrb : Component, ITouchable, IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable, IWorldLink, IMaterialApplyPolicy, IItemMetadataSource
{
	public enum VisitState
	{
		Visited,
		Updated,
		New
	}

	private enum MenuState
	{
		Off,
		Closed,
		Open
	}

	public const string FONT_MATERIAL_KEY = "WorldOrb_FontMaterial";

	public const float RADIUS = 0.05f;

	public readonly SyncRef<User> SessionStartingUser;

	public readonly Sync<Uri> URL_Field;

	public readonly SyncFieldList<Uri> ActiveSessionURLs_Field;

	public readonly SyncDelegate<WorldCreator> CreateIfNotExists_Field;

	public readonly SyncDelegate<Action<WorldOrb>> OpenActionOverride;

	public readonly Sync<VisitState> Visit;

	public readonly Sync<int> ActiveUsers;

	public World LocalOpenedWorld;

	public bool LocalFocused;

	[NonPersistent]
	public readonly Sync<bool> RecordStateUpdated;

	public readonly Sync<bool> IsPublic;

	public readonly Sync<bool> CanModify;

	public readonly Sync<colorX> LongPressIndicatorColor;

	public readonly Sync<float> LongPressTime;

	protected readonly SyncRef<RingMesh> _longPressIndicator;

	protected readonly SyncRef<UnlitMaterial> _longPressIndicatorMaterial;

	public readonly SyncDelegate<Action<WorldOrb, TouchEventInfo>> Touched;

	public readonly SyncDelegate<Action<WorldOrb>> LongPressTriggered;

	protected readonly Sync<Uri> _lastFetchedUrl;

	protected readonly Sync<bool> _isReadOnly;

	protected readonly SyncRef<Slot> _orbRoot;

	protected readonly SyncRef<Slot> _infoRoot;

	protected readonly SyncRef<StaticTexture2D> _thumbTex;

	protected readonly SyncRef<Projection360Material> _thumbMaterial;

	protected readonly DriveRef<PBS_RimMetallic> _shellMaterial;

	protected readonly SyncRef<TextRenderer> _nameText;

	protected readonly SyncRef<TextRenderer> _creatorText;

	protected readonly SyncRef<TextRenderer> _visitsText;

	protected readonly SyncRef<TextRenderer> _usersText;

	protected readonly FieldDrive<float3> _namePosition;

	protected readonly FieldDrive<float3> _creatorPosition;

	protected readonly FieldDrive<float3> _visitsPosition;

	protected readonly FieldDrive<float3> _usersPosition;

	protected readonly FieldDrive<string> _userCountText;

	protected readonly FieldDrive<float3> _sizeDrive;

	protected readonly SyncRef<Slot> _iconSlot;

	protected readonly SyncRef<StaticTexture2D> _iconTexture;

	protected readonly SyncRef<UnlitMaterial> _iconMaterial;

	protected readonly FieldDrive<float3> _iconPosition;

	protected readonly SlotCleanupRef<NewWorldDialog> _sessionStartDialog;

	[NonPersistent]
	[DefaultValue(-1.0)]
	[DontCopy]
	protected readonly Sync<double> _lastTouch;

	[NonPersistent]
	[DefaultValue(-1.0)]
	[DontCopy]
	protected readonly Sync<double> _lastFlash;

	private float _touchAccumulation;

	private int _touchAccumulationFrames;

	private float _angle;

	private bool sizeUpdated;

	private bool _isOpening;

	private WorldLoadProgress _loadingIndicator;

	public override int Version => 5;

	bool IMaterialApplyPolicy.CanApplyMaterial => false;

	public static colorX ACTIVE_SESSION_COLOR => new colorX(1f, 0f, 0.5f);

	public static colorX EMPTY_SESSION_COLOR => new colorX(0.5f, 0f, 1f);

	public static colorX NEW_COLOR => new colorX(1.5f, 1.2f, 0.5f);

	public static colorX OPENED_COLOR => colorX.Orange;

	public static colorX FOCUSED_COLOR => colorX.Cyan;

	private float NAME_HEIGHT => 0.075f;

	private float CREATOR_HEIGHT => NAME_HEIGHT * 0.35f;

	private float NAME_WIDTH => 0.2f;

	private float VISITS_HEIGHT => 0.0375f;

	private float VISITS_WIDTH => 0.05f;

	private float ICON_SIZE => 0.030000001f;

	public Uri URL
	{
		get
		{
			return URL_Field;
		}
		set
		{
			URL_Field.Value = value;
		}
	}

	public IEnumerable<Uri> ActiveSessionURLs
	{
		get
		{
			return ActiveSessionURLs_Field;
		}
		set
		{
			ActiveSessionURLs_Field.Clear();
			if (value != null)
			{
				ActiveSessionURLs_Field.AddRange(value);
			}
		}
	}

	public bool HasAnyActiveSessionURLs => ActiveSessionURLs.Any((Uri u) => u != null);

	public WorldCreator CreateIfNotExists
	{
		get
		{
			return CreateIfNotExists_Field.Target;
		}
		set
		{
			CreateIfNotExists_Field.Target = value;
		}
	}

	public string WorldName
	{
		get
		{
			return _nameText.Target.Text;
		}
		set
		{
			_nameText.Target.Text.Value = value;
			base.Slot.Name = value;
		}
	}

	public Uri ThumbnailTexURL
	{
		get
		{
			return _thumbTex.Target.URL.Value;
		}
		set
		{
			SetThumbnail(value);
		}
	}

	public bool ThumbnailLoaded => _thumbTex.Target?.IsAssetAvailable ?? false;

	public string CreatorName
	{
		get
		{
			return _creatorText.Target.Text;
		}
		set
		{
			_creatorText.Target.Text.Value = value;
		}
	}

	public colorX CreatorTextColor
	{
		get
		{
			return _creatorText.Target.Color.Value;
		}
		set
		{
			_creatorText.Target.Color.Value = value;
		}
	}

	public bool LocalOpened
	{
		get
		{
			if (LocalOpenedWorld != null)
			{
				return !LocalOpenedWorld.IsDisposed;
			}
			return false;
		}
	}

	public bool CanTransfer => ComputeTransfer(shouldTransfer: false);

	public bool ShouldTransfer => ComputeTransfer(shouldTransfer: true);

	public Slot OrbRoot => _orbRoot.Target;

	public Slot InfoRoot => _infoRoot.Target;

	public float LongPressProgress { get; private set; }

	string IItemMetadataSource.ItemName => WorldName;

	IEnumerable<string> IItemMetadataSource.ItemTags
	{
		get
		{
			if (URL != null)
			{
				yield return RecordTags.WorldOrb;
				yield return RecordTags.CorrespondingWorldUrl(URL.ToString());
			}
		}
	}

	public bool CanTouchOutOfSight => false;

	public bool AcceptsExistingTouch => false;

	public bool HasActiveSessionURL(Uri url)
	{
		return ActiveSessionURLs.Contains(url);
	}

	public bool MatchesAnyActiveSessionURL(IEnumerable<Uri> urls)
	{
		return urls.Any((Uri u) => ActiveSessionURLs.Contains(u));
	}

	public void SetThumbnail(Uri url)
	{
		_thumbTex.Target.URL.Value = url;
		_thumbTex.Target.DirectLoad.Value = url?.Scheme != base.Cloud.Assets.DBScheme;
	}

	private void SetProgressIndicator(float progress, colorX color)
	{
		LongPressIndicatorColor.Value = color;
		if (progress != LongPressProgress)
		{
			LongPressProgress = progress;
			if (_longPressIndicator.Target == null)
			{
				AttachedModel<RingMesh, UnlitMaterial> attachedModel = InfoRoot.AddSlot("Long Press Indicator").AttachMesh<RingMesh, UnlitMaterial>();
				_longPressIndicator.Target = attachedModel.mesh;
				_longPressIndicatorMaterial.Target = attachedModel.material;
				attachedModel.mesh.InnerRadius.Value = 0.0575f;
				attachedModel.mesh.OuterRadius.Value = 0.0675f;
			}
			_longPressIndicatorMaterial.Target.TintColor.Value = MathX.Lerp(in color, colorX.White, 0.25f) * 1.5f;
			_longPressIndicator.Target.Slot.ActiveSelf = progress > 0f;
			_longPressIndicator.Target.Arc.Value = 360f * progress;
		}
	}

	private bool ComputeTransfer(bool shouldTransfer)
	{
		if (URL == null)
		{
			return false;
		}
		if (base.World.CorrespondingRecord == null)
		{
			return false;
		}
		if (!base.Cloud.Records.ExtractRecordID(URL, out var ownerId, out var _))
		{
			return false;
		}
		OwnerType ownerType = IdUtil.GetOwnerType(base.World.CorrespondingRecord.OwnerId);
		OwnerType ownerType2 = IdUtil.GetOwnerType(ownerId);
		if (ownerType == ownerType2)
		{
			return false;
		}
		if (!base.Engine.RecordManager.CanModify(base.World.CorrespondingRecord.OwnerId))
		{
			return false;
		}
		if (!shouldTransfer)
		{
			return true;
		}
		switch (ownerType2)
		{
		case OwnerType.Machine:
			return true;
		case OwnerType.User:
			if (ownerType == OwnerType.Group)
			{
				return true;
			}
			break;
		}
		return false;
	}

	protected override void OnAwake()
	{
		LongPressIndicatorColor.Value = LegacyUIStyle.BASE_COLOR;
		LongPressTime.Value = 0.6666f;
		base.Engine.WorldManager.WorldAdded += WorldManager_WorldAdded;
		base.Engine.WorldManager.WorldRemoved += WorldManager_WorldRemoved;
		base.Engine.WorldManager.WorldFocused += WorldManager_WorldFocused;
	}

	protected override void OnDispose()
	{
		_loadingIndicator?.DestroyIndicator();
		_loadingIndicator = null;
		base.Engine.WorldManager.WorldAdded -= WorldManager_WorldAdded;
		base.Engine.WorldManager.WorldRemoved -= WorldManager_WorldRemoved;
		base.Engine.WorldManager.WorldFocused -= WorldManager_WorldFocused;
		base.OnDispose();
	}

	private bool MatchesSessionURL(World world)
	{
		return ActiveSessionURLs.Any((Uri uri) => world.SessionURLs.Contains(uri));
	}

	private void WorldManager_WorldFocused(World world)
	{
		LocalFocused = MatchesSessionURL(world) || (URL != null && URL == world.RecordURL);
		MarkChangeDirty();
	}

	private void WorldManager_WorldRemoved(World obj)
	{
		if (obj == LocalOpenedWorld)
		{
			LocalOpenedWorld = null;
			MarkChangeDirty();
		}
	}

	private void WorldManager_WorldAdded(World world)
	{
		if (LocalOpened)
		{
			return;
		}
		RunSynchronously(delegate
		{
			if (MatchesSessionURL(world) || (URL != null && URL == world.RecordURL))
			{
				LocalOpenedWorld = world;
				MarkChangeDirty();
			}
		});
	}

	protected override void OnAttach()
	{
		SetupOrb();
	}

	public static TextUnlitMaterial GetFontMaterial(World world)
	{
		return world.GetSharedComponentOrCreate("WorldOrb_FontMaterial", delegate(TextUnlitMaterial mat)
		{
			mat.OutlineThickness.Value = 0.2f;
			mat.OutlineColor.Value = colorX.Black;
			mat.FaceDilate.Value = 0.2f;
		}, 0, replaceExisting: true);
	}

	private void SetupOrb()
	{
		TextUnlitMaterial fontMaterial = GetFontMaterial(base.World);
		_orbRoot.Target = base.Slot.AddSlot("Orb");
		_infoRoot.Target = base.Slot.AddSlot("Info");
		LookAtUser lookAtUser = _infoRoot.Target.AttachComponent<LookAtUser>();
		lookAtUser.TargetAtLocalUser.Value = true;
		lookAtUser.RotationOffset.Value = floatQ.AxisAngle(float3.Up, 180f);
		SphereMesh sphereMesh = _orbRoot.Target.AttachComponent<SphereMesh>();
		MeshRenderer meshRenderer = _orbRoot.Target.AttachComponent<MeshRenderer>();
		MeshRenderer meshRenderer2 = _orbRoot.Target.AttachComponent<MeshRenderer>();
		_thumbMaterial.Target = _orbRoot.Target.AttachComponent<Projection360Material>();
		_shellMaterial.Target = _orbRoot.Target.AttachComponent<PBS_RimMetallic>();
		_thumbTex.Target = _orbRoot.Target.AttachComponent<StaticTexture2D>();
		sphereMesh.Radius.Value = 0.05f;
		_thumbMaterial.Target.Projection.Value = Projection360Material.Mode.Normal;
		_thumbMaterial.Target.Texture.Target = _thumbTex.Target;
		meshRenderer.Mesh.Target = sphereMesh;
		meshRenderer.Material.Target = _thumbMaterial.Target;
		meshRenderer2.Mesh.Target = sphereMesh;
		meshRenderer2.Material.Target = _shellMaterial.Target;
		_sizeDrive.Target = _orbRoot.Target.Scale_Field;
		SphereCollider sphereCollider = base.Slot.AttachComponent<SphereCollider>();
		sphereCollider.Radius.Value = 0.05f;
		sphereCollider.SetActive();
		base.Slot.AttachComponent<Grabbable>().ReparentOnRelease.Value = true;
		Slot slot = _infoRoot.Target.AddSlot("Name");
		_nameText.Target = slot.AttachComponent<TextRenderer>();
		_nameText.Target.Bounded.Value = true;
		_nameText.Target.AutoSize = true;
		_nameText.Target.BoundsSize.Value = new float2(NAME_WIDTH, NAME_HEIGHT);
		_nameText.Target.Color.Value = colorX.White;
		_nameText.Target.Material.Target = fontMaterial;
		_nameText.Target.LineHeight.Value = 0.8f;
		_namePosition.Target = slot.Position_Field;
		Slot slot2 = _infoRoot.Target.AddSlot("Creator");
		_creatorText.Target = slot2.AttachComponent<TextRenderer>();
		_creatorText.Target.Bounded.Value = true;
		_creatorText.Target.AutoSize = true;
		_creatorText.Target.BoundsSize.Value = new float2(NAME_WIDTH, CREATOR_HEIGHT);
		_creatorText.Target.Color.Value = colorX.White;
		_creatorText.Target.Material.Target = fontMaterial;
		_creatorPosition.Target = slot2.Position_Field;
		Slot slot3 = _infoRoot.Target.AddSlot("Visits");
		_visitsText.Target = slot3.AttachComponent<TextRenderer>();
		_visitsText.Target.Bounded.Value = true;
		_visitsText.Target.AutoSize = true;
		_visitsText.Target.BoundsSize.Value = new float2(VISITS_WIDTH, VISITS_HEIGHT);
		_visitsText.Target.Color.Value = colorX.White;
		_visitsText.Target.Align = Alignment.TopRight;
		_visitsText.Target.Material.Target = fontMaterial;
		_visitsPosition.Target = slot3.Position_Field;
		Slot slot4 = _infoRoot.Target.AddSlot("Users");
		_usersText.Target = slot4.AttachComponent<TextRenderer>();
		_usersText.Target.Bounded.Value = true;
		_usersText.Target.AutoSize = true;
		_usersText.Target.BoundsSize.Value = new float2(VISITS_WIDTH, VISITS_HEIGHT);
		_usersText.Target.Color.Value = ACTIVE_SESSION_COLOR;
		_usersText.Target.Align = Alignment.TopLeft;
		_usersText.Target.Material.Target = fontMaterial;
		_userCountText.Target = _usersText.Target.Text;
		_usersPosition.Target = slot4.Position_Field;
	}

	private void UpdateInfoPositions()
	{
		_namePosition.Target.Value = new float3(0f, 0.05f + NAME_HEIGHT * 0.5f);
		_creatorPosition.Target.Value = new float3(0f, 0f - (0.05f + CREATOR_HEIGHT * 0.6f));
		_visitsPosition.Target.Value = floatQ.AxisAngle(float3.Forward, 45f) * new float3(-0.05f) + new float3((0f - VISITS_WIDTH) * 0.5f, (0f - VISITS_HEIGHT) * 0.25f);
		_usersPosition.Target.Value = floatQ.AxisAngle(float3.Forward, -45f) * new float3(0.05f) + new float3(VISITS_WIDTH * 0.5f, (0f - VISITS_HEIGHT) * 0.25f);
		if (_iconPosition.Target != null)
		{
			_iconPosition.Target.Value = floatQ.AxisAngle(float3.Forward, -30f) * new float3(-0.05f) + new float3(0f - ICON_SIZE, ICON_SIZE * 0.5f);
		}
	}

	public void SetIcon(Uri url)
	{
		EnsureIcon();
		_iconTexture.Target.URL.Value = url;
	}

	public void RemoveIcon()
	{
		_iconSlot.Target?.Destroy();
	}

	private void EnsureIcon()
	{
		if (_iconSlot.Target == null)
		{
			_iconSlot.Target = _infoRoot.Target.AddSlot("Icon");
			_iconPosition.Target = _iconSlot.Target.Position_Field;
			AttachedModel<QuadMesh, UnlitMaterial> attachedModel = _iconSlot.Target.AttachMesh<QuadMesh, UnlitMaterial>();
			attachedModel.mesh.Size.Value = float2.One * ICON_SIZE;
			attachedModel.material.BlendMode.Value = BlendMode.Alpha;
			_iconTexture.Target = _iconSlot.Target.AttachComponent<StaticTexture2D>();
			attachedModel.material.Texture.Target = _iconTexture.Target;
			_iconMaterial.Target = attachedModel.material;
		}
	}

	protected override void OnStart()
	{
		_lastTouch.Value = -1.0;
		if (base.World.IsAuthority)
		{
			if (_orbRoot.Target == null)
			{
				base.Slot.LocalScale = float3.One;
				WorldLink component = base.Slot.GetComponent<WorldLink>();
				if (component != null)
				{
					URL = component.URL.Value;
					component.Destroy();
				}
				Uri value = base.Slot.GetComponent<StaticTexture2D>()?.URL.Value;
				foreach (Component item in base.Slot.Components.ToList())
				{
					if (item != this)
					{
						item.Destroy();
					}
				}
				SetupOrb();
				_thumbTex.Target.URL.Value = value;
			}
			if (_lastFetchedUrl.Value != URL)
			{
				RefreshInfo();
			}
			_thumbMaterial.Target.Sidedness.Value = Sidedness.Back;
		}
		UpdateInfoPositions();
	}

	public void RefreshInfo()
	{
		StartTask(RefreshOrbInfo);
	}

	private async Task RefreshOrbInfo()
	{
		RecordStateUpdated.Value = true;
		_lastFetchedUrl.Value = URL;
		CloudResult<FrooxEngine.Store.Record> cloudResult = await base.Engine.RecordManager.FetchRecord(URL, TimeSpan.FromDays(1));
		if (cloudResult.IsOK)
		{
			await UpdateFromRecord(cloudResult.Entity);
		}
		else if (cloudResult.State == HttpStatusCode.NotFound && OpenActionOverride.Target == null)
		{
			base.Slot.Destroy();
		}
	}

	public async Task UpdateFromRecord(IRecord record)
	{
		URL = record.GetUrl(base.Cloud.Platform);
		_nameText.Target.Text.Value = record.Name ?? "<i>Unnamed</i>";
		_isReadOnly.Value = record.IsReadOnly;
		OwnerType ownerType = IdUtil.GetOwnerType(record.OwnerId);
		switch (ownerType)
		{
		case OwnerType.Machine:
			CreatorName = "<i>(machine)</i>";
			CreatorTextColor = new colorX(1f, 0.2f);
			break;
		case OwnerType.User:
		{
			CreatorTextColor = new colorX(1f, 0.8f, 0.4f, 0.6f);
			CloudResult<SkyFrost.Base.User> cloudResult2 = await base.Engine.Cloud.Users.GetUserCached(record.OwnerId);
			if (cloudResult2.IsOK)
			{
				CreatorName = "<i>by </i>" + cloudResult2.Entity.Username;
			}
			break;
		}
		case OwnerType.Group:
		{
			CreatorTextColor = new colorX(0.4f, 0.8f, 1f, 0.6f);
			CloudResult<Group> cloudResult = await base.Engine.Cloud.Groups.GetGroupCached(record.OwnerId);
			if (cloudResult.IsOK)
			{
				CreatorName = "<i>by </i>" + cloudResult.Entity.Name;
			}
			break;
		}
		}
		if (record.Visits > 0)
		{
			_visitsText.Target.Text.Value = record.Visits.ToString();
		}
		else
		{
			_visitsText.Target.Text.Value = "";
		}
		if (record.ThumbnailURI != null)
		{
			_thumbTex.Target.URL.Value = new Uri(record.ThumbnailURI);
		}
		IsPublic.Value = record.IsPublic;
		CanModify.Value = base.Engine.RecordManager.CanModify(record);
		if (ownerType != OwnerType.Machine)
		{
			LocalVisit localVisit = await base.Engine.LocalDB.GetVisitAsync(URL.ToString());
			if (localVisit == null)
			{
				Visit.Value = VisitState.New;
			}
			else if (localVisit.globalVersion < record.Version.GlobalVersion)
			{
				Visit.Value = VisitState.Updated;
			}
			else
			{
				Visit.Value = VisitState.Visited;
			}
		}
		else
		{
			Visit.Value = VisitState.Visited;
		}
		RecordStateUpdated.Value = true;
	}

	protected override void OnChanges()
	{
		if (_userCountText.IsLinkValid)
		{
			if (ActiveUsers.Value > 0)
			{
				_userCountText.Target.Value = ActiveUsers.Value.ToString();
			}
			else
			{
				_userCountText.Target.Value = null;
			}
		}
		UpdateMaterial();
	}

	private void UpdateMaterial()
	{
		if (_shellMaterial.IsLinkValid)
		{
			float num = (float)MathX.Clamp01(_lastFlash.Value - base.Time.WorldTime + 1.0);
			PBS_RimMetallic target = _shellMaterial.Target;
			target.RenderQueue.Value = 3010;
			target.Transparent.Value = true;
			target.AlbedoColor.Value = new colorX(0.1f, 0.1f, 0.1f, 0f);
			target.Metallic.Value = 0.1f;
			target.Smoothness.Value = 0.95f;
			target.RimPower.Value = 1.5f;
			_thumbMaterial.Target.Tint.Value = colorX.White;
			if (LocalFocused)
			{
				target.RimColor.Value = FOCUSED_COLOR;
			}
			else if (LocalOpened)
			{
				target.RimColor.Value = OPENED_COLOR;
			}
			else if (ActiveUsers.Value > 0)
			{
				target.RimColor.Value = ACTIVE_SESSION_COLOR;
			}
			else if (HasAnyActiveSessionURLs)
			{
				target.RimColor.Value = EMPTY_SESSION_COLOR;
			}
			else if (Visit.Value != VisitState.Visited)
			{
				target.RimColor.Value = NEW_COLOR;
			}
			else if (!HasAnyActiveSessionURLs && URL == null)
			{
				target.RimColor.Value = colorX.Red;
				_thumbMaterial.Target.Tint.Value = MathX.Lerp(colorX.Red, colorX.White, 0.5f);
			}
			else
			{
				target.RimColor.Value = colorX.Clear;
			}
			target.EmissiveColor.Value = colorX.Yellow * num;
			if (_sizeDrive.IsLinkValid)
			{
				_sizeDrive.Target.Value = float3.One;
			}
		}
	}

	private void UpdateSize()
	{
		double num = base.Time.WorldTime - _lastTouch.Value;
		float num2 = 1f - MathX.Clamp01((float)num / 0.5f);
		if (num2 <= 0f)
		{
			if (sizeUpdated)
			{
				return;
			}
			sizeUpdated = true;
		}
		else
		{
			sizeUpdated = false;
		}
		float num3 = num2 * 20f;
		_angle += num3 * base.Time.Delta;
		if (_sizeDrive.IsLinkValid)
		{
			_sizeDrive.Target.Value = new float3(1f + MathX.Sin(_angle) * 0.05f * num2, 1f + MathX.Cos(_angle) * 0.05f * num2, 1f - MathX.Sin(_angle) * 0.05f * num2);
		}
	}

	protected override void OnCommonUpdate()
	{
		UpdateMaterial();
		UpdateSize();
	}

	public bool CanTouchInteract(TouchSource touchSource)
	{
		return true;
	}

	public void OnTouch(in TouchEventInfo touchInfo)
	{
		if (touchInfo.touch == EventState.Begin && URL?.Scheme == base.Cloud.Platform.RecordScheme && !RecordStateUpdated.Value)
		{
			RefreshInfo();
		}
		Touched.Target?.Invoke(this, touchInfo);
		if (touchInfo.touch == EventState.Stay && touchInfo.type == TouchType.Remote && LongPressTriggered.Target != null)
		{
			if (_touchAccumulation >= 0f)
			{
				if (_touchAccumulationFrames++ > 5)
				{
					_touchAccumulation += base.Time.Delta / (float)LongPressTime;
				}
				if (_touchAccumulation >= 1f)
				{
					touchInfo.source?.Slot.TryVibrateMedium();
					if (LongPressTriggered.Target != null)
					{
						LongPressTriggered.Target(this);
					}
					else
					{
						ToggleSessionStartDialog();
					}
					_touchAccumulation = -1f;
				}
			}
		}
		else
		{
			_touchAccumulation = 0f;
			_touchAccumulationFrames = 0;
		}
		SetProgressIndicator(MathX.Clamp01((_touchAccumulation - 0.2f) / 0.8f), LongPressIndicatorColor);
		if (touchInfo.touch != EventState.Begin)
		{
			return;
		}
		Grabbable component = base.Slot.GetComponent<Grabbable>();
		if (component != null && component.IsGrabbed)
		{
			return;
		}
		touchInfo.source.Slot.TryVibrateShort();
		if (base.World == Userspace.UserspaceWorld)
		{
			return;
		}
		ToggleContextMenu(touchInfo);
		if (base.Time.WorldTime - _lastTouch.Value < 0.5)
		{
			_lastTouch.Value = -1.0;
			if (URL != null || HasAnyActiveSessionURLs)
			{
				Open(touchInfo.source.Slot);
			}
		}
		else
		{
			_lastTouch.Value = base.Time.WorldTime;
		}
	}

	public void Open(Slot source = null)
	{
		_lastFlash.Value = base.Time.WorldTime;
		source?.TryVibrateLong();
		StartTask(async delegate
		{
			await OpenAsync();
		});
	}

	public bool OpenLink(WorldStartSettings startInfo = null)
	{
		StartTask(async delegate
		{
			await OpenAsync(startInfo);
		});
		return true;
	}

	private async Task OpenAsync(WorldStartSettings startInfo = null)
	{
		if (OpenActionOverride.Target != null)
		{
			OpenActionOverride.Target(this);
		}
		else
		{
			if (_isOpening)
			{
				return;
			}
			_isOpening = true;
			_loadingIndicator?.DestroyIndicator();
			if (startInfo?.LoadingIndicator == null)
			{
				if (SessionStartingUser.Target != null)
				{
					_loadingIndicator = await WorldLoadProgress.CreateIndicator(WorldName, "World.Waiting".AsLocaleKey(), "World.HostIsStarting".AsLocaleKey());
				}
				else
				{
					_loadingIndicator = await WorldLoadProgress.CreateIndicator(WorldName);
				}
			}
			else
			{
				_loadingIndicator = startInfo.LoadingIndicator;
			}
			await default(ToWorld);
			if (SessionStartingUser.Target != null)
			{
				while (!HasAnyActiveSessionURLs)
				{
					await default(NextUpdate);
				}
				await DelaySeconds(2f);
			}
			Visit.Value = VisitState.Visited;
			bool startingSession = !HasAnyActiveSessionURLs;
			if (startingSession)
			{
				SessionStartingUser.Target = base.LocalUser;
				if (base.World.ActiveUserCount > 1)
				{
					await DelaySeconds(2f);
				}
				if (SessionStartingUser.Target != base.LocalUser)
				{
					_isOpening = false;
					await OpenAsync();
					return;
				}
			}
			if (startInfo == null)
			{
				startInfo = new WorldStartSettings
				{
					GetExisting = true,
					Relation = Userspace.WorldRelation.Nest,
					FetchedWorldName = WorldName
				};
			}
			startInfo.Link = this;
			if (startInfo.LoadingIndicator == null)
			{
				startInfo.LoadingIndicator = _loadingIndicator;
			}
			_loadingIndicator = null;
			World world = await Userspace.OpenWorld(startInfo);
			await Task.Delay(20000);
			if (startingSession && SessionStartingUser.Target == base.LocalUser)
			{
				SessionStartingUser.Target = null;
			}
			_isOpening = false;
			WorldOpened(world);
		}
	}

	private void ToggleContextMenu(TouchEventInfo touchInfo)
	{
		Slot pressingSlot = touchInfo.source.Slot;
		InteractionHandler commonTool = pressingSlot.FindInteractionHandler();
		bool canStartSession = URL != null;
		bool canJoinSession = HasAnyActiveSessionURLs || SessionStartingUser.Target != null;
		bool canModify = false;
		if (!_isReadOnly.Value && base.Cloud.Records.ExtractRecordID(URL, out var ownerId, out var _))
		{
			canModify = base.Engine.RecordManager.CanModify(ownerId);
		}
		if (!canStartSession && !canModify)
		{
			return;
		}
		StartTask(async delegate
		{
			ContextMenu contextMenu = await base.LocalUser.ToggleContextMenu(this, pressingSlot, new ContextMenuOptions
			{
				disableFlick = true
			});
			if (contextMenu != null)
			{
				if (canStartSession)
				{
					contextMenu.AddItem("World.Actions.StartSession".AsLocaleKey(), OfficialAssets.Graphics.Icons.General.LightBluePlus, (colorX?)null, OnStartNewSession);
					contextMenu.AddItem("World.Actions.StartCustomSession".AsLocaleKey(), OfficialAssets.Graphics.Icons.General.NewCustom, (colorX?)null, OnStartCustomSession);
				}
				if (canJoinSession)
				{
					contextMenu.AddItem("World.Actions.Join".AsLocaleKey(), OfficialAssets.Graphics.Icons.Dash.Login, (colorX?)null, OnJoinSession);
				}
				if (canModify)
				{
					WorldOrb worldOrb = commonTool?.Grabber?.HolderSlot?.GetComponentInChildren<WorldOrb>();
					if (CanUseForOverwrite(worldOrb))
					{
						contextMenu.AddRefItem((LocaleString)this.GetLocalized("World.Actions.Overwrite", null, "name", worldOrb.WorldName), OfficialAssets.Graphics.Icons.Dash.OverwriteWorld, (colorX?)null, OnOverwrite, worldOrb);
					}
					else
					{
						contextMenu.AddItem("World.Actions.ModifyMetadata".AsLocaleKey(), OfficialAssets.Graphics.Icons.Dash.Settings, (colorX?)null, OnEditMetadata);
						contextMenu.AddItem("World.Actions.Clone".AsLocaleKey(), OfficialAssets.Graphics.Icons.Dash.SaveWorldCopy, (colorX?)null, OnCloneWorld);
						contextMenu.AddItem("World.Actions.Delete".AsLocaleKey(), OfficialAssets.Graphics.Icons.General.Cancel, (colorX?)null, OnDeleteWorld);
					}
				}
			}
		});
	}

	private bool CanUseForOverwrite(WorldOrb overwriteWith)
	{
		if (overwriteWith?.URL == null)
		{
			return false;
		}
		if (!base.Cloud.Records.ExtractRecordID(overwriteWith.URL, out var ownerId, out var _))
		{
			return false;
		}
		if (!base.Engine.RecordManager.CanModify(ownerId))
		{
			return false;
		}
		return true;
	}

	private void WorldOpened(World world)
	{
		if (world == null)
		{
			NotificationMessage.SpawnTextMessage(color: new colorX(1f, 0.2f, 0.3f), root: base.Slot, message: "Permissions.NotAllowedToOpen".AsLocaleKey());
		}
	}

	private void ToggleSessionStartDialog()
	{
		if (_sessionStartDialog.Target != null)
		{
			_sessionStartDialog.Target.Slot.Destroy();
		}
		else if (!(URL == null) && !HasAnyActiveSessionURLs)
		{
			Slot slot = base.World.AddSlot("Session Start");
			slot.PositionInFrontOfUser(float3.Backward);
			NewWorldDialog newWorldDialog = NewWorldDialog.OpenDialogWindow(slot, "NewWorld.SessionTitle".AsLocaleKey());
			newWorldDialog.BuildUI(CustomWorldStart, delegate(UIBuilder ui)
			{
				ui.Text((LocaleString)WorldName);
				ui.Text((LocaleString)CreatorName);
				ui.PushStyle();
				ui.Style.PreferredHeight = 96f;
				ui.Image(ThumbnailTexURL);
				ui.PopStyle();
			});
			newWorldDialog.WorldName = WorldName;
			_sessionStartDialog.Target = newWorldDialog;
		}
	}

	[SyncMethod(typeof(Action<NewWorldDialog>), new string[] { })]
	private void CustomWorldStart(NewWorldDialog newWorldDialog)
	{
		string worldName = newWorldDialog.WorldName;
		bool mobileFriendly = newWorldDialog.MobileFriendly;
		SessionAccessLevel accessLevel = newWorldDialog.AccessLevel;
		bool unsafeMode = newWorldDialog.UnsafeMode;
		StartTask(async delegate
		{
			World world = await Userspace.OpenWorld(new WorldStartSettings
			{
				Link = this,
				DefaultAccessLevel = accessLevel,
				UnsafeMode = unsafeMode,
				ForcePort = newWorldDialog.Port,
				FetchedWorldName = worldName
			});
			if (world != null)
			{
				world.Name = worldName;
				world.MobileFriendly = mobileFriendly;
				WorldOpened(world);
			}
		});
	}

	private void DuplicateOrb(LegacySegmentCircleMenuController.Item item)
	{
		SplitWorldOrb().URL = URL;
	}

	private WorldOrb SplitWorldOrb()
	{
		Slot slot = base.Slot.Parent.AddSlot(base.Slot.Name);
		slot.LocalPosition = base.Slot.LocalPosition;
		slot.LocalRotation = base.Slot.LocalRotation;
		slot.LocalScale = base.Slot.LocalScale;
		WorldOrb result = slot.AttachComponent<WorldOrb>();
		float3 v = base.World.LocalUser.Root.HeadSlot.Left;
		float3 globalPoint = base.Slot.GlobalPosition + v * 0.05f * 2f;
		float3 globalPoint2 = base.Slot.GlobalPosition - v * 0.05f * 2f;
		globalPoint = base.Slot.Parent.GlobalPointToLocal(in globalPoint);
		globalPoint2 = base.Slot.Parent.GlobalPointToLocal(in globalPoint2);
		base.Slot.Position_Field.TweenTo(globalPoint, 0.25f);
		slot.Position_Field.TweenTo(globalPoint2, 0.25f);
		return result;
	}

	private async Task DuplicateWorld()
	{
		CloudResult<FrooxEngine.Store.Record> cloudResult = await base.Engine.RecordManager.FetchRecord(URL).ConfigureAwait(continueOnCapturedContext: false);
		if (cloudResult.IsOK && cloudResult.IsOK)
		{
			FrooxEngine.Store.Record record = cloudResult.Entity;
			record.RecordId = RecordHelper.GenerateRecordID();
			record.Name += " (copy)";
			if ((await base.Engine.RecordManager.SaveRecord(record)).saved)
			{
				await default(ToWorld);
				SplitWorldOrb().URL = record.GetUrl(base.Cloud.Platform);
			}
		}
	}

	private void DeleteWorld()
	{
		base.World.Coroutines.StartTask(async delegate
		{
			await default(ToWorld);
			Uri uRL = URL;
			Engine _engine = base.Engine;
			AnimatedDestroy();
			UniLog.Log("Deleting world: " + uRL);
			CloudResult<FrooxEngine.Store.Record> cloudResult = await _engine.RecordManager.FetchRecord(uRL);
			if (cloudResult.IsOK)
			{
				await _engine.RecordManager.DeleteRecord(cloudResult.Entity);
				UniLog.Log("World delete complete");
			}
			else
			{
				UniLog.Warning("Error fetching record data for delete: " + cloudResult.State.ToString() + "\n" + cloudResult.Content);
			}
		});
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnStartNewSession(IButton button, ButtonEventData eventData)
	{
		ActiveSessionURLs = null;
		SessionStartingUser.Target = null;
		Open();
		base.LocalUser.CloseContextMenu(this);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnStartCustomSession(IButton button, ButtonEventData eventData)
	{
		ToggleSessionStartDialog();
		base.LocalUser.CloseContextMenu(this);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnJoinSession(IButton button, ButtonEventData eventData)
	{
		Open();
		base.LocalUser.CloseContextMenu(this);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnEditMetadata(IButton button, ButtonEventData eventData)
	{
		Slot slot = base.LocalUserSpace.AddSlot("Record Edit Form");
		_ = base.World.LocalUser.Root;
		slot.PositionInFrontOfUser(float3.Backward, float3.Right * 0.5f);
		RecordEditForm form = RecordEditForm.OpenDialogWindow(slot);
		StartTask(async delegate
		{
			CloudResult<FrooxEngine.Store.Record> cloudResult = await base.Engine.RecordManager.FetchRecord(URL);
			if (cloudResult.IsOK)
			{
				form.Setup(this, cloudResult.Entity);
			}
			else
			{
				form.Error(cloudResult.State.ToString() + "\n" + cloudResult.Content);
			}
		});
		base.LocalUser.CloseContextMenu(this);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnCancelMenu(IButton button, ButtonEventData eventData)
	{
		base.LocalUser.CloseContextMenu(this);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnCloneWorld(IButton button, ButtonEventData eventData)
	{
		StartTask(DuplicateWorld);
		base.LocalUser.CloseContextMenu(this);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnDeleteWorld(IButton button, ButtonEventData eventData)
	{
		StartTask(async delegate
		{
			ContextMenu contextMenu = await base.LocalUser.OpenContextMenu(this, eventData.source.Slot, new ContextMenuOptions
			{
				disableFlick = true
			});
			if (contextMenu != null)
			{
				contextMenu.AddItem("World.Actions.ConfirmDelete".AsLocaleKey("<b><color=#fcc>{0}</color></b>"), OfficialAssets.Graphics.Icons.General.Cancel, new colorX?(colorX.Red), OnReallyDeleteWorld);
				for (int i = 0; i < 4; i++)
				{
					contextMenu.AddItem("General.Cancel".AsLocaleKey(), (Uri?)null, new colorX?(colorX.Gray), (ButtonEventHandler)OnCancelMenu);
				}
			}
		});
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnReallyDeleteWorld(IButton button, ButtonEventData eventData)
	{
		base.LocalUser.CloseContextMenu(this);
		Userspace.OpenContextMenu(eventData.source.Slot, new ContextMenuOptions
		{
			disableFlick = true
		}, async delegate(ContextMenu menu)
		{
			for (int i = 0; i < 4; i++)
			{
				menu.AddItem("General.Cancel".AsLocaleKey(), (Uri?)null, new colorX?(colorX.Gray), (ButtonEventHandler)menu.CloseMenu);
			}
			menu.AddItem("World.Actions.ReallyConfirmDelete".AsLocaleKey("<b><color=#fcc>{0}</color></b>"), OfficialAssets.Graphics.Icons.General.Cancel, new colorX?(colorX.Red)).Button.LocalPressed += [SyncMethod(typeof(Delegate), null)] (IButton button2, ButtonEventData buttonEventData) =>
			{
				DeleteWorld();
				menu.Close();
			};
			for (int num = 0; num < 2; num++)
			{
				menu.AddItem("General.Cancel".AsLocaleKey(), (Uri?)null, new colorX?(colorX.Gray), (ButtonEventHandler)menu.CloseMenu);
			}
		});
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnOverwrite(IButton button, ButtonEventData eventData, WorldOrb otherOrb)
	{
		if (!CanUseForOverwrite(otherOrb))
		{
			return;
		}
		base.LocalUser.CloseContextMenu(this);
		Userspace.OpenContextMenu(eventData.source.Slot, new ContextMenuOptions
		{
			disableFlick = true
		}, async delegate(ContextMenu menu)
		{
			menu.AddItem("World.Actions.ConfirmOverwrite".AsLocaleKey(), OfficialAssets.Common.Icons.Bang, new colorX?(colorX.Yellow)).Button.LocalPressed += [SyncMethod(typeof(Delegate), null)] (IButton button2, ButtonEventData buttonEventData) =>
			{
				if (CanUseForOverwrite(otherOrb))
				{
					StartTask(async delegate
					{
						await default(ToWorld);
						await OverwriteWith(otherOrb);
					});
					menu.Close();
				}
			};
			for (int num = 0; num < 4; num++)
			{
				menu.AddItem("General.Cancel".AsLocaleKey(), (Uri?)null, new colorX?(colorX.Gray), (ButtonEventHandler)menu.CloseMenu);
			}
		});
	}

	public void AnimatedDestroy()
	{
		base.Slot.Scale_Field.TweenTo(float3.Zero, 0.25f, CurvePreset.Sine, null, base.Slot.Destroy);
	}

	protected override void OnLoading(DataTreeNode node, LoadControl control)
	{
		int typeVersion = control.GetTypeVersion(typeof(WorldOrb));
		if (typeVersion < 2)
		{
			RunSynchronously(delegate
			{
				Slot slot = base.Slot.Parent.AddSlot(base.Slot.Name);
				slot.CopyTransform(base.Slot);
				slot.AttachComponent<WorldOrb>().URL = URL;
				base.Slot.Destroy();
			});
		}
		if (typeVersion < 3)
		{
			RunSynchronously(delegate
			{
				InfoRoot?.GetComponent<LegacyCircleSegmentMesh>()?.Destroy();
				InfoRoot?.GetComponent<LegacyCircleSegmentMaterial>()?.Destroy();
				InfoRoot?.GetComponent<LegacySegmentCircleMenuController>()?.Destroy();
				InfoRoot?.GetComponent<MeshRenderer>()?.Destroy();
				InfoRoot?.FindChild("Items")?.Destroy();
			});
		}
		if (HasAnyActiveSessionURLs && URL != null)
		{
			RunSynchronously(delegate
			{
				ActiveSessionURLs = null;
			});
		}
		if (typeVersion < 4)
		{
			RunSynchronously(delegate
			{
				AutoLookAtUser componentInChildren = base.Slot.GetComponentInChildren<AutoLookAtUser>();
				Slot slot = componentInChildren.Slot;
				componentInChildren.Destroy();
				LookAtUser lookAtUser = slot.AttachComponent<LookAtUser>();
				lookAtUser.TargetAtLocalUser.Value = true;
				lookAtUser.RotationOffset.Value = floatQ.AxisAngle(float3.Up, 180f);
			});
		}
		if (typeVersion < 5)
		{
			control.OnLoaded(this, delegate
			{
				URL_Field.Value = URL_Field.Value.MigrateLegacyURL(base.Cloud.Platform);
			});
		}
	}

	public bool CanExecute(Slot touchedSlot, Slot touchingSlot)
	{
		InteractionHandler component = touchingSlot.GetComponent<InteractionHandler>();
		if (component != null)
		{
			touchingSlot = component.Grabber.Slot;
		}
		if (!base.Slot.IsChildOf(touchingSlot))
		{
			return false;
		}
		WorldOrb componentInChildren = touchedSlot.GetComponentInChildren<WorldOrb>();
		if (componentInChildren == null)
		{
			return false;
		}
		if (!base.Cloud.Records.ExtractRecordID(componentInChildren.URL, out var ownerId, out var _))
		{
			return false;
		}
		if (!base.Engine.RecordManager.CanModify(ownerId))
		{
			return false;
		}
		return true;
	}

	private async Task OverwriteWith(WorldOrb sourceOrb)
	{
		if (!CanUseForOverwrite(sourceOrb))
		{
			return;
		}
		WorldName = this.GetLocalized("World.Actions.Overwriting");
		Task<CloudResult<FrooxEngine.Store.Record>> task = base.Engine.RecordManager.FetchRecord(URL);
		Task<CloudResult<FrooxEngine.Store.Record>> sourceRecordTask = base.Engine.RecordManager.FetchRecord(sourceOrb.URL);
		CloudResult<FrooxEngine.Store.Record> targetRecord = await task;
		CloudResult<FrooxEngine.Store.Record> cloudResult = await sourceRecordTask;
		if (targetRecord.IsError || cloudResult.IsError)
		{
			UniLog.Warning("Failed overwriting world orb, failed to fetch one of the records.\n" + $"SourceResult: {cloudResult.State}, TargetResult: {targetRecord.State}");
			WorldName = this.GetLocalized("General.FAILED");
		}
		else
		{
			cloudResult.Entity.TakeIdentityFrom(targetRecord.Entity);
			RecordManager.RecordSaveResult recordSaveResult = await base.Engine.RecordManager.SaveRecord(cloudResult.Entity);
			if (recordSaveResult.task != null)
			{
				await recordSaveResult.task.Task;
			}
			RefreshInfo();
		}
	}

	public static void SpawnUrlOrb(World world, Uri uri, Transform? transform = null)
	{
		SpawnWorldOrb(world, delegate(WorldOrb orb)
		{
			orb.URL = uri;
			orb.WorldName = uri.ToString();
			if (transform.HasValue)
			{
				orb.Slot.SetGlobalTransform(transform.Value);
			}
			else
			{
				orb.Slot.PositionInFrontOfUser();
			}
		});
	}

	public static void SpawnSessionOrb(World world, SessionInfo sessionInfo, Transform? transform = null)
	{
		SpawnWorldOrb(world, delegate(WorldOrb orb)
		{
			orb.ActiveSessionURLs = sessionInfo.GetSessionURLs();
			orb.ActiveUsers.Value = sessionInfo.ActiveUsers;
			orb.WorldName = sessionInfo.Name;
			orb.CreatorName = sessionInfo.SanitizedHostUsername;
			if (!string.IsNullOrEmpty(sessionInfo.ThumbnailUrl))
			{
				orb.SetThumbnail(new Uri(sessionInfo.ThumbnailUrl));
			}
			if (transform.HasValue)
			{
				orb.Slot.SetGlobalTransform(transform.Value);
			}
			else
			{
				orb.Slot.PositionInFrontOfUser();
			}
		});
	}

	public static void SpawnWorldOrb(World w, IRecord? record, Transform? transform = null)
	{
		if (record == null)
		{
			return;
		}
		SpawnWorldOrb(w, delegate(WorldOrb orb)
		{
			orb.UpdateFromRecord(record);
			if (transform.HasValue)
			{
				orb.Slot.SetGlobalTransform(transform.Value);
			}
			else
			{
				orb.Slot.PositionInFrontOfUser();
			}
		});
	}

	public static void SpawnWorldOrb(World w, Action<WorldOrb> setup)
	{
		w.RunSynchronously(delegate
		{
			if (!w.CanSpawnObjects())
			{
				NotificationMessage.SpawnTextMessage(color: new colorX(1f, 0.2f, 0.3f), root: w.LocalUserSpace, message: "Permissions.NotAllowedToSpawn".AsLocaleKey());
			}
			else
			{
				WorldOrb obj = w.LocalUserSpace.AddSlot("World Orb").AttachComponent<WorldOrb>();
				setup(obj);
			}
		});
	}

	void ITouchable.OnTouch(in TouchEventInfo eventInfo)
	{
		OnTouch(in eventInfo);
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		SessionStartingUser = new SyncRef<User>();
		URL_Field = new Sync<Uri>();
		ActiveSessionURLs_Field = new SyncFieldList<Uri>();
		CreateIfNotExists_Field = new SyncDelegate<WorldCreator>();
		OpenActionOverride = new SyncDelegate<Action<WorldOrb>>();
		Visit = new Sync<VisitState>();
		ActiveUsers = new Sync<int>();
		RecordStateUpdated = new Sync<bool>();
		RecordStateUpdated.MarkNonPersistent();
		IsPublic = new Sync<bool>();
		CanModify = new Sync<bool>();
		LongPressIndicatorColor = new Sync<colorX>();
		LongPressTime = new Sync<float>();
		_longPressIndicator = new SyncRef<RingMesh>();
		_longPressIndicatorMaterial = new SyncRef<UnlitMaterial>();
		Touched = new SyncDelegate<Action<WorldOrb, TouchEventInfo>>();
		LongPressTriggered = new SyncDelegate<Action<WorldOrb>>();
		_lastFetchedUrl = new Sync<Uri>();
		_isReadOnly = new Sync<bool>();
		_orbRoot = new SyncRef<Slot>();
		_infoRoot = new SyncRef<Slot>();
		_thumbTex = new SyncRef<StaticTexture2D>();
		_thumbMaterial = new SyncRef<Projection360Material>();
		_shellMaterial = new DriveRef<PBS_RimMetallic>();
		_nameText = new SyncRef<TextRenderer>();
		_creatorText = new SyncRef<TextRenderer>();
		_visitsText = new SyncRef<TextRenderer>();
		_usersText = new SyncRef<TextRenderer>();
		_namePosition = new FieldDrive<float3>();
		_creatorPosition = new FieldDrive<float3>();
		_visitsPosition = new FieldDrive<float3>();
		_usersPosition = new FieldDrive<float3>();
		_userCountText = new FieldDrive<string>();
		_sizeDrive = new FieldDrive<float3>();
		_iconSlot = new SyncRef<Slot>();
		_iconTexture = new SyncRef<StaticTexture2D>();
		_iconMaterial = new SyncRef<UnlitMaterial>();
		_iconPosition = new FieldDrive<float3>();
		_sessionStartDialog = new SlotCleanupRef<NewWorldDialog>();
		_lastTouch = new Sync<double>();
		_lastTouch.MarkNonPersistent();
		_lastFlash = new Sync<double>();
		_lastFlash.MarkNonPersistent();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => SessionStartingUser, 
			4 => URL_Field, 
			5 => ActiveSessionURLs_Field, 
			6 => CreateIfNotExists_Field, 
			7 => OpenActionOverride, 
			8 => Visit, 
			9 => ActiveUsers, 
			10 => RecordStateUpdated, 
			11 => IsPublic, 
			12 => CanModify, 
			13 => LongPressIndicatorColor, 
			14 => LongPressTime, 
			15 => _longPressIndicator, 
			16 => _longPressIndicatorMaterial, 
			17 => Touched, 
			18 => LongPressTriggered, 
			19 => _lastFetchedUrl, 
			20 => _isReadOnly, 
			21 => _orbRoot, 
			22 => _infoRoot, 
			23 => _thumbTex, 
			24 => _thumbMaterial, 
			25 => _shellMaterial, 
			26 => _nameText, 
			27 => _creatorText, 
			28 => _visitsText, 
			29 => _usersText, 
			30 => _namePosition, 
			31 => _creatorPosition, 
			32 => _visitsPosition, 
			33 => _usersPosition, 
			34 => _userCountText, 
			35 => _sizeDrive, 
			36 => _iconSlot, 
			37 => _iconTexture, 
			38 => _iconMaterial, 
			39 => _iconPosition, 
			40 => _sessionStartDialog, 
			41 => _lastTouch, 
			42 => _lastFlash, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static WorldOrb __New()
	{
		return new WorldOrb();
	}
}
