using System;
using System.Collections.Generic;
using Elements.Core;
using FrooxEngine.UIX;
using SkyFrost.Base;

namespace FrooxEngine;

/// <summary>
/// A base class for all components that provide Assets, either static or dynamic.
/// It provides common behavior, such as tracking whether the asset is referenced and informing derived classes
/// when the load or free the asset from the memory.
/// </summary>
/// <typeparam name="A">The type of the asset that is provided</typeparam>
public abstract class AssetProvider<A> : Component, IAssetProvider<A>, IAssetProvider, IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable, ICustomInspector where A : Asset, new()
{
	private HashSet<IAssetRef> references = new HashSet<IAssetRef>();

	private HashSet<IAssetRef> updateListeners;

	protected AssetManager AssetManager => base.Engine.AssetManager;

	/// <summary>
	/// Number of references to this asset in the world
	/// </summary>
	public int AssetReferenceCount => references.Count;

	/// <summary>
	/// Instance of the asset if loaded
	/// </summary>
	public abstract A Asset { get; }

	public IAsset GenericAsset => Asset;

	/// <summary>
	/// Determines if the asset is currently loaded and available for use
	/// </summary>
	public abstract bool IsAssetAvailable { get; }

	protected virtual bool AlwaysLoad => false;

	protected virtual bool ForceUnload => false;

	public IEnumerable<IAssetRef> References => references;

	public void ReferenceSet(IAssetRef reference)
	{
		if (!base.IsDisposed && references.Add(reference) && references.Count == 1)
		{
			MarkChangeDirty();
		}
	}

	public void ReferenceFreed(IAssetRef reference)
	{
		if (!base.IsDisposed)
		{
			references.Remove(reference);
			if (references.Count == 0)
			{
				MarkChangeDirty();
			}
		}
	}

	public void RegisterUpdateListener(IAssetRef reference)
	{
		if (!base.IsDisposed)
		{
			if (updateListeners == null)
			{
				updateListeners = new HashSet<IAssetRef>();
			}
			updateListeners.Add(reference);
		}
	}

	public void UnregisterUpdateListener(IAssetRef reference)
	{
		if (!base.IsDisposed)
		{
			updateListeners.Remove(reference);
		}
	}

	protected override void OnChanges()
	{
		RefreshAssetState();
	}

	protected override void OnDestroy()
	{
		FreeAsset();
		AssetRemoved();
		base.OnDestroy();
	}

	protected override void OnDispose()
	{
		FreeAsset();
		if (base.World.IsDisposed)
		{
			references.Clear();
			references = null;
		}
		else
		{
			AssetRemoved();
		}
		updateListeners?.Clear();
		updateListeners = null;
		base.OnDispose();
	}

	private void RefreshAssetState()
	{
		if (ForceUnload)
		{
			FreeAsset();
		}
		else if (AlwaysLoad)
		{
			UpdateAsset();
		}
		else if (AssetReferenceCount == 0 && IsAssetAvailable)
		{
			RunInUpdates(8, TryFreeAsset);
		}
		else if (AssetReferenceCount > 0)
		{
			UpdateAsset();
		}
	}

	protected void TryFreeAsset()
	{
		if (AssetReferenceCount == 0 && IsAssetAvailable)
		{
			FreeAsset();
		}
	}

	protected abstract void FreeAsset();

	protected abstract void UpdateAsset();

	protected void AssetCreated()
	{
		if (!base.IsDisposed && references.Count > 0)
		{
			base.World.AssetManager.QueueAssetCreated(this);
		}
	}

	protected void AssetUpdated()
	{
		if (!base.IsDisposed && updateListeners != null && updateListeners.Count > 0)
		{
			base.World.AssetManager.QueueAssetUpdated(this);
		}
	}

	protected void AssetRemoved()
	{
		if (!base.IsDisposed && references.Count > 0)
		{
			base.World.AssetManager.QueueAssetRemoved(this);
		}
	}

	void IAssetProvider.SendAssetCreated()
	{
		if (base.IsDisposed)
		{
			return;
		}
		foreach (IAssetRef reference in references)
		{
			reference.AssetUpdated();
		}
	}

	void IAssetProvider.SendAssetUpdated()
	{
		if (base.IsDisposed)
		{
			return;
		}
		foreach (IAssetRef updateListener in updateListeners)
		{
			updateListener.AssetUpdated();
		}
	}

	void IAssetProvider.SendAssetRemoved()
	{
		if (references == null)
		{
			return;
		}
		foreach (IAssetRef reference in references)
		{
			reference.AssetUpdated();
		}
		if (base.IsDisposed)
		{
			references.Clear();
			references = null;
		}
	}

	protected virtual Uri ProcessURL(Uri assetURL)
	{
		if (assetURL == null)
		{
			return null;
		}
		if (!AssetManager.IsSupportedScheme(assetURL))
		{
			if (!(assetURL.Scheme != base.Cloud.Platform.DBScheme))
			{
				return null;
			}
			assetURL = assetURL.MigrateLegacyURL(base.Cloud.Platform);
		}
		if (assetURL.Scheme == base.Cloud.Assets.DBScheme && !base.Cloud.Assets.IsValidDBUri(assetURL))
		{
			return null;
		}
		return assetURL;
	}

	public virtual void BuildInspectorUI(UIBuilder ui)
	{
		WorkerInspector.BuildInspectorUI(this, ui);
		if (Asset is IRendererAsset rendererAsset)
		{
			ui.Text((LocaleString)$"RenderableAssetID: {rendererAsset.AssetId}");
		}
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
	}
}
