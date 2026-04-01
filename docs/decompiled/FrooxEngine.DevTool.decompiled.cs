using System;
using System.Collections.Generic;
using System.Linq;
using Elements.Core;
using Elements.Data;
using FrooxEngine.ProtoFlux;
using FrooxEngine.Undo;

namespace FrooxEngine;

[Category(new string[] { "Tools" })]
[OldTypeName("FrooxEngine.DevToolTip", null)]
public class DevTool : Tool
{
	public enum Selection
	{
		Single,
		Multi
	}

	public enum Interaction
	{
		Tip,
		Projection
	}

	public const float GIZMO_SEARCH_RADIUS = 0.1f;

	public readonly Sync<Selection> SelectionMode;

	public readonly Sync<Interaction> InteractionMode;

	protected readonly SyncRef<PointAnchor> _selectedAnchor;

	protected readonly SyncRef<Slot> _selectedAnchorHighlight;

	protected readonly DriveRef<OverlayFresnelMaterial> _material;

	private Gizmo activeGizmo;

	private bool? _lastEditMode;

	private bool _lastUnlockRegistered;

	protected readonly SyncRef<Slot> _currentGizmo;

	protected readonly SyncRef<Slot> _previousGizmo;

	private DevToolInputs _input;

	public bool IsDevModeEnabled
	{
		get
		{
			return DevModeController.IsDevModeEnabled(base.World);
		}
		set
		{
			if (value)
			{
				DevModeController.EnableDevMode(base.World);
			}
			else
			{
				DevModeController.DisableDevMode(base.World);
			}
		}
	}

	public bool UseProjection
	{
		get
		{
			if (!base.InputInterface.ScreenActive)
			{
				return InteractionMode.Value == Interaction.Projection;
			}
			return true;
		}
	}

	protected override float3 DefaultLocalTip => float3.Forward * 0.075f;

	public override bool UsesLaser => true;

	public override string PrimaryDescription => this.GetLocalized("Tutorial.Tooltip.DevPrimary");

	public override string SecondaryDescription => this.GetLocalized("Tutorial.Tooltip.DevSecondary");

	private string DevModeLabel => this.GetLocalized("Tools.Dev.DevMode." + (IsDevModeEnabled ? "On" : "Off"));

	public void SetActiveSlotGizmo(Slot slot)
	{
		if (_currentGizmo.Target != slot)
		{
			_previousGizmo.Target = _currentGizmo.Target;
			_currentGizmo.Target = slot;
		}
	}

	public override bool IsMovingTarget(Slot target)
	{
		if (activeGizmo?.TargetSlot.Target != null && target.IsChildOf(activeGizmo.TargetSlot.Target, includeSelf: true))
		{
			return true;
		}
		return false;
	}

	public override float3? OverrideTargetPoint(float3 origin, float3 direction)
	{
		if (!UseProjection || activeGizmo == null)
		{
			return null;
		}
		return activeGizmo.OverrideTargetPoint(origin, direction);
	}

	public override bool IsInteractionTarget(Slot target)
	{
		if (target.GetComponentInParents<PointAnchor>() != null)
		{
			return true;
		}
		if (target.GetComponentInParents<Gizmo>() != null)
		{
			return true;
		}
		if (target.GetComponentInParents<IComponentGizmo>() != null)
		{
			return true;
		}
		return false;
	}

	public override LaserHitClass ClassifyHit(RaycastHit hit)
	{
		Slot slot = hit.Collider.Slot;
		if (IsInteractionTarget(slot))
		{
			return LaserHitClass.Prefer;
		}
		if (slot.Tag == "Developer")
		{
			return LaserHitClass.Prefer;
		}
		return LaserHitClass.Allow;
	}

	protected override void OnAwake()
	{
		base.OnAwake();
		InteractionMode.Value = Interaction.Projection;
	}

	protected override void OnAttach()
	{
		base.OnAttach();
		Slot slot = base.Slot.AddSlot("Visual");
		slot.AttachComponent<SphereCollider>().Radius.Value = 0.02f;
		slot.LocalRotation = floatQ.Euler(90f, 0f, 0f);
		slot.LocalPosition += float3.Forward * 0.05f;
		_material.Target = slot.AttachComponent<OverlayFresnelMaterial>();
		ConeMesh coneMesh = slot.AttachMesh<ConeMesh>(_material.Target);
		coneMesh.RadiusTop.Value = 0.0025f;
		coneMesh.RadiusBase.Value = 0.015f;
		coneMesh.Height.Value = 0.05f;
	}

	public override void Update(float primaryStrength, float2 secondaryAxis, Digital primary, Digital secondary)
	{
		if (activeGizmo != null)
		{
			if (!_lastUnlockRegistered)
			{
				_lastUnlockRegistered = true;
				base.Input.RegisterCursorUnlock(this);
			}
		}
		else
		{
			UnregisterCursorUnlock();
		}
		if (_input != null)
		{
			if (_input.Inspector.Pressed)
			{
				OpenInspector();
			}
			if (_input.Focus.Pressed)
			{
				_currentGizmo.Target?.FocusFreecam(toggle: true);
			}
		}
	}

	private void UnregisterCursorUnlock()
	{
		if (_lastUnlockRegistered)
		{
			base.Input.UnregisterCursorUnlock(this);
			_lastUnlockRegistered = false;
		}
	}

	public override void OnPrimaryPress()
	{
		RaycastHit? hit = GetHit();
		if (!hit.HasValue)
		{
			return;
		}
		RaycastHit value = hit.Value;
		PointAnchor componentInParents = value.Collider.Slot.GetComponentInParents<PointAnchor>();
		if (componentInParents != null)
		{
			SelectAnchor(componentInParents);
			return;
		}
		Gizmo componentInParents2 = value.Collider.Slot.GetComponentInParents<Gizmo>();
		if (componentInParents2 != null)
		{
			float3? overrideAnchor = null;
			if (_selectedAnchor.Target != null)
			{
				overrideAnchor = _selectedAnchor.Target.Slot.GlobalPosition;
			}
			componentInParents2.BeginInteraction(this, value.Collider.Slot, UseProjection ? base.InteractionOrigin : base.Tip, base.InteractionDirection, UseProjection, overrideAnchor);
			activeGizmo = componentInParents2;
		}
	}

	public override void OnPrimaryHold()
	{
		try
		{
			activeGizmo?.UpdatePoint(this, UseProjection ? base.ActiveHandler.Laser.LastInteractionTargetPoint : base.Tip, base.InteractionDirection);
		}
		catch (Exception exception)
		{
			UniLog.Error("Exception updating active gizmo:\n" + DebugManager.PreprocessException(exception));
		}
	}

	public override void OnPrimaryRelease()
	{
		try
		{
			activeGizmo?.EndInteraction(this);
		}
		catch (Exception exception)
		{
			UniLog.Error("Exception updating active gizmo:\n" + DebugManager.PreprocessException(exception));
		}
		activeGizmo = null;
	}

	public override void OnSecondaryPress()
	{
		TryOpenGizmo();
	}

	private void SelectAnchor(PointAnchor pointAnchor)
	{
		_selectedAnchorHighlight.Target?.Destroy();
		_selectedAnchorHighlight.Target = null;
		if (pointAnchor == _selectedAnchor.Target || pointAnchor == null)
		{
			_selectedAnchor.Target = null;
			return;
		}
		_selectedAnchor.Target = pointAnchor;
		Slot slot = pointAnchor.Slot.AddSlot("Highlight");
		slot.PersistentSelf = false;
		AttachedModel<IcoSphereMesh, OverlayFresnelMaterial> attachedModel = slot.AttachMesh<IcoSphereMesh, OverlayFresnelMaterial>();
		attachedModel.mesh.Subdivisions.Value = 1;
		attachedModel.mesh.Radius.Value = 0.00825f;
		attachedModel.material.RenderQueue.Value = 4000;
		GizmoHelper.SetupMaterial(attachedModel.material, PointAnchor.SELECTED_COLOR);
		_selectedAnchorHighlight.Target = slot;
	}

	protected override void OnCommonUpdate()
	{
		OverlayFresnelMaterial target = _material.Target;
		if (target != null)
		{
			target.BlendMode.Value = BlendMode.Alpha;
			target.Sidedness.Value = Sidedness.Front;
			if (IsDevModeEnabled)
			{
				target.FrontNearColor.Value = new colorX(0f);
				target.FrontFarColor.Value = new colorX(0f, 1f, 0f);
				target.BehindNearColor.Value = new colorX(0f, 0.25f);
				target.BehindFarColor.Value = new colorX(0f, 0.8f, 0f, 0.25f);
			}
			else
			{
				target.FrontNearColor.Value = new colorX(0f);
				target.FrontFarColor.Value = new colorX(1f, 0f, 0f);
				target.BehindNearColor.Value = new colorX(0f, 0.25f);
				target.BehindFarColor.Value = new colorX(0.8f, 0f, 0f, 0.25f);
			}
		}
	}

	private bool IsGizmo(Slot slot)
	{
		if (slot.GetComponentInParents<IComponentGizmo>() != null)
		{
			return true;
		}
		if (slot.GetComponentInChildren<IComponentGizmo>() != null)
		{
			return true;
		}
		return false;
	}

	private void TryOpenGizmo()
	{
		float3 b = base.Tip;
		float num = float.MaxValue;
		int num2 = 0;
		Slot slot = null;
		Slot slot2 = base.Slot.ActiveUserRoot?.Slot;
		RaycastHit? hit = GetHit();
		if (hit.HasValue)
		{
			RaycastHit value = hit.Value;
			if (!IsGizmo(value.Collider.Slot))
			{
				slot = value.Collider.Slot;
				num = value.Distance;
			}
		}
		if (slot == null)
		{
			foreach (ICollider item in base.Physics.SphereOverlap(base.Tip, 0.025f))
			{
				if (!item.Slot.IsChildOf(slot2 ?? base.Slot, includeSelf: true) && !IsGizmo(item.Slot))
				{
					num = 0.025f;
					slot = item.Slot;
					break;
				}
			}
		}
		foreach (Slot allSlot in base.World.AllSlots)
		{
			float3 a;
			try
			{
				a = allSlot.GlobalPosition;
			}
			catch (Exception ex)
			{
				UniLog.Error("Exception getting global position for:\n" + allSlot?.ParentHierarchyToString() + "\n" + ex, stackTrace: false);
				continue;
			}
			float num3 = MathX.Distance(in a, in b);
			if (!(num3 > 0.1f) && (num3 < num || (MathX.Approximately(num3, num) && allSlot.Components.Count() > num2)) && !allSlot.IsChildOf(slot2 ?? base.Slot, includeSelf: true) && !IsGizmo(allSlot))
			{
				ProtoFluxNode component = allSlot.GetComponent<ProtoFluxNode>();
				if (component == null || component.HasActiveVisual())
				{
					num = num3;
					slot = allSlot;
					num2 = allSlot.Components.Count();
				}
			}
		}
		if (slot == null)
		{
			return;
		}
		SlotGizmo slotGizmo;
		if (_currentGizmo.Target == slot)
		{
			if (SelectionMode.Value == Selection.Single)
			{
				_currentGizmo.Target.RemoveGizmo();
				_currentGizmo.Target = null;
				return;
			}
			slotGizmo = _currentGizmo.Target.TryGetGizmo<SlotGizmo>();
			if (slotGizmo != null)
			{
				if (slotGizmo.IsFolded)
				{
					slotGizmo.Slot.Destroy();
				}
				else
				{
					slotGizmo.IsFolded = true;
				}
			}
			return;
		}
		if (_currentGizmo.Target != null)
		{
			if (SelectionMode.Value == Selection.Single)
			{
				_previousGizmo.Target?.RemoveGizmo();
				slotGizmo = _currentGizmo.Target.TryGetGizmo<SlotGizmo>();
				if (slotGizmo != null)
				{
					slotGizmo.IsFolded = true;
				}
			}
			_previousGizmo.Target = _currentGizmo.Target;
		}
		_currentGizmo.Target = slot;
		slotGizmo = _currentGizmo.Target.GetGizmo<SlotGizmo>();
		slotGizmo.IsFolded = false;
		slotGizmo.GizmoReplaced.Target = OnGizmoReplaced;
	}

	public override void GenerateMenuItems(InteractionHandler tool, ContextMenu menu)
	{
		menu.AddItem("Tools.Dev.CreateNew".AsLocaleKey(), OfficialAssets.Graphics.Icons.Item.Add, (colorX?)null, OpenCreate);
		menu.AddItem("Tools.Dev.OpenInspector".AsLocaleKey(), OfficialAssets.Graphics.Icons.Tool.InspectorPanel, (colorX?)null, OpenInspector);
		if (_currentGizmo.Target != null)
		{
			Uri icon = OfficialAssets.Graphics.Icons.Gizmo.EditAsset;
			SlotGizmo slotGizmo = _currentGizmo.Target?.TryGetGizmo<SlotGizmo>();
			if (slotGizmo != null)
			{
				if (slotGizmo.TranslationGizmoActive)
				{
					icon = OfficialAssets.Graphics.Icons.Gizmo.TranslateMode;
				}
				else if (slotGizmo.RotationGizmoActive)
				{
					icon = OfficialAssets.Graphics.Icons.Gizmo.RotationMode;
				}
				else if (slotGizmo.ScaleGizmoActive)
				{
					icon = OfficialAssets.Graphics.Icons.Gizmo.ScalingMode;
				}
			}
			menu.AddItem("Tools.Dev.GizmoOptions".AsLocaleKey(), icon, (colorX?)null, OpenGizmoOptions);
		}
		if (base.LocalUser.EditMode)
		{
			menu.AddItem((LocaleString)DevModeLabel, OfficialAssets.Common.Icons.Bolt, (colorX?)null, ToggleDevMode);
		}
		menu.AddEnumShiftItem(SelectionMode, new List<OptionDescription<Selection>>
		{
			new OptionDescription<Selection>(Selection.Single, "Tools.Dev.Selection.Single".AsLocaleKey(), OfficialAssets.Graphics.Icons.Item.Selection),
			new OptionDescription<Selection>(Selection.Multi, "Tools.Dev.Selection.Multi".AsLocaleKey(), OfficialAssets.Graphics.Icons.Item.MultiSelect)
		});
		if (base.InputInterface.VR_Active)
		{
			menu.AddEnumShiftItem(InteractionMode, new List<OptionDescription<Interaction>>
			{
				new OptionDescription<Interaction>(Interaction.Tip, "Tools.Dev.Interaction.Tip".AsLocaleKey(), OfficialAssets.Graphics.Icons.Tool.RayMode),
				new OptionDescription<Interaction>(Interaction.Projection, "Tools.Dev.Interaction.Projection".AsLocaleKey(), OfficialAssets.Graphics.Icons.Tool.RayMode2)
			});
		}
		menu.AddItem("Tools.Dev.DeselectAll".AsLocaleKey(), OfficialAssets.Graphics.Icons.Item.Deselect, (colorX?)null, DeselectAll);
		menu.AddItem("Tools.Dev.DestroySelected".AsLocaleKey(), OfficialAssets.Graphics.Icons.Item.DestroySelect, (colorX?)null, DestroySelected);
		_lastEditMode = base.LocalUser.editMode;
	}

	[SyncMethod(typeof(Delegate), null)]
	private void ToggleDevMode(IButton button, ButtonEventData eventData)
	{
		DevModeController.ToggleDevMode(base.World);
		base.ActiveHandler?.CloseContextMenu();
	}

	[SyncMethod(typeof(Delegate), null)]
	private void DeselectAll(IButton button, ButtonEventData eventData)
	{
		base.World.RootSlot.GetComponentsInChildren<SlotGizmo>().ForEach(delegate(SlotGizmo s)
		{
			s.Slot.Destroy();
		});
		_currentGizmo.Target = null;
		_previousGizmo.Target = null;
		base.ActiveHandler?.CloseContextMenu();
		SelectAnchor(null);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OpenCreate(IButton button, ButtonEventData eventData)
	{
		Slot slot = base.LocalUserSpace.AddSlot("Create Dialog");
		slot.AttachComponent<DevCreateNewForm>();
		slot.PositionInFrontOfUser(float3.Backward);
		base.ActiveHandler?.CloseContextMenu();
	}

	private void CreateInspector(Slot target)
	{
		Slot slot = base.LocalUserSpace.AddSlot("Inspector");
		slot.PositionInFrontOfUser(float3.Backward);
		SceneInspector sceneInspector = slot.AttachComponent<SceneInspector>();
		sceneInspector.Root.Target = target;
		if (!target.IsRootSlot)
		{
			sceneInspector.ComponentView.Target = target;
		}
	}

	private void OpenInspector()
	{
		if (base.ActiveHandler?.Grabber != null)
		{
			foreach (IGrabbable grabbedObject in base.ActiveHandler.Grabber.GrabbedObjects)
			{
				if (grabbedObject.Slot.GetComponentInChildren<ReferenceProxy>()?.Reference.Target is Worker worldElement)
				{
					CreateInspector(worldElement.FindNearestParent<Slot>());
					base.ActiveHandler?.CloseContextMenu();
					return;
				}
			}
		}
		if (_currentGizmo.Target?.TryGetGizmo() == null)
		{
			_currentGizmo.Target = null;
		}
		if (_currentGizmo.Target != null)
		{
			CreateInspector(_currentGizmo.Target);
			base.ActiveHandler?.CloseContextMenu();
		}
		else
		{
			CreateInspector(base.World.RootSlot);
			base.ActiveHandler?.CloseContextMenu();
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OpenInspector(IButton button, ButtonEventData eventData)
	{
		OpenInspector();
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OpenGizmoOptions(IButton button, ButtonEventData eventData)
	{
		StartTask(async delegate
		{
			ContextMenu contextMenu = await base.LocalUser.OpenContextMenu(base.ActiveHandler, base.ActiveHandler.PointReference);
			if (contextMenu != null)
			{
				SlotGizmo slotGizmo = _currentGizmo.Target?.TryGetGizmo<SlotGizmo>();
				if (slotGizmo != null)
				{
					contextMenu.AddItem("Tools.Dev.SelectParent".AsLocaleKey(), OfficialAssets.Graphics.Icons.Gizmo.SelectParent, (colorX?)null, OnOpenParent);
					contextMenu.AddToggleItem(slotGizmo.IsLocalSpace, "Tools.Dev.LocalSpace".AsLocaleKey(), "Tools.Dev.GlobalSpace".AsLocaleKey(), in RadiantUI_Constants.Hero.ORANGE, in RadiantUI_Constants.Hero.CYAN, OfficialAssets.Graphics.Icons.Gizmo.TransformLocal, OfficialAssets.Graphics.Icons.Gizmo.TransformGlobal);
					contextMenu.AddItem("Tools.Dev.Translation".AsLocaleKey(), OfficialAssets.Graphics.Icons.Gizmo.TranslateMode, (colorX?)null, SetTranslation);
					contextMenu.AddItem("Tools.Dev.Rotation".AsLocaleKey(), OfficialAssets.Graphics.Icons.Gizmo.RotationMode, (colorX?)null, SetRotation);
					contextMenu.AddItem("Tools.Dev.Scale".AsLocaleKey(), OfficialAssets.Graphics.Icons.Gizmo.ScalingMode, (colorX?)null, SetScale);
					{
						foreach (Component component in _currentGizmo.Target.Components)
						{
							if (component.GizmoType != null)
							{
								contextMenu.AddRefItem((LocaleString)component.WorkerType.GetNiceName(), OfficialAssets.Graphics.Icons.Gizmo.EditAsset, new colorX?(component.GetType().GetTypeColor()), ActivateGizmo, component);
							}
						}
						return;
					}
				}
				base.LocalUser.CloseContextMenu(base.ActiveHandler);
			}
		});
	}

	[SyncMethod(typeof(Action), new string[] { })]
	public void SelectParent()
	{
		_currentGizmo.Target?.TryGetGizmo<SlotGizmo>()?.OpenParent();
	}

	[SyncMethod(typeof(Action), new string[] { })]
	public void ToggleSpace()
	{
		_currentGizmo.Target?.TryGetGizmo<SlotGizmo>()?.SwitchSpace();
	}

	[SyncMethod(typeof(Action), new string[] { })]
	public void SetTranslation()
	{
		_currentGizmo.Target?.TryGetGizmo<SlotGizmo>()?.SetTranslation();
	}

	[SyncMethod(typeof(Action), new string[] { })]
	public void SetRotation()
	{
		_currentGizmo.Target?.TryGetGizmo<SlotGizmo>()?.SetRotation();
	}

	[SyncMethod(typeof(Action), new string[] { })]
	public void SetScale()
	{
		_currentGizmo.Target?.TryGetGizmo<SlotGizmo>()?.SetScale();
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnOpenParent(IButton button, ButtonEventData eventData)
	{
		SelectParent();
	}

	[SyncMethod(typeof(Delegate), null)]
	private void SetTranslation(IButton button, ButtonEventData eventData)
	{
		SetTranslation();
	}

	[SyncMethod(typeof(Delegate), null)]
	private void SetRotation(IButton button, ButtonEventData eventData)
	{
		SetRotation();
	}

	[SyncMethod(typeof(Delegate), null)]
	private void SetScale(IButton button, ButtonEventData eventData)
	{
		SetScale();
	}

	[SyncMethod(typeof(Delegate), null)]
	private void ActivateGizmo(IButton button, ButtonEventData eventData, Component component)
	{
		component.Slot.GetGizmo<SlotGizmo>().SwitchGizmo(component);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void DestroySelected(IButton button, ButtonEventData eventData)
	{
		EditSettings? activeSetting = Settings.GetActiveSetting<EditSettings>();
		if (activeSetting != null && !activeSetting.ConfirmSlotDestroy.Value)
		{
			DestroySelected();
			return;
		}
		StartTask(async delegate
		{
			ContextMenu contextMenu = await base.LocalUser.OpenContextMenu(this, eventData.source.Slot, new ContextMenuOptions
			{
				disableFlick = true
			});
			if (contextMenu != null)
			{
				contextMenu.AddItem("Tools.Dev.ConfirmDestroySelected".AsLocaleKey("<b><color=#fcc>{0}</color></b>"), OfficialAssets.Graphics.Icons.Item.DestroySelect, new colorX?(colorX.Red), DestroyConfirm);
				for (int i = 0; i < 4; i++)
				{
					contextMenu.AddItem("General.Cancel".AsLocaleKey(), (Uri?)null, new colorX?(colorX.Gray), (ButtonEventHandler)OnCancelMenu);
				}
			}
		});
	}

	[SyncMethod(typeof(Delegate), null)]
	private void DestroyConfirm(IButton button, ButtonEventData eventData)
	{
		DestroySelected();
		base.LocalUser.CloseContextMenu(this);
	}

	private void DestroySelected()
	{
		_currentGizmo.Target?.UndoableDestroyPreservingAssets();
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnCancelMenu(IButton button, ButtonEventData eventData)
	{
		base.LocalUser.CloseContextMenu(this);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnGizmoReplaced(SlotGizmo gizmo, SlotGizmo newGizmo)
	{
		if (_currentGizmo.Target == gizmo.TargetSlot)
		{
			_currentGizmo.Target = newGizmo.TargetSlot;
			newGizmo.GizmoReplaced.Target = OnGizmoReplaced;
		}
		if (_previousGizmo.Target == gizmo.TargetSlot)
		{
			_previousGizmo.Target = newGizmo.TargetSlot;
			newGizmo.GizmoReplaced.Target = OnGizmoReplaced;
		}
	}

	public override void OnEquipped()
	{
		base.OnEquipped();
		Settings.GetActiveSetting<EditSettings>();
		_input = new DevToolInputs(base.ActiveHandler.Side.Value);
		base.Input.RegisterInputGroup(_input, this);
	}

	public override void OnDequipped()
	{
		if (_input != null)
		{
			base.Input.UnregisterInputGroup(ref _input);
		}
		UnregisterCursorUnlock();
		base.OnDequipped();
	}

	protected override void OnDispose()
	{
		if (_input != null)
		{
			base.Input.UnregisterInputGroup(ref _input);
		}
		UnregisterCursorUnlock();
		activeGizmo = null;
		base.OnDispose();
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		SelectionMode = new Sync<Selection>();
		InteractionMode = new Sync<Interaction>();
		_selectedAnchor = new SyncRef<PointAnchor>();
		_selectedAnchorHighlight = new SyncRef<Slot>();
		_material = new DriveRef<OverlayFresnelMaterial>();
		_currentGizmo = new SyncRef<Slot>();
		_previousGizmo = new SyncRef<Slot>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => _equipLink, 
			4 => TipReference, 
			5 => BlockGripEquip, 
			6 => BlockRemoteEquip, 
			7 => EquipName, 
			8 => _overrideActiveTool, 
			9 => _gripPosesGenerated, 
			10 => SelectionMode, 
			11 => InteractionMode, 
			12 => _selectedAnchor, 
			13 => _selectedAnchorHighlight, 
			14 => _material, 
			15 => _currentGizmo, 
			16 => _previousGizmo, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static DevTool __New()
	{
		return new DevTool();
	}
}
