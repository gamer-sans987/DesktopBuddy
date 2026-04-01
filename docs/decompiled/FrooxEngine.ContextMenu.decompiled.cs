using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Elements.Core;
using FrooxEngine.UIX;

namespace FrooxEngine;

[Category(new string[] { "Radiant UI/Context Menu" })]
public class ContextMenu : UserRootComponent
{
	public enum State
	{
		Closed,
		Opening,
		Opened
	}

	public const float CLOSE_SPEED = 6f;

	public const float OPEN_SPEED = 6f;

	public const float CLOSED_SIZE = 0.5f;

	public const float MAIN_SIZE = 0.85f;

	public const float EXIT_START = 0.9775f;

	public const float EXIT_END = 1.4875001f;

	public const float DEFAULT_INNER_SIZE = 0.3f;

	public const float HIGHLIGHT_INNER_SIZE = 0.4f;

	public const float FLICK_INNER_SIZE = 0.05f;

	public const float SELECTED_ICON_SIZE = 0.3f;

	public const float TEXT_OUTLINE_THICKNESS = 0.2f;

	public readonly SyncRef<User> Owner;

	public readonly SyncRef<Slot> Pointer;

	public readonly Sync<float> Separation;

	public readonly Sync<float2> LabelSize;

	public readonly Sync<float> RadiusRatio;

	protected readonly SyncRef _currentSummoner;

	protected readonly SyncRef<Canvas> _canvas;

	protected readonly SyncRef<ArcLayout> _arcLayout;

	protected readonly FieldDrive<bool> _canvasActive;

	protected readonly FieldDrive<bool> _colliderEnabled;

	protected readonly SyncRef<Image> _iconImage;

	protected readonly FieldDrive<float> _separation;

	protected readonly FieldDrive<float2> _offsetMin;

	protected readonly FieldDrive<float2> _offsetMax;

	protected readonly SyncRef<OutlinedArc> _innerCircle;

	protected readonly SyncRef<Button> _innerCircleButton;

	protected readonly FieldDrive<float2> _innerCircleAnchorMin;

	protected readonly FieldDrive<float2> _innerCircleAnchorMax;

	protected readonly SyncRef<Slot> _itemsRoot;

	protected readonly SyncRef<UI_CircleSegment> _arcMaterial;

	protected readonly SyncRef<UI_TextUnlitMaterial> _fontMaterial;

	protected readonly SyncRef<UI_UnlitMaterial> _spriteMaterial;

	protected readonly FieldDrive<bool> _arcOverlay;

	protected readonly FieldDrive<bool> _fontOverlay;

	protected readonly FieldDrive<bool> _spriteOverlay;

	protected readonly FieldDrive<ZTest> _arcZTest;

	protected readonly FieldDrive<ZTest> _fontZTest;

	protected readonly FieldDrive<ZTest> _spriteZTest;

	protected readonly FieldDrive<ZWrite> _zwriteArc;

	protected readonly FieldDrive<ZWrite> _zwriteText;

	protected readonly FieldDrive<int> _arcRenderQueue;

	protected readonly FieldDrive<int> _fontRenderQueue;

	protected readonly FieldDrive<int> _spriteRenderQueue;

	protected readonly FieldDrive<int> _canvasOffset;

	protected readonly FieldDrive<colorX> _fillFade;

	protected readonly FieldDrive<colorX> _outlineFade;

	protected readonly FieldDrive<colorX> _textFade;

	protected readonly FieldDrive<colorX> _iconFade;

	protected readonly Sync<float> _lerp;

	protected readonly Sync<State> _state;

	protected readonly Sync<bool> _flickModeActive;

	protected readonly Sync<bool> _flickEnabled;

	protected readonly Sync<bool> _hidden;

	protected readonly SyncRef<ContextMenuItem> _selectedItem;

	private float? _speedOverride;

	private UIBuilder _ui;

	private float _openLerp;

	private float _exitLerp;

	private bool _exitLerpActivated;

	private int _activeRequests;

	private float? _innerLerp;

	private bool _unlockRegistered;

	private ContextMenuInputs _inputs;

	private ContextMenuItem _highlightedItem;

	private bool _mysterySettingRegistered;

	public IWorldElement CurrentSummoner => _currentSummoner.Target;

	public float Lerp
	{
		get
		{
			if (_hidden.Value && !base.IsUnderLocalUser)
			{
				return 0f;
			}
			return _lerp.Value;
		}
	}

	public State MenuState => _state.Value;

	public bool ColliderEnabled => _colliderEnabled.Target.Value;

	public bool IsOpened
	{
		get
		{
			if (MenuState != State.Closed)
			{
				return ColliderEnabled;
			}
			return false;
		}
	}

	public bool IsVisible
	{
		get
		{
			if (!(Lerp > 0f))
			{
				return MenuState == State.Opening;
			}
			return true;
		}
	}

	public bool HiddenToOthers => _hidden.Value;

	public Canvas Canvas => _canvas.Target;

	protected override void OnAwake()
	{
		base.OnAwake();
		Separation.Value = 6f;
		LabelSize.Value = new float2(400f, 120f);
		RadiusRatio.Value = 0.5f;
	}

	protected override void OnStart()
	{
		base.OnStart();
		if (FrooxEngine.Engine.IsAprilFools && Owner.Target == base.LocalUser)
		{
			_mysterySettingRegistered = true;
			Settings.RegisterValueChanges<MysterySettings>(OnMysterySettingsUpdated);
		}
	}

	protected override void OnDispose()
	{
		base.OnDispose();
		if (_mysterySettingRegistered)
		{
			Settings.UnregisterValueChanges<MysterySettings>(OnMysterySettingsUpdated);
		}
	}

	private void OnMysterySettingsUpdated(MysterySettings settings)
	{
		RunSynchronously(delegate
		{
			ArcLayout component = _itemsRoot.Target.GetComponent<ArcLayout>();
			if (component != null)
			{
				if (settings.Difficulty.Value == MysterySettings.ResoniteDifficulty.Hard)
				{
					if (!component.Offset.IsDriven)
					{
						Panner1D panner1D = component.Slot.AttachComponent<Panner1D>();
						panner1D.Speed = 90f;
						panner1D.Repeat = 360f;
						panner1D.Target = component.Offset;
					}
				}
				else if (component.Offset.IsDriven)
				{
					component.Offset.ActiveLink?.FindNearestParent<Panner1D>()?.Destroy();
				}
			}
		});
	}

	protected override void OnAttach()
	{
		base.OnAttach();
		Owner.Target = base.LocalUser;
		Slot slot = base.Slot.AddSlot("Visual");
		_canvas.Target = slot.AttachComponent<Canvas>();
		_canvas.Target.Size.Value = new float2(512f, 512f);
		_canvas.Target.AcceptPhysicalTouch.Value = false;
		_canvas.Target.HighPriorityIntegration.Value = true;
		_canvas.Target.AcceptExistingTouch.Value = true;
		_canvas.Target.Collider.Target.Enabled = false;
		_canvasOffset.Target = _canvas.Target.StartingOffset;
		_colliderEnabled.Target = _canvas.Target.Collider.Target.EnabledField;
		_canvasActive.Target = _canvas.Target.Slot.ActiveSelf_Field;
		slot.LocalScale = float3.One * (0.2f / _canvas.Target.Size.Value.y);
		Slot slot2 = slot.AddSlot("Radial Menu");
		RectTransform rectTransform = slot2.AttachComponent<RectTransform>();
		Slot slot3 = slot2.AddSlot("ArcLayout");
		ArcLayout arcLayout = slot3.AttachComponent<ArcLayout>();
		_arcLayout.Target = arcLayout;
		_separation.Target = arcLayout.Separation;
		_offsetMin.Target = rectTransform.OffsetMin;
		_offsetMax.Target = rectTransform.OffsetMax;
		rectTransform.AnchorMin.Value = float2.One * 0.5f;
		rectTransform.AnchorMax.Value = float2.One * 0.5f;
		_itemsRoot.Target = slot3;
		_arcMaterial.Target = base.Slot.AttachComponent<UI_CircleSegment>();
		_arcMaterial.Target.ZWrite.Value = ZWrite.On;
		_arcMaterial.Target.OffsetFactor.Value = 1f;
		_arcMaterial.Target.OffsetUnits.Value = 100f;
		_arcMaterial.Target.Overlay.Value = true;
		_arcMaterial.Target.ZTest.Value = ZTest.Always;
		_zwriteArc.Target = _arcMaterial.Target.ZWrite;
		_fillFade.Target = _arcMaterial.Target.OutlineTint;
		_outlineFade.Target = _arcMaterial.Target.FillTint;
		_fontMaterial.Target = base.Slot.AttachComponent<UI_TextUnlitMaterial>();
		_fontMaterial.Target.OutlineThickness.Value = 0.2f;
		_fontMaterial.Target.FaceDilate.Value = 0.2f;
		_fontMaterial.Target.OutlineColor.Value = colorX.Black;
		_zwriteText.Target = _fontMaterial.Target.ZWrite;
		_textFade.Target = _fontMaterial.Target.TintColor;
		_spriteMaterial.Target = base.Slot.AttachComponent<UI_UnlitMaterial>();
		_iconFade.Target = _spriteMaterial.Target.Tint;
		Slot slot4 = slot2.AddSlot("Center Circle");
		RectTransform rectTransform2 = slot4.AttachComponent<RectTransform>();
		OutlinedArc outlinedArc = slot4.AttachComponent<OutlinedArc>();
		Button button = slot4.AttachComponent<Button>();
		button.RequireInitialPress.Value = false;
		button.Pressed.Target = OnInnerPressed;
		button.Released.Target = OnInnerReleased;
		button.PassThroughHorizontalMovement.Value = false;
		button.PassThroughVerticalMovement.Value = false;
		outlinedArc.InnerRadiusRatio.Value = -0.1f;
		outlinedArc.Material.Target = _arcMaterial.Target;
		_innerCircle.Target = outlinedArc;
		_innerCircleButton.Target = button;
		_innerCircleAnchorMin.Target = rectTransform2.AnchorMin;
		_innerCircleAnchorMax.Target = rectTransform2.AnchorMax;
		UIBuilder.SetupButtonColor(button, outlinedArc);
		ClearInnerCircleColor();
		Slot slot5 = slot4.AddSlot("Icon");
		RectTransform rectTransform3 = slot5.AttachComponent<RectTransform>();
		Image image = slot5.AttachComponent<Image>();
		_iconImage.Target = image;
		_iconImage.Target.Enabled = false;
		image.Material.Target = _spriteMaterial.Target;
		rectTransform3.AnchorMin.Value = new float2(0.25f, 0.25f);
		rectTransform3.AnchorMax.Value = new float2(0.75f, 0.75f);
		_arcZTest.Target = _arcMaterial.Target.ZTest;
		_fontZTest.Target = _fontMaterial.Target.ZTest;
		_spriteZTest.Target = _spriteMaterial.Target.ZTest;
		_arcOverlay.Target = _arcMaterial.Target.Overlay;
		_fontOverlay.Target = _fontMaterial.Target.Overlay;
		_spriteOverlay.Target = _spriteMaterial.Target.Overlay;
		_arcRenderQueue.Target = _arcMaterial.Target.RenderQueue;
		_fontRenderQueue.Target = _fontMaterial.Target.RenderQueue;
		_spriteRenderQueue.Target = _spriteMaterial.Target.RenderQueue;
	}

	protected override void OnChanges()
	{
		base.OnChanges();
		if (Lerp <= 0f)
		{
			_canvasActive.Target.Value = false;
		}
		else
		{
			float num = MathX.SmootherStep(Lerp);
			colorX value = new colorX(1f, 1f, 1f, num);
			ZWrite value2 = ((num >= 1f) ? ZWrite.On : ZWrite.Auto);
			_zwriteArc.Target.Value = value2;
			_zwriteText.Target.Value = value2;
			_fillFade.Target.Value = value;
			_outlineFade.Target.Value = value;
			_textFade.Target.Value = value;
			_iconFade.Target.Value = value;
			_separation.Target.Value = (float)Separation * ((1f - num) * 2f + 1f);
			float2 from = _canvas.Target.Size.Value;
			float2 v = ((_state.Value != State.Opening) ? MathX.Lerp(in from, from * 0.85f, num) : MathX.Lerp(from * 0.5f, from * 0.85f, num)) * 0.5f;
			_offsetMin.Target.Value = -v;
			_offsetMax.Target.Value = v;
			_canvasActive.Target.Value = true;
			_colliderEnabled.Target.Value = (Owner.Target == null || Owner.Target == base.LocalUser) && (Lerp >= 1f || _state.Value == State.Opening);
		}
		bool flag = Owner.Target == base.LocalUser;
		bool flag2 = flag && IsVisible;
		if (flag2 != _unlockRegistered)
		{
			if (flag2)
			{
				base.Input.RegisterCursorUnlock(this);
			}
			else
			{
				base.Input.UnregisterCursorUnlock(this);
			}
			_unlockRegistered = flag2;
		}
		ZTest value3 = (flag ? ZTest.Always : ZTest.LessOrEqual);
		int value4 = (flag ? 4000 : (-1));
		bool value5 = flag;
		_arcZTest.Target.Value = value3;
		_fontZTest.Target.Value = value3;
		_spriteZTest.Target.Value = value3;
		_arcOverlay.Target.Value = value5;
		_fontOverlay.Target.Value = value5;
		_spriteOverlay.Target.Value = value5;
		_arcRenderQueue.Target.Value = value4;
		_fontRenderQueue.Target.Value = value4;
		_spriteRenderQueue.Target.Value = value4;
		_canvasOffset.Target.Value = ((!flag) ? (-32000) : 0);
		if (base.Engine.Platform == Platform.Android)
		{
			_arcMaterial.Target.OverlayTint.Value = colorX.White;
			_fontMaterial.Target.OverlayTint.Value = colorX.White;
			_spriteMaterial.Target.OverlayTint.Value = colorX.White;
		}
	}

	protected override void OnCommonUpdate()
	{
		if (Lerp > 0f)
		{
			float target = 0f;
			if (_flickModeActive.Value)
			{
				target = -1f;
			}
			else if (_selectedItem.Target != null)
			{
				target = 1f;
			}
			float num = MathX.ConstantLerp(_innerLerp.GetValueOrDefault(), target, base.Time.Delta * (_flickModeActive.Value ? 12f : 6f));
			if (num != _innerLerp || _flickModeActive.Value)
			{
				_innerLerp = num;
				float a = 0.3f;
				float b = 0.4f;
				if (!_flickEnabled.Value)
				{
					a = 0f;
				}
				if (num < 0f)
				{
					a = 0.3f;
					b = 0.05f;
				}
				float num2 = RadiusRatio.Value * MathX.Lerp(a, b, MathX.SmootherStep(MathX.Abs(num)));
				float2 b2 = float2.Zero;
				if (num < 0f)
				{
					float3 pointerLocalPoint = GetPointerLocalPoint();
					float2 v = pointerLocalPoint.xy / (float2)_canvas.Target.Size;
					b2 = v * (0f - num);
					v = v.Normalized;
					float num3 = _canvas.Target.Size.Value.x * 0.5f;
					float num4 = MathX.Lerp(RadiusRatio, 0.85f, 0.5f);
					if (Owner.Target == base.LocalUser && pointerLocalPoint.Magnitude > num3 * num4 && _state.Value != State.Closed)
					{
						ContextMenuItem contextMenuItem = FindMenuItem(v);
						if (contextMenuItem != null && PressMenuItem(contextMenuItem, pointerLocalPoint))
						{
							_state.Value = State.Closed;
						}
					}
				}
				_innerCircleAnchorMin.Target.Value = float2.One * (0.5f - num2) + b2;
				_innerCircleAnchorMax.Target.Value = float2.One * (0.5f + num2) + b2;
			}
		}
		if (Owner.Target != base.LocalUser)
		{
			return;
		}
		ContextMenuItem contextMenuItem2 = null;
		if (IsVisible)
		{
			if (_inputs == null)
			{
				_inputs = new ContextMenuInputs();
				base.Input.RegisterInputGroup(_inputs, this);
			}
			float2 @float = _inputs.SelectDirection;
			if (@float.Magnitude > 0.5f)
			{
				ContextMenuItem contextMenuItem3 = FindMenuItem(@float.Normalized);
				if (contextMenuItem3 != null)
				{
					if (_inputs.Select.Pressed)
					{
						PressMenuItem(contextMenuItem3, float3.Zero);
					}
					contextMenuItem2 = contextMenuItem3;
				}
			}
		}
		else if (_inputs != null)
		{
			base.Input.UnregisterInputGroup(ref _inputs);
		}
		if (contextMenuItem2 != _highlightedItem)
		{
			if (_highlightedItem != null && _highlightedItem.IsRemoved)
			{
				_highlightedItem = null;
			}
			if (contextMenuItem2 != null && contextMenuItem2.IsRemoved)
			{
				contextMenuItem2 = null;
			}
			_highlightedItem?.ClearHighlighted();
			_highlightedItem = contextMenuItem2;
			_highlightedItem?.SetHighlighted();
		}
		float value = _speedOverride ?? 6f;
		float value2 = _speedOverride ?? 6f;
		value = MathX.Clamp(MathX.FilterInvalid(value, 6f), 0.1f, 100f);
		value2 = MathX.Clamp(MathX.FilterInvalid(value2, 6f), 0.1f, 100f);
		if (_state.Value == State.Closed)
		{
			if (_lerp.Value > 0f)
			{
				_lerp.Value = MathX.Clamp01(_lerp.Value - value * base.Time.Delta);
			}
		}
		else
		{
			if (_state.Value == State.Opening)
			{
				_exitLerpActivated = base.InputInterface.VR_Active;
				_openLerp = MathX.Clamp01(_openLerp + value2 * base.Time.Delta);
				if (_openLerp >= 1f)
				{
					_state.Value = State.Opened;
					_speedOverride = null;
				}
			}
			else
			{
				_openLerp = 1f;
			}
			float val = 1f;
			if (Pointer.Target != null)
			{
				float magnitude = GetPointerLocalPoint().Magnitude;
				float num5 = _canvas.Target.Size.Value.x * 0.5f;
				float num6 = MathX.Clamp01(MathX.InverseLerp(0.9775f * num5, 1.4875001f * num5, magnitude));
				if (num6 <= 0f)
				{
					_exitLerpActivated = true;
				}
				else if (!_exitLerpActivated)
				{
					num6 = 0f;
				}
				if (num6 >= 1f)
				{
					_state.Value = State.Closed;
				}
				_exitLerp = MathX.ConstantLerp(_exitLerp, num6, value * base.Time.Delta);
				val = 1f - _exitLerp;
			}
			float num7 = MathX.Min(_openLerp, val);
			_lerp.Value = num7;
			if (_state.Value == State.Opened && num7 <= 0f)
			{
				_state.Value = State.Closed;
				_speedOverride = null;
			}
		}
		if (_state.Value == State.Closed)
		{
			_openLerp = 0f;
		}
	}

	private ContextMenuItem FindMenuItem(float2 normalizedPoint)
	{
		float num = _canvas.Target.Size.Value.x * 0.5f;
		float num2 = MathX.Lerp(RadiusRatio, 0.85f, 0.5f);
		List<RectTransform> list = Pool.BorrowList<RectTransform>();
		float2 @float = normalizedPoint * num * num2;
		_canvas.Target.GetIntersectingTransforms(@float.xy, list);
		try
		{
			foreach (RectTransform item in list)
			{
				ContextMenuItem component = item.Slot.GetComponent<ContextMenuItem>();
				if (component != null)
				{
					return component;
				}
			}
		}
		finally
		{
			Pool.Return(ref list);
		}
		return null;
	}

	private bool PressMenuItem(ContextMenuItem menuItem, float3 point)
	{
		Button component = menuItem.Slot.GetComponent<Button>();
		if (!component.Enabled)
		{
			return false;
		}
		component.SimulatePress(0.1f, new ButtonEventData(this, _canvas.Target.Slot.LocalPointToGlobal(in point), float2.Zero, float2.Zero));
		return true;
	}

	private float3 GetPointerLocalPoint()
	{
		if (Pointer.Target == null)
		{
			return float3.Zero;
		}
		float3 origin;
		float3 direction;
		if (base.InputInterface.ScreenActive)
		{
			PointerInteractionController pointerInteractionController = base.LocalUserRoot?.GetRegisteredComponent<PointerInteractionController>();
			Pointer pointer = pointerInteractionController?.PrimaryPointer.pointer;
			if (pointer == null)
			{
				return float3.Zero;
			}
			pointerInteractionController.PointerToRay(pointer, out origin, out direction);
		}
		else
		{
			origin = Pointer.Target.GlobalPosition;
			direction = Pointer.Target.Forward;
		}
		Slot slot = _canvas.Target.Slot;
		return MathX.RayPlaneIntersection(slot.GlobalPointToLocal(in origin), slot.GlobalDirectionToLocal(in direction), float3.Zero, float3.Backward);
	}

	private void CheckOwner()
	{
		if (base.LocalUser != Owner.Target)
		{
			throw new InvalidOperationException("Calling user isn't the owner of this context menu, cannot open the menu!");
		}
	}

	private void CheckBuildingUI()
	{
		if (_ui == null)
		{
			throw new InvalidOperationException("The menu generation has finished, cannot add new items!");
		}
	}

	public void Close()
	{
		_state.Value = State.Closed;
	}

	[SyncMethod(typeof(Delegate), null)]
	public void CloseMenu(IButton button, ButtonEventData eventData)
	{
		Close();
	}

	public Task<bool> OpenMenu(IWorldElement summoner, Slot pointer, ContextMenuOptions options = default(ContextMenuOptions))
	{
		return StartTask(async () => await OpenMenuIntern(summoner, pointer, options));
	}

	private async Task<bool> OpenMenuIntern(IWorldElement summoner, Slot pointer, ContextMenuOptions options)
	{
		CheckOwner();
		if (_state.Value != State.Closed)
		{
			_speedOverride = options.speedOverride;
			_state.Value = State.Closed;
		}
		_activeRequests++;
		while (_lerp.Value > 0f)
		{
			await default(NextUpdate);
		}
		_activeRequests--;
		if (_activeRequests > 0)
		{
			return false;
		}
		Pointer.Target = pointer;
		_currentSummoner.Target = summoner;
		_state.Value = State.Opening;
		_lerp.Value = 0f;
		_openLerp = 0f;
		_exitLerp = 0f;
		_innerLerp = null;
		_flickModeActive.Value = false;
		_canvas.Target.LaserPassThrough.Value = options.disableFlick;
		_speedOverride = options.speedOverride;
		if (options.counterClockwise)
		{
			_arcLayout.Target.ItemDirection.Value = ArcLayout.Direction.CounterClockwise;
			_arcLayout.Target.Offset.Value = 180f;
		}
		else
		{
			_arcLayout.Target.ItemDirection.Value = ArcLayout.Direction.Clockwise;
			_arcLayout.Target.Offset.Value = 0f;
		}
		_flickEnabled.Value = !options.disableFlick;
		_hidden.Value = options.hidden;
		StartNewMenu();
		return true;
	}

	private void StartNewMenu()
	{
		_itemsRoot.Target.DestroyChildren();
		_ui = new UIBuilder(_itemsRoot.Target);
	}

	public ContextMenuItem AddItem(in LocaleString label, Uri? icon, in colorX? color, ButtonEventHandler action)
	{
		return AddItem(in label, null, icon, in color, action);
	}

	public ContextMenuItem AddItem(in LocaleString label, IAssetProvider<ITexture2D> icon, in colorX? color, ButtonEventHandler action)
	{
		return AddItem(in label, icon, null, in color, action);
	}

	public ContextMenuItem AddItem(in LocaleString label, IAssetProvider<Sprite> sprite, in colorX? color)
	{
		return AddItem(in label, null, null, sprite, in color);
	}

	public ContextMenuItem AddRefItem<T>(in LocaleString label, Uri icon, in colorX? color, ButtonEventHandler<T> action, T argument) where T : class, IWorldElement
	{
		return AddRefItem(in label, null, icon, in color, action, argument);
	}

	public ContextMenuItem AddDelegateItem<T>(in LocaleString label, Uri icon, in colorX? color, ButtonEventHandler<T> action, T argument) where T : Delegate
	{
		return AddDelegateItem(in label, null, icon, in color, action, argument);
	}

	private ContextMenuItem AddItem(in LocaleString label, IAssetProvider<ITexture2D> texture, Uri? icon, in colorX? color, ButtonEventHandler action)
	{
		ContextMenuItem contextMenuItem = AddItem(in label, texture, icon, null, in color);
		contextMenuItem.Button.SetupAction(action);
		return contextMenuItem;
	}

	private ContextMenuItem AddRefItem<T>(in LocaleString label, IAssetProvider<ITexture2D> texture, Uri icon, in colorX? color, ButtonEventHandler<T> action, T argument) where T : class, IWorldElement
	{
		ContextMenuItem contextMenuItem = AddItem(in label, texture, icon, null, in color);
		contextMenuItem.Button.SetupRefAction(action, argument);
		return contextMenuItem;
	}

	private ContextMenuItem AddDelegateItem<T>(in LocaleString label, IAssetProvider<ITexture2D> texture, Uri icon, in colorX? color, ButtonEventHandler<T> action, T argument) where T : Delegate
	{
		ContextMenuItem contextMenuItem = AddItem(in label, texture, icon, null, in color);
		contextMenuItem.Button.SetupDelegateAction(action, argument);
		return contextMenuItem;
	}

	public ContextMenuItem AddItem(in LocaleString label, Uri? icon, bool colorFromImage = true)
	{
		return AddItem(in label, null, icon, null, (colorX?)null, colorFromImage);
	}

	public ContextMenuItem AddItem(in LocaleString label, IAssetProvider<ITexture2D> icon, in colorX? color)
	{
		return AddItem(in label, icon, null, null, in color);
	}

	public ContextMenuItem AddItem(in LocaleString label, Uri icon, in colorX? color)
	{
		return AddItem(in label, null, icon, null, in color);
	}

	public ContextMenuItem AddItem(in LocaleString label, Uri icon, in colorX? color, bool colorFromImage = true)
	{
		return AddItem(in label, null, icon, null, in color, colorFromImage);
	}

	private ContextMenuItem AddItem(in LocaleString label, IAssetProvider<ITexture2D>? texture, Uri? icon, IAssetProvider<Sprite>? sprite, in colorX? color, bool colorFromImage = true)
	{
		CheckOwner();
		CheckBuildingUI();
		ArcData arcData = ((label.content == null) ? _ui.Arc((LocaleString)"") : _ui.Arc(in label));
		if (sprite == null)
		{
			if (texture != null)
			{
				SpriteProvider spriteProvider = arcData.image.Slot.AttachComponent<SpriteProvider>();
				spriteProvider.Texture.Target = texture;
				sprite = spriteProvider;
			}
			else
			{
				SpriteProvider spriteProvider2 = arcData.image.Slot.AttachSprite(icon, uncompressed: false, evenNull: true);
				texture = spriteProvider2.Texture.Target;
				sprite = spriteProvider2;
			}
		}
		arcData.arc.InnerRadiusRatio.Value = RadiusRatio;
		arcData.arcLayout.LabelSize.Value = LabelSize;
		arcData.arcLayout.NestedSizeRatio.Value = 0.65f;
		arcData.arc.OutlineThickness.Value = 3f;
		arcData.arc.RoundedCornerRadius.Value = 16f;
		arcData.arc.Material.Target = _arcMaterial.Target;
		arcData.text.Material.Target = _fontMaterial.Target;
		arcData.text.Color.Value = RadiantUI_Constants.TEXT_COLOR;
		arcData.text.Size.Value = 50f;
		arcData.text.AutoSizeMax.Value = 50f;
		arcData.image.Sprite.Target = sprite;
		arcData.image.Material.Target = _spriteMaterial.Target;
		ContextMenuItem contextMenuItem = arcData.button.Slot.AttachComponent<ContextMenuItem>();
		arcData.button.HoverVibrate.Value = VibratePreset.Medium;
		arcData.button.PressVibrate.Value = VibratePreset.Long;
		InteractionElement.ColorDriver colorDriver = arcData.button.ColorDrivers.Add();
		colorDriver.ColorDrive.Target = arcData.image.Tint;
		colorDriver.NormalColor.Value = colorX.White;
		colorDriver.HighlightColor.Value = colorX.White;
		colorDriver.PressColor.Value = colorX.White;
		colorDriver.DisabledColor.Value = colorX.White.SetA(0.53f);
		contextMenuItem.Initialize(this, arcData.arc, arcData.button);
		contextMenuItem.Icon.Target = arcData.image;
		contextMenuItem.Sprite.Target = sprite as SpriteProvider;
		contextMenuItem.Label.Target = arcData.text.Content;
		if (color.HasValue)
		{
			contextMenuItem.Color.Value = color.Value;
		}
		else if (colorFromImage)
		{
			BitmapAssetMetadata bitmapAssetMetadata = contextMenuItem.Slot.AttachComponent<BitmapAssetMetadata>();
			bitmapAssetMetadata.Asset.Target = texture as IAssetProvider<Texture2D>;
			contextMenuItem.Color.DriveFrom(bitmapAssetMetadata.AverageVisibleHSV);
		}
		contextMenuItem.UpdateColor();
		return contextMenuItem;
	}

	public void AddToggleItem(IField<bool> field, LocaleString trueLabel, LocaleString falseLabel, in colorX trueColor, in colorX falseColor, Uri trueIcon = null, Uri falseIcon = null)
	{
		AddItem((LocaleString)"", null, colorFromImage: false).SetupToggle(field, new OptionDescription<bool>(reference: true, trueLabel, trueColor, trueIcon), new OptionDescription<bool>(reference: false, falseLabel, falseColor, falseIcon));
	}

	public void AddEnumShiftItem<E>(IField<E> field, List<OptionDescription<E>> labels, bool colorFromIcon = true) where E : Enum
	{
		AddEnumShiftItem(field, "{0}", null, null, labels, colorFromIcon);
	}

	public void AddEnumShiftItem<E>(IField<E> field, string label, Uri? defaultIcon, colorX? defaultColor, List<OptionDescription<E>> labels = null, bool colorFromIcon = true) where E : Enum
	{
		List<E> list = Enum.GetValues(typeof(E)).Cast<E>().ToList();
		List<OptionDescription<E>> list2 = new List<OptionDescription<E>>();
		for (int i = 0; i < list.Count; i++)
		{
			E val = list[i];
			LocaleString label2 = string.Format(label, list[i].ToString().BeautifyName());
			colorX? colorX = defaultColor;
			Uri uri = defaultIcon;
			if (labels != null)
			{
				foreach (OptionDescription<E> label3 in labels)
				{
					OptionDescription<E> current = label3;
					if (EqualityComparer<E>.Default.Equals(val, current.reference))
					{
						if (current.label != (LocaleString)null)
						{
							label2 = current.label;
						}
						colorX = current.color ?? colorX;
						uri = current.spriteUrl ?? uri;
						break;
					}
				}
			}
			list2.Add(new OptionDescription<E>(val, label2, colorX, uri));
		}
		ContextMenuItem contextMenuItem = ((!colorFromIcon) ? AddItem((LocaleString)"", defaultIcon, in defaultColor, colorFromImage: false) : AddItem((LocaleString)"", defaultIcon));
		contextMenuItem.AttachOptionDescriptionDriver<E>(setupLabel: true, !colorFromIcon);
		contextMenuItem.Button.SetupEnumShift(field, list2, !colorFromIcon);
	}

	public void AddEnumSetItems<E>(IField<E> field, string label, colorX enabledColor, colorX disabledColor, List<OptionDescription<E>> labels = null) where E : Enum
	{
		List<E> list = Enum.GetValues(typeof(E)).Cast<E>().ToList();
		ValueSetOption<E>[] array = new ValueSetOption<E>[list.Count];
		for (int i = 0; i < list.Count; i++)
		{
			E y = list[i];
			colorX? enabledColor2 = null;
			LocaleString label2 = string.Format(label, list[i].ToString().BeautifyName());
			Uri icon = null;
			if (labels != null)
			{
				foreach (OptionDescription<E> label3 in labels)
				{
					OptionDescription<E> current = label3;
					if (EqualityComparer<E>.Default.Equals(current.reference, y))
					{
						enabledColor2 = current.color;
						if (current.label != (LocaleString)null)
						{
							label2 = current.label;
						}
						icon = current.spriteUrl;
						break;
					}
				}
			}
			array[i] = new ValueSetOption<E>(in label2, list[i], icon, enabledColor2);
		}
		AddValueSetItems(field, enabledColor, disabledColor, array);
	}

	public void AddValueSetItems<T>(IField<T> field, colorX enabledColor, colorX disabledColor, params ValueSetOption<T>[] options)
	{
		for (int i = 0; i < options.Length; i++)
		{
			ValueSetOption<T> valueSetOption = options[i];
			AddItem(in valueSetOption.label, valueSetOption.icon, new colorX?(disabledColor), colorFromImage: false).SetupValueSet(field, valueSetOption.value, valueSetOption.enabledColor ?? enabledColor, disabledColor);
		}
	}

	private void ClearInnerCircleColor()
	{
		ContextMenuItem.UpdateColor(_innerCircleButton, colorX.Gray, highlight: false);
	}

	internal void ItemSelected(ContextMenuItem item)
	{
		ContextMenuItem.UpdateColor(_innerCircleButton, (colorX)item.Color, highlight: false);
		_selectedItem.Target = item;
		if (item.Sprite.Target == null)
		{
			_iconImage.Target.Enabled = false;
			return;
		}
		_iconImage.Target.Enabled = true;
		_iconImage.Target.Sprite.Target = item.Sprite.Target;
	}

	internal void ItemPressed(ContextMenuItem item)
	{
		RunInUpdates(1, delegate
		{
			ItemSelected(item);
		});
	}

	internal void ItemDeselected(ContextMenuItem item)
	{
		if (_selectedItem.Target == item)
		{
			ClearInnerCircleColor();
			_iconImage.Target.Enabled = false;
			_selectedItem.Target = null;
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnInnerPressed(IButton button, ButtonEventData eventData)
	{
		if (_flickEnabled.Value)
		{
			_flickModeActive.Value = true;
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnInnerReleased(IButton button, ButtonEventData eventData)
	{
		_flickModeActive.Value = false;
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		Owner = new SyncRef<User>();
		Pointer = new SyncRef<Slot>();
		Separation = new Sync<float>();
		LabelSize = new Sync<float2>();
		RadiusRatio = new Sync<float>();
		_currentSummoner = new SyncRef();
		_canvas = new SyncRef<Canvas>();
		_arcLayout = new SyncRef<ArcLayout>();
		_canvasActive = new FieldDrive<bool>();
		_colliderEnabled = new FieldDrive<bool>();
		_iconImage = new SyncRef<Image>();
		_separation = new FieldDrive<float>();
		_offsetMin = new FieldDrive<float2>();
		_offsetMax = new FieldDrive<float2>();
		_innerCircle = new SyncRef<OutlinedArc>();
		_innerCircleButton = new SyncRef<Button>();
		_innerCircleAnchorMin = new FieldDrive<float2>();
		_innerCircleAnchorMax = new FieldDrive<float2>();
		_itemsRoot = new SyncRef<Slot>();
		_arcMaterial = new SyncRef<UI_CircleSegment>();
		_fontMaterial = new SyncRef<UI_TextUnlitMaterial>();
		_spriteMaterial = new SyncRef<UI_UnlitMaterial>();
		_arcOverlay = new FieldDrive<bool>();
		_fontOverlay = new FieldDrive<bool>();
		_spriteOverlay = new FieldDrive<bool>();
		_arcZTest = new FieldDrive<ZTest>();
		_fontZTest = new FieldDrive<ZTest>();
		_spriteZTest = new FieldDrive<ZTest>();
		_zwriteArc = new FieldDrive<ZWrite>();
		_zwriteText = new FieldDrive<ZWrite>();
		_arcRenderQueue = new FieldDrive<int>();
		_fontRenderQueue = new FieldDrive<int>();
		_spriteRenderQueue = new FieldDrive<int>();
		_canvasOffset = new FieldDrive<int>();
		_fillFade = new FieldDrive<colorX>();
		_outlineFade = new FieldDrive<colorX>();
		_textFade = new FieldDrive<colorX>();
		_iconFade = new FieldDrive<colorX>();
		_lerp = new Sync<float>();
		_state = new Sync<State>();
		_flickModeActive = new Sync<bool>();
		_flickEnabled = new Sync<bool>();
		_hidden = new Sync<bool>();
		_selectedItem = new SyncRef<ContextMenuItem>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => Owner, 
			4 => Pointer, 
			5 => Separation, 
			6 => LabelSize, 
			7 => RadiusRatio, 
			8 => _currentSummoner, 
			9 => _canvas, 
			10 => _arcLayout, 
			11 => _canvasActive, 
			12 => _colliderEnabled, 
			13 => _iconImage, 
			14 => _separation, 
			15 => _offsetMin, 
			16 => _offsetMax, 
			17 => _innerCircle, 
			18 => _innerCircleButton, 
			19 => _innerCircleAnchorMin, 
			20 => _innerCircleAnchorMax, 
			21 => _itemsRoot, 
			22 => _arcMaterial, 
			23 => _fontMaterial, 
			24 => _spriteMaterial, 
			25 => _arcOverlay, 
			26 => _fontOverlay, 
			27 => _spriteOverlay, 
			28 => _arcZTest, 
			29 => _fontZTest, 
			30 => _spriteZTest, 
			31 => _zwriteArc, 
			32 => _zwriteText, 
			33 => _arcRenderQueue, 
			34 => _fontRenderQueue, 
			35 => _spriteRenderQueue, 
			36 => _canvasOffset, 
			37 => _fillFade, 
			38 => _outlineFade, 
			39 => _textFade, 
			40 => _iconFade, 
			41 => _lerp, 
			42 => _state, 
			43 => _flickModeActive, 
			44 => _flickEnabled, 
			45 => _hidden, 
			46 => _selectedItem, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static ContextMenu __New()
	{
		return new ContextMenu();
	}
}
