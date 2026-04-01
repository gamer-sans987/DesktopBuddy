using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Elements.Core;
using Elements.Data;
using EnumsNET;
using FrooxEngine.ProtoFlux;

namespace FrooxEngine;

public static class WorkerInitializer
{
	private static Dictionary<Type, FeatureUpgradeReplacement> featureFlagReplacements = new Dictionary<Type, FeatureUpgradeReplacement>();

	private static Type[] workers;

	private static Dictionary<Type, List<Type>> genericInfos = new Dictionary<Type, List<Type>>();

	private static ConcurrentDictionary<Type, WorkerInitInfo> initInfos = new ConcurrentDictionary<Type, WorkerInitInfo>();

	private static bool initialized;

	public static CategoryNode<Type> ComponentLibrary { get; private set; }

	/// <summary>
	/// List of all found workers in the FrooxEngine
	/// </summary>
	public static IEnumerable<Type> Workers => workers;

	internal static WorkerInitInfo GetInitInfo(Type workerType)
	{
		if (!initInfos.TryGetValue(workerType, out WorkerInitInfo value))
		{
			try
			{
				value = Initialize(workerType);
				initInfos.TryAdd(workerType, value);
			}
			catch (TypeLoadException innerException)
			{
				throw new Exception($"Exception loading type {workerType}", innerException);
			}
		}
		return value;
	}

	internal static WorkerInitInfo GetInitInfo(IWorker worker)
	{
		return GetInitInfo(worker.GetType());
	}

	public static IEnumerable<Type> GetCommonGenericTypes(Type type)
	{
		if (!genericInfos.TryGetValue(type, out List<Type> value))
		{
			value = new List<Type>();
			if (type.GetCustomAttributes(typeof(GenericTypesAttribute), inherit: true).FirstOrDefault() is GenericTypesAttribute genericTypesAttribute)
			{
				foreach (Type type2 in genericTypesAttribute.Types)
				{
					if (genericTypesAttribute.Mode == GenericTypesAttribute.TypeMode.DirectTypes)
					{
						try
						{
							Type item = type.MakeGenericType(type2);
							value.Add(item);
						}
						catch (ArgumentException)
						{
						}
						continue;
					}
					foreach (Type derivedType in GetDerivedTypes(type2))
					{
						try
						{
							Type item2 = type.MakeGenericType(derivedType);
							value.Add(item2);
						}
						catch (ArgumentException)
						{
						}
					}
				}
			}
			genericInfos.Add(type, value);
		}
		return value;
	}

	private static IEnumerable<Type> GetDerivedTypes(Type baseType)
	{
		List<Type> list = new List<Type>();
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
		foreach (Assembly assembly in assemblies)
		{
			try
			{
				if (assembly.IsDynamic)
				{
					continue;
				}
				Type[] exportedTypes = assembly.GetExportedTypes();
				foreach (Type type in exportedTypes)
				{
					if (!type.IsAbstract && baseType.IsAssignableFrom(type))
					{
						list.Add(type);
					}
				}
			}
			catch (Exception ex)
			{
				UniLog.Error($"Exception getting types from assembly {assembly}:\n" + ex);
			}
		}
		return list;
	}

	public static Type ResolveReplacement(Type type, VersionNumber version, IReadOnlyDictionary<string, int> featureFlags)
	{
		if (type == null)
		{
			throw new ArgumentNullException("type");
		}
		if (featureFlagReplacements.TryGetValue(type, out FeatureUpgradeReplacement value) && value.NeedsUpgrade(featureFlags))
		{
			Type newType = value.GetNewType(type);
			if (newType != type && newType != null)
			{
				return ResolveReplacement(newType, version, featureFlags);
			}
			return type;
		}
		if (type.IsGenericType && !type.IsGenericTypeDefinition)
		{
			Type genericTypeDefinition = type.GetGenericTypeDefinition();
			Type type2 = ResolveReplacement(genericTypeDefinition, version, featureFlags);
			if (type2 != genericTypeDefinition)
			{
				Type[] genericArguments = type.GetGenericArguments();
				for (int i = 0; i < genericArguments.Length; i++)
				{
					genericArguments[i] = ResolveReplacement(genericArguments[i], version, featureFlags);
				}
				type = type2.MakeGenericType(genericArguments);
			}
		}
		return type;
	}

	public static void Initialize(List<Type> allTypes, bool verbose)
	{
		if (initialized)
		{
			throw new Exception("Cannot initialize WorkerInitializer twice!");
		}
		initialized = true;
		if (verbose)
		{
			UniLog.Log("Extracting Workers");
		}
		workers = allTypes.Where((Type t) => t.IsClass && !t.IsAbstract && typeof(Worker).IsAssignableFrom(t)).ToArray();
		if (verbose)
		{
			UniLog.Log("Initializing non-generic workers");
		}
		ComponentLibrary = new CategoryNode<Type>((Type t) => t.Name);
		List<Type> list = new List<Type>();
		foreach (Type allType in allTypes)
		{
			if (!allType.IsClass || allType.IsAbstract || !typeof(Worker).IsAssignableFrom(allType))
			{
				continue;
			}
			list.Add(allType);
			foreach (FeatureUpgradeReplacement customAttribute in allType.GetCustomAttributes<FeatureUpgradeReplacement>(inherit: false))
			{
				featureFlagReplacements.Add(allType, customAttribute);
			}
			if (!typeof(Component).IsAssignableFrom(allType))
			{
				continue;
			}
			string[] array = (allType.GetCustomAttributes(typeof(CategoryAttribute), inherit: true).FirstOrDefault() as CategoryAttribute)?.Paths ?? new string[1] { "Uncategorized" };
			foreach (string text in array)
			{
				if (text != "Hidden")
				{
					ComponentLibrary.GetSubcategory(text).AddElement(allType);
				}
			}
		}
		workers = list.ToArray();
		ProtoFluxHelper.Initialize(workers);
		GizmoHelper.Initialize(workers);
	}

	private static void BuildCategoryTree(StringBuilder str, CategoryNode<Type> cat, int level)
	{
		foreach (CategoryNode<Type> subcategory in cat.Subcategories)
		{
			for (int i = 0; i < level; i++)
			{
				str.Append(" ");
			}
			str.AppendLine("+ " + subcategory.Name);
			BuildCategoryTree(str, subcategory, level + 1);
		}
		foreach (Type element in cat.Elements)
		{
			for (int j = 0; j < level; j++)
			{
				str.Append(" ");
			}
			str.AppendLine("- " + element.Name);
		}
	}

	private static bool IsValidField(FieldInfo field, Type workerType)
	{
		if (typeof(ISyncMember).IsAssignableFrom(field.FieldType))
		{
			if (field.FieldType.IsInterface)
			{
				return false;
			}
			if (!field.IsInitOnly)
			{
				return false;
			}
			if (field.FieldType.IsAbstract)
			{
				throw new Exception($"Field {field.Name} on Worker {workerType} is abstract type {field.FieldType}");
			}
			return true;
		}
		return false;
	}

	private static void GatherWorkerFields(Type workerType, List<FieldInfo> fields)
	{
		if (workerType.BaseType != typeof(Worker))
		{
			GatherWorkerFields(workerType.BaseType, fields);
		}
		List<FieldInfo> collection = (from f in workerType.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			where IsValidField(f, workerType)
			select f).ToList();
		fields.AddRange(collection);
	}

	private static WorkerInitInfo Initialize(Type workerType)
	{
		WorkerInitInfo workerInitInfo = new WorkerInitInfo();
		List<FieldInfo> list = new List<FieldInfo>();
		GatherWorkerFields(workerType, list);
		workerInitInfo.syncMemberFields = list.ToArray();
		workerInitInfo.syncMemberNames = new string[workerInitInfo.syncMemberFields.Length];
		workerInitInfo.syncMemberNonpersitent = new bool[workerInitInfo.syncMemberFields.Length];
		workerInitInfo.syncMemberNondrivable = new bool[workerInitInfo.syncMemberFields.Length];
		workerInitInfo.syncMemberDontCopy = new bool[workerInitInfo.syncMemberFields.Length];
		workerInitInfo.defaultValues = new object[workerInitInfo.syncMemberFields.Length];
		workerInitInfo.syncMemberNameToIndex = new Dictionary<string, int>();
		for (int i = 0; i < workerInitInfo.syncMemberFields.Length; i++)
		{
			FieldInfo fieldInfo = workerInitInfo.syncMemberFields[i];
			workerInitInfo.syncMemberNonpersitent[i] = fieldInfo.GetCustomAttribute<NonPersistent>() != null;
			workerInitInfo.syncMemberNondrivable[i] = fieldInfo.GetCustomAttribute<NonDrivable>() != null;
			workerInitInfo.syncMemberDontCopy[i] = fieldInfo.GetCustomAttribute<DontCopy>() != null;
			string text = null;
			object[] customAttributes = fieldInfo.GetCustomAttributes(inherit: true);
			for (int j = 0; j < customAttributes.Length; j++)
			{
				if (customAttributes[j] is NameOverride nameOverride)
				{
					text = nameOverride.Name;
					break;
				}
			}
			if (text == null)
			{
				text = fieldInfo.Name;
				if (text.EndsWith("_Field") && text != "_Field")
				{
					text = text.Substring(0, text.LastIndexOf("_Field"));
				}
			}
			workerInitInfo.syncMemberNames[i] = text;
			workerInitInfo.syncMemberNameToIndex.Add(text, i);
			foreach (OldName item2 in fieldInfo.GetCustomAttributes(typeof(OldName), inherit: false).Cast<OldName>())
			{
				if (workerInitInfo.oldSyncMemberNames == null)
				{
					workerInitInfo.oldSyncMemberNames = new Dictionary<string, List<string>>();
				}
				if (!workerInitInfo.oldSyncMemberNames.TryGetValue(text, out List<string> value))
				{
					value = new List<string>();
					workerInitInfo.oldSyncMemberNames.Add(text, value);
				}
				string[] oldNames = item2.OldNames;
				foreach (string item in oldNames)
				{
					value.Add(item);
				}
			}
			if (fieldInfo.GetCustomAttributes(typeof(DefaultValue), inherit: false).FirstOrDefault() is DefaultValue defaultValue)
			{
				workerInitInfo.defaultValues[i] = defaultValue.Default;
			}
		}
		if (typeof(IComponentBase).IsAssignableFrom(workerType))
		{
			Type methodOrigin = workerType.FindGenericBaseClass(typeof(ComponentBase<>));
			workerInitInfo.HasUpdateMethods = workerType.OverridesMethod("OnCommonUpdate", methodOrigin) | workerType.OverridesMethod("OnBehaviorUpdate", methodOrigin);
			workerInitInfo.HasLinkedMethod = workerType.OverridesMethod("OnLinked", methodOrigin);
			workerInitInfo.HasUnlinkedMethod = workerType.OverridesMethod("OnUnlinked", methodOrigin);
			workerInitInfo.HasAudioUpdateMethod = workerType.OverridesMethod("OnAudioUpdate", methodOrigin);
			workerInitInfo.HasAudioConfigurationChangedMethod = workerType.OverridesMethod("OnAudioConfigurationChanged", methodOrigin);
			workerInitInfo.ReceivesWorldEvent = new bool[Enums.GetMemberCount<World.WorldEvent>()];
			foreach (World.WorldEvent value2 in Enums.GetValues<World.WorldEvent>())
			{
				if (workerType.OverridesMethod(value2.ToString(), methodOrigin))
				{
					workerInitInfo.ReceivesWorldEvent[(int)value2] = true;
					workerInitInfo.ReceivesAnyWorldEvent = true;
				}
			}
		}
		SyncMethodInfo[] array = (from m in workerType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
			where m.GetCustomAttribute<SyncMethod>() != null
			select new SyncMethodInfo(m)).ToArray();
		SyncMethodInfo[] array2 = (from m in workerType.GetMethods(BindingFlags.Static | BindingFlags.Public)
			where m.GetCustomAttribute<SyncMethod>() != null
			select new SyncMethodInfo(m)).ToArray();
		if (array.Length != 0)
		{
			workerInitInfo.syncMethods = array;
		}
		if (array2.Length != 0)
		{
			workerInitInfo.staticSyncMethods = array2;
		}
		workerInitInfo.SingleInstancePerSlot = workerType.GetCustomAttribute<SingleInstancePerSlot>(inherit: true, fromInterfaces: true) != null;
		workerInitInfo.DontDuplicate = workerType.GetCustomAttribute<DontDuplicateAttribute>(inherit: true, fromInterfaces: true) != null;
		workerInitInfo.PreserveWithAssets = workerType.GetCustomAttribute<PreserveWithAssetsAttribute>(inherit: true, fromInterfaces: true) != null;
		workerInitInfo.RegisterGlobally = workerType.GetCustomAttribute<GloballyRegisteredAttribute>(inherit: true, fromInterfaces: true) != null;
		workerInitInfo.CategoryPath = workerType.GetCustomAttribute<CategoryAttribute>()?.Paths?.FirstOrDefault();
		workerInitInfo.GroupingName = workerType.GetCustomAttribute<GroupingAttribute>(inherit: true, fromInterfaces: false)?.GroupName;
		if (workerInitInfo.PreserveWithAssets)
		{
			UniLog.Log("PreserveWithAssets: " + workerType);
		}
		workerInitInfo.DefaultUpdateOrder = workerType.GetCustomAttribute<DefaultUpdateOrder>()?.Order ?? 0;
		return workerInitInfo;
	}
}
