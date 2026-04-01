using System;
using System.Linq;
using Elements.Core;
using FrooxEngine.UIX;
using FrooxEngine.Undo;

namespace FrooxEngine;

public class SceneInspector : InspectorPanel, INoDestroyUndo, IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable, IDeveloperInterface, IObjectRoot
{
	public readonly RelayRef<Slot> Root;

	public readonly SyncRef<Slot> ComponentView;

	protected readonly SyncRef<Sync<string>> _rootText;

	protected readonly SyncRef<Sync<string>> _componentText;

	protected readonly SyncRef<Slot> _hierarchyContentRoot;

	protected readonly SyncRef<Slot> _componentsContentRoot;

	protected readonly SyncRef<Slot> _currentComponent;

	protected readonly SyncRef<Slot> _currentRoot;

	protected override void OnAwake()
	{
		base.OnAwake();
		ComponentView.OnTargetChange += OnComponentViewChange;
	}

	private void OnComponentViewChange(SyncRef<Slot> reference)
	{
		if (!base.World.CanMakeSynchronousChanges || reference.Target == null)
		{
			return;
		}
		foreach (DevTool item in base.LocalUser.GetActiveTools().OfType<DevTool>())
		{
			item.SetActiveSlotGizmo(reference.Target);
		}
	}

	protected override void OnAttach()
	{
		base.OnAttach();
		Setup(RadiantUI_Constants.Dark.CYAN, RadiantUI_Constants.BG_COLOR, "Inspector.Title".AsLocaleKey(), out RectTransform hierarchyHeader, out RectTransform hierarchyContent, out RectTransform detailHeader, out RectTransform detailRoot, out RectTransform detailContent, out RectTransform detailFooter);
		UIBuilder uIBuilder = new UIBuilder(hierarchyHeader);
		RadiantUI_Constants.SetupEditorStyle(uIBuilder);
		_rootText.Target = uIBuilder.Text((LocaleString)"Root:").Content;
		uIBuilder.Style.FlexibleWidth = -1f;
		uIBuilder.Style.MinWidth = 64f;
		uIBuilder.Button(OfficialAssets.Graphics.Icons.Inspector.ObjectRoot, OnObjectRootPressed);
		uIBuilder.Button(OfficialAssets.Graphics.Icons.Inspector.RootUp, OnRootUpPressed);
		_hierarchyContentRoot.Target = hierarchyContent.Slot;
		uIBuilder = new UIBuilder(detailFooter);
		RadiantUI_Constants.SetupEditorStyle(uIBuilder);
		uIBuilder.Button("Inspector.Slot.AttachComponent".AsLocaleKey(), OnAttachComponentPressed);
		uIBuilder = new UIBuilder(detailHeader);
		RadiantUI_Constants.SetupEditorStyle(uIBuilder);
		uIBuilder.Style.FlexibleWidth = 100f;
		_componentText.Target = uIBuilder.Text((LocaleString)"Slot:").Content;
		uIBuilder.Style.FlexibleWidth = -1f;
		uIBuilder.Style.MinWidth = 64f;
		uIBuilder.Button(OfficialAssets.Graphics.Icons.Inspector.Destroy, OnDestroyPressed);
		uIBuilder.Button(OfficialAssets.Graphics.Icons.Inspector.DestroyPreservingAssets, OnDestroyPreservingAssetsPressed);
		uIBuilder.Button(OfficialAssets.Graphics.Icons.Inspector.InsertParent, OnInsertParentPressed);
		uIBuilder.Button(OfficialAssets.Graphics.Icons.Inspector.AddChild, OnAddChildPressed);
		uIBuilder.Button(OfficialAssets.Graphics.Icons.Inspector.Duplicate, OnDuplicatePressed);
		uIBuilder.Button(OfficialAssets.Graphics.Icons.Inspector.SetRoot, OnSetRootPressed);
		_componentsContentRoot.Target = detailContent.Slot;
		detailRoot.Slot.AttachComponent<SlotComponentReceiver>().Target.DriveFrom(ComponentView);
		detailRoot.Slot.AttachComponent<Button>();
	}

	protected override void OnStart()
	{
		base.OnStart();
		base.Slot.GetComponentInChildrenOrParents<Canvas>()?.MarkDeveloper();
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnRootUpPressed(IButton button, ButtonEventData eventData)
	{
		if (Root.Target != null && !Root.Target.IsRootSlot)
		{
			Root.Target = Root.Target.Parent;
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnObjectRootPressed(IButton button, ButtonEventData eventData)
	{
		if (Root.Target != null && !Root.Target.IsRootSlot)
		{
			Root.Target = GetRoot(Root.Target.Parent);
		}
	}

	private Slot GetRoot(Slot slot)
	{
		IObjectRoot componentInParents = slot.GetComponentInParents<IObjectRoot>();
		Slot objectRoot = slot.GetObjectRoot();
		if (componentInParents == null)
		{
			return objectRoot;
		}
		if (objectRoot == slot)
		{
			return componentInParents.Slot;
		}
		if (componentInParents.Slot.HierachyDepth > objectRoot.HierachyDepth)
		{
			return componentInParents.Slot;
		}
		return objectRoot;
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnSetRootPressed(IButton button, ButtonEventData eventData)
	{
		if (ComponentView.Target != null)
		{
			Root.Target = ComponentView.Target;
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnAddChildPressed(IButton button, ButtonEventData eventData)
	{
		if (ComponentView.Target != null)
		{
			ComponentView.Target.AddSlot(ComponentView.Target.Name + " - Child").CreateSpawnUndoPoint();
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnInsertParentPressed(IButton button, ButtonEventData eventData)
	{
		if (ComponentView.Target != null)
		{
			Slot target = ComponentView.Target;
			base.World.BeginUndoBatch(this.GetLocalized("Undo.InsertParent", null, "name", target.Name));
			Slot slot = target.Parent.AddSlot(target.Name + " - Parent");
			slot.CopyTransform(target);
			slot.CreateSpawnUndoPoint();
			target.CreateTransformUndoState(parent: true);
			target.SetParent(slot);
			target.SetIdentityTransform();
			base.World.EndUndoBatch();
			Root.Target = slot;
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnAttachComponentPressed(IButton button, ButtonEventData eventData)
	{
		if (ComponentView.Target != null)
		{
			Slot slot = base.LocalUserSpace.AddSlot("Component Selector");
			DestroyProxy destroyProxy = this.DestroyWhenDestroyed(slot);
			destroyProxy.Persistent = false;
			slot.DestroyWhenDestroyed(destroyProxy);
			ComponentSelector componentSelector = slot.AttachComponent<ComponentSelector>();
			componentSelector.SetupDefault();
			slot.GlobalPosition = eventData.globalPoint + base.Slot.Forward * -0.05f * base.LocalUserRoot.GlobalScale;
			slot.GlobalRotation = base.Slot.GlobalRotation;
			slot.LocalScale *= base.LocalUserRoot.GlobalScale;
			componentSelector.ComponentSelected.Target = OnComponentSelected;
		}
	}

	[SyncMethod(typeof(ComponentSelectionHandler), new string[] { })]
	private void OnComponentSelected(ComponentSelector selector, Type componentType)
	{
		Slot target = ComponentView.Target;
		if (target != null)
		{
			HighlightHelper.FlashHighlight(selector.Slot, null, new colorX(0f, 0.5f, 0f));
			HighlightHelper.FlashHighlight(base.Slot, null, new colorX(0f, 0.5f, 0f));
			target.AttachComponent(componentType).CreateSpawnUndoPoint();
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnDestroyPressed(IButton button, ButtonEventData eventData)
	{
		RunDestroy(delegate(Slot s)
		{
			s.UndoableDestroy(sendDestroyingEvent: false);
		});
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnDestroyPreservingAssetsPressed(IButton button, ButtonEventData eventData)
	{
		RunDestroy(delegate(Slot s)
		{
			s.UndoableDestroyPreservingAssets();
		});
	}

	private void RunDestroy(Action<Slot> destroyAction)
	{
		Slot target = ComponentView.Target;
		if (target != null && !target.IsRootSlot)
		{
			int childIndex = target.ChildIndex;
			Slot parent = target.Parent;
			bool flag = target == Root.Target;
			destroyAction(target);
			if (parent.IsRemoved || flag)
			{
				ComponentView.Target = null;
			}
			else if (parent.ChildrenCount > 0)
			{
				ComponentView.Target = parent[MathX.Min(childIndex, parent.ChildrenCount - 1)];
			}
			else if (!parent.IsRootSlot)
			{
				ComponentView.Target = parent;
			}
			else
			{
				ComponentView.Target = null;
			}
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnDuplicatePressed(IButton button, ButtonEventData eventData)
	{
		if (ComponentView.Target != null)
		{
			ComponentView.Target.Duplicate(ComponentView.Target.Parent).CreateSpawnUndoPoint();
		}
	}

	protected override void OnChanges()
	{
		if (!base.World.IsAuthority)
		{
			return;
		}
		if (Root.IsTargetRemoved)
		{
			base.Slot.Destroy();
		}
		if (_currentRoot.Target != Root.Target)
		{
			_hierarchyContentRoot.Target.DestroyChildren();
			_currentRoot.Target = Root.Target;
			_rootText.Target.Value = "Root: " + (_currentRoot.Target?.Name ?? "<i>null</i>");
			if (_currentRoot.Target != null)
			{
				_hierarchyContentRoot.Target.AddSlot("HierarchyRoot").AttachComponent<SlotInspector>().Setup(_currentRoot.Target, _currentComponent);
			}
		}
		if (_currentComponent.Target != ComponentView.Target)
		{
			_currentComponent.Target?.RemoveGizmo();
			if (ComponentView.Target != null && !ComponentView.Target.IsRootSlot)
			{
				ComponentView.Target.GetGizmo();
			}
			_componentsContentRoot.Target.DestroyChildren();
			_currentComponent.Target = ComponentView.Target;
			_componentText.Target.Value = "Slot: " + (_currentComponent.Target?.Name ?? "<i>null</i>");
			if (_currentComponent.Target != null)
			{
				_componentsContentRoot.Target.AddSlot("ComponentRoot").AttachComponent<WorkerInspector>().SetupContainer(_currentComponent.Target);
			}
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		_currentComponent.Target?.RemoveGizmo();
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		Root = new RelayRef<Slot>();
		ComponentView = new SyncRef<Slot>();
		_rootText = new SyncRef<Sync<string>>();
		_componentText = new SyncRef<Sync<string>>();
		_hierarchyContentRoot = new SyncRef<Slot>();
		_componentsContentRoot = new SyncRef<Slot>();
		_currentComponent = new SyncRef<Slot>();
		_currentRoot = new SyncRef<Slot>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => Root, 
			4 => ComponentView, 
			5 => _rootText, 
			6 => _componentText, 
			7 => _hierarchyContentRoot, 
			8 => _componentsContentRoot, 
			9 => _currentComponent, 
			10 => _currentRoot, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public new static SceneInspector __New()
	{
		return new SceneInspector();
	}
}
