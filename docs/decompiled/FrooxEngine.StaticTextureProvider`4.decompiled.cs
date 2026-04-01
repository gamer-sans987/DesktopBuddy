using System;
using System.Threading.Tasks;
using Elements.Assets;
using Elements.Core;
using FrooxEngine.UIX;
using FrooxEngine.Undo;
using Renderite.Shared;

namespace FrooxEngine;

public abstract class StaticTextureProvider<A, B, M, D> : StaticAssetProvider<A, M, D>, IAssetProvider<ITexture>, IAssetProvider, IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable, ITextureProvider, ICustomInspector where A : Asset, ITexture, new() where B : Bitmap where M : ImageMetadataBase, new() where D : class, IEngineTextureVariantDescriptor
{
	public readonly Sync<TextureFilterMode?> FilterMode;

	public readonly Sync<int?> AnisotropicLevel;

	public readonly Sync<bool> Uncompressed;

	public readonly Sync<bool> DirectLoad;

	public readonly Sync<bool> ForceExactVariant;

	public readonly Sync<TextureCompression?> PreferredFormat;

	public readonly Sync<ColorProfile?> PreferredProfile;

	[Range(-1f, 1f, "0.00")]
	public readonly Sync<float> MipMapBias;

	protected abstract bool UseCrunchCompression { get; }

	protected abstract bool UseNormalMap { get; }

	protected override bool FetchMetadata => !DirectLoad.Value;

	ITexture IAssetProvider<ITexture>.Asset => Asset;

	protected override Uri ProcessURL(Uri assetURL)
	{
		if (assetURL != null)
		{
			assetURL = base.Cloud.Assets.FilterDatabaseURL(assetURL);
		}
		return base.ProcessURL(assetURL);
	}

	protected override void OnAwake()
	{
		base.OnAwake();
		FilterMode.Value = null;
		AnisotropicLevel.Value = null;
		PreferredProfile.Value = null;
	}

	protected abstract bool UpdateTextureVariantDescriptor(M metadata, ref D descriptor, TextureCompression compression, int quality, ColorProfile? profile, ColorPreprocess colorPreprocess, AlphaPreprocess alphaPreprocess, bool exact);

	protected override async ValueTask<D> UpdateVariantDescriptor(M metadata, D descriptor)
	{
		TextureCompression compression = TextureCompression.RawRGBA;
		ColorProfile? colorProfile = PreferredProfile;
		bool useCrunchCompression = UseCrunchCompression;
		bool flag = ForceExactVariant.Value;
		bool flag2 = false;
		if (metadata != null)
		{
			if (metadata.ColorData == ColorChannelData.ColorHDR && !colorProfile.HasValue)
			{
				flag2 = true;
			}
			if (Uncompressed.Value)
			{
				compression = (metadata.ColorData.IsHDR() ? TextureCompression.RawRGBAHalf : TextureCompression.RawRGBA);
				flag = true;
			}
			else
			{
				switch (base.Engine.SystemInfo.Platform)
				{
				case Platform.Windows:
				case Platform.Linux:
					compression = ((!metadata.ColorData.IsHDR()) ? ((!UseNormalMap) ? (((ColorProfile?)PreferredProfile != ColorProfile.Linear) ? ((metadata.AlphaData != AlphaChannelData.FullyOpaque) ? (useCrunchCompression ? TextureCompression.BC3_Crunched : TextureCompression.BC3_LZMA) : (useCrunchCompression ? TextureCompression.BC1_Crunched : TextureCompression.BC1_LZMA)) : ((metadata.AlphaData != AlphaChannelData.FullyOpaque) ? (useCrunchCompression ? TextureCompression.BC3_Crunched_Non_Perceptual : TextureCompression.BC3_LZMA) : (useCrunchCompression ? TextureCompression.BC1_Crunched_Non_Perceptual : TextureCompression.BC1_LZMA))) : (useCrunchCompression ? TextureCompression.BC3nm_Crunched : TextureCompression.BC3nm_LZMA)) : TextureCompression.BC6H_LZMA);
					break;
				case Platform.Android:
					compression = (metadata.ColorData.IsHDR() ? TextureCompression.RawRGBAHalf : ((metadata.AlphaData != AlphaChannelData.FullyOpaque) ? (useCrunchCompression ? TextureCompression.ETC2_RGBA8_Crunched : TextureCompression.ETC2_RGBA8_LZMA) : (useCrunchCompression ? TextureCompression.ETC2_RGB_Crunched : TextureCompression.ETC2_RGB_LZMA)));
					break;
				}
			}
		}
		if (PreferredFormat.Value.HasValue && base.Engine.RenderSystem.SupportsTextureFormat(PreferredFormat.Value.Value.ToFormat()))
		{
			TextureCompression? textureCompression = PreferredFormat.Value;
			if (!flag)
			{
				if ((textureCompression == TextureCompression.BC3nm_Crunched || textureCompression == TextureCompression.BC3nm_LZMA) && !UseNormalMap)
				{
					textureCompression = null;
				}
				else if (textureCompression.Value.ToFormat().IsHDR())
				{
					if (metadata != null && !metadata.ColorData.IsHDR())
					{
						textureCompression = null;
					}
				}
			}
			if (textureCompression.HasValue)
			{
				compression = textureCompression.Value;
			}
			if (textureCompression.Value.ToFormat().IsHDR())
			{
				flag2 = true;
			}
		}
		int recommendedQuality = compression.GetRecommendedQuality();
		if (UseNormalMap || flag2)
		{
			colorProfile = ColorProfile.Linear;
		}
		else if (!colorProfile.HasValue)
		{
			colorProfile = ColorProfile.sRGB;
		}
		ColorPreprocess colorPreprocess = ColorPreprocess.None;
		AlphaPreprocess alphaPreprocess = AlphaPreprocess.None;
		if (colorProfile == ColorProfile.sRGBAlpha)
		{
			alphaPreprocess = AlphaPreprocess.sRGB;
		}
		if ((colorProfile == ColorProfile.sRGBAlpha || colorProfile == ColorProfile.sRGB) && metadata != null && metadata.ColorData.IsHDR())
		{
			colorPreprocess = ColorPreprocess.sRGB;
		}
		if (UpdateTextureVariantDescriptor(metadata, ref descriptor, compression, recommendedQuality, colorProfile, colorPreprocess, alphaPreprocess, flag))
		{
			return descriptor;
		}
		return null;
	}

	public override void BuildInspectorUI(UIBuilder ui)
	{
		base.BuildInspectorUI(ui);
		ui.Button("Inspector.Texture.InvertRGB".AsLocaleKey(), InvertRGB);
		ui.Button("Inspector.Texture.InvertR".AsLocaleKey(), InvertR);
		ui.Button("Inspector.Texture.InvertG".AsLocaleKey(), InvertG);
		ui.Button("Inspector.Texture.InvertB".AsLocaleKey(), InvertB);
		ui.Button("Inspector.Texture.InvertA".AsLocaleKey(), InvertA);
		ui.Button("Inspector.Texture.ColorToAlphaWhite".AsLocaleKey(), ColorToAlphaWhite);
		ui.Button("Inspector.Texture.ColorToAlphaBlack".AsLocaleKey(), ColorToAlphaBlack);
		ui.Button("Inspector.Texture.AlphaFromIntensity".AsLocaleKey(), AlphaFromIntensity);
		ui.Button("Inspector.Texture.AlphaToMask".AsLocaleKey(), AlphaToMask);
		ui.Button("Inspector.Texture.RemoveAlpha".AsLocaleKey(), RemoveAlpha);
		ui.Button("Inspector.Texture.ConvertToGrayscaleAverage".AsLocaleKey(), GrayscaleAverage);
		ui.Button("Inspector.Texture.ConvertToGrayscaleLuminance".AsLocaleKey(), GrayscaleLuminance);
		ui.Button("Inspector.Texture.SwapRG".AsLocaleKey(), SwapRG);
		ui.Button("Inspector.Texture.SwapRB".AsLocaleKey(), SwapRB);
		ui.Button("Inspector.Texture.SwapRA".AsLocaleKey(), SwapRA);
		ui.Button("Inspector.Texture.SwapGB".AsLocaleKey(), SwapGB);
		ui.Button("Inspector.Texture.SwapGA".AsLocaleKey(), SwapGA);
		ui.Button("Inspector.Texture.SwapBA".AsLocaleKey(), SwapBA);
		ui.Button("Inspector.Texture.IsolateR".AsLocaleKey(), IsolateR);
		ui.Button("Inspector.Texture.IsolateG".AsLocaleKey(), IsolateG);
		ui.Button("Inspector.Texture.IsolateB".AsLocaleKey(), IsolateB);
		ui.Button("Inspector.Texture.IsolateA".AsLocaleKey(), IsolateA);
		ui.Button("Inspector.Texture.AddWhiteBackground".AsLocaleKey(), AddWhiteBackground);
		ui.Button("Inspector.Texture.AddBlackBackground".AsLocaleKey(), AddBlackBackground);
		ui.Button("Inspector.Texture.NormalizeMaxOnly".AsLocaleKey(), NormalizeMaxOnly);
		ui.Button("Inspector.Texture.NormalizeMinMax".AsLocaleKey(), NormalizeMinMax);
		ui.Button("Inspector.Texture.NormalizeIndependent".AsLocaleKey(), NormalizeIndependent);
		ui.Button("Inspector.Texture.InvalidFloats".AsLocaleKey(), InvalidFloats);
		ui.HorizontalLayout(4f);
		ui.Text("Inspector.Texture.Hue".AsLocaleKey());
		FloatTextEditorParser floatTextEditorParser = ui.FloatField();
		floatTextEditorParser.ParsedValue.Value = 0.1f;
		ui.ButtonRef("Inspector.Texture.ShiftHue".AsLocaleKey(), (colorX?)null, ShiftHue, floatTextEditorParser);
		ui.NestOut();
		ui.HorizontalLayout(4f);
		ui.Text("Inspector.Texture.Saturation".AsLocaleKey());
		FloatTextEditorParser argument = ui.FloatField();
		floatTextEditorParser.ParsedValue.Value = 0.1f;
		ui.ButtonRef("Inspector.Texture.AdjustSaturation".AsLocaleKey(), (colorX?)null, AdjustSaturation, argument);
		ui.NestOut();
		ui.HorizontalLayout(4f);
		ui.Text("Inspector.Texture.Value".AsLocaleKey());
		FloatTextEditorParser argument2 = ui.FloatField();
		floatTextEditorParser.ParsedValue.Value = 0.1f;
		ui.ButtonRef("Inspector.Texture.AdjustValue".AsLocaleKey(), (colorX?)null, AdjustValue, argument2);
		ui.NestOut();
		ui.HorizontalLayout(4f);
		ui.Text("Inspector.Texture.Gamma".AsLocaleKey());
		FloatTextEditorParser floatTextEditorParser2 = ui.FloatField(float.MinValue, float.MaxValue, 4);
		floatTextEditorParser2.ParsedValue.Value = 2.2f;
		ui.ButtonRef("Inspector.Texture.AdjustGamma".AsLocaleKey(), AdjustGamma, floatTextEditorParser2);
		ui.NestOut();
		ui.HorizontalLayout(4f);
		ui.Text("Inspector.Texture.AlphaGamma".AsLocaleKey());
		FloatTextEditorParser floatTextEditorParser3 = ui.FloatField(float.MinValue, float.MaxValue, 4);
		floatTextEditorParser3.ParsedValue.Value = 2.2f;
		ui.ButtonRef("Inspector.Texture.AdjustAlphaGamma".AsLocaleKey(), (colorX?)null, AdjustAlphaGamma, floatTextEditorParser3);
		ui.NestOut();
		ui.HorizontalLayout(4f);
		ui.Text("Inspector.Texture.AddAlpha".AsLocaleKey());
		FloatTextEditorParser floatTextEditorParser4 = ui.FloatField(float.MinValue, float.MaxValue, 4);
		floatTextEditorParser4.ParsedValue.Value = 0.1f;
		ui.ButtonRef("Inspector.Texture.AddAlpha".AsLocaleKey(), (colorX?)null, AddAlpha, floatTextEditorParser4);
		ui.NestOut();
	}

	protected abstract Task<B> GetOriginalTextureData();

	protected abstract Task<Uri> SaveBitmapData(B bitmap);

	public Task<bool> ProcessPixels(Func<color, color> pixelProcess)
	{
		return ProcessPixels(pixelProcess, null);
	}

	public Task<bool> ProcessPixels(Func<color, color> pixelProcess, IButton button)
	{
		Func<B, B> processFunc = delegate(B tex)
		{
			tex.ForEachPixel(pixelProcess);
			return tex;
		};
		return Process(processFunc, button);
	}

	public Task<bool> ProcessBitmap(Func<B, B> processFunc)
	{
		return Process(processFunc, null);
	}

	public Task<bool> Process(Func<B, B> processFunc, IButton button)
	{
		return StartGlobalTask(async () => await ProcessAsync(processFunc, button));
	}

	public Task<bool> Process(Action<B> processFunc, IButton button)
	{
		return StartGlobalTask(async () => await ProcessAsync(delegate(B t)
		{
			processFunc(t);
			return t;
		}, button));
	}

	private async Task<bool> ProcessAsync(Func<B, B> processFunc, IButton button)
	{
		if (URL.Value == null)
		{
			return false;
		}
		while (Asset == null)
		{
			await default(NextUpdate);
		}
		string _description = button?.LabelText;
		if (button != null)
		{
			button.LabelText = this.GetLocalized("General.Processing");
			button.Enabled = false;
		}
		Uri uri;
		try
		{
			await default(ToBackground);
			B bitmap = processFunc(await GetOriginalTextureData());
			uri = await SaveBitmapData(bitmap);
		}
		catch (Exception ex)
		{
			UniLog.Error($"Exception processing texture {URL.Value}:\n" + ex);
			await default(ToWorld);
			if (button != null && !button.IsDestroyed)
			{
				button.LabelText = "<color=#f00>Error! Check log.</color>";
			}
			throw;
		}
		await default(ToWorld);
		if (button != null && !button.IsDestroyed)
		{
			button.LabelText = _description;
			button.Enabled = true;
		}
		if (uri == null)
		{
			return false;
		}
		if (button != null)
		{
			base.World.BeginUndoBatch(_description);
			URL.UndoableSet(uri, forceNew: true);
			base.World.EndUndoBatch();
		}
		else
		{
			URL.Value = uri;
		}
		return true;
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> InvertRGB()
	{
		return Process(delegate(B t)
		{
			t.InvertRGB();
		}, null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> InvertR()
	{
		return Process(delegate(B t)
		{
			t.InvertR();
		}, null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> InvertG()
	{
		return Process(delegate(B t)
		{
			t.InvertG();
		}, null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> InvertB()
	{
		return Process(delegate(B t)
		{
			t.InvertB();
		}, null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> InvertA()
	{
		return Process(delegate(B t)
		{
			t.InvertA();
		}, null);
	}

	[SyncMethod(typeof(Func<colorX, Task<bool>>), new string[] { })]
	public Task<bool> ColorToAlpha(colorX fillColor)
	{
		return Process(delegate(B t)
		{
			t.ColorToAlpha(fillColor.ToProfile(t.Profile));
		}, null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> AlphaFromIntensity()
	{
		return Process(delegate(B t)
		{
			t.AlphaFromIntensity();
		}, null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> AlphaToMask()
	{
		return Process(delegate(B t)
		{
			t.AlphaToMask();
		}, null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> RemoveAlpha()
	{
		return Process(delegate(B t)
		{
			t.RemoveAlpha();
		}, null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> GrayscaleAverage()
	{
		return Process(delegate(B t)
		{
			t.GrayscaleAverage();
		}, null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> GrayscaleLuminance()
	{
		return Process(delegate(B t)
		{
			t.GrayscaleLuminance();
		}, null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> SwapRG()
	{
		return Process(delegate(B t)
		{
			t.SwapRG();
		}, null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> SwapRB()
	{
		return Process(delegate(B t)
		{
			t.SwapRB();
		}, null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> SwapRA()
	{
		return Process(delegate(B t)
		{
			t.SwapRA();
		}, null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> SwapGB()
	{
		return Process(delegate(B t)
		{
			t.SwapGB();
		}, null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> SwapGA()
	{
		return Process(delegate(B t)
		{
			t.SwapGA();
		}, null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> SwapBA()
	{
		return Process(delegate(B t)
		{
			t.SwapBA();
		}, null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> IsolateR()
	{
		return Process(delegate(B t)
		{
			t.IsolateR();
		}, null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> IsolateG()
	{
		return Process(delegate(B t)
		{
			t.IsolateG();
		}, null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> IsolateB()
	{
		return Process(delegate(B t)
		{
			t.IsolateB();
		}, null);
	}

	[SyncMethod(typeof(Func<Task<bool>>), new string[] { })]
	public Task<bool> IsolateA()
	{
		return Process(delegate(B t)
		{
			t.IsolateA();
		}, null);
	}

	[SyncMethod(typeof(Func<colorX, Task<bool>>), new string[] { })]
	public Task<bool> AddBackground(colorX color)
	{
		return Process(delegate(B t)
		{
			t.AddBackground(color.ToProfile(t.Profile));
		}, null);
	}

	[SyncMethod(typeof(Func<float, Task<bool>>), new string[] { })]
	public Task<bool> AdjustGamma(float gamma)
	{
		return Process(delegate(B t)
		{
			t.AdjustGamma(gamma);
		}, null);
	}

	[SyncMethod(typeof(Func<float, Task<bool>>), new string[] { })]
	public Task<bool> AdjustAlphaGamma(float gamma)
	{
		return Process(delegate(B t)
		{
			t.AdjustAlphaGamma(gamma);
		}, null);
	}

	[SyncMethod(typeof(Func<float, Task<bool>>), new string[] { })]
	public Task<bool> ShiftHue(float offset)
	{
		return Process(delegate(B t)
		{
			t.ShiftHue(offset);
		}, null);
	}

	[SyncMethod(typeof(Func<float, Task<bool>>), new string[] { })]
	public Task<bool> SetHue(float hue)
	{
		return Process(delegate(B t)
		{
			t.SetHue(hue);
		}, null);
	}

	[SyncMethod(typeof(Func<float, Task<bool>>), new string[] { })]
	public Task<bool> SetSaturation(float saturation)
	{
		return Process(delegate(B t)
		{
			t.SetSaturation(saturation);
		}, null);
	}

	[SyncMethod(typeof(Func<float, Task<bool>>), new string[] { })]
	public Task<bool> OffsetSaturation(float offset)
	{
		return Process(delegate(B t)
		{
			t.OffsetSaturation(offset);
		}, null);
	}

	[SyncMethod(typeof(Func<float, Task<bool>>), new string[] { })]
	public Task<bool> MulSaturation(float ratio)
	{
		return Process(delegate(B t)
		{
			t.MulSaturation(ratio);
		}, null);
	}

	[SyncMethod(typeof(Func<float, Task<bool>>), new string[] { })]
	public Task<bool> SetValue(float value)
	{
		return Process(delegate(B t)
		{
			t.SetValue(value);
		}, null);
	}

	[SyncMethod(typeof(Func<float, Task<bool>>), new string[] { })]
	public Task<bool> MulValue(float ratio)
	{
		return Process(delegate(B t)
		{
			t.MulValue(ratio);
		}, null);
	}

	[SyncMethod(typeof(Func<float, Task<bool>>), new string[] { })]
	public Task<bool> OffsetValue(float offset)
	{
		return Process(delegate(B t)
		{
			t.OffsetValue(offset);
		}, null);
	}

	[SyncMethod(typeof(Func<float, Task<bool>>), new string[] { })]
	public Task<bool> OffsetAlpha(float offset)
	{
		return Process(delegate(B t)
		{
			t.OffsetAlpha(offset);
		}, null);
	}

	[SyncMethod(typeof(Func<bool, bool, bool, Task<bool>>), new string[] { })]
	public Task<bool> Normalize(bool rgbIndependently, bool normalizeAlpha, bool normalizeMinValue)
	{
		return Process(delegate(B t)
		{
			t.Normalize(rgbIndependently, normalizeAlpha, normalizeMinValue);
		}, null);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void InvertRGB(IButton button, ButtonEventData eventData)
	{
		Process(delegate(B t)
		{
			t.InvertRGB();
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void InvertR(IButton button, ButtonEventData eventData)
	{
		Process(delegate(B t)
		{
			t.InvertR();
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void InvertG(IButton button, ButtonEventData eventData)
	{
		Process(delegate(B t)
		{
			t.InvertG();
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void InvertB(IButton button, ButtonEventData eventData)
	{
		Process(delegate(B t)
		{
			t.InvertB();
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void InvertA(IButton button, ButtonEventData eventData)
	{
		Process(delegate(B t)
		{
			t.InvertA();
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void ColorToAlphaWhite(IButton button, ButtonEventData eventData)
	{
		Process(delegate(B t)
		{
			t.ColorToAlpha(color.White);
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void ColorToAlphaBlack(IButton button, ButtonEventData eventData)
	{
		Process(delegate(B t)
		{
			t.ColorToAlpha(color.Black);
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void AlphaFromIntensity(IButton button, ButtonEventData eventData)
	{
		Process(delegate(B t)
		{
			t.AlphaFromIntensity();
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void AlphaToMask(IButton button, ButtonEventData eventData)
	{
		Process(delegate(B t)
		{
			t.AlphaToMask();
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void RemoveAlpha(IButton button, ButtonEventData eventData)
	{
		Process(delegate(B t)
		{
			t.RemoveAlpha();
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void GrayscaleAverage(IButton button, ButtonEventData eventData)
	{
		Process(delegate(B t)
		{
			t.GrayscaleAverage();
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void GrayscaleLuminance(IButton button, ButtonEventData eventData)
	{
		Process(delegate(B t)
		{
			t.GrayscaleLuminance();
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void SwapRG(IButton button, ButtonEventData eventData)
	{
		Process(delegate(B t)
		{
			t.SwapRG();
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void SwapRB(IButton button, ButtonEventData eventData)
	{
		Process(delegate(B t)
		{
			t.SwapRB();
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void SwapRA(IButton button, ButtonEventData eventData)
	{
		Process(delegate(B t)
		{
			t.SwapRA();
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void SwapGB(IButton button, ButtonEventData eventData)
	{
		Process(delegate(B t)
		{
			t.SwapGB();
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void SwapGA(IButton button, ButtonEventData eventData)
	{
		Process(delegate(B t)
		{
			t.SwapGA();
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void SwapBA(IButton button, ButtonEventData eventData)
	{
		Process(delegate(B t)
		{
			t.SwapBA();
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void IsolateR(IButton button, ButtonEventData eventData)
	{
		Process(delegate(B t)
		{
			t.IsolateR();
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void IsolateG(IButton button, ButtonEventData eventData)
	{
		Process(delegate(B t)
		{
			t.IsolateG();
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void IsolateB(IButton button, ButtonEventData eventData)
	{
		Process(delegate(B t)
		{
			t.IsolateB();
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void IsolateA(IButton button, ButtonEventData eventData)
	{
		Process(delegate(B t)
		{
			t.IsolateA();
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void AddWhiteBackground(IButton button, ButtonEventData eventData)
	{
		Process(delegate(B t)
		{
			t.AddBackground(color.White);
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void AddBlackBackground(IButton button, ButtonEventData eventData)
	{
		Process(delegate(B t)
		{
			t.AddBackground(color.Black);
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void AdjustGamma(IButton button, ButtonEventData eventData, FloatTextEditorParser field)
	{
		float _gamma = field.ParsedValue.Value;
		Process(delegate(B t)
		{
			t.AdjustGamma(_gamma);
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void InvalidFloats(IButton button, ButtonEventData eventData)
	{
		ProcessPixels(delegate(color c)
		{
			float4 @float = c;
			return (@float.IsInfinity || @float.IsNaN) ? new color(1f) : new color(0f);
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void NormalizeMaxOnly(IButton button, ButtonEventData eventData)
	{
		Process(delegate(B tex)
		{
			tex.Normalize();
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void NormalizeMinMax(IButton button, ButtonEventData eventData)
	{
		Process(delegate(B tex)
		{
			tex.Normalize(rgbIndependently: false, normalizeAlpha: false, normalizeMinValue: true);
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void NormalizeIndependent(IButton button, ButtonEventData eventData)
	{
		Process(delegate(B tex)
		{
			tex.Normalize(rgbIndependently: true, normalizeAlpha: false, normalizeMinValue: true);
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void ShiftHue(IButton button, ButtonEventData eventData, FloatTextEditorParser field)
	{
		float _shift = field.ParsedValue.Value;
		Process(delegate(B tex)
		{
			tex.ShiftHue(_shift);
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void AdjustSaturation(IButton button, ButtonEventData eventData, FloatTextEditorParser field)
	{
		float _mul = field.ParsedValue.Value;
		Process(delegate(B tex)
		{
			tex.MulSaturation(_mul);
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void AdjustValue(IButton button, ButtonEventData eventData, FloatTextEditorParser field)
	{
		float _mul = field.ParsedValue.Value;
		Process(delegate(B tex)
		{
			tex.MulValue(_mul);
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void AdjustAlphaGamma(IButton button, ButtonEventData eventData, FloatTextEditorParser field)
	{
		float _gamma = field.ParsedValue.Value;
		Process(delegate(B tex)
		{
			tex.AdjustAlphaGamma(_gamma);
		}, button);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void AddAlpha(IButton button, ButtonEventData eventData, FloatTextEditorParser field)
	{
		float _value = field.ParsedValue.Value;
		Process(delegate(B tex)
		{
			tex.OffsetAlpha(_value);
		}, button);
	}

	protected override void OnLoading(DataTreeNode node, LoadControl control)
	{
		base.OnLoading(node, control);
		if (control.GetFeatureFlag("TEXTURE_QUALITY").HasValue)
		{
			return;
		}
		control.OnLoaded(this, delegate
		{
			if (this is StaticTexture2D && !FilterMode.IsDriven && FilterMode.Value != TextureFilterMode.Point)
			{
				FilterMode.Value = null;
				AnisotropicLevel.Value = null;
			}
		});
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		FilterMode = new Sync<TextureFilterMode?>();
		AnisotropicLevel = new Sync<int?>();
		Uncompressed = new Sync<bool>();
		DirectLoad = new Sync<bool>();
		ForceExactVariant = new Sync<bool>();
		PreferredFormat = new Sync<TextureCompression?>();
		PreferredProfile = new Sync<ColorProfile?>();
		MipMapBias = new Sync<float>();
	}
}
