using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using ResoniteModLoader.Locale;

namespace ResoniteModLoader;

public sealed class ModLoader
{
	public static readonly string VERSION = "5.0.1";

	private static readonly Type RESONITE_MOD_TYPE = typeof(ResoniteMod);

	private static readonly List<ResoniteMod> LoadedMods = new List<ResoniteMod>();

	internal static readonly Dictionary<Assembly, ResoniteMod> AssemblyLookupMap = new Dictionary<Assembly, ResoniteMod>();

	private static readonly Dictionary<string, ResoniteMod> ModNameLookupMap = new Dictionary<string, ResoniteMod>();

	private static bool? _isHeadless;

	internal const string VERSION_CONSTANT = "5.0.1";

	public static bool IsHeadless
	{
		get
		{
			bool valueOrDefault = _isHeadless == true;
			if (!_isHeadless.HasValue)
			{
				valueOrDefault = AppDomain.CurrentDomain.GetAssemblies().Any(delegate(Assembly a)
				{
					IEnumerable<Type> types;
					try
					{
						types = a.GetTypes();
					}
					catch (ReflectionTypeLoadException ex)
					{
						types = ex.Types;
					}
					return types.Any(delegate(Type t)
					{
						try
						{
							return t != null && t.Namespace == "FrooxEngine.Headless";
						}
						catch
						{
							return false;
						}
					});
				});
				_isHeadless = valueOrDefault;
				return valueOrDefault;
			}
			return valueOrDefault;
		}
	}

	public static IEnumerable<ResoniteModBase> Mods()
	{
		return LoadedMods.AsReadOnly();
	}

	internal static void LoadMods()
	{
		ModLoaderConfiguration modLoaderConfiguration = ModLoaderConfiguration.Get();
		if (modLoaderConfiguration.NoMods)
		{
			Logger.DebugInternal("Mods will not be loaded due to configuration file");
			return;
		}
		LoadProgressIndicator.SetSubphase("Gathering mods");
		AssemblyFile[] array = AssemblyLoader.LoadAssembliesFromDir("rml_mods");
		if (array == null)
		{
			return;
		}
		AssemblyFile[] array2 = array;
		ModConfiguration.EnsureDirectoryExists();
		AssemblyFile[] array3 = array2;
		foreach (AssemblyFile assemblyFile in array3)
		{
			try
			{
				ResoniteMod resoniteMod = InitializeMod(assemblyFile);
				if (resoniteMod != null)
				{
					RegisterMod(resoniteMod);
				}
			}
			catch (ReflectionTypeLoadException ex)
			{
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.AppendLine(ex.ToString());
				Exception[] loaderExceptions = ex.LoaderExceptions;
				foreach (Exception ex2 in loaderExceptions)
				{
					StringBuilder stringBuilder2 = stringBuilder;
					IFormatProvider invariantCulture = CultureInfo.InvariantCulture;
					StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(18, 1, stringBuilder2, invariantCulture);
					handler.AppendLiteral("Loader Exception: ");
					handler.AppendFormatted(ex2?.Message);
					stringBuilder2.AppendLine(invariantCulture, ref handler);
					if (ex2 is FileNotFoundException ex3 && !string.IsNullOrEmpty(ex3.FusionLog))
					{
						stringBuilder.Append("    Fusion Log:\n    ");
						stringBuilder.AppendLine(ex3.FusionLog);
					}
				}
				Logger.ErrorInternal($"ReflectionTypeLoadException initializing mod from {assemblyFile.File}:\n{stringBuilder}");
			}
			catch (Exception value)
			{
				Logger.ErrorInternal($"Unexpected exception initializing mod from {assemblyFile.File}:\n{value}");
			}
		}
		foreach (ResoniteMod loadedMod in LoadedMods)
		{
			try
			{
				HookMod(loadedMod);
			}
			catch (Exception value2)
			{
				Logger.ErrorInternal($"Unexpected exception in OnEngineInit() for mod {loadedMod.Name} from {loadedMod.ModAssembly?.File ?? "Unknown Assembly"}:\n{value2}");
			}
		}
		if (modLoaderConfiguration.LogConflicts)
		{
			LoadProgressIndicator.SetSubphase("Looking for conflicts");
			foreach (MethodBase patchedMethod in Harmony.GetAllPatchedMethods())
			{
				Patches patchInfo = Harmony.GetPatchInfo(patchedMethod);
				HashSet<string> hashSet = new HashSet<string>();
				foreach (string owner2 in patchInfo.Owners)
				{
					hashSet.Add(owner2);
				}
				HashSet<string> hashSet2 = hashSet;
				if (hashSet2.Count > 1)
				{
					Logger.WarnInternal("Method \"" + GeneralExtensions.FullDescription(patchedMethod) + "\" has been patched by the following:");
					foreach (string item in hashSet2)
					{
						Logger.WarnInternal($"    \"{item}\" ({TypesForOwner(patchInfo, item)})");
					}
				}
				else if (modLoaderConfiguration.Debug)
				{
					string owner = hashSet2.FirstOrDefault();
					Logger.DebugFuncInternal(() => $"Method \"{GeneralExtensions.FullDescription(patchedMethod)}\" has been patched by \"{owner}\"");
				}
			}
		}
		Logger.DebugInternal("Mod loading finished");
	}

	private static void RegisterMod(ResoniteMod mod)
	{
		if (mod.ModAssembly == null)
		{
			throw new ArgumentException("Cannot register a mod before it's properly initialized.");
		}
		try
		{
			ModNameLookupMap.Add(mod.Name, mod);
		}
		catch (ArgumentException)
		{
			ResoniteModBase resoniteModBase = ModNameLookupMap[mod.Name];
			Logger.ErrorInternal($"{mod.ModAssembly?.File} declares duplicate mod {mod.Name} already declared in {resoniteModBase.ModAssembly?.File ?? "Unknown Assembly"}. The new mod will be ignored.");
			return;
		}
		LoadedMods.Add(mod);
		AssemblyLookupMap.Add(mod.ModAssembly.Assembly, mod);
		mod.FinishedLoading = true;
	}

	private static string TypesForOwner(Patches patches, string owner)
	{
		int value = patches.Prefixes.Where(ownerEquals).Count();
		int value2 = patches.Postfixes.Where(ownerEquals).Count();
		int value3 = patches.Transpilers.Where(ownerEquals).Count();
		int value4 = patches.Finalizers.Where(ownerEquals).Count();
		return $"prefix={value}; postfix={value2}; transpiler={value3}; finalizer={value4}";
		bool ownerEquals(Patch patch)
		{
			return object.Equals(patch.owner, owner);
		}
	}

	private static ResoniteMod? InitializeMod(AssemblyFile mod)
	{
		if (mod.Assembly == null)
		{
			return null;
		}
		Type[] array = mod.Assembly.GetLoadableTypes((Type t) => t.IsClass && !t.IsAbstract && RESONITE_MOD_TYPE.IsAssignableFrom(t)).ToArray();
		if (array.Length == 0)
		{
			Logger.ErrorInternal("No loadable mod found in " + mod.File);
			return null;
		}
		if (array.Length != 1)
		{
			Logger.ErrorInternal("More than one mod found in " + mod.File + ". File will not be loaded.");
			return null;
		}
		Type type = array[0];
		ResoniteMod resoniteMod = null;
		try
		{
			resoniteMod = (ResoniteMod)AccessTools.CreateInstance(type);
		}
		catch (Exception value)
		{
			Logger.ErrorInternal($"Error instantiating mod {type.FullName} from {mod.File}:\n{value}");
			return null;
		}
		if (resoniteMod == null)
		{
			Logger.ErrorInternal("Unexpected null instantiating mod " + type.FullName + " from " + mod.File);
			return null;
		}
		resoniteMod.ModAssembly = mod;
		resoniteMod.IsLocalized = LocaleLoader.ContainsLocales(resoniteMod);
		Logger.MsgInternal($"Loaded mod [{resoniteMod.Name}/{resoniteMod.Version}] ({Path.GetFileName(mod.File)}) by {resoniteMod.Author} with Sha256: {mod.Sha256}");
		LoadProgressIndicator.SetSubphase($"Loading configuration for [{resoniteMod.Name}/{resoniteMod.Version}]");
		resoniteMod.ModConfiguration = ModConfiguration.LoadConfigForMod(resoniteMod);
		return resoniteMod;
	}

	private static void HookMod(ResoniteMod mod)
	{
		LoadProgressIndicator.SetSubphase($"Starting mod [{mod.Name}/{mod.Version}]");
		Logger.DebugFuncInternal(() => $"calling OnEngineInit() for [{mod.Name}/{mod.Version}]");
		try
		{
			mod.OnEngineInit();
		}
		catch (Exception value)
		{
			Logger.ErrorInternal($"Mod {mod.Name} from {mod.ModAssembly?.File ?? "Unknown Assembly"} threw error from OnEngineInit():\n{value}");
		}
	}
}
