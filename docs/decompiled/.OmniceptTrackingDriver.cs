public class OmniceptTrackingDriver : IInputDriver
{
	private InputInterface input;

	private Eyes eyes;

	private GliaHandler glia;

	public int UpdateOrder => 0;

	public static bool ShouldRegister(InputInterface inputInterface)
	{
		if (inputInterface.HeadOutputDevice != HeadOutputDevice.SteamVR)
		{
			return false;
		}
		if (inputInterface.GetDevice<GeneralHeadset>().ConnectionType == HeadsetConnection.WirelessSteamLink)
		{
			return false;
		}
		return true;
	}

	public void CollectDeviceInfos(DataTreeList list)
	{
		if (eyes != null)
		{
			DataTreeDictionary dataTreeDictionary = new DataTreeDictionary();
			dataTreeDictionary.Add("Name", "HP Omnicept");
			dataTreeDictionary.Add("Type", "Eye Tracking");
			dataTreeDictionary.Add("Model", "HP Omnicept Eye Tracking");
			list.Add(dataTreeDictionary);
		}
	}

	public void RegisterInputs(InputInterface inputInterface)
	{
		input = inputInterface;
		UniLog.Log("Initializing HP Omnicept Glia...");
		glia = new GliaHandler();
		glia.OnConnect += OnConnected;
		glia.OnConnectionFailure += OnConnectionFailure;
		glia.OnDisconnect += OnDisconnect;
		glia.Initialize();
	}

	private void OnDisconnect(string obj)
	{
		UniLog.Warning("Disconnected from HP Omnicept Glia: " + obj);
	}

	private void OnConnected(GliaHandler glia)
	{
		try
		{
			eyes = new Eyes(input, "HP Omnicept Eye", supportsPupilTracking: true);
			UniLog.Log("Initialized HP Omnicept Glia");
		}
		catch (Exception ex)
		{
			UniLog.Error("Exception initializing HP Omnicept Eye: " + ex);
		}
	}

	private void OnConnectionFailure(ClientHandshakeError error)
	{
		UniLog.Log("Could not connect to HP Omnicept Runtime: " + error.Message);
		glia.Dispose();
	}

	public void UpdateInputs(float deltaTime)
	{
		glia.Update();
		if (eyes != null)
		{
			EyeTracking lastEyeTracking = glia.GetLastEyeTracking();
			if (lastEyeTracking == null)
			{
				eyes.IsEyeTrackingActive = false;
				eyes.SetTracking(state: false);
				return;
			}
			eyes.IsEyeTrackingActive = input.VR_Active;
			eyes.Timestamp = lastEyeTracking.Timestamp.HardwareTimeMicroSeconds;
			UpdateEye(eyes.LeftEye, lastEyeTracking.LeftEye);
			UpdateEye(eyes.RightEye, lastEyeTracking.RightEye);
			UpdateGaze(eyes.CombinedEye, lastEyeTracking.CombinedGaze);
			eyes.ComputeCombinedEyeParameters();
			eyes.FinishUpdate();
		}
	}

	private void UpdateEye(FrooxEngine.Eye eye, HP.Omnicept.Messaging.Messages.Eye omnicept)
	{
		eye.RawPosition = new Elements.Core.float3(0.064f * (float)((eye.Side != EyeSide.Left) ? 1 : (-1)));
		UpdateGaze(eye, omnicept.Gaze);
		if (omnicept.OpennessConfidence >= 0.25f)
		{
			eye.Openness = omnicept.Openness;
		}
		if (omnicept.PupilDilationConfidence >= 0.25f)
		{
			eye.PupilDiameter = omnicept.PupilDilation * 0.001f;
		}
	}

	private void UpdateGaze(FrooxEngine.Eye eye, EyeGaze gaze)
	{
		eye.IsTracking = gaze.Confidence >= 0.25f;
		if (eye.IsTracking)
		{
			eye.UpdateWithDirection(new Elements.Core.float3(0f - gaze.X, gaze.Y, gaze.Z).Normalized);
		}
	}
}
