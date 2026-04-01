using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Elements.Core;
using Elements.Data;

namespace FrooxEngine.UIX;

[Category(new string[] { "UIX" })]
[DefaultUpdateOrder(100000)]
[OldTypeName("FrooxEngine.UI.Canvas", null)]
public class Canvas : Component, ITouchable, IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable, ITouchGrabbable, IBounded, ILaserInteractionModifier, IInteractionTarget, IContextMenuActionReceiver, ISecondaryActionReceiver, IAxisActionReceiver, IUIInterface, IRenderable
{
	public class InteractionData : IPoolable
	{
		public readonly List<Predicate<IUIInteractable>> filters = new List<Predicate<IUIInteractable>>();

		private HashSet<IUIHoverable> _currentHovers = new HashSet<IUIHoverable>();

		private IUIInteractable _currentInteractable;

		private EventState _currentInteractableHover;

		private EventState _currentInteractableTouch;

		internal readonly List<IUIInteractable> currentInteractables = new List<IUIInteractable>();

		internal IUIInteractable touchLock;

		public float2 position { get; internal set; }

		public float2 lastPosition { get; internal set; }

		public double lastTime { get; internal set; }

		public EventState hover { get; internal set; }

		public EventState touch { get; internal set; }

		public double initialHoverTime { get; internal set; }

		public double initialTouchTime { get; internal set; }

		public float2 initialTouchPosition { get; internal set; }

		public TouchType type { get; internal set; }

		public TouchSource source { get; internal set; }

		public float3 tip { get; internal set; }

		public float3 direction { get; internal set; }

		internal IUIInteractable CurrentInteractable
		{
			get
			{
				if (_currentInteractable == null || _currentInteractable.IsRemoved)
				{
					return null;
				}
				return _currentInteractable;
			}
		}

		public void SetCurrentInteractable(IUIInteractable interactable)
		{
			if (CurrentInteractable != interactable)
			{
				FinishCurrentInteraction();
			}
			_currentInteractable = interactable;
			_currentInteractableHover = hover;
			_currentInteractableTouch = touch;
		}

		public void FinishCurrentInteraction()
		{
			if (CurrentInteractable == null)
			{
				_currentInteractable = null;
				return;
			}
			EventState eventState = hover;
			EventState eventState2 = touch;
			hover = _currentInteractableHover;
			touch = _currentInteractableTouch;
			hover = EventState.End;
			if (touch != EventState.None)
			{
				touch = EventState.End;
			}
			_currentInteractable.ProcessEvent(this);
			hover = eventState;
			touch = eventState2;
			_currentInteractable = null;
		}

		public void UpdateHovers(List<IUIHoverable> hovers, bool debug = false)
		{
			HashSet<IUIHoverable> hashSet = Pool.BorrowHashSet<IUIHoverable>();
			List<IUIHoverable> list = Pool.BorrowList<IUIHoverable>();
			foreach (IUIHoverable currentHover in _currentHovers)
			{
				hashSet.Add(currentHover);
			}
			foreach (IUIHoverable hover in hovers)
			{
				if (_currentHovers.Add(hover))
				{
					if (debug)
					{
						UniLog.Log("BEGIN HOVER:\n" + hover);
					}
					list.Add(hover);
				}
				else
				{
					hashSet.Remove(hover);
				}
			}
			foreach (IUIHoverable item in hashSet)
			{
				_currentHovers.Remove(item);
				if (!item.IsRemoved)
				{
					if (debug)
					{
						UniLog.Log("END HOVER:\n" + item);
					}
					item.EndHover();
				}
			}
			Pool.Return(ref hashSet);
			foreach (IUIHoverable item2 in list)
			{
				item2.BeginHover();
			}
			Pool.Return(ref list);
		}

		public void EndAllHovers(bool debug = false)
		{
			foreach (IUIHoverable currentHover in _currentHovers)
			{
				if (!currentHover.IsRemoved)
				{
					if (debug)
					{
						UniLog.Log("END HOVER (all):\n" + currentHover);
					}
					currentHover.EndHover();
				}
			}
			_currentHovers.Clear();
		}

		public InteractionData()
		{
			Reset();
		}

		private void Reset()
		{
			position = default(float2);
			lastPosition = default(float2);
			lastTime = -1.0;
			hover = EventState.None;
			touch = EventState.None;
			initialHoverTime = -1.0;
			initialTouchTime = -1.0;
			initialTouchPosition = default(float2);
			type = TouchType.Physical;
			source = null;
			tip = default(float3);
			direction = default(float3);
			_currentInteractable = null;
			_currentInteractableHover = EventState.None;
			_currentInteractableTouch = EventState.None;
			currentInteractables.Clear();
			touchLock = null;
			filters.Clear();
			_currentHovers.Clear();
		}

		void IPoolable.Clean()
		{
			Reset();
		}
	}

	public const int DEFAULT_STARTING_OFFSET = -32000;

	public readonly Sync<float2> Size;

	public readonly Sync<bool> EditModeOnly;

	public readonly Sync<bool> AcceptRemoteTouch;

	public readonly Sync<bool> AcceptPhysicalTouch;

	public readonly Sync<bool> AcceptExistingTouch;

	public readonly Sync<bool> HighPriorityIntegration;

	public readonly Sync<bool> IgnoreTouchesFromBehind;

	public readonly Sync<bool> BlockAllInteractions;

	public readonly Sync<bool> LaserPassThrough;

	[Range(0.1f, 10f, "0.00")]
	public readonly Sync<float> PixelScale;

	[Range(1f, 1000f, "0.00")]
	public readonly Sync<float> UnitScale;

	protected readonly SyncRef<RectTransform> _rootRect;

	public readonly SyncRef<BoxCollider> Collider;

	public readonly Sync<Culling> DefaultCulling;

	protected readonly FieldDrive<float3> _colliderSize;

	protected readonly FieldDrive<float3> _colliderOffset;

	public readonly Sync<int> StartingOffset;

	public readonly Sync<int> StartingMaskDepth;

	private volatile bool _updateRunning;

	private volatile bool _runAgain;

	private bool _updateScheduled;

	private Func<Task> _computeCanvasUpdate;

	private Action _finishCanvasUpdate;

	private bool _pruneDisabledTransforms;

	private bool _graphicChunksDirty;

	private World _cachedWorld;

	private RectTransform _root;

	private float2 _size;

	private float _pixelScale;

	private float _unitScale;

	private List<GraphicsChunk> _graphicChunks = new List<GraphicsChunk>();

	private List<GraphicsChunk> _removedGraphicChunks = new List<GraphicsChunk>();

	private List<GraphicsChunk> _chunksToDisable = new List<GraphicsChunk>();

	private ConcurrentBag<RectTransform> _autoInvalidateRects = new ConcurrentBag<RectTransform>();

	private Slot _rootSlot;

	private GraphicsChunk _rootChunk;

	private GraphicsChunk _debugChunk;

	private List<RectTransform> _dataModelDirtyTransforms = new List<RectTransform>();

	private List<RectTransform> _computeDirtyTransforms = new List<RectTransform>();

	private List<RectTransform> _removedTransforms = new List<RectTransform>();

	private List<RectTransform> _changedChildrenTransforms = new List<RectTransform>();

	private List<Action> _postCycleActions = new List<Action>();

	private List<Action> _removals = new List<Action>();

	private Dictionary<Component, InteractionData> _currentInteractions = new Dictionary<Component, InteractionData>();

	public override int Version => 2;

	public bool IsDeveloper { get; private set; }

	public bool ShouldBeEnabled
	{
		get
		{
			if (!base.Enabled)
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

	public bool RenderingLocallyUnblocked { get; set; }

	public World CachedWorld => _cachedWorld;

	public int UpdateCycleIndex { get; private set; }

	public int LastRendererCount { get; private set; }

	public int LastChunkCount { get; private set; }

	public DateTime UpdateCycleStart { get; private set; }

	public string DebugCanvasIdentity => $"Canvas {GetHashCode()} - {base.Slot?.Name}, UpdateCyle: {UpdateCycleIndex}";

	public float ComputePixelScale => _pixelScale;

	public float ComputeUnitScale => _unitScale;

	public Slot ComputeRootSlot => _rootSlot;

	public RectTransform RootRect => _root;

	public bool CanTouchOutOfSight => false;

	public bool AcceptsExistingTouch => AcceptExistingTouch.Value;

	public bool HasBoundingBox => true;

	public bool IsBoundingBoxAvailable => true;

	public BoundingBox GlobalBoundingBox => LocalBoundingBox.Transform(base.Slot.LocalToGlobal);

	public BoundingBox LocalBoundingBox => BoundingBox.CenterSize(float3.Zero, (float3)Size.Value);

	public virtual int InteractionTargetPriority => 0;

	public float3 UI_FacingDirection => -base.Slot.Forward;

	public float3 UI_UpDirection => base.Slot.Up;

	public float3 UI_Center => base.Slot.GlobalPosition;

	public float2 UI_Size => Size.Value * base.Slot.LocalScaleToGlobal(1f);

	public event Action<Canvas> OnLocalSubmissionFinished;

	public void MarkDeveloper()
	{
		IsDeveloper = true;
	}

	protected override void OnAwake()
	{
		base.OnAwake();
		StartingOffset.Value = -32000;
		Size.Value = new float2(1920f, 1080f);
		AcceptRemoteTouch.Value = true;
		AcceptPhysicalTouch.Value = true;
		LaserPassThrough.Value = false;
		IgnoreTouchesFromBehind.Value = true;
		PixelScale.Value = 1f;
		UnitScale.Value = 1f;
		_computeCanvasUpdate = ComputeCanvasUpdate;
		_finishCanvasUpdate = FinishCanvasUpdate;
		DefaultCulling.Value = Culling.Back;
		base.Slot.ForeachComponentInChildren(delegate(RectTransform r)
		{
			r.MarkStructureChanged();
		});
		if (!base.Slot.IsLocalElement)
		{
			base.Slot.ActiveUserRootChanged += Slot_ActiveUserRootChanged;
		}
	}

	private void Slot_ActiveUserRootChanged(Slot slot)
	{
		MarkChangeDirty();
	}

	protected override void OnAttach()
	{
		base.OnAttach();
		_rootRect.Target = base.Slot.GetComponentOrAttach<RectTransform>();
		BoxCollider boxCollider = base.Slot.AttachComponent<BoxCollider>();
		Collider.Target = boxCollider;
		_colliderSize.Target = boxCollider.Size;
		_colliderOffset.Target = boxCollider.Offset;
		boxCollider.Size.Value = float3.Zero;
		boxCollider.Offset.Value = float3.Zero;
	}

	protected override void OnChanges()
	{
		if (!ShouldBeEnabled)
		{
			if (_rootSlot != null)
			{
				_rootSlot.ActiveSelf = false;
			}
			_updateScheduled = true;
			return;
		}
		if (!base.Slot.IsActive)
		{
			_updateScheduled = true;
			return;
		}
		if (_rootSlot != null)
		{
			_rootSlot.ActiveSelf = true;
		}
		_updateScheduled = false;
		lock (this)
		{
			if (_updateRunning)
			{
				_runAgain = true;
				return;
			}
			_updateRunning = true;
		}
		StartCanvasUpdate();
	}

	protected override void OnActivated()
	{
		base.OnActivated();
		if (_updateScheduled)
		{
			MarkChangeDirty();
		}
	}

	protected override void OnDestroy()
	{
		if (base.Slot.GetComponentInParents((Canvas c) => c != this) != null)
		{
			base.Slot.ForeachComponentInChildren(delegate(RectTransform r)
			{
				r.MarkStructureChanged();
			});
		}
		base.OnDestroy();
	}

	protected override void OnDispose()
	{
		if (!base.Slot.IsLocalElement)
		{
			base.Slot.ActiveUserRootChanged -= Slot_ActiveUserRootChanged;
		}
		lock (this)
		{
			if (!_updateRunning)
			{
				FinishRemainingRemovals();
				CleanupChunks();
				CleanupData();
			}
		}
		this.OnLocalSubmissionFinished = null;
		base.OnDispose();
	}

	private void StartCanvasUpdate()
	{
		UpdateCycleIndex++;
		UpdateCycleStart = DateTime.UtcNow;
		_cachedWorld = base.World;
		foreach (Action removal in _removals)
		{
			removal();
		}
		_removals.Clear();
		PrepareCanvasUpdate();
		StartTask(_computeCanvasUpdate);
	}

	private void EnsureRootTransform()
	{
		_root = base.Slot.GetComponentOrAttach<RectTransform>();
		_rootRect.Target = _root;
	}

	private void PrepareCanvasUpdate()
	{
		if (_rootSlot == null)
		{
			_rootSlot = base.Slot.AddLocalSlot(null);
		}
		_root = _rootRect.Target;
		_size = Size.Value * UnitScale.Value;
		_pixelScale = PixelScale.Value;
		_unitScale = UnitScale.Value;
		if (_root == null)
		{
			EnsureRootTransform();
		}
		foreach (RectTransform removedTransform in _removedTransforms)
		{
			if (removedTransform.HasGraphicsChunk)
			{
				_removedGraphicChunks.Add(removedTransform.GraphicsChunk);
				_graphicChunks.Remove(removedTransform.GraphicsChunk);
				_graphicChunksDirty = true;
			}
			removedTransform.FinishRemove();
		}
		_removedTransforms.Clear();
		_pruneDisabledTransforms = false;
		for (int i = 0; i < _dataModelDirtyTransforms.Count; i++)
		{
			_pruneDisabledTransforms |= !_dataModelDirtyTransforms[i].PrepareCompute();
		}
		for (int j = 0; j < _dataModelDirtyTransforms.Count; j++)
		{
			RectTransform rectTransform = _dataModelDirtyTransforms[j];
			if (!_pruneDisabledTransforms || !ShouldPrune(rectTransform))
			{
				rectTransform.ClearDataModelFlags();
			}
		}
		foreach (RectTransform changedChildrenTransform in _changedChildrenTransforms)
		{
			changedChildrenTransform.PrepareChildrenSort();
		}
		List<RectTransform> computeDirtyTransforms = _computeDirtyTransforms;
		List<RectTransform> dataModelDirtyTransforms = _dataModelDirtyTransforms;
		_dataModelDirtyTransforms = computeDirtyTransforms;
		_computeDirtyTransforms = dataModelDirtyTransforms;
		_dataModelDirtyTransforms.Clear();
	}

	private bool ShouldPrune(RectTransform rect)
	{
		if (!rect.IsRemoved && !rect.IsRectDisabled)
		{
			if (rect.RectParent == null)
			{
				return rect != _root;
			}
			return false;
		}
		return true;
	}

	private async Task ComputeCanvasUpdate()
	{
		await default(ToBackground);
		try
		{
			if (_pruneDisabledTransforms)
			{
				_computeDirtyTransforms.RemoveAll(ShouldPrune);
			}
			foreach (RectTransform changedChildrenTransform in _changedChildrenTransforms)
			{
				changedChildrenTransform.ProcessChangedChildren();
			}
			_changedChildrenTransforms.Clear();
			if (IsRemoved)
			{
				return;
			}
			RectTransform result;
			while (_autoInvalidateRects.TryTake(out result))
			{
				result.MarkComputeRectChangedAndPropagate();
			}
			foreach (RectTransform computeDirtyTransform in _computeDirtyTransforms)
			{
				computeDirtyTransform.ProcessChanges();
				bool hasGraphicsChunk = computeDirtyTransform.HasGraphicsChunk;
				if (hasGraphicsChunk && computeDirtyTransform.StructureChanged)
				{
					_graphicChunksDirty = true;
				}
				if (hasGraphicsChunk != computeDirtyTransform.RequiresGraphicChunk)
				{
					_graphicChunksDirty = true;
					if (computeDirtyTransform.RequiresGraphicChunk)
					{
						_graphicChunks.Add(new GraphicsChunk(this, computeDirtyTransform));
						continue;
					}
					GraphicsChunk graphicsChunk = computeDirtyTransform.GraphicsChunk;
					graphicsChunk.Unregister();
					_graphicChunks.Remove(graphicsChunk);
					_removedGraphicChunks.Add(graphicsChunk);
				}
			}
			if (IsRemoved)
			{
				return;
			}
			List<ValueTask> precomputes = Pool.BorrowList<ValueTask>();
			foreach (RectTransform computeDirtyTransform2 in _computeDirtyTransforms)
			{
				if (computeDirtyTransform2.RequiresPreLayoutCompute)
				{
					precomputes.Add(computeDirtyTransform2.RunPreLayoutCompute());
				}
			}
			foreach (ValueTask item in precomputes)
			{
				await item.ConfigureAwait(continueOnCapturedContext: false);
			}
			await default(ToBackground);
			precomputes.Clear();
			if (_root != null)
			{
				Rect parentRect = new Rect(_size * -0.5f, in _size);
				_root.ComputeRect(ref parentRect, null);
			}
			foreach (RectTransform computeDirtyTransform3 in _computeDirtyTransforms)
			{
				if (computeDirtyTransform3.RequiresPreGraphicsCompute)
				{
					precomputes.Add(computeDirtyTransform3.RunPreGraphicsCompute());
				}
			}
			EnsureSortedGraphicsChunks();
			foreach (ValueTask item2 in precomputes)
			{
				await item2.ConfigureAwait(continueOnCapturedContext: false);
			}
			Pool.Return(ref precomputes);
			await default(ToBackground);
			if (IsRemoved)
			{
				return;
			}
			foreach (GraphicsChunk graphicChunk in _graphicChunks)
			{
				if (graphicChunk.IsEnabled)
				{
					await graphicChunk.WaitForLastUpload().ConfigureAwait(continueOnCapturedContext: false);
				}
			}
			if (_debugChunk != null)
			{
				await _debugChunk.WaitForLastUpload().ConfigureAwait(continueOnCapturedContext: false);
			}
			if (IsRemoved)
			{
				return;
			}
			List<Task> tasks = Pool.BorrowList<Task>();
			foreach (GraphicsChunk graphicChunk2 in _graphicChunks)
			{
				if (graphicChunk2.IsEnabled)
				{
					tasks.Add(graphicChunk2.ComputeGraphics());
				}
			}
			if (_debugChunk != null)
			{
				tasks.Add(_debugChunk.ComputeGraphics());
			}
			foreach (Task item3 in tasks)
			{
				await item3.ConfigureAwait(continueOnCapturedContext: false);
			}
			Pool.Return(ref tasks);
			if (!IsRemoved && _root != null)
			{
				_root.UpdateBoundsAndClear(float2.Zero);
			}
		}
		finally
		{
			_cachedWorld.RunSynchronously(_finishCanvasUpdate, immediatellyIfPossible: false, this, evenIfDisposed: true);
		}
	}

	private void FinishRemainingRemovals()
	{
		if (_removals == null)
		{
			return;
		}
		foreach (Action removal in _removals)
		{
			removal();
		}
		_removals.Clear();
		_removals = null;
	}

	private void FinishCanvasUpdate()
	{
		_cachedWorld = null;
		if (IsRemoved || base.World.IsDisposed)
		{
			FinishRemainingRemovals();
			CleanupChunks();
			CleanupData();
			_postCycleActions.Clear();
			_postCycleActions = null;
			return;
		}
		SubmitGraphics();
		foreach (Action postCycleAction in _postCycleActions)
		{
			postCycleAction();
		}
		_postCycleActions.Clear();
		bool flag = false;
		lock (this)
		{
			if (_runAgain)
			{
				flag = true;
				_runAgain = false;
			}
			else
			{
				_updateRunning = false;
			}
		}
		BoundingBox boundingBox;
		if (_rootChunk != null)
		{
			Rect rect = _rootChunk.Root.LocalComputeRect.Translate(_rootChunk.ComputeOffset);
			boundingBox = BoundingBox.CenterSize(rect.Center.xy_, rect.size.xy_);
		}
		else
		{
			boundingBox = BoundingBox.Empty();
		}
		float3 v;
		float3 v2;
		if (boundingBox.IsValid)
		{
			v = boundingBox.Size;
			v2 = boundingBox.Center;
		}
		else
		{
			v = _size;
			v2 = float3.Zero;
		}
		v /= ComputeUnitScale;
		v2 /= ComputeUnitScale;
		if (_colliderSize.IsLinkValid)
		{
			_colliderSize.Target.Value = v;
		}
		if (_colliderOffset.IsLinkValid)
		{
			_colliderOffset.Target.Value = v2;
		}
		if (flag)
		{
			StartCanvasUpdate();
		}
	}

	private void EnsureSortedGraphicsChunks()
	{
		if (!_graphicChunksDirty)
		{
			return;
		}
		_rootChunk = null;
		foreach (GraphicsChunk graphicChunk in _graphicChunks)
		{
			graphicChunk.ClearHiearchy();
		}
		foreach (GraphicsChunk graphicChunk2 in _graphicChunks)
		{
			graphicChunk2.RegisterInHierarchy();
			if (graphicChunk2.IsRoot)
			{
				if (_rootChunk != null)
				{
					throw new Exception($"Multiple root chunks!\n\nFirst found root {_rootChunk.GetHashCode()}: {_rootChunk.Root.ParentHierarchyToString()}\n\nNext found root {graphicChunk2.GetHashCode()}: {graphicChunk2.Root.ParentHierarchyToString()}\n\nOn: {this.ParentHierarchyToString()}");
				}
				_rootChunk = graphicChunk2;
			}
		}
		if (_rootChunk != null)
		{
			_rootChunk.MarkEnabled();
			foreach (GraphicsChunk graphicChunk3 in _graphicChunks)
			{
				if (graphicChunk3.IsEnabled)
				{
					graphicChunk3.SortChildren();
				}
				else
				{
					_chunksToDisable.Add(graphicChunk3);
				}
			}
		}
		_graphicChunksDirty = false;
	}

	private void CleanupData()
	{
		RectTransform result;
		while (_autoInvalidateRects.TryTake(out result))
		{
		}
	}

	private void CleanupChunks()
	{
		foreach (GraphicsChunk removedGraphicChunk in _removedGraphicChunks)
		{
			removedGraphicChunk.RemoveComponents();
		}
		foreach (GraphicsChunk graphicChunk in _graphicChunks)
		{
			graphicChunk.RemoveComponents();
		}
		_removedGraphicChunks.Clear();
		_graphicChunks.Clear();
		_rootChunk = null;
		_removedGraphicChunks = null;
		_graphicChunks = null;
	}

	private void SubmitGraphics()
	{
		foreach (GraphicsChunk item in _chunksToDisable)
		{
			item.Disable();
		}
		_chunksToDisable.Clear();
		foreach (GraphicsChunk removedGraphicChunk in _removedGraphicChunks)
		{
			removedGraphicChunk.RemoveComponents();
		}
		_removedGraphicChunks.Clear();
		if (_rootChunk != null)
		{
			_rootSlot.LocalScale = float3.One / ComputeUnitScale;
			int renderOffset = StartingOffset.Value;
			int value = StartingMaskDepth.Value;
			int num = renderOffset;
			Rect maskRect = _rootChunk.Root.LocalComputeRect.Translate(_rootChunk.ComputeOffset);
			_rootChunk.SubmitChanges(ref renderOffset, maskRect, 0, value);
			foreach (GraphicsChunk graphicChunk in _graphicChunks)
			{
				if (graphicChunk.IsEnabled)
				{
					graphicChunk.UploadMesh(HighPriorityIntegration);
				}
			}
			if (_debugChunk != null)
			{
				_debugChunk.SubmitChanges(ref renderOffset, maskRect);
				_debugChunk.UploadMesh(HighPriorityIntegration);
			}
			LastRendererCount = renderOffset - num;
		}
		else
		{
			LastRendererCount = 0;
		}
		LastChunkCount = _graphicChunks?.Count ?? 0;
		this.OnLocalSubmissionFinished?.Invoke(this);
	}

	internal void RegisterDirtyTransform(RectTransform rectTransform)
	{
		_dataModelDirtyTransforms.Add(rectTransform);
		MarkChangeDirty();
	}

	internal void RegisterRemovedTransform(RectTransform rectTransform)
	{
		_removedTransforms.Add(rectTransform);
		MarkChangeDirty();
	}

	internal void RegisterChangedChildrenTransform(RectTransform rectTransform)
	{
		_changedChildrenTransforms.Add(rectTransform);
	}

	internal void RegisterRemoval(Action action)
	{
		if (_removals == null)
		{
			action();
			return;
		}
		_removals.Add(action);
		MarkChangeDirty();
	}

	internal void MarkGraphicChunksDirty()
	{
		_graphicChunksDirty = true;
	}

	public void RegisterPostCycleAction(Action action)
	{
		lock (_postCycleActions)
		{
			_postCycleActions.Add(action);
		}
	}

	public void MarkForAutoInvalidation(RectTransform rect)
	{
		_autoInvalidateRects.Add(rect);
	}

	public Task<BoundingBox> ComputeExactBounds(Slot space)
	{
		return Task.FromResult(LocalBoundingBox.Transform(base.Slot.GetLocalToSpaceMatrix(space)));
	}

	public Task ForeachExactBoundedPoint(Slot space, Action<float3> point)
	{
		BoundingBox localBoundingBox = LocalBoundingBox;
		for (int i = 0; i < 8; i++)
		{
			point(base.Slot.LocalPointToSpace(localBoundingBox.GetVertexPoint(i), space));
		}
		return Task.CompletedTask;
	}

	public bool CanTouchInteract(TouchSource source)
	{
		if ((bool)EditModeOnly && !base.LocalUser.EditMode)
		{
			return false;
		}
		return true;
	}

	private bool CanInteract(in TouchEventInfo eventInfo)
	{
		if (!CanTouchInteract(eventInfo.source))
		{
			return false;
		}
		return eventInfo.type switch
		{
			TouchType.Physical => AcceptPhysicalTouch, 
			TouchType.Remote => AcceptRemoteTouch, 
			_ => throw new Exception("Unexpected touch type: " + eventInfo.type), 
		};
	}

	public void OnTouch(in TouchEventInfo eventInfo)
	{
		ProcessTouchEvent(in eventInfo);
	}

	public bool ProcessTouchEvent(in TouchEventInfo eventInfo, List<Predicate<IUIInteractable>> filters = null)
	{
		if (eventInfo.source == null)
		{
			return false;
		}
		TouchSource source = eventInfo.source;
		if (source == null || !source.SafeTouchSource)
		{
			return false;
		}
		bool flag = false;
		if (BlockAllInteractions.Value)
		{
			flag = true;
		}
		if (IgnoreTouchesFromBehind.Value && MathX.Dot(in eventInfo.direction, base.Slot.Forward) < 0f)
		{
			flag = true;
		}
		InteractionData value;
		if (!CanInteract(in eventInfo) || eventInfo.hover == EventState.End || flag)
		{
			if (_currentInteractions.TryGetValue(eventInfo.source, out value))
			{
				value.FinishCurrentInteraction();
				value.EndAllHovers();
				_currentInteractions.Remove(eventInfo.source);
				Pool<InteractionData>.Return(ref value);
			}
			return false;
		}
		if (!_currentInteractions.TryGetValue(eventInfo.source, out value))
		{
			value = Pool<InteractionData>.Borrow();
			value.source = eventInfo.source;
			_currentInteractions.Add(eventInfo.source, value);
		}
		float2 v = base.Slot.GlobalPointToLocal(in eventInfo.point).xy;
		float2 point = (value.position = v * ComputeUnitScale);
		value.type = eventInfo.type;
		value.tip = eventInfo.tip;
		value.direction = eventInfo.direction;
		if (value.lastTime < 0.0)
		{
			value.lastPosition = value.position;
		}
		List<RectTransform> list = Pool.BorrowList<RectTransform>();
		List<IUIHoverable> list2 = Pool.BorrowList<IUIHoverable>();
		GetIntersectingTransformsIntern(in point, list);
		foreach (RectTransform item in list)
		{
			item.Slot.GetComponents(list2, null, excludeDisabled: true);
		}
		value.UpdateHovers(list2);
		Pool.Return(ref list2);
		if (value.CurrentInteractable != null && eventInfo.touch != EventState.None && eventInfo.touch != EventState.Begin)
		{
			IUIInteractable touchLock = value.touchLock;
			if (touchLock != null && touchLock.TouchExitLock)
			{
				goto IL_01f9;
			}
		}
		GetInteractables(list, value);
		goto IL_01f9;
		IL_01f9:
		Pool.Return(ref list);
		bool flag2 = false;
		value.filters.Clear();
		if (filters != null)
		{
			value.filters.AddRange(filters);
		}
		if (value.currentInteractables.Count > 0)
		{
			for (int i = 0; i < value.currentInteractables.Count; i++)
			{
				(value.currentInteractables[i] as IUIPreprocessInteractable)?.PreprocessInteraction(value, in eventInfo);
			}
			if (eventInfo.touch == EventState.Begin)
			{
				value.touchLock = null;
				value.initialTouchTime = base.Time.WorldTime;
				value.initialTouchPosition = point;
			}
			int num = 0;
			if (value.touchLock != null)
			{
				bool flag3 = false;
				for (int j = 0; j < value.currentInteractables.Count; j++)
				{
					if (value.currentInteractables[j] == value.touchLock)
					{
						flag3 = true;
						num = j;
						break;
					}
				}
				if (!flag3)
				{
					value.touchLock = null;
				}
			}
			double initialHoverTime = value.initialHoverTime;
			float2 lastPosition = value.lastPosition;
			double lastTime = value.lastTime;
			bool flag4 = false;
			for (int k = num; k < value.currentInteractables.Count; k++)
			{
				IUIInteractable iUIInteractable = value.currentInteractables[k];
				foreach (Predicate<IUIInteractable> filter in value.filters)
				{
					filter(iUIInteractable);
				}
				if (iUIInteractable == value.CurrentInteractable)
				{
					value.initialHoverTime = initialHoverTime;
					value.hover = EventState.Stay;
					value.lastPosition = lastPosition;
					value.lastTime = lastTime;
				}
				else
				{
					value.initialHoverTime = base.Time.WorldTime;
					value.hover = EventState.Begin;
					if (eventInfo.touch == EventState.Begin || eventInfo.touch == EventState.Stay)
					{
						value.lastPosition = value.initialTouchPosition;
						value.lastTime = value.initialTouchTime;
					}
				}
				if ((eventInfo.touch == EventState.Begin || value.touchLock == iUIInteractable || !iUIInteractable.TouchEnterLock || flag4) && eventInfo.touch != EventState.None)
				{
					value.touchLock = iUIInteractable;
					value.touch = eventInfo.touch;
				}
				else
				{
					value.touch = EventState.None;
					value.touchLock = null;
				}
				if (iUIInteractable.ProcessEvent(value))
				{
					flag2 = true;
					value.SetCurrentInteractable(iUIInteractable);
					break;
				}
				if (value.touchLock == iUIInteractable && eventInfo.touch == EventState.Stay)
				{
					flag4 = true;
				}
				value.touchLock = null;
			}
		}
		if (!flag2)
		{
			value.FinishCurrentInteraction();
		}
		value.lastPosition = value.position;
		value.lastTime = base.Time.WorldTime;
		value.filters.Clear();
		return flag2;
	}

	public void GetIntersectingTransforms(in float2 point, List<RectTransform> rects)
	{
		GetIntersectingTransformsIntern(point * ComputeUnitScale, rects);
	}

	private void GetIntersectingTransformsIntern(in float2 point, List<RectTransform> rects)
	{
		_root?.GetIntersectingTransforms(point, rects);
	}

	private void GetInteractables(List<RectTransform> rects, InteractionData data)
	{
		data.currentInteractables.Clear();
		foreach (RectTransform rect in rects)
		{
			IUIInteractable componentInParents = rect.Slot.GetComponentInParents<IUIInteractable>();
			if (componentInParents != null)
			{
				data.currentInteractables.AddUnique(componentInParents);
			}
		}
	}

	public IGrabbable TryGrab(Component grabber, in float3 point)
	{
		if (_currentInteractions.TryGetValue(grabber, out InteractionData value) && value.CurrentInteractable != null)
		{
			if (value.CurrentInteractable is NestedCanvas nestedCanvas && nestedCanvas.TargetCanvas.Target != null)
			{
				IGrabbable grabbable = nestedCanvas.TargetCanvas.Target.TryGrab(grabber, in point);
				if (grabbable != null)
				{
					return grabbable;
				}
			}
			List<IUIGrabbable> list = Pool.BorrowList<IUIGrabbable>();
			try
			{
				value.CurrentInteractable.Slot.GetComponentsInParents(list);
				foreach (IUIGrabbable item in list)
				{
					IGrabbable grabbable2 = item.TryGrab(grabber, value, in point);
					if (grabbable2 != null)
					{
						return grabbable2;
					}
				}
			}
			finally
			{
				Pool.Return(ref list);
			}
		}
		return null;
	}

	public void Release(IEnumerable<IGrabbable> items, Component grabber, in float3 point)
	{
		if (!_currentInteractions.TryGetValue(grabber, out InteractionData value) || value.CurrentInteractable == null)
		{
			return;
		}
		List<IUIGrabReceiver> list = Pool.BorrowList<IUIGrabReceiver>();
		try
		{
			value.CurrentInteractable.Slot.GetComponentsInParents(list);
			using List<IUIGrabReceiver>.Enumerator enumerator = list.GetEnumerator();
			while (enumerator.MoveNext() && !enumerator.Current.TryReceive(items, grabber, value, in point))
			{
			}
		}
		finally
		{
			Pool.Return(ref list);
		}
	}

	public DistanceResult<T> FindNearest<T>(in float2 point, Predicate<T> filter = null) where T : class
	{
		return _root?.FindNearest(point, filter) ?? new DistanceResult<T>(null, float.MaxValue);
	}

	protected override void OnLoading(DataTreeNode node, LoadControl control)
	{
		base.OnLoading(node, control);
		int typeVersion = control.GetTypeVersion(GetType());
		if (typeVersion == 0)
		{
			control.OnLoaded(this, EnsureRootTransform);
		}
		if (typeVersion < 2)
		{
			control.OnLoaded(this, delegate
			{
				DefaultCulling.Value = Culling.Off;
			});
		}
	}

	public float? GetSmoothSpeed(InteractionLaser laser, in float3 newPoint, in float3 oldPoint)
	{
		return null;
	}

	public float3 FilterPoint(InteractionLaser laser, in float3 point)
	{
		return point;
	}

	public bool IsInteractionHit(in float3 point, in float3 direction)
	{
		if (LaserPassThrough.Value)
		{
			return IsLocalPointInteractionHit(base.Slot.GlobalPointToLocal(in point).xy);
		}
		return true;
	}

	public bool IsLocalPointInteractionHit(in float2 localPoint)
	{
		if (!LaserPassThrough.Value)
		{
			return true;
		}
		List<RectTransform> list = Pool.BorrowList<RectTransform>();
		GetIntersectingTransforms(in localPoint, list);
		bool result = false;
		foreach (RectTransform item in list)
		{
			if (item.Graphic != null)
			{
				result = true;
				break;
			}
			if (item.Controller is NestedCanvas nestedCanvas && nestedCanvas.TargetCanvas.Target != null)
			{
				float2 localPoint2 = item.RootPointToLocal(localPoint);
				if (nestedCanvas.TargetCanvas.Target.IsLocalPointInteractionHit(in localPoint2))
				{
					result = true;
					break;
				}
			}
		}
		Pool.Return(ref list);
		return result;
	}

	public bool IsInteracting(InteractionLaser laser)
	{
		if (_currentInteractions.TryGetValue(laser.TouchSource, out InteractionData value) && value.CurrentInteractable != null)
		{
			return true;
		}
		return false;
	}

	public InteractionDescription GetInteractionDescription(InteractionLaser laser)
	{
		InteractionDescription result = new InteractionDescription
		{
			name = "Click"
		};
		if (_currentInteractions.TryGetValue(laser.TouchSource, out InteractionData value) && value.CurrentInteractable != null)
		{
			List<IUIInteractionTarget> list = Pool.BorrowList<IUIInteractionTarget>();
			value.CurrentInteractable.Slot.GetComponents(list);
			IUIInteractionTarget iUIInteractionTarget = null;
			foreach (IUIInteractionTarget item in list)
			{
				if (iUIInteractionTarget == null || item.InteractionTargetPriority > iUIInteractionTarget.InteractionTargetPriority)
				{
					iUIInteractionTarget = item;
				}
			}
			Pool.Return(ref list);
			if (iUIInteractionTarget != null)
			{
				result.name = iUIInteractionTarget.InteractionName;
				result.cursor = iUIInteractionTarget.GetCursor(laser);
			}
		}
		return result;
	}

	public bool TriggerContextMenu(Component source)
	{
		bool result = false;
		if (_currentInteractions.TryGetValue(source, out InteractionData value) && value.CurrentInteractable != null)
		{
			List<IUIContextMenuActionReceiver> list = Pool.BorrowList<IUIContextMenuActionReceiver>();
			value.CurrentInteractable.Slot.GetComponentsInParents(list);
			foreach (IUIContextMenuActionReceiver item in list)
			{
				if (item.TriggerContextMenu(source, value))
				{
					result = true;
					break;
				}
			}
			Pool.Return(ref list);
		}
		return result;
	}

	public bool TriggerSecondary(Component source)
	{
		bool result = false;
		if (_currentInteractions.TryGetValue(source, out InteractionData value) && value.CurrentInteractable != null)
		{
			List<IUISecondaryActionReceiver> list = Pool.BorrowList<IUISecondaryActionReceiver>();
			value.CurrentInteractable.Slot.GetComponentsInParents(list);
			foreach (IUISecondaryActionReceiver item in list)
			{
				if (item.TriggerSecondary(source, value))
				{
					result = true;
					break;
				}
			}
			Pool.Return(ref list);
		}
		return result;
	}

	public bool ProcessAxis(Component source, float2 axis)
	{
		bool result = false;
		if (_currentInteractions.TryGetValue(source, out InteractionData value) && value.CurrentInteractable != null)
		{
			List<IUIAxisActionReceiver> list = Pool.BorrowList<IUIAxisActionReceiver>();
			value.CurrentInteractable.Slot.GetComponentsInParents(list);
			foreach (IUIAxisActionReceiver item in list)
			{
				if (item.ProcessAxis(source, axis, value))
				{
					result = true;
					break;
				}
			}
			Pool.Return(ref list);
		}
		return result;
	}

	void ITouchable.OnTouch(in TouchEventInfo eventInfo)
	{
		OnTouch(in eventInfo);
	}

	IGrabbable ITouchGrabbable.TryGrab(Component grabber, in float3 point)
	{
		return TryGrab(grabber, in point);
	}

	void ITouchGrabbable.Release(IEnumerable<IGrabbable> items, Component grabber, in float3 point)
	{
		Release(items, grabber, in point);
	}

	float? ILaserInteractionModifier.GetSmoothSpeed(InteractionLaser laser, in float3 newPoint, in float3 oldPoint)
	{
		return GetSmoothSpeed(laser, in newPoint, in oldPoint);
	}

	float3 ILaserInteractionModifier.FilterPoint(InteractionLaser laser, in float3 point)
	{
		return FilterPoint(laser, in point);
	}

	bool ILaserInteractionModifier.IsInteractionHit(in float3 point, in float3 direction)
	{
		return IsInteractionHit(in point, in direction);
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		Size = new Sync<float2>();
		EditModeOnly = new Sync<bool>();
		AcceptRemoteTouch = new Sync<bool>();
		AcceptPhysicalTouch = new Sync<bool>();
		AcceptExistingTouch = new Sync<bool>();
		HighPriorityIntegration = new Sync<bool>();
		IgnoreTouchesFromBehind = new Sync<bool>();
		BlockAllInteractions = new Sync<bool>();
		LaserPassThrough = new Sync<bool>();
		PixelScale = new Sync<float>();
		UnitScale = new Sync<float>();
		_rootRect = new SyncRef<RectTransform>();
		Collider = new SyncRef<BoxCollider>();
		DefaultCulling = new Sync<Culling>();
		_colliderSize = new FieldDrive<float3>();
		_colliderOffset = new FieldDrive<float3>();
		StartingOffset = new Sync<int>();
		StartingMaskDepth = new Sync<int>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => Size, 
			4 => EditModeOnly, 
			5 => AcceptRemoteTouch, 
			6 => AcceptPhysicalTouch, 
			7 => AcceptExistingTouch, 
			8 => HighPriorityIntegration, 
			9 => IgnoreTouchesFromBehind, 
			10 => BlockAllInteractions, 
			11 => LaserPassThrough, 
			12 => PixelScale, 
			13 => UnitScale, 
			14 => _rootRect, 
			15 => Collider, 
			16 => DefaultCulling, 
			17 => _colliderSize, 
			18 => _colliderOffset, 
			19 => StartingOffset, 
			20 => StartingMaskDepth, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static Canvas __New()
	{
		return new Canvas();
	}
}
