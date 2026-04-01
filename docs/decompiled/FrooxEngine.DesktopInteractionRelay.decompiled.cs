using System;
using System.Collections.Generic;
using System.Linq;
using Elements.Core;
using FrooxEngine.UIX;
using Renderite.Shared;
using SkyFrost.Base;

namespace FrooxEngine;

[Category(new string[] { "Input/Desktop" })]
public class DesktopInteractionRelay : UIController, IUIInteractable, IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable, IFocusable, IUISecondaryActionReceiver
{
	public readonly Sync<int> DisplayIndex;

	public readonly Sync<bool> UseLegacyTextInput;

	private Dictionary<TouchSource, Pointer> _activeTouches = new Dictionary<TouchSource, Pointer>();

	private List<Key> _injectKeys = new List<Key>();

	private string _typeAppend;

	public bool TouchExitLock => true;

	public bool TouchEnterLock => true;

	public Rect CurrentGlobalRect { get; private set; }

	protected override void OnAwake()
	{
		AppConfig config = FrooxEngine.Engine.Config;
		if (config == null || !config.DisableDesktop)
		{
			base.OnAwake();
			base.InputInterface.OnSimulatedPress += Input_OnSimulatedPress;
			base.InputInterface.OnTypeAppend += Input_OnTypeAppend;
			DisplayIndex.Changed += DisplayIndex_Changed;
		}
	}

	private void DisplayIndex_Changed(IChangeable obj)
	{
		ClearTouches();
	}

	private void Input_OnTypeAppend(string obj)
	{
		AppConfig config = FrooxEngine.Engine.Config;
		if (config == null || !config.DisableDesktop)
		{
			_typeAppend = _typeAppend ?? "";
			_typeAppend += obj;
			MarkChangeDirty();
		}
	}

	private void Input_OnSimulatedPress(Key key)
	{
		AppConfig config = FrooxEngine.Engine.Config;
		if (config == null || !config.DisableDesktop)
		{
			_injectKeys.Add(key);
			MarkChangeDirty();
		}
	}

	public override void PrepareCompute()
	{
	}

	protected override void FlagChanges(RectTransform rect)
	{
	}

	protected override void OnChanges()
	{
		if (base.World != Userspace.UserspaceWorld)
		{
			return;
		}
		AppConfig config = FrooxEngine.Engine.Config;
		if (config != null && config.DisableDesktop)
		{
			return;
		}
		base.OnChanges();
		if (this.HasLocalFocus())
		{
			bool flag = _injectKeys.Any((Key k) => k.IsModifier() && !k.IsShift());
			if (_typeAppend != null && !flag)
			{
				if (UseLegacyTextInput.Value)
				{
					for (int num = 0; num < _typeAppend.Length; num++)
					{
						bool shift;
						Key key = _typeAppend[num].ToKey(out shift);
						if (key != Key.None)
						{
							_injectKeys.Clear();
							if (shift)
							{
								_injectKeys.Add(Key.LeftShift);
							}
							_injectKeys.Add(key);
							base.InputInterface.InjectKeyPress(_injectKeys);
						}
					}
				}
				else
				{
					base.InputInterface.InjectWrite(_typeAppend);
				}
			}
			else if (_injectKeys.Count > 0)
			{
				base.InputInterface.InjectKeyPress(_injectKeys);
			}
		}
		_typeAppend = null;
		_injectKeys.Clear();
	}

	private bool ShouldProcessEvent()
	{
		if (base.World != Userspace.UserspaceWorld)
		{
			return false;
		}
		AppConfig config = FrooxEngine.Engine.Config;
		if (config != null && config.DisableDesktop)
		{
			return false;
		}
		if (base.InputInterface.ScreenActive)
		{
			return false;
		}
		return true;
	}

	public bool ProcessEvent(Canvas.InteractionData eventData)
	{
		if (!ShouldProcessEvent())
		{
			return false;
		}
		float2? displayPoint = GetDisplayPoint(eventData);
		if (!displayPoint.HasValue)
		{
			ClearTouch(eventData.source);
			return false;
		}
		if (eventData.touch == EventState.Begin)
		{
			this.Focus();
		}
		if (eventData.hover == EventState.End)
		{
			ClearTouch(eventData.source);
		}
		else
		{
			if (!_activeTouches.TryGetValue(eventData.source, out Pointer value))
			{
				value = base.InputInterface.InjectTouch();
				if (value != null)
				{
					_activeTouches.Add(eventData.source, value);
				}
			}
			value?.Update(displayPoint.Value, eventData.touch == EventState.Begin || eventData.touch == EventState.Stay, base.Time.Delta);
		}
		return true;
	}

	private void ClearTouch(TouchSource source)
	{
		AppConfig config = FrooxEngine.Engine.Config;
		if ((config == null || !config.DisableDesktop) && _activeTouches.TryGetValue(source, out Pointer value))
		{
			base.InputInterface.RemoveInjectedTouch(value);
			_activeTouches.Remove(source);
		}
	}

	public override void OnComputingBounds(in float2 offset)
	{
		CurrentGlobalRect = base.RectTransform.LocalComputeRect.Translate(in offset);
	}

	protected override void OnDispose()
	{
		ClearTouches();
		base.OnDispose();
	}

	public void Focus(User user)
	{
	}

	public void Defocus(User user)
	{
		if (user.IsLocalUser)
		{
			base.InputInterface.HideKeyboard(this);
		}
	}

	private void ClearTouches()
	{
		AppConfig config = FrooxEngine.Engine.Config;
		if (config != null && config.DisableDesktop)
		{
			return;
		}
		foreach (KeyValuePair<TouchSource, Pointer> activeTouch in _activeTouches)
		{
			base.InputInterface.RemoveInjectedTouch(activeTouch.Value);
		}
		_activeTouches.Clear();
	}

	private float2? GetDisplayPoint(Canvas.InteractionData eventData)
	{
		AppConfig config = FrooxEngine.Engine.Config;
		if (config != null && config.DisableDesktop)
		{
			return null;
		}
		Display display = base.InputInterface.TryGetDisplay(DisplayIndex.Value);
		if (display == null)
		{
			return null;
		}
		float2 normalizedPoint = CurrentGlobalRect.GetNormalizedPoint(eventData.position);
		return display.Rect.GetPoint(new float2(normalizedPoint.x, 1f - normalizedPoint.y));
	}

	public bool TriggerSecondary(Component source, Canvas.InteractionData eventData)
	{
		if (!ShouldProcessEvent())
		{
			return false;
		}
		float2? displayPoint = GetDisplayPoint(eventData);
		if (!displayPoint.HasValue)
		{
			return false;
		}
		base.InputInterface.InjectRightClick(displayPoint.Value);
		return true;
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		DisplayIndex = new Sync<int>();
		UseLegacyTextInput = new Sync<bool>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => DisplayIndex, 
			4 => UseLegacyTextInput, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static DesktopInteractionRelay __New()
	{
		return new DesktopInteractionRelay();
	}
}
