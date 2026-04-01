using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Cryptography;
using System.Security.Permissions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Elements.Assets;
using Elements.Core;
using Elements.Data;
using LiteDB;
using LiteDB.Async;
using Newtonsoft.Json;
using SkyFrost.Base;
using Wiry.Base32;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: AssemblyTitle("FrooxEngine.Store")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("FrooxEngine.Store")]
[assembly: AssemblyCopyright("Copyright ©  2023")]
[assembly: AssemblyTrademark("")]
[assembly: ComVisible(false)]
[assembly: Guid("992eb0da-3bff-4cd8-99b8-bfcd065aaf20")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: DataModelAssembly(DataModelAssemblyType.Core)]
[assembly: TargetFramework(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
[assembly: AssemblyVersion("1.0.0.0")]
[module: UnverifiableCode]
[module: RefSafetyRules(11)]
namespace FrooxEngine.Store;

public class AssetRecord
{
	public int id { get; set; }

	public string url { get; set; }

	public string path { get; set; }

	public string signature { get; set; }

	public string cloudsig { get; set; }

	public long bytes { get; set; }

	public byte[] encryptionKey { get; set; }
}
public class LocalDatabaseAccountDataStore : IAccountDataStore
{
	public readonly LocalDB Database;

	private Dictionary<string, int> _fetchedRecords = new Dictionary<string, int>();

	public IPlatformProfile PlatformProfile { get; private set; }

	public string MigrationId { get; set; }

	public string Name => "LocalDB Data Store";

	public string UserId { get; private set; }

	public string Username { get; private set; }

	public int FetchedGroupCount { get; private set; }

	public bool MachineOwnerOnly { get; private set; }

	public event Action<string> ProgressMessage;

	public LocalDatabaseAccountDataStore(LocalDB database, IPlatformProfile platform, bool machineOwnerOnly)
	{
		Database = database;
		PlatformProfile = platform;
		MachineOwnerOnly = machineOwnerOnly;
		UserId = "M-" + database.MachineID;
	}

	public Task Complete()
	{
		return Task.CompletedTask;
	}

	public async Task<User> GetUser()
	{
		return null;
	}

	public async Task<List<ExitMessage>> GetExitMessages()
	{
		return new List<ExitMessage>();
	}

	public async Task<List<PatreonFundingEvent>> GetPatreonFundingEvents()
	{
		return new List<PatreonFundingEvent>();
	}

	public async IAsyncEnumerable<RecordAuditInfo> GetRecordAuditLog(string ownerId)
	{
		yield break;
	}

	public async Task DownloadAsset(string hash, string targetPath)
	{
		AssetRecord assetRecord = await GetAssetRecord(hash);
		if (assetRecord != null)
		{
			File.Copy(assetRecord.path, targetPath);
			FileAttributes attributes = File.GetAttributes(targetPath);
			if (attributes.HasFlag(FileAttributes.Hidden))
			{
				File.SetAttributes(targetPath, attributes & ~FileAttributes.Hidden);
			}
		}
	}

	public int FetchedRecordCount(string ownerId)
	{
		_fetchedRecords.TryGetValue(ownerId, out var value);
		return value;
	}

	public async Task<string> GetAsset(string hash)
	{
		return (await GetAssetRecord(hash))?.path;
	}

	public async Task<long> GetAssetSize(string hash)
	{
		return (await GetAssetRecord(hash))?.bytes ?? (-1);
	}

	private async Task<AssetRecord> GetAssetRecord(string hash)
	{
		AssetRecord assetRecord = await Database.TryFetchAssetRecordAsync(new Uri(PlatformProfile.DBScheme + ":///" + hash));
		if (assetRecord == null)
		{
			assetRecord = await Database.TryFetchAssetByCloudSignatureAsync(hash);
		}
		return assetRecord;
	}

	public Task<List<Contact>> GetContacts()
	{
		throw new NotSupportedException();
	}

	public async IAsyncEnumerable<GroupData> GetGroups()
	{
		if (MachineOwnerOnly)
		{
			yield break;
		}
		HashSet<string> uniqueGroups = new HashSet<string>();
		foreach (Record item in await Database.FetchRecordsAsync((Record r) => r.OwnerId.StartsWith("G-")))
		{
			if (uniqueGroups.Add(item.OwnerId))
			{
				yield return new GroupData(new Group
				{
					GroupId = item.OwnerId,
					Name = item.OwnerName
				}, new Storage());
			}
		}
	}

	public Task<DateTime> GetLatestMessageTime(string contactId)
	{
		throw new NotSupportedException();
	}

	public Task<DateTime?> GetLatestRecordTime(string ownerId)
	{
		return Task.FromResult<DateTime?>(null);
	}

	public Task<List<MemberData>> GetMembers(string groupId)
	{
		throw new NotSupportedException();
	}

	public IAsyncEnumerable<Message> GetMessages(string contactId, DateTime? from)
	{
		throw new NotSupportedException();
	}

	public async Task<SkyFrost.Base.Record> GetRecord(string ownerId, string recordId)
	{
		Record record = await Database.FetchRecordAsync((Record r) => r.OwnerId == ownerId && r.RecordId == recordId);
		if (record == null)
		{
			return null;
		}
		try
		{
			await ComputeManifest(record);
		}
		catch (Exception ex)
		{
			this.ProgressMessage?.Invoke($"Failed to compute manifest for record: {record}:\n" + ex);
		}
		return System.Text.Json.JsonSerializer.Deserialize<SkyFrost.Base.Record>(System.Text.Json.JsonSerializer.Serialize(record));
	}

	private async Task ComputeManifest(Record record)
	{
		this.ProgressMessage?.Invoke($"Computing asset manifest for record: {record}");
		if (!Uri.TryCreate(record.AssetURI, UriKind.Absolute, out Uri mainUrl))
		{
			return;
		}
		AssetRecord asset = await Database.TryFetchAssetRecordAsync(mainUrl);
		if (asset == null)
		{
			return;
		}
		DataTreeDictionary dataTreeDictionary = DataTreeConverter.Load(asset.path, mainUrl);
		if (dataTreeDictionary == null)
		{
			return;
		}
		if (record.AssetManifest == null)
		{
			record.AssetManifest = new List<DBAsset>();
		}
		HashSet<string> assets = new HashSet<string>();
		foreach (DBAsset item in record.AssetManifest)
		{
			assets.Add(item.Hash);
		}
		SavedGraph graph = new SavedGraph(dataTreeDictionary);
		await ProcessUrl(mainUrl);
		foreach (DataTreeValue uRLNode in graph.URLNodes)
		{
			Uri uri = uRLNode.TryExtractURL();
			if (!(uri == null))
			{
				await ProcessUrl(uri);
			}
		}
		async Task ProcessUrl(Uri url)
		{
			if (url.Scheme == "local")
			{
				if (await Database.TryFetchAssetRecordWithMetadataAsync(url) != null && assets.Add(asset.cloudsig))
				{
					record.AssetManifest.Add(new DBAsset
					{
						Hash = asset.cloudsig,
						Bytes = asset.bytes
					});
				}
			}
			else if (url.Scheme == PlatformProfile.DBScheme)
			{
				string text = Database.Cloud.Assets.DBSignature(url);
				if (assets.Add(text))
				{
					record.AssetManifest.Add(new DBAsset
					{
						Hash = text,
						Bytes = 0L
					});
				}
			}
		}
	}

	public async IAsyncEnumerable<SkyFrost.Base.Record> GetRecords(string ownerId, DateTime? from, Action<string> searchProgressReport = null)
	{
		List<Record> list = (from.HasValue ? (await Database.FetchRecordsAsync((Record record) => record.LastModificationTime >= ((DateTime?)from).Value)) : (await Database.FetchRecordsAsync((Record record) => true)));
		OwnerType ownerType = IdUtil.GetOwnerType(ownerId);
		foreach (Record r in list)
		{
			OwnerType ownerType2 = IdUtil.GetOwnerType(r.OwnerId);
			if (MachineOwnerOnly)
			{
				if (ownerType2 != OwnerType.Machine)
				{
					continue;
				}
			}
			else if ((ownerType == OwnerType.User && ownerType2 == OwnerType.Group) || (ownerType == OwnerType.Group && r.OwnerId != ownerId))
			{
				continue;
			}
			await ComputeManifest(r);
			string json = System.Text.Json.JsonSerializer.Serialize(r);
			yield return System.Text.Json.JsonSerializer.Deserialize<SkyFrost.Base.Record>(json);
		}
	}

	public Task<CloudVariable> GetVariable(string ownerId, string path)
	{
		throw new NotImplementedException();
	}

	public Task<List<CloudVariableDefinition>> GetVariableDefinitions(string ownerId)
	{
		throw new NotImplementedException();
	}

	public Task<List<CloudVariable>> GetVariables(string ownerId)
	{
		throw new NotImplementedException();
	}

	public Task Prepare()
	{
		return Task.CompletedTask;
	}

	public Task<AssetData> ReadAsset(string hash)
	{
		throw new NotImplementedException();
	}

	public Task StoreContact(Contact contact, IAccountDataStore source)
	{
		throw new NotSupportedException();
	}

	public Task StoreDefinitions(List<CloudVariableDefinition> definition, IAccountDataStore source)
	{
		throw new NotSupportedException();
	}

	public Task StoreGroup(Group group, Storage storage, IAccountDataStore source)
	{
		throw new NotSupportedException();
	}

	public Task StoreMember(Group group, Member member, Storage storage, IAccountDataStore source)
	{
		throw new NotSupportedException();
	}

	public Task StoreMessage(Message message, IAccountDataStore source)
	{
		throw new NotSupportedException();
	}

	public Task<StoreResultData> StoreRecord(SkyFrost.Base.Record record, IAccountDataStore source, RecordStatusCallbacks statusCallbacks = null, bool forceConflictOverwrite = false)
	{
		throw new NotImplementedException();
	}

	public Task StoreVariables(List<CloudVariable> variable, IAccountDataStore source)
	{
		throw new NotImplementedException();
	}

	public Task StoreUserData(User user)
	{
		throw new NotImplementedException();
	}

	public Task StoreExitMessage(ExitMessage exitMessage)
	{
		throw new NotImplementedException();
	}

	public Task StoreFundingEvent(PatreonFundingEvent patreonFunding)
	{
		throw new NotImplementedException();
	}

	public Task StoreRecordAudit(RecordAuditInfo info)
	{
		throw new NotImplementedException();
	}
}
public class LocalDB : IDisposable
{
	public enum ImportLocation
	{
		Original,
		Copy,
		Move
	}

	public readonly struct VariableResult<T>
	{
		public readonly T value;

		public readonly bool hasValue;

		public VariableResult(T value)
		{
			this.value = value;
			hasValue = true;
		}
	}

	public const int CURRENT_DATABASE_VERSION = 1;

	private static Type connectorType;

	private bool markedForRepair;

	private string initSubphase;

	private LiteDatabaseAsync appDB;

	private ILiteCollectionAsync<AssetRecord> assets;

	private ILiteCollectionAsync<Record> records;

	private ILiteCollectionAsync<LocalVariable> variables;

	private ILiteCollectionAsync<LocalVisit> visits;

	private ILiteCollectionAsync<LocalMetadata> assetMetadata;

	private HashSet<string> _precachePaths = new HashSet<string>();

	private string RepairMarkFile;

	private RSACryptoServiceProvider _machineCryptoProvider;

	private RSAParameters _localMachineKey;

	private static ushort[] INIT_TABLE = new ushort[10] { 0, 1, 39, 52, 48, 55, 50, 49, 30, 44 };

	private static ushort[] SEED_TABLE = new ushort[31]
	{
		25185, 25699, 26213, 26727, 27241, 27755, 28269, 28783, 29297, 29811,
		30325, 30839, 31353, 16961, 17475, 17989, 18503, 19017, 19531, 20045,
		20559, 21073, 21587, 22101, 22615, 23129, 12592, 13106, 13620, 14134,
		14648
	};

	private Dictionary<string, List<Action>> variableListeners = new Dictionary<string, List<Action>>();

	public const int IV_SIZE = 16;

	public const int KEY_SIZE = 16;

	public SkyFrostInterface Cloud { get; private set; }

	public bool DatabaseCorrupted => markedForRepair;

	public string PermanentPath { get; private set; }

	public string TemporaryPath { get; private set; }

	public string AssetCachePath { get; private set; }

	public string AssetStoragePath { get; private set; }

	public string DatabaseFile { get; private set; }

	public string LocalKeyFile { get; private set; }

	public string DatabaseVersionFile { get; private set; }

	public string MachineID { get; private set; }

	public string SecretMachineID { get; private set; }

	public string LocalOwnerID => "M-" + MachineID;

	public byte[] PublicKeyExponent => _localMachineKey.Exponent;

	public byte[] PublicKeyModulus => _localMachineKey.Modulus;

	public bool IsDisposed { get; private set; }

	private event Action _databaseCorruptionDetected;

	public event Action DatabaseCorruptionDetected
	{
		add
		{
			Task.Run(async delegate
			{
				if (DatabaseCorrupted)
				{
					value();
				}
				else
				{
					_databaseCorruptionDetected += value;
				}
			});
		}
		remove
		{
			Task.Run(async delegate
			{
				_databaseCorruptionDetected -= value;
			});
		}
	}

	public byte[] SignHash(byte[] hash)
	{
		return _machineCryptoProvider.SignHash(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
	}

	public static string GenerateGUIDSignature()
	{
		return StringHelper.ToURLBase64(Guid.CreateVersion7().ToByteArray());
	}

	public static bool IsValidMachineId(string machineId)
	{
		if (string.IsNullOrWhiteSpace(machineId))
		{
			return false;
		}
		if (machineId.Length > 52)
		{
			return false;
		}
		foreach (char c in machineId)
		{
			if (!char.IsLetterOrDigit(c))
			{
				return false;
			}
			if (char.IsLetter(c) && char.IsUpper(c))
			{
				return false;
			}
			if ("ybndrfg8ejkmcpqxot1uwisza345h769".IndexOf(c) < 0)
			{
				return false;
			}
		}
		return true;
	}

	public static string GenerateMachineID(RSAParameters rsa)
	{
		return Base32Encoding.ZBase32.GetString(HashPublicKey(rsa));
	}

	public static bool MachineIdMatches(string machineId, RSAParameters rsa)
	{
		byte[] list = Base32Encoding.ZBase32.ToBytes(machineId);
		byte[] other = HashPublicKey(rsa);
		return list.ElementWiseEquals(other);
	}

	public static byte[] HashPublicKey(RSAParameters rsa)
	{
		byte[] array = new byte[rsa.Exponent.Length + rsa.Modulus.Length];
		Array.Copy(rsa.Exponent, array, rsa.Exponent.Length);
		Array.Copy(rsa.Modulus, 0, array, rsa.Exponent.Length, rsa.Modulus.Length);
		return CryptoHelper.HashData(array);
	}

	public static string ProcessUrl(Uri url)
	{
		return ProcessUrl(url.ToString());
	}

	public static string ProcessUrl(string url)
	{
		if (url.Length > 384)
		{
			using SHA256 sHA = SHA256.Create();
			url = Convert.ToBase64String(sHA.ComputeHash(Encoding.Unicode.GetBytes(url)));
		}
		return url;
	}

	public async Task<int> GetRecordCount()
	{
		return await records.CountAsync();
	}

	public unsafe static string ProcessConnection(string connection, string seed)
	{
		StringBuilder stringBuilder = Pool.BorrowStringBuilder();
		for (int i = 0; i < connection.Length; i++)
		{
			stringBuilder.Append(connection[i]);
		}
		byte* ptr = stackalloc byte[64];
		SEED_TABLE.AsSpan().CopyTo(new Span<ushort>(ptr, 32));
		for (int j = 0; j < 128; j++)
		{
			for (int k = 0; k < seed.Length; k++)
			{
				for (int l = 0; l < seed.Length; l++)
				{
					if (l != k)
					{
						int num = (seed[l] * 43690 + j) % 62;
						int num2 = seed[k] * 43690 % 62;
						if (((j ^ k ^ l) & 1) == 0)
						{
							num2 = 61 - num2;
						}
						byte b = ptr[num];
						ptr[num] = ptr[num2];
						ptr[num2] = b;
					}
				}
			}
		}
		for (int m = 0; m < seed.Length; m++)
		{
			ushort num3 = seed[m];
			byte* ptr2 = ptr + num3 % 62;
			stringBuilder.Insert(stringBuilder.Length - num3 % (m + 1), (char)(*ptr2));
		}
		for (int n = 0; n < INIT_TABLE.Length; n++)
		{
			int num4 = INIT_TABLE[n] + n + 59;
			if (n == 0)
			{
				stringBuilder.Append((char)num4);
			}
			else
			{
				stringBuilder.Insert(stringBuilder.Length - (seed.Length + n), (char)num4);
			}
		}
		return Pool.ReturnToString(ref stringBuilder);
	}

	public int GetDatabaseVersion()
	{
		if (!File.Exists(DatabaseVersionFile))
		{
			return 0;
		}
		if (int.TryParse(File.ReadAllText(DatabaseVersionFile), out var result))
		{
			return result;
		}
		return 0;
	}

	public void WriteDatabaseVersion(int version)
	{
		File.WriteAllText(DatabaseVersionFile, version.ToString());
	}

	public async Task Initialize(IProgressIndicator progress)
	{
		progress?.UpdateProgress(-1f, "Initializing Local Data", default(LocaleString));
		bool flag = false;
		try
		{
			if (File.Exists(LocalKeyFile))
			{
				using FileStream input = File.OpenRead(LocalKeyFile);
				BinaryReader reader = new BinaryReader(input);
				try
				{
					_localMachineKey.Exponent = ReadArray();
					_localMachineKey.Modulus = ReadArray();
					_localMachineKey.P = ReadArray();
					_localMachineKey.Q = ReadArray();
					_localMachineKey.DP = ReadArray();
					_localMachineKey.DQ = ReadArray();
					_localMachineKey.InverseQ = ReadArray();
					_localMachineKey.D = ReadArray();
					_machineCryptoProvider = new RSACryptoServiceProvider();
					_machineCryptoProvider.ImportParameters(_localMachineKey);
					flag = true;
				}
				finally
				{
					if (reader != null)
					{
						((IDisposable)reader).Dispose();
					}
				}
				byte[] ReadArray()
				{
					int num = (int)reader.Read7BitEncoded();
					if (num > 4096)
					{
						throw new InvalidDataException("Local key is corrupted");
					}
					byte[] array = new byte[num];
					if (reader.Read(array, 0, num) != num)
					{
						throw new InvalidDataException("Local key is corrupted");
					}
					return array;
				}
			}
		}
		catch (Exception ex)
		{
			UniLog.Error("Exception loading local key: " + ex);
		}
		if (!flag)
		{
			RSACryptoServiceProvider rSACryptoServiceProvider = new RSACryptoServiceProvider(2048);
			_localMachineKey = rSACryptoServiceProvider.ExportParameters(includePrivateParameters: true);
			FileStream stream = File.OpenWrite(LocalKeyFile);
			try
			{
				BinaryWriter writer = new BinaryWriter(stream);
				try
				{
					WriteArray(_localMachineKey.Exponent);
					WriteArray(_localMachineKey.Modulus);
					WriteArray(_localMachineKey.P);
					WriteArray(_localMachineKey.Q);
					WriteArray(_localMachineKey.DP);
					WriteArray(_localMachineKey.DQ);
					WriteArray(_localMachineKey.InverseQ);
					WriteArray(_localMachineKey.D);
				}
				finally
				{
					if (writer != null)
					{
						((IDisposable)writer).Dispose();
					}
				}
				void WriteArray(byte[] data)
				{
					writer.Write7BitEncoded((ulong)data.Length);
					writer.Flush();
					stream.Write(data, 0, data.Length);
				}
			}
			finally
			{
				if (stream != null)
				{
					((IDisposable)stream).Dispose();
				}
			}
			_machineCryptoProvider = rSACryptoServiceProvider;
		}
		MachineID = GenerateMachineID(_localMachineKey);
		UniLog.Log("MachineID: " + MachineID);
		int version = GetDatabaseVersion();
		progress?.UpdateProgress(-1f, $"Loading LiteDB database. Version: {version}", default(LocaleString));
		if (version > 1)
		{
			UniLog.Warning("WARNING! The database is newer than currently supported one. This might cause issues.");
		}
		bool repairOrUpgrade = Environment.GetCommandLineArgs().Any((string arg) => arg.ToLower().EndsWith("repairdatabase"));
		if (File.Exists(RepairMarkFile))
		{
			repairOrUpgrade = true;
			File.Delete(RepairMarkFile);
		}
		string repairName = null;
		if (repairOrUpgrade)
		{
			string text = Path.Combine(Path.GetDirectoryName(DatabaseFile), "Corrupted" + RandomX.Int);
			string text2 = Path.Combine(text, Path.GetFileName(DatabaseFile));
			Directory.CreateDirectory(text);
			File.Move(DatabaseFile, text2);
			string text3 = DatabaseFile.Replace("Data.litedb", "Data-log.litedb");
			if (File.Exists(text3))
			{
				File.Move(text3, text2.Replace("Data.litedb", "Data-log.litedb"));
			}
			repairName = "filename=" + text2 + ";";
			repairName = ProcessConnection(repairName, MachineID);
		}
		string connectionString = ProcessConnection("filename=" + DatabaseFile + ";", MachineID);
		appDB = new LiteDatabaseAsync(connectionString);
		assets = appDB.GetCollection<AssetRecord>("Assets");
		records = appDB.GetCollection<Record>("Records");
		variables = appDB.GetCollection<LocalVariable>("Variables");
		visits = appDB.GetCollection<LocalVisit>("Visits");
		assetMetadata = appDB.GetCollection<LocalMetadata>("AssetMetadata");
		progress?.UpdateProgress(-1f, "Configuring LiteDB database", default(LocaleString));
		bool rebuildingIndex = false;
		if (version == 0)
		{
			rebuildingIndex = true;
			progress?.UpdateProgress(-1f, "Loading LiteDB database", "Rebuilding database index, please wait...");
			UniLog.Log("Detected pre-CoreCLR LiteDB database. Rebuilding indexes...");
			await assets.DropIndexAsync("url");
			await assets.DropIndexAsync("signature");
			await assets.DropIndexAsync("cloudsig");
			await records.DropIndexAsync("OwnerId");
			await records.DropIndexAsync("RecordId");
			await variables.DropIndexAsync("path");
			await visits.DropIndexAsync("url");
			await assetMetadata.DropIndexAsync("MetadataId");
		}
		if (rebuildingIndex)
		{
			progress?.UpdateProgress(-1f, "Loading LiteDB database", "Rebuilding asset.url, please wait...");
		}
		await assets.EnsureIndexAsync((AssetRecord r) => r.url);
		if (rebuildingIndex)
		{
			progress?.UpdateProgress(-1f, "Loading LiteDB database", "Rebuilding asset.signature, please wait...");
		}
		await assets.EnsureIndexAsync((AssetRecord r) => r.signature);
		if (rebuildingIndex)
		{
			progress?.UpdateProgress(-1f, "Loading LiteDB database", "Rebuilding asset.cloudsig, please wait...");
		}
		await assets.EnsureIndexAsync((AssetRecord r) => r.cloudsig);
		if (rebuildingIndex)
		{
			progress?.UpdateProgress(-1f, "Loading LiteDB database", "Rebuilding records.OwnerId, please wait...");
		}
		await records.EnsureIndexAsync((Record r) => r.OwnerId);
		if (rebuildingIndex)
		{
			progress?.UpdateProgress(-1f, "Loading LiteDB database", "Rebuilding records.RecordId, please wait...");
		}
		await records.EnsureIndexAsync((Record r) => r.RecordId);
		if (rebuildingIndex)
		{
			progress?.UpdateProgress(-1f, "Loading LiteDB database", "Rebuilding variables.path, please wait...");
		}
		await variables.EnsureIndexAsync((LocalVariable r) => r.path);
		if (rebuildingIndex)
		{
			progress?.UpdateProgress(-1f, "Loading LiteDB database", "Rebuilding visits.url, please wait...");
		}
		await visits.EnsureIndexAsync((LocalVisit r) => r.url);
		if (rebuildingIndex)
		{
			progress?.UpdateProgress(-1f, "Loading LiteDB database", "Rebuilding assetMetadata.MetadataId, please wait...");
		}
		await assetMetadata.EnsureIndexAsync((LocalMetadata r) => r.MetadataId);
		if (repairOrUpgrade)
		{
			progress?.UpdateProgress(-1f, "Repairing/Upgrading LiteDB database", default(LocaleString));
			Task task = Task.Run(async delegate
			{
				_ = 4;
				try
				{
					UniLog.Log("Opening repair database");
					LiteDatabase repairDB = new LiteDatabase(repairName);
					UniLog.Log("Extracting all assets");
					List<AssetRecord> entities = RepairExtractAllEntries(repairDB, "Assets", (AssetRecord r) => r.url);
					UniLog.Log("Inserting all assets");
					await assets.InsertBulkAsync(entities, 100000);
					UniLog.Log("Transferring Records");
					await records.InsertBulkAsync(RepairExtractAllEntries<Record>(repairDB, "Records"), 100000);
					UniLog.Log("Transferring Variables");
					await variables.InsertBulkAsync(RepairExtractAllEntries(repairDB, "Variables", (LocalVariable r) => r.path), 100000);
					UniLog.Log("Transferring Visits");
					await visits.InsertBulkAsync(RepairExtractAllEntries(repairDB, "Visits", (LocalVisit r) => r.url), 100000);
					UniLog.Log("Transferring Asset Metadata");
					await assetMetadata.InsertBulkAsync(RepairExtractAllEntries(repairDB, "AssetMetadata", (LocalMetadata r) => r.MetadataId), 100000);
					UniLog.Log("Disposing of repair database");
					repairDB.Dispose();
					UniLog.Log("Database repair/upgrade finished");
				}
				catch (Exception ex2)
				{
					UniLog.Error("Exception when repairing database: " + ex2);
					throw;
				}
			});
			int index = 0;
			while (!task.IsCompleted)
			{
				await Task.WhenAny(task, Task.Delay(TimeSpan.FromMilliseconds(500L)));
				initSubphase = initSubphase ?? "";
				progress?.UpdateProgress(-1f, "Repairing/Upgrading LiteDB database", initSubphase.PadRight(initSubphase.Length + index, '.'));
				index++;
				index %= 32;
			}
		}
		Directory.CreateDirectory(AssetCachePath);
		Directory.CreateDirectory(AssetStoragePath);
		SecretMachineID = await ReadVariableOrCreateAsync("SecretMachineID", CryptoHelper.GenerateCryptoToken());
		if (version != 1)
		{
			WriteDatabaseVersion(1);
		}
		Task.Run(async delegate
		{
			await Task.Delay(TimeSpan.FromSeconds(20L));
			foreach (string item in Directory.EnumerateFiles(PermanentPath, "*beforeRepairOrUpgrade*"))
			{
				FileInfo fileInfo = new FileInfo(item);
				if ((DateTime.UtcNow - fileInfo.LastWriteTimeUtc).TotalDays >= 14.0)
				{
					UniLog.Log("Cleaning up old database backup file: " + item);
					FileUtil.Delete(item);
				}
			}
		});
	}

	public LocalDB(SkyFrostInterface cloud, string permanentPath, string temporaryPath, HashSet<string> precachePaths)
	{
		if (cloud != null)
		{
			AssignCloud(cloud);
		}
		if (permanentPath == null)
		{
			throw new ArgumentNullException("permanentPath");
		}
		if (temporaryPath == null)
		{
			throw new ArgumentNullException("temporaryPath");
		}
		Cloud = cloud;
		PermanentPath = permanentPath;
		TemporaryPath = temporaryPath;
		_precachePaths = precachePaths;
		AssetCachePath = Path.Combine(TemporaryPath, "Cache");
		AssetStoragePath = Path.Combine(PermanentPath, "Assets");
		LocalKeyFile = Path.Combine(PermanentPath, "LocalKey.bin");
		DatabaseVersionFile = Path.Combine(PermanentPath, "Data.version");
		DatabaseFile = Path.Combine(PermanentPath, "Data.litedb");
		RepairMarkFile = Path.Combine(PermanentPath, "DatabaseRepair.mark");
	}

	public void AssignCloud(SkyFrostInterface cloud)
	{
		if (Cloud != null)
		{
			throw new InvalidOperationException("Cloud has already been assigned");
		}
		Cloud = cloud;
	}

	public void Dispose()
	{
		CheckValid();
		IsDisposed = true;
		Task.Run(async delegate
		{
			LiteDatabaseAsync liteDatabaseAsync = appDB;
			appDB = null;
			liteDatabaseAsync.Dispose();
		});
	}

	public void CheckValid()
	{
		if (IsDisposed)
		{
			throw new Exception("LocalDB is already disposed");
		}
		if (Cloud == null)
		{
			throw new InvalidOperationException("Cloud has not been assigned");
		}
	}

	public string GetTempFileName(string extension)
	{
		if (extension != null && extension.Length > 0 && extension[0] != '.')
		{
			extension = "." + extension;
		}
		return GenerateGUIDSignature() + extension;
	}

	public string GetTempFilePath(string extension)
	{
		if (extension != null && extension.Length > 0 && extension[0] != '.')
		{
			extension = "." + extension;
		}
		return GetTempFilePath() + extension;
	}

	public string GetTempFilePath()
	{
		return Path.Combine(AssetCachePath, RandomX.LettersAndDigits(16));
	}

	public bool IsPathWithinDatabase(string path)
	{
		if (Path.IsPathRooted(path))
		{
			if (!path.StartsWith(TemporaryPath, StringComparison.OrdinalIgnoreCase))
			{
				return path.StartsWith(PermanentPath, StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}
		return false;
	}

	private async Task MarkDatabaseForRepair(Exception ex)
	{
		if (IsDisposed)
		{
			throw new InvalidOperationException("Cannot mark database for repair, already disposed. Exception:\n" + ex);
		}
		if (!markedForRepair)
		{
			UniLog.Warning("Database corrupted, marking for repair on next launch.\n\n" + ex);
			if (!File.Exists(RepairMarkFile))
			{
				File.WriteAllText(RepairMarkFile, "");
			}
			markedForRepair = true;
			this._databaseCorruptionDetected?.Invoke();
		}
	}

	public List<T> RepairExtractAllEntries<T>(LiteDatabase database, string collectionName, Func<T, string> uniqueKeySelector = null)
	{
		initSubphase = "Extracting " + collectionName + " directly";
		ILiteCollection<T> collection = database.GetCollection<T>(collectionName);
		HashSet<string> uniqueKeys = new HashSet<string>();
		try
		{
			List<T> list = collection.FindAll().ToList();
			if (uniqueKeySelector != null)
			{
				list.RemoveAll((T e) => !uniqueKeys.Add(uniqueKeySelector(e)));
			}
			return list;
		}
		catch (Exception)
		{
			UniLog.Log("Failed extracting " + collectionName + " entities directly, using fallback method");
		}
		List<T> list2 = new List<T>();
		int num = collection.Count();
		initSubphase = $"Trying to extract {num} entries";
		int num2 = 0;
		while (list2.Count < num && num2 < num * 3)
		{
			try
			{
				T val = collection.FindById(num2);
				if (val != null)
				{
					if (uniqueKeySelector != null && !uniqueKeys.Add(uniqueKeySelector(val)))
					{
						continue;
					}
					list2.Add(val);
					if (list2.Count % 1000 == 0)
					{
						initSubphase = "Extracted: " + list2.Count;
					}
				}
			}
			catch
			{
			}
			num2++;
		}
		return list2;
	}

	public Uri GenerateLocalURL(string extension, string signature = null)
	{
		signature = signature ?? GenerateGUIDSignature();
		return new Uri("local://" + MachineID + "/" + signature + "." + extension);
	}

	public async Task<Uri> ImportLocalAssetAsync(string path, ImportLocation location, string forceSignature = null, string cloudSig = null)
	{
		string hashSig = await FileUtil.GenerateFileSignatureAsync(path);
		string ext = Path.GetExtension(path).Replace(".", "");
		if (forceSignature == null)
		{
			AssetRecord assetRecord = await TryFetchAssetBySignatureAsync(hashSig);
			if (assetRecord != null)
			{
				if (File.Exists(assetRecord.path))
				{
					if (location == ImportLocation.Move)
					{
						FileUtil.Delete(path);
					}
					return new Uri(assetRecord.url);
				}
				try
				{
					await assets.DeleteAsync(assetRecord.id);
				}
				catch (Exception ex)
				{
					await MarkDatabaseForRepair(ex);
					throw;
				}
			}
		}
		string text = forceSignature ?? GenerateGUIDSignature();
		Uri url = GenerateLocalURL(ext, text);
		if (location != ImportLocation.Original)
		{
			string text2 = Path.Combine(AssetStoragePath, text + "." + ext);
			FileUtil.Delete(text2);
			if (location == ImportLocation.Copy)
			{
				File.Copy(path, text2);
			}
			else
			{
				FileUtil.Move(path, text2);
			}
			path = text2;
		}
		AssetRecord assetRecord2 = new AssetRecord();
		assetRecord2.path = path;
		assetRecord2.url = url.ToString();
		assetRecord2.signature = hashSig;
		assetRecord2.cloudsig = cloudSig;
		PackAssetRecord(assetRecord2);
		try
		{
			await assets.InsertAsync(assetRecord2);
		}
		catch (Exception ex2)
		{
			await MarkDatabaseForRepair(ex2);
			throw;
		}
		return url;
	}

	public async Task<List<AssetRecord>> GetAllVariantsAsync(Uri assetURL)
	{
		List<AssetRecord> list = (await EnumerateAll(assetURL.OriginalString)).ToList();
		if (Cloud.Assets.IsValidDBUri(assetURL))
		{
			Cloud.Assets.DBSignature(assetURL, out var extension);
			if (!string.IsNullOrEmpty(extension))
			{
				string url = assetURL.OriginalString.Replace(extension, "");
				List<AssetRecord> list2 = list;
				list2.AddRange(await EnumerateAll(url));
			}
		}
		return list;
		async Task<IEnumerable<AssetRecord>> EnumerateAll(string text)
		{
			return (await assets.FindAsync((AssetRecord r) => r.url.StartsWith(text))).Where((AssetRecord r) => r.url != text && r.url.Count((char ch) => ch == '?') == 1);
		}
	}

	public async Task<string> StoreCacheRecordAsync(Uri assetURL, string path, bool encrypt = false)
	{
		if (assetURL == null)
		{
			throw new ArgumentNullException("assetURL");
		}
		if (string.IsNullOrEmpty(path))
		{
			throw new ArgumentNullException("path");
		}
		path = PathUtility.NormalizePath(path);
		string fileName = PathUtility.GetFileName(path);
		string directoryName = PathUtility.GetDirectoryName(path);
		string localpath = Path.Combine(AssetCachePath, fileName);
		byte[] array = null;
		if (assetURL.Scheme == Cloud.Assets.DBScheme && _precachePaths.Contains(directoryName))
		{
			localpath = path;
		}
		else
		{
			if (localpath != path || encrypt)
			{
				FileUtil.Delete(localpath);
				if (encrypt)
				{
					array = GenerateKey();
					using (FileStream outputStream = File.OpenWrite(localpath))
					{
						using CryptoStream destination = CreateEncryptionStream(array, outputStream);
						using FileStream fileStream = File.OpenRead(path);
						fileStream.CopyTo(destination);
					}
					FileUtil.Delete(path);
				}
				else
				{
					try
					{
						FileUtil.Move(path, localpath);
					}
					catch (Exception ex)
					{
						UniLog.Warning($"Couldn't move file {path} to {localpath} after several attempts, copying instead. Exception: {ex.Message}");
						File.Copy(path, localpath);
					}
				}
			}
			File.SetAttributes(localpath, FileAttributes.Hidden | FileAttributes.NotContentIndexed);
		}
		await StoreAssetRecordAsync(assetURL, localpath, array);
		return localpath;
	}

	public async Task DeleteCacheRecordAsync(Uri assetURL)
	{
		AssetRecord asset = await TryFetchAssetRecordAsync(assetURL);
		if (asset != null)
		{
			await assets.DeleteAsync(asset.id);
			FileUtil.Delete(asset.path);
		}
	}

	public async Task StoreAssetRecordAsync(Uri assetURL, string path, byte[] encryptionKey = null)
	{
		AssetRecord assetRecord = new AssetRecord();
		assetRecord.path = path;
		assetRecord.url = ProcessUrl(assetURL);
		assetRecord.encryptionKey = encryptionKey;
		try
		{
			await assets.DeleteManyAsync((AssetRecord rec) => rec.url == assetRecord.url);
		}
		catch (Exception ex)
		{
			await MarkDatabaseForRepair(ex);
			throw;
		}
		await InsertAssetRecordAsync(assetRecord);
	}

	private async Task InsertAssetRecordAsync(AssetRecord assetRecord)
	{
		try
		{
			assetRecord.id = 0;
			PackAssetRecord(assetRecord);
			await assets.InsertAsync(assetRecord);
		}
		catch (LiteAsyncException ex)
		{
			if (ex.InnerException.Message.Contains("duplicate key"))
			{
				AssetRecord assetRecord2 = await assets.FindOneAsync((AssetRecord a) => a.url == assetRecord.url);
				UniLog.Log($"Trying to insert duplicate asset record with URL: {assetRecord.url}, Path: {assetRecord.path}. OtherPath: {assetRecord2?.path}");
				return;
			}
			await MarkDatabaseForRepair(ex);
			throw;
		}
		catch (Exception ex2)
		{
			await MarkDatabaseForRepair(ex2);
			throw;
		}
	}

	private async Task InsertRecordAsync(Record record)
	{
		try
		{
			await records.InsertAsync(record);
		}
		catch (Exception ex)
		{
			await MarkDatabaseForRepair(ex);
			throw ex;
		}
	}

	public async Task<Uri> EnsurePermanentStorageAsync(Uri assetURL, bool ignoreMissing = false)
	{
		AssetRecord oldRecord = await TryFetchAssetRecordWithMetadataAsync(assetURL);
		if (oldRecord == null)
		{
			if (ignoreMissing)
			{
				return null;
			}
			throw new Exception($"Asset {assetURL} isn't stored in the local database!");
		}
		Uri newURL = new Uri("local://" + MachineID + "/" + oldRecord.cloudsig);
		if (await TryFetchAssetRecordAsync(newURL) != null)
		{
			return newURL;
		}
		string newPath = Path.Combine(AssetStoragePath, PathUtility.GetFileName(oldRecord.path));
		if (oldRecord.path != newPath)
		{
			bool flag = false;
			int num = 0;
			do
			{
				try
				{
					FileUtil.Move(oldRecord.path, newPath);
					flag = true;
				}
				catch (Exception)
				{
					if (++num == 3)
					{
						File.Copy(oldRecord.path, newPath);
						flag = true;
					}
					else
					{
						Thread.Sleep(50 * (num + 1));
					}
				}
			}
			while (!flag);
		}
		AssetRecord assetRecord = new AssetRecord();
		assetRecord.path = newPath;
		assetRecord.url = newURL.ToString();
		await InsertAssetRecordAsync(assetRecord);
		oldRecord.path = newPath;
		PackAssetRecord(oldRecord);
		try
		{
			await assets.UpdateAsync(oldRecord);
		}
		catch (Exception ex2)
		{
			await MarkDatabaseForRepair(ex2);
			throw;
		}
		return newURL;
	}

	public async Task<AssetRecord> TryFetchAssetBySignatureAsync(string signature)
	{
		try
		{
			return await FindAssetRecordWithExistingAsset((AssetRecord r) => r.signature == signature);
		}
		catch (Exception ex)
		{
			await MarkDatabaseForRepair(ex);
			throw;
		}
	}

	public async Task<AssetRecord> TryFetchAssetByCloudSignatureAsync(string hash)
	{
		try
		{
			return await FindAssetRecordWithExistingAsset((AssetRecord r) => r.cloudsig == hash);
		}
		catch (Exception ex)
		{
			await MarkDatabaseForRepair(ex);
			throw;
		}
	}

	private async Task<AssetRecord> FindAssetRecordWithExistingAsset(Expression<Func<AssetRecord, bool>> predicate)
	{
		AssetRecord assetRecord = null;
		do
		{
			if (assetRecord != null)
			{
				await assets.DeleteAsync(assetRecord.id);
			}
			assetRecord = await assets.FindOneAsync(predicate);
			UnpackAssetRecord(assetRecord);
		}
		while (assetRecord != null && !File.Exists(assetRecord.path));
		return assetRecord;
	}

	public async Task<AssetRecord> TryFetchAssetRecordAsync(Uri assetURL)
	{
		if (assetURL == null)
		{
			throw new ArgumentException("assetURL cannot be null");
		}
		try
		{
			string urlstr = ProcessUrl(assetURL);
			return await FindAssetRecordWithExistingAsset((AssetRecord r) => r.url == urlstr);
		}
		catch (Exception ex)
		{
			await MarkDatabaseForRepair(ex);
			throw;
		}
	}

	public async Task<Stream> TryOpenAsset(Uri assetURL)
	{
		AssetRecord assetRecord = await TryFetchAssetRecordAsync(assetURL).ConfigureAwait(continueOnCapturedContext: false);
		if (assetRecord == null)
		{
			return null;
		}
		return File.OpenRead(assetRecord.path);
	}

	public async Task<AssetRecord> TryFetchAssetRecordWithMetadataAsync(Uri assetURL)
	{
		if (assetURL == null)
		{
			throw new ArgumentException("assetURL cannot be null");
		}
		AssetRecord record = await TryFetchAssetRecordAsync(assetURL);
		if (record == null)
		{
			return null;
		}
		if (!File.Exists(record.path))
		{
			return null;
		}
		if (string.IsNullOrEmpty(record.cloudsig) || record.bytes == 0L)
		{
			if (assetURL.Scheme == Cloud.Assets.DBScheme)
			{
				record.cloudsig = Cloud.Assets.DBSignature(assetURL);
			}
			else
			{
				record.cloudsig = AssetUtil.GenerateHashSignature(record.path);
			}
			record.bytes = new FileInfo(record.path).Length;
			PackAssetRecord(record);
			try
			{
				await assets.UpdateAsync(record);
			}
			catch (Exception ex)
			{
				await MarkDatabaseForRepair(ex);
				throw;
			}
			UnpackAssetRecord(record);
		}
		return record;
	}

	private void UnpackAssetRecord(AssetRecord record)
	{
		if (record != null && record.path != null)
		{
			if (!Path.IsPathRooted(record.path))
			{
				record.path = Path.Combine(AssetStoragePath, record.path);
			}
			record.path = record.path.Replace("\\", "/");
		}
	}

	private void PackAssetRecord(AssetRecord record)
	{
		if (record != null && record.path != null && record.path.StartsWith(AssetStoragePath))
		{
			record.path = record.path.Substring(AssetStoragePath.Length + 1);
		}
	}

	public async Task<IAssetMetadata> TryFetchAssetMetadataAsync(string identifier)
	{
		IAssetMetadata assetMetadata = await TryFetchAssetMetadataAsync<BitmapMetadata>(identifier);
		if (assetMetadata == null)
		{
			IAssetMetadata assetMetadata2 = await TryFetchAssetMetadataAsync<CubemapMetadata>(identifier);
			if (assetMetadata2 == null)
			{
				IAssetMetadata assetMetadata3 = await TryFetchAssetMetadataAsync<VolumeMetadata>(identifier);
				if (assetMetadata3 == null)
				{
					IAssetMetadata assetMetadata4 = await TryFetchAssetMetadataAsync<ShaderMetadata>(identifier);
					if (assetMetadata4 == null)
					{
						IAssetMetadata assetMetadata5 = await TryFetchAssetMetadataAsync<MeshMetadata>(identifier);
						if (assetMetadata5 == null)
						{
							assetMetadata5 = await TryFetchAssetMetadataAsync<GaussianSplatMetadata>(identifier);
						}
						assetMetadata4 = assetMetadata5;
					}
					assetMetadata3 = assetMetadata4;
				}
				assetMetadata2 = assetMetadata3;
			}
			assetMetadata = assetMetadata2;
		}
		return assetMetadata;
	}

	public async Task<T> TryFetchAssetMetadataAsync<T>(string identifier) where T : class, IAssetMetadata
	{
		Type typeFromHandle = typeof(T);
		identifier = typeFromHandle.Name + "-" + ProcessUrl(identifier);
		LocalMetadata metadata;
		try
		{
			metadata = await assetMetadata.FindOneAsync((LocalMetadata r) => r.MetadataId == identifier);
		}
		catch (Exception ex)
		{
			await MarkDatabaseForRepair(ex);
			throw;
		}
		if (metadata == null)
		{
			return null;
		}
		return System.Text.Json.JsonSerializer.Deserialize<T>(metadata.Metadata);
	}

	public string MetadataIdentifier(IAssetMetadata metadata)
	{
		string text = ProcessUrl(metadata.AssetIdentifier);
		return metadata.GetType().Name + "-" + text;
	}

	public async Task SaveAssetMetadataAsync(IAssetMetadata metadata)
	{
		if (IsDisposed)
		{
			return;
		}
		string identifier = MetadataIdentifier(metadata);
		string json = System.Text.Json.JsonSerializer.Serialize(metadata, metadata.GetType());
		try
		{
			LocalMetadata localMetadata = await assetMetadata.FindOneAsync((LocalMetadata r) => r.MetadataId == identifier);
			if (localMetadata == null)
			{
				localMetadata = new LocalMetadata();
			}
			localMetadata.MetadataId = identifier;
			localMetadata.Metadata = json;
			await assetMetadata.UpsertAsync(localMetadata);
		}
		catch (LiteAsyncException ex)
		{
			if (IsDisposed || ex.InnerException.Message.Contains("duplicate key"))
			{
				return;
			}
			await MarkDatabaseForRepair(ex);
		}
		catch (Exception ex2)
		{
			if (IsDisposed)
			{
				return;
			}
			await MarkDatabaseForRepair(ex2);
		}
	}

	public async Task<Record> TryFetchRecordAsync(string ownerId, string recordId)
	{
		return await records.FindOneAsync((Record r) => r.OwnerId == ownerId && r.RecordId == recordId);
	}

	public async Task<bool> StoreRecordAsync(Record record, Func<Record, bool> overwriteCheck = null)
	{
		Record record2 = null;
		if (record.id != 0)
		{
			record2 = await records.FindByIdAsync(record.id);
		}
		if (record2 != null && (record2.OwnerId != record.OwnerId || record2.RecordId != record.RecordId))
		{
			record.id = 0;
			record2 = null;
		}
		if (record2 == null)
		{
			record2 = await TryFetchRecordAsync(record.OwnerId, record.RecordId);
		}
		if (record2 != null)
		{
			if (overwriteCheck == null || overwriteCheck(record2))
			{
				record.id = record2.id;
				try
				{
					return await records.UpdateAsync(record);
				}
				catch (Exception ex)
				{
					await MarkDatabaseForRepair(ex);
					throw;
				}
			}
			return false;
		}
		await InsertRecordAsync(record);
		return true;
	}

	public async Task<bool> DeleteRecordAsync(Uri recordUri)
	{
		if (Cloud.Records.ExtractRecordID(recordUri, out var ownerId, out var recordId))
		{
			return await DeleteRecordAsync(ownerId, recordId);
		}
		return false;
	}

	public async Task<bool> DeleteRecordAsync(string ownerId, string recordId)
	{
		Record record = await TryFetchRecordAsync(ownerId, recordId);
		if (record != null)
		{
			return await records.DeleteAsync(record.id);
		}
		return false;
	}

	public async Task<bool> DeleteRecordAsync(Record record)
	{
		if (record.id == 0)
		{
			return await DeleteRecordAsync(record.OwnerId, record.RecordId);
		}
		return await records.DeleteAsync(record.id);
	}

	public async Task UpdateRecordAsync(string ownerId, string recordId, Func<Record, bool> updateFunc)
	{
		Record record = await TryFetchRecordAsync(ownerId, recordId);
		if (updateFunc(record))
		{
			await StoreRecordAsync(record);
		}
	}

	public async Task<List<Record>> FetchRecordsAsync(Expression<Func<Record, bool>> predicate)
	{
		try
		{
			return (await records.FindAsync(predicate)).ToList();
		}
		catch (Exception ex)
		{
			await MarkDatabaseForRepair(ex);
			return null;
		}
	}

	public async Task<Record> FetchRecordAsync(Expression<Func<Record, bool>> predicate)
	{
		return await records.FindOneAsync(predicate);
	}

	public async Task<Uri> SaveAssetAsync(Bitmap2D texture, string extension = "webp", int quality = int.MaxValue, bool preserveColorInAlpha = true)
	{
		CheckValid();
		extension = extension?.ToLower();
		if (texture.BitsPerPixel > 32.0 && extension != "exr")
		{
			UniLog.Warning($"Bitmap2D has {texture.BitsPerPixel} bits per pixel, changing format to exr from {extension}");
			extension = "exr";
		}
		if (extension == "webp" && (texture.Size > 16383).Any())
		{
			UniLog.Warning("Bitmap2D has side larger than 16383, changing format from WebP");
			extension = ((quality <= 100) ? "jpg" : "png");
		}
		string tempfile = GetTempFilePath(extension);
		Task<bool> task = Task.Run(() => texture.Save(tempfile, quality, preserveColorInAlpha));
		Task<BitmapMetadata> metadataTask = Task.Run(() => BitmapMetadata.GenerateMetadata(texture));
		await task;
		BitmapMetadata metadata = await metadataTask;
		Uri uri = await ImportLocalAssetAsync(tempfile, ImportLocation.Move);
		metadata.AssetIdentifier = uri.ToString();
		await SaveAssetMetadataAsync(metadata);
		return uri;
	}

	public Task<Uri> SaveAssetAsync(GaussianCloud cloud, string extension = "frsplt", IProgressIndicator progress = null)
	{
		CheckValid();
		string tempFilePath = GetTempFilePath(extension);
		if (extension == "frsplt")
		{
			cloud.SaveToFile(tempFilePath, GaussianCloud.BinaryEncoding.LZ4, progress);
		}
		else
		{
			if (!(extension == "spz"))
			{
				throw new ArgumentException("Unsupported gaussian splat format: " + extension);
			}
			cloud.WriteToSPZ(tempFilePath, progress);
		}
		return ImportLocalAssetAsync(tempFilePath, ImportLocation.Move);
	}

	public Task<Uri> SaveAssetAsync(Bitmap3D texture, string extension = "3dtex")
	{
		CheckValid();
		string tempFilePath = GetTempFilePath(extension);
		texture.Save(tempFilePath);
		return ImportLocalAssetAsync(tempFilePath, ImportLocation.Move);
	}

	public Task<Uri> SaveAssetAsync(MeshX mesh, MeshX.Encoding encoding = MeshX.Encoding.LZ4)
	{
		CheckValid();
		string tempFilePath = GetTempFilePath("meshx");
		mesh.SaveToFile(tempFilePath, encoding);
		return ImportLocalAssetAsync(tempFilePath, ImportLocation.Move);
	}

	public Task<Uri> SaveAssetAsync(AnimX anim, AnimX.Encoding encoding = AnimX.Encoding.LZMA)
	{
		CheckValid();
		string tempFilePath = GetTempFilePath("animx");
		anim.SaveToFile(tempFilePath, encoding);
		return ImportLocalAssetAsync(tempFilePath, ImportLocation.Move);
	}

	public Task<Uri> SaveAssetAsync(BitmapCube texture)
	{
		CheckValid();
		string tempFilePath = GetTempFilePath("cubemap");
		texture.Save(tempFilePath);
		return ImportLocalAssetAsync(tempFilePath, ImportLocation.Move);
	}

	public Task<Uri> SaveAssetAsync(AudioX audio, AudioEncodeSettings encodeSettings = null)
	{
		CheckValid();
		if (encodeSettings == null)
		{
			encodeSettings = ((audio.EncodeSettings != null) ? audio.EncodeSettings : ((!(audio.Duration < 10.0)) ? ((AudioEncodeSettings)new VorbisEncodeSettings()) : ((AudioEncodeSettings)new WavEncodeSettings())));
		}
		encodeSettings.UpdateFrom(audio);
		string tempFilePath = GetTempFilePath(encodeSettings.Extension);
		audio.Encode(tempFilePath, encodeSettings);
		return ImportLocalAssetAsync(tempFilePath, ImportLocation.Move);
	}

	public void RegisterVariableListener(string path, Action callback)
	{
		lock (variableListeners)
		{
			if (!variableListeners.TryGetValue(path, out var value))
			{
				value = new List<Action>();
				variableListeners.Add(path, value);
			}
			value.Add(callback);
		}
	}

	public void UnregisterVariableListener(string path, Action callback)
	{
		lock (variableListeners)
		{
			variableListeners[path].Remove(callback);
		}
	}

	public async Task<LocalVariable> GetVariableAsync(string path)
	{
		return await variables.FindOneAsync((LocalVariable v) => v.path == path);
	}

	public async Task<T> ReadVariableOrCreateAsync<T>(string path, T def = default(T))
	{
		_ = 1;
		try
		{
			string text = await ReadVariableAsync(path);
			if (text == null)
			{
				await WriteVariableAsync(path, def);
				return def;
			}
			return Coder<T>.DecodeFromString(text);
		}
		catch (Exception)
		{
			return def;
		}
	}

	public async Task<T> ReadVariableAsync<T>(string path, T def = default(T))
	{
		try
		{
			string text = await ReadVariableAsync(path);
			if (text == null)
			{
				return def;
			}
			return Coder<T>.DecodeFromString(text);
		}
		catch (Exception)
		{
			return def;
		}
	}

	public async Task<VariableResult<T>> TryReadVariableAsync<T>(string path)
	{
		try
		{
			string text = await ReadVariableAsync(path);
			if (text == null)
			{
				return default(VariableResult<T>);
			}
			return new VariableResult<T>(Coder<T>.DecodeFromString(text));
		}
		catch (Exception)
		{
			return default(VariableResult<T>);
		}
	}

	public async Task<T> ReadOrInitVariableAsync<T>(string path, T initValue)
	{
		VariableResult<T> variableResult = await TryReadVariableAsync<T>(path);
		if (variableResult.hasValue)
		{
			return variableResult.value;
		}
		await WriteVariableAsync(path, initValue);
		return initValue;
	}

	public async Task WriteVariableAsync<T>(string path, T value)
	{
		await WriteVariableAsync(path, Coder<T>.EncodeToString(value)).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task<string> ReadVariableAsync(string path)
	{
		return (await GetVariableAsync(path).ConfigureAwait(continueOnCapturedContext: false))?.value;
	}

	public async Task WriteVariableAsync(string path, string value)
	{
		try
		{
			LocalVariable localVariable = await GetVariableAsync(path);
			if (localVariable == null)
			{
				try
				{
					await variables.InsertAsync(new LocalVariable
					{
						path = path,
						value = value
					});
				}
				catch (LiteAsyncException ex)
				{
					if (!ex.InnerException.Message.Contains("duplicate key"))
					{
						throw;
					}
					await WriteVariableAsync(path, value);
					return;
				}
			}
			else
			{
				localVariable.value = value;
				await variables.UpdateAsync(localVariable);
			}
		}
		catch (Exception ex2)
		{
			await MarkDatabaseForRepair(ex2);
			throw;
		}
		lock (variableListeners)
		{
			if (!variableListeners.TryGetValue(path, out var value2))
			{
				return;
			}
			foreach (Action item in value2)
			{
				item();
			}
		}
	}

	public async Task<bool> DeleteVariableAsync(string path)
	{
		LocalVariable localVariable = await GetVariableAsync(path);
		if (localVariable == null)
		{
			return false;
		}
		return await variables.DeleteAsync(localVariable.id);
	}

	public async Task<LocalVisit> GetVisitAsync(string url)
	{
		return await visits.FindOneAsync((LocalVisit v) => v.url == url);
	}

	public async Task LogVisitAsync(string url, int globalVersion = 0)
	{
		LocalVisit localVisit = await GetVisitAsync(url);
		if (localVisit == null)
		{
			localVisit = new LocalVisit();
		}
		localVisit.url = url;
		localVisit.globalVersion = globalVersion;
		localVisit.lastVisit = DateTime.UtcNow;
		await visits.UpsertAsync(localVisit);
	}

	public Stream OpenAssetRead(AssetRecord record)
	{
		if (record.encryptionKey != null)
		{
			return CreateDecryptionStream(record.encryptionKey, File.OpenRead(record.path));
		}
		return File.OpenRead(record.path);
	}

	public static byte[] GenerateKey()
	{
		byte[] array = new byte[16];
		using RNGCryptoServiceProvider rNGCryptoServiceProvider = new RNGCryptoServiceProvider();
		rNGCryptoServiceProvider.GetBytes(array);
		return array;
	}

	public static CryptoStream CreateEncryptionStream(byte[] key, Stream outputStream)
	{
		byte[] array = new byte[16];
		using (RNGCryptoServiceProvider rNGCryptoServiceProvider = new RNGCryptoServiceProvider())
		{
			rNGCryptoServiceProvider.GetNonZeroBytes(array);
		}
		outputStream.Write(array, 0, array.Length);
		Rijndael rijndael = new RijndaelManaged();
		rijndael.KeySize = 128;
		return new CryptoStream(outputStream, rijndael.CreateEncryptor(key, array), CryptoStreamMode.Write);
	}

	public static CryptoStream CreateDecryptionStream(byte[] key, Stream inputStream)
	{
		byte[] array = new byte[16];
		if (inputStream.Read(array, 0, array.Length) != array.Length)
		{
			throw new ApplicationException("Failed to read IV from stream.");
		}
		Rijndael rijndael = new RijndaelManaged();
		rijndael.KeySize = 128;
		return new CryptoStream(inputStream, rijndael.CreateDecryptor(key, array), CryptoStreamMode.Read);
	}
}
public static class LocalDBUtility
{
	public static async Task ExportData(string persistentPath, string tempPath, string exportPath)
	{
		SkyFrostInterface skyfrost = new SkyFrostInterface(UID.Compute(), null, SkyFrostConfig.DEFAULT_PRODUCTION);
		LocalDB localdb = new LocalDB(skyfrost, persistentPath, tempPath, new HashSet<string>());
		await localdb.Initialize(null);
		LocalDatabaseAccountDataStore source = new LocalDatabaseAccountDataStore(localdb, skyfrost.Platform, machineOwnerOnly: false);
		LocalAccountDataStore target = new LocalAccountDataStore(skyfrost.Platform, localdb.MachineID, Path.Combine(exportPath, "Data"), Path.Combine(exportPath, "Assets"));
		AccountMigrationConfig accountMigrationConfig = new AccountMigrationConfig();
		accountMigrationConfig.ClearAll();
		accountMigrationConfig.MigrateUserRecords = true;
		accountMigrationConfig.MigrateGroups = true;
		await new AccountTransferController(source, target, Guid.CreateVersion7().ToString(), accountMigrationConfig).Transfer(CancellationToken.None);
	}
}
public class LocalMetadata
{
	public int id { get; set; }

	public string MetadataId { get; set; }

	public string Metadata { get; set; }
}
public class LocalVariable
{
	public int id { get; set; }

	public string path { get; set; }

	public string value { get; set; }
}
public class LocalVariableProxy<T> : IDisposable
{
	private string path;

	private T currentValue;

	private T defaultValue;

	private LocalDB db;

	public T Value
	{
		get
		{
			return currentValue;
		}
		set
		{
			Task.Run(async delegate
			{
				await db.WriteVariableAsync(path, value);
			});
		}
	}

	public string Path => path;

	public event Action<LocalVariableProxy<T>> OnChanged;

	public LocalVariableProxy(LocalDB db, string path, T defaultValue)
	{
		this.db = db;
		this.path = path;
		this.defaultValue = defaultValue;
		db.RegisterVariableListener(path, OnVariableChanged);
		OnVariableChanged();
	}

	private void OnVariableChanged()
	{
		Task.Run(async delegate
		{
			currentValue = await db.ReadVariableAsync(path, defaultValue);
			this.OnChanged?.Invoke(this);
		});
	}

	public void Dispose()
	{
		db.UnregisterVariableListener(path, OnVariableChanged);
		db = null;
	}

	public static implicit operator T(LocalVariableProxy<T> proxy)
	{
		return proxy.Value;
	}
}
public class LocalVisit
{
	public int id { get; set; }

	public string url { get; set; }

	public int globalVersion { get; set; }

	public DateTime lastVisit { get; set; }
}
[Serializable]
[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
[DataModelType]
public class Record : IRecord
{
	private Uri _cachedURL;

	private string _cachedURL_OwnerId;

	private string _cachedURL_RecordId;

	[System.Text.Json.Serialization.JsonIgnore]
	public int id { get; set; }

	[System.Text.Json.Serialization.JsonIgnore]
	public RecordId CombinedRecordId => new RecordId(OwnerId, RecordId);

	[JsonProperty(PropertyName = "id")]
	[JsonPropertyName("id")]
	public string RecordId { get; set; }

	[JsonProperty(PropertyName = "ownerId")]
	[JsonPropertyName("ownerId")]
	public string OwnerId { get; set; }

	[JsonProperty(PropertyName = "assetUri")]
	[JsonPropertyName("assetUri")]
	public string AssetURI { get; set; }

	[JsonProperty(PropertyName = "version")]
	[JsonPropertyName("version")]
	public RecordVersion Version { get; set; }

	[System.Text.Json.Serialization.JsonIgnore]
	public bool IsSynced { get; set; }

	[System.Text.Json.Serialization.JsonIgnore]
	public Record ConflictingCloudVersion { get; set; }

	[System.Text.Json.Serialization.JsonIgnore]
	public DateTime FetchedOn { get; set; }

	[JsonProperty(PropertyName = "name")]
	[JsonPropertyName("name")]
	public string Name { get; set; }

	[JsonProperty(PropertyName = "description", NullValueHandling = NullValueHandling.Ignore)]
	[JsonPropertyName("description")]
	public string Description { get; set; }

	[JsonProperty(PropertyName = "recordType")]
	[JsonPropertyName("recordType")]
	public string RecordType { get; set; }

	[JsonProperty(PropertyName = "ownerName")]
	[JsonPropertyName("ownerName")]
	public string OwnerName { get; set; }

	[JsonProperty(PropertyName = "tags", NullValueHandling = NullValueHandling.Ignore)]
	[JsonPropertyName("tags")]
	public HashSet<string> Tags { get; set; }

	[JsonProperty(PropertyName = "path")]
	[JsonPropertyName("path")]
	public string Path { get; set; }

	[JsonProperty(PropertyName = "thumbnailUri", NullValueHandling = NullValueHandling.Ignore)]
	[JsonPropertyName("thumbnailUri")]
	public string ThumbnailURI { get; set; }

	[JsonProperty(PropertyName = "isPublic")]
	[JsonPropertyName("isPublic")]
	public bool IsPublic { get; set; }

	[JsonProperty(PropertyName = "isForPatrons")]
	[JsonPropertyName("isForPatrons")]
	public bool IsForPatrons { get; set; }

	[JsonProperty(PropertyName = "isListed")]
	[JsonPropertyName("isListed")]
	public bool IsListed { get; set; }

	[JsonProperty(PropertyName = "isReadOnly")]
	[JsonPropertyName("isReadOnly")]
	public bool IsReadOnly { get; set; }

	[JsonProperty(PropertyName = "lastModificationTime")]
	[JsonPropertyName("lastModificationTime")]
	public DateTime LastModificationTime { get; set; }

	[JsonProperty(PropertyName = "rootRecordId", NullValueHandling = NullValueHandling.Ignore)]
	[JsonPropertyName("rootRecordId")]
	public RecordId RootRecordId { get; set; }

	[JsonProperty(PropertyName = "creationTime")]
	[JsonPropertyName("creationTime")]
	public DateTime? CreationTime { get; set; }

	[JsonProperty(PropertyName = "firstPublishTime")]
	[JsonPropertyName("firstPublishTime")]
	public DateTime? FirstPublishTime { get; set; }

	[JsonProperty(PropertyName = "isDeleted")]
	[JsonPropertyName("isDeleted")]
	public bool IsDeleted { get; set; }

	[JsonProperty(PropertyName = "visits")]
	[JsonPropertyName("visits")]
	public int Visits { get; set; }

	[JsonProperty(PropertyName = "rating")]
	[JsonPropertyName("rating")]
	public double Rating { get; set; }

	[JsonProperty(PropertyName = "randomOrder")]
	[JsonPropertyName("randomOrder")]
	public int RandomOrder { get; set; }

	[JsonProperty(PropertyName = "submissions")]
	[JsonPropertyName("submissions")]
	public List<Submission> Submissions { get; set; }

	[JsonProperty(PropertyName = "migrationMetadata", NullValueHandling = NullValueHandling.Ignore)]
	[JsonPropertyName("migrationMetadata")]
	public MigrationMetadata MigrationMetadata { get; set; }

	[System.Text.Json.Serialization.JsonIgnore]
	public List<string> Manifest { get; set; }

	[JsonProperty(PropertyName = "assetManifest", NullValueHandling = NullValueHandling.Ignore)]
	[JsonPropertyName("assetManifest")]
	public List<DBAsset> AssetManifest { get; set; }

	public bool IsValidOwnerId => IdUtil.GetOwnerType(OwnerId) != OwnerType.INVALID;

	public bool IsValidRecordId => RecordHelper.IsValidRecordID(RecordId);

	[Obsolete]
	[JsonProperty(PropertyName = "neosDBmanifest", NullValueHandling = NullValueHandling.Ignore)]
	[JsonPropertyName("neosDBmanifest")]
	public List<DBAsset> LegacyManifest
	{
		get
		{
			return null;
		}
		set
		{
			if (value != null)
			{
				if (AssetManifest == null)
				{
					AssetManifest = value;
				}
				else
				{
					AssetManifest.AddRange(value);
				}
			}
		}
	}

	[Obsolete]
	[JsonProperty(PropertyName = "globalVersion")]
	[JsonPropertyName("globalVersion")]
	public int? LegacyGlobalVersion
	{
		set
		{
			if (value.HasValue)
			{
				RecordVersion version = Version;
				version.GlobalVersion = value.Value;
				Version = version;
			}
		}
	}

	[Obsolete]
	[JsonProperty(PropertyName = "localVersion")]
	[JsonPropertyName("localVersion")]
	public int? LegacyLocalVersion
	{
		set
		{
			if (value.HasValue)
			{
				RecordVersion version = Version;
				version.LocalVersion = value.Value;
				Version = version;
			}
		}
	}

	[Obsolete]
	[JsonProperty(PropertyName = "lastModifyingUserId")]
	[JsonPropertyName("lastModifyingUserId")]
	public string LegacyLastModifyingUserId
	{
		set
		{
			RecordVersion version = Version;
			version.LastModifyingUserId = value;
			Version = version;
		}
	}

	[Obsolete]
	[JsonProperty(PropertyName = "lastModifyingMachineId")]
	[JsonPropertyName("lastModifyingMachineId")]
	public string LegacyLastModifyingMachineId
	{
		set
		{
			RecordVersion version = Version;
			version.LastModifyingMachineId = value;
			Version = version;
		}
	}

	public void ReplaceInTags(string oldString, string newString)
	{
		if (Tags == null)
		{
			return;
		}
		HashSet<string> hashSet = Pool.BorrowHashSet<string>();
		foreach (string tag in Tags)
		{
			if (tag.Contains(oldString))
			{
				hashSet.Add(tag);
			}
		}
		foreach (string item in hashSet)
		{
			Tags.Remove(item);
			Tags.Add(item.Replace(oldString, newString));
		}
	}

	public void ClearRecordSpecificMetadata()
	{
		Submissions?.Clear();
		FirstPublishTime = null;
		Visits = 0;
		Rating = 0.0;
		OwnerName = null;
		IsSynced = false;
		ConflictingCloudVersion = null;
		Version = default(RecordVersion);
		IsPublic = false;
		IsListed = false;
		MigrationMetadata = null;
		id = 0;
	}

	public Uri GetUrl(IPlatformProfile platform)
	{
		if (_cachedURL == null || OwnerId != _cachedURL_OwnerId || RecordId != _cachedURL_RecordId)
		{
			_cachedURL = RecordHelper.GetUrl(this, platform);
			_cachedURL_OwnerId = OwnerId;
			_cachedURL_RecordId = RecordId;
		}
		return _cachedURL;
	}

	public Uri GetWebURL(IPlatformProfile platform)
	{
		return this.GetWebUrl(platform);
	}

	public override string ToString()
	{
		return $"Record {CombinedRecordId}, Name: {Name}, Path: {Path}";
	}

	public static bool IsValidId(string recordId)
	{
		return recordId.StartsWith("R-", ignoreCase: false, CultureInfo.InvariantCulture);
	}

	public void ResetVersioning()
	{
		id = 0;
		Version = default(RecordVersion);
	}

	public void OverrideGlobalVersion(int globalVersion)
	{
		if (globalVersion < Version.GlobalVersion)
		{
			throw new InvalidOperationException($"GlobalVersion cannot be set to a lower value than it already is. Current: {Version.GlobalVersion}, target: {globalVersion}");
		}
		RecordVersion version = Version;
		version.GlobalVersion = globalVersion;
		Version = version;
	}

	public void IncrementGlobalVersion()
	{
		OverrideGlobalVersion(Version.GlobalVersion + 1);
	}

	public void IncrementLocalVersion(string machineId, string userId)
	{
		RecordVersion version = Version;
		version.LocalVersion++;
		version.LastModifyingMachineId = machineId;
		version.LastModifyingUserId = userId;
		Version = version;
	}

	public R Clone<R>() where R : class, IRecord, new()
	{
		return System.Text.Json.JsonSerializer.Deserialize<R>(System.Text.Json.JsonSerializer.Serialize(this));
	}
}
