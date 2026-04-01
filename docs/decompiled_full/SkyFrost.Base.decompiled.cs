using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Elements.Assets;
using Elements.Core;
using Elements.Data;
using EnumsNET;
using Hardware.Info;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SignalR.Strong;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: AssemblyTitle("SkyFrost.Base")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Yellow Dog Man Studios")]
[assembly: AssemblyCopyright("Copyright © 2023")]
[assembly: AssemblyProduct("SkyFrost.Base")]
[assembly: AssemblyTrademark("")]
[assembly: ComVisible(false)]
[assembly: Guid("67fa0b7e-451b-4e24-a70f-b12cb4c5b5c6")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: DataModelAssembly(DataModelAssemblyType.Core)]
[assembly: TargetFramework(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]
[assembly: AssemblyVersion("1.0.0.0")]
[module: RefSafetyRules(11)]
namespace SkyFrost.Base;

public class AccountDataStoreUploadTask : RecordUploadTaskBase<Record>
{
	private IAccountDataStore source;

	public AccountDataStoreUploadTask(SkyFrostInterface cloud, IAccountDataStore source, Record record)
		: base(cloud, record, ensureFolder: false)
	{
		this.source = source;
	}

	protected override async ValueTask<AssetData> ReadFile(string signature)
	{
		return await source.ReadAsset(signature).ConfigureAwait(continueOnCapturedContext: false);
	}

	protected override Task<bool> PrepareFilesForUpload(CancellationToken cancellationToken)
	{
		return Task.FromResult(result: true);
	}

	protected override Task<bool> PrepareRecord(CancellationToken cancelationToken)
	{
		base.Record.AssetURI = base.Record.AssetURI.MigrateURL(source.PlatformProfile, base.Cloud.Platform);
		base.Record.ThumbnailURI = base.Record.ThumbnailURI.MigrateURL(source.PlatformProfile, base.Cloud.Platform);
		if (base.Record.Tags != null)
		{
			HashSet<string> hashSet = new HashSet<string>();
			foreach (string tag in base.Record.Tags)
			{
				hashSet.Add(tag.MigrateSubStrings(source.PlatformProfile, base.Cloud.Platform));
			}
			base.Record.Tags = hashSet;
		}
		return Task.FromResult(result: true);
	}

	protected override Task StoreSyncedRecord(Record record)
	{
		return Task.CompletedTask;
	}
}
public class AccountTransferController
{
	private IAccountDataStore source;

	private IAccountDataStore target;

	private AccountMigrationConfig config;

	public string ProgressMessage { get; private set; }

	public bool ContactsCompleted { get; set; }

	public bool UserOwnedCompleted { get; set; }

	public HashSet<string> GroupsCompleted { get; set; }

	public AccountMigrationStatus Status { get; private set; } = new AccountMigrationStatus();

	public event Action<string> ProgressMessagePosted;

	public AccountTransferController(IAccountDataStore source, IAccountDataStore target, string migrationId, AccountMigrationConfig config)
	{
		this.source = source;
		this.target = target;
		this.source.MigrationId = migrationId;
		this.target.MigrationId = migrationId;
		this.config = config;
		this.source.ProgressMessage += delegate(string str)
		{
			this.ProgressMessagePosted?.Invoke(str);
		};
		this.target.ProgressMessage += delegate(string str)
		{
			this.ProgressMessagePosted?.Invoke(str);
		};
		if (this.target is CloudAccountDataStore cloudAccountDataStore)
		{
			cloudAccountDataStore.PreserveOldHome = config.PreserveOldHome;
		}
	}

	private void SetProgressMessage(string message)
	{
		ProgressMessage = message;
		this.ProgressMessagePosted?.Invoke(message);
	}

	private bool ProcessRecord(Record record)
	{
		return true;
	}

	private bool ProcessContact(Contact contact)
	{
		if (contact.ContactUserId == target.PlatformProfile.AppUserId && source.PlatformProfile.AppUserId != target.PlatformProfile.AppUserId)
		{
			return false;
		}
		if (contact.ContactUserId == target.PlatformProfile.DevBotUserId && source.PlatformProfile.DevBotUserId != target.PlatformProfile.DevBotUserId)
		{
			return false;
		}
		return true;
	}

	private bool ProcessGroup(Group group)
	{
		if (config.GroupsToMigrate == null || config.GroupsToMigrate.Count == 0)
		{
			return true;
		}
		return config.GroupsToMigrate.Contains(group.GroupId);
	}

	public async Task<bool> Transfer(CancellationToken cancellationToken)
	{
		_ = 12;
		try
		{
			Status.Phase = "Setup";
			SetProgressMessage("Beginning transfer");
			Status.StartedOn = DateTimeOffset.UtcNow;
			SetProgressMessage("Preparing data source");
			await source.Prepare().ConfigureAwait(continueOnCapturedContext: false);
			SetProgressMessage("Preparing data target");
			await target.Prepare().ConfigureAwait(continueOnCapturedContext: false);
			Status.CurrentlyMigratingName = source.Username;
			if (config.MigrateUserProfile)
			{
				await TransferUserProfileData().ConfigureAwait(continueOnCapturedContext: false);
			}
			if (config.MigrateFundingEvents)
			{
				await TransferFundingEvents().ConfigureAwait(continueOnCapturedContext: false);
			}
			if (config.MigrateExitMessages)
			{
				await TransferExitMessages().ConfigureAwait(continueOnCapturedContext: false);
			}
			if (config.MigrateContacts && !ContactsCompleted)
			{
				await TransferContacts(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				ContactsCompleted = true;
			}
			if (cancellationToken.IsCancellationRequested)
			{
				return false;
			}
			if (!UserOwnedCompleted)
			{
				await TransferOwned(source.UserId, Status.UserRecordsStatus, Status.UserVariablesStatus, config.RecordsToMigrate, config.VariablesToMigrate, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				UserOwnedCompleted = true;
			}
			if (cancellationToken.IsCancellationRequested)
			{
				return false;
			}
			bool result;
			if (config.MigrateGroups)
			{
				await foreach (GroupData item in source.GetGroups().ConfigureAwait(continueOnCapturedContext: false))
				{
					Status.Phase = "Groups";
					if (cancellationToken.IsCancellationRequested)
					{
						result = false;
					}
					else
					{
						Group group = item.group;
						Status.TotalGroupCount = source.FetchedGroupCount;
						if (!ProcessGroup(group))
						{
							continue;
						}
						GroupMigrationStatus groupStatus = Status.GetGroupStatus(group.GroupId, group.Name);
						Status.CurrentlyMigratingName = group.Name;
						SetProgressMessage($"Transferring group {group.Name} ({group.GroupId})");
						await target.StoreGroup(group, item.storage, source).ConfigureAwait(continueOnCapturedContext: false);
						if (cancellationToken.IsCancellationRequested)
						{
							result = false;
						}
						else
						{
							Status.Phase = "Group Members";
							await TransferMembers(group, groupStatus, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
							if (!cancellationToken.IsCancellationRequested)
							{
								if (!GroupsCompleted.Contains(group.GroupId))
								{
									Status.Phase = "Group Records";
									await TransferOwned(group.GroupId, groupStatus.RecordsStatus, groupStatus.VariablesStatus, null, null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
									GroupsCompleted.Add(group.GroupId);
									Status.MigratedGroupCount++;
								}
								continue;
							}
							result = false;
						}
					}
					goto IL_08e1;
				}
			}
			Status.Phase = "Finalizing";
			if (cancellationToken.IsCancellationRequested)
			{
				return false;
			}
			SetProgressMessage("Finishing up transfer...");
			await target.Complete().ConfigureAwait(continueOnCapturedContext: false);
			SetProgressMessage("Transfer complete");
			Status.Phase = "Complete";
			return true;
			IL_08e1:
			return result;
		}
		catch (Exception ex)
		{
			Status.Phase = "Error";
			Status.Error = ex.ToString();
			return false;
		}
		finally
		{
			Status.CompletedOn = DateTimeOffset.UtcNow;
			Status.CurrentlyMigratingName = null;
			Status.CurrentlyMigratingItem = null;
		}
	}

	public async Task TransferUserProfileData()
	{
		Status.Phase = "Migrating user profile";
		SetProgressMessage(Status.Phase);
		User user = await source.GetUser().ConfigureAwait(continueOnCapturedContext: false);
		if (user != null)
		{
			await target.StoreUserData(user).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	public async Task TransferFundingEvents()
	{
		Status.Phase = "Migrating funding events";
		SetProgressMessage(Status.Phase);
		List<PatreonFundingEvent> list = await source.GetPatreonFundingEvents().ConfigureAwait(continueOnCapturedContext: false);
		if (list == null)
		{
			return;
		}
		foreach (PatreonFundingEvent item in list)
		{
			await target.StoreFundingEvent(item).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	public async Task TransferExitMessages()
	{
		Status.Phase = "Migrating exit messages";
		SetProgressMessage(Status.Phase);
		List<ExitMessage> list = await source.GetExitMessages().ConfigureAwait(continueOnCapturedContext: false);
		if (list == null)
		{
			return;
		}
		foreach (ExitMessage item in list)
		{
			await target.StoreExitMessage(item).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	public async Task TransferOwned(string ownerId, RecordMigrationStatus recordsStatus, VariableMigrationStatus variablesStatus, List<string> recordsToMigrate, List<string> variablesToMigrate, CancellationToken cancellationToken)
	{
		if (config.MigrateCloudVariableDefinitions)
		{
			Status.Phase = "Cloud Variable Definitions";
			await TransferVariableDefinitions(ownerId, variablesStatus, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (cancellationToken.IsCancellationRequested)
		{
			return;
		}
		Status.Phase = "Cloud Variables";
		if (config.MigrateCloudVariables)
		{
			if (variablesToMigrate == null || variablesToMigrate.Count <= 0)
			{
				await TransferVariables(ownerId, variablesStatus, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			else
			{
				List<CloudVariable> variables = new List<CloudVariable>();
				foreach (string item in variablesToMigrate)
				{
					CloudVariable cloudVariable = await source.GetVariable(ownerId, item).ConfigureAwait(continueOnCapturedContext: false);
					if (cloudVariable != null)
					{
						variables.Add(cloudVariable);
					}
				}
				Dictionary<string, string> dictionary = new Dictionary<string, string>();
				foreach (FavoriteEntity value2 in Enums.GetValues<FavoriteEntity>())
				{
					string text = source.PlatformProfile.FavoriteVariable(value2);
					string text2 = target.PlatformProfile.FavoriteVariable(value2);
					if (text != null && text2 != null && !(text == text2))
					{
						dictionary.Add(text, text2);
					}
				}
				foreach (CloudVariable item2 in variables)
				{
					if (dictionary.TryGetValue(item2.Path, out var value))
					{
						item2.Path = value;
					}
				}
				await target.StoreVariables(variables, source).ConfigureAwait(continueOnCapturedContext: false);
				variablesStatus.MigratedVariableCount = variables.Count;
			}
		}
		if (cancellationToken.IsCancellationRequested)
		{
			return;
		}
		Status.Phase = "Records";
		if (config.MigrateUserRecords || IdUtil.GetOwnerType(ownerId) != OwnerType.User)
		{
			if (recordsToMigrate == null || recordsToMigrate.Count <= 0)
			{
				await TransferRecords(ownerId, recordsStatus, config.OnlyNewRecords, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
			else
			{
				await TransferSelectedRecords(ownerId, recordsStatus, recordsToMigrate).ConfigureAwait(continueOnCapturedContext: false);
			}
			if (config.MigrateRecordAuditLog)
			{
				await TrasnferRecordAuditLog(ownerId).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
	}

	private async Task TrasnferRecordAuditLog(string ownerId)
	{
		Status.Phase = "Record Audit Log";
		SetProgressMessage("Transferring record audit log for " + ownerId);
		await foreach (RecordAuditInfo item in source.GetRecordAuditLog(ownerId).ConfigureAwait(continueOnCapturedContext: false))
		{
			SetProgressMessage($"Transferring record audit log: {item}");
			await target.StoreRecordAudit(item).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	private async Task TransferSelectedRecords(string ownerId, RecordMigrationStatus recordsStatus, List<string> recordsToMigrate)
	{
		RecordStatusCallbacks callbacks = SetupCallbacks(recordsStatus);
		recordsStatus.TotalRecordCount = recordsToMigrate.Count;
		recordsStatus.Updated();
		foreach (string item in recordsToMigrate)
		{
			Record record = await source.GetRecord(ownerId, item).ConfigureAwait(continueOnCapturedContext: false);
			if (record == null)
			{
				continue;
			}
			Status.CurrentlyMigratingItem = $"Record {record.Name} ({record.CombinedRecordId})";
			StoreResultData storeResultData = await target.StoreRecord(record, source, callbacks, config.ForceOverwrite).ConfigureAwait(continueOnCapturedContext: false);
			if (storeResultData.result == RecordStoreResult.Error || storeResultData.result == RecordStoreResult.Conflict)
			{
				if (storeResultData.result == RecordStoreResult.Conflict)
				{
					lock (recordsStatus)
					{
						recordsStatus.ConflictedRecordCount++;
						recordsStatus.Updated();
					}
				}
				HandleRecordError(recordsStatus, record, storeResultData.error);
				continue;
			}
			lock (recordsStatus)
			{
				recordsStatus.Updated();
				switch (storeResultData.result)
				{
				case RecordStoreResult.Stored:
					recordsStatus.MigratedRecordCount++;
					break;
				case RecordStoreResult.AlreadyExists:
					recordsStatus.AlreadyMigratedRecordCount++;
					break;
				}
			}
		}
	}

	public async Task TransferVariables(string ownerId, VariableMigrationStatus status, CancellationToken cancellationToken)
	{
		SetProgressMessage("Transferring variables for " + ownerId + "...");
		List<CloudVariable> variables = await source.GetVariables(ownerId).ConfigureAwait(continueOnCapturedContext: false);
		if (!cancellationToken.IsCancellationRequested)
		{
			await target.StoreVariables(variables, source).ConfigureAwait(continueOnCapturedContext: false);
			status.MigratedVariableCount = variables.Count;
			SetProgressMessage($"Transferred {variables.Count} variables");
		}
	}

	public async Task TransferVariableDefinitions(string ownerId, VariableMigrationStatus status, CancellationToken cancellationToken)
	{
		SetProgressMessage("Transferring variable definitions for " + ownerId + "...");
		List<CloudVariableDefinition> definitions = await source.GetVariableDefinitions(ownerId).ConfigureAwait(continueOnCapturedContext: false);
		await target.StoreDefinitions(definitions, source).ConfigureAwait(continueOnCapturedContext: false);
		status.MigratedVariableDefinitionCount = definitions.Count;
		SetProgressMessage($"Transferred {definitions.Count} variable definitions");
	}

	private RecordStatusCallbacks SetupCallbacks(RecordMigrationStatus status)
	{
		return new RecordStatusCallbacks
		{
			AssetToUploadAdded = delegate(AssetDiff diff)
			{
				lock (status)
				{
					status.AssetsToUpload++;
					status.BytesToUpload += diff.Bytes;
					status.Updated();
				}
			},
			BytesUploaded = delegate(long bytes)
			{
				lock (status)
				{
					status.BytesUploaded += bytes;
					status.Updated();
				}
			},
			AssetUploaded = delegate
			{
				lock (status)
				{
					status.AssetsUploaded++;
					status.Updated();
				}
			},
			AssetMissing = delegate(string hash)
			{
				Status.RegisterMissingAsset(hash);
			},
			MigrationStarted = delegate(string id)
			{
				lock (status)
				{
					status.CurrentlyMigratingRecords.Add(id);
					status.Updated();
				}
			},
			MigrationFinished = delegate(string id)
			{
				lock (status)
				{
					status.CurrentlyMigratingRecords.Remove(id);
					status.Updated();
				}
			}
		};
	}

	public async Task TransferRecords(string ownerId, RecordMigrationStatus status, bool onlyNew, CancellationToken cancellationToken)
	{
		DateTime? dateTime = null;
		if (onlyNew)
		{
			dateTime = await target.GetLatestRecordTime(ownerId).ConfigureAwait(continueOnCapturedContext: false);
			if (dateTime.HasValue)
			{
				try
				{
					dateTime = dateTime.Value.AddDays(-1.0);
				}
				catch
				{
					dateTime = null;
				}
			}
		}
		string text = (onlyNew ? $" from {dateTime}" : "");
		SetProgressMessage("Transferring records for " + ownerId + text);
		int count = 0;
		ActionBlock<Record> recordProcessing = new ActionBlock<Record>(async delegate(Record r)
		{
			if (!cancellationToken.IsCancellationRequested && !Status.Abort)
			{
				Status.CurrentlyMigratingItem = $"Record {r.Name} ({r.CombinedRecordId})";
				try
				{
					StoreResultData storeResultData = await target.StoreRecord(r, source, SetupCallbacks(status), config.ForceOverwrite).ConfigureAwait(continueOnCapturedContext: false);
					if (storeResultData.result == RecordStoreResult.Error || storeResultData.result == RecordStoreResult.Conflict)
					{
						if (storeResultData.result == RecordStoreResult.Conflict)
						{
							lock (status)
							{
								status.ConflictedRecordCount++;
								status.Updated();
							}
						}
						if (storeResultData.error != null && storeResultData.error.Contains("Out of space"))
						{
							Status.Error = "Out of space";
							Status.Abort = true;
							throw new Exception("Out of space");
						}
						HandleRecordError(status, r, storeResultData.error);
						return;
					}
					lock (status)
					{
						status.Updated();
						switch (storeResultData.result)
						{
						case RecordStoreResult.Stored:
							status.MigratedRecordCount++;
							break;
						case RecordStoreResult.AlreadyExists:
							status.AlreadyMigratedRecordCount++;
							break;
						}
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"EXCEPTION PROCESSING RECORD\n{r}\n{ex}");
					HandleRecordError(status, r, ex.ToString());
					return;
				}
				if (Interlocked.Increment(ref count) % 100 == 0)
				{
					SetProgressMessage($"Transferred {count} records...");
				}
			}
		}, new ExecutionDataflowBlockOptions
		{
			MaxDegreeOfParallelism = 8,
			EnsureOrdered = false
		});
		await foreach (Record item in source.GetRecords(ownerId, dateTime, delegate(string msg)
		{
			status.RecordSearchPhase = msg;
			status.Updated();
		}).ConfigureAwait(continueOnCapturedContext: false))
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return;
			}
			status.TotalRecordCount = source.FetchedRecordCount(ownerId);
			status.Updated();
			if (ProcessRecord(item))
			{
				recordProcessing.Post(item);
				while (recordProcessing.InputCount > 16)
				{
					await Task.Delay(100);
				}
			}
		}
		recordProcessing.Complete();
		await recordProcessing.Completion.ConfigureAwait(continueOnCapturedContext: false);
		SetProgressMessage($"Transferred {count} records.");
	}

	public async Task TransferContacts(CancellationToken cancellationToken)
	{
		Status.Phase = "Contacts";
		SetProgressMessage("Transfering contacts");
		List<Contact> contacts = await source.GetContacts().ConfigureAwait(continueOnCapturedContext: false);
		Status.TotalContactCount = contacts.Count;
		Dictionary<string, string> contactIdMapping = new Dictionary<string, string>();
		foreach (Contact contact in contacts)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return;
			}
			if (ProcessContact(contact))
			{
				Status.CurrentlyMigratingItem = "Contact: " + contact.ContactUsername;
				string originalContactId = contact.ContactUserId;
				await target.StoreContact(contact, source);
				SetProgressMessage($"Transferred {contact.ContactUsername} ({contact.ContactUserId})");
				if (!contactIdMapping.ContainsKey(contact.ContactUserId))
				{
					contactIdMapping.Add(contact.ContactUserId, originalContactId);
				}
			}
		}
		if (config.MigrateMessageHistory)
		{
			Status.Phase = "Message History";
			foreach (Contact item in contacts)
			{
				if (cancellationToken.IsCancellationRequested)
				{
					return;
				}
				if (ProcessContact(item))
				{
					if (!Status.MessagesFailed)
					{
						await TransferMessages(item, contactIdMapping[item.ContactUserId], cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					}
					Status.MigratedContactCount++;
				}
			}
		}
		Status.MigratedContactCount = Status.TotalContactCount;
	}

	public async Task TransferMessages(Contact contact, string contactId, CancellationToken cancellationToken)
	{
		DateTime value = (await target.GetLatestMessageTime(contact.ContactUserId).ConfigureAwait(continueOnCapturedContext: false)).AddMinutes(-10.0);
		SetProgressMessage($"Fetching messages from {contact.ContactUserId} from {value}...");
		int count = 0;
		await foreach (Message message in source.GetMessages(contactId, value).ConfigureAwait(continueOnCapturedContext: false))
		{
			if (!cancellationToken.IsCancellationRequested)
			{
				Status.CurrentlyMigratingItem = $"Message {message.Id} ({message.SendTime})";
				message.OtherUserId = contact.ContactUserId;
				await target.StoreMessage(message, source).ConfigureAwait(continueOnCapturedContext: false);
				Status.MigratedMessageCount++;
				count++;
				if (count % 100 == 0)
				{
					SetProgressMessage($"Transferred {count} messages...");
				}
				if (message.Content?.Contains("<SYSTEM MESSAGE>") ?? false)
				{
					Status.MessagesFailed = true;
				}
				continue;
			}
			return;
		}
		SetProgressMessage($"Transferred {count} messages.");
	}

	public async Task TransferMembers(Group group, GroupMigrationStatus groupStatus, CancellationToken cancellationToken)
	{
		SetProgressMessage("Transferring members for " + group.GroupId);
		List<MemberData> memberData = await source.GetMembers(group.GroupId).ConfigureAwait(continueOnCapturedContext: false);
		foreach (MemberData item in memberData)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return;
			}
			Status.CurrentlyMigratingItem = "Member " + item.member.UserId + " of " + group.Name;
			await target.StoreMember(group, item.member, item.storage, source).ConfigureAwait(continueOnCapturedContext: false);
			groupStatus.MigratedMemberCount++;
		}
		SetProgressMessage($"Transferred {memberData.Count} members.");
	}

	private void HandleRecordError(RecordMigrationStatus status, Record record, string error)
	{
		lock (status)
		{
			if (status.FailedRecords == null)
			{
				status.FailedRecords = new List<RecordMigrationFailure>();
			}
			status.FailedRecords.Add(new RecordMigrationFailure
			{
				RecordId = record.RecordId,
				OwnerId = record.OwnerId,
				RecordName = record.Name,
				RecordPath = record.Path,
				FailureReason = error
			});
		}
	}
}
public class CloudAccountDataStore : IAccountDataStore
{
	public readonly SkyFrostInterface Cloud;

	private static readonly HashSet<string> CONFLICTING_IDS = new HashSet<string> { "R-Home", "R-Settings" };

	private Dictionary<string, int> _fetchedRecords = new Dictionary<string, int>();

	public string MigrationId { get; set; }

	public bool PreserveOldHome { get; set; }

	public string Name => Cloud.UserAgentProduct + " " + Cloud.UserAgentVersion;

	public string UserId => Cloud.Session.CurrentUserID;

	public string Username => Cloud.Session.CurrentUsername;

	public int FetchedGroupCount { get; private set; }

	public IPlatformProfile PlatformProfile => Cloud.Platform;

	public event Action<string> ProgressMessage;

	public int FetchedRecordCount(string ownerId)
	{
		_fetchedRecords.TryGetValue(ownerId, out var value);
		return value;
	}

	public CloudAccountDataStore(SkyFrostInterface cloud)
	{
		Cloud = cloud;
	}

	public virtual async Task Prepare()
	{
		await Cloud.Groups.UpdateCurrentUserMemberships().ConfigureAwait(continueOnCapturedContext: false);
		List<Group> list = new List<Group>();
		Cloud.Groups.GetCurrentGroups(list);
		FetchedGroupCount = list.Where((Group g) => g.AdminUserId == Cloud.Session.CurrentUserID).Count();
	}

	public virtual async Task Complete()
	{
	}

	public virtual Task<User> GetUser()
	{
		return Task.FromResult(Cloud.CurrentUser);
	}

	public virtual async Task<List<ExitMessage>> GetExitMessages()
	{
		return (await Cloud.Users.GetExitMessages().ConfigureAwait(continueOnCapturedContext: false)).Entity;
	}

	public virtual async Task<List<PatreonFundingEvent>> GetPatreonFundingEvents()
	{
		return (await Cloud.Users.GetPatreonFundingEvents().ConfigureAwait(continueOnCapturedContext: false)).Entity;
	}

	public virtual async Task<List<Contact>> GetContacts()
	{
		CloudResult<List<Contact>> result = null;
		for (int attempt = 0; attempt < 10; attempt++)
		{
			result = await Cloud.Contacts.GetContacts().ConfigureAwait(continueOnCapturedContext: false);
			if (result.IsOK)
			{
				return result.Entity;
			}
			await Task.Delay(TimeSpan.FromSeconds((double)attempt * 1.5)).ConfigureAwait(continueOnCapturedContext: false);
		}
		throw new Exception("Could not fetch contacts after several attempts. Result: " + result);
	}

	public virtual async IAsyncEnumerable<Message> GetMessages(string contactId, DateTime? from)
	{
		DateTime start = from ?? new DateTime(2016, 1, 1);
		HashSet<string> processed = new HashSet<string>();
		while (true)
		{
			CloudResult<List<Message>> messagesResult = null;
			for (int attempt = 0; attempt < 10; attempt++)
			{
				messagesResult = await Cloud.Messages.GetMessages(start, 100, contactId, unreadOnly: false).ConfigureAwait(continueOnCapturedContext: false);
				if (messagesResult.State == HttpStatusCode.TooManyRequests)
				{
					await Task.Delay(TimeSpan.FromSeconds(attempt + 1)).ConfigureAwait(continueOnCapturedContext: false);
				}
				if (messagesResult.IsOK)
				{
					break;
				}
			}
			if (!messagesResult.IsOK)
			{
				yield return new Message
				{
					Content = "<SYSTEM MESSAGE>\nMessages failed to fetch from the source API and were not migrated. You can attempt another migration later to see if the issue was resolved. We recommend logging into your account on the source infrastructure first to ensure that messages are loading.",
					SenderId = contactId,
					RecipientId = Cloud.CurrentUserID,
					OwnerId = Cloud.CurrentUserID,
					MessageType = MessageType.Text,
					SendTime = DateTime.UtcNow,
					LastUpdateTime = DateTime.UtcNow
				};
				break;
			}
			List<Message> entity = messagesResult.Entity;
			entity?.RemoveAll((Message message) => processed.Contains(message.Id));
			if (entity == null || entity.Count == 0)
			{
				break;
			}
			foreach (Message m in entity)
			{
				yield return m;
				if (m.SendTime >= start)
				{
					start = m.SendTime;
				}
				processed.Add(m.Id);
			}
		}
	}

	public virtual async IAsyncEnumerable<GroupData> GetGroups()
	{
		foreach (Membership membership in (await Cloud.Groups.GetUserGroupMemeberships().ConfigureAwait(continueOnCapturedContext: false)).Entity)
		{
			CloudResult<Group> group = await Cloud.Groups.GetGroup(membership.GroupId).ConfigureAwait(continueOnCapturedContext: false);
			if (!(group.Entity.AdminUserId != Cloud.Session.CurrentUserID))
			{
				CloudResult<Storage> cloudResult = await Cloud.Storage.GetStorage(membership.GroupId).ConfigureAwait(continueOnCapturedContext: false);
				yield return new GroupData(group.Entity, cloudResult.Entity);
			}
		}
	}

	public virtual async Task<List<MemberData>> GetMembers(string groupId)
	{
		CloudResult<List<Member>> cloudResult = await Cloud.Groups.GetGroupMembers(groupId).ConfigureAwait(continueOnCapturedContext: false);
		List<MemberData> data = new List<MemberData>();
		foreach (Member member in cloudResult.Entity)
		{
			data.Add(new MemberData(member, (await Cloud.Storage.GetMemberStorage(groupId, member.UserId).ConfigureAwait(continueOnCapturedContext: false)).Entity));
		}
		return data;
	}

	public virtual async Task<Record> GetRecord(string ownerId, string recordId)
	{
		return (await Cloud.Records.GetRecord<Record>(ownerId, recordId).ConfigureAwait(continueOnCapturedContext: false)).Entity;
	}

	public virtual async IAsyncEnumerable<Record> GetRecords(string ownerId, DateTime? from, Action<string> searchProgressReport = null)
	{
		HashSet<string> fetchedRecords = new HashSet<string>();
		for (int i = 0; i < 2; i++)
		{
			SearchSortParameter sortParam = ((i / 2 == 0) ? SearchSortParameter.LastUpdateDate : SearchSortParameter.CreationDate);
			SearchSortDirection sortDir = (((i & 1) == 0) ? SearchSortDirection.Descending : SearchSortDirection.Ascending);
			if (from.HasValue && sortDir == SearchSortDirection.Ascending)
			{
				continue;
			}
			this.ProgressMessage?.Invoke($"Fetching records through search, phase {i}");
			searchProgressReport?.Invoke($"Fetching records through search, phase {i}");
			DateTime? dateLimit = null;
			HashSet<string> groupFetchedRecords = new HashSet<string>();
			bool empty;
			do
			{
				int fetchCount = 100;
				SearchParameters searchParameters = new SearchParameters();
				searchParameters.ByOwner = ownerId;
				searchParameters.Private = true;
				searchParameters.SortBy = sortParam;
				searchParameters.SortDirection = sortDir;
				if (from.HasValue)
				{
					searchParameters.MinDate = from;
				}
				if (sortDir == SearchSortDirection.Descending)
				{
					searchParameters.MaxDate = dateLimit;
				}
				else
				{
					searchParameters.MinDate = dateLimit;
				}
				RecordSearch<Record> search = new RecordSearch<Record>(searchParameters, Cloud, cache: false);
				DateTime? lastDate = null;
				bool flag;
				do
				{
					int startIndex = search.Records.Count;
					searchProgressReport?.Invoke($"Fetching records through search, phase {i}, Fetching count: {fetchCount} - {DateTime.UtcNow}, GroupFetched: {groupFetchedRecords.Count}");
					await search.EnsureResults(fetchCount, 25, throwOnError: true, 1000, TimeSpan.FromMinutes(5L)).ConfigureAwait(continueOnCapturedContext: false);
					fetchCount += 100;
					flag = false;
					for (int j = startIndex; j < search.Records.Count; j++)
					{
						Record record = search.Records[j];
						DateTime? dateTime = ((sortParam == SearchSortParameter.LastUpdateDate) ? new DateTime?(record.LastModificationTime) : record.CreationTime);
						if (!lastDate.HasValue)
						{
							lastDate = dateTime;
						}
						else if (lastDate != dateTime)
						{
							flag = true;
						}
					}
				}
				while (!flag && search.HasMoreResults);
				empty = true;
				searchProgressReport?.Invoke($"Fetching records through search, phase {i}, Processing count: {fetchCount} - {DateTime.UtcNow}, GroupFetched: {groupFetchedRecords.Count}");
				foreach (Record record2 in search.Records)
				{
					if (groupFetchedRecords.Add(record2.RecordId))
					{
						empty = false;
						dateLimit = ((!dateLimit.HasValue) ? new DateTime?(record2.LastModificationTime) : ((sortDir != SearchSortDirection.Descending) ? new DateTime?(MathX.Max(dateLimit.Value, record2.LastModificationTime)) : new DateTime?(MathX.Min(dateLimit.Value, record2.LastModificationTime))));
						if (fetchedRecords.Add(record2.RecordId))
						{
							_fetchedRecords[ownerId] = fetchedRecords.Count;
							yield return await FillRecordDetails(record2).ConfigureAwait(continueOnCapturedContext: false);
						}
					}
				}
			}
			while (!empty);
		}
		this.ProgressMessage?.Invoke("Fetching records recursively for " + ownerId);
		searchProgressReport?.Invoke($"Fetching records recursively for {ownerId} - {DateTime.UtcNow}");
		int index = 0;
		await foreach (Record item in Cloud.Records.GetRecordsInHierarchy<Record>(ownerId, "Inventory").ConfigureAwait(continueOnCapturedContext: false))
		{
			index++;
			searchProgressReport?.Invoke($"Fetching records recursively for {ownerId}. Processed: {index} - {DateTime.UtcNow}");
			if (fetchedRecords.Add(item.RecordId))
			{
				_fetchedRecords[ownerId] = fetchedRecords.Count;
				yield return await FillRecordDetails(item).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		this.ProgressMessage?.Invoke("Completed record fetch for " + ownerId);
		searchProgressReport?.Invoke($"Completed record fetch for {ownerId} - {DateTime.UtcNow}");
	}

	public IAsyncEnumerable<RecordAuditInfo> GetRecordAuditLog(string ownerId)
	{
		return Cloud.Records.EnumerateRecordAuditLog(ownerId);
	}

	private async Task<Record> FillRecordDetails(Record r)
	{
		string lastError = "Failed to get record after several attempts";
		for (int attempt = 0; attempt < 10; attempt++)
		{
			CloudResult<Record> cloudResult = await Cloud.Records.GetRecord<Record>(r.OwnerId, r.RecordId).ConfigureAwait(continueOnCapturedContext: false);
			if (cloudResult.Entity == null)
			{
				if (cloudResult.State == HttpStatusCode.NotFound)
				{
					break;
				}
				lastError = $"Could not get record: {r.OwnerId}:{r.RecordId}. Result: {cloudResult}";
				continue;
			}
			return cloudResult.Entity;
		}
		throw new Exception(lastError);
	}

	public virtual async Task<List<CloudVariableDefinition>> GetVariableDefinitions(string ownerId)
	{
		return (await Cloud.Variables.ListDefinitions(ownerId).ConfigureAwait(continueOnCapturedContext: false)).Entity;
	}

	public virtual async Task<List<CloudVariable>> GetVariables(string ownerId)
	{
		return (await Cloud.Variables.GetAllByOwner(ownerId).ConfigureAwait(continueOnCapturedContext: false)).Entity;
	}

	public virtual async Task<CloudVariable> GetVariable(string ownerId, string path)
	{
		return (await Cloud.Variables.Get(ownerId, path).ConfigureAwait(continueOnCapturedContext: false))?.Entity;
	}

	public virtual async Task<DateTime> GetLatestMessageTime(string contactId)
	{
		int delay = 50;
		CloudResult lastResult = null;
		for (int attempt = 0; attempt < 10; attempt++)
		{
			CloudResult<List<Message>> cloudResult = await Cloud.Messages.GetMessages(null, 1, contactId, unreadOnly: false).ConfigureAwait(continueOnCapturedContext: false);
			lastResult = cloudResult;
			if (cloudResult.IsOK)
			{
				if (cloudResult.Entity.Count > 0)
				{
					return cloudResult.Entity[0].LastUpdateTime;
				}
				return new DateTime(2016, 1, 1);
			}
			await Task.Delay(delay);
			delay *= 2;
		}
		throw new Exception($"Failed to determine latest message time after several attempts for contactId: {contactId}. Result: {lastResult}");
	}

	public virtual async Task<DateTime?> GetLatestRecordTime(string ownerId)
	{
		SearchParameters searchParameters = new SearchParameters();
		searchParameters.ByOwner = ownerId;
		searchParameters.Private = true;
		searchParameters.SortBy = SearchSortParameter.LastUpdateDate;
		searchParameters.SortDirection = SearchSortDirection.Descending;
		searchParameters.Count = 1;
		CloudResult<SearchResults<Record>> cloudResult = await Cloud.Records.FindRecords<Record>(searchParameters).ConfigureAwait(continueOnCapturedContext: false);
		if (cloudResult.IsOK && cloudResult.Entity.Records.Count == 1)
		{
			return cloudResult.Entity.Records[0].LastModificationTime;
		}
		return null;
	}

	public virtual async Task StoreDefinitions(List<CloudVariableDefinition> definitions, IAccountDataStore source)
	{
		foreach (CloudVariableDefinition definition in definitions)
		{
			if (IdUtil.GetOwnerType(definition.DefinitionOwnerId) == OwnerType.User)
			{
				definition.DefinitionOwnerId = UserId;
			}
			CloudResult<CloudVariableDefinition> value = await Cloud.Variables.UpsertDefinition(definition).ConfigureAwait(continueOnCapturedContext: false);
			this.ProgressMessage?.Invoke($"Stored variable definition {definition} - {value}");
		}
	}

	public virtual async Task StoreContact(Contact contact, IAccountDataStore source)
	{
		contact.OwnerId = UserId;
		if (await Cloud.Contacts.AddContact(contact))
		{
			this.ProgressMessage?.Invoke($"Stored contact {contact}");
			return;
		}
		throw new Exception("Failed to store contact: " + contact);
	}

	public virtual async Task StoreMessage(Message message, IAccountDataStore source)
	{
		message.OwnerId = UserId;
		CloudResult<Message> value = await Cloud.Messages.StoreMessage(message).ConfigureAwait(continueOnCapturedContext: false);
		this.ProgressMessage?.Invoke($"Stored message {message} - {value}");
	}

	public virtual async Task StoreVariables(List<CloudVariable> variables, IAccountDataStore source)
	{
		foreach (CloudVariable variable in variables)
		{
			if (IdUtil.GetOwnerType(variable.VariableOwnerId) == OwnerType.User)
			{
				variable.VariableOwnerId = UserId;
			}
			CloudResult value = await Cloud.Variables.Upsert(variable).ConfigureAwait(continueOnCapturedContext: false);
			this.ProgressMessage?.Invoke($"Upserted variable {variable.ToString()} - {value}");
		}
	}

	public virtual async Task StoreGroup(Group group, Storage storage, IAccountDataStore source)
	{
		if (!Cloud.Groups.IsCurrentUserMemberOfGroup(group.GroupId))
		{
			CloudResult<Group> cloudResult = await Cloud.Groups.CreateGroup(group).ConfigureAwait(continueOnCapturedContext: false);
			this.ProgressMessage?.Invoke($"Creating Group {group.Name} ({group.GroupId}) - " + cloudResult);
		}
	}

	public virtual async Task StoreMember(Group group, Member member, Storage storage, IAccountDataStore source)
	{
		CloudResult cloudResult = await Cloud.Groups.AddGroupMember(member, -1L).ConfigureAwait(continueOnCapturedContext: false);
		this.ProgressMessage?.Invoke($"Adding Group Member {member.UserId} to {group.Name} ({group.GroupId}) - " + cloudResult);
	}

	public virtual async Task StoreUserData(User user)
	{
		await Cloud.Profile.UpdateProfile(user.Profile).ConfigureAwait(continueOnCapturedContext: false);
	}

	public virtual Task StoreFundingEvent(PatreonFundingEvent fundingEvent)
	{
		return Task.CompletedTask;
	}

	public Task StoreRecordAudit(RecordAuditInfo auditInfo)
	{
		return Task.CompletedTask;
	}

	public Task StoreExitMessage(ExitMessage message)
	{
		return Task.CompletedTask;
	}

	public virtual async Task<StoreResultData> StoreRecord(Record record, IAccountDataStore source, RecordStatusCallbacks statusCallbacks, bool overwriteOnConflict)
	{
		statusCallbacks?.MigrationStarted(record.RecordId);
		try
		{
			if (IdUtil.GetOwnerType(record.OwnerId) == OwnerType.User)
			{
				record.OwnerId = UserId;
				record.OwnerName = Username;
			}
			RecordVersion sourceVersion = record.Version;
			if (record.MigrationMetadata == null)
			{
				record.MigrationMetadata = new MigrationMetadata
				{
					MigrationId = MigrationId,
					SourceVersion = sourceVersion,
					MigratedOn = DateTime.UtcNow,
					MigrationSource = source.Name?.Trim()
				};
				if (record.AssetManifest != null)
				{
					record.MigrationMetadata.AssetManifest = new List<DBAsset>(record.AssetManifest);
				}
			}
			record.Version = new RecordVersion(record.Version.GlobalVersion - 1, record.Version.LocalVersion, record.Version.LastModifyingUserId, record.Version.LastModifyingMachineId);
			if (CONFLICTING_IDS.Contains(record.RecordId))
			{
				CloudResult<Record> cloudResult = await Cloud.Records.GetRecord<Record>(record.OwnerId, record.RecordId).ConfigureAwait(continueOnCapturedContext: false);
				if (cloudResult.IsOK)
				{
					if (!overwriteOnConflict)
					{
						MigrationMetadata migrationMetadata = cloudResult.Entity.MigrationMetadata;
						if (migrationMetadata != null && migrationMetadata.TargetVersion.HasValue && !cloudResult.Entity.Version.IsSameVersion(cloudResult.Entity.MigrationMetadata.TargetVersion.Value))
						{
							return new StoreResultData(RecordStoreResult.Conflict);
						}
					}
					record.Version = cloudResult.Entity.Version;
					record.IncrementLocalVersion("MIGRATION", record.Version.LastModifyingUserId);
					record.MigrationMetadata.PreviousMigration = cloudResult.Entity.MigrationMetadata;
					record.MigrationMetadata.TargetVersion = cloudResult.Entity.Version;
				}
			}
			if (record.RecordId == "R-Home")
			{
				record.RecordId = "R-" + source.PlatformProfile.Abbreviation + "-Home";
				if (PreserveOldHome)
				{
					CloudVariable cloudVariable = new CloudVariable();
					cloudVariable.VariableOwnerId = record.OwnerId;
					cloudVariable.Path = Cloud.Platform.FavoriteVariable(FavoriteEntity.Home);
					cloudVariable.Value = Cloud.Platform.GetRecordUri(record).ToString();
					await StoreVariables(new List<CloudVariable> { cloudVariable }, source).ConfigureAwait(continueOnCapturedContext: false);
				}
			}
			if (record.AssetManifest != null)
			{
				foreach (DBAsset item in record.AssetManifest)
				{
					if (item.Bytes == 0L)
					{
						DBAsset dBAsset = item;
						dBAsset.Bytes = await source.GetAssetSize(item.Hash).ConfigureAwait(continueOnCapturedContext: false);
					}
				}
				foreach (DBAsset item2 in record.AssetManifest)
				{
					if (item2.Bytes == 0L)
					{
						statusCallbacks?.AssetMissing?.Invoke(item2.Hash);
					}
				}
				record.AssetManifest.RemoveAll((DBAsset a) => a.Bytes == 0);
			}
			else if (record.RecordType == "object" || record.RecordType == "world")
			{
				statusCallbacks?.AssetMissing?.Invoke($"AssetManifestMissing for: {record}");
			}
			string failReason = null;
			for (int attempt = 0; attempt < 5; attempt++)
			{
				AccountDataStoreUploadTask task = new AccountDataStoreUploadTask(Cloud, source, record);
				if (overwriteOnConflict)
				{
					task.ForceConflictSync = true;
					if (!CONFLICTING_IDS.Contains(record.RecordId))
					{
						CloudResult<Record> cloudResult2 = await Cloud.Records.GetRecord<Record>(record.OwnerId, record.RecordId).ConfigureAwait(continueOnCapturedContext: false);
						if (cloudResult2.IsOK)
						{
							record.Version = cloudResult2.Entity.Version;
						}
					}
				}
				task.AssetToUploadAdded += statusCallbacks.AssetToUploadAdded;
				task.BytesUploadedAdded += statusCallbacks.BytesUploaded;
				task.AssetUploaded += statusCallbacks.AssetUploaded;
				task.AssetMissing += statusCallbacks.AssetMissing;
				this.ProgressMessage?.Invoke($"Syncing record {record}...");
				try
				{
					Console.WriteLine("Running Record upload for: " + record);
					await task.RunUpload(CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
					if (task.Failed)
					{
						this.ProgressMessage?.Invoke($"Sync failed (attempt {attempt}): {task.FailReason}");
						failReason = task.FailReason + $"\nSourceVersion: {sourceVersion}";
						if (!task.FailReason.Contains("Conflict") && !task.FailReason.Contains("Out of space"))
						{
							await Task.Delay(TimeSpan.FromSeconds(attempt * 2)).ConfigureAwait(continueOnCapturedContext: false);
							continue;
						}
						break;
					}
					if (task.WasAlreadySynced)
					{
						this.ProgressMessage?.Invoke("Already synced.");
						return new StoreResultData(RecordStoreResult.AlreadyExists);
					}
					this.ProgressMessage?.Invoke("Sync succeeded.");
					return new StoreResultData(RecordStoreResult.Stored);
				}
				catch (Exception ex)
				{
					throw new Exception($"Exception when uploading record: {record.CombinedRecordId} ({record.Name}). SyncStage: {task.StageDescription}\n" + ex, ex);
				}
			}
			return new StoreResultData(RecordStoreResult.Error, failReason ?? "UNKNOWN REASON");
		}
		finally
		{
			statusCallbacks?.MigrationFinished(record.RecordId);
		}
	}

	public virtual async Task DownloadAsset(string hash, string targetPath)
	{
		WebClient webClient = new WebClient();
		try
		{
			webClient.Proxy = Cloud.Proxy;
			await webClient.DownloadFileTaskAsync(Cloud.Assets.DBToHttp(new Uri(Cloud.Assets.DBScheme + ":///" + hash), DB_Endpoint.Default), targetPath);
		}
		finally
		{
			((IDisposable)webClient)?.Dispose();
		}
	}

	public virtual async Task<long> GetAssetSize(string hash)
	{
		CloudResult<AssetInfo> cloudResult = await Cloud.Assets.GetGlobalAssetInfo(hash).ConfigureAwait(continueOnCapturedContext: false);
		if (cloudResult.IsOK)
		{
			return cloudResult.Entity.Bytes;
		}
		return 0L;
	}

	public virtual async Task<string> GetAsset(string hash)
	{
		string tempPath = Path.GetTempFileName();
		await DownloadAsset(hash, tempPath).ConfigureAwait(continueOnCapturedContext: false);
		return tempPath;
	}

	public virtual Task<AssetData> ReadAsset(string hash)
	{
		return Task.FromResult((AssetData)Cloud.Assets.DBToHttp(new Uri(Cloud.Assets.DBScheme + ":///" + hash), DB_Endpoint.Default));
	}
}
public enum RecordStoreResult
{
	Stored,
	AlreadyExists,
	Conflict,
	Ignored,
	Error
}
public readonly struct StoreResultData
{
	public readonly RecordStoreResult result;

	public readonly string error;

	public StoreResultData(RecordStoreResult result, string error = null)
	{
		this.result = result;
		this.error = error;
	}
}
public readonly struct GroupData
{
	public readonly Group group;

	public readonly Storage storage;

	public GroupData(Group group, Storage storage)
	{
		this.group = group;
		this.storage = storage;
	}
}
public readonly struct MemberData
{
	public readonly Member member;

	public readonly Storage storage;

	public MemberData(Member member, Storage storage)
	{
		this.member = member;
		this.storage = storage;
	}
}
public class RecordStatusCallbacks
{
	public Action<AssetDiff> AssetToUploadAdded;

	public Action<long> BytesUploaded;

	public Action AssetUploaded;

	public Action<string> AssetMissing;

	public Action<string> MigrationStarted;

	public Action<string> MigrationFinished;
}
public interface IAccountDataStore
{
	IPlatformProfile PlatformProfile { get; }

	string MigrationId { get; set; }

	string Name { get; }

	string UserId { get; }

	string Username { get; }

	int FetchedGroupCount { get; }

	event Action<string> ProgressMessage;

	int FetchedRecordCount(string ownerId);

	Task Prepare();

	Task Complete();

	Task<long> GetAssetSize(string hash);

	Task DownloadAsset(string hash, string targetPath);

	Task<string> GetAsset(string hash);

	Task<AssetData> ReadAsset(string hash);

	Task<User> GetUser();

	Task<List<ExitMessage>> GetExitMessages();

	Task<List<PatreonFundingEvent>> GetPatreonFundingEvents();

	Task<List<CloudVariableDefinition>> GetVariableDefinitions(string ownerId);

	Task<CloudVariable> GetVariable(string ownerId, string path);

	Task<List<CloudVariable>> GetVariables(string ownerId);

	IAsyncEnumerable<GroupData> GetGroups();

	Task<List<MemberData>> GetMembers(string groupId);

	Task<Record> GetRecord(string ownerId, string recordId);

	IAsyncEnumerable<Record> GetRecords(string ownerId, DateTime? from, Action<string> searchProgressReport = null);

	IAsyncEnumerable<RecordAuditInfo> GetRecordAuditLog(string ownerId);

	Task<List<Contact>> GetContacts();

	IAsyncEnumerable<Message> GetMessages(string contactId, DateTime? from);

	Task<DateTime?> GetLatestRecordTime(string ownerId);

	Task<DateTime> GetLatestMessageTime(string contactId);

	Task StoreUserData(User user);

	Task StoreExitMessage(ExitMessage exitMessage);

	Task StoreFundingEvent(PatreonFundingEvent fundingEvent);

	Task StoreDefinitions(List<CloudVariableDefinition> definition, IAccountDataStore source);

	Task StoreVariables(List<CloudVariable> variable, IAccountDataStore source);

	Task StoreGroup(Group group, Storage storage, IAccountDataStore source);

	Task StoreMember(Group group, Member member, Storage storage, IAccountDataStore source);

	Task<StoreResultData> StoreRecord(Record record, IAccountDataStore source, RecordStatusCallbacks statusCallbacks = null, bool forceConflictOverwrite = false);

	Task StoreRecordAudit(RecordAuditInfo info);

	Task StoreContact(Contact contact, IAccountDataStore source);

	Task StoreMessage(Message message, IAccountDataStore source);
}
public class LocalAccountDataStore : IAccountDataStore
{
	private readonly struct AssetJob
	{
		public readonly string hash;

		public readonly IAccountDataStore source;

		public AssetJob(string hash, IAccountDataStore source)
		{
			this.hash = hash;
			this.source = source;
		}
	}

	private ActionBlock<AssetJob> downloadProcessor;

	private HashSet<string> scheduledAssets = new HashSet<string>();

	public readonly string BasePath;

	public readonly string AssetsPath;

	private Dictionary<string, int> _fetchedRecords = new Dictionary<string, int>();

	public string MigrationId { get; set; }

	public string Name => "Local Data Store";

	public string UserId { get; private set; }

	public string Username { get; private set; }

	public int FetchedGroupCount { get; private set; }

	public IPlatformProfile PlatformProfile { get; private set; }

	public event Action<string> ProgressMessage;

	public int FetchedRecordCount(string ownerId)
	{
		_fetchedRecords.TryGetValue(ownerId, out var value);
		return value;
	}

	public LocalAccountDataStore(IPlatformProfile platform, string userId, string basePath, string assetsPath)
	{
		PlatformProfile = platform;
		UserId = userId;
		BasePath = basePath;
		AssetsPath = assetsPath;
		InitDownloadProcessor();
	}

	public Task Prepare()
	{
		return Task.CompletedTask;
	}

	public async Task Complete()
	{
		downloadProcessor.Complete();
		await downloadProcessor.Completion.ConfigureAwait(continueOnCapturedContext: false);
		InitDownloadProcessor();
	}

	private void InitDownloadProcessor()
	{
		Directory.CreateDirectory(AssetsPath);
		downloadProcessor = new ActionBlock<AssetJob>(async delegate(AssetJob job)
		{
			string assetPath = GetAssetPath(job.hash);
			if (File.Exists(assetPath))
			{
				return;
			}
			try
			{
				this.ProgressMessage?.Invoke("Downloading asset " + job.hash);
				await job.source.DownloadAsset(job.hash, assetPath).ConfigureAwait(continueOnCapturedContext: false);
				this.ProgressMessage?.Invoke("Finished download " + job.hash);
			}
			catch (Exception ex)
			{
				this.ProgressMessage?.Invoke("Exception " + job.hash + ": " + ex);
			}
		});
	}

	public Task<List<Contact>> GetContacts()
	{
		return GetEntities<Contact>(ContactsPath(UserId));
	}

	public Task<List<ExitMessage>> GetExitMessages()
	{
		return GetEntities<ExitMessage>(ExitMessagesPath());
	}

	public async IAsyncEnumerable<RecordAuditInfo> GetRecordAuditLog(string ownerId)
	{
		foreach (RecordAuditInfo item in await GetEntities<RecordAuditInfo>(RecordAuditPath(ownerId)))
		{
			yield return item;
		}
	}

	public async Task<User> GetUser()
	{
		return GetEntity<User>(Path.Combine(UserPath(), "User"));
	}

	public async Task<List<PatreonFundingEvent>> GetPatreonFundingEvents()
	{
		return await GetEntities<PatreonFundingEvent>(PatreonPath());
	}

	public async IAsyncEnumerable<Message> GetMessages(string contactId, DateTime? from)
	{
		foreach (Message item in await GetEntities<Message>(MessagesPath(UserId, contactId)).ConfigureAwait(continueOnCapturedContext: false))
		{
			if (!from.HasValue || !(item.LastUpdateTime < from.Value))
			{
				yield return item;
			}
		}
	}

	public async Task<Record> GetRecord(string ownerId, string recordId)
	{
		return GetEntity<Record>(Path.Combine(RecordsPath(ownerId), recordId));
	}

	public async IAsyncEnumerable<Record> GetRecords(string ownerId, DateTime? from, Action<string> searchProgressReport = null)
	{
		List<Record> list = await GetEntities<Record>(RecordsPath(ownerId)).ConfigureAwait(continueOnCapturedContext: false);
		_fetchedRecords[ownerId] = list.Count;
		foreach (Record item in list)
		{
			if (!from.HasValue || !(item.LastModificationTime < from.Value))
			{
				yield return item;
			}
		}
	}

	public Task<List<CloudVariableDefinition>> GetVariableDefinitions(string ownerId)
	{
		return GetEntities<CloudVariableDefinition>(VariableDefinitionPath(ownerId));
	}

	public Task<List<CloudVariable>> GetVariables(string ownerId)
	{
		return GetEntities<CloudVariable>(VariablePath(ownerId));
	}

	public async Task<CloudVariable> GetVariable(string ownerId, string path)
	{
		return GetEntity<CloudVariable>(Path.Combine(VariablePath(ownerId), path));
	}

	public async IAsyncEnumerable<GroupData> GetGroups()
	{
		string path = GroupsPath(UserId);
		List<Group> list = await GetEntities<Group>(path).ConfigureAwait(continueOnCapturedContext: false);
		FetchedGroupCount = list.Count;
		foreach (Group item in list)
		{
			Storage entity = GetEntity<Storage>(Path.Combine(path, item.GroupId + ".Storage"));
			yield return new GroupData(item, entity);
		}
	}

	public async Task<List<MemberData>> GetMembers(string groupId)
	{
		string path = MembersPath(UserId, groupId);
		List<Member> obj = await GetEntities<Member>(path).ConfigureAwait(continueOnCapturedContext: false);
		List<MemberData> list = new List<MemberData>();
		foreach (Member item in obj)
		{
			Storage entity = GetEntity<Storage>(Path.Combine(path, item.UserId + ".Storage"));
			list.Add(new MemberData(item, entity));
		}
		return list;
	}

	private Task<List<T>> GetEntities<T>(string path)
	{
		List<T> list = new List<T>();
		if (Directory.Exists(path))
		{
			foreach (string item2 in Directory.EnumerateFiles(path, "*.json"))
			{
				T item = System.Text.Json.JsonSerializer.Deserialize<T>(File.ReadAllText(item2));
				list.Add(item);
			}
		}
		return Task.FromResult(list);
	}

	private T GetEntity<T>(string path)
	{
		path += ".json";
		if (File.Exists(path))
		{
			return System.Text.Json.JsonSerializer.Deserialize<T>(File.ReadAllText(path));
		}
		return default(T);
	}

	public async Task StoreDefinitions(List<CloudVariableDefinition> definitions, IAccountDataStore source)
	{
		foreach (CloudVariableDefinition definition in definitions)
		{
			await StoreEntity(definition, Path.Combine(VariableDefinitionPath(definition.DefinitionOwnerId), definition.Subpath)).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	public async Task StoreVariables(List<CloudVariable> variables, IAccountDataStore source)
	{
		foreach (CloudVariable variable in variables)
		{
			await StoreEntity(variable, Path.Combine(VariablePath(variable.VariableOwnerId), variable.Path)).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	public Task StoreContact(Contact contact, IAccountDataStore source)
	{
		return StoreEntity(contact, Path.Combine(ContactsPath(contact.OwnerId), contact.ContactUserId));
	}

	public Task StoreMessage(Message message, IAccountDataStore source)
	{
		return StoreEntity(message, Path.Combine(MessagesPath(message.OwnerId, message.OtherUserId), message.Id));
	}

	public async Task<StoreResultData> StoreRecord(Record record, IAccountDataStore source, RecordStatusCallbacks statusCallbacks, bool overwriteOnConflict)
	{
		await StoreEntity(record, Path.Combine(RecordsPath(record.OwnerId), record.RecordId)).ConfigureAwait(continueOnCapturedContext: false);
		if (record.AssetManifest != null)
		{
			foreach (DBAsset item in record.AssetManifest)
			{
				ScheduleAsset(item.Hash, source);
			}
		}
		return new StoreResultData(RecordStoreResult.Stored);
	}

	public async Task StoreGroup(Group group, Storage storage, IAccountDataStore source)
	{
		string path = Path.Combine(GroupsPath(group.AdminUserId), group.GroupId);
		await StoreEntity(group, path);
		await StoreEntity(storage, path + ".Storage");
	}

	public async Task StoreMember(Group group, Member member, Storage storage, IAccountDataStore source)
	{
		string path = Path.Combine(MembersPath(group.AdminUserId, member.GroupId), member.UserId);
		await StoreEntity(member, path);
		await StoreEntity(storage, path + ".Storage");
	}

	public async Task StoreRecordAudit(RecordAuditInfo info)
	{
		string path = Path.Combine(RecordAuditPath(info.OwnerId), info.Timestamp.ToString("s").Replace(":", "-"));
		await StoreEntity(info, path);
	}

	public async Task StoreUserData(User user)
	{
		string path = Path.Combine(UserPath(), "User");
		await StoreEntity(user, path);
	}

	public async Task StoreFundingEvent(PatreonFundingEvent fundingEvent)
	{
		string path = Path.Combine(PatreonPath(), fundingEvent.Id);
		await StoreEntity(fundingEvent, path);
	}

	public async Task StoreExitMessage(ExitMessage exitMessage)
	{
		string path = Path.Combine(ExitMessagesPath(), "ExitMessage" + exitMessage.MessageIndex);
		await StoreEntity(exitMessage, path);
	}

	private Task StoreEntity<T>(T entity, string path)
	{
		string directoryName = Path.GetDirectoryName(path);
		if (!Directory.Exists(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		string contents = System.Text.Json.JsonSerializer.Serialize(entity);
		File.WriteAllText(path + ".json", contents);
		return Task.CompletedTask;
	}

	private string UserPath()
	{
		return Path.Combine(BasePath, UserId ?? "UNKNOWN_USER", "UserData");
	}

	private string PatreonPath()
	{
		return Path.Combine(UserPath(), "Patreon");
	}

	private string ExitMessagesPath()
	{
		return Path.Combine(UserPath(), "ExitMessages");
	}

	private string VariableDefinitionPath(string ownerId)
	{
		return Path.Combine(BasePath, ownerId, "VariableDefinitions");
	}

	private string VariablePath(string ownerId)
	{
		return Path.Combine(BasePath, ownerId, "Variables");
	}

	private string ContactsPath(string ownerId)
	{
		return Path.Combine(BasePath, ownerId, "Contacts");
	}

	private string MessagesPath(string ownerId, string contactId)
	{
		return Path.Combine(BasePath, ownerId, "Messages", contactId);
	}

	private string RecordsPath(string ownerId)
	{
		return Path.Combine(BasePath, ownerId, "Records");
	}

	private string RecordAuditPath(string ownerId)
	{
		return Path.Combine(BasePath, ownerId, "RecordAudit");
	}

	private string GroupsPath(string ownerId)
	{
		return Path.Combine(BasePath, ownerId, "Groups");
	}

	private string MembersPath(string ownerId, string groupId)
	{
		return Path.Combine(BasePath, ownerId, "GroupMembers", groupId);
	}

	private string GetAssetPath(string hash)
	{
		return Path.Combine(AssetsPath, hash);
	}

	public async Task<DateTime> GetLatestMessageTime(string contactId)
	{
		DateTime latest = new DateTime(2016, 1, 1);
		await foreach (Message item in GetMessages(contactId, null).ConfigureAwait(continueOnCapturedContext: false))
		{
			if (item.LastUpdateTime > latest)
			{
				latest = item.LastUpdateTime;
			}
		}
		return latest;
	}

	public async Task<DateTime?> GetLatestRecordTime(string ownerId)
	{
		DateTime? latest = null;
		await foreach (Record item in GetRecords(ownerId, null).ConfigureAwait(continueOnCapturedContext: false))
		{
			if (latest.HasValue)
			{
				DateTime lastModificationTime = item.LastModificationTime;
				DateTime? dateTime = latest;
				if (!(lastModificationTime > dateTime))
				{
					continue;
				}
			}
			latest = item.LastModificationTime;
		}
		return latest;
	}

	private void ScheduleAsset(string hash, IAccountDataStore store)
	{
		if (scheduledAssets.Add(hash))
		{
			AssetJob item = new AssetJob(hash, store);
			downloadProcessor.Post(item);
		}
	}

	public Task DownloadAsset(string hash, string targetPath)
	{
		return Task.Run(delegate
		{
			File.Copy(GetAssetPath(hash), targetPath);
		});
	}

	public Task<long> GetAssetSize(string hash)
	{
		if (File.Exists(GetAssetPath(hash)))
		{
			return Task.FromResult(new FileInfo(GetAssetPath(hash)).Length);
		}
		return Task.FromResult(0L);
	}

	public Task<string> GetAsset(string hash)
	{
		return Task.FromResult(GetAssetPath(hash));
	}

	public Task<AssetData> ReadAsset(string hash)
	{
		return Task.FromResult((AssetData)File.OpenRead(GetAssetPath(hash)));
	}
}
public static class MigrationHelper
{
	private readonly struct MigrationGroup : IEquatable<MigrationGroup>
	{
		public readonly IPlatformProfile from;

		public readonly IPlatformProfile to;

		public MigrationGroup(IPlatformProfile from, IPlatformProfile to)
		{
			this.from = from;
			this.to = to;
		}

		public bool Equals(MigrationGroup other)
		{
			if (from == other.from)
			{
				return to == other.to;
			}
			return false;
		}

		public override bool Equals(object obj)
		{
			if (obj is MigrationGroup other)
			{
				return Equals(other);
			}
			return false;
		}

		public override int GetHashCode()
		{
			return (-1951484959 * -1521134295 + EqualityComparer<IPlatformProfile>.Default.GetHashCode(from)) * -1521134295 + EqualityComparer<IPlatformProfile>.Default.GetHashCode(to);
		}

		public static bool operator ==(MigrationGroup left, MigrationGroup right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(MigrationGroup left, MigrationGroup right)
		{
			return !(left == right);
		}
	}

	public static HashSet<string> AllowedHosts = new HashSet<string> { "cloudx.azurewebsites.net", "cloudxstorage.blob.core.windows.net", "api.resonite.com" };

	private static ConcurrentDictionary<MigrationGroup, SortedDictionary<string, string>> migrationStrings = new ConcurrentDictionary<MigrationGroup, SortedDictionary<string, string>>();

	public static bool IsAllowedSource(SkyFrostConfig config)
	{
		if (!Uri.TryCreate(config.ApiEndpoint, UriKind.Absolute, out Uri result))
		{
			return false;
		}
		if (!Uri.TryCreate(config.SignalREndpoint, UriKind.Absolute, out Uri result2))
		{
			return false;
		}
		if (config.ProxyConfig != null)
		{
			return false;
		}
		if (!AllowedHosts.Contains(result.Host))
		{
			return false;
		}
		if (!AllowedHosts.Contains(result2.Host))
		{
			return false;
		}
		return true;
	}

	private static SortedDictionary<string, string> GetMigrationStrings(IPlatformProfile from, IPlatformProfile to)
	{
		MigrationGroup key = new MigrationGroup(from, to);
		if (migrationStrings.TryGetValue(key, out var value))
		{
			return value;
		}
		value = new SortedDictionary<string, string>(Comparer<string>.Create(delegate(string a, string b)
		{
			int num = -a.Length.CompareTo(b.Length);
			return (num != 0) ? num : a.CompareTo(b);
		}));
		value.Add(from.Name, to.Name);
		if (from.ShortNamePrefix != from.Name)
		{
			value.Add(from.ShortNamePrefix, to.ShortNamePrefix);
		}
		value.Add(from.Abbreviation, to.Abbreviation);
		value.Add(from.GroupId, to.GroupId);
		value.Add(from.TeamGroupId, to.TeamGroupId);
		value.Add(from.ComputeGroupId, to.ComputeGroupId);
		if (from.AppUsername != from.Name)
		{
			value.Add(from.AppUsername, to.AppUsername);
		}
		value.Add(from.DevBotUsername, to.DevBotUsername);
		value.Add(from.AppUserId, to.AppUserId);
		value.Add(from.DevBotUserId, to.DevBotUserId);
		value.Add(from.AppScheme, to.AppScheme);
		value.Add(from.DBScheme, to.DBScheme);
		value.Add(from.SessionScheme, to.SessionScheme);
		value.Add(from.RecordScheme, to.RecordScheme);
		value.Add(from.UserSessionScheme, to.UserSessionScheme);
		migrationStrings.TryAdd(key, value);
		return value;
	}

	public static Uri MigrateLegacyURL(this Uri url, IPlatformProfile to)
	{
		if (url == null)
		{
			return null;
		}
		foreach (IPlatformProfile legacyProfile in PlatformProfile.LegacyProfiles)
		{
			Uri uri = url.MigrateURL(legacyProfile, to);
			if (uri != url)
			{
				return uri;
			}
		}
		return url;
	}

	public static Uri MigrateURL(this Uri url, IPlatformProfile from, IPlatformProfile to)
	{
		if (url == null)
		{
			return null;
		}
		if (url.Scheme == from.DBScheme)
		{
			return new Uri(url.OriginalString.Replace(from.DBScheme + ":", to.DBScheme + ":"));
		}
		if (url.Scheme == from.RecordScheme)
		{
			return new Uri(url.OriginalString.Replace(from.RecordScheme + ":", to.RecordScheme + ":"));
		}
		if (url.Scheme == from.SessionScheme)
		{
			return new Uri(url.OriginalString.Replace(from.SessionScheme + ":", to.SessionScheme + ":"));
		}
		if (url.Scheme == from.UserSessionScheme)
		{
			return new Uri(url.OriginalString.Replace(from.UserSessionScheme + ":", to.UserSessionScheme + ":"));
		}
		return url;
	}

	public static string MigrateURL(this string str, IPlatformProfile from, IPlatformProfile to)
	{
		if (str == null)
		{
			return null;
		}
		if (Uri.TryCreate(str, UriKind.Absolute, out Uri result))
		{
			if (result.Scheme == from.DBScheme)
			{
				return str.Replace(from.DBScheme, to.DBScheme);
			}
			if (result.Scheme == from.SessionScheme)
			{
				return str.Replace(from.SessionScheme, to.SessionScheme);
			}
			if (result.Scheme == from.RecordScheme)
			{
				return str.Replace(from.RecordScheme, to.RecordScheme);
			}
		}
		return str;
	}

	public static string MigrateString(this string str, IPlatformProfile from, IPlatformProfile to)
	{
		if (str == null)
		{
			return null;
		}
		if (GetMigrationStrings(from, to).TryGetValue(str, out var value))
		{
			return value;
		}
		return str;
	}

	public static string MigrateSubStrings(this string str, IPlatformProfile from, IPlatformProfile to)
	{
		if (str == null)
		{
			return null;
		}
		foreach (KeyValuePair<string, string> migrationString in GetMigrationStrings(from, to))
		{
			str = str.Replace(migrationString.Key, migrationString.Value);
		}
		return str;
	}
}
[JsonDerivedType(typeof(CloudflareAssetInterface), "cloudflare")]
[JsonDerivedType(typeof(AzureAssetInterface), "azure")]
public abstract class AssetInterface
{
	[System.Text.Json.Serialization.JsonIgnore]
	public SkyFrostInterface Cloud { get; private set; }

	[System.Text.Json.Serialization.JsonIgnore]
	public ApiClient Api => Cloud.Api;

	[System.Text.Json.Serialization.JsonIgnore]
	public User CurrentUser => Cloud.Session.CurrentUser;

	[System.Text.Json.Serialization.JsonIgnore]
	public string CurrentUserID => Cloud.Session.CurrentUserID;

	[System.Text.Json.Serialization.JsonIgnore]
	public string DBScheme => Cloud.Platform.DBScheme;

	public void Initialize(SkyFrostInterface cloud)
	{
		if (Cloud != null)
		{
			throw new InvalidOperationException("AssetBackend has already been initialized");
		}
		Cloud = cloud;
	}

	public abstract Uri DBToHttp(Uri productDBUri, DB_Endpoint endpoint);

	public abstract Uri ThumbnailToHttp(ThumbnailInfo thumbnail);

	public async Task<Stream> GatherAsset(string signature)
	{
		_ = 1;
		try
		{
			HttpRequestMessage request = Api.CreateRequest(DBToHttp(GenerateURL(signature), DB_Endpoint.Default), authenticate: false, HttpMethod.Get);
			CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(30L));
			return await (await Api.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationTokenSource.Token).ConfigureAwait(continueOnCapturedContext: false)).Content.ReadAsStreamAsync().ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (HttpRequestException)
		{
			return null;
		}
	}

	public Uri FilterDatabaseURL(Uri assetURL)
	{
		if (assetURL.Scheme == DBScheme && assetURL.Segments.Length >= 2 && assetURL.Segments[1].Contains("."))
		{
			assetURL = new Uri(DBScheme + ":///" + Path.GetFileNameWithoutExtension(assetURL.Segments[1]) + assetURL.Query);
		}
		return assetURL;
	}

	public Uri GenerateURL(string signature)
	{
		return GenerateURLWithExtension(signature, null);
	}

	public Uri GenerateURLWithExtension(string signature, string extension)
	{
		if (!string.IsNullOrEmpty(extension) && extension[0] != '.')
		{
			extension = "." + extension;
		}
		return new Uri(DBScheme + ":///" + signature + extension);
	}

	public string DBSignature(Uri dbUri, bool ignoreScheme = false)
	{
		string extension;
		return DBSignature(dbUri, out extension, ignoreScheme);
	}

	public string DBSignature(Uri dbUri, out string extension, bool ignoreScheme = false)
	{
		if (!ignoreScheme && dbUri.Scheme != DBScheme)
		{
			throw new ArgumentException($"{dbUri} is not a Database URI");
		}
		if (dbUri.Segments.Length < 2)
		{
			extension = null;
			return null;
		}
		string path = dbUri.Segments[1];
		extension = Path.GetExtension(path);
		string text = Path.GetFileNameWithoutExtension(path);
		if (text.Length < 30)
		{
			text = LegacyAssetMap.MapLegacySignature(text);
		}
		return text;
	}

	public string DBQuery(Uri dbUri)
	{
		if (string.IsNullOrWhiteSpace(dbUri.Query))
		{
			return null;
		}
		return dbUri.Query.Substring(1);
	}

	public string DBFilename(Uri dbUri)
	{
		if (dbUri.Segments.Length < 2)
		{
			return null;
		}
		return dbUri.Segments[1] + dbUri.Query;
	}

	public bool IsValidDBUri(string uri)
	{
		if (string.IsNullOrWhiteSpace(uri))
		{
			return false;
		}
		if (!Uri.TryCreate(uri, UriKind.Absolute, out Uri result))
		{
			return false;
		}
		return IsValidDBUri(result);
	}

	public bool IsValidDBUri(Uri uri)
	{
		if (uri.Scheme != DBScheme)
		{
			return false;
		}
		if (uri.Segments.Length < 2)
		{
			return false;
		}
		return true;
	}

	public bool IsLegacyDB(Uri uri)
	{
		if (uri.Scheme != DBScheme)
		{
			return false;
		}
		if (uri.Segments.Length < 2)
		{
			return false;
		}
		return Path.GetFileNameWithoutExtension(uri.Segments[1]).Length < 30;
	}

	public Task<CloudResult<AssetInfo>> GetGlobalAssetInfo(string hash)
	{
		return Api.GET<AssetInfo>("assets/" + hash.ToLower());
	}

	public Task<CloudResult<AssetInfo>> GetUserAssetInfo(string hash)
	{
		return GetAssetInfo(CurrentUserID, hash);
	}

	public Task<CloudResult<AssetInfo>> GetAssetInfo(string ownerId, string hash)
	{
		return IdUtil.GetOwnerType(ownerId) switch
		{
			OwnerType.User => Api.GET<AssetInfo>("users/" + ownerId + "/assets/" + hash), 
			OwnerType.Group => Api.GET<AssetInfo>("groups/" + ownerId + "/assets/" + hash), 
			_ => throw new Exception("Invalid ownerId"), 
		};
	}

	public async Task<bool> IsValidShader(string hash)
	{
		if ((await Api.GET("shaders/" + hash).ConfigureAwait(continueOnCapturedContext: false)).IsOK)
		{
			return true;
		}
		return false;
	}

	public AssetUploadTask CreateFileAssetUploadTask(string ownerId, string signature, string variant, string assetPath, IProgressIndicator progress = null, int retries = 5)
	{
		AssetUploadTask assetUploadTask = CreateEmptyAssetUploadTask();
		assetUploadTask.InitializeWithFile(Cloud, ownerId, signature, variant, assetPath, progress, retries);
		return assetUploadTask;
	}

	public AssetUploadTask CreateStreamAssetUploadTask(string ownerId, string signature, string variant, Stream assetStream, IProgressIndicator progress = null, long? bytes = null, int retries = 5)
	{
		AssetUploadTask assetUploadTask = CreateEmptyAssetUploadTask();
		assetUploadTask.InitializeWithStream(Cloud, ownerId, signature, variant, assetStream, progress, bytes, retries);
		return assetUploadTask;
	}

	public AssetUploadTask CreateURLAssetUploadTask(string ownerId, string signature, string variant, Uri assetURL, IProgressIndicator progress = null, long? bytes = null, int retries = 5)
	{
		AssetUploadTask assetUploadTask = CreateEmptyAssetUploadTask();
		assetUploadTask.InitializeWithURL(Cloud, ownerId, signature, variant, assetURL, progress, bytes, retries);
		return assetUploadTask;
	}

	public Task<CloudResult> GetAssetMime(Uri url)
	{
		return GetAssetMime(DBSignature(url));
	}

	public abstract Task<CloudResult> GetAssetMime(string hash);

	public abstract Task<CloudResult<ThumbnailInfo>> UploadThumbnail(string path, string session);

	protected abstract AssetUploadTask CreateEmptyAssetUploadTask();

	public string GetAssetBaseURL(string ownerId, string hash, string variant)
	{
		hash = hash.ToLower();
		string text = hash;
		if (variant != null)
		{
			text = text + "&" + variant;
		}
		return IdUtil.GetOwnerType(ownerId) switch
		{
			OwnerType.User => "users/" + ownerId + "/assets/" + text, 
			OwnerType.Group => "groups/" + ownerId + "/assets/" + text, 
			_ => throw new Exception("Invalid ownerId"), 
		};
	}

	public Task<CloudResult<List<string>>> GetAvailableVariants(Uri dbUrl)
	{
		string hash = DBSignature(dbUrl);
		return GetAvailableVariants(hash);
	}

	public abstract Task<CloudResult<List<string>>> GetAvailableVariants(string hash);

	public Task<CloudResult<List<T>>> GetAssetMetadata<T>(List<string> hashes) where T : class, IAssetMetadata, new()
	{
		Type typeFromHandle = typeof(T);
		string resource = "assets/" + GetMetadataURLSegment(typeFromHandle);
		return Api.POST<List<T>>(resource, hashes);
	}

	public async Task<CloudResult<IAssetMetadata>> GetAssetMetadata(AssetVariantType variantType, string hash)
	{
		return variantType switch
		{
			AssetVariantType.Texture => (await GetAssetMetadata<BitmapMetadata>(hash).ConfigureAwait(continueOnCapturedContext: false)).AsResult<IAssetMetadata>(), 
			AssetVariantType.Cubemap => (await GetAssetMetadata<CubemapMetadata>(hash).ConfigureAwait(continueOnCapturedContext: false)).AsResult<IAssetMetadata>(), 
			AssetVariantType.Volume => (await GetAssetMetadata<VolumeMetadata>(hash).ConfigureAwait(continueOnCapturedContext: false)).AsResult<IAssetMetadata>(), 
			AssetVariantType.Mesh => (await GetAssetMetadata<MeshMetadata>(hash).ConfigureAwait(continueOnCapturedContext: false)).AsResult<IAssetMetadata>(), 
			AssetVariantType.Shader => (await GetAssetMetadata<ShaderMetadata>(hash).ConfigureAwait(continueOnCapturedContext: false)).AsResult<IAssetMetadata>(), 
			AssetVariantType.GaussianSplat => (await GetAssetMetadata<GaussianSplatMetadata>(hash).ConfigureAwait(continueOnCapturedContext: false)).AsResult<IAssetMetadata>(), 
			_ => throw new Exception("Unsupported metadata type: " + variantType), 
		};
	}

	public Task<CloudResult<T>> GetAssetMetadata<T>(string hash) where T : class, IAssetMetadata, new()
	{
		Type typeFromHandle = typeof(T);
		string resource = "assets/" + hash + "/" + GetMetadataURLSegment(typeFromHandle);
		return Api.GET<T>(resource);
	}

	public Task<CloudResult<List<string>>> RequestAssetVariant(string hash, IAssetVariantDescriptor descriptor)
	{
		if (!(descriptor is Texture2DVariantDescriptor texture2DVariantDescriptor))
		{
			if (!(descriptor is CubemapVariantDescriptor cubemapVariantDescriptor))
			{
				if (!(descriptor is Texture3DVariantDescriptor texture3DVariantDescriptor))
				{
					if (!(descriptor is ShaderVariantDescriptor shaderVariantDescriptor))
					{
						if (!(descriptor is MeshVariantDescriptor meshVariantDescriptor))
						{
							if (descriptor is GaussianSplatVariantDescriptor gaussianSplatVariantDescriptor)
							{
								return Api.POST<List<string>>("assets/" + hash + "/gaussianSplatVariant/" + gaussianSplatVariantDescriptor.VariantIdentifier, null);
							}
							throw new Exception("Unsupported variant descriptor: " + descriptor.GetType());
						}
						return Api.POST<List<string>>("assets/" + hash + "/meshVariant/" + meshVariantDescriptor.VariantIdentifier, null);
					}
					return Api.POST<List<string>>("assets/" + hash + "/shaderVariant/" + shaderVariantDescriptor.VariantIdentifier, null);
				}
				return Api.POST<List<string>>("assets/" + hash + "/volumeVariant/" + texture3DVariantDescriptor.VariantIdentifier, null);
			}
			return Api.POST<List<string>>("assets/" + hash + "/cubemapVariant/" + cubemapVariantDescriptor.VariantIdentifier, null);
		}
		return Api.POST<List<string>>("assets/" + hash + "/bitmapVariant/" + texture2DVariantDescriptor.VariantIdentifier, null);
	}

	public Task<CloudResult> StoreAssetMetadata(IAssetMetadata metadata)
	{
		if (!(metadata is BitmapMetadata metadata2))
		{
			if (!(metadata is CubemapMetadata metadata3))
			{
				if (!(metadata is VolumeMetadata metadata4))
				{
					if (!(metadata is MeshMetadata metadata5))
					{
						if (!(metadata is ShaderMetadata metadata6))
						{
							if (metadata is GaussianSplatMetadata metadata7)
							{
								return StoreGaussianSplatMetadata(metadata.AssetIdentifier, metadata7);
							}
							throw new Exception("Unsupported metadata type: " + metadata.GetType());
						}
						return StoreShaderMetadata(metadata.AssetIdentifier, metadata6);
					}
					return StoreMeshMetadata(metadata.AssetIdentifier, metadata5);
				}
				return StoreVolumeMetadata(metadata.AssetIdentifier, metadata4);
			}
			return StoreCubemapMetadata(metadata.AssetIdentifier, metadata3);
		}
		return StoreBitmapMetadata(metadata.AssetIdentifier, metadata2);
	}

	public Task<CloudResult<BitmapMetadata>> GetBitmapMetadata(string hash)
	{
		return Api.GET<BitmapMetadata>("assets/" + hash + "/bitmapMetadata");
	}

	public Task<CloudResult<List<BitmapMetadata>>> GetBitmapMetadata(List<string> hashes)
	{
		return Api.POST<List<BitmapMetadata>>("assets/bitmapMetadata", hashes);
	}

	public Task<CloudResult> StoreBitmapMetadata(string hash, BitmapMetadata metadata)
	{
		return Api.PUT("assets/" + hash + "/bitmapMetadata", metadata);
	}

	public Task<CloudResult<CubemapMetadata>> GetCubemapMetadata(string hash)
	{
		return Api.GET<CubemapMetadata>("assets/" + hash + "/cubemapMetadata");
	}

	public Task<CloudResult<List<CubemapMetadata>>> GetCubemapMetadata(List<string> hashes)
	{
		return Api.POST<List<CubemapMetadata>>("assets/cubemapMetadata", hashes);
	}

	public Task<CloudResult> StoreCubemapMetadata(string hash, CubemapMetadata metadata)
	{
		return Api.PUT("assets/" + hash + "/cubemapMetadata", metadata);
	}

	public Task<CloudResult> StoreVolumeMetadata(string hash, VolumeMetadata metadata)
	{
		return Api.PUT("assets/" + hash + "/volumeMetadata", metadata);
	}

	public Task<CloudResult> StoreMeshMetadata(string hash, MeshMetadata metadata)
	{
		return Api.PUT("assets/" + hash + "/meshMetadata", metadata);
	}

	public Task<CloudResult> StoreShaderMetadata(string hash, ShaderMetadata metadata)
	{
		return Api.PUT("assets/" + hash + "/shaderMetadata", metadata);
	}

	public Task<CloudResult> StoreGaussianSplatMetadata(string hash, GaussianSplatMetadata metadata)
	{
		return Api.PUT("assets/" + hash + "/gaussianSplatMetadata", metadata);
	}

	public Task<CloudResult<ExternalQueueObject<AssetVariantComputationTask>>> GetAssetComputationTask(bool usePoisonQueue = false)
	{
		return Api.GET<ExternalQueueObject<AssetVariantComputationTask>>($"processing/assetComputations?computeVersion={AssetUtil.COMPUTE_VERSION}&usePoisonQueue={usePoisonQueue}", null, throwOnError: false);
	}

	public async Task<CloudResult> ExtendAssetComputationTask(ExternalQueueObject<AssetVariantComputationTask> task)
	{
		CloudResult cloudResult = await Api.PATCH("processing/assetComputations", task);
		if (cloudResult.IsOK)
		{
			task.PopReceipt = cloudResult.Content;
		}
		return cloudResult;
	}

	public Task<CloudResult> FinishAssetComputation(ExternalQueueObject<AssetVariantComputationTask> task)
	{
		string text = "processing/assetComputations/" + Uri.EscapeDataString(task.Id) + "?popReceipt=" + Uri.EscapeDataString(task.PopReceipt);
		if (!string.IsNullOrEmpty(task.BlobKey))
		{
			text = text + "&blobKey=" + Uri.EscapeDataString(task.BlobKey);
		}
		if (task.QueueName != null)
		{
			text = text + "&queueName=" + task.QueueName;
		}
		return Api.DELETE(text);
	}

	public Task<CloudResult> FinishVariantComputation(string hash, string variantId)
	{
		return Api.POST("processing/assetComputations/" + hash + "/" + variantId, null);
	}

	private static string GetMetadataURLSegment(Type type)
	{
		if (type == typeof(BitmapMetadata))
		{
			return "bitmapMetadata";
		}
		if (type == typeof(CubemapMetadata))
		{
			return "cubemapMetadata";
		}
		if (type == typeof(VolumeMetadata))
		{
			return "volumeMetadata";
		}
		if (type == typeof(MeshMetadata))
		{
			return "meshMetadata";
		}
		if (type == typeof(ShaderMetadata))
		{
			return "shaderMetadata";
		}
		if (type == typeof(GaussianSplatMetadata))
		{
			return "gaussianSplatMetadata";
		}
		throw new NotImplementedException("Unsupported metadata type: " + type);
	}
}
public abstract class AssetUploadTask : IDisposable
{
	public static bool DEBUG_UPLOAD;

	protected Stream assetStream;

	protected string assetPath;

	protected Uri assetURL;

	protected string assetFileName;

	public SkyFrostInterface Cloud { get; private set; }

	public ApiClient Api => Cloud.Api;

	public string OwnerId { get; private set; }

	public string Signature { get; private set; }

	public string Variant { get; private set; }

	public int Retries { get; protected set; }

	public bool IsOptional { get; set; }

	public IProgressIndicator Progress { get; private set; }

	public CloudResult<AssetUploadData> UploadData { get; protected set; }

	public bool UploadStarted { get; protected set; }

	public long TotalBytes { get; private set; }

	public int EnqueuedChunks { get; protected set; }

	public int UploadedChunks { get; protected set; }

	public abstract int MaxParallelChunks { get; }

	public void InitializeWithFile(SkyFrostInterface cloud, string ownerId, string signature, string variant, string assetPath, IProgressIndicator progress = null, int retries = 5)
	{
		Initialize(cloud, ownerId, signature, variant, assetPath, File.OpenRead(assetPath), null, progress, null, retries);
	}

	public void InitializeWithStream(SkyFrostInterface cloud, string ownerId, string signature, string variant, Stream assetStream, IProgressIndicator progress = null, long? bytes = null, int retries = 5)
	{
		Initialize(cloud, ownerId, signature, variant, null, assetStream, null, progress, bytes, retries);
	}

	public void InitializeWithURL(SkyFrostInterface cloud, string ownerId, string signature, string variant, Uri url, IProgressIndicator progress = null, long? bytes = null, int retries = 5)
	{
		Initialize(cloud, ownerId, signature, variant, null, null, url, progress, bytes, retries);
	}

	private void Initialize(SkyFrostInterface cloud, string ownerId, string signature, string variant, string assetPath, Stream assetStream, Uri assetURL, IProgressIndicator progress = null, long? bytes = null, int retries = 5)
	{
		if (Cloud != null)
		{
			throw new InvalidOperationException("AssetUploadTask is already initialized");
		}
		if (assetURL != null && assetURL.Scheme != "http" && assetURL.Scheme != "https" && assetURL.Scheme != "ftp")
		{
			throw new Exception("Unsupported URL: " + assetURL);
		}
		Cloud = cloud;
		OwnerId = ownerId;
		Signature = signature;
		Variant = variant;
		Retries = retries;
		Progress = progress;
		TotalBytes = bytes ?? (assetStream.Length - assetStream.Position);
		this.assetURL = assetURL;
		this.assetPath = assetPath;
		this.assetStream = assetStream;
		if (!string.IsNullOrEmpty(assetPath))
		{
			assetFileName = Path.GetFileName(assetPath);
		}
		OnInitialize();
	}

	public async Task<CloudResult<AssetUploadData>> RunUpload()
	{
		CloudResult<AssetUploadData> cloudResult = await UploadAssetData().ConfigureAwait(continueOnCapturedContext: false);
		if (cloudResult.IsOK)
		{
			return await WaitForAssetFinishProcessing().ConfigureAwait(continueOnCapturedContext: false);
		}
		return cloudResult;
	}

	public abstract Task<CloudResult<AssetUploadData>> UploadAssetData();

	public abstract ValueTask<CloudResult<AssetUploadData>> WaitForAssetFinishProcessing();

	protected virtual void OnInitialize()
	{
	}

	protected void DisposeOfStream()
	{
		if (assetPath != null)
		{
			assetStream.Dispose();
		}
		assetStream = null;
	}

	public virtual void Dispose()
	{
		DisposeOfStream();
	}
}
public abstract class AssetUploadTask<TChunkResult> : AssetUploadTask where TChunkResult : class
{
	private class UploadChunkBuffer
	{
		public byte[] data;

		public Task<CloudResult<TChunkResult>> task;

		public int chunk = -1;

		public int length = -1;
	}

	private void EnqueueChunk(UploadChunkBuffer buffer, List<UploadChunkBuffer> processingBuffers)
	{
		buffer.task = base.Api.RunRequest<TChunkResult>(() => CreateChunkUploadRequest(buffer.chunk, buffer.data, buffer.length), TimeSpan.FromMinutes(5L), throwOnError: true);
		processingBuffers.Add(buffer);
	}

	private async Task<UploadChunkBuffer> TakeFinishedBuffer(List<UploadChunkBuffer> buffers)
	{
		List<Task> tasks = Pool.BorrowList<Task>();
		for (int i = 0; i < buffers.Count; i++)
		{
			if (buffers[i].task != null)
			{
				if (AssetUploadTask.DEBUG_UPLOAD)
				{
					UniLog.Log($"Adding task from chunk {buffers[i].chunk}: {buffers[i].task.Id}, IsCompleted: {buffers[i].task.IsCompleted}, IsCanceled: {buffers[i].task.IsCanceled}, IsFaulted: {buffers[i].task.IsFaulted}");
				}
				tasks.Add(buffers[i].task);
			}
		}
		if (AssetUploadTask.DEBUG_UPLOAD)
		{
			UniLog.Log("Waiting for any task to finish. Count: " + tasks.Count);
		}
		await Task.WhenAny(tasks).ConfigureAwait(continueOnCapturedContext: false);
		if (AssetUploadTask.DEBUG_UPLOAD)
		{
			UniLog.Log("Task finished, checking buffers");
		}
		Pool.Return(ref tasks);
		foreach (UploadChunkBuffer buffer in buffers)
		{
			if (AssetUploadTask.DEBUG_UPLOAD)
			{
				UniLog.Log($"Buffer Task {buffer.task?.Id} from chunk {buffer.chunk}. IsCompleted: {buffer.task?.IsCompleted}. IsCanceled: {buffer.task?.IsCanceled}. IsFaulted: {buffer.task?.IsFaulted}");
			}
			if (buffer.task != null && buffer.task.IsCompleted)
			{
				buffers.Remove(buffer);
				return buffer;
			}
		}
		throw new Exception("No Finished Buffer Available");
	}

	public override async Task<CloudResult<AssetUploadData>> UploadAssetData()
	{
		if (base.UploadStarted)
		{
			throw new InvalidOperationException("Upload already started");
		}
		base.UploadStarted = true;
		CloudResult<AssetUploadData> cloudResult = null;
		for (int attempt = 0; attempt < base.Retries; attempt++)
		{
			cloudResult = await Upload().ConfigureAwait(continueOnCapturedContext: false);
			if (cloudResult.IsOK)
			{
				return cloudResult;
			}
			if (cloudResult.State == HttpStatusCode.Forbidden && base.IsOptional)
			{
				return cloudResult;
			}
			if (cloudResult.State == HttpStatusCode.BadRequest && (cloudResult.Content?.Contains("AlreadyUploaded") ?? false))
			{
				return cloudResult;
			}
			assetStream?.Seek(0L, SeekOrigin.Begin);
			UniLog.Warning($"Error uploading asset data {base.OwnerId}:{base.Signature}:{base.Variant}. Attempt: {attempt}\n{cloudResult}");
		}
		return cloudResult;
	}

	private async Task<CloudResult<AssetUploadData>> Upload()
	{
		CloudResult<AssetUploadData> assetUploadResult = (base.UploadData = await InitiateUpload().ConfigureAwait(continueOnCapturedContext: false));
		if (AssetUploadTask.DEBUG_UPLOAD)
		{
			UniLog.Log("Initiate Chunk Upload: " + assetUploadResult);
		}
		if (assetUploadResult.IsError)
		{
			return assetUploadResult;
		}
		await UploadInitiated(base.UploadData.Entity).ConfigureAwait(continueOnCapturedContext: false);
		List<UploadChunkBuffer> freeBuffers = Pool.BorrowList<UploadChunkBuffer>();
		List<UploadChunkBuffer> processingBuffers = Pool.BorrowList<UploadChunkBuffer>();
		for (int i = 0; i < MathX.Min(base.UploadData.Entity.TotalChunks, MaxParallelChunks); i++)
		{
			UploadChunkBuffer uploadChunkBuffer = new UploadChunkBuffer();
			if (assetStream != null)
			{
				uploadChunkBuffer.data = new byte[base.UploadData.Entity.ChunkSize];
			}
			freeBuffers.Add(uploadChunkBuffer);
		}
		Stopwatch s = Stopwatch.StartNew();
		base.EnqueuedChunks = 0;
		base.UploadedChunks = 0;
		while (base.UploadedChunks < base.UploadData.Entity.TotalChunks)
		{
			bool flag;
			UploadChunkBuffer uploadBuffer;
			if (freeBuffers.Count > 0 && base.EnqueuedChunks < base.UploadData.Entity.TotalChunks)
			{
				uploadBuffer = freeBuffers.TakeLast();
				int expectedSize = ((base.EnqueuedChunks == base.UploadData.Entity.TotalChunks - 1) ? base.UploadData.Entity.LastChunkSize : base.UploadData.Entity.ChunkSize);
				if (assetStream != null)
				{
					int totalRead;
					for (totalRead = 0; totalRead < expectedSize; totalRead += await assetStream.ReadAsync(uploadBuffer.data, totalRead, expectedSize - totalRead).ConfigureAwait(continueOnCapturedContext: false))
					{
						if (!assetStream.CanRead)
						{
							break;
						}
					}
					if (totalRead != expectedSize)
					{
						UniLog.Log($"Source stream didn't provide enough data for chunk. Expected: {expectedSize}, got: {totalRead}");
						return new CloudResult<AssetUploadData>(null, (HttpStatusCode)0, null, 0);
					}
				}
				uploadBuffer.chunk = base.EnqueuedChunks;
				uploadBuffer.length = expectedSize;
				if (AssetUploadTask.DEBUG_UPLOAD)
				{
					UniLog.Log($"Enqueuing chunk {base.EnqueuedChunks}.");
				}
				EnqueueChunk(uploadBuffer, processingBuffers);
				base.EnqueuedChunks++;
				flag = freeBuffers.Count == 0;
			}
			else
			{
				flag = true;
			}
			if (!flag)
			{
				continue;
			}
			if (AssetUploadTask.DEBUG_UPLOAD)
			{
				UniLog.Log("Waiting for finished buffer");
			}
			uploadBuffer = await TakeFinishedBuffer(processingBuffers).ConfigureAwait(continueOnCapturedContext: false);
			if (AssetUploadTask.DEBUG_UPLOAD)
			{
				UniLog.Log($"Got finished buffer {uploadBuffer.chunk}: " + uploadBuffer?.task);
			}
			if (uploadBuffer.task.IsCanceled)
			{
				if (AssetUploadTask.DEBUG_UPLOAD)
				{
					UniLog.Log($"Task failed, enqueuing chunk {uploadBuffer.chunk} again");
				}
				EnqueueChunk(uploadBuffer, processingBuffers);
				continue;
			}
			CloudResult<TChunkResult> cloudResult2 = await uploadBuffer.task.ConfigureAwait(continueOnCapturedContext: false);
			if (AssetUploadTask.DEBUG_UPLOAD)
			{
				UniLog.Log($"Chunk {uploadBuffer.chunk} result: " + cloudResult2);
			}
			if (cloudResult2.IsError)
			{
				return new CloudResult<AssetUploadData>(null, cloudResult2.State, cloudResult2.Headers, cloudResult2.RequestAttempts, cloudResult2.Content);
			}
			ProcessChunkUploadResult(uploadBuffer.chunk, cloudResult2.Entity);
			uploadBuffer.task = null;
			freeBuffers.Add(uploadBuffer);
			base.UploadedChunks++;
			float num = (float)base.UploadedChunks / (float)base.UploadData.Entity.TotalChunks;
			base.Progress?.UpdateProgress(num, "Upload", $"Chunk {base.UploadedChunks} out of {base.UploadData.Entity.TotalChunks} ({num * 100f:F0} %)");
		}
		s.Stop();
		try
		{
			DisposeOfStream();
		}
		catch (Exception ex)
		{
			UniLog.Error("Exception when disposing of stream:\n" + ex);
		}
		double rate = (double)base.UploadData.Entity.TotalBytes / s.Elapsed.TotalSeconds;
		Pool.Return(ref processingBuffers);
		Pool.Return(ref freeBuffers);
		CloudResult cloudResult3 = await FinalizeChunkUpload().ConfigureAwait(continueOnCapturedContext: false);
		if (cloudResult3.IsError)
		{
			UniLog.Log($"Asset {assetPath ?? base.Signature} failed to complete upload. State: {cloudResult3.State}, Message: {cloudResult3.Content}");
			base.UploadData = new CloudResult<AssetUploadData>(base.UploadData.Entity, cloudResult3.State, cloudResult3.Headers, cloudResult3.RequestAttempts, cloudResult3.Content);
			return base.UploadData;
		}
		UniLog.Log($"Asset {assetPath ?? base.Signature} uploaded in {s.Elapsed}. Average rate: {UnitFormatting.FormatBytes(rate)}/s");
		return assetUploadResult;
	}

	protected abstract Task<CloudResult<AssetUploadData>> InitiateUpload();

	protected virtual Task UploadInitiated(AssetUploadData data)
	{
		return Task.CompletedTask;
	}

	protected abstract HttpRequestMessage CreateChunkUploadRequest(int chunkIndex, byte[] data, int length);

	protected abstract void ProcessChunkUploadResult(int chunkIndex, TChunkResult result);

	protected abstract Task<CloudResult> FinalizeChunkUpload();

	protected async ValueTask<CloudResult<AssetUploadData>> WaitForAssetFinishProcessing(string apiUrl)
	{
		if (base.UploadData.Entity.IsDirectUpload)
		{
			base.UploadData.Entity.UploadState = UploadState.Uploaded;
			return new CloudResult<AssetUploadData>(base.UploadData.Entity, HttpStatusCode.OK, null, 0);
		}
		CloudResult<AssetUploadData> cloudResult;
		while (true)
		{
			cloudResult = (base.UploadData = await base.Api.GET<AssetUploadData>(apiUrl).ConfigureAwait(continueOnCapturedContext: false));
			if (cloudResult.IsError)
			{
				return cloudResult;
			}
			if (cloudResult.Entity.UploadState == UploadState.Uploaded || cloudResult.Entity.UploadState == UploadState.Failed)
			{
				break;
			}
			await Task.Delay(1500).ConfigureAwait(continueOnCapturedContext: false);
		}
		return cloudResult;
	}
}
public static class AssetUtil
{
	/// <summary>
	/// This represents the current version of the asset variant system. Incrementing this will make sure that any old systems
	/// will not receive any jobs generated with this version, since they do not support the latest changes made and could corrupt the
	/// system if they attempted to process those new variants.
	/// </summary>
	public static int COMPUTE_VERSION => 23;

	public static string GenerateHashSignature(string file)
	{
		using FileStream fileStream = File.OpenRead(file);
		return GenerateHashSignature(fileStream);
	}

	public static string GenerateHashSignature(Stream fileStream)
	{
		using SHA256 sHA = SHA256.Create();
		return BitConverter.ToString(sHA.ComputeHash(fileStream)).Replace("-", "").ToLower();
	}

	public static string ComposeIdentifier(string signature, string variant)
	{
		if (string.IsNullOrWhiteSpace(variant))
		{
			return signature;
		}
		return signature + "&" + variant;
	}

	public static void SplitIdentifier(string identifier, out string signature, out string variant)
	{
		int num = identifier.IndexOf('&');
		if (num >= 0)
		{
			variant = identifier.Substring(num + 1);
			signature = identifier.Substring(0, num);
		}
		else
		{
			variant = null;
			signature = identifier;
		}
		signature = signature.ToLower();
	}
}
public class AssetVariantIdentifier
{
	[JsonProperty(PropertyName = "assetSignature")]
	[JsonPropertyName("assetSignature")]
	public string AssetSignature { get; set; }

	[JsonProperty(PropertyName = "variantIdentifier")]
	[JsonPropertyName("variantIdentifier")]
	public string VariantIdentifier { get; set; }
}
public class AzureAssetInterface : AssetInterface
{
	[JsonPropertyName("blobEndpoint")]
	public string BlobEndpoint { get; private set; }

	[JsonPropertyName("thumbnailEndpoint")]
	public string ThumbnailEndpoint { get; private set; }

	[JsonPropertyName("legacyBlobEndpoint")]
	public string LegacyBlobEndpoint { get; private set; }

	[System.Text.Json.Serialization.JsonIgnore]
	public string AssetsEndpoint => BlobEndpoint + "assets/";

	[System.Text.Json.Serialization.JsonIgnore]
	public string InstallsEndpoint => BlobEndpoint + "install/";

	public AzureAssetInterface(string blobEndpoint, string thumbnailEndpoint, string legacyBlobEndpoint)
	{
		BlobEndpoint = blobEndpoint;
		ThumbnailEndpoint = thumbnailEndpoint;
		LegacyBlobEndpoint = legacyBlobEndpoint;
	}

	public override Uri DBToHttp(Uri productDBUri, DB_Endpoint endpoint)
	{
		string text = DBSignature(productDBUri);
		string text2 = DBQuery(productDBUri);
		string text3 = text;
		if (text2 != null)
		{
			text3 = text3 + "/" + text2;
		}
		if (IsLegacyDB(productDBUri))
		{
			return new Uri(LegacyBlobEndpoint + text3);
		}
		if ((uint)endpoint <= 1u)
		{
			string assetsEndpoint = AssetsEndpoint;
			return new Uri(assetsEndpoint + text3);
		}
		throw new Exception("Invalid DB_Endpoint: " + endpoint);
	}

	public override Uri ThumbnailToHttp(ThumbnailInfo thumbnail)
	{
		return ThumbnailIdToHttp(thumbnail.Id);
	}

	public Uri ThumbnailIdToHttp(string id)
	{
		return new Uri(ThumbnailEndpoint + id);
	}

	protected override AssetUploadTask CreateEmptyAssetUploadTask()
	{
		return new AzureAssetUploadTask();
	}

	public override Task<CloudResult> GetAssetMime(string hash)
	{
		return base.Api.GET("assets/" + hash.ToLower() + "/mime");
	}

	public override Task<CloudResult<ThumbnailInfo>> UploadThumbnail(string path, string session)
	{
		return base.Api.POST_File<ThumbnailInfo>("thumbnails/" + session + "?version=1", path, "image/webp");
	}

	public override Task<CloudResult<List<string>>> GetAvailableVariants(string hash)
	{
		return base.Api.GET<List<string>>("assets/" + hash + "/variants");
	}
}
public class AzureAssetUploadTask : AssetUploadTask<CloudMessage>
{
	public static int UPLOAD_DEGREE_OF_PARALLELISM = 16;

	private string baseUrl;

	public override int MaxParallelChunks => UPLOAD_DEGREE_OF_PARALLELISM;

	protected override void OnInitialize()
	{
		baseUrl = base.Cloud.Assets.GetAssetBaseURL(base.OwnerId, base.Signature, base.Variant) + "/chunks";
	}

	protected override Task<CloudResult<AssetUploadData>> InitiateUpload()
	{
		return base.Api.POST<AssetUploadData>(baseUrl + $"?bytes={base.TotalBytes}", null);
	}

	protected override HttpRequestMessage CreateChunkUploadRequest(int chunkIndex, byte[] data, int length)
	{
		HttpRequestMessage httpRequestMessage = base.Api.CreateRequest(baseUrl + "/" + chunkIndex, HttpMethod.Post);
		MultipartFormDataContent multipartFormDataContent = new MultipartFormDataContent();
		ByteArrayContent byteArrayContent = new ByteArrayContent(data, 0, length);
		byteArrayContent.Headers.ContentLength = length;
		multipartFormDataContent.Add(byteArrayContent, "file", assetFileName);
		httpRequestMessage.Content = multipartFormDataContent;
		return httpRequestMessage;
	}

	protected override Task<CloudResult> FinalizeChunkUpload()
	{
		return base.Api.PATCH(baseUrl, null);
	}

	protected override void ProcessChunkUploadResult(int chunkIndex, CloudMessage result)
	{
	}

	public override ValueTask<CloudResult<AssetUploadData>> WaitForAssetFinishProcessing()
	{
		return WaitForAssetFinishProcessing(baseUrl);
	}
}
public class CloudflareAssetInterface : AssetInterface
{
	[JsonPropertyName("apiEndpoint")]
	public string ApiEndpoint { get; private set; }

	[JsonPropertyName("assetsEndpoint")]
	public string AssetsEndpoint { get; private set; }

	[JsonPropertyName("variantsEndpoint")]
	public string VariantsEndpoint { get; private set; }

	[JsonPropertyName("thumbnailsEndpoint")]
	public string ThumbnailsEndpoint { get; private set; }

	public CloudflareAssetInterface(string apiEndpoint, string assetsEndpoint, string variantsEndpoint, string thumbnailsEndpoint)
	{
		ApiEndpoint = apiEndpoint;
		AssetsEndpoint = assetsEndpoint;
		VariantsEndpoint = variantsEndpoint;
		ThumbnailsEndpoint = thumbnailsEndpoint;
	}

	public override Uri DBToHttp(Uri dbUri, DB_Endpoint endpoint)
	{
		string text = DBSignature(dbUri);
		string text2 = DBQuery(dbUri);
		if (string.IsNullOrEmpty(text2))
		{
			return new Uri(AssetsEndpoint + text);
		}
		return new Uri(VariantsEndpoint + text + "/" + text2);
	}

	public override Uri ThumbnailToHttp(ThumbnailInfo thumbnail)
	{
		if (Uri.TryCreate(ThumbnailsEndpoint + thumbnail.Id, UriKind.Absolute, out Uri result))
		{
			return result;
		}
		return null;
	}

	public async Task<CloudResult<BlobMetadata>> GetAssetMedata(string hash)
	{
		string url = AssetsEndpoint + hash;
		CloudResult cloudResult = await base.Cloud.Api.HEAD(url).ConfigureAwait(continueOnCapturedContext: false);
		if (cloudResult.IsError)
		{
			return new CloudResult<BlobMetadata>(null, cloudResult.State, cloudResult.Headers, cloudResult.RequestAttempts, cloudResult.Content, cloudResult.ContentType, cloudResult.ContentLength);
		}
		return new CloudResult<BlobMetadata>(new BlobMetadata
		{
			Url = url,
			Name = hash,
			Size = cloudResult.ContentLength.Value,
			ContentType = cloudResult.ContentType,
			LastModified = cloudResult.LastModified.Value,
			IsFree = cloudResult.TryGetHeaderValue("Is-Free")
		}, cloudResult.State, cloudResult.Headers, cloudResult.RequestAttempts, cloudResult.Content);
	}

	public override async Task<CloudResult> GetAssetMime(string hash)
	{
		if (string.IsNullOrEmpty(hash))
		{
			throw new ArgumentException("Missing hash");
		}
		CloudResult cloudResult = await base.Cloud.Api.HEAD(AssetsEndpoint + hash).ConfigureAwait(continueOnCapturedContext: false);
		return new CloudResult(cloudResult.State, cloudResult.Headers, cloudResult.RequestAttempts, cloudResult.ContentType);
	}

	public override Task<CloudResult<List<string>>> GetAvailableVariants(string hash)
	{
		if (string.IsNullOrEmpty(hash))
		{
			throw new ArgumentException("Missing hash");
		}
		return base.Cloud.Api.GET<List<string>>(ApiEndpoint + "variants/" + hash);
	}

	public override async Task<CloudResult<ThumbnailInfo>> UploadThumbnail(string path, string sessionId)
	{
		CloudResult<ThumbnailInfo> info = await base.Cloud.Api.POST<ThumbnailInfo>("thumbnails/" + sessionId, null).ConfigureAwait(continueOnCapturedContext: false);
		if (!info.IsOK)
		{
			return info;
		}
		CloudResult cloudResult = await base.Cloud.Api.PUT_FileDirect(info.Entity.UploadEndpoint, path, "image/webp", null, null, delegate(HttpRequestMessage r)
		{
			r.Headers.Add("Thumbnail-Timestamp", info.Entity.Timestamp.ToString("o"));
			r.Headers.Add("Thumbnail-Signature", info.Entity.Signature);
		}).ConfigureAwait(continueOnCapturedContext: false);
		if (cloudResult.IsOK)
		{
			return info;
		}
		return new CloudResult<ThumbnailInfo>(null, cloudResult.State, cloudResult.Headers, cloudResult.RequestAttempts, cloudResult.Content);
	}

	protected override AssetUploadTask CreateEmptyAssetUploadTask()
	{
		return new CloudflareAssetUploadTask();
	}
}
public class CloudflareChunkResult
{
	public string ETag { get; set; }
}
public class CloudflareAssetUploadTask : AssetUploadTask<CloudflareChunkResult>
{
	private string apiBaseUrl;

	private List<AssetChunk> parts = new List<AssetChunk>();

	public override int MaxParallelChunks => Math.Max(1, (base.UploadData?.Entity?.MaxUploadConcurrency).GetValueOrDefault());

	protected override void OnInitialize()
	{
		apiBaseUrl = base.Cloud.Assets.GetAssetBaseURL(base.OwnerId, base.Signature, base.Variant) + "/upload";
	}

	protected override Task UploadInitiated(AssetUploadData data)
	{
		parts.Clear();
		return Task.CompletedTask;
	}

	protected override HttpRequestMessage CreateChunkUploadRequest(int chunkIndex, byte[] data, int length)
	{
		HttpRequestMessage httpRequestMessage = base.Cloud.Api.CreateRequest(base.UploadData.Entity.UploadEndpoint, HttpMethod.Put);
		httpRequestMessage.Headers.Add("Upload-Key", base.UploadData.Entity.UploadKey);
		if (!base.UploadData.Entity.IsDirectUpload)
		{
			httpRequestMessage.Headers.Add("Part-Number", (chunkIndex + 1).ToString());
		}
		else
		{
			httpRequestMessage.Headers.Add("Upload-Timestamp", base.UploadData.Entity.CreatedOn.ToString("o"));
		}
		if (data == null)
		{
			if (assetURL == null)
			{
				throw new InvalidOperationException("Data buffer is null and AssetURL is also null");
			}
			long num = (long)chunkIndex * (long)base.UploadData.Entity.ChunkSize;
			httpRequestMessage.Headers.Add("From-URL", assetURL.ToString());
			httpRequestMessage.Headers.Range = new RangeHeaderValue(num, num + length - 1);
		}
		else
		{
			ByteArrayContent byteArrayContent = new ByteArrayContent(data, 0, length);
			byteArrayContent.Headers.ContentLength = length;
			httpRequestMessage.Content = byteArrayContent;
		}
		return httpRequestMessage;
	}

	protected override Task<CloudResult<AssetUploadData>> InitiateUpload()
	{
		return base.Cloud.Api.POST<AssetUploadData>(apiBaseUrl + $"?size={base.TotalBytes}", null);
	}

	protected override Task<CloudResult> FinalizeChunkUpload()
	{
		if (!base.UploadData.Entity.IsDirectUpload)
		{
			parts.Sort((AssetChunk a, AssetChunk b) => a.Index.CompareTo(b.Index));
			base.UploadData.Entity.Chunks = parts;
		}
		return base.Cloud.Api.PATCH(apiBaseUrl + "/" + base.UploadData.Entity.Id, base.UploadData.Entity);
	}

	protected override void ProcessChunkUploadResult(int chunkIndex, CloudflareChunkResult result)
	{
		if (base.UploadData.Entity.IsDirectUpload)
		{
			return;
		}
		lock (parts)
		{
			parts.Add(new AssetChunk
			{
				Index = chunkIndex,
				Key = result.ETag
			});
		}
	}

	public override ValueTask<CloudResult<AssetUploadData>> WaitForAssetFinishProcessing()
	{
		return WaitForAssetFinishProcessing(apiBaseUrl + "/" + base.UploadData.Entity.Id);
	}
}
public static class LegacyAssetMap
{
	private static Dictionary<string, string> map = new Dictionary<string, string>
	{
		{ "-2EdpXSpXxLx9Pxwtvov-oI5gQ4", "54e39643b43b287bde1d2e937c46af7a4c3beee274693199b02ea4fae32581ac" },
		{ "-2lKpxd9dbvoY85bRxCKzzkFBJU", "3fdda767f5fea532c568240a524a21767384cbd95cf760d91538304748d3a328" },
		{ "-9TAX4FYe6hFmM7nud6IlL4AHMs", "ea2b8c020a9608a5be2b6acd06928d61bd47f811941ee888b27a4889f9722da3" },
		{ "-BjJOFBD-edmTie4qxc5Z0JGc5E", "62f04d70e4728ab6394c39d3fd5fbcb93eb596acefbf191eb746df7e5dacc8dd" },
		{ "-fqZZ8QbZWBgqV93uKrI20_1M6Y", "5e6c44edf3eec0affb58d6934f4bd750ae8a39d25dcbf596141c4ae3f710740c" },
		{ "-i6sbppr-XA6sUZbT4auDNVJHWM", "77f90d45bff039e8f89abe91ac310a3da7a82a8d048b7b4223328480f1e9bb8c" },
		{ "-KbXN5cu9C-Sjk-SoSrn3tBCAFg", "19041045f19e85a6e9552a82898073262013d136443b981c5cdfa3b331d601ff" },
		{ "-kloCw1776NHJ7XVnkpVN4lQOhA", "bdc21efbd71e90334261790d9fc1bb814a504d467b610c2ff82c2f6136347ede" },
		{ "-KZ15fevCUAnRJeOMgLuN7ECtK8", "83aeb4ea685fb4d3806f6dfd3e295179b60ee1be22c1db6e6cdf4788964a3193" },
		{ "-lvOP3YO3rVP8vjmNs7vjg6mj4o", "482a858e93e2e8ff2dd9d01c5504939e1f3bc825400252d1ecd9a56c9102731c" },
		{ "-MqBwpqgfUosBFTY2owx2kTKaWg", "870e382e350848cc3d6e01260cb2a47ba5378b8518a76503934fd4b531daf49b" },
		{ "-mSNxggHOpoqC4WVxkLp0ynnrOY", "d4f918f7afe65059b3bcd3244614b1ffc311eedce1234d9c7edc48fee00c2676" },
		{ "-nMFDRPnfnXdETHGjEUIHhIEs-U", "584d3bdd939ccb39db0b1c8d9c40d8764ec35d39c0af8f2d747b78b210bbb4f1" },
		{ "-qDMy2rQQkYLLJGGNAUzdR4I-As", "6c62000610cbe245c75724d92f1a99d0531afdc3616bf5e4d98dc82d14ec6c81" },
		{ "-qwlhTE1Ir6tEjzZnGNPXvt6pRg", "7c268428487c48b83bcc926431e61296205d116c5b63361c77ad90643e67a892" },
		{ "-r5HEblK4ZFEActXQgDsY8LLGF8", "099542b45898419f3d85807f5d2e1c0eb0bcd2b52eaab54cfb0fcf583a7e7f12" },
		{ "-tWT4c6O00P0xL8oxBn-DE_Ooyk", "eb92742bb98e383e4934fc3b5ecfd681f65dab141903aa5de498b792f937c2c6" },
		{ "-uCQxopnvEuIdmYfIz1hwQ", "4de6d04ce1762c0a4ce3acc519b8e6e47519dc53679333bd33cea5bdd7204b0d" },
		{ "-V123MNioiXmG_pOO4oWNoTrrZI", "49d9b83585b6f2f9518b01191f07da3d0d09c3af51da7d85da67bcfd74fe4aa8" },
		{ "-vWyGPpgY1Kv7Kvw0U4t4nElo_8", "7b250ad6361ed5ec9fb6352dad4a332415997097f6b8434f2b4b1090593d2762" },
		{ "-wofVF_bAPQi5OsER2yMMo0CRrA", "f78bedea7fc2da5a72f23ea3405e45b0cfcd7649fbf3796914f3802b6ede7806" },
		{ "-zCMrslHk-vIFJ2swiih3f3YsGk", "1ad1016ad5ce1e52dce3835a0bfb8efbfeabaa14b058044964ba85a4ad8e93bf" },
		{ "-_PHC6Xg-uVEEJpUcC5PLwlxVXk", "9649f7c843c9c458eaa2b0efcadb434d3ad0d3eeb98ca26e0212d9a607a63b5f" },
		{ "0-JmT7kfSc2U5MBdupcperFbApk", "7f31db9f871909185416a685c433ced6bf085c8b1fcd5becb3a3ab1fa0930bdb" },
		{ "01aXgjs6QkibUmDEBZKmBg", "f2e1f3b4039940172b6244462fbcdb58c6530dbb2080127fd2f9bd1e2a28f802" },
		{ "07UpI8MYUVoAOq3Xd2Nn-wtGPOk", "5b744d12e90abc5d44e4bbb7c2d5c925f8399512ced69e68dcee301dbea05553" },
		{ "096bQ2_a10SydpNN_W6YFQ", "20bb727a15fdad40884e0d046959de65d4d5f84374906f2219a268fabb0c96ab" },
		{ "0bEBrEUg-bGHOb8v91Ht7HeZEfo", "ae320d865cc38eec5569c421b8c57adf33765ee12f1bd0e79464a9f58a1c6782" },
		{ "0Bpae8jA3Mr7h6UFlP5i7uEHuuI", "b8f131292dd8489719177f544463cdbc0c35e7bb47f683bda9027c65c07e3a46" },
		{ "0B_3-zKOvEWe4xq0HKTPaA", "fd5838991852d77eac121a1b01ac1dc7c64a1fbd3113a59d572acd7259a8341a" },
		{ "0CyUAnCpkDg1FhmSIwmhkbjwngg", "844b6d1d2ed206183aa2bc119e9250facbba9c96f6c99f4daab19efe6aec50fe" },
		{ "0DnoEepUfrmgxk9-ugXp06gaM_0", "bdeda487b3fcf3eaf8881af3eac6a3f30a1d47814859b5ea929d36ba11604ff2" },
		{ "0eK-gw4PAPM5K2s2FVBoTPUfVNA", "d705900055fabf3bb93d457a7257c3922f8da4122d2786fc4da665ad2a0bf1e6" },
		{ "0FqhUk6X5OpniTORWKAGu6L3YxU", "0145c47de73771eaf06d1ee79d0891470953fed5a05dbdc0526a99de462804b6" },
		{ "0GRsunopJEu90JYVEmgavw", "0d0736c16470b698f48eadd798989d7cb5037422a47c062d5de57d9142d23c71" },
		{ "0j3gPWMGhBrP8BcxizHP1PT3bIc", "c03eb147d9e8ebb48088da04e7a99f6b5ba45844bc3375863a181ee71153b9a3" },
		{ "0J7ovJp403M2rkOZWoMyNM58fIw", "e3646b9535011746b02222dd304d9b4fc328a1193f1f2fe99eec582f73f5c864" },
		{ "0j8ahQinzaYHxY2gQqD14SzRLEg", "bdf21a4e9a8ceea91052c7a0c53e1b6c6fd202e28b0f8c7f43f427724ff1fdb4" },
		{ "0JcpEbDKgr76M7sSZ9aGy-OBxDE", "d5d5fcfeb5dbb633217fbd70130e174f851a669d3087aaa51e9adc25a1a539f4" },
		{ "0JjUXpZcEcpHEvoWmFSODxtwL9Q", "a7d76600e97856276921a7c1dc7633aaeadbbaeaf6ef318d9316b8662024093b" },
		{ "0loE_Q6R9DKPa77gF1hAPbE3-r0", "81a0894f08daba1c0e32b2a5fbffeb1f2879bef3d6f3e68669845e83a8583d73" },
		{ "0Measloq1j9obDSHf-S56FHFyWk", "a5496030d054d091e8d4992ff8997bb711932493bbda261453f02373cf602958" },
		{ "0op4fMR8KYauE4f0gMAhfg0y6g4", "13160fd44188bfefd60ca12c6681bdef7eca6546a32ee132b702e365475654fd" },
		{ "0r-uxbZEUdy_qTwgMbdyaY9siAc", "e12ea5e836c74bbf5bb7d744a2c37882e9d9d3733d4d15e2fb37dbbb2ead9439" },
		{ "0T6cemfLGtmbAvrireaqIxSS45g", "a8d7c1855fa0d92d196a546539b7e29beac30b5f939cccd12988b184dab390a3" },
		{ "0uA4j1tH5O60ntEg3098ofNNoOE", "633df846e01fce104fd98cc379aa01193ad61a804d90747390e1b3d802e06f69" },
		{ "0Vs8Qo7ULAE89ihN39StFuu7Dic", "640aff81a6e88a2a96d8f5de8acb9ff5e7edb0fb15f957374c49f066c1c68e9c" },
		{ "0V_ByKGvkkWd7xWxuw_eXA", "a481cab77d22f411b21fc46a7ae822d658465151d87b63b877b9c24d088adcd1" },
		{ "0WVqPCWPntCMod-lBvIlk5nyj0s", "f3fc6c24b8dc5026808c4ca0778008eeea8e6b315d8c7df24dce7167e9e92eb0" },
		{ "14IcR-plnNP0Nyp_hgoCH7u2HhU", "ad0da969c7326d2a2700e2eb51fc27ec91d81ebec2f72be0185caf1f20c9b446" },
		{ "15Da6HBLfDa8SVnXUxNirabCXS8", "588e59be82af55c9aea1db3c5917db09cc770356cddc67203432d9a0875613ab" },
		{ "17sMmPPovOVRPtKxTB1Z7idspqc", "b8bdab95a23f8f8e21af8b3d6010c97934b7ccb3405870b6b456503b88c2886b" },
		{ "19HVhIV2xFVWFBHTaeOnvJgjNYk", "9c02730f6684fbef19c21a7268dd69de8ba0bdb9f83e5f4c3a98093537561f11" },
		{ "1ao4ugGfgs-amPJtGTiKG1HQ_2A", "8b0fcd2b8d4cbf37eaf12d9e219c188a5d2ad716d4426c372ed379133be97b52" },
		{ "1bZ1HWX4yVB1Q1LzBuZj2tlmnF8", "277d443119ccb84ef197bb8d5cc6ac4b487f379219908be5823d59f56d43ad25" },
		{ "1D5AE6OTd577DIw5iGsad5aAZFs", "1175a86a3067bcbad1ec63f359b922700314f11899b48576e4822e987f813e66" },
		{ "1EW87eEx7HjRjReRD19P9P-6RwU", "0d1308eb53837aeabca131943ed670f1f3c5033b53060aedec63690f34bdbeee" },
		{ "1fNY4W8dpOP_RivKWhrLe66zPLI", "6fae098a0c481ae860a174511f8defd83a1e35f69370f29c352da8f5fa6b97db" },
		{ "1gUlAGRdXa0L29PmdYadBqdILxE", "56d1710a45c37b07e0f679c44cb7de447ce7baa5a9575b08576b3cbffb7d9a50" },
		{ "1gYkEHG2ZUqqaP7nKcr6GGkZkPI", "e4bb1dcb7353e72e0f25be0078b673461cc95a5d753d3c7054f32d6554fdf393" },
		{ "1hvp5y2IK2PBDydgsGKVZtfA1mM", "2dc2be71fad0596e9b9b48ea6460620dd9d274afbb3866638a4c10cafa9b785c" },
		{ "1JsIczu4se3fcKtFBIu-7MS2D0g", "e93ffb8a3e364e2356603873a29fa34ecad18a78847abcfbd6d95c2f67885318" },
		{ "1lW5vninX5BGwp5EYSR0VMLehkA", "357bdc05bed16ca45eca3437aa87945b5a71f48cfb41a5b2f28c5580151ba736" },
		{ "1mawerpv3yUclFoUSzU2ObuLiLM", "5e69befcb61b959bcfac1afd1c40ad88eba77a6222264add436fc2454f5f7a44" },
		{ "1pSVn8x83xchw_LNnBC2g7uRraw", "2aeb756e34c0eb8e3aa03fe10a4dcf04f3817d56b0096e279d71a1b752801afc" },
		{ "1PZM2QBltuwsqhNOIuWPzsaxTU8", "ded8e5871ea0d771ebb6683c154df77eb7bc465a6fc32a5db9f82897d8d34aab" },
		{ "1QapZ2b7v1ylH97N_0pHnchIixc", "b0b282e2264520afc1ae54570715eb529c1ccdf83486dc6492157f815fcf24a8" },
		{ "1r3RX5QrzD9LvyRrWQTvLOi0UU0", "26c1e54d3ffa2e8a43f87ddcc7ef724b55a60d0e69efbd3524f9d7e03444725e" },
		{ "1rQcU1X5_xDlRPdPPixRDQlKTxQ", "1d082f3f496ffa7cdcd10d356c8ccf192e1431f3c86d58c80d810680b937ff58" },
		{ "1W89qrv9P5Ow-JYrbMbBS3raC9Q", "27c3cd758b8221c07120aff3677a4e4fcac1cc06b08ba8914db6abe1a3512068" },
		{ "1XP6ATqj2bbsxW3aBzAANoNJ8NI", "95eb320b13f977502cac32009905ded55129e29d34cc5037aa83e14f572e5dd0" },
		{ "1ZzytFDOTECCQ-U6P4j2KA", "f3a6236c40dfc885547921ad4417f65198e4e66a6fe84cef7e42adb08cb3430c" },
		{ "2-TDXcHikIXIPbWpnXOxz3ntwBQ", "3aefc0eba5b432c670dea21868b50c98d9ee78d5f3ce5dd468348e298b4e9d21" },
		{ "22eMjGRee4zTX0D9lkmlZ3SQlPk", "c1a62cff32e4d6ce1180b9475ea3601169117ed6a15135283f8a5f15d3b32c4a" },
		{ "24hCzSpMcEq6kltH3Ca_Rg", "4edda961b49452a876dcfcc538428de5c1952e6aa934dfef44325f647353b056" },
		{ "24PPA46KsuKULh1SHBMhc0Awmhk", "1f05fc81040c23c111b8f068b3b8253b81f0f9e9ef750dc50a60d18191634510" },
		{ "2A8jM9Z0HK2PSjHp3Hox486TiKo", "363ba3b36b41605742109a18f544fbe483ad5674cf8fdcb7d89aad6a4a850c7a" },
		{ "2aRhQfcXp74qQItemh6pys6lbSk", "63ac52e714159641c0b2e292fb1d6e97da6cc8d72fbd7e2de52ff5286c6f2fd9" },
		{ "2B3HFJmTek-ycdXVACgyyQ", "5d86e386db2d7acaaf1b4084d9857be16922c145dbd7e741336208e9de885c88" },
		{ "2CqaL4pjhvF2U76dGYa9XUpe6Iw", "9988f85295998dbcf4bd77880175e352eb1621659394dfca9c6a4a12747d6ee7" },
		{ "2DdxJFYK1uCsGoh9GzVmXES3vW4", "88a3a41e43c310d6fb9981bb7f5b6feac3b2d31da0068e001eec00a62e4d5433" },
		{ "2gHeh04FIV3HBsYxHw8hJ17nGss", "7ef040c488d7d4ae640614403c326ee3cf4a2ef26434a27a1066291c401f313b" },
		{ "2gNexzHCHwMummFtMkBCQOIhZzE", "a9df09b6740581c2e9d26ab3da537808ecc7874780690369d31fc2b20906824e" },
		{ "2gq_cyCPGKzP0LA8oycjcbgOXXg", "16138e3d94ab407bea03aa992b7377f2a9995636f239ff6c404d4908f0537783" },
		{ "2iS4u9HrMZi1rqwa8qaAvNTadMc", "a177737b92f4d76069401711c5d36beac57089262ac494669d25ec1cf0444aa1" },
		{ "2j61vQUwZAa7T8TOW53yfWCnjNU", "d7c8bbfc8cb45eb123b95b7fda745d8488e2f5df5fcee3d7b3b004a0e799973c" },
		{ "2nPEpJyMlRKN5j8JB2esHCPdlHA", "adeea08d90c81541bdc9eac557c686897303c4323103d31cb326acf8701c9731" },
		{ "2oa7GbBEjeVdrabTHluPj24KP9k", "60fc8f9ce20b28d8aac3433a6826e3e794f61cd4075348739549d2032fae2eda" },
		{ "2OrOMSuAzXdy2S3DcbHPuLF2qLU", "416bddbdde57bb2ecd471f64d5bf787d096cc00d56b40aaff333da7fa8d6397e" },
		{ "2Ow_uNS0CtC1EXxfnWoryeqhlXE", "acfca829813a05fb32024d60d6f7fc99ae1185fad16b3d0374923d0a1814caf6" },
		{ "2P13b9N9XS7N_1iDfSBdH7r3G_g", "aecc5db7b62467dea9fdbff4a02cf8e1814a107427fe6fc2791ed5749135cdea" },
		{ "2PSqMFLriU2_5rrkmObS1Q", "f4f00470a7e501dac3e57ebba1085b177b4f8d40bb2e16be6ba6a46d196c6945" },
		{ "2qRtCVPJs8HJmGJq40tBUZ40nQw", "e586d94e5f5407cff52f7f6e362b50e4887ed52ac2c2a51308d48db879731c26" },
		{ "2RQO3PGsksEoTAqtTdHfat1cPKE", "2762fb6b17693460920b8cca26bf0a0ba68b28f3c5240c2f420e7ab3eb8875ec" },
		{ "2Tjn_veHykGZohkmb6B8JQ", "c36d93ddf8819532c041492e0060dd8d8d76281138f40ab3d046653fc05dfb39" },
		{ "2Ub31K_PndEdGuz5KTEIHh4yxgU", "1648ff595e4537c698a7b7e7a26c7cb5015bab6f6cde5206747f41b592be48f0" },
		{ "2VZY3KcTRtZoJ36Zk-qnuMK20ic", "5aec0e0e12228cdcd28a1f9056eaf6e923a226bea4c954e782c6afa202cb9708" },
		{ "2Xy8PwRLjUew9mYi9UVfOw", "095815f3f4584315dc0d5c8c78395b22f7935f6b52f79b7bb953a178a5d3c6cd" },
		{ "2y7_Gf9EGwYsaxLKZ2T6oLWfMOU", "511bbcd2bd42b67bc7d279685b221124ece577f0d57e8dedd8fd2db45bb04eda" },
		{ "2Yb9OmsrH9ZcoTnjMWOd9_UvqD0", "8fb443e5dbc99a8803a5c1304d788513c9a905eeba6e0c5a794880e77b601b3b" },
		{ "30Y_i2lWbaoJd4k_0S6eaN3u0gs", "231026041beec55d8c85c57ba8ec3ff44d4af27a6c6b80b6272960176f1e79a8" },
		{ "33FznsQO20w4YvKs_xFYcc2dj70", "4f50dd853d79ed386f199d7f2ab9a36794522dc9a359658817c58a1be7eadecf" },
		{ "38Q1yp-a2UuBfzPGXiUqNA", "dd3122a330f99a8024266d4a6eaf36e45804c6679a622d5ad67a00ee5d965e37" },
		{ "392_TOMRAEpHusYPojdv6n1JvnU", "624cc42ebb67fd12256ba3d24823880dfb0ade5c311daa179be656a611b4f7e5" },
		{ "39yWkZD_WqALSqZppRuX_CMtREk", "9f2f777e280d9dc015837c487f7777861cb0cce48f991d758dc84c2db37ce040" },
		{ "3bwRLFkiIequnXyrEILQU8O7T4E", "aa5afd8897f72829da93fde3e135b0c6f99d383671eedaf542d5bbd409b4a8ff" },
		{ "3eLSjOw0wLFmXP_E4Prw4dumjSY", "c53c54cab79e9e0c19eae50b9c20ca791eca8f68925f3c77c8b3c495042e493d" },
		{ "3G3srVrBGJvIIr1bVAEfKJJ5sEo", "946e0275e44ed11382f3349587b64101e0e376e8d56749e59e4d39d6d4c636de" },
		{ "3gFKyBUWd3bBaTD54hAFpkUgGhA", "e80209c45e8a141f3a10ae6e06c8cb19c8db6ce2c63292bc30a5c0bcba661b0d" },
		{ "3GiTFZvXLTuj7fgZ_BpYkBVc2J0", "5f4e17003d9f7d87c8e0508e985d7f64d0e2bf48ff35b5f949b881781de2661e" },
		{ "3HCZwP25IUWa0jiTe4J6mg", "2eb89ec5714b6587197b625658e25f242ab1f1d3dd53f28ac51e785842900fd3" },
		{ "3HkDRw9b4jItPy4s34gxkmM5_BI", "7cdd712776bf2edb7250a035364e23ecc69628d5ef4b262a120b05b6f4d7b176" },
		{ "3iSnR7LhpEGeEFvC6lzNRg", "3d62d1ab2e8e268c792db7b7879f80733fcec7cf4d369dc8197680af643e7eee" },
		{ "3IU0dLJ-H11aGrJj8YCQzZDer9Q", "563fcfff1e277a26f383d535929f3de134b48e81ef7ab30f766c3cf912a3228d" },
		{ "3lDa3-ikoUylVxTyyT_rBw", "14c225ebe4c48f6a5d665a4921a7e93dca22cb4bf151ccfcc0632cdebc6104e8" },
		{ "3nhPFBpEkfpYbWkcEs4nsnhHBQI", "a378f43162bb7b7f789d6bd16ff95e7eafd70dadb61ac596cbcb5fabf92fe954" },
		{ "3nygAZMoxMNFTfmybjG8nbUPztc", "c9578234fc99bb5169da4b0df7ccd03b698a6e54155409fba48c332d18a42dc6" },
		{ "3O8nA3Mb2MBMJ1j9v3uOPKE_pG8", "1695ecf3d54537d135951fc6cd5967c604e9bc0b72d8004223972a113fa0877c" },
		{ "3p2Q7SDtDAc3aLLJtZIPHEDKzzk", "3dfd635463bdda9512232878549297142097b20d81734f3686d219244d077d8e" },
		{ "3pm__s2JgU6pO4s1XsdmCg", "dc50e938fa0a96db92501b8cbc0bafc019b8bb947b0abf10703de8fdb206a0f8" },
		{ "3qLlhkEP30ecoZAwiL_IVg", "9658725e5828b0e9c30f7de992452bac6f776a2153b65acd59c0ca769471b16d" },
		{ "3qVYWL1EPk2LvDK_HCRJIw.meshx", "db6cbdfbf9e126708cc502cca8ad6d0c2f3b4a7aa9d06d99185bed8b9eda662d" },
		{ "3Rg8QIKHY8sJ5nyiXlkfQWIEn0s", "9d184eb02eebc920a1feb7a4f83975b8b1251c4f0892cbec6d07a63d6ff7670b" },
		{ "3rj42JKdDRwT3fTI0MV9Z_414GI", "a4bbaff136e7c8457b307fc76f79708ab9a0521f07bc29e8b9e560aa7cb0cd11" },
		{ "3TpJb9p8FkGoNk0UF4bmZQ", "4263b32618bf0fbee41162d4921deebd41be3607dc93f9ab587fc50a80629048" },
		{ "3UW_telxI8BbArpVdVCTrRX-Pks", "0fc7e21333683bdee7278662c9964d34bf419643b6393a7e8af5497c2c0f4ffc" },
		{ "3V40si4cEPyutiMqhGCtZD9dKlo", "42c9f89414f0ce3d6456e8e42df2de5767fb12c56c0f1087576846a342eeb7f0" },
		{ "3vyWy08ipcaJHQx_SxEx9soiddA", "05652f245be73eed804d94f93995cd977ce558c422b26fd38973299c2e69fc60" },
		{ "3x6TRFQKjk6iz7yDA0OknA", "90aa74ad78f554884b69caeecd05f6f72e22214d1d4b379575dacfdef30f4005" },
		{ "43NcHIcauI_T5SaMOoFMIbKGtLM", "3913537da59e928892015c0932456cd45a95f65815a068c0cd89414fb73fe2c3" },
		{ "44tcOrMlJpq_yaQd8M8TPSDWu4Q", "7733968afc99833244d3aa287ee61eff5e03e95d50bfd9b47c255ab722057b43" },
		{ "4ag6Dq3Pik660n_6z50vqg", "26aa1425947191fd3055902cfbdad79b48edf29046be662bac3422f1ca564e00" },
		{ "4BQckM53EEvwRp6VENufjTaqNT0", "ddfaa711043993261b573a1ba2b3d8dffaf2dd3db729afe380fa7e089c8ecf60" },
		{ "4eeEvMuf0UG7_Cv4FOivPw", "d47240a259ea9063d4a47fbf0ed951017435158ecc800271afd9714ba90b356d" },
		{ "4hWuK6o4yEO9Y2jcuxdeayTCYaQ", "94535f8af4bd1d572219aa2627b312c5eb6524e6a6d198b7f631637b12ae6b63" },
		{ "4J9xapNCbK9NE4YYPkrWPyR-j74", "1bfc9c850d17adda6087cb066ce2ea82024368ade6ca0177aae755bc5a83345b" },
		{ "4juzapppuIP8GC8vu2owhW35TLI", "0b70de0f88b5ca5505945cd1e968c519936e0046ae387c6b8b6c566907bff0c6" },
		{ "4LxMgaLIw3IX3gF3lCLtFbCXpzY", "c4d77f22f74df0c07490ef68635ef275a403ca2e0b95dc5700fe9337ce616aac" },
		{ "4m13J1YaZhSzEb7Ao_FV5nUAemk", "2e7e4cbc80fc541e57ad83c5dc40e6196235102a784c72f6d2d18eb1798d71fb" },
		{ "4O2g3SDaDlXxxALGqY-pZJC_foQ", "fad3dc1671eadd96fde8ba0400edf8386de6fab3a6c53ff8a31d452a756be5cb" },
		{ "4O8qyVxjLHbb40vY3zaCwSwh8QA", "60bf580a0026159ada5f8d182c8f5790729d9039ae5de610b9b30cef56db7cc2" },
		{ "4opr2A5_XEKR_fqzazMy8g", "052d847c2af36b747b2a55bf758a89c5c5d8cfa2bc6852bd3045e41ff6da7b55" },
		{ "4QXAkHiZQbRD-CVZUSDaE9pwF6I", "6f6d9bc1046876d163777b67d86eb7e830cca9c00ce1cfb8451cfcb05d3c9e4b" },
		{ "4QZ9VEkeTk7Bh_YWQwtRHnapmmY", "73603fd0a066a3da4690fa4abbfdd148339adcbda5d1859c4c89c1c3b74ae782" },
		{ "4RwIMcVk8OqzbhDzpdkQZOvzQW0", "6f21c802a1067097e7c8f7b6bda7da84794f148be8405e9f04c4bb1ba0c5055f" },
		{ "4SUNxpFN_rcoTNJJ3pH2tgguwCM", "45e4a07ba795a73bccc65c3a24d92cb65fc1eb2b8fd65791a2331e2d7192cd52" },
		{ "4uCDm_4raDbRn-xEEtIxEFioBGs", "9ed866202a01d969d3b79c930dfdcd7a75fb2a0a46da35ba437954e51d6eff5f" },
		{ "4uWcpAVdM4_UHL6lk1Bg3FCIHCk", "42cc0d0ae814d5fdefdfb67e9021479d3c3cd9d5709581f037d2e16e3713bd5f" },
		{ "4vdN9KSGodDNv7LCcJrYdouU4cQ", "35e2fef84e60a165be057869346afa074668db5d0b008ec7bd7af7aeb0dfed36" },
		{ "4X9LcIvDzUa6j4F6R2NXXQ.meshx", "0d0a939b2e2ef5bd881848fdf8e6c2d21a29a6c60994642f8494207f5aad16d4" },
		{ "4xBZr1R1h0Daa7njTZksrOPgRxY", "421855bbf3976da630905337530d45465543171cfe0440136b296cb0c2e2495c" },
		{ "4X_-i50TiD-kkYlSFaPBMsnTgJ0", "36b1c8ec8400fdabcbd5bbf3c96c0d3b0f26e7fe45041a285958316bf62b083c" },
		{ "4Z691qAMDNuQ08iOdvZHbLlueg4", "1568e3790304891a0780d0593ea68163695ea27a8c606ffad7a4d3992249d5a8" },
		{ "5-BbDKQaCb8sURfrRcBxTb-5rOs", "559391493fc5f2161f8744fca9b7cd8bccaf0a1b6002aaf6e3334cc66a918e0d" },
		{ "590qxyyBoADNNPTCblaTt6gSHiA", "4f92ae11c843f4b2df6be80b55c5be03013e6b5be86892692725f92adb2b82e9" },
		{ "5AWR0QCEgRZhI0pzsPCwpO63d5w", "77b4ba43fd513f8bb1df3984dbd3f08e7acaf8f2b8e85b012c770774ca667af6" },
		{ "5BekSjP21FWc-Hy4t6EirJ1HiME", "af98dfbfd063eb0433d4441413183cd8e5fa760ceceab90fe18a733802ac6121" },
		{ "5BHGqJGpilP1T1uo-pyvk4rstO4", "8122caf9f1700dedf785ccb57e66d692827b96677114cda90ce8ce7dedb98d94" },
		{ "5C2PYlt14bTjXsT_MaQ-9j8fSws", "ab0aa40911c0389a33a7936e50c3dc075795414c0d47cfe87001025119ca4fc9" },
		{ "5enT5yyzdEi6iEAhRGqohg", "4c78001ccde85ea293eba64a9331913596f87d4cc622107540dae6bd51b56219" },
		{ "5F7YAgfhWw5iraXY7UbnhAjozi0", "5abf40433849919c404b464700a40edca342d9fe5d794dd51ab7c6c38c901a98" },
		{ "5fp9599AXx1GeRK1U5f55a2P1dQ", "cf183b06ef7f19d84a46259aeb60ba77c7a885bc5da5c6a07e3d8d898c13d0b8" },
		{ "5GbA9HYzBEKnYEwJwuMm9HCHtVU", "1eee896c1cfc590d1a0bc91750e840dec5522c57b6771897ce07fcfad61c5004" },
		{ "5GcOnQpn0sFEcut1hoidoZf4sGo", "a6a7d7470301bde9a3b3325cbf231f3caf8987ffc876fb758fdc43c0704b7f7c" },
		{ "5HevHU8hf2EOWpZQh-IRVe1rAHs", "744a3b9cd695ad12ead166852bcda836c9e1a84052bc7aaaa1e2b1a65a8891da" },
		{ "5JLNK1_yHQruWcTxqiGlzyBKYs8", "2cb073f579b6ea8e71f178f90380abd69dabe7fbe97c1a5a89c16c71a9e2490d" },
		{ "5LRoPvpBGUOrt4CcX2UlPA", "d664933ec27e955c2a89c9c9a7710f68d1441f5c42d3dc33ddb911a45b7ba5ef" },
		{ "5MxheyZVziLObb5pALPTLAmGZaI", "4ce94550ec055b009fc11617df68c8895ef636181c00e7eb02b736c6f99787d1" },
		{ "5ot0SXynj0Koth6ZgeA9Jw", "d592d47f99a55454ca1091c32853d80a7360678f749efab8d55bd85654919a5f" },
		{ "5RB7SMtQikuxaMrYMn5lmA", "d214dbb6bd9cd11363de77050acece2a2269b977a6d50e85df44f6b92d760983" },
		{ "5RB7SMtQikuxaMrYMn5lmA.meshx", "d214dbb6bd9cd11363de77050acece2a2269b977a6d50e85df44f6b92d760983" },
		{ "5s8_JITmcEuTAclcSZBrbw", "4cece86df7282376026f298e0751df4f391d2254d718660d2b2da71817635855" },
		{ "5SX375SifMpvOacKluwfXeHU0r4", "d828ce5d65386ea826080ac216580deaf3e0fbb23657764fcfc9ee2a254565f7" },
		{ "5tFM782qXsVoTupk_GF2y8Y1d2U", "63b848ccea34887070ccef9a4d1276de48fa16aa1f326b1fd5bba9bcbaf932eb" },
		{ "5TgEfuvKFhixlnGigWZB1tivXh0", "cc0c2513ea103c225ad429a65cef1de10c8f59016ae8c3fef299747303d72746" },
		{ "5VTunx8OljxNR4j-RWKyqWyhGoY", "91cb3005eb3ed3695cad92aa2084f01fc1ffc96f34486ee9fd9d972778523033" },
		{ "5XsyO35sLPPzReIyWJ9fMsWWeT8", "55f04ed77dc216a289b6df6dc7bc245fb7410a101e74153004cac6c71a507740" },
		{ "5YbsWaMyh1vMF2NBjirveIxm5js", "a37e542c08b8d49a966623873332aa8e0fec41079d6b29ab28ac9e24d134e148" },
		{ "6-bEk-xGV45CNBl7VIKREL2bQUc", "aaace57d6d521ba45b85413c9fa0807dc36c653e103ad50fbf26ddbe8d972b95" },
		{ "60uyZsJx8LCWrNM_Rn6rerg-ykA", "2c2212962ba76de515330392ca1a3c8b9eed207d6432dba2a0235311b7ce98a3" },
		{ "62WoOyggXUSwrv4pVhbGWw", "19a662b637b31b2612af33ba46d8eb178a97086beb46324f4f9ad9ee6b3566fe" },
		{ "63cJlkTsXQ6S0l2FZmYDt4WDrws", "4b1bf2feb9fe0346495fd9a022cb163c732ead34277311b434aadda04fe0e01a" },
		{ "63Mx7XALx9__rvFFBbrjUo0QaHg", "14a2853acea04a177366c7d150f2df3158758b4cd2b40620014328ce78f6b2cd" },
		{ "64Euu_pCkZkD78r2BQXIcxt_PuQ", "9ba1311eee383719f89fd49ec432ff80861c97a6d48fd53883c33c9c109deb3a" },
		{ "65RSi5kGSLVmjdiBU56ps2QCTmE", "ab95fcb86d9ce390bec0031b4893aba011c5a3cc53efdda7c3b31a9b181be842" },
		{ "683sLm1jZ31m_9QNhHXASAhLvgw", "75efcd6b7cfe7079de37e09ab25aca52b7f117b44525483de59e65f1916b72b0" },
		{ "69A-CoT3H0uA00-bPsAl2g", "267e500bdd8df0ddfd35274f393fed28eaec0b3362a41349eb979f57d3bc90aa" },
		{ "6c8GS_F0yQYV_zpWWCSHXgz8q3E", "b5289766eb975f1c0ba0ebe25a242358d498ca30759a6f2ac9aca94850138826" },
		{ "6CHP97lC_kOND3zG_o4zpw", "40f88add3ae89cd8f7a5b863121d80de76d4b7612273ae0cf75df01f9e1aba7c" },
		{ "6E73i9LjaXXetqFW9GOrD4cX9to", "9d05b9132021ab30c91f3288d379454f5300d0590de54d6e37a235163ea5b755" },
		{ "6eh0xFy2SNrdx-EuhM141M0Sz90", "7a5f4f1e110d72918016c88343c7a37ac8804d20d6bd74b99117daa947e6438d" },
		{ "6g8sNIc0zzYygQ9QpFZ-9EARMNQ", "8188d7b90572beb6a47c1a3290762c0ad131aa784e11817f0c2ac00f9e36ec49" },
		{ "6gG_e57XA9YPT_KrP57OuqJpmlE", "ee6fe6277787280e7eea933cbbe19bbf659cb17bb088cc57237b453ef117632d" },
		{ "6mrHnF7xcJdVkefHeMnYR2fXWss", "98ffab5b742a0bcc1938fbc7b4488f44442eb8dbf79764076abb04d47abeb92f" },
		{ "6nC3mlBNTsXUrSKBAeLdbW-0jds", "534fe87f1ab5c7e9eac513381e3b094651335cbdd368b4fc5546c44caa4821cd" },
		{ "6OaPxj47VkeaVqLZKGDfvw", "cf5c64fd450e1e721adbd6b1b84d217e212a35991508cb50dd7c942b6fdf19aa" },
		{ "6ojJ6g_bjo_6G8NVGH4yt_A4iQA", "dddf2383f1ac630ca9dc9eadf59098c1fbc791c42cce083d7b243e45006511c0" },
		{ "6OOwx9NtNzTQ2iIWl6NoqK80ICI", "c7a39923ec38e5a0ba8d371e274c86e3de6d2d9b1e98769d0f9c03e9fe2fe275" },
		{ "6ouuZs1T5bYeMkXQKkdhYTMZ2ww", "5ed7da462196711d90a6e2d1d2cd2524af014042fe60d9468d12838d083df6f8" },
		{ "6pYFWWaTKcD2cKgiFwrfPSTd0M0", "8106250724aea3dfa279764cd37797ca4bd03f7ee50b124ea0aa6866d6ba3199" },
		{ "6QGmxHckfxUzKCkB98eLzTlReFY", "4c029adcd718f732d898824012a848771ff7abf3ce4940ea6a9a28c6ffe6c5a1" },
		{ "6qHyTqMQ97eknnxdh_pCH3_3g7c", "63f7a3453d096876f9f1ca17513d42e2e4de55af7ca70ad8dc6506ec9b34d1f4" },
		{ "6Rw8_5SzsQPVOFj3sHRaL4OMcX0", "9574f3e35c064ebf7a49fc1328a1212242a8fb9367c287151d1ffad9d4506ac1" },
		{ "6TaNSqff3cKS_YSgw_BiGuteGzQ", "38a15c6d3b6ed07874792556b240e810308eb03544afd5d2c9c13246f083980c" },
		{ "6TP68gO3qEzIJqyOclrxmFYpwGA", "5d64288fd80673d8083e43a8f6833b760d612898c5d1515e45c6477db4950e74" },
		{ "6xoSJkS_b-iv5nrdUQLwVfa7xww", "90c8ddcbb3a143e253d0fc68a122c64f5408e055e35af630bc1dfe198dd4b914" },
		{ "7ACK-4JufgYJVZUkGKeWG7l7LiU", "1ed99677888e9db565380e31b84addbae16c72cfe383ed6449222b79acf8091b" },
		{ "7EAcRYQSr0K0KHutmeW34w", "250303ad1f10a0effb84b8cee106a00ce88ee47746a329e248cdb8630e07fd8a" },
		{ "7f4gVgfVge_LOmWq06bFhXNC2bk", "68d14f183234b18d4e07f2cfb97eb21a30ea3537b07063c83eee2012fa7525d6" },
		{ "7I6xc-qhX2WPZKDNfdbdq1uvXZU", "80ff262057202b13c3c3d891ff90d926957ee179985f095503cfb13e1470a8a8" },
		{ "7jnpPfBDEFqe20g7DYR4fA_sYKc", "da32c05981f100e5e9b9eb40e40e64b546d6b27263137ba1a5e8b108b5b393da" },
		{ "7k_G26mz50ya-PPRLq7oyQ", "adb9eed673c9d7f78f34277dd5ed3b8635768fa3bdd25b71fc9b56bce7778fea" },
		{ "7m_Cnk7l1yBeVvqPjzuL8OjNjko", "3a39ad6cbbdf46ba2d5acb8267f880b532a42d8e3ee98b7bbf037dfd2d76a1c7" },
		{ "7nQ9GkYGTAG0-5ngh5NcMaRRnUo", "49aef00a8cef46c2c9f372ce4977361b683251cba2f5a455bd263e265a9adc9b" },
		{ "7pofgrwvFpuDumY1mVuctPW7IcI", "753a0513e4283d7031397a95a19ece6ecb5926121eede7b6b0c755cf839a2915" },
		{ "7pvzFhvqzqBWq_Dlx_qmN1iOfAo", "d5a5c721624f73263522a3274d617071531bfd95274a0d931d758c2e3c4c1840" },
		{ "7qEU_R2EAi6fOLnTiXBw2GMPkus", "ea46e3615773e15db85f712f47d78a28ac417d8f9b238e6a1868dc1557928f81" },
		{ "7SxP9_BOaUSZzCCp2E5PDw", "a36614a14e3a57f64b7cd22182cd28e6a00a40f011b826fd52aa1cf91b7082f6" },
		{ "7TzKFi-adTYRXX2wuDzOzqOQRaA", "c2f373b7048fcd5fc64196eb1eedd6d478bcb0602f5d67a38b8255d14457bb07" },
		{ "7uNqorxAqJvR-FCU-UGEAl343Kg", "6551b2e7df2df31f1812413d9f330d1bfa9c1206b5167b2341ef1d5799f74bba" },
		{ "7X0yBgOjniLSOBRv0AUiz9GBsBI", "c3fb5a8e4873eb34b4980942fa0ff2632b47b832244426637db5bc2ad958d8f5" },
		{ "7XRkE4jZfvagkIEz5Oe39ZCNIjE", "d95f16c87d96b674859b12f4f5dfdad0bdc3e0a8b56c9409ac82edeadc1b4dc5" },
		{ "82OwgoCiYgafIynFsrm2X_9_WTo", "d12f0b51405847beb73ecc2421d27c01d391442cc3ea391d4c232d9f5c8e7ce7" },
		{ "837pUoEme6gm84WFFa96J4Er0dk", "207c052a37032de634b9f78683481c4074cb9389cfff41cbf17a9addbb44ab5f" },
		{ "86UoFONcDMHkNj32vhNfSEJXags", "fcd317b4523eea725fd1e65b9ef21edbac5b56e333a1ac599b74223567eb6c05" },
		{ "88P73n7ej1K9RWiI0HbGjj8V--M", "b61699ba3b79987418c2176dac1bc13aae0d718ff949171176afa48494290c8d" },
		{ "8dXCZeBO5esFEC9gMXzyYrraQo4", "a512a5ae0e07e27938fddf1e058a406fdc7ebb6b7e32c5f763ee395a708c4b77" },
		{ "8eEBrJPW93Rto3tEs5IETvPB4xI", "f6bab9ab6dc0ae58bcff3ea56c8a90977f3605147d3aa06db6682f0375b72402" },
		{ "8ineuL2tzOWTGqNfZXPtI6CquM0", "5af32ecabeec06a2f99c56d95f88dbd0ee5107c37e75e0a54eae7cf035511145" },
		{ "8IRMJn59Q0wGXuaeHl_Kalu5w6s", "896df7920faa203d715f8707cacf84359bfbee5154b1c9a447f922ed1ff928c0" },
		{ "8K0TIxgqz8BwUkWWC8qEfXVjh7c", "54f8c49f8a49ad6783de761789374594835e9b1eaec37c8d7eaac87b095ccaf3" },
		{ "8Mbzt6aXzelIrt7i3CMA5pfU54E", "ad6b666f7412958ec202d59d60b74e9a7fe1f131e88c1955262c0879ae5e0aa0" },
		{ "8qNUmJe3YOHjhP-jnCgF07JcDB4", "dc3233ff922a776ab55c4bb0e75c7cdd76e28f8fcd4b7720d020cb3458d3b88d" },
		{ "8SssFw4MiwyMV1sMe-i7ROxHUNI", "c9d5bbfcfab02acd5f240a6473dbae1d232a5e73616a548742832d3c130cf2c1" },
		{ "8tEPxWR51YQuTTHdoGvkd3upTWA", "91d1548ea7e4a20fd5d59e32e7927527940429e4a679747e6b537ddcef6feff4" },
		{ "8tZrz--IvIDzShXlygSigSum2ss", "ccaf1b88359fd4fa8557272261694c0f4eeebd1c7712bbb93fb313d3055fe7c8" },
		{ "8U57JKye1aVBmipwnLP7hQHo0_M", "a4e6ee22699c8a2b2dcd2a612b7f20a479b405b2d6997cbb9971862ec58b83da" },
		{ "8wRi4lZBv1HdWjl6S1-nTAPj-wY", "dd5a6811e47f0882e61110a3d64dd57580d3b89642b7d73796550a4b06e50061" },
		{ "8WZP1odO3S3Vy1vqWuZ6g0k2v9c", "5824725edd5a986f7793c0239a4ca09124ce1d7e5df6ee7fc89ca2fc4259be99" },
		{ "8xmRI9_JxznEaLgj9ah3sv9FXZE", "38c9a74d65122cb33e3aa736013c1e47286a47be121f698174e8f6ed2771f891" },
		{ "8xRAoG_UYQpr1MP0LF_3qtAChQM", "170c3ed4bade939ec8ec95192b8ce40e17540468aee5c169867470b2456ecaec" },
		{ "91JjQhM48zhoI3TXHLJb-lRlPSY", "e7fb5684445b895da2010456047eb37ae41b97ed54e6ec1b8d0a7815d6d0cbf7" },
		{ "92czM0ewASHQVW1m8eUUL-NvPW4", "4e1966c9bc87a04d8c9f07d279c28439384ad2a6048a47e80a8d6313e9324cdb" },
		{ "92km--h07sHIheobiaNHt5_ILa0", "010bb2c126fac7ca6e3f8642bd56e827a1fc404bed2d883f924e6adcac5ac0a3" },
		{ "97FZ1icHpSYCIm4ZyFvqaS695tM", "ba7ad4646b5e4bbded76c34066ca5155dfbab337158d0b44d187387332fdabc9" },
		{ "97PGXjqyzYGvqF6JpCnjnXU4pDg", "afc26e97c3cfd725b6baf14197a19336c95937edfd109d88a4d6e44dd4c6a03d" },
		{ "99FbxUa7C5iyeUZgCX19_JPtke8", "f839d9b14ce185ac2d4d1bcc1c9502d95aa6fe2d870fd1532443ebe019721061" },
		{ "9bf75dvLWYKvLhpBWJXNlNplk34", "daae8d2728d1fda1e34ca504483a851c0586f3742fcb552a42ac9829620befc6" },
		{ "9EKxfJhzj0Op8oxtjeRypA", "e8384bdc807055d9d89e1e2e52ac0b91a6d64b31f0f315e84eff6e6303824eff" },
		{ "9eyK1ums57osd0gn-MkIELcXXV0", "19d250153716adeff68068e87e1bbfde38a4ab7aeda0feec6c1301d25c5b68d7" },
		{ "9FMbyEtNqik-ewnU6bEfdpEJ7rI", "d1c8c9e9bcf2a5173d7828b86f05ba3b3f0a1813f2074156c2313e3935b76efd" },
		{ "9GVmIc52JO_ahU_99qheZeU4e_Q", "d0d0e8a40fd34af2e9528d517b1af2d5fe9c02d38a553d21779f008d7ad65bbf" },
		{ "9H5rDpb8Z0u4afFRZLBVvw", "abea684a9ba77e54df97ee2e75f18aa615e23bdeaa5c804a838d5403f48cfc7f" },
		{ "9i1f2qZBrVWajZuNi3VPfNXp9Eg", "4f03c6ff9d2a7867f30c94044c55fc8789617a3fd5b63768668a229a3249e814" },
		{ "9K9ft2r4MqIv7zWaZQio0Ve2URg", "0b24709f38296c96763d82c0f0dc2272a81e4844739ee2db98fe5ab07d32ddbe" },
		{ "9kdYIV5r8USnHIpqHSrYEA", "fb0dfc24eb9db961abc91108159018db7b23230718ac06e47536d65e64c8895c" },
		{ "9kUckvcRs7gG-TLgdU5y2LOsoz8", "868fe27679aec2d71e867f67fbb43b001c45866b778ce2aaa8c0a722f823651c" },
		{ "9L0tdZRTdTbmaj_403tZlqIzbnI", "7932e59f6033fabb88258d264757284585bd73f050941dc71898b401bbae69e1" },
		{ "9m1jQFP7htLAoTAfeehhFYHUlFY", "7667e7f1bd7e4fd834fa7db17dd644932bd2b3d36c09060190f4ff7ce020d043" },
		{ "9Moacvb2WyzUFp_ckkqTx5vgf-w", "ad57bb10c98b3189f27256e09cd92daa5c8371a1dfe2496e10b3f65dc7935f72" },
		{ "9OQLn5MC3piEnb9hSyQcNis0CZ0", "80f10faf6b60742818c80ef81d58a3b95b6acd83f57c50e3a860c49e04f2b30d" },
		{ "9P8JNZ5SId9UUvJIe-djb3u3b74", "07adb35b0673b54a6d7090db7a8f7405ce8272c4251a6247b6e2f914f30c78d9" },
		{ "9pQGTDLoEa6kEVOle29XjeQZao0", "50045a6bc6259daa3dfa38b668d72b85f47298a0fd21e98cfe1b688f2ceadb99" },
		{ "9PyRgI2Kh40N8NomAUpgO8NATsM", "40abdcefb41ecf504271b4b50ed23d053019185b01afdecb4d44701811ecc543" },
		{ "9RnDe_eLF3Rpj1cqcSJNHrkLgIw", "67e9147a0c14106d39449bab73a9bfdb12fdd0b6b9f3015d531181cd09a444da" },
		{ "9ttqJA017qRvjQRzgxeARKqaLrc", "3666352fa68726acdcf137db1248b9989bf85ea11ef2be894be601096c8e3155" },
		{ "9USUSsJtRWLTjG-wgMmAaWh0KwA", "c82911a3999bd66f052f43c78f2979c977d0ca2d4e16ed2c418c5be6606b5608" },
		{ "9w1eJevmyR7h3ID3KfjTpdMwyG8", "b0fcc2bc5f6bc2003a9b1b740e6a7c0694f645f1115adf364794abbb8794df14" },
		{ "9WKEmnBltUarLW2FH0_1gw", "799c4a6c0659f1f927e3f1cbc12fc737376ba560cdb20223694a29c0793c44dd" },
		{ "9WLO3nlNmXaU-U-iFJVbdxrsbsA", "fee0d1494bae15ff9488e8c49197187b2ba2594df3b2233730070103b1d7489f" },
		{ "9wqQZt5yVsXCzgsPlYMDy3cJ03o", "db43d2c27e0cfaf446ac3279a91968406dfd91c7720ecc96e3e4413e09af4528" },
		{ "9y3368-AVk2P6EJYtFJFMg", "6b8e20e0bd3446de7ef040dca2d59260ee425987359757fd1001c5ca1a95a506" },
		{ "9YyqzVLwxSeUq4iLbP27VH1qrGw", "f1a0cc77e452dca6ea5156b7474a6ff1d6a67c76f1f333eb75ccedd88aa0869f" },
		{ "a-2hCAVcnYRRgxUosydWtoC_n1Q", "f4eb6a4fe635021cf20dfea48073d4ff8f41613e1cd64e88214455c775387d6f" },
		{ "a-HuuBj3MtmTg5YlWflGG2SGgEI", "2e805585151033da0479b15811913e6e386ecd7ab350921153f93d863c36b586" },
		{ "a0FP8xubBseDy4zbZ1VYAy3MXhY", "bb224839b829900891a4dec101ba07e5fa5d34e6c4d0fc0cee1faf6aead2ba7b" },
		{ "A1UDTo88UI1hws3R9joZKPGgoGY", "352c610d963e884d2fd8548c849544504b46df1db09a7bf9a201e4ec86c04fda" },
		{ "a3f2bRkFnWidWQjHCbhjyIGKwJI", "55e41ee0dee7d3396d7d79485664ff04fa7b9c3dd21598affd96946ea0f7d5b5" },
		{ "a4vRz4jZOs3aBfGlgyEiFImk0D0", "1047556e44732e231a73166e28d4eb67bd8ce3963e4f66a8dec81b6ab6a03c6a" },
		{ "a62Zo7pWwAtE1JaiEw68I_dsBso", "359eb8cbe0971922f6817f618cf045ea12296a75889fb9fcd7657cc3d8235615" },
		{ "A71Ok3zsA5a0sSZjzXHGGykv61U", "abc080c4e4b71c669351449dcf7ebb36b932836c1adf4f35e91a688407c57164" },
		{ "a9gJEQs6NR32N-ugzZ70VSQX87w", "350ae8bd5160d95cc05cdfac9689c54295cc95307332066a182dbad9a180b341" },
		{ "a9zTmXovhupxJ7y43KikcSY6UW8", "e970afb82309fbe58504e84a315bf10d69d0fc32ae928a2b23ecba1c346a0608" },
		{ "ABZmxZbegSNPME0Ks-wS8Yzro68", "2d28ad11a880dd87e9daf5708c69e7ea71509bae7fc523979051040f2dcc13d3" },
		{ "AC00NGilh0yuPYkJ2OiOfQ", "7dbd6e4e4b922cdf7a1f1b48155cd40d4861d9e2777e02a90d7b79fd436959a4" },
		{ "AcykxUoYabvGO9RBN96JNFpCUm0", "ec612961060451667e6ed45f0c9b7a8c23447336680c009dd6612a22a157e9a0" },
		{ "Adazw2HK4Ep6uwVy6qKfmjbl9PY", "9c601b0e05215605a85eb643cefa485a2624f1ed3bc5948e0078f21e7ed5325c" },
		{ "ae6wMfPnLfkWzyq6zDD6LZPDYcM", "9cd83f4fa509d3ac013abcd17efb581d2291c9627d25e5383b26b9125d855ef9" },
		{ "Af5e1apf6-Kty7YO8AaABBPNi9w", "4506ece68007a0d8424278e84d88cef79bc8a67ec1ee8359f5f40a06a0e0b1eb" },
		{ "aFNZSGLxAXasUgcL57krId26uIQ", "e8950924df6d520fc8d53d632440db2ee5ff3b6318edc14e821a90a41803cf86" },
		{ "agOMcJeG7kSwLcLoPvu9Qg", "8ba96356d790b52c6a0f3f2d7185d9d911c45d2d596d915ad07fbe28285ea1f0" },
		{ "AgqpFM10LF3jOyUHdQF3vp5mr8A", "f9b8473852c4693e80e19d6b4441d6702eec5ce3d320aacba5a20d888052d3ac" },
		{ "AGVSTRwucaNoFWhbJMSgpgf7Reo", "e3a95d1798a2911e9cbd844263a72a9dc5940fe1ee61ed9ed0e724b50f3e24ff" },
		{ "AGWR9r33syQQ_k8dmqG9YObGB1o", "a4f0f337340ac0634c4a13f5c6d57311c3964d4f2e369b8581bdf170c5442496" },
		{ "AhBs41eozpNWM1CtwmbUeRRsf9A", "c3c7aead0cf30d4221613c668e56098762c7a9c010c48f094603d83dac9b3d6c" },
		{ "aItYI6ZAsP_fz-fvU9piJh3vL2o", "5ccfed850a5101bb7e0f5dbccf6721dbc0395a1244904c1755b35c2ca059f66b" },
		{ "AJlst9oHLy5umgS-43KRm_bVvpY", "97ae77f1554c5e136b89ab78cd81e4a7b4117bd8cd29e11e9def33969f1c842e" },
		{ "ajm4a4378sS7k7U3DKYH0zeGgNM", "463d16afc013c34c391c8edfa914c58106f72305d7584e9deee5ac7f7eebfe32" },
		{ "AKfljYe5O6NcLskGn-eJC4NvhZ0", "2b17901cca62bd0680d46b6678328dd0a24fefd05a8ea4ef5cb8b315bad3cdb9" },
		{ "AKo-zzO4IQpUEBhS1BXsAMYcobo", "699f771b0bd9e01ecb269c8c420d6d9eb35801c8e1d3503814a61d7a00374b20" },
		{ "aLmgngET9zBDdDWA9Am8-hd0nAQ", "f054ce357fc57d582dc58cec35a0604f6387b3cc9d958ff64d9c3fe96940ba1b" },
		{ "AQObX2zvjadNOpD6M5D2AtHyJB4", "f21543d9518b44657254a3750383b98faf559d3db6fb351b640203ef101fc060" },
		{ "ArcNqvxg-czK77KepGwwLQ4my4w", "2618a154908c8af0a3aea4753b355e00b17a2ad90f257788278641f3db056aff" },
		{ "aRllKF7pF0y48_vaf-QtQA", "ce2d29ee5a8fa32720d4bf68884ab23f88340d615089b4da175682f25fd27182" },
		{ "ArvwDNzJtUCEoii83WTNfQ", "8012644de6cc6414e6ee6ab88a928843caa1853b571ed96771dc2ad2c92298a8" },
		{ "aThhw6AYhE5Fw5UHBDpJOBbu6uo", "ec4745c20661a43f628c136620ebda02cbab90c9ac739823503b98357180777f" },
		{ "atmJRCasqFcFKokC1_WpchBdbXQ", "638a35f2fb1263bbeba29e035e2a627d174591d12be56f30f1133359b315d8c8" },
		{ "AuLxquZngnGcMN0xS5mAdXasTJs", "07596565b4f12a00669c2db3200d786f64915a0e2cdcc3d5f4f925ba71135b57" },
		{ "AVdLemzoRN3hmotc_TelykuyVIQ", "2db4c70a429adc4279bf6f69007432bb7ed98b6566535b3cfbb05387729dedce" },
		{ "AvrdNzXMLPhDtDSzYmpiZDL3h7Y", "1a2f4617f8ee7996bb78027180520dbad451ee4efd8cbf93870fb1f4f4c1e801" },
		{ "aW1566M_Z02ppOo2elU3Qg", "08242024534c7ae9df436c0a69189a54019a3be64c0f14d32d856ef558483f74" },
		{ "AWxQKh97Ln7BZMkWxZ7LsaT4NyQ", "94ef58f00abae8322ed6aa04faa97d19bfb50fcbf43eda16cdfb194280578557" },
		{ "axBcFmozamiySfwxZZEsj6RqBvI", "e234577157af5a85bd621daea5ae0408f89601012d5f0b6a56fb758eddba7f4a" },
		{ "aZE9HkT516EOddyHGyXxYybn9bk", "f790e1ee3035f2fcb41b787d1a67035b64186fc2e79b786f2ef4e0735c5b28e4" },
		{ "azq9I1uIFkimtzojHD9jIw", "128dd8bbe7eafea31aa4b7a172f71e18629e7312ec481a370339e1a9fdce65c1" },
		{ "B-3iy6S4kEO5MwLlYDsB0A", "08fd9fdc1eb4ea393ec61223f9fa1bb859c8216929dc4f6a673b0744b8443293" },
		{ "B3TzVWpFiwMtMro2zQIa_DkRva8", "f181fe01dd72c5c588641a2dee482563dc93b7518096733f17593d44d1654a75" },
		{ "B8GjeULzqToy0yvmjSSjAERkQUc", "94705507b51cfe04337540de9a7a537eb1bb89071867e31b47cd21e4cdd13be1" },
		{ "B9FPOxBUK0ewvwOqeN4svA", "1a9e81329737009f98131ecbb4fe6a153c028033df2e72eda683e5154ae9263d" },
		{ "B9FPOxBUK0ewvwOqeN4svA.meshx", "1a9e81329737009f98131ecbb4fe6a153c028033df2e72eda683e5154ae9263d" },
		{ "bA4iv9lhpkirvE4fVlk3eg", "54ed709b4f981e0c862618af21c30231582e9b9e68c338e181a22d3f8a016d88" },
		{ "bB05_UQAY2GEYiHTU07xVL16QOA", "548e47ef1c327ab4a7e05d6a2c49a56dc2f713f773d7fc83a38690bb1340943a" },
		{ "bbC8-62xpWl3MLqRDc0Q1rjPPKg", "58b726a51aa50f85e65ce26edf777f089ac5938600cb25c340a37f1d13d51f0b" },
		{ "BbgE-vvHlA0bOIFjZoUjr4eRx-0", "2ab537da1a8e2ddfa49db05c78c0c6f9dd51d0029051ed7bf2e3597843639421" },
		{ "BBM1I68pgJjtW2o02e25H_BvUtc", "13051b63661a127ecfb33305d6394968ba4769477fd10c8e792b572c43ef7529" },
		{ "BC5kHrNwTScZal0qDx3PwRGaQGU", "cc8b3385f692a922f5a657e66fd88526d41dbd8d2e41cf1247f0866504bf5d0a" },
		{ "bCIAAq2obWsElMyPRzdShJcmKIo", "305a3e0a4c5acb3615df8151ed45e2d9f8e1af0a73a2d138d2be0aa648c00497" },
		{ "bDR3v25UeEQPwVGzHER_-MkQ-KA", "f43ca46ad09b1e94fcc93760fb5c1f4681741fd715898ca484c9b19494218ac6" },
		{ "BeO-do3hkY37Y4p9icilyS135Ac", "7529c012c3bef118b1d4a0b624e2035d44e4263f85cf344a9eab6ea707f783ad" },
		{ "beX2ijIy_UGDsGPsnhvwaw", "0ea7894648c1dc8370f461a79f916271dd79b29248cc51bad94d5dc89a45dcaf" },
		{ "BEXd0Rg0v0-cuqQE5-ZQzw", "5d18c3a5447c236d5f1bdcc490c3bb2e556e6fbfdaa93372585358ce22734a59" },
		{ "bfLBM7K3TSqIRf4SeuE-rGlDjP0", "b9afcceac81c7a360935fade5820fdc0b3aced60333041495adb9875c2129ef2" },
		{ "bGgqflz-dallJTfVrMnAimonrcA", "f2538d35d8cde7846abfbea2deaca6328edf5375a80cfa28fef84575df7d724a" },
		{ "bGHy_OLgswth4Zmm4YOLML87aDY", "86de4e72d5ac70ef650f128045567a92c1826697d1622cffc39f912cc862c79f" },
		{ "BgrPDiW4dQO-EUoLhEftsKrmhh0", "810931e1a1949ef265da125095d3e2077e24b194fe5bfee53d9a07e5cfa4ce47" },
		{ "Bh2ftXZxnhrRetnUvu5j1p9KIlI", "e8efc2798c51498a1ed7be2e58bd8be47b4a95e1afd1ca66da90a71819f1709f" },
		{ "BIi0xWA1sXgE5OBYZiw0Q0BWZ90", "8ad0eb905b6013d9ba12654300eacd5f258b4dd5d3b1addae5a92aee2ab55e51" },
		{ "BivZ2up5Ooung-rkdX5_RwUl8m4", "71398fce0fe6c7302c2c400d67a874511419977ed9ba6e9433e6f6b5b17f6799" },
		{ "BJ33Ast4eRV6CCrFLVihrgdHYGg", "dd7a30b0ac8318c0afc5da0674fea84b5ac6a70cd230e4fbf7ab88f40907f137" },
		{ "Bjag4qadRRYwCDjWBLVarZGLHrI", "4319aa41cfb14a64cf38eea18a7943a6c8689fc2e54f532336ef19df18bfc04e" },
		{ "bjDjHF6WmTuaDw4JUkCnwe_N2pg", "a6f8d8ae45cbc143fbcdccb0bc3a9835c911c37446a96fe2c316302eb33815e2" },
		{ "BJLc-kEI80WvLhDeZYCGnA", "2e8cef0463a3bda9f6d7a283d4d138d87f1fb8bcf47563d38f7bc03d67bd0825" },
		{ "bJls0x5ku0KjXl8XyXKTnA", "e7a03e357937a55f85de931e92986518a1d1444f48de621f30f841df6241e1ea" },
		{ "bKH66oWh54Kg1aHd-lQqtNmM9dE", "6b98224193713a1f948ada3eae82dd1eb496e73efebe1ae02fe660d308a8ce13" },
		{ "BknGi6WZz3gKlgGhEhXsaq8ce04", "776f80952a378a6104c4a8dc8ac1c2a6ee5827432e1ec24b2eb4cf1caf1c2738" },
		{ "bktAfdSaeFsfAyFwCJoO_UHDvZY", "887715bce771dd37dde4f75246972e0c6ee52164488928da49c487d5a78377d8" },
		{ "BL88KBUhUEWbGWZwxDm7_w", "7f42434e38c96a8ed2bd3f40cd622e9d8e1b0e8932cd57e877aff6ce44da3538" },
		{ "Bl8l5rl8CEajnhq2rQjdgA", "5b8fbfa0f973d6833626cb5428be4d79c03c1375e8c48c1598c2a04eff20d19d" },
		{ "bL9WCDFXMTpmYm9mk1R0OQrjj-Q", "ac93552aee201887b6544aa73c5de82e2cf0e888335382933eed63a3f8704992" },
		{ "BnJs6thISAcf1OY0AGuOnPSHqow", "12f7809f1c1bcf63d2482587a8b7fda94168c12adb20c989466ba7a7ba0dc73a" },
		{ "bNqF1Ushhe1IxrY5njxdT2agaVI", "90bebd5b43d56c387d08db5144e0584bca9b28cb5c1f31c4eb372cda286c0270" },
		{ "BOrEVCrhSVQrXijcuToo3-2hdXE", "0fb201336c83f5e00d21f11e9700eda09b9740d623e633ed0caa4023a7938c94" },
		{ "BPtV_OKo2JPn38IqT7hyNv3GLts", "ee9b9f43cae477fd64230e8b274f6bbf9d2f711f69baa156e04889f525ce7536" },
		{ "BqJF6JA_2irFX_bw1dxne5YmD9Q", "3fcdb86e239d02844080f6ddc26247a83ce4959ded81bec33b43ab5d432a3d28" },
		{ "bQtAg8HSDK0g4CeAkZ7wuHeUTFk", "f19b323964b340cab23ab0740257e18bab7ccbcf0c20ace4e2b23bc6494bcb06" },
		{ "bsXtZVkteUClq6FaCUdZMQ", "1030363f7d34a13e084cbc7f02b6ea67397e678841b422b58e1e6973f2a8416b" },
		{ "bToH30FT0LpF6gI8yyK3GSgMf4Y", "93655708239442c0fdd91d478f76083c83afa995db4d544ddba822a27bdd4537" },
		{ "bTvOYdIfNJAzp0-jqrY5rQKZ-Dc", "8aa608365056539582a30b7dc6a2a08a3bc47652490da71522479071de554437" },
		{ "bUGcOyXW_k6FAM6j38gpkA", "a4c4b4cdca57c788b947d643934a6f4cd0303b543dff5e022d6c4ec42330b29c" },
		{ "BuKUS5vrU-FDCvb9spnosThRMq0", "c1c11d909d69c5d97b6bb57ecbc589de009feb13cfec2ea34814cb72ec867736" },
		{ "BV2B8Hm4mRdhcATBs4JYWgPs4cg", "ffad4ec8cd61c8662e954d03d8aa81390044378e191dbc37e2a1a7f0623336bc" },
		{ "Bx8DviZmTUSZh2mNuHq5qQ", "6728727f68bfe022ee32132e69f721700a89c1ee1019b2d1d8543842289f398e" },
		{ "BXq850gM7kqVZz38te8CJA", "a6f89c3165460de18f38da7bd1e9e9192e5d06199a26abddd84e7c60b6b9d059" },
		{ "Bxu_BC_HBEm4uKuaxd4h4A", "05763a061912459180d94caab05c78dd7a89f6b4c3e77ce4476ff5c280f0ecb3" },
		{ "BXvTijB5EsM1Kd7DQrH37vPHBQ0", "5bd85cf078e08f2763a4f90c81188d323d46362ed08714b47878705c82904402" },
		{ "bY1qc3c1qbns1srE5DuUWdw4Q-Q", "96aa6ea96f3a006adaa1817cecbe64d241954fbbb7aa83d22cdc0e43496f6148" },
		{ "bZuBNvkxhaBHquBChWPEtAdJFrI", "d5c168643306a493f32cd8c3875dd0fe9d98a8cd63d372b1e33b7aaa84ad7220" },
		{ "BZWdm0W6xk4Pt2VmM2YUPxNApwM", "85939958410ddb0abe7acf313a499d1baeee2496de7d8239ef99a468a0e3b743" },
		{ "C0-zn7xV7VZmOXSGT5CZ2c9IY90", "7cc6765f9c4490106bf6ac3b07f0c298d0ddde1504641b2919ecead4a16f12e2" },
		{ "c0NM_lHFXdxmcDDY_ayZ5yhOGWQ", "13aaa86fa49007a2691e4cfc8f936a6bcac26e07dfc5da015b2036ddb4d94db2" },
		{ "c5nJAWDabdK5HLudSdgdC9lHZi0", "c168274cffcdf9eba4a2ba3940f1a36a04023a6922d618ea60e46f3216a7f0a4" },
		{ "c6p1fewNluKDppHJITnlsft8kqQ", "7fa20c167cd6fe4fb2039f25020a6f90c596208124c6f1dbe35f87790950c097" },
		{ "C9ftcFYwdc3pZLFPc--rv2DsZ9E", "91aea995340cab5f9a7c95d8aa783bd636c477328a612c684a75f0a9b3f52b64" },
		{ "Ca5ksU2Ke1IFZxV1nWB_cppzFXo", "df0edf5ffe06d38a00c8edcf5e7b7a50397ca9adf631e67885346a0423f5a120" },
		{ "CaNP5DEgIkO14APjYMAq-Q", "65e2f3531520a30bd855361937b12170e7ae5211e50d26a978fc08bc52dfa94e" },
		{ "CbBxE1iq_sj_A6UPwkRUhg48cUo", "b4b3a05cb467d91e57f243b3f8c02d3b0133979c0f067fccfc1ccf653b3fd485" },
		{ "CbMFa6016eO64OOzVgOflb_TLR0", "b889debb3b5389001f7be05c6cb5339e0168ea8efa4fd5fd644c953a6ae8c739" },
		{ "Cbq8squtloxCTxWoj-pHVyNmXOc", "caab51c6caffacfe220b1148347a825c3e32ad404f46eafc9ff405b94770554d" },
		{ "CbWnOXsq5hY7BOdDqy1aeBCyrAw", "6bd4ff97f632dbd0c4b353db56edbc5117eb0113308c4af851fd6a6f4d57d70d" },
		{ "CCe_CHbdxfoO-F4o4rtTXi2vfUk", "3bea458d1dc18569a235711f80d12b3ac36f489ebcc8c15a72e3e420f1533553" },
		{ "cDJvahQnuqK_8laRgK5r-Y6Xt3o", "4103963e0e546041d9b66e3b8b575b9996c9c2adb0625f4bef5604fede8386bb" },
		{ "ce29z84iCGIrumCaEs4dqej5lWY", "da5685211c9ba3be455df3fe811c3abdaacee58edc020ce98e09d08b17c636ab" },
		{ "CGDOjoe7fJhgp-PXDHbQWJq-JtE", "29a2c0789923997409346fb544630bc26f77f2dd435b4e509289361a8d4e4198" },
		{ "cgjYCJJEHdDAy26Z1UkgxQFq4K4", "277e991d1438a7089a01be889bbd00feef666611c464d096c29a3a093adee07a" },
		{ "cGNHbz4vjxGhHHdGfOihfirxfBI", "547c0965ba79c1470a6923eefb98764e0cce8cf8894cf53d028a78b128548d11" },
		{ "CgpriXgSBILsMyBOIePhpfQ9eBw", "fde639eadc0d9b6c7052df24624e747a62fad6c4d227e133459f5f280673bbb4" },
		{ "chQx2YrZdoF2I2ZcavgBetLsylQ", "178afa829cce91171dd03ad87e1446a3ee33c8f4c4be94f1f5d4d77884bdbe93" },
		{ "Chs3ysCsnsb1IyK_nyOnH8P5dRo", "a730de2a5ff8e1b1a352967ea21c3508380d8f7708621e88c24261342082665c" },
		{ "cI48WcrL1nMWsSlON2j3quFVKIY", "cbb0477af7c199e016ef770b02f89c001cc087eb6828685d5512e24d3f43d913" },
		{ "CJt9jQFHiagiOa44BdUCmmrHR8A", "4a5b27b8da88a349d725f692aa5f3f2b1ac8af3e33b43db4eaee331ffa82eb63" },
		{ "cK2lQIKq-iLU3HSivb8Kau3BWYA", "e8a6f7e791f37a9eceaff8e321f3d8c54e444603ddaeb15d6d3eb4dc0dc5299a" },
		{ "ckvi2-qbg6jlLsT7cw4-34v9EmE", "cbc225d28a825317b61ff115e74efb7c152d20d614ea1de61c94f45dfb78dbde" },
		{ "cl2P3tpwo4sp197-orcmgBBit40", "c93abab8097f94f4bf7cd1dff44822aa75172ff4596fc8f67c225a940755baae" },
		{ "clB2yeXTIBHtMsyHidkLqDg0ytE", "57d1626079ebdc1c65a14af86b53c9703f7abcb1dabd194c3e1fa126a654d1b2" },
		{ "cldGlUVVLj-1Le63UJw5eXP89HU", "14d57f7d56e4ecd4957ac42c33fcabddac8cef15cdb400ef413dc462c9a09ccc" },
		{ "clJ6EGhxU5Qc_DOKedmGTQReTq0", "d0b0ce06fe3109af5388b351731dad755d44f3ea150535c32ddb32a97f6365dc" },
		{ "ClpK0fXp-S6N1YyKcgVSsxpjJvc", "04de5330aee9b06a23c1f201cbce47e56188661888df79fa0a24c8053680a672" },
		{ "cMkHn0Oc9jF6z_YSsOM9t_8-niA", "bc06b2c320997744e036c03a860137b2d01faefe3a894e4ca0e07c27cb480188" },
		{ "CN3dCVN3JCttoxL6pQB-iY_pQ1k", "afed9b2be7faf4b7447d2ad3496a47f49cb1d27a67ee8871ade9662ea35466a7" },
		{ "CNnrbdYaqJPPxlbEeVsLFMFEJjQ", "b65eb33e52701ea4989015e69da7298411a83851ac7f24c2399b996fcb45f7bd" },
		{ "cOy9MaJVwLTXDwqwRgjYnw40Q7U", "86ca41b9cd736ae4b4f75e67da5555a01f7e724f6072dc82116320b265c2a38a" },
		{ "CpgZ_77PQtyHnH68F_HVRvziNAA", "c6a0d47ce27e0c47274cd35e4c449f6db0db76186fed1c2cdab0bee8f7068421" },
		{ "CRaoyURR4qF3bPBzP7OLOXr1SOU", "c5a7004e1d9307ca92788e64c4dc46c5438bf289f8777196a4df6ead0ddcf8ba" },
		{ "CRRZeoz1g6DLr84XS1JY0Ey-US0", "a4bb7f85a5a79847b82bbcf1656ef2a86356862b04335f53f4569b0f29553a99" },
		{ "ctZGTZti3er31hAE0q5Ou9V7PtM", "ecd5230bdc9a53d20febc8ed128cf9ced9bead2bc3d2294e86819027df387a5e" },
		{ "CvYXEQeX8kWDgyYa2nHAzzYDDVU", "42705e0942e2a4eca317147ca168508ed605a71808f8d7efec7fd25520df9d48" },
		{ "CWOFB_TGvrgJma0yeJfUcyavZF0", "da89f8f2da44d3df6a1bee1fadb4b534e72c27c7adf6ab778f3ace38c8eedc07" },
		{ "cx0IXDxYbSm0u4khJuM_0HEVmfA", "f12a3c383bf08a04ce3df824dff3ed80be3ae0c2516ae7dc8ad78779e3c94840" },
		{ "CxHdNSJKxaVsGunsv9dOvtlxYUU", "c4d81bb0563349893ee6014ac564d85a3abec8f22f837ace9fa17adce336174a" },
		{ "cXm_Y6NhtiVCAOXclEiRN6i_VD0", "da17bcb3864116705a17864a78b707be453452ecae1622b6c23040e16d8d42e6" },
		{ "cXPG7siSqYLzPxehX2J_ril6NSE", "fb2562db7ab4bef53777c9c08246cbc7f7e76f9fdcbb698d2056de3029952bce" },
		{ "CZjZJRtbGzR4W7hub8dN7T7avg4", "2638493ffa8161590b271a8113937ebc791ce5bcb6c89c9028fa7d0fae8bd063" },
		{ "czpa6rFBlgly7_KXxV0rABSiDoE", "204956413b798d5cf18c1d109b3c8828e00ce842af3942fe57794c97023d2af1" },
		{ "cZv-TeE0I-3kUnqakM6BQ2x4ri0", "96ac58949fd9229c69c5fcc068a0e59366aeebfe25cabab58f0a242d1e041683" },
		{ "C_cRddGaMCdxN9yEHfnG7VTvxp8", "058f654ba3d7c7f0f8e01f005af3031c25d1e269e2278a5a70bd5f96af29e5c8" },
		{ "d0gAK6mnH02DCE13w_ABIA", "4bb64fe28966417a4ed90dd9a73cd1f5193489b69b727834c7be67074e4c54c9" },
		{ "d16kCtE-WGhur8x-uPtJl5_nOVo", "9c897f77c68b53f8050d5bf70d14767086ae3fe5dfc312a613f2b3f76da59426" },
		{ "D2RCtvBervqrPbb5P-xDjnVymd0", "aafa894ce09fa03809d4b3ed95cacf695a813ba6ff31ff57627fe83e2d6d32f8" },
		{ "d41S0XrqjkWSmyRhW63IkQ", "669b6d5f7ccd0fc5bf240ac88dae006ec15f05ffe0d0d1832d6a684b0e0e4c2e" },
		{ "d4fVB0i87kCq2Uo60RQcDw", "e4ea6e093b10825b6d852fb731c62dc0dcddbf85bd330df5caf7cd92db0d7ddb" },
		{ "d4I_LHxEm57etj7YMwIim-IUnX8", "dbbd7061f395ce15dfe293874908c6af8c70db115ce24b8f64e107aee7c23a3e" },
		{ "D4NOI7Yyf0KqpWld8CX2Hg", "95dbbb9cea57542b913c76a118eaafdfc2854fca2ff4a71052ae902097d1da2c" },
		{ "d5JB1xXTtLFAz5lZXxWmqo7eGM4", "3c3e187f75ef44e521509ba395e6195c6fa2d78b18f61467aec7f3c67c095bd1" },
		{ "d5NN2T9P0dBfAAdnnJvEhpZ-nU0", "8ffe9d97b1ad1acf600505df215e421bd96fc5f814c5044228259433d5ef5377" },
		{ "D61cHY_uGlI9x1Z2PsB9WupG-EE", "fb628509242884b914c5ea2d311205427c04ce742de6ec50be429fe53bdfc680" },
		{ "d9OqpCIJsQFqCUYS36PVaG87TbU", "fd3d818c74d6ecee28cfd5adb6a22f2b7faf24b2d4e606b5d3fb00f7ee56cdf2" },
		{ "D9sFpxuz1Z0IbzvD6tbxH7wb4pY", "e56a70e3c239defb4d11d8fd539aa2161afa3afe8e87dd58a884b529695e5a25" },
		{ "dA5X_cguDkKKcSlp3xI4gwwLsXk", "6fc106aba4d0b65a4fe6bcd035a0ef05e82a45475927b6898904d4ebb56cbbc0" },
		{ "DanO1s4kE39Jjj8untWWqyCdrtI", "2c66abc624b415136447caba16634d807672b803d6bf9f6671e3899631ed4c0e" },
		{ "DBBd1K_2HUSatKh4Xexatw", "9a34fe046d49c685e2ce6d6f3c4633949245f4abb2ce4be0b5c82b2fd522f3e6" },
		{ "dBLTN93x0S5pKB0reRNvJlmcPIY", "242cd556c408317ca69291af22022344f8ea487546b384ba2f29e9f01dcb1c34" },
		{ "Db_IlWXy94Z8B4fnnwDg528Pkp4", "80ba5fdfc723d51b07b49f22ad52210e4bf4f294fb37c5dedf98d5cfb083d07c" },
		{ "dC4ThlzTHnld-WPanI5GmNTLpGE", "041607f51340da1d213c84b03b79c7914899c2b5653fbbd620bb2287f4e0eafd" },
		{ "DdWWZZacy1u3yee_yw7ti3wXlxk", "abb56cbd59c4d3e28a7f2113bbd9bb81f747958baf4c685ffd6ce8ad283d4344" },
		{ "dE6029897hLKFmbG-kGKGw8d1pM", "35e6efe0ed39d238bae39e8a1df186fa4891063d356e9fe30ad4ae4c5ada3298" },
		{ "DecjCDc7gGjuhlFWdGMfQvWdsSM", "9926831984681988c1f4259afc6c9574dd7fe3e509ade0b2cdeee90e9e410200" },
		{ "dFl7DTb1Qj57iHsf5Bwogiznlfg", "18ea2be3f175153824da27ea0baa4972817aa030fd66593e658f876f67365334" },
		{ "dg5GEQNIdyuuwbRiZBF9AWyTBbg", "1e70b38562f8f9477561bc74ee5a152136e3401015532d04db20b6e86f36f331" },
		{ "dGifRLK8ardnyYpNhFI7-MpA2os", "6b30ec01467fa456c1217c36dd6ba2889d32039fabd4c2f385bb2cf190579794" },
		{ "DGOyQsNn_ILLhjhtIA6ikdS5ad4", "afb062db424eaba4d88404eb7707f4691745169bc1b5801db4a98c2bb609e05a" },
		{ "DgUv_EyaCd_gMcKbx5du5nxKRoM", "18a341beb0f8460a0f45eb1afd4c9fefa64594ad78a56acaf28095ef2de8c65e" },
		{ "DH9u1uOUJgrL43nfgLAJt11WvcI", "756e956ed3beb73c48087e431e662421913f24412aa4eb6ab74c473695bcba7c" },
		{ "dHCiJuak22us-BqTJT92QZZCDk4", "c8c78a53371cb69fed9a9010fb59f53a02bc032e3338e29b5fb8ba6b4f446a19" },
		{ "DHpcpKwm2DGOdqhWNQcP6_aZY7Y", "3c5ebb848d0dd5473a18e9e4a1c1a3725633ba9895369f044a75ed3f0289aaf9" },
		{ "DJJbWGE-HhGaQgyAPQPjP-84QwU", "e8c6419c02305fda66f40f99e399486f4d3d7d189b6b4b99e4efdff2c1615a5f" },
		{ "DjKfvcDhI8SC0_iOeZR9-vjHQH4", "85c1c5c7158f8385e876c6f0575a4055df72be07a94ced332cf47702cda2729c" },
		{ "dKImtyvZWiHwX7DNao75mo4ElK4", "c86c8980c25597c4d616affd3824b6dbec10f1c3101ee39e4acc7c9789bbb45e" },
		{ "DkrXIoIUZVj6yOaLgYICpcjJSrM", "fb5c037f5b0ef7c11f05c4ddfdfb6fa9ed6e12a094a2793792302abf1c14e7c6" },
		{ "dkTSSJm3serefZZT6asjtwWso5o", "02bfc43f9b3392053367bf3374c96b5c010f9cdf85d06bf7ba6b1e62975180bc" },
		{ "dM8SJpfZ90O7GP-cBns8Ag", "ad91ed5531a1abbb014f505544c13250a6052e3131e57bcf58987a911a8dab9e" },
		{ "DMBkk8i-FBH_11Qe0gfnuODwmPU", "926a1fd84bae7d362f2a0a55096edc144aa78a7c1c6ec3915c431c7a5fd9e90a" },
		{ "DMJKeKUXhDuzDScTFs3I3Up-8Mw", "38636689c276cd86bc279ee86abb2255eff35fb48ed8dfc1a4b8dd22fb1391c6" },
		{ "DOc-WRDPluHry6UknySdgm1wSAo", "fe79167f0393e90c10df44558eacf89f515e56aa846187abfe7e0b1d92605856" },
		{ "dOL_2Ue29_B1QBxsIiCyc8lh6bs", "2635cb5c2464db55ff5f227a37a4fef03ff663f05b7c3ed407afee14b412275f" },
		{ "DPJqE8jYQeGbIgSAUcPcTIy1os8", "cf7e2ebb2dff5a6471a0f55392105402cdd44df3c5780b4a8779769d8ebde2f5" },
		{ "dPxZzU0QYrSfsERFekgA60ewOp0", "a97cbe37884961ac2d6e970d089b35aa6d09eda361465a9f91a047e181c910d3" },
		{ "dq2ac5soA3ZT5vewGKornbgut3o", "b4fc8328c7150214a45ff018b47b9ef6d1f694dbc50c20e82c63e788ddec36c5" },
		{ "DQ3kKRKaFvfvXivtebMpc2d1bbo", "cf7f7f2397140a86a57e66311eec877ce726b426e69b6695f88b9cc5ed9f0906" },
		{ "DQqZkXDTF1bnu6957f40KJK9h8E", "78d55813b299a7d6d08da4aec8291d2dc88e28de16af1ecc63bed2d8b2fdc552" },
		{ "DQyhCgzRHk2X9WDDYJ_Qhg", "3de5942cb8e558289dd1b761f9233d4819d14a81e5a507ef8fb2c3aeda6707af" },
		{ "Dr1ky4qd9pIgQ2qP6SpyLFHrVsI", "e16aa5fb7429fa5ad217a6d004143f0a8a15262f78274848b5d2fef5da66b015" },
		{ "DTCrfZLLHky28rm8MhEaeYsZ-PY", "00e4191af2684d41a98a815632bee4c68b0adf18ea2a57e1b02922698b2838bf" },
		{ "DTxNzoMZww4VEnmKKtmN0AAV0rw", "6badbad47cb5c838f5dee035210c44903e80513daf40398e3a5a29892a08c862" },
		{ "DuVfOObPOSHemFDRGmSb-QmJqZw", "360b191d6ebc11a0bca6ad434127dee03f5e0a6587c97f92a7e4a243841d6e05" },
		{ "dVwnHmrjjvWRU04cvjzNUgAp9g8", "0ddfc43d73b79585a0bb50a323ff967445f1fc88ab8f584127e5a66a86f81f51" },
		{ "DwFG8APdtJMhXInkuZouQsGhwFM", "157d1ee75cd5b2039283162f5e8ac99bb98a2178ab61d0e0012b33b3488487e1" },
		{ "dxfdZslWFjyx05jpFytpsy-h_SQ", "bec835e4ec935ccffff94192e5fe8cf7f152d428a077f24a2a5d8acc46a16e8e" },
		{ "Dy88kmbuSdZ6YlIFjrGdxu1WJ5k", "91929f09e4bda7bc879a7d73326b079b39c1cc1ba689ef562bef7445337b1fe6" },
		{ "DYABvqQLmq6Xg9FI5klcNqorPIw", "c313e4dc7e1deec6256086953549b1258d19ea7040010004c2fc55dabb75f50b" },
		{ "dymWwairCum2L2WTB-FTa61kNdE", "d1760f38cc9f5d03df59b600d292244df06f75728bec132e570b91ebd4ae4a64" },
		{ "dyvTV8glk-0UI4zTNA9GuIFNXig", "b2f1563cbe2ed21111cdde1451101df1c71bcf3579dbaace2777be25c6566fec" },
		{ "dyxWBhD4nLb3_fH4pFVlajqfKcI", "6c6fe290fe5f5246f9aeeecafb61ffa011f454f606d08216b916491c59fcecbe" },
		{ "Dyzmy5opDjBEQa7LNJQPYaXiilM", "4f595514398fc09caa2b7955d3f2889e1f5b84dada2a53b3e953e58b2935ed91" },
		{ "DZwqvz0CnUKWNB35ajIP_g", "398810eeb2b2dd57dd7e25aef69afc2ab9cdb5aea47c4fbed07e6e03ecf12c9d" },
		{ "d_IHpnXx_b1qBsNnS1mI1zK5e-0", "2f2df79127ff388f0102d3ef7fb30180ed379018b3aa5acb517e94db1ca8bbcf" },
		{ "e-0g-sq_dlmJo8Lld9EUumFUmp0", "7f013b02564979eab46a6fc4a3556f54307f003b476259f44b2671acad2ec2dc" },
		{ "E0u4PAm3K0aWZ6T6LbyQIQ", "7dd1c322750240b14aded7356a509f37db559687fae2bf1d84ad3a0fa2a5c060" },
		{ "E0u4PAm3K0aWZ6T6LbyQIQ.meshx", "7dd1c322750240b14aded7356a509f37db559687fae2bf1d84ad3a0fa2a5c060" },
		{ "e1vDw_WrRYnzwlg6U3WEqnN-x5E", "0a329e4a44603a8f02e57b3b9400cd68d5f31cea87e81d48a7f53349f12c86ba" },
		{ "e4CcdzEWhGDmyM-MMsxvD-mn5JY", "8cb5e43dd0113b63c00c33ca33a75cbc87642c849dd360a2824699d488e4bf71" },
		{ "e5qc-6mxYor1wqpgIfQu_r6tzks", "385e9dcbafbba1698c7499a2d9e918b7b25c3b8ff6b2c39403e949ebbc4658ef" },
		{ "E74yG_6i2F6ClbW20PtjpQWUKGI", "1e433a5255abef79cc6d48e044569e6a8c7298bbfca61931f48151a3959f2f42" },
		{ "E89B8pdir4eyTs-aHMRnjtVPCrE", "e6e360010d08dd60e847dc68aae8c7907406f4768470b5aaa0905088dafae21f" },
		{ "E8P3n3IMwfU936GDFbyRVbf9glc", "c5aae441451fdc985b9efc6e0357dd2783325f56bb69f0e5989dabca58310cfc" },
		{ "E9Ou8C8nL8xvOd_tHhf5fJrwk7U", "db35f5379a97c85e77df07c237f425d980439d349222b4ae14c05b66ea573b8b" },
		{ "ECfswYYk4BbUmKOVbowCiMoozRw", "d66431af9b427509c40ff583a6475b2400092c8f67cdc975bff5e2293380f16e" },
		{ "EfZ0UPLEIFNQsSberKwp3Lczc08", "5f00941bf20bdf7c7cf0e74a2b749a4b94777131bf5b3dfd9009e648ee02a244" },
		{ "EG6Gzd4A2u-EF6Hurq0KpNhwooQ", "1e4110bb9ce518ee4eaf062f51492ff4feb34fccdec120bba486be1d67cbe45a" },
		{ "EGueAdHqXfWh8bp3g2BYJbAy5g8", "29dadc4d50485e40eb07471c4c1de15760a336c5b00a230f4b1946f483577179" },
		{ "eIDmPhpnYYMpYZ6q4yS7g1ho7NU", "86e9b88748bfa53c444f0737bc9ccf36d78f2418299149609564432cac3cacb0" },
		{ "eiIWyX0nnNacB9mESbGU1QtE8w4", "b7e846928f4296545ec8613a92dc22899d4dfdc4e666df59341289c3149c8e5c" },
		{ "eItiJlUqiRg475mrg6HjtZTvLT0", "44f995c7db668b1a757f4c2f1b03ca06e42d58d9c13eda15f3385d967190e955" },
		{ "eJgQ9-7ZxlvQmZFEGebitdQia78", "6ef71e301de61262f25d548bd314e48a322088e9733d4e173a10172451829ab4" },
		{ "eJQiB78355aAUR-3b5IEPTjZQtQ", "9ff7736f13026848e8153b85d11d783a491aef70265cca44f6afef2c9c060b52" },
		{ "EL6tfwN1YkGrOIvw8oBWwQ", "1a4ea5c1f0051188d50657b4a7cfa1f65a033c91fd9cc4a67267427a53feb88f" },
		{ "emi05tZs3NuiqKfg6vhWpAFUYX8", "f4d09ea9bc0dec6e45291ae72ba12d568ee7dfc47d9390c0af2eca641f1d172a" },
		{ "Emr_T_mnhu65wcjWQkSMV5RKr3g", "61b88661792a0b1dbcccf3c717ee57843e87ad15815bf3c64f1e4cd6dc32c0af" },
		{ "eMttbJX3f0BsKmTN0a_PxJjx9Mo", "f32caf6b8a44cbce669d93a0e88ad6a49c6754cafeb9b4f3c5ccdfba0ed8f7f3" },
		{ "eN5IWqUlz06HJNsB_jGJsA", "cdc181c358ad18de4c166282f54f160d648dd0b1119ac0292787dd484091cfe0" },
		{ "eNFmvpmfZ4FXmKlL85-MnIIqzXY", "26e9f112b5517f2437ea6326a32e7e603a92ebce451eab3829841fced2f17d03" },
		{ "eObNIygT_EmAt9i5J728OA", "66c3f854d50517d5711cef318dd49ce355d29257fcd7cf89c179fb8e930ea904" },
		{ "eogA1CiatUi8TFH_8G0iSA", "7d3a88347923004c1d8e13e502cc74075727853a7b25de7961778b5d77732b9e" },
		{ "ePArIWVXhkufrYpUrg06VA", "9d8f72c79b0f828746abdbe7e66d07e374061f193ea0f79fcbdd370425b1352d" },
		{ "eQ63jmn2j7Ui9JW7FBWxgJz71ak", "11f56890b26c2e7604c7c6fab3c76605286d0eb4b3c1e1bcc9fae9ff89983082" },
		{ "EQhkg0W7r-ohh3KWOArj7Oy_Gn0", "043f9e403f36369593b2a0dbe9c26b36ea5a445e06dd6f38f3473e7818779412" },
		{ "EqJV-HCXWWpms77dpadqjau6Zmg", "15dda5c95b7306afd7a49bde8d408f11c8d585fcb1741e4113532a70eb3c9dde" },
		{ "EqZNtyEnoyVqzVm6ehYOOigo3rk", "075ede5cc401c4cd2f443f4917bb4194a96d804c85ad897cb5b7ff3862c604fa" },
		{ "ERbVUaEmGkW7v9kvH91pHQ", "7a5d9cfc8dfa57002282910df83bd8f42f6d11d7f8e613594e592f4e6ee53115" },
		{ "ERspymQofrls2bBQNaSDBMdFXOM", "10912d8d4a2361362f010a84a7ac5c05c88ea5e7eddbb120bf221b7c731f3c16" },
		{ "esLWzgjSZ0Ge8Rq7UR4x2w", "1ea1d0169addc251a02a8f05afcf0bb223ec4af168c21215fca5f23b20b53594" },
		{ "EsUC4FbC_OeHd0-P-kFHU3EYkP0", "dc856174ca52d9786ffbeeac077f262c2d52f4a41052c9b212b6265ac2f2f7bd" },
		{ "esuI6_26YY4t_rkSqDkrpN1ADwk", "9cb63866e9194340764d8890f3837744ac6f3a561f4064b957409481636b93b1" },
		{ "eT9aHb42LbcrpwT5L5WwLb4Z5n8", "7927e8bb82a74750bd2a591b46d1015a2ae857bf6be5b85cd5472339e7c4f893" },
		{ "etR6iRc9rNeZTinq4oAinwGYcuY", "7046543c122f1535f0414e6bd64e397ecf449c156667be96ba33492acef2202d" },
		{ "eW7zYBko8qj-GpIhleoaH6qORJ4", "186d572f9cb9ebd5fa1db7cfef45db1d8cc69b3a57e2d1b9eee720f548e9e14e" },
		{ "ewnDKL_iM0W8tjiCAG7xQQ", "9e874dbf65e3dd92243620c4bf46d77096dd0ddf2430a57d8545938a5dc00dea" },
		{ "eWqA_kZICwLxibHTGi0tpUS2W2Q", "acd3f730d762690f1baef01a1bb9f8a303fa314636bc63086eb8ccbb431e74ab" },
		{ "EyeyiVKM3zNIpvsExJJ4MaLdyP4", "c42c2ca540307ee8c75469e52f994a29bd4eff9455c7febdbf45f04fa9094cbf" },
		{ "Ez6ABYHk_oNyqkw2_wqO6VQ8kM4", "e6f25b1f070f61002c52ad41acf9c98565299638637d98c8f16b23424000530c" },
		{ "Ez8sPAdO0kqKP_4zV3poTQ", "ea4c0aa6e63d70b0343aa666ff2095a612ad47ea85c572ecbdd51aeb75c658a6" },
		{ "eZVeaY7yEClkPi1gG4Cna67LPkM", "f74c183b68b9f4f7033623d0d0aae853ee9bf9e87250542e7cac0bec5ccdc5bf" },
		{ "e_JcSxm9u0Gbp9MusDTMOw", "0549f07e1375d541e4a2988dd65897a069935b9c9d116b5935d2eeac068d8342" },
		{ "e_JcSxm9u0Gbp9MusDTMOw.meshx", "0549f07e1375d541e4a2988dd65897a069935b9c9d116b5935d2eeac068d8342" },
		{ "F-uth7RNp3GRaOIiGAvvrXjat1U", "a9d4e25e77e8e32b2ce61b292a5688ffce786d4bbd706ae6d42d7cb604ae5108" },
		{ "f1dWnwGWfipm61t3EyAS_Ze5AUM", "dfb8294c9e8716ebf0de951b18fe74e8c985655d8d83ae2d1dd27e6c0a501dbf" },
		{ "F22SiVN2VTPw_YfJ5muH91SYr70", "a515a47c907cf1fb20f1786687cf81628b60510b1a2ffcae85a95208fbdcb6ba" },
		{ "f2iuefCF7UN6iO1CKZ7S-TVBrCY", "13930dd4c0d546566414690f0e0dd306b45e13c10b22f8555aacf056306dbc49" },
		{ "F3afSbAreTpSIykU1P7-jO9wsc8", "849654e8bdb6d421b0650431da2504b051447326836db322b69029d2919ef83e" },
		{ "F3Tava0BZqeRkSKcyZuH8oC-rhc", "dd9e0d22985fca593b68eeef7ef5f7f23fe50a93b5e4d4cb818912c695629e3d" },
		{ "f3WktDxX_BVFUhX-tJwP22vlRkQ", "dbd3f1d82c3f72cfbda74ad4f95e3f01d7c5fabef4e5b915c82cfa585cadf44c" },
		{ "F5stdV-R3Sty42ioxzHDEmFT-y8", "cecb8759234966bc4af82c4559b5596fca2a2dc2470858967088b09ed5cdf74f" },
		{ "F7-wW-jvLDJi2MsRMPo717N95xI", "4874ff41d387c47757fe38a65bd2fb464baf74535fd5dea9576ac5e1335c0a71" },
		{ "f79kKuOuxjn35qAKMf3hNPyYF6I", "2109570c16550852b0fa6fae90d048a7d9f8182a89a9d78a325cf2292daff13f" },
		{ "f7SB2fVb9OlMLEAqpEPVXn4jrjo", "793338c8f7403fffd8071eb7953b9a4c0ff5202a722305382c00d7953f026b9f" },
		{ "f8AOnrNXyeEgddJ0Xu4fRdiEH3c", "6e3d7ca5ac520f37ecfe17ec09af06c8ea85f2c2e512f129f6e155910490d25a" },
		{ "F8X9Vikztq5zpa82tjJXspF27wI", "a955a50732c6dc23be8c94ac52529f92d70d1dbf57c2040ec708c4cad4ae40e6" },
		{ "Fa7ZXznap9Q1W2IcCtvkMONk7sE", "4426767d61b18986e83cf2e0de49b9017769e80fa56b9d7b8b351ba937566ce9" },
		{ "FaehXlLlkPCeA4CIX5j7h3fFDgY", "b3544da114458d5ad8be576785fff561de3f41d708973a9e7514ff2837adfb84" },
		{ "FaZVMowugw6zK0wVjvjD_PVxA_8", "0f0f80c82a431f538c56464e54492c2922a391d6295acd43b1580da23ced8568" },
		{ "fCrLAHCh1HUAB11oJt-tbC8BEsg", "1e40ff106ba7363e3aa96fc3fc67a13bcd8b88cee891ecbc28f88e8185d7ddfe" },
		{ "FEeD4DZEmGjJVgowZ53IeeSmxRo", "bf6de39311c2572eb111b781ca8ba63ce2bc2c105dd09abed1f24c40567b70cb" },
		{ "FEPhSUml6zTklqkj6hLEh81Q9Vc", "57cc5da9563c067edc52e9141f156da518240f0124fcbda6e5b13f9035642b04" },
		{ "FevLko5xEDR1yoi7eX2nVMvVwW8", "da04f62b1fbc1c7147332fd265a77de5547b95fd8a6b4ad1b01dba0e797ba50b" },
		{ "Fg64F5TxdPk1NV6_8pDJdVe2280", "7bb381db47e37d488338cfc84d134d90b762216dbabaf5604f311a0523b290e4" },
		{ "FGYUgu5u_Pt1bMOGxhxhh5Yyjzc", "9870920635a82622ca0cb4e792a3124b77c891277d7762a4d81aa5ad200edb27" },
		{ "fI0ZS4arhvAo02UC5c9fdmjZrc0", "b2cc39ecc20aaab3c4224361f12d9613a6d848e097d3c0b25aa24d4db102037d" },
		{ "FIT2B8W10rnJvB89EeQWpAlAgEg", "c02ed02e18f03a18c73d638af8ceeaf756e40ddd512b924cbc8c24e9e3caad50" },
		{ "fJ6ktfR9dk2mqaev-_sIKQ", "820cfab80bbd6253547435e182da59fdb7ab4734cb7a3ef6ddf1be44c428143d" },
		{ "Fjbi3wHigNO6kUAOrLjEtgAvDDw", "9beb057382e207287a526474c0371255ac6f979afa093860197fb1165e89ea00" },
		{ "fjS5XSKMLQZeRJ_8y2EuD8_RvMk", "e53127263ae0a135af110f70d1975bd91a344b6666dd07e7f5672bf4253d3e7c" },
		{ "fKskvEUjAUAx3iCpwByY6pUY8Yc", "fa2b5ac2ee7e396c24148dade53f2a6bb885299f31562dfb313fd672a7cbcb19" },
		{ "Fl06SSZDSra0bDz5yEsnWxLYnt4", "53752fe32e3dc901781d249e79723583295a2e94937449c9404175f5f1a3ef78" },
		{ "fl7TGTUwPUoODt1F8usa9qTDQC0", "adb80afd75f487b6c675a306f4d44a8875130fdbb6fa3f45c82323e731b374a3" },
		{ "FL9ZGYc_r_PupXJ-fe9eOwFIeHI", "9cc453cde05f62a964fd31852606e7eba0aa0305176c1c57a9da1f3fda76d7ae" },
		{ "FlGJ0PzQ2MK3G-ZPMT9PQn1VQDg", "d0dbf3c46f64f362512eb3f930cbed2b85791237069de5b1086847d04a1d245e" },
		{ "FMpPgR4ri09qdKwXWXsp5i8T_mA", "aa85248397b4b9ac467ceccb1902fa589bd2c697fcd71a06f506edb3e9cf2b8f" },
		{ "FNMqcmkLJQ5rAXTY2FkP6C-MEJY", "083778c254e3b9f55129e3c1789fb5770417b5304adbe5da8a0bedb7994bbc78" },
		{ "fp3qE3znU5ERU1j-UwfPQMXHeU4", "342560126dd59f80ef6bdaba393d47158ce26568bf3c4d0c44457ed51b571bf2" },
		{ "FPR0WyunxSN11D1ogDpqkh31Dgs", "0491d2042816f97887d645aae7197871319f1f43019bdf2a6d4e544c6ca02ef9" },
		{ "fqVXlXkfI9liNXNBMSOxtfCIgQY", "cc97fd518b2e81f032f50f690eac52921bd70dd919bd7a6e72ad67ec2ee19bfa" },
		{ "FSc6pJNbN2vu4l6si9wDYdy1-Sc", "c52ea8304c01208285a64ef04e5ff88fa9ab0a3b3a37c7b2bf3150ee0cdc978c" },
		{ "FsntFoX-t3mHcMjV3JhM_sv1-Lc", "accc3851bcd116d03b72744d19b0a600d1c8726397bc5a06159a99cb033bb903" },
		{ "FsX3qF-i0Hbbk9XV2brVskwdZJc", "46f71ce1e6dfb5252d13e24d27b3ae89cc496e1e7e871e9169146f0c832418c6" },
		{ "fsyCV8yhIhJ8rg0ZE3NHo3IqA4A", "620425bf48755d61d2e4a98e774fcd6b3fb7de5208bbf812ef193e48a43bbc60" },
		{ "fT--Jzjkuu_XSVeMmSCYNr-ebX8", "39a5608c5d01f669d9a2ae5f91bed74e7de5f0fc97b063c860cf49afaa270c23" },
		{ "FTgWZjhw-iBtZ2bAugMTFOPJt9o", "6892c28f72fa176a9ff7f006ac0259bd7a7fc13ebc006cd58be2933049611309" },
		{ "ftHylyvZMBcFhqu-CwAbbkvTjo8", "161e519611864cde89cc56769f5c02a9b579738200a84d40a5cb418a3164bf68" },
		{ "fVZ3gIqAMxsFLc5vMPUE-wIwNmo", "519780e6601ebf7101ca33bcc3058a3fe0e1ab68141663ef1f38cb71b3de9ecb" },
		{ "Fw1sZz6q_lsEX-1jEA5Ch1JCfv0", "fd4d5b9e6ec283299ddeea98ecd382f349a5ea22e1d00c88c29459a385d10d1d" },
		{ "FwzpLBT1gUWIxAlRjrYwMA", "7fbf90056d8636c0e3b0c06336674ac2f7095547239061a435b26a0bec19cdd7" },
		{ "fXnYJFiub7p98oC-dRCvKDDxzTs", "d22e48446290b4c9dce2c0adc5547845594e156b00feb60151a4e71dc28f85b6" },
		{ "fyeKkYuxvs7fAiFYDlfTWHFE0bU", "e5b7db2e3ad953202ea8187a2a6f304db7b4ded9303b3fda9c7c226e7637f3db" },
		{ "FytPARbir0adR_S1r0wJRA8ufwk", "ab8e7d49d43669f8ed733630d6fffb8e298b107918680ce7d8fe3f7ff89016e2" },
		{ "fzn2J7je3pq3NENhVvyRSBxTeVc", "35054622f97a0901d5b271f0d4799a9a8e70cd6eea0b0d068af939d7046f0a8f" },
		{ "F_wR6WirFHFLqw7wC3og5ddZPsU", "74f788dd89f670bdd0cb2519ee023da3025f01fa24e3d4abad43e71d3743760b" },
		{ "G-BLuZRbMTrVA5i5X1IyQttbGaA", "2df6063091546e17be96628bc0760124782e93b86e5de8018438e40a5bfd72ad" },
		{ "g0gRo94wBG26CVsSfGDlyn3LKAg", "976e80bb9cafd44cc0088f55cfa13ab4262b479c4037ee8044e97b0dc344795e" },
		{ "G12JPjThGEG0r2au11zpqQ", "cbd3e5669be8a8af9cb331f6bdbf2a14d2f2c16b3addc66af87832fa4c7428a2" },
		{ "G1NDRZmmzI_lXD6TeJbR3BJguRI", "cab830eed635db47867f2c9f93efa57ec716cd1db55069bdf938034b2ee92461" },
		{ "G268cakzp-s-J20a2n3_aE6wqBg", "5e64c787457a1842ebb4ad9fca4e8b9fdfacdd848c7bbfc5dd437bd12c3d635c" },
		{ "G2FpxHD8PhC-apOpfX94ElHKHKQ", "4657f58a1dfbf71e1004092a7477a8a6be28a94d70c5d694a046fb19f7c13bdc" },
		{ "g30T1mTx20m62NFtbAb5Vw", "22919e32bfe90d84ee3d9d7f11de9af24bdd0bd2f7326c15b62b66d45f23b7c3" },
		{ "G3iAL-dFfGz_PI06udjGkfbSCeo", "45028b9b22f02e912d177b1836cdf6341d36f80a3c0f0dfa4e1928bc66fe1d7b" },
		{ "G4HmxQAT4mtFeE8jNUHe7-BkFP8", "c8cfe6fd358535241737d6cdb8bc4d8a07ede3aaad91d62bb2237ab43eca407d" },
		{ "g5B3NckqcxdyoV6SqM34-hgBpPM", "2033b62c88388bffbd7657b258308c4c221b88b37a720ef5c85264d72b9faf2a" },
		{ "g6dy5fzQk56xcHIvzt38Rn8oaHQ", "e48304726c2a41ae9c78d7c2d8fc705c273ad3d679164260895773eec5f89634" },
		{ "G6XEfKmbm0WNeQit60ErIA", "c75043c61b70272440fb59ef5117d1483f9367af19eee0c59b3622e690917528" },
		{ "G74YeRcNYE23j39XeyO0Xw", "38c95e63e4349e379d5a43a8d484890d6bed545bab4f8e111a98ed0929a2e5fa" },
		{ "Gak7OixB0kxSiHdH40f7PJe45Jg", "3e596eee0b1d54c311abff7fd189bb2d563042f0854822735be3a6cf4818c60d" },
		{ "gb1KQxpopU296D5_7aX_vg", "1a9a0789832bd82295f8b6017e7e972383b6bc9f457fc84ffd189ae260450187" },
		{ "gc0__xdzRlBeIkr2wG-gAQlV6F0", "ce0641039ef7133b6e68f28b2ba548e8062d5a7c7549cfeb7aa2fa7714e435bd" },
		{ "gcoWqIaHrE6qWLJO44OPBA", "3f24229d50e1eeefd2239b193a1e9bb54a813543afecbcd62364d11d3d362c19" },
		{ "GCV7xf0kRdmyFQPAjitaLUg6nvU", "b0ff9165f07374f3c032b43a16de67b805859e3975f3bccc54a9d63f4dc2ba9e" },
		{ "GCvgjGBagayX8H8CfhbdUyNCLyo", "d21daf143715591f4a9fef546711b452eb8c51f206e61498f2a5af26355a718e" },
		{ "GeBp1iLzswUJcUy_UwMThVgc_1s", "39b27f31fb6a6952b785a8f8306d344e962699c3876b52fb26e9233151595202" },
		{ "Gen0AC-UKyHvaEtAjPiRAGrkA7A", "93cb1a24359340b9c25ccf30a085e263ef6ba3df34d1983417581a46b9fefeb9" },
		{ "GGpxDovWp1jzEgl1EDcuZLWIeXs", "3381e68d389e41d767324a94d85ced8519c7af6fd887ea8c30b9166a2dd5970b" },
		{ "gGqd7av6OG9RNJohGHeNL_iXNOk", "f9d93b4a0e62819d3b20425fd4831d8183a3f79be53cfb610dd4158a0e338c0e" },
		{ "ggs93FUhFkeaJHJnqcmopg", "aeef89a24a2fc4f748166bd986eda6dd7e6e1af92850db5952d4e568a7480d5a" },
		{ "gh2cqCbPVk2ZaW05oenTdg", "65347d1a6b6a6621ab66e1dcc23105cbf0354b0ac99105e77a691dae4cd3ec56" },
		{ "ghCeeIlRBOWWjE_dGhNcATb5QB8", "897d6dbfff1069523be251378037e97fe136783e183085827aa69926335b0fac" },
		{ "ghgr15BmjZzDbnhAAW56duiuYFY", "d50189d1bc10ce1a8c666d5efd7687e1c4e9722f134950f14678284e5d568a7b" },
		{ "gHLwydeh96phD0PlPkhv3cdgxvU", "5258ecf5e0b1a4cbf9f3cb4706a338ac914144eee68ada8032b3291880144fc7" },
		{ "gH_ZXvJ8s0NhzuUyspMgxb2QWQ0", "98b07981fd134983f89aefac4951ae3e4b9f4d08e3c19229b6df17399e66a51f" },
		{ "gi1PO8y670yLegWGIkNXOg", "f680dc63e3e32ec55d558e01f55b72d636523b906601ef48b3f4b2302bc3461e" },
		{ "gI4a8UUoSzYGbtaxQiJCJzW6bpo", "facf6dded106f02154796266948f127494e53009fcdab2cb46ccb3160c598d38" },
		{ "gk-2WJvc7gCDX-UoCOWf4bRmOR4", "94bf27a5525f5a1213b0e158d37abcdd33b6510f2fdc6655ac20974ac818cafc" },
		{ "gk2yvRtF8fZ1sawM1TWcq38ercU", "916424520ae9296bb5231f126b85ace636b13df396d496204d2b75d04d2eb969" },
		{ "GLC7hGHQvESkekohZgBy9Q", "f95998d9a9f40cf1353424e548cee7344b332ef0e51b9135d32545364bf3236a" },
		{ "GLeX0qzh-e5U1uIGp8CMgMH_Cxw", "6b9895b0ea6a8d48faba18871fb9f3a997121e4e96869b80ff7e60a7a74880de" },
		{ "gLFWXD7w2D553PcTtGEJvwfsBFw", "0048b1b6ee71610dc4ac5be9b02e2e250b68f1e3ca78a7f2333e8217f19d3563" },
		{ "gNColO0H_v7niaXMyQy4NW9jfP0", "939a85d825be4918b291d5138b9f441a74c82ccce546d814bdda02d0e6e2edc5" },
		{ "gNZXncpnv5iNl_GIUQfn3khwVss", "d5be6eada69897bdde76311d679938cd2f8c5783ce2dc849259f8042fc074fa1" },
		{ "Go3cCKpbG3v6qlV0I_jfQ7KNZSU", "447453d679e8bd21a362b2ab193b3a3f1b1a0187daff2c97b81154c36000ba3a" },
		{ "Go97UKS9X99jwO2u-eQWhuzJ1Cs", "d26063d471d004ec488812dc3ab8ea0f19dd7c14507a07a0e9991cde5ab6de97" },
		{ "GrjOzNwxQ0uYygPXIkR0CTYywdQ", "a0064c12fee6b7ac14d1b65778f5a0d6f5f7f901fd2d4d7bf7a9253ebd2cd996" },
		{ "GRRtjCWHbEohqdNHPIIMM1l0gow", "9af3f74159d818f759f73cb98298bc5c0fbe4d956329768afec3c4387da820d8" },
		{ "GrwD0I4INO6I09XvMPosaU60V5Y", "4484479e94cbec283c994e2dc86087e15405508a209ec48256f2766104fd4f3b" },
		{ "GR_ezSavKEBdwvCCihWt4EJGpnA", "ae90821369d6805a9aa9ee3a240464112352972b0cdf94f1025a9f4b4f9d3e2c" },
		{ "gsdALoVPfdBW0QaLbJD-2RJxjtU", "a56b71fe0f1e6436391f95ccb25f2eaa915c1d04b85972d7a465fa49e3fb059f" },
		{ "GSqHCZk3tLwk-HeMbIcjcAe5ZGg", "353221e35b0ea6d64f21d11c89d374af0a56d288cdac3e8612973e633193267c" },
		{ "gtwkySqQqzjqPCEmCNnyxUaHRDQ", "bfba4cdcfa7a041b3dac01dcb98969d4b77751744d559ab8ea3f90be709c8fff" },
		{ "guETg3fvi73cntCD6bXcmN44c5k", "19b9e77038defe4cf42ae2106484d7a60c1caab7eb338c351358fbc766c1f16d" },
		{ "gUNf-Qo-oiLzLWu96ea6ZpKEhKk", "18b544fb2ebb0b1588259da1dd22ef4882b6223607aa3439805c1a7b85d9ce38" },
		{ "GUu8QlnUekXi6rryhJsBK5J8tsg", "42722d206d23c5a35886deb2379a6170e7c52ae123f801bd5553f625195cb0b5" },
		{ "Gvlyv57umE67aqzE6dD4qw", "26d58665602bc08b80a2d11f0c8affb0466760afd3d8b48ae090e792732b8b5a" },
		{ "gvqb3lrQUkqWdQPh0mM4iQ.meshx", "906fe6f28b52badd9cd3730a345859498b2c452d4c0f57bc8c5fdeac366715db" },
		{ "GvyomifnKFuyt9-EZ3gQjHuvRb8", "3d1cb294e04f7eccdda4635279ff80fe4bd1efc7053be2ea60eeca9330d3eea6" },
		{ "Gwa3pPcmQ17s5PDlV_ufytkLoAs", "eb0e49f7c5c2c54bbaadefaf721d8e8dcf2a1e4fdcf634654af29a68c6f89db8" },
		{ "gXHuCRukyUqXYX9QzcmbNw", "56bebde7927c6024e4759af0f32452240230e29bffb6a724deca724b25b6a7b1" },
		{ "gYqBHaM4e6YbKrmBZ3iH67XjUpg", "76bfb30638e88b547f8e45040473bfd9961a04e164f233ce566ff77b4d0b0b48" },
		{ "gz5P4WxqXDq00_aLnNyoomtdU6k", "aa0c01089b524d34af18f50c015cb07c592153ea44588334fb3f80f703967484" },
		{ "gzGxzemuAqDPp4gzbNECSCxcbjI", "e2c3845602260621819d5e6d02f0f7b2a8258aa472100ef31fb9fb4e599de6c9" },
		{ "GzRhtDRgu8N11b4tH2G3O1Ju_pU", "1f428c02b9b46cfa533b4c1f13a6b47fc895c31c2baa2d488f0f316f14183122" },
		{ "G_9SuXLs2JeHcT-EbfuPD6uXLCY", "6a29fcfb97a8a25d3e3f6eaf37f4c7c907eec77fb788f41476e7b9cf1c66eb82" },
		{ "h0pOoUzJnC6nIBeBDc96w7-jF-g", "0477f41210cc542a3910d3d686418e3ec7a696b16605e5224d77ec5d653ec37b" },
		{ "h2m7BpDM03pyvaHqbaR-HAiDWNs", "72523748f6c7b8389e9780a261000c525ab12c19e4330064e738137a9c33684c" },
		{ "H3uwMK19IutPtpjadlmSnEtVyIw", "c4c3d4bdf0c3d34d30ca107ea22551402304a27ef81089bb6a065a7304018d11" },
		{ "h4Bi4rhBv7slIGjT7KkkDCQ_vPo", "73ca39346bc77ba44b17e069f631a959f14df242f4406cb31e13438d6661082a" },
		{ "h5I8HVz4hUidIl_kmUSd1Q", "e7e2c9b514d8dd545674f1a8e2b32dbd104740b994cbd999a0ebf83e826a741e" },
		{ "h6A0xL01V062j1WYudwOPA", "4ba11cb9e2971452e25b1baba72023483568773b9161399d3962106b8d5791d9" },
		{ "h78siAjwWyzL_DYaOPF7S0M5Ws8", "a59777b5cee8b06d0bad74ba85fdd5101bc6a9a386a9f49996a144fe64968787" },
		{ "H7_ereLTukKqq3N9mgt94w", "d35643d19e39f77a4fec69a9b7a22e90a3dbc4b1cd159e7cc0ac4886490368e3" },
		{ "h869ZZ2fJrnBU_As5CI-dHDr5Dc", "8a423eecbe2d41f62a4aff04ac871d6e9cdeda81498917a7d59de9c0f13abb11" },
		{ "H9IeX4ROWKcgny4_-KSayTv4kOI", "32b827689a8ade5b4ce168d8877b302debd614f4749211ef086394ee9982a296" },
		{ "H9ig6tSoyFk2o1YXDOtFPtlrRio", "64f4c661ce81503cf48190b55a43a5af4558bb3b973ebaad1b3ad41d7a19bd2e" },
		{ "h9YzLBMRIrd6Bl7MHJHf9eUFNl0", "9b7ce9a15f587a8c272fb133d62559c3c5421be102d1216aa5f7d6dc36fc61e9" },
		{ "Hb-aEjmH9S4g4guIbrOc92_sIeQ", "70c7c068e42186bc8fe593ea32133260a90dc0fdc9480e7561c393cee4373bf4" },
		{ "hc2KMInofEW8mOzUP17GuQ", "629bb0e94fb71aec9fb778659dfce7a46e3ef969ed8b2dce7673dcbd0616b54d" },
		{ "hCcEObNiEUiTAiLlADm9Ow", "58cbe8704629a906796dc78571f4c4d4f49886e98a90454e7ca30455488fa280" },
		{ "HCiRl6qVS4TXuYK2jKDOtnu87JI", "eee4dca449b03af9cbc6a7fba2e248336d315a074d4c66a784e6351f57f0fce0" },
		{ "hddu52Ml0F2vcRytw-YYJCq1kEw", "4246e9e7df8822eac6e3144b228d0825b9ffd1b65eacb6d4f5b992ab90b6b32f" },
		{ "hEa3_IujkkuoGcn047dHEg", "370e23e10bce546814b80b398eaee686f9aeca4ae147ca1675992cf8b7635e7c" },
		{ "hEHQBO2FE8y3ZSfR6cPkwbP0wME", "50d9ccc69af276c848951af327960483dab4133e7399f3ee5d1016caa1c9a837" },
		{ "HEQg-C7TEtgDgOTnzIZjkzfMO4A", "391ce0ec8e03918695175a0a73f4740f59f779f1926b1ba662e7d2333ba0bac3" },
		{ "hEQpW_LKMe0LXRVk8NqNGmSUbcY", "4599573873ca24754c413397b4b63a365e2d73a81d7e40b2687af2879bcc2a09" },
		{ "Hf1AlND7J_r-KYdbuX8G7yjf-ZA", "e4198978522e5b77327a3b33cd8e6f42aad017ef64b34cc41c4da49ed3178c60" },
		{ "hFPHiXr1nXw-fXTs-dOYMpgNjUY", "94a367ae3c2a14eb5bf562fb5a26be32ca71288205e2f321add5ac120dfc7cbb" },
		{ "hGDhDEPeG7rZfof3Zg3lnLAOa5U", "c276949982674a9cebc8e1300596728ac3fe1a962868fb57f53e9589931608d7" },
		{ "HGhgQZCcAxCSn0cr45L1Ulr2GPc", "b8dd278c139eaa5ea462c03f154e6027030045d0f9de592fc4835780aeae2cd9" },
		{ "hGQ0lfYc7DIGfySjJPfD1wtECS0", "40bd5caf8a2b7a17544f4c6d1be115bd299e894852c12f57c6fb9bcad61221c5" },
		{ "hiBkZterZnTdL20JDNjTN85zeZk", "4017b6b577eb65283d58d8870c38df836091134cbfc6319bc34301ab5e0c1246" },
		{ "hiZU2JnxqgdYwPzdHRYs1GC_mX4", "d3fd86305dca6373a237e2564aa5c07275340f76888c01bd19359c11b4e13624" },
		{ "hjYG_plN-fLXTsGZMamddg004uM", "cdbab383d46398f01f6c3629371276e4a5d2d49bb9383e560f5980982b2a9448" },
		{ "HkShY1r42xtdk9afbePW1AloKk4", "afc943791831dd96e4e1c0f7742f9e15118842f95ee1ebb9ea05771ff61d1c65" },
		{ "HLB6Da5Wf0-T3tfSV9TkrQ", "6e27015179716721394d0f8c4bf80c0728b5b582b83b3108d587b72bcbec62c6" },
		{ "hLDrgOWP2C7wlbNkr-C3xN2ynfA", "ff750e8924ae1ae3089ff220b98f1710e0cdf7216e3d26e69d19ecce2c4be5c1" },
		{ "HlKCRe5omkq_rLHLvEyx0g", "8f594ac43db02c3fbec680a4adafca706709d5c16b381ea8c1c44ad1633d19ae" },
		{ "hlwTy2c_TLJ1LUOrmlSt2QwQ9SU", "2527db6cd5f2970cfc4c256222b45aca8b4e086ea9eddc7f64d85adf293c98ca" },
		{ "HMkF9Jw7v-UgEaVNukVor2QZYjY", "a97a98bf271d473974f53009a749fd7ab934e96fef86cdde1c79bf55bc9ea364" },
		{ "HMMINsK6iCcVV3JLVVoEe4zvXDg", "051da25954a1658cdc7c967d7a893f374bea6a5ba7d3de61833e44a8bbdf52b5" },
		{ "hM_-jWnZC63jxdAwft9v6n_u_ck", "b0aafaeff1e75d8ce1d904a95621e4ad905464a23ffcaeb2b68ec2805b101357" },
		{ "hNVMOkZHnp5vYOePVdKAkSv1Oa8", "58985bdc6789b601fa673ade7e05d00bb54eb22a32fcadf5878475d9ae41eb12" },
		{ "Ho_Ka22fwEaUvNPFtkQhug", "3c58ffa93935457b309a152c5b30c63f7128b9907f21735523c65afd94c068bb" },
		{ "hpspclh943AHgjQPi6mm4smVQRE", "8cb6f0bf41e0af902c41e1180ac6be3f7be85fae1c95d75d28e09005844e29d7" },
		{ "hQb2LUJU_taznSOMdRpgEkIJIPo", "5dab8e1bf857901269e26f5dbe4ee7816b8d43ac40556ee65e51e8254d93c458" },
		{ "hRQjw6oLmZLkLeNoJekppsaIRJM", "40d3348c0108215f1387fa9e37eae7fbdd90b108434928eb1c02b3edc1ee68d8" },
		{ "hSJUcAshd_sikBZDH1P3J91013g", "95e3af269e394b0ed81c298bcc4823419e3113a6a0ba9ef6003b034c49c57ea2" },
		{ "hSw8vLdqSNMp5M0n5NTXSJblQLs", "1cb652595da93cbd0216f25d40303dfe74a25fb066fa758a5ae5b595793afc5a" },
		{ "ht4AjjD70Zhxt44o-U_TkRGMU8I", "2aa8135d06c4246f31950b3e6ad709876e9cd668c7e7e1448b7135c572b3dba9" },
		{ "HtRhHUfye0iczvbo2O0mhw", "cc236b2b35a4757239145e574aa3f5388b32275eb1ba64d1d3dea849d4d558c8" },
		{ "HtXSpXCtJEWF5CTccXgDFg", "0c6312469062a1fe954451262908217fef6604615324e6e1504ae82a6757878f" },
		{ "Huem4f6Ehue5vu6JHq1gZxClgOM", "99d326f87d758abba5e6ca92b25e1f28bc4646012b360290f5104de3e7a1a080" },
		{ "HUFOZdmbOy-tPNhqdvzr_cC3PEI", "38131bc1c277f925a3d88138a95dfeea1cff0a5454fd2fd2abe1d85a7822e74b" },
		{ "hXo7_nVnMkiNLadPy9I0CQ", "1ff95b27b85e5f4d10944cfb8d9d1753ed10b98cce13bdca64bbe9dbf738dc50" },
		{ "HxRmiorQjteG8bq_4evqX8T7bh4", "106cb28ec514465e3df163e173e302ff46752003a25fbfcfeebca0a30dcad1f0" },
		{ "HXSNaUgkjJ8mioNpz9Eq0zcY8J4", "adb6a2bce0e7d6c945255206023f31968a90189dcdf0c5bf3a8919ac4be66a6e" },
		{ "HyGqMbw4V8vCWdjQmHCwQy3gJVQ", "244fcb9386845b4b2e9e8cce94f24b17bd669b6262d5dcee87787e5c1a15248a" },
		{ "hzDmwluydw_KNfEMcso4TDVNypo", "3e296e66c72dad226aa1164c35ce033709b429be805f364a690d246075f102dc" },
		{ "hzeMACh4Qivk4aGOWUrgiJFO2bA", "3ba3ae53b170b8477ec18bfa0f59be6b0d1b27b2bd1f3e6d2cd1f17a2aa51cc2" },
		{ "hZwxv3Nr7E3cYAF1KuwTiZ5Q1CI", "bd6006c43b21666b01700969f42f1f3798e89b78f5fe622986315345ecca89ee" },
		{ "HzZz4QEuy-TAKXNe-1oiohJEw6s", "9b20ec95e603b714f85768112e2b1f862b2b8144b326bcf345f052e9c8a600f5" },
		{ "H_nlISxaa1k-MOuNDbId8eLusiA", "81bce27a0d824de7d481f1638c5e8cf61883cf05938e931ef6883a9d52d6f30d" },
		{ "I-D3xV4uDTqD1Hc3pY43A4tCgt0", "5e05599ea9d4a9304743f90c7be86370730a552d3b04f040cdbea7ee68bd4112" },
		{ "I1ZpJD3X3k_Pfs8kvqXbvss2cqQ", "73d456ad24ca95d9b87da26096baa0d93a112404f25a18c0be3326503e52bea5" },
		{ "i4jtSZMKhddGPeGAmNgRPG6Q__8", "51bce38708ddbae5f41ae1eb84334c57f99c52ec7fb458f548d3960271eb7510" },
		{ "I6QtPFysc1xpPBkfIG9yXqRqKaY", "60a353501e2cce26627291b4432ca94fb1bec2c78faa3a831d55bb62b04fce3c" },
		{ "i6S4ji18YtODzdHxP0IS2L8HAMg", "715008a19186d6e803de6455313716838284ad335744ecda1140847baff76c8a" },
		{ "I73GX7OVKkCjmMa29U8Cxg", "b58e41f43753983002013d6612295354615ef64cd14a982a55217a31551a64ea" },
		{ "i7xtr8-ZbEuPo9kYPSNamg", "a33ca7f30417b2914a223be7b51fe679c4ef12ce91fdef61a5678c6e20f4977a" },
		{ "i81JmrfgLXn02rk6zaAqjIywLOY", "8bf160adf162d00e06c37871d6cb1789e6ce674dcfb4c22d763f0f7e6deba9d5" },
		{ "I848hiyswWebMiO-bGslp5X3TPY", "f5c84f0697ee55f3217453fb72e4e891d29a0969cad9cfbe245b46590eb28b2f" },
		{ "I96OpbHdxk-IDJWCFVJWqQ", "420d992b670b84b0c518d2e75379cd9a395f3633e2ff62c7d4e856e660c1e2e2" },
		{ "I9u9c9ij8US78kcLuei0lQ", "36b67c434076212980e99a4564d91b7e9909340dc14c247c309dd5424bf79eef" },
		{ "IahMFXMaNE-ADKUsE22KXg", "c7682d6faca129bd4cfd126bc10cc412587b644dbe791dfbf938246d89bbb393" },
		{ "ibIdEFk4tpgNTBZ2-UU-3OU2xqI", "e07ee6cb54b8c2b07a5876ee2ffe01c64c179b3f145de0d91bacf2717b4f95dc" },
		{ "ibYCk-cI1zGmlh7enMeVoH5aWec", "18b1d2da49f7c834b3f286ca5c15da40f3da17de09db41ee40b3de8245181220" },
		{ "idhdApiqTpQ2f0X7Oq8v2nbpuFw", "17eddca2b9854f515fbe363f95f2d684cd99ef4af95d59037df9654509cb28c5" },
		{ "Ie5GFTkDZJjGeL1vnJGPR5oaTEo", "018086f72cea1211d533648b29725f35f8464fe00ea0664ca0c0c5eae0c8f833" },
		{ "IEKGhEYll3k7v9RhCjJwTksVKFg", "01a972d2bb028b0ff0feb64d96928740f5806068a84d8582c54c889deb10866e" },
		{ "IeReyHffLXjgJQpiKfy1RDsaTus", "c767228105c1f78dc0e4836c35909d6175cb2c457c1bbebd0d4cd75bb0e55217" },
		{ "ierMwrbtCWiVGVqYaAYl1knLlp0", "e64e252a4f67cfc7d8b5b203f4d685948de71dc3bb29f8b4acdcaf3a45c7f647" },
		{ "IFaDN_fZPa-jY1hQSRSAJGoVZv0", "392d4dddcfb4cb3d5ecc67c9dcd1b7011019f3aa60fcb2dec41d8795333202a3" },
		{ "iGj65ZfpKUaYyxcESxxzNA", "38723e4d54fec75be117c8bcc145ac962d7aafcacd03ce5d9f8b0ff66264216a" },
		{ "IgJMkKGMtTrRJnkLsf7Ley8jWWE", "5d7eef928e15543b65e22284b7ee05df03e49d4ecf6d3ccd3337b58b4078b7cb" },
		{ "iGNYfQSvSzvqx14RNDNRmCGBzWA", "155488e20d7877cd5505fcba27658166c74d91b483c358fc4632381df2ce436d" },
		{ "Ihk9UbuDsDlEMh9KCG6XmPQHsnw", "d05b13253a4adc3eb847afa4d0e4cbfce0dbea0671a770c4c5c7ca481ec58a67" },
		{ "IIM9LWbtslM4tsNOa02Na-oLz0U", "f8cee81cef06b36469c1cadb373933ef30e25d3700853cef82e3b905c0667346" },
		{ "iJ0KJsrEYhywMTGpOJYugjDMeeY", "2763f9077a8772918e7d2f6188c160baa0ccdef55a8a25201fbb4318d118bdbb" },
		{ "IJ2_YEk5ySoSppmX7H1fSHAr2Ok", "989849b0de4727d4fe3a915dc0febfc0ee2c1eea062efa4c641fc3e171e026c3" },
		{ "IJaHIFtdZQrxyBkPTAAW_QVvUz8", "5b47a5c5a00209dceb990ce0452b7deb1924f7cfab6a1b5b2de2fa451636195d" },
		{ "IknLEAj04YlR6XpZM1XuUxyT3_0", "33895478f868eff01ecf2dbbedc0c912955fb1ac5edc4ef77271256ff6e34848" },
		{ "ikrnN1LGIoUmBPKWyBtOgE0PUig", "51e32bdf4e2b2733fe1db02759d560cbd3b83df8ca2cdd4e3ee4f8d9a7d985e6" },
		{ "iLlbVJra0NNoZmjjf7hkYjR-Kio", "f557658959b5cd16e6955edbcdcf299954491b4ff8360ff27ed155883cf3ab4f" },
		{ "im1VdQZiKrOwLeNRrpEITjoSTi0", "89e9136df9dd03de804ac4b7e01cdcbdf677c7bb1a4e97b3c12b6d51e473ce51" },
		{ "im5g1qsRtEMacnJd75YvsjgIeF8", "e0d40e59972c1c152eb3a25838ec3284216f43933e460fddbc077833acf3ed99" },
		{ "iMGJ5RbnI8N-fxVxOQWB7B0f4ik", "a27f4c422a116fc1542b89c5d83cf2548d40bd19d9c93a1a0f909c937730361c" },
		{ "IML-iiHmeUeS3mZuVo_4xPxUG6I", "298239fbffcbdbb9068fabef8d6f3b07c8f99ef9b20e654fa6c40ee88c75cdaf" },
		{ "IMZI_0pOTDhidsj8opT8u8U8eGs", "e2ef05b3f6a908e34a1e076ee339370308656d5f6f3f9e38ebacb4d8425a72df" },
		{ "in94HTjzadqPmS-lwj9vvRDnOVo", "2caa8c11554876a5c2d1d5dc235bc03ea4a212b029d862f3b5b0c5e88f7b8dbf" },
		{ "infgt43D90Ky0HtmhI2g9Q", "d8e7139337d2d3ccba222c458912c595770e10a5ee4d7e3d1a20fb612d030fcc" },
		{ "INyQs5C1PUdQcq1VpZbEbu7KXHY", "91a5d6de93724cfa4b97dea914a29658a8e0c9693f277bb426fa6453482238e5" },
		{ "IOGudCpk-pWflqTh2tnVyY_WLhY", "f7ba6685aefc3d6407a84ffba9a37f6c5e0504b17b98a31827dc879de8274b50" },
		{ "ipA0NF4T248t9UnuV68lyp4oEbo", "dafd929cca8c3a91d0238f6e09707be2ca9e42cdcccc73570444a7a2cc832be1" },
		{ "iSjmJbWpMhe0zxm3EU6kh-yaRnY", "0ce83a4af5bd919c5a5f51db30a90d27511d681221bc3338ee9ea89ea328db98" },
		{ "ismncCG2JPhxt9Hnw4RbU4Vw5qk", "172d57c26127c3e19dd4429d830a01fda55544d806b57b9f8074595243b849d7" },
		{ "iUU-nEtqH7PgS2cDb7jpF80Tq6E", "8f2b85021ee401ec39c0bd87bc0435ca0643846fe805e6633a77de0b0b9dae93" },
		{ "IVHeWjIxvX4Vb0kRbSfd3Lr0SKc", "95bf7bc7c41c026dbe554e7a56ed1bed80e4cc0e981cfa95b8003892fd56bd6b" },
		{ "IvX23c9QTVggQI_V5cdffWIkTIU", "02f7632dd2557df9e7770ebbc385ab50a2bb4be7f52bc2cf1d194c7e33d9a969" },
		{ "iW5zlzg0ZXZUa5Wl_lMX-jhWVlo", "89a51259ca9e1f886241544c8819c4f4d3d14f0cbf500a468f3fc9b5311d20cd" },
		{ "iX7NEGJs7j8-I2alv-0CIk9Ejv8", "2b8112df5a2990a0a0f75008c652a7ad4a3645ff31cb98333f4e61379bb698a7" },
		{ "ixbnIEky84U-T0Ozhmk_6yGS9u8", "d8416db7f8cf19294b3387bd851095080b1f30b71681a49286e3ab15d8e4256b" },
		{ "IXTuL2cAgmaswEfC6heD91zdKYw", "1c069a596ed63326e7d6f14e06737f96df084ad7fed85fc8d61338df352b075c" },
		{ "IyGoDzwbVWXtrnqF0hjHdXR252k", "02751c28f06b0bb8f4a36ad5933fc0f7f3d2188cbd166c032e399016f86d2e1a" },
		{ "izhC0qXVsVOYass927fIACAkHj4", "9c16add58e5baa54b8405f802d02fc087a7d2211d36bc64cba753038eff48abf" },
		{ "iZMAjxlEJ5iEDQf1BqxX3wTataE", "953a56cda15dab8045e92fbcde02b985c885a9e389f71f7346a94306ab0a023a" },
		{ "J0PS_XvSXYl6-jVDh0vHJNf_PYs", "7c17899710e41f2d14cca9f3eb665c4a0ee14beec3a31871e94fe7235ad7afc1" },
		{ "J0VyvSBMO2AQPWpfAV_thE11ock", "5532e118447fc8ccb157ceedfae7583a1944cd5f839ab7997805690205445ba0" },
		{ "J1rUjzh2wkG6UVV-lqCumjZktoA", "c2e8e9ff287202ae89e38ef77e376f7b66396f4c7442b911de720a6d4bf2f934" },
		{ "j2G-5EDxdkyNI1molFt4Ag", "a5db91285de25a4782508918896211ca56841891e19272dc0d40cd0665f87cbe" },
		{ "J3aA0lkHFfWtJfiwtw_bECJydrU", "6a20726aac2f855a1390741a999d366446ecee887dcbb372be857691f5a6917a" },
		{ "j3bdUz_6okGJelB3ub-pqg", "c218f274f538de6190f7bb10407bb3eaee767fb22876f498d3ee565820840b27" },
		{ "j5oEVxzL9JuFxp6Qx6BOV41V-Q0", "d3fd6b279aaab877bb6f9265bc9cf724700653fd4af707ba0a6e978b35d1d2fa" },
		{ "J7unu55bT0XTS4EGC62aCr7htXE", "8a0a3fa0f146878c779c22d31d1d9bbbd6536655b5cf499074983349c05ca8d0" },
		{ "J8PFPOuWMkG6CR6571juVQ", "1f661d699929b6e37dfb7b5b0d25ee43879eeadc5c199612389b77e89f1f7a7e" },
		{ "j8ZVwWUM4qNY7KG1-cDFkksXJ2k", "7ffb8ae1f6ba0aa1a02acfe4c169a4993518c7c910787963e349794e76f792c2" },
		{ "jA8XCJtI20KIZW-oHNZxfw", "4589d9775333e20a433548bd9d98d54bc6b80c3f6466938208b7d16bd7fbb243" },
		{ "jABvh3weE4Z7609aIFIQfnwaLi4", "7af26117f3c597ab9f87658dc37a23cc82129b6e0cee694f1c9bb3626de28bf9" },
		{ "jAKY_sI77EUT_T6riiJ11RxPj7g", "0f05578e18d74cd4894b06aefc5124a466e898502cf38baa1c3965823b73fe94" },
		{ "jB6bDWQIST-rrx_lMm42BLY6m68", "cad4a5c355db5eafa557a5e554bc54c3816d1b287c0efcc0c749cdf86cdfd26c" },
		{ "JbkumhpSn3xv0CXAuCgebAR2Vek", "60e378a7a38623d57af47da36c5f0341229a314bfb8fc389f6c17df4e1c68a4e" },
		{ "JbW45S-x1yRA9EA_gxF1aWUHY0A", "ada0523236d28aa9e05c89cd6a2d531ee115b55b1fc9ee84b7d0627a5e641265" },
		{ "JbwvBLqhAOdaBWxNPqyoas2KhSQ", "21d4dc5134073a0fbcf77dc624c7ccf294cdbe43f9c24db006faabff0097b2ca" },
		{ "Jc5BwcEUHHhCg5nW-ZLW-07TSb0", "83279d90d793b7f6054bb7ea133dcab2e3c2b256f3268469d7d80e100efef5ea" },
		{ "jcALaoTnqb_WUAbdLYO48GhkakU", "9bdc87699952a5a196b3512971d9cbdef6208e9e5c7b59fb5989a2b3f222f366" },
		{ "JcdVziVZ4TAWquNCLSTcTZ6cKt4", "6c76299add33fde51fb0bdb45e00865e5f25368f0d2ffaef277d8bcb8e5cdc5e" },
		{ "JelMHaK7nJK5ZrN1fgcQIsUx49w", "c263192b284c31b3dea7269006fb74f506189240e4933d77c0c31fa9bb114ab3" },
		{ "jENI9aUcTZk-jmGJpGClbJQ29KQ", "76e4c0b98982af169406f15a1299f0207d9dc96ace82278add2faa0ddd5a4d1c" },
		{ "JFj-uhBXdSb1pouZfnO9hSpfits", "3fde615770b7280713bcc06951a2678f5384f926c1f5df94d483035a1446cddb" },
		{ "Jfpe11cDvjoFSSeVWOKHBOCr_XI", "a5fd5498c916540a6123c7fd8c173a4f1bd7107131070e88b72f05382e865c2d" },
		{ "Jhc5iYsN6Einb96oPD59nA", "ac596b959a46d8275f35c52bd9104f1060856dc8c8fadfece697e0cc92caaab0" },
		{ "jheyOGMv27YG8_eZaajhOFcuk-Q", "42996e01152a5b6d5337e6f53ace5d13483c6a8a1197870f1152c39aa8451322" },
		{ "JIJXPPgSKpKVl5X37oz8uzSKi1w", "55a9376cc1272990d7caeeb90397ef23495c27e8b822fe7236eb309b80336e65" },
		{ "JiuS0FaEeRd5J1_WCIrC2dVEPno", "12e8044a82a7bfc855f5b35cf2efa5187f4c1bb76fadb17fc684030dcc3c9208" },
		{ "jMhhDMWNFU23k6imOqKXPg", "0105a594091fdab3daccbb5d95296161db41bee3fc30588681bbbd09dae7d6eb" },
		{ "jN0Wr4C76Ov28oZR5RFcEBtTfYE", "9ffddc16766e036f124d11f057418c02da035bfb4af470667e280f06490dfe53" },
		{ "JNLm1O45QXuyAU-yS_fCK35i3w0", "8bfe9b77ce66374638973cb1831b5b2244c14a9fc1b0c894600f3688585282c0" },
		{ "jNuTlTyDs9VyUKRk4_U8LJlGJm4", "d0579943f653e8e5f035862ffde20dc9f07ee7edf71678d00283e5d5c11204b6" },
		{ "JNWhTMQBtfmGE-WPg6rZ42ipEng", "5127c6e6552e6fdb503c3e7f54967fb9d4b9a055fe3423720d70c911c8644850" },
		{ "jo1ZxonNb48ulmpeHMQJJxhu9Rg", "207e8534bbf3c51af5c37c2e057d0552299c3e37aa42cb433e4c4ac89d06a763" },
		{ "JOnfud0WLsvt64r2i85dR0F8yE0", "c59cbd6e3b7af37f3cd42202a81bedcc93b23ebfb818c583e3422adee81f30fd" },
		{ "JORxb-jFWEGOWwyl6C3qyw", "2e63de4341fc36bafce919ea932095c0ce77ecc30d5dcdbe7ffc9960b09cf941" },
		{ "JpJxcLYVfA5f9A7sfeW0dg3FsKQ", "3d7adde8809ce92672b5d032839efec0ec7511f19b81b28dbf1a1c67d2713458" },
		{ "JPp3Ic85kylFrhgP4c4mU8oQkgY", "c28d2c3898c11150b08d6fffae2090fc5d4953bbd299911c6279c58a6602b4b2" },
		{ "jPZX3jUwFDIpO8ixH6JZHq6KvL4", "72a06447c4e7f41710034618a407c41e582b9e2a3995d5c8f9a2aca90b085914" },
		{ "jQ-oEbYcWDfPCLYvieaYwsNrjMY", "d29de4432a7347873fefd0f26d6d65187658896cb18bc85ac593d1aa3fb47c71" },
		{ "JQGNPV1JTEyrYP2IgwuyNA", "b6da99f14184b32eb6332605fabfcb39812d03b227bd5f01df2dbc38a2d7e91a" },
		{ "jQUH0cEbn0mmFSkTk5IJmw", "c352cf10c743c0923a1df874c3764964468ef5c38939e18c14fee1da2126fbbf" },
		{ "jQUH0cEbn0mmFSkTk5IJmw.meshx", "c352cf10c743c0923a1df874c3764964468ef5c38939e18c14fee1da2126fbbf" },
		{ "jQVuJu6G__SE6IF3kUPOaQXgfGk", "72912e6fdf7c7c5252f41a841e404b38f0706db6dc60d8055821f947ee6b0c73" },
		{ "JR-OiWVMxuYkDHs1XIB0SBOiIZ8", "5f43237f70a4470025be154e4b5db42fb49d508547e9e9aa791b5a3e559bd718" },
		{ "JRbX5pXP8FZv0aZBSCstbgBKXqo", "fc7be019a3362d410c29d40016e91ce46b670240da99e3e23a2e3a6f3201ac2c" },
		{ "JRmsz6ytlpkbh4qg40c_i6dmxyE", "4ce8c139f3f71590873869d38bb91964ab260a612014e42f914c08fea5216612" },
		{ "JSZbrdyDC80m_QzmY_ergMxQiYk", "7b4e6d9f04c3d73394d7a419625e112ef88aa17c83a7ceb4b5b82a3dc47b9fa5" },
		{ "JTmDZtGZ8Wwav8UoLqPNtQYkOfU", "b383e7199cf04479cd4312a41b7558c705bb9ce89528dce20e942e59b775cdad" },
		{ "JTwhXgDbVX46XlOVa6EHcxRNLtc", "94f426ce088df57c87004978f3b6be81d4d146501d334ff3c4c343312e911278" },
		{ "ju6-CFTt1YkE0bBh72rBO7llPP8", "f555885190cc9f66538522da3d310b3446880872c61378313f2e7879949f5e48" },
		{ "jULNJdv7eUDwbEnHXVFIfcxhEys", "5b252ebd03db8f746d9c0aca0697d58d934e82c35ba81921c447f85a3e45462d" },
		{ "JUXnY8Q1ynB7ECIM3LvPd0-E73g", "29031581aea71a73994e11f2c8fb575969212babef76ac6deb9a8fdc9a0ae237" },
		{ "jVNVYcLOm-yB4bH6nFpLj8g9810", "312db25becd502be1c8cc1173149ce1c7455b4b7acf7935db52f748bdfa189d5" },
		{ "jwudUjRBltYDUcGtvuTJXp2MGD4", "6710b9ff75f26db009bf56d7b40e9c30c4e17868a163fd041ea036933cfeddf1" },
		{ "JWu_C_8TyJ32RzcX6bZKo2KvvuQ", "6664b0b9eaee7ed720aadf85bb73842d8c66adbec4b4d6c07a9b0173ca126d63" },
		{ "jYJTv20cwQOWBHtJ0vtj-F3Z92s", "dde99aea690d54b060c90d4c89c18e91bfa9799108167c3c45439e0a1aa824d0" },
		{ "jYNA5tDJFRaqnkxzEJi3N-dgxX4", "82d96f463fa5dfc9b13b423d1f26f05528d3213c82e924a6d3648b97d9218322" },
		{ "jYSiC5eM0YAu-ZKobMcNi5-s6iI", "aec90bf2f5e7e3b6e06e6ebe47a06c65dd36ea136ecdda9d14fd54aebb970fb5" },
		{ "jYt-qzBWGowMKkfx42Zz9QRicfw", "7eeeb9b798ed903870b5fb5022f54daec39d76cda170f5be06397a6164051e59" },
		{ "JzTxwveM_L0ZML9Vs6Yz5upSrZ8", "611bb4f5188a4837bf876fe8dd8bee5145f60ac65b069a8a45f2bac49202e1ef" },
		{ "K03eEbIJEQMCO5kLSULqAoASGjQ", "0fab6b722e7b498fdd6b74b08cb1ba7e234ffc2da305c1a08b3d9c26561c39d9" },
		{ "k3m0FrrjXrBgrWg1kxPe0Frsd8s", "bbf95f0e30576d19452dec55177a1059de7e99e3406cb919b67503e3edffd344" },
		{ "k51AokpMJAXhsbmxaDevzt3ladQ", "ee9fafd77681b5870530c5fb8c883bf6361059c6f78bddb5c3611a13fe29c348" },
		{ "K5rCGMy4T74-NICafNz7-n20ebo", "6246aa46f0f73a50cc401dcdaa62dbd29a21e59d6323f422c6f791414b51b75e" },
		{ "K6QVO1VhQtWDEOo-0RqNes1x-Mo", "80eaf688a1c8e58d2c6f5c878c4cd7db241767dc05ff52f9c1afe9673ffdcf63" },
		{ "Kan0tr6WDkDs0V8VDeRTY7_NGlE", "082ad98dbb5efa0e1f74797e8eb04b94fc49cc1f249b1a47561817b4e0df258d" },
		{ "kBLPjhiqp0ar7jIl6UWTFA", "98f7527e68e774fa49d4534d17cd396d79d7f70a4d2da00937cc0728d7e389e7" },
		{ "kBlsrVY0JbQNaYcQL4D9SErIbCw", "a5840325ae465852fc5071a7eed351062423473999b9265b07709317d88154c4" },
		{ "kbsKysQjjMhdmvReqjRmRpaS52w", "474f9980b44a2d085bec3f6f8992b8446bc6fc177e9ee52ba28eaf9ae12f1c01" },
		{ "Kc6q5RGyYJza6ct8XHKGRJB57_o", "df2f2232ecbe29aeaeb0ffdaffb616fe35a1d5da89b3f438405fa5a0c1c37021" },
		{ "kC_qCjMBay1pBZid8znR2TNfW10", "39308124d1c36b431fc7e6a39008e552ac92389298d14602e0253d98e965619b" },
		{ "Kd_SdZIEKh3XblIpjpbqUSIuH4I", "976cdcb5b2151eecdb92662c6ea2dfe331a98f268d7d251679bc9b01137e0312" },
		{ "KEk8y5U_l3v3YBHd_vJTKXojIVM", "28ebdc019a1e903b2cea4b284c82a8c9e87f9f26f7fc4b41c6e2048818bfce03" },
		{ "KfGNq8WIrKw262mv7VgMg5-uFJA", "7d6d76dd5d252058a6c2a62b397d073c1505c2d6aff6f0a22d3e0fdc6d408f73" },
		{ "KGDlddSS9Cdzc43CxNx2bsukHMw", "3342f3debd2c3ad92b5cc50e107a7e0d6d98340b8dd1944b58ae2adf2384c6e9" },
		{ "kgTSxxCHdip0kWGy-a6La_OZiKs", "45d432457b54a07c041fc4a979943df38e255150086c3d678bb266e9264671ee" },
		{ "KiA1iq_zTuQ8ClT8-4-7hda1XNA", "4e5991e2d85bb17edc899b02452ab423b17b9975968e862935b45e8a09a4e1f2" },
		{ "kimb64jkjcW2Gj04lVIOmsryyRk", "0872774e749df4f62047b1cde2ca5ecf4d3df9ac448dc9398f71ff490ae75c3b" },
		{ "kinTinv2Lkq9T9IYIU5CDg", "3af2beda69f382a4af87878f4cb26a57740bdca348811e5e63a38dec24f347f6" },
		{ "KiXlM6yUEp6mas3tmK23jhWfMu4", "525fe2c0e9015243bc1958e8b16c6944fa81763342a4db788bcac8c17ee3beee" },
		{ "kj6msTqsUKylS6aE-RBO3fsYRMU", "4da52d216949aa01d7fa1d8ef77855f395e6d9c7c051f6c7ab64647d859ad1e6" },
		{ "kjCjdc3byQS6BDl9fbmdHxsLE08", "fc25c1d1bb9249df246a684e89fa27f523fb4dad6e9ef97d1ef7d39cfbb1ed56" },
		{ "kkxRO8wVqk-CGWfTAT3-gg", "7957f03e77569a1d8cd0bb7810784fbd57c9b9ef8576d05604684ef3758484cb" },
		{ "kKZStxMemlyHcKQ3VQHm5TpD8bg", "38be7ddf29f1d1b607ba9481f94e4918df318c3dede7c36a859dc450771cbf27" },
		{ "KK_vRLkRLBaJH3sGzjvAy18cy9s", "73be35b6d91cbfde62bd004bc8d57f75c09a3c4c03562abcf2256849e7ad6bdd" },
		{ "kLkr5-M45CBGrrLrsB2i622xZ6g", "69a7d1ab681e038169501fe36ec821373067408909a8895eada6e66d2c46cecc" },
		{ "km82b9kG1Hp2cQYhTuDsjUPVmlg", "4594296513fd476c7cc7b9b0c3bb53a14e8068b70b99f9e920fb20bbf7eba303" },
		{ "kMf30b0TLvzipqVMXzcYOW23CWs", "6f3e7a4b2c4aa16bdc758b6df5eb547cab81edf780ae2c5c0aae5662fd614566" },
		{ "koz3ymvXq79DXl04MwfhKfPc3-M", "76c683da9b23aa25ac2c589d6e91b53d0196db071e019f67553e5ec5ad31aa52" },
		{ "kP1Eb7WctSLU5Q2P2qPXJu6IVbU", "ea83314cc509e457d80772e1a846f4d6271163253e939efcdbb7533629d1c1be" },
		{ "kpOl2gOQuUehl73gS0hEr6Z0jBQ", "f249c7e181fc60ca9dccfa6c69e60731218de4cfb007cd63c3a8ffffed07b284" },
		{ "kPZCpi4eHUSB_tlrDSKMNw", "e5101917a5e680d39bc4d8fe6ed6bcea31d1a52d0ad95e28ed9be648e317591e" },
		{ "KqpCNY3fL0ghvCesKrkTlhCnWw0", "6183cbc8eb6a7a47b395ef1b6e463c3692c4e126dbdac5e63706b7e4c238fc88" },
		{ "KRrnLgYuGxt-6AAZSkLnXKUavZE", "cadaedfe89bdec944d1cfdcc5f0f3a3d490358fafbfbbcf16931db1d49e7faad" },
		{ "KswVTCrnhrWaqPGP2l2fV0Q_EC0", "9953bdadeda37e4b2187ffc9b27344ee975fee0dd7e0c806833bad08f576e850" },
		{ "ktx5vnXwaEyqquP_zEgQXQ", "c58093fef52976fa89ee7878a36e71a6ddf088c8b9e250a77ecb45c1d3dc0f66" },
		{ "ku0P6P6BQ0mAw6PmPTek4A", "7ed3ccc7a1607b09f4a90ae4bab02b66a7973de1da14534a96aeccdbc58b91f1" },
		{ "kUmKEj6bUasxTZ7VZdrYJ606Zfo", "e90e2beb6ab755c78bacdb82a2812b5e7ff7fdafd4d15a79527160f9589aeb9e" },
		{ "kuVmFBFyzGSdrUp5WJA20-BECwI", "96a62a38d983da0b6dca4bcffa96ea6fbc209c96c97eb691ac0de49638db4849" },
		{ "kvIJdfQkitkifRs2dhyfl1s0ZRs", "e2378780a606c10a7d758cd1cabf2ca4fcf336f75f738f5d37487fa198487ab4" },
		{ "kvZzLMm3KvN7msOBrMEEG6S7HE4", "343ee1731357890a7a1380ca180874f29de59b9d6b732fa1427e6fb506cce8f2" },
		{ "KwQXf8esqVIUBZeboNNJEN_rn6o", "8e2a72ed513fc3d7d8ee1ed02192e8c6ea3031aeef78f3c471d107e31140afc6" },
		{ "kxHTJ-EpGsoXzrCdcggLKcdGcng", "b27449d4dac6c49dd897922cff73e2d922f23dc641d3f43a6e835405a598587b" },
		{ "kyCDxQSO6kOAXho6T8RC6g", "40a7ca96f5f4b458308fe79b2a811ee0fc2b0e17317e0f988bcdd92ad44e88ef" },
		{ "KYWpjyidxyakWMNVVSM5Q0i9NLs", "85d07db4148ff51e7ab5b05860d508ab717fa795a3472f483095b3ebd993b36a" },
		{ "KYxlN02Xm8qY2A9MfUJLWAK3tZ4", "c9f418085dda1394d08245eb5f9bbfc42ebb1d2ab8a164464e6ad7fe7933369b" },
		{ "k_qf4hdgl-3nkl4-3uhobX0vt10", "d51e81b89f0a1923bab95c69d314527ffc622ad7aa6d7316439062eebc1c2ec8" },
		{ "L-66HK-mZkNo2SKna5aPI2twJdk", "7705fae0bfb474fda43392e7bf27ab2101f079c89438d04777e6558a9ede44ee" },
		{ "L1px-gtg6djPfE9DzoEPJPelLBs", "dc2c34bc06d39ab1214aaffba9a33e965bc47bf397710ba6225d135226d5e2ca" },
		{ "L2ENVjx33EWCD76gkbLGfA", "49205f1e8b3226f41f745c56723a942e4b74c69ce290dfe6c37d59c68497a47d" },
		{ "L5s7V5-3QxX5G-j634l9h5Ve-n8", "0003ac7a198237a95c3e4fe22c41ae175c723d85cbdea0046447659bfc5c42ef" },
		{ "l6T5WRVbNE649YjAahKSiQ", "4090d254e5058290fc61f61e595b91307ebfa20a7cf03effe5fb08af9870bdde" },
		{ "L7h6LBDqbEmglz_xQZqFiw", "fbdba0b940871628632b5b7f3da009cd4ebbf7eb2c860efd8dabceaec1872863" },
		{ "L7pdZPRcmbEF7sVQeO6zKSldtNA", "2448059ca3279b39dc0623ea6d8224749d22ca5b785248a4d21765c74ba40ffe" },
		{ "L9lxiXY8CGEcaCbfaFvwHzSaqQ4", "a9bfcd0dfce07421e51a16f48e575d9298a1ccb17a9bff90b4180d3b9ff858de" },
		{ "laVDg8yOzBnGFWgfZaoN3t277vU", "3285939bae74a5aeb3c14241aa64ee94f1fb56c5c89facbf54a2768196c22844" },
		{ "lAX-afQ79Ie9GRToScaslsqTG6Y", "dc3789e9191546e55fb8478224e2e92e243952d6683cf9af1e3457214cddda89" },
		{ "LaydMOoLr3WyOQ2RwlnDAS8--eg", "c473b5730960bf893270849e8973f69818557a056fc66d7fc319c609ed9167c0" },
		{ "Lbmn_0-Ek3yW7W3MeiU6lCqmZxU", "d2aaa78ec75443882f1ed9780d2ba25fd45475f5b08f638279933f28188e5e8a" },
		{ "lbpUJ2mIEXkhcYe9ZWotQbf-Z-g", "4dae4b517b19846f43018cf1db14b9c50a53b4c726c5cc853d31795a2b4cc445" },
		{ "lBx65RfBCpAYvldlJcGpP0jzdsk", "3b7b47d758854dbe019e64be1eef1798e419b0876ecab1bad0686d12aac34671" },
		{ "lcHC5GOM11NGHJHmcrk_q5c46to", "f2103addd6cb7497d9dec128d58094264366c599fd64ef78219f181ecffdfdb9" },
		{ "LCkmZ9-3PC7Rj6PHJH_SiwLdDYY", "7ae26ed05c451fff664d29bda64ff8949b0effe4e1493f116d1dccea4b1a18ef" },
		{ "lClw6tF33VQV8pwiu-5--8LG2Y8", "8c08d07ecdbebdb449e532a5baf06263c051a062ae3b51a10f0ace68744db538" },
		{ "lcrVhfTi88an0D4360Q1cfCGEqk", "8e9530f72a231b23575a535f241039f3fe7f57c3a0de7daa8a29ec556a4c18df" },
		{ "ldjYfpaNIUGMFS7E46SLfA", "2a9910e9d640159e943d07ff758cf81a9d6e81db1f992124628754a9b0ca6d25" },
		{ "ldjYfpaNIUGMFS7E46SLfA.meshx", "2a9910e9d640159e943d07ff758cf81a9d6e81db1f992124628754a9b0ca6d25" },
		{ "lfww8se7Ck2JkbdrMbyrhQ", "464e634c55ebf505dc35f1e1a2cdc6f5cf133a1906588f188550131114736e57" },
		{ "LhK8sdPccsqT_QfudK4L0EDT24c", "67f40a5b066eaf1497bbe3e347db8e06a2522bc4a1e1b7c9115d10c391990162" },
		{ "lIb4spE3N-XLZrMZkW_dPeIxgO0", "6e3419b18287374f8153c5f0b2121f87eadd775f5b348058ae5c26eb7fcbb9be" },
		{ "lIOpUtw8nabfrrHjjVw_tptgALE", "249cc43dd5d520338ed95ceae042b10449d3856a097b00573c5fb1925c8f9480" },
		{ "ljbHGncb9YzWo1E-Cfe0CNh5l_4", "510df92ea74bb86bfb056508ed1a60def2d8551b7d9e18ef507dd5ded0482436" },
		{ "LK4E71fyhZhTMFzuiChBijqEPBA", "0705d7b893b755adeac0cc684fda17284ac17efac71ec896bbebacba1a759ae2" },
		{ "LlG5qXcnwfJandalumePaSAKWoI", "3b7c0e1918893609013047903a9175abcf84946d87c5574aa6cee312dcb229c6" },
		{ "lmcZZBUh8UWhUDWpIl6A-Q.meshx", "07d6f6e20593688c6b0fb055d4c282160a65b7e408853e1f95c36e86c1d5a133" },
		{ "LngHTtDqWh77Q1tZwkAN6yoywzQ", "345b60cbf09b16a7f651610ce490cd666788e0c6d7f7cd4967e9271b98ce0cef" },
		{ "LNKcJUe3Vs62jtAjZz7rsu2qeOA", "dada12fad429a1de64040c4928307df8e06958069c794db9cab8352ae1586d13" },
		{ "LNmQyoK0XrQT9eETHp94vI03vzM", "c9113a367cf412a920fa09e482a177d52f85676d36578d850b61ce357e4be854" },
		{ "lNys5DSmc7nW-YiHuthtqaVLj6o", "645f1a3b2aa0f45994a806b80ace939f50afb48d746ac0a10ab421582ad76015" },
		{ "Lol8F-P9JwByEj3XBImZwlb6eMo", "231f74cbf31d6230deec88831f83f8c21cbd4b32ab6c3d5457e29755a82298f8" },
		{ "LOnjTaHPi27yAFkOdYlTUJJH7cY", "485cd64c0ca4b6f98c3f52f61e0c039448732d6c1a5488ca1ce7aa01e663fee3" },
		{ "Lp0kwtBvxRuPWdc6AqdGX3Sonx4", "d4368a572716d6bd06dc926b6d8988b80f4512b77abaff447c1eccbd66e1a414" },
		{ "LpbcvvJFfsGJNfLvYRxjr-RmCR4", "63980806afc0b28158c18f14d4c6397c784bc59ad4552a0b0cb34dd56bc5ec35" },
		{ "LQlf6KZQYkqnEHc93y88lg", "0afce812f92650805489fa95c90c5ecedb76190b1520f98f152801bcf2c282de" },
		{ "lqzACtvRmHew-KekuGlR1hYo95M", "19af35f7a9fb795a35032522a149df3faabd81c0557e270efef1dcd3b9c3a773" },
		{ "LrRcOdGa3A1rhTlKKpteN4Lxe5c", "8e97192c068a090e439c6f550552a3a7690711153849362a176a6df462c17204" },
		{ "lRu-mvLvzx-k2ttVWUDZeBtJG2w", "d55a8b365722ca2badeacb211b3cb4c6f17cbd45bb0f7a132dc5d34a3f64c711" },
		{ "Ls2X1wrBk0ujViOQ2Q0W5A", "7a3f087690b6010f953922221a9e859ae88ec04ae414d5abe120901cfcf677fa" },
		{ "lSGOrWc9bC0dKBDqpXYwQcYYGLk", "bc173d7cb7280a2e335645e65854efbb6e42ba33151e3b798856a44e1575c01a" },
		{ "lsrUotDsvHboxaEOAuM_83IuewY", "81076f63fe6cb4f12856d128f23bfb6ad7b4c82f600e63bcbc90cfe9dda5305e" },
		{ "LsxxsjF60V8uSMk9xN00l2c0vYs", "9d9b2e00ba7e3b99a5ea0f1446b47a75c2aafd20f0d00a803d42d9468c3982d9" },
		{ "LT4MLnQ0aFcBtb8Z7W92-jm5trg", "960d1ec07c232a34752468d89c15b10150211f7a8242393e91214574ca3e35da" },
		{ "Ltmi5tDlaAPJvQrFnYDBbXWoeLs", "0e5fa90e20f0b9ef3d64ef440330145ab498de6ced177128e5720a3c2454ce75" },
		{ "LU1XZPknQQE2UNm54Ln2WsGI-2Q", "54eb30580a5837bfda83c47a1d9a4284b0bd4f576421505a3efc2c7d9ce6150d" },
		{ "LuBvyLlnpUCvUw2ZDABGOA", "aedb44bfc44c145d8e227e81e914f6ec206ee79a99983bd446f25b95c6d2a9f0" },
		{ "LuV0DXH_eE2OSVWozqh7hg", "46ea62f9937e5954fdaf96b2f3ac563f16fabf0c19739c50ba3e00ddcbe2797f" },
		{ "LVEBhn1CNg145CDOvs1EghIJvBM", "5561cfd4576a7ce3b1b1e31c421e5334dfda47394c502383174a71d1fc9f1cdb" },
		{ "lwmcNzXj8LeN7MMxj8ISGdJjFNw", "5e3ebbd8b288930d5c97826b523ac3908fe209062d91d14549896242a4153d29" },
		{ "lWQ8sVkVzZSivnHGR8-Cxvo1EPU", "52c9a2fda7f853a60cd6e7a08315e8f765034972a835e82b9d9617d52cb99780" },
		{ "LwrZFDunpm5EQGHadUPYcJCn21Q", "b62294a37a86033bb57758b4aeae96098fbc9e6f57b7a74e1d7a9000aa95338e" },
		{ "lX3duFBOTUu8uURW50tAag", "bfe42d8ad8eb2042392e17805b788a27650b73ed1af387a078af17090b50fcce" },
		{ "lxnmgqm5kfO2bctHvAJGNSRCm54", "81a57c7408102b5668b168314a82adab29b9aba55be9d1536890ef52ab1ef92a" },
		{ "Ly3XHLo7FwdRZ1N75TVd3ix0wIo", "eef553de4d441e7180a7b4e7c80bac040bb133bdce389642101ba2c399e187a1" },
		{ "LYe34wlXQS0JPCvcx7s9lSjPX6M", "c5689ba1503d20a72399e1aa2b761e16d41f8075f0821edc0125fbb129e69718" },
		{ "M1j86CVB-jnRbd1rTqYyQ4tXv1I", "988819369c71f0ee58fe6741cb5ff098fc9843494cf0f0ca6d49d7f391232eac" },
		{ "M1v8qRc1vOAM3gcQSyd2JXYxUEs", "b0e433a461151cd2b55bd564a5e94a8371497f122605ef3ffef79b139687121a" },
		{ "M2zruLTziSoumZL3p-1AbBnD28Q", "e0f65679dac52e47ef1e027a916affab016893a667c98044411b16dbd4d4ec93" },
		{ "m6MHSWNQZ8xcL6DqR3uYYQKDoLw", "d450a246d7b9a42f40aac6057375f221b9ded740a546a98eb29560adf105ead6" },
		{ "M6qpQahRMpN9VCoc9CJjZmWMXvA", "f14bc0fbd89f14204b6d6335aaff64924d1316724c033b83ed2558df09c190e5" },
		{ "m7ueXbO9ydGQ2XYdyX7VnJihr0w", "c611f5b98b6a401a10572d0292ae5cbd283cea727285db5d124fa2a9c58f5c70" },
		{ "m8TQa653ycF212eYidGPoxhUIYI", "3b22cd4b2c0beffff26281c197b791566a0ea4540f21dbaa2b780c6dc0dafd87" },
		{ "M9C_qgQJTKMxh-KGhlwOzMhhmgk", "58bf2e7733daf48df30ffebc6578982bab0121f8e63692a907a47dabffe6fbd3" },
		{ "m9IfuySpeaAxeVkbd9kQFfutt7A", "c02106803ad321ca940cfe52e8ffa98fc1204b2e0a25c73ec5e71f3019bf33d6" },
		{ "mb6YA-MAS_AMgWf2ESkPqA8Fntg", "4fdaa6c6320a45bfa315649db1223b48eb3f97bc760a0750bf04fa30007b47c0" },
		{ "mCtNNosUHLY8_vXcdqthQg3tmdo", "a669c412294848768a2ac4cba5a5f7ed0b028b3f2f2cd569213c5a9e2c0c51e1" },
		{ "McxiLkDLmq3Qo8VefX-r1hoUEp0", "634ad0c30fe49a8a98b240de4819d02935a2a1c287d46c9fe903e86137226256" },
		{ "mDCWP0iedA1ZC7XMxF0IYWnDQ3I", "ac3061297f7800860dbcfa704f0f463bfb1906a279ce25289b9239a8f798534d" },
		{ "meEqE6mt8YKkTXCNLMANwdVMlrg", "f7c668ca1bcf7b55dcd8fae84da05a336ec5040366c73d763776505baa19dddd" },
		{ "MfRiwluOJKvruIIuAHAsd4WS2BA", "c73738ab5734c78d5b42709ea3c854ba671356fc9c9badce1428ed6c6683b25b" },
		{ "mGb2OTL_c01ZmdgAcH_yVzbzChw", "317df332faf9f7f8f227e9d4905b294b824953b2d592b2636c24ac60b6db8cb1" },
		{ "mgEWf7IoVhQO0FD2JThGNDxPTdc", "4a2fa5159c48a276558a6722497e12578658d8e2d816d39d424a1d186a048c31" },
		{ "mhrrE2JGq06ht1tkEnLSeg", "9559f1ba4cd94e5b6cdbf05e8a9dcd48c9aa4adee4285f87e18b4270820bd03c" },
		{ "MIzfxwfOUAHsyMfFt3bPwBhPgyY", "a4a8bbd68520ce1ada4ed2c153b3a8d8fac1a80ecd64220e8930d76ad5f5108a" },
		{ "MJ16obQx6Br-ZqCGqzlGzSc9GLI", "0e56fd6509e8be53f182113155315f5678538c11af185d48bd9e77af4001b862" },
		{ "mjPewEJTlEO1ugUPLcgIRA", "ea05bdcacc7ec71ddab49cf0efda872b15856f12409461b9297edbc242244519" },
		{ "MjSoBdBSKXK48U4g9JVx6DWdZk4", "a8033e5fbd693f78bc973e50c530cd2b541120523124d9c9e77efaec44dadb94" },
		{ "mjSTXVfHwuCNlqXTL93uK-081MQ", "308c6a2c101ba70ac19b739aa42538f99e2553922be706fed31fe6d59ef44d3b" },
		{ "mJY4VAUDIdXVG7nxiW7Wr5D5W04", "9dbd8dba3c337fb2f100f1d2dfebfebf4a36465297ba4c2fe63841dcefa1e9b9" },
		{ "MKskaWMQV0q_hCRtgWdu6A", "6d6b8f20d75507ae7935ff4890dcb15b52444f3cee8b34a63073943113d15c43" },
		{ "MKskaWMQV0q_hCRtgWdu6A.meshx", "6d6b8f20d75507ae7935ff4890dcb15b52444f3cee8b34a63073943113d15c43" },
		{ "MMRDPJADKkRtKSimCRJrnH4B8Ig", "770a3a250ca58948bfa7d6302a587e8439b425689067047c782417237bfdcf49" },
		{ "MOWSrfhWMEivtL7VZ0oDuw", "4b179eb79eb809b09072d92a275896ab702ec1379d4bdd368fcc795bb8ce0557" },
		{ "MOX3LuLM1qocufASVCaa5lLXOu0", "d2ea7e4a6ac85cc1f95f7c7a4c87c71e716c7a6c1373361f6fd719cb967106d5" },
		{ "MptAvWqfz8kmrRuP8s3hoGbaz4Q", "b76a28c1d769e58e59e1c421f7367a23921ae0f23396c17db9f96c89701726d8" },
		{ "MQ15WXOlO5KLHYYSPeMi619dNbU", "5010950792bd7b5091539af7cc8693bd6f99d884ad360848ac82f334fd8f7cd1" },
		{ "mRQpZfFcuONN8pE_s4zQ7gB45eo", "7b1f121950e348e26d364f316623c6af4afbcd14ff28d911f5cfce9c53f38685" },
		{ "msjqmqvWBJDuvMQ79gzlTdzzKQ0", "99a99bbb6bceee120f04246ed8be3a3c09d17799ef673eb4921b59bd86a173fc" },
		{ "mTv5Ije3tu0q3mgHcboWvlbmV4w", "b1a118dc9568cfff3ef0b3eaea2ef938d237cf630d71c055713ca412c6ad2741" },
		{ "mU9Cc-cvyroqdB-VdTyk_r-DIGU", "959f973b4bab5add9452b4df14dc769bfd9c6c99344a418847ada91e644f3a08" },
		{ "mUiQCKgwfy6mvEW7_J69oq0gAPg", "69ccbf19031c652d91b64e933c352114254d7e118ba7c6395b2d35a6f803cfd1" },
		{ "MvbUH2XDaEu6DTvRiInBXw", "e0b31427bada4b0a06bc90c444929f2b5b78f51a90170560f6d0c164f62b5c75" },
		{ "MvbUH2XDaEu6DTvRiInBXw.meshx", "e0b31427bada4b0a06bc90c444929f2b5b78f51a90170560f6d0c164f62b5c75" },
		{ "MvKRJgDG4Iicp4HI04uxRABkbC0", "6a68c658a0f928d074f3cbf44baf635b395b27f21d195f467ee83ef4c618a225" },
		{ "MvwVK8-3lHsTzOuMFgm1itKX1oE", "0b3609819acf9b25036e4094ff6d1b8676a28ba0eba9dc591d713f6cb8e176b5" },
		{ "MWEKxYNOkUH2H3NkxunNzydLqfs", "654bec10d878c46be8c10b6bf970380cf0a55853df7efa73bd9af8a0bc9eefd7" },
		{ "mwiABXMYTbE6yXTAuuH6-ar5pkY", "5b62c8eb6e64084f623abdbd37639f92800ae5e861930dfe58134008caabcde0" },
		{ "MX0jWgxct09Lv7eqDWebVtRY1Lw", "4e1e68afcc653f6b7eba0f6fa517ee31adb85e6d5834e25fa7f2350204f8a0e7" },
		{ "MXRsV6KOECOK-nT80xN0AR1L6O4", "a57b6591d381a869fc4ce49897659b83ae55dca0d5eff72e13aaf92908adc915" },
		{ "MZU_YNjwWV3Ia7D-i0dpSJS55-g", "f148fd6044cf53ddc2e98cf8ff4998a3d5089c5b3cfa8a5d36f258d2a501f5fb" },
		{ "n3dCmYrEVpUIE0bj5UQMwS17rGw", "5d6364e718da4d30dde8dc8b9f60ea01135c46f318769af3885f086609e43aaf" },
		{ "n3xSPYdn6gSX4iyhHzwwSmDnmow", "5baf36630542c44ace1eefe57ee5888a08861e6d012881abf5b5a2ac5aac8f4c" },
		{ "n3_9fFNJ3XuYEUPK_jCLoNlEMCY", "ac98b1631fc4318e16d196089447af5aadcaa204a43ed87e1471e8aba652acc5" },
		{ "n4mINWj2NMVb1BATjZp00xbiWhk", "43b12282d86882cfb5c15cb658f64e8a5b6f6daca0ccd9f2dd6c4731bc8312b4" },
		{ "N6BwA12VC41J9ClnHnqtFzvtTNI", "45c0741a159053c7d1895016f6a11e189bb295d8b0584789df5c9376aeee5d1e" },
		{ "N80ayGXtoUmoiZwuwJQaPJ0ntlo", "dd28448a72bb612839b2ce02c9941f4a5dd1937ab640abac4d5801a8562105f5" },
		{ "NcK0WJJZVDMAcunUarJQiOHHjV0", "2a9dbcf594982c38d503c74d7ac1ec34e3e8fead5317a34bd5e2a9afd4b463d5" },
		{ "ncroxeJZ0XzmFVm0QxFdbDordPo", "6515bb8e7f798a90f1c4768244c8454dccae9daca02f8ccdd5dd53546fdbba07" },
		{ "ndIddC1mGNRozLmJlTzN-apoIPs", "3604ae785146a474f8d8d8fd035b15bfa20df3cd8218bfbd3cdf567efd1aed72" },
		{ "NE8ir1kEdyGiWNTxGzvKCHzXIno", "5d2ff7517b5f2225e420774dfb69636edc5c653a2d8baf658b910b2211af9707" },
		{ "nEDQQ2V1dtrQFzu8isbNWoERDxg", "4ca2312366e566de91c8de018fb3d575a9ec6d6d892737e57543cf363a51b35b" },
		{ "nEo9sFEuqkS3-Kbstpu88A", "d115499ab6d5d988cb7e346c6984b2e2b7c02fc6ce5b9ac38bef5adc40593183" },
		{ "neTug10V0kGnkH7qy20UkA", "8c338d2f45378129aae3e3c9db5f2ad5bcd3d527008709c757a2130ae2ea65df" },
		{ "nhaEazwxe9EHUsc_iH6DHSViMJc", "1ee14ee4c06cd8a6d478f579a75f8ac979968041a3478b2e896489486b358266" },
		{ "NhrhDZEoCuvIHtT7_j03Oj3H3OA", "3e61f49d732116572e437ca0f344ced5e43069ce7e65e89af29185ce0c482596" },
		{ "nIHTy5ELeZVrhCOCOsNm2CbE5pc", "9fd2ccceee2c7f163925bbdcf093a313660239e9c8663f58815033009419ce41" },
		{ "NijofGfbl0ISgyi_MrUX-rdDKzM", "6ffe4bca55a287c373a189a57474edefe0aedf376f2c1aba6280b8d8cbd6880e" },
		{ "njvnfIc9NnNy_OSqOEfFamYQ31o", "367050a51492911e2a1c90b50d862319a69171dfbf0c5467554191bb1d9b575a" },
		{ "NKdGLpKIbizM-ObRKJUaRHvEpRE", "1c95916df4d6e821750aa0d4481dc3befcc7d3a44e2eb304099382426f6a1fd7" },
		{ "NKgCOSybQVTyoAIN__tLEFgWrfU", "d05e6d2c7f390f04c169d370983ef7fe0f7ebb4174adf17a3fd8c3bf066547f8" },
		{ "Nkj8g8mQLEu6_pEzF_u5Vw", "bed1a8f4973bdc405b447ba7ca1a4c2ab7777dac29cdfa030badd09974ca81f2" },
		{ "Nkj8g8mQLEu6_pEzF_u5Vw.meshx", "bed1a8f4973bdc405b447ba7ca1a4c2ab7777dac29cdfa030badd09974ca81f2" },
		{ "NLNr-tqxAFp2jzg1FbZjWVPcZ8c", "595568ddf539ea4ec059e2b33997ee6bdd985670e6735f94ae9f3d0bbae7aaf3" },
		{ "nMFCVKb8anDow4MAbPw4J7xj7iE", "cd7683ccdce26b84c33626e32aec6c6226570474cd6adedf9e77214de67a413c" },
		{ "NmIml8R3VZ6NYoVf5xFgnSd1wbI", "aa0225d580c214943db13023d6a4b43f5b10e5d6021ae419459f61f7dcc9108f" },
		{ "nMW9H0kJoIzmI2Tgn2UJHF4o_Aw", "0e1660507b1b1c872bc9d2f4311ba18d4c6558294ee09719975a460200123064" },
		{ "nn1unPt-RkCAHTit-1dKJw", "c2a8d1d76365ad50ba77b563633ebbfeacfe8da87f47a86f66cf8fcedbac4133" },
		{ "nN7e4mmU-gMkqfunz8ZIig-ycR8", "540a05cba6f0594742fdff76c5e44d2040ba1042c63b8565b39917846cde1636" },
		{ "NnJrF2ynU5gFQ1-dAN2QcejBTGg", "e08354dced2d75c01eb0fc0aa4515a6e61dec3d0a2a5a0cd26e62bb46a13ba5b" },
		{ "no3cDG2JUv3qfQzXz_S_POUhw68", "4222078f91e465aa13b1d411dfc94fbfb636fd4217800b8696f76c2758955055" },
		{ "nodOASXU9COv2dHX_-QHJTefaCQ", "c1cbab7a762b4d0f3f99e6e90388a204769170bb980cdf20f8766abe220e235d" },
		{ "nOrHWJCJLqWOb0v04R8hdtgP7LY", "af56083ebeeeb3fc07b545c548b3a05ef5a510e9baeeae4115ed2e46ac02e69d" },
		{ "NPK6GHWpBml6duWRDlascKjYiYo", "64d913705e1a743f557b4991ef3e381285338bb24833a8edb4cbc63563ec8c29" },
		{ "nPRTyeDfZartw7c-xce89lx1nEA", "8763c4305f0b48c65114185ce60b18f92a458d57150e878da15ac76f7917b5f6" },
		{ "nq0LBiuUtIvkWg6i9yVFe75PzME", "77199101764ba3b880422a901eb651fa9a9352c7db78584ca8e4e77b073e9950" },
		{ "nq8NbBUyrEYeH7lLok98GBMi3Zw", "e2932b09cee10c915e34afcad08833917e660cfc39289bdb1d0d93c19137529a" },
		{ "NqgLx4zfmBz8IecXaMv_er771zw", "0224d175d47c6e844482b93b83decef7b31f05a2cf7d01967050980ed546f0ea" },
		{ "NqUrkpsIuHL3Tf6YQxYC_rm2Mbo", "78c4c64c7dc99c24669080e5342eb4cb0794193f70db0aac176e7c86e101e833" },
		{ "NRrA09VWBiOpCuN3B790vWtUTBI", "2c2bfba0b88edb11e437d3e1568459979df84f99f6c1068428c6badb43850dd5" },
		{ "nrrQNdP4TkmB6kK6lZ8GQg", "9ed69de86b3029fc4ca516e2256b481a4f9f9f74ab285a1932ccdbda2ec41a2c" },
		{ "NSdXnulBipj3IWfI2PX4-y24gBk", "d5b1381872de098745f1128733576c4dbd80ddd912da879db1c438b8d3beaca8" },
		{ "NSY_XsFGFmwknrWq5TYIEHlQKzw", "e0cd10922df112eae0e3790bff5b8b9fd06c9ec48aa1feb9a9b7bae677dcfc0e" },
		{ "nv9L57hrV3Oc2dI79VUZ7R9fuf8", "c3a15c321b7364d2731161f1e2661132b1c5359e04761f31edb244cd5024ee19" },
		{ "nvI4iyAxj68H6QNbodzPOG_1VvU", "1f4aed9c09f3c6ca1639a81815a52b0ff43bcf3d610c2bc4ff08eeb2fa7b8bbe" },
		{ "NVtYhW8BzCbiC560JSZDJqRE_3o", "372df4b6dccae7f16fa2125f9adfa1a5b198f48194167326c74943f4b9712148" },
		{ "Nwn9lKEwmkeVFm3WFUr_3g", "b80854626d6c39358330ecc37048e6df10f15addb955fceec3c7f4344eb39f0d" },
		{ "nxCi4eDOwSDHP-96qJxxzeXQ0Eg", "0bf0480f4f48eaecc0a547f25e65e612657fa0e0f8228cb6fd760139f582ed91" },
		{ "nxKe15OyWKioDUktkBUYxunJIHw", "24ac193dbb522d4e126f4e592a76280e946052fc73af28f5fc442c2cf52a1f1b" },
		{ "nYdDqBr7e7AGPe2R80hZ7HhS_vM", "dedc7502052f6293ca7ee50e5f882248ab7baf4d576bffbcd91710617c2df4f2" },
		{ "NyRNSPjKdS2YchILOMTO6OgpJHM", "98511e890e44a70df1b5c6f39ebea8a563c34149ea74e720b0a1b150b19ab788" },
		{ "nyXWvT2hzkaTa9nzFM0Zqw", "25d8293b543f5369e6765d4d83a7077bfc5f58ef58ff9a20af6438e415876cda" },
		{ "NZfvb3WE5jX-w5epHPH4gPUd8FY", "556dd12942faa495f450616f04760df93c56b8705519ba3f3ad4933f8438513c" },
		{ "N_V5l4BTeueEcpsRz-vRu1P0KMc", "e4b911e9f28eee00d719c95ae433f370d0e8501954511fd76a18d5001fff4d04" },
		{ "O-brgxprXHUCbN7eqx5eJnFYKI8", "e86a06d6f49e0f9e49991634ac7d9e7ccd71e91d3afefeb4293b2d41d597d396" },
		{ "O-ELJajsVA-BNKl8mS1fgsG1cgY", "cc975112c8a975cb4ebf690861b95e5aa684a5f89632c6ef7b306bd997620fc8" },
		{ "o-gVt0Ms0mlrgzqwY1MJv_Jlh9U", "2bd891df234e42444211251d1958fe003378d032131d2b69a75c4e424040c00b" },
		{ "O-PqdmzoFItdwsZX-HQgwmWVenE", "be6861306ad77394057bbe3b1ea02627a17ad4ceae3881aa38028f092a59ac4d" },
		{ "o-tHYwgYYHo9ubfa1DJ1sNJB3BQ", "70869ab5422129a013396bce60822e8586ddc780e84f914530ee604e15c1f5ce" },
		{ "O04DjOt6HvxPhhOP9NIZWF1ldlA", "be9ba280efd17890626a70d51be62c05b11a16eba4cb95d66944fa2a485f3252" },
		{ "o0EvFYUv-o0qW_mBDu3rV9v1BNk", "4711cd803289340fdac3765cdcd656d011c14653ec6de2a230df1958180fc6d4" },
		{ "o0K9obp6sGuj4puuE3mIrTUq6Co", "42242f91e6747e8f3b4fa0b5ff2ee515e50f1d1ee3c949c6622380388461cbc2" },
		{ "o1C3-xlV9o_MCRZZpopPO42EK1g", "f9878baa4e2e12c87374767d234ee398fe66e22705a2518888f7b887a69fa391" },
		{ "o2BIYf6phMYeT1J71p1LX4ewDy0", "4a04d7bc56cd806e68848b9fb65967894735f0114d66293d0991724b4322fb60" },
		{ "o3ZEo8-EZt8hyF8WLxd1GX7Jruw", "5d4558b684b010828dd8c016695b70c964cf8d61705795dbdf334168ba0fcf74" },
		{ "o5kHVrCmC_huwRIzkC4SRhWRfho", "5f9fc0ce792599d34ac92d38ea98518d7c02ba4326e1573ac861ab107d8ed97d" },
		{ "o5Kp6bJ_Q3aQTP6H--9Wv4gqAtA", "be2abe77ecf560cb4fedae463114ec5129dc195f0f0105d5f645bb7c970eb7d2" },
		{ "o5q1wbV-21mgV9SvvX0I9zJhf7Y", "6ffaca75529b56da8ed882a8d08bb8d0e80b2bae789542802b0c74ff2606bda6" },
		{ "o5tFV3OS2ECUIRylKI08WQ", "52d654ff785a24bfca99e96e307537f911539dfcfd3a9dc5ed56e2167d72bf0a" },
		{ "o6-gLFievdI-LRVfUZtfrKWO9Mg", "f63971b388fde3f9fe49ce827af06118639fa0a4fe1a2487a7ccf3b1253c3e5b" },
		{ "o6wjbwAb4UqtWI8-eU3osA", "0c787f7cd954bef0e65ddc02f6c0e18a295c2b93e918daf87c95196912e6f69a" },
		{ "O7fx6yoZx0IRbBTb-Pl_5uLd46A", "8e0fe550b7cb99c66014b99069192343faa46f627bb79a2cb877cd8c0ac9627d" },
		{ "O9hr-CEzpfQA7MwfG2760Y8NynQ", "dc72437880b564bdd6c47a26632ec6641c13f2fb66ebd54338a38d385768be0d" },
		{ "O9xjWjOAL1aC3cGpJ8oyn99EV54", "c34ef92665c065085d5cc560026456761d311daafa43013c28a28dbe16cb2569" },
		{ "OA2fXW2LGn6HAhBD38CVADC2fzQ", "28c69b5408bb60160e5a1d51b1cd7e2fc38eda6b5b7d1d1bdf544a3579fc7cbc" },
		{ "ob3Rh8xxhMDpIWTkLDoeueJHRW0", "0de632e7652f68849f66f5db37c9c735302fb527724b9b593828673ba876d7bd" },
		{ "OCWm4pAdOu7zQeYoj9qLIG_BsQw", "4a4892aaa425621fff27f287956140843079280e5978ba0943b0f6f0d1284bbb" },
		{ "odHD8MoUNHmEvYDxhMe9EMQMfes", "3808cbf1e7e2a26c5a8434cc5d02b2941e45bef5f4e80125aeb27da8dcf27368" },
		{ "oDxFC9fKh-R5MCr486GwIWcYHCg", "5eb71f2e362c754aab7c846d4b2ddfbf9f5c7323bdf7c32d2a71a43e4a932a2c" },
		{ "OE4RpoWX25FrS9kxTH9F7jUOaQU", "fdd27431b8a08d75f658bd9d100b54364d766a2a707ed6f6b26164956521c077" },
		{ "oeMynlcix7FLtUnxibwuyT7qbHY", "a4f0e33cc676c0b2daecfba31618f1b3946c44f160643d98d70e216b14202b44" },
		{ "OEnIAMnepyunC1jSZchflxmfemI", "1221ed303847b8bf0146c36df9c68779e4c1123d140953e3c4183b724e7def7b" },
		{ "oesJd3wZf1Szp-gQB57syWHuI8E", "b33cd39aea2a3f31e790e49398b84611bc4377f78621c89f7082e66dbdd26647" },
		{ "ofP4WQmUaWAEW_t0xNtKNREBdpU", "b0ce5aca4773dc81f6d04d65fd278d5b5ef84c189d62eacde9758516e514b2e7" },
		{ "Og1FNO9PPE1LyNcd-kp8eUZaJPg", "8e8f3d742138d5539d8fae2b4790d880e513708922edb4f02452e720224cbaa0" },
		{ "OGwrZdQAQjwHz_IkH73K3jOXeuY", "53c49a79ba91fa629df2fa600626b1f743205559ad85bccd35b429fcf8d1cfba" },
		{ "ojevgZLJSprusZMvO6OdWXApVIk", "06fa94a89e9e249d5e4046638aa836fd9468467b2cf744798b4c0ef59aa20641" },
		{ "OKjnP5AFHbLq9lCvfHlw22tWtIw", "8a200b2c753fbe1384b19474989c58588cde33f63894c29486d86e3fcfa34c07" },
		{ "olOKBNViusvRs9m687PF_-3qJnw", "c54c3963ee23999e4e57bcc2021a394637c6d3243b23a873f9ae9271e10c7255" },
		{ "OLqaPI8a3fbHIHng1_Y0Pni8pVc", "8613871bf79f299512bd2a040857da959ef7bee205b100c542679ede70281942" },
		{ "ols9wMhcY49YKSYYrCgiwVRLHFI", "5011d0783d000e9b3f4792b8389d0d8d0af2182d6360440b3a53907744add164" },
		{ "oML-my4ncobUH2F7KpKyoXmnego", "8017d37d53b031cda67983f866394866d27c06bff3a2999556aebb389075e728" },
		{ "OnhMEAlbYvnf-D3qEjUgFUsTedU", "7286c0bf16d6c40fc7d433f7522bf8e26bc8f3d203f4b3e54a2f94a452c1622d" },
		{ "oo-CngwdQEmgCQkEDNhJaw", "da99e4efa780773171bbcb0d38814d9eae364d471df6039e3ff5e15edd1cd841" },
		{ "oOg6ONHdW_Qkb1ABTkNoPNoRJlo", "691da9687c8f8f8bf79223712cfdd5719f8d81ee3f911efe28a62584ad06134a" },
		{ "OonwH9ZtLx8oHI3z_52lFkYm5ts", "cde6650c4f1977482f9aa2677020aef47d19823ceb186a53221324eec29783ca" },
		{ "ootea5Y3ASewacexl66DJ2nvNjE", "7bb9ef2985761de6500aa8bc7f45d8c61e8104dbf1643500adfa7a9d1434f07c" },
		{ "oP-XezLOpsMTn5TVBO-3cHU9MKs", "24a0e24b20ccae2fee98429ef55aac2f4202c9581af38135c47932761ba5c453" },
		{ "oP3MyMQ3G7EvuLMZ_R-BAMlxzro", "981eb383caccddc64b66856e637e97b21070c539525b4085f2c69c5b9f7702a0" },
		{ "OPyibvgV_Dql3l5SYStzMna196k", "9eb96b4d8facd64f7b3f3764c30f9e5061c809356826007b22393a94a6f7eb94" },
		{ "oqaj1KdkibPigKp-YL8etn_4y2s", "9bb6696f692e32986b8af10cd29460160ad120ea65ad9c28dba604e7d6205c9a" },
		{ "OquaIoPgWUAmihkxt9EbnxO4kGg", "3e42d993a7f816f7c95cc8696aa880f61c43c18b31d6414a25d7fb3c706f4544" },
		{ "OrwqYlUPKUWak_SjqCltSp6I5q0", "954616dcd9436113b57156626168cfd972dbb45e33795e21b2bbdbd7e7ab9659" },
		{ "Os4giiGMoWZ7HD9qLhJNpyLS3Yg", "7c892faaee459857cd2244e2703b8e9d264ae9bd8aa1bf4b3d5afefc5c1071a3" },
		{ "os6l2RikvJ7vJaoEBGvPbzrm1Vs", "d7505ef3146c4a4d8a409fe3129864f68120ca681c64650a14950de20b1c8800" },
		{ "OSCQujU2-X59c2tmNj8m3AqS3SE", "2a75aa829fdce305ef38efe16d6f06453ba5f0e648d800043587fe5a63f93757" },
		{ "OtkM3HMpuCX9_CtKEdS05F2uYag", "0c27aeb5494b1834f361098f80b333e9bf967fc27e3d5b2fd2fad7415fdee382" },
		{ "otV0CBdKQIuThrvBXFVfpYNtnm8", "53eb8305e9eb330aed00b896920e02e756a7f9af5e93cdc008dae5499f5e897a" },
		{ "ouF8TiSidxI0cQrSo4hp7IrH0k4", "560b4ad75a4d2e2ce9de5c408f08106cf04aa0c397564525f3e152c31f31ca30" },
		{ "OVkBeUY2sH80kJkCOp1Wvsw8aVI", "eb9dc43144281f7fd891e8a32280660e1946b8e7d4c8ce2860d8139a571e2e20" },
		{ "OVpVYfmm1OdL7d0ECeDuP21WNjE", "de362667da2a097190696ce87b305edb920a8caabc87363c2edb891bb7173633" },
		{ "OVwHIRygby-3RoSLnTerin6X4IA", "4fd82c73f3829368330c2cc362ca9f480ea1129fd82219594699d50698041391" },
		{ "oWjgyoxrYJ6TxGOfoACBRMtNVOM", "ea7e8532690ad850bd9b55f29eac6933c7d0104010f9a13c650267a57e49c803" },
		{ "oWkzTAoWQbvHUvBGzae66CtZHyY", "c728a2d2242aedb85cf2a3538af1bda18e26fad996441fc72cbcc4793033f66e" },
		{ "OWUDaWIVukSm1X2Kc2KOaA", "4422085727f40871e7513542ed38b966e115a55d380746d53e628f1c2f28626b" },
		{ "OZCzPC3BQMCL_v8GycC5Mlbl76c", "a3953afc591df480eef705585a0b9b80b8e60163f0f35c4e0adf04f4112359c8" },
		{ "P-3y_dytkZHSlimdmC3I6GWGZr0", "38c168f1cec299576700d2524b4ee6a025af0c468a52d40a6a2dc6fce0238596" },
		{ "p-q6T7b5_DLD_aYcLOj5TmsPKsI", "10c9f9c58e827eface8e570764e82f4cd2b2beee4b25152b2cff6cadc0b04ed7" },
		{ "P0ea7chREdDwiserPbFnktuHzWY", "cdd0dfafe6e7893982c062a46140b570e84c0deeef47c2772f8dfc3ad18aa9ea" },
		{ "P3RV9RRFKdpWRPSDLxIZJ7jmdas", "ab45c55ea27b8cce7b83476cbf77cd272d0a8a4f6cc1e987768c2e133873b887" },
		{ "P3sZpYObyHjfTxOsmANLMB70R2U", "760bed62b3d48bec9dfecfdb3d71d354743ec58bf7adcde2f5dc8e440e5955a4" },
		{ "p7-wvNTU_oCC0vcHJy2xfQMgPXk", "cb70832b202af77742169ea423f68899aae33afa3972f224f5059174d674e850" },
		{ "P75AE-HqJjQWSP_9Ns42yaTZYb4", "326712afad57c608954b9850908032777848931d18bb6ffb71de8aa35cca92d9" },
		{ "p7dLH_MnVEZsCmCxl_3p_IQYifM", "3aab616c6c31ea8c896166d63718999892c90276a7a987aa6bc847894218400b" },
		{ "P7oYPeNhPL2qtkoeWqHfpqcv09M", "96778872f633930f3472d7cd9f6bc8d6909900b66775fa0bc911b5d9c2bea252" },
		{ "P93Mk_Os5U-avxbEZYcKv9vKjbg", "0241fea0e61bb60a6631910cebbfcc9b6c93a43daf2b5c1290c1062468bc8e1b" },
		{ "P9iRqOyIiD7wyBxNFQgfk3Sia5s", "eaa9ba7528e61c73decaf1b9a63ab4768c6865aab1449af582933b051439fa1c" },
		{ "P9TzAM2FtgGwckkSYVqVwzPdgPU", "9df8cf180068dda99a0ac562a4a6c445d4f5ce48e5774df5bddbb6a7166714c9" },
		{ "paCT_S6HHEK44ut1fz3I8A", "1b91ba39da7be45d2887fca037f3a8d24e63d2c62853115d21af30959fd1432a" },
		{ "PAq3uDwKFe5czyQjfy4_5Zx5e6w", "e64c69774ace19d68cc0ff782f4d47f80353f96ac1d5cb71500a239b7b78f034" },
		{ "pbBhVchlw0qU9Cv5dwmtcw", "5ba04ca612ce0922a34a98dbe945f315c5edee602922ebc9967936129f641a7b" },
		{ "PbDjNChi1Ey0tWNd8AhCDA", "cf29b7e528443fb84a4765eb4dd99a09393b04a99ccbb648cbf42ee42da6e11f" },
		{ "PcVbX1rR6agvLFeZPiToTp_Q6jc", "23d9c83c3dfe1bd84621b7c84322b0dac9cfdc3b04eb4276ec8467cd7fddf93a" },
		{ "pDHgcWdD9qxe83UJuCVssaEHGEk", "29868e62095fb6f39ec704220dc054c97f51c612ee63071bdd5e17acbf9e40a7" },
		{ "pdykz_SKzYAUv0p33yoXxHSR5Ps", "c5cd47952145d7c85b6c4107a65cffcaccd20f7e7705598775867de40fbbdb7c" },
		{ "PEczoZlEs2Cnt2x3inoOVQLy8UE", "afe411b5b3218e630e658d13558c86f4b09b0937e6ec0cefb701fceac2aaa070" },
		{ "pfDuJ85s0fy3IuhxQMCvE_mK0tg", "b24a7cdaa6815166f1c98a80e594740823f198e379d49b8c2bbb4c4b5baa857f" },
		{ "Pg8Rrpu-o0WSYrJK-kuDng", "633a415989eb3891df2b20d3dc841650759c09497ea9f16b0f8d7056c72d45da" },
		{ "pgtGkZe-1iHi8wpbXEr1dV1Wh8c", "9ec754f957e80d7d2dbe0fe5e5610248ab058df5018a225b357602a259f4c292" },
		{ "Ph2nJkrnSJnVE3shyNiZj8fPKOM", "a660d1955dbe702369fdcb6ad831d0f1e9f2825c4d7250a0a322e512c7e61e8a" },
		{ "piabEne5vXM3iYk0Br1oT7ON3WU", "dc748a1366ad12cf6bbe9746f7ba294edb376dea42d976d484932e53db054caa" },
		{ "PkAhaY-X_jFiYfa9Lkpbx4KBg6o", "5b42ad6681716f3d37cff625307c7e7a57d9b1dd220fd9a88dae9d603e505b30" },
		{ "PkyWVdPR3kmCcXVQg2Mw4Q", "bbd478d9a032621e25d353b49fbebf71e280bee378425bd33b3f8f58fe271b59" },
		{ "PmII09RfuONknJ-bss689RzvZ6c", "dc65029ac61d96fd7a8f0d74c1ebccb23bfe4b5aa8ba1df9ad98cde87a4b64ea" },
		{ "poTTGdO4cfHxyrKOU0gEGlk2Ces", "bcb94211f38c86143ad2b904735f09b804c9e0df3f3b378edb23a5432a125dab" },
		{ "pPD3To259BkIVRV61W57jqmDPxY", "74b287b3357e8c753da8b1a414894522d50a472716c0f671c4dcc39da6dbd847" },
		{ "prCFpZl7EeiAKKcJzLzZrNjZL2s", "7d30580656a9c3f1c09f444f46a4ebaad4fbf5b7e31f3e9bbf4266463305b450" },
		{ "prRh4EcQ70me4sdUH_1wOA", "ea6ae25d7498b6663befdf02501f89f6d3ccefcb293d3f02b2ff1e0e68ca4479" },
		{ "prRx2IAwouJGpNxGL4k0S2j_1Jc", "0daa3f003d3ea522b10449b2512a31c7d05a4c4aa622246340e6a6481e14ac4a" },
		{ "PRSJnIzv5JpasmR-2rjjCoLrI_c", "894e47ea46f43b9f64039adb18ab6e2df24718e9278d562a8acd930b29247972" },
		{ "PrSz09IzpUCIlvhLH642DA", "21136be0d8988874a76c8c7c97ad68e434b7cd62fd574012f25406e9e47f67a0" },
		{ "PTFwkv5iEU-dQ2j0VZlRWw", "d176b1c2c5c779d82abc4a523efa75a7127fbfeaf39fa3bbe22e25cd579348a9" },
		{ "PTFwkv5iEU-dQ2j0VZlRWw.meshx", "d176b1c2c5c779d82abc4a523efa75a7127fbfeaf39fa3bbe22e25cd579348a9" },
		{ "pU8pBRLyJ1Er5aPjzvog0RAjUMw", "7bcfab1581c74527c0dea5de8131a4c6704e97697c22c24f3d32b21348586e64" },
		{ "puVJlHLNHO4NaO1ngZ2DzDoVNNY", "3d5a951f4133f2e51a689c8401a242a4c7541482828dec0b15dbc5c7c161457c" },
		{ "PvaNhpVHYmxOpjM32JzcfCGvkOI", "acf0b5c2f4c9a5f076c3e102c7569622c80035fa26dcbe610b19fb2039f93abf" },
		{ "PWHZGvtLeHC-as0heHyiN9Eprxg", "52ba49622388028fbb1b9096c6fe3e3fde3c053e4106b0b9b2757e18392fd038" },
		{ "PwtfwJI1Y0iyBlCTWlM6Fg", "dd61968bbe484769141749b449555ad6008391a7eb5de1993228024f816efd41" },
		{ "px9upEK2bEqg74ccqZTdeQ", "838e40641010db435412a884a2ff7c27ecad57e9df5ce577eb9c73cb81c20357" },
		{ "PYAOLMUTaj7E2UwW3Njy4_AoTUY", "fd511be098f99b3fb47ee4838139248e804e6c460304aa2c2e926b3aa3436f75" },
		{ "pZLOsR-NBkaQK2FYzu9kkw", "bdddefd10f407ea626ed62bb951e6099dd20080db47a65dfe0d0255812ed0893" },
		{ "P_0Z5AcqDVrX-ir5fxuAfRxpL4c", "e508ec17e37e9df123fb5f9cf2f92af502bec490abdb9dde64ba58a592edb02d" },
		{ "q05stx9UZIPraatiAJiKj9msm9U", "398a47024a4d72443d5549613f382e00affcaa497e40afb061c3adc3657621e6" },
		{ "q2jsDsHm_eWfZD0l3YRvaxWxAl4", "96abe8b4e33b3e3ff30f4e74dae5ccc2e93df784b8943d62eb6e541bee65bee7" },
		{ "Q3T2PV7kLv1uwHT_ba4UjbeCkUo", "6262dd75a568b89afea33ed8dd21170aed0aa585973ac4263d728dde7b7f7791" },
		{ "q4rnZGYs1FjBUyjAxz8zK4o-7gs", "416b4079cc7eabaf7a1d08d581b84185b7a055167d10f7d60abc8bfd58c7be8d" },
		{ "q53Rm9N1POZxkfA_GjOTJTD_YIE", "595324a18f333ad0398a888b7425f11b6071ebd323ec8ff4a6bb66fda0f0457f" },
		{ "q7jhU1uzRMIHPFfm9VboZLAN8Qk", "6350495dd0aeb543b20460a9fd57c0cd66056e82fff8e2b2896fc4f618262677" },
		{ "Q7oOaUX1mk-L8ToVv3Fejw", "1a4726cdbb8332b91efef119c67a05391f4a0ded3cd86034688077ea497cdad9" },
		{ "q83JDEKuTkeEVkPo9bkDng", "1b07910b179d297bfd50ac3b644d0089a72b9eabda7fa44b08841a3964f2d3f8" },
		{ "q83JDEKuTkeEVkPo9bkDng.meshx", "1b07910b179d297bfd50ac3b644d0089a72b9eabda7fa44b08841a3964f2d3f8" },
		{ "Q8KABUaQZKkWbxTjbVczsQeHXtk", "b8e39216bb04dc121ab7677112058bdb23e6c057de217dd99788587149513118" },
		{ "Q9akRCtf6vQ7QuZ8EB7lVVh8X64", "19b391ed0c4669a94ac9225bc1420f0ec1edf6ff4377ba256b12b73dbea562d9" },
		{ "qBm-Jes2b9mm0SPKtShrhWPhdIM", "fb041eb29eeb78d6e0bc8a26357dbf87ed2f90750c62521f8ed8894082585306" },
		{ "QBUo1oAYiPr_zIsu-gNgbHtEneE", "60acbbd4b0302002a4f56f3acddd071fc159a668dd6625590d8ae35049a9acd9" },
		{ "QbZ77vS9wChhiR4PnKQobv-SH7w", "53709d391302feb847365a0b0f17fcf348318644cf89dac763ec7fa5a17a2b08" },
		{ "QC3BuXdzHLukK7YAANJURXQCOQg", "566f67eb97b94517130c117f8ae73e0afa9e319a01f755e55d67a800c686eea4" },
		{ "QD8tuqcJ0wJcmXImCI26aA0JTyI", "622d1bb3bf9ad7a53385fc392c7cdc3bedca70d6aba250a9f996869308f537ea" },
		{ "Qdv7XXQVTi9PGPzbELESu_hjmuU", "550727522d77d14722e16bf5e73cbac3420ea7f9e94e3a365216179db9778e28" },
		{ "QdygdItc0-gxWD_IQ0o4-WnFh-U", "5547e621053f617753fd7e157b7c920024ea675ad9cfff56bc2246e5ad49d118" },
		{ "qEEp0_q88jNFKrlS7xvfwRyjth4", "f0b638272b622083e9a9fc96345325d1c67817d98082f4505917931ddf12590c" },
		{ "Qeu3ymCgIdq2D4HDr6QVMbMX-LY", "dc043bd6c06958ff5230cdd6ee24e6552a91608a92ccabc4c0441d2ec1d56d7f" },
		{ "qFGmIYCR8OCVzOQToonndhEBNBw", "5840b5670042bee04440cb5ea7f61108840e3c7a9758108b19ca3518930d1524" },
		{ "QforJsC86s2fs3aNep_uiEvcEYo", "9e47322006798869106179100480dc0628a633e94c86093f9c5c23753ec6a135" },
		{ "QFYWdBRVyvfp7wMwhX6KLE7Uuis", "c52cac655f7ef38094496474fccae6b3ac73d1ffa77db79764be9e0b4cab8935" },
		{ "Qh2xDrkzXtUantO7rSkCkcmGURg", "dbdca224f07eaf3e10a1a0a176b2ef8b501863307547db2bb3f46a76a1bfb8e0" },
		{ "Qj-vmuNMbhyzSp5QffVadqbtGE4", "0629356100a28e4858afdafb51c2001e9635f6486c96ad6f4abd6923bedca49c" },
		{ "qJs3a0XHbE7da-ktW59Fc9NYnl4", "102ebfc3ccfb38358e9fe52689789be89bd3546f282f4914a62d42b47583449a" },
		{ "QJtY4APEdnHX4URkyu5JVvMMbnI", "20774758bd3f829e0efb5abfb78d3ae82cb43fbc7f2437f3894a4c687f70af64" },
		{ "qKAwQw_7wtSR9InnBLYfbPewnBE", "bab02a7f411f6800fbfbf9cd95fad3695629506e7ac44e5660ffe5bae4be2b7e" },
		{ "qlK-xWnohVluY06S9geJLHPJeMw", "5cdbc82ff11bcedbfa5097faa1b634dc13df223d4658eb0bf4922be61ae60257" },
		{ "QLt4BngLCicg9d8LBCy1feBDzy4", "e4149c143695c12ce50fab36ff65c46457057ac41d8871c10a3e8e5848e2a21d" },
		{ "QmqraCmCO5gO_JximLx9nL6MbCE", "137f7f41e1a12d4a4152a1bcc02904bdf5902c1c2be295525861a4186bdef975" },
		{ "qniCHBR5YrnTfgS16fd2-ZQfbSY", "15fa473e25fd9564f93988562e15b05deae6b4d00b8e2a2e149085fc08e98218" },
		{ "qnJFDFCBM3y2H5kfqc15h2gB2cI", "cccf8ff41715ab7683a3bdc45b133a4fa3d36c101a43d7c5eb16458d567f73b4" },
		{ "qNqkjVKBVN5kz6ESlAJq3QPszeQ", "dd9ae214bed9f14f6194b31141edb864bfeaffdfa5038a8ab4d1ad3bfda22996" },
		{ "QpqTELpp6U-Heee3ebUOlpV9T5A", "2284483ff602168363eb122c0ef50999e4127db7e81b8069730f8f2dc0635153" },
		{ "qrdfW3YeyEimVV0KtsJ7ck8uamE", "d61e0c544ee30798072482b06327099275e60782ec2d82484031feeaffd72c43" },
		{ "QRm5kOVs0_JmSd42g2TiUAC7lEI", "c7027c8526645517971052dc51bf5864e240c7873ddf2710e73cf692cd98ac8b" },
		{ "QRsARqqs9EGiSVpi8Xc5nA", "88b06495482fd00b9bc634fd054179610763365359afbfbffa46d2c833fbc231" },
		{ "Qsg6NtcLub7-EqSMWACwePQ-aNk", "fd14d082ff3e295e125756af009edb908286355779ee0175a46d7a7a3dd2c5fd" },
		{ "qTVqXH1lvq-8G75p62t4lK5NTig", "e5d09047332aa86bdb64bb4345a21403b4ce8a4aa3f7caeb10875cf8d4c6fada" },
		{ "qUU_wg8qwV4OrAVkpz6M_UxAs1k", "027cf5169eab7d7f2fe93af746fa87b7abe255d220e2a60e71b02642fb09fd60" },
		{ "qVgvqGFu3EDMZHCVak3k-ogD9yI", "127e2ebd046ae57c8d0bf9747130cca81552d8af14cd065950ce3e36e6a7cffb" },
		{ "qVKV9Dj10BPFTCqr9ZlXXfmCsag", "99db3595f0f7b06323d392d65884da1c802cfe847b91c5c5f8cf1ad6662a656a" },
		{ "QXmQooJcJno2x-TG7mPZgrumxos", "93518963e40e060d5dbd1232f075f66150df88e83a44fbe79c1e60aef12481f8" },
		{ "Qyg4ooLXRb6q0WIjAaD1gRUVkeU", "7863c6930d31cb08a25e9559d0d3622c15afda672bd67c53e6b8fd1a4a5f5360" },
		{ "QzIc31Iu-MEJinxs0Zwdn3y9enw", "069567613745fe0c7d1367354c459fd351ed3d8c93777398b175ee12dccc5ff5" },
		{ "QZZDvWgciuzNcphM7CNF_8KGq78", "c3164054f09f5efc50817a93ce9c72b7234a54c30d8a291a71fd1ea3b6a8163b" },
		{ "r16moQpimCPYP4rRG-heaouvx6M", "d7197eaf35ecbc65e7cf7df1664b8dab463be77e146a1b6601e256ef78054669" },
		{ "r1qR9mT0ewJecw78AZMxJOkY_e4", "532d1d40181ae4dd00841bf9ae99600417a64c8d21d84c354207c92b13d279bf" },
		{ "R2zKsNNqN04gZ9UaTueGAJZ9d7Q", "2beac6f8abb70b116827e53f6e5b6a4cd78b0165c8c12d2046872670dba7225b" },
		{ "R3FCOgscpop10LNXDDzg2HacEVk", "9525fd7de1496f7f9acfdacbb14dc2b170b3e3ac62d3191aa57eff468ba1c6ec" },
		{ "R4Qtc-zpfZKdjo98F56JsoANeGY", "4abca3de8260ce42ae99357db207d0472ac1d12fd8229b445918fe82af9d1ba6" },
		{ "r4zPhDLOCSMNwIEMWXRhuFyG3zg", "957b86edcd6d4392474bd860945e74a092695fde2b17f9edc70d838a8ef6a08e" },
		{ "r7o0GfiehugWD1MSdfQvOPnZbKo", "9de51be6d093b5cbb4a1f7f49ed7a8a510eeaa783a6f6b9c1a6996597d521d23" },
		{ "r8qWJNTmYE2yWHkboTqtTQ", "c940c57385f12a9969e46c3e1556945782c1faa19b15754a9f0d190582dc9753" },
		{ "r8XjQh4_xzgw7H9hg20fI-iDgTk", "adb3b7072ac2b1ee839001b5d3604d9b0f6149752db12cf1a193890d246fa7a9" },
		{ "RA8qN2IS8BXge4PtAHeV3XszkdQ", "2ff860112ac9a7653dc913741416cbd2f08f0beec51c98b8e5316e6f1eca000e" },
		{ "RAG6xnh9onmP1L8E0eNYe5J0nYM", "7d38174ef2f091e6a080c37bc3aa9301c204e2f10fe9bdf9164adb207f6f3fa8" },
		{ "raXA5KjteISJTgDg6jSPQ0419Ro", "de47c36c9e29321f1f14d548afec0daf9ce6ebcd6dcc86ff1dc1da9840a027e8" },
		{ "RB-MgX4xzKlVZPe4_Icf64WmDYk", "1e5f650567d5a496e5b154366a13b9fc557b0679fa0cb869d2c78dfeb8edaf56" },
		{ "rBQ2mjZ6SuF1KCOGYB6708IQSng", "8b03f74a5fc25cb0b4a0db19c9f6d79088a82b7fa840fba613cac0981387cab7" },
		{ "rCJY1Ijz7HpeMBpsNHiroI1V8bY", "2ad1f2a9567a36dbcb901b39cdee919cf021f5e6c5d9165b354a6542867e7789" },
		{ "RCK2VJ55rJuB6wwsXxkn2Gj09CY", "73040495bce55e61571fd6f803db5461122acd53fd73d89603b3332e66e8c700" },
		{ "RCzqP29EF-eTeM2t6sQuDmI1BjU", "2d03ba73874f33b4d64df1fb82306d27d8808274db2bde63602b60ec6a9a339e" },
		{ "rDx8UDonT9ARNnedbPhJ00XEPA8", "38c108540bf8a0d2d3a017e84c335533e9c2a56865f421bca3ebcca28f2098df" },
		{ "re05s29cBosp_VFyPz1Vo3ybTcQ", "f67a269820d4bad2e891ecac94d276710a3edaaad4eaf3931bde4beeb66cfc3a" },
		{ "reT7eW6slEsdgLR3kW1avtoNCxk", "09666592f1c014ca58928e2060eb0f5ba5cfdc91a0968dfa255050fea1c8a898" },
		{ "RfYLx82UKmgm01Cu8wQDT3UF8RY", "e4ab85355afa919d753c27c843039551b4720bbdba385360c902e74486b12d7e" },
		{ "rg8aX3eB6kW0tMDf9gVXsg", "e229ebc0f139a12a475e399677f8e6688eee616588bd674f793673ec47b0432b" },
		{ "ri-21H0ZEkq0wJ4YJ1wLUg", "7817190ae0c0f957270b4e9c7cfb625ad3a057712d8d318db49f6e3886656aa4" },
		{ "RI5oGMLuJ0wB2aA5O1gMqDTLKmU", "7dbe60436bd4c67096c442f8c9d61340ff401ae0f7971b8773f8e357d059423e" },
		{ "rIqk01-u30GVuSU8eG0kwQ", "4953fc008b9387b0c02aa95d84dfeada4041e95d89c209d23c72efc6d0a65b5f" },
		{ "RKAv0J3-Eqe2eOjrcFAAa43f1Mo", "fc4292c8cfc59cc27a5369263f6be5cc28b2ed760f5748ef044bf74a6af1d4a7" },
		{ "rlNC8m3RciS4-FrdkuYWdKGNFBU", "900f419333b59da51a1f7205d3aa2e853dda351b7c17a7176498083a8339dc2e" },
		{ "rm382jQE_k2IncrYGvIQHg", "a7529906c6d71d8a5906896058a0bceda9b32d94c0979e8f86e379268a47bba5" },
		{ "rMdEAny94kWTTPxS8-G22A", "2b81ca7f1edfcee8ed8c51cb38d79154ccae29c74d667f91f50744f4bd0295e7" },
		{ "rMdEAny94kWTTPxS8-G22A.meshx", "2b81ca7f1edfcee8ed8c51cb38d79154ccae29c74d667f91f50744f4bd0295e7" },
		{ "RnlnP-oEXcg35X_kEzHxhorO6PA", "0dc8d08ae16900146ade3e313a28d5bdb9d2015d0938388ed9a1f692846ce6e3" },
		{ "rnYekdAdy9UMqqWVBkuB3YN5i8o", "3caac43aaaf888f81771b3ad0a581ff0ff1c2abb4e8daf1a8570531ab9767ad0" },
		{ "RobCmYPWcA8nRGX5LGD5ZKV55e0", "1deb3286c2ec7f50ac527f39ab56d5a26dc6cd4ba80fbeff5a02ca32629fbfa6" },
		{ "ROzxjNnl0fbTNKvo5jCp3Z1Nz00", "1fa17907d3aed312c989afe446ae21584b479ce7a1d6e7395d564eeb7b9bec7f" },
		{ "rq8-73ISzbSEXqxfNTUvmgmUC9s", "11aad853470078dcfaefcab49d9303c91ce4071e39dfd3b0026cc1bc9c2035d8" },
		{ "rRBwAEbPdCNh2avf5kxq52nvgrI", "fd0d801615cc4580d209b50a7f0a1f3e303250fe7addeeb753256930e7d2bbd7" },
		{ "RRuBkaQv4Gx5lVT1C74ciobHuRE", "d9b81ab61a9fcff1e06acc3410ed462fc58f341738dd6b3f686802b09d9021f0" },
		{ "rslF0gzR_SXFKQLWOWas4H2BJVM", "d768cad34d05257c090122f906ed97692b9db3a84a33897680a545b627c5a879" },
		{ "Rszx52AqOwO1NUfZ8TPdenfqRNo", "c06e7146115adc189da4b3f2471fa762d5aa4fd20dceeadfb6d802ebdcecb1b4" },
		{ "rs_DxxYSI2YgsuqTfMaW86Siyew", "2cc6f4a422cbf6bb371a25be6f333882b2dcec9fa69bfdd79f44ed7414bdb178" },
		{ "RTx0Wz66M5weOcyuAIkscZ5JOzY", "7d2e2235699756893b08d184e67015103968e20ab2c19802f05084b41a64f6df" },
		{ "RVhctcOhykiM8CMUWhCtHA", "4d52696d17bf01feaecdaebf50a1d65e0f3ecedbb50b42bca5b53aca3fc691a5" },
		{ "RWoaK_sNd6MhxTuqGDYpWXD0Z3M", "cbf709f653c92749387651c1ee6b441a8096f91ef0655091238c60d0085c81d5" },
		{ "rx4avMSyU7l0ssvFCUtBfHCvsSY", "ffb9549983e08965f51d4258b8b82ce4b88f94d558c24c637416bd3b115da96c" },
		{ "rXhFdwXEyYeV4KwcsIz53VOgPYU", "b64eb4a3fa354aeba1c71d7922fd1491afe5298ffb83e9ef15d61f12b711b355" },
		{ "rxTk_Mk-Co0cr6A6qEKkI_LGZMk", "9d377c5de081c750a9d8c59e496176fd166d4ea717d8ab610ddcf1ca1c8dd14b" },
		{ "rYLX9n-Z4m21CoMisYEzYdw-5uo", "a078eba4fa7d0bdd5f0658eae82d1c72a1e7829eb849f25b28fbcc1fa52d28ce" },
		{ "RYvuwzblgBZphzlYFE5v8qi_lnU", "c0bb9f0d4be74e02b5c5c5f2a63411488c379b7ea17c5c6834e355edf8a3bf11" },
		{ "RZ4sRvhmOPPd8DZrRCmYUB1iMZw", "47e5b73a18b8de19762c5379cb43b6bfebd3667058e6a1b77874e1c03d947cec" },
		{ "Rz9dg6_Gzu2k_yXLD7yd4JCEiUc", "725f6bc91625a13eadd10911777fcf30011b2d20f6159d41ec2889670862dc62" },
		{ "RzWjDk2SIsz2HHnsoVEpg2P60eY", "7f0c6cfe23eaf2b7214aba31639f8ad9778f1f33dcbea19378492684f3dfdf24" },
		{ "R_JrND97lUL7FpVkIcOZpdnL9Ak", "e7a864c7042321caea9e7882d8ddafc0a892a4f200780a9b505925d47b85e6c3" },
		{ "S0qk679oY8uv7sp5iCwlZJWvrYU", "b170abc8a7dd3fc99a83b3270f13982b2113e12419ffd821ae8a7c9ca1b7bb75" },
		{ "S1B0thPKTgEWvjyneIFvWZqHTFo", "c7d3640c3485d14e115b68e13eff4fc6f952d3bc51ab22abd0191c1c6901754a" },
		{ "S3D_6vwS7UORcIdC2stM0g.meshx", "0a15db9ae6c449cb3e026d947046c44a981967a71ad6e8f67a9255c37e8b8f98" },
		{ "S3hMJBEbtilvucpXyHFxYyv0fM0", "212d4b67988686c3d0789cac07fc10dfe4163a2e002004a70abc54f9be1efe88" },
		{ "S4eJmdLw80DEYVkji31gwPFBuyg", "0116e7080eb2976a7cf2fcde56afedc996468f159240ebc7e50bb2825b89e75a" },
		{ "S4KQJeVnbWT3UutWfmpEJ7xELKA", "84071f1949a43d12333a2e320bfffca8160c9531f527f4512532911c15c830bd" },
		{ "s76tGLJ8wAVL2-0TvuJ__lN9YjM", "59a3ef4ccc3a7468274e39c76473625c906b1edc81121456e8986a617428b61d" },
		{ "S7Q0C-hZNn38bIrDC2KzXmDQSwY", "5fc95c631d608cc03c6e10bdd2e25f8d362cdb3994f283c353ae0ac7a0f67379" },
		{ "SajGBrRHcG7_9xdZPzb7oxQVqqo", "1e2a69fc42b4f3b061d2a7efd10173381109c3ffc84169509f803c1950959311" },
		{ "SaM2XrupAs-W3VP9HOTmfzsLCMA", "c5c3317e532ef69d402f6c99c6f99ec4341c91c6ded4765bd7cb87fea39cc0e7" },
		{ "sB5tSapkk4YF1zYBkIV9PY-VLvM", "33be900fb757e3bd171c6ff67ed97759639d06dde59a7c7658bae69b0c924222" },
		{ "Sb8vOAvCSIxd5R2hOqF8M3TdkxU", "1f9fb94c6d12f6ae0a0c2bef3a939b2577d519a924f362c7aa64dbeeb0619d91" },
		{ "SCQalYVYYeY_G8kHGBpVhHRvBMk", "ee39f08657db6e51ee34457d22a968a2188fa0fe8755e29b2647058a63958694" },
		{ "SD149eIwkt_yq8KxfP8zxSxR9xw", "254f8a91034e97866f7fd08eab0fdce30f0a7c713cc9b5a9b398dd09cb268312" },
		{ "sdOK3C0E1_XbMQflVbuUmDXSOb0", "ca7f3b5b326fa2e7fad227afa3481c449dfff0e81f17ff04b8d45a59d6009b54" },
		{ "sdTm8yvf_rBTzrg-ve1AaJZ_GRg", "b48e7306314d025fdfffd4f351c7e80cb26ab8757169d181ceef693752a39f83" },
		{ "SGLX2jht9KOJJa_6rLa8qgawnaQ", "a37e83aaa73d7623497bb53fc5dae8d4aa4491f312295694972a4b39bdcf06a6" },
		{ "sgpyYBNM9-YH1QyV0nJ8oneigEQ", "b8f1bb938f1ef3902423d42a156fb897b0dbf25c70cb1e6e6087a221a2a54852" },
		{ "sGYlS61KGWLxdCIsqBf1zG66oEU", "1225b2f26346d440887981c371137e3eee27614995143a08e938a39faa51d266" },
		{ "SiYZ-q_ReUuQ7vphRx8I5g", "c1ab95c39635b61753a65072321a9bd43a244af94e8bb6dae4b28fb7516094e2" },
		{ "SJBKlZPsmQ1KYops-IrxLzXhIME", "2971b25e76e640337aa8a6ed3ae28582614bfec0e04c5ae5d37484a0c2470e80" },
		{ "SJhDG-_M0XhIPvV_2dBb2WHP_28", "eccf904acf8c032e09b4317a40b97535228bb07c96c5bfe19180d558f433f0f9" },
		{ "sk1frFD1ruj38_uTv2aKxPaR9-U", "99dfa37fd70cdbf25b4d432bff45789c0903b87bbf48e63b393629a2766a37cf" },
		{ "sLXXQYhBJxmbmoub8dmZDg4vAOQ", "93c099aa935ae573b0d0a1fbd662fa6cc90cf0b8b4b2029ce23b8f32147b775c" },
		{ "SMFoejVTL37abHpPIdwvw8Oo3gY", "6545635b3ee805137e597d569756ccad84d45fe0902f97fdf6fd86b272302b54" },
		{ "SnOjCjU3TpGN_AHZERC35JBUoI0", "cc07e91e83bdd95ed50d4bae51698a5ef96fd0e820245a6cf4dce0e1bbae942c" },
		{ "sP4v2hCHfpaHqkk5RkrZs0tMHfY", "4e6cdb6acb63463a8029605706459d8346d9eb1d3aa23f1ae207eb4d934d2534" },
		{ "sr5WvNrGDRTBPGFtB810ppWLz5Y", "e58b1e522b931ba7b39402e61cafd519c14a2d03db267fa66fdc34ad5fa74bd2" },
		{ "stCLsDGP9-3ejt7d8bXn5ktllQk", "c24809cd39ba7991ffc44f48334fd701c1d0b4d80e8ddff1792ba3d1a41ce183" },
		{ "SUb7Y2tXTWjvR23o9TKJ-E3WNEo", "9a1a5a19b57a415a85020ae6e8aff95988ba9e299e7358313b8826d302ebc53b" },
		{ "sufpMnDp9pT4EfxKpSNQL6GnMWM", "92970d9138ec74bb11a302f09630f2cc8639bfd4aa542e128f5ba2ed8a6ea0d4" },
		{ "svLlk97-XgvlsMpalOhh1l3VkDc", "9059aeab13a48aec1fdb7ca84aea7e40816cfc624eb3da0787233aa5ba7c34c6" },
		{ "Sw6Kra7LQKr8K-8AXTVHZlQ3M44", "047a684f3cd12cd2b6ec391e4040f8e40d8874fbb4faed08502b11a1c69a2772" },
		{ "sw6yhgP8oUuggY9r7Re59Q", "4d2e75f1de264735c97029387c3dc94642e223b7a27bbef3d9b545574e60c64b" },
		{ "Sw7Jy31lN57e2zw_tHpM-ZqdUDU", "b014bca5c3aa1a2ec5e5a6c124e10c8d96ee68189924fee1b7cc02836a0d0aef" },
		{ "SWwxjB9p4dFuOqnjep0y9aiIQVQ", "72484a2006c63af412069bc7581adb135e3558132e6857aac483d407968ce432" },
		{ "SXGjINAyTk-7kaB51VrQ1g", "deddacccabe3c2e48a7181ead870b6a83c645fdcab4f6835c84fc13371eb83b4" },
		{ "sXxqt97HVH4ISXiC-ri8V9YvuCg", "acfc07dd360da3287f1d9bb7e3a7384c6bfe329a24f7ef3c084573f5983b140d" },
		{ "sXZchcCn4S5fokjt9suEOeOXhwQ", "8626e49e8276a03207a23c50c12ea82ae5d7c5b9c0abe042a383dc7c7d3cb8b9" },
		{ "syrA3ftAIE-tCGr4w1RlZw", "62f8cbe8640664e77cff4ee8fa7c075f1c13eb81c59bdc4654e5d66d4e2bf628" },
		{ "syrA3ftAIE-tCGr4w1RlZw.meshx", "62f8cbe8640664e77cff4ee8fa7c075f1c13eb81c59bdc4654e5d66d4e2bf628" },
		{ "syrmGwc2-keWcYu8sFI3jw", "67cfdf829085e00e3a3fddee6d9fff62f66cd74659154d062e09b6c98bd7b223" },
		{ "SzjjtADgeBcb9LdNC196O0DdBMk", "4c3daa7cadbf005ee67ec290b9bf26344bcfb9600c72478742a4213cfe13bbd8" },
		{ "s_sMZL-nDxX9Cc6wF6fdeg0CgcY", "16954983cba9d76b925b4050b76945d703665b2abee6c41f53db357ca799521c" },
		{ "S_srsVBDyESY4eksA82JPg", "08049c788f425592e157e922561a996c28a9686802e5a8f50cbf9b1ca7259edd" },
		{ "T-1je3Xa5T8Ed0k13b3niGzjfwo", "e8decc5d84f84cdbcfaaa6066919a91daa63cc4077ad6bf046205420d47519b7" },
		{ "T-lAxuwFX-1Fctd-lyBhFXPLk4k", "f49c2d711391333d4d060cba87292d85bc571cb5267f1a74f1ce96281c3544b6" },
		{ "T-nTQRBXOxh8kFfhecQKMJoZD1M", "7ca264f3207eff0d0cd2380ba3831009ad4ba5f87e8ff1d949308faf5cab730d" },
		{ "T0dq6AJ8SfAw_dGmUBYF0P_H8eE", "de8c48c46aed2db4f3dfc86fbcf3a9a6cbd7372fda766f47aade737d0b5a2c02" },
		{ "t4Gl_5C1bHroafDYXv61Ge4Ol6U", "5043a3e557b400c02d2abf377d20e5bb8c69d6bf4e55b57ac8ccd7b8a1daeb18" },
		{ "T4H3M_uJ3qgjXJXDpy-gfvt3KtU", "54d38a228f08d89e84e4d02239f235a4bcc19473f492ab74c1bc425a5531424f" },
		{ "T5X8SLXW2l5oay-Pxt_4fuKmnxg", "adbf8e56529dcec795279aca2add3aac5b634471abb2530bc9d3b8b49b633d12" },
		{ "T90ny9rc1efZ-zebYMoC91Gr6XQ", "2a64cfa63321a3e087b45a58f35ac69448b4ed44277ef49e0f9e662467fa44c5" },
		{ "TbrkWo_LvvzAYYkYPqR2-gGBW1Y", "ceae793848d4d57c74ad6315c65f5958fdb7e42d349b06802313675178639b0a" },
		{ "TCTbRwafvqupVuQ_6vnyI7SMQE0", "ebe6466d00f695d724ed1175c7331762e95beb24f908f93130b430f6fb9d3b2e" },
		{ "Tcu9n_xjM-D_IVUIxcXzKbaSXoU", "3aa0f46e577493c3e5415e8bf2907b7bb7716e4f7164f6b5216e6f866fc77d30" },
		{ "tcx46ame_QjvsRBf34noKuCNA8Q", "3782cd2bd5663bb21067e054d6dacfede3a66aac186df940982a3e56ea424080" },
		{ "tcXKSJdNx7NfBjOFolWFLdR-P0I", "d4ae63220e2329b02fd03b14aef0a0dd2a839407fdff656c40dc3a2ba39fa0a6" },
		{ "td8n5ajcLjSCJQUzWxKO0bWGNVk", "231ccb4b22a75226adac95b04508c2b843e0ae07c6f3461e7567e3b60fdc0667" },
		{ "tDsO4v_G9U1GhrR0s1mqyeETY2M", "c4d33b1bd47987777a997619515a0288dfbe97a160f4ef6f007c5a14658718b6" },
		{ "teDCufGR4_DrrRsUeTHvVd3iQTI", "8f01f159cd5bd12215013ab2ef7a6820add7b22d45ac671273f43fce17017e76" },
		{ "TefPNmuQEDQsu5UyeCPFG86Elck", "4f489d7c6ffb61e61cb1f125e54dfdd771d075a94922e311020b4b4f0612a225" },
		{ "TeIcSW3MuysoSQ3ej-B0V-9hGcc", "b8149143b35f1350294ec2c6de4d457ec34792c2496e222835249da9340db0ea" },
		{ "tFnD-AHCzxlGvQ2RyGRzn0kGQ9Y", "12e3f7c54b08020c27f785e29d41d2f329bf24e27a72eb14764dcc838c691d87" },
		{ "TfQhqDUhWUyu4pMMo2TkBA", "34c1ab374dda03d4b36e10f2720a83bc2e54d2527e0840107ae5d5db9594f71d" },
		{ "THjivUwHsnsSdxSVJQuZfuxlXZ8", "3ed71db3aadbdccd162236729c2c95dee5e9893a6824cbcd7833ac55f575bf39" },
		{ "tHuWMExGQ5sDCb0iraIJQMJKq14", "106c69f86f44ce49650b46392395f17ca855f9ff0ced4a753281989df39ac93c" },
		{ "Tio4YzmRpD4QQUmdy_PZ6_NJDJM", "d35ef19f83c94e8159f7832c0d14f7be42a3080109b7a8e3a4a4967c1114f4d1" },
		{ "TJs4FvKnT9eSsoflkhQ3LL7co8w", "10a2ce9c92aa009d92be523e89fb0f312d83c2a72a27a8b27eceb20349a3ec8d" },
		{ "TJZ1yA7L6IRMXWUYw_dQrJlMvnM", "953d4e03152276c83926d6f4cebfdf92129cad54210dc101f9e2ebb95d097594" },
		{ "tJzhxcyF3OYlj28nnE4Yan9zoms", "0fecd16920b2ad94b1232edbb30b4c4f23ef7a467f527f0d0aa0f928f00e3be8" },
		{ "TKkEQ1B65hx303Y0FymK5aeenGE", "a2b4c70a436fdea395310f35234eb16fe86a5b6de5de2fa4b3eb3dad2e8a67b7" },
		{ "tkqzomG1bKeXINYZW15wQYVnkgg", "a24e996e7744d7d7455cd712927950b27de982837af4a99a3a6bcb6cfc1a672e" },
		{ "tkW3kmJMPnQSiOwcEGVEnY4I10Y", "07060d3f0a68874c64a9fe31d41d7b1e7da6d2444a4ece3671728c4c6ae798bd" },
		{ "TmVNSKj2yQczz7rCG9SZAOka-00", "c958ab9fd24dba070e1bc6ac00dc0b75cd182cfd0bc5078880d281af5de3b495" },
		{ "To6Su0bjiwKIBUOAzNJgjK1SkaI", "7f8cef336f2d0c9c3bb3e9ecc18fb386abe09fe795d2d06fc9ebe046887da377" },
		{ "tO8rRGoai5rN2lz-XYAtAvdabLk", "73f67a028de2f63c3afe3cc6890d0a897ff2438671bfed07e4c00e8994cf674a" },
		{ "ToDTDU2dF5-ibixiBaHPhnz1pFw", "efd97e69846010e6de3631600bdc886a3f0b589b1b2b3136598e6bc0c78536ac" },
		{ "TOpKWAhRKgYNhZOdcOZKT1fvxG8", "3bf7911e1df11eb9754e86af78c619e3bfeb41c9790e64078b096cc26e6b81e4" },
		{ "TOPqRqfUuJEtKsGmKCK-6DvHfis", "5f090e200a43a9c83c7caac39903b66cc6d8239139f619aba51a0b08598f4799" },
		{ "TOWJyG3GOqfEOulBOKTy383KxFs", "9f677efee6bc08252f6dc7f52166f6c04868e3c58fe3a743b8d817be446759ad" },
		{ "TqdwexAmY5AHjzKq27Tug48kbZo", "b38dce4ad692e8df206629122f30fc1e4e71aa647d25668b83f9517e638bb964" },
		{ "trbBgxTsV6Z7CsoTFO5Ooe_MKkc", "aa65c6f972342f3e7086b88d45b7945e1baceb6e64f590c529f3aebcae7923fb" },
		{ "Ts-mmU_FzkyXKU05YcmH6g", "64db557eda2fb2f240fd759c210c714d9ac1e7dd241da23c2a3165c40ee5361e" },
		{ "tsprGCFbfkKVBp2K30ujOQ", "5fe60a866138d10f4725c113b7cff75dde67e39562b7cb36ddf5712bfbfd7c95" },
		{ "tSQ9JZHB5dlw08WkNHYUPkU32Co", "4fd53374b088c8d0778ca8271cf9427ad97f65fe4de7cef79c604a706427758b" },
		{ "TtRoM-2vU5_RgMGgYVqapJSvoPo", "669b0f41ed33b66e665ce3e581fbef9fbbd781a445e40f001ea66474d0208451" },
		{ "TuiEtd0tyU2mvsmL1zUbRw", "4b5821927c27ced65c016e6d6a8c9739bab07da628abbcb19c495372894e5fd6" },
		{ "TVo9YTYOow0baqxkNbS6JqAezT0", "5e54363b832e2aab37c91a89723ed27c84358cf282739511fcb93afb18cc4d5c" },
		{ "TVp6nohPbFqUBtrl_csXDKGh42k", "7be60ef0bc34b8240cb2cf4b3f97ef1fcff6d5ab63a58a4ad7b91f052488c384" },
		{ "TwWxkBUe27Qsyid3p00IvRr0ngk", "90fbeb8b21f1faf9d27643c03d7e06396399d2a34cf91a437a37e4b9df1273d9" },
		{ "tXNLt8j89Ny3xqxOz4RIVxGDqYQ", "3fe45802711c72518e595ce1ec1fab9c0c8757796ff9629405f822b90bedd331" },
		{ "txq6rCbIAX9W7BAW2NSjKa0ezTE", "ee1e0122113d65f42d753aee543ac27b14227d33663e452f108f4fec01a209f1" },
		{ "tzp9uEiALjj6uPp7CvT_rUB8Qsk", "446fce8f30639bf10391c301e060664a010571f0383725126f08c981b87435c7" },
		{ "u0XE5zVYwEK2K-B0IjjdWcwkBvs", "e2ce8ad114e8fc640350c6b11243807bf560be1d7c52bfe00e7a13567faaef65" },
		{ "u1Xp1ZQnd9x0Ai1f7UsiUh0aWu4", "12be9a337e9d185b6dbc9575c5b6134e614f8dc734a9573772932108f7a21562" },
		{ "u3yiARcJ3EK9CJ5xsU535g", "8e41ffec200606c892af582f87f9813ff926859219ee6392f38a60a2ad461f27" },
		{ "u5lGkEgtvtr2yMFYgrMYQQFZJw8", "733b9cb1960c5a62c5855e0f6eac5385e6e1d8b6a00a2495b76611e54197eaf7" },
		{ "U5_K-X_nPuy6GY-aVKlUmifDlYY", "4ab58c2ce28173d83d384a8cbfb3b567f86347602789171d994d84516129ea62" },
		{ "u7UeWCmUeK-I0wkQx0GTnZZSybc", "c4a7f1ccacd18a4a9baff42fb93b59ef9d84f7f5520b96e8527631c9b99d0088" },
		{ "U884sH9yLJPCpSRwLHEdSRa7y_I", "54bb374f9f2f11742dc627a8c5275e92c8ff7ae104f28ccbbd51214d6c16e8d3" },
		{ "u8CeblhD80TS7-WajcQ4bR16zdY", "04947803b5ab12bdceccb9b7edf4a76fd2abb58ccd63d69a0b74cc0c40abb2a1" },
		{ "UapcNAT-7X2lzauSL29yztC4G9Q", "d8192cd017c3c4e62d5c266ab5776090fe4748e4e1f4826ad63c888cb1df6b6b" },
		{ "uaxtOwUeOOlw5qXQgE3zRJn2X1c", "8db1e848358ae701b9a0549f50854600c358614cd83bfce3ed76c6ce8e168b8a" },
		{ "Ub1_nqp2JPeT2w4lAtVWdEvrMjA", "6a169d420328abad7a6014d43786373a1f8c768974efd48dfb34d94d00eee99a" },
		{ "UbCIUfoTnBsBZb5sS4hct48nmNI", "d973d9cba7d1e20d4630b802e916b2cbfee8fa742ccf1d4a6a7fef556395d1d5" },
		{ "UBP4wNxj89mhQsqfJ9sLrZ4-UXE", "d566b974b245aff5e198920582dc54bb0c2540495f516154a70d93494d3f6c97" },
		{ "UbTDhWG7wZEZJujIshSU-7D8xFE", "7fb7b075da646a902b4ddc0cc8626083e1e93f4fffa40cf352893fa5919fb5a2" },
		{ "ubx1mDaXGMEC3DlxorOvplzh5G0", "0a9f62488ab8705959e37ce32e2918565e7f649144a84669abfa1e0c9a2c1ddb" },
		{ "uDJyaTKAqbA-scUZFzYoh4KEqj0", "c27c7e555b9a847edc904261a83577320019f03fb3a7e528dceb9adefe4e7531" },
		{ "Uev_nAmqJDAjs4MTVZuJSN6ECJI", "088a806b368da7c21bbc4b33067856067fd975882d55a125546cefa6f06709cb" },
		{ "uFymf5qdtCjGSGrBIS3SGoaHO9M", "9537ea7457ce92d7b0bc977a41f818f093393fc03f8c0fc2faa85c63f2b42cd3" },
		{ "UhlTv_stGglLTRtFvUVP-l89CA4", "2883f403b8003be16456b9427cbb36d613250322a79dae6c722f77014ce212e5" },
		{ "UHqqWvmZ0kiRbLFdstvjeg.meshx", "180d3e772e6c7d9067a21744ffb73e979268967c10d0bbd57108e65cd506578c" },
		{ "UihYVsOTZU-VEQyFTp512g", "27259a96125b23c4b6063b77b2ed01dec6e91bf63a4b165870ce7fbfa5f06107" },
		{ "UJmyBeodSEOCCn1DdwI6Xg", "93fbd8db207eeeae16b51b7d0df2d9b36118de55b412548de05b0802557c53e6" },
		{ "UJmyBeodSEOCCn1DdwI6Xg.meshx", "93fbd8db207eeeae16b51b7d0df2d9b36118de55b412548de05b0802557c53e6" },
		{ "UJOrQm1TKtZbV5P9shAImzvGQuY", "978ff4e20296851943e03dacf2353f1bedd350b4a62cfe7854c57d139cd3ca85" },
		{ "UJpIsmVOIiU6NySDybDcmN8huK8", "a5b8c99f8bcbcd1fe84005ba4cea74a0646a72421b2474b82f42f58e9d1a693d" },
		{ "ukv19qWnarzzZfIgjo8xmbV7csQ", "770c3552231b5b76f3d2a0c2887ee3647fce890358522c55f9d325ea105fa45d" },
		{ "ul57Rv7fC4HT2xx9jrhXbYudQdg", "378d2c97616c9c93884880cf121c49d5b34152dd633ef3b9f1b3f3be289cb784" },
		{ "umG9hmMl-EC-6007ioThOQ", "43aa68f4d7438e81cd0e64b1eae6375713bb631d99627c10be8155abea62bc0c" },
		{ "Unhp6e7BekixDZJdEv4G5g.meshx", "426fb8543e016d7f218c4b2811f8b202d41e35705402571f98bad4c66c555be5" },
		{ "UNjxw95zwdJc9HjCyESqQCLLu24", "f9c785ce79404d6901c6bb8a5e13df4fd238c3672090eafb7bb1659b4e1fc06e" },
		{ "UNPHTgdQJsO90mBCpmxCZaAxW80", "6c2db586cc0c589a312d465921977b0122e224738aab2c023aba86e44f4512f8" },
		{ "UOBq3nb5Esw068q6FxGmQ9kAKYo", "b6b594335e42d60dd8c6465c3c24c5d39f1eb13a809e951c580e3bb6bbfdc11e" },
		{ "UoWzQ1M_9vQV86st_UJ9QdjJ-Tk", "1c460915c064f3f73aa8b2bdc905b4828b1fda8cdaef291876b4db607fc196c4" },
		{ "Uox1YO92ei51kObCEjr3ahrkJ-U", "4fd2e14008676ca4826bd3775c1b1710ae05a563bc7ef034fce89c64c97f8e8c" },
		{ "uoy_k78tSC-pGrsiGQUq3HZnXhI", "e3ab3c1fccfe14c17bb942ec0feb2c1c29bbd15da32d01e3a95a8642788ff2cc" },
		{ "UrG09j3A1Colve7okQZoaf2AKB4", "dfd70e683b1e60b01056d207a6fd3ed895f333e9918f56c64db52a5d6a93506d" },
		{ "uthxXZbH6FP0-MZyGw5BOFz3dms", "ac85d67780f83ce07756172adc03d3327ad7b8a758e4f53bee790cfaa278572c" },
		{ "UtMJ8mJMP7ysJ5xs_7lEPboQLqo", "e87e922f707be13c45507d379e0f226da6f0db239b7cbb1414c055ac77851fde" },
		{ "uTv3KwIk1UahvgF7ZEDBdA", "711120d7b6ed2bf5d1296f7c196d09e8b95c54dfa43f86a5b2cb4320ad9c6296" },
		{ "UUE3yMt73O9KKA2BhS8VSG89ZRQ", "94b1dd02196072a7018e989a571f1a23f75dad973a96405b8e7006832e27b9b5" },
		{ "uV9BEc5jEGnGD6Rgn9s6qJEVIG0", "b26efd69c7420df537535b7ccf51927e7b16e666ee1da9e0ad0fefd991c66a1e" },
		{ "UVEAhmqsXWztNs_CBblb-kHXkpU", "c0316c15a2342812980e6ba8dbfc08568003394342c9c0bd87f053f76f522dda" },
		{ "UVK2qiQ24--Wanhq9sffIRL9I4Q", "5987aa38b5a55a5bfaaef166111d7686ace2f3cf8cb917a93005a30c9f9acbcb" },
		{ "uvsFUnAoP6e4a4Y4FPyZCHUFtXk", "d86d6a193bd366a5844d46fab7a4a99c1a7ede11b4193124008f81170913b97d" },
		{ "UVXjQNxumBuPjyvrIhm0BtEbi-I", "bfddb170303c0e7fb836cf080c381b35603423a056e72a75c7d174fac1fb95bc" },
		{ "uvyhRCBCfUe3yj3BL4wtoA", "aa3dbbe30aca537291d90e9ef9e1344fed648c56557e6eb63cfa105b639c7c5b" },
		{ "UwvM49t2bFk3KF7P0wrmGVSmUuo", "160483d35c558ff33b18f186677da2382f5693eeca922298a46500f0a96ec086" },
		{ "UWW1u8uWAZzbngqZdESQpqQ2f-Y", "725067d629bc3cd3dc4ee6d915400535071e1be2c133086c01c830a90b04b1f4" },
		{ "uxUdXj-NFQpuLxv0HYibxBcYGbI", "9ea0977ab46e4237bb04a6607f98064b0bd5c5381bff87d0f7d7ef9b1a6e00bc" },
		{ "uYmLenn0erhJY8PR8UiMj_VJvIY", "ee350c7019280cab19cb5b987c0d49a420e8ef6d7248c8ae570099ab5bcd04d4" },
		{ "uyZr92vudL3lIHj03HIQeXvRtwc", "f2b4e7e1205a909f07a47f65e859c202559ee3172f8e383e38b67d6acd126368" },
		{ "uZtsiJ4zMcWrpX8zhoehUgZLNPc", "65c4628e387f555a46cb420607b9f89ed8cf43013d592588bb89ae9396be2249" },
		{ "v-kaxlZgS0rtxo_gJI4muATsWdk", "76d754e239333da663aa5aa1f207c619a6af63b6cef73bfed82d488517a7351e" },
		{ "v0l8YwIy4hO8nJiekzAIB7J3h4w", "c9cd77e6881c4aa13517e439587e39f5747b122c111cf64d0f3c0f199d3ad8a9" },
		{ "v2MQc-6Jmne9pta2Qs6n_QNkPEc", "652b13743ac5a876ec76f5df5ffd2ac20f6d64477b82cde54c3b37e46f3c175d" },
		{ "v3bjAottWttiRgYDNIwhgEOfLuM", "78cbc0154c3635d54432444a45910ff9217e7753ba7d8d29ba937efd66e4b5c4" },
		{ "V3VxD42BSnFm-6He3oypUPMjLF8", "2e2bb3af14c089f029c65ca5346b6abce9ac34defd1ed8561dd0af334ec5b269" },
		{ "v4M5HRlI9Y-FSIwy0a088ZtYuuU", "236047652d52104512796c863fe0ed135c71fc5bc3fccbe9a2262ac577ecae71" },
		{ "v5RF36XAcC2do857sMEasjmEGTs", "a47a2751aab431ddb36fd1d5b7ba335dec5c07ce8443709ef508dc80803bd802" },
		{ "V6op1A8yL0SONvklZYRCLg", "ad0232598231a7e03cae18f70181c85de670d25bd80399f371e5cd867f5233cf" },
		{ "V6vAyInL42bM1S-nPGtIo26B5rY", "825dfc93afe803e9ddc602ce1570bf3d36877fafd01f96458f6d0db6d7bcc8c7" },
		{ "v6YajbCkv0-eO8ysZj7SdQ", "399cae7a8d6a21ee752ab8dbb86a1063eaebe7acada4db447e23879328cfde4c" },
		{ "V8jByF0DmPkUksSFCtfWDV8GUHc", "fd3c365d3a65c2ecce5967620787dd174eb914b74cbf4d94f65ce6970008b206" },
		{ "V8JlqLgKfSDIGcQzmQ8R2kr8rcU", "8e02f6b5617f3884f8dd20a6a70661a0736cba8652c147ce9aa637f6d1e9ba37" },
		{ "v8Q6B2O3IIBf9MHwaXX5J8PeXeo", "3def0dcd41add86c312d13a2c0759f31737a57d22a775c955206a16b8fba3fe1" },
		{ "V8wx4w4AGgcLthdbqJ5ISVsGV_I", "3100ea70e3a95869d4d58c32268754c8dd5137e8965fa1a5818507594f12dd2c" },
		{ "v9J16-INBosGTj1RlrW7iVb8f_0", "3ea948fb179cab2a38bec7a724aeb44eb87dc401afe0128379075970575b701f" },
		{ "VaEK0aKQdhH6PhrDkpvrWur6rew", "f9f2e773df15138d6cd4353d3fad787d01ee3f2da6c7b3c493667019dba307d3" },
		{ "vClQVjphEvLHqiQ4oNUiXpYVQWY", "7036627d5bc4b4c3041c2d2b0bf9f8c87a5ba9f0cd5eb171cf2863337b43d5fe" },
		{ "VElhFu47M4I9k2H0JA8Q4M0IUy4", "f07ef4efef29f9bdc5e2375d39968b5e15e30cf8a06e478a4b2876be269f4a9a" },
		{ "vfgKcfaJDED5Lefp-d1rIWthcYY", "7c19ead487b1b5af2c365cb913a35e64ca5e3abacf627ac90019d11bf0a0e885" },
		{ "VfoC2pOvlcuBVVCaP8e_qU7VoRQ", "1abfac02dedf09a601abb13335f731458c5c1191493a92719cfad4dd14096f6e" },
		{ "vG8OLPunsHb8AZ0spAgNL5sVBnA", "8193ee644e20991cdbdeb1422e596098a3c91f08ceaffadc6cb0c38cb39e22e2" },
		{ "VGulAXlgCQdm_2QYGZSBqiFdCNs", "1d73e10181a8aa5f6b091f51ecf56504acd6572443337fb64578c80bda9f833c" },
		{ "Vih76rP_Jybdr7-rbcfuS9blDSY", "1d7c71d167cae6d5cd0cb31033750d53863ed892d322fb2b6ae68bd9acc0f282" },
		{ "VJD4FleGG0giuiliuowcafJ2ULI", "0c4eaef68db4b2245a8d7d7651ea82e461c520c6cae78b1ed55fdace19246f2a" },
		{ "vjvp2-6l6bUP3cyVWr0JWN5Ffvc", "bafa69e9f91aa511627596a29861b68103ecd140b71ac51cbde5c7b08306ef11" },
		{ "VKTxlnere0SsfGEjkZPUsQ", "80bc9f5ea9cb92e99def5e08be0c60b715c4b5e0c6907a331f36644e6ead90a0" },
		{ "VlO1KMG3BrFwL_3_1yudty6rTxs", "db5fc4f06fa0134266b674d19b5b1617edf71b1fda1b20615f2ae2c0676c1282" },
		{ "VoqQs9yXwtknjdfKUPjYGSC0bmQ", "39bccfcad708080dfd8821235e090900d2d9bfce1a83dbb4bd1691e14004134e" },
		{ "vpcQ-MIlQ9gnKl1xwAoplNlibLw", "dfc4a93bbb2f9eb8340cd790df7de1834e7c03694daa86674c3b46ad578c709c" },
		{ "VTa1IfewubP8EJq_-sCW5QGtFog", "5991ca3f9ef1afcac0906fabe185e59349743c88ffaf2a269d849bd41f7d7c04" },
		{ "vtIAnr9nbHQ-E_41tBZGsIlQ_2k", "308c712904ca37e9552f527b9e1f1eecc7588b15f68f7509c46e248ea5c4b184" },
		{ "vTqqWcCOGowN9iIuMcScfLnTFcY", "a1e21be90a4e5dcee1edd55b4e0c6fc719ef838bf08cf4960d0f41c341868e76" },
		{ "vUWI8TwzvnNKacHpZtnZhEWFSAk", "8866cd0873e08d16d2de24ad00923075371fe29edaee9cb13321036aa222f610" },
		{ "vwAxCe4MZPNzP4zh_igtDelVPMQ", "b2d3b28327125d3700dfd1d17639875c61592bb0f401164f1a9bfe88fe1233d4" },
		{ "VweEglE-MfpF_95CsNF__XdW2K4", "3c181278848b729fd15065faa3039b31b202660d6bdbdcb0f70af3e2577d3090" },
		{ "vWFs7ZIdJdVXNfUCDmZqDLIiRco", "99776d7c9924088cfb48c1f0f9e7f948a77d1a9ee9a6509962657c9a98f5f5ac" },
		{ "vXCWaijISmcjqfrwfN2b0mq0FpI", "fb0d152279b770b5b691a003a6c2fd4ec783efa254aefdee822ec4991529cc07" },
		{ "vYommFF-5I8AeYctxJqu3qFUvjw", "d8f95821c8dcbffb98c7fafb387dfbc83151879723cb39edc215ae15047055ee" },
		{ "VZZbnROtc-TCN2NL7GmtdgQoG9Q", "0130b1e187cdf1800c2865a40915c56ec177b5c9ed045a2097c3355ec9b5919c" },
		{ "v_AOrOpSFwSm5LlGiHCDG9tBiDo", "ef4bbd1748327bc50e6fd66c911e817f385be75243c00b35d104ac97f58b15ad" },
		{ "V_f0CfhsyG7NGikb8_96TUQ5mi8", "b66e7fec655a6568b76730718879e14d3db482f812f6bb01262f732cf277c5c8" },
		{ "V_fs2-2YNl7T83Dv-be5CVcXM7o", "b419724b8848fa4be5e02b8900cca50053856440d855e5d2031548471376fcd6" },
		{ "w-uH5BsanJzhVG32sZRlQ6LjZgg", "f2f96b9e1cd70214df08ebf77ffbc7d8e6f00615b60ed1997eebd054f54e2256" },
		{ "w0lOagQkUki1bX-U-zDgtw", "26e7edcb24cbf2172e3826e2bb870fdff4708b48922024a3f21edf65c295d4bf" },
		{ "w0lOagQkUki1bX-U-zDgtw.meshx", "26e7edcb24cbf2172e3826e2bb870fdff4708b48922024a3f21edf65c295d4bf" },
		{ "W0vIVX8fYihC6sBkLQaPf9LGVwI", "df1f48682cbc5e1ce405dbbf455590abbfc864956ea47fdb6fae343756d51cef" },
		{ "W5DNfpJ67Yx4t-C-3Z2lwJ_Xf7g", "43dd880d8e153b89cb941ff9b3a9cf1ae88c576fcb38f641d4b70b2c7ebfa65f" },
		{ "w7hQroMqpkWhorcCYVSfVA", "bc78a323c154db720320a41d3e049b5a7453db86c817d96cdcdbcb62328b51eb" },
		{ "W8aVd7WHrJf-mxKHY5qaLXI5Kvo", "c2e5b255110671ebc9851e31c7366ee1da5d013a5937270b1f8dc196a766db42" },
		{ "Wa2hS9Q_I6o-UT7MKEjilSYAt_E", "e73bee519be32015e98f669b60db8891a300f397389e0c8d3e4d4fe5afd3a982" },
		{ "WADAmBIT-cZAvQrKRDjkITDeO-A", "1a2beea49065427afe25a8a2d86c5a07169ac6e5994b57f041f6a66c599cc303" },
		{ "WAo6aEi4gEqWyf9CTk8fQg", "38ad05c5f253a6361bbe4e2549b7ac80d744cf3f39e71081f76969eedef8d52a" },
		{ "Wb5IdUKITl-AjpqOMVbtIb4CNfg", "6ec02469b209e76009095d1b2d91e19826d8858d628e15819e78feeb26a61f75" },
		{ "wcL3IJpzj7-xWlbqg_sn-slw2XE", "a091a126c41b79b60421455fbd825e9819f162fba12aec7848936df19b212133" },
		{ "WeGzv8ODxUi0ZtL9EqIA9Q", "a8769775d8da247266f8d4424fd02f24f6fffd840b70af93f85b4f2da4ff562c" },
		{ "wEwH9TM--sgbzyY-_dBzyliMrGg", "fd2bdd933eda77662687f206ce887d8afa7c41c412d015e98b7bbf951649390a" },
		{ "wfOs1YqWfmByipB8eZ9y3TImzx0", "29cf19e53e5305df1ea6a104eaf9ed2fd08a06aac0d134696695f1a0558df09e" },
		{ "WG7nkkpXH__Q-Hj0iJo3yGVeVpQ", "1983b4b340b26b43b42da110eb0fba24061b6ecf1342aeb9a102c4225fce886f" },
		{ "WGhNtrb9-wld8s_Behw5XKJd2UY", "5adbb62de3e0c0bede19f09dc69f32983a49e63668545635e274f0e454004df7" },
		{ "wGSKAOu7VTEL64OCjFeXf4V9bFU", "aec775a1244a40d10185c87dd462ec9df6337e7afff9d86f68261e9edcb64f25" },
		{ "wh0FU_KP0tkK6pqtlIZs3Nt3EHM", "206edf01ecbade1b63810cb084eb5f4e7ee3b0a0c3ea820e1794defe8d1e2b97" },
		{ "whYUEUp2QPLYbhQbS2muGsu0ahs", "af9dfb3abcdc04dc10a164e4ec1ad262723db6400c3d84e829dd83b1144c1a26" },
		{ "wiP7Mb4nGDHlBHASnq7vGK720g4", "e262d479a4c6e6880c47effea89842c357439321f5c8c85b03b4b29fc55a4311" },
		{ "wJG2awHJy0-wa_nLKe_GJQ", "d562309d9b77d233a5762765745447847c64b510d9d986842ef0497cc8d2ac15" },
		{ "wKabLlzwoT2LuOr6Ew9cjlYovWY", "ae6284d0ff5acecaf415d4d3627a586c304eb7621839cdc6b86a4d6e593b8de2" },
		{ "WKcXyFXLIwetu3jA2XfsoqPlPk8", "7958c85f851c2465543f26e4d35e9b155bb3e627d10fc8bcc068807b474b8202" },
		{ "wl9zrCepXiBV2OJNHlT3m3gPfD8", "561ad964319e013d89b9ac8b9b1ae65c41a1c450c9dda0b172a5f360ea5e3205" },
		{ "wlKut3AmKSVlRlb3krqwixn3DEU", "d47266bc6f8621dcfdb778b7c34c21009ca637321f376bfac197228ccbb8e496" },
		{ "WLrKPRyLVhUAwDfym72_0191-78", "4c93e2a3b52b12ef8995f68a91489564c51e9e39913a2174e60a011ec71b6cec" },
		{ "WlS0IP-AkSZp-g0pRH6iMRUET3c", "45e24170b3c7fd05703cd83ba43f5d2e00d71ddd62fd7af5e65804dd5df4aac7" },
		{ "WLXfN_8VYWmCuqP9EzZhInKS4ig", "30049d483006da7c13938a46971c087e4d12aedf9d9529a6ea716f9521c30b82" },
		{ "WnHCAHYh7Hecd6yCjcAkQoFypT0", "f88b96c6d8a9ae8ac49a78d8bb20643a2623000bf14369fe1ef22c043498ec2f" },
		{ "WNlT3xYwQU96wta1ZTblCfEf_5g", "e3c4c2b91ff85e47a4b4bcaf459e61b5be633a67d98da3c5f001ceb2bd8e85c9" },
		{ "Wp4KT3onSHby1M0Nvt1W6LNYsAY", "c54d8ee6afd9842c2e3a66317c7e0122dcbdfeb3f5d5228aedc48561d2b070c5" },
		{ "wrcFsZg7BslbuhV7GAF4hmNPjMw", "f2bfbc48847e274f335da2ed60f741a3fcfd179e620ee25ac9a4972051125295" },
		{ "wRMJjle_4LI99b6KIllR1MZXAG4", "854f0a8da854994ba33c018f18b0ac10e58a13c823e6ba61a0456745cbe3a405" },
		{ "WTIG2Tg3GLkiau-uMfiTNyVT4IY", "588adf22dd26441008393b3505422d2344d3f58d4daee38985f35d895b40a89a" },
		{ "WTRgdD4z82knjdjj-tXX4rlQbx8", "e8560df3037fd771e2e4dcde7fbc57d7641a36be4e9e3dd262d454e95f43d1fb" },
		{ "wuIp73N-59RH8k-SqCTNOOQIVuw", "f83cedf0d895f5997793e743883be0926faa42aa3b69497622a5b616e8a650a1" },
		{ "wUn6NDZf0DYfeS_AuyDuWWp6kfE", "4dffc9fb5318c12706a5d5275d4c7fb8c65d49aa7968958670d709cc58d91930" },
		{ "Ww77oz45X2OcGnYxZ20jfVET8gY", "d62d4984ec6f805e657796ddcb4fb6704ff03c0692ea31b6a69732f34432dc5f" },
		{ "WwOXAUJ5vqu7Xhe7hGSXpQwPVDQ", "2224592b6136b065559b5fd639e4800359e3336c8c3b0772a63aa7f5f951f674" },
		{ "wYFYsU0eEH2gALtf5diryMzp-1g", "76243acbbb33079bcb03424df1d151cc38602d1694df7f2312622a76e5db9446" },
		{ "WyKYvFHsTKdh5ccaSSa3ZSOaxVM", "5a0080c9737eb337f999f8fe198b6089b9fb2d59b9f98baec559c34c0e076072" },
		{ "wzFvlL6eo3CfkLaV2TO3u3DQl4M", "147c576986b9b07536d6ce79b5c59a422bdf11cddf1cb287cd1eb99a2fb38d30" },
		{ "wZgy1a2eO_JPwX-qQQO0yaq0zUI", "3c0c82917cf3d79c1c5f48124a8b4193286b650b734cb6449c396427896a2cd8" },
		{ "Wzr97zcEm_V7d-f2ruqyBlTtGWU", "8fa20042205df2c2ca0b0826654dffb60524e5dc6c221a83b65dad9f92f9b408" },
		{ "w_GMtTNF8QDFt9SABsxgckWnmw4", "3e4d25aa1ce73289598d118daa383d0e0e3e75448ed7e804c58aee64d985558a" },
		{ "X0zZ3iMXWQ1w2xPohRD9Bty23Ko", "e3c2b4e02a2f504c9e39a6dcd827a4398a585e9fc3ad3ad80447249743ff929b" },
		{ "X4FkVj3swG8QakrhcnV60Zx0I0Q", "db33c6b5fd7a4a2d61f9c98290daac04c1bf523b330ac38bdf9f6d1e9ef9e76c" },
		{ "x632r0Z9bIxlh4g0abLL2ceSDbs", "501eadfdb3f6a5da192e2c5b1ecbe7d40ca5bffa88ac9fc6eac48a556bd5c5cd" },
		{ "X6V4VCDrWYH6W7cU-vqeJ2zlb1g", "db545d78b812544e4e5d01687e2e105613e291bc95ad2e5e93fd3fe289572ef0" },
		{ "x71QmaRiPavweoHgp2NRsxdCspg", "18a2a8c6c40cfdf51416406a2ae0c220f473862eb437d0bbdc7587e72cccee79" },
		{ "X80E0_am4DpwJBGPmbiM9hpMv8c", "efb02ae49210c6737339fc37b8dde9cf147c59cb7716b60c8bc94f56fe72ceb9" },
		{ "X8kl5dqYz-9_CfnfEsDdPj-Ac3Y", "bb0bac40df18d3928d8f029674610ac837ed6ea60bef31f4bde2869bb05c3734" },
		{ "x91127HKRMFpx6W6d85pyvvhWts", "10367bc44d2c3991300d14eb541820ac69d3c9c0b3620c37413986ea2628358f" },
		{ "X9EQn_vDtvix3iXKrwU9aD1c644", "299a25ca7c171097e080e634423836789f9f21a344906ae37df153e0f2e6ed0e" },
		{ "xA3FTAdpawjU7kzOVZW2FPQUnN4", "e25fc206f8904ea34b4f2fad524ddd5a3b356fcdee4e48957f85aeb611466130" },
		{ "xaTXz1hpdFrsP_yvHR0iA-ECUBE", "c55952915d8a9af2c34c31c2f34cfe835d9380c103d6e8500de391627a51824e" },
		{ "xb0TQYLBWpMMI5CebBkui2KYFBs", "886ca405befa0162e0533e4bacb5c5570a8e9de3897442021fd25a09363ff210" },
		{ "xc9ys-lLzqEbd0Ui2T2NroIFzZ0", "c13d72a59dd0205f7fe538c8c32082ff51507a8d6be6ce53096d6470001ea7a7" },
		{ "XCHXOCOd_Uh8CJ0Rpiwz-CEb9XA", "f65905b45e962c134883233b5ac2f6ab6ad7f864df5a3b7aa872193d96db171a" },
		{ "XdEPVUBCnO5b7ASi1voR4tBwTrM", "5e83f0153cc4c012b525ccf5d04dc5f66aafa7dadb48d27967ad309ccccc91bd" },
		{ "Xdr3Um88go2djp_s5u3Bi4cWTeE", "9e1366a59a66e59bef40f86d0568d5e1e4c5184a7568ae9f64e98509923bb971" },
		{ "xEHN-xHj6_jDH3VTgYQtEp26eOM", "c3f6ecd02fff49dfc312f3c837730887e011e8c4c6986b72a6f635dd53de1ac5" },
		{ "XeN1-xoG6qvSf-h3Vxeoac8Ulgc", "2272537d46ecee1e5c71a9e9887aa187a101676529c1ce4bfcb826cfe6527abf" },
		{ "XfyuQGLUxUeQH7-vFMv2jg", "865ba02704cb23016318c97f70db088b1ba7443f789ca1f3b20ebc74ac916786" },
		{ "XfyuQGLUxUeQH7-vFMv2jg.png", "865ba02704cb23016318c97f70db088b1ba7443f789ca1f3b20ebc74ac916786" },
		{ "XG6nPA29q8AjhhslG16I9Hlo8M8", "5675ef6ba3c9fa83e2ba189488336fec88a4a10cf0767c7a6f12028b3c55b45c" },
		{ "xGn-yMgviUGcd_sHKd8Cfg", "9af2fc9ec6ce816ac5167b5d76715daf1d8ed3f809c4ce4eb724479c306267e7" },
		{ "XhV7XmA4ihaGUIgIy3DXU0umKpU", "3f280ef17d9cd09d63dbfb856c1ad831019da29550f58b16d6a669a37653998e" },
		{ "Xikj17mrz_bk9LZt4xTMkr08MFc", "223a6de1ca1c9ae5c9705f53abd3f876565a051647ff524c4fdd9fb7d8b9c584" },
		{ "XIMKLrXNrcJgags6h7pTy7pkSOc", "ac58b6570fe7855d3319f8c4ecb537723d084783281e4aa4ba946d8ca5fd3e00" },
		{ "XirfXQH_Nh1NPOMOP3KpejR6sQo", "855acc909c8f3d609bf8b8a985934cfa08eaf8c3794705664b8ca3b9639fceb1" },
		{ "XJipV5_u8WOIcDkeYG1L78yVVVs", "cb2f7bd9936acb1450796a398423af9a3fbef70cac48cf1e4a4f4c8f40637b6b" },
		{ "xKo4yeR1RAaT4uexQaVy-Q7msq0", "44559e33fadc98cd09b2e2d772a9d9e53b1c31aef23c18529d7f074b89bb9d88" },
		{ "XlwtGMXU-EGV72jBToNMkg", "e5dce28c9087d08073c8a386e4181bdaea1b591a4cef7c97188720a7e28f63b5" },
		{ "xlY7vbv4scSkfxCG1Lw6p24onNc", "153a013dc69ce2248f00155370e0489016de9e2c294aaa21a02bbc4e7164f2ed" },
		{ "xMQ6qevobEixW5z7SAbsvQ", "b97bbdc17a9cebf702e5c752c45273b72a98177d8407ae5ea43ca803327d9de3" },
		{ "xnAfoxGk93Bwdu-n5vJj_OZ3xck", "4caf1e25a26efe77ca68a14bfd66b733c69f45a3f6afcbebce8f9d8e098b635c" },
		{ "xNtMJcoQaQAOB-2iRMedoCjq41M", "73169de7defcc6112e978e486ad286e6becc21b357e962255cf68790f63bbfcd" },
		{ "XOaZrX3xnDacjXnxiPbCl4XWAiA", "eb8d4aeb5247a8381ed0cf169dca48c6e5120977bb6974ed5d45fe71483546ee" },
		{ "XOjTqDiFG8YbtPZi2HV4hzCgkic", "84b09af687c82348e44ebdc47b171004f4755a230ec52a2e484f7361ca92efdf" },
		{ "xPHZkAdYNpvSxeX5bV0fch9nQM8", "36276376226f87e8efbb2657d471bacf8deb1997996efe69a91dd1a5ac72e2f9" },
		{ "XPwgyO-NQ5uxsD7hn2uzu3Hq2Gg", "dc9daf112e03e216c576693d0f1d67b53b5b0bbdd0aa20ac9354e2b4395a4ef8" },
		{ "XQEi8xY6ehFMB6PH7_jbH7pKNA4", "376d0ae831d31d5c1cd6c77087be0b2f4c8fc6b2788e29763a714a41f0a6ebe1" },
		{ "XqNSL_Tckak5R3TpFQG0xfi7lqE", "ba8597e5f8dbd8de0ccbc0b3575ab814ef2bc204291e7da7bf7862fab1eb15c3" },
		{ "xqYB3uTyRoJ43VgOmsiR_svwkGk", "940c4783ecc2ac27f697439971fce0f29f26299c1271bd61e2626d9ac2c227a2" },
		{ "xrCRvOl0OkuZ6Z1iQiws0A", "2ecfe68c659349fd041c5e66344d805f2f8cac6092646d3f45ed0bde81ed9f90" },
		{ "xrCRvOl0OkuZ6Z1iQiws0A.meshx", "2ecfe68c659349fd041c5e66344d805f2f8cac6092646d3f45ed0bde81ed9f90" },
		{ "xRHP5bym4X71qdjVbwNvTNhWwcY", "3cb30f93f690b759037c6f85123903a8fc85b9137fbdad0b0648bf0f88386a26" },
		{ "Xt9U-sjmoxrMVjUne8bASQwjDVc", "be1f94a0425b119ff72b6c929981e3d8971906cd2f9a3005d7fa0d541d8d371b" },
		{ "xUaMZDMD6O_O6norUct64ZHA6YE", "f9342b60fa50732694a030fd76e541a6930d77d6a7b40f657ffc76fa883f3d3f" },
		{ "XUSE2zE91keawyUQBDNIwawUrUU", "e2132f0a97053a05d5c41bf425e049d50655481936fb14ca28f0232068328b2a" },
		{ "XuX-CQ-bgz6Ld_jIlw7A_hkB58k", "49221aabc3e16e6f9e93ee945816fb21071229208d93fe1b4f0367d0baa3c090" },
		{ "XxGsI-zzvEsbdGabG_ZWFwILLOM", "08b401632e2153e990b41abd20a396dccdd2872ce8305bcb67fb5e1e645bad9c" },
		{ "xYcNH4hx7vLU5ndQ_3bzHkGt2iE", "61004122a22787bf75320ea293a3674bc138bc08f09a638d1f752498a157079b" },
		{ "XyOc_mf-z0K6tBfZLN36Xg", "0614dcda5c9117b8576e74ac53e082ce4f4334de0882de45d2c95401f908d2e8" },
		{ "XZXg7Y9CGfaqidc0Cp40siBa_mI", "882ce89bad6dc20ec30f76ec08092e1bf18003a746b42e56131ca0411c72f3b3" },
		{ "x_TioaG68o-x4IX19RGqHiHQMhs", "8237d80e3544265ec6c0285c5b1e0ac07f54cfcb87baacf00315ae3ad569dc39" },
		{ "y-LuW6C_Q-NMZb5urASIszflwtY", "f397f77e2a050214d9fb3783361b3658edcce87003a4790657d32900521fbf19" },
		{ "Y0sLfHyqyrucOR8BwfBamLQ1INY", "3600c16e726690632c9a5c44d34219e3e16f35241a7a40abbe177c38b6645a70" },
		{ "y1WrSa0euUuqOlqw1RcgLQ", "96a9cbefd5744e16d0f6298e4d0dce20981429caaa0f9d53ca76d672a514dec6" },
		{ "Y32oBtJp0p_TZ6khxsgTpDML57s", "226fb89bad62c337875b72bf28151a67fa74be923fce2f7b264448895a934059" },
		{ "Y3aAHuXW0rBkGjFKPmFAXHBhUKg", "0d100d942fa3225292df2ba4b702dc2082086250f911c68804b7e70facea37c7" },
		{ "Y43unbXHbUSnh8_sWecJWg", "d00a38f4ff72536ec6e854c5fc8b003daa3fd4038d1f2b4004957d1c3fb1a82c" },
		{ "Y45bf9gUDHl7MW8jweVNZjYHYho", "9fb31bdda1f316496cac63d49a520eec3c4aa42bf7a51d4dae027bb919d50df1" },
		{ "y4nU2PIHf7QPdwli0aN-3GIl2mM", "706e961afa67f86fec8078555269d254f0eb3c1daac17795e82104e94f9df991" },
		{ "Y6yKG2cxS0O97_cE7qNMvQ", "0c72f6c58613bfce2becfd40c1fcff9c0d2542a2e325ee5bc3bd672557fa5c24" },
		{ "y75di9_BBBUS1aeuA6Y7c0XezwU", "dbad50e67a73a1e0855ceecf8a506ef855ae2ef5d40345dad1de2a70e82d4442" },
		{ "y7N6ljUhpNgoVmo8Iv0-fuFMeL8", "5bb78ae210ccdbfc1399a35821ccc451db9ba744cb03c043e78b5cc24a6e3cf7" },
		{ "Y7uW8qYdqeiwKh6PrMY3m8CCnxw", "d96b01df710b0217b4a749c325d9f2ef1b2a4148e6ec2e3917b658fa64854b6e" },
		{ "Y7ZmSQQ950K9JMQrvzb4mA", "df52851d07efb4d32dd7ce5a71582aea85124503218137a1be99e500bd8a1183" },
		{ "y8Fyjkl6e0_EKifoBF3Iz7DN6a4", "026dc9842e54c5f66e347bae256ffe0ee02ee0eb4bd2d8765fff5b68f5c58dfe" },
		{ "Y92uqbbTA-Lz4VVRWJWF7_4ZfPU", "0ac721c9b400eedfa3d962b20b9df504282867d62649945cf1dc2d5b5c5bb74f" },
		{ "y9vvk0HktQYEzubLW5EV6pVfJ0Q", "54b97840fe4cfd33474f46e21190bccb8cce4f7cb222870e76de52094b7ac38d" },
		{ "yAQ6Vey9fXQRMz4wv3mZF-B9Qyo", "2a96de8f83029314333d16050fdebd10dd1d52261f88e8231a6227135ca69fe0" },
		{ "YB5HBJNW-yc6pquBoTzjNv3lO2w", "3d15cbff830ff7e259d70def48ac0d9e306b01fda9b0720e79e4f5351bbdb66c" },
		{ "yBlx9l9XdsWk0k6Sd6hJp9lPciw", "7ecd851f9499f26ede3f76bfe9d28ac7f4bf6e44d0af359cbd4eebdd087d1922" },
		{ "yBslwnNpI0-k876qDlImbQ", "63f9d0d7dc2c0bb147248685d0f40a0f78f25994469d123308e4a1df2fc245c4" },
		{ "yd5Eoi7NIwLBabGJhfHsjccK4Vw", "60f153e0aaf331d71ed58d6e796c0a63a7f0a45c83618997a12c103f75ff515c" },
		{ "yDaAG34sZ8n0DyiSlAlywDSLJv4", "4567d5a3453c301504fcd300c947daadbb0d775a62abe49523061afe949ff453" },
		{ "ydL7V4_OoKOxGsq7Dd5LCxr4T28", "bb9bda856642ca23f8a78e95577c610167b13d27aea22a577c03873c10c317e1" },
		{ "YdraA0jri-eNDwhh14_yQJGII_I", "1703fafe51f0616908e5ace9525ac568ce4af6cf0dee26959e43c3f28c4e775d" },
		{ "yE89dutr7mssUywGc1AK5yBjMvA", "e87545a7157ad64d2b69bd8c8c3347ea6a9583535c7e7b048df1943bb4798232" },
		{ "yETqskiqyMjbfidGx-p0kNZ5PoY", "1ef0ece9cb2d68273040d929633ba9645d635a6624ad220ca418965c0ae6e721" },
		{ "yF6Kp_g4NtMos8_DxBEG2RDwNCU", "9eceb7cc1fc8921d6d30bb9f7de528c56a9e523bac9adfb79b1b66849f3a4d1a" },
		{ "Yf8-p4qiNnhJd9waaz6oDlGejYE", "3796da1fcd39d20997ac22ce7b2bcda9e8246c4892dc642dc8a04cf290e4a68b" },
		{ "yfjI2YMAVZxC9Xn--qu5V_64tpU", "3c5ae7b0c0a63df7c3f85f0410f31c6d01007b1d1607fb036ceaf0984b0140fb" },
		{ "Yh4GhKk1WkLGMepNSBvhbFZiSDg", "e9f75c8cafc1cc336014498dba055a3a153160b1b8a64662e9c4ed471ba591bc" },
		{ "yhbMXAwi_Um1vn62BSzaug", "f519c0d8d2ebc478651caec679336879c3675c819e01baa1029dfdb558803672" },
		{ "yHh1lzHqJAT6U5tnRsVB9qcZjMQ", "578e9c1c8d88d45c1441cc94f4e7c70f25a5b3893ba6a56535849fa28dd4414f" },
		{ "yhhODK0t2W_z9hoihMyBq1cT3eQ", "57e4213d9ddd2076ccd938f99fa23b8ddc0ec8ddc9bd8d0986c26d75825f56ae" },
		{ "YIO32u-z2moa52PAagx5poYC9ME", "a85aea3367448cf272b354cfe810e34b0b86e440631a8ad5dc153531db39cb5c" },
		{ "ykDfKkwh4Sj3o_ziigHKrxXK1FI", "5674e43b6c33c016b5196f1515655aa1cbb2c4450c58bd653cd63e8b32550e83" },
		{ "YKLTVPnvyA8TypFqWHLBEOdbVaI", "fcc4f1c4fef849352db567701a80e9a00bc7eba2578f9c55fd57d429230d3bae" },
		{ "YktyrHWrKgczbFHvROxubeFisv8", "27435501ef0f0b4137dbb478b3a2f6e3daf2f67c337bbbcc89ac916f03aa60e4" },
		{ "YlmQCGtBwzf7rvwGLNO095KmQws", "857ae08b415cd3c6dd57301bf84244a622f88abac0319fb51a43a7f87682ff03" },
		{ "Yln0Xc8hrHFBVa06K_dYewWvyDs", "af96b0ddb30d80001cb4f137824dfb71bad7e1314d4068200b2edff320095f0e" },
		{ "yLvQYYY61ejE2Ibn0FZZo1UHHpg", "86e06d49b88e867c365cd583adc3edc1cf21710954ac80201ff428a7baf2b11c" },
		{ "ynCU-R7qWr5f3Rv7EtzPPxmu9ow", "b6b8deea0bb1fddd09867b6f113f5969ccae8e97285c52bf1f7e5b1d06b0baba" },
		{ "Yo5shlj7urleJcnoiaXPldLHyHk", "399ab20d9ca4b6ff5005fa74332acf9aea312f8be87e0d2b8e781d80c171e6f8" },
		{ "yo6Z6fTMX1V_xlZPZpgUSLDnJeA", "3bea3653fb6b2033dbb03f21e38eef2d0a646fccb84d5f98aa0e615653ecaea4" },
		{ "YRmcX7g3MHfMRpzYt5pSxaN1bbk", "7338f027d0b9556415eb7e5cd696c1bbe8ef2c559917cac67f6333406fb2f716" },
		{ "YRQWtyRyP9yoiK7csFdNo3zXfks", "a6f5a8eb8ce8bd1b3500c9f76accaf4c840eefac8066314038b64a4594e9c074" },
		{ "yRuoemreJr0wiNjId7g6ofqC9HE", "841bd3930c5d93ed83ded60bb32cfa7a1fb7845b3d5245a247481dec2088cb30" },
		{ "YrYGa1h0Bt4LYzcxeWsiT-hhvcA", "36261c4c69e868a9edab57ed0ecbb84a4ad71f68c79257d05928a8c595d7c033" },
		{ "ytrjRc_02xcJxHVZlnv3AmIlvD8", "1f401d078290f250b9adcce866dc18300465b3029bccc81eca865158a84fec78" },
		{ "YuT_8MCk3kyBf7VmZVA6Bg.meshx", "0f5179dd2ae4aec5d374b5ebfc9a24d304525f9e679814126341f451823941d2" },
		{ "YVjV5VqlwAzdHF_dZJ_UEtkJQEg", "e99694068646300b893f92cbacd1ffedf21f1d84b6ae4bd9e6fd6a6b4db1a244" },
		{ "yW6UHNJ-d6QKYEwAdQ7RXfx8WWo", "1e76ed295bbe44370579366224e2afe15281b5dbf00043d00ba63937747ebd31" },
		{ "yWaaURw9aaU9vuOzwBwPOAFioz0", "33e59839e76fccd190d0b4feaf8d2f517ece69ca5048fd7ab104be7addd05a3f" },
		{ "yxlnvOgyLVwt5lJ2i8tuZwZooWc", "8b541d936fdb41a049862844983098a990b04cf7c4ecf6e004e5407c4d94707b" },
		{ "yxud-a5UltZWkrTKhg_KLpQFmt0", "395beff56056ec996b3648f245f15ad744cf3f0fe616635ac64a4d749689dc38" },
		{ "YymheUZ_b_-QD4OP7IGnh_psY6Y", "9e76835e00f449fb2a1e6bd9d7b4636a0ffd1f1ff06f63a988291f2cadc24c2d" },
		{ "yzma61L6I3AHnWDd9CiB_E08Coc", "c8116e3cf7b7732c5cfdde483caa77aed777f7879f3c1b35a80e4ca7e3a37810" },
		{ "YzR5IFSOHUFt7UhSJggwQqKsbI8", "9d4400d17d259dd4465ac0e25b206666e29cd17a0eb87ce4ceac1e6806ab6760" },
		{ "Yzrqr-X0STAuHBhTFGlReGM1Tyk", "ef5a2998e90985c9acb949cc5887c1ef01e0d13b50840fbc95e03b58b6024d42" },
		{ "z-jHsC1YvbHxW6qsq1HB4UPT1UE", "4d2d1f33149f4f9373b9c48e873495ab8c0704e3aad139633df5d555e6eccb46" },
		{ "Z-jHtjuIAQdm3a1WQ-w5eS3Ix2w", "2cd1057eecdc83617066481e57ffcf10e8dae08c3be9aa8e4caee54fddf94170" },
		{ "Z04sFWvufd91FW8BpvjB4Mb12eQ", "7847567236df38bd81dd9c77d184a34e73d3a1bbd075d56fea672e2f1b803615" },
		{ "Z0akEjGBTwNZxY7kIIhlg7TBStw", "5f23ecf1aa98a6279d08f4815cfd78f6874ee6ccef1d2a72f3158575386f1b11" },
		{ "Z11pZx3Yg0uEhZpy_gYvGw", "656437b8a81b50e78726448d2c4ba0fdf62a095dbfd7b40d73668210945f8357" },
		{ "z1hhZxPVuBaAKKiQqg0cyidC0tc", "91c0633bef791263b9eb5cb38ca38d2be99152d2847b83fb465d2f5fec7b21cb" },
		{ "Z1ocUUy0uDPtfOjzXwrVvY7FnnY", "f9150568f11dda1cbc148f0662142c0e9c4e33e742f59a99079c8fa1cd2bfa66" },
		{ "z3KDIbdQqR-fVO9UcVVXZxQVsFM", "36a56b72d266178ac612e5d6153a3e1f84cbb1345f13d6da96145d169fa24797" },
		{ "z5P_u0NW_pzn76D3iC74NvpIVNI", "43d8bcba02fdb2f4713ddedb241a704edfab4e809c981d6449cf4e83a7a9c1dd" },
		{ "Z6RNEdC6VjIoFP3XMCN6wYTTXEs", "f1a65814fa92016456784f1157d861971c6a358168dfe8cae7e90cfbb98235fc" },
		{ "Z7GCdrIt_O7FFyuAaHJktnI_-Ok", "746e1ac1e65dbb59009182c8f05cdea7f4b39b060804024657022acdb090b251" },
		{ "z7OjYBh1Nd9XtBrQB9-ublMz36Q", "1ea34652dcb1d3553915878cd4e9b71e861c93d1fbb62c2acb46def5422e4ae3" },
		{ "Z8jIB5tnpukA-kHaYvjk-cw6pMU", "ca0ac41ad66a7804a1607970825b9f6ac2b74bed9ec00c21884bfd502e059f4f" },
		{ "Z9tCwlaBNYXvQUFCoXU82cjQbR4", "7d7cc53b3a20981c5821642f854d657a20c8eaca9d2c3307d9cc6577650899f7" },
		{ "ZbtpZh8A6gdobwEGCZCMnND7rVc", "63ff8cb2ad3bad61959f4a87382045285d19cb109cba3496a224328b03c26b73" },
		{ "zDdtToxnYOizfc3A7E0JqFpTv78", "9780668234810821148e66edca99efdbf471232129a4a227757a3ca6d96813e3" },
		{ "zEGuFE86sbCj067UmyE7KBgIP3g", "9c7d0caee313ee8bf8028c792b93386a9f7fa97f898beac47ee792fb0bdc3f05" },
		{ "zEzmvAs3bvPVq1T4CyiA64zOLNw", "44ecc6c45110717687b9654bd03f67d7e8c3cc284c7b2f61f0e6ed09c3438b1e" },
		{ "ZgOGe_2_DLi2jSLrr-1m9Gp6s50", "4359d44f59ac1376970bd0aef29daa3f79522d035865a5cefc8441b475c66d72" },
		{ "ZHwri8QfhnjToR6-zYNdGGIh72k", "3a091f908e50f757019b5a17a9cb869186657abc963450a8f2f54e0eb0384fc2" },
		{ "zi9ENowYub0qoKrzDrDjkBXskMY", "1199630b94822b8e17c75ddeecbcc456cf7317256a5fee32c7c9d465b46dfb38" },
		{ "ziChvDICEHZ3xGer6B60QBKOKqc", "4f864a1770257bb70fbdfc0e1c3f9427588b5bda297b02771fb5aa3806b06308" },
		{ "ZIInR5-84ytO5N5QPvH6N9dDB1E", "04b454f581e0d60048de23e5272044953a61da032f7d11e092d467047aa1ef1f" },
		{ "ZITDA7k6Fa_ikJmUEV0w6w684_4", "66d063be329df17b960e3260c6f0e5bb6328de0733a47833f37fb0b57d7d8c22" },
		{ "Zj-K6jflzF91CXIOQRNTV0toNPI", "c43589356d2a75f281a837c2432a54c9590b5418cbbee9ebeb6a1a7a92d10101" },
		{ "zJhjVIWWbwp9oU7nYOguJE68bq8", "3b05747267e0cc0625995386b9f709ed82b80cc83934ef1ea550d7cb309d682f" },
		{ "ZkEAjipjE-xQUKPI4vanh5ZotLk", "c9e79f25d2f238432f94ae804a316a0554e2536b9a497400a709b93e2a87a8d2" },
		{ "ZKnZStUhydUFX2ufsZcTnFqakdE", "59050d1e3868e71d26dcd853d71877efeb67fff6d68defc638be173b9cd12c99" },
		{ "zKXiHgQn-DWajSS5dOCcPPpoa7E", "6697708b9ab03ec4f3d18ddf561d2afa41d73a864d65edacdf57ef38622f36c4" },
		{ "zl2-eg2AGCoZvDxan8Y0fhdV-2E", "7cddcac6a5168ed7f0aabdc90668f89d9f3d40ea758c7a381898f0de02776543" },
		{ "zLF_sTz-UkOIqhSMver8Pw", "21b3497130579b1c745ee3249b21e2ddb7df0eb8667f47cd3b21e1037be1feb5" },
		{ "ZLy7MHXXwZSXzEgmv5BlfnKANtM", "7e02db4a8467d8b8d0e3d42570317613e6cc447fbcc61d72295e0e198e79aef1" },
		{ "zMyyGtMxO4c20B0rxvgYUv9ki6A", "afc93f37a5003d18b5b1bd7c5f1e0ef61e0e5d6a147d1ed5ec335f605170994c" },
		{ "zOtmJG_-j4sVZo7LzFtng2Q-O_o", "655bbc357740b803aa078aa353e62964c28320f2cf8e5b8d3ac1f4935dfee283" },
		{ "ZP_6D2KmDt1Pen_xuEyIRWodANg", "5fcb18c0a66e883c48e0c9d593e52d323c0aa5d974567c0cd42153f43ad2fa8c" },
		{ "zP_lRmEjYxpm_QHC3QNav28tIdg", "aa78e5da6f0da360c42c3912bf7ef2520e01ef193bdc09a680c58863f2817d86" },
		{ "ZQhsBvYRpR_qcHkwOce6effTiY4", "b7e736beecd6de09f6df679327004c81f97d8e4ae44587a466a7c4f3cb2bd65a" },
		{ "zqjMZulcLh68SbWT99m7q5ujumQ", "307784aa5cd9f4021aac56cd287b41694c5c5f96ba3163cc6c84d8939382611b" },
		{ "zR7ooGqxSAsaNVmDzsCKJItiKTc", "da889e0dfcb2585ddd2cb060aacc9b755f6f0cb6b1630d4ddf023b50491f181d" },
		{ "ZUbFVZBq45b3IWZO3KAajYcI5-w", "1402efd731e3bbb8610c2f4d03bac9aac7b8e07c1d56f167d1fea914b77871d6" },
		{ "zui22nwnGr39zDjYnT0tP85yzas", "ffca4ebea6f17f9f1e5f098d5f44e7957468701a83fd53700b7b4f9f9379ce24" },
		{ "ZumGb5zNQ87ikBsienz1xoaYC78", "d846728b965bb4f3a8d0c071f12d99b40876efa293409594f68c6c6533725e2e" },
		{ "ZuOiTXSXaKHAzqVxLhsA61rINZY", "e2896a570ee00ff54d9d1a687625eccb68732ba984729e412d18060a9cac6898" },
		{ "zVspToUWEWFoxPsbyYNfl-PXEsU", "5f306c64146a78fcef2274c8ccb22ca5c340bcbe946b638a9a4d184fd05aca19" },
		{ "zwzFcA4sSrRUS-Sx47_RfDj_aCQ", "a871b8a5974ddd19ba2af3fbd9f1d2680a7e976a1a393c86eabaec92d5e15bf2" },
		{ "ZX7KmLtQE1ntU6F0tlz9R_8WFG8", "2fea29acc5f92eacb0e3b12544ce2489ec28ec15ad530d35a7c750c3a395348a" },
		{ "ZyZA4K5ls0ekhNIwmZVSsQ", "6f7a134e96de53bda7790a224e6d58d8fba02f7da0b4241902c30f137ed2f055" },
		{ "zZ-BUqbg4h9ezWR_TvO1sC8MTBg", "ef23d5cb5166880a8f430661a0be3c9565714a30fdff32ebc918b50cfabe75b8" },
		{ "zZ7G3qXNBkKvVHx7G6hhh1_ICwE", "74eff696da8938fc9f933587bced340eff1d02e625de9b762ed3880f94f63149" },
		{ "Z_FbCnpQwkKYabFus2PsGg", "ff0d90c1f1fbe4fddaeea7f576cd0c818850eeb616ecfef867880081137f365f" },
		{ "_-PpG90Bi5WAmBsxL-TcwogmauE", "b68d18c371e06626c425404abc83e567e9afe410eb50c67533b44343a31e5d17" },
		{ "_4BTYObXV2rVSqHb5pVGGcNElkA", "764d8e8e420bc6a740f0b458dabfb571424bb8d896c1cc60d2a5f427ef0f979d" },
		{ "_4TE1cAqzFLoIKd41aFFuOYRugQ", "3fb38d555f28ac3566c675b162d60cff4766d7fdd598412509837696006c2c79" },
		{ "_4V-5uFqScrzc2DPbM9DrliCSm0", "6fd44050f35b43ca2741d62ac5f904135e93c9d85031e4559eb7ae1efdeb2c68" },
		{ "_5dNzEIGk74i2_LiJltgEzZ0EA4", "36450701e395367381af8294368b3d609f7be8b0c7ca206764f0f3ad7afd9e13" },
		{ "_8_YfB7dX063bOQ19qf8Lw", "32b102387a1ccb883e483abc6d1838a747e2ecb9834ad0f85ecbd71ae3aaf90a" },
		{ "_AqOcI3xSUmEcG6WakPlTqIjDOE", "6a35f73fafacbe95a49d5ceefceaa710132fd83483d0ec545976434bc192a69b" },
		{ "_bMBvjH7ayNAiaN53EBfDe68nE8", "0d1e760fb10b7673c78c6c5ef9d521f88661a57daa3448f1b43b6f9fb431dc6e" },
		{ "_bVreic0oTYFpAb-d4rsNDakF58", "b408e6d12a4c530a8ea9e22234ead943ab50115325ca7d426ed3c914273f5181" },
		{ "_cPyIWi37LmQN8V10eAkedHQzGY", "61b65c6637b38ba0397cf5eada867671f8cdd2475efea39e2a3eb20a3dd45d38" },
		{ "_DtpDeO8uu8b_mstbXLZ1t1OmSU", "6ed8f7b6e02aa21e3d88f8c98353aa742cb018d930de5d10aa4b5fee30768a86" },
		{ "_E34UWvGteLZA8cURm01TqrdJrE", "a450bccfa20ea45efeb25862812abba5a113e2ef64e05c29b865843153fa625c" },
		{ "_EwaQKNR4fDTBTwne2JlCtpm8Yk", "6e31de81cd294ae92b2dd99568874a0fbc6638e1683cff7e6220427bce6e5f84" },
		{ "_iav4gjPqLdevyUbjPGWHhL8hdw", "4a7f2d2a2155955ffc9b3f314f2867316683616c82c63eda8095c8d6fe1b3b5e" },
		{ "_l25-SJNOtB782yv2_JMbq1BgHg", "bd151912248df1b9054dcbd740f193c57cd9e0c1998bf5cacd0d12a90cd79e77" },
		{ "_oj0aW4Rke4zYngtVWgItzrGwdI", "d89e64ac40a158c1fb7ebbf74d6e4aa854f42ea115d27b301fd1e3aeb13a20f6" },
		{ "_UiZDPR4dMJzQagOHnANjGnOJnY", "bcf5abe42c1a3f9eda510c6d41b8841b8ff26273ca9a937c8f4c07c94d69f70f" },
		{ "_weV4n72G6_BMVFxNjak_bL_oo8", "1be11a4c5fe1798b60879e66b07ecc97f8da914ac2aced2fcbdd72a1d3d0ef2b" },
		{ "_WfUeQFnwCmfbb0IPMn9v1sMIh4", "d55e971a1d0ada5c2e28c8a97224b863979f5d3a47105639d760aff9562f6cab" },
		{ "_xxm-LoC_1qocH5j91Sc0X87L-w", "f44bd9a66a6cc31f1381470e06efe213f347640687af71cbaf95fea947a8415c" }
	};

	public static string MapLegacySignature(string signature)
	{
		if (map.TryGetValue(signature, out var value))
		{
			return value;
		}
		return signature;
	}
}
public readonly struct CloudVariableIdentity : IEquatable<CloudVariableIdentity>
{
	public readonly string ownerId;

	public readonly string path;

	public CloudVariableIdentity(string ownerId, string path)
	{
		this.ownerId = ownerId;
		this.path = path;
	}

	public override bool Equals(object obj)
	{
		if (obj is CloudVariableIdentity other)
		{
			return Equals(other);
		}
		return false;
	}

	public bool Equals(CloudVariableIdentity other)
	{
		if (ownerId == other.ownerId)
		{
			return path == other.path;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return (-1485666409 * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ownerId)) * -1521134295 + EqualityComparer<string>.Default.GetHashCode(path);
	}

	public override string ToString()
	{
		return "OwnerId: " + ownerId + ", Path: " + path;
	}

	public static bool operator ==(CloudVariableIdentity left, CloudVariableIdentity right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(CloudVariableIdentity left, CloudVariableIdentity right)
	{
		return !(left == right);
	}
}
public delegate void CloudVariableEventHandler(CloudVariableProxy proxy);
public readonly struct TimestampedCloudVariable<T>
{
	public readonly T value;

	public readonly DateTimeOffset? timestamp;

	public TimestampedCloudVariable(T value, DateTimeOffset? timestamp)
	{
		this.value = value;
		this.timestamp = timestamp;
	}
}
public enum CloudVariableState
{
	Uninitialized,
	ReadFromTheCloud,
	ChangedLocally,
	WrittenToCloud,
	Invalid,
	Unregistered
}
public class CloudVariableManager : SkyFrostModule
{
	private Dictionary<CloudVariableIdentity, CloudVariableProxy> _variableProxies = new Dictionary<CloudVariableIdentity, CloudVariableProxy>();

	private HashSet<CloudVariableProxy> _changedVariables = new HashSet<CloudVariableProxy>();

	private VariableReadBatchQuery _readBatch;

	private VariableWriteBatchQuery _writeBatch;

	public ILocalVariableAccessor LocalAccessor { get; private set; }

	internal object Lock { get; private set; } = new object();

	public void SetLocalAccessor(ILocalVariableAccessor accessor)
	{
		if (LocalAccessor != null)
		{
			throw new InvalidOperationException("Local variable accessor has already been assigned");
		}
		LocalAccessor = accessor;
	}

	internal Task<VariableReadResult<CloudVariable, CloudVariableDefinition>> ReadVariable(string ownerId, string path)
	{
		return _readBatch.Request(new VariableReadRequest
		{
			OwnerId = ownerId,
			Path = path
		});
	}

	internal Task<CloudVariable> WriteVariable(CloudVariable variable)
	{
		return _writeBatch.Request(variable);
	}

	internal void RegisterChanged(CloudVariableProxy proxies)
	{
		lock (Lock)
		{
			if (_changedVariables != null)
			{
				_changedVariables.Add(proxies);
			}
		}
	}

	public CloudVariableManager(SkyFrostInterface cloud)
		: base(cloud)
	{
		_readBatch = new VariableReadBatchQuery(this);
		_writeBatch = new VariableWriteBatchQuery(this);
	}

	public void Update()
	{
		try
		{
			lock (Lock)
			{
				if (_changedVariables == null)
				{
					return;
				}
				HashSet<CloudVariableProxy> hashSet = Pool.BorrowHashSet<CloudVariableProxy>();
				foreach (CloudVariableProxy changedVariable in _changedVariables)
				{
					if (changedVariable.WriteToCloud())
					{
						hashSet.Add(changedVariable);
					}
				}
				foreach (CloudVariableProxy item in hashSet)
				{
					_changedVariables.Remove(item);
				}
				Pool.Return(ref hashSet);
			}
		}
		catch (Exception ex)
		{
			UniLog.Error("Exception updating cloud variables:\n" + ex, stackTrace: false);
		}
	}

	public void SignIn()
	{
		_changedVariables = new HashSet<CloudVariableProxy>();
	}

	public async Task SignOut()
	{
		await SaveAllChangedVariables();
	}

	public async Task SaveAllChangedVariables()
	{
		HashSet<CloudVariableProxy> changedVariables;
		lock (Lock)
		{
			changedVariables = _changedVariables;
			_changedVariables = null;
		}
		if (changedVariables == null)
		{
			return;
		}
		List<Task> list = new List<Task>();
		foreach (CloudVariableProxy item in changedVariables)
		{
			list.Add(item.ForceWriteToCloud());
		}
		await Task.WhenAll(list).ConfigureAwait(continueOnCapturedContext: false);
	}

	public CloudVariableProxy RequestProxy(string ownerId, string path)
	{
		CloudVariableIdentity cloudVariableIdentity = new CloudVariableIdentity(ownerId, path);
		lock (Lock)
		{
			if (!_variableProxies.TryGetValue(cloudVariableIdentity, out var value))
			{
				value = new CloudVariableProxy(cloudVariableIdentity, this, LocalAccessor);
				_variableProxies.Add(cloudVariableIdentity, value);
			}
			return value;
		}
	}

	public CloudVariableProxy RegisterListener(string ownerId, string path, CloudVariableEventHandler onChanged)
	{
		if (!CloudVariableHelper.IsValidPath(path))
		{
			throw new ArgumentException("Invalid path: " + path);
		}
		lock (Lock)
		{
			CloudVariableProxy cloudVariableProxy = RequestProxy(ownerId, path);
			cloudVariableProxy.Register(onChanged);
			if (cloudVariableProxy.State != CloudVariableState.Uninitialized && cloudVariableProxy.State != CloudVariableState.Invalid)
			{
				onChanged(cloudVariableProxy);
			}
			return cloudVariableProxy;
		}
	}

	internal bool TryUnregisterProxy(CloudVariableProxy proxy)
	{
		lock (Lock)
		{
			if (!proxy.HasListeners && proxy.State != CloudVariableState.ChangedLocally)
			{
				_variableProxies.Remove(proxy.Identity);
				return true;
			}
			return false;
		}
	}

	public Task<CloudResult<CloudVariableDefinition>> GetDefinition(string ownerId, string subpath)
	{
		string ownerPath = base.Cloud.GetOwnerPath(ownerId);
		return base.Api.GET<CloudVariableDefinition>($"{ownerPath}/{ownerId}/vardefs/{subpath}");
	}

	public Task<CloudResult<CloudVariableDefinition>> UpsertDefinition(CloudVariableDefinition definition)
	{
		string ownerPath = base.Cloud.GetOwnerPath(definition.DefinitionOwnerId);
		return base.Api.PUT<CloudVariableDefinition>($"{ownerPath}/{definition.DefinitionOwnerId}/vardefs/{definition.Subpath}", definition);
	}

	public Task<CloudResult> DeleteDefinition(string ownerId, string subpath)
	{
		string ownerPath = base.Cloud.GetOwnerPath(ownerId);
		return base.Api.DELETE($"{ownerPath}/{ownerId}/vardefs/{subpath}");
	}

	public Task<CloudResult<T>> ReadGlobal<T>(string path, string variableType)
	{
		return Read<T>("GLOBAL", path, variableType);
	}

	public Task<CloudResult<CloudVariable>> Get(string ownerId, string path)
	{
		string resource;
		if (ownerId == "GLOBAL")
		{
			resource = "globalvars/" + path;
		}
		else
		{
			string ownerPath = base.Cloud.GetOwnerPath(ownerId);
			resource = $"{ownerPath}/{ownerId}/vars/{path}";
		}
		return base.Api.GET<CloudVariable>(resource);
	}

	public async Task<CloudResult<TimestampedCloudVariable<T>>> ReadWithTimestamp<T>(string ownerId, string path, string variableType = null)
	{
		CloudResult<CloudVariable> cloudResult = await Get(ownerId, path).ConfigureAwait(continueOnCapturedContext: false);
		if (cloudResult.IsOK && cloudResult.Entity?.Value != null)
		{
			CloudVariableHelper.ParseValue<T>(cloudResult.Entity.Value, variableType, out var value);
			return new CloudResult<TimestampedCloudVariable<T>>(new TimestampedCloudVariable<T>(value, cloudResult.Entity.Timestamp), cloudResult.State, cloudResult.Headers, cloudResult.RequestAttempts, cloudResult.Content);
		}
		return new CloudResult<TimestampedCloudVariable<T>>(default(TimestampedCloudVariable<T>), cloudResult.State, cloudResult.Headers, cloudResult.RequestAttempts, cloudResult.Content);
	}

	public async Task<CloudResult<T>> Read<T>(string ownerId, string path, string variableType = null)
	{
		CloudResult<TimestampedCloudVariable<T>> cloudResult = await ReadWithTimestamp<T>(ownerId, path, variableType).ConfigureAwait(continueOnCapturedContext: false);
		return new CloudResult<T>(cloudResult.Entity.value, cloudResult);
	}

	public Task<CloudResult<List<VariableReadResult<CloudVariable, CloudVariableDefinition>>>> ReadBatch(List<VariableReadRequest> batch)
	{
		return base.Api.POST<List<VariableReadResult<CloudVariable, CloudVariableDefinition>>>("readvars", batch);
	}

	public Task<CloudResult<List<CloudVariable>>> GetAllByOwner(string ownerId)
	{
		return base.Api.GET<List<CloudVariable>>(base.Cloud.GetOwnerPath(ownerId) + "/" + ownerId + "/vars");
	}

	public Task<CloudResult<List<CloudVariableDefinition>>> ListDefinitions(string ownerId)
	{
		return base.Api.GET<List<CloudVariableDefinition>>(base.Cloud.GetOwnerPath(ownerId) + "/" + ownerId + "/vardefs");
	}

	public Task<CloudResult<List<CloudVariable>>> WriteBatch(List<CloudVariable> batch)
	{
		return base.Api.POST<List<CloudVariable>>("writevars", batch);
	}

	public Task<CloudResult> Write<T>(string ownerId, string path, T value, string variableType = null)
	{
		string ownerPath = base.Cloud.GetOwnerPath(ownerId);
		CloudVariable cloudVariable = new CloudVariable();
		cloudVariable.Value = CloudVariableHelper.EncodeValue(value, variableType);
		return base.Api.PUT($"{ownerPath}/{ownerId}/vars/{path}", cloudVariable);
	}

	public Task<CloudResult> Upsert(CloudVariable variable)
	{
		string ownerPath = base.Cloud.GetOwnerPath(variable.VariableOwnerId);
		return base.Api.PUT($"{ownerPath}/{variable.VariableOwnerId}/vars/{variable.Path}", variable);
	}

	public Task<CloudResult> Delete(string ownerId, string path)
	{
		string ownerPath = base.Cloud.GetOwnerPath(ownerId);
		return base.Api.DELETE(ownerPath + "/vars/" + path);
	}

	public Task<CloudResult<T>> ReadOwners<T>(string path, string variableType = null)
	{
		return Read<T>(base.CurrentUserID, path, variableType);
	}

	public Task<CloudResult<TimestampedCloudVariable<T>>> ReadOwnersWithTimestamp<T>(string path, string variableType = null)
	{
		return ReadWithTimestamp<T>(base.CurrentUserID, path, variableType);
	}

	public Task<CloudResult> WriteOwners<T>(string path, T value, string variableType = null)
	{
		return Write(base.CurrentUserID, path, value, variableType);
	}

	public Task<CloudResult> Delete(string path)
	{
		return Delete(base.CurrentUserID, path);
	}
}
public interface ILocalVariableAccessor
{
	Task<CloudVariable> ReadLocalVariable(string path, string defaultValue);

	Task WriteLocalVariable(CloudVariable variable);
}
public class CloudVariableProxy
{
	public const double WRITE_DELAY_SECONDS = 30.0;

	public const double REFRESH_INTERVAL_SECONDS = 300.0;

	public const double UNREGISTER_DELAY_SECONDS = 300.0;

	public CloudVariableDefinition _definition;

	private CloudVariable _variable;

	private bool _isLocalVariable;

	private string _definitionOwnerId;

	private string _definitionSubpath;

	private CloudVariableManager _manager;

	private ILocalVariableAccessor _localVariableAccessor;

	private Task _readTask;

	private CancellationTokenSource _readCancel;

	private Task _writeTask;

	private CancellationTokenSource _unregisterCancel;

	private object _lock = new object();

	public CloudVariableState State { get; private set; }

	public bool HasValidValue
	{
		get
		{
			if (State != CloudVariableState.Uninitialized)
			{
				return State != CloudVariableState.Invalid;
			}
			return false;
		}
	}

	public DateTime LastCloudWrite { get; private set; }

	public DateTime LastCloudRead { get; private set; }

	public CloudVariableIdentity Identity { get; private set; }

	public bool HasListeners => this._valueChanged != null;

	public string RawValue => _variable?.Value;

	public bool IsDefinitionOwner
	{
		get
		{
			if (_definition == null)
			{
				return false;
			}
			if (IdUtil.GetOwnerType(_definition.DefinitionOwnerId) == OwnerType.User)
			{
				return _definition.DefinitionOwnerId == Cloud.Session.CurrentUserID;
			}
			return Cloud.Groups.IsCurrentUserMemberOfGroup(_definition.DefinitionOwnerId);
		}
	}

	public bool IsVariableOwner => IdUtil.GetOwnerType(Identity.ownerId) switch
	{
		OwnerType.Machine => true, 
		OwnerType.User => Identity.ownerId == Cloud.Session.CurrentUserID, 
		_ => Cloud.Groups.IsCurrentUserMemberOfGroup(Identity.ownerId), 
	};

	public bool PublicRead { get; private set; }

	public bool PublicWrite { get; private set; }

	public bool PrivateWrite { get; private set; }

	public SkyFrostInterface Cloud => _manager.Cloud;

	private event CloudVariableEventHandler _valueChanged;

	public CloudVariableProxy(CloudVariableIdentity identity, CloudVariableManager manager, ILocalVariableAccessor localReader)
	{
		Identity = identity;
		_manager = manager;
		_localVariableAccessor = localReader;
		_isLocalVariable = IdUtil.GetOwnerType(Identity.ownerId) == OwnerType.Machine;
		if (_isLocalVariable && _localVariableAccessor == null)
		{
			throw new InvalidOperationException("Cannot request a local cloud variable without passing valid local reader");
		}
		int num = identity.path.IndexOf(".");
		_definitionOwnerId = identity.path.Substring(0, num);
		_definitionSubpath = identity.path.Substring(num + 1);
		State = CloudVariableState.Uninitialized;
		ScheduleUnregistration();
		Task.Run(async delegate
		{
			_ = 1;
			do
			{
				try
				{
					await Refresh().ConfigureAwait(continueOnCapturedContext: false);
					await Task.Delay(TimeSpan.FromSeconds(300.0));
				}
				catch (Exception value)
				{
					UniLog.Error($"Exception when running refresh for cloud variable proxy {Identity}\n{value}");
				}
			}
			while (State != CloudVariableState.Unregistered && State != CloudVariableState.Invalid);
		});
	}

	public void Register(CloudVariableEventHandler onChanged)
	{
		if (State == CloudVariableState.Unregistered)
		{
			throw new InvalidOperationException("Proxy has been unregistered!");
		}
		bool flag = false;
		if (_unregisterCancel != null && !IsVariableOwner)
		{
			flag = true;
		}
		_unregisterCancel?.Cancel();
		_unregisterCancel = null;
		_valueChanged += onChanged;
		if (flag)
		{
			Task.Run((Func<Task?>)Refresh);
		}
	}

	public void Unregister(CloudVariableEventHandler onChanged)
	{
		if (State == CloudVariableState.Unregistered)
		{
			throw new InvalidOperationException("Proxy has been unregistered!");
		}
		_valueChanged -= onChanged;
		if (this._valueChanged == null)
		{
			ScheduleUnregistration();
		}
	}

	private void ScheduleUnregistration()
	{
		CancellationTokenSource cancellationSource = new CancellationTokenSource();
		_unregisterCancel = cancellationSource;
		Task.Run(async delegate
		{
			do
			{
				await Task.Delay(TimeSpan.FromSeconds(300.0), cancellationSource.Token).ConfigureAwait(continueOnCapturedContext: false);
				if (cancellationSource.IsCancellationRequested)
				{
					break;
				}
				if (_manager.TryUnregisterProxy(this))
				{
					State = CloudVariableState.Unregistered;
				}
			}
			while (!HasListeners);
		});
	}

	public bool WriteToCloud()
	{
		if (State == CloudVariableState.Unregistered)
		{
			throw new InvalidOperationException("Proxy has been unregistered!");
		}
		if (State != CloudVariableState.ChangedLocally)
		{
			throw new Exception($"Variable isn't changed locally! State: {State}, Identity: {Identity}");
		}
		if ((DateTime.UtcNow - LastCloudWrite).TotalSeconds < 30.0)
		{
			return false;
		}
		if (_writeTask != null)
		{
			return false;
		}
		lock (_lock)
		{
			State = CloudVariableState.WrittenToCloud;
			LastCloudWrite = DateTime.UtcNow;
			_writeTask = Task.Run(async delegate
			{
				_ = 1;
				try
				{
					if (!_isLocalVariable)
					{
						await _manager.WriteVariable(_variable).ConfigureAwait(continueOnCapturedContext: false);
					}
					else
					{
						await _localVariableAccessor.WriteLocalVariable(_variable).ConfigureAwait(continueOnCapturedContext: false);
					}
					lock (_lock)
					{
						_readCancel?.Cancel();
						_readCancel = null;
					}
				}
				finally
				{
					lock (_lock)
					{
						_writeTask = null;
					}
				}
			});
		}
		return true;
	}

	public async Task ForceWriteToCloud()
	{
		if (State != CloudVariableState.ChangedLocally)
		{
			throw new Exception($"Variable isn't changed locally! State: {State}, Identity: {Identity}");
		}
		Task writeTask = _writeTask;
		if (writeTask != null)
		{
			await writeTask.ConfigureAwait(continueOnCapturedContext: false);
		}
		if (_isLocalVariable)
		{
			await _localVariableAccessor.WriteLocalVariable(_variable).ConfigureAwait(continueOnCapturedContext: false);
		}
		else
		{
			await _manager.WriteVariable(_variable).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	public Task Refresh()
	{
		UniLog.Log($"Running refresh on: {Identity}");
		Task readTask = _readTask;
		if (readTask != null)
		{
			return readTask;
		}
		if (State == CloudVariableState.ChangedLocally)
		{
			return Task.CompletedTask;
		}
		if (_writeTask != null)
		{
			return Task.CompletedTask;
		}
		lock (_lock)
		{
			if (_readTask != null)
			{
				return _readTask;
			}
			_readCancel = new CancellationTokenSource();
			CancellationToken cancel = _readCancel.Token;
			return _readTask = Task.Run(async delegate
			{
				_ = 3;
				try
				{
					VariableReadResult<CloudVariable, CloudVariableDefinition> result;
					if (_isLocalVariable)
					{
						CloudResult<CloudVariableDefinition> definition = await Cloud.Variables.GetDefinition(_definitionOwnerId, _definitionSubpath).ConfigureAwait(continueOnCapturedContext: false);
						if (definition.IsOK)
						{
							CloudVariable variable = await _localVariableAccessor.ReadLocalVariable(Identity.path, definition.Entity.DefaultValue).ConfigureAwait(continueOnCapturedContext: false);
							result = new VariableReadResult<CloudVariable, CloudVariableDefinition>
							{
								Definition = definition.Entity,
								Variable = variable
							};
						}
						else
						{
							result = null;
						}
					}
					else
					{
						result = await _manager.ReadVariable(Identity.ownerId, Identity.path).ConfigureAwait(continueOnCapturedContext: false);
					}
					while (!_isLocalVariable && !Cloud.Contacts.ContactListLoaded)
					{
						await Task.Delay(TimeSpan.FromSeconds(0.5));
					}
					lock (_lock)
					{
						if (cancel.IsCancellationRequested || State == CloudVariableState.ChangedLocally)
						{
							return;
						}
						if (result == null)
						{
							State = CloudVariableState.Invalid;
						}
						else
						{
							bool num = _variable == null || _variable?.Value != result.Variable?.Value;
							_definition = result.Definition;
							_variable = result.Variable;
							PublicRead = CanAccessInPublic(_definition.ReadPermissions);
							PublicWrite = CanAccessInPublic(_definition.WritePermissions);
							PrivateWrite = CanAccessInPrivate(_definition.WritePermissions);
							LastCloudRead = DateTime.UtcNow;
							State = CloudVariableState.ReadFromTheCloud;
							if (num)
							{
								RunChangedEvent();
							}
						}
					}
				}
				finally
				{
					lock (_lock)
					{
						_readTask = null;
					}
				}
			});
		}
	}

	private bool CanAccessInPublic(List<string> permissions)
	{
		foreach (string permission in permissions)
		{
			if (CloudVariableHelper.AllowsPublicAccess(permission) && CanAccessInPrivate(permission))
			{
				return true;
			}
		}
		return false;
	}

	private bool CanAccessInPrivate(List<string> permissions)
	{
		foreach (string permission in permissions)
		{
			if (CanAccessInPrivate(permission))
			{
				return true;
			}
		}
		return false;
	}

	private bool CanAccessInPrivate(string perm)
	{
		if (perm == "anyone")
		{
			return true;
		}
		if (CloudVariableHelper.RequiresDefinitionOwner(perm) && !IsDefinitionOwner)
		{
			return false;
		}
		if (CloudVariableHelper.RequiresVariableOwner(perm) && !IsVariableOwner)
		{
			return false;
		}
		if (CloudVariableHelper.TargetDefinitionOwnerOnly(perm) && Identity.ownerId != _definition.DefinitionOwnerId)
		{
			return false;
		}
		if (CloudVariableHelper.TargetContactsOnly(perm) && !Cloud.Contacts.IsContact(Identity.ownerId, mutuallyAccepted: true))
		{
			return false;
		}
		return true;
	}

	public T ReadValue<T>()
	{
		if (_variable == null)
		{
			return default(T);
		}
		if (CloudVariableHelper.ParseValue<T>(_variable.Value, _definition.VariableType, out var value))
		{
			return value;
		}
		return default(T);
	}

	public bool SetValue(string value)
	{
		if (State == CloudVariableState.Unregistered)
		{
			throw new InvalidOperationException("Proxy has been unregistered!");
		}
		if (_definition == null)
		{
			return false;
		}
		if (!CloudVariableHelper.IsValidValue(_definition.VariableType, value))
		{
			return false;
		}
		if (_variable.Value == value)
		{
			return true;
		}
		_variable.Value = value;
		_readCancel?.Cancel();
		_readCancel = null;
		State = CloudVariableState.ChangedLocally;
		_manager.RegisterChanged(this);
		RunChangedEvent();
		return true;
	}

	public bool SetValue<T>(T value)
	{
		return SetValue(CloudVariableHelper.EncodeValue(value, _definition.VariableType));
	}

	private void RunChangedEvent()
	{
		try
		{
			this._valueChanged?.Invoke(this);
		}
		catch (Exception value)
		{
			UniLog.Log($"Exception running ValueChanged:\n{value}");
		}
	}
}
public class AppHub : IHubServer, IHubDebugClient
{
	private CancellationTokenSource _cancellation;

	public HubConnection Hub { get; private set; }

	private CancellationToken Token => _cancellation.Token;

	private bool IsClosed => _cancellation.IsCancellationRequested;

	public AppHub(HubConnection hub)
	{
		Hub = hub;
		_cancellation = new CancellationTokenSource();
	}

	public void Disconnect()
	{
		_cancellation.Cancel();
	}

	private async ValueTask EnsureConnectedHub()
	{
		int attempts = 10;
		while (Hub.State != HubConnectionState.Connected && attempts > 0 && !IsClosed)
		{
			await Task.Delay(TimeSpan.FromSeconds(1L)).ConfigureAwait(continueOnCapturedContext: false);
			attempts--;
		}
	}

	public Task Pong(int index)
	{
		UniLog.Log($"Pong {index}!");
		return Task.CompletedTask;
	}

	public Task Debug(string message)
	{
		UniLog.Log("Cloud Debug: " + message);
		return Task.CompletedTask;
	}

	public async Task Ping(int index)
	{
		await EnsureConnectedHub().ConfigureAwait(continueOnCapturedContext: false);
		await Hub.SendAsync("Ping", index, Token).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task SendMessage(Message message)
	{
		await EnsureConnectedHub().ConfigureAwait(continueOnCapturedContext: false);
		UniLog.Log($"SIGNALR: SendMessage - {message}");
		try
		{
			await Hub.SendAsync("SendMessage", message, Token).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex)
		{
			if (IsClosed)
			{
				throw new TaskCanceledException();
			}
			UniLog.Error("Exception running SendMessage:\n" + ex, stackTrace: false);
			throw;
		}
	}

	public async Task MarkMessagesRead(MarkReadBatch markReadBatch)
	{
		await EnsureConnectedHub().ConfigureAwait(continueOnCapturedContext: false);
		UniLog.Log($"SIGNALR: MarkMessagesRead - {markReadBatch}");
		try
		{
			await Hub.SendAsync("MarkMessagesRead", markReadBatch, Token).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex)
		{
			if (IsClosed)
			{
				throw new TaskCanceledException();
			}
			UniLog.Error("Exception running MarkMessagesRead:\n" + ex, stackTrace: false);
			throw;
		}
	}

	public async Task BroadcastStatus(UserStatus status, BroadcastTarget target)
	{
		await EnsureConnectedHub().ConfigureAwait(continueOnCapturedContext: false);
		UniLog.Log($"SIGNALR: BroadcastStatus - {status} to {target}");
		try
		{
			await Hub.SendAsync("BroadcastStatus", status, target, Token).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex)
		{
			if (IsClosed)
			{
				throw new TaskCanceledException();
			}
			UniLog.Error("Exception running BroadcastStatus:\n" + ex, stackTrace: false);
			throw;
		}
	}

	public async Task<bool> UpdateContact(Contact contact)
	{
		await EnsureConnectedHub().ConfigureAwait(continueOnCapturedContext: false);
		UniLog.Log($"SIGNALR: UpdateContact - {contact}");
		try
		{
			return await Hub.InvokeAsync<bool>("UpdateContact", contact, Token).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex)
		{
			if (IsClosed)
			{
				throw new TaskCanceledException();
			}
			UniLog.Error("Exception running UpdateContact:\n" + ex, stackTrace: false);
			throw;
		}
	}

	public async Task ListenOnContact(string contactId)
	{
		await EnsureConnectedHub().ConfigureAwait(continueOnCapturedContext: false);
		UniLog.Log("SIGNALR: ListenOnContact - " + contactId);
		try
		{
			await Hub.SendAsync("ListenOnContact", contactId, Token).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex)
		{
			if (IsClosed)
			{
				throw new TaskCanceledException();
			}
			UniLog.Error("Exception running ListenOnContact:\n" + ex, stackTrace: false);
			throw;
		}
	}

	public async Task ListenOnKey(string broadcastKey)
	{
		await EnsureConnectedHub().ConfigureAwait(continueOnCapturedContext: false);
		UniLog.Log("SIGNALR: ListenOnKey - " + broadcastKey);
		try
		{
			await Hub.SendAsync("ListenOnKey", broadcastKey, Token).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex)
		{
			if (IsClosed)
			{
				throw new TaskCanceledException();
			}
			UniLog.Error("Exception running ListenOnKey:\n" + ex, stackTrace: false);
			throw;
		}
	}

	public async Task RequestStatus(string userId = null, bool invisible = false)
	{
		await EnsureConnectedHub().ConfigureAwait(continueOnCapturedContext: false);
		UniLog.Log($"SIGNALR: RequestStatus - {userId} - Invisible: {invisible}");
		try
		{
			await Hub.SendAsync("RequestStatus", userId, invisible, Token).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex)
		{
			if (IsClosed)
			{
				throw new TaskCanceledException();
			}
			UniLog.Error("Exception running RequestStatus:\n" + ex, stackTrace: false);
			throw;
		}
	}

	public async Task<StatusInitializationResult> InitializeStatus()
	{
		await EnsureConnectedHub().ConfigureAwait(continueOnCapturedContext: false);
		UniLog.Log("SIGNALR: InitializeStatus");
		try
		{
			return await Hub.InvokeAsync<StatusInitializationResult>("InitializeStatus", Token).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception)
		{
			if (IsClosed)
			{
				throw new TaskCanceledException();
			}
			throw;
		}
	}

	public async IAsyncEnumerable<Contact> InitializeContacts(CancellationToken cancellationToken)
	{
		await EnsureConnectedHub().ConfigureAwait(continueOnCapturedContext: false);
		UniLog.Log("SIGNALR: InitializeContacts");
		await foreach (Contact item in Hub.StreamAsync<Contact>("InitializeContacts", cancellationToken).ConfigureAwait(continueOnCapturedContext: false))
		{
			yield return item;
		}
	}

	public async Task BroadcastSession(SessionInfo session, BroadcastTarget target)
	{
		await EnsureConnectedHub().ConfigureAwait(continueOnCapturedContext: false);
		UniLog.Log($"SIGNALR: BroadcastSession {session} to {target}");
		try
		{
			await Hub.SendAsync("BroadcastSession", session, target, Token).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex)
		{
			if (IsClosed)
			{
				throw new TaskCanceledException();
			}
			UniLog.Error("Exception running BroadcastSession:\n" + ex, stackTrace: false);
			throw;
		}
	}

	public async Task BroadcastSessionEnded(string sessionId, DateTime timestamp, BroadcastTarget target)
	{
		await EnsureConnectedHub().ConfigureAwait(continueOnCapturedContext: false);
		UniLog.Log($"SIGNALR: BroadcastSessionEnded {sessionId} to {target}");
		try
		{
			await Hub.SendAsync("BroadcastSessionEnded", sessionId, timestamp, target, Token).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex)
		{
			if (IsClosed)
			{
				throw new TaskCanceledException();
			}
			UniLog.Error("Exception running BroadcastSession:\n" + ex, stackTrace: false);
			throw;
		}
	}

	public async Task ListenForLNLPokeRequests(string universeId, string connectionUrl)
	{
		await EnsureConnectedHub().ConfigureAwait(continueOnCapturedContext: false);
		UniLog.Log("SIGNALR: ListenForLNLPokeRequests " + universeId + " - " + connectionUrl);
		try
		{
			await Hub.SendAsync("ListenForLNLPokeRequests", universeId, connectionUrl, Token).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex)
		{
			if (IsClosed)
			{
				throw new TaskCanceledException();
			}
			UniLog.Error("Exception running ListenForLNLPokeRequests:\n" + ex, stackTrace: false);
			throw;
		}
	}

	public async Task RequestLNLPoke(string universeId, string connectionUrl, string address, int port)
	{
		await EnsureConnectedHub().ConfigureAwait(continueOnCapturedContext: false);
		UniLog.Log($"SIGNALR: RequestLNLPoke {universeId} - {connectionUrl} to {address}:{port}");
		try
		{
			await Hub.SendAsync("RequestLNLPoke", universeId, connectionUrl, address, port, Token).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception ex)
		{
			if (IsClosed)
			{
				throw new TaskCanceledException();
			}
			UniLog.Error("Exception running RequestLNLPoke:\n" + ex, stackTrace: false);
			throw;
		}
	}
}
public class HubStatusController : IHubStatusClient
{
	private CancellationTokenSource _initializationCancellation;

	private int exceptionCount;

	public bool Initialized { get; private set; }

	public bool Initializing => _initializationCancellation != null;

	public SkyFrostInterface Cloud { get; private set; }

	public event Action<string> OnUserStatusRequested;

	public event Action<string, string> OnKeyListenerAdded;

	public HubStatusController(SkyFrostInterface cloud)
	{
		Cloud = cloud;
	}

	internal void ResetInitializationStatus()
	{
		_initializationCancellation?.Cancel();
		_initializationCancellation = null;
		Initialized = false;
	}

	public void Initialize(CancellationToken token)
	{
		_initializationCancellation = new CancellationTokenSource();
		CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, _initializationCancellation.Token);
		RunInitialization(cancellationTokenSource.Token);
	}

	private void RunInitialization(CancellationToken cancellation)
	{
		Task.Run(async delegate
		{
			if (!cancellation.IsCancellationRequested && Cloud.Session.CurrentUser != null)
			{
				AppHub _client = Cloud.HubClient;
				try
				{
					int count = 0;
					int requests = 0;
					UniLog.Log("Initializing contacts");
					await foreach (Contact item in _client.InitializeContacts(cancellation).ConfigureAwait(continueOnCapturedContext: false))
					{
						if (cancellation.IsCancellationRequested)
						{
							return;
						}
						int num = count + 1;
						count = num;
						if (num % 50 == 0)
						{
							UniLog.Log($"Fetched {count} contacts...");
						}
						Cloud.Contacts.LoadContact(item, ref requests);
					}
					Cloud.Contacts.FinalizeLoading(requests);
					UniLog.Log("Status Initialized. Contact Count: " + count);
					Initialized = true;
					while (Cloud.Status.LoadingOnlineStatus)
					{
						await Task.Delay(100).ConfigureAwait(continueOnCapturedContext: false);
					}
					await _client.RequestStatus(null, Cloud.Status.OnlineStatus == OnlineStatus.Invisible).ConfigureAwait(continueOnCapturedContext: false);
				}
				catch (Exception ex)
				{
					if (cancellation.IsCancellationRequested)
					{
						return;
					}
					exceptionCount++;
					if (exceptionCount < 5)
					{
						if (ex is HubException)
						{
							UniLog.Error($"Exception when initializing status hub:\nMessage: {ex.Message}\nSource: {ex.Source}\nHelpLink: {ex.HelpLink}\nConnectionId: {_client?.Hub?.ConnectionId}\nConnectionState: {_client?.Hub?.State}\nServerTimeout: {_client?.Hub?.ServerTimeout}\nKeepAliveInterval: {_client?.Hub?.KeepAliveInterval}\nHandshakeTimeout: {_client?.Hub?.HandshakeTimeout}\nInnerException: {ex.InnerException}");
						}
						else
						{
							UniLog.Error("Exception when initializing status hub:\n" + ex.PrintAllInnerExceptions());
						}
					}
					await Task.Delay(TimeSpan.FromSeconds(0.25)).ConfigureAwait(continueOnCapturedContext: false);
					RunInitialization(cancellation);
				}
			}
		});
	}

	public async Task SignOut()
	{
		ResetInitializationStatus();
	}

	public async Task ReceiveStatusUpdate(UserStatus status)
	{
		try
		{
			await WaitForInitialized().ConfigureAwait(continueOnCapturedContext: false);
			Cloud.Contacts.ContactStatusUpdated(status);
		}
		catch (Exception value)
		{
			UniLog.Error($"Exception when ReceivingStatusUpdate:\n{value}", stackTrace: false);
		}
	}

	public async Task SendStatusToUser(string userId)
	{
		try
		{
			await WaitForInitialized().ConfigureAwait(continueOnCapturedContext: false);
			if (Cloud.Contacts.IsContact(userId))
			{
				Cloud.Status.SendStatusToUser(userId);
				this.OnUserStatusRequested?.Invoke(userId);
			}
		}
		catch (Exception ex)
		{
			UniLog.Error("Exception when sending status to user " + userId + ":\n" + ex);
		}
	}

	public async Task ContactAddedOrUpdated(Contact contact)
	{
		await WaitForInitialized().ConfigureAwait(continueOnCapturedContext: false);
		Cloud.Contacts.ContactAddedOrUpdated(contact);
	}

	public async Task KeyListenerAdded(string broadcastKey, string connectionId)
	{
		try
		{
			UniLog.Log("KeyListenerAdded: " + broadcastKey + " - " + connectionId);
			await WaitForInitialized().ConfigureAwait(continueOnCapturedContext: false);
			this.OnKeyListenerAdded?.Invoke(broadcastKey, connectionId);
		}
		catch (Exception ex)
		{
			UniLog.Error($"Exception when processing new broadcast key listener {broadcastKey} - connectionId: {connectionId}:\n" + ex);
		}
	}

	private async ValueTask WaitForInitialized()
	{
		while (!Initialized)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(250L)).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	public Task ReceiveSessionUpdate(SessionInfo info)
	{
		try
		{
			Cloud.Sessions.UpdateSessionInfo(info);
		}
		catch (Exception ex)
		{
			UniLog.Error("Exception when receiving session update:\n" + ex);
		}
		return Task.CompletedTask;
	}

	public Task RemoveSession(string sessionId, DateTime timestamp)
	{
		try
		{
			Cloud.Sessions.RemoveSession(sessionId, timestamp);
		}
		catch (Exception ex)
		{
			UniLog.Error("Exception when removing session:\n" + ex);
		}
		return Task.CompletedTask;
	}
}
/// <summary>
/// This interface collects all the individual clients that use the SignalR service
/// </summary>
public interface IHubClient : IHubDebugClient, IHubMessagingClient, IModerationClient, IHubStatusClient, IHubNetworkingClient
{
}
/// <summary>
/// This interface is used for debugging the system and isn't normally used in production.
/// </summary>
public interface IHubDebugClient
{
	/// <summary>
	/// Is called as response to the Ping call. Used for debugging purposes
	/// </summary>
	/// <param name="index">Matches the index passed to the Ping call</param>
	/// <returns></returns>
	Task Pong(int index);

	/// <summary>
	/// Used for debugging. Called on client to receive a debug message.
	/// </summary>
	/// <param name="message">Debug message that is being received</param>
	/// <returns></returns>
	Task Debug(string message);
}
/// <summary>
/// This interface handles the messaging system - sending and receiving messages
/// </summary>
public interface IHubMessagingClient
{
	/// <summary>
	/// Called on client when they should receive a message from another user.
	/// </summary>
	/// <param name="message">The message to be received</param>
	/// <returns></returns>
	Task ReceiveMessage(Message message);

	/// <summary>
	/// Called on client when they send a message. This is so the message can be added to history on other signed sessions.
	/// </summary>
	/// <param name="message">The message that was sent</param>
	/// <returns></returns>
	Task MessageSent(Message message);

	/// <summary>
	/// Called on client when one or more messages have been read by users
	/// </summary>
	/// <param name="readBatch">Batch of the messages that were read (can be one or more)</param>
	/// <returns></returns>
	Task MessagesRead(ReadMessageBatch readBatch);
}
/// <summary>
/// This interface is for receiving any global moderation messages that clients might need to react to.
/// </summary>
public interface IModerationClient
{
	/// <summary>
	/// Called on client when an user has been public banned
	/// </summary>
	/// <param name="userId"></param>
	/// <returns></returns>
	Task UserPublicBanned(string userId);

	/// <summary>
	/// Called on client when user has been mute banned
	/// </summary>
	/// <param name="userId"></param>
	/// <returns></returns>
	Task UserMuteBanned(string userId);

	/// <summary>
	/// Called on client when user has been spectator banned
	/// </summary>
	/// <param name="userId"></param>
	/// <returns></returns>
	Task UserSpectatorBanned(string userId);
}
/// <summary>
/// This interface is used for clients to exchange their current status - profile, online status, sessions and such
/// </summary>
public interface IHubStatusClient
{
	/// <summary>
	/// Called on client when user's status has been updated
	/// </summary>
	/// <param name="status">The updated user status</param>
	/// <returns></returns>
	Task ReceiveStatusUpdate(UserStatus status);

	/// <summary>
	/// Called on client when a given user should receive full status update (e.g. they just logged in)
	/// </summary>
	/// <param name="userId">User who to send the status update to</param>
	/// <returns></returns>
	Task SendStatusToUser(string userId);

	/// <summary>
	/// Called on client when one of their contacts has been updated. Used to sync status between active sessions.
	/// E.g. when user adds a new contact or accepts/ignores them. Also called when they receive a contact request.
	/// </summary>
	/// <param name="contact">New or Updated contact</param>
	/// <returns></returns>
	Task ContactAddedOrUpdated(Contact contact);

	/// <summary>
	/// Called on the client when the session info has been added or created.
	/// </summary>
	/// <param name="info">Data of the new/updated session</param>
	/// <returns></returns>
	Task ReceiveSessionUpdate(SessionInfo info);

	/// <summary>
	/// Called when session is to be removed (e.g. when it ends)
	/// </summary>
	/// <param name="sessionId">The id of the session that is to be removed</param>
	/// <param name="timestamp">Timestamp of the removal</param>
	/// <returns></returns>
	Task RemoveSession(string sessionId, DateTime timestamp);

	/// <summary>
	/// Called when a new listener is registered on particular broadcast key
	/// </summary>
	/// <param name="broadcastKey"></param>
	/// <param name="connectionId"></param>
	/// <returns></returns>
	Task KeyListenerAdded(string broadcastKey, string connectionId);
}
public interface IHubNetworkingClient
{
	/// <summary>
	/// This instructs the target to poke the target over LNL. This can be useful to help establish LNL
	/// connections, particularly within corporate networks.
	/// </summary>
	/// <param name="connectionUrl">Which connection is expected to poke</param>
	/// <param name="address">The target address to poke</param>
	/// <param name="port">Target port to poke</param>
	/// <returns></returns>
	Task PokeOverLNL(string connectionUrl, string address, int port);
}
public interface IHubServer
{
	Task Ping(int index);

	Task SendMessage(Message message);

	Task MarkMessagesRead(MarkReadBatch batch);

	[Obsolete]
	Task<StatusInitializationResult> InitializeStatus();

	IAsyncEnumerable<Contact> InitializeContacts(CancellationToken cancellationToken);

	Task ListenOnContact(string contactId);

	Task ListenOnKey(string broadcastKey);

	Task BroadcastStatus(UserStatus status, BroadcastTarget target);

	Task<bool> UpdateContact(Contact contact);

	Task RequestStatus(string userId = null, bool invisible = false);

	Task BroadcastSession(SessionInfo session, BroadcastTarget target);

	Task BroadcastSessionEnded(string sessionId, DateTime timestamp, BroadcastTarget target);

	Task ListenForLNLPokeRequests(string universeId, string connectionUrl);

	Task RequestLNLPoke(string universeId, string connectionUrl, string address, int port);
}
/// <summary>
/// Used to expose Host configuration to SkyFrost.Base. 
/// </summary>
/// <seealso cref="T:SkyFrost.Base.IUserStatusSource" />
public interface ISessionListingSettings
{
	bool HasUniverse { get; }

	/// <summary>
	/// A universe ID to filter the session list by
	/// </summary>
	string UniverseId { get; }

	bool AcceptSession(SessionInfo sessionInfo);
}
public interface IUserStatusSource
{
	bool LoadingOnlineStatus { get; }

	string AppVersion { get; }

	bool IsMobile { get; }

	UserSessionType SessionType { get; }

	bool IsUserPresent { get; }

	DateTime LastPresenceTimestamp { get; }

	OutputDevice? OutputDevice { get; }

	DateTime LastSessionChangeTimestamp { get; }

	void OnlineStatusChanged(OnlineStatus status);

	void SignIn();

	Task SignOut();

	bool BeginUpdate();

	void FinishUpdate();

	/// <summary>
	/// Fills the UserStatus with user's currently active sessions
	/// </summary>
	/// <param name="status"></param>
	bool UpdateSessions(UserStatus status, bool forceUpdate);
}
public class AppsManager : SkyFrostModule
{
	public Universe? Universe { get; set; }

	public AppsManager(SkyFrostInterface cloud)
		: base(cloud)
	{
	}

	public Task<CloudResult<List<SamlIdentityProvider>>> GetSamlProviders(string universeId)
	{
		if (string.IsNullOrWhiteSpace(universeId))
		{
			return base.Api.GET<List<SamlIdentityProvider>>("saml2/providers");
		}
		return base.Api.GET<List<SamlIdentityProvider>>("saml2/providers?universeId=" + universeId);
	}

	public async Task Initialize(string universeId)
	{
		if (!string.IsNullOrEmpty(universeId))
		{
			await UpdateCurrentUniverse(universeId).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	public Task<CloudResult<Universe>> GetUniverse(string universeId)
	{
		return base.Api.GET<Universe>("universes/" + universeId);
	}

	private async Task UpdateCurrentUniverse(string universeId)
	{
		CloudResult<Universe> cloudResult = await GetUniverse(universeId).ConfigureAwait(continueOnCapturedContext: false);
		if (cloudResult.IsOK)
		{
			Universe = cloudResult.Entity;
			UniLog.Log("Valid universe found: " + Universe.Id + ", universe name: " + Universe.Name);
			return;
		}
		UniLog.Log($"Invalid Universe found, it will not be used: {universeId}, {cloudResult.State}");
	}
}
public class BadgeManager : SkyFrostModule
{
	private ConcurrentDictionary<EntityId, BadgeDefinition> _cachedBadges = new ConcurrentDictionary<EntityId, BadgeDefinition>();

	public BadgeManager(SkyFrostInterface cloud)
		: base(cloud)
	{
	}

	public async Task<CloudResult<BadgeDefinition>> GetBadge(string ownerId, string badgeId, bool allowCache = true)
	{
		EntityId id = new EntityId(ownerId, badgeId);
		if (allowCache && _cachedBadges.TryGetValue(id, out var value))
		{
			return new CloudResult<BadgeDefinition>(value, HttpStatusCode.OK, null, 0);
		}
		CloudResult<BadgeDefinition> result = await base.Api.GET<BadgeDefinition>("owners/" + ownerId + "/badges/" + badgeId);
		if (result.IsOK)
		{
			_cachedBadges.AddOrUpdate(id, result.Entity, (EntityId entityId, BadgeDefinition badge) => result.Entity);
		}
		return result;
	}

	public async Task<CloudResult> UpdateBadge(BadgeDefinition badge)
	{
		EntityId key = new EntityId(badge.OwnerId, badge.Id);
		_cachedBadges.AddOrUpdate(key, badge, (EntityId id, BadgeDefinition result) => result);
		return await base.Api.PUT<BadgeDefinition>("owners/" + badge.OwnerId + "/badges/" + badge.Id, badge);
	}
}
public class ContactData
{
	private Dictionary<string, UserStatus> statuses = new Dictionary<string, UserStatus>();

	public ContactManager ContactManager { get; private set; }

	public SkyFrostInterface Cloud => ContactManager?.Cloud;

	public Contact Contact { get; internal set; }

	public string UserId => Contact.ContactUserId;

	public UserStatus CurrentStatus { get; private set; }

	public UserStatus PreviousStatus { get; private set; }

	public SessionInfo CurrentSessionInfo { get; private set; }

	public DateTime CreatedOn { get; private set; }

	public ContactData(ContactManager manager, Contact contact)
	{
		ContactManager = manager;
		Contact = contact;
		CreatedOn = DateTime.UtcNow;
		CurrentStatus = GetDefaultStatus();
		PreviousStatus = GetDefaultStatus();
	}

	public UserStatus GetStatus(string userSessionId)
	{
		if (statuses.TryGetValue(userSessionId, out var value))
		{
			return value;
		}
		return null;
	}

	public void DecodeSessions(HashSet<SessionInfo> infos, List<UserSessionMetadata> undecoded = null)
	{
		Dictionary<string, SessionInfo> dictionary = new Dictionary<string, SessionInfo>();
		foreach (KeyValuePair<string, UserStatus> status in statuses)
		{
			List<UserSessionMetadata> sessions = status.Value.Sessions;
			if (sessions == null || sessions.Count == 0)
			{
				continue;
			}
			Cloud.Sessions.CreateSessionMap(status.Value, dictionary);
			foreach (UserSessionMetadata session in status.Value.Sessions)
			{
				if (dictionary.TryGetValue(session.SessionHash, out var value))
				{
					infos.Add(value);
				}
				else
				{
					undecoded?.Add(session);
				}
			}
		}
	}

	internal bool UpdateStatus(UserStatus status)
	{
		if (status.IsExpired)
		{
			UniLog.Warning("Received status update that's already expired:\n" + status, stackTrace: true);
			return false;
		}
		if (statuses.TryGetValue(status.UserSessionId, out var value))
		{
			if (status.LastStatusChange > value.LastStatusChange)
			{
				statuses[status.UserSessionId] = status;
				UpdateAggregate();
				return true;
			}
			return false;
		}
		statuses.Add(status.UserSessionId, status);
		UpdateAggregate();
		return true;
	}

	internal bool ClearExpired()
	{
		if (statuses.Count == 0)
		{
			return false;
		}
		List<string> list = null;
		foreach (KeyValuePair<string, UserStatus> status in statuses)
		{
			if (status.Value.IsExpired)
			{
				if (list == null)
				{
					list = Pool.BorrowList<string>();
				}
				list.Add(status.Key);
				UniLog.Log($"Clearing expired status for contact {Contact.ContactUserId}.\nStatus: {status}\nTotal statuses: {statuses.Count}");
			}
		}
		if (list != null)
		{
			foreach (string item in list)
			{
				statuses.Remove(item);
			}
			UniLog.Log($"Status before clearing: {CurrentStatus}");
			UpdateAggregate();
			UniLog.Log($"Status after clearing: {CurrentStatus}");
			Pool.Return(ref list);
			return true;
		}
		return false;
	}

	private void UpdateAggregate()
	{
		UserStatus userStatus = null;
		foreach (KeyValuePair<string, UserStatus> status in statuses)
		{
			if (status.Value.IsDominantOver(userStatus))
			{
				userStatus = status.Value;
			}
		}
		PreviousStatus = CurrentStatus;
		CurrentStatus = userStatus ?? GetDefaultStatus();
		UserSessionMetadata currentSession = CurrentStatus.CurrentSession;
		if (currentSession == null)
		{
			CurrentSessionInfo = null;
		}
		else
		{
			CurrentSessionInfo = Cloud.Sessions.MatchSessionInfo(CurrentStatus, currentSession);
		}
	}

	internal bool TryMatchNewSession(SessionInfo info)
	{
		if (CurrentSessionInfo != null)
		{
			return false;
		}
		UserSessionMetadata currentSession = CurrentStatus.CurrentSession;
		if (currentSession == null)
		{
			return false;
		}
		if (CurrentStatus.IsMatchingSession(info, currentSession))
		{
			PreviousStatus = CurrentStatus;
			CurrentSessionInfo = info;
			return true;
		}
		return false;
	}

	internal bool TryUpdateSession(SessionInfo info)
	{
		if (CurrentSessionInfo == null)
		{
			return false;
		}
		if (info.SessionId == CurrentSessionInfo.SessionId && info != CurrentSessionInfo)
		{
			PreviousStatus = CurrentStatus;
			CurrentSessionInfo = info;
			return true;
		}
		return false;
	}

	internal bool TryRemoveSession(string sessionId)
	{
		if (CurrentSessionInfo == null)
		{
			return false;
		}
		if (CurrentSessionInfo.SessionId == sessionId)
		{
			PreviousStatus = CurrentStatus;
			CurrentSessionInfo = null;
			return true;
		}
		return false;
	}

	private UserStatus GetDefaultStatus()
	{
		return new UserStatus
		{
			UserId = UserId,
			UserSessionId = "DEFAULT",
			SessionType = UserSessionType.Unknown,
			OutputDevice = null,
			OnlineStatus = OnlineStatus.Offline,
			CurrentSessionIndex = -1
		};
	}
}
public class ContactManager : SkyFrostModule
{
	private readonly string _contactPath;

	private Dictionary<string, ContactData> contacts = new Dictionary<string, ContactData>();

	private List<ContactData> contactDataList = new List<ContactData>();

	private HashSet<string> sessionBroadcastKeys = new HashSet<string>();

	private object _lock = new object();

	private int updateListIndex;

	public int ContactRequestCount { get; private set; }

	public bool ContactListLoaded { get; private set; }

	public int ContactCount => contacts.Count;

	public event Action<ContactData> ContactAdded;

	public event Action<ContactData> ContactUpdated;

	public event Action<ContactData> ContactRemoved;

	public event Action<int> ContactRequestCountChanged;

	public event Action<ContactData> ContactStatusChanged;

	public ContactManager(SkyFrostInterface cloud, string contactPath)
		: base(cloud)
	{
		_contactPath = contactPath;
		base.Cloud.Sessions.SessionAdded += OnSessionAdded;
		base.Cloud.Sessions.SessionUpdated += OnSessionUpdated;
		base.Cloud.Sessions.SessionRemoved += OnSessionRemoved;
	}

	private void OnSessionRemoved(SessionInfo info)
	{
		Task.Run(delegate
		{
			lock (_lock)
			{
				foreach (KeyValuePair<string, ContactData> contact in contacts)
				{
					if (contact.Value.TryRemoveSession(info.SessionId))
					{
						this.ContactStatusChanged?.Invoke(contact.Value);
					}
				}
			}
		});
	}

	private void OnSessionUpdated(SessionInfo info)
	{
		Task.Run(delegate
		{
			lock (_lock)
			{
				foreach (KeyValuePair<string, ContactData> contact in contacts)
				{
					if (contact.Value.TryUpdateSession(info))
					{
						this.ContactStatusChanged?.Invoke(contact.Value);
					}
				}
			}
		});
	}

	private void OnSessionAdded(SessionInfo info)
	{
		Task.Run(delegate
		{
			lock (_lock)
			{
				foreach (KeyValuePair<string, ContactData> contact in contacts)
				{
					if (contact.Value.TryMatchNewSession(info))
					{
						this.ContactStatusChanged?.Invoke(contact.Value);
					}
				}
			}
		});
	}

	public void GetContacts(List<Contact> list)
	{
		lock (_lock)
		{
			foreach (KeyValuePair<string, ContactData> contact in contacts)
			{
				list.Add(contact.Value.Contact);
			}
		}
	}

	public void ForeachContact(Action<Contact> action)
	{
		lock (_lock)
		{
			foreach (KeyValuePair<string, ContactData> contact in contacts)
			{
				action(contact.Value.Contact);
			}
		}
	}

	public void ForeachContactData(Action<ContactData> action)
	{
		lock (_lock)
		{
			foreach (KeyValuePair<string, ContactData> contact in contacts)
			{
				action(contact.Value);
			}
		}
	}

	public Contact GetContact(string contactId)
	{
		lock (_lock)
		{
			if (contacts.TryGetValue(contactId, out var value))
			{
				return value.Contact;
			}
		}
		return null;
	}

	public UserStatus GetContactSession(string contactId, string userSessionId)
	{
		lock (_lock)
		{
			if (contacts.TryGetValue(contactId, out var value))
			{
				return value.GetStatus(userSessionId);
			}
		}
		return null;
	}

	public Contact FindContact(Predicate<Contact> predicate)
	{
		lock (_lock)
		{
			foreach (KeyValuePair<string, ContactData> contact in contacts)
			{
				if (predicate(contact.Value.Contact))
				{
					return contact.Value.Contact;
				}
			}
		}
		return null;
	}

	public bool IsContact(string userId, bool mutuallyAccepted = false)
	{
		if (string.IsNullOrEmpty(userId))
		{
			return false;
		}
		lock (_lock)
		{
			if (contacts.TryGetValue(userId, out var value))
			{
				if (value.Contact.ContactStatus != ContactStatus.Accepted)
				{
					return false;
				}
				if (mutuallyAccepted && !value.Contact.IsAccepted)
				{
					return false;
				}
				return true;
			}
			return false;
		}
	}

	public int CountPresentContacts(SessionInfo session)
	{
		if (session == null)
		{
			return 0;
		}
		if (session.SessionUsers == null || session.SessionUsers.Count == 0)
		{
			return 0;
		}
		int num = 0;
		lock (_lock)
		{
			foreach (SessionUser sessionUser in session.SessionUsers)
			{
				if (sessionUser.IsPresent && sessionUser.UserID != null && contacts.ContainsKey(sessionUser.UserID))
				{
					num++;
				}
			}
			return num;
		}
	}

	public Task<bool> AddContact(string contactId, string contactName)
	{
		Contact contact = new Contact();
		contact.OwnerId = base.CurrentUserID;
		contact.ContactUserId = contactId;
		contact.ContactUsername = contactName;
		return AddContact(contact);
	}

	public Task<bool> AddContact(Contact contact)
	{
		contact.ContactStatus = ContactStatus.Accepted;
		return UpdateContact(contact);
	}

	public Task<bool> RemoveContact(Contact contact)
	{
		contact.ContactStatus = ContactStatus.Ignored;
		return UpdateContact(contact);
	}

	public Task<bool> IgnoreRequest(Contact contact)
	{
		contact.ContactStatus = ContactStatus.Ignored;
		return UpdateContact(contact);
	}

	private Task<bool> UpdateContact(Contact contact)
	{
		if (contact.OwnerId == null)
		{
			contact.OwnerId = base.CurrentUserID;
		}
		else if (contact.OwnerId != base.CurrentUserID)
		{
			throw new ArgumentException("Contact is owned by " + contact.OwnerId + ", but currently signed user is " + base.CurrentUserID);
		}
		UpdateContactRequestCount();
		return Task.Run(async () => await base.Cloud.HubClient.UpdateContact(contact));
	}

	internal void LoadContact(Contact contact, ref int requests)
	{
		ContactData contactData;
		lock (_lock)
		{
			if (contacts.ContainsKey(contact.ContactUserId))
			{
				return;
			}
			if (contact.IsContactRequest)
			{
				requests++;
			}
			contactData = new ContactData(this, contact);
			contacts.Add(contact.ContactUserId, contactData);
			contactDataList.Add(contactData);
		}
		this.ContactAdded?.Invoke(contactData);
	}

	internal void FinalizeLoading(int requests)
	{
		ContactRequestCount = requests;
		ContactListLoaded = true;
		this.ContactRequestCountChanged?.Invoke(ContactRequestCount);
	}

	internal void ContactStatusUpdated(UserStatus status)
	{
		lock (_lock)
		{
			if (!contacts.TryGetValue(status.UserId, out var value))
			{
				UniLog.Log("Corresponding contact not found!");
			}
			else
			{
				if (!value.UpdateStatus(status))
				{
					return;
				}
				if (status.Sessions != null)
				{
					try
					{
						foreach (UserSessionMetadata session in status.Sessions)
						{
							if (sessionBroadcastKeys.Add(session.BroadcastKey))
							{
								string key = session.BroadcastKey;
								Task.Run(async delegate
								{
									await base.Cloud.HubClient.ListenOnKey(key).ConfigureAwait(continueOnCapturedContext: false);
								});
							}
						}
					}
					catch (Exception value2)
					{
						UniLog.Error($"Exception when processing sessions from contact status update:\n{value2}", stackTrace: false);
					}
				}
				this.ContactStatusChanged?.Invoke(value);
			}
		}
	}

	internal void ContactAddedOrUpdated(Contact contact)
	{
		lock (_lock)
		{
			bool flag = false;
			bool flag2 = false;
			if (contacts.TryGetValue(contact.ContactUserId, out var value))
			{
				if (!value.Contact.IsAccepted && contact.IsAccepted)
				{
					flag2 = true;
				}
				if (contact.IsContactRequest != value.Contact.IsContactRequest)
				{
					flag = true;
				}
				value.Contact = contact;
				this.ContactUpdated?.Invoke(value);
			}
			else
			{
				ContactData contactData = new ContactData(this, contact);
				contacts.Add(contact.ContactUserId, contactData);
				contactDataList.Add(contactData);
				this.ContactAdded?.Invoke(contactData);
				if (contact.IsAccepted)
				{
					flag2 = true;
				}
				if (contact.IsContactRequest)
				{
					flag = true;
				}
			}
			if (flag2)
			{
				Task.Run(async delegate
				{
					_ = 2;
					try
					{
						UniLog.Log("Newly accepted contact: " + contact.ContactUserId + ", requesting listen...");
						bool isInvisible = base.Cloud.Status.OnlineStatus == OnlineStatus.Invisible;
						await base.Cloud.HubClient.ListenOnContact(contact.ContactUserId).ConfigureAwait(continueOnCapturedContext: false);
						await base.Cloud.HubClient.RequestStatus(contact.ContactUserId, isInvisible).ConfigureAwait(continueOnCapturedContext: false);
						if (!isInvisible)
						{
							await base.Cloud.HubStatusController.SendStatusToUser(contact.ContactUserId).ConfigureAwait(continueOnCapturedContext: false);
						}
					}
					catch (Exception ex)
					{
						UniLog.Error("Exception when listening on newly accepted contact:\n" + ex);
					}
				});
			}
			if (flag)
			{
				Task.Run((Action)UpdateContactRequestCount);
			}
		}
	}

	internal void UpdateContactRequestCount()
	{
		lock (_lock)
		{
			int num = 0;
			foreach (KeyValuePair<string, ContactData> contact in contacts)
			{
				if (contact.Value.Contact.IsContactRequest)
				{
					num++;
				}
			}
			if (ContactRequestCount != num)
			{
				ContactRequestCount = num;
				this.ContactRequestCountChanged?.Invoke(ContactRequestCount);
			}
		}
	}

	internal void Reconnecting()
	{
		lock (_lock)
		{
			sessionBroadcastKeys.Clear();
		}
	}

	internal void Reset()
	{
		lock (_lock)
		{
			ContactListLoaded = false;
			ContactRequestCount = 0;
			foreach (KeyValuePair<string, ContactData> contact in contacts)
			{
				this.ContactRemoved?.Invoke(contact.Value);
			}
			contacts.Clear();
			contactDataList.Clear();
			sessionBroadcastKeys.Clear();
		}
	}

	internal void Update()
	{
		lock (_lock)
		{
			if (contactDataList.Count > 0)
			{
				ContactData contactData = contactDataList[updateListIndex % contactDataList.Count];
				if (contactData.ClearExpired())
				{
					this.ContactStatusChanged?.Invoke(contactData);
				}
			}
			updateListIndex++;
			if (updateListIndex >= contactDataList.Count)
			{
				updateListIndex = 0;
			}
		}
	}

	public Task<CloudResult<List<Contact>>> GetContacts()
	{
		return GetContacts(base.CurrentUserID);
	}

	public Task<CloudResult<List<Contact>>> GetContacts(string userId)
	{
		return base.Api.GET<List<Contact>>("users/" + userId + "/" + _contactPath);
	}
}
public class GroupsManager : SkyFrostModule
{
	private object _groupsLock = new object();

	private Dictionary<string, Member> _groupMemberInfos = new Dictionary<string, Member>();

	private Dictionary<string, Group> _groups = new Dictionary<string, Group>();

	private Dictionary<string, Storage> _groupStorages = new Dictionary<string, Storage>();

	private Dictionary<string, Storage> _groupMemberStorages = new Dictionary<string, Storage>();

	private List<Membership> _groupMemberships = new List<Membership>();

	public IEnumerable<Membership> CurrentUserMemberships => _groupMemberships;

	public IEnumerable<Member> CurrentUserMemberInfos => _groupMemberInfos.Select((KeyValuePair<string, Member> p) => p.Value);

	public event Action<IEnumerable<Membership>> MembershipsUpdated;

	public event Action<Group> GroupUpdated;

	public event Action<Member> GroupMemberUpdated;

	public GroupsManager(SkyFrostInterface cloud)
		: base(cloud)
	{
	}

	public void GetCurrentGroups(List<Group> groups)
	{
		lock (_groupsLock)
		{
			groups.AddRange(_groups.Select((KeyValuePair<string, Group> p) => p.Value));
		}
	}

	public Group TryGetCurrentUserGroupInfo(string groupId)
	{
		lock (_groupsLock)
		{
			_groups.TryGetValue(groupId, out var value);
			return value;
		}
	}

	public Member TryGetCurrentUserGroupMemberInfo(string groupId)
	{
		lock (_groupsLock)
		{
			_groupMemberInfos.TryGetValue(groupId, out var value);
			return value;
		}
	}

	public Storage TryGetGroupStorage(string groupId)
	{
		lock (_groupsLock)
		{
			_groupStorages.TryGetValue(groupId, out var value);
			return value;
		}
	}

	public Storage TryGetMemberStorage(string groupId)
	{
		lock (_groupsLock)
		{
			_groupMemberStorages.TryGetValue(groupId, out var value);
			return value;
		}
	}

	public bool IsCurrentUserMemberOfGroup(string groupId)
	{
		return TryGetCurrentUserGroupMemberInfo(groupId) != null;
	}

	public Membership TryGetCurrentUserGroupMembership(string groupId)
	{
		return _groupMemberships.FirstOrDefault((Membership m) => m.GroupId == groupId);
	}

	internal void Reset()
	{
		ClearMemberships();
		lock (_groupsLock)
		{
			_groups.Clear();
			_groupMemberInfos.Clear();
			_groupMemberships.Clear();
			_groupStorages.Clear();
			_groupMemberStorages.Clear();
		}
	}

	private void SetMemberships(IEnumerable<Membership> memberships)
	{
		lock (_groupsLock)
		{
			_groupMemberships.Clear();
			_groupMemberships.AddRange(memberships);
			RunMembershipsUpdated();
		}
	}

	private void ClearMemberships()
	{
		lock (_groupsLock)
		{
			if (_groupMemberships.Count != 0)
			{
				_groupMemberships.Clear();
				RunMembershipsUpdated();
			}
		}
	}

	private void AddMembership(Membership membership)
	{
		lock (_groupsLock)
		{
			_groupMemberships.Add(membership);
			RunMembershipsUpdated();
		}
	}

	private async Task RunMembershipsUpdated()
	{
		foreach (Membership groupMembership in _groupMemberships)
		{
			await UpdateGroupInfo(groupMembership.GroupId);
		}
		this.MembershipsUpdated?.Invoke(_groupMemberships);
	}

	public Task<CloudResult<Group>> GetGroup(string groupId)
	{
		return base.Api.GET<Group>("groups/" + groupId);
	}

	public Task<CloudResult<Group>> GetGroupCached(string groupId)
	{
		return GetGroup(groupId);
	}

	public Task<CloudResult<Group>> CreateGroup(Group group)
	{
		return base.Api.POST<Group>("groups", group);
	}

	public Task<CloudResult> AddGroupMember(Member member, long quota = -1L)
	{
		return base.Api.POST($"groups/{member.GroupId}/members?quota={quota}", member);
	}

	public Task<CloudResult> DeleteGroupMember(Member member)
	{
		return base.Api.DELETE("groups/" + member.GroupId + "/members/" + member.UserId);
	}

	public Task<CloudResult<Member>> GetGroupMember(string groupId, string userId)
	{
		return base.Api.GET<Member>("groups/" + groupId + "/members/" + userId);
	}

	public Task<CloudResult<List<Member>>> GetGroupMembers(string groupId)
	{
		return base.Api.GET<List<Member>>("groups/" + groupId + "/members");
	}

	public async Task<CloudResult> UpdateCurrentUserMemberships()
	{
		CloudResult<List<Membership>> cloudResult = await GetUserGroupMemeberships();
		if (cloudResult.IsOK)
		{
			SetMemberships(cloudResult.Entity);
		}
		return cloudResult;
	}

	public Task<CloudResult<List<Membership>>> GetUserGroupMemeberships()
	{
		return GetUserGroupMemeberships(base.CurrentUserID);
	}

	public Task<CloudResult<List<Membership>>> GetUserGroupMemeberships(string userId)
	{
		return base.Api.GET<List<Membership>>("users/" + userId + "/memberships");
	}

	public async Task UpdateGroupInfo(string groupId)
	{
		Task<CloudResult<Group>> task = GetGroup(groupId);
		Task<CloudResult<Member>> memberTask = GetGroupMember(groupId, base.CurrentUserID);
		Task<CloudResult<Storage>> groupStorageTask = base.Cloud.Storage.GetStorage(groupId);
		Task<CloudResult<Storage>> memberStorageTask = base.Cloud.Storage.GetMemberStorage(groupId, base.CurrentUserID);
		CloudResult<Group> groupResult = await task.ConfigureAwait(continueOnCapturedContext: false);
		CloudResult<Member> memberResult = await memberTask.ConfigureAwait(continueOnCapturedContext: false);
		CloudResult<Storage> groupStorage = await groupStorageTask.ConfigureAwait(continueOnCapturedContext: false);
		CloudResult<Storage> cloudResult = await memberStorageTask.ConfigureAwait(continueOnCapturedContext: false);
		lock (_groupsLock)
		{
			if (groupResult.IsOK)
			{
				_groups[groupId] = groupResult.Entity;
				this.GroupUpdated?.Invoke(groupResult.Entity);
			}
			if (memberResult.IsOK)
			{
				_groupMemberInfos[groupId] = memberResult.Entity;
				this.GroupMemberUpdated?.Invoke(memberResult.Entity);
			}
			if (groupStorage.IsOK)
			{
				_groupStorages[groupId] = groupStorage.Entity;
			}
			if (cloudResult.IsOK)
			{
				_groupMemberStorages[groupId] = cloudResult.Entity;
			}
		}
	}

	public Task<CloudResult<Submission>> UpsertSubmission(string groupId, string ownerId, string recordId, bool feature = false)
	{
		Submission submission = new Submission();
		submission.GroupId = groupId;
		submission.TargetRecordId = new RecordId(ownerId, recordId);
		submission.Featured = feature;
		return base.Api.PUT<Submission>("groups/" + groupId + "/submissions", submission);
	}

	public Task<CloudResult> DeleteSubmission(string groupId, string submissionId)
	{
		return base.Api.DELETE("groups/" + groupId + "/submissions/" + submissionId);
	}
}
public class MessageManager : SkyFrostModule, IHubMessagingClient
{
	private CancellationTokenSource _fetchCancellation;

	private bool _initialFetchRunning;

	private object _messagesLock = new object();

	private Dictionary<string, UserMessages> _messages = new Dictionary<string, UserMessages>();

	private ConcurrentDictionary<string, TaskCompletionSource<bool>> _messagesWaitingForConfirmation = new ConcurrentDictionary<string, TaskCompletionSource<bool>>();

	private volatile bool _unreadCountDirty;

	public bool SendReadNotification { get; set; } = true;

	public bool InitialUnreadMessagesFetched { get; private set; }

	public int TotalUnreadCount { get; private set; }

	public ConcurrentDictionary<string, int> UnreadCountByUser { get; private set; } = new ConcurrentDictionary<string, int>();

	public event Action<Message> OnMessageSent;

	public event Action<Message> OnMessageReceived;

	public event Action<Message> OnMessageRead;

	public event Action<int> UnreadMessageCountChanged;

	public MessageManager(SkyFrostInterface cloud)
		: base(cloud)
	{
	}

	public Task ReceiveMessage(Message message)
	{
		if (message.SenderId == base.Platform.AppUserId)
		{
			base.Cloud.Migration.UpdateMigrationTasksInSeconds(10f);
		}
		lock (_messagesLock)
		{
			if (!GetUserMessages(message.SenderId).AddMessage(message))
			{
				return Task.CompletedTask;
			}
		}
		ProcessReceivedMessage(message);
		MarkUnreadCountDirty();
		return Task.CompletedTask;
	}

	private void ProcessReceivedMessage(Message message)
	{
		this.OnMessageReceived?.Invoke(message);
		Contact contact = base.Cloud.Contacts.GetContact(message.SenderId);
		if (contact != null)
		{
			contact.LatestMessageTime = MathX.Max(DateTime.UtcNow, message.SendTime);
		}
	}

	internal void ProcessSentMessage(Message message)
	{
		this.OnMessageSent?.Invoke(message);
	}

	public Task MessageSent(Message message)
	{
		if (_messagesWaitingForConfirmation.TryGetValue(message.Id, out var value))
		{
			value.TrySetResult(result: true);
		}
		else if (message.SenderUserSessionId != base.Cloud.Status.UserSessionId)
		{
			base.Cloud.Messages.GetUserMessages(message.RecipientId).RegisterSentMessage(message);
		}
		return Task.CompletedTask;
	}

	public Task MessagesRead(ReadMessageBatch batch)
	{
		List<Message> list = Pool.BorrowList<Message>();
		lock (_messagesLock)
		{
			GetUserMessages(batch.RecipientId).MarkReadByRecipient(batch.Ids, batch.ReadTime, list);
		}
		foreach (Message item in list)
		{
			this.OnMessageRead?.Invoke(item);
		}
		Pool.Return(ref list);
		return Task.CompletedTask;
	}

	internal void Update()
	{
		if (base.CurrentUser == null)
		{
			return;
		}
		if (!InitialUnreadMessagesFetched && !_initialFetchRunning)
		{
			FetchInitialMessages();
		}
		if (!_unreadCountDirty || !base.Cloud.Contacts.ContactListLoaded)
		{
			return;
		}
		_unreadCountDirty = false;
		lock (_messagesLock)
		{
			TotalUnreadCount = _messages.Where((KeyValuePair<string, UserMessages> keyValuePair) => base.Cloud.Contacts.IsContact(keyValuePair.Value.UserId)).Sum((KeyValuePair<string, UserMessages> keyValuePair) => keyValuePair.Value.UnreadCount);
			foreach (KeyValuePair<string, UserMessages> m in _messages)
			{
				if (m.Value.UnreadCount == 0)
				{
					UnreadCountByUser.TryRemove(m.Key, out var _);
					continue;
				}
				UnreadCountByUser.AddOrUpdate(m.Key, m.Value.UnreadCount, (string key, int num) => m.Value.UnreadCount);
			}
		}
		this.UnreadMessageCountChanged?.Invoke(TotalUnreadCount);
	}

	private void FetchInitialMessages()
	{
		if (InitialUnreadMessagesFetched)
		{
			throw new InvalidOperationException("Initial messages were already fetched");
		}
		if (_initialFetchRunning)
		{
			throw new InvalidOperationException("Initial message fetch is already running!");
		}
		_initialFetchRunning = true;
		_fetchCancellation = new CancellationTokenSource();
		CancellationToken token = _fetchCancellation.Token;
		Task.Run(async delegate
		{
			try
			{
				CloudResult<List<Message>> cloudResult = await GetUnreadMessages().ConfigureAwait(continueOnCapturedContext: false);
				if (!token.IsCancellationRequested)
				{
					if (cloudResult.IsOK)
					{
						HashSet<Message> hashSet = Pool.BorrowHashSet<Message>();
						lock (_messagesLock)
						{
							foreach (Message item in cloudResult.Entity)
							{
								if (!GetUserMessages(item.SenderId).AddMessage(item))
								{
									hashSet.Add(item);
								}
							}
						}
						foreach (Message item2 in cloudResult.Entity)
						{
							if (!hashSet.Contains(item2))
							{
								item2.IsPreFetched = true;
								ProcessReceivedMessage(item2);
							}
						}
						Pool.Return(ref hashSet);
						MarkUnreadCountDirty();
						InitialUnreadMessagesFetched = true;
					}
					else
					{
						UniLog.Log($"Failed to fetch unread messages, Result: {cloudResult}");
					}
				}
			}
			catch (Exception ex)
			{
				UniLog.Error("Exception in fetching initial messages:\n" + ex, stackTrace: false);
			}
			finally
			{
				if (!token.IsCancellationRequested)
				{
					_initialFetchRunning = false;
				}
			}
		});
	}

	internal void RegisterMessageWaitingForConfirmation(Message message, TaskCompletionSource<bool> completion)
	{
		_messagesWaitingForConfirmation.TryAdd(message.Id, completion);
	}

	internal void UnregisterMessageWaitingForConfirmation(Message message)
	{
		_messagesWaitingForConfirmation.TryRemove(message.Id, out var _);
	}

	internal void MarkUnreadCountDirty()
	{
		_unreadCountDirty = true;
	}

	internal void Reset()
	{
		_fetchCancellation?.Cancel();
		_fetchCancellation = null;
		bool flag = TotalUnreadCount > 0;
		lock (_messagesLock)
		{
			_messages.Clear();
			InitialUnreadMessagesFetched = false;
			_initialFetchRunning = false;
			TotalUnreadCount = 0;
			UnreadCountByUser.Clear();
		}
		if (flag)
		{
			this.UnreadMessageCountChanged?.Invoke(0);
		}
	}

	public Task<CloudResult<Message>> StoreMessage(Message message)
	{
		throw new NotImplementedException();
	}

	public Task<CloudResult<List<Message>>> GetUnreadMessages(DateTime? fromTime = null)
	{
		return GetMessages(fromTime, -1, null, unreadOnly: true, TimeSpan.FromSeconds(90L));
	}

	public Task<CloudResult<List<Message>>> GetMessageHistory(string user, int maxItems = 100)
	{
		return GetMessages(null, maxItems, user, unreadOnly: false);
	}

	public Task<CloudResult<List<Message>>> GetMessages(DateTime? fromTime, int maxItems, string user, bool unreadOnly, TimeSpan? timeout = null)
	{
		StringBuilder stringBuilder = Pool.BorrowStringBuilder();
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(10, 1, stringBuilder2);
		handler.AppendLiteral("?maxItems=");
		handler.AppendFormatted(maxItems);
		stringBuilder3.Append(ref handler);
		if (fromTime.HasValue)
		{
			stringBuilder.Append("&fromTime=" + fromTime.Value.ToUniversalTime().ToString("o"));
		}
		if (user != null)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder4 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(6, 1, stringBuilder2);
			handler.AppendLiteral("&user=");
			handler.AppendFormatted(user);
			stringBuilder4.Append(ref handler);
		}
		if (unreadOnly)
		{
			stringBuilder.Append("&unread=true");
		}
		string resource = $"users/{base.CurrentUserID}/messages{stringBuilder}";
		Pool.Return(ref stringBuilder);
		return base.Api.GET<List<Message>>(resource, timeout);
	}

	public UserMessages GetUserMessages(string userId)
	{
		lock (_messagesLock)
		{
			if (_messages.TryGetValue(userId, out var value))
			{
				return value;
			}
			value = new UserMessages(userId, this);
			_messages.Add(userId, value);
			return value;
		}
	}

	public void GetAllUserMessages(List<UserMessages> list)
	{
		lock (_messagesLock)
		{
			foreach (KeyValuePair<string, UserMessages> message in _messages)
			{
				list.Add(message.Value);
			}
		}
	}
}
public class UserMessages
{
	private HashSet<string> _messageIds = new HashSet<string>();

	private object _lock = new object();

	private Task<CloudResult<List<Message>>> _historyLoadTask;

	private bool _historyLoaded;

	public static int MAX_READ_HISTORY => 100;

	public static int MAX_UNREAD_HISTORY => 200;

	public MessageManager Manager { get; private set; }

	public SkyFrostInterface Cloud => Manager.Cloud;

	public string UserId { get; private set; }

	public List<Message> Messages { get; private set; } = new List<Message>();

	public int UnreadCount { get; private set; }

	public UserMessages(string userId, MessageManager manager)
	{
		UserId = userId;
		Manager = manager;
	}

	public void MarkAllRead()
	{
		List<List<string>> ids = null;
		int num = 0;
		lock (_lock)
		{
			if (UnreadCount == 0)
			{
				return;
			}
			ids = new List<List<string>>();
			ids.Add(new List<string>());
			foreach (Message message in Messages)
			{
				if (!message.IsSent && !message.ReadTime.HasValue)
				{
					message.ReadTime = DateTime.UtcNow;
					if (ids[num].Count == 512)
					{
						ids.Add(new List<string>());
						num++;
					}
					ids[num].Add(message.Id);
				}
			}
			UnreadCount = 0;
		}
		if (ids != null && ids.Count > 0)
		{
			Task.Run(async delegate
			{
				DateTime time = DateTime.UtcNow;
				foreach (List<string> item in ids)
				{
					MarkReadBatch markReadBatch = new MarkReadBatch
					{
						SenderId = (Manager.SendReadNotification ? UserId : null),
						Ids = item,
						ReadTime = time
					};
					await Cloud.HubClient.MarkMessagesRead(markReadBatch).ConfigureAwait(continueOnCapturedContext: false);
				}
			});
		}
		Manager.MarkUnreadCountDirty();
	}

	public Message CreateInviteRequest()
	{
		Message message = new Message();
		message.Id = Message.GenerateId();
		message.MessageType = MessageType.InviteRequest;
		message.SetContent(new InviteRequest
		{
			InviteRequestId = Guid.CreateVersion7().ToString(),
			UserIdToInvite = Cloud.CurrentUserID,
			UsernameToInvite = Cloud.CurrentUsername,
			RequestingFromUserId = UserId,
			RequestingFromUsername = Cloud.Contacts.GetContact(UserId)?.ContactUsername
		});
		return message;
	}

	public Message CreateForwardedInviteRequest(InviteRequest request, string forSessionId, string forSessionName)
	{
		Message obj = new Message
		{
			Id = Message.GenerateId(),
			MessageType = MessageType.InviteRequest
		};
		InviteRequest inviteRequest = request.Clone();
		inviteRequest.ForSessionId = forSessionId;
		inviteRequest.ForSessionName = forSessionName;
		obj.SetContent(inviteRequest);
		return obj;
	}

	public Message CreateHeadlessForwardedInviteRequest(InviteRequest request, bool isContactOfHost)
	{
		Message obj = new Message
		{
			Id = Message.GenerateId(),
			MessageType = MessageType.InviteRequest
		};
		InviteRequest inviteRequest = request.Clone();
		inviteRequest.IsContactWithHost = isContactOfHost;
		obj.SetContent(inviteRequest);
		return obj;
	}

	public Message CreateGrantedInviteRequest(InviteRequest request, SessionInfo invite)
	{
		Message obj = new Message
		{
			Id = Message.GenerateId(),
			MessageType = MessageType.InviteRequest
		};
		InviteRequest inviteRequest = request.Clone();
		inviteRequest.Invite = invite;
		if (!inviteRequest.Response.HasValue)
		{
			inviteRequest.Response = InviteRequestResponse.SendInvite;
		}
		obj.SetContent(inviteRequest);
		return obj;
	}

	public Message CreateInviteRequestResponse(InviteRequest request, InviteRequestResponse response)
	{
		Message obj = new Message
		{
			Id = Message.GenerateId(),
			MessageType = MessageType.InviteRequest
		};
		InviteRequest inviteRequest = request.Clone();
		inviteRequest.Response = response;
		obj.SetContent(inviteRequest);
		return obj;
	}

	public Message CreateTextMessage(string text)
	{
		return new Message
		{
			Id = Message.GenerateId(),
			MessageType = MessageType.Text,
			Content = text
		};
	}

	public Message CreateInviteMessage(SessionInfo sessionInfo)
	{
		Message message = new Message();
		message.Id = Message.GenerateId();
		message.SendTime = DateTime.UtcNow;
		message.MessageType = MessageType.SessionInvite;
		message.SetContent(sessionInfo);
		return message;
	}

	public Task<bool> SendInviteMessage(SessionInfo sessionInfo)
	{
		return SendMessage(CreateInviteMessage(sessionInfo));
	}

	public Task<bool> SendInviteRequest()
	{
		return SendMessage(CreateInviteRequest());
	}

	public Task<bool> SendInviteRequestGrant(InviteRequest request, SessionInfo info)
	{
		return SendMessage(CreateGrantedInviteRequest(request, info));
	}

	public async Task<bool> SendMessage(Message message)
	{
		if (message.Id == null)
		{
			message.Id = Message.GenerateId();
		}
		message.RecipientId = UserId;
		message.SenderId = Manager.CurrentUserID;
		message.SenderUserSessionId = Cloud.Status.UserSessionId;
		message.OwnerId = message.SenderId;
		message.SendTime = DateTime.UtcNow;
		RegisterSentMessage(message);
		Contact contact = Cloud.Contacts.GetContact(message.RecipientId);
		if (contact != null)
		{
			contact.LatestMessageTime = DateTime.UtcNow;
		}
		TaskCompletionSource<bool> completionSource = new TaskCompletionSource<bool>();
		CancellationTokenSource cancellationSource = new CancellationTokenSource();
		Manager.RegisterMessageWaitingForConfirmation(message, completionSource);
		message.SendStatus = SendStatus.Sending;
		await Cloud.HubClient.SendMessage(message).ConfigureAwait(continueOnCapturedContext: false);
		Task.Run(async delegate
		{
			await Task.Delay(TimeSpan.FromSeconds(30L), cancellationSource.Token).ConfigureAwait(continueOnCapturedContext: false);
			if (!cancellationSource.Token.IsCancellationRequested)
			{
				completionSource.TrySetResult(result: false);
			}
		});
		bool num = await completionSource.Task.ConfigureAwait(continueOnCapturedContext: false);
		if (num)
		{
			cancellationSource.Cancel();
		}
		Manager.UnregisterMessageWaitingForConfirmation(message);
		if (num)
		{
			message.SendStatus = SendStatus.Sent;
		}
		else
		{
			message.SendStatus = SendStatus.Failed;
		}
		return num;
	}

	public void RegisterSentMessage(Message message)
	{
		lock (_lock)
		{
			Messages.Add(message);
		}
		Manager.ProcessSentMessage(message);
	}

	public Task<bool> SendTextMessage(string text)
	{
		return SendMessage(CreateTextMessage(text));
	}

	public async Task EnsureHistory()
	{
		if (_historyLoaded)
		{
			return;
		}
		bool isFirstRequest = false;
		lock (_lock)
		{
			if (_historyLoaded)
			{
				return;
			}
			if (_historyLoadTask == null)
			{
				isFirstRequest = true;
				_historyLoadTask = Manager.GetMessageHistory(UserId, MAX_READ_HISTORY);
			}
		}
		CloudResult<List<Message>> cloudResult = await _historyLoadTask.ConfigureAwait(continueOnCapturedContext: false);
		if (!isFirstRequest)
		{
			return;
		}
		if (!cloudResult.IsOK)
		{
			UniLog.Log($"Failed getting message history for {UserId}, Result: {cloudResult}");
			_historyLoadTask = null;
			return;
		}
		cloudResult.Entity.ForEach(delegate(Message m)
		{
			if (m.IsSent)
			{
				m.SendStatus = SendStatus.Sent;
			}
		});
		lock (_lock)
		{
			if (Messages != null && Messages.Count > 0)
			{
				HashSet<string> hashSet = new HashSet<string>();
				foreach (Message message in Messages)
				{
					hashSet.Add(message.Id);
				}
				foreach (Message item in cloudResult.Entity)
				{
					if (!hashSet.Contains(item.Id))
					{
						Messages.Add(item);
					}
				}
				Messages.Sort((Message a, Message b) => a.LastUpdateTime.CompareTo(b.LastUpdateTime));
			}
			else
			{
				Messages = cloudResult.Entity;
				Messages.Reverse();
			}
			UnreadCount = Messages.Count((Message m) => !m.ReadTime.HasValue);
			_historyLoaded = true;
		}
	}

	internal bool AddMessage(Message message)
	{
		lock (_lock)
		{
			if (_messageIds.Contains(message.Id))
			{
				return false;
			}
			Messages.Add(message);
			_messageIds.Add(message.Id);
			if (message.IsReceived && !message.ReadTime.HasValue)
			{
				UnreadCount++;
			}
			while (Messages.Count > MAX_UNREAD_HISTORY || (Messages.Count > MAX_READ_HISTORY && (Messages[0].IsSent || Messages[0].ReadTime.HasValue)))
			{
				_messageIds.Remove(Messages[0].Id);
				Messages.RemoveAt(0);
			}
			return true;
		}
	}

	public void GetMessages(List<Message> messages)
	{
		lock (_lock)
		{
			messages.AddRange(Messages);
		}
	}

	public void MarkReadByRecipient(List<string> ids, DateTime readTime, List<Message> readMessages)
	{
		lock (_lock)
		{
			foreach (Message message in Messages)
			{
				if (!message.ReadTime.HasValue && message.IsSent && ids.Contains(message.Id))
				{
					message.ReadTime = readTime;
					readMessages.Add(message);
				}
			}
		}
	}
}
public class MigrationManager : SkyFrostModule
{
	private ConcurrentDictionary<string, AccountMigrationTask> _migrationTasks = new ConcurrentDictionary<string, AccountMigrationTask>();

	private DateTime nextMigrationTaskUpdate = DateTime.MinValue;

	public IEnumerable<AccountMigrationTask> MigrationTasks
	{
		get
		{
			TryScheduleMigrationTaskUpdate();
			foreach (KeyValuePair<string, AccountMigrationTask> migrationTask in _migrationTasks)
			{
				yield return migrationTask.Value;
			}
		}
	}

	public MigrationManager(SkyFrostInterface cloud)
		: base(cloud)
	{
	}

	internal void Reset()
	{
		_migrationTasks.Clear();
		nextMigrationTaskUpdate = default(DateTime);
	}

	public AccountMigrationTask TryGetMigrationTask(string taskId)
	{
		if (taskId == null)
		{
			return null;
		}
		TryScheduleMigrationTaskUpdate();
		_migrationTasks.TryGetValue(taskId, out var value);
		return value;
	}

	public void UpdateMigrationTasksInSeconds(float seconds)
	{
		nextMigrationTaskUpdate = DateTime.UtcNow.AddSeconds(seconds);
	}

	public void ForceMigrationTaskUpdate()
	{
		UpdateMigrationTasksInSeconds(-1f);
		TryScheduleMigrationTaskUpdate();
	}

	private void TryScheduleMigrationTaskUpdate()
	{
		if (base.Cloud.Session.CurrentUser == null || !(DateTime.UtcNow > nextMigrationTaskUpdate))
		{
			return;
		}
		nextMigrationTaskUpdate = DateTime.UtcNow.AddMinutes(5.0);
		Task.Run(async delegate
		{
			CloudResult<List<AccountMigrationTask>> cloudResult = await GetMigrations().ConfigureAwait(continueOnCapturedContext: false);
			if (cloudResult.IsOK)
			{
				TimeSpan? timeSpan = null;
				foreach (AccountMigrationTask task in cloudResult.Entity)
				{
					_migrationTasks.AddOrUpdate(task.TaskId, task, (string key, AccountMigrationTask existing) => task);
					if (task.State == MigrationState.Migrating)
					{
						timeSpan = TimeSpan.FromSeconds(5L);
					}
					else if (task.State == MigrationState.Waiting && (!timeSpan.HasValue || timeSpan.Value > TimeSpan.FromMinutes(1L)))
					{
						timeSpan = TimeSpan.FromMinutes(1L);
					}
				}
				if (timeSpan.HasValue)
				{
					nextMigrationTaskUpdate = DateTime.UtcNow.Add(timeSpan.Value);
				}
			}
		});
	}

	public Task<CloudResult<AccountMigrationTask>> GetMigration(string migrationId)
	{
		return base.Api.GET<AccountMigrationTask>("users/" + base.CurrentUserID + "/migrations/" + migrationId);
	}

	public Task<CloudResult<List<AccountMigrationTask>>> GetMigrations()
	{
		return base.Api.GET<List<AccountMigrationTask>>("users/" + base.CurrentUserID + "/migrations");
	}

	public Task<CloudResult<List<AccountMigrationTask>>> CreateMigrationTask(MigrationInitialization init, bool migrateFavorites, bool overwriteFavorites)
	{
		string text = "";
		if (migrateFavorites)
		{
			text = "?migrateFavorites=true";
			if (overwriteFavorites)
			{
				text += "&overwriteFavorites=true";
			}
		}
		return base.Api.POST<List<AccountMigrationTask>>("users/" + base.CurrentUserID + "/migrations" + text, init);
	}
}
public class ModerationManager : SkyFrostModule, IModerationClient
{
	public event Action<string> OnUserPublicBanned;

	public event Action<string> OnUserMuted;

	public event Action<string> OnUserSpectatorBanned;

	public ModerationManager(SkyFrostInterface cloud)
		: base(cloud)
	{
	}

	public async Task<CloudResult<bool>> IsPublicBanned(string id)
	{
		CloudResult cloudResult = await base.Api.GET("publicbans/" + id).ConfigureAwait(continueOnCapturedContext: false);
		bool.TryParse(cloudResult.Content, out var result);
		return new CloudResult<bool>(result, cloudResult.State, cloudResult.Headers, cloudResult.RequestAttempts, cloudResult.Content);
	}

	public Task UserPublicBanned(string userId)
	{
		this.OnUserPublicBanned?.Invoke(userId);
		return Task.CompletedTask;
	}

	public Task UserMuteBanned(string userId)
	{
		this.OnUserMuted?.Invoke(userId);
		return Task.CompletedTask;
	}

	public Task UserSpectatorBanned(string userId)
	{
		this.OnUserSpectatorBanned?.Invoke(userId);
		return Task.CompletedTask;
	}
}
public class FixedNetworkNodeSource : INetworkNodeManager
{
	[JsonPropertyName("nodes")]
	public List<NetworkNodeInfo> Nodes { get; set; }

	public event Action<INetworkNodeManager> AvailableNodesChanged;

	public FixedNetworkNodeSource(List<NetworkNodeInfo> nodes)
	{
		Nodes = nodes;
	}

	public Task ForceUpdate()
	{
		return Task.CompletedTask;
	}

	public IEnumerable<NetworkNodeInfo> GetNodes(NetworkNodeType type, int protocolVersion, NetworkNodePreference? preference, string universeId)
	{
		return Nodes.Where((NetworkNodeInfo n) => n.NodeType == type && n.ShouldUse(protocolVersion, preference, universeId));
	}

	public void Initialize(SkyFrostInterface SkyFrost)
	{
	}

	public NetworkNodeInfo TryGetNode(string id)
	{
		return Nodes.FirstOrDefault((NetworkNodeInfo n) => n.NodeId == id);
	}

	public async ValueTask<NetworkNodeInfo> TryGetNodeWithRefetch(string id)
	{
		return TryGetNode(id);
	}

	public void Update()
	{
	}
}
[JsonDerivedType(typeof(FixedNetworkNodeSource), "fixed")]
[JsonDerivedType(typeof(NetworkNodeManager), "dynamic")]
public interface INetworkNodeManager
{
	event Action<INetworkNodeManager> AvailableNodesChanged;

	void Initialize(SkyFrostInterface SkyFrost);

	Task ForceUpdate();

	IEnumerable<NetworkNodeInfo> GetNodes(NetworkNodeType type, int protocolVersion, NetworkNodePreference? preference, string universeId);

	NetworkNodeInfo TryGetNode(string id);

	ValueTask<NetworkNodeInfo> TryGetNodeWithRefetch(string id);

	void Update();
}
public delegate void LNL_PokeRequestHandler(string connectionUrl, string targetAddress, int targetPort);
public class NetworkNodeManager : INetworkNodeManager, IHubNetworkingClient
{
	private List<NetworkNodeInfo> _nodes;

	private DateTime _lastUpdateTime;

	[System.Text.Json.Serialization.JsonIgnore]
	public SkyFrostInterface Cloud { get; private set; }

	[System.Text.Json.Serialization.JsonIgnore]
	public ApiClient Api => Cloud.Api;

	public event Action<INetworkNodeManager> AvailableNodesChanged;

	public event LNL_PokeRequestHandler OnLNLPokeRequested;

	public void Initialize(SkyFrostInterface SkyFrost)
	{
		Cloud = SkyFrost;
	}

	public IEnumerable<NetworkNodeInfo> GetNodes(NetworkNodeType type, int protocolVersion, NetworkNodePreference? preference, string universeId)
	{
		List<NetworkNodeInfo> nodes = _nodes;
		if (nodes == null)
		{
			yield break;
		}
		foreach (NetworkNodeInfo item in nodes)
		{
			if (item.NodeType == type && item.ShouldUse(protocolVersion, preference, universeId))
			{
				yield return item;
			}
		}
	}

	public NetworkNodeInfo TryGetNode(string id)
	{
		return _nodes?.FirstOrDefault((NetworkNodeInfo n) => n.NodeId == id);
	}

	public async ValueTask<NetworkNodeInfo> TryGetNodeWithRefetch(string id)
	{
		NetworkNodeInfo networkNodeInfo = TryGetNode(id);
		if (networkNodeInfo != null)
		{
			return networkNodeInfo;
		}
		await ForceUpdate().ConfigureAwait(continueOnCapturedContext: false);
		return TryGetNode(id);
	}

	public void Update()
	{
		if (!((DateTime.UtcNow - _lastUpdateTime).TotalSeconds > NetworkNodeInfo.INFO_EXPIRATION.TotalSeconds / 2.0))
		{
			return;
		}
		_lastUpdateTime = DateTime.UtcNow;
		Task.Run(async delegate
		{
			try
			{
				await ForceUpdate().ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (Exception ex)
			{
				UniLog.Log("Exception when updating network nodes:\n" + ex);
			}
		});
	}

	public async Task ForceUpdate()
	{
		CloudResult<List<NetworkNodeInfo>> cloudResult = await GetNetworkNodes(Cloud.UniverseID);
		if (!cloudResult.IsOK)
		{
			return;
		}
		HashSet<string> hashSet = Pool.BorrowHashSet<string>();
		if (_nodes != null)
		{
			foreach (NetworkNodeInfo node in _nodes)
			{
				hashSet.Add(node.NodeId);
			}
		}
		bool flag = false;
		foreach (NetworkNodeInfo item in cloudResult.Entity)
		{
			if (!hashSet.Remove(item.NodeId))
			{
				flag = true;
				break;
			}
		}
		if (hashSet.Count > 0)
		{
			flag = true;
		}
		Pool.Return(ref hashSet);
		_nodes = cloudResult.Entity;
		if (flag)
		{
			this.AvailableNodesChanged?.Invoke(this);
		}
	}

	/// <summary>
	/// Submits a request to the cloud to obtain the network nodes for relays, bridges, etc.
	/// </summary>
	/// <param name="universeId">
	/// Universe ID to obtain nodes against.
	/// </param>
	/// <param name="nodePreference">
	/// What nodes are preferred for retrieval.
	/// </param>
	/// <returns>
	/// Nodes that are specified for the universe, and given node preference.
	/// </returns>
	public Task<CloudResult<List<NetworkNodeInfo>>> GetNetworkNodes(string universeId = "")
	{
		if (!string.IsNullOrEmpty(universeId))
		{
			return Api.GET<List<NetworkNodeInfo>>("networknodes?universeId=" + universeId);
		}
		return Api.GET<List<NetworkNodeInfo>>("networknodes");
	}

	public Task<CloudResult> UpdateNetworkNodeInfo(NetworkNodeInfo info)
	{
		return Api.POST("networknodes", info);
	}

	public Task ListenForLNLPokeRequests(string connectionUrl)
	{
		if (Cloud.UniverseID == null)
		{
			throw new InvalidOperationException("LNL pokes requests can only be listened to when universe is active");
		}
		return Cloud.HubClient.ListenForLNLPokeRequests(Cloud.UniverseID, connectionUrl);
	}

	public Task RequestLNLPoke(string connectionUrl, string address, int port)
	{
		if (Cloud.UniverseID == null)
		{
			throw new InvalidOperationException("LNL pokes can only be requested when universe is active");
		}
		return Cloud.HubClient.RequestLNLPoke(Cloud.UniverseID, connectionUrl, address, port);
	}

	public Task PokeOverLNL(string connectionUrl, string address, int port)
	{
		this.OnLNLPokeRequested?.Invoke(connectionUrl, address, port);
		return Task.CompletedTask;
	}
}
public class ProfileManager : SkyFrostModule
{
	private Uri[] _favorites;

	private Action<Uri>[] _favoriteListeners;

	private CloudVariableProxy[] _favoriteProxies;

	private string[] _favoriteVariablePaths;

	public ProfileManager(SkyFrostInterface cloud)
		: base(cloud)
	{
		_favorites = new Uri[Enum.GetValues(typeof(FavoriteEntity)).Length];
		_favoriteListeners = new Action<Uri>[_favorites.Length];
		_favoriteProxies = new CloudVariableProxy[_favorites.Length];
		_favoriteVariablePaths = new string[_favorites.Length];
	}

	public Uri GetCurrentFavorite(FavoriteEntity entity)
	{
		return _favorites[(int)entity];
	}

	public void SetFavorite(FavoriteEntity entity, Uri url)
	{
		url = FilterUrl(url);
		if (url == GetCurrentFavorite(entity))
		{
			return;
		}
		if (base.IsUserSignedIn)
		{
			CloudVariableProxy proxy = _favoriteProxies[(int)entity];
			proxy.SetValue(url);
			Task.Run(async delegate
			{
				try
				{
					await proxy.ForceWriteToCloud().ConfigureAwait(continueOnCapturedContext: false);
				}
				catch (Exception value)
				{
					UniLog.Error($"Exception when writing cloud variable to the cloud:\n{value}");
				}
			});
		}
		else if (url != null)
		{
			throw new InvalidOperationException($"Cannot set favorite {entity} without the user being signed in");
		}
		SafeInvoke(_favoriteListeners[(int)entity], url);
	}

	public async Task EnsureInitialized(FavoriteEntity entity)
	{
		while (_favoriteProxies[(int)entity].State == CloudVariableState.Uninitialized)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(50L)).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	public void RegisterListener(FavoriteEntity entity, Action<Uri> callback)
	{
		ref Action<Uri> reference = ref _favoriteListeners[(int)entity];
		reference = (Action<Uri>)Delegate.Combine(reference, callback);
	}

	public void UnregisterListener(FavoriteEntity entity, Action<Uri> callback)
	{
		ref Action<Uri> reference = ref _favoriteListeners[(int)entity];
		reference = (Action<Uri>)Delegate.Remove(reference, callback);
	}

	internal async Task SignIn()
	{
		if (base.CurrentUser == null)
		{
			return;
		}
		for (int i = 0; i < _favorites.Length; i++)
		{
			string text = base.Platform.FavoriteVariable((FavoriteEntity)i);
			_favoriteVariablePaths[i] = text;
			CloudVariableProxy cloudVariableProxy = base.Cloud.Variables.RequestProxy(base.CurrentUserID, text);
			cloudVariableProxy.Register(OnCloudVariableChanged);
			_favoriteProxies[i] = cloudVariableProxy;
			Uri uri = cloudVariableProxy.ReadValue<Uri>();
			if (uri != _favorites[i])
			{
				_favorites[i] = uri;
				SafeInvoke(_favoriteListeners[i], uri);
			}
		}
	}

	public void Reset()
	{
		Array.Clear(_favorites, 0, _favorites.Length);
		for (int i = 0; i < _favoriteListeners.Length; i++)
		{
			_favoriteProxies[i]?.Unregister(OnCloudVariableChanged);
			_favoriteProxies[i] = null;
			SafeInvoke(_favoriteListeners[i], null);
		}
		Array.Clear(_favoriteProxies, 0, _favoriteProxies.Length);
	}

	private void OnCloudVariableChanged(CloudVariableProxy proxy)
	{
		int num = _favoriteVariablePaths.FindIndex((string n) => n == proxy.Identity.path);
		if (num < 0)
		{
			UniLog.Warning("Could not found matching favorite entity for variable path: " + proxy.Identity.path);
			return;
		}
		Uri uri = proxy.ReadValue<Uri>();
		_favorites[num] = uri;
		SafeInvoke(_favoriteListeners[num], uri);
	}

	private Uri FilterUrl(Uri url)
	{
		if (url == null)
		{
			return null;
		}
		if (!url.IsAbsoluteUri)
		{
			return null;
		}
		if (url.Scheme != base.Platform.RecordScheme)
		{
			return null;
		}
		return url;
	}

	private void SafeInvoke(Action<Uri> events, Uri arg)
	{
		if (events == null)
		{
			return;
		}
		Delegate[] invocationList = events.GetInvocationList();
		for (int i = 0; i < invocationList.Length; i++)
		{
			Action<Uri> action = (Action<Uri>)invocationList[i];
			try
			{
				action(arg);
			}
			catch (Exception ex)
			{
				UniLog.Error("Exception invoking event:\n" + ex);
			}
		}
	}

	public Task<CloudResult> UpdateProfile(UserProfile profile)
	{
		base.CurrentUser.Profile = profile;
		return UpdateProfile(base.CurrentUserID, profile);
	}

	public Task<CloudResult> UpdateProfile(string userId, UserProfile profile)
	{
		return base.Api.PUT("users/" + userId + "/profile", profile);
	}
}
public class RecordsManager : SkyFrostModule
{
	private ConcurrentDictionary<Type, object> _recordBatchQueries = new ConcurrentDictionary<Type, object>();

	private ConcurrentDictionary<Type, object> _recordCaches = new ConcurrentDictionary<Type, object>();

	public Dictionary<Type, Dictionary<Uri, CloudResult>> cachedRecords = new Dictionary<Type, Dictionary<Uri, CloudResult>>();

	public RecordsManager(SkyFrostInterface cloud)
		: base(cloud)
	{
	}

	public RecordBatchQuery<R> RecordBatch<R>() where R : class, IRecord, new()
	{
		if (_recordBatchQueries.TryGetValue(typeof(R), out var value))
		{
			return (RecordBatchQuery<R>)value;
		}
		RecordBatchQuery<R> recordBatchQuery = new RecordBatchQuery<R>(this);
		_recordBatchQueries.TryAdd(typeof(R), recordBatchQuery);
		return recordBatchQuery;
	}

	public RecordCache<R> RecordCache<R>() where R : class, IRecord, new()
	{
		if (_recordCaches.TryGetValue(typeof(R), out var value))
		{
			return (RecordCache<R>)value;
		}
		RecordCache<R> recordCache = new RecordCache<R>(this);
		_recordCaches.TryAdd(typeof(R), recordCache);
		return recordCache;
	}

	public Uri GenerateRecordUri(string ownerId, string recordId)
	{
		return base.Cloud.Platform.GetRecordUri(ownerId, recordId);
	}

	public bool ExtractRecordID(Uri recordUri, out string ownerId, out string recordId, out bool isLinked)
	{
		return base.Cloud.Platform.ExtractRecordID(recordUri, out ownerId, out recordId, out isLinked);
	}

	public bool ExtractRecordID(Uri recordUri, out string ownerId, out string recordId)
	{
		return base.Cloud.Platform.ExtractRecordID(recordUri, out ownerId, out recordId);
	}

	public bool ExtractRecordPath(Uri recordUri, out string ownerId, out string recordPath, out bool isLinked)
	{
		return base.Cloud.Platform.ExtractRecordPath(recordUri, out ownerId, out recordPath, out isLinked);
	}

	public bool ExtractRecordPath(Uri recordUri, out string ownerId, out string recordPath)
	{
		return base.Cloud.Platform.ExtractRecordPath(recordUri, out ownerId, out recordPath);
	}

	public async Task<CloudResult<R>> GetRecordCached<R>(Uri recordUri, string accessKey = null) where R : class, IRecord, new()
	{
		lock (cachedRecords)
		{
			if (!cachedRecords.TryGetValue(typeof(R), out var value))
			{
				value = new Dictionary<Uri, CloudResult>();
				cachedRecords.Add(typeof(R), value);
			}
			if (value.TryGetValue(recordUri, out var value2))
			{
				return (CloudResult<R>)value2;
			}
		}
		CloudResult<R> cloudResult = await GetRecord<R>(recordUri, accessKey).ConfigureAwait(continueOnCapturedContext: false);
		lock (cachedRecords)
		{
			Dictionary<Uri, CloudResult> dictionary = cachedRecords[typeof(R)];
			dictionary.Remove(recordUri);
			dictionary.Add(recordUri, cloudResult);
		}
		return cloudResult;
	}

	public Task<CloudResult<R>> GetRecord<R>(Uri recordUri, string accessKey = null) where R : class, IRecord, new()
	{
		if (ExtractRecordID(recordUri, out var ownerId, out var recordId))
		{
			return GetRecord<R>(ownerId, recordId, accessKey);
		}
		if (ExtractRecordPath(recordUri, out ownerId, out var recordPath))
		{
			return GetRecordAtPath<R>(ownerId, recordPath, accessKey);
		}
		throw new ArgumentException("Uri is not a record URI");
	}

	public Task<CloudResult<R>> GetRecord<R>(string ownerId, string recordId, string accessKey = null, bool includeDeleted = false) where R : class, IRecord, new()
	{
		if (includeDeleted && accessKey != null)
		{
			throw new ArgumentNullException("accessKey and includeDeleted cannot be used at the same time");
		}
		string ownerPath = base.Cloud.GetOwnerPath(ownerId);
		string text = $"{ownerPath}/{ownerId}/records/{recordId}";
		if (includeDeleted)
		{
			text += "?includeDeleted=true";
		}
		else if (accessKey != null)
		{
			text = text + "?accessKey=" + Uri.EscapeDataString(accessKey);
		}
		return base.Api.GET<R>(text);
	}

	public Task<CloudResult<R>> GetRecordAtPath<R>(string ownerId, string path, string accessKey = null) where R : class, IRecord, new()
	{
		path = path.Replace("\\", "/");
		string ownerPath = base.Cloud.GetOwnerPath(ownerId);
		string text = $"{ownerPath}/{ownerId}/records/root/{path}";
		if (accessKey != null)
		{
			text = text + "?accessKey=" + Uri.EscapeDataString(accessKey);
		}
		return base.Api.GET<R>(text);
	}

	public Task<CloudResult<List<R>>> GetRecords<R>(List<RecordId> ids) where R : class, IRecord, new()
	{
		return base.Api.POST<List<R>>("records/list", ids);
	}

	public Task<CloudResult<List<R>>> GetRecords<R>(string ownerId, string tag = null, string path = null) where R : class, IRecord, new()
	{
		string ownerPath = base.Cloud.GetOwnerPath(ownerId);
		string value = "";
		if (tag != null)
		{
			value = "?tag=" + Uri.EscapeDataString(tag);
		}
		if (path != null)
		{
			value = "?path=" + Uri.EscapeDataString(path);
		}
		return base.Api.GET<List<R>>($"{ownerPath}/{ownerId}/records{value}");
	}

	public async IAsyncEnumerable<R> GetRecordsInHierarchy<R>(string ownerId, string path) where R : class, IRecord, new()
	{
		CloudResult<List<R>> result = null;
		for (int attempt = 0; attempt < 10; attempt++)
		{
			result = await GetRecords<R>(ownerId, null, path).ConfigureAwait(continueOnCapturedContext: false);
			if (result.IsOK)
			{
				break;
			}
			await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt)).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (!result.IsOK)
		{
			throw new Exception($"Failed to fetch records for {ownerId} at path {path} after several attempts.\nLast result: {result}");
		}
		foreach (R item in result.Entity)
		{
			yield return item;
		}
		foreach (R item2 in result.Entity)
		{
			if (!(item2.RecordType == "directory"))
			{
				continue;
			}
			string path2 = path + "\\" + item2.Name;
			await foreach (R item3 in GetRecordsInHierarchy<R>(ownerId, path2).ConfigureAwait(continueOnCapturedContext: false))
			{
				yield return item3;
			}
		}
	}

	public Task<CloudResult<SearchResults<R>>> FindRecords<R>(SearchParameters search, TimeSpan? timeout = null, bool throwOnError = true) where R : class, IRecord, new()
	{
		return base.Api.POST<SearchResults<R>>("records/pagedSearch", search, timeout, null, throwOnError);
	}

	public Task<CloudResult<CloudMessage>> UpsertRecord<R>(R record, bool ensureFolder = true) where R : class, IRecord, new()
	{
		string text = IdUtil.GetOwnerType(record.OwnerId) switch
		{
			OwnerType.User => "users/" + record.OwnerId + "/records/" + record.RecordId, 
			OwnerType.Group => "groups/" + record.OwnerId + "/records/" + record.RecordId, 
			_ => throw new Exception("Invalid record owner"), 
		};
		return base.Api.PUT<CloudMessage>(text + $"?ensureFolder={ensureFolder}", record);
	}

	public Task<CloudResult<RecordPreprocessStatus>> PreprocessRecord<R>(R record) where R : class, IRecord, new()
	{
		string resource = IdUtil.GetOwnerType(record.OwnerId) switch
		{
			OwnerType.User => $"users/{record.OwnerId}/records/{record.RecordId}/preprocess", 
			OwnerType.Group => $"groups/{record.OwnerId}/records/{record.RecordId}/preprocess", 
			_ => throw new Exception("Invalid record owner"), 
		};
		return base.Api.POST<RecordPreprocessStatus>(resource, record);
	}

	public Task<CloudResult<RecordPreprocessStatus>> GetPreprocessStatus(RecordPreprocessStatus status)
	{
		return GetPreprocessStatus(status.OwnerId, status.RecordId, status.PreprocessId);
	}

	public Task<CloudResult<RecordPreprocessStatus>> GetPreprocessStatus(string ownerId, string recordId, string id)
	{
		string resource = IdUtil.GetOwnerType(ownerId) switch
		{
			OwnerType.User => $"users/{ownerId}/records/{recordId}/preprocess/{id}", 
			OwnerType.Group => $"groups/{ownerId}/records/{recordId}/preprocess/{id}", 
			_ => throw new Exception("Invalid record owner"), 
		};
		return base.Api.GET<RecordPreprocessStatus>(resource);
	}

	public Task<CloudResult> DeleteRecord(IRecord record)
	{
		return DeleteRecord(record.OwnerId, record.RecordId);
	}

	public async Task<CloudResult> DeleteRecord(string ownerId, string recordId)
	{
		CloudResult result = await base.Api.DELETE("users/" + ownerId + "/records/" + recordId).ConfigureAwait(continueOnCapturedContext: false);
		base.Cloud.Storage.MarkStorageDirty(ownerId);
		return result;
	}

	public Task<CloudResult> AddTag(string ownerId, string recordId, string tag)
	{
		return IdUtil.GetOwnerType(ownerId) switch
		{
			OwnerType.User => base.Api.PUT($"users/{ownerId}/records/{recordId}/tags", tag), 
			OwnerType.Group => base.Api.PUT($"groups/{ownerId}/records/{recordId}/tags", tag), 
			_ => throw new Exception("Invalid record owner"), 
		};
	}

	public Task<CloudResult<List<RecordAuditInfo>>> GetRecordAuditLog(string ownerId, DateTime? from, DateTime? to)
	{
		string text = "";
		if (from.HasValue)
		{
			text = "?from=" + Uri.EscapeDataString(from.Value.ToString("s", CultureInfo.InvariantCulture));
		}
		if (to.HasValue)
		{
			text = ((text.Length <= 0) ? (text + "?") : (text + "&"));
			text = text + "to=" + Uri.EscapeDataString(to.Value.ToString("s", CultureInfo.InvariantCulture));
		}
		return IdUtil.GetOwnerType(ownerId) switch
		{
			OwnerType.User => base.Api.GET<List<RecordAuditInfo>>("users/" + ownerId + "/recordaudit" + text), 
			OwnerType.Group => base.Api.GET<List<RecordAuditInfo>>("groups/" + ownerId + "/recordaudit" + text), 
			_ => throw new Exception("Invalid record owner"), 
		};
	}

	public IAsyncEnumerable<RecordAuditInfo> EnumerateRecordAuditLog()
	{
		return EnumerateRecordAuditLog(base.Cloud.CurrentUserID);
	}

	public async IAsyncEnumerable<RecordAuditInfo> EnumerateRecordAuditLog(string ownerId)
	{
		DateTime last = default(DateTime);
		DateTime? to = null;
		CloudResult<List<RecordAuditInfo>> result;
		do
		{
			result = await GetRecordAuditLog(ownerId, null, to).ConfigureAwait(continueOnCapturedContext: false);
			if (!result.IsOK)
			{
				continue;
			}
			foreach (RecordAuditInfo r in result.Entity)
			{
				if (!(r.Timestamp == last))
				{
					if (!to.HasValue || r.Timestamp < to.Value)
					{
						to = r.Timestamp;
					}
					yield return r;
					last = r.Timestamp;
				}
			}
		}
		while (result.IsOK && result.Entity.Count > 0);
	}
}
public readonly struct CryptoData
{
	public readonly RSACryptoServiceProvider provider;

	public readonly RSAParameters parameters;

	public CryptoData(RSACryptoServiceProvider provider, RSAParameters parameters)
	{
		this.provider = provider;
		this.parameters = parameters;
	}
}
public class SecurityManager : SkyFrostModule
{
	private RSACryptoServiceProvider _cryptoProvider;

	public RSAParameters PublicKey { get; private set; }

	public SecurityManager(SkyFrostInterface cloud)
		: base(cloud)
	{
	}

	internal void ActivateSession(CryptoData cryptoData)
	{
		_cryptoProvider = cryptoData.provider;
		PublicKey = cryptoData.parameters;
	}

	internal void Reset()
	{
		_cryptoProvider?.Dispose();
		_cryptoProvider = null;
		PublicKey = default(RSAParameters);
	}

	public static Task<CryptoData> GenerateCryptoData()
	{
		return Task.Run(delegate
		{
			RSACryptoServiceProvider rSACryptoServiceProvider = new RSACryptoServiceProvider(2048);
			RSAParameters parameters = rSACryptoServiceProvider.ExportParameters(includePrivateParameters: false);
			return new CryptoData(rSACryptoServiceProvider, parameters);
		});
	}

	public byte[] SignHash(byte[] hash)
	{
		return _cryptoProvider.SignHash(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
	}

	public Task<CloudResult<OneTimeVerificationKey>> CreateKey(string baseId, VerificationKeyUse use, int expirationSeconds = 0)
	{
		string text = $"keyUse={use}";
		if (!string.IsNullOrWhiteSpace(baseId))
		{
			text = text + "&baseKeyId=" + Uri.EscapeDataString(baseId);
		}
		if (expirationSeconds > 0)
		{
			text += $"&expirationSeconds={expirationSeconds}";
		}
		return base.Api.POST<OneTimeVerificationKey>("users/" + base.CurrentUserID + "/onetimekeys?" + text, null);
	}

	public async Task<CloudResult<bool>> CheckContact(CheckContactData data)
	{
		CloudResult cloudResult = await base.Api.POST("users/" + data.OwnerId + "/checkContact", data).ConfigureAwait(continueOnCapturedContext: false);
		if (cloudResult.State == HttpStatusCode.OK)
		{
			return new CloudResult<bool>(result: true, cloudResult.State, cloudResult.Headers, cloudResult.RequestAttempts);
		}
		return new CloudResult<bool>(result: false, cloudResult.State, cloudResult.Headers, cloudResult.RequestAttempts, cloudResult.Content);
	}

	public Task<CloudResult<TOTP_Key>> InitializeTOTP()
	{
		return base.Api.POST<TOTP_Key>("users/" + base.CurrentUserID + "/totp", null);
	}

	public Task<CloudResult> ActivateTOTP(string code)
	{
		return base.Api.PATCH("users/" + base.CurrentUserID + "/totp?code=" + code, null);
	}

	public Task<CloudResult> DeactivateTOTP(string code)
	{
		return base.Api.DELETE("users/" + base.CurrentUserID + "/totp", null, code);
	}

	public async ValueTask EnsureIPAllowed()
	{
		if (!string.IsNullOrEmpty(base.Api.SecretClientAccessKey))
		{
			await TemporarilyAllowIP().ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	public Task<CloudResult> TemporarilyAllowIP()
	{
		return base.Api.POST("security/allowip", null);
	}
}
public class SessionManager : SkyFrostModule
{
	public static readonly TimeSpan SESSION_EXTEND_INTERVAL = TimeSpan.FromHours(1.0);

	public static readonly TimeSpan TOKEN_UPDATE_INTERVAL = TimeSpan.FromHours(12.0);

	private DateTime _lastSessionUpdate;

	private DateTime _lastTokenRefresh;

	private bool _updateCurrentUserInfo;

	private string _saml2Endpoint;

	private bool _useLegacyLogin;

	private UserSession _currentSession;

	private User _currentUser;

	private AuthenticationHeaderValue _currentAuthenticationHeader;

	private object _sessionLock = new object();

	public DateTime InitialSessionActivationTime { get; private set; }

	public new User CurrentUser
	{
		get
		{
			return _currentUser;
		}
		set
		{
			if (value != _currentUser)
			{
				_currentUser = value;
				this.UserUpdated?.Invoke(_currentUser);
			}
		}
	}

	public UserSession CurrentSession
	{
		get
		{
			return _currentSession;
		}
		private set
		{
			if (value == _currentSession)
			{
				return;
			}
			lock (_sessionLock)
			{
				if (_currentSession?.SessionToken != value?.SessionToken)
				{
					_lastSessionUpdate = DateTime.UtcNow;
				}
				_currentSession = value;
				if (value == null)
				{
					_currentAuthenticationHeader = null;
				}
				else
				{
					string text = value.SessionToken;
					if (value.IsMachineBound)
					{
						text = CryptoHelper.HashIDToToken(text, base.Cloud.SecretMachineId + base.Cloud.UID);
					}
					_currentAuthenticationHeader = new AuthenticationHeaderValue(base.Platform.AuthScheme, value.UserId + ":" + text);
				}
				try
				{
					this.SessionChanged?.Invoke(_currentSession);
				}
				catch (Exception ex)
				{
					UniLog.Error($"Exception in SessionChanged. CurrentSession: {CurrentSession}.\n" + ex);
				}
			}
		}
	}

	internal AuthenticationHeaderValue AuthenticationHeader => _currentAuthenticationHeader;

	public event Action<UserSession> SessionChanged;

	public event Action<User> UserUpdated;

	public event Action<List<Task>> OnFinalizeSession;

	public SessionManager(SkyFrostInterface cloud, string saml2Endpoint, bool useLegacyLogin)
		: base(cloud)
	{
		_saml2Endpoint = saml2Endpoint;
		_useLegacyLogin = useLegacyLogin;
	}

	public void ScheduleUpdateCurrentUserInfo()
	{
		_updateCurrentUserInfo = true;
	}

	public async Task<CloudResult<User>> UpdateCurrentUserInfo()
	{
		if (base.CurrentUserID == null)
		{
			throw new Exception("No current user!");
		}
		CloudResult<User> obj = await base.Cloud.Users.GetUser(base.CurrentUserID).ConfigureAwait(continueOnCapturedContext: false);
		User entity = obj.Entity;
		if (obj.IsOK && CurrentUser != null && base.CurrentUserID == entity.Id)
		{
			CurrentUser = entity;
		}
		return obj;
	}

	/// <summary>
	/// Login user and create new UserSession for authenticating further requests.
	/// To authenticate self, either password, session token or recover code should be provided.
	/// </summary>
	/// <param name="credential">User's credential. Typically username or email</param>
	/// <param name="authentication">Entity containing authentication information for given user</param>
	/// <param name="secretMachineId">Secret machine ID used to identity the machine that the login is from</param>
	/// <param name="rememberMe">Whether this session should be saved and reused. This keeps the validity of the session token for longer</param>
	/// <param name="totp">TOTP code when user has 2FA enabled</param>
	/// <returns></returns>
	public async Task<CloudResult<UserSessionResult<UserSession>>> Login(string credential, LoginAuthentication authentication, string secretMachineId, bool rememberMe, string totp)
	{
		if (_currentSession != null)
		{
			await Logout(isManual: false);
		}
		LoginCredentials loginCredentials = LoginCredentials.FromCredentialAuto(credential, authentication);
		loginCredentials.SecretMachineId = secretMachineId;
		loginCredentials.RememberMe = rememberMe;
		loginCredentials.MachineBound = true;
		Task<CryptoData> cryptoProviderTask = SecurityManager.GenerateCryptoData();
		CloudResult<UserSessionResult<UserSession>> result;
		if (_useLegacyLogin)
		{
			ApiClient api = base.Api;
			Dictionary<string, object> entity = GenerateLegacyLogin(credential, ((PasswordLogin)authentication).Password, secretMachineId, rememberMe, totp);
			string totpCode = totp;
			CloudResult<UserSession> cloudResult = await api.POST<UserSession>("userSessions", entity, null, totpCode).ConfigureAwait(continueOnCapturedContext: false);
			result = new CloudResult<UserSessionResult<UserSession>>(new UserSessionResult<UserSession>
			{
				Entity = cloudResult.Entity
			}, cloudResult);
		}
		else
		{
			ApiClient api2 = base.Api;
			string totpCode = totp;
			result = await api2.POST<UserSessionResult<UserSession>>("userSessions", loginCredentials, null, totpCode).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (result.IsOK)
		{
			base.Cloud.ProcessUserSessionResult(result.Entity);
			UserSession entity2 = result.Entity.Entity;
			await ActivateSession(entity2, await cryptoProviderTask.ConfigureAwait(continueOnCapturedContext: false)).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (result.Content != "TOTP")
		{
			UniLog.Warning("Error logging in: " + result.State.ToString() + "\n" + result.Content);
		}
		return result;
	}

	private Dictionary<string, object> GenerateLegacyLogin(string credential, string password, string secretMachineId, bool rememberMe = false, string totp = null)
	{
		if (_currentSession != null)
		{
			throw new InvalidOperationException("Cannot use legacy login when already logged in");
		}
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		if (credential.StartsWith("U-"))
		{
			dictionary.Add("ownerId", credential);
		}
		else if (ValidationHelper.IsValidEmail(credential))
		{
			dictionary.Add("email", credential);
		}
		else
		{
			dictionary.Add("username", credential);
		}
		dictionary.Add("password", password);
		dictionary.Add("secretMachineId", secretMachineId);
		dictionary.Add("rememberMe", rememberMe);
		dictionary.Add("uniqueDeviceID", base.Cloud.UID);
		if (!string.IsNullOrEmpty(totp))
		{
			dictionary.Add("totp", totp);
		}
		return dictionary;
	}

	/// <summary>
	/// This can be used to activate/continue an existing session, without explicit Login process.
	/// This method is useful in scenarios where the session needs to be continued without invalidating
	/// the existing session token. However it should be used ONLY in those circumstances, when it's absolutely necessary.
	/// Generally it's recommened to create new UserSession with the old token for security reasons, as it keeps the lifetime
	/// of those tokens pretty short.
	/// </summary>
	/// <param name="session">Existing session to activate</param>
	/// <param name="cryptoData">Crypto data for digital signing</param>
	/// <returns></returns>
	public async Task ActivateSession(UserSession session, CryptoData cryptoData)
	{
		InitialSessionActivationTime = DateTime.UtcNow;
		_lastTokenRefresh = DateTime.UtcNow;
		base.Cloud.Security.ActivateSession(cryptoData);
		CurrentSession = session;
		CurrentUser = new User
		{
			Id = CurrentSession.UserId
		};
		await Task.WhenAll(UpdateCurrentUserInfo(), base.Cloud.Storage.UpdateCurrentUserStorage()).ConfigureAwait(continueOnCapturedContext: false);
		await base.Cloud.Login().ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task FinalizeSession()
	{
		List<Task> list = new List<Task>();
		this.OnFinalizeSession?.Invoke(list);
		if (list.Count > 0)
		{
			await Task.WhenAll(list).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	public async Task Logout(bool isManual)
	{
		Task logoutTask = null;
		await base.Cloud.BeginLogout(isManual).ConfigureAwait(continueOnCapturedContext: false);
		if (CurrentSession != null && (!CurrentSession.RememberMe || isManual || CurrentSession.LogoutUrl != null || CurrentSession.LogoutUrlClientSide))
		{
			string _userId = CurrentSession.UserId;
			string _sessionToken = CurrentSession.SessionToken;
			UserSession _session = CurrentSession;
			logoutTask = Task.Run(async delegate
			{
				if (_session.OriginalLoginType == SessionLoginType.Saml2)
				{
					await LogoutSaml2(_session).ConfigureAwait(continueOnCapturedContext: false);
				}
				else
				{
					await base.Api.DELETE("userSessions/" + _userId + "/" + _sessionToken).ConfigureAwait(continueOnCapturedContext: false);
				}
			});
		}
		CurrentSession = null;
		CurrentUser = null;
		base.Cloud.ResetModules();
		if (logoutTask != null)
		{
			await logoutTask.ConfigureAwait(continueOnCapturedContext: false);
		}
		base.Cloud.CompleteLogout(isManual);
	}

	internal void Update()
	{
		if (_updateCurrentUserInfo && base.CurrentUserID != null)
		{
			_updateCurrentUserInfo = false;
			Task.Run((Func<Task<CloudResult<User>>?>)UpdateCurrentUserInfo);
		}
		lock (_sessionLock)
		{
			if (CurrentSession != null && DateTime.UtcNow - _lastSessionUpdate >= SESSION_EXTEND_INTERVAL)
			{
				bool updateToken = false;
				if (DateTime.UtcNow - _lastTokenRefresh >= TOKEN_UPDATE_INTERVAL)
				{
					updateToken = true;
				}
				Task.Run(async () => await ExtendSession(updateToken, base.Cloud.SecretMachineId).ConfigureAwait(continueOnCapturedContext: false));
				_lastSessionUpdate = DateTime.UtcNow;
			}
		}
	}

	public Task<CloudResult> LogoutAll(bool keepCurrent, string secretMachineId)
	{
		return base.Api.DELETE($"userSessions/{base.CurrentUserID}?keepCurrent={keepCurrent}&secretMachineId={Uri.EscapeDataString(secretMachineId)}");
	}

	public Task<CloudResult<UserSession>> GetExternalLogin(string key)
	{
		return base.Api.GET<UserSession>("externalLogins/" + key);
	}

	public Task<CloudResult> DeleteExternalLogin(string key)
	{
		return base.Api.DELETE("externalLogins/" + key);
	}

	public async Task<CloudResult> ExtendSession(bool updateToken = true, string secretMachineId = null)
	{
		CloudResult<UserSession> result = await base.Api.PATCH<UserSession>($"userSessions?updateToken={updateToken}&secretMachineId={Uri.EscapeDataString(secretMachineId)}", null);
		if (result.IsOK && result.Entity != null)
		{
			_lastTokenRefresh = DateTime.UtcNow;
			UniLog.Log("Extended & updated session token.");
			CurrentSession = result.Entity;
			await base.Cloud.ConnectToHub("SessionTokenRefresh for " + CurrentSession.UserId).ConfigureAwait(continueOnCapturedContext: false);
			await base.Cloud.OnSessionTokenRefresh().ConfigureAwait(continueOnCapturedContext: false);
			UniLog.Log("Logging out existing sessions for this machine");
			await LogoutAll(keepCurrent: true, base.Cloud.SecretMachineId).ConfigureAwait(continueOnCapturedContext: false);
		}
		else if (!result.IsOK)
		{
			UniLog.Log("Failed to extend & update session token: " + result);
		}
		return result;
	}

	public async Task<bool> LoginSaml2(string samlEntityId, string secretMachineId, CancellationToken cancellationToken)
	{
		await base.Cloud.Security.EnsureIPAllowed().ConfigureAwait(continueOnCapturedContext: false);
		string key = CryptoHelper.GenerateReadableCryptoToken(32);
		Task<CryptoData> cryptoProviderTask = SecurityManager.GenerateCryptoData();
		Process.Start(_saml2Endpoint + $"Login?samlEntityId={Uri.EscapeDataString(samlEntityId)}&loginId={Uri.EscapeDataString(key)}&secretMachineId={Uri.EscapeDataString(secretMachineId)}");
		DateTime start = DateTime.UtcNow;
		bool result;
		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				await Task.Delay(TimeSpan.FromSeconds(2.5), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (cancellationToken.IsCancellationRequested)
				{
					result = false;
				}
				else
				{
					CloudResult<UserSession> cloudResult = await GetExternalLogin(key).ConfigureAwait(continueOnCapturedContext: false);
					if (cloudResult.State == HttpStatusCode.NotFound && (DateTime.UtcNow - start).TotalMinutes > 2.0)
					{
						result = false;
					}
					else
					{
						if (cloudResult.State != HttpStatusCode.OK || cloudResult.Entity == null)
						{
							continue;
						}
						UserSession entity = cloudResult.Entity;
						await ActivateSession(entity, await cryptoProviderTask.ConfigureAwait(continueOnCapturedContext: false)).ConfigureAwait(continueOnCapturedContext: false);
						result = true;
					}
				}
				goto IL_0486;
			}
		}
		finally
		{
			await DeleteExternalLogin(key).ConfigureAwait(continueOnCapturedContext: false);
		}
		return false;
		IL_0486:
		return result;
	}

	private async Task LogoutSaml2(UserSession session)
	{
		if (session.OriginalLoginType != SessionLoginType.Saml2)
		{
			throw new InvalidOperationException("Session is not SAML 2.0");
		}
		if (!session.LogoutUrlClientSide)
		{
			throw new InvalidOperationException("LogoutURL is not clientside!");
		}
		await base.Cloud.Security.EnsureIPAllowed().ConfigureAwait(continueOnCapturedContext: false);
		Process.Start(_saml2Endpoint + "Logout?userId=" + session.UserId + "&sessionToken=" + Uri.EscapeDataString(session.SessionToken));
	}
}
public delegate void SessionsChangedHandler(bool sessionsAddedOrRemoved);
public class SessionsManager : SkyFrostModule
{
	private struct SessionInfoData
	{
		public SessionInfo info;

		public DateTime lastExternalUpdate;
	}

	private object _lock = new object();

	private Dictionary<string, SessionInfoData> sessions = new Dictionary<string, SessionInfoData>();

	private bool _sessionsChanged;

	private bool _sessionsAddedOrRemoved;

	private bool _initialPublicSessionsFetched;

	private bool _forceFetchRequested;

	private Task _initialPublicSessionFetchTask;

	private Dictionary<string, DateTime> _removedPublicSessions;

	private DateTime lastCleanup;

	private DictionaryList<string, Action<SessionInfo>> _sessionIdListeners = new DictionaryList<string, Action<SessionInfo>>();

	private DictionaryList<RecordId, Action<IReadOnlyList<SessionInfo>>> _worldIdListeners = new DictionaryList<RecordId, Action<IReadOnlyList<SessionInfo>>>();

	private bool ShouldDoFullFetch
	{
		get
		{
			if (!_initialPublicSessionsFetched || _forceFetchRequested)
			{
				return _initialPublicSessionFetchTask == null;
			}
			return false;
		}
	}

	public ISessionListingSettings? ListingSettings { get; set; }

	public event Action<SessionInfo> SessionAdded;

	public event Action<SessionInfo> SessionUpdated;

	public event Action<SessionInfo> SessionRemoved;

	public event SessionsChangedHandler SessionsChanged;

	public SessionsManager(SkyFrostInterface cloud)
		: base(cloud)
	{
	}

	public void ForceFetch()
	{
		_forceFetchRequested = true;
	}

	public void Update()
	{
		if (ShouldDoFullFetch)
		{
			_initialPublicSessionFetchTask = Task.Run(async delegate
			{
				try
				{
					CloudResult<List<SessionInfo>> cloudResult = await GetSessions(null, ListingSettings?.UniverseId).ConfigureAwait(continueOnCapturedContext: false);
					if (cloudResult.IsOK)
					{
						lock (_lock)
						{
							DateTime value = default(DateTime);
							foreach (SessionInfo item in cloudResult.Entity)
							{
								if ((ListingSettings == null || ListingSettings.AcceptSession(item)) && (!(_removedPublicSessions?.TryGetValue(item.SessionId, out value) ?? false) || !(value > item.LastUpdate)))
								{
									UpdateSessionInfo(item);
								}
							}
							_initialPublicSessionsFetched = true;
							_removedPublicSessions = null;
							_forceFetchRequested = false;
						}
					}
				}
				catch (Exception value2)
				{
					UniLog.Log($"Exception fetching initial public sessions:\n{value2}");
				}
				_initialPublicSessionFetchTask = null;
			});
		}
		if (_sessionsChanged)
		{
			bool sessionsAddedOrRemoved = _sessionsAddedOrRemoved;
			_sessionsAddedOrRemoved = false;
			_sessionsChanged = false;
			this.SessionsChanged?.Invoke(sessionsAddedOrRemoved);
		}
		if (!((DateTime.UtcNow - lastCleanup).TotalSeconds > 15.0))
		{
			return;
		}
		lastCleanup = DateTime.UtcNow;
		lock (_lock)
		{
			HashSet<string> hashSet = null;
			foreach (KeyValuePair<string, SessionInfoData> session in sessions)
			{
				if (session.Value.info.IsExpired || ShouldRemoveSession(session.Value))
				{
					if (hashSet == null)
					{
						hashSet = Pool.BorrowHashSet<string>();
					}
					hashSet.Add(session.Key);
				}
			}
			if (hashSet == null)
			{
				return;
			}
			foreach (string item2 in hashSet)
			{
				RemoveSession(item2, DateTime.UtcNow);
			}
			Pool.Return(ref hashSet);
		}
	}

	public bool IsValidSessionUrl(Uri sessionUri)
	{
		if (sessionUri.Scheme == base.Cloud.Platform.SessionScheme)
		{
			return sessionUri.Segments.Length == 2;
		}
		return false;
	}

	public string? TryGetSessionIdFromSessionUrl(Uri sessionUri)
	{
		if (IsValidSessionUrl(sessionUri))
		{
			return sessionUri.Segments[1];
		}
		return null;
	}

	public SessionInfo? TryGetInfo(Uri sessionUri)
	{
		return TryGetInfo(TryGetSessionIdFromSessionUrl(sessionUri));
	}

	public SessionInfo? TryGetInfo(string sessionId)
	{
		if (string.IsNullOrEmpty(sessionId))
		{
			return null;
		}
		lock (_lock)
		{
			sessions.TryGetValue(sessionId.ToLowerInvariant(), out var value);
			return value.info;
		}
	}

	public void GetSessions(List<SessionInfo> list)
	{
		lock (_lock)
		{
			foreach (KeyValuePair<string, SessionInfoData> session in sessions)
			{
				if (!session.Value.info.IsExpired)
				{
					list.Add(session.Value.info);
				}
			}
		}
	}

	public void GetNestedSessions(string sessionId, List<SessionInfo> nestedSessions)
	{
		lock (_lock)
		{
			foreach (KeyValuePair<string, SessionInfoData> session in sessions)
			{
				if (session.Value.info.ParentSessionIds != null && session.Value.info.ParentSessionIds.Any((string s) => s.Equals(sessionId, StringComparison.InvariantCultureIgnoreCase)))
				{
					nestedSessions.Add(session.Value.info);
				}
			}
		}
	}

	public void GetSessionsForWorldId(RecordId id, List<SessionInfo> infos)
	{
		lock (_lock)
		{
			foreach (KeyValuePair<string, SessionInfoData> session in sessions)
			{
				if (!(session.Value.info.ActualCorrespondingWorldId == null) && session.Value.info.ActualCorrespondingWorldId == id)
				{
					infos.Add(session.Value.info);
				}
			}
		}
	}

	public SessionInfo MatchSessionInfo(UserStatus status, UserSessionMetadata metadata)
	{
		lock (_lock)
		{
			return status.MatchSessionInfo(sessions.Values.Select((SessionInfoData d) => d.info), metadata);
		}
	}

	public void CreateSessionMap(UserStatus status, Dictionary<string, SessionInfo> map)
	{
		lock (_lock)
		{
			status.CreateSessionMap(sessions.Values.Select((SessionInfoData d) => d.info), map);
		}
	}

	private void RunSessionUpdated(SessionInfo info, string normalizedSessionId)
	{
		_sessionsChanged = true;
		this.SessionUpdated?.Invoke(info);
		lock (_sessionIdListeners)
		{
			List<Action<SessionInfo>> list = _sessionIdListeners.TryGetList(normalizedSessionId);
			if (list != null)
			{
				foreach (Action<SessionInfo> item in list)
				{
					try
					{
						item(info);
					}
					catch (Exception value)
					{
						UniLog.Error($"Exception in SessionId listener for {info.SessionId}:\n{value}", stackTrace: false);
					}
				}
			}
		}
		if (!(info.ActualCorrespondingWorldId != null))
		{
			return;
		}
		lock (_worldIdListeners)
		{
			List<Action<IReadOnlyList<SessionInfo>>> list2 = _worldIdListeners.TryGetList(info.ActualCorrespondingWorldId);
			if (list2 == null || list2.Count <= 0)
			{
				return;
			}
			List<SessionInfo> list3 = new List<SessionInfo>();
			GetSessionsForWorldId(info.ActualCorrespondingWorldId, list3);
			foreach (Action<IReadOnlyList<SessionInfo>> item2 in list2)
			{
				item2(list3);
			}
		}
	}

	public void UpdateSessionInfo(SessionInfo info, bool localUpdate = false)
	{
		if (info.SessionId == null)
		{
			throw new ArgumentNullException("SessionId");
		}
		string text = info.SessionId.ToLowerInvariant();
		lock (_lock)
		{
			if (sessions.TryGetValue(text, out var value))
			{
				if (info == value.info)
				{
					if (localUpdate)
					{
						RunSessionUpdated(info, text);
					}
					return;
				}
				if (info.LastUpdate <= value.info.LastUpdate)
				{
					if (!localUpdate)
					{
						value.lastExternalUpdate = info.LastUpdate;
						sessions[text] = value;
					}
					return;
				}
				if (value.info.IsOnLAN && !info.IsOnLAN)
				{
					info.CopyLAN_Data(value.info);
				}
				_sessionsChanged = true;
				value.info = info;
				sessions[text] = value;
				RunSessionUpdated(info, text);
			}
			else
			{
				SessionInfoData sessionInfoData = new SessionInfoData
				{
					info = info
				};
				if (!localUpdate)
				{
					sessionInfoData.lastExternalUpdate = info.LastUpdate;
				}
				if (!ShouldRemoveSession(sessionInfoData))
				{
					sessions.Add(text, sessionInfoData);
					_sessionsChanged = true;
					_sessionsAddedOrRemoved = true;
					this.SessionAdded?.Invoke(info);
				}
			}
		}
	}

	private bool ShouldRemoveSession(SessionInfoData data)
	{
		if (ListingSettings == null)
		{
			return false;
		}
		if (!ListingSettings.AcceptSession(data.info))
		{
			return true;
		}
		if (data.info.UniverseId != ListingSettings?.UniverseId)
		{
			return true;
		}
		return false;
	}

	public void RemoveSession(string sessionId, DateTime timestamp, bool isLocalRemoval = false)
	{
		lock (_lock)
		{
			string key = sessionId.ToLowerInvariant();
			if (sessions.TryGetValue(key, out var value))
			{
				if (isLocalRemoval && !SessionInfo.IsTimestampExpired(value.lastExternalUpdate))
				{
					UniLog.Log("Ignoring local removal for session: " + sessionId);
				}
				else if (isLocalRemoval || !(value.info.LastUpdate >= timestamp))
				{
					sessions.Remove(key);
					_sessionsChanged = true;
					_sessionsAddedOrRemoved = true;
					this.SessionRemoved?.Invoke(value.info);
				}
			}
			else if (!_initialPublicSessionsFetched)
			{
				if (_removedPublicSessions == null)
				{
					_removedPublicSessions = new Dictionary<string, DateTime>();
				}
				_removedPublicSessions.Add(sessionId, timestamp);
			}
		}
	}

	public void RegisterSessionIdChanges(string sessionId, Action<SessionInfo> callback, bool callImmediatelly = false)
	{
		sessionId = sessionId.ToLowerInvariant();
		lock (_sessionIdListeners)
		{
			_sessionIdListeners.Add(sessionId, callback);
		}
		if (callImmediatelly)
		{
			SessionInfo sessionInfo = TryGetInfo(sessionId);
			if (sessionInfo == null)
			{
				sessionInfo = new SessionInfo(sessionId);
			}
			callback(sessionInfo);
		}
	}

	public void UnregisterSessionIdChanges(string sessionId, Action<SessionInfo> callback)
	{
		sessionId = sessionId?.ToLowerInvariant();
		lock (_sessionIdListeners)
		{
			_sessionIdListeners.Remove(sessionId, callback);
		}
	}

	public void RegisterWorldIdChanges(RecordId worldId, Action<IReadOnlyList<SessionInfo>> callback, bool callImmediatelly = false)
	{
		lock (_worldIdListeners)
		{
			_worldIdListeners.Add(worldId, callback);
		}
		if (callImmediatelly)
		{
			List<SessionInfo> list = new List<SessionInfo>();
			GetSessionsForWorldId(worldId, list);
			callback(list);
		}
	}

	public void UnregisterWorldIdChanges(RecordId worldId, Action<IReadOnlyList<SessionInfo>> callback)
	{
		lock (_worldIdListeners)
		{
			_worldIdListeners.Remove(worldId, callback);
		}
	}

	public Task<CloudResult> UpdateSessions(SessionUpdate update)
	{
		return base.Api.PUT("sessions", update);
	}

	public Task<CloudResult<SessionInfo>> GetSession(Uri sessionUri)
	{
		string text = TryGetSessionIdFromSessionUrl(sessionUri);
		if (text == null)
		{
			throw new ArgumentException("Invalid session URI", "sessionUri");
		}
		return base.Api.GET<SessionInfo>("sessions/" + text);
	}

	public Task<CloudResult<SessionInfo>> GetSession(string sessionId)
	{
		return base.Api.GET<SessionInfo>("sessions/" + sessionId);
	}

	public Task<CloudResult<List<SessionInfo>>> GetSessions(string name = null, string universeId = null, string hostName = null, string hostId = null, int? minActiveUsers = null, bool includeEmptyHeadless = true)
	{
		StringBuilder stringBuilder = Pool.BorrowStringBuilder();
		StringBuilder stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler;
		if (!string.IsNullOrWhiteSpace(name))
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder3 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(6, 1, stringBuilder2);
			handler.AppendLiteral("&name=");
			handler.AppendFormatted(Uri.EscapeDataString(name));
			stringBuilder3.Append(ref handler);
		}
		if (!string.IsNullOrWhiteSpace(universeId))
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder4 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(12, 1, stringBuilder2);
			handler.AppendLiteral("&universeId=");
			handler.AppendFormatted(Uri.EscapeDataString(universeId));
			stringBuilder4.Append(ref handler);
		}
		if (!string.IsNullOrWhiteSpace(hostName))
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder5 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(10, 1, stringBuilder2);
			handler.AppendLiteral("&hostName=");
			handler.AppendFormatted(Uri.EscapeDataString(hostName));
			stringBuilder5.Append(ref handler);
		}
		if (!string.IsNullOrWhiteSpace(hostId))
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder6 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(8, 1, stringBuilder2);
			handler.AppendLiteral("&hostId=");
			handler.AppendFormatted(Uri.EscapeDataString(hostId));
			stringBuilder6.Append(ref handler);
		}
		if (minActiveUsers.HasValue)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder7 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(16, 1, stringBuilder2);
			handler.AppendLiteral("&minActiveUsers=");
			handler.AppendFormatted(minActiveUsers.Value);
			stringBuilder7.Append(ref handler);
		}
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder8 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(22, 1, stringBuilder2);
		handler.AppendLiteral("&includeEmptyHeadless=");
		handler.AppendFormatted(includeEmptyHeadless ? "true" : "false");
		stringBuilder8.Append(ref handler);
		if (stringBuilder.Length > 0)
		{
			stringBuilder[0] = '?';
		}
		string text = stringBuilder.ToString();
		Pool.Return(ref stringBuilder);
		return base.Api.GET<List<SessionInfo>>("sessions" + text);
	}

	public Task<CloudResult> UpdateSessionMetadata(CloudSessionMetadata metadata)
	{
		return base.Api.PUT("sessions/" + metadata.SessionId + "/metadata", metadata);
	}

	public Task<CloudResult<List<string>>> GetSessionURLs(string sessionId)
	{
		return base.Api.GET<List<string>>("sessions/" + sessionId + "/urls");
	}

	public string PrintoutSessions()
	{
		StringBuilder stringBuilder = new StringBuilder();
		lock (_lock)
		{
			foreach (KeyValuePair<string, SessionInfoData> session in sessions)
			{
				stringBuilder.AppendLine(session.Value.ToString());
			}
		}
		return stringBuilder.ToString();
	}
}
public class DefaultSessionListingSettings : ISessionListingSettings
{
	public bool HasUniverse => false;

	public string UniverseId => null;

	public bool AcceptSession(SessionInfo sessionInfo)
	{
		return true;
	}
}
public class StatisticsManager : SkyFrostModule
{
	private Dictionary<CreditType, List<CreditUser>> _userCredits = new Dictionary<CreditType, List<CreditUser>>();

	private DateTime _lastServerStatsUpdate;

	private DateTime _lastOnlineStatsUpdate;

	public string ServerStatusEndpoint { get; private set; }

	public ServerStatus ServerStatus
	{
		get
		{
			if ((DateTime.UtcNow - LastServerStateFetch).TotalSeconds >= 60.0)
			{
				return ServerStatus.NoInternet;
			}
			if ((DateTime.UtcNow - LastServerUpdate).TotalSeconds >= 60.0)
			{
				return ServerStatus.Down;
			}
			if (ServerResponseTime > 500)
			{
				return ServerStatus.Slow;
			}
			return ServerStatus.Good;
		}
	}

	public OnlineStats OnlineStats { get; private set; }

	public long ServerResponseTime { get; private set; }

	public DateTime LastServerUpdate { get; private set; }

	public DateTime LastServerStateFetch { get; private set; }

	public DateTime LastLocalServerResponse { get; private set; }

	public StatisticsManager(SkyFrostInterface cloud, string serverStatusEndpoint)
		: base(cloud)
	{
		ServerStatusEndpoint = serverStatusEndpoint;
	}

	public void Update()
	{
		if ((DateTime.UtcNow - _lastServerStatsUpdate).TotalSeconds >= 10.0)
		{
			Task.Run(async delegate
			{
				CloudResult<ServerStatistics> cloudResult = await GetServerStatistics().ConfigureAwait(continueOnCapturedContext: false);
				if (cloudResult.IsOK)
				{
					ServerResponseTime = cloudResult.Entity.ResponseTimeMilliseconds;
					LastServerUpdate = cloudResult.Entity.LastUpdate;
				}
				LastServerStateFetch = DateTime.UtcNow;
			});
			_lastServerStatsUpdate = DateTime.UtcNow;
		}
		if (!((DateTime.UtcNow - _lastOnlineStatsUpdate).TotalSeconds >= 10.0))
		{
			return;
		}
		Task.Run(async delegate
		{
			try
			{
				OnlineStats = (await GetOnlineStats().ConfigureAwait(continueOnCapturedContext: false)) ?? OnlineStats;
			}
			catch (Exception ex)
			{
				UniLog.Error("Exception when fetching OnlineUserstats:\n" + ex);
			}
		});
		_lastOnlineStatsUpdate = DateTime.UtcNow;
	}

	public Task<CloudResult> HealthCheck()
	{
		return base.Api.GET("testing/healthcheck");
	}

	public Task<CloudResult> Ping()
	{
		return base.Api.GET("testing/ping");
	}

	public Task<CloudResult> NotifyOnlineInstance(string machineId)
	{
		return base.Api.POST("stats/instanceOnline/" + machineId, null);
	}

	public async Task<CloudResult<ServerStatistics>> GetServerStatistics()
	{
		_ = 3;
		try
		{
			using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, ServerStatusEndpoint);
			HttpResponseMessage httpResponseMessage = await base.Cloud.SafeHttpClient.SendAsync(request).ConfigureAwait(continueOnCapturedContext: false);
			if (!httpResponseMessage.IsSuccessStatusCode)
			{
				return new CloudResult<ServerStatistics>(null, httpResponseMessage.StatusCode, null, 1);
			}
			if (SkyFrostInterface.UseNewtonsoftJson)
			{
				string value = await httpResponseMessage.Content.ReadAsStringAsync().ConfigureAwait(continueOnCapturedContext: false);
				if (string.IsNullOrEmpty(value))
				{
					return new CloudResult<ServerStatistics>(null, (HttpStatusCode)0, null, 1);
				}
				return new CloudResult<ServerStatistics>(JsonConvert.DeserializeObject<ServerStatistics>(value), HttpStatusCode.OK, null, 1);
			}
			if (httpResponseMessage.Content.Headers.ContentLength > 0)
			{
				using (Stream responseStream = await httpResponseMessage.Content.ReadAsStreamAsync().ConfigureAwait(continueOnCapturedContext: false))
				{
					return new CloudResult<ServerStatistics>(await System.Text.Json.JsonSerializer.DeserializeAsync<ServerStatistics>(responseStream).ConfigureAwait(continueOnCapturedContext: false), HttpStatusCode.OK, null, 1);
				}
			}
			return new CloudResult<ServerStatistics>(null, (HttpStatusCode)0, null, 1);
		}
		catch (Exception)
		{
			return new CloudResult<ServerStatistics>(null, (HttpStatusCode)0, null, 1);
		}
	}

	public async Task<OnlineStats> GetOnlineStats()
	{
		CloudResult<OnlineStats> cloudResult = await base.Api.GET<OnlineStats>("stats/onlineStats").ConfigureAwait(continueOnCapturedContext: false);
		if (cloudResult.IsOK && cloudResult.Entity != null)
		{
			return cloudResult.Entity;
		}
		return null;
	}

	public async Task<CloudStats> GetCloudStats()
	{
		CloudResult<CloudStats> cloudResult = await base.Api.GET<CloudStats>("stats/cloudStats").ConfigureAwait(continueOnCapturedContext: false);
		if (cloudResult.IsOK && cloudResult.Entity != null)
		{
			return cloudResult.Entity;
		}
		return null;
	}

	public async Task<GlobalFundingStats> GetFundingStats()
	{
		CloudResult<GlobalFundingStats> cloudResult = await base.Api.GET<GlobalFundingStats>("stats/fundingStats").ConfigureAwait(continueOnCapturedContext: false);
		if (cloudResult.IsOK && cloudResult.Entity != null)
		{
			return cloudResult.Entity;
		}
		return null;
	}

	public async Task<CloudResult<List<CreditUser>>> GetUserCredits(CreditType type)
	{
		string text = type switch
		{
			CreditType.Basic => "basic", 
			CreditType.Prominent => "prominent", 
			CreditType.Spoken => "spoken", 
			CreditType.Sponsor => "sponsor", 
			_ => throw new ArgumentException("Invalid credit type: " + type), 
		};
		CloudResult<List<CreditUser>> cloudResult = await base.Api.GET<List<CreditUser>>("supporters/credits/" + text);
		if (cloudResult.IsOK)
		{
			lock (_userCredits)
			{
				_userCredits[type] = new List<CreditUser>(cloudResult.Entity);
			}
		}
		return cloudResult;
	}

	public async Task<List<CreditUser>> GetUserCreditsCached(CreditType type)
	{
		lock (_userCredits)
		{
			if (_userCredits.TryGetValue(type, out var value))
			{
				return value;
			}
		}
		return (await GetUserCredits(type).ConfigureAwait(continueOnCapturedContext: false))?.Entity;
	}
}
public class StorageManager : SkyFrostModule
{
	public static float[] storageUpdateDelays = new float[4] { 1f, 5f, 15f, 30f };

	private bool _updateCurrentUserStorage;

	private Storage _currentUserStorage;

	private ConcurrentDictionary<string, bool> _storageDirty = new ConcurrentDictionary<string, bool>();

	private ConcurrentDictionary<string, bool> _updatingStorage = new ConcurrentDictionary<string, bool>();

	public long? CurrentStorageQuota => CurrentStorage?.QuotaBytes;

	public long? CurrentStorageUsed => CurrentStorage?.UsedBytes;

	public long? CurrentStorageFree => CurrentStorageQuota - CurrentStorageUsed;

	public Storage CurrentStorage
	{
		get
		{
			return _currentUserStorage;
		}
		set
		{
			if (value != _currentUserStorage)
			{
				_currentUserStorage = value;
				this.StorageUpdated?.Invoke(_currentUserStorage);
			}
		}
	}

	public event Action<Storage> StorageUpdated;

	public StorageManager(SkyFrostInterface cloud)
		: base(cloud)
	{
	}

	public void ScheduleUpdateCurrentUserStorage()
	{
		_updateCurrentUserStorage = true;
	}

	public void MarkUserStorageDirty()
	{
		MarkStorageDirty(base.CurrentUserID);
	}

	public void MarkStorageDirty(string ownerId)
	{
		_storageDirty.TryAdd(ownerId, value: true);
	}

	public async Task<CloudResult<Storage>> UpdateCurrentUserStorage()
	{
		if (base.CurrentUserID == null)
		{
			throw new Exception("No current user!");
		}
		CloudResult<Storage> cloudResult = await GetStorage(base.CurrentUserID).ConfigureAwait(continueOnCapturedContext: false);
		if (cloudResult.IsOK && base.CurrentUserID == cloudResult.Entity.OwnerId)
		{
			CurrentStorage = cloudResult.Entity;
		}
		return cloudResult;
	}

	public async Task UpdateStorage(string ownerId)
	{
		if (base.CurrentUser != null)
		{
			OwnerType ownerType = IdUtil.GetOwnerType(ownerId);
			string _signedUserId = base.CurrentUserID;
			float[] array = storageUpdateDelays;
			for (int i = 0; i < array.Length; i++)
			{
				await Task.Delay(TimeSpan.FromSeconds(array[i])).ConfigureAwait(continueOnCapturedContext: false);
				if (base.CurrentUserID != _signedUserId)
				{
					break;
				}
				if (ownerType == OwnerType.User)
				{
					await UpdateCurrentUserStorage().ConfigureAwait(continueOnCapturedContext: false);
				}
				else
				{
					await base.Cloud.Groups.UpdateGroupInfo(ownerId).ConfigureAwait(continueOnCapturedContext: false);
				}
			}
		}
		_updatingStorage.TryRemove(ownerId, out var _);
	}

	internal void Update()
	{
		if (_updateCurrentUserStorage && base.CurrentUserID != null)
		{
			_updateCurrentUserStorage = false;
			Task.Run((Func<Task<CloudResult<Storage>>?>)UpdateCurrentUserStorage);
		}
		foreach (KeyValuePair<string, bool> item in _storageDirty)
		{
			if (_updatingStorage.TryAdd(item.Key, value: true))
			{
				string _ownerId = item.Key;
				Task.Run(async delegate
				{
					await UpdateStorage(_ownerId).ConfigureAwait(continueOnCapturedContext: false);
				});
			}
		}
	}

	internal void Reset()
	{
		CurrentStorage = null;
	}

	public Task<CloudResult<Storage>> GetStorage(string ownerId)
	{
		return base.Api.GET<Storage>(base.Cloud.GetOwnerPath(ownerId) + "/" + ownerId + "/storage");
	}

	public Task<CloudResult<Storage>> GetMemberStorage(string ownerId, string userId)
	{
		return base.Api.GET<Storage>($"groups/{ownerId}/members/{userId}/storage");
	}
}
public class UsersManager : SkyFrostModule
{
	public UsersManager(SkyFrostInterface cloud)
		: base(cloud)
	{
	}

	public Task<CloudResult<User>> GetUser(string userId, string banAccessKey = null)
	{
		string text = "";
		if (banAccessKey != null)
		{
			text = "?banAccessKey=" + Uri.EscapeDataString(banAccessKey);
		}
		return base.Api.GET<User>("users/" + userId + text);
	}

	public Task<CloudResult<User>> GetUserByName(string username)
	{
		return base.Api.GET<User>("users?name=" + Uri.EscapeDataString(username) + "&exactMatch=true");
	}

	public Task<CloudResult<User>> GetUserByNameLegacy(string username)
	{
		return base.Api.GET<User>("users/" + Uri.EscapeDataString(username) + "?byUsername=true");
	}

	public Task<CloudResult<List<User>>> GetUsers(string searchName)
	{
		return base.Api.GET<List<User>>("users?name=" + Uri.EscapeDataString(searchName));
	}

	public Task<CloudResult<RSAParametersData>> GetPublicKey(string userId, string userSessionId)
	{
		UserStatus contactSession = base.Cloud.Contacts.GetContactSession(userId, userSessionId);
		if (contactSession != null)
		{
			return Task.FromResult(new CloudResult<RSAParametersData>(contactSession.PublicRSAKey, HttpStatusCode.OK, null, 0));
		}
		return base.Api.GET<RSAParametersData>("users/" + userId + "/rsa/" + userSessionId);
	}

	public Task<CloudResult> UpdatePublicKey(string userId, string userSessionId, RSAParametersData key)
	{
		return base.Api.PUT("users/" + userId + "/rsa/" + userSessionId, key);
	}

	public async Task<CloudResult<User>> GetUserCached(string userId)
	{
		return await GetUser(userId).ConfigureAwait(continueOnCapturedContext: false);
	}

	public Task<CloudResult> RequestAccountDeletion(LoginCredentials credentials, string totp)
	{
		return base.Api.POST("users/requestAccountDeletion", credentials, null, totp);
	}

	public Task<CloudResult> CancelAccountDeletion(LoginCredentials credentials, string totp)
	{
		return base.Api.POST("users/cancelAccountDeletion", credentials, null, totp);
	}

	/// <summary>
	/// Submits a new user registration
	/// </summary>
	/// <param name="username"></param>
	/// <param name="email"></param>
	/// <param name="password"></param>
	/// <param name="dateOfBirth"></param>
	/// <returns></returns>
	public async Task<CloudResult<RegistrationStatus>> Register(string username, string email, string password, DateTimeOffset dateOfBirth)
	{
		await base.Cloud.Session.Logout(isManual: false).ConfigureAwait(continueOnCapturedContext: false);
		return await base.Api.POST<RegistrationStatus>("users", new RegistrationRequest
		{
			Username = username,
			Email = email,
			DateOfBirth = dateOfBirth,
			Password = new PasswordLogin(password)
		}).ConfigureAwait(continueOnCapturedContext: false);
	}

	/// <summary>
	/// Gets the registration status of a user
	/// </summary>
	/// <param name="userId"></param>
	/// <param name="token"></param>
	/// <returns></returns>
	public async Task<CloudResult<RegistrationStatus>> GetRegistrationStatus(string userId, string token)
	{
		return await base.Api.GET<RegistrationStatus>("users/" + userId + "/registration?token=" + Uri.EscapeDataString(token)).ConfigureAwait(continueOnCapturedContext: false);
	}

	/// <summary>
	/// Gets session data for a user whose registration finished processing
	/// </summary>
	/// <param name="userId"></param>
	/// <param name="token"></param>
	/// <returns></returns>
	public async Task<CloudResult<UserSessionResult<User>>> GetRegisteredSession(string userId, string token)
	{
		CloudResult<UserSessionResult<User>> result = await base.Api.GET<UserSessionResult<User>>("users/" + userId + "/registration/session?token=" + Uri.EscapeDataString(token)).ConfigureAwait(continueOnCapturedContext: false);
		if (result.IsOK)
		{
			await base.Cloud.Security.EnsureIPAllowed().ConfigureAwait(continueOnCapturedContext: false);
			base.Cloud.ProcessUserSessionResult(result.Entity);
		}
		return result;
	}

	public Task<CloudResult> RequestRecoveryCode(string email)
	{
		return base.Api.POST("users/requestlostpassword", new User
		{
			Email = email
		});
	}

	public Task<CloudResult<List<PatreonFundingEvent>>> GetPatreonFundingEvents()
	{
		return base.Api.GET<List<PatreonFundingEvent>>("users/" + base.CurrentUserID + "/funding/patreon");
	}

	public Task<CloudResult<List<ExitMessage>>> GetExitMessages()
	{
		return base.Api.GET<List<ExitMessage>>("users/" + base.CurrentUserID + "/exitMessages");
	}
}
public class UserStatusManager : SkyFrostModule
{
	public bool QuietMode;

	public float AwayActivateSeconds = 60f;

	private OnlineStatus _onlineStatus;

	private UserStatus status;

	private DateTime lastSessionChangeTimestamp;

	private DateTime lastPublicKeyUpdateTimestamp;

	private CancellationTokenSource publicKeyCancellationToken;

	private bool _forceUpdate;

	private object _lock = new object();

	public bool ForceInvisible { get; set; }

	public IUserStatusSource StatusSource { get; set; }

	public string UserSessionId => status?.UserSessionId;

	public bool LoadingOnlineStatus => StatusSource?.LoadingOnlineStatus ?? false;

	public OnlineStatus OnlineStatus
	{
		get
		{
			return _onlineStatus;
		}
		set
		{
			if (value != _onlineStatus)
			{
				_onlineStatus = value;
				StatusSource?.OnlineStatusChanged(_onlineStatus);
			}
		}
	}

	public bool IsPresent { get; private set; }

	public bool IsAutoAway { get; private set; }

	public UserStatusManager(SkyFrostInterface cloud)
		: base(cloud)
	{
		InitializeNewStatus();
	}

	internal void Update()
	{
		IUserStatusSource statusSource = StatusSource;
		if (statusSource != null && statusSource.IsUserPresent)
		{
			IsPresent = true;
			if (OnlineStatus == OnlineStatus.Away && IsAutoAway)
			{
				_onlineStatus = OnlineStatus.Online;
				IsAutoAway = false;
			}
		}
		else
		{
			IsPresent = false;
			if (OnlineStatus == OnlineStatus.Online)
			{
				_onlineStatus = OnlineStatus.Away;
				IsAutoAway = true;
			}
		}
		if (OnlineStatus != OnlineStatus.Away)
		{
			IsAutoAway = false;
		}
		lock (_lock)
		{
			if (status != null)
			{
				DoUpdate();
			}
		}
	}

	internal void ForceUpdate()
	{
		_forceUpdate = true;
	}

	private void UpdatePublicKey()
	{
		if ((DateTime.UtcNow - lastPublicKeyUpdateTimestamp).TotalHours < 6.0 || publicKeyCancellationToken != null)
		{
			return;
		}
		publicKeyCancellationToken = new CancellationTokenSource();
		CancellationToken token = publicKeyCancellationToken.Token;
		UserStatus _status = status;
		Task.Run(async delegate
		{
			try
			{
				if ((await base.Cloud.Users.UpdatePublicKey(_status.UserId, _status.UserSessionId, _status.PublicRSAKey).ConfigureAwait(continueOnCapturedContext: false)).IsOK && !token.IsCancellationRequested)
				{
					lastPublicKeyUpdateTimestamp = DateTime.UtcNow;
				}
			}
			catch (Exception ex)
			{
				UniLog.Error("Exception when updating public key:\n" + ex);
			}
			finally
			{
				if (!token.IsCancellationRequested)
				{
					publicKeyCancellationToken = null;
				}
			}
		});
	}

	private void UpdateStatus()
	{
		bool flag = _onlineStatus == OnlineStatus.Invisible || _onlineStatus == OnlineStatus.Offline;
		bool flag2 = status.OnlineStatus == OnlineStatus.Invisible || status.OnlineStatus == OnlineStatus.Offline;
		bool flag3 = StatusSource?.BeginUpdate() ?? false;
		flag3 |= _forceUpdate;
		OutputDevice? outputDevice = (flag ? ((OutputDevice?)null) : StatusSource?.OutputDevice);
		flag3 |= outputDevice != status.OutputDevice || (DateTime.UtcNow - status.LastStatusChange > UserStatus.StatusHeartbeat && !flag);
		if (status.SessionType == UserSessionType.GraphicalClient)
		{
			flag3 = ((!flag) ? (flag3 | (_onlineStatus != status.OnlineStatus || IsPresent != status.IsPresent)) : (flag3 || !flag2));
		}
		if (StatusSource != null && !flag)
		{
			DateTime dateTime = StatusSource.LastSessionChangeTimestamp;
			bool flag4 = dateTime != lastSessionChangeTimestamp;
			if (flag3 || flag4)
			{
				flag3 |= StatusSource.UpdateSessions(status, flag3);
				lastSessionChangeTimestamp = dateTime;
			}
		}
		if (!flag3)
		{
			return;
		}
		if (status.SessionType == UserSessionType.Headless)
		{
			status.OnlineStatus = null;
			status.OutputDevice = null;
			status.IsPresent = false;
		}
		else if (flag)
		{
			SetStatusToOffline();
		}
		else
		{
			status.OnlineStatus = _onlineStatus;
			status.OutputDevice = outputDevice;
			status.IsPresent = IsPresent;
			if (IsPresent)
			{
				status.LastPresenceTimestamp = StatusSource?.LastPresenceTimestamp;
			}
		}
		status.LastStatusChange = DateTime.UtcNow;
		_forceUpdate = false;
		if (!(flag && flag2))
		{
			UserStatus _status = status;
			Task.Run(async delegate
			{
				await base.Cloud.HubClient.BroadcastStatus(_status, BroadcastTarget.ALL_CONTACTS).ConfigureAwait(continueOnCapturedContext: false);
			});
		}
	}

	private void DoUpdate()
	{
		if (status != null)
		{
			UpdatePublicKey();
			UpdateStatus();
		}
	}

	internal void SignIn()
	{
		lock (_lock)
		{
			InitializeNewStatus();
			_onlineStatus = OnlineStatus.Invisible;
			StatusSource?.SignIn();
		}
	}

	internal void SendStatusToUser(string userId)
	{
		UniLog.Log($"SendStatusToUser: {userId}. OnlineStatus: {status.OnlineStatus}");
		if (status.OnlineStatus != OnlineStatus.Invisible)
		{
			UserStatus _status = status;
			Task.Run(async delegate
			{
				await base.Cloud.HubClient.BroadcastStatus(_status, BroadcastTarget.ToContact(userId)).ConfigureAwait(continueOnCapturedContext: false);
			});
		}
	}

	internal async Task SignOut()
	{
		if (status == null)
		{
			return;
		}
		Task task;
		lock (_lock)
		{
			bool num = status.OnlineStatus == OnlineStatus.Invisible || status.OnlineStatus == OnlineStatus.Offline;
			SetStatusToOffline();
			if (!num)
			{
				UserStatus _status = status;
				task = Task.Run(async delegate
				{
					await base.Cloud.HubClient.BroadcastStatus(_status, BroadcastTarget.ALL_CONTACTS).ConfigureAwait(continueOnCapturedContext: false);
				});
			}
			else
			{
				task = Task.CompletedTask;
			}
			status = null;
		}
		if (StatusSource != null)
		{
			await StatusSource.SignOut().ConfigureAwait(continueOnCapturedContext: false);
		}
		await task.ConfigureAwait(continueOnCapturedContext: false);
	}

	private void SetStatusToOffline()
	{
		status.OnlineStatus = OnlineStatus.Offline;
		status.LastStatusChange = DateTime.UtcNow;
		status.IsPresent = false;
		status.Sessions.Clear();
		status.CurrentSessionIndex = -1;
	}

	private void InitializeNewStatus()
	{
		if (base.Cloud.Session.CurrentUser == null)
		{
			status = null;
			return;
		}
		publicKeyCancellationToken?.Cancel();
		publicKeyCancellationToken = null;
		lastSessionChangeTimestamp = default(DateTime);
		lastPublicKeyUpdateTimestamp = default(DateTime);
		status = new UserStatus();
		status.UserId = base.CurrentUserID;
		status.UserSessionId = Guid.CreateVersion7().ToString();
		status.SessionType = StatusSource?.SessionType ?? UserSessionType.Unknown;
		status.IsMobile = StatusSource?.IsMobile ?? false;
		status.AppVersion = StatusSource?.AppVersion;
		status.PublicRSAKey = base.Cloud.Security.PublicKey;
		status.OnlineStatus = OnlineStatus.Offline;
		status.Sessions = new List<UserSessionMetadata>();
	}
}
public class VisitsManager : SkyFrostModule
{
	public VisitsManager(SkyFrostInterface cloud)
		: base(cloud)
	{
	}

	public Task<CloudResult> LogVisit(Visit visit)
	{
		return base.Api.POST("visits", visit);
	}
}
public enum InviteRequestResponse
{
	SendInvite,
	AddAsContact
}
public class InviteRequest
{
	/// <summary>
	/// Unique ID for the invite request. This is done for validation to prevent abuse.
	/// </summary>
	[JsonProperty(PropertyName = "inviteRequestId")]
	[JsonPropertyName("inviteRequestId")]
	public string InviteRequestId { get; set; }

	/// <summary>
	/// This is the user who is requesting UserId. We store it explicitly, because the invite request can be
	/// forwarded to the host - meaning the message the host receives won't be coming from the user who wants
	/// to be invited.
	/// </summary>
	[JsonProperty(PropertyName = "userIdToInvite")]
	[JsonPropertyName("userIdToInvite")]
	public string UserIdToInvite { get; set; }

	/// <summary>
	/// The username of the user to be invited. This is so it doesn't need to be fetched from the cloud
	/// </summary>
	[JsonProperty(PropertyName = "usernameToInvite")]
	[JsonPropertyName("usernameToInvite")]
	public string UsernameToInvite { get; set; }

	/// <summary>
	/// This is the user that the invite was original requested from. This is for when this is forwarded, so the
	/// system can show who is the originator of the forwarding
	/// </summary>
	[JsonProperty(PropertyName = "requestingFromUserId")]
	[JsonPropertyName("requestingFromUserId")]
	public string RequestingFromUserId { get; set; }

	/// <summary>
	/// Username of the user who the invite was originally requested from. This is so it doesn't need to be re-fetched
	/// </summary>
	[JsonProperty(PropertyName = "requestingFromUsername")]
	[JsonPropertyName("requestingFromUsername")]
	public string RequestingFromUsername { get; set; }

	/// <summary>
	/// This is the session ID that this is being requested for. This is added when a user forwards this request.
	/// It's important to include this explicitly, because the user this is forwarded to might not be in the same
	/// session at the time they receive it.
	/// </summary>
	[JsonProperty(PropertyName = "forSessionId")]
	[JsonPropertyName("forSessionId")]
	public string ForSessionId { get; set; }

	/// <summary>
	/// The name of the session at the time of the request. This should be used as fallback to display the session
	/// name when it cannot be found based on the session ID, but when it can that should be preferred as it will
	/// have more up to date name of the session.
	/// </summary>
	[JsonProperty(PropertyName = "forSessionName")]
	[JsonPropertyName("forSessionName")]
	public string ForSessionName { get; set; }

	/// <summary>
	/// This indicates if the user who wants to be invited is currently contact with the host of the session.
	/// When the invite request is forwarded by the headless host, it'll fill this, so the user handling the
	/// request can get proper UI to add them as a host or no
	/// </summary>
	[JsonProperty(PropertyName = "isContactOfHost")]
	[JsonPropertyName("isContactOfHost")]
	public bool? IsContactWithHost { get; set; }

	/// <summary>
	/// This indicates what type of response is given to the invite request. This comes into play when the response
	/// is forwarded, because the action might need to be done on the different account than the one doing the
	/// actual resopnse - e.g. when headless forwards message to admin - the headless needs to know how to handle that  
	/// </summary>
	[JsonProperty(PropertyName = "response")]
	[JsonPropertyName("response")]
	[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
	[System.Text.Json.Serialization.JsonConverter(typeof(JsonStringEnumConverter))]
	public InviteRequestResponse? Response { get; set; }

	/// <summary>
	/// When the invite request is granted and needs to be forwarded, this contains the session info for the invite
	/// </summary>
	[JsonProperty(PropertyName = "invite")]
	[JsonPropertyName("invite")]
	public SessionInfo Invite { get; set; }

	[System.Text.Json.Serialization.JsonIgnore]
	[Newtonsoft.Json.JsonIgnore]
	public bool IsGranted
	{
		get
		{
			if (Invite == null)
			{
				return Response.HasValue;
			}
			return true;
		}
	}

	public InviteRequest Clone()
	{
		return System.Text.Json.JsonSerializer.Deserialize<InviteRequest>(System.Text.Json.JsonSerializer.Serialize(this));
	}

	public override string ToString()
	{
		return System.Text.Json.JsonSerializer.Serialize(this);
	}
}
/// <summary>
/// Configuration of account migration task, determining what should be migrated.
/// </summary>
public class AccountMigrationConfig
{
	/// <summary>
	/// Should user profile be migrated?
	/// </summary>
	[JsonProperty(PropertyName = "migrateUserProfile")]
	[JsonPropertyName("migrateUserProfile")]
	public bool MigrateUserProfile { get; set; } = true;

	/// <summary>
	/// Should funding events be migrated?
	/// </summary>
	[JsonProperty(PropertyName = "migrateFundingEvents")]
	[JsonPropertyName("migrateFundingEvents")]
	public bool MigrateFundingEvents { get; set; } = true;

	/// <summary>
	/// Should exit messages be migrated?
	/// </summary>
	[JsonProperty(PropertyName = "migrateExitMessages")]
	[JsonPropertyName("migrateExitMessages")]
	public bool MigrateExitMessages { get; set; } = true;

	/// <summary>
	/// Should contacts be migrated?
	/// </summary>
	[JsonProperty(PropertyName = "migrateContacts")]
	[JsonPropertyName("migrateContacts")]
	public bool MigrateContacts { get; set; } = true;

	/// <summary>
	/// Should contact message history be migrated? 
	/// </summary>
	[JsonProperty(PropertyName = "migrateMessageHistory")]
	[JsonPropertyName("migrateMessageHistory")]
	public bool MigrateMessageHistory { get; set; } = true;

	/// <summary>
	/// Should cloud variables be migrated?
	/// </summary>
	[JsonProperty(PropertyName = "migrateCloudVariables")]
	[JsonPropertyName("migrateCloudVariables")]
	public bool MigrateCloudVariables { get; set; } = true;

	/// <summary>
	/// Should cloud variables be migrated?
	/// </summary>
	[JsonProperty(PropertyName = "migrateCloudVariableDefinitions")]
	[JsonPropertyName("migrateCloudVariableDefinitions")]
	public bool MigrateCloudVariableDefinitions { get; set; } = true;

	/// <summary>
	/// Should records beloning to user be migrated?
	/// Doesn't affect group migration
	/// </summary>
	[JsonProperty(PropertyName = "migrateUserRecords")]
	[JsonPropertyName("migrateUserRecords")]
	public bool MigrateUserRecords { get; set; } = true;

	/// <summary>
	/// Should the record audit record be migrated? This is shared for both user and groups (if they migrate)
	/// </summary>
	[JsonProperty(PropertyName = "migrateRecordAuditLog")]
	[JsonPropertyName("migrateRecordAuditLog")]
	public bool MigrateRecordAuditLog { get; set; } = true;

	/// <summary>
	/// Should the old cloud home be preserved? Homes migrate under a different RecordId to avoid conflicts
	/// When this is true, the migration system will force a favorite to the old home
	/// </summary>
	[JsonProperty(PropertyName = "preserveOldHome")]
	[JsonPropertyName("preserveOldHome")]
	public bool PreserveOldHome { get; set; }

	/// <summary>
	/// Optional list of records to migrate
	/// </summary>
	[JsonProperty(PropertyName = "recordsToMigrate")]
	[JsonPropertyName("recordsToMigrate")]
	public List<string> RecordsToMigrate { get; set; }

	/// <summary>
	/// Optional list of variables to migrate
	/// </summary>
	[JsonProperty(PropertyName = "variablesToMigrate")]
	[JsonPropertyName("variablesToMigrate")]
	public List<string> VariablesToMigrate { get; set; }

	/// <summary>
	/// Should only the latest records be migrated?
	/// </summary>
	[JsonProperty(PropertyName = "onlyNewRecords")]
	[JsonPropertyName("onlyNewRecords")]
	public bool OnlyNewRecords { get; set; }

	/// <summary>
	/// Force overwrite synced records even if there's a conflict
	/// </summary>
	[JsonProperty(PropertyName = "forceOverwrite")]
	[JsonPropertyName("forceOverwrite")]
	public bool ForceOverwrite { get; set; }

	/// <summary>
	/// Should groups be migrated?
	/// </summary>
	[JsonProperty(PropertyName = "migrateGroups")]
	[JsonPropertyName("migrateGroups")]
	public bool MigrateGroups { get; set; } = true;

	/// <summary>
	/// Optional list of groups to migrate. If empty, all of them will be migrated.
	/// </summary>
	[JsonProperty(PropertyName = "groupsToMigrate")]
	[JsonPropertyName("groupsToMigrate")]
	public HashSet<string> GroupsToMigrate { get; set; }

	/// <summary>
	/// True if this migration config has at least one migration option checked.
	/// </summary>
	public bool IsMigratingSomething
	{
		get
		{
			if (!MigrateUserProfile && !MigrateFundingEvents && !MigrateExitMessages && !MigrateContacts && !MigrateMessageHistory && !MigrateCloudVariables && !MigrateCloudVariableDefinitions && !MigrateUserRecords && !MigrateRecordAuditLog)
			{
				return MigrateGroups;
			}
			return true;
		}
	}

	public void ClearAll()
	{
		MigrateUserProfile = false;
		MigrateFundingEvents = false;
		MigrateExitMessages = false;
		MigrateContacts = false;
		MigrateMessageHistory = false;
		MigrateCloudVariables = false;
		MigrateCloudVariableDefinitions = false;
		MigrateUserRecords = false;
		MigrateRecordAuditLog = false;
		MigrateGroups = false;
	}
}
/// <summary>
/// Status of the migration task
/// </summary>
public class AccountMigrationStatus
{
	private DateTime? _lastSnapshot;

	private int _lastMigratedRecords;

	[System.Text.Json.Serialization.JsonIgnore]
	[Newtonsoft.Json.JsonIgnore]
	public TimeSpan? TotalTime => CompletedOn - StartedOn;

	/// <summary>
	/// Represents the "phase" of the transfer E.g. Contacts, User Records etc
	/// </summary>
	[JsonProperty(PropertyName = "phase")]
	[JsonPropertyName("phase")]
	public string Phase { get; set; }

	/// <summary>
	/// When was the migration started on
	/// </summary>
	[JsonProperty(PropertyName = "startedOn")]
	[JsonPropertyName("startedOn")]
	public DateTimeOffset? StartedOn { get; set; }

	/// <summary>
	/// When was the migration completed
	/// </summary>
	[JsonProperty(PropertyName = "completedOn")]
	[JsonPropertyName("completedOn")]
	public DateTimeOffset? CompletedOn { get; set; }

	/// <summary>
	/// Migration status of the records of the user
	/// </summary>
	[JsonProperty(PropertyName = "userRecordsStatus")]
	[JsonPropertyName("userRecordsStatus")]
	public RecordMigrationStatus UserRecordsStatus { get; set; } = new RecordMigrationStatus();

	/// <summary>
	/// Migration status of the variables of the user
	/// </summary>
	[JsonProperty(PropertyName = "userVariablesStatus")]
	[JsonPropertyName("userVariablesStatus")]
	public VariableMigrationStatus UserVariablesStatus { get; set; } = new VariableMigrationStatus();

	/// <summary>
	/// Average number of records that are migrated per minute
	/// </summary>
	[JsonProperty(PropertyName = "recordsPerMinute")]
	[JsonPropertyName("recordsPerMinute")]
	public double RecordsPerMinute { get; set; }

	/// <summary>
	/// Name of the entity that's being currently migrated (either the username or group name)
	/// </summary>
	[JsonProperty(PropertyName = "currentlyMigratingName")]
	[JsonPropertyName("currentlyMigratingName")]
	public string CurrentlyMigratingName { get; set; }

	/// <summary>
	/// Name of the individual item that's being currently migrated
	/// </summary>
	[JsonProperty(PropertyName = "currentlyMigratingItem")]
	[JsonPropertyName("currentlyMigratingItem")]
	public string CurrentlyMigratingItem { get; set; }

	/// <summary>
	/// Total number of contacts (and their messages) to migrate
	/// </summary>
	[JsonProperty(PropertyName = "totalContactCount")]
	[JsonPropertyName("totalContactCount")]
	public int TotalContactCount { get; set; }

	/// <summary>
	/// Number of contacts that have already been fully migrated (including their message history)
	/// </summary>
	[JsonProperty(PropertyName = "migratedContactCount")]
	[JsonPropertyName("migratedContactCount")]
	public int MigratedContactCount { get; set; }

	/// <summary>
	/// Total number of messages that have been already migrated.
	/// </summary>
	[JsonProperty(PropertyName = "migratedMessageCount")]
	[JsonPropertyName("migratedMessageCount")]
	public int MigratedMessageCount { get; set; }

	/// <summary>
	/// Number of groups to migrate for given user
	/// </summary>
	[JsonProperty(PropertyName = "totalGroupCount")]
	[JsonPropertyName("totalGroupCount")]
	public int TotalGroupCount { get; set; }

	/// <summary>
	/// Number of groups that were completely migrated (including their records transferred)
	/// </summary>
	[JsonProperty(PropertyName = "migratedGroupCount")]
	[JsonPropertyName("migratedGroupCount")]
	public int MigratedGroupCount { get; set; }

	/// <summary>
	/// Assets that for some reason were missing on the source and could not be uploaded.
	/// Mainly for diagnostic purposes, but could be used in the future to try to relocate those.
	/// </summary>
	[JsonProperty(PropertyName = "missingAssets")]
	[JsonPropertyName("missingAssets")]
	public HashSet<string> MissingAssets { get; set; } = new HashSet<string>();

	/// <summary>
	/// If the migration failed
	/// </summary>
	[JsonProperty(PropertyName = "error")]
	[JsonPropertyName("error")]
	public string Error { get; set; }

	/// <summary>
	/// Indicates if the messages have failed to migrate
	/// </summary>
	[JsonProperty(PropertyName = "messagesFailed")]
	[JsonPropertyName("messagesFailed")]
	public bool MessagesFailed { get; set; }

	/// <summary>
	/// Indicates if the migration should be aborted
	/// </summary>
	[JsonProperty(PropertyName = "abort")]
	[JsonPropertyName("abort")]
	public bool Abort { get; set; }

	/// <summary>
	/// Migration statuses of the groups
	/// </summary>
	[JsonProperty(PropertyName = "groupStatuses")]
	[JsonPropertyName("groupStatuses")]
	public List<GroupMigrationStatus> GroupStatuses { get; set; }

	[System.Text.Json.Serialization.JsonIgnore]
	[Newtonsoft.Json.JsonIgnore]
	public int TotalMigratedMemberCount => GroupStatuses?.Sum((GroupMigrationStatus g) => g.MigratedMemberCount) ?? 0;

	[System.Text.Json.Serialization.JsonIgnore]
	[Newtonsoft.Json.JsonIgnore]
	public int TotalMigratedVariableCount => (UserVariablesStatus.MigratedVariableCount + GroupStatuses?.Sum((GroupMigrationStatus g) => g.VariablesStatus.MigratedVariableCount)).GetValueOrDefault();

	[System.Text.Json.Serialization.JsonIgnore]
	[Newtonsoft.Json.JsonIgnore]
	public int TotalMigratedVariableDefinitionCount => (UserVariablesStatus.MigratedVariableDefinitionCount + GroupStatuses?.Sum((GroupMigrationStatus g) => g.VariablesStatus.MigratedVariableDefinitionCount)).GetValueOrDefault();

	[System.Text.Json.Serialization.JsonIgnore]
	[Newtonsoft.Json.JsonIgnore]
	public int TotalRecordCount
	{
		get
		{
			int num = UserRecordsStatus.TotalRecordCount;
			if (GroupStatuses != null)
			{
				num += GroupStatuses.Sum((GroupMigrationStatus g) => g.RecordsStatus.TotalRecordCount);
			}
			return num;
		}
	}

	[System.Text.Json.Serialization.JsonIgnore]
	[Newtonsoft.Json.JsonIgnore]
	public int TotalProcessedRecordCount
	{
		get
		{
			int num = UserRecordsStatus.TotalProcessedRecordCount;
			if (GroupStatuses != null)
			{
				num += GroupStatuses.Sum((GroupMigrationStatus g) => g.RecordsStatus.TotalProcessedRecordCount);
			}
			return num;
		}
	}

	[System.Text.Json.Serialization.JsonIgnore]
	[Newtonsoft.Json.JsonIgnore]
	public int TotalFailedRecordCount
	{
		get
		{
			int num = UserRecordsStatus.FailedRecords?.Count ?? 0;
			if (GroupStatuses != null)
			{
				num += GroupStatuses.Sum((GroupMigrationStatus g) => g.RecordsStatus.FailedRecords?.Count ?? 0);
			}
			return num;
		}
	}

	public void RegisterMissingAsset(string hash)
	{
		lock (MissingAssets)
		{
			MissingAssets.Add(hash);
		}
	}

	public GroupMigrationStatus GetGroupStatus(string ownerId, string groupName)
	{
		GroupMigrationStatus groupMigrationStatus = GroupStatuses?.FirstOrDefault((GroupMigrationStatus s) => s.OwnerId == ownerId);
		if (groupMigrationStatus == null)
		{
			if (GroupStatuses == null)
			{
				GroupStatuses = new List<GroupMigrationStatus>();
			}
			groupMigrationStatus = new GroupMigrationStatus
			{
				OwnerId = ownerId,
				GroupName = groupName
			};
			GroupStatuses.Add(groupMigrationStatus);
		}
		return groupMigrationStatus;
	}

	public void UpdateStats()
	{
		DateTime utcNow = DateTime.UtcNow;
		DateTime? lastSnapshot = _lastSnapshot;
		double? num = (utcNow - lastSnapshot)?.TotalMinutes;
		bool num2 = num >= 2.5;
		int totalProcessedRecordCount = TotalProcessedRecordCount;
		if (num2)
		{
			int num3 = totalProcessedRecordCount - _lastMigratedRecords;
			RecordsPerMinute = (double)num3 / num.Value;
		}
		if (num2 || !num.HasValue)
		{
			_lastMigratedRecords = totalProcessedRecordCount;
			_lastSnapshot = DateTime.UtcNow;
		}
	}
}
[DataModelType]
public enum MigrationState
{
	Waiting,
	Migrating,
	Completed,
	Failed
}
/// <summary>
/// Task for a cloud-based account migration, representing its configuration and current status
/// </summary>
public class AccountMigrationTask
{
	/// <summary>
	/// Owner of the migration task (must be the user)
	/// </summary>
	[JsonProperty(PropertyName = "ownerId")]
	[JsonPropertyName("ownerId")]
	public string OwnerId { get; set; }

	/// <summary>
	/// Unique id of the migration task
	/// </summary>
	[JsonProperty(PropertyName = "id")]
	[JsonPropertyName("id")]
	public string TaskId { get; set; }

	/// <summary>
	/// Name of the migratoin task
	/// </summary>
	[JsonProperty(PropertyName = "name")]
	[JsonPropertyName("name")]
	public string Name { get; set; }

	/// <summary>
	/// Description of the account migration task
	/// </summary>
	[JsonProperty(PropertyName = "description")]
	[JsonPropertyName("description")]
	public string Description { get; set; }

	/// <summary>
	/// Current state of the migration task
	/// </summary>
	[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
	[System.Text.Json.Serialization.JsonConverter(typeof(JsonStringEnumConverter))]
	[JsonProperty(PropertyName = "state")]
	[JsonPropertyName("state")]
	public MigrationState State { get; set; }

	/// <summary>
	/// A rough estimate of where is this migration task in the queue (how many other tasks are in front of it)
	/// </summary>
	[JsonProperty(PropertyName = "estimatedQueuePosition")]
	[JsonPropertyName("estimatedQueuePosition")]
	public int? EstimatedQueuePosition { get; set; }

	/// <summary>
	/// When was this migration task created
	/// </summary>
	[JsonProperty(PropertyName = "createdOn")]
	[JsonPropertyName("createdOn")]
	public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// How many times has the migration started. Values larger than 1 indicate failures
	/// </summary>
	[JsonProperty(PropertyName = "startCount")]
	[JsonPropertyName("startCount")]
	public int StartCount { get; set; }

	/// <summary>
	/// Last main error that's been logged for this whole task. Only for debugging purposes
	/// </summary>
	[JsonProperty(PropertyName = "lastError")]
	[JsonPropertyName("lastError")]
	public string LastError { get; set; }

	/// <summary>
	/// Current status of the migration job
	/// </summary>
	[JsonProperty(PropertyName = "config")]
	[JsonPropertyName("config")]
	public AccountMigrationConfig Config { get; set; } = new AccountMigrationConfig();

	/// <summary>
	/// Current status of the migration job. This is a composite of all the run statuses, to provide a complete information
	/// in case the migration fails due to a bug. Statuses of the individual runs are in the RunStatuses
	/// </summary>
	[JsonProperty(PropertyName = "status")]
	[JsonPropertyName("status")]
	public AccountMigrationStatus Status { get; set; } = new AccountMigrationStatus();

	/// <summary>
	/// Indicates if contacts completed migration
	/// </summary>
	[JsonProperty(PropertyName = "contactsCompleted")]
	[JsonPropertyName("contactsCompleted")]
	public bool ContactsCompleted { get; set; }

	/// <summary>
	/// Indicates if user owned entities have completed migration
	/// </summary>
	[JsonProperty(PropertyName = "userOwnedCompleted")]
	[JsonPropertyName("userOwnedCompleted")]
	public bool UserOwnedCompleted { get; set; }

	/// <summary>
	/// Indicates which groups completed migration
	/// </summary>
	[JsonProperty(PropertyName = "groupsCompleted")]
	[JsonPropertyName("groupsCompleted")]
	public HashSet<string> GroupsCompleted { get; set; } = new HashSet<string>();

	/// <summary>
	/// Statuses of all the runs/attempts
	/// </summary>
	[JsonProperty(PropertyName = "runStatuses")]
	[JsonPropertyName("runStatuses")]
	public List<AccountMigrationStatus> RunStatuses { get; set; } = new List<AccountMigrationStatus>();

	public void UpdateStatus()
	{
		if (RunStatuses.Count == 0)
		{
			return;
		}
		AccountMigrationStatus accountMigrationStatus = RunStatuses.Last();
		if (!accountMigrationStatus.CompletedOn.HasValue)
		{
			accountMigrationStatus.UpdateStats();
		}
		Status.Abort = RunStatuses.Any((AccountMigrationStatus s) => s.Abort);
		Status.StartedOn = RunStatuses.Min((AccountMigrationStatus s) => s.StartedOn);
		Status.CompletedOn = RunStatuses.Max((AccountMigrationStatus s) => s.CompletedOn);
		Status.UserRecordsStatus.TotalRecordCount = RunStatuses.Max((AccountMigrationStatus s) => s.UserRecordsStatus.TotalRecordCount);
		Status.UserRecordsStatus.MigratedRecordCount = RunStatuses.Max((AccountMigrationStatus s) => s.UserRecordsStatus.MigratedRecordCount);
		Status.UserRecordsStatus.AlreadyMigratedRecordCount = RunStatuses.Max((AccountMigrationStatus s) => s.UserRecordsStatus.AlreadyMigratedRecordCount);
		Status.UserRecordsStatus.ConflictedRecordCount = RunStatuses.Max((AccountMigrationStatus s) => s.UserRecordsStatus.ConflictedRecordCount);
		Status.UserRecordsStatus.BytesToUpload = RunStatuses.Sum((AccountMigrationStatus s) => s.UserRecordsStatus.BytesToUpload);
		Status.UserRecordsStatus.BytesUploaded = RunStatuses.Sum((AccountMigrationStatus s) => s.UserRecordsStatus.BytesUploaded);
		Status.UserRecordsStatus.AssetsToUpload = RunStatuses.Sum((AccountMigrationStatus s) => s.UserRecordsStatus.AssetsToUpload);
		Status.UserRecordsStatus.AssetsUploaded = RunStatuses.Sum((AccountMigrationStatus s) => s.UserRecordsStatus.AssetsUploaded);
		if (Status.UserRecordsStatus.FailedRecords == null)
		{
			Status.UserRecordsStatus.FailedRecords = new List<RecordMigrationFailure>();
		}
		Status.UserRecordsStatus.FailedRecords.Clear();
		HashSet<string> hashSet = new HashSet<string>();
		foreach (AccountMigrationStatus runStatus in RunStatuses)
		{
			if (runStatus.UserRecordsStatus.FailedRecords == null)
			{
				continue;
			}
			foreach (RecordMigrationFailure failedRecord in runStatus.UserRecordsStatus.FailedRecords)
			{
				if (hashSet.Add(failedRecord.OwnerId + ":" + failedRecord.RecordId))
				{
					Status.UserRecordsStatus.FailedRecords.Add(failedRecord);
				}
			}
		}
		Status.UserVariablesStatus.MigratedVariableCount = RunStatuses.Max((AccountMigrationStatus s) => s.UserVariablesStatus.MigratedVariableCount);
		Status.UserVariablesStatus.MigratedVariableDefinitionCount = RunStatuses.Max((AccountMigrationStatus s) => s.UserVariablesStatus.MigratedVariableDefinitionCount);
		Status.TotalContactCount = RunStatuses.Max((AccountMigrationStatus s) => s.TotalContactCount);
		Status.MigratedContactCount = RunStatuses.Max((AccountMigrationStatus s) => s.MigratedContactCount);
		Status.MigratedMessageCount = RunStatuses.Max((AccountMigrationStatus s) => s.MigratedMessageCount);
		Status.TotalGroupCount = RunStatuses.Max((AccountMigrationStatus s) => s.TotalGroupCount);
		Status.MigratedGroupCount = RunStatuses.Max((AccountMigrationStatus s) => s.MigratedGroupCount);
		Status.CurrentlyMigratingName = accountMigrationStatus.CurrentlyMigratingName;
		Status.CurrentlyMigratingItem = accountMigrationStatus.CurrentlyMigratingItem;
		Status.RecordsPerMinute = accountMigrationStatus.RecordsPerMinute;
		if (Status.GroupStatuses != null)
		{
			foreach (GroupMigrationStatus groupStatus2 in Status.GroupStatuses)
			{
				groupStatus2.MigratedMemberCount = 0;
				groupStatus2.RecordsStatus.AssetsToUpload = 0;
				groupStatus2.RecordsStatus.AssetsUploaded = 0;
				groupStatus2.RecordsStatus.BytesToUpload = 0L;
				groupStatus2.RecordsStatus.BytesUploaded = 0L;
				groupStatus2.RecordsStatus.FailedRecords?.Clear();
			}
		}
		foreach (AccountMigrationStatus runStatus2 in RunStatuses)
		{
			if (runStatus2.GroupStatuses == null)
			{
				continue;
			}
			foreach (GroupMigrationStatus groupStatus3 in runStatus2.GroupStatuses)
			{
				GroupMigrationStatus groupStatus = Status.GetGroupStatus(groupStatus3.OwnerId, groupStatus3.GroupName);
				groupStatus.MigratedMemberCount = MathX.Max(groupStatus.MigratedMemberCount, groupStatus3.MigratedMemberCount);
				if (groupStatus.RecordsStatus.FailedRecords == null)
				{
					groupStatus.RecordsStatus.FailedRecords = new List<RecordMigrationFailure>();
				}
				if (groupStatus3.RecordsStatus.FailedRecords != null)
				{
					foreach (RecordMigrationFailure failedRecord2 in groupStatus3.RecordsStatus.FailedRecords)
					{
						if (hashSet.Add(failedRecord2.OwnerId + ":" + failedRecord2.RecordId))
						{
							groupStatus.RecordsStatus.FailedRecords.Add(failedRecord2);
						}
					}
				}
				MergeStatus(groupStatus.RecordsStatus, groupStatus3.RecordsStatus);
				MergeStatus(groupStatus.VariablesStatus, groupStatus3.VariablesStatus);
			}
		}
	}

	public void MergeStatus(VariableMigrationStatus target, VariableMigrationStatus source)
	{
		target.MigratedVariableCount = MathX.Max(target.MigratedVariableCount, source.MigratedVariableCount);
		target.MigratedVariableDefinitionCount = MathX.Max(target.MigratedVariableDefinitionCount, source.MigratedVariableDefinitionCount);
	}

	public void MergeStatus(RecordMigrationStatus target, RecordMigrationStatus source)
	{
		target.MigratedRecordCount = MathX.Max(target.MigratedRecordCount, source.MigratedRecordCount);
		target.AlreadyMigratedRecordCount = MathX.Max(target.AlreadyMigratedRecordCount, source.AlreadyMigratedRecordCount);
		target.ConflictedRecordCount = MathX.Max(target.ConflictedRecordCount, source.ConflictedRecordCount);
		target.TotalRecordCount = MathX.Max(target.TotalRecordCount, source.TotalRecordCount);
		target.AssetsToUpload += source.AssetsToUpload;
		target.AssetsUploaded += source.AssetsUploaded;
		target.BytesToUpload += source.BytesToUpload;
		target.BytesUploaded += source.BytesUploaded;
	}
}
/// <summary>
/// Represents current status of a group to migrate
/// </summary>
public class GroupMigrationStatus
{
	/// <summary>
	/// OwnerID of the group
	/// </summary>
	[JsonProperty(PropertyName = "ownerId")]
	[JsonPropertyName("ownerId")]
	public string OwnerId { get; set; }

	/// <summary>
	/// Name of the group
	/// </summary>
	[JsonProperty(PropertyName = "groupName")]
	[JsonPropertyName("groupName")]
	public string GroupName { get; set; }

	/// <summary>
	/// Were members already migrated? It doesn't make sense to have progress indicator for this,
	/// because there's barely any data to go with this.
	/// </summary>
	[JsonProperty(PropertyName = "migratedMemberCount")]
	[JsonPropertyName("migratedMemberCount")]
	public int MigratedMemberCount { get; set; }

	/// <summary>
	/// Status of the migrated records
	/// </summary>
	[JsonProperty(PropertyName = "recordsStatus")]
	[JsonPropertyName("recordsStatus")]
	public RecordMigrationStatus RecordsStatus { get; set; } = new RecordMigrationStatus();

	/// <summary>
	/// Migration status of the variables of the group
	/// </summary>
	[JsonProperty(PropertyName = "variablesStatus")]
	[JsonPropertyName("variablesStatus")]
	public VariableMigrationStatus VariablesStatus { get; set; } = new VariableMigrationStatus();
}
/// <summary>
/// Contains all necessary data to initialize a migration process from a source infrastructure
/// </summary>
public class MigrationInitialization
{
	/// <summary>
	/// Configuration for the source cloud
	/// </summary>
	[JsonProperty(PropertyName = "sourceCloudConfig")]
	[JsonPropertyName("sourceCloudConfig")]
	public SkyFrostConfig SourceCloudConfig { get; set; }

	/// <summary>
	/// Login credentials for the source infrastructure
	/// </summary>
	[JsonProperty(PropertyName = "sourceLogin")]
	[JsonPropertyName("sourceLogin")]
	public LoginCredentials SourceLogin { get; set; }

	/// <summary>
	/// TOTP code for the source infrastructure
	/// </summary>
	[JsonProperty(PropertyName = "sourceTOTP")]
	[JsonPropertyName("sourceTOTP")]
	public string SourceTOTP { get; set; }

	/// <summary>
	/// Session for the source infrastructure
	/// </summary>
	[JsonProperty(PropertyName = "sourceSession")]
	[JsonPropertyName("sourceSession")]
	public UserSession SourceSession { get; set; }

	/// <summary>
	/// The UID used to generate this data. This is explicitly stored, so it can be used by the cloud during
	/// the migration process. The session tokens can be verified against the UID, to prevent leaked tokens
	/// from being abused. Since the cloud is handling the actual migration, we'll need to provide it explicitly
	/// </summary>
	[JsonProperty(PropertyName = "sourceUID")]
	[JsonPropertyName("sourceUID")]
	public string SourceUID { get; set; }

	/// <summary>
	/// The secret machine ID generated specifically for this migration. This is necessary for the migration to
	/// function, because the cloud verifies if the session token matches the SecretMachineID. We just generate
	/// a random one for the purposes of migration, so it cannot be used for anything else.
	/// </summary>
	[JsonProperty(PropertyName = "sourceSecretMachineID")]
	[JsonPropertyName("sourceSecretMachineID")]
	public string SourceSecretMachineID { get; set; }

	/// <summary>
	/// The actual migration task to be performed
	/// </summary>
	[JsonProperty(PropertyName = "config")]
	[JsonPropertyName("config")]
	public AccountMigrationConfig Config { get; set; }
}
public class RecordMigrationFailure
{
	[JsonProperty(PropertyName = "recordId")]
	[JsonPropertyName("recordId")]
	public string RecordId { get; set; }

	[JsonProperty(PropertyName = "ownerId")]
	[JsonPropertyName("ownerId")]
	public string OwnerId { get; set; }

	[JsonProperty(PropertyName = "recordName")]
	[JsonPropertyName("recordName")]
	public string RecordName { get; set; }

	[JsonProperty(PropertyName = "recordPath")]
	[JsonPropertyName("recordPath")]
	public string RecordPath { get; set; }

	[JsonProperty(PropertyName = "failureReason")]
	[JsonPropertyName("failureReason")]
	public string FailureReason { get; set; }
}
public class RecordMigrationStatus
{
	/// <summary>
	/// Total number of records to be migrated
	/// </summary>
	[JsonProperty(PropertyName = "totalRecordCount")]
	[JsonPropertyName("totalRecordCount")]
	public int TotalRecordCount { get; set; }

	/// <summary>
	/// Total number of records that were migrated
	/// </summary>
	[JsonProperty(PropertyName = "migratedRecordCount")]
	[JsonPropertyName("migratedRecordCount")]
	public int MigratedRecordCount { get; set; }

	/// <summary>
	/// Total number of records that were already migrated and were skipped
	/// </summary>
	[JsonProperty(PropertyName = "alreadyMigratedRecordCount")]
	[JsonPropertyName("alreadyMigratedRecordCount")]
	public int AlreadyMigratedRecordCount { get; set; }

	/// <summary>
	/// How many records have conflicted
	/// </summary>
	[JsonProperty(PropertyName = "conflictedRecordCount")]
	[JsonPropertyName("conflictedRecordCount")]
	public int ConflictedRecordCount { get; set; }

	/// <summary>
	/// Total number of records that were processed
	/// </summary>
	[JsonProperty(PropertyName = "totalProcessedRecordCount")]
	[JsonPropertyName("totalProcessedRecordCount")]
	public int TotalProcessedRecordCount => MigratedRecordCount + AlreadyMigratedRecordCount + ConflictedRecordCount;

	/// <summary>
	/// Debug description of the current search phase for the records
	/// </summary>
	[JsonProperty(PropertyName = "recordSearchPhase")]
	[JsonPropertyName("recordSearchPhase")]
	public string RecordSearchPhase { get; set; }

	/// <summary>
	/// How many bytes need to be uploaded
	/// </summary>
	[JsonProperty(PropertyName = "bytesToUpload")]
	[JsonPropertyName("bytesToUpload")]
	public long BytesToUpload { get; set; }

	/// <summary>
	/// How many bytes were already uploaded
	/// </summary>
	[JsonProperty(PropertyName = "bytesUploaded")]
	[JsonPropertyName("bytesUploaded")]
	public long BytesUploaded { get; set; }

	/// <summary>
	/// How many assets need to be uplaoded
	/// </summary>
	[JsonProperty(PropertyName = "assetsToUpload")]
	[JsonPropertyName("assetsToUpload")]
	public int AssetsToUpload { get; set; }

	/// <summary>
	/// How many assets were uploaded
	/// </summary>
	[JsonProperty(PropertyName = "assetsUploaded")]
	[JsonPropertyName("assetsUploaded")]
	public int AssetsUploaded { get; set; }

	/// <summary>
	/// How many assets were uploaded
	/// </summary>
	[JsonProperty(PropertyName = "lastUpdated")]
	[JsonPropertyName("lastUpdated")]
	public DateTime LastUpdated { get; set; }

	/// <summary>
	/// Which records are currently being migrated
	/// </summary>
	[JsonProperty(PropertyName = "currentlyMigratingRecords")]
	[JsonPropertyName("currentlyMigratingRecords")]
	public HashSet<string> CurrentlyMigratingRecords { get; set; } = new HashSet<string>();

	/// <summary>
	/// Records that failed to migrate
	/// </summary>
	[JsonProperty(PropertyName = "failedRecords")]
	[JsonPropertyName("failedRecords")]
	public List<RecordMigrationFailure> FailedRecords { get; set; }

	public void Updated()
	{
		LastUpdated = DateTime.UtcNow;
	}
}
public class VariableMigrationStatus
{
	[JsonProperty(PropertyName = "migratedVariableCount")]
	[JsonPropertyName("migratedVariableCount")]
	public int MigratedVariableCount { get; set; }

	[JsonProperty(PropertyName = "migratedVariableDefinitionCount")]
	[JsonPropertyName("migratedVariableDefinitionCount")]
	public int MigratedVariableDefinitionCount { get; set; }
}
[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
[DataModelType]
public class SessionInfo
{
	public const int MAX_DLL_LENGTH = 128;

	public const int MAX_NAME_LENGTH = 256;

	public const int MAX_DESCRIPTION_LENGTH = 16384;

	public const int MAX_TAG_LENGTH = 128;

	public const int MAX_TAGS = 256;

	public const int MAX_PARENT_SESSION_IDS = 16;

	public const int MAX_NESTED_SESSION_IDS = 256;

	public const int MAX_ID_LENGTH = 128;

	public const int MAX_URL_LENGTH = 256;

	public const int MAX_USER_COUNT = 256;

	public static TimeSpan SESSION_UPDATE_INTERVAL => TimeSpan.FromSeconds(5L);

	[JsonProperty(PropertyName = "name")]
	[JsonPropertyName("name")]
	public string Name { get; set; }

	[JsonProperty(PropertyName = "description")]
	[JsonPropertyName("description")]
	public string Description { get; set; }

	[Newtonsoft.Json.JsonIgnore]
	[System.Text.Json.Serialization.JsonIgnore]
	public RecordId ActualCorrespondingWorldId => PrivateCorrespondingWorldId ?? CorrespondingWorldId;

	[JsonProperty(PropertyName = "correspondingWorldId")]
	[JsonPropertyName("correspondingWorldId")]
	public RecordId CorrespondingWorldId { get; set; }

	[Newtonsoft.Json.JsonIgnore]
	[System.Text.Json.Serialization.JsonIgnore]
	public RecordId PrivateCorrespondingWorldId { get; set; }

	[JsonProperty(PropertyName = "tags")]
	[JsonPropertyName("tags")]
	public HashSet<string> Tags { get; set; }

	[JsonProperty(PropertyName = "sessionId")]
	[JsonPropertyName("sessionId")]
	public string SessionId { get; set; }

	[JsonProperty(PropertyName = "normalizedSessionId")]
	[JsonPropertyName("normalizedSessionId")]
	public string NormalizedSessionId => NormalizeId(SessionId);

	[JsonProperty(PropertyName = "hostUserId")]
	[JsonPropertyName("hostUserId")]
	public string HostUserId { get; set; }

	[JsonProperty(PropertyName = "hostUserSessionId")]
	[JsonPropertyName("hostUserSessionId")]
	public string HostUserSessionId { get; set; }

	[JsonProperty(PropertyName = "hostMachineId")]
	[JsonPropertyName("hostMachineId")]
	public string HostMachineId { get; set; }

	[JsonProperty(PropertyName = "hostUsername")]
	[JsonPropertyName("hostUsername")]
	public string HostUsername { get; set; }

	[JsonProperty(PropertyName = "compatibilityHash")]
	[JsonPropertyName("compatibilityHash")]
	public string CompatibilityHash { get; set; }

	[JsonProperty(PropertyName = "systemCompatibilityHash")]
	[JsonPropertyName("systemCompatibilityHash")]
	public string SystemCompatibilityHash { get; set; }

	[JsonProperty(PropertyName = "dataModelAssemblies")]
	[JsonPropertyName("dataModelAssemblies")]
	public List<AssemblyInfo> DataModelAssemblies { get; set; }

	[JsonProperty(PropertyName = "universeId")]
	[JsonPropertyName("universeId")]
	public string UniverseId { get; set; }

	[JsonProperty(PropertyName = "appVersion")]
	[JsonPropertyName("appVersion")]
	public string AppVersion { get; set; }

	[JsonProperty(PropertyName = "headlessHost")]
	[JsonPropertyName("headlessHost")]
	public bool HeadlessHost { get; set; }

	[JsonProperty(PropertyName = "sessionURLs")]
	[JsonPropertyName("sessionURLs")]
	public List<string> SessionURLs { get; set; }

	[JsonProperty(PropertyName = "parentSessionIds")]
	[JsonPropertyName("parentSessionIds")]
	public List<string> ParentSessionIds { get; set; }

	[JsonProperty(PropertyName = "nestedSessionIds")]
	[JsonPropertyName("nestedSessionIds")]
	public List<string> NestedSessionIds { get; set; }

	[JsonProperty(PropertyName = "sessionUsers")]
	[JsonPropertyName("sessionUsers")]
	public List<SessionUser> SessionUsers { get; set; }

	[JsonProperty(PropertyName = "thumbnailUrl")]
	[JsonPropertyName("thumbnailUrl")]
	public string ThumbnailUrl { get; set; }

	[JsonProperty(PropertyName = "joinedUsers")]
	[JsonPropertyName("joinedUsers")]
	public int JoinedUsers { get; set; }

	[JsonProperty(PropertyName = "activeUsers")]
	[JsonPropertyName("activeUsers")]
	public int ActiveUsers { get; set; }

	[JsonProperty(PropertyName = "totalJoinedUsers")]
	[JsonPropertyName("totalJoinedUsers")]
	public int TotalJoinedUsers { get; set; }

	[JsonProperty(PropertyName = "totalActiveUsers")]
	[JsonPropertyName("totalActiveUsers")]
	public int TotalActiveUsers { get; set; }

	[JsonProperty(PropertyName = "maxUsers")]
	[JsonPropertyName("maxUsers")]
	public int MaximumUsers { get; set; }

	[JsonProperty(PropertyName = "mobileFriendly")]
	[JsonPropertyName("mobileFriendly")]
	public bool MobileFriendly { get; set; }

	[JsonProperty(PropertyName = "sessionBeginTime")]
	[JsonPropertyName("sessionBeginTime")]
	public DateTime SessionBeginTime { get; set; }

	[JsonProperty(PropertyName = "lastUpdate")]
	[JsonPropertyName("lastUpdate")]
	public DateTime LastUpdate { get; set; }

	[JsonProperty(PropertyName = "accessLevel")]
	[JsonPropertyName("accessLevel")]
	[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
	[System.Text.Json.Serialization.JsonConverter(typeof(JsonStringEnumConverter))]
	public SessionAccessLevel AccessLevel { get; set; }

	[JsonProperty(PropertyName = "hideFromListing")]
	[JsonPropertyName("hideFromListing")]
	public bool HideFromListing { get; set; }

	[JsonProperty(PropertyName = "broadcastKey")]
	[JsonPropertyName("broadcastKey")]
	public string BroadcastKey { get; set; }

	/// <summary>
	/// Represents if users in the session will get auto-kicked wheb they have been away from the session for longer than the time period listed in AwayKickMinutes.
	/// </summary>
	[JsonProperty(PropertyName = "awayKickEnabled")]
	[JsonPropertyName("awayKickEnabled")]
	public bool AwayKickEnabled { get; set; }

	/// <summary>
	/// Represents how long a user must be away from the session before they are auto-kicked, represented in total minutes.
	/// </summary>
	[JsonProperty(PropertyName = "awayKickMinutes")]
	[JsonPropertyName("awayKickMinutes")]
	public float AwayKickMinutes { get; set; }

	[Newtonsoft.Json.JsonIgnore]
	[System.Text.Json.Serialization.JsonIgnore]
	public ushort? LAN_Port { get; set; }

	[Newtonsoft.Json.JsonIgnore]
	[System.Text.Json.Serialization.JsonIgnore]
	public string LAN_URL { get; set; }

	[Newtonsoft.Json.JsonIgnore]
	[System.Text.Json.Serialization.JsonIgnore]
	public bool IsOnLAN => LAN_URL != null;

	[Newtonsoft.Json.JsonIgnore]
	[System.Text.Json.Serialization.JsonIgnore]
	public DateTime LastLAN_Update { get; set; }

	[Newtonsoft.Json.JsonIgnore]
	[System.Text.Json.Serialization.JsonIgnore]
	public DateTime LastWorldConfigurationUpdate { get; set; }

	[Newtonsoft.Json.JsonIgnore]
	[System.Text.Json.Serialization.JsonIgnore]
	public DateTime LastWorldUserUpdate { get; set; }

	[Newtonsoft.Json.JsonIgnore]
	[System.Text.Json.Serialization.JsonIgnore]
	public DateTime LastInviteListUpdate { get; set; }

	[Newtonsoft.Json.JsonIgnore]
	[System.Text.Json.Serialization.JsonIgnore]
	public bool IsExpired => IsTimestampExpired(LastUpdate);

	[Newtonsoft.Json.JsonIgnore]
	[System.Text.Json.Serialization.JsonIgnore]
	public float ExpirationProgress => TimestampExpirationProgress(LastUpdate);

	[Newtonsoft.Json.JsonIgnore]
	[System.Text.Json.Serialization.JsonIgnore]
	public string SanitizedHostUsername => StringParsingHelper.SanitizeFormatTags(HostUsername);

	public bool HasEnded
	{
		get
		{
			if (SessionURLs != null)
			{
				return SessionURLs.Count == 0;
			}
			return true;
		}
	}

	public bool IsValid
	{
		get
		{
			string name = Name;
			if (name != null && name.Length > 256)
			{
				return false;
			}
			if (!IsAllowedName(Name))
			{
				return false;
			}
			string description = Description;
			if (description != null && description.Length > 16384)
			{
				return false;
			}
			if (CorrespondingWorldId != null && !CorrespondingWorldId.IsValid)
			{
				return false;
			}
			HashSet<string> tags = Tags;
			if (tags != null && tags.Count > 256)
			{
				return false;
			}
			HashSet<string> tags2 = Tags;
			if (tags2 != null && tags2.Any((string s) => s != null && s.Length > 128))
			{
				return false;
			}
			string sessionId = SessionId;
			if (sessionId != null && sessionId.Length > 128)
			{
				return false;
			}
			string universeId = UniverseId;
			if (universeId != null && universeId.Length > 128)
			{
				return false;
			}
			string hostUserId = HostUserId;
			if (hostUserId != null && hostUserId.Length > 128)
			{
				return false;
			}
			string hostMachineId = HostMachineId;
			if (hostMachineId != null && hostMachineId.Length > 128)
			{
				return false;
			}
			string hostUsername = HostUsername;
			if (hostUsername != null && hostUsername.Length > 32)
			{
				return false;
			}
			string compatibilityHash = CompatibilityHash;
			if (compatibilityHash != null && compatibilityHash.Length > 128)
			{
				return false;
			}
			string appVersion = AppVersion;
			if (appVersion != null && appVersion.Length > 128)
			{
				return false;
			}
			List<string> sessionURLs = SessionURLs;
			if (sessionURLs != null && sessionURLs.Any((string s) => s != null && s.Length > 256))
			{
				return false;
			}
			List<string> nestedSessionIds = NestedSessionIds;
			if (nestedSessionIds != null && nestedSessionIds.Count > 256)
			{
				return false;
			}
			List<string> parentSessionIds = ParentSessionIds;
			if (parentSessionIds != null && parentSessionIds.Count > 16)
			{
				return false;
			}
			List<string> nestedSessionIds2 = NestedSessionIds;
			if (nestedSessionIds2 != null && nestedSessionIds2.Any((string s) => (s != null && s.Length > 128) || !IsValidSessionId(s)))
			{
				return false;
			}
			List<string> parentSessionIds2 = ParentSessionIds;
			if (parentSessionIds2 != null && parentSessionIds2.Any((string s) => (s != null && s.Length > 128) || !IsValidSessionId(s)))
			{
				return false;
			}
			string thumbnailUrl = ThumbnailUrl;
			if (thumbnailUrl != null && thumbnailUrl.Length > 256)
			{
				return false;
			}
			if (!IsValidSessionId(SessionId))
			{
				return false;
			}
			if (!IsValidVersion(AppVersion))
			{
				return false;
			}
			if (SessionUsers != null)
			{
				if (SessionUsers.Count > 256)
				{
					return false;
				}
				foreach (SessionUser sessionUser in SessionUsers)
				{
					string userID = sessionUser.UserID;
					if (userID != null && userID.Length > 128)
					{
						return false;
					}
					string username = sessionUser.Username;
					if (username != null && username.Length > 32)
					{
						return false;
					}
				}
			}
			return true;
		}
	}

	public static string NormalizeId(string id)
	{
		return id?.ToLowerInvariant();
	}

	public static bool IsTimestampExpired(DateTime lastUpdate)
	{
		return DateTime.UtcNow - lastUpdate > UserStatus.StatusExpiration;
	}

	public static float TimestampExpirationProgress(DateTime lastUpdate)
	{
		return MathX.InverseLerp(lastUpdate, lastUpdate + UserStatus.StatusExpiration, DateTime.UtcNow);
	}

	public SessionInfo()
	{
	}

	public SessionInfo(string sessionId)
	{
		SessionId = sessionId;
		LastUpdate = DateTime.UtcNow;
	}

	public static bool IsAllowedName(string name)
	{
		if (name == null)
		{
			return true;
		}
		name = new StringRenderTree(name).GetRawString().ToLower();
		if (name.Contains("18+"))
		{
			return false;
		}
		if (name.Contains("nsfw"))
		{
			return false;
		}
		return true;
	}

	public static bool IsCustomSessionId(string sessionId)
	{
		return sessionId.StartsWith("S-U-", StringComparison.InvariantCultureIgnoreCase);
	}

	public static string GetCustomSessionIdOwner(string sessionId)
	{
		int num = sessionId.IndexOf(":");
		if (num < 0)
		{
			throw new Exception("Invalid custom sessionId! Make sure it's valid first");
		}
		return sessionId.Substring(2, num - 2);
	}

	public static bool IsValidSessionId(string sessionId)
	{
		if (string.IsNullOrWhiteSpace(sessionId))
		{
			return false;
		}
		foreach (char c in sessionId)
		{
			if (char.IsDigit(c))
			{
				continue;
			}
			if (char.IsLetter(c))
			{
				if (c > '\u007f')
				{
					return false;
				}
			}
			else if (c != '-' && c != ':' && c != '_')
			{
				return false;
			}
		}
		if (sessionId.StartsWith("U-", StringComparison.InvariantCultureIgnoreCase))
		{
			return false;
		}
		if (IsCustomSessionId(sessionId) && sessionId.IndexOf(":") < 0)
		{
			return false;
		}
		return true;
	}

	public static bool IsValidVersion(string version)
	{
		if (version == null)
		{
			return false;
		}
		int num = version.IndexOf('+');
		string str;
		string text;
		if (num < 0)
		{
			str = version;
			text = "";
		}
		else
		{
			str = version.Substring(0, num);
			text = version.Substring(num + 1);
		}
		if (!VersionNumber.TryParse(str, out var _))
		{
			return false;
		}
		if (text.Length > 128)
		{
			return false;
		}
		return true;
	}

	public List<Uri> GetSessionURLs()
	{
		return (from str in SessionURLs
			where Uri.IsWellFormedUriString(str, UriKind.Absolute)
			select new Uri(str)).ToList();
	}

	public void SetEnded()
	{
		SessionURLs = null;
	}

	public void CopyLAN_Data(SessionInfo source)
	{
		LAN_URL = source.LAN_URL;
		LastLAN_Update = source.LastLAN_Update;
		if (LAN_URL != null)
		{
			if (SessionURLs == null)
			{
				SessionURLs = new List<string>();
			}
			SessionURLs.AddUnique(LAN_URL);
		}
	}

	public bool HasTag(string tag)
	{
		return Tags?.Contains(tag) ?? false;
	}

	public void Trim()
	{
		string name = Name;
		if (name != null && name.Length > 256)
		{
			Name = Name.Substring(0, 256);
		}
		string description = Description;
		if (description != null && description.Length > 16384)
		{
			Description = Description.Substring(0, 16384);
		}
		Tags?.RemoveWhere((string s) => s != null && s.Length > 128);
	}

	public CloudSessionMetadata GetMetadata()
	{
		return new CloudSessionMetadata
		{
			AccessLevel = AccessLevel,
			HostUserId = HostUserId,
			HostUserSessionId = HostUserSessionId,
			SessionHidden = HideFromListing,
			SessionId = SessionId,
			ActiveUsers = ActiveUsers,
			JoinedUsers = JoinedUsers,
			SessionURLs = SessionURLs
		};
	}

	public override string ToString()
	{
		return $"SessionInfo. Id: {SessionId}, Name: {Name}, Host: {HostUsername}, CorrespondingWorldId: {ActualCorrespondingWorldId}, URLs: {((SessionURLs != null) ? string.Join(", ", SessionURLs) : "")}, IsExpired: {IsExpired}";
	}
}
public class SessionUpdate
{
	[JsonProperty(PropertyName = "hostedSessions")]
	[JsonPropertyName("hostedSessions")]
	public List<SessionInfo> HostedSessions { get; set; }

	[JsonProperty(PropertyName = "removedSessions")]
	[JsonPropertyName("removedSessions")]
	public List<string> RemovedSessions { get; set; }
}
[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
public class SessionUser : IEquatable<SessionUser>
{
	[JsonProperty(PropertyName = "username")]
	[JsonPropertyName("username")]
	public string Username { get; set; }

	[JsonProperty(PropertyName = "userID")]
	[JsonPropertyName("userID")]
	public string UserID { get; set; }

	[JsonProperty(PropertyName = "userSessionId")]
	[JsonPropertyName("userSessionId")]
	public string UserSessionId { get; set; }

	[JsonProperty(PropertyName = "isPresent")]
	[JsonPropertyName("isPresent")]
	public bool IsPresent { get; set; }

	[JsonProperty(PropertyName = "outputDevice")]
	[JsonPropertyName("outputDevice")]
	[Newtonsoft.Json.JsonConverter(typeof(NewtonsoftJsonTransitionEnumConverter))]
	[System.Text.Json.Serialization.JsonConverter(typeof(JsonTransitionEnumConverter<OutputDevice>))]
	public OutputDevice? OutputDevice { get; set; }

	[Newtonsoft.Json.JsonIgnore]
	[System.Text.Json.Serialization.JsonIgnore]
	public string SanitizedUsername => StringParsingHelper.SanitizeFormatTags(Username);

	public bool Equals(SessionUser other)
	{
		if (Username == other.Username && UserID == other.UserID && IsPresent == other.IsPresent)
		{
			return OutputDevice == other.OutputDevice;
		}
		return false;
	}
}
public class SkyFrostConfig
{
	public const string SECRET_CLIENT_KEY = "oi+ISZuYtMYtpruYHLQLPkXgPaD+IcaRNXPI7b3Z0iYe5+AcccouLYFI9vloMmYEYDlE1PhDL52GsddfxgQeK4Z_hem84t1OXGUdScFkLSMhJA2te86LBL_rFL4JjO4F_hHHIJH1Gm1IYVuvBQjpb89AJ0D6eamd7u4MxeWeEVE=";

	public const string CLOUDFLARE_DURIAN_ENDPOINT = "https://assetx.frooxius.workers.dev/";

	public const string DURIAN_ASSET_ENDPOINT = "https://assets.everion.com/";

	public const string DURIAN_VARIANT_ENDPOINT = "https://variants.everion.com/";

	public const string DURIAN_THUMBNAIL_ENDPOINT = "https://thumbnails.everion.com/";

	public const string CLOUDFLARE_SKYFROST_ENDPOINT = "https://skyfrost-archive.resonite.com/";

	public const string SKYFROST_ASSET_ENDPOINT = "https://assets.resonite.com/";

	public const string SKYFROST_VARIANT_ENDPOINT = "https://variants.resonite.com/";

	public const string SKYFROST_THUMBNAIL_ENDPOINT = "https://thumbnails.resonite.com/";

	/// <summary>
	/// Profile of the entire platform, containing common names, ID's, schemes and accounts
	/// </summary>
	[JsonPropertyName("platform")]
	public IPlatformProfile Platform { get; set; }

	/// <summary>
	/// How will the client identity itself to the API.
	/// </summary>
	[JsonPropertyName("userAgentProduct")]
	public string UserAgentProduct { get; set; }

	/// <summary>
	/// Version of the product that it will identify as.
	/// </summary>
	[JsonPropertyName("userAgentVersion")]
	public string UserAgentVersion { get; set; }

	/// <summary>
	/// Enable GZip compression for requests and responses. This can decrease latency and
	/// signififcantly decreate the bandwidth necessary to communicate with the API.
	/// On some platforms it causes trouble though (e.g. Linux) so must be disabled.
	/// </summary>
	[JsonPropertyName("gzip")]
	public bool GZip { get; set; } = true;

	/// <summary>
	/// Proxy settings to use when initializing SkyFrost.
	/// </summary>
	[JsonPropertyName("proxyConfig")]
	public ProxyConfig ProxyConfig { get; set; }

	/// <summary>
	/// URL of the main endpoint for API calls
	/// </summary>
	[JsonPropertyName("apiEndpoint")]
	public string ApiEndpoint { get; set; }

	/// <summary>
	/// Endpoint where SignalR hub is located
	/// </summary>
	[JsonPropertyName("signalREndpoint")]
	public string SignalREndpoint { get; set; }

	/// <summary>
	/// Force the use of long polling for SignalR. This can be useful if the connection is having issues
	/// with websockets for some reason
	/// </summary>
	[JsonPropertyName("forceSignalRLongPolling")]
	public bool ForceSignalRLongPolling { get; set; }

	/// <summary>
	/// Endpoint that can be checked for current status of the API. This generally should be different from
	/// the ApiEndpoint, because if that one is down, then requests to this one would fail as well.
	/// </summary>
	[JsonPropertyName("serverStatusEndpoint")]
	public string ServerStatusEndpoint { get; set; }

	/// <summary>
	/// If the target endpoint is hidden under a secret access key, it needs to be provided there
	/// </summary>
	[JsonPropertyName("secretClientAccessKey")]
	public string SecretClientAccessKey { get; set; }

	/// <summary>
	/// URL for initiating SAML 2.0 login
	/// </summary>
	[JsonPropertyName("saml2Endpoint")]
	public string Saml2Endpoint { get; set; }

	/// <summary>
	/// Default timeout for the web requests. Should be reasonably low, so if there are transient errors,
	/// the request doesn't hang for too long, but also not too short that network glitches would cause
	/// requests to fail.
	/// </summary>
	[JsonPropertyName("defaultTimeout")]
	public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30L);

	/// <summary>
	/// The interface used to communicate with the cloud asset system for downloading and uploading assets.
	/// </summary>
	[JsonPropertyName("assetInterface")]
	public AssetInterface AssetInterface { get; set; }

	/// <summary>
	/// Manager for gathering network node information - e.g. matchmaker and relays
	/// </summary>
	[JsonPropertyName("networkNodes")]
	public INetworkNodeManager NetworkNodes { get; set; }

	/// <summary>
	/// Name of the URL path for contacts. This is to support legacy API which used "friends"
	/// </summary>
	[JsonPropertyName("contactPath")]
	public string ContactPath { get; set; } = "contacts";

	/// <summary>
	/// Indicates if needs to use the legacy login method
	/// </summary>
	[JsonPropertyName("legacyLogin")]
	public bool UseLegacyLogin { get; set; }

	/// <summary>
	/// The current universe the client is using to interact with SkyFrost. Gets used for various requests.
	/// </summary>
	[JsonPropertyName("universeId")]
	public string UniverseID { get; set; }

	/// <summary>
	/// The current universe the client is using to interact with SkyFrost. Gets used for various requests.
	/// </summary>
	[JsonPropertyName("nodePreference")]
	public NetworkNodePreference NodePreference { get; set; }

	public static SkyFrostConfig DEFAULT_PRODUCTION => SKYFROST_PRODUCTION;

	public static SkyFrostConfig DEFAULT_PRODUCTION_DIRECT => SKYFROST_PRODUCTION_DIRECT;

	public static SkyFrostConfig DEFAULT_STAGING => SKYFROST_STAGING;

	public static SkyFrostConfig DEFAULT_LOCAL => SKYFROST_LOCAL;

	public static SkyFrostConfig DURIAN_LOCAL => new SkyFrostConfig
	{
		Platform = PlatformProfile.DURIAN,
		UserAgentProduct = "Durian",
		ApiEndpoint = "http://localhost:60612",
		SignalREndpoint = "http://localhost:60612/hub",
		ServerStatusEndpoint = "https://skyfroststorage.blob.core.windows.net/install/ServerResponse",
		AssetInterface = new CloudflareAssetInterface("https://assetx.frooxius.workers.dev/", "https://assets.everion.com/", "https://variants.everion.com/", "https://thumbnails.everion.com/"),
		NetworkNodes = new NetworkNodeManager(),
		ContactPath = "contacts"
	};

	public static SkyFrostConfig DURIAN_STAGING => new SkyFrostConfig
	{
		Platform = PlatformProfile.DURIAN,
		UserAgentProduct = "Durian",
		ApiEndpoint = "https://everion-api.azurewebsites.net",
		SignalREndpoint = "https://everion-api.azurewebsites.net/hub",
		ServerStatusEndpoint = "https://everionfastblob.blob.core.windows.net/metadata/ServerResponse",
		Saml2Endpoint = "https://everionaccount.azurewebsites.net/Identity/Account/SAML/",
		AssetInterface = new CloudflareAssetInterface("https://assetx.frooxius.workers.dev/", "https://assets.everion.com/", "https://variants.everion.com/", "https://thumbnails.everion.com/"),
		NetworkNodes = new NetworkNodeManager()
	};

	public static SkyFrostConfig DURIAN_PRODUCTION => new SkyFrostConfig
	{
		Platform = PlatformProfile.DURIAN,
		UserAgentProduct = "Durian",
		ApiEndpoint = "https://everion-api.azurewebsites.net",
		SignalREndpoint = "https://everion-api.azurewebsites.net/hub",
		ServerStatusEndpoint = "https://everionfastblob.blob.core.windows.net/metadata/ServerResponse",
		Saml2Endpoint = "https://everionaccount.azurewebsites.net/Identity/Account/SAML/",
		AssetInterface = new CloudflareAssetInterface("https://assetx.frooxius.workers.dev/", "https://assets.everion.com/", "https://variants.everion.com/", "https://thumbnails.everion.com/"),
		NetworkNodes = new NetworkNodeManager()
	};

	public static SkyFrostConfig SKYFROST_LOCAL => new SkyFrostConfig
	{
		Platform = PlatformProfile.RESONITE,
		UserAgentProduct = "Resonite",
		ApiEndpoint = "http://localhost:60612",
		SignalREndpoint = "http://localhost:60612/hub",
		ServerStatusEndpoint = "https://skyfrostfastblob.blob.core.windows.net/install/ServerResponse",
		AssetInterface = new CloudflareAssetInterface("https://skyfrost-archive.resonite.com/", "https://assets.resonite.com/", "https://variants.resonite.com/", "https://thumbnails.resonite.com/"),
		NetworkNodes = new NetworkNodeManager(),
		ContactPath = "contacts"
	};

	public static SkyFrostConfig SKYFROST_STAGING => new SkyFrostConfig
	{
		Platform = PlatformProfile.RESONITE,
		UserAgentProduct = "Resonite",
		ApiEndpoint = "https://skyfrost-api-staging.azurewebsites.net/",
		SignalREndpoint = "https://skyfrost-api-staging.azurewebsites.net/hub",
		ServerStatusEndpoint = "https://skyfrostfastblob.blob.core.windows.net/metadata/ServerResponse",
		Saml2Endpoint = "https://account.resonite.com/Identity/Account/SAML/",
		AssetInterface = new CloudflareAssetInterface("https://skyfrost-archive.resonite.com/", "https://assets.resonite.com/", "https://variants.resonite.com/", "https://thumbnails.resonite.com/"),
		NetworkNodes = new NetworkNodeManager()
	};

	public static SkyFrostConfig SKYFROST_PRODUCTION => new SkyFrostConfig
	{
		Platform = PlatformProfile.RESONITE,
		UserAgentProduct = "Resonite",
		ApiEndpoint = "https://api.resonite.com",
		SignalREndpoint = "https://api.resonite.com/hub",
		ServerStatusEndpoint = "https://skyfrostfastblob.blob.core.windows.net/metadata/ServerResponse",
		Saml2Endpoint = "https://account.resonite.com/Identity/Account/SAML/",
		AssetInterface = new CloudflareAssetInterface("https://skyfrost-archive.resonite.com/", "https://assets.resonite.com/", "https://variants.resonite.com/", "https://thumbnails.resonite.com/"),
		NetworkNodes = new NetworkNodeManager()
	};

	public static SkyFrostConfig SKYFROST_PRODUCTION_DIRECT => new SkyFrostConfig
	{
		Platform = PlatformProfile.RESONITE,
		UserAgentProduct = "Resonite",
		ApiEndpoint = "https://skyfrost-api.azurewebsites.net",
		SignalREndpoint = "https://skyfrost-api.azurewebsites.net//hub",
		ServerStatusEndpoint = "https://skyfrostfastblob.blob.core.windows.net/metadata/ServerResponse",
		Saml2Endpoint = "https://account.resonite.com/Identity/Account/SAML/",
		AssetInterface = new CloudflareAssetInterface("https://skyfrost-archive.resonite.com/", "https://assets.resonite.com/", "https://variants.resonite.com/", "https://thumbnails.resonite.com/"),
		NetworkNodes = new NetworkNodeManager()
	};

	public SkyFrostConfig WithUserAgent(string product, string version = null)
	{
		UserAgentProduct = product;
		UserAgentVersion = version;
		return this;
	}

	public SkyFrostConfig WithGzip(bool enabled)
	{
		GZip = enabled;
		return this;
	}

	public SkyFrostConfig WithoutSignalR()
	{
		SignalREndpoint = null;
		return this;
	}

	public SkyFrostConfig WithSignalRLongPolling()
	{
		ForceSignalRLongPolling = true;
		return this;
	}

	public SkyFrostConfig WithUniverse(string universeId)
	{
		UniverseID = universeId;
		return this;
	}

	public SkyFrostConfig WithProxy(ProxyConfig proxy)
	{
		ProxyConfig = proxy;
		return this;
	}
}
[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
public class UserSessionMetadata : IEquatable<UserSessionMetadata>
{
	/// <summary>
	/// Hash of the session ID (+ salt from the user status) that the user is in
	/// This field uses hash so only users who already have data of given session can determine that the user
	/// is in this session, but other users cannot.
	/// The session hash might be null if the user is present in a session that is private, to completely hide its identity.
	/// </summary>
	[JsonProperty(PropertyName = "sessionHash")]
	[JsonPropertyName("sessionHash")]
	public string SessionHash { get; set; }

	/// <summary>
	/// Access level of this session
	/// </summary>
	[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
	[System.Text.Json.Serialization.JsonConverter(typeof(JsonStringEnumConverter))]
	[JsonProperty(PropertyName = "accessLevel")]
	[JsonPropertyName("accessLevel")]
	public SessionAccessLevel AccessLevel { get; set; }

	/// <summary>
	/// Indicates if this session is hidden from listing
	/// </summary>
	[JsonProperty(PropertyName = "sessionHidden")]
	[JsonPropertyName("sessionHidden")]
	public bool SessionHidden { get; set; }

	/// <summary>
	/// Is the user owning this status the host of the session this represents?
	/// </summary>
	[JsonProperty(PropertyName = "isHost")]
	[JsonPropertyName("isHost")]
	public bool IsHost { get; set; }

	/// <summary>
	/// BroadcastKey under which updates for this session can be received.
	/// This is used for certain types of sessions, like Contacts+, so anyone who becomes aware
	/// of the broadcast key can actually receive updates on this session
	/// </summary>
	[JsonProperty(PropertyName = "broadcastKey")]
	[JsonPropertyName("broadcastKey")]
	public string BroadcastKey { get; set; }

	public bool Equals(UserSessionMetadata other)
	{
		if (SessionHash == other.SessionHash && AccessLevel == other.AccessLevel && SessionHidden == other.SessionHidden)
		{
			return BroadcastKey == other.BroadcastKey;
		}
		return false;
	}
}
public static class OnlineStatusHelper
{
	public static bool DefaultPrivate(this OnlineStatus status)
	{
		return status == OnlineStatus.Invisible;
	}
}
[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
public class UserStatus : IEquatable<UserStatus>
{
	public static readonly TimeSpan StatusExpiration = TimeSpan.FromMinutes(5L);

	public static readonly TimeSpan StatusHeartbeat = TimeSpan.FromSeconds(StatusExpiration.TotalSeconds * 0.5);

	/// <summary>
	/// UserID that this status belongs to
	/// </summary>
	[JsonProperty(PropertyName = "userId")]
	[JsonPropertyName("userId")]
	public string UserId { get; set; }

	/// <summary>
	/// Unique identifier of the user's current status session. User can have multiple concurrent sessions,
	/// e.g. being logged in the graphical client, while at the same time running headless and chat client.
	/// This allows those sessions from being distinguished from each other.
	/// </summary>
	[JsonProperty(PropertyName = "userSessionId")]
	[JsonPropertyName("userSessionId")]
	public string UserSessionId { get; set; }

	/// <summary>
	/// Identifies the type of session this represents
	/// </summary>
	[JsonProperty(PropertyName = "sessionType")]
	[JsonPropertyName("sessionType")]
	[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
	[System.Text.Json.Serialization.JsonConverter(typeof(JsonStringEnumConverter))]
	public UserSessionType SessionType { get; set; }

	/// <summary>
	/// Current output device for graphical client. This can be used to tell if the user is in VR, screen, using camera or something else.
	/// </summary>
	[JsonProperty(PropertyName = "outputDevice")]
	[JsonPropertyName("outputDevice")]
	[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
	[System.Text.Json.Serialization.JsonConverter(typeof(JsonStringEnumConverter))]
	public OutputDevice? OutputDevice { get; set; }

	/// <summary>
	/// Is this session running on a mobile device?
	/// </summary>
	[JsonProperty(PropertyName = "isMobile")]
	[JsonPropertyName("isMobile")]
	public bool IsMobile { get; set; }

	/// <summary>
	/// The online status of this particular session, if applicable.
	/// Certain session types might be missing a status, e.g. Headless
	/// </summary>
	[JsonProperty(PropertyName = "onlineStatus")]
	[JsonPropertyName("onlineStatus")]
	[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
	[System.Text.Json.Serialization.JsonConverter(typeof(JsonStringEnumConverter))]
	public OnlineStatus? OnlineStatus { get; set; }

	/// <summary>
	/// Indicates if the user is currently present at this session
	/// </summary>
	[JsonProperty(PropertyName = "isPresent")]
	[JsonPropertyName("isPresent")]
	public bool IsPresent { get; set; }

	/// <summary>
	/// When is the last time that user was recorded as present in this session? Used to determine
	/// which of the statuses is the most "fresh"
	/// </summary>
	[JsonProperty(PropertyName = "lastPresenceTimestamp")]
	[JsonPropertyName("lastPresenceTimestamp")]
	public DateTime? LastPresenceTimestamp { get; set; }

	/// <summary>
	/// Timestamp of the last time this session info has changed
	/// </summary>
	[JsonProperty(PropertyName = "lastStatusChange")]
	[JsonPropertyName("lastStatusChange")]
	public DateTime LastStatusChange { get; set; }

	/// <summary>
	/// Randomized salt used to compute the session hashes. This is to prevent people from determining which users
	/// are in the same session by matching their hashes.
	/// This salt can change frequently, potentially with every single update of user status, to help obfuscate which
	/// sessions is the user present in and how they change their status.
	/// </summary>
	[JsonProperty(PropertyName = "hashSalt")]
	[JsonPropertyName("hashSalt")]
	public string HashSalt { get; set; }

	/// <summary>
	/// Version string of the app that the user is currently running on
	/// </summary>
	[JsonProperty(PropertyName = "appVersion")]
	[JsonPropertyName("appVersion")]
	public string AppVersion { get; set; }

	/// <summary>
	/// The compatibility hash of the client that the user is running on. Can be null if not applicable.
	/// The future is now! This is deprecated now - compatibility is determined on per-session basis,
	/// instead of globally.
	/// </summary>
	[JsonProperty(PropertyName = "compatibilityHash")]
	[JsonPropertyName("compatibilityHash")]
	[Obsolete]
	public string CompatibilityHash { get; set; }

	/// <summary>
	/// Public RSA parameters for authenticating user owning this status. The user generates private and public key pair
	/// when they start up the app. The public key can be fetched from the cloud under the user's ID and then used to verify
	/// any data signed by the user owning this, to ensure that they are who they claim to be.
	/// </summary>
	[JsonProperty(PropertyName = "publicRSAKey")]
	[JsonPropertyName("publicRSAKey")]
	public RSAParametersData PublicRSAKey { get; set; }

	/// <summary>
	/// Metadata of sessions that the user is currently present in. This requires having the actual session data
	/// to fully decode, as the session ID's are hashed with the user's salt
	/// </summary>
	[JsonProperty(PropertyName = "sessions")]
	[JsonPropertyName("sessions")]
	public List<UserSessionMetadata> Sessions { get; set; }

	/// <summary>
	/// Index of the session (in the session metadata list) that the user is currently present in
	/// </summary>
	[JsonProperty(PropertyName = "currentSessionIndex")]
	[JsonPropertyName("currentSessionIndex")]
	public int CurrentSessionIndex { get; set; }

	/// <summary>
	/// Helper to fetch the metadata of the session that the user is currently in
	/// </summary>
	[Newtonsoft.Json.JsonIgnore]
	[System.Text.Json.Serialization.JsonIgnore]
	public UserSessionMetadata CurrentSession
	{
		get
		{
			if (Sessions == null)
			{
				return null;
			}
			if (CurrentSessionIndex < 0 || CurrentSessionIndex >= Sessions.Count)
			{
				return null;
			}
			return Sessions[CurrentSessionIndex];
		}
		set
		{
			if (value == null)
			{
				CurrentSessionIndex = -1;
			}
			else
			{
				CurrentSessionIndex = Sessions.IndexOf(value);
			}
		}
	}

	[Newtonsoft.Json.JsonIgnore]
	[System.Text.Json.Serialization.JsonIgnore]
	public bool IsExpired => DateTime.UtcNow - LastStatusChange > StatusExpiration;

	public UserStatus Clone()
	{
		UserStatus userStatus = (UserStatus)MemberwiseClone();
		if (Sessions != null)
		{
			userStatus.Sessions = new List<UserSessionMetadata>(Sessions);
		}
		return userStatus;
	}

	/// <summary>
	/// Initializes the list of sessions to be filled, by generating new salt and ensuring there's a clear list
	/// </summary>
	public void InitializeSessionList()
	{
		HashSalt = CryptoHelper.GenerateCryptoToken();
		if (Sessions == null)
		{
			Sessions = new List<UserSessionMetadata>();
		}
		else
		{
			Sessions.Clear();
		}
	}

	/// <summary>
	/// Adds metadata of given session to the list. Assumes that rest of the data have been properly initialized
	/// </summary>
	/// <param name="info">Info of the session to add</param>
	/// <param name="isFocused">Indicates if given session is currently focused world</param>
	/// <returns>Generated metadata for given sesesion</returns>
	public UserSessionMetadata AddSession(SessionInfo info, bool isFocused)
	{
		UserSessionMetadata userSessionMetadata = new UserSessionMetadata();
		userSessionMetadata.SessionHash = CryptoHelper.HashIDToToken(info.SessionId + HashSalt);
		userSessionMetadata.AccessLevel = info.AccessLevel;
		userSessionMetadata.SessionHidden = info.HideFromListing;
		userSessionMetadata.IsHost = info.HostUserId == UserId;
		userSessionMetadata.BroadcastKey = info.BroadcastKey;
		Sessions.Add(userSessionMetadata);
		if (isFocused)
		{
			CurrentSessionIndex = Sessions.Count - 1;
		}
		return userSessionMetadata;
	}

	/// <summary>
	/// Given a list of session infos, this will try to find the session info corresponding to a given session metadata
	/// </summary>
	/// <param name="infos">List of the session info's to try to match</param>
	/// <param name="metadata">Metadata for which the match is being searched for</param>
	/// <returns>Matched session info if found, null otherwise</returns>
	public SessionInfo MatchSessionInfo(IEnumerable<SessionInfo> infos, UserSessionMetadata metadata)
	{
		foreach (SessionInfo info in infos)
		{
			if (IsMatchingSession(info, metadata))
			{
				return info;
			}
		}
		return null;
	}

	/// <summary>
	/// Determines if given session matches the session metadata for this user status.
	/// </summary>
	/// <param name="info">The session information to match</param>
	/// <param name="metadata">Metadata of the session to match against</param>
	/// <returns>True if matches, False if not</returns>
	public bool IsMatchingSession(SessionInfo info, UserSessionMetadata metadata)
	{
		string text = CryptoHelper.HashIDToToken(info.SessionId + HashSalt);
		return metadata.SessionHash == text;
	}

	/// <summary>
	/// Fills dictionary that maps session hashes represented by this status to their correspodning session infos
	/// </summary>
	/// <param name="infos">List of session infos to generate the map for</param>
	/// <param name="map">Dictionary to fill in with the matched data</param>
	public void CreateSessionMap(IEnumerable<SessionInfo> infos, Dictionary<string, SessionInfo> map)
	{
		foreach (SessionInfo info in infos)
		{
			string key = CryptoHelper.HashIDToToken(info.SessionId + HashSalt);
			map.Add(key, info);
		}
	}

	/// <summary>
	/// Determines if this status is dominant over another user status - that is if this one
	/// should be displayed as their primary status instead of the other status or not,
	/// </summary>
	/// <param name="status">The status to compare this one to</param>
	/// <returns>Whether the current status is dominant over the other status</returns>
	public bool IsDominantOver(UserStatus status)
	{
		if (status == null)
		{
			return true;
		}
		if (status.UserId != UserId)
		{
			throw new ArgumentException("Status doesn't belong to the same user");
		}
		if (IsPresent && !status.IsPresent)
		{
			return true;
		}
		if (LastPresenceTimestamp > status.LastPresenceTimestamp)
		{
			return true;
		}
		return false;
	}

	public override string ToString()
	{
		return $"Contact status for {UserId}.\n\tUserSessionId: {UserSessionId}.\n\tType: {SessionType}\n\tOutputDevice: {OutputDevice}\n\tIsMobile: {IsMobile}\n\tOnlineStatus: {OnlineStatus}\n\tIsPresent: {IsPresent}\n\tLastPresenceTimestamp: {LastPresenceTimestamp}\n\tLastStatusChange: {LastStatusChange}\n\tAppVersion: {AppVersion}\n\tCompatibilityHash: {CompatibilityHash}\n\tCurrentSessionIndex: {CurrentSessionIndex}";
	}

	public bool Equals(UserStatus other)
	{
		if (UserId == other.UserId && UserSessionId == other.UserSessionId && SessionType == other.SessionType && OutputDevice == other.OutputDevice && IsMobile == other.IsMobile && OnlineStatus == other.OnlineStatus && IsPresent == other.IsPresent)
		{
			DateTime? lastPresenceTimestamp = LastPresenceTimestamp;
			DateTime? lastPresenceTimestamp2 = other.LastPresenceTimestamp;
			if (lastPresenceTimestamp.HasValue == lastPresenceTimestamp2.HasValue && (!lastPresenceTimestamp.HasValue || lastPresenceTimestamp.GetValueOrDefault() == lastPresenceTimestamp2.GetValueOrDefault()) && LastStatusChange == other.LastStatusChange && HashSalt == other.HashSalt && AppVersion == other.AppVersion && CompatibilityHash == other.CompatibilityHash && PublicRSAKey == other.PublicRSAKey)
			{
				List<UserSessionMetadata> sessions = Sessions;
				if (sessions != null && sessions.SequenceEqual(other.Sessions))
				{
					return CurrentSessionIndex == other.CurrentSessionIndex;
				}
			}
		}
		return false;
	}
}
public static class RecordTags
{
	private static HashSet<string> IGNORE_TAGS = new HashSet<string> { "a", "an", "the", "and" };

	public static string CommonAvatar => "common_avatar";

	public static string CommonTool => "common_tool";

	public static string ProfileIcon => "profile_icon";

	public static string MessageItem => "message_item";

	public static string WorldOrb => "world_orb";

	public static string AudioPlayer => "audio_player";

	public static string VideoPlayer => "video_player";

	public static string VirtualKeyboard => "virtual_keyboard";

	public static string InteractiveCamera => "interactive_camera";

	public static string Facet => "facet";

	public static string ProgressBar => "progress_bar";

	public static string WorldLoadingProgress => "world_loading_progress";

	public static string AudioStreamInterface => "audio_stream_interface";

	public static string TextDisplay => "text_display";

	public static string DocumentDisplay => "document_display";

	public static string UrlDisplay => "url_display";

	public static string NameplateTemplate => "nameplate_template";

	public static string NoticeDisplay => "notice_display";

	public static string ColorDialog => "color_dialog";

	public static string Photo => "camera_photo";

	public static string VRPhoto => "vr_photo";

	public static string Photo360 => "360_photo";

	public static string PhotoStereo => "stereo_photo";

	public static string RawFile => "raw_file";

	public static string AudioClip => "audio_clip";

	public static string VideoClip => "video_clip";

	public static string RawFileAsset(string url)
	{
		return "raw_file_asset:" + url;
	}

	public static string TextureAsset(string url)
	{
		return "texture_asset:" + url;
	}

	public static string ClipAsset(string url)
	{
		return "clip_asset:" + url;
	}

	public static string ClipLength(double length)
	{
		return "clip_length:" + length.ToString(CultureInfo.InvariantCulture);
	}

	public static string LocationName(string name)
	{
		return "location_name:" + name;
	}

	public static string LocationHost(string userId)
	{
		return "location_host:" + userId;
	}

	public static string LocationAccessLevel(SessionAccessLevel accessLevel)
	{
		return $"location_accesslevel:{accessLevel}";
	}

	public static string LocationHiddenFromListing(bool hidden)
	{
		return $"location_hiddenfromlisting:{hidden}";
	}

	public static string PresentUser(string userId)
	{
		return "user:" + userId;
	}

	public static string Timestamp(DateTime time)
	{
		return "timestamp:" + time.ToString("o");
	}

	public static string CorrespondingMessageId(string messageId)
	{
		return "message_id:" + messageId;
	}

	public static string CorrespondingWorldUrl(string worldUrl)
	{
		return "world_url:" + worldUrl;
	}

	public static string GetCorrespondingMessageId(HashSet<string> tags)
	{
		return ExtractValue(tags, "message_id:");
	}

	public static string GetCorrespondingWorldUrl(HashSet<string> tags)
	{
		return ExtractValue(tags, "world_url:");
	}

	private static string ExtractValue(HashSet<string> tags, string prefix)
	{
		if (tags == null)
		{
			return null;
		}
		string text = tags.FirstOrDefault((string s) => s.StartsWith(prefix));
		if (text != null)
		{
			text = text.Substring(prefix.Length);
		}
		return text;
	}

	public static void GenerateTagsFromName(string name, HashSet<string> tags)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return;
		}
		name = new StringRenderTree(name).GetRawString();
		StringBuilder stringBuilder = new StringBuilder();
		string text = name;
		foreach (char c in text)
		{
			if (char.IsLetter(c))
			{
				stringBuilder.Append(char.ToLower(c));
			}
			else
			{
				ExtractTag(stringBuilder, tags);
			}
		}
		ExtractTag(stringBuilder, tags);
	}

	private static void ExtractTag(StringBuilder tagBuilder, HashSet<string> tags)
	{
		if (tagBuilder.Length > 1)
		{
			string item = tagBuilder.ToString();
			if (!IGNORE_TAGS.Contains(item))
			{
				tags.Add(item);
			}
		}
		tagBuilder.Clear();
	}
}
[DataModelType]
public enum DB_Endpoint
{
	Default,
	Video
}
[DataModelType]
public enum ServerStatus
{
	Good,
	Slow,
	Down,
	NoInternet
}
public delegate Task<T> ConsoleLoginHandler<T>(string totpCode) where T : CloudResult;
public class SkyFrostInterface
{
	public static bool DEBUG_REQUESTS;

	public static bool UseNewtonsoftJson;

	/// <summary>
	/// The HttpTransports that SkyFrost will instruct SignalR to use.
	/// </summary>
	private HttpTransportType SignalRTransports = HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling;

	public static Action<string> ProfilerBeginSampleCallback;

	public static Action ProfilerEndSampleCallback;

	public static Func<MemoryStream> MemoryStreamAllocator;

	private CancellationTokenSource _hubConnectionToken;

	private ConcurrentDictionary<Type, object> _metadataBatchQueries = new ConcurrentDictionary<Type, object>();

	private int SignalRConnectionAttempts;

	public string UID { get; private set; }

	public string SecretMachineId { get; private set; }

	public string UserAgentProduct { get; private set; }

	public string UserAgentVersion { get; private set; }

	public string ApiEndpoint { get; private set; }

	public string SignalREndpoint { get; private set; }

	public ProductInfoHeaderValue UserAgent { get; private set; }

	public User CurrentUser => Session.CurrentUser;

	public string CurrentUserID => Session.CurrentUserID;

	public string CurrentUsername => Session.CurrentUsername;

	public string UniverseID { get; protected set; }

	public NetworkNodePreference NodePreference { get; private set; }

	public IPlatformProfile Platform { get; private set; }

	public ApiClient Api { get; private set; }

	public HttpClient SafeHttpClient { get; private set; }

	public WebProxy? Proxy { get; private set; }

	public AppHub HubClient { get; private set; }

	public AssetGatherer AssetGatherer { get; private set; }

	public AssetInterface Assets { get; private set; }

	public ProxyConfig ProxyConfig { get; private set; }

	public HubStatusController HubStatusController { get; private set; }

	public SessionManager Session { get; private set; }

	public UsersManager Users { get; private set; }

	public StorageManager Storage { get; private set; }

	public SecurityManager Security { get; private set; }

	public ProfileManager Profile { get; private set; }

	public StatisticsManager Stats { get; private set; }

	public RecordsManager Records { get; private set; }

	public SessionsManager Sessions { get; private set; }

	public CloudVariableManager Variables { get; private set; }

	public UserStatusManager Status { get; private set; }

	public ContactManager Contacts { get; private set; }

	public GroupsManager Groups { get; private set; }

	public MessageManager Messages { get; private set; }

	public INetworkNodeManager NetworkNodes { get; private set; }

	public MigrationManager Migration { get; private set; }

	public VisitsManager Visits { get; private set; }

	public BadgeManager Badges { get; private set; }

	public ModerationManager Moderation { get; private set; }

	[Conditional("PROFILE")]
	private void ProfilerBeginSample(string name)
	{
		ProfilerBeginSampleCallback?.Invoke(name);
	}

	[Conditional("PROFILE")]
	private void ProfilerEndSample()
	{
		ProfilerEndSampleCallback?.Invoke();
	}

	public MetadataBatchQuery<M> MetadataBatch<M>() where M : class, IAssetMetadata, new()
	{
		if (_metadataBatchQueries.TryGetValue(typeof(M), out var value))
		{
			return (MetadataBatchQuery<M>)value;
		}
		MetadataBatchQuery<M> metadataBatchQuery = new MetadataBatchQuery<M>(this);
		_metadataBatchQueries.TryAdd(typeof(M), metadataBatchQuery);
		return metadataBatchQuery;
	}

	protected virtual Task OnLogin()
	{
		return Task.CompletedTask;
	}

	protected virtual Task OnLogout(bool isManual)
	{
		return Task.CompletedTask;
	}

	public virtual Task OnSessionTokenRefresh()
	{
		return Task.CompletedTask;
	}

	protected virtual void InstallConfigFile(string path, string content)
	{
	}

	public SkyFrostInterface(string uid, string secretMachineId, SkyFrostConfig config)
	{
		if (string.IsNullOrEmpty(uid))
		{
			throw new ArgumentNullException("uid");
		}
		if (config == null)
		{
			throw new ArgumentNullException("config");
		}
		if (config.Platform == null)
		{
			throw new ArgumentNullException("Platform");
		}
		if (config.AssetInterface == null)
		{
			throw new ArgumentNullException("AssetInterface");
		}
		Platform = config.Platform;
		UniverseID = config.UniverseID;
		ApiEndpoint = config.ApiEndpoint;
		SignalREndpoint = config.SignalREndpoint;
		UserAgentProduct = config.UserAgentProduct;
		UserAgentVersion = config.UserAgentVersion;
		NodePreference = config.NodePreference;
		if (config.ForceSignalRLongPolling)
		{
			SignalRTransports = HttpTransportType.LongPolling;
			UniLog.Log("Switched SignalR to long polling");
		}
		UserAgent = new ProductInfoHeaderValue(UserAgentProduct, UserAgentVersion);
		HttpClientHandler httpClientHandler = new HttpClientHandler
		{
			MaxConnectionsPerServer = 16
		};
		WebProxy webProxy = WebProxyUtility.CreateProxy(config.ProxyConfig);
		if (webProxy != null)
		{
			UniLog.Log("Initializing proxy configuration for SkyFrost.");
			httpClientHandler.Proxy = webProxy;
			Proxy = webProxy;
		}
		else
		{
			UniLog.Log("Proxy configuration for SkyFrost not found or failed to initialize.");
		}
		if (httpClientHandler.SupportsAutomaticDecompression && config.GZip)
		{
			httpClientHandler.AutomaticDecompression = DecompressionMethods.GZip;
		}
		UniLog.Log($"HttpClient AutomaticDecompressionSupported: {httpClientHandler.SupportsAutomaticDecompression}");
		HttpClient httpClient = new HttpClient(httpClientHandler)
		{
			DefaultRequestVersion = HttpVersion.Version11,
			DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
			Timeout = Timeout.InfiniteTimeSpan
		};
		httpClient.DefaultRequestHeaders.UserAgent.Add(UserAgent);
		Api = new ApiClient(httpClient, config.ApiEndpoint, AuthenticateApiRequest, config.SecretClientAccessKey, () => MemoryStreamAllocator?.Invoke(), delegate
		{
		}, delegate
		{
		});
		Api.DefaultTimeout = config.DefaultTimeout;
		SafeHttpClient = new HttpClient(httpClientHandler)
		{
			DefaultRequestVersion = HttpVersion.Version11,
			DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
			Timeout = TimeSpan.FromMinutes(1L)
		};
		SafeHttpClient.DefaultRequestHeaders.UserAgent.Add(UserAgent);
		if (!string.IsNullOrWhiteSpace(uid))
		{
			UID = uid;
		}
		if (!string.IsNullOrWhiteSpace(secretMachineId))
		{
			SecretMachineId = secretMachineId;
		}
		HubStatusController = new HubStatusController(this);
		Session = new SessionManager(this, config.Saml2Endpoint, config.UseLegacyLogin);
		Users = new UsersManager(this);
		Storage = new StorageManager(this);
		Security = new SecurityManager(this);
		Profile = new ProfileManager(this);
		Stats = new StatisticsManager(this, config.ServerStatusEndpoint);
		Sessions = new SessionsManager(this);
		Variables = new CloudVariableManager(this);
		Status = new UserStatusManager(this);
		Contacts = new ContactManager(this, config.ContactPath);
		Groups = new GroupsManager(this);
		Messages = new MessageManager(this);
		Migration = new MigrationManager(this);
		Records = new RecordsManager(this);
		Visits = new VisitsManager(this);
		Badges = new BadgeManager(this);
		Moderation = new ModerationManager(this);
		Assets = config.AssetInterface;
		Assets.Initialize(this);
		NetworkNodes = config.NetworkNodes;
		NetworkNodes.Initialize(this);
		Sessions.ListingSettings = new DefaultSessionListingSettings();
		Task.Run(async delegate
		{
			if (await RunInitialAnonymousHubConnection().ConfigureAwait(continueOnCapturedContext: false))
			{
				await ConnectToHub("Initial Startup").ConfigureAwait(continueOnCapturedContext: false);
			}
		});
	}

	protected virtual Task<bool> RunInitialAnonymousHubConnection()
	{
		return Task.FromResult(result: true);
	}

	private void AuthenticateApiRequest(HttpRequestMessage request, string totpCode)
	{
		if (Session?.CurrentSession != null)
		{
			request.Headers.Authorization = Session.AuthenticationHeader;
		}
		if (UID != null)
		{
			request.Headers.Add("UID", UID);
		}
		if (totpCode != null)
		{
			request.Headers.Add("TOTP", totpCode.Trim());
		}
	}

	internal async Task ConnectToHub(string source)
	{
		UniLog.Log("Initializing SignalR: " + source);
		_hubConnectionToken?.Cancel();
		HubClient?.Disconnect();
		await DisconnectFromHub().ConfigureAwait(continueOnCapturedContext: false);
		_hubConnectionToken = new CancellationTokenSource();
		CancellationToken cancellationToken = _hubConnectionToken.Token;
		HubConnection connection = new HubConnectionBuilder().WithUrl(SignalREndpoint, delegate(HttpConnectionOptions options)
		{
			string text = Session.CurrentSession?.SessionToken;
			if (!string.IsNullOrEmpty(UserAgentProduct))
			{
				string text2 = UserAgentProduct ?? "";
				if (!string.IsNullOrEmpty(UserAgentVersion))
				{
					text2 = text2 + "/" + UserAgentVersion;
				}
				UniLog.Log("Setting user agent to " + text2);
				options.Headers["User-Agent"] = text2;
			}
			if (text != null)
			{
				if (Session.CurrentSession.IsMachineBound)
				{
					text = CryptoHelper.HashIDToToken(text, SecretMachineId + UID);
				}
				options.Headers["Authorization"] = $"{Platform.AuthScheme} {Session.CurrentUserID}:{text}";
			}
			if (UID != null)
			{
				options.Headers["UID"] = UID;
			}
			if (!string.IsNullOrEmpty(Api.SecretClientAccessKey))
			{
				options.Headers["SecretClientAccessKey"] = Api.SecretClientAccessKey;
			}
			if (Proxy != null)
			{
				options.Proxy = Proxy;
				SignalRTransports = HttpTransportType.LongPolling;
				UniLog.Log("Switched SignalR to long polling");
			}
			options.Transports = SignalRTransports;
		}).WithAutomaticReconnect(new InfiniteRetryPolicy(1, 2, 5, 10, 15, 30)).Build();
		connection.Reconnecting += async delegate(Exception? ex)
		{
			UniLog.Warning("SignalR Reconnecting (" + source + "):\n" + ex.PrintAllInnerExceptions());
			HubStatusController.ResetInitializationStatus();
			Contacts.Reconnecting();
		};
		connection.Reconnected += async delegate(string? message)
		{
			UniLog.Warning("SignalR Reconnected (" + source + "): " + message, stackTrace: true);
			HubStatusController.Initialize(cancellationToken);
			Status.ForceUpdate();
		};
		connection.Closed += async delegate(Exception? error)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				UniLog.Log("SignalR Connection closed due to cancellation from SkyFrost.");
			}
			else
			{
				if (error is WebSocketException ex)
				{
					UniLog.Log($"SignalR Connection Closed due to Websockets issue Code: {ex.ErrorCode}, Message: {ex.Message}");
				}
				else
				{
					UniLog.Warning($"SignalR Connection Closed ({source}): {error}", stackTrace: true);
				}
				if (error is HubException)
				{
					UniLog.Log("Running manual reconnect (" + source + ")");
					await Connect(connection, source, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
			}
		};
		connection.RegisterSpoke((IHubDebugClient)HubClient);
		connection.RegisterSpoke((IModerationClient)Moderation);
		connection.RegisterSpoke((IHubMessagingClient)Messages);
		connection.RegisterSpoke((IHubStatusClient)HubStatusController);
		connection.RegisterSpoke<IHubNetworkingClient>((object)NetworkNodes);
		await Connect(connection, source, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (cancellationToken.IsCancellationRequested)
		{
			if (connection.State != HubConnectionState.Disconnected)
			{
				await connection.StopAsync().ConfigureAwait(continueOnCapturedContext: false);
				await connection.DisposeAsync().ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		else
		{
			connection.KeepAliveInterval = TimeSpan.FromSeconds(15L);
			UniLog.Log("Connected to SignalR (" + source + ")");
			HubClient = new AppHub(connection);
			HubStatusController.Initialize(cancellationToken);
			Status.ForceUpdate();
		}
	}

	private static async Task Connect(HubConnection connection, string source, CancellationToken cancellationToken)
	{
		bool connected = false;
		int attempt = 0;
		do
		{
			try
			{
				UniLog.Log("Connecting to SignalR (" + source + ")...");
				await connection.StartAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				connected = true;
			}
			catch (Exception ex)
			{
				if (!cancellationToken.IsCancellationRequested)
				{
					UniLog.Error("Exception connecting to SignalR (" + source + "):\n" + ex);
				}
				await Task.Delay(TimeSpan.FromSeconds(MathX.Min((float)attempt * 0.5f, 10f))).ConfigureAwait(continueOnCapturedContext: false);
				attempt++;
			}
		}
		while (!connected && !cancellationToken.IsCancellationRequested);
	}

	private async Task DisconnectFromHub()
	{
		if (HubClient != null)
		{
			AppHub _oldHub = HubClient;
			HubClient = null;
			await _oldHub.Hub.StopAsync().ConfigureAwait(continueOnCapturedContext: false);
			await _oldHub.Hub.DisposeAsync().ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	public virtual void Update()
	{
		Session.Update();
		Stats.Update();
		Sessions.Update();
		Variables.Update();
		Status.Update();
		Contacts.Update();
		Messages.Update();
		NetworkNodes.Update();
	}

	internal async Task Login()
	{
		if (SignalREndpoint != null)
		{
			try
			{
				await ConnectToHub("UserLogin: " + CurrentUserID).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (Exception ex)
			{
				UniLog.Error("Exception when connecting to hub:\n" + ex, stackTrace: false);
			}
		}
		Status.SignIn();
		Variables.SignIn();
		Groups.UpdateCurrentUserMemberships();
		Status.Update();
		Contacts.Update();
		await Profile.SignIn().ConfigureAwait(continueOnCapturedContext: false);
		await OnLogin().ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task FinalizeSession()
	{
		await Session.FinalizeSession().ConfigureAwait(continueOnCapturedContext: false);
		await Task.WhenAll(Variables.SignOut(), Status.SignOut(), HubStatusController.SignOut()).ConfigureAwait(continueOnCapturedContext: false);
	}

	internal async Task BeginLogout(bool isManual)
	{
		await OnLogout(isManual).ConfigureAwait(continueOnCapturedContext: false);
		await FinalizeSession().ConfigureAwait(continueOnCapturedContext: false);
		Task.Run((Func<Task?>)DisconnectFromHub);
	}

	internal void ResetModules()
	{
		Security.Reset();
		Profile.Reset();
		Storage.Reset();
		Groups.Reset();
		Contacts.Reset();
		Messages.Reset();
		Migration.Reset();
	}

	internal void CompleteLogout(bool isManual)
	{
		if (isManual)
		{
			Task.Run(async delegate
			{
				await ConnectToHub("OnLogout").ConfigureAwait(continueOnCapturedContext: false);
			});
		}
	}

	public bool HasPotentialAccess(string ownerId)
	{
		return IdUtil.GetOwnerType(ownerId) switch
		{
			OwnerType.Machine => true, 
			OwnerType.User => ownerId == Session.CurrentUserID, 
			OwnerType.Group => Groups.CurrentUserMemberships.Any((Membership m) => m.GroupId == ownerId), 
			_ => false, 
		};
	}

	public async Task<bool> InteractiveConsoleLogin(bool tryUseCommandLineArgs = false)
	{
		bool loggedIn = false;
		do
		{
			bool usedCommandLineArgs = false;
			string login = null;
			string pass = null;
			if (tryUseCommandLineArgs)
			{
				string[] commandLineArgs = Environment.GetCommandLineArgs();
				if (commandLineArgs.Length == 3)
				{
					login = commandLineArgs[1];
					pass = commandLineArgs[2];
					usedCommandLineArgs = true;
				}
			}
			if (!usedCommandLineArgs)
			{
				Console.Write("Login: ");
				login = await Task.Run(() => Console.ReadLine());
				Console.Write("Password: ");
				pass = await Task.Run(() => Console.ReadLine());
			}
			Console.WriteLine("Logging in...");
			string id = SecretMachineId ?? Guid.NewGuid().ToString();
			CloudResult<UserSessionResult<UserSession>> cloudResult = await HandleConsoleLogin((string code) => Session.Login(login, new PasswordLogin(pass), id, rememberMe: false, code)).ConfigureAwait(continueOnCapturedContext: false);
			login = null;
			pass = null;
			if (cloudResult.IsError)
			{
				ConsoleColor foregroundColor = Console.ForegroundColor;
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("Error!\t" + cloudResult.Content);
				Console.ForegroundColor = foregroundColor;
				if (usedCommandLineArgs)
				{
					return false;
				}
			}
			else
			{
				loggedIn = true;
				Console.Clear();
			}
		}
		while (!loggedIn);
		return true;
	}

	public async Task<T> HandleConsoleLogin<T>(ConsoleLoginHandler<T> handler) where T : CloudResult
	{
		T val = await handler(null).ConfigureAwait(continueOnCapturedContext: false);
		if (val.Content == "TOTP")
		{
			Console.Write("2FA Code: ");
			val = await handler(await Task.Run(() => Console.ReadLine())).ConfigureAwait(continueOnCapturedContext: false);
		}
		return val;
	}

	public async Task<bool> InteractiveConsoleRegister()
	{
		Console.Write("Username: ");
		string username = await Task.Run(() => Console.ReadLine().Trim());
		Console.Write("Email: ");
		string email = await Task.Run(() => Console.ReadLine().Trim());
		Console.Write("Password: ");
		string password = await Task.Run(() => Console.ReadLine().Trim());
		bool validInput = false;
		int birthMonth = 0;
		while (!validInput)
		{
			Console.Write("Birth Month (number): ");
			if (!int.TryParse(await Task.Run(() => Console.ReadLine().Trim()), out birthMonth))
			{
				Console.WriteLine("Invalid birth month entered! Please enter a valid month.");
			}
			else
			{
				validInput = true;
			}
		}
		validInput = false;
		int birthDay = 0;
		while (!validInput)
		{
			Console.Write("Birth Day (number): ");
			if (!int.TryParse(await Task.Run(() => Console.ReadLine().Trim()), out birthDay))
			{
				Console.WriteLine("Invalid birth day entered! Please enter a valid day.");
			}
			else
			{
				validInput = true;
			}
		}
		validInput = false;
		int result = 0;
		while (!validInput)
		{
			Console.Write("Birth Year (number): ");
			if (!int.TryParse(await Task.Run(() => Console.ReadLine().Trim()), out result))
			{
				Console.WriteLine("Invalid birth year entered! Please enter a valid year.");
			}
			else
			{
				validInput = true;
			}
		}
		DateTimeOffset dateOfBirth = new DateTime(result, birthMonth, birthDay);
		Console.WriteLine("Registering...");
		CloudResult<RegistrationStatus> cloudResult = await Users.Register(username, email, password, dateOfBirth).ConfigureAwait(continueOnCapturedContext: false);
		if (cloudResult.IsError)
		{
			Console.WriteLine("Error registering user:\n" + cloudResult.Content);
			return false;
		}
		string userId = cloudResult.Entity.Id;
		string token = cloudResult.Entity.Token;
		Console.WriteLine("Registration accepted - waiting to be processed...");
		string text = await WaitForRegistration(userId, token).ConfigureAwait(continueOnCapturedContext: false);
		if (text != null)
		{
			Console.WriteLine("Error processing user registration:\n" + text);
			return false;
		}
		Console.WriteLine("User (" + userId + ") registered - retrieving session data...");
		CloudResult<UserSessionResult<User>> cloudResult2 = await Users.GetRegisteredSession(userId, token).ConfigureAwait(continueOnCapturedContext: false);
		if (cloudResult2.IsError)
		{
			Console.WriteLine("Unable to get session data: " + cloudResult2.Content);
			return false;
		}
		Console.WriteLine("Registration complete! Please verify your email.");
		Console.Write("Waiting for email verification... ");
		CloudResult<User> cloudResult3;
		do
		{
			await Task.Delay(TimeSpan.FromSeconds(5L));
			cloudResult3 = await Users.GetUser(userId);
		}
		while (!cloudResult3.IsOK || !cloudResult3.Entity.IsVerified);
		Console.WriteLine("Email verified!");
		return true;
	}

	/// <summary>
	/// Waits for a user's registration to complete
	/// </summary>
	/// <param name="id"></param>
	/// <param name="token"></param>
	/// <returns>Error encountered, or <c>null</c> when successful</returns>
	private async Task<string> WaitForRegistration(string id, string token)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id, "id");
		ArgumentException.ThrowIfNullOrWhiteSpace(token, "token");
		int statusCheckErrors = 0;
		while (true)
		{
			await Task.Delay(5000);
			CloudResult<RegistrationStatus> cloudResult = await Users.GetRegistrationStatus(id, token).ConfigureAwait(continueOnCapturedContext: false);
			if (cloudResult.IsError)
			{
				int num = statusCheckErrors + 1;
				statusCheckErrors = num;
				if (num >= 3)
				{
					return "Error checking status of registration: " + cloudResult.Content;
				}
				continue;
			}
			if (cloudResult.Entity.State == RegistrationState.Failed)
			{
				return "Error during registration: " + cloudResult.Entity.Error;
			}
			if (cloudResult.Entity.State == RegistrationState.Done)
			{
				break;
			}
		}
		return null;
	}

	public string GetOwnerPath(string ownerId)
	{
		return IdUtil.GetOwnerType(ownerId) switch
		{
			OwnerType.Group => "groups", 
			OwnerType.User => "users", 
			_ => throw new Exception("Invalid owner type: " + ownerId), 
		};
	}

	internal void ProcessUserSessionResult<T>(UserSessionResult<T> result)
	{
		if (result?.ConfigFiles == null)
		{
			return;
		}
		foreach (ConfigFileData configFile in result.ConfigFiles)
		{
			InstallConfigFile(configFile.Path, configFile.Content);
		}
	}

	public async Task<ExitMessage> GetRandomExitMessage()
	{
		return (await Api.GET<ExitMessage>("exitMessage").ConfigureAwait(continueOnCapturedContext: false))?.Entity;
	}
}
public abstract class SkyFrostModule
{
	public SkyFrostInterface Cloud { get; private set; }

	public ApiClient Api => Cloud.Api;

	public IPlatformProfile Platform => Cloud.Platform;

	public bool IsUserSignedIn => CurrentUser != null;

	public User CurrentUser => Cloud.Session.CurrentUser;

	public string CurrentUserID => CurrentUser?.Id;

	public string CurrentUsername => CurrentUser?.Username;

	public SkyFrostModule(SkyFrostInterface cloud)
	{
		Cloud = cloud;
	}
}
public delegate void ApiAuthenticator(HttpRequestMessage request, string totpCode);
public delegate MemoryStream MemoryStreamAllocator();
public class ApiClient
{
	private static readonly MediaTypeHeaderValue JSON_MEDIA_TYPE = new MediaTypeHeaderValue("application/json")
	{
		CharSet = "utf-8"
	};

	private static readonly HttpMethod PATCH_METHOD = new HttpMethod("PATCH");

	private static readonly HttpMethod COPY_METHOD = new HttpMethod("COPY");

	public TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30L);

	public int DefaultRetries = 10;

	public readonly HttpClient Client;

	public readonly string ApiBase;

	public readonly string SecretClientAccessKey;

	private readonly ApiAuthenticator authenticator;

	private readonly MemoryStreamAllocator memoryAllocator;

	private readonly Action<string> profilerBeginSampleCallback;

	private readonly Action profilerEndSampleCallback;

	public DateTime LastServerResponse { get; private set; }

	public ApiClient(HttpClient client, string apiBase, ApiAuthenticator authenticator, string secretClientAccessKey = null, MemoryStreamAllocator memoryAllocator = null, Action<string> profilerBeginSampleCallback = null, Action profilerEndSampleCallback = null)
	{
		Client = client;
		ApiBase = apiBase;
		SecretClientAccessKey = secretClientAccessKey;
		if (ApiBase != null && ApiBase.EndsWith("/"))
		{
			ApiBase = ApiBase.Substring(0, ApiBase.Length - 1);
		}
		this.authenticator = authenticator;
		this.memoryAllocator = memoryAllocator;
		this.profilerBeginSampleCallback = profilerBeginSampleCallback;
		this.profilerEndSampleCallback = profilerEndSampleCallback;
	}

	[Conditional("PROFILE")]
	private void ProfilerBeginSample(string name)
	{
		profilerBeginSampleCallback?.Invoke(name);
	}

	[Conditional("PROFILE")]
	private void ProfilerEndSample()
	{
		profilerEndSampleCallback?.Invoke();
	}

	public Task<CloudResult<T>> GET<T>(string resource, TimeSpan? timeout = null, bool throwOnError = true) where T : class, new()
	{
		return RunRequest<T>(() => CreateRequest(resource, HttpMethod.Get), timeout, throwOnError);
	}

	public Task<CloudResult<T>> HEAD<T>(string resource, TimeSpan? timeout = null, bool throwOnError = true) where T : class, new()
	{
		return RunRequest<T>(() => CreateRequest(resource, HttpMethod.Head), timeout, throwOnError);
	}

	public Task<CloudResult<T>> POST<T>(string resource, object entity, TimeSpan? timeout = null, string totpCode = null, bool throwOnError = true, Action<HttpRequestMessage> postprocessRequest = null) where T : class, new()
	{
		return RunRequest<T>(delegate
		{
			HttpRequestMessage httpRequestMessage = CreateRequest(resource, HttpMethod.Post, totpCode, postprocessRequest);
			if (entity != null)
			{
				AddBody(httpRequestMessage, entity);
			}
			return httpRequestMessage;
		}, timeout, throwOnError);
	}

	public Task<CloudResult<T>> POST_File<T>(string resource, string filePath, string fileMIME = null, IProgressIndicator progressIndicator = null) where T : class, new()
	{
		return RunRequest<T>(delegate
		{
			HttpRequestMessage httpRequestMessage = CreateRequest(resource, HttpMethod.Post);
			AddMultipartFileToRequest(httpRequestMessage, filePath, fileMIME, progressIndicator);
			return httpRequestMessage;
		}, TimeSpan.FromMinutes(60L), throwOnError: true);
	}

	public Task<CloudResult<T>> PUT<T>(string resource, object entity, TimeSpan? timeout = null, bool throwOnError = true, string totpCode = null, Action<HttpRequestMessage> postprocessRequest = null) where T : class, new()
	{
		return RunRequest<T>(delegate
		{
			HttpRequestMessage httpRequestMessage = CreateRequest(resource, HttpMethod.Put, totpCode, postprocessRequest);
			if (entity != null)
			{
				AddBody(httpRequestMessage, entity);
			}
			return httpRequestMessage;
		}, timeout, throwOnError);
	}

	public Task<CloudResult<T>> PATCH<T>(string resource, object entity, TimeSpan? timeout = null, bool throwOnError = true, string totpCode = null, Action<HttpRequestMessage> postprocessRequest = null) where T : class, new()
	{
		return RunRequest<T>(delegate
		{
			HttpRequestMessage httpRequestMessage = CreateRequest(resource, PATCH_METHOD, totpCode, postprocessRequest);
			if (entity != null)
			{
				AddBody(httpRequestMessage, entity);
			}
			return httpRequestMessage;
		}, timeout, throwOnError);
	}

	public Task<CloudResult<T>> DELETE<T>(string resource, TimeSpan? timeout = null, string totpCode = null, bool throwOnError = true) where T : class, new()
	{
		return RunRequest<T>(() => CreateRequest(resource, HttpMethod.Delete, totpCode), timeout, throwOnError);
	}

	public Task<CloudResult> GET(string resource, TimeSpan? timeout = null, bool throwOnError = true)
	{
		return RunRequest(() => CreateRequest(resource, HttpMethod.Get), timeout, throwOnError);
	}

	public Task<CloudResult> HEAD(string resource, TimeSpan? timeout = null, bool throwOnError = true)
	{
		return RunRequest(() => CreateRequest(resource, HttpMethod.Head), timeout, throwOnError);
	}

	public Task<CloudResult> COPY(string resource, TimeSpan? timeout = null, bool throwOnError = true, string totpCode = null, Action<HttpRequestMessage> postprocessRequest = null)
	{
		return RunRequest(() => CreateRequest(resource, COPY_METHOD, totpCode, postprocessRequest), timeout, throwOnError);
	}

	public Task<CloudResult> POST(string resource, object entity, TimeSpan? timeout = null, string totpCode = null, bool throwOnError = true)
	{
		return RunRequest(delegate
		{
			HttpRequestMessage httpRequestMessage = CreateRequest(resource, HttpMethod.Post, totpCode);
			if (entity != null)
			{
				AddBody(httpRequestMessage, entity);
			}
			return httpRequestMessage;
		}, timeout, throwOnError);
	}

	public Task<CloudResult> POST_FileMultipart(string resource, string filePath, string fileMIME = null, IProgressIndicator progressIndicator = null, string totpCode = null, Action<HttpRequestMessage> postprocessRequest = null)
	{
		return RunRequest(delegate
		{
			HttpRequestMessage httpRequestMessage = CreateRequest(resource, HttpMethod.Post, totpCode, postprocessRequest);
			AddMultipartFileToRequest(httpRequestMessage, filePath, fileMIME, progressIndicator);
			return httpRequestMessage;
		}, TimeSpan.FromMinutes(60L), throwOnError: true);
	}

	public Task<CloudResult> POST_FileDirect(string resource, string filePath, string fileMIME = null, IProgressIndicator progressIndicator = null, string totpCode = null, Action<HttpRequestMessage> postprocessRequest = null)
	{
		return RunRequest(delegate
		{
			HttpRequestMessage httpRequestMessage = CreateRequest(resource, HttpMethod.Post, totpCode, postprocessRequest);
			AddFileToRequest(httpRequestMessage, filePath, fileMIME, progressIndicator);
			return httpRequestMessage;
		}, TimeSpan.FromMinutes(60L), throwOnError: true);
	}

	public Task<CloudResult> PUT_FileMultipart(string resource, string filePath, string fileMIME = null, IProgressIndicator progressIndicator = null, string totpCode = null, Action<HttpRequestMessage> postprocessRequest = null)
	{
		return RunRequest(delegate
		{
			HttpRequestMessage httpRequestMessage = CreateRequest(resource, HttpMethod.Put, totpCode, postprocessRequest);
			AddMultipartFileToRequest(httpRequestMessage, filePath, fileMIME, progressIndicator);
			return httpRequestMessage;
		}, TimeSpan.FromMinutes(60L), throwOnError: true);
	}

	public Task<CloudResult> PUT_FileDirect(string resource, string filePath, string fileMIME = null, IProgressIndicator progressIndicator = null, string totpCode = null, Action<HttpRequestMessage> postprocessRequest = null)
	{
		return RunRequest(delegate
		{
			HttpRequestMessage httpRequestMessage = CreateRequest(resource, HttpMethod.Put, totpCode, postprocessRequest);
			AddFileToRequest(httpRequestMessage, filePath, fileMIME, progressIndicator);
			return httpRequestMessage;
		}, TimeSpan.FromMinutes(60L), throwOnError: true);
	}

	public Task<CloudResult> PUT(string resource, object entity, TimeSpan? timeout = null, bool throwOnError = true)
	{
		return RunRequest(delegate
		{
			HttpRequestMessage httpRequestMessage = CreateRequest(resource, HttpMethod.Put);
			if (entity != null)
			{
				AddBody(httpRequestMessage, entity);
			}
			return httpRequestMessage;
		}, timeout, throwOnError);
	}

	public Task<CloudResult> PATCH(string resource, object entity, TimeSpan? timeout = null, bool throwOnError = true, string totpCode = null, Action<HttpRequestMessage> postprocessRequest = null)
	{
		return RunRequest(delegate
		{
			HttpRequestMessage httpRequestMessage = CreateRequest(resource, PATCH_METHOD, totpCode, postprocessRequest);
			if (entity != null)
			{
				AddBody(httpRequestMessage, entity);
			}
			return httpRequestMessage;
		}, timeout, throwOnError);
	}

	public Task<CloudResult> DELETE(string resource, TimeSpan? timeout = null, string totpCode = null, Action<HttpRequestMessage> postprocess = null, bool throwOnError = true)
	{
		return RunRequest(() => CreateRequest(resource, HttpMethod.Delete, totpCode, postprocess), timeout, throwOnError);
	}

	public HttpRequestMessage CreateRequest(string resource, HttpMethod method, string totpCode = null, Action<HttpRequestMessage> postprocess = null)
	{
		bool authenticate;
		if (Uri.IsWellFormedUriString(resource, UriKind.Relative))
		{
			if (resource.StartsWith("/"))
			{
				resource = resource.Substring(1);
			}
			authenticate = true;
			resource = ApiBase + "/" + resource;
		}
		else
		{
			if (!Uri.IsWellFormedUriString(resource, UriKind.Absolute))
			{
				throw new ArgumentException("Resource is neither relative nor absolute URL");
			}
			authenticate = (resource.StartsWith(ApiBase) ? true : false);
		}
		return CreateRequest(new Uri(resource), authenticate, method, totpCode, postprocess);
	}

	public HttpRequestMessage CreateRequest(Uri resource, bool authenticate, HttpMethod method, string totpCode = null, Action<HttpRequestMessage> postprocess = null)
	{
		HttpRequestMessage httpRequestMessage = new HttpRequestMessage(method, resource);
		if (authenticate)
		{
			authenticator?.Invoke(httpRequestMessage, totpCode);
		}
		if (!string.IsNullOrEmpty(SecretClientAccessKey))
		{
			httpRequestMessage.Headers.Add("SecretClientAccessKey", SecretClientAccessKey);
		}
		postprocess?.Invoke(httpRequestMessage);
		return httpRequestMessage;
	}

	public void AddFileToRequest(HttpRequestMessage request, string filePath, string mime = null, IProgressIndicator progressIndicator = null)
	{
		FileStream fileStream = File.OpenRead(filePath);
		StreamContent streamContent = new StreamContent(new StreamProgressWrapper(fileStream, progressIndicator), 32768);
		if (mime != null)
		{
			streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mime);
		}
		streamContent.Headers.ContentLength = fileStream.Length;
		request.Content = streamContent;
	}

	public void AddMultipartFileToRequest(HttpRequestMessage request, string filePath, string mime = null, IProgressIndicator progressIndicator = null)
	{
		FileStream fileStream = File.OpenRead(filePath);
		StreamProgressWrapper content = new StreamProgressWrapper(fileStream, progressIndicator);
		MultipartFormDataContent multipartFormDataContent = new MultipartFormDataContent();
		StreamContent streamContent = new StreamContent(content, 32768);
		if (mime != null)
		{
			streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mime);
		}
		streamContent.Headers.ContentLength = fileStream.Length;
		multipartFormDataContent.Add(streamContent, "file", Path.GetFileName(filePath));
		request.Content = multipartFormDataContent;
	}

	private void AddBody(HttpRequestMessage message, object entity)
	{
		if (entity == null)
		{
			throw new ArgumentNullException("entity");
		}
		try
		{
			if (SkyFrostInterface.UseNewtonsoftJson)
			{
				message.Content = new System.Net.Http.StringContent(JsonConvert.SerializeObject(entity), Encoding.UTF8, "application/json");
				return;
			}
			MemoryStream memoryStream = memoryAllocator?.Invoke() ?? new MemoryStream();
			using (Utf8JsonWriter writer = new Utf8JsonWriter((Stream)memoryStream, default(JsonWriterOptions)))
			{
				System.Text.Json.JsonSerializer.Serialize(writer, entity, entity?.GetType() ?? typeof(object));
			}
			memoryStream.Seek(0L, SeekOrigin.Begin);
			StreamContent streamContent = new StreamContent(memoryStream);
			streamContent.Headers.ContentType = JSON_MEDIA_TYPE;
			message.Content = streamContent;
		}
		catch (Exception value)
		{
			UniLog.Error($"Exception serializing {entity?.GetType()} for request: {message.RequestUri}\n{entity}\n{value}");
			throw;
		}
	}

	internal async Task<CloudResult> RunRequest(Func<HttpRequestMessage> requestSource, TimeSpan? timeout, bool throwOnError)
	{
		return await RunRequest<string>(requestSource, timeout, throwOnError).ConfigureAwait(continueOnCapturedContext: false);
	}

	internal async Task<CloudResult<T>> RunRequest<T>(Func<HttpRequestMessage> requestSource, TimeSpan? timeout, bool throwOnError) where T : class
	{
		DateTime start = DateTime.UtcNow;
		HttpRequestMessage request = null;
		HttpResponseMessage result = null;
		Exception exception = null;
		int remainingRetries = DefaultRetries;
		int delay = 250;
		bool success = false;
		int attempts = 0;
		HttpStatusCode statusCode;
		do
		{
			DateTime? sendStart = null;
			try
			{
				request?.Dispose();
				request = requestSource();
				CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(timeout ?? DefaultTimeout);
				if (SkyFrostInterface.DEBUG_REQUESTS)
				{
					UniLog.Log($"{request.Method} - {request.RequestUri}");
				}
				sendStart = DateTime.UtcNow;
				attempts++;
				result = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationTokenSource.Token).ConfigureAwait(continueOnCapturedContext: false);
				success = true;
				if (SkyFrostInterface.DEBUG_REQUESTS)
				{
					UniLog.Log($"RESULT for {request.Method} - {request.RequestUri}:\n{result.StatusCode}");
				}
			}
			catch (Exception ex)
			{
				exception = ex;
			}
			TimeSpan? timeSpan = DateTime.UtcNow - sendStart;
			statusCode = result?.StatusCode ?? ((HttpStatusCode)0);
			if (result == null || statusCode == HttpStatusCode.TooManyRequests || statusCode == HttpStatusCode.InternalServerError || statusCode == HttpStatusCode.BadGateway || statusCode == HttpStatusCode.ServiceUnavailable || statusCode == HttpStatusCode.GatewayTimeout || statusCode == (HttpStatusCode)522)
			{
				if (exception is TaskCanceledException)
				{
					UniLog.Log($"{request?.Method} request to {request?.RequestUri} timed out. Remaining retries: {remainingRetries}. Elapsed: {GetRequestDuration()}");
				}
				else if (result == null)
				{
					UniLog.Log($"Exception running {request?.Method} request to {request?.RequestUri}. Remaining retries: {remainingRetries}. Elapsed: {GetRequestDuration()}\n" + exception);
				}
				else if (statusCode == HttpStatusCode.InternalServerError || statusCode == HttpStatusCode.ServiceUnavailable)
				{
					UniLog.Log($"Server Error running {request?.Method} request to {request?.RequestUri}. Remaining retries: {remainingRetries}. RequestTime: {timeSpan?.TotalSeconds.ToString("F2") + "s"}, Total Elapsed: {GetRequestDuration()}");
				}
				success = false;
				await Task.Delay(delay).ConfigureAwait(continueOnCapturedContext: false);
				delay *= 2;
				delay = MathX.Min(15000, delay);
			}
		}
		while (!success && remainingRetries-- > 0);
		if (result == null)
		{
			request?.Dispose();
			if (exception is TaskCanceledException)
			{
				return new CloudResult<T>(null, HttpStatusCode.RequestTimeout, null, attempts);
			}
			if (!throwOnError)
			{
				return new CloudResult<T>(null, (HttpStatusCode)0, null, attempts);
			}
			if (exception == null)
			{
				throw new Exception($"Failed to get response. Last status code: {statusCode}, Exception is null. Elapsed: {GetRequestDuration()}");
			}
			throw exception;
		}
		T entity = null;
		string content = null;
		if (result.IsSuccessStatusCode)
		{
			if (request.RequestUri.OriginalString.Contains(ApiBase))
			{
				LastServerResponse = DateTime.UtcNow;
			}
			if (request.Method != HttpMethod.Head && result.StatusCode != HttpStatusCode.NoContent)
			{
				if (typeof(T) == typeof(string))
				{
					content = await result.Content.ReadAsStringAsync().ConfigureAwait(continueOnCapturedContext: false);
					entity = content as T;
				}
				else
				{
					try
					{
						if (SkyFrostInterface.UseNewtonsoftJson)
						{
							content = await result.Content.ReadAsStringAsync().ConfigureAwait(continueOnCapturedContext: false);
							entity = JsonConvert.DeserializeObject<T>(content);
						}
						else if (result.Content.Headers.ContentLength > 0 || !result.Content.Headers.ContentLength.HasValue)
						{
							using Stream responseStream = await result.Content.ReadAsStreamAsync().ConfigureAwait(continueOnCapturedContext: false);
							entity = await System.Text.Json.JsonSerializer.DeserializeAsync<T>(responseStream).ConfigureAwait(continueOnCapturedContext: false);
						}
						if (SkyFrostInterface.DEBUG_REQUESTS)
						{
							UniLog.Log($"ENTITY for {request.Method} - {request.RequestUri}:\n" + System.Text.Json.JsonSerializer.Serialize(entity));
						}
					}
					catch (Exception ex2)
					{
						UniLog.Log($"Exception deserializing {typeof(T)} response from {request.Method}:{request.RequestUri}\nException:\n" + ex2);
					}
					finally
					{
						_ = 0;
					}
				}
			}
		}
		else
		{
			content = await result.Content.ReadAsStringAsync().ConfigureAwait(continueOnCapturedContext: false);
			if (SkyFrostInterface.DEBUG_REQUESTS)
			{
				UniLog.Log($"CONTENT for {request.Method} - {request.RequestUri}:\n{content}");
			}
		}
		CloudResult<T> result2 = new CloudResult<T>(entity, result.StatusCode, result.Headers, attempts, content, result.Content.Headers.ContentType?.ToString(), result.Content.Headers.ContentLength, result.Content.Headers.LastModified);
		result.Dispose();
		request.Dispose();
		return result2;
		string GetRequestDuration()
		{
			return (DateTime.UtcNow - start).TotalSeconds.ToString("F2") + "s";
		}
	}
}
public enum GatherJobState
{
	Waiting,
	Initiating,
	Gathering,
	Finished,
	Failed
}
public abstract class AssetGatherer
{
	public const int DEFAULT_CONCURRENT_DOWNLOADS = 100;

	public const int DEFAULT_MAX_ATTEMPTS = 5;

	public int BufferSize = 32768;

	public int MaximumAttempts = 5;

	public string TemporaryPath;

	private Stack<byte[]> buffers = new Stack<byte[]>();

	private long _totalBytesPerSecond;

	private DateTime _lastSpeedUpdate;

	private long _speedAccumulatedBytes;

	private object _speedLock = new object();

	public SkyFrostInterface Cloud { get; private set; }

	public long TotalBytesPerSecond
	{
		get
		{
			BytesDownloaded(0L);
			return _totalBytesPerSecond;
		}
	}

	public AssetGatherer(SkyFrostInterface cloud)
	{
		if (cloud == null)
		{
			throw new ArgumentNullException("cloud");
		}
		Cloud = cloud;
	}

	internal byte[] BorrowBuffer()
	{
		lock (buffers)
		{
			while (buffers.Count > 0)
			{
				byte[] array = buffers.Pop();
				if (array.Length == BufferSize)
				{
					return array;
				}
			}
			return new byte[BufferSize];
		}
	}

	internal void ReturnBuffer(byte[] buffer)
	{
		if (buffer.Length != BufferSize)
		{
			return;
		}
		lock (buffers)
		{
			buffers.Push(buffer);
		}
	}

	internal void BytesDownloaded(long bytes)
	{
		lock (_speedLock)
		{
			_speedAccumulatedBytes += bytes;
			DateTime utcNow = DateTime.UtcNow;
			TimeSpan timeSpan = utcNow - _lastSpeedUpdate;
			if (timeSpan.TotalSeconds >= 1.0)
			{
				_totalBytesPerSecond = (int)((double)_speedAccumulatedBytes / timeSpan.TotalSeconds);
				_lastSpeedUpdate = utcNow;
				_speedAccumulatedBytes = 0L;
			}
		}
	}
}
public class AssetGatherer<G> : AssetGatherer where G : GatherJob, new()
{
	private Dictionary<object, ActionBlock<object>> processors = new Dictionary<object, ActionBlock<object>>();

	private Dictionary<object, List<G>> waitingJobs = new Dictionary<object, List<G>>();

	private Dictionary<Uri, G> jobs = new Dictionary<Uri, G>();

	private List<G> activeJobs = new List<G>();

	public int ActiveJobCount
	{
		get
		{
			lock (activeJobs)
			{
				return activeJobs.Count;
			}
		}
	}

	public AssetGatherer(SkyFrostInterface cloud)
		: base(cloud)
	{
	}

	private async Task ProcessJob(object category)
	{
		G job;
		lock (jobs)
		{
			List<G> list = waitingJobs[category];
			if (list.Count == 0)
			{
				return;
			}
			list.Sort((G a, G b) => -a.Priority.CompareTo(b.Priority));
			job = list.TakeFirst();
		}
		lock (activeJobs)
		{
			activeJobs.Add(job);
		}
		try
		{
			await job.Download().ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception value)
		{
			UniLog.Error($"Exception running GatherJob: {job}\n{value}");
		}
		lock (jobs)
		{
			jobs.Remove(job.URL);
		}
		lock (activeJobs)
		{
			activeJobs.Remove(job);
		}
	}

	public void SetCategoryParallelism(object category, int concurrentJobs)
	{
		SetCategoryParallelismIntern(category, concurrentJobs);
	}

	private ActionBlock<object> SetCategoryParallelismIntern(object category, int concurrentJobs)
	{
		lock (jobs)
		{
			if (processors.TryGetValue(category, out var value))
			{
				value.Complete();
			}
			if (!waitingJobs.ContainsKey(category))
			{
				waitingJobs.Add(category, new List<G>());
			}
			value = new ActionBlock<object>((Func<object, Task>)ProcessJob, new ExecutionDataflowBlockOptions
			{
				MaxDegreeOfParallelism = concurrentJobs,
				EnsureOrdered = true
			});
			processors[category] = value;
			return value;
		}
	}

	public G Gather(Uri url, float priority = 0f, Action<G> initialize = null)
	{
		lock (jobs)
		{
			if (jobs.TryGetValue(url, out var value))
			{
				return value;
			}
			value = new G();
			value.Initialize(this, url);
			value.Priority = priority;
			initialize?.Invoke(value);
			object categoryKey = value.CategoryKey;
			if (!processors.TryGetValue(categoryKey, out var value2))
			{
				value2 = SetCategoryParallelismIntern(categoryKey, 100);
			}
			jobs.Add(url, value);
			waitingJobs[categoryKey].Add(value);
			value2.Post(categoryKey);
			return value;
		}
	}

	public void GetActiveJobs(List<G> list)
	{
		lock (activeJobs)
		{
			list.AddRange(activeJobs);
		}
	}

	public void GetAllJobs(List<G> list)
	{
		lock (jobs)
		{
			list.AddRange(jobs.Values);
		}
	}
}
public abstract class BatchQuery<Query, Result> where Query : class, IEquatable<Query> where Result : class
{
	protected class QueryResult
	{
		public readonly Query query;

		public Result result;

		public QueryResult(Query query)
		{
			this.query = query;
		}
	}

	private object _lock = new object();

	private Dictionary<Query, TaskCompletionSource<Result>> queue = new Dictionary<Query, TaskCompletionSource<Result>>();

	private TaskCompletionSource<bool> immediateDispatch;

	private volatile bool dispatchScheduled;

	public int MaxBatchSize { get; set; } = 32;

	public float DelaySeconds { get; set; } = 0.25f;

	public BatchQuery(int maxBatchSize = 32, float delaySeconds = 0.25f)
	{
		MaxBatchSize = maxBatchSize;
		DelaySeconds = delaySeconds;
	}

	public async Task<Result> Request(Query query)
	{
		TaskCompletionSource<Result> value = null;
		lock (_lock)
		{
			if (!queue.TryGetValue(query, out value))
			{
				value = new TaskCompletionSource<Result>();
				queue.Add(query, value);
				if (!dispatchScheduled)
				{
					dispatchScheduled = true;
					immediateDispatch = new TaskCompletionSource<bool>();
					Task.Run(async delegate
					{
						try
						{
							await SendBatch().ConfigureAwait(continueOnCapturedContext: false);
						}
						catch (Exception value2)
						{
							UniLog.Error($"Exception when sending metadata batch query of type {typeof(Query)} with result {typeof(Result)}\n{value2}");
						}
					});
				}
				else if (queue.Count >= MaxBatchSize)
				{
					immediateDispatch.TrySetResult(result: true);
				}
			}
		}
		return await value.Task.ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task SendBatch()
	{
		await Task.WhenAny(immediateDispatch.Task, Task.Delay(TimeSpan.FromSeconds(DelaySeconds))).ConfigureAwait(continueOnCapturedContext: false);
		List<QueryResult> toSend = Pool.BorrowList<QueryResult>();
		lock (_lock)
		{
			foreach (KeyValuePair<Query, TaskCompletionSource<Result>> item in queue)
			{
				toSend.Add(new QueryResult(item.Key));
				if (toSend.Count == MaxBatchSize)
				{
					break;
				}
			}
		}
		Exception exception = null;
		try
		{
			if (toSend.Count > 0)
			{
				await RunBatch(toSend).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		catch (Exception ex)
		{
			UniLog.Error($"Exception running batch for metadata {typeof(Result)}\n{ex}");
			exception = ex;
		}
		lock (_lock)
		{
			foreach (QueryResult item2 in toSend)
			{
				if (exception != null)
				{
					queue[item2.query].SetException(exception);
				}
				else
				{
					queue[item2.query].SetResult(item2.result);
				}
				queue.Remove(item2.query);
			}
			if (queue.Count > 0)
			{
				if (queue.Count >= MaxBatchSize)
				{
					immediateDispatch.TrySetResult(result: true);
				}
				else
				{
					immediateDispatch = new TaskCompletionSource<bool>();
				}
				Task.Run((Func<Task?>)SendBatch);
			}
			else
			{
				dispatchScheduled = false;
			}
		}
	}

	protected abstract Task RunBatch(List<QueryResult> batch);
}
public class CloudResult
{
	public HttpStatusCode State { get; private set; }

	public HttpResponseHeaders Headers { get; private set; }

	public string Content { get; protected set; }

	public string ContentType { get; protected set; }

	public long? ContentLength { get; protected set; }

	public DateTimeOffset? LastModified { get; protected set; }

	public int RequestAttempts { get; protected set; }

	public bool IsOK
	{
		get
		{
			if (State != HttpStatusCode.OK && State != HttpStatusCode.NoContent)
			{
				return State == HttpStatusCode.Accepted;
			}
			return true;
		}
	}

	public bool IsError => !IsOK;

	public CloudResult(HttpStatusCode state, HttpResponseHeaders headers, int requestAttempts, string content, string contentType = null, long? contentSize = null, DateTimeOffset? lastModified = null)
	{
		State = state;
		RequestAttempts = requestAttempts;
		Content = content;
		Headers = headers;
		ContentType = contentType;
		ContentLength = contentSize;
		LastModified = lastModified;
		if (IsError && content != null)
		{
			try
			{
				Content = JsonConvert.DeserializeObject<CloudMessage>(content)?.Message;
			}
			catch
			{
				Content = content;
			}
		}
	}

	public override string ToString()
	{
		return $"CloudResult - State: {State}, Attempts: {RequestAttempts}, Content: {Content}";
	}

	public string TryGetHeaderValue(string name)
	{
		if (Headers == null)
		{
			return null;
		}
		if (Headers.TryGetValues(name, out IEnumerable<string> values))
		{
			return values.FirstOrDefault();
		}
		return null;
	}
}
public class CloudResult<T> : CloudResult
{
	public static readonly bool IsSensitive;

	public T Entity { get; private set; }

	static CloudResult()
	{
		IsSensitive = typeof(T).GetCustomAttribute<SensitiveEntityAttribute>() != null;
	}

	public CloudResult(T result, CloudResult sourceEntity)
		: this(result, sourceEntity.State, sourceEntity.Headers, sourceEntity.RequestAttempts, sourceEntity.Content, sourceEntity.ContentType, sourceEntity.ContentLength, sourceEntity.LastModified)
	{
	}

	public CloudResult(T result, HttpStatusCode state, HttpResponseHeaders headers, int requestAttempts, string content = null, string contentType = null, long? contentLength = null, DateTimeOffset? lastModified = null)
		: base(state, headers, requestAttempts, content, contentType, contentLength, lastModified)
	{
		Entity = result;
	}

	public CloudResult<E> AsResult<E>() where E : class
	{
		return new CloudResult<E>(Entity as E, base.State, base.Headers, base.RequestAttempts, base.Content, base.ContentType, base.ContentLength, base.LastModified);
	}

	public override string ToString()
	{
		string value = null;
		if (IsSensitive)
		{
			value = "[REDACTED]";
		}
		else if (Entity != null)
		{
			try
			{
				value = System.Text.Json.JsonSerializer.Serialize(Entity);
			}
			catch (Exception ex)
			{
				value = "EXCEPTION SERIALIZING: " + ex;
			}
		}
		return $"CloudResult<{typeof(T)}> - State: {base.State}, Attempts: {base.RequestAttempts}, Content: {(IsSensitive ? "[REDACTED]" : base.Content)}, Entity: {value}";
	}
}
public class GatherJob
{
	public static readonly object DEFAULT_CATEGORY = new object();

	private TaskCompletionSource<dummy> _taskSource;

	private DateTime _lastSpeedUpdate;

	private long _speedAccumulatedBytes;

	public Uri URL { get; private set; }

	public AssetGatherer Gatherer { get; private set; }

	public virtual object CategoryKey => DEFAULT_CATEGORY;

	public float Progress
	{
		get
		{
			if (TotalBytes == 0L)
			{
				return 0f;
			}
			return (float)((double)DownloadedBytes / (double)TotalBytes);
		}
	}

	public GatherJobState State { get; protected set; }

	public HttpStatusCode StatusCode { get; private set; }

	public float Priority { get; set; }

	public string FilePath { get; private set; }

	public string Error { get; private set; }

	public Exception Exception { get; private set; }

	public long TotalBytes { get; private set; }

	public long DownloadedBytes { get; private set; }

	public int BytesPerSecond { get; private set; }

	public int AttemptsLeft { get; private set; }

	public bool Active { get; private set; }

	public bool Completed { get; private set; }

	public DB_Endpoint AppDB_Endpoint { get; private set; }

	public DateTime GatheringStarted { get; private set; }

	public DateTime GatheringFinished { get; private set; }

	public Task Task => _taskSource.Task;

	public event Action<float> ProgressUpdated;

	internal void Initialize(AssetGatherer gatherer, Uri url)
	{
		if (URL != null)
		{
			throw new InvalidOperationException("GatherJob is already initialized");
		}
		URL = url;
		Gatherer = gatherer;
		State = GatherJobState.Waiting;
		_taskSource = new TaskCompletionSource<dummy>();
		AttemptsLeft = gatherer.MaximumAttempts;
		AppDB_Endpoint = DB_Endpoint.Default;
		FilePath = RandomX.LettersAndDigits(16);
		if (!string.IsNullOrEmpty(gatherer.TemporaryPath))
		{
			FilePath = Path.Combine(gatherer.TemporaryPath, FilePath);
		}
	}

	private Uri ResolveURL(Uri url, DB_Endpoint endpoint)
	{
		if (url.Scheme == "http" || url.Scheme == "https" || url.Scheme == "ftp")
		{
			return url;
		}
		if (url.Scheme == Gatherer.Cloud.Assets.DBScheme)
		{
			return Gatherer.Cloud.Assets.DBToHttp(url, endpoint);
		}
		throw new NotSupportedException("Unsupported scheme: " + url.Scheme);
	}

	public async Task Download()
	{
		Active = true;
		while (State != GatherJobState.Finished && AttemptsLeft-- > 0)
		{
			try
			{
				await RunDownload().ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (Exception ex)
			{
				Fail(ex.ToString(), nonRecoverable: false, ex);
			}
		}
		if (State == GatherJobState.Finished && File.Exists(FilePath))
		{
			FilePath = await FinishGather().ConfigureAwait(continueOnCapturedContext: false);
		}
		Active = false;
		Completed = true;
		_taskSource.SetResult(default(dummy));
	}

	protected void StartGathering()
	{
		if (State == GatherJobState.Finished)
		{
			throw new InvalidOperationException("The gather job already finished");
		}
		if (State == GatherJobState.Gathering)
		{
			throw new InvalidOperationException("The gather job is already gathering");
		}
		GatheringStarted = DateTime.UtcNow;
		State = GatherJobState.Gathering;
	}

	protected void FinishGathering(string newFilePath = null)
	{
		if (newFilePath != null)
		{
			FilePath = newFilePath;
		}
		GatheringFinished = DateTime.UtcNow;
		State = GatherJobState.Finished;
	}

	protected void Fail(string reason, bool nonRecoverable, Exception exception = null, HttpStatusCode statusCode = (HttpStatusCode)0)
	{
		State = GatherJobState.Failed;
		Exception = exception;
		Error = reason;
		StatusCode = statusCode;
		if (statusCode == HttpStatusCode.NotFound || statusCode == HttpStatusCode.Forbidden)
		{
			nonRecoverable = true;
		}
		if (nonRecoverable)
		{
			AttemptsLeft = 0;
		}
	}

	protected void AddDownloadedBytes(long delta)
	{
		UpdateBytes(TotalBytes, DownloadedBytes + delta);
	}

	protected void UpdateBytes(long total, long received)
	{
		long num = received - DownloadedBytes;
		if (num < 0 || total != TotalBytes)
		{
			_lastSpeedUpdate = DateTime.UtcNow;
			_speedAccumulatedBytes = 0L;
		}
		else
		{
			Gatherer.BytesDownloaded(num);
			_speedAccumulatedBytes += num;
			DateTime utcNow = DateTime.UtcNow;
			TimeSpan timeSpan = utcNow - _lastSpeedUpdate;
			if (timeSpan.TotalSeconds >= 1.0)
			{
				BytesPerSecond = (int)((double)_speedAccumulatedBytes / timeSpan.TotalSeconds);
				_lastSpeedUpdate = utcNow;
				_speedAccumulatedBytes = 0L;
			}
		}
		TotalBytes = total;
		DownloadedBytes = received;
		this.ProgressUpdated?.Invoke(Progress);
	}

	protected virtual async Task RunDownload()
	{
		State = GatherJobState.Initiating;
		Uri requestUri = ResolveURL(URL, AppDB_Endpoint);
		using HttpResponseMessage response = await Gatherer.Cloud.Api.Client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(continueOnCapturedContext: false);
		StatusCode = response.StatusCode;
		if (!response.IsSuccessStatusCode)
		{
			Fail(response.StatusCode.ToString(), nonRecoverable: false, null, response.StatusCode);
			return;
		}
		StartGathering();
		UpdateBytes(response.Content.Headers.ContentLength ?? (-1), 0L);
		if (File.Exists(FilePath))
		{
			File.Delete(FilePath);
		}
		using (Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(continueOnCapturedContext: false))
		{
			using FileStream fileStream = new FileStream(FilePath, FileMode.CreateNew);
			byte[] buffer = Gatherer.BorrowBuffer();
			try
			{
				int read;
				do
				{
					read = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(continueOnCapturedContext: false);
					if (read > 0)
					{
						await fileStream.WriteAsync(buffer, 0, read).ConfigureAwait(continueOnCapturedContext: false);
						AddDownloadedBytes(read);
					}
				}
				while (read > 0);
			}
			finally
			{
				Gatherer.ReturnBuffer(buffer);
			}
		}
		if (TotalBytes < 0)
		{
			TotalBytes = DownloadedBytes;
		}
		if (DownloadedBytes == TotalBytes)
		{
			FinishGathering();
		}
		else
		{
			Fail("Size mismatch", nonRecoverable: false, null, response.StatusCode);
		}
	}

	protected virtual Task<string> FinishGather()
	{
		return Task.FromResult(FilePath);
	}

	public override string ToString()
	{
		return $"GatherJob {URL}, State: {State}, StatusCode: {StatusCode}, Progress: {Progress * 100f:F2}, Started: {GatheringStarted}";
	}
}
public class InfiniteRetryPolicy : IRetryPolicy
{
	public readonly TimeSpan[] Intervals;

	public InfiniteRetryPolicy(params int[] secondIntervals)
	{
		Intervals = secondIntervals.Select((int s) => TimeSpan.FromSeconds(s)).ToArray();
	}

	public InfiniteRetryPolicy(params TimeSpan[] intervals)
	{
		Intervals = intervals;
	}

	public TimeSpan? NextRetryDelay(RetryContext retryContext)
	{
		return Intervals[MathX.Clamp(retryContext.PreviousRetryCount, 0L, Intervals.Length)];
	}
}
public static class IPAddressExtensions
{
	public static bool IsLinkLocalAddress(this IPAddress ipAddress)
	{
		if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
		{
			byte[] addressBytes = ipAddress.GetAddressBytes();
			if (addressBytes[0] == 169)
			{
				return addressBytes[1] == 254;
			}
			return false;
		}
		if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
		{
			return ipAddress.IsIPv6LinkLocal;
		}
		return false;
	}
}
public class MetadataBatchQuery<M> : BatchQuery<string, M> where M : class, IAssetMetadata, new()
{
	private AssetInterface assetInterface;

	public MetadataBatchQuery(SkyFrostInterface cloud)
		: base(32, 0.25f)
	{
		assetInterface = cloud.Assets;
	}

	protected override async Task RunBatch(List<QueryResult> batch)
	{
		List<string> hashes = Pool.BorrowList<string>();
		foreach (QueryResult item in batch)
		{
			hashes.Add(item.query);
		}
		CloudResult<List<M>> cloudResult = await assetInterface.GetAssetMetadata<M>(hashes).ConfigureAwait(continueOnCapturedContext: false);
		Pool.Return(ref hashes);
		if (!cloudResult.IsOK)
		{
			return;
		}
		foreach (M metadata in cloudResult.Entity)
		{
			QueryResult queryResult = batch.FirstOrDefault((QueryResult q) => q.query == metadata.AssetIdentifier);
			if (queryResult != null)
			{
				queryResult.result = metadata;
			}
		}
	}
}
public class RecordBatchQuery<R> : BatchQuery<RecordId, R> where R : class, IRecord, new()
{
	private RecordsManager records;

	public RecordBatchQuery(SkyFrostInterface cloud)
		: base(32, 0.25f)
	{
		records = cloud.Records;
	}

	public RecordBatchQuery(RecordsManager records)
		: base(32, 0.25f)
	{
		this.records = records;
	}

	protected override async Task RunBatch(List<QueryResult> batch)
	{
		List<RecordId> ids = Pool.BorrowList<RecordId>();
		foreach (QueryResult item in batch)
		{
			ids.Add(item.query);
		}
		CloudResult<List<R>> cloudResult = await records.GetRecords<R>(ids).ConfigureAwait(continueOnCapturedContext: false);
		Pool.Return(ref ids);
		if (!cloudResult.IsOK)
		{
			return;
		}
		foreach (R record in cloudResult.Entity)
		{
			QueryResult queryResult = batch.FirstOrDefault((QueryResult q) => q.query.OwnerId == record.OwnerId && q.query.Id == record.RecordId);
			if (queryResult != null)
			{
				queryResult.result = record;
			}
		}
	}
}
public class RecordCache<TRecord> where TRecord : class, IRecord, new()
{
	private Dictionary<RecordId, TRecord> cached = new Dictionary<RecordId, TRecord>();

	public RecordsManager Records { get; private set; }

	public RecordCache(RecordsManager records)
	{
		Records = records;
	}

	public RecordCache(SkyFrostInterface cloud)
	{
		Records = cloud.Records;
	}

	public Task<TRecord> Get(string ownerId, string recordId)
	{
		return Get(new RecordId(ownerId, recordId));
	}

	public async Task<TRecord> Get(RecordId recordId)
	{
		TRecord value;
		lock (cached)
		{
			if (cached.TryGetValue(recordId, out value))
			{
				return value;
			}
		}
		value = await Records.RecordBatch<TRecord>().Request(recordId).ConfigureAwait(continueOnCapturedContext: false);
		lock (cached)
		{
			CacheIntern(recordId, value);
		}
		return value;
	}

	public void Cache(TRecord record)
	{
		if (record == null)
		{
			return;
		}
		lock (cached)
		{
			CacheIntern(GetKey(record), record);
		}
	}

	public void Cache(IEnumerable<TRecord> records)
	{
		lock (cached)
		{
			foreach (TRecord record in records)
			{
				CacheIntern(GetKey(record), record);
			}
		}
	}

	private RecordId GetKey(IRecord record)
	{
		return new RecordId(record.OwnerId, record.RecordId);
	}

	private void CacheIntern(RecordId key, TRecord record)
	{
		if (cached.TryGetValue(key, out var value))
		{
			if (record.CanOverwrite(value))
			{
				cached[key] = value;
			}
		}
		else
		{
			cached.Add(key, record);
		}
	}
}
public class RecordSearch<R> where R : class, IRecord, new()
{
	public const int DEFAULT_BATCH_SIZE = 100;

	public int BatchSize = 100;

	private SearchParameters searchParameters;

	private RecordsManager recordsManager;

	private bool cache;

	public readonly List<R> Records;

	public bool HasMoreResults { get; private set; }

	public bool EqualsParameters(SearchParameters other)
	{
		return searchParameters.Equals(other, excludeOffsetAndCount: true);
	}

	public RecordSearch(SearchParameters searchParameters, SkyFrostInterface cloud, bool cache = true)
	{
		this.searchParameters = searchParameters;
		recordsManager = cloud.Records;
		this.cache = cache;
		Records = new List<R>();
		HasMoreResults = true;
	}

	public async ValueTask GetResultsSlice(int offset, int count, List<R> results, int attempts = 5, bool throwOnError = true)
	{
		int endIndex = offset + count;
		await EnsureResults(endIndex, attempts, throwOnError).ConfigureAwait(continueOnCapturedContext: false);
		endIndex = MathX.Min(endIndex, Records.Count);
		for (int i = offset; i < endIndex; i++)
		{
			results.Add(Records[i]);
		}
	}

	public async ValueTask<bool> EnsureResults(int count, int attempts = 5, bool throwOnError = true, int delayMilliseconds = 250, TimeSpan? timeout = null)
	{
		bool fetchedNew = false;
		int attemptsRemaining = attempts;
		Stopwatch stopwatch = Stopwatch.StartNew();
		while (HasMoreResults && Records.Count < count)
		{
			searchParameters.Offset = Records.Count;
			searchParameters.Count = BatchSize;
			CloudResult<SearchResults<R>> cloudResult = await recordsManager.FindRecords<R>(searchParameters, timeout, throwOnError: false).ConfigureAwait(continueOnCapturedContext: false);
			if (cloudResult.IsOK)
			{
				attemptsRemaining = attempts;
				if (cloudResult.Entity.Records.Count > 0)
				{
					fetchedNew = true;
				}
				if (cache)
				{
					recordsManager.RecordCache<R>().Cache(cloudResult.Entity.Records);
				}
				Records.AddRange(cloudResult.Entity.Records);
				HasMoreResults = cloudResult.Entity.HasMoreResults;
				continue;
			}
			if (attemptsRemaining-- == 0)
			{
				if (throwOnError)
				{
					throw new Exception($"Unable to fetch search results. Current count: {Records.Count}. Desired count: {count}. Attempts: {attempts}. Elapsed: {stopwatch.Elapsed}. LastResult: {cloudResult}");
				}
				HasMoreResults = false;
			}
			int num = attempts - attemptsRemaining;
			await Task.Delay(TimeSpan.FromMilliseconds(delayMilliseconds * num)).ConfigureAwait(continueOnCapturedContext: false);
		}
		return fetchedNew;
	}
}
public struct AssetData : IDisposable
{
	public readonly Uri url;

	public readonly Stream stream;

	public AssetData(Uri url)
	{
		this.url = url;
		stream = null;
	}

	public AssetData(Stream stream)
	{
		this.stream = stream;
		url = null;
	}

	public void Dispose()
	{
		stream?.Dispose();
	}

	public static implicit operator AssetData(Stream stream)
	{
		return new AssetData(stream);
	}

	public static implicit operator AssetData(Uri url)
	{
		return new AssetData(url);
	}
}
public abstract class RecordUploadTaskBase<R> where R : class, IRecord, new()
{
	public enum FailureScope
	{
		Record,
		Owner,
		Global
	}

	public class AssetUploadData
	{
		public Uri appDBURL;

		public long bytes;

		public string appDBSig;

		public AssetInfo cloudInfo;

		public string localFile;
	}

	protected static bool LOG_PROGRESS;

	private string _stageDescription;

	private float preprocessProgress;

	private TaskCompletionSource<bool> _completionSource = new TaskCompletionSource<bool>();

	public SkyFrostInterface Cloud { get; private set; }

	public bool IsFinished { get; private set; }

	public bool Failed { get; private set; }

	public string FailReason { get; private set; }

	public bool WasAlreadySynced { get; private set; }

	public float Progress
	{
		get
		{
			float num = preprocessProgress * 0.1f;
			if (BytesToUpload > 0)
			{
				num += (float)BytesUploaded / (float)BytesToUpload * 0.9f;
			}
			return num;
		}
	}

	public string StageDescription
	{
		get
		{
			return _stageDescription;
		}
		set
		{
			if (LOG_PROGRESS)
			{
				UniLog.Log($"RecordSync {Record?.CombinedRecordId} Stage: " + value);
			}
			_stageDescription = value;
		}
	}

	public long BytesToUpload { get; private set; }

	public long BytesUploaded { get; private set; }

	public long AssetsToUpload { get; private set; }

	public long AssetsUploaded { get; private set; }

	public Task Task => _completionSource.Task;

	public R Record { get; private set; }

	public bool EnsureFolder { get; private set; }

	public List<AssetDiff> AssetDiffs { get; private set; }

	public bool ForceConflictSync { get; set; }

	public event Action<AssetDiff> AssetToUploadAdded;

	public event Action<long> BytesUploadedAdded;

	public event Action AssetUploaded;

	public event Action<string> AssetMissing;

	protected abstract Task<bool> PrepareFilesForUpload(CancellationToken cancellationToken);

	protected abstract Task StoreSyncedRecord(R record);

	protected abstract ValueTask<AssetData> ReadFile(string signature);

	protected virtual ValueTask UploadStarted(string signature)
	{
		return default(ValueTask);
	}

	public RecordUploadTaskBase(SkyFrostInterface cloud, R record, bool ensureFolder = true)
	{
		Cloud = cloud;
		Record = record.Clone<R>();
		EnsureFolder = ensureFolder;
	}

	protected void Fail(string error)
	{
		UniLog.Error($"Failed sync for {Record.OwnerId}:{Record.RecordId}. Local: {Record.Version.LocalVersion}, Global: {Record.Version.GlobalVersion}:\n" + error);
		Failed = true;
		FailReason = error;
		IsFinished = true;
	}

	protected void FailConflict()
	{
		Fail("Conflict");
	}

	private void RemoveManifestDuplicates()
	{
		if (Record.AssetManifest == null)
		{
			return;
		}
		HashSet<string> hashSet = Pool.BorrowHashSet<string>();
		for (int num = Record.AssetManifest.Count - 1; num >= 0; num--)
		{
			if (!hashSet.Add(Record.AssetManifest[num].Hash))
			{
				Record.AssetManifest.RemoveAt(num);
			}
		}
		Pool.Return(ref hashSet);
	}

	private async Task EnsureManifestAssetSizes()
	{
		if (Record.AssetManifest == null)
		{
			return;
		}
		foreach (DBAsset dbAsset in Record.AssetManifest)
		{
			if (dbAsset.Bytes <= 0)
			{
				CloudResult<AssetInfo> cloudResult = await Cloud.Assets.GetGlobalAssetInfo(dbAsset.Hash).ConfigureAwait(continueOnCapturedContext: false);
				if (cloudResult.IsOK)
				{
					dbAsset.Bytes = cloudResult.Entity.Bytes;
					continue;
				}
				UniLog.Warning($"Failed getting asset info for asset with 0 byte size: {dbAsset.Hash}\n{cloudResult}");
			}
		}
	}

	public Task RunUpload(CancellationToken cancellationToken)
	{
		try
		{
			return Task.Run(async delegate
			{
				try
				{
					await RunUploadInternal(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					_completionSource.SetResult(IsFinished);
				}
				catch (Exception ex2)
				{
					UniLog.Error("Exception during record upload task:\n" + ex2);
					Fail("Exception during sync.");
					_completionSource.SetException(ex2);
				}
			});
		}
		catch (Exception ex)
		{
			UniLog.Error("Exception during record upload task:\n" + ex);
			Fail("Exception during sync.");
			_completionSource.SetResult(result: false);
			return Task.CompletedTask;
		}
	}

	private async Task<bool> CheckCloudVersion(CancellationToken cancelationToken)
	{
		if (cancelationToken.IsCancellationRequested)
		{
			Fail("Record upload task cancelled");
			return false;
		}
		UniLog.Log($"Starting sync for {Record.OwnerId}:{Record.RecordId}. Name: {Record.Name}, Path: {Record.Path}, Local: {Record.Version.LocalVersion}, Global: {Record.Version.GlobalVersion}");
		StageDescription = "Starting record upload task";
		CloudResult<R> cloudResult = await Cloud.Records.GetRecord<R>(Record.OwnerId, Record.RecordId, null, includeDeleted: true).ConfigureAwait(continueOnCapturedContext: false);
		if (cancelationToken.IsCancellationRequested)
		{
			Fail("Record upload task cancelled");
			return false;
		}
		if (Record.RecordId == "R-SettingsData")
		{
			ForceConflictSync = true;
		}
		if (cloudResult.IsOK)
		{
			R entity = cloudResult.Entity;
			if (Record.IsSameVersion(entity))
			{
				if (LOG_PROGRESS)
				{
					UniLog.Log($"RecordSync {Record?.CombinedRecordId}, Same version, skipping upload");
				}
				await StoreSyncedRecord(entity);
				IsFinished = true;
				WasAlreadySynced = true;
				return false;
			}
			if (!Record.CanOverwrite(entity))
			{
				UniLog.Log("Conflict! Cloud Record: " + JsonConvert.SerializeObject(entity) + "\n\nLocal record: " + JsonConvert.SerializeObject(Record));
				if (entity.IsDeleted && Record.Version.GlobalVersion == 0)
				{
					UniLog.Log("Cloud Record has been deleted and local version is completely new - considering this as replacement.");
					ForceConflictSync = true;
				}
				if (!ForceConflictSync)
				{
					FailConflict();
					return false;
				}
				UniLog.Log("Forcing synchronization");
				try
				{
					Record.OverrideGlobalVersion(entity.Version.GlobalVersion);
					if (Record.Version.LastModifyingUserId == entity.Version.LastModifyingUserId && Record.Version.LastModifyingMachineId == entity.Version.LastModifyingMachineId)
					{
						RecordVersion version = Record.Version;
						version.LocalVersion = entity.Version.LocalVersion + 1;
						Record.Version = version;
					}
				}
				catch (Exception ex)
				{
					Fail(ex.Message);
					return false;
				}
			}
		}
		return true;
	}

	protected abstract Task<bool> PrepareRecord(CancellationToken cancelationToken);

	private async Task<bool> PreprocessRecord(CancellationToken cancelationToken)
	{
		string lastFailReason = null;
		for (int attempt = 0; attempt < 10; attempt++)
		{
			StageDescription = $"Preprocessing record, Assets: {Record.AssetManifest?.Count ?? 0}. Attempt: {attempt}";
			preprocessProgress = 0f;
			UniLog.Log("Preprocessing record: " + Record.OwnerId + ":" + Record.RecordId);
			CloudResult<RecordPreprocessStatus> preprocessStatus = await Cloud.Records.PreprocessRecord(Record).ConfigureAwait(continueOnCapturedContext: false);
			if (cancelationToken.IsCancellationRequested)
			{
				Fail("Record upload task cancelled");
				return false;
			}
			if (preprocessStatus.Entity == null)
			{
				Fail(preprocessStatus.State.ToString() + " - " + preprocessStatus.Content);
				return false;
			}
			while (preprocessStatus.IsOK && preprocessStatus.Entity.State == RecordPreprocessState.Preprocessing)
			{
				preprocessProgress = preprocessStatus.Entity.Progress;
				StageDescription = $"Waiting for preprocess to finish: {preprocessProgress * 100f:F2}. Attempt: {attempt}";
				await Task.Delay(1000, cancelationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (cancelationToken.IsCancellationRequested)
				{
					Fail("Record upload task cancelled");
					return false;
				}
				preprocessStatus = await Cloud.Records.GetPreprocessStatus(preprocessStatus.Entity).ConfigureAwait(continueOnCapturedContext: false);
			}
			preprocessProgress = 1f;
			StageDescription = $"Handling preprocess result: {preprocessStatus}. Attempt: {attempt}";
			if (preprocessStatus.IsError || preprocessStatus.Entity == null)
			{
				Fail("Record preprocessing failed: " + preprocessStatus.State.ToString() + " - " + preprocessStatus.Content);
				return false;
			}
			if (preprocessStatus.Entity.State != RecordPreprocessState.Failed)
			{
				AssetDiffs = preprocessStatus.Entity.ResultDiffs;
				return true;
			}
			lastFailReason = (StageDescription = "Record preprocessing failed. PreprocessId: " + preprocessStatus.Entity.PreprocessId + ", RecordId:" + preprocessStatus.Entity.RecordId + ", Fail Reason: " + preprocessStatus.Entity.FailReason);
			if (preprocessStatus.Entity.FailReason == null || !preprocessStatus.Entity.FailReason.Contains("TooManyRequests"))
			{
				Fail(lastFailReason);
				return false;
			}
			await Task.Delay(TimeSpan.FromSeconds((float)attempt * 2f)).ConfigureAwait(continueOnCapturedContext: false);
		}
		Fail("Record preprocessing failed after all attempts: " + lastFailReason);
		return false;
	}

	private async Task<bool> UploadAssets(CancellationToken cancelationToken)
	{
		StageDescription = "Uploading assets";
		if (!(await PrepareFilesForUpload(cancelationToken).ConfigureAwait(continueOnCapturedContext: false)))
		{
			return false;
		}
		StageDescription = "Collecting upload information";
		foreach (AssetDiff assetDiff in AssetDiffs)
		{
			if (assetDiff.IsUploaded == false)
			{
				AssetsToUpload++;
				BytesToUpload += assetDiff.Bytes;
				this.AssetToUploadAdded?.Invoke(assetDiff);
			}
		}
		StageDescription = "Scheduling uploads";
		List<AssetUploadTask> assetUploads = new List<AssetUploadTask>();
		foreach (AssetDiff diff in AssetDiffs)
		{
			if (diff.IsUploaded != false)
			{
				continue;
			}
			long uploadedBefore = BytesUploaded;
			long uploadedBytes = 0L;
			CallbackProgressIndicator progressIndicator = new CallbackProgressIndicator(delegate(float percent, LocaleString info, LocaleString detailInfo)
			{
				long num = (long)((float)diff.Bytes * percent);
				long obj = num - uploadedBytes;
				BytesUploaded = uploadedBefore + num;
				this.BytesUploadedAdded?.Invoke(obj);
			}, null, null);
			string _hash = diff.Hash.ToLowerInvariant();
			AssetData fileData = await ReadFile(_hash).ConfigureAwait(continueOnCapturedContext: false);
			try
			{
				if (LOG_PROGRESS)
				{
					UniLog.Log($"RecordSync {Record?.CombinedRecordId}, Uploading {diff.Hash} - {UnitFormatting.FormatBytes(diff.Bytes)}");
				}
				AssetUploadTask uploadTask = ((fileData.stream == null) ? Cloud.Assets.CreateURLAssetUploadTask(Record.OwnerId, diff.Hash, null, fileData.url, progressIndicator, diff.Bytes) : Cloud.Assets.CreateStreamAssetUploadTask(Record.OwnerId, diff.Hash, null, fileData.stream, progressIndicator, diff.Bytes));
				CloudResult<SkyFrost.Base.AssetUploadData> uploadResult = await uploadTask.UploadAssetData().ConfigureAwait(continueOnCapturedContext: false);
				await UploadStarted(_hash).ConfigureAwait(continueOnCapturedContext: false);
				if (cancelationToken.IsCancellationRequested)
				{
					Fail("Record upload task cancelled");
					return false;
				}
				if (uploadResult.IsError)
				{
					if (uploadResult.State == HttpStatusCode.NotFound)
					{
						UniLog.Log($"Asset missing: {uploadTask.Signature} - {uploadTask.UploadData?.Entity} - {uploadResult.Content}");
						this.AssetMissing?.Invoke(uploadTask.Signature);
					}
					else
					{
						if (uploadResult.Content != "AlreadyUploaded")
						{
							Fail("Couldn't upload asset " + diff.Hash + ": " + uploadResult.State.ToString() + " - " + uploadResult.Content);
							return false;
						}
						this.AssetUploaded?.Invoke();
					}
				}
				else
				{
					this.AssetUploaded?.Invoke();
					assetUploads.Add(uploadTask);
				}
			}
			finally
			{
				((IDisposable)fileData/*cast due to .constrained prefix*/).Dispose();
			}
			BytesUploaded = uploadedBefore + diff.Bytes;
		}
		foreach (AssetUploadTask uploadTask in assetUploads)
		{
			CloudResult<SkyFrost.Base.AssetUploadData> cloudResult = await uploadTask.WaitForAssetFinishProcessing().ConfigureAwait(continueOnCapturedContext: false);
			if (cloudResult.IsError)
			{
				if (cloudResult.State != HttpStatusCode.NotFound)
				{
					if (cloudResult.Content.StartsWith("MissingAllRequiredChunks") && uploadTask.UploadData?.Entity != null)
					{
						Fail("MissingAllRequiredChunks\n" + System.Text.Json.JsonSerializer.Serialize(uploadTask.UploadData.Entity));
						return false;
					}
					Fail($"Couldn't upload asset {uploadTask.Signature} - {uploadTask.UploadData?.Entity}: {cloudResult.State} - {cloudResult.Content}");
					return false;
				}
				UniLog.Log($"Asset missing: {uploadTask.Signature} - {uploadTask.UploadData?.Entity} - {cloudResult.Content}");
				this.AssetMissing?.Invoke(uploadTask.Signature);
			}
			else
			{
				AssetsUploaded++;
			}
		}
		return true;
	}

	private async Task<bool> UpsertRecord(CancellationToken cancelationToken)
	{
		StageDescription = "Upserting Record";
		CloudResult<CloudMessage> cloudResult = await Cloud.Records.UpsertRecord(Record, EnsureFolder).ConfigureAwait(continueOnCapturedContext: false);
		if (cancelationToken.IsCancellationRequested)
		{
			Fail("Record upload task cancelled");
			return false;
		}
		StageDescription = "Finishing";
		if (!cloudResult.IsOK)
		{
			Fail("UpsertResult State: " + cloudResult.State.ToString() + "\nContent:\n" + cloudResult.Content);
			return false;
		}
		await StoreSyncedRecord(Record).ConfigureAwait(continueOnCapturedContext: false);
		IsFinished = true;
		StageDescription = "Finished Record Upload";
		UniLog.Log($"Finished sync for {Record.CombinedRecordId}. Local: {Record.Version.LocalVersion}, Global: {Record.Version.GlobalVersion}");
		return true;
	}

	private async Task RunUploadInternal(CancellationToken cancelationToken)
	{
		if (await CheckCloudVersion(cancelationToken).ConfigureAwait(continueOnCapturedContext: false) && await PrepareRecord(cancelationToken).ConfigureAwait(continueOnCapturedContext: false))
		{
			RemoveManifestDuplicates();
			if (await PreprocessRecord(cancelationToken).ConfigureAwait(continueOnCapturedContext: false) && await UploadAssets(cancelationToken).ConfigureAwait(continueOnCapturedContext: false))
			{
				await EnsureManifestAssetSizes().ConfigureAwait(continueOnCapturedContext: false);
				await UpsertRecord(cancelationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
	}
}
public static class SearchQueryParser
{
	public static void Parse(string search, List<string> optionalTags, List<string> requiredTags, List<string> excludedTags)
	{
		if (string.IsNullOrWhiteSpace(search))
		{
			return;
		}
		bool flag = false;
		StringBuilder stringBuilder = Pool.BorrowStringBuilder();
		for (int i = 0; i <= search.Length; i++)
		{
			bool num = i == search.Length;
			char c = (num ? ' ' : search[i]);
			if (num || (char.IsWhiteSpace(c) && !flag) || (c == '"' && flag))
			{
				if (stringBuilder.Length > 0)
				{
					if (stringBuilder[0] == '+')
					{
						stringBuilder.Remove(0, 1);
						if (stringBuilder.Length > 0)
						{
							requiredTags.Add(stringBuilder.ToString());
						}
					}
					else if (stringBuilder[0] == '-')
					{
						stringBuilder.Remove(0, 1);
						if (stringBuilder.Length > 0)
						{
							excludedTags.Add(stringBuilder.ToString());
						}
					}
					else
					{
						optionalTags.Add(stringBuilder.ToString());
					}
				}
				stringBuilder.Clear();
				flag = false;
			}
			else if (c == '"')
			{
				flag = true;
			}
			else
			{
				stringBuilder.Append(c);
			}
		}
		Pool.Return(ref stringBuilder);
	}
}
public readonly struct CloudVariableInfo
{
	public readonly string subpath;

	public readonly string type;

	public readonly string defaultValue;

	public CloudVariableInfo(string path, string type, string defaultValue)
	{
		subpath = path;
		this.type = type;
		this.defaultValue = defaultValue;
	}
}
public static class SettingsCloudVariables
{
	public static CloudVariableInfo LAST_MUTE_STATUS => new CloudVariableInfo("Profile.LastMuteStatus", "bool", false.ToString());

	public static CloudVariableInfo LAST_ONLINE_STATUS => new CloudVariableInfo("Profile.LastOnlineStatus", "string", OnlineStatus.Online.ToString());

	public static float3 DEFAULT_DASH_OFFSET => float3.Down * 0.25f + float3.Forward * 0.5f;

	public static CloudVariableInfo DASH_OFFSET => new CloudVariableInfo("Userspace.RadiantDash.Offset", "float3", DEFAULT_DASH_OFFSET.ToString());

	public static CloudVariableInfo DASH_SCALE => new CloudVariableInfo("Userspace.RadiantDash.Scale", "float3", float3.One.ToString());

	public static CloudVariableInfo DASH_FREEFORM => new CloudVariableInfo("Userspace.RadiantDash.Freeform", "bool", "false");

	public static IEnumerable<CloudVariableInfo> AllSettings
	{
		get
		{
			yield return LAST_MUTE_STATUS;
			yield return LAST_ONLINE_STATUS;
			yield return DASH_OFFSET;
			yield return DASH_SCALE;
			yield return DASH_FREEFORM;
		}
	}
}
public static class TemporaryUtility
{
	public static string FilterViveportUsername(string username)
	{
		int num = username.IndexOf("(movieguest");
		if (num < 0)
		{
			return username;
		}
		string text = username.Substring(num + "(movieguest".Length);
		text = text.Substring(0, text.Length - 1);
		if (int.TryParse(text, out var result))
		{
			return "movieguest" + result.ToString("D3");
		}
		return username;
	}
}
public static class UID
{
	public static string Compute()
	{
		HardwareInfo hardwareInfo = new HardwareInfo(TimeSpan.FromSeconds(5L));
		hardwareInfo.RefreshCPUList(includePercentProcessorTime: false);
		hardwareInfo.RefreshMotherboardList();
		hardwareInfo.RefreshBIOSList();
		hardwareInfo.RefreshMemoryList();
		StringBuilder stringBuilder = new StringBuilder();
		foreach (Motherboard motherboard in hardwareInfo.MotherboardList)
		{
			stringBuilder.Append(motherboard.SerialNumber);
			stringBuilder.Append(motherboard.Manufacturer);
			stringBuilder.Append(motherboard.Product);
		}
		foreach (CPU cpu in hardwareInfo.CpuList)
		{
			stringBuilder.Append(cpu.Name);
			stringBuilder.Append(cpu.ProcessorId);
			stringBuilder.Append(cpu.Manufacturer);
		}
		foreach (BIOS bios in hardwareInfo.BiosList)
		{
			stringBuilder.Append(bios.SerialNumber);
		}
		foreach (Memory memory in hardwareInfo.MemoryList)
		{
			stringBuilder.Append(memory.Manufacturer);
			stringBuilder.Append(memory.PartNumber);
			stringBuilder.Append(memory.SerialNumber);
		}
		return CryptoHelper.HashIDToToken(stringBuilder.ToString());
	}
}
public class VariableReadBatchQuery : BatchQuery<VariableReadRequest, VariableReadResult<CloudVariable, CloudVariableDefinition>>
{
	private CloudVariableManager variables;

	public VariableReadBatchQuery(CloudVariableManager variables)
		: base(32, 0.25f)
	{
		if (variables == null)
		{
			throw new ArgumentNullException("variables");
		}
		this.variables = variables;
		base.DelaySeconds = 1f;
	}

	protected override async Task RunBatch(List<QueryResult> batch)
	{
		List<VariableReadRequest> requests = Pool.BorrowList<VariableReadRequest>();
		foreach (QueryResult item in batch)
		{
			requests.Add(item.query);
		}
		CloudResult<List<VariableReadResult<CloudVariable, CloudVariableDefinition>>> cloudResult = await variables.ReadBatch(requests).ConfigureAwait(continueOnCapturedContext: false);
		Pool.Return(ref requests);
		if (!cloudResult.IsOK)
		{
			return;
		}
		foreach (VariableReadResult<CloudVariable, CloudVariableDefinition> result in cloudResult.Entity)
		{
			QueryResult queryResult = batch.FirstOrDefault((QueryResult q) => q.query.OwnerId == result.Variable.VariableOwnerId && q.query.Path == result.Variable.Path);
			if (queryResult != null)
			{
				queryResult.result = result;
			}
		}
	}
}
public class VariableWriteBatchQuery : BatchQuery<CloudVariable, CloudVariable>
{
	private CloudVariableManager variables;

	public VariableWriteBatchQuery(CloudVariableManager variables)
		: base(32, 0.25f)
	{
		if (variables == null)
		{
			throw new ArgumentNullException("variables");
		}
		this.variables = variables;
		base.DelaySeconds = 5f;
	}

	protected override async Task RunBatch(List<QueryResult> batch)
	{
		List<CloudVariable> requests = Pool.BorrowList<CloudVariable>();
		foreach (QueryResult item in batch)
		{
			requests.Add(item.query);
		}
		CloudResult<List<CloudVariable>> cloudResult = await variables.WriteBatch(requests).ConfigureAwait(continueOnCapturedContext: false);
		Pool.Return(ref requests);
		if (!cloudResult.IsOK)
		{
			return;
		}
		foreach (CloudVariable result in cloudResult.Entity)
		{
			QueryResult queryResult = batch.FirstOrDefault((QueryResult q) => q.query.VariableOwnerId == result.VariableOwnerId && q.query.Path == result.Path);
			if (queryResult != null)
			{
				queryResult.result = result;
			}
		}
	}
}
public readonly struct RegistryProxyConfiguration
{
	public readonly string ProxyOverride;

	public readonly Uri ProxyServer;

	public RegistryProxyConfiguration(Uri proxyServer, string? proxyOverride)
	{
		ProxyOverride = proxyOverride ?? string.Empty;
		ProxyServer = proxyServer;
	}

	public static RegistryProxyConfiguration From(string? proxyServer = "", string? proxyOverride = "")
	{
		if (proxyServer == null)
		{
			throw new ArgumentNullException("proxyServer");
		}
		if (Uri.TryCreate(WebProxyUtility.MakeAbsolouteProxyAddress(proxyServer), UriKind.Absolute, out Uri result))
		{
			return new RegistryProxyConfiguration(result, proxyOverride);
		}
		throw new FormatException("Invalid proxy server address");
	}
}
public class WebProxyUtility
{
	public const string SKYFROST_NO_PROXY = "SKYFROST_NO_PROXY";

	private static ICredentials SetupCredentials(ProxyConfig config)
	{
		if (config.CredentialStore != ProxyConfig.CredentialType.UsernamePassword && !string.IsNullOrEmpty(config.Username) && !string.IsNullOrEmpty(config.Password))
		{
			UniLog.Warning("A username and password was provided but credential store is not set to \"UsernamePassword\", the provided Username and Password will be ignored. If this is intentional, please omit the username & password values or set Credential store to \"UsernamePassword\"");
		}
		return config.CredentialStore switch
		{
			ProxyConfig.CredentialType.NetworkCache => CredentialCache.DefaultNetworkCredentials, 
			ProxyConfig.CredentialType.DefaultSystemCache => CredentialCache.DefaultCredentials, 
			ProxyConfig.CredentialType.Kerberos => new CredentialCache { 
			{
				config.Address,
				"Kerberos",
				CredentialCache.DefaultNetworkCredentials
			} }, 
			ProxyConfig.CredentialType.UsernamePassword => new NetworkCredential(config.Username, config.Password), 
			_ => throw new Exception($"Unsupported credential type for Proxy initialization: {config.CredentialStore}"), 
		};
	}

	/// <summary>
	/// Given a ProxyConfig, usually sourced from <see cref="T:SkyFrost.Base.AppConfig" /> creates an instance of <see cref="T:System.Net.WebProxy" />.
	/// </summary>
	/// <param name="config">ProxyConfig from <see cref="T:SkyFrost.Base.AppConfig" /></param>
	/// <returns>A setup WebProxy, or null if the proxy is invalid.</returns>
	public static WebProxy? CreateProxy(ProxyConfig config, IPlatformProfile profile = null)
	{
		if (config == null)
		{
			return null;
		}
		if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SKYFROST_NO_PROXY")))
		{
			UniLog.Warning("Disabling proxy due to SKYFROST_NO_PROXY set in ENV, delete/set to false to re-enable proxies.");
			return null;
		}
		config = HydrateProxyConfiguration(config);
		if (config == null || !config.IsValid)
		{
			UniLog.Log("Invalid proxy configuration, aborting Setup");
			return null;
		}
		if (!config.Address.IsAbsoluteUri || !IsValidProxyScheme(config.Address.Scheme))
		{
			config.Address = MakeAbsolouteProxyAddress(config.Address);
		}
		WebProxy webProxy = new WebProxy(config.Address);
		if (config.BypassDomains != null && config.BypassDomains.Count > 0)
		{
			webProxy.BypassList = config.BypassDomains.Where((string domain) => !string.IsNullOrEmpty(domain)).ToArray();
		}
		webProxy.Credentials = SetupCredentials(config);
		webProxy.BypassProxyOnLocal = config.LocalBypass;
		UniLog.Log($"Final Proxy url: {config.Address}");
		return webProxy;
	}

	private static bool IsValidProxyScheme(string scheme)
	{
		if (!(scheme == "http"))
		{
			return scheme == "https";
		}
		return true;
	}

	public static Uri MakeAbsolouteProxyAddress(Uri uri)
	{
		return new Uri(MakeAbsolouteProxyAddress(uri.ToString()), UriKind.Absolute);
	}

	public static string MakeAbsolouteProxyAddress(string str)
	{
		if (!str.StartsWith("http") && !str.StartsWith("https"))
		{
			str = "http://" + str;
		}
		return str;
	}

	/// <summary>
	/// Augments, an initial config with additional properties that might be avilable from other sources
	/// </summary>
	/// <param name="initialConfig">Initial config which should be hydrated with additional data from other sources</param>
	/// <returns>Hydrated config</returns>
	private static ProxyConfig HydrateProxyConfiguration(ProxyConfig initialConfig)
	{
		if (initialConfig.ShouldUseAutoDetect)
		{
			initialConfig = GenerateProxySettingsFromDefaultWebProxy(initialConfig);
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				ProxyConfig proxyConfig = GenerateProxyConfigFromRegistry(GetProxyConfigFromRegistry());
				if (proxyConfig != null)
				{
					if (proxyConfig.Address != null)
					{
						initialConfig.Address = proxyConfig.Address;
					}
					if (proxyConfig.BypassDomains != null)
					{
						initialConfig.BypassDomains = proxyConfig.BypassDomains;
					}
				}
			}
		}
		return initialConfig;
	}

	private static ProxyConfig GenerateProxySettingsFromDefaultWebProxy(ProxyConfig initialConfig)
	{
		IWebProxy defaultWebProxy = WebRequest.DefaultWebProxy;
		if (defaultWebProxy != null && defaultWebProxy is WebProxy webProxy)
		{
			UniLog.Log("Retrieving Proxy info from .NET Default Proxy");
			if (webProxy.Address != null)
			{
				initialConfig.Address = webProxy.Address;
				UniLog.Log($"Overwriting Proxy address with .NET Defaults. {initialConfig.Address}");
			}
			initialConfig.BypassDomains = new List<string>(webProxy.BypassList.Where((string domain) => !string.IsNullOrEmpty(domain)));
			return initialConfig;
		}
		return initialConfig;
	}

	private static RegistryProxyConfiguration? GetProxyConfigFromRegistry()
	{
		RegistryProxyConfiguration value = default(RegistryProxyConfiguration);
		try
		{
			using RegistryKey registryKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default).OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Internet Settings\\");
			string text = registryKey?.GetValue("ProxyServer")?.ToString();
			string proxyOverride = registryKey?.GetValue("ProxyOverride")?.ToString();
			if (string.IsNullOrEmpty(text))
			{
				return null;
			}
			value = RegistryProxyConfiguration.From(text, proxyOverride);
		}
		catch (Exception ex)
		{
			UniLog.Log("Error getting Proxy configuration from Registry: " + ex.Message);
			return null;
		}
		return value;
	}

	/// <summary>
	/// Given some registry values in the form of a struct, parses them into valid proxy definitions.
	/// </summary>
	/// <param name="registryValues">Registry values, found in the windows registry.</param>
	/// <returns>A proxy Configuration, sourced from the registry</returns>
	public static ProxyConfig? GenerateProxyConfigFromRegistry(RegistryProxyConfiguration? registryValues)
	{
		ProxyConfig proxyConfig = new ProxyConfig();
		if (!registryValues.HasValue)
		{
			return null;
		}
		if (registryValues?.ProxyServer != null)
		{
			proxyConfig.Address = registryValues?.ProxyServer;
		}
		if (!string.IsNullOrEmpty(registryValues?.ProxyOverride))
		{
			string[] array = registryValues?.ProxyOverride?.Split(';');
			if (array != null)
			{
				string[] array2 = array;
				for (int i = 0; i < array2.Length; i++)
				{
					string domainFromProxyOverride = GetDomainFromProxyOverride(array2[i]);
					if (!string.IsNullOrEmpty(domainFromProxyOverride) && Uri.TryCreate(domainFromProxyOverride, UriKind.RelativeOrAbsolute, out Uri _))
					{
						proxyConfig.BypassDomains.Add(domainFromProxyOverride);
					}
				}
			}
		}
		return proxyConfig;
	}

	private static string? GetDomainFromProxyOverride(string domain)
	{
		if (domain.Contains('<') && domain.Contains('>'))
		{
			return null;
		}
		string[] array = domain.Split('*');
		string text = "";
		for (int num = array.Length - 1; num > -1; num--)
		{
			if (array[num].Length > 0)
			{
				text = array[num];
				break;
			}
		}
		text = text.TrimStart('.');
		return text.Trim();
	}

	public static async Task<bool> TestProxy(WebProxy proxy, IPlatformProfile? profile = null)
	{
		Uri requestUri = ((profile != null) ? new Uri("http://" + profile?.Domain) : new Uri("http://google.com"));
		HttpResponseMessage httpResponseMessage = await new HttpClient(new HttpClientHandler
		{
			Proxy = proxy
		}).GetAsync(requestUri);
		return httpResponseMessage != null && httpResponseMessage.StatusCode != HttpStatusCode.UseProxy;
	}
}
