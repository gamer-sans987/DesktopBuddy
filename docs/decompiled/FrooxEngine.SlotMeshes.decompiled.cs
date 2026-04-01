using System;
using Elements.Core;

namespace FrooxEngine;

public static class SlotMeshes
{
	public static QuadMesh AttachQuad(this Slot slot, float2 size, IAssetProvider<Material> material, bool collider = true)
	{
		QuadMesh quadMesh = slot.AttachMesh<QuadMesh>(material);
		quadMesh.Size.Value = size;
		if (collider)
		{
			slot.AttachComponent<BoxCollider>().Size.Value = size;
		}
		return quadMesh;
	}

	public static QuadMesh AttachQuad<MAT>(this Slot slot, float2 size, bool collider = true) where MAT : Component, IAssetProvider<Material>, new()
	{
		return slot.AttachQuad(size, slot.AttachComponent<MAT>(), collider);
	}

	public static SphereMesh AttachSphere(this Slot slot, float radius, IAssetProvider<Material> material, bool collider = true)
	{
		SphereMesh sphereMesh = slot.AttachMesh<SphereMesh>(material);
		sphereMesh.Radius.Value = radius;
		if (collider)
		{
			slot.AttachComponent<SphereCollider>().Radius.Value = radius;
		}
		return sphereMesh;
	}

	public static BoxMesh AttachBox(this Slot slot, float3 size, IAssetProvider<Material> material, bool collider = true)
	{
		BoxMesh boxMesh = slot.AttachMesh<BoxMesh>(material);
		boxMesh.Size.Value = size;
		if (collider)
		{
			slot.AttachComponent<BoxCollider>().Size.Value = size;
		}
		return boxMesh;
	}

	public static ArrowMesh AttachArrow(this Slot slot, float3 vector, colorX color)
	{
		AttachedModel<ArrowMesh, PBS_Metallic> attachedModel = slot.AttachArrow<PBS_Metallic>(vector);
		attachedModel.material.AlbedoColor.Value = color;
		return attachedModel.mesh;
	}

	public static AttachedModel<ArrowMesh, MAT> AttachArrow<MAT>(this Slot slot, float3 vector) where MAT : Component, IAssetProvider<Material>, new()
	{
		AttachedModel<ArrowMesh, MAT> result = slot.AttachMesh<ArrowMesh, MAT>();
		result.mesh.Vector.Value = vector;
		return result;
	}

	public static BoxMesh AttachBox<MAT>(this Slot slot, float3 size, bool collider = true) where MAT : Component, IAssetProvider<Material>, new()
	{
		return slot.AttachBox(size, slot.AttachComponent<MAT>(), collider);
	}

	public static SphereMesh AttachSphere<MAT>(this Slot slot, float radius, bool collider = true) where MAT : Component, IAssetProvider<Material>, new()
	{
		return slot.AttachSphere(radius, slot.AttachComponent<MAT>(), collider);
	}

	public static CylinderMesh AttachCylinder<MAT>(this Slot slot, float radius, float height, bool collider = true) where MAT : Component, IAssetProvider<Material>, new()
	{
		return slot.AttachCylinder(radius, height, slot.AttachComponent<MAT>(), collider);
	}

	public static CylinderMesh AttachCylinder(this Slot slot, float radius, float height, IAssetProvider<Material> mat, bool collider = true)
	{
		CylinderMesh cylinderMesh = slot.AttachMesh<CylinderMesh>(mat);
		cylinderMesh.Radius.Value = radius;
		cylinderMesh.Height.Value = height;
		if (collider)
		{
			CylinderCollider cylinderCollider = slot.AttachComponent<CylinderCollider>();
			cylinderCollider.Radius.Value = radius;
			cylinderCollider.Height.Value = height;
		}
		return cylinderMesh;
	}

	public static MESH AttachMesh<MESH>(this Slot slot, IAssetProvider<Material> material, bool collider = false, int sortingOrder = 0) where MESH : Component, IAssetProvider<Mesh>, new()
	{
		MeshRenderer renderer;
		return slot.AttachMesh<MESH>(material, out renderer, collider, sortingOrder);
	}

	public static MESH AttachMesh<MESH>(this Slot slot, IAssetProvider<Material> material, out MeshRenderer renderer, bool collider = false, int sortingOrder = 0) where MESH : Component, IAssetProvider<Mesh>, new()
	{
		renderer = slot.AttachComponent<MeshRenderer>();
		MESH val = slot.AttachComponent<MESH>();
		renderer.Mesh.Target = val;
		renderer.Material.Target = material;
		renderer.SortingOrder.Value = sortingOrder;
		if (collider)
		{
			slot.AttachComponent<MeshCollider>().Mesh.Target = val;
		}
		return val;
	}

	public static MAT AttachMesh<MAT>(this Slot slot, IAssetProvider<Mesh> mesh, bool collider = false, int sortingOrder = 0) where MAT : Component, IAssetProvider<Material>, new()
	{
		MeshRenderer meshRenderer = slot.AttachComponent<MeshRenderer>();
		MAT val = slot.AttachComponent<MAT>();
		meshRenderer.Mesh.Target = mesh;
		meshRenderer.Material.Target = val;
		meshRenderer.SortingOrder.Value = sortingOrder;
		if (collider)
		{
			slot.AttachComponent<MeshCollider>().Mesh.Target = mesh;
		}
		return val;
	}

	public static AttachedModel<MESH, MAT> AttachMesh<MESH, MAT>(this Slot slot, bool collider = false, int sortingOrder = 0) where MESH : Component, IAssetProvider<Mesh>, new() where MAT : Component, IAssetProvider<Material>, new()
	{
		MeshRenderer meshRenderer = slot.AttachComponent<MeshRenderer>();
		MESH val = slot.AttachComponent<MESH>();
		MAT val2 = slot.AttachComponent<MAT>();
		meshRenderer.SortingOrder.Value = sortingOrder;
		MeshCollider meshCollider = null;
		if (collider)
		{
			meshCollider = slot.AttachComponent<MeshCollider>();
			meshCollider.Mesh.Target = val;
		}
		meshRenderer.Mesh.Target = val;
		meshRenderer.Material.Target = val2;
		return new AttachedModel<MESH, MAT>(val, val2, meshRenderer, meshCollider);
	}

	public static AttachedModel<MESH, MAT> AttachMesh<MESH, MAT>(this Slot slot, colorX color) where MESH : Component, IAssetProvider<Mesh>, new() where MAT : Component, ICommonMaterial, IAssetProvider<Material>, new()
	{
		AttachedModel<MESH, MAT> result = slot.AttachMesh<MESH, MAT>();
		result.material.Color = color;
		return result;
	}

	public static MeshRenderer AttachMesh(this Slot slot, IAssetProvider<Mesh> mesh, IAssetProvider<Material> material, int sortingOrder = 0)
	{
		MeshRenderer meshRenderer = slot.AttachComponent<MeshRenderer>();
		meshRenderer.SortingOrder.Value = sortingOrder;
		meshRenderer.Mesh.Target = mesh;
		int num = mesh.Asset?.Data?.SubmeshCount ?? 1;
		for (int i = 0; i < num; i++)
		{
			meshRenderer.Materials.Add(material);
		}
		return meshRenderer;
	}

	public static Slot AttachPrimitive<MAT>(this Slot slot, Primitive primitive, bool collider = true) where MAT : Component, ICommonMaterial, IAssetProvider<Material>, new()
	{
		return slot.AttachPrimitive<MAT>(primitive, float3.One, colorX.White, collider);
	}

	public static Slot AttachPrimitive<MAT>(this Slot slot, Primitive primitive, float3 size, bool collider = true) where MAT : Component, ICommonMaterial, IAssetProvider<Material>, new()
	{
		return slot.AttachPrimitive<MAT>(primitive, size, colorX.White, collider);
	}

	public static Slot AttachPrimitive<MAT>(this Slot slot, Primitive primitive, colorX color, bool collider = true) where MAT : Component, ICommonMaterial, IAssetProvider<Material>, new()
	{
		return slot.AttachPrimitive<MAT>(primitive, float3.One, color, collider);
	}

	public static Slot AttachPrimitive<MAT>(this Slot slot, Primitive primitive, float3 size, colorX color, bool collider = true) where MAT : Component, ICommonMaterial, IAssetProvider<Material>, new()
	{
		Slot slot2 = slot.AddSlot(primitive.ToString());
		slot2.LocalScale = size;
		IAssetProvider<Mesh> target;
		switch (primitive)
		{
		case Primitive.Quad:
			target = slot2.AttachComponent<QuadMesh>();
			if (collider)
			{
				slot2.AttachComponent<BoxCollider>().Size.Value = new float3(1f, 1f);
			}
			break;
		case Primitive.Cube:
			target = slot2.AttachComponent<BoxMesh>();
			if (collider)
			{
				slot2.AttachComponent<BoxCollider>();
			}
			break;
		case Primitive.Sphere:
			target = slot2.AttachComponent<SphereMesh>();
			if (collider)
			{
				slot2.AttachComponent<SphereCollider>();
			}
			break;
		default:
			throw new ArgumentException("Invalid Primitive Type");
		}
		MAT val = slot2.AttachComponent<MAT>();
		MeshRenderer meshRenderer = slot2.AttachComponent<MeshRenderer>();
		val.Color = color;
		meshRenderer.Mesh.Target = target;
		meshRenderer.Material.Target = val;
		return slot2;
	}

	public static MAT AttachSkybox<MAT>(this Slot slot) where MAT : Component, IAssetProvider<Material>, new()
	{
		Skybox skybox = slot.AttachComponent<Skybox>();
		MAT val = slot.AttachComponent<MAT>();
		skybox.Material.Target = val;
		skybox.SetupAmbientLight();
		return val;
	}
}
