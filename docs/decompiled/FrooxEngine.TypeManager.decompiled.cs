using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Elements.Core;
using Elements.Quantity;

namespace FrooxEngine;

public class TypeManager
{
	public const string TYPE_MANAGEMENT_FLAG = "TypeManagement";

	internal static string LegacyMatch = Encoding.UTF8.GetString(new byte[4] { 78, 101, 111, 115 });

	internal static string LegacyMatchCloud = Encoding.UTF8.GetString(new byte[13]
	{
		67, 108, 111, 117, 100, 88, 46, 83, 104, 97,
		114, 101, 100
	});

	internal static string LegacyMatchCore = Encoding.UTF8.GetString(new byte[5] { 66, 97, 115, 101, 88 });

	internal static string LegacyMatchAssets = Encoding.UTF8.GetString(new byte[5] { 67, 111, 100, 101, 88 });

	internal static string LegacyMatchColor = LegacyMatchCore + ".color";

	private Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();

	private Dictionary<string, Type> _legacyTypeCache = new Dictionary<string, Type>();

	private List<AssemblyTypeRegistry> _allowedAssemblies = new List<AssemblyTypeRegistry>();

	private Dictionary<string, int> _assemblyNameToIndex = new Dictionary<string, int>();

	private Dictionary<Assembly, int> _assemblyToIndex = new Dictionary<Assembly, int>();

	private Dictionary<string, AssemblyTypeRegistry> _movedTypeAssemblies = new Dictionary<string, AssemblyTypeRegistry>();

	private Dictionary<Type, bool> _supportedTypes = new Dictionary<Type, bool>();

	public World World { get; private set; }

	public IEnumerable<AssemblyTypeRegistry> AllowedAssemblies => _allowedAssemblies;

	public string CompatibilityHash { get; private set; }

	internal void InitializeAssemblies(IEnumerable<AssemblyTypeRegistry> assemblies)
	{
		if (_allowedAssemblies.Count > 0)
		{
			throw new InvalidOperationException("Assemblies were already initialized");
		}
		HashSet<AssemblyTypeRegistry> registered = new HashSet<AssemblyTypeRegistry>();
		foreach (AssemblyTypeRegistry assembly in assemblies)
		{
			Register(assembly);
		}
		for (int i = 0; i < _allowedAssemblies.Count; i++)
		{
			foreach (Assembly dependencyAssembly in _allowedAssemblies[i].DependencyAssemblies)
			{
				AssemblyTypeRegistry typeRegistry = GlobalTypeRegistry.GetTypeRegistry(dependencyAssembly);
				if (registered.Add(typeRegistry))
				{
					Register(typeRegistry);
				}
			}
		}
		foreach (AssemblyTypeRegistry allowedAssembly in _allowedAssemblies)
		{
			foreach (AssemblyTypeRegistry movedTypeAssembly in allowedAssembly.MovedTypeAssemblies)
			{
				if (!_assemblyNameToIndex.ContainsKey(movedTypeAssembly.AssemblyName) && !_movedTypeAssemblies.ContainsKey(movedTypeAssembly.AssemblyName))
				{
					_movedTypeAssemblies.Add(movedTypeAssembly.AssemblyName, movedTypeAssembly);
				}
			}
		}
		using (ContinuousHasher<MD5CryptoServiceProvider> continuousHasher = new ContinuousHasher<MD5CryptoServiceProvider>())
		{
			BinaryWriter binaryWriter = new BinaryWriter(continuousHasher);
			binaryWriter.Write(GlobalTypeRegistry.SystemCompatibilityHash);
			foreach (AssemblyTypeRegistry allowedAssembly2 in _allowedAssemblies)
			{
				if (!allowedAssembly2.IsDependency)
				{
					binaryWriter.Write(allowedAssembly2.CompatibilityHash);
				}
			}
			binaryWriter.Flush();
			byte[] inArray = continuousHasher.FinalizeHash();
			CompatibilityHash = Convert.ToBase64String(inArray);
		}
		void Register(AssemblyTypeRegistry assembly)
		{
			_assemblyNameToIndex.Add(assembly.AssemblyName, _allowedAssemblies.Count);
			_assemblyToIndex.Add(assembly.Assembly, _allowedAssemblies.Count);
			_allowedAssemblies.Add(assembly);
			registered.Add(assembly);
		}
	}

	public TypeManager(World world)
	{
		World = world;
	}

	public void Dispose()
	{
		World = null;
	}

	public bool IsSupported(Type type)
	{
		if (type == null)
		{
			return true;
		}
		if (_supportedTypes.TryGetValue(type, out var value))
		{
			return value;
		}
		value = ComputeIsSupported(type);
		_supportedTypes.Add(type, value);
		return value;
	}

	private bool ComputeIsSupported(Type type)
	{
		if (type.IsGenericType && !type.IsGenericTypeDefinition)
		{
			if (!IsSupported(type.GetGenericTypeDefinition()))
			{
				return false;
			}
			Type[] genericTypeArguments = type.GenericTypeArguments;
			foreach (Type type2 in genericTypeArguments)
			{
				if (!IsSupported(type2))
				{
					return false;
				}
			}
			return true;
		}
		if (GlobalTypeRegistry.IsSystemType(type))
		{
			return true;
		}
		if (!_assemblyToIndex.TryGetValue(type.Assembly, out var value))
		{
			return false;
		}
		return _allowedAssemblies[value].HasType(type);
	}

	public void EncodeType(BinaryWriter writer, Type type, bool isDeconstructing = false)
	{
		int num = GlobalTypeRegistry.TryGetSystemTypeIndex(type);
		if (num >= 0)
		{
			writer.Write7BitEncoded((ulong)num);
		}
		else if (type.IsGenericType && !type.IsGenericTypeDefinition)
		{
			Type genericTypeDefinition = type.GetGenericTypeDefinition();
			EncodeType(writer, genericTypeDefinition, isDeconstructing: true);
			if (!isDeconstructing)
			{
				writer.Write(value: true);
			}
			Type[] genericTypeArguments = type.GenericTypeArguments;
			foreach (Type type2 in genericTypeArguments)
			{
				EncodeType(writer, type2, isDeconstructing: true);
			}
		}
		else
		{
			if (!_assemblyToIndex.TryGetValue(type.Assembly, out var value))
			{
				throw new ArgumentException("Invalid data model type: " + type.FullName);
			}
			int typeIndex = _allowedAssemblies[value].GetTypeIndex(type);
			value += GlobalTypeRegistry.SystemTypeCount;
			writer.Write7BitEncoded((ulong)value);
			writer.Write7BitEncoded((ulong)typeIndex);
			if (type.IsGenericType && !isDeconstructing)
			{
				writer.Write(value: false);
			}
		}
	}

	public Type DecodeType(BinaryReader reader, bool isReconstructing = false)
	{
		int num = (int)reader.Read7BitEncoded();
		Type type;
		if (num < GlobalTypeRegistry.SystemTypeCount)
		{
			type = GlobalTypeRegistry.GetSystemType(num);
		}
		else
		{
			num -= GlobalTypeRegistry.SystemTypeCount;
			int index = (int)reader.Read7BitEncoded();
			if (num < 0 || num >= _allowedAssemblies.Count)
			{
				throw new Exception($"Assembly index is out of range: {num}");
			}
			type = _allowedAssemblies[num].GetType(index);
		}
		if (type.IsGenericTypeDefinition && (isReconstructing || reader.ReadBoolean()))
		{
			Type[] genericArguments = type.GetGenericArguments();
			for (int i = 0; i < genericArguments.Length; i++)
			{
				genericArguments[i] = DecodeType(reader, isReconstructing: true);
			}
			try
			{
				type = type.MakeGenericType(genericArguments);
			}
			catch (Exception)
			{
				UniLog.Log($"Failed to make generic type. IsReconstructing: {isReconstructing}, GenericDefinition: {type}\nArguments: {string.Join(", ", genericArguments.Select((Type t) => t.FullName))}\nAssemblyIndex {num}");
				throw;
			}
		}
		return type;
	}

	public static T Instantiate<T>() where T : IWorker, new()
	{
		return WorkerHelper<T>.New();
	}

	public static IWorker Instantiate(Type type)
	{
		try
		{
			return (IWorker)Activator.CreateInstance(type);
		}
		catch (Exception ex)
		{
			throw new Exception($"Error instantiating type: {ex}", ex);
		}
	}

	private AssemblyTypeRegistry GetAssembly(string assemblyName)
	{
		if (_assemblyNameToIndex.TryGetValue(assemblyName, out var value))
		{
			return _allowedAssemblies[value];
		}
		if (_movedTypeAssemblies.TryGetValue(assemblyName, out AssemblyTypeRegistry value2))
		{
			return value2;
		}
		return null;
	}

	private Type GetDataModelType(string typename, string assembly)
	{
		AssemblyTypeRegistry assembly2 = GetAssembly(assembly);
		if (assembly2 == null)
		{
			UniLog.Warning("Assembly " + assembly + " is not a data model assembly for this world, cannot decode type: " + typename);
			return null;
		}
		Type type = assembly2.TryGetType(typename);
		if (type == null)
		{
			UniLog.Log("Type not found: " + typename + " in assembly " + assembly);
		}
		return type;
	}

	public Type GetDataModelType(string typename, bool allowAmbigious)
	{
		if (string.IsNullOrEmpty(typename))
		{
			throw new ArgumentException("Typename cannot be null or empty!");
		}
		typename = typename.Trim();
		if (typename.StartsWith("["))
		{
			int num = typename.IndexOf("]");
			if (num < 0)
			{
				return null;
			}
			string assembly = typename.Substring(1, num - 1);
			typename = typename.Substring(num + 1);
			return GetDataModelType(typename, assembly);
		}
		Type type = GlobalTypeRegistry.TryGetSystemType(typename);
		if (type != null)
		{
			return type;
		}
		if (!allowAmbigious)
		{
			return null;
		}
		foreach (AssemblyTypeRegistry allowedAssembly in _allowedAssemblies)
		{
			Type type2 = allowedAssembly.TryGetType(typename);
			if (type2 != null)
			{
				return type2;
			}
		}
		foreach (AssemblyTypeRegistry allowedAssembly2 in _allowedAssemblies)
		{
			IReadOnlyList<Type> readOnlyList = allowedAssembly2.TryGetTypesByName(typename);
			if (readOnlyList != null)
			{
				return readOnlyList[0];
			}
		}
		return null;
	}

	public Type ParseNiceType(string typename, bool allowAmbigious)
	{
		try
		{
			return NiceTypeParser.TryParse(typename, (string s) => GetDataModelType(s, allowAmbigious));
		}
		catch (Exception)
		{
			UniLog.Warning("Failed to parse typename: " + typename);
			throw;
		}
	}

	public string EncodeType(Type type)
	{
		return type.FormatType(delegate(Type t)
		{
			if (t.IsGenericParameter)
			{
				return "";
			}
			string text = GlobalTypeRegistry.TryGetSystemTypeName(t);
			if (text != null)
			{
				return text;
			}
			if (!_assemblyToIndex.TryGetValue(t.Assembly, out var value))
			{
				throw new ArgumentException($"Type {t} is not a data model type!");
			}
			AssemblyTypeRegistry assemblyTypeRegistry = _allowedAssemblies[value];
			if (!assemblyTypeRegistry.HasType(t))
			{
				throw new ArgumentException($"Type {t} is not a data model type in {assemblyTypeRegistry.AssemblyName}!");
			}
			return (t.DeclaringType != null) ? t.Name : ("[" + assemblyTypeRegistry.AssemblyName + "]" + t.FullName);
		});
	}

	public Type DecodeType(string typename)
	{
		if (_typeCache.TryGetValue(typename, out Type value))
		{
			return value;
		}
		try
		{
			value = ParseNiceType(typename, allowAmbigious: false);
		}
		catch (Exception)
		{
			UniLog.Error("Failed to decode type: " + typename);
			throw;
		}
		_typeCache.Add(typename, value);
		return value;
	}

	public Type DecodeLegacyType(string str)
	{
		if (_legacyTypeCache.TryGetValue(str, out Type value))
		{
			return value;
		}
		value = LegacyTypeParser.TryParse(str, delegate(string typename, string assembly)
		{
			Type type = MapLegacyType(typename, assembly);
			if (type != null)
			{
				return type;
			}
			if (!string.IsNullOrEmpty(assembly))
			{
				Type dataModelType = GetDataModelType(typename, assembly);
				if (dataModelType != null)
				{
					return dataModelType;
				}
			}
			else
			{
				foreach (AssemblyTypeRegistry allowedAssembly in _allowedAssemblies)
				{
					Type type2 = allowedAssembly.TryGetType(typename);
					if (type2 != null)
					{
						return type2;
					}
				}
			}
			Type type3 = GlobalTypeRegistry.TryGetOldTypeByHash(typename);
			if (type3 != null)
			{
				return type3;
			}
			typename = typename.Replace(LegacyMatch, "Legacy");
			type3 = GetDataModelType(typename, assembly) ?? GetDataModelType(typename, allowAmbigious: true);
			if (type3 != null)
			{
				return type3;
			}
			UniLog.Log("Type not found: " + typename);
			return (Type)null;
		});
		_legacyTypeCache.Add(str, value);
		return value;
	}

	public Type MapLegacyType(string typename, string assembly)
	{
		if (typename.StartsWith("QuantityX") || typename.StartsWith("Elements.Quantity"))
		{
			switch (typename.Substring(typename.LastIndexOf(".") + 1))
			{
			case "Acceleration":
				return typeof(Acceleration);
			case "Angle":
				return typeof(Angle);
			case "Current":
				return typeof(Current);
			case "Distance":
				return typeof(Distance);
			case "Mass":
				return typeof(Mass);
			case "Ratio":
				return typeof(Ratio);
			case "Resistance":
				return typeof(Resistance);
			case "Temperature":
				return typeof(Temperature);
			case "Time":
				return typeof(Time);
			case "Velocity":
				return typeof(Velocity);
			case "Voltage":
				return typeof(Voltage);
			}
		}
		if (typename.Contains(LegacyMatchCloud))
		{
			typename = typename.Replace(LegacyMatchCloud, "SkyFrost.Base");
			return GetDataModelType(typename, "SkyFrost.Base");
		}
		return null;
	}
}
