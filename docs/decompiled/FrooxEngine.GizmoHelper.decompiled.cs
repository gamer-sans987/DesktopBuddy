using System;
using System.Collections.Generic;
using System.Linq;
using Elements.Core;

namespace FrooxEngine;

public static class GizmoHelper
{
	public const float RADIUS = 0.05f;

	private static Dictionary<Type, Type> _componentGizmos;

	private static HashSet<Type> _spawnOnDevModeEnabled;

	internal static void Initialize(Type[] workers)
	{
		if (_componentGizmos != null)
		{
			return;
		}
		_componentGizmos = new Dictionary<Type, Type>();
		_spawnOnDevModeEnabled = new HashSet<Type>();
		foreach (Type type in workers)
		{
			if (!(type.GetCustomAttributes(typeof(GizmoForComponent), inherit: false).FirstOrDefault() is GizmoForComponent gizmoForComponent))
			{
				continue;
			}
			try
			{
				_componentGizmos.Add(gizmoForComponent.TargetComponent, type);
				if (type.GetCustomAttributes(typeof(SpawnOnDevModeEnabled), inherit: true).Any())
				{
					_spawnOnDevModeEnabled.Add(type);
				}
			}
			catch
			{
				UniLog.Error($"Multiple Gizmos for component {gizmoForComponent.TargetComponent}");
			}
		}
	}

	public static bool ShouldSpawnOnDevModeEnabled(Type gizmoType)
	{
		return _spawnOnDevModeEnabled.Contains(gizmoType);
	}

	public static Type GetGizmoType(Type componentType)
	{
		_componentGizmos.TryGetValue(componentType, out Type value);
		return value;
	}

	public static bool IsGizmoActive(this Worker worker, Type gizmoType = null)
	{
		return worker.TryGetGizmo(gizmoType)?.IsActive ?? false;
	}

	public static void ActivateGizmo(this Worker worker, Type gizmoType = null)
	{
		worker.GetGizmo(gizmoType).IsActive = true;
	}

	public static void DeactivateGizmo(this Worker worker, Type gizmoType = null)
	{
		IComponentGizmo componentGizmo = worker.TryGetGizmo(gizmoType);
		if (componentGizmo != null)
		{
			componentGizmo.IsActive = false;
		}
	}

	public static T TryGetGizmo<T>(this Worker worker) where T : class, IComponentGizmo
	{
		return worker?.TryGetGizmo(typeof(T)) as T;
	}

	public static IComponentGizmo TryGetGizmo(this Worker worker, Type gizmoType = null)
	{
		if (worker.IsLocalElement)
		{
			return null;
		}
		Component component = worker as Component;
		Slot slot = worker as Slot;
		if (component == null && slot == null)
		{
			return null;
		}
		gizmoType = gizmoType ?? worker.GizmoType;
		return (component?.Slot ?? slot)?.GetComponent((GizmoLink link) => link.TargetWorker == worker && link.LinkedGizmoType == gizmoType)?.Gizmo;
	}

	public static void RemoveGizmo(this Worker worker, Type gizmoType = null)
	{
		worker.TryGetGizmo(gizmoType)?.Slot.Destroy();
	}

	public static T GetGizmo<T>(this Worker worker, bool isExplicit = false) where T : IComponentGizmo
	{
		return (T)worker.GetGizmo(typeof(T), isExplicit);
	}

	public static IComponentGizmo GetGizmo(this Worker worker, bool isExplicit = false)
	{
		return worker.GetGizmo(null, isExplicit);
	}

	public static IComponentGizmo GetGizmo(this Worker worker, Type gizmoType, bool isExplicit = false)
	{
		IComponentGizmo componentGizmo = worker.TryGetGizmo(gizmoType);
		if (componentGizmo != null)
		{
			return componentGizmo;
		}
		Component component = worker as Component;
		Slot slot = worker as Slot;
		gizmoType = gizmoType ?? worker.GizmoType;
		if (gizmoType == null)
		{
			return null;
		}
		componentGizmo = (IComponentGizmo)worker.World.AddSlot("Gizmo").AttachComponent(gizmoType);
		componentGizmo.Setup(worker);
		(component?.Slot ?? slot).AttachComponent<GizmoLink>().Setup(worker, componentGizmo);
		if (isExplicit)
		{
			foreach (DevTool item in worker.LocalUser.GetActiveTools().OfType<DevTool>())
			{
				item.SetActiveSlotGizmo(slot);
			}
		}
		return componentGizmo;
	}

	public static void SetupMaterial(OverlayFresnelMaterial material, colorX color)
	{
		material.BlendMode.Value = BlendMode.Alpha;
		material.Sidedness.Value = Sidedness.Front;
		SetMaterialColor(material, color);
	}

	public static void SetMaterialColor(OverlayFresnelMaterial material, colorX color)
	{
		ColorHSV colorHSV2;
		ColorHSV colorHSV = (colorHSV2 = new ColorHSV(in color));
		colorHSV2.h += 0.02f;
		colorHSV2.s *= 0.85f;
		colorHSV2.v *= 1.25f;
		ColorHSV colorHSV3 = colorHSV;
		colorHSV3.v *= 0.75f;
		colorX colorX = colorHSV2.ToRGB(color.profile);
		colorX value = colorHSV3.ToRGB(color.profile);
		material.FrontNearColor.Value = value;
		material.FrontFarColor.Value = new colorX(colorX.rgb, color.a, color.profile);
		material.BehindNearColor.Value = new colorX(value.rgb, color.a * 0.25f, color.profile);
		material.BehindFarColor.Value = new colorX(colorX.rgb, color.a * 0.25f, color.profile);
	}

	public static void SetupLogoMenu(LegacySegmentCircleMenuController menu)
	{
		menu.LogoCircle.Value = false;
		menu.HighlightRadiusOffset.Value = 0.010000001f;
		menu.LogoMenuMesh.OutlineWidth.Value = 0.002f;
		menu.DisabledFillColor.Value = new colorX(0.5f);
		menu.DisabledOutlineColor.Value = new colorX(0.25f);
	}

	public static void SetupLogoMenuItem(LegacySegmentCircleMenuController.Item item)
	{
		item.RadiusStart.Value = 0.05f;
		item.Thickness.Value = 0.025f;
		item.ArcLength.Value = item.ArcFromThickness;
	}

	public static void SetItemColor(LegacySegmentCircleMenuController.Item item, colorX color)
	{
		LegacyUIStyle.SetItemColor(item, color);
	}
}
