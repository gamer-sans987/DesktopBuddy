using System;
using Elements.Core;
using Renderite.Shared;

namespace FrooxEngine;

public class DesktopTexture : DynamicRendererAsset<DesktopTexture>, ITexture2D, ITexture, IRendererAsset, IAsset
{
	private Action onUpdated;

	public int2 Size { get; private set; }

	public bool HasAlpha => false;

	protected override RenderAssetManager<DesktopTexture> Manager => base.RenderSystem.DesktopTextures;

	public void Update(int index, Action onUpdated)
	{
		if (Manager == null)
		{
			onUpdated();
			return;
		}
		SetDesktopTextureProperties setDesktopTextureProperties = new SetDesktopTextureProperties();
		setDesktopTextureProperties.assetId = base.AssetId;
		setDesktopTextureProperties.displayIndex = index;
		this.onUpdated = onUpdated;
		base.RenderSystem.SendAssetUpdate(setDesktopTextureProperties);
	}

	public void HandlePropertiesUpdate(DesktopTexturePropertiesUpdate update)
	{
		base.Version++;
		Size = update.size;
		onUpdated();
	}

	public override void Unload()
	{
		base.Unload();
		UnloadDesktopTexture unloadDesktopTexture = new UnloadDesktopTexture();
		unloadDesktopTexture.assetId = base.AssetId;
		base.RenderSystem.SendAssetUpdate(unloadDesktopTexture);
	}
}
