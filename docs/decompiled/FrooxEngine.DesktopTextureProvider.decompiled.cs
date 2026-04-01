using System;
using SkyFrost.Base;

namespace FrooxEngine;

[Category(new string[] { "Assets" })]
public class DesktopTextureProvider : AssetProvider<DesktopTexture>, ITexture2DProvider, IAssetProvider<ITexture2D>, IAssetProvider, IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable, ITextureProvider, IAssetProvider<ITexture>
{
	public readonly Sync<int> DisplayIndex;

	private DesktopTexture _desktopTex;

	private bool _created;

	protected override bool ForceUnload
	{
		get
		{
			if (base.Slot.IsActive)
			{
				return !base.Enabled;
			}
			return true;
		}
	}

	public override DesktopTexture Asset => _desktopTex;

	public override bool IsAssetAvailable => _created;

	ITexture IAssetProvider<ITexture>.Asset => _desktopTex;

	ITexture2D IAssetProvider<ITexture2D>.Asset => _desktopTex;

	protected override void OnActivated()
	{
		base.OnActivated();
		MarkChangeDirty();
	}

	protected override void OnDeactivated()
	{
		base.OnDeactivated();
		MarkChangeDirty();
	}

	protected override void FreeAsset()
	{
		_desktopTex?.Unload();
		_desktopTex = null;
		_created = false;
	}

	protected override void UpdateAsset()
	{
		if (base.World != Userspace.UserspaceWorld)
		{
			return;
		}
		AppConfig config = FrooxEngine.Engine.Config;
		if (config == null || !config.DisableDesktop)
		{
			if (_desktopTex == null)
			{
				_desktopTex = new DesktopTexture();
				_desktopTex.InitializeDynamic(base.AssetManager);
			}
			_desktopTex.Update(DisplayIndex.Value, OnTextureCreated);
		}
	}

	private void OnTextureCreated()
	{
		if (base.World != null)
		{
			_created = true;
			AssetCreated();
		}
		else
		{
			FreeAsset();
		}
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		DisplayIndex = new Sync<int>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => DisplayIndex, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static DesktopTextureProvider __New()
	{
		return new DesktopTextureProvider();
	}
}
