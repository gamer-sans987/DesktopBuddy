public class ViveProEyeTrackingDriver : IInputDriver
{
	private InputInterface input;

	private Eyes eyes;

	private VerboseData latestVerboseData;

	private EyeData_v2 latestEyeData;

	private Error latestEyeResult;

	private Mouth mouth;

	private LipData_v2 latestLipData;

	private Error latestLipResult;

	private AutoResetEvent updateResetEvent = new AutoResetEvent(initialState: false);

	private TaskCompletionSource<bool> disposeTask;

	private volatile bool eyeApiInitialized;

	private volatile bool lipApiInitialized;

	private int lastTimestamp;

	private object _lock = new object();

	public int UpdateOrder => 100;

	private static bool SupressRegistration(InputInterface input)
	{
		if (input.HeadOutputDevice == HeadOutputDevice.Screen)
		{
			return true;
		}
		if (input.Engine.Platform != Platform.Windows)
		{
			return true;
		}
		GeneralHeadset device = input.GetDevice<GeneralHeadset>();
		if (device == null)
		{
			UniLog.Log("Could not find headset device, supressing SRAnipal");
			return true;
		}
		if (device.ConnectionType == HeadsetConnection.WirelessSteamLink)
		{
			UniLog.Log("Detected SteamLink, supressing SRAnipal");
			return true;
		}
		if ((device.HeadsetManufacturer != null && device.HeadsetManufacturer.IndexOf("oculus", StringComparison.InvariantCultureIgnoreCase) >= 0) || (device.HeadsetModel != null && device.HeadsetModel.IndexOf("oculus", StringComparison.InvariantCultureIgnoreCase) >= 0))
		{
			UniLog.Log("Detected Oculus Headset, supressing SRAnipal");
			return true;
		}
		Process[] processesByName = Process.GetProcessesByName("ReVision.App");
		if (processesByName != null && processesByName.Length != 0)
		{
			UniLog.Log("Detected ReVision, supressing SRAnipal");
			return true;
		}
		return false;
	}

	public static bool ShouldRegister(LaunchOptions options, InputInterface input)
	{
		if (options.ForceSRAnipal)
		{
			UniLog.Log("Forcing SRAnipal");
			return true;
		}
		if (input.HeadOutputDevice == HeadOutputDevice.Screen)
		{
			return false;
		}
		if (SupressRegistration(input))
		{
			UniLog.Log("SRAnipal registration supressed");
			return false;
		}
		if (File.Exists("C:\\Program Files\\VIVE\\SRanipal\\sr_runtime.exe"))
		{
			return true;
		}
		if (input.HeadOutputDevice == HeadOutputDevice.SteamVR)
		{
			bool result = SRanipal_Eye_API.IsViveProEye();
			UniLog.Log("Detect ViveProEye: " + result);
			return result;
		}
		return false;
	}

	public void CollectDeviceInfos(DataTreeList list)
	{
		if (eyes != null)
		{
			DataTreeDictionary dataTreeDictionary = new DataTreeDictionary();
			dataTreeDictionary.Add("Name", "Vive Pro Eye");
			dataTreeDictionary.Add("Type", "Eye Tracking");
			dataTreeDictionary.Add("Model", "Vive Pro Eye");
			list.Add(dataTreeDictionary);
		}
		if (mouth != null)
		{
			DataTreeDictionary dataTreeDictionary2 = new DataTreeDictionary();
			dataTreeDictionary2.Add("Name", "Vive Lip Tracker");
			dataTreeDictionary2.Add("Type", "Lip Tracking");
			dataTreeDictionary2.Add("Model", "Lip Tracker");
			list.Add(dataTreeDictionary2);
		}
	}

	public void RegisterInputs(InputInterface inputInterface)
	{
		InitializeViveProEye(inputInterface);
	}

	private void InitializeViveProEye(InputInterface inputInterface)
	{
		UniLog.Log("Initializing SRAnipal");
		input = inputInterface;
		StartTask(async delegate
		{
			UniLog.Log("Initializing SRAnipal Eyes");
			await InitializeEyes(isFirstInitialization: true);
			UniLog.Log("Initializing SRAnipal Lip");
			await InitializeLip(isFirstInitialization: true);
		});
		Thread thread = new Thread(SRAnipalWorker);
		thread.Name = "SrAnipalWorker Worker";
		thread.Priority = ThreadPriority.BelowNormal;
		thread.Start();
		inputInterface.Engine.OnShutdown += Engine_OnShutdown;
	}

	private void Engine_OnShutdown()
	{
		Destroy();
	}

	private void StartTask(Func<Task> task)
	{
		input.Engine.GlobalCoroutineManager.StartTask(async delegate
		{
			try
			{
				await task();
			}
			catch (Exception ex)
			{
				UniLog.Error("Exception in SRAnipal task:\n" + ex);
			}
		});
	}

	private async Task InitializeEyes(bool isFirstInitialization)
	{
		await default(ToBackground);
		await InitializeAPI(isFirstInitialization, isLip: false);
		UniLog.Log("Post InitializeAPI Eye. ApiInitialized: " + eyeApiInitialized);
		if (eyeApiInitialized)
		{
			await default(ToWorld);
			eyes = new Eyes(input, "Vive Pro Eye", supportsPupilTracking: true);
		}
	}

	private async Task InitializeLip(bool isFirstInitialization)
	{
		await default(ToBackground);
		await InitializeAPI(isFirstInitialization, isLip: true);
		UniLog.Log("Post InitializeAPI Lip. ApiInitialized: " + lipApiInitialized);
		if (lipApiInitialized)
		{
			await default(ToWorld);
			mouth = new Mouth(input, "Vive Lip Tracking Camera", new MouthParameterGroup[11]
			{
				MouthParameterGroup.JawPose,
				MouthParameterGroup.JawOpen,
				MouthParameterGroup.TonguePose,
				MouthParameterGroup.TongueRoll,
				MouthParameterGroup.LipRaise,
				MouthParameterGroup.LipHorizontal,
				MouthParameterGroup.SmileFrown,
				MouthParameterGroup.MouthPout,
				MouthParameterGroup.LipOverturn,
				MouthParameterGroup.LipOverUnder,
				MouthParameterGroup.CheekPuffSuck
			});
		}
	}

	private async Task InitializeAPI(bool isFirstInitialization, bool isLip)
	{
		string type = (isLip ? "Lip Tracking" : "Eye Tracking");
		Error error = Error.UNDEFINED;
		int noDeviceCount = 0;
		while (true)
		{
			int anipalType = (isLip ? 3 : 2);
			lock (_lock)
			{
				error = SRanipal_API.Initial(anipalType, IntPtr.Zero);
			}
			UniLog.Log("SRAnipal " + type + " Init Result: " + error);
			switch (error)
			{
			case Error.UNKONW_MODULE:
			case Error.DEVICE_NOT_FOUND:
			case Error.SERVICE_NOT_FOUND:
			case Error.DISABLED_BY_USER:
			case Error.NOT_SUPPORT_EYE_TRACKING:
				UniLog.Log("No Device for " + type + ". First initialization: " + isFirstInitialization);
				noDeviceCount++;
				if (isFirstInitialization)
				{
					return;
				}
				if (noDeviceCount < 10)
				{
					await Task.Delay(TimeSpan.FromSeconds(5L));
					continue;
				}
				if (noDeviceCount >= 15)
				{
					return;
				}
				await Task.Delay(TimeSpan.FromSeconds(15L));
				continue;
			default:
				UniLog.Error("Error initializing SRanipal " + type + ": " + error);
				if (error != Error.RUNTIME_NOT_FOUND && error != Error.TIMEOUT)
				{
					await Task.Delay(TimeSpan.FromSeconds(2L));
					continue;
				}
				break;
			case Error.WORK:
				break;
			}
			break;
		}
		if (error == Error.WORK)
		{
			if (isLip)
			{
				lipApiInitialized = true;
			}
			else
			{
				eyeApiInitialized = true;
			}
			updateResetEvent.Set();
			UniLog.Log(type + " Initialized!");
		}
	}

	private void SRAnipalWorker()
	{
		try
		{
			while (disposeTask == null)
			{
				updateResetEvent.WaitOne();
				if (eyes != null && eyeApiInitialized)
				{
					lock (_lock)
					{
						latestEyeResult = SRanipal_Eye_API.GetEyeData_v2(ref latestEyeData);
					}
					if (latestEyeResult != Error.WORK)
					{
						UniLog.Error("Error getting eye data from SRanipal: " + latestEyeResult);
					}
				}
				if (mouth != null && lipApiInitialized)
				{
					lock (_lock)
					{
						latestLipResult = SRanipal_Lip_API.GetLipData_v2(ref latestLipData);
					}
					if (latestLipResult != Error.WORK)
					{
						UniLog.Error("Error getting lip data from SRanipal: " + latestLipResult);
					}
				}
			}
			try
			{
				UniLog.Log("Shutting down SRanipal_API");
				lock (_lock)
				{
					if (eyes != null)
					{
						SRanipal_API.Release(2);
					}
					if (mouth != null)
					{
						SRanipal_API.Release(3);
					}
				}
			}
			catch (Exception ex)
			{
				UniLog.Error("Exception when disposing SRanipal_API:\n" + ex);
			}
		}
		finally
		{
			disposeTask?.SetResult(result: true);
		}
	}

	public void UpdateInputs(float deltaTime)
	{
		bool num = eyes != null && eyeApiInitialized;
		bool flag = mouth != null && lipApiInitialized;
		if (num)
		{
			if (latestEyeResult != Error.WORK)
			{
				eyes.SetTracking(state: false);
				if (latestEyeResult == Error.UNKONW_MODULE || latestLipResult == Error.NOT_INITIAL)
				{
					eyeApiInitialized = false;
					StartTask(async delegate
					{
						await InitializeAPI(isFirstInitialization: false, isLip: false);
					});
				}
			}
			else
			{
				eyes.IsEyeTrackingActive = input.VR_Active;
				eyes.Timestamp = (double)latestEyeData.timestamp * 0.001;
				UpdateEye(in latestEyeData.verbose_data.left, eyes.LeftEye);
				UpdateEye(in latestEyeData.verbose_data.right, eyes.RightEye);
				UpdateEye(in latestEyeData.verbose_data.combined.eye_data, eyes.CombinedEye);
				eyes.LeftEye.Widen = latestEyeData.expression_data.left.eye_wide;
				eyes.RightEye.Widen = latestEyeData.expression_data.right.eye_wide;
				eyes.LeftEye.Frown = latestEyeData.expression_data.left.eye_frown;
				eyes.RightEye.Frown = latestEyeData.expression_data.right.eye_frown;
				eyes.LeftEye.Squeeze = latestEyeData.expression_data.left.eye_squeeze;
				eyes.RightEye.Squeeze = latestEyeData.expression_data.right.eye_squeeze;
				eyes.ComputeCombinedEyeParameters();
				if (latestEyeData.verbose_data.combined.convergence_distance_validity)
				{
					eyes.ConvergenceDistance = latestEyeData.verbose_data.combined.convergence_distance_mm * 0.001f;
				}
				eyes.FinishUpdate();
			}
		}
		if (flag)
		{
			if (latestLipResult != Error.WORK)
			{
				mouth.IsTracking = false;
				if (latestLipResult == Error.UNKONW_MODULE || latestLipResult == Error.NOT_INITIAL)
				{
					lipApiInitialized = false;
					StartTask(async delegate
					{
						await InitializeAPI(isFirstInitialization: false, isLip: true);
					});
				}
			}
			else
			{
				float[] blend_shape_weight = latestLipData.prediction_data.blend_shape_weight;
				if (blend_shape_weight == null)
				{
					mouth.IsTracking = false;
				}
				else
				{
					mouth.IsTracking = true;
					float x = blend_shape_weight[0] - blend_shape_weight[1];
					float z = blend_shape_weight[2];
					float num2 = blend_shape_weight[4];
					mouth.Jaw = new Elements.Core.float3(x, 0f - num2, z);
					mouth.JawOpen = blend_shape_weight[3];
					float z2 = (blend_shape_weight[26] + blend_shape_weight[32]) * 0.5f;
					float y = blend_shape_weight[29] - blend_shape_weight[30];
					float x2 = blend_shape_weight[28] - blend_shape_weight[27];
					mouth.Tongue = new Elements.Core.float3(x2, y, z2);
					mouth.TongueRoll = blend_shape_weight[31];
					mouth.LipUpperLeftRaise = blend_shape_weight[20];
					mouth.LipUpperRightRaise = blend_shape_weight[19];
					mouth.LipLowerLeftRaise = blend_shape_weight[22];
					mouth.LipLowerRightRaise = blend_shape_weight[21];
					mouth.LipUpperHorizontal = blend_shape_weight[5] - blend_shape_weight[6];
					mouth.LipLowerHorizontal = blend_shape_weight[7] - blend_shape_weight[8];
					mouth.MouthLeftSmileFrown = blend_shape_weight[13] - blend_shape_weight[15];
					mouth.MouthRightSmileFrown = blend_shape_weight[12] - blend_shape_weight[14];
					mouth.MouthPoutLeft = blend_shape_weight[11];
					mouth.MouthPoutRight = mouth.MouthPoutLeft;
					mouth.LipTopLeftOverturn = blend_shape_weight[9];
					mouth.LipTopRightOverturn = mouth.LipTopLeftOverturn;
					mouth.LipBottomLeftOverturn = blend_shape_weight[10];
					mouth.LipBottomRightOverturn = mouth.LipBottomLeftOverturn;
					mouth.LipTopLeftOverUnder = 0f - blend_shape_weight[23];
					mouth.LipTopRightOverUnder = mouth.LipTopLeftOverUnder;
					mouth.LipBottomLeftOverUnder = blend_shape_weight[25] - blend_shape_weight[24];
					mouth.LipBottomRightOverUnder = mouth.LipBottomLeftOverUnder;
					mouth.CheekLeftPuffSuck = blend_shape_weight[17];
					mouth.CheekRightPuffSuck = blend_shape_weight[16];
					mouth.CheekLeftPuffSuck -= blend_shape_weight[18];
					mouth.CheekRightPuffSuck -= blend_shape_weight[18];
				}
			}
		}
		if (num || flag)
		{
			updateResetEvent.Set();
		}
	}

	private bool IsValid(ViveSR.anipal.Vector3 value)
	{
		if (IsValid(value.x) && IsValid(value.y))
		{
			return IsValid(value.z);
		}
		return false;
	}

	private bool IsValid(float value)
	{
		if (!float.IsInfinity(value))
		{
			return !float.IsNaN(value);
		}
		return false;
	}

	private void UpdateEye(in SingleEyeData data, FrooxEngine.Eye eye)
	{
		bool flag = data.GetValidity(SingleEyeDataValidity.SINGLE_EYE_DATA_GAZE_ORIGIN_VALIDITY) && data.GetValidity(SingleEyeDataValidity.SINGLE_EYE_DATA_GAZE_DIRECTION_VALIDITY);
		flag &= IsValid(data.gaze_direction_normalized);
		flag &= IsValid(data.eye_openness);
		flag &= IsValid(data.gaze_origin_mm);
		flag &= IsValid(data.pupil_diameter_mm);
		eye.IsTracking = flag;
		if (eye.IsTracking)
		{
			eye.UpdateWithDirection(ToEngine(data.gaze_direction_normalized) * new Elements.Core.float3(-1f, 1f, 1f));
			eye.RawPosition = ToEngine(data.gaze_origin_mm) * 0.001f * new Elements.Core.float3(-1f, 1f, 1f);
		}
		if (eye.IsTracking && data.GetValidity(SingleEyeDataValidity.SINGLE_EYE_DATA_PUPIL_DIAMETER_VALIDITY))
		{
			eye.PupilDiameter = data.pupil_diameter_mm * 0.001f;
		}
		if (data.GetValidity(SingleEyeDataValidity.SINGLE_EYE_DATA_EYE_OPENNESS_VALIDITY))
		{
			eye.Openness = MathX.FilterInvalid(data.eye_openness);
		}
	}

	private void Destroy()
	{
		disposeTask = new TaskCompletionSource<bool>();
		input?.Engine?.RegisterShutdownTask(disposeTask.Task);
		updateResetEvent.Set();
	}

	private static Elements.Core.float3 ToEngine(ViveSR.anipal.Vector3 v)
	{
		return new Elements.Core.float3(v.x, v.y, v.z);
	}
}
