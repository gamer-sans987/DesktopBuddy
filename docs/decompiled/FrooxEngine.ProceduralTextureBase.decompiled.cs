using System;
using System.Threading.Tasks;
using Elements.Assets;
using Elements.Core;
using FrooxEngine.UIX;
using Renderite.Shared;

namespace FrooxEngine;

public abstract class ProceduralTextureBase : ProceduralAssetProvider<Texture2D>, ITexture2DProvider, IAssetProvider<ITexture2D>, IAssetProvider, IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable, ITextureProvider, IAssetProvider<ITexture>
{
	protected TextureUploadHint uploadHint;

	public readonly Sync<TextureFilterMode> FilterMode;

	public readonly Sync<int> AnisotropicLevel;

	public readonly Sync<TextureWrapMode> WrapModeU;

	public readonly Sync<TextureWrapMode> WrapModeV;

	[Range(-1f, 1f, "0.00")]
	public readonly Sync<float> MipmapBias;

	public readonly Sync<ColorProfile> Profile;

	private AssetIntegrated _assetIntegrated;

	public override int Version => 1;

	public virtual bool AutoGenerateMipmaps => true;

	protected abstract int2 GenerateSize { get; }

	protected abstract bool GenerateMipmaps { get; }

	protected abstract TextureFormat GenerateFormat { get; }

	protected Bitmap2D tex2D { get; private set; }

	ITexture2D IAssetProvider<ITexture2D>.Asset => Asset;

	ITexture IAssetProvider<ITexture>.Asset => Asset;

	protected override void OnAwake()
	{
		base.OnAwake();
		FilterMode.Value = TextureFilterMode.Bilinear;
		AnisotropicLevel.Value = 8;
		WrapModeU.Value = TextureWrapMode.Repeat;
		WrapModeV.Value = TextureWrapMode.Repeat;
		Profile.Value = ColorProfile.sRGB;
	}

	protected override void AssetCreated(Texture2D asset)
	{
	}

	private void PrepareBitmap()
	{
		if (tex2D == null || tex2D.Size != GenerateSize || tex2D.HasMipMaps != GenerateMipmaps || tex2D.Format != GenerateFormat || tex2D.Profile != (ColorProfile)Profile)
		{
			tex2D?.Buffer.Dispose();
			tex2D = null;
			if (MathX.MinComponent(GenerateSize) >= 4 && GenerateFormat.SupportsWrite())
			{
				tex2D = new Bitmap2D(GenerateSize.x, GenerateSize.y, GenerateFormat, GenerateMipmaps, Profile, flipY: true, null, base.Allocator);
				if (base.Allocator != null)
				{
					tex2D.RawData.Clear();
				}
			}
		}
		uploadHint.region = null;
	}

	protected void SetBitmap(Bitmap2D tex2D)
	{
		this.tex2D = tex2D;
		uploadHint.region = null;
	}

	private void PostprocessTexture()
	{
		if (tex2D.HasMipMaps && AutoGenerateMipmaps)
		{
			tex2D.GenerateMipmapsBox();
		}
	}

	protected override void UpdateAssetData(Texture2D asset)
	{
		PrepareBitmap();
		if (tex2D != null)
		{
			UpdateTextureData(tex2D);
			PostprocessTexture();
		}
	}

	protected override async ValueTask UpdateAssetDataAsync(Texture2D asset)
	{
		PrepareBitmap();
		if (tex2D != null)
		{
			await UpdateTextureDataAsync(tex2D);
			PostprocessTexture();
		}
	}

	protected override void ClearAsset()
	{
		tex2D?.Buffer.Dispose();
		tex2D = null;
		ClearTextureData();
	}

	protected override void UploadAssetData(AssetIntegrated integratedCallback)
	{
		if (tex2D != null)
		{
			SetFromCurrentBitmap(uploadHint, integratedCallback);
		}
		else
		{
			integratedCallback(environmentInstanceChanged: true);
		}
	}

	protected void SetFromCurrentBitmap(TextureUploadHint hint, AssetIntegrated integratedCallback = null)
	{
		if (integratedCallback == null && _assetIntegrated == null)
		{
			_assetIntegrated = TextureIntegrated;
		}
		hint.readable = true;
		Asset.SetFromBitmap2D(tex2D, hint, FilterMode, AnisotropicLevel, WrapModeU, WrapModeV, MipmapBias, integratedCallback ?? _assetIntegrated);
	}

	private void TextureIntegrated(bool instanceChanged)
	{
		AssetIntegrationUpdated(instanceChanged);
	}

	protected virtual void UpdateTextureData(Bitmap2D tex2D)
	{
		throw new NotImplementedException("Derived class must override this method");
	}

	protected virtual Task UpdateTextureDataAsync(Bitmap2D tex2D)
	{
		throw new NotImplementedException("Derived class must override this method");
	}

	protected abstract void ClearTextureData();

	public override void BuildInspectorUI(UIBuilder ui)
	{
		base.BuildInspectorUI(ui);
		ProceduralAssetMetadata<Texture2D> proceduralAssetMetadata = ui.Root.AttachComponent<ProceduralAssetMetadata<Texture2D>>();
		proceduralAssetMetadata.Asset.Target = this;
		ui.Text("Inspector.ProceduralAsset.UpdateCount".AsLocaleKey("n", proceduralAssetMetadata.UpdateCount));
		ui.Text("Inspector.ProceduralAsset.Error".AsLocaleKey("error", (object)proceduralAssetMetadata.Error));
		ui.Button("Inspector.Texture.BakeTexture".AsLocaleKey(), OnBakeTexture);
	}

	[SyncMethod(typeof(Action), new string[] { })]
	public void BakeTexture()
	{
		StartTask(BakeTextureAsync);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnBakeTexture(IButton button, ButtonEventData eventData)
	{
		button.Enabled = false;
		BakeTexture();
	}

	private async Task BakeTextureAsync()
	{
		AssetLoader<ITexture2D> loader = null;
		if (base.AssetReferenceCount == 0)
		{
			loader = this.ForceLoad();
		}
		while (Asset?.Data == null)
		{
			await default(NextUpdate);
		}
		object bakeLock = new object();
		await Asset.RequestReadLock(bakeLock);
		await default(ToBackground);
		Uri uri;
		try
		{
			uri = await base.Engine.LocalDB.SaveAssetAsync(Asset.Data).ConfigureAwait(continueOnCapturedContext: false);
		}
		finally
		{
			Asset.ReleaseReadLock(bakeLock);
		}
		await default(ToWorld);
		StaticTexture2D staticTexture2D = base.Slot.AttachComponent<StaticTexture2D>();
		staticTexture2D.URL.Value = uri;
		staticTexture2D.FilterMode.Value = FilterMode.Value;
		staticTexture2D.AnisotropicLevel.Value = AnisotropicLevel.Value;
		staticTexture2D.WrapModeU.Value = WrapModeU.Value;
		staticTexture2D.WrapModeV.Value = WrapModeV.Value;
		base.World.ReplaceReferenceTargets(this, staticTexture2D, nullIfIncompatible: false);
		Destroy();
		loader?.Destroy();
	}

	protected override void OnLoading(DataTreeNode node, LoadControl control)
	{
		base.OnLoading(node, control);
		if (control.GetTypeVersion(GetType()) < 1)
		{
			Profile.Value = ColorProfile.sRGBAlpha;
		}
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		FilterMode = new Sync<TextureFilterMode>();
		AnisotropicLevel = new Sync<int>();
		WrapModeU = new Sync<TextureWrapMode>();
		WrapModeV = new Sync<TextureWrapMode>();
		MipmapBias = new Sync<float>();
		Profile = new Sync<ColorProfile>();
	}
}
