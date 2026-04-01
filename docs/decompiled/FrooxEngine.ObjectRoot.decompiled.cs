using System;
using System.Collections.Generic;
using System.Linq;
using Elements.Core;
using FrooxEngine.UIX;

namespace FrooxEngine;

[Category(new string[] { "Transform" })]
[SingleInstancePerSlot]
public class ObjectRoot : Component, IObjectRoot, IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable, ICustomInspector
{
	private static List<Type> consolidatedComponents = new List<Type> { typeof(Grabbable) };

	public bool IsPure
	{
		get
		{
			foreach (Component component in base.Slot.Components)
			{
				if (component != this && !consolidatedComponents.Contains(component.GetType()))
				{
					return false;
				}
			}
			return true;
		}
	}

	public ObjectRoot MergeInto(IEnumerable<ObjectRoot> roots, string rootName = "Root")
	{
		ObjectRoot objectRoot = EnsurePure(rootName);
		foreach (ObjectRoot root in roots)
		{
			root.MergeWith(objectRoot);
		}
		return objectRoot;
	}

	public void MergeWith(ObjectRoot target)
	{
		if (target.Slot.IsChildOf(base.Slot))
		{
			target.Destroy();
			return;
		}
		ConsolidateComponents(target.Slot);
		if (IsPure)
		{
			base.Slot.Destroy(target.Slot);
			return;
		}
		base.Slot.SetParent(target.Slot);
		Destroy();
	}

	public ObjectRoot EnsurePure(string name = "Root")
	{
		if (!IsPure)
		{
			Slot slot = base.Slot.Parent.AddSlot(name);
			ObjectRoot result = slot.AttachComponent<ObjectRoot>();
			TransferConsolidatedComponents(slot);
			base.Slot.Parent = slot;
			Destroy();
			return result;
		}
		base.Slot.Name = name;
		return this;
	}

	private void ConsolidateComponents(Slot target)
	{
		foreach (Type type in consolidatedComponents)
		{
			Component component = base.Slot.Components.FirstOrDefault((Component c) => c.GetType() == type);
			Component component2 = target.Components.FirstOrDefault((Component c) => c.GetType() == type);
			if (component2 != null && component == null)
			{
				component2.Destroy();
			}
			else
			{
				component?.Destroy();
			}
		}
	}

	private void TransferConsolidatedComponents(Slot target)
	{
		foreach (Component component in base.Slot.Components)
		{
			if (consolidatedComponents.Contains(component.GetType()))
			{
				target.AttachComponent(component.GetType(), runOnAttachBehavior: false).CopyValues(component);
			}
		}
		base.Slot.RemoveAllComponents((Component c) => consolidatedComponents.Contains(c.GetType()));
	}

	public void BuildInspectorUI(UIBuilder ui)
	{
		WorkerInspector.BuildInspectorUI(this, ui);
		ui.Button((LocaleString)"Remove all children object roots", RemoveChildrenObjectRoots);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void RemoveChildrenObjectRoots(IButton button, ButtonEventData eventData)
	{
		int num = RemoveChildrenObjectRoots();
		string oldLabel = button.LabelText;
		button.LabelText = "Removed: " + num;
		button.RunInSeconds(5f, delegate
		{
			button.LabelText = oldLabel;
		});
	}

	[SyncMethod(typeof(Func<int>), new string[] { })]
	public int RemoveChildrenObjectRoots()
	{
		List<ObjectRoot> componentsInChildren = base.Slot.GetComponentsInChildren((ObjectRoot r) => r != this);
		foreach (ObjectRoot item in componentsInChildren)
		{
			item.Destroy();
		}
		return componentsInChildren.Count;
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static ObjectRoot __New()
	{
		return new ObjectRoot();
	}
}
