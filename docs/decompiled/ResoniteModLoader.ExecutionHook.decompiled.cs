using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using FrooxEngine;

namespace ResoniteModLoader;

public class ExecutionHook : IPlatformConnector, IDisposable
{
	public PlatformInterface Platform { get; private set; }

	public int Priority => -10;

	public string PlatformName => "ResoniteModLoader";

	public string Username => null;

	public string PlatformUserId => null;

	public bool IsPlatformNameUnique => false;

	public void SetCurrentStatus(World world, bool isPrivate, int totalWorldCount)
	{
	}

	public void ClearCurrentStatus()
	{
	}

	public void Update()
	{
	}

	public void Dispose()
	{
		GC.SuppressFinalize(this);
	}

	public void NotifyOfLocalUser(User user)
	{
	}

	public void NotifyOfFile(string file, string name)
	{
	}

	public void NotifyOfScreenshot(World world, string file, ScreenshotType type, DateTime time)
	{
	}

	public async Task<bool> Initialize(PlatformInterface platformInterface)
	{
		Logger.DebugInternal("Initialize() from platformInterface");
		Platform = platformInterface;
		return true;
	}

	[ModuleInitializer]
	public static void Init()
	{
		Logger.DebugInternal("Init() from ModuleInitializer");
	}

	static ExecutionHook()
	{
		Logger.DebugInternal("Start of ExecutionHook");
		ModLoaderInit.Initialize();
	}
}
