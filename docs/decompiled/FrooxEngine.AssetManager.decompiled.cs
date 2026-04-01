using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Elements.Assets;
using Elements.Core;
using FrooxEngine.Store;
using Renderite.Shared;
using SkyFrost.Base;

namespace FrooxEngine;

public class AssetManager : IDisposable
{
	/// <summary>
	/// Will force asset variants to be generated locally. This is mostly useful for debugging and should NOT
	/// be used for production, as it will create load on systems and not work on certain platforms (e.g. Android)
	/// </summary>
	public static bool ForceGenerateVariants;

	private static HashSet<string> supportedSchemes = new HashSet<string> { "local", "http", "https", "ftp" };

	private EngineAssetGatherer assetGatherer;

	private AssetMetadataManager metadataManager;

	private AssetVariantGenerator textureVariantGenerator;

	private AssetVariantGenerator generalVariantGenerator;

	private Dictionary<AssetID, AssetVariantManager> variantManagers = new Dictionary<AssetID, AssetVariantManager>();

	private List<AssetID> managersToRemove = new List<AssetID>();

	private Dictionary<Type, Dictionary<Uri, Task>> metadataRequests = new Dictionary<Type, Dictionary<Uri, Task>>();

	private object assetManagerLock = new object();

	public float UnloadDelaySeconds
	{
		get
		{
			if (Engine.Platform != Platform.Android)
			{
				return 15f;
			}
			return 5f;
		}
	}

	public Engine Engine { get; private set; }

	public RenderSystem Render => Engine.RenderSystem;

	public IBackingBufferAllocator TextureAllocator
	{
		get
		{
			if (!Render.HasRenderer)
			{
				return null;
			}
			return Render;
		}
	}

	public LocalDB LocalDB => Engine.LocalDB;

	public EngineSkyFrostInterface Cloud => Engine.Cloud;

	public bool IsDisposed { get; private set; }

	public TextureQualitySettings TextureSettings { get; private set; }

	public Texture2D WhiteTexture { get; private set; }

	public Texture2D BlackTexture { get; private set; }

	public Texture2D ClearTexture { get; private set; }

	public Texture2D DarkCheckerTexture { get; private set; }

	public Cubemap DarkCheckerCubemap { get; private set; }

	public long TotalBytesPerSecond => assetGatherer.TotalBytesPerSecond;

	private Texture2D CreateDefaultTexture(int width, int height, Action<Bitmap2D> init)
	{
		Texture2D texture2D = new Texture2D();
		texture2D.InitializeDynamic(this);
		Bitmap2D bitmap2D = new Bitmap2D(width, height, TextureFormat.RGBA32, mipmaps: false, ColorProfile.sRGB, flipY: true, null, TextureAllocator);
		init(bitmap2D);
		texture2D.SetFromBitmap2D(bitmap2D, new TextureUploadHint
		{
			readable = false
		}, TextureFilterMode.Bilinear, 8, TextureWrapMode.Repeat, TextureWrapMode.Repeat, 0f, delegate
		{
		});
		return texture2D;
	}

	private Texture2D CreateDefaultTexture(colorX c)
	{
		return CreateDefaultTexture(4, 4, delegate(Bitmap2D bmp)
		{
			bmp.Clear(c.baseColor);
		});
	}

	private Cubemap CreateDefaultCubemap(int size, Action<BitmapCube> init)
	{
		Cubemap cubemap = new Cubemap();
		cubemap.InitializeDynamic(this);
		BitmapCube bitmapCube = new BitmapCube(size, size, TextureFormat.RGBA32, mipmaps: false, ColorProfile.sRGB, null, TextureAllocator);
		init(bitmapCube);
		cubemap.SetFromBitmapCube(bitmapCube, TextureFilterMode.Bilinear, 0, delegate
		{
		});
		return cubemap;
	}

	internal async Task Initialize(Engine engine)
	{
		Engine = engine;
		int maxThreads = MathX.Max(1, (engine.PhysicalProcessorCount ?? engine.ProcessorCount) - 2);
		int num = MathX.Max(1, (engine.PhysicalProcessorCount ?? engine.ProcessorCount) / 4);
		assetGatherer = new EngineAssetGatherer(this);
		metadataManager = new AssetMetadataManager(this, num);
		textureVariantGenerator = new AssetVariantGenerator(this, maxThreads, MathX.Max(1, num / 2));
		generalVariantGenerator = new AssetVariantGenerator(this, maxThreads, num);
		WhiteTexture = CreateDefaultTexture(colorX.White);
		BlackTexture = CreateDefaultTexture(colorX.Black);
		ClearTexture = CreateDefaultTexture(colorX.Clear);
		DarkCheckerTexture = CreateDefaultTexture(128, 128, delegate(Bitmap2D bmp)
		{
			for (int i = 0; i < bmp.Size.y; i++)
			{
				for (int j = 0; j < bmp.Size.x; j++)
				{
					bool num2 = ((j >> 2) & 1) == 1;
					bool flag = ((i >> 2) & 1) == 0;
					bool flag2 = num2 ^ flag;
					bmp.SetPixel(j, i, flag2 ? new color(0f, 0.1f) : new color(0.05f, 0.5f));
				}
			}
		});
		DarkCheckerCubemap = CreateDefaultCubemap(64, delegate(BitmapCube bmp)
		{
			for (int i = 0; i < 6; i++)
			{
				BitmapCube.Face face = (BitmapCube.Face)i;
				colorX colorX = new colorX(0.1f, 0.2f);
				switch (face)
				{
				case BitmapCube.Face.NegX:
					colorX = new colorX(0.1f, 0f, 0.1f, 0.5f);
					break;
				case BitmapCube.Face.NegY:
					colorX = new colorX(0.1f, 0.1f, 0f, 0.5f);
					break;
				case BitmapCube.Face.NegZ:
					colorX = new colorX(0f, 0.1f, 0.1f, 0.5f);
					break;
				case BitmapCube.Face.PosX:
					colorX = new colorX(0.1f, 0f, 0f, 0.5f);
					break;
				case BitmapCube.Face.PosY:
					colorX = new colorX(0f, 0.1f, 0f, 0.5f);
					break;
				case BitmapCube.Face.PosZ:
					colorX = new colorX(0f, 0f, 0.1f, 0.5f);
					break;
				}
				for (int j = 0; j < bmp.Size.y; j++)
				{
					for (int k = 0; k < bmp.Size.x; k++)
					{
						bool num2 = ((k >> 2) & 1) == 1;
						bool flag = ((j >> 2) & 1) == 0;
						bool flag2 = num2 ^ flag;
						bmp.SetPixel(k, j, face, flag2 ? new color(0f, 0.2f) : colorX.baseColor);
					}
				}
			}
		});
		Settings.RegisterComponentChanges<TextureQualitySettings>(OnTextureQualitySettingsChanged);
	}

	public void ForceUpdateAllTextures()
	{
		List<World> list = Pool.BorrowList<World>();
		Engine.WorldManager.GetWorlds(list);
		foreach (World w in list)
		{
			w.RunSynchronously(delegate
			{
				w.RootSlot.ForeachComponentInChildren(delegate(StaticTexture2D t)
				{
					t.MarkChangeDirty();
				});
			});
		}
		Pool.Return(ref list);
	}

	private void OnTextureQualitySettingsChanged(TextureQualitySettings settings)
	{
		TextureSettings = settings;
	}

	public int? GetMaxTextureSize(int size)
	{
		return TextureSettings?.GetMaxSize(size);
	}

	public void Update(double assetsMaxMilliseconds, double particlesMaxMilliseconds)
	{
		lock (assetManagerLock)
		{
			foreach (AssetID item in managersToRemove)
			{
				if (variantManagers.TryGetValue(item, out AssetVariantManager value) && value.VariantCount == 0)
				{
					variantManagers.Remove(item);
				}
			}
			managersToRemove.Clear();
		}
	}

	public void Dispose()
	{
		CheckDisposed();
		IsDisposed = true;
	}

	private void CheckDisposed()
	{
		if (IsDisposed)
		{
			throw new Exception("AssetManager is disposed");
		}
	}

	public bool IsSupportedScheme(Uri assetURL)
	{
		if (assetURL.Scheme == Cloud.Assets.DBScheme)
		{
			return true;
		}
		return supportedSchemes.Contains(assetURL.Scheme);
	}

	public ValueTask<GatherResult> GatherAsset(Uri assetURL, float priority, DB_Endpoint? overrideEndpoint = null)
	{
		return assetGatherer.Gather(assetURL, priority, overrideEndpoint);
	}

	public async ValueTask<string> GatherAssetFile(Uri assetURL, float priority, DB_Endpoint? overrideEndpoint = null)
	{
		return await (await assetGatherer.Gather(assetURL, priority, overrideEndpoint).ConfigureAwait(continueOnCapturedContext: false)).GetFile().ConfigureAwait(continueOnCapturedContext: false);
	}

	public World GetBestWorldForLocal(Uri assetUrl)
	{
		World world = null;
		List<World> list = Pool.BorrowList<World>();
		Engine.WorldManager.GetWorlds(list);
		foreach (World item in list)
		{
			if (item.Session != null)
			{
				WorldAssetManager assetManager = item.AssetManager;
				if (assetManager != null && assetManager.HasLocalAsset(assetUrl))
				{
					world = item;
					break;
				}
				if (item.GetUserByMachineId(assetUrl.Host) != null && (world == null || (item.Focus != World.WorldFocus.Focused && world.Focus == World.WorldFocus.Focused)))
				{
					world = item;
				}
			}
		}
		Pool.Return(ref list);
		return world;
	}

	public void RequestAsset<A>(Uri assetURL, IEngineAssetVariantDescriptor variantDescriptor, IAssetRequester requester, IAssetMetadata metadata) where A : Asset, new()
	{
		if (variantDescriptor == null && variantDescriptor.CorrespondingAssetType != typeof(A))
		{
			throw new Exception("Variant descriptor asset type doesn't match the type of requested asset.");
		}
		AssetID key = new AssetID(assetURL, typeof(A));
		lock (assetManagerLock)
		{
			if (!variantManagers.TryGetValue(key, out AssetVariantManager value))
			{
				value = new AssetVariantManager<A>(assetURL, this, metadata);
				variantManagers.Add(key, value);
			}
			(value as AssetVariantManager<A>).RequestAsset(requester, variantDescriptor);
		}
	}

	internal void ScheduleVariantManagerRemoval(in AssetID id)
	{
		lock (assetManagerLock)
		{
			managersToRemove.Add(id);
		}
	}

	public async Task<T> RequestMetadata<T>(Uri assetURL, bool waitOnCloud = false) where T : class, IAssetMetadata, new()
	{
		Task<T> task;
		lock (metadataRequests)
		{
			if (!metadataRequests.TryGetValue(typeof(T), out Dictionary<Uri, Task> requests))
			{
				requests = new Dictionary<Uri, Task>();
				metadataRequests.Add(typeof(T), requests);
			}
			if (requests.TryGetValue(assetURL, out Task value))
			{
				task = (Task<T>)value;
			}
			else
			{
				task = Task.Run(async delegate
				{
					T result = await RequestMetadataDirect<T>(assetURL, waitOnCloud).ConfigureAwait(continueOnCapturedContext: false);
					lock (metadataRequests)
					{
						requests.Remove(assetURL);
						return result;
					}
				});
				requests.Add(assetURL, task);
			}
		}
		return await task.ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task<T> RequestMetadataDirect<T>(Uri assetURL, bool waitOnCloud = false) where T : class, IAssetMetadata, new()
	{
		if (typeof(T) == typeof(DummyMetadata))
		{
			return DummyMetadata.Dummy as T;
		}
		await default(ToBackground);
		string identifier;
		if (assetURL.Scheme == Cloud.Assets.DBScheme)
		{
			identifier = Cloud.Assets.DBSignature(assetURL);
		}
		else
		{
			identifier = assetURL.ToString();
		}
		T metadata = await Engine.LocalDB.TryFetchAssetMetadataAsync<T>(identifier);
		if (metadata != null && metadata.IsLatestVersion)
		{
			return metadata;
		}
		if (assetURL.Scheme == Cloud.Assets.DBScheme)
		{
			T cloudMetadata = await metadataManager.FetchCloudMetadata<T>(identifier).ConfigureAwait(continueOnCapturedContext: false);
			if (cloudMetadata != null)
			{
				Task.Run(async delegate
				{
					await Engine.LocalDB.SaveAssetMetadataAsync(cloudMetadata);
				});
				return cloudMetadata;
			}
			if (!waitOnCloud)
			{
				Task.Run(async delegate
				{
					await Task.Delay(30000).ConfigureAwait(continueOnCapturedContext: false);
					await Cloud.Assets.GetAssetMetadata<T>(identifier).ConfigureAwait(continueOnCapturedContext: false);
				});
			}
			if (metadata != null)
			{
				return metadata;
			}
			if (waitOnCloud)
			{
				do
				{
					CloudResult<T> cloudResult = await Cloud.Assets.GetAssetMetadata<T>(identifier).ConfigureAwait(continueOnCapturedContext: false);
					metadata = ((cloudResult != null) ? cloudResult.Entity : null);
					if (metadata == null)
					{
						await Task.Delay(5000).ConfigureAwait(continueOnCapturedContext: false);
					}
				}
				while (metadata == null);
				if (metadata != null)
				{
					await Engine.LocalDB.SaveAssetMetadataAsync(metadata);
				}
				return metadata;
			}
		}
		if (assetURL.Scheme == "local" && assetURL.Host != LocalDB.MachineID)
		{
			World bestWorldForLocal = GetBestWorldForLocal(assetURL);
			if (bestWorldForLocal != null)
			{
				metadata = await bestWorldForLocal.Session.Assets.RequestMetadata<T>(assetURL).ConfigureAwait(continueOnCapturedContext: false);
				if (metadata != null)
				{
					await Engine.LocalDB.SaveAssetMetadataAsync(metadata);
					return metadata;
				}
			}
		}
		metadata = await metadataManager.ComputeMetadata<T>(assetURL).ConfigureAwait(continueOnCapturedContext: false);
		if (metadata != null)
		{
			await Engine.LocalDB.SaveAssetMetadataAsync(metadata);
		}
		return metadata;
	}

	public void SeedMobileVariant(string signature, IAssetVariantDescriptor variantDescriptor)
	{
		if (Engine.Platform != Platform.Windows || !(variantDescriptor is Elements.Assets.Texture2DVariantDescriptor))
		{
			return;
		}
		Task.Run(async delegate
		{
			await Task.Delay(TimeSpan.FromMinutes(2L)).ConfigureAwait(continueOnCapturedContext: false);
			Elements.Assets.Texture2DVariantDescriptor texture2DVariantDescriptor = (Elements.Assets.Texture2DVariantDescriptor)variantDescriptor;
			TextureCompression compression;
			switch (texture2DVariantDescriptor.TextureCompression)
			{
			default:
				return;
			case TextureCompression.BC1_Crunched:
				compression = TextureCompression.ETC2_RGB_Crunched;
				break;
			case TextureCompression.BC3_Crunched:
				compression = TextureCompression.ETC2_RGBA8_Crunched;
				break;
			}
			Elements.Assets.Texture2DVariantDescriptor descriptor = new Elements.Assets.Texture2DVariantDescriptor(compression, texture2DVariantDescriptor.CompressionQuality, texture2DVariantDescriptor.Width, texture2DVariantDescriptor.Height, texture2DVariantDescriptor.MipMaps, texture2DVariantDescriptor.Filtering, texture2DVariantDescriptor.ColorPreprocess, texture2DVariantDescriptor.AlphaPreprocess);
			await Cloud.Assets.RequestAssetVariant(signature, descriptor).ConfigureAwait(continueOnCapturedContext: false);
		});
	}

	public void SeedGaussianSplatVariants(string signature, IAssetVariantDescriptor variantDescriptor)
	{
		if (Engine.Platform == Platform.Windows && variantDescriptor is Elements.Assets.GaussianSplatVariantDescriptor)
		{
			Task.Run(async delegate
			{
				await Task.Delay(TimeSpan.FromMinutes(2L)).ConfigureAwait(continueOnCapturedContext: false);
				GaussianSplatVariantDescriptor qualityDescriptor = GaussianSplatQualitySettings.GetQualityDescriptor(GaussianSplatQualityPreset.VeryLow);
				GaussianSplatVariantDescriptor medium = GaussianSplatQualitySettings.GetQualityDescriptor(GaussianSplatQualityPreset.Medium);
				GaussianSplatVariantDescriptor high = GaussianSplatQualitySettings.GetQualityDescriptor(GaussianSplatQualityPreset.High);
				GaussianSplatVariantDescriptor low = GaussianSplatQualitySettings.GetQualityDescriptor(GaussianSplatQualityPreset.Low);
				await Cloud.Assets.RequestAssetVariant(signature, qualityDescriptor).ConfigureAwait(continueOnCapturedContext: false);
				await Cloud.Assets.RequestAssetVariant(signature, medium).ConfigureAwait(continueOnCapturedContext: false);
				await Cloud.Assets.RequestAssetVariant(signature, high).ConfigureAwait(continueOnCapturedContext: false);
				await Cloud.Assets.RequestAssetVariant(signature, low).ConfigureAwait(continueOnCapturedContext: false);
			});
		}
	}

	public void SeedMeshColliderVariants(string signature, IAssetVariantDescriptor variantDescriptor)
	{
		if (Engine.Platform != Platform.Windows)
		{
			return;
		}
		Elements.Assets.MeshVariantDescriptor mesh = variantDescriptor as Elements.Assets.MeshVariantDescriptor;
		if (mesh != null && (mesh.DataType == MeshDataType.MeshCollider || mesh.DataType == MeshDataType.DualSidedMeshCollider))
		{
			Task.Run(async delegate
			{
				await Task.Delay(TimeSpan.FromMinutes(2L)).ConfigureAwait(continueOnCapturedContext: false);
				Elements.Assets.MeshVariantDescriptor otherCompression = new Elements.Assets.MeshVariantDescriptor(mesh.DataType, (mesh.Compression != MeshCompression.LZ4) ? MeshCompression.LZ4 : MeshCompression.LZMA);
				Elements.Assets.MeshVariantDescriptor descriptor = new Elements.Assets.MeshVariantDescriptor((mesh.DataType != MeshDataType.MeshCollider) ? MeshDataType.MeshCollider : MeshDataType.DualSidedMeshCollider, MeshCompression.LZ4);
				Elements.Assets.MeshVariantDescriptor otherVariantLZMA = new Elements.Assets.MeshVariantDescriptor((mesh.DataType != MeshDataType.MeshCollider) ? MeshDataType.MeshCollider : MeshDataType.DualSidedMeshCollider, MeshCompression.LZMA);
				await Cloud.Assets.RequestAssetVariant(signature, descriptor).ConfigureAwait(continueOnCapturedContext: false);
				await Cloud.Assets.RequestAssetVariant(signature, otherVariantLZMA).ConfigureAwait(continueOnCapturedContext: false);
				await Cloud.Assets.RequestAssetVariant(signature, otherCompression).ConfigureAwait(continueOnCapturedContext: false);
			});
		}
	}

	public void SeedNormalMapVariant(string signature, IAssetVariantDescriptor variantDescriptor)
	{
		if (Engine.Platform == Platform.Windows && variantDescriptor is Elements.Assets.Texture2DVariantDescriptor)
		{
			Task.Run(async delegate
			{
				await Task.Delay(TimeSpan.FromMinutes(RandomX.Range(1f, 2f))).ConfigureAwait(continueOnCapturedContext: false);
				Elements.Assets.Texture2DVariantDescriptor texture2DVariantDescriptor = (Elements.Assets.Texture2DVariantDescriptor)variantDescriptor;
				Elements.Assets.Texture2DVariantDescriptor descriptor = new Elements.Assets.Texture2DVariantDescriptor(TextureCompression.BC3nm_Crunched, texture2DVariantDescriptor.CompressionQuality, texture2DVariantDescriptor.Width, texture2DVariantDescriptor.Height, texture2DVariantDescriptor.MipMaps, texture2DVariantDescriptor.Filtering, texture2DVariantDescriptor.ColorPreprocess, texture2DVariantDescriptor.AlphaPreprocess);
				await Cloud.Assets.RequestAssetVariant(signature, descriptor).ConfigureAwait(continueOnCapturedContext: false);
			});
		}
	}

	public async ValueTask<string> RequestVariant(Uri assetURL, string variantIdentifier, IAssetVariantDescriptor variantDescriptor, bool generateVariant, bool waitForCloud = false)
	{
		if (string.IsNullOrWhiteSpace(variantIdentifier))
		{
			return await GatherAssetFile(assetURL, 0f).ConfigureAwait(continueOnCapturedContext: false);
		}
		Uri variantUrl = new Uri(assetURL, "?" + variantIdentifier);
		string text = (await LocalDB.TryFetchAssetRecordAsync(variantUrl))?.path;
		bool flag = text != null && File.Exists(text);
		if (flag && EngineAssetGatherer.IsFileValid(text))
		{
			return text;
		}
		if (flag)
		{
			FileUtil.Delete(text);
		}
		if (assetURL.Scheme == Cloud.Assets.DBScheme)
		{
			string signature = Cloud.Assets.DBSignature(variantUrl);
			string variantFile = await GatherAssetFile(variantUrl, 0f).ConfigureAwait(continueOnCapturedContext: false);
			if (variantFile != null)
			{
				return variantFile;
			}
			CloudResult<List<string>> result = await Cloud.Assets.RequestAssetVariant(signature, variantDescriptor).ConfigureAwait(continueOnCapturedContext: false);
			if (result.State == HttpStatusCode.BadRequest && result.Content.Contains("no metadata"))
			{
				Task.Run(async delegate
				{
					AssetVariantType assetType = AssetVariantHelper.DescriptorToVariantType(variantDescriptor.GetType());
					if (waitForCloud)
					{
						bool gotMetadata = false;
						do
						{
							CloudResult<IAssetMetadata> cloudResult = await Cloud.Assets.GetAssetMetadata(assetType, signature).ConfigureAwait(continueOnCapturedContext: false);
							if (cloudResult.State == HttpStatusCode.OK)
							{
								gotMetadata = true;
							}
							else
							{
								if (cloudResult.State != HttpStatusCode.NoContent)
								{
									UniLog.Warning($"Error when requesting asset metadata in order to request asset variant for {variantUrl}\n" + cloudResult);
									break;
								}
								await Task.Delay(5000).ConfigureAwait(continueOnCapturedContext: false);
							}
						}
						while (!gotMetadata);
						result = await Cloud.Assets.RequestAssetVariant(signature, variantDescriptor).ConfigureAwait(continueOnCapturedContext: false);
					}
				});
			}
			SeedMobileVariant(signature, variantDescriptor);
			SeedMeshColliderVariants(signature, variantDescriptor);
			SeedGaussianSplatVariants(signature, variantDescriptor);
			if (result.State == HttpStatusCode.OK)
			{
				foreach (string newIdentifier in result.Entity)
				{
					string text2 = await GatherAssetFile(new Uri(assetURL, "?" + newIdentifier), 0f).ConfigureAwait(continueOnCapturedContext: false);
					if (text2 == null)
					{
						variantFile = null;
						break;
					}
					IAssetVariantDescriptor assetVariantDescriptor = AssetVariantHelper.FromIdentifier(variantDescriptor.GetType(), newIdentifier);
					string oldIdentifier = assetVariantDescriptor.VariantIdentifier;
					text2 = await LocalDB.StoreCacheRecordAsync(new Uri(assetURL, "?" + oldIdentifier), text2);
					text2 = await LocalDB.StoreCacheRecordAsync(new Uri(assetURL, "?" + newIdentifier), text2);
					if (oldIdentifier == variantIdentifier)
					{
						variantFile = text2;
					}
				}
				if (variantFile != null)
				{
					return variantFile;
				}
			}
			if (waitForCloud)
			{
				do
				{
					variantFile = await GatherAssetFile(variantUrl, 0f).ConfigureAwait(continueOnCapturedContext: false);
					if (variantFile == null)
					{
						await Task.Delay(5000).ConfigureAwait(continueOnCapturedContext: false);
					}
				}
				while (variantFile == null);
				return variantFile;
			}
		}
		if (assetURL.Scheme == "local" && assetURL.Host != Engine.LocalDB.MachineID)
		{
			Uri assetURL2 = new Uri(assetURL, "?" + variantIdentifier + "?" + variantDescriptor.VariantIdentifier + "?" + AssetVariantHelper.DescriptorToVariantType(variantDescriptor.GetType()));
			string variantFile = await GatherAssetFile(assetURL2, 1f).ConfigureAwait(continueOnCapturedContext: false);
			if (variantFile != null)
			{
				await LocalDB.StoreCacheRecordAsync(variantUrl, variantFile);
				return variantFile;
			}
		}
		if (!generateVariant)
		{
			return null;
		}
		string text3 = await GatherAssetFile(assetURL, 0f).ConfigureAwait(continueOnCapturedContext: false);
		if (text3 == null || !File.Exists(text3))
		{
			return null;
		}
		return (!(variantDescriptor is TextureVariantDescriptor)) ? (await generalVariantGenerator.ComputeVariant(assetURL, text3, variantIdentifier, variantDescriptor).ConfigureAwait(continueOnCapturedContext: false)) : (await textureVariantGenerator.ComputeVariant(assetURL, text3, variantIdentifier, variantDescriptor).ConfigureAwait(continueOnCapturedContext: false));
	}

	public void GetGatherJobs(List<EngineGatherJob> jobs)
	{
		assetGatherer.GetAllJobs(jobs);
	}

	public void GetActiveJobs(List<EngineGatherJob> jobs)
	{
		assetGatherer.GetActiveJobs(jobs);
	}

	public void GetVariantManagers(List<AssetVariantManager> managers)
	{
		lock (assetManagerLock)
		{
			managers.AddRange(variantManagers.Values);
		}
	}
}
