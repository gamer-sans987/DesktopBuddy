using System;
using Elements.Core;
using Renderite.Shared;

namespace FrooxEngine;

/// <summary>
/// Renders a light source in the world
/// </summary>
[Category(new string[] { "Rendering" })]
public class Light : ChangeHandlingRenderableComponent, IRenderable, IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable
{
	/// <summary>
	/// The type of the light source to render
	/// </summary>
	public readonly Sync<LightType> LightType;

	/// <summary>
	/// Intensity of the light source
	/// </summary>
	public readonly Sync<float> Intensity;

	/// <summary>
	/// Color of the light source
	/// </summary>
	public readonly Sync<colorX> Color;

	/// <summary>
	/// Type of the shadow the light source casts
	/// </summary>
	public readonly Sync<ShadowType> ShadowType;

	/// <summary>
	/// Intensity of the cast shadow from this light source
	/// </summary>
	[Range(0f, 1f, "0.00")]
	public readonly Sync<float> ShadowStrength;

	/// <summary>
	/// The near clip plane for the shadow's camera itself.
	/// </summary>
	[Range(0f, 10f, "0.00")]
	public readonly Sync<float> ShadowNearPlane;

	/// <summary>
	/// Override for the shadow map's resolution.  0 is automatic.
	/// </summary>
	public readonly Sync<int> ShadowMapResolution;

	/// <summary>
	/// Bias for shadows.  Offsets the shadow map's depth buffer.
	/// </summary>
	[Range(0f, 2f, "0.00")]
	public readonly Sync<float> ShadowBias;

	/// <summary>
	/// Bias for shadows.  Offsets the shadow map's depth buffer using the receiving surface's angle.
	/// </summary>
	[Range(0f, 3f, "0.00")]
	public readonly Sync<float> ShadowNormalBias;

	/// <summary>
	/// Used by point and spot light sources, determines the maximum distance the light reaches
	/// </summary>
	public readonly Sync<float> Range;

	/// <summary>
	/// Anglular size in degrees of a spot light type
	/// </summary>
	public readonly Sync<float> SpotAngle;

	/// <summary>
	/// Texture whose alpha channel is used to modulate the intensity of the light
	/// </summary>
	public readonly AssetRef<ITexture> Cookie;

	public override int Version => 1;

	public LightsManager Manager => base.Render.Lights;

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

	public override void InitRenderableState()
	{
	}

	protected override void OnAwake()
	{
		base.OnAwake();
		base.Slot.WorldTransformChanged += Slot_WorldTransformChanged;
		LightType.Value = Renderite.Shared.LightType.Point;
		Intensity.Value = 1f;
		Color.Value = colorX.White;
		Range.Value = 10f;
		SpotAngle.Value = 60f;
		ShadowStrength.Value = 1f;
		ShadowNearPlane.Value = 0.2f;
		ShadowMapResolution.Value = 0;
		ShadowBias.Value = 0.125f;
		ShadowNormalBias.Value = 0.6f;
		if (!base.Slot.IsLocalElement)
		{
			base.Slot.ActiveUserRootChanged += Slot_ActiveUserRootChanged;
		}
	}

	protected override void OnDispose()
	{
		base.Slot.WorldTransformChanged -= Slot_WorldTransformChanged;
		if (!base.Slot.IsLocalElement)
		{
			base.Slot.ActiveUserRootChanged -= Slot_ActiveUserRootChanged;
		}
		base.OnDispose();
	}

	private void Slot_ActiveUserRootChanged(Slot slot)
	{
		MarkChangeDirty();
	}

	private void Slot_WorldTransformChanged(Slot slot)
	{
		MarkChangeDirty();
	}

	protected override void OnLoading(DataTreeNode node, LoadControl control)
	{
		base.OnLoading(node, control);
		if (!control.GetFeatureFlag("ColorManagement").HasValue)
		{
			if ((LightType)LightType != Renderite.Shared.LightType.Directional)
			{
				control.Convert<LightIntensityFieldAdapter, float>(Intensity);
			}
			else
			{
				control.Convert<DirectionalLightIntensityFieldAdapter, float>(Intensity);
			}
		}
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
		LightType = new Sync<LightType>();
		Intensity = new Sync<float>();
		Color = new Sync<colorX>();
		ShadowType = new Sync<ShadowType>();
		ShadowStrength = new Sync<float>();
		ShadowNearPlane = new Sync<float>();
		ShadowMapResolution = new Sync<int>();
		ShadowBias = new Sync<float>();
		ShadowNormalBias = new Sync<float>();
		Range = new Sync<float>();
		SpotAngle = new Sync<float>();
		Cookie = new AssetRef<ITexture>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => LightType, 
			4 => Intensity, 
			5 => Color, 
			6 => ShadowType, 
			7 => ShadowStrength, 
			8 => ShadowNearPlane, 
			9 => ShadowMapResolution, 
			10 => ShadowBias, 
			11 => ShadowNormalBias, 
			12 => Range, 
			13 => SpotAngle, 
			14 => Cookie, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static Light __New()
	{
		return new Light();
	}
}
