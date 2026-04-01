using System;
using System.Diagnostics;
using System.Linq;
using ResoniteModLoader.Locale;

namespace ResoniteModLoader;

internal static class ModLoaderInit
{
	internal static void Initialize()
	{
		Logger.DebugInternal("Start of ModLoader Initialization");
		Stopwatch stopwatch = Stopwatch.StartNew();
		try
		{
			AssemblyLoader.SetupResolveHook();
			LoadProgressIndicator.SetSubphase("Loading Libraries");
			AssemblyFile[] array = AssemblyLoader.LoadAssembliesFromDir("rml_libs");
			if (array.Length != 0)
			{
				string text = string.Join("\n", array.Select((AssemblyFile a) => a.Name + ", Version=" + a.Version + ", Sha256=" + a.Sha256));
				Logger.MsgInternal("Loaded libraries from rml_libs:\n" + text);
			}
			LoadProgressIndicator.SetSubphase("Initializing");
			DebugInfo.Log();
			LocaleLoader.InitLocales();
			HarmonyWorker.Init();
			LoadProgressIndicator.SetSubphase("Loaded");
		}
		catch (Exception value)
		{
			Logger.ErrorInternal($"Exception during initialization!\n{value}");
		}
		stopwatch.Stop();
		Logger.MsgInternal($"Initialization completed in {stopwatch.ElapsedMilliseconds}ms");
	}
}
