using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Elements.Assets;
using Elements.Core;
using Elements.Data;
using FrooxEngine.CommonAvatar;
using Renderite.Shared;

namespace FrooxEngine;

[OldTypeName("FrooxEngine.FingerPhotoGesture", null)]
public class PhotoCaptureManager : Component
{
	private struct GestureData
	{
		public float3 center;

		public float size;

		public float3 direction;

		public float distance;

		public float3 leftCorner;

		public float3 rightCorner;

		public float timerTrigger;

		public float takeTrigger;
	}

	public const string PHOTO_TEMP_HOLDER = "PhotoTempHolder";

	public readonly Sync<bool> FingerGestureEnabled;

	public readonly Sync<float> MinDistance;

	public readonly Sync<float> MaxDistance;

	public readonly Sync<float> MinFOV;

	public readonly Sync<float> MaxFOV;

	public readonly Sync<int2> PreviewResolution;

	public readonly Sync<int2> NormalResolution;

	public readonly Sync<int2> TimerResolution;

	public readonly Sync<bool> CaptureStereo;

	public readonly Sync<float> StereoSeparation;

	public readonly Sync<float> TimerSeconds;

	public readonly Sync<bool> HideAllNameplates;

	public readonly Sync<PhotoEncodeFormat> EncodeFormat;

	public readonly Sync<bool> DebugGesture;

	protected readonly SyncTime _timerStart;

	protected readonly Sync<bool> _timerActive;

	protected readonly SyncRef<Slot> _originalParent;

	protected readonly Sync<float3> _originalPosition;

	protected readonly Sync<floatQ> _originalRotation;

	protected readonly Sync<float3> _originalScale;

	protected readonly SyncRef<Slot> _root;

	protected readonly SyncRef<Slot> _previewRoot;

	protected readonly SyncRef<RenderTextureProvider> _renderTex;

	protected readonly SyncRef<QuadMesh> _quad;

	protected readonly SyncRef<FrameMesh> _frame;

	protected readonly SyncRef<Slot> _cameraRoot;

	protected readonly FieldDrive<float3> _cameraPos;

	protected readonly FieldDrive<floatQ> _cameraRot;

	protected readonly SyncRef<Camera> _camera;

	protected readonly SyncRef<UnlitMaterial> _frameMaterial;

	protected readonly SyncRef<Slot> _timerTextRoot;

	protected readonly SyncRef<TextRenderer> _timerText;

	protected readonly AssetRef<AudioClip> _shutterClip;

	protected readonly AssetRef<AudioClip> _timerStartClip;

	protected readonly SyncRef<AudioClipPlayer> _timerCountdownSlowPlayer;

	protected readonly SyncRef<AudioClipPlayer> _timerCountdownFastPlayer;

	protected readonly SyncRef<AudioOutput> _timerCountdownSlowOutput;

	protected readonly SyncRef<AudioOutput> _timerCountdownFastOutput;

	protected readonly SyncRef<Slot> _timerRoot;

	private bool timerTransitioned;

	private bool settingsRegistered;

	private bool capturePrivateUI;

	private float _bindingCaptureCharge;

	private float _fingerGestureCharge;

	private float _flash;

	private bool _timerPhotoTaken;

	private bool _lastTimerTrigger;

	private bool _lastTakeTrigger;

	private float _lastGestureSize = 0.2f;

	private PhotoInputs _inputs;

	private float _indexPerpendicularCharge;

	private void GetCaptureCameraPosition(out float3 pos, out floatQ rot)
	{
		if (!base.IsUnderLocalUser || base.InputInterface.VR_Active || _timerActive.Value)
		{
			pos = float3.Zero;
			rot = floatQ.Identity;
		}
		else
		{
			Slot parent = _cameraRoot.Target.Parent;
			pos = parent.GlobalPointToLocal(base.World.LocalUserViewPosition);
			rot = parent.GlobalRotationToLocal(base.World.LocalUserViewRotation);
		}
	}

	protected override void OnAwake()
	{
		base.OnAwake();
		FingerGestureEnabled.Value = true;
		MinDistance.Value = 0.1f;
		MaxDistance.Value = 0.5f;
		MinFOV.Value = 20f;
		MaxFOV.Value = 90f;
		PreviewResolution.Value = new int2(1920, 1080);
		NormalResolution.Value = new int2(1920, 1080);
		TimerResolution.Value = new int2(2560, 1440);
		TimerSeconds.Value = 10f;
		StereoSeparation.Value = 0.065f;
		EncodeFormat.Value = PhotoEncodeFormat.WebP;
	}

	protected override void OnAttach()
	{
		base.OnAttach();
		Slot slot = base.Slot.AddSlot("Photo Capture");
		slot.ActiveSelf = false;
		slot.PersistentSelf = false;
		Slot slot2 = slot.AddSlot("Timer Icon");
		slot2.AttachComponent<LookAtUser>().TargetAtLocalUser.Value = true;
		AttachedModel<QuadMesh, UnlitMaterial> attachedModel = slot2.AttachMesh<QuadMesh, UnlitMaterial>();
		attachedModel.mesh.Size.Value = float2.One * 0.05f;
		attachedModel.material.BlendMode.Value = BlendMode.Alpha;
		attachedModel.material.Texture.Target = slot2.AttachTexture(OfficialAssets.Common.Icons.Timer);
		Slot slot3 = slot.AddSlot("Preview");
		Slot slot4 = slot3.AddSlot("Timer");
		AttachedModel<QuadMesh, UnlitMaterial> attachedModel2 = slot3.AttachMesh<QuadMesh, UnlitMaterial>();
		AttachedModel<FrameMesh, UnlitMaterial> attachedModel3 = slot3.AttachMesh<FrameMesh, UnlitMaterial>();
		Slot slot5 = slot3.AddSlot("Camera");
		Camera camera = slot5.AttachComponent<Camera>();
		RenderTextureProvider renderTextureProvider = slot5.AttachComponent<RenderTextureProvider>();
		camera.DoubleBuffered.Value = true;
		camera.ExcludeRender.Add(slot);
		renderTextureProvider.Size.DriveFrom(PreviewResolution);
		renderTextureProvider.Depth.Value = 24;
		camera.RenderTexture.Target = renderTextureProvider;
		attachedModel2.material.Texture.Target = renderTextureProvider;
		attachedModel2.material.Sidedness.Value = Sidedness.Double;
		attachedModel3.mesh.Thickness.Value = 0.003f;
		attachedModel3.material.BlendMode.Value = BlendMode.Additive;
		TextRenderer textRenderer = slot4.AttachComponent<TextRenderer>();
		TextUnlitMaterial textUnlitMaterial = slot4.AttachComponent<TextUnlitMaterial>();
		textRenderer.Align = Alignment.BottomCenter;
		textRenderer.Material.Target = textUnlitMaterial;
		textRenderer.Color.Value = colorX.White;
		textUnlitMaterial.OutlineColor.Value = colorX.Black;
		textUnlitMaterial.OutlineThickness.Value = 0.2f;
		textUnlitMaterial.FaceDilate.Value = 0.2f;
		slot4.AttachComponent<FlipAtUser>();
		_root.Target = slot;
		_timerRoot.Target = slot2;
		_previewRoot.Target = slot3;
		_renderTex.Target = renderTextureProvider;
		_quad.Target = attachedModel2.mesh;
		_frame.Target = attachedModel3.mesh;
		_frameMaterial.Target = attachedModel3.material;
		_camera.Target = camera;
		_cameraRoot.Target = slot5;
		_cameraPos.Target = slot5.Position_Field;
		_cameraRot.Target = slot5.Rotation_Field;
		_timerText.Target = textRenderer;
		_timerTextRoot.Target = slot4;
		_shutterClip.Target = slot3.AttachAudioClip(OfficialAssets.Sounds.Interactions._520684__tonik1105__contarex_camera_shutter);
	}

	protected override void OnStart()
	{
		base.OnStart();
		if (base.IsUnderLocalUser)
		{
			_inputs = new PhotoInputs();
			base.Input.RegisterInputGroup(_inputs, this);
			Settings.RegisterValueChanges<PhotoCaptureSettings>(OnCaptureSettingsChanged);
			settingsRegistered = true;
		}
	}

	private colorX FilterColor(colorX c)
	{
		return MathX.Lerp(in c, colorX.White, 0.5f).MulRGB(0.5f) + _flash * 2f;
	}

	private void OnCaptureSettingsChanged(PhotoCaptureSettings settings)
	{
		RunSynchronously(delegate
		{
			FingerGestureEnabled.Value = settings.FingerGestureEnabled;
			NormalResolution.Value = settings.NormalCaptureResolution;
			TimerResolution.Value = settings.TimerCaptureResolution;
			TimerSeconds.Value = settings.TimerSeconds;
			MinFOV.Value = settings.HandsNearFOV;
			MaxFOV.Value = settings.HandsFarFOV;
			CaptureStereo.Value = settings.CaptureStereo;
			StereoSeparation.Value = settings.StereoSeparation;
			HideAllNameplates.Value = settings.AlwaysHideNameplates.Value;
			EncodeFormat.Value = settings.EncodeFormat;
			capturePrivateUI = settings.CapturePrivateUI.Value;
		});
	}

	protected override void OnCommonUpdate()
	{
		GetCaptureCameraPosition(out var pos, out var rot);
		_cameraPos.Target.Value = pos;
		_cameraRot.Target.Value = rot;
		if (!base.IsUnderLocalUser || base.World.Focus == FrooxEngine.World.WorldFocus.Background)
		{
			return;
		}
		_camera.Target.RenderPrivateUI = capturePrivateUI;
		_camera.Target.NearClipping.Value = (base.World.LocalUserRenderSettings?.NearClip ?? 0.005f) * (base.LocalUserRoot?.GlobalScale ?? 1f);
		_camera.Target.FarClipping.Value = base.World.LocalUserRenderSettings?.FarClip ?? 4096f;
		_flash -= base.Time.Delta * 2f;
		if (_flash < 0f)
		{
			_flash = 0f;
		}
		if (_timerActive.Value)
		{
			_timerTextRoot.Target.ActiveSelf = !_timerPhotoTaken;
			if (!_timerPhotoTaken)
			{
				_timerText.Target.Text.Value = MathX.Max(0.0, (double)TimerSeconds.Value - _timerStart.CurrentTime).ToString("F1");
				_timerTextRoot.Target.LocalPosition = new float3(0f, _quad.Target.Size.Value.y * 0.55f);
			}
			UnlitMaterial target = _frameMaterial.Target;
			bool num = _timerStart.CurrentTime < (double)(TimerSeconds.Value - 2f);
			if (num && !timerTransitioned)
			{
				TransitionToFastTimerSound();
			}
			float num2 = (num ? 1f : 0.5f);
			if (!_timerPhotoTaken)
			{
				target.TintColor.Value = FilterColor((_timerStart.CurrentTime % (double)num2 > (double)(num2 * 0.5f)) ? (colorX.Orange * 3f) : colorX.Cyan);
			}
			else
			{
				target.TintColor.Value = FilterColor(colorX.Green);
			}
			if (!(_timerStart.CurrentTime > (double)TimerSeconds.Value) || _timerPhotoTaken)
			{
				return;
			}
			_timerPhotoTaken = true;
			TakePhoto(base.LocalUserSpace, TimerResolution, addTemporaryHolder: false);
			RunInSeconds(1f, delegate
			{
				_previewRoot.Target.Scale_Field.TweenTo(float3.Zero, 0.5f);
				_fingerGestureCharge = 0f;
				RunInSeconds(1f, delegate
				{
					_previewRoot.Target.LocalScale = float3.One;
					Slot target7 = _root.Target;
					target7.SetParent(_originalParent, keepGlobalTransform: false);
					target7.LocalPosition = _originalPosition;
					target7.LocalRotation = _originalRotation;
					target7.LocalScale = _originalScale;
					_timerActive.Value = false;
				});
			});
			return;
		}
		_timerTextRoot.Target.ActiveSelf = false;
		Slot target2 = _previewRoot.Target;
		Slot target3 = _timerRoot.Target;
		Camera target4 = _camera.Target;
		UnlitMaterial target5 = _frameMaterial.Target;
		float3 localPosition = target2.LocalPosition;
		float num3 = _lastGestureSize;
		float3 forward = target2.LocalRotation * float3.Forward;
		float value = base.World.LocalUserDesktopFOV;
		float num4 = 0f;
		bool flag = false;
		bool flag2 = false;
		bool flag3 = _inputs.TakePhoto.AnyState || _inputs.StartTimerPhoto.AnyState;
		if (flag3 || _bindingCaptureCharge > 0f)
		{
			_bindingCaptureCharge = MathX.Progress01(_bindingCaptureCharge, base.Time.Delta * (flag3 ? 5f : 2.5f), flag3);
			num4 = _bindingCaptureCharge;
			num3 = 0.25f;
			float3 globalPoint = base.World.LocalUserViewPosition;
			float3 globalDirection = base.World.LocalUserViewRotation * float3.Forward;
			globalPoint = base.Slot.GlobalPointToLocal(in globalPoint);
			globalDirection = base.Slot.GlobalDirectionToLocal(in globalDirection);
			localPosition = globalPoint + globalDirection * 0.2f;
			forward = globalDirection;
			flag = _inputs.TakePhoto.Released && _inputs.TakePhoto.HasValue;
			flag2 = _inputs.StartTimerPhoto.Released && _inputs.StartTimerPhoto.HasValue;
			if (flag || flag2)
			{
				num4 = 1f;
			}
			if (flag2)
			{
				_bindingCaptureCharge = 0f;
			}
			target3.ActiveSelf = false;
		}
		else
		{
			List<Hand> list = Pool.BorrowList<Hand>();
			base.InputInterface.GetDevices(list);
			Hand hand = null;
			Hand hand2 = null;
			if (FingerGestureEnabled.Value)
			{
				foreach (Hand item in list)
				{
					if (item.IsTracking)
					{
						if (item.Chirality == Chirality.Left && (hand == null || hand.Wrist.Priority < item.Wrist.Priority))
						{
							hand = item;
						}
						if (item.Chirality == Chirality.Right && (hand2 == null || hand2.Wrist.Priority < item.Wrist.Priority))
						{
							hand2 = item;
						}
					}
				}
				Pool.Return(ref list);
			}
			GestureData? gestureData = GetFingerGestureData(hand, hand2);
			if (gestureData.HasValue && _fingerGestureCharge < 1f && (gestureData.Value.timerTrigger > 0.75f || gestureData.Value.takeTrigger > 0.75f))
			{
				gestureData = null;
			}
			_fingerGestureCharge = MathX.Progress01(_fingerGestureCharge, base.Time.Delta * 4f, gestureData.HasValue);
			if (gestureData.HasValue)
			{
				GestureData value2 = gestureData.Value;
				localPosition = value2.center;
				num3 = value2.size;
				forward = value2.direction;
				flag2 = DetectTrigger(value2.timerTrigger, ref _lastTimerTrigger);
				flag = DetectTrigger(value2.takeTrigger, ref _lastTakeTrigger);
				float lerp = MathX.InverseLerp(MinDistance, MaxDistance, value2.distance);
				value = MathX.Lerp(MaxFOV, MinFOV, lerp);
				target3.LocalPosition = MathX.Lerp(in value2.leftCorner, in value2.center, 0.15f) - value2.direction * 0.05f;
				target3.ActiveSelf = true;
			}
			else
			{
				target3.ActiveSelf = false;
			}
			num4 = _fingerGestureCharge;
		}
		Slot target6 = _root.Target;
		if (num4 > 0f)
		{
			_lastGestureSize = num3;
			float num5 = MathX.SmootherStep(num4);
			target6.ActiveSelf = true;
			target2.LocalScale = float3.One * num5;
			target3.LocalScale = target2.LocalScale;
			bool flag4 = num4 >= 1f;
			target5.TintColor.Value = FilterColor(flag4 ? colorX.Cyan : colorX.Red);
			target2.LocalPosition = localPosition;
			target2.LocalRotation = floatQ.LookRotation(in forward);
			_quad.Target.Size.Value = new float2(16f, 9f).Normalized * num3;
			_frame.Target.ContentSize.Value = _quad.Target.Size;
			target4.FieldOfView.Value = value;
			if (flag4)
			{
				if (flag)
				{
					TakePhoto(base.LocalUserRoot.Slot, NormalResolution, addTemporaryHolder: true);
				}
				else if (flag2)
				{
					PlayTimerStartSound();
					_timerPhotoTaken = false;
					_timerStart.SetNow();
					_timerActive.Value = true;
					_originalParent.Target = target6.Parent;
					_originalPosition.Value = target6.LocalPosition;
					_originalRotation.Value = target6.LocalRotation;
					_originalScale.Value = target6.LocalScale;
					target6.Parent = base.LocalUserSpace;
					target2.Scale_Field.TweenTo(target2.LocalScale * 5f, 1f);
					target3.Scale_Field.TweenTo(float3.Zero, 0.5f);
				}
			}
		}
		else
		{
			target6.ActiveSelf = false;
		}
	}

	private bool DetectTrigger(float trigger, ref bool lastState)
	{
		if (lastState)
		{
			if (trigger < 0.4f)
			{
				lastState = false;
			}
			return false;
		}
		if (trigger > 0.75f)
		{
			lastState = true;
			return true;
		}
		return false;
	}

	public void PlayCaptureSound()
	{
		if (_shutterClip.IsAssetAvailable)
		{
			_previewRoot.Target.PlayOneShot(_shutterClip.Target).SetupAsUI();
		}
		_timerCountdownSlowPlayer.Target?.Stop();
		_timerCountdownFastPlayer.Target?.Stop();
	}

	public void PlayTimerStartSound()
	{
		timerTransitioned = false;
		if (_timerStartClip.IsAssetAvailable)
		{
			_previewRoot.Target.PlayOneShot(_timerStartClip.Target).SetupAsUI();
		}
		_timerCountdownSlowPlayer.Target?.Play();
		_timerCountdownFastPlayer.Target?.Play();
		if (_timerCountdownSlowOutput.Target != null)
		{
			_timerCountdownSlowOutput.Target.Volume.ClearTweens();
			_timerCountdownSlowOutput.Target.Volume.Value = 1f;
		}
		if (_timerCountdownFastOutput.Target != null)
		{
			_timerCountdownFastOutput.Target.Volume.ClearTweens();
			_timerCountdownFastOutput.Target.Volume.Value = 0f;
		}
	}

	public void TransitionToFastTimerSound()
	{
		timerTransitioned = true;
		_timerCountdownSlowOutput.Target?.Volume.TweenTo(0f, 0.2f);
		_timerCountdownFastOutput.Target?.Volume.TweenTo(1f, 0.2f);
	}

	private void TakePhoto(Slot rootSpace, int2 resolution, bool addTemporaryHolder)
	{
		_flash = 1f;
		PlayCaptureSound();
		Sync<float> fov = _camera.Target.FieldOfView;
		float2 value = _quad.Target.Size.Value;
		float3 position = _previewRoot.Target.GlobalPosition;
		floatQ rotation = _previewRoot.Target.GlobalRotation;
		float3 scale = _previewRoot.Target.GlobalScale * (value.x / value.Normalized.x);
		position = rootSpace.GlobalPointToLocal(in position);
		rotation = rootSpace.GlobalRotationToLocal(in rotation);
		scale = rootSpace.GlobalScaleToLocal(in scale);
		RenderTask leftRenderSettings = _camera.Target.GetRenderSettings(resolution);
		RenderTask rightRenderSettings = _camera.Target.GetRenderSettings(resolution);
		leftRenderSettings.parameters.renderPrivateUI = capturePrivateUI;
		rightRenderSettings.parameters.renderPrivateUI = capturePrivateUI;
		GetCaptureCameraPosition(out var pos, out var rot);
		Slot parent = _camera.Target.Slot.Parent;
		if (CaptureStereo.Value)
		{
			float3 position2 = parent.LocalPointToGlobal(pos + float3.Left * StereoSeparation.Value * 0.5f);
			float3 position3 = parent.LocalPointToGlobal(pos + float3.Right * StereoSeparation.Value * 0.5f);
			rot = parent.LocalRotationToGlobal(in rot);
			leftRenderSettings.position = position2;
			leftRenderSettings.rotation = rot;
			rightRenderSettings.position = position3;
			rightRenderSettings.rotation = rot;
		}
		else
		{
			pos = parent.LocalPointToGlobal(in pos);
			rot = parent.LocalRotationToGlobal(in rot);
			leftRenderSettings.position = pos;
			leftRenderSettings.rotation = rot;
		}
		StartTask(async delegate
		{
			if (leftRenderSettings.excludeObjects == null)
			{
				leftRenderSettings.excludeObjects = new List<Slot>();
			}
			InteractionHandler.GetLaserRoots(base.World.AllUsers, leftRenderSettings.excludeObjects);
			if (HideAllNameplates.Value)
			{
				AvatarManager.CollectAllBadgeRoots(base.World.AllUsers, leftRenderSettings.excludeObjects);
			}
			foreach (User allUser in base.World.AllUsers)
			{
				if (allUser.HideInScreenshots && allUser.Root != null)
				{
					leftRenderSettings.excludeObjects.Add(allUser.Root.Slot);
				}
			}
			foreach (Slot child in rootSpace.Children)
			{
				if (child.Name == "PhotoTempHolder")
				{
					leftRenderSettings.excludeObjects.Add(child);
				}
			}
			StereoLayout layout = StereoLayout.None;
			Uri uri;
			if (CaptureStereo.Value)
			{
				rightRenderSettings.excludeObjects = leftRenderSettings.excludeObjects;
				Task<Bitmap2D> task = base.World.Render.RenderToBitmap(leftRenderSettings);
				Task<Bitmap2D> rightTask = base.World.Render.RenderToBitmap(rightRenderSettings);
				Bitmap2D leftTex = await task.ConfigureAwait(continueOnCapturedContext: false);
				Bitmap2D rightTex = await rightTask.ConfigureAwait(continueOnCapturedContext: false);
				await default(ToBackground);
				Bitmap2D bitmap2D = new Bitmap2D(resolution.x * 2, resolution.y, leftTex.Format, mipmaps: false, ColorProfile.sRGB);
				bitmap2D.CopyFrom(leftTex, 0, 0, 0, 0, resolution.x, resolution.y);
				bitmap2D.CopyFrom(rightTex, 0, 0, resolution.x, 0, resolution.x, resolution.y);
				uri = await base.Engine.LocalDB.SaveAssetAsync(bitmap2D, EncodeFormat.Value.ToExtension()).ConfigureAwait(continueOnCapturedContext: false);
				await default(ToWorld);
				layout = StereoLayout.Horizontal_LR;
			}
			else
			{
				uri = await base.World.Render.RenderToAsset(leftRenderSettings, EncodeFormat.Value.ToExtension());
			}
			bool canSpawn = base.World.CanSpawnObjects();
			if (addTemporaryHolder && canSpawn)
			{
				rootSpace = rootSpace.AddSlot("PhotoTempHolder");
				rootSpace.PersistentSelf = false;
				rootSpace.AttachComponent<DestroyWithoutChildren>();
			}
			Slot s;
			if (canSpawn)
			{
				s = rootSpace.AddSlot("Photo");
			}
			else
			{
				s = base.LocalUserRoot.Slot.AddLocalSlot("Photo", persistent: true);
				s.LocalPosition = new float3(0f, -10000f);
			}
			StaticTexture2D staticTexture2D = s.AttachTexture(uri, getExisting: true, uncompressed: false, directLoad: false, evenNull: false, TextureWrapMode.Clamp);
			ImageImporter.SetupTextureProxyComponents(s, staticTexture2D, layout, ImageProjection.Perspective, setupPhotoMetadata: true);
			PhotoMetadata componentInChildren = s.GetComponentInChildren<PhotoMetadata>();
			componentInChildren.CameraManufacturer.Value = base.Engine.Cloud.Platform.Name;
			componentInChildren.CameraModel.Value = GetType().Name;
			componentInChildren.CameraFOV.Value = fov;
			componentInChildren.StereoLayout.Value = layout;
			s.AttachComponent<Grabbable>().Scalable.Value = true;
			AttachedModel<QuadMesh, UnlitMaterial> attachedModel = s.AttachMesh<QuadMesh, UnlitMaterial>();
			attachedModel.material.Texture.Target = staticTexture2D;
			attachedModel.material.Sidedness.Value = Sidedness.Double;
			ImageImporter.SetupStereoLayout(attachedModel.material, layout);
			TextureSizeDriver textureSizeDriver = s.AttachComponent<TextureSizeDriver>();
			textureSizeDriver.Texture.Target = staticTexture2D;
			textureSizeDriver.DriveMode.Value = TextureSizeDriver.Mode.Normalized;
			textureSizeDriver.Target.Target = attachedModel.mesh.Size;
			switch (layout)
			{
			case StereoLayout.Horizontal_LR:
			case StereoLayout.Horizontal_RL:
				textureSizeDriver.Ratio.Value = new float2(0.5f, 1f);
				break;
			case StereoLayout.Vertical_LR:
			case StereoLayout.Vertical_RL:
				textureSizeDriver.Ratio.Value = new float2(1f, 0.5f);
				break;
			}
			if (canSpawn)
			{
				s.LocalPosition = position;
				s.LocalRotation = rotation;
				s.LocalScale = scale;
			}
			BoxCollider boxCollider = s.AttachComponent<BoxCollider>();
			boxCollider.Size.DriveFromXY(attachedModel.mesh.Size);
			boxCollider.Type.Value = ColliderType.NoCollision;
			await componentInChildren.NotifyOfScreenshot();
			if (!canSpawn)
			{
				s.Destroy();
			}
		});
	}

	private float3 GetDirection(Finger finger)
	{
		float3 v = ((finger.FingerType != FingerType.Thumb) ? (finger.Proximal.Rotation * float3.Forward) : (finger.Proximal.Rotation * float3.Forward));
		return finger.Hand.Wrist.Rotation * v;
	}

	private float3 GetPosition(Finger finger)
	{
		float3 v = finger.Proximal.Position;
		return finger.Hand.Wrist.Rotation * v + finger.Hand.Wrist.Position;
	}

	private GestureData? GetFingerGestureData(Hand left, Hand right)
	{
		if (left == null || right == null)
		{
			return null;
		}
		float3 a = GetDirection(left.Index);
		float3 b = GetDirection(left.Thumb);
		float3 a2 = GetDirection(right.Index);
		float3 b2 = GetDirection(right.Thumb);
		float num = MathX.Angle(in a, in b);
		float num2 = MathX.Angle(in a2, in b2);
		if (num < 30f || num2 < 30f)
		{
			if ((bool)DebugGesture)
			{
				base.Debug.Text(base.LocalUserRoot.LeftControllerPosition, num.ToString("F0"), colorX.Red);
				base.Debug.Text(base.LocalUserRoot.RightControllerPosition, num2.ToString("F0"), colorX.Red);
			}
			_indexPerpendicularCharge = 0f;
			return null;
		}
		float num3 = MathX.Angle(in a, in a2);
		if ((bool)DebugGesture)
		{
			base.Debug.Text((base.LocalUserRoot.LeftControllerPosition + base.LocalUserRoot.RightControllerPosition) * 0.5f, num3.ToString("F0"));
		}
		if (MathX.Abs(MathX.DeltaAngle(num3, 90f)) > 8f)
		{
			_indexPerpendicularCharge = MathX.Progress01(_indexPerpendicularCharge, base.Time.Delta * -2f);
			if (_indexPerpendicularCharge <= 0f)
			{
				return null;
			}
		}
		else if (_fingerGestureCharge >= 1f)
		{
			_indexPerpendicularCharge = MathX.Progress01(_indexPerpendicularCharge, base.Time.Delta * 2f);
		}
		float3 a3 = GetDirection(left.Middle);
		float3 a4 = GetDirection(right.Middle);
		ITrackedDevice bodyNode = base.InputInterface.GetBodyNode(BodyNode.Head);
		float3 b3 = bodyNode.Rotation * float3.Forward;
		float3 b4 = bodyNode.Rotation * float3.Up;
		float3 b5 = bodyNode.Rotation * float3.Down;
		if (_fingerGestureCharge < 1f)
		{
			float num4 = MathX.Angle(in a, in b4);
			float num5 = MathX.Angle(in a2, in b4);
			MathX.Angle(in b, in b5);
			MathX.Angle(in b2, in b5);
			if (num4 > 25f && num5 > 25f)
			{
				if ((bool)DebugGesture)
				{
					base.Debug.Text(base.LocalUserRoot.LeftControllerPosition, num4.ToString("F0"), colorX.Orange);
					base.Debug.Text(base.LocalUserRoot.RightControllerPosition, num5.ToString("F0"), colorX.Orange);
				}
				return null;
			}
		}
		float num6 = MathX.Angle(in a3, in b3);
		float num7 = MathX.Angle(in a4, in b3);
		if (num6 > 30f || num7 > 30f)
		{
			if ((bool)DebugGesture)
			{
				base.Debug.Text(base.LocalUserRoot.LeftControllerPosition, num6.ToString("F0"), colorX.Yellow);
				base.Debug.Text(base.LocalUserRoot.RightControllerPosition, num7.ToString("F0"), colorX.Yellow);
			}
			return null;
		}
		float3 position = GetPosition(left.Index);
		float3 position2 = GetPosition(right.Index);
		float3 a5 = position;
		float3 b6 = position2;
		GestureData value = default(GestureData);
		float3 v = (value.direction = (left.Wrist.Rotation * float3.Down + right.Wrist.Rotation * float3.Down).Normalized);
		value.center = (a5 + b6) * 0.5f + v * 0.1f;
		value.size = MathX.Distance(in a5, in b6) * 0.75f;
		value.distance = MathX.Distance(bodyNode.Position, in value.center);
		value.timerTrigger = MathX.Dot(in a, in a3);
		value.takeTrigger = MathX.Dot(in a2, in a4);
		value.leftCorner = a5;
		value.rightCorner = b6;
		return value;
	}

	protected override void OnDestroy()
	{
		_root.Target?.Destroy();
		base.OnDestroy();
	}

	protected override void OnDispose()
	{
		base.OnDispose();
		if (settingsRegistered)
		{
			Settings.UnregisterValueChanges<PhotoCaptureSettings>(OnCaptureSettingsChanged);
		}
	}

	protected override void InitializeSyncMembers()
	{
		base.InitializeSyncMembers();
		FingerGestureEnabled = new Sync<bool>();
		MinDistance = new Sync<float>();
		MaxDistance = new Sync<float>();
		MinFOV = new Sync<float>();
		MaxFOV = new Sync<float>();
		PreviewResolution = new Sync<int2>();
		NormalResolution = new Sync<int2>();
		TimerResolution = new Sync<int2>();
		CaptureStereo = new Sync<bool>();
		StereoSeparation = new Sync<float>();
		TimerSeconds = new Sync<float>();
		HideAllNameplates = new Sync<bool>();
		EncodeFormat = new Sync<PhotoEncodeFormat>();
		DebugGesture = new Sync<bool>();
		_timerStart = new SyncTime();
		_timerActive = new Sync<bool>();
		_originalParent = new SyncRef<Slot>();
		_originalPosition = new Sync<float3>();
		_originalRotation = new Sync<floatQ>();
		_originalScale = new Sync<float3>();
		_root = new SyncRef<Slot>();
		_previewRoot = new SyncRef<Slot>();
		_renderTex = new SyncRef<RenderTextureProvider>();
		_quad = new SyncRef<QuadMesh>();
		_frame = new SyncRef<FrameMesh>();
		_cameraRoot = new SyncRef<Slot>();
		_cameraPos = new FieldDrive<float3>();
		_cameraRot = new FieldDrive<floatQ>();
		_camera = new SyncRef<Camera>();
		_frameMaterial = new SyncRef<UnlitMaterial>();
		_timerTextRoot = new SyncRef<Slot>();
		_timerText = new SyncRef<TextRenderer>();
		_shutterClip = new AssetRef<AudioClip>();
		_timerStartClip = new AssetRef<AudioClip>();
		_timerCountdownSlowPlayer = new SyncRef<AudioClipPlayer>();
		_timerCountdownFastPlayer = new SyncRef<AudioClipPlayer>();
		_timerCountdownSlowOutput = new SyncRef<AudioOutput>();
		_timerCountdownFastOutput = new SyncRef<AudioOutput>();
		_timerRoot = new SyncRef<Slot>();
	}

	public override ISyncMember GetSyncMember(int index)
	{
		return index switch
		{
			0 => persistent, 
			1 => updateOrder, 
			2 => EnabledField, 
			3 => FingerGestureEnabled, 
			4 => MinDistance, 
			5 => MaxDistance, 
			6 => MinFOV, 
			7 => MaxFOV, 
			8 => PreviewResolution, 
			9 => NormalResolution, 
			10 => TimerResolution, 
			11 => CaptureStereo, 
			12 => StereoSeparation, 
			13 => TimerSeconds, 
			14 => HideAllNameplates, 
			15 => EncodeFormat, 
			16 => DebugGesture, 
			17 => _timerStart, 
			18 => _timerActive, 
			19 => _originalParent, 
			20 => _originalPosition, 
			21 => _originalRotation, 
			22 => _originalScale, 
			23 => _root, 
			24 => _previewRoot, 
			25 => _renderTex, 
			26 => _quad, 
			27 => _frame, 
			28 => _cameraRoot, 
			29 => _cameraPos, 
			30 => _cameraRot, 
			31 => _camera, 
			32 => _frameMaterial, 
			33 => _timerTextRoot, 
			34 => _timerText, 
			35 => _shutterClip, 
			36 => _timerStartClip, 
			37 => _timerCountdownSlowPlayer, 
			38 => _timerCountdownFastPlayer, 
			39 => _timerCountdownSlowOutput, 
			40 => _timerCountdownFastOutput, 
			41 => _timerRoot, 
			_ => throw new ArgumentOutOfRangeException(), 
		};
	}

	public static PhotoCaptureManager __New()
	{
		return new PhotoCaptureManager();
	}
}
