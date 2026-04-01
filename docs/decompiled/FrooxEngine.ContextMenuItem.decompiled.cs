using System;
using Elements.Core;
using FrooxEngine.UIX;

namespace FrooxEngine;

[Category(new string[] { "Radiant UI/Context Menu" })]
public class ContextMenuItem : Component, IButtonHoverReceiver, IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable, IButtonPressReceiver
{
	public const float ANIMATION_SPEED = 6f;

	public const float SELECTED_ARC = 1.25f;

	public const float FILL_ALPHA = 0.75f;

	public readonly Sync<bool> Highlight;

	public readonly SyncRef<Image> Icon;

	public readonly RelayRef<IAssetProvider<Sprite>> Sprite;

	public readonly SyncRef<IField<string>> Label;

	public readonly Sync<colorX> Color;

	protected readonly SyncRef<ContextMenu> _menu;

	protected readonly Sync<bool> _highlighted;

	protected readonly FieldDrive<float> _arc;

	protected readonly FieldDrive<float> _outerRadius;

	protected readonly SyncRef<Button> _button;

	private float lerp;

	public Button Button => _button.Target;

	public string LabelText
	{
		get
		{
			return Label.Target?.Value;
		}
		set
		{
			if (Label.Target != null)
			{
				Label.Target.Value = value;
			}
		}
	}

	public Uri SpriteURL
	{
		get
		{
			return SpriteURLField?.Value;
		}
		set
		{
			IField<Uri> spriteURLField = SpriteURLField;
			if (spriteURLField != null)
			{
				spriteURLField.Value = value;
			}
		}
	}

	public IField<Uri> SpriteURLField => ((Sprite.Target as SpriteProvider)?.Texture.Target as StaticTexture2D)?.URL;

	public bool HasSprite
	{
		get
		{
			if (Sprite.Target is SpriteProvider spriteProvider)
			{
				return (spriteProvider.Texture.Target as IStaticAssetProvider)?.URL != null;
			}
			return Sprite.Target != null;
		}
	}

	public void SetHighlighted()
	{
		Highlight.Value = true;
		_highlighted.Value = true;
		_menu.Target?.ItemSelected(this);
	}

	public void ClearHighlighted()
	{
		Highlight.Value = false;
		_menu.Target?.ItemDeselected(this);
		_highlighted.Value = false;
	}

	protected override void OnCommonUpdate()
	{
		if (_arc.IsLinkValid && _outerRadius.IsLinkValid)
		{
			lerp = MathX.Progress01(lerp, base.Time.Delta * 6f, _highlighted);
			float num = MathX.SmootherStep(lerp);
			_arc.Target.Value = 1f + num * 0.25f;
			_outerRadius.Target.Value = 1f + num * 0.05f;
		}
	}

	internal void Initialize(ContextMenu menu, OutlinedArc arc, Button button)
	{
		_menu.Target = menu;
		_arc.Target = arc.Arc;
		_outerRadius.Target = arc.OuterRadiusRatio;
		_button.Target = button;
	}

	protected override void OnChanges()
	{
		UpdateColor();
		if (Icon.Target != null)
		{
			Icon.Target.Enabled = HasSprite;
		}
	}

	public static void UpdateColor(Button button, in colorX color, bool highlight)
	{
		InteractionElement.ColorDriver colorDriver = button.ColorDrivers[0];
		InteractionElement.ColorDriver colorDriver2 = button.ColorDrivers[1];
		ColorHSV colorHSV = new ColorHSV(in color);
		colorDriver.NormalColor.Value = RadiantUI_Constants.BG_COLOR;
		colorDriver.HighlightColor.Value = RadiantUI_Constants.GetTintedButton(color);
		colorDriver.PressColor.Value = RadiantUI_Constants.GetTintedButton(color).MulRGB(3f);
		colorDriver.DisabledColor.Value = RadiantUI_Constants.DISABLED_COLOR.SetA(0.5f);
		colorDriver2.NormalColor.Value = new ColorHSV(colorHSV.h, MathX.Min(colorHSV.s * 1.5f, 0.9f), 1.5f).ToRGB(color.profile);
		colorDriver2.HighlightColor.Value = new ColorHSV(colorHSV.h, MathX.Min(colorHSV.s, 0.6f), 3f).ToRGB(color.profile);
		colorDriver2.PressColor.Value = new ColorHSV(colorHSV.h, MathX.Min(colorHSV.s, 0.5f), 6f).ToRGB(color.profile);
		colorDriver2.DisabledColor.Value = new colorX(0.5f, 0.75f);
	}

	public void UpdateColor()
	{
		if (_button.Target != null && (Color.GetWasChangedAndClear() || Highlight.GetWasChangedAndClear()))
		{
			UpdateColor(_button.Target, (colorX)Color, Highlight);
		}
	}

	public void HoverEnter(IButton button, ButtonEventData eventData)
	{
		if (base.LocalUser == _menu.Target?.Owner.Target)
		{
			_highlighted.Value = true;
			_menu.Target?.ItemSelected(this);
		}
	}

	public void HoverStay(IButton button, ButtonEventData eventData)
	{
	}

	public void HoverLeave(IButton button, ButtonEventData eventData)
	{
		if (base.LocalUser == _menu.Target?.Owner.Target)
		{
			_menu.Target?.ItemDeselected(this);
			_highlighted.Value = false;
		}
	}

	public void Pressed(IButton button, ButtonEventData eventData)
	{
		if (base.LocalUser == _menu.Target?.Owner.Target)
		{
			_menu.Target?.ItemPressed(this);
		}
	}

	public void Pressing(IButton button, ButtonEventData eventData)
	{
	}

	public void Released(IButton button, ButtonEventData eventData)
	{
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		Highlight = new Sync<bool>();
		Icon = new SyncRef<Image>();
		Sprite = new RelayRef<IAssetProvider<Sprite>>();
		Label = new SyncRef<IField<string>>();
		Color = new Sync<colorX>();
		_menu = new SyncRef<ContextMenu>();
		_highlighted = new Sync<bool>();
		_arc = new FieldDrive<float>();
		_outerRadius = new FieldDrive<float>();
		_button = new SyncRef<Button>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => Highlight, 
			4 => Icon, 
			5 => Sprite, 
			6 => Label, 
			7 => Color, 
			8 => _menu, 
			9 => _highlighted, 
			10 => _arc, 
			11 => _outerRadius, 
			12 => _button, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static ContextMenuItem __New()
	{
		return new ContextMenuItem();
	}
}
