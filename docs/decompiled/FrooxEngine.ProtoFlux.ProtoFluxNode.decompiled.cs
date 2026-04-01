using System;
using System.Collections.Generic;
using Elements.Core;
using FrooxEngine.UIX;
using FrooxEngine.Undo;
using ProtoFlux.Core;

namespace FrooxEngine.ProtoFlux;

public abstract class ProtoFluxNode : Component, IProtoFluxNode, IWorker, IWorldElement, ICustomInspector, IObjectRoot, IComponent, IComponentBase, IDestroyable, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable
{
	public abstract INode NodeInstance { get; }

	public ProtoFluxNodeGroup Group { get; private set; }

	public abstract Type NodeType { get; }

	public bool IsBuilt { get; private set; }

	public virtual string NodeName => GetType().GetNiceName().BeautifyName();

	public virtual float? OverrideWidth => null;

	public virtual bool SupressLabels => false;

	public virtual bool SupressHeaderAndFooter => false;

	public virtual bool? OverrideOverviewMode => null;

	public virtual int NodeInputCount => 0;

	public virtual int NodeInputListCount => 0;

	public virtual int NodeOutputCount => 0;

	public virtual int NodeOutputListCount => 0;

	public virtual int NodeImpulseCount => 0;

	public virtual int NodeImpulseListCount => 0;

	public virtual int NodeOperationCount => 0;

	public virtual int NodeOperationListCount => 0;

	public virtual int NodeReferenceCount => 0;

	public virtual int NodeGlobalRefCount => 0;

	public virtual int NodeGlobalRefListCount => 0;

	public IEnumerable<ProtoFluxNode> ReferencedNodes
	{
		get
		{
			foreach (INodeOutput allSourceOutput in AllSourceOutputs)
			{
				yield return allSourceOutput.FindNearestParent<ProtoFluxNode>();
			}
			foreach (INodeOperation allTargetOperation in AllTargetOperations)
			{
				yield return allTargetOperation.FindNearestParent<ProtoFluxNode>();
			}
			foreach (ISyncRef nodeReference in NodeReferences)
			{
				IWorldElement target = nodeReference.Target;
				if (target != null)
				{
					yield return target.FindNearestParent<ProtoFluxNode>();
				}
			}
		}
	}

	public IEnumerable<IWorldElement> AllNodeElements
	{
		get
		{
			foreach (ISyncRef nodeInput in NodeInputs)
			{
				yield return nodeInput;
			}
			foreach (INodeOutput nodeOutput in NodeOutputs)
			{
				yield return nodeOutput;
			}
			foreach (ISyncRef nodeImpulse in NodeImpulses)
			{
				yield return nodeImpulse;
			}
			foreach (INodeOperation nodeOperation in NodeOperations)
			{
				yield return nodeOperation;
			}
			foreach (ISyncRef nodeReference in NodeReferences)
			{
				yield return nodeReference;
			}
			foreach (ISyncRef nodeGlobalRef in NodeGlobalRefs)
			{
				yield return nodeGlobalRef;
			}
			foreach (ISyncList nodeInputList in NodeInputLists)
			{
				yield return nodeInputList;
			}
			foreach (ISyncList nodeOutputList in NodeOutputLists)
			{
				yield return nodeOutputList;
			}
			foreach (ISyncList nodeImpulseList in NodeImpulseLists)
			{
				yield return nodeImpulseList;
			}
			foreach (ISyncList nodeOperationList in NodeOperationLists)
			{
				yield return nodeOperationList;
			}
			foreach (ISyncList nodeGlobalRefList in NodeGlobalRefLists)
			{
				yield return nodeGlobalRefList;
			}
		}
	}

	public IEnumerable<ISyncRef> NodeInputs
	{
		get
		{
			for (int i = 0; i < NodeInputCount; i++)
			{
				yield return GetInput(i);
			}
		}
	}

	public IEnumerable<INodeOutput> NodeOutputs
	{
		get
		{
			for (int i = 0; i < NodeOutputCount; i++)
			{
				yield return GetOutput(i);
			}
		}
	}

	public IEnumerable<ISyncRef> NodeImpulses
	{
		get
		{
			for (int i = 0; i < NodeImpulseCount; i++)
			{
				yield return GetImpulse(i);
			}
		}
	}

	public IEnumerable<INodeOperation> NodeOperations
	{
		get
		{
			for (int i = 0; i < NodeOperationCount; i++)
			{
				yield return GetOperation(i);
			}
		}
	}

	public IEnumerable<ISyncRef> NodeReferences
	{
		get
		{
			for (int i = 0; i < NodeReferenceCount; i++)
			{
				yield return GetReference(i);
			}
		}
	}

	public IEnumerable<ISyncList> NodeInputLists
	{
		get
		{
			for (int i = 0; i < NodeInputListCount; i++)
			{
				yield return GetInputList(i);
			}
		}
	}

	public IEnumerable<ISyncList> NodeOutputLists
	{
		get
		{
			for (int i = 0; i < NodeOutputListCount; i++)
			{
				yield return GetOutputList(i);
			}
		}
	}

	public IEnumerable<ISyncList> NodeImpulseLists
	{
		get
		{
			for (int i = 0; i < NodeImpulseListCount; i++)
			{
				yield return GetImpulseList(i);
			}
		}
	}

	public IEnumerable<ISyncList> NodeOperationLists
	{
		get
		{
			for (int i = 0; i < NodeOperationListCount; i++)
			{
				yield return GetOperationList(i);
			}
		}
	}

	public IEnumerable<ISyncRef> NodeGlobalRefs
	{
		get
		{
			for (int i = 0; i < NodeGlobalRefCount; i++)
			{
				yield return GetGlobalRef(i);
			}
		}
	}

	public IEnumerable<ISyncList> NodeGlobalRefLists
	{
		get
		{
			for (int i = 0; i < NodeGlobalRefListCount; i++)
			{
				yield return GetGlobalRefList(i);
			}
		}
	}

	public IEnumerable<ISyncRef> AllInputs
	{
		get
		{
			foreach (ISyncRef nodeInput in NodeInputs)
			{
				yield return nodeInput;
			}
			foreach (ISyncList nodeInputList in NodeInputLists)
			{
				foreach (object element in nodeInputList.Elements)
				{
					yield return (ISyncRef)element;
				}
			}
		}
	}

	public IEnumerable<ISyncRef> AllImpulses
	{
		get
		{
			foreach (ISyncRef nodeImpulse in NodeImpulses)
			{
				yield return nodeImpulse;
			}
			foreach (ISyncList nodeImpulseList in NodeImpulseLists)
			{
				foreach (object element in nodeImpulseList.Elements)
				{
					yield return (ISyncRef)element;
				}
			}
		}
	}

	public IEnumerable<INodeOutput> AllSourceOutputs
	{
		get
		{
			foreach (ISyncRef nodeInput in NodeInputs)
			{
				if (nodeInput.Target is INodeOutput nodeOutput)
				{
					yield return nodeOutput;
				}
			}
			foreach (ISyncList nodeInputList in NodeInputLists)
			{
				foreach (object element in nodeInputList.Elements)
				{
					if (element is ISyncRef { Target: INodeOutput target })
					{
						yield return target;
					}
				}
			}
		}
	}

	public IEnumerable<INodeOperation> AllTargetOperations
	{
		get
		{
			foreach (ISyncRef nodeImpulse in NodeImpulses)
			{
				if (nodeImpulse.Target != null)
				{
					yield return (INodeOperation)nodeImpulse.Target;
				}
			}
			foreach (ISyncList impulseList in NodeImpulseLists)
			{
				for (int i = 0; i < impulseList.Count; i++)
				{
					ISyncRef syncRef = (ISyncRef)impulseList.GetElement(i);
					if (syncRef.Target != null)
					{
						yield return (INodeOperation)syncRef.Target;
					}
				}
			}
		}
	}

	public abstract N Instantiate<N>() where N : class, INode;

	public abstract void ClearInstance();

	protected abstract void AssociateInstanceInternal(INode node);

	public virtual void BuildContentUI(ProtoFluxNodeVisual visual, UIBuilder ui)
	{
	}

	public virtual bool GenerateElement(string elementName)
	{
		return true;
	}

	public bool GetAnyConnectionChangedAndClear()
	{
		bool flag = false;
		foreach (ISyncRef nodeInput in NodeInputs)
		{
			flag |= ((SyncElement)nodeInput).GetWasChangedAndClear();
		}
		foreach (ISyncList nodeInputList in NodeInputLists)
		{
			flag |= ((SyncElement)nodeInputList).GetWasChangedAndClear();
			for (int i = 0; i < nodeInputList.Count; i++)
			{
				flag |= ((SyncElement)nodeInputList.GetElement(i)).GetWasChangedAndClear();
			}
		}
		foreach (ISyncRef nodeReference in NodeReferences)
		{
			flag |= ((SyncElement)nodeReference).GetWasChangedAndClear();
		}
		foreach (ISyncRef nodeImpulse in NodeImpulses)
		{
			flag |= ((SyncElement)nodeImpulse).GetWasChangedAndClear();
		}
		foreach (ISyncList nodeImpulseList in NodeImpulseLists)
		{
			flag |= ((SyncElement)nodeImpulseList).GetWasChangedAndClear();
			for (int j = 0; j < nodeImpulseList.Count; j++)
			{
				flag |= ((SyncElement)nodeImpulseList.GetElement(j)).GetWasChangedAndClear();
			}
		}
		foreach (ISyncRef nodeGlobalRef in NodeGlobalRefs)
		{
			flag |= ((SyncElement)nodeGlobalRef).GetWasChangedAndClear();
		}
		foreach (ISyncList nodeGlobalRefList in NodeGlobalRefLists)
		{
			flag |= ((SyncElement)nodeGlobalRefList).GetWasChangedAndClear();
			for (int k = 0; k < nodeGlobalRefList.Count; k++)
			{
				flag |= ((SyncElement)nodeGlobalRefList.GetElement(k)).GetWasChangedAndClear();
			}
		}
		return flag;
	}

	public ISyncRef GetInput(int index)
	{
		return GetInputInternal(ref index) ?? throw new ArgumentOutOfRangeException("index");
	}

	public INodeOutput GetOutput(int index)
	{
		return GetOutputInternal(ref index) ?? throw new ArgumentOutOfRangeException("index");
	}

	public ISyncRef GetImpulse(int index)
	{
		return GetImpulseInternal(ref index) ?? throw new ArgumentOutOfRangeException("index");
	}

	public INodeOperation GetOperation(int index)
	{
		return GetOperationInternal(ref index) ?? throw new ArgumentOutOfRangeException("index");
	}

	public ISyncRef GetReference(int index)
	{
		return GetReferenceInternal(ref index) ?? throw new ArgumentOutOfRangeException("index");
	}

	public ISyncList GetInputList(int index)
	{
		return GetInputListInternal(ref index) ?? throw new ArgumentOutOfRangeException("index");
	}

	public ISyncList GetOutputList(int index)
	{
		return GetOutputListInternal(ref index) ?? throw new ArgumentOutOfRangeException("index");
	}

	public ISyncList GetImpulseList(int index)
	{
		return GetImpulseListInternal(ref index) ?? throw new ArgumentOutOfRangeException("index");
	}

	public ISyncList GetOperationList(int index)
	{
		return GetOperationListInternal(ref index) ?? throw new ArgumentOutOfRangeException("index");
	}

	public ISyncRef GetGlobalRef(int index)
	{
		return GetGlobalRefInternal(ref index) ?? throw new ArgumentOutOfRangeException("index");
	}

	public ISyncList GetGlobalRefList(int index)
	{
		return GetGlobalRefListInternal(ref index) ?? throw new ArgumentOutOfRangeException("index");
	}

	protected virtual ISyncRef GetInputInternal(ref int index)
	{
		return null;
	}

	protected virtual INodeOutput GetOutputInternal(ref int index)
	{
		return null;
	}

	protected virtual ISyncRef GetImpulseInternal(ref int index)
	{
		return null;
	}

	protected virtual INodeOperation GetOperationInternal(ref int index)
	{
		return null;
	}

	protected virtual ISyncRef GetReferenceInternal(ref int index)
	{
		return null;
	}

	protected virtual ISyncList GetInputListInternal(ref int index)
	{
		return null;
	}

	protected virtual ISyncList GetOutputListInternal(ref int index)
	{
		return null;
	}

	protected virtual ISyncList GetImpulseListInternal(ref int index)
	{
		return null;
	}

	protected virtual ISyncList GetOperationListInternal(ref int index)
	{
		return null;
	}

	protected virtual ISyncRef GetGlobalRefInternal(ref int index)
	{
		return null;
	}

	protected virtual ISyncList GetGlobalRefListInternal(ref int index)
	{
		return null;
	}

	public int IndexOfOutput(INodeOutput output)
	{
		for (int i = 0; i < NodeOutputCount; i++)
		{
			if (GetOutput(i) == output)
			{
				return i;
			}
		}
		return -1;
	}

	public int IndexOfOutputList(ISyncList list)
	{
		for (int i = 0; i < NodeOutputListCount; i++)
		{
			if (GetOutputList(i) == list)
			{
				return i;
			}
		}
		return -1;
	}

	public int IndexOfOperation(INodeOperation operation)
	{
		for (int i = 0; i < NodeOperationCount; i++)
		{
			if (GetOperation(i) == operation)
			{
				return i;
			}
		}
		return -1;
	}

	public int IndexOfOperationList(ISyncList list)
	{
		for (int i = 0; i < NodeOperationListCount; i++)
		{
			if (GetOperationList(i) == list)
			{
				return i;
			}
		}
		return -1;
	}

	public bool TryConnectInput(ISyncRef input, INodeOutput output, bool allowExplicitCast, bool undoable)
	{
		IUndoable undoable2 = null;
		if (undoable)
		{
			undoable2 = base.World.BeginUndoBatch("Connect " + input.Name);
			input.CreateUndoPoint(forceNew: true);
		}
		if (input.TrySet(output))
		{
			if (undoable)
			{
				base.World.EndUndoBatch();
			}
			return true;
		}
		ProtoFluxNodeGroup protoFluxNodeGroup = Group;
		if (protoFluxNodeGroup != null && protoFluxNodeGroup.TryConnectInput(this, input, output, allowExplicitCast, undoable))
		{
			if (undoable)
			{
				base.World.EndUndoBatch();
			}
			return true;
		}
		if (undoable2 != null)
		{
			base.World.EndUndoBatch();
			undoable2.Destroy();
		}
		return false;
	}

	public bool TryConnectImpulse(ISyncRef impulse, INodeOperation operation, bool undoable)
	{
		IUndoable undoable2 = null;
		if (undoable)
		{
			undoable2 = impulse.CreateUndoPoint(forceNew: true);
		}
		if (impulse.TrySet(operation))
		{
			return true;
		}
		undoable2?.Destroy();
		return false;
	}

	public bool TryConnectReference(ISyncRef reference, ProtoFluxNode node, bool undoable)
	{
		IUndoable undoable2 = null;
		if (undoable)
		{
			undoable2 = base.World.BeginUndoBatch("Connect " + reference.Name);
			reference.CreateUndoPoint(forceNew: true);
		}
		if (reference.TrySet(node))
		{
			if (undoable)
			{
				base.World.EndUndoBatch();
			}
			return true;
		}
		ProtoFluxNodeGroup protoFluxNodeGroup = Group;
		if (protoFluxNodeGroup != null && protoFluxNodeGroup.TryConnectReference(this, reference, node, undoable))
		{
			if (undoable)
			{
				base.World.EndUndoBatch();
			}
			return true;
		}
		if (undoable2 != null)
		{
			base.World.EndUndoBatch();
			undoable2.Destroy();
		}
		return false;
	}

	public ElementRef? GetInputElementRef(ISyncRef input)
	{
		if (input.Parent == this)
		{
			for (int i = 0; i < NodeInputCount; i++)
			{
				if (GetInput(i) == input)
				{
					return new ElementRef(i);
				}
			}
		}
		else if (input.Parent is ISyncList syncList)
		{
			for (int j = 0; j < NodeInputListCount; j++)
			{
				if (GetInputList(j) != syncList)
				{
					continue;
				}
				for (int k = 0; k < syncList.Count; k++)
				{
					if (syncList.GetElement(k) == input)
					{
						return new ElementRef(j, k);
					}
				}
			}
		}
		return null;
	}

	public int GetReferenceIndex(ISyncRef reference)
	{
		for (int i = 0; i < NodeReferenceCount; i++)
		{
			if (GetReference(i) == reference)
			{
				return i;
			}
		}
		return -1;
	}

	public object GetDefaultInputValue(ISyncRef input)
	{
		ElementRef? inputElementRef = GetInputElementRef(input);
		if (!inputElementRef.HasValue)
		{
			throw new ArgumentException("Given input does not exist on this node");
		}
		NodeMetadata metadata = NodeMetadataHelper.GetMetadata(NodeType);
		if (!inputElementRef.Value.IsDynamic)
		{
			return metadata.FixedInputs[inputElementRef.Value.index].DefaultValue;
		}
		return metadata.DynamicInputs[inputElementRef.Value.listIndex].DefaultValue;
	}

	internal void MarkForRebuild()
	{
		IsBuilt = false;
		base.World.ProtoFlux.RegisterDirtyNode(this);
	}

	protected override void OnAwake()
	{
		base.OnAwake();
		MarkForRebuild();
	}

	protected override void OnDestroying()
	{
		Cleanup();
		base.OnDestroying();
	}

	protected override void OnDestroy()
	{
		if (!base.World.IsDestroyed)
		{
			Cleanup();
		}
		base.OnDestroy();
	}

	protected override void OnDispose()
	{
		if (!base.World.IsDestroyed)
		{
			Cleanup();
		}
		base.OnDispose();
	}

	private void Cleanup()
	{
		if (Group != null)
		{
			Group.UnregisterNode(this);
			UnmapOutputs();
			UnmapOperations();
		}
		Group = null;
	}

	protected override void OnStart()
	{
		base.OnStart();
		GetAnyConnectionChangedAndClear();
	}

	protected override void OnChanges()
	{
		base.OnChanges();
		if (IsBuilt && GetAnyConnectionChangedAndClear())
		{
			MarkForRebuild();
		}
	}

	internal void Rebuild(ref ProtoFluxNodeGroup currentGroup)
	{
		if (IsBuilt)
		{
			throw new InvalidOperationException("Node is already built: " + this);
		}
		IsBuilt = true;
		ProtoFluxNodeGroup protoFluxNodeGroup = Group;
		Group = null;
		foreach (ProtoFluxNode referencedNode in ReferencedNodes)
		{
			if (referencedNode.IsBuilt)
			{
				if (referencedNode.Group != null && referencedNode.Group != currentGroup)
				{
					if (currentGroup == null)
					{
						currentGroup = referencedNode.Group;
						currentGroup.MarkForRebuild();
					}
					else if (referencedNode.Group.NodeCount > currentGroup.NodeCount)
					{
						referencedNode.Group.MergeIn(currentGroup);
						currentGroup = referencedNode.Group;
						currentGroup.MarkForRebuild();
					}
					else
					{
						currentGroup.MergeIn(referencedNode.Group);
						currentGroup.MarkForRebuild();
					}
				}
			}
			else
			{
				referencedNode.Rebuild(ref currentGroup);
			}
		}
		if (currentGroup == null)
		{
			if (protoFluxNodeGroup != null && protoFluxNodeGroup.NodeCount == 1 && protoFluxNodeGroup.HasNode(this))
			{
				currentGroup = protoFluxNodeGroup;
				currentGroup.MarkForRebuild();
			}
			else
			{
				currentGroup = base.World.ProtoFlux.AllocateGroup();
			}
		}
		if (Group == null && currentGroup != protoFluxNodeGroup)
		{
			if (protoFluxNodeGroup != null && !protoFluxNodeGroup.IsMarkedForRemoval)
			{
				protoFluxNodeGroup.UnregisterNode(this);
				protoFluxNodeGroup.MarkNodesForRebuild();
			}
			if (NodeInstance != null)
			{
				ClearInstance();
			}
			currentGroup.RegisterNode(this);
		}
		Group = currentGroup;
	}

	internal void MoveToGroup(ProtoFluxNodeGroup group)
	{
		Group = group;
		ClearInstance();
		UnmapOutputs();
		UnmapOperations();
	}

	internal void ClearGroupAndInstance()
	{
		Group?.UnregisterExtrenallyRemovedNode(this);
		Group = null;
		ClearInstance();
		UnmapOutputs();
		UnmapOperations();
	}

	internal void MapOutputs()
	{
		for (int i = 0; i < NodeOutputCount; i++)
		{
			GetOutput(i).MappedOutput = NodeInstance.GetOutput(i);
		}
		for (int j = 0; j < NodeOutputListCount; j++)
		{
			ISyncList outputList = GetOutputList(j);
			IOutputList outputList2 = NodeInstance.GetOutputList(j);
			for (int k = 0; k < outputList.Count; k++)
			{
				INodeOutput nodeOutput = (INodeOutput)outputList.GetElement(k);
				if (outputList2.Count == k)
				{
					nodeOutput.MappedOutput = outputList2.AddOutput();
				}
				else
				{
					nodeOutput.MappedOutput = outputList2.GetOutput(k);
				}
			}
			while (outputList2.Count > outputList.Count)
			{
				outputList2.RemoveOutput();
			}
		}
	}

	internal void MapOperations()
	{
		for (int i = 0; i < NodeOperationCount; i++)
		{
			GetOperation(i).MappedOperation = NodeInstance.GetOperation(i);
		}
		for (int j = 0; j < NodeOperationListCount; j++)
		{
			ISyncList operationList = GetOperationList(j);
			IOperationList operationList2 = NodeInstance.GetOperationList(j);
			for (int k = 0; k < operationList.Count; k++)
			{
				INodeOperation nodeOperation = (INodeOperation)operationList.GetElement(k);
				if (operationList2.Count == k)
				{
					nodeOperation.MappedOperation = operationList2.AddOperation();
				}
				else
				{
					nodeOperation.MappedOperation = operationList2.GetOperation(k);
				}
			}
			while (operationList2.Count > operationList.Count)
			{
				operationList2.RemoveOperation();
			}
		}
	}

	internal void UnmapOutputs()
	{
		for (int i = 0; i < NodeOutputCount; i++)
		{
			GetOutput(i).MappedOutput = null;
		}
		for (int j = 0; j < NodeOutputListCount; j++)
		{
			ISyncList outputList = GetOutputList(j);
			for (int k = 0; k < outputList.Count; k++)
			{
				((INodeOutput)outputList.GetElement(k)).MappedOutput = null;
			}
		}
	}

	internal void UnmapOperations()
	{
		for (int i = 0; i < NodeOperationCount; i++)
		{
			GetOperation(i).MappedOperation = null;
		}
		for (int j = 0; j < NodeOperationListCount; j++)
		{
			ISyncList operationList = GetOperationList(j);
			for (int k = 0; k < operationList.Count; k++)
			{
				((INodeOperation)operationList.GetElement(k)).MappedOperation = null;
			}
		}
	}

	internal void MapInputs()
	{
		for (int i = 0; i < NodeInputCount; i++)
		{
			INodeOutput nodeOutput = GetInput(i).Target as INodeOutput;
			NodeInstance.SetInputSource(i, nodeOutput?.MappedOutput);
		}
		for (int j = 0; j < NodeInputListCount; j++)
		{
			ISyncList inputList = GetInputList(j);
			IInputList inputList2 = NodeInstance.GetInputList(j);
			for (int k = 0; k < inputList.Count; k++)
			{
				INodeOutput nodeOutput2 = (inputList.GetElement(k) as ISyncRef).Target as INodeOutput;
				if (inputList2.Count == k)
				{
					inputList2.AddInput(nodeOutput2?.MappedOutput);
				}
				else
				{
					inputList2.SetInputSource(k, nodeOutput2?.MappedOutput);
				}
			}
			while (inputList2.Count > inputList.Count)
			{
				inputList2.RemoveInput();
			}
		}
	}

	internal void MapImpulses()
	{
		for (int i = 0; i < NodeImpulseCount; i++)
		{
			INodeOperation nodeOperation = GetImpulse(i).Target as INodeOperation;
			NodeInstance.SetImpulseTarget(i, nodeOperation?.MappedOperation);
		}
		for (int j = 0; j < NodeImpulseListCount; j++)
		{
			ISyncList impulseList = GetImpulseList(j);
			IImpulseList impulseList2 = NodeInstance.GetImpulseList(j);
			for (int k = 0; k < impulseList.Count; k++)
			{
				IOperation target = (((ISyncRef)impulseList.GetElement(k)).Target as INodeOperation)?.MappedOperation;
				if (impulseList2.Count == k)
				{
					impulseList2.AddImpulse(target);
				}
				else
				{
					impulseList2.SetImpulseTarget(k, target);
				}
			}
			while (impulseList2.Count > impulseList.Count)
			{
				impulseList2.RemoveImpulse();
			}
		}
	}

	internal void MapReferences()
	{
		for (int i = 0; i < NodeReferenceCount; i++)
		{
			ProtoFluxNode protoFluxNode = GetReference(i).Target as ProtoFluxNode;
			NodeInstance.SetReferenceTarget(i, protoFluxNode?.NodeInstance);
		}
	}

	internal void MapGlobalRefs(ProtoFluxBuildContext context)
	{
		for (int i = 0; i < NodeGlobalRefCount; i++)
		{
			IGlobalValueProxy proxy = GetGlobalRef(i).Target as IGlobalValueProxy;
			NodeInstance.SetGlobalRefBinding(i, context.MapGlobal(proxy));
		}
	}

	internal void AssociateInstance(ProtoFluxNodeGroup group, INode node)
	{
		if (NodeInstance != null)
		{
			throw new InvalidOperationException("Node already has an instance");
		}
		if (Group != null)
		{
			throw new InvalidOperationException("Node already belongs to a group");
		}
		if (group == null)
		{
			throw new ArgumentNullException("group");
		}
		if (node == null)
		{
			throw new ArgumentNullException("node");
		}
		AssociateInstanceInternal(node);
		Group = group;
		Group.RegisterExternallyInstantiatedNode(this);
		for (int i = 0; i < node.DynamicInputCount; i++)
		{
			GetInputList(i).EnsureExactElementCount(node.GetInputList(i).Count);
		}
		for (int j = 0; j < node.DynamicOutputCount; j++)
		{
			GetOutputList(j).EnsureExactElementCount(node.GetOutputList(j).Count);
		}
		for (int k = 0; k < node.DynamicImpulseCount; k++)
		{
			GetImpulseList(k).EnsureExactElementCount(node.GetImpulseList(k).Count);
		}
		for (int l = 0; l < node.DynamicOperationCount; l++)
		{
			GetOperationList(l).EnsureExactElementCount(node.GetOperationList(l).Count);
		}
	}

	internal void ReverseMapElements(Dictionary<INode, ProtoFluxNode> nodeMapping, bool undoable)
	{
		if (NodeInstance == null)
		{
			throw new InvalidOperationException("Node proxy needs to have node instance for the reverse mapping");
		}
		for (int i = 0; i < NodeInstance.FixedInputCount; i++)
		{
			IOutput inputSource = NodeInstance.GetInputSource(i);
			if (inputSource != null)
			{
				int index = inputSource.FindLinearOutputIndex();
				ProtoFluxNode protoFluxNode = nodeMapping[inputSource.OwnerNode];
				ISyncRef input = GetInput(i);
				if (undoable)
				{
					input.CreateUndoPoint(forceNew: true);
				}
				input.Target = protoFluxNode.GetOutput(index);
			}
		}
		for (int j = 0; j < NodeInstance.DynamicInputCount; j++)
		{
			IInputList inputList = NodeInstance.GetInputList(j);
			if (inputList.Count == 0)
			{
				continue;
			}
			ISyncList inputList2 = GetInputList(j);
			for (int k = 0; k < inputList.Count; k++)
			{
				IOutput inputSource2 = inputList.GetInputSource(k);
				if (inputSource2 != null)
				{
					int index2 = inputSource2.FindLinearOutputIndex();
					ProtoFluxNode protoFluxNode2 = nodeMapping[inputSource2.OwnerNode];
					ISyncRef syncRef = (ISyncRef)inputList2.GetElement(k);
					if (undoable)
					{
						syncRef.CreateUndoPoint(forceNew: true);
					}
					syncRef.Target = protoFluxNode2.GetOutput(index2);
				}
			}
		}
		for (int l = 0; l < NodeInstance.FixedImpulseCount; l++)
		{
			IOperation impulseTarget = NodeInstance.GetImpulseTarget(l);
			if (impulseTarget != null)
			{
				int index3 = impulseTarget.FindLinearOperationIndex();
				ProtoFluxNode protoFluxNode3 = nodeMapping[impulseTarget.OwnerNode];
				ISyncRef impulse = GetImpulse(l);
				if (undoable)
				{
					impulse.CreateUndoPoint(forceNew: true);
				}
				impulse.Target = protoFluxNode3.GetOperation(index3);
			}
		}
		for (int m = 0; m < NodeInstance.DynamicImpulseCount; m++)
		{
			IImpulseList impulseList = NodeInstance.GetImpulseList(m);
			if (impulseList.Count == 0)
			{
				continue;
			}
			ISyncList impulseList2 = GetImpulseList(m);
			for (int n = 0; n < impulseList.Count; n++)
			{
				IOperation impulseTarget2 = impulseList.GetImpulseTarget(n);
				if (impulseTarget2 != null)
				{
					int index4 = impulseTarget2.FindLinearOperationIndex();
					ProtoFluxNode protoFluxNode4 = nodeMapping[impulseTarget2.OwnerNode];
					ISyncRef syncRef2 = (ISyncRef)impulseList2.GetElement(n);
					if (undoable)
					{
						syncRef2.CreateUndoPoint(forceNew: true);
					}
					syncRef2.Target = protoFluxNode4.GetOperation(index4);
				}
			}
		}
		for (int num = 0; num < NodeInstance.FixedReferenceCount; num++)
		{
			INode referenceTarget = NodeInstance.GetReferenceTarget(num);
			if (referenceTarget != null)
			{
				ProtoFluxNode target = nodeMapping[referenceTarget];
				ISyncRef reference = GetReference(num);
				if (undoable)
				{
					reference.CreateUndoPoint(forceNew: true);
				}
				reference.Target = target;
			}
		}
		for (int num2 = 0; num2 < NodeInstance.FixedGlobalRefCount; num2++)
		{
			Global globalRefBinding = NodeInstance.GetGlobalRefBinding(num2);
			if (globalRefBinding != null)
			{
				ISyncRef globalRef = GetGlobalRef(num2);
				if (undoable)
				{
					globalRef.CreateUndoPoint(forceNew: true);
				}
				globalRef.Target = Group.GetGlobal(globalRefBinding.Index) as IWorldElement;
			}
		}
	}

	public virtual void BuildInspectorUI(UIBuilder ui)
	{
		WorkerInspector.BuildInspectorUI(this, ui);
		ui.Button((LocaleString)"Dump ProtoFlux Node Structure to clipboard", OnDumpStructure);
		Text text = ui.Text((LocaleString)"");
		ProtoFluxNodeDebugInfo protoFluxNodeDebugInfo = text.Slot.AttachComponent<ProtoFluxNodeDebugInfo>();
		protoFluxNodeDebugInfo.Node.Target = this;
		text.Content.DriveFormatted("<color=yellow>Built: {0} ({1}; {2}), ContinuouslyChanging: {3}, Group: <color=orange>{4}</color> (Valid: {5}, Nodes: {6}, ContinuousChanges: {7}, Updates: {8})</color>", protoFluxNodeDebugInfo.IsBuilt, protoFluxNodeDebugInfo.IndexInGroup, protoFluxNodeDebugInfo.AllocationIndex, protoFluxNodeDebugInfo.NodeContinuouslyChanging, protoFluxNodeDebugInfo.GroupName, protoFluxNodeDebugInfo.GroupIsValid, protoFluxNodeDebugInfo.GroupNodeCount, protoFluxNodeDebugInfo.GroupRegisteredForContinuousChanges, protoFluxNodeDebugInfo.GroupRegisteredForUpdates);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnDumpStructure(IButton button, ButtonEventData eventData)
	{
		base.InputInterface.Clipboard.SetText(Group.DebugNodeStructure());
		button.LabelText = "Copied!";
	}

	public void EnsureElementsInDynamicLists()
	{
		foreach (ISyncList nodeInputList in NodeInputLists)
		{
			nodeInputList.AddElement();
			nodeInputList.AddElement();
		}
		foreach (ISyncList nodeOutputList in NodeOutputLists)
		{
			nodeOutputList.AddElement();
			nodeOutputList.AddElement();
		}
		foreach (ISyncList nodeImpulseList in NodeImpulseLists)
		{
			nodeImpulseList.AddElement();
			nodeImpulseList.AddElement();
		}
		foreach (ISyncList nodeOperationList in NodeOperationLists)
		{
			nodeOperationList.AddElement();
			nodeOperationList.AddElement();
		}
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
	}
}
