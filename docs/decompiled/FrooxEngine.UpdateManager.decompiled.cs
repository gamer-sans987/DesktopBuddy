using System;
using System.Collections.Generic;
using System.Threading;
using Elements.Core;

namespace FrooxEngine;

public class UpdateManager : IDisposable
{
	private User _currentlyUpdatingUser;

	private Queue<IUpdatable> slotsToStartup = new Queue<IUpdatable>();

	private Queue<IUpdatable> toStartup = new Queue<IUpdatable>();

	private Queue<IUpdatable> toDestroy = new Queue<IUpdatable>();

	private SortedDictionary<int, List<IUpdatable>> toUpdate = new SortedDictionary<int, List<IUpdatable>>();

	private List<IUpdatable> moveUpdateBuckets = new List<IUpdatable>();

	private SortedDictionary<int, Queue<IUpdatable>> toApplyChanges = new SortedDictionary<int, Queue<IUpdatable>>();

	private Queue<IUpdatable> toApplyChangesNext = new Queue<IUpdatable>();

	private SpinQueue<IUpdatable> toApplyChangesBuffer = new SpinQueue<IUpdatable>();

	private SpinLock audioUpdateLock = new SpinLock(enableThreadOwnerTracking: false);

	private SpinLock changesLock = new SpinLock(enableThreadOwnerTracking: false);

	private List<IAudioUpdatable> toAudioUpdate = new List<IAudioUpdatable>();

	private List<IAudioUpdatable> toAudioConfigurationChanged = new List<IAudioUpdatable>();

	private Stack<IUpdatable> currentlyUpdatingStack = new Stack<IUpdatable>();

	private SortedDictionary<int, List<IUpdatable>>.Enumerator updateBucketEnumerator;

	private int updateIndex;

	private bool updatesRunning;

	private SortedDictionary<int, Queue<IUpdatable>>.Enumerator changeBucketEnumerator;

	private Dictionary<SyncElement, List<IInitializable>> initializableChildren = new Dictionary<SyncElement, List<IInitializable>>();

	private HashSet<Slot> activatedEvents = new HashSet<Slot>();

	private int changeUpdateIndex = 1;

	private List<IAudioUpdatable> _tempAudioUpdate = new List<IAudioUpdatable>();

	private Action<IUpdatable> _processDestruction;

	private Action<IUpdatable> _processChange;

	public Engine Engine => World.Engine;

	public World World { get; private set; }

	public User CurrentlyUpdatingUser
	{
		get
		{
			return _currentlyUpdatingUser ?? World.LocalUser;
		}
		set
		{
			_currentlyUpdatingUser = value;
		}
	}

	public IUpdatable CurrentlyUpdating { get; internal set; }

	public event Action<IUpdatable> UpdatableChanged;

	public UpdateManager(World world)
	{
		World = world;
		_processDestruction = ProcessDestruction;
		_processChange = ProcessChange;
	}

	public void Dispose()
	{
		World = null;
		_currentlyUpdatingUser = null;
		this.UpdatableChanged = null;
		slotsToStartup?.Clear();
		toStartup?.Clear();
		toDestroy?.Clear();
		toUpdate?.Clear();
		moveUpdateBuckets?.Clear();
		toApplyChanges?.Clear();
		toApplyChangesNext?.Clear();
		toApplyChangesBuffer?.Clear();
		toAudioUpdate?.Clear();
		toAudioConfigurationChanged?.Clear();
		currentlyUpdatingStack?.Clear();
		updateBucketEnumerator = default(SortedDictionary<int, List<IUpdatable>>.Enumerator);
		changeBucketEnumerator = default(SortedDictionary<int, Queue<IUpdatable>>.Enumerator);
		initializableChildren?.Clear();
		activatedEvents?.Clear();
	}

	public void NestCurrentlyUpdating(IUpdatable current)
	{
		currentlyUpdatingStack.Push(CurrentlyUpdating);
		CurrentlyUpdating = current;
	}

	public void PopCurrentlyUpdating(IUpdatable current)
	{
		if (CurrentlyUpdating != current)
		{
			throw new InvalidOperationException($"CurrentlyUpdating mismatch.\nCurrent: {CurrentlyUpdating}\nCalling: {current}\nStack: {string.Join("\n", currentlyUpdatingStack)}");
		}
		CurrentlyUpdating = currentlyUpdatingStack.Pop();
	}

	private void RestoreRootCurrentlyUpdating()
	{
		UniLog.Warning("Restoring currently updating root. Stack:\n" + string.Join("\n", currentlyUpdatingStack), stackTrace: true);
		while (currentlyUpdatingStack.Count > 0)
		{
			PopCurrentlyUpdating(CurrentlyUpdating);
		}
	}

	public void RegisterForStartup(IUpdatable updatable)
	{
		if (updatable == null)
		{
			throw new ArgumentNullException("updatable");
		}
		if (updatable is Slot)
		{
			slotsToStartup.Enqueue(updatable);
		}
		else
		{
			toStartup.Enqueue(updatable);
		}
	}

	public void RegisterToDestroy(IUpdatable updatable)
	{
		if (updatable == null)
		{
			throw new ArgumentNullException("updatable");
		}
		toDestroy.Enqueue(updatable);
	}

	public void RegisterForUpdates(IUpdatable updatable)
	{
		if (updatable == null)
		{
			throw new ArgumentNullException("updatable");
		}
		GetUpdateBucket(updatable.UpdateOrder).Add(updatable);
	}

	public void UpdateBucketChanged(IUpdatable updatable)
	{
		if (updatesRunning)
		{
			moveUpdateBuckets.Add(updatable);
		}
		else
		{
			MoveToNewBucket(updatable);
		}
	}

	private List<IUpdatable> GetUpdateBucket(int bucket, bool createIfNotExists = true)
	{
		if (toUpdate.TryGetValue(bucket, out List<IUpdatable> value))
		{
			return value;
		}
		if (createIfNotExists)
		{
			value = new List<IUpdatable>();
			toUpdate.Add(bucket, value);
			return value;
		}
		return null;
	}

	private bool RemoveFromUpdateBucket(IUpdatable updatable)
	{
		int updateOrder = updatable.UpdateOrder;
		List<IUpdatable> updateBucket = GetUpdateBucket(updateOrder, createIfNotExists: false);
		if (updateBucket != null && updateBucket.Remove(updatable))
		{
			if (updateBucket.Count == 0)
			{
				toUpdate.Remove(updateOrder);
			}
			return true;
		}
		foreach (KeyValuePair<int, List<IUpdatable>> item in toUpdate)
		{
			if (item.Key != updateOrder && item.Value.Remove(updatable))
			{
				if (item.Value.Count == 0)
				{
					toUpdate.Remove(item.Key);
				}
				return true;
			}
		}
		return false;
	}

	private void MoveToNewBucket(IUpdatable updatable)
	{
		if (RemoveFromUpdateBucket(updatable))
		{
			GetUpdateBucket(updatable.UpdateOrder).Add(updatable);
		}
	}

	public void RegisterForAudioUpdates(IAudioUpdatable updatable)
	{
		if (updatable == null)
		{
			throw new ArgumentNullException("updatable");
		}
		bool lockTaken = false;
		try
		{
			audioUpdateLock.Enter(ref lockTaken);
			toAudioUpdate.Add(updatable);
		}
		finally
		{
			if (lockTaken)
			{
				audioUpdateLock.Exit();
			}
		}
	}

	public void RegisterForAudioConfigurationChanged(IAudioUpdatable updatable)
	{
		if (updatable == null)
		{
			throw new ArgumentNullException("updatable");
		}
		toAudioConfigurationChanged.Add(updatable);
	}

	public void Changed(IUpdatable updatable, bool triggerEvent = true)
	{
		if (updatable == null)
		{
			throw new ArgumentNullException("updatable");
		}
		if (!updatable.IsStarted)
		{
			throw new Exception("Cannot register change dirty before startup!");
		}
		bool lockTaken = false;
		try
		{
			changesLock.Enter(ref lockTaken);
			if (triggerEvent)
			{
				this.UpdatableChanged?.Invoke(updatable);
			}
			if (World.CanMakeSynchronousChanges)
			{
				if (updatable.LastChangeUpdateIndex != changeUpdateIndex)
				{
					AddToBucket(toApplyChanges, updatable);
				}
				else
				{
					toApplyChangesNext.Enqueue(updatable);
				}
			}
			else
			{
				toApplyChangesBuffer.Enqueue(updatable);
			}
		}
		catch (Exception exception)
		{
			UniLog.Error("Exception registerting changed updatable:\n" + DebugManager.PreprocessException(exception));
			World.Destroy();
		}
		finally
		{
			if (lockTaken)
			{
				changesLock.Exit();
			}
		}
	}

	private void AddToBucket(SortedDictionary<int, Queue<IUpdatable>> buckets, IUpdatable item)
	{
		if (!buckets.TryGetValue(item.UpdateOrder, out Queue<IUpdatable> value))
		{
			value = new Queue<IUpdatable>();
			buckets.Add(item.UpdateOrder, value);
		}
		value.Enqueue(item);
	}

	public void UnregisterFromUpdates(IUpdatable updatable)
	{
		if (updatable == null)
		{
			throw new ArgumentNullException("updatable");
		}
		RemoveFromUpdateBucket(updatable);
	}

	public void UnregisterFromAudioUpdates(IAudioUpdatable updatable)
	{
		if (updatable == null)
		{
			throw new ArgumentNullException("updatable");
		}
		bool lockTaken = false;
		try
		{
			audioUpdateLock.Enter(ref lockTaken);
			toAudioUpdate.Remove(updatable);
		}
		finally
		{
			if (lockTaken)
			{
				audioUpdateLock.Exit();
			}
		}
	}

	public void UnregisterFromAudioConfigurationChanged(IAudioUpdatable updatable)
	{
		if (updatable == null)
		{
			throw new ArgumentNullException("updatable");
		}
		toAudioConfigurationChanged.Remove(updatable);
	}

	public void UpdateStreams()
	{
		foreach (User allUser in World.AllUsers)
		{
			foreach (Stream stream in allUser.Streams)
			{
				stream.Update();
			}
		}
	}

	public void PrepareUpdateCycle()
	{
		updatesRunning = true;
		updateIndex = 0;
		updateBucketEnumerator = toUpdate.GetEnumerator();
		updateBucketEnumerator.MoveNext();
	}

	public void PrepareChangesCycle()
	{
		IUpdatable val;
		while (toApplyChangesBuffer.TryDequeue(out val))
		{
			Changed(val, triggerEvent: false);
		}
		changeBucketEnumerator = toApplyChanges.GetEnumerator();
		changeBucketEnumerator.MoveNext();
	}

	public void FinishChangeUpdateCycle()
	{
		changeUpdateIndex++;
		while (toApplyChangesNext.Count > 0)
		{
			AddToBucket(toApplyChanges, toApplyChangesNext.Dequeue());
		}
	}

	public bool RunUpdates()
	{
		if (toUpdate.Count == 0 || updateBucketEnumerator.Current.Value == null)
		{
			return true;
		}
		try
		{
			while (updateBucketEnumerator.Current.Value != null)
			{
				List<IUpdatable> value = updateBucketEnumerator.Current.Value;
				while (updateIndex < value.Count)
				{
					IUpdatable updatable = value[updateIndex++];
					if (!updatable.IsRemoved)
					{
						World.LastCommonUpdates++;
						CurrentlyUpdating = updatable;
						updatable.InternalRunUpdate();
						CurrentlyUpdating = null;
					}
				}
				if (updateIndex == value.Count)
				{
					updateIndex = 0;
					updateBucketEnumerator.MoveNext();
				}
			}
		}
		catch (OutOfMemoryException)
		{
			Engine.ForceCrash();
			throw;
		}
		catch (FatalWorldException)
		{
			throw;
		}
		catch (Exception exception)
		{
			RestoreRootCurrentlyUpdating();
			UniLog.Log("Exception when Updating object: " + CurrentlyUpdating.ParentHierarchyToString() + "\n\nException:\n" + DebugManager.PreprocessException(exception));
			if (CurrentlyUpdating is Component component)
			{
				ExceptionAction exceptionAction = ExceptionAction.Disable;
				ExceptionHandlingAttribute customAttribute = component.GetType().GetCustomAttribute<ExceptionHandlingAttribute>(inherit: true, fromInterfaces: false);
				if (customAttribute != null)
				{
					exceptionAction = customAttribute.ExceptionAction;
				}
				bool flag = component.Slot.IsRootSlot || component.Slot.IsProtected;
				switch (exceptionAction)
				{
				case ExceptionAction.Disable:
					component.EnabledField.ActiveLink?.ReleaseLink();
					component.Enabled = false;
					break;
				case ExceptionAction.DeactivateSlot:
					component.EnabledField.ActiveLink?.ReleaseLink();
					component.Enabled = false;
					if (!flag)
					{
						component.Slot.ActiveSelf_Field.ActiveLink?.ReleaseLink();
						component.Slot.ActiveSelf = false;
					}
					break;
				case ExceptionAction.Destroy:
					component.Destroy();
					break;
				case ExceptionAction.DestroySlot:
					if (flag)
					{
						component.Destroy();
					}
					else
					{
						component.Slot.Destroy();
					}
					break;
				case ExceptionAction.DestroyUserRoot:
					(component.Slot.ActiveUserRoot?.Slot ?? component.Slot.GetObjectRoot()).Destroy();
					break;
				}
			}
			CurrentlyUpdating = null;
		}
		return updateBucketEnumerator.Current.Value == null;
	}

	public void FinishUpdateCycle()
	{
		updatesRunning = false;
		foreach (IUpdatable moveUpdateBucket in moveUpdateBuckets)
		{
			MoveToNewBucket(moveUpdateBucket);
		}
		moveUpdateBuckets.Clear();
	}

	public void RunAudioUpdates()
	{
		bool lockTaken = false;
		try
		{
			audioUpdateLock.Enter(ref lockTaken);
			foreach (IAudioUpdatable item in toAudioUpdate)
			{
				_tempAudioUpdate.Add(item);
			}
		}
		finally
		{
			if (lockTaken)
			{
				audioUpdateLock.Exit();
			}
		}
		foreach (IAudioUpdatable item2 in _tempAudioUpdate)
		{
			item2.InternalRunAudioUpdate();
		}
		_tempAudioUpdate.Clear();
	}

	public void RunAudioConfigurationChanged()
	{
		int num = 0;
		IAudioUpdatable audioUpdatable = null;
		while (num < toAudioConfigurationChanged.Count)
		{
			try
			{
				while (num < toAudioConfigurationChanged.Count)
				{
					audioUpdatable = toAudioConfigurationChanged[num];
					num++;
					audioUpdatable.InternalRunAudioConfigurationChanged();
				}
			}
			catch (FatalWorldException)
			{
				throw;
			}
			catch (Exception exception)
			{
				UniLog.Log("Exception when running AudioConfigurationChanged : " + (audioUpdatable as IWorldElement)?.ParentHierarchyToString() + "\n\nException:\n" + DebugManager.PreprocessException(exception));
			}
		}
	}

	public bool RunStartups()
	{
		try
		{
			while (toStartup.Count > 0 || slotsToStartup.Count > 0)
			{
				IUpdatable updatable = ((slotsToStartup.Count <= 0) ? toStartup.Dequeue() : slotsToStartup.Dequeue());
				if (!updatable.IsRemoved)
				{
					CurrentlyUpdating = updatable;
					updatable.InternalRunStartup();
					CurrentlyUpdating = null;
				}
			}
		}
		catch (OutOfMemoryException)
		{
			Engine.ForceCrash();
			throw;
		}
		catch (FatalWorldException)
		{
			throw;
		}
		catch (Exception exception)
		{
			RestoreRootCurrentlyUpdating();
			UniLog.Log("Exception when running Startup on object: " + CurrentlyUpdating.ParentHierarchyToString() + "\n\nException:\n" + DebugManager.PreprocessException(exception));
			if (CurrentlyUpdating is Component component)
			{
				component.Enabled = false;
			}
			CurrentlyUpdating = null;
		}
		if (toStartup.Count == 0)
		{
			return slotsToStartup.Count == 0;
		}
		return false;
	}

	public bool RunDestructions()
	{
		return RunQueue(toDestroy, _processDestruction);
	}

	public bool RunChangeApplications()
	{
		if (changeBucketEnumerator.Current.Value == null)
		{
			return true;
		}
		while (changeBucketEnumerator.Current.Value != null)
		{
			if (RunQueue(changeBucketEnumerator.Current.Value, _processChange))
			{
				changeBucketEnumerator.MoveNext();
			}
		}
		return true;
	}

	private void ProcessDestruction(IUpdatable updatable)
	{
		CurrentlyUpdating = updatable;
		updatable.InternalRunDestruction();
		CurrentlyUpdating = null;
	}

	private void ProcessChange(IUpdatable updatable)
	{
		World.LastChanges++;
		if (!updatable.IsRemoved)
		{
			CurrentlyUpdating = updatable;
			updatable.InternalRunApplyChanges(changeUpdateIndex);
			CurrentlyUpdating = null;
		}
	}

	private bool RunQueue<T>(Queue<T> queue, Action<T> action) where T : class
	{
		try
		{
			while (queue.Count > 0)
			{
				T obj = queue.Dequeue();
				action(obj);
			}
		}
		catch (OutOfMemoryException)
		{
			Engine.ForceCrash();
			throw;
		}
		catch (FatalWorldException)
		{
			throw;
		}
		catch (Exception exception)
		{
			RestoreRootCurrentlyUpdating();
			UniLog.Error("Exception when Updating object: " + CurrentlyUpdating.ParentHierarchyToString() + "\n\nException:\n" + DebugManager.PreprocessException(exception), stackTrace: false);
			if (CurrentlyUpdating is Component component)
			{
				component.Enabled = false;
			}
			CurrentlyUpdating = null;
		}
		return queue.Count == 0;
	}

	public void ActiveStateChagned(Slot slot)
	{
		bool lockTaken = false;
		try
		{
			changesLock.Enter(ref lockTaken);
			activatedEvents.Add(slot);
		}
		finally
		{
			if (lockTaken)
			{
				changesLock.Exit();
			}
		}
	}

	public void RunActiveStateChangedEvents()
	{
		bool lockTaken = false;
		List<Slot> list = Pool.BorrowList<Slot>();
		try
		{
			changesLock.Enter(ref lockTaken);
			foreach (Slot activatedEvent in activatedEvents)
			{
				if (!activatedEvent.IsRemoved)
				{
					list.Add(activatedEvent);
				}
			}
			activatedEvents.Clear();
		}
		finally
		{
			if (lockTaken)
			{
				changesLock.Exit();
			}
		}
		foreach (Slot item in list)
		{
			item.SendActivatedEvents();
		}
		Pool.Return(ref list);
	}

	public void AddInitializableChild(SyncElement element, IInitializable initializable)
	{
		if (!initializableChildren.TryGetValue(element, out List<IInitializable> value))
		{
			value = Pool.BorrowList<IInitializable>();
			initializableChildren.Add(element, value);
		}
		value.Add(initializable);
	}

	public void EndInitPhaseInChildren(SyncElement element)
	{
		if (!initializableChildren.TryGetValue(element, out List<IInitializable> value))
		{
			return;
		}
		foreach (IInitializable item in value)
		{
			item.EndInitPhase();
		}
		initializableChildren.Remove(element);
		Pool.Return(ref value);
	}
}
