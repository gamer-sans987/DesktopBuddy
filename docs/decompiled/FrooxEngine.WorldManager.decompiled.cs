using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Elements.Core;
using FrooxEngine.Store;
using Renderite.Shared;
using SkyFrost.Base;

namespace FrooxEngine;

public class WorldManager
{
	public enum UpdateStage
	{
		UpdateBegin,
		WorldUpdating,
		Finished
	}

	private int _worldHandleIndex;

	private World _setWorldFocus;

	private List<World> _setWorldOverlay = new List<World>();

	private List<World> _setWorldPrivateOverlay = new List<World>();

	private List<World> _removeWorldOverlay = new List<World>();

	private List<World> _destroyWorlds = new List<World>();

	private List<World> worlds = new List<World>();

	private List<World> _overlayWorlds = new List<World>();

	private List<World> _privateOverlayWorlds = new List<World>();

	private UpdateStage stage;

	private Stopwatch stopwatch = new Stopwatch();

	private Queue<World> worldsToUpdate = new Queue<World>();

	private List<World> _audioUpdateWorlds = new List<World>();

	private DictionaryList<string, WorldIdLinkHandler> _worldIdListeners = new DictionaryList<string, WorldIdLinkHandler>();

	private DictionaryList<string, WorldIdLinkHandler> _sessionIdListeners = new DictionaryList<string, WorldIdLinkHandler>();

	private bool worldsChanged;

	private static string[] updateStageNames = Enum.GetNames(typeof(UpdateStage));

	public Engine Engine { get; private set; }

	public World FocusedWorld { get; private set; }

	public IEnumerable<World> OverlayWorlds => _overlayWorlds;

	public IEnumerable<World> PrivateOverlayWorlds => _privateOverlayWorlds;

	public LocalDB LocalDB => Engine.LocalDB;

	public IEnumerable<World> Worlds => worlds;

	public int WorldCount => worlds.Count;

	public World this[int index] => worlds[index];

	public event Action WorldsChanged;

	public event Action<World> WorldAdded;

	public event Action<World> WorldFocused;

	public event Action<World> WorldRemoved;

	public event Action<World> WorldFailed;

	public int AllocateWorldHandle()
	{
		return Interlocked.Increment(ref _worldHandleIndex);
	}

	internal async Task Initialize(Engine engine)
	{
		Engine = engine;
	}

	public void GetWorlds(List<World> list)
	{
		lock (worlds)
		{
			foreach (World world in worlds)
			{
				list.Add(world);
			}
		}
	}

	public World GetWorld(Func<World, bool> predicate)
	{
		lock (worlds)
		{
			foreach (World world in worlds)
			{
				if (predicate(world))
				{
					return world;
				}
			}
			return null;
		}
	}

	public World GetWorld(int localHandle)
	{
		lock (worlds)
		{
			foreach (World world in worlds)
			{
				if (world.LocalWorldHandle == localHandle)
				{
					return world;
				}
			}
			return null;
		}
	}

	public World GetWorld(FrooxEngine.Store.Record record)
	{
		lock (worlds)
		{
			foreach (World world in worlds)
			{
				if (world.CorrespondingRecord != null && world.CorrespondingRecord.IsSameRecord(record))
				{
					return world;
				}
			}
			return null;
		}
	}

	public World GetWorld(Uri uri)
	{
		lock (worlds)
		{
			foreach (World world in worlds)
			{
				if (world.SourceLink?.URL == uri || world.RecordURL == uri)
				{
					return world;
				}
			}
			return null;
		}
	}

	public World StartLocal(WorldAction init, DataTreeDictionary load = null, IEnumerable<AssemblyTypeRegistry> assemblies = null)
	{
		World world = World.LocalWorld(this, init, load, unsafeMode: false, assemblies);
		lock (worlds)
		{
			worlds.Add(world);
		}
		RunWorldAdded(world);
		return world;
	}

	public World StartSession(WorldAction init = null, ushort port = 0, string forceSessionId = null, DataTreeDictionary load = null, FrooxEngine.Store.Record record = null, bool unsafeMode = false, IEnumerable<AssemblyTypeRegistry> assemblies = null)
	{
		World world = World.StartSession(this, init, port, forceSessionId, load, record, unsafeMode, assemblies);
		lock (worlds)
		{
			worlds.Add(world);
		}
		RunWorldAdded(world);
		return world;
	}

	public World JoinSession(IEnumerable<Uri> addresses)
	{
		World world = World.JoinSession(this, addresses);
		lock (worlds)
		{
			worlds.Add(world);
		}
		RunWorldAdded(world);
		return world;
	}

	public void FocusWorld(World world)
	{
		if (world.IsDestroyed)
		{
			throw new Exception("Cannot focus destroyed world: " + world.RawName);
		}
		_setWorldFocus = world;
	}

	public void OverlayWorld(World world)
	{
		lock (_setWorldOverlay)
		{
			_setWorldOverlay.Add(world);
		}
	}

	public void PrivateOverlayWorld(World world)
	{
		lock (_setWorldPrivateOverlay)
		{
			_setWorldPrivateOverlay.Add(world);
		}
	}

	public void DestroyWorld(World world)
	{
		lock (_destroyWorlds)
		{
			_destroyWorlds.Add(world);
		}
	}

	public void RegisterWorldIdEvents(string worldId, WorldIdLinkHandler callback)
	{
		RegisterIdEvents(_worldIdListeners, (World w) => w.CorrespondingWorldId, worldId, callback);
	}

	public void UnregisterWorldIdEvents(string worldId, WorldIdLinkHandler callback)
	{
		UnregisterIdEvents(_worldIdListeners, worldId, callback);
	}

	public void RegisterSessionIdEvents(string sesionId, WorldIdLinkHandler callback)
	{
		RegisterIdEvents(_sessionIdListeners, (World w) => w.SessionId, sesionId, callback);
	}

	public void UnregisterSesionIdEvents(string sessionId, WorldIdLinkHandler callback)
	{
		UnregisterIdEvents(_sessionIdListeners, sessionId, callback);
	}

	internal void WorldIdLinked(World world)
	{
		IdLinked(_worldIdListeners, world, world.CorrespondingWorldId);
	}

	internal void WorldIdUnlinked(World world, string worldId)
	{
		IdUnlinked(_worldIdListeners, world, worldId);
	}

	internal void SessionIdLinked(World world)
	{
		IdLinked(_sessionIdListeners, world, world.SessionId);
	}

	internal void SessionIdUnlinked(World world, string sessionId)
	{
		IdUnlinked(_sessionIdListeners, world, sessionId);
	}

	private void RegisterIdEvents(DictionaryList<string, WorldIdLinkHandler> listeners, Func<World, string> idFetcher, string id, WorldIdLinkHandler callback)
	{
		id = id.ToLowerInvariant();
		lock (listeners)
		{
			listeners.Add(id, callback);
			lock (worlds)
			{
				foreach (World world in worlds)
				{
					if (id.Equals(idFetcher(world), StringComparison.InvariantCultureIgnoreCase))
					{
						callback(id, world, WorldIdLinkStatus.Linked);
					}
				}
			}
		}
	}

	private void UnregisterIdEvents(DictionaryList<string, WorldIdLinkHandler> listeners, string id, WorldIdLinkHandler callback)
	{
		id = id.ToLowerInvariant();
		lock (listeners)
		{
			listeners.Remove(id, callback);
		}
	}

	private void IdLinked(DictionaryList<string, WorldIdLinkHandler> listeners, World world, string id)
	{
		id = id.ToLowerInvariant();
		lock (listeners)
		{
			List<WorldIdLinkHandler> list = listeners.TryGetList(id);
			if (list == null)
			{
				return;
			}
			foreach (WorldIdLinkHandler item in list)
			{
				item(id, world, WorldIdLinkStatus.Linked);
			}
		}
	}

	private void IdUnlinked(DictionaryList<string, WorldIdLinkHandler> listeners, World world, string id)
	{
		id = id.ToLower();
		lock (listeners)
		{
			List<WorldIdLinkHandler> list = listeners.TryGetList(id);
			if (list == null)
			{
				return;
			}
			foreach (WorldIdLinkHandler item in list)
			{
				item(id, world, WorldIdLinkStatus.Unliked);
			}
		}
	}

	private void RunWorldAdded(World world)
	{
		Delegate[] array = this.WorldAdded?.GetInvocationList();
		if (array != null)
		{
			Delegate[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				Action<World> action = (Action<World>)array2[i];
				try
				{
					action(world);
				}
				catch (Exception exception)
				{
					UniLog.Error("SEVERE!!! Exception running WorldAdded:\n" + DebugManager.PreprocessException(exception));
				}
			}
		}
		worldsChanged = true;
	}

	private void RunWorldRemoved(World world)
	{
		Delegate[] array = this.WorldRemoved?.GetInvocationList();
		if (array != null)
		{
			Delegate[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				Action<World> action = (Action<World>)array2[i];
				try
				{
					action(world);
				}
				catch (Exception exception)
				{
					UniLog.Error("SEVERE!!! Exception running WorldRemoved:\n" + DebugManager.PreprocessException(exception));
				}
			}
		}
		worldsChanged = true;
	}

	private void RunWorldFocused(World world)
	{
		Delegate[] array = this.WorldFocused?.GetInvocationList();
		if (array != null)
		{
			Delegate[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				Action<World> action = (Action<World>)array2[i];
				try
				{
					action(world);
				}
				catch (Exception exception)
				{
					UniLog.Error("SEVERE!!! Exception running WorldFocused:\n" + DebugManager.PreprocessException(exception));
				}
			}
		}
		worldsChanged = true;
	}

	private void RunWorldFailed(World world)
	{
		Delegate[] array = this.WorldFailed?.GetInvocationList();
		if (array == null)
		{
			return;
		}
		Delegate[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			Action<World> action = (Action<World>)array2[i];
			try
			{
				action(world);
			}
			catch (Exception exception)
			{
				UniLog.Error("SEVERE!!! Exception running WorldFailed:\n" + DebugManager.PreprocessException(exception));
			}
		}
	}

	private void TryRunWorldsChanged()
	{
		if (!worldsChanged)
		{
			return;
		}
		Delegate[] array = this.WorldsChanged?.GetInvocationList();
		if (array != null)
		{
			Delegate[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				Action action = (Action)array2[i];
				try
				{
					action();
				}
				catch (Exception exception)
				{
					UniLog.Error("SEVERE!!! Exception running WorldsChanged:\n" + DebugManager.PreprocessException(exception));
				}
			}
		}
		worldsChanged = false;
	}

	public bool RunUpdateLoop()
	{
		stopwatch.Restart();
		stage = UpdateStage.UpdateBegin;
		while (stage != UpdateStage.Finished)
		{
			UpdateStep();
		}
		stopwatch.Stop();
		return true;
	}

	public void RunAudioUpdates()
	{
		lock (worlds)
		{
			foreach (World world in worlds)
			{
				if (Engine.InputInterface.HeadOutputDevice == HeadOutputDevice.Headless || (world.Focus != World.WorldFocus.Background && world.State == World.WorldState.Running))
				{
					_audioUpdateWorlds.Add(world);
				}
			}
		}
		foreach (World audioUpdateWorld in _audioUpdateWorlds)
		{
			audioUpdateWorld.RunAudioUpdates();
		}
		_audioUpdateWorlds.Clear();
	}

	public void Dispose()
	{
		lock (worlds)
		{
			foreach (World world in worlds)
			{
				world.Dispose();
			}
		}
	}

	private void UpdateStep()
	{
		switch (stage)
		{
		case UpdateStage.UpdateBegin:
		{
			World world2 = _setWorldFocus;
			_setWorldFocus = null;
			if (world2 != null && world2.IsDestroyed)
			{
				world2 = null;
			}
			if (FocusedWorld != null && FocusedWorld.IsDestroyed && world2 == null)
			{
				world2 = GetWorld((World world3) => !world3.IsDestroyed && world3.Focus == World.WorldFocus.Background && world3.State == World.WorldState.Running);
			}
			if (world2 != FocusedWorld && world2 != null)
			{
				if (FocusedWorld != null && !FocusedWorld.IsDestroyed)
				{
					FocusedWorld.Focus = World.WorldFocus.Background;
					World w0 = FocusedWorld;
					if (Engine.InputInterface.HeadOutputDevice != HeadOutputDevice.Headless)
					{
						w0.RunSynchronously(delegate
						{
							w0.LocalUser.IsPresentInWorld = false;
						});
					}
				}
				FocusedWorld = world2;
				FocusedWorld.Focus = World.WorldFocus.Focused;
				World w1 = FocusedWorld;
				if (Engine.InputInterface.HeadOutputDevice != HeadOutputDevice.Headless)
				{
					w1.RunSynchronously(delegate
					{
						w1.LocalUser.IsPresentInWorld = true;
					});
				}
				RunWorldFocused(world2);
			}
			lock (_setWorldOverlay)
			{
				foreach (World w2 in _setWorldOverlay)
				{
					if (!_overlayWorlds.Contains(w2))
					{
						w2.Focus = World.WorldFocus.Overlay;
						w2.RunSynchronously(delegate
						{
							w2.LocalUser.IsPresentInWorld = true;
						});
						_overlayWorlds.Add(w2);
					}
				}
				_setWorldOverlay.Clear();
			}
			lock (_setWorldPrivateOverlay)
			{
				foreach (World w3 in _setWorldPrivateOverlay)
				{
					if (!_privateOverlayWorlds.Contains(w3))
					{
						w3.Focus = World.WorldFocus.PrivateOverlay;
						w3.RunSynchronously(delegate
						{
							w3.LocalUser.IsPresentInWorld = true;
						});
						_privateOverlayWorlds.Add(w3);
					}
				}
				_setWorldPrivateOverlay.Clear();
			}
			lock (_removeWorldOverlay)
			{
				foreach (World item in _removeWorldOverlay)
				{
					if (_overlayWorlds.Contains(item))
					{
						item.Focus = World.WorldFocus.Background;
						_overlayWorlds.Remove(item);
					}
				}
				_removeWorldOverlay.Clear();
			}
			lock (worlds)
			{
				lock (_destroyWorlds)
				{
					foreach (World destroyWorld in _destroyWorlds)
					{
						if (!destroyWorld.IsDisposed)
						{
							try
							{
								destroyWorld.Dispose();
							}
							catch (Exception exception2)
							{
								UniLog.Error("Exception Disposing World:\n" + DebugManager.PreprocessException(exception2));
							}
							if (FocusedWorld == destroyWorld)
							{
								FocusedWorld = null;
							}
							RunWorldRemoved(destroyWorld);
						}
					}
					_destroyWorlds.Clear();
				}
				worlds.RemoveAll((World world3) => world3.IsDestroyed);
				foreach (World world3 in worlds)
				{
					if (world3.State == World.WorldState.Running)
					{
						worldsToUpdate.Enqueue(world3);
					}
				}
			}
			TryRunWorldsChanged();
			stage = UpdateStage.WorldUpdating;
			break;
		}
		case UpdateStage.WorldUpdating:
			if (worldsToUpdate.Count > 0)
			{
				World world = worldsToUpdate.Peek();
				try
				{
					if (world.IsDestroyed || world.Refresh())
					{
						worldsToUpdate.Dequeue();
					}
					break;
				}
				catch (Exception exception)
				{
					if (world.IsUserspace())
					{
						foreach (World world4 in worlds)
						{
							if (world4 != world && world4.IsAuthority)
							{
								string text = world4.EmergencyDump();
								UniLog.Log("World " + world4.Name + " dumped to: " + text);
							}
						}
					}
					else if (world.IsAuthority)
					{
						string text2 = world.EmergencyDump();
						UniLog.Log("World " + world.Name + " dumped to: " + text2);
					}
					UniLog.Error($"Unhandled Exception when updating world: {world.RawName}. State {world.State}, Refresh Stage: {world.Stage}, Init State: {world.InitState}, SyncTick {world.SyncTick}, StateVersion: {world.StateVersion}\n{DebugManager.PreprocessException(exception)}", stackTrace: false);
					RunWorldFailed(world);
					worldsToUpdate.Dequeue();
					world.Destroy();
					if (world.IsUserspace())
					{
						UniLog.Error("Userspace crashed! Shutting down engine", stackTrace: false);
						Engine.RequestShutdown();
					}
					if (Engine.InputInterface.HeadOutputDevice != HeadOutputDevice.Headless)
					{
						try
						{
							WorldLoadProgress.ShowMessage(world.Name, "<color=#f00>World Crashed</color>", "<color=#f00>Unhandled exception in updating the world</color>");
							break;
						}
						catch (Exception ex)
						{
							UniLog.Error("Exception creating error indication:\n" + ex);
							break;
						}
					}
					break;
				}
			}
			stage = UpdateStage.Finished;
			break;
		}
	}

	public static float4x4 TransferMatrix(float4x4 matrix, World from, World to)
	{
		Slot slot = from.LocalUser?.Root?.Slot;
		if (slot != null)
		{
			matrix = slot.GlobalToLocal * matrix;
		}
		Slot slot2 = to.LocalUser?.Root?.Slot;
		if (slot2 != null)
		{
			matrix = slot2.LocalToGlobal * matrix;
		}
		return matrix;
	}

	public static float3 TransferPoint(float3 point, World from, World to)
	{
		Slot slot = from.LocalUser?.Root?.Slot;
		if (slot != null)
		{
			point = slot.GlobalPointToLocal(in point);
		}
		Slot slot2 = to.LocalUser?.Root?.Slot;
		if (slot2 != null)
		{
			point = slot2.LocalPointToGlobal(in point);
		}
		return point;
	}

	public static float3 TransferDirection(float3 dir, World from, World to)
	{
		Slot slot = from.LocalUser?.Root?.Slot;
		if (slot != null)
		{
			dir = slot.GlobalDirectionToLocal(in dir);
		}
		Slot slot2 = to.LocalUser?.Root?.Slot;
		if (slot2 != null)
		{
			dir = slot2.LocalDirectionToGlobal(in dir);
		}
		return dir;
	}

	public static floatQ TransferRotation(floatQ rotation, World from, World to)
	{
		Slot slot = from.LocalUser?.Root?.Slot;
		if (slot != null)
		{
			rotation = slot.GlobalRotationToLocal(in rotation);
		}
		Slot slot2 = to.LocalUser?.Root?.Slot;
		if (slot2 != null)
		{
			rotation = slot2.LocalRotationToGlobal(in rotation);
		}
		return rotation;
	}

	public static float3 TransferScale(float3 scale, World from, World to)
	{
		Slot slot = from.LocalUser?.Root?.Slot;
		if (slot != null)
		{
			scale = slot.GlobalScaleToLocal(in scale);
		}
		Slot slot2 = to.LocalUser?.Root?.Slot;
		if (slot2 != null)
		{
			scale = slot2.LocalScaleToGlobal(in scale);
		}
		return scale;
	}

	public static float TransferScale(float scale, World from, World to)
	{
		return MathX.AvgComponent(TransferScale(new float3(scale, scale, scale), from, to));
	}

	public static Job<Slot> TransferToWorld(Slot root, World targetWorld, bool deleteOriginal = true)
	{
		Job<Slot> task = new Job<Slot>();
		if (root.World == targetWorld)
		{
			task.SetResultAndFinish(root);
			return task;
		}
		root.World.RunSynchronously(delegate
		{
			float3 pos = TransferPoint(root.GlobalPosition, root.World, targetWorld);
			floatQ rot = TransferRotation(root.GlobalRotation, root.World, targetWorld);
			float3 scl = TransferScale(root.GlobalScale, root.World, targetWorld);
			SavedGraph graph = root.SaveObject(DependencyHandling.CollectAssets, saveNonPersistent: true);
			if (deleteOriginal)
			{
				root.Destroy();
			}
			targetWorld.RunSynchronously(delegate
			{
				Slot slot = targetWorld.AddSlot();
				slot.LoadObject(graph.Root, null);
				slot.GlobalPosition = pos;
				slot.GlobalRotation = rot;
				slot.GlobalScale = scl;
				task.SetResultAndFinish(slot);
			});
		});
		return task;
	}
}
