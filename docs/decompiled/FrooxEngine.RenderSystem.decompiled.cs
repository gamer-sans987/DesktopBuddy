using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Elements.Assets;
using Elements.Core;
using Renderite.Shared;

namespace FrooxEngine;

public class RenderSystem : IBackingBufferAllocator
{
	private const int ENGINE_READY_FRAMES = 120;

	public bool DebugFramePacing;

	public bool DebugLogNextFrame;

	private SharedMemoryManager _sharedMemory;

	private ManualResetEvent _frameStartEvent;

	private FrameStartData _frameStartData;

	private TaskCompletionSource<HeadOutputDevice> _actualHeadOutputDevice;

	private RenderiteMessagingHost _messagingHost;

	private BootstrapperManager _bootstrapper;

	private HashSet<TextureFormat> _supportedFormats;

	private bool _initDone;

	private bool _resolutionSubmitWaiting;

	private bool? _requestedFullScreenMode;

	private int2? _requestedResolution;

	private Stopwatch _frameStartWaitTimer;

	private Stopwatch _frameTimer;

	private Bitmap2D _customSplashScreen;

	private List<CameraRenderTask> _renderTasks = new List<CameraRenderTask>();

	private int _setIconRequest;

	private Dictionary<int, TaskCompletionSource<bool>> _setWindowRequests = new Dictionary<int, TaskCompletionSource<bool>>();

	private TaskbarProgressBarMode _submittedTaskbarProgressMode;

	private ulong _submittedTaskbarCompleted;

	private ulong _submittedTaskbarTotal;

	public string RendererName { get; private set; }

	public nint RendererWindowHandle { get; private set; }

	public RendererState State { get; private set; }

	public bool HasRenderer { get; private set; }

	public ColorProfile MaterialColorProfile => ColorProfile.sRGB;

	public bool IsGPUTexturePOTByteAligned { get; private set; }

	public int MaxTextureSize { get; private set; }

	public Engine Engine { get; private set; }

	public string RendererPath { get; private set; }

	public Process? RendererProcess { get; private set; }

	public FrameFinalizeHandler Finalizer { get; private set; }

	public ReflectionProbeRenderTaskManager ReflectionProbeRenderManager { get; private set; }

	public RenderAssetManager<Texture2D> Texture2Ds { get; private set; }

	public RenderAssetManager<Texture3D> Texture3Ds { get; private set; }

	public RenderAssetManager<Cubemap> Cubemaps { get; private set; }

	public RenderAssetManager<RenderTexture> RenderTextures { get; private set; }

	public RenderAssetManager<VideoTexture> VideoTextures { get; private set; }

	public RenderAssetManager<DesktopTexture> DesktopTextures { get; private set; }

	public RenderAssetManager<Mesh> Meshes { get; private set; }

	public RenderAssetManager<Shader> Shaders { get; private set; }

	public RenderMaterialManager Materials { get; private set; }

	public RenderAssetManager<PointRenderBuffer> PointBuffers { get; private set; }

	public RenderAssetManager<TrailsRenderBuffer> TrailBuffers { get; private set; }

	public RenderAssetManager<GaussianSplat> GaussianSplats { get; private set; }

	public GlobalRenderableManager<LocalLightsBufferRenderer> LightBuffers { get; private set; }

	public int FrameIndex { get; private set; }

	public float LastFrameGenerationTime { get; private set; }

	public float FrameStartWaitTime { get; private set; }

	public int LastFrameStartMessageBytes { get; private set; }

	public int SentPrimaryMessages => _messagingHost.SentPrimaryMessages;

	public int ReceivedPrimaryMessages => _messagingHost.SentPrimaryMessages;

	public int SentBackgroundMessages => _messagingHost.SentBackgroundMessages;

	public int ReceivedBackgroundMessages => _messagingHost.ReceivedBackgroundMessages;

	public int SharedMemoryManagers => _sharedMemory?.ManagerCount ?? 0;

	public int SharedMemoryManagersInUse => _sharedMemory?.UsedManagerCount ?? 0;

	public long SharedMemoryUsedBytes => _sharedMemory?.TotalUsedBytes ?? 0;

	public long SharedMemoryFreeBytes => _sharedMemory?.TotalFreeBytes ?? 0;

	public long SharedMemoryCapacityBytes => _sharedMemory?.TotalCapacity ?? 0;

	public bool SharedMemoryPruning => _sharedMemory?.ShouldPrune ?? false;

	public TaskbarProgressBarMode TaskbarProgressMode { get; set; }

	public ulong TaskbarCompleted { get; set; }

	public ulong TaskbarTotal { get; set; }

	private async Task<Process> StartRenderer()
	{
		UniLog.Log("Starting renderer process");
		string text = $"-{"QueueName"} {_messagingHost.QueueName} -{"QueueCapacity"} {_messagingHost.QueueCapacity}";
		if (_bootstrapper != null)
		{
			return await _bootstrapper.StartRenderer(text);
		}
		return Process.Start(new ProcessStartInfo(RendererPath, text)
		{
			UseShellExecute = false,
			WorkingDirectory = "Renderer"
		});
	}

	public async Task<IEngineInitProgress> Initialize(Engine engine, HeadOutputDevice outputDevice, Guid uniqueSessionId, bool useRenderer, Bitmap2D rendererIcon, SplashScreenDescriptor splashScreen, IEngineInitProgress initProgress)
	{
		Engine = engine;
		if (!useRenderer)
		{
			return initProgress;
		}
		HasRenderer = true;
		RendererPath = Path.Combine(engine.AppPath, "Renderer", "Renderite.Renderer.exe");
		_messagingHost = new RenderiteMessagingHost(HandleCommand, HandleFailure);
		if (engine.IsRunningWithBootstrapper)
		{
			_bootstrapper = new BootstrapperManager(engine);
		}
		State = RendererState.StartingUp;
		RendererProcess = await StartRenderer();
		if (RendererProcess == null)
		{
			throw new Exception("Renderer process failed to start");
		}
		UniLog.Log($"Renderer process started: {RendererProcess.ProcessName} ({RendererProcess.Id})");
		_sharedMemory = new SharedMemoryManager(engine.SharedMemoryPrefix + "_" + CryptoHelper.GenerateCryptoToken(32), MemoryBufferFreed);
		Finalizer = new FrameFinalizeHandler();
		ReflectionProbeRenderManager = new ReflectionProbeRenderTaskManager();
		Texture2Ds = new RenderAssetManager<Texture2D>();
		Texture3Ds = new RenderAssetManager<Texture3D>();
		Cubemaps = new RenderAssetManager<Cubemap>();
		RenderTextures = new RenderAssetManager<RenderTexture>();
		VideoTextures = new RenderAssetManager<VideoTexture>();
		DesktopTextures = new RenderAssetManager<DesktopTexture>();
		Meshes = new RenderAssetManager<Mesh>();
		Shaders = new RenderAssetManager<Shader>();
		Materials = new RenderMaterialManager(this);
		PointBuffers = new RenderAssetManager<PointRenderBuffer>();
		TrailBuffers = new RenderAssetManager<TrailsRenderBuffer>();
		GaussianSplats = new RenderAssetManager<GaussianSplat>();
		LightBuffers = new GlobalRenderableManager<LocalLightsBufferRenderer>();
		RendererInitData initData = new RendererInitData
		{
			sharedMemoryPrefix = _sharedMemory.InstancePrefix,
			uniqueSessionId = uniqueSessionId,
			mainProcessId = Environment.ProcessId,
			debugFramePacing = DebugFramePacing,
			outputDevice = outputDevice,
			windowTitle = engine.PlatformProfile.Name
		};
		if (rendererIcon != null)
		{
			SetWindowIcon(rendererIcon, delegate(SetWindowIcon setIcon)
			{
				initData.setWindowIcon = setIcon;
			});
		}
		if (splashScreen != null)
		{
			_customSplashScreen = splashScreen.Texture.ConvertTo(TextureFormat.BGRA32, this);
			initData.splashScreenOverride = new RendererSplashScreenOverride();
			initData.splashScreenOverride.textureSize = _customSplashScreen.Size;
			initData.splashScreenOverride.textureRelativeScreenSize = splashScreen.SplashScreenRelativeSize;
			initData.splashScreenOverride.loadingBarOffset = splashScreen.LoadingBarOffset;
			initData.splashScreenOverride.textureData = ((SharedMemoryBlockLease<byte>)_customSplashScreen.Buffer).Descriptor;
		}
		_messagingHost.SendCommand(initData, isBackground: false);
		initProgress = new RendererInitProgressWrapper(initProgress, this);
		_frameStartEvent = new ManualResetEvent(initialState: false);
		_actualHeadOutputDevice = new TaskCompletionSource<HeadOutputDevice>();
		_frameTimer = new Stopwatch();
		_frameStartWaitTimer = new Stopwatch();
		if (RendererProcess != null)
		{
			Task.Run((Func<Task?>)RendererWatchdog);
		}
		return initProgress;
	}

	public async Task FinishInitialize()
	{
		if (HasRenderer)
		{
			Settings.RegisterValueChanges<PostProcessingSettings>(OnPostProcessingChanged);
			Settings.RegisterValueChanges<RenderingQualitySettings>(OnRenderingQuailtySettingsChanged);
			Settings.RegisterValueChanges<ResolutionSettings>(OnResolutionSettingsChanged);
			Settings.RegisterValueChanges<DesktopRenderSettings>(OnDesktopRenderSettingsChanged);
			Settings.RegisterValueChanges<GaussianSplatQualitySettings>(OnGaussianSplatQualityChanged);
			Settings.RegisterValueChanges<RendererDecouplingSettings>(OnDecouplingSettingsChanged);
		}
	}

	public void Update()
	{
		if (HasRenderer)
		{
			_sharedMemory.Update();
		}
	}

	private void OnPostProcessingChanged(PostProcessingSettings settings)
	{
		PostProcessingConfig command = new PostProcessingConfig
		{
			motionBlurIntensity = settings.MotionBlurIntensity,
			bloomIntensity = settings.BloomIntensity,
			ambientOcclusionIntensity = settings.AmbientOcclusionIntensity,
			screenSpaceReflections = settings.ScreenSpaceReflections,
			antialiasing = settings.Antialiasing
		};
		_messagingHost.SendCommand(command, isBackground: false);
	}

	private void OnRenderingQuailtySettingsChanged(RenderingQualitySettings settings)
	{
		QualityConfig command = new QualityConfig
		{
			perPixelLights = settings.PerPixelLights,
			shadowCascades = settings.ShadowCascades,
			shadowResolution = settings.ShadowResolution,
			shadowDistance = settings.ShadowDistance,
			skinWeightMode = settings.SkinWeightMode
		};
		_messagingHost.SendCommand(command, isBackground: true);
	}

	private void OnDecouplingSettingsChanged(RendererDecouplingSettings settings)
	{
		RenderDecouplingConfig renderDecouplingConfig = new RenderDecouplingConfig();
		if (settings.ForceDecouple.Value)
		{
			renderDecouplingConfig.decoupleActivateInterval = 0f;
			renderDecouplingConfig.recoupleFrameCount = int.MaxValue;
		}
		else
		{
			renderDecouplingConfig.decoupleActivateInterval = 1f / MathX.Max(0f, settings.ActivationFramerate.Value);
			renderDecouplingConfig.recoupleFrameCount = settings.DeactivationFrames;
		}
		renderDecouplingConfig.decoupledMaxAssetProcessingTime = (float)settings.AssetProcessingMaxTimeMilliseconds * 0.001f;
		_messagingHost.SendCommand(renderDecouplingConfig, isBackground: true);
	}

	private void OnGaussianSplatQualityChanged(GaussianSplatQualitySettings settings)
	{
		GaussianSplatConfig gaussianSplatConfig = new GaussianSplatConfig();
		gaussianSplatConfig.sortingMegaOperationsPerCamera = settings.SortMegaOperationsPerCamera;
		_messagingHost.SendCommand(gaussianSplatConfig, isBackground: true);
	}

	private void OnResolutionSettingsChanged(ResolutionSettings settings)
	{
		int2 currentResolution = Engine.InputInterface.WindowResolution;
		if (!settings.IsCommitedResolutionValid)
		{
			Settings.UpdateActiveSetting(delegate(ResolutionSettings s)
			{
				s.Fullscreen.Value = Engine.InputInterface.IsFullscreen;
				_requestedFullScreenMode = s.Fullscreen.Value;
				_requestedResolution = currentResolution;
				if (!ResolutionSettings.IsResolutionValid(s.CommitedWindowResolution.Value))
				{
					s.WindowResolution.Value = currentResolution;
					s.CommitedWindowResolution.Value = currentResolution;
				}
				if (!ResolutionSettings.IsResolutionValid(s.CommitedFullscreenResolution.Value))
				{
					s.FullscreenResolution.Value = currentResolution;
					s.CommitedFullscreenResolution.Value = currentResolution;
				}
			});
		}
		else
		{
			_requestedFullScreenMode = settings.Fullscreen.Value;
			_requestedResolution = settings.CurrentCommitedResolution;
			ResolutionConfig command = new ResolutionConfig
			{
				resolution = settings.CurrentCommitedResolution,
				fullscreen = settings.Fullscreen
			};
			_resolutionSubmitWaiting = true;
			_messagingHost.SendCommand(command, isBackground: true);
		}
	}

	private void OnDesktopRenderSettingsChanged(DesktopRenderSettings settings)
	{
		DesktopConfig desktopConfig = new DesktopConfig();
		desktopConfig.maximumBackgroundFramerate = (settings.BackgroundFramerateEnabled.Value ? new int?(settings.MaximumBackgroundFramerate.Value) : ((int?)null));
		desktopConfig.vSync = settings.VSync;
		_messagingHost.SendCommand(desktopConfig, isBackground: true);
	}

	public async Task<HeadOutputDevice?> WaitForRendererInit()
	{
		if (_actualHeadOutputDevice == null)
		{
			return null;
		}
		return await _actualHeadOutputDevice.Task;
	}

	public FrameStartData WaitForFrameBegin()
	{
		if (Engine.ShutdownRequested)
		{
			return null;
		}
		if (_frameStartEvent == null)
		{
			return null;
		}
		UpdateResolution();
		UpdateTaskbar();
		_frameStartWaitTimer.Restart();
		_frameStartEvent.WaitOne();
		_frameStartEvent.Reset();
		_frameStartWaitTimer.Stop();
		FrameStartWaitTime = (float)_frameStartWaitTimer.Elapsed.TotalSeconds;
		if (Engine.ShutdownRequested)
		{
			return null;
		}
		_frameTimer.Restart();
		FrameStartData frameStartData = _frameStartData;
		_frameStartData = null;
		Finalizer.FrameFinalized();
		return frameStartData;
	}

	private void UpdateResolution()
	{
		if (_resolutionSubmitWaiting)
		{
			return;
		}
		if (_requestedFullScreenMode.HasValue && Engine.InputInterface.IsFullscreen != _requestedFullScreenMode.Value)
		{
			_requestedFullScreenMode = Engine.InputInterface.IsFullscreen;
			Settings.UpdateActiveSetting(delegate(ResolutionSettings s)
			{
				s.Fullscreen.Value = _requestedFullScreenMode.Value;
			});
		}
		if (!_requestedResolution.HasValue)
		{
			return;
		}
		int2 currentResolution = Engine.InputInterface.WindowResolution;
		if (currentResolution != _requestedResolution.Value)
		{
			_requestedResolution = currentResolution;
			Settings.UpdateActiveSetting(delegate(ResolutionSettings s)
			{
				s.CurrentTargetResolution = currentResolution;
				s.CurrentCommitedResolution = currentResolution;
			});
		}
	}

	internal void ResolutionSettingsConsumed()
	{
		_resolutionSubmitWaiting = false;
	}

	public void SubmitFrame()
	{
		if (State != RendererState.Rendering)
		{
			return;
		}
		FrameSubmitData frameSubmitData = new FrameSubmitData();
		frameSubmitData.frameIndex = FrameIndex;
		frameSubmitData.vrActive = Engine.InputInterface.VR_Active;
		frameSubmitData.debugLog = DebugLogNextFrame;
		DebugLogNextFrame = false;
		frameSubmitData.outputState = Engine.InputInterface.CollectOutputState();
		foreach (World world in Engine.WorldManager.Worlds)
		{
			if (!world.IsDestroyed && !world.IsDisposed && world.Render.IsGeneratingRenderUpdates)
			{
				frameSubmitData.renderSpaces.Add(world.Render.CollectRenderUpdate(_renderTasks));
				if (world.Focus == World.WorldFocus.Focused)
				{
					frameSubmitData.desktopFOV = world.LocalUserDesktopFOV;
					frameSubmitData.nearClip = world.LocalUserRenderSettings?.NearClip ?? 0.005f;
					frameSubmitData.farClip = world.LocalUserRenderSettings?.FarClip ?? 4096f;
				}
			}
		}
		if (_renderTasks.Count > 0)
		{
			frameSubmitData.renderTasks = _renderTasks;
			_renderTasks = new List<CameraRenderTask>();
		}
		if (DebugFramePacing)
		{
			UniLog.Log($"SEND FRAME {frameSubmitData.frameIndex}");
		}
		FrameIndex++;
		if (FrameIndex == 120)
		{
			_messagingHost.SendCommand(new RendererEngineReady(), isBackground: false);
		}
		_messagingHost.SendCommand(frameSubmitData, isBackground: false);
		if (DebugFramePacing)
		{
			UniLog.Log($"SEND FRAME {frameSubmitData.frameIndex} - COMPLETE");
		}
		_frameTimer.Stop();
		LastFrameGenerationTime = (float)(_frameTimer.GetElapsedMilliseconds() * 0.0010000000474974513);
	}

	public bool SupportsTextureFormat(TextureFormat format)
	{
		return _supportedFormats?.Contains(format) ?? false;
	}

	internal SharedMemoryBlockLease<T> AllocateBlock<T>(int count, bool frameBound, object owner) where T : unmanaged
	{
		SharedMemoryBlockLease<T> sharedMemoryBlockLease = _sharedMemory.ClaimBlock<T>(count, owner);
		if (frameBound && sharedMemoryBlockLease != null)
		{
			Finalizer.RegisterFrameMemoryBlock(sharedMemoryBlockLease);
		}
		return sharedMemoryBlockLease;
	}

	private void HandleFailure(Exception exception)
	{
		UniLog.Log($"Failure in message processing:\n{exception}");
		Engine.RequestShutdown();
	}

	private void HandleCommand(RendererCommand command, int messageSize)
	{
		if (!(command is RendererInitResult rendererInitResult))
		{
			if (!(command is RendererShutdownRequest))
			{
				if (!(command is FrameStartData frameStartData))
				{
					if (!(command is SetWindowIconResult setWindowIconResult))
					{
						if (!(command is SetTexture2DResult setTexture2DResult))
						{
							if (!(command is SetTexture3DResult setTexture3DResult))
							{
								if (!(command is SetCubemapResult setCubemapResult))
								{
									if (!(command is RenderTextureResult renderTextureResult))
									{
										if (!(command is DesktopTexturePropertiesUpdate desktopTexturePropertiesUpdate))
										{
											if (!(command is VideoTextureReady videoTextureReady))
											{
												if (!(command is VideoTextureChanged videoTextureChanged))
												{
													if (!(command is MeshUploadResult meshUploadResult))
													{
														if (!(command is ShaderUploadResult shaderUploadResult))
														{
															if (!(command is MaterialPropertyIdResult result))
															{
																if (!(command is MaterialsUpdateBatchResult result2))
																{
																	if (!(command is PointRenderBufferConsumed pointRenderBufferConsumed))
																	{
																		if (!(command is TrailRenderBufferConsumed trailRenderBufferConsumed))
																		{
																			if (!(command is LightsBufferRendererConsumed lightsBufferRendererConsumed))
																			{
																				if (!(command is GaussianSplatResult gaussianSplatResult))
																				{
																					if (!(command is KeepAlive))
																					{
																						if (!(command is ReflectionProbeRenderResult result3))
																						{
																							throw new InvalidOperationException("Invalid command received: " + command.GetType().Name);
																						}
																						ReflectionProbeRenderManager.HandleResult(result3);
																					}
																					else
																					{
																						UniLog.Log("Received KeepAlive");
																					}
																				}
																				else
																				{
																					GaussianSplats.TryGet(gaussianSplatResult.assetId)?.HandleResult(gaussianSplatResult);
																				}
																			}
																			else
																			{
																				LightBuffers.TryGet(lightsBufferRendererConsumed.globalUniqueId)?.HandleSubmitted();
																			}
																		}
																		else
																		{
																			TrailBuffers[trailRenderBufferConsumed.assetId].HandleBuffersConsumed();
																		}
																	}
																	else
																	{
																		PointBuffers[pointRenderBufferConsumed.assetId].HandleBuffersConsumed();
																	}
																}
																else
																{
																	Materials.HandleMaterialUpdateBatchResult(result2);
																}
															}
															else
															{
																Materials.HandlePropertyInitResult(result);
															}
														}
														else
														{
															Shaders.TryGet(shaderUploadResult.assetId)?.HandleResult(shaderUploadResult);
														}
													}
													else
													{
														Meshes.TryGet(meshUploadResult.assetId)?.HandleResult(meshUploadResult);
													}
												}
												else
												{
													VideoTextures.TryGet(videoTextureChanged.assetId)?.HandleTextureChanged(videoTextureChanged);
												}
											}
											else
											{
												VideoTextures.TryGet(videoTextureReady.assetId)?.HandleVideoReady(videoTextureReady);
											}
										}
										else
										{
											DesktopTextures.TryGet(desktopTexturePropertiesUpdate.assetId)?.HandlePropertiesUpdate(desktopTexturePropertiesUpdate);
										}
									}
									else
									{
										RenderTextures.TryGet(renderTextureResult)?.HandleResult(renderTextureResult);
									}
								}
								else
								{
									Cubemaps.TryGet(setCubemapResult.assetId)?.HandleResult(setCubemapResult);
								}
							}
							else
							{
								Texture3Ds.TryGet(setTexture3DResult.assetId)?.HandleResult(setTexture3DResult);
							}
						}
						else
						{
							Texture2Ds.TryGet(setTexture2DResult.assetId)?.HandleResult(setTexture2DResult);
						}
					}
					else
					{
						HandleSetIconResult(setWindowIconResult);
					}
				}
				else
				{
					LastFrameStartMessageBytes = messageSize;
					_frameStartData = frameStartData;
					if (DebugFramePacing)
					{
						UniLog.Log($"RECEIVE FRAME START {frameStartData.lastFrameIndex}");
					}
					_frameStartEvent.Set();
				}
			}
			else
			{
				Engine.RequestShutdown();
			}
		}
		else
		{
			UniLog.Log("Renderer initialized. Result:\n" + rendererInitResult);
			IsGPUTexturePOTByteAligned = rendererInitResult.isGPUTexturePOTByteAligned;
			MaxTextureSize = rendererInitResult.maxTextureSize;
			RendererName = rendererInitResult.rendererIdentifier;
			RendererWindowHandle = new IntPtr(rendererInitResult.mainWindowHandlePtr);
			_supportedFormats = new HashSet<TextureFormat>(rendererInitResult.supportedTextureFormats);
			State = RendererState.Rendering;
			_actualHeadOutputDevice.SetResult(rendererInitResult.actualOutputDevice);
			if (_customSplashScreen != null)
			{
				_customSplashScreen.Buffer.Dispose();
				_customSplashScreen = null;
			}
		}
	}

	internal void SendInitProgress(RendererInitProgressUpdate update)
	{
		if (_initDone)
		{
			throw new InvalidOperationException("Cannot update init progress, init has already completed");
		}
		_messagingHost.SendCommand(update, isBackground: false);
	}

	internal void SendInitDone()
	{
		if (_initDone)
		{
			throw new InvalidOperationException("Init is already one");
		}
		_initDone = true;
		_messagingHost.SendCommand(new RendererInitFinalizeData(), isBackground: false);
	}

	internal void SendAssetUpdate(AssetCommand assetCommand)
	{
		if (assetCommand == null)
		{
			throw new ArgumentNullException("assetCommand");
		}
		if (assetCommand.assetId < 0)
		{
			throw new ArgumentException("AssetID was not assigned!");
		}
		_messagingHost.SendCommand(assetCommand, isBackground: true);
	}

	internal void SendMaterialUpdate(MaterialsUpdateBatch materialBatch)
	{
		if (materialBatch == null)
		{
			throw new ArgumentNullException("materialBatch");
		}
		if (materialBatch.updateBatchId < 0)
		{
			throw new ArgumentException("UpdateBatchId was not assigned!");
		}
		_messagingHost.SendCommand(materialBatch, isBackground: true);
	}

	internal void SendMaterialPropertyIdRequest(MaterialPropertyIdRequest request)
	{
		if (request == null)
		{
			throw new ArgumentNullException("request");
		}
		_messagingHost.SendCommand(request, isBackground: true);
	}

	internal void SendLightsBuffer(LightsBufferRendererSubmission submission)
	{
		if (submission == null)
		{
			throw new ArgumentNullException("submission");
		}
		if (submission.lightsBufferUniqueId < 0)
		{
			throw new ArgumentException("LightsBufferUniqueId must be assigned!");
		}
		_messagingHost.SendCommand(submission, isBackground: true);
	}

	public void ParentWindow(nint window)
	{
		RendererParentWindow rendererParentWindow = new RendererParentWindow();
		rendererParentWindow.windowHandle = ((IntPtr)window).ToInt64();
		_messagingHost.SendCommand(rendererParentWindow, isBackground: true);
	}

	public IBackingMemoryBuffer Allocate(int size, object owner = null)
	{
		return _sharedMemory.ClaimBlock<byte>(size, owner);
	}

	public void Free(IBackingMemoryBuffer buffer)
	{
		if (buffer is SharedMemoryBlockLease sharedMemoryBlockLease)
		{
			sharedMemoryBlockLease.Dispose();
			return;
		}
		throw new ArgumentException("Buffer must be a " + typeof(SharedMemoryBlockLease).Name);
	}

	public void ShutdownRenderer()
	{
		_messagingHost.SendCommand(new RendererShutdown(), isBackground: false);
		_bootstrapper?.Dispose();
		_bootstrapper = null;
	}

	internal void HandleVideoClockErrors(List<VideoTextureClockErrorState> states)
	{
		foreach (VideoTextureClockErrorState state in states)
		{
			VideoTextures.TryGet(state.assetId)?.HandleClockError(state);
		}
	}

	private void MemoryBufferFreed(int id)
	{
		FreeSharedMemoryView freeSharedMemoryView = new FreeSharedMemoryView();
		freeSharedMemoryView.bufferId = id;
		_messagingHost.SendCommand(freeSharedMemoryView, isBackground: true);
	}

	private async Task RendererWatchdog()
	{
		while (!Engine.ShutdownRequested)
		{
			if (RendererProcess.HasExited)
			{
				UniLog.Log("RendererProcess has exited. Shutting down.");
				Engine.ForceCrash();
				_frameStartEvent.Set();
				break;
			}
			_bootstrapper?.SendHeartbeat();
			await Task.Delay(TimeSpan.FromSeconds(5L));
		}
	}

	public TextureFormat EnsureCompatibleFormat(TextureFormat format)
	{
		if (!HasRenderer)
		{
			return format;
		}
		if (format == TextureFormat.RGB24 && Engine.RenderSystem.IsGPUTexturePOTByteAligned)
		{
			format = TextureFormat.RGBA32;
		}
		if (!Engine.RenderSystem.SupportsTextureFormat(format))
		{
			TextureFormat? textureFormat = format.FindCompatibleFormat(Engine.RenderSystem.SupportsTextureFormat);
			if (!textureFormat.HasValue)
			{
				throw new Exception($"Procedural texture format is set to {format}, but this is not compatible with current renderer. Could not find alternate format");
			}
			format = textureFormat.Value;
		}
		return format;
	}

	public string GenerateSharedMemoryDebugDiagnostic()
	{
		return _sharedMemory.GenerateDebugDiagnostic();
	}

	public void RegisterBootstrapperClipboardInterface()
	{
		if (_bootstrapper == null)
		{
			throw new InvalidOperationException("Engine is not running with the bootstrapper");
		}
		BootstrapperClipboardInterface clipboardInterface = new BootstrapperClipboardInterface(_bootstrapper);
		Engine.InputInterface.RegisterClipboardInterface(clipboardInterface);
	}

	private void HandleSetIconResult(SetWindowIconResult setWindowIconResult)
	{
		lock (_setWindowRequests)
		{
			_setWindowRequests[setWindowIconResult.requestId].TrySetResult(setWindowIconResult.success);
			_setWindowRequests.Remove(setWindowIconResult.requestId);
		}
	}

	public Task<bool> SetWindowIcon(Bitmap2D bitmap, Action<SetWindowIcon> overrideSend = null)
	{
		return SetWindowIcon(bitmap, isOverlay: false, null, overrideSend);
	}

	public Task<bool> SetWindowOverlayIcon(Bitmap2D bitmap, string description)
	{
		return SetWindowIcon(bitmap, isOverlay: true, description, null);
	}

	public Task<bool> ClearOverlayIcon(string description)
	{
		return SetWindowIcon(null, isOverlay: true, description, null);
	}

	private async Task<bool> SetWindowIcon(Bitmap2D bitmap, bool isOverlay, string overlayDescription, Action<SetWindowIcon> overrideSend)
	{
		try
		{
			if (!isOverlay && bitmap == null)
			{
				throw new ArgumentException("Bitmap cannot be null for the main icon");
			}
			bool allocated = false;
			if (bitmap != null && (bitmap.Format != TextureFormat.BGRA32 || !(bitmap.Buffer is SharedMemoryBlockLease)))
			{
				allocated = true;
				bitmap = bitmap.ConvertTo(TextureFormat.BGRA32, this);
			}
			SetWindowIcon setWindowIcon = new SetWindowIcon();
			setWindowIcon.requestId = Interlocked.Increment(ref _setIconRequest);
			setWindowIcon.isOverlay = isOverlay;
			if (bitmap != null)
			{
				setWindowIcon.size = bitmap.Size;
				setWindowIcon.iconData = ((SharedMemoryBlockLease<byte>)bitmap.Buffer).Descriptor;
			}
			if (isOverlay)
			{
				setWindowIcon.overlayDescription = overlayDescription;
			}
			TaskCompletionSource<bool> taskCompletionSource = new TaskCompletionSource<bool>();
			lock (_setWindowRequests)
			{
				_setWindowRequests.Add(setWindowIcon.requestId, taskCompletionSource);
			}
			if (overrideSend == null)
			{
				_messagingHost.SendCommand(setWindowIcon, isBackground: true);
			}
			else
			{
				overrideSend(setWindowIcon);
			}
			bool result = await taskCompletionSource.Task;
			if (allocated)
			{
				bitmap.Buffer.Dispose();
			}
			return result;
		}
		catch (Exception ex)
		{
			UniLog.Error("Exception setting window icon:\n" + ex);
			return false;
		}
	}

	private void UpdateTaskbar()
	{
		if (TaskbarProgressMode != _submittedTaskbarProgressMode || TaskbarCompleted != _submittedTaskbarCompleted || TaskbarTotal != _submittedTaskbarTotal)
		{
			_submittedTaskbarProgressMode = TaskbarProgressMode;
			_submittedTaskbarCompleted = TaskbarCompleted;
			_submittedTaskbarTotal = TaskbarTotal;
			_messagingHost.SendCommand(new SetTaskbarProgress
			{
				mode = _submittedTaskbarProgressMode,
				completed = _submittedTaskbarCompleted,
				total = _submittedTaskbarTotal
			}, isBackground: true);
		}
	}
}
