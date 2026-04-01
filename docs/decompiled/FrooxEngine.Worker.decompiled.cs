using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Elements.Core;

namespace FrooxEngine;

public abstract class Worker : IWorker, IWorldElement
{
	protected class InternalReferences : IDisposable
	{
		private struct SyncMemberPair
		{
			public ISyncRef origRef;

			public IWorldElement origTarget;

			public ISyncRef copyRef;

			public IWorldElement copyTarget;
		}

		private DictionaryList<IWorldElement, int> pairsByTarget;

		private Dictionary<ISyncRef, int> pairsByRef;

		private List<SyncMemberPair> pairs;

		public InternalReferences()
		{
			pairsByTarget = Pool.BorrowDictionaryList<IWorldElement, int>();
			pairsByRef = Pool.BorrowDictionary<ISyncRef, int>();
			pairs = Pool.BorrowList<SyncMemberPair>();
		}

		public void AddPair(ISyncRef reference, IWorldElement target)
		{
			SyncMemberPair item = new SyncMemberPair
			{
				origRef = reference,
				origTarget = target
			};
			pairs.Add(item);
			int num = pairs.Count - 1;
			pairsByTarget.Add(target, num);
			pairsByRef.Add(reference, num);
		}

		public void RegisterCopy(IWorldElement source, IWorldElement copy)
		{
			List<int> list = pairsByTarget.TryGetList(source);
			if (list == null)
			{
				return;
			}
			foreach (int item in list)
			{
				SyncMemberPair value = pairs[item];
				value.copyTarget = copy;
				pairs[item] = value;
			}
		}

		public bool TryRegisterCopyReference(ISyncRef fromRef, ISyncRef toRef)
		{
			if (toRef == null)
			{
				throw new ArgumentNullException("toRef");
			}
			if (pairsByRef.TryGetValue(fromRef, out var value))
			{
				SyncMemberPair value2 = pairs[value];
				value2.copyRef = toRef;
				pairs[value] = value2;
				return true;
			}
			return false;
		}

		public void TransferReferences(bool preserveMissingTargets)
		{
			foreach (SyncMemberPair pair in pairs)
			{
				if (pair.copyRef != null)
				{
					if (pair.copyTarget != null)
					{
						pair.copyRef.Target = pair.copyTarget;
					}
					else if (preserveMissingTargets)
					{
						pair.copyRef.Target = pair.origTarget;
					}
				}
			}
		}

		public void Dispose()
		{
			Pool.Return(ref pairsByTarget);
			Pool.Return(ref pairsByRef);
			Pool.Return(ref pairs);
		}
	}

	private SpinLock coroutineLock = new SpinLock(enableThreadOwnerTracking: false);

	private HashSet<Coroutine> coroutines;

	protected readonly WorkerInitInfo InitInfo;

	private Action<Coroutine> _coroutineFinishedDelegate;

	public World World { get; private set; }

	public Engine Engine => World?.Engine;

	public PhysicsManager Physics => World?.Physics;

	public EngineSkyFrostInterface Cloud => Engine?.Cloud;

	public TimeController Time => World?.Time;

	public AudioManager Audio => World?.Audio;

	public AudioSystem AudioSystem => Engine?.AudioSystem;

	public InputInterface InputInterface => World?.InputInterface;

	public InputBindingManager Input => World?.Input;

	public DebugManager Debug => World?.Debug;

	public PermissionController Permissions => World?.Permissions;

	public User LocalUser => World?.LocalUser;

	public UserRoot LocalUserRoot => LocalUser?.Root;

	public Slot LocalUserSpace => LocalUserRoot?.Slot.Parent ?? World.RootSlot;

	public virtual Type GizmoType => GizmoHelper.GetGizmoType(GetType());

	public virtual int Version => 0;

	public Type WorkerType => GetType();

	public string WorkerTypeName => WorkerType.FullName;

	public virtual string Name => WorkerType.Name;

	public string WorkerCategoryPath => InitInfo.CategoryPath;

	public RefID ReferenceID { get; private set; }

	public bool IsLocalElement { get; private set; }

	public bool IsDisposed { get; private set; }

	public bool IsScheduledForValidation { get; internal set; }

	public virtual bool IsRemoved => IsDisposed;

	public int SyncMemberCount => InitInfo.syncMemberFields.Length;

	public int SyncMethodCount
	{
		get
		{
			SyncMethodInfo[] syncMethods = InitInfo.syncMethods;
			if (syncMethods == null)
			{
				return 0;
			}
			return syncMethods.Length;
		}
	}

	public bool DontDuplicate => InitInfo.DontDuplicate;

	public IEnumerable<ISyncMember> SyncMembers
	{
		get
		{
			for (int i = 0; i < SyncMemberCount; i++)
			{
				yield return GetSyncMember(i);
			}
		}
	}

	public bool PreserveWithAssets => InitInfo.PreserveWithAssets;

	public bool GloballyRegistered => InitInfo.RegisterGlobally;

	public IWorldElement Parent { get; private set; }

	public abstract bool IsPersistent { get; }

	public event Action<Worker> Disposing;

	public FieldInfo GetSyncMemberFieldInfo(int index)
	{
		return InitInfo.syncMemberFields[index];
	}

	public string GetSyncMemberName(int index)
	{
		return InitInfo.syncMemberNames[index];
	}

	public string GetSyncMemberName(ISyncMember member)
	{
		int num = IndexOfMember(member);
		if (num < 0)
		{
			return null;
		}
		return InitInfo.syncMemberNames[num];
	}

	public int IndexOfMember(ISyncMember member)
	{
		for (int i = 0; i < SyncMemberCount; i++)
		{
			if (GetSyncMember(i) == member)
			{
				return i;
			}
		}
		return -1;
	}

	public virtual ISyncMember GetSyncMember(int index)
	{
		return InitInfo.syncMemberFields[index].GetValue(this) as ISyncMember;
	}

	public virtual void GetSyncMethodData(int index, out SyncMethodInfo info, out Delegate method)
	{
		info = InitInfo.syncMethods[index];
		if (info.methodType == typeof(Delegate))
		{
			method = null;
		}
		else if (info.methodType.IsGenericTypeDefinition)
		{
			Type[] array = new Type[info.genericMapping.Count];
			Type[] genericArguments = GetType().GetGenericArguments();
			Type[] genericArguments2 = GetType().GetGenericTypeDefinition().GetGenericArguments();
			IReadOnlyList<string> mappings = info.genericMapping;
			int i;
			for (i = 0; i < array.Length; i++)
			{
				if (genericArguments2.FindIndex((Type t) => t.Name == mappings[i]) < 0)
				{
					method = null;
					return;
				}
				array[i] = genericArguments[i];
			}
			method = info.method.CreateDelegate(info.methodType.MakeGenericType(array), this);
		}
		else
		{
			method = info.method.CreateDelegate(info.methodType, this);
		}
	}

	public virtual int IndexOfSyncMethod(Predicate<SyncMethodInfo> filter)
	{
		return InitInfo.syncMethods.FindIndex(filter);
	}

	public virtual int IndexOfStaticSyncMethod(Predicate<SyncMethodInfo> filter)
	{
		return InitInfo.staticSyncMethods.FindIndex(filter);
	}

	public virtual void GetStaticSyncMethodData(int index, out SyncMethodInfo info, out Delegate method)
	{
		info = InitInfo.staticSyncMethods[index];
		if (info.methodType == typeof(Delegate))
		{
			method = null;
		}
		else if (info.methodType.IsGenericTypeDefinition)
		{
			Type[] array = new Type[info.genericMapping.Count];
			Type[] genericArguments = GetType().GetGenericArguments();
			Type[] genericArguments2 = GetType().GetGenericTypeDefinition().GetGenericArguments();
			IReadOnlyList<string> mappings = info.genericMapping;
			int i;
			for (i = 0; i < array.Length; i++)
			{
				if (genericArguments2.FindIndex((Type t) => t.Name == mappings[i]) < 0)
				{
					method = null;
					return;
				}
				array[i] = genericArguments[i];
			}
			method = info.method.CreateDelegate(info.methodType.MakeGenericType(array));
		}
		else
		{
			method = info.method.CreateDelegate(info.methodType);
		}
	}

	public virtual Delegate GetSyncMethod(int index)
	{
		SyncMethodInfo syncMethodInfo = InitInfo.syncMethods[index];
		return syncMethodInfo.method.CreateDelegate(syncMethodInfo.methodType, this);
	}

	public virtual Delegate GetSyncMethod(string name)
	{
		int num = InitInfo.syncMethods.FindIndex((SyncMethodInfo info) => info.method.Name == name);
		if (num < 0)
		{
			return null;
		}
		return GetSyncMethod(num);
	}

	public virtual Type GetSyncMethodType(int index)
	{
		return InitInfo.syncMethods[index].methodType;
	}

	private int IndexOfMember(string name)
	{
		if (InitInfo.syncMemberNameToIndex.TryGetValue(name, out var value))
		{
			return value;
		}
		return -1;
	}

	public ISyncMember GetSyncMember(string name)
	{
		int num = IndexOfMember(name);
		if (num < 0)
		{
			return null;
		}
		return GetSyncMember(num);
	}

	public FieldInfo GetSyncMemberFieldInfo(string name)
	{
		return GetSyncMemberFieldInfo(IndexOfMember(name));
	}

	public Worker()
	{
		InitInfo = WorkerInitializer.GetInitInfo(this);
	}

	protected virtual void InitializeSyncMembers()
	{
		for (int i = 0; i < InitInfo.syncMemberFields.Length; i++)
		{
			FieldInfo obj = InitInfo.syncMemberFields[i];
			_ = InitInfo.syncMemberNames[i];
			ISyncMember syncMember = Activator.CreateInstance(obj.FieldType) as ISyncMember;
			obj.SetValue(this, syncMember);
			if (InitInfo.syncMemberNonpersitent[i])
			{
				((SyncElement)syncMember).MarkNonPersistent();
			}
			if (InitInfo.syncMemberNondrivable[i])
			{
				((SyncElement)syncMember).MarkNonDrivable();
			}
		}
	}

	protected virtual void InitializeSyncMemberDefaults()
	{
		for (int i = 0; i < InitInfo.syncMemberFields.Length; i++)
		{
			if (InitInfo.defaultValues[i] != null)
			{
				((IField)GetSyncMember(i)).BoxedValue = InitInfo.defaultValues[i];
			}
		}
	}

	protected void InitializeWorker(IWorldElement parent)
	{
		try
		{
			World = parent.World;
			Parent = parent;
			InitializeSyncMembers();
			ReferenceID = World.ReferenceController.AllocateID();
			IsLocalElement = ReferenceID.IsLocalID;
			if (SyncMemberCount > 0)
			{
				ushort[] randomizationTable = World.GetRandomizationTable(SyncMemberCount);
				if (randomizationTable == null)
				{
					for (int i = 0; i < SyncMemberCount; i++)
					{
						GetSyncMember(i).Initialize(World, this);
					}
				}
				else
				{
					for (int j = 0; j < SyncMemberCount; j++)
					{
						GetSyncMember(randomizationTable[j]).Initialize(World, this);
					}
				}
			}
			InitializeSyncMemberDefaults();
			World.ReferenceController.RegisterReference(this);
			if (GloballyRegistered)
			{
				World.RegisterGlobalWorker(this);
			}
			PostInitializeWorker();
		}
		catch (Exception innerException)
		{
			throw new Exception($"Exception during initializing Worker of type {GetType()}", innerException);
		}
	}

	protected virtual void PostInitializeWorker()
	{
	}

	protected virtual void SyncMemberChanged(IChangeable member)
	{
	}

	protected void EndInitializationStageForMembers()
	{
		for (int i = 0; i < SyncMemberCount; i++)
		{
			GetSyncMember(i).EndInitPhase();
		}
	}

	void IWorldElement.ChildChanged(IWorldElement child)
	{
		SyncMemberChanged(child as IChangeable);
	}

	public void Dispose()
	{
		if (!IsDisposed)
		{
			try
			{
				OnDispose();
				this.Disposing?.Invoke(this);
				this.Disposing = null;
			}
			catch (Exception exception)
			{
				UniLog.Error("Exception running OnDispose():\n" + DebugManager.PreprocessException(exception));
			}
			StopAllCoroutines();
			for (int i = 0; i < SyncMemberCount; i++)
			{
				GetSyncMember(i).Dispose();
			}
			World.ReferenceController.UnregisterReference(this);
			World = null;
			Parent = null;
			IsDisposed = true;
		}
	}

	protected void PrepareMembersForDestroy()
	{
		StopAllCoroutines();
		if (GloballyRegistered)
		{
			World.UnregisterGlobalWorker(this);
		}
		for (int i = 0; i < SyncMemberCount; i++)
		{
			ISyncMember syncMember = GetSyncMember(i);
			if (syncMember is SyncElement syncElement)
			{
				syncElement.PrepareDestroy();
			}
			else if (syncMember is Worker worker)
			{
				worker.PrepareMembersForDestroy();
			}
		}
	}

	public void CopyValues(Worker source, Action<ISyncMember, ISyncMember> copy, bool allowTypeMismatch = false)
	{
		if (!allowTypeMismatch && source?.WorkerType != WorkerType)
		{
			throw new Exception("The source type doesn't match!");
		}
		for (int i = 0; i < SyncMemberCount; i++)
		{
			if (!InitInfo.syncMemberDontCopy[i])
			{
				copy(source.GetSyncMember(i), GetSyncMember(i));
			}
		}
	}

	/// <summary>
	/// Copies values from a Worker of the same type
	/// </summary>
	/// <param name="source"></param>
	public void CopyValues(Worker source)
	{
		CopyValues(source, delegate(ISyncMember from, ISyncMember to)
		{
			to.CopyValues(from);
		});
	}

	/// <summary>
	/// Copies values from a Worker of a different type, matching them by name.
	/// </summary>
	/// <param name="source"></param>
	/// <param name="includePrivate"></param>
	public void CopyProperties(Worker source, bool includePrivate = false, Predicate<ISyncMember> filter = null)
	{
		for (int i = 0; i < source.SyncMemberCount; i++)
		{
			ISyncMember syncMember = source.GetSyncMember(i);
			FieldInfo syncMemberFieldInfo = source.GetSyncMemberFieldInfo(i);
			if ((includePrivate || syncMemberFieldInfo.IsPublic) && (filter == null || filter(syncMember)))
			{
				ISyncMember syncMember2 = GetSyncMember(source.GetSyncMemberName(i));
				if (syncMember2 != null && syncMember2.GetType() == syncMember.GetType())
				{
					syncMember2.CopyValues(syncMember);
				}
			}
		}
	}

	protected static void MemberCopy(ISyncMember from, ISyncMember to, InternalReferences internalRefs, HashSet<ISyncRef> breakRefs, bool checkTypes)
	{
		internalRefs.RegisterCopy(from, to);
		if (from is ISyncRef syncRef && !(from is SyncObject))
		{
			ISyncDelegate syncDelegate = from as ISyncDelegate;
			if ((syncRef.Value == RefID.Null && (syncDelegate == null || !syncDelegate.IsStaticReference)) || breakRefs.Contains(syncRef))
			{
				return;
			}
			if (internalRefs.TryRegisterCopyReference((ISyncRef)from, (ISyncRef)to))
			{
				if (to is ISyncDelegate syncDelegate2)
				{
					syncDelegate2.MethodName = ((ISyncDelegate)from).MethodName;
				}
			}
			else
			{
				to.CopyValues(from);
			}
		}
		else if (!checkTypes || !(from.GetType() != to.GetType()))
		{
			to.CopyValues(from, delegate(ISyncMember _from, ISyncMember _to)
			{
				MemberCopy(_from, _to, internalRefs, breakRefs, checkTypes);
			});
		}
	}

	public Task StartTask(Func<Task> task)
	{
		return World?.Coroutines.StartTask(task, this as IUpdatable) ?? NullTask();
	}

	public Task<T> StartTask<T>(Func<Task<T>> task)
	{
		return World?.Coroutines.StartTask(task, this as IUpdatable) ?? NullTask<T>();
	}

	public Task StartTask<T>(Func<T, Task> task, T argument)
	{
		return World?.Coroutines.StartTask(task, argument, this as IUpdatable) ?? NullTask();
	}

	public Task StartGlobalTask(Func<Task> task)
	{
		return World?.Coroutines.StartTask(task) ?? NullTask();
	}

	public Task<T> StartGlobalTask<T>(Func<Task<T>> task)
	{
		return World?.Coroutines.StartTask(task) ?? NullTask<T>();
	}

	public Task DelaySeconds(float seconds)
	{
		return DelayTimeSpan(TimeSpan.FromSeconds(seconds));
	}

	public async Task DelayTimeSpan(TimeSpan timespan)
	{
		await Task.Delay(timespan).ConfigureAwait(continueOnCapturedContext: false);
		await default(ToWorld);
	}

	private static async Task NullTask()
	{
		await NullTask<bool>();
	}

	private static async Task<T> NullTask<T>()
	{
		return await new TaskCompletionSource<T>().Task;
	}

	public Coroutine StartCoroutine(IEnumerator<Context> coroutine)
	{
		if (_coroutineFinishedDelegate == null)
		{
			_coroutineFinishedDelegate = CoroutineFinished;
		}
		Coroutine coroutine2 = World.Coroutines.StartCoroutine(coroutine, _coroutineFinishedDelegate, this as IUpdatable);
		if (!coroutine2.IsDone)
		{
			bool lockTaken = false;
			try
			{
				coroutineLock.Enter(ref lockTaken);
				if (coroutines == null)
				{
					coroutines = new HashSet<Coroutine>();
				}
				coroutines.Add(coroutine2);
				if (coroutine2.IsDone)
				{
					coroutines.Remove(coroutine2);
				}
			}
			finally
			{
				if (lockTaken)
				{
					coroutineLock.Exit();
				}
			}
		}
		return coroutine2;
	}

	public void StopAllCoroutines()
	{
		if (coroutines == null)
		{
			return;
		}
		bool lockTaken = false;
		HashSet<Coroutine> hashSet = Pool.BorrowHashSet<Coroutine>();
		try
		{
			coroutineLock.Enter(ref lockTaken);
			foreach (Coroutine coroutine in coroutines)
			{
				hashSet.Add(coroutine);
			}
		}
		finally
		{
			if (lockTaken)
			{
				coroutineLock.Exit();
			}
		}
		foreach (Coroutine item in hashSet)
		{
			item.Stop();
		}
		Pool.Return(ref hashSet);
	}

	private void CoroutineFinished(Coroutine obj)
	{
		if (coroutines == null)
		{
			return;
		}
		bool lockTaken = false;
		try
		{
			coroutineLock.Enter(ref lockTaken);
			coroutines.Remove(obj);
		}
		finally
		{
			if (lockTaken)
			{
				coroutineLock.Exit();
			}
		}
	}

	public bool CheckPermission<T>(Predicate<T> check, User user = null) where T : class, IWorkerPermissions
	{
		return Permissions.Check(this, check, user);
	}

	public virtual void Load(DataTreeNode node, LoadControl control)
	{
		Load(node, control, (string m) => true);
	}

	public void Load(DataTreeNode node, LoadControl control, Predicate<string> memberFilter)
	{
		DataTreeDictionary dataTreeDictionary = (DataTreeDictionary)node;
		control.AssociateReference(ReferenceID, dataTreeDictionary["ID"]);
		OnBeforeLoad(node, control);
		for (int i = 0; i < SyncMemberCount; i++)
		{
			WorkerInitInfo initInfo = InitInfo;
			string text = initInfo.syncMemberNames[i];
			if (!memberFilter(text))
			{
				continue;
			}
			bool flag = false;
			DataTreeNode dataTreeNode = dataTreeDictionary.TryGetNode(text);
			if (dataTreeNode == null)
			{
				dataTreeNode = dataTreeDictionary.TryGetNode(text + "-ID");
				if (dataTreeNode != null)
				{
					flag = true;
				}
			}
			if (dataTreeNode == null && initInfo.oldSyncMemberNames != null && initInfo.oldSyncMemberNames.TryGetValue(text, out List<string> value))
			{
				foreach (string item in value)
				{
					dataTreeNode = dataTreeDictionary.TryGetNode(item);
					if (dataTreeNode != null)
					{
						break;
					}
					dataTreeNode = dataTreeDictionary.TryGetNode(item + "-ID");
					if (dataTreeNode != null)
					{
						flag = true;
						break;
					}
				}
			}
			if (dataTreeNode == null)
			{
				continue;
			}
			ISyncMember syncMember = GetSyncMember(i);
			if (flag)
			{
				control.AssociateReference(syncMember.ReferenceID, dataTreeNode);
				continue;
			}
			try
			{
				syncMember.Load(dataTreeNode, control);
			}
			catch (Exception value2)
			{
				UniLog.Error($"Exception loading member ({syncMember.Name}) {i}:\n{syncMember.ParentHierarchyToString()}\n{value2}");
			}
		}
		OnLoading(node, control);
	}

	public virtual DataTreeNode Save(SaveControl control)
	{
		if (!IsPersistent && !control.SaveNonPersistent)
		{
			throw new Exception("Cannot save non-persistent objects");
		}
		if (Version > 0)
		{
			control.RegisterTypeVersion(GetType(), Version);
		}
		DataTreeDictionary dataTreeDictionary = new DataTreeDictionary();
		dataTreeDictionary.Add("ID", control.SaveReference(ReferenceID));
		for (int i = 0; i < SyncMemberCount; i++)
		{
			ISyncMember syncMember = GetSyncMember(i);
			if (SaveMember(syncMember, control))
			{
				if ((syncMember.IsPersistent || control.SaveNonPersistent) && !InitInfo.syncMemberDontCopy[i])
				{
					DataTreeNode dataTreeNode = syncMember.Save(control);
					dataTreeDictionary.Add(InitInfo.syncMemberNames[i], dataTreeNode);
					MemberSaved(syncMember, dataTreeNode, control);
				}
				else
				{
					dataTreeDictionary.Add(InitInfo.syncMemberNames[i] + "-ID", control.SaveReference(syncMember.ReferenceID));
				}
			}
		}
		return dataTreeDictionary;
	}

	internal virtual void RunOnSaving(SaveControl control)
	{
		OnSaving(control);
	}

	protected virtual void OnSaving(SaveControl control)
	{
	}

	protected virtual void OnBeforeLoad(DataTreeNode node, LoadControl control)
	{
	}

	protected virtual void OnLoading(DataTreeNode node, LoadControl control)
	{
	}

	protected virtual void MemberSaved(ISyncMember member, DataTreeNode node, SaveControl control)
	{
	}

	protected virtual bool SaveMember(ISyncMember member, SaveControl control)
	{
		return true;
	}

	protected virtual void OnDispose()
	{
	}

	public IField TryGetField(string name)
	{
		if (InitInfo.syncMemberNameToIndex.TryGetValue(name, out var value))
		{
			return GetSyncMember(value) as IField;
		}
		return null;
	}

	public IField<T> TryGetField<T>(string name)
	{
		return TryGetField(name) as IField<T>;
	}

	public void GetReferencedObjects(List<IWorldElement> referencedObjects, bool assetRefOnly, bool persistentOnly = true, bool skipDontCopy = false)
	{
		WorkerInitInfo _initInfo = InitInfo;
		List<ISyncRef> list = Pool.BorrowList<ISyncRef>();
		GetSyncMembers(list, skipDontCopy ? ((Predicate<int>)((int i) => !_initInfo.syncMemberDontCopy[i])) : null);
		foreach (ISyncRef item in list)
		{
			if (item.Target == null)
			{
				continue;
			}
			bool flag = false;
			if (assetRefOnly)
			{
				if (item is IAssetRef)
				{
					flag = true;
				}
				else if (item.Target is Component { PreserveWithAssets: not false })
				{
					flag = true;
				}
				if (!flag)
				{
					continue;
				}
			}
			if ((!persistentOnly || item.Target.IsPersistent || flag) && item.Target != null)
			{
				referencedObjects.Add(item.Target);
			}
		}
		Pool.Return(ref list);
	}

	public int ForeachSyncMember<T>(Action<T> action) where T : class, IWorldElement
	{
		return ProcessSyncMembers(null, action);
	}

	public int GetSyncMembers<T>(List<T> list, bool skipDontCopy) where T : class, IWorldElement
	{
		return ProcessSyncMembers(list, null, skipDontCopy ? ((Predicate<int>)((int i) => !InitInfo.syncMemberDontCopy[i])) : null);
	}

	public int GetSyncMembers<T>(List<T> list, Predicate<int> rootMemberFilter = null) where T : class, IWorldElement
	{
		return ProcessSyncMembers(list, null, rootMemberFilter);
	}

	public List<T> GetSyncMembers<T>() where T : class, IWorldElement
	{
		List<T> list = new List<T>();
		GetSyncMembers(list);
		return list;
	}

	public int GetSyncMembers<T>(int syncMemberIndex, List<T> list) where T : class, IWorldElement
	{
		int count = 0;
		ProcessSyncMembers(list, null, GetSyncMember(syncMemberIndex), ref count);
		return count;
	}

	private int ProcessSyncMembers<T>(List<T> list, Action<T> action, Predicate<int> rootMemberFilter = null) where T : class, IWorldElement
	{
		int count = 0;
		for (int i = 0; i < SyncMemberCount; i++)
		{
			if (rootMemberFilter == null || rootMemberFilter(i))
			{
				ProcessSyncMembers(list, action, GetSyncMember(i), ref count);
			}
		}
		return count;
	}

	private void ProcessSyncMembers<T>(List<T> list, Action<T> action, ISyncMember member, ref int count) where T : class, IWorldElement
	{
		if (member is T val)
		{
			list?.Add(val);
			action?.Invoke(val);
			count++;
		}
		if (!(member is SyncObject syncObject))
		{
			if (!(member is SyncVar syncVar))
			{
				if (!(member is ISyncList syncList))
				{
					if (member is ISyncDictionary syncDictionary)
					{
						{
							foreach (ISyncMember value in syncDictionary.Values)
							{
								ProcessSyncMembers(list, action, value, ref count);
							}
							return;
						}
					}
					if (!(member is ISyncBag syncBag))
					{
						return;
					}
					{
						foreach (IWorldElement value2 in syncBag.Values)
						{
							if (value2 is ISyncMember member2)
							{
								ProcessSyncMembers(list, action, member2, ref count);
							}
						}
						return;
					}
				}
				for (int i = 0; i < syncList.Count; i++)
				{
					ProcessSyncMembers(list, action, syncList.GetElement(i), ref count);
				}
			}
			else
			{
				ProcessSyncMembers(list, action, syncVar.Element, ref count);
			}
		}
		else
		{
			count += syncObject.ProcessSyncMembers(list, action);
		}
	}

	public bool PublicMembersEqual(Worker other)
	{
		if (GetType() != other.GetType())
		{
			return false;
		}
		List<IWorldElement> list = Pool.BorrowList<IWorldElement>();
		List<IWorldElement> list2 = Pool.BorrowList<IWorldElement>();
		try
		{
			GetWorldElementsForComparison(this, list);
			GetWorldElementsForComparison(other, list2);
			if (list.Count != list2.Count)
			{
				return false;
			}
			for (int i = 0; i < list.Count; i++)
			{
				IWorldElement worldElement = list[i];
				IWorldElement worldElement2 = list2[i];
				if (worldElement.GetType() != worldElement2.GetType())
				{
					return false;
				}
				if (!(worldElement is ISyncArray syncArray))
				{
					if (!(worldElement is ISyncRef syncRef))
					{
						if (!(worldElement is IField field))
						{
							continue;
						}
						IField field2 = (IField)worldElement2;
						object boxedValue = field.BoxedValue;
						object boxedValue2 = field2.BoxedValue;
						if (boxedValue != null || boxedValue2 != null)
						{
							if (boxedValue == null || boxedValue2 == null)
							{
								return false;
							}
							if (!field.BoxedValue.Equals(field2.BoxedValue))
							{
								return false;
							}
						}
					}
					else
					{
						ISyncRef syncRef2 = (ISyncRef)worldElement2;
						IAsset obj = (syncRef.Target as IAssetProvider)?.GenericAsset;
						IAsset asset = (syncRef2.Target as IAssetProvider)?.GenericAsset;
						if (obj != asset && syncRef.Target != syncRef2.Target)
						{
							return false;
						}
					}
					continue;
				}
				ISyncArray syncArray2 = (ISyncArray)worldElement2;
				if (syncArray.Count != syncArray2.Count)
				{
					return false;
				}
				for (int j = 0; j < syncArray.Count; j++)
				{
					if (!syncArray.GetElement(j).Equals(syncArray2.GetElement(j)))
					{
						return false;
					}
				}
			}
		}
		finally
		{
			Pool.Return(ref list);
			Pool.Return(ref list2);
		}
		return true;
	}

	private static void GetWorldElementsForComparison(Worker worker, List<IWorldElement> elements)
	{
		for (int i = 0; i < worker.SyncMemberCount; i++)
		{
			if (worker.GetSyncMemberFieldInfo(i).IsPublic)
			{
				worker.GetSyncMembers(i, elements);
			}
		}
	}

	public override string ToString()
	{
		return this.ParentHierarchyToString();
	}

	public string MembersToString()
	{
		StringBuilder stringBuilder = Pool.BorrowStringBuilder();
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(15, 2, stringBuilder2);
		handler.AppendLiteral("Members on: ");
		handler.AppendFormatted(GetType());
		handler.AppendLiteral(" - ");
		handler.AppendFormatted(ReferenceID);
		stringBuilder3.AppendLine(ref handler);
		for (int i = 0; i < SyncMemberCount; i++)
		{
			ISyncMember syncMember = GetSyncMember(i);
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder4 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(8, 4, stringBuilder2);
			handler.AppendFormatted(syncMember.Name);
			handler.AppendLiteral(" - ");
			handler.AppendFormatted(syncMember.GetType());
			handler.AppendLiteral(" - ");
			handler.AppendFormatted(ReferenceID);
			handler.AppendLiteral(": ");
			handler.AppendFormatted(syncMember.ToString());
			stringBuilder4.AppendLine(ref handler);
		}
		string result = stringBuilder.ToString();
		Pool.Return(ref stringBuilder);
		return result;
	}
}
