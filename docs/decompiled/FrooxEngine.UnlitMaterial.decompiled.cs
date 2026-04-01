using System;
using Elements.Core;
using Elements.Data;
using Renderite.Shared;

namespace FrooxEngine;

[Category(new string[] { "Assets/Materials/Unlit" })]
public class UnlitMaterial : MaterialProvider, ICommonMaterial, IAssetProvider<Material>, IAssetProvider, IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable, IStereoMaterial, IBillboardMaterial, IBlendModeMaterial, ICullingMaterial
{
	public readonly Sync<colorX> TintColor;

	public readonly AssetRef<ITexture2D> Texture;

	public readonly Sync<float2> TextureScale;

	public readonly Sync<float2> TextureOffset;

	public readonly AssetRef<ITexture2D> MaskTexture;

	public readonly Sync<float2> MaskScale;

	public readonly Sync<float2> MaskOffset;

	[OldName("MaskTextureMode")]
	public readonly Sync<MaskTextureMode> MaskMode;

	public readonly Sync<BlendMode> BlendMode;

	[Range(0f, 1f, "0.##")]
	public readonly Sync<float> AlphaCutoff;

	public readonly Sync<bool> UseVertexColors;

	public readonly Sync<ColorProfile> VertexColorInterpolationSpace;

	public readonly Sync<Sidedness> Sidedness;

	public readonly Sync<ZWrite> ZWrite;

	public readonly AssetRef<ITexture2D> OffsetTexture;

	public readonly Sync<float2> OffsetMagnitude;

	public readonly Sync<float2> OffsetTextureScale;

	public readonly Sync<float2> OffsetTextureOffset;

	public readonly Sync<bool> PolarUVmapping;

	public readonly Sync<float> PolarPower;

	public readonly Sync<bool> StereoTextureTransform;

	public readonly Sync<float2> RightEyeTextureScale;

	public readonly Sync<float2> RightEyeTextureOffset;

	public readonly Sync<bool> DecodeAsNormalMap;

	public readonly Sync<bool> UseBillboardGeometry;

	public readonly Sync<bool> UsePerBillboardScale;

	public readonly Sync<bool> UsePerBillboardRotation;

	public readonly Sync<bool> UsePerBillboardUV;

	public readonly Sync<float2> BillboardSize;

	public readonly Sync<float> OffsetFactor;

	public readonly Sync<float> OffsetUnits;

	[DefaultValue(-1)]
	public readonly Sync<int> RenderQueue;

	[NonDrivable]
	[NonPersistent]
	[DontCopy]
	protected readonly AssetRef<Shader> _unlit;

	[NonDrivable]
	[NonPersistent]
	[DontCopy]
	protected readonly AssetRef<Shader> _unlitBillboard;

	private static bool _propertyInitializationState;

	private static MaterialProperty _Cull = new MaterialProperty("_Cull");

	private static MaterialProperty _Cutoff = new MaterialProperty("_Cutoff");

	private static MaterialProperty _Color = new MaterialProperty("_Color");

	private static MaterialProperty _Tex = new MaterialProperty("_Tex");

	private static MaterialProperty _Tex_ST = new MaterialProperty("_Tex_ST");

	private static MaterialProperty _OffsetTex = new MaterialProperty("_OffsetTex");

	private static MaterialProperty _OffsetMagnitude = new MaterialProperty("_OffsetMagnitude");

	private static MaterialProperty _OffsetTex_ST = new MaterialProperty("_OffsetTex_ST");

	private static MaterialProperty _MaskTex = new MaterialProperty("_MaskTex");

	private static MaterialProperty _MaskTex_ST = new MaterialProperty("_MaskTex_ST");

	private static MaterialProperty _PolarPow = new MaterialProperty("_PolarPow");

	private static MaterialProperty _RightEye_ST = new MaterialProperty("_RightEye_ST");

	private static MaterialProperty _PointSize = new MaterialProperty("_PointSize");

	private static MaterialProperty _OffsetFactor = new MaterialProperty("_OffsetFactor");

	private static MaterialProperty _OffsetUnits = new MaterialProperty("_OffsetUnits");

	private bool? _lastTexture;

	private bool? _lastTextureNormal;

	private bool? _lastMaskMul;

	private bool? _lastMaskClip;

	private bool? _lastColor;

	bool IStereoMaterial.StereoTextureTransform
	{
		get
		{
			return StereoTextureTransform;
		}
		set
		{
			StereoTextureTransform.Value = value;
		}
	}

	float2 IStereoMaterial.LeftEyeTextureScale
	{
		get
		{
			return TextureScale;
		}
		set
		{
			TextureScale.Value = value;
		}
	}

	float2 IStereoMaterial.LeftEyeTextureOffset
	{
		get
		{
			return TextureOffset;
		}
		set
		{
			TextureOffset.Value = value;
		}
	}

	float2 IStereoMaterial.RightEyeTextureScale
	{
		get
		{
			return RightEyeTextureScale;
		}
		set
		{
			RightEyeTextureScale.Value = value;
		}
	}

	float2 IStereoMaterial.RightEyeTextureOffset
	{
		get
		{
			return RightEyeTextureOffset;
		}
		set
		{
			RightEyeTextureOffset.Value = value;
		}
	}

	bool IBillboardMaterial.UseBillboard
	{
		get
		{
			return UseBillboardGeometry;
		}
		set
		{
			UseBillboardGeometry.Value = value;
		}
	}

	bool IBillboardMaterial.PerBillboardColor
	{
		get
		{
			return UseVertexColors;
		}
		set
		{
			UseVertexColors.Value = value;
		}
	}

	bool IBillboardMaterial.PerBillboardSize
	{
		get
		{
			return UsePerBillboardScale;
		}
		set
		{
			UsePerBillboardScale.Value = value;
		}
	}

	bool IBillboardMaterial.PerBillboardRotation
	{
		get
		{
			return UsePerBillboardRotation;
		}
		set
		{
			UsePerBillboardRotation.Value = value;
		}
	}

	bool IBillboardMaterial.PerBillboardUV
	{
		get
		{
			return UsePerBillboardUV;
		}
		set
		{
			UsePerBillboardUV.Value = value;
		}
	}

	float2 IBillboardMaterial.BillboardSize
	{
		get
		{
			return BillboardSize;
		}
		set
		{
			BillboardSize.Value = value;
		}
	}

	Culling ICullingMaterial.Culling
	{
		get
		{
			return Sidedness.Value.GetCulling(BlendMode);
		}
		set
		{
			Sidedness.Value = value.ToSidedness();
		}
	}

	colorX ICommonMaterial.Color
	{
		get
		{
			return TintColor.Value;
		}
		set
		{
			TintColor.Value = value;
		}
	}

	IAssetProvider<ITexture2D> ICommonMaterial.MainTexture
	{
		get
		{
			return Texture.Target;
		}
		set
		{
			Texture.Target = value;
		}
	}

	BlendMode IBlendModeMaterial.BlendMode
	{
		get
		{
			return BlendMode.Value;
		}
		set
		{
			BlendMode.Value = value;
		}
	}

	public override bool PropertiesInitialized
	{
		get
		{
			return _propertyInitializationState;
		}
		protected set
		{
			_propertyInitializationState = value;
		}
	}

	protected override bool UseLateRenderQueue => true;

	float2 ICommonMaterial.TextureScale
	{
		get
		{
			return TextureScale;
		}
		set
		{
			TextureScale.Value = value;
		}
	}

	float2 ICommonMaterial.TextureOffset
	{
		get
		{
			return TextureOffset;
		}
		set
		{
			TextureOffset.Value = value;
		}
	}

	float ICommonMaterial.NormalScale
	{
		get
		{
			return 1f;
		}
		set
		{
		}
	}

	float2 ICommonMaterial.NormalTextureScale
	{
		get
		{
			return float2.One;
		}
		set
		{
		}
	}

	float2 ICommonMaterial.NormalTextureOffset
	{
		get
		{
			return float2.Zero;
		}
		set
		{
		}
	}

	public IAssetProvider<ITexture2D> NormalMap
	{
		get
		{
			return null;
		}
		set
		{
		}
	}

	protected override void OnAwake()
	{
		base.OnAwake();
		TintColor.Value = colorX.White;
		AlphaCutoff.Value = 0.5f;
		TextureScale.Value = float2.One;
		MaskScale.Value = float2.One;
		OffsetTextureScale.Value = float2.One;
		UseVertexColors.Value = true;
		Sidedness.Value = FrooxEngine.Sidedness.Auto;
		RightEyeTextureScale.Value = float2.One;
		PolarPower.Value = 1f;
		BillboardSize.Value = float2.One * 0.005f;
		VertexColorInterpolationSpace.Value = ColorProfile.Linear;
	}

	protected override Shader GetShader()
	{
		if ((bool)UseBillboardGeometry)
		{
			return EnsureSharedShader(_unlitBillboard, OfficialAssets.Shaders.BillboardUnlit).Asset;
		}
		return EnsureSharedShader(_unlit, OfficialAssets.Shaders.Unlit).Asset;
	}

	protected override void OnMaterialReinitialize()
	{
		base.OnMaterialReinitialize();
		_lastTexture = null;
		_lastTextureNormal = null;
		_lastMaskMul = null;
		_lastMaskClip = null;
		_lastColor = null;
	}

	protected override void UpdateKeywords(ShaderKeywords keywords)
	{
		SetBlendModeKeywords(keywords, BlendMode);
		if (BlendMode.WasChanged || keywords.NeedsInitialization)
		{
			keywords.SetKeyword("_MUL_RGB_BY_ALPHA", BlendMode.Value == FrooxEngine.BlendMode.Additive);
		}
		keywords.UpdateKeyword("_VERTEXCOLORS", UseVertexColors);
		if (TintColor.WasChanged || keywords.NeedsInitialization)
		{
			bool state = TintColor.Value != colorX.White;
			keywords.UpdateKeyword("_COLOR", ref _lastColor, state);
		}
		if (Texture.WasChanged || DecodeAsNormalMap.WasChanged || keywords.NeedsInitialization)
		{
			DecodeAsNormalMap.WasChanged = false;
			bool flag = Texture.Target != null;
			keywords.UpdateKeyword("_TEXTURE", ref _lastTexture, flag && !DecodeAsNormalMap);
			keywords.UpdateKeyword("_TEXTURE_NORMALMAP", ref _lastTextureNormal, flag && (bool)DecodeAsNormalMap);
		}
		if (MaskTexture.WasChanged || MaskMode.WasChanged || keywords.NeedsInitialization)
		{
			MaskMode.WasChanged = false;
			bool flag2 = MaskTexture.Target != null;
			keywords.UpdateKeyword("_MASK_TEXTURE_MUL", ref _lastMaskMul, flag2 && MaskMode.Value == MaskTextureMode.MultiplyAlpha);
			keywords.UpdateKeyword("_MASK_TEXTURE_CLIP", ref _lastMaskClip, flag2 && MaskMode.Value == MaskTextureMode.AlphaClip);
		}
		keywords.UpdateKeyword("_OFFSET_TEXTURE", OffsetTexture);
		keywords.UpdateKeyword("_POLARUV", PolarUVmapping);
		keywords.UpdateKeyword("_RIGHT_EYE_ST", StereoTextureTransform);
		if ((bool)UseBillboardGeometry)
		{
			keywords.UpdateKeyword("_POINT_ROTATION", UsePerBillboardRotation);
			keywords.UpdateKeyword("_POINT_SIZE", UsePerBillboardScale);
			keywords.UpdateKeyword("_POINT_UV", UsePerBillboardUV);
		}
		keywords.UpdateVertexColorInterpolationSpace(VertexColorInterpolationSpace);
	}

	protected override void UpdateMaterial(ref MaterialUpdateWriter writer)
	{
		writer.UpdateInstancing(!UseBillboardGeometry, ref _lastInstancing);
		if (BlendMode.WasChanged || Sidedness.WasChanged)
		{
			Sidedness.WasChanged = false;
			writer.SetFloat(_Cull, (float)Sidedness.Value.GetCulling(BlendMode.Value));
		}
		UpdateBlendMode(ref writer, BlendMode, ZWrite, RenderQueue);
		writer.UpdateFloat(_Cutoff, AlphaCutoff);
		writer.UpdateColor(_Color, TintColor);
		MaterialProviderBase<Material>.GetTexture(Texture, base.AssetManager.DarkCheckerTexture);
		writer.UpdateTexture(_Tex, Texture, ColorProfile.sRGB, ColorProfileRequirement.NoChange, base.AssetManager.DarkCheckerTexture);
		writer.UpdateST(_Tex_ST, TextureScale, TextureOffset);
		writer.UpdateTexture(_OffsetTex, OffsetTexture, ColorProfile.Linear, ColorProfileRequirement.Default);
		writer.UpdateST(_OffsetTex_ST, OffsetTextureScale, OffsetTextureOffset);
		writer.UpdateFloat2(_OffsetMagnitude, OffsetMagnitude);
		writer.UpdateFloat(_PolarPow, PolarPower);
		writer.UpdateTexture(_MaskTex, MaskTexture, ColorProfile.sRGB, ColorProfileRequirement.Default);
		writer.UpdateST(_MaskTex_ST, MaskScale, MaskOffset);
		if ((bool)StereoTextureTransform)
		{
			writer.UpdateST(_RightEye_ST, RightEyeTextureScale, RightEyeTextureOffset);
		}
		if ((bool)UseBillboardGeometry)
		{
			writer.UpdateFloat2(_PointSize, BillboardSize);
		}
		writer.UpdateFloat(_OffsetFactor, OffsetFactor);
		writer.UpdateFloat(_OffsetUnits, OffsetUnits);
	}

	public bool IsBlendModeSupported(BlendMode mode)
	{
		return true;
	}

	protected override void OnLoading(DataTreeNode node, LoadControl control)
	{
		base.OnLoading(node, control);
		if (control.GetFeatureFlag("ColorManagement").HasValue)
		{
			return;
		}
		if (BlendMode.Value == FrooxEngine.BlendMode.Alpha || BlendMode.Value == FrooxEngine.BlendMode.Transparent || BlendMode.Value == FrooxEngine.BlendMode.Cutout)
		{
			control.OnLoaded(this, delegate
			{
				if (Texture.Target is StaticTexture2D staticTexture2D)
				{
					staticTexture2D.PreferredProfile.Value = ColorProfile.sRGBAlpha;
				}
			});
		}
		if (BlendMode.Value == FrooxEngine.BlendMode.Additive || BlendMode.Value == FrooxEngine.BlendMode.Multiply)
		{
			control.Convert<LegacyColorAsLinearAdapter, colorX>(TintColor);
		}
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		TintColor = new Sync<colorX>();
		Texture = new AssetRef<ITexture2D>();
		TextureScale = new Sync<float2>();
		TextureOffset = new Sync<float2>();
		MaskTexture = new AssetRef<ITexture2D>();
		MaskScale = new Sync<float2>();
		MaskOffset = new Sync<float2>();
		MaskMode = new Sync<MaskTextureMode>();
		BlendMode = new Sync<BlendMode>();
		AlphaCutoff = new Sync<float>();
		UseVertexColors = new Sync<bool>();
		VertexColorInterpolationSpace = new Sync<ColorProfile>();
		Sidedness = new Sync<Sidedness>();
		ZWrite = new Sync<ZWrite>();
		OffsetTexture = new AssetRef<ITexture2D>();
		OffsetMagnitude = new Sync<float2>();
		OffsetTextureScale = new Sync<float2>();
		OffsetTextureOffset = new Sync<float2>();
		PolarUVmapping = new Sync<bool>();
		PolarPower = new Sync<float>();
		StereoTextureTransform = new Sync<bool>();
		RightEyeTextureScale = new Sync<float2>();
		RightEyeTextureOffset = new Sync<float2>();
		DecodeAsNormalMap = new Sync<bool>();
		UseBillboardGeometry = new Sync<bool>();
		UsePerBillboardScale = new Sync<bool>();
		UsePerBillboardRotation = new Sync<bool>();
		UsePerBillboardUV = new Sync<bool>();
		BillboardSize = new Sync<float2>();
		OffsetFactor = new Sync<float>();
		OffsetUnits = new Sync<float>();
		RenderQueue = new Sync<int>();
		_unlit = new AssetRef<Shader>();
		_unlit.MarkNonPersistent();
		_unlit.MarkNonDrivable();
		_unlitBillboard = new AssetRef<Shader>();
		_unlitBillboard.MarkNonPersistent();
		_unlitBillboard.MarkNonDrivable();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => HighPriorityIntegration, 
			4 => TintColor, 
			5 => Texture, 
			6 => TextureScale, 
			7 => TextureOffset, 
			8 => MaskTexture, 
			9 => MaskScale, 
			10 => MaskOffset, 
			11 => MaskMode, 
			12 => BlendMode, 
			13 => AlphaCutoff, 
			14 => UseVertexColors, 
			15 => VertexColorInterpolationSpace, 
			16 => Sidedness, 
			17 => ZWrite, 
			18 => OffsetTexture, 
			19 => OffsetMagnitude, 
			20 => OffsetTextureScale, 
			21 => OffsetTextureOffset, 
			22 => PolarUVmapping, 
			23 => PolarPower, 
			24 => StereoTextureTransform, 
			25 => RightEyeTextureScale, 
			26 => RightEyeTextureOffset, 
			27 => DecodeAsNormalMap, 
			28 => UseBillboardGeometry, 
			29 => UsePerBillboardScale, 
			30 => UsePerBillboardRotation, 
			31 => UsePerBillboardUV, 
			32 => BillboardSize, 
			33 => OffsetFactor, 
			34 => OffsetUnits, 
			35 => RenderQueue, 
			36 => _unlit, 
			37 => _unlitBillboard, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static UnlitMaterial __New()
	{
		return new UnlitMaterial();
	}
}
