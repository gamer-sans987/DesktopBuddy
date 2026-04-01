using System;

namespace FrooxEngine;

public abstract class DynamicAssetProvider<A> : AssetProvider<A> where A : Asset, new()
{
	private A _asset;

	public readonly Sync<bool> HighPriorityIntegration;

	public bool LocalManualUpdate;

	public override A Asset => _asset;

	public override bool IsAssetAvailable => Asset != null;

	public void RunManualUpdate()
	{
		if (!LocalManualUpdate)
		{
			throw new Exception("This asset provider isn't configured for manual update locally, cannot run!");
		}
		RunAssetUpdate();
	}

	protected override void UpdateAsset()
	{
		if (!LocalManualUpdate)
		{
			RunAssetUpdate();
		}
	}

	private void RunAssetUpdate()
	{
		if (_asset == null)
		{
			_asset = new A();
			_asset.InitializeDynamic(base.AssetManager);
			_asset.SetOwner(this);
			AssetCreated(_asset);
		}
		_asset.HighPriorityIntegration = HighPriorityIntegration.Value;
		UpdateAsset(_asset);
	}

	protected override void FreeAsset()
	{
		if (_asset != null)
		{
			_asset.Unload();
			_asset = null;
			ClearAsset();
			AssetRemoved();
		}
	}

	protected abstract void AssetCreated(A asset);

	protected abstract void UpdateAsset(A asset);

	protected abstract void ClearAsset();

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		HighPriorityIntegration = new Sync<bool>();
	}
}
