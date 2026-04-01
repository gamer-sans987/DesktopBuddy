using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Elements.Assets;
using Elements.Core;
using FrooxEngine.UIX;
using Renderite.Shared;

namespace FrooxEngine;

/// <summary>
/// Renders a mesh with given set of materials in the world.
/// </summary>
[Category(new string[] { "Rendering" })]
public class MeshRenderer : RenderableComponent, IBounded, IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable, IHighlightable, ICustomInspector, IRenderable
{
	/// <summary>
	/// Reference to the mesh asset to be rendered
	/// </summary>
	public readonly AssetRef<Mesh> Mesh;

	/// <summary>
	/// List of references to the materials to be rendered
	/// </summary>
	public readonly SyncAssetList<Material> Materials;

	public readonly SyncAssetList<MaterialPropertyBlock> MaterialPropertyBlocks;

	/// <summary>
	/// ShadowCasting mode for rendering the mesh.
	/// </summary>
	public readonly Sync<ShadowCastMode> ShadowCastMode;

	public readonly Sync<MotionVectorMode> MotionVectorMode;

	public readonly Sync<int> SortingOrder;

	public bool MaterialsOrPropertyBlocksChanged;

	public MeshRendererManager Manager => base.Render.MeshRenderers;

	/// <summary>s
	/// Shortcut for the first material in the list of materials.
	/// </summary>
	public AssetRef<Material> Material
	{
		get
		{
			if (Materials.Count == 0)
			{
				return Materials.Add();
			}
			return Materials.GetElement(0);
		}
	}

	public virtual bool HasBoundingBox => Mesh.Value != RefID.Null;

	public virtual bool IsBoundingBoxAvailable => Mesh.IsAssetAvailable;

	public virtual BoundingBox LocalBoundingBox => Mesh.Asset?.Bounds ?? BoundingBox.Empty();

	public virtual BoundingBox GlobalBoundingBox => LocalBoundingBox.Transform(base.Slot.LocalToGlobal);

	public override bool IsRenderable
	{
		get
		{
			if (base.IsRenderable)
			{
				return ShouldBeEnabled;
			}
			return false;
		}
	}

	public bool RenderingLocallyUnblocked { get; set; }

	public bool ShouldBeEnabled
	{
		get
		{
			if (!base.Enabled)
			{
				return false;
			}
			if (!Mesh.IsAssetAvailable)
			{
				return false;
			}
			if (!base.Slot.IsLocalElement)
			{
				User activeUser = base.Slot.ActiveUser;
				if (activeUser != null && activeUser.IsRenderingLocallyBlocked && !RenderingLocallyUnblocked)
				{
					return false;
				}
			}
			return true;
		}
	}

	public bool IsLoaded
	{
		get
		{
			if (Mesh.Target != null && !Mesh.IsAssetAvailable)
			{
				return false;
			}
			foreach (IAssetProvider<Material> material in Materials)
			{
				if (material is MaterialProvider { IsLoaded: false })
				{
					return false;
				}
			}
			return true;
		}
	}

	public void ReplaceAllMaterials(IAssetProvider<Material> material)
	{
		for (int i = 0; i < Materials.Count; i++)
		{
			Materials[i] = material;
		}
	}

	public virtual Task<BoundingBox> ComputeExactBounds(Slot space)
	{
		return base.Slot.ComputeExactBoundsForMesh(space, Mesh.Asset?.Data);
	}

	public virtual Task ForeachExactBoundedPoint(Slot space, Action<float3> action)
	{
		return base.Slot.ForeachVertexInMesh(space, Mesh.Asset?.Data, action);
	}

	public override void InitRenderableState()
	{
		MaterialsOrPropertyBlocksChanged = true;
	}

	protected override void OnAwake()
	{
		base.OnAwake();
		ShadowCastMode.Value = Renderite.Shared.ShadowCastMode.On;
		MotionVectorMode.Value = Renderite.Shared.MotionVectorMode.Object;
		Materials.Changed += Materials_Changed;
		MaterialPropertyBlocks.Changed += MaterialPropertyBlocks_Changed;
		if (!base.Slot.IsLocalElement)
		{
			base.Slot.ActiveUserRootChanged += Slot_ActiveUserRootChanged;
		}
	}

	protected override void OnDispose()
	{
		base.OnDispose();
		if (!base.Slot.IsLocalElement)
		{
			base.Slot.ActiveUserRootChanged -= Slot_ActiveUserRootChanged;
		}
	}

	private void Slot_ActiveUserRootChanged(Slot slot)
	{
		MarkChangeDirty();
	}

	private void MaterialPropertyBlocks_Changed(IChangeable obj)
	{
		MaterialsOrPropertyBlocksChanged = true;
	}

	private void Materials_Changed(IChangeable obj)
	{
		MaterialsOrPropertyBlocksChanged = true;
	}

	public virtual Slot GenerateHighlight(Slot root, IAssetProvider<Material> highlightMaterial, bool trackPosition = true)
	{
		Slot slot = root.AddSlot(base.Slot.Name);
		if (trackPosition)
		{
			slot.SetupCopyTransform(base.Slot, keepGlobalPosition: false);
		}
		else
		{
			slot.CopyTransform(base.Slot);
		}
		MeshRenderer meshRenderer = slot.AttachComponent<MeshRenderer>();
		meshRenderer.Mesh.Target = Mesh.Target;
		for (int i = 0; i < Materials.Count; i++)
		{
			meshRenderer.Materials.Add(highlightMaterial);
		}
		return slot;
	}

	public IAssetProvider<Material> GetUniqueMaterial(int index = 0)
	{
		IAssetProvider<Material> assetProvider = Materials[index];
		if (assetProvider.References.Any((IAssetRef r) => r != Materials.GetElement(index)))
		{
			assetProvider = (IAssetProvider<Material>)base.World.AssetsSlot.AddSlot(base.Slot.Name + " - Unique Material").CopyComponent((Component)assetProvider);
			Materials[index] = assetProvider;
		}
		return assetProvider;
	}

	public override void BuildInspectorUI(UIBuilder ui)
	{
		base.BuildInspectorUI(ui);
		ui.Button("Inspector.MeshRenderer.SplitByMaterial".AsLocaleKey(), SplitSubmeshes);
		ui.Button("Inspector.MeshRenderer.MergeByMaterial".AsLocaleKey(), MergeByMaterial);
	}

	[SyncMethod(typeof(Action), new string[] { })]
	public void SplitSubmeshes()
	{
		SplitSubmeshesAsync();
	}

	[SyncMethod(typeof(Action), new string[] { })]
	public void MergeByMaterial()
	{
		MergeByMaterialAsync();
	}

	public Task SplitSubmeshesAsync(SubmeshNameGenerator nameGenerator = null)
	{
		return RunProcessingTask(null, async delegate(MeshX source, Slot assetsTarget)
		{
			await SplitSubmeshesTask(source, assetsTarget, nameGenerator);
		});
	}

	public Task MergeByMaterialAsync()
	{
		return RunProcessingTask(null, MergeByMaterialTask);
	}

	private Task RunProcessingTask(IButton button, Func<MeshX, Slot, Task> process)
	{
		if (Mesh.Target == null)
		{
			return Task.CompletedTask;
		}
		if (button != null)
		{
			button.LabelText = this.GetLocalized("General.Processing");
			button.Enabled = false;
		}
		Slot assetTarget = base.Slot;
		if (Mesh.Target.Slot != base.Slot)
		{
			assetTarget = base.World.AssetsSlot.AddSlot(base.Slot.Name + " - Processed");
		}
		return StartGlobalTask(async delegate
		{
			while (!Mesh.IsAssetAvailable)
			{
				await default(NextUpdate);
			}
			await default(ToBackground);
			object _lock = new object();
			await Mesh.Asset.RequestReadLock(_lock).ConfigureAwait(continueOnCapturedContext: false);
			MeshX arg = new MeshX(Mesh.Asset.Data);
			Mesh.Asset.ReleaseReadLock(_lock);
			try
			{
				await process(arg, assetTarget);
				await default(ToWorld);
				if (button != null && !button.IsRemoved)
				{
					button.LabelText = this.GetLocalized("General.Done");
					button.Enabled = true;
				}
			}
			catch (Exception ex)
			{
				UniLog.Error("Exception processing mesh:\n" + ex, stackTrace: false);
				await default(ToWorld);
				if (button != null && !button.IsRemoved)
				{
					button.LabelText = this.GetLocalized("General.FAILED");
					button.Enabled = true;
				}
			}
		});
	}

	[SyncMethod(typeof(Delegate), null)]
	private void SplitSubmeshes(IButton button, ButtonEventData eventData)
	{
		RunProcessingTask(button, async delegate(MeshX mesh, Slot assets)
		{
			await SplitSubmeshesTask(mesh, assets);
		});
	}

	[SyncMethod(typeof(Delegate), null)]
	private void MergeByMaterial(IButton button, ButtonEventData eventData)
	{
		RunProcessingTask(button, MergeByMaterialTask);
	}

	private async Task MergeByMaterialTask(MeshX sourceMesh, Slot assetTarget)
	{
		await default(ToWorld);
		List<object> materialKeys = new List<object>();
		foreach (IAssetProvider<Material> material in Materials)
		{
			materialKeys.Add(material);
		}
		await default(ToBackground);
		while (materialKeys.Count < sourceMesh.SubmeshCount)
		{
			materialKeys.Add(null);
		}
		while (materialKeys.Count > sourceMesh.SubmeshCount)
		{
			materialKeys.RemoveAt(materialKeys.Count - 1);
		}
		List<int> mergedSourceIndexes = new List<int>();
		sourceMesh.MergeSubmeshesByKey(materialKeys, mergedSourceIndexes);
		Uri uri = await base.Engine.LocalDB.SaveAssetAsync(sourceMesh).ConfigureAwait(continueOnCapturedContext: false);
		await default(ToWorld);
		Mesh.Target = assetTarget.AttachStaticMesh(uri);
		for (int i = 0; i < MathX.Min(mergedSourceIndexes.Count, Materials.Count); i++)
		{
			Materials[i] = Materials[mergedSourceIndexes[i]];
		}
		Materials.EnsureExactCount(mergedSourceIndexes.Count);
	}

	private async Task SplitSubmeshesTask(MeshX sourceMesh, Slot assetTarget, SubmeshNameGenerator nameGenerator = null)
	{
		if (nameGenerator == null)
		{
			nameGenerator = (string materialName, int index) => $"{materialName} (Submesh #{index})";
		}
		List<MeshX> splitMeshes = sourceMesh.SplitSubmeshes();
		foreach (MeshX item in splitMeshes)
		{
			PostprocessSplitMesh(item);
		}
		List<Uri> savedUris = new List<Uri>();
		foreach (MeshX item2 in splitMeshes)
		{
			List<Uri> list = savedUris;
			list.Add(await base.Engine.LocalDB.SaveAssetAsync(item2).ConfigureAwait(continueOnCapturedContext: false));
		}
		await default(ToWorld);
		MeshCollider component = base.Slot.GetComponent((MeshCollider c) => c.Mesh.Target == Mesh.Target);
		for (int num = 0; num < savedUris.Count; num++)
		{
			IAssetProvider<Material> assetProvider = Materials[MathX.Min(Materials.Count - 1, num)];
			string name = assetProvider.Slot.Name;
			Slot slot = base.Slot.AddSlot(nameGenerator(name, num));
			StaticMesh target = assetTarget.AttachStaticMesh(savedUris[num]);
			MeshRenderer meshRenderer = AttachSplitMesh(slot, splitMeshes[num]);
			meshRenderer.Mesh.Target = target;
			meshRenderer.Material.Target = assetProvider;
			if (component != null)
			{
				MeshCollider meshCollider = slot.AttachComponent<MeshCollider>();
				meshCollider.Mesh.Target = target;
				meshCollider.Type.Value = component.Type.Value;
				meshCollider.CharacterCollider.Value = component.CharacterCollider.Value;
				meshCollider.IgnoreRaycasts.Value = component.IgnoreRaycasts.Value;
			}
		}
		component?.Destroy();
		Destroy();
	}

	protected virtual void PostprocessSplitMesh(MeshX mesh)
	{
		mesh.StripEmptyBlendshapes();
	}

	protected virtual MeshRenderer AttachSplitMesh(Slot root, MeshX splitMesh)
	{
		return root.AttachComponent<MeshRenderer>();
	}

	protected override void RegisterAddedOrRemoved()
	{
		Manager.RenderableAddedOrRemoved(this);
	}

	protected override void OnChanges()
	{
		base.OnChanges();
		if (base.IsRenderableAllocated)
		{
			RegisterChanged();
		}
	}

	protected virtual void RegisterChanged()
	{
		Manager.RegisterChangedMeshRenderer(this);
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		Mesh = new AssetRef<Mesh>();
		Materials = new SyncAssetList<Material>();
		MaterialPropertyBlocks = new SyncAssetList<MaterialPropertyBlock>();
		ShadowCastMode = new Sync<ShadowCastMode>();
		MotionVectorMode = new Sync<MotionVectorMode>();
		SortingOrder = new Sync<int>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => Mesh, 
			4 => Materials, 
			5 => MaterialPropertyBlocks, 
			6 => ShadowCastMode, 
			7 => MotionVectorMode, 
			8 => SortingOrder, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static MeshRenderer __New()
	{
		return new MeshRenderer();
	}
}
