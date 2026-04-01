using System;
using System.IO;
using Elements.Core;
using FrooxEngine.ProtoFlux.Visuals;
using FrooxEngine.UIX;
using ProtoFlux.Core;

namespace FrooxEngine.ProtoFlux;

public class ProtoFluxNodeVisual : Component
{
	public const float DEFAULT_SCALE = 0.00093750004f;

	public const float DEFAULT_WIDTH = 128f;

	public const string SLOT_NAME = "<NODE_UI>";

	public const float NODE_SCALE = 1.25f;

	public const string CONNECT_POINT_NAME = "<WIRE_POINT>";

	public const float ELEMENT_HEIGHT = 32f;

	public const float FLOW_ELEMENT_HEIGHT = 32f;

	public const float CONNECTOR_WIDTH = 16f;

	public const float LABEL_HEIGHT = 24f;

	public const float FOOTER_HEIGHT = 16f;

	public const float LINE_VERTICAL_OFFSET = 0.1f;

	public const float LINE_HORIZONTAL_OFFSET = 30.5f;

	public const float LINE_WIDTH = 3f;

	public const float SPACING = 2f;

	public const float COLOR_BOOST = 1.5f;

	public readonly RelayRef<ProtoFluxNode> Node;

	[NonPersistent]
	public readonly Sync<bool> IsSelected;

	[NonPersistent]
	public readonly Sync<bool> IsHighlighted;

	protected readonly SyncRef<HoverArea> _nodeHoverArea;

	protected readonly SyncRef<Image> _bgImage;

	protected readonly SyncRef<Slot> _inputsRoot;

	protected readonly SyncRef<Slot> _outputsRoot;

	protected readonly SyncRef<Slot> _referencesRoot;

	protected readonly FieldDrive<bool> _overviewVisual;

	protected readonly FieldDrive<colorX> _overviewBg;

	protected readonly FieldDrive<bool> _labelBg;

	protected readonly FieldDrive<bool> _labelText;

	private UIBuilder _uiBuilder;

	public bool IsNodeValid => Node.Target?.Group?.IsValid == true;

	public HoverArea NodeHoverArea => _nodeHoverArea.Target;

	public Type NodeType => Node.Target?.NodeType;

	public NodeMetadata NodeMetadata
	{
		get
		{
			Type nodeType = NodeType;
			if (nodeType == null)
			{
				return null;
			}
			return NodeMetadataHelper.GetMetadata(nodeType);
		}
	}

	public UIBuilder LocalUIBuilder
	{
		get
		{
			if (_uiBuilder == null)
			{
				_uiBuilder = new UIBuilder(base.Slot);
				RadiantUI_Constants.SetupEditorStyle(_uiBuilder);
			}
			return _uiBuilder;
		}
	}

	public void GenerateVisual(ProtoFluxNode node)
	{
		if (Node.Target != null)
		{
			throw new InvalidOperationException("Node is already assigned!");
		}
		Node.Target = node;
		base.Slot.AttachComponent<Canvas>().Size.Value = new float2(node.OverrideWidth ?? 128f);
		Grabbable grabbable = base.Slot.Parent.AttachComponent<Grabbable>();
		grabbable.Scalable.Value = true;
		this.DestroyWhenDestroyed(grabbable);
		BuildUI(LocalUIBuilder, node);
	}

	public ProtoFluxInputProxy GetFixedInputProxy(string name)
	{
		return _inputsRoot.Target.FindChild(name)?.GetComponentInChildren<ProtoFluxInputProxy>();
	}

	public ProtoFluxOutputProxy GetFixedOutputProxy(string name)
	{
		return _outputsRoot.Target.FindChild(name)?.GetComponentInChildren<ProtoFluxOutputProxy>();
	}

	public ProtoFluxImpulseProxy GetFixedImpulseProxy(string name)
	{
		return _outputsRoot.Target.FindChild(name)?.GetComponentInChildren<ProtoFluxImpulseProxy>();
	}

	public ProtoFluxOperationProxy GetFixedOperationProxy(string name)
	{
		return _inputsRoot.Target.FindChild(name)?.GetComponentInChildren<ProtoFluxOperationProxy>();
	}

	public ProtoFluxInputProxy GetDynamicInputProxy(string name, int index)
	{
		return _inputsRoot.Target.FindChild(name)?.FindChild(index.ToString())?.GetComponentInChildren<ProtoFluxInputProxy>();
	}

	public ProtoFluxOutputProxy GetDynamicOutputProxy(string name, int index)
	{
		return _outputsRoot.Target.FindChild(name)?.FindChild(index.ToString())?.GetComponentInChildren<ProtoFluxOutputProxy>();
	}

	public ProtoFluxOperationProxy GetDynamicOperationProxy(string name, int index)
	{
		return _inputsRoot.Target.FindChild(name)?.FindChild(index.ToString())?.GetComponentInChildren<ProtoFluxOperationProxy>();
	}

	public ProtoFluxImpulseProxy GetDynamicImpulseProxy(string name, int index)
	{
		return _inputsRoot.Target.FindChild(name)?.FindChild(index.ToString())?.GetComponentInChildren<ProtoFluxImpulseProxy>();
	}

	private void BuildUI(UIBuilder ui, ProtoFluxNode node)
	{
		NodeMetadata metadata = NodeMetadataHelper.GetMetadata(node.NodeType);
		ui.LayoutTarget = base.Slot;
		ui.VerticalLayout();
		ui.FitContent(SizeFit.Disabled, SizeFit.MinSize);
		_bgImage.Target = ui.Image(RadiantUI_Constants.BG_COLOR, zwrite: true);
		ui.IgnoreLayout();
		_nodeHoverArea.Target = ui.Current.AttachComponent<HoverArea>();
		bool flag = !node.SupressHeaderAndFooter;
		string nodeName = Node.Target.NodeName;
		bool? overrideOverviewMode = node.OverrideOverviewMode;
		Image image = null;
		Slot slot = null;
		if (flag)
		{
			ui.Style.MinHeight = 24f;
			if (overrideOverviewMode != true)
			{
				image = ui.Panel(RadiantUI_Constants.HEADER);
				slot = ui.Text((LocaleString)nodeName).Slot;
				ui.NestOut();
			}
			else
			{
				ui.Empty();
			}
		}
		ui.Style.MinHeight = 32f;
		ui.Style.SupressLayoutElement = true;
		ui.OverlappingLayout(0f, Alignment.TopCenter).ForceExpandHeight.Value = false;
		_inputsRoot.Target = ui.VerticalLayout().Slot;
		_inputsRoot.Target.Name = "Inputs & Operations";
		ui.Style.SupressLayoutElement = false;
		GenerateOperations(ui, node, metadata);
		GenerateInputs(ui, node, metadata);
		ui.NestOut();
		ui.Style.SupressLayoutElement = true;
		_outputsRoot.Target = ui.VerticalLayout().Slot;
		_outputsRoot.Target.Name = "Outputs & Impulses";
		ui.Style.SupressLayoutElement = false;
		GenerateImpulses(ui, node, metadata);
		GenerateOutputs(ui, node, metadata);
		ui.NestOut();
		ui.Style.SupressLayoutElement = false;
		node.BuildContentUI(this, ui);
		if (overrideOverviewMode ?? true)
		{
			ui.Style.SupressLayoutElement = true;
			Image image2 = ui.Panel(RadiantUI_Constants.BG_COLOR);
			image2.Slot.Name = "Overview";
			ui.IgnoreLayout();
			ui.Text((LocaleString)nodeName);
			ui.NestOut();
			image2.RectTransform.AddFixedPadding(flag ? (-24f) : 0f, 16f, 0f, 16f);
			_overviewBg.Target = image2.Tint;
			if (!overrideOverviewMode.HasValue)
			{
				_overviewVisual.Target = image2.Slot.ActiveSelf_Field;
				if (image != null)
				{
					_labelBg.Target = image.EnabledField;
				}
				if (slot != null)
				{
					_labelText.Target = slot.ActiveSelf_Field;
				}
			}
		}
		ui.NestOut();
		ui.Style.SupressLayoutElement = true;
		_referencesRoot.Target = ui.VerticalLayout().Slot;
		_referencesRoot.Target.Name = "References";
		ui.Style.SupressLayoutElement = false;
		GenerateReferences(ui, node, metadata);
		GenerateGlobalRefs(ui, node, metadata);
		ui.NestOut();
		if (flag)
		{
			ui.Style.MinHeight = 16f;
			string workerCategoryPath = node.WorkerCategoryPath;
			if (workerCategoryPath != null)
			{
				ui.Text((LocaleString)Path.GetFileName(workerCategoryPath)).Color.Value = colorX.DarkGray;
			}
		}
	}

	private void GenerateInputs(UIBuilder ui, ProtoFluxNode node, NodeMetadata metadata)
	{
		for (int i = 0; i < metadata.FixedInputCount; i++)
		{
			InputMetadata inputMetadata = metadata.FixedInputs[i];
			if (node.GenerateElement(inputMetadata.Name))
			{
				GenerateInputElement(ui, node.GetInput(i), inputMetadata.Name, inputMetadata.InputType);
			}
		}
		for (int j = 0; j < metadata.DynamicInputCount; j++)
		{
			InputListMetadata inputList = metadata.DynamicInputs[j];
			if (node.GenerateElement(inputList.Name))
			{
				GenerateDynamicElement(ui, node, node.GetInputList(j), inputList.Name, isOutput: false, delegate(ProtoFluxInputListManager m)
				{
					m.InputType.Value = inputList.TypeConstraint;
				});
			}
		}
	}

	private void GenerateOutputs(UIBuilder ui, ProtoFluxNode node, NodeMetadata metadata)
	{
		for (int i = 0; i < metadata.FixedOutputCount; i++)
		{
			OutputMetadata outputMetadata = metadata.FixedOutputs[i];
			if (node.GenerateElement(outputMetadata.Name))
			{
				GenerateOutputElement(ui, node.GetOutput(i), outputMetadata.Name, outputMetadata.OutputType);
			}
		}
		for (int j = 0; j < metadata.DynamicOutputCount; j++)
		{
			OutputListMetadata outputList = metadata.DynamicOutputs[j];
			if (node.GenerateElement(outputList.Name))
			{
				GenerateDynamicElement(ui, node, node.GetOutputList(j), outputList.Name, isOutput: true, delegate(ProtoFluxOutputListManager m)
				{
					m.OutputType.Value = outputList.TypeConstraint;
				});
			}
		}
	}

	private void GenerateOperations(UIBuilder ui, ProtoFluxNode node, NodeMetadata metadata)
	{
		for (int i = 0; i < metadata.FixedOperationCount; i++)
		{
			OperationMetadata operationMetadata = metadata.FixedOperations[i];
			if (node.GenerateElement(operationMetadata.Name))
			{
				GenerateOperationElement(ui, node.GetOperation(i), operationMetadata.Name, operationMetadata.IsAsync);
			}
		}
		for (int j = 0; j < metadata.DynamicOperationCount; j++)
		{
			OperationListMetadata operationList = metadata.DynamicOperations[j];
			GenerateDynamicElement(ui, node, node.GetOperationList(j), operationList.Name, isOutput: false, delegate(ProtoFluxOperationListManager m)
			{
				m.SupportsAsync.Value = operationList.SupportsAsync;
			});
		}
	}

	private void GenerateImpulses(UIBuilder ui, ProtoFluxNode node, NodeMetadata metadata)
	{
		for (int i = 0; i < metadata.FixedImpulseCount; i++)
		{
			ImpulseMetadata impulseMetadata = metadata.FixedImpulses[i];
			if (node.GenerateElement(impulseMetadata.Name))
			{
				GenerateImpulseElement(ui, node.GetImpulse(i), impulseMetadata.Name, impulseMetadata.Type);
			}
		}
		for (int j = 0; j < metadata.DynamicImpulseCount; j++)
		{
			ImpulseListMetadata impulseList = metadata.DynamicImpulses[j];
			if (node.GenerateElement(impulseList.Name))
			{
				GenerateDynamicElement(ui, node, node.GetImpulseList(j), impulseList.Name, isOutput: true, delegate(ProtoFluxImpulseListManager m)
				{
					m.ImpulseType.Value = impulseList.Type;
				});
			}
		}
	}

	private void GenerateReferences(UIBuilder ui, ProtoFluxNode node, NodeMetadata metadata)
	{
		for (int i = 0; i < metadata.FixedReferenceCount; i++)
		{
			ReferenceMetadata referenceMetadata = metadata.FixedReferences[i];
			if (node.GenerateElement(referenceMetadata.Name))
			{
				GenerateReferenceElement(ui, node.GetReference(i), referenceMetadata.Name, referenceMetadata.ReferenceType);
			}
		}
	}

	private void GenerateGlobalRefs(UIBuilder ui, ProtoFluxNode node, NodeMetadata metadata)
	{
		for (int i = 0; i < metadata.FixedGlobalRefCount; i++)
		{
			GlobalRefMetadata globalRefMetadata = metadata.FixedGlobalRefs[i];
			if (node.GenerateElement(globalRefMetadata.Name))
			{
				GenerateGlobalRefElement(ui, globalRefMetadata.Name, globalRefMetadata.ValueType.GetTypeColor().MulRGB(1.5f), globalRefMetadata.ValueType, node.GetGlobalRef(i));
			}
		}
	}

	internal Slot GenerateInputElement(UIBuilder ui, ISyncRef input, string name, Type elementType, int? listIndex = null)
	{
		var (result, protoFluxInputProxy) = GenerateFixedElement<ProtoFluxInputProxy>(ui, name, elementType.GetTypeColor().MulRGB(1.5f), elementType.GetTypeConnectorSprite(base.World), isOutput: false, flipSprite: false, listIndex);
		protoFluxInputProxy.NodeInput.Target = input;
		protoFluxInputProxy.InputType.Value = elementType;
		return result;
	}

	internal Slot GenerateOutputElement(UIBuilder ui, INodeOutput output, string name, Type elementType, int? listIndex = null)
	{
		var (result, protoFluxOutputProxy) = GenerateFixedElement<ProtoFluxOutputProxy>(ui, name, elementType.GetTypeColor().MulRGB(1.5f), elementType.GetTypeConnectorSprite(base.World), isOutput: true, flipSprite: true, listIndex);
		protoFluxOutputProxy.NodeOutput.Target = output;
		protoFluxOutputProxy.OutputType.Value = elementType;
		return result;
	}

	internal Slot GenerateImpulseElement(UIBuilder ui, ISyncRef input, string name, ImpulseType type, int? listIndex = null)
	{
		var (result, protoFluxImpulseProxy) = GenerateFixedElement<ProtoFluxImpulseProxy>(ui, name, type.GetImpulseColor().MulRGB(1.5f), base.World.GetFlowConnectorSprite(), isOutput: true, flipSprite: false, listIndex);
		protoFluxImpulseProxy.NodeImpulse.Target = input;
		protoFluxImpulseProxy.ImpulseType.Value = type;
		return result;
	}

	internal Slot GenerateOperationElement(UIBuilder ui, INodeOperation operation, string name, bool isAsync, int? listIndex = null)
	{
		var (result, protoFluxOperationProxy) = GenerateFixedElement<ProtoFluxOperationProxy>(ui, name, DatatypeColorHelper.GetOperationColor(isAsync).MulRGB(1.5f), base.World.GetFlowConnectorSprite(), isOutput: false, flipSprite: false, listIndex);
		protoFluxOperationProxy.NodeOperation.Target = operation;
		protoFluxOperationProxy.IsAsync.Value = isAsync;
		return result;
	}

	internal void GenerateReferenceElement(UIBuilder ui, ISyncRef reference, string name, Type referenceType, int? listIndex = null)
	{
		GenerateRefElement(ui, name, referenceType.GetTypeColor().MulRGB(1.5f), delegate(ProtoFluxReferenceProxy proxy, Text label, Slot connectPoint)
		{
			proxy.Node.Target = Node.Target;
			proxy.NodeReference.Target = reference;
			proxy.ValueType.Value = referenceType;
			proxy.ConnectPoint.Target = connectPoint;
			proxy.BuildUI(label, ui);
		});
	}

	internal (Slot slot, P proxy) GenerateFixedElement<P>(UIBuilder ui, string name, in colorX color, IAssetProvider<Sprite> sprite, bool isOutput, bool flipSprite, int? listIndex = null) where P : ProtoFluxElementProxy, new()
	{
		RectTransform rectTransform = ui.Panel();
		rectTransform.Slot.Name = name ?? listIndex?.ToString() ?? "Element";
		Image image = ui.Image(sprite, in color);
		image.Material.Target = base.World.GetDefaultOpaqueDualsidedUI_Unlit();
		image.Slot.Name = "Connector";
		if (flipSprite)
		{
			image.FlipHorizontally.Value = true;
		}
		if (isOutput)
		{
			image.RectTransform.SetFixedHorizontal(-16f, 0f, 1f);
		}
		else
		{
			image.RectTransform.SetFixedHorizontal(0f, 16f, 0f);
		}
		Slot slot = image.Slot.AddSlot("<WIRE_POINT>");
		RectTransform rectTransform2 = slot.AttachComponent<RectTransform>();
		rectTransform2.AnchorMin.Value = new float2(isOutput ? 1f : 0f, 0.5f);
		rectTransform2.AnchorMax.Value = new float2(isOutput ? 1f : 0f, 0.5f);
		slot.AttachComponent<RectSlotDriver>();
		P val = image.Slot.AttachComponent<P>();
		ProtoFluxNode target = Node.Target;
		val.Node.Target = target;
		val.ElementName.Value = name;
		val.IsDynamic.Value = listIndex.HasValue;
		val.Index.Value = listIndex.GetValueOrDefault();
		val.ConnectPoint.Target = rectTransform2.Slot;
		if (!listIndex.HasValue && !target.SupressLabels)
		{
			Image image2 = ui.Panel(color.SetA(0.3f));
			if (isOutput)
			{
				image2.RectTransform.AnchorMax.Value = new float2(1f, 0.5f);
				image2.RectTransform.OffsetMin.Value = new float2(32f);
				image2.RectTransform.OffsetMax.Value = new float2(-16f);
			}
			else
			{
				image2.RectTransform.AnchorMin.Value = new float2(0f, 0.5f);
				image2.RectTransform.OffsetMin.Value = new float2(16f);
				image2.RectTransform.OffsetMax.Value = new float2(-32f);
			}
			image2.RectTransform.AddFixedVerticalPadding(2f);
			ui.Text((LocaleString)name, bestFit: true, isOutput ? Alignment.BottomRight : Alignment.TopLeft).RectTransform.AddFixedPadding(2f);
			ui.NestOut();
		}
		ui.NestOut();
		return (slot: rectTransform.Slot, proxy: val);
	}

	internal void GenerateRefElement<P>(UIBuilder ui, string name, in colorX color, Action<P, Text, Slot> proxySetup) where P : ProtoFluxRefProxy, new()
	{
		ui.Panel();
		ui.VerticalHeader(4f, out RectTransform header, out RectTransform content);
		ui.ForceNext = header;
		Image image = ui.Image(in color);
		image.Material.Target = base.World.GetDefaultOpaqueDualsidedUI_Unlit();
		Slot slot = image.Slot.AddSlot("<WIRE_POINT>");
		RectTransform rectTransform = slot.AttachComponent<RectTransform>();
		rectTransform.AnchorMin.Value = new float2(0f, 0.5f);
		rectTransform.AnchorMax.Value = new float2(0f, 0.5f);
		slot.AttachComponent<RectSlotDriver>();
		ui.NestInto(content);
		ui.SplitVertically(0.5f, out RectTransform top, out RectTransform bottom);
		ui.ForceNext = top;
		Text arg = ui.Text((LocaleString)name);
		ui.NestInto(bottom);
		P arg2 = ui.Root.AttachComponent<P>();
		proxySetup(arg2, arg, slot);
		ui.NestOut();
		ui.NestOut();
		ui.NestOut();
	}

	internal void GenerateGlobalRefElement(UIBuilder ui, string name, in colorX color, Type referenceType, ISyncRef globalRef)
	{
		GenerateRefElement(ui, name, in color, delegate(ProtoFluxGlobalRefProxy refProxy, Text label, Slot connectPoint)
		{
			refProxy.Node.Target = Node.Target;
			refProxy.ValueType.Value = referenceType;
			refProxy.BuildUI(label, ui, globalRef);
		});
	}

	private T GenerateDynamicElement<T>(UIBuilder ui, ProtoFluxNode node, ISyncList list, string name, bool isOutput, Action<T> postprocess) where T : ProtoFluxDynamicElementManager, new()
	{
		ui.Style.SupressLayoutElement = true;
		VerticalLayout verticalLayout = ui.VerticalLayout(0f, 0f, Alignment.TopLeft);
		verticalLayout.ForceExpandHeight.Value = false;
		verticalLayout.Slot.Name = name;
		LayoutElement layoutElement = ui.Root.AttachComponent<LayoutElement>();
		layoutElement.MinHeight.Value = 64f;
		layoutElement.Priority.Value = 0;
		T val = ui.Root.AttachComponent<T>();
		val.Visual.Target = this;
		val.List.Target = list;
		postprocess(val);
		if (!node.SupressLabels)
		{
			Text text = ui.Text((LocaleString)name, bestFit: true, isOutput ? Alignment.BottomRight : Alignment.TopLeft);
			ui.IgnoreLayout();
			text.Slot.OrderOffset = 32767L;
			if (isOutput)
			{
				text.RectTransform.OffsetMin.Value = new float2(16f, -32f);
				text.RectTransform.OffsetMax.Value = new float2(-16f, -16f);
			}
			else
			{
				text.RectTransform.OffsetMin.Value = new float2(16f, -16f);
				text.RectTransform.OffsetMax.Value = new float2(-16f);
			}
			text.RectTransform.AnchorMin.Value = new float2(0f, 1f);
			text.RectTransform.AnchorMax.Value = new float2(1f, 1f);
			text.RectTransform.AddFixedPadding(1f);
		}
		Image image = ui.Image(colorX.White.SetA(0.25f));
		ui.IgnoreLayout();
		image.Slot.OrderOffset = 32768L;
		if (isOutput)
		{
			image.RectTransform.OffsetMin.Value = new float2(-30.5f, 19.2f);
			image.RectTransform.OffsetMax.Value = new float2(-27.5f, -19.2f);
			image.RectTransform.AnchorMin.Value = new float2(1f);
			image.RectTransform.AnchorMax.Value = new float2(1f, 1f);
		}
		else
		{
			image.RectTransform.OffsetMin.Value = new float2(30.5f, 19.2f);
			image.RectTransform.OffsetMax.Value = new float2(33.5f, -19.2f);
			image.RectTransform.AnchorMin.Value = new float2(0f, 0f);
			image.RectTransform.AnchorMax.Value = new float2(0f, 1f);
		}
		RectTransform rectTransform = ui.Panel();
		ui.IgnoreLayout();
		rectTransform.Slot.OrderOffset = 32769L;
		rectTransform.OffsetMin.Value = new float2(16f);
		rectTransform.OffsetMax.Value = new float2(-16f, 16f);
		if (isOutput)
		{
			rectTransform.AnchorMin.Value = new float2(0.5f);
			rectTransform.AnchorMax.Value = new float2(1f);
		}
		else
		{
			rectTransform.AnchorMin.Value = new float2(0f, 0f);
			rectTransform.AnchorMax.Value = new float2(0.5f);
		}
		ui.HorizontalLayout(2f);
		ui.Style.SupressLayoutElement = false;
		Button button = ui.Button((LocaleString)"+", val.AddElement);
		Button button2 = ui.Button((LocaleString)"-", val.RemoveElement);
		val.AddButtonEnabled.Target = button.EnabledField;
		val.RemoveButtonEnabled.Target = button2.EnabledField;
		val.GenerateList(this, ui, list);
		ui.NestOut();
		ui.NestOut();
		ui.NestOut();
		return val;
	}

	public void UpdateNodeStatus()
	{
		if (_bgImage.Target != null)
		{
			colorX a = RadiantUI_Constants.BG_COLOR;
			if (IsSelected.Value)
			{
				a = MathX.LerpUnclamped(in a, colorX.Cyan, 0.5f);
			}
			if (IsHighlighted.Value)
			{
				a = MathX.LerpUnclamped(in a, colorX.Yellow, 0.1f);
			}
			if (!IsNodeValid)
			{
				a = MathX.LerpUnclamped(in a, colorX.Red, 0.5f);
			}
			_bgImage.Target.Tint.Value = a;
			if (_overviewBg.IsLinkValid)
			{
				_overviewBg.Target.Value = a;
			}
		}
	}

	protected override void OnStart()
	{
		base.OnStart();
		if (Node.Target is IProtoFluxNodePackUnpackListener protoFluxNodePackUnpackListener)
		{
			protoFluxNodePackUnpackListener.PackStateChanged(isPacked: false);
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (Node.Target is IProtoFluxNodePackUnpackListener protoFluxNodePackUnpackListener)
		{
			protoFluxNodePackUnpackListener.PackStateChanged(isPacked: true);
		}
	}

	protected override void OnChanges()
	{
		base.OnChanges();
		UpdateNodeStatus();
		if (Node.IsTargetRemoved)
		{
			base.Slot.Destroy();
			base.Slot.Parent.GetComponent<Grabbable>()?.Destroy();
		}
		else if (_overviewVisual.IsLinkValid)
		{
			bool flag = !IsHighlighted.Value && (base.LocalUser.GetComponent<ProtofluxUserEditSettings>()?.OverviewMode.Value ?? false);
			_overviewVisual.Target.Value = flag;
			_labelBg.Target.Value = !flag;
			_labelText.Target.Value = !flag;
		}
	}

	protected override void OnDuplicate()
	{
		base.OnDuplicate();
		if (IsSelected.Value)
		{
			IsSelected.Value = false;
		}
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		Node = new RelayRef<ProtoFluxNode>();
		IsSelected = new Sync<bool>();
		IsSelected.MarkNonPersistent();
		IsHighlighted = new Sync<bool>();
		IsHighlighted.MarkNonPersistent();
		_nodeHoverArea = new SyncRef<HoverArea>();
		_bgImage = new SyncRef<Image>();
		_inputsRoot = new SyncRef<Slot>();
		_outputsRoot = new SyncRef<Slot>();
		_referencesRoot = new SyncRef<Slot>();
		_overviewVisual = new FieldDrive<bool>();
		_overviewBg = new FieldDrive<colorX>();
		_labelBg = new FieldDrive<bool>();
		_labelText = new FieldDrive<bool>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => Node, 
			4 => IsSelected, 
			5 => IsHighlighted, 
			6 => _nodeHoverArea, 
			7 => _bgImage, 
			8 => _inputsRoot, 
			9 => _outputsRoot, 
			10 => _referencesRoot, 
			11 => _overviewVisual, 
			12 => _overviewBg, 
			13 => _labelBg, 
			14 => _labelText, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static ProtoFluxNodeVisual __New()
	{
		return new ProtoFluxNodeVisual();
	}
}
