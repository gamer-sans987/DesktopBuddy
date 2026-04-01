using System;
using Elements.Assets;
using Elements.Core;

namespace FrooxEngine;

[Category(new string[] { "Assets/Procedural Textures" })]
public class SolidColorTexture : ProceduralTexture
{
	public readonly Sync<colorX> Color;

	private color color;

	protected override void OnAwake()
	{
		base.OnAwake();
		Color.Value = colorX.Black;
		Size.Value = int2.One * 4;
	}

	protected override void PrepareAssetUpdateData()
	{
		color = Color.Value.ToProfile(Profile);
	}

	protected override void ClearTextureData()
	{
	}

	protected override void UpdateTextureData(Bitmap2D tex2D)
	{
		_ = Color.Value;
		for (int i = 0; i < tex2D.Size.y; i++)
		{
			for (int j = 0; j < tex2D.Size.x; j++)
			{
				tex2D.SetPixel(j, i, in color);
			}
		}
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		Color = new Sync<colorX>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => HighPriorityIntegration, 
			4 => FilterMode, 
			5 => AnisotropicLevel, 
			6 => WrapModeU, 
			7 => WrapModeV, 
			8 => MipmapBias, 
			9 => Profile, 
			10 => Size, 
			11 => Mipmaps, 
			12 => Format, 
			13 => Color, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static SolidColorTexture __New()
	{
		return new SolidColorTexture();
	}
}
