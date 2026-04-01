using System;
using System.Threading;
using System.Threading.Tasks;
using Elements.Assets;
using Elements.Core;

namespace FrooxEngine;

public abstract class ProceduralAssetProvider<A> : DynamicAssetProvider<A> where A : Asset, new()
{
	private SpinLock updateLock = new SpinLock(enableThreadOwnerTracking: false);

	private volatile bool updateRunning;

	private volatile bool runUpdate;

	private volatile bool safeDisposeFinished;

	private int _updateCount;

	private bool _error;

	private Action<IAsset> _writeLockGranted;

	private Action _backgroundAssetUpdate;

	private AssetIntegrated _integratedCallback;

	private Func<Task> _asyncAssetUpdate;

	public int UpdateCount => _updateCount;

	public bool Error => _error;

	protected IBackingBufferAllocator Allocator
	{
		get
		{
			if (!base.Engine.RenderSystem.HasRenderer)
			{
				return null;
			}
			return base.Engine.RenderSystem;
		}
	}

	protected virtual bool UseAsyncUpdate => false;

	protected override void UpdateAsset(A asset)
	{
		bool lockTaken = false;
		try
		{
			updateLock.Enter(ref lockTaken);
			if (base.IsDisposed)
			{
				RunSafeDispose();
				return;
			}
			if (updateRunning)
			{
				runUpdate = true;
				return;
			}
			updateRunning = true;
		}
		finally
		{
			if (lockTaken)
			{
				updateLock.Exit();
			}
		}
		PrepareAssetUpdateData();
		if (_writeLockGranted == null)
		{
			_writeLockGranted = WriteLockGranted;
			_backgroundAssetUpdate = RunBackgroundAssetUpdate;
			_asyncAssetUpdate = RunBackgroundAssetUpdateAsync;
			_integratedCallback = AssetIntegrated;
		}
		asset.RequestWriteLock(this, _writeLockGranted);
	}

	protected override void FreeAsset()
	{
		bool lockTaken = false;
		try
		{
			updateLock.Enter(ref lockTaken);
			if (updateRunning)
			{
				runUpdate = true;
				return;
			}
		}
		finally
		{
			if (lockTaken)
			{
				updateLock.Exit();
			}
		}
		base.FreeAsset();
	}

	private void WriteLockGranted(IAsset asset)
	{
		if (base.IsDisposed)
		{
			asset.ReleaseWriteLock(this);
			RunSafeDispose();
		}
		else if (UseAsyncUpdate)
		{
			StartTask(_asyncAssetUpdate);
		}
		else
		{
			base.Engine.WorkProcessor.Enqueue(_backgroundAssetUpdate, HighPriorityIntegration.Value ? WorkType.HighPriority : WorkType.Background);
		}
	}

	private async Task RunBackgroundAssetUpdateAsync()
	{
		try
		{
			if (!_error)
			{
				await UpdateAssetDataAsync(Asset).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		catch (ThreadAbortException)
		{
		}
		catch (Exception exception)
		{
			_error = true;
			UniLog.Error($"Exception updating procedural asset {GetType()}\n{MembersToString()}:ds\n" + DebugManager.PreprocessException(exception), stackTrace: false);
			GenerateErrorIndication();
		}
		FinishAssetUpdate();
	}

	private void RunBackgroundAssetUpdate()
	{
		if (base.IsDisposed)
		{
			Asset?.ReleaseWriteLock(this);
			RunSafeDispose();
			return;
		}
		try
		{
			if (!_error)
			{
				UpdateAssetData(Asset);
			}
		}
		catch (ThreadAbortException)
		{
		}
		catch (Exception exception)
		{
			_error = true;
			UniLog.Error($"Exception updating procedural asset {GetType()}\n{MembersToString()}:\n" + DebugManager.PreprocessException(exception), stackTrace: false);
			GenerateErrorIndication();
		}
		FinishAssetUpdate();
	}

	protected abstract void GenerateErrorIndication();

	private void FinishAssetUpdate()
	{
		Asset?.ReleaseWriteLock(this);
		if (base.IsDisposed)
		{
			RunSafeDispose();
		}
		else
		{
			UploadAssetData(_integratedCallback);
		}
	}

	protected void AssetIntegrationUpdated(bool instanceChanged)
	{
		if (instanceChanged)
		{
			AssetCreated();
		}
		else
		{
			AssetUpdated();
		}
	}

	private void AssetIntegrated(bool assetInstanceChanged)
	{
		AssetIntegrationUpdated(assetInstanceChanged);
		OnAssetIntegrated();
		_updateCount++;
		bool lockTaken = false;
		try
		{
			updateLock.Enter(ref lockTaken);
			if (runUpdate)
			{
				MarkChangeDirty();
				runUpdate = false;
			}
			updateRunning = false;
		}
		finally
		{
			if (lockTaken)
			{
				updateLock.Exit();
			}
		}
	}

	private void RunSafeDispose()
	{
		if (!safeDisposeFinished)
		{
			safeDisposeFinished = true;
			OnSafeDispose();
		}
	}

	protected virtual void PrepareAssetUpdateData()
	{
	}

	protected virtual void OnAssetIntegrated()
	{
	}

	protected virtual void OnSafeDispose()
	{
	}

	protected abstract ValueTask UpdateAssetDataAsync(A asset);

	protected abstract void UpdateAssetData(A asset);

	protected abstract void UploadAssetData(AssetIntegrated integratedCallback);

	protected override void OnDispose()
	{
		bool lockTaken = false;
		try
		{
			updateLock.Enter(ref lockTaken);
			if (!updateRunning)
			{
				RunSafeDispose();
			}
		}
		finally
		{
			if (lockTaken)
			{
				updateLock.Exit();
			}
		}
		base.OnDispose();
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
	}
}
