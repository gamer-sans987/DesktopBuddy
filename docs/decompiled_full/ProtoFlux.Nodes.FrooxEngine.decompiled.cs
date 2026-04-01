using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Awwdio;
using Elements.Assets;
using Elements.Core;
using Elements.Data;
using FrooxEngine;
using FrooxEngine.CommonAvatar;
using FrooxEngine.ProtoFlux;
using FrooxEngine.Undo;
using ProtoFlux.Core;
using ProtoFlux.Runtimes.Execution.Nodes.Actions;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Assets;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Async;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Avatar.BodyNodes;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Cloud;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Input.Controllers;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Input.Mouse;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Interaction.Tools;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Nodes;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Operators;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Playback;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Rendering;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Slots;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Time;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Users;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Utility;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Variables;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Worlds;
using ProtoFlux.Runtimes.Execution.Nodes.Math;
using ProtoFlux.Runtimes.Execution.Nodes.Math.Random;
using ProtoFlux.Runtimes.Execution.Nodes.Operators;
using ProtoFlux.Runtimes.Execution.Nodes.Strings;
using ProtoFlux.Runtimes.Execution.Nodes.Strings.Characters;
using Renderite.Shared;
using SharpPipe;
using SkyFrost.Base;
using TwitchLib.Client.Enums;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.PubSub.Events;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: ExternalDataModelType(typeof(BadgeColor))]
[assembly: ExternalDataModelType(typeof(SubscriptionPlan))]
[assembly: AssemblyTitle("ProtoFlux.Nodes.FrooxEngine")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("ProtoFlux.Nodes.FrooxEngine")]
[assembly: AssemblyCopyright("Copyright ©  2023")]
[assembly: AssemblyTrademark("")]
[assembly: ComVisible(false)]
[assembly: Guid("f0e251b1-87f0-4d7f-8190-7594be097f39")]
[assembly: AssemblyFileVersion("2026.3.25.1356")]
[assembly: DataModelAssembly(DataModelAssemblyType.Core)]
[assembly: TargetFramework(".NETCoreApp,Version=v10.0", FrameworkDisplayName = ".NET 10.0")]
[assembly: AssemblyVersion("2026.3.25.1356")]
[module: RefSafetyRules(11)]
[module: Description("FROOXENGINE_WEAVED")]
namespace ProtoFlux.Nodes.FrooxEngine
{
	public static class ProtoFluxMapper
	{
		private static SortedDictionary<string, ProtoFluxTypeMapping> genericMappings = new SortedDictionary<string, ProtoFluxTypeMapping>(Comparer<string>.Create(ProtoFluxHelper.CompareNodeList))
		{
			{
				"Delta_",
				typeof(ValueDelta<>)
			},
			{
				"SmoothLerp_",
				typeof(ValueSmoothLerp<>)
			},
			{
				"ConstantLerp_",
				typeof(ValueConstantLerp<>)
			},
			{
				"MulDeltaTime_",
				typeof(MulDeltaTime<>)
			},
			{
				"DivDeltaTime_",
				typeof(DivDeltaTime<>)
			},
			{
				"SampleAnimationTrack",
				new ProtoFluxTypeMapping(typeof(SampleValueAnimationTrack<>), typeof(SampleObjectAnimationTrack<>))
			},
			{
				"ReadCloudVariable",
				new ProtoFluxTypeMapping(typeof(ReadValueCloudVariable<>), typeof(ReadObjectCloudVariable<>))
			},
			{
				"WriteCloudVariable",
				new ProtoFluxTypeMapping(typeof(WriteValueCloudVariable<>), typeof(WriteObjectCloudVariable<>))
			},
			{
				"ReadDynamicVariable",
				new ProtoFluxTypeMapping(typeof(ReadDynamicValueVariable<>), typeof(ReadDynamicObjectVariable<>))
			},
			{
				"WriteDynamicVariable",
				new ProtoFluxTypeMapping(typeof(WriteDynamicValueVariable<>), typeof(WriteDynamicObjectVariable<>))
			},
			{
				"CreateDynamicVariable",
				new ProtoFluxTypeMapping(typeof(CreateDynamicValueVariable<>), typeof(CreateDynamicObjectVariable<>))
			},
			{
				"WriteOrCreateDynamicVariable",
				new ProtoFluxTypeMapping(typeof(WriteOrCreateDynamicValueVariable<>), typeof(WriteOrCreateDynamicObjectVariable<>))
			},
			{
				"LocalFireOnChange",
				new ProtoFluxTypeMapping(typeof(FireOnLocalValueChange<>), typeof(FireOnLocalObjectChange<>))
			},
			{
				"DynamicVariableInputWithEvents",
				new ProtoFluxTypeMapping(typeof(DynamicVariableValueInputWithEvents<>), typeof(DynamicVariableObjectInputWithEvents<>))
			},
			{
				"DynamicVariableInput",
				new ProtoFluxTypeMapping(typeof(DynamicVariableValueInput<>), typeof(DynamicVariableObjectInput<>))
			},
			{
				"ValueRegister",
				new ProtoFluxTypeMapping(typeof(DataModelValueFieldStore<>), typeof(DataModelObjectFieldStore<>))
			},
			{
				"ReferenceRegister",
				typeof(DataModelObjectRefStore<>)
			},
			{
				"FireOnChange",
				new ProtoFluxTypeMapping(typeof(FireOnValueChange<>), typeof(FireOnObjectValueChange<>))
			},
			{
				"FireOnChangeRef",
				typeof(FireOnRefChange<>)
			},
			{
				"DelayValueNode",
				new ProtoFluxTypeMapping(typeof(DelayValue<>), typeof(DelayObject<>))
			},
			{
				"UpdatesDelayWithValueNode",
				new ProtoFluxTypeMapping(typeof(DelayUpdatesWithValue<>), typeof(DelayUpdatesWithObject<>))
			},
			{
				"DynamicImpulseReceiverWithValue",
				new ProtoFluxTypeMapping(typeof(DynamicImpulseReceiverWithValue<>), typeof(DynamicImpulseReceiverWithObject<>))
			},
			{
				"DynamicImpulseTriggerWithValue",
				new ProtoFluxTypeMapping(typeof(DynamicImpulseTriggerWithValue<>), typeof(DynamicImpulseTriggerWithObject<>))
			},
			{
				"DelayWithValueNode",
				new ProtoFluxTypeMapping(typeof(DelayUpdatesOrTimeWithValueSecondsFloat<>), typeof(DelayUpdatesOrTimeWithObjectSecondsFloat<>))
			}
		};

		public static void Initialize()
		{
			ProtoFluxHelper.RegisterDynamicImpulseHandler(DynamicImpulseHelper.Singleton);
			ProtoFluxHelper.RegisterStartAsyncTaskNode(typeof(StartAsyncTask));
		}

		public static ProtoFluxTypeMapping MapNode(string name, string @namespace)
		{
			switch (name)
			{
			case "NewLine":
				if (@namespace.Contains("Characters"))
				{
					return typeof(ProtoFlux.Runtimes.Execution.Nodes.Strings.Characters.NewLine);
				}
				return typeof(ProtoFlux.Runtimes.Execution.Nodes.Strings.NewLine);
			case "ToUpper":
				if (@namespace.Contains("Characters"))
				{
					return typeof(ProtoFlux.Runtimes.Execution.Nodes.Strings.Characters.ToUpper);
				}
				return typeof(ProtoFlux.Runtimes.Execution.Nodes.Strings.ToUpper);
			case "ToLower":
				if (@namespace.Contains("Characters"))
				{
					return typeof(ProtoFlux.Runtimes.Execution.Nodes.Strings.Characters.ToLower);
				}
				return typeof(ProtoFlux.Runtimes.Execution.Nodes.Strings.ToLower);
			default:
				switch (name.Length)
				{
				case 9:
					switch (name[0])
					{
					case 'T':
						if (!(name == "TimerNode"))
						{
							break;
						}
						return typeof(SecondsTimer);
					case 'D':
						if (!(name == "DelayNode"))
						{
							break;
						}
						return typeof(DelayUpdatesOrSecondsFloat);
					case 'R':
						if (!(name == "RandomRGB"))
						{
							if (!(name == "RandomHue"))
							{
								break;
							}
							return typeof(RandomHue_ColorX);
						}
						return typeof(RandomRGB_ColorX);
					}
					break;
				case 8:
					switch (name[0])
					{
					case 'T':
						if (!(name == "TimeNode"))
						{
							break;
						}
						return typeof(WorldTimeFloat);
					case 'W':
						if (!(name == "WorldURL"))
						{
							break;
						}
						return typeof(WorldWebURL);
					}
					break;
				case 10:
					switch (name[0])
					{
					case 'T':
						if (!(name == "Time10Node"))
						{
							break;
						}
						return typeof(WorldTime10Float);
					case 'R':
						if (!(name == "RandomRGBA"))
						{
							break;
						}
						return typeof(RandomRGBA_ColorX);
					}
					break;
				case 12:
					switch (name[0])
					{
					case 'T':
						if (!(name == "TimeHalfNode"))
						{
							break;
						}
						return typeof(WorldTimeHalfFloat);
					case 'S':
						if (!(name == "SlotRegister"))
						{
							break;
						}
						return typeof(DataModelObjectRefStore<Slot>);
					case 'U':
						if (!(name == "UserRegister"))
						{
							break;
						}
						return typeof(DataModelUserRefStore);
					}
					break;
				case 13:
					switch (name[0])
					{
					case 'T':
						if (!(name == "TimeTenthNode"))
						{
							break;
						}
						return typeof(WorldTimeTenthFloat);
					case 'I':
						if (!(name == "IsBodyNodeEye"))
						{
							break;
						}
						return typeof(IsEye);
					case 'B':
						if (!(name == "BooleanToggle"))
						{
							break;
						}
						return typeof(DataModelBooleanToggle);
					case 'S':
						if (!(name == "StopwatchNode"))
						{
							break;
						}
						return typeof(ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Time.Stopwatch);
					case 'O':
						if (!(name == "OpenWorldLink"))
						{
							break;
						}
						return typeof(OpenWorld);
					}
					break;
				case 21:
					switch (name[0])
					{
					case 'M':
						if (!(name == "MouseScrollWheelDelta"))
						{
							break;
						}
						return typeof(MouseScrollDelta);
					case 'T':
						if (!(name == "TransformMatrix_float"))
						{
							break;
						}
						return typeof(ComposeTRS_Float4x4);
					}
					break;
				case 20:
					switch (name[0])
					{
					case 'G':
						if (!(name == "GetBodyNodeOtherSide"))
						{
							break;
						}
						return typeof(OtherSide);
					case 'W':
						if (!(name == "WindowsMR_Controller"))
						{
							break;
						}
						return typeof(ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Input.Controllers.WindowsMRController);
					case 'T':
						if (!(name == "TooltipEquippingNode"))
						{
							break;
						}
						return typeof(ToolEquippingSide);
					}
					break;
				case 15:
					switch (name[0])
					{
					case 'G':
						if (!(name == "GetBodyNodeSide"))
						{
							break;
						}
						return typeof(GetSide);
					case 'L':
						if (!(name == "LocalFireOnTrue"))
						{
							break;
						}
						return typeof(FireOnLocalTrue);
					case 'E':
						if (!(name == "ElapsedTimeNode"))
						{
							break;
						}
						return typeof(ElapsedTimeFloat);
					case 'R':
						if (!(name == "RandomGrayscale"))
						{
							break;
						}
						return typeof(RandomGrayscale_ColorX);
					}
					break;
				case 19:
					switch (name[0])
					{
					case 'P':
						if (!(name == "PlaybackPauseResume"))
						{
							break;
						}
						return typeof(Toggle);
					case 'L':
						if (!(name == "LocalImpulseTimeout"))
						{
							break;
						}
						return typeof(LocalImpulseTimeoutSeconds);
					}
					break;
				case 16:
					switch (name[0])
					{
					case 'L':
						if (!(name == "LocalFireOnFalse"))
						{
							break;
						}
						return typeof(FireOnLocalFalse);
					case 'F':
						if (!(name == "FireOnChangeType"))
						{
							break;
						}
						return typeof(FireOnTypeChange);
					case 'U':
						if (!(name == "UpdatesDelayNode"))
						{
							break;
						}
						return typeof(DelayUpdates);
					}
					break;
				case 14:
					switch (name[0])
					{
					case 'I':
						if (!(name == "ImpulseTimeout"))
						{
							break;
						}
						return typeof(LocalImpulseTimeoutSeconds);
					case 'G':
						if (!(name == "GetChildByName"))
						{
							break;
						}
						return typeof(FindChildByName);
					}
					break;
				case 17:
					if (!(name == "PlaybackReadState"))
					{
						break;
					}
					return typeof(PlaybackState);
				case 18:
					if (!(name == "PlaybackClipLength"))
					{
						break;
					}
					return typeof(ClipLengthFloat);
				case 22:
					if (!(name == "TransformMatrix_double"))
					{
						break;
					}
					return typeof(ComposeTRS_Double4x4);
				case 11:
					if (!(name == "SampleColor"))
					{
						break;
					}
					return typeof(SampleColorX);
				}
				break;
			case null:
				break;
			}
			if (name.StartsWith("Is") && name.EndsWith("DashOpened"))
			{
				return typeof(IsAppDashOpened);
			}
			if (name.EndsWith("Register"))
			{
				switch (name.Replace("Register", "").ToLower())
				{
				case "dummy":
				case "bool":
				case "byte":
				case "ushort":
				case "uint":
				case "ulong":
				case "sbyte":
				case "short":
				case "int":
				case "long":
				case "float":
				case "double":
				case "decimal":
				case "char":
				case "bool2":
				case "uint2":
				case "ulong2":
				case "int2":
				case "long2":
				case "float2":
				case "double2":
				case "bool3":
				case "uint3":
				case "ulong3":
				case "int3":
				case "long3":
				case "float3":
				case "double3":
				case "bool4":
				case "uint4":
				case "ulong4":
				case "int4":
				case "long4":
				case "float4":
				case "double4":
				case "floatq":
				case "doubleq":
				case "datetime":
				case "timespan":
				case "color":
				case "colorx":
					return typeof(DataModelValueFieldStore<>);
				case "object":
				case "uri":
				case "string":
					return typeof(DataModelObjectFieldStore<>);
				}
			}
			foreach (KeyValuePair<string, ProtoFluxTypeMapping> genericMapping in genericMappings)
			{
				if (name.StartsWith(genericMapping.Key))
				{
					return genericMapping.Value;
				}
			}
			if (name.Contains("Visuals"))
			{
				if (name.Contains("RevealAll"))
				{
					return typeof(UnpackProtoFlux);
				}
				if (name.Contains("RemoveAll"))
				{
					return typeof(PackProtoFluxNodes);
				}
			}
			return null;
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes
{
	[NodeOverload("Engine.InputDisplay")]
	public class ValueDisplay<T> : ExternalValueDisplay<FrooxEngineContext, T> where T : unmanaged
	{
		public static bool IsValidGenericType => Coder<T>.IsSupported;
	}
	[NodeOverload("Engine.InputDisplay")]
	public class GenericValueDisplay<T> : ExternalValueDisplay<FrooxEngineContext, T> where T : unmanaged
	{
	}
	[NodeOverload("Engine.InputDisplay")]
	public class ObjectDisplay<T> : ExternalObjectDisplay<FrooxEngineContext, T>
	{
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.Math
{
	[NodeCategory("Math/Interpolation")]
	[ContinuouslyChanging]
	public abstract class SmoothLerpBase<T> : ValueFunctionUpdateBase<T> where T : unmanaged
	{
		public ValueInput<T> Input;

		public ValueInput<float> Speed;

		private ValueStore<bool> _initialized;

		private ValueStore<T> _current;

		private ValueStore<T> _intermediate;

		protected override T Compute(FrooxEngineContext context)
		{
			return _current.Read(context);
		}

		protected override void RunUpdate(FrooxEngineContext context)
		{
			T target = Input.Evaluate(context);
			if (_initialized.Read(context))
			{
				float num = Speed.Evaluate(context, 0f);
				ref T reference = ref _current.Access(context);
				ref T reference2 = ref _intermediate.Access(context);
				if (float.IsNaN(num))
				{
					num = 0f;
				}
				reference = Coder<T>.FilterInvalid(reference, Coder<T>.Default);
				reference2 = Coder<T>.FilterInvalid(reference2, reference);
				reference = Lerp(ref reference, ref target, ref reference2, context.World.Time.Delta * num);
			}
			else
			{
				_initialized.Write(value: true, context);
				_current.Write(target, context);
				_intermediate.Write(target, context);
			}
		}

		protected override void OnAddedToScope(FrooxEngineContext context, NodeContextPath path)
		{
			if (!_initialized.Read(context))
			{
				context.GetEventDispatcher(out ExecutionEventDispatcher<FrooxEngineContext> eventDispatcher);
				eventDispatcher.ScheduleEvent(path, RunUpdate);
			}
		}

		protected abstract T Lerp(ref T current, ref T target, ref T intermediate, float delta);
	}
	[NodeName("Smooth Lerp", false)]
	[NodeOverload("Engine.Math.SmoothLerp")]
	public class ValueSmoothLerp<T> : SmoothLerpBase<T> where T : unmanaged
	{
		public static bool IsValidGenericType => Coder<T>.SupportsSmoothLerp;

		protected override T Lerp(ref T current, ref T target, ref T intermediate, float delta)
		{
			return Coder<T>.SmoothLerp(current, target, ref intermediate, delta);
		}
	}
	[NodeName("Smooth Slerp", false)]
	[NodeOverload("Engine.Math.SmoothSlerp")]
	public class SmoothSlerp_floatQ : SmoothLerpBase<floatQ>
	{
		protected override floatQ Lerp(ref floatQ current, ref floatQ target, ref floatQ intermediate, float delta)
		{
			return MathX.SmoothSlerp(in current, in target, ref intermediate, delta);
		}
	}
	[NodeName("Smooth Slerp", false)]
	[NodeOverload("Engine.Math.SmoothSlerp")]
	public class SmoothSlerp_doubleQ : SmoothLerpBase<doubleQ>
	{
		protected override doubleQ Lerp(ref doubleQ current, ref doubleQ target, ref doubleQ intermediate, float delta)
		{
			return MathX.SmoothSlerp(in current, in target, ref intermediate, delta);
		}
	}
	[ContinuouslyChanging]
	public abstract class ConstantLerpBase<T> : ValueFunctionUpdateBase<T> where T : unmanaged
	{
		public ValueInput<T> Input;

		public ValueInput<float> Speed;

		private ValueStore<bool> _initialized;

		private ValueStore<T> _current;

		protected override T Compute(FrooxEngineContext context)
		{
			return _current.Read(context);
		}

		protected override void RunUpdate(FrooxEngineContext context)
		{
			T target = Input.Evaluate(context);
			if (_initialized.Read(context))
			{
				float num = Speed.Evaluate(context, 0f);
				ref T reference = ref _current.Access(context);
				if (float.IsNaN(num))
				{
					num = 0f;
				}
				reference = Coder<T>.FilterInvalid(reference, Coder<T>.Default);
				reference = Lerp(ref reference, ref target, context.World.Time.Delta * num);
			}
			else
			{
				_initialized.Write(value: true, context);
				_current.Write(target, context);
			}
		}

		protected abstract T Lerp(ref T current, ref T target, float delta);

		protected override void OnAddedToScope(FrooxEngineContext context, NodeContextPath path)
		{
			if (!_initialized.Read(context))
			{
				context.GetEventDispatcher(out ExecutionEventDispatcher<FrooxEngineContext> eventDispatcher);
				eventDispatcher.ScheduleEvent(path, RunUpdate);
			}
		}
	}
	[NodeName("Constant Lerp", false)]
	[NodeCategory("Math/Interpolation")]
	[NodeOverload("Engine.Math.ConstantLerp")]
	public class ValueConstantLerp<T> : ConstantLerpBase<T> where T : unmanaged
	{
		public static bool IsValidGenericType => Coder<T>.SupportsConstantLerp;

		protected override T Lerp(ref T current, ref T target, float delta)
		{
			return Coder<T>.ConstantLerp(current, target, delta);
		}
	}
	[NodeName("Constant Slerp", false)]
	[NodeCategory("Math/Interpolation")]
	[NodeOverload("Engine.Math.ConstantSlerp")]
	public class ConstantSlerp_floatQ : ConstantLerpBase<floatQ>
	{
		protected override floatQ Lerp(ref floatQ current, ref floatQ target, float delta)
		{
			return MathX.ConstantSlerp(in current, in target, delta);
		}
	}
	[NodeName("Constant Slerp", false)]
	[NodeCategory("Math/Interpolation")]
	[NodeOverload("Engine.Math.ConstantSlerp")]
	public class ConstantSlerp_doubleQ : ConstantLerpBase<doubleQ>
	{
		protected override doubleQ Lerp(ref doubleQ current, ref doubleQ target, float delta)
		{
			return MathX.ConstantSlerp(in current, in target, delta);
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.Math.Bounds
{
	[NodeCategory("Transform/Bounds")]
	[ContinuouslyChanging]
	public class TransformBounds : ValueFunctionNode<FrooxEngineContext, BoundingBox>
	{
		public ValueArgument<BoundingBox> Bounds;

		public ObjectArgument<Slot> SourceSpace;

		public ObjectArgument<Slot> TargetSpace;

		protected override BoundingBox Compute(FrooxEngineContext context)
		{
			BoundingBox result = 0.ReadValue<BoundingBox>(context);
			if (!result.IsValid)
			{
				return result;
			}
			Slot obj = 1.ReadObject<Slot>(context) ?? context.World.RootSlot;
			Slot space = 2.ReadObject<Slot>(context) ?? context.World.RootSlot;
			return result.Transform(obj.GetLocalToSpaceMatrix(space));
		}
	}
	[NodeCategory("Transform/Bounds")]
	[ContinuouslyChanging]
	public class ComputeBoundingBox : ValueFunctionNode<FrooxEngineContext, BoundingBox>
	{
		public ObjectArgument<Slot> Instance;

		public ValueArgument<bool> IncludeInactive;

		public ObjectArgument<Slot> CoordinateSpace;

		public ObjectArgument<string> OnlyWithTag;

		protected override BoundingBox Compute(FrooxEngineContext context)
		{
			Slot slot = 0.ReadObject<Slot>(context);
			if (slot == null)
			{
				return BoundingBox.Empty();
			}
			string tag = 3.ReadObject<string>(context);
			Slot space = 2.ReadObject<Slot>(context);
			bool includeInactive = 1.ReadValue<bool>(context);
			Predicate<IBounded> filter = null;
			if (!string.IsNullOrWhiteSpace(tag))
			{
				filter = (IBounded b) => b.Slot.Tag == tag;
			}
			return slot.ComputeBoundingBox(includeInactive, space, filter);
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Worlds
{
	[NodeCategory("World")]
	public abstract class WorldURLActionNode : AsyncActionNode<FrooxEngineContext>
	{
		public ObjectInput<Uri> URL;

		public ObjectInput<IWorldLink> WorldLink;

		private ObjectStore<Uri> previousURL;

		private ObjectStore<IWorldLink> previousWorldLink;

		protected abstract IOperation OnOperationFail { get; }

		protected override async Task<IOperation> RunAsync(FrooxEngineContext context)
		{
			Uri uri = URL.Evaluate(context);
			IWorldLink worldLink = WorldLink.Evaluate(context);
			if (uri == null && worldLink == null)
			{
				return OnOperationFail;
			}
			uri = uri.MigrateLegacyURL(context.Cloud.Platform);
			if (previousURL.Read(context) == uri && previousWorldLink.Read(context) == worldLink)
			{
				return OnOperationFail;
			}
			previousURL.Write(uri, context);
			previousWorldLink.Write(worldLink, context);
			IOperation result = await RunWorldAction(context, uri, worldLink);
			previousURL.Write(null, context);
			previousWorldLink.Write(null, context);
			return result;
		}

		protected abstract Task<IOperation> RunWorldAction(FrooxEngineContext context, Uri url, IWorldLink worldLink);
	}
	public class OpenWorld : WorldURLActionNode
	{
		public ValueInput<Userspace.WorldRelation> Relation;

		[ProtoFlux.Core.DefaultValue(true)]
		public ValueInput<bool> GetExisting;

		[ProtoFlux.Core.DefaultValue(true)]
		public ValueInput<bool> LoadingIndicator;

		[ProtoFlux.Core.DefaultValue(true)]
		public ValueInput<bool> AutoFocus;

		public ValueInput<bool> MakePrivate;

		public readonly ObjectOutput<string> SessionID;

		public readonly ObjectOutput<Uri> SessionURL;

		public AsyncCall OnOpenStart;

		public AsyncCall OnOpenDone;

		public Continuation OnWorldReady;

		public Continuation OnOpenFail;

		public static Userspace.WorldRelation RelationDefault => Userspace.WorldRelation.Nest;

		protected override IOperation OnOperationFail => OnOpenFail.Target;

		protected override async Task<IOperation> RunWorldAction(FrooxEngineContext context, Uri url, IWorldLink worldLink)
		{
			await OnOpenStart.ExecuteAsync(context);
			WorldStartSettings startInfo = ((!(url != null)) ? new WorldStartSettings() : new WorldStartSettings(url));
			startInfo.Link = worldLink;
			startInfo.Relation = Relation.Evaluate(context, RelationDefault);
			startInfo.GetExisting = GetExisting.Evaluate(context, defaultValue: true);
			startInfo.CreateLoadIndicator = LoadingIndicator.Evaluate(context, defaultValue: true);
			startInfo.AutoFocus = AutoFocus.Evaluate(context, defaultValue: true);
			if (MakePrivate.Evaluate(context, defaultValue: false))
			{
				startInfo.DefaultAccessLevel = SessionAccessLevel.Private;
			}
			if (startInfo.Relation == Userspace.WorldRelation.Replace && context.World.IsAuthority && context.World.IsAllowedToSaveWorld() && context.World.HasUnsavedChanges())
			{
				startInfo.Relation = Userspace.WorldRelation.Nest;
			}
			if (!startInfo.CanStart(context.Engine.PlatformProfile, context.Engine.NetworkManager))
			{
				return OnOpenFail.Target;
			}
			try
			{
				World world = await Userspace.OpenWorld(startInfo);
				if (world == null)
				{
					return OnOpenFail.Target;
				}
				await OnOpenDone.ExecuteAsync(context);
				await Userspace.WaitForReady(world);
				if (world.State == World.WorldState.Failed)
				{
					return OnOpenFail.Target;
				}
				while (!world.SessionURLs.Any((Uri u) => u != null))
				{
					await default(NextUpdate);
				}
				SessionID.Write(world.SessionId, context);
				SessionURL.Write(world.SessionURLs.FirstOrDefault(), context);
				return OnWorldReady.Target;
			}
			catch (Exception ex)
			{
				UniLog.Warning("Exception when opening world: " + startInfo?.ToString() + "\n" + ex);
				return OnOpenFail.Target;
			}
		}

		public OpenWorld()
		{
			SessionID = new ObjectOutput<string>(this);
			SessionURL = new ObjectOutput<Uri>(this);
		}
	}
	public class FocusWorld : WorldURLActionNode
	{
		public ValueInput<bool> CloseCurrent;

		public Continuation OnNotFound;

		public AsyncCall OnFocused;

		public Continuation OnFail;

		protected override IOperation OnOperationFail => OnFail.Target;

		protected override async Task<IOperation> RunWorldAction(FrooxEngineContext context, Uri url, IWorldLink worldLink)
		{
			if (!IsAllowedToFocus(context))
			{
				return OnFail.Target;
			}
			bool closeCurrent = false;
			if (!context.World.IsAuthority || !context.World.IsAllowedToSaveWorld() || !context.World.HasUnsavedChanges())
			{
				closeCurrent = CloseCurrent.Evaluate(context, defaultValue: false);
			}
			WorldStartSettings startInfo = ((!(url != null)) ? new WorldStartSettings() : new WorldStartSettings(url));
			startInfo.Link = worldLink;
			await Userspace.ResolveUris(startInfo);
			World existingWorld = Userspace.GetExistingWorld(startInfo);
			if (existingWorld == null)
			{
				return OnNotFound.Target;
			}
			await Userspace.FocusWhenReady(existingWorld);
			if (closeCurrent)
			{
				await OnFocused.ExecuteAsync(context);
				await Userspace.ExitWorld(context.World);
				return null;
			}
			return OnFocused.Target;
		}

		private static bool IsAllowedToFocus(FrooxEngineContext context)
		{
			World world = context.World;
			if (world.Focus != World.WorldFocus.Focused)
			{
				return world.Focus == World.WorldFocus.PrivateOverlay;
			}
			return true;
		}
	}
	[NodeCategory("World")]
	public class WorldSaved : ProxyVoidNode<FrooxEngineContext, WorldSaved.Proxy>
	{
		public class Proxy : ProtoFluxEngineProxy
		{
			public Action OnSaved;

			public override void OnWorldSaved()
			{
				OnSaved?.Invoke();
			}

			protected override void InitializeSyncMembers()
			{
				base.InitializeSyncMembers();
			}

			public override ISyncMember GetSyncMember(int index)
			{
				return index switch
				{
					0 => persistent, 
					1 => updateOrder, 
					2 => EnabledField, 
					3 => Node, 
					4 => Path, 
					_ => throw new ArgumentOutOfRangeException(), 
				};
			}

			public static Proxy __New()
			{
				return new Proxy();
			}
		}

		public Call OnSaved;

		protected override void ProxyAdded(Proxy proxy, FrooxEngineContext context)
		{
			NodeContextPath path = context.CaptureContextPath();
			context.GetEventDispatcher(out var dispatcher);
			Action onSaved = delegate
			{
				dispatcher.ScheduleEvent(path, HandleEvent, null);
			};
			proxy.OnSaved = onSaved;
		}

		protected override void ProxyRemoved(Proxy proxy, FrooxEngineContext context, bool inUseByAnotherInstance)
		{
			if (!inUseByAnotherInstance)
			{
				proxy.OnSaved = null;
			}
		}

		private void HandleEvent(FrooxEngineContext context, object eventData)
		{
			OnSaved.Execute(context);
		}
	}
	[NodeCategory("World")]
	public class UserJoined : ProxyVoidNode<FrooxEngineContext, UserJoined.Proxy>
	{
		public class Proxy : ProtoFluxEngineProxy
		{
			public Action<global::FrooxEngine.User> Joined;

			internal List<global::FrooxEngine.User> QueuedEvents;

			public override void OnUserJoined(global::FrooxEngine.User user)
			{
				if (Joined != null)
				{
					Joined(user);
					return;
				}
				if (QueuedEvents == null)
				{
					QueuedEvents = Pool.BorrowList<global::FrooxEngine.User>();
				}
				QueuedEvents.Add(user);
			}

			protected override void InitializeSyncMembers()
			{
				base.InitializeSyncMembers();
			}

			public override ISyncMember GetSyncMember(int index)
			{
				return index switch
				{
					0 => persistent, 
					1 => updateOrder, 
					2 => EnabledField, 
					3 => Node, 
					4 => Path, 
					_ => throw new ArgumentOutOfRangeException(), 
				};
			}

			public static Proxy __New()
			{
				return new Proxy();
			}
		}

		[ProtoFlux.Core.DefaultValue(true)]
		public ValueInput<bool> OnlyHost;

		public Call OnJoined;

		public readonly ObjectOutput<global::FrooxEngine.User> JoinedUser;

		public override bool CanBeEvaluated => false;

		protected override void ProxyAdded(Proxy proxy, FrooxEngineContext context)
		{
			NodeContextPath path = context.CaptureContextPath();
			context.GetEventDispatcher(out var dispatcher);
			Action<global::FrooxEngine.User> joined = delegate(global::FrooxEngine.User u)
			{
				dispatcher.ScheduleEvent(path, HandleEvent, u);
			};
			proxy.Joined = joined;
			if (proxy.QueuedEvents == null)
			{
				return;
			}
			foreach (global::FrooxEngine.User queuedEvent in proxy.QueuedEvents)
			{
				dispatcher.ScheduleEvent(path, HandleEvent, queuedEvent);
			}
			Pool.Return(ref proxy.QueuedEvents);
		}

		protected override void ProxyRemoved(Proxy proxy, FrooxEngineContext context, bool inUseByAnotherInstance)
		{
			if (!inUseByAnotherInstance)
			{
				proxy.Joined = null;
			}
		}

		private void HandleEvent(FrooxEngineContext context, object user)
		{
			if (context.World.IsAuthority || !OnlyHost.Evaluate(context, defaultValue: true))
			{
				JoinedUser.Write(user as global::FrooxEngine.User, context);
				OnJoined.Execute(context);
			}
		}

		public UserJoined()
		{
			JoinedUser = new ObjectOutput<global::FrooxEngine.User>(this);
		}
	}
	[NodeCategory("World")]
	public class UserLeft : ProxyVoidNode<FrooxEngineContext, UserLeft.Proxy>
	{
		public class Proxy : ProtoFluxEngineProxy
		{
			public Action<global::FrooxEngine.User> Left;

			public override void OnUserLeft(global::FrooxEngine.User user)
			{
				Left?.Invoke(user);
			}

			protected override void InitializeSyncMembers()
			{
				base.InitializeSyncMembers();
			}

			public override ISyncMember GetSyncMember(int index)
			{
				return index switch
				{
					0 => persistent, 
					1 => updateOrder, 
					2 => EnabledField, 
					3 => Node, 
					4 => Path, 
					_ => throw new ArgumentOutOfRangeException(), 
				};
			}

			public static Proxy __New()
			{
				return new Proxy();
			}
		}

		[ProtoFlux.Core.DefaultValue(true)]
		public ValueInput<bool> OnlyHost;

		public Call OnLeft;

		public readonly ObjectOutput<global::FrooxEngine.User> LeftUser;

		public override bool CanBeEvaluated => false;

		protected override void ProxyAdded(Proxy proxy, FrooxEngineContext context)
		{
			NodeContextPath path = context.CaptureContextPath();
			context.GetEventDispatcher(out var dispatcher);
			Action<global::FrooxEngine.User> left = delegate(global::FrooxEngine.User u)
			{
				dispatcher.ScheduleEvent(path, HandleEvent, u);
			};
			proxy.Left = left;
		}

		protected override void ProxyRemoved(Proxy proxy, FrooxEngineContext context, bool inUseByAnotherInstance)
		{
			if (!inUseByAnotherInstance)
			{
				proxy.Left = null;
			}
		}

		private void HandleEvent(FrooxEngineContext context, object user)
		{
			if (context.World.IsAuthority || !OnlyHost.Evaluate(context, defaultValue: true))
			{
				LeftUser.Write(user as global::FrooxEngine.User, context);
				OnLeft.Execute(context);
			}
		}

		public UserLeft()
		{
			LeftUser = new ObjectOutput<global::FrooxEngine.User>(this);
		}
	}
	[NodeCategory("World")]
	public class UserSpawn : ProxyVoidNode<FrooxEngineContext, UserSpawn.Proxy>
	{
		public class Proxy : ProtoFluxEngineProxy
		{
			internal Action<global::FrooxEngine.User> Spawn;

			internal List<global::FrooxEngine.User> QueuedEvents;

			public override void OnUserSpawn(global::FrooxEngine.User user)
			{
				if (Spawn != null)
				{
					Spawn(user);
					return;
				}
				if (QueuedEvents == null)
				{
					QueuedEvents = Pool.BorrowList<global::FrooxEngine.User>();
				}
				QueuedEvents.Add(user);
			}

			protected override void InitializeSyncMembers()
			{
				base.InitializeSyncMembers();
			}

			public override ISyncMember GetSyncMember(int index)
			{
				return index switch
				{
					0 => persistent, 
					1 => updateOrder, 
					2 => EnabledField, 
					3 => Node, 
					4 => Path, 
					_ => throw new ArgumentOutOfRangeException(), 
				};
			}

			public static Proxy __New()
			{
				return new Proxy();
			}
		}

		[ProtoFlux.Core.DefaultValue(true)]
		public ValueInput<bool> OnlyHost;

		public Call OnSpawn;

		public readonly ObjectOutput<global::FrooxEngine.User> SpawnedUser;

		public override bool CanBeEvaluated => false;

		protected override void ProxyAdded(Proxy proxy, FrooxEngineContext context)
		{
			NodeContextPath path = context.CaptureContextPath();
			context.GetEventDispatcher(out var dispatcher);
			Action<global::FrooxEngine.User> spawn = delegate(global::FrooxEngine.User u)
			{
				dispatcher.ScheduleEvent(path, HandleEvent, u);
			};
			proxy.Spawn = spawn;
			if (proxy.QueuedEvents == null)
			{
				return;
			}
			foreach (global::FrooxEngine.User queuedEvent in proxy.QueuedEvents)
			{
				dispatcher.ScheduleEvent(path, HandleEvent, queuedEvent);
			}
			Pool.Return(ref proxy.QueuedEvents);
		}

		protected override void ProxyRemoved(Proxy proxy, FrooxEngineContext context, bool inUseByAnotherInstance)
		{
			if (!inUseByAnotherInstance)
			{
				proxy.Spawn = null;
			}
		}

		private void HandleEvent(FrooxEngineContext context, object user)
		{
			if (context.World.IsAuthority || !OnlyHost.Evaluate(context, defaultValue: true))
			{
				SpawnedUser.Write(user as global::FrooxEngine.User, context);
				OnSpawn.Execute(context);
			}
		}

		public UserSpawn()
		{
			SpawnedUser = new ObjectOutput<global::FrooxEngine.User>(this);
		}
	}
	[NodeCategory("World/Info")]
	[ContinuouslyChanging]
	public class WorldName : ObjectFunctionNode<FrooxEngineContext, string>
	{
		protected override string Compute(FrooxEngineContext context)
		{
			return context.World.Name;
		}
	}
	[NodeCategory("World/Info")]
	[ContinuouslyChanging]
	public class WorldDescription : ObjectFunctionNode<FrooxEngineContext, string>
	{
		protected override string Compute(FrooxEngineContext context)
		{
			return context.World.Description;
		}
	}
	[NodeCategory("World/Info")]
	public class WorldSessionID : ObjectFunctionNode<FrooxEngineContext, string>
	{
		protected override string Compute(FrooxEngineContext context)
		{
			return context.World.SessionId;
		}
	}
	[NodeCategory("World/Info")]
	public class WorldSessionURL : ObjectFunctionNode<FrooxEngineContext, string>
	{
		protected override string Compute(FrooxEngineContext context)
		{
			return context.Cloud.Platform.GetSessionUri(context.World.SessionId).OriginalString;
		}
	}
	[NodeCategory("World/Info")]
	public class WorldSessionWebURL : ObjectFunctionNode<FrooxEngineContext, string>
	{
		protected override string Compute(FrooxEngineContext context)
		{
			return context.Cloud.Platform.GetSessionWebUri(context.World.SessionId).OriginalString;
		}
	}
	[NodeCategory("World/Info")]
	[ContinuouslyChanging]
	public class WorldMobileFriendly : ValueFunctionNode<FrooxEngineContext, bool>
	{
		protected override bool Compute(FrooxEngineContext context)
		{
			return context.World.MobileFriendly;
		}
	}
	[NodeCategory("World/Info")]
	[ContinuouslyChanging]
	public class WorldMaxUsers : ValueFunctionNode<FrooxEngineContext, int>
	{
		protected override int Compute(FrooxEngineContext context)
		{
			return context.World.MaxUsers;
		}
	}
	[NodeCategory("World/Info")]
	[ContinuouslyChanging]
	public class WorldUserCount : ValueFunctionNode<FrooxEngineContext, int>
	{
		protected override int Compute(FrooxEngineContext context)
		{
			return context.World.UserCount;
		}
	}
	[NodeCategory("World/Info")]
	[ContinuouslyChanging]
	public class WorldActiveUserCount : ValueFunctionNode<FrooxEngineContext, int>
	{
		protected override int Compute(FrooxEngineContext context)
		{
			return context.World.ActiveUserCount;
		}
	}
	[NodeCategory("World/Info")]
	[ContinuouslyChanging]
	public class WorldAccessLevel : ValueFunctionNode<FrooxEngineContext, SessionAccessLevel>
	{
		protected override SessionAccessLevel Compute(FrooxEngineContext context)
		{
			return context.World.AccessLevel;
		}
	}
	[NodeCategory("World/Info")]
	[ContinuouslyChanging]
	public class WorldHideFromListing : ValueFunctionNode<FrooxEngineContext, bool>
	{
		protected override bool Compute(FrooxEngineContext context)
		{
			return context.World.HideFromListing;
		}
	}
	[NodeCategory("World/Info")]
	[ContinuouslyChanging]
	public class WorldAwayKickEnabled : ValueFunctionNode<FrooxEngineContext, bool>
	{
		protected override bool Compute(FrooxEngineContext context)
		{
			return context.World.AwayKickEnabled;
		}
	}
	[NodeCategory("World/Info")]
	[ContinuouslyChanging]
	public class WorldAwayKickMinutes : ValueFunctionNode<FrooxEngineContext, float>
	{
		protected override float Compute(FrooxEngineContext context)
		{
			return context.World.AwayKickMinutes;
		}
	}
	[NodeCategory("World/Info")]
	[ContinuouslyChanging]
	public class WorldAwayKickInterval : ValueFunctionNode<FrooxEngineContext, TimeSpan>
	{
		protected override TimeSpan Compute(FrooxEngineContext context)
		{
			return context.World.AwayKickInterval;
		}
	}
	[NodeCategory("World/Info")]
	public class WorldWebURL : ObjectFunctionNode<FrooxEngineContext, string>
	{
		protected override string Compute(FrooxEngineContext context)
		{
			return context.World.RecordWebURL?.OriginalString;
		}
	}
	[NodeCategory("World/Info")]
	public class WorldRecordURL : ObjectFunctionNode<FrooxEngineContext, string>
	{
		protected override string Compute(FrooxEngineContext context)
		{
			return context.World.RecordURL?.OriginalString;
		}
	}
	[NodeCategory("World/Info")]
	public class WorldPath : ObjectFunctionNode<FrooxEngineContext, string>
	{
		protected override string Compute(FrooxEngineContext context)
		{
			return context.World?.CorrespondingRecord?.Path;
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Transform
{
	[NodeCategory("Transform")]
	public class GlobalTransform : VoidNode<FrooxEngineContext>
	{
		public ObjectArgument<Slot> Instance;

		[ContinuouslyChanging]
		public readonly ValueOutput<float3> GlobalPosition;

		[ContinuouslyChanging]
		public readonly ValueOutput<floatQ> GlobalRotation;

		[ContinuouslyChanging]
		public readonly ValueOutput<float3> GlobalScale;

		protected override void ComputeOutputs(FrooxEngineContext context)
		{
			Slot slot = 0.ReadObject<Slot>(context);
			if (slot != null)
			{
				GlobalScale.Write(slot.GlobalScale, context);
				GlobalRotation.Write(slot.GlobalRotation, context);
				GlobalPosition.Write(slot.GlobalPosition, context);
			}
			else
			{
				GlobalPosition.Write(default(float3), context);
				GlobalRotation.Write(default(floatQ), context);
				GlobalScale.Write(default(float3), context);
			}
		}

		public GlobalTransform()
		{
			GlobalPosition = new ValueOutput<float3>(this);
			GlobalRotation = new ValueOutput<floatQ>(this);
			GlobalScale = new ValueOutput<float3>(this);
		}
	}
	[NodeCategory("Transform")]
	public class LocalTransform : VoidNode<FrooxEngineContext>
	{
		public ObjectArgument<Slot> Instance;

		[ContinuouslyChanging]
		public readonly ValueOutput<float3> LocalPosition;

		[ContinuouslyChanging]
		public readonly ValueOutput<floatQ> LocalRotation;

		[ContinuouslyChanging]
		public readonly ValueOutput<float3> LocalScale;

		protected override void ComputeOutputs(FrooxEngineContext context)
		{
			Slot slot = 0.ReadObject<Slot>(context);
			if (slot != null)
			{
				LocalPosition.Write(slot.LocalPosition, context);
				LocalRotation.Write(slot.LocalRotation, context);
				LocalScale.Write(slot.LocalScale, context);
			}
			else
			{
				LocalPosition.Write(default(float3), context);
				LocalRotation.Write(default(floatQ), context);
				LocalScale.Write(default(float3), context);
			}
		}

		public LocalTransform()
		{
			LocalPosition = new ValueOutput<float3>(this);
			LocalRotation = new ValueOutput<floatQ>(this);
			LocalScale = new ValueOutput<float3>(this);
		}
	}
	[NodeCategory("Transform/Direction")]
	[ContinuouslyChanging]
	public class GetForward : ValueFunctionNode<FrooxEngineContext, float3>
	{
		public ObjectArgument<Slot> Instance;

		protected override float3 Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<Slot>(context)?.Forward ?? float3.Forward;
		}
	}
	[NodeCategory("Transform/Direction")]
	[ContinuouslyChanging]
	public class GetUp : ValueFunctionNode<FrooxEngineContext, float3>
	{
		public ObjectArgument<Slot> Instance;

		protected override float3 Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<Slot>(context)?.Up ?? float3.Up;
		}
	}
	[NodeCategory("Transform/Direction")]
	[ContinuouslyChanging]
	public class GetRight : ValueFunctionNode<FrooxEngineContext, float3>
	{
		public ObjectArgument<Slot> Instance;

		protected override float3 Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<Slot>(context)?.Right ?? float3.Right;
		}
	}
	[NodeCategory("Transform/Direction")]
	[ContinuouslyChanging]
	public class GetBackward : ValueFunctionNode<FrooxEngineContext, float3>
	{
		public ObjectArgument<Slot> Instance;

		protected override float3 Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<Slot>(context)?.Backward ?? float3.Backward;
		}
	}
	[NodeCategory("Transform/Direction")]
	[ContinuouslyChanging]
	public class GetDown : ValueFunctionNode<FrooxEngineContext, float3>
	{
		public ObjectArgument<Slot> Instance;

		protected override float3 Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<Slot>(context)?.Down ?? float3.Down;
		}
	}
	[NodeCategory("Transform/Direction")]
	[ContinuouslyChanging]
	public class GetLeft : ValueFunctionNode<FrooxEngineContext, float3>
	{
		public ObjectArgument<Slot> Instance;

		protected override float3 Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<Slot>(context)?.Left ?? float3.Left;
		}
	}
	public abstract class TransformSetter : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<Slot> Instance;

		public static floatQ RotationDefault => floatQ.Identity;

		public static float3 ScaleDefault => float3.One;

		protected override bool Do(FrooxEngineContext context)
		{
			Slot slot = Instance.Evaluate(context);
			if (slot == null || slot.IsRemoved)
			{
				return false;
			}
			Set(slot, context);
			return true;
		}

		protected abstract void Set(Slot instance, FrooxEngineContext context);
	}
	[NodeCategory("Transform")]
	public class SetGlobalPositionRotation : TransformSetter
	{
		public ValueInput<float3> Position;

		public ValueInput<floatQ> Rotation;

		protected override void Set(Slot instance, FrooxEngineContext context)
		{
			instance.GlobalPosition = Position.Evaluate(context);
			instance.GlobalRotation = Rotation.Evaluate(context, TransformSetter.RotationDefault);
		}
	}
	[NodeCategory("Transform")]
	public class SetGlobalTransform : TransformSetter
	{
		public ValueInput<float3> Position;

		public ValueInput<floatQ> Rotation;

		public ValueInput<float3> Scale;

		protected override void Set(Slot instance, FrooxEngineContext context)
		{
			instance.GlobalPosition = Position.Evaluate(context);
			instance.GlobalRotation = Rotation.Evaluate(context, TransformSetter.RotationDefault);
			instance.GlobalScale = Scale.Evaluate(context, TransformSetter.ScaleDefault);
		}
	}
	[NodeCategory("Transform")]
	public class SetGlobalTransformMatrix : TransformSetter
	{
		public ValueInput<float4x4> Matrix;

		public static float4x4 MatrixDefault => float4x4.Identity;

		protected override void Set(Slot instance, FrooxEngineContext context)
		{
			instance.LocalToGlobal = Matrix.Evaluate(context, MatrixDefault);
		}
	}
	[NodeCategory("Transform")]
	public class SetGlobalPosition : TransformSetter
	{
		public ValueInput<float3> Position;

		protected override void Set(Slot instance, FrooxEngineContext context)
		{
			instance.GlobalPosition = Position.Evaluate(context);
		}
	}
	[NodeCategory("Transform")]
	public class SetGlobalRotation : TransformSetter
	{
		public ValueInput<floatQ> Rotation;

		protected override void Set(Slot instance, FrooxEngineContext context)
		{
			instance.GlobalRotation = Rotation.Evaluate(context, TransformSetter.RotationDefault);
		}
	}
	[NodeCategory("Transform")]
	public class SetGlobalScale : TransformSetter
	{
		public ValueInput<float3> Scale;

		protected override void Set(Slot instance, FrooxEngineContext context)
		{
			instance.GlobalScale = Scale.Evaluate(context, TransformSetter.ScaleDefault);
		}
	}
	[NodeCategory("Transform")]
	public class SetLocalPositionRotation : TransformSetter
	{
		public ValueInput<float3> Position;

		public ValueInput<floatQ> Rotation;

		protected override void Set(Slot instance, FrooxEngineContext context)
		{
			instance.LocalPosition = Position.Evaluate(context);
			instance.LocalRotation = Rotation.Evaluate(context, TransformSetter.RotationDefault);
		}
	}
	[NodeCategory("Transform")]
	public class SetLocalTransform : TransformSetter
	{
		public ValueInput<float3> Position;

		public ValueInput<floatQ> Rotation;

		public ValueInput<float3> Scale;

		protected override void Set(Slot instance, FrooxEngineContext context)
		{
			instance.LocalScale = Scale.Evaluate(context, TransformSetter.ScaleDefault);
			instance.LocalRotation = Rotation.Evaluate(context, TransformSetter.RotationDefault);
			instance.LocalPosition = Position.Evaluate(context);
		}
	}
	[NodeCategory("Transform")]
	public class SetTRS : TransformSetter
	{
		public ValueInput<float4x4> TRS;

		public static float4x4 TRSDefault => float4x4.Identity;

		protected override void Set(Slot instance, FrooxEngineContext context)
		{
			instance.TRS = TRS.Evaluate(context, TRSDefault);
		}
	}
	[NodeCategory("Transform")]
	public class SetLocalPosition : TransformSetter
	{
		public ValueInput<float3> Position;

		protected override void Set(Slot instance, FrooxEngineContext context)
		{
			instance.LocalPosition = Position.Evaluate(context);
		}
	}
	[NodeCategory("Transform")]
	public class SetLocalRotation : TransformSetter
	{
		public ValueInput<floatQ> Rotation;

		protected override void Set(Slot instance, FrooxEngineContext context)
		{
			instance.LocalRotation = Rotation.Evaluate(context, TransformSetter.RotationDefault);
		}
	}
	[NodeCategory("Transform")]
	public class SetLocalScale : TransformSetter
	{
		public ValueInput<float3> Scale;

		protected override void Set(Slot instance, FrooxEngineContext context)
		{
			instance.LocalScale = Scale.Evaluate(context, TransformSetter.ScaleDefault);
		}
	}
	[NodeCategory("Transform/Direction")]
	public class SetForward : TransformSetter
	{
		public ValueInput<float3> Forward;

		protected override void Set(Slot instance, FrooxEngineContext context)
		{
			float3 a = Forward.Evaluate(context);
			if (!(a == float3.Zero))
			{
				instance.Forward = a;
			}
		}
	}
	[NodeCategory("Transform/Direction")]
	public class SetUp : TransformSetter
	{
		public ValueInput<float3> Up;

		protected override void Set(Slot instance, FrooxEngineContext context)
		{
			float3 a = Up.Evaluate(context);
			if (!(a == float3.Zero))
			{
				instance.Up = a;
			}
		}
	}
	[NodeCategory("Transform/Direction")]
	public class SetRight : TransformSetter
	{
		public ValueInput<float3> Right;

		protected override void Set(Slot instance, FrooxEngineContext context)
		{
			float3 a = Right.Evaluate(context);
			if (!(a == float3.Zero))
			{
				instance.Right = a;
			}
		}
	}
	[NodeCategory("Transform/Direction")]
	public class SetBackward : TransformSetter
	{
		public ValueInput<float3> Backward;

		protected override void Set(Slot instance, FrooxEngineContext context)
		{
			float3 a = Backward.Evaluate(context);
			if (!(a == float3.Zero))
			{
				instance.Backward = a;
			}
		}
	}
	[NodeCategory("Transform/Direction")]
	public class SetDown : TransformSetter
	{
		public ValueInput<float3> Down;

		protected override void Set(Slot instance, FrooxEngineContext context)
		{
			float3 a = Down.Evaluate(context);
			if (!(a == float3.Zero))
			{
				instance.Down = a;
			}
		}
	}
	[NodeCategory("Transform/Direction")]
	public class SetLeft : TransformSetter
	{
		public ValueInput<float3> Left;

		protected override void Set(Slot instance, FrooxEngineContext context)
		{
			float3 a = Left.Evaluate(context);
			if (!(a == float3.Zero))
			{
				instance.Left = a;
			}
		}
	}
	[NodeCategory("Transform/Conversion")]
	[ContinuouslyChanging]
	public class GlobalPointToLocal : ValueFunctionNode<FrooxEngineContext, float3>
	{
		public ObjectArgument<Slot> Instance;

		public ValueArgument<float3> GlobalPoint;

		protected override float3 Compute(FrooxEngineContext context)
		{
			Slot slot = 0.ReadObject<Slot>(context);
			float3 globalPoint = 1.ReadValue<float3>(context);
			return slot?.GlobalPointToLocal(in globalPoint) ?? globalPoint;
		}
	}
	[NodeCategory("Transform/Conversion")]
	[ContinuouslyChanging]
	public class LocalPointToGlobal : ValueFunctionNode<FrooxEngineContext, float3>
	{
		public ObjectArgument<Slot> Instance;

		public ValueArgument<float3> LocalPoint;

		protected override float3 Compute(FrooxEngineContext context)
		{
			Slot slot = 0.ReadObject<Slot>(context);
			float3 localPoint = 1.ReadValue<float3>(context);
			return slot?.LocalPointToGlobal(in localPoint) ?? localPoint;
		}
	}
	[NodeCategory("Transform/Conversion")]
	[ContinuouslyChanging]
	public class TransformPoint : ValueFunctionNode<FrooxEngineContext, float3>
	{
		public ObjectArgument<Slot> FromSpace;

		public ObjectArgument<Slot> ToSpace;

		public ValueArgument<float3> SourcePoint;

		protected override float3 Compute(FrooxEngineContext context)
		{
			return (0.ReadObject<Slot>(context) ?? context.World.RootSlot).LocalPointToSpace(space: 1.ReadObject<Slot>(context) ?? context.World.RootSlot, localPoint: 2.ReadValue<float3>(context));
		}
	}
	[NodeCategory("Transform/Conversion")]
	[ContinuouslyChanging]
	public class GlobalDirectionToLocal : ValueFunctionNode<FrooxEngineContext, float3>
	{
		public ObjectArgument<Slot> Instance;

		public ValueArgument<float3> GlobalDirection;

		protected override float3 Compute(FrooxEngineContext context)
		{
			Slot slot = 0.ReadObject<Slot>(context);
			float3 globalDirection = 1.ReadValue<float3>(context);
			return slot?.GlobalDirectionToLocal(in globalDirection) ?? globalDirection;
		}
	}
	[NodeCategory("Transform/Conversion")]
	[ContinuouslyChanging]
	public class LocalDirectionToGlobal : ValueFunctionNode<FrooxEngineContext, float3>
	{
		public ObjectArgument<Slot> Instance;

		public ValueArgument<float3> LocalDirection;

		protected override float3 Compute(FrooxEngineContext context)
		{
			Slot slot = 0.ReadObject<Slot>(context);
			float3 localDirection = 1.ReadValue<float3>(context);
			return slot?.LocalDirectionToGlobal(in localDirection) ?? localDirection;
		}
	}
	[NodeCategory("Transform/Conversion")]
	[ContinuouslyChanging]
	public class TransformDirection : ValueFunctionNode<FrooxEngineContext, float3>
	{
		public ObjectArgument<Slot> FromSpace;

		public ObjectArgument<Slot> ToSpace;

		public ValueArgument<float3> SourceDirection;

		protected override float3 Compute(FrooxEngineContext context)
		{
			return (0.ReadObject<Slot>(context) ?? context.World.RootSlot).LocalDirectionToSpace(space: 1.ReadObject<Slot>(context) ?? context.World.RootSlot, localDirection: 2.ReadValue<float3>(context));
		}
	}
	[NodeCategory("Transform/Conversion")]
	[ContinuouslyChanging]
	public class GlobalVectorToLocal : ValueFunctionNode<FrooxEngineContext, float3>
	{
		public ObjectArgument<Slot> Instance;

		public ValueArgument<float3> GlobalVector;

		protected override float3 Compute(FrooxEngineContext context)
		{
			Slot slot = 0.ReadObject<Slot>(context);
			float3 globalVector = 1.ReadValue<float3>(context);
			return slot?.GlobalVectorToLocal(in globalVector) ?? globalVector;
		}
	}
	[NodeCategory("Transform/Conversion")]
	[ContinuouslyChanging]
	public class LocalVectorToGlobal : ValueFunctionNode<FrooxEngineContext, float3>
	{
		public ObjectArgument<Slot> Instance;

		public ValueArgument<float3> LocalVector;

		protected override float3 Compute(FrooxEngineContext context)
		{
			Slot slot = 0.ReadObject<Slot>(context);
			float3 localVector = 1.ReadValue<float3>(context);
			return slot?.LocalVectorToGlobal(in localVector) ?? localVector;
		}
	}
	[NodeCategory("Transform/Conversion")]
	[ContinuouslyChanging]
	public class TransformVector : ValueFunctionNode<FrooxEngineContext, float3>
	{
		public ObjectArgument<Slot> FromSpace;

		public ObjectArgument<Slot> ToSpace;

		public ValueArgument<float3> SourceVector;

		protected override float3 Compute(FrooxEngineContext context)
		{
			return (0.ReadObject<Slot>(context) ?? context.World.RootSlot).LocalVectorToSpace(space: 1.ReadObject<Slot>(context) ?? context.World.RootSlot, localVector: 2.ReadValue<float3>(context));
		}
	}
	[NodeCategory("Transform/Conversion")]
	[ContinuouslyChanging]
	public class GlobalRotationToLocal : ValueFunctionNode<FrooxEngineContext, floatQ>
	{
		public ObjectArgument<Slot> Instance;

		public ValueArgument<floatQ> GlobalRotation;

		public static floatQ GlobalRotationDefault => floatQ.Identity;

		protected override floatQ Compute(FrooxEngineContext context)
		{
			Slot slot = 0.ReadObject<Slot>(context);
			floatQ globalRotation = 1.ReadValue<floatQ>(context);
			return slot?.GlobalRotationToLocal(in globalRotation) ?? globalRotation;
		}
	}
	[NodeCategory("Transform/Conversion")]
	[ContinuouslyChanging]
	public class LocalRotationToGlobal : ValueFunctionNode<FrooxEngineContext, floatQ>
	{
		public ObjectArgument<Slot> Instance;

		public ValueArgument<floatQ> LocalRotation;

		public static floatQ LocalRotationDefault => floatQ.Identity;

		protected override floatQ Compute(FrooxEngineContext context)
		{
			Slot slot = 0.ReadObject<Slot>(context);
			floatQ localRotation = 1.ReadValue<floatQ>(context);
			return slot?.LocalRotationToGlobal(in localRotation) ?? localRotation;
		}
	}
	[NodeCategory("Transform/Conversion")]
	[ContinuouslyChanging]
	public class TransformRotation : ValueFunctionNode<FrooxEngineContext, floatQ>
	{
		public ObjectArgument<Slot> FromSpace;

		public ObjectArgument<Slot> ToSpace;

		public ValueArgument<floatQ> SourceRotation;

		public static floatQ SourceRotationDefault => floatQ.Identity;

		protected override floatQ Compute(FrooxEngineContext context)
		{
			return (0.ReadObject<Slot>(context) ?? context.World.RootSlot).LocalRotationToSpace(space: 1.ReadObject<Slot>(context) ?? context.World.RootSlot, localRotation: 2.ReadValue<floatQ>(context));
		}
	}
	[NodeCategory("Transform/Conversion")]
	[ContinuouslyChanging]
	public class GlobalScaleToLocal : ValueFunctionNode<FrooxEngineContext, float3>
	{
		public ObjectArgument<Slot> Instance;

		public ValueArgument<float3> GlobalScale;

		protected override float3 Compute(FrooxEngineContext context)
		{
			Slot slot = 0.ReadObject<Slot>(context);
			float3 globalScale = 1.ReadValue<float3>(context);
			return slot?.GlobalScaleToLocal(in globalScale) ?? globalScale;
		}
	}
	[NodeCategory("Transform/Conversion")]
	[ContinuouslyChanging]
	public class LocalScaleToGlobal : ValueFunctionNode<FrooxEngineContext, float3>
	{
		public ObjectArgument<Slot> Instance;

		public ValueArgument<float3> LocalScale;

		protected override float3 Compute(FrooxEngineContext context)
		{
			Slot slot = 0.ReadObject<Slot>(context);
			float3 localScale = 1.ReadValue<float3>(context);
			return slot?.LocalScaleToGlobal(in localScale) ?? localScale;
		}
	}
	[NodeCategory("Transform/Conversion")]
	[ContinuouslyChanging]
	public class TransformScale : ValueFunctionNode<FrooxEngineContext, float3>
	{
		public ObjectArgument<Slot> FromSpace;

		public ObjectArgument<Slot> ToSpace;

		public ValueArgument<float3> SourceScale;

		protected override float3 Compute(FrooxEngineContext context)
		{
			return (0.ReadObject<Slot>(context) ?? context.World.RootSlot).LocalScaleToSpace(space: 1.ReadObject<Slot>(context) ?? context.World.RootSlot, localScale: 2.ReadValue<float3>(context));
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Users
{
	[NodeCategory("Users")]
	public class SetUserScale : AsyncActionNode<FrooxEngineContext>
	{
		public ObjectInput<UserRoot> UserRoot;

		[ProtoFlux.Core.DefaultValue(1f)]
		public ValueInput<float> Scale;

		public ValueInput<float> AnimationTime;

		public AsyncCall OnScaleChangeStart;

		public Continuation OnAnimationFinished;

		protected override async Task<IOperation> RunAsync(FrooxEngineContext context)
		{
			UserRoot userRoot = UserRoot.Evaluate(context, context.LocalUser.Root);
			if (userRoot == null)
			{
				return null;
			}
			Task scaleTask = userRoot.SetUserScale(Scale.Evaluate(context, 1f), AnimationTime.Evaluate(context, 0f));
			await OnScaleChangeStart.ExecuteAsync(context);
			await scaleTask;
			return OnAnimationFinished.Target;
		}
	}
	[NodeCategory("Users")]
	public class LocalUser : ObjectFunctionNode<FrooxEngineContext, global::FrooxEngine.User>
	{
		protected override global::FrooxEngine.User Compute(FrooxEngineContext context)
		{
			return context.World.LocalUser;
		}
	}
	[NodeCategory("Users")]
	public class HostUser : ObjectFunctionNode<FrooxEngineContext, global::FrooxEngine.User>
	{
		protected override global::FrooxEngine.User Compute(FrooxEngineContext context)
		{
			return context.World.HostUser;
		}
	}
	[NodeCategory("Users")]
	[ContinuouslyChanging]
	public class LocalUserRoot : ObjectFunctionNode<FrooxEngineContext, UserRoot>
	{
		protected override UserRoot Compute(FrooxEngineContext context)
		{
			return context.World.LocalUser?.Root;
		}
	}
	[NodeCategory("Users")]
	[ContinuouslyChanging]
	public class LocalUserSlot : ObjectFunctionNode<FrooxEngineContext, Slot>
	{
		protected override Slot Compute(FrooxEngineContext context)
		{
			return context.World.LocalUser?.Root?.Slot;
		}
	}
	[NodeCategory("Users")]
	[ContinuouslyChanging]
	public class LocalUserSpace : ObjectFunctionNode<FrooxEngineContext, Slot>
	{
		protected override Slot Compute(FrooxEngineContext context)
		{
			return context.World.LocalUserSpace;
		}
	}
	[NodeCategory("Users")]
	public class UserUserID : ObjectFunctionNode<FrooxEngineContext, string>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override string Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.UserID;
		}
	}
	[NodeCategory("Users")]
	public class UserMachineID : ObjectFunctionNode<FrooxEngineContext, string>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override string Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.MachineID;
		}
	}
	[NodeCategory("Users/Status")]
	public class IsUserHost : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.IsHost ?? false;
		}
	}
	[NodeCategory("Users/Status")]
	public class IsLocalUser : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.IsLocalUser ?? false;
		}
	}
	[NodeCategory("Users/Status")]
	[ContinuouslyChanging]
	public class IsUserPresent : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.IsPresent ?? false;
		}
	}
	[NodeCategory("Users/Status")]
	[ContinuouslyChanging]
	public class IsUserPresentInHeadset : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.IsPresentInHeadset ?? false;
		}
	}
	[NodeCategory("Users/Status")]
	[ContinuouslyChanging]
	public class IsUserPresentInWorld : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.IsPresentInWorld ?? false;
		}
	}
	[NodeCategory("Users/Status")]
	[ContinuouslyChanging]
	public class IsUserLagging : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.IsLagging ?? false;
		}
	}
	[NodeCategory("Users/Status")]
	[ContinuouslyChanging]
	public class IsAppDashOpened : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.IsAppDashOpened ?? false;
		}
	}
	[NodeCategory("Users/Status")]
	[ContinuouslyChanging]
	public class IsPlatformDashOpened : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.IsPlatformDashOpened ?? false;
		}
	}
	[NodeCategory("Users/Status")]
	[ContinuouslyChanging]
	public class AreAppFacetsOpened : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.AreAppFacetsOpened ?? false;
		}
	}
	[NodeName("User VR Active", false)]
	[NodeCategory("Users/Info")]
	[ContinuouslyChanging]
	public class UserVR_Active : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.VR_Active ?? false;
		}
	}
	[NodeCategory("Users/Info")]
	[ContinuouslyChanging]
	public class UserPrimaryHand : ValueFunctionNode<FrooxEngineContext, Chirality>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override Chirality Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.Primaryhand ?? ((Chirality)(-1));
		}
	}
	[NodeCategory("Users/Status")]
	public class IsUserPatron : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.IsPatron ?? false;
		}
	}
	[NodeCategory("Users/Info")]
	[ContinuouslyChanging]
	public class UserVoiceMode : ValueFunctionNode<FrooxEngineContext, VoiceMode>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override VoiceMode Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.ActiveVoiceMode ?? VoiceMode.Mute;
		}
	}
	[NodeCategory("Users/Info")]
	[ContinuouslyChanging]
	public class UserRecordingVoiceMessage : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.RecordingVoiceMessage ?? false;
		}
	}
	[NodeCategory("Users/Info")]
	[ContinuouslyChanging]
	public class UserActiveViewTargettingController : ObjectFunctionNode<FrooxEngineContext, IViewTargettingController>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override IViewTargettingController Compute(FrooxEngineContext context)
		{
			global::FrooxEngine.User user = 0.ReadObject<global::FrooxEngine.User>(context);
			if (user == null || user.VR_Active)
			{
				return null;
			}
			return user.GetScreen()?.ActiveTargetting.Target;
		}
	}
	[NodeCategory("Users/Info")]
	[ContinuouslyChanging]
	public class UserViewReferenceActive : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.Root?.GetRegisteredComponent<ViewReferenceController>()?.IsVisualReferenceActive == true;
		}
	}
	[NodeCategory("Users/Info")]
	[ContinuouslyChanging]
	public class UserViewVoiceActive : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override bool Compute(FrooxEngineContext context)
		{
			RawOutput<bool> rawOutput = 0.ReadObject<global::FrooxEngine.User>(context)?.Root?.GetRegisteredComponent<ViewReferenceController>()?.ShouldVoiceBeActive;
			if (rawOutput == null)
			{
				return false;
			}
			return rawOutput;
		}
	}
	[NodeCategory("Users/Status")]
	[ContinuouslyChanging]
	public class IsUserSilenced : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.IsSilenced ?? false;
		}
	}
	[NodeCategory("Users")]
	[ContinuouslyChanging]
	public class UserUsername : ObjectFunctionNode<FrooxEngineContext, string>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override string Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.UserName;
		}
	}
	[NodeCategory("Users")]
	[ContinuouslyChanging]
	public class UserUserRoot : ObjectFunctionNode<FrooxEngineContext, UserRoot>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override UserRoot Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.Root;
		}
	}
	[NodeCategory("Users")]
	[ContinuouslyChanging]
	public class UserRootSlot : ObjectFunctionNode<FrooxEngineContext, Slot>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override Slot Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.Root?.Slot;
		}
	}
	[NodeCategory("Users/Status")]
	[ContinuouslyChanging]
	public class IsUserInEditMode : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.EditMode ?? false;
		}
	}
	[NodeCategory("Users/Status")]
	[ContinuouslyChanging]
	public class IsUserLive : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.IsLive ?? false;
		}
	}
	[NodeCategory("Users/Status")]
	[ContinuouslyChanging]
	public class IsUserInKioskMode : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.KioskMode ?? false;
		}
	}
	[NodeCategory("Users/Info")]
	[ContinuouslyChanging]
	public class UserFPS : ValueFunctionNode<FrooxEngineContext, float>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override float Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.FPS ?? 0f;
		}
	}
	[NodeCategory("Users/Info")]
	[ContinuouslyChanging]
	public class UserPing : ValueFunctionNode<FrooxEngineContext, int>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override int Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.Ping ?? 0;
		}
	}
	[NodeCategory("Users/Info")]
	[ContinuouslyChanging]
	public class UserQueuedMessages : ValueFunctionNode<FrooxEngineContext, int>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override int Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.QueuedMessages ?? 0;
		}
	}
	[NodeCategory("Users/Info")]
	[ContinuouslyChanging]
	public class UserPacketLoss : ValueFunctionNode<FrooxEngineContext, float>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override float Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.PacketLoss ?? 0f;
		}
	}
	[NodeCategory("Users/Info")]
	[ContinuouslyChanging]
	public class UserDeltaMessages : ValueFunctionNode<FrooxEngineContext, int>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override int Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.GeneratedDeltaMessages ?? 0;
		}
	}
	[NodeCategory("Users/Info")]
	[ContinuouslyChanging]
	[NodeOverload("User.UserNetworkStatistic")]
	[OldTypeName("ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Users.UserNetworkStatistic", null)]
	public class UserNumericNetworkStatistic<T> : ObjectFunctionNode<FrooxEngineContext, T?> where T : struct
	{
		public ObjectArgument<string> Name;

		public ObjectArgument<global::FrooxEngine.User> User;

		protected override T? Compute(FrooxEngineContext context)
		{
			global::FrooxEngine.User user = 1.ReadObject<global::FrooxEngine.User>(context);
			if (user == null)
			{
				return null;
			}
			string text = 0.ReadObject<string>(context);
			if (string.IsNullOrEmpty(text))
			{
				return null;
			}
			if (user.TryGetNetworkStatistic<T>(text, out var value))
			{
				return value;
			}
			return null;
		}
	}
	[NodeCategory("Users/Info")]
	[ContinuouslyChanging]
	[NodeOverload("User.UserNetworkStatistic")]
	public class UserObjectNetworkStatistic<T> : ObjectFunctionNode<FrooxEngineContext, T>
	{
		public ObjectArgument<string> Name;

		public ObjectArgument<global::FrooxEngine.User> User;

		protected override T Compute(FrooxEngineContext context)
		{
			global::FrooxEngine.User user = 1.ReadObject<global::FrooxEngine.User>(context);
			if (user == null)
			{
				return default(T);
			}
			string text = 0.ReadObject<string>(context);
			if (string.IsNullOrEmpty(text))
			{
				return default(T);
			}
			if (user.TryGetNetworkStatistic<T>(text, out var value))
			{
				return value;
			}
			return default(T);
		}
	}
	[NodeCategory("Users/Info")]
	[ContinuouslyChanging]
	public class UserStreamMessages : ValueFunctionNode<FrooxEngineContext, int>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override int Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.GeneratedStreamMessages ?? 0;
		}
	}
	[NodeCategory("Users/Info")]
	[ContinuouslyChanging]
	public class UserControlMessages : ValueFunctionNode<FrooxEngineContext, int>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override int Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.GeneratedControlMessages ?? 0;
		}
	}
	[NodeCategory("Users/Info")]
	[ContinuouslyChanging]
	public class UserDownloadedBytes : ValueFunctionNode<FrooxEngineContext, ulong>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override ulong Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.DownloadedBytes ?? 0;
		}
	}
	[NodeCategory("Users/Info")]
	[ContinuouslyChanging]
	public class UserUploadedBytes : ValueFunctionNode<FrooxEngineContext, ulong>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override ulong Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.UploadedBytes ?? 0;
		}
	}
	[NodeCategory("Users/Info")]
	[ContinuouslyChanging]
	public class UserDownloadSpeed : ValueFunctionNode<FrooxEngineContext, float>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override float Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.DownloadSpeed ?? 0f;
		}
	}
	[NodeCategory("Users/Info")]
	[ContinuouslyChanging]
	public class UserDownloadSpeedMax : ValueFunctionNode<FrooxEngineContext, float>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override float Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.DownloadMax ?? 0f;
		}
	}
	[NodeCategory("Users/Info")]
	[ContinuouslyChanging]
	public class UserUploadSpeed : ValueFunctionNode<FrooxEngineContext, float>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override float Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.UploadSpeed ?? 0f;
		}
	}
	[NodeCategory("Users/Info")]
	[ContinuouslyChanging]
	public class UserUploadSpeedMax : ValueFunctionNode<FrooxEngineContext, float>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override float Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.UploadMax ?? 0f;
		}
	}
	[NodeCategory("Users/Info")]
	public class UserPlatform : ValueFunctionNode<FrooxEngineContext, Platform>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override Platform Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.Platform ?? ((Platform)(-1));
		}
	}
	[NodeCategory("Users/Info")]
	public class UserEngineVersion : ObjectFunctionNode<FrooxEngineContext, string>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override string Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.EngineVersionNumber;
		}
	}
	[NodeCategory("Users/Info")]
	public class UserRendererName : ObjectFunctionNode<FrooxEngineContext, string>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override string Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.RendererName;
		}
	}
	[NodeCategory("Users/Info")]
	public class UserRuntimeVersion : ObjectFunctionNode<FrooxEngineContext, string>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override string Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.RuntimeVersion;
		}
	}
	[NodeCategory("Users/Info")]
	public class UserHeadOutputDevice : ValueFunctionNode<FrooxEngineContext, HeadOutputDevice>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override HeadOutputDevice Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.HeadDevice ?? HeadOutputDevice.UNKNOWN;
		}
	}
	[NodeCategory("Components")]
	[NodeCategory("Users")]
	public class GetUserFromComponent : ObjectFunctionNode<FrooxEngineContext, global::FrooxEngine.User>
	{
		public ObjectArgument<global::FrooxEngine.IComponent> Instance;

		protected override global::FrooxEngine.User Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.IComponent>(context)?.Slot.ActiveUser;
		}
	}
	[NodeCategory("Users")]
	[ContinuouslyChanging]
	public class UserFromID : ObjectFunctionNode<FrooxEngineContext, global::FrooxEngine.User>
	{
		public ObjectArgument<string> UserId;

		public ObjectArgument<string> MachineId;

		protected override global::FrooxEngine.User Compute(FrooxEngineContext context)
		{
			string userId = 0.ReadObject<string>(context);
			if (!string.IsNullOrEmpty(userId))
			{
				global::FrooxEngine.User user = context.World.FindUser((global::FrooxEngine.User u) => u.UserID == userId);
				if (user != null)
				{
					return user;
				}
			}
			string machineId = 1.ReadObject<string>(context);
			if (!string.IsNullOrEmpty(machineId))
			{
				global::FrooxEngine.User user2 = context.World.FindUser((global::FrooxEngine.User u) => u.MachineID == machineId);
				if (user2 != null)
				{
					return user2;
				}
			}
			return null;
		}
	}
	[NodeCategory("Users")]
	[ContinuouslyChanging]
	public class UserFromUsername : ObjectFunctionNode<FrooxEngineContext, global::FrooxEngine.User>
	{
		public ObjectArgument<string> Username;

		[ProtoFlux.Core.DefaultValue(true)]
		public ValueArgument<bool> IgnoreCase;

		public ValueArgument<bool> AllowPartialMatch;

		protected override global::FrooxEngine.User Compute(FrooxEngineContext context)
		{
			string text = 0.ReadObject<string>(context);
			if (string.IsNullOrWhiteSpace(text))
			{
				return null;
			}
			bool flag = 2.ReadValue<bool>(context);
			StringComparison comparisonType = (1.ReadValue<bool>(context) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
			global::FrooxEngine.User user = null;
			string text2 = null;
			foreach (global::FrooxEngine.User allUser in context.World.AllUsers)
			{
				string userName = allUser.UserName;
				if (string.IsNullOrEmpty(userName))
				{
					continue;
				}
				if (flag)
				{
					if (userName.StartsWith(text, comparisonType) && (user == null || text2.Length < userName.Length))
					{
						user = allUser;
						text2 = userName;
					}
				}
				else if (text.Equals(userName, comparisonType))
				{
					return allUser;
				}
			}
			return user;
		}
	}
	[NodeCategory("Users/Info")]
	public class LocalTimeOffset : ValueFunctionNode<FrooxEngineContext, TimeSpan>
	{
		protected override TimeSpan Compute(FrooxEngineContext context)
		{
			return context.World.LocalUser.UTCOffset;
		}
	}
	[NodeCategory("Users/Info")]
	public class UserTimeOffset : ValueFunctionNode<FrooxEngineContext, TimeSpan>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override TimeSpan Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.UTCOffset ?? TimeSpan.Zero;
		}
	}
	[NodeCategory("Users/Info")]
	[ContinuouslyChanging]
	public class UserTime : ValueFunctionNode<FrooxEngineContext, DateTime>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override DateTime Compute(FrooxEngineContext context)
		{
			return DateTime.SpecifyKind(DateTime.UtcNow.Add(0.ReadObject<global::FrooxEngine.User>(context)?.UTCOffset ?? TimeSpan.Zero), DateTimeKind.Unspecified);
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Users.Roots
{
	[NodeCategory("Users/User Root")]
	[ContinuouslyChanging]
	public class ActiveUserRootUser : ObjectFunctionNode<FrooxEngineContext, global::FrooxEngine.User>
	{
		public ObjectArgument<UserRoot> UserRoot;

		protected override global::FrooxEngine.User Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<UserRoot>(context)?.ActiveUser;
		}
	}
	[NodeCategory("Users/User Root")]
	[ContinuouslyChanging]
	public class HeadSlot : ObjectFunctionNode<FrooxEngineContext, Slot>
	{
		public ObjectArgument<UserRoot> UserRoot;

		protected override Slot Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<UserRoot>(context)?.HeadSlot;
		}
	}
	[NodeCategory("Users/User Root")]
	[ContinuouslyChanging]
	public class ControllerSlot : ObjectFunctionNode<FrooxEngineContext, Slot>
	{
		public ObjectArgument<UserRoot> UserRoot;

		public ValueArgument<Chirality> Side;

		protected override Slot Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<UserRoot>(context)?.GetControllerSlot(1.ReadValue<Chirality>(context), throwOnInvalid: false);
		}
	}
	[NodeCategory("Users/User Root")]
	[ContinuouslyChanging]
	public class HandSlot : ObjectFunctionNode<FrooxEngineContext, Slot>
	{
		public ObjectArgument<UserRoot> UserRoot;

		public ValueArgument<Chirality> Side;

		protected override Slot Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<UserRoot>(context)?.GetHandSlot(1.ReadValue<Chirality>(context), throwOnInvalid: false);
		}
	}
	[NodeCategory("Users/User Root")]
	[ContinuouslyChanging]
	public class HeadPosition : ValueFunctionNode<FrooxEngineContext, float3>
	{
		public ObjectArgument<UserRoot> UserRoot;

		protected override float3 Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<UserRoot>(context)?.HeadPosition ?? float3.Zero;
		}
	}
	[NodeCategory("Users/User Root")]
	[ContinuouslyChanging]
	public class HeadRotation : ValueFunctionNode<FrooxEngineContext, floatQ>
	{
		public ObjectArgument<UserRoot> UserRoot;

		protected override floatQ Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<UserRoot>(context)?.HeadRotation ?? floatQ.Identity;
		}
	}
	[NodeCategory("Users/User Root")]
	[ContinuouslyChanging]
	public class HeadFacingDirection : ValueFunctionNode<FrooxEngineContext, float3>
	{
		public ObjectArgument<UserRoot> UserRoot;

		protected override float3 Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<UserRoot>(context)?.HeadFacingDirection ?? float3.Zero;
		}
	}
	[NodeCategory("Users/User Root")]
	[ContinuouslyChanging]
	public class HeadFacingRotation : ValueFunctionNode<FrooxEngineContext, floatQ>
	{
		public ObjectArgument<UserRoot> UserRoot;

		protected override floatQ Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<UserRoot>(context)?.HeadFacingRotation ?? floatQ.Identity;
		}
	}
	[NodeCategory("Users/User Root")]
	[ContinuouslyChanging]
	public class LeftHandPosition : ValueFunctionNode<FrooxEngineContext, float3>
	{
		public ObjectArgument<UserRoot> UserRoot;

		protected override float3 Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<UserRoot>(context)?.LeftHandPosition ?? float3.Zero;
		}
	}
	[NodeCategory("Users/User Root")]
	[ContinuouslyChanging]
	public class LeftHandRotation : ValueFunctionNode<FrooxEngineContext, floatQ>
	{
		public ObjectArgument<UserRoot> UserRoot;

		protected override floatQ Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<UserRoot>(context)?.LeftHandRotation ?? floatQ.Identity;
		}
	}
	[NodeCategory("Users/User Root")]
	[ContinuouslyChanging]
	public class RightHandPosition : ValueFunctionNode<FrooxEngineContext, float3>
	{
		public ObjectArgument<UserRoot> UserRoot;

		protected override float3 Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<UserRoot>(context)?.RightHandPosition ?? float3.Zero;
		}
	}
	[NodeCategory("Users/User Root")]
	[ContinuouslyChanging]
	public class RightHandRotation : ValueFunctionNode<FrooxEngineContext, floatQ>
	{
		public ObjectArgument<UserRoot> UserRoot;

		protected override floatQ Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<UserRoot>(context)?.RightHandRotation ?? floatQ.Identity;
		}
	}
	[NodeCategory("Users/User Root")]
	[ContinuouslyChanging]
	public class HipsPosition : ValueFunctionNode<FrooxEngineContext, float3>
	{
		public ObjectArgument<UserRoot> UserRoot;

		protected override float3 Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<UserRoot>(context)?.HipsPosition ?? float3.Zero;
		}
	}
	[NodeCategory("Users/User Root")]
	[ContinuouslyChanging]
	public class HipsRotation : ValueFunctionNode<FrooxEngineContext, floatQ>
	{
		public ObjectArgument<UserRoot> UserRoot;

		protected override floatQ Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<UserRoot>(context)?.HipsRotation ?? floatQ.Identity;
		}
	}
	[NodeCategory("Users/User Root")]
	[ContinuouslyChanging]
	public class FeetPosition : ValueFunctionNode<FrooxEngineContext, float3>
	{
		public ObjectArgument<UserRoot> UserRoot;

		protected override float3 Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<UserRoot>(context)?.FeetPosition ?? float3.Zero;
		}
	}
	[NodeCategory("Users/User Root")]
	[ContinuouslyChanging]
	public class FeetRotation : ValueFunctionNode<FrooxEngineContext, floatQ>
	{
		public ObjectArgument<UserRoot> UserRoot;

		protected override floatQ Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<UserRoot>(context)?.FeetRotation ?? floatQ.Identity;
		}
	}
	[NodeCategory("Users/User Root")]
	[ContinuouslyChanging]
	public class UserRootGlobalScale : ValueFunctionNode<FrooxEngineContext, float>
	{
		public ObjectArgument<UserRoot> UserRoot;

		protected override float Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<UserRoot>(context)?.GlobalScale ?? 1f;
		}
	}
	[NodeCategory("Users/User Root")]
	public abstract class UserRootSetter : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<UserRoot> UserRoot;

		public static floatQ RotationDefault => floatQ.Identity;

		protected override bool Do(FrooxEngineContext context)
		{
			UserRoot userRoot = UserRoot.Evaluate(context);
			if (userRoot == null)
			{
				return false;
			}
			Set(userRoot, context);
			return true;
		}

		protected abstract void Set(UserRoot root, FrooxEngineContext context);
	}
	public class SetHeadFacingDirection : UserRootSetter
	{
		public ValueInput<float3> Direction;

		protected override void Set(UserRoot root, FrooxEngineContext context)
		{
			root.HeadFacingDirection = Direction.Evaluate(context);
		}
	}
	public class SetHeadFacingRotation : UserRootSetter
	{
		public ValueInput<floatQ> Rotation;

		protected override void Set(UserRoot root, FrooxEngineContext context)
		{
			root.HeadFacingRotation = Rotation.Evaluate(context, UserRootSetter.RotationDefault);
		}
	}
	public class SetHeadPosition : UserRootSetter
	{
		public ValueInput<float3> Position;

		protected override void Set(UserRoot root, FrooxEngineContext context)
		{
			root.HeadPosition = Position.Evaluate(context);
		}
	}
	public class SetHeadRotation : UserRootSetter
	{
		public ValueInput<floatQ> Rotation;

		protected override void Set(UserRoot root, FrooxEngineContext context)
		{
			root.HeadRotation = Rotation.Evaluate(context, UserRootSetter.RotationDefault);
		}
	}
	public class SetHipsPosition : UserRootSetter
	{
		public ValueInput<float3> Position;

		protected override void Set(UserRoot root, FrooxEngineContext context)
		{
			root.HipsPosition = Position.Evaluate(context);
		}
	}
	public class SetHipsRotation : UserRootSetter
	{
		public ValueInput<floatQ> Rotation;

		protected override void Set(UserRoot root, FrooxEngineContext context)
		{
			root.HipsRotation = Rotation.Evaluate(context, UserRootSetter.RotationDefault);
		}
	}
	public class SetFeetPosition : UserRootSetter
	{
		public ValueInput<float3> Position;

		protected override void Set(UserRoot root, FrooxEngineContext context)
		{
			root.FeetPosition = Position.Evaluate(context);
		}
	}
	public class SetFeetRotation : UserRootSetter
	{
		public ValueInput<floatQ> Rotation;

		protected override void Set(UserRoot root, FrooxEngineContext context)
		{
			root.FeetRotation = Rotation.Evaluate(context, UserRootSetter.RotationDefault);
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Users.LocalScreen
{
	[NodeCategory("Transform/Conversion")]
	[ContinuouslyChanging]
	public class LocalScreenPointToDirection : ValueFunctionNode<FrooxEngineContext, float3>
	{
		public ValueArgument<float2> NormalizedScreenPoint;

		public static float2 NormalizedScreenPointDefault => float2.One * 0.5f;

		protected override float3 Compute(FrooxEngineContext context)
		{
			return context.World.ScreenPointToDirection(0.ReadValue<float2>(context));
		}
	}
	[NodeCategory("Transform/Conversion")]
	[ContinuouslyChanging]
	public class LocalScreenPointToWorld : ValueFunctionNode<FrooxEngineContext, float3>
	{
		public ValueArgument<float2> NormalizedScreenPoint;

		[ProtoFlux.Core.DefaultValue(1f)]
		public ValueArgument<float> Distance;

		public static float2 NormalizedScreenPointDefault => float2.One * 0.5f;

		protected override float3 Compute(FrooxEngineContext context)
		{
			return context.World.ScreenPointToWorld(0.ReadValue<float2>(context), 1.ReadValue<float>(context));
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Users.LocalOutput
{
	[NodeCategory("Users/Local Output")]
	[ContinuouslyChanging]
	public class ViewOverriden : ValueFunctionNode<FrooxEngineContext, bool>
	{
		protected override bool Compute(FrooxEngineContext context)
		{
			return context.World.OverrideViewPosition;
		}
	}
	[NodeCategory("Users/Local Output")]
	[ContinuouslyChanging]
	public class ViewPosition : ValueFunctionNode<FrooxEngineContext, float3>
	{
		protected override float3 Compute(FrooxEngineContext context)
		{
			return context.World.LocalUserViewPosition;
		}
	}
	[NodeCategory("Users/Local Output")]
	[ContinuouslyChanging]
	public class ViewRotation : ValueFunctionNode<FrooxEngineContext, floatQ>
	{
		protected override floatQ Compute(FrooxEngineContext context)
		{
			return context.World.LocalUserViewRotation;
		}
	}
	[NodeCategory("Users/Local Output")]
	[ContinuouslyChanging]
	public class ViewScale : ValueFunctionNode<FrooxEngineContext, float3>
	{
		protected override float3 Compute(FrooxEngineContext context)
		{
			return context.World.LocalUserViewScale;
		}
	}
	[NodeCategory("Users/Local Output")]
	[ContinuouslyChanging]
	public class EarsOverriden : ValueFunctionNode<FrooxEngineContext, bool>
	{
		protected override bool Compute(FrooxEngineContext context)
		{
			return !(context.World.LocalUser.Root?.IsPrimaryListenerActive ?? false);
		}
	}
	[NodeCategory("Users/Local Output")]
	[ContinuouslyChanging]
	public class EarsPosition : ValueFunctionNode<FrooxEngineContext, float3>
	{
		protected override float3 Compute(FrooxEngineContext context)
		{
			return context.World.Audio.LastPrimaryEarPosition;
		}
	}
	[NodeCategory("Users/Local Output")]
	[ContinuouslyChanging]
	public class EarsRotation : ValueFunctionNode<FrooxEngineContext, floatQ>
	{
		protected override floatQ Compute(FrooxEngineContext context)
		{
			return context.World.Audio.LastPrimaryEarRotation;
		}
	}
	[NodeCategory("Users/Local Output")]
	[ContinuouslyChanging]
	public class EarsScale : ValueFunctionNode<FrooxEngineContext, float3>
	{
		protected override float3 Compute(FrooxEngineContext context)
		{
			return context.World.Audio.LastPrimaryEarScale;
		}
	}
	[NodeCategory("Users/Local Output")]
	[ContinuouslyChanging]
	public class DesktopFOV : ValueFunctionNode<FrooxEngineContext, float>
	{
		protected override float Compute(FrooxEngineContext context)
		{
			return context.World.LocalUserDesktopFOV;
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Security
{
	[NodeCategory("Security")]
	public class AllowJoin : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<JoinRequestHandle> Handle;

		protected override bool Do(FrooxEngineContext context)
		{
			JoinRequestHandle joinRequestHandle = Handle.Evaluate(context);
			if (joinRequestHandle == null)
			{
				return false;
			}
			joinRequestHandle.grant = JoinGrant.Allow();
			return true;
		}
	}
	[NodeCategory("Security")]
	public class AssignRole : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<JoinRequestHandle> Handle;

		public ObjectInput<string> RoleName;

		protected override bool Do(FrooxEngineContext context)
		{
			JoinRequestHandle joinRequestHandle = Handle.Evaluate(context);
			if (joinRequestHandle == null)
			{
				return false;
			}
			joinRequestHandle.data.RoleOverride = RoleName.Evaluate(context);
			return true;
		}
	}
	[NodeCategory("Security")]
	public class DenyJoin : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<JoinRequestHandle> Handle;

		public ObjectInput<string> DenyReason;

		protected override bool Do(FrooxEngineContext context)
		{
			JoinRequestHandle joinRequestHandle = Handle.Evaluate(context);
			if (joinRequestHandle == null)
			{
				return false;
			}
			joinRequestHandle.grant = JoinGrant.Deny(DenyReason.Evaluate(context));
			return true;
		}
	}
	[DataModelType]
	public class JoinRequestHandle
	{
		public readonly SessionConnection data;

		public JoinGrant? grant;

		public JoinRequestHandle(SessionConnection data)
		{
			this.data = data;
		}
	}
	[NodeCategory("Security")]
	public class VerifyJoinRequest : ProxyVoidNode<FrooxEngineContext, VerifyJoinRequest.Proxy>
	{
		public class Proxy : ProtoFluxEngineProxy, IWorldUserJoinVerifier, IUserJoinVerifier, IWorldElement
		{
			public Func<SessionConnection, Task<JoinGrant?>> Handler;

			public async Task<JoinGrant?> VerifyJoinRequest(SessionConnection request)
			{
				if (Handler == null)
				{
					return null;
				}
				return await StartTask(async delegate
				{
					await default(ToWorld);
					return await Handler(request);
				});
			}

			protected override void InitializeSyncMembers()
			{
				base.InitializeSyncMembers();
			}

			public override ISyncMember GetSyncMember(int index)
			{
				return index switch
				{
					0 => persistent, 
					1 => updateOrder, 
					2 => EnabledField, 
					3 => Node, 
					4 => Path, 
					_ => throw new ArgumentOutOfRangeException(), 
				};
			}

			public static Proxy __New()
			{
				return new Proxy();
			}
		}

		public AsyncCall Verify;

		public readonly ObjectOutput<string> UserId;

		public readonly ObjectOutput<string> UserSessionId;

		public readonly ObjectOutput<string> MachineId;

		public readonly ObjectOutput<string> Username;

		public readonly ValueOutput<HeadOutputDevice> HeadOutputDevice;

		public readonly ValueOutput<Platform> Platform;

		public readonly ValueOutput<bool> IsInvited;

		public readonly ValueOutput<bool> IsContact;

		public readonly ValueOutput<bool> IsInKioskMode;

		public readonly ValueOutput<bool> IsSpectatorBanned;

		public readonly ValueOutput<bool> IsMuteBanned;

		public readonly ObjectOutput<JoinRequestHandle> Handle;

		public override bool CanBeEvaluated => false;

		private Task Execute(SessionConnection connection, JoinRequestHandle handle, FrooxEngineContext context)
		{
			UserId.Write(connection.UserID, context);
			UserSessionId.Write(connection.UserSessionID, context);
			MachineId.Write(connection.MachineID, context);
			Username.Write(connection.Username, context);
			HeadOutputDevice.Write(connection.HeadDevice, context);
			Platform.Write(connection.Platform, context);
			IsInvited.Write(context.World.IsUserAllowed(connection.UserID), context);
			IsContact.Write(context.Cloud.Contacts.IsContact(connection.UserID), context);
			IsInKioskMode.Write(connection.KioskMode, context);
			IsSpectatorBanned.Write(connection.SpectatorBan, context);
			IsMuteBanned.Write(connection.MuteBan, context);
			Handle.Write(handle, context);
			return Verify.ExecuteAsync(context);
		}

		protected override void ProxyAdded(Proxy proxy, FrooxEngineContext context)
		{
			NodeContextPath path = context.CaptureContextPath();
			ProtoFluxNodeGroup group = context.Group;
			proxy.Handler = async delegate(SessionConnection request)
			{
				JoinRequestHandle handle = new JoinRequestHandle(request);
				await group.ExecuteImmediatellyAsync(path, async delegate(FrooxEngineContext c)
				{
					await Execute(request, handle, c);
				});
				return handle.grant;
			};
		}

		protected override void ProxyRemoved(Proxy proxy, FrooxEngineContext context, bool inUseByAnotherInstance)
		{
			if (!inUseByAnotherInstance)
			{
				proxy.Handler = null;
			}
		}

		public VerifyJoinRequest()
		{
			UserId = new ObjectOutput<string>(this);
			UserSessionId = new ObjectOutput<string>(this);
			MachineId = new ObjectOutput<string>(this);
			Username = new ObjectOutput<string>(this);
			HeadOutputDevice = new ValueOutput<HeadOutputDevice>(this);
			Platform = new ValueOutput<Platform>(this);
			IsInvited = new ValueOutput<bool>(this);
			IsContact = new ValueOutput<bool>(this);
			IsInKioskMode = new ValueOutput<bool>(this);
			IsSpectatorBanned = new ValueOutput<bool>(this);
			IsMuteBanned = new ValueOutput<bool>(this);
			Handle = new ObjectOutput<JoinRequestHandle>(this);
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Undo
{
	[NodeCategory("Undo")]
	public class CreateUndoBatch : ActionNode<FrooxEngineContext>
	{
		public ObjectInput<string> Description;

		public Call Create;

		public Continuation OnCreated;

		protected override IOperation Run(FrooxEngineContext context)
		{
			context.World.BeginUndoBatch(Description.Evaluate(context));
			Create.Execute(context);
			context.World.EndUndoBatch();
			return OnCreated.Target;
		}
	}
	[NodeCategory("Undo")]
	public class BeginUndoBatch : ActionFlowNode<FrooxEngineContext>
	{
		public ObjectInput<string> Description;

		protected override void Do(FrooxEngineContext context)
		{
			context.World.BeginUndoBatch(Description.Evaluate(context));
		}
	}
	[NodeCategory("Undo")]
	public class EndUndoBatch : ActionFlowNode<FrooxEngineContext>
	{
		protected override void Do(FrooxEngineContext context)
		{
			context.World.EndUndoBatch();
		}
	}
	[NodeCategory("Undo")]
	public class CreateTransformUndoStep : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<Slot> Target;

		[ProtoFlux.Core.DefaultValue(true)]
		public ValueInput<bool> SaveParent;

		[ProtoFlux.Core.DefaultValue(true)]
		public ValueInput<bool> SavePosition;

		[ProtoFlux.Core.DefaultValue(true)]
		public ValueInput<bool> SaveRotation;

		[ProtoFlux.Core.DefaultValue(true)]
		public ValueInput<bool> SaveScale;

		protected override bool Do(FrooxEngineContext context)
		{
			Slot slot = Target.Evaluate(context);
			if (slot == null || slot.IsRemoved)
			{
				return false;
			}
			slot.CreateTransformUndoState(SaveParent.Evaluate(context, defaultValue: true), SavePosition.Evaluate(context, defaultValue: true), SaveRotation.Evaluate(context, defaultValue: true), SaveScale.Evaluate(context, defaultValue: true));
			return true;
		}
	}
	[NodeCategory("Undo")]
	public class CreateSpawnUndoStep : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<Slot> Target;

		public ObjectInput<string> Description;

		protected override bool Do(FrooxEngineContext context)
		{
			Slot slot = Target.Evaluate(context);
			if (slot == null || slot.IsRemoved)
			{
				return false;
			}
			slot.CreateSpawnUndoPoint(Description.Evaluate(context));
			return true;
		}
	}
	[NodeCategory("Undo")]
	public class UndoableDestroy : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<Slot> Target;

		public ValueInput<bool> PreserveAssets;

		protected override bool Do(FrooxEngineContext context)
		{
			Slot slot = Target.Evaluate(context);
			if (slot == null || slot.IsRemoved)
			{
				return false;
			}
			if (PreserveAssets.Evaluate(context, defaultValue: false))
			{
				slot.UndoableDestroyPreservingAssets();
			}
			else
			{
				slot.UndoableDestroy();
			}
			return true;
		}
	}
	[NodeCategory("Undo")]
	public class CreateFieldUndoStep : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<IField> Target;

		public ValueInput<bool> ForceNew;

		protected override bool Do(FrooxEngineContext context)
		{
			IField field = Target.Evaluate(context);
			if (field == null || field.IsRemoved)
			{
				return false;
			}
			field.CreateUndoPoint(ForceNew.Evaluate(context, defaultValue: false));
			return true;
		}
	}
	[NodeCategory("Undo")]
	public class CreateReferenceUndoStep : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<ISyncRef> Target;

		public ValueInput<bool> ForceNew;

		protected override bool Do(FrooxEngineContext context)
		{
			ISyncRef syncRef = Target.Evaluate(context);
			if (syncRef == null || syncRef.IsRemoved)
			{
				return false;
			}
			syncRef.CreateUndoPoint(ForceNew.Evaluate(context, defaultValue: false));
			return true;
		}
	}
	[NodeCategory("Undo")]
	public class CreateTypeFieldUndoStep : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<SyncType> Target;

		public ValueInput<bool> ForceNew;

		protected override bool Do(FrooxEngineContext context)
		{
			SyncType syncType = Target.Evaluate(context);
			if (syncType == null || syncType.IsRemoved)
			{
				return false;
			}
			syncType.CreateUndoPoint(ForceNew.Evaluate(context, defaultValue: false));
			return true;
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Interactions
{
	[NodeCategory("Utility")]
	public class NotifyModified : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<global::FrooxEngine.IComponent> ModifiedComponent;

		protected override bool Do(FrooxEngineContext context)
		{
			global::FrooxEngine.IComponent component = ModifiedComponent.Evaluate(context);
			if (component == null)
			{
				return false;
			}
			component.NotifyModified();
			return true;
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Utility
{
	internal readonly struct TimepointValue<T>
	{
		public readonly T value;

		public readonly double timestamp;

		public TimepointValue(T value, double timestamp)
		{
			this.value = value;
			this.timestamp = timestamp;
		}
	}
	[NodeCategory("Utility")]
	[NodeName("Delay", false)]
	[NodeOverload("Engine.DelayValue")]
	public abstract class DelayBase<T> : UpdateBase
	{
		public ValueInput<float> DelaySeconds;

		private ObjectStore<Queue<TimepointValue<T>>> _buffer;

		protected override void RunUpdate(FrooxEngineContext context)
		{
			Queue<TimepointValue<T>> queue = _buffer.Read(context);
			if (queue == null)
			{
				queue = new Queue<TimepointValue<T>>();
				_buffer.Write(queue, context);
			}
			double worldTime = context.Time.WorldTime;
			float num = MathX.Max(0f, DelaySeconds.Evaluate(context, 0f));
			while (queue.Count > 0 && worldTime - queue.Peek().timestamp > (double)num)
			{
				queue.Dequeue();
			}
			T value = EvaluateCurrent(context);
			queue.Enqueue(new TimepointValue<T>(value, worldTime));
		}

		protected T GetCurrent(FrooxEngineContext context)
		{
			Queue<TimepointValue<T>> queue = _buffer.Read(context);
			if (queue == null || queue.Count == 0)
			{
				return default(T);
			}
			return queue.Peek().value;
		}

		protected abstract T EvaluateCurrent(FrooxEngineContext context);
	}
	public class DelayValue<T> : DelayBase<T> where T : unmanaged
	{
		public ValueInput<T> Value;

		[ContinuouslyChanging]
		public readonly ValueOutput<T> DelayedValue;

		protected override T EvaluateCurrent(FrooxEngineContext context)
		{
			return Value.Evaluate(context);
		}

		protected override void ComputeOutputs(FrooxEngineContext context)
		{
			DelayedValue.Write(GetCurrent(context), context);
		}

		public DelayValue()
		{
			((DelayValue<>)(object)this).DelayedValue = new ValueOutput<T>(this);
		}
	}
	public class DelayObject<T> : DelayBase<T>
	{
		public ObjectInput<T> Value;

		[ContinuouslyChanging]
		public readonly ObjectOutput<T> DelayedValue;

		protected override T EvaluateCurrent(FrooxEngineContext context)
		{
			return Value.Evaluate(context);
		}

		protected override void ComputeOutputs(FrooxEngineContext context)
		{
			DelayedValue.Write(GetCurrent(context), context);
		}

		public DelayObject()
		{
			((DelayObject<>)(object)this).DelayedValue = new ObjectOutput<T>(this);
		}
	}
	[NodeCategory("Utility")]
	public class TypeColor : ValueFunctionNode<ExecutionContext, colorX>
	{
		public ObjectArgument<Type> Type;

		protected override colorX Compute(ExecutionContext context)
		{
			Type type = 0.ReadObject<Type>(context);
			if (type == null)
			{
				return default(colorX);
			}
			return type.GetTypeColor();
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Rendering
{
	[NodeCategory("Rendering")]
	public class RenderToTextureAsset : AsyncActionNode<FrooxEngineContext>
	{
		public const int MAX_RESOLUTION = 8192;

		public ObjectInput<Camera> Camera;

		public ValueInput<int2> Resolution;

		[ProtoFlux.Core.DefaultValue("webp")]
		public ObjectInput<string> Format;

		[ProtoFlux.Core.DefaultValue(200)]
		public ValueInput<int> Quality;

		public AsyncCall OnRenderStarted;

		public Continuation OnRendered;

		public Continuation OnFailed;

		public readonly ObjectOutput<Uri> RenderedAssetURL;

		protected override async Task<IOperation> RunAsync(FrooxEngineContext context)
		{
			Camera camera = Camera.Evaluate(context);
			if (camera == null)
			{
				return null;
			}
			int2 v = Resolution.Evaluate(context);
			if (MathX.MinComponent(in v) <= 0)
			{
				return null;
			}
			v = MathX.Min(in v, 8192);
			string format = Format.Evaluate(context, "webp");
			int quality = Quality.Evaluate(context, 200);
			if (context.Group.World != context.Engine.WorldManager.FocusedWorld)
			{
				return OnFailed.Target;
			}
			Task<Uri> renderTask = camera.RenderToAsset(v, format, quality);
			await OnRenderStarted.ExecuteAsync(context);
			Uri url = null;
			try
			{
				url = await renderTask;
			}
			catch (Exception value)
			{
				UniLog.Warning($"Failed rendering camera to texture:\n{value}");
			}
			RenderedAssetURL.Write(url, context);
			if (url != null)
			{
				return OnRendered.Target;
			}
			return OnFailed.Target;
		}

		public RenderToTextureAsset()
		{
			RenderedAssetURL = new ObjectOutput<Uri>(this);
		}
	}
	[NodeCategory("Rendering")]
	public class FlashHighlightHierarchy : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<Slot> HierarchyRoot;

		public ValueInput<bool> ExcludeColliders;

		public ValueInput<bool> ExcludeMeshes;

		public ValueInput<bool> ExcludeDisabled;

		[ProtoFlux.Core.DefaultValue(true)]
		public ValueInput<bool> TrackPosition;

		[ProtoFlux.Core.DefaultValue(0.5f)]
		public ValueInput<float> Duration;

		public ValueInput<colorX> Color;

		public readonly ObjectOutput<Slot> FlashRoot;

		public static color ColorDefault => color.White;

		protected override bool Do(FrooxEngineContext context)
		{
			Slot slot = HierarchyRoot.Evaluate(context);
			if (slot == null || slot.IsRemoved)
			{
				FlashRoot.Write(null, context);
				return false;
			}
			bool excludeColliders = ExcludeColliders.Evaluate(context, defaultValue: false);
			bool excludeMeshes = ExcludeMeshes.Evaluate(context, defaultValue: false);
			bool excludeDisabled = ExcludeDisabled.Evaluate(context, defaultValue: false);
			float duration = Duration.Evaluate(context, 0.5f);
			colorX color = Color.Evaluate(context);
			bool trackPosition = TrackPosition.Evaluate(context, defaultValue: true);
			Slot value = HighlightHelper.FlashHighlight(slot, delegate(IHighlightable h)
			{
				if (excludeColliders && h is ICollider)
				{
					return false;
				}
				return (!excludeMeshes || !(h is MeshRenderer)) ? true : false;
			}, color, duration, excludeDisabled, trackPosition);
			FlashRoot.Write(value, context);
			return true;
		}

		public FlashHighlightHierarchy()
		{
			FlashRoot = new ObjectOutput<Slot>(this);
		}
	}
	[NodeCategory("Rendering")]
	public class BakeReflectionProbe : AsyncActionNode<FrooxEngineContext>
	{
		public ObjectInput<ReflectionProbe> Probe;

		public AsyncCall OnBakeStart;

		public Continuation OnBakeFail;

		public Continuation OnBakeComplete;

		public readonly ObjectOutput<Uri> BakedCubemapURL;

		protected override async Task<IOperation> RunAsync(FrooxEngineContext context)
		{
			ReflectionProbe reflectionProbe = Probe.Evaluate(context);
			if (reflectionProbe == null)
			{
				return null;
			}
			if (reflectionProbe.ProbeType.Value != ReflectionProbeType.Baked)
			{
				return OnBakeFail.Target;
			}
			Task<Uri> bakeTask = reflectionProbe.BakeAsync();
			await OnBakeStart.ExecuteAsync(context);
			Uri uri = await bakeTask;
			BakedCubemapURL.Write(uri, context);
			if (uri != null)
			{
				return OnBakeComplete.Target;
			}
			return OnBakeFail.Target;
		}

		public BakeReflectionProbe()
		{
			BakedCubemapURL = new ObjectOutput<Uri>(this);
		}
	}
	[NodeCategory("Rendering")]
	public class BakeReflectionProbes : AsyncActionNode<FrooxEngineContext>
	{
		public ObjectInput<Slot> Root;

		public ValueInput<bool> BakeInactive;

		public ObjectInput<string> FilterWithTag;

		[ProtoFlux.Core.DefaultValue(0.25f)]
		public ValueInput<float> DelayBeforeBake;

		public readonly ObjectOutput<ReflectionProbe> Probe;

		public readonly ValueOutput<int> ProbeIndex;

		public readonly ValueOutput<int> ProbeCount;

		public AsyncCall OnBakeBatchStart;

		public AsyncCall OnBeforeProbeBake;

		public AsyncCall OnProbeBaked;

		public Continuation OnBakeBatchFinished;

		protected override async Task<IOperation> RunAsync(FrooxEngineContext context)
		{
			Slot slot = Root.Evaluate(context, context.World.RootSlot);
			if (slot == null || slot.IsRemoved)
			{
				return null;
			}
			bool includeInactive = BakeInactive.Evaluate(context, defaultValue: false);
			string tag = FilterWithTag.Evaluate(context);
			float delay = DelayBeforeBake.Evaluate(context, 0.25f);
			List<ReflectionProbe> probes = ReflectionProbe.GetBakedReflectionProbes(slot, includeInactive, tag);
			Probe.Write(null, context);
			ProbeIndex.Write(-1, context);
			ProbeCount.Write(probes.Count, context);
			await OnBakeBatchStart.ExecuteAsync(context);
			await ReflectionProbe.BakeReflectionProbes(probes, delay, async delegate(int i, ReflectionProbe probe)
			{
				Probe.Write(probe, context);
				ProbeIndex.Write(i, context);
				await OnBeforeProbeBake.ExecuteAsync(context);
			}, async delegate
			{
				await OnProbeBaked.ExecuteAsync(context);
			});
			Probe.Write(null, context);
			ProbeIndex.Write(-1, context);
			return OnBakeBatchFinished.Target;
		}

		public BakeReflectionProbes()
		{
			Probe = new ObjectOutput<ReflectionProbe>(this);
			ProbeIndex = new ValueOutput<int>(this);
			ProbeCount = new ValueOutput<int>(this);
		}
	}
	[NodeCategory("Rendering")]
	public class SampleColorX : AsyncActionNode<FrooxEngineContext>
	{
		public ValueInput<float3> Point;

		public ValueInput<float3> Direction;

		public ObjectInput<Slot> Reference;

		[ProtoFlux.Core.DefaultValue(0.01f)]
		public ValueInput<float> NearClip;

		[ProtoFlux.Core.DefaultValue(1024f)]
		public ValueInput<float> FarClip;

		public AsyncCall OnSampleStart;

		public Continuation OnSampled;

		public readonly ValueOutput<colorX> SampledColor;

		protected override async Task<IOperation> RunAsync(FrooxEngineContext context)
		{
			float3 localPoint = Point.Evaluate(context);
			float3 localDirection = Direction.Evaluate(context);
			float3 localDirection2 = float3.Up;
			Slot slot = Reference.Evaluate(context);
			if (slot != null && !slot.IsRemoved)
			{
				localPoint = slot.LocalPointToGlobal(in localPoint);
				localDirection = slot.LocalDirectionToGlobal(in localDirection);
				localDirection2 = slot.LocalDirectionToGlobal(in localDirection2);
			}
			RenderTask renderTask = new RenderTask(localPoint, floatQ.LookRotation(in localDirection, in localDirection2));
			renderTask.parameters.fov = 1f;
			renderTask.parameters.resolution = int2.One * 4;
			renderTask.parameters.postProcessing = false;
			renderTask.parameters.nearClip = NearClip.Evaluate(context, 0.01f);
			renderTask.parameters.farClip = FarClip.Evaluate(context, 1024f);
			renderTask.parameters.clearMode = CameraClearMode.Skybox;
			Task<Bitmap2D> renderTask2 = context.World.Render.RenderToBitmap(renderTask);
			await OnSampleStart.ExecuteAsync(context);
			colorX color = (await renderTask2.ConfigureAwait(continueOnCapturedContext: false)).AverageColor();
			await default(ToWorld);
			SampledColor.Write(color, context);
			return OnSampled.Target;
		}

		public SampleColorX()
		{
			SampledColor = new ValueOutput<colorX>(this);
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Nodes
{
	[NodeCategory("Nodes")]
	public class PackProtoFluxNodes : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<Slot> Root;

		public ObjectInput<Slot> Target;

		protected override bool Do(FrooxEngineContext context)
		{
			Slot slot = Root.Evaluate(context);
			if (slot == null)
			{
				return false;
			}
			Slot slot2 = Target.Evaluate(context);
			HashSet<ProtoFluxNodeGroup> hashSet = Pool.BorrowHashSet<ProtoFluxNodeGroup>();
			try
			{
				ProtoFluxNode protoFluxNode = null;
				foreach (ProtoFluxNode componentsInChild in slot.GetComponentsInChildren<ProtoFluxNode>())
				{
					if (componentsInChild.Group != null)
					{
						if (protoFluxNode == null)
						{
							protoFluxNode = componentsInChild;
						}
						hashSet.Add(componentsInChild.Group);
					}
				}
				if (hashSet.Count == 0)
				{
					return false;
				}
				if (slot2 == null)
				{
					hashSet.PackInPlace(protoFluxNode);
				}
				else
				{
					foreach (ProtoFluxNodeGroup item in hashSet)
					{
						item.Pack(slot2);
					}
				}
				return true;
			}
			finally
			{
				Pool.Return(ref hashSet);
			}
		}
	}
	[NodeCategory("Nodes")]
	public class PackProtoFluxFromNode : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<ProtoFluxNode> StartNode;

		public ObjectInput<Slot> Target;

		protected override bool Do(FrooxEngineContext context)
		{
			ProtoFluxNode protoFluxNode = StartNode.Evaluate(context);
			if (protoFluxNode == null)
			{
				return false;
			}
			Slot slot = Target.Evaluate(context);
			if (slot == null)
			{
				return false;
			}
			protoFluxNode.Pack(slot);
			return true;
		}
	}
	[NodeCategory("Nodes")]
	public class PackProtoFluxInPlace : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<ProtoFluxNode> StartNode;

		protected override bool Do(FrooxEngineContext context)
		{
			ProtoFluxNode protoFluxNode = StartNode.Evaluate(context);
			if (protoFluxNode == null || protoFluxNode.IsRemoved)
			{
				return false;
			}
			protoFluxNode.PackInPlace();
			return true;
		}
	}
	[NodeCategory("Nodes")]
	public class UnpackProtoFlux : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<Slot> Root;

		protected override bool Do(FrooxEngineContext context)
		{
			Slot slot = Root.Evaluate(context);
			if (slot == null || slot.IsRemoved)
			{
				return false;
			}
			slot.UnpackNodes();
			return true;
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Physics
{
	[NodeCategory("Physics")]
	public class IsCharacterController : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<ICollider> Collider;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<ICollider>(context)?.ColliderOwner is CharacterController;
		}
	}
	[NodeCategory("Physics")]
	public class AsCharacterController : ObjectFunctionNode<FrooxEngineContext, CharacterController>
	{
		public ObjectArgument<ICollider> Collider;

		protected override CharacterController Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<ICollider>(context)?.ColliderOwner as CharacterController;
		}
	}
	[NodeCategory("Physics")]
	public class CharacterControllerUser : ObjectFunctionNode<FrooxEngineContext, global::FrooxEngine.User>
	{
		public ObjectArgument<CharacterController> Character;

		protected override global::FrooxEngine.User Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<CharacterController>(context)?.SimulatingUser.Target;
		}
	}
	[NodeCategory("Physics")]
	[ContinuouslyChanging]
	public class FindCharacterControllerFromSlot : ObjectFunctionNode<FrooxEngineContext, CharacterController>
	{
		public ObjectArgument<Slot> Source;

		protected override CharacterController Compute(FrooxEngineContext context)
		{
			Slot slot = 0.ReadObject<Slot>(context);
			if (slot == null)
			{
				return null;
			}
			CharacterController characterController = slot.GetComponentInParents<CharacterController>(null, includeSelf: true, excludeDisabled: true);
			if (characterController == null)
			{
				IPhysicalLocomotion obj = slot.ActiveUserRoot?.GetRegisteredComponent<LocomotionController>()?.ActiveModule as IPhysicalLocomotion;
				if (obj == null)
				{
					return null;
				}
				characterController = obj.CharacterController;
			}
			return characterController;
		}
	}
	[NodeCategory("Physics")]
	[ContinuouslyChanging]
	public class FindCharacterControllerFromUser : ObjectFunctionNode<FrooxEngineContext, CharacterController>
	{
		public ObjectArgument<global::FrooxEngine.User> Source;

		protected override CharacterController Compute(FrooxEngineContext context)
		{
			return (0.ReadObject<global::FrooxEngine.User>(context)?.Root?.GetRegisteredComponent<LocomotionController>()?.ActiveModule as IPhysicalLocomotion)?.CharacterController;
		}
	}
	[NodeCategory("Physics")]
	[ContinuouslyChanging]
	public class CharacterLinearVelocity : ValueFunctionNode<FrooxEngineContext, float3>
	{
		public ObjectArgument<CharacterController> Character;

		protected override float3 Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<CharacterController>(context)?.LinearVelocity ?? float3.Zero;
		}
	}
	[NodeCategory("Physics")]
	[ContinuouslyChanging]
	public class IsCharacterOnGround : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<CharacterController> Character;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<CharacterController>(context)?.CurrentGround != null;
		}
	}
	[NodeCategory("Physics")]
	[ContinuouslyChanging]
	public class CharacterGravity : VoidNode<FrooxEngineContext>
	{
		public ObjectArgument<CharacterController> Character;

		public readonly ValueOutput<float3> Gravity;

		public readonly ValueOutput<float3> ActualGravity;

		protected override void ComputeOutputs(FrooxEngineContext context)
		{
			CharacterController characterController = 0.ReadObject<CharacterController>(context);
			if (characterController != null)
			{
				Gravity.Write(characterController.Gravity.Value, context);
				ActualGravity.Write(characterController.ActualGravity, context);
			}
			else
			{
				Gravity.Write(default(float3), context);
				ActualGravity.Write(default(float3), context);
			}
		}

		public CharacterGravity()
		{
			Gravity = new ValueOutput<float3>(this);
			ActualGravity = new ValueOutput<float3>(this);
		}
	}
	[NodeCategory("Physics")]
	[ContinuouslyChanging]
	public class CharacterGroundCollider : ObjectFunctionNode<FrooxEngineContext, ICollider>
	{
		public ObjectArgument<CharacterController> Character;

		protected override ICollider Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<CharacterController>(context)?.CurrentGround;
		}
	}
	[NodeCategory("Physics")]
	public class ApplyCharacterImpulse : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ValueInput<float3> Impulse;

		public ObjectInput<CharacterController> Character;

		public ValueInput<bool> IgnoreMass;

		protected override bool Do(FrooxEngineContext context)
		{
			CharacterController characterController = Character.Evaluate(context);
			if (characterController == null)
			{
				return false;
			}
			bool num = IgnoreMass.Evaluate(context, defaultValue: false);
			float3 v = Impulse.Evaluate(context);
			if (!num)
			{
				v /= characterController.ActualMass;
			}
			characterController.LinearVelocity += v;
			return true;
		}
	}
	[NodeCategory("Physics")]
	public class ApplyCharacterForce : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ValueInput<float3> Force;

		public ObjectInput<CharacterController> Character;

		public ValueInput<bool> IgnoreMass;

		protected override bool Do(FrooxEngineContext context)
		{
			CharacterController characterController = Character.Evaluate(context);
			if (characterController == null)
			{
				return false;
			}
			bool num = IgnoreMass.Evaluate(context, defaultValue: false);
			float3 v = Force.Evaluate(context) * context.World.Time.Delta;
			if (!num)
			{
				v /= characterController.ActualMass;
			}
			characterController.LinearVelocity += v;
			return true;
		}
	}
	[NodeCategory("Physics")]
	public class SetCharacterVelocity : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ValueInput<float3> Velocity;

		public ObjectInput<CharacterController> Character;

		protected override bool Do(FrooxEngineContext context)
		{
			CharacterController characterController = Character.Evaluate(context);
			if (characterController == null)
			{
				return false;
			}
			float3 linearVelocity = Velocity.Evaluate(context);
			characterController.LinearVelocity = linearVelocity;
			return true;
		}
	}
	[NodeCategory("Physics")]
	public class SetCharacterGravity : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ValueInput<float3> Gravity;

		public ObjectInput<CharacterController> Character;

		protected override bool Do(FrooxEngineContext context)
		{
			CharacterController characterController = Character.Evaluate(context);
			if (characterController == null)
			{
				return false;
			}
			float3 value = Gravity.Evaluate(context);
			characterController.Gravity.Value = value;
			return true;
		}
	}
	[NodeCategory("Physics/Events")]
	public abstract class LocomotionEvents : VoidNode<FrooxEngineContext>
	{
		public readonly GlobalRef<PhysicalLocomotion> Locomotion;

		private ObjectStore<PhysicalLocomotion> _current;

		public override bool CanBeEvaluated => false;

		private void OnLocomotionChanged(PhysicalLocomotion locomotion, FrooxEngineContext context)
		{
			PhysicalLocomotion physicalLocomotion = _current.Read(context);
			if (physicalLocomotion != locomotion)
			{
				if (physicalLocomotion != null)
				{
					Unregister(physicalLocomotion, context);
				}
				if (locomotion != null)
				{
					NodeContextPath path = context.CaptureContextPath();
					context.GetEventDispatcher(out ExecutionEventDispatcher<FrooxEngineContext> eventDispatcher);
					Register(locomotion, path, eventDispatcher, context);
					_current.Write(locomotion, context);
				}
				else
				{
					_current.Clear(context);
					Clear(context);
				}
			}
		}

		protected abstract void Register(PhysicalLocomotion locomotion, NodeContextPath path, ExecutionEventDispatcher<FrooxEngineContext> dispatcher, FrooxEngineContext context);

		protected abstract void Unregister(PhysicalLocomotion locomotion, FrooxEngineContext context);

		protected abstract void Clear(FrooxEngineContext context);

		protected LocomotionEvents()
		{
			Locomotion = new GlobalRef<PhysicalLocomotion>(this, 0);
		}
	}
	public abstract class LocomotionGripEvent : LocomotionEvents
	{
		public Call OnEvent;

		public readonly ObjectOutput<Slot> GrippedSlot;

		public readonly ValueOutput<float3> GrippedPoint;

		public readonly ValueOutput<Chirality> GrippingHand;

		private ObjectStore<PhysicalLocomotion.HandGripHandler> _handler;

		protected override void Register(PhysicalLocomotion locomotion, NodeContextPath path, ExecutionEventDispatcher<FrooxEngineContext> dispatcher, FrooxEngineContext context)
		{
			PhysicalLocomotion.HandGripHandler handGripHandler = delegate(Slot s, float3 p, Chirality h)
			{
				dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
				{
					Handle(s, in p, h, c);
				});
			};
			Register(locomotion, handGripHandler);
			_handler.Write(handGripHandler, context);
		}

		protected override void Unregister(PhysicalLocomotion locomotion, FrooxEngineContext context)
		{
			Unregister(locomotion, _handler.Read(context));
		}

		protected override void Clear(FrooxEngineContext context)
		{
			_handler.Clear(context);
		}

		private void Handle(Slot slot, in float3 point, Chirality hand, FrooxEngineContext context)
		{
			GrippedSlot.Write(slot, context);
			GrippedPoint.Write(point, context);
			GrippingHand.Write(hand, context);
			OnEvent.Execute(context);
		}

		protected abstract void Register(PhysicalLocomotion locomotion, PhysicalLocomotion.HandGripHandler handler);

		protected abstract void Unregister(PhysicalLocomotion locomotion, PhysicalLocomotion.HandGripHandler handler);

		protected LocomotionGripEvent()
		{
			GrippedSlot = new ObjectOutput<Slot>(this);
			GrippedPoint = new ValueOutput<float3>(this);
			GrippingHand = new ValueOutput<Chirality>(this);
		}
	}
	public class OnLocomotionGripBegin : LocomotionGripEvent
	{
		protected override void Register(PhysicalLocomotion locomotion, PhysicalLocomotion.HandGripHandler handler)
		{
			locomotion.LocalGripBegin += handler;
		}

		protected override void Unregister(PhysicalLocomotion locomotion, PhysicalLocomotion.HandGripHandler handler)
		{
			locomotion.LocalGripBegin -= handler;
		}
	}
	public class OnLocomotionGripEnd : LocomotionGripEvent
	{
		protected override void Register(PhysicalLocomotion locomotion, PhysicalLocomotion.HandGripHandler handler)
		{
			locomotion.LocalGripEnd += handler;
		}

		protected override void Unregister(PhysicalLocomotion locomotion, PhysicalLocomotion.HandGripHandler handler)
		{
			locomotion.LocalGripEnd -= handler;
		}
	}
	[NodeCategory("Physics/Events")]
	public abstract class ContactEventNode : VoidNode<FrooxEngineContext>
	{
		public readonly GlobalRef<ICollider> Collider;

		public Call OnEvent;

		public readonly ObjectOutput<ICollider> Other;

		private ObjectStore<ICollider> _current;

		private ObjectStore<ContactEvent> _handler;

		private NodeEventHandler<FrooxEngineContext> _cachedEventHandler;

		public override bool CanBeEvaluated => false;

		private void OnColliderChanged(ICollider collider, FrooxEngineContext context)
		{
			ICollider collider2 = _current.Read(context);
			if (collider2 == collider)
			{
				return;
			}
			if (collider2 != null)
			{
				Unregister(collider2, _handler.Read(context));
			}
			if (collider != null)
			{
				if (_cachedEventHandler == null)
				{
					_cachedEventHandler = HandleEvent;
				}
				NodeContextPath path = context.CaptureContextPath();
				context.GetEventDispatcher(out var dispatcher);
				ContactEvent contactEvent = delegate(ICollider c, ICollider o)
				{
					dispatcher.ScheduleEvent(path, _cachedEventHandler, o);
				};
				Register(collider, contactEvent);
				_current.Write(collider, context);
				_handler.Write(contactEvent, context);
			}
			else
			{
				_current.Clear(context);
				_handler.Clear(context);
			}
		}

		private void HandleEvent(FrooxEngineContext context, object other)
		{
			Other.Write(other as ICollider, context);
			OnEvent.Execute(context);
		}

		protected abstract void Register(ICollider collider, ContactEvent handler);

		protected abstract void Unregister(ICollider collider, ContactEvent handler);

		protected ContactEventNode()
		{
			Collider = new GlobalRef<ICollider>(this, 0);
			Other = new ObjectOutput<ICollider>(this);
		}
	}
	public class OnContactStart : ContactEventNode
	{
		protected override void Register(ICollider collider, ContactEvent handler)
		{
			collider.ContactStart += handler;
		}

		protected override void Unregister(ICollider collider, ContactEvent handler)
		{
			collider.ContactStart -= handler;
		}
	}
	public class OnContactStay : ContactEventNode
	{
		protected override void Register(ICollider collider, ContactEvent handler)
		{
			collider.ContactStay += handler;
		}

		protected override void Unregister(ICollider collider, ContactEvent handler)
		{
			collider.ContactStay -= handler;
		}
	}
	public class OnContactEnd : ContactEventNode
	{
		protected override void Register(ICollider collider, ContactEvent handler)
		{
			collider.ContactEnd += handler;
		}

		protected override void Unregister(ICollider collider, ContactEvent handler)
		{
			collider.ContactEnd -= handler;
		}
	}
	[NodeCategory("Physics/Events")]
	public abstract class GripEvents : VoidNode<FrooxEngineContext>
	{
		public readonly GlobalRef<LocomotionGrip> Grip;

		public Call OnEvent;

		public readonly ObjectOutput<ILocomotionModule> Module;

		public readonly ValueOutput<BodyNode> GrippingBodyNode;

		private ObjectStore<LocomotionGrip> _current;

		private ObjectStore<GripEvent> _handler;

		public override bool CanBeEvaluated => false;

		private void OnGripChanged(LocomotionGrip grip, FrooxEngineContext context)
		{
			LocomotionGrip locomotionGrip = _current.Read(context);
			if (locomotionGrip == grip)
			{
				return;
			}
			if (locomotionGrip != null)
			{
				Unregister(locomotionGrip, _handler.Read(context));
			}
			if (grip != null)
			{
				NodeContextPath path = context.CaptureContextPath();
				context.GetEventDispatcher(out var dispatcher);
				GripEvent gripEvent = delegate(ILocomotionModule m, BodyNode n)
				{
					dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
					{
						Handle(m, n, c);
					});
				};
				Register(grip, gripEvent);
				_current.Write(grip, context);
				_handler.Write(gripEvent, context);
			}
			else
			{
				_current.Clear(context);
				_handler.Clear(context);
			}
		}

		protected abstract void Register(LocomotionGrip grip, GripEvent handler);

		protected abstract void Unregister(LocomotionGrip grip, GripEvent handler);

		private void Handle(ILocomotionModule module, BodyNode node, FrooxEngineContext context)
		{
			Module.Write(module, context);
			GrippingBodyNode.Write(node, context);
			OnEvent.Execute(context);
		}

		protected GripEvents()
		{
			Grip = new GlobalRef<LocomotionGrip>(this, 0);
			Module = new ObjectOutput<ILocomotionModule>(this);
			GrippingBodyNode = new ValueOutput<BodyNode>(this);
		}
	}
	public class OnGripStart : GripEvents
	{
		protected override void Register(LocomotionGrip grip, GripEvent handler)
		{
			grip.LocalGripBegin += handler;
		}

		protected override void Unregister(LocomotionGrip grip, GripEvent handler)
		{
			grip.LocalGripBegin -= handler;
		}
	}
	public class OnGripStay : GripEvents
	{
		protected override void Register(LocomotionGrip grip, GripEvent handler)
		{
			grip.LocalGripStay += handler;
		}

		protected override void Unregister(LocomotionGrip grip, GripEvent handler)
		{
			grip.LocalGripStay -= handler;
		}
	}
	public class OnGripEnd : GripEvents
	{
		protected override void Register(LocomotionGrip grip, GripEvent handler)
		{
			grip.LocalGripEnd += handler;
		}

		protected override void Unregister(LocomotionGrip grip, GripEvent handler)
		{
			grip.LocalGripEnd -= handler;
		}
	}
	[NodeCategory("Physics")]
	public class HitUVCoordinate : VoidNode<FrooxEngineContext>
	{
		public ObjectArgument<ICollider> HitCollider;

		public ValueArgument<int> HitTriangleIndex;

		public ValueArgument<float3> HitPoint;

		public readonly ValueOutput<float2> UV;

		public readonly ValueOutput<bool> IsValidUV;

		protected override void ComputeOutputs(FrooxEngineContext context)
		{
			MeshCollider meshCollider = 0.ReadObject<ICollider>(context) as MeshCollider;
			MeshX meshX = meshCollider?.Mesh.Asset?.Data;
			int num = 1.ReadValue<int>(context);
			if (meshX != null && num >= 0 && num < meshX.TotalTriangleCount)
			{
				Triangle triangleFromAllTriangleSubmeshes = meshX.GetTriangleFromAllTriangleSubmeshes(num);
				float2 value = triangleFromAllTriangleSubmeshes.InterpolateUV0(triangleFromAllTriangleSubmeshes.GetBarycentricCoordinate(meshCollider.Slot.GlobalPointToLocal(2.ReadValue<float3>(context))));
				UV.Write(value, context);
				IsValidUV.Write(value: true, context);
			}
			else
			{
				UV.Write(float2.Zero, context);
				IsValidUV.Write(value: false, context);
			}
		}

		public HitUVCoordinate()
		{
			UV = new ValueOutput<float2>(this);
			IsValidUV = new ValueOutput<bool>(this);
		}
	}
	[NodeCategory("Physics")]
	public class RaycastOne : ActionNode<FrooxEngineContext>
	{
		public ValueInput<float3> Origin;

		public ValueInput<float3> Direction;

		[ProtoFlux.Core.DefaultValue(float.MaxValue)]
		public ValueInput<float> MaxDistance;

		public ValueInput<bool> HitTriggers;

		public ValueInput<bool> UsersOnly;

		[ProtoFlux.Core.DefaultValue(-1f)]
		public ValueInput<float> DebugDuration;

		public ObjectInput<Slot> Root;

		public Continuation OnHit;

		public Continuation OnMiss;

		public readonly ObjectOutput<ICollider> HitCollider;

		public readonly ValueOutput<float> HitDistance;

		public readonly ValueOutput<float3> HitPoint;

		public readonly ValueOutput<float3> HitNormal;

		public readonly ValueOutput<int> HitTriangleIndex;

		private static Predicate<ICollider> _userFilter = UserFilter;

		private static bool UserFilter(ICollider c)
		{
			return c.Slot.ActiveUserRoot != null;
		}

		protected override IOperation Run(FrooxEngineContext context)
		{
			Slot slot = Root.Evaluate(context);
			float3 localPoint = Origin.Evaluate(context);
			float3 localDirection = Direction.Evaluate(context);
			float num = MaxDistance.Evaluate(context, float.MaxValue);
			if (slot != null)
			{
				localPoint = slot.LocalPointToGlobal(in localPoint);
				localDirection = slot.LocalDirectionToGlobal(in localDirection);
				num = slot.LocalScaleToGlobal(num);
			}
			if (num <= 0f || localDirection.SqrMagnitude == 0f || !localPoint.IsValid() || !localDirection.IsValid() || float.IsNaN(num))
			{
				return null;
			}
			bool hitTriggers = HitTriggers.Evaluate(context, defaultValue: false);
			bool flag = UsersOnly.Evaluate(context, defaultValue: false);
			float num2 = DebugDuration.Evaluate(context, -1f);
			if (!PhysicsManager.IsValidRaycast(in localPoint, in localDirection, num))
			{
				return OnMiss.Target;
			}
			RaycastHit? raycastHit = context.World.Physics.RaycastOne(in localPoint, in localDirection, num, flag ? _userFilter : null, hitTriggers, (num2 < 0f) ? ((float?)null) : new float?(num2));
			if (raycastHit.HasValue)
			{
				HitCollider.Write(raycastHit.Value.Collider, context);
				HitDistance.Write(raycastHit.Value.Distance, context);
				HitPoint.Write(raycastHit.Value.Point, context);
				HitNormal.Write(raycastHit.Value.Normal, context);
				HitTriangleIndex.Write(raycastHit.Value.TriangleIndex, context);
				return OnHit.Target;
			}
			return OnMiss.Target;
		}

		public RaycastOne()
		{
			HitCollider = new ObjectOutput<ICollider>(this);
			HitDistance = new ValueOutput<float>(this);
			HitPoint = new ValueOutput<float3>(this);
			HitNormal = new ValueOutput<float3>(this);
			HitTriangleIndex = new ValueOutput<int>(this);
		}
	}
	[NodeCategory("Physics")]
	public class Raycaster : VoidNode<FrooxEngineContext>
	{
		public ValueArgument<float3> Origin;

		public ValueArgument<float3> Direction;

		[ProtoFlux.Core.DefaultValue(float.MaxValue)]
		public ValueArgument<float> MaxDistance;

		public ValueArgument<bool> HitTriggers;

		public ValueArgument<bool> UsersOnly;

		public ObjectArgument<Slot> Root;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> HasHit;

		[ContinuouslyChanging]
		public readonly ObjectOutput<ICollider> HitCollider;

		[ContinuouslyChanging]
		public readonly ValueOutput<float> HitDistance;

		[ContinuouslyChanging]
		public readonly ValueOutput<float3> HitPoint;

		[ContinuouslyChanging]
		public readonly ValueOutput<float3> HitNormal;

		[ContinuouslyChanging]
		public readonly ValueOutput<int> HitTriangleIndex;

		private static Predicate<ICollider> _userFilter = UserFilter;

		private static bool UserFilter(ICollider c)
		{
			return c.Slot.ActiveUserRoot != null;
		}

		protected override void ComputeOutputs(FrooxEngineContext context)
		{
			Slot slot = 5.ReadObject<Slot>(context);
			float3 localPoint = 0.ReadValue<float3>(context);
			float3 localDirection = 1.ReadValue<float3>(context);
			float num = 2.ReadValue<float>(context);
			if (slot != null)
			{
				localPoint = slot.LocalPointToGlobal(in localPoint);
				localDirection = slot.LocalDirectionToGlobal(in localDirection);
				num = slot.LocalScaleToGlobal(num);
			}
			if (num <= 0f || localDirection.SqrMagnitude == 0f || !localPoint.IsValid() || !localDirection.IsValid() || float.IsNaN(num))
			{
				HasHit.Write(value: false, context);
				return;
			}
			bool hitTriggers = 3.ReadValue<bool>(context);
			bool flag = 4.ReadValue<bool>(context);
			RaycastHit? raycastHit = context.World.Physics.RaycastOne(in localPoint, in localDirection, num, flag ? _userFilter : null, hitTriggers);
			if (raycastHit.HasValue)
			{
				HasHit.Write(value: true, context);
				HitCollider.Write(raycastHit.Value.Collider, context);
				HitDistance.Write(raycastHit.Value.Distance, context);
				HitPoint.Write(raycastHit.Value.Point, context);
				HitNormal.Write(raycastHit.Value.Normal, context);
				HitTriangleIndex.Write(raycastHit.Value.TriangleIndex, context);
			}
			else
			{
				HasHit.Write(value: false, context);
				HitCollider.Write(null, context);
				HitDistance.Write(0f, context);
				HitPoint.Write(default(float3), context);
				HitNormal.Write(default(float3), context);
				HitTriangleIndex.Write(0, context);
			}
		}

		public Raycaster()
		{
			HasHit = new ValueOutput<bool>(this);
			HitCollider = new ObjectOutput<ICollider>(this);
			HitDistance = new ValueOutput<float>(this);
			HitPoint = new ValueOutput<float3>(this);
			HitNormal = new ValueOutput<float3>(this);
			HitTriangleIndex = new ValueOutput<int>(this);
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Operators
{
	[NodeCategory("Math")]
	[ContinuouslyChanging]
	[NodeOverload("Engine.Operators.Delta")]
	public abstract class DeltaBase<T> : ValueFunctionUpdateBase<T> where T : unmanaged
	{
		public ValueInput<T> Value;

		private ValueStore<bool> _initialized;

		private ValueStore<T> _delta;

		private ValueStore<T> _previous;

		protected override T Compute(FrooxEngineContext context)
		{
			return _delta.Read(context);
		}

		protected override void RunUpdate(FrooxEngineContext context)
		{
			T current = Value.Evaluate(context);
			if (_initialized.Read(context))
			{
				ref T reference = ref _delta.Access(context);
				ref T reference2 = ref _previous.Access(context);
				reference = Delta(ref current, ref reference2);
				reference2 = current;
			}
			else
			{
				_initialized.Write(value: true, context);
				_delta.Write(default(T), context);
				_previous.Write(current, context);
			}
		}

		protected abstract T Delta(ref T current, ref T previous);
	}
	public class ValueDelta<T> : DeltaBase<T> where T : unmanaged
	{
		public static bool IsValidGenericType => Coder<T>.SupportsAddSub;

		protected override T Delta(ref T current, ref T previous)
		{
			return Coder<T>.Sub(current, previous);
		}
	}
	[NodeName("Delta", false)]
	public class Delta_floatQ : DeltaBase<floatQ>
	{
		protected override floatQ Delta(ref floatQ current, ref floatQ previous)
		{
			return floatQ.FromToRotation(previous, current);
		}
	}
	[NodeName("Delta", false)]
	public class Delta_doubleQ : DeltaBase<doubleQ>
	{
		protected override doubleQ Delta(ref doubleQ current, ref doubleQ previous)
		{
			return doubleQ.FromToRotation(previous, current);
		}
	}
	[ContinuouslyChanging]
	[NodeCategory("Time")]
	[NodeOverload("Engine.Operators.Mul_dT")]
	[NodeName("*dT", false)]
	public class MulDeltaTime<T> : ValueFunctionNode<FrooxEngineContext, T> where T : unmanaged
	{
		public ValueArgument<T> A;

		public static bool IsValidGenericType => Coder<T>.SupportsMul;

		protected override T Compute(FrooxEngineContext context)
		{
			return Coder<T>.Scale(0.ReadValue<T>(context), context.World.Time.Delta);
		}
	}
	[ContinuouslyChanging]
	[NodeCategory("Time")]
	[NodeName("÷dT", false)]
	[NodeOverload("Engine.Operators.Div_dT")]
	public class DivDeltaTime<T> : ValueFunctionNode<FrooxEngineContext, T> where T : unmanaged
	{
		public ValueArgument<T> A;

		public static bool IsValidGenericType => Coder<T>.SupportsMul;

		protected override T Compute(FrooxEngineContext context)
		{
			return Coder<T>.Scale(0.ReadValue<T>(context), context.World.Time.InvertedDelta);
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Network
{
	[NodeCategory("Network")]
	[NodeName("Is Host Access Allowed", false)]
	[NodeOverload("FrooxEngine.Network.IsHostAccessAllowed")]
	public class IsHostAccessAllowed : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<string> Host;

		[ProtoFlux.Core.DefaultValue(80)]
		public ValueArgument<int> Port;

		[ProtoFlux.Core.DefaultValue(HostAccessScope.Everything)]
		public ValueArgument<HostAccessScope> Scope;

		protected override bool Compute(FrooxEngineContext context)
		{
			return context.Engine.Security.CanAccess(0.ReadObject<string>(context), 1.ReadValue<int>(context), 2.ReadValue<HostAccessScope>(context)) == true;
		}
	}
	[NodeCategory("Network")]
	[NodeName("Is Host Access Allowed", false)]
	[NodeOverload("FrooxEngine.Network.IsHostAccessAllowed")]
	public class IsHostAccessAllowedUrl : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<Uri> Host;

		[ProtoFlux.Core.DefaultValue(HostAccessScope.Everything)]
		public ValueArgument<HostAccessScope> Scope;

		protected override bool Compute(FrooxEngineContext context)
		{
			Uri uri = 0.ReadObject<Uri>(context);
			if (uri == null)
			{
				return false;
			}
			return context.Engine.Security.CanAccess(uri.Host, uri.Port, 1.ReadValue<HostAccessScope>(context)) == true;
		}
	}
	[NodeName("Request Host Access", false)]
	[NodeCategory("Network")]
	[NodeOverload("FrooxEngine.Network.RequestHostAccess")]
	public abstract class RequestHostAccessBase : AsyncActionNode<FrooxEngineContext>
	{
		public Continuation OnGranted;

		public Continuation OnDenied;

		public Continuation OnIgnored;

		protected override async Task<IOperation> RunAsync(FrooxEngineContext context)
		{
			return await Request(context) switch
			{
				HostAccessPermission.Allowed => OnGranted.Target, 
				HostAccessPermission.Denied => OnDenied.Target, 
				HostAccessPermission.Ignored => OnIgnored.Target, 
				_ => null, 
			};
		}

		protected abstract Task<HostAccessPermission?> Request(FrooxEngineContext context);
	}
	public class RequestHostAccess : RequestHostAccessBase
	{
		public ObjectInput<string> Host;

		[ProtoFlux.Core.DefaultValue(80)]
		public ValueInput<int> Port;

		[ProtoFlux.Core.DefaultValue(HostAccessScope.Everything)]
		public ValueInput<HostAccessScope> Scope;

		public ObjectInput<string> Reason;

		protected override async Task<HostAccessPermission?> Request(FrooxEngineContext context)
		{
			string text = Host.Evaluate(context);
			if (string.IsNullOrWhiteSpace(text))
			{
				return null;
			}
			return await context.Engine.Security.RequestAccessPermission(text, Port.Evaluate(context, 80), Scope.Evaluate(context, HostAccessScope.Everything), Reason.Evaluate(context));
		}
	}
	public class RequestHostAccessUrl : RequestHostAccessBase
	{
		public ObjectInput<Uri> Host;

		public ObjectInput<string> Reason;

		[ProtoFlux.Core.DefaultValue(HostAccessScope.Everything)]
		public ValueInput<HostAccessScope> Scope;

		protected override async Task<HostAccessPermission?> Request(FrooxEngineContext context)
		{
			Uri uri = Host.Evaluate(context);
			if (uri == null)
			{
				return null;
			}
			return await context.Engine.Security.RequestAccessPermission(uri.Host, uri.Port, Scope.Evaluate(context, HostAccessScope.Everything), Reason.Evaluate(context));
		}
	}
	[NodeCategory("Network")]
	public abstract class WebRequestBase : AsyncActionNode<FrooxEngineContext>
	{
		public ObjectInput<Uri> URL;

		public readonly ValueOutput<HttpStatusCode> StatusCode;

		public AsyncCall OnSent;

		public Continuation OnResponse;

		public Continuation OnError;

		public Continuation OnDenied;

		protected override async Task<IOperation> RunAsync(FrooxEngineContext context)
		{
			Uri url = URL.Evaluate(context);
			if (url == null)
			{
				return null;
			}
			if (url.Scheme != "http" && url.Scheme != "https" && url.Scheme != "ftp")
			{
				return null;
			}
			switch (await context.Engine.Security.RequestAccessPermission(url.Host, url.Port, HostAccessScope.HTTP, "Web Request Node"))
			{
			case HostAccessPermission.Denied:
				return OnDenied.Target;
			default:
				return null;
			case HostAccessPermission.Allowed:
				try
				{
					using HttpRequestMessage request = CreateRequest(context, url);
					Task<HttpResponseMessage> responseTask = context.Cloud.SafeHttpClient.SendAsync(request);
					await OnSent.ExecuteAsync(context);
					using HttpResponseMessage response = await responseTask;
					StatusCode.Write(response.StatusCode, context);
					await ProcessResponse(context, response);
					return OnResponse.Target;
				}
				catch (HttpRequestException exception)
				{
					await ProcessError(context, exception);
					StatusCode.Write((HttpStatusCode)0, context);
					return OnError.Target;
				}
			}
		}

		protected abstract HttpRequestMessage CreateRequest(FrooxEngineContext context, Uri url);

		protected abstract ValueTask ProcessResponse(FrooxEngineContext context, HttpResponseMessage response);

		protected abstract ValueTask ProcessError(FrooxEngineContext context, HttpRequestException exception);

		protected WebRequestBase()
		{
			StatusCode = new ValueOutput<HttpStatusCode>(this);
		}
	}
	public abstract class StringResponseWebRequest : WebRequestBase
	{
		public readonly ObjectOutput<string> Content;

		protected override async ValueTask ProcessResponse(FrooxEngineContext context, HttpResponseMessage response)
		{
			ObjectOutput<string> content = Content;
			content.Write(await response.Content.ReadAsStringAsync(), context);
		}

		protected override async ValueTask ProcessError(FrooxEngineContext context, HttpRequestException exception)
		{
			Content.Write(exception.Message, context);
		}

		protected StringResponseWebRequest()
		{
			Content = new ObjectOutput<string>(this);
		}
	}
	[NodeName("GET String", false)]
	public class GET_String : StringResponseWebRequest
	{
		protected override HttpRequestMessage CreateRequest(FrooxEngineContext context, Uri url)
		{
			return new HttpRequestMessage(HttpMethod.Get, url);
		}
	}
	[NodeName("POST String", false)]
	public class POST_String : StringResponseWebRequest
	{
		public ObjectInput<string> String;

		[ProtoFlux.Core.DefaultValue("application/json")]
		public ObjectInput<string> MediaType;

		protected override HttpRequestMessage CreateRequest(FrooxEngineContext context, Uri url)
		{
			return new HttpRequestMessage(HttpMethod.Post, url)
			{
				Content = new System.Net.Http.StringContent(String.Evaluate(context) ?? "", mediaType: MediaType.Evaluate(context, "application/json"), encoding: Encoding.UTF8)
			};
		}
	}
	[NodeCategory("Network/Websockets")]
	public class WebsocketConnect : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<WebsocketClient> Client;

		public ObjectInput<Uri> URL;

		public ObjectInput<global::FrooxEngine.User> HandlingUser;

		protected override bool Do(FrooxEngineContext context)
		{
			WebsocketClient websocketClient = Client.Evaluate(context);
			if (websocketClient == null)
			{
				return false;
			}
			Uri uri = URL.Evaluate(context);
			if (uri != null)
			{
				websocketClient.URL.Value = uri;
			}
			websocketClient.HandlingUser.Target = HandlingUser.Evaluate(context, context.LocalUser);
			return true;
		}
	}
	[NodeCategory("Network/Websockets")]
	public class WebsocketTextMessageSender : AsyncActionNode<FrooxEngineContext>
	{
		public ObjectInput<WebsocketClient> Client;

		public ObjectInput<string> Data;

		public AsyncCall OnSendStart;

		public Continuation OnSent;

		public Continuation OnSendError;

		protected override async Task<IOperation> RunAsync(FrooxEngineContext context)
		{
			WebsocketClient websocketClient = Client.Evaluate(context);
			if (websocketClient == null)
			{
				return null;
			}
			string data = Data.Evaluate(context);
			Task<bool> sendTask = websocketClient.Send(data);
			await OnSendStart.ExecuteAsync(context);
			if (await sendTask)
			{
				return OnSent.Target;
			}
			return OnSendError.Target;
		}
	}
	[NodeCategory("Network/Websockets")]
	public abstract class WebsocketEvents : VoidNode<FrooxEngineContext>
	{
		public readonly GlobalRef<WebsocketClient> Client;

		private ObjectStore<WebsocketClient> _current;

		private void OnClientChanged(WebsocketClient client, FrooxEngineContext context)
		{
			WebsocketClient websocketClient = _current.Read(context);
			if (client != websocketClient)
			{
				if (websocketClient != null)
				{
					Unregister(websocketClient, context);
				}
				if (client != null)
				{
					NodeContextPath path = context.CaptureContextPath();
					context.GetEventDispatcher(out ExecutionEventDispatcher<FrooxEngineContext> eventDispatcher);
					Register(client, path, eventDispatcher, context);
					_current.Write(client, context);
				}
				else
				{
					_current.Clear(context);
					Clear(context);
				}
			}
		}

		protected abstract void Register(WebsocketClient client, NodeContextPath path, ExecutionEventDispatcher<FrooxEngineContext> dispatcher, FrooxEngineContext context);

		protected abstract void Unregister(WebsocketClient client, FrooxEngineContext context);

		protected abstract void Clear(FrooxEngineContext context);

		protected WebsocketEvents()
		{
			Client = new GlobalRef<WebsocketClient>(this, 0);
		}
	}
	public class WebsocketConnectionEvents : WebsocketEvents
	{
		public Call OnConnected;

		public Call OnDisconnected;

		private ObjectStore<Action<WebsocketClient>> _connected;

		private ObjectStore<Action<WebsocketClient>> _disconnected;

		protected override void Register(WebsocketClient client, NodeContextPath path, ExecutionEventDispatcher<FrooxEngineContext> dispatcher, FrooxEngineContext context)
		{
			Action<WebsocketClient> value = delegate
			{
				dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
				{
					Connected(c);
				});
			};
			Action<WebsocketClient> value2 = delegate
			{
				dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
				{
					Disconnected(c);
				});
			};
			client.Connected += value;
			client.Closed += value2;
			_connected.Write(value, context);
			_disconnected.Write(value2, context);
		}

		protected override void Unregister(WebsocketClient client, FrooxEngineContext context)
		{
			client.Connected -= _connected.Read(context);
			client.Closed -= _disconnected.Read(context);
		}

		protected override void Clear(FrooxEngineContext context)
		{
			_connected.Clear(context);
			_disconnected.Clear(context);
		}

		private void Connected(FrooxEngineContext context)
		{
			OnConnected.Execute(context);
		}

		private void Disconnected(FrooxEngineContext context)
		{
			OnDisconnected.Execute(context);
		}
	}
	public class WebsocketTextMessageReceiver : WebsocketEvents
	{
		public Call OnReceived;

		public readonly ObjectOutput<string> Data;

		private ObjectStore<Action<WebsocketClient, string>> _handler;

		private NodeEventHandler<FrooxEngineContext> _callback;

		public override bool CanBeEvaluated => false;

		protected override void Register(WebsocketClient client, NodeContextPath path, ExecutionEventDispatcher<FrooxEngineContext> dispatcher, FrooxEngineContext context)
		{
			if (_callback == null)
			{
				_callback = Receive;
			}
			Action<WebsocketClient, string> value = delegate(WebsocketClient w, string s)
			{
				dispatcher.ScheduleEvent(path, _callback, s);
			};
			client.TextMessageReceived += value;
			_handler.Write(value, context);
		}

		protected override void Unregister(WebsocketClient client, FrooxEngineContext context)
		{
			client.TextMessageReceived -= _handler.Read(context);
		}

		protected override void Clear(FrooxEngineContext context)
		{
			_handler.Clear(context);
		}

		private void Receive(FrooxEngineContext context, object data)
		{
			Data.Write(data as string, context);
			OnReceived.Execute(context);
		}

		public WebsocketTextMessageReceiver()
		{
			Data = new ObjectOutput<string>(this);
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Interaction
{
	[NodeCategory("Interaction")]
	public class ButtonEvents : VoidNode<FrooxEngineContext>
	{
		public readonly GlobalRef<IButton> Button;

		public Call Pressed;

		public Call Pressing;

		public Call Released;

		public Call HoverEnter;

		public Call HoverStay;

		public Call HoverLeave;

		public readonly ObjectOutput<global::FrooxEngine.Component> Source;

		public readonly ValueOutput<float3> GlobalPoint;

		public readonly ValueOutput<float2> LocalPoint;

		public readonly ValueOutput<float2> NormalizedPoint;

		private ObjectStore<IButton> _currentButton;

		private ObjectStore<ButtonEventHandler> _pressed;

		private ObjectStore<ButtonEventHandler> _pressing;

		private ObjectStore<ButtonEventHandler> _released;

		private ObjectStore<ButtonEventHandler> _hoverEnter;

		private ObjectStore<ButtonEventHandler> _hoverStay;

		private ObjectStore<ButtonEventHandler> _hoverLeave;

		public override bool CanBeEvaluated => false;

		private void OnButtonChanged(IButton button, FrooxEngineContext context)
		{
			IButton button2 = _currentButton.Read(context);
			if (button == button2)
			{
				return;
			}
			if (button2 != null)
			{
				button2.LocalPressed -= _pressed.Read(context);
				button2.LocalPressing -= _pressing.Read(context);
				button2.LocalReleased -= _released.Read(context);
				button2.LocalHoverEnter -= _hoverEnter.Read(context);
				button2.LocalHoverStay -= _hoverStay.Read(context);
				button2.LocalHoverLeave -= _hoverLeave.Read(context);
			}
			if (button != null)
			{
				NodeContextPath path = context.CaptureContextPath();
				context.GetEventDispatcher(out var dispatcher);
				ButtonEventHandler value = delegate(IButton b, ButtonEventData e)
				{
					dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
					{
						OnPressed(b, in e, c);
					});
				};
				ButtonEventHandler value2 = delegate(IButton b, ButtonEventData e)
				{
					dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
					{
						OnPressing(b, in e, c);
					});
				};
				ButtonEventHandler value3 = delegate(IButton b, ButtonEventData e)
				{
					dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
					{
						OnRelased(b, in e, c);
					});
				};
				ButtonEventHandler value4 = delegate(IButton b, ButtonEventData e)
				{
					dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
					{
						OnHoverEnter(b, in e, c);
					});
				};
				ButtonEventHandler value5 = delegate(IButton b, ButtonEventData e)
				{
					dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
					{
						OnHoverStay(b, in e, c);
					});
				};
				ButtonEventHandler value6 = delegate(IButton b, ButtonEventData e)
				{
					dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
					{
						OnHoverLeave(b, in e, c);
					});
				};
				_currentButton.Write(button, context);
				_pressed.Write(value, context);
				_pressing.Write(value2, context);
				_released.Write(value3, context);
				_hoverEnter.Write(value4, context);
				_hoverStay.Write(value5, context);
				_hoverLeave.Write(value6, context);
				button.LocalPressed += value;
				button.LocalPressing += value2;
				button.LocalReleased += value3;
				button.LocalHoverEnter += value4;
				button.LocalHoverStay += value5;
				button.LocalHoverLeave += value6;
			}
			else
			{
				_currentButton.Clear(context);
				_pressed.Clear(context);
				_pressing.Clear(context);
				_released.Clear(context);
				_hoverEnter.Clear(context);
				_hoverStay.Clear(context);
				_hoverLeave.Clear(context);
			}
		}

		private void WriteEventData(in ButtonEventData eventData, FrooxEngineContext context)
		{
			Source.Write(eventData.source, context);
			GlobalPoint.Write(eventData.globalPoint, context);
			LocalPoint.Write(eventData.localPoint, context);
			NormalizedPoint.Write(eventData.normalizedPressPoint, context);
		}

		private void OnPressed(IButton button, in ButtonEventData eventData, FrooxEngineContext context)
		{
			WriteEventData(in eventData, context);
			Pressed.Execute(context);
		}

		private void OnPressing(IButton button, in ButtonEventData eventData, FrooxEngineContext context)
		{
			WriteEventData(in eventData, context);
			Pressing.Execute(context);
		}

		private void OnRelased(IButton button, in ButtonEventData eventData, FrooxEngineContext context)
		{
			WriteEventData(in eventData, context);
			Released.Execute(context);
		}

		private void OnHoverEnter(IButton button, in ButtonEventData eventData, FrooxEngineContext context)
		{
			WriteEventData(in eventData, context);
			HoverEnter.Execute(context);
		}

		private void OnHoverStay(IButton button, in ButtonEventData eventData, FrooxEngineContext context)
		{
			WriteEventData(in eventData, context);
			HoverStay.Execute(context);
		}

		private void OnHoverLeave(IButton button, in ButtonEventData eventData, FrooxEngineContext context)
		{
			WriteEventData(in eventData, context);
			HoverLeave.Execute(context);
		}

		public ButtonEvents()
		{
			Button = new GlobalRef<IButton>(this, 0);
			Source = new ObjectOutput<global::FrooxEngine.Component>(this);
			GlobalPoint = new ValueOutput<float3>(this);
			LocalPoint = new ValueOutput<float2>(this);
			NormalizedPoint = new ValueOutput<float2>(this);
		}
	}
	[NodeCategory("Interaction")]
	public class CloseContextMenu : ActionFlowNode<FrooxEngineContext>
	{
		public ObjectInput<IWorldElement> Summoner;

		protected override void Do(FrooxEngineContext context)
		{
			context.World.LocalUser.CloseContextMenu(Summoner.Evaluate(context));
		}
	}
	[NodeCategory("Interaction")]
	[ContinuouslyChanging]
	public class IsContextMenuOpen : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.IsContextMenuOpen() ?? false;
		}
	}
	[NodeCategory("Interaction/Grabbable")]
	public class ReleaseAllGrabbed : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ValueInput<BodyNode> Node;

		public ValueInput<bool> SupressEvents;

		protected override bool Do(FrooxEngineContext context)
		{
			UserRoot localUserRoot = context.LocalUser.LocalUserRoot;
			if (localUserRoot == null)
			{
				return false;
			}
			BodyNode node = Node.Evaluate(context, BodyNode.NONE);
			Grabber registeredComponent = localUserRoot.GetRegisteredComponent((Grabber g) => (BodyNode)g.CorrespondingBodyNode == node);
			if (registeredComponent == null)
			{
				return false;
			}
			registeredComponent.Release(SupressEvents.Evaluate(context, defaultValue: false));
			return true;
		}
	}
	[NodeCategory("Interaction/Grabbable")]
	public abstract class GrabbableEvents : VoidNode<FrooxEngineContext>
	{
		public readonly GlobalRef<IGrabbable> Grabbable;

		private ObjectStore<IGrabbable> _current;

		private ObjectStore<Action<IGrabbable>> _handler;

		private void OnGrabbableChanged(IGrabbable grabbable, FrooxEngineContext context)
		{
			IGrabbable grabbable2 = _current.Read(context);
			if (grabbable2 == grabbable)
			{
				return;
			}
			if (grabbable2 != null)
			{
				Unregister(grabbable2, _handler.Read(context));
			}
			if (grabbable != null)
			{
				NodeContextPath path = context.CaptureContextPath();
				context.GetEventDispatcher(out var dispatcher);
				Action<IGrabbable> action = delegate(IGrabbable g)
				{
					dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
					{
						Handle(g, c);
					});
				};
				Register(grabbable, action);
				_current.Write(grabbable, context);
				_handler.Write(action, context);
			}
			else
			{
				_current.Clear(context);
				_handler.Clear(context);
			}
		}

		protected abstract void Register(IGrabbable grabbable, Action<IGrabbable> handler);

		protected abstract void Unregister(IGrabbable grabbable, Action<IGrabbable> handler);

		protected abstract void Handle(IGrabbable grabbable, FrooxEngineContext context);

		protected GrabbableEvents()
		{
			Grabbable = new GlobalRef<IGrabbable>(this, 0);
		}
	}
	public class OnGrabbableGrabbed : GrabbableEvents
	{
		public Call OnGrabbed;

		protected override void Register(IGrabbable grabbable, Action<IGrabbable> handler)
		{
			grabbable.OnLocalGrabbed += handler;
		}

		protected override void Unregister(IGrabbable grabbable, Action<IGrabbable> handler)
		{
			grabbable.OnLocalGrabbed -= handler;
		}

		protected override void Handle(IGrabbable grabbable, FrooxEngineContext context)
		{
			OnGrabbed.Execute(context);
		}
	}
	public class OnGrabbableReleased : GrabbableEvents
	{
		public Call OnReleased;

		protected override void Register(IGrabbable grabbable, Action<IGrabbable> handler)
		{
			grabbable.OnLocalReleased += handler;
		}

		protected override void Unregister(IGrabbable grabbable, Action<IGrabbable> handler)
		{
			grabbable.OnLocalReleased -= handler;
		}

		protected override void Handle(IGrabbable grabbable, FrooxEngineContext context)
		{
			OnReleased.Execute(context);
		}
	}
	[NodeCategory("Interaction/Grabbable")]
	[ContinuouslyChanging]
	public class CanBeGrabbed : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<Slot> Slot;

		public ObjectArgument<Grabber> Grabber;

		protected override bool Compute(FrooxEngineContext context)
		{
			Slot slot = 0.ReadObject<Slot>(context);
			Grabber grabber = 1.ReadObject<Grabber>(context);
			if (slot == null || grabber == null)
			{
				return false;
			}
			return grabber.FindGrabbableInParents(slot, null)?.CanGrab(grabber) ?? false;
		}
	}
	[NodeCategory("Interaction/Grabbable")]
	[ContinuouslyChanging]
	public abstract class GrabbableValuePropertyNode<T> : ValueFunctionNode<FrooxEngineContext, T> where T : unmanaged
	{
		public ObjectArgument<IGrabbable> Grabbable;

		protected override T Compute(FrooxEngineContext context)
		{
			return Get(0.ReadObject<IGrabbable>(context));
		}

		protected abstract T Get(IGrabbable grabbable);
	}
	[NodeCategory("Interaction/Grabbable")]
	[ContinuouslyChanging]
	public abstract class GrabbableObjectPropertyNode<T> : ObjectFunctionNode<FrooxEngineContext, T>
	{
		public ObjectArgument<IGrabbable> Grabbable;

		protected override T Compute(FrooxEngineContext context)
		{
			return Get(0.ReadObject<IGrabbable>(context));
		}

		protected abstract T Get(IGrabbable grabbable);
	}
	public class IsGrabbableGrabbed : GrabbableValuePropertyNode<bool>
	{
		protected override bool Get(IGrabbable grabbable)
		{
			return grabbable?.IsGrabbed ?? false;
		}
	}
	public class IsGrabbableScalable : GrabbableValuePropertyNode<bool>
	{
		protected override bool Get(IGrabbable grabbable)
		{
			return grabbable?.Scalable ?? false;
		}
	}
	public class IsGrabbableReceivable : GrabbableValuePropertyNode<bool>
	{
		protected override bool Get(IGrabbable grabbable)
		{
			return grabbable?.Receivable ?? false;
		}
	}
	public class GrabbablePriority : GrabbableValuePropertyNode<int>
	{
		protected override int Get(IGrabbable grabbable)
		{
			return grabbable?.GrabPriority ?? 0;
		}
	}
	public class GrabbableGrabber : GrabbableObjectPropertyNode<Grabber>
	{
		protected override Grabber Get(IGrabbable grabbable)
		{
			return grabbable?.Grabber;
		}
	}
	[NodeCategory("Interaction/Grabbable")]
	public class OnGrabbableReceiverSurfaceReceived : VoidNode<FrooxEngineContext>
	{
		public readonly GlobalRef<GrabbableReceiverSurface> Source;

		public Call OnReceived;

		public readonly ObjectOutput<IGrabbable> ReceivedGrabbable;

		public readonly ObjectOutput<Grabber> FromGrabber;

		private ObjectStore<GrabbableReceiverSurface> _current;

		private ObjectStore<Action<IGrabbable, Grabber>> _handler;

		public override bool CanBeEvaluated => false;

		private void OnSourceChanged(GrabbableReceiverSurface surface, FrooxEngineContext context)
		{
			GrabbableReceiverSurface grabbableReceiverSurface = _current.Read(context);
			if (surface == grabbableReceiverSurface)
			{
				return;
			}
			if (grabbableReceiverSurface != null)
			{
				grabbableReceiverSurface.OnLocalReceived -= _handler.Read(context);
			}
			if (surface != null)
			{
				NodeContextPath path = context.CaptureContextPath();
				context.GetEventDispatcher(out var dispatcher);
				Action<IGrabbable, Grabber> value = delegate(IGrabbable grabbable, Grabber grabber)
				{
					dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
					{
						Received(grabbable, grabber, c);
					});
				};
				surface.OnLocalReceived += value;
				_current.Write(surface, context);
				_handler.Write(value, context);
			}
			else
			{
				_current.Clear(context);
				_handler.Clear(context);
			}
		}

		private void Received(IGrabbable grabbable, Grabber grabber, FrooxEngineContext context)
		{
			ReceivedGrabbable.Write(grabbable, context);
			FromGrabber.Write(grabber, context);
			OnReceived.Execute(context);
		}

		public OnGrabbableReceiverSurfaceReceived()
		{
			Source = new GlobalRef<GrabbableReceiverSurface>(this, 0);
			ReceivedGrabbable = new ObjectOutput<IGrabbable>(this);
			FromGrabber = new ObjectOutput<Grabber>(this);
		}
	}
	[NodeCategory("Interaction/Grabbable")]
	public class GetUserGrabber : ObjectFunctionNode<FrooxEngineContext, Grabber>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		public ValueArgument<BodyNode> Node;

		protected override Grabber Compute(FrooxEngineContext context)
		{
			global::FrooxEngine.User user = 0.ReadObject<global::FrooxEngine.User>(context);
			if (user == null)
			{
				if (User.Source != null)
				{
					return null;
				}
				user = context.LocalUser;
			}
			BodyNode node = 1.ReadValue<BodyNode>(context);
			return user.Root?.GetRegisteredComponent((Grabber g) => g.CorrespondingBodyNode.Value == node);
		}
	}
	[NodeCategory("Interaction/Grabbable")]
	public class GrabberBodyNode : ValueFunctionNode<FrooxEngineContext, BodyNode>
	{
		public ObjectArgument<Grabber> Grabber;

		protected override BodyNode Compute(FrooxEngineContext context)
		{
			Sync<BodyNode> sync = 0.ReadObject<Grabber>(context)?.CorrespondingBodyNode;
			if (sync == null)
			{
				return BodyNode.NONE;
			}
			return sync;
		}
	}
	[NodeCategory("Interaction/UI")]
	public class TextEditorEvents : VoidNode<FrooxEngineContext>
	{
		public readonly GlobalRef<TextEditor> Editor;

		public Call EditingStarted;

		public Call EditingChanged;

		public Call EditingFinished;

		public Call SubmitPressed;

		private ObjectStore<TextEditor> _current;

		private ObjectStore<Action<TextEditor>> _started;

		private ObjectStore<Action<TextEditor>> _changed;

		private ObjectStore<Action<TextEditor>> _finished;

		private ObjectStore<Action<TextEditor>> _submit;

		private void OnEditorChanged(TextEditor editor, FrooxEngineContext context)
		{
			TextEditor textEditor = _current.Read(context);
			if (textEditor == editor)
			{
				return;
			}
			if (textEditor != null)
			{
				textEditor.LocalEditingStarted -= _started.Read(context);
				textEditor.LocalEditingChanged -= _changed.Read(context);
				textEditor.LocalEditingFinished -= _finished.Read(context);
				textEditor.LocalSubmitPressed -= _submit.Read(context);
			}
			if (editor != null)
			{
				NodeContextPath path = context.CaptureContextPath();
				context.GetEventDispatcher(out var dispatcher);
				Action<TextEditor> value = delegate
				{
					dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
					{
						OnStarted(c);
					});
				};
				Action<TextEditor> value2 = delegate
				{
					dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
					{
						OnChanged(c);
					});
				};
				Action<TextEditor> value3 = delegate
				{
					dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
					{
						OnFinished(c);
					});
				};
				Action<TextEditor> value4 = delegate
				{
					dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
					{
						OnSubmit(c);
					});
				};
				editor.LocalEditingStarted += value;
				editor.LocalEditingChanged += value2;
				editor.LocalEditingFinished += value3;
				editor.LocalSubmitPressed += value4;
				_current.Write(editor, context);
				_started.Write(value, context);
				_changed.Write(value2, context);
				_finished.Write(value3, context);
				_submit.Write(value4, context);
			}
			else
			{
				_current.Clear(context);
				_started.Clear(context);
				_changed.Clear(context);
				_finished.Clear(context);
				_submit.Clear(context);
			}
		}

		private void OnStarted(FrooxEngineContext context)
		{
			EditingStarted.Execute(context);
		}

		private void OnChanged(FrooxEngineContext context)
		{
			EditingChanged.Execute(context);
		}

		private void OnFinished(FrooxEngineContext context)
		{
			EditingFinished.Execute(context);
		}

		private void OnSubmit(FrooxEngineContext context)
		{
			SubmitPressed.Execute(context);
		}

		public TextEditorEvents()
		{
			Editor = new GlobalRef<TextEditor>(this, 0);
		}
	}
	[NodeCategory("Interaction")]
	public class TouchableEvents : VoidNode<FrooxEngineContext>
	{
		public readonly GlobalRef<TouchEventRelay> EventSource;

		public Call OnEvent;

		public readonly ValueOutput<EventState> Hover;

		public readonly ValueOutput<EventState> Touch;

		public readonly ValueOutput<float3> Point;

		public readonly ValueOutput<float3> Tip;

		public readonly ValueOutput<TouchType> Type;

		public readonly ObjectOutput<global::FrooxEngine.Component> Source;

		private ObjectStore<TouchEventRelay> _current;

		private ObjectStore<TouchEvent> _handler;

		public override bool CanBeEvaluated => false;

		private void OnEventSourceChanged(TouchEventRelay source, FrooxEngineContext context)
		{
			TouchEventRelay touchEventRelay = _current.Read(context);
			if (source == touchEventRelay)
			{
				return;
			}
			if (touchEventRelay != null)
			{
				touchEventRelay.LocalTouched -= _handler.Read(context);
			}
			if (source != null)
			{
				NodeContextPath path = context.CaptureContextPath();
				context.GetEventDispatcher(out var dispatcher);
				TouchEvent value = delegate(ITouchable t, in TouchEventInfo e)
				{
					TouchEventInfo _e = e;
					dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
					{
						OnTouched(t, in _e, c);
					});
				};
				source.LocalTouched += value;
				_current.Write(source, context);
				_handler.Write(value, context);
			}
			else
			{
				_current.Clear(context);
				_handler.Clear(context);
			}
		}

		private void OnTouched(ITouchable touchable, in TouchEventInfo eventInfo, FrooxEngineContext context)
		{
			Hover.Write(eventInfo.hover, context);
			Touch.Write(eventInfo.touch, context);
			Point.Write(eventInfo.point, context);
			Tip.Write(eventInfo.tip, context);
			Type.Write(eventInfo.type, context);
			Source.Write(eventInfo.source, context);
			OnEvent.Execute(context);
		}

		public TouchableEvents()
		{
			EventSource = new GlobalRef<TouchEventRelay>(this, 0);
			Hover = new ValueOutput<EventState>(this);
			Touch = new ValueOutput<EventState>(this);
			Point = new ValueOutput<float3>(this);
			Tip = new ValueOutput<float3>(this);
			Type = new ValueOutput<TouchType>(this);
			Source = new ObjectOutput<global::FrooxEngine.Component>(this);
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Interaction.Tools
{
	[NodeCategory("Tools")]
	[OldTypeName("ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Interaction.Tooltips.EquipTooltip", null)]
	public class EquipTool : ActionNode<FrooxEngineContext>
	{
		[OldName("Tooltip")]
		public ObjectInput<ITool> Tool;

		public ObjectInput<global::FrooxEngine.User> User;

		public ValueInput<Chirality> Side;

		public ValueInput<bool> DequipExisting;

		public Continuation OnEquipped;

		public Continuation OnEquipFail;

		public static Chirality SideDefault => (Chirality)(-1);

		protected override IOperation Run(FrooxEngineContext context)
		{
			ITool tool = Tool.Evaluate(context);
			if (tool == null || tool.IsEquipped)
			{
				return OnEquipFail.Target;
			}
			global::FrooxEngine.User user = User.Evaluate(context, context.LocalUser);
			if (user == null)
			{
				return OnEquipFail.Target;
			}
			Chirality side = Side.Evaluate(context, SideDefault);
			InteractionHandler interactionHandler = user.GetInteractionHandler(side);
			if (interactionHandler == null)
			{
				return OnEquipFail.Target;
			}
			if (interactionHandler.ActiveTool != null)
			{
				if (!DequipExisting.Evaluate(context, defaultValue: false))
				{
					return OnEquipFail.Target;
				}
				interactionHandler.Dequip(popOff: false);
			}
			if (interactionHandler.Equip(tool, lockEquip: true))
			{
				return OnEquipped.Target;
			}
			return OnEquipFail.Target;
		}
	}
	[NodeCategory("Tools")]
	[OldTypeName("ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Interaction.Tooltips.DequipTooltip", null)]
	public class DequipTool : ActionNode<FrooxEngineContext>
	{
		public ObjectInput<global::FrooxEngine.User> User;

		public ValueInput<Chirality> Side;

		public ValueInput<bool> PopOff;

		public Continuation OnDequipped;

		public Continuation OnDequipFail;

		protected override IOperation Run(FrooxEngineContext context)
		{
			global::FrooxEngine.User user = User.Evaluate(context, context.LocalUser);
			if (user == null)
			{
				return OnDequipFail.Target;
			}
			InteractionHandler interactionHandler = user.GetInteractionHandler(Side.Evaluate(context, Chirality.Left));
			if (interactionHandler == null)
			{
				return OnDequipFail.Target;
			}
			if (interactionHandler.Dequip(PopOff.Evaluate(context, defaultValue: false)))
			{
				return OnDequipped.Target;
			}
			return OnDequipFail.Target;
		}
	}
	[NodeCategory("Tools")]
	[OldTypeName("ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Interaction.Tooltips.TooltipEvents", null)]
	public class ToolEvents : VoidNode<FrooxEngineContext>
	{
		[OldName("Tooltip")]
		public readonly GlobalRef<ITool> Tool;

		public Call Equipped;

		public Call Dequipped;

		private ObjectStore<ITool> _current;

		private ObjectStore<Action> _equipped;

		private ObjectStore<Action> _dequipped;

		private void OnToolChanged(ITool tool, FrooxEngineContext context)
		{
			ITool tool2 = _current.Read(context);
			if (tool2 == tool)
			{
				return;
			}
			if (tool2 != null)
			{
				tool2.LocalEquipped -= _equipped.Read(context);
				tool2.LocalDequipped -= _dequipped.Read(context);
			}
			if (tool != null)
			{
				NodeContextPath path = context.CaptureContextPath();
				context.GetEventDispatcher(out var dispatcher);
				Action value = delegate
				{
					dispatcher.ScheduleEvent(path, HandleEquipped, null);
				};
				Action value2 = delegate
				{
					dispatcher.ScheduleEvent(path, HandleDequipped, null);
				};
				tool.LocalEquipped += value;
				tool.LocalDequipped += value2;
				_current.Write(tool, context);
				_equipped.Write(value, context);
				_dequipped.Write(value2, context);
			}
			else
			{
				_current.Clear(context);
				_equipped.Clear(context);
				_dequipped.Clear(context);
			}
		}

		private void HandleEquipped(FrooxEngineContext context, object args)
		{
			Equipped.Execute(context);
		}

		private void HandleDequipped(FrooxEngineContext context, object args)
		{
			Dequipped.Execute(context);
		}

		public ToolEvents()
		{
			Tool = new GlobalRef<ITool>(this, 0);
		}
	}
	[NodeCategory("Tools")]
	[OldTypeName("ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Interaction.Tooltips.RawDataTooltipEvents", null)]
	public class RawDataToolEvents : VoidNode<FrooxEngineContext>
	{
		[OldName("Tooltip")]
		public readonly GlobalRef<RawDataTool> Tool;

		public Call Equipped;

		public Call Dequipped;

		public Call ToolUpdate;

		public Call PrimaryPressed;

		public Call PrimaryHeld;

		public Call PrimaryReleased;

		public Call SecondaryPressed;

		public Call SecondaryHeld;

		public Call SecondaryReleased;

		private ObjectStore<RawDataTool> _current;

		private ObjectStore<Action> _update;

		private ObjectStore<Action> _equipped;

		private ObjectStore<Action> _dequipped;

		private ObjectStore<Action> _primaryPressed;

		private ObjectStore<Action> _primaryHeld;

		private ObjectStore<Action> _primaryReleased;

		private ObjectStore<Action> _secondaryPressed;

		private ObjectStore<Action> _secondaryHeld;

		private ObjectStore<Action> _secondaryReleased;

		private NodeEventHandler<FrooxEngineContext> _handleUpdate;

		private NodeEventHandler<FrooxEngineContext> _handlePrimaryPressed;

		private NodeEventHandler<FrooxEngineContext> _handlePrimaryHeld;

		private NodeEventHandler<FrooxEngineContext> _handlePrimaryReleased;

		private NodeEventHandler<FrooxEngineContext> _handleSecondaryPressed;

		private NodeEventHandler<FrooxEngineContext> _handleSecondaryHeld;

		private NodeEventHandler<FrooxEngineContext> _handleSecondaryReleased;

		private void OnToolChanged(RawDataTool tool, FrooxEngineContext context)
		{
			RawDataTool rawDataTool = _current.Read(context);
			if (rawDataTool == tool)
			{
				return;
			}
			if (rawDataTool != null)
			{
				rawDataTool.LocalToolUpdate -= _update.Read(context);
				rawDataTool.LocalEquipped -= _equipped.Read(context);
				rawDataTool.LocalDequipped -= _dequipped.Read(context);
				rawDataTool.LocalPrimaryPressed -= _primaryPressed.Read(context);
				rawDataTool.LocalPrimaryHeld -= _primaryHeld.Read(context);
				rawDataTool.LocalPrimaryReleased -= _primaryReleased.Read(context);
				rawDataTool.LocalSecondaryPressed -= _secondaryPressed.Read(context);
				rawDataTool.LocalSecondaryHeld -= _secondaryHeld.Read(context);
				rawDataTool.LocalSecondaryReleased -= _secondaryReleased.Read(context);
			}
			if (tool != null)
			{
				NodeContextPath path = context.CaptureContextPath();
				context.GetEventDispatcher(out var dispatcher);
				if (_handleUpdate == null)
				{
					_handleUpdate = HandleUpdate;
					_handlePrimaryPressed = HandlePrimaryPressed;
					_handlePrimaryHeld = HandlePrimaryHeld;
					_handlePrimaryReleased = HandlePrimaryReleased;
					_handleSecondaryPressed = HandleSecondaryPressed;
					_handleSecondaryHeld = HandleSecondaryHeld;
					_handleSecondaryReleased = HandleSecondaryReleased;
				}
				Action value = delegate
				{
					dispatcher.ScheduleEvent(path, HandleEquipped, null);
				};
				Action value2 = delegate
				{
					dispatcher.ScheduleEvent(path, HandleDequipped, null);
				};
				Action value3 = delegate
				{
					dispatcher.ScheduleEvent(path, _handleUpdate, null);
				};
				Action value4 = delegate
				{
					dispatcher.ScheduleEvent(path, _handlePrimaryPressed, null);
				};
				Action value5 = delegate
				{
					dispatcher.ScheduleEvent(path, _handlePrimaryHeld, null);
				};
				Action value6 = delegate
				{
					dispatcher.ScheduleEvent(path, _handlePrimaryReleased, null);
				};
				Action value7 = delegate
				{
					dispatcher.ScheduleEvent(path, _handleSecondaryPressed, null);
				};
				Action value8 = delegate
				{
					dispatcher.ScheduleEvent(path, _handleSecondaryHeld, null);
				};
				Action value9 = delegate
				{
					dispatcher.ScheduleEvent(path, _handleSecondaryReleased, null);
				};
				tool.LocalEquipped += value;
				tool.LocalDequipped += value2;
				tool.LocalToolUpdate += value3;
				tool.LocalPrimaryPressed += value4;
				tool.LocalPrimaryHeld += value5;
				tool.LocalPrimaryReleased += value6;
				tool.LocalSecondaryPressed += value7;
				tool.LocalSecondaryHeld += value8;
				tool.LocalSecondaryReleased += value9;
				_current.Write(tool, context);
				_equipped.Write(value, context);
				_dequipped.Write(value2, context);
				_update.Write(value3, context);
				_primaryPressed.Write(value4, context);
				_primaryHeld.Write(value5, context);
				_primaryReleased.Write(value6, context);
				_secondaryPressed.Write(value7, context);
				_secondaryHeld.Write(value8, context);
				_secondaryReleased.Write(value9, context);
			}
			else
			{
				_current.Clear(context);
				_update.Clear(context);
				_equipped.Clear(context);
				_dequipped.Clear(context);
				_primaryPressed.Clear(context);
				_primaryHeld.Clear(context);
				_primaryReleased.Clear(context);
				_secondaryPressed.Clear(context);
				_secondaryHeld.Clear(context);
				_secondaryReleased.Clear(context);
			}
		}

		private void HandleEquipped(FrooxEngineContext context, object args)
		{
			Equipped.Execute(context);
		}

		private void HandleDequipped(FrooxEngineContext context, object args)
		{
			Dequipped.Execute(context);
		}

		private void HandleUpdate(FrooxEngineContext context, object args)
		{
			ToolUpdate.Execute(context);
		}

		private void HandlePrimaryPressed(FrooxEngineContext context, object args)
		{
			PrimaryPressed.Execute(context);
		}

		private void HandlePrimaryHeld(FrooxEngineContext context, object args)
		{
			PrimaryHeld.Execute(context);
		}

		private void HandlePrimaryReleased(FrooxEngineContext context, object args)
		{
			PrimaryReleased.Execute(context);
		}

		private void HandleSecondaryPressed(FrooxEngineContext context, object args)
		{
			SecondaryPressed.Execute(context);
		}

		private void HandleSecondaryHeld(FrooxEngineContext context, object args)
		{
			SecondaryHeld.Execute(context);
		}

		private void HandleSecondaryReleased(FrooxEngineContext context, object args)
		{
			SecondaryReleased.Execute(context);
		}

		public RawDataToolEvents()
		{
			Tool = new GlobalRef<RawDataTool>(this, 0);
		}
	}
	[NodeCategory("Tools")]
	[ContinuouslyChanging]
	[OldTypeName("ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Interaction.Tooltips.GetTooltip", null)]
	public class GetTool : ObjectFunctionNode<FrooxEngineContext, ITool>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		public ValueArgument<Chirality> Side;

		public static Chirality SideDefault => (Chirality)(-1);

		protected override ITool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.GetInteractionHandler(1.ReadValue<Chirality>(context))?.ActiveTool;
		}
	}
	[NodeCategory("Tools")]
	[ContinuouslyChanging]
	[OldTypeName("ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Interaction.Tooltips.HasTooltip", null)]
	public class HasTool : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		public ValueArgument<Chirality> Side;

		public static Chirality SideDefault => (Chirality)(-1);

		protected override bool Compute(FrooxEngineContext context)
		{
			global::FrooxEngine.User user = 0.ReadObject<global::FrooxEngine.User>(context);
			if (user == null)
			{
				return false;
			}
			return user.GetInteractionHandler(1.ReadValue<Chirality>(context))?.ActiveTool != null;
		}
	}
	[NodeCategory("Tools")]
	[ContinuouslyChanging]
	[OldTypeName("ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Interaction.Tooltips.IsTooltipEquipped", null)]
	public class IsToolEquipped : ValueFunctionNode<FrooxEngineContext, bool>
	{
		[OldName("Tooltip")]
		public ObjectArgument<ITool> Tool;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<ITool>(context)?.IsEquipped ?? false;
		}
	}
	[NodeCategory("Tools")]
	[ContinuouslyChanging]
	[OldTypeName("ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Interaction.Tooltips.IsTooltipInUse", null)]
	public class IsToolInUse : ValueFunctionNode<FrooxEngineContext, bool>
	{
		[OldName("Tooltip")]
		public ObjectArgument<ITool> Tool;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<ITool>(context)?.IsInUse ?? false;
		}
	}
	[NodeCategory("Tools")]
	[ContinuouslyChanging]
	[OldTypeName("ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Interaction.Tooltips.TooltipEquippingSlot", null)]
	public class ToolEquippingSlot : ObjectFunctionNode<FrooxEngineContext, Slot>
	{
		[OldName("Tooltip")]
		public ObjectArgument<ITool> Tool;

		protected override Slot Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<ITool>(context)?.ActiveHandler?.Slot;
		}
	}
	[NodeCategory("Tools")]
	[ContinuouslyChanging]
	[OldTypeName("ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Interaction.Tooltips.TooltipEquippingSide", null)]
	public class ToolEquippingSide : ValueFunctionNode<FrooxEngineContext, Chirality>
	{
		[OldName("Tooltip")]
		public ObjectArgument<ITool> Tool;

		protected override Chirality Compute(FrooxEngineContext context)
		{
			Sync<Chirality> sync = 0.ReadObject<ITool>(context)?.ActiveHandler?.Side;
			if (sync == null)
			{
				return (Chirality)(-1);
			}
			return sync;
		}
	}
	[NodeCategory("Tools")]
	[ContinuouslyChanging]
	[OldTypeName("ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Interaction.Tooltips.GetRawDataTooltipHit", null)]
	public class GetRawDataToolHit : VoidNode<FrooxEngineContext>
	{
		[OldName("Tooltip")]
		public ObjectArgument<RawDataTool> Tool;

		public readonly ObjectOutput<ICollider> HitCollider;

		public readonly ValueOutput<float3> HitPoint;

		public readonly ValueOutput<float3> HitNormal;

		public readonly ValueOutput<float> HitDistance;

		public readonly ValueOutput<int> HitTriangleIndex;

		protected override void ComputeOutputs(FrooxEngineContext context)
		{
			RaycastHit? raycastHit = 0.ReadObject<RawDataTool>(context)?.GetCurrentHit();
			HitCollider.Write(raycastHit?.Collider, context);
			HitPoint.Write(raycastHit?.Point ?? default(float3), context);
			HitNormal.Write(raycastHit?.Normal ?? default(float3), context);
			HitDistance.Write(raycastHit?.Distance ?? 0f, context);
			HitTriangleIndex.Write(raycastHit?.TriangleIndex ?? (-1), context);
		}

		public GetRawDataToolHit()
		{
			HitCollider = new ObjectOutput<ICollider>(this);
			HitPoint = new ValueOutput<float3>(this);
			HitNormal = new ValueOutput<float3>(this);
			HitDistance = new ValueOutput<float>(this);
			HitTriangleIndex = new ValueOutput<int>(this);
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Interaction.Focusing
{
	[NodeCategory("Interaction/UI")]
	public class FocusFocusable : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<IFocusable> Target;

		protected override bool Do(FrooxEngineContext context)
		{
			IFocusable focusable = Target.Evaluate(context);
			if (focusable == null)
			{
				return false;
			}
			focusable.Focus();
			return true;
		}
	}
	[NodeCategory("Interaction/UI")]
	public class DefocusFocusable : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<IFocusable> Target;

		protected override bool Do(FrooxEngineContext context)
		{
			IFocusable focusable = Target.Evaluate(context);
			if (focusable == null)
			{
				return false;
			}
			focusable.Defocus();
			return true;
		}
	}
	[NodeCategory("Interaction/UI")]
	public class ClearFocus : ActionFlowNode<FrooxEngineContext>
	{
		protected override void Do(FrooxEngineContext context)
		{
			context.World.LocalUser.ClearFocus();
		}
	}
	[NodeCategory("Interaction/UI")]
	[ContinuouslyChanging]
	public class HasLocalFocus : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<IFocusable> Target;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<IFocusable>(context)?.HasLocalFocus() ?? false;
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Input.Mouse
{
	[NodeCategory("Devices/Mouse")]
	[ContinuouslyChanging]
	public abstract class MouseNode<T> : ValueFunctionNode<FrooxEngineContext, T> where T : unmanaged
	{
		protected override T Compute(FrooxEngineContext context)
		{
			return GetState(context.World.InputInterface);
		}

		protected abstract T GetState(InputInterface input);
	}
	public class LeftMousePressed : MouseNode<bool>
	{
		protected override bool GetState(InputInterface input)
		{
			return input.Mouse?.LeftButton?.Pressed == true;
		}
	}
	public class LeftMouseHeld : MouseNode<bool>
	{
		protected override bool GetState(InputInterface input)
		{
			return input.Mouse?.LeftButton?.Held == true;
		}
	}
	public class LeftMouseReleased : MouseNode<bool>
	{
		protected override bool GetState(InputInterface input)
		{
			return input.Mouse?.LeftButton?.Released == true;
		}
	}
	public class RightMousePressed : MouseNode<bool>
	{
		protected override bool GetState(InputInterface input)
		{
			return input.Mouse?.RightButton?.Pressed == true;
		}
	}
	public class RightMouseHeld : MouseNode<bool>
	{
		protected override bool GetState(InputInterface input)
		{
			return input.Mouse?.RightButton?.Held == true;
		}
	}
	public class RightMouseReleased : MouseNode<bool>
	{
		protected override bool GetState(InputInterface input)
		{
			return input.Mouse?.RightButton?.Released == true;
		}
	}
	public class MiddleMousePressed : MouseNode<bool>
	{
		protected override bool GetState(InputInterface input)
		{
			return input.Mouse?.MiddleButton?.Pressed == true;
		}
	}
	public class MiddleMouseHeld : MouseNode<bool>
	{
		protected override bool GetState(InputInterface input)
		{
			return input.Mouse?.MiddleButton?.Held == true;
		}
	}
	public class MiddleMouseReleased : MouseNode<bool>
	{
		protected override bool GetState(InputInterface input)
		{
			return input.Mouse?.MiddleButton?.Released == true;
		}
	}
	public class MouseScrollDelta : MouseNode<float>
	{
		protected override float GetState(InputInterface input)
		{
			return input.Mouse?.ScrollWheelDelta.Value.y ?? 0f;
		}
	}
	public class MouseScrollDelta2D : MouseNode<float2>
	{
		protected override float2 GetState(InputInterface input)
		{
			return input.Mouse?.ScrollWheelDelta.Value ?? float2.Zero;
		}
	}
	public class MousePosition : MouseNode<float2>
	{
		protected override float2 GetState(InputInterface input)
		{
			return input.Mouse?.WindowPosition.Value ?? new float2(-1f, -1f);
		}
	}
	public class NormalizedMousePosition : MouseNode<float2>
	{
		protected override float2 GetState(InputInterface input)
		{
			return input.Mouse?.NormalizedWindowPosition ?? new float2(-1f, -1f);
		}
	}
	public class DesktopMousePosition : MouseNode<float2>
	{
		protected override float2 GetState(InputInterface input)
		{
			return input.Mouse?.DesktopPosition.Value ?? new float2(-1f, -1f);
		}
	}
	public class MouseMovementDelta : MouseNode<float2>
	{
		protected override float2 GetState(InputInterface input)
		{
			return input.Mouse?.DirectDelta.Value ?? float2.Zero;
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Input.Keyboard
{
	public static class KeyboardNodeHelper
	{
		public static bool CanReadKeyboard(this FrooxEngineContext context)
		{
			if (context.World.LocalUser.HasActiveFocus())
			{
				return false;
			}
			if (Userspace.HasFocus)
			{
				return false;
			}
			if (context.World.Focus != World.WorldFocus.Focused)
			{
				return false;
			}
			return true;
		}
	}
	[NodeCategory("Devices/Keyboard")]
	[ContinuouslyChanging]
	public class TypeDelta : ObjectFunctionNode<FrooxEngineContext, string>
	{
		protected override string Compute(FrooxEngineContext context)
		{
			if (!context.CanReadKeyboard())
			{
				return "";
			}
			return context.World.InputInterface.TypeDelta;
		}
	}
	[NodeCategory("Devices/Keyboard")]
	[ContinuouslyChanging]
	public abstract class KeyNode : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ValueArgument<Key> Key;

		protected override bool Compute(FrooxEngineContext context)
		{
			if (!context.CanReadKeyboard())
			{
				return false;
			}
			return GetStatus(0.ReadValue<Key>(context), context.World.InputInterface);
		}

		protected abstract bool GetStatus(Key key, InputInterface input);
	}
	public class KeyPressed : KeyNode
	{
		protected override bool GetStatus(Key key, InputInterface input)
		{
			return input.GetKeyDown(key);
		}
	}
	public class KeyHeld : KeyNode
	{
		protected override bool GetStatus(Key key, InputInterface input)
		{
			return input.GetKey(key);
		}
	}
	public class KeyReleased : KeyNode
	{
		protected override bool GetStatus(Key key, InputInterface input)
		{
			return input.GetKeyUp(key);
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Input.Headsets
{
	[NodeCategory("Devices")]
	public class GeneralHeadset : VoidNode<FrooxEngineContext>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> IsActive;

		[ContinuouslyChanging]
		public readonly ValueOutput<float> BatteryLevel;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> IsBatteryCharging;

		protected override void ComputeOutputs(FrooxEngineContext context)
		{
			global::FrooxEngine.User user = 0.ReadObject<global::FrooxEngine.User>(context);
			if (user != null && user.IsRemoved)
			{
				user = null;
			}
			HeadsetProxy headsetProxy = user?.GetComponent<HeadsetProxy>();
			if (headsetProxy == null && user != null)
			{
				headsetProxy = user.AttachComponent<HeadsetProxy>();
			}
			IsActive.Write(headsetProxy?.IsHeadsetActive.Value ?? false, context);
			BatteryLevel.Write(headsetProxy?.BatteryLevel.Target?.Value ?? (-1f), context);
			IsBatteryCharging.Write(headsetProxy?.BatteryCharging.Target?.Value == true, context);
		}

		public GeneralHeadset()
		{
			IsActive = new ValueOutput<bool>(this);
			BatteryLevel = new ValueOutput<float>(this);
			IsBatteryCharging = new ValueOutput<bool>(this);
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Input.Haptics
{
	[NodeCategory("Devices/Haptics")]
	public class TriggerHapticsInHierarchy : ActionBreakableFlowNode<FrooxEngineContext>, IMappableNode, INode
	{
		public ObjectInput<Slot> TargetHierarchy;

		[ProtoFlux.Core.DefaultValue(0.5f)]
		public ValueInput<float> RelativeIntensity;

		protected override bool Do(FrooxEngineContext context)
		{
			Slot slot = TargetHierarchy.Evaluate(context) ?? context.GetRootSlotContainer(this);
			if (slot == null || slot.IsRemoved)
			{
				return false;
			}
			if (slot.TryVibrateRelative(RelativeIntensity.Evaluate(context, 0.5f)))
			{
				return true;
			}
			return false;
		}
	}
	[NodeCategory("Devices/Haptics")]
	public class TriggerHapticsOnController : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ValueInput<Chirality> Side;

		[ProtoFlux.Core.DefaultValue(0.5f)]
		public ValueInput<float> RelativeIntensity;

		public static Chirality SideDefault => (Chirality)(-1);

		protected override bool Do(FrooxEngineContext context)
		{
			if (context.World.InputInterface.GetControllerNode(Side.Evaluate(context, SideDefault)) is IVibrationDevice vibrationDevice)
			{
				vibrationDevice.Vibrate(MathX.LerpUnclamped(vibrationDevice.ShortInterval, vibrationDevice.LongInterval, RelativeIntensity.Evaluate(context, 0.5f)));
				return true;
			}
			return false;
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Input.Display
{
	[NodeCategory("Devices/Display")]
	public class LocalPrimaryResolution : ValueFunctionNode<FrooxEngineContext, int2>
	{
		protected override int2 Compute(FrooxEngineContext context)
		{
			return context.World.InputInterface.PrimaryResolution;
		}
	}
	[NodeCategory("Devices/Display")]
	[ContinuouslyChanging]
	public class LocalWindowResolution : ValueFunctionNode<FrooxEngineContext, int2>
	{
		protected override int2 Compute(FrooxEngineContext context)
		{
			return context.World.InputInterface.WindowResolution;
		}
	}
	[NodeCategory("Devices/Display")]
	[ContinuouslyChanging]
	public class LocalWindowAspectRatio : ValueFunctionNode<FrooxEngineContext, float>
	{
		protected override float Compute(FrooxEngineContext context)
		{
			return context.World.InputInterface.WindowAspectRatio;
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Input.Controllers
{
	[NodeCategory("Devices/Controllers")]
	public abstract class ControllerNode<C, P> : VoidNode<FrooxEngineContext> where C : class, IStandardController where P : ControllerProxy<C>, new()
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		public ValueArgument<Chirality> Node;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> IsActive;

		[ContinuouslyChanging]
		public readonly ObjectOutput<Type> Type;

		[ContinuouslyChanging]
		public readonly ValueOutput<float> BatteryLevel;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> IsBatteryCharging;

		protected override void ComputeOutputs(FrooxEngineContext context)
		{
			global::FrooxEngine.User user = 0.ReadObject<global::FrooxEngine.User>(context);
			Chirality side = 1.ReadValue<Chirality>(context);
			if (user != null && user.IsRemoved)
			{
				user = null;
			}
			P val = ((user != null) ? user.GetComponent((P c) => c.Side.Value == side) : null);
			if (val == null && user != null)
			{
				val = user.AttachComponent<P>();
				val.Side.Value = side;
			}
			IsActive.Write(val?.IsControllerActive.Value ?? false, context);
			Type.Write(val?.ControllerType.Value, context);
			BatteryLevel.Write(val?.BatteryLevel.Target?.Value ?? (-1f), context);
			IsBatteryCharging.Write(val?.BatteryCharging.Target?.Value == true, context);
			Update(val, context);
		}

		protected abstract void Update(P proxy, FrooxEngineContext context);

		protected ControllerNode()
		{
			((ControllerNode<, >)(object)this).IsActive = new ValueOutput<bool>(this);
			((ControllerNode<, >)(object)this).Type = new ObjectOutput<Type>(this);
			((ControllerNode<, >)(object)this).BatteryLevel = new ValueOutput<float>(this);
			((ControllerNode<, >)(object)this).IsBatteryCharging = new ValueOutput<bool>(this);
		}
	}
	public class StandardController : ControllerNode<IStandardController, StandardControllerProxy>
	{
		[ContinuouslyChanging]
		public readonly ValueOutput<bool> Primary;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> Secondary;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> Grab;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> Menu;

		[ContinuouslyChanging]
		public readonly ValueOutput<float> Strength;

		[ContinuouslyChanging]
		public readonly ValueOutput<float2> Axis;

		protected override void Update(StandardControllerProxy proxy, FrooxEngineContext context)
		{
			Primary.Write(proxy?.Primary.Target?.Value == true, context);
			Secondary.Write(proxy?.Secondary.Target?.Value == true, context);
			Grab.Write(proxy?.Grab.Target?.Value == true, context);
			Menu.Write(proxy?.Menu.Target?.Value == true, context);
			Strength.Write((proxy?.Strength.Target?.Value).GetValueOrDefault(), context);
			Axis.Write(proxy?.Axis.Target?.Value ?? float2.Zero, context);
		}

		public StandardController()
		{
			Primary = new ValueOutput<bool>(this);
			Secondary = new ValueOutput<bool>(this);
			Grab = new ValueOutput<bool>(this);
			Menu = new ValueOutput<bool>(this);
			Strength = new ValueOutput<float>(this);
			Axis = new ValueOutput<float2>(this);
		}
	}
	public class IndexController : ControllerNode<global::FrooxEngine.IndexController, IndexControllerProxy>
	{
		[ContinuouslyChanging]
		public readonly ValueOutput<bool> ButtonA;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> ButtonB;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> ButtonA_Touch;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> ButtonB_Touch;

		[ContinuouslyChanging]
		public readonly ValueOutput<float> Grip;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> GripTouch;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> GripClick;

		[ContinuouslyChanging]
		public readonly ValueOutput<float2> Joystick;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> JoystickTouch;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> JoystickClick;

		[ContinuouslyChanging]
		public readonly ValueOutput<float> Trigger;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> TriggerTouch;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> TriggerClick;

		[ContinuouslyChanging]
		public readonly ValueOutput<float2> Touchpad;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> TouchpadTouch;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> TouchpadPress;

		[ContinuouslyChanging]
		public readonly ValueOutput<float> TouchpadForce;

		protected override void Update(IndexControllerProxy proxy, FrooxEngineContext context)
		{
			ButtonA.Write(proxy?.ButtonA.Target?.Value == true, context);
			ButtonB.Write(proxy?.ButtonB.Target?.Value == true, context);
			ButtonA_Touch.Write(proxy?.ButtonA_Touch.Target?.Value == true, context);
			ButtonB_Touch.Write(proxy?.ButtonB_Touch.Target?.Value == true, context);
			Grip.Write((proxy?.Grip.Target?.Value).GetValueOrDefault(), context);
			GripTouch.Write(proxy?.GripTouch.Target?.Value == true, context);
			GripClick.Write(proxy?.GripClick.Target?.Value == true, context);
			Joystick.Write(proxy?.Joystick.Target?.Value ?? float2.Zero, context);
			JoystickTouch.Write(proxy?.JoystickTouch.Target?.Value == true, context);
			JoystickClick.Write(proxy?.JoystickClick.Target?.Value == true, context);
			Trigger.Write((proxy?.Trigger.Target?.Value).GetValueOrDefault(), context);
			TriggerTouch.Write(proxy?.TriggerTouch.Target?.Value == true, context);
			TriggerClick.Write(proxy?.TriggerClick.Target?.Value == true, context);
			Touchpad.Write(proxy?.Touchpad.Target?.Value ?? float2.Zero, context);
			TouchpadTouch.Write(proxy?.TouchpadTouch.Target?.Value == true, context);
			TouchpadPress.Write(proxy?.TouchpadPress.Target?.Value == true, context);
			TouchpadForce.Write((proxy?.TouchpadForce.Target?.Value).GetValueOrDefault(), context);
		}

		public IndexController()
		{
			ButtonA = new ValueOutput<bool>(this);
			ButtonB = new ValueOutput<bool>(this);
			ButtonA_Touch = new ValueOutput<bool>(this);
			ButtonB_Touch = new ValueOutput<bool>(this);
			Grip = new ValueOutput<float>(this);
			GripTouch = new ValueOutput<bool>(this);
			GripClick = new ValueOutput<bool>(this);
			Joystick = new ValueOutput<float2>(this);
			JoystickTouch = new ValueOutput<bool>(this);
			JoystickClick = new ValueOutput<bool>(this);
			Trigger = new ValueOutput<float>(this);
			TriggerTouch = new ValueOutput<bool>(this);
			TriggerClick = new ValueOutput<bool>(this);
			Touchpad = new ValueOutput<float2>(this);
			TouchpadTouch = new ValueOutput<bool>(this);
			TouchpadPress = new ValueOutput<bool>(this);
			TouchpadForce = new ValueOutput<float>(this);
		}
	}
	public class TouchController : ControllerNode<global::FrooxEngine.TouchController, TouchControllerProxy>
	{
		[ContinuouslyChanging]
		public readonly ValueOutput<TouchControllerModel> Model;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> Start;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> ButtonYB;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> ButtonXA;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> ButtonYB_Touch;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> ButtonXA_Touch;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> ThumbRestTouch;

		[ContinuouslyChanging]
		public readonly ValueOutput<float> Grip;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> GripClick;

		[ContinuouslyChanging]
		public readonly ValueOutput<float2> Joystick;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> JoystickTouch;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> JoystickClick;

		[ContinuouslyChanging]
		public readonly ValueOutput<float> Trigger;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> TriggerTouch;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> TriggerClick;

		protected override void Update(TouchControllerProxy proxy, FrooxEngineContext context)
		{
			Model.Write(proxy?.Model.Value ?? ((TouchControllerModel)(-1)), context);
			Start.Write(proxy?.Start.Target?.Value == true, context);
			ButtonYB.Write(proxy?.ButtonYB.Target?.Value == true, context);
			ButtonXA.Write(proxy?.ButtonXA.Target?.Value == true, context);
			ButtonYB_Touch.Write(proxy?.ButtonYB_Touch.Target?.Value == true, context);
			ButtonXA_Touch.Write(proxy?.ButtonXA_Touch.Target?.Value == true, context);
			ThumbRestTouch.Write(proxy?.ThumbRestTouch.Target?.Value == true, context);
			Grip.Write((proxy?.Grip.Target?.Value).GetValueOrDefault(), context);
			GripClick.Write(proxy?.GripClick.Target?.Value == true, context);
			Joystick.Write(proxy?.Joystick.Target?.Value ?? float2.Zero, context);
			JoystickTouch.Write(proxy?.JoystickTouch.Target?.Value == true, context);
			JoystickClick.Write(proxy?.JoystickClick.Target?.Value == true, context);
			Trigger.Write((proxy?.Trigger.Target?.Value).GetValueOrDefault(), context);
			TriggerTouch.Write(proxy?.TriggerTouch.Target?.Value == true, context);
			TriggerClick.Write(proxy?.TriggerClick.Target?.Value == true, context);
		}

		public TouchController()
		{
			Model = new ValueOutput<TouchControllerModel>(this);
			Start = new ValueOutput<bool>(this);
			ButtonYB = new ValueOutput<bool>(this);
			ButtonXA = new ValueOutput<bool>(this);
			ButtonYB_Touch = new ValueOutput<bool>(this);
			ButtonXA_Touch = new ValueOutput<bool>(this);
			ThumbRestTouch = new ValueOutput<bool>(this);
			Grip = new ValueOutput<float>(this);
			GripClick = new ValueOutput<bool>(this);
			Joystick = new ValueOutput<float2>(this);
			JoystickTouch = new ValueOutput<bool>(this);
			JoystickClick = new ValueOutput<bool>(this);
			Trigger = new ValueOutput<float>(this);
			TriggerTouch = new ValueOutput<bool>(this);
			TriggerClick = new ValueOutput<bool>(this);
		}
	}
	public class ViveController : ControllerNode<global::FrooxEngine.ViveController, ViveControllerProxy>
	{
		[ContinuouslyChanging]
		public readonly ValueOutput<bool> Grip;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> App;

		[ContinuouslyChanging]
		public readonly ValueOutput<float> Trigger;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> TriggerHair;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> TriggerClick;

		[ContinuouslyChanging]
		public readonly ValueOutput<float2> Touchpad;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> TouchpadTouch;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> TouchpadClick;

		protected override void Update(ViveControllerProxy proxy, FrooxEngineContext context)
		{
			Grip.Write(proxy?.Grip.Target?.Value == true, context);
			App.Write(proxy?.App.Target?.Value == true, context);
			Trigger.Write((proxy?.Trigger.Target?.Value).GetValueOrDefault(), context);
			TriggerHair.Write(proxy?.TriggerHair.Target?.Value == true, context);
			TriggerClick.Write(proxy?.TriggerClick.Target?.Value == true, context);
			Touchpad.Write(proxy?.Touchpad.Target?.Value ?? float2.Zero, context);
			TouchpadTouch.Write(proxy?.TouchpadTouch.Target?.Value == true, context);
			TouchpadClick.Write(proxy?.TouchpadClick.Target?.Value == true, context);
		}

		public ViveController()
		{
			Grip = new ValueOutput<bool>(this);
			App = new ValueOutput<bool>(this);
			Trigger = new ValueOutput<float>(this);
			TriggerHair = new ValueOutput<bool>(this);
			TriggerClick = new ValueOutput<bool>(this);
			Touchpad = new ValueOutput<float2>(this);
			TouchpadTouch = new ValueOutput<bool>(this);
			TouchpadClick = new ValueOutput<bool>(this);
		}
	}
	public class WindowsMRController : ControllerNode<global::FrooxEngine.WindowsMRController, WindowsMRControllerProxy>
	{
		[ContinuouslyChanging]
		public readonly ValueOutput<bool> Grip;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> App;

		[ContinuouslyChanging]
		public readonly ValueOutput<float2> Joystick;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> JoystickClick;

		[ContinuouslyChanging]
		public readonly ValueOutput<float> Trigger;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> TriggerHair;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> TriggerClick;

		[ContinuouslyChanging]
		public readonly ValueOutput<float2> Touchpad;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> TouchpadTouch;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> TouchpadClick;

		protected override void Update(WindowsMRControllerProxy proxy, FrooxEngineContext context)
		{
			Grip.Write(proxy?.Grip.Target?.Value == true, context);
			App.Write(proxy?.App.Target?.Value == true, context);
			Joystick.Write(proxy?.Joystick.Target?.Value ?? float2.Zero, context);
			JoystickClick.Write(proxy?.JoystickClick.Target?.Value == true, context);
			Trigger.Write((proxy?.Trigger.Target?.Value).GetValueOrDefault(), context);
			TriggerHair.Write(proxy?.TriggerHair.Target?.Value == true, context);
			TriggerClick.Write(proxy?.TriggerClick.Target?.Value == true, context);
			Touchpad.Write(proxy?.Touchpad.Target?.Value ?? float2.Zero, context);
			TouchpadTouch.Write(proxy?.TouchpadTouch.Target?.Value == true, context);
			TouchpadClick.Write(proxy?.TouchpadClick.Target?.Value == true, context);
		}

		public WindowsMRController()
		{
			Grip = new ValueOutput<bool>(this);
			App = new ValueOutput<bool>(this);
			Joystick = new ValueOutput<float2>(this);
			JoystickClick = new ValueOutput<bool>(this);
			Trigger = new ValueOutput<float>(this);
			TriggerHair = new ValueOutput<bool>(this);
			TriggerClick = new ValueOutput<bool>(this);
			Touchpad = new ValueOutput<float2>(this);
			TouchpadTouch = new ValueOutput<bool>(this);
			TouchpadClick = new ValueOutput<bool>(this);
		}
	}
	public class HPReverbController : ControllerNode<global::FrooxEngine.HPReverbController, HPReverbControllerProxy>
	{
		[ContinuouslyChanging]
		public readonly ValueOutput<bool> AppMenu;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> ButtonYB;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> ButtonXA;

		[ContinuouslyChanging]
		public readonly ValueOutput<float> Grip;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> GripTouch;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> GripClick;

		[ContinuouslyChanging]
		public readonly ValueOutput<float2> Joystick;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> JoystickClick;

		[ContinuouslyChanging]
		public readonly ValueOutput<float> Trigger;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> TriggerClick;

		protected override void Update(HPReverbControllerProxy proxy, FrooxEngineContext context)
		{
			AppMenu.Write(proxy?.AppMenu.Target?.Value == true, context);
			ButtonYB.Write(proxy?.ButtonYB.Target?.Value == true, context);
			ButtonXA.Write(proxy?.ButtonXA.Target?.Value == true, context);
			Grip.Write((proxy?.Grip.Target?.Value).GetValueOrDefault(), context);
			GripTouch.Write(proxy?.GripTouch.Target?.Value == true, context);
			GripClick.Write(proxy?.GripClick.Target?.Value == true, context);
			Joystick.Write(proxy?.Joystick.Target?.Value ?? float2.Zero, context);
			JoystickClick.Write(proxy?.JoystickClick.Target?.Value == true, context);
			Trigger.Write((proxy?.Trigger.Target?.Value).GetValueOrDefault(), context);
			TriggerClick.Write(proxy?.TriggerClick.Target?.Value == true, context);
		}

		public HPReverbController()
		{
			AppMenu = new ValueOutput<bool>(this);
			ButtonYB = new ValueOutput<bool>(this);
			ButtonXA = new ValueOutput<bool>(this);
			Grip = new ValueOutput<float>(this);
			GripTouch = new ValueOutput<bool>(this);
			GripClick = new ValueOutput<bool>(this);
			Joystick = new ValueOutput<float2>(this);
			JoystickClick = new ValueOutput<bool>(this);
			Trigger = new ValueOutput<float>(this);
			TriggerClick = new ValueOutput<bool>(this);
		}
	}
	public class CosmosController : ControllerNode<global::FrooxEngine.CosmosController, CosmosControllerProxy>
	{
		[ContinuouslyChanging]
		public readonly ValueOutput<bool> Menu;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> ButtonBY;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> ButtonAX;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> GripClick;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> Bumper;

		[ContinuouslyChanging]
		public readonly ValueOutput<float2> Joystick;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> JoystickTouch;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> JoystickClick;

		[ContinuouslyChanging]
		public readonly ValueOutput<float> Trigger;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> TriggerTouch;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> TriggerClick;

		protected override void Update(CosmosControllerProxy proxy, FrooxEngineContext context)
		{
			Menu.Write(proxy?.Menu.Target?.Value == true, context);
			ButtonBY.Write(proxy?.ButtonBY.Target?.Value == true, context);
			ButtonAX.Write(proxy?.ButtonAX.Target?.Value == true, context);
			GripClick.Write(proxy?.GripClick.Target?.Value == true, context);
			Bumper.Write(proxy?.Bumper.Target?.Value == true, context);
			Joystick.Write(proxy?.Joystick.Target?.Value ?? float2.Zero, context);
			JoystickTouch.Write(proxy?.JoystickTouch.Target?.Value == true, context);
			JoystickClick.Write(proxy?.JoystickClick.Target?.Value == true, context);
			Trigger.Write((proxy?.Trigger.Target?.Value).GetValueOrDefault(), context);
			TriggerTouch.Write(proxy?.TriggerTouch.Target?.Value == true, context);
			TriggerClick.Write(proxy?.TriggerClick.Target?.Value == true, context);
		}

		public CosmosController()
		{
			Menu = new ValueOutput<bool>(this);
			ButtonBY = new ValueOutput<bool>(this);
			ButtonAX = new ValueOutput<bool>(this);
			GripClick = new ValueOutput<bool>(this);
			Bumper = new ValueOutput<bool>(this);
			Joystick = new ValueOutput<float2>(this);
			JoystickTouch = new ValueOutput<bool>(this);
			JoystickClick = new ValueOutput<bool>(this);
			Trigger = new ValueOutput<float>(this);
			TriggerTouch = new ValueOutput<bool>(this);
			TriggerClick = new ValueOutput<bool>(this);
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Experimental
{
	[NodeCategory("Experimental")]
	public class WriteTextToFile : AsyncActionNode<FrooxEngineContext>
	{
		public ObjectInput<string> String;

		public ObjectInput<string> FilePath;

		public ValueInput<bool> Append;

		public ValueInput<bool> NewLine;

		public AsyncCall OnWriteStarted;

		public Continuation OnWriteFinished;

		public Continuation OnWriteFail;

		protected override async Task<IOperation> RunAsync(FrooxEngineContext context)
		{
			if (!context.World.UnsafeMode)
			{
				return null;
			}
			string file = FilePath.Evaluate(context);
			if (string.IsNullOrWhiteSpace(file))
			{
				return null;
			}
			string content = String.Evaluate(context) ?? "";
			bool append = Append.Evaluate(context, defaultValue: false);
			NewLine.Evaluate(context, defaultValue: false);
			Task writeTask = Task.Run(delegate
			{
				if (append)
				{
					File.AppendAllText(file, content);
				}
				else
				{
					File.WriteAllText(file, content);
				}
			});
			await OnWriteStarted.ExecuteAsync(context);
			try
			{
				await writeTask;
				return OnWriteFinished.Target;
			}
			catch (Exception)
			{
				return OnWriteFail.Target;
			}
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Debugging
{
	[NodeCategory("Debug")]
	[ContinuouslyChanging]
	public class EstimatedMasterClockError : ValueFunctionNode<FrooxEngineContext, float>
	{
		protected override float Compute(FrooxEngineContext context)
		{
			return (float)context.World.Time.EstimatedAuthorityTimeError;
		}
	}
	[NodeCategory("Debug")]
	public abstract class DebugNode : ActionFlowNode<FrooxEngineContext>, IMappableNode, INode
	{
		public static colorX ColorDefault => colorX.White;

		protected override void Do(FrooxEngineContext context)
		{
			RunDebug(context.World.Debug, context.GetRootSlotContainer(this), context);
		}

		protected abstract void RunDebug(DebugManager debug, Slot container, FrooxEngineContext context);
	}
	public class DebugText : DebugNode
	{
		public ValueInput<float3> Position;

		public ObjectInput<string> Text;

		public ValueInput<float> Size;

		public ValueInput<colorX> Color;

		public ValueInput<float> Duration;

		public static float SizeDefault => 0.1f;

		protected override void RunDebug(DebugManager debug, Slot container, FrooxEngineContext context)
		{
			debug.Text(Position.Evaluate(context, container.GlobalPosition), Text.Evaluate(context), Size.Evaluate(context, SizeDefault), Color.Evaluate(context, DebugNode.ColorDefault), Duration.Evaluate(context, 0f));
		}
	}
	public class DebugVector : DebugNode
	{
		public ValueInput<float3> Position;

		public ValueInput<float3> Vector;

		public ValueInput<colorX> Color;

		public ValueInput<float> RadiusRatio;

		public ValueInput<float> Duration;

		public static float RadiusRatioDefault => 1f;

		protected override void RunDebug(DebugManager debug, Slot container, FrooxEngineContext context)
		{
			debug.Vector(Position.Evaluate(context, container.GlobalPosition), Vector.Evaluate(context), Color.Evaluate(context, DebugNode.ColorDefault), RadiusRatio.Evaluate(context, RadiusRatioDefault), Duration.Evaluate(context, 0f));
		}
	}
	public class DebugLine : DebugNode
	{
		public ValueInput<float3> Point0;

		public ValueInput<float3> Point1;

		public ValueInput<colorX> Color;

		public ValueInput<float> Radius;

		public ValueInput<float> Duration;

		public static float RadiusDefault => 0.005f;

		protected override void RunDebug(DebugManager debug, Slot container, FrooxEngineContext context)
		{
			debug.Line(Point0.Evaluate(context), Point1.Evaluate(context), Color.Evaluate(context, DebugNode.ColorDefault), Radius.Evaluate(context, RadiusDefault), Duration.Evaluate(context, 0f));
		}
	}
	public class DebugTriangle : DebugNode
	{
		public ValueInput<float3> Point0;

		public ValueInput<float3> Point1;

		public ValueInput<float3> Point2;

		public ValueInput<colorX> Color;

		public ValueInput<float> Duration;

		protected override void RunDebug(DebugManager debug, Slot container, FrooxEngineContext context)
		{
			debug.Triangle(Point0.Evaluate(context), Point1.Evaluate(context), Point2.Evaluate(context), Color.Evaluate(context, DebugNode.ColorDefault), Duration.Evaluate(context, 0f));
		}
	}
	public class DebugSphere : DebugNode
	{
		public ValueInput<float3> Point;

		public ValueInput<float> Radius;

		public ValueInput<colorX> Color;

		public ValueInput<float> Duration;

		protected override void RunDebug(DebugManager debug, Slot container, FrooxEngineContext context)
		{
			debug.Sphere(Point.Evaluate(context, container.GlobalPosition), Radius.Evaluate(context, 0f), Color.Evaluate(context, DebugNode.ColorDefault), 1, Duration.Evaluate(context, 0f));
		}
	}
	public class DebugBox : DebugNode
	{
		public ValueInput<float3> Point;

		public ValueInput<float3> Size;

		public ValueInput<floatQ> Orientation;

		public ValueInput<colorX> Color;

		public ValueInput<float> Duration;

		public static floatQ OrientationDefault => floatQ.Identity;

		protected override void RunDebug(DebugManager debug, Slot container, FrooxEngineContext context)
		{
			debug.Box(Point.Evaluate(context, container.GlobalPosition), Size.Evaluate(context), Color.Evaluate(context, DebugNode.ColorDefault), Orientation.Evaluate(context, OrientationDefault), Duration.Evaluate(context, 0f));
		}
	}
	public class DebugAxes : DebugNode
	{
		public ValueInput<float3> Position;

		public ValueInput<floatQ> Rotation;

		public ValueInput<float> Length;

		public ValueInput<colorX> RightColor;

		public ValueInput<colorX> UpColor;

		public ValueInput<colorX> ForwardColor;

		public ValueInput<float> Duration;

		public static floatQ RotationDefault => floatQ.Identity;

		public static float LengthDefault => 0.1f;

		public static colorX RightColorDefault => colorX.Red;

		public static colorX UpColorDefault => colorX.Green;

		public static colorX ForwardColorDefault => colorX.Blue;

		protected override void RunDebug(DebugManager debug, Slot container, FrooxEngineContext context)
		{
			debug.Axes(Position.Evaluate(context, container.GlobalPosition), Rotation.Evaluate(context, RotationDefault), Length.Evaluate(context, LengthDefault), new colorX?(RightColor.Evaluate(context, RightColorDefault)), new colorX?(UpColor.Evaluate(context, UpColorDefault)), new colorX?(ForwardColor.Evaluate(context, ForwardColorDefault)), Duration.Evaluate(context, 0f));
		}
	}
	[NodeCategory("Experimental")]
	[FeatureUpgradeReplacement("DebuggingTest", 1, typeof(TestFeatureUpgrade))]
	public class TestFeatureUpgrade : ActionNode<FrooxEngineContext>
	{
		protected override IOperation Run(FrooxEngineContext context)
		{
			return null;
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Elements
{
	[NodeCategory("References/Elements")]
	public class ElementExists : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<IWorldElement> Element;

		protected override bool Compute(FrooxEngineContext context)
		{
			IWorldElement worldElement = 0.ReadObject<IWorldElement>(context);
			if (worldElement == null)
			{
				return false;
			}
			return !worldElement.IsRemoved;
		}
	}
	[NodeCategory("References/Elements")]
	public class IsRemoved : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<IWorldElement> Element;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<IWorldElement>(context)?.IsRemoved ?? false;
		}
	}
	[NodeCategory("References/Elements")]
	public class IsDisposed : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<Worker> Element;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<Worker>(context)?.IsDisposed ?? false;
		}
	}
	[NodeCategory("References/Elements")]
	public class IsDestroyed : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<IDestroyable> Element;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<IDestroyable>(context)?.IsDestroyed ?? false;
		}
	}
	[NodeCategory("References/Elements")]
	public class IsLocalElement : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<IWorldElement> Element;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<IWorldElement>(context)?.IsLocalElement ?? false;
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Slots
{
	[NodeCategory("Slots/Info")]
	public class SetSlotName : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<Slot> Instance;

		public ObjectInput<string> Name;

		protected override bool Do(FrooxEngineContext context)
		{
			Slot slot = Instance.Evaluate(context);
			if (slot == null || slot.IsRemoved)
			{
				return false;
			}
			slot.Name = Name.Evaluate(context);
			return true;
		}
	}
	[NodeCategory("Slots/Info")]
	public class SetSlotActiveSelf : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<Slot> Instance;

		public ValueInput<bool> Active;

		protected override bool Do(FrooxEngineContext context)
		{
			Slot slot = Instance.Evaluate(context);
			if (slot == null || slot.IsRemoved)
			{
				return false;
			}
			if (Active.Source == null)
			{
				return true;
			}
			slot.ActiveSelf = Active.Evaluate(context, defaultValue: false);
			return true;
		}
	}
	[NodeCategory("Slots/Info")]
	public class SetSlotPersistentSelf : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<Slot> Instance;

		public ValueInput<bool> Persistent;

		protected override bool Do(FrooxEngineContext context)
		{
			Slot slot = Instance.Evaluate(context);
			if (slot == null || slot.IsRemoved)
			{
				return false;
			}
			if (Persistent.Source == null)
			{
				return true;
			}
			slot.PersistentSelf = Persistent.Evaluate(context, defaultValue: false);
			return true;
		}
	}
	[NodeCategory("Slots/Info")]
	public class SetTag : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<Slot> Instance;

		public ObjectInput<string> Tag;

		protected override bool Do(FrooxEngineContext context)
		{
			Slot slot = Instance.Evaluate(context);
			if (slot == null || slot.IsRemoved)
			{
				return false;
			}
			slot.Tag = Tag.Evaluate(context);
			return true;
		}
	}
	[NodeCategory("Slots")]
	public class SetChildIndex : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<Slot> Instance;

		public ValueInput<int> Index;

		protected override bool Do(FrooxEngineContext context)
		{
			Slot slot = Instance.Evaluate(context);
			if (slot == null || slot.IsRemoved)
			{
				return false;
			}
			if (slot.IsRootSlot)
			{
				return false;
			}
			int num = Index.Evaluate(context, 0);
			if (num < 0 || num >= slot.Parent.ChildrenCount)
			{
				return false;
			}
			slot.ChildIndex = num;
			return true;
		}
	}
	[NodeCategory("Slots/Info")]
	public class SetSlotOrderOffset : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<Slot> Instance;

		public ValueInput<long> OrderOffset;

		protected override bool Do(FrooxEngineContext context)
		{
			Slot slot = Instance.Evaluate(context);
			if (slot == null || slot.IsRemoved)
			{
				return false;
			}
			if (slot.IsRootSlot)
			{
				return false;
			}
			long orderOffset = OrderOffset.Evaluate(context, 0L);
			slot.OrderOffset = orderOffset;
			return true;
		}
	}
	[NodeCategory("Slots")]
	public class DuplicateSlot : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<Slot> Template;

		public ObjectInput<Slot> OverrideParent;

		public readonly ObjectOutput<Slot> Duplicate;

		protected override bool Do(FrooxEngineContext context)
		{
			Slot slot = Template.Evaluate(context);
			if (slot == null || slot.IsRemoved || slot.IsRootSlot)
			{
				return false;
			}
			Slot duplicateRoot = OverrideParent.Evaluate(context);
			Duplicate.Write(slot.Duplicate(duplicateRoot), context);
			return true;
		}

		public DuplicateSlot()
		{
			Duplicate = new ObjectOutput<Slot>(this);
		}
	}
	[NodeCategory("Slots")]
	public class SetParent : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<Slot> Instance;

		public ObjectInput<Slot> NewParent;

		[ProtoFlux.Core.DefaultValue(true)]
		public ValueInput<bool> PreserveGlobalPosition;

		protected override bool Do(FrooxEngineContext context)
		{
			Slot slot = Instance.Evaluate(context);
			if (slot == null || slot.IsRemoved)
			{
				return false;
			}
			Slot slot2 = NewParent.Evaluate(context) ?? context.World.RootSlot;
			if (slot2.IsChildOf(slot, includeSelf: true))
			{
				return false;
			}
			slot.SetParent(slot2, PreserveGlobalPosition.Evaluate(context, defaultValue: true));
			return true;
		}
	}
	[NodeCategory("Slots")]
	public class DestroySlot : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<Slot> Instance;

		public ValueInput<bool> PreserveAssets;

		[ProtoFlux.Core.DefaultValue(true)]
		public ValueInput<bool> SendDestroyingEvent;

		protected override bool Do(FrooxEngineContext context)
		{
			Slot slot = Instance.Evaluate(context);
			if (slot == null || slot.IsRemoved)
			{
				return false;
			}
			if (slot.IsRootSlot)
			{
				return false;
			}
			bool sendDestroyingEvent = SendDestroyingEvent.Evaluate(context, defaultValue: true);
			if (PreserveAssets.Evaluate(context, defaultValue: false))
			{
				slot.DestroyPreservingAssets(null, sendDestroyingEvent);
			}
			else
			{
				slot.Destroy(sendDestroyingEvent);
			}
			return true;
		}
	}
	[NodeCategory("Slots")]
	public class DestroySlotChildren : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<Slot> Instance;

		public ValueInput<bool> PreserveAssets;

		[ProtoFlux.Core.DefaultValue(true)]
		public ValueInput<bool> SendDestroyingEvent;

		protected override bool Do(FrooxEngineContext context)
		{
			Slot slot = Instance.Evaluate(context);
			if (slot == null || slot.IsRemoved)
			{
				return false;
			}
			if (slot.IsRootSlot)
			{
				return false;
			}
			bool preserveAssets = PreserveAssets.Evaluate(context, defaultValue: false);
			bool sendDestroyingEvent = SendDestroyingEvent.Evaluate(context, defaultValue: true);
			slot.DestroyChildren(preserveAssets, sendDestroyingEvent);
			return true;
		}
	}
	[NodeCategory("Flow/Events")]
	public class OnStart : ProxyVoidNode<FrooxEngineContext, OnStart.Proxy>
	{
		public class Proxy : ProtoFluxEngineProxy
		{
			public Action Start;

			public bool StartScheduled;

			protected override void OnStart()
			{
				base.OnStart();
				Start?.Invoke();
				StartScheduled = true;
			}

			protected override void InitializeSyncMembers()
			{
				base.InitializeSyncMembers();
			}

			public override ISyncMember GetSyncMember(int index)
			{
				return index switch
				{
					0 => persistent, 
					1 => updateOrder, 
					2 => EnabledField, 
					3 => Node, 
					4 => Path, 
					_ => throw new ArgumentOutOfRangeException(), 
				};
			}

			public static Proxy __New()
			{
				return new Proxy();
			}
		}

		public Call Trigger;

		public ValueInput<bool> OnlyHost;

		protected override void ProxyAdded(Proxy proxy, FrooxEngineContext context)
		{
			NodeContextPath path = context.CaptureContextPath();
			context.GetEventDispatcher(out var dispatcher);
			Action action = (proxy.Start = delegate
			{
				dispatcher.ScheduleEvent(path, HandleEvent, null);
			});
			if (proxy.StartScheduled)
			{
				proxy.StartScheduled = false;
				action();
			}
		}

		protected override void ProxyRemoved(Proxy proxy, FrooxEngineContext context, bool inUseByAnotherInstance)
		{
			if (!inUseByAnotherInstance)
			{
				proxy.Start = null;
			}
		}

		private void HandleEvent(FrooxEngineContext context, object eventData)
		{
			Proxy proxy = GetProxy(context);
			if (proxy != null && !proxy.IsRemoved)
			{
				proxy.StartScheduled = false;
				if (!OnlyHost.Evaluate(context, defaultValue: false) || context.LocalUser.IsHost)
				{
					Trigger.Execute(context);
				}
			}
		}
	}
	[NodeCategory("Flow/Events")]
	public class OnDuplicate : ProxyVoidNode<FrooxEngineContext, OnDuplicate.Proxy>
	{
		public class Proxy : ProtoFluxEngineProxy
		{
			public Action Duplicate;

			public bool DuplicateScheduled;

			protected override void OnDuplicate()
			{
				base.OnDuplicate();
				Duplicate?.Invoke();
				DuplicateScheduled = true;
			}

			protected override void InitializeSyncMembers()
			{
				base.InitializeSyncMembers();
			}

			public override ISyncMember GetSyncMember(int index)
			{
				return index switch
				{
					0 => persistent, 
					1 => updateOrder, 
					2 => EnabledField, 
					3 => Node, 
					4 => Path, 
					_ => throw new ArgumentOutOfRangeException(), 
				};
			}

			public static Proxy __New()
			{
				return new Proxy();
			}
		}

		public Call Trigger;

		protected override void ProxyAdded(Proxy proxy, FrooxEngineContext context)
		{
			NodeContextPath path = context.CaptureContextPath();
			context.GetEventDispatcher(out var dispatcher);
			Action action = (proxy.Duplicate = delegate
			{
				dispatcher.ScheduleEvent(path, HandleEvent, null);
			});
			if (proxy.DuplicateScheduled)
			{
				proxy.DuplicateScheduled = false;
				action();
			}
		}

		protected override void ProxyRemoved(Proxy proxy, FrooxEngineContext context, bool inUseByAnotherInstance)
		{
			if (proxy != null && !inUseByAnotherInstance)
			{
				proxy.Duplicate = null;
			}
		}

		private void HandleEvent(FrooxEngineContext context, object eventData)
		{
			Proxy proxy = GetProxy(context);
			if (proxy != null)
			{
				proxy.DuplicateScheduled = false;
				Trigger.Execute(context);
			}
		}
	}
	[NodeCategory("Flow/Events")]
	public class OnPaste : ProxyVoidNode<FrooxEngineContext, OnPaste.Proxy>
	{
		public class Proxy : ProtoFluxEngineProxy
		{
			public Action Paste;

			public bool PasteScheduled;

			protected override void OnPaste()
			{
				base.OnPaste();
				Paste?.Invoke();
				PasteScheduled = true;
			}

			protected override void InitializeSyncMembers()
			{
				base.InitializeSyncMembers();
			}

			public override ISyncMember GetSyncMember(int index)
			{
				return index switch
				{
					0 => persistent, 
					1 => updateOrder, 
					2 => EnabledField, 
					3 => Node, 
					4 => Path, 
					_ => throw new ArgumentOutOfRangeException(), 
				};
			}

			public static Proxy __New()
			{
				return new Proxy();
			}
		}

		public Call Trigger;

		protected override void ProxyAdded(Proxy proxy, FrooxEngineContext context)
		{
			NodeContextPath path = context.CaptureContextPath();
			context.GetEventDispatcher(out var dispatcher);
			Action action = (proxy.Paste = delegate
			{
				dispatcher.ScheduleEvent(path, HandleEvent, null);
			});
			if (proxy.PasteScheduled)
			{
				proxy.PasteScheduled = false;
				action();
			}
		}

		protected override void ProxyRemoved(Proxy proxy, FrooxEngineContext context, bool inUseByAnotherInstance)
		{
			if (!inUseByAnotherInstance)
			{
				proxy.Paste = null;
			}
		}

		private void HandleEvent(FrooxEngineContext context, object eventData)
		{
			Proxy proxy = GetProxy(context);
			if (proxy != null)
			{
				proxy.PasteScheduled = false;
				Trigger.Execute(context);
			}
		}
	}
	[NodeCategory("Flow/Events")]
	public class OnActivated : ProxyVoidNode<FrooxEngineContext, OnActivated.Proxy>
	{
		public class Proxy : ProtoFluxEngineProxy
		{
			public Action Activated;

			public bool ActivatedScheduled;

			protected override void OnActivated()
			{
				base.OnActivated();
				Activated?.Invoke();
				ActivatedScheduled = true;
			}

			protected override void InitializeSyncMembers()
			{
				base.InitializeSyncMembers();
			}

			public override ISyncMember GetSyncMember(int index)
			{
				return index switch
				{
					0 => persistent, 
					1 => updateOrder, 
					2 => EnabledField, 
					3 => Node, 
					4 => Path, 
					_ => throw new ArgumentOutOfRangeException(), 
				};
			}

			public static Proxy __New()
			{
				return new Proxy();
			}
		}

		public Call Trigger;

		public ValueInput<bool> OnlyHost;

		protected override void ProxyAdded(Proxy proxy, FrooxEngineContext context)
		{
			NodeContextPath path = context.CaptureContextPath();
			context.GetEventDispatcher(out var dispatcher);
			Action action = (proxy.Activated = delegate
			{
				dispatcher.ScheduleEvent(path, HandleEvent, null);
			});
			if (proxy.ActivatedScheduled)
			{
				proxy.ActivatedScheduled = false;
				action();
			}
		}

		protected override void ProxyRemoved(Proxy proxy, FrooxEngineContext context, bool inUseByAnotherInstance)
		{
			if (!inUseByAnotherInstance)
			{
				proxy.Activated = null;
			}
		}

		private void HandleEvent(FrooxEngineContext context, object eventData)
		{
			Proxy proxy = GetProxy(context);
			if (proxy != null && !proxy.IsRemoved)
			{
				proxy.ActivatedScheduled = false;
				if (!OnlyHost.Evaluate(context, defaultValue: false) || context.LocalUser.IsHost)
				{
					Trigger.Execute(context);
				}
			}
		}
	}
	[NodeCategory("Flow/Events")]
	public class OnDeactivated : ProxyVoidNode<FrooxEngineContext, OnDeactivated.Proxy>
	{
		public class Proxy : ProtoFluxEngineProxy
		{
			public Action Deactivated;

			public bool DeactivatedScheduled;

			protected override void OnDeactivated()
			{
				base.OnDeactivated();
				Deactivated?.Invoke();
				DeactivatedScheduled = true;
			}

			protected override void InitializeSyncMembers()
			{
				base.InitializeSyncMembers();
			}

			public override ISyncMember GetSyncMember(int index)
			{
				return index switch
				{
					0 => persistent, 
					1 => updateOrder, 
					2 => EnabledField, 
					3 => Node, 
					4 => Path, 
					_ => throw new ArgumentOutOfRangeException(), 
				};
			}

			public static Proxy __New()
			{
				return new Proxy();
			}
		}

		public Call Trigger;

		public ValueInput<bool> OnlyHost;

		protected override void ProxyAdded(Proxy proxy, FrooxEngineContext context)
		{
			NodeContextPath path = context.CaptureContextPath();
			context.GetEventDispatcher(out var dispatcher);
			Action action = (proxy.Deactivated = delegate
			{
				dispatcher.ScheduleEvent(path, HandleEvent, null);
			});
			if (proxy.DeactivatedScheduled)
			{
				proxy.DeactivatedScheduled = false;
				action();
			}
		}

		protected override void ProxyRemoved(Proxy proxy, FrooxEngineContext context, bool inUseByAnotherInstance)
		{
			if (!inUseByAnotherInstance)
			{
				proxy.Deactivated = null;
			}
		}

		private void HandleEvent(FrooxEngineContext context, object eventData)
		{
			Proxy proxy = GetProxy(context);
			if (proxy != null && !proxy.IsRemoved)
			{
				proxy.DeactivatedScheduled = false;
				if (!OnlyHost.Evaluate(context, defaultValue: false) || context.LocalUser.IsHost)
				{
					Trigger.Execute(context);
				}
			}
		}
	}
	[NodeCategory("Flow/Events")]
	public class OnDestroy : ProxyVoidNode<FrooxEngineContext, OnDestroy.Proxy>
	{
		public class Proxy : ProtoFluxEngineProxy
		{
			public new Action Destroy;

			protected override void OnDestroy()
			{
				base.OnDestroy();
				Destroy?.Invoke();
			}

			protected override void InitializeSyncMembers()
			{
				base.InitializeSyncMembers();
			}

			public override ISyncMember GetSyncMember(int index)
			{
				return index switch
				{
					0 => persistent, 
					1 => updateOrder, 
					2 => EnabledField, 
					3 => Node, 
					4 => Path, 
					_ => throw new ArgumentOutOfRangeException(), 
				};
			}

			public static Proxy __New()
			{
				return new Proxy();
			}
		}

		public Call Trigger;

		public ValueInput<bool> OnlyHost;

		protected override void ProxyAdded(Proxy proxy, FrooxEngineContext context)
		{
			NodeContextPath path = context.CaptureContextPath();
			ProtoFluxNodeGroup group = context.Group;
			Action destroy = delegate
			{
				group.ExecuteImmediatelly(path, delegate(FrooxEngineContext c)
				{
					if (!OnlyHost.Evaluate(c, defaultValue: false) || c.LocalUser.IsHost)
					{
						Trigger.Execute(c);
					}
				});
			};
			proxy.Destroy = destroy;
		}

		protected override void ProxyRemoved(Proxy proxy, FrooxEngineContext context, bool inUseByAnotherInstance)
		{
			if (!inUseByAnotherInstance)
			{
				proxy.Destroy = null;
			}
		}
	}
	[NodeCategory("Flow/Events")]
	public class OnDestroying : ProxyVoidNode<FrooxEngineContext, OnDestroying.Proxy>
	{
		public class Proxy : ProtoFluxEngineProxy
		{
			public Action Destroying;

			protected override void OnDestroying()
			{
				base.OnDestroying();
				Destroying?.Invoke();
			}

			protected override void InitializeSyncMembers()
			{
				base.InitializeSyncMembers();
			}

			public override ISyncMember GetSyncMember(int index)
			{
				return index switch
				{
					0 => persistent, 
					1 => updateOrder, 
					2 => EnabledField, 
					3 => Node, 
					4 => Path, 
					_ => throw new ArgumentOutOfRangeException(), 
				};
			}

			public static Proxy __New()
			{
				return new Proxy();
			}
		}

		public Call Trigger;

		protected override void ProxyAdded(Proxy proxy, FrooxEngineContext context)
		{
			NodeContextPath path = context.CaptureContextPath();
			ProtoFluxNodeGroup group = context.Group;
			Action destroying = delegate
			{
				group.ExecuteImmediatelly(path, delegate(FrooxEngineContext c)
				{
					Trigger.Execute(c);
				});
			};
			proxy.Destroying = destroying;
		}

		protected override void ProxyRemoved(Proxy proxy, FrooxEngineContext context, bool inUseByAnotherInstance)
		{
			if (!inUseByAnotherInstance)
			{
				proxy.Destroying = null;
			}
		}
	}
	[NodeCategory("Flow/Events")]
	public class OnSaving : ProxyVoidNode<FrooxEngineContext, OnSaving.Proxy>
	{
		public class Proxy : ProtoFluxEngineProxy
		{
			public Action Saving;

			protected override void OnSaving(SaveControl control)
			{
				control.OnBeforeSaveStart(Saving);
			}

			protected override void InitializeSyncMembers()
			{
				base.InitializeSyncMembers();
			}

			public override ISyncMember GetSyncMember(int index)
			{
				return index switch
				{
					0 => persistent, 
					1 => updateOrder, 
					2 => EnabledField, 
					3 => Node, 
					4 => Path, 
					_ => throw new ArgumentOutOfRangeException(), 
				};
			}

			public static Proxy __New()
			{
				return new Proxy();
			}
		}

		public Call Trigger;

		protected override void ProxyAdded(Proxy proxy, FrooxEngineContext context)
		{
			NodeContextPath path = context.CaptureContextPath();
			ProtoFluxNodeGroup group = context.Group;
			Action saving = delegate
			{
				group.ExecuteImmediatelly(path, delegate(FrooxEngineContext c)
				{
					Trigger.Execute(c);
				});
			};
			proxy.Saving = saving;
		}

		protected override void ProxyRemoved(Proxy proxy, FrooxEngineContext context, bool inUseByAnotherInstance)
		{
			if (!inUseByAnotherInstance)
			{
				proxy.Saving = null;
			}
		}
	}
	[NodeCategory("Flow/Events")]
	public class OnLoaded : ProxyVoidNode<FrooxEngineContext, OnLoaded.Proxy>
	{
		public class Proxy : ProtoFluxEngineProxy
		{
			public Action Loaded;

			public bool LoadedScheduled;

			protected override void OnLoading(DataTreeNode node, LoadControl control)
			{
				Loaded?.Invoke();
				LoadedScheduled = true;
			}

			protected override void InitializeSyncMembers()
			{
				base.InitializeSyncMembers();
			}

			public override ISyncMember GetSyncMember(int index)
			{
				return index switch
				{
					0 => persistent, 
					1 => updateOrder, 
					2 => EnabledField, 
					3 => Node, 
					4 => Path, 
					_ => throw new ArgumentOutOfRangeException(), 
				};
			}

			public static Proxy __New()
			{
				return new Proxy();
			}
		}

		public Call Trigger;

		protected override void ProxyAdded(Proxy proxy, FrooxEngineContext context)
		{
			NodeContextPath path = context.CaptureContextPath();
			context.GetEventDispatcher(out var dispatcher);
			Action action = (proxy.Loaded = delegate
			{
				dispatcher.ScheduleEvent(path, HandleEvent, null);
			});
			if (proxy.LoadedScheduled)
			{
				proxy.LoadedScheduled = false;
				action();
			}
		}

		protected override void ProxyRemoved(Proxy proxy, FrooxEngineContext context, bool inUseByAnotherInstance)
		{
			if (!inUseByAnotherInstance)
			{
				proxy.Loaded = null;
			}
		}

		private void HandleEvent(FrooxEngineContext context, object eventData)
		{
			Proxy proxy = GetProxy(context);
			if (proxy != null)
			{
				proxy.LoadedScheduled = false;
				Trigger.Execute(context);
			}
		}
	}
	[NodeCategory("Flow/Events")]
	public class OnPackageImported : ProxyVoidNode<FrooxEngineContext, OnPackageImported.Proxy>
	{
		public class Proxy : ProtoFluxEngineProxy, IPackageImportEventReceiver, IWorker, IWorldElement
		{
			public Action Imported;

			public bool ImportedScheduled;

			public void OnPackageImported()
			{
				Imported?.Invoke();
				ImportedScheduled = true;
			}

			protected override void InitializeSyncMembers()
			{
				base.InitializeSyncMembers();
			}

			public override ISyncMember GetSyncMember(int index)
			{
				return index switch
				{
					0 => persistent, 
					1 => updateOrder, 
					2 => EnabledField, 
					3 => Node, 
					4 => Path, 
					_ => throw new ArgumentOutOfRangeException(), 
				};
			}

			public static Proxy __New()
			{
				return new Proxy();
			}
		}

		public Call Trigger;

		protected override void ProxyAdded(Proxy proxy, FrooxEngineContext context)
		{
			NodeContextPath path = context.CaptureContextPath();
			context.GetEventDispatcher(out var dispatcher);
			Action action = (proxy.Imported = delegate
			{
				dispatcher.ScheduleEvent(path, HandleEvent, null);
			});
			if (proxy.ImportedScheduled)
			{
				proxy.ImportedScheduled = false;
				action();
			}
		}

		protected override void ProxyRemoved(Proxy proxy, FrooxEngineContext context, bool inUseByAnotherInstance)
		{
			if (proxy != null && !inUseByAnotherInstance)
			{
				proxy.Imported = null;
			}
		}

		private void HandleEvent(FrooxEngineContext context, object eventData)
		{
			Proxy proxy = GetProxy(context);
			if (proxy != null)
			{
				proxy.ImportedScheduled = false;
				Trigger.Execute(context);
			}
		}
	}
	[NodeCategory("Slots")]
	public class SlotChildrenEvents : VoidNode<FrooxEngineContext>
	{
		public readonly GlobalRef<Slot> Instance;

		public ObjectInput<global::FrooxEngine.User> OnUser;

		public Call OnChildAdded;

		public Call OnChildRemoved;

		public readonly ObjectOutput<Slot> Child;

		private ObjectStore<Slot> _currentSlot;

		private ObjectStore<SlotChildEvent> _currentOnAdded;

		private ObjectStore<SlotChildEvent> _currentOnRemoved;

		private HashSet<NodeContextPath> _currentlyFiring = new HashSet<NodeContextPath>();

		public override bool CanBeEvaluated => false;

		private void OnInstanceChanged(Slot target, FrooxEngineContext context)
		{
			Slot slot = _currentSlot.Read(context);
			if (slot == target)
			{
				return;
			}
			if (slot != null)
			{
				slot.ChildAdded -= _currentOnAdded.Read(context);
				slot.ChildRemoved -= _currentOnRemoved.Read(context);
			}
			if (target != null)
			{
				NodeContextPath contextPath = context.CaptureContextPath();
				context.GetEventDispatcher(out var dispatcher);
				SlotChildEvent value = delegate(Slot slot2, Slot child)
				{
					lock (_currentlyFiring)
					{
						if (_currentlyFiring.Contains(contextPath))
						{
							return;
						}
					}
					bool _canMakeSynchronousChanges = slot2.World.CanMakeSynchronousChanges;
					dispatcher.ScheduleEvent(contextPath, delegate(FrooxEngineContext c)
					{
						ChildAdded(child, c, _canMakeSynchronousChanges);
					});
				};
				SlotChildEvent value2 = delegate(Slot slot2, Slot child)
				{
					lock (_currentlyFiring)
					{
						if (_currentlyFiring.Contains(contextPath))
						{
							return;
						}
					}
					bool _canMakeSynchronousChanges = slot2.World.CanMakeSynchronousChanges;
					dispatcher.ScheduleEvent(contextPath, delegate(FrooxEngineContext c)
					{
						ChildRemoved(child, c, _canMakeSynchronousChanges);
					});
				};
				target.ChildAdded += value;
				target.ChildRemoved += value2;
				_currentSlot.Write(target, context);
				_currentOnAdded.Write(value, context);
				_currentOnRemoved.Write(value2, context);
			}
			else
			{
				_currentSlot.Clear(context);
				_currentOnAdded.Clear(context);
				_currentOnRemoved.Clear(context);
			}
		}

		private void ChildAdded(Slot child, FrooxEngineContext context, bool canMakeSynchronousChanges)
		{
			if (!ShouldUserHandleEvent(context, canMakeSynchronousChanges))
			{
				return;
			}
			NodeContextPath item = context.CaptureContextPath();
			try
			{
				lock (_currentlyFiring)
				{
					_currentlyFiring.Add(item);
				}
				Child.Write(child, context);
				OnChildAdded.Execute(context);
			}
			finally
			{
				lock (_currentlyFiring)
				{
					_currentlyFiring.Remove(item);
				}
			}
		}

		private void ChildRemoved(Slot child, FrooxEngineContext context, bool canMakeSynchronousChanges)
		{
			if (!ShouldUserHandleEvent(context, canMakeSynchronousChanges))
			{
				return;
			}
			NodeContextPath item = context.CaptureContextPath();
			try
			{
				lock (_currentlyFiring)
				{
					_currentlyFiring.Add(item);
				}
				Child.Write(child, context);
				OnChildRemoved.Execute(context);
			}
			finally
			{
				lock (_currentlyFiring)
				{
					_currentlyFiring.Remove(item);
				}
			}
		}

		private bool ShouldUserHandleEvent(FrooxEngineContext context, bool canMakeSynchronousChanges)
		{
			return OnUser.Evaluate(context)?.IsLocalUser ?? canMakeSynchronousChanges;
		}

		public SlotChildrenEvents()
		{
			Instance = new GlobalRef<Slot>(this, 0);
			Child = new ObjectOutput<Slot>(this);
		}
	}
	[NodeCategory("Slots")]
	public class RootSlot : ObjectFunctionNode<FrooxEngineContext, Slot>
	{
		protected override Slot Compute(FrooxEngineContext context)
		{
			return context.World.RootSlot;
		}
	}
	[NodeCategory("Slots")]
	public class GetSlot : ObjectFunctionNode<FrooxEngineContext, Slot>
	{
		public ObjectArgument<global::FrooxEngine.IComponent> Component;

		protected override Slot Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.IComponent>(context)?.Slot;
		}
	}
	[ContinuouslyChanging]
	[NodeCategory("Slots")]
	public class GetObjectRoot : ObjectFunctionNode<FrooxEngineContext, Slot>
	{
		public ObjectArgument<Slot> Instance;

		public ValueArgument<bool> OnlyExplicit;

		protected override Slot Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<Slot>(context)?.GetObjectRoot(1.ReadValue<bool>(context));
		}
	}
	[ContinuouslyChanging]
	[NodeCategory("Slots")]
	public class GetParentSlot : ObjectFunctionNode<FrooxEngineContext, Slot>
	{
		public ObjectArgument<Slot> Instance;

		protected override Slot Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<Slot>(context)?.Parent;
		}
	}
	[NodeCategory("Slots/Info")]
	public class GetSlotName : ObjectFunctionNode<FrooxEngineContext, string>
	{
		public ObjectArgument<Slot> Instance;

		protected override string Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<Slot>(context)?.Name;
		}
	}
	[NodeCategory("Slots/Info")]
	public class GetSlotActive : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<Slot> Instance;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<Slot>(context)?.IsActive ?? false;
		}
	}
	[NodeCategory("Slots/Info")]
	public class GetSlotActiveSelf : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<Slot> Instance;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<Slot>(context)?.ActiveSelf ?? false;
		}
	}
	[NodeCategory("Slots/Info")]
	public class GetSlotPersistent : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<Slot> Instance;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<Slot>(context)?.IsPersistent ?? false;
		}
	}
	[NodeCategory("Slots/Info")]
	public class GetSlotPersistentSelf : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<Slot> Instance;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<Slot>(context)?.PersistentSelf ?? false;
		}
	}
	[NodeCategory("Slots/Info")]
	public class GetTag : ObjectFunctionNode<FrooxEngineContext, string>
	{
		public ObjectArgument<Slot> Instance;

		protected override string Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<Slot>(context)?.Tag;
		}
	}
	[NodeCategory("Slots/Info")]
	public class HasTag : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<Slot> Instance;

		public ObjectArgument<string> Tag;

		protected override bool Compute(FrooxEngineContext context)
		{
			Slot slot = 0.ReadObject<Slot>(context);
			if (slot == null)
			{
				return false;
			}
			string text = 1.ReadObject<string>(context);
			if (string.IsNullOrEmpty(text))
			{
				return string.IsNullOrEmpty(slot.Tag);
			}
			return slot.Tag == text;
		}
	}
	[NodeCategory("Slots")]
	[ContinuouslyChanging]
	public class ChildrenCount : ValueFunctionNode<FrooxEngineContext, int>
	{
		public ObjectArgument<Slot> Instance;

		protected override int Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<Slot>(context)?.ChildrenCount ?? 0;
		}
	}
	[NodeCategory("Slots")]
	[ContinuouslyChanging]
	public class GetChild : ObjectFunctionNode<FrooxEngineContext, Slot>
	{
		public ObjectArgument<Slot> Instance;

		public ValueArgument<int> ChildIndex;

		protected override Slot Compute(FrooxEngineContext context)
		{
			Slot slot = 0.ReadObject<Slot>(context);
			if (slot == null)
			{
				return null;
			}
			int num = 1.ReadValue<int>(context);
			if (num < 0 || num >= slot.ChildrenCount)
			{
				return null;
			}
			return slot[num];
		}
	}
	[NodeCategory("Slots")]
	[ContinuouslyChanging]
	public class IndexOfChild : ValueFunctionNode<FrooxEngineContext, int>
	{
		public ObjectArgument<Slot> Instance;

		protected override int Compute(FrooxEngineContext context)
		{
			Slot slot = 0.ReadObject<Slot>(context);
			if (slot == null || slot.IsRootSlot)
			{
				return -1;
			}
			return slot.ChildIndex;
		}
	}
	[NodeCategory("Users/User Root")]
	[NodeCategory("Slots")]
	public class GetActiveUserRoot : ObjectFunctionNode<FrooxEngineContext, UserRoot>
	{
		public ObjectArgument<Slot> Instance;

		protected override UserRoot Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<Slot>(context)?.ActiveUserRoot;
		}
	}
	[NodeCategory("Users")]
	[NodeCategory("Slots")]
	public class GetActiveUser : ObjectFunctionNode<FrooxEngineContext, global::FrooxEngine.User>
	{
		public ObjectArgument<Slot> Instance;

		protected override global::FrooxEngine.User Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<Slot>(context)?.ActiveUser;
		}
	}
	[NodeCategory("Slots")]
	public class GetSlotOrderOffset : ValueFunctionNode<FrooxEngineContext, long>
	{
		public ObjectArgument<Slot> Instance;

		protected override long Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<Slot>(context)?.OrderOffset ?? 0;
		}
	}
	[NodeCategory("Users")]
	[NodeCategory("Slots")]
	[ChangeSource]
	public class GetActiveUserSelf : ProxyObjectFunctionNode<FrooxEngineContext, GetActiveUserSelf.Proxy, global::FrooxEngine.User>
	{
		public class Proxy : ProtoFluxEngineProxy
		{
			public new Action<Slot> Changed;

			protected override void OnAwake()
			{
				base.OnAwake();
				base.Slot.ActiveUserRootChanged += OnChanged;
			}

			protected override void OnDispose()
			{
				base.OnDispose();
				base.Slot.ActiveUserRootChanged -= OnChanged;
			}

			private void OnChanged(Slot slot)
			{
				Changed?.Invoke(slot);
			}

			protected override void InitializeSyncMembers()
			{
				base.InitializeSyncMembers();
			}

			public override ISyncMember GetSyncMember(int index)
			{
				return index switch
				{
					0 => persistent, 
					1 => updateOrder, 
					2 => EnabledField, 
					3 => Node, 
					4 => Path, 
					_ => throw new ArgumentOutOfRangeException(), 
				};
			}

			public static Proxy __New()
			{
				return new Proxy();
			}
		}

		protected override void ProxyAdded(Proxy proxy, FrooxEngineContext context)
		{
			NodeContextPath path = context.CaptureContextPath();
			ElementPath<IOutput> element = new ElementPath<IOutput>(this, path);
			ExecutionChangesDispatcher<FrooxEngineContext> changes = context.Changes;
			proxy.Changed = delegate
			{
				changes.OutputChanged(element);
			};
		}

		protected override void ProxyRemoved(Proxy proxy, FrooxEngineContext context, bool inUseByAnotherInstance)
		{
			if (!inUseByAnotherInstance)
			{
				proxy.Changed = null;
			}
		}

		protected override global::FrooxEngine.User Compute(FrooxEngineContext context)
		{
			return GetProxy(context)?.Slot.ActiveUserRoot?.ActiveUser;
		}
	}
	[NodeCategory("Slots")]
	public class IsChildOf : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<Slot> Instance;

		public ObjectArgument<Slot> Other;

		public ValueArgument<bool> IncludeSelf;

		protected override bool Compute(FrooxEngineContext context)
		{
			Slot slot = 0.ReadObject<Slot>(context);
			if (slot == null)
			{
				return false;
			}
			Slot slot2 = 1.ReadObject<Slot>(context);
			if (slot2 == null)
			{
				return false;
			}
			return slot.IsChildOf(slot2, 2.ReadValue<bool>(context));
		}
	}
	[NodeCategory("Slots/Searching")]
	public class FindChildByName : ObjectFunctionNode<FrooxEngineContext, Slot>
	{
		public ObjectArgument<Slot> Instance;

		public ObjectArgument<string> Name;

		[ProtoFlux.Core.DefaultValue(true)]
		public ValueArgument<bool> MatchSubstring;

		public ValueArgument<bool> IgnoreCase;

		public ValueArgument<int> SearchDepth;

		protected override Slot Compute(FrooxEngineContext context)
		{
			Slot slot = 0.ReadObject<Slot>(context);
			if (slot == null)
			{
				return null;
			}
			string name = 1.ReadObject<string>(context);
			return slot.FindChild(name, 2.ReadValue<bool>(context), 3.ReadValue<bool>(context), 4.ReadValue<int>(context));
		}
	}
	[NodeCategory("Slots/Searching")]
	public class FindChildByTag : ObjectFunctionNode<FrooxEngineContext, Slot>
	{
		public ObjectArgument<Slot> Instance;

		public ObjectArgument<string> Tag;

		[ProtoFlux.Core.DefaultValue(-1)]
		public ValueArgument<int> SearchDepth;

		protected override Slot Compute(FrooxEngineContext context)
		{
			Slot slot = 0.ReadObject<Slot>(context);
			if (slot == null)
			{
				return null;
			}
			string tag = 1.ReadObject<string>(context);
			int maxDepth = 2.ReadValue<int>(context);
			return slot.FindChild((Slot s) => s.Tag == tag, maxDepth);
		}
	}
	[NodeCategory("Slots/Searching")]
	public class FindParentByName : ObjectFunctionNode<FrooxEngineContext, Slot>
	{
		public ObjectArgument<Slot> Instance;

		public ObjectArgument<string> Name;

		[ProtoFlux.Core.DefaultValue(true)]
		public ValueArgument<bool> MatchSubstring;

		public ValueArgument<bool> IgnoreCase;

		public ValueArgument<int> SearchDepth;

		protected override Slot Compute(FrooxEngineContext context)
		{
			Slot slot = 0.ReadObject<Slot>(context);
			if (slot == null)
			{
				return null;
			}
			string name = 1.ReadObject<string>(context);
			return slot.FindParent(name, 2.ReadValue<bool>(context), 3.ReadValue<bool>(context), 4.ReadValue<int>(context));
		}
	}
	[NodeCategory("Slots/Searching")]
	public class FindParentByTag : ObjectFunctionNode<FrooxEngineContext, Slot>
	{
		public ObjectArgument<Slot> Instance;

		public ObjectArgument<string> Tag;

		[ProtoFlux.Core.DefaultValue(-1)]
		public ValueArgument<int> SearchDepth;

		protected override Slot Compute(FrooxEngineContext context)
		{
			Slot slot = 0.ReadObject<Slot>(context);
			if (slot == null)
			{
				return null;
			}
			string tag = 1.ReadObject<string>(context);
			int maxDepth = 2.ReadValue<int>(context);
			return slot.FindParent((Slot s) => s.Tag == tag, maxDepth);
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.References
{
	[NodeCategory("References")]
	public class ReferenceTarget<T> : ObjectFunctionNode<FrooxEngineContext, T> where T : class, IWorldElement
	{
		public ObjectArgument<SyncRef<T>> Reference;

		protected override T Compute(FrooxEngineContext context)
		{
			SyncRef<T> syncRef = 0.ReadObject<SyncRef<T>>(context);
			if (syncRef == null)
			{
				return null;
			}
			return syncRef.Target;
		}
	}
	[NodeCategory("References")]
	public class ReferenceID : ValueFunctionNode<FrooxEngineContext, RefID>
	{
		public ObjectArgument<IWorldElement> Element;

		protected override RefID Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<IWorldElement>(context)?.ReferenceID ?? RefID.Null;
		}
	}
	[NodeCategory("References")]
	public class AllocatingUser : ObjectFunctionNode<FrooxEngineContext, global::FrooxEngine.User>
	{
		public ObjectArgument<IWorldElement> Element;

		protected override global::FrooxEngine.User Compute(FrooxEngineContext context)
		{
			IWorldElement worldElement = 0.ReadObject<IWorldElement>(context).FilterWorldElement();
			if (worldElement == null)
			{
				return null;
			}
			worldElement.ReferenceID.ExtractIDs(out var position, out var user);
			global::FrooxEngine.User userByAllocationID = worldElement.World.GetUserByAllocationID(user);
			if (userByAllocationID == null)
			{
				return null;
			}
			if (position < userByAllocationID.AllocationIDStart)
			{
				return null;
			}
			return userByAllocationID;
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Playback
{
	[NodeCategory("Media")]
	public abstract class PlaybackAction : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<IPlayable> Target;

		protected override bool Do(FrooxEngineContext context)
		{
			IPlayable playable = Target.Evaluate(context);
			if (playable == null)
			{
				return false;
			}
			Perform(playable, context);
			return true;
		}

		protected abstract void Perform(IPlayable playable, FrooxEngineContext context);
	}
	public class Play : PlaybackAction
	{
		protected override void Perform(IPlayable playable, FrooxEngineContext context)
		{
			playable.Play();
		}
	}
	public class Pause : PlaybackAction
	{
		protected override void Perform(IPlayable playable, FrooxEngineContext context)
		{
			playable.Pause();
		}
	}
	public class Stop : PlaybackAction
	{
		protected override void Perform(IPlayable playable, FrooxEngineContext context)
		{
			playable.Stop();
		}
	}
	public class Resume : PlaybackAction
	{
		protected override void Perform(IPlayable playable, FrooxEngineContext context)
		{
			playable.Resume();
		}
	}
	public class Toggle : PlaybackAction
	{
		protected override void Perform(IPlayable playable, FrooxEngineContext context)
		{
			if (playable.IsPlaying)
			{
				playable.Pause();
			}
			else
			{
				playable.Resume();
			}
		}
	}
	[NodeCategory("Media")]
	public class Wait : AsyncActionNode<FrooxEngineContext>
	{
		public ObjectInput<IPlayable> Target;

		public AsyncCall OnWaitBegin;

		public Continuation OnPlaybackFinished;

		protected override async Task<IOperation> RunAsync(FrooxEngineContext context)
		{
			IPlayable target = Target.Evaluate(context);
			if (target == null)
			{
				return null;
			}
			await OnWaitBegin.ExecuteAsync(context);
			while (target.IsPlaying)
			{
				await default(NextUpdate);
			}
			return OnPlaybackFinished.Target;
		}
	}
	[NodeCategory("Media")]
	public class PlayAndWait : AsyncActionNode<FrooxEngineContext>
	{
		public ObjectInput<IPlayable> Target;

		public AsyncCall OnStarted;

		public Continuation OnPlaybackFinished;

		protected override async Task<IOperation> RunAsync(FrooxEngineContext context)
		{
			IPlayable target = Target.Evaluate(context);
			if (target == null)
			{
				return null;
			}
			target.Play();
			await OnStarted.ExecuteAsync(context);
			while (target.IsPlaying)
			{
				await default(NextUpdate);
			}
			return OnPlaybackFinished.Target;
		}
	}
	[NodeCategory("Media")]
	public class PlaybackState : VoidNode<FrooxEngineContext>
	{
		public ObjectArgument<IPlayable> Source;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> IsPlaying;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> Loop;

		[ContinuouslyChanging]
		public readonly ValueOutput<float> Position;

		[ContinuouslyChanging]
		public readonly ValueOutput<float> NormalizedPosition;

		[ContinuouslyChanging]
		public readonly ValueOutput<float> ClipLength;

		[ContinuouslyChanging]
		public readonly ValueOutput<float> Speed;

		protected override void ComputeOutputs(FrooxEngineContext context)
		{
			IPlayable playable = 0.ReadObject<IPlayable>(context);
			if (playable != null)
			{
				IsPlaying.Write(playable.IsPlaying, context);
				Loop.Write(playable.IsPlaying, context);
				Position.Write(playable.Position, context);
				NormalizedPosition.Write(playable.NormalizedPosition, context);
				ClipLength.Write((float)playable.ClipLength, context);
				Speed.Write(playable.Speed, context);
			}
			else
			{
				IsPlaying.Write(value: false, context);
				Loop.Write(value: false, context);
				Position.Write(0f, context);
				NormalizedPosition.Write(0f, context);
				ClipLength.Write(0f, context);
				Speed.Write(0f, context);
			}
		}

		public PlaybackState()
		{
			IsPlaying = new ValueOutput<bool>(this);
			Loop = new ValueOutput<bool>(this);
			Position = new ValueOutput<float>(this);
			NormalizedPosition = new ValueOutput<float>(this);
			ClipLength = new ValueOutput<float>(this);
			Speed = new ValueOutput<float>(this);
		}
	}
	[NodeCategory("Media")]
	[ContinuouslyChanging]
	public abstract class PlaybackProperty<T> : ValueFunctionNode<FrooxEngineContext, T> where T : unmanaged
	{
		public ObjectArgument<IPlayable> Source;

		protected override T Compute(FrooxEngineContext context)
		{
			IPlayable playable = 0.ReadObject<IPlayable>(context);
			if (playable == null)
			{
				return default(T);
			}
			return GetProperty(playable);
		}

		protected abstract T GetProperty(IPlayable playable);
	}
	public class IsPlaying : PlaybackProperty<bool>
	{
		protected override bool GetProperty(IPlayable playable)
		{
			return playable.IsPlaying;
		}
	}
	public class IsLooped : PlaybackProperty<bool>
	{
		protected override bool GetProperty(IPlayable playable)
		{
			return playable.Loop;
		}
	}
	public class Position : PlaybackProperty<float>
	{
		protected override float GetProperty(IPlayable playable)
		{
			return playable.Position;
		}
	}
	public class NormalizedPosition : PlaybackProperty<float>
	{
		protected override float GetProperty(IPlayable playable)
		{
			return playable.NormalizedPosition;
		}
	}
	public class Speed : PlaybackProperty<float>
	{
		protected override float GetProperty(IPlayable playable)
		{
			return playable.Speed;
		}
	}
	[NodeOverload("Engine.Playback.ClipLength")]
	public class ClipLengthFloat : PlaybackProperty<float>
	{
		protected override float GetProperty(IPlayable playable)
		{
			return (float)playable.ClipLength;
		}
	}
	[NodeOverload("Engine.Playback.ClipLength")]
	public class ClipLengthDouble : PlaybackProperty<double>
	{
		protected override double GetProperty(IPlayable playable)
		{
			return playable.ClipLength;
		}
	}
	public class SetLoop : PlaybackAction
	{
		public ValueInput<bool> Loop;

		protected override void Perform(IPlayable playable, FrooxEngineContext context)
		{
			playable.Loop = Loop.Evaluate(context, defaultValue: false);
		}
	}
	public class SetPosition : PlaybackAction
	{
		public ValueInput<float> Position;

		protected override void Perform(IPlayable playable, FrooxEngineContext context)
		{
			playable.Position = Position.Evaluate(context, 0f);
		}
	}
	public class ShiftPosition : PlaybackAction
	{
		public ValueInput<float> Delta;

		protected override void Perform(IPlayable playable, FrooxEngineContext context)
		{
			playable.Position += Delta.Evaluate(context, 0f);
		}
	}
	public class SetNormalizedPosition : PlaybackAction
	{
		public ValueInput<float> NormalizedPosition;

		protected override void Perform(IPlayable playable, FrooxEngineContext context)
		{
			playable.NormalizedPosition = NormalizedPosition.Evaluate(context, 0f);
		}
	}
	public class SetSpeed : PlaybackAction
	{
		public ValueInput<float> Speed;

		protected override void Perform(IPlayable playable, FrooxEngineContext context)
		{
			playable.Speed = Speed.Evaluate(context, 0f);
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Time
{
	[ContinuouslyChanging]
	[NodeCategory("Time")]
	[NodeName("Elapsed Time", false)]
	[NodeOverload("Engine.ElapsedTime")]
	public abstract class ElapsedTime<T> : ProxyValueFunctionNode<FrooxEngineContext, ElapsedTime<T>.Proxy, T> where T : unmanaged
	{
		public class Proxy : ProtoFluxEngineProxy
		{
			public readonly SyncTime StartTime;

			protected override void InitializeSyncMembers()
			{
				base.InitializeSyncMembers();
				StartTime = new SyncTime();
			}

			public override ISyncMember GetSyncMember(int index)
			{
				return index switch
				{
					0 => persistent, 
					1 => updateOrder, 
					2 => EnabledField, 
					3 => Node, 
					4 => Path, 
					5 => StartTime, 
					_ => throw new ArgumentOutOfRangeException(), 
				};
			}

			public static Proxy __New()
			{
				return new Proxy();
			}
		}

		public readonly Operation Reset;

		public Continuation OnReset;

		private IOperation DoReset(FrooxEngineContext context)
		{
			Proxy proxy = GetProxy(context);
			if (proxy == null)
			{
				return null;
			}
			proxy.StartTime.SetNow();
			return OnReset.Target;
		}

		protected ElapsedTime()
		{
			((ElapsedTime<>)(object)this).Reset = new Operation(this, 0);
		}
	}
	public class ElapsedTimeFloat : ElapsedTime<float>
	{
		protected override float Compute(FrooxEngineContext context)
		{
			return (float)(GetProxy(context)?.StartTime.CurrentTime ?? 0.0);
		}
	}
	public class ElapsedTimeDouble : ElapsedTime<double>
	{
		protected override double Compute(FrooxEngineContext context)
		{
			return GetProxy(context)?.StartTime.CurrentTime ?? 0.0;
		}
	}
	public class ElapsedTimeTimeSpan : ElapsedTime<TimeSpan>
	{
		protected override TimeSpan Compute(FrooxEngineContext context)
		{
			return TimeSpan.FromSeconds(GetProxy(context)?.StartTime.CurrentTime ?? 0.0);
		}
	}
	[NodeCategory("Time")]
	public class Stopwatch : ProxyVoidNode<FrooxEngineContext, Stopwatch.Proxy>
	{
		public class Proxy : ProtoFluxEngineProxy
		{
			public readonly SyncPlayback Stopwatch;

			protected override void OnAwake()
			{
				base.OnAwake();
				Stopwatch.ClipLength = double.MaxValue;
			}

			protected override void InitializeSyncMembers()
			{
				base.InitializeSyncMembers();
				Stopwatch = new SyncPlayback();
			}

			public override ISyncMember GetSyncMember(int index)
			{
				return index switch
				{
					0 => persistent, 
					1 => updateOrder, 
					2 => EnabledField, 
					3 => Node, 
					4 => Path, 
					5 => Stopwatch, 
					_ => throw new ArgumentOutOfRangeException(), 
				};
			}

			public static Proxy __New()
			{
				return new Proxy();
			}
		}

		[ContinuouslyChanging]
		public readonly ValueOutput<float> Time;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> IsRunning;

		[PossibleContinuations(new string[] { "OnStart" })]
		public readonly Operation Start;

		[PossibleContinuations(new string[] { "OnStop" })]
		public readonly Operation Stop;

		[PossibleContinuations(new string[] { "OnReset" })]
		public readonly Operation Reset;

		public Continuation OnStart;

		public Continuation OnStop;

		public Continuation OnReset;

		private IOperation DoStart(FrooxEngineContext context)
		{
			Proxy proxy = GetProxy(context);
			if (proxy == null)
			{
				return null;
			}
			proxy.Stopwatch.Resume();
			return OnStart.Target;
		}

		private IOperation DoStop(FrooxEngineContext context)
		{
			Proxy proxy = GetProxy(context);
			if (proxy == null)
			{
				return null;
			}
			proxy.Stopwatch.Pause();
			return OnStop.Target;
		}

		private IOperation DoReset(FrooxEngineContext context)
		{
			Proxy proxy = GetProxy(context);
			if (proxy == null)
			{
				return null;
			}
			proxy.Stopwatch.Position = 0f;
			return OnReset.Target;
		}

		protected override void ComputeOutputs(FrooxEngineContext context)
		{
			SyncPlayback syncPlayback = GetProxy(context)?.Stopwatch;
			Time.Write(syncPlayback?.Position ?? 0f, context);
			IsRunning.Write(syncPlayback?.IsPlaying ?? false, context);
		}

		public Stopwatch()
		{
			Time = new ValueOutput<float>(this);
			IsRunning = new ValueOutput<bool>(this);
			Start = new Operation(this, 0);
			Stop = new Operation(this, 1);
			Reset = new Operation(this, 2);
		}
	}
	[NodeCategory("Time")]
	[NodeName("T", false)]
	[ContinuouslyChanging]
	public class WorldTimeFloat : ValueFunctionNode<FrooxEngineContext, float>
	{
		protected override float Compute(FrooxEngineContext context)
		{
			return (float)context.World.Time.WorldTime;
		}
	}
	[NodeCategory("Time")]
	[NodeName("T*2", false)]
	[ContinuouslyChanging]
	public class WorldTime2Float : ValueFunctionNode<FrooxEngineContext, float>
	{
		protected override float Compute(FrooxEngineContext context)
		{
			return (float)context.World.Time.WorldTime * 2f;
		}
	}
	[NodeCategory("Time")]
	[NodeName("T*10", false)]
	[ContinuouslyChanging]
	public class WorldTime10Float : ValueFunctionNode<FrooxEngineContext, float>
	{
		protected override float Compute(FrooxEngineContext context)
		{
			return (float)context.World.Time.WorldTime * 10f;
		}
	}
	[NodeName("T/2", false)]
	[NodeCategory("Time")]
	[ContinuouslyChanging]
	public class WorldTimeHalfFloat : ValueFunctionNode<FrooxEngineContext, float>
	{
		protected override float Compute(FrooxEngineContext context)
		{
			return (float)context.World.Time.WorldTime * 0.5f;
		}
	}
	[NodeName("T/10", false)]
	[NodeCategory("Time")]
	[ContinuouslyChanging]
	public class WorldTimeTenthFloat : ValueFunctionNode<FrooxEngineContext, float>
	{
		protected override float Compute(FrooxEngineContext context)
		{
			return (float)context.World.Time.WorldTime * 0.1f;
		}
	}
	[NodeName("T <size=25%>(double)", false)]
	[NodeCategory("Time")]
	[ContinuouslyChanging]
	public class WorldTimeDouble : ValueFunctionNode<FrooxEngineContext, double>
	{
		protected override double Compute(FrooxEngineContext context)
		{
			return context.World.Time.WorldTime;
		}
	}
	[NodeCategory("Time")]
	[NodeName("dT", false)]
	[ContinuouslyChanging]
	public class DeltaTime : ValueFunctionNode<FrooxEngineContext, float>
	{
		protected override float Compute(FrooxEngineContext context)
		{
			return context.World.Time.Delta;
		}
	}
	[NodeName("1/dT", false)]
	[NodeCategory("Time")]
	[ContinuouslyChanging]
	public class InvertedDeltaTime : ValueFunctionNode<FrooxEngineContext, float>
	{
		protected override float Compute(FrooxEngineContext context)
		{
			return context.World.Time.InvertedDelta;
		}
	}
	[NodeName("Smooth dT", false)]
	[NodeCategory("Time")]
	[ContinuouslyChanging]
	public class SmoothDeltaTime : ValueFunctionNode<FrooxEngineContext, float>
	{
		protected override float Compute(FrooxEngineContext context)
		{
			return context.World.Time.SmoothDelta;
		}
	}
	[NodeName("Smooth 1/dT", false)]
	[NodeCategory("Time")]
	[ContinuouslyChanging]
	public class InvertedSmoothDeltaTime : ValueFunctionNode<FrooxEngineContext, float>
	{
		protected override float Compute(FrooxEngineContext context)
		{
			return context.World.Time.InvertedSmoothDelta;
		}
	}
	[NodeCategory("Time")]
	[NodeName("Raw dT", false)]
	[ContinuouslyChanging]
	public class RawDeltaTime : ValueFunctionNode<FrooxEngineContext, float>
	{
		protected override float Compute(FrooxEngineContext context)
		{
			return context.World.Time.RawDelta;
		}
	}
	[NodeCategory("Time")]
	[NodeName("Raw 1/dT", false)]
	[ContinuouslyChanging]
	public class InvertedRawDeltaTime : ValueFunctionNode<FrooxEngineContext, float>
	{
		protected override float Compute(FrooxEngineContext context)
		{
			return context.World.Time.InvertedRawDelta;
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Variables
{
	[NodeCategory("Flow")]
	[NodeName("Boolean Latch", false)]
	[NodeOverload("Engine.DataModelBooleanToggle")]
	[ChangeSource]
	public class DataModelBooleanToggle : DataModelValueFieldStore<bool>
	{
		[PossibleContinuations(new string[] { "OnSet" })]
		public readonly Operation Set;

		[PossibleContinuations(new string[] { "OnReset" })]
		public readonly Operation Reset;

		public readonly Operation Toggle;

		public readonly Continuation OnSet;

		public readonly Continuation OnReset;

		private IOperation DoSet(FrooxEngineContext context)
		{
			Store proxy = GetProxy(context);
			if (proxy == null)
			{
				return null;
			}
			proxy.Value.Value = true;
			return OnSet.Target;
		}

		private IOperation DoReset(FrooxEngineContext context)
		{
			Store proxy = GetProxy(context);
			if (proxy == null)
			{
				return null;
			}
			proxy.Value.Value = false;
			return OnReset.Target;
		}

		private IOperation DoToggle(FrooxEngineContext context)
		{
			Sync<bool> sync = GetProxy(context)?.Value;
			if (sync == null)
			{
				return null;
			}
			if (sync.Value)
			{
				sync.Value = false;
				return OnReset.Target;
			}
			sync.Value = true;
			return OnSet.Target;
		}

		public DataModelBooleanToggle()
		{
			Set = new Operation(this, 0);
			Reset = new Operation(this, 1);
			Toggle = new Operation(this, 2);
		}
	}
	[NodeCategory("Variables")]
	[ChangeSource]
	[NodeOverload("Engine.DataModelStore")]
	public class DataModelValueFieldStore<T> : ProxyValueFunctionNode<FrooxEngineContext, DataModelValueFieldStore<T>.Store, T>, IVariable<FrooxEngineContext, T>, INode where T : unmanaged
	{
		public class Store : ProtoFluxEngineProxy
		{
			public readonly Sync<T> Value;

			internal Action ValueChanged;

			protected override void OnAwake()
			{
				base.OnAwake();
				Value.Changed += Value_Changed;
			}

			private void Value_Changed(IChangeable obj)
			{
				ValueChanged?.Invoke();
			}

			protected override void InitializeSyncMembers()
			{
				base.InitializeSyncMembers();
				Value = new Sync<T>();
			}

			public override ISyncMember GetSyncMember(int index)
			{
				return index switch
				{
					0 => persistent, 
					1 => updateOrder, 
					2 => EnabledField, 
					3 => Node, 
					4 => Path, 
					5 => Value, 
					_ => throw new ArgumentOutOfRangeException(), 
				};
			}

			public static Store __New()
			{
				return new Store();
			}
		}

		public static bool IsValidGenericType => Coder<T>.IsEnginePrimitive;

		protected override void ProxyAdded(Store proxy, FrooxEngineContext context)
		{
			NodeContextPath path = context.CaptureContextPath();
			ExecutionChangesDispatcher<FrooxEngineContext> changes = context.Changes;
			ElementPath<IOutput> element = new ElementPath<IOutput>(this, path);
			proxy.ValueChanged = delegate
			{
				changes.OutputChanged(element);
			};
		}

		protected override void ProxyRemoved(Store proxy, FrooxEngineContext context, bool inUseByAnotherInstance)
		{
			if (!inUseByAnotherInstance)
			{
				proxy.ValueChanged = null;
			}
		}

		protected override T Compute(FrooxEngineContext context)
		{
			return Read(context);
		}

		public T Read(FrooxEngineContext context)
		{
			return GetProxy(context)?.Value.Value ?? default(T);
		}

		public bool Write(T value, FrooxEngineContext context)
		{
			Sync<T> sync = GetProxy(context)?.Value;
			if (sync == null)
			{
				return false;
			}
			if (sync.IsBlockedByDrive)
			{
				return false;
			}
			sync.Value = value;
			return true;
		}
	}
	[NodeCategory("Variables")]
	[ChangeSource]
	[NodeOverload("Engine.DataModelStore")]
	public class DataModelObjectFieldStore<T> : ProxyObjectFunctionNode<FrooxEngineContext, DataModelObjectFieldStore<T>.Store, T>, IVariable<FrooxEngineContext, T>, INode
	{
		public class Store : ProtoFluxEngineProxy
		{
			public readonly Sync<T> Value;

			internal Action ValueChanged;

			protected override void OnAwake()
			{
				base.OnAwake();
				Value.Changed += Value_Changed;
			}

			private void Value_Changed(IChangeable obj)
			{
				ValueChanged?.Invoke();
			}

			protected override void InitializeSyncMembers()
			{
				base.InitializeSyncMembers();
				Value = new Sync<T>();
			}

			public override ISyncMember GetSyncMember(int index)
			{
				return index switch
				{
					0 => persistent, 
					1 => updateOrder, 
					2 => EnabledField, 
					3 => Node, 
					4 => Path, 
					5 => Value, 
					_ => throw new ArgumentOutOfRangeException(), 
				};
			}

			public static Store __New()
			{
				return new Store();
			}
		}

		public static bool IsValidGenericType
		{
			get
			{
				if (Coder<T>.IsEnginePrimitive)
				{
					return typeof(T) != typeof(Type);
				}
				return false;
			}
		}

		protected override void ProxyAdded(Store proxy, FrooxEngineContext context)
		{
			NodeContextPath path = context.CaptureContextPath();
			ExecutionChangesDispatcher<FrooxEngineContext> changes = context.Changes;
			ElementPath<IOutput> element = new ElementPath<IOutput>(this, path);
			proxy.ValueChanged = delegate
			{
				changes.OutputChanged(element);
			};
		}

		protected override void ProxyRemoved(Store proxy, FrooxEngineContext context, bool inUseByAnotherInstance)
		{
			if (!inUseByAnotherInstance)
			{
				proxy.ValueChanged = null;
			}
		}

		protected override T Compute(FrooxEngineContext context)
		{
			return Read(context);
		}

		public T Read(FrooxEngineContext context)
		{
			Store proxy = GetProxy(context);
			if (proxy == null)
			{
				return default(T);
			}
			return proxy.Value.Value;
		}

		public bool Write(T value, FrooxEngineContext context)
		{
			Sync<T> sync = GetProxy(context)?.Value;
			if (sync == null)
			{
				return false;
			}
			if (sync.IsBlockedByDrive)
			{
				return false;
			}
			sync.Value = value;
			return true;
		}
	}
	[NodeCategory("Variables")]
	[ChangeSource]
	[NodeOverload("Engine.DataModelStore")]
	public class DataModelTypeStore : ProxyObjectFunctionNode<FrooxEngineContext, DataModelTypeStore.Store, Type>, IVariable<FrooxEngineContext, Type>, INode
	{
		public class Store : ProtoFluxEngineProxy
		{
			public readonly SyncType Value;

			internal Action ValueChanged;

			protected override void OnAwake()
			{
				base.OnAwake();
				Value.Changed += Value_Changed;
			}

			private void Value_Changed(IChangeable obj)
			{
				ValueChanged?.Invoke();
			}

			protected override void InitializeSyncMembers()
			{
				base.InitializeSyncMembers();
				Value = new SyncType();
			}

			public override ISyncMember GetSyncMember(int index)
			{
				return index switch
				{
					0 => persistent, 
					1 => updateOrder, 
					2 => EnabledField, 
					3 => Node, 
					4 => Path, 
					5 => Value, 
					_ => throw new ArgumentOutOfRangeException(), 
				};
			}

			public static Store __New()
			{
				return new Store();
			}
		}

		protected override void ProxyAdded(Store proxy, FrooxEngineContext context)
		{
			NodeContextPath path = context.CaptureContextPath();
			ExecutionChangesDispatcher<FrooxEngineContext> changes = context.Changes;
			ElementPath<IOutput> element = new ElementPath<IOutput>(this, path);
			proxy.ValueChanged = delegate
			{
				changes.OutputChanged(element);
			};
		}

		protected override void ProxyRemoved(Store proxy, FrooxEngineContext context, bool inUseByAnotherInstance)
		{
			if (!inUseByAnotherInstance)
			{
				proxy.ValueChanged = null;
			}
		}

		protected override Type Compute(FrooxEngineContext context)
		{
			return Read(context);
		}

		public Type Read(FrooxEngineContext context)
		{
			return GetProxy(context)?.Value.Value;
		}

		public bool Write(Type value, FrooxEngineContext context)
		{
			SyncType syncType = GetProxy(context)?.Value;
			if (syncType == null)
			{
				return false;
			}
			if (syncType.IsBlockedByDrive)
			{
				return false;
			}
			if (!context.World.Types.IsSupported(value))
			{
				return false;
			}
			syncType.Value = value;
			return true;
		}
	}
	[NodeCategory("Variables")]
	[ChangeSource]
	[NodeOverload("Engine.DataModelStore")]
	public class DataModelObjectRefStore<T> : ProxyObjectFunctionNode<FrooxEngineContext, DataModelObjectRefStore<T>.Store, T>, IVariable<FrooxEngineContext, T>, INode where T : class, IWorldElement
	{
		public class Store : ProtoFluxEngineProxy
		{
			public readonly SyncRef<T> Target;

			internal Action ValueChanged;

			protected override void OnAwake()
			{
				base.OnAwake();
				Target.OnTargetChange += Target_OnTargetChange;
			}

			private void Target_OnTargetChange(SyncRef<T> reference)
			{
				ValueChanged?.Invoke();
			}

			protected override void InitializeSyncMembers()
			{
				base.InitializeSyncMembers();
				Target = new SyncRef<T>();
			}

			public override ISyncMember GetSyncMember(int index)
			{
				return index switch
				{
					0 => persistent, 
					1 => updateOrder, 
					2 => EnabledField, 
					3 => Node, 
					4 => Path, 
					5 => Target, 
					_ => throw new ArgumentOutOfRangeException(), 
				};
			}

			public static Store __New()
			{
				return new Store();
			}
		}

		protected override void ProxyAdded(Store proxy, FrooxEngineContext context)
		{
			NodeContextPath path = context.CaptureContextPath();
			ExecutionChangesDispatcher<FrooxEngineContext> changes = context.Changes;
			ElementPath<IOutput> element = new ElementPath<IOutput>(this, path);
			proxy.ValueChanged = delegate
			{
				changes.OutputChanged(element);
			};
		}

		protected override void ProxyRemoved(Store proxy, FrooxEngineContext context, bool inUseByAnotherInstance)
		{
			if (!inUseByAnotherInstance)
			{
				proxy.ValueChanged = null;
			}
		}

		protected override T Compute(FrooxEngineContext context)
		{
			return Read(context);
		}

		public T Read(FrooxEngineContext context)
		{
			Store proxy = GetProxy(context);
			if (proxy == null)
			{
				return null;
			}
			return proxy.Target.Target;
		}

		public bool Write(T value, FrooxEngineContext context)
		{
			SyncRef<T> syncRef = GetProxy(context)?.Target;
			if (syncRef == null)
			{
				return false;
			}
			if (syncRef.IsBlockedByDrive)
			{
				return false;
			}
			value = value.FilterWorldElement();
			if (value != null && value.IsLocalElement && !syncRef.IsLocalElement)
			{
				return false;
			}
			syncRef.Target = value;
			return true;
		}
	}
	[NodeCategory("Variables")]
	[ChangeSource]
	[NodeOverload("Engine.DataModelStore")]
	public class DataModelUserRefStore : ProxyObjectFunctionNode<FrooxEngineContext, DataModelUserRefStore.Store, global::FrooxEngine.User>, IVariable<FrooxEngineContext, global::FrooxEngine.User>, INode
	{
		public class Store : ProtoFluxEngineProxy
		{
			public readonly UserRef User;

			internal Action ValueChanged;

			protected override void OnAwake()
			{
				base.OnAwake();
				User.User.OnTargetChange += User_OnTargetChange;
			}

			private void User_OnTargetChange(SyncRef<global::FrooxEngine.User> reference)
			{
				ValueChanged?.Invoke();
			}

			protected override void InitializeSyncMembers()
			{
				base.InitializeSyncMembers();
				User = new UserRef();
			}

			public override ISyncMember GetSyncMember(int index)
			{
				return index switch
				{
					0 => persistent, 
					1 => updateOrder, 
					2 => EnabledField, 
					3 => Node, 
					4 => Path, 
					5 => User, 
					_ => throw new ArgumentOutOfRangeException(), 
				};
			}

			public static Store __New()
			{
				return new Store();
			}
		}

		protected override void ProxyAdded(Store proxy, FrooxEngineContext context)
		{
			NodeContextPath path = context.CaptureContextPath();
			ExecutionChangesDispatcher<FrooxEngineContext> changes = context.Changes;
			ElementPath<IOutput> element = new ElementPath<IOutput>(this, path);
			proxy.ValueChanged = delegate
			{
				changes.OutputChanged(element);
			};
		}

		protected override void ProxyRemoved(Store proxy, FrooxEngineContext context, bool inUseByAnotherInstance)
		{
			if (!inUseByAnotherInstance)
			{
				proxy.ValueChanged = null;
			}
		}

		protected override global::FrooxEngine.User Compute(FrooxEngineContext context)
		{
			return Read(context);
		}

		public global::FrooxEngine.User Read(FrooxEngineContext context)
		{
			return GetProxy(context)?.User.Target;
		}

		public bool Write(global::FrooxEngine.User value, FrooxEngineContext context)
		{
			UserRef userRef = GetProxy(context)?.User;
			if (userRef == null)
			{
				return false;
			}
			if (userRef.IsDriven)
			{
				return false;
			}
			userRef.Target = value.FilterWorldElement();
			return true;
		}
	}
	[NodeCategory("Variables")]
	[ChangeSource]
	[NodeOverload("Engine.DataModelStore")]
	public class DataModelObjectAssetRefStore<T> : ProxyObjectFunctionNode<FrooxEngineContext, DataModelObjectAssetRefStore<T>.Store, IAssetProvider<T>>, IVariable<FrooxEngineContext, IAssetProvider<T>>, INode where T : class, IAsset
	{
		public class Store : ProtoFluxEngineProxy
		{
			public readonly AssetRef<T> Target;

			internal Action ValueChanged;

			protected override void OnAwake()
			{
				base.OnAwake();
				Target.Changed += Value_Changed;
			}

			private void Value_Changed(IChangeable obj)
			{
				ValueChanged?.Invoke();
			}

			protected override void InitializeSyncMembers()
			{
				base.InitializeSyncMembers();
				Target = new AssetRef<T>();
			}

			public override ISyncMember GetSyncMember(int index)
			{
				return index switch
				{
					0 => persistent, 
					1 => updateOrder, 
					2 => EnabledField, 
					3 => Node, 
					4 => Path, 
					5 => Target, 
					_ => throw new ArgumentOutOfRangeException(), 
				};
			}

			public static Store __New()
			{
				return new Store();
			}
		}

		protected override void ProxyAdded(Store proxy, FrooxEngineContext context)
		{
			NodeContextPath path = context.CaptureContextPath();
			ExecutionChangesDispatcher<FrooxEngineContext> changes = context.Changes;
			ElementPath<IOutput> element = new ElementPath<IOutput>(this, path);
			proxy.ValueChanged = delegate
			{
				changes.OutputChanged(element);
			};
		}

		protected override void ProxyRemoved(Store proxy, FrooxEngineContext context, bool inUseByAnotherInstance)
		{
			if (!inUseByAnotherInstance)
			{
				proxy.ValueChanged = null;
			}
		}

		protected override IAssetProvider<T> Compute(FrooxEngineContext context)
		{
			return Read(context);
		}

		public IAssetProvider<T> Read(FrooxEngineContext context)
		{
			return GetProxy(context)?.Target.Target;
		}

		public bool Write(IAssetProvider<T> value, FrooxEngineContext context)
		{
			AssetRef<T> assetRef = GetProxy(context)?.Target;
			if (assetRef == null)
			{
				return false;
			}
			if (assetRef.IsBlockedByDrive)
			{
				return false;
			}
			value = value.FilterWorldElement();
			if (value.IsLocalElement && !assetRef.IsLocalElement)
			{
				return false;
			}
			assetRef.Target = value;
			return true;
		}
	}
	[NodeCategory("Variables/Dynamic")]
	public abstract class DynamicVariableAction : ActionNode<FrooxEngineContext>, IMappableNode, INode
	{
		public ObjectInput<Slot> Target;

		[OldName("VariableName")]
		public ObjectInput<string> Path;

		public Continuation OnNotFound;

		protected override IOperation Run(FrooxEngineContext context)
		{
			Slot slot = ((Target.Source != null) ? Target.Evaluate(context) : context.GetRootSlotContainer(this));
			if (slot == null || slot.IsRemoved)
			{
				return OnNotFound.Target;
			}
			DynamicVariableHelper.ParsePath(Path.Evaluate(context), out string spaceName, out string variableName);
			if (string.IsNullOrEmpty(variableName))
			{
				return OnNotFound.Target;
			}
			DynamicVariableSpace dynamicVariableSpace = slot.FindSpace(spaceName);
			if (dynamicVariableSpace == null)
			{
				return OnNotFound.Target;
			}
			return DoAction(dynamicVariableSpace, variableName, slot, context);
		}

		protected abstract IOperation DoAction(DynamicVariableSpace space, string variableName, Slot target, FrooxEngineContext context);
	}
	[NodeName("Write DynVar", false)]
	[NodeOverload("Engine.DynamicVariables.Write")]
	public abstract class WriteDynamicVariable<T> : DynamicVariableAction
	{
		public Continuation OnSuccess;

		public Continuation OnFailed;

		protected override IOperation DoAction(DynamicVariableSpace space, string variableName, Slot target, FrooxEngineContext context)
		{
			T valueToWrite = GetValueToWrite(context);
			if (valueToWrite is IWorldElement { IsLocalElement: not false } && !space.IsLocalElement)
			{
				return OnFailed.Target;
			}
			DynamicVariableWriteResult dynamicVariableWriteResult = space.TryWriteValue(variableName, valueToWrite);
			return dynamicVariableWriteResult switch
			{
				DynamicVariableWriteResult.Success => OnSuccess.Target, 
				DynamicVariableWriteResult.NotFound => OnNotFound.Target, 
				DynamicVariableWriteResult.Failed => OnFailed.Target, 
				_ => throw new NotImplementedException("Unsupported variable write result: " + dynamicVariableWriteResult), 
			};
		}

		protected abstract T GetValueToWrite(FrooxEngineContext context);
	}
	public class WriteDynamicValueVariable<T> : WriteDynamicVariable<T> where T : unmanaged
	{
		public ValueInput<T> Value;

		protected override T GetValueToWrite(FrooxEngineContext context)
		{
			return Value.Evaluate(context);
		}
	}
	public class WriteDynamicObjectVariable<T> : WriteDynamicVariable<T>
	{
		public ObjectInput<T> Value;

		protected override T GetValueToWrite(FrooxEngineContext context)
		{
			return Value.Evaluate(context);
		}
	}
	[NodeName("Create DynVar", false)]
	[NodeOverload("Engine.DynamicVariables.Create")]
	public abstract class CreateDynamicVariable<T> : DynamicVariableAction
	{
		public Continuation OnCreated;

		public Continuation OnAlreadyExists;

		public Continuation OnFailed;

		public ValueInput<bool> CreateDirectlyOnTarget;

		public ValueInput<bool> CreateNonPersistent;

		protected override IOperation DoAction(DynamicVariableSpace space, string variableName, Slot target, FrooxEngineContext context)
		{
			DynamicVariableSpace.ValueManager<T> manager = space.GetManager<T>(variableName, createIfNotExist: false);
			if (manager == null || manager.ReadableValueCount == 0)
			{
				Slot slot = (CreateDirectlyOnTarget.Evaluate(context, defaultValue: false) ? target : space.Slot);
				string text = variableName;
				if (!string.IsNullOrEmpty(space.SpaceName.Value))
				{
					text = space.SpaceName.Value + "/" + text;
				}
				T initialValue = GetInitialValue(context);
				if (initialValue is IWorldElement { IsLocalElement: not false } && !slot.IsLocalElement)
				{
					return OnFailed.Target;
				}
				slot.CreateVariable(text, initialValue, !CreateNonPersistent.Evaluate(context, defaultValue: false));
				return OnCreated.Target;
			}
			return OnAlreadyExists.Target;
		}

		protected abstract T GetInitialValue(FrooxEngineContext context);
	}
	public class CreateDynamicValueVariable<T> : CreateDynamicVariable<T> where T : unmanaged
	{
		public ValueInput<T> InitialValue;

		protected override T GetInitialValue(FrooxEngineContext context)
		{
			return InitialValue.Evaluate(context);
		}
	}
	public class CreateDynamicObjectVariable<T> : CreateDynamicVariable<T>
	{
		public ObjectInput<T> InitialValue;

		protected override T GetInitialValue(FrooxEngineContext context)
		{
			return InitialValue.Evaluate(context);
		}
	}
	[NodeName("Write or Create DynVar", false)]
	[NodeOverload("Engine.DynamicVariables.WriteOrCreate")]
	public abstract class WriteOrCreateDynamicVariable<T> : DynamicVariableAction
	{
		public Continuation OnCreated;

		public Continuation OnWritten;

		public Continuation OnFailed;

		public ValueInput<bool> CreateDirectlyOnTarget;

		public ValueInput<bool> CreateNonPersistent;

		protected override IOperation DoAction(DynamicVariableSpace space, string variableName, Slot target, FrooxEngineContext context)
		{
			T value = GetValue(context);
			if (value is IWorldElement { IsLocalElement: not false } && !space.IsLocalElement)
			{
				return OnFailed.Target;
			}
			switch (space.TryWriteValue(variableName, value))
			{
			case DynamicVariableWriteResult.Success:
				return OnWritten.Target;
			case DynamicVariableWriteResult.Failed:
				return OnFailed.Target;
			default:
			{
				Slot slot = (CreateDirectlyOnTarget.Evaluate(context, defaultValue: false) ? target : space.Slot);
				string text = variableName;
				if (!string.IsNullOrEmpty(space.SpaceName.Value))
				{
					text = space.SpaceName.Value + "/" + text;
				}
				if (slot.CreateVariable(text, value, !CreateNonPersistent.Evaluate(context, defaultValue: false)))
				{
					return OnCreated.Target;
				}
				return OnNotFound.Target;
			}
			}
		}

		protected abstract T GetValue(FrooxEngineContext context);
	}
	public class WriteOrCreateDynamicValueVariable<T> : WriteOrCreateDynamicVariable<T> where T : unmanaged
	{
		public ValueInput<T> Value;

		protected override T GetValue(FrooxEngineContext context)
		{
			return Value.Evaluate(context);
		}
	}
	public class WriteOrCreateDynamicObjectVariable<T> : WriteOrCreateDynamicVariable<T>
	{
		public ObjectInput<T> Value;

		protected override T GetValue(FrooxEngineContext context)
		{
			return Value.Evaluate(context);
		}
	}
	public class DeleteDynamicVariable<T> : DynamicVariableAction
	{
		public Continuation OnDeleted;

		protected override IOperation DoAction(DynamicVariableSpace space, string variableName, Slot target, FrooxEngineContext context)
		{
			DynamicVariableSpace.ValueManager<T> manager = space.GetManager<T>(variableName, createIfNotExist: false);
			if (manager == null || manager.ReadableValueCount == 0)
			{
				return OnNotFound.Target;
			}
			manager.DeleteAllReadable();
			return OnDeleted.Target;
		}
	}
	[NodeCategory("Variables/Dynamic")]
	public abstract class ClearDynamicVariablesBase : ActionNode<FrooxEngineContext>, IMappableNode, INode
	{
		public ObjectInput<Slot> Target;

		public ObjectInput<string> SpaceName;

		public Continuation OnNotFound;

		public Continuation OnCleared;

		public readonly ValueOutput<int> ClearedCount;

		protected override IOperation Run(FrooxEngineContext context)
		{
			Slot slot = ((Target.Source != null) ? Target.Evaluate(context) : context.GetRootSlotContainer(this));
			ClearedCount.Write(0, context);
			if (slot == null || slot.IsRemoved)
			{
				return OnNotFound.Target;
			}
			string text = SpaceName.Evaluate(context);
			if (string.IsNullOrEmpty(text))
			{
				return OnNotFound.Target;
			}
			if (!text.EndsWith("/"))
			{
				text += "/";
			}
			DynamicVariableHelper.ParsePath(text, out string spaceName, out string _);
			if (string.IsNullOrEmpty(spaceName))
			{
				return OnNotFound.Target;
			}
			DynamicVariableSpace dynamicVariableSpace = slot.FindSpace(spaceName);
			if (dynamicVariableSpace == null)
			{
				return OnNotFound.Target;
			}
			ClearedCount.Write(Clear(dynamicVariableSpace, context), context);
			return OnCleared.Target;
		}

		protected abstract int Clear(DynamicVariableSpace space, FrooxEngineContext context);

		protected ClearDynamicVariablesBase()
		{
			ClearedCount = new ValueOutput<int>(this);
		}
	}
	public class ClearDynamicVariables : ClearDynamicVariablesBase
	{
		protected override int Clear(DynamicVariableSpace space, FrooxEngineContext context)
		{
			return space.ClearAllValues();
		}
	}
	public class ClearDynamicVariablesOfType<T> : ClearDynamicVariablesBase
	{
		protected override int Clear(DynamicVariableSpace space, FrooxEngineContext context)
		{
			return space.ClearAllValuesOfType<T>();
		}
	}
	public class DynamicVariableInputProxy<T> : ProtoFluxEngineProxy, IDynamicVariable<T>, IDynamicVariable, IWorldElement
	{
		internal Action OnChanged;

		internal Action OnSpaceLinked;

		internal Action OnSpaceUnlinked;

		protected DynamicVariableHandler<T> handler;

		private string _variableName;

		public bool AlwaysOverrideOnLink => false;

		public bool IsWriteOnly => true;

		public T DynamicValue
		{
			get
			{
				return handler.LastValue;
			}
			set
			{
				if (!EqualityComparer<T>.Default.Equals(handler.LastValue, value))
				{
					handler.LastValue = value;
					OnChanged?.Invoke();
				}
			}
		}

		public bool HasValue => handler.HasVariable;

		public string VariableName
		{
			get
			{
				return _variableName;
			}
			set
			{
				if (!(_variableName == value))
				{
					_variableName = value;
					UpdateLinking();
				}
			}
		}

		public void MarkSpaceDirty()
		{
			handler?.MarkSpaceDirty();
		}

		public bool UpdateLinking()
		{
			if (base.IsDisposed)
			{
				throw new InvalidOperationException("Cannot update linking on disposed proxy");
			}
			DynamicVariableSpace.ValueManager<T> currentManager = handler.CurrentManager;
			bool result = handler.UpdateLinking(_variableName, hasLocalValue: false);
			if (currentManager != handler.CurrentManager)
			{
				if (currentManager != null)
				{
					OnSpaceUnlinked?.Invoke();
				}
				if (handler.CurrentManager != null)
				{
					Action onSpaceLinked = OnSpaceLinked;
					if (onSpaceLinked == null)
					{
						return result;
					}
					onSpaceLinked();
				}
			}
			return result;
		}

		private void MarkDirty()
		{
			UpdateLinking();
			OnChanged?.Invoke();
		}

		protected override void OnAwake()
		{
			base.OnAwake();
			handler = new DynamicVariableHandler<T>(base.Slot, this, MarkDirty, null);
		}

		protected override void OnDispose()
		{
			handler?.Dispose();
			handler = null;
			base.OnDispose();
		}

		protected override void InitializeSyncMembers()
		{
			base.InitializeSyncMembers();
		}

		public override ISyncMember GetSyncMember(int index)
		{
			return index switch
			{
				0 => persistent, 
				1 => updateOrder, 
				2 => EnabledField, 
				3 => Node, 
				4 => Path, 
				_ => throw new ArgumentOutOfRangeException(), 
			};
		}

		public static DynamicVariableInputProxy<T> __New()
		{
			return new DynamicVariableInputProxy<T>();
		}
	}
	[NodeCategory("Variables/Dynamic")]
	public abstract class DynamicVariableInput<T> : ProxyVoidNode<FrooxEngineContext, DynamicVariableInputProxy<T>>
	{
		public readonly GlobalRef<string> VariableName;

		protected abstract IOutput ValueOutput { get; }

		protected abstract IOutput HasValueOutput { get; }

		private void OnVariableNameChanged(string name, FrooxEngineContext context)
		{
			DynamicVariableInputProxy<T> proxy = GetProxy(context);
			if (proxy != null)
			{
				proxy.VariableName = name;
			}
		}

		protected override void ProxyAdded(DynamicVariableInputProxy<T> proxy, FrooxEngineContext context)
		{
			NodeContextPath path = context.CaptureContextPath();
			ElementPath<IOutput> valueElement = new ElementPath<IOutput>(ValueOutput, path);
			ElementPath<IOutput> hasValueElement = new ElementPath<IOutput>(HasValueOutput, path);
			ExecutionChangesDispatcher<FrooxEngineContext> changes = context.Changes;
			proxy.VariableName = context.CurrentScope.ReadGlobal(VariableName);
			proxy.OnChanged = (Action)Delegate.Combine(proxy.OnChanged, (Action)delegate
			{
				changes.OutputChanged(valueElement);
				changes.OutputChanged(hasValueElement);
			});
		}

		protected override void ProxyRemoved(DynamicVariableInputProxy<T> proxy, FrooxEngineContext context, bool inUseByAnotherInstance)
		{
			if (!inUseByAnotherInstance)
			{
				proxy.VariableName = null;
				proxy.OnChanged = null;
			}
		}

		protected override void ComputeOutputs(FrooxEngineContext context)
		{
			DynamicVariableInputProxy<T> proxy = GetProxy(context);
			if (proxy == null)
			{
				UpdateValue(default(T), hasValue: false, context);
			}
			else
			{
				UpdateValue(proxy.DynamicValue, proxy.HasValue, context);
			}
		}

		protected abstract void UpdateValue(T value, bool hasValue, FrooxEngineContext context);

		protected DynamicVariableInput()
		{
			((DynamicVariableInput<>)(object)this).VariableName = new GlobalRef<string>(this, 0);
		}
	}
	public abstract class DynamicVariableInputWithEvents<T> : DynamicVariableInput<T>
	{
		public ObjectInput<global::FrooxEngine.User> DetectingUser;

		public Call OnSpaceLinked;

		public Call OnSpaceUnlinked;

		protected override void ProxyAdded(DynamicVariableInputProxy<T> proxy, FrooxEngineContext context)
		{
			base.ProxyAdded(proxy, context);
			NodeContextPath path = context.CaptureContextPath();
			context.GetEventDispatcher(out var dispatcher);
			proxy.OnSpaceLinked = delegate
			{
				dispatcher.ScheduleEvent(path, HandleLinked, null);
			};
			proxy.OnSpaceUnlinked = delegate
			{
				dispatcher.ScheduleEvent(path, HandleUnlinked, null);
			};
		}

		protected override void ProxyRemoved(DynamicVariableInputProxy<T> proxy, FrooxEngineContext context, bool inUseByAnotherInstance)
		{
			base.ProxyRemoved(proxy, context, inUseByAnotherInstance);
			if (!inUseByAnotherInstance)
			{
				proxy.OnSpaceLinked = null;
				proxy.OnSpaceUnlinked = null;
			}
		}

		private void HandleLinked(FrooxEngineContext context, object arg)
		{
			global::FrooxEngine.User user = DetectingUser.Evaluate(context);
			if (user != null && user.IsLocalUser)
			{
				OnSpaceLinked.Execute(context);
			}
		}

		private void HandleUnlinked(FrooxEngineContext context, object arg)
		{
			global::FrooxEngine.User user = DetectingUser.Evaluate(context);
			if (user != null && user.IsLocalUser)
			{
				OnSpaceUnlinked.Execute(context);
			}
		}
	}
	[NodeName("DynVar Input", false)]
	[NodeOverload("Engine.DynamicVariables.Input")]
	public class DynamicVariableValueInput<T> : DynamicVariableInput<T> where T : unmanaged
	{
		[ChangeSource]
		public readonly ValueOutput<T> Value;

		[ChangeSource]
		public readonly ValueOutput<bool> HasValue;

		protected override IOutput ValueOutput => Value;

		protected override IOutput HasValueOutput => HasValue;

		protected override void UpdateValue(T value, bool hasValue, FrooxEngineContext context)
		{
			Value.Write(value, context);
			HasValue.Write(hasValue, context);
		}

		public DynamicVariableValueInput()
		{
			((DynamicVariableValueInput<>)(object)this).Value = new ValueOutput<T>(this);
			((DynamicVariableValueInput<>)(object)this).HasValue = new ValueOutput<bool>(this);
		}
	}
	[NodeName("DynVar Input", false)]
	[NodeOverload("Engine.DynamicVariables.Input")]
	public class DynamicVariableObjectInput<T> : DynamicVariableInput<T>
	{
		[ChangeSource]
		public readonly ObjectOutput<T> Value;

		[ChangeSource]
		public readonly ValueOutput<bool> HasValue;

		protected override IOutput ValueOutput => Value;

		protected override IOutput HasValueOutput => HasValue;

		protected override void UpdateValue(T value, bool hasValue, FrooxEngineContext context)
		{
			Value.Write(value, context);
			HasValue.Write(hasValue, context);
		}

		public DynamicVariableObjectInput()
		{
			((DynamicVariableObjectInput<>)(object)this).Value = new ObjectOutput<T>(this);
			((DynamicVariableObjectInput<>)(object)this).HasValue = new ValueOutput<bool>(this);
		}
	}
	[NodeName("DynVar Input with Events", false)]
	[NodeOverload("Engine.DynamicVariables.InputWithEvents")]
	public class DynamicVariableValueInputWithEvents<T> : DynamicVariableInputWithEvents<T> where T : unmanaged
	{
		[ChangeSource]
		public readonly ValueOutput<T> Value;

		[ChangeSource]
		public readonly ValueOutput<bool> HasValue;

		protected override IOutput ValueOutput => Value;

		protected override IOutput HasValueOutput => HasValue;

		protected override void UpdateValue(T value, bool hasValue, FrooxEngineContext context)
		{
			Value.Write(value, context);
			HasValue.Write(hasValue, context);
		}

		public DynamicVariableValueInputWithEvents()
		{
			((DynamicVariableValueInputWithEvents<>)(object)this).Value = new ValueOutput<T>(this);
			((DynamicVariableValueInputWithEvents<>)(object)this).HasValue = new ValueOutput<bool>(this);
		}
	}
	[NodeName("DynVar Input with Events", false)]
	[NodeOverload("Engine.DynamicVariables.InputWithEvents")]
	public class DynamicVariableObjectInputWithEvents<T> : DynamicVariableInputWithEvents<T>
	{
		[ChangeSource]
		public readonly ObjectOutput<T> Value;

		[ChangeSource]
		public readonly ValueOutput<bool> HasValue;

		protected override IOutput ValueOutput => Value;

		protected override IOutput HasValueOutput => HasValue;

		protected override void UpdateValue(T value, bool hasValue, FrooxEngineContext context)
		{
			Value.Write(value, context);
			HasValue.Write(hasValue, context);
		}

		public DynamicVariableObjectInputWithEvents()
		{
			((DynamicVariableObjectInputWithEvents<>)(object)this).Value = new ObjectOutput<T>(this);
			((DynamicVariableObjectInputWithEvents<>)(object)this).HasValue = new ValueOutput<bool>(this);
		}
	}
	[NodeCategory("Variables/Dynamic")]
	[NodeName("Read DynVar", false)]
	[NodeOverload("Engine.DynamicVariables.Read")]
	public abstract class ReadDynamicVariable<T> : VoidNode<FrooxEngineContext>, IMappableNode, INode
	{
		public ObjectArgument<Slot> Source;

		public ObjectArgument<string> Path;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> FoundValue;

		protected override void ComputeOutputs(FrooxEngineContext context)
		{
			Slot slot = ((Source.Source != null) ? 0.ReadObject<Slot>(context) : context.GetRootSlotContainer(this));
			if (slot == null)
			{
				SetNotFound(context);
				return;
			}
			DynamicVariableHelper.ParsePath(1.ReadObject<string>(context), out string spaceName, out string variableName);
			if (string.IsNullOrEmpty(variableName))
			{
				SetNotFound(context);
				return;
			}
			DynamicVariableSpace dynamicVariableSpace = slot.FindSpace(spaceName);
			T value;
			if (dynamicVariableSpace == null)
			{
				SetNotFound(context);
			}
			else if (dynamicVariableSpace.TryReadValue<T>(variableName, out value))
			{
				FoundValue.Write(value: true, context);
				SetValue(value, context);
			}
			else
			{
				SetNotFound(context);
			}
		}

		protected void SetNotFound(FrooxEngineContext context)
		{
			FoundValue.Write(value: false, context);
			SetValue(Coder<T>.Default, context);
		}

		protected abstract void SetValue(T value, FrooxEngineContext context);

		protected ReadDynamicVariable()
		{
			((ReadDynamicVariable<>)(object)this).FoundValue = new ValueOutput<bool>(this);
		}
	}
	public class ReadDynamicValueVariable<T> : ReadDynamicVariable<T> where T : unmanaged
	{
		[ContinuouslyChanging]
		public readonly ValueOutput<T> Value;

		protected override void SetValue(T value, FrooxEngineContext context)
		{
			Value.Write(value, context);
		}

		public ReadDynamicValueVariable()
		{
			((ReadDynamicValueVariable<>)(object)this).Value = new ValueOutput<T>(this);
		}
	}
	public class ReadDynamicObjectVariable<T> : ReadDynamicVariable<T>
	{
		[ContinuouslyChanging]
		public readonly ObjectOutput<T> Value;

		protected override void SetValue(T value, FrooxEngineContext context)
		{
			Value.Write(value, context);
		}

		public ReadDynamicObjectVariable()
		{
			((ReadDynamicObjectVariable<>)(object)this).Value = new ObjectOutput<T>(this);
		}
	}
	[NodeCategory("Variables/Spatial")]
	[NodeName("Sample Boolean SpatialVar", false)]
	[NodeOverload("Engine.SpatialVariables.SampleBoolean")]
	[ContinuouslyChanging]
	public class SampleBooleanSpatialVariable : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ValueArgument<float3> Point;

		public ObjectArgument<string> Name;

		public ValueArgument<BooleanSpatialVariableMode> Mode;

		public ValueArgument<bool> BaseValue;

		protected override bool Compute(FrooxEngineContext context)
		{
			float3 point = 0.ReadValue<float3>(context);
			string name = 1.ReadObject<string>(context);
			BooleanSpatialVariableMode mode = 2.ReadValue<BooleanSpatialVariableMode>(context);
			bool defaultValue = 3.ReadValue<bool>(context);
			return context.World.SampleBooleanSpatialVariable(name, in point, mode, defaultValue);
		}
	}
	[NodeCategory("Variables/Spatial")]
	[NodeName("Sample SpatialVar", false)]
	[NodeOverload("Engine.SpatialVariables.Sample")]
	[ContinuouslyChanging]
	public class SampleValueSpatialVariable<T> : ValueFunctionNode<FrooxEngineContext, T> where T : unmanaged
	{
		public ValueArgument<float3> Point;

		public ObjectArgument<string> Name;

		public ValueArgument<T> DefaultValue;

		public static bool IsValidGenericType => Coder<T>.IsSupported;

		protected override T Compute(FrooxEngineContext context)
		{
			float3 point = 0.ReadValue<float3>(context);
			string name = 1.ReadObject<string>(context);
			T defaultValue = 2.ReadValue<T>(context);
			return context.World.SampleHighestPrioritySpatialVariable(name, in point, defaultValue);
		}
	}
	[NodeCategory("Variables/Spatial")]
	[NodeName("Sample SpatialVar", false)]
	[NodeOverload("Engine.SpatialVariables.Sample")]
	[ContinuouslyChanging]
	public class SampleObjectSpatialVariable<T> : ObjectFunctionNode<FrooxEngineContext, T>
	{
		public ValueArgument<float3> Point;

		public ObjectArgument<string> Name;

		public ObjectArgument<T> DefaultValue;

		protected override T Compute(FrooxEngineContext context)
		{
			float3 point = 0.ReadValue<float3>(context);
			string name = 1.ReadObject<string>(context);
			T defaultValue = 2.ReadObject<T>(context);
			return context.World.SampleHighestPrioritySpatialVariable(name, in point, defaultValue);
		}
	}
	[NodeCategory("Variables/Spatial")]
	[NodeName("Sample Numeric SpatialVar", false)]
	[NodeOverload("Engine.SpatialVariables.SampleNumeric")]
	[ContinuouslyChanging]
	public class SampleNumericSpatialVariable<T> : ValueFunctionNode<FrooxEngineContext, T> where T : unmanaged
	{
		public ValueArgument<float3> Point;

		public ObjectArgument<string> Name;

		public ValueArgument<ValueSpatialVariableMode> Mode;

		public ValueArgument<T> BaseValue;

		public static bool IsValidGenericType
		{
			get
			{
				if (Coder<T>.SupportsAddSub && Coder<T>.SupportsScale)
				{
					return Coder<T>.SupportsLerp;
				}
				return false;
			}
		}

		protected override T Compute(FrooxEngineContext context)
		{
			float3 point = 0.ReadValue<float3>(context);
			string name = 1.ReadObject<string>(context);
			ValueSpatialVariableMode mode = 2.ReadValue<ValueSpatialVariableMode>(context);
			T baseValue = 3.ReadValue<T>(context);
			return context.World.SampleNumericValueSpatialVariable(name, in point, mode, baseValue);
		}
	}
	[NodeCategory("Variables/Spatial")]
	[NodeName("Sample SparialVar Partial Derivative", false)]
	[NodeOverload("Engine.SpatialVariables.SampleDerivative")]
	[ContinuouslyChanging]
	public class SampleSpatialVariablePartialDerivative<T> : VoidNode<FrooxEngineContext> where T : unmanaged
	{
		public ValueArgument<float3> Point;

		public ValueArgument<floatQ> Orientation;

		public ObjectArgument<string> Name;

		public ValueArgument<ValueSpatialVariableMode> Mode;

		public ValueArgument<T> BaseValue;

		[@DefaultValue(0.001f)]
		public ValueArgument<float> SamplingDistance;

		public readonly ValueOutput<T> X;

		public readonly ValueOutput<T> Y;

		public readonly ValueOutput<T> Z;

		public static bool IsValidGenericType
		{
			get
			{
				if (Coder<T>.SupportsAddSub && Coder<T>.SupportsScale)
				{
					return Coder<T>.SupportsLerp;
				}
				return false;
			}
		}

		protected override void ComputeOutputs(FrooxEngineContext context)
		{
			float3 point = 0.ReadValue<float3>(context);
			floatQ orientation = 1.ReadValue<floatQ>(context);
			string name = 2.ReadObject<string>(context);
			ValueSpatialVariableMode mode = 3.ReadValue<ValueSpatialVariableMode>(context);
			T baseValue = 4.ReadValue<T>(context);
			float samplingDistance = 5.ReadValue<float>(context);
			var (value, value2, value3) = context.World.SampleSpatialVariablePartialDerivative(name, in point, in orientation, samplingDistance, mode, baseValue);
			X.Write(value, context);
			Y.Write(value2, context);
			Z.Write(value3, context);
		}

		public SampleSpatialVariablePartialDerivative()
		{
			((SampleSpatialVariablePartialDerivative<>)(object)this).X = new ValueOutput<T>(this);
			((SampleSpatialVariablePartialDerivative<>)(object)this).Y = new ValueOutput<T>(this);
			((SampleSpatialVariablePartialDerivative<>)(object)this).Z = new ValueOutput<T>(this);
		}
	}
	[NodeCategory("Variables/Spatial")]
	[NodeName("Sample Min/Max SpatialVar", false)]
	[NodeOverload("Engine.SpatialVariables.SampleMinMax")]
	public class SampleMinMaxSpatialVariable<T> : VoidNode<FrooxEngineContext> where T : unmanaged
	{
		public ValueArgument<float3> Point;

		public ObjectArgument<string> Name;

		[ContinuouslyChanging]
		public readonly ValueOutput<T> Min;

		[ContinuouslyChanging]
		public readonly ValueOutput<T> Max;

		[ContinuouslyChanging]
		public readonly ValueOutput<bool> FoundAny;

		public static bool IsValidGenericType => Coder<T>.SupportsMinMax;

		protected override void ComputeOutputs(FrooxEngineContext context)
		{
			float3 point = 0.ReadValue<float3>(context);
			string name = 1.ReadObject<string>(context);
			T min;
			T max;
			bool value = context.World.SampleMinMaxValueSpatialVariable<T>(name, in point, out min, out max);
			Min.Write(min, context);
			Max.Write(max, context);
			FoundAny.Write(value, context);
		}

		public SampleMinMaxSpatialVariable()
		{
			((SampleMinMaxSpatialVariable<>)(object)this).Min = new ValueOutput<T>(this);
			((SampleMinMaxSpatialVariable<>)(object)this).Max = new ValueOutput<T>(this);
			((SampleMinMaxSpatialVariable<>)(object)this).FoundAny = new ValueOutput<bool>(this);
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Components
{
	[NodeCategory("Components")]
	public class GetComponentEnabled : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<global::FrooxEngine.IComponent> Component;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.IComponent>(context)?.Enabled ?? false;
		}
	}
	[NodeCategory("Components")]
	public class SetComponentEnabled : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<global::FrooxEngine.IComponent> Component;

		public ValueInput<bool> State;

		protected override bool Do(FrooxEngineContext context)
		{
			global::FrooxEngine.IComponent component = Component.Evaluate(context);
			if (component == null)
			{
				return false;
			}
			if (State.Source != null)
			{
				component.Enabled = State.Evaluate(context, defaultValue: false);
			}
			return true;
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Cloud
{
	[NodeCategory("Variables/Cloud")]
	public abstract class CloudVariableRequest<T> : AsyncActionNode<FrooxEngineContext>
	{
		public ObjectInput<string> Path;

		public ObjectInput<string> VariableOwnerId;

		public AsyncCall OnRequest;

		public Continuation OnDone;

		public Continuation OnFail;

		protected override async Task<IOperation> RunAsync(FrooxEngineContext context)
		{
			string path = Path.Evaluate(context);
			if (!CloudVariableHelper.IsValidPath(path))
			{
				return OnFail.Target;
			}
			string text = VariableOwnerId.Evaluate(context, context.LocalUser?.UserID);
			OwnerType ownerType = IdUtil.GetOwnerType(text);
			if (ownerType != OwnerType.User && ownerType != OwnerType.Group)
			{
				return OnFail.Target;
			}
			CloudVariableProxy proxy = context.Cloud.Variables.RequestProxy(text, path);
			Task refreshTask = proxy.Refresh();
			await OnRequest.ExecuteAsync(context);
			await refreshTask;
			if (proxy.State == CloudVariableState.Invalid || proxy.State == CloudVariableState.Unregistered)
			{
				return OnFail.Target;
			}
			if (Process(proxy, context))
			{
				return OnDone.Target;
			}
			return OnFail.Target;
		}

		protected abstract bool Process(CloudVariableProxy proxy, FrooxEngineContext context);
	}
	[NodeName("Read Cloud Variable", false)]
	[NodeOverload("Engine.Cloud.ReadCloudVariable")]
	public abstract class ReadCloudVariable<T> : CloudVariableRequest<T>
	{
		protected override bool Process(CloudVariableProxy proxy, FrooxEngineContext context)
		{
			if (!proxy.PublicRead && !context.World.IsUserspace())
			{
				return false;
			}
			SetValue(proxy.ReadValue<T>(), context);
			return true;
		}

		protected abstract void SetValue(T value, FrooxEngineContext context);
	}
	public class ReadValueCloudVariable<T> : ReadCloudVariable<T> where T : unmanaged
	{
		public readonly ValueOutput<T> Value;

		protected override void SetValue(T value, FrooxEngineContext context)
		{
			Value.Write(value, context);
		}

		public ReadValueCloudVariable()
		{
			((ReadValueCloudVariable<>)(object)this).Value = new ValueOutput<T>(this);
		}
	}
	public class ReadObjectCloudVariable<T> : ReadCloudVariable<T>
	{
		public readonly ObjectOutput<T> Value;

		protected override void SetValue(T value, FrooxEngineContext context)
		{
			Value.Write(value, context);
		}

		public ReadObjectCloudVariable()
		{
			((ReadObjectCloudVariable<>)(object)this).Value = new ObjectOutput<T>(this);
		}
	}
	[NodeName("Write Cloud Variable", false)]
	[NodeOverload("Engine.Cloud.WriteCloudVariable")]
	public abstract class WriteCloudVariable<T> : CloudVariableRequest<T>
	{
		protected override bool Process(CloudVariableProxy proxy, FrooxEngineContext context)
		{
			if (!proxy.PublicWrite && !context.World.IsUserspace())
			{
				return false;
			}
			return proxy.SetValue(GetValueToWrite(context));
		}

		protected abstract T GetValueToWrite(FrooxEngineContext context);
	}
	public class WriteValueCloudVariable<T> : WriteCloudVariable<T> where T : unmanaged
	{
		public ValueInput<T> Value;

		protected override T GetValueToWrite(FrooxEngineContext context)
		{
			return Value.Evaluate(context);
		}
	}
	public class WriteObjectCloudVariable<T> : WriteCloudVariable<T>
	{
		public ObjectInput<T> Value;

		protected override T GetValueToWrite(FrooxEngineContext context)
		{
			return Value.Evaluate(context);
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Cloud.Twitch
{
	[NodeCategory("Network/Twitch")]
	public abstract class TwitchEventsNode : VoidNode<FrooxEngineContext>
	{
		public readonly GlobalRef<TwitchInterface> Interface;

		private ObjectStore<TwitchInterface> _current;

		public override bool CanBeEvaluated => false;

		private void OnInterfaceChanged(TwitchInterface twitch, FrooxEngineContext context)
		{
			TwitchInterface twitchInterface = _current.Read(context);
			if (twitchInterface != twitch)
			{
				if (twitchInterface != null)
				{
					Unregister(twitchInterface, context);
				}
				if (twitch != null)
				{
					NodeContextPath path = context.CaptureContextPath();
					context.GetEventDispatcher(out ExecutionEventDispatcher<FrooxEngineContext> eventDispatcher);
					Register(twitch, path, eventDispatcher, context);
					_current.Write(twitch, context);
				}
				else
				{
					_current.Clear(context);
					Clear(context);
				}
			}
		}

		protected abstract void Register(TwitchInterface twitch, NodeContextPath path, ExecutionEventDispatcher<FrooxEngineContext> dispatcher, FrooxEngineContext context);

		protected abstract void Unregister(TwitchInterface twitch, FrooxEngineContext context);

		protected abstract void Clear(FrooxEngineContext context);

		protected TwitchEventsNode()
		{
			Interface = new GlobalRef<TwitchInterface>(this, 0);
		}
	}
	public class TwitchChatMessageEvent : TwitchEventsNode
	{
		public Call OnMessage;

		public readonly ObjectOutput<string> Message;

		public readonly ObjectOutput<string> UserId;

		public readonly ObjectOutput<string> DisplayName;

		public readonly ValueOutput<colorX> Color;

		public readonly ValueOutput<bool> IsHighlighted;

		public readonly ValueOutput<bool> IsSubscriber;

		public readonly ValueOutput<bool> IsModerator;

		public readonly ValueOutput<bool> IsBroadcaster;

		public readonly ValueOutput<bool> IsTurbo;

		public readonly ValueOutput<bool> IsVIP;

		public readonly ValueOutput<BadgeColor> CheerBadge;

		public readonly ValueOutput<int> CheerAmount;

		public readonly ValueOutput<int> Bits;

		public readonly ValueOutput<double> BitsDollars;

		public readonly ValueOutput<int> SubscribedMonthCount;

		public readonly ObjectOutput<string> CustomRewardId;

		private ObjectStore<Action<OnMessageReceivedArgs>> _handler;

		protected override void Register(TwitchInterface twitch, NodeContextPath path, ExecutionEventDispatcher<FrooxEngineContext> dispatcher, FrooxEngineContext context)
		{
			Action<OnMessageReceivedArgs> value = delegate(OnMessageReceivedArgs args)
			{
				dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
				{
					HandleEvent(args, c);
				});
			};
			twitch.OnMessageReceived += value;
			_handler.Write(value, context);
		}

		protected override void Unregister(TwitchInterface twitch, FrooxEngineContext context)
		{
			twitch.OnMessageReceived -= _handler.Read(context);
		}

		protected override void Clear(FrooxEngineContext context)
		{
			_handler.Clear(context);
		}

		private void HandleEvent(OnMessageReceivedArgs args, FrooxEngineContext context)
		{
			ChatMessage chatMessage = args.ChatMessage;
			Message.Write(chatMessage.Message, context);
			UserId.Write(chatMessage.UserId, context);
			DisplayName.Write(chatMessage.DisplayName, context);
			Color.Write(colorX.FromHexCode(chatMessage.ColorHex, colorX.Black), context);
			IsHighlighted.Write(chatMessage.IsHighlighted, context);
			IsSubscriber.Write(chatMessage.IsSubscriber, context);
			IsModerator.Write(chatMessage.IsModerator, context);
			IsBroadcaster.Write(chatMessage.IsBroadcaster, context);
			IsTurbo.Write(chatMessage.IsTurbo, context);
			IsVIP.Write(chatMessage.IsVip, context);
			CheerBadge.Write(chatMessage.CheerBadge?.Color ?? BadgeColor.Gray, context);
			CheerAmount.Write(chatMessage.CheerBadge?.CheerAmount ?? 0, context);
			Bits.Write(chatMessage.Bits, context);
			BitsDollars.Write(chatMessage.BitsInDollars, context);
			SubscribedMonthCount.Write(chatMessage.SubscribedMonthCount, context);
			CustomRewardId.Write(chatMessage.CustomRewardId, context);
			OnMessage.Execute(context);
		}

		public TwitchChatMessageEvent()
		{
			Message = new ObjectOutput<string>(this);
			UserId = new ObjectOutput<string>(this);
			DisplayName = new ObjectOutput<string>(this);
			Color = new ValueOutput<colorX>(this);
			IsHighlighted = new ValueOutput<bool>(this);
			IsSubscriber = new ValueOutput<bool>(this);
			IsModerator = new ValueOutput<bool>(this);
			IsBroadcaster = new ValueOutput<bool>(this);
			IsTurbo = new ValueOutput<bool>(this);
			IsVIP = new ValueOutput<bool>(this);
			CheerBadge = new ValueOutput<BadgeColor>(this);
			CheerAmount = new ValueOutput<int>(this);
			Bits = new ValueOutput<int>(this);
			BitsDollars = new ValueOutput<double>(this);
			SubscribedMonthCount = new ValueOutput<int>(this);
			CustomRewardId = new ObjectOutput<string>(this);
		}
	}
	public class TwitchSubscriptionEvent : TwitchEventsNode
	{
		public Call OnSubscription;

		public readonly ObjectOutput<string> UserId;

		public readonly ObjectOutput<string> DisplayName;

		public readonly ObjectOutput<string> Message;

		public readonly ValueOutput<int> Months;

		public readonly ValueOutput<SubscriptionPlan> Plan;

		public readonly ValueOutput<bool> IsResub;

		public readonly ValueOutput<bool> IsGifted;

		public readonly ObjectOutput<string> GiftedBy;

		public readonly ValueOutput<bool> IsAnonymous;

		private ObjectStore<Action<OnNewSubscriberArgs>> _newSub;

		private ObjectStore<Action<OnReSubscriberArgs>> _reSub;

		private ObjectStore<Action<OnGiftedSubscriptionArgs>> _gifted;

		protected override void Register(TwitchInterface twitch, NodeContextPath path, ExecutionEventDispatcher<FrooxEngineContext> dispatcher, FrooxEngineContext context)
		{
			Action<OnNewSubscriberArgs> value = delegate(OnNewSubscriberArgs args)
			{
				dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
				{
					OnNew(args, c);
				});
			};
			Action<OnReSubscriberArgs> value2 = delegate(OnReSubscriberArgs args)
			{
				dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
				{
					OnResub(args, c);
				});
			};
			Action<OnGiftedSubscriptionArgs> value3 = delegate(OnGiftedSubscriptionArgs args)
			{
				dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
				{
					OnGifted(args, c);
				});
			};
			twitch.OnNewSubscriber += value;
			twitch.OnReSubscriber += value2;
			twitch.OnGiftedSubscription += value3;
			_newSub.Write(value, context);
			_reSub.Write(value2, context);
			_gifted.Write(value3, context);
		}

		protected override void Unregister(TwitchInterface twitch, FrooxEngineContext context)
		{
			twitch.OnNewSubscriber -= _newSub.Read(context);
			twitch.OnReSubscriber -= _reSub.Read(context);
			twitch.OnGiftedSubscription -= _gifted.Read(context);
		}

		protected override void Clear(FrooxEngineContext context)
		{
			_newSub.Clear(context);
			_reSub.Clear(context);
			_gifted.Clear(context);
		}

		private void OnNew(OnNewSubscriberArgs args, FrooxEngineContext context)
		{
			SendEvent(args.Subscriber, 0, isResub: false, context);
		}

		private void OnResub(OnReSubscriberArgs args, FrooxEngineContext context)
		{
			SendEvent(args.ReSubscriber, args.ReSubscriber.Months, isResub: true, context);
		}

		private void OnGifted(OnGiftedSubscriptionArgs args, FrooxEngineContext context)
		{
			GiftedSubscription giftedSubscription = args.GiftedSubscription;
			UserId.Write(giftedSubscription.MsgParamRecipientId, context);
			DisplayName.Write(giftedSubscription.MsgParamRecipientDisplayName, context);
			Message.Write(null, context);
			int.TryParse(giftedSubscription.MsgParamMonths, out var result);
			Months.Write(result, context);
			Plan.Write(giftedSubscription.MsgParamSubPlan, context);
			IsResub.Write(value: false, context);
			IsGifted.Write(value: true, context);
			GiftedBy.Write(giftedSubscription.DisplayName, context);
			IsAnonymous.Write(giftedSubscription.IsAnonymous, context);
			OnSubscription.Execute(context);
		}

		private void SendEvent(SubscriberBase args, int months, bool isResub, FrooxEngineContext context)
		{
			UserId.Write(args.UserId, context);
			DisplayName.Write(args.DisplayName, context);
			Message.Write(args.ResubMessage, context);
			Months.Write(months, context);
			Plan.Write(args.SubscriptionPlan, context);
			IsResub.Write(isResub, context);
			IsGifted.Write(value: false, context);
			GiftedBy.Write(null, context);
			IsAnonymous.Write(value: false, context);
			OnSubscription.Execute(context);
		}

		public TwitchSubscriptionEvent()
		{
			UserId = new ObjectOutput<string>(this);
			DisplayName = new ObjectOutput<string>(this);
			Message = new ObjectOutput<string>(this);
			Months = new ValueOutput<int>(this);
			Plan = new ValueOutput<SubscriptionPlan>(this);
			IsResub = new ValueOutput<bool>(this);
			IsGifted = new ValueOutput<bool>(this);
			GiftedBy = new ObjectOutput<string>(this);
			IsAnonymous = new ValueOutput<bool>(this);
		}
	}
	public class TwitchFollowEvent : TwitchEventsNode
	{
		public Call OnFollow;

		public readonly ObjectOutput<string> UserId;

		public readonly ObjectOutput<string> DisplayName;

		private ObjectStore<Action<OnFollowArgs>> _handler;

		protected override void Register(TwitchInterface twitch, NodeContextPath path, ExecutionEventDispatcher<FrooxEngineContext> dispatcher, FrooxEngineContext context)
		{
			Action<OnFollowArgs> value = delegate(OnFollowArgs args)
			{
				dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
				{
					Follow(args, c);
				});
			};
			twitch.OnFollow += value;
			_handler.Write(value, context);
		}

		protected override void Unregister(TwitchInterface twitch, FrooxEngineContext context)
		{
			twitch.OnFollow -= _handler.Read(context);
		}

		protected override void Clear(FrooxEngineContext context)
		{
			_handler.Clear(context);
		}

		private void Follow(OnFollowArgs args, FrooxEngineContext context)
		{
			UserId.Write(args.UserId, context);
			DisplayName.Write(args.DisplayName, context);
			OnFollow.Execute(context);
		}

		public TwitchFollowEvent()
		{
			UserId = new ObjectOutput<string>(this);
			DisplayName = new ObjectOutput<string>(this);
		}
	}
	public class TwitchRewardRedeemEvent : TwitchEventsNode
	{
		public Call OnRedeem;

		public readonly ObjectOutput<string> DisplayName;

		public readonly ObjectOutput<string> Message;

		public readonly ValueOutput<DateTime> TimeStamp;

		public readonly ObjectOutput<string> RewardId;

		public readonly ObjectOutput<string> RewardTitle;

		public readonly ObjectOutput<string> RewardPrompt;

		public readonly ObjectOutput<string> Status;

		public readonly ValueOutput<int> RewardCost;

		private ObjectStore<Action<OnRewardRedeemedArgs>> _handler;

		protected override void Register(TwitchInterface twitch, NodeContextPath path, ExecutionEventDispatcher<FrooxEngineContext> dispatcher, FrooxEngineContext context)
		{
			Action<OnRewardRedeemedArgs> value = delegate(OnRewardRedeemedArgs args)
			{
				dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
				{
					Redeem(args, c);
				});
			};
			twitch.OnRewardRedeemed += value;
			_handler.Write(value, context);
		}

		protected override void Unregister(TwitchInterface twitch, FrooxEngineContext context)
		{
			twitch.OnRewardRedeemed -= _handler.Read(context);
		}

		protected override void Clear(FrooxEngineContext context)
		{
			_handler.Clear(context);
		}

		private void Redeem(OnRewardRedeemedArgs args, FrooxEngineContext context)
		{
			DisplayName.Write(args.DisplayName, context);
			Message.Write(args.Message, context);
			TimeStamp.Write(args.TimeStamp, context);
			RewardId.Write(args.RewardId.ToString(), context);
			RewardTitle.Write(args.RewardTitle, context);
			RewardPrompt.Write(args.RewardPrompt, context);
			Status.Write(args.Status, context);
			RewardCost.Write(args.RewardCost, context);
			OnRedeem.Execute(context);
		}

		public TwitchRewardRedeemEvent()
		{
			DisplayName = new ObjectOutput<string>(this);
			Message = new ObjectOutput<string>(this);
			TimeStamp = new ValueOutput<DateTime>(this);
			RewardId = new ObjectOutput<string>(this);
			RewardTitle = new ObjectOutput<string>(this);
			RewardPrompt = new ObjectOutput<string>(this);
			Status = new ObjectOutput<string>(this);
			RewardCost = new ValueOutput<int>(this);
		}
	}
	public class TwitchRaidEvent : TwitchEventsNode
	{
		public Call OnRaid;

		public readonly ObjectOutput<string> UserId;

		public readonly ObjectOutput<string> DisplayName;

		public readonly ValueOutput<colorX> Color;

		public readonly ValueOutput<int> ViewerCount;

		public readonly ValueOutput<bool> IsSubscriber;

		private ObjectStore<Action<OnRaidNotificationArgs>> _handler;

		protected override void Register(TwitchInterface twitch, NodeContextPath path, ExecutionEventDispatcher<FrooxEngineContext> dispatcher, FrooxEngineContext context)
		{
			Action<OnRaidNotificationArgs> value = delegate(OnRaidNotificationArgs args)
			{
				dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
				{
					Raid(args, c);
				});
			};
			twitch.OnRaidNotification += value;
			_handler.Write(value, context);
		}

		protected override void Unregister(TwitchInterface twitch, FrooxEngineContext context)
		{
			twitch.OnRaidNotification -= _handler.Read(context);
		}

		protected override void Clear(FrooxEngineContext context)
		{
			_handler.Clear(context);
		}

		private void Raid(OnRaidNotificationArgs args, FrooxEngineContext context)
		{
			RaidNotification raidNotification = args.RaidNotification;
			UserId.Write(raidNotification.UserId, context);
			DisplayName.Write(raidNotification.DisplayName, context);
			Color.Write(colorX.FromHexCode(raidNotification.Color, colorX.Black), context);
			int.TryParse(raidNotification.MsgParamViewerCount, out var result);
			ViewerCount.Write(result, context);
			IsSubscriber.Write(raidNotification.Subscriber, context);
			OnRaid.Execute(context);
		}

		public TwitchRaidEvent()
		{
			UserId = new ObjectOutput<string>(this);
			DisplayName = new ObjectOutput<string>(this);
			Color = new ValueOutput<colorX>(this);
			ViewerCount = new ValueOutput<int>(this);
			IsSubscriber = new ValueOutput<bool>(this);
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Locomotion
{
	[NodeCategory("Locomotion")]
	[ContinuouslyChanging]
	public class GetActiveLocomotionModule : ObjectFunctionNode<FrooxEngineContext, ILocomotionModule>
	{
		public ObjectInput<global::FrooxEngine.User> User;

		protected override ILocomotionModule Compute(FrooxEngineContext context)
		{
			return User.Evaluate(context, context.World.LocalUser)?.Root?.GetRegisteredComponent<LocomotionController>()?.ActiveModule;
		}
	}
	[NodeCategory("Locomotion")]
	public class InstallLocomotionModules : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<Slot> ModulesRoot;

		public ObjectInput<global::FrooxEngine.User> TargetUser;

		public ValueInput<bool> ClearExisting;

		protected override bool Do(FrooxEngineContext context)
		{
			Slot slot = ModulesRoot.Evaluate(context);
			if (slot == null || slot.IsRemoved)
			{
				return false;
			}
			global::FrooxEngine.User user = TargetUser.Evaluate(context, context.World.LocalUser);
			if (user == null)
			{
				return false;
			}
			LocomotionController locomotionController = user.Root?.GetRegisteredComponent<LocomotionController>();
			if (locomotionController == null)
			{
				return false;
			}
			if (ClearExisting.Evaluate(context, defaultValue: false))
			{
				locomotionController.LocomotionModulesRoot.DestroyChildren();
				locomotionController.LocomotionModules.Clear();
			}
			List<ILocomotionModule> existingModules = locomotionController.Slot.GetComponentsInChildren<ILocomotionModule>();
			slot.Duplicate(locomotionController.LocomotionModulesRoot, keepGlobalTransform: false);
			locomotionController.LocomotionModules.AddRange(locomotionController.LocomotionModulesRoot.GetComponentsInChildren((ILocomotionModule m) => !existingModules.Contains(m)));
			locomotionController.ValidateLocomotionMode();
			return true;
		}
	}
	[NodeCategory("Locomotion")]
	public class SwitchLocomotionModule : ActionNode<FrooxEngineContext>
	{
		public ObjectInput<global::FrooxEngine.User> TargetUser;

		public ObjectInput<string> ModuleName;

		public ValueInput<bool> ExactMatch;

		public Continuation OnSwitched;

		public Continuation OnNotFound;

		protected override IOperation Run(FrooxEngineContext context)
		{
			global::FrooxEngine.User user = TargetUser.Evaluate(context, context.World.LocalUser);
			string _name = ModuleName.Evaluate(context);
			bool _exact = ExactMatch.Evaluate(context, defaultValue: false);
			if (user == null)
			{
				return null;
			}
			if (string.IsNullOrWhiteSpace(_name))
			{
				return OnNotFound.Target;
			}
			LocomotionController locomotionController = user.Root?.GetRegisteredComponent<LocomotionController>();
			if (locomotionController == null)
			{
				return OnNotFound.Target;
			}
			ILocomotionModule locomotionModule = locomotionController.TryFindModule((ILocomotionModule m) => _exact ? (m.LocomotionName.content == _name) : (m.LocomotionName.content?.Contains(_name) ?? false));
			if (locomotionModule == null)
			{
				return OnNotFound.Target;
			}
			locomotionController.ActiveModule = locomotionModule;
			return OnSwitched.Target;
		}
	}
	[NodeCategory("Locomotion")]
	public class GetLocomotionArchetype : ObjectFunctionNode<FrooxEngineContext, LocomotionArchetype?>
	{
		public ObjectArgument<ILocomotionModule> Module;

		protected override LocomotionArchetype? Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<ILocomotionModule>(context)?.LocomotionArchetype;
		}
	}
	[NodeCategory("Locomotion")]
	public class FootstepEvents : VoidNode<FrooxEngineContext>
	{
		public readonly GlobalRef<IFootstepEventRelay> Source;

		public Call Footstep;

		public readonly ValueOutput<Chirality> Side;

		public readonly ValueOutput<float3> Position;

		public readonly ValueOutput<floatQ> Rotation;

		public readonly ValueOutput<float3> ImpactVelocity;

		public readonly ValueOutput<bool> HasLanded;

		public readonly ObjectOutput<ICollider> HitCollider;

		public readonly ValueOutput<int> HitTriangleIndex;

		private ObjectStore<IFootstepEventRelay> _currentRelay;

		private ObjectStore<FootstepEventHandler> _eventHandler;

		public override bool CanBeEvaluated => false;

		private void OnSourceChanged(IFootstepEventRelay relay, FrooxEngineContext context)
		{
			IFootstepEventRelay footstepEventRelay = _currentRelay.Read(context);
			if (relay == footstepEventRelay)
			{
				return;
			}
			if (footstepEventRelay != null)
			{
				footstepEventRelay.OnLocalFootstep -= _eventHandler.Read(context);
			}
			if (relay != null)
			{
				NodeContextPath path = context.CaptureContextPath();
				context.GetEventDispatcher(out var dispatcher);
				FootstepEventHandler value = delegate(Chirality side, float3 position, floatQ rotation, float3 impactVelocity, bool hasLanded, RaycastHit hit)
				{
					dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
					{
						OnFootstep(side, position, rotation, impactVelocity, hasLanded, hit, c);
					});
				};
				_currentRelay.Write(relay, context);
				_eventHandler.Write(value, context);
				relay.OnLocalFootstep += value;
			}
			else
			{
				_currentRelay.Clear(context);
				_eventHandler.Clear(context);
			}
		}

		private void OnFootstep(Chirality side, float3 position, floatQ rotation, float3 impactVelocity, bool hasLanded, RaycastHit hit, FrooxEngineContext context)
		{
			Side.Write(side, context);
			Position.Write(position, context);
			Rotation.Write(rotation, context);
			ImpactVelocity.Write(impactVelocity, context);
			HasLanded.Write(hasLanded, context);
			HitCollider.Write(hit.Collider, context);
			HitTriangleIndex.Write(hit.TriangleIndex, context);
			Footstep.Execute(context);
		}

		public FootstepEvents()
		{
			Source = new GlobalRef<IFootstepEventRelay>(this, 0);
			Side = new ValueOutput<Chirality>(this);
			Position = new ValueOutput<float3>(this);
			Rotation = new ValueOutput<floatQ>(this);
			ImpactVelocity = new ValueOutput<float3>(this);
			HasLanded = new ValueOutput<bool>(this);
			HitCollider = new ObjectOutput<ICollider>(this);
			HitTriangleIndex = new ValueOutput<int>(this);
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Avatar
{
	[NodeCategory("Avatars")]
	public class EquipAvatar : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<global::FrooxEngine.User> User;

		public ObjectInput<Slot> AvatarRoot;

		public ValueInput<bool> DestroyOld;

		protected override bool Do(FrooxEngineContext context)
		{
			Slot slot = AvatarRoot.Evaluate(context);
			if (slot == null || slot.IsRemoved)
			{
				return false;
			}
			global::FrooxEngine.User user = User.Evaluate(context, context.World.LocalUser);
			if (user == null)
			{
				return false;
			}
			AvatarManager avatarManager = user.Root?.GetRegisteredComponent<AvatarManager>();
			if (avatarManager == null)
			{
				return false;
			}
			avatarManager.Equip(slot, isManualEquip: false, DestroyOld.Evaluate(context, defaultValue: false));
			return true;
		}
	}
	[NodeCategory("Avatars/Body Nodes")]
	public class BodyNodeSlot : ObjectFunctionNode<FrooxEngineContext, Slot>
	{
		public ObjectArgument<global::FrooxEngine.User> Source;

		public ValueArgument<BodyNode> Node;

		protected override Slot Compute(FrooxEngineContext context)
		{
			global::FrooxEngine.User user = ((Source.Source != null) ? 0.ReadObject<global::FrooxEngine.User>(context) : context.World.LocalUser);
			BodyNode node = 1.ReadValue<BodyNode>(context);
			return user.GetBodyNodeSlot(node);
		}
	}
	[NodeCategory("Avatars/Body Nodes")]
	public class BodyNodeSlotInChildren : ObjectFunctionNode<FrooxEngineContext, Slot>
	{
		public ObjectArgument<Slot> Source;

		public ValueArgument<BodyNode> Node;

		protected override Slot Compute(FrooxEngineContext context)
		{
			Slot slot = 0.ReadObject<Slot>(context);
			BodyNode bodyNode = 1.ReadValue<BodyNode>(context);
			if (slot == null || bodyNode == BodyNode.NONE)
			{
				return null;
			}
			Slot slot2 = slot.GetComponentInChildren<BipedRig>()?.TryGetBone(bodyNode);
			if (slot2 != null)
			{
				return slot2;
			}
			return slot.FindSlotForNodeInChildren(bodyNode)?.Slot;
		}
	}
	[NodeCategory("Avatars")]
	[ContinuouslyChanging]
	public class UserFingerPoseSource : ObjectFunctionNode<FrooxEngineContext, IFingerPoseSourceComponent>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override IFingerPoseSourceComponent Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.Root?.GetRegisteredComponent<AvatarFingerPoseInfo>()?.FingerPoseSource.Target;
		}
	}
	[NodeCategory("Avatars")]
	[ContinuouslyChanging]
	public class FingerPose : VoidNode<FrooxEngineContext>
	{
		public ObjectArgument<IFingerPoseSourceComponent> PoseSource;

		public ValueArgument<BodyNode> FingerNode;

		public readonly ValueOutput<float3> Position;

		public readonly ValueOutput<floatQ> Rotation;

		protected override void ComputeOutputs(FrooxEngineContext context)
		{
			IFingerPoseSourceComponent fingerPoseSourceComponent = 0.ReadObject<IFingerPoseSourceComponent>(context);
			if (fingerPoseSourceComponent != null && fingerPoseSourceComponent.TryGetFingerData(1.ReadValue<BodyNode>(context), out var position, out var rotation))
			{
				Position.Write(position, context);
				Rotation.Write(rotation, context);
			}
			else
			{
				Position.Write(default(float3), context);
				Rotation.Write(floatQ.Identity, context);
			}
		}

		public FingerPose()
		{
			Position = new ValueOutput<float3>(this);
			Rotation = new ValueOutput<floatQ>(this);
		}
	}
	[DataModelType]
	public interface INearestData
	{
		float Distance { get; set; }

		global::FrooxEngine.User User { get; set; }

		Slot Slot { get; set; }
	}
	[NodeCategory("Avatars")]
	public abstract class NearestUserNode<D> : VoidNode<FrooxEngineContext>, IMappableNode, INode where D : struct, INearestData
	{
		public ObjectInput<Slot> Reference;

		public ObjectInput<global::FrooxEngine.User> IgnoreUser;

		public ValueInput<bool> IgnoreAFK;

		[ContinuouslyChanging]
		public readonly ObjectOutput<Slot> Slot;

		[ContinuouslyChanging]
		public readonly ObjectOutput<global::FrooxEngine.User> User;

		[ContinuouslyChanging]
		public readonly ValueOutput<float> Distance;

		private ValueStore<int> _cachedFrame;

		private ObjectStore<Slot> _slot;

		private ObjectStore<global::FrooxEngine.User> _user;

		private ValueStore<float> _distance;

		protected override void ComputeOutputs(FrooxEngineContext context)
		{
			ref int reference = ref _cachedFrame.Access(context);
			if (reference != context.Time.LocalUpdateIndex)
			{
				ComputeData(context);
				reference = context.Time.LocalUpdateIndex;
			}
			Slot.Write(_slot.Read(context), context);
			User.Write(_user.Read(context), context);
			Distance.Write(_distance.Read(context), context);
			WriteOutputs(context);
		}

		private void ComputeData(FrooxEngineContext context)
		{
			Slot obj = Reference.Evaluate(context).FilterWorldElement() ?? context.GetRootSlotContainer(this);
			global::FrooxEngine.User user = IgnoreUser.Evaluate(context);
			bool flag = IgnoreAFK.Evaluate(context, defaultValue: false);
			D nearest = new D
			{
				Distance = float.MaxValue
			};
			Initialize(ref nearest, context);
			float3 referencePos = obj.GlobalPosition;
			foreach (global::FrooxEngine.User allUser in context.World.AllUsers)
			{
				if (allUser != user && (!flag || allUser.IsPresentInWorld))
				{
					UpdateNearest(allUser, in referencePos, ref nearest, context);
				}
			}
			_slot.Write(nearest.Slot, context);
			_user.Write(nearest.User, context);
			_distance.Write(nearest.Distance, context);
			StoreNearest(ref nearest, context);
		}

		protected abstract void Initialize(ref D nearest, FrooxEngineContext context);

		protected abstract void UpdateNearest(global::FrooxEngine.User user, in float3 referencePos, ref D nearest, FrooxEngineContext context);

		protected abstract void StoreNearest(ref D nearest, FrooxEngineContext context);

		protected abstract void WriteOutputs(FrooxEngineContext context);

		protected NearestUserNode()
		{
			((NearestUserNode<>)(object)this).Slot = new ObjectOutput<Slot>(this);
			((NearestUserNode<>)(object)this).User = new ObjectOutput<global::FrooxEngine.User>(this);
			((NearestUserNode<>)(object)this).Distance = new ValueOutput<float>(this);
		}
	}
	public class NearestUserHead : NearestUserNode<NearestUserHead.Data>
	{
		public struct Data : INearestData
		{
			public float Distance { get; set; }

			public global::FrooxEngine.User User { get; set; }

			public Slot Slot { get; set; }
		}

		protected override void Initialize(ref Data nearest, FrooxEngineContext context)
		{
		}

		protected override void UpdateNearest(global::FrooxEngine.User user, in float3 referencePos, ref Data nearest, FrooxEngineContext context)
		{
			Slot slot = user.Root?.HeadSlot;
			if (slot != null)
			{
				float num = MathX.Distance(slot.GlobalPosition, in referencePos);
				if (num < nearest.Distance)
				{
					nearest.Distance = num;
					nearest.User = user;
					nearest.Slot = slot;
				}
			}
		}

		protected override void StoreNearest(ref Data nearest, FrooxEngineContext context)
		{
		}

		protected override void WriteOutputs(FrooxEngineContext context)
		{
		}
	}
	public class NearestUserHand : NearestUserNode<NearestUserHand.Data>
	{
		public struct Data : INearestData
		{
			public Chirality Chirality;

			public bool GetLeft;

			public bool GetRight;

			public float Distance { get; set; }

			public global::FrooxEngine.User User { get; set; }

			public Slot Slot { get; set; }
		}

		[ProtoFlux.Core.DefaultValue(true)]
		public ValueInput<bool> GetLeft;

		[ProtoFlux.Core.DefaultValue(true)]
		public ValueInput<bool> GetRight;

		[ContinuouslyChanging]
		public readonly ValueOutput<Chirality> Chirality;

		private ValueStore<Chirality> _chirality;

		protected override void Initialize(ref Data nearest, FrooxEngineContext context)
		{
			nearest.GetLeft = GetLeft.Evaluate(context, defaultValue: true);
			nearest.GetRight = GetRight.Evaluate(context, defaultValue: true);
			nearest.Chirality = (Chirality)(-1);
		}

		protected override void UpdateNearest(global::FrooxEngine.User user, in float3 referencePos, ref Data nearest, FrooxEngineContext context)
		{
			Slot slot = user.Root?.LeftHandSlot ?? user.Root?.LeftControllerSlot;
			Slot slot2 = user.Root?.RightHandSlot ?? user.Root?.RightControllerSlot;
			if (nearest.GetLeft && slot != null)
			{
				float num = MathX.Distance(slot.GlobalPosition, in referencePos);
				if (num < nearest.Distance)
				{
					nearest.Slot = slot;
					nearest.Distance = num;
					nearest.User = user;
					nearest.Chirality = Renderite.Shared.Chirality.Left;
				}
			}
			if (nearest.GetRight && slot2 != null)
			{
				float num2 = MathX.Distance(slot2.GlobalPosition, in referencePos);
				if (num2 < nearest.Distance)
				{
					nearest.Slot = slot2;
					nearest.Distance = num2;
					nearest.User = user;
					nearest.Chirality = Renderite.Shared.Chirality.Right;
				}
			}
		}

		protected override void StoreNearest(ref Data nearest, FrooxEngineContext context)
		{
			_chirality.Write(nearest.Chirality, context);
		}

		protected override void WriteOutputs(FrooxEngineContext context)
		{
			Chirality.Write(_chirality.Read(context), context);
		}

		public NearestUserHand()
		{
			Chirality = new ValueOutput<Chirality>(this);
		}
	}
	public class NearestUserFoot : NearestUserNode<NearestUserFoot.Data>
	{
		public struct Data : INearestData
		{
			public Chirality Chirality;

			public bool GetLeft;

			public bool GetRight;

			public float Distance { get; set; }

			public global::FrooxEngine.User User { get; set; }

			public Slot Slot { get; set; }
		}

		[ProtoFlux.Core.DefaultValue(true)]
		public ValueInput<bool> GetLeft;

		[ProtoFlux.Core.DefaultValue(true)]
		public ValueInput<bool> GetRight;

		[ContinuouslyChanging]
		public readonly ValueOutput<Chirality> Chirality;

		private ValueStore<Chirality> _chirality;

		protected override void Initialize(ref Data nearest, FrooxEngineContext context)
		{
			nearest.GetLeft = GetLeft.Evaluate(context, defaultValue: true);
			nearest.GetRight = GetRight.Evaluate(context, defaultValue: true);
			nearest.Chirality = (Chirality)(-1);
		}

		protected override void UpdateNearest(global::FrooxEngine.User user, in float3 referencePos, ref Data nearest, FrooxEngineContext context)
		{
			Slot slot = user.Root?.LeftFootSlot ?? user.GetBodyNodeSlot(BodyNode.LeftFoot);
			Slot slot2 = user.Root?.RightFootSlot ?? user.GetBodyNodeSlot(BodyNode.RightFoot);
			if (nearest.GetLeft && slot != null)
			{
				float num = MathX.Distance(slot.GlobalPosition, in referencePos);
				if (num < nearest.Distance)
				{
					nearest.Slot = slot;
					nearest.Distance = num;
					nearest.User = user;
					nearest.Chirality = Renderite.Shared.Chirality.Left;
				}
			}
			if (nearest.GetRight && slot2 != null)
			{
				float num2 = MathX.Distance(slot2.GlobalPosition, in referencePos);
				if (num2 < nearest.Distance)
				{
					nearest.Slot = slot2;
					nearest.Distance = num2;
					nearest.User = user;
					nearest.Chirality = Renderite.Shared.Chirality.Right;
				}
			}
		}

		protected override void StoreNearest(ref Data nearest, FrooxEngineContext context)
		{
			_chirality.Write(nearest.Chirality, context);
		}

		protected override void WriteOutputs(FrooxEngineContext context)
		{
			Chirality.Write(_chirality.Read(context), context);
		}

		public NearestUserFoot()
		{
			Chirality = new ValueOutput<Chirality>(this);
		}
	}
	[NodeCategory("Avatars")]
	[NodeOverload("Engine.Avatars.DefaultUserScale")]
	public class DefaultUserScale : ValueFunctionNode<FrooxEngineContext, float>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override float Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<global::FrooxEngine.User>(context)?.GetDefaultScale() ?? 0f;
		}
	}
	[NodeCategory("Avatars")]
	[NodeOverload("Engine.Avatars.DefaultUserScale")]
	public class DefaultUserRootScale : ValueFunctionNode<FrooxEngineContext, float>
	{
		public ObjectArgument<UserRoot> User;

		protected override float Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<UserRoot>(context)?.GetDefaultScale() ?? 0f;
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Avatar.BodyNodes
{
	[NodeCategory("Avatars/Body Nodes")]
	public abstract class BodyNodeConverter<T> : ValueFunctionNode<ExecutionContext, T> where T : unmanaged
	{
		public ValueArgument<BodyNode> Node;

		protected override T Compute(ExecutionContext context)
		{
			return Convert(0.ReadValue<BodyNode>(context), context);
		}

		protected virtual T Convert(BodyNode node)
		{
			throw new NotImplementedException();
		}

		protected virtual T Convert(BodyNode node, ExecutionContext context)
		{
			return Convert(node);
		}
	}
	public class BodyNodeChirality : BodyNodeConverter<Chirality>
	{
		protected override Chirality Convert(BodyNode node)
		{
			return node.GetChirality();
		}
	}
	public class RelativeBodyNode : BodyNodeConverter<BodyNode>
	{
		protected override BodyNode Convert(BodyNode node)
		{
			return node.GetRelativeNode();
		}
	}
	public class FingerNodeIndex : BodyNodeConverter<int>
	{
		protected override int Convert(BodyNode node)
		{
			Chirality chirality;
			return node.GetFingerNodeIndex(out chirality);
		}
	}
	public class IsEye : BodyNodeConverter<bool>
	{
		protected override bool Convert(BodyNode node)
		{
			return node.IsEye();
		}
	}
	public class OtherSide : BodyNodeConverter<BodyNode>
	{
		protected override BodyNode Convert(BodyNode node)
		{
			return node.GetOtherSide();
		}
	}
	public class GetSide : BodyNodeConverter<BodyNode>
	{
		public ValueArgument<Chirality> Side;

		protected override BodyNode Convert(BodyNode node, ExecutionContext context)
		{
			return node.GetSide(1.ReadValue<Chirality>(context));
		}
	}
	public class GetFingerType : BodyNodeConverter<FingerType>
	{
		protected override FingerType Convert(BodyNode node)
		{
			return node.GetFingerType();
		}
	}
	public class GetFingerSegmentType : BodyNodeConverter<FingerSegmentType>
	{
		protected override FingerSegmentType Convert(BodyNode node)
		{
			return node.GetFingerSegmentType();
		}
	}
	[NodeCategory("Avatars/Body Nodes")]
	public class ComposeFinger : ValueFunctionNode<FrooxEngineContext, BodyNode>
	{
		public ValueArgument<FingerType> Finger;

		public ValueArgument<FingerSegmentType> Segment;

		public ValueArgument<Chirality> Chirality;

		protected override BodyNode Compute(FrooxEngineContext context)
		{
			return 0.ReadValue<FingerType>(context).ComposeFinger(1.ReadValue<FingerSegmentType>(context), 2.ReadValue<Chirality>(context), throwOnInvalid: false);
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Avatar.Anchors
{
	[NodeCategory("Avatars/Anchors")]
	public abstract class AnchorEventsBase : VoidNode<FrooxEngineContext>
	{
		public readonly GlobalRef<AvatarAnchor> Anchor;

		private ObjectStore<AvatarAnchor> _current;

		private ObjectStore<AvatarAnchorUserEvent> _anchored;

		private ObjectStore<AvatarAnchorUserEvent> _released;

		public override bool CanBeEvaluated => false;

		private void OnAnchorChanged(AvatarAnchor anchor, FrooxEngineContext context)
		{
			AvatarAnchor avatarAnchor = _current.Read(context);
			if (anchor == avatarAnchor)
			{
				return;
			}
			if (avatarAnchor != null)
			{
				avatarAnchor.LocalUserAnchored -= _anchored.Read(context);
				avatarAnchor.LocalUserReleased -= _released.Read(context);
				Unregister(avatarAnchor, context);
			}
			if (anchor != null)
			{
				NodeContextPath path = context.CaptureContextPath();
				context.GetEventDispatcher(out var dispatcher);
				AvatarAnchorUserEvent value = delegate(AvatarAnchor a, global::FrooxEngine.User u)
				{
					dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
					{
						HandleAnchored(u, c);
					});
				};
				AvatarAnchorUserEvent value2 = delegate(AvatarAnchor a, global::FrooxEngine.User u)
				{
					dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
					{
						HandleReleased(u, c);
					});
				};
				anchor.LocalUserAnchored += value;
				anchor.LocalUserReleased += value2;
				Register(anchor, path, dispatcher, context);
				_current.Write(anchor, context);
				_anchored.Write(value, context);
				_released.Write(value2, context);
			}
			else
			{
				_current.Clear(context);
				_anchored.Clear(context);
				_released.Clear(context);
				Clear(context);
			}
		}

		protected abstract void Register(AvatarAnchor anchor, NodeContextPath path, ExecutionEventDispatcher<FrooxEngineContext> dispatcher, FrooxEngineContext context);

		protected abstract void Unregister(AvatarAnchor anchor, FrooxEngineContext context);

		protected abstract void Clear(FrooxEngineContext context);

		protected abstract void HandleAnchored(global::FrooxEngine.User user, FrooxEngineContext context);

		protected abstract void HandleReleased(global::FrooxEngine.User user, FrooxEngineContext context);

		protected AnchorEventsBase()
		{
			Anchor = new GlobalRef<AvatarAnchor>(this, 0);
		}
	}
	public class AnchorEvents : AnchorEventsBase
	{
		public Call OnAnchored;

		public Call OnReleased;

		public readonly ObjectOutput<global::FrooxEngine.User> User;

		protected override void Register(AvatarAnchor anchor, NodeContextPath path, ExecutionEventDispatcher<FrooxEngineContext> dispatcher, FrooxEngineContext context)
		{
		}

		protected override void Unregister(AvatarAnchor anchor, FrooxEngineContext context)
		{
		}

		protected override void Clear(FrooxEngineContext context)
		{
		}

		protected override void HandleAnchored(global::FrooxEngine.User user, FrooxEngineContext context)
		{
			User.Write(user, context);
			OnAnchored.Execute(context);
		}

		protected override void HandleReleased(global::FrooxEngine.User user, FrooxEngineContext context)
		{
			User.Write(user, context);
			OnReleased.Execute(context);
		}

		public AnchorEvents()
		{
			User = new ObjectOutput<global::FrooxEngine.User>(this);
		}
	}
	public class AnchorLocomotionData : AnchorEventsBase, IMappableNode, INode
	{
		public Call OnLocomotionUpdate;

		public readonly ValueOutput<bool> HasPrimary;

		public readonly ValueOutput<bool> HasSecondary;

		public readonly ValueOutput<float2> PrimaryAxis;

		public readonly ValueOutput<float2> SecondaryAxis;

		public readonly ValueOutput<bool> PrimaryAction;

		public readonly ValueOutput<bool> SecondaryAction;

		private ObjectStore<AvatarAnchorUserEvent> _userStay;

		private ObjectStore<AnchorLocomotionInputs> _locomotionInputs;

		private NodeEventHandler<FrooxEngineContext> _handler;

		protected override void Register(AvatarAnchor anchor, NodeContextPath path, ExecutionEventDispatcher<FrooxEngineContext> dispatcher, FrooxEngineContext context)
		{
			if (_handler == null)
			{
				_handler = HandleStay;
			}
			AvatarAnchorUserEvent value = delegate(AvatarAnchor a, global::FrooxEngine.User u)
			{
				dispatcher.ScheduleEvent(path, _handler, u);
			};
			anchor.LocalUserStay += value;
			_userStay.Write(value, context);
		}

		protected override void Unregister(AvatarAnchor anchor, FrooxEngineContext context)
		{
			UnregisterInputs(context);
			anchor.LocalUserStay -= _userStay.Read(context);
		}

		protected override void Clear(FrooxEngineContext context)
		{
			_userStay.Clear(context);
			UnregisterInputs(context);
		}

		protected override void HandleAnchored(global::FrooxEngine.User user, FrooxEngineContext context)
		{
			AnchorLocomotionInputs anchorLocomotionInputs = new AnchorLocomotionInputs();
			_locomotionInputs.Write(anchorLocomotionInputs, context);
			context.World.Input.RegisterInputGroup(anchorLocomotionInputs, context.GetRootContainer(this));
		}

		protected override void HandleReleased(global::FrooxEngine.User user, FrooxEngineContext context)
		{
			if (user.IsLocalUser)
			{
				UnregisterInputs(context);
			}
		}

		private void UnregisterInputs(FrooxEngineContext context)
		{
			AnchorLocomotionInputs group = _locomotionInputs.Read(context);
			if (group != null)
			{
				context.World.Input.UnregisterInputGroup(ref group);
				_locomotionInputs.Clear(context);
			}
		}

		private void HandleStay(FrooxEngineContext context, object userObj)
		{
			if (userObj is global::FrooxEngine.User { IsLocalUser: not false })
			{
				AnchorLocomotionInputs anchorLocomotionInputs = _locomotionInputs.Read(context);
				if (anchorLocomotionInputs != null)
				{
					HasPrimary.Write(anchorLocomotionInputs.PrimaryAction.HasValue && anchorLocomotionInputs.PrimaryAxis.HasValue, context);
					HasSecondary.Write(anchorLocomotionInputs.SecondaryAction.HasValue && anchorLocomotionInputs.SecondaryAxis.HasValue, context);
					PrimaryAxis.Write(anchorLocomotionInputs.PrimaryAxis, context);
					SecondaryAxis.Write(anchorLocomotionInputs.SecondaryAxis, context);
					PrimaryAction.Write(anchorLocomotionInputs.PrimaryAction, context);
					SecondaryAction.Write(anchorLocomotionInputs.SecondaryAction, context);
					OnLocomotionUpdate.Execute(context);
				}
			}
		}

		public AnchorLocomotionData()
		{
			HasPrimary = new ValueOutput<bool>(this);
			HasSecondary = new ValueOutput<bool>(this);
			PrimaryAxis = new ValueOutput<float2>(this);
			SecondaryAxis = new ValueOutput<float2>(this);
			PrimaryAction = new ValueOutput<bool>(this);
			SecondaryAction = new ValueOutput<bool>(this);
		}
	}
	[NodeCategory("Avatars/Anchors")]
	[ContinuouslyChanging]
	public class AnchoredUser : ObjectFunctionNode<FrooxEngineContext, global::FrooxEngine.User>
	{
		public ObjectArgument<IAvatarAnchor> Anchor;

		protected override global::FrooxEngine.User Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<IAvatarAnchor>(context)?.AnchoredUser;
		}
	}
	[NodeCategory("Avatars/Anchors")]
	[ContinuouslyChanging]
	public class GetUserAnchor : ObjectFunctionNode<FrooxEngineContext, IAvatarAnchor>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override IAvatarAnchor Compute(FrooxEngineContext context)
		{
			if (User.Source == null)
			{
				return context.World.LocalUser?.GetCurrentAnchor();
			}
			return 0.ReadObject<global::FrooxEngine.User>(context)?.GetCurrentAnchor();
		}
	}
	[NodeCategory("Avatars/Anchors")]
	[ContinuouslyChanging]
	public class IsUserAnchored : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<global::FrooxEngine.User> User;

		protected override bool Compute(FrooxEngineContext context)
		{
			if (User.Source == null)
			{
				return context.World.LocalUser?.IsAnchored() ?? false;
			}
			return 0.ReadObject<global::FrooxEngine.User>(context)?.IsAnchored() ?? false;
		}
	}
	[NodeCategory("Avatars/Anchors")]
	[ContinuouslyChanging]
	public class IsAnchorOccupied : ValueFunctionNode<FrooxEngineContext, bool>
	{
		public ObjectArgument<IAvatarAnchor> Anchor;

		protected override bool Compute(FrooxEngineContext context)
		{
			return 0.ReadObject<IAvatarAnchor>(context)?.IsOccupied ?? false;
		}
	}
	[NodeCategory("Avatars/Anchors")]
	public class AnchorUser : ActionNode<FrooxEngineContext>
	{
		public ObjectInput<IAvatarAnchor> Anchor;

		public ObjectInput<global::FrooxEngine.User> User;

		public Continuation OnAnchored;

		public Continuation OnFailure;

		protected override IOperation Run(FrooxEngineContext context)
		{
			global::FrooxEngine.User user = User.Evaluate(context, context.World.LocalUser);
			if (user == null)
			{
				return OnFailure.Target;
			}
			IAvatarAnchor avatarAnchor = Anchor.Evaluate(context);
			if (avatarAnchor == null || avatarAnchor.IsOccupied)
			{
				return OnFailure.Target;
			}
			avatarAnchor.Anchor(user);
			return OnAnchored.Target;
		}
	}
	[NodeCategory("Avatars/Anchors")]
	public class ReleaseUser : ActionNode<FrooxEngineContext>
	{
		public ObjectInput<IAvatarAnchor> Anchor;

		public Continuation OnReleased;

		public Continuation OnFailure;

		protected override IOperation Run(FrooxEngineContext context)
		{
			IAvatarAnchor avatarAnchor = Anchor.Evaluate(context);
			if (avatarAnchor == null || !avatarAnchor.IsOccupied)
			{
				return OnFailure.Target;
			}
			avatarAnchor.Release();
			return OnReleased.Target;
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Audio
{
	[NodeCategory("Audio")]
	public class PlayOneShot : ActionNode<FrooxEngineContext>, IMappableNode, INode
	{
		public ObjectInput<IAssetProvider<AudioClip>> Clip;

		[@DefaultValue(1f)]
		public ValueInput<float> Volume;

		[@DefaultValue(1f)]
		public ValueInput<float> Speed;

		[@DefaultValue(true)]
		public ValueInput<bool> Spatialize;

		[@DefaultValue(1f)]
		public ValueInput<float> SpatialBlend;

		public ObjectInput<bool?> Global;

		public ValueInput<float3> Point;

		public ObjectInput<Slot> Root;

		[@DefaultValue(true)]
		public ValueInput<bool> ParentUnderRoot;

		[@DefaultValue(128)]
		public ValueInput<int> Priority;

		[@DefaultValue(1f)]
		public ValueInput<float> Doppler;

		[@DefaultValue(1f)]
		public ValueInput<float> MinDistance;

		[@DefaultValue(500f)]
		public ValueInput<float> MaxDistance;

		[@DefaultValue(AudioRolloffCurve.LogarithmicFadeOff)]
		public ValueInput<AudioRolloffCurve> Rolloff;

		[@DefaultValue(AudioDistanceSpace.Global)]
		public ValueInput<AudioDistanceSpace> DistanceSpace;

		[@DefaultValue(0f)]
		public ValueInput<float> MinScale;

		[@DefaultValue(float.PositiveInfinity)]
		public ValueInput<float> MaxScale;

		[@DefaultValue(AudioTypeGroup.SoundEffect)]
		public ValueInput<AudioTypeGroup> Group;

		public ValueInput<bool> IgnoreAudioEffects;

		[@DefaultValue(false)]
		public ValueInput<bool> LocalOnly;

		public readonly ObjectOutput<global::FrooxEngine.AudioOutput> Audio;

		public Continuation OnStartedPlaying;

		protected override IOperation Run(FrooxEngineContext context)
		{
			IAssetProvider<AudioClip> assetProvider = Clip.Evaluate(context);
			if (assetProvider == null)
			{
				return null;
			}
			Slot slot = ((Root.Source != null) ? Root.Evaluate(context) : context.GetRootSlotContainer(this));
			if (slot == null)
			{
				return null;
			}
			bool flag = !slot.IsRootSlot && ParentUnderRoot.Evaluate(context, defaultValue: true);
			global::FrooxEngine.AudioOutput audioOutput = context.World.PlayOneShot(slot.LocalPointToGlobal(Point.Evaluate(context)), assetProvider, Volume.Evaluate(context, 1f), Spatialize.Evaluate(context, defaultValue: true), Global.Evaluate(context), Speed.Evaluate(context, 1f), flag ? slot.FilterWorldElement() : null, DistanceSpace.Evaluate(context, AudioDistanceSpace.Global), LocalOnly.Evaluate(context, defaultValue: false));
			audioOutput.SpatialBlend.Value = SpatialBlend.Evaluate(context, 1f);
			audioOutput.DopplerLevel.Value = Doppler.Evaluate(context, 1f);
			audioOutput.MinDistance.Value = MinDistance.Evaluate(context, 1f);
			audioOutput.MaxDistance.Value = MaxDistance.Evaluate(context, 500f);
			audioOutput.RolloffMode.Value = Rolloff.Evaluate(context, AudioRolloffCurve.LogarithmicFadeOff);
			audioOutput.DistanceSpace.Value = DistanceSpace.Evaluate(context, AudioDistanceSpace.Global);
			audioOutput.MinScale.Value = MinScale.Evaluate(context, 0f);
			audioOutput.MaxScale.Value = MaxScale.Evaluate(context, float.PositiveInfinity);
			audioOutput.Priority.Value = Priority.Evaluate(context, 128);
			audioOutput.IgnoreAudioEffects.Value = IgnoreAudioEffects.Evaluate(context, defaultValue: false);
			audioOutput.AudioTypeGroup.Value = Group.Evaluate(context, AudioTypeGroup.SoundEffect);
			Audio.Write(audioOutput, context);
			return OnStartedPlaying.Target;
		}

		public PlayOneShot()
		{
			Audio = new ObjectOutput<global::FrooxEngine.AudioOutput>(this);
		}
	}
	[NodeCategory("Audio")]
	public class PlayOneShotAndWait : AsyncActionNode<FrooxEngineContext>, IMappableNode, INode
	{
		public ObjectInput<IAssetProvider<AudioClip>> Clip;

		[@DefaultValue(1f)]
		public ValueInput<float> Volume;

		[@DefaultValue(1f)]
		public ValueInput<float> Speed;

		[@DefaultValue(true)]
		public ValueInput<bool> Spatialize;

		[@DefaultValue(1f)]
		public ValueInput<float> SpatialBlend;

		public ObjectInput<bool?> Global;

		public ValueInput<float3> Point;

		public ObjectInput<Slot> Root;

		[@DefaultValue(true)]
		public ValueInput<bool> ParentUnderRoot;

		[@DefaultValue(128)]
		public ValueInput<int> Priority;

		[@DefaultValue(1f)]
		public ValueInput<float> Doppler;

		[@DefaultValue(1f)]
		public ValueInput<float> MinDistance;

		[@DefaultValue(500f)]
		public ValueInput<float> MaxDistance;

		[@DefaultValue(AudioRolloffCurve.LogarithmicFadeOff)]
		public ValueInput<AudioRolloffCurve> Rolloff;

		[@DefaultValue(AudioDistanceSpace.Global)]
		public ValueInput<AudioDistanceSpace> DistanceSpace;

		[@DefaultValue(0f)]
		public ValueInput<float> MinScale;

		[@DefaultValue(float.PositiveInfinity)]
		public ValueInput<float> MaxScale;

		[@DefaultValue(AudioTypeGroup.SoundEffect)]
		public ValueInput<AudioTypeGroup> Group;

		public ValueInput<bool> IgnoreAudioEffects;

		[@DefaultValue(false)]
		public ValueInput<bool> LocalOnly;

		public readonly ObjectOutput<global::FrooxEngine.AudioOutput> Audio;

		public AsyncCall OnStartedPlaying;

		public Continuation OnFinishedPlaying;

		protected override async Task<IOperation> RunAsync(FrooxEngineContext context)
		{
			IAssetProvider<AudioClip> assetProvider = Clip.Evaluate(context);
			if (assetProvider == null)
			{
				return null;
			}
			Slot slot = ((Root.Source != null) ? Root.Evaluate(context) : context.GetRootSlotContainer(this));
			if (slot == null)
			{
				return null;
			}
			bool flag = !slot.IsRootSlot && ParentUnderRoot.Evaluate(context, defaultValue: true);
			global::FrooxEngine.AudioOutput player = context.World.PlayOneShot(slot.LocalPointToGlobal(Point.Evaluate(context)), assetProvider, Volume.Evaluate(context, 1f), Spatialize.Evaluate(context, defaultValue: true), Global.Evaluate(context), Speed.Evaluate(context, 1f), flag ? slot.FilterWorldElement() : null, DistanceSpace.Evaluate(context, AudioDistanceSpace.Global), LocalOnly.Evaluate(context, defaultValue: false));
			player.SpatialBlend.Value = SpatialBlend.Evaluate(context, 1f);
			player.DopplerLevel.Value = Doppler.Evaluate(context, 1f);
			player.MinDistance.Value = MinDistance.Evaluate(context, 1f);
			player.MaxDistance.Value = MaxDistance.Evaluate(context, 500f);
			player.RolloffMode.Value = Rolloff.Evaluate(context, AudioRolloffCurve.LogarithmicFadeOff);
			player.DistanceSpace.Value = DistanceSpace.Evaluate(context, AudioDistanceSpace.Global);
			player.MinScale.Value = MinScale.Evaluate(context, 0f);
			player.MaxScale.Value = MaxScale.Evaluate(context, float.PositiveInfinity);
			player.Priority.Value = Priority.Evaluate(context, 128);
			player.IgnoreAudioEffects.Value = IgnoreAudioEffects.Evaluate(context, defaultValue: false);
			player.AudioTypeGroup.Value = Group.Evaluate(context, AudioTypeGroup.SoundEffect);
			Audio.Write(player, context);
			await OnStartedPlaying.ExecuteAsync(context);
			AudioClipPlayer clipPlayer = (AudioClipPlayer)player.Source.Target;
			while (!clipPlayer.Clip.IsAssetAvailable)
			{
				await default(NextUpdate);
				if (clipPlayer.IsRemoved || clipPlayer.Clip.Target == null)
				{
					return null;
				}
			}
			do
			{
				await default(NextUpdate);
			}
			while (clipPlayer.IsPlaying);
			return OnFinishedPlaying.Target;
		}

		public PlayOneShotAndWait()
		{
			Audio = new ObjectOutput<global::FrooxEngine.AudioOutput>(this);
		}
	}
	[NodeCategory("Audio")]
	public class ConstructZitaParameters : ValueFunctionNode<ExecutionContext, ZitaParameters>
	{
		[ProtoFlux.Core.DefaultValue(0f)]
		public ValueArgument<float> InDelay;

		[ProtoFlux.Core.DefaultValue(200f)]
		public ValueArgument<float> Crossover;

		[ProtoFlux.Core.DefaultValue(1.49f)]
		public ValueArgument<float> RT60Low;

		[ProtoFlux.Core.DefaultValue(1.2f)]
		public ValueArgument<float> RT60Mid;

		[ProtoFlux.Core.DefaultValue(6000f)]
		public ValueArgument<float> HighFrequencyDamping;

		[ProtoFlux.Core.DefaultValue(250f)]
		public ValueArgument<float> EQ1Frequency;

		[ProtoFlux.Core.DefaultValue(0f)]
		public ValueArgument<float> EQ1Level;

		[ProtoFlux.Core.DefaultValue(5000f)]
		public ValueArgument<float> EQ2Frequency;

		[ProtoFlux.Core.DefaultValue(0f)]
		public ValueArgument<float> EQ2Level;

		[ProtoFlux.Core.DefaultValue(0.7f)]
		public ValueArgument<float> Mix;

		[ProtoFlux.Core.DefaultValue(8f)]
		public ValueArgument<float> Level;

		protected override ZitaParameters Compute(ExecutionContext context)
		{
			return new ZitaParameters
			{
				InDelay = 0.ReadValue<float>(context),
				Crossover = 1.ReadValue<float>(context),
				RT60Low = 2.ReadValue<float>(context),
				RT60Mid = 3.ReadValue<float>(context),
				HighFrequencyDamping = 4.ReadValue<float>(context),
				EQ1Frequency = 5.ReadValue<float>(context),
				EQ1Level = 6.ReadValue<float>(context),
				EQ2Frequency = 7.ReadValue<float>(context),
				EQ2Level = 8.ReadValue<float>(context),
				Mix = 9.ReadValue<float>(context),
				Level = 10.ReadValue<float>(context)
			};
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Async
{
	[NodeName("Updates Delay", false)]
	[NodeCategory("Flow/Async")]
	[OldTypeName("ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Async.UpdatesDelay", null)]
	public class DelayUpdates : AsyncActionFlowNode<FrooxEngineContext>
	{
		public AsyncCall OnTriggered;

		[ProtoFlux.Core.DefaultValue(1)]
		public ValueInput<int> Updates;

		protected virtual void BeforeUpdate(FrooxEngineContext context)
		{
		}

		protected override async Task Do(FrooxEngineContext context)
		{
			int updates = Updates.Evaluate(context, 1);
			BeforeUpdate(context);
			if (OnTriggered.Target == null)
			{
				await new Updates(updates);
				return;
			}
			Task delayTask = context.World.Coroutines.StartTask(async delegate
			{
				await new Updates(updates);
			});
			await OnTriggered.ExecuteAsync(context);
			await delayTask;
		}
	}
	[NodeName("Updates Delay with Data", false)]
	[NodeOverload("Engine.UpdatesDelayWithData")]
	[NodeCategory("Flow/Async")]
	[OldTypeName("ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Async.UpdatesDelayWithValue", null)]
	public class DelayUpdatesWithValue<T> : DelayUpdates where T : unmanaged
	{
		public ValueInput<T> Value;

		public readonly ValueOutput<T> DelayedValue;

		protected override void BeforeUpdate(FrooxEngineContext context)
		{
			DelayedValue.Write(Value.Evaluate(context), context);
		}

		public DelayUpdatesWithValue()
		{
			((DelayUpdatesWithValue<>)(object)this).DelayedValue = new ValueOutput<T>(this);
		}
	}
	[NodeName("Updates Delay with Data", false)]
	[NodeOverload("Engine.UpdatesDelayWithData")]
	[NodeCategory("Flow/Async")]
	[OldTypeName("ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Async.UpdatesDelayWithObject", null)]
	public class DelayUpdatesWithObject<T> : DelayUpdates
	{
		public ObjectInput<T> Value;

		public readonly ObjectOutput<T> DelayedValue;

		protected override void BeforeUpdate(FrooxEngineContext context)
		{
			DelayedValue.Write(Value.Evaluate(context), context);
		}

		public DelayUpdatesWithObject()
		{
			((DelayUpdatesWithObject<>)(object)this).DelayedValue = new ObjectOutput<T>(this);
		}
	}
	[NodeCategory("Flow/Async")]
	[NodeName("Updates or Time Delay", false)]
	[NodeOverload("Engine.UpdatesOrTimeDelay")]
	public abstract class DelayUpdatesOrTime : AsyncActionFlowNode<FrooxEngineContext>
	{
		public AsyncCall OnTriggered;

		[ProtoFlux.Core.DefaultValue(1)]
		public ValueInput<int> Updates;

		protected virtual void BeforeDelay(FrooxEngineContext context)
		{
		}

		protected abstract TimeSpan GetDuration(FrooxEngineContext context);

		protected override async Task Do(FrooxEngineContext context)
		{
			TimeSpan duration = GetDuration(context);
			int updates = Updates.Evaluate(context, 1);
			int frameIndex = context.World.Time.LocalUpdateIndex;
			Task delayTask = Task.Delay(duration);
			BeforeDelay(context);
			await OnTriggered.ExecuteAsync(context);
			await delayTask;
			int localUpdateIndex = context.World.Time.LocalUpdateIndex;
			updates -= localUpdateIndex - frameIndex;
			if (updates > 0)
			{
				await new Updates(updates);
			}
		}
	}
	public class DelayUpdatesOrSecondsInt : DelayUpdatesOrTime
	{
		public ValueInput<int> Duration;

		protected override TimeSpan GetDuration(FrooxEngineContext context)
		{
			return TimeSpan.FromSeconds(Duration.Evaluate(context, 0));
		}
	}
	public class DelayUpdatesOrSecondsFloat : DelayUpdatesOrTime
	{
		public ValueInput<float> Duration;

		protected override TimeSpan GetDuration(FrooxEngineContext context)
		{
			return TimeSpan.FromSeconds(Duration.Evaluate(context, 0f));
		}
	}
	public class DelayUpdatesOrSecondsDouble : DelayUpdatesOrTime
	{
		public ValueInput<double> Duration;

		protected override TimeSpan GetDuration(FrooxEngineContext context)
		{
			return TimeSpan.FromSeconds(Duration.Evaluate(context, 0.0));
		}
	}
	public class DelayUpdatesOrTimeSpan : DelayUpdatesOrTime
	{
		public ValueInput<TimeSpan> Duration;

		protected override TimeSpan GetDuration(FrooxEngineContext context)
		{
			return Duration.Evaluate(context);
		}
	}
	[NodeName("Updates or Time Delay with Data", false)]
	[NodeOverload("Engine.UpdatesOrTimeDelayWithData")]
	public abstract class DelayUpdatesOrTimeWithValue<T> : DelayUpdatesOrTime where T : unmanaged
	{
		public ValueInput<T> Value;

		public readonly ValueOutput<T> DelayedValue;

		protected override void BeforeDelay(FrooxEngineContext context)
		{
			DelayedValue.Write(Value.Evaluate(context), context);
		}

		protected DelayUpdatesOrTimeWithValue()
		{
			((DelayUpdatesOrTimeWithValue<>)(object)this).DelayedValue = new ValueOutput<T>(this);
		}
	}
	public class DelayUpdatesOrTimeWithValueTimeSpan<T> : DelayUpdatesOrTimeWithValue<T> where T : unmanaged
	{
		public ValueInput<TimeSpan> Duration;

		protected override TimeSpan GetDuration(FrooxEngineContext context)
		{
			return Duration.Evaluate(context);
		}
	}
	public class DelayUpdatesOrTimeWithValueSecondsInt<T> : DelayUpdatesOrTimeWithValue<T> where T : unmanaged
	{
		public ValueInput<int> Duration;

		protected override TimeSpan GetDuration(FrooxEngineContext context)
		{
			return TimeSpan.FromSeconds(Duration.Evaluate(context, 0));
		}
	}
	public class DelayUpdatesOrTimeWithValueSecondsFloat<T> : DelayUpdatesOrTimeWithValue<T> where T : unmanaged
	{
		public ValueInput<float> Duration;

		protected override TimeSpan GetDuration(FrooxEngineContext context)
		{
			return TimeSpan.FromSeconds(Duration.Evaluate(context, 0f));
		}
	}
	public class DelayUpdatesOrTimeWithValueSecondsDouble<T> : DelayUpdatesOrTimeWithValue<T> where T : unmanaged
	{
		public ValueInput<double> Duration;

		protected override TimeSpan GetDuration(FrooxEngineContext context)
		{
			return TimeSpan.FromSeconds(Duration.Evaluate(context, 0.0));
		}
	}
	[NodeName("Updates or Time Delay with Data", false)]
	[NodeOverload("Engine.UpdatesOrTimeDelayWithData")]
	public abstract class DelayUpdatesOrTimeWithObject<T> : DelayUpdatesOrTime
	{
		public ObjectInput<T> Value;

		public readonly ObjectOutput<T> DelayedValue;

		protected override void BeforeDelay(FrooxEngineContext context)
		{
			DelayedValue.Write(Value.Evaluate(context), context);
		}

		protected DelayUpdatesOrTimeWithObject()
		{
			((DelayUpdatesOrTimeWithObject<>)(object)this).DelayedValue = new ObjectOutput<T>(this);
		}
	}
	public class DelayUpdatesOrTimeWithObjectTimeSpan<T> : DelayUpdatesOrTimeWithObject<T>
	{
		public ValueInput<TimeSpan> Duration;

		protected override TimeSpan GetDuration(FrooxEngineContext context)
		{
			return Duration.Evaluate(context);
		}
	}
	public class DelayUpdatesOrTimeWithObjectSecondsInt<T> : DelayUpdatesOrTimeWithObject<T>
	{
		public ValueInput<int> Duration;

		protected override TimeSpan GetDuration(FrooxEngineContext context)
		{
			return TimeSpan.FromSeconds(Duration.Evaluate(context, 0));
		}
	}
	public class DelayUpdatesOrTimeWithObjectSecondsFloat<T> : DelayUpdatesOrTimeWithObject<T>
	{
		public ValueInput<float> Duration;

		protected override TimeSpan GetDuration(FrooxEngineContext context)
		{
			return TimeSpan.FromSeconds(Duration.Evaluate(context, 0f));
		}
	}
	public class DelayUpdatesOrTimeWithObjectSecondsDouble<T> : DelayUpdatesOrTimeWithObject<T>
	{
		public ValueInput<double> Duration;

		protected override TimeSpan GetDuration(FrooxEngineContext context)
		{
			return TimeSpan.FromSeconds(Duration.Evaluate(context, 0.0));
		}
	}
	[NodeCategory("Flow/Async")]
	public class StartAsyncTask : ActionNode<FrooxEngineContext>
	{
		public AsyncResumption TaskStart;

		public Continuation OnStarted;

		public Continuation OnFailed;

		protected override IOperation Run(FrooxEngineContext context)
		{
			if (TaskStart.Target == null)
			{
				return OnFailed.Target;
			}
			context.GetEventDispatcher(out ExecutionEventDispatcher<FrooxEngineContext> _);
			FrooxEngineContext capturedContext = context.Controller.CaptureContextFrom(context);
			capturedContext.InheritExecutionDepthFrom(context);
			context.Group.StartAsyncTask(async delegate(ExecutionRuntime<FrooxEngineContext> runtime)
			{
				await runtime.ExecuteAsyncResumption(TaskStart, capturedContext);
				capturedContext.Controller.ReturnContext(ref capturedContext);
			});
			return OnStarted.Target;
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Assets
{
	[NodeCategory("Assets")]
	public class SampleValueAnimationTrack<T> : ValueFunctionNode<FrooxEngineContext, T> where T : unmanaged
	{
		public ObjectArgument<global::FrooxEngine.Animation> Animation;

		public ValueArgument<int> TrackIndex;

		public ValueArgument<float> Time;

		protected override T Compute(FrooxEngineContext context)
		{
			global::FrooxEngine.Animation animation = 0.ReadObject<global::FrooxEngine.Animation>(context);
			if (animation?.Data == null)
			{
				return default(T);
			}
			int num = 1.ReadValue<int>(context);
			if (num < 0 || num >= animation.Data.TrackCount)
			{
				return default(T);
			}
			if (animation.Data[num] is IAnimationTrack<T> animationTrack)
			{
				return animationTrack.Sample(2.ReadValue<float>(context));
			}
			return default(T);
		}
	}
	[NodeCategory("Assets")]
	public class SampleObjectAnimationTrack<T> : ObjectFunctionNode<FrooxEngineContext, T>
	{
		public ObjectArgument<global::FrooxEngine.Animation> Animation;

		public ValueArgument<int> TrackIndex;

		public ValueArgument<float> Time;

		protected override T Compute(FrooxEngineContext context)
		{
			global::FrooxEngine.Animation animation = 0.ReadObject<global::FrooxEngine.Animation>(context);
			if (animation?.Data == null)
			{
				return default(T);
			}
			int num = 1.ReadValue<int>(context);
			if (num < 0 || num >= animation.Data.TrackCount)
			{
				return default(T);
			}
			if (animation.Data[num] is IAnimationTrack<T> animationTrack)
			{
				return animationTrack.Sample(2.ReadValue<float>(context));
			}
			return default(T);
		}
	}
	[NodeCategory("Assets")]
	public class FindAnimationTrackIndex : ValueFunctionNode<FrooxEngineContext, int>
	{
		public ObjectArgument<global::FrooxEngine.Animation> Animation;

		public ObjectArgument<string> Node;

		public ObjectArgument<string> Property;

		protected override int Compute(FrooxEngineContext context)
		{
			global::FrooxEngine.Animation animation = 0.ReadObject<global::FrooxEngine.Animation>(context);
			if (animation?.Data == null)
			{
				return -1;
			}
			return animation.Data.FindTrackIndex(1.ReadObject<string>(context), 2.ReadObject<string>(context));
		}
	}
	[NodeCategory("Assets")]
	[GenericTypes(GenericTypesAttribute.Group.Assets)]
	[NodeOverload("Engine.AssetLoadProgress")]
	public class AssetLoadProgress<A> : VoidNode<FrooxEngineContext> where A : class, IAsset
	{
		public ObjectArgument<UsersAssetLoadProgress<A>> Tracker;

		public ObjectArgument<global::FrooxEngine.User> User;

		public readonly ObjectOutput<float?> DownloadProgress;

		public readonly ValueOutput<AssetLoadState> LoadState;

		public override bool CanBeEvaluated => true;

		protected override void ComputeOutputs(FrooxEngineContext context)
		{
			UsersAssetLoadProgress<A> usersAssetLoadProgress = 0.ReadObject<UsersAssetLoadProgress<A>>(context);
			global::FrooxEngine.User user = ((User.Source != null) ? 1.ReadObject<global::FrooxEngine.User>(context) : context.LocalUser);
			UsersAssetLoadProgress<A>.LoadProgress loadProgress = null;
			if (user != null && usersAssetLoadProgress != null)
			{
				loadProgress = usersAssetLoadProgress.GetProgressForUser(user, addIfNotExists: false);
			}
			DownloadProgress.Write(loadProgress?.DownloadProgress.Value, context);
			LoadState.Write(loadProgress?.LoadState.Value ?? AssetLoadState.Unloaded, context);
		}

		public AssetLoadProgress()
		{
			((AssetLoadProgress<>)(object)this).DownloadProgress = new ObjectOutput<float?>(this);
			((AssetLoadProgress<>)(object)this).LoadState = new ValueOutput<AssetLoadState>(this);
		}
	}
	[NodeCategory("Assets")]
	public class GetAsset<A> : ObjectFunctionNode<FrooxEngineContext, A> where A : class, IAsset
	{
		public ObjectArgument<IAssetProvider<A>> Provider;

		protected override A Compute(FrooxEngineContext context)
		{
			IAssetProvider<A> assetProvider = 0.ReadObject<IAssetProvider<A>>(context);
			if (assetProvider == null)
			{
				return null;
			}
			return assetProvider.Asset;
		}
	}
	[NodeCategory("Assets")]
	public abstract class AttachAsset<A> : ActionBreakableFlowNode<FrooxEngineContext> where A : class, IAssetProvider
	{
		public ObjectInput<Uri> URL;

		public ObjectInput<Slot> Target;

		[ProtoFlux.Core.DefaultValue(true)]
		public ValueInput<bool> GetExisting;

		public readonly ObjectOutput<A> AttachedProvider;

		protected override bool Do(FrooxEngineContext context)
		{
			Slot slot = Target.Evaluate(context);
			if (slot == null || slot.IsRemoved)
			{
				return false;
			}
			Uri uri = URL.Evaluate(context);
			if (uri == null)
			{
				return false;
			}
			A value = Attach(slot, uri, GetExisting.Evaluate(context, defaultValue: true));
			AttachedProvider.Write(value, context);
			return true;
		}

		protected abstract A Attach(Slot root, Uri url, bool getExisting);

		protected AttachAsset()
		{
			((AttachAsset<>)(object)this).AttachedProvider = new ObjectOutput<A>(this);
		}
	}
	public class AttachTexture2D : AttachAsset<StaticTexture2D>
	{
		protected override StaticTexture2D Attach(Slot root, Uri url, bool getExisting)
		{
			return root.AttachTexture(url, getExisting);
		}
	}
	public class AttachSprite : AttachAsset<SpriteProvider>
	{
		protected override SpriteProvider Attach(Slot root, Uri url, bool getExisting)
		{
			return root.AttachSprite(url, uncompressed: false, evenNull: false, getExisting);
		}
	}
	public class AttachMesh : AttachAsset<StaticMesh>
	{
		protected override StaticMesh Attach(Slot root, Uri url, bool getExisting)
		{
			return root.AttachStaticMesh(url, getExisting);
		}
	}
	public class AttachAudioClip : AttachAsset<StaticAudioClip>
	{
		protected override StaticAudioClip Attach(Slot root, Uri url, bool getExisting)
		{
			return root.AttachAudioClip(url, getExisting);
		}
	}
	[NodeCategory("Strings/Localization")]
	public class FormatLocaleString : ObjectFunctionNode<FrooxEngineContext, string>
	{
		public ObjectArgument<global::FrooxEngine.LocaleResource> Locale;

		public ObjectArgument<string> Key;

		protected override string Compute(FrooxEngineContext context)
		{
			global::FrooxEngine.LocaleResource localeResource = 0.ReadObject<global::FrooxEngine.LocaleResource>(context);
			string text = 1.ReadObject<string>(context);
			if (localeResource?.Data == null || string.IsNullOrWhiteSpace(text))
			{
				return null;
			}
			return localeResource.Format(text, null);
		}
	}
	[NodeCategory("Strings/Localization")]
	public class LocaleMessageCount : ValueFunctionNode<FrooxEngineContext, int>
	{
		public ObjectArgument<global::FrooxEngine.LocaleResource> Locale;

		protected override int Compute(FrooxEngineContext context)
		{
			return (0.ReadObject<global::FrooxEngine.LocaleResource>(context)?.Data?.MessageCount).GetValueOrDefault();
		}
	}
	[NodeCategory("Assets")]
	public class BakeMeshes : AsyncActionNode<FrooxEngineContext>
	{
		public ObjectInput<Slot> Root;

		public ValueInput<bool> SkinnedMeshMode;

		public ValueInput<bool> IncludeInactive;

		[ProtoFlux.Core.DefaultValue(true)]
		public ValueInput<bool> DestroyOriginal;

		public ObjectInput<Slot> AssetsSlot;

		[ProtoFlux.Core.DefaultValue(ComponentHandling.IfExists)]
		public ValueInput<ComponentHandling> GrabbableHandling;

		[ProtoFlux.Core.DefaultValue(ComponentHandling.IfExists)]
		public ValueInput<ComponentHandling> ColliderHandling;

		public ValueInput<bool> Undoable;

		public readonly ObjectOutput<Slot> BakedRoot;

		public AsyncCall OnBakeStarted;

		public Continuation OnBaked;

		protected override async Task<IOperation> RunAsync(FrooxEngineContext context)
		{
			Slot slot = Root.Evaluate(context);
			if (slot == null || slot.IsRemoved || slot.IsRootSlot)
			{
				return null;
			}
			bool skinnedMeshMode = SkinnedMeshMode.Evaluate(context, defaultValue: false);
			bool includeInactive = IncludeInactive.Evaluate(context, defaultValue: false);
			bool destroyOriginal = DestroyOriginal.Evaluate(context, defaultValue: true);
			Slot element = AssetsSlot.Evaluate(context);
			element = element.FilterWorldElement();
			ComponentHandling grabbable = GrabbableHandling.Evaluate(context, ComponentHandling.IfExists);
			ComponentHandling collider = ColliderHandling.Evaluate(context, ComponentHandling.IfExists);
			bool undoable = Undoable.Evaluate(context, defaultValue: false);
			Task<Slot> bakeTask = slot.BakeMeshes(destroyOriginal, undoable, includeInactive, skinnedMeshMode, grabbable, collider, element);
			await OnBakeStarted.ExecuteAsync(context);
			Slot value = await bakeTask;
			BakedRoot.Write(value, context);
			return OnBaked.Target;
		}

		public BakeMeshes()
		{
			BakedRoot = new ObjectOutput<Slot>(this);
		}
	}
	[NodeCategory("Assets")]
	[NodeName("Sample Texture 2D UV", false)]
	public class SampleTexture2D_UV : ValueFunctionNode<FrooxEngineContext, colorX>
	{
		public ObjectArgument<Texture2D> Texture;

		public ValueArgument<float2> UV;

		public ValueArgument<WrapMode> WrapMode;

		public static WrapMode WrapModeDefault => global::Elements.Assets.WrapMode.Repeat;

		protected override colorX Compute(FrooxEngineContext context)
		{
			Bitmap2D bitmap2D = 0.ReadObject<Texture2D>(context)?.Data;
			if (bitmap2D == null || !bitmap2D.CanRead || !bitmap2D.Buffer.TryLockUse())
			{
				return colorX.Clear;
			}
			return Read(bitmap2D, context);
		}

		private colorX Read(Bitmap2D data, FrooxEngineContext context)
		{
			try
			{
				return new colorX(data.SampleUV(1.ReadValue<float2>(context), 2.ReadValue<WrapMode>(context)), data.Profile);
			}
			catch (Exception ex)
			{
				UniLog.Warning("Exception reading texture: " + ex);
				return colorX.NaN;
			}
			finally
			{
				data.Buffer.Unlock();
			}
		}
	}
	[NodeCategory("Assets")]
	[NodeName("Get Texture 2D Pixel", false)]
	public class GetTexture2D_Pixel : ValueFunctionNode<FrooxEngineContext, colorX>
	{
		public ObjectArgument<Texture2D> Texture;

		public ValueArgument<int2> Position;

		public ValueArgument<int> MipLevel;

		public static int2 PositionDefault => new int2(-1, -1);

		protected override colorX Compute(FrooxEngineContext context)
		{
			Bitmap2D bitmap2D = 0.ReadObject<Texture2D>(context)?.Data;
			if (bitmap2D == null || !bitmap2D.CanRead || !bitmap2D.Buffer.TryLockUse())
			{
				return colorX.Clear;
			}
			return Read(bitmap2D, context);
		}

		private colorX Read(Bitmap2D data, FrooxEngineContext context)
		{
			try
			{
				int num = 2.ReadValue<int>(context);
				if (num < 0 || num >= data.MipMapLevels)
				{
					return colorX.Clear;
				}
				int2 @int = data.MipMapSize(num);
				int2 int2 = 1.ReadValue<int2>(context);
				if (int2.x < 0 || int2.y < 0 || int2.x >= @int.x || int2.y >= @int.y)
				{
					return colorX.Clear;
				}
				return new colorX(data.GetPixel(int2.x, int2.y, num), data.Profile);
			}
			catch (Exception ex)
			{
				UniLog.Warning("Exception reading texture: " + ex);
				return colorX.NaN;
			}
			finally
			{
				data.Buffer.Unlock();
			}
		}
	}
	[NodeCategory("Assets")]
	[NodeName("Texture 2D Format", false)]
	public class Texture2D_Format : VoidNode<FrooxEngineContext>
	{
		public ObjectArgument<Texture2D> Texture;

		public readonly ValueOutput<int2> Size;

		public readonly ValueOutput<TextureFormat> Format;

		public readonly ValueOutput<int> MipMapCount;

		protected override void ComputeOutputs(FrooxEngineContext context)
		{
			Texture2D texture2D = 0.ReadObject<Texture2D>(context);
			Size.Write(texture2D?.Size ?? new int2(-1, -1), context);
			Format.Write(texture2D?.Format ?? TextureFormat.Unknown, context);
			MipMapCount.Write(texture2D?.MipMapCount ?? (-1), context);
		}

		public Texture2D_Format()
		{
			Size = new ValueOutput<int2>(this);
			Format = new ValueOutput<TextureFormat>(this);
			MipMapCount = new ValueOutput<int>(this);
		}
	}
	[NodeCategory("Assets")]
	[NodeName("Sample Texture 2D UVW", false)]
	public class SampleTexture3D_UVW : ValueFunctionNode<FrooxEngineContext, colorX>
	{
		public ObjectArgument<Texture3D> Texture;

		public ValueArgument<float3> UVW;

		protected override colorX Compute(FrooxEngineContext context)
		{
			Bitmap3D bitmap3D = 0.ReadObject<Texture3D>(context)?.Data;
			if (bitmap3D == null || !bitmap3D.CanRead || !bitmap3D.Buffer.TryLockUse())
			{
				return colorX.Clear;
			}
			return Read(bitmap3D, context);
		}

		private colorX Read(Bitmap3D data, FrooxEngineContext context)
		{
			try
			{
				return new colorX(data.SampleUVW(1.ReadValue<float3>(context)), data.Profile);
			}
			catch (Exception ex)
			{
				UniLog.Warning("Exception reading texture: " + ex);
				return colorX.NaN;
			}
			finally
			{
				data.Buffer.Unlock();
			}
		}
	}
	[NodeCategory("Assets")]
	[NodeName("Get Texture 3D Pixel", false)]
	public class GetTexture3D_Pixel : ValueFunctionNode<FrooxEngineContext, colorX>
	{
		public ObjectArgument<Texture3D> Texture;

		public ValueArgument<int3> Position;

		public ValueArgument<int> MipLevel;

		public static int3 PositionDefault => new int3(-1, -1, -1);

		protected override colorX Compute(FrooxEngineContext context)
		{
			Bitmap3D bitmap3D = 0.ReadObject<Texture3D>(context)?.Data;
			if (bitmap3D == null || !bitmap3D.CanRead || !bitmap3D.Buffer.TryLockUse())
			{
				return colorX.Clear;
			}
			return Read(bitmap3D, context);
		}

		private colorX Read(Bitmap3D data, FrooxEngineContext context)
		{
			try
			{
				int3 @int = 1.ReadValue<int3>(context);
				int num = 2.ReadValue<int>(context);
				if (num < 0 || num >= data.MipMapLevels)
				{
					return colorX.Clear;
				}
				int3 int2 = data.MipMapSize(num);
				if (@int.x < 0 || @int.y < 0 || @int.z < 0 || @int.x >= int2.x || @int.y >= int2.y || @int.z >= int2.z)
				{
					return colorX.Clear;
				}
				return new colorX(data.GetPixel(@int.x, @int.y, @int.z, num), data.Profile);
			}
			catch (Exception ex)
			{
				UniLog.Warning("Exception reading texture: " + ex);
				return colorX.NaN;
			}
			finally
			{
				data.Buffer.Unlock();
			}
		}
	}
	[NodeCategory("Assets")]
	[NodeName("Texture 3D Format", false)]
	public class Texture3D_Format : VoidNode<FrooxEngineContext>
	{
		public ObjectArgument<Texture3D> Texture;

		public readonly ValueOutput<int3> Size;

		public readonly ValueOutput<TextureFormat> Format;

		public readonly ValueOutput<int> MipMapCount;

		protected override void ComputeOutputs(FrooxEngineContext context)
		{
			Texture3D texture3D = 0.ReadObject<Texture3D>(context);
			Size.Write(texture3D?.Size ?? new int3(-1, -1, -1), context);
			Format.Write(texture3D?.Format ?? TextureFormat.Unknown, context);
			MipMapCount.Write(texture3D?.Data?.MipMapLevels ?? (-1), context);
		}

		public Texture3D_Format()
		{
			Size = new ValueOutput<int3>(this);
			Format = new ValueOutput<TextureFormat>(this);
			MipMapCount = new ValueOutput<int>(this);
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Animation
{
	[NodeCategory("Actions")]
	[NodeName("Tween", false)]
	public class TweenValue<T> : AsyncActionNode<FrooxEngineContext> where T : unmanaged
	{
		public ValueInput<T> To;

		public ValueInput<T> From;

		[ProtoFlux.Core.DefaultValue(1f)]
		public ValueInput<float> Duration;

		[ProtoFlux.Core.DefaultValue(CurvePreset.Smooth)]
		public ValueInput<CurvePreset> Curve;

		public ValueInput<bool> ProportionalDuration;

		public ObjectInput<IField<T>> Target;

		public AsyncCall OnStarted;

		public Continuation OnDone;

		protected override async Task<IOperation> RunAsync(FrooxEngineContext context)
		{
			IField<T> field = Target.Evaluate(context);
			if (field == null)
			{
				return null;
			}
			T val = field.Value;
			T val2 = field.Value;
			if (To.Source != null)
			{
				val2 = To.Evaluate(context);
			}
			if (From.Source != null)
			{
				val = From.Evaluate(context);
			}
			float num = Duration.Evaluate(context, 1f);
			bool num2 = ProportionalDuration.Evaluate(context, defaultValue: false);
			CurvePreset curve = Curve.Evaluate(context, CurvePreset.Smooth);
			if (num2 && Coder<T>.SupportsDistance)
			{
				float num3 = Coder<T>.Distance(val, val2);
				if (num3.IsValid())
				{
					num *= num3;
				}
			}
			TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>();
			field.TweenFromTo(val, val2, num, curve, null, delegate
			{
				completion.SetResult(result: true);
			});
			await OnStarted.ExecuteAsync(context);
			await completion.Task;
			return OnDone.Target;
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.Actions
{
	[NodeOverload("Core.Action.FireOnLocalChange")]
	public class FireOnLocalValueChange<T> : FireOnLocalValueChange<FrooxEngineContext, T> where T : unmanaged
	{
	}
	[NodeOverload("Core.Action.FireOnLocalChange")]
	public class FireOnLocalObjectChange<T> : FireOnLocalObjectChange<FrooxEngineContext, T>
	{
	}
	[NodeOverload("Core.Action.FireOnLocalTrue")]
	public class FireOnLocalTrue : FireOnLocalTrue<FrooxEngineContext>
	{
	}
	[NodeOverload("Core.Action.FireOnLocalFalse")]
	public class FireOnLocalFalse : FireOnLocalFalse<FrooxEngineContext>
	{
	}
	public interface IAsyncDynamicImpulseTarget
	{
		string Tag { get; }
	}
	public delegate Task AsyncDynamicImpulseHandler(FrooxEngineContext sourceContext);
	public delegate Task AsyncDynamicImpulseHandler<T>(T value, FrooxEngineContext sourceContext);
	[NodeCategory("Flow/Async")]
	public class AsyncDynamicImpulseReceiver : ProxyVoidNode<FrooxEngineContext, AsyncDynamicImpulseReceiver.Proxy>
	{
		public class Proxy : ProtoFluxEngineProxy, IDynamicImpulseTarget
		{
			public AsyncDynamicImpulseHandler Trigger;

			public string Tag { get; set; }

			protected override void InitializeSyncMembers()
			{
				base.InitializeSyncMembers();
			}

			public override ISyncMember GetSyncMember(int index)
			{
				return index switch
				{
					0 => persistent, 
					1 => updateOrder, 
					2 => EnabledField, 
					3 => Node, 
					4 => Path, 
					_ => throw new ArgumentOutOfRangeException(), 
				};
			}

			public static Proxy __New()
			{
				return new Proxy();
			}
		}

		public readonly GlobalRef<string> Tag;

		public AsyncCall OnTriggered;

		private void OnTagChanged(string newTag, FrooxEngineContext context)
		{
			Proxy proxy = GetProxy(context);
			if (proxy != null)
			{
				proxy.Tag = newTag;
			}
		}

		protected override void ProxyAdded(Proxy proxy, FrooxEngineContext context)
		{
			IExecutionRuntime rootRuntime;
			NodeContextPath path = context.CaptureContextPath(out rootRuntime);
			ProtoFluxNodeGroup group = context.Group;
			proxy.Tag = context.CurrentScope.ReadGlobal(Tag);
			proxy.Trigger = async delegate(FrooxEngineContext triggeringContext)
			{
				if (triggeringContext == null || !triggeringContext.IsCurrentPath(rootRuntime, path))
				{
					await group.ExecuteImmediatellyAsync(path, async delegate(FrooxEngineContext c)
					{
						await OnTriggered.ExecuteAsync(c);
					});
				}
				else
				{
					await OnTriggered.ExecuteAsync(triggeringContext);
				}
			};
		}

		protected override void ProxyRemoved(Proxy proxy, FrooxEngineContext context, bool inUseByAnotherInstance)
		{
			if (!inUseByAnotherInstance)
			{
				proxy.Tag = null;
				proxy.Trigger = null;
			}
		}

		public AsyncDynamicImpulseReceiver()
		{
			Tag = new GlobalRef<string>(this, 0);
		}
	}
	[NodeCategory("Flow/Async")]
	[NodeName("Async Dynamic Impulse Receiver With Data", false)]
	[NodeOverload("Engine.AsyncDynamicImpulseReceiver")]
	public class AsyncDynamicImpulseReceiverWithValue<T> : ProxyVoidNode<FrooxEngineContext, AsyncDynamicImpulseReceiverWithValue<T>.Proxy> where T : unmanaged
	{
		public class Proxy : ProtoFluxEngineProxy, IDynamicImpulseTarget
		{
			public AsyncDynamicImpulseHandler<T> Trigger;

			public string Tag { get; set; }

			protected override void InitializeSyncMembers()
			{
				base.InitializeSyncMembers();
			}

			public override ISyncMember GetSyncMember(int index)
			{
				return index switch
				{
					0 => persistent, 
					1 => updateOrder, 
					2 => EnabledField, 
					3 => Node, 
					4 => Path, 
					_ => throw new ArgumentOutOfRangeException(), 
				};
			}

			public static Proxy __New()
			{
				return new Proxy();
			}
		}

		public readonly GlobalRef<string> Tag;

		public AsyncCall OnTriggered;

		public readonly ValueOutput<T> Value;

		public override bool CanBeEvaluated => false;

		private void OnTagChanged(string newTag, FrooxEngineContext context)
		{
			Proxy proxy = GetProxy(context);
			if (proxy != null)
			{
				proxy.Tag = newTag;
			}
		}

		protected override void ProxyAdded(Proxy proxy, FrooxEngineContext context)
		{
			IExecutionRuntime rootRuntime;
			NodeContextPath path = context.CaptureContextPath(out rootRuntime);
			ProtoFluxNodeGroup group = context.Group;
			proxy.Tag = context.CurrentScope.ReadGlobal(Tag);
			proxy.Trigger = async delegate(T value, FrooxEngineContext trigerringContext)
			{
				if (trigerringContext == null || !trigerringContext.IsCurrentPath(rootRuntime, path))
				{
					await group.ExecuteImmediatellyAsync(path, async delegate(FrooxEngineContext c)
					{
						Value.Write(value, c);
						await OnTriggered.ExecuteAsync(c);
					});
				}
				else
				{
					Value.Write(value, trigerringContext);
					await OnTriggered.ExecuteAsync(trigerringContext);
				}
			};
		}

		protected override void ProxyRemoved(Proxy proxy, FrooxEngineContext context, bool inUseByAnotherInstance)
		{
			if (!inUseByAnotherInstance)
			{
				proxy.Tag = null;
				proxy.Trigger = null;
			}
		}

		public AsyncDynamicImpulseReceiverWithValue()
		{
			((AsyncDynamicImpulseReceiverWithValue<>)(object)this).Tag = new GlobalRef<string>(this, 0);
			((AsyncDynamicImpulseReceiverWithValue<>)(object)this).Value = new ValueOutput<T>(this);
		}
	}
	[NodeCategory("Flow/Async")]
	[NodeName("Async Dynamic Impulse Receiver With Data", false)]
	[NodeOverload("Engine.AsyncDynamicImpulseReceiver")]
	public class AsyncDynamicImpulseReceiverWithObject<T> : ProxyVoidNode<FrooxEngineContext, AsyncDynamicImpulseReceiverWithObject<T>.Proxy>
	{
		public class Proxy : ProtoFluxEngineProxy, IDynamicImpulseTarget
		{
			public AsyncDynamicImpulseHandler<T> Trigger;

			public string Tag { get; set; }

			protected override void InitializeSyncMembers()
			{
				base.InitializeSyncMembers();
			}

			public override ISyncMember GetSyncMember(int index)
			{
				return index switch
				{
					0 => persistent, 
					1 => updateOrder, 
					2 => EnabledField, 
					3 => Node, 
					4 => Path, 
					_ => throw new ArgumentOutOfRangeException(), 
				};
			}

			public static Proxy __New()
			{
				return new Proxy();
			}
		}

		public readonly GlobalRef<string> Tag;

		public AsyncCall OnTriggered;

		public readonly ObjectOutput<T> Value;

		public override bool CanBeEvaluated => false;

		private void OnTagChanged(string newTag, FrooxEngineContext context)
		{
			Proxy proxy = GetProxy(context);
			if (proxy != null)
			{
				proxy.Tag = newTag;
			}
		}

		protected override void ProxyAdded(Proxy proxy, FrooxEngineContext context)
		{
			IExecutionRuntime rootRuntime;
			NodeContextPath path = context.CaptureContextPath(out rootRuntime);
			ProtoFluxNodeGroup group = context.Group;
			proxy.Tag = context.CurrentScope.ReadGlobal(Tag);
			proxy.Trigger = async delegate(T value, FrooxEngineContext trigerringContext)
			{
				if (trigerringContext == null || !trigerringContext.IsCurrentPath(rootRuntime, path))
				{
					await group.ExecuteImmediatellyAsync(path, async delegate(FrooxEngineContext c)
					{
						Value.Write(value, c);
						await OnTriggered.ExecuteAsync(c);
					});
				}
				else
				{
					Value.Write(value, trigerringContext);
					await OnTriggered.ExecuteAsync(trigerringContext);
				}
			};
		}

		protected override void ProxyRemoved(Proxy proxy, FrooxEngineContext context, bool inUseByAnotherInstance)
		{
			if (!inUseByAnotherInstance)
			{
				proxy.Tag = null;
				proxy.Trigger = null;
			}
		}

		public AsyncDynamicImpulseReceiverWithObject()
		{
			((AsyncDynamicImpulseReceiverWithObject<>)(object)this).Tag = new GlobalRef<string>(this, 0);
			((AsyncDynamicImpulseReceiverWithObject<>)(object)this).Value = new ObjectOutput<T>(this);
		}
	}
	[NodeCategory("Flow/Async")]
	public abstract class AsyncDynamicImpulseTriggerBase : AsyncActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<string> Tag;

		public ObjectInput<Slot> TargetHierarchy;

		public ValueInput<bool> ExcludeDisabled;

		public readonly ValueOutput<int> TriggeredCount;

		protected override async Task<bool> Do(FrooxEngineContext context)
		{
			Slot slot = TargetHierarchy.Evaluate(context);
			if (slot == null || slot.IsRemoved)
			{
				TriggeredCount.Write(0, context);
				return false;
			}
			string tag = Tag.Evaluate(context);
			bool excludeDisabled = ExcludeDisabled.Evaluate(context, defaultValue: false);
			int value = await Trigger(slot, tag, excludeDisabled, context);
			TriggeredCount.Write(value, context);
			return true;
		}

		protected abstract Task<int> Trigger(Slot hierarchy, string tag, bool excludeDisabled, FrooxEngineContext context);

		protected AsyncDynamicImpulseTriggerBase()
		{
			TriggeredCount = new ValueOutput<int>(this);
		}
	}
	[NodeName("Asyncs Dynamic Impulse Trigger", false)]
	public class AsyncDynamicImpulseTrigger : AsyncDynamicImpulseTriggerBase
	{
		protected override Task<int> Trigger(Slot hierarchy, string tag, bool excludeDisabled, FrooxEngineContext context)
		{
			return DynamicImpulseHelper.Singleton.TriggerAsyncDynamicImpulse(hierarchy, tag, excludeDisabled, context);
		}
	}
	[NodeName("Async Dynamic Impulse Trigger With Data", false)]
	[NodeOverload("Engine.AsyncDynamicImpulseTrigger")]
	public class AsyncDynamicImpulseTriggerWithValue<T> : AsyncDynamicImpulseTriggerBase where T : unmanaged
	{
		public ValueInput<T> Value;

		protected override Task<int> Trigger(Slot hierarchy, string tag, bool excludeDisabled, FrooxEngineContext context)
		{
			T value = Value.Evaluate(context);
			return DynamicImpulseHelper.TriggerAsyncDynamicImpulseWithValue(hierarchy, tag, excludeDisabled, value, context);
		}
	}
	[NodeName("Async Dynamic Impulse Trigger With Data", false)]
	[NodeOverload("Engine.AsyncDynamicImpulseTrigger")]
	public class AsyncDynamicImpulseTriggerWithObject<T> : AsyncDynamicImpulseTriggerBase
	{
		public ObjectInput<T> Value;

		protected override Task<int> Trigger(Slot hierarchy, string tag, bool excludeDisabled, FrooxEngineContext context)
		{
			T value = Value.Evaluate(context);
			return DynamicImpulseHelper.TriggerAsyncDynamicImpulseWithObject(hierarchy, tag, excludeDisabled, value, context);
		}
	}
	public class DynamicImpulseHelper : IDynamicImpulseHandler
	{
		public static readonly DynamicImpulseHelper Singleton;

		static DynamicImpulseHelper()
		{
			Singleton = new DynamicImpulseHelper();
		}

		public int TriggerDynamicImpulse(Slot hierarchy, string tag, bool excludeDisabled, FrooxEngineContext sourceContext = null)
		{
			return TriggerDynamicImpulse(hierarchy, tag, excludeDisabled, delegate(DynamicImpulseReceiver.Proxy t)
			{
				t.Trigger?.Invoke(sourceContext);
			});
		}

		public int TriggerDynamicImpulseWithArgument<T>(Slot hierarchy, string tag, bool excludeDisabled, T value, FrooxEngineContext sourceContext = null)
		{
			if (typeof(T).IsValueType)
			{
				return (int)typeof(DynamicImpulseHelper).GetMethod("TriggerDynamicImpulseWithValue", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).MakeGenericMethod(typeof(T)).Invoke(null, new object[5] { hierarchy, tag, excludeDisabled, value, sourceContext });
			}
			return TriggerDynamicImpulseWithObject(hierarchy, tag, excludeDisabled, value);
		}

		public Task<int> TriggerAsyncDynamicImpulse(Slot hierarchy, string tag, bool excludeDisabled, FrooxEngineContext sourceContext = null)
		{
			return TriggerAsyncDynamicImpulse(hierarchy, tag, excludeDisabled, (AsyncDynamicImpulseReceiver.Proxy t) => t.Trigger?.Invoke(sourceContext) ?? Task.CompletedTask);
		}

		public Task<int> TriggerAsyncDynamicImpulseWithArgument<T>(Slot hierarchy, string tag, bool excludeDisabled, T value, FrooxEngineContext sourceContext = null)
		{
			if (typeof(T).IsValueType)
			{
				return (Task<int>)typeof(DynamicImpulseHelper).GetMethod("TriggerAsyncDynamicImpulseWithValue", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).MakeGenericMethod(typeof(T)).Invoke(null, new object[5] { hierarchy, tag, excludeDisabled, value, sourceContext });
			}
			return TriggerAsyncDynamicImpulseWithObject(hierarchy, tag, excludeDisabled, value);
		}

		internal static int TriggerDynamicImpulseWithValue<T>(Slot hierarchy, string tag, bool excludeDisabled, T value, FrooxEngineContext sourceContext = null) where T : unmanaged
		{
			return TriggerDynamicImpulse(hierarchy, tag, excludeDisabled, delegate(DynamicImpulseReceiverWithValue<T>.Proxy t)
			{
				t.Trigger?.Invoke(value, sourceContext);
			});
		}

		internal static int TriggerDynamicImpulseWithObject<T>(Slot hierarchy, string tag, bool excludeDisabled, T value, FrooxEngineContext sourceContext = null)
		{
			return TriggerDynamicImpulse(hierarchy, tag, excludeDisabled, delegate(DynamicImpulseReceiverWithObject<T>.Proxy t)
			{
				t.Trigger?.Invoke(value, sourceContext);
			});
		}

		internal static int TriggerDynamicImpulse<P>(Slot hierarchy, string tag, bool excludeDisabled, Action<P> trigger) where P : class, IDynamicImpulseTarget
		{
			if (hierarchy == null)
			{
				return 0;
			}
			hierarchy.World.ProtoFlux.Rebuild();
			List<P> list = Pool.BorrowList<P>();
			hierarchy.GetComponentsInChildren(list, (P r) => r.Tag == tag, excludeDisabled, includeLocal: false, (Slot s) => s.Name != "<NODE_UI>");
			int count = list.Count;
			foreach (P item in list)
			{
				trigger(item);
			}
			Pool.Return(ref list);
			return count;
		}

		internal static Task<int> TriggerAsyncDynamicImpulseWithValue<T>(Slot hierarchy, string tag, bool excludeDisabled, T value, FrooxEngineContext sourceContext = null) where T : unmanaged
		{
			return TriggerAsyncDynamicImpulse(hierarchy, tag, excludeDisabled, (AsyncDynamicImpulseReceiverWithValue<T>.Proxy t) => t.Trigger?.Invoke(value, sourceContext));
		}

		internal static Task<int> TriggerAsyncDynamicImpulseWithObject<T>(Slot hierarchy, string tag, bool excludeDisabled, T value, FrooxEngineContext sourceContext = null)
		{
			return TriggerAsyncDynamicImpulse(hierarchy, tag, excludeDisabled, (AsyncDynamicImpulseReceiverWithObject<T>.Proxy t) => t.Trigger?.Invoke(value, sourceContext));
		}

		internal static async Task<int> TriggerAsyncDynamicImpulse<P>(Slot hierarchy, string tag, bool excludeDisabled, Func<P, Task> trigger) where P : class, IDynamicImpulseTarget
		{
			if (hierarchy == null)
			{
				return 0;
			}
			hierarchy.World.ProtoFlux.Rebuild();
			List<P> targets = Pool.BorrowList<P>();
			hierarchy.GetComponentsInChildren(targets, (P r) => r.Tag == tag, excludeDisabled, includeLocal: false, (Slot s) => s.Name != "<NODE_UI>");
			int count = targets.Count;
			foreach (P item in targets)
			{
				await trigger(item);
			}
			Pool.Return(ref targets);
			return count;
		}
	}
	public interface IDynamicImpulseTarget
	{
		string Tag { get; }
	}
	public delegate void DynamicImpulseHandler(FrooxEngineContext sourceContext);
	public delegate void DynamicImpulseHandler<T>(T value, FrooxEngineContext sourceContext);
	[NodeCategory("Flow")]
	public class DynamicImpulseReceiver : ProxyVoidNode<FrooxEngineContext, DynamicImpulseReceiver.Proxy>
	{
		public class Proxy : ProtoFluxEngineProxy, IDynamicImpulseTarget
		{
			public DynamicImpulseHandler Trigger;

			public string Tag { get; set; }

			protected override void InitializeSyncMembers()
			{
				base.InitializeSyncMembers();
			}

			public override ISyncMember GetSyncMember(int index)
			{
				return index switch
				{
					0 => persistent, 
					1 => updateOrder, 
					2 => EnabledField, 
					3 => Node, 
					4 => Path, 
					_ => throw new ArgumentOutOfRangeException(), 
				};
			}

			public static Proxy __New()
			{
				return new Proxy();
			}
		}

		public readonly GlobalRef<string> Tag;

		public Call OnTriggered;

		private void OnTagChanged(string newTag, FrooxEngineContext context)
		{
			Proxy proxy = GetProxy(context);
			if (proxy != null)
			{
				proxy.Tag = newTag;
			}
		}

		protected override void ProxyAdded(Proxy proxy, FrooxEngineContext context)
		{
			IExecutionRuntime rootRuntime;
			NodeContextPath path = context.CaptureContextPath(out rootRuntime);
			ProtoFluxNodeGroup group = context.Group;
			proxy.Tag = context.CurrentScope.ReadGlobal(Tag);
			proxy.Trigger = delegate(FrooxEngineContext triggerContext)
			{
				if (triggerContext != null && triggerContext.IsCurrentPath(rootRuntime, path))
				{
					OnTriggered.Execute(triggerContext);
				}
				else
				{
					group.ExecuteImmediatelly(path, delegate(FrooxEngineContext c)
					{
						OnTriggered.Execute(c);
					});
				}
			};
		}

		protected override void ProxyRemoved(Proxy proxy, FrooxEngineContext context, bool inUseByAnotherInstance)
		{
			if (!inUseByAnotherInstance)
			{
				proxy.Tag = null;
				proxy.Trigger = null;
			}
		}

		public DynamicImpulseReceiver()
		{
			Tag = new GlobalRef<string>(this, 0);
		}
	}
	[NodeCategory("Flow")]
	[NodeName("Dynamic Impulse Receiver With Data", false)]
	[NodeOverload("Engine.DynamicImpulseReceiverWithData")]
	public class DynamicImpulseReceiverWithValue<T> : ProxyVoidNode<FrooxEngineContext, DynamicImpulseReceiverWithValue<T>.Proxy> where T : unmanaged
	{
		public class Proxy : ProtoFluxEngineProxy, IDynamicImpulseTarget
		{
			public DynamicImpulseHandler<T> Trigger;

			public string Tag { get; set; }

			protected override void InitializeSyncMembers()
			{
				base.InitializeSyncMembers();
			}

			public override ISyncMember GetSyncMember(int index)
			{
				return index switch
				{
					0 => persistent, 
					1 => updateOrder, 
					2 => EnabledField, 
					3 => Node, 
					4 => Path, 
					_ => throw new ArgumentOutOfRangeException(), 
				};
			}

			public static Proxy __New()
			{
				return new Proxy();
			}
		}

		public readonly GlobalRef<string> Tag;

		public Call OnTriggered;

		public readonly ValueOutput<T> Value;

		public override bool CanBeEvaluated => false;

		private void OnTagChanged(string newTag, FrooxEngineContext context)
		{
			Proxy proxy = GetProxy(context);
			if (proxy != null)
			{
				proxy.Tag = newTag;
			}
		}

		protected override void ProxyAdded(Proxy proxy, FrooxEngineContext context)
		{
			IExecutionRuntime rootRuntime;
			NodeContextPath path = context.CaptureContextPath(out rootRuntime);
			ProtoFluxNodeGroup group = context.Group;
			proxy.Tag = context.CurrentScope.ReadGlobal(Tag);
			proxy.Trigger = delegate(T value, FrooxEngineContext triggerContext)
			{
				if (triggerContext != null && triggerContext.IsCurrentPath(rootRuntime, path))
				{
					Value.Write(value, triggerContext);
					OnTriggered.Execute(triggerContext);
				}
				else
				{
					group.ExecuteImmediatelly(path, delegate(FrooxEngineContext c)
					{
						Value.Write(value, c);
						OnTriggered.Execute(c);
					});
				}
			};
		}

		protected override void ProxyRemoved(Proxy proxy, FrooxEngineContext context, bool inUseByAnotherInstance)
		{
			if (!inUseByAnotherInstance)
			{
				proxy.Tag = null;
				proxy.Trigger = null;
			}
		}

		public DynamicImpulseReceiverWithValue()
		{
			((DynamicImpulseReceiverWithValue<>)(object)this).Tag = new GlobalRef<string>(this, 0);
			((DynamicImpulseReceiverWithValue<>)(object)this).Value = new ValueOutput<T>(this);
		}
	}
	[NodeCategory("Flow")]
	[NodeName("Dynamic Impulse Receiver With Data", false)]
	[NodeOverload("Engine.DynamicImpulseReceiverWithData")]
	public class DynamicImpulseReceiverWithObject<T> : ProxyVoidNode<FrooxEngineContext, DynamicImpulseReceiverWithObject<T>.Proxy>
	{
		public class Proxy : ProtoFluxEngineProxy, IDynamicImpulseTarget
		{
			public DynamicImpulseHandler<T> Trigger;

			public string Tag { get; set; }

			protected override void InitializeSyncMembers()
			{
				base.InitializeSyncMembers();
			}

			public override ISyncMember GetSyncMember(int index)
			{
				return index switch
				{
					0 => persistent, 
					1 => updateOrder, 
					2 => EnabledField, 
					3 => Node, 
					4 => Path, 
					_ => throw new ArgumentOutOfRangeException(), 
				};
			}

			public static Proxy __New()
			{
				return new Proxy();
			}
		}

		public readonly GlobalRef<string> Tag;

		public Call OnTriggered;

		public readonly ObjectOutput<T> Value;

		public override bool CanBeEvaluated => false;

		private void OnTagChanged(string newTag, FrooxEngineContext context)
		{
			Proxy proxy = GetProxy(context);
			if (proxy != null)
			{
				proxy.Tag = newTag;
			}
		}

		protected override void ProxyAdded(Proxy proxy, FrooxEngineContext context)
		{
			IExecutionRuntime rootRuntime;
			NodeContextPath path = context.CaptureContextPath(out rootRuntime);
			ProtoFluxNodeGroup group = context.Group;
			proxy.Tag = context.CurrentScope.ReadGlobal(Tag);
			proxy.Trigger = delegate(T value, FrooxEngineContext triggerContext)
			{
				if (triggerContext != null && triggerContext.IsCurrentPath(rootRuntime, path))
				{
					Value.Write(value, triggerContext);
					OnTriggered.Execute(triggerContext);
				}
				else
				{
					group.ExecuteImmediatelly(path, delegate(FrooxEngineContext c)
					{
						Value.Write(value, c);
						OnTriggered.Execute(c);
					});
				}
			};
		}

		protected override void ProxyRemoved(Proxy proxy, FrooxEngineContext context, bool inUseByAnotherInstance)
		{
			if (!inUseByAnotherInstance)
			{
				proxy.Tag = null;
				proxy.Trigger = null;
			}
		}

		public DynamicImpulseReceiverWithObject()
		{
			((DynamicImpulseReceiverWithObject<>)(object)this).Tag = new GlobalRef<string>(this, 0);
			((DynamicImpulseReceiverWithObject<>)(object)this).Value = new ObjectOutput<T>(this);
		}
	}
	public abstract class DynamicImpulseTriggerBase : ActionBreakableFlowNode<FrooxEngineContext>
	{
		public ObjectInput<string> Tag;

		public ObjectInput<Slot> TargetHierarchy;

		public ValueInput<bool> ExcludeDisabled;

		public readonly ValueOutput<int> TriggeredCount;

		protected override bool Do(FrooxEngineContext context)
		{
			Slot slot = TargetHierarchy.Evaluate(context);
			if (slot == null || slot.IsRemoved)
			{
				return false;
			}
			string tag = Tag.Evaluate(context);
			bool excludeDisabled = ExcludeDisabled.Evaluate(context, defaultValue: false);
			int value = Trigger(slot, tag, excludeDisabled, context);
			TriggeredCount.Write(value, context);
			return true;
		}

		protected abstract int Trigger(Slot hierarchy, string tag, bool excludeDisabled, FrooxEngineContext context);

		protected DynamicImpulseTriggerBase()
		{
			TriggeredCount = new ValueOutput<int>(this);
		}
	}
	[NodeCategory("Flow")]
	public class DynamicImpulseTrigger : DynamicImpulseTriggerBase
	{
		protected override int Trigger(Slot hierarchy, string tag, bool excludeDisabled, FrooxEngineContext context)
		{
			return DynamicImpulseHelper.Singleton.TriggerDynamicImpulse(hierarchy, tag, excludeDisabled, context);
		}
	}
	[NodeCategory("Flow")]
	[NodeName("Dynamic Impulse Trigger With Data", false)]
	[NodeOverload("Engine.DynamicImpulseTriggerWithData")]
	public class DynamicImpulseTriggerWithValue<T> : DynamicImpulseTriggerBase where T : unmanaged
	{
		public ValueInput<T> Value;

		protected override int Trigger(Slot hierarchy, string tag, bool excludeDisabled, FrooxEngineContext context)
		{
			T value = Value.Evaluate(context);
			return DynamicImpulseHelper.TriggerDynamicImpulseWithValue(hierarchy, tag, excludeDisabled, value, context);
		}
	}
	[NodeCategory("Flow")]
	[NodeName("Dynamic Impulse Trigger With Data", false)]
	[NodeOverload("Engine.DynamicImpulseTriggerWithData")]
	public class DynamicImpulseTriggerWithObject<T> : DynamicImpulseTriggerBase
	{
		public ObjectInput<T> Value;

		protected override int Trigger(Slot hierarchy, string tag, bool excludeDisabled, FrooxEngineContext context)
		{
			T value = Value.Evaluate(context);
			return DynamicImpulseHelper.TriggerDynamicImpulseWithObject(hierarchy, tag, excludeDisabled, value, context);
		}
	}
	public interface ILastValueProxy<T> : global::FrooxEngine.IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable
	{
		T Last { get; set; }
	}
	public class ValueProxy<T> : ProtoFluxEngineProxy, ILastValueProxy<T>, global::FrooxEngine.IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable
	{
		public readonly Sync<T> Last;

		T ILastValueProxy<T>.Last
		{
			get
			{
				return Last.Value;
			}
			set
			{
				Last.Value = value;
			}
		}

		protected override void InitializeSyncMembers()
		{
			base.InitializeSyncMembers();
			Last = new Sync<T>();
		}

		public override ISyncMember GetSyncMember(int index)
		{
			return index switch
			{
				0 => persistent, 
				1 => updateOrder, 
				2 => EnabledField, 
				3 => Node, 
				4 => Path, 
				5 => Last, 
				_ => throw new ArgumentOutOfRangeException(), 
			};
		}

		public static ValueProxy<T> __New()
		{
			return new ValueProxy<T>();
		}
	}
	public class RefProxy<T> : ProtoFluxEngineProxy, ILastValueProxy<T>, global::FrooxEngine.IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable where T : class, IWorldElement
	{
		public readonly SyncRef<T> Last;

		T ILastValueProxy<T>.Last
		{
			get
			{
				return Last.Target;
			}
			set
			{
				Last.Target = value;
			}
		}

		protected override void InitializeSyncMembers()
		{
			base.InitializeSyncMembers();
			Last = new SyncRef<T>();
		}

		public override ISyncMember GetSyncMember(int index)
		{
			return index switch
			{
				0 => persistent, 
				1 => updateOrder, 
				2 => EnabledField, 
				3 => Node, 
				4 => Path, 
				5 => Last, 
				_ => throw new ArgumentOutOfRangeException(), 
			};
		}

		public static RefProxy<T> __New()
		{
			return new RefProxy<T>();
		}
	}
	public class TypeProxy : ProtoFluxEngineProxy, ILastValueProxy<Type>, global::FrooxEngine.IComponent, IComponentBase, IDestroyable, IWorker, IWorldElement, IUpdatable, IChangeable, IAudioUpdatable, IInitializable, ILinkable
	{
		public readonly SyncType Last;

		Type ILastValueProxy<Type>.Last
		{
			get
			{
				return Last.Value;
			}
			set
			{
				Last.Value = value;
			}
		}

		protected override void InitializeSyncMembers()
		{
			base.InitializeSyncMembers();
			Last = new SyncType();
		}

		public override ISyncMember GetSyncMember(int index)
		{
			return index switch
			{
				0 => persistent, 
				1 => updateOrder, 
				2 => EnabledField, 
				3 => Node, 
				4 => Path, 
				5 => Last, 
				_ => throw new ArgumentOutOfRangeException(), 
			};
		}

		public static TypeProxy __New()
		{
			return new TypeProxy();
		}
	}
	[NodeCategory("Flow")]
	[NodeName("Fire On Change", false)]
	public abstract class FireOnChange<T, P> : ProxyVoidNode<FrooxEngineContext, P>, IMappableNode, INode, IExecutionChangeListener<FrooxEngineContext> where P : ProtoFluxEngineProxy, ILastValueProxy<T>, new()
	{
		public ObjectInput<global::FrooxEngine.User> OnlyForUser;

		public Call OnChanged;

		private ObjectStore<Action<IChangeable>> _enabledChangedHandler;

		private ObjectStore<SlotEvent> _activeChangedHandler;

		public override bool CanBeEvaluated => false;

		protected bool InputListensToChanges { get; private set; }

		private bool ShouldListen(P proxy)
		{
			if (proxy.Enabled)
			{
				return proxy.Slot.IsActive;
			}
			return false;
		}

		protected override void ProxyAdded(P proxy, FrooxEngineContext context)
		{
			NodeContextPath path = context.CaptureContextPath();
			context.GetEventDispatcher(out var dispatcher);
			Action<IChangeable> value = delegate
			{
				dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
				{
					UpdateListenerState(c);
				});
			};
			SlotEvent value2 = delegate
			{
				dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
				{
					UpdateListenerState(c);
				});
			};
			proxy.EnabledField.Changed += value;
			proxy.Slot.ActiveChanged += value2;
			InputListensToChanges = ShouldListen(proxy);
			dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
			{
				if (DetectChanges(proxy, c))
				{
					proxy.Last = GetCurrent(c);
				}
			});
		}

		protected override void ProxyRemoved(P proxy, FrooxEngineContext context, bool inUseByAnotherInstance)
		{
			if (!inUseByAnotherInstance)
			{
				proxy.EnabledField.Changed -= _enabledChangedHandler.Read(context);
				proxy.Slot.ActiveChanged -= _activeChangedHandler.Read(context);
				_enabledChangedHandler.Clear(context);
				_activeChangedHandler.Clear(context);
			}
		}

		protected void UpdateListenerState(FrooxEngineContext context)
		{
			P proxy = GetProxy(context);
			if (proxy != null)
			{
				bool flag = ShouldListen(proxy);
				if (flag != InputListensToChanges)
				{
					InputListensToChanges = flag;
					context.Group.MarkChangeTrackingDirty();
				}
			}
		}

		public void Changed(FrooxEngineContext context)
		{
			P proxy = GetProxy(context);
			if (proxy == null || !DetectChanges(proxy, context))
			{
				return;
			}
			T current = GetCurrent(context);
			if (!EqualityComparer<T>.Default.Equals(current, proxy.Last))
			{
				proxy.Last = current;
				if (ShouldFire(current))
				{
					OnChanged.Execute(context);
				}
			}
		}

		private bool DetectChanges(P proxy, FrooxEngineContext context)
		{
			if (!proxy.Enabled)
			{
				return false;
			}
			global::FrooxEngine.User user = OnlyForUser.Evaluate(context);
			if (user != null)
			{
				if (!user.IsLocalUser)
				{
					return false;
				}
			}
			else if (proxy.Slot.ActiveUserRoot != null && !proxy.IsUnderLocalUser)
			{
				return false;
			}
			return true;
		}

		protected abstract T GetCurrent(FrooxEngineContext context);

		protected virtual bool ShouldFire(T value)
		{
			return true;
		}
	}
	[NodeOverload("Engine.FireOnChange")]
	public class FireOnValueChange<T> : FireOnChange<T, ValueProxy<T>> where T : unmanaged
	{
		public ValueInput<T> Value;

		public bool ValueListensToChanges => base.InputListensToChanges;

		protected override T GetCurrent(FrooxEngineContext context)
		{
			return Value.Evaluate(context);
		}
	}
	[NodeOverload("Engine.FireOnChange")]
	public class FireOnObjectValueChange<T> : FireOnChange<T, ValueProxy<T>>
	{
		public ObjectInput<T> Value;

		public static bool IsValidGenericType => Coder<T>.IsSupported;

		public bool ValueListensToChanges => base.InputListensToChanges;

		protected override T GetCurrent(FrooxEngineContext context)
		{
			return Value.Evaluate(context);
		}
	}
	[NodeOverload("Engine.FireOnChange")]
	public class FireOnRefChange<T> : FireOnChange<T, RefProxy<T>> where T : class, IWorldElement
	{
		public ObjectInput<T> Value;

		public bool ValueListensToChanges => base.InputListensToChanges;

		protected override T GetCurrent(FrooxEngineContext context)
		{
			return Value.Evaluate(context).FilterGlobalWorldElement();
		}
	}
	[NodeOverload("Engine.FireOnChange")]
	public class FireOnTypeChange : FireOnChange<Type, TypeProxy>
	{
		public ObjectInput<Type> Value;

		public bool ValueListensToChanges => base.InputListensToChanges;

		protected override Type GetCurrent(FrooxEngineContext context)
		{
			return Value.Evaluate(context);
		}
	}
	[NodeName("Fire On True", false)]
	public class FireOnTrue : FireOnChange<bool, ValueProxy<bool>>
	{
		public ValueInput<bool> Condition;

		public bool ConditionListensToChanges => base.InputListensToChanges;

		protected override bool GetCurrent(FrooxEngineContext context)
		{
			return Condition.Evaluate(context, defaultValue: false);
		}

		protected override bool ShouldFire(bool value)
		{
			return value;
		}
	}
	[NodeName("Fire On False", false)]
	public class FireOnFalse : FireOnChange<bool, ValueProxy<bool>>
	{
		public ValueInput<bool> Condition;

		public bool ConditionListensToChanges => base.InputListensToChanges;

		protected override bool GetCurrent(FrooxEngineContext context)
		{
			return Condition.Evaluate(context, defaultValue: false);
		}

		protected override bool ShouldFire(bool value)
		{
			return !value;
		}
	}
	[NodeCategory("Flow")]
	public abstract class FireOnLocalBool<C> : VoidNode<FrooxEngineContext>, IExecutionChangeListener<C>, IScopeEventListener<C>, IMappableNode, INode where C : FrooxEngineContext
	{
		public ValueInput<bool> Condition;

		public Call OnChange;

		private ValueStore<bool> _last;

		private ObjectStore<global::FrooxEngine.Component> _container;

		private ObjectStore<Action<IChangeable>> _enabledChangedHandler;

		private ObjectStore<SlotEvent> _activeChangedHandler;

		public bool ConditionListensToChanges { get; private set; }

		public void AddedToScope(C context)
		{
			NodeContextPath path = context.CaptureContextPath();
			context.GetEventDispatcher(out var dispatcher);
			dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
			{
				_last.Write(Condition.Evaluate(c, defaultValue: false), c);
			});
			global::FrooxEngine.Component rootContainer = context.GetRootContainer(this);
			Action<IChangeable> value = delegate
			{
				dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
				{
					UpdateListenerState(c);
				});
			};
			SlotEvent value2 = delegate
			{
				dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
				{
					UpdateListenerState(c);
				});
			};
			rootContainer.EnabledField.Changed += value;
			rootContainer.Slot.ActiveChanged += value2;
			_container.Write(rootContainer, context);
			_activeChangedHandler.Write(value2, context);
			_enabledChangedHandler.Write(value, context);
			ConditionListensToChanges = ShouldListen(rootContainer);
		}

		public void Changed(C context)
		{
			bool flag = Condition.Evaluate(context, defaultValue: false);
			bool flag2 = _last.Read(context);
			if (flag != flag2)
			{
				if (ShouldFire(flag))
				{
					OnChange.Execute(context);
				}
				_last.Write(flag, context);
			}
		}

		public void RemovedFromScope(C context)
		{
			global::FrooxEngine.Component component = _container.Read(context);
			component.EnabledField.Changed -= _enabledChangedHandler.Read(context);
			component.Slot.ActiveChanged -= _activeChangedHandler.Read(context);
			_enabledChangedHandler.Clear(context);
			_activeChangedHandler.Clear(context);
			_container.Clear(context);
		}

		protected abstract bool ShouldFire(bool state);

		protected bool ShouldListen(global::FrooxEngine.Component component)
		{
			if (component.Enabled)
			{
				return component.Slot.IsActive;
			}
			return false;
		}

		protected void UpdateListenerState(FrooxEngineContext context)
		{
			global::FrooxEngine.Component rootContainer = context.GetRootContainer(this);
			if (rootContainer != null)
			{
				bool flag = ShouldListen(rootContainer);
				if (flag != ConditionListensToChanges)
				{
					ConditionListensToChanges = flag;
					context.Group.MarkChangeTrackingDirty();
				}
			}
		}
	}
	[NodeOverload("Core.Action.FireOnLocalTrue")]
	public class FireOnLocalTrue<C> : FireOnLocalBool<C> where C : FrooxEngineContext
	{
		protected override bool ShouldFire(bool state)
		{
			return state;
		}
	}
	[NodeOverload("Core.Action.FireOnLocalFalse")]
	public class FireOnLocalFalse<C> : FireOnLocalBool<C> where C : FrooxEngineContext
	{
		protected override bool ShouldFire(bool state)
		{
			return !state;
		}
	}
	[NodeCategory("Flow")]
	[NodeOverload("Core.Action.FireOnLocalChange")]
	public class FireOnLocalValueChange<C, T> : VoidNode<FrooxEngineContext>, IExecutionChangeListener<C>, IScopeEventListener<C>, IMappableNode, INode where C : FrooxEngineContext where T : unmanaged
	{
		public ValueInput<T> Value;

		public Call OnChange;

		private ValueStore<T> _last;

		private ObjectStore<global::FrooxEngine.Component> _container;

		private ObjectStore<Action<IChangeable>> _enabledChangedHandler;

		private ObjectStore<SlotEvent> _activeChangedHandler;

		public bool ValueListensToChanges { get; private set; }

		public void AddedToScope(C context)
		{
			context.GetEventDispatcher(out var dispatcher);
			NodeContextPath path = context.CaptureContextPath();
			dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
			{
				_last.Write(Value.Evaluate(c), c);
			});
			global::FrooxEngine.Component rootContainer = context.GetRootContainer(this);
			Action<IChangeable> value = delegate
			{
				dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
				{
					UpdateListenerState(c);
				});
			};
			SlotEvent value2 = delegate
			{
				dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
				{
					UpdateListenerState(c);
				});
			};
			rootContainer.EnabledField.Changed += value;
			rootContainer.Slot.ActiveChanged += value2;
			_container.Write(rootContainer, context);
			_activeChangedHandler.Write(value2, context);
			_enabledChangedHandler.Write(value, context);
			ValueListensToChanges = ShouldListen(rootContainer);
		}

		public void RemovedFromScope(C context)
		{
			global::FrooxEngine.Component component = _container.Read(context);
			component.EnabledField.Changed -= _enabledChangedHandler.Read(context);
			component.Slot.ActiveChanged -= _activeChangedHandler.Read(context);
			_enabledChangedHandler.Clear(context);
			_activeChangedHandler.Clear(context);
			_container.Clear(context);
		}

		public void Changed(C context)
		{
			T val = Value.Evaluate(context);
			T y = _last.Read(context);
			if (!EqualityComparer<T>.Default.Equals(val, y))
			{
				_last.Write(val, context);
				OnChange.Execute(context);
			}
		}

		protected bool ShouldListen(global::FrooxEngine.Component component)
		{
			if (component.Enabled)
			{
				return component.Slot.IsActive;
			}
			return false;
		}

		protected void UpdateListenerState(FrooxEngineContext context)
		{
			global::FrooxEngine.Component rootContainer = context.GetRootContainer(this);
			if (rootContainer != null)
			{
				bool flag = ShouldListen(rootContainer);
				if (flag != ValueListensToChanges)
				{
					ValueListensToChanges = flag;
					context.Group.MarkChangeTrackingDirty();
				}
			}
		}
	}
	[NodeCategory("Flow")]
	[NodeOverload("Core.Action.FireOnLocalChange")]
	public class FireOnLocalObjectChange<C, T> : VoidNode<FrooxEngineContext>, IExecutionChangeListener<C>, IScopeEventListener<C>, IMappableNode, INode where C : FrooxEngineContext
	{
		public ObjectInput<T> Value;

		public Call OnChange;

		private ObjectStore<T> _last;

		private ObjectStore<global::FrooxEngine.Component> _container;

		private ObjectStore<Action<IChangeable>> _enabledChangedHandler;

		private ObjectStore<SlotEvent> _activeChangedHandler;

		public bool ValueListensToChanges { get; private set; }

		public void AddedToScope(C context)
		{
			context.GetEventDispatcher(out var dispatcher);
			NodeContextPath path = context.CaptureContextPath();
			dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
			{
				_last.Write(Value.Evaluate(c), c);
			});
			global::FrooxEngine.Component rootContainer = context.GetRootContainer(this);
			Action<IChangeable> value = delegate
			{
				dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
				{
					UpdateListenerState(c);
				});
			};
			SlotEvent value2 = delegate
			{
				dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
				{
					UpdateListenerState(c);
				});
			};
			rootContainer.EnabledField.Changed += value;
			rootContainer.Slot.ActiveChanged += value2;
			_container.Write(rootContainer, context);
			_activeChangedHandler.Write(value2, context);
			_enabledChangedHandler.Write(value, context);
			ValueListensToChanges = ShouldListen(rootContainer);
		}

		public void RemovedFromScope(C context)
		{
			global::FrooxEngine.Component component = _container.Read(context);
			component.EnabledField.Changed -= _enabledChangedHandler.Read(context);
			component.Slot.ActiveChanged -= _activeChangedHandler.Read(context);
			_enabledChangedHandler.Clear(context);
			_activeChangedHandler.Clear(context);
			_container.Clear(context);
		}

		public void Changed(C context)
		{
			T val = Value.Evaluate(context);
			T y = _last.Read(context);
			if (!EqualityComparer<T>.Default.Equals(val, y))
			{
				_last.Write(val, context);
				OnChange.Execute(context);
			}
		}

		protected bool ShouldListen(global::FrooxEngine.Component component)
		{
			if (component.Enabled)
			{
				return component.Slot.IsActive;
			}
			return false;
		}

		protected void UpdateListenerState(FrooxEngineContext context)
		{
			global::FrooxEngine.Component rootContainer = context.GetRootContainer(this);
			bool flag = ShouldListen(rootContainer);
			if (flag != ValueListensToChanges)
			{
				ValueListensToChanges = flag;
				context.Group.MarkChangeTrackingDirty();
			}
		}
	}
	[NodeCategory("Flow")]
	public class FireWhileTrue : UserUpdateBase
	{
		public Call OnUpdate;

		public ValueInput<bool> Condition;

		protected override void RunUpdate(FrooxEngineContext context)
		{
			if (Condition.Evaluate(context, defaultValue: false))
			{
				OnUpdate.Execute(context);
			}
		}
	}
	[NodeCategory("Flow")]
	public class LocalFireWhileTrue : VoidNode<FrooxEngineContext>, IExecutionUpdateReceiver<FrooxEngineContext>, INode, IScopeEventListener<FrooxEngineContext>, IMappableNode
	{
		public Call OnUpdate;

		public ValueInput<bool> Condition;

		public void AddedToScope(FrooxEngineContext context)
		{
			NodeContextPath path = context.CaptureContextPath();
			context.Updates.RegisterNode(path, this, 0);
		}

		public void RemovedFromScope(FrooxEngineContext context)
		{
			NodeContextPath path = context.CaptureContextPath();
			context.Updates.UnregisterNode(path, this, 0);
		}

		public void Update(FrooxEngineContext context)
		{
			global::FrooxEngine.Component rootContainer = context.GetRootContainer(this);
			if (rootContainer.Enabled && rootContainer.Slot.IsActive && !rootContainer.IsRemoved && Condition.Evaluate(context, defaultValue: false))
			{
				OnUpdate.Execute(context);
			}
		}
	}
	[NodeCategory("Flow")]
	[NodeName("Local Impulse Timeout", false)]
	[NodeOverload("Core.Actions.LocalImpulseTimeout")]
	public abstract class LocalImpulseTimeout : VoidNode<FrooxEngineContext>
	{
		[PossibleContinuations(new string[] { "Next" })]
		public readonly Operation Trigger;

		[PossibleContinuations(new string[] { })]
		public readonly Operation Reset;

		public Continuation Next;

		private ValueStore<double> _blockUntil;

		protected IOperation DoTrigger(FrooxEngineContext context)
		{
			double num = _blockUntil.Read(context);
			if (context.World.Time.WorldTime >= num)
			{
				_blockUntil.Write(context.World.Time.WorldTime + EvaluateTimeout(context), context);
				return Next.Target;
			}
			return null;
		}

		protected void DoReset(FrooxEngineContext context)
		{
			_blockUntil.Write(-1.0, context);
		}

		protected abstract double EvaluateTimeout(FrooxEngineContext context);

		protected LocalImpulseTimeout()
		{
			Trigger = new Operation(this, 0);
			Reset = new Operation(this, 1);
		}
	}
	public class LocalImpulseTimeoutSeconds : LocalImpulseTimeout
	{
		[OldElementName("TimeoutSeconds")]
		public ValueInput<float> Timeout;

		protected override double EvaluateTimeout(FrooxEngineContext context)
		{
			return Timeout.Evaluate(context, 0f);
		}
	}
	public class LocalImpulseTimeoutTimeSpan : LocalImpulseTimeout
	{
		public ValueInput<TimeSpan> Timeout;

		protected override double EvaluateTimeout(FrooxEngineContext context)
		{
			return Timeout.Evaluate(context).TotalSeconds;
		}
	}
	[NodeCategory("Flow")]
	public class LocalLeakyImpulseBucket : VoidNode<FrooxEngineContext>, IMappableNode, INode
	{
		public Call Pulse;

		public Continuation Overflow;

		public ValueInput<float> Interval;

		[ProtoFlux.Core.DefaultValue(int.MaxValue)]
		public ValueInput<int> MaximumCapacity;

		[ChangeSource]
		public readonly ValueOutput<int> CurrentCapacity;

		[PossibleContinuations(new string[] { "Pulse", "Overflow" })]
		public readonly Operation Trigger;

		[PossibleContinuations(new string[] { })]
		public readonly Operation Reset;

		private ValueStore<int> _capacity;

		private ValueStore<double> _lastPulse;

		private ValueStore<bool> _delayRunning;

		private ObjectStore<CancellationTokenSource> _cancellation;

		private ObjectStore<Action> _scheduler;

		private ObjectStore<NodeContextPath> _path;

		private NodeEventHandler<FrooxEngineContext> _handler;

		private void CapacityChanged(FrooxEngineContext context, NodeContextPath path)
		{
			context.Changes.OutputChanged(new ElementPath<IOutput>(CurrentCapacity, path));
		}

		protected override void ComputeOutputs(FrooxEngineContext context)
		{
			CurrentCapacity.Write(_capacity.Read(context), context);
		}

		private NodeContextPath GetPath(FrooxEngineContext context)
		{
			NodeContextPath nodeContextPath = _path.Read(context);
			if (nodeContextPath.PathLength == 0)
			{
				nodeContextPath = context.CaptureContextPath();
				if (nodeContextPath.PathLength > 0)
				{
					_path.Write(nodeContextPath, context);
				}
			}
			return nodeContextPath;
		}

		protected IOperation DoTrigger(FrooxEngineContext context)
		{
			ref int reference = ref _capacity.Access(context);
			int num = MaximumCapacity.Evaluate(context, int.MaxValue);
			if (reference >= num)
			{
				return Overflow.Target;
			}
			ref bool reference2 = ref _delayRunning.Access(context);
			NodeContextPath path = GetPath(context);
			if (reference2)
			{
				reference++;
				CapacityChanged(context, path);
				return null;
			}
			ref double reference3 = ref _lastPulse.Access(context);
			float num2 = Interval.Evaluate(context, 0f);
			float num3 = (float)(context.Time.WorldTime - reference3);
			if (num3 >= num2)
			{
				reference3 = context.Time.WorldTime;
				return Pulse.Target;
			}
			reference2 = true;
			reference++;
			CapacityChanged(context, path);
			num2 -= num3;
			global::FrooxEngine.Component rootContainer = context.GetRootContainer(this);
			context.GetEventDispatcher(out var dispatcher);
			Action action = _scheduler.Read(context);
			if (action == null)
			{
				if (_handler == null)
				{
					_handler = HandleDelay;
				}
				CancellationTokenSource cancel = new CancellationTokenSource();
				_cancellation.Write(cancel, context);
				action = delegate
				{
					dispatcher.ScheduleEvent(path, _handler, cancel);
				};
				_scheduler.Write(action, context);
			}
			rootContainer.RunInSeconds(num2, action);
			return null;
		}

		private void HandleDelay(FrooxEngineContext context, object data)
		{
			if (data is CancellationTokenSource { IsCancellationRequested: false })
			{
				ref int reference = ref _capacity.Access(context);
				reference--;
				CapacityChanged(context, GetPath(context));
				_lastPulse.Write(context.Time.WorldTime, context);
				Pulse.Execute(context);
				if (reference > 0)
				{
					float seconds = Interval.Evaluate(context, 0f);
					context.GetRootContainer(this).RunInSeconds(seconds, _scheduler.Read(context));
				}
				else
				{
					_delayRunning.Write(value: false, context);
				}
			}
		}

		protected void DoReset(FrooxEngineContext context)
		{
			_cancellation.Read(context)?.Cancel();
			_cancellation.Clear(context);
			_scheduler.Clear(context);
			_capacity.Write(0, context);
			_lastPulse.Write(double.MinValue, context);
			_delayRunning.Write(value: false, context);
			context.Changes.OutputChanged(new ElementPath<IOutput>(CurrentCapacity, context.CaptureContextPath()));
		}

		public LocalLeakyImpulseBucket()
		{
			CurrentCapacity = new ValueOutput<int>(this);
			Trigger = new Operation(this, 0);
			Reset = new Operation(this, 1);
		}
	}
	[NodeCategory("Flow")]
	public class LocalUpdate : VoidNode<FrooxEngineContext>, IExecutionUpdateReceiver<FrooxEngineContext>, INode, IScopeEventListener<FrooxEngineContext>, IMappableNode
	{
		public Call OnUpdate;

		public void AddedToScope(FrooxEngineContext context)
		{
			NodeContextPath path = context.CaptureContextPath();
			context.Updates.RegisterNode(path, this, 0);
		}

		public void RemovedFromScope(FrooxEngineContext context)
		{
			NodeContextPath path = context.CaptureContextPath();
			context.Updates.UnregisterNode(path, this, 0);
		}

		public void Update(FrooxEngineContext context)
		{
			global::FrooxEngine.Component rootContainer = context.GetRootContainer(this);
			if (rootContainer.Enabled && rootContainer.Slot.IsActive && !rootContainer.IsRemoved)
			{
				OnUpdate.Execute(context);
			}
		}
	}
	[NodeName("Once Per Frame", false)]
	[NodeCategory("Flow")]
	public class OnePerFrame : ActionBreakableFlowNode<FrooxEngineContext>
	{
		private ValueStore<double> _lastTime;

		protected override bool Do(FrooxEngineContext context)
		{
			ref double reference = ref _lastTime.Access(context);
			if (reference != context.Time.WorldTime)
			{
				reference = context.Time.WorldTime;
				return true;
			}
			return false;
		}
	}
	[NodeCategory("Flow")]
	[OldTypeName("ProtoFlux.Runtimes.Execution.Nodes.Actions.Timer", null)]
	public class SecondsTimer : UserUpdateBase
	{
		public Call OnUpdate;

		public ValueInput<float> Interval;

		private ValueStore<double> _lastPulse;

		protected override void RunUpdate(FrooxEngineContext context)
		{
			ref double reference = ref _lastPulse.Access(context);
			if (context.Time.WorldTime - reference >= (double)Interval.Evaluate(context, 0f))
			{
				reference = context.Time.WorldTime;
				OnUpdate.Execute(context);
			}
		}
	}
	[NodeCategory("Flow")]
	public class UpdatesTimer : UserUpdateBase
	{
		public Call OnUpdate;

		public ValueInput<int> Interval;

		private ValueStore<int> _lastPulse;

		protected override void RunUpdate(FrooxEngineContext context)
		{
			ref int reference = ref _lastPulse.Access(context);
			if (context.Time.LocalUpdateIndex - reference >= Interval.Evaluate(context, 0))
			{
				reference = context.Time.LocalUpdateIndex;
				OnUpdate.Execute(context);
			}
		}
	}
	[NodeCategory("Flow")]
	public class Update : UserUpdateBase
	{
		public Call OnUpdate;

		protected override void RunUpdate(FrooxEngineContext context)
		{
			OnUpdate.Execute(context);
		}
	}
	public abstract class UpdateBase : VoidNode<FrooxEngineContext>, IExecutionUpdateReceiver<FrooxEngineContext>, INode, IScopeEventListener<FrooxEngineContext>, IMappableNode
	{
		private ValueStore<bool> _registered;

		private ObjectStore<global::FrooxEngine.Component> _container;

		private ObjectStore<Action<IChangeable>> _enabledChangedHandler;

		private ObjectStore<SlotEvent> _activeChangedHandler;

		protected virtual int Bucket => 0;

		protected virtual bool ShouldRegister(FrooxEngineContext context)
		{
			return true;
		}

		protected void UpdateRegistration(FrooxEngineContext context)
		{
			ref bool reference = ref _registered.Access(context);
			global::FrooxEngine.Component rootContainer = context.GetRootContainer(this);
			bool flag = rootContainer.Enabled && rootContainer.Slot.IsActive && ShouldRegister(context);
			if (reference != flag)
			{
				NodeContextPath path = context.CaptureContextPath();
				if (reference)
				{
					context.Updates.UnregisterNode(path, this, Bucket);
				}
				reference = flag;
				if (reference)
				{
					context.Updates.RegisterNode(path, this, Bucket);
				}
			}
		}

		void IExecutionUpdateReceiver<FrooxEngineContext>.Update(FrooxEngineContext context)
		{
			global::FrooxEngine.Component component = _container.Read(context);
			if (component.Enabled && component.Slot.IsActive && !component.IsRemoved)
			{
				RunUpdate(context);
			}
		}

		public void AddedToScope(FrooxEngineContext context)
		{
			UpdateRegistration(context);
			global::FrooxEngine.Component rootContainer = context.GetRootContainer(this);
			NodeContextPath path = context.CaptureContextPath();
			context.GetEventDispatcher(out var dispatcher);
			Action<IChangeable> value = delegate
			{
				dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
				{
					UpdateRegistration(c);
				});
			};
			SlotEvent value2 = delegate
			{
				dispatcher.ScheduleEvent(path, delegate(FrooxEngineContext c)
				{
					UpdateRegistration(c);
				});
			};
			rootContainer.EnabledField.Changed += value;
			rootContainer.Slot.ActiveChanged += value2;
			_container.Write(rootContainer, context);
			_activeChangedHandler.Write(value2, context);
			_enabledChangedHandler.Write(value, context);
			OnAddedToScope(context, path);
		}

		public void RemovedFromScope(FrooxEngineContext context)
		{
			OnRemoveFromScope(context);
			ref bool reference = ref _registered.Access(context);
			if (reference)
			{
				NodeContextPath path = context.CaptureContextPath();
				context.Updates.UnregisterNode(path, this, Bucket);
				reference = false;
			}
			global::FrooxEngine.Component component = _container.Read(context);
			component.EnabledField.Changed -= _enabledChangedHandler.Read(context);
			component.Slot.ActiveChanged -= _activeChangedHandler.Read(context);
			_enabledChangedHandler.Clear(context);
			_activeChangedHandler.Clear(context);
			_container.Clear(context);
		}

		protected virtual void OnAddedToScope(FrooxEngineContext context, NodeContextPath path)
		{
		}

		protected virtual void OnRemoveFromScope(FrooxEngineContext context)
		{
		}

		protected abstract void RunUpdate(FrooxEngineContext context);
	}
	public abstract class ValueFunctionUpdateBase<T> : UpdateBase, IValueOutput<T>, IOutput<T>, IOutput where T : unmanaged
	{
		public Node OwnerNode => this;

		public override int OutputCount => 1;

		public Type OutputType => typeof(T);

		public DataClass OutputDataClass => DataClass.Value;

		public override bool CanBeEvaluated => true;

		public override IOutput GetOutput(int index)
		{
			if (index != 0)
			{
				throw new ArgumentOutOfRangeException("index");
			}
			return this;
		}

		public override Type GetOutputType(int index)
		{
			if (index != 0)
			{
				throw new ArgumentOutOfRangeException("index");
			}
			return typeof(T);
		}

		public override DataClass GetOutputTypeClass(int index)
		{
			if (index != 0)
			{
				throw new ArgumentOutOfRangeException("index");
			}
			return DataClass.Value;
		}

		public sealed override void Evaluate(FrooxEngineContext context)
		{
			T value = Compute(context);
			context.PopInputs();
			context.Values.Push(value);
		}

		protected abstract T Compute(FrooxEngineContext context);
	}
	public abstract class ObjectFunctionUpdateBase<T> : UpdateBase, IObjectOutput<T>, IOutput<T>, IOutput
	{
		public Node OwnerNode => this;

		public override int OutputCount => 1;

		public Type OutputType => typeof(T);

		public DataClass OutputDataClass => DataClass.Object;

		public override bool CanBeEvaluated => true;

		public override IOutput GetOutput(int index)
		{
			if (index != 0)
			{
				throw new ArgumentOutOfRangeException("index");
			}
			return this;
		}

		public override Type GetOutputType(int index)
		{
			if (index != 0)
			{
				throw new ArgumentOutOfRangeException("index");
			}
			return typeof(T);
		}

		public override DataClass GetOutputTypeClass(int index)
		{
			if (index != 0)
			{
				throw new ArgumentOutOfRangeException("index");
			}
			return DataClass.Object;
		}

		public sealed override void Evaluate(FrooxEngineContext context)
		{
			T obj = Compute(context);
			context.PopInputs();
			context.Objects.Push(obj);
		}

		protected abstract T Compute(FrooxEngineContext context);
	}
	public abstract class UserUpdateBase : UpdateBase
	{
		public readonly GlobalRef<global::FrooxEngine.User> UpdatingUser;

		public readonly GlobalRef<bool> SkipIfNull;

		private void OnUpdatingUserChanged(global::FrooxEngine.User user, FrooxEngineContext context)
		{
			UpdateRegistration(context);
		}

		private void OnSkipIfNullChanged(bool skipIfNull, FrooxEngineContext context)
		{
			UpdateRegistration(context);
		}

		protected override bool ShouldRegister(FrooxEngineContext context)
		{
			global::FrooxEngine.User user = UpdatingUser.Read(context);
			if (user != null && user.IsRemoved)
			{
				user = null;
			}
			if (user == null && !SkipIfNull.Read(context))
			{
				user = context.World.HostUser;
			}
			return user?.IsLocalUser ?? false;
		}

		protected UserUpdateBase()
		{
			UpdatingUser = new GlobalRef<global::FrooxEngine.User>(this, 0);
			SkipIfNull = new GlobalRef<bool>(this, 1);
		}
	}
}
internal class ProtoFluxNodesFrooxEngine_ProcessedByFody
{
	internal const string FodyVersion = "6.7.0.0";

	internal const string NodeWeaver = "1.0.0.0";
}
