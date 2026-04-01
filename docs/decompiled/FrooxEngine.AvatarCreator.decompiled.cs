using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Elements.Core;
using FrooxEngine.CommonAvatar;
using FrooxEngine.FinalIK;
using FrooxEngine.UIX;
using Renderite.Shared;

namespace FrooxEngine;

[GloballyRegistered]
public class AvatarCreator : Component, IMaterialApplyPolicy, IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable
{
	private class Anchor : SyncObject
	{
		public readonly FieldDrive<float3> ScaleDrive;

		public readonly Sync<string> AnchorName;

		public readonly Sync<bool> IsRight;

		public readonly SyncRef<Anchor> OtherSide;

		public new readonly SyncRef<Slot> Slot;

		public readonly SyncRef<Slider> Slider;

		protected override void InitializeSyncMembers()
		{
			base.InitializeSyncMembers();
			ScaleDrive = new FieldDrive<float3>();
			AnchorName = new Sync<string>();
			IsRight = new Sync<bool>();
			OtherSide = new SyncRef<Anchor>();
			Slot = new SyncRef<Slot>();
			Slider = new SyncRef<Slider>();
		}

		public override ISyncMember GetSyncMember(int index)
		{
			return index switch
			{
				0 => ScaleDrive, 
				1 => AnchorName, 
				2 => IsRight, 
				3 => OtherSide, 
				4 => Slot, 
				5 => Slider, 
				_ => throw new ArgumentOutOfRangeException(), 
			};
		}

		public static Anchor __New()
		{
			return new Anchor();
		}
	}

	private const string JP_BLINK = "まばたき";

	private const string JP_WINK_RIGHT = "ウィンク右";

	private const string JP_WINK_LEFT = "ウィンク２";

	private const string JP_WINK_LEFT_ALT = "ウィンク";

	public const float HEADSET_DETECTION_RADIUS = 0.2f;

	public const float CONTROLLER_DETECTION_RADIUS = 0.15f;

	public const float CONTROLLER_SEPARATION_DISTANCE = 0.5f;

	public const float FEET_SEPARATION_DISTANCE = 0.3f;

	protected readonly SyncRef<Slot> _headsetPoint;

	protected readonly SyncRef<Slot> _leftPoint;

	protected readonly SyncRef<Slot> _rightPoint;

	protected readonly SyncRef<Slot> _leftFootPoint;

	protected readonly SyncRef<Slot> _rightFootPoint;

	protected readonly SyncRef<Slot> _pelvisPoint;

	protected readonly SyncRef<Slot> _headsetReference;

	protected readonly SyncRef<Slot> _pelvisReference;

	protected readonly SyncRef<Slot> _leftReference;

	protected readonly SyncRef<Slot> _rightReference;

	protected readonly SyncRef<Slot> _leftFootReference;

	protected readonly SyncRef<Slot> _rightFootReference;

	protected readonly Sync<bool> _initialized;

	protected readonly Sync<bool> _showAnchors;

	protected readonly Sync<bool> _useSymmetry;

	protected readonly Sync<bool> _setupVolumeMeter;

	protected readonly Sync<bool> _setupProtection;

	protected readonly Sync<bool> _setupEyes;

	protected readonly Sync<bool> _setupFaceTracking;

	protected readonly Sync<bool> _calibrateFeet;

	protected readonly Sync<bool> _calibratePelvis;

	protected readonly Sync<bool> _canProtect;

	protected readonly Sync<bool> _symmetrySetup;

	protected readonly SyncList<Anchor> _anchors;

	protected readonly Sync<float> _scale;

	protected readonly FieldDrive<bool> _protectAvatarEnabled;

	protected readonly FieldDrive<bool> _createEnabled;

	bool IMaterialApplyPolicy.CanApplyMaterial => false;

	protected override void OnAwake()
	{
		base.OnAwake();
		_useSymmetry.Value = true;
		_setupEyes.Value = true;
		_setupFaceTracking.Value = false;
		_setupProtection.Value = true;
		_anchors.ElementsAdded += _anchors_ElementsAdded;
		_scale.Value = 1f;
	}

	private void _anchors_ElementsAdded(SyncElementList<Anchor> list, int startIndex, int count)
	{
		for (int i = 0; i < count; i++)
		{
			list[i + startIndex].ScaleDrive.SetupValueSetHook(ScaleSet);
		}
	}

	private void ScaleSet(IField<float3> field, float3 value)
	{
		field.Value = value;
		Slot slot = field.FindNearestParent<Slot>();
		_scale.Value = base.Slot.GlobalScaleToLocal(slot.GlobalScale).x;
	}

	private void SetupGrabbableProxy(Slot slot, string name, bool isRight)
	{
		Slider slider = slot.AttachComponent<Slider>();
		slider.Rotatable.Value = true;
		slider.Scalable.Value = true;
		slider.GrabPriority.Value = 10;
		slider.DontDrive.Value = true;
		AddAnchor(slot, slider, name, isRight);
	}

	protected override void OnAttach()
	{
		base.Slot.AttachComponent<ObjectRoot>();
		base.Slot.AttachComponent<DestroyRoot>();
		base.Slot.Tag = "Developer";
		PBS_DualSidedMetallic material = base.Slot.AttachComponent<PBS_DualSidedMetallic>();
		material.AlphaHandling.Value = AlphaHandling.AlphaBlend;
		material.AlbedoColor.Value = new colorX(1f, 0.8f, 0.1f, 0.1f);
		material.Smoothness.Value = 0.8f;
		material.Metallic.Value = 0f;
		material.EmissiveColor.Value = material.AlbedoColor.Value * 0.15f;
		PBS_DualSidedMetallic leftMaterial = base.Slot.CopyComponent(material);
		leftMaterial.AlbedoColor.Value = colorX.Cyan.SetA(0.42f);
		leftMaterial.EmissiveColor.Value = leftMaterial.AlbedoColor.Value * 0.15f;
		PBS_DualSidedMetallic rightMaterial = base.Slot.CopyComponent(material);
		rightMaterial.AlbedoColor.Value = colorX.Red.SetA(0.42f);
		rightMaterial.EmissiveColor.Value = rightMaterial.AlbedoColor.Value * 0.15f;
		PBS_DualSidedMetallic pBS_DualSidedMetallic = base.Slot.CopyComponent(material);
		pBS_DualSidedMetallic.AlbedoColor.Value = colorX.Purple.SetA(0.35f);
		pBS_DualSidedMetallic.EmissiveColor.Value = pBS_DualSidedMetallic.AlbedoColor.Value * 0.15f;
		FresnelMaterial fresnelMaterial = base.Slot.AttachComponent<FresnelMaterial>();
		fresnelMaterial.BlendMode.Value = BlendMode.Additive;
		fresnelMaterial.NearColor.Value = new colorX(0f);
		fresnelMaterial.FarColor.Value = new colorX(0.5f, 0.4f, 0f);
		Slot headsetSlot = base.Slot.AddSlot("Headset");
		SetupGrabbableProxy(headsetSlot, "Headset", isRight: false);
		Task headsetTask = StartTask(async delegate
		{
			await DebugAvatarBuilder.SpawnHeadsetModel(headsetSlot, colorX.White);
		});
		Slot slot = headsetSlot.AddSlot("Left Eye");
		Slot slot2 = headsetSlot.AddSlot("Right Eye");
		slot.AttachSphere(0.012f, material, collider: false);
		slot2.AttachSphere(0.012f, material, collider: false);
		slot.LocalPosition = float3.Left * 0.064f * 0.5f;
		slot2.LocalPosition = float3.Right * 0.064f * 0.5f;
		Slot leftHandSlot = base.Slot.AddSlot("LeftHand");
		Slot leftHandModel = leftHandSlot.AddSlot("Model");
		SetupGrabbableProxy(leftHandSlot, "Hand", isRight: false);
		Task<bool> leftHandTask = leftHandModel.LoadObjectAsync(OfficialAssets.Graphics.Avatar_Creator.LeftHand);
		Slot slot3 = leftHandSlot.AddSlot("Label");
		slot3.LocalPosition = new float3(0f, 0.058f, -0.075f);
		slot3.LocalRotation = floatQ.Euler(0f, -90f, 0f);
		TextRenderer textRenderer = slot3.AttachComponent<TextRenderer>();
		textRenderer.Color.Value = leftMaterial.AlbedoColor;
		textRenderer.Text.Value = "Left";
		Slot rightHandSlot = base.Slot.AddSlot("RightHand");
		Slot rightHandModel = rightHandSlot.AddSlot("Model");
		SetupGrabbableProxy(rightHandSlot, "Hand", isRight: true);
		Task<bool> rightHandTask = rightHandModel.LoadObjectAsync(OfficialAssets.Graphics.Avatar_Creator.RightHand);
		Slot slot4 = rightHandSlot.AddSlot("Label");
		slot4.LocalPosition = new float3(0f, 0.058f, -0.075f);
		slot4.LocalRotation = floatQ.Euler(0f, 90f, 0f);
		TextRenderer textRenderer2 = slot4.AttachComponent<TextRenderer>();
		textRenderer2.Color.Value = rightMaterial.AlbedoColor;
		textRenderer2.Text.Value = "Right";
		Slot slot5 = base.Slot.AddSlot("LeftFoot");
		Slot leftFootModel = slot5.AddSlot("Model");
		SetupGrabbableProxy(slot5, "Foot", isRight: false);
		Task<bool> leftFootTask = leftFootModel.LoadObjectAsync(OfficialAssets.Graphics.Avatar_Creator.LeftFoot);
		slot5.LocalPosition = float3.Left * 0.3f * 0.5f + float3.Up * 0.1f;
		Slot slot6 = base.Slot.AddSlot("RightFoot");
		Slot rightFootModel = slot6.AddSlot("Model");
		SetupGrabbableProxy(slot6, "Foot", isRight: true);
		Task<bool> rightFootTask = rightFootModel.LoadObjectAsync(OfficialAssets.Graphics.Avatar_Creator.RightFoot);
		slot6.LocalPosition = float3.Right * 0.3f * 0.5f + float3.Up * 0.1f;
		Slot slot7 = base.Slot.AddSlot("Pelvis");
		Slot pelvisModel = slot7.AddSlot("Model");
		SetupGrabbableProxy(slot7, "Pelvis", isRight: false);
		Task<bool> pelvisTask = pelvisModel.LoadObjectAsync(OfficialAssets.Graphics.Avatar_Creator.Pelvis);
		slot7.LocalPosition = float3.Up;
		headsetSlot.LocalPosition = float3.Up * 1.8f;
		leftHandSlot.LocalPosition = float3.Up + float3.Left * 0.5f * 0.5f;
		leftHandSlot.LocalRotation = floatQ.Euler(90f, 0f, 0f);
		rightHandSlot.LocalPosition = float3.Up + float3.Right * 0.5f * 0.5f;
		rightHandSlot.LocalRotation = floatQ.Euler(90f, 0f, 0f);
		StartTask(async delegate
		{
			await headsetTask;
			await leftHandTask;
			await rightHandTask;
			await leftFootTask;
			await rightFootTask;
			await pelvisTask;
			headsetSlot.ForeachComponentInChildren(delegate(MeshRenderer m)
			{
				m.Material.Target = material;
			});
			leftHandModel.ForeachComponentInChildren(delegate(MeshRenderer m)
			{
				m.Material.Target = leftMaterial;
			});
			leftHandModel.LocalRotation = floatQ.Euler(0f, 90f, 0f);
			rightHandModel.ForeachComponentInChildren(delegate(MeshRenderer m)
			{
				m.Material.Target = rightMaterial;
			});
			rightHandModel.LocalRotation = floatQ.Euler(0f, -90f, 0f);
			leftFootModel.ForeachComponentInChildren(delegate(MeshRenderer m)
			{
				m.Material.Target = leftMaterial;
			});
			leftFootModel.LocalRotation = floatQ.Euler(0f, 0f, 0f);
			rightFootModel.ForeachComponentInChildren(delegate(MeshRenderer m)
			{
				m.Material.Target = rightMaterial;
			});
			rightFootModel.LocalRotation = floatQ.Euler(0f, 0f, 0f);
			pelvisModel.ForeachComponentInChildren(delegate(MeshRenderer m)
			{
				m.Material.Target = material;
			});
			pelvisModel.LocalRotation = floatQ.Euler(0f, 0f, 0f);
		});
		SpawnToolAnchors(leftHandSlot, pBS_DualSidedMetallic, isRight: false);
		SpawnToolAnchors(rightHandSlot, pBS_DualSidedMetallic, isRight: true);
		Slot slot8 = headsetSlot.AddSlot("HeadsetPoint");
		Slot leftPoint = leftHandSlot.AddSlot("LeftControllerPoint");
		Slot rightPoint = rightHandSlot.AddSlot("RightControllerPoint");
		Slot slot9 = slot5.AddSlot("LeftFootPoint");
		Slot slot10 = slot6.AddSlot("RightFootPoint");
		Slot slot11 = slot7.AddSlot("PelvisPoint");
		slot8.AttachSphere(0.2f, fresnelMaterial, collider: false);
		leftPoint.AttachSphere(0.15f, fresnelMaterial, collider: false);
		rightPoint.AttachSphere(0.15f, fresnelMaterial, collider: false);
		slot9.AttachSphere(0.15f, fresnelMaterial, collider: false);
		slot10.AttachSphere(0.15f, fresnelMaterial, collider: false);
		slot11.AttachSphere(0.15f, fresnelMaterial, collider: false);
		slot8.CenterAt(headsetSlot);
		StartTask(async delegate
		{
			await leftHandTask;
			await rightHandTask;
			leftPoint.CenterAt(leftHandSlot);
			rightPoint.CenterAt(rightHandSlot);
		});
		Slot slot12 = base.Slot.AddSlot("Panel");
		UIBuilder uIBuilder = RadiantUI_Panel.SetupPanel(slot12, "AvatarCreator.Title".AsLocaleKey(), new float2(360f, 640f));
		slot12.LocalScale *= 0.0005f;
		RadiantUI_Constants.SetupEditorStyle(uIBuilder);
		slot12.Tag = "Developer";
		slot12.DestroyWhenDestroyed(base.Slot);
		slot12.LocalPosition = float3.Up * 1.4f + float3.Backward * 0.2f;
		slot12.LocalRotation = floatQ.Euler(25f, 180f, 0f);
		uIBuilder.VerticalLayout(4f);
		uIBuilder.Style.FlexibleHeight = 100f;
		uIBuilder.Style.MinHeight = 64f;
		uIBuilder.Text("AvatarCreator.Instructions".AsLocaleKey());
		uIBuilder.Style.FlexibleHeight = -1f;
		uIBuilder.Style.MinHeight = 24f;
		Checkbox checkbox = uIBuilder.Checkbox("AvatarCreator.UseSymmetry".AsLocaleKey(), state: true);
		checkbox.TargetState.Target = _useSymmetry;
		checkbox = uIBuilder.Checkbox("AvatarCreator.ShowToolAnchors".AsLocaleKey());
		checkbox.TargetState.Target = _showAnchors;
		checkbox = uIBuilder.Checkbox("AvatarCreator.SetupVolumeMeter".AsLocaleKey(), state: true);
		checkbox.TargetState.Target = _setupVolumeMeter;
		checkbox = uIBuilder.Checkbox("AvatarCreator.SetupEyes".AsLocaleKey());
		checkbox.TargetState.Target = _setupEyes;
		checkbox = uIBuilder.Checkbox("AvatarCreator.SetupFaceTracking".AsLocaleKey());
		checkbox.TargetState.Target = _setupFaceTracking;
		Image image = uIBuilder.VerticalLayout(0f, 40f, 0f, 40f, 0f).Slot.AttachComponent<Image>();
		image.Tint.Value = RadiantUI_Constants.Sub.RED;
		image.Tint.DriveFromBool(_canProtect, colorX.Clear, RadiantUI_Constants.Dark.RED);
		checkbox = uIBuilder.Checkbox("AvatarCreator.ProtectAvatar".AsLocaleKey());
		checkbox.TargetState.Target = _setupProtection;
		_protectAvatarEnabled.Target = checkbox.Slot.GetComponentInChildren<Button>().EnabledField;
		uIBuilder.Style.MinHeight = 30f;
		uIBuilder.Panel();
		uIBuilder.Text("AvatarCreator.ProtectionUnavailable".AsLocaleKey(), bestFit: true, Alignment.MiddleLeft).EnabledField.DriveInverted(_canProtect);
		uIBuilder.NestOut();
		uIBuilder.NestOut();
		uIBuilder.Style.MinHeight = 24f;
		uIBuilder.Button("AvatarCreator.AlignHeadForward".AsLocaleKey(), AlignHeadForward);
		uIBuilder.Button("AvatarCreator.AlignHeadUp".AsLocaleKey(), AlignHeadUp);
		uIBuilder.Button("AvatarCreator.AlignHeadRight".AsLocaleKey(), AlignHeadRight);
		uIBuilder.Button("AvatarCreator.CenterHead".AsLocaleKey(), AlignHeadPosition);
		uIBuilder.Button("AvatarCreator.TryAlignHands".AsLocaleKey(), AlignHands);
		uIBuilder.Button("AvatarCreator.AlignToolAnchors".AsLocaleKey(), AlignToolAnchors);
		uIBuilder.Style.MinHeight = 32f;
		Button button = uIBuilder.Button("AvatarCreator.Create".AsLocaleKey("<b>{0}</b>"), new colorX?(RadiantUI_Constants.Sub.GREEN), OnCreate);
		_createEnabled.Target = button.EnabledField;
		_headsetPoint.Target = slot8;
		_leftPoint.Target = leftPoint;
		_rightPoint.Target = rightPoint;
		_leftFootPoint.Target = slot9;
		_rightFootPoint.Target = rightFootModel;
		_pelvisPoint.Target = slot11;
		_headsetReference.Target = headsetSlot;
		_pelvisReference.Target = slot7;
		_leftReference.Target = leftHandSlot;
		_rightReference.Target = rightHandSlot;
		_leftFootReference.Target = slot5;
		_rightFootReference.Target = slot6;
		foreach (Anchor anchor in _anchors)
		{
			if (!anchor.IsRight)
			{
				Anchor anchor2 = _anchors.FirstOrDefault((Anchor a) => a.IsRight.Value && a.AnchorName.Value == anchor.AnchorName.Value);
				if (anchor2 != null)
				{
					anchor2.OtherSide.Target = anchor;
					anchor.OtherSide.Target = anchor2;
				}
			}
		}
		_initialized.Value = true;
	}

	protected override void OnCommonUpdate()
	{
		_canProtect.Value = !string.IsNullOrEmpty(base.LocalUser.UserID);
		if (_protectAvatarEnabled.IsLinkValid)
		{
			_protectAvatarEnabled.Target.Value = _canProtect.Value || _setupProtection.Value;
		}
		if (_createEnabled.IsLinkValid)
		{
			_createEnabled.Target.Value = !_setupProtection.Value || _canProtect.Value;
		}
		foreach (Anchor anchor in _anchors)
		{
			Slider target = anchor.Slider.Target;
			if (target != null)
			{
				_ = target.Slot;
				if (target.IsGrabbed && target.Grabber.Slot.GetComponentInParents<UserRoot>()?.ActiveUser == base.LocalUser)
				{
					_scale.Value = base.Slot.GlobalScaleToLocal(anchor.Slot.Target.GlobalScale.x);
				}
			}
		}
	}

	protected override void OnChanges()
	{
		if (_leftFootReference.Target != null)
		{
			_leftFootReference.Target.ActiveSelf = _calibrateFeet;
		}
		if (_rightFootReference.Target != null)
		{
			_rightFootReference.Target.ActiveSelf = _calibrateFeet;
		}
		if (_pelvisReference.Target != null)
		{
			_pelvisReference.Target.ActiveSelf = _calibratePelvis;
		}
		if (_symmetrySetup.Value != _useSymmetry.Value)
		{
			foreach (Anchor anchor in _anchors)
			{
				if (anchor.Slot.Target == null)
				{
					base.Slot.Destroy();
					return;
				}
				if (anchor.OtherSide.Target == null || anchor.IsRight.Value)
				{
					continue;
				}
				Slot target = anchor.Slot.Target;
				if (_useSymmetry.Value)
				{
					MirrorTransform componentOrAttach = target.GetComponentOrAttach<MirrorTransform>();
					componentOrAttach.MirrorSource.Target = anchor.OtherSide.Target.Slot.Target;
					componentOrAttach.MirrorPlane.Target = _headsetReference.Target;
					componentOrAttach.MirrorNormal.Value = float3.Right;
					componentOrAttach.AllowWriteBack.Value = true;
				}
				else
				{
					target.GetComponents<MirrorTransform>().ForEach(delegate(MirrorTransform c)
					{
						c.Destroy();
					});
				}
			}
			_symmetrySetup.Value = _useSymmetry.Value;
		}
		foreach (Anchor anchor2 in _anchors)
		{
			if (anchor2.Slot.Target == null)
			{
				base.Slot.Destroy();
				break;
			}
			anchor2.Slot.Target.LocalScale = base.Slot.LocalScaleToSpace(_scale * float3.One, anchor2.Slot.Target.Parent);
		}
	}

	private void SpawnToolAnchors(Slot controllerModel, IAssetProvider<Material> material, bool isRight)
	{
		Slot slot = controllerModel.AddSlot("Tooltip");
		Slot slot2 = controllerModel.AddSlot("Grabber");
		Slot slot3 = controllerModel.AddSlot("Shelf");
		slot.LocalPosition += new float3(0f, 0f, 0.15f);
		slot2.LocalPosition += new float3(0f, -0.02f, 0.075f);
		slot3.LocalPosition += new float3(0f, 0.03f, 0.03f);
		SetupAnchor(slot, "Tooltip", isRight);
		SetupAnchor(slot2, "Grabber", isRight);
		SetupAnchor(slot3, "Shelf", isRight);
		Slot slot4 = slot3.AddSlot("Model");
		slot4.LocalRotation = InteractionHandler.SHELF_DEFAULT_ROTATION;
		Slot slot5 = slot.AddSlot("Model");
		slot5.LocalRotation = floatQ.Euler(90f, 0f, 0f);
		slot5.LocalPosition = float3.Forward * 0.05f;
		ConeMesh coneMesh = slot5.AttachMesh<ConeMesh>(material, collider: true);
		coneMesh.RadiusBase.Value = 0.015f;
		coneMesh.RadiusTop.Value = 0.0025f;
		coneMesh.Height.Value = 0.05f;
		slot2.AttachMesh<IcoSphereMesh>(material, collider: true).Radius.Value = 0.07f;
		BevelStripeMesh bevelStripeMesh = slot4.AttachMesh<BevelStripeMesh>(material, collider: true);
		bevelStripeMesh.Slant = -22.5f;
		bevelStripeMesh.Width = 0.05f;
		bevelStripeMesh.Thickness = 0.005f;
		bevelStripeMesh.Height = 0.06f;
	}

	private void SetupAnchor(Slot anchor, string name, bool isRight)
	{
		anchor.ActiveSelf_Field.DriveFrom(_showAnchors);
		Slider slider = anchor.AttachComponent<Slider>();
		AddAnchor(anchor, slider, name, isRight);
		slider.Rotatable.Value = true;
		slider.Scalable.Value = true;
		slider.DontDrive.Value = true;
		slider.GrabPriority.Value = 5;
	}

	private void AddAnchor(Slot slot, Slider slider, string name, bool isRight)
	{
		Anchor anchor = _anchors.Add();
		anchor.ScaleDrive.Target = slot.Scale_Field;
		anchor.Slot.Target = slot;
		anchor.Slider.Target = slider;
		anchor.AnchorName.Value = name;
		anchor.IsRight.Value = isRight;
	}

	[SyncMethod(typeof(Delegate), null)]
	private void OnCreate(IButton button, ButtonEventData eventData)
	{
		RunCreate();
	}

	private void OnCancel(IButton button, ButtonEventData eventData)
	{
		base.Slot.Destroy();
	}

	[SyncMethod(typeof(Delegate), null)]
	private void AlignHands(IButton button, ButtonEventData eventData)
	{
		List<Slot> headsetObjects;
		BipedRig bipedRig = TryGetBipedFromHead(out headsetObjects);
		if (bipedRig != null && bipedRig.IsValid)
		{
			VRIK componentInChildrenOrParents = bipedRig.Slot.GetComponentInChildrenOrParents<VRIK>();
			if (componentInChildrenOrParents != null)
			{
				Slot target = _leftReference.Target;
				Slot target2 = _rightReference.Target;
				Slot slot = bipedRig[BodyNode.LeftHand];
				Slot slot2 = bipedRig[BodyNode.RightHand];
				IKSolverVR.Arm leftArm = componentInChildrenOrParents.Solver.leftArm;
				IKSolverVR.Arm rightArm = componentInChildrenOrParents.Solver.rightArm;
				target.GlobalPosition = slot.GlobalPosition;
				target2.GlobalPosition = slot2.GlobalPosition;
				float3 b = leftArm.WristToPalmAxis.Value;
				float3 a = leftArm.PalmToThumbAxis.Value;
				MathX.Cross(in a, in b);
				float3 b2 = rightArm.WristToPalmAxis.Value;
				float3 a2 = rightArm.PalmToThumbAxis.Value;
				MathX.Cross(in a2, in b2);
				floatQ b3 = floatQ.FromToRotation(floatQ.LookRotation(in b, in a), floatQ.LookRotation(float3.Forward, float3.Up));
				floatQ b4 = floatQ.FromToRotation(floatQ.LookRotation(in b2, in a2), floatQ.LookRotation(float3.Forward, float3.Up));
				floatQ b5 = floatQ.AxisAngle(float3.Forward, 180f);
				target.GlobalRotation = slot.GlobalRotation * b3 * b5;
				target2.GlobalRotation = slot2.GlobalRotation * b4 * b5;
			}
		}
	}

	[SyncMethod(typeof(Delegate), null)]
	private void AlignHeadPosition(IButton button, ButtonEventData eventData)
	{
		AlignHead(position: true, alignForward: false, alignUp: false, alignRight: false);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void AlignHeadForward(IButton button, ButtonEventData eventData)
	{
		AlignHead(position: false, alignForward: true, alignUp: false, alignRight: false);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void AlignHeadUp(IButton button, ButtonEventData eventData)
	{
		AlignHead(position: false, alignForward: false, alignUp: true, alignRight: false);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void AlignHeadRight(IButton button, ButtonEventData eventData)
	{
		AlignHead(position: false, alignForward: false, alignUp: false, alignRight: true);
	}

	[SyncMethod(typeof(Delegate), null)]
	private void AlignToolAnchors(IButton button, ButtonEventData eventData)
	{
		AlignAnchors(_leftReference.Target);
		AlignAnchors(_rightReference.Target);
	}

	private void AlignAnchors(Slot controller)
	{
		Slot slot = controller.FindChild("Tooltip");
		Slot slot2 = controller.FindChild("Grabber");
		Slot slot3 = controller.FindChild("Shelf");
		if (slot != null)
		{
			slot.GlobalRotation = controller.GlobalRotation;
		}
		if (slot2 != null)
		{
			slot2.GlobalRotation = controller.GlobalRotation;
		}
		if (slot3 != null)
		{
			slot3.GlobalRotation = controller.GlobalRotation;
		}
	}

	private BipedRig TryGetBipedFromHead(out List<Slot> headsetObjects)
	{
		List<ICollider> colliders = base.Physics.SphereOverlap(_headsetPoint.Target.GlobalPosition, 0.2f);
		FilterObjects(colliders, null);
		headsetObjects = GetRootGroups(colliders);
		if (headsetObjects.Count == 0)
		{
			return null;
		}
		BipedRig bipedRig = null;
		foreach (Slot headsetObject in headsetObjects)
		{
			bipedRig = headsetObject.GetComponentInChildren<BipedRig>() ?? headsetObject.GetComponentInParents<BipedRig>();
			if (bipedRig != null)
			{
				break;
			}
		}
		return bipedRig;
	}

	private void AlignHead(bool position, bool alignForward, bool alignUp, bool alignRight)
	{
		List<Slot> headsetObjects;
		BipedRig bipedRig = TryGetBipedFromHead(out headsetObjects);
		Slot target = _headsetReference.Target;
		float3 from = target.Forward;
		float3 from2 = target.Up;
		float3 from3 = target.Right;
		float3 globalPoint;
		float3 globalDirection;
		float3 globalDirection2;
		float3 globalDirection3;
		if (bipedRig != null)
		{
			Slot slot = bipedRig[BodyNode.Head];
			globalPoint = slot.GlobalPosition;
			globalDirection = slot.GetClosestAxis(in from);
			globalDirection2 = slot.GetClosestAxis(in from2);
			globalDirection3 = slot.GetClosestAxis(in from3);
		}
		else
		{
			BoundingBox boundingBox = BoundingBox.Empty();
			globalDirection = float3.Zero;
			globalDirection2 = float3.Zero;
			globalDirection3 = float3.Zero;
			foreach (Slot item in headsetObjects)
			{
				globalDirection += item.GetClosestAxis(in from);
				globalDirection2 += item.GetClosestAxis(in from2);
				globalDirection3 += item.GetClosestAxis(in from3);
				boundingBox.Encapsulate(item.ComputeBoundingBox());
			}
			globalPoint = boundingBox.Center;
			globalDirection = globalDirection.Normalized;
			globalDirection2 = globalDirection2.Normalized;
			globalDirection3 = globalDirection3.Normalized;
		}
		if (position)
		{
			target.GlobalPosition = target.LocalPointToGlobal(target.GlobalPointToLocal(in globalPoint).x__);
		}
		if (alignForward)
		{
			target.GlobalRotation = floatQ.FromToRotation(in from, target.LocalDirectionToGlobal(target.GlobalDirectionToLocal(in globalDirection).x_z)) * target.GlobalRotation;
		}
		if (alignUp)
		{
			target.GlobalRotation = floatQ.FromToRotation(in from2, target.LocalDirectionToGlobal(target.GlobalDirectionToLocal(in globalDirection2).xy_)) * target.GlobalRotation;
		}
		if (alignRight)
		{
			target.GlobalRotation = floatQ.FromToRotation(in from3, target.LocalDirectionToGlobal(target.GlobalDirectionToLocal(in globalDirection3).x_z)) * target.GlobalRotation;
		}
	}

	private void RunCreate()
	{
		List<ICollider> list = base.Physics.SphereOverlap(_headsetPoint.Target.GlobalPosition, 0.2f);
		List<ICollider> colliders = base.Physics.SphereOverlap(_leftPoint.Target.GlobalPosition, 0.15f);
		List<ICollider> colliders2 = base.Physics.SphereOverlap(_rightPoint.Target.GlobalPosition, 0.15f);
		List<ICollider> colliders3 = null;
		List<ICollider> colliders4 = null;
		List<ICollider> colliders5 = null;
		if (_calibrateFeet.Value)
		{
			colliders3 = base.Physics.SphereOverlap(_leftFootPoint.Target.GlobalPosition, 0.15f);
			colliders4 = base.Physics.SphereOverlap(_rightFootPoint.Target.GlobalPosition, 0.15f);
		}
		if ((bool)_calibratePelvis)
		{
			colliders5 = base.Physics.SphereOverlap(_pelvisPoint.Target.GlobalPosition, 0.15f);
		}
		FilterObjects(list, null);
		FilterObjects(colliders, list);
		FilterObjects(colliders2, list);
		FilterObjects(colliders3, list);
		FilterObjects(colliders4, list);
		FilterObjects(colliders5, list);
		List<Slot> rootGroups = GetRootGroups(list);
		List<Slot> rootGroups2 = GetRootGroups(colliders);
		List<Slot> rootGroups3 = GetRootGroups(colliders2);
		List<Slot> rootGroups4 = GetRootGroups(colliders3);
		List<Slot> rootGroups5 = GetRootGroups(colliders4);
		List<Slot> rootGroups6 = GetRootGroups(colliders5);
		BipedRig bipedRig = null;
		foreach (Slot item in rootGroups)
		{
			bipedRig = item.GetComponentInChildren<BipedRig>() ?? item.GetComponentInParents<BipedRig>();
			if (bipedRig != null)
			{
				break;
			}
		}
		if (bipedRig == null)
		{
			foreach (Slot item2 in rootGroups2)
			{
				bipedRig = item2.GetComponentInChildren<BipedRig>() ?? item2.GetComponentInParents<BipedRig>();
				if (bipedRig != null)
				{
					break;
				}
			}
		}
		if (bipedRig == null)
		{
			foreach (Slot item3 in rootGroups3)
			{
				bipedRig = item3.GetComponentInChildren<BipedRig>() ?? item3.GetComponentInParents<BipedRig>();
				if (bipedRig != null)
				{
					break;
				}
			}
		}
		if (bipedRig == null)
		{
			foreach (Slot item4 in rootGroups4)
			{
				bipedRig = item4.GetComponentInChildren<BipedRig>() ?? item4.GetComponentInParents<BipedRig>();
				if (bipedRig != null)
				{
					break;
				}
			}
		}
		if (bipedRig == null)
		{
			foreach (Slot item5 in rootGroups5)
			{
				bipedRig = item5.GetComponentInChildren<BipedRig>() ?? item5.GetComponentInParents<BipedRig>();
				if (bipedRig != null)
				{
					break;
				}
			}
		}
		if (bipedRig == null)
		{
			foreach (Slot item6 in rootGroups6)
			{
				bipedRig = item6.GetComponentInChildren<BipedRig>() ?? item6.GetComponentInParents<BipedRig>();
				if (bipedRig != null)
				{
					break;
				}
			}
		}
		CreateAvatar(_headsetReference.Target, _leftReference.Target, _rightReference.Target, _leftFootReference.Target, _rightFootReference.Target, _pelvisReference.Target, _setupEyes, _setupProtection, _setupVolumeMeter, _setupFaceTracking, _calibrateFeet, _calibratePelvis, bipedRig, rootGroups, rootGroups2, rootGroups3, rootGroups4, rootGroups5, rootGroups6);
		base.Slot.Destroy();
	}

	[SyncMethod(typeof(Action<BipedRig, Slot, Slot, Slot, Slot, Slot, Slot, bool, bool, bool, bool>), new string[] { })]
	public static void CreateBipedAvatar(BipedRig biped, Slot headReference, Slot leftHandReference, Slot rightHandReference, Slot leftFootReference, Slot rightFootReference, Slot hipsReference, bool setupEyes, bool setupProtection, bool setupVolumeMeter, bool setupFaceTracking)
	{
		CreateAvatar(headReference, leftHandReference, rightHandReference, leftFootReference, rightFootReference, hipsReference, setupEyes, setupProtection, setupVolumeMeter, setupFaceTracking, leftFootReference != null && rightFootReference != null, hipsReference != null, biped, null, null, null, null, null, null);
	}

	private static void CreateAvatar(Slot headReference, Slot leftHandReference, Slot rightHandReference, Slot leftFootReference, Slot rightFootReference, Slot hipsReference, bool setupEyes, bool setupProtection, bool setupVolumeMeter, bool setupFaceTracking, bool calibrateFeet, bool calibrateHips, BipedRig biped, List<Slot> headsetObjects, List<Slot> leftObjects, List<Slot> rightObjects, List<Slot> leftFootObjects, List<Slot> rightFootObjects, List<Slot> pelvisObjects)
	{
		Slot slot = null;
		if (biped != null)
		{
			slot = biped.Slot.GetObjectRoot();
			VRIK vRIK = biped.Slot.GetComponent<VRIK>();
			if (vRIK == null)
			{
				UniLog.Warning("VRIK not found, but BipedRig was. Setting up IK");
				vRIK = slot.AttachComponent<VRIK>();
				vRIK.Solver.SimulationSpace.Target = slot;
				vRIK.Solver.OffsetSpace.Target = slot;
				vRIK.Initiate();
			}
			Slot slot2 = biped[BodyNode.LeftHand];
			Slot slot3 = biped[BodyNode.RightHand];
			SetupAnchors(leftHandReference, slot2);
			SetupAnchors(rightHandReference, slot3);
			if (setupEyes)
			{
				Slot slot4 = biped.TryGetBone(BodyNode.LeftEye);
				Slot slot5 = biped.TryGetBone(BodyNode.RightEye);
				if (slot4 != null && slot5 != null)
				{
					SetupEyes(headReference, slot4, slot5, biped, slot);
				}
			}
			slot.AttachComponent<VRIKAvatar>().Setup(vRIK, biped, headReference, leftHandReference, rightHandReference, calibrateFeet ? leftFootReference : null, calibrateFeet ? rightFootReference : null, calibrateHips ? hipsReference : null);
			IAvatarObject componentInChildren = slot.GetComponentInChildren((IAvatarObject o) => o.Node == BodyNode.LeftHand);
			IAvatarObject componentInChildren2 = slot.GetComponentInChildren((IAvatarObject o) => o.Node == BodyNode.RightHand);
			AvatarObjectComponentProxy avatarObjectComponentProxy = componentInChildren.Slot.AttachComponent<AvatarObjectComponentProxy>();
			AvatarObjectComponentProxy avatarObjectComponentProxy2 = componentInChildren2.Slot.AttachComponent<AvatarObjectComponentProxy>();
			avatarObjectComponentProxy.Target.Target = slot2;
			avatarObjectComponentProxy2.Target.Target = slot3;
		}
		else
		{
			Slot slot6 = null;
			Slot slot7 = null;
			slot = headReference.World.AddSlot("Avatar");
			slot.CopyTransform(headReference);
			SetupAvatarNode(headsetObjects, slot, headReference, BodyNode.Head);
			slot6 = SetupAvatarNode(leftObjects, slot, leftHandReference, BodyNode.LeftHand);
			slot7 = SetupAvatarNode(rightObjects, slot, rightHandReference, BodyNode.RightHand);
			SetupAvatarNode(leftFootObjects, slot, leftFootReference, BodyNode.LeftFoot);
			SetupAvatarNode(rightFootObjects, slot, rightFootReference, BodyNode.RightFoot);
			SetupAvatarNode(pelvisObjects, slot, hipsReference, BodyNode.Hips);
			if (slot6 != null)
			{
				SetupAnchors(leftHandReference, slot6);
			}
			if (slot7 != null)
			{
				SetupAnchors(rightHandReference, slot7);
			}
		}
		slot.GetComponentsInChildren<ObjectRoot>().ForEach(delegate(ObjectRoot c)
		{
			c.Destroy();
		});
		slot.GetComponentsInChildren<Grabbable>().ForEach(delegate(Grabbable c)
		{
			c.Destroy();
		});
		slot.GetComponentsInChildren<AvatarGroup>().ForEach(delegate(AvatarGroup c)
		{
			c.Destroy();
		});
		Grabbable grabbable = slot.AttachComponent<Grabbable>();
		slot.AttachComponent<ObjectRoot>();
		slot.AttachComponent<AvatarGroup>();
		slot.AttachComponent<AvatarRoot>();
		grabbable.CustomCanGrabCheck.Target = Grabbable.UserRootGrabCheck;
		if (setupProtection)
		{
			slot.AttachComponent<SimpleAvatarProtection>();
			foreach (MeshRenderer componentsInChild in slot.GetComponentsInChildren<MeshRenderer>())
			{
				componentsInChild.Slot.AttachComponent<SimpleAvatarProtection>();
			}
		}
		EnsureVoiceOutput(slot, biped, setupVolumeMeter);
		EnsureHeadPositioner(slot);
		SetupAwayIndicator(slot);
		if (setupFaceTracking)
		{
			TrySetupFaceTracking(slot);
		}
		slot.GetComponentInChildren((AvatarPoseNode n) => n.Node.Value == BodyNode.Head)?.InstrumentWithViewHeadOverride();
	}

	private static void SetupEyes(Slot headReference, Slot leftEye, Slot rightEye, BipedRig rig, Slot avatarRoot)
	{
		Slot slot = rig[BodyNode.Head].AddSlot("Eye Manager");
		slot.CopyTransform(headReference);
		EyeManager eyeManager = slot.AttachComponent<EyeManager>();
		AvatarEyeDataSourceAssigner avatarEyeDataSourceAssigner = slot.AttachComponent<AvatarEyeDataSourceAssigner>();
		AvatarUserReferenceAssigner avatarUserReferenceAssigner = slot.AttachComponent<AvatarUserReferenceAssigner>();
		avatarEyeDataSourceAssigner.TargetReference.Target = eyeManager.EyeDataSource;
		avatarUserReferenceAssigner.References.Add(eyeManager.SimulatingUser);
		eyeManager.IgnoreLocalUserHead.Value = true;
		Slot slot2 = leftEye.AddSlot("Left Eye Pivot");
		Slot slot3 = rightEye.AddSlot("Right Eye Pivot");
		slot2.Parent = leftEye.Parent;
		slot3.Parent = rightEye.Parent;
		slot2.GlobalRotation = slot.GlobalRotation;
		slot3.GlobalRotation = slot.GlobalRotation;
		leftEye.Parent = slot2;
		rightEye.Parent = slot3;
		EyeRotationDriver eyeRotationDriver = slot.AttachComponent<EyeRotationDriver>();
		eyeRotationDriver.EyeManager.Target = eyeManager;
		EyeRotationDriver.Eye eye = eyeRotationDriver.Eyes.Add();
		EyeRotationDriver.Eye eye2 = eyeRotationDriver.Eyes.Add();
		eye.Root.Target = slot2;
		eye.Side.Value = EyeSide.Left;
		eye.SetupFromRoot();
		eye2.Root.Target = slot3;
		eye2.Side.Value = EyeSide.Right;
		eye2.SetupFromRoot();
		EyeLinearDriver eyeLinearDriver = slot.AttachComponent<EyeLinearDriver>();
		eyeLinearDriver.EyeManager.Target = eyeManager;
		foreach (SkinnedMeshRenderer componentsInChild in avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
		{
			string text = null;
			string text2 = null;
			string text3 = null;
			for (int i = 0; i < componentsInChild.MeshBlendshapeCount; i++)
			{
				string text4 = componentsInChild.BlendShapeName(i);
				List<string> names = NameHeuristicsHelper.SplitName(text4);
				if (IsBlinkBlendshape(names))
				{
					Chirality? chirality = NameToChirality(text4, names);
					if (!chirality.HasValue)
					{
						text3 = GetBetterBlinkCandidate(text3, text4);
					}
					else if (chirality.Value == Chirality.Left)
					{
						text = GetBetterBlinkCandidate(text, text4);
					}
					else
					{
						text2 = GetBetterBlinkCandidate(text2, text4);
					}
				}
			}
			if (text != null && text2 == null)
			{
				text2 = text3;
			}
			if (text2 != null && text == null)
			{
				text = text3;
			}
			if (text != null && text2 != null)
			{
				EyeLinearDriver.Eye eye3 = eyeLinearDriver.Eyes.Add();
				EyeLinearDriver.Eye eye4 = eyeLinearDriver.Eyes.Add();
				eye3.Side.Value = EyeSide.Left;
				eye4.Side.Value = EyeSide.Right;
				eye3.OpenCloseTarget.Target = componentsInChild.GetBlendShape(text);
				eye4.OpenCloseTarget.Target = componentsInChild.GetBlendShape(text2);
			}
			else if (text3 != null)
			{
				EyeLinearDriver.Eye eye5 = eyeLinearDriver.Eyes.Add();
				eye5.Side.Value = EyeSide.Combined;
				eye5.OpenCloseTarget.Target = componentsInChild.GetBlendShape(text3);
			}
		}
	}

	private static int GetNameScore(string wholeName)
	{
		wholeName = wholeName.ToLower();
		if (wholeName.Contains("vrc"))
		{
			return 10;
		}
		if (wholeName.Contains("blinking"))
		{
			return 8;
		}
		if (wholeName.Contains("blink"))
		{
			return 7;
		}
		if (wholeName.Contains("close"))
		{
			return 5;
		}
		if (wholeName.Contains("closed"))
		{
			return 5;
		}
		if (wholeName.Contains("ウィンク右") || wholeName.Contains("ウィンク２"))
		{
			return 4;
		}
		if (wholeName.Contains("ウィンク"))
		{
			return 3;
		}
		if (wholeName.Contains("まばたき"))
		{
			return 2;
		}
		return 0;
	}

	private static Chirality? NameToChirality(string wholeName, List<string> names)
	{
		if (wholeName.Contains("ウィンク２"))
		{
			return Chirality.Left;
		}
		if (wholeName.Contains("ウィンク右"))
		{
			return Chirality.Right;
		}
		if (wholeName.Contains("ウィンク"))
		{
			return Chirality.Left;
		}
		return NameHeuristicsHelper.NameToBoneChirality(wholeName, names);
	}

	private static bool IsBlinkBlendshape(List<string> names)
	{
		if (names.Contains("まばたき"))
		{
			return true;
		}
		if (names.Contains("ウィンク右"))
		{
			return true;
		}
		if (names.Contains("ウィンク２"))
		{
			return true;
		}
		if (names.Contains("ウィンク"))
		{
			return true;
		}
		if (names.Contains("blink"))
		{
			return true;
		}
		if (names.Contains("blinking"))
		{
			return true;
		}
		if (names.Contains("wink"))
		{
			return true;
		}
		if (names.Contains("eye"))
		{
			if (names.Contains("close"))
			{
				return true;
			}
			if (names.Contains("closed"))
			{
				return true;
			}
		}
		return false;
	}

	private static string GetBetterBlinkCandidate(string a, string b)
	{
		if (a == null)
		{
			return b;
		}
		if (b == null)
		{
			return a;
		}
		int nameScore = GetNameScore(a);
		int nameScore2 = GetNameScore(b);
		if (nameScore > nameScore2)
		{
			return a;
		}
		if (nameScore2 > nameScore)
		{
			return b;
		}
		if (a.Length < b.Length)
		{
			return a;
		}
		return b;
	}

	private static void SetupAnchors(Slot reference, Slot anchorRoot)
	{
		Slot slot = reference.FindChild("Tooltip");
		Slot slot2 = reference.FindChild("Grabber");
		Slot slot3 = reference.FindChild("Shelf");
		if (slot != null)
		{
			SetupAnchor(anchorRoot, slot, AvatarToolAnchor.Point.Tool);
		}
		if (slot2 != null)
		{
			SetupAnchor(anchorRoot, slot2, AvatarToolAnchor.Point.GrabArea);
		}
		if (slot3 != null)
		{
			SetupAnchor(anchorRoot, slot3, AvatarToolAnchor.Point.Toolshelf);
		}
	}

	private static Slot SetupAvatarNode(List<Slot> objects, Slot avatarRoot, Slot reference, BodyNode bodyNode)
	{
		if (objects.Count > 0)
		{
			Slot slot = avatarRoot.AddSlot(bodyNode.ToString());
			slot.AttachComponent<AvatarPoseNode>().Node.Value = bodyNode;
			slot.AttachComponent<AvatarObjectScale>();
			slot.CopyTransform(reference);
			{
				foreach (Slot @object in objects)
				{
					@object.Parent = slot;
				}
				return slot;
			}
		}
		return null;
	}

	private static void SetupAnchor(Slot anchorRoot, Slot anchorReference, AvatarToolAnchor.Point point)
	{
		Slot slot = anchorRoot.AddSlot(point.ToString() + " Anchor");
		slot.CopyTransform(anchorReference);
		slot.AttachComponent<AvatarToolAnchor>().AnchorPoint.Value = point;
	}

	public static void EnsureVoiceOutput(Slot root, BipedRig biped, bool volumeMeter)
	{
		IAvatarObject componentInChildren = root.GetComponentInChildren((IAvatarObject o) => o.Node == BodyNode.Head);
		if (componentInChildren != null)
		{
			AvatarVoiceSourceAssigner componentOrAttach = componentInChildren.Slot.GetComponentOrAttach<AvatarVoiceSourceAssigner>();
			AvatarAudioOutputManager componentOrAttach2 = componentInChildren.Slot.GetComponentOrAttach<AvatarAudioOutputManager>();
			if (volumeMeter)
			{
				VolumeMeter volumeMeter2 = componentInChildren.Slot.AttachComponent<VolumeMeter>();
				componentInChildren.Slot.AttachComponent<AvatarVoiceSourceAssigner>().TargetReference.Target = volumeMeter2.Source;
			}
			AudioOutput audioOutput = root.GetComponentInChildren((AudioOutput o) => o.Source.Target == null);
			if (audioOutput == null)
			{
				audioOutput = componentInChildren.Slot.AttachComponent<AudioOutput>();
				audioOutput.SpatialBlend.Value = 1f;
				audioOutput.Spatialize.Value = true;
				audioOutput.AudioTypeGroup.Value = AudioTypeGroup.Voice;
			}
			componentOrAttach2.AudioOutput.Target = audioOutput;
			componentOrAttach.TargetReference.Target = audioOutput.Source;
			componentInChildren.Slot.GetComponentOrAttach<AvatarVoiceRangeVisualizer>();
			TrySetupVisemes(root, componentInChildren.Slot);
		}
	}

	public static void TrySetupFaceTracking(Slot root)
	{
		foreach (SkinnedMeshRenderer componentsInChild in root.GetComponentsInChildren<SkinnedMeshRenderer>())
		{
			AvatarExpressionDriver avatarExpressionDriver = componentsInChild.Slot.AttachComponent<AvatarExpressionDriver>();
			avatarExpressionDriver.AutoAssign(componentsInChild);
			if (avatarExpressionDriver.ExpressionDrivers.Count == 0)
			{
				avatarExpressionDriver.Destroy();
			}
		}
	}

	public static void TrySetupVisemes(Slot root, Slot head = null)
	{
		if (head == null)
		{
			head = root.GetComponentInChildren((IAvatarObject o) => o.Node == BodyNode.Head)?.Slot;
		}
		List<DirectVisemeDriver> list = new List<DirectVisemeDriver>();
		foreach (SkinnedMeshRenderer componentsInChild in root.GetComponentsInChildren<SkinnedMeshRenderer>())
		{
			DirectVisemeDriver directVisemeDriver = componentsInChild.Slot.AttachComponent<DirectVisemeDriver>();
			if (directVisemeDriver.AutoAssignTargets(componentsInChild))
			{
				list.Add(directVisemeDriver);
			}
			else
			{
				directVisemeDriver.Destroy();
			}
		}
		if (list.Count <= 0)
		{
			return;
		}
		VisemeAnalyzer visemeAnalyzer = head.AttachComponent<VisemeAnalyzer>();
		head.AttachComponent<AvatarVoiceSourceAssigner>().TargetReference.Target = visemeAnalyzer.Source;
		foreach (DirectVisemeDriver item in list)
		{
			item.Source.Target = visemeAnalyzer;
		}
	}

	private static void EnsureHeadPositioner(Slot root)
	{
		if (root.GetComponentInChildren<AvatarUserPositioner>() == null)
		{
			IAvatarObject componentInChildren = root.GetComponentInChildren((IAvatarObject o) => o.Node == BodyNode.Head);
			if (componentInChildren != null)
			{
				AvatarUserPositioner componentOrAttach = componentInChildren.Slot.GetComponentOrAttach<AvatarUserPositioner>();
				componentOrAttach.RotationNode.Value = UserRoot.UserNode.Feet;
				componentOrAttach.PositionNode.Value = UserRoot.UserNode.Head;
			}
		}
	}

	private static void SetupAwayIndicator(Slot root)
	{
		IAvatarObject componentInChildren = root.GetComponentInChildren((IAvatarObject o) => o.Node == BodyNode.Head);
		if (componentInChildren == null)
		{
			return;
		}
		AvatarUserReferenceAssigner avatarUserReferenceAssigner = componentInChildren.Slot.AttachComponent<AvatarUserReferenceAssigner>();
		IAssetProvider<Material> target = SimpleAwayIndicator.CreateAwayMaterial(root);
		foreach (MeshRenderer componentsInChild in root.GetComponentsInChildren<MeshRenderer>())
		{
			SimpleAwayIndicator simpleAwayIndicator = componentsInChild.Slot.AttachComponent<SimpleAwayIndicator>();
			simpleAwayIndicator.AwayMaterial.Target = target;
			simpleAwayIndicator.Renderer.Target = componentsInChild;
			avatarUserReferenceAssigner.References.Add(simpleAwayIndicator.User);
		}
	}

	private void FilterObjects(List<ICollider> colliders, List<ICollider> exclusiveColliders)
	{
		if (colliders == null)
		{
			return;
		}
		colliders.RemoveAll((ICollider c) => c.Slot.IsChildOf(base.Slot, includeSelf: true));
		colliders.RemoveAll((ICollider c) => c.Slot.ActiveUserRoot != null);
		if (exclusiveColliders == null)
		{
			return;
		}
		colliders.RemoveAll((ICollider c) => exclusiveColliders.Any((ICollider other) => c.Slot.IsChildOf(other.Slot, includeSelf: true)));
	}

	private static List<Slot> GetRootGroups(List<ICollider> colliders)
	{
		List<Slot> list = new List<Slot>();
		if (colliders == null)
		{
			return list;
		}
		while (colliders.Count > 0)
		{
			Slot current = null;
			colliders.RemoveAll(delegate(ICollider c)
			{
				Slot objectRoot = c.Slot.GetObjectRoot();
				if (current == null)
				{
					current = objectRoot;
					return true;
				}
				Slot slot = current.FindCommonRoot(objectRoot);
				if (slot.IsRootSlot)
				{
					return false;
				}
				current = slot;
				return true;
			});
			list.Add(current);
		}
		return list;
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		_headsetPoint = new SyncRef<Slot>();
		_leftPoint = new SyncRef<Slot>();
		_rightPoint = new SyncRef<Slot>();
		_leftFootPoint = new SyncRef<Slot>();
		_rightFootPoint = new SyncRef<Slot>();
		_pelvisPoint = new SyncRef<Slot>();
		_headsetReference = new SyncRef<Slot>();
		_pelvisReference = new SyncRef<Slot>();
		_leftReference = new SyncRef<Slot>();
		_rightReference = new SyncRef<Slot>();
		_leftFootReference = new SyncRef<Slot>();
		_rightFootReference = new SyncRef<Slot>();
		_initialized = new Sync<bool>();
		_showAnchors = new Sync<bool>();
		_useSymmetry = new Sync<bool>();
		_setupVolumeMeter = new Sync<bool>();
		_setupProtection = new Sync<bool>();
		_setupEyes = new Sync<bool>();
		_setupFaceTracking = new Sync<bool>();
		_calibrateFeet = new Sync<bool>();
		_calibratePelvis = new Sync<bool>();
		_canProtect = new Sync<bool>();
		_symmetrySetup = new Sync<bool>();
		_anchors = new SyncList<Anchor>();
		_scale = new Sync<float>();
		_protectAvatarEnabled = new FieldDrive<bool>();
		_createEnabled = new FieldDrive<bool>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => _headsetPoint, 
			4 => _leftPoint, 
			5 => _rightPoint, 
			6 => _leftFootPoint, 
			7 => _rightFootPoint, 
			8 => _pelvisPoint, 
			9 => _headsetReference, 
			10 => _pelvisReference, 
			11 => _leftReference, 
			12 => _rightReference, 
			13 => _leftFootReference, 
			14 => _rightFootReference, 
			15 => _initialized, 
			16 => _showAnchors, 
			17 => _useSymmetry, 
			18 => _setupVolumeMeter, 
			19 => _setupProtection, 
			20 => _setupEyes, 
			21 => _setupFaceTracking, 
			22 => _calibrateFeet, 
			23 => _calibratePelvis, 
			24 => _canProtect, 
			25 => _symmetrySetup, 
			26 => _anchors, 
			27 => _scale, 
			28 => _protectAvatarEnabled, 
			29 => _createEnabled, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static AvatarCreator __New()
	{
		return new AvatarCreator();
	}
}
