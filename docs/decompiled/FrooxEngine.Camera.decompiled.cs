using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Elements.Assets;
using Elements.Core;
using Renderite.Shared;

namespace FrooxEngine;

[Category(new string[] { "Rendering" })]
public class Camera : ChangeHandlingRenderableComponent, IUVToRayConverter, IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable
{
	public readonly Sync<bool> DoubleBuffered;

	public readonly Sync<bool> ForwardOnly;

	public readonly Sync<CameraProjection> Projection;

	[Range(0.1f, 10f, "0.00")]
	public readonly Sync<float> OrthographicSize;

	[Range(10f, 140f, "0.00")]
	public readonly Sync<float> FieldOfView;

	public readonly Sync<float> NearClipping;

	public readonly Sync<float> FarClipping;

	public readonly Sync<bool> UseTransformScale;

	private bool _renderPrivateUI;

	public readonly Sync<CameraClearMode> Clear;

	public readonly Sync<colorX> ClearColor;

	public readonly Sync<Rect> Viewport;

	public readonly Sync<float> Depth;

	public readonly AssetRef<RenderTexture> RenderTexture;

	public readonly Sync<bool> Postprocessing;

	public readonly Sync<bool> ScreenSpaceReflections;

	public readonly Sync<bool> MotionBlur;

	public readonly Sync<bool> RenderShadows;

	public readonly AutoSyncRefList<Slot> SelectiveRender;

	public readonly AutoSyncRefList<Slot> ExcludeRender;

	private bool _renderSlotsChanged;

	private HashSet<Slot> _markedRenderSlots = new HashSet<Slot>();

	public override int Version => 1;

	public override bool ActiveWhenDisabled => true;

	public override bool ActiveWhenDeactivated => true;

	public CamerasManager Manager => base.Render.Cameras;

	public bool RenderPrivateUI
	{
		get
		{
			return _renderPrivateUI;
		}
		set
		{
			if (value != _renderPrivateUI)
			{
				_renderPrivateUI = value;
				MarkChangeDirty();
			}
		}
	}

	public float AspectRatio
	{
		get
		{
			RenderTexture asset = RenderTexture.Asset;
			if (asset == null || (asset.Size == 0).Any())
			{
				return 1f;
			}
			return (float)asset.Size.x / (float)asset.Size.y;
		}
	}

	public float HorizontalFieldOfView => MathX.HorizontalFOVFromVerical(FieldOfView, AspectRatio);

	protected override void OnAwake()
	{
		base.OnAwake();
		Projection.Value = CameraProjection.Perspective;
		FieldOfView.Value = 60f;
		OrthographicSize.Value = 8f;
		NearClipping.Value = 0.1f;
		FarClipping.Value = 4096f;
		Viewport.Value = new Rect(float2.Zero, float2.One);
		Clear.Value = CameraClearMode.Skybox;
		ClearColor.Value = colorX.Clear;
		Postprocessing.Value = true;
		MotionBlur.Value = true;
		RenderShadows.Value = true;
		SelectiveRender.ElementsAdded += SelectiveRender_ElementsAdded;
		ExcludeRender.ElementsAdded += ExcludeRender_ElementsAdded;
	}

	protected override void OnChanges()
	{
		base.OnChanges();
		if (!_renderSlotsChanged || !base.IsRenderingSupported)
		{
			return;
		}
		_renderSlotsChanged = false;
		HashSet<Slot> hashSet = Pool.BorrowHashSet<Slot>();
		foreach (Slot markedRenderSlot in _markedRenderSlots)
		{
			hashSet.Add(markedRenderSlot);
		}
		foreach (Slot item in ExcludeRender)
		{
			if (item != null)
			{
				hashSet.Remove(item);
				if (_markedRenderSlots.Add(item))
				{
					item.IncrementRenderable();
				}
			}
		}
		foreach (Slot item2 in SelectiveRender)
		{
			if (item2 != null)
			{
				hashSet.Remove(item2);
				if (_markedRenderSlots.Add(item2))
				{
					item2.IncrementRenderable();
				}
			}
		}
		foreach (Slot item3 in hashSet)
		{
			_markedRenderSlots.Remove(item3);
			item3.DecrementRenderable();
		}
		Pool.Return(ref hashSet);
	}

	private void SelectiveRender_ElementsAdded(SyncElementList<AutoSyncRef<Slot>> list, int startIndex, int count)
	{
		for (int i = 0; i < count; i++)
		{
			list[i + startIndex].OnTargetChange += RenderSlotChanged;
		}
	}

	private void ExcludeRender_ElementsAdded(SyncElementList<AutoSyncRef<Slot>> list, int startIndex, int count)
	{
		for (int i = 0; i < count; i++)
		{
			list[i + startIndex].OnTargetChange += RenderSlotChanged;
		}
	}

	private void RenderSlotChanged(SyncRef<Slot> reference)
	{
		if (!_renderSlotsChanged)
		{
			_renderSlotsChanged = true;
			MarkChangeDirty();
		}
	}

	public override void InitRenderableState()
	{
	}

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

	protected override void OnDispose()
	{
		base.OnDispose();
		foreach (Slot markedRenderSlot in _markedRenderSlots)
		{
			markedRenderSlot.DecrementRenderable();
		}
		_markedRenderSlots.Clear();
	}

	public RenderTask GetRenderSettings(int2 resolution)
	{
		RenderTask renderTask = new RenderTask(base.Slot.GlobalPosition, base.Slot.GlobalRotation);
		renderTask.parameters.projection = Projection.Value;
		renderTask.parameters.fov = FieldOfView.Value;
		renderTask.parameters.orthographicSize = OrthographicSize.Value;
		renderTask.parameters.resolution = resolution;
		renderTask.parameters.nearClip = NearClipping.Value;
		renderTask.parameters.farClip = FarClipping.Value;
		renderTask.parameters.clearMode = Clear.Value;
		renderTask.ClearColor = ClearColor.Value;
		renderTask.parameters.postProcessing = Postprocessing.Value;
		renderTask.parameters.screenSpaceReflections = ScreenSpaceReflections.Value;
		renderTask.parameters.renderPrivateUI = RenderPrivateUI;
		if (UseTransformScale.Value)
		{
			renderTask.parameters.orthographicSize *= MathX.AvgComponent(base.Slot.GlobalScale);
		}
		if (SelectiveRender.Any((Slot s) => s != null))
		{
			renderTask.renderObjects = SelectiveRender.Where((Slot s) => s != null).ToList();
		}
		if (ExcludeRender.Any((Slot s) => s != null))
		{
			renderTask.excludeObjects = ExcludeRender.Where((Slot s) => s != null).ToList();
		}
		return renderTask;
	}

	public async Task<StaticTexture2D> RenderToTexture(int2 resolution, Slot root = null, string format = "webp", int quality = 200)
	{
		root = root ?? base.World.AssetsSlot.AddSlot("Camera Render");
		return await base.World.Render.RenderToStaticTexture(GetRenderSettings(resolution), root, format, quality);
	}

	public async Task<Uri> RenderToAsset(int2 resolution, string format = "webp", int quality = 200)
	{
		return await base.World.Render.RenderToAsset(GetRenderSettings(resolution), format, quality);
	}

	public async Task<Bitmap2D> RenderToBitmap(int2 resolution)
	{
		return await base.World.Render.RenderToBitmap(GetRenderSettings(resolution));
	}

	public void UVToRay(float2 uv, out float3 rayOrigin, out float3 rayDirection)
	{
		switch (Projection.Value)
		{
		case CameraProjection.Orthographic:
		{
			float num = OrthographicSize.Value;
			if (UseTransformScale.Value)
			{
				num *= MathX.AvgComponent(base.Slot.GlobalScale);
			}
			uv -= 0.5f;
			uv *= new float2(AspectRatio, 1f) * num * 2;
			rayOrigin = base.Slot.GlobalPosition + base.Slot.GlobalRotation * uv.xy_;
			rayDirection = base.Slot.Forward;
			break;
		}
		case CameraProjection.Perspective:
			rayOrigin = base.Slot.GlobalPosition;
			rayDirection = base.Slot.LocalDirectionToGlobal(MathX.UVToPerspectiveCameraDirection(uv, AspectRatio, FieldOfView));
			break;
		default:
			rayOrigin = base.Slot.GlobalPosition;
			rayDirection = float3.Forward;
			break;
		}
	}

	public float2 PointToUV(float3 point)
	{
		float aspectRatio = AspectRatio;
		point = base.Slot.GlobalPointToLocal(in point);
		switch (Projection.Value)
		{
		case CameraProjection.Orthographic:
		{
			float num = OrthographicSize.Value;
			if (!UseTransformScale.Value)
			{
				num *= MathX.AvgComponent(base.Slot.GlobalScale);
			}
			point /= (float3)(num * new float2(aspectRatio, 1f));
			point /= 2;
			point += 0.5f;
			return point.xy;
		}
		case CameraProjection.Perspective:
		{
			float x = MathX.AngleRad(point.x_z, float3.Forward);
			float y = MathX.AngleRad(point._yz, float3.Forward);
			return MathX.Tan(new float2(x, y)) * MathX.Sign(point.xy) / (MathX.Tan((float)FieldOfView * 0.5f * (MathF.PI / 180f)) * new float2(aspectRatio, 1f)) / 2f + 0.5f;
		}
		default:
			return default(float2);
		}
	}

	protected override void OnLoading(DataTreeNode node, LoadControl control)
	{
		base.OnLoading(node, control);
		if (control.GetTypeVersion<Camera>() != 0)
		{
			return;
		}
		RunSynchronously(delegate
		{
			if (FarClipping.Value == 1000f)
			{
				FarClipping.Value = 4096f;
			}
		});
	}

	protected override void RegisterAddedOrRemoved()
	{
		Manager.RenderableAddedOrRemoved(this);
	}

	protected override void RegisterChanged()
	{
		Manager.RegisterChangedState(this);
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		DoubleBuffered = new Sync<bool>();
		ForwardOnly = new Sync<bool>();
		Projection = new Sync<CameraProjection>();
		OrthographicSize = new Sync<float>();
		FieldOfView = new Sync<float>();
		NearClipping = new Sync<float>();
		FarClipping = new Sync<float>();
		UseTransformScale = new Sync<bool>();
		Clear = new Sync<CameraClearMode>();
		ClearColor = new Sync<colorX>();
		Viewport = new Sync<Rect>();
		Depth = new Sync<float>();
		RenderTexture = new AssetRef<RenderTexture>();
		Postprocessing = new Sync<bool>();
		ScreenSpaceReflections = new Sync<bool>();
		MotionBlur = new Sync<bool>();
		RenderShadows = new Sync<bool>();
		SelectiveRender = new AutoSyncRefList<Slot>();
		ExcludeRender = new AutoSyncRefList<Slot>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => DoubleBuffered, 
			4 => ForwardOnly, 
			5 => Projection, 
			6 => OrthographicSize, 
			7 => FieldOfView, 
			8 => NearClipping, 
			9 => FarClipping, 
			10 => UseTransformScale, 
			11 => Clear, 
			12 => ClearColor, 
			13 => Viewport, 
			14 => Depth, 
			15 => RenderTexture, 
			16 => Postprocessing, 
			17 => ScreenSpaceReflections, 
			18 => MotionBlur, 
			19 => RenderShadows, 
			20 => SelectiveRender, 
			21 => ExcludeRender, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static Camera __New()
	{
		return new Camera();
	}
}
