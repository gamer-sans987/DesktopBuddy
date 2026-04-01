using System;
using Elements.Core;
using Renderite.Shared;

namespace FrooxEngine;

[Category(new string[] { "Assets/Materials" })]
public class PBS_Metallic : PBS_Material, IPBS_Metallic, IPBS_Material, IAssetProvider<Material>, IAssetProvider, IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable
{
	[Range(0f, 1f, "0.##")]
	public readonly Sync<float> Metallic;

	[Range(0f, 1f, "0.##")]
	public readonly Sync<float> Smoothness;

	public readonly AssetRef<ITexture2D> MetallicMap;

	private static bool _propertyInitializationState;

	private static MaterialProperty _Metallic = new MaterialProperty("_Metallic");

	private static MaterialProperty _Glossiness = new MaterialProperty("_Glossiness");

	private static MaterialProperty _MetallicGlossMap = new MaterialProperty("_MetallicGlossMap");

	protected override Uri ShaderURL => OfficialAssets.Shaders.PBSMetallic;

	float IPBS_Metallic.Metallic
	{
		get
		{
			return Metallic;
		}
		set
		{
			Metallic.Value = value;
		}
	}

	float IPBS_Metallic.Smoothness
	{
		get
		{
			return Smoothness;
		}
		set
		{
			Smoothness.Value = value;
		}
	}

	IAssetProvider<ITexture2D> IPBS_Metallic.MetallicMap
	{
		get
		{
			return MetallicMap.Target;
		}
		set
		{
			MetallicMap.Target = value;
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

	protected override void UpdateKeywords(ShaderKeywords keywords)
	{
		base.UpdateKeywords(keywords);
		keywords.UpdateKeyword("_METALLICGLOSSMAP", MetallicMap);
	}

	protected override void UpdateMaterial(ref MaterialUpdateWriter writer)
	{
		writer.UpdateFloat(_Metallic, Metallic);
		writer.UpdateFloat(_Glossiness, Smoothness);
		writer.UpdateTexture(_MetallicGlossMap, MetallicMap, ColorProfile.sRGB, ColorProfileRequirement.Default);
		base.UpdateMaterial(ref writer);
	}

	protected override void OnAwake()
	{
		base.OnAwake();
		Smoothness.Value = 0.25f;
		Metallic.Value = 0f;
	}

	protected override void OnLoading(DataTreeNode node, LoadControl control)
	{
		base.OnLoading(node, control);
		if (!control.GetFeatureFlag("ColorManagement").HasValue)
		{
			control.OnLoaded(this, delegate
			{
				control.Convert<LegacyLinearFloatColorFieldAdapter, float>(Metallic);
			});
		}
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		Metallic = new Sync<float>();
		Smoothness = new Sync<float>();
		MetallicMap = new AssetRef<ITexture2D>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => HighPriorityIntegration, 
			4 => _shader, 
			5 => TextureScale, 
			6 => TextureOffset, 
			7 => DetailTextureScale, 
			8 => DetailTextureOffset, 
			9 => AlbedoColor, 
			10 => AlbedoTexture, 
			11 => EmissiveColor, 
			12 => EmissiveMap, 
			13 => NormalScale, 
			14 => NormalMap, 
			15 => HeightMap, 
			16 => HeightScale, 
			17 => OcclusionMap, 
			18 => DetailAlbedoTexture, 
			19 => DetailNormalMap, 
			20 => DetailNormalScale, 
			21 => BlendMode, 
			22 => AlphaCutoff, 
			23 => OffsetFactor, 
			24 => OffsetUnits, 
			25 => RenderQueue, 
			26 => Metallic, 
			27 => Smoothness, 
			28 => MetallicMap, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static PBS_Metallic __New()
	{
		return new PBS_Metallic();
	}
}
