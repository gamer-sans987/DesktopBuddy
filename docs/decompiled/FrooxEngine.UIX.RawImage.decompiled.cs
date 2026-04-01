using System;
using System.Threading.Tasks;
using Elements.Assets;
using Elements.Core;
using Elements.Data;
using Renderite.Shared;

namespace FrooxEngine.UIX;

[Category(new string[] { "UIX/Graphics" })]
[OldTypeName("FrooxEngine.UI.RawImage", null)]
public class RawImage : Graphic
{
	public readonly AssetRef<ITexture2D> Texture;

	public readonly AssetRef<Material> Material;

	public readonly Sync<colorX> Tint;

	public readonly Sync<Rect> UVRect;

	public readonly Sync<RectOrientation> Orientation;

	public readonly Sync<bool> PreserveAspect;

	public readonly Sync<bool> InteractionTarget;

	private colorX _tint;

	private Rect _uvRect;

	private RectOrientation _orientation;

	private bool _preserveAspect;

	private IAssetProvider<ITexture2D> _textureProvider;

	private ITexture2D _texture;

	private IAssetProvider<Material> _material;

	public override bool RequiresPreGraphicsCompute => false;

	public override ValueTask PreGraphicsCompute()
	{
		throw new NotSupportedException();
	}

	protected override void OnAwake()
	{
		base.OnAwake();
		Tint.Value = colorX.White;
		UVRect.Value = new Rect(float2.Zero, float2.One);
		InteractionTarget.Value = true;
	}

	protected override void FlagChanges(RectTransform rect)
	{
		rect.MarkChangeDirty();
	}

	public override void ComputeGraphic(GraphicsChunk.RenderData renderData)
	{
		Rect localComputeRect = base.RectTransform.LocalComputeRect;
		if (!(localComputeRect.width <= 0f) && !(localComputeRect.height <= 0f))
		{
			TriangleSubmesh submesh = renderData.GetSubmesh(_material, _textureProvider, ImageBase.TextureMaterialMapper);
			GenerateImage(renderData.Mesh, submesh, localComputeRect, _uvRect, _orientation, _texture, _preserveAspect, in _tint);
		}
	}

	public override bool IsPointInside(in float2 point)
	{
		if (!InteractionTarget.Value)
		{
			return false;
		}
		return true;
	}

	public override void PrepareCompute()
	{
		_textureProvider = Texture.Target;
		_texture = _textureProvider?.Asset;
		_material = Material.Target;
		_preserveAspect = PreserveAspect;
		_tint = Tint;
		_uvRect = UVRect;
		_orientation = Orientation;
	}

	public void UseDefaultMaterial()
	{
		Material.Target = null;
	}

	public void UseOpaqueMaterial()
	{
		Material.Target = base.World.GetDefaultOpaqueUI_Unlit();
	}

	public void UseNormalMapMaterial()
	{
		Material.Target = base.World.GetDefaultNormalUI_Unlit();
	}

	public static int GenerateImage(MeshX mesh, TriangleSubmesh submesh, Rect rect, Rect uvRect, RectOrientation uvOrientation, ITexture2D texture, bool preserveAspect, in colorX color)
	{
		return GenerateImage(mesh, submesh, rect, uvRect, uvOrientation, texture, preserveAspect, in color, in color, in color, in color);
	}

	public static int GenerateImage(MeshX mesh, TriangleSubmesh submesh, Rect rect, Rect uvRect, RectOrientation uvOrientation, ITexture2D texture, bool preserveAspect, in colorX colorTopLeft, in colorX colorTopRight, in colorX colorBottomRight, in colorX colorBottomLeft)
	{
		if (texture != null && preserveAspect)
		{
			float2 v = (float2)(texture?.Size ?? int2.One) * uvRect.size;
			v = ((!(v <= 0f).Any()) ? (v / MathX.MaxComponent(in v)) : float2.One);
			float num = MathX.MaxComponent(in rect.size);
			float2 a = rect.size / num;
			if (v.x > a.x)
			{
				v *= a.x / v.x;
			}
			if (v.y > a.y)
			{
				v *= a.y / v.y;
			}
			float2 v2 = a - v;
			ref float2 position = ref rect.position;
			position += v2 * 0.5f * num;
			rect.size = v * num;
		}
		mesh.IncreaseVertexCount(4);
		int num2 = mesh.VertexCount - 4;
		int num3 = num2 + 1;
		int num4 = num2 + 2;
		int num5 = num2 + 3;
		for (int i = 0; i < 4; i++)
		{
			int num6 = num2 + i;
			mesh.RawPositions[num6] = rect.GetExtent(i);
			mesh.RawUV0s[num6] = uvRect.GetExtent(i, uvOrientation);
		}
		mesh.RawColors[num2] = colorBottomLeft.ToProfile(mesh.Profile);
		mesh.RawColors[num3] = colorTopLeft.ToProfile(mesh.Profile);
		mesh.RawColors[num4] = colorTopRight.ToProfile(mesh.Profile);
		mesh.RawColors[num5] = colorBottomRight.ToProfile(mesh.Profile);
		submesh.AddQuadAsTriangles(num2, num3, num4, num5);
		return num2;
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		Texture = new AssetRef<ITexture2D>();
		Material = new AssetRef<Material>();
		Tint = new Sync<colorX>();
		UVRect = new Sync<Rect>();
		Orientation = new Sync<RectOrientation>();
		PreserveAspect = new Sync<bool>();
		InteractionTarget = new Sync<bool>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => Texture, 
			4 => Material, 
			5 => Tint, 
			6 => UVRect, 
			7 => Orientation, 
			8 => PreserveAspect, 
			9 => InteractionTarget, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static RawImage __New()
	{
		return new RawImage();
	}
}
