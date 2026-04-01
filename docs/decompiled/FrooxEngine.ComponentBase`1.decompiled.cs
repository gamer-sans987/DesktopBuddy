using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Elements.Core;

namespace FrooxEngine;

public abstract class ComponentBase<C> : Worker, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable, IWorldEventReceiver where C : ComponentBase<C>
{
	private volatile bool _synchronousChangeScheduled;

	private SpinLock destroyLock = new SpinLock(enableThreadOwnerTracking: false);

	[NonPersistent]
	protected readonly Sync<bool> persistent;

	[NameOverride("UpdateOrder")]
	protected readonly Sync<int> updateOrder;

	[NameOverride("Enabled")]
	public readonly Sync<bool> EnabledField;

	private bool _runningOnDestroying;

	internal ContainerWorker<C> Container { get; private set; }

	public bool IsStarted { get; private set; }

	public bool IsDestroyed { get; private set; }

	public override bool IsRemoved
	{
		get
		{
			if (!IsDestroyed)
			{
				return base.IsDisposed;
			}
			return true;
		}
	}

	public bool IsValid { get; private set; }

	public bool IsChangeDirty { get; private set; }

	public int LastChangeUpdateIndex { get; private set; }

	public virtual bool UserspaceOnly => false;

	protected virtual bool CanRunUpdates => true;

	public int UpdateOrder
	{
		get
		{
			return updateOrder;
		}
		set
		{
			updateOrder.Value = value;
		}
	}

	public bool Persistent
	{
		get
		{
			return persistent.Value;
		}
		set
		{
			persistent.Value = value;
		}
	}

	public override bool IsPersistent
	{
		get
		{
			if (persistent.Value)
			{
				return base.Parent.IsPersistent;
			}
			return false;
		}
	}

	public bool Enabled
	{
		get
		{
			return EnabledField.Value;
		}
		set
		{
			EnabledField.Value = value;
		}
	}

	public bool IsInInitPhase { get; private set; }

	public bool IsLinked => ActiveLink != null;

	public bool IsDriven => ActiveLink?.IsDriving ?? false;

	public bool IsHooked => ActiveLink?.IsHooking ?? false;

	public ILinkRef ActiveLink => DirectLink;

	public ILinkRef DirectLink { get; private set; }

	public ILinkRef InheritedLink => null;

	public IEnumerable<ILinkable> LinkableChildren => base.SyncMembers.Cast<ILinkable>();

	public event Action<IChangeable> Changed;

	public event Action<IDestroyable> Destroyed;

	internal virtual void Initialize(ContainerWorker<C> container, bool isNew)
	{
		IsInInitPhase = true;
		Container = container;
		InitializeWorker(container);
		persistent.Value = true;
		EnabledField.Value = true;
		EnabledField.OnValueChange += EnabledField_OnValueChange;
		updateOrder.Value = InitInfo.DefaultUpdateOrder;
		updateOrder.OnValueChange += UpdateOrder_OnValueChange;
		base.World.ReferenceController.BlockAllocations();
		try
		{
			OnAwake();
		}
		catch (Exception ex)
		{
			base.World.FatalError("Exception in OnAwake on " + this.ParentHierarchyToString() + ":\n" + ex);
			return;
		}
		base.World.ReferenceController.UnblockAllocations();
		if (isNew)
		{
			try
			{
				OnInit();
			}
			catch (Exception ex2)
			{
				base.World.FatalError("Exception in OnAwake on " + this.ParentHierarchyToString() + ":\n" + ex2);
				return;
			}
		}
		base.World.UpdateManager.RegisterForStartup(this);
		IsValid = true;
		if (!InitInfo.SingleInstancePerSlot || IsSingleValidInstance())
		{
			return;
		}
		IsValid = false;
		if (base.World.IsAuthority)
		{
			base.World.RunSynchronously(delegate
			{
				if (!IsSingleValidInstance())
				{
					Destroy(sendDestroyingEvent: false);
				}
			});
		}
		else
		{
			Container.ComponentRemoved += ValidateSingleInstance;
		}
	}

	private void EnabledField_OnValueChange(SyncField<bool> syncField)
	{
		base.World?.RunSynchronously(RunEnabledChanged, immediatellyIfPossible: true, this);
	}

	private void UpdateOrder_OnValueChange(SyncField<int> syncField)
	{
		base.World?.UpdateManager.UpdateBucketChanged(this);
	}

	private void RunEnabledChanged()
	{
		if (CheckUserspaceOnly())
		{
			return;
		}
		try
		{
			base.World.UpdateManager.NestCurrentlyUpdating(this);
			if (Enabled)
			{
				OnEnabled();
			}
			else
			{
				OnDisabled();
			}
		}
		catch (Exception exception)
		{
			base.Debug.Error("Exception running " + (Enabled ? "OnEnabled()" : "OnDisabled()") + ":\n" + DebugManager.PreprocessException(exception));
		}
		finally
		{
			base.World.UpdateManager.PopCurrentlyUpdating(this);
		}
	}

	public bool IsSingleValidInstance()
	{
		return Container.GetComponent((C c) => c != this && c.GetType() == GetType() && c.IsValid) == null;
	}

	private void ValidateSingleInstance(C component)
	{
		if (component == this)
		{
			Container.ComponentRemoved -= ValidateSingleInstance;
		}
		else if (IsSingleValidInstance())
		{
			IsValid = true;
			MarkChangeDirty();
			Container.ComponentRemoved -= ValidateSingleInstance;
		}
	}

	protected override void SyncMemberChanged(IChangeable member)
	{
		MarkChangeDirty();
	}

	public void MarkChangeDirty()
	{
		World world = base.World;
		if (world == null || IsChangeDirty || !IsStarted)
		{
			return;
		}
		if (!world.CanCurrentThreadModify)
		{
			if (!_synchronousChangeScheduled)
			{
				_synchronousChangeScheduled = true;
				RunSynchronously(MarkChangeDirty);
			}
			return;
		}
		IsChangeDirty = true;
		if (!IsDestroyed)
		{
			world?.UpdateManager.Changed(this);
		}
		TriggerChangedEvent();
	}

	protected void TriggerChangedEvent()
	{
		OnImmediateChanged();
		this.Changed?.Invoke(this);
	}

	public void Destroy()
	{
		Destroy(sendDestroyingEvent: true);
	}

	public void Destroy(bool sendDestroyingEvent = true)
	{
		if (!IsDestroyed)
		{
			if (sendDestroyingEvent)
			{
				RunOnDestroying();
			}
			Container.RemoveComponent(base.ReferenceID);
		}
	}

	internal void PrepareDestruction()
	{
		if (!IsDestroyed)
		{
			MarkChangeDirty();
			StopAllCoroutines();
			PrepareMembersForDestroy();
			IsDestroyed = true;
			base.World.UpdateManager.RegisterToDestroy(this);
			OnPrepareDestroy();
		}
	}

	public void CopyValues(C source, Action<ISyncMember, ISyncMember> copy)
	{
		CopyValues(source, copy, allowTypeMismatch: false);
	}

	public void CopyValues(C source)
	{
		CopyValues((Worker)source);
	}

	private bool CheckUserspaceOnly()
	{
		if (UserspaceOnly && base.World != Userspace.UserspaceWorld && Userspace.UserspaceWorld != null)
		{
			UniLog.Warning($"{GetType()} is Userspace only component, cannot use in World {base.World}");
			base.World.RunSynchronously(Destroy);
			return true;
		}
		return false;
	}

	internal void RunOnAttach()
	{
		if (!CheckUserspaceOnly())
		{
			base.World.UpdateManager.NestCurrentlyUpdating(this);
			try
			{
				OnAttach();
			}
			catch (Exception exception)
			{
				base.Debug.Error("Exception running OnAttach behavior for Component: " + this.ParentHierarchyToString() + "\n" + DebugManager.PreprocessException(exception));
			}
			base.World.UpdateManager.PopCurrentlyUpdating(this);
		}
	}

	internal void RunOnPaste()
	{
		if (!CheckUserspaceOnly())
		{
			base.World.UpdateManager.NestCurrentlyUpdating(this);
			try
			{
				OnPaste();
			}
			catch (Exception exception)
			{
				base.Debug.Error("Exception running OnPaste behavior for Component: " + this.ParentHierarchyToString() + "\n" + DebugManager.PreprocessException(exception));
			}
			base.World.UpdateManager.PopCurrentlyUpdating(this);
		}
	}

	protected virtual void OnInit()
	{
	}

	protected virtual void OnAwake()
	{
	}

	protected virtual void OnStart()
	{
	}

	protected virtual void OnAttach()
	{
	}

	protected virtual void OnDuplicate()
	{
	}

	protected virtual void OnPaste()
	{
	}

	protected virtual void OnCommonUpdate()
	{
	}

	protected virtual void OnBehaviorUpdate()
	{
	}

	protected virtual void OnImmediateChanged()
	{
	}

	protected virtual void OnChanges()
	{
	}

	protected virtual void OnActivated()
	{
	}

	protected virtual void OnDeactivated()
	{
	}

	protected virtual void OnLinked()
	{
	}

	protected virtual void OnUnlinked()
	{
	}

	protected virtual void OnEnabled()
	{
	}

	protected virtual void OnDisabled()
	{
	}

	protected virtual void OnAudioUpdate()
	{
	}

	protected virtual void OnAudioConfigurationChanged()
	{
	}

	protected virtual void OnPrepareDestroy()
	{
	}

	/// <remarks>
	/// Not guarenteed to be called. Use <see cref="M:FrooxEngine.Worker.OnDispose" /> to run actions that must be guarenteed to run.
	/// </remarks>
	protected virtual void OnDestroy()
	{
	}

	protected virtual void OnDestroying()
	{
	}

	public void EndInitPhase()
	{
		EndInitializationStageForMembers();
		IsInInitPhase = false;
	}

	public void Link(ILinkRef link)
	{
		DirectLink = link;
		RunLinked();
		foreach (ILinkable linkableChild in LinkableChildren)
		{
			linkableChild.InheritLink(link);
		}
	}

	public void ReleaseLink(ILinkRef link)
	{
		if (link != DirectLink)
		{
			return;
		}
		DirectLink = null;
		RunUnlinked();
		foreach (ILinkable linkableChild in LinkableChildren)
		{
			linkableChild.ReleaseInheritedLink(link);
		}
	}

	public void InheritLink(ILinkRef link)
	{
		throw new Exception("Components cannot inherit links!");
	}

	public void ReleaseInheritedLink(ILinkRef link)
	{
		throw new Exception("Components cannot inherit links!");
	}

	public void RunSynchronously(Action action, bool immediatellyIfPossible = false)
	{
		base.World?.RunSynchronously(action, immediatellyIfPossible, this);
	}

	public Task RunSynchronouslyAsync(Action action, bool immediatellyIfPossible = false)
	{
		return RunSynchronouslyAsync(delegate
		{
			action();
			return true;
		}, immediatellyIfPossible);
	}

	public Task<T> RunSynchronouslyAsync<T>(Func<T> function, bool immediatellyIfPossible = false)
	{
		World world = base.World;
		if (world == null)
		{
			return Task.FromResult(default(T));
		}
		TaskCompletionSource<T> completionSource = new TaskCompletionSource<T>();
		world.RunSynchronously(delegate
		{
			try
			{
				completionSource.SetResult(function());
			}
			catch (Exception exception)
			{
				completionSource.SetException(exception);
			}
		}, immediatellyIfPossible, this);
		return completionSource.Task;
	}

	public Coroutine RunInSeconds(float seconds, Action action)
	{
		return base.World.RunInSeconds(seconds, WrapSynchronousAction(action));
	}

	public Coroutine RunInUpdates(int updates, Action action)
	{
		return base.World.RunInUpdates(updates, WrapSynchronousAction(action));
	}

	protected void RunInBackground(Action action, WorkType workType = WorkType.Background)
	{
		base.World?.RunInBackground(action, workType);
	}

	private Action WrapSynchronousAction(Action action)
	{
		return delegate
		{
			if (!base.IsDisposed && !IsDestroyed)
			{
				RunInUpdateScope(action);
			}
		};
	}

	protected void RunInUpdateScope(Action action)
	{
		try
		{
			base.World.UpdateManager.NestCurrentlyUpdating(this);
			action();
		}
		finally
		{
			base.World.UpdateManager.PopCurrentlyUpdating(this);
		}
	}

	protected void RunInUpdateScope<T>(Action<T> action, T arg0)
	{
		try
		{
			base.World.UpdateManager.NestCurrentlyUpdating(this);
			action(arg0);
		}
		finally
		{
			base.World.UpdateManager.PopCurrentlyUpdating(this);
		}
	}

	public virtual void InternalRunStartup()
	{
		if (!CheckUserspaceOnly())
		{
			OnStart();
			IsStarted = true;
			WorkerInitInfo initInfo = InitInfo;
			if (initInfo.HasUpdateMethods)
			{
				base.World.UpdateManager.RegisterForUpdates(this);
			}
			if (initInfo.HasAudioUpdateMethod)
			{
				base.World.UpdateManager.RegisterForAudioUpdates(this);
			}
			if (initInfo.HasAudioConfigurationChangedMethod)
			{
				base.World.UpdateManager.RegisterForAudioConfigurationChanged(this);
			}
			if (initInfo.ReceivesAnyWorldEvent)
			{
				base.World.RegisterEventReceiver(this);
			}
			MarkChangeDirty();
		}
	}

	public virtual void InternalRunUpdate()
	{
		if (Enabled && CanRunUpdates && !CheckUserspaceOnly())
		{
			OnBehaviorUpdate();
			OnCommonUpdate();
		}
	}

	public virtual void InternalRunApplyChanges(int updateIndex)
	{
		IsChangeDirty = false;
		_synchronousChangeScheduled = false;
		LastChangeUpdateIndex = updateIndex;
		OnChanges();
	}

	public virtual void InternalRunAudioUpdate()
	{
		if (IsDestroyed)
		{
			return;
		}
		bool lockTaken = false;
		try
		{
			destroyLock.Enter(ref lockTaken);
			if (!IsDestroyed)
			{
				OnAudioUpdate();
			}
		}
		finally
		{
			if (lockTaken)
			{
				destroyLock.Exit();
			}
		}
	}

	public virtual void InternalRunAudioConfigurationChanged()
	{
		OnAudioConfigurationChanged();
	}

	public virtual void RunOnDestroying()
	{
		if (CheckUserspaceOnly() || _runningOnDestroying)
		{
			return;
		}
		try
		{
			_runningOnDestroying = true;
			OnDestroying();
		}
		catch (Exception exception)
		{
			base.Debug.Error($"Exception calling OnDestroying() event on:\n{this.ParentHierarchyToString()}\n{DebugManager.PreprocessException(exception)}");
		}
		finally
		{
			_runningOnDestroying = false;
		}
	}

	public virtual void InternalRunDestruction()
	{
		if (CheckUserspaceOnly())
		{
			return;
		}
		bool lockTaken = false;
		try
		{
			destroyLock.Enter(ref lockTaken);
			OnDestroy();
			if (InitInfo.HasUpdateMethods)
			{
				base.World.UpdateManager.UnregisterFromUpdates(this);
			}
			if (InitInfo.HasAudioUpdateMethod)
			{
				base.World.UpdateManager.UnregisterFromAudioUpdates(this);
			}
			if (InitInfo.HasAudioConfigurationChangedMethod)
			{
				base.World.UpdateManager.UnregisterFromAudioConfigurationChanged(this);
			}
			base.World.UnregisterEventReceiver(this);
			Dispose();
			this.Destroyed?.Invoke(this);
			this.Destroyed = null;
			this.Changed = null;
		}
		finally
		{
			if (lockTaken)
			{
				destroyLock.Exit();
			}
		}
	}

	internal void RunActivated()
	{
		if (!CheckUserspaceOnly())
		{
			OnActivated();
		}
	}

	internal void RunDeactivated()
	{
		if (!CheckUserspaceOnly())
		{
			OnDeactivated();
		}
	}

	internal void RunDuplicate()
	{
		if (!CheckUserspaceOnly())
		{
			OnDuplicate();
		}
	}

	internal void RunLinked()
	{
		if (!InitInfo.HasLinkedMethod || CheckUserspaceOnly())
		{
			return;
		}
		if (base.World.CanMakeSynchronousChanges)
		{
			try
			{
				OnLinked();
				return;
			}
			catch (Exception exception)
			{
				base.Debug.Error("Exception running OnLinked:\n" + DebugManager.PreprocessException(exception));
				return;
			}
		}
		RunSynchronously(RunLinked);
	}

	internal void RunUnlinked()
	{
		if (!InitInfo.HasUnlinkedMethod || CheckUserspaceOnly())
		{
			return;
		}
		if (base.World.CanMakeSynchronousChanges)
		{
			try
			{
				OnUnlinked();
				return;
			}
			catch (Exception exception)
			{
				base.Debug.Error("Exception running OnUnlinked:\n" + DebugManager.PreprocessException(exception));
				return;
			}
		}
		RunSynchronously(RunUnlinked);
	}

	public bool HasEventHandler(World.WorldEvent worldEvent)
	{
		return InitInfo.ReceivesWorldEvent[(int)worldEvent];
	}

	public virtual void OnFocusChanged(World.WorldFocus focus)
	{
		ShouldntExecute();
	}

	public virtual void OnWorldDestroy()
	{
		ShouldntExecute();
	}

	public virtual void OnUserJoined(User user)
	{
	}

	public virtual void OnUserSpawn(User user)
	{
	}

	public virtual void OnUserLeft(User user)
	{
	}

	public virtual void OnWorldSaved()
	{
		ShouldntExecute();
	}

	private void ShouldntExecute()
	{
		throw new InvalidOperationException("This method should never be executed! Only overriden versions must be executed if they exist.");
	}

	protected override void InitializeSyncMembers()
	{
		persistent = new Sync<bool>();
		persistent.MarkNonPersistent();
		updateOrder = new Sync<int>();
		EnabledField = new Sync<bool>();
	}
}
