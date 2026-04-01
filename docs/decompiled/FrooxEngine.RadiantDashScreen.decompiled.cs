using System;
using System.Linq;
using Elements.Core;
using FrooxEngine.UIX;
using Renderite.Shared;

namespace FrooxEngine;

public class RadiantDashScreen : Component, IUIContainer
{
	public readonly Sync<Uri> Icon;

	public readonly Sync<colorX?> ActiveColor;

	public readonly Sync<string> Label;

	public readonly Sync<bool> ScreenEnabled;

	public readonly Sync<float2> BaseResolution;

	protected readonly SyncRef<Slot> _screenRoot;

	protected readonly SyncRef<Canvas> _screenCanvas;

	protected readonly SyncRef<ModalOverlayManager> _modalOverlayManager;

	protected readonly SyncRef<RadiantDashButton> _button;

	protected readonly AssetRef<Texture2D> _iconTexture;

	public static Uri BackgroundTexture => OfficialAssets.Graphics.Patterns.CircularPatternDark0;

	public static float2 BackgroundTiling => float2.One * 512 + 256;

	public Slot ScreenRoot => _screenRoot.Target;

	public Canvas ScreenCanvas => _screenCanvas.Target;

	public virtual bool AlwaysSwitchable => false;

	public IField<string> ContainerTitle => Label;

	public bool IsShown => ScreenRoot.ActiveSelf;

	public colorX CurrentColor
	{
		get
		{
			if (ActiveColor.Value.HasValue)
			{
				return ActiveColor.Value.Value;
			}
			ColorHSV? colorHSV = _iconTexture.Asset?.BitmapMetadata?.AverageVisibleHSV;
			if (colorHSV.HasValue)
			{
				ColorHSV value = colorHSV.Value;
				if (value.s > 0.5f)
				{
					value.s = 1f;
				}
				value.v = 1f;
				value.a = 1f;
				return value.ToRGB(ColorProfile.sRGB);
			}
			return colorX.White;
		}
	}

	public void SetResolution(float2 resolution)
	{
		BaseResolution.Value = resolution;
		SetAspectRatio(base.Slot.GetComponentInParents<RadiantDash>()?.ScreenAspectRatio ?? (resolution.x / resolution.y));
	}

	protected virtual void OnShow()
	{
	}

	protected virtual void OnHide()
	{
	}

	public virtual void OnButtonGenerated(RadiantDashButton button)
	{
		_button.Target = button;
		button.MarkChangeDirty();
	}

	protected override void OnAwake()
	{
		base.OnAwake();
		ScreenEnabled.Value = true;
		BaseResolution.Value = new float2(1920f, 1080f);
	}

	protected override void OnAttach()
	{
		base.OnAttach();
		Slot slot = base.Slot.AddSlot("Screen");
		Canvas canvas = slot.AddSlot("Canvas").AttachComponent<Canvas>();
		canvas.Size.Value = BaseResolution.Value;
		canvas.Collider.Target.SetTrigger();
		_screenRoot.Target = slot;
		_screenCanvas.Target = canvas;
		SetupIconTexture();
		SetupModalOverlay();
	}

	protected override void OnChanges()
	{
		base.OnChanges();
		if (_screenCanvas.Target == null)
		{
			_screenCanvas.Target = base.Slot.FindChild("Screen")?.FindChild("Canvas")?.GetComponent<Canvas>();
			UniLog.Warning($"_screenCanvas is null. Re-assigning. Found one: {_screenCanvas.Target != null}.\nChildren: {base.Slot.ChildrenHierarchyToString()}");
		}
	}

	private void SetupIconTexture()
	{
		StaticTexture2D staticTexture2D = base.Slot.AttachComponent<StaticTexture2D>();
		staticTexture2D.URL.DriveFrom(Icon);
		_iconTexture.Target = staticTexture2D;
	}

	public void SetAspectRatio(float ratio)
	{
		if (_screenCanvas.Target == null)
		{
			UniLog.Warning("ScreenCanvas is missing, cannot update AspectRatio");
		}
		else
		{
			_screenCanvas.Target.Size.Value = new float2(BaseResolution.Value.y * ratio, BaseResolution.Value.y);
		}
	}

	private void SetupModalOverlay()
	{
		if (!base.IsDestroyed)
		{
			if (ScreenCanvas == null)
			{
				UniLog.Warning($"Cannot setup modal overlay, ScreenCanvas is null. On\n{this}");
				RunInSeconds(1f, SetupModalOverlay);
			}
			else
			{
				ModalOverlayManager modalOverlayManager = base.Slot.AttachComponent<ModalOverlayManager>();
				modalOverlayManager.SpawnRoot.Target = ScreenCanvas.Slot;
				modalOverlayManager.Constructor.Target = RadiantModalOverlay.Construct;
				_modalOverlayManager.Target = modalOverlayManager;
			}
		}
	}

	public void Show()
	{
		ScreenRoot.ActiveSelf = true;
		OnShow();
	}

	public void Hide()
	{
		ScreenRoot.ActiveSelf = false;
		OnHide();
	}

	public void CloseContainer()
	{
		RadiantDash componentInParents = base.Slot.GetComponentInParents<RadiantDash>();
		if (componentInParents == null || componentInParents.CurrentScreen.Target != this)
		{
			base.Slot.Destroy();
			return;
		}
		componentInParents.CurrentScreen.Target = componentInParents.Screens.FirstOrDefault((RadiantDashScreen s) => s != this);
		StartTask(async delegate
		{
			while (ScreenRoot.ActiveSelf)
			{
				await default(NextUpdate);
			}
			base.Slot.Destroy();
		});
	}

	protected void BuildBackground(Slot root)
	{
		Uri uri = (base.Engine.InUniverse ? null : BackgroundTexture);
		if (uri != null)
		{
			TiledRawImage tiledRawImage = root.AttachComponent<TiledRawImage>();
			tiledRawImage.TileSize.Value = BackgroundTiling;
			tiledRawImage.Texture.Target = root.AttachTexture(uri);
			tiledRawImage.Tint.Value = UserspaceRadiantDash.DEFAULT_BACKGROUND;
		}
		else
		{
			root.AttachComponent<Image>().Tint.Value = UserspaceRadiantDash.DEFAULT_BACKGROUND;
		}
	}

	protected void BuildBackground(UIBuilder ui, bool nest = true)
	{
		Uri uri = (base.Engine.InUniverse ? null : BackgroundTexture);
		if (uri != null)
		{
			ui.TiledRawImage(ui.Root.AttachTexture(uri), UserspaceRadiantDash.DEFAULT_BACKGROUND).TileSize.Value = BackgroundTiling;
		}
		else
		{
			ui.Image(UserspaceRadiantDash.DEFAULT_BACKGROUND);
		}
		if (nest)
		{
			ui.Nest();
		}
	}

	protected override void OnLoading(DataTreeNode node, LoadControl control)
	{
		base.OnLoading(node, control);
		if (node is DataTreeDictionary dataTreeDictionary && !dataTreeDictionary.ContainsKey("BaseResolution"))
		{
			control.OnLoaded(this, delegate
			{
				BaseResolution.Value = _screenCanvas.Target.Size.Value;
			}, -1000);
		}
		if (_modalOverlayManager.Target == null)
		{
			control.OnLoaded(this, delegate
			{
				SetupModalOverlay();
			});
		}
		if (_iconTexture.Target == null)
		{
			control.OnLoaded(this, delegate
			{
				ActiveColor.Value = null;
				SetupIconTexture();
			});
		}
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		Icon = new Sync<Uri>();
		ActiveColor = new Sync<colorX?>();
		Label = new Sync<string>();
		ScreenEnabled = new Sync<bool>();
		BaseResolution = new Sync<float2>();
		_screenRoot = new SyncRef<Slot>();
		_screenCanvas = new SyncRef<Canvas>();
		_modalOverlayManager = new SyncRef<ModalOverlayManager>();
		_button = new SyncRef<RadiantDashButton>();
		_iconTexture = new AssetRef<Texture2D>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => Icon, 
			4 => ActiveColor, 
			5 => Label, 
			6 => ScreenEnabled, 
			7 => BaseResolution, 
			8 => _screenRoot, 
			9 => _screenCanvas, 
			10 => _modalOverlayManager, 
			11 => _button, 
			12 => _iconTexture, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static RadiantDashScreen __New()
	{
		return new RadiantDashScreen();
	}
}
