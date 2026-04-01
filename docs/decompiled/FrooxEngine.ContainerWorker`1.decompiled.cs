using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Elements.Core;

namespace FrooxEngine;

public abstract class ContainerWorker<C> : Worker where C : ComponentBase<C>
{
	public struct ComponentEnumerator : IEnumerator<C>, IEnumerator, IDisposable
	{
		private Dictionary<RefID, C>.Enumerator bagEnumerator;

		public C Current => bagEnumerator.Current.Value;

		object IEnumerator.Current => Current;

		public ComponentEnumerator(ContainerWorker<C> container)
		{
			bagEnumerator = container.componentBag.GetEnumerator();
		}

		public void Dispose()
		{
			bagEnumerator.Dispose();
		}

		public bool MoveNext()
		{
			return bagEnumerator.MoveNext();
		}

		public void Reset()
		{
			((IEnumerator)bagEnumerator).Reset();
		}
	}

	public readonly struct ComponentEnumerable : IEnumerable<C>, IEnumerable
	{
		private readonly ContainerWorker<C> container;

		public ComponentEnumerable(ContainerWorker<C> container)
		{
			this.container = container;
		}

		public ComponentEnumerator GetEnumerator()
		{
			return new ComponentEnumerator(container);
		}

		IEnumerator<C> IEnumerable<C>.GetEnumerator()
		{
			return GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}

	protected SlimList<IInitializable> childInitializables;

	[NameOverride("Components")]
	[HideInInspector]
	protected readonly WorkerBag<C> componentBag;

	public UpdateManager UpdateManager => base.World?.UpdateManager;

	public bool IsInInitPhase { get; protected set; }

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

	public ComponentEnumerable Components { get; private set; }

	public int ComponentCount => componentBag.Count;

	public event ComponentEvent<C> ComponentAdded;

	public event ComponentEvent<C> ComponentRemoved;

	public ContainerWorker()
	{
		Components = new ComponentEnumerable(this);
	}

	internal virtual void Initialize(IWorldElement element)
	{
		InitializeWorker(element);
		componentBag.OnElementAdded += OnComponentAdded;
		componentBag.OnElementRemoved += OnComponentRemoved;
	}

	internal virtual void PrepareDestruction()
	{
		if (IsDestroyed)
		{
			return;
		}
		IsDestroyed = true;
		foreach (KeyValuePair<RefID, C> item in componentBag)
		{
			item.Value.PrepareDestruction();
		}
		PrepareMembersForDestroy();
	}

	public T AttachComponent<T>(bool runOnAttachBehavior = true, Action<T> beforeAttach = null) where T : C, new()
	{
		CheckAttachComponent(typeof(T));
		T val = TypeManager.Instantiate<T>();
		Action<C> beforeAttach2 = null;
		if (beforeAttach != null)
		{
			beforeAttach2 = delegate(C c)
			{
				beforeAttach((T)(ComponentBase<C>)c);
			};
		}
		AttachComponentInternal((C)(ComponentBase<C>)val, runOnAttachBehavior, beforeAttach2);
		return val;
	}

	public C AttachComponent(Type type, bool runOnAttachBehavior = true, Action<C> beforeAttach = null)
	{
		CheckAttachComponent(type);
		C val = (C)TypeManager.Instantiate(type);
		AttachComponentInternal(val, runOnAttachBehavior, beforeAttach);
		return val;
	}

	public T CopyComponent<T>(T source) where T : C, new()
	{
		T val = this.AttachComponent<T>(runOnAttachBehavior: false, (Action<T>)null);
		val.CopyValues((C)(ComponentBase<C>)source);
		return val;
	}

	public C CopyComponent(C source)
	{
		C val = AttachComponent(source.GetType(), runOnAttachBehavior: false);
		val.CopyValues(source);
		return val;
	}

	public C MoveComponent(C original)
	{
		C val = CopyComponent(original);
		MoveComponentFromCopy(original, val);
		return val;
	}

	public T MoveComponent<T>(T original) where T : C, new()
	{
		T val = this.CopyComponent<T>(original);
		MoveComponentFromCopy((C)(ComponentBase<C>)original, (C)(ComponentBase<C>)val);
		return val;
	}

	private void MoveComponentFromCopy(C original, C copy)
	{
		Dictionary<IWorldElement, IWorldElement> dictionary = new Dictionary<IWorldElement, IWorldElement>();
		dictionary.Add(original, copy);
		for (int i = 0; i < original.SyncMemberCount; i++)
		{
			RecursivelyMapElements(original.GetSyncMember(i), copy.GetSyncMember(i), dictionary);
		}
		base.World.ReplaceReferenceTargets(dictionary, nullIfIncompatible: true);
		original.Destroy();
	}

	public void RecursivelyMapElements(ISyncMember source, ISyncMember target, Dictionary<IWorldElement, IWorldElement> map)
	{
		map.Add(source, target);
		if (!(source is ISyncList syncList))
		{
			if (!(source is ISyncDictionary syncDictionary))
			{
				if (!(source is SyncObject syncObject))
				{
					if (!(source is SyncVar syncVar))
					{
						if (!(source is ISyncBag syncBag))
						{
							return;
						}
						ISyncBag syncBag2 = (ISyncBag)target;
						if (syncBag.Count != syncBag2.Count)
						{
							return;
						}
						{
							foreach (var item in syncBag.Values.OfType<ISyncMember>().Zip(syncBag2.Values.OfType<ISyncMember>(), (ISyncMember a, ISyncMember b) => (a: a, b: b)))
							{
								RecursivelyMapElements(item.a, item.b, map);
							}
							return;
						}
					}
					SyncVar syncVar2 = (SyncVar)target;
					if (!(syncVar.ElementType != syncVar2.ElementType))
					{
						RecursivelyMapElements(syncVar.Element, syncVar2.Element, map);
					}
					return;
				}
				SyncObject syncObject2 = (SyncObject)target;
				if (syncObject.SyncMemberCount == syncObject2.SyncMemberCount)
				{
					for (int num = 0; num < syncObject.SyncMemberCount; num++)
					{
						RecursivelyMapElements(syncObject.GetSyncMember(num), syncObject2.GetSyncMember(num), map);
					}
				}
				return;
			}
			ISyncDictionary syncDictionary2 = (ISyncDictionary)target;
			{
				foreach (KeyValuePair<object, ISyncMember> boxedEntry in syncDictionary.BoxedEntries)
				{
					ISyncMember syncMember = syncDictionary2.TryGetMember(boxedEntry.Key);
					if (syncMember != null)
					{
						RecursivelyMapElements(boxedEntry.Value, syncMember, map);
					}
				}
				return;
			}
		}
		ISyncList syncList2 = (ISyncList)target;
		if (syncList.Count == syncList2.Count)
		{
			for (int num2 = 0; num2 < syncList.Count; num2++)
			{
				RecursivelyMapElements(syncList.GetElement(num2), syncList2.GetElement(num2), map);
			}
		}
	}

	public bool RemoveComponent(C component)
	{
		return RemoveComponentInternal(component.ReferenceID);
	}

	public bool RemoveComponent(RefID id)
	{
		return RemoveComponentInternal(id);
	}

	public int RemoveAllComponents(Predicate<C> match)
	{
		return componentBag.RemoveAll(match);
	}

	public T GetComponentOrAttach<T>(Predicate<T> filter = null) where T : C, new()
	{
		bool attached;
		return this.GetComponentOrAttach<T>(out attached, filter);
	}

	public T GetComponentOrAttach<T>(out bool attached, Predicate<T> filter = null) where T : C, new()
	{
		T val = GetComponent(filter);
		if (val == null)
		{
			val = this.AttachComponent<T>(runOnAttachBehavior: true, (Action<T>)null);
			attached = true;
		}
		else
		{
			attached = false;
		}
		return val;
	}

	public T EnsureSingleComponent<T>(Predicate<T> filter = null) where T : C, new()
	{
		bool attached;
		int removed;
		return this.EnsureSingleComponent<T>(out attached, out removed, filter);
	}

	public T EnsureSingleComponent<T>(out bool attached, out int removed, Predicate<T> filter = null) where T : C, new()
	{
		List<T> list = Pool.BorrowList<T>();
		GetComponents(list, filter);
		removed = 0;
		while (list.Count > 1)
		{
			removed++;
			list.TakeLast().Destroy();
		}
		T result;
		if (list.Count == 1)
		{
			result = list[0];
			attached = false;
		}
		else
		{
			result = this.AttachComponent<T>(runOnAttachBehavior: true, (Action<T>)null);
			attached = true;
		}
		Pool.Return(ref list);
		return result;
	}

	public IEnumerable<C> EnumerateComponents(Predicate<C> predicate)
	{
		if (IsRemoved)
		{
			return EmptyEnumerator<C>.Instance;
		}
		return componentBag.Where(predicate);
	}

	public C GetComponent(Predicate<C> predicate)
	{
		if (IsRemoved)
		{
			return null;
		}
		foreach (KeyValuePair<RefID, C> item in componentBag)
		{
			if (predicate(item.Value))
			{
				return item.Value;
			}
		}
		return null;
	}

	public C GetComponent(Type type, bool exactTypeOnly = false)
	{
		if (IsRemoved)
		{
			return null;
		}
		foreach (KeyValuePair<RefID, C> item in componentBag)
		{
			if (exactTypeOnly)
			{
				if (item.Value.GetType() == type)
				{
					return item.Value;
				}
			}
			else if (type.IsAssignableFrom(item.Value.GetType()))
			{
				return item.Value;
			}
		}
		return null;
	}

	public IEnumerable<C> GetComponents(string typeName)
	{
		return EnumerateComponents((C c) => c.WorkerTypeName == typeName);
	}

	public C GetComponent(string name)
	{
		return GetComponents(name).FirstOrDefault();
	}

	public IEnumerable<C> EnumerateComponents(Type type)
	{
		return EnumerateComponents((C c) => type.IsAssignableFrom(c.GetType()));
	}

	public List<T> GetComponents<T>(Predicate<T> filter = null, bool exludeDisabled = false) where T : class
	{
		List<T> list = new List<T>();
		GetComponents(list, filter, exludeDisabled);
		return list;
	}

	public T GetComponent<T>(Predicate<T> filter = null, bool excludeDisabled = false) where T : class
	{
		if (IsRemoved)
		{
			return null;
		}
		foreach (KeyValuePair<RefID, C> item in componentBag)
		{
			if (item.Value is T val && (!excludeDisabled || item.Value.Enabled) && (filter == null || filter(val)))
			{
				return val;
			}
		}
		return null;
	}

	public int GetComponents<T>(List<T> results, Predicate<T> filter = null, bool excludeDisabled = false) where T : class
	{
		if (IsRemoved)
		{
			return 0;
		}
		int count = results.Count;
		foreach (KeyValuePair<RefID, C> item in componentBag)
		{
			if (item.Value is T val && (filter == null || filter(val)) && (item.Value.Enabled || !excludeDisabled))
			{
				results.Add(val);
			}
		}
		return results.Count - count;
	}

	public bool ForeachComponent<T>(Action<T> callback, bool cacheItems = false, bool exludeDisabled = false) where T : class
	{
		return ForeachComponent(callback, null, cacheItems, exludeDisabled);
	}

	public bool ForeachComponent<T>(Func<T, bool> callback, bool cacheItems = false, bool exludeDisabled = false) where T : class
	{
		return ForeachComponent(null, callback, cacheItems, exludeDisabled);
	}

	protected bool ForeachComponent<T>(Action<T> callback, Func<T, bool> callbackStopper, bool cacheItems, bool excludeDisabled) where T : class
	{
		if (IsRemoved)
		{
			return false;
		}
		if (cacheItems)
		{
			List<T> list = Pool.BorrowList<T>();
			GetComponents(list, null, excludeDisabled);
			bool flag = false;
			foreach (T item in list)
			{
				if (callback != null)
				{
					callback(item);
				}
				else if (!callbackStopper(item))
				{
					flag = true;
					break;
				}
			}
			Pool.Return(ref list);
			return !flag;
		}
		foreach (KeyValuePair<RefID, C> item2 in componentBag)
		{
			if ((!excludeDisabled || item2.Value.Enabled) && item2.Value is T val)
			{
				if (callback != null)
				{
					callback(val);
				}
				else if (!callbackStopper(val))
				{
					return false;
				}
			}
		}
		return true;
	}

	protected virtual void CheckAttachComponent(Type componentType)
	{
	}

	private void AttachComponentInternal(C component, bool runOnAttachBehavior, Action<C> beforeAttach)
	{
		if (base.IsLocalElement)
		{
			base.World.ReferenceController.LocalAllocationBlockBegin();
		}
		RefID key = base.World.ReferenceController.PeekID();
		componentBag.Add(key, component, isNew: true);
		if (base.IsLocalElement)
		{
			base.World.ReferenceController.LocalAllocationBlockEnd();
		}
		beforeAttach?.Invoke(component);
		if (runOnAttachBehavior)
		{
			component.RunOnAttach();
		}
	}

	private bool RemoveComponentInternal(RefID id)
	{
		return componentBag.Remove(id);
	}

	private void OnComponentAdded(SyncBagBase<RefID, C> bag, RefID idStart, C component, bool isNew)
	{
		base.World.ReferenceController.AllocationBlockBegin(in idStart);
		bool isInInitPhase = IsInInitPhase;
		IsInInitPhase = true;
		component.Initialize(this, isNew);
		childInitializables.Add(component);
		if (!isInInitPhase)
		{
			EndInitPhase();
		}
		base.World.ReferenceController.AllocationBlockEnd();
		RunComponentAdded(component);
		if (IsDestroyed)
		{
			component.PrepareDestruction();
		}
	}

	private void OnComponentRemoved(SyncBagBase<RefID, C> bag, RefID key, C component)
	{
		component.PrepareDestruction();
		RunComponentRemoved(component);
	}

	protected virtual void RunComponentAdded(C component)
	{
		this.ComponentAdded?.Invoke(component);
	}

	protected virtual void RunComponentRemoved(C component)
	{
		this.ComponentRemoved?.Invoke(component);
	}

	public virtual void EndInitPhase()
	{
		foreach (IInitializable childInitializable in childInitializables)
		{
			childInitializable.EndInitPhase();
		}
		childInitializables.Clear();
		IsInInitPhase = false;
	}

	protected override void InitializeSyncMembers()
	{
		componentBag = new WorkerBag<C>();
	}
}
