using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Elements.Core;

namespace FrooxEngine;

public class CoroutineManager : SynchronizationContext, IDisposable
{
	private readonly struct TaskData
	{
		public readonly SendOrPostCallback callback;

		public readonly object state;

		public readonly IUpdatable updatable;

		public TaskData(SendOrPostCallback callback, object state, IUpdatable updatable)
		{
			this.callback = callback;
			this.state = state;
			this.updatable = updatable;
		}
	}

	private struct DelayedTask
	{
		public CoroutineHandle coroutine;

		public TaskData task;

		public Context.WorldDelay delay;

		public double amount;

		public DelayedTask(CoroutineHandle coroutine, Context.WorldDelay delay, double amount)
		{
			this.coroutine = coroutine;
			task = default(TaskData);
			this.delay = delay;
			this.amount = amount;
		}

		public DelayedTask(in TaskData task, Context.WorldDelay delay, double amount)
		{
			coroutine = null;
			this.task = task;
			this.delay = delay;
			this.amount = amount;
		}
	}

	private SpinQueue<CoroutineHandle> _backgroundCoroutines = new SpinQueue<CoroutineHandle>();

	private Action _backgroundProcessDelegate;

	public static readonly AsyncLocal<CoroutineManager> Manager = new AsyncLocal<CoroutineManager>();

	public static readonly AsyncLocal<IUpdatable> Updatable = new AsyncLocal<IUpdatable>();

	private SpinQueue<CoroutineHandle> worldQueue = new SpinQueue<CoroutineHandle>();

	private SpinQueue<TaskData> worldTaskQueue = new SpinQueue<TaskData>();

	private SpinQueue<TaskData> syncTaskQueue = new SpinQueue<TaskData>();

	private List<DelayedTask> _delayedRoutines = new List<DelayedTask>();

	private static Action<Task> _checkException = CheckExceptions;

	public World World { get; private set; }

	public Engine Engine { get; private set; }

	private WorkProcessor JobProcessor => Engine.WorkProcessor;

	private void ProcessBackgroundCoroutines()
	{
		CoroutineHandle val;
		while (_backgroundCoroutines.TryDequeue(out val))
		{
			RunStep(val);
		}
	}

	public CoroutineManager(Engine engine, World world)
	{
		_backgroundProcessDelegate = ProcessBackgroundCoroutines;
		Engine = engine;
		World = world;
	}

	public void Dispose()
	{
		Engine = null;
		World = null;
		worldQueue.Clear();
		worldTaskQueue.Clear();
		syncTaskQueue.Clear();
		_delayedRoutines.Clear();
		Manager.Value = null;
		Updatable.Value = null;
	}

	public int ExecuteWorldQueue(double deltaTime)
	{
		lock (_delayedRoutines)
		{
			for (int i = 0; i < _delayedRoutines.Count; i++)
			{
				DelayedTask value = _delayedRoutines[i];
				if (value.delay == Context.WorldDelay.Updates)
				{
					value.amount -= 1.0;
				}
				else if (value.delay == Context.WorldDelay.Seconds)
				{
					value.amount -= deltaTime;
				}
				if (value.amount <= 0.0)
				{
					if (value.coroutine != null)
					{
						worldQueue.Enqueue(value.coroutine);
					}
					else
					{
						worldTaskQueue.Enqueue(value.task);
					}
					_delayedRoutines.RemoveAt(i);
					i--;
				}
				else
				{
					_delayedRoutines[i] = value;
				}
			}
		}
		int num = 0;
		CoroutineHandle val;
		while (worldQueue.TryDequeue(out val))
		{
			IUpdatable updatable = val.updatable;
			if (updatable != null)
			{
				World?.UpdateManager.NestCurrentlyUpdating(updatable);
			}
			num++;
			RunStep(val);
			if (updatable != null)
			{
				World?.UpdateManager.PopCurrentlyUpdating(updatable);
			}
		}
		return num + ExecuteAsyncQueue(worldTaskQueue);
	}

	public int ExecuteSyncQueue()
	{
		return ExecuteAsyncQueue(syncTaskQueue);
	}

	private int ExecuteAsyncQueue(SpinQueue<TaskData> queue)
	{
		int num = 0;
		Manager.Value = this;
		SynchronizationContext.SetSynchronizationContext(this);
		TaskData val;
		while (queue.TryDequeue(out val))
		{
			if (val.updatable == null || !val.updatable.IsRemoved)
			{
				if (val.updatable != null)
				{
					World?.UpdateManager.NestCurrentlyUpdating(val.updatable);
				}
				try
				{
					num++;
					val.callback(val.state);
				}
				catch (Exception ex)
				{
					UniLog.Error("Exception when updating async task: " + ex);
				}
				if (val.updatable != null)
				{
					World?.UpdateManager.PopCurrentlyUpdating(val.updatable);
				}
			}
		}
		Manager.Value = null;
		SynchronizationContext.SetSynchronizationContext(null);
		return num;
	}

	public Task StartTask(Func<Task> task, IUpdatable updatable = null)
	{
		CoroutineManager value = Manager.Value;
		IUpdatable value2 = Updatable.Value;
		SynchronizationContext current = SynchronizationContext.Current;
		Manager.Value = this;
		Updatable.Value = updatable;
		SynchronizationContext.SetSynchronizationContext(this);
		Task task2 = task();
		task2.ContinueWith(_checkException);
		Manager.Value = value;
		Updatable.Value = value2;
		SynchronizationContext.SetSynchronizationContext(current);
		return task2;
	}

	public Task StartTask<T>(Func<T, Task> task, T argument, IUpdatable updatable = null)
	{
		CoroutineManager value = Manager.Value;
		IUpdatable value2 = Updatable.Value;
		SynchronizationContext current = SynchronizationContext.Current;
		Manager.Value = this;
		Updatable.Value = updatable;
		SynchronizationContext.SetSynchronizationContext(this);
		Task task2 = task(argument);
		task2.ContinueWith(_checkException);
		Manager.Value = value;
		Updatable.Value = value2;
		SynchronizationContext.SetSynchronizationContext(current);
		return task2;
	}

	private static void CheckExceptions(Task task)
	{
		if (task?.Exception != null)
		{
			UniLog.Error("Exception running asynchronous task:\n" + DebugManager.PreprocessException(task.Exception));
		}
	}

	public Task<T> StartTask<T>(Func<Task<T>> task, IUpdatable updatable = null)
	{
		CoroutineManager value = Manager.Value;
		IUpdatable value2 = Updatable.Value;
		SynchronizationContext current = SynchronizationContext.Current;
		Manager.Value = this;
		Updatable.Value = updatable;
		SynchronizationContext.SetSynchronizationContext(this);
		Task<T> task2 = task();
		task2.ContinueWith(_checkException);
		Manager.Value = value;
		Updatable.Value = value2;
		SynchronizationContext.SetSynchronizationContext(current);
		return task2;
	}

	public Task StartBackgroundTask(Func<Task> task, IUpdatable updatable = null)
	{
		CoroutineManager value = Manager.Value;
		IUpdatable value2 = Updatable.Value;
		SynchronizationContext current = SynchronizationContext.Current;
		Manager.Value = this;
		Updatable.Value = updatable;
		SynchronizationContext.SetSynchronizationContext(null);
		Task task2 = Task.Run(task);
		task2.ContinueWith(_checkException);
		Manager.Value = value;
		Updatable.Value = value2;
		SynchronizationContext.SetSynchronizationContext(current);
		return task2;
	}

	public Task<T> StartBackgroundTask<T>(Func<Task<T>> task, IUpdatable updatable = null)
	{
		CoroutineManager value = Manager.Value;
		IUpdatable value2 = Updatable.Value;
		SynchronizationContext current = SynchronizationContext.Current;
		Manager.Value = this;
		Updatable.Value = updatable;
		SynchronizationContext.SetSynchronizationContext(null);
		Task<T> task2 = Task.Run(task);
		task2.ContinueWith(_checkException);
		Manager.Value = value;
		Updatable.Value = value2;
		SynchronizationContext.SetSynchronizationContext(current);
		return task2;
	}

	public Coroutine StartCoroutine(IEnumerator<Context> coroutine, Action<Coroutine> onDone = null, IUpdatable updatable = null)
	{
		CoroutineHandle coroutineHandle = Pool<CoroutineHandle>.Borrow();
		int id = coroutineHandle.id;
		coroutineHandle.coroutineIterator = coroutine;
		coroutineHandle.Done = onDone;
		coroutineHandle.updatable = updatable;
		coroutineHandle.lastContext = Context.TargetContext.World;
		RunStep(coroutineHandle);
		return new Coroutine(id, coroutineHandle);
	}

	public Coroutine RunInSeconds(float seconds, Action action)
	{
		return StartCoroutine(RunInSecondsCo(seconds, action));
	}

	public Coroutine RunInUpdates(int updates, Action action)
	{
		return StartCoroutine(RunInUpdatesCo(updates, action));
	}

	private IEnumerator<Context> RunInSecondsCo(float seconds, Action action)
	{
		yield return Context.WaitForSeconds(seconds);
		action();
	}

	private IEnumerator<Context> RunInUpdatesCo(int updates, Action action)
	{
		yield return Context.WaitForUpdates(updates);
		action();
	}

	public override void Post(SendOrPostCallback d, object state)
	{
		PostDelayed(d, state, 0);
	}

	internal void PostToSync(SendOrPostCallback d, object state)
	{
		syncTaskQueue.Enqueue(new TaskData(d, state, Updatable.Value));
	}

	internal void PostDelayed(SendOrPostCallback d, object state, int updates)
	{
		if (updates == 0)
		{
			worldTaskQueue.Enqueue(new TaskData(d, state, Updatable.Value));
			return;
		}
		DelayedTask item = new DelayedTask(new TaskData(d, state, Updatable.Value), Context.WorldDelay.Updates, updates);
		lock (_delayedRoutines)
		{
			_delayedRoutines.Add(item);
		}
	}

	private void RunStep(CoroutineHandle coroutine)
	{
		if (coroutine.ShouldStop)
		{
			coroutine.Finish();
			coroutine.Clear();
			Pool<CoroutineHandle>.ReturnCleaned(ref coroutine);
			return;
		}
		try
		{
			if (coroutine.coroutineIterator.MoveNext())
			{
				if (coroutine.coroutineIterator.Current.target == Context.TargetContext.Coroutine)
				{
					CoroutineHandle coroutineHandle = Pool<CoroutineHandle>.Borrow();
					coroutineHandle.coroutineIterator = coroutine.coroutineIterator.Current.coroutine;
					coroutineHandle.onFinish = coroutine;
					coroutineHandle.lastContext = coroutine.lastContext;
					if (coroutine.coroutineIterator.Current.continueInBackground)
					{
						coroutine.lastContext = Context.TargetContext.Background;
					}
					coroutine.waitingFor = coroutineHandle;
					coroutine.waitingForId = coroutineHandle.id;
					RunStep(coroutineHandle);
				}
				else
				{
					ScheduleNext(coroutine, coroutine.coroutineIterator.Current);
				}
			}
			else
			{
				coroutine.Finish();
				if (coroutine.onFinish != null)
				{
					coroutine.onFinish.waitingFor = null;
					ScheduleNext(coroutine.onFinish, new Context(coroutine.onFinish.lastContext));
				}
				coroutine.Clear();
				Pool<CoroutineHandle>.ReturnCleaned(ref coroutine);
			}
		}
		catch (Exception exception)
		{
			string text = "Exception in RunningCoroutine: " + coroutine.coroutineIterator?.ToString() + "\n" + DebugManager.PreprocessException(exception);
			if (World != null)
			{
				World.Debug.Error(text);
			}
			else
			{
				UniLog.Log(text);
			}
			FinishChain(coroutine);
		}
	}

	private void FinishChain(CoroutineHandle coroutine)
	{
		coroutine.Finish();
		if (coroutine.onFinish != null)
		{
			FinishChain(coroutine.onFinish);
		}
		coroutine.Clear();
		Pool<CoroutineHandle>.ReturnCleaned(ref coroutine);
	}

	private void ScheduleNext(CoroutineHandle coroutine, in Context context)
	{
		switch (context.target)
		{
		case Context.TargetContext.Background:
			coroutine.lastContext = Context.TargetContext.Background;
			ScheduleBackground(coroutine);
			break;
		case Context.TargetContext.World:
		{
			coroutine.lastContext = Context.TargetContext.World;
			if (context.delay == Context.WorldDelay.None)
			{
				worldQueue.Enqueue(coroutine);
				break;
			}
			DelayedTask item = new DelayedTask(coroutine, context.delay, context.delayLength);
			lock (_delayedRoutines)
			{
				_delayedRoutines.Add(item);
				break;
			}
		}
		case Context.TargetContext.Job:
		{
			bool continueInBackground = context.continueInBackground;
			context.task.OnDone += delegate
			{
				if (continueInBackground)
				{
					coroutine.lastContext = Context.TargetContext.Background;
				}
				ScheduleNext(coroutine, new Context(coroutine.lastContext));
			};
			break;
		}
		default:
			throw new Exception("Invalid SchedulNext context, this shouldn't happen!");
		}
	}

	private void ScheduleBackground(CoroutineHandle coroutine)
	{
		_backgroundCoroutines.Enqueue(coroutine);
		JobProcessor.Enqueue(_backgroundProcessDelegate);
	}
}
