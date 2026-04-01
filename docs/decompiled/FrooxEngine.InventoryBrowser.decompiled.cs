using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Elements.Core;
using EnumsNET;
using FrooxEngine.CommonAvatar;
using FrooxEngine.Store;
using FrooxEngine.UIX;
using FrooxEngine.Undo;
using Renderite.Shared;
using SkyFrost.Base;

namespace FrooxEngine;

[GloballyRegistered]
public class InventoryBrowser : BrowserDialog
{
	public enum SpecialItemType
	{
		None,
		Avatar,
		World,
		VirtualKeyboard,
		InteractiveCamera,
		Facet,
		AudioPlayer,
		VideoPlayer,
		TextDisplay,
		UrlDisplay,
		DocumentDisplay,
		AudioStreamController,
		ProgressBar,
		WorldLoadingIndicator,
		ColorDialog
	}

	public readonly SyncDelegate<Action<FrooxEngine.Store.Record>> CustomItemSpawn;

	internal string _changePath;

	internal string _changeOwnerId;

	protected readonly UserRef _user;

	protected readonly Sync<bool> _autoReinitialize;

	protected readonly SyncDelegate<Func<RecordDirectory>> _initFunction;

	protected readonly Sync<string> _currentPath;

	protected readonly Sync<string> _currentOwnerId;

	protected readonly SyncRef<Button> _addNewButton;

	protected readonly SyncRef<Button> _deleteButton;

	protected readonly SyncRef<Button> _inventoriesButton;

	protected readonly SyncRef<Button> _shareButton;

	protected readonly SyncRef<Button> _unshareButton;

	protected readonly SyncRef<Button> _copyLink;

	protected readonly SyncRef<Button> _addCurrentAvatar;

	protected readonly Sync<SpecialItemType> _lastSpecialItemType;

	private string _lastUserId;

	private User _safeUser;

	private InventoryItemUI _removeConfirm;

	private InventoryItemUI _unpublishConfirm;

	private Dictionary<FrooxEngine.Store.Record, InventoryItemUI> _currentItems = new Dictionary<FrooxEngine.Store.Record, InventoryItemUI>();

	public static string INVENTORY_ROOT => "Inventory";

	public static InventoryBrowser CurrentUserspaceInventory { get; internal set; }

	public static colorX DESELECTED_COLOR => RadiantUI_Constants.Neutrals.MID;

	public static colorX SELECTED_COLOR => RadiantUI_Constants.Sub.GREEN;

	public static colorX SELECTED_TEXT => RadiantUI_Constants.Hero.GREEN;

	public static colorX FOLDER_COLOR => RadiantUI_Constants.Sub.YELLOW;

	public static colorX FOLDER_TEXT => RadiantUI_Constants.Hero.YELLOW;

	public static colorX LINK_COLOR => RadiantUI_Constants.Sub.CYAN;

	public static colorX LINK_TEXT => RadiantUI_Constants.Hero.CYAN;

	public static colorX FAVORITE_COLOR => RadiantUI_Constants.Sub.PURPLE;

	public User OwnerUser => _user.Target;

	public bool CanWriteToCurrentDirectory
	{
		get
		{
			RecordDirectory currentDirectory = CurrentDirectory;
			if (currentDirectory != null && currentDirectory.CanWrite)
			{
				return CurrentDirectory?.OwnerId != base.Cloud.Platform.GroupId;
			}
			return false;
		}
	}

	public float ActualDoublePressInterval
	{
		get
		{
			if (!AllowSelect)
			{
				return 0f;
			}
			return InteractionHandler.DoubleClickInterval;
		}
	}

	public string CurrentPath => _currentPath;

	public string CurrentOwnerId => _currentOwnerId;

	public InventoryItemUI SelectedInventoryItem
	{
		get
		{
			return SelectedItem.Target as InventoryItemUI;
		}
		set
		{
			SelectedItem.Target = value;
		}
	}

	public User SafeUser
	{
		set
		{
			_safeUser = value;
		}
	}

	public RecordDirectory CurrentDirectory { get; private set; }

	public override bool CanInteract(User user)
	{
		if (user == _user.Target && user == base.LocalUser)
		{
			if (_safeUser != _user.Target || _safeUser != user || _safeUser == null || !(_safeUser.MachineID == _user.LinkedMachineId) || !(_safeUser.UserID == _user.LinkedCloudId))
			{
				return base.World.IsUserspace();
			}
			return true;
		}
		return false;
	}

	protected override void OnAwake()
	{
		base.OnAwake();
		foreach (FavoriteEntity value in Enums.GetValues<FavoriteEntity>())
		{
			base.Engine.Cloud.Profile.RegisterListener(value, OnFavoriteChanged);
		}
		base.Engine.RecordManager.RecordSaved += RecordManager_RecordSaved;
	}

	private void RecordManager_RecordSaved(FrooxEngine.Store.Record record)
	{
		if (string.IsNullOrWhiteSpace(record.Path) || !record.Path.StartsWith("Inventory"))
		{
			return;
		}
		RecordDirectory currentDirectory = CurrentDirectory;
		if (currentDirectory == null || currentDirectory.OwnerId != record.OwnerId)
		{
			return;
		}
		RecordDirectory rootDirectory = currentDirectory.GetRootDirectory();
		RecordDirectory recordDirectory = ((!(record.Path == "Inventory")) ? rootDirectory.TryGetSubdirectoryAtPath(record.Path.Substring("Inventory\\".Length), createIfNotLoaded: true) : rootDirectory);
		if (recordDirectory == null || recordDirectory.CurrentLoadState == RecordDirectory.LoadState.NotLoaded)
		{
			return;
		}
		Uri.TryCreate(record.AssetURI, UriKind.Absolute, out Uri _);
		Uri.TryCreate(record.ThumbnailURI, UriKind.Absolute, out Uri _);
		recordDirectory.TryAddRecord(record);
		if (recordDirectory == CurrentDirectory)
		{
			RunSynchronously(delegate
			{
				Open(CurrentDirectory, SlideSwapRegion.Slide.None);
			});
		}
	}

	protected override void OnCommonUpdate()
	{
		base.OnCommonUpdate();
		if (_autoReinitialize.Value && _lastUserId != base.Engine.Cloud.CurrentUserID)
		{
			TryInitialize();
		}
		if (!CanInteract(base.LocalUser) || base.World != Userspace.UserspaceWorld)
		{
			return;
		}
		if (base.World.GetLocalUserGrabberWithItems() == null)
		{
			_addNewButton.Target.Label.LocaleContent = "Inventory.CreateDirectory".AsLocaleKey();
		}
		else
		{
			_addNewButton.Target.Label.LocaleContent = "Inventory.SaveHeld".AsLocaleKey();
		}
		if (_changePath != null)
		{
			if (_changePath != _currentPath.Value || _changeOwnerId != _currentOwnerId.Value)
			{
				_currentPath.Value = _changePath;
				_currentOwnerId.Value = _changeOwnerId;
				TryInitialize();
			}
			_changePath = null;
			_changeOwnerId = null;
		}
		else if (this != CurrentUserspaceInventory && CurrentUserspaceInventory != null && CurrentUserspaceInventory._changePath == null && (CurrentUserspaceInventory._currentPath.Value != _currentPath.Value || CurrentUserspaceInventory._currentOwnerId.Value != _currentOwnerId.Value))
		{
			_currentPath.Value = CurrentUserspaceInventory._currentPath.Value;
			_currentOwnerId.Value = CurrentUserspaceInventory._currentOwnerId.Value;
			TryInitialize();
		}
	}

	protected override void OnStart()
	{
		base.OnStart();
		TryInitialize();
	}

	private void TryInitialize()
	{
		if (!CanInteract(base.LocalUser))
		{
			return;
		}
		if (base.LocalUser == OwnerUser && _initFunction.Target != null)
		{
			RecordDirectory root = _initFunction.Target();
			if (string.IsNullOrEmpty(_currentPath.Value))
			{
				RunSynchronously(delegate
				{
					Open(root, SlideSwapRegion.Slide.None);
				});
			}
			else
			{
				StartTask(async delegate
				{
					RecordDirectory recordDirectory = await root.GetSubdirectoryAtPath(_currentPath.Value);
					Open(recordDirectory ?? root, SlideSwapRegion.Slide.None);
				});
			}
		}
		_lastUserId = base.Engine.Cloud.CurrentUserID;
	}

	private void OnFavoriteChanged(Uri obj)
	{
		if (CanInteract(base.LocalUser))
		{
			RunSynchronously(delegate
			{
				ReprocessItems();
			});
		}
	}

	protected override void OnDispose()
	{
		foreach (FavoriteEntity value in Enums.GetValues<FavoriteEntity>())
		{
			base.Engine.Cloud.Profile.UnregisterListener(value, OnFavoriteChanged);
		}
		base.Engine.RecordManager.RecordSaved -= RecordManager_RecordSaved;
		base.OnDispose();
	}

	protected override void OnAttach()
	{
		base.OnAttach();
		_user.Target = base.World.LocalUser;
		OnItemSelected(null, SelectedItem.Target);
	}

	protected override void OnItemSelected(BrowserItem previousItem, BrowserItem currentItem)
	{
		if (!CanInteract(base.LocalUser))
		{
			return;
		}
		InventoryItemUI inventoryItemUI = currentItem as InventoryItemUI;
		SpecialItemType specialItemType = ClassifyItem(inventoryItemUI);
		if (_inventoriesButton.Target != null && _lastSpecialItemType.Value == specialItemType && specialItemType != SpecialItemType.World)
		{
			return;
		}
		_lastSpecialItemType.Value = specialItemType;
		UIBuilder uIBuilder = BeginGenerateToolPanel();
		RadiantUI_Constants.SetupDefaultStyle(uIBuilder);
		if (specialItemType == SpecialItemType.Avatar)
		{
			uIBuilder.Button(OfficialAssets.Graphics.Icons.Dash.Armature, "Inventory.Equip".AsLocaleKey(), OnEquipAvatar);
			uIBuilder.Button(OfficialAssets.Graphics.Icons.Inspector.Pin, "Inventory.Favorite.Avatar".AsLocaleKey(), OnSetFavorite, FavoriteEntity.Avatar);
		}
		bool flag = inventoryItemUI != null && (inventoryItemUI.Item?.OwnerId == base.Cloud.Platform.GroupId || inventoryItemUI.Item?.OwnerId == base.Cloud.CurrentUserID);
		if (specialItemType == SpecialItemType.Facet && flag)
		{
			uIBuilder.Button(OfficialAssets.Graphics.Icons.General.BoxOut, "Inventory.SpawnFacet".AsLocaleKey(), OnSpawnFacet);
		}
		if (specialItemType == SpecialItemType.VirtualKeyboard && flag)
		{
			uIBuilder.Button(OfficialAssets.Graphics.Icons.Inspector.Pin, "Inventory.Favorite.Keyboard".AsLocaleKey(), OnSetFavorite, FavoriteEntity.Keyboard);
		}
		if (specialItemType == SpecialItemType.InteractiveCamera)
		{
			uIBuilder.Button(OfficialAssets.Graphics.Icons.Inspector.Pin, "Inventory.Favorite.InteractiveCamera".AsLocaleKey(), OnSetFavorite, FavoriteEntity.Camera);
		}
		if (specialItemType == SpecialItemType.AudioPlayer)
		{
			uIBuilder.Button(OfficialAssets.Graphics.Icons.Inspector.Pin, "Inventory.Favorite.AudioPlayer".AsLocaleKey(), OnSetFavorite, FavoriteEntity.AudioPlayer);
		}
		if (specialItemType == SpecialItemType.VideoPlayer)
		{
			uIBuilder.Button(OfficialAssets.Graphics.Icons.Inspector.Pin, "Inventory.Favorite.VideoPlayer".AsLocaleKey(), OnSetFavorite, FavoriteEntity.VideoPlayer);
		}
		if (specialItemType == SpecialItemType.TextDisplay)
		{
			uIBuilder.Button(OfficialAssets.Graphics.Icons.Inspector.Pin, "Inventory.Favorite.TextDisplay".AsLocaleKey(), OnSetFavorite, FavoriteEntity.TextDisplay);
		}
		if (specialItemType == SpecialItemType.UrlDisplay)
		{
			uIBuilder.Button(OfficialAssets.Graphics.Icons.Inspector.Pin, "Inventory.Favorite.Hyperlink".AsLocaleKey(), OnSetFavorite, FavoriteEntity.UrlDisplay);
		}
		if (specialItemType == SpecialItemType.DocumentDisplay)
		{
			uIBuilder.Button(OfficialAssets.Graphics.Icons.Inspector.Pin, "Inventory.Favorite.Document".AsLocaleKey(), OnSetFavorite, FavoriteEntity.DocumentDisplay);
		}
		if (specialItemType == SpecialItemType.AudioStreamController)
		{
			uIBuilder.Button(OfficialAssets.Graphics.Icons.Inspector.Pin, "Inventory.Favorite.AudioStreamController".AsLocaleKey(), OnSetFavorite, FavoriteEntity.AudioStreamController);
		}
		if (specialItemType == SpecialItemType.ProgressBar)
		{
			uIBuilder.Button(OfficialAssets.Graphics.Icons.Inspector.Pin, "Inventory.Favorite.ProgressBar".AsLocaleKey(), OnSetFavorite, FavoriteEntity.ProgressBar);
		}
		if (specialItemType == SpecialItemType.WorldLoadingIndicator)
		{
			uIBuilder.Button(OfficialAssets.Graphics.Icons.Inspector.Pin, "Inventory.Favorite.WorldLoadingIndicator".AsLocaleKey(), OnSetFavorite, FavoriteEntity.WorldLoadingIndicator);
		}
		if (specialItemType == SpecialItemType.ColorDialog)
		{
			uIBuilder.Button(OfficialAssets.Graphics.Icons.Inspector.Pin, "Inventory.Favorite.ColorDialog".AsLocaleKey(), OnSetFavorite, FavoriteEntity.ColorDialog);
		}
		if (specialItemType == SpecialItemType.World)
		{
			uIBuilder.Button(OfficialAssets.Graphics.Icons.Dash.WorldUploader, "Inventory.OpenWorld".AsLocaleKey(), OnOpenWorld);
			Uri itemWorldUri = GetItemWorldUri((InventoryItemUI)currentItem);
			if (base.Cloud.Records.ExtractRecordID(itemWorldUri, out var ownerId, out var _) && (ownerId == base.Cloud.CurrentUserID || base.Engine.RecordManager.CanModify(ownerId)))
			{
				uIBuilder.Button(OfficialAssets.Graphics.Icons.Inspector.Pin, "Inventory.Favorite.Home".AsLocaleKey(), OnSetFavorite, FavoriteEntity.Home);
			}
		}
		_inventoriesButton.Target = uIBuilder.Button(OfficialAssets.Graphics.Icons.Dash.HamburgerMenu, "Inventory.Inventories".AsLocaleKey(), ShowInventoryOwners);
		_shareButton.Target = uIBuilder.Button(OfficialAssets.Graphics.Icons.Dash.Contacts, "Inventory.Share".AsLocaleKey(), Share);
		_unshareButton.Target = uIBuilder.Button(OfficialAssets.Graphics.Icons.Dash.FreeformDashOn, "Inventory.Unshare".AsLocaleKey(), Unshare);
		_copyLink.Target = uIBuilder.Button(OfficialAssets.Graphics.Icons.General.Chainlink, "Inventory.GetURL".AsLocaleKey(), CopyURL);
		_deleteButton.Target = uIBuilder.Button(OfficialAssets.Graphics.Icons.Inspector.Destroy, "Inventory.Delete".AsLocaleKey(), DeleteItem);
		_addCurrentAvatar.Target = uIBuilder.Button(OfficialAssets.Graphics.Icons.Dash.Login, "Inventory.SaveAvatar".AsLocaleKey(), AddCurrentAvatar);
		_addNewButton.Target = uIBuilder.Button(OfficialAssets.Graphics.Icons.General.Save, "Inventory.SaveHeld".AsLocaleKey(), AddNew);
	}

	public void OpenDirectoryFromRoot(string path)
	{
		StartTask(async delegate
		{
			RecordDirectory recordDirectory = await CurrentDirectory.GetRootDirectory().GetSubdirectoryAtPath(path);
			if (recordDirectory != null)
			{
				Open(recordDirectory, SlideSwapRegion.Slide.None);
			}
		});
	}

	public void Open(RecordDirectory directory, SlideSwapRegion.Slide slide)
	{
		if (!CanInteract(base.LocalUser))
		{
			return;
		}
		if (directory != null)
		{
			_currentPath.Value = directory.GetRelativePath(includeRoot: false);
			if (base.World == Userspace.UserspaceWorld && this != CurrentUserspaceInventory && CurrentUserspaceInventory != null)
			{
				CurrentUserspaceInventory._changePath = _currentPath.Value;
				CurrentUserspaceInventory._changeOwnerId = _currentOwnerId.Value;
			}
		}
		StartTask(async delegate
		{
			await OpenDirectory(directory, slide).ConfigureAwait(continueOnCapturedContext: false);
		});
	}

	private async Task OpenDirectory(RecordDirectory directory, SlideSwapRegion.Slide slide)
	{
		GridLayout folders;
		GridLayout items;
		UIBuilder ui = BeginGeneratingNewDirectory(directory, out folders, out items, slide);
		if (directory != null)
		{
			if (directory.CurrentLoadState != RecordDirectory.LoadState.FullyLoaded && await directory.TryLocalCacheLoad() && directory == CurrentDirectory)
			{
				UpdateDirectoryItems(ui, folders, items);
			}
			await directory.EnsureFullyLoaded();
			if (directory == CurrentDirectory)
			{
				UpdateDirectoryItems(ui, folders, items);
			}
		}
	}

	private colorX? GetTabColor(FrooxEngine.Store.Record record)
	{
		if (record == null || !record.IsPublic)
		{
			return null;
		}
		return colorX.Cyan.SetA(0.75f);
	}

	private void UpdateDirectoryItems(UIBuilder ui, GridLayout folder, GridLayout items)
	{
		if (ui.Root.IsDestroyed || !CanInteract(base.LocalUser) || CurrentDirectory == null)
		{
			return;
		}
		HideLoadingIndicator();
		RecordDirectory currentDirectory = CurrentDirectory;
		HashSet<InventoryItemUI> hashSet = Pool.BorrowHashSet<InventoryItemUI>();
		foreach (KeyValuePair<FrooxEngine.Store.Record, InventoryItemUI> currentItem in _currentItems)
		{
			hashSet.Add(currentItem.Value);
		}
		ui.NestInto(folder.Slot);
		foreach (RecordDirectory subdirectory in currentDirectory.Subdirectories)
		{
			if (subdirectory.EntryRecord != null && _currentItems.TryGetValue(subdirectory.EntryRecord, out InventoryItemUI value))
			{
				hashSet.Remove(value);
				continue;
			}
			string name = subdirectory.Name;
			colorX? tabColor = GetTabColor(subdirectory.EntryRecord);
			InventoryItemUI inventoryItemUI = GenerateItem<InventoryItemUI>(ui, name, null, null, null, tabColor);
			inventoryItemUI.Directory = subdirectory;
			ProcessItem(inventoryItemUI);
			if (subdirectory.EntryRecord != null)
			{
				_currentItems.Add(subdirectory.EntryRecord, inventoryItemUI);
			}
		}
		ui.NestOut();
		ui.NestInto(items.Slot);
		foreach (FrooxEngine.Store.Record record in currentDirectory.Records)
		{
			if (_currentItems.TryGetValue(record, out InventoryItemUI value2))
			{
				hashSet.Remove(value2);
				continue;
			}
			Uri.TryCreate(record.ThumbnailURI, UriKind.Absolute, out Uri result);
			string name2 = record.Name;
			Uri icon = result;
			colorX? tabColor = GetTabColor(record);
			InventoryItemUI inventoryItemUI2 = GenerateItem<InventoryItemUI>(ui, name2, icon, null, null, tabColor);
			inventoryItemUI2.Item = record;
			ProcessItem(inventoryItemUI2);
			_currentItems.Add(record, inventoryItemUI2);
		}
		ui.NestOut();
		foreach (InventoryItemUI item in hashSet)
		{
			item.Slot.Destroy();
		}
		Pool.Return(ref hashSet);
	}

	private void ReprocessItems()
	{
		if (!CanInteract(base.LocalUser))
		{
			return;
		}
		List<InventoryItemUI> list = Pool.BorrowList<InventoryItemUI>();
		base.Slot.GetComponentsInChildren(list);
		foreach (InventoryItemUI item in list)
		{
			ProcessItem(item);
		}
		Pool.Return(ref list);
	}

	private void ProcessItem(InventoryItemUI item)
	{
		if (!CanInteract(base.LocalUser))
		{
			return;
		}
		Uri uri = item.Item?.GetUrl(base.Cloud.Platform);
		if (uri != null)
		{
			foreach (FavoriteEntity value in Enums.GetValues<FavoriteEntity>())
			{
				if (uri == base.Engine.Cloud.Profile.GetCurrentFavorite(value))
				{
					item.NormalColor.Value = FAVORITE_COLOR;
					item.SelectedColor.Value = FAVORITE_COLOR.MulRGB(2f);
					return;
				}
			}
		}
		if (item.Directory != null)
		{
			item.NormalColor.Value = (item.Directory.IsLink ? LINK_COLOR : FOLDER_COLOR);
			item.NormalText.Value = (item.Directory.IsLink ? LINK_TEXT : FOLDER_TEXT);
			item.SelectedColor.Value = SELECTED_COLOR;
			item.SelectedText.Value = SELECTED_TEXT;
		}
		else
		{
			item.NormalColor.Value = DESELECTED_COLOR;
			item.SelectedColor.Value = SELECTED_COLOR;
		}
	}

	private UIBuilder BeginGeneratingNewDirectory(RecordDirectory directory, out GridLayout folders, out GridLayout items, SlideSwapRegion.Slide slide)
	{
		_currentItems.Clear();
		CurrentDirectory = directory;
		SetPath((from d in CurrentDirectory?.GetChainFromRoot()
			select d.Name).ToList());
		UIBuilder uIBuilder = GenerateContent(slide, out folders, out items, CurrentDirectory != null);
		uIBuilder.Style.ButtonTextAlignment = Alignment.MiddleLeft;
		uIBuilder.Style.ButtonTextPadding = 4f;
		uIBuilder.NestInto(folders.Slot);
		if (directory?.ParentDirectory != null)
		{
			GenerateBackButton(uIBuilder, "General.Back".AsLocaleKey());
		}
		if (CurrentDirectory == null)
		{
			uIBuilder.Button((LocaleString)"Personal", new colorX?(FOLDER_COLOR), OpenInventory, base.Engine.Cloud.CurrentUserID, ActualDoublePressInterval).Label.Color.Value = FOLDER_TEXT;
			foreach (Membership currentUserMembership in base.Engine.Cloud.Groups.CurrentUserMemberships)
			{
				uIBuilder.Button((LocaleString)currentUserMembership.GroupName, new colorX?(LINK_COLOR), OpenInventory, currentUserMembership.GroupId, ActualDoublePressInterval).Label.Color.Value = LINK_TEXT;
			}
		}
		uIBuilder.NestOut();
		return uIBuilder;
	}

	protected override void OnChanges()
	{
		base.OnChanges();
		if (CanInteract(base.LocalUser) && _addNewButton.Target != null)
		{
			_addNewButton.Target.Enabled = CurrentDirectory?.CanWrite ?? false;
			_deleteButton.Target.Enabled = CurrentDirectory?.CanWrite ?? false;
			_inventoriesButton.Target.Enabled = base.Engine.Cloud.CurrentUser != null;
			_shareButton.Target.Enabled = SelectedInventoryItem?.Directory != null;
			_addCurrentAvatar.Target.Enabled = CurrentDirectory?.CanWrite ?? false;
			_copyLink.Target.Enabled = SelectedInventoryItem != null;
			bool enabled = SelectedInventoryItem?.Item?.IsPublic ?? (SelectedInventoryItem?.Directory?.DirectoryRecord?.IsPublic == true);
			_unshareButton.Target.Enabled = enabled;
		}
	}

	protected override string GetSelectedText()
	{
		string text = SelectedInventoryItem?.ItemName;
		if (text != null)
		{
			if (_removeConfirm == SelectedInventoryItem)
			{
				return "<color=#ff7676>DELETE</color> " + text + "?";
			}
			if (_unpublishConfirm == SelectedInventoryItem)
			{
				return "<color=#ff7676>UNPUBLISH</color> " + text + "?";
			}
			if (SelectedInventoryItem?.Item?.AssetManifest != null)
			{
				long num = SelectedInventoryItem.Item.AssetManifest.Sum((DBAsset a) => a.Bytes);
				text = text + " (" + UnitFormatting.FormatBytes(num) + ")";
			}
			return text;
		}
		return "---";
	}

	public void SaveItemFromGrabber(Grabber grabber)
	{
		if (grabber.IsHoldingInteractionBlock<IGrabbableSaveBlock>())
		{
			NotificationMessage.SpawnTextMessage("Permissions.SavingDisabledForItem".AsLocaleKey(), colorX.Red);
		}
		else if (grabber.LocalExternallyHeldItem != null)
		{
			StartTask(async delegate
			{
				await AddItem(grabber.LocalExternallyHeldItem, saveRoot: true, CurrentDirectory);
			});
		}
		else
		{
			StartTask(async delegate
			{
				await AddItem(grabber.HolderSlot, saveRoot: false, CurrentDirectory);
			});
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void AddNew(IButton button, ButtonEventData eventData)
	{
		if (CanInteract(base.LocalUser))
		{
			Grabber localUserGrabberWithItems = base.World.GetLocalUserGrabberWithItems(eventData.source.Slot);
			if (localUserGrabberWithItems != null)
			{
				SaveItemFromGrabber(localUserGrabberWithItems);
				return;
			}
			Slot slot = base.LocalUserSpace.AddSlot("Directory Create Dialog");
			slot.AttachComponent<BrowserCreateDirectoryDialog>().Setup(this, CreateDirectory);
			slot.PositionInFrontOfUser(float3.Backward, null, 0.6f);
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void Unshare(IButton button, ButtonEventData eventData)
	{
		if (!CanInteract(base.LocalUser) || SelectedInventoryItem == null || SelectedInventoryItem.Directory == null)
		{
			return;
		}
		if (_unpublishConfirm == SelectedInventoryItem)
		{
			if (SelectedInventoryItem.Item != null)
			{
				SelectedInventoryItem.Item.IsPublic = false;
				base.Engine.RecordManager.SaveRecord(SelectedInventoryItem.Item);
			}
			else
			{
				SelectedInventoryItem.Directory.SetPublicRecursively(publicState: false);
			}
			RunInSeconds(2f, delegate
			{
				Open(CurrentDirectory, SlideSwapRegion.Slide.None);
			});
			return;
		}
		_unpublishConfirm = SelectedInventoryItem;
		MarkChangeDirty();
		InventoryItemUI _cancelConfirm = _unpublishConfirm;
		RunInSeconds(2f, delegate
		{
			if (_cancelConfirm == _unpublishConfirm)
			{
				_unpublishConfirm = null;
				MarkChangeDirty();
			}
		});
	}

	[SyncMethod(typeof(Delegate), null)]
	private void CopyURL(IButton button, ButtonEventData eventData)
	{
		if (CanInteract(base.LocalUser) && SelectedInventoryItem != null && SelectedInventoryItem.Item != null)
		{
			Uri url = SelectedInventoryItem.Item.GetUrl(base.Cloud.Platform);
			base.InputInterface.Clipboard.SetText(url.ToString());
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void Share(IButton button, ButtonEventData eventData)
	{
		if (!CanInteract(base.LocalUser) || SelectedInventoryItem == null || SelectedInventoryItem.Directory == null)
		{
			return;
		}
		RecordDirectory dir = SelectedInventoryItem.Directory;
		if (!dir.IsLink)
		{
			dir.SetPublicRecursively(publicState: true);
		}
		World world = base.Engine.WorldManager.FocusedWorld;
		world.RunSynchronously(delegate
		{
			if (!world.CanSpawnObjects())
			{
				NotificationMessage.SpawnTextMessage("Permissions.NotAllowedToSpawn".AsLocaleKey(), colorX.Red);
			}
			else
			{
				Slot slot = world.LocalUser.Root.Slot.Parent.AddSlot("Inventory link to " + dir.Name);
				InventoryLink inventoryLink = slot.AttachComponent<InventoryLink>();
				inventoryLink.TargetName.Value = dir.Name;
				inventoryLink.Target.Value = (dir.IsLink ? new Uri(dir.LinkRecord.AssetURI) : dir.EntryRecord.GetUrl(base.Cloud.Platform));
				slot.PositionInFrontOfUser(float3.Backward);
			}
		});
	}

	[SyncMethod(typeof(Delegate), null)]
	private void DeleteItem(IButton button, ButtonEventData eventData)
	{
		if (!CanInteract(base.LocalUser) || SelectedInventoryItem == null)
		{
			return;
		}
		if (_removeConfirm == SelectedInventoryItem)
		{
			if (SelectedInventoryItem.Item != null)
			{
				CurrentDirectory.DeleteItem(SelectedInventoryItem.Item);
			}
			else
			{
				CurrentDirectory.DeleteSubdirectory(SelectedInventoryItem.Directory);
			}
			Open(CurrentDirectory, SlideSwapRegion.Slide.None);
			return;
		}
		_removeConfirm = SelectedInventoryItem;
		MarkChangeDirty();
		InventoryItemUI _cancelConfirm = _removeConfirm;
		RunInSeconds(2f, delegate
		{
			if (_cancelConfirm == _removeConfirm)
			{
				_removeConfirm = null;
				MarkChangeDirty();
			}
		});
	}

	private IEnumerator<Context> SaveActiveAvatar(RecordDirectory directory)
	{
		World focusedWorld = base.Engine.WorldManager.FocusedWorld;
		if (!focusedWorld.CanSaveItems())
		{
			NotificationMessage.SpawnTextMessage("Permissions.NotAllowedToSave".AsLocaleKey(), colorX.Red);
			yield break;
		}
		Task<ItemHelper.SavedItem> itemTask = null;
		focusedWorld.RunSynchronously(delegate
		{
			UserRoot root = focusedWorld.LocalUser.Root;
			AvatarManager avatarManager = root.GetRegisteredComponent<AvatarManager>();
			Slot slot = root.GetRegisteredComponent((AvatarObjectSlot s) => s.Node.Value == BodyNode.Head && s.HasEquipped).Equipped.Target.Slot;
			Slot slot2 = focusedWorld.AddSlot("Dummy Head");
			slot2.PersistentSelf = false;
			slot2.AttachComponent<AvatarPoseNode>().Node.Value = BodyNode.Head;
			slot2.AttachComponent<AvatarDestroyOnDequip>();
			avatarManager.Equip(slot2);
			Slot avatarRoot = slot.GetObjectRoot();
			itemTask = ItemHelper.SaveItem(avatarRoot);
			itemTask.ContinueWith(delegate
			{
				focusedWorld.RunSynchronously(delegate
				{
					avatarManager.Equip(avatarRoot);
				});
			});
		});
		while (itemTask == null)
		{
			yield return Context.WaitForNextUpdate();
		}
		yield return Context.WaitFor(itemTask);
		ItemHelper.SavedItem result = itemTask.Result;
		directory.AddItem(result.Name, result.Asset, result.Thumbnail, result.Tags);
		RunSynchronously(delegate
		{
			Open(CurrentDirectory, SlideSwapRegion.Slide.None);
		});
	}

	public bool AddItem(Slot itemRoot, bool saveRoot)
	{
		if (CanWriteToCurrentDirectory)
		{
			StartTask(async delegate
			{
				await AddItem(itemRoot, saveRoot, CurrentDirectory).ConfigureAwait(continueOnCapturedContext: false);
			});
			return true;
		}
		return false;
	}

	private async Task AddItem(Slot itemRoot, bool saveRoot, RecordDirectory directory)
	{
		if (!CanInteract(base.LocalUser))
		{
			return;
		}
		if (!itemRoot.World.CanSaveItems())
		{
			NotificationMessage.SpawnTextMessage("Permissions.NotAllowedToSave".AsLocaleKey(), colorX.Red);
			return;
		}
		InventoryLink componentInChildren = itemRoot.GetComponentInChildren((InventoryLink l) => l.Enabled);
		WorldOrbSaver worldSaver = itemRoot.GetComponentInChildren<WorldOrbSaver>();
		if (componentInChildren != null)
		{
			await directory.AddLinkAsync(componentInChildren.TargetName, componentInChildren.Target);
		}
		else if (worldSaver != null)
		{
			WorldOrb orb = await worldSaver.StartGlobalTask(async delegate
			{
				await default(ToWorld);
				return await worldSaver.Save(directory.OwnerId, null).ConfigureAwait(continueOnCapturedContext: false);
			}).ConfigureAwait(continueOnCapturedContext: false);
			while (!orb.ThumbnailLoaded)
			{
				await Task.Delay(TimeSpan.FromSeconds(0.20000000298023224)).ConfigureAwait(continueOnCapturedContext: false);
			}
			await Task.Delay(TimeSpan.FromSeconds(0.5)).ConfigureAwait(continueOnCapturedContext: false);
			ItemHelper.SavedItem item = await ItemHelper.SaveItem(orb.Slot).ConfigureAwait(continueOnCapturedContext: false);
			if (item == null)
			{
				return;
			}
			if (!(await directory.TryLocalCacheLoad().ConfigureAwait(continueOnCapturedContext: false)))
			{
				await directory.EnsureFullyLoaded().ConfigureAwait(continueOnCapturedContext: false);
			}
			directory.AddItem(orb.WorldName, item.Asset, item.Thumbnail, item.Tags);
			orb.World.RunSynchronously(delegate
			{
				orb.AnimatedDestroy();
			});
		}
		else
		{
			ItemHelper.SavedItem item = await ItemHelper.SaveItem(itemRoot, saveRoot).ConfigureAwait(continueOnCapturedContext: false);
			if (item == null)
			{
				return;
			}
			if (!(await directory.TryLocalCacheLoad().ConfigureAwait(continueOnCapturedContext: false)))
			{
				await directory.EnsureFullyLoaded().ConfigureAwait(continueOnCapturedContext: false);
			}
			directory.AddItem(item.Name, item.Asset, item.Thumbnail, item.Tags);
		}
		RunSynchronously(delegate
		{
			Open(CurrentDirectory, SlideSwapRegion.Slide.None);
		});
	}

	protected override void GoUp(int levels)
	{
		if (!CanInteract(base.LocalUser) || levels == 0 || CurrentDirectory == null)
		{
			return;
		}
		RecordDirectory recordDirectory = CurrentDirectory;
		for (int i = 0; i < levels; i++)
		{
			if (recordDirectory.ParentDirectory != null)
			{
				recordDirectory = recordDirectory.ParentDirectory;
			}
		}
		if (CurrentDirectory.ParentDirectory != null)
		{
			Open(recordDirectory, SlideSwapRegion.Slide.Right);
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void AddCurrentAvatar(IButton button, ButtonEventData eventData)
	{
		if (CanInteract(base.LocalUser))
		{
			StartCoroutine(SaveActiveAvatar(CurrentDirectory));
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void ShowInventoryOwners(IButton button, ButtonEventData eventData)
	{
		if (CanInteract(base.LocalUser))
		{
			Open(null, SlideSwapRegion.Slide.Right);
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OpenInventory(IButton button, ButtonEventData eventData, string ownerId)
	{
		if (CanInteract(base.LocalUser))
		{
			_currentOwnerId.Value = ownerId;
			RecordDirectory directory = new RecordDirectory(ownerId, "Inventory", base.Engine);
			Open(directory, SlideSwapRegion.Slide.Left);
		}
	}

	internal void Spawn(FrooxEngine.Store.Record record)
	{
		if (CanInteract(base.LocalUser) && Uri.TryCreate(record.AssetURI, UriKind.Absolute, out Uri _))
		{
			if (CustomItemSpawn.Target != null)
			{
				CustomItemSpawn.Target(record);
				return;
			}
			World focusedWorld = base.Engine.WorldManager.FocusedWorld;
			SpawnItem(focusedWorld, record);
		}
	}

	private void SpawnItem(World world, FrooxEngine.Store.Record record)
	{
		if (!CanInteract(base.LocalUser) || record == null)
		{
			return;
		}
		world.RunSynchronously(delegate
		{
			if (!world.CanSpawnObjects())
			{
				NotificationMessage.SpawnTextMessage("Permissions.NotAllowedToSpawn".AsLocaleKey(), colorX.Red);
			}
			else
			{
				Slot s = world.RootSlot.LocalUserSpace.AddSlot("InventorySpawn");
				s.StartTask(async delegate
				{
					await default(ToWorld);
					await s.LoadObjectAsync(record);
					List<Slot> list = Pool.BorrowList<Slot>();
					s.PositionInFrontOfUser(null, float3.Down * 0.2f, 0.5f);
					s = s.GetComponent<InventoryItem>()?.Unpack(keepExistingPosition: false, list) ?? s;
					s.World.BeginUndoBatch("Undo.Spawn".AsLocaleKey(("name", s.Name_Field)));
					if (list.Count > 0)
					{
						foreach (Slot item in list)
						{
							item.CreateSpawnUndoPoint();
						}
					}
					else
					{
						s.CreateSpawnUndoPoint();
					}
					s.World.EndUndoBatch();
					Pool.Return(ref list);
				});
			}
		});
	}

	[SyncMethod(typeof(BrowserCreateDirectoryDialog.CreateHandler), new string[] { })]
	private bool CreateDirectory(string name, out string error)
	{
		if (!CanInteract(base.LocalUser))
		{
			error = "Not allowed";
			return false;
		}
		if (CurrentDirectory.GetSubdirectory(name) != null)
		{
			error = "Directory with given name already exists";
			return false;
		}
		RecordDirectory directory = CurrentDirectory.AddSubdirectory(name);
		Open(directory, SlideSwapRegion.Slide.Left);
		error = null;
		return true;
	}

	public static SpecialItemType ClassifyItem(InventoryItemUI itemui)
	{
		if (itemui?.Item?.Tags == null)
		{
			return SpecialItemType.None;
		}
		if (itemui.Item.Tags.Contains(RecordTags.CommonAvatar))
		{
			return SpecialItemType.Avatar;
		}
		if (itemui.Item.Tags.Contains(RecordTags.Facet))
		{
			return SpecialItemType.Facet;
		}
		if (itemui.Item.Tags.Contains(RecordTags.WorldOrb))
		{
			return SpecialItemType.World;
		}
		if (itemui.Item.Tags.Contains(RecordTags.VirtualKeyboard))
		{
			return SpecialItemType.VirtualKeyboard;
		}
		if (itemui.Item.Tags.Contains(RecordTags.InteractiveCamera))
		{
			return SpecialItemType.InteractiveCamera;
		}
		if (itemui.Item.Tags.Contains(RecordTags.AudioPlayer))
		{
			return SpecialItemType.AudioPlayer;
		}
		if (itemui.Item.Tags.Contains(RecordTags.VideoPlayer))
		{
			return SpecialItemType.VideoPlayer;
		}
		if (itemui.Item.Tags.Contains(RecordTags.TextDisplay))
		{
			return SpecialItemType.TextDisplay;
		}
		if (itemui.Item.Tags.Contains(RecordTags.UrlDisplay))
		{
			return SpecialItemType.UrlDisplay;
		}
		if (itemui.Item.Tags.Contains(RecordTags.DocumentDisplay))
		{
			return SpecialItemType.DocumentDisplay;
		}
		if (itemui.Item.Tags.Contains(RecordTags.AudioStreamInterface))
		{
			return SpecialItemType.AudioStreamController;
		}
		if (itemui.Item.Tags.Contains(RecordTags.ProgressBar))
		{
			return SpecialItemType.ProgressBar;
		}
		if (itemui.Item.Tags.Contains(RecordTags.WorldLoadingProgress))
		{
			return SpecialItemType.WorldLoadingIndicator;
		}
		if (itemui.Item.Tags.Contains(RecordTags.ColorDialog))
		{
			return SpecialItemType.ColorDialog;
		}
		return SpecialItemType.None;
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnSpawnFacet(IButton button, ButtonEventData eventData)
	{
		if (CanInteract(base.LocalUser))
		{
			SpawnItem(base.World, SelectedInventoryItem.Item);
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnEquipAvatar(IButton button, ButtonEventData eventData)
	{
		if (CanInteract(base.LocalUser))
		{
			base.Engine.WorldManager.FocusedWorld?.TryEquipAvatar(SelectedInventoryItem.Item);
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnSetFavorite(IButton button, ButtonEventData eventData, FavoriteEntity entity)
	{
		if (CanInteract(base.LocalUser))
		{
			Uri uri = ((ClassifyItem(SelectedInventoryItem) != SpecialItemType.World) ? SelectedInventoryItem.Item.GetUrl(base.Cloud.Platform) : GetItemWorldUri(SelectedInventoryItem));
			if (base.Engine.Cloud.Profile.GetCurrentFavorite(entity) == uri)
			{
				base.Engine.Cloud.Profile.SetFavorite(entity, null);
			}
			else
			{
				base.Engine.Cloud.Profile.SetFavorite(entity, uri);
			}
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnOpenWorld(IButton button, ButtonEventData eventData)
	{
		if (CanInteract(base.LocalUser))
		{
			string correspondingWorldUrl = RecordTags.GetCorrespondingWorldUrl(SelectedInventoryItem.Item.Tags);
			if (Uri.IsWellFormedUriString(correspondingWorldUrl, UriKind.Absolute))
			{
				Userspace.OpenWorld(new WorldStartSettings(new Uri(correspondingWorldUrl))
				{
					GetExisting = true,
					FetchedWorldName = SelectedInventoryItem.Item.Name
				});
			}
		}
	}

	private Uri GetItemWorldUri(InventoryItemUI inventoryItem)
	{
		if (Uri.TryCreate(RecordTags.GetCorrespondingWorldUrl(inventoryItem.Item.Tags), UriKind.Absolute, out Uri result))
		{
			return result;
		}
		return null;
	}

	public void OpenDefault()
	{
		_autoReinitialize.Value = true;
		_initFunction.Target = GetDefaultAuto;
	}

	[SyncMethod(typeof(Delegate), null)]
	private RecordDirectory GetDefaultAuto()
	{
		if (base.Engine.Cloud.Session.CurrentUser != null)
		{
			return GetCurrentUserRoot();
		}
		return GetAnonymousRoot();
	}

	private RecordDirectory GetCurrentUserRoot()
	{
		if (string.IsNullOrEmpty(_currentOwnerId.Value) || _currentOwnerId.Value == base.Engine.Cloud.CurrentUserID)
		{
			return base.Engine.Cloud.InventoryRootDirectory;
		}
		return new RecordDirectory(_currentOwnerId.Value, "Inventory", base.Engine);
	}

	private RecordDirectory GetAnonymousRoot()
	{
		List<RecordDirectory> list = new List<RecordDirectory>();
		list.Add(new RecordDirectory(base.Cloud.Platform.GroupId, "Inventory\\" + base.Cloud.Platform.Name + " Essentials", base.Engine));
		return new RecordDirectory(base.Engine, list, null);
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		CustomItemSpawn = new SyncDelegate<Action<FrooxEngine.Store.Record>>();
		_user = new UserRef();
		_autoReinitialize = new Sync<bool>();
		_initFunction = new SyncDelegate<Func<RecordDirectory>>();
		_currentPath = new Sync<string>();
		_currentOwnerId = new Sync<string>();
		_addNewButton = new SyncRef<Button>();
		_deleteButton = new SyncRef<Button>();
		_inventoriesButton = new SyncRef<Button>();
		_shareButton = new SyncRef<Button>();
		_unshareButton = new SyncRef<Button>();
		_copyLink = new SyncRef<Button>();
		_addCurrentAvatar = new SyncRef<Button>();
		_lastSpecialItemType = new Sync<SpecialItemType>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => SelectedItem, 
			4 => _previousSelectedItem, 
			5 => AllowSelect, 
			6 => ItemSize, 
			7 => _selectedText, 
			8 => _pathRoot, 
			9 => _buttonsRoot, 
			10 => _folderGrid, 
			11 => _itemGrid, 
			12 => _tabSprite, 
			13 => _loadingIndicator, 
			14 => _swapper, 
			15 => CustomItemSpawn, 
			16 => _user, 
			17 => _autoReinitialize, 
			18 => _initFunction, 
			19 => _currentPath, 
			20 => _currentOwnerId, 
			21 => _addNewButton, 
			22 => _deleteButton, 
			23 => _inventoriesButton, 
			24 => _shareButton, 
			25 => _unshareButton, 
			26 => _copyLink, 
			27 => _addCurrentAvatar, 
			28 => _lastSpecialItemType, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static InventoryBrowser __New()
	{
		return new InventoryBrowser();
	}
}
