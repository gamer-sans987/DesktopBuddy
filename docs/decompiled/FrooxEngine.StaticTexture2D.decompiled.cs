using System;
using System.Text.Json;
using System.Threading.Tasks;
using Elements.Assets;
using Elements.Core;
using FrooxEngine.UIX;
using FrooxEngine.Undo;
using Renderite.Shared;

namespace FrooxEngine;

[Category(new string[] { "Assets" })]
public class StaticTexture2D : StaticTextureProvider<Texture2D, Bitmap2D, BitmapMetadata, Texture2DVariantDescriptor>, ITexture2DProvider, IAssetProvider<ITexture2D>, IAssetProvider, IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable, ITextureProvider, IAssetProvider<ITexture>, ICustomInspector
{
	public readonly Sync<bool> IsNormalMap;

	public readonly Sync<TextureWrapMode> WrapModeU;

	public readonly Sync<TextureWrapMode> WrapModeV;

	public readonly Sync<float> PowerOfTwoAlignThreshold;

	public readonly Sync<bool> CrunchCompressed;

	public readonly Sync<int?> MinSize;

	public readonly Sync<int?> MaxSize;

	public readonly Sync<bool> MipMaps;

	public readonly Sync<bool> KeepOriginalMipMaps;

	public readonly Sync<Filtering> MipMapFilter;

	public readonly Sync<bool> Readable;

	public override EngineAssetClass AssetClass => EngineAssetClass.Texture2D;

	protected override bool UseCrunchCompression => CrunchCompressed.Value;

	protected override bool UseNormalMap => IsNormalMap.Value;

	public TextureWrapMode WrapMode
	{
		get
		{
			return WrapModeU.Value;
		}
		set
		{
			WrapModeU.Value = value;
			WrapModeV.Value = value;
		}
	}

	ITexture2D IAssetProvider<ITexture2D>.Asset => Asset;

	protected override void OnAwake()
	{
		base.OnAwake();
		MipMaps.Value = true;
		KeepOriginalMipMaps.Value = false;
		MipMapFilter.Value = Filtering.Box;
		CrunchCompressed.Value = true;
		WrapModeU.Value = TextureWrapMode.Repeat;
		WrapModeV.Value = TextureWrapMode.Repeat;
		PowerOfTwoAlignThreshold.Value = 0.05f;
	}

	protected override bool UpdateTextureVariantDescriptor(BitmapMetadata metadata, ref Texture2DVariantDescriptor descriptor, TextureCompression compression, int quality, ColorProfile? profile, ColorPreprocess colorPreprocess, AlphaPreprocess alphaPreprocess, bool exact)
	{
		int2 @int = int2.Zero;
		if (metadata != null)
		{
			if (metadata.IsCorrupted)
			{
				if (descriptor != null)
				{
					descriptor = null;
					return true;
				}
				return false;
			}
			int2 maxSize = new int2(metadata.Width, metadata.Height);
			@int = maxSize;
			@int = TextureVariantDescriptor<Elements.Assets.Texture2DVariantDescriptor, BitmapMetadata>.GetMaxLimitedSize(in @int);
			if (MaxSize.Value.HasValue)
			{
				int maxSize2 = MathX.NearestPowerOfTwo(MathX.Max(MaxSize.Value.Value, 128));
				@int = TextureVariantDescriptor<Elements.Assets.Texture2DVariantDescriptor, BitmapMetadata>.GetMaxLimitedSize(in @int, maxSize2);
			}
			int num = MathX.MaxComponent(in @int);
			int? maxTextureSize = base.AssetManager.GetMaxTextureSize(num);
			if (maxTextureSize.HasValue)
			{
				@int = MathX.RoundToInt((double)maxTextureSize.Value / (double)num * @int);
			}
			if (MinSize.Value.HasValue)
			{
				@int = TextureVariantDescriptor<Elements.Assets.Texture2DVariantDescriptor, BitmapMetadata>.GetMinLimitedSize(in @int, in maxSize, MinSize.Value.Value);
			}
			if (PowerOfTwoAlignThreshold.Value > 0f)
			{
				float2 @float = @int * (float)PowerOfTwoAlignThreshold;
				int2 b = MathX.NearestPowerOfTwo(in @int);
				if ((MathX.Abs(@int - b) <= @float).All())
				{
					@int = b;
				}
			}
			if (!MathX.IsPowerOfTwo(@int.x) || !MathX.IsPowerOfTwo(@int.y))
			{
				Span<int2> span = stackalloc int2[metadata.MipMapCount];
				int2 size = maxSize;
				int num2 = metadata.MipMapCount - 1;
				for (int i = 0; i < metadata.MipMapCount; i++)
				{
					span[i] = size;
					if (Elements.Assets.Texture2DVariantDescriptor.IsMergedMip(in size))
					{
						num2 = i;
						break;
					}
					size >>= 1;
				}
				int2 int2 = -int2.One;
				int num3 = 0;
				for (int j = 0; j <= num2; j++)
				{
					int2 int3 = MathX.Abs(span[j] - @int);
					int num4 = int3.x + int3.y;
					if (int2.x < 0 || num4 < num3)
					{
						int2 = span[j];
						num3 = num4;
					}
				}
				@int = int2;
			}
			@int = Bitmap2DBase.AlignSize(in @int, compression.ToFormat());
		}
		bool value = MipMaps.Value;
		float num5 = MipMapBias.Value;
		Filtering? filtering = MipMapFilter.Value;
		bool flag = KeepOriginalMipMaps.Value;
		if (!value)
		{
			num5 = 0f;
			flag = false;
		}
		if (flag && metadata != null && metadata.MipMapCount > 1)
		{
			filtering = null;
		}
		TextureFilterMode textureFilterMode = FilterMode.Value ?? base.AssetManager.TextureSettings?.DefaultFilterMode.Value ?? TextureFilterMode.Bilinear;
		int num6 = AnisotropicLevel.Value ?? base.AssetManager.TextureSettings?.AnisotropicLevel.Value ?? 1;
		if (compression.IsHDR())
		{
			profile = ColorProfile.Linear;
		}
		if (descriptor == null || descriptor.Width != @int.x || descriptor.Height != @int.y || descriptor.TextureCompression != compression || descriptor.FilterMode != (TextureFilterMode?)FilterMode || descriptor.RequireExactVariant != exact || descriptor.MipMaps != value || descriptor.Filtering != filtering || descriptor.ColorPreprocess != colorPreprocess || descriptor.AlphaPreprocess != alphaPreprocess || !MathX.Approximately(descriptor.MipMapBias, num5) || (textureFilterMode == TextureFilterMode.Anisotropic && descriptor.AnisotropicLevel != num6) || WrapModeU.Value != descriptor.WrapModeU || WrapModeV.Value != descriptor.WrapModeV || Readable.Value != descriptor.Readable || profile.Value != descriptor.Profile)
		{
			if (metadata == null || (bool)DirectLoad)
			{
				descriptor = new Texture2DVariantDescriptor(typeof(Texture2D), textureFilterMode, num6, WrapModeU, WrapModeV, TextureWrapMode.Repeat, value, num5, Readable.Value, profile.Value, colorPreprocess, alphaPreprocess);
			}
			else
			{
				descriptor = new Texture2DVariantDescriptor(typeof(Texture2D), textureFilterMode, num6, WrapModeU, WrapModeV, TextureWrapMode.Repeat, num5, compression, quality, @int.x, @int.y, value, filtering, profile.Value, colorPreprocess, alphaPreprocess, exact, Readable.Value);
			}
			return true;
		}
		return false;
	}

	protected override Task<Bitmap2D> GetOriginalTextureData()
	{
		return Asset.GetOriginalTextureData();
	}

	protected override Task<Uri> SaveBitmapData(Bitmap2D bitmap)
	{
		return base.Engine.LocalDB.SaveAssetAsync(bitmap);
	}

	public override void BuildInspectorUI(UIBuilder ui)
	{
		base.BuildInspectorUI(ui);
		ui.PushStyle();
		ui.Style.MinHeight = 128f;
		ui.Panel();
		ui.HorizontalLayout(4f);
		ui.PopStyle();
		ui.Image(this);
		Texture2DAssetMetadata texture2DAssetMetadata = ui.Root.AttachComponent<Texture2DAssetMetadata>();
		texture2DAssetMetadata.Texture.Target = this;
		ui.Text("Inspector.Texture.Size".AsLocaleKey(("width", texture2DAssetMetadata.Width), ("height", texture2DAssetMetadata.Height)));
		ui.Text("Inspector.Texture.Format".AsLocaleKey(("format", texture2DAssetMetadata.Format), ("memory", texture2DAssetMetadata.FormattedMemoryBytes), ("profile", texture2DAssetMetadata.Profile)));
		ui.NestOut();
		ui.NestOut();
		ui.Text("Inspector.Texture.Variant".AsLocaleKey("variant", texture2DAssetMetadata.ActualLoadedVariant));
		ui.Button("Inspector.Texture.ReplaceFromClipboard".AsLocaleKey(), ReplaceFromClipboard);
		ui.HorizontalLayout(4f);
		ui.Text("Inspector.Texture.MakeTileable".AsLocaleKey());
		FloatTextEditorParser floatTextEditorParser = ui.FloatField();
		floatTextEditorParser.ParsedValue.Value = 0.1f;
		ui.NestOut();
		ui.HorizontalLayout(4f);
		ui.ButtonRef("Inspector.Texture.TileLoop".AsLocaleKey(), (colorX?)null, TileLoop, floatTextEditorParser);
		ui.ButtonRef("Inspector.Texture.TileMirror".AsLocaleKey(), (colorX?)null, TileMirror, floatTextEditorParser);
		ui.NestOut();
		ui.HorizontalLayout(4f);
		ui.Text("Inspector.Texture.LongestSide".AsLocaleKey());
		IntTextEditorParser intTextEditorParser = ui.IntegerField(4, 16384);
		intTextEditorParser.ParsedValue.Value = 1024;
		ui.ButtonRef("Inspector.Texture.Resize".AsLocaleKey(), (colorX?)null, Resize, intTextEditorParser);
		ui.NestOut();
		ui.Button("Inspector.Texture.BleedColorToAlpha".AsLocaleKey(), BleedColorToAlpha);
		ui.Button("Inspector.Texture.FlipHorizontal".AsLocaleKey(), FlipHorizontal);
		ui.Button("Inspector.Texture.FlipVertical".AsLocaleKey(), FlipVertical);
		ui.Button("Inspector.Texture.RotateCW".AsLocaleKey(), Rotate90CW);
		ui.Button("Inspector.Texture.RotateCCW".AsLocaleKey(), Rotate90CCW);
		ui.Button("Inspector.Texture.Rotate180".AsLocaleKey(), Rotate180);
		ui.Button("Inspector.Texture.TrimTransparent".AsLocaleKey(), TrimTransparent);
		ui.Button("Inspector.Texture.TrimByCornerColor".AsLocaleKey(), TrimByCornerColor);
		ui.Button("Inspector.Texture.MakeSquare".AsLocaleKey(), MakeSquare);
		ui.Button("Inspector.Texture.ToNearestPOT".AsLocaleKey(), ToNearestPOT);
		ui.Button("Inspector.Texture.GenerateMetadata".AsLocaleKey(), GenerateBitmapMetadata);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> BleedColorToAlpha()
	{
		return Process(delegate(Bitmap2D t)
		{
			t.BleedColorToAlpha();
		}, null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> FlipHorizontal()
	{
		return Process(delegate(Bitmap2D t)
		{
			t.FlipHorizontal();
		}, null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> FlipVertical()
	{
		return Process(delegate(Bitmap2D t)
		{
			t.FlipVertical();
		}, null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> Rotate90CW()
	{
		return Process((Bitmap2D t) => t.Rotate90CW(), null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> Rotate90CCW()
	{
		return Process((Bitmap2D t) => t.Rotate90CCW(), null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> Rotate180()
	{
		return Process((Bitmap2D t) => t.Rotate180(), null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> MakeSquare()
	{
		return Process((Bitmap2D t) => t.MakeSquare(), null);
	}

	[SyncMethod(typeof(Func<float2, Task<bool>>), new string[] { })]
	public Task<bool> TileLoop(float2 transition)
	{
		return Process((Bitmap2D t) => t.GenerateTileableLoop(transition), null);
	}

	[SyncMethod(typeof(Func<float2, Task<bool>>), new string[] { })]
	public Task<bool> TileMirror(float2 transition)
	{
		return Process((Bitmap2D t) => t.GenerateTileableMirrored(transition), null);
	}

	[SyncMethod(typeof(Func<int, Filtering, Task<bool>>), new string[] { })]
	public Task<bool> Rescale(int size, Filtering filtering)
	{
		return Process(delegate(Bitmap2D t)
		{
			int longestSize = size;
			Filtering filtering2 = filtering;
			return t.GetRescaled(longestSize, null, forceRescale: false, filtering2);
		}, null);
	}

	[SyncMethod(typeof(Func<int2, Filtering, Task<bool>>), new string[] { })]
	public Task<bool> Rescale(int2 size, Filtering filtering)
	{
		return Process(delegate(Bitmap2D t)
		{
			int2 size2 = size;
			Filtering filtering2 = filtering;
			return t.GetRescaled(size2, null, forceRescale: false, filtering2);
		}, null);
	}

	[SyncMethod(typeof(Func<int2, int2, Task<bool>>), new string[] { })]
	public Task<bool> Crop(int2 position, int2 size)
	{
		return Process((Bitmap2D t) => t.Crop(position, size), null);
	}

	[SyncMethod(typeof(Func<color, Task<bool>>), new string[] { })]
	public Task<bool> Trim(color color)
	{
		return Process((Bitmap2D t) => t.Trim(color), null);
	}

	[SyncMethod(typeof(Func<color32, Task<bool>>), new string[] { })]
	public Task<bool> Trim(color32 color)
	{
		return Process((Bitmap2D t) => t.Trim(color), null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> TrimTransparent()
	{
		return Process((Bitmap2D t) => t.TrimTransparent(), null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> TrimByCornerColor()
	{
		return Process((Bitmap2D t) => t.TrimByCornerColor(), null);
	}

	[SyncMethod(typeof(Func<float, Task<bool>>), new string[] { })]
	public Task<bool> LuminanceThreshold(float threshold)
	{
		return Process(delegate(Bitmap2D t)
		{
			t.LuminanceThreshold(threshold);
		}, null);
	}

	[SyncMethod(typeof(Func<float, color, color, Task<bool>>), new string[] { })]
	public Task<bool> LuminanceThreshold(float threshold, color above, color below)
	{
		return Process(delegate(Bitmap2D t)
		{
			t.LuminanceThreshold(threshold, above, below);
		}, null);
	}

	[SyncMethod(typeof(Func<float, int, Task<bool>>), new string[] { })]
	public Task<bool> LocalizedLuminanceThreshold(float threshold, int range)
	{
		return Process(delegate(Bitmap2D t)
		{
			t.LocalizedLuminanceThreshold(threshold, range);
		}, null);
	}

	[SyncMethod(typeof(Func<float, int, color, color, Task<bool>>), new string[] { })]
	public Task<bool> LocalizedLuminanceThreshold(float threshold, int range, color above, color below)
	{
		return Process(delegate(Bitmap2D t)
		{
			t.LocalizedLuminanceThreshold(threshold, range, above, below);
		}, null);
	}

	[SyncMethod(typeof(Func<int, float, int, float, Task<bool>>), new string[] { })]
	public Task<bool> KMeansCluster(int k, float positionWeight, int batchSize = 1024, float passesOverData = 1f)
	{
		return Process(delegate(Bitmap2D t)
		{
			t.KMeansCluster(k, positionWeight, batchSize, passesOverData);
		}, null);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void TileLoop(IButton button, ButtonEventData eventData, FloatTextEditorParser field)
	{
		float _transition = field.ParsedValue.Value;
		Process((Bitmap2D b) => b.GenerateTileableLoop(float2.One * _transition), button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void TileMirror(IButton button, ButtonEventData eventData, FloatTextEditorParser field)
	{
		float _transition = field.ParsedValue.Value;
		Process((Bitmap2D b) => b.GenerateTileableMirrored(float2.One * _transition), button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void Resize(IButton button, ButtonEventData eventData, IntTextEditorParser field)
	{
		int _longestSide = field.ParsedValue.Value;
		Process((Bitmap2D b) => b.GetRescaled(_longestSide, false), button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void BleedColorToAlpha(IButton button, ButtonEventData eventData)
	{
		Process(delegate(Bitmap2D tex)
		{
			tex.BleedColorToAlpha();
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void FlipHorizontal(IButton button, ButtonEventData eventData)
	{
		Process(delegate(Bitmap2D tex)
		{
			tex.FlipHorizontal();
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void FlipVertical(IButton button, ButtonEventData eventData)
	{
		Process(delegate(Bitmap2D tex)
		{
			tex.FlipVertical();
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void Rotate90CW(IButton button, ButtonEventData eventData)
	{
		Process((Bitmap2D tex) => tex.Rotate90CW(), button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void Rotate90CCW(IButton button, ButtonEventData eventData)
	{
		Process((Bitmap2D tex) => tex.Rotate90CCW(), button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void Rotate180(IButton button, ButtonEventData eventData)
	{
		Process((Bitmap2D tex) => tex.Rotate180(), button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void TrimTransparent(IButton button, ButtonEventData eventData)
	{
		Process((Bitmap2D tex) => tex.TrimTransparent(), button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void TrimByCornerColor(IButton button, ButtonEventData eventData)
	{
		Process((Bitmap2D tex) => tex.TrimByCornerColor(), button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void MakeSquare(IButton button, ButtonEventData eventData)
	{
		Process((Bitmap2D tex) => tex.MakeSquare(), button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void ToNearestPOT(IButton button, ButtonEventData eventData)
	{
		Process((Bitmap2D tex) => tex.GetRescaled(MathX.RoundToInt(MathX.Pow(2f, MathX.Round(MathX.Log((float2)tex.Size, 2f)))), false), button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void GenerateBitmapMetadata(IButton button, ButtonEventData eventData)
	{
		StartTask(async delegate
		{
			await default(ToBackground);
			BitmapMetadata metadata = BitmapMetadata.GenerateMetadata(await base.Asset.GetOriginalTextureData().ConfigureAwait(continueOnCapturedContext: false));
			await default(ToWorld);
			Slot slot = base.World.AddSlot("Texture Metadata");
			UniversalImporter.SpawnText(slot, "Texture Metadata", JsonSerializer.Serialize(metadata));
			slot.PositionInFrontOfUser(float3.Backward);
		});
	}

	[SyncMethod(typeof(Delegate), null)]
	private void ReplaceFromClipboard(IButton button, ButtonEventData eventData)
	{
		if (!base.InputInterface.IsClipboardSupported)
		{
			return;
		}
		button.Enabled = false;
		button.LabelTextField.SetLocalized("General.Processing".AsLocaleKey());
		StartTask(async delegate
		{
			try
			{
				await default(ToBackground);
				if (!base.InputInterface.Clipboard.ContainsImage)
				{
					return;
				}
				Bitmap2D bitmap2D = await base.InputInterface.Clipboard.GetImage();
				if (bitmap2D == null)
				{
					return;
				}
				Uri uri = await base.Engine.LocalDB.SaveAssetAsync(bitmap2D).ConfigureAwait(continueOnCapturedContext: false);
				await default(ToWorld);
				URL.UndoableSet(uri, forceNew: true);
			}
			finally
			{
				await default(ToWorld);
				if (!button.IsDestroyed)
				{
					button.Enabled = true;
					button.LabelTextField.SetLocalized("Inspector.Texture.ReplaceFromClipboard");
				}
			}
		});
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		IsNormalMap = new Sync<bool>();
		WrapModeU = new Sync<TextureWrapMode>();
		WrapModeV = new Sync<TextureWrapMode>();
		PowerOfTwoAlignThreshold = new Sync<float>();
		CrunchCompressed = new Sync<bool>();
		MinSize = new Sync<int?>();
		MaxSize = new Sync<int?>();
		MipMaps = new Sync<bool>();
		KeepOriginalMipMaps = new Sync<bool>();
		MipMapFilter = new Sync<Filtering>();
		Readable = new Sync<bool>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => URL, 
			4 => FilterMode, 
			5 => AnisotropicLevel, 
			6 => Uncompressed, 
			7 => DirectLoad, 
			8 => ForceExactVariant, 
			9 => PreferredFormat, 
			10 => PreferredProfile, 
			11 => MipMapBias, 
			12 => IsNormalMap, 
			13 => WrapModeU, 
			14 => WrapModeV, 
			15 => PowerOfTwoAlignThreshold, 
			16 => CrunchCompressed, 
			17 => MinSize, 
			18 => MaxSize, 
			19 => MipMaps, 
			20 => KeepOriginalMipMaps, 
			21 => MipMapFilter, 
			22 => Readable, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static StaticTexture2D __New()
	{
		return new StaticTexture2D();
	}
}
