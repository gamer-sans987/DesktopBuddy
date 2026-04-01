using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Elements.Assets;
using Elements.Core;
using Renderite.Shared;

namespace FrooxEngine;

public class Texture2D : Texture<Texture2D, Texture2DVariantDescriptor, Elements.Assets.Texture2DVariantDescriptor, BitmapMetadata, Bitmap2D>, ITexture2D, ITexture, IRendererAsset, IAsset
{
	private Bitmap2D _data;

	private bool _instanceChanged;

	private int _fireOnIntegratedResultTypeFlags;

	private AssetIntegrated _assetIntegrated;

	public Bitmap2D Data
	{
		get
		{
			return _data;
		}
		set
		{
			if (_data != value)
			{
				if (value != null && !value.BufferExists)
				{
					throw new InvalidOperationException("Trying to assign texture with missing buffer: " + value);
				}
				_data?.Buffer.Dispose();
				_data = value;
			}
		}
	}

	public int2 Size { get; private set; }

	public int MipMapCount { get; private set; }

	public BitmapMetadata BitmapMetadata => base.VariantManager.Metadata as BitmapMetadata;

	private TextureUpdateResultType FireIntegratedOnResultType
	{
		get
		{
			return (TextureUpdateResultType)_fireOnIntegratedResultTypeFlags;
		}
		set
		{
			_fireOnIntegratedResultTypeFlags = (int)value;
		}
	}

	protected override RenderAssetManager<Texture2D> Manager => base.RenderSystem.Texture2Ds;

	protected override void SetParamsFromVariant(Texture2DVariantDescriptor variant)
	{
		Size = new int2(variant.Width, variant.Height);
		MipMapCount = variant.MipMapCount;
		base.Format = variant.TextureCompression.ToFormat(BitmapMetadata, base.IsThreeChannelSupported);
		base.Profile = variant.Profile;
	}

	public async Task<Bitmap2D> GetReadableTextureData()
	{
		if (Data != null && Data.Format.SupportsRead())
		{
			return Data;
		}
		if (base.AssetURL != null)
		{
			return await GatherTextureDataAndLoad();
		}
		return null;
	}

	public async Task<Bitmap2D> GetOriginalTextureData()
	{
		if (base.AssetURL != null)
		{
			return await GatherTextureDataAndLoad();
		}
		if (Data != null && Data.Format.SupportsRead())
		{
			return Data;
		}
		return null;
	}

	private async Task<Bitmap2D> GatherTextureDataAndLoad()
	{
		if (base.AssetURL == null)
		{
			throw new InvalidOperationException("This operation cannot be called when AssetURL is null");
		}
		await default(ToBackground);
		string file = await base.AssetManager.GatherAssetFile(base.AssetURL, 0f);
		await default(ToBackground);
		if (file == null)
		{
			return null;
		}
		return DecodeFile(file, null, forceMips: false, loadingOriginal: false, sharedMemory: false);
	}

	public void SetFromBitmap2D(Bitmap2D bitmap, TextureUploadHint hint, TextureFilterMode filterMode, int anisoLevel, TextureWrapMode wrapU, TextureWrapMode wrapV, float mipmapBias, AssetIntegrated onLoaded)
	{
		if (Manager != null && !(bitmap.Buffer is SharedMemoryBlockLease<byte>))
		{
			throw new ArgumentException("Bitmap Buffer must use shared memory when rendering is active");
		}
		if (hint.region.HasValue && (hint.region.Value.width < 0 || hint.region.Value.height < 0))
		{
			throw new ArgumentException($"Invalid hint region on Texture2D upload: {hint.region}\n{this}");
		}
		Data = bitmap;
		UpdateData(null, bitmap);
		if (Manager != null)
		{
			SetTexture2DProperties setTexture2DProperties = new SetTexture2DProperties();
			setTexture2DProperties.assetId = base.AssetId;
			setTexture2DProperties.filterMode = filterMode;
			setTexture2DProperties.anisoLevel = anisoLevel;
			setTexture2DProperties.wrapU = wrapU;
			setTexture2DProperties.wrapV = wrapV;
			setTexture2DProperties.mipmapBias = mipmapBias;
			setTexture2DProperties.applyImmediatelly = false;
			base.RenderSystem.SendAssetUpdate(setTexture2DProperties);
			SetTexture2DFormat setTexture2DFormat = new SetTexture2DFormat();
			setTexture2DFormat.assetId = base.AssetId;
			setTexture2DFormat.width = bitmap.Size.x;
			setTexture2DFormat.height = bitmap.Size.y;
			setTexture2DFormat.mipmapCount = bitmap.MipMapLevels;
			setTexture2DFormat.format = bitmap.Format;
			setTexture2DFormat.profile = bitmap.Profile;
			FireIntegratedOnResultType = TextureUpdateResultType.FormatSet | TextureUpdateResultType.DataUpload;
			_assetIntegrated = onLoaded;
			base.RenderSystem.SendAssetUpdate(setTexture2DFormat);
			SetTexture2DData setTexture2DData = new SetTexture2DData();
			setTexture2DData.assetId = base.AssetId;
			setTexture2DData.startMipLevel = 0;
			setTexture2DData.hint = hint;
			AssignUploadLayout(bitmap, setTexture2DData);
			base.RenderSystem.SendAssetUpdate(setTexture2DData);
		}
		else
		{
			onLoaded(environmentInstanceChanged: false);
		}
	}

	public void UpdateProperties(TextureFilterMode filterMode, int anisoLevel, TextureWrapMode wrapU, TextureWrapMode wrapV, float mipmapBias, AssetIntegrated onDone)
	{
		if (Manager != null)
		{
			SetTexture2DProperties setTexture2DProperties = new SetTexture2DProperties();
			setTexture2DProperties.assetId = base.AssetId;
			setTexture2DProperties.filterMode = filterMode;
			setTexture2DProperties.anisoLevel = anisoLevel;
			setTexture2DProperties.wrapU = wrapU;
			setTexture2DProperties.wrapV = wrapV;
			setTexture2DProperties.mipmapBias = mipmapBias;
			setTexture2DProperties.applyImmediatelly = true;
			FireIntegratedOnResultType = TextureUpdateResultType.PropertiesSet;
			_assetIntegrated = onDone;
			base.RenderSystem.SendAssetUpdate(setTexture2DProperties);
		}
		else
		{
			onDone(environmentInstanceChanged: false);
		}
	}

	public void HandleResult(SetTexture2DResult result)
	{
		_instanceChanged |= result.instanceChanged;
		int num = Interlocked.And(ref _fireOnIntegratedResultTypeFlags, (int)(~result.type));
		if (_fireOnIntegratedResultTypeFlags == 0 && num == (int)result.type)
		{
			bool environmentInstanceChanged = Interlocked.Exchange(ref _instanceChanged, value: false);
			Interlocked.Exchange(ref _assetIntegrated, null)?.Invoke(environmentInstanceChanged);
		}
	}

	private Bitmap2D EnsureRendererCompatible(Bitmap2D bitmap, IBackingBufferAllocator allocator)
	{
		if (!base.RenderSystem.HasRenderer)
		{
			return bitmap;
		}
		if ((bitmap.Size > base.RenderSystem.MaxTextureSize).Any())
		{
			UniLog.Warning($"Texture {base.AssetURL} size is larger than supported by platform: {bitmap.Size}. Resizing to {base.RenderSystem.MaxTextureSize}");
			Bitmap2D rescaled = bitmap.GetRescaled(base.RenderSystem.MaxTextureSize, null, forceRescale: false, Filtering.Box, allocator);
			bitmap.Buffer.Dispose();
			bitmap = rescaled;
		}
		if (!bitmap.FlipY)
		{
			bitmap.FlipYInMemory();
		}
		if (!base.RenderSystem.SupportsTextureFormat(bitmap.Format))
		{
			TextureFormat? textureFormat = bitmap.Format.FindCompatibleFormat(base.RenderSystem.SupportsTextureFormat);
			if (!textureFormat.HasValue)
			{
				throw new InvalidOperationException($"Can't find compatible format for {bitmap.Format}");
			}
			UniLog.Warning($"Asset {base.AssetURL} is in incompatible format: {bitmap.Format}. Converting to: {textureFormat.Value}");
			Bitmap2D bitmap2D = bitmap.ConvertTo(textureFormat.Value, allocator);
			bitmap.Buffer.Dispose();
			bitmap = bitmap2D;
		}
		return bitmap;
	}

	protected override Bitmap2D DecodeFile(string file, Texture2DVariantDescriptor descriptor, bool forceMips, bool loadingOriginal, bool sharedMemory)
	{
		if (Manager == null)
		{
			sharedMemory = false;
		}
		object obj;
		if (!sharedMemory)
		{
			obj = null;
		}
		else
		{
			IBackingBufferAllocator renderSystem = base.RenderSystem;
			obj = renderSystem;
		}
		IBackingBufferAllocator allocator = (IBackingBufferAllocator)obj;
		base.ActualLoadedVariant = descriptor?.VariantIdentifier ?? base.ActualLoadedVariant;
		if (descriptor != null && descriptor.OriginalAsset)
		{
			descriptor = null;
		}
		switch (descriptor?.TextureCompression ?? TextureCompression.RawRGBA)
		{
		case TextureCompression.BC1_Crunched:
		case TextureCompression.BC3_Crunched:
		case TextureCompression.BC3nm_Crunched:
		case TextureCompression.BC1_Crunched_Non_Perceptual:
		case TextureCompression.BC3_Crunched_Non_Perceptual:
		case TextureCompression.ETC2_RGB_Crunched:
		case TextureCompression.ETC2_RGBA8_Crunched:
			return Bitmap2D.LoadCRN(file, allocator);
		case TextureCompression.BC1_LZMA:
		case TextureCompression.BC3_LZMA:
		case TextureCompression.BC3nm_LZMA:
		case TextureCompression.BC4_LZMA:
		case TextureCompression.BC6H_LZMA:
		case TextureCompression.BC7_LZMA:
		case TextureCompression.ETC2_RGB_LZMA:
		case TextureCompression.ETC2_RGBA8_LZMA:
		case TextureCompression.ASTC_4x4_LZMA:
		case TextureCompression.ASTC_5x5_LZMA:
		case TextureCompression.ASTC_6x6_LZMA:
		case TextureCompression.ASTC_8x8_LZMA:
		case TextureCompression.ASTC_10x10_LZMA:
		case TextureCompression.ASTC_12x12_LZMA:
			return Bitmap2D.LoadRaw(file, allocator);
		case TextureCompression.RawRGBA:
		{
			Elements.Assets.AlphaHandling alphaHandling = ((!base.RenderSystem.IsGPUTexturePOTByteAligned && BitmapMetadata != null) ? ((BitmapMetadata.AlphaData != AlphaChannelData.FullyOpaque) ? Elements.Assets.AlphaHandling.ForceRGB : Elements.Assets.AlphaHandling.ForceRGBA) : Elements.Assets.AlphaHandling.ForceRGBA);
			Bitmap2D bitmap;
			try
			{
				bitmap = TextureDecoder.Decode(file, forceMips, alphaHandling, int.MaxValue, 1f, allocator);
				bitmap = EnsureRendererCompatible(bitmap, allocator);
				if (descriptor != null && loadingOriginal)
				{
					if (descriptor.ColorPreprocess == ColorPreprocess.sRGB)
					{
						bitmap.ConvertTosRGB();
					}
					else if (descriptor.ColorPreprocess == ColorPreprocess.HDRsRGB)
					{
						bitmap.ConvertHDRToHDRsRGB();
					}
					if (descriptor.AlphaPreprocess == AlphaPreprocess.sRGB)
					{
						bitmap.AdjustAlphaGamma(2.2f);
					}
				}
			}
			catch (Exception value2)
			{
				UniLog.Warning($"Exception decoding texture:\n{value2}");
				bitmap = null;
			}
			return bitmap ?? TextureDecoder.ERRORTEXTURE;
		}
		case TextureCompression.RawRGBAHalf:
		{
			Bitmap2D bitmap;
			try
			{
				bitmap = TextureDecoder.Decode(file, forceMips, Elements.Assets.AlphaHandling.KeepOriginal, int.MaxValue, 1f, allocator);
				bitmap = EnsureRendererCompatible(bitmap, allocator);
				if (descriptor != null && loadingOriginal)
				{
					if (descriptor.ColorPreprocess == ColorPreprocess.sRGB)
					{
						bitmap.ConvertTosRGB();
					}
					else if (descriptor.ColorPreprocess == ColorPreprocess.HDRsRGB)
					{
						bitmap.ConvertHDRToHDRsRGB();
					}
					if (descriptor.AlphaPreprocess == AlphaPreprocess.sRGB)
					{
						bitmap.AdjustAlphaGamma(2.2f);
					}
				}
			}
			catch (Exception value)
			{
				UniLog.Warning($"Exception decoding texture:\n{value}");
				bitmap = null;
			}
			return bitmap ?? TextureDecoder.ERRORTEXTURE;
		}
		default:
			throw new Exception("Invalid Texture Compression: " + descriptor.TextureCompression);
		}
	}

	protected override bool UpdateData(Texture2DVariantDescriptor variant, Bitmap2D bitmap, int? overrideMips = null)
	{
		int num;
		if ((object)variant == null)
		{
			num = 0;
		}
		else
		{
			num = (variant.Readable ? 1 : 0);
			if (num != 0)
			{
				Data = bitmap;
			}
		}
		Size = bitmap.Size;
		MipMapCount = overrideMips ?? bitmap.MipMapLevels;
		base.Format = bitmap.Format;
		base.Profile = variant?.Profile ?? bitmap.Profile;
		return (byte)num != 0;
	}

	protected override DecodeFormat GetDecodeFormat(Texture2DVariantDescriptor variant)
	{
		return new Texture<Texture2D, Texture2DVariantDescriptor, Elements.Assets.Texture2DVariantDescriptor, BitmapMetadata, Bitmap2D>.DecodeFormat
		{
			miplevels = variant.MipMapCount,
			format = variant.TextureCompression.ToFormat(BitmapMetadata, base.IsThreeChannelSupported),
			dimensions = new int3(variant.Width, variant.Height),
			profile = variant.Profile
		};
	}

	protected override DecodeFormat GetDecodeFormat(Bitmap2D bitmap, Texture2DVariantDescriptor variant)
	{
		Texture<Texture2D, Texture2DVariantDescriptor, Elements.Assets.Texture2DVariantDescriptor, BitmapMetadata, Bitmap2D>.DecodeFormat result = new Texture<Texture2D, Texture2DVariantDescriptor, Elements.Assets.Texture2DVariantDescriptor, BitmapMetadata, Bitmap2D>.DecodeFormat
		{
			miplevels = bitmap.MipMapLevels,
			format = bitmap.Format,
			dimensions = bitmap.Size
		};
		if ((object)variant != null && variant.OriginalAsset)
		{
			result.profile = bitmap.Profile;
		}
		else
		{
			result.profile = variant?.Profile ?? bitmap.Profile;
		}
		return result;
	}

	protected override Texture2DVariantDescriptor UpdateVariantFromAlternate(Texture2DVariantDescriptor variant, Elements.Assets.Texture2DVariantDescriptor alternate)
	{
		return new Texture2DVariantDescriptor(variant.CorrespondingAssetType, variant.FilterMode, variant.AnisotropicLevel, variant.WrapModeU, variant.WrapModeV, variant.WrapModeW, 0f, alternate.TextureCompression, alternate.CompressionQuality, variant.Width, variant.Height, variant.MipMaps, alternate.Filtering, variant.Profile, variant.ColorPreprocess, variant.AlphaPreprocess);
	}

	protected override Texture2DVariantDescriptor GetOriginalVersionVariant(Texture2DVariantDescriptor variant)
	{
		return new Texture2DVariantDescriptor(variant.CorrespondingAssetType, variant.FilterMode, variant.AnisotropicLevel, variant.WrapModeU, variant.WrapModeV, variant.WrapModeW, variant.MipMaps, 0f, readable: false, variant.Profile, variant.ColorPreprocess, variant.AlphaPreprocess);
	}

	protected override async Task InitializeTexture(Texture2DVariantDescriptor variant, DecodeFormat decodeFormat)
	{
		TaskCompletionSource<bool> taskCompletionSource = new TaskCompletionSource<bool>();
		if (Manager != null)
		{
			SetTexture2DProperties setTexture2DProperties = new SetTexture2DProperties();
			setTexture2DProperties.assetId = base.AssetId;
			setTexture2DProperties.filterMode = variant.FilterMode;
			setTexture2DProperties.anisoLevel = variant.AnisotropicLevel;
			setTexture2DProperties.wrapU = variant.WrapModeU;
			setTexture2DProperties.wrapV = variant.WrapModeV;
			setTexture2DProperties.mipmapBias = variant.MipMapBias;
			setTexture2DProperties.applyImmediatelly = false;
			base.RenderSystem.SendAssetUpdate(setTexture2DProperties);
			SetTexture2DFormat setTexture2DFormat = new SetTexture2DFormat();
			setTexture2DFormat.assetId = base.AssetId;
			setTexture2DFormat.width = decodeFormat.dimensions.x;
			setTexture2DFormat.height = decodeFormat.dimensions.y;
			setTexture2DFormat.mipmapCount = decodeFormat.miplevels;
			setTexture2DFormat.format = decodeFormat.format;
			setTexture2DFormat.profile = decodeFormat.profile;
			FireIntegratedOnResultType = TextureUpdateResultType.FormatSet;
			_assetIntegrated = taskCompletionSource.SetResult;
			base.RenderSystem.SendAssetUpdate(setTexture2DFormat);
		}
		else
		{
			taskCompletionSource.SetResult(result: true);
		}
		await taskCompletionSource.Task.ConfigureAwait(continueOnCapturedContext: false);
	}

	protected override TaskCompletionSource<bool> UploadTextureData(Bitmap2D decoded, int startMipLevel)
	{
		TaskCompletionSource<bool> taskCompletionSource = new TaskCompletionSource<bool>();
		TextureUploadHint hint = new TextureUploadHint
		{
			readable = false
		};
		if (Manager != null)
		{
			SetTexture2DData setTexture2DData = new SetTexture2DData();
			setTexture2DData.assetId = base.AssetId;
			setTexture2DData.startMipLevel = startMipLevel;
			setTexture2DData.hint = hint;
			AssignUploadLayout(decoded, setTexture2DData);
			FireIntegratedOnResultType = TextureUpdateResultType.DataUpload;
			_assetIntegrated = taskCompletionSource.SetResult;
			base.RenderSystem.SendAssetUpdate(setTexture2DData);
		}
		else
		{
			taskCompletionSource.SetResult(result: true);
		}
		return taskCompletionSource;
	}

	protected override async Task UpdateTextureProperties(Texture2DVariantDescriptor variant)
	{
		TaskCompletionSource<bool> taskCompletionSource = new TaskCompletionSource<bool>();
		if (Manager != null)
		{
			SetTexture2DProperties setTexture2DProperties = new SetTexture2DProperties();
			setTexture2DProperties.assetId = base.AssetId;
			setTexture2DProperties.filterMode = variant.FilterMode;
			setTexture2DProperties.anisoLevel = variant.AnisotropicLevel;
			setTexture2DProperties.wrapU = variant.WrapModeU;
			setTexture2DProperties.wrapV = variant.WrapModeV;
			setTexture2DProperties.mipmapBias = variant.MipMapBias;
			setTexture2DProperties.applyImmediatelly = true;
			FireIntegratedOnResultType = TextureUpdateResultType.PropertiesSet;
			_assetIntegrated = taskCompletionSource.SetResult;
			base.RenderSystem.SendAssetUpdate(setTexture2DProperties);
		}
		else
		{
			taskCompletionSource.SetResult(result: false);
		}
		await taskCompletionSource.Task.ConfigureAwait(continueOnCapturedContext: false);
	}

	public override void Unload()
	{
		_assetIntegrated = null;
		if (Manager != null)
		{
			UnloadTexture2D unloadTexture2D = new UnloadTexture2D();
			unloadTexture2D.assetId = base.AssetId;
			base.RenderSystem.SendAssetUpdate(unloadTexture2D);
		}
		Data = null;
		base.Unload();
	}

	private static void AssignUploadLayout(Bitmap2D bitmap, SetTexture2DData data)
	{
		data.flipY = bitmap.FlipY;
		data.mipMapSizes = new List<RenderVector2i>();
		data.mipStarts = new List<int>();
		for (int i = 0; i < bitmap.MipMapLevels; i++)
		{
			data.mipMapSizes.Add(bitmap.MipMapSize(i));
			data.mipStarts.Add(bitmap.MipmapOrigin(i));
		}
		SharedMemoryBlockLease<byte> sharedMemoryBlockLease = (SharedMemoryBlockLease<byte>)bitmap.Buffer;
		data.data = sharedMemoryBlockLease.Descriptor;
	}
}
