using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Elements;
using Elements.Assets;
using Elements.Core;
using FrooxEngine.CommonAvatar;
using FrooxEngine.UIX;

namespace FrooxEngine;

/// <summary>
/// Renders a skinned mesh in the world, deformed by a set of bones.
/// </summary>
[Category(new string[] { "Rendering" })]
public class SkinnedMeshRenderer : MeshRenderer, ICustomInspector, IWorker, IWorldElement, ICustomMemberNameSource
{
	public readonly Sync<SkinnedBounds> BoundsComputeMethod;

	public readonly SyncRef<SkinnedMeshRenderer> ProxyBoundsSource;

	public readonly Sync<BoundingBox> ExplicitLocalBounds;

	/// <summary>
	/// List of references to slots in the world representing the mesh bones
	/// </summary>
	public readonly SyncRefList<Slot> Bones;

	/// <summary>
	/// List of weights of the blend shapes contained in the skinned mesh
	/// </summary>
	[Range(0f, 1f, "F2")]
	[CustomListEditor(typeof(BlendshapeWeightListEditor))]
	public readonly SyncFieldList<float> BlendShapeWeights;

	private List<BoundingBox> _staticBounds;

	internal bool BonesChanged;

	internal bool BlendShapeWeightsChanged;

	internal bool UpdateAllBlendshapes = true;

	internal int LastMeshAssetId = -1;

	internal AssetLoadState LastMeshLoadState = (AssetLoadState)(-1);

	private MeshX _setupMesh;

	private HashSet<Slot> _markedBones = new HashSet<Slot>();

	public new SkinnedMeshRendererManager Manager => base.Render.SkinnedMeshRenderers;

	public int? MeshBoneCount => Mesh.Asset?.Data?.BoneCount;

	public BoundingBox ComputedBounds { get; private set; } = BoundingBox.Empty();

	public Slot ComputedBoundsSpace { get; private set; }

	public Slot ComputedRendererRoot { get; private set; }

	public List<BoneNode> ComputedBoneHierarchies { get; private set; }

	internal BoundingBox? RendererComputedGlobalBounds { get; set; }

	/// <summary>
	/// Returns the number of blend shapes in the currently associated mesh asset
	/// </summary>
	public int MeshBlendshapeCount => (Mesh.Asset?.Data?.BlendShapeCount).GetValueOrDefault();

	public int RenderableBlendshapeCount => MathX.Min(MeshBlendshapeCount, BlendShapeWeights.Count);

	public override bool HasBoundingBox => base.HasBoundingBox;

	public override bool IsBoundingBoxAvailable
	{
		get
		{
			if (base.IsBoundingBoxAvailable && ComputedBounds.IsValid && ComputedBoundsSpace != null)
			{
				return !ComputedBoundsSpace.IsRemoved;
			}
			return false;
		}
	}

	public override BoundingBox LocalBoundingBox
	{
		get
		{
			if (!IsBoundingBoxAvailable)
			{
				return BoundingBox.Empty();
			}
			if (ComputedBoundsSpace != base.Slot)
			{
				return ComputedBounds.Transform(ComputedBoundsSpace.GetLocalToSpaceMatrix(base.Slot));
			}
			return ComputedBounds;
		}
	}

	public BoundingBox UnboundMeshLocalBoundingBox => base.LocalBoundingBox;

	public BoundingBox UnboundMeshGlobalBoundingBox => UnboundMeshLocalBoundingBox.Transform(base.Slot.LocalToGlobal);

	public Slot TryGetBone(int index)
	{
		if (index < 0)
		{
			throw new ArgumentOutOfRangeException("Index must be positive");
		}
		if (index < Bones.Count)
		{
			return Bones[index];
		}
		return null;
	}

	protected override void OnAwake()
	{
		base.OnAwake();
		Bones.ElementsAdded += Bones_ElementsAdded;
		BlendShapeWeights.Changed += BlendShapeWeights_Changed;
		base.Slot.ActiveUserRootChanged += Slot_ActiveUserRootChanged;
		ExplicitLocalBounds.Value = BoundingBox.Empty();
	}

	private void Bones_ElementsAdded(SyncElementList<SyncRef<Slot>> list, int startIndex, int count)
	{
		for (int i = 0; i < count; i++)
		{
			list[i + startIndex].OnTargetChange += BoneTargetChanged;
		}
	}

	private void BoneTargetChanged(SyncRef<Slot> reference)
	{
		MarkBonesChanged();
	}

	private void MarkBonesChanged()
	{
		if (base.IsRenderingSupported && !BonesChanged)
		{
			BonesChanged = true;
			Manager.RegisterUpdatedBones(this);
			MarkChangeDirty();
		}
	}

	protected override void OnChanges()
	{
		base.OnChanges();
		MeshX meshX = Mesh.Asset?.Data;
		if (meshX != _setupMesh)
		{
			_staticBounds?.Clear();
			ComputedRendererRoot = null;
			MarkBonesChanged();
			MarkBlendshapeWeightsChanged();
			UpdateAllBlendshapes = true;
			if (meshX != null)
			{
				SetupBlendShapes(meshX);
			}
			_setupMesh = meshX;
		}
		if (!BonesChanged && ComputedRendererRoot != null)
		{
			return;
		}
		ComputedRendererRoot = ComputeRootBone();
		_staticBounds?.Clear();
		if (!base.IsRenderingSupported)
		{
			return;
		}
		List<Slot> list = Pool.BorrowList<Slot>();
		foreach (Slot markedBone in _markedBones)
		{
			list.Add(markedBone);
		}
		foreach (Slot bone in Bones)
		{
			if (bone != null)
			{
				list.Remove(bone);
				if (_markedBones.Add(bone))
				{
					bone.IncrementRenderable();
				}
			}
		}
		if (ComputedRendererRoot != null && _markedBones.Add(ComputedRendererRoot))
		{
			ComputedRendererRoot.IncrementRenderable();
		}
		foreach (Slot item in list)
		{
			item.DecrementRenderable();
			_markedBones.Remove(item);
		}
		Pool.Return(ref list);
	}

	private void Slot_ActiveUserRootChanged(Slot slot)
	{
		MarkChangeDirty();
	}

	protected override void OnDispose()
	{
		_setupMesh = null;
		foreach (Slot markedBone in _markedBones)
		{
			markedBone.DecrementRenderable();
		}
		_markedBones = null;
		base.Slot.ActiveUserRootChanged -= Slot_ActiveUserRootChanged;
		base.OnDispose();
	}

	private void BlendShapeWeights_Changed(IChangeable obj)
	{
		MarkBlendshapeWeightsChanged();
	}

	private void MarkBlendshapeWeightsChanged()
	{
		if (base.IsRenderingSupported && !BlendShapeWeightsChanged)
		{
			BlendShapeWeightsChanged = true;
			Manager.RegisterUpdatedBlendshapes(this);
		}
	}

	/// <summary>
	/// Finds the index of a blend shape with given name in the currently associated mesh asset.
	/// </summary>
	/// <param name="name"></param>
	/// <returns></returns>
	public int BlendShapeIndex(string name)
	{
		return Mesh.Asset?.Data?.BlendShapeIndex(name) ?? (-1);
	}

	/// <summary>
	/// Returns a name of a blend shape with given index in the currently associated mesh asset
	/// </summary>
	/// <param name="index"></param>
	/// <returns></returns>
	public string BlendShapeName(int index)
	{
		MeshX meshX = Mesh.Asset?.Data;
		if (meshX == null)
		{
			return null;
		}
		if (index < 0 || index >= meshX.BlendShapeCount)
		{
			return null;
		}
		return meshX.GetBlendShape(index)?.Name;
	}

	/// <summary>
	/// Returns whether a blendshape exists in the currently associated mesh asset
	/// </summary>
	/// <param name="name">The blendshape name to check</param>
	/// <returns>True if the blendshape exists, otherwise false</returns>
	public bool HasBlendshape(string name)
	{
		return Mesh.Asset?.Data?.HasBlendShape(name) == true;
	}

	/// <summary>
	/// Tries to get a blendshape from the currently associated mesh asset
	/// </summary>
	/// <param name="name">The blendshape name to search for</param>
	/// <returns>The IField of the blendshape, otherwise null</returns>
	public IField<float>? TryGetBlendShape(string name)
	{
		int num = BlendShapeIndex(name);
		if (num < 0)
		{
			return null;
		}
		return BlendShapeWeights.GetElement(num);
	}

	public IField<float> GetBlendShape(string name)
	{
		return BlendShapeWeights.GetElement(BlendShapeIndex(name));
	}

	/// <summary>
	/// Returns the current blend shape weight for the currently associated mesh asset
	/// </summary>
	/// <param name="name"></param>
	/// <returns></returns>
	public float GetBlendShapeWeight(string name)
	{
		int num = BlendShapeIndex(name);
		if (num < 0)
		{
			throw new Exception("BlendShape with given name doesn't currently exist");
		}
		if (num >= BlendShapeWeights.Count)
		{
			return 0f;
		}
		return BlendShapeWeights[num];
	}

	/// <summary>
	/// Sets the blend shape weight for the currently associated mesh asset
	/// </summary>
	/// <param name="name"></param>
	/// <param name="weight"></param>
	public void SetBlendShapeWeight(string name, float weight)
	{
		int num = BlendShapeIndex(name);
		if (num < 0)
		{
			throw new Exception("BlendShape with given name doesn't currently exist");
		}
		while (BlendShapeWeights.Count <= num)
		{
			BlendShapeWeights.Add();
		}
		BlendShapeWeights[num] = weight;
	}

	public float GetBlendShapeWeight(int index)
	{
		if (index < 0 || index >= MeshBlendshapeCount)
		{
			throw new ArgumentOutOfRangeException();
		}
		if (index >= BlendShapeWeights.Count)
		{
			return 0f;
		}
		return BlendShapeWeights[index];
	}

	public void SetBlendShapeWeight(int index, float weight)
	{
		if (index < 0 || index >= MeshBlendshapeCount)
		{
			throw new ArgumentOutOfRangeException();
		}
		while (BlendShapeWeights.Count <= index)
		{
			BlendShapeWeights.Add();
		}
		BlendShapeWeights[index] = weight;
	}

	public List<BoneNode> ComputeBoneHierarchies()
	{
		int count = MeshBoneCount ?? int.MaxValue;
		return BoneNode.ConstructBoneNodeHiearchies((from b in Bones.Take(count)
			where b != null
			select b).Distinct());
	}

	public Slot ComputeRootBone()
	{
		ComputedBoneHierarchies = ComputeBoneHierarchies();
		if (ComputedBoneHierarchies.Count == 1)
		{
			return ComputedBoneHierarchies[0].bone;
		}
		return base.Slot;
	}

	public void SetupBones(Dictionary<string, Slot> mapping)
	{
		MeshX meshX = Mesh.Asset?.Data;
		if (meshX == null)
		{
			throw new Exception("Cannot setup bones without a loaded asset");
		}
		Bones.Clear();
		for (int i = 0; i < meshX.BoneCount; i++)
		{
			string name = meshX.GetBone(i).Name;
			mapping.TryGetValue(name, out Slot value);
			Bones.Add().Target = value;
		}
	}

	public void SetupBones(Slot root)
	{
		MeshX meshX = Mesh.Asset?.Data;
		if (meshX == null)
		{
			throw new Exception("Cannot setup bones without a loaded asset");
		}
		Bones.Clear();
		Dictionary<string, Slot> dictionary = Pool.BorrowDictionary<string, Slot>();
		GetBoneCandidates(root, dictionary);
		for (int i = 0; i < meshX.BoneCount; i++)
		{
			string name = meshX.GetBone(i).Name;
			dictionary.TryGetValue(name, out var value);
			Bones.Add().Target = value;
		}
		Pool.Return(ref dictionary);
	}

	private void GetBoneCandidates(Slot root, Dictionary<string, Slot> candidates)
	{
		if (root.GetComponent<MeshRenderer>() != null)
		{
			return;
		}
		if (!candidates.ContainsKey(root.Name))
		{
			candidates.Add(root.Name, root);
		}
		else
		{
			UniLog.Warning("Duplicate slot name that's a potential joint: " + root.Name);
		}
		foreach (Slot child in root.Children)
		{
			GetBoneCandidates(child, candidates);
		}
	}

	public void SetupBlendShapes()
	{
		SetupBlendShapes(Mesh.Asset?.Data);
	}

	private void SetupBlendShapes(MeshX meshx)
	{
		if (meshx == null)
		{
			throw new ArgumentNullException("meshx");
		}
		while (BlendShapeWeights.Count > meshx.BlendShapeCount)
		{
			BlendShapeWeights.RemoveAt(BlendShapeWeights.Count - 1);
		}
		while (BlendShapeWeights.Count < meshx.BlendShapeCount)
		{
			BlendShapeWeights.Add(0f);
		}
	}

	public override Slot GenerateHighlight(Slot root, IAssetProvider<Material> highlightMaterial, bool trackPosition = true)
	{
		Slot slot = root.AddSlot(base.Slot.Name);
		if (trackPosition)
		{
			slot.SetupCopyTransform(base.Slot);
		}
		else
		{
			slot.CopyTransform(base.Slot);
		}
		SkinnedMeshRenderer skinnedMeshRenderer = slot.AttachComponent<SkinnedMeshRenderer>();
		skinnedMeshRenderer.Mesh.Target = Mesh.Target;
		for (int i = 0; i < Materials.Count; i++)
		{
			skinnedMeshRenderer.Materials.Add(highlightMaterial);
		}
		foreach (Slot bone in Bones)
		{
			skinnedMeshRenderer.Bones.Add(bone);
		}
		foreach (float blendShapeWeight in BlendShapeWeights)
		{
			skinnedMeshRenderer.BlendShapeWeights.Add(blendShapeWeight);
		}
		return slot;
	}

	public void GetBoneTransforms(Span<float4x4> buffer)
	{
		if (buffer.Length != Bones.Count)
		{
			throw new ArgumentException("Buffer length must match the number of blendshapes");
		}
		for (int i = 0; i < buffer.Length; i++)
		{
			Slot slot = Bones[i];
			if (slot == null)
			{
				buffer[i] = float4x4.Identity;
			}
			else
			{
				buffer[i] = slot.GetLocalToSpaceMatrix(base.Slot);
			}
		}
	}

	public void GetBlendshapeWeights(Span<float> buffer)
	{
		if (buffer.Length != MeshBlendshapeCount)
		{
			throw new ArgumentException("Buffer length must match the number of blendshapes");
		}
		for (int i = 0; i < MeshBlendshapeCount; i++)
		{
			Sync<float> element = BlendShapeWeights.GetElement(i);
			buffer[i] = element.Value;
		}
	}

	public Dictionary<int, float> GetBlendshapeWeights(Predicate<IField<float>> filter = null)
	{
		Dictionary<int, float> dictionary = new Dictionary<int, float>();
		for (int i = 0; i < MeshBlendshapeCount; i++)
		{
			Sync<float> element = BlendShapeWeights.GetElement(i);
			if (filter == null || filter(element))
			{
				dictionary.Add(i, element.Value);
			}
		}
		return dictionary;
	}

	public float4x4[] GetBoneTransforms()
	{
		float4x4[] array = new float4x4[Bones.Count];
		GetBoneTransforms(array);
		return array;
	}

	public override void BuildInspectorUI(UIBuilder ui)
	{
		base.BuildInspectorUI(ui);
		ui.Button("Inspector.SkinnedMesh.SeparateOutBlendshapes".AsLocaleKey(), SeparateOutBlendshapes);
		ui.Button("Inspector.SkinnedMesh.StripEmptyBlendshapes".AsLocaleKey(), StripEmptyBlendshapes);
		ui.Button("Inspector.SkinnedMesh.StripEmptyBones".AsLocaleKey(), StripEmptyBones);
		ui.Button("Inspector.SkinnedMesh.BakeNonDrivenBlendshapes".AsLocaleKey(), BakeNonDrivenBlendshapes);
		ui.Button("Inspector.SkinnedMesh.VisualizeBoneBounds".AsLocaleKey(), VisualizeBoneBounds);
		ui.Button("Inspector.SkinnedMesh.VisualizeApproximateBoneBounds".AsLocaleKey(), VisualizeApproximateBoneBounds);
		ui.Button("Inspector.SkinnedMesh.ClearBoundsVisuals".AsLocaleKey(), ClearBoundsVisuals);
		ui.Button("Inspector.SkinnedMesh.ResetBonesToBindPoses".AsLocaleKey(), ResetBonesToBindPoses);
		ui.Button("Inspector.SkinnedMesh.ComputeExplicitBoundsFromPose".AsLocaleKey(), ComputeExplicitBoundsFromPose);
		ui.Button("Inspector.SkinnedMesh.ExtendExplicitBoundsFromPose".AsLocaleKey(), ExtendExplicitBoundsFromPose);
		ui.Button("Inspector.SkinnedMesh.BakeToStaticMesh".AsLocaleKey(), BakeToStaticMesh);
		ui.Button("Inspector.SkinnedMesh.SortBlendshapes.Name".AsLocaleKey(), SortBlendshapesByName);
		ui.Button("Inspector.SkinnedMesh.SortBlendshapes.NameLength".AsLocaleKey(), SortBlendshapesByNameLength);
		bool flag = false;
		if (base.World.GetUserByAllocationID(ui.Canvas.ReferenceID.User)?.UserID == "U-Zaytha")
		{
			flag = true;
		}
		ui.Button(flag ? ((LocaleString)"DeZomp") : "Inspector.Mesh.MergeBlendshapes".AsLocaleKey(), MergeBlendshapes);
	}

	[SyncMethod(typeof(Func<int, Axis3D, float, float, string, string, Task<bool>>), new string[] { })]
	public async Task<bool> SplitBlenshapeAlongAxis(int blendshapeIndex, Axis3D axis, float center = 0f, float transition = 1E-05f, string negativeSuffix = ".L", string positiveSuffix = ".R")
	{
		return await ProcessMesh(async delegate(MeshX data)
		{
			await default(ToWorld);
			if (blendshapeIndex < 0 || blendshapeIndex >= data.BlendShapeCount)
			{
				return (MeshX)null;
			}
			await default(ToBackground);
			data.SplitBlenshapeByAxis(blendshapeIndex, axis, center, transition, negativeSuffix, positiveSuffix);
			return data;
		}, async delegate
		{
			BlendShapeWeights.Insert(blendshapeIndex + 1, BlendShapeWeights[blendshapeIndex]);
			return true;
		});
	}

	[SyncMethod(typeof(Func<int, Task<bool>>), new string[] { })]
	public async Task<bool> BakeBlendshape(int blendshapeIndex)
	{
		return await ProcessMesh(async delegate(MeshX data)
		{
			await default(ToWorld);
			if (blendshapeIndex < 0 || blendshapeIndex >= data.BlendShapeCount)
			{
				return (MeshX)null;
			}
			Dictionary<int, float> weights = new Dictionary<int, float> { 
			{
				blendshapeIndex,
				BlendShapeWeights[blendshapeIndex]
			} };
			await default(ToBackground);
			data.BakeBlendShapes(weights);
			return data;
		}, async delegate
		{
			BlendShapeWeights.RemoveAt(blendshapeIndex);
			return true;
		});
	}

	public async Task<bool> SplitBlendshapeIntoStaticMesh(int blendshapeIndex)
	{
		StaticMesh provider = Mesh.Target as StaticMesh;
		if (Mesh.Asset == null || provider == null)
		{
			return false;
		}
		await default(ToBackground);
		object _lock = new object();
		await Mesh.Asset.RequestReadLock(_lock).ConfigureAwait(continueOnCapturedContext: false);
		MeshX data = new MeshX(Mesh.Asset.Data);
		Mesh.Asset.ReleaseReadLock(_lock);
		if (blendshapeIndex < 0 || blendshapeIndex >= data.BlendShapeCount)
		{
			return false;
		}
		string blendshapeName = data.GetBlendShape(blendshapeIndex).Name;
		Dictionary<int, float> dictionary = new Dictionary<int, float>();
		dictionary.Add(blendshapeIndex, BlendShapeWeights[blendshapeIndex]);
		var (meshX, mapping) = data.SplitBlendShapesIntoStaticMesh(dictionary);
		if (meshX == null)
		{
			return false;
		}
		Uri splitUrl = await base.Engine.LocalDB.SaveAssetAsync(meshX);
		Uri newUrl = await base.Engine.LocalDB.SaveAssetAsync(data);
		await default(ToWorld);
		provider.URL.Value = newUrl;
		BlendShapeWeights.RemoveAt(blendshapeIndex);
		MeshRenderer meshRenderer = base.Slot.AddSlot("Split blendshape: " + blendshapeName).AttachComponent<MeshRenderer>();
		meshRenderer.Mesh.Target = base.World.AssetsSlot.AddSlot(base.Slot.Name + " - Blendshape to Static Mesh").AttachStaticMesh(splitUrl);
		meshRenderer.Materials.Clear();
		for (int i = 0; i < mapping.submeshMapping.Count; i++)
		{
			if (mapping.submeshMapping[i] >= 0)
			{
				meshRenderer.Materials.Add(Materials[i]);
			}
		}
		return true;
	}

	[SyncMethod(typeof(Func<int, Task<bool>>), new string[] { })]
	public async Task<bool> RemoveBlendshape(int blendshapeIndex)
	{
		return await ProcessMesh(async delegate(MeshX data)
		{
			await default(ToWorld);
			if (blendshapeIndex < 0 || blendshapeIndex >= data.BlendShapeCount)
			{
				return (MeshX)null;
			}
			Dictionary<int, float> weights = new Dictionary<int, float> { { blendshapeIndex, 0f } };
			await default(ToBackground);
			data.BakeBlendShapes(weights);
			return data;
		}, async delegate
		{
			BlendShapeWeights.RemoveAt(blendshapeIndex);
			return true;
		});
	}

	[SyncMethod(typeof(Delegate), null)]
	private void VisualizeApproximateBoneBounds(IButton button, ButtonEventData eventData)
	{
		StaticMesh staticMesh = Mesh.Target as StaticMesh;
		if (Mesh.Asset == null || staticMesh == null)
		{
			return;
		}
		IReadOnlyList<BoneMetadata> boneMetadata = Mesh.Asset.BoneMetadata;
		if (boneMetadata == null)
		{
			return;
		}
		foreach (ApproximateBoneBounds item in Mesh.Asset.Data.CalculateApproximateBoneBounds(boneMetadata))
		{
			Slot slot = Bones[item.rootBoneIndex].AddSlot("BoneBoundsVisual");
			slot.PersistentSelf = false;
			slot.LocalPosition = item.bounds.center;
			OverlayFresnelMaterial overlayFresnelMaterial = slot.AttachComponent<OverlayFresnelMaterial>();
			overlayFresnelMaterial.Slot.PersistentSelf = false;
			overlayFresnelMaterial.BlendMode.Value = BlendMode.Alpha;
			colorX a = RandomX.Hue;
			overlayFresnelMaterial.FrontNearColor.Value = a * 0.5f;
			overlayFresnelMaterial.FrontFarColor.Value = a;
			slot.AttachSphere(item.bounds.radius, overlayFresnelMaterial, collider: false);
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void ResetBonesToBindPoses(IButton button, ButtonEventData eventData)
	{
		if (Mesh.Asset?.Data == null)
		{
			return;
		}
		MeshX data = Mesh.Asset.Data;
		Slot slot = ComputedRendererRoot;
		if (slot == null)
		{
			slot = base.Slot;
		}
		int num = Bones.IndexOf(slot);
		float4x4 b;
		if (num >= 0)
		{
			b = data.GetBone(num).BindPose;
			b = slot.LocalToGlobal * b;
		}
		else
		{
			b = slot.LocalToGlobal;
		}
		for (int i = 0; i < data.BoneCount; i++)
		{
			Bone bone = data.GetBone(i);
			Slot slot2 = TryGetBone(i);
			if (slot2 != null)
			{
				float4x4 float4x = b * bone.BindPose.AffineInverseFast;
				slot2.GlobalPosition = float4x.DecomposedPosition;
				slot2.GlobalRotation = float4x.DecomposedRotation;
				slot2.GlobalScale = float4x.DecomposedScale;
			}
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void VisualizeBoneBounds(IButton button, ButtonEventData eventData)
	{
		StaticMesh staticMesh = Mesh.Target as StaticMesh;
		if (Mesh.Asset == null || staticMesh == null)
		{
			return;
		}
		IReadOnlyList<BoneMetadata> boneMetadata = Mesh.Asset.BoneMetadata;
		if (boneMetadata == null)
		{
			return;
		}
		for (int i = 0; i < MathX.Min(boneMetadata.Count, Bones.Count); i++)
		{
			BoneMetadata boneMetadata2 = boneMetadata[i];
			Slot slot = Bones[i];
			if (boneMetadata2.bounds.IsValid)
			{
				Slot slot2 = slot.AddSlot("BoneBoundsVisual");
				slot2.PersistentSelf = false;
				slot2.LocalPosition = boneMetadata2.bounds.Center;
				OverlayFresnelMaterial overlayFresnelMaterial = slot2.AttachComponent<OverlayFresnelMaterial>();
				overlayFresnelMaterial.Slot.PersistentSelf = false;
				overlayFresnelMaterial.BlendMode.Value = BlendMode.Alpha;
				colorX a = RandomX.Hue;
				overlayFresnelMaterial.FrontNearColor.Value = a * 0.5f;
				overlayFresnelMaterial.FrontFarColor.Value = a;
				slot2.AttachBox(boneMetadata2.bounds.Size, overlayFresnelMaterial, collider: false);
			}
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void ClearBoundsVisuals(IButton button, ButtonEventData eventData)
	{
		foreach (Slot bone in Bones)
		{
			bone?.FindChild("BoneBoundsVisual")?.Destroy();
		}
	}

	private async Task SeparateOutBlendshapes()
	{
		StaticMesh provider = Mesh.Target as StaticMesh;
		if (Mesh.Asset == null || provider == null)
		{
			return;
		}
		await default(ToBackground);
		object _lock = new object();
		await Mesh.Asset.RequestReadLock(_lock).ConfigureAwait(continueOnCapturedContext: false);
		MeshX data = new MeshX(Mesh.Asset.Data);
		Mesh.Asset.ReleaseReadLock(_lock);
		Slot slot = ComputeRootBone();
		int rootBoneIndex = ((slot == null) ? (-1) : Bones.IndexOf(slot));
		MeshX split = data.SeparateGeometryUnaffectedByBlendshapes();
		if (split == null)
		{
			return;
		}
		List<int> newIndexes = new List<int>();
		List<int> splitIndexes = new List<int>();
		List<int> newBoneRemapIndex = new List<int>();
		List<int> splitBoneRemapIndex = new List<int>();
		data.StripEmptySubmeshes(newIndexes);
		split.StripEmptySubmeshes(splitIndexes);
		data.StripEmptyBones(newBoneRemapIndex, (Bone b) => b.Index == rootBoneIndex);
		split.StripEmptyBones(splitBoneRemapIndex, (Bone b) => b.Index == rootBoneIndex);
		Uri splitUrl = await base.Engine.LocalDB.SaveAssetAsync(split);
		Uri newUrl = await base.Engine.LocalDB.SaveAssetAsync(data);
		BoundingBox newBounds = data.CalculateBoundingBox();
		BoundingBox splitBounds = split.CalculateBoundingBox();
		await default(ToWorld);
		provider.URL.Value = newUrl;
		SkinnedMeshRenderer skinnedMeshRenderer = base.Slot.AddSlot("Non-Blendshape Part").AttachComponent<SkinnedMeshRenderer>();
		skinnedMeshRenderer.Mesh.Target = base.World.AssetsSlot.AddSlot(base.Slot.Name + " - Blendshape Split").AttachStaticMesh(splitUrl);
		skinnedMeshRenderer.BoundsComputeMethod.Value = BoundsComputeMethod;
		Materials.EnsureMinimumCount(MathX.Max(newIndexes.Count, splitIndexes.Count));
		foreach (int item in splitIndexes)
		{
			skinnedMeshRenderer.Materials.Add(Materials[item]);
		}
		foreach (Slot bone in Bones)
		{
			skinnedMeshRenderer.Bones.Add().Target = bone;
		}
		HandleStrippedBones(newBoneRemapIndex);
		skinnedMeshRenderer.HandleStrippedBones(splitBoneRemapIndex);
		for (int num = 0; num < newIndexes.Count; num++)
		{
			Materials[num] = Materials[newIndexes[num]];
		}
		Materials.EnsureExactCount(newIndexes.Count);
		float num2 = newBounds.Size.x * newBounds.Size.y * newBounds.Size.z;
		float num3 = splitBounds.Size.x * splitBounds.Size.y * splitBounds.Size.z;
		SkinnedMeshRenderer target;
		SkinnedMeshRenderer skinnedMeshRenderer2;
		if (num2 > num3)
		{
			target = this;
			skinnedMeshRenderer2 = skinnedMeshRenderer;
		}
		else
		{
			target = skinnedMeshRenderer;
			skinnedMeshRenderer2 = this;
		}
		skinnedMeshRenderer2.BoundsComputeMethod.Value = SkinnedBounds.Proxy;
		skinnedMeshRenderer2.ProxyBoundsSource.Target = target;
		SimpleAwayIndicator component = base.Slot.GetComponent<SimpleAwayIndicator>();
		if (component != null)
		{
			SimpleAwayIndicator simpleAwayIndicator = skinnedMeshRenderer.Slot.DuplicateComponent(component);
			simpleAwayIndicator.Renderer.Target = skinnedMeshRenderer;
			skinnedMeshRenderer.Slot.AttachComponent<AvatarUserReferenceAssigner>().References.Add(simpleAwayIndicator.User);
		}
		if (base.Slot.GetComponent<SimpleAvatarProtection>() != null)
		{
			skinnedMeshRenderer.Slot.AttachComponent<SimpleAvatarProtection>();
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void SeparateOutBlendshapes(IButton button, ButtonEventData eventData)
	{
		StaticMesh staticMesh = Mesh.Target as StaticMesh;
		if (Mesh.Asset == null || staticMesh == null)
		{
			return;
		}
		button.LabelText = this.GetLocalized("General.Processing");
		button.Enabled = false;
		StartTask(async delegate
		{
			await SeparateOutBlendshapes();
			if (!button.IsDestroyed)
			{
				button.LabelText = this.GetLocalized("General.Done");
				button.Enabled = true;
			}
		});
	}

	[SyncMethod(typeof(Delegate), null)]
	private void StripEmptyBlendshapes(IButton button, ButtonEventData eventData)
	{
		StaticMesh provider = Mesh.Target as StaticMesh;
		if (Mesh.Asset == null || provider == null)
		{
			return;
		}
		button.LabelText = this.GetLocalized("General.Processing");
		button.Enabled = false;
		StartTask(async delegate
		{
			await default(ToBackground);
			object _lock = new object();
			await Mesh.Asset.RequestReadLock(_lock).ConfigureAwait(continueOnCapturedContext: false);
			MeshX meshX = new MeshX(Mesh.Asset.Data);
			Mesh.Asset.ReleaseReadLock(_lock);
			List<int> removedBlendshapes = new List<int>();
			meshX.StripEmptyBlendshapes(removedBlendshapes);
			Uri uri = ((removedBlendshapes.Count <= 0) ? null : (await base.Engine.LocalDB.SaveAssetAsync(meshX)));
			Uri uri2 = uri;
			await default(ToWorld);
			if (uri2 != null)
			{
				provider.URL.Value = uri2;
				foreach (int item in removedBlendshapes)
				{
					BlendShapeWeights.RemoveAt(item);
				}
			}
			if (!button.IsDestroyed)
			{
				button.LabelText = this.GetLocalized("Inspector.SkinnedMesh.StripBlendshapesResult", null, "n", removedBlendshapes.Count);
				button.Enabled = true;
			}
		});
	}

	private async Task<bool> BakeBlendshapes(Dictionary<int, float> weights)
	{
		if (weights.Count == 0)
		{
			return false;
		}
		return await ProcessMesh(async delegate(MeshX data)
		{
			data.BakeBlendShapes(weights);
			return data;
		}, async delegate
		{
			foreach (KeyValuePair<int, float> item in weights.Reverse())
			{
				BlendShapeWeights.RemoveAt(item.Key);
			}
			return true;
		});
	}

	[SyncMethod(typeof(Delegate), null)]
	private void BakeNonDrivenBlendshapes(IButton button, ButtonEventData eventData)
	{
		StaticMesh staticMesh = Mesh.Target as StaticMesh;
		if (Mesh.Asset == null || staticMesh == null)
		{
			return;
		}
		button.LabelText = this.GetLocalized("General.Processing");
		button.Enabled = false;
		Dictionary<int, float> blendshapeWeights = GetBlendshapeWeights((IField<float> f) => !f.IsDriven);
		StartTask(async delegate
		{
			bool flag = await BakeBlendshapes(blendshapeWeights);
			if (!button.IsDestroyed)
			{
				button.LabelText = this.GetLocalized("Inspector.SkinnedMesh.BakeBlendShapeResult", null, "n", flag);
				button.Enabled = true;
			}
		});
	}

	[SyncMethod(typeof(Delegate), null)]
	private void BakeToStaticMesh(IButton button, ButtonEventData eventData)
	{
		StaticMesh staticMesh = Mesh.Target as StaticMesh;
		if (Mesh.Asset == null || staticMesh == null)
		{
			return;
		}
		button.LabelText = this.GetLocalized("General.Processing");
		button.Enabled = false;
		Dictionary<int, float> blendshapeWeights = GetBlendshapeWeights();
		float4x4[] boneTransforms = GetBoneTransforms();
		StartTask(async delegate
		{
			await default(ToBackground);
			_ = Mesh.Asset.BoneMetadata;
			object _lock = new object();
			await Mesh.Asset.RequestReadLock(_lock).ConfigureAwait(continueOnCapturedContext: false);
			MeshX meshX = new MeshX(Mesh.Asset.Data);
			Mesh.Asset.ReleaseReadLock(_lock);
			meshX.BakeBlendShapes(blendshapeWeights);
			if (boneTransforms.Length != 0)
			{
				meshX.BakeSkinnedMesh(boneTransforms);
			}
			Uri uri = await base.Engine.LocalDB.SaveAssetAsync(meshX).ConfigureAwait(continueOnCapturedContext: false);
			await default(ToWorld);
			if (uri != null)
			{
				StaticMesh target = Mesh.Target.Slot.AttachStaticMesh(uri);
				MeshRenderer meshRenderer = base.Slot.AttachComponent<MeshRenderer>();
				meshRenderer.Mesh.Target = target;
				foreach (IAssetProvider<Material> material in Materials)
				{
					meshRenderer.Materials.Add(material);
				}
				Destroy();
			}
			else if (!button.IsDestroyed)
			{
				button.Enabled = true;
			}
		});
	}

	[SyncMethod(typeof(Delegate), null)]
	private void MergeBlendshapes(IButton button, ButtonEventData eventData)
	{
		StaticMesh provider = Mesh.Target as StaticMesh;
		if (Mesh.Asset == null || provider == null)
		{
			return;
		}
		button.LabelText = this.GetLocalized("General.Processing");
		button.Enabled = false;
		GetBlendshapeWeights();
		GetBoneTransforms();
		StartTask(async delegate
		{
			await default(ToBackground);
			_ = Mesh.Asset.BoneMetadata;
			object _lock = new object();
			await Mesh.Asset.RequestReadLock(_lock).ConfigureAwait(continueOnCapturedContext: false);
			MeshX meshX = new MeshX(Mesh.Asset.Data);
			Mesh.Asset.ReleaseReadLock(_lock);
			meshX.MergeBlendshapes();
			Uri uri = await base.Engine.LocalDB.SaveAssetAsync(meshX).ConfigureAwait(continueOnCapturedContext: false);
			await default(ToWorld);
			if (uri != null)
			{
				provider.URL.Value = uri;
			}
			BlendShapeWeights.EnsureExactCount(1);
			if (!button.IsDestroyed)
			{
				button.Enabled = true;
			}
		});
	}

	[SyncMethod(typeof(Delegate), null)]
	private void StripEmptyBones(IButton button, ButtonEventData eventData)
	{
		StaticMesh provider = Mesh.Target as StaticMesh;
		if (Mesh.Asset == null || provider == null)
		{
			return;
		}
		button.LabelText = this.GetLocalized("General.Processing");
		button.Enabled = false;
		StartTask(async delegate
		{
			await default(ToBackground);
			IReadOnlyList<BoneMetadata> metadata = Mesh.Asset.BoneMetadata;
			object _lock = new object();
			await Mesh.Asset.RequestReadLock(_lock).ConfigureAwait(continueOnCapturedContext: false);
			MeshX meshX = new MeshX(Mesh.Asset.Data);
			Mesh.Asset.ReleaseReadLock(_lock);
			List<int> remapIndex = new List<int>();
			int strippedCount = meshX.StripEmptyBones(metadata, remapIndex);
			Uri uri = ((strippedCount <= 0) ? null : (await base.Engine.LocalDB.SaveAssetAsync(meshX)));
			Uri uri2 = uri;
			await default(ToWorld);
			if (uri2 != null)
			{
				provider.URL.Value = uri2;
				HandleStrippedBones(remapIndex);
			}
			if (!button.IsDestroyed)
			{
				button.LabelText = this.GetLocalized("Inspector.SkinnedMesh.StripBonesResult", null, "n", strippedCount);
				button.Enabled = true;
			}
		});
	}

	[SyncMethod(typeof(Delegate), null)]
	private void ComputeExplicitBoundsFromPose(IButton button, ButtonEventData eventData)
	{
		ComputeExplicitBounds(button, eventData, extend: false);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void ExtendExplicitBoundsFromPose(IButton button, ButtonEventData eventData)
	{
		ComputeExplicitBounds(button, eventData, extend: true);
	}

	private void ComputeExplicitBounds(IButton button, ButtonEventData eventData, bool extend)
	{
		StaticMesh staticMesh = Mesh.Target as StaticMesh;
		if (Mesh.Asset == null || staticMesh == null)
		{
			return;
		}
		button.LabelText = this.GetLocalized("General.Processing");
		button.Enabled = false;
		StartTask(async delegate
		{
			float4x4[] boneTransforms = GetBoneTransforms();
			await default(ToBackground);
			object _lock = new object();
			await Mesh.Asset.RequestReadLock(_lock).ConfigureAwait(continueOnCapturedContext: false);
			BoundingBox bounds = Mesh.Asset.Data.ComputeSkinnedBounds(boneTransforms);
			Mesh.Asset.ReleaseReadLock(_lock);
			await default(ToWorld);
			BoundsComputeMethod.Value = SkinnedBounds.Explicit;
			if (extend)
			{
				bounds.Encapsulate(ExplicitLocalBounds.Value);
				ExplicitLocalBounds.Value = bounds;
			}
			else
			{
				ExplicitLocalBounds.Value = bounds;
			}
			if (!button.IsDestroyed)
			{
				button.LabelText = this.GetLocalized("General.Done");
				button.Enabled = true;
			}
		});
	}

	[SyncMethod(typeof(Delegate), null)]
	private void SortBlendshapesByName(IButton button, ButtonEventData eventData)
	{
		SortBlendshapes(button, eventData, delegate(MeshX mesh, List<int> remappedOrder)
		{
			mesh.SortBlendshapesByName(remappedOrder);
		});
	}

	[SyncMethod(typeof(Delegate), null)]
	private void SortBlendshapesByNameLength(IButton button, ButtonEventData eventData)
	{
		SortBlendshapes(button, eventData, delegate(MeshX mesh, List<int> remappedOrder)
		{
			mesh.SortBlendshapesByNameLength(remappedOrder);
		});
	}

	private void SortBlendshapes(IButton button, ButtonEventData eventData, Action<MeshX, List<int>> process)
	{
		StaticMesh provider = Mesh.Target as StaticMesh;
		if (Mesh.Asset == null || provider == null)
		{
			return;
		}
		button.LabelText = this.GetLocalized("General.Processing");
		button.Enabled = false;
		StartTask(async delegate
		{
			Dictionary<int, float> originalWeights = GetBlendshapeWeights();
			await default(ToBackground);
			object _lock = new object();
			await Mesh.Asset.RequestReadLock(_lock).ConfigureAwait(continueOnCapturedContext: false);
			MeshX meshX = new MeshX(Mesh.Asset.Data);
			Mesh.Asset.ReleaseReadLock(_lock);
			List<int> remappedOrder = new List<int>();
			process(meshX, remappedOrder);
			Uri uri = await base.Engine.LocalDB.SaveAssetAsync(meshX);
			await default(ToWorld);
			provider.URL.Value = uri;
			Dictionary<IWorldElement, IWorldElement> dictionary = new Dictionary<IWorldElement, IWorldElement>();
			int count = BlendShapeWeights.Count;
			for (int i = 0; i < count; i++)
			{
				BlendShapeWeights.Add();
			}
			for (int j = 0; j < remappedOrder.Count; j++)
			{
				dictionary.Add(BlendShapeWeights.GetElement(j), BlendShapeWeights.GetElement(count + remappedOrder[j]));
			}
			base.World.ReplaceReferenceTargets(dictionary, nullIfIncompatible: false);
			for (int k = 0; k < count; k++)
			{
				BlendShapeWeights.RemoveAt(0);
			}
			foreach (KeyValuePair<int, float> item in originalWeights)
			{
				Sync<float> element = BlendShapeWeights.GetElement(remappedOrder[item.Key]);
				if (element.CanWrite)
				{
					element.Value = item.Value;
				}
			}
			if (!button.IsDestroyed)
			{
				button.LabelText = this.GetLocalized("General.Done");
				button.Enabled = true;
			}
		});
	}

	private void HandleStrippedBones(List<int> remapIndex)
	{
		for (int num = MathX.Min(Bones.Count, remapIndex.Count) - 1; num >= 0; num--)
		{
			if (remapIndex[num] < 0)
			{
				Bones.RemoveAt(num);
			}
		}
	}

	protected override MeshRenderer AttachSplitMesh(Slot root, MeshX splitMesh)
	{
		SkinnedMeshRenderer skinnedMeshRenderer = root.AttachComponent<SkinnedMeshRenderer>();
		skinnedMeshRenderer.BoundsComputeMethod.Value = BoundsComputeMethod;
		foreach (Slot bone in Bones)
		{
			skinnedMeshRenderer.Bones.Add().Target = bone;
		}
		BlendShapeWeights.EnsureExactCount(splitMesh.BlendShapeCount);
		return skinnedMeshRenderer;
	}

	public string GetMemberName(SyncElement element)
	{
		if (element.Parent == BlendShapeWeights)
		{
			int index = BlendShapeWeights.IndexOfElement((Sync<float>)element);
			return BlendShapeName(index);
		}
		return null;
	}

	private async Task<bool> ProcessMesh(Func<MeshX, Task<MeshX>> process, Func<MeshX, Task<bool>> finalize = null)
	{
		StaticMesh provider = Mesh.Target as StaticMesh;
		if (Mesh.Asset == null || provider == null)
		{
			return false;
		}
		if (provider.Asset?.Data == null)
		{
			return false;
		}
		await default(ToBackground);
		object _lock = new object();
		await Mesh.Asset.RequestReadLock(_lock).ConfigureAwait(continueOnCapturedContext: false);
		MeshX data = new MeshX(Mesh.Asset.Data);
		Mesh.Asset.ReleaseReadLock(_lock);
		if (await process(data) == null)
		{
			return false;
		}
		Uri uri = await base.Engine.LocalDB.SaveAssetAsync(data);
		await default(ToWorld);
		provider.URL.Value = uri;
		if (finalize != null)
		{
			await finalize(data);
		}
		return true;
	}

	protected override void OnCommonUpdate()
	{
		base.OnCommonUpdate();
		SkinnedBounds skinnedBounds = BoundsComputeMethod.Value;
		if (skinnedBounds == SkinnedBounds.Static && base.Slot.IsUnderLocalUser)
		{
			skinnedBounds = SkinnedBounds.FastDisjointRootApproximate;
		}
		if (skinnedBounds == SkinnedBounds.SlowRealtimeAccurate && !base.IsRenderingSupported)
		{
			skinnedBounds = SkinnedBounds.MediumPerBoneApproximate;
		}
		var (boundingBox, slot) = ComputeLocalBounds(skinnedBounds);
		if (!(boundingBox != ComputedBounds) && slot == ComputedBoundsSpace && skinnedBounds != SkinnedBounds.SlowRealtimeAccurate)
		{
			return;
		}
		ComputedBounds = boundingBox;
		ComputedBoundsSpace = slot;
		if (base.IsRenderingSupported)
		{
			if (skinnedBounds != SkinnedBounds.SlowRealtimeAccurate)
			{
				RendererComputedGlobalBounds = null;
				Manager.RegisterUpdatedBounds(this);
			}
			else
			{
				Manager.RegisterRealtimeBoundsToCompute(this);
			}
		}
	}

	public (BoundingBox, Slot) ComputeLocalBounds(SkinnedBounds method)
	{
		switch (method)
		{
		case SkinnedBounds.Explicit:
			return (ExplicitLocalBounds, ComputedRendererRoot);
		case SkinnedBounds.Static:
		{
			if (_staticBounds == null)
			{
				_staticBounds = new List<BoundingBox>();
			}
			if (_staticBounds.Count == 0)
			{
				ComputeApproximateStaticBounds(_staticBounds);
			}
			if (_staticBounds.Count == 0)
			{
				return (BoundingBox.Empty(), base.Slot);
			}
			Slot computedRendererRoot = ComputedRendererRoot;
			List<BoneNode> computedBoneHierarchies = ComputedBoneHierarchies;
			if (_staticBounds.Count == 1 && (computedBoneHierarchies == null || computedBoneHierarchies.Count == 0))
			{
				return (_staticBounds[0], computedRendererRoot);
			}
			if (_staticBounds.Count != computedBoneHierarchies?.Count)
			{
				return (BoundingBox.Empty(), base.Slot);
			}
			BoundingBox item3 = BoundingBox.Empty();
			for (int i = 0; i < _staticBounds.Count; i++)
			{
				BoundingBox box = _staticBounds[i];
				if (box.IsValid)
				{
					Slot bone = computedBoneHierarchies[i].bone;
					if (bone != computedRendererRoot)
					{
						box = box.Transform(bone.GetLocalToSpaceMatrix(computedRendererRoot));
					}
					item3.Encapsulate(box);
				}
			}
			return (item3, ComputedRendererRoot);
		}
		case SkinnedBounds.Proxy:
		{
			BoundingBox item2 = ProxyBoundsSource.Target?.ComputedBounds ?? BoundingBox.Empty();
			Slot slot = ProxyBoundsSource.Target?.ComputedRendererRoot;
			if (!item2.IsValid || slot == null || slot.IsRemoved)
			{
				return (item2, base.Slot);
			}
			item2 = item2.Transform(slot.GetLocalToSpaceMatrix(ComputedRendererRoot));
			return (item2, ComputedRendererRoot);
		}
		case SkinnedBounds.FastDisjointRootApproximate:
			return (ComputeBoundsFastDisjointRootApproximate(), ComputedRendererRoot);
		case SkinnedBounds.MediumPerBoneApproximate:
			return (ComputeBoundsMediumPerBoneApproximate(), ComputedRendererRoot);
		case SkinnedBounds.SlowRealtimeAccurate:
		{
			BoundingBox item = RendererComputedGlobalBounds ?? BoundingBox.Empty();
			if (item.IsValid)
			{
				return (item.Transform(base.Slot.GlobalToLocal), base.Slot);
			}
			return (item, base.Slot);
		}
		default:
			return (BoundingBox.Empty(), base.Slot);
		}
	}

	public void ComputeApproximateStaticBounds(List<BoundingBox> boundsList)
	{
		List<BoneNode> computedBoneHierarchies = ComputedBoneHierarchies;
		Mesh asset = Mesh.Asset;
		IReadOnlyList<ApproximateBoneBounds> readOnlyList = asset?.ApproximateBoneBounds;
		if (asset?.Data == null)
		{
			return;
		}
		if (asset.Data.BoneCount == 0 || computedBoneHierarchies == null || computedBoneHierarchies.Count == 0)
		{
			boundsList.Add(asset.Bounds);
		}
		else
		{
			if (readOnlyList == null)
			{
				return;
			}
			MeshX data = asset.Data;
			foreach (BoneNode item in computedBoneHierarchies)
			{
				int num = Bones.IndexOf(item.bone);
				if (num < 0 || num >= data.BoneCount)
				{
					boundsList.Add(BoundingBox.Empty());
					continue;
				}
				float4x4 a = ((num < 0) ? float4x4.Identity : data.GetBone(num).BindPose);
				BoundingSphere sphere = new BoundingSphere(float3.Zero, 0f);
				foreach (ApproximateBoneBounds item2 in readOnlyList)
				{
					ApproximateBoneBounds current2 = item2;
					float4x4 float4x = a * asset.Data.GetBone(current2.rootBoneIndex).BindPose.AffineInverseFast;
					float3 center = float4x.TransformPoint3x4(in current2.bounds.center);
					float radius = float4x.TransformScale(current2.bounds.radius);
					sphere.Encapsulate(new BoundingSphere(in center, radius));
				}
				boundsList.Add(BoundingBox.FromBoundingSphere(sphere));
			}
		}
	}

	public BoundingBox ComputeBoundsFastDisjointRootApproximate()
	{
		IReadOnlyList<ApproximateBoneBounds> readOnlyList = Mesh.Asset?.ApproximateBoneBounds;
		if (readOnlyList == null)
		{
			return BoundingBox.Empty();
		}
		Slot computedRendererRoot = ComputedRendererRoot;
		if (computedRendererRoot == null)
		{
			return BoundingBox.Empty();
		}
		BoundingBox result = BoundingBox.Empty();
		foreach (ApproximateBoneBounds item in readOnlyList)
		{
			Slot slot = TryGetBone(item.rootBoneIndex);
			if (slot != null)
			{
				float3 localPoint = item.bounds.center;
				float radius = item.bounds.radius;
				localPoint = slot.LocalPointToGlobal(in localPoint);
				radius = slot.LocalScaleToGlobal(radius);
				localPoint = computedRendererRoot.GlobalPointToLocal(in localPoint);
				radius = computedRendererRoot.GlobalScaleToLocal(radius);
				result.Encapsulate(new BoundingSphere(in localPoint, radius));
			}
		}
		return result;
	}

	public BoundingBox ComputeBoundsMediumPerBoneApproximate()
	{
		IReadOnlyList<BoneMetadata> readOnlyList = Mesh.Asset?.BoneMetadata;
		if (readOnlyList == null)
		{
			return BoundingBox.Empty();
		}
		Slot computedRendererRoot = ComputedRendererRoot;
		if (computedRendererRoot == null)
		{
			return BoundingBox.Empty();
		}
		BoundingBox result = BoundingBox.Empty();
		for (int i = 0; i < readOnlyList.Count; i++)
		{
			BoundingBox bounds = readOnlyList[i].bounds;
			Slot slot = TryGetBone(i);
			if (slot != null && bounds.IsValid)
			{
				for (int j = 0; j < 8; j++)
				{
					result.Encapsulate(computedRendererRoot.GlobalPointToLocal(slot.LocalPointToGlobal(bounds.GetVertexPoint(j))));
				}
			}
		}
		return result;
	}

	protected override void RegisterAddedOrRemoved()
	{
		Manager.RenderableAddedOrRemoved(this);
	}

	protected override void RegisterChanged()
	{
		Manager.RegisterChangedMeshRenderer(this);
	}

	public override void InitRenderableState()
	{
		base.InitRenderableState();
		UpdateAllBlendshapes = true;
		LastMeshAssetId = -1;
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		BoundsComputeMethod = new Sync<SkinnedBounds>();
		ProxyBoundsSource = new SyncRef<SkinnedMeshRenderer>();
		ExplicitLocalBounds = new Sync<BoundingBox>();
		Bones = new SyncRefList<Slot>();
		BlendShapeWeights = new SyncFieldList<float>();
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
			9 => BoundsComputeMethod, 
			10 => ProxyBoundsSource, 
			11 => ExplicitLocalBounds, 
			12 => Bones, 
			13 => BlendShapeWeights, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public new static SkinnedMeshRenderer __New()
	{
		return new SkinnedMeshRenderer();
	}
}
