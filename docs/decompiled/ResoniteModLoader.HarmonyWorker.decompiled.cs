using HarmonyLib;

namespace ResoniteModLoader;

internal sealed class HarmonyWorker
{
	internal static void Init()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Expected O, but got Unknown
		Harmony val = new Harmony("com.resonitemodloader.ResoniteModLoader");
		ModLoader.LoadMods();
		ModConfiguration.RegisterShutdownHook(val);
	}
}
