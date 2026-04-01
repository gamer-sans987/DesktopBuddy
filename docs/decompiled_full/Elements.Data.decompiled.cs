using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: TargetFramework(".NETStandard,Version=v2.0", FrameworkDisplayName = ".NET Standard 2.0")]
[assembly: AssemblyCompany("Elements.Data")]
[assembly: AssemblyConfiguration("Release")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0+12b4cfcfc837805a57a4a75a11ff2017f926f49b")]
[assembly: AssemblyProduct("Elements.Data")]
[assembly: AssemblyTitle("Elements.Data")]
[assembly: AssemblyVersion("1.0.0.0")]
namespace Elements.Data;

public enum DataModelAssemblyType
{
	Core,
	UserspaceCore,
	Optional,
	Dependency
}
[AttributeUsage(AttributeTargets.Assembly)]
public class DataModelAssemblyAttribute : Attribute
{
	public DataModelAssemblyType AssemblyType { get; private set; }

	public DataModelAssemblyAttribute(DataModelAssemblyType type)
	{
		AssemblyType = type;
	}

	public override string ToString()
	{
		return $"DataModelAssembly ({AssemblyType})";
	}
}
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Interface | AttributeTargets.Delegate)]
public class DataModelTypeAttribute : Attribute
{
}
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public class ExternalDataModelTypeAttribute : Attribute
{
	public Type ExternalType { get; private set; }

	public ExternalDataModelTypeAttribute(Type externalType)
	{
		ExternalType = externalType;
	}
}
public static class FeatureUpgradeFlags
{
	public const string NET_CORE = "NetCore";

	public const int NET_CORE_VERSION = 0;
}
public class FeatureUpgradeReplacement : TypeReplacement
{
	public string FeatureFlag { get; private set; }

	public int Version { get; private set; }

	public FeatureUpgradeReplacement(string featureFlag, int version, string replaceSource, string replaceTarget)
	{
		FeatureFlag = featureFlag;
		Version = version;
		base.ReplaceSource = replaceSource;
		base.ReplaceTarget = replaceTarget;
	}

	public FeatureUpgradeReplacement(string featureFlag, int version, Type newType)
	{
		FeatureFlag = featureFlag;
		Version = version;
		base.NewType = newType;
	}

	public bool NeedsUpgrade(IReadOnlyDictionary<string, int> featureFlags)
	{
		if (featureFlags == null)
		{
			return true;
		}
		if (!featureFlags.TryGetValue(FeatureFlag, out var value))
		{
			return true;
		}
		return value < Version;
	}
}
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Interface, AllowMultiple = true)]
public class OldAssemblyAttribute : Attribute
{
	public readonly string OldAssembly;

	public OldAssemblyAttribute(string oldAssembly)
	{
		OldAssembly = oldAssembly;
	}
}
[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
public class OldName : Attribute
{
	public readonly string[] OldNames;

	public OldName(string name)
	{
		OldNames = new string[1];
		OldNames[0] = name;
	}

	public OldName(params string[] names)
	{
		OldNames = names;
	}
}
[AttributeUsage(AttributeTargets.Field)]
public class OldNameHash : Attribute
{
	public readonly long[] OldNameHashes;

	public OldNameHash(long hash)
	{
		OldNameHashes = new long[1];
		OldNameHashes[0] = hash;
	}

	public OldNameHash(params long[] hashes)
	{
		OldNameHashes = hashes;
	}
}
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
public class OldNamespaceAttribute : Attribute
{
	public readonly string Namespace;

	public OldNamespaceAttribute(string @namespace)
	{
		Namespace = @namespace;
	}
}
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false)]
public class OldNonGenericDefaultAttribute : Attribute
{
	public readonly Type GenericType;

	public OldNonGenericDefaultAttribute(Type genericType)
	{
		GenericType = genericType;
	}
}
[AttributeUsage(AttributeTargets.Class)]
public class OldTypeHashAttribute : Attribute
{
	public readonly long Hash;

	public OldTypeHashAttribute(long hash)
	{
		Hash = hash;
	}
}
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Interface, AllowMultiple = true)]
public class OldTypeNameAttribute : Attribute
{
	public readonly string OldTypename;

	public readonly string OldAssembly;

	public OldTypeNameAttribute(string name, string oldAssembly = null)
	{
		OldTypename = name;
		OldAssembly = oldAssembly;
	}
}
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class OldTypeSpecialization : Attribute
{
	public readonly string Name;

	public readonly Type[] GenericArguments;

	public OldTypeSpecialization(string name, params Type[] genericArguments)
	{
		Name = name;
		GenericArguments = genericArguments;
	}
}
[AttributeUsage(AttributeTargets.Class)]
public abstract class TypeReplacement : Attribute
{
	public Type NewType { get; protected set; }

	public string ReplaceSource { get; protected set; }

	public string ReplaceTarget { get; protected set; }
}
