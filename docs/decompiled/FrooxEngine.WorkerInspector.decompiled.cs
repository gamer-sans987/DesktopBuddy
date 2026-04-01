using System;
using System.Reflection;
using Elements.Core;
using Elements.Data;
using FrooxEngine.UIX;
using FrooxEngine.Undo;

namespace FrooxEngine;

[OldTypeName("FrooxEngine.ComponentInspector", null)]
public class WorkerInspector : Component, IDeveloperInterface, IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable
{
	protected readonly SyncRef<Worker> _targetContainer;

	protected readonly SyncDelegate<Predicate<Worker>> _workerFilter;

	protected readonly SyncRef<Worker> _targetWorker;

	private Worker _currentContainer;

	public Worker TargetWorker => _targetContainer.Target;

	protected override void OnStart()
	{
		base.OnStart();
		base.Slot.GetComponentInChildrenOrParents<Canvas>()?.MarkDeveloper();
	}

	public static WorkerInspector Create(Slot root, Worker worker, Predicate<ISyncMember> memberFilter = null)
	{
		root.Tag = "Developer";
		UIBuilder uIBuilder = RadiantUI_Panel.SetupPanel(root, "WorkerInspector.Title".AsLocaleKey(("name", worker.GetType().GetNiceName())), new float2(660f, 1600f));
		root.LocalScale *= 0.0005f;
		RadiantUI_Constants.SetupEditorStyle(uIBuilder);
		Canvas canvas = uIBuilder.Canvas;
		canvas.Slot.Tag = "Developer";
		canvas.AcceptPhysicalTouch.Value = false;
		uIBuilder.Panel(RadiantUI_Constants.BG_COLOR);
		uIBuilder.Style.ChildAlignment = Alignment.TopLeft;
		uIBuilder.ScrollArea();
		uIBuilder.VerticalLayout();
		uIBuilder.FitContent(SizeFit.Disabled, SizeFit.MinSize);
		WorkerInspector workerInspector = uIBuilder.Root.AttachComponent<WorkerInspector>();
		if (worker is Slot)
		{
			workerInspector.SetupContainer((Slot)worker, memberFilter);
		}
		else
		{
			workerInspector.Setup(worker);
		}
		return workerInspector;
	}

	public void SetupContainer(Worker container, Predicate<ISyncMember> memberFilter = null, Predicate<Worker> workerFilter = null, bool includeContainer = true)
	{
		_targetContainer.Target = container;
		_workerFilter.Target = workerFilter;
		VerticalLayout verticalLayout = base.Slot.AttachComponent<VerticalLayout>();
		verticalLayout.Spacing.Value = 4f;
		verticalLayout.ForceExpandHeight.Value = false;
		verticalLayout.ChildAlignment = Alignment.TopLeft;
		if (includeContainer)
		{
			BuildUIForComponent(container, allowRemove: false);
		}
		Slot slot = container as Slot;
		User user = container as User;
		if (slot != null)
		{
			foreach (Component component in slot.Components)
			{
				if ((workerFilter == null || workerFilter(component)) && !(component is GizmoLink))
				{
					BuildUIForComponent(component);
				}
			}
		}
		if (user == null)
		{
			return;
		}
		foreach (UserComponent component2 in user.Components)
		{
			if (workerFilter == null || workerFilter(component2))
			{
				BuildUIForComponent(component2);
			}
		}
		foreach (Stream stream in user.Streams)
		{
			if (workerFilter == null || workerFilter(stream))
			{
				BuildUIForComponent(stream);
			}
		}
	}

	public void Setup(Worker target, Predicate<ISyncMember> memberFilter = null)
	{
		_targetWorker.Target = target;
		BuildUIForComponent(target, allowRemove: true, allowDuplicate: false, allowContainer: true);
	}

	private void BuildUIForComponent(Worker worker, bool allowRemove = true, bool allowDuplicate = true, bool allowContainer = false, Predicate<ISyncMember> memberFilter = null)
	{
		UIBuilder uIBuilder = new UIBuilder(base.Slot);
		RadiantUI_Constants.SetupEditorStyle(uIBuilder);
		uIBuilder.Style.RequireLockInToPress = true;
		uIBuilder.VerticalLayout(6f);
		if (!(worker is Slot))
		{
			uIBuilder.Style.MinHeight = 32f;
			uIBuilder.HorizontalLayout(4f);
			uIBuilder.Style.MinHeight = 24f;
			uIBuilder.Style.FlexibleWidth = 1000f;
			Button button = uIBuilder.ButtonRef((LocaleString)("<b>" + worker.GetType().GetNiceName() + "</b>"), new colorX?(RadiantUI_Constants.BUTTON_COLOR), OnWorkerTypePressed, worker);
			button.Label.Color.Value = RadiantUI_Constants.LABEL_COLOR;
			if (allowRemove || allowDuplicate || allowContainer)
			{
				uIBuilder.Style.FlexibleWidth = 0f;
				uIBuilder.Style.MinWidth = 40f;
				if (allowContainer && worker.FindNearestParent<Slot>() != null)
				{
					ButtonRefRelay<Worker> buttonRefRelay = uIBuilder.Button(OfficialAssets.Graphics.Icons.Inspector.RootUp, new colorX?(RadiantUI_Constants.Sub.PURPLE)).Slot.AttachComponent<ButtonRefRelay<Worker>>();
					buttonRefRelay.Argument.Target = worker;
					buttonRefRelay.ButtonPressed.Target = OnOpenContainerPressed;
				}
				if (allowDuplicate)
				{
					ButtonRefRelay<Worker> buttonRefRelay2 = uIBuilder.Button(OfficialAssets.Graphics.Icons.Inspector.Duplicate, new colorX?(RadiantUI_Constants.Sub.GREEN)).Slot.AttachComponent<ButtonRefRelay<Worker>>();
					buttonRefRelay2.Argument.Target = worker;
					buttonRefRelay2.ButtonPressed.Target = OnDuplicateComponentPressed;
				}
				if (allowRemove)
				{
					ButtonRefRelay<Worker> buttonRefRelay3 = uIBuilder.Button(OfficialAssets.Graphics.Icons.Inspector.Destroy, new colorX?(RadiantUI_Constants.Sub.RED)).Slot.AttachComponent<ButtonRefRelay<Worker>>();
					buttonRefRelay3.Argument.Target = worker;
					buttonRefRelay3.ButtonPressed.Target = OnRemoveComponentPressed;
				}
			}
			button.Slot.AttachComponent<ReferenceProxySource>().Reference.Target = worker;
			uIBuilder.NestOut();
		}
		InspectorHeaderAttribute customAttribute = worker.GetType().GetCustomAttribute<InspectorHeaderAttribute>();
		if (customAttribute != null)
		{
			AddHeaderText(uIBuilder, customAttribute);
		}
		if (worker is ICustomInspector customInspector)
		{
			try
			{
				uIBuilder.Style.MinHeight = 24f;
				customInspector.BuildInspectorUI(uIBuilder);
			}
			catch (Exception ex)
			{
				uIBuilder.Text((LocaleString)"EXCEPTION BUILDING UI. See log");
				UniLog.Error(ex.ToString(), stackTrace: false);
			}
		}
		else
		{
			BuildInspectorUI(worker, uIBuilder, memberFilter);
		}
		uIBuilder.Style.MinHeight = 8f;
		uIBuilder.Panel();
		uIBuilder.NestOut();
	}

	private static void AddHeaderText(UIBuilder ui, InspectorHeaderAttribute header)
	{
		ui.PushStyle();
		ui.Style.MinHeight = header.MinHeight;
		ui.Text(in header.LocaleKey, bestFit: true, Alignment.TopLeft);
		ui.PopStyle();
	}

	public static void BuildInspectorUI(Worker worker, UIBuilder ui, Predicate<ISyncMember> memberFilter = null)
	{
		for (int i = 0; i < worker.SyncMemberCount; i++)
		{
			ISyncMember syncMember = worker.GetSyncMember(i);
			if (worker.GetSyncMemberFieldInfo(i).GetCustomAttribute<HideInInspectorAttribute>() == null && (memberFilter == null || memberFilter(syncMember)))
			{
				SyncMemberEditorBuilder.Build(syncMember, worker.GetSyncMemberName(i), worker.GetSyncMemberFieldInfo(i), ui);
			}
		}
		for (int j = 0; j < worker.SyncMethodCount; j++)
		{
			worker.GetSyncMethodData(j, out SyncMethodInfo info, out Delegate method);
			if ((object)method != null)
			{
				SyncMemberEditorBuilder.BuildSyncMethod(method, info.methodType, info.method, ui);
			}
		}
	}

	protected override void OnAttach()
	{
		base.OnAttach();
		base.Slot.Tag = "Developer";
		Settings.GetActiveSetting<EditSettings>();
	}

	protected override void OnCommonUpdate()
	{
		if (base.World.IsAuthority && _targetWorker.IsTargetRemoved)
		{
			base.Slot.GetComponentInParents<LegacyPanel>()?.Slot.Destroy();
		}
	}

	protected override void OnChanges()
	{
		if (base.World.IsAuthority && _currentContainer != _targetContainer.Target)
		{
			UnregisterEvents();
			Slot slot = _targetContainer.Target as Slot;
			User user = _targetContainer.Target as User;
			if (slot != null)
			{
				slot.ComponentAdded += OnComponentAdded;
				slot.ComponentRemoved += OnComponentRemoved;
			}
			if (user != null)
			{
				user.ComponentAdded += UserComponentAdded;
				user.ComponentRemoved += UserComponentRemoved;
				user.StreamAdded += StreamAdded;
				user.StreamRemoved += StreamRemoved;
			}
			_currentContainer = _targetContainer.Target;
		}
	}

	private void StreamRemoved(Stream obj)
	{
		WorkerRemoved(obj);
	}

	private void StreamAdded(Stream obj)
	{
		WorkerAdded(obj);
	}

	private void UserComponentRemoved(UserComponent component)
	{
		WorkerRemoved(component);
	}

	private void UserComponentAdded(UserComponent component)
	{
		WorkerAdded(component);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnWorkerTypePressed(IButton button, ButtonEventData eventData, Worker worker)
	{
	}

	protected override void OnDestroy()
	{
		UnregisterEvents();
	}

	private void UnregisterEvents()
	{
		Slot slot = _currentContainer as Slot;
		User user = _currentContainer as User;
		if (slot != null)
		{
			slot.ComponentAdded -= OnComponentAdded;
			slot.ComponentRemoved -= OnComponentRemoved;
		}
		if (user != null)
		{
			user.ComponentAdded -= UserComponentAdded;
			user.ComponentRemoved -= UserComponentRemoved;
			user.StreamAdded -= StreamAdded;
			user.StreamRemoved -= StreamRemoved;
		}
	}

	private void RemoveComponent(Worker worker)
	{
		Component obj = worker as Component;
		UserComponent userComponent = worker as UserComponent;
		obj?.UndoableDestroy();
		userComponent?.Destroy();
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnRemoveComponentPressed(IButton button, ButtonEventData eventData, Worker worker)
	{
		EditSettings? activeSetting = Settings.GetActiveSetting<EditSettings>();
		if (activeSetting != null && !activeSetting.ConfirmComponentDestroy.Value)
		{
			RemoveComponent(worker);
			return;
		}
		StartGlobalTask(async delegate
		{
			if (await base.LocalUser.ContextMenuConfirm(this, eventData.source.Slot, "General.Confirm".AsLocaleKey(), OfficialAssets.Graphics.Icons.Inspector.Destroy, RadiantUI_Constants.Hero.RED, null))
			{
				base.LocalUser.CloseContextMenu(this);
				RemoveComponent(worker);
			}
		});
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnDuplicateComponentPressed(IButton button, ButtonEventData eventData, Worker worker)
	{
		Component component = worker as Component;
		base.World.BeginUndoBatch("Undo.DuplicateComponent".AsLocaleKey());
		component?.Slot.DuplicateComponent(component).CreateSpawnUndoPoint();
		base.World.EndUndoBatch();
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnOpenContainerPressed(IButton button, ButtonEventData eventData, Worker worker)
	{
		worker.FindNearestParent<Slot>()?.OpenInspectorForTarget(base.Slot);
	}

	private void WorkerAdded(Worker worker)
	{
		RunSynchronously(delegate
		{
			if ((_workerFilter.Target == null || _workerFilter.Target(worker)) && !worker.IsRemoved)
			{
				BuildUIForComponent(worker);
			}
		});
	}

	private void WorkerRemoved(Worker worker)
	{
		RunSynchronously(delegate
		{
			for (int i = 0; i < base.Slot.ChildrenCount; i++)
			{
				if (base.Slot[i].GetComponentInChildren<ReferenceProxySource>().Reference.RawTarget == worker)
				{
					base.Slot[i].Destroy();
					break;
				}
			}
		});
	}

	private void OnComponentRemoved(Component component)
	{
		WorkerRemoved(component);
	}

	private void OnComponentAdded(Component component)
	{
		WorkerAdded(component);
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		_targetContainer = new SyncRef<Worker>();
		_workerFilter = new SyncDelegate<Predicate<Worker>>();
		_targetWorker = new SyncRef<Worker>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => _targetContainer, 
			4 => _workerFilter, 
			5 => _targetWorker, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static WorkerInspector __New()
	{
		return new WorkerInspector();
	}
}
