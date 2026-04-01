# Slot, ProtoFlux, and Type System Reference

## Slot

`FrooxEngine.Slot` -- the fundamental scene-graph node. Extends `ContainerWorker<Component>`.

### Synced Fields

| Field | Type | Notes |
|---|---|---|
| `Name_Field` | `Sync<string>` | Slot name |
| `Tag_Field` | `Sync<string>` | For finding/classifying slots |
| `ActiveSelf_Field` | `Sync<bool>` | Local active state |
| `Persistent_Field` | `Sync<bool>` | `[NonPersistent]` attribute -- NOT saved itself but controls persistence of hierarchy |
| `Position_Field` | `Sync<float3>` | Local position |
| `Rotation_Field` | `Sync<floatQ>` | Local rotation |
| `Scale_Field` | `Sync<float3>` | Local scale |
| `OrderOffset_Field` | `Sync<long>` | Controls child ordering |
| `ParentReference` | `SyncRef<Slot>` | Parent slot reference |

### Key Properties

- `Name`, `Tag`, `ActiveSelf`, `PersistentSelf` -- wrappers around the Sync fields above.
- `Parent` -- get/set. Setting calls `SetParent()` which preserves global transform by default. Returns `World.RootSlot` if no parent set and not root.
- `RawParent` -- the raw reference; may return a stale/removed parent. Not guaranteed consistent across clients.
- `IsActive` -- hierarchical: false if any ancestor is inactive.
- `IsPersistent` -- hierarchical: false if any ancestor is non-persistent.
- `IsRootSlot` -- true only for the world root.
- `IsProtected` -- protected slots cannot be reparented, deactivated, or destroyed.
- `ForcedPersistent` -- prevents toggling persistence off.
- `ChildrenCount`, `Children`, `LocalChildrenCount`, `LocalChildren` -- child access. `Children` triggers sort.
- `this[int]` -- indexed child access (triggers child order sort).
- `ChildIndex` -- get/set. Setting swaps with the slot currently at that index.
- `ActiveUser`, `ActiveUserRoot`, `IsUnderLocalUser` -- user ownership tracking.

### Transform

- `LocalPosition`, `LocalRotation`, `LocalScale` -- local-space transform.
- `GlobalPosition`, `GlobalRotation`, `GlobalScale` -- world-space; setting back-computes local.
- `Forward`, `Up`, `Backward`, `Down`, `Left`, `Right` -- world-space direction vectors (get/set).
- `TRS` -- local transform matrix (`float4x4`). Setting decomposes into position/rotation/scale.
- `LocalToGlobal`, `GlobalToLocal` -- cached matrices with lazy recomputation.
- `LocalToGlobalQuaternion`, `LocalToGlobalScale` -- decomposed from the matrix.
- `HasIdentityTransform` -- true if pos=Zero, rot=Identity, scale=One.
- `GetLocalToSpaceMatrix(Slot space)` -- computes transform from this slot's space into another's.

Gotchas:
- Transform caching uses bitflags (`_transformElementValid`). Invalidation propagates to children.
- Non-uniform scale flag (`_isUniformScale`) triggers broader invalidation.
- Setting `GlobalPosition`/`GlobalRotation`/`GlobalScale` with NaN/Infinity/invalid values is silently ignored.
- Root slot has filters that throw exceptions if you try to change its transform, active, or persistence.
- Position filter resets NaN/Infinity to `float3.Zero`; rotation filter normalizes or resets to identity; scale filter resets to `float3.One`.

### Hierarchy Methods

- `AddSlot(name, persistent)` -- creates a child. Returns local slot if `IsLocalElement` is true.
- `AddLocalSlot(name, persistent)` -- always creates a local (non-synced) child.
- `InsertSlot(index, name)` -- add child at specific index.
- `SetParent(newParent, keepGlobalTransform=true)` -- reparent. Throws if in init phase with non-init parent. Prevents circular parenting. Skips if protected.
- `TrySetParent(newParent, keepGlobalTransform)` -- no-op if parent is already the target.
- `ReparentChildren(newParent)` -- moves all children (including local) to new parent.
- `SortChildren(Comparison<Slot>)` -- reassigns `OrderOffset` values (i*100).
- `InsertAtIndex(index)` -- moves this slot to a specific child index via `OrderOffset` manipulation.
- `SwapChildren(a, b)` -- static; swaps two children by index.
- `IsChildOf(slot, includeSelf)` -- hierarchy membership test.
- `ComputeHierarchyDepth(root)` -- returns -1 if root is not an ancestor.
- `FindCommonRoot(other)` -- nearest common ancestor.

### Finding Children/Parents

- `FindChild(name)` -- direct children only, by exact name.
- `FindChildInHierarchy(name)` -- recursive depth-first search.
- `FindChildOrAdd(name, persistent)` -- find or create.
- `FindChild(Predicate<Slot>, maxDepth)` -- predicate-based search.
- `FindChild(name, matchSubstring, ignoreCase, maxDepth)` -- flexible name search.
- `FindParent(Predicate<Slot>, maxDepth)` -- walks up the hierarchy.
- `GetAllChildren(includeSelf)` -- returns flat list of entire subtree.
- `ForeachChild(action/func, includeSelf)` -- walk subtree. `Func<Slot,bool>` variant stops on false.
- `ForeachChildDepthFirst(action, includeSelf, childrenInReverseOrder)` -- depth-first; action called after children.

### Component Search

- `GetComponentInChildren<T>(filter, includeLocal, excludeDisabled)` -- first match in subtree.
- `GetComponentsInChildren<T>(filter, ...)` -- all matches in subtree.
- `GetComponentInParents<T>(filter, includeSelf, excludeDisabled)` -- first match walking up.
- `GetComponentsInParents<T>(filter)` -- all matches walking up.
- `GetComponentInParentsOrChildren<T>()` -- parents first, then children.
- `GetComponentInChildrenOrParents<T>()` -- children first, then parents.
- `ForeachComponentInChildren<T>(callback, includeLocal, cacheItems)` -- action or `Func<T,bool>` stopper.
- `ForeachComponentInParents<T>(callback, cacheItems, excludeDisabled)` -- same pattern.
- `GetFirstDirectComponentsInChildren<T>(...)` -- finds first component at each branch, does not descend past a match.

### Destruction

- `Destroy()` -- destroys slot and entire subtree.
- `Destroy(moveChildren)` -- destroys this slot only, reparents children to `moveChildren`.
- `DestroyChildren(preserveAssets, sendDestroyingEvent, includeLocal, filter)` -- keeps this slot.
- `DestroyPreservingAssets(relocateAssets, sendDestroyingEvent)` -- preserves `IAssetProvider` components referenced externally; destroys everything else.

Gotchas:
- Cannot destroy root slot (throws).
- Protected slots silently skip destruction.
- `sendDestroyingEvent` controls whether `OnDestroying` fires on components. Default true.
- `DestroyPreservingAssets` relocates asset slots to `World.AssetsSlot` if no target given.

### Duplication

- `Duplicate(duplicateRoot, keepGlobalTransform, settings, duplicateAsLocal)` -- deep copy. Cannot duplicate root. `DuplicationSettings` allows filtering slots/components and remapping types.
- `DuplicateComponent<T>(source, breakExternalReferences)` -- copies a single component onto this slot.
- `DuplicateComponents(sources, breakExternalReferences)` -- batch version.

### Events

| Event | Signature | Notes |
|---|---|---|
| `OnPrepareDestroy` | `SlotEvent` | Before destruction |
| `Destroyed` | `Action<IDestroyable>` | After destruction |
| `Changed` | `Action<IChangeable>` | Any sync member change; propagates through hierarchy |
| `ActiveChanged` | `SlotEvent` | Hierarchical active state changed |
| `PersistentChanged` | `SlotEvent` | Hierarchical persistence changed |
| `NameChanged` | `SlotEvent` | Name field changed |
| `OrderOffsetChanged` | `SlotEvent` | Child order changed |
| `ParentChanged` | `SlotEvent` | Reparented |
| `ActiveUserRootChanged` | `SlotEvent` | User root association changed |
| `ChildAdded` | `SlotChildEvent` | Child added |
| `ChildRemoved` | `SlotChildEvent` | Child removed |
| `WorldTransformChanged` | `SlotEvent` | Uses `GeneralMovedHierarchyEventHandler` |
| `PhysicsWorldTransformChanged` | `SlotEvent` | Physics-specific transform events |
| `PhysicsWorldScaleChanged` | `SlotEvent` | Physics-specific scale events |
| `ChildrenOrderInvalidated` | `SlotEvent` | Child sort invalidated |

### Serialization

- `Save(SaveControl)` -- serializes to `DataTreeDictionary`. Skips non-persistent children unless `SaveNonPersistent` is set. Skips children with an `ActiveUserRoot`.
- `Load(DataTreeNode, LoadControl)` -- deserializes. Special case: root slot accepts a `DataTreeList` directly.

### Misc

- `TagHierarchy(tag)` -- sets tag on entire subtree.
- `GetChildrenWithTag(tag)` -- recursive tag search.
- `CopyComponents(target)` -- copies all components from target onto this slot.
- `CopyTransform(slot)` -- optimized: uses local copy if same parent, otherwise matrix decomposition.
- `SetIdentityTransform()` -- resets local transform.
- `TransformByAnother(other, globalPos, globalRot, globalScale)` -- applies a virtual transform.
- `MarkProtected(forcePersistent)` -- marks slot and all ancestors as protected.

---

## SlotMeshes

`FrooxEngine.SlotMeshes` -- static extension methods on `Slot` for quickly attaching meshes with renderers, materials, and optional colliders.

### Methods

| Method | Returns | Description |
|---|---|---|
| `AttachQuad(size, material, collider)` | `QuadMesh` | Quad + optional BoxCollider |
| `AttachQuad<MAT>(size, collider)` | `QuadMesh` | Generic material version |
| `AttachSphere(radius, material, collider)` | `SphereMesh` | Sphere + optional SphereCollider |
| `AttachSphere<MAT>(radius, collider)` | `SphereMesh` | Generic material version |
| `AttachBox(size, material, collider)` | `BoxMesh` | Box + optional BoxCollider |
| `AttachBox<MAT>(size, collider)` | `BoxMesh` | Generic material version |
| `AttachCylinder(radius, height, material, collider)` | `CylinderMesh` | Cylinder + optional CylinderCollider |
| `AttachCylinder<MAT>(radius, height, collider)` | `CylinderMesh` | Generic material version |
| `AttachArrow(vector, color)` | `ArrowMesh` | Arrow with PBS_Metallic material |
| `AttachArrow<MAT>(vector)` | `AttachedModel<ArrowMesh, MAT>` | Generic material arrow |
| `AttachMesh<MESH>(material, collider, sortingOrder)` | `MESH` | Generic mesh + renderer setup |
| `AttachMesh<MESH>(material, out renderer, ...)` | `MESH` | Same but outputs the renderer |
| `AttachMesh<MAT>(mesh, collider, sortingOrder)` | `MAT` | Attach material to existing mesh |
| `AttachMesh<MESH, MAT>(collider, sortingOrder)` | `AttachedModel<MESH, MAT>` | Both mesh and material generic |
| `AttachMesh<MESH, MAT>(color)` | `AttachedModel<MESH, MAT>` | With color on ICommonMaterial |
| `AttachMesh(mesh, material, sortingOrder)` | `MeshRenderer` | Non-generic; handles submeshes |
| `AttachPrimitive<MAT>(primitive, size, color, collider)` | `Slot` | Creates a child slot with the primitive. Supports `Quad`, `Cube`, `Sphere`. |
| `AttachSkybox<MAT>()` | `MAT` | Skybox + material + ambient light |

Gotchas:
- `AttachPrimitive` creates a new child slot (unlike the others which attach to the current slot).
- The non-generic `AttachMesh(mesh, material)` adds one material per submesh.
- `collider` defaults to `true` for shape methods, `false` for generic `AttachMesh`.

---

## ProtoFluxNode

`FrooxEngine.ProtoFlux.ProtoFluxNode` -- abstract base for all ProtoFlux visual programming nodes. Extends `Component`.

### Key Properties

- `NodeInstance` (abstract) -- the runtime `INode` instance. null until built.
- `NodeType` (abstract) -- the underlying ProtoFlux.Core node type.
- `Group` -- the `ProtoFluxNodeGroup` this node belongs to. Assigned during build.
- `IsBuilt` -- whether the node has been compiled into its group.
- `NodeName` -- display name derived from type name.
- `OverrideWidth`, `SupressLabels`, `SupressHeaderAndFooter`, `OverrideOverviewMode` -- visual customization overrides.

### Element Counts and Accessors

Nodes have fixed and dynamic (list-based) elements in these categories:
- **Inputs** (`NodeInputCount`, `NodeInputListCount`) -- data inputs (SyncRefs to INodeOutput)
- **Outputs** (`NodeOutputCount`, `NodeOutputListCount`) -- data outputs (INodeOutput)
- **Impulses** (`NodeImpulseCount`, `NodeImpulseListCount`) -- flow outputs (SyncRefs to INodeOperation)
- **Operations** (`NodeOperationCount`, `NodeOperationListCount`) -- flow inputs (INodeOperation)
- **References** (`NodeReferenceCount`) -- references to other ProtoFluxNodes
- **GlobalRefs** (`NodeGlobalRefCount`, `NodeGlobalRefListCount`) -- references to global value proxies

Accessors: `GetInput(i)`, `GetOutput(i)`, `GetImpulse(i)`, `GetOperation(i)`, `GetReference(i)`, `GetGlobalRef(i)`, and list versions (`GetInputList(i)`, etc.). All throw `ArgumentOutOfRangeException` on invalid index.

### Enumerables

- `NodeInputs`, `NodeOutputs`, `NodeImpulses`, `NodeOperations`, `NodeReferences`, `NodeGlobalRefs` -- iterate fixed elements.
- `NodeInputLists`, `NodeOutputLists`, etc. -- iterate dynamic lists.
- `AllInputs`, `AllImpulses` -- fixed + dynamic flattened.
- `AllSourceOutputs` -- all connected source outputs.
- `AllTargetOperations` -- all connected target operations.
- `ReferencedNodes` -- yields all nodes this one connects to (outputs, operations, references).
- `AllNodeElements` -- every element of every type.

### Connection Methods

- `TryConnectInput(input, output, allowExplicitCast, undoable)` -- connects an input to an output. Falls back to group-level connection (for cast insertion). Returns bool.
- `TryConnectImpulse(impulse, operation, undoable)` -- connects a flow impulse.
- `TryConnectReference(reference, node, undoable)` -- connects a node reference.
- `GetDefaultInputValue(input)` -- retrieves the default value from `NodeMetadata`.

### Build Lifecycle

1. `MarkForRebuild()` -- sets `IsBuilt=false`, registers with `World.ProtoFlux`.
2. `Rebuild(ref currentGroup)` -- recursively builds referenced nodes, merges groups, registers this node.
3. After rebuild: `MapOutputs()`, `MapOperations()`, `MapInputs()`, `MapImpulses()`, `MapReferences()`, `MapGlobalRefs()` -- wires up runtime instances.
4. `ClearInstance()`, `UnmapOutputs()`, `UnmapOperations()` -- teardown.

Gotchas:
- `OnChanges` triggers `MarkForRebuild` if any connection changed after being built.
- Group merging picks the larger group and merges the smaller one into it.
- `AssociateInstance` is for externally instantiated nodes; throws if already has instance or group.
- `EnsureElementsInDynamicLists()` adds 2 elements to every dynamic list (used for initial setup).

### Inspector

- `BuildInspectorUI` -- standard inspector + "Dump ProtoFlux Node Structure to clipboard" button + debug info text showing build state, group info, continuous changes.

---

## ProtoFluxNodeVisual

`FrooxEngine.ProtoFlux.ProtoFluxNodeVisual` -- the UI component that renders a ProtoFlux node in-world.

### Constants

| Constant | Value | Description |
|---|---|---|
| `DEFAULT_SCALE` | 0.0009375 | World-space scale |
| `DEFAULT_WIDTH` | 128 | Canvas width |
| `SLOT_NAME` | `<NODE_UI>` | Slot name convention |
| `NODE_SCALE` | 1.25 | |
| `CONNECT_POINT_NAME` | `<WIRE_POINT>` | Wire connection slot name |
| `ELEMENT_HEIGHT` | 32 | Input/output row height |
| `CONNECTOR_WIDTH` | 16 | |
| `LABEL_HEIGHT` | 24 | Header label height |
| `FOOTER_HEIGHT` | 16 | |
| `COLOR_BOOST` | 1.5 | Multiplier for type colors |

### Fields

- `Node` (`RelayRef<ProtoFluxNode>`) -- the node this visual represents.
- `IsSelected`, `IsHighlighted` (`Sync<bool>`) -- both `[NonPersistent]`.
- `_bgImage`, `_inputsRoot`, `_outputsRoot`, `_referencesRoot` -- internal UI structure refs.
- `_overviewVisual`, `_overviewBg`, `_labelBg`, `_labelText` -- drives for overview mode toggle.

### Key Properties

- `IsNodeValid` -- true if node's group is valid.
- `NodeType` -- shortcut to `Node.Target.NodeType`.
- `NodeMetadata` -- fetched via `NodeMetadataHelper.GetMetadata`.
- `LocalUIBuilder` -- lazily created `UIBuilder` with `RadiantUI_Constants` editor style.

### Methods

- `GenerateVisual(ProtoFluxNode)` -- one-time setup. Attaches Canvas, Grabbable, builds full UI. Throws if Node already assigned.
- `GetFixedInputProxy(name)`, `GetFixedOutputProxy(name)`, `GetFixedImpulseProxy(name)`, `GetFixedOperationProxy(name)` -- find proxy components by element name.
- `GetDynamicInputProxy(name, index)`, `GetDynamicOutputProxy(name, index)`, etc. -- dynamic list element proxies.
- `UpdateNodeStatus()` -- updates background color based on selection (cyan), highlight (yellow), validity (red).
- `GenerateInputElement(...)`, `GenerateOutputElement(...)`, `GenerateImpulseElement(...)`, `GenerateOperationElement(...)` -- create individual connector UI elements.
- `GenerateReferenceElement(...)`, `GenerateGlobalRefElement(...)` -- reference field UI.

### UI Structure (BuildUI)

1. Vertical layout with background image.
2. Header with node name (if not suppressed).
3. Overlapping layout containing:
   - Left column: Operations + Inputs
   - Right column: Impulses + Outputs
4. Custom content via `node.BuildContentUI(visual, ui)`.
5. Overview panel (toggled by `ProtofluxUserEditSettings.OverviewMode`).
6. References section.
7. Footer with category path.

Gotchas:
- `OnDestroy` fires `PackStateChanged(isPacked: true)` on the node if it implements `IProtoFluxNodePackUnpackListener`.
- If the referenced node is removed, the visual destroys itself and its parent Grabbable.
- Overview mode hides header label and shows a centered name overlay instead.
- Duplication clears `IsSelected`.

---

## ValueStream / ReferenceStream

No source available.

---

## Text

No source available.

---

## Permission

No source available.

---

## TypeManager

`FrooxEngine.TypeManager` -- per-World type encoding/decoding system for serialization and network sync.

### Key Properties

- `World` -- the owning World.
- `AllowedAssemblies` -- `IEnumerable<AssemblyTypeRegistry>` of registered assemblies.
- `CompatibilityHash` -- MD5-based hash combining `SystemCompatibilityHash` and all non-dependency assembly hashes.

### Assembly Initialization

- `InitializeAssemblies(assemblies)` -- called once. Registers provided assemblies, auto-discovers dependency assemblies, collects moved-type assemblies, computes `CompatibilityHash`. Throws if called twice.

### Type Support

- `IsSupported(Type)` -- checks if a type can be used in this world's data model. Caches results. Handles generics by checking definition + all arguments.
- For a type to be supported: it must be a system type OR belong to a registered assembly that includes it.

### Binary Encoding/Decoding

- `EncodeType(BinaryWriter, Type)` -- writes type as 7-bit encoded indices. System types = single index. Assembly types = assembly index + type index. Generic types recursively encode definition + arguments.
- `DecodeType(BinaryReader)` -- inverse. Reconstructs generics.

### String Encoding/Decoding

- `EncodeType(Type) -> string` -- format: `[AssemblyName]FullTypeName` for assembly types. Uses `NiceName` for system types.
- `DecodeType(string) -> Type` -- parses the `[Assembly]TypeName` format. Caches results.
- `ParseNiceType(string, allowAmbiguous)` -- uses `NiceTypeParser`.

### Legacy Support

- `DecodeLegacyType(string)` -- handles old serialization formats. Replaces `Neos` -> `Legacy`, `CloudX.Shared` -> `SkyFrost.Base`, `BaseX` -> mapped, `CodeX` -> mapped. Maps legacy quantity types.
- `MapLegacyType(typename, assembly)` -- explicit mapping for `QuantityX`/`Elements.Quantity` types and cloud types.
- `LegacyMatch` = "Neos", `LegacyMatchCloud` = "CloudX.Shared", `LegacyMatchCore` = "BaseX", `LegacyMatchAssets` = "CodeX".

### Factory

- `Instantiate<T>()` (static) -- creates a worker using `WorkerHelper<T>.New()`.
- `Instantiate(Type)` (static) -- creates via `Activator.CreateInstance`.

---

## GlobalTypeRegistry

`FrooxEngine.GlobalTypeRegistry` -- static, global type registration system. Manages all data-model assemblies and system types.

### Constants

- `SYSTEM_COMPATIBILITY_VERSION` = 3 -- manually bumped for binary-incompatible changes that don't add/remove types.

### Initialization

- `Initialize(metadataCachePath, fastCompatibilityHash)` -- registers all system types: `Coder.BaseEnginePrimitives`, quantity types, `Nullable<>`, `object`, `void`, `Type`, `Guid`, common enums, `Task`/`Task<>`, all `Action<>` (0-16 args), all `Func<>` (0-17 args), `Predicate<>`.
- `FinalizeTypes()` -- called once after all assemblies registered. Computes `SystemCompatibilityHash`, processes moved types, sets `_finalized` flag. After this, no more registrations allowed.

### Assembly Registration

- `RegisterAssembly(assembly, assemblyType, types)` -- creates `AssemblyTypeRegistry`, adds to lookup dictionaries. Thread-safe (locked).
- Assembly types: `Core`, `UserspaceCore`, `Dependency`.
- `CoreAssemblies`, `UserspaceCoreAssemblies` -- read-only lists.
- `DataModelAssemblies` -- non-dependency assemblies.
- `GetTypeRegistry(name/assembly)` -- lookup; throws if not found.
- `TryGetTypeRegistry(name)` -- returns null instead of throwing.

### System Types

- `SystemTypeCount` -- number of registered system types.
- `IsSystemType(Type)` -- check membership.
- `TryGetSystemTypeIndex(Type)` -- returns -1 if not found.
- `GetSystemType(index)` -- by index.
- `GetSystemTypeName(index/Type)`, `TryGetSystemTypeName(Type)` -- name lookup.

### Type Validation

- `IsSupportedType(Type)` -- static check. Handles generics recursively.
- `ValidateAllTypes(invalidTypes, progress)` -- validates all types in core assemblies.

### External Types

- `RegisterExternalType(Type)` -- for types from non-data-model assemblies. Must be called before `FinalizeTypes()`. Throws if type belongs to a data-model assembly.
- `RegisterMovedType(type, typename, assembly)` -- for types that moved between assemblies.
- `RegisterOldTypeHash(hash, type)` -- for backward compat with old serialized type hashes.
- `TryGetOldTypeByHash(name)` -- looks up by SpookyHash64 of the name string.

Gotchas:
- All registration must happen before `FinalizeTypes()`. After that, any registration call throws `InvalidOperationException`.
- `CompatibilityHash` changes if system types are added/removed/reordered OR if `SYSTEM_COMPATIBILITY_VERSION` is bumped. This breaks network compatibility between builds.
- External type registration is deferred and sorted (by assembly name, then type name) to ensure deterministic ordering.
