using System;
using System.Collections.Generic;
using Elements.Core;
using EnumsNET;
using FrooxEngine.Store;
using FrooxEngine.UIX;
using Renderite.Shared;
using SkyFrost.Base;

namespace FrooxEngine;

[ExceptionHandling(ExceptionAction.DestroySlot)]
public class SessionControlDialog : Component
{
	public enum Tab
	{
		Settings,
		Users,
		Permissions
	}

	public readonly Sync<Tab> ActiveTab;

	protected readonly SyncRef<Slot> _contentRoot;

	protected readonly SyncRef<TextField> _worldName;

	protected readonly SyncRef<IntTextEditorParser> _maxUsers;

	protected readonly SyncRef<Checkbox> _awayKickEnabled;

	protected readonly SyncRef<FloatTextEditorParser> _awayKickMinutes;

	protected readonly SyncRef<Checkbox> _autosaveEnabled;

	protected readonly SyncRef<FloatTextEditorParser> _autosaveMinutes;

	protected readonly SyncRef<Checkbox> _autocleanEnabled;

	protected readonly SyncRef<FloatTextEditorParser> _autocleanMinutes;

	protected readonly SyncRef<Checkbox> _mobileFriendly;

	protected readonly SyncRef<Checkbox> _hideFromListing;

	protected readonly SyncRef<TextField> _description;

	protected readonly SyncRef<Button> _worldNameButton;

	protected readonly SyncRef<Button> _descriptionButton;

	protected readonly SyncRef<Button> _maxUsersButton;

	protected readonly SyncRef<Button> _awayKickEnabledButton;

	protected readonly SyncRef<Button> _awayKickMinutesButton;

	protected readonly SyncRef<Button> _autosaveEnabledButton;

	protected readonly SyncRef<Button> _autosaveMinutesButton;

	protected readonly SyncRef<Button> _autocleanEnabledButton;

	protected readonly SyncRef<Button> _autocleanMinutesButton;

	protected readonly SyncRef<Button> _mobileFriendlyButton;

	protected readonly SyncRef<Button> _hideFromListingButton;

	protected readonly SyncRef<Text> _permissionOverridesIndicator;

	protected readonly SyncRef<Button> _permissionOverridesButton;

	protected readonly SyncRef<Button> _getSessionOrb;

	protected readonly SyncRef<Button> _getWorldOrb;

	protected readonly SyncRef<Button> _editMode;

	protected readonly SyncRef<Button> _copySessionURL;

	protected readonly SyncRef<Button> _copyWorldURL;

	protected readonly SyncRef<Button> _copyRecordURL;

	protected readonly SyncRefList<Radio> _accessLevelRadios;

	protected readonly SyncRefList<Button> _accessLevelRadiosButtons;

	protected readonly SyncRef<WorldValueSync<string>> _worldNameSync;

	protected readonly SyncRef<WorldValueSync<string>> _descriptionSync;

	protected readonly SyncRef<WorldValueSync<int>> _maxUsersSync;

	protected readonly SyncRef<WorldValueSync<bool>> _awayKickEnabledSync;

	protected readonly SyncRef<WorldValueSync<float>> _awayKickMinutesSync;

	protected readonly SyncRef<WorldValueSync<bool>> _autosaveEnabledSync;

	protected readonly SyncRef<WorldValueSync<float>> _autosaveMinutesSync;

	protected readonly SyncRef<WorldValueSync<bool>> _autocleanEnabledSync;

	protected readonly SyncRef<WorldValueSync<float>> _autocleanSecondsSync;

	protected readonly SyncRef<WorldValueSync<bool>> _mobileFriendlySync;

	protected readonly SyncRef<WorldValueSync<bool>> _hideFromListingSync;

	protected readonly SyncRef<WorldValueSync<bool>> _editModeSync;

	protected readonly SyncRef<WorldValueSync<SessionAccessLevel>> _accessLevelSync;

	protected readonly SyncRef<Text> _customVerifierLabel;

	protected readonly SyncRef<Checkbox> _customVerifierCheckbox;

	protected readonly SyncRef<Button> _customVerifierButton;

	protected readonly SyncRef<WorldValueSync<bool>> _customVerifierSync;

	protected readonly SyncRef<Slot> _uiContentRoot;

	protected readonly SyncRef<SlideSwapRegion> _slideSwap;

	protected readonly SyncRef<Button> _saveWorld;

	protected readonly SyncRef<Button> _saveWorldAs;

	protected readonly SyncRef<Button> _saveWorldCopy;

	protected readonly SyncRef<Button> _enableResoniteLink;

	protected readonly SyncRef<Text> _resoniteLinkPort;

	protected readonly SyncRefList<Button> _tabButtons;

	private World _lastWorld;

	private Dictionary<User, SessionUserController> _currentUserControllers = new Dictionary<User, SessionUserController>();

	private Dictionary<User, SessionPermissionController> _currentPermissionControllers = new Dictionary<User, SessionPermissionController>();

	public override bool UserspaceOnly => true;

	public string PermissionOverrides(int overrides)
	{
		return this.GetLocalized("Session.Permission.PermissionOverrideCount", null, "n", overrides);
	}

	protected override void OnAwake()
	{
		base.OnAwake();
		ActiveTab.Value = Tab.Settings;
	}

	protected override void OnAttach()
	{
		base.Slot.AttachComponent<DuplicateBlock>();
		base.OnAttach();
		UIBuilder uIBuilder = new UIBuilder(base.Slot);
		RadiantUI_Constants.SetupDefaultStyle(uIBuilder);
		uIBuilder.HorizontalHeader(36f, out RectTransform header, out RectTransform content);
		header.AddFixedPadding(4f, 0f, 0f, 0f);
		uIBuilder = new UIBuilder(header);
		RadiantUI_Constants.SetupDefaultStyle(uIBuilder);
		uIBuilder.HorizontalLayout(4f);
		foreach (Tab value in Enum.GetValues(typeof(Tab)))
		{
			Button target = uIBuilder.Button(("Session.Tab." + value).AsLocaleKey(), new colorX?(RadiantUI_Constants.TAB_INACTIVE_BACKGROUND_COLOR), SwitchTab, value);
			_tabButtons.Add(target);
		}
		_contentRoot.Target = content.Slot;
		_slideSwap.Target = content.Slot.AttachComponent<SlideSwapRegion>();
		GenerateUi(Tab.Settings);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void SwitchTab(IButton button, ButtonEventData eventData, Tab tab)
	{
		if (ActiveTab.Value != tab)
		{
			GenerateUi(tab);
		}
	}

	private void GenerateUi(Tab tab)
	{
		_currentPermissionControllers.Clear();
		_currentUserControllers.Clear();
		_accessLevelRadios.Clear();
		_accessLevelRadiosButtons.Clear();
		int num = tab.CompareTo(ActiveTab.Value);
		SlideSwapRegion.Slide slide = ((num < 0) ? SlideSwapRegion.Slide.Right : ((num > 0) ? SlideSwapRegion.Slide.Left : SlideSwapRegion.Slide.None));
		UIBuilder ui = _slideSwap.Target.Swap(slide);
		RadiantUI_Constants.SetupDefaultStyle(ui);
		ActiveTab.Value = tab;
		switch (tab)
		{
		case Tab.Users:
		case Tab.Permissions:
			ui.ScrollArea(Alignment.MiddleCenter);
			ui.VerticalLayout(4f);
			ui.FitContent(SizeFit.Disabled, SizeFit.MinSize);
			if (tab == Tab.Permissions)
			{
				SessionPermissionController.Create(() => FocusedWorldPermission((PermissionController p) => p.DefaultAnonymousRole), ui, "Session.Permission.Anonymous".AsLocaleKey());
				SessionPermissionController.Create(() => FocusedWorldPermission((PermissionController p) => p.DefaultVisitorRole), ui, "Session.Permission.Vistor".AsLocaleKey());
				SessionPermissionController.Create(() => FocusedWorldPermission((PermissionController p) => p.DefaultContactRole), ui, "Session.Permission.Contact".AsLocaleKey());
				SessionPermissionController.Create(() => FocusedWorldPermission((PermissionController p) => p.DefaultHostRole), ui, "Session.Permission.Host".AsLocaleKey());
				ui.Panel();
				ui.SplitHorizontally(0.6f, out RectTransform left, out RectTransform right);
				ui.NestInto(left);
				_permissionOverridesIndicator.Target = ui.Text((LocaleString)PermissionOverrides(-1));
				ui.NestOut();
				ui.NestInto(right);
				_permissionOverridesButton.Target = ui.Button("Session.Permission.ClearOverrides".AsLocaleKey(), OnClearUserPermissionOverrides);
				ui.NestOut();
				ui.NestOut();
				ui.Style.MinHeight *= 0.25f;
				ui.Panel(colorX.Black);
				ui.NestOut();
				ui.Style.MinHeight *= 4f;
			}
			_uiContentRoot.Target = ui.Root;
			break;
		case Tab.Settings:
		{
			_lastWorld = null;
			List<RectTransform> list = ui.SplitHorizontally(0.4f, 0.05f, 0.6f);
			ui = new UIBuilder(list[0]);
			RadiantUI_Constants.SetupDefaultStyle(ui);
			ui.VerticalLayout(4f);
			ui.FitContent(SizeFit.Disabled, SizeFit.PreferredSize);
			ui.Style.MinHeight = 24f;
			ui.Style.PreferredHeight = 24f;
			ui.Text("World.Config.Name".AsLocaleKey());
			_worldName.Target = ui.TextField();
			_worldNameButton.Target = _worldName.Target.Slot.GetComponent<Button>();
			_worldNameSync.Target = _worldName.Target.Text.Content.SyncWithWorld(null);
			_maxUsers.Target = ui.HorizontalElementWithLabel("World.Config.MaxUsers".AsLocaleKey(), 0.75f, () => ui.IntegerField(1, 255, 1, parseContinuously: false));
			_maxUsersButton.Target = _maxUsers.Target.Slot.GetComponent<Button>();
			_maxUsersSync.Target = _maxUsers.Target.ParsedValue.SyncWithWorld(null);
			_mobileFriendly.Target = ui.Checkbox("World.Config.MobileFriendly".AsLocaleKey());
			_mobileFriendlyButton.Target = _mobileFriendly.Target.Slot.GetComponent<Button>();
			_mobileFriendlySync.Target = _mobileFriendly.Target.State.SyncWithWorld(null);
			_getSessionOrb.Target = ui.Button("World.Actions.GetSessionOrb".AsLocaleKey(), GetSessionOrb);
			_getWorldOrb.Target = ui.Button("World.Actions.GetWorldOrb".AsLocaleKey(), GetWorldOrb);
			_copySessionURL.Target = ui.Button("World.Actions.CopySessionURL".AsLocaleKey(), CopySessionURL);
			_copyWorldURL.Target = ui.Button("World.Actions.CopyWorldURL".AsLocaleKey(), CopyWorldURL);
			_copyRecordURL.Target = ui.Button("World.Actions.CopyRecordURL".AsLocaleKey(), CopyRecordURL);
			ui.Text("World.Config.Description".AsLocaleKey());
			ui.PushStyle();
			ui.Style.TextAutoSizeMax = 24f;
			ui.Style.MinHeight = 72f;
			ui.Style.PreferredHeight = 72f;
			_description.Target = ui.TextField();
			ui.PopStyle();
			_descriptionButton.Target = _description.Target.Slot.GetComponent<Button>();
			_descriptionSync.Target = _description.Target.Text.Content.SyncWithWorld(null);
			ui.PushStyle();
			ui.Text("World.Config.SaveOptionsHeader".AsLocaleKey());
			ui.Style.MinHeight *= 1.5f;
			ui.Style.PreferredHeight *= 1.5f;
			ui.Style.ButtonSpriteColor = colorX.White;
			_saveWorld.Target = ui.Button(OfficialAssets.Graphics.Icons.Dash.SaveWorld, "World.Actions.Save".AsLocaleKey(), OnSave);
			_saveWorldAs.Target = ui.Button(OfficialAssets.Graphics.Icons.Dash.SaveWorldAs, "World.Actions.SaveAs".AsLocaleKey(), OnSaveAs);
			_saveWorldCopy.Target = ui.Button(OfficialAssets.Graphics.Icons.Dash.SaveWorldCopy, "World.Actions.SaveCopy".AsLocaleKey(), OnSaveCopy);
			ui.PopStyle();
			ui.PushStyle();
			ui.Text("World.Config.ResoniteLinkHeader".AsLocaleKey());
			ui.Style.MinHeight *= 1.5f;
			ui.Style.PreferredHeight *= 1.5f;
			ui.Style.ButtonSpriteColor = colorX.White;
			_enableResoniteLink.Target = ui.Button("World.Actions.EnableResoniteLink".AsLocaleKey(), OnEnableResoniteLink);
			_resoniteLinkPort.Target = ui.Text((LocaleString)"");
			ui.PopStyle();
			ui = new UIBuilder(list[2]);
			RadiantUI_Constants.SetupDefaultStyle(ui);
			ui.VerticalLayout(4f);
			ui.FitContent(SizeFit.Disabled, SizeFit.PreferredSize);
			ui.Style.MinHeight = 24f;
			ui.Style.PreferredHeight = 24f;
			Checkbox checkbox = ui.Checkbox("World.Config.EditMode".AsLocaleKey());
			_editMode.Target = checkbox.Slot.GetComponent<Button>();
			_editModeSync.Target = checkbox.State.SyncWithWorld(null);
			ui.Text("World.Config.AccessLevelHeader".AsLocaleKey("<b>{0}</b>"));
			ValueField<SessionAccessLevel> valueField = ui.Current.AttachComponent<ValueField<SessionAccessLevel>>();
			GenerateAccessLevelUI(ui, valueField.Value, delegate(ValueRadio<SessionAccessLevel> radio)
			{
				_accessLevelRadios.Add(radio);
				_accessLevelRadiosButtons.Add(radio.Slot.GetComponent<Button>());
			});
			_accessLevelSync.Target = valueField.Value.SyncWithWorld(null);
			_customVerifierCheckbox.Target = ui.Checkbox((LocaleString)"Custom Join Verifier", out Text labelText);
			_customVerifierLabel.Target = labelText;
			Button component = _customVerifierCheckbox.Target.Slot.GetComponent<Button>();
			_customVerifierButton.Target = component;
			_customVerifierSync.Target = _customVerifierCheckbox.Target.State.SyncWithWorld(null);
			component.SendSlotEvents.Value = false;
			component.Pressed.Target = CustomVerifierPressed;
			ui.PushStyle();
			ui.Style.Height = 2f;
			ui.Image(colorX.Black);
			ui.PopStyle();
			_awayKickEnabled.Target = ui.Checkbox("World.Config.AutoKickAFK".AsLocaleKey());
			_awayKickMinutes.Target = ui.HorizontalElementWithLabel("World.Config.AutoKickAFKMinutes".AsLocaleKey(), 0.75f, () => ui.FloatField(1f, 120f, 1));
			_awayKickEnabledSync.Target = _awayKickEnabled.Target.State.SyncWithWorld(null);
			_awayKickMinutesSync.Target = _awayKickMinutes.Target.ParsedValue.SyncWithWorld(null);
			_awayKickEnabledButton.Target = _awayKickEnabled.Target.Slot.GetComponent<Button>();
			_awayKickMinutesButton.Target = _awayKickMinutes.Target.Slot.GetComponent<Button>();
			_hideFromListing.Target = ui.Checkbox("World.Config.HideFromListing".AsLocaleKey());
			_hideFromListingButton.Target = _hideFromListing.Target.Slot.GetComponent<Button>();
			_hideFromListingSync.Target = _hideFromListing.Target.State.SyncWithWorld(null);
			ui.PushStyle();
			ui.Style.Height = 2f;
			ui.Image(colorX.Black);
			ui.PopStyle();
			_autosaveEnabled.Target = ui.Checkbox("World.Config.Autosave".AsLocaleKey());
			_autosaveMinutes.Target = ui.HorizontalElementWithLabel("World.Config.AutosaveInterval".AsLocaleKey(), 0.75f, () => ui.FloatField(1f, 120f, 1));
			_autosaveEnabledSync.Target = _autosaveEnabled.Target.State.SyncWithWorld(null);
			_autosaveMinutesSync.Target = _autosaveMinutes.Target.ParsedValue.SyncWithWorld(null);
			_autosaveEnabledButton.Target = _autosaveEnabled.Target.Slot.GetComponent<Button>();
			_autosaveMinutesButton.Target = _autosaveMinutes.Target.Slot.GetComponent<Button>();
			ui.PushStyle();
			ui.Style.Height = 2f;
			ui.Image(colorX.Black);
			ui.PopStyle();
			_autocleanEnabled.Target = ui.Checkbox("World.Config.CleanupAssets".AsLocaleKey());
			_autocleanMinutes.Target = ui.HorizontalElementWithLabel("World.Config.CleanupInterval".AsLocaleKey(), 0.75f, () => ui.FloatField(1f, 3600f, 1));
			_autocleanEnabledSync.Target = _autocleanEnabled.Target.State.SyncWithWorld(null);
			_autocleanSecondsSync.Target = _autocleanMinutes.Target.ParsedValue.SyncWithWorld(null);
			_autocleanEnabledButton.Target = _autocleanEnabled.Target.Slot.GetComponent<Button>();
			_autocleanMinutesButton.Target = _autocleanMinutes.Target.Slot.GetComponent<Button>();
			UpdateValueSyncs(_lastWorld);
			break;
		}
		}
	}

	public static void GenerateAccessLevelUI(UIBuilder ui, IField<SessionAccessLevel> field, Action<ValueRadio<SessionAccessLevel>> radioSpawned = null)
	{
		foreach (SessionAccessLevel value in Enums.GetValues<SessionAccessLevel>(EnumMemberSelection.Distinct | EnumMemberSelection.DisplayOrder))
		{
			ValueRadio<SessionAccessLevel> obj = ui.ValueRadio(AccessLevelDescription(value), field, value);
			radioSpawned?.Invoke(obj);
		}
	}

	private static LocaleString AccessLevelDescription(SessionAccessLevel accessLevel)
	{
		return ("World.AccessLevel." + accessLevel.AsString()).AsLocaleKey();
	}

	[SyncMethod(typeof(Delegate), null)]
	private void GetSessionOrb(IButton button, ButtonEventData eventData)
	{
		GetOrb(sessionOrb: true);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void GetWorldOrb(IButton button, ButtonEventData eventData)
	{
		GetOrb(sessionOrb: false);
	}

	private void GetOrb(bool sessionOrb)
	{
		World world = base.Engine.WorldManager.FocusedWorld;
		world.RunSynchronously(delegate
		{
			Slot slot = world.RootSlot.LocalUserSpace.AddSlot("World Orb");
			WorldOrb worldOrb = slot.AttachComponent<WorldOrb>();
			if (sessionOrb)
			{
				worldOrb.ActiveSessionURLs = world.SessionURLs;
				worldOrb.ActiveUsers.Value = world.UserCount;
			}
			else
			{
				worldOrb.URL = world.RecordURL;
			}
			worldOrb.WorldName = world.Name;
			Uri uri = Userspace.GetThumbnailData(world)?.PublicThumbnailUrl;
			if (uri != null)
			{
				worldOrb.ThumbnailTexURL = uri;
			}
			slot.PositionInFrontOfUser();
		});
	}

	[SyncMethod(typeof(Delegate), null)]
	private void CopySessionURL(IButton button, ButtonEventData eventData)
	{
		World focusedWorld = base.Engine.WorldManager.FocusedWorld;
		if (base.InputInterface.IsClipboardSupported)
		{
			base.InputInterface.Clipboard.SetText(base.Cloud.ApiEndpoint + "/open/session/" + focusedWorld.SessionId);
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void CopyWorldURL(IButton button, ButtonEventData eventData)
	{
		FrooxEngine.Store.Record correspondingRecord = base.Engine.WorldManager.FocusedWorld.CorrespondingRecord;
		if (correspondingRecord != null && base.InputInterface.IsClipboardSupported)
		{
			base.InputInterface.Clipboard.SetText($"{base.Cloud.ApiEndpoint}/open/world/{correspondingRecord.OwnerId}/{correspondingRecord.RecordId}");
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void CopyRecordURL(IButton button, ButtonEventData eventData)
	{
		FrooxEngine.Store.Record correspondingRecord = base.Engine.WorldManager.FocusedWorld.CorrespondingRecord;
		if (correspondingRecord != null && base.InputInterface.IsClipboardSupported)
		{
			base.InputInterface.Clipboard.SetText(correspondingRecord.GetUrl(base.Cloud.Platform).OriginalString);
		}
	}

	private void PasteFromClipboard(IButton button, ButtonEventData eventData)
	{
		base.InputInterface.SimulatePress(Key.LeftControl, base.World);
		base.InputInterface.SimulatePress(Key.V, base.World);
	}

	private void NotifyCopied(IButton button, ButtonEventData eventData)
	{
		string oldText = button.LabelText;
		button.Enabled = false;
		button.LabelText = this.GetLocalized("General.CopiedToClipboardLong");
		button.RunInSeconds(2f, delegate
		{
			button.Enabled = true;
			button.LabelText = oldText;
		});
	}

	private void UpdateValueSyncs(World world)
	{
		if (_worldNameSync.Target != null)
		{
			_worldNameSync.Target.TargetWorldValue = world?.Configuration.WorldName;
			_descriptionSync.Target.TargetWorldValue = world?.Configuration.WorldDescription;
			_maxUsersSync.Target.TargetWorldValue = world?.Configuration.MaxUsers;
			_awayKickEnabledSync.Target.TargetWorldValue = world?.Configuration.AwayKickEnabled;
			_awayKickMinutesSync.Target.TargetWorldValue = world?.Configuration.AwayKickMinutes;
			_autosaveEnabledSync.Target.TargetWorldValue = world?.Configuration.AutoSaveEnabled;
			_autosaveMinutesSync.Target.TargetWorldValue = world?.Configuration.AutoSaveInterval;
			_autocleanEnabledSync.Target.TargetWorldValue = world?.Configuration.AutoCleanupEnabled;
			_autocleanSecondsSync.Target.TargetWorldValue = world?.Configuration.AutoCleanupInterval;
			_mobileFriendlySync.Target.TargetWorldValue = world?.Configuration.MobileFriendly;
			_hideFromListingSync.Target.TargetWorldValue = world?.Configuration.HideFromListing;
			_editModeSync.Target.TargetWorldValue = world?.LocalUser?.editMode;
			_accessLevelSync.Target.TargetWorldValue = world?.Configuration.AccessLevel;
			_customVerifierSync.Target.TargetWorldValue = world?.Configuration.UseCustomJoinVerifier;
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void CustomVerifierPressed(IButton button, ButtonEventData eventData)
	{
		World focusedWorld = base.Engine.WorldManager.FocusedWorld;
		if (focusedWorld?.Configuration?.CustomJoinVerifier.Target == null)
		{
			return;
		}
		if (focusedWorld.Configuration.UseCustomJoinVerifier.Value)
		{
			focusedWorld.RunSynchronously(delegate
			{
				focusedWorld.Configuration.UseCustomJoinVerifier.Value = false;
			});
		}
		else
		{
			base.Slot.OpenModalChoiceDialog("Session.JoinVerifier.EnableHeader".AsLocaleKey(), "Session.JoinVerifier.EnableText".AsLocaleKey(), new ModalChoiceItem("General.UnderstandEnable".AsLocaleKey(), null, OnEnableCustomJoinVerifier), new ModalChoiceItem("General.Cancel".AsLocaleKey(), null, null));
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnEnableCustomJoinVerifier(IButton button, ButtonEventData eventData)
	{
		World focusedWorld = base.Engine.WorldManager.FocusedWorld;
		if (focusedWorld != null)
		{
			focusedWorld.RunSynchronously(delegate
			{
				focusedWorld.Configuration.UseCustomJoinVerifier.Value = true;
			});
		}
	}

	protected override void OnCommonUpdate()
	{
		for (int i = 0; i < _tabButtons.Count; i++)
		{
			_tabButtons[i].SetColors((i == (int)ActiveTab.Value) ? RadiantUI_Constants.TAB_ACTIVE_BACKGROUND_COLOR : RadiantUI_Constants.TAB_INACTIVE_BACKGROUND_COLOR);
		}
		World focusedWorld = base.Engine.WorldManager.FocusedWorld;
		if (_customVerifierButton.Target != null)
		{
			IWorldUserJoinVerifier worldUserJoinVerifier = focusedWorld?.Configuration?.CustomJoinVerifier.Target;
			_customVerifierButton.Target.Enabled = worldUserJoinVerifier != null && focusedWorld.IsAuthority;
			_customVerifierLabel.Target.Color.Value = RadiantUI_Constants.TEXT_COLOR.SetA((worldUserJoinVerifier != null) ? 1f : 0.5f);
			_customVerifierLabel.Target.Content.SetLocalized("Session.JoinVerifier.Label", null, "name", (worldUserJoinVerifier == null) ? "(none)" : $"<color={RadiantUI_Constants.LABEL_HEX}>{worldUserJoinVerifier.FindNearestParent<Slot>()?.Name}</color>");
		}
		switch (ActiveTab.Value)
		{
		case Tab.Settings:
		{
			bool flag = focusedWorld.IsAllowedToSaveWorld();
			bool flag2 = Userspace.CanSave(focusedWorld);
			bool flag3 = focusedWorld.IsAllowedToRunResoniteLink();
			_saveWorld.Target.Enabled = flag2 && flag;
			_saveWorldAs.Target.Enabled = flag && focusedWorld != Userspace.LocalHome;
			_saveWorldCopy.Target.Enabled = flag;
			ResoniteLinkHost resoniteLink = focusedWorld.ResoniteLink;
			_enableResoniteLink.Target.Enabled = resoniteLink == null && flag3;
			if (resoniteLink == null)
			{
				_resoniteLinkPort.Target.LocaleContent = "World.Config.ResoniteLinkOff".AsLocaleKey();
			}
			else
			{
				_resoniteLinkPort.Target.LocaleContent = "World.Config.ResoniteLinkPort".AsLocaleKey(null, "port", resoniteLink.Port);
			}
			if (focusedWorld != _lastWorld)
			{
				_lastWorld = focusedWorld;
				UpdateValueSyncs(_lastWorld);
			}
			if (_worldName.Target != null)
			{
				_worldNameButton.Target.Enabled = focusedWorld.Configuration.CanChangeName();
				_descriptionButton.Target.Enabled = focusedWorld.Configuration.CanChangeName();
				_maxUsersButton.Target.Enabled = focusedWorld.Configuration.CanChangeProperties();
				_awayKickEnabledButton.Target.Enabled = focusedWorld.Configuration.CanChangeAccessLevel();
				_awayKickMinutesButton.Target.Enabled = focusedWorld.Configuration.CanChangeAccessLevel();
				bool enabled = focusedWorld.IsAuthority && flag2;
				_autosaveEnabledButton.Target.Enabled = enabled;
				_autosaveMinutesButton.Target.Enabled = enabled;
				_autocleanEnabledButton.Target.Enabled = focusedWorld.Configuration.CanChangeProperties();
				_autocleanMinutesButton.Target.Enabled = focusedWorld.Configuration.CanChangeProperties();
				_mobileFriendlyButton.Target.Enabled = focusedWorld.Configuration.CanChangeProperties();
				_hideFromListingButton.Target.Enabled = focusedWorld.Configuration.CanChangeAccessLevel();
				bool enabled2 = focusedWorld.Configuration.CanChangeAccessLevel();
				foreach (Button accessLevelRadiosButton in _accessLevelRadiosButtons)
				{
					accessLevelRadiosButton.Enabled = enabled2;
				}
			}
			bool enabled3 = _lastWorld?.RecordURL != null;
			if (_getWorldOrb.Target != null)
			{
				_getWorldOrb.Target.Enabled = enabled3;
			}
			if (_copyWorldURL.Target != null)
			{
				_copyWorldURL.Target.Enabled = enabled3;
			}
			if (_copyRecordURL.Target != null)
			{
				_copyRecordURL.Target.Enabled = enabled3;
			}
			_editMode.Target.Enabled = _lastWorld?.LocalUser.CanEnableEditMode() ?? false;
			break;
		}
		case Tab.Users:
		case Tab.Permissions:
		{
			List<User> list = Pool.BorrowList<User>();
			focusedWorld?.GetUsers(list);
			if (ActiveTab.Value == Tab.Users)
			{
				UpdateUserControls(list, (User user, UIBuilder ui) => SessionUserController.Create(user, ui), _currentUserControllers);
			}
			else
			{
				UpdateUserControls(list, (User user, UIBuilder ui) => SessionPermissionController.Create(() => user.role, ui, user.UserName, user), _currentPermissionControllers);
			}
			Pool.Return(ref list);
			if (focusedWorld != null && _permissionOverridesIndicator.Target != null)
			{
				if (focusedWorld.IsAuthority)
				{
					_permissionOverridesIndicator.Target.Content.Value = PermissionOverrides(focusedWorld.Permissions.DefaultUserPermissions.Count);
					_permissionOverridesButton.Target.Enabled = true;
				}
				else
				{
					_permissionOverridesIndicator.Target.Content.Value = PermissionOverrides(-1);
					_permissionOverridesButton.Target.Enabled = false;
				}
			}
			break;
		}
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnClearUserPermissionOverrides(IButton button, ButtonEventData eventData)
	{
		World focusedWorld = base.Engine.WorldManager.FocusedWorld;
		focusedWorld.RunSynchronously(delegate
		{
			if (focusedWorld.IsAuthority)
			{
				focusedWorld.Permissions.DefaultUserPermissions.Clear();
			}
		});
	}

	private ReadOnlyRef<PermissionSet> FocusedWorldPermission(Func<PermissionController, ReadOnlyRef<PermissionSet>> getter)
	{
		return getter(base.Engine.WorldManager.FocusedWorld.Permissions);
	}

	private void UpdateUserControls<T>(List<User> users, Func<User, UIBuilder, T> createNew, Dictionary<User, T> current) where T : IUpdatedFlag, IComponent
	{
		foreach (KeyValuePair<User, T> item in current)
		{
			T value = item.Value;
			value.Updated = false;
		}
		foreach (User user in users)
		{
			if (!current.TryGetValue(user, out T value2))
			{
				UIBuilder uIBuilder = new UIBuilder(_uiContentRoot.Target);
				RadiantUI_Constants.SetupDefaultStyle(uIBuilder);
				value2 = createNew(user, uIBuilder);
				current.Add(user, value2);
			}
			value2.Updated = true;
		}
		users.Clear();
		foreach (KeyValuePair<User, T> item2 in current)
		{
			if (!item2.Value.Updated)
			{
				item2.Value.Slot.Destroy();
				users.Add(item2.Key);
			}
		}
		foreach (User user2 in users)
		{
			current.Remove(user2);
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnSave(IButton button, ButtonEventData eventData)
	{
		World focusedWorld = base.Engine.WorldManager.FocusedWorld;
		if (focusedWorld != null && focusedWorld.IsAllowedToSaveWorld())
		{
			Userspace.SaveWorldAuto(focusedWorld, SaveType.Overwrite, exitOnSave: false);
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnSaveAs(IButton button, ButtonEventData eventData)
	{
		World focusedWorld = base.Engine.WorldManager.FocusedWorld;
		if (focusedWorld != null && focusedWorld.IsAllowedToSaveWorld())
		{
			Userspace.SaveWorldAuto(focusedWorld, SaveType.SaveAs, exitOnSave: false);
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnSaveCopy(IButton button, ButtonEventData eventData)
	{
		World focusedWorld = base.Engine.WorldManager.FocusedWorld;
		if (focusedWorld != null && focusedWorld.IsAllowedToSaveWorld())
		{
			Userspace.SaveWorldAuto(focusedWorld, SaveType.SaveCopy, exitOnSave: false);
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnEnableResoniteLink(IButton button, ButtonEventData eventData)
	{
		World focusedWorld = base.Engine.WorldManager.FocusedWorld;
		if (focusedWorld != null && focusedWorld.IsAuthority && focusedWorld.ResoniteLink == null)
		{
			focusedWorld.StartResoniteLink();
		}
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		ActiveTab = new Sync<Tab>();
		_contentRoot = new SyncRef<Slot>();
		_worldName = new SyncRef<TextField>();
		_maxUsers = new SyncRef<IntTextEditorParser>();
		_awayKickEnabled = new SyncRef<Checkbox>();
		_awayKickMinutes = new SyncRef<FloatTextEditorParser>();
		_autosaveEnabled = new SyncRef<Checkbox>();
		_autosaveMinutes = new SyncRef<FloatTextEditorParser>();
		_autocleanEnabled = new SyncRef<Checkbox>();
		_autocleanMinutes = new SyncRef<FloatTextEditorParser>();
		_mobileFriendly = new SyncRef<Checkbox>();
		_hideFromListing = new SyncRef<Checkbox>();
		_description = new SyncRef<TextField>();
		_worldNameButton = new SyncRef<Button>();
		_descriptionButton = new SyncRef<Button>();
		_maxUsersButton = new SyncRef<Button>();
		_awayKickEnabledButton = new SyncRef<Button>();
		_awayKickMinutesButton = new SyncRef<Button>();
		_autosaveEnabledButton = new SyncRef<Button>();
		_autosaveMinutesButton = new SyncRef<Button>();
		_autocleanEnabledButton = new SyncRef<Button>();
		_autocleanMinutesButton = new SyncRef<Button>();
		_mobileFriendlyButton = new SyncRef<Button>();
		_hideFromListingButton = new SyncRef<Button>();
		_permissionOverridesIndicator = new SyncRef<Text>();
		_permissionOverridesButton = new SyncRef<Button>();
		_getSessionOrb = new SyncRef<Button>();
		_getWorldOrb = new SyncRef<Button>();
		_editMode = new SyncRef<Button>();
		_copySessionURL = new SyncRef<Button>();
		_copyWorldURL = new SyncRef<Button>();
		_copyRecordURL = new SyncRef<Button>();
		_accessLevelRadios = new SyncRefList<Radio>();
		_accessLevelRadiosButtons = new SyncRefList<Button>();
		_worldNameSync = new SyncRef<WorldValueSync<string>>();
		_descriptionSync = new SyncRef<WorldValueSync<string>>();
		_maxUsersSync = new SyncRef<WorldValueSync<int>>();
		_awayKickEnabledSync = new SyncRef<WorldValueSync<bool>>();
		_awayKickMinutesSync = new SyncRef<WorldValueSync<float>>();
		_autosaveEnabledSync = new SyncRef<WorldValueSync<bool>>();
		_autosaveMinutesSync = new SyncRef<WorldValueSync<float>>();
		_autocleanEnabledSync = new SyncRef<WorldValueSync<bool>>();
		_autocleanSecondsSync = new SyncRef<WorldValueSync<float>>();
		_mobileFriendlySync = new SyncRef<WorldValueSync<bool>>();
		_hideFromListingSync = new SyncRef<WorldValueSync<bool>>();
		_editModeSync = new SyncRef<WorldValueSync<bool>>();
		_accessLevelSync = new SyncRef<WorldValueSync<SessionAccessLevel>>();
		_customVerifierLabel = new SyncRef<Text>();
		_customVerifierCheckbox = new SyncRef<Checkbox>();
		_customVerifierButton = new SyncRef<Button>();
		_customVerifierSync = new SyncRef<WorldValueSync<bool>>();
		_uiContentRoot = new SyncRef<Slot>();
		_slideSwap = new SyncRef<SlideSwapRegion>();
		_saveWorld = new SyncRef<Button>();
		_saveWorldAs = new SyncRef<Button>();
		_saveWorldCopy = new SyncRef<Button>();
		_enableResoniteLink = new SyncRef<Button>();
		_resoniteLinkPort = new SyncRef<Text>();
		_tabButtons = new SyncRefList<Button>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => ActiveTab, 
			4 => _contentRoot, 
			5 => _worldName, 
			6 => _maxUsers, 
			7 => _awayKickEnabled, 
			8 => _awayKickMinutes, 
			9 => _autosaveEnabled, 
			10 => _autosaveMinutes, 
			11 => _autocleanEnabled, 
			12 => _autocleanMinutes, 
			13 => _mobileFriendly, 
			14 => _hideFromListing, 
			15 => _description, 
			16 => _worldNameButton, 
			17 => _descriptionButton, 
			18 => _maxUsersButton, 
			19 => _awayKickEnabledButton, 
			20 => _awayKickMinutesButton, 
			21 => _autosaveEnabledButton, 
			22 => _autosaveMinutesButton, 
			23 => _autocleanEnabledButton, 
			24 => _autocleanMinutesButton, 
			25 => _mobileFriendlyButton, 
			26 => _hideFromListingButton, 
			27 => _permissionOverridesIndicator, 
			28 => _permissionOverridesButton, 
			29 => _getSessionOrb, 
			30 => _getWorldOrb, 
			31 => _editMode, 
			32 => _copySessionURL, 
			33 => _copyWorldURL, 
			34 => _copyRecordURL, 
			35 => _accessLevelRadios, 
			36 => _accessLevelRadiosButtons, 
			37 => _worldNameSync, 
			38 => _descriptionSync, 
			39 => _maxUsersSync, 
			40 => _awayKickEnabledSync, 
			41 => _awayKickMinutesSync, 
			42 => _autosaveEnabledSync, 
			43 => _autosaveMinutesSync, 
			44 => _autocleanEnabledSync, 
			45 => _autocleanSecondsSync, 
			46 => _mobileFriendlySync, 
			47 => _hideFromListingSync, 
			48 => _editModeSync, 
			49 => _accessLevelSync, 
			50 => _customVerifierLabel, 
			51 => _customVerifierCheckbox, 
			52 => _customVerifierButton, 
			53 => _customVerifierSync, 
			54 => _uiContentRoot, 
			55 => _slideSwap, 
			56 => _saveWorld, 
			57 => _saveWorldAs, 
			58 => _saveWorldCopy, 
			59 => _enableResoniteLink, 
			60 => _resoniteLinkPort, 
			61 => _tabButtons, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static SessionControlDialog __New()
	{
		return new SessionControlDialog();
	}
}
