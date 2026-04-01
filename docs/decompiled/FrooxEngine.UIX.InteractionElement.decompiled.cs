using System;
using Elements.Core;
using Elements.Data;

namespace FrooxEngine.UIX;

public abstract class InteractionElement : UIController, IUIInteractable, IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable
{
	public enum ColorMode
	{
		Explicit,
		Multiply,
		Additive,
		Direct
	}

	public class ColorDriver : SyncObject
	{
		public readonly FieldDrive<colorX> ColorDrive;

		public readonly Sync<ColorMode> TintColorMode;

		public readonly Sync<colorX> NormalColor;

		public readonly Sync<colorX> HighlightColor;

		public readonly Sync<colorX> PressColor;

		public readonly Sync<colorX> DisabledColor;

		protected override void OnAwake()
		{
			base.OnAwake();
			NormalColor.Value = colorX.White;
			HighlightColor.Value = MathX.Lerp(colorX.White, colorX.Yellow, 0.2f);
			PressColor.Value = MathX.Lerp(colorX.White, new colorX(1f, 0.75f, 0f), 0.4f);
			DisabledColor.Value = new colorX(0.65f);
			ColorDrive.SetupValueSetHook(delegate(IField<colorX> f, colorX c)
			{
				SetColors(in c);
			});
		}

		public colorX GetColor(in colorX color, in colorX baseColor)
		{
			return TintColorMode.Value switch
			{
				ColorMode.Additive => baseColor + color, 
				ColorMode.Multiply => NormalColor.Value * color * baseColor, 
				ColorMode.Direct => color, 
				_ => color * baseColor, 
			};
		}

		public void UpdateColor(in colorX baseColor, bool enabled, bool hovering, bool pressed)
		{
			if (!ColorDrive.IsLinkValid)
			{
				return;
			}
			if (!enabled)
			{
				ColorDrive.Target.Value = DisabledColor.Value;
				return;
			}
			if (pressed)
			{
				ColorDrive.Target.Value = GetColor(PressColor.Value, in baseColor);
				return;
			}
			if (hovering)
			{
				ColorDrive.Target.Value = GetColor(HighlightColor.Value, in baseColor);
				return;
			}
			switch (TintColorMode.Value)
			{
			case ColorMode.Direct:
				ColorDrive.Target.Value = NormalColor.Value;
				break;
			case ColorMode.Multiply:
				ColorDrive.Target.Value = NormalColor.Value * baseColor;
				break;
			default:
				ColorDrive.Target.Value = GetColor((colorX)NormalColor, in baseColor);
				break;
			}
		}

		public void SetColors(in colorX c)
		{
			NormalColor.Value = c;
			ColorHSV colorHSV = new ColorHSV(in c);
			bool flag = colorHSV.s >= 0.1f;
			if (colorHSV.v < 0.5f)
			{
				HighlightColor.Value = new ColorHSV(colorHSV.h, colorHSV.s, colorHSV.v + 0.25f).ToRGB(c.profile);
				PressColor.Value = new ColorHSV(colorHSV.h, flag ? MathX.Clamp01(colorHSV.s + 0.2f) : colorHSV.s, colorHSV.v + 0.5f).ToRGB(c.profile);
				DisabledColor.Value = new colorX(0.45f);
			}
			else
			{
				HighlightColor.Value = new ColorHSV(colorHSV.h, colorHSV.s, colorHSV.v - 0.25f).ToRGB(c.profile);
				PressColor.Value = new ColorHSV(colorHSV.h, flag ? MathX.Clamp01(colorHSV.s + 0.2f) : colorHSV.s, colorHSV.v - 0.5f).ToRGB(c.profile);
				DisabledColor.Value = new colorX(0.65f);
			}
		}

		protected override void InitializeSyncMembers()
		{
			base.InitializeSyncMembers();
			ColorDrive = new FieldDrive<colorX>();
			TintColorMode = new Sync<ColorMode>();
			NormalColor = new Sync<colorX>();
			HighlightColor = new Sync<colorX>();
			PressColor = new Sync<colorX>();
			DisabledColor = new Sync<colorX>();
		}

		public override ISyncMember GetSyncMember(int index)
		{
			return index switch
			{
				0 => ColorDrive, 
				1 => TintColorMode, 
				2 => NormalColor, 
				3 => HighlightColor, 
				4 => PressColor, 
				5 => DisabledColor, 
				_ => throw new ArgumentOutOfRangeException(), 
			};
		}

		public static ColorDriver __New()
		{
			return new ColorDriver();
		}
	}

	public const int PASS_THRESHOLD = 16;

	public readonly Sync<colorX> BaseColor;

	public readonly SyncList<ColorDriver> ColorDrivers;

	[OldName("NormalColor")]
	[NonPersistent]
	protected readonly Sync<colorX> __legacy_NormalColor;

	[OldName("HighlightColor")]
	[NonPersistent]
	protected readonly Sync<colorX> __legacy_HighlightColor;

	[OldName("PressColor")]
	[NonPersistent]
	protected readonly Sync<colorX> __legacy_PressColor;

	[OldName("DisabledColor")]
	[NonPersistent]
	protected readonly Sync<colorX> __legacy_DisabledColor;

	[OldName("TintColorMode")]
	[NonPersistent]
	protected readonly Sync<ColorMode> __legacy_TintColorMode;

	[OldName("ColorDrive")]
	[NonPersistent]
	protected readonly FieldDrive<colorX> __legacy_ColorDrive;

	public readonly Sync<bool> IsPressed;

	public readonly Sync<bool> IsHovering;

	private bool _isLockedIn;

	public override bool InteractionTarget => true;

	public virtual bool RequireLockIn => false;

	public virtual bool TouchExitLock => true;

	public virtual bool TouchEnterLock => true;

	public Rect CurrentGlobalRect { get; private set; }

	protected abstract bool PassOnVerticalMovement { get; }

	protected abstract bool PassOnHorizontalMovement { get; }

	public void SetColors(in colorX color)
	{
		foreach (ColorDriver colorDriver in ColorDrivers)
		{
			if (colorDriver.TintColorMode.Value != ColorMode.Direct)
			{
				colorDriver.SetColors(in color);
			}
		}
	}

	public void SetTintColorMode(ColorMode mode)
	{
		foreach (ColorDriver colorDriver in ColorDrivers)
		{
			colorDriver.TintColorMode.Value = mode;
		}
	}

	public void ConvertTintToAdditive()
	{
		colorX b = ColorDrivers[0].NormalColor.Value * BaseColor.Value;
		BaseColor.Value = b;
		foreach (ColorDriver colorDriver in ColorDrivers)
		{
			if (colorDriver.TintColorMode.Value != ColorMode.Direct)
			{
				colorDriver.TintColorMode.Value = ColorMode.Additive;
				Sync<colorX> normalColor = colorDriver.NormalColor;
				normalColor.Value -= b;
				Sync<colorX> highlightColor = colorDriver.HighlightColor;
				highlightColor.Value -= b;
				Sync<colorX> pressColor = colorDriver.PressColor;
				pressColor.Value -= b;
			}
		}
	}

	public void SetupTransparentOnDisabled(IField<colorX> field, float alpha = 0.25f)
	{
		ColorDriver colorDriver = ColorDrivers.Add();
		colorDriver.ColorDrive.Target = field;
		colorDriver.TintColorMode.Value = ColorMode.Direct;
		colorDriver.NormalColor.Value = field.Value;
		colorDriver.PressColor.Value = field.Value;
		colorDriver.HighlightColor.Value = field.Value;
		colorDriver.DisabledColor.Value = field.Value.SetA(alpha);
	}

	protected override void OnAwake()
	{
		base.OnAwake();
		BaseColor.Value = colorX.White;
	}

	protected override void OnChanges()
	{
		base.OnChanges();
		foreach (ColorDriver colorDriver in ColorDrivers)
		{
			colorDriver.UpdateColor(BaseColor.Value, base.Enabled, IsHovering, IsPressed);
		}
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();
		foreach (ColorDriver colorDriver in ColorDrivers)
		{
			colorDriver.UpdateColor(BaseColor.Value, base.Enabled, IsHovering, IsPressed);
		}
	}

	public bool ProcessEvent(Canvas.InteractionData eventData)
	{
		if (!base.Enabled)
		{
			if (IsHovering.Value)
			{
				OnHoverEnd(eventData);
			}
			if (IsPressed.Value)
			{
				OnPressEnd(eventData);
			}
			IsHovering.Value = false;
			IsPressed.Value = false;
			return false;
		}
		bool value = IsHovering.Value;
		bool value2 = IsPressed.Value;
		if (eventData.hover == EventState.Begin || eventData.touch == EventState.Begin)
		{
			_isLockedIn = false;
		}
		if (eventData.hover == EventState.Begin || eventData.hover == EventState.Stay)
		{
			IsHovering.Value = true;
		}
		else
		{
			IsHovering.Value = false;
			if (eventData.hover == EventState.End)
			{
				OnHoverEnd(eventData);
			}
		}
		if (eventData.touch == EventState.Begin || eventData.touch == EventState.Stay)
		{
			IsPressed.Value = true;
		}
		else if (IsPressed.Value)
		{
			IsPressed.Value = false;
		}
		if (!_isLockedIn && IsPressed.Value)
		{
			float2 @float = MathX.Abs(eventData.position - eventData.initialTouchPosition);
			bool flag = false;
			if (@float.y > 16f)
			{
				if (PassOnVerticalMovement)
				{
					flag = true;
				}
				else
				{
					_isLockedIn = true;
				}
			}
			if (@float.x > 16f)
			{
				if (PassOnHorizontalMovement)
				{
					flag = true;
				}
				else
				{
					_isLockedIn = true;
				}
			}
			if (flag)
			{
				if (IsHovering.Value)
				{
					OnHoverEnd(eventData);
				}
				if (IsPressed.Value && !RequireLockIn)
				{
					OnPressEnd(eventData);
				}
				IsHovering.Value = false;
				IsPressed.Value = false;
				return false;
			}
		}
		if (IsHovering.Value && !value)
		{
			OnHoverBegin(eventData);
		}
		if (IsHovering.Value && value)
		{
			OnHoverStay(eventData);
		}
		if (!RequireLockIn || _isLockedIn || (value2 && !IsPressed.Value))
		{
			bool flag2 = IsPressed.Value;
			bool flag3 = value2;
			if (RequireLockIn && !_isLockedIn)
			{
				flag2 = true;
				flag3 = false;
			}
			if (flag2 && !flag3)
			{
				OnPressBegin(eventData);
			}
			if (flag2 && flag3)
			{
				OnPressStay(eventData);
			}
		}
		if (!IsPressed.Value && value2)
		{
			OnPressEnd(eventData);
		}
		if (!IsHovering.Value && value)
		{
			OnHoverEnd(eventData);
		}
		return ProcessInteractionEvent(eventData);
	}

	protected virtual void OnHoverBegin(Canvas.InteractionData interactionData)
	{
	}

	protected virtual void OnHoverStay(Canvas.InteractionData interactionData)
	{
	}

	protected virtual void OnHoverEnd(Canvas.InteractionData interactionData)
	{
	}

	protected virtual void OnPressBegin(Canvas.InteractionData interactionData)
	{
	}

	protected virtual void OnPressStay(Canvas.InteractionData interactionData)
	{
	}

	protected virtual void OnPressEnd(Canvas.InteractionData interactionData)
	{
	}

	protected abstract bool ProcessInteractionEvent(Canvas.InteractionData eventData);

	protected override void FlagChanges(RectTransform rect)
	{
	}

	public override void PrepareCompute()
	{
	}

	public override void OnComputingBounds(in float2 offset)
	{
		CurrentGlobalRect = base.RectTransform.LocalComputeRect.Translate(in offset);
	}

	protected override void OnLoading(DataTreeNode node, LoadControl control)
	{
		base.OnLoading(node, control);
		if (__legacy_ColorDrive.Target != null)
		{
			control.OnLoaded(this, delegate
			{
				ColorDriver colorDriver = ColorDrivers.Add();
				colorDriver.ColorDrive.ForceLink(__legacy_ColorDrive.Target);
				colorDriver.NormalColor.Value = __legacy_NormalColor;
				colorDriver.HighlightColor.Value = __legacy_HighlightColor;
				colorDriver.PressColor.Value = __legacy_PressColor;
				colorDriver.DisabledColor.Value = __legacy_DisabledColor;
				colorDriver.TintColorMode.Value = __legacy_TintColorMode;
			});
		}
		if (IsHovering.Value)
		{
			control.OnLoaded(this, delegate
			{
				IsHovering.Value = false;
			});
		}
		if (IsPressed.Value)
		{
			control.OnLoaded(this, delegate
			{
				IsPressed.Value = false;
			});
		}
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		BaseColor = new Sync<colorX>();
		ColorDrivers = new SyncList<ColorDriver>();
		__legacy_NormalColor = new Sync<colorX>();
		__legacy_NormalColor.MarkNonPersistent();
		__legacy_HighlightColor = new Sync<colorX>();
		__legacy_HighlightColor.MarkNonPersistent();
		__legacy_PressColor = new Sync<colorX>();
		__legacy_PressColor.MarkNonPersistent();
		__legacy_DisabledColor = new Sync<colorX>();
		__legacy_DisabledColor.MarkNonPersistent();
		__legacy_TintColorMode = new Sync<ColorMode>();
		__legacy_TintColorMode.MarkNonPersistent();
		__legacy_ColorDrive = new FieldDrive<colorX>();
		__legacy_ColorDrive.MarkNonPersistent();
		IsPressed = new Sync<bool>();
		IsHovering = new Sync<bool>();
	}
}
