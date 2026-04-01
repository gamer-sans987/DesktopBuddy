# FrooxEngine Assets, Cloud & Store Reference

Covers classes from `Elements.Assets`, `SkyFrost.Base`, `FrooxEngine.Store`, `FrooxEngine.Commands`, and `Elements.Data`.

---

## FrooxEngine.Commands

### `CommandServer`
TCP server listening on localhost for JSON-serialized commands. Used for IPC (e.g. opening files/URLs from external processes).
- **Fields:** `PORT = 41245`, `listener` (TcpListener), `callback` (Action<BaseCommand>)
- **Methods:** `CommandServer(Action<BaseCommand> commandCallback, int port)`, `Dispose()`

### `CommandInterface`
Static helper to send commands to a running `CommandServer`.
- **Properties:** `IsCommandServerRunning`
- **Methods:** `SendCommand(BaseCommand obj, int port = 41245)`

### `BaseCommand`
Polymorphic JSON base class for IPC commands. Discriminator property: `"command"`.
- **Properties:** `Version` (int, default 1)
- **Derived types:** `OpenFile` (has `File` Uri), `OpenURL` (has `URL` Uri)

### `CommandConstants`
- `PROTOCOL_VERSION = 1`

---

## Elements.Data

### `DataModelAssemblyAttribute`
Assembly-level attribute marking an assembly's role in the data model.
- **Property:** `AssemblyType` (enum: `Core`, `UserspaceCore`, `Optional`, `Dependency`)

### `DataModelTypeAttribute`
Marks a class/struct/enum/interface as part of the data model.

### `ExternalDataModelTypeAttribute`
Assembly-level attribute referencing an external type as a data model type.
- **Property:** `ExternalType` (Type)

### `TypeReplacement` (abstract)
Base attribute for type migration/replacement during deserialization.
- **Properties:** `NewType`, `ReplaceSource`, `ReplaceTarget`

### `FeatureUpgradeReplacement`
Type replacement conditioned on a feature flag version.
- **Properties:** `FeatureFlag` (string), `Version` (int)
- **Methods:** `NeedsUpgrade(IReadOnlyDictionary<string, int> featureFlags)`

### `FeatureUpgradeFlags`
- Constants: `NET_CORE = "NetCore"`, `NET_CORE_VERSION = 0`

### Legacy/Migration Attributes
- `OldAssemblyAttribute` -- old assembly name for type resolution
- `OldName` / `OldNameHash` -- field rename tracking
- `OldNamespaceAttribute` -- old namespace for type resolution
- `OldTypeNameAttribute` -- old full type name
- `OldTypeHashAttribute` -- old type hash for binary lookups
- `OldTypeSpecialization` -- old generic specialization mapping
- `OldNonGenericDefaultAttribute` -- maps old non-generic type to a generic default

---

## FrooxEngine.Store

### `AssetRecord`
LiteDB record for a locally stored asset.
- **Fields:** `id`, `url` (DB URI string), `path` (file path), `signature` (file hash), `cloudsig` (cloud hash), `bytes`, `encryptionKey`

### `LocalDB`
Core local database backed by LiteDB. Manages assets, records, variables, visits, and metadata on disk.
- **Key Properties:** `Cloud` (SkyFrostInterface), `PermanentPath`, `TemporaryPath`, `AssetCachePath`, `AssetStoragePath`, `DatabaseFile`, `MachineID`, `SecretMachineID`, `LocalOwnerID` ("M-" + MachineID), `IsDisposed`
- **Collections:** `assets`, `records`, `variables`, `visits`, `assetMetadata`
- **Initialization:** `Initialize(IProgressIndicator)` -- loads/generates RSA key, opens LiteDB, rebuilds indexes if needed, handles repair/upgrade
- **Asset Operations:**
  - `ImportLocalAssetAsync(path, ImportLocation, ...)` -- imports file into local DB, returns `local://` URI
  - `StoreCacheRecordAsync(Uri, path, encrypt)` -- stores downloaded cloud asset in cache
  - `TryFetchAssetRecordAsync(Uri)` -- looks up asset by URL
  - `TryFetchAssetBySignatureAsync(string)` / `TryFetchAssetByCloudSignatureAsync(string)`
  - `TryFetchAssetRecordWithMetadataAsync(Uri)` -- fetches with cloud sig + bytes populated
  - `EnsurePermanentStorageAsync(Uri)` -- moves asset from cache to permanent storage
  - `TryOpenAsset(Uri)` -- returns read stream
  - `SaveAssetAsync(...)` -- overloads for Bitmap2D, Bitmap3D, MeshX, AnimX, BitmapCube, AudioX, GaussianCloud
- **Record Operations:**
  - `StoreRecordAsync(Record, overwriteCheck)`, `DeleteRecordAsync(...)`, `FetchRecordsAsync(predicate)`, `TryFetchRecordAsync(ownerId, recordId)`
- **Variable Operations:**
  - `ReadVariableAsync<T>(path)`, `WriteVariableAsync<T>(path, value)`, `DeleteVariableAsync(path)`
  - `RegisterVariableListener(path, callback)` / `UnregisterVariableListener(...)`
- **Visit Tracking:** `LogVisitAsync(url, globalVersion)`, `GetVisitAsync(url)`
- **Metadata:** `TryFetchAssetMetadataAsync(identifier)`, `SaveAssetMetadataAsync(metadata)` -- supports BitmapMetadata, CubemapMetadata, VolumeMetadata, ShaderMetadata, MeshMetadata, GaussianSplatMetadata
- **Crypto:** `SignHash(byte[])`, `GenerateKey()`, `CreateEncryptionStream(...)`, `CreateDecryptionStream(...)`
- **Enum `ImportLocation`:** `Original`, `Copy`, `Move`

### `LocalDatabaseAccountDataStore`
Implements `IAccountDataStore` backed by `LocalDB`. Used for local-only / machine-owner data during account migration.
- **Key Properties:** `Database` (LocalDB), `UserId` ("M-" + MachineID), `MachineOwnerOnly`
- **Methods:** `GetRecords(ownerId, from)`, `GetRecord(ownerId, recordId)`, `DownloadAsset(hash, targetPath)`, `GetAsset(hash)`, `GetGroups()`
- **Note:** Many Store* methods throw `NotSupportedException` -- this is a read-only source for migration.

### `Record` (FrooxEngine.Store)
Local record entity stored in LiteDB. Implements `IRecord`.
- **Key Fields:** `RecordId`, `OwnerId`, `AssetURI`, `Name`, `Description`, `RecordType`, `Path`, `ThumbnailURI`, `Tags`, `Version` (RecordVersion), `LastModificationTime`, `CreationTime`, `IsPublic`, `IsListed`, `IsDeleted`, `Visits`, `Rating`, `AssetManifest` (List<DBAsset>), `Submissions`, `MigrationMetadata`
- **Key Methods:** `GetUrl(IPlatformProfile)`, `Clone<R>()`, `IncrementGlobalVersion()`, `IncrementLocalVersion(machineId, userId)`, `ClearRecordSpecificMetadata()`
- **Legacy Properties:** `LegacyGlobalVersion`, `LegacyLocalVersion`, `LegacyManifest` (neosDBmanifest migration)

### `LocalVariable`
LiteDB entity for persistent local key-value storage.
- **Fields:** `id`, `path`, `value`

### `LocalVariableProxy<T>`
Reactive wrapper around a `LocalVariable`. Auto-reads/writes through `LocalDB`.
- **Properties:** `Value`, `Path`
- **Events:** `OnChanged`

### `LocalVisit`
Tracks URL visit history.
- **Fields:** `id`, `url`, `globalVersion`, `lastVisit`

### `LocalMetadata`
Stores serialized asset metadata JSON in LiteDB.
- **Fields:** `id`, `MetadataId`, `Metadata` (JSON string)

### `LocalDBUtility`
Static helper: `ExportData(persistentPath, tempPath, exportPath)` -- exports local DB to file-based `LocalAccountDataStore`.

---

## SkyFrost.Base -- Cloud Infrastructure

### `SkyFrostInterface`
Central hub for all cloud API access. Owns all manager modules and the HTTP/SignalR clients.
- **Key Properties:** `UID`, `ApiEndpoint`, `SignalREndpoint`, `Platform` (IPlatformProfile), `Api` (ApiClient), `CurrentUser`, `CurrentUserID`
- **Manager Modules (all SkyFrostModule):** `Assets` (AssetInterface), `Session` (SessionManager), `Users` (UsersManager), `Storage` (StorageManager), `Records` (RecordsManager), `Sessions` (SessionsManager), `Variables` (CloudVariableManager), `Status` (UserStatusManager), `Contacts` (ContactManager), `Groups` (GroupsManager), `Messages` (MessageManager), `Migration` (MigrationManager), `Visits` (VisitsManager), `Badges` (BadgeManager), `Moderation` (ModerationManager), `Security` (SecurityManager), `Profile` (ProfileManager), `Stats` (StatisticsManager), `NetworkNodes` (INetworkNodeManager)
- **Other:** `HubClient` (AppHub), `AssetGatherer`, `HubStatusController`, `SafeHttpClient`
- **Constructor:** `SkyFrostInterface(string uid, string secretMachineId, SkyFrostConfig config)`
- **Methods:** `MetadataBatch<M>()`, `OnLogin()`, `OnLogout(bool)`, `OnSessionTokenRefresh()`

### `SkyFrostModule` (abstract)
Base class for all cloud manager modules. Holds reference to parent `SkyFrostInterface`.

### `ApiClient`
HTTP client wrapper for SkyFrost REST API. Handles auth headers, retries, JSON serialization.
- **Methods:** `GET<T>(resource)`, `POST<T>(resource, body)`, `PUT<T>(resource, body)`, `DELETE(resource)`, `CreateRequest(...)`, `RunRequest<T>(...)`

### `CloudResult` / `CloudResult<T>`
Wraps HTTP response from cloud API calls.
- **Properties:** `State` (HttpStatusCode), `IsOK`, `IsError`, `Content`, `Entity` (T), `Headers`, `RequestAttempts`

### `AssetInterface` (abstract)
Manages cloud asset URLs, signatures, and upload task creation.
- **Key Properties:** `Cloud`, `DBScheme`, `CurrentUserID`
- **Methods:**
  - `DBToHttp(Uri, DB_Endpoint)` -- converts DB URI to HTTP URL (abstract)
  - `GenerateURL(signature)` / `GenerateURLWithExtension(signature, extension)`
  - `DBSignature(Uri)` -- extracts hash signature from DB URI
  - `IsValidDBUri(Uri)` / `IsLegacyDB(Uri)`
  - `FilterDatabaseURL(Uri)` -- strips extension from DB URIs
  - `GatherAsset(signature)` -- downloads asset as Stream
  - `GetGlobalAssetInfo(hash)` / `GetAssetInfo(ownerId, hash)`
  - `CreateFileAssetUploadTask(...)` / `CreateStreamAssetUploadTask(...)` / `CreateURLAssetUploadTask(...)`

### `AzureAssetInterface` : AssetInterface
Azure Blob Storage backend.
- **Properties:** `BlobEndpoint`, `ThumbnailEndpoint`, `LegacyBlobEndpoint`, `AssetsEndpoint`

### `CloudflareAssetInterface` : AssetInterface
Cloudflare R2 storage backend.

### `AssetUploadTask` (abstract)
Manages chunked upload of a single asset to the cloud.
- **Properties:** `OwnerId`, `Signature`, `Variant`, `TotalBytes`, `Retries`, `IsOptional`, `UploadData`, `EnqueuedChunks`, `UploadedChunks`
- **Methods:** `RunUpload()`, `UploadAssetData()`, `WaitForAssetFinishProcessing()`
- **Initialization:** `InitializeWithFile(...)`, `InitializeWithStream(...)`, `InitializeWithURL(...)`

### `AssetUploadTask<TChunkResult>` : AssetUploadTask
Generic chunked upload with parallel chunk processing via buffer pool.

### `AzureAssetUploadTask` : AssetUploadTask<CloudMessage>
Azure-specific chunked upload implementation.

### `CloudflareAssetUploadTask` : AssetUploadTask<CloudflareChunkResult>
Cloudflare-specific chunked upload implementation.

### `AssetUtil`
Static utilities for asset hashing and variant identifiers.
- `COMPUTE_VERSION` (current: 23)
- `GenerateHashSignature(file/stream)` -- SHA256 hash
- `ComposeIdentifier(signature, variant)` / `SplitIdentifier(identifier, out signature, out variant)`

### `AssetVariantIdentifier`
Pairs an `AssetSignature` with a `VariantIdentifier`.

### `AssetData` (struct)
Disposable wrapper holding either a `Uri` or a `Stream` for asset content.

### `AssetGatherer` (abstract) / `AssetGatherer<G>`
Manages concurrent asset downloads with priority queuing and speed tracking.
- **Properties:** `Cloud`, `TotalBytesPerSecond`, `BufferSize`, `MaximumAttempts`, `TemporaryPath`
- **Methods:** `Gather(Uri, priority, initialize)`, `SetCategoryParallelism(category, concurrentJobs)`

### `GatherJob`
Represents a single asset download job. Tracks state (`GatherJobState`: Waiting, Initiating, Gathering, Finished, Failed), priority, and target path.

---

## SkyFrost.Base -- Records

### `RecordsManager` : SkyFrostModule
Cloud record CRUD operations via REST API.
- **Methods:**
  - `GetRecord<R>(ownerId, recordId, accessKey)` / `GetRecord<R>(Uri)`
  - `GetRecordCached<R>(Uri)` -- with in-memory cache
  - `GetRecords<R>(ownerId, tag, path)` / `GetRecords<R>(List<RecordId>)`
  - `GetRecordsInHierarchy<R>(ownerId, path)` -- recursive directory traversal
  - `FindRecords<R>(SearchParameters)` -- paged search
  - `UpsertRecord<R>(record, ensureFolder)` -- PUT to cloud
  - `PreprocessRecord<R>(record)` / `GetPreprocessStatus(...)`
  - `DeleteRecord(ownerId, recordId)`
  - `AddTag(ownerId, recordId, tag)`
  - `GetRecordAuditLog(ownerId, from, to)` / `EnumerateRecordAuditLog(ownerId)`
- **Helpers:** `GenerateRecordUri(...)`, `ExtractRecordID(Uri, ...)`, `ExtractRecordPath(Uri, ...)`
- **Batch/Cache:** `RecordBatch<R>()`, `RecordCache<R>()`

### `RecordUploadTaskBase<R>` (abstract)
Orchestrates uploading a record and its assets to the cloud. Handles asset diff computation, chunked upload, preprocess polling, conflict detection.
- **Properties:** `Record`, `Cloud`, `IsFinished`, `Failed`, `FailReason`, `Progress`, `BytesToUpload`, `BytesUploaded`, `AssetsToUpload`, `AssetsUploaded`, `AssetDiffs`, `ForceConflictSync`
- **Methods:** `RunUpload(CancellationToken)`, `PrepareFilesForUpload(...)`, `StoreSyncedRecord(record)`, `ReadFile(signature)`
- **Events:** `AssetToUploadAdded`, `BytesUploadedAdded`, `AssetUploaded`, `AssetMissing`

### `AccountDataStoreUploadTask` : RecordUploadTaskBase<Record>
Upload task that reads assets from an `IAccountDataStore` source (used during migration).

### `RecordSearch<R>`
Paged search over cloud records. Wraps `SearchParameters` and fetches results in batches (default 100).
- **Properties:** `Records` (List<R>), `HasMoreResults`

### `RecordCache<TRecord>`
In-memory cache for records fetched from the cloud, keyed by `RecordId`.
- **Methods:** `Get(ownerId, recordId)`, `Cache(record)`, `Cache(IEnumerable<TRecord>)`

### `RecordBatchQuery<R>` : BatchQuery<RecordId, R>
Batches multiple record fetch requests into a single API call.

---

## SkyFrost.Base -- Account Data & Migration

### `IAccountDataStore` (interface)
Abstraction for reading/writing account data (records, contacts, messages, groups, variables, assets).
- **Key Methods:** `GetRecords(ownerId, from)`, `GetRecord(ownerId, recordId)`, `GetContacts()`, `GetMessages(contactId, from)`, `GetGroups()`, `StoreRecord(record, source, ...)`, `StoreContact(...)`, `StoreMessage(...)`, `DownloadAsset(hash, targetPath)`, `ReadAsset(hash)`, `GetAssetSize(hash)`
- **Properties:** `PlatformProfile`, `UserId`, `Username`, `Name`, `MigrationId`, `FetchedGroupCount`

### `CloudAccountDataStore` : IAccountDataStore
Reads/writes account data from the live cloud API via `SkyFrostInterface`.

### `LocalAccountDataStore` : IAccountDataStore
File-system-based data store. Reads/writes JSON files and downloads assets to a local directory.
- **Fields:** `BasePath`, `AssetsPath`
- **Uses:** `ActionBlock<AssetJob>` for parallel asset downloads

### `AccountTransferController`
Orchestrates migration between two `IAccountDataStore` instances (source -> target).
- **Properties:** `Status` (AccountMigrationStatus), `ProgressMessage`
- **Methods:** `Transfer(CancellationToken)`

### `AccountMigrationConfig`
Configuration for which data to migrate: `MigrateUserRecords`, `MigrateGroups`, `PreserveOldHome`, etc.

### `RecordStoreResult` (enum)
`Stored`, `AlreadyExists`, `Conflict`, `Ignored`, `Error`

### `StoreResultData` (struct)
Pairs a `RecordStoreResult` with an optional error string.

### `RecordStatusCallbacks`
Callback hooks for record sync progress: `AssetToUploadAdded`, `BytesUploaded`, `AssetUploaded`, `AssetMissing`, `MigrationStarted`, `MigrationFinished`.

### `GroupData` / `MemberData` (structs)
Bundles a `Group`/`Member` with its `Storage` info.

---

## SkyFrost.Base -- Storage & Variables

### `StorageManager` : SkyFrostModule
Tracks and updates cloud storage quotas.
- **Properties:** `CurrentStorage`, `CurrentStorageQuota`, `CurrentStorageUsed`, `CurrentStorageFree`
- **Methods:** `GetStorage(ownerId)`, `GetMemberStorage(ownerId, userId)`, `MarkStorageDirty(ownerId)`, `UpdateCurrentUserStorage()`
- **Events:** `StorageUpdated`

### `CloudVariableManager` : SkyFrostModule
Manages cloud variable proxies, batched reads/writes.
- **Methods:** `ReadVariable(ownerId, path)`, `WriteVariable(variable)`, `Update()`, `SetLocalAccessor(ILocalVariableAccessor)`

### `CloudVariableProxy`
Client-side proxy for a single cloud variable. Handles read/write with cloud sync.

### `CloudVariableState` (enum)
`Uninitialized`, `ReadFromTheCloud`, `ChangedLocally`, `WrittenToCloud`, `Invalid`, `Unregistered`

---

## SkyFrost.Base -- Other Managers

### `SessionManager` : SkyFrostModule
Manages login/logout, session tokens, current user identity.

### `UsersManager` : SkyFrostModule
User CRUD: `GetUser(userId)`, `GetUserByName(username)`, `GetUsers(searchName)`, `GetPublicKey(...)`, `RequestAccountDeletion(...)`, `CancelAccountDeletion(...)`

### `ContactManager` : SkyFrostModule
Contact list management.

### `GroupsManager` : SkyFrostModule
Group membership and info.

### `MessageManager` : SkyFrostModule (implements IHubMessagingClient)
Messaging via REST + SignalR hub.

### `SessionsManager` : SkyFrostModule
Manages active world sessions, listing, updates.

### `MigrationManager` : SkyFrostModule
Cloud-side migration status and coordination.

### `SecurityManager` : SkyFrostModule
RSA key management, public key operations.

### `ProfileManager` : SkyFrostModule
User profile operations.

### `StatisticsManager` : SkyFrostModule
Usage statistics.

### `ModerationManager` : SkyFrostModule (implements IModerationClient)
Moderation actions via hub.

### `BadgeManager` : SkyFrostModule
Badge management.

### `VisitsManager` : SkyFrostModule
World visit tracking.

### `AppHub` (implements IHubServer, IHubDebugClient)
SignalR hub client for real-time communication (messaging, status, networking, moderation).

### `HubStatusController` (implements IHubStatusClient)
Tracks SignalR connection status.

### `SkyFrostConfig`
Configuration for `SkyFrostInterface`: `ApiEndpoint`, `SignalREndpoint`, `Platform`, `AssetInterface`, `UniverseID`, `UserAgentProduct`, `UserAgentVersion`, `NodePreference`, `ProxyConfig`, `GZip`, `ForceSignalRLongPolling`.

---

## Elements.Assets (Key Classes)

This is a large library covering asset types, codecs, mesh processing, textures, fonts, and variant generation. Below are the most relevant classes for cloud/store interaction.

### `IAssetMetadata` (interface)
Base interface for all asset metadata. Key property: `AssetIdentifier`.

### Metadata Classes
- `BitmapMetadata` : ImageMetadataBase -- 2D texture metadata (size, format, mips, color/alpha channel data)
- `CubemapMetadata` : ImageMetadataBase -- cubemap texture metadata
- `VolumeMetadata` : ImageMetadataBase -- 3D texture metadata
- `MeshMetadata` : IAssetMetadata -- mesh stats (vertices, triangles, submeshes, bones, blend shapes, bounds)
- `ShaderMetadata` : IAssetMetadata -- shader source files, platforms, compilation info
- `GaussianSplatMetadata` : IAssetMetadata -- gaussian splat cloud metadata

### `ImageMetadataBase` (abstract) : IAssetMetadata
Common image metadata: size, format, mip levels, color/alpha channel analysis.

### Variant Descriptors
Describe how to compute an asset variant (e.g. compressed texture, specific mesh LOD):
- `Texture2DVariantDescriptor`, `Texture3DVariantDescriptor`, `CubemapVariantDescriptor` : TextureVariantDescriptor
- `MeshVariantDescriptor` : IAssetVariantDescriptor
- `ShaderVariantDescriptor` : IAssetVariantDescriptor
- `GaussianSplatVariantDescriptor` : IAssetVariantDescriptor

### Variant Generators (static)
Produce variant assets from source data:
- `Texture2DVariantGenerator`, `Texture3DVariantGenerator`, `CubemapVariantGenerator`
- `MeshVariantGenerator`, `ShaderVariantGenerator`, `GaussianSplatVariantGenerator`

### `RecordPackage` : IDisposable
Serialized record package (world/object). Handles loading/saving of record data trees and associated assets.

### `ShaderPackage` : IDisposable
Compiled shader package containing shader sources and binary data.

### Asset Types
- `AudioX` -- audio container (load, encode, decode; supports WAV, FLAC, Vorbis)
- `MeshX` -- mesh container (vertices, triangles, points, bones, blend shapes, submeshes)
- `AnimX` -- animation container
- `Bitmap2D` / `Bitmap3D` / `BitmapCube` -- texture containers
- `GaussianCloud` -- gaussian splat point cloud
- `FontX` -- font container
- `DocumentX` -- document container (PDF support)

### `AssetHelper` (static)
Asset classification utilities: `ClassifyExtension(string)`, `ClassifyAssetClass(...)`.

### `AssetClass` (enum)
`Unknown`, `Model`, `Texture`, `Audio`, `Font`, `Video`, `Document`, `Shader`, `Binary`, `Text`, `Spreadsheet`, `Presentation`, `PointCloud`, `GaussianSplat`

### Encoding Settings
- `AudioEncodeSettings` (abstract), `FlacEncodeSettings`, `VorbisEncodeSettings`, `WavEncodeSettings`
- `TextureCompression`, `TextureSize`, `Filtering`, `SizeMode` (enums for texture variant config)

### `LegacyAssetMap` (static)
Maps old (pre-migration) Neos asset signatures to current ones. Large lookup dictionary.

---

## Asset Metadata Components

Components that expose runtime metadata from assets as synced fields:

| Component | Asset Type | Key Outputs |
|---|---|---|
| `BitmapAssetMetadata` | Texture2D | Width, Height, Format, AverageColor, AlphaData |
| `Texture2DAssetMetadata` | Texture2D | Size, MipMaps, MemoryBytes, Format, Profile |
| `Texture3DAssetMetadata` | Texture3D | Size (3D), MemoryBytes, Format, Profile |
| `CubemapAssetMetadata` | Cubemap | Size, MipMaps, MemoryBytes, Format |
| `ITexture2DAssetMetadata` | ITexture2D | Basic size (Width, Height) |
| `MeshAssetMetadata` | Mesh | VertexCount, TriangleCount, BoneCount, BlendshapeCount, UV channels |
| `MaterialAssetMetadata` | Material | VariantIndex, RawVariantID, WaitingForApply |
| `VideoTextureAssetMetadata` | VideoTexture | Size, HasAlpha, Length, PlaybackEngine |
| `GaussianSplatAssetMetadata` | GaussianSplat | SplatCount, MemoryBytes |
| `DocumentAssetMetadata` | Document | PageCount |
| `AudioStreamMetadata<S>` | AudioStream | UnreadSamples, PacketLoss, EncodedSampleRate |
| `ProceduralAssetMetadata<A>` | ProceduralAssetProvider | UpdateCount, Error |

---

## Core Asset Types

| Asset Class | Base | Purpose |
|---|---|---|
| `Animation` | `Asset<AnimationVariantDescriptor>` | AnimX animation data |
| `AudioClip` | `Asset<AudioClipVariantDescriptor>` | AudioX audio data |
| `Binary` | `Asset<BinaryVariantDescriptor>` | Generic binary file data |
| `Document` | `Asset<DocumentVariantDescriptor>` | DocumentX data (PDFs) |
| `Font` | `Asset<FontVariantDescriptor>` | Font with dynamic glyph atlas |
| `GaussianSplat` | `RendererAsset` | Gaussian splat point clouds |
| `LocaleResource` | `Asset<LocaleVariantDescriptor>` | Locale/translation resources |
| `Material` | `SharedMaterialBase<Material>` | GPU material instances |
| `MaterialPropertyBlock` | `SharedMaterialBase` | Per-instance property overrides |
| `Mesh` | `RendererAsset` | Full mesh with physics collider data |
| `RenderTexture` | `DynamicRendererAsset` | Render-to-texture |
| `SavedObject` | `Asset<SingleVariantDescriptor>` | Saved slot hierarchy graph |
| `Shader` | `RendererAsset` | Shader with variant management |
| `Sprite` | `DynamicAsset` | Texture region with border/scale |
| `Texture2D` | `Texture<...>` | 2D texture with full pipeline |
| `Texture3D` | `Texture<...>` | 3D volume texture |
| `PointRenderBuffer` / `TrailsRenderBuffer` | `RenderBufferBase` | GPU render buffers |

### Asset Base Classes

- **`Asset`** -- Abstract root with load state, read/write locking, variant tracking
- **`Asset<V>`** -- Generic base with typed variant descriptors
- **`DynamicAsset`** -- Base for dynamically created assets
- **`DynamicRendererAsset<A>`** -- Dynamic assets with renderer registration
- **`RendererAsset<A, V>`** -- Static/dynamic with renderer + variant support

---

## Export Components

| Component | Exports |
|---|---|
| `AudioExportable` | Audio clips in original format |
| `BinaryExportable` | Generic binary files |
| `GaussianSplatExportable` | PLY, SPZ formats |
| `ModelExportable` | DAE, OBJ, PLY, FBX, STL, X, GLTF, ASSBIN, ASSXML |
| `PackageExportable` | `.resonitepackage` with gathered assets |
| `TextExportable` | Text content with configurable extension |
| `TextureExportable` | PNG, JPG, EXR, LUT |
| `ExportDialog` | UI dialog for configuring exports |

---

## Cloud Indicator Components

| Component | Purpose |
|---|---|
| `CloudServerStatus` | Exposes cloud server health/status |
| `CloudUserInfo` | Fetches/exposes cloud user profile for a UserId |
| `OnlineStatistics` | Live online user and session statistics |
| `FundingStatistics` | Global funding/supporter statistics |
| `RecordSyncStatus` | Record sync queue status with color coding |
| `StorageUsageStatus` | Cloud storage usage/quota |
| `UserLoginStatus` | Whether current user is logged in |
| `UserOnlineStatusSync` | Two-way syncs user's online status |
| `UniverseStatus` | Current universe identity and membership |
| `AccountMigrationsList` / `AccountMigrationStatus` | Migration task tracking |
| `SupporterCreditList` | Formatted supporter credit list |
| `UserLoginManager` | Login/logout UI flow management |

---

## Cloud Variable Components

### Base Classes

- **`CloudVariableBase`** -- Base managing proxy registration; fields: `Path` (string)
- **`CloudValueBase<T>`** -- Read/write logic for cloud-synced fields
- **`CloudValueOwnerBase<T>`** -- Adds explicit `VariableOwnerId` and `ChangeHandling`
- **`ActiveUserCloudValueBase<T>`** -- Scoped to active user under the slot

### Concrete Components

| Component | Purpose |
|---|---|
| `CloudValueVariable<T>` | Self-contained cloud variable with explicit owner |
| `CloudValueField<T>` | Links external field to cloud variable |
| `ActiveUserCloudField<T>` | Syncs cloud variable with target field for active user |
| `ActiveUserCloudValueVariable<T>` | Self-contained cloud variable for active user |
| `CloudValueVariableDriver<T>` | Drives a field from a cloud variable with optional write-back |

### CloudVariableChangeMode (enum)
`Ignore`, `WriteIfOwner`, `AlwaysWrite`

### Extension Methods
- `SyncWithCloudVariable` -- Drive fields from cloud variables
- `SyncWithCloudVariableNonDriving` -- Attach CloudValueField to a field

---

## Localization Components

| Component | Purpose |
|---|---|
| `LocaleStringDriver` | Drives a string field with localized value from locale resource |
| `LocaleActiveDriver` | Drives boolean based on current locale match |
| `CurrentLocaleInfo` | Outputs current locale code, language, names |
| `LocaleAuthorsInfo` | Outputs locale translation author credits |
| `LocaleHelper` (static) | Utility for locale resolution and driving |
