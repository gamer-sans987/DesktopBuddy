using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Elements.Core;
using Elements.Data;
using FrooxEngine.Store;
using SkyFrost.Base;

namespace FrooxEngine;

[DataModelType]
public class RecordDirectory
{
	public enum LoadState
	{
		NotLoaded,
		LocalCache,
		FullyLoaded
	}

	private static ActionBlock<Action> _cacheTasks = new ActionBlock<Action>(delegate(Action a)
	{
		a();
	}, new ExecutionDataflowBlockOptions
	{
		MaxDegreeOfParallelism = 1,
		EnsureOrdered = false
	});

	private Task _loadTask;

	private Task<bool> _cacheLoadTask;

	private object _lock = new object();

	private List<RecordDirectory> subdirectories = new List<RecordDirectory>();

	private List<FrooxEngine.Store.Record> records = new List<FrooxEngine.Store.Record>();

	public Engine Engine { get; private set; }

	public SkyFrostInterface Cloud => Engine.Cloud;

	public string OwnerId { get; private set; }

	public string Path { get; private set; }

	public string Name { get; private set; }

	public string ChildRecordPath
	{
		get
		{
			if (DirectoryRecord == null)
			{
				return Path;
			}
			return DirectoryRecord.Path + "\\" + DirectoryRecord.Name;
		}
	}

	public bool IsLink => LinkRecord != null;

	public LoadState CurrentLoadState { get; private set; }

	public bool CanWrite
	{
		get
		{
			if (OwnerId == null)
			{
				return false;
			}
			if (OwnerId == Engine.Cloud.Session.CurrentUserID)
			{
				return true;
			}
			foreach (Membership currentUserMembership in Engine.Cloud.Groups.CurrentUserMemberships)
			{
				if (OwnerId == currentUserMembership.GroupId)
				{
					return true;
				}
			}
			return false;
		}
	}

	public FrooxEngine.Store.Record DirectoryRecord { get; private set; }

	public FrooxEngine.Store.Record LinkRecord { get; private set; }

	public RecordDirectory ParentDirectory { get; private set; }

	public FrooxEngine.Store.Record EntryRecord
	{
		get
		{
			if (!IsLink)
			{
				return DirectoryRecord;
			}
			return LinkRecord;
		}
	}

	public IReadOnlyList<RecordDirectory> Subdirectories => subdirectories;

	public IReadOnlyList<FrooxEngine.Store.Record> Records => records;

	public RecordDirectory(Engine engine, List<RecordDirectory> subdirs, List<FrooxEngine.Store.Record> items)
	{
		Engine = engine;
		OwnerId = "NONE";
		Path = "NONE";
		Name = "NONE";
		if (subdirs != null)
		{
			subdirectories.AddRange(subdirs);
			foreach (RecordDirectory subdir in subdirs)
			{
				subdir.ParentDirectory = this;
			}
		}
		if (items != null)
		{
			records.AddRange(items);
		}
		HashSet<string> alreadyAdded = Pool.BorrowHashSet<string>();
		subdirectories.RemoveAll((RecordDirectory r) => !alreadyAdded.Add(r.Name));
		records.RemoveAll((FrooxEngine.Store.Record r) => !alreadyAdded.Add(r.RecordId));
		Pool.Return(ref alreadyAdded);
		subdirectories.Sort((RecordDirectory a, RecordDirectory b) => StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name));
		CurrentLoadState = LoadState.FullyLoaded;
	}

	public RecordDirectory(FrooxEngine.Store.Record record, RecordDirectory parent, Engine engine)
	{
		Engine = engine;
		ParentDirectory = parent;
		Name = record.Name;
		if (record.RecordType == "directory")
		{
			OwnerId = record.OwnerId;
			DirectoryRecord = record;
			Path = record.Path + "\\" + record.Name;
			return;
		}
		if (record.RecordType == "link")
		{
			string ownerId = null;
			if (Uri.TryCreate(record.AssetURI, UriKind.Absolute, out Uri result))
			{
				Cloud.Records.ExtractRecordID(result, out ownerId, out var _);
			}
			else
			{
				UniLog.Warning("Invalid link URL for Record: " + record);
			}
			OwnerId = ownerId;
			LinkRecord = record;
			Path = parent.Path + "\\" + record.Name;
			return;
		}
		throw new Exception("Invalid Record Type: " + record.RecordType);
	}

	public RecordDirectory(string ownerId, string path, Engine engine, string overrideName = null)
	{
		Engine = engine;
		OwnerId = ownerId;
		Path = path;
		if (overrideName != null)
		{
			Name = overrideName;
			return;
		}
		string[] array = path.Split('\\');
		if (array.Length != 0)
		{
			Name = array[^1];
		}
	}

	public async Task<bool> TryLocalCacheLoad()
	{
		if (CurrentLoadState == LoadState.LocalCache)
		{
			return true;
		}
		if (CurrentLoadState == LoadState.FullyLoaded)
		{
			return false;
		}
		lock (_lock)
		{
			if (CurrentLoadState == LoadState.NotLoaded && _cacheLoadTask == null)
			{
				_cacheLoadTask = Task.Run((Func<Task<bool>?>)LoadFromLocalCacheAsync);
			}
		}
		return await _cacheLoadTask.ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task<bool> LoadFromLocalCacheAsync()
	{
		if (IsLink)
		{
			Cloud.Records.ExtractRecordID(new Uri(LinkRecord.AssetURI), out var ownerId, out var recordId);
			FrooxEngine.Store.Record record = await Engine.LocalDB.FetchRecordAsync((FrooxEngine.Store.Record r) => r.OwnerId == ownerId && r.RecordId == recordId);
			if (record != null)
			{
				DirectoryRecord = record;
				return await LoadCachedFromAsync(DirectoryRecord).ConfigureAwait(continueOnCapturedContext: false);
			}
			return false;
		}
		if (DirectoryRecord != null)
		{
			return await LoadCachedFromAsync(DirectoryRecord).ConfigureAwait(continueOnCapturedContext: false);
		}
		return await LoadCachedFromAsync(OwnerId, Path).ConfigureAwait(continueOnCapturedContext: false);
	}

	public Task EnsureFullyLoaded()
	{
		if (CurrentLoadState == LoadState.FullyLoaded)
		{
			return Task.CompletedTask;
		}
		lock (_lock)
		{
			if (_loadTask == null)
			{
				_loadTask = Task.Run((Func<Task?>)FullyLoad);
			}
		}
		return _loadTask;
	}

	private async Task FullyLoad()
	{
		if (IsLink)
		{
			CloudResult<FrooxEngine.Store.Record> dirResult = await Cloud.Records.GetRecord<FrooxEngine.Store.Record>(new Uri(LinkRecord.AssetURI)).ConfigureAwait(continueOnCapturedContext: false);
			if (dirResult.IsOK)
			{
				_cacheTasks.Post(delegate
				{
					CacheRecord(dirResult.Entity).Wait();
				});
				DirectoryRecord = dirResult.Entity;
				await LoadFrom(DirectoryRecord).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		else if (DirectoryRecord != null)
		{
			await LoadFrom(DirectoryRecord).ConfigureAwait(continueOnCapturedContext: false);
		}
		else
		{
			await LoadFrom(OwnerId, Path).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	private Task LoadFrom(FrooxEngine.Store.Record directoryRecord)
	{
		return LoadFrom(directoryRecord.OwnerId, directoryRecord.Path + "\\" + directoryRecord.Name);
	}

	private async Task LoadFrom(string ownerId, string path)
	{
		CloudResult<List<FrooxEngine.Store.Record>> cloudResult = await Cloud.Records.GetRecords<FrooxEngine.Store.Record>(ownerId, null, path).ConfigureAwait(continueOnCapturedContext: false);
		if (cloudResult.IsOK)
		{
			ProcessContents(cloudResult.Entity, isCached: false);
		}
		CurrentLoadState = LoadState.FullyLoaded;
	}

	private Task<bool> LoadCachedFromAsync(FrooxEngine.Store.Record directoryRecord)
	{
		return LoadCachedFromAsync(directoryRecord.OwnerId, directoryRecord.Path + "\\" + directoryRecord.Name);
	}

	private async Task<bool> LoadCachedFromAsync(string ownerId, string path)
	{
		List<FrooxEngine.Store.Record> list = await Engine.LocalDB.FetchRecordsAsync((FrooxEngine.Store.Record r) => r.OwnerId == ownerId && r.Path == path).ConfigureAwait(continueOnCapturedContext: false);
		if (list.Count > 0)
		{
			ProcessContents(list, isCached: true);
			CurrentLoadState = LoadState.LocalCache;
			return true;
		}
		return false;
	}

	private void ProcessContents(List<FrooxEngine.Store.Record> contents, bool isCached)
	{
		Dictionary<Uri, RecordDirectory> dictionary = Pool.BorrowDictionary<Uri, RecordDirectory>();
		Dictionary<Uri, FrooxEngine.Store.Record> dictionary2 = Pool.BorrowDictionary<Uri, FrooxEngine.Store.Record>();
		List<FrooxEngine.Store.Record> toCache = new List<FrooxEngine.Store.Record>();
		List<RecordDirectory> list = new List<RecordDirectory>();
		List<FrooxEngine.Store.Record> list2 = new List<FrooxEngine.Store.Record>();
		if (!isCached)
		{
			foreach (RecordDirectory subdirectory in subdirectories)
			{
				list.Add(subdirectory);
				if (subdirectory.EntryRecord != null)
				{
					dictionary.Add(subdirectory.EntryRecord.GetUrl(Cloud.Platform), subdirectory);
				}
			}
			foreach (FrooxEngine.Store.Record record in records)
			{
				list2.Add(record);
				Uri url = record.GetUrl(Cloud.Platform);
				if (url != null)
				{
					dictionary2.Add(url, record);
				}
			}
		}
		if (contents != null)
		{
			foreach (FrooxEngine.Store.Record content in contents)
			{
				Uri url2 = content.GetUrl(Cloud.Platform);
				if (content.RecordType == "directory" || content.RecordType == "link")
				{
					if (dictionary.TryGetValue(url2, out var value))
					{
						dictionary.Remove(url2);
						if (!ReplaceRecord(value.EntryRecord, content))
						{
							continue;
						}
						list.Remove(value);
					}
					list.Add(new RecordDirectory(content, this, Engine));
					if (!isCached)
					{
						toCache.Add(content);
					}
					continue;
				}
				if (dictionary2.TryGetValue(url2, out var value2))
				{
					dictionary2.Remove(url2);
					if (!ReplaceRecord(value2, content))
					{
						continue;
					}
					list2.Remove(content);
				}
				list2.Add(content);
				if (!isCached)
				{
					toCache.Add(content);
				}
			}
		}
		foreach (KeyValuePair<Uri, RecordDirectory> item in dictionary)
		{
			list.Remove(item.Value);
		}
		foreach (KeyValuePair<Uri, FrooxEngine.Store.Record> item2 in dictionary2)
		{
			list2.Remove(item2.Value);
		}
		HashSet<string> alreadyAdded = Pool.BorrowHashSet<string>();
		list.RemoveAll((RecordDirectory r) => !alreadyAdded.Add(r.Name));
		list2.RemoveAll((FrooxEngine.Store.Record r) => !alreadyAdded.Add(r.RecordId));
		Pool.Return(ref alreadyAdded);
		list.Sort((RecordDirectory a, RecordDirectory b) => StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name));
		list2.Sort(delegate(FrooxEngine.Store.Record a, FrooxEngine.Store.Record b)
		{
			if (!a.CreationTime.HasValue && !b.CreationTime.HasValue)
			{
				return 0;
			}
			if (!a.CreationTime.HasValue)
			{
				return -1;
			}
			return (!b.CreationTime.HasValue) ? 1 : a.CreationTime.Value.CompareTo(b.CreationTime.Value);
		});
		Pool.Return(ref dictionary);
		Pool.Return(ref dictionary2);
		subdirectories = list;
		records = list2;
		Task.Run(async delegate
		{
			await Task.Delay(5000);
			foreach (FrooxEngine.Store.Record item3 in toCache)
			{
				await CacheRecord(item3).ConfigureAwait(continueOnCapturedContext: false);
				await Task.Yield();
			}
			Pool.Return(ref toCache);
		});
	}

	private bool ReplaceRecord(FrooxEngine.Store.Record oldRecord, FrooxEngine.Store.Record newRecord)
	{
		if (oldRecord.CanOverwrite(newRecord) || oldRecord.IsSameVersion(newRecord))
		{
			return false;
		}
		return true;
	}

	private async Task CacheRecord(FrooxEngine.Store.Record record)
	{
		record.IsSynced = true;
		await Engine.LocalDB.StoreRecordAsync(record, (FrooxEngine.Store.Record r) => record.CanOverwrite(r)).ConfigureAwait(continueOnCapturedContext: false);
	}

	public string GetRelativePath(bool includeRoot)
	{
		StringBuilder stringBuilder = new StringBuilder();
		GetRelativePath(includeRoot, isFirst: true, stringBuilder);
		return stringBuilder.ToString();
	}

	private void GetRelativePath(bool includeRoot, bool isFirst, StringBuilder str)
	{
		if (ParentDirectory != null)
		{
			ParentDirectory.GetRelativePath(includeRoot, isFirst: false, str);
		}
		if (includeRoot || ParentDirectory != null)
		{
			str.Append(Name);
			if (!isFirst)
			{
				str.Append('\\');
			}
		}
	}

	public List<RecordDirectory> GetChainFromRoot()
	{
		List<RecordDirectory> list = new List<RecordDirectory>();
		RecordDirectory recordDirectory = this;
		do
		{
			list.Add(recordDirectory);
			recordDirectory = recordDirectory.ParentDirectory;
		}
		while (recordDirectory != null);
		list.Reverse();
		return list;
	}

	private void CheckLoaded()
	{
		if (CurrentLoadState != LoadState.FullyLoaded)
		{
			throw new Exception("The directory is not fully loaded");
		}
	}

	private void CheckPartiallyLoaded()
	{
		if (CurrentLoadState == LoadState.NotLoaded)
		{
			throw new Exception("The directory is not loaded");
		}
	}

	public RecordDirectory GetRootDirectory()
	{
		RecordDirectory recordDirectory = this;
		while (recordDirectory.ParentDirectory != null)
		{
			recordDirectory = recordDirectory.ParentDirectory;
		}
		return recordDirectory;
	}

	public RecordDirectory GetSubdirectory(string name, bool createIfNotExist = false)
	{
		RecordDirectory recordDirectory = Subdirectories.FirstOrDefault((RecordDirectory d) => d.Name == name);
		if (recordDirectory == null && createIfNotExist)
		{
			recordDirectory = AddSubdirectory(name, dummyOnly: true);
		}
		return recordDirectory;
	}

	public RecordDirectory TryGetSubdirectoryAtPath(string path, bool createIfNotLoaded)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return this;
		}
		if (CurrentLoadState == LoadState.NotLoaded)
		{
			return null;
		}
		int num = path.IndexOfAny(new char[2] { '\\', '/' });
		if (num < 0)
		{
			return GetSubdirectory(path, createIfNotLoaded);
		}
		string name = path.Substring(0, num);
		string path2 = path.Substring(num + 1);
		return GetSubdirectory(name, createIfNotLoaded)?.TryGetSubdirectoryAtPath(path2, createIfNotLoaded);
	}

	public async Task<RecordDirectory> GetSubdirectoryAtPath(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return this;
		}
		await EnsureFullyLoaded().ConfigureAwait(continueOnCapturedContext: false);
		int num = path.IndexOfAny(new char[2] { '\\', '/' });
		if (num < 0)
		{
			return GetSubdirectory(path);
		}
		string name = path.Substring(0, num);
		string path2 = path.Substring(num + 1);
		RecordDirectory subdirectory = GetSubdirectory(name);
		if (subdirectory == null)
		{
			return null;
		}
		return await subdirectory.GetSubdirectoryAtPath(path2).ConfigureAwait(continueOnCapturedContext: false);
	}

	public RecordDirectory AddSubdirectory(string name, bool dummyOnly = false)
	{
		CheckPartiallyLoaded();
		if (GetSubdirectory(name) != null)
		{
			throw new Exception("Subdirectory with name '" + name + "' already exists.");
		}
		if (!CanWrite)
		{
			throw new InvalidOperationException("Cannot add subdirectory to a directory owned by another user");
		}
		FrooxEngine.Store.Record record = RecordHelper.CreateForDirectory<FrooxEngine.Store.Record>(OwnerId, ChildRecordPath, name);
		if (DirectoryRecord != null)
		{
			record.InheritPermissions(DirectoryRecord);
		}
		if (!dummyOnly)
		{
			Engine.RecordManager.SaveRecord(record);
		}
		RecordDirectory recordDirectory = new RecordDirectory(record, this, Engine);
		subdirectories.Add(recordDirectory);
		return recordDirectory;
	}

	public FrooxEngine.Store.Record AddItem(string name, Uri objectData, Uri thumbnail, IEnumerable<string> tags = null)
	{
		CheckPartiallyLoaded();
		if (!CanWrite)
		{
			throw new InvalidOperationException("Cannot add item to a directory owned by another user");
		}
		FrooxEngine.Store.Record record = RecordHelper.CreateForObject<FrooxEngine.Store.Record>(name, OwnerId, objectData.ToString(), thumbnail?.ToString());
		record.Path = ChildRecordPath;
		if (tags != null)
		{
			record.Tags = new HashSet<string>(tags);
		}
		if (DirectoryRecord != null)
		{
			record.InheritPermissions(DirectoryRecord);
		}
		records.Add(record);
		Task.Run(async delegate
		{
			EngineRecordUploadTask uploadTask = (await Engine.RecordManager.SaveRecord(record)).task;
			if (uploadTask != null)
			{
				await uploadTask.Task.ConfigureAwait(continueOnCapturedContext: false);
				if (!uploadTask.Failed)
				{
					record.AssetURI = uploadTask.Record.AssetURI;
				}
			}
		});
		return record;
	}

	public bool TryAddRecord(FrooxEngine.Store.Record record)
	{
		if (records.Any((FrooxEngine.Store.Record r) => record.RecordId == r.RecordId && record.OwnerId == r.OwnerId))
		{
			return false;
		}
		records.Add(record);
		return true;
	}

	public async Task<FrooxEngine.Store.Record> AddLinkAsync(string name, Uri target)
	{
		CheckPartiallyLoaded();
		if (!CanWrite)
		{
			throw new InvalidOperationException("Cannot add item to a directory owned by another user");
		}
		FrooxEngine.Store.Record record = RecordHelper.CreateForLink<FrooxEngine.Store.Record>(name, OwnerId, target.ToString());
		record.Path = ChildRecordPath;
		if (DirectoryRecord != null)
		{
			record.InheritPermissions(DirectoryRecord);
		}
		await Engine.RecordManager.SaveRecord(record);
		RecordDirectory item = new RecordDirectory(record, this, Engine);
		subdirectories.Add(item);
		return record;
	}

	public bool DeleteItem(FrooxEngine.Store.Record record)
	{
		if (records.Remove(record))
		{
			Engine.RecordManager.DeleteRecord(record);
			return true;
		}
		return false;
	}

	public Task SetPublicRecursively(bool publicState, bool followLinks = false)
	{
		return UpdateRecursively(delegate(RecordDirectory dir)
		{
			FrooxEngine.Store.Record entryRecord = dir.EntryRecord;
			if (entryRecord.IsPublic != publicState)
			{
				entryRecord.IsPublic = publicState;
				Engine.RecordManager.SaveRecord(entryRecord);
			}
		}, delegate(FrooxEngine.Store.Record record)
		{
			if (record.IsPublic != publicState)
			{
				record.IsPublic = publicState;
				Engine.RecordManager.SaveRecord(record);
			}
		}, followLinks);
	}

	public Task UpdateRecursively(Action<RecordDirectory> directoryAction, Action<FrooxEngine.Store.Record> recordAction, bool followLinks = true)
	{
		return Task.Run(async delegate
		{
			await UpdateRecursivelyAsync(this, directoryAction, recordAction, followLinks).ConfigureAwait(continueOnCapturedContext: false);
		});
	}

	private static async Task UpdateRecursivelyAsync(RecordDirectory directory, Action<RecordDirectory> directoryAction, Action<FrooxEngine.Store.Record> recordAction, bool followLinks)
	{
		if (!followLinks && directory.IsLink)
		{
			return;
		}
		await directory.EnsureFullyLoaded().ConfigureAwait(continueOnCapturedContext: false);
		if (!directory.CanWrite)
		{
			return;
		}
		directoryAction?.Invoke(directory);
		if (recordAction != null)
		{
			foreach (FrooxEngine.Store.Record record in directory.records)
			{
				recordAction(record);
			}
		}
		foreach (RecordDirectory subdirectory in directory.subdirectories)
		{
			await UpdateRecursivelyAsync(subdirectory, directoryAction, recordAction, followLinks).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	public Task DeleteSubdirectory(RecordDirectory directory)
	{
		if (!subdirectories.Remove(directory))
		{
			throw new Exception("Directory doesn't contain given subdirectory");
		}
		return Task.Run(async delegate
		{
			try
			{
				await DeleteSubdirectoryRecursively(directory).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (Exception ex)
			{
				UniLog.Error("Exception deleting subdirectory:\n" + ex, stackTrace: false);
			}
		});
	}

	private async Task DeleteSubdirectoryRecursively(RecordDirectory directory)
	{
		if (directory.CanWrite && !directory.IsLink)
		{
			await directory.EnsureFullyLoaded().ConfigureAwait(continueOnCapturedContext: false);
			foreach (RecordDirectory item in directory.Subdirectories.ToList())
			{
				await DeleteSubdirectoryRecursively(item).ConfigureAwait(continueOnCapturedContext: false);
			}
			foreach (FrooxEngine.Store.Record item2 in directory.Records.ToList())
			{
				directory.DeleteItem(item2);
			}
			if (directory.DirectoryRecord != null)
			{
				await Engine.RecordManager.DeleteRecord(directory.DirectoryRecord).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		if (directory.LinkRecord != null)
		{
			await Engine.RecordManager.DeleteRecord(directory.LinkRecord).ConfigureAwait(continueOnCapturedContext: false);
		}
	}
}
