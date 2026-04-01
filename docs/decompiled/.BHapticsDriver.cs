public class BHapticsDriver : IInputDriver
{
	private class HapticPointData
	{
		public readonly HapticPoint point;

		public float tempPhi;

		public float vibrationPhi;

		public HapticPointData(HapticPoint point)
		{
			this.point = point;
		}
	}

	public const int UPDATE_INTERVAL_MS = 10;

	public const int HEAD_POINTS = 6;

	public const float HEAD_ANGLE_RANGE = 90f;

	public const float HEAD_PITCH = 35f;

	public const float HEAD_RADIUS = 0.01f;

	public const int VEST_X_POINTS = 4;

	public const int VEST_Y_POINTS = 5;

	public const float VEST_RADIUS = 0.05f;

	public const int FOREARM_X_POINTS = 3;

	public const int FOREARM_Y_POINTS = 2;

	public const float FOREARM_RADIUS = 0.025f;

	public const float FOREARM_ANGLE_RANGE = 270f;

	public const int FOOT_X_POINTS = 3;

	public const int FOOT_Y_POINTS = 2;

	public const float FOOT_RADIUS = 0.025f;

	public const float FOOT_ANGLE_RANGE = 270f;

	private InputInterface inputInterface;

	private HapticPlayer haptic;

	private CancellationTokenSource cancellationToken;

	private Dictionary<Bhaptics.Tact.PositionType, List<HapticPointData>> hapticPoints = new Dictionary<Bhaptics.Tact.PositionType, List<HapticPointData>>();

	public int UpdateOrder => 0;

	public void CollectDeviceInfos(DataTreeList list)
	{
		foreach (KeyValuePair<Bhaptics.Tact.PositionType, List<HapticPointData>> hapticPoint in hapticPoints)
		{
			DataTreeDictionary dataTreeDictionary = new DataTreeDictionary();
			dataTreeDictionary.Add("Name", "bHaptics");
			dataTreeDictionary.Add("Type", "Haptics");
			dataTreeDictionary.Add("Model", hapticPoint.Key.ToString());
			list.Add(dataTreeDictionary);
		}
	}

	public void RegisterInputs(InputInterface inputInterface)
	{
		inputInterface.Engine.GlobalCoroutineManager.StartTask(async delegate
		{
			try
			{
				await InitializeBhaptics(inputInterface);
			}
			catch (Exception ex)
			{
				UniLog.Error("Exception initializing bHpatics:\n" + ex);
			}
		});
	}

	public void UpdateInputs(float deltaTime)
	{
	}

	private async Task InitializeBhaptics(InputInterface inputInterface)
	{
		this.inputInterface = inputInterface;
		await default(ToBackground);
		UniLog.Log("Initializing bHaptics...");
		IPlatformProfile platformProfile = inputInterface.Engine.PlatformProfile;
		haptic = new HapticPlayer(platformProfile.Abbreviation, platformProfile.Name);
		bool hasHead = false;
		bool hasVest = false;
		bool hasLeftForearm = false;
		bool hasRightForearm = false;
		bool hasLeftFoot = false;
		bool hasRightFoot = false;
		bool hasVestFront = false;
		bool hasVestBack = false;
		for (int i = 0; i < 5; i++)
		{
			hasHead = haptic.IsActive(Bhaptics.Tact.PositionType.Head);
			hasVest = haptic.IsActive(Bhaptics.Tact.PositionType.Vest);
			hasVestFront = haptic.IsActive(Bhaptics.Tact.PositionType.VestFront);
			hasVestBack = haptic.IsActive(Bhaptics.Tact.PositionType.VestBack);
			hasLeftForearm = haptic.IsActive(Bhaptics.Tact.PositionType.ForearmL);
			hasRightForearm = haptic.IsActive(Bhaptics.Tact.PositionType.ForearmR);
			hasLeftFoot = haptic.IsActive(Bhaptics.Tact.PositionType.FootL);
			hasRightFoot = haptic.IsActive(Bhaptics.Tact.PositionType.FootR);
			if (HasAny())
			{
				break;
			}
			await Task.Delay(TimeSpan.FromSeconds(0.20000000298023224));
		}
		UniLog.Log($"bHaptics detected - Head: {hasHead}, Vest: {hasVest} (Front: {hasVestFront}, Back: {hasVestBack}), LeftForearm: {hasLeftForearm}, RightForearm: {hasRightForearm}, LeftFoot: {hasLeftFoot}, RightFoot: {hasRightFoot}");
		if (!HasAny())
		{
			haptic.Dispose();
			return;
		}
		if (hasHead)
		{
			InitializeHead();
		}
		if (hasVest)
		{
			InitializeVest();
		}
		if (hasLeftForearm)
		{
			InitializeForearm(left: true);
		}
		if (hasRightForearm)
		{
			InitializeForearm(left: false);
		}
		if (hasLeftFoot)
		{
			InitializeFoot(left: true);
		}
		if (hasRightFoot)
		{
			InitializeFoot(left: false);
		}
		await default(ToWorld);
		int num = 0;
		foreach (KeyValuePair<Bhaptics.Tact.PositionType, List<HapticPointData>> hapticPoint in hapticPoints)
		{
			foreach (HapticPointData item in hapticPoint.Value)
			{
				inputInterface.RegisterHapticPoint(item.point);
				num++;
				UniLog.Log($"HapticPoint ({hapticPoint.Key}): " + item.point);
			}
		}
		UniLog.Log($"Registered {num} haptic points. Starting HapticsWorkerThread");
		cancellationToken = new CancellationTokenSource();
		Thread thread = new Thread(HapticsWorkerThread);
		thread.Priority = ThreadPriority.Highest;
		thread.IsBackground = true;
		thread.Start();
		bool HasAny()
		{
			return hasHead || hasVest || hasLeftForearm || hasRightForearm || hasLeftFoot || hasRightFoot;
		}
	}

	private void InitializeHead()
	{
		List<HapticPointData> list = new List<HapticPointData>();
		hapticPoints.Add(Bhaptics.Tact.PositionType.Head, list);
		float num = 45f;
		for (int i = 0; i < 6; i++)
		{
			float lerp = (float)i / 5f;
			HapticPoint point = new HapticPoint(inputInterface, 0.01f, new HeadHapticPointPosition(35f, MathX.Lerp(0f - num, num, lerp)));
			list.Add(new HapticPointData(point));
		}
	}

	private void InitializeVest()
	{
		List<HapticPointData> list = new List<HapticPointData>();
		hapticPoints.Add(Bhaptics.Tact.PositionType.Vest, list);
		for (int i = 0; i < 2; i++)
		{
			bool flag = i == 0;
			for (int j = 0; j < 5; j++)
			{
				float vertical = 1f - (float)j / 4f;
				for (int k = 0; k < 4; k++)
				{
					float num = (float)k / 3f;
					num *= 2f;
					num -= 1f;
					HapticPoint point = new HapticPoint(inputInterface, 0.05f, new TorsoHapticPointPosition(num, vertical, (!flag) ? TorsoSide.Back : TorsoSide.Front));
					list.Add(new HapticPointData(point));
				}
			}
		}
	}

	private void InitializeForearm(bool left)
	{
		List<HapticPointData> list = new List<HapticPointData>();
		hapticPoints.Add(left ? Bhaptics.Tact.PositionType.ForearmL : Bhaptics.Tact.PositionType.ForearmR, list);
		for (int i = 0; i < 2; i++)
		{
			float lerp = 1f - (float)i / 1f;
			for (int j = 0; j < 3; j++)
			{
				float lerp2 = (float)j / 2f;
				float angleAround = MathX.Lerp(135f, -135f, lerp2);
				HapticPoint point = new HapticPoint(inputInterface, 0.025f, new ArmHapticPosition((!left) ? Chirality.Right : Chirality.Left, MathX.Lerp(0.75f, 0.9f, lerp), angleAround));
				list.Add(new HapticPointData(point));
			}
		}
	}

	private void InitializeFoot(bool left)
	{
		List<HapticPointData> list = new List<HapticPointData>();
		hapticPoints.Add(left ? Bhaptics.Tact.PositionType.FootL : Bhaptics.Tact.PositionType.FootR, list);
		for (int i = 0; i < 2; i++)
		{
			float lerp = 1f - (float)i / 1f;
			for (int j = 0; j < 3; j++)
			{
				float lerp2 = (float)j / 2f;
				float angleAround = MathX.Lerp(135f, -135f, lerp2);
				HapticPoint point = new HapticPoint(inputInterface, 0.025f, new LegHapticPosition((!left) ? Chirality.Right : Chirality.Left, MathX.Lerp(0.85f, 1f, lerp), angleAround));
				list.Add(new HapticPointData(point));
			}
		}
	}

	private void HapticsWorkerThread()
	{
		Dictionary<Bhaptics.Tact.PositionType, string> dictionary = new Dictionary<Bhaptics.Tact.PositionType, string>();
		foreach (KeyValuePair<Bhaptics.Tact.PositionType, List<HapticPointData>> hapticPoint in hapticPoints)
		{
			dictionary.Add(hapticPoint.Key, Guid.NewGuid().ToString());
		}
		List<DotPoint> list = new List<DotPoint>();
		float num = 0f;
		while (!cancellationToken.IsCancellationRequested)
		{
			Thread.Sleep(10);
			float num2 = 0.010000001f;
			float num3 = 0f;
			foreach (KeyValuePair<Bhaptics.Tact.PositionType, List<HapticPointData>> hapticPoint2 in hapticPoints)
			{
				foreach (HapticPointData item in hapticPoint2.Value)
				{
					item.point.SampleSources();
					num3 = MathX.Max(item.point.Pain, num3);
				}
			}
			num += MathF.PI * 2f * num2 * MathX.Lerp(1.3333334f, 2.3333333f, num3);
			num %= MathF.PI * 4f;
			foreach (KeyValuePair<Bhaptics.Tact.PositionType, List<HapticPointData>> hapticPoint3 in hapticPoints)
			{
				int num4 = 0;
				foreach (HapticPointData item2 in hapticPoint3.Value)
				{
					HapticPoint point = item2.point;
					float force = point.Force;
					float num5 = MathX.Pow(MathX.Abs(MathX.Sin(num)), 2f) * (float)MathX.Max(0, MathX.Sign(MathX.Sin(num * 0.5f)));
					num5 *= MathX.Pow(point.Pain, 0.5f);
					num5 += RandomX.Value * MathX.Pow(point.Pain, 0.25f) * 0.1f;
					force = MathX.Max(force, num5);
					float num6 = MathX.Abs(point.Temperature / 100f);
					item2.tempPhi += num6 * 4f;
					item2.tempPhi %= 20000f;
					float val = num6 * MathX.SimplexNoise(item2.tempPhi);
					force = MathX.Max(force, val);
					item2.vibrationPhi += MathF.PI * 2f * num2 * MathX.Lerp(0.1f, 10f, point.Vibration);
					item2.vibrationPhi %= MathF.PI * 2f;
					float val2 = (MathX.Sin(item2.vibrationPhi) * 0.5f + 0.5f) * point.Vibration;
					force = MathX.Max(force, val2);
					list.Add(new DotPoint(num4++, MathX.Clamp(MathX.RoundToInt(force * 100f), 0, 100)));
				}
				haptic.Submit(dictionary[hapticPoint3.Key], hapticPoint3.Key, list, 40);
				list.Clear();
			}
		}
		haptic.Dispose();
	}
}
