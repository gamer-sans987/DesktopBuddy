using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Elements.Core;
using Elements.Data;
using Elements.Quantity;
using SpookilySharp;

namespace FrooxEngine;

public static class GlobalTypeRegistry
{
	private readonly struct SystemType
	{
		public readonly Type type;

		public readonly string alias;

		public SystemType(Type type, string alias)
		{
			this.type = type;
			this.alias = alias;
		}
	}

	/// <summary>
	/// This indicates the general compatibility version between the system types. This can be used to manually break
	/// compatibility between builds when changes are made that do not add or remove any data types, but that in some
	/// other way make the two versions incompatible.
	///
	/// An example of this might be when algorithms responsible for realtime encoding of the types are changed, so even
	/// with the same set of types, the two clients wouldn't compatible at the binary level. These changes should be rare
	/// so this shouldn't update very often.
	///
	/// Adding, removing or reordering types doesn't require this value to be changed, because that will change
	/// the compatibility hash implicitly.
	/// </summary>
	public const int SYSTEM_COMPATIBILITY_VERSION = 3;

	private static List<SystemType> _systemTypes = new List<SystemType>();

	private static Dictionary<Type, int> _systemTypeIndexes = new Dictionary<Type, int>();

	private static Dictionary<string, Type> _nameToSystemType = new Dictionary<string, Type>();

	private static Dictionary<string, AssemblyTypeRegistry> _byName = new Dictionary<string, AssemblyTypeRegistry>();

	private static Dictionary<Assembly, AssemblyTypeRegistry> _byAssembly = new Dictionary<Assembly, AssemblyTypeRegistry>();

	private static bool _finalized;

	private static List<AssemblyTypeRegistry> _coreAssemblies = new List<AssemblyTypeRegistry>();

	private static List<AssemblyTypeRegistry> _userspaceCoreAssemblies = new List<AssemblyTypeRegistry>();

	private static HashSet<Type> _externalTypesToRegister = new HashSet<Type>();

	private static Dictionary<long, Type> _oldTypesByHash = new Dictionary<long, Type>();

	private static object _lock = new object();

	public static int SystemTypeCount => _systemTypes.Count;

	public static string SystemCompatibilityHash { get; private set; }

	public static string MetadataCachePath { get; private set; }

	public static bool FastCompatibilityHash { get; private set; }

	public static IReadOnlyList<AssemblyTypeRegistry> CoreAssemblies => _coreAssemblies;

	public static IReadOnlyList<AssemblyTypeRegistry> UserspaceCoreAssemblies => _userspaceCoreAssemblies;

	public static IEnumerable<AssemblyTypeRegistry> DataModelAssemblies => _byAssembly.Values.Where((AssemblyTypeRegistry a) => !a.IsDependency);

	public static IEnumerable<AssemblyTypeRegistry> DependencyAssemblies => _byAssembly.Values.Where((AssemblyTypeRegistry a) => a.IsDependency);

	public static void Initialize(string metadataCachePath, bool fastCompatibilityHash)
	{
		MetadataCachePath = metadataCachePath;
		FastCompatibilityHash = fastCompatibilityHash;
		if (metadataCachePath != null)
		{
			Directory.CreateDirectory(metadataCachePath);
		}
		foreach (Type baseEnginePrimitive in Coder.BaseEnginePrimitives)
		{
			RegisterSystemType(baseEnginePrimitive);
		}
		Assembly assembly = typeof(IQuantity).Assembly;
		RegisterSystemType(typeof(IQuantity));
		RegisterSystemType(typeof(IQuantity<>));
		RegisterSystemType(typeof(IQuantitySI));
		RegisterSystemType(typeof(IQuantitySI<>));
		Type[] types = assembly.GetTypes();
		foreach (Type type in types)
		{
			if (type.IsValueType && typeof(IQuantity).IsAssignableFrom(type))
			{
				RegisterSystemType(type);
			}
		}
		RegisterSystemType(typeof(Nullable<>));
		RegisterSystemType(typeof(object));
		RegisterSystemType(typeof(void));
		RegisterSystemType(typeof(Type));
		RegisterSystemType(typeof(Guid));
		RegisterSystemType(typeof(DateTimeKind));
		RegisterSystemType(typeof(DayOfWeek));
		RegisterSystemType(typeof(StringComparison));
		RegisterSystemType(typeof(IFormatProvider));
		RegisterSystemType(typeof(NumberStyles));
		RegisterSystemType(typeof(CultureInfo));
		RegisterSystemType(typeof(HttpStatusCode));
		RegisterSystemType(typeof(Task));
		RegisterSystemType(typeof(Task<>));
		RegisterSystemType(typeof(Delegate));
		RegisterSystemType(typeof(Action));
		RegisterSystemType(typeof(Action<>));
		RegisterSystemType(typeof(Action<, >));
		RegisterSystemType(typeof(Action<, , >));
		RegisterSystemType(typeof(Action<, , , >));
		RegisterSystemType(typeof(Action<, , , , >));
		RegisterSystemType(typeof(Action<, , , , , >));
		RegisterSystemType(typeof(Action<, , , , , , >));
		RegisterSystemType(typeof(Action<, , , , , , , >));
		RegisterSystemType(typeof(Action<, , , , , , , , >));
		RegisterSystemType(typeof(Action<, , , , , , , , , >));
		RegisterSystemType(typeof(Action<, , , , , , , , , , >));
		RegisterSystemType(typeof(Action<, , , , , , , , , , , >));
		RegisterSystemType(typeof(Action<, , , , , , , , , , , , >));
		RegisterSystemType(typeof(Action<, , , , , , , , , , , , , >));
		RegisterSystemType(typeof(Action<, , , , , , , , , , , , , , >));
		RegisterSystemType(typeof(Action<, , , , , , , , , , , , , , , >));
		RegisterSystemType(typeof(Func<>));
		RegisterSystemType(typeof(Func<, >));
		RegisterSystemType(typeof(Func<, , >));
		RegisterSystemType(typeof(Func<, , , >));
		RegisterSystemType(typeof(Func<, , , , >));
		RegisterSystemType(typeof(Func<, , , , , >));
		RegisterSystemType(typeof(Func<, , , , , , >));
		RegisterSystemType(typeof(Func<, , , , , , , >));
		RegisterSystemType(typeof(Func<, , , , , , , , >));
		RegisterSystemType(typeof(Func<, , , , , , , , , >));
		RegisterSystemType(typeof(Func<, , , , , , , , , , >));
		RegisterSystemType(typeof(Func<, , , , , , , , , , , >));
		RegisterSystemType(typeof(Func<, , , , , , , , , , , , >));
		RegisterSystemType(typeof(Func<, , , , , , , , , , , , , >));
		RegisterSystemType(typeof(Func<, , , , , , , , , , , , , , >));
		RegisterSystemType(typeof(Func<, , , , , , , , , , , , , , , >));
		RegisterSystemType(typeof(Func<, , , , , , , , , , , , , , , , >));
		RegisterSystemType(typeof(Predicate<>));
	}

	public static bool IsSystemType(Type type)
	{
		return _systemTypeIndexes.ContainsKey(type);
	}

	public static int TryGetSystemTypeIndex(Type type)
	{
		if (_systemTypeIndexes.TryGetValue(type, out var value))
		{
			return value;
		}
		return -1;
	}

	public static Type TryGetSystemType(string name)
	{
		if (_nameToSystemType.TryGetValue(name, out Type value))
		{
			return value;
		}
		return null;
	}

	public static Type TryGetOldTypeByHash(string name)
	{
		long key = name.SpookyHash64();
		if (_oldTypesByHash.TryGetValue(key, out Type value))
		{
			return value;
		}
		return null;
	}

	public static Type GetSystemType(int index)
	{
		return _systemTypes[index].type;
	}

	public static string GetSystemTypeName(int index)
	{
		return _systemTypes[index].alias;
	}

	public static string GetSystemTypeName(Type type)
	{
		int num = TryGetSystemTypeIndex(type);
		if (num < 0)
		{
			throw new ArgumentException($"Type {type} is not a system type!");
		}
		return GetSystemTypeName(num);
	}

	public static string TryGetSystemTypeName(Type type)
	{
		int num = TryGetSystemTypeIndex(type);
		if (num < 0)
		{
			return null;
		}
		return GetSystemTypeName(num);
	}

	private static void RegisterSystemType(Type type, string alias = null)
	{
		CheckFinalized();
		if (alias == null)
		{
			alias = (type.IsGenericType ? type.Name : type.GetNiceName());
		}
		_systemTypeIndexes.Add(type, SystemTypeCount);
		_nameToSystemType.Add(alias, type);
		_systemTypes.Add(new SystemType(type, alias));
	}

	internal static AssemblyTypeRegistry RegisterMovedType(Type type, string typename, string assembly)
	{
		if (!_byName.TryGetValue(assembly, out AssemblyTypeRegistry value))
		{
			value = new AssemblyTypeRegistry(assembly);
			_byName.Add(assembly, value);
		}
		value.RegisterMovedType(typename, type);
		return value;
	}

	internal static void RegisterOldTypeHash(long hash, Type type)
	{
		_oldTypesByHash.Add(hash, type);
	}

	internal static void FinalizeTypes()
	{
		RegisterDeferredExternalTypes();
		using (ContinuousHasher<MD5CryptoServiceProvider> continuousHasher = new ContinuousHasher<MD5CryptoServiceProvider>())
		{
			BinaryWriter binaryWriter = new BinaryWriter(continuousHasher);
			binaryWriter.Write(3);
			foreach (SystemType systemType in _systemTypes)
			{
				binaryWriter.Write(systemType.type.FullName);
			}
			binaryWriter.Flush();
			SystemCompatibilityHash = Convert.ToBase64String(continuousHasher.FinalizeHash());
		}
		_coreAssemblies.Sort((AssemblyTypeRegistry a, AssemblyTypeRegistry b) => string.Compare(a.AssemblyName, b.AssemblyName, StringComparison.InvariantCulture));
		foreach (KeyValuePair<Assembly, AssemblyTypeRegistry> item in _byAssembly)
		{
			item.Value.ProcessMovedTypes();
		}
		_finalized = true;
	}

	internal static void RegisterExternalType(Type type)
	{
		CheckFinalized();
		if (type.Assembly.GetCustomAttribute<DataModelAssemblyAttribute>() != null)
		{
			throw new InvalidOperationException($"Trying to register {type} as external type, but is belongs to a data model assembly {type.Assembly.FullName}");
		}
		lock (_externalTypesToRegister)
		{
			_externalTypesToRegister.Add(type);
		}
	}

	private static void RegisterDeferredExternalTypes()
	{
		CheckFinalized();
		List<Type> list = _externalTypesToRegister.ToList();
		_externalTypesToRegister = null;
		list.Sort(delegate(Type a, Type b)
		{
			int num = string.Compare(a.Assembly.GetName().Name, b.Assembly.GetName().Name, StringComparison.InvariantCulture);
			return (num != 0) ? num : string.Compare(a.Name, b.Name, StringComparison.InvariantCulture);
		});
		foreach (Type item in list)
		{
			if (!_byAssembly.TryGetValue(item.Assembly, out AssemblyTypeRegistry value))
			{
				value = new AssemblyTypeRegistry(item.Assembly, DataModelAssemblyType.Dependency, null, scanTypes: false);
				value = RegisterAssemblyRegistry(value);
			}
			value.RegisterExternalType(item);
		}
	}

	public static AssemblyTypeRegistry RegisterAssembly(Assembly assembly, DataModelAssemblyType assemblyType, IEnumerable<Type> types = null)
	{
		CheckFinalized();
		return RegisterAssemblyRegistry(new AssemblyTypeRegistry(assembly, assemblyType, types, scanTypes: true, FastCompatibilityHash));
	}

	private static AssemblyTypeRegistry RegisterAssemblyRegistry(AssemblyTypeRegistry registry)
	{
		CheckFinalized();
		lock (_lock)
		{
			if (_byAssembly.TryGetValue(registry.Assembly, out AssemblyTypeRegistry value))
			{
				return value;
			}
			_byName.Add(registry.AssemblyName, registry);
			_byAssembly.Add(registry.Assembly, registry);
			if (registry.AssemblyType == DataModelAssemblyType.Core)
			{
				_coreAssemblies.Add(registry);
			}
			else if (registry.AssemblyType == DataModelAssemblyType.UserspaceCore)
			{
				_userspaceCoreAssemblies.Add(registry);
			}
			return registry;
		}
	}

	public static AssemblyTypeRegistry GetTypeRegistry(string name)
	{
		if (!_byName.TryGetValue(name, out AssemblyTypeRegistry value))
		{
			throw new Exception("Assembly " + name + " has not been registered");
		}
		return value;
	}

	public static AssemblyTypeRegistry TryGetTypeRegistry(string name)
	{
		if (_byName.TryGetValue(name, out AssemblyTypeRegistry value))
		{
			return value;
		}
		return null;
	}

	public static AssemblyTypeRegistry GetTypeRegistry(Assembly assembly)
	{
		if (!_byAssembly.TryGetValue(assembly, out AssemblyTypeRegistry value))
		{
			throw new Exception("Assembly " + assembly.FullName + " has not been registered");
		}
		return value;
	}

	public static Type GetType(string assemblyName, string typeName)
	{
		return GetTypeRegistry(assemblyName).GetType(typeName);
	}

	public static bool IsSupportedType(Type type)
	{
		if (IsSystemType(type))
		{
			return true;
		}
		if (type.IsGenericType && !type.IsGenericTypeDefinition)
		{
			if (!IsSupportedType(type.GetGenericTypeDefinition()))
			{
				return false;
			}
			Type[] genericTypeArguments = type.GenericTypeArguments;
			foreach (Type type2 in genericTypeArguments)
			{
				if (!type2.IsGenericParameter && !IsSupportedType(type2))
				{
					return false;
				}
			}
			return true;
		}
		if (!_byAssembly.TryGetValue(type.Assembly, out AssemblyTypeRegistry value))
		{
			return false;
		}
		return value.HasType(type);
	}

	public static void ValidateAllTypes(HashSet<Type> invalidTypes, IEngineInitProgress progress = null)
	{
		lock (_lock)
		{
			foreach (AssemblyTypeRegistry coreAssembly in _coreAssemblies)
			{
				coreAssembly.ValidateTypes(invalidTypes, progress);
			}
		}
	}

	private static void CheckFinalized()
	{
		if (_finalized)
		{
			throw new InvalidOperationException("Type registry has been finalized, register any additional types");
		}
	}
}
