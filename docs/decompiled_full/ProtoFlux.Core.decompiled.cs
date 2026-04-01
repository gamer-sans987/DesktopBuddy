using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EnumsNET;
using ProtoFlux.Core;
using ProtoFlux.Runtimes.Execution;
using ProtoFlux.Runtimes.Execution.Nodes;
using ProtoFlux.Runtimes.Execution.Nodes.Casts;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: AssemblyTitle("ProtoFlux.Core")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("ProtoFlux.Core")]
[assembly: AssemblyCopyright("Copyright ©  2022")]
[assembly: AssemblyTrademark("")]
[assembly: ComVisible(false)]
[assembly: Guid("18abf908-7fb1-40b0-a9a0-dc21cd43ff72")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: TargetFramework(".NETCoreApp,Version=v9.0", FrameworkDisplayName = ".NET 9.0")]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
[assembly: AssemblyVersion("1.0.0.0")]
[module: UnverifiableCode]
[module: RefSafetyRules(11)]
namespace ProtoFlux.Runtimes.Execution
{
	public class ExecutionContext
	{
		public struct StackFrame
		{
			public int valueBottom;

			public int objectBottom;

			public ushort pinCount;

			public ushort sourceFrame;

			public int valueStoreOffset;

			public int objectStoreOffset;

			public IExecutionRuntime runtime;

			public IExecutionNestedNode nestedNode;

			public ScopePoint sharedScope;

			public override string ToString()
			{
				return $"Value: {valueBottom}; Object: {objectBottom}; Pins: {pinCount}; Source: {sourceFrame}; ValueStore: {valueStoreOffset}; ObjectStore: {objectStoreOffset}; Runtime: {runtime}";
			}
		}

		public struct StackLayout
		{
			public short[] layout;

			public short valueSize;

			public short objectSize;

			public StackLayout(short[] layout, short valueSize, short objectSize)
			{
				this.layout = layout;
				this.valueSize = valueSize;
				this.objectSize = objectSize;
			}

			public override string ToString()
			{
				return $"ValueSize: {valueSize}, ObjectSize: {objectSize}, Stack: {string.Join(", ", layout ?? new short[0])}";
			}
		}

		private Stack<ExecutionImpulseExportHandler> _impulseExportHandlers = new Stack<ExecutionImpulseExportHandler>();

		private Stack<ExecutionAsyncImpulseExportHandler> _asyncImpulseExportHandlers = new Stack<ExecutionAsyncImpulseExportHandler>();

		private StackFrame[] _frames = new StackFrame[1024];

		private ushort _allocatedFrames;

		private ushort _currentFrameIndex;

		internal StackLayout stackLayout;

		private HashSet<IExecutionRuntime> onceEnteredRuntimes = new HashSet<IExecutionRuntime>();

		public ValueStack Values { get; private set; }

		public ObjectStack Objects { get; private set; }

		public int ImpulseExport { get; internal set; } = -1;

		public int CurrentFramePins => _frames[_currentFrameIndex].pinCount;

		public int CurrentValueStoreOffset => _frames[_currentFrameIndex].valueStoreOffset;

		public int CurrentObjectStoreOffset => _frames[_currentFrameIndex].objectStoreOffset;

		public SharedExecutionScope SharedScope { get; set; }

		public bool IsEmpty => _allocatedFrames == 0;

		public ushort CurrentFrameIndex => _currentFrameIndex;

		public bool CurrentFrameIsTop => _currentFrameIndex == _allocatedFrames - 1;

		public int MaxDepth { get; set; } = 256;

		public int AutoYieldSafetyDepth { get; set; } = 128;

		public int CurrentDepth { get; private set; }

		public int InheritedDepth { get; private set; }

		public bool AbortExecution { get; set; }

		public IExecutionRuntime CurrentRuntime
		{
			get
			{
				if (IsEmpty)
				{
					return null;
				}
				return _frames[_currentFrameIndex].runtime;
			}
		}

		public IExecutionNestedNode CurrentNestedNode
		{
			get
			{
				if (IsEmpty)
				{
					return null;
				}
				return _frames[_currentFrameIndex].nestedNode;
			}
		}

		public ScopePoint CurrentScope
		{
			get
			{
				if (IsEmpty)
				{
					return SharedScope.RootScope;
				}
				return _frames[_currentFrameIndex].sharedScope;
			}
		}

		public IExecutionRuntime GetFrameRuntime(int frameIndex)
		{
			if (frameIndex > _allocatedFrames)
			{
				throw new ArgumentOutOfRangeException("frameIndex");
			}
			return _frames[frameIndex].runtime;
		}

		public IExecutionNestedNode GetFrameNestedNode(int frameIndex)
		{
			if (frameIndex > _allocatedFrames)
			{
				throw new ArgumentOutOfRangeException("frameIndex");
			}
			return _frames[frameIndex].nestedNode;
		}

		public int GetFrameSource(int frameIndex)
		{
			if (frameIndex > _allocatedFrames)
			{
				throw new ArgumentOutOfRangeException("frameIndex");
			}
			return _frames[frameIndex].sourceFrame;
		}

		public ExecutionContext()
			: this(new SharedExecutionScope())
		{
		}

		public ExecutionContext(SharedExecutionScope scope)
		{
			Values = new ValueStack();
			Objects = new ObjectStack();
			SharedScope = scope;
		}

		public string StackLayoutDebug()
		{
			return stackLayout.ToString();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void PopInputs()
		{
			if (stackLayout.valueSize > 0)
			{
				Values.Pop(stackLayout.valueSize);
			}
			if (stackLayout.objectSize > 0)
			{
				Objects.Pop(stackLayout.objectSize);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T ReadValue<T>(int index) where T : unmanaged
		{
			return ReadValueDirect<T>(stackLayout.layout[index]);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T ReadObject<T>(int index)
		{
			return ReadObjectDirect<T>(stackLayout.layout[index]);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T ReadValue<T>(int index, int offset) where T : unmanaged
		{
			int num = stackLayout.layout[index];
			if (num >= 0)
			{
				num += offset;
			}
			return ReadValueDirect<T>(num);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T ReadObject<T>(int index, int offset)
		{
			int num = stackLayout.layout[index];
			if (num >= 0)
			{
				num += offset;
			}
			return ReadObjectDirect<T>(num);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T ReadValueDirect<T>(int offset) where T : unmanaged
		{
			return Values.Read<T>(offset);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T ReadObjectDirect<T>(int offset)
		{
			return Objects.Read<T>(offset);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T ReadStoredValue<T>(int offset) where T : unmanaged
		{
			return Unsafe.ReadUnaligned<T>(in SharedScope.ValuesStore[CurrentValueStoreOffset + offset]);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteStoredValue<T>(int offset, T value)
		{
			Unsafe.WriteUnaligned(ref SharedScope.ValuesStore[CurrentValueStoreOffset + offset], value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ref T AccessStoredValue<T>(int offset)
		{
			return ref Unsafe.As<byte, T>(ref SharedScope.ValuesStore[CurrentValueStoreOffset + offset]);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T ReadStoredObject<T>(int offset)
		{
			object obj = SharedScope.ObjectsStore[CurrentObjectStoreOffset + offset];
			if (obj is T)
			{
				return (T)obj;
			}
			return default(T);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteStoredObject<T>(int offset, T value)
		{
			SharedScope.ObjectsStore[CurrentObjectStoreOffset + offset] = value;
		}

		public void AllocateFrame(IExecutionRuntime runtime, IExecutionNestedNode nestedNode)
		{
			ushort allocatedFrames = _allocatedFrames;
			ScopePoint currentScope = CurrentScope;
			ref StackFrame reference = ref CreateStackFrame(runtime, nestedNode);
			if (runtime.RequiresScopeData)
			{
				ScopePoint scopePoint = (reference.sharedScope = SharedScope.GetNestedScopeOrAllocate(currentScope, runtime, nestedNode));
				reference.valueStoreOffset = scopePoint.ValuesStoreOffset;
				reference.objectStoreOffset = scopePoint.ObjectsStoreOffset;
			}
			else
			{
				reference.sharedScope = currentScope;
				reference.valueStoreOffset = -1073741824;
				reference.objectStoreOffset = -1073741824;
			}
			_currentFrameIndex = allocatedFrames;
		}

		private ref StackFrame CreateStackFrame(IExecutionRuntime runtime, IExecutionNestedNode nestedNode)
		{
			if (_allocatedFrames > 0)
			{
				ref StackFrame reference = ref _frames[_allocatedFrames - 1];
				Values.Bottom = reference.valueBottom;
				Objects.Bottom = reference.objectBottom;
			}
			int totalValueStackSize = runtime.TotalValueStackSize;
			int totalObjectStackSize = runtime.TotalObjectStackSize;
			if (totalValueStackSize > 0)
			{
				Values.Allocate(totalValueStackSize);
			}
			if (totalObjectStackSize > 0)
			{
				Objects.Allocate(totalObjectStackSize);
			}
			ref StackFrame reference2 = ref _frames[_allocatedFrames];
			reference2.valueBottom = Values.Bottom;
			reference2.objectBottom = Objects.Bottom;
			reference2.pinCount = 0;
			reference2.sourceFrame = _currentFrameIndex;
			reference2.runtime = runtime;
			reference2.nestedNode = nestedNode;
			_allocatedFrames++;
			return ref reference2;
		}

		public void DeallocateFrame()
		{
			if (_currentFrameIndex != _allocatedFrames - 1)
			{
				throw new InvalidOperationException("Cannot deallocate frame that's not on the top");
			}
			if (_allocatedFrames == 0)
			{
				throw new InvalidOperationException("There are currently no allocated frames");
			}
			ref StackFrame reference = ref _frames[_currentFrameIndex];
			if (reference.pinCount > 0)
			{
				throw new InvalidOperationException("Current frame is still pinned, cannot deallocated");
			}
			reference.runtime = null;
			reference.nestedNode = null;
			_allocatedFrames--;
			if (_allocatedFrames > 0)
			{
				StepIntoFrame(reference.sourceFrame);
				return;
			}
			Values.Bottom = 0;
			Objects.Bottom = 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void PinFrame()
		{
			ref ushort pinCount = ref _frames[_currentFrameIndex].pinCount;
			checked
			{
				pinCount = (ushort)(unchecked((uint)pinCount) + 1u);
			}
		}

		public ushort ReturnToPreviousFrame()
		{
			ref StackFrame reference = ref _frames[_currentFrameIndex];
			ushort currentFrameIndex = _currentFrameIndex;
			StepIntoFrame(reference.sourceFrame);
			return currentFrameIndex;
		}

		public void StepIntoFrame(ushort index)
		{
			_currentFrameIndex = index;
			ref StackFrame reference = ref _frames[index];
			Values.Bottom = reference.valueBottom;
			Objects.Bottom = reference.objectBottom;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool UnpinFrame()
		{
			ref StackFrame reference = ref _frames[_currentFrameIndex];
			ref ushort pinCount = ref reference.pinCount;
			checked
			{
				pinCount = (ushort)(unchecked((uint)pinCount) - 1u);
				return reference.pinCount == 0;
			}
		}

		public Span<StackFrame> GetRawStackFrame()
		{
			return _frames;
		}

		internal void PopToLocal(in LocalNodeData mapping)
		{
			if (mapping.valueSize > 0)
			{
				Values.Store(mapping.valueSize, mapping.valueStart);
			}
			if (mapping.objectSize > 0)
			{
				Objects.Store(mapping.objectSize, mapping.objectStart);
			}
		}

		internal void PushFromLocal(in LocalNodeData mapping)
		{
			if (mapping.valueSize > 0)
			{
				Values.Load(mapping.valueSize, mapping.valueStart);
			}
			if (mapping.objectSize > 0)
			{
				Objects.Load(mapping.objectSize, mapping.objectStart);
			}
		}

		internal void PushImpulseExportHandler(ExecutionImpulseExportHandler handler)
		{
			_impulseExportHandlers.Push(handler);
		}

		internal void PushAsyncImpulseExportHandler(ExecutionAsyncImpulseExportHandler handler)
		{
			_asyncImpulseExportHandlers.Push(handler);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal void PopImpulseExportHandler()
		{
			_impulseExportHandlers.Pop();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal void PopAsyncImpulseExportHandler()
		{
			_asyncImpulseExportHandlers.Pop();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal void InvokeImpulseExport(int index)
		{
			if (_impulseExportHandlers.Count != 0)
			{
				_impulseExportHandlers.Peek()(this, index);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal Task InvokeAsyncImpulseExport(int index)
		{
			if (_asyncImpulseExportHandlers.Count == 0)
			{
				return Task.CompletedTask;
			}
			return _asyncImpulseExportHandlers.Peek()(this, index);
		}

		public void CaptureContextFrom(ExecutionContext source)
		{
			if (_allocatedFrames > 0)
			{
				throw new InvalidOperationException("Cannot capture context when there are already allocated frames.");
			}
			SharedScope = source.SharedScope;
			source.WalkStackAndCopyTo(source._currentFrameIndex, this);
			_currentFrameIndex = (ushort)(_allocatedFrames - 1);
		}

		public NodeContextPath CaptureContextPath()
		{
			IExecutionRuntime rootRuntime;
			return CaptureContextPath(out rootRuntime);
		}

		public NodeContextPath CaptureContextPath(out IExecutionRuntime rootRuntime)
		{
			int num = ComputeStackDepth();
			if (num == 1)
			{
				rootRuntime = _frames[CurrentFrameIndex].runtime;
				return default(NodeContextPath);
			}
			INode[] array = new INode[num - 1];
			int num2 = array.Length - 1;
			ushort num3 = CurrentFrameIndex;
			while (_frames[num3].sourceFrame != num3)
			{
				ref StackFrame reference = ref _frames[num3];
				array[num2--] = reference.nestedNode;
				num3 = reference.sourceFrame;
			}
			rootRuntime = _frames[num3].runtime;
			return new NodeContextPath(array);
		}

		public bool IsCurrentPath(IExecutionRuntime rootRuntime, NodeContextPath path)
		{
			int num = path.PathLength - 1;
			ushort num2 = CurrentFrameIndex;
			while (_frames[num2].sourceFrame != num2)
			{
				if (num < 0)
				{
					return false;
				}
				ref StackFrame reference = ref _frames[num2];
				if (reference.nestedNode != path[num--])
				{
					return false;
				}
				num2 = reference.sourceFrame;
			}
			if (num >= 0)
			{
				return false;
			}
			return _frames[num2].runtime == rootRuntime;
		}

		public int ComputeStackDepth()
		{
			int num = 0;
			ushort num2 = CurrentFrameIndex;
			ushort num3;
			do
			{
				num3 = num2;
				num2 = _frames[num3].sourceFrame;
				num++;
			}
			while (num3 != num2);
			return num;
		}

		internal void EnterExecution()
		{
			if (++CurrentDepth == MaxDepth)
			{
				throw new StackOverflowException($"ProtoFlux execution flow reached maximum depth of {CurrentDepth}");
			}
		}

		internal async Task TryEnterAsyncExecution()
		{
			int currentDepth = CurrentDepth + 1;
			CurrentDepth = currentDepth;
			if (InheritedDepth > 0 && CurrentDepth >= MaxDepth - AutoYieldSafetyDepth)
			{
				SubtractInheritedDepth();
				await Task.Yield();
			}
			if (CurrentDepth == MaxDepth)
			{
				throw new StackOverflowException($"ProtoFlux execution flow reached maximum depth of {CurrentDepth}");
			}
		}

		internal void ExitExecution()
		{
			CurrentDepth--;
		}

		public void InheritExecutionDepthFrom(ExecutionContext context)
		{
			InheritedDepth = context.CurrentDepth;
			CurrentDepth = InheritedDepth;
		}

		public void SubtractInheritedDepth()
		{
			if (InheritedDepth > 0)
			{
				CurrentDepth -= InheritedDepth;
				InheritedDepth = 0;
			}
		}

		public void ClearExecutionDepth()
		{
			CurrentDepth = 0;
			InheritedDepth = 0;
		}

		private void WalkStackAndCopyTo(ushort frameIndex, ExecutionContext target)
		{
			ref StackFrame reference = ref _frames[frameIndex];
			if (reference.sourceFrame != frameIndex)
			{
				WalkStackAndCopyTo(reference.sourceFrame, target);
			}
			ref StackFrame reference2 = ref target.CreateStackFrame(reference.runtime, reference.nestedNode);
			reference2.sharedScope = reference.sharedScope;
			reference2.valueStoreOffset = reference.valueStoreOffset;
			reference2.objectStoreOffset = reference.objectStoreOffset;
			Values.CopyBottomTo(reference.valueBottom, reference2.valueBottom, reference.runtime.TotalValueStackSize, target.Values);
			Objects.CopyBottomTo(reference.objectBottom, reference2.objectBottom, reference.runtime.TotalObjectStackSize, target.Objects);
		}

		public bool TryEnterRuntimeOnce(IExecutionRuntime runtime)
		{
			return onceEnteredRuntimes.Add(runtime);
		}

		public void ExitRuntimeOnce(IExecutionRuntime runtime)
		{
			if (!onceEnteredRuntimes.Remove(runtime))
			{
				throw new InvalidOperationException("Runtime wasn't currently entered: " + runtime);
			}
		}
	}
	public static class ExecutionContextExtensions
	{
		private static NotImplementedException Exception()
		{
			return new NotImplementedException("This method must be replaced by NodeWeaver");
		}

		public static T ReadValue<T>(this ValueArgument<T> input, ExecutionContext context) where T : unmanaged
		{
			throw Exception();
		}

		public static T ReadObject<T>(this ObjectArgument<T> input, ExecutionContext context)
		{
			throw Exception();
		}

		public static T ReadValue<T>(this IInputList list, int index, ExecutionContext context) where T : unmanaged
		{
			throw Exception();
		}

		public static T ReadObject<T>(this IInputList list, int index, ExecutionContext context)
		{
			throw Exception();
		}

		public static T ReadValue<T>(this ValueArgumentList<T> list, int index, ExecutionContext context) where T : unmanaged
		{
			throw Exception();
		}

		public static T ReadObject<T>(this ObjectArgumentList<T> list, int index, ExecutionContext context)
		{
			throw Exception();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T ReadValue<T>(this int index, ExecutionContext context) where T : unmanaged
		{
			return context.ReadValue<T>(index);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T ReadObject<T>(this int index, ExecutionContext context)
		{
			return context.ReadObject<T>(index);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T ReadValueDirect<T>(this int offset, ExecutionContext context) where T : unmanaged
		{
			return context.ReadValueDirect<T>(offset);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T ReadObjectDirect<T>(this int offset, ExecutionContext context)
		{
			return context.ReadObjectDirect<T>(offset);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T ReadValue<T>(this IInputList list, int index, ExecutionContext context, int listOffset) where T : unmanaged
		{
			if (index < 0 || index >= list.Count)
			{
				throw new ArgumentOutOfRangeException("index");
			}
			return context.ReadValue<T>(listOffset + index);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T ReadObject<T>(this IInputList list, int index, ExecutionContext context, int listOffset)
		{
			if (index < 0 || index >= list.Count)
			{
				throw new ArgumentOutOfRangeException("index");
			}
			return context.ReadObject<T>(listOffset + index);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T ReadValue<T>(this ValueArgumentList<T> list, int index, ExecutionContext context, int listOffset) where T : unmanaged
		{
			if (index < 0 || index >= list.Count)
			{
				throw new ArgumentOutOfRangeException("index");
			}
			return context.ReadValue<T>(listOffset + index);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T ReadObject<T>(this ObjectArgumentList<T> list, int index, ExecutionContext context, int listOffset)
		{
			if (index < 0 || index >= list.Count)
			{
				throw new ArgumentOutOfRangeException("index");
			}
			return context.ReadObject<T>(listOffset + index);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T Evaluate<T>(this ValueInput<T> input, ExecutionContext context, T defaultValue = default(T)) where T : unmanaged
		{
			if (input.Source == null)
			{
				return defaultValue;
			}
			return context.CurrentRuntime.EvaluateValue<T>(input.Source, context);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T Evaluate<T>(this ObjectInput<T> input, ExecutionContext context, T defaultValue = default(T))
		{
			if (input.Source == null)
			{
				return defaultValue;
			}
			return context.CurrentRuntime.EvaluateObject<T>(input.Source, context);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T Evaluate<T>(this ValueInputList<T> input, int index, ExecutionContext context, T defaultValue = default(T)) where T : unmanaged
		{
			IOutput inputSource = input.GetInputSource(index);
			if (inputSource == null)
			{
				return defaultValue;
			}
			return context.CurrentRuntime.EvaluateValue<T>(inputSource, context);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T Evaluate<T>(this ObjectInputList<T> input, int index, ExecutionContext context, T defaultValue = default(T))
		{
			IOutput inputSource = input.GetInputSource(index);
			if (inputSource == null)
			{
				return defaultValue;
			}
			return context.CurrentRuntime.EvaluateObject<T>(inputSource, context);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Write<T>(this ValueOutput<T> output, T value, ExecutionContext context) where T : unmanaged
		{
			context.CurrentRuntime.SetValue(output, value, context);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Write<T>(this ObjectOutput<T> output, T value, ExecutionContext context)
		{
			context.CurrentRuntime.SetObject(output, value, context);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Execute(this IImpulse impulse, ExecutionContext context)
		{
			if (impulse is ICall call)
			{
				if (call.Target != null)
				{
					context.CurrentRuntime.Execute(call.Target, context);
				}
				return;
			}
			throw new InvalidOperationException("Dynamic execution can only be invoked on Calls");
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Task ExecuteAsync(this IImpulse impulse, ExecutionContext context)
		{
			if (impulse is ICall call)
			{
				if (call.Target == null)
				{
					return Task.CompletedTask;
				}
				context.CurrentRuntime.Execute(call.Target, context);
				return Task.CompletedTask;
			}
			if (impulse is IAsyncCall asyncCall)
			{
				if (asyncCall.Target == null)
				{
					return Task.CompletedTask;
				}
				return context.CurrentRuntime.ExecuteAsync(asyncCall.Target, context);
			}
			throw new InvalidOperationException("Dynamic execution can only be invoked on Calls");
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Execute(this ICall call, ExecutionContext context)
		{
			if (call.Target != null)
			{
				context.CurrentRuntime.Execute(call.Target, context);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Execute(this Call call, ExecutionContext context)
		{
			if (call.Target != null)
			{
				context.CurrentRuntime.Execute(call.Target, context);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Task ExecuteAsync(this AsyncCall call, ExecutionContext context)
		{
			if (call.Target == null)
			{
				return Task.CompletedTask;
			}
			return context.CurrentRuntime.ExecuteAsync(call.Target, context);
		}

		public static void MoveToContext<C>(this C context, NodeContextPath previousPath, NodeContextPath nextPath) where C : ExecutionContext
		{
			int num = previousPath.FindSharedRootLength(nextPath);
			int num2 = previousPath.PathLength;
			while (num2 > num)
			{
				context.UnpinFrame();
				((NestedNode<C>)previousPath[--num2]).ExitTargetFrame(context);
			}
			while (num2 < nextPath.PathLength)
			{
				((NestedNode<C>)nextPath[num2++]).EnterTargetFrame(context);
				context.PinFrame();
			}
		}
	}
	public abstract class ExtendedExecutionContext<C> : ExecutionContext where C : ExtendedExecutionContext<C>
	{
		private ExecutionEventDispatcher<C> _eventDispatcher;

		public ExecutionUpdateDispatcher<C> Updates { get; set; }

		public ExecutionChangesDispatcher<C> Changes { get; set; }

		public int ScheduledEventCount => _eventDispatcher?.ScheduledEventCount ?? 0;

		public void GetEventDispatcher(out ExecutionEventDispatcher<C> eventDispatcher)
		{
			eventDispatcher = _eventDispatcher;
		}

		public void DispatchEvents(ExecutionRuntime<C> runtime)
		{
			_eventDispatcher.DispatchEvents(runtime, (C)this);
		}

		public void SetEventDispatcher(ExecutionEventDispatcher<C> eventDispatcher)
		{
			_eventDispatcher = eventDispatcher;
		}

		public ExtendedExecutionContext()
			: this(new SharedExecutionScope(), new ExecutionUpdateDispatcher<C>(), new ExecutionEventDispatcher<C>(), (ExecutionChangesDispatcher<C>)null)
		{
		}

		public ExtendedExecutionContext(SharedExecutionScope sharedExecutionScope, ExecutionUpdateDispatcher<C> updateDispatcher, ExecutionEventDispatcher<C> eventDispatcher, ExecutionChangesDispatcher<C> changesDispatcher)
			: base(sharedExecutionScope)
		{
			Updates = updateDispatcher;
			Changes = changesDispatcher;
			_eventDispatcher = eventDispatcher;
		}
	}
	public class ExtendedExecutionContext : ExtendedExecutionContext<ExtendedExecutionContext>
	{
	}
	public interface IGlobalValue
	{
		bool IsWriteable { get; }

		Type ValueType { get; }

		object BoxedValue { get; }
	}
	public interface IGlobalValue<T> : IGlobalValue
	{
		T Value { get; }

		bool SetValue(T newValue);
	}
	public class ObjectStack
	{
		private object[] stack = new object[10240];

		public int Top { get; private set; }

		public int Bottom { get; internal set; }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int GetBottomPosition(int bottom, int offset)
		{
			return stack.Length - bottom + offset;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int GetPosition(int offset)
		{
			if (offset < 0)
			{
				int num = Top + offset;
				if (num < 0)
				{
					throw new ArgumentOutOfRangeException("Read location is out of range");
				}
				return num;
			}
			int num2 = stack.Length - Bottom + offset;
			if (num2 < Top)
			{
				throw new ArgumentOutOfRangeException("Read location is out of range");
			}
			return num2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Push<T>(T obj)
		{
			if (Top == stack.Length - 1 - Bottom)
			{
				throw new Exception($"Stack overflow. Bottom: {Bottom}, Top: {Top}, Size: {stack.Length}");
			}
			stack[Top++] = obj;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T Pop<T>()
		{
			if (Top == 0)
			{
				throw new Exception($"Stack underflow. Bottom: {Bottom}, Top: {Top}, Size: {stack.Length}");
			}
			Top--;
			T result = ((!(stack[Top] is T val)) ? default(T) : val);
			stack[Top] = null;
			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T Read<T>(int offset)
		{
			object obj = stack[GetPosition(offset)];
			if (obj is T)
			{
				return (T)obj;
			}
			return default(T);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Write<T>(int offset, T obj)
		{
			stack[GetPosition(offset)] = obj;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Pop(int size)
		{
			Top -= size;
			if (Top < 0)
			{
				Top += size;
				throw new InvalidOperationException("Stack underflow");
			}
			Array.Clear(stack, Top, size);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Store(int size, int offset)
		{
			if (offset < 0)
			{
				throw new ArgumentOutOfRangeException("offset");
			}
			int num = stack.Length - Bottom + offset;
			if (num < Top)
			{
				throw new ArgumentOutOfRangeException("offset");
			}
			Array.Copy(stack, Top - size, stack, num, size);
			Array.Clear(stack, Top, size);
			Top -= size;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Load(int size, int offset)
		{
			if (offset < 0)
			{
				throw new ArgumentOutOfRangeException("offset");
			}
			int num = Top + size;
			int num2 = stack.Length - Bottom + offset;
			if (num > stack.Length - Bottom)
			{
				throw new Exception("Stack overflow");
			}
			if (num2 < num)
			{
				throw new ArgumentOutOfRangeException("offset");
			}
			Array.Copy(stack, num2, stack, Top, size);
			Top = num;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Allocate(int count)
		{
			Bottom += count;
			if (Bottom > stack.Length - Top)
			{
				Bottom -= count;
				throw new InvalidOperationException("Stack overflow");
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Clear(int position, int size)
		{
			Array.Clear(stack, stack.Length - Bottom + position, size);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void DeAllocate(int size)
		{
			Bottom -= size;
			if (Bottom < 0)
			{
				Bottom += size;
				throw new InvalidOperationException("Stack underflow");
			}
			Array.Clear(stack, stack.Length - Bottom - size, size);
		}

		public void CopyBottomTo(int sourceBottom, int targetBottom, int count, ObjectStack target)
		{
			int bottomPosition = GetBottomPosition(sourceBottom, 0);
			int bottomPosition2 = GetBottomPosition(targetBottom, 0);
			Array.Copy(stack, bottomPosition, target.stack, bottomPosition2, count);
		}
	}
	public readonly struct ScopeKey : IEquatable<ScopeKey>
	{
		public readonly IExecutionRuntime runtime;

		public readonly IExecutionNestedNode nestedNode;

		public ScopeKey(IExecutionRuntime runtime, IExecutionNestedNode nestedNode = null)
		{
			this.runtime = runtime;
			this.nestedNode = nestedNode;
		}

		public bool Equals(ScopeKey other)
		{
			if (runtime == other.runtime)
			{
				return nestedNode == other.nestedNode;
			}
			return false;
		}

		public override bool Equals(object obj)
		{
			if (obj is ScopeKey other)
			{
				return Equals(other);
			}
			return false;
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(runtime, nestedNode);
		}

		public static bool operator ==(ScopeKey left, ScopeKey right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(ScopeKey left, ScopeKey right)
		{
			return !(left == right);
		}
	}
	public readonly struct NodeStoreOffsets
	{
		public readonly int valuesOffset;

		public readonly int objectsOffset;

		public NodeStoreOffsets(int valuesOffset, int objectsOffset)
		{
			this.valuesOffset = valuesOffset;
			this.objectsOffset = objectsOffset;
		}
	}
	public class ScopePoint
	{
		private IGlobalValue[] _mappedGlobals;

		private ConcurrentDictionary<ScopeKey, ScopePoint> nestedScopes;

		private Dictionary<IExecutionNode, NodeStoreOffsets> explicitNodeMap;

		public ScopePoint Parent { get; private set; }

		public ScopeKey Key { get; private set; }

		public IExecutionRuntime Runtime => Key.runtime;

		public IExecutionNestedNode NestedNode => Key.nestedNode;

		public bool IsRoot => Parent == null;

		public int Depth { get; private set; }

		public int NestedScopeCount => nestedScopes?.Count ?? 0;

		public int ValuesStoreOffset { get; private set; }

		public int ObjectsStoreOffset { get; private set; }

		public bool AreGlobalsMapped => _mappedGlobals != null;

		public ScopePoint()
		{
		}

		public ScopePoint(ScopePoint parent, in ScopeKey key, int valuesStoreOffset, int objectsStoreOffset)
		{
			Parent = parent;
			Key = key;
			ValuesStoreOffset = valuesStoreOffset;
			ObjectsStoreOffset = objectsStoreOffset;
			Depth = parent.Depth + 1;
		}

		public ScopePoint GetNestedScope(in ScopeKey key)
		{
			if (nestedScopes == null)
			{
				return null;
			}
			if (nestedScopes.TryGetValue(key, out var value))
			{
				return value;
			}
			return null;
		}

		public ScopePoint AllocateScope(ScopeKey key, int valuesStoreOffset, int objectsStoreOffset)
		{
			if (nestedScopes == null)
			{
				nestedScopes = new ConcurrentDictionary<ScopeKey, ScopePoint>();
			}
			ScopePoint scopePoint = new ScopePoint(this, in key, valuesStoreOffset, objectsStoreOffset);
			if (!nestedScopes.TryAdd(key, scopePoint))
			{
				throw new InvalidOperationException("Nested scope for given runtime already exists! This indicates a potential race condition.");
			}
			return scopePoint;
		}

		internal void MapGlobals(IGlobalValue[] globals)
		{
			_mappedGlobals = globals;
		}

		public T ReadGlobal<T>(GlobalRef<T> globalRef)
		{
			if (globalRef.Global == null)
			{
				return default(T);
			}
			return ReadGlobal<T>(globalRef.Global.Index);
		}

		public T ReadGlobal<T>(int index)
		{
			if (_mappedGlobals == null)
			{
				return default(T);
			}
			IGlobalValue globalValue = _mappedGlobals[index];
			if (globalValue == null)
			{
				return default(T);
			}
			return ((IGlobalValue<T>)globalValue).Value;
		}

		public bool WriteGlobal<T>(GlobalRef<T> globalRef, T value)
		{
			if (globalRef.Global == null)
			{
				return false;
			}
			return WriteGlobal(globalRef.Global.Index, value);
		}

		public bool WriteGlobal<T>(int index, T value)
		{
			if (_mappedGlobals == null)
			{
				return false;
			}
			IGlobalValue globalValue = _mappedGlobals[index];
			if (globalValue == null)
			{
				return false;
			}
			return ((IGlobalValue<T>)globalValue).SetValue(value);
		}

		public bool HasAnyStoreDataInHierarchy()
		{
			if (!IsRoot && (Runtime.ValueStoreSize > 0 || Runtime.ObjectStoreSize > 0))
			{
				return true;
			}
			if (nestedScopes != null)
			{
				foreach (KeyValuePair<ScopeKey, ScopePoint> nestedScope in nestedScopes)
				{
					if (nestedScope.Value.HasAnyStoreDataInHierarchy())
					{
						return true;
					}
				}
			}
			return false;
		}

		public void ComputeTotalStoreSizes(out int valueStoreSize, out int objectStoreSize)
		{
			if (!IsRoot)
			{
				valueStoreSize = Runtime.ValueStoreSize;
				objectStoreSize = Runtime.ObjectStoreSize;
			}
			else
			{
				valueStoreSize = 0;
				objectStoreSize = 0;
			}
			if (nestedScopes == null)
			{
				return;
			}
			foreach (KeyValuePair<ScopeKey, ScopePoint> nestedScope in nestedScopes)
			{
				nestedScope.Value.ComputeTotalStoreSizes(out var _, out var objectStoreSize2);
				valueStoreSize += valueStoreSize;
				objectStoreSize += objectStoreSize2;
			}
		}

		internal void CaptureExplicitNodeMap()
		{
			if (!IsRoot)
			{
				explicitNodeMap = new Dictionary<IExecutionNode, NodeStoreOffsets>();
				foreach (IExecutionNode node in Runtime.Nodes)
				{
					if (node.FixedStoresCount > 0 && (node.ValueStoreStartOffset >= 0 || node.ObjectStoreStartOffset >= 0))
					{
						explicitNodeMap.Add(node, new NodeStoreOffsets(node.ValueStoreStartOffset, node.ObjectStoreStartOffset));
					}
				}
			}
			if (nestedScopes == null)
			{
				return;
			}
			foreach (KeyValuePair<ScopeKey, ScopePoint> nestedScope in nestedScopes)
			{
				nestedScope.Value.CaptureExplicitNodeMap();
			}
		}

		internal NodeStoreOffsets? GetStoredOffset(IExecutionNode node)
		{
			if (explicitNodeMap.TryGetValue(node, out var value))
			{
				return value;
			}
			return null;
		}
	}
	public class SharedExecutionScope
	{
		private object _lock = new object();

		private int _allocatedValueStoreSize;

		private int _allocatedObjectStoreSize;

		public ScopePoint RootScope { get; private set; }

		public byte[] ValuesStore { get; private set; }

		public object[] ObjectsStore { get; private set; }

		public SharedExecutionScope()
		{
			RootScope = new ScopePoint();
			ValuesStore = new byte[16384];
			ObjectsStore = new object[4096];
		}

		public ScopePoint GetNestedScopeOrAllocate(ScopePoint sourcePoint, IExecutionRuntime runtime, IExecutionNestedNode node)
		{
			ScopeKey key = new ScopeKey(runtime, node);
			ScopePoint nestedScope = sourcePoint.GetNestedScope(in key);
			if (nestedScope != null)
			{
				return nestedScope;
			}
			lock (_lock)
			{
				nestedScope = sourcePoint.GetNestedScope(in key);
				if (nestedScope != null)
				{
					return nestedScope;
				}
				nestedScope = sourcePoint.AllocateScope(key, _allocatedValueStoreSize, _allocatedObjectStoreSize);
				_allocatedValueStoreSize += runtime.ValueStoreSize;
				_allocatedObjectStoreSize += runtime.ObjectStoreSize;
				if (_allocatedValueStoreSize > ValuesStore.Length)
				{
					throw new OverflowException($"ValueStore overflow. ValueStore.Length: {ValuesStore.Length}. AllocatedSize: {_allocatedValueStoreSize}. RuntimeSize: {runtime.ValueStoreSize}");
				}
				if (_allocatedObjectStoreSize > ObjectsStore.Length)
				{
					throw new OverflowException($"ObjectStore overflow. ObjectStore.Length: {ObjectsStore.Length}. AllocatedSize: {_allocatedObjectStoreSize}. RuntimeSize: {runtime.ObjectStoreSize}");
				}
				return nestedScope;
			}
		}

		public bool HasAnyStoreDataInHierarchy()
		{
			return RootScope.HasAnyStoreDataInHierarchy();
		}

		public void ComputeTotalStoreSizes(out int valueStoreSize, out int objectStoreSize)
		{
			RootScope.ComputeTotalStoreSizes(out valueStoreSize, out objectStoreSize);
		}

		public ScopePoint CaptureScopeAndSwap(ref byte[] valuesStore, ref object[] objectsStore)
		{
			if (valuesStore == null)
			{
				throw new ArgumentNullException("valuesStore");
			}
			if (objectsStore == null)
			{
				throw new ArgumentNullException("objectsStore");
			}
			if (valuesStore.Length != ValuesStore.Length)
			{
				throw new ArgumentException("ValuesStore array must be the same length as existing one");
			}
			if (objectsStore.Length != ObjectsStore.Length)
			{
				throw new ArgumentException("ValuesStore array must be the same length as existing one");
			}
			ScopePoint rootScope = RootScope;
			rootScope.CaptureExplicitNodeMap();
			byte[] valuesStore2 = ValuesStore;
			object[] objectsStore2 = ObjectsStore;
			ValuesStore = valuesStore;
			ObjectsStore = objectsStore;
			valuesStore = valuesStore2;
			objectsStore = objectsStore2;
			RootScope = new ScopePoint();
			_allocatedValueStoreSize = 0;
			_allocatedObjectStoreSize = 0;
			return rootScope;
		}

		public void Clear()
		{
			RootScope = new ScopePoint();
			Array.Clear(ValuesStore, 0, _allocatedValueStoreSize);
			Array.Clear(ObjectsStore, 0, _allocatedObjectStoreSize);
			_allocatedValueStoreSize = 0;
			_allocatedObjectStoreSize = 0;
		}
	}
	public class SimpleGlobalValue<T> : IGlobalValue<T>, IGlobalValue
	{
		public T Value { get; private set; }

		public object BoxedValue => Value;

		public bool IsWriteable => true;

		public Type ValueType => typeof(T);

		public bool SetValue(T newValue)
		{
			Value = newValue;
			return true;
		}
	}
	public class ValueStack
	{
		private byte[] stack = new byte[10240];

		public int Top { get; private set; }

		public int Bottom { get; internal set; }

		public int Size => stack.Length;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int GetBottomPosition(int bottom, int offset)
		{
			return Size - bottom + offset;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe void Push<T>(T value) where T : unmanaged
		{
			int num = Top + sizeof(T);
			if (num > Size - Bottom)
			{
				throw new Exception($"Stack doesn't have enough capacity to hold value of type {typeof(T)}. Bottom: {Bottom}, Top: {Top}, Size: {Size}");
			}
			Unsafe.WriteUnaligned(ref stack[Top], value);
			Top = num;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int GetPosition<T>(int offset) where T : unmanaged
		{
			if (offset < 0)
			{
				return Top + offset;
			}
			return GetBottomPosition(Bottom, offset);
		}

		public unsafe T Pop<T>() where T : unmanaged
		{
			Pop(sizeof(T));
			return Unsafe.ReadUnaligned<T>(in stack[Top]);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T Read<T>(int offset) where T : unmanaged
		{
			return Unsafe.ReadUnaligned<T>(in stack[GetPosition<T>(offset)]);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Write<T>(int offset, T value) where T : unmanaged
		{
			Unsafe.WriteUnaligned(ref stack[GetPosition<T>(offset)], value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ref T Access<T>(int offset) where T : unmanaged
		{
			return ref Unsafe.As<byte, T>(ref stack[GetPosition<T>(offset)]);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Pop(int size)
		{
			Top -= size;
			if (Top < 0)
			{
				Top += size;
				throw new InvalidOperationException("Stack underflow");
			}
		}

		public void Store(int size, int offset)
		{
			if (offset < 0)
			{
				throw new ArgumentOutOfRangeException("offset");
			}
			Pop(size);
			int num = Size - Bottom + offset;
			if (num < Top)
			{
				throw new ArgumentOutOfRangeException("offset");
			}
			Array.Copy(stack, Top, stack, num, size);
		}

		public void Load(int size, int offset)
		{
			if (offset < 0)
			{
				throw new ArgumentOutOfRangeException("offset");
			}
			int num = Top + size;
			int num2 = Size - Bottom + offset;
			if (num > Size - Bottom)
			{
				throw new Exception("Stack overflow");
			}
			if (num2 < num)
			{
				throw new ArgumentOutOfRangeException("offset");
			}
			Array.Copy(stack, num2, stack, Top, size);
			Top = num;
		}

		public void Allocate(int size)
		{
			Bottom += size;
			if (Bottom > Size - Top)
			{
				Bottom -= size;
				throw new InvalidOperationException("Stack overflow");
			}
		}

		public void Clear(int position, int size)
		{
			Array.Clear(stack, Size - Bottom + position, size);
		}

		public void DeAllocate(int size)
		{
			Bottom -= size;
			if (Bottom < 0)
			{
				Bottom += size;
				throw new InvalidOperationException("Stack underflow");
			}
		}

		public void CopyBottomTo(int sourceBottom, int targetBottom, int size, ValueStack target)
		{
			int bottomPosition = GetBottomPosition(sourceBottom, 0);
			int bottomPosition2 = target.GetBottomPosition(targetBottom, 0);
			Buffer.BlockCopy(stack, bottomPosition, target.stack, bottomPosition2, size);
		}

		public Span<byte> GetRawStack()
		{
			return stack;
		}
	}
	public interface IVariable<C, T> : INode where C : ExecutionContext
	{
		T Read(C context);

		bool Write(T value, C context);
	}
	public struct ObjectLocal<T>
	{
		internal int offset;

		public T Read(ExecutionContext context)
		{
			return context.Objects.Read<T>(offset);
		}

		public void Write(T value, ExecutionContext context)
		{
			context.Objects.Write(offset, value);
		}
	}
	public struct ValueLocal<T> where T : unmanaged
	{
		internal int offset;

		public T Read(ExecutionContext context)
		{
			return context.Values.Read<T>(offset);
		}

		public void Write(T value, ExecutionContext context)
		{
			context.Values.Write(offset, value);
		}

		public ref T Access(ExecutionContext context)
		{
			return ref context.Values.Access<T>(offset);
		}
	}
	public class ExecutionChangesDispatcher<C> where C : ExecutionContext
	{
		private NodeGroup group;

		private SortedSet<ElementPath<INode>> changedNodes = new SortedSet<ElementPath<INode>>();

		private SortedSet<ElementPath<INode>> backBuffer = new SortedSet<ElementPath<INode>>();

		public int CurrentChangedCount => changedNodes.Count;

		public bool IsEmpty => CurrentChangedCount == 0;

		public IEnumerable<ElementPath<INode>> CurrentChangedNodes => changedNodes;

		public event Action FirstChangeRegistered;

		public ExecutionChangesDispatcher(NodeGroup group)
		{
			this.group = group;
		}

		public void AllTrackedChanged()
		{
			if (IsEmpty)
			{
				this.FirstChangeRegistered?.Invoke();
			}
			group.AllChanged(changedNodes);
		}

		public void MarkChanged(INode node)
		{
			if (IsEmpty)
			{
				this.FirstChangeRegistered?.Invoke();
			}
			changedNodes.Add(new ElementPath<INode>(node));
		}

		public void MarkChanged(INode node, NodeContextPath path)
		{
			if (IsEmpty)
			{
				this.FirstChangeRegistered?.Invoke();
			}
			changedNodes.Add(new ElementPath<INode>(node, path));
		}

		public void OutputChanged(ElementPath<IOutput> node)
		{
			bool isEmpty = IsEmpty;
			group.OutputChanged(node, changedNodes);
			if (isEmpty && !IsEmpty)
			{
				this.FirstChangeRegistered?.Invoke();
			}
		}

		public void ClearChanges()
		{
			changedNodes.Clear();
		}

		public int DispatchChangeToAllListeners(ExecutionRuntime<C> runtime, C context)
		{
			if (!context.TryEnterRuntimeOnce(runtime))
			{
				throw new InvalidOperationException("Target runtime has already been entered");
			}
			SortedSet<ElementPath<INode>> sortedSet = changedNodes;
			SortedSet<ElementPath<INode>> sortedSet2 = backBuffer;
			backBuffer = sortedSet;
			changedNodes = sortedSet2;
			runtime.BeginStackFrame(context);
			context.PinFrame();
			int result = DispatchChangeToAllRecursively(runtime, context);
			context.UnpinFrame();
			runtime.EndStackFrame(context);
			context.ExitRuntimeOnce(runtime);
			backBuffer.Clear();
			return result;
		}

		private int DispatchChangeToAllRecursively(ExecutionRuntime<C> runtime, C context)
		{
			int num = 0;
			foreach (IExecutionNode<C> node in runtime.Nodes)
			{
				if (node is IExecutionChangeListener<C> executionChangeListener)
				{
					executionChangeListener.Changed(context);
					num++;
				}
			}
			foreach (NestedNode<C> nestedNode in runtime.GetNestedNodes(cache: true))
			{
				if (context.TryEnterRuntimeOnce(nestedNode.Target))
				{
					nestedNode.EnterTargetFrame(context);
					context.PinFrame();
					num += DispatchChangeToAllRecursively(nestedNode.Target, context);
					context.UnpinFrame();
					nestedNode.ExitTargetFrame(context);
					context.ExitRuntimeOnce(nestedNode.Target);
				}
			}
			return num;
		}

		public int DispatchChanges(ExecutionRuntime<C> runtime, C context)
		{
			SortedSet<ElementPath<INode>> sortedSet = changedNodes;
			SortedSet<ElementPath<INode>> sortedSet2 = backBuffer;
			backBuffer = sortedSet;
			changedNodes = sortedSet2;
			runtime.BeginStackFrame(context);
			context.PinFrame();
			NodeContextPath previousPath = default(NodeContextPath);
			foreach (ElementPath<INode> continuousChange in group.ContinuousChanges)
			{
				if (continuousChange.element is IExecutionChangeListener<C> executionChangeListener)
				{
					context.MoveToContext(previousPath, continuousChange.path);
					previousPath = continuousChange.path;
					executionChangeListener.Changed(context);
				}
			}
			foreach (ElementPath<INode> item in backBuffer)
			{
				if (item.element is IExecutionChangeListener<C> executionChangeListener2)
				{
					context.MoveToContext(previousPath, item.path);
					previousPath = item.path;
					executionChangeListener2.Changed(context);
				}
			}
			int count = backBuffer.Count;
			backBuffer.Clear();
			context.MoveToContext(previousPath, default(NodeContextPath));
			context.UnpinFrame();
			runtime.EndStackFrame(context);
			return count;
		}
	}
	public delegate void NodeEventHandler<in C>(C context, object eventData) where C : ExecutionContext;
	public class ExecutionEventDispatcher<C> where C : ExecutionContext
	{
		private readonly struct Event : IEquatable<Event>, IComparable<Event>
		{
			public readonly NodeContextPath path;

			public readonly NodeEventHandler<C> handler;

			public readonly object eventData;

			public Event(NodeContextPath path, NodeEventHandler<C> handler, object eventData)
			{
				this.path = path;
				this.handler = handler;
				this.eventData = eventData;
			}

			public int CompareTo(Event other)
			{
				return path.CompareTo(other.path);
			}

			public bool Equals(Event other)
			{
				if (handler == other.handler && path.Equals(other.path))
				{
					return eventData == other.eventData;
				}
				return false;
			}
		}

		private List<Event> orderedEvents = new List<Event>();

		private List<Event> unorderedEvents = new List<Event>();

		public int ScheduledEventCount => ScheduledOrderedEventCount + ScheduledUnorderedEventCount;

		public int ScheduledOrderedEventCount => orderedEvents.Count;

		public int ScheduledUnorderedEventCount => unorderedEvents.Count;

		public bool IsEmpty => ScheduledEventCount == 0;

		public event Action FirstEventRegistered;

		public void ScheduleEvent(NodeContextPath path, NodeEventHandler<C> handler, object eventData, bool ordered = true)
		{
			if (handler == null)
			{
				throw new ArgumentNullException("handler");
			}
			if (IsEmpty)
			{
				this.FirstEventRegistered?.Invoke();
			}
			(ordered ? orderedEvents : unorderedEvents).Add(new Event(path, handler, eventData));
		}

		public void ScheduleEvent(NodeContextPath path, Action<C> handler, bool ordered = true)
		{
			ScheduleEvent(path, delegate(C context, object eventData)
			{
				handler(context);
			}, null, ordered);
		}

		public void DispatchEvents(ExecutionRuntime<C> runtime, C context)
		{
			runtime.BeginStackFrame(context);
			context.PinFrame();
			NodeContextPath previousPath = default(NodeContextPath);
			int count = orderedEvents.Count;
			int count2 = unorderedEvents.Count;
			for (int i = 0; i < count; i++)
			{
				Event obj = orderedEvents[i];
				context.MoveToContext(previousPath, obj.path);
				previousPath = obj.path;
				obj.handler(context, obj.eventData);
			}
			unorderedEvents.Sort(0, count2, Comparer<Event>.Default);
			for (int j = 0; j < count2; j++)
			{
				Event obj2 = unorderedEvents[j];
				context.MoveToContext(previousPath, obj2.path);
				previousPath = obj2.path;
				obj2.handler(context, obj2.eventData);
			}
			context.MoveToContext(previousPath, default(NodeContextPath));
			context.UnpinFrame();
			runtime.EndStackFrame(context);
			orderedEvents.RemoveRange(0, count);
			unorderedEvents.RemoveRange(0, count2);
			if (!IsEmpty)
			{
				this.FirstEventRegistered?.Invoke();
			}
		}

		public void ClearEvents()
		{
			orderedEvents.Clear();
			unorderedEvents.Clear();
		}
	}
	public class ExecutionUpdateDispatcher<C> where C : ExecutionContext
	{
		private readonly struct UpdateNode : IComparable<UpdateNode>, IEquatable<UpdateNode>
		{
			public readonly NodeContextPath path;

			public readonly IExecutionUpdateReceiver<C> receiver;

			public UpdateNode(NodeContextPath path, IExecutionUpdateReceiver<C> receiver)
			{
				this.path = path;
				this.receiver = receiver;
			}

			public bool Equals(UpdateNode other)
			{
				if (receiver == other.receiver)
				{
					return path.Equals(other.path);
				}
				return false;
			}

			public int CompareTo(UpdateNode other)
			{
				int num = path.CompareTo(other.path);
				if (num != 0)
				{
					return num;
				}
				return receiver.AllocationIndex.CompareTo(other.receiver.AllocationIndex);
			}

			public override int GetHashCode()
			{
				return HashCode.Combine(path, receiver);
			}
		}

		private SortedDictionary<int, SortedSet<UpdateNode>> updateBuckets = new SortedDictionary<int, SortedSet<UpdateNode>>();

		public bool IsEmpty => updateBuckets.Count == 0;

		public int UpdateBucketCount => updateBuckets.Count;

		public int TotalUpdateNodes => updateBuckets.Sum((KeyValuePair<int, SortedSet<UpdateNode>> b) => b.Value.Count);

		public event Action FirstNodeRegistered;

		public event Action BecameEmpty;

		public void RegisterNode(NodeContextPath path, IExecutionUpdateReceiver<C> node, int bucket)
		{
			bool isEmpty = IsEmpty;
			if (!updateBuckets.TryGetValue(bucket, out var value))
			{
				value = new SortedSet<UpdateNode>();
				updateBuckets.Add(bucket, value);
			}
			if (!value.Add(new UpdateNode(path, node)))
			{
				throw new InvalidOperationException($"Node has already been registered for updates in bucket {bucket} at path {path}: {node}.");
			}
			if (isEmpty)
			{
				this.FirstNodeRegistered?.Invoke();
			}
		}

		public void UnregisterNode(NodeContextPath path, IExecutionUpdateReceiver<C> node, int bucket)
		{
			if (!updateBuckets.TryGetValue(bucket, out var value))
			{
				throw new InvalidOperationException($"There is no {bucket} bucket");
			}
			if (!value.Remove(new UpdateNode(path, node)))
			{
				throw new InvalidOperationException("Given node at given context path does not exist in the bucket");
			}
			if (value.Count == 0)
			{
				updateBuckets.Remove(bucket);
				if (IsEmpty)
				{
					this.BecameEmpty?.Invoke();
				}
			}
		}

		public int UpdateAllBuckets(ExecutionRuntime<C> runtime, C context)
		{
			runtime.BeginStackFrame(context);
			context.PinFrame();
			NodeContextPath current = default(NodeContextPath);
			int num = 0;
			foreach (KeyValuePair<int, SortedSet<UpdateNode>> updateBucket in updateBuckets)
			{
				num += UpdateBucket(updateBucket.Value, ref current, context);
			}
			context.MoveToContext(current, default(NodeContextPath));
			context.UnpinFrame();
			runtime.EndStackFrame(context);
			return num;
		}

		public void UpdateBucket(int bucket, ExecutionRuntime<C> runtime, C context)
		{
			if (!updateBuckets.TryGetValue(bucket, out var value))
			{
				throw new InvalidOperationException($"Bucket {bucket} doesn't exist");
			}
			runtime.BeginStackFrame(context);
			context.PinFrame();
			NodeContextPath current = default(NodeContextPath);
			UpdateBucket(value, ref current, context);
			context.MoveToContext(current, default(NodeContextPath));
			context.UnpinFrame();
			runtime.EndStackFrame(context);
		}

		private int UpdateBucket(SortedSet<UpdateNode> list, ref NodeContextPath current, C context)
		{
			foreach (UpdateNode item in list)
			{
				context.MoveToContext(current, item.path);
				current = item.path;
				item.receiver.Update(context);
			}
			return list.Count;
		}
	}
	public interface IExecutionChangeListener<C> where C : ExecutionContext
	{
		void Changed(C context);
	}
	public interface IExecutionUpdateReceiver<C> : INode where C : ExecutionContext
	{
		void Update(C context);
	}
	public interface IScopeEventListener<in C>
	{
		void AddedToScope(C context);

		void RemovedFromScope(C context);
	}
	public class ScopeAddRemoveDispatcher<C> where C : ExecutionContext
	{
		private class ScopeNode
		{
			public readonly IScopeEventListener<C> Node;

			public bool NewlyAdded;

			public bool Exists;

			public ScopeNode(IScopeEventListener<C> node)
			{
				Node = node;
			}
		}

		private Dictionary<NodeContextPath, Dictionary<IScopeEventListener<C>, ScopeNode>> registeredNodes = new Dictionary<NodeContextPath, Dictionary<IScopeEventListener<C>, ScopeNode>>();

		private bool _hasNewNodes;

		public void UpdateScopesAndSendRemoved(ExecutionRuntime<C> runtime, C context)
		{
			UpdateScopesAndSendRemoved(runtime, context, scanForNewNodes: true);
		}

		private void UpdateScopesAndSendRemoved(ExecutionRuntime<C> runtime, C context, bool scanForNewNodes)
		{
			if (!context.TryEnterRuntimeOnce(runtime))
			{
				throw new InvalidOperationException("Target runtime can be entered only once, but it has already been entered in this context");
			}
			runtime.BeginStackFrame(context);
			context.PinFrame();
			foreach (KeyValuePair<NodeContextPath, Dictionary<IScopeEventListener<C>, ScopeNode>> registeredNode in registeredNodes)
			{
				foreach (KeyValuePair<IScopeEventListener<C>, ScopeNode> item in registeredNode.Value)
				{
					item.Value.Exists = false;
				}
			}
			if (scanForNewNodes)
			{
				ProcessScope(runtime, context);
			}
			NodeContextPath previousPath = default(NodeContextPath);
			List<NodeContextPath> list = null;
			foreach (KeyValuePair<NodeContextPath, Dictionary<IScopeEventListener<C>, ScopeNode>> registeredNode2 in registeredNodes)
			{
				List<IScopeEventListener<C>> list2 = null;
				foreach (KeyValuePair<IScopeEventListener<C>, ScopeNode> item2 in registeredNode2.Value)
				{
					if (!item2.Value.Exists)
					{
						context.MoveToContext(previousPath, registeredNode2.Key);
						previousPath = registeredNode2.Key;
						if (!item2.Value.NewlyAdded)
						{
							item2.Value.Node.RemovedFromScope(context);
						}
						if (list2 == null)
						{
							list2 = new List<IScopeEventListener<C>>();
						}
						list2.Add(item2.Key);
					}
				}
				if (list2 == null)
				{
					continue;
				}
				foreach (IScopeEventListener<C> item3 in list2)
				{
					registeredNode2.Value.Remove(item3);
				}
				if (registeredNode2.Value.Count == 0)
				{
					if (list == null)
					{
						list = new List<NodeContextPath>();
					}
					list.Add(registeredNode2.Key);
				}
			}
			if (list != null)
			{
				foreach (NodeContextPath item4 in list)
				{
					registeredNodes.Remove(item4);
				}
			}
			context.MoveToContext(previousPath, default(NodeContextPath));
			context.UnpinFrame();
			runtime.EndStackFrame(context);
			context.ExitRuntimeOnce(runtime);
		}

		public void SendAllRemovedEvents(ExecutionRuntime<C> runtime, C context)
		{
			UpdateScopesAndSendRemoved(runtime, context, scanForNewNodes: false);
		}

		public void SendAddedEvents(ExecutionRuntime<C> runtime, C context)
		{
			if (!_hasNewNodes)
			{
				return;
			}
			if (!context.TryEnterRuntimeOnce(runtime))
			{
				throw new InvalidOperationException("Target runtime can be entered only once, but it has already been entered in this context");
			}
			runtime.BeginStackFrame(context);
			context.PinFrame();
			NodeContextPath previousPath = default(NodeContextPath);
			foreach (KeyValuePair<NodeContextPath, Dictionary<IScopeEventListener<C>, ScopeNode>> registeredNode in registeredNodes)
			{
				foreach (KeyValuePair<IScopeEventListener<C>, ScopeNode> item in registeredNode.Value)
				{
					if (item.Value.NewlyAdded)
					{
						context.MoveToContext(previousPath, registeredNode.Key);
						previousPath = registeredNode.Key;
						item.Value.Node.AddedToScope(context);
						item.Value.NewlyAdded = false;
					}
				}
			}
			_hasNewNodes = false;
			context.MoveToContext(previousPath, default(NodeContextPath));
			context.UnpinFrame();
			runtime.EndStackFrame(context);
			context.ExitRuntimeOnce(runtime);
		}

		private void ProcessScope(ExecutionRuntime<C> runtime, C context)
		{
			NodeContextPath path = context.CaptureContextPath();
			Dictionary<IScopeEventListener<C>, ScopeNode> dictionary = null;
			foreach (IExecutionNode<C> node in runtime.Nodes)
			{
				if (!(node is IScopeEventListener<C> scopeEventListener))
				{
					if (node is NestedNode<C> nestedNode && context.TryEnterRuntimeOnce(nestedNode.Target))
					{
						nestedNode.EnterTargetFrame(context);
						ProcessScope(nestedNode.Target, context);
						nestedNode.ExitTargetFrame(context);
						context.ExitRuntimeOnce(nestedNode.Target);
					}
					continue;
				}
				if (dictionary == null)
				{
					dictionary = EnsureScopeDictionary(path);
				}
				if (dictionary.TryGetValue(scopeEventListener, out var value))
				{
					value.Exists = true;
					continue;
				}
				value = new ScopeNode(scopeEventListener);
				value.NewlyAdded = true;
				value.Exists = true;
				_hasNewNodes = true;
				dictionary.Add(scopeEventListener, value);
			}
		}

		private Dictionary<IScopeEventListener<C>, ScopeNode> EnsureScopeDictionary(NodeContextPath path)
		{
			if (registeredNodes.TryGetValue(path, out var value))
			{
				return value;
			}
			value = new Dictionary<IScopeEventListener<C>, ScopeNode>();
			registeredNodes.Add(path, value);
			return value;
		}
	}
	public class ExecutionAbortedException : Exception
	{
		public IExecutionRuntime Runtime { get; private set; }

		public IOperation InitialOperation { get; private set; }

		public IOperation NextOperation { get; private set; }

		public bool IsAsyncContext { get; private set; }

		public string DebugDetails { get; private set; }

		public ExecutionAbortedException(IExecutionRuntime runtime, IOperation initialOperation, IOperation nextOperation, bool isAsync, string debugDetails = null)
			: base($"Execution aborted! IsAsync: {isAsync}\nRuntime: {runtime}\nInitialOperation: {initialOperation}\nNextOperation: {nextOperation}\nDebugDetails: {debugDetails}")
		{
			Runtime = runtime;
			InitialOperation = initialOperation;
			NextOperation = nextOperation;
			IsAsyncContext = isAsync;
			DebugDetails = debugDetails;
		}
	}
	internal class EvaluateSequence<C> : ExecutionControlNode<C> where C : ExecutionContext
	{
		public EvaluationSequence<C> Sequence;

		public override void Evaluate(C context)
		{
			Sequence.Evaluate(context);
		}

		public EvaluateSequence(EvaluationSequence<C> sequence)
		{
			Sequence = sequence;
		}
	}
	internal abstract class ExecutionControlNode<C> : IExecutionNode<C>, IExecutionNode, INode where C : ExecutionContext
	{
		public int FixedValueStackSize
		{
			get
			{
				throw new NotSupportedException();
			}
		}

		public int FixedObjectStackSize
		{
			get
			{
				throw new NotSupportedException();
			}
		}

		public int InputCount
		{
			get
			{
				throw new NotSupportedException();
			}
		}

		public int OutputCount
		{
			get
			{
				throw new NotSupportedException();
			}
		}

		public int FixedInputCount
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public int FixedOutputCount
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public int DynamicInputCount
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public int DynamicOutputCount
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public int FixedImpulseCount
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public int FixedOperationCount
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public int ImpulseCount
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public int OperationCount
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public int DynamicImpulseCount
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public int DynamicOperationCount
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public string Overload
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public bool IsPassthrough
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public NodeMetadata Metadata
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public bool CanBeEvaluated
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public int FixedLocalsCount
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public int FixedValueLocalsSize
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public int FixedObjectLocalsSize
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public int FixedReferenceCount
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public NodeRuntime Runtime
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public bool ListensToChanges
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public int FixedStoresCount
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public int FixedValueStoresSize
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public int FixedObjectStoresSize
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public bool HasSingleContinuation
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public bool HasSyncAsyncTransition
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public int IndexInGroup
		{
			get
			{
				throw new NotImplementedException();
			}
			set
			{
				throw new NotImplementedException();
			}
		}

		public int FixedGlobalRefCount
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public int ArgumentCount
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public int AllocationIndex
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public int ValueStoreStartOffset
		{
			get
			{
				throw new NotImplementedException();
			}
			set
			{
				throw new NotImplementedException();
			}
		}

		public int ObjectStoreStartOffset
		{
			get
			{
				throw new NotImplementedException();
			}
			set
			{
				throw new NotImplementedException();
			}
		}

		public abstract void Evaluate(C context);

		public IOperation GetOperation(int index)
		{
			throw new NotImplementedException();
		}

		public ExecutionOperationHandler<T> GetOperationHandler<T>(int index) where T : ExecutionContext
		{
			throw new NotImplementedException();
		}

		public short[] GetDefaultStackLayout()
		{
			throw new NotSupportedException();
		}

		public ExecutionOperationHandler<T> GetDynamicOperationHandler<T>(int listIndex, int index) where T : ExecutionContext
		{
			throw new NotImplementedException();
		}

		public IInputList GetInputList(int index)
		{
			throw new NotImplementedException();
		}

		public IOperation GetImpulseTarget(int index)
		{
			throw new NotImplementedException();
		}

		public object GetInputDefaultValue(int index)
		{
			throw new NotImplementedException();
		}

		public string GetInputName(int index)
		{
			throw new NotSupportedException();
		}

		public IOutput GetInputSource(int index)
		{
			throw new NotSupportedException();
		}

		public int GetInputStackOffset(int index)
		{
			throw new NotSupportedException();
		}

		public Type GetInputType(int index)
		{
			throw new NotSupportedException();
		}

		public DataClass GetInputTypeClass(int index)
		{
			throw new NotSupportedException();
		}

		public IOutput GetOutput(int index)
		{
			throw new NotSupportedException();
		}

		public string GetOutputName(int index)
		{
			throw new NotImplementedException();
		}

		public Type GetOutputType(int index)
		{
			throw new NotSupportedException();
		}

		public DataClass GetOutputTypeClass(int index)
		{
			throw new NotSupportedException();
		}

		public int GetValueInputSize(int index)
		{
			throw new NotSupportedException();
		}

		public int GetValueOutputSize(int index)
		{
			throw new NotSupportedException();
		}

		public bool IsImpulseImplicit(int index)
		{
			throw new NotImplementedException();
		}

		public bool IsInputConditional(int index)
		{
			throw new NotImplementedException();
		}

		public bool IsOutputImplicit(int index)
		{
			throw new NotImplementedException();
		}

		public void SetInputSource(int index, IOutput source)
		{
			throw new NotImplementedException();
		}

		public IOutputList GetOutputList(int index)
		{
			throw new NotImplementedException();
		}

		public void SetImpulseTarget(int index, IOperation target)
		{
			throw new NotImplementedException();
		}

		public IImpulseList GetImpulseList(int index)
		{
			throw new NotImplementedException();
		}

		public SyncOperationList GetOperationList(int index)
		{
			throw new NotImplementedException();
		}

		public string GetInputListName(int index)
		{
			throw new NotImplementedException();
		}

		public Type GetInputListTypeConstraint(int index)
		{
			throw new NotImplementedException();
		}

		public string GetOutputListName(int index)
		{
			throw new NotImplementedException();
		}

		public string GetOperationListName(int index)
		{
			throw new NotImplementedException();
		}

		public void CopyDynamicOutputLayout(INode source)
		{
			throw new NotImplementedException();
		}

		public void CopyDynamicOperationLayout(INode source)
		{
			throw new NotImplementedException();
		}

		public DataClass GetLocalDataClass(int index)
		{
			throw new NotImplementedException();
		}

		public Type GetLocalType(int index)
		{
			throw new NotImplementedException();
		}

		public int GetValueLocalSize(int index)
		{
			throw new NotImplementedException();
		}

		public void SetLocalOffset(int index, int offset)
		{
			throw new NotImplementedException();
		}

		public string GetImpulseListName(int index)
		{
			throw new NotImplementedException();
		}

		public string GetReferenceName(int index)
		{
			throw new NotImplementedException();
		}

		public Type GetReferenceType(int index)
		{
			throw new NotImplementedException();
		}

		public INode GetReferenceTarget(int index)
		{
			throw new NotImplementedException();
		}

		public void SetReferenceTarget(int index, INode target)
		{
			throw new NotImplementedException();
		}

		public bool TrySetReferenceTarget(int index, INode target)
		{
			throw new NotImplementedException();
		}

		public void SetInputSource(ElementRef input, IOutput source)
		{
			throw new NotImplementedException();
		}

		public void Initialize(NodeRuntime runtime, int allocationIndex)
		{
			throw new NotImplementedException();
		}

		public bool IsInputListeningToChanges(int index)
		{
			throw new NotImplementedException();
		}

		public CrossRuntimeInputAttribute GetInputCrossRuntime(int index)
		{
			throw new NotImplementedException();
		}

		public OutputChangeSource GetOutputChangeType(int index)
		{
			throw new NotImplementedException();
		}

		public DataClass GetStoreDataClass(int index)
		{
			throw new NotImplementedException();
		}

		public Type GetStoreType(int index)
		{
			throw new NotImplementedException();
		}

		public int GetValueStoreSize(int index)
		{
			throw new NotImplementedException();
		}

		public void SetStoreOffset(int index, int offset)
		{
			throw new NotImplementedException();
		}

		public string GetImpulseName(int index)
		{
			throw new NotImplementedException();
		}

		public ImpulseType GetImpulseType(int index)
		{
			throw new NotImplementedException();
		}

		public bool IsOperationAsync(int index)
		{
			throw new NotImplementedException();
		}

		public bool IsOperationListAsync(int index)
		{
			throw new NotImplementedException();
		}

		public AsyncExecutionOperationHandler<T> GetAsyncOperationHandler<T>(int index) where T : ExecutionContext
		{
			throw new NotImplementedException();
		}

		public AsyncExecutionOperationHandler<T> GetDynamicAsyncOperationHandler<T>(int listIndex, int index) where T : ExecutionContext
		{
			throw new NotImplementedException();
		}

		IOperationList INode.GetOperationList(int index)
		{
			throw new NotImplementedException();
		}

		public string GetGlobalRefName(int index)
		{
			throw new NotImplementedException();
		}

		public Type GetGlobalRefValueType(int index)
		{
			throw new NotImplementedException();
		}

		public Global GetGlobalRefBinding(int index)
		{
			throw new NotImplementedException();
		}

		public void SetGlobalRefBinding(int index, Global binding)
		{
			throw new NotImplementedException();
		}

		public bool TrySetGlobalRefBinding(int index, Global binding)
		{
			throw new NotImplementedException();
		}

		public void Dispose()
		{
			throw new NotImplementedException();
		}

		public void GlobalChanged<T>(int index, T newValue, C context)
		{
			throw new NotImplementedException();
		}

		public void ListGlobalChanged<T>(int listIndex, int index, T newValue, C context)
		{
			throw new NotImplementedException();
		}

		public bool CanOperationContinueTo(int index, string impulseName)
		{
			throw new NotImplementedException();
		}

		public bool CanOperationListContinueTo(int index, string impulseName)
		{
			throw new NotImplementedException();
		}

		public bool OperationHasSingleContinuation(int index)
		{
			throw new NotImplementedException();
		}

		public bool OperationHasSyncAsyncTransition(int index)
		{
			throw new NotImplementedException();
		}

		public string GetOperationName(int index)
		{
			throw new NotImplementedException();
		}

		public bool IsOperationPassthrough(int index)
		{
			throw new NotImplementedException();
		}
	}
	internal class LoadValueFromLocal<C> : ExecutionControlNode<C> where C : ExecutionContext
	{
		public int Size;

		public int Offset;

		public override void Evaluate(C context)
		{
			context.Values.Load(Size, Offset);
		}

		public LoadValueFromLocal(int size, int offset)
		{
			Size = size;
			Offset = offset;
		}
	}
	internal class LoadObjectFromLocal<C> : ExecutionControlNode<C> where C : ExecutionContext
	{
		public int Size;

		public int Offset;

		public override void Evaluate(C context)
		{
			context.Objects.Load(Size, Offset);
		}

		public LoadObjectFromLocal(int size, int offset)
		{
			Size = size;
			Offset = offset;
		}
	}
	internal class PopToLocal<C> : ExecutionControlNode<C> where C : ExecutionContext
	{
		public readonly LocalNodeData Mapping;

		public override void Evaluate(C context)
		{
			context.PopToLocal(in Mapping);
		}

		public PopToLocal(in LocalNodeData mapping)
		{
			Mapping = mapping;
		}
	}
	internal class PushObject<C, T> : ExecutionControlNode<C> where C : ExecutionContext
	{
		public T Object;

		public override void Evaluate(C context)
		{
			context.Objects.Push(Object);
		}

		public PushObject(T @object)
		{
			Object = @object;
		}
	}
	internal class PushValue<C, T> : ExecutionControlNode<C> where C : ExecutionContext where T : unmanaged
	{
		public T Value;

		public override void Evaluate(C context)
		{
			context.Values.Push(Value);
		}

		public PushValue(T value)
		{
			Value = value;
		}
	}
	public static class ExecutionHelper
	{
		private static ConcurrentDictionary<Type, int> _sizeCache = new ConcurrentDictionary<Type, int>();

		public static int SizeOf(Type type)
		{
			if (_sizeCache.TryGetValue(type, out var value))
			{
				return value;
			}
			try
			{
				value = (int)typeof(ExecutionHelper).GetMethod("SizeOfGeneric", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(type).Invoke(null, null);
				_sizeCache.TryAdd(type, value);
				return value;
			}
			catch (Exception innerException)
			{
				throw new Exception($"Exception when determining size of type {type}", innerException);
			}
		}

		private unsafe static int SizeOfGeneric<T>() where T : unmanaged
		{
			return sizeof(T);
		}
	}
	[AttributeUsage(AttributeTargets.Field)]
	public class ExecutionInputAttribute : CrossRuntimeInputAttribute
	{
		public override bool IsValidTargetRuntime(NodeRuntime runtime)
		{
			Type type = runtime.GetType();
			if (!type.IsGenericType)
			{
				return false;
			}
			return type.GetGenericTypeDefinition() == typeof(ExecutionRuntime<>);
		}
	}
	public abstract class ExecutionNode<C> : Node, IExecutionNode<C>, IExecutionNode, INode where C : ExecutionContext
	{
		private static ConcurrentDictionary<Type, ExecutionNodeMetadata> _metadataCache = new ConcurrentDictionary<Type, ExecutionNodeMetadata>();

		private const BindingFlags OPERATION_BIND_FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		private const BindingFlags LOCALS_BIND_FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		private const BindingFlags STORES_BIND_FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		private const BindingFlags GLOBAL_BIND_FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		public abstract bool CanBeEvaluated { get; }

		public virtual int FixedValueStackSize => ExecutionMetadata.FixedValueStackSize;

		public virtual int FixedObjectStackSize => ExecutionMetadata.FixedObjectStackSize;

		public virtual int FixedLocalsCount => ExecutionMetadata.FixedLocalsCount;

		public virtual int FixedValueLocalsSize => ExecutionMetadata.FixedValueLocalsSize;

		public virtual int FixedObjectLocalsSize => ExecutionMetadata.FixedObjectLocalsSize;

		public virtual int FixedStoresCount => ExecutionMetadata.FixedStoresCount;

		public virtual int FixedValueStoresSize => ExecutionMetadata.FixedValueStoresSize;

		public virtual int FixedObjectStoresSize => ExecutionMetadata.FixedObjectStoresSize;

		public int ValueStoreStartOffset { get; set; } = -1073741824;

		public int ObjectStoreStartOffset { get; set; } = -1073741824;

		public virtual ExecutionNodeMetadata ExecutionMetadata
		{
			get
			{
				Type type = GetType();
				if (_metadataCache.TryGetValue(type, out var value))
				{
					return value;
				}
				NodeMetadata metadata = base.Metadata;
				value = new ExecutionNodeMetadata();
				foreach (InputMetadata fixedInput in metadata.FixedInputs)
				{
					if (!fixedInput.IsConditional)
					{
						switch (fixedInput.DataClass)
						{
						case DataClass.Value:
						{
							int num = ExecutionHelper.SizeOf(fixedInput.InputType);
							value.FixedValueStackSize += num;
							break;
						}
						case DataClass.Object:
							value.FixedObjectStackSize++;
							break;
						default:
							throw new NotImplementedException("Unsupported data class");
						}
					}
				}
				int num2 = -value.FixedValueStackSize;
				int num3 = -value.FixedObjectStackSize;
				foreach (InputMetadata fixedInput2 in metadata.FixedInputs)
				{
					switch (fixedInput2.DataClass)
					{
					case DataClass.Value:
					{
						int num4 = ExecutionHelper.SizeOf(fixedInput2.InputType);
						if (fixedInput2.IsConditional)
						{
							value.FixedInputs.Add(new InputExecutionMetadata((short)num4, -1));
							break;
						}
						value.FixedInputs.Add(new InputExecutionMetadata((short)num4, (short)num2));
						num2 += num4;
						break;
					}
					case DataClass.Object:
						if (fixedInput2.IsConditional)
						{
							value.FixedInputs.Add(new InputExecutionMetadata(-1, -1));
							break;
						}
						value.FixedInputs.Add(new InputExecutionMetadata(-1, (short)num3));
						num3++;
						break;
					default:
						throw new NotImplementedException("Unsupported data class");
					}
				}
				foreach (OutputMetadata fixedOutput in metadata.FixedOutputs)
				{
					switch (fixedOutput.DataClass)
					{
					case DataClass.Value:
						value.FixedOutputs.Add(new OutputExecutionMetadata((short)ExecutionHelper.SizeOf(fixedOutput.OutputType)));
						break;
					case DataClass.Object:
						value.FixedOutputs.Add(new OutputExecutionMetadata(-1));
						break;
					default:
						throw new NotImplementedException("Unsupported data class");
					}
				}
				foreach (OperationMetadata fixedOperation in metadata.FixedOperations)
				{
					if (fixedOperation.IsSelf)
					{
						value.FixedOperations.Add(default(OperationExecutionMetadata));
						continue;
					}
					MethodInfo methodInfo = type.FindMethodInHierarchy("Do" + fixedOperation.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (methodInfo == null)
					{
						throw new Exception("Matching method not found for operation " + fixedOperation.Name);
					}
					value.FixedOperations.Add(new OperationExecutionMetadata(methodInfo));
				}
				foreach (OperationListMetadata dynamicOperation in metadata.DynamicOperations)
				{
					MethodInfo methodInfo2 = null;
					MethodInfo methodInfo3 = null;
					if (dynamicOperation.SupportsSync)
					{
						methodInfo2 = type.FindMethodInHierarchy("Do" + dynamicOperation.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						if (methodInfo2 == null)
						{
							throw new Exception("Matching sync method not found for operation list " + dynamicOperation.Name);
						}
					}
					if (dynamicOperation.SupportsAsync)
					{
						methodInfo3 = type.FindMethodInHierarchy("Do" + dynamicOperation.Name + "Async", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						if (methodInfo3 == null)
						{
							throw new Exception("Matching async method not found for operation list " + dynamicOperation.Name);
						}
					}
					value.DynamicOperations.Add(new OperationListExecutionMetadata(methodInfo2, methodInfo3));
				}
				foreach (GlobalRefMetadata fixedGlobalRef in metadata.FixedGlobalRefs)
				{
					MethodInfo methodInfo4 = type.FindMethodInHierarchy("On" + fixedGlobalRef.Name + "Changed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (methodInfo4 == null)
					{
						throw new Exception("Matching GlobalRef change not found for " + fixedGlobalRef.Name);
					}
					value.FixedGlobalRefs.Add(new GlobalRefExecutionMetadata(methodInfo4));
				}
				foreach (GlobalRefListMetadata dynamicGlobalRef in metadata.DynamicGlobalRefs)
				{
					MethodInfo methodInfo5 = type.FindMethodInHierarchy("On" + dynamicGlobalRef.Name + "Changed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (methodInfo5 == null)
					{
						throw new Exception("Matching GlobalRefList change not found for " + dynamicGlobalRef.Name);
					}
					value.DynamicGlobalRefs.Add(new GlobalRefListExecutionMetadata(methodInfo5));
				}
				foreach (FieldInfo item in type.EnumerateAllInstanceFields((Type t) => !t.IsGenericType || t.GetGenericTypeDefinition() != typeof(ExecutionNode<>)))
				{
					if (item.FieldType.IsGenericType)
					{
						Type genericTypeDefinition = item.FieldType.GetGenericTypeDefinition();
						if (genericTypeDefinition == typeof(ValueLocal<>))
						{
							value.FixedLocals.Add(new LocalExecutionMetadata(item, DataClass.Value));
						}
						else if (genericTypeDefinition == typeof(ObjectLocal<>))
						{
							value.FixedLocals.Add(new LocalExecutionMetadata(item, DataClass.Object));
						}
						else if (genericTypeDefinition == typeof(ValueStore<>))
						{
							value.FixedStores.Add(new StoreExecutionMetadata(item, DataClass.Value));
						}
						else if (genericTypeDefinition == typeof(ObjectStore<>))
						{
							value.FixedStores.Add(new StoreExecutionMetadata(item, DataClass.Object));
						}
					}
				}
				foreach (LocalExecutionMetadata fixedLocal in value.FixedLocals)
				{
					if (fixedLocal.valueSize > 0)
					{
						value.FixedValueLocalsSize += fixedLocal.valueSize;
					}
					else
					{
						value.FixedObjectLocalsSize++;
					}
				}
				foreach (StoreExecutionMetadata fixedStore in value.FixedStores)
				{
					if (fixedStore.valueSize > 0)
					{
						value.FixedValueStoresSize += fixedStore.valueSize;
					}
					else
					{
						value.FixedObjectStoresSize++;
					}
				}
				value.GenerateDefaultStackLayout();
				_metadataCache.TryAdd(type, value);
				return value;
			}
		}

		public abstract void Evaluate(C context);

		public virtual int GetValueInputSize(int index)
		{
			ExecutionNodeMetadata executionMetadata = ExecutionMetadata;
			if (index < executionMetadata.FixedInputCount)
			{
				return executionMetadata.FixedInputs[index].size;
			}
			InputListMetadata listMetadata;
			return ExecutionHelper.SizeOf(GetDynamicInputList(ref index, out listMetadata).GetInputType(index));
		}

		public virtual int GetInputStackOffset(int index)
		{
			return ExecutionMetadata.FixedInputs[index].stackOffset;
		}

		public virtual short[] GetDefaultStackLayout()
		{
			return ExecutionMetadata.DefaultFixedStackLayout;
		}

		public virtual int GetValueOutputSize(int index)
		{
			ExecutionNodeMetadata executionMetadata = ExecutionMetadata;
			if (index < executionMetadata.FixedOutputCount)
			{
				OutputExecutionMetadata outputExecutionMetadata = executionMetadata.FixedOutputs[index];
				if (outputExecutionMetadata.size <= 0)
				{
					throw new InvalidOperationException($"Output with index {index} is not of value type");
				}
				return outputExecutionMetadata.size;
			}
			OutputListMetadata listMetadata;
			return ExecutionHelper.SizeOf(GetDynamicOutputList(ref index, out listMetadata).GetOutputType(index));
		}

		public virtual bool IsOperationPassthrough(int index)
		{
			return false;
		}

		public virtual ExecutionOperationHandler<T> GetOperationHandler<T>(int index) where T : ExecutionContext
		{
			MethodInfo handlerMethod = GetHandlerMethod(index, async: false);
			if (handlerMethod.ReturnType == typeof(void))
			{
				return new VoidExecutionOperationWrapper<T>((VoidExecutionOperationHandler<T>)Delegate.CreateDelegate(typeof(VoidExecutionOperationHandler<T>), this, handlerMethod)).Run;
			}
			return (ExecutionOperationHandler<T>)Delegate.CreateDelegate(typeof(ExecutionOperationHandler<T>), this, handlerMethod);
		}

		public virtual AsyncExecutionOperationHandler<T> GetAsyncOperationHandler<T>(int index) where T : ExecutionContext
		{
			MethodInfo handlerMethod = GetHandlerMethod(index, async: true);
			if (handlerMethod.ReturnType == typeof(Task))
			{
				return new AsyncVoidExecutionOperationWrapper<T>((AsyncVoidExecutionOperationHandler<T>)Delegate.CreateDelegate(typeof(AsyncVoidExecutionOperationHandler<T>), this, handlerMethod)).RunAsync;
			}
			return (AsyncExecutionOperationHandler<T>)Delegate.CreateDelegate(typeof(AsyncExecutionOperationHandler<T>), this, handlerMethod);
		}

		public virtual void GlobalChanged<T>(int index, T value, C context)
		{
			MethodInfo methodInfo = ExecutionMetadata.FixedGlobalRefs[index].Method;
			if (methodInfo.IsGenericMethodDefinition)
			{
				methodInfo = methodInfo.MakeGenericMethod(typeof(T));
			}
			((ExecutionGlobalRefHandler<C, T>)methodInfo.CreateDelegate(typeof(ExecutionGlobalRefHandler<C, T>), this))(value, context);
		}

		public virtual void ListGlobalChanged<T>(int listIndex, int index, T value, C context)
		{
			MethodInfo methodInfo = ExecutionMetadata.DynamicGlobalRefs[listIndex].Method;
			if (methodInfo.IsGenericMethodDefinition)
			{
				methodInfo = methodInfo.MakeGenericMethod(typeof(T));
			}
			Type typeFromHandle = typeof(ExecutionGlobalRefListHandler<C, T>);
			((ExecutionGlobalRefListHandler<C, T>)methodInfo.CreateDelegate(typeFromHandle, this))(index, value, context);
		}

		private MethodInfo GetHandlerMethod(int index, bool async)
		{
			OperationExecutionMetadata operationExecutionMetadata = ExecutionMetadata.FixedOperations[index];
			if (operationExecutionMetadata.Method == null)
			{
				return GetType().GetMethod(async ? "RunAsync" : "Run");
			}
			return operationExecutionMetadata.Method;
		}

		public virtual ExecutionOperationHandler<T> GetDynamicOperationHandler<T>(int listIndex, int index) where T : ExecutionContext
		{
			OperationListExecutionMetadata operationListExecutionMetadata = ExecutionMetadata.DynamicOperations[listIndex];
			if (operationListExecutionMetadata.SyncMethod == null)
			{
				throw new InvalidOperationException("Operation list doesn't support sync operations");
			}
			return new ExecutionListOperationWrapper<T>((ExecutionListOperationHandler<T>)Delegate.CreateDelegate(typeof(ExecutionListOperationHandler<T>), this, operationListExecutionMetadata.SyncMethod), index).Execute;
		}

		public virtual AsyncExecutionOperationHandler<T> GetDynamicAsyncOperationHandler<T>(int listIndex, int index) where T : ExecutionContext
		{
			OperationListExecutionMetadata operationListExecutionMetadata = ExecutionMetadata.DynamicOperations[listIndex];
			if (operationListExecutionMetadata.AsyncMethod == null)
			{
				throw new InvalidOperationException("Operation list doesn't support async operations");
			}
			return new AsyncExecutionListOperationWrapper<T>((AsyncExecutionListOperationHandler<T>)Delegate.CreateDelegate(typeof(AsyncExecutionListOperationHandler<T>), this, operationListExecutionMetadata.AsyncMethod), index).ExecuteAsync;
		}

		public virtual DataClass GetLocalDataClass(int index)
		{
			return ExecutionMetadata.FixedLocals[index].dataClass;
		}

		public virtual Type GetLocalType(int index)
		{
			return ExecutionMetadata.FixedLocals[index].type;
		}

		public virtual int GetValueLocalSize(int index)
		{
			return ExecutionMetadata.FixedLocals[index].valueSize;
		}

		public virtual void SetLocalOffset(int index, int offset)
		{
			FieldInfo field = ExecutionMetadata.FixedLocals[index].field;
			FieldInfo field2 = field.FieldType.GetField("offset", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			object value = field.GetValue(this);
			field2.SetValue(value, offset);
			field.SetValue(this, value);
		}

		public virtual DataClass GetStoreDataClass(int index)
		{
			return ExecutionMetadata.FixedStores[index].dataClass;
		}

		public virtual Type GetStoreType(int index)
		{
			return ExecutionMetadata.FixedStores[index].type;
		}

		public virtual int GetValueStoreSize(int index)
		{
			return ExecutionMetadata.FixedStores[index].valueSize;
		}

		public virtual void SetStoreOffset(int index, int offset)
		{
			FieldInfo field = ExecutionMetadata.FixedStores[index].field;
			FieldInfo field2 = field.FieldType.GetField("offset", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			object value = field.GetValue(this);
			field2.SetValue(value, offset);
			field.SetValue(this, value);
		}
	}
	public abstract class ValueFunctionNode<C, T> : ExecutionNode<C>, IValueOutput<T>, IOutput<T>, IOutput where C : ExecutionContext where T : unmanaged
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

		public sealed override void Evaluate(C context)
		{
			T value = Compute(context);
			context.PopInputs();
			context.Values.Push(value);
		}

		protected abstract T Compute(C context);
	}
	public abstract class ObjectFunctionNode<C, T> : ExecutionNode<C>, IObjectOutput<T>, IOutput<T>, IOutput where C : ExecutionContext
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

		public sealed override void Evaluate(C context)
		{
			T obj = Compute(context);
			context.PopInputs();
			context.Objects.Push(obj);
		}

		protected abstract T Compute(C context);
	}
	public abstract class VoidNode<C> : ExecutionNode<C> where C : ExecutionContext
	{
		public override bool CanBeEvaluated => true;

		public override void Evaluate(C context)
		{
			ComputeOutputs(context);
			context.PopInputs();
		}

		protected virtual void ComputeOutputs(C context)
		{
			throw new NotImplementedException($"Method Compute() is not implemented on derived type {GetType()}");
		}
	}
	public interface IExecutionOperationNode : ISyncOperation, IOperation
	{
		ExecutionOperationHandler<C> GetHandler<C>() where C : ExecutionContext;
	}
	public interface IExecutionAsyncOperationNode : IAsyncOperation, IOperation
	{
		AsyncExecutionOperationHandler<C> GetAsyncHandler<C>() where C : ExecutionContext;
	}
	public abstract class ActionNode<C> : ExecutionNode<C>, IExecutionOperationNode, ISyncOperation, IOperation where C : ExecutionContext
	{
		public Node OwnerNode => this;

		public ExecutionOperationHandler<C> Handler => Run;

		public override bool CanBeEvaluated => false;

		public override void Evaluate(C context)
		{
			throw new NotSupportedException("Evaluation is not supported for action nodes.");
		}

		public virtual ExecutionOperationHandler<T> GetHandler<T>() where T : ExecutionContext
		{
			return (ExecutionOperationHandler<T>)new ExecutionOperationHandler<C>(Run);
		}

		protected abstract IOperation Run(C context);
	}
	public abstract class ActionFlowNode<C> : ActionNode<C> where C : ExecutionContext
	{
		public Continuation Next;

		protected sealed override IOperation Run(C context)
		{
			Do(context);
			return Next.Target;
		}

		protected abstract void Do(C context);
	}
	public abstract class ActionBreakableFlowNode<C> : ActionNode<C> where C : ExecutionContext
	{
		public Continuation Next;

		protected sealed override IOperation Run(C context)
		{
			if (Do(context))
			{
				return Next.Target;
			}
			return null;
		}

		protected abstract bool Do(C context);
	}
	public abstract class AsyncActionNode<C> : ExecutionNode<C>, IExecutionAsyncOperationNode, IAsyncOperation, IOperation where C : ExecutionContext
	{
		public Node OwnerNode => this;

		public override bool CanBeEvaluated => false;

		public override void Evaluate(C context)
		{
			throw new NotSupportedException("Evaluation is not supported for action nodes.");
		}

		public virtual AsyncExecutionOperationHandler<T> GetAsyncHandler<T>() where T : ExecutionContext
		{
			return (AsyncExecutionOperationHandler<T>)new AsyncExecutionOperationHandler<C>(RunAsync);
		}

		protected abstract Task<IOperation> RunAsync(C context);
	}
	public abstract class AsyncActionFlowNode<C> : AsyncActionNode<C> where C : ExecutionContext
	{
		public Continuation Next;

		protected sealed override async Task<IOperation> RunAsync(C context)
		{
			await Do(context);
			return Next.Target;
		}

		protected abstract Task Do(C context);
	}
	public abstract class AsyncActionBreakableFlowNode<C> : AsyncActionNode<C> where C : ExecutionContext
	{
		public Continuation Next;

		protected sealed override async Task<IOperation> RunAsync(C context)
		{
			if (await Do(context))
			{
				return Next.Target;
			}
			return null;
		}

		protected abstract Task<bool> Do(C context);
	}
	public interface IExecutionNode : INode
	{
		bool CanBeEvaluated { get; }

		int FixedValueStackSize { get; }

		int FixedObjectStackSize { get; }

		int FixedLocalsCount { get; }

		int FixedValueLocalsSize { get; }

		int FixedObjectLocalsSize { get; }

		int FixedStoresCount { get; }

		int FixedValueStoresSize { get; }

		int FixedObjectStoresSize { get; }

		int ValueStoreStartOffset { get; set; }

		int ObjectStoreStartOffset { get; set; }

		int GetValueInputSize(int index);

		int GetInputStackOffset(int index);

		short[] GetDefaultStackLayout();

		int GetValueOutputSize(int index);

		bool IsOperationPassthrough(int index);

		ExecutionOperationHandler<T> GetOperationHandler<T>(int index) where T : ExecutionContext;

		ExecutionOperationHandler<T> GetDynamicOperationHandler<T>(int listIndex, int index) where T : ExecutionContext;

		AsyncExecutionOperationHandler<T> GetAsyncOperationHandler<T>(int index) where T : ExecutionContext;

		AsyncExecutionOperationHandler<T> GetDynamicAsyncOperationHandler<T>(int listIndex, int index) where T : ExecutionContext;

		DataClass GetLocalDataClass(int index);

		Type GetLocalType(int index);

		int GetValueLocalSize(int index);

		void SetLocalOffset(int index, int offset);

		DataClass GetStoreDataClass(int index);

		Type GetStoreType(int index);

		int GetValueStoreSize(int index);

		void SetStoreOffset(int index, int offset);
	}
	public interface IExecutionNode<in C> : IExecutionNode, INode where C : ExecutionContext
	{
		void Evaluate(C context);

		void GlobalChanged<T>(int index, T value, C context);

		void ListGlobalChanged<T>(int listIndex, int index, T value, C context);
	}
	public interface IExecutionRuntimeInterop
	{
		bool InputNodesMustBeLocal { get; }
	}
	public readonly struct OperationExecutionMetadata
	{
		public readonly MethodInfo Method;

		public OperationExecutionMetadata(MethodInfo method)
		{
			Method = method;
		}
	}
	public readonly struct OperationListExecutionMetadata
	{
		public readonly MethodInfo SyncMethod;

		public readonly MethodInfo AsyncMethod;

		public OperationListExecutionMetadata(MethodInfo syncMethod, MethodInfo asyncMethod)
		{
			SyncMethod = syncMethod;
			AsyncMethod = asyncMethod;
		}
	}
	public class ExecutionNodeMetadata
	{
		public int FixedInputCount => FixedInputs.Count;

		public int FixedOutputCount => FixedOutputs.Count;

		public int FixedActionCount => FixedOperations.Count;

		public int FixedLocalsCount => FixedLocals.Count;

		public int FixedStoresCount => FixedStores.Count;

		public int DynamicActionCount => DynamicOperations.Count;

		public int FixedValueStackSize { get; internal set; }

		public int FixedObjectStackSize { get; internal set; }

		public int FixedValueLocalsSize { get; internal set; }

		public int FixedObjectLocalsSize { get; internal set; }

		public int FixedValueStoresSize { get; internal set; }

		public int FixedObjectStoresSize { get; internal set; }

		public short[] DefaultFixedStackLayout { get; private set; }

		public List<InputExecutionMetadata> FixedInputs { get; private set; } = new List<InputExecutionMetadata>();

		public List<OutputExecutionMetadata> FixedOutputs { get; private set; } = new List<OutputExecutionMetadata>();

		public List<OperationExecutionMetadata> FixedOperations { get; private set; } = new List<OperationExecutionMetadata>();

		public List<LocalExecutionMetadata> FixedLocals { get; private set; } = new List<LocalExecutionMetadata>();

		public List<StoreExecutionMetadata> FixedStores { get; private set; } = new List<StoreExecutionMetadata>();

		public List<GlobalRefExecutionMetadata> FixedGlobalRefs { get; private set; } = new List<GlobalRefExecutionMetadata>();

		public List<OperationListExecutionMetadata> DynamicOperations { get; private set; } = new List<OperationListExecutionMetadata>();

		public List<GlobalRefListExecutionMetadata> DynamicGlobalRefs { get; private set; } = new List<GlobalRefListExecutionMetadata>();

		internal void GenerateDefaultStackLayout()
		{
			if (FixedInputs.Count > 0)
			{
				DefaultFixedStackLayout = new short[FixedInputs.Count];
				for (int i = 0; i < FixedInputs.Count; i++)
				{
					DefaultFixedStackLayout[i] = FixedInputs[i].stackOffset;
				}
			}
		}
	}
	public readonly struct GlobalRefExecutionMetadata
	{
		public readonly MethodInfo Method;

		public GlobalRefExecutionMetadata(MethodInfo method)
		{
			Method = method;
		}
	}
	public readonly struct GlobalRefListExecutionMetadata
	{
		public readonly MethodInfo Method;

		public GlobalRefListExecutionMetadata(MethodInfo method)
		{
			Method = method;
		}
	}
	public readonly struct InputExecutionMetadata
	{
		public readonly short size;

		public readonly short stackOffset;

		public InputExecutionMetadata(short size, short stackOffset)
		{
			this.size = size;
			this.stackOffset = stackOffset;
		}
	}
	public readonly struct LocalExecutionMetadata
	{
		public readonly FieldInfo field;

		public readonly Type type;

		public readonly DataClass dataClass;

		public readonly short valueSize;

		public LocalExecutionMetadata(FieldInfo field, DataClass dataClass)
		{
			this.field = field;
			type = field.FieldType.GetGenericArguments()[0];
			this.dataClass = dataClass;
			if (dataClass == DataClass.Value)
			{
				valueSize = (short)ExecutionHelper.SizeOf(type);
			}
			else
			{
				valueSize = -1;
			}
		}
	}
	public readonly struct OutputExecutionMetadata
	{
		public readonly short size;

		public OutputExecutionMetadata(short size)
		{
			this.size = size;
		}
	}
	public readonly struct StoreExecutionMetadata
	{
		public readonly FieldInfo field;

		public readonly Type type;

		public readonly DataClass dataClass;

		public readonly short valueSize;

		public StoreExecutionMetadata(FieldInfo field, DataClass dataClass)
		{
			this.field = field;
			type = field.FieldType.GetGenericArguments()[0];
			this.dataClass = dataClass;
			if (dataClass == DataClass.Value)
			{
				valueSize = (short)ExecutionHelper.SizeOf(type);
			}
			else
			{
				valueSize = -1;
			}
		}
	}
	public delegate void ExecutionNodeOperation<T, C>(T node, C context) where T : INode where C : ExecutionContext;
	public class AsyncCallExportWrapper<C> : ImpulseExportWrapper<C> where C : ExecutionContext
	{
		public AsyncCallExportWrapper(int index)
			: base(index)
		{
		}

		public async Task<IOperation> ExecuteAsync(C context)
		{
			if (!ShouldBeContinuation(context))
			{
				await context.InvokeAsyncImpulseExport(base.Index);
			}
			else
			{
				context.ImpulseExport = base.Index;
			}
			return null;
		}
	}
	internal class AsyncExecutionListOperationWrapper<C> where C : ExecutionContext
	{
		public int Index { get; private set; }

		public AsyncExecutionListOperationHandler<C> Handler { get; private set; }

		public AsyncExecutionListOperationWrapper(AsyncExecutionListOperationHandler<C> handler, int index)
		{
			Handler = handler;
			Index = index;
		}

		public Task<IOperation> ExecuteAsync(C context)
		{
			return Handler(context, Index);
		}
	}
	internal class AsyncOperationSequence<C> : OperationSequence<C, IAsyncOperation, AsyncExecutionOperationHandler<C>> where C : ExecutionContext
	{
		public override bool IsAsync => true;

		public AsyncOperationSequence(ExecutionRuntime<C> runtime, IAsyncOperation origin)
			: base(runtime, origin)
		{
		}

		protected override AsyncExecutionOperationHandler<C> GetHandler(IOperation operation)
		{
			if (!(operation is AsyncOperation asyncOperation))
			{
				if (!(operation is AsyncOperationList.Operation operation2))
				{
					if (!(operation is MixedOperationList.Operation operation3))
					{
						if (!(operation is IExecutionAsyncOperationNode executionAsyncOperationNode))
						{
							if (!(operation is AsyncCallExport asyncCallExport))
							{
								if (operation is ContinuationExport continuationExport)
								{
									return new ContinuationExportWrapper<C>(continuationExport.Index).ExecuteAsync;
								}
								throw new ArgumentException("Could not extract handler from: " + operation);
							}
							return new AsyncCallExportWrapper<C>(asyncCallExport.Index).ExecuteAsync;
						}
						return executionAsyncOperationNode.GetAsyncHandler<C>();
					}
					if (!operation3.IsAsync)
					{
						throw new InvalidOperationException("Target list operation is sync, cannot get handler for AsyncOperationSequence");
					}
					return ((IExecutionNode)operation3.OwnerNode).GetDynamicAsyncOperationHandler<C>(operation3.List.Index, operation3.Index);
				}
				return ((IExecutionNode)operation2.OwnerNode).GetDynamicAsyncOperationHandler<C>(operation2.List.Index, operation2.Index);
			}
			IExecutionNode executionNode = (IExecutionNode)asyncOperation.OwnerNode;
			if (executionNode.IsOperationPassthrough(asyncOperation.Index))
			{
				return null;
			}
			return executionNode.GetAsyncOperationHandler<C>(asyncOperation.Index);
		}

		public override async Task<IOperation> ExecuteAsync(C context)
		{
			IOperation operation = null;
			foreach (AsyncExecutionOperationHandler<C> item in base.operationSequence)
			{
				if (base.Runtime.IsEvaluationDirty(context))
				{
					base.Runtime.ClearEvaluatedFlags(context);
				}
				operation = await item(context);
				if (operation == null)
				{
					return null;
				}
			}
			return operation;
		}
	}
	internal class EvaluationAnalysisContext<C> where C : ExecutionContext
	{
		private HashSet<EvaluationSequence<C>> processedSequences = new HashSet<EvaluationSequence<C>>();

		private HashSet<IExecutionNode<C>> globalEvaluatedNodes = new HashSet<IExecutionNode<C>>();

		private HashSet<IExecutionNode<C>> sequenceEvaluatedNodes = new HashSet<IExecutionNode<C>>();

		private HashSet<IExecutionNode<C>> localNodes = new HashSet<IExecutionNode<C>>();

		public ExecutionRuntime<C> Runtime { get; private set; }

		public IEnumerable<IExecutionNode<C>> LocalNodes => localNodes;

		public EvaluationAnalysisContext(ExecutionRuntime<C> runtime)
		{
			Runtime = runtime;
		}

		public bool BeginSequence(EvaluationSequence<C> sequence)
		{
			if (!processedSequences.Add(sequence))
			{
				return false;
			}
			sequenceEvaluatedNodes.Clear();
			return true;
		}

		public bool MarkNodeEvaluated(IExecutionNode<C> node, EvaluationSequence<C> sequence, bool external)
		{
			if (!external && !sequenceEvaluatedNodes.Add(node))
			{
				localNodes.Add(node);
				return true;
			}
			if (!globalEvaluatedNodes.Add(node))
			{
				EvaluationSequence<C> evaluationSequence = Runtime.EnsureSequence(node);
				evaluationSequence.MarkLocal();
				processedSequences.Add(evaluationSequence);
				localNodes.Add(node);
			}
			return false;
		}

		public void MarkLocal(IExecutionNode<C> node)
		{
			localNodes.Add(node);
		}
	}
	internal class EvaluationBuildContext<C> where C : ExecutionContext
	{
		private HashSet<IExecutionNode<C>> evaluatedNodes = new HashSet<IExecutionNode<C>>();

		public bool MarkEvaluated(IExecutionNode<C> node)
		{
			return !evaluatedNodes.Add(node);
		}

		public void ClearEvaluated()
		{
			evaluatedNodes.Clear();
		}
	}
	public class CallExportWrapper<C> : ImpulseExportWrapper<C> where C : ExecutionContext
	{
		public CallExportWrapper(int index)
			: base(index)
		{
		}

		public IOperation Execute(C context)
		{
			if (ShouldBeContinuation(context))
			{
				context.ImpulseExport = base.Index;
			}
			else
			{
				context.InvokeImpulseExport(base.Index);
			}
			return null;
		}
	}
	public class ContinuationExportWrapper<C> : ImpulseExportWrapper<C> where C : ExecutionContext
	{
		public ContinuationExportWrapper(int index)
			: base(index)
		{
		}

		public IOperation Execute(C context)
		{
			if (ShouldBeContinuation(context))
			{
				context.ImpulseExport = base.Index;
				return null;
			}
			throw new InvalidOperationException("Continuation export must be a continuation!");
		}

		public async Task<IOperation> ExecuteAsync(C context)
		{
			return Execute(context);
		}
	}
	internal readonly struct EvaluationAction<C> where C : ExecutionContext
	{
		public readonly IExecutionNode<C> node;

		public readonly ExecutionContext.StackLayout stackLayout;

		public EvaluationAction(IExecutionNode<C> node, short[] stackLayout, short valueSize, short objectSize)
		{
			this.node = node;
			this.stackLayout = new ExecutionContext.StackLayout(stackLayout, valueSize, objectSize);
		}

		public EvaluationAction(IExecutionNode<C> node)
		{
			this.node = node;
			stackLayout = default(ExecutionContext.StackLayout);
		}

		public override string ToString()
		{
			return $"Eval: {node}. StackLayout: {stackLayout}";
		}
	}
	internal class EvaluationSequence<C> where C : ExecutionContext
	{
		private bool isRoot;

		private HashSet<IExecutionNode<C>> rootNodes = new HashSet<IExecutionNode<C>>();

		private IExecutionNode<C> firstNode;

		private List<IOutput> outputs;

		private List<EvaluationAction<C>> evaluationSequence = new List<EvaluationAction<C>>();

		public ExecutionRuntime<C> Runtime { get; private set; }

		public int Index { get; private set; }

		public bool IsLocal { get; private set; }

		public EvaluationSequence(ExecutionRuntime<C> runtime, List<IOutput> outputs)
		{
			Runtime = runtime;
			Index = -1;
			this.outputs = outputs;
			isRoot = true;
			foreach (IOutput output in outputs)
			{
				if (output.OwnerNode is IExecutionNode<C> item)
				{
					rootNodes.Add(item);
					if (firstNode == null)
					{
						firstNode = item;
					}
				}
			}
		}

		public EvaluationSequence(ExecutionRuntime<C> runtime, IExecutionNode<C> node, int index)
		{
			Runtime = runtime;
			Index = index;
			rootNodes.Add(node);
			firstNode = node;
			if (!node.CanBeEvaluated)
			{
				MarkLocal();
			}
		}

		internal void MarkLocal()
		{
			IsLocal = true;
		}

		internal void Analyze(EvaluationAnalysisContext<C> context)
		{
			foreach (IExecutionNode<C> rootNode in rootNodes)
			{
				if (!isRoot || !context.MarkNodeEvaluated(rootNode, this, external: false))
				{
					AnalyzeRecursive(rootNode, context);
				}
			}
		}

		private void AnalyzeRecursive(IExecutionNode<C> node, EvaluationAnalysisContext<C> context)
		{
			if (!node.CanBeEvaluated)
			{
				return;
			}
			for (int i = 0; i < node.InputCount; i++)
			{
				IOutput inputSource = node.GetInputSource(i);
				if (inputSource != null && !(inputSource.OwnerNode is DataImportNode))
				{
					IExecutionNode<C> node2 = (IExecutionNode<C>)inputSource.OwnerNode;
					bool flag = node.IsInputConditional(i);
					if (!(context.MarkNodeEvaluated(node2, this, flag) || flag))
					{
						AnalyzeRecursive(node2, context);
					}
				}
			}
		}

		internal void Build(EvaluationBuildContext<C> context)
		{
			foreach (IExecutionNode<C> rootNode in rootNodes)
			{
				if (Runtime.IsLocalNode(rootNode))
				{
					MarkLocal();
				}
				if (BuildRecursive(rootNode, context) || !isRoot)
				{
					continue;
				}
				for (int i = 0; i < outputs.Count; i++)
				{
					IOutput output = outputs[i];
					if (output.OwnerNode == rootNode)
					{
						switch (output.OutputDataClass)
						{
						case DataClass.Value:
							evaluationSequence.Add(new EvaluationAction<C>(new LoadValueFromLocal<C>(ExecutionHelper.SizeOf(output.OutputType), Runtime.GetLocalValueMapping(output))));
							break;
						case DataClass.Object:
							evaluationSequence.Add(new EvaluationAction<C>(new LoadObjectFromLocal<C>(1, Runtime.GetLocalValueMapping(output))));
							break;
						default:
							throw new NotImplementedException("Unsupported data class: " + output.OutputDataClass);
						}
					}
				}
			}
		}

		private bool BuildRecursive(IExecutionNode<C> node, EvaluationBuildContext<C> context)
		{
			if (!node.CanBeEvaluated)
			{
				return false;
			}
			short[] array = node.GetDefaultStackLayout();
			short num = 0;
			short num2 = 0;
			bool flag = false;
			bool flag2 = false;
			int argumentCount = node.ArgumentCount;
			if (argumentCount > node.FixedInputCount)
			{
				short[] array2 = new short[argumentCount];
				flag = true;
				if (array != null)
				{
					Array.Copy(array, array2, array.Length);
				}
				int num3 = ((array != null) ? array.Length : 0);
				int num4 = 0;
				int num5 = 0;
				for (int num6 = argumentCount - 1; num6 >= num3; num6--)
				{
					switch (node.GetInputTypeClass(num6))
					{
					case DataClass.Value:
						num4 -= node.GetValueInputSize(num6);
						array2[num6] = (short)num4;
						break;
					case DataClass.Object:
						num5--;
						array2[num6] = (short)num5;
						break;
					default:
						throw new NotSupportedException("Unsupported data class: " + node.GetInputTypeClass(num6));
					}
				}
				for (int i = 0; i < num3; i++)
				{
					switch (node.GetInputTypeClass(i))
					{
					case DataClass.Value:
						array2[i] += (short)num4;
						break;
					case DataClass.Object:
						array2[i] += (short)num5;
						break;
					default:
						throw new NotSupportedException("Unsupported data class: " + node.GetInputTypeClass(i));
					}
				}
				array = array2;
			}
			if (Runtime.IsLocalNode(node))
			{
				EvaluationSequence<C> sequence = Runtime.GetSequence(node);
				if (sequence != null)
				{
					if (sequence != this)
					{
						evaluationSequence.Add(new EvaluationAction<C>(new EvaluateSequence<C>(sequence), null, 0, 0));
						return false;
					}
					flag2 = true;
				}
				else
				{
					if (context.MarkEvaluated(node))
					{
						return false;
					}
					flag2 = true;
				}
			}
			for (int j = 0; j < node.InputCount; j++)
			{
				if (node.IsInputConditional(j))
				{
					continue;
				}
				IOutput inputSource = node.GetInputSource(j);
				if (inputSource != null)
				{
					if (inputSource.OwnerNode is DataImportNode || !BuildRecursive((IExecutionNode<C>)inputSource.OwnerNode, context))
					{
						if (!flag)
						{
							array = array.ToArray();
							flag = true;
						}
						DataClass inputTypeClass = node.GetInputTypeClass(j);
						int num7 = inputTypeClass switch
						{
							DataClass.Value => node.GetValueInputSize(j), 
							DataClass.Object => 1, 
							_ => throw new NotImplementedException("Invalid data class type"), 
						};
						for (int num8 = j - 1; num8 >= 0; num8--)
						{
							if (array[num8] < 0 && node.GetInputTypeClass(num8) == inputTypeClass)
							{
								array[num8] += (short)num7;
							}
						}
						array[j] = (short)Runtime.GetLocalValueMapping(inputSource);
					}
					else
					{
						switch (node.GetInputTypeClass(j))
						{
						case DataClass.Value:
							num += (short)node.GetValueInputSize(j);
							break;
						case DataClass.Object:
							num2++;
							break;
						default:
							throw new NotImplementedException("Invalid data class type");
						}
					}
				}
				else
				{
					Type inputType = node.GetInputType(j);
					Type type;
					switch (node.GetInputTypeClass(j))
					{
					case DataClass.Value:
						type = typeof(PushValue<, >).MakeGenericType(typeof(C), inputType);
						num += (short)node.GetValueInputSize(j);
						break;
					case DataClass.Object:
						type = typeof(PushObject<, >).MakeGenericType(typeof(C), inputType);
						num2++;
						break;
					default:
						throw new NotImplementedException("Unsupported data class");
					}
					IExecutionNode<C> node2 = (IExecutionNode<C>)Activator.CreateInstance(type, node.GetInputDefaultValue(j));
					evaluationSequence.Add(new EvaluationAction<C>(node2, null, 0, 0));
				}
			}
			evaluationSequence.Add(new EvaluationAction<C>(node, array, num, num2));
			if (flag2)
			{
				LocalNodeData mapping = Runtime.GetStaticNodeMapping(node);
				if (mapping.isImplicit)
				{
					evaluationSequence.Add(new EvaluationAction<C>(new PopToLocal<C>(in mapping), null, 0, 0));
				}
			}
			return !flag2;
		}

		internal T EvaluateValue<T>(IOutput output, C context) where T : unmanaged
		{
			Evaluate(context);
			if (IsLocal && !isRoot)
			{
				return context.Values.Read<T>(Runtime.GetLocalValueMapping(output));
			}
			return context.Values.Pop<T>();
		}

		internal T EvaluateObject<T>(IOutput output, C context)
		{
			Evaluate(context);
			if (IsLocal && !isRoot)
			{
				return context.Objects.Read<T>(Runtime.GetLocalValueMapping(output));
			}
			return context.Objects.Pop<T>();
		}

		internal void EvaluateValueToStack<T>(IOutput output, C context) where T : unmanaged
		{
			Evaluate(context);
			if (IsLocal && !isRoot)
			{
				context.Values.Push(context.Values.Read<T>(Runtime.GetLocalValueMapping(output)));
			}
		}

		internal void EvaluateObjectToStack<T>(IOutput output, C context)
		{
			Evaluate(context);
			if (IsLocal && !isRoot)
			{
				context.Objects.Push(context.Objects.Read<T>(Runtime.GetLocalValueMapping(output)));
			}
		}

		internal void Evaluate(C context)
		{
			if (IsLocal && !isRoot)
			{
				if (Runtime.IsEvaluated(firstNode, context))
				{
					return;
				}
				Runtime.MarkEvaluated(firstNode, context);
			}
			foreach (EvaluationAction<C> item in evaluationSequence)
			{
				context.stackLayout = item.stackLayout;
				item.node.Evaluate(context);
			}
		}

		public string ToDebugString(string prefix = "")
		{
			StringBuilder stringBuilder = new StringBuilder();
			int num = 0;
			foreach (EvaluationAction<C> item in evaluationSequence)
			{
				StringBuilder stringBuilder2;
				StringBuilder.AppendInterpolatedStringHandler handler;
				if (item.node is EvaluateSequence<C> evaluateSequence)
				{
					stringBuilder2 = stringBuilder;
					StringBuilder stringBuilder3 = stringBuilder2;
					handler = new StringBuilder.AppendInterpolatedStringHandler(21, 4, stringBuilder2);
					handler.AppendFormatted(prefix);
					handler.AppendLiteral("[");
					handler.AppendFormatted(num++);
					handler.AppendLiteral("] - ");
					handler.AppendFormatted(evaluateSequence.Sequence.ToString());
					handler.AppendLiteral(" (StackLayout: ");
					handler.AppendFormatted(item.stackLayout);
					handler.AppendLiteral(")");
					stringBuilder3.AppendLine(ref handler);
					stringBuilder.Append(evaluateSequence.Sequence.ToDebugString(prefix + "---"));
					continue;
				}
				string value = ((!(item.node is PopToLocal<C> popToLocal)) ? ((!(item.node is LoadValueFromLocal<C> loadValueFromLocal)) ? ((!(item.node is LoadObjectFromLocal<C> loadObjectFromLocal)) ? ((!(item.node is EvaluateSequence<C> evaluateSequence2)) ? item.node.ToString() : evaluateSequence2.ToString()) : $"LoadObjectFromLocal (Offset: {loadObjectFromLocal.Offset}, Size: {loadObjectFromLocal.Size})") : $"LoadValueFromLocal (Offset: {loadValueFromLocal.Offset}, Size: {loadValueFromLocal.Size})") : $"PopToLocal ({popToLocal.Mapping})");
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder4 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(21, 4, stringBuilder2);
				handler.AppendFormatted(prefix);
				handler.AppendLiteral("[");
				handler.AppendFormatted(num++);
				handler.AppendLiteral("] - ");
				handler.AppendFormatted(value);
				handler.AppendLiteral(" (StackLayout: ");
				handler.AppendFormatted(item.stackLayout);
				handler.AppendLiteral(")");
				stringBuilder4.AppendLine(ref handler);
			}
			return stringBuilder.ToString();
		}

		public override string ToString()
		{
			return $"{GetType().GetNiceTypeName()} (Index: {Index}) - Steps: {evaluationSequence.Count}, IsLocal: {IsLocal}, IsRoot: {isRoot}";
		}
	}
	public delegate void ExecutionGlobalRefHandler<in C, T>(T value, C context) where C : ExecutionContext;
	public delegate void ExecutionGlobalRefListHandler<in C, T>(int index, T value, C context) where C : ExecutionContext;
	internal class ExecutionListOperationWrapper<C> where C : ExecutionContext
	{
		public int Index { get; private set; }

		public ExecutionListOperationHandler<C> Handler { get; private set; }

		public ExecutionListOperationWrapper(ExecutionListOperationHandler<C> handler, int index)
		{
			Handler = handler;
			Index = index;
		}

		public IOperation Execute(C context)
		{
			return Handler(context, Index);
		}
	}
	public delegate IOperation ExecutionOperationHandler<in C>(C context) where C : ExecutionContext;
	public delegate IOperation ExecutionListOperationHandler<in C>(C context, int index) where C : ExecutionContext;
	public delegate Task<IOperation> AsyncExecutionOperationHandler<in C>(C context) where C : ExecutionContext;
	public delegate Task<IOperation> AsyncExecutionListOperationHandler<in C>(C context, int index) where C : ExecutionContext;
	public delegate void ExecutionImpulseExportHandler(ExecutionContext context, int index);
	public delegate Task ExecutionAsyncImpulseExportHandler(ExecutionContext context, int index);
	public delegate void VoidExecutionOperationHandler<in C>(C context) where C : ExecutionContext;
	public delegate Task AsyncVoidExecutionOperationHandler<in C>(C context) where C : ExecutionContext;
	public class VoidExecutionOperationWrapper<C> where C : ExecutionContext
	{
		public readonly VoidExecutionOperationHandler<C> handler;

		public VoidExecutionOperationWrapper(VoidExecutionOperationHandler<C> handler)
		{
			this.handler = handler;
		}

		public IOperation Run(C context)
		{
			handler(context);
			return null;
		}
	}
	public class AsyncVoidExecutionOperationWrapper<C> where C : ExecutionContext
	{
		public readonly AsyncVoidExecutionOperationHandler<C> handler;

		public AsyncVoidExecutionOperationWrapper(AsyncVoidExecutionOperationHandler<C> handler)
		{
			this.handler = handler;
		}

		public async Task<IOperation> RunAsync(C context)
		{
			await handler(context);
			return null;
		}
	}
	public interface IExecutionRuntime
	{
		bool RequiresScopeData { get; }

		int ValueStoreSize { get; }

		int ObjectStoreSize { get; }

		int TotalValueStackSize { get; }

		int TotalObjectStackSize { get; }

		IEnumerable<IExecutionNode> Nodes { get; }

		T EvaluateValue<T>(IOutput output, ExecutionContext context) where T : unmanaged;

		T EvaluateObject<T>(IOutput output, ExecutionContext context);

		void SetValue<T>(IOutput output, T value, ExecutionContext context) where T : unmanaged;

		void SetObject<T>(IOutput output, T value, ExecutionContext context);

		void Execute(ISyncOperation action, ExecutionContext context);

		Task ExecuteAsync(IOperation action, ExecutionContext context);

		string GetEvaluationSequenceDebug(IOutput output);
	}
	public class ExecutionRuntime<C> : NodeRuntime<IExecutionNode<C>>, IExecutionRuntime where C : ExecutionContext
	{
		private EvaluationSequence<C> _exportsEvaluationSequence;

		private Dictionary<IExecutionNode<C>, EvaluationSequence<C>> evaluationSequences = new Dictionary<IExecutionNode<C>, EvaluationSequence<C>>();

		private List<int> importsMapping = new List<int>();

		private Dictionary<IOutput, int> localValueMapping = new Dictionary<IOutput, int>();

		private Dictionary<IExecutionNode<C>, LocalNodeData> localNodes = new Dictionary<IExecutionNode<C>, LocalNodeData>();

		private Dictionary<IOperation, IOperationSequence<C>> operationSequences = new Dictionary<IOperation, IOperationSequence<C>>();

		private int _evaluationFlagsSize;

		private int _localValueStackSize;

		private int _localObjectStackSize;

		private int _valueLocalsSize;

		private int _objectLocalsSize;

		private bool _requiresScopeData;

		private int _valueStoresSize;

		private int _objectStoresSize;

		public bool RequiresScopeData => _requiresScopeData;

		public int ValueStoreSize => _valueStoresSize;

		public int ObjectStoreSize => _objectStoresSize;

		public int TotalValueStackSize => _localValueStackSize + _evaluationFlagsSize;

		public int TotalObjectStackSize => _localObjectStackSize;

		IEnumerable<IExecutionNode> IExecutionRuntime.Nodes => base.Nodes;

		public override Type GetCompatibleNodeType(Type type)
		{
			if (type.ContainsGenericParameters)
			{
				Type[] genericArguments = type.GetGenericArguments();
				Type contextType = typeof(C);
				for (int i = 0; i < genericArguments.Length; i++)
				{
					if (genericArguments[i].IsGenericParameter)
					{
						Type[] genericParameterConstraints = genericArguments[i].GetGenericParameterConstraints();
						if (genericParameterConstraints.Length != 0 && genericParameterConstraints.All((Type c) => c.IsAssignableFrom(contextType)))
						{
							genericArguments[i] = contextType;
							type = type.GetGenericTypeDefinition().MakeGenericType(genericArguments);
							break;
						}
					}
				}
			}
			return base.GetCompatibleNodeType(type);
		}

		public T EvaluateValue<T>(IOutput output, ExecutionContext context) where T : unmanaged
		{
			if (output.OwnerNode is DataImportNode)
			{
				return context.Values.Read<T>(GetLocalValueMapping(output));
			}
			MarkEvaluationDirty(context);
			C context2 = (C)context;
			if (!evaluationSequences.TryGetValue((IExecutionNode<C>)output.OwnerNode, out var value))
			{
				throw new InvalidOperationException("No evaluation sequence found for output: " + output?.ToString() + " on node: " + output.OwnerNode);
			}
			ExecutionContext.StackLayout stackLayout = context.stackLayout;
			try
			{
				return value.EvaluateValue<T>(output, context2);
			}
			finally
			{
				context.stackLayout = stackLayout;
			}
		}

		public T EvaluateObject<T>(IOutput output, ExecutionContext context)
		{
			if (output.OwnerNode is DataImportNode)
			{
				return context.Objects.Read<T>(GetLocalValueMapping(output));
			}
			MarkEvaluationDirty(context);
			C context2 = (C)context;
			EvaluationSequence<C> evaluationSequence = evaluationSequences[(IExecutionNode<C>)output.OwnerNode];
			ExecutionContext.StackLayout stackLayout = context.stackLayout;
			try
			{
				return evaluationSequence.EvaluateObject<T>(output, context2);
			}
			finally
			{
				context.stackLayout = stackLayout;
			}
		}

		internal void EvaluateValueToStack<T>(IOutput output, C context) where T : unmanaged
		{
			MarkEvaluationDirty(context);
			evaluationSequences[(IExecutionNode<C>)output.OwnerNode].EvaluateValueToStack<T>(output, context);
		}

		internal void EvaluateObjectToStack<T>(IOutput output, C context)
		{
			MarkEvaluationDirty(context);
			evaluationSequences[(IExecutionNode<C>)output.OwnerNode].EvaluateObjectToStack<T>(output, context);
		}

		public void SetValue<T>(IOutput output, T value, ExecutionContext context) where T : unmanaged
		{
			int offset = localValueMapping[output];
			context.Values.Write(offset, value);
		}

		public void SetObject<T>(IOutput output, T value, ExecutionContext context)
		{
			int offset = localValueMapping[output];
			context.Objects.Write(offset, value);
		}

		public void Execute(ISyncOperation action, ExecutionContext context)
		{
			C val = (C)context;
			val.EnterExecution();
			ExecuteSequence(action, val);
			val.ExitExecution();
		}

		public async Task ExecuteAsync(IOperation operation, ExecutionContext context)
		{
			C typedContext = (C)context;
			await typedContext.TryEnterAsyncExecution();
			await ExecuteAsyncSequence(operation, typedContext);
			typedContext.ExitExecution();
		}

		public void Rebuild()
		{
			_exportsEvaluationSequence = null;
			evaluationSequences.Clear();
			localValueMapping.Clear();
			importsMapping.Clear();
			localNodes.Clear();
			_evaluationFlagsSize = 0;
			_localValueStackSize = 0;
			_localObjectStackSize = 0;
			_valueLocalsSize = 0;
			_objectLocalsSize = 0;
			_requiresScopeData = false;
			_valueStoresSize = 0;
			_objectStoresSize = 0;
			operationSequences.Clear();
			ClearQueryCaches();
			for (int i = 0; i < base.ImpulseImportsCount; i++)
			{
				IOperation impulseImport = GetImpulseImport(i);
				if (!(impulseImport is ISyncOperation syncOperation))
				{
					if (!(impulseImport is IAsyncOperation asyncOperation))
					{
						if (impulseImport != null)
						{
							throw new NotImplementedException("Unsupported operation type: " + impulseImport?.GetType());
						}
					}
					else
					{
						operationSequences.Add(asyncOperation, new AsyncOperationSequence<C>(this, asyncOperation));
					}
				}
				else
				{
					operationSequences.Add(syncOperation, new SyncOperationSequence<C>(this, syncOperation));
				}
			}
			EvaluationAnalysisContext<C> evaluationAnalysisContext = new EvaluationAnalysisContext<C>(this);
			foreach (IExecutionNode<C> node2 in base.Nodes)
			{
				if (node2.FixedGlobalRefCount > 0)
				{
					_requiresScopeData = true;
				}
				for (int j = 0; j < node2.FixedLocalsCount; j++)
				{
					switch (node2.GetLocalDataClass(j))
					{
					case DataClass.Value:
					{
						int valueLocalSize = node2.GetValueLocalSize(j);
						node2.SetLocalOffset(j, _valueLocalsSize);
						_valueLocalsSize += valueLocalSize;
						break;
					}
					case DataClass.Object:
						node2.SetLocalOffset(j, _objectLocalsSize);
						_objectLocalsSize++;
						break;
					default:
						throw new NotImplementedException("Unknown data class");
					}
				}
				node2.ValueStoreStartOffset = int.MinValue;
				node2.ObjectStoreStartOffset = int.MinValue;
				for (int k = 0; k < node2.FixedStoresCount; k++)
				{
					switch (node2.GetStoreDataClass(k))
					{
					case DataClass.Value:
					{
						if (node2.ValueStoreStartOffset < 0)
						{
							node2.ValueStoreStartOffset = _valueStoresSize;
						}
						int valueStoreSize = node2.GetValueStoreSize(k);
						node2.SetStoreOffset(k, _valueStoresSize);
						_valueStoresSize += valueStoreSize;
						break;
					}
					case DataClass.Object:
						if (node2.ObjectStoreStartOffset < 0)
						{
							node2.ObjectStoreStartOffset = _objectStoresSize;
						}
						node2.SetStoreOffset(k, _objectStoresSize);
						_objectStoresSize++;
						break;
					default:
						throw new NotImplementedException("Unknown data class");
					}
				}
			}
			_localValueStackSize = _valueLocalsSize;
			_localObjectStackSize = _objectLocalsSize;
			_requiresScopeData |= _valueStoresSize > 0;
			_requiresScopeData |= _objectStoresSize > 0;
			for (int l = 0; l < base.DataImportsCount; l++)
			{
				IOutput import = GetImport(l);
				switch (import.OutputDataClass)
				{
				case DataClass.Value:
				{
					int num = ExecutionHelper.SizeOf(import.OutputType);
					localValueMapping.Add(import, _localValueStackSize);
					importsMapping.Add(_localValueStackSize);
					_localValueStackSize += num;
					break;
				}
				case DataClass.Object:
					localValueMapping.Add(import, _localObjectStackSize);
					importsMapping.Add(_localObjectStackSize);
					_localObjectStackSize++;
					break;
				}
			}
			foreach (NodeRuntime runtime in base.Group.Runtimes)
			{
				if (runtime == this)
				{
					continue;
				}
				for (int m = 0; m < runtime.NodeCount; m++)
				{
					INode nodeGeneric = runtime.GetNodeGeneric(m);
					NodeMetadata metadata = nodeGeneric.Metadata;
					for (int n = 0; n < metadata.FixedInputCount; n++)
					{
						if (!(metadata.FixedInputs[n].CrossRuntime is ExecutionInputAttribute))
						{
							continue;
						}
						IOutput inputSource = nodeGeneric.GetInputSource(n);
						if (inputSource != null && inputSource.OwnerNode.Runtime == this)
						{
							IExecutionNode<C> node = (IExecutionNode<C>)inputSource.OwnerNode;
							EvaluationSequence<C> evaluationSequence = EnsureSequence(node);
							if (runtime is IExecutionRuntimeInterop { InputNodesMustBeLocal: not false })
							{
								evaluationAnalysisContext.MarkLocal(node);
								evaluationSequence.MarkLocal();
							}
						}
					}
				}
			}
			int num2 = 0;
			foreach (IExecutionNode<C> node3 in base.Nodes)
			{
				num2 = Math.Max(num2, node3.ImpulseCount);
			}
			Span<bool> span = stackalloc bool[num2];
			int num3 = 0;
			foreach (IExecutionNode<C> node4 in base.Nodes)
			{
				node4.IndexInGroup = num3++;
				for (int num4 = 0; num4 < node4.InputCount; num4++)
				{
					if (node4.IsInputConditional(num4))
					{
						IOutput inputSource2 = node4.GetInputSource(num4);
						if (inputSource2 != null && !(inputSource2.OwnerNode is DataImportNode))
						{
							EnsureSequence((IExecutionNode<C>)inputSource2.OwnerNode);
						}
					}
				}
				for (int num5 = 0; num5 < node4.OutputCount; num5++)
				{
					if (!node4.IsOutputImplicit(num5))
					{
						evaluationAnalysisContext.MarkLocal(node4);
						break;
					}
				}
				for (int num6 = 0; num6 < node4.ImpulseCount; num6++)
				{
					span[num6] = false;
				}
				for (int num7 = 0; num7 < node4.ImpulseCount; num7++)
				{
					if (node4.GetImpulseType(num7) != ImpulseType.Continuation)
					{
						span[num7] = true;
					}
				}
				for (int num8 = 0; num8 < node4.OperationCount; num8++)
				{
					if (node4.OperationHasSingleContinuation(num8) && !node4.OperationHasSyncAsyncTransition(num8))
					{
						continue;
					}
					for (int num9 = 0; num9 < node4.ImpulseCount; num9++)
					{
						if (!span[num9] && node4.CanOperationContinueTo(num8, node4.GetImpulseName(num9)))
						{
							span[num9] = true;
						}
					}
				}
				for (int num10 = 0; num10 < node4.ImpulseCount; num10++)
				{
					if (!span[num10])
					{
						continue;
					}
					IOperation impulseTarget = node4.GetImpulseTarget(num10);
					if (!(impulseTarget is ISyncOperation syncOperation2))
					{
						if (!(impulseTarget is IAsyncOperation asyncOperation2))
						{
							if (impulseTarget != null)
							{
								throw new NotImplementedException("Unsupported operation type: " + impulseTarget.GetType());
							}
						}
						else if (!operationSequences.ContainsKey(asyncOperation2))
						{
							operationSequences.Add(asyncOperation2, new AsyncOperationSequence<C>(this, asyncOperation2));
						}
					}
					else if (!operationSequences.ContainsKey(syncOperation2))
					{
						operationSequences.Add(syncOperation2, new SyncOperationSequence<C>(this, syncOperation2));
					}
				}
				if (node4.OperationCount <= 0)
				{
					continue;
				}
				for (int num11 = 0; num11 < node4.InputCount; num11++)
				{
					IOutput inputSource3 = node4.GetInputSource(num11);
					if (inputSource3 != null && !(inputSource3.OwnerNode is DataImportNode))
					{
						EnsureSequence((IExecutionNode<C>)inputSource3.OwnerNode);
					}
				}
			}
			_exportsEvaluationSequence = new EvaluationSequence<C>(this, base.DataExports.ToList());
			evaluationAnalysisContext.BeginSequence(_exportsEvaluationSequence);
			_exportsEvaluationSequence.Analyze(evaluationAnalysisContext);
			foreach (EvaluationSequence<C> item in evaluationSequences.Values.ToList())
			{
				if (evaluationAnalysisContext.BeginSequence(item))
				{
					item.Analyze(evaluationAnalysisContext);
				}
			}
			foreach (IExecutionNode<C> localNode in evaluationAnalysisContext.LocalNodes)
			{
				MapLocalNode(localNode);
			}
			int num12 = localNodes.Count;
			if (num12 > 0)
			{
				num12++;
			}
			_evaluationFlagsSize = (num12 + 31) / 32 * 4;
			EvaluationBuildContext<C> evaluationBuildContext = new EvaluationBuildContext<C>();
			_exportsEvaluationSequence.Build(evaluationBuildContext);
			foreach (KeyValuePair<IExecutionNode<C>, EvaluationSequence<C>> evaluationSequence2 in evaluationSequences)
			{
				evaluationBuildContext.ClearEvaluated();
				evaluationSequence2.Value.Build(evaluationBuildContext);
			}
			foreach (IOperationSequence<C> item2 in operationSequences.Values.ToList())
			{
				item2.Build();
			}
		}

		private void MapLocalNode(IExecutionNode<C> node)
		{
			int num = -1;
			int num2 = 0;
			int num3 = -1;
			int num4 = 0;
			bool flag = true;
			for (int i = 0; i < node.OutputCount; i++)
			{
				DataClass outputTypeClass = node.GetOutputTypeClass(i);
				IOutput output = node.GetOutput(i);
				if (flag && !node.IsOutputImplicit(i))
				{
					flag = false;
				}
				switch (outputTypeClass)
				{
				case DataClass.Value:
				{
					int valueOutputSize = node.GetValueOutputSize(i);
					if (num < 0)
					{
						num = _localValueStackSize;
					}
					num2 += valueOutputSize;
					localValueMapping.Add(output, _localValueStackSize);
					_localValueStackSize += valueOutputSize;
					break;
				}
				case DataClass.Object:
					if (num3 < 0)
					{
						num3 = _localObjectStackSize;
					}
					num4++;
					localValueMapping.Add(output, _localObjectStackSize);
					_localObjectStackSize++;
					break;
				default:
					throw new NotImplementedException("Unsupported output type class: " + outputTypeClass);
				}
			}
			localNodes.Add(node, new LocalNodeData(localNodes.Count + 1, flag, num, num2, num3, num4));
		}

		internal EvaluationSequence<C> EnsureSequence(IExecutionNode<C> node)
		{
			if (evaluationSequences.TryGetValue(node, out var value))
			{
				return value;
			}
			value = new EvaluationSequence<C>(this, node, evaluationSequences.Count);
			evaluationSequences.Add(node, value);
			return value;
		}

		internal EvaluationSequence<C> GetSequence(IExecutionNode<C> node)
		{
			evaluationSequences.TryGetValue(node, out var value);
			return value;
		}

		internal bool IsLocalNode(IExecutionNode<C> node)
		{
			return localNodes.ContainsKey(node);
		}

		internal int GetLocalValueMapping(IOutput output)
		{
			return localValueMapping[output];
		}

		internal LocalNodeData GetStaticNodeMapping(IExecutionNode<C> node)
		{
			return localNodes[node];
		}

		internal void MapSequence(IOperation alias, IOperation existing)
		{
			operationSequences[alias] = operationSequences[existing];
		}

		internal void SetNullSequence(IOperation alias)
		{
			operationSequences[alias] = null;
		}

		public bool IsEvaluationDirty(C context)
		{
			if (_evaluationFlagsSize == 0)
			{
				return false;
			}
			return (context.Values.Read<int>(_localValueStackSize) & 1) != 0;
		}

		public void MarkEvaluationDirty(ExecutionContext context)
		{
			if (_evaluationFlagsSize != 0)
			{
				context.Values.Access<int>(_localValueStackSize) |= 1;
			}
		}

		public bool IsEvaluated(IExecutionNode<C> node, ExecutionContext context)
		{
			GetEvaluationIndex(node, out var pos, out var bitIndex);
			return (context.Values.Read<int>(pos) & (1 << bitIndex)) != 0;
		}

		public void MarkEvaluated(IExecutionNode<C> node, ExecutionContext context)
		{
			GetEvaluationIndex(node, out var pos, out var bitIndex);
			context.Values.Access<int>(pos) |= 1 << bitIndex;
		}

		public void EnsureEvaluated(IOutput output, C context)
		{
			if (!(output.OwnerNode is DataImportNode))
			{
				EvaluationSequence<C> evaluationSequence = evaluationSequences[(IExecutionNode<C>)output.OwnerNode];
				if (!evaluationSequence.IsLocal)
				{
					throw new InvalidOperationException("Cannot ensure evaluated output on sequences that aren't local");
				}
				evaluationSequence.Evaluate(context);
			}
		}

		public void GetEvaluationIndex(IExecutionNode<C> node, out int pos, out int bitIndex)
		{
			int index = localNodes[node].index;
			int num = index / 32;
			bitIndex = index % 32;
			pos = _localValueStackSize + num * 4;
		}

		public void ExecuteImpulseImport(int index, C context)
		{
			if (IsImpulseImportAsync(index))
			{
				throw new InvalidOperationException($"Impulse import {index} is async, cannot execute synchronously!");
			}
			ExecuteSequence((ISyncOperation)GetImpulseImport(index), context);
		}

		public Task ExecuteAsyncImpulseImport(int index, C context)
		{
			if (!IsImpulseImportAsync(index))
			{
				throw new InvalidOperationException($"Impulse import {index} is sync, cannot execute asynchronously!");
			}
			return ExecuteAsyncSequence(GetImpulseImport(index), context);
		}

		public void ExecuteSequence(ISyncOperation operation, C context)
		{
			context.PinFrame();
			IOperation operation2 = operation;
			try
			{
				do
				{
					if (context.AbortExecution)
					{
						throw new ExecutionAbortedException(this, operation, operation2, isAsync: false);
					}
					operation2 = operationSequences[operation2]?.ExecuteSync(context);
				}
				while (operation2 != null);
			}
			catch (KeyNotFoundException)
			{
				throw new Exception($"Sequence is not built for next operation: {operation2}\nOn Group: {base.Group}\nStarting operation: {operation}");
			}
			context.UnpinFrame();
		}

		public async Task ExecuteAsyncSequence(IOperation operation, C context)
		{
			context.PinFrame();
			IOperation next = operation;
			try
			{
				do
				{
					if (context.AbortExecution)
					{
						throw new ExecutionAbortedException(this, operation, next, isAsync: false);
					}
					IOperationSequence<C> operationSequence = operationSequences[next];
					if (operationSequence == null)
					{
						break;
					}
					next = ((!operationSequence.IsAsync) ? operationSequence.ExecuteSync(context) : (await operationSequence.ExecuteAsync(context)));
				}
				while (next != null);
			}
			catch (KeyNotFoundException)
			{
				throw new Exception($"Sequence is not built for next operation: {next}");
			}
			context.UnpinFrame();
		}

		public void ExecuteResumption(SyncResumption resumption, C context)
		{
			context.EnterExecution();
			context.StepIntoFrame(0);
			if (context.CurrentRuntime != this)
			{
				throw new InvalidOperationException("Runtime mistmach! The first frame of the context doesn't match the runtime the resumption is called on.");
			}
			ResumeExecution(resumption, context);
			EndStackFrame(context);
			context.ExitExecution();
		}

		public async Task ExecuteAsyncResumption(AsyncResumption resumption, C context)
		{
			await context.TryEnterAsyncExecution();
			context.StepIntoFrame(0);
			if (context.CurrentRuntime != this)
			{
				throw new InvalidOperationException("Runtime mistmach! The first frame of the context doesn't match the runtime the resumption is called on.");
			}
			await ResumeAsyncExecution(resumption, context);
			EndStackFrame(context);
			context.ExitExecution();
		}

		internal void ResumeExecution(SyncResumption resumption, C context)
		{
			if (!context.CurrentFrameIsTop)
			{
				context.StepIntoFrame((ushort)(context.CurrentFrameIndex + 1));
				IOperation operation = context.CurrentNestedNode.ResumeExecution(resumption, context);
				if (operation != null)
				{
					ExecuteSequence((ISyncOperation)operation, context);
				}
			}
			else
			{
				ExecuteSequence(resumption.Target, context);
			}
		}

		internal async Task ResumeAsyncExecution(AsyncResumption resumption, C context)
		{
			if (!context.CurrentFrameIsTop)
			{
				context.StepIntoFrame((ushort)(context.CurrentFrameIndex + 1));
				IOperation operation = await context.CurrentNestedNode.ResumeAsyncExecution(resumption, context);
				if (operation != null)
				{
					await ExecuteAsyncSequence(operation, context);
				}
			}
			else
			{
				await ExecuteAsyncSequence(resumption.Target, context);
			}
		}

		public void SetValueImport<T>(int index, T value, C context) where T : unmanaged
		{
			context.Values.Write(importsMapping[index], value);
		}

		public void SetObjectImport<T>(int index, T value, C context)
		{
			context.Objects.Write(importsMapping[index], value);
		}

		public void SetValueImport<T>(IOutput output, T value, C context) where T : unmanaged
		{
			context.Values.Write(GetLocalValueMapping(output), value);
		}

		public void SetObjectImport<T>(IOutput output, T value, C context)
		{
			context.Objects.Write(GetLocalValueMapping(output), value);
		}

		public void BeginStackFrame(C context, IExecutionNestedNode nestedNode = null)
		{
			context.AllocateFrame(this, nestedNode);
			if (_evaluationFlagsSize > 0)
			{
				ClearEvaluatedFlags(context);
			}
			if (_localValueStackSize > 0)
			{
				ClearValueLocals(context);
			}
		}

		public void PinStackFrame(C context)
		{
			context.PinFrame();
		}

		public bool ExitStackFrame(C context)
		{
			return context.UnpinFrame();
		}

		internal void ClearValueLocals(C context)
		{
			context.Values.Clear(0, _localValueStackSize);
		}

		internal void ClearObjectLocals(C context)
		{
			context.Objects.Clear(0, _localObjectStackSize);
		}

		internal void ClearEvaluatedFlags(C context)
		{
			context.Values.Clear(_localValueStackSize, _evaluationFlagsSize);
		}

		public void RunEvaluation(C context)
		{
			if (IsEvaluationDirty(context))
			{
				ClearEvaluatedFlags(context);
			}
			_exportsEvaluationSequence.Evaluate(context);
			MarkEvaluationDirty(context);
		}

		public void EndStackFrame(C context)
		{
			if (context.CurrentRuntime == this)
			{
				if (_localObjectStackSize > 0)
				{
					ClearObjectLocals(context);
				}
				context.DeallocateFrame();
				return;
			}
			throw new InvalidOperationException("Runtime mismatch, cannot EndStackFrame for another runtime!");
		}

		internal void InvokeImpulseExport(C context, int index)
		{
			context.InvokeImpulseExport(index);
		}

		public int GetOperationSequenceSteps(IOperation operation)
		{
			return operationSequences[operation].SequenceSteps;
		}

		public string GetEvaluationSequenceDebug(IOutput output)
		{
			return evaluationSequences[(IExecutionNode<C>)output.OwnerNode].ToDebugString();
		}

		public int DoOnEachNode<T>(ExecutionNodeOperation<T, C> action, C context, bool cache = true, HashSet<NodeRuntime> walkedRuntimes = null) where T : INode
		{
			if (walkedRuntimes == null)
			{
				walkedRuntimes = new HashSet<NodeRuntime>();
			}
			if (walkedRuntimes.Count == 0)
			{
				walkedRuntimes.Add(this);
			}
			context.PinFrame();
			int num = 0;
			foreach (T item in GetNodesOfType<T>(cache))
			{
				action(item, context);
				num++;
			}
			foreach (IExecutionNestedNode nestedNode in GetNestedNodes(cache))
			{
				if (walkedRuntimes.Add((NodeRuntime)nestedNode.TargetRuntime))
				{
					num += ((NestedNode<C>)nestedNode).DoOnEachNode(action, context, cache, walkedRuntimes);
					walkedRuntimes.Remove((NodeRuntime)nestedNode.TargetRuntime);
				}
			}
			context.UnpinFrame();
			return num;
		}

		public void MapGlobals(IGlobalValue[] globals, C context)
		{
			if (!context.TryEnterRuntimeOnce(this))
			{
				throw new InvalidOperationException("MapGlobals can only be called at the top runtime, but the runtime has already been entered in given context.");
			}
			try
			{
				BeginStackFrame(context);
				MapGlobalsInternal(globals, context);
			}
			finally
			{
				EndStackFrame(context);
				context.ExitRuntimeOnce(this);
			}
		}

		public void UpdateGlobal<T>(Global<T> global, T value, C context)
		{
			if (!context.TryEnterRuntimeOnce(this))
			{
				throw new InvalidOperationException("Runtime has already been entered once. Make sure the context is empty when calling this.");
			}
			BeginStackFrame(context);
			global.ValueChanged(value, context);
			EndStackFrame(context);
			context.ExitRuntimeOnce(this);
		}

		public void UpdateGlobalsToInitialValue(C context)
		{
			if (!context.TryEnterRuntimeOnce(this))
			{
				throw new InvalidOperationException("Runtime has already been entered once. Make sure the context is empty when calling this.");
			}
			BeginStackFrame(context);
			for (int i = 0; i < base.GlobalsCount; i++)
			{
				GetGlobal(i).UpdateToInitialValue(context);
			}
			EndStackFrame(context);
			context.ExitRuntimeOnce(this);
		}

		public void ResetGlobalsToDefault(C context)
		{
			if (!context.TryEnterRuntimeOnce(this))
			{
				throw new InvalidOperationException("Runtime has already been entered once. Make sure the context is empty when calling this.");
			}
			BeginStackFrame(context);
			for (int i = 0; i < base.GlobalsCount; i++)
			{
				GetGlobal(i).ResetValueToDefault(context);
			}
			EndStackFrame(context);
			context.ExitRuntimeOnce(this);
		}

		internal void MapGlobalsInternal(IGlobalValue[] globals, C context)
		{
			if (globals == null)
			{
				throw new ArgumentNullException("globals");
			}
			if (globals.Length != base.GlobalsCount)
			{
				throw new ArgumentException("Globals array length must match the number of globals");
			}
			context.CurrentScope.MapGlobals(globals);
			foreach (NestedNode<C> nestedNode in GetNestedNodes(cache: true))
			{
				nestedNode.MapGlobals(globals, context);
			}
		}

		public int RestoreSharedScope(ScopePoint rootScope, C context, byte[] values, object[] objects)
		{
			ScopePoint nestedScope = rootScope.GetNestedScope(new ScopeKey(this));
			if (nestedScope == null)
			{
				return 0;
			}
			BeginStackFrame(context);
			int result = RestoreSharedScopeInternal(nestedScope, context, values, objects);
			EndStackFrame(context);
			return result;
		}

		private int RestoreSharedScopeInternal(ScopePoint point, C context, byte[] values, object[] objects)
		{
			int num = 0;
			if (ValueStoreSize > 0 || ObjectStoreSize > 0)
			{
				foreach (IExecutionNode<C> node in base.Nodes)
				{
					if (node.FixedStoresCount == 0)
					{
						continue;
					}
					NodeStoreOffsets? storedOffset = point.GetStoredOffset(node);
					if (storedOffset.HasValue)
					{
						bool flag = false;
						if (node.FixedValueStoresSize > 0)
						{
							int sourceIndex = point.ValuesStoreOffset + storedOffset.Value.valuesOffset;
							Array.Copy(values, sourceIndex, context.SharedScope.ValuesStore, context.CurrentScope.ValuesStoreOffset + node.ValueStoreStartOffset, node.FixedValueStoresSize);
							flag = true;
						}
						if (node.FixedObjectStoresSize > 0)
						{
							int sourceIndex2 = point.ObjectsStoreOffset + storedOffset.Value.objectsOffset;
							Array.Copy(objects, sourceIndex2, context.SharedScope.ObjectsStore, context.CurrentScope.ObjectsStoreOffset + node.ObjectStoreStartOffset, node.FixedObjectStoresSize);
							flag = true;
						}
						if (flag)
						{
							num++;
						}
					}
				}
			}
			foreach (IExecutionNestedNode nestedNode2 in GetNestedNodes(cache: true))
			{
				ScopePoint nestedScope = point.GetNestedScope(new ScopeKey(nestedNode2.TargetRuntime, nestedNode2));
				if (nestedScope != null)
				{
					NestedNode<C> nestedNode = (NestedNode<C>)nestedNode2;
					nestedNode.EnterTargetFrame(context);
					num += nestedNode.Target.RestoreSharedScopeInternal(nestedScope, context, values, objects);
					nestedNode.ExitTargetFrame(context);
				}
			}
			return num;
		}
	}
	public abstract class ImpulseExportWrapper<C> where C : ExecutionContext
	{
		public int Index { get; private set; }

		public bool IsLast { get; internal set; }

		public ImpulseExportWrapper(int index)
		{
			Index = index;
		}

		protected bool ShouldBeContinuation(C context)
		{
			if (IsLast)
			{
				return context.CurrentFramePins == 1;
			}
			return false;
		}
	}
	internal readonly struct LocalNodeData
	{
		public readonly int index;

		public readonly bool isImplicit;

		public readonly int valueStart;

		public readonly int valueSize;

		public readonly int objectStart;

		public readonly int objectSize;

		public LocalNodeData(int index, bool isImplicit, int valueStart, int valueSize, int objectStart, int objectSize)
		{
			this.index = index;
			this.isImplicit = isImplicit;
			this.valueStart = valueStart;
			this.valueSize = valueSize;
			this.objectStart = objectStart;
			this.objectSize = objectSize;
		}

		public override string ToString()
		{
			return $"Index: {index}, Implicit: {isImplicit}, ValueStart: {valueStart}, ValueSize: {valueSize}, ObjectStart: {objectStart}, ObjectSize: {objectSize}";
		}
	}
	internal interface IOperationSequence<C> where C : ExecutionContext
	{
		ExecutionRuntime<C> Runtime { get; }

		bool IsAsync { get; }

		int SequenceSteps { get; }

		void Build();

		IOperation ExecuteSync(C context);

		Task<IOperation> ExecuteAsync(C context);
	}
	internal abstract class OperationSequence<C, O, H> : IOperationSequence<C> where C : ExecutionContext where O : class, IOperation where H : Delegate
	{
		private O origin;

		public ExecutionRuntime<C> Runtime { get; private set; }

		public int SequenceSteps => operationSequence.Count;

		protected List<H> operationSequence { get; private set; } = new List<H>();

		public abstract bool IsAsync { get; }

		public OperationSequence(ExecutionRuntime<C> runtime, O origin)
		{
			Runtime = runtime;
			this.origin = origin;
		}

		void IOperationSequence<C>.Build()
		{
			IOperation operation = origin;
			IOperation initialOperationSkip = null;
			while (operation != null)
			{
				operation = BuildOperationStep(operation, ref initialOperationSkip);
			}
			if (operationSequence.Count != 0 && operationSequence[operationSequence.Count - 1].Target is ImpulseExportWrapper<C> impulseExportWrapper)
			{
				impulseExportWrapper.IsLast = true;
			}
		}

		internal void StitchSequence(Node node, IOperation initialOperationSkip)
		{
			IOperation operation = null;
			for (int i = 0; i < node.ImpulseCount; i++)
			{
				IOperation impulseTarget = node.GetImpulseTarget(i);
				if (impulseTarget != null)
				{
					if (operation != null)
					{
						throw new InvalidProgramException("Cannot have passthrough operations on nodes that have multiple impulse outputs");
					}
					operation = impulseTarget;
				}
			}
			if (operation != null)
			{
				Runtime.MapSequence(initialOperationSkip, operation);
			}
			else
			{
				Runtime.SetNullSequence(initialOperationSkip);
			}
		}

		internal IOperation BuildOperationStep(IOperation operation, ref IOperation initialOperationSkip)
		{
			H handler = GetHandler(operation);
			if (handler != null)
			{
				operationSequence.Add(handler);
				initialOperationSkip = null;
			}
			else if (initialOperationSkip == null)
			{
				initialOperationSkip = operation;
			}
			Node ownerNode = operation.OwnerNode;
			if (ownerNode == null || operation is ImpulseExport)
			{
				return null;
			}
			int index = operation.FindLinearOperationIndex();
			if (!ownerNode.OperationHasSingleContinuation(index))
			{
				if (handler == null)
				{
					StitchSequence(ownerNode, initialOperationSkip);
				}
				return null;
			}
			for (int i = 0; i < ownerNode.FixedImpulseCount; i++)
			{
				if (ownerNode.GetImpulseType(i) == ImpulseType.Continuation && ownerNode.CanOperationContinueTo(index, ownerNode.GetImpulseName(i)))
				{
					IOperation impulseTarget = ownerNode.GetImpulseTarget(i);
					if (impulseTarget != null && IsSupported(impulseTarget))
					{
						return impulseTarget;
					}
				}
			}
			for (int j = 0; j < ownerNode.DynamicImpulseCount; j++)
			{
				if (!ownerNode.CanOperationContinueTo(index, ownerNode.GetImpulseListName(j)))
				{
					continue;
				}
				IImpulseList impulseList = ownerNode.GetImpulseList(j);
				for (int k = 0; k < impulseList.Count; k++)
				{
					if (impulseList.GetImpulseType(k) == ImpulseType.Continuation)
					{
						IOperation impulseTarget2 = impulseList.GetImpulseTarget(k);
						if (impulseTarget2 != null && IsSupported(impulseTarget2))
						{
							return impulseTarget2;
						}
					}
				}
			}
			if (handler == null)
			{
				StitchSequence(ownerNode, initialOperationSkip);
			}
			return null;
		}

		protected virtual bool IsSupported(IOperation operation)
		{
			if (!(operation is O))
			{
				return operation is ContinuationExport;
			}
			return true;
		}

		protected abstract H GetHandler(IOperation operation);

		public virtual IOperation ExecuteSync(C context)
		{
			throw new InvalidOperationException("Sync execution is not supported on this sequence");
		}

		public virtual Task<IOperation> ExecuteAsync(C context)
		{
			throw new InvalidOperationException("Async execution is not supported on this sequence");
		}
	}
	internal class SyncOperationSequence<C> : OperationSequence<C, ISyncOperation, ExecutionOperationHandler<C>> where C : ExecutionContext
	{
		public override bool IsAsync => false;

		public SyncOperationSequence(ExecutionRuntime<C> runtime, ISyncOperation origin)
			: base(runtime, origin)
		{
		}

		protected override ExecutionOperationHandler<C> GetHandler(IOperation operation)
		{
			if (!(operation is Operation operation2))
			{
				if (!(operation is SyncOperationList.Operation operation3))
				{
					if (!(operation is MixedOperationList.Operation operation4))
					{
						if (!(operation is IExecutionOperationNode executionOperationNode))
						{
							if (!(operation is CallExport callExport))
							{
								if (operation is ContinuationExport continuationExport)
								{
									return new ContinuationExportWrapper<C>(continuationExport.Index).Execute;
								}
								throw new ArgumentException("Could not extract handler from: " + operation);
							}
							return new CallExportWrapper<C>(callExport.Index).Execute;
						}
						return executionOperationNode.GetHandler<C>();
					}
					if (operation4.IsAsync)
					{
						throw new InvalidOperationException("Target list operation is async, cannot get handler for SyncOperationSequence");
					}
					return ((IExecutionNode)operation4.OwnerNode).GetDynamicOperationHandler<C>(operation4.List.Index, operation4.Index);
				}
				return ((IExecutionNode)operation3.OwnerNode).GetDynamicOperationHandler<C>(operation3.List.Index, operation3.Index);
			}
			IExecutionNode executionNode = (IExecutionNode)operation2.OwnerNode;
			if (executionNode.IsOperationPassthrough(operation2.Index))
			{
				return null;
			}
			return executionNode.GetOperationHandler<C>(operation2.Index);
		}

		public override IOperation ExecuteSync(C context)
		{
			IOperation operation = null;
			foreach (ExecutionOperationHandler<C> item in base.operationSequence)
			{
				if (base.Runtime.IsEvaluationDirty(context))
				{
					base.Runtime.ClearEvaluatedFlags(context);
				}
				operation = item(context);
				if (operation == null)
				{
					return null;
				}
			}
			return operation;
		}
	}
	public interface IExecutionNestedNode : INestedNode, INode
	{
		new IExecutionRuntime TargetRuntime { get; }

		IOperation ResumeExecution(SyncResumption resumption, ExecutionContext context);

		Task<IOperation> ResumeAsyncExecution(AsyncResumption resumption, ExecutionContext context);
	}
	[NodeOverload("Core.Nest")]
	public class NestedNode<C> : VoidNode<C>, IExecutionNestedNode, INestedNode, INode where C : ExecutionContext
	{
		private abstract class InputMapping
		{
			public abstract void Map(ArgumentList inputList, IOutput output);

			public abstract void SetImport(int index, ExecutionRuntime<C> group, C context, int valueOffset, int objectOffset);

			public abstract void EvaluateInput(ExecutionRuntime<C> group, C context);

			public abstract void SetFromStack(int index, ExecutionRuntime<C> group, C context);
		}

		private abstract class OutputMapping
		{
			public abstract void Map(OutputList outputList, IOutput output);

			public abstract void ExtractExport(int index, ExecutionRuntime<C> runtime, C context);
		}

		private abstract class InputMapping<T> : InputMapping
		{
		}

		private abstract class OutputMapping<T> : OutputMapping
		{
		}

		private class ValueInputMapping<T> : InputMapping<T> where T : unmanaged
		{
			private IInput<T> input;

			private ValueOutput<T> import;

			public override void Map(ArgumentList inputList, IOutput output)
			{
				input = inputList.AddInput<T>();
				import = (ValueOutput<T>)output;
			}

			public override void SetImport(int index, ExecutionRuntime<C> group, C context, int valueOffset, int objectOffset)
			{
				group.SetValueImport(index, context.ReadValue<T>(index, valueOffset), context);
			}

			public override void EvaluateInput(ExecutionRuntime<C> group, C context)
			{
				if (input.Source == null)
				{
					context.Values.Push(default(T));
				}
				else
				{
					group.EvaluateValueToStack<T>(input.Source, context);
				}
			}

			public override void SetFromStack(int index, ExecutionRuntime<C> group, C context)
			{
				group.SetValueImport(index, context.Values.Pop<T>(), context);
			}
		}

		private class ObjectInputMapping<T> : InputMapping<T>
		{
			private IInput<T> input;

			private ObjectOutput<T> import;

			public override void Map(ArgumentList inputList, IOutput output)
			{
				input = inputList.AddInput<T>();
				import = (ObjectOutput<T>)output;
			}

			public override void SetImport(int index, ExecutionRuntime<C> group, C context, int valueOffset, int objectOffset)
			{
				group.SetObjectImport(index, context.ReadObject<T>(index, objectOffset), context);
			}

			public override void EvaluateInput(ExecutionRuntime<C> group, C context)
			{
				if (input.Source == null)
				{
					context.Objects.Push(default(T));
				}
				else
				{
					group.EvaluateObjectToStack<T>(input.Source, context);
				}
			}

			public override void SetFromStack(int index, ExecutionRuntime<C> group, C context)
			{
				group.SetObjectImport(index, context.Objects.Pop<T>(), context);
			}
		}

		private class ValueOutputMapping<T> : OutputMapping<T> where T : unmanaged
		{
			private ValueOutput<T> output;

			private IOutput export;

			public override void Map(OutputList outputList, IOutput output)
			{
				this.output = outputList.AddValueOutput<T>();
				export = output;
			}

			public override void ExtractExport(int index, ExecutionRuntime<C> runtime, C context)
			{
				runtime.SetValue(output, context.Values.Pop<T>(), context);
			}
		}

		private class ObjectOutputMapping<T> : OutputMapping<T>
		{
			private ObjectOutput<T> output;

			private IOutput export;

			public override void Map(OutputList outputList, IOutput output)
			{
				this.output = outputList.AddObjectOutput<T>();
				export = output;
			}

			public override void ExtractExport(int index, ExecutionRuntime<C> runtime, C context)
			{
				runtime.SetObject(output, context.Objects.Pop<T>(), context);
			}
		}

		private static readonly Type _valueInputMapping = typeof(ValueInputMapping<>);

		private static readonly Type _objectInputMapping = typeof(ObjectInputMapping<>);

		private static readonly Type _valueOutputMapping = typeof(ValueOutputMapping<>);

		private static readonly Type _objectOutputMapping = typeof(ObjectOutputMapping<>);

		public ExecutionRuntime<C> Target;

		public readonly ArgumentList Inputs;

		public readonly OutputList Outputs;

		public readonly MixedOperationList Operations;

		public readonly CallList Impulses;

		public readonly GlobalRefList GlobalRefs;

		private List<InputMapping> _inputMappings = new List<InputMapping>();

		private List<OutputMapping> _outputMappings = new List<OutputMapping>();

		NodeGroup INestedNode.TargetGroup => Target?.Group;

		NodeRuntime INestedNode.TargetRuntime => Target;

		IExecutionRuntime IExecutionNestedNode.TargetRuntime => Target;

		public override bool CanBeEvaluated
		{
			get
			{
				if (Operations.Count == 0)
				{
					return Impulses.Count == 0;
				}
				return false;
			}
		}

		public override bool IsInputConditional(int index)
		{
			return !CanBeEvaluated;
		}

		public IOutput GetTargetExport(IOutput output)
		{
			if (Target == null)
			{
				throw new InvalidOperationException("Cannot get exports when there's no target node group");
			}
			IListOutput listOutput = (IListOutput)output;
			return Target.GetValueExport(listOutput.Index);
		}

		public IOutput GetImportSource(IOutput import)
		{
			if (Target == null)
			{
				throw new InvalidOperationException("Cannot get imports when there's no target node group");
			}
			IListOutput listOutput = (IListOutput)import;
			return Inputs.GetInputSource(listOutput.Index);
		}

		public void MapTarget()
		{
			if (Target == null)
			{
				throw new ArgumentNullException("Target");
			}
			Inputs.Clear();
			Outputs.Clear();
			Impulses.Clear();
			Operations.Clear();
			GlobalRefs.Clear();
			_inputMappings.Clear();
			_outputMappings.Clear();
			for (int i = 0; i < Target.DataImportsCount; i++)
			{
				IOutput import = Target.GetImport(i);
				Type importType = Target.GetImportType(i);
				InputMapping inputMapping = (InputMapping)Activator.CreateInstance(Target.GetImportDataClass(i) switch
				{
					DataClass.Value => _valueInputMapping.MakeGenericType(typeof(C), importType), 
					DataClass.Object => _objectInputMapping.MakeGenericType(typeof(C), importType), 
					_ => throw new NotImplementedException("Unsupported data class"), 
				});
				inputMapping.Map(Inputs, import);
				_inputMappings.Add(inputMapping);
			}
			for (int j = 0; j < Target.DataExportsCount; j++)
			{
				IOutput valueExport = Target.GetValueExport(j);
				OutputMapping outputMapping = (OutputMapping)Activator.CreateInstance(valueExport.OutputDataClass switch
				{
					DataClass.Value => _valueOutputMapping.MakeGenericType(typeof(C), valueExport.OutputType), 
					DataClass.Object => _objectOutputMapping.MakeGenericType(typeof(C), valueExport.OutputType), 
					_ => throw new NotImplementedException("Unsupported data class"), 
				});
				outputMapping.Map(Outputs, valueExport);
				_outputMappings.Add(outputMapping);
			}
			for (int k = 0; k < Target.ImpulseExportCount; k++)
			{
				Impulses.AddImpulse();
			}
			for (int l = 0; l < Target.ImpulseImportsCount; l++)
			{
				if (Target.IsImpulseImportAsync(l))
				{
					Operations.AddAsyncOperation();
				}
				else
				{
					Operations.AddSyncOperation();
				}
			}
			for (int m = 0; m < Target.GlobalsCount; m++)
			{
				Target.GetGlobal(m).AddMatchingTypeToList(GlobalRefs);
			}
		}

		internal void EnterTargetFrame(C context)
		{
			ExecutionRuntime<C> executionRuntime = (ExecutionRuntime<C>)context.CurrentRuntime;
			for (int i = 0; i < _inputMappings.Count; i++)
			{
				_inputMappings[i].EvaluateInput(executionRuntime, context);
			}
			Target.BeginStackFrame(context, this);
			for (int num = _inputMappings.Count - 1; num >= 0; num--)
			{
				_inputMappings[num].SetFromStack(num, Target, context);
			}
			context.PushImpulseExportHandler(OnImpulseExport);
		}

		internal void ExitTargetFrame(C context)
		{
			context.PopImpulseExportHandler();
			Target.RunEvaluation(context);
			FinishNestedFrame(context);
		}

		protected IOperation DoOperations(C context, int index)
		{
			EnterTargetFrame(context);
			Target.ExecuteSequence((ISyncOperation)Target.GetImpulseImport(index), context);
			return FinishOperations(context);
		}

		protected async Task<IOperation> DoOperationsAsync(C context, int index)
		{
			EnterTargetFrame(context);
			if (Target.IsImpulseImportAsync(index))
			{
				await Target.ExecuteAsyncSequence(Target.GetImpulseImport(index), context);
			}
			else
			{
				Target.ExecuteSequence((ISyncOperation)Target.GetImpulseImport(index), context);
			}
			return FinishOperations(context);
		}

		private void OnGlobalRefsChanged<T>(int index, T value, C context)
		{
			if (context.TryEnterRuntimeOnce(Target))
			{
				EnterTargetFrame(context);
				Target.GetGlobal<T>(index).ValueChanged(value, context);
				ExitTargetFrame(context);
				context.ExitRuntimeOnce(Target);
			}
		}

		internal void MapGlobals(IGlobalValue[] scopeGlobals, C context)
		{
			if (GlobalRefs.Count == 0 || !context.TryEnterRuntimeOnce(Target))
			{
				return;
			}
			IGlobalValue[] array = new IGlobalValue[GlobalRefs.Count];
			for (int i = 0; i < GlobalRefs.Count; i++)
			{
				Global untypedGlobal = GlobalRefs.GetUntypedGlobalRef(i).UntypedGlobal;
				if (untypedGlobal != null)
				{
					array[i] = scopeGlobals[untypedGlobal.Index];
				}
			}
			EnterTargetFrame(context);
			Target.MapGlobalsInternal(array, context);
			ExitTargetFrame(context);
			context.ExitRuntimeOnce(Target);
		}

		private IOperation FinishOperations(C context)
		{
			ExitTargetFrame(context);
			if (context.ImpulseExport >= 0)
			{
				IOperation impulseTarget = Impulses.GetImpulseTarget(context.ImpulseExport);
				context.ImpulseExport = -1;
				return impulseTarget;
			}
			return null;
		}

		public IOperation ResumeExecution(SyncResumption resumption, ExecutionContext context)
		{
			C context2 = (C)context;
			context.PushImpulseExportHandler(OnImpulseExport);
			Target.ResumeExecution(resumption, context2);
			return FinishOperations(context2);
		}

		public async Task<IOperation> ResumeAsyncExecution(AsyncResumption resumption, ExecutionContext context)
		{
			C typedContext = (C)context;
			context.PushImpulseExportHandler(OnImpulseExport);
			await Target.ResumeAsyncExecution(resumption, typedContext);
			return FinishOperations(typedContext);
		}

		internal int DoOnEachNode<T>(ExecutionNodeOperation<T, C> action, C context, bool cache, HashSet<NodeRuntime> walkedRuntimes) where T : INode
		{
			EnterTargetFrame(context);
			int result = Target.DoOnEachNode(action, context, cache, walkedRuntimes);
			ExitTargetFrame(context);
			return result;
		}

		private void OnImpulseExport(ExecutionContext context, int index)
		{
			C context2 = (C)context;
			context.PopImpulseExportHandler();
			Target.RunEvaluation(context2);
			ushort index2 = context.ReturnToPreviousFrame();
			ExtractExports(context2);
			Impulses.GetImpulse(index).Execute(context);
			context.PushImpulseExportHandler(OnImpulseExport);
			context.StepIntoFrame(index2);
		}

		protected override void ComputeOutputs(C context)
		{
			ExecutionContext.StackLayout stackLayout = context.stackLayout;
			EnterNestedFrame(context);
			Target.RunEvaluation(context);
			FinishNestedFrame(context);
			context.stackLayout = stackLayout;
		}

		private void EnterNestedFrame(C context)
		{
			int bottom = context.Values.Bottom;
			int bottom2 = context.Objects.Bottom;
			Target.BeginStackFrame(context, this);
			int valueOffset = context.Values.Bottom - bottom;
			int objectOffset = context.Objects.Bottom - bottom2;
			for (int i = 0; i < _inputMappings.Count; i++)
			{
				_inputMappings[i].SetImport(i, Target, context, valueOffset, objectOffset);
			}
		}

		private void FinishNestedFrame(C context)
		{
			Target.EndStackFrame(context);
			ExtractExports(context);
		}

		private void ExtractExports(C context)
		{
			ExecutionRuntime<C> runtime = (ExecutionRuntime<C>)context.CurrentRuntime;
			for (int num = _outputMappings.Count - 1; num >= 0; num--)
			{
				_outputMappings[num].ExtractExport(num, runtime, context);
			}
		}

		public override string ToString()
		{
			return $"Nested Node (Target: {Target?.Group?.Name}, Instance: {GetHashCode()})";
		}

		public NestedNode()
		{
			((NestedNode<>)(object)this).Inputs = new ArgumentList();
			((NestedNode<>)(object)this).Outputs = new OutputList(this);
			((NestedNode<>)(object)this).Operations = new MixedOperationList(this, 0);
			((NestedNode<>)(object)this).Impulses = new CallList();
			((NestedNode<>)(object)this).GlobalRefs = new GlobalRefList(this, 0);
		}
	}
	public static class NestedNodeHelper
	{
		public static NestedNode<C> AddNestedNode<C>(this ExecutionRuntime<C> runtime, ExecutionRuntime<C> target) where C : ExecutionContext
		{
			NestedNode<C> nestedNode = runtime.AddNode<NestedNode<C>>();
			nestedNode.Target = target;
			nestedNode.MapTarget();
			return nestedNode;
		}
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes
{
	[NodeCategory("Flow")]
	[NodeName("Time Delay", false)]
	[NodeOverload("Core.Delay")]
	public abstract class DelayTime : AsyncActionFlowNode<ExecutionContext>
	{
		public AsyncCall OnTriggered;

		protected virtual void BeforeDelay(ExecutionContext context)
		{
		}

		protected abstract TimeSpan GetDuration(ExecutionContext context);

		protected override async Task Do(ExecutionContext context)
		{
			TimeSpan duration = GetDuration(context);
			Task delayTask = Task.Delay(duration);
			BeforeDelay(context);
			await OnTriggered.ExecuteAsync(context);
			await delayTask;
		}
	}
	[NodeName("Delay", false)]
	public class DelayTimeSpan : DelayTime
	{
		public ValueInput<TimeSpan> Duration;

		protected override TimeSpan GetDuration(ExecutionContext context)
		{
			return Duration.Evaluate(context);
		}
	}
	[NodeName("Delay", false)]
	public class DelaySecondsInt : DelayTime
	{
		public ValueInput<int> Duration;

		protected override TimeSpan GetDuration(ExecutionContext context)
		{
			return TimeSpan.FromSeconds(Duration.Evaluate(context, 0));
		}
	}
	[NodeName("Delay", false)]
	public class DelaySecondsFloat : DelayTime
	{
		public ValueInput<float> Duration;

		protected override TimeSpan GetDuration(ExecutionContext context)
		{
			return TimeSpan.FromSeconds(Duration.Evaluate(context, 0f));
		}
	}
	[NodeName("Delay", false)]
	public class DelaySecondsDouble : DelayTime
	{
		public ValueInput<double> Duration;

		protected override TimeSpan GetDuration(ExecutionContext context)
		{
			return TimeSpan.FromSeconds(Duration.Evaluate(context, 0.0));
		}
	}
	[NodeName("Time Delay with Data", false)]
	[NodeOverload("Core.DelayWithData")]
	public abstract class DelayTimeWithValue<T> : DelayTime where T : unmanaged
	{
		public ValueInput<T> Value;

		public readonly ValueOutput<T> DelayedValue;

		protected override void BeforeDelay(ExecutionContext context)
		{
			DelayedValue.Write(Value.Evaluate(context), context);
		}

		protected DelayTimeWithValue()
		{
			((DelayTimeWithValue<>)(object)this).DelayedValue = new ValueOutput<T>(this);
		}
	}
	public class DelayWithValueTimeSpan<T> : DelayTimeWithValue<T> where T : unmanaged
	{
		public ValueInput<TimeSpan> Duration;

		protected override TimeSpan GetDuration(ExecutionContext context)
		{
			return Duration.Evaluate(context);
		}
	}
	public class DelayWithValueSecondsInt<T> : DelayTimeWithValue<T> where T : unmanaged
	{
		public ValueInput<int> Duration;

		protected override TimeSpan GetDuration(ExecutionContext context)
		{
			return TimeSpan.FromSeconds(Duration.Evaluate(context, 0));
		}
	}
	public class DelayWithValueSecondsFloat<T> : DelayTimeWithValue<T> where T : unmanaged
	{
		public ValueInput<float> Duration;

		protected override TimeSpan GetDuration(ExecutionContext context)
		{
			return TimeSpan.FromSeconds(Duration.Evaluate(context, 0f));
		}
	}
	public class DelayWithValueSecondsDouble<T> : DelayTimeWithValue<T> where T : unmanaged
	{
		public ValueInput<double> Duration;

		protected override TimeSpan GetDuration(ExecutionContext context)
		{
			return TimeSpan.FromSeconds(Duration.Evaluate(context, 0.0));
		}
	}
	[NodeName("Time Delay with Data", false)]
	[NodeOverload("Core.DelayWithData")]
	public abstract class DelayTimeWithObject<T> : DelayTime
	{
		public ObjectInput<T> Value;

		public readonly ObjectOutput<T> DelayedValue;

		protected override void BeforeDelay(ExecutionContext context)
		{
			DelayedValue.Write(Value.Evaluate(context), context);
		}

		protected DelayTimeWithObject()
		{
			((DelayTimeWithObject<>)(object)this).DelayedValue = new ObjectOutput<T>(this);
		}
	}
	public class DelayWithObjectTimeSpan<T> : DelayTimeWithObject<T>
	{
		public ValueInput<TimeSpan> Duration;

		protected override TimeSpan GetDuration(ExecutionContext context)
		{
			return Duration.Evaluate(context);
		}
	}
	public class DelayWithObjectSecondsInt<T> : DelayTimeWithObject<T>
	{
		public ValueInput<int> Duration;

		protected override TimeSpan GetDuration(ExecutionContext context)
		{
			return TimeSpan.FromSeconds(Duration.Evaluate(context, 0));
		}
	}
	public class DelayWithObjectSecondsFloat<T> : DelayTimeWithObject<T>
	{
		public ValueInput<float> Duration;

		protected override TimeSpan GetDuration(ExecutionContext context)
		{
			return TimeSpan.FromSeconds(Duration.Evaluate(context, 0f));
		}
	}
	public class DelayWithObjectSecondsDouble<T> : DelayTimeWithObject<T>
	{
		public ValueInput<double> Duration;

		protected override TimeSpan GetDuration(ExecutionContext context)
		{
			return TimeSpan.FromSeconds(Duration.Evaluate(context, 0.0));
		}
	}
	[NodeCategory("Core")]
	[NodeOverload("Core.Box")]
	public class Box<T> : ObjectFunctionNode<ExecutionContext, object> where T : unmanaged
	{
		public ValueArgument<T> Input;

		protected override object Compute(ExecutionContext context)
		{
			return 0.ReadValue<T>(context);
		}
	}
	[NodeCategory("Core")]
	[NodeOverload("Core.Constant")]
	public class ValueConstant<T> : ValueFunctionNode<ExecutionContext, T> where T : unmanaged
	{
		public T Value;

		protected override T Compute(ExecutionContext context)
		{
			return Value;
		}
	}
	[NodeCategory("Core")]
	[NodeOverload("Core.Constant")]
	public class ObjectConstant<T> : ObjectFunctionNode<ExecutionContext, T>
	{
		public T Value;

		protected override T Compute(ExecutionContext context)
		{
			return Value;
		}
	}
	public static class ConstantHelper
	{
		public static ValueConstant<T> AddValueConstant<T>(this ExecutionRuntime<ExecutionContext> runtime, T value) where T : unmanaged
		{
			return runtime.AddValueConstant<ExecutionContext, T>(value);
		}

		public static ObjectConstant<T> AddObjectConstant<T>(this ExecutionRuntime<ExecutionContext> runtime, T value)
		{
			return runtime.AddObjectConstant<ExecutionContext, T>(value);
		}

		public static ValueConstant<T> AddValueConstant<C, T>(this ExecutionRuntime<C> runtime, T value) where C : ExecutionContext where T : unmanaged
		{
			ValueConstant<T> valueConstant = runtime.AddNode<ValueConstant<T>>();
			valueConstant.Value = value;
			return valueConstant;
		}

		public static ObjectConstant<T> AddObjectConstant<C, T>(this ExecutionRuntime<C> runtime, T value) where C : ExecutionContext
		{
			ObjectConstant<T> objectConstant = runtime.AddNode<ObjectConstant<T>>();
			objectConstant.Value = value;
			return objectConstant;
		}
	}
	[NodeCategory("Utility")]
	[NodeName("Continuous Relay", false)]
	[ContinuouslyChanging]
	[NodeOverload("Core.ContinuouslyChangingRelay")]
	[OldNodeName("ContinouslyChangingValueRelay")]
	public class ContinuouslyChangingValueRelay<T> : ValueFunctionNode<ExecutionContext, T> where T : unmanaged
	{
		public ValueArgument<T> Input;

		public override bool IsPassthrough => true;

		protected override T Compute(ExecutionContext context)
		{
			return 0.ReadValue<T>(context);
		}
	}
	[NodeCategory("Utility")]
	[NodeName("Continuous Relay", false)]
	[ContinuouslyChanging]
	[NodeOverload("Core.ContinuouslyChangingRelay")]
	[OldNodeName("ContinouslyChangingObjectRelay")]
	public class ContinuouslyChangingObjectRelay<T> : ObjectFunctionNode<ExecutionContext, T>
	{
		public ObjectArgument<T> Input;

		public override bool IsPassthrough => true;

		protected override T Compute(ExecutionContext context)
		{
			return 0.ReadObject<T>(context);
		}
	}
	[NodeCategory("Utility")]
	[NodeOverload("Core.Demultiplex")]
	public class ValueDemultiplex<T> : VoidNode<ExecutionContext> where T : unmanaged
	{
		public ValueArgument<T> Value;

		public ValueArgument<T> DefaultValue;

		public ValueArgument<int> Index;

		public readonly ValueOutputList<T> ValueOutputs;

		protected override void ComputeOutputs(ExecutionContext context)
		{
			T val = 0.ReadValue<T>(context);
			T val2 = 1.ReadValue<T>(context);
			int num = 2.ReadValue<int>(context);
			for (int i = 0; i < ValueOutputs.Count; i++)
			{
				ValueOutputs.GetOutput(i).Write((i == num) ? val : val2, context);
			}
		}

		public ValueDemultiplex()
		{
			((ValueDemultiplex<>)(object)this).ValueOutputs = new ValueOutputList<T>(this);
		}
	}
	[NodeCategory("Utility")]
	[NodeOverload("Core.Demultiplex")]
	public class ObjectDemultiplex<T> : VoidNode<ExecutionContext>
	{
		public ObjectArgument<T> Value;

		public ObjectArgument<T> DefaultValue;

		public ValueArgument<int> Index;

		public readonly ObjectOutputList<T> ValueOutputs;

		protected override void ComputeOutputs(ExecutionContext context)
		{
			T val = 0.ReadObject<T>(context);
			T val2 = 1.ReadObject<T>(context);
			int num = 2.ReadValue<int>(context);
			for (int i = 0; i < ValueOutputs.Count; i++)
			{
				ValueOutputs.GetOutput(i).Write((i == num) ? val : val2, context);
			}
		}

		public ObjectDemultiplex()
		{
			((ObjectDemultiplex<>)(object)this).ValueOutputs = new ObjectOutputList<T>(this);
		}
	}
	[NodeCategory("Core")]
	[NodeName("Eval Point", false)]
	[NodeOverload("Core.EvaluationPoint")]
	public class ValueEvaluationPoint<T> : VoidNode<ExecutionContext> where T : unmanaged
	{
		public ValueInput<T> Input;
	}
	[NodeCategory("Core")]
	[NodeName("Eval Point", false)]
	[NodeOverload("Core.EvaluationPoint")]
	public class ObjectEvaluationPoint<T> : VoidNode<ExecutionContext>
	{
		public ObjectInput<T> Input;
	}
	public delegate void DisplayHandler<C, T>(T value, C context);
	public delegate void ImpulseDisplayHandler<C>(C context);
	[NodeCategory("Core")]
	[NodeName("Display", false)]
	[NodeOverload("Core.Display")]
	public class ExternalValueDisplay<C, T> : VoidNode<C>, IExecutionChangeListener<C> where C : ExtendedExecutionContext<C> where T : unmanaged
	{
		public ValueInput<T> Input;

		public DisplayHandler<C, T> OnDisplay;

		public bool InputListensToChanges { get; set; } = true;

		public void Changed(C context)
		{
			T value = Input.Evaluate(context);
			OnDisplay?.Invoke(value, context);
		}
	}
	[NodeCategory("Core")]
	[NodeName("Display", false)]
	[NodeOverload("Core.Display")]
	public class ExternalObjectDisplay<C, T> : VoidNode<C>, IExecutionChangeListener<C> where C : ExtendedExecutionContext<C>
	{
		public ObjectInput<T> Input;

		public DisplayHandler<C, T> OnDisplay;

		public bool InputListensToChanges { get; set; } = true;

		public void Changed(C context)
		{
			T value = Input.Evaluate(context);
			OnDisplay?.Invoke(value, context);
		}
	}
	[NodeCategory("Core")]
	[NodeName("Pulse Display", false)]
	public class ExternalImpulseDisplay<C> : ActionNode<C> where C : ExecutionContext
	{
		public ImpulseDisplayHandler<C> OnPulsed;

		protected override IOperation Run(C context)
		{
			OnPulsed?.Invoke(context);
			return null;
		}
	}
	public class ExternalImpulseDisplay : ExternalImpulseDisplay<ExecutionContext>
	{
	}
	[NodeCategory("Core")]
	[NodeName("Input", false)]
	[ChangeSource]
	[NodeOverload("Core.Input")]
	public class ExternalValueInput<C, T> : ValueFunctionNode<C, T>, IScopeEventListener<C> where C : ExtendedExecutionContext<C> where T : unmanaged
	{
		private Dictionary<NodeContextPath, ExecutionChangesDispatcher<C>> _changeListeners = new Dictionary<NodeContextPath, ExecutionChangesDispatcher<C>>();

		public T Value { get; private set; }

		public void AddedToScope(C context)
		{
			NodeContextPath key = context.CaptureContextPath();
			ExecutionChangesDispatcher<C> changes = context.Changes;
			_changeListeners.Add(key, changes);
		}

		public void RemovedFromScope(C context)
		{
			_changeListeners.Remove(context.CaptureContextPath());
		}

		public void SetValue(T value)
		{
			Value = value;
			foreach (KeyValuePair<NodeContextPath, ExecutionChangesDispatcher<C>> changeListener in _changeListeners)
			{
				changeListener.Value.OutputChanged(new ElementPath<IOutput>(this, changeListener.Key));
			}
		}

		protected override T Compute(C context)
		{
			return Value;
		}
	}
	[NodeCategory("Core")]
	[NodeName("Input", false)]
	[ChangeSource]
	[NodeOverload("Core.Input")]
	public class ExternalObjectInput<C, T> : ObjectFunctionNode<C, T>, IScopeEventListener<C> where C : ExtendedExecutionContext<C>
	{
		private Dictionary<NodeContextPath, ExecutionChangesDispatcher<C>> _changeListeners = new Dictionary<NodeContextPath, ExecutionChangesDispatcher<C>>();

		public T Value { get; private set; }

		public void AddedToScope(C context)
		{
			NodeContextPath key = context.CaptureContextPath();
			ExecutionChangesDispatcher<C> changes = context.Changes;
			_changeListeners.Add(key, changes);
		}

		public void RemovedFromScope(C context)
		{
			_changeListeners.Remove(context.CaptureContextPath());
		}

		public void SetValue(T value)
		{
			Value = value;
			foreach (KeyValuePair<NodeContextPath, ExecutionChangesDispatcher<C>> changeListener in _changeListeners)
			{
				changeListener.Value.OutputChanged(new ElementPath<IOutput>(this, changeListener.Key));
			}
		}

		protected override T Compute(C context)
		{
			return Value;
		}
	}
	[NodeCategory("Core")]
	[NodeName("FilterInput", false)]
	[ChangeSource]
	[NodeOverload("Core.FilterInput")]
	public class ExternalValueInputWithFilter<C, T> : ExternalValueInput<C, T> where C : ExtendedExecutionContext<C> where T : unmanaged
	{
		public Func<T, T> Filter;

		protected override T Compute(C context)
		{
			return Filter(base.Value);
		}
	}
	[NodeCategory("Core")]
	[NodeName("FilterInput", false)]
	[ChangeSource]
	[NodeOverload("Core.FilterInput")]
	public class ExternalObjectInputWithFilter<C, T> : ExternalObjectInput<C, T> where C : ExtendedExecutionContext<C>
	{
		public Func<T, T> Filter;

		protected override T Compute(C context)
		{
			return Filter(base.Value);
		}
	}
	[NodeCategory("Core")]
	[NodeName("Call", false)]
	public class ExternalCall<C> : VoidNode<C> where C : ExecutionContext
	{
		public Call Target;

		public void Execute(C context)
		{
			Target.Execute(context);
		}
	}
	[NodeCategory("Core")]
	[NodeName("Async Call", false)]
	public class ExternalAsyncCall<C> : VoidNode<C> where C : ExecutionContext
	{
		public AsyncCall Target;

		public Task Execute(C context)
		{
			return Target.ExecuteAsync(context);
		}
	}
	[NodeCategory("Flow/Async")]
	[NodeName("Async For", false)]
	public class AsyncFor : AsyncActionNode<ExecutionContext>
	{
		public ValueInput<int> Count;

		public ValueInput<bool> Reverse;

		public AsyncCall LoopStart;

		public AsyncCall LoopIteration;

		public Continuation LoopEnd;

		public readonly ValueOutput<int> Iteration;

		public override bool CanBeEvaluated => false;

		protected override async Task<IOperation> RunAsync(ExecutionContext context)
		{
			int _count = Count.Evaluate(context, 0);
			bool _reverse = Reverse.Evaluate(context, defaultValue: false);
			await LoopStart.ExecuteAsync(context);
			if (_count > 0)
			{
				if (_reverse)
				{
					for (int i = _count - 1; i >= 0; i--)
					{
						if (context.AbortExecution)
						{
							throw new ExecutionAbortedException(base.Runtime as IExecutionRuntime, this, LoopIteration.Target, isAsync: true);
						}
						Iteration.Write(i, context);
						await LoopIteration.ExecuteAsync(context);
					}
				}
				else
				{
					for (int i = 0; i < _count; i++)
					{
						if (context.AbortExecution)
						{
							throw new ExecutionAbortedException(base.Runtime as IExecutionRuntime, this, LoopIteration.Target, isAsync: true);
						}
						Iteration.Write(i, context);
						await LoopIteration.ExecuteAsync(context);
					}
					Iteration.Write(0, context);
				}
			}
			return LoopEnd.Target;
		}

		public AsyncFor()
		{
			Iteration = new ValueOutput<int>(this);
		}
	}
	[NodeCategory("Flow/Async")]
	[NodeName("Async Range Loop", false)]
	[NodeOverload("Core.AsyncRangeLoop")]
	public class AsyncRangeLoopInt : AsyncActionNode<ExecutionContext>
	{
		public ValueInput<int> Start;

		public ValueInput<int> End;

		[DefaultValue(1)]
		public ValueInput<int> StepSize;

		public AsyncCall LoopStart;

		public AsyncCall LoopIteration;

		public Continuation LoopEnd;

		public readonly ValueOutput<int> Current;

		public override bool CanBeEvaluated => false;

		protected override async Task<IOperation> RunAsync(ExecutionContext context)
		{
			int _start = Start.Evaluate(context, 0);
			int _end = End.Evaluate(context, 0);
			int _stepSize = StepSize.Evaluate(context, 1);
			await LoopStart.ExecuteAsync(context);
			if (_stepSize > 0)
			{
				int current = _start;
				if (_start > _end)
				{
					while (current >= _end)
					{
						if (context.AbortExecution)
						{
							throw new ExecutionAbortedException(base.Runtime as IExecutionRuntime, this, LoopIteration.Target, isAsync: true);
						}
						Current.Write(current, context);
						await LoopIteration.ExecuteAsync(context);
						current -= _stepSize;
					}
				}
				else
				{
					for (; current <= _end; current += _stepSize)
					{
						if (context.AbortExecution)
						{
							throw new ExecutionAbortedException(base.Runtime as IExecutionRuntime, this, LoopIteration.Target, isAsync: true);
						}
						Current.Write(current, context);
						await LoopIteration.ExecuteAsync(context);
					}
				}
			}
			return LoopEnd.Target;
		}

		public AsyncRangeLoopInt()
		{
			Current = new ValueOutput<int>(this);
		}
	}
	[NodeCategory("Flow/Async")]
	[NodeName("Async Sequence", false)]
	public class AsyncSequence : AsyncActionNode<ExecutionContext>
	{
		public readonly AsyncCallList Calls;

		protected override async Task<IOperation> RunAsync(ExecutionContext context)
		{
			if (Calls.Count == 0)
			{
				return null;
			}
			for (int i = 0; i < Calls.Count - 1; i++)
			{
				await Calls.GetImpulse(i).ExecuteAsync(context);
			}
			return Calls.GetImpulseTarget(Calls.Count - 1);
		}

		public AsyncSequence()
		{
			Calls = new AsyncCallList();
		}
	}
	[NodeCategory("Flow/Async")]
	[NodeName("Async While", false)]
	public class AsyncWhile : AsyncActionNode<ExecutionContext>
	{
		public ValueInput<bool> Condition;

		public AsyncCall LoopStart;

		public AsyncCall LoopIteration;

		public Continuation LoopEnd;

		protected override async Task<IOperation> RunAsync(ExecutionContext context)
		{
			await LoopStart.ExecuteAsync(context);
			while (Condition.Evaluate(context, defaultValue: false))
			{
				if (context.AbortExecution)
				{
					throw new ExecutionAbortedException(base.Runtime as IExecutionRuntime, this, LoopIteration.Target, isAsync: true);
				}
				await LoopIteration.ExecuteAsync(context);
			}
			return LoopEnd.Target;
		}
	}
	[NodeCategory("Flow")]
	[NodeName("For", false)]
	public class For : ActionNode<ExecutionContext>
	{
		public ValueInput<int> Count;

		public ValueInput<bool> Reverse;

		public Call LoopStart;

		public Call LoopIteration;

		public Continuation LoopEnd;

		public readonly ValueOutput<int> Iteration;

		public override bool CanBeEvaluated => false;

		protected override IOperation Run(ExecutionContext context)
		{
			int num = Count.Evaluate(context, 0);
			bool flag = Reverse.Evaluate(context, defaultValue: false);
			LoopStart.Execute(context);
			if (num > 0)
			{
				if (flag)
				{
					for (int num2 = num - 1; num2 >= 0; num2--)
					{
						if (context.AbortExecution)
						{
							throw new ExecutionAbortedException(base.Runtime as IExecutionRuntime, this, LoopIteration.Target, isAsync: false, $"For Count: {num}, Reverse: {flag}, Index: {num2}");
						}
						Iteration.Write(num2, context);
						LoopIteration.Execute(context);
					}
				}
				else
				{
					for (int i = 0; i < num; i++)
					{
						if (context.AbortExecution)
						{
							throw new ExecutionAbortedException(base.Runtime as IExecutionRuntime, this, LoopIteration.Target, isAsync: false, $"For Count: {num}, Reverse: {flag}, Index: {i}");
						}
						Iteration.Write(i, context);
						LoopIteration.Execute(context);
					}
					Iteration.Write(0, context);
				}
			}
			return LoopEnd.Target;
		}

		public For()
		{
			Iteration = new ValueOutput<int>(this);
		}
	}
	[NodeCategory("Flow")]
	[NodeName("If", false)]
	public class If : ActionNode<ExecutionContext>
	{
		public Continuation OnTrue;

		public Continuation OnFalse;

		public ValueInput<bool> Condition;

		protected override IOperation Run(ExecutionContext context)
		{
			if (Condition.Evaluate(context, defaultValue: false))
			{
				return OnTrue.Target;
			}
			return OnFalse.Target;
		}
	}
	[NodeCategory("Flow")]
	public class ImpulseDemultiplexer : VoidNode<ExecutionContext>
	{
		public readonly SyncOperationList Operations;

		public Continuation OnTriggered;

		public readonly ValueOutput<int> Index;

		public override bool CanBeEvaluated => false;

		public IOperation DoOperations(ExecutionContext context, int index)
		{
			Index.Write(index, context);
			return OnTriggered.Target;
		}

		public ImpulseDemultiplexer()
		{
			Operations = new SyncOperationList(this, 0);
			Index = new ValueOutput<int>(this);
		}
	}
	[NodeCategory("Flow")]
	[NodeName("Multiplex", false)]
	public class ImpulseMultiplexer : ActionNode<ExecutionContext>
	{
		public ValueInput<int> Index;

		public readonly ContinuationList Impulses;

		protected override IOperation Run(ExecutionContext context)
		{
			int num = Index.Evaluate(context, 0);
			if (num < 0 || num >= Impulses.Count)
			{
				return null;
			}
			return Impulses.GetImpulseTarget(num);
		}

		public ImpulseMultiplexer()
		{
			Impulses = new ContinuationList();
		}
	}
	[NodeCategory("Flow")]
	[NodeName("Range Loop", false)]
	[NodeOverload("Core.RangeLoop")]
	public class RangeLoopInt : ActionNode<ExecutionContext>
	{
		public ValueInput<int> Start;

		public ValueInput<int> End;

		[DefaultValue(1)]
		public ValueInput<int> StepSize;

		public Call LoopStart;

		public Call LoopIteration;

		public Continuation LoopEnd;

		public readonly ValueOutput<int> Current;

		public override bool CanBeEvaluated => false;

		protected override IOperation Run(ExecutionContext context)
		{
			int num = Start.Evaluate(context, 0);
			int num2 = End.Evaluate(context, 0);
			int num3 = StepSize.Evaluate(context, 1);
			LoopStart.Execute(context);
			if (num3 > 0)
			{
				int i = num;
				if (num > num2)
				{
					while (i >= num2)
					{
						if (context.AbortExecution)
						{
							throw new ExecutionAbortedException(base.Runtime as IExecutionRuntime, this, LoopIteration.Target, isAsync: false);
						}
						Current.Write(i, context);
						LoopIteration.Execute(context);
						i -= num3;
					}
				}
				else
				{
					for (; i <= num2; i += num3)
					{
						if (context.AbortExecution)
						{
							throw new ExecutionAbortedException(base.Runtime as IExecutionRuntime, this, LoopIteration.Target, isAsync: false);
						}
						Current.Write(i, context);
						LoopIteration.Execute(context);
					}
				}
			}
			return LoopEnd.Target;
		}

		public RangeLoopInt()
		{
			Current = new ValueOutput<int>(this);
		}
	}
	[NodeCategory("Flow")]
	[NodeName("Sequence", false)]
	public class Sequence : ActionNode<ExecutionContext>
	{
		public readonly CallList Calls;

		protected override IOperation Run(ExecutionContext context)
		{
			if (Calls.Count == 0)
			{
				return null;
			}
			for (int i = 0; i < Calls.Count - 1; i++)
			{
				Calls.GetImpulse(i).Execute(context);
			}
			return Calls.GetImpulseTarget(Calls.Count - 1);
		}

		public Sequence()
		{
			Calls = new CallList();
		}
	}
	[NodeCategory("Flow")]
	[NodeName("While", false)]
	public class While : ActionNode<ExecutionContext>
	{
		public ValueInput<bool> Condition;

		public Call LoopStart;

		public Call LoopIteration;

		public Continuation LoopEnd;

		protected override IOperation Run(ExecutionContext context)
		{
			LoopStart.Execute(context);
			while (Condition.Evaluate(context, defaultValue: false))
			{
				if (context.AbortExecution)
				{
					throw new ExecutionAbortedException(base.Runtime as IExecutionRuntime, this, LoopIteration.Target, isAsync: false);
				}
				LoopIteration.Execute(context);
			}
			return LoopEnd.Target;
		}
	}
	[NodeCategory("Utility")]
	[NodeName("Get Type", false)]
	public class GetType : ObjectFunctionNode<ExecutionContext, Type>
	{
		public ObjectArgument<object> Object;

		protected override Type Compute(ExecutionContext context)
		{
			return 0.ReadObject<object>(context)?.GetType();
		}
	}
	[NodeCategory("Core")]
	[NodeName("Global To Output", false)]
	[NodeOverload("Core.GlobalToOutput")]
	public class GlobalToValueOutput<T> : ValueFunctionNode<ExecutionContext, T>, IVariable<ExecutionContext, T>, INode where T : unmanaged
	{
		public readonly GlobalRef<T> Global;

		public T Read(ExecutionContext context)
		{
			return Global.Read(context);
		}

		public bool Write(T value, ExecutionContext context)
		{
			return Global.Write(value, context);
		}

		protected override T Compute(ExecutionContext context)
		{
			return Global.Read(context);
		}

		private void OnGlobalChanged(T value, ExecutionContext context)
		{
		}

		public GlobalToValueOutput()
		{
			((GlobalToValueOutput<>)(object)this).Global = new GlobalRef<T>(this, 0);
		}
	}
	[NodeCategory("Core")]
	[NodeName("Global To Output", false)]
	[NodeOverload("Core.GlobalToOutput")]
	public class GlobalToObjectOutput<T> : ObjectFunctionNode<ExecutionContext, T>, IVariable<ExecutionContext, T>, INode
	{
		public readonly GlobalRef<T> Global;

		public T Read(ExecutionContext context)
		{
			return Global.Read(context);
		}

		public bool Write(T value, ExecutionContext context)
		{
			return Global.Write(value, context);
		}

		protected override T Compute(ExecutionContext context)
		{
			return Global.Read(context);
		}

		private void OnGlobalChanged(T value, ExecutionContext context)
		{
		}

		public GlobalToObjectOutput()
		{
			((GlobalToObjectOutput<>)(object)this).Global = new GlobalRef<T>(this, 0);
		}
	}
	[NodeCategory("Core")]
	[NodeName("Link", false)]
	public class Link : VoidNode<ExecutionContext>
	{
		public readonly Reference<INode> A;

		public readonly Reference<INode> B;
	}
	[NodeCategory("Variables")]
	[NodeOverload("Core.Local")]
	public class LocalValue<T> : ValueFunctionNode<ExecutionContext, T>, IVariable<ExecutionContext, T>, INode where T : unmanaged
	{
		private ValueLocal<T> _data;

		protected override T Compute(ExecutionContext context)
		{
			return _data.Read(context);
		}

		public T Read(ExecutionContext context)
		{
			return _data.Read(context);
		}

		public bool Write(T value, ExecutionContext context)
		{
			_data.Write(value, context);
			return true;
		}
	}
	[NodeCategory("Variables")]
	[NodeOverload("Core.Local")]
	public class LocalObject<T> : ObjectFunctionNode<ExecutionContext, T>, IVariable<ExecutionContext, T>, INode
	{
		private ObjectLocal<T> _data;

		protected override T Compute(ExecutionContext context)
		{
			return _data.Read(context);
		}

		public T Read(ExecutionContext context)
		{
			return _data.Read(context);
		}

		public bool Write(T value, ExecutionContext context)
		{
			_data.Write(value, context);
			return true;
		}
	}
	[NodeCategory("Utility")]
	[NodeOverload("Core.Multiplex")]
	public class ValueMultiplex<T> : VoidNode<ExecutionContext> where T : unmanaged
	{
		public readonly ValueInputList<T> Inputs;

		public ValueArgument<int> Index;

		public readonly ValueOutput<T> Output;

		public new readonly ValueOutput<int> InputCount;

		protected override void ComputeOutputs(ExecutionContext context)
		{
			int num = 0.ReadValue<int>(context);
			if (num >= 0 && num < Inputs.Count)
			{
				Output.Write(Inputs.Evaluate(num, context), context);
			}
			else
			{
				Output.Write(default(T), context);
			}
			InputCount.Write(Inputs.Count, context);
		}

		public ValueMultiplex()
		{
			((ValueMultiplex<>)(object)this).Inputs = new ValueInputList<T>();
			((ValueMultiplex<>)(object)this).Output = new ValueOutput<T>(this);
			((ValueMultiplex<>)(object)this).InputCount = new ValueOutput<int>(this);
		}
	}
	[NodeCategory("Utility")]
	[NodeOverload("Core.Multiplex")]
	public class ObjectMultiplex<T> : VoidNode<ExecutionContext>
	{
		public readonly ObjectInputList<T> Inputs;

		public ValueArgument<int> Index;

		public readonly ObjectOutput<T> Output;

		public new readonly ValueOutput<int> InputCount;

		protected override void ComputeOutputs(ExecutionContext context)
		{
			int num = 0.ReadValue<int>(context);
			if (num >= 0 && num < Inputs.Count)
			{
				Output.Write(Inputs.Evaluate(num, context), context);
			}
			else
			{
				Output.Write(default(T), context);
			}
			InputCount.Write(Inputs.Count, context);
		}

		public ObjectMultiplex()
		{
			((ObjectMultiplex<>)(object)this).Inputs = new ObjectInputList<T>();
			((ObjectMultiplex<>)(object)this).Output = new ObjectOutput<T>(this);
			((ObjectMultiplex<>)(object)this).InputCount = new ValueOutput<int>(this);
		}
	}
	[NodeCategory("Operators/Packing")]
	[NodeName("Pack Nullable", false)]
	[NodeOverload("Core.PackNullable")]
	public class PackNullable<T> : ObjectFunctionNode<ExecutionContext, T?> where T : unmanaged
	{
		public ValueArgument<T> Value;

		public ValueArgument<bool> HasValue;

		protected override T? Compute(ExecutionContext context)
		{
			if (1.ReadValue<bool>(context))
			{
				return 0.ReadValue<T>(context);
			}
			return null;
		}
	}
	[NodeCategory("Operators/Packing")]
	[NodeName("Unpack Nullable", false)]
	[NodeOverload("Core.UnpackNullable")]
	public class UnpackNullable<T> : VoidNode<ExecutionContext> where T : unmanaged
	{
		public ObjectArgument<T?> Nullable;

		public readonly ValueOutput<T> Value;

		public readonly ValueOutput<bool> HasValue;

		protected override void ComputeOutputs(ExecutionContext context)
		{
			T? val = 0.ReadObject<T?>(context);
			Value.Write(val.GetValueOrDefault(), context);
			HasValue.Write(val.HasValue, context);
		}

		public UnpackNullable()
		{
			((UnpackNullable<>)(object)this).Value = new ValueOutput<T>(this);
			((UnpackNullable<>)(object)this).HasValue = new ValueOutput<bool>(this);
		}
	}
	[NodeCategory("Operators")]
	[NodeName("?:", false)]
	[NodeOverload("Core.Conditional")]
	public class ValueConditional<T> : ValueFunctionNode<ExecutionContext, T> where T : unmanaged
	{
		public ValueInput<T> OnTrue;

		public ValueInput<T> OnFalse;

		public ValueArgument<bool> Condition;

		protected override T Compute(ExecutionContext context)
		{
			if (2.ReadValue<bool>(context))
			{
				return OnTrue.Evaluate(context);
			}
			return OnFalse.Evaluate(context);
		}
	}
	[NodeCategory("Operators")]
	[NodeName("?:", false)]
	[NodeOverload("Core.Conditional")]
	public class ObjectConditional<T> : ObjectFunctionNode<ExecutionContext, T>
	{
		public ObjectInput<T> OnTrue;

		public ObjectInput<T> OnFalse;

		public ValueArgument<bool> Condition;

		protected override T Compute(ExecutionContext context)
		{
			if (2.ReadValue<bool>(context))
			{
				return OnTrue.Evaluate(context);
			}
			return OnFalse.Evaluate(context);
		}
	}
	[NodeCategory("Operators")]
	[NodeName("==", true)]
	[NodeOverload("Core.Equals")]
	public class ValueEquals<T> : ValueFunctionNode<ExecutionContext, bool> where T : unmanaged
	{
		public ValueArgument<T> A;

		public ValueArgument<T> B;

		protected override bool Compute(ExecutionContext context)
		{
			return EqualityComparer<T>.Default.Equals(0.ReadValue<T>(context), 1.ReadValue<T>(context));
		}
	}
	[NodeCategory("Operators")]
	[NodeName("==", true)]
	[NodeOverload("Core.Equals")]
	public class ObjectEquals<T> : ValueFunctionNode<ExecutionContext, bool>
	{
		public ObjectArgument<T> A;

		public ObjectArgument<T> B;

		protected override bool Compute(ExecutionContext context)
		{
			return EqualityComparer<T>.Default.Equals(0.ReadObject<T>(context), 1.ReadObject<T>(context));
		}
	}
	[NodeCategory("Operators")]
	[NodeName("Is Null", true)]
	[NodeOverload("Core.IsNull")]
	public class IsNull<T> : ValueFunctionNode<ExecutionContext, bool> where T : class
	{
		public ObjectArgument<T> Instance;

		protected override bool Compute(ExecutionContext context)
		{
			return 0.ReadObject<T>(context) == null;
		}
	}
	[NodeCategory("Operators")]
	[NodeName("NOT Null", true)]
	[NodeOverload("Core.NotNull")]
	public class NotNull<T> : ValueFunctionNode<ExecutionContext, bool> where T : class
	{
		public ObjectArgument<T> Instance;

		protected override bool Compute(ExecutionContext context)
		{
			return 0.ReadObject<T>(context) != null;
		}
	}
	[NodeCategory("Operators")]
	[NodeName("!=", true)]
	[NodeOverload("Core.NotEquals")]
	public class ValueNotEquals<T> : ValueFunctionNode<ExecutionContext, bool> where T : unmanaged
	{
		public ValueArgument<T> A;

		public ValueArgument<T> B;

		protected override bool Compute(ExecutionContext context)
		{
			return !EqualityComparer<T>.Default.Equals(0.ReadValue<T>(context), 1.ReadValue<T>(context));
		}
	}
	[NodeCategory("Operators")]
	[NodeName("!=", true)]
	[NodeOverload("Core.NotEquals")]
	public class ObjectNotEquals<T> : ValueFunctionNode<ExecutionContext, bool>
	{
		public ObjectArgument<T> A;

		public ObjectArgument<T> B;

		protected override bool Compute(ExecutionContext context)
		{
			return !EqualityComparer<T>.Default.Equals(0.ReadObject<T>(context), 1.ReadObject<T>(context));
		}
	}
	[NodeCategory("Operators")]
	[NodeName("??", true)]
	[NodeOverload("Core.NullCoalesce")]
	public class NullCoalesce<T> : ObjectFunctionNode<ExecutionContext, T> where T : class
	{
		public ObjectArgument<T> A;

		public ObjectInput<T> B;

		protected override T Compute(ExecutionContext context)
		{
			T val = 0.ReadObject<T>(context);
			if (val != null)
			{
				return val;
			}
			return B.Evaluate(context);
		}
	}
	[NodeCategory("Operators")]
	[NodeName("??", false)]
	[NodeOverload("Core.MultiNullCoalesce")]
	public class MultiNullCoalesce<T> : ObjectFunctionNode<ExecutionContext, T> where T : class
	{
		public readonly ObjectInputList<T> Operands;

		protected override T Compute(ExecutionContext context)
		{
			for (int i = 0; i < Operands.Count; i++)
			{
				T val = Operands.Evaluate(i, context);
				if (val != null)
				{
					return val;
				}
			}
			return null;
		}

		public MultiNullCoalesce()
		{
			((MultiNullCoalesce<>)(object)this).Operands = new ObjectInputList<T>();
		}
	}
	[NodeCategory("Core")]
	[NodeName("Ref To Output", false)]
	[NodeOverload("Core.RefToOutput")]
	public class ReferenceToOutput<T> : ObjectFunctionNode<ExecutionContext, T> where T : INode
	{
		public Reference<T> Reference;

		protected override T Compute(ExecutionContext context)
		{
			return Reference.Target;
		}
	}
	[NodeCategory("Core")]
	[NodeOverload("Core.Relay")]
	public class ValueRelay<T> : ValueFunctionNode<ExecutionContext, T> where T : unmanaged
	{
		public ValueArgument<T> Input;

		public override bool IsPassthrough => true;

		protected override T Compute(ExecutionContext context)
		{
			return 0.ReadValue<T>(context);
		}
	}
	[NodeCategory("Core")]
	[NodeOverload("Core.Relay")]
	public class ObjectRelay<T> : ObjectFunctionNode<ExecutionContext, T>
	{
		public ObjectArgument<T> Input;

		public override bool IsPassthrough => true;

		protected override T Compute(ExecutionContext context)
		{
			return 0.ReadObject<T>(context);
		}
	}
	[NodeCategory("Core")]
	[NodeName("Continuation Relay", false)]
	[NodeOverload("Core.ContinuationRelay")]
	public class ContinuationRelay : ActionFlowNode<ExecutionContext>
	{
		protected override void Do(ExecutionContext context)
		{
		}

		public override ExecutionOperationHandler<T> GetHandler<T>()
		{
			return null;
		}

		public override bool IsOperationPassthrough(int index)
		{
			if (index != 0)
			{
				throw new ArgumentOutOfRangeException("index");
			}
			return true;
		}
	}
	[NodeCategory("Core")]
	[NodeName("Call Relay", false)]
	[NodeOverload("Core.CallRelay")]
	public class CallRelay : ActionNode<ExecutionContext>
	{
		public Call OnTriggered;

		protected override IOperation Run(ExecutionContext context)
		{
			return OnTriggered.Target;
		}

		public override ExecutionOperationHandler<T> GetHandler<T>()
		{
			return null;
		}

		public override bool IsOperationPassthrough(int index)
		{
			if (index != 0)
			{
				throw new ArgumentOutOfRangeException("index");
			}
			return true;
		}
	}
	[NodeCategory("Core")]
	[NodeName("Async Relay", false)]
	[NodeOverload("Core.CallRelay")]
	public class AsyncCallRelay : AsyncActionNode<ExecutionContext>
	{
		public AsyncCall OnTriggered;

		protected override Task<IOperation> RunAsync(ExecutionContext context)
		{
			return Task.FromResult(OnTriggered.Target);
		}

		public override AsyncExecutionOperationHandler<T> GetAsyncHandler<T>()
		{
			return null;
		}

		public override bool IsOperationPassthrough(int index)
		{
			if (index != 0)
			{
				throw new ArgumentOutOfRangeException("index");
			}
			return true;
		}
	}
	[NodeCategory("Variables")]
	[NodeOverload("Core.Store")]
	public class StoredValue<T> : ValueFunctionNode<ExecutionContext, T>, IVariable<ExecutionContext, T>, INode where T : unmanaged
	{
		private ValueStore<T> _data;

		protected override T Compute(ExecutionContext context)
		{
			return _data.Read(context);
		}

		public T Read(ExecutionContext context)
		{
			return _data.Read(context);
		}

		public bool Write(T value, ExecutionContext context)
		{
			_data.Write(value, context);
			return true;
		}
	}
	[NodeCategory("Variables")]
	[NodeOverload("Core.Store")]
	public class StoredObject<T> : ObjectFunctionNode<ExecutionContext, T>, IVariable<ExecutionContext, T>, INode
	{
		private ObjectStore<T> _data;

		protected override T Compute(ExecutionContext context)
		{
			return _data.Read(context);
		}

		public T Read(ExecutionContext context)
		{
			return _data.Read(context);
		}

		public bool Write(T value, ExecutionContext context)
		{
			_data.Write(value, context);
			return true;
		}
	}
	[NodeCategory("Core")]
	[NodeOverload("Core.Unbox")]
	public class Unbox<T> : ValueFunctionNode<ExecutionContext, T> where T : unmanaged
	{
		public ObjectArgument<object> Input;

		protected override T Compute(ExecutionContext context)
		{
			object obj = 0.ReadObject<object>(context);
			if (obj is T)
			{
				return (T)obj;
			}
			return default(T);
		}
	}
	public abstract class WriteBase<C, T> : ActionNode<C> where C : ExecutionContext
	{
		public Continuation OnWritten;

		public Continuation OnFail;

		protected abstract IVariable<C, T> GetVariable(C context);

		protected abstract T GetValue(IVariable<C, T> variable, C context);

		protected override IOperation Run(C context)
		{
			IVariable<C, T> variable = GetVariable(context);
			if (variable != null && variable.Write(GetValue(variable, context), context))
			{
				return OnWritten.Target;
			}
			return OnFail.Target;
		}
	}
	[NodeCategory("Actions")]
	[NodeName("Write", false)]
	[NodeOverload("Core.Write")]
	public class ValueWrite<C, T> : WriteBase<C, T> where C : ExecutionContext where T : unmanaged
	{
		public Reference<IVariable<C, T>> Variable;

		public ValueInput<T> Value;

		protected sealed override IVariable<C, T> GetVariable(C context)
		{
			return Variable.Target;
		}

		protected sealed override T GetValue(IVariable<C, T> variable, C context)
		{
			return Value.Evaluate(context);
		}
	}
	[NodeCategory("Actions")]
	[NodeName("Write", false)]
	[NodeOverload("Core.Write")]
	public class ObjectWrite<C, T> : WriteBase<C, T> where C : ExecutionContext
	{
		public Reference<IVariable<C, T>> Variable;

		public ObjectInput<T> Value;

		protected sealed override IVariable<C, T> GetVariable(C context)
		{
			return Variable.Target;
		}

		protected sealed override T GetValue(IVariable<C, T> variable, C context)
		{
			return Value.Evaluate(context);
		}
	}
	[NodeCategory("Actions/Indirect")]
	[NodeName("Indirect Write", false)]
	[NodeOverload("Core.IndirectWrite")]
	public class ValueIndirectWrite<C, T> : WriteBase<C, T> where C : ExecutionContext where T : unmanaged
	{
		public ObjectInput<IVariable<C, T>> Variable;

		public ValueInput<T> Value;

		protected sealed override IVariable<C, T> GetVariable(C context)
		{
			return Variable.Evaluate(context);
		}

		protected sealed override T GetValue(IVariable<C, T> variable, C context)
		{
			return Value.Evaluate(context);
		}
	}
	[NodeCategory("Actions/Indirect")]
	[NodeName("Indirect Write", false)]
	[NodeOverload("Core.IndirectWrite")]
	public class ObjectIndirectWrite<C, T> : WriteBase<C, T> where C : ExecutionContext
	{
		public ObjectInput<IVariable<C, T>> Variable;

		public ObjectInput<T> Value;

		protected sealed override IVariable<C, T> GetVariable(C context)
		{
			return Variable.Evaluate(context);
		}

		protected sealed override T GetValue(IVariable<C, T> variable, C context)
		{
			return Value.Evaluate(context);
		}
	}
	public class ValueWrite<T> : ValueWrite<ExecutionContext, T> where T : unmanaged
	{
	}
	public class ObjectWrite<T> : ObjectWrite<ExecutionContext, T>
	{
	}
	public class ValueIndirectWrite<T> : ValueIndirectWrite<ExecutionContext, T> where T : unmanaged
	{
	}
	public class ObjectIndirectWrite<T> : ObjectIndirectWrite<ExecutionContext, T>
	{
	}
	[NodeCategory("Core")]
	[NodeName("Write Global", false)]
	[NodeOverload("Core.WriteGlobal")]
	public class WriteValueToGlobal<C, T> : ActionNode<C> where C : ExecutionContext where T : unmanaged
	{
		public readonly GlobalRef<T> Global;

		public ValueInput<T> Value;

		public Continuation OnWritten;

		public Continuation OnFail;

		protected override IOperation Run(C context)
		{
			if (Global.Write(Value.Evaluate(context), context))
			{
				return OnWritten.Target;
			}
			return OnFail.Target;
		}

		private void OnGlobalChanged(T value, C context)
		{
		}

		public WriteValueToGlobal()
		{
			((WriteValueToGlobal<, >)(object)this).Global = new GlobalRef<T>(this, 0);
		}
	}
	[NodeCategory("Core")]
	[NodeName("Write Global", false)]
	[NodeOverload("Core.WriteGlobal")]
	public class WriteObjectToGlobal<C, T> : ActionNode<C> where C : ExecutionContext
	{
		public readonly GlobalRef<T> Global;

		public ObjectInput<T> Value;

		public Continuation OnWritten;

		public Continuation OnFail;

		protected override IOperation Run(C context)
		{
			if (Global.Write(Value.Evaluate(context), context))
			{
				return OnWritten.Target;
			}
			return OnFail.Target;
		}

		private void OnGlobalChanged(T value, C context)
		{
		}

		public WriteObjectToGlobal()
		{
			((WriteObjectToGlobal<, >)(object)this).Global = new GlobalRef<T>(this, 0);
		}
	}
	public abstract class WriteLatchBase<C, T> : VoidNode<C> where C : ExecutionContext
	{
		public Continuation OnSet;

		public Continuation OnReset;

		public Continuation OnFail;

		[PossibleContinuations(new string[] { "OnSet", "OnFail" })]
		public readonly Operation Set;

		[PossibleContinuations(new string[] { "OnReset", "OnFail" })]
		public readonly Operation Reset;

		public IOperation DoSet(C context)
		{
			IVariable<C, T> variable = GetVariable(context);
			if (variable != null && variable.Write(GetSetValue(variable, context), context))
			{
				return OnSet.Target;
			}
			return OnFail.Target;
		}

		public IOperation DoReset(C context)
		{
			IVariable<C, T> variable = GetVariable(context);
			if (variable != null && variable.Write(GetResetValue(variable, context), context))
			{
				return OnReset.Target;
			}
			return OnFail.Target;
		}

		protected abstract IVariable<C, T> GetVariable(C context);

		protected abstract T GetSetValue(IVariable<C, T> variable, C context);

		protected abstract T GetResetValue(IVariable<C, T> variable, C context);

		protected WriteLatchBase()
		{
			((WriteLatchBase<, >)(object)this).Set = new Operation(this, 0);
			((WriteLatchBase<, >)(object)this).Reset = new Operation(this, 1);
		}
	}
	[NodeCategory("Actions")]
	[NodeName("Write Latch", false)]
	[NodeOverload("Core.WriteLatch")]
	public class ValueWriteLatch<C, T> : WriteLatchBase<C, T> where C : ExecutionContext where T : unmanaged
	{
		public Reference<IVariable<C, T>> Variable;

		public ValueInput<T> SetValue;

		public ValueInput<T> ResetValue;

		protected override IVariable<C, T> GetVariable(C context)
		{
			return Variable.Target;
		}

		protected override T GetSetValue(IVariable<C, T> variable, C context)
		{
			return SetValue.Evaluate(context);
		}

		protected override T GetResetValue(IVariable<C, T> variable, C context)
		{
			return ResetValue.Evaluate(context);
		}
	}
	[NodeCategory("Actions")]
	[NodeName("Write Latch", false)]
	[NodeOverload("Core.WriteLatch")]
	public class ObjectWriteLatch<C, T> : WriteLatchBase<C, T> where C : ExecutionContext
	{
		public Reference<IVariable<C, T>> Variable;

		public ObjectInput<T> SetValue;

		public ObjectInput<T> ResetValue;

		protected override IVariable<C, T> GetVariable(C context)
		{
			return Variable.Target;
		}

		protected override T GetSetValue(IVariable<C, T> variable, C context)
		{
			return SetValue.Evaluate(context);
		}

		protected override T GetResetValue(IVariable<C, T> variable, C context)
		{
			return ResetValue.Evaluate(context);
		}
	}
	[NodeCategory("Actions/Indirect")]
	[NodeName("Indirect Write Latch", false)]
	[NodeOverload("Core.IndirectWriteLatch")]
	public class ValueIndirectWriteLatch<C, T> : WriteLatchBase<C, T> where C : ExecutionContext where T : unmanaged
	{
		public ObjectInput<IVariable<C, T>> Variable;

		public ValueInput<T> SetValue;

		public ValueInput<T> ResetValue;

		protected override IVariable<C, T> GetVariable(C context)
		{
			return Variable.Evaluate(context);
		}

		protected override T GetSetValue(IVariable<C, T> variable, C context)
		{
			return SetValue.Evaluate(context);
		}

		protected override T GetResetValue(IVariable<C, T> variable, C context)
		{
			return ResetValue.Evaluate(context);
		}
	}
	[NodeCategory("Actions/Indirect")]
	[NodeName("Indirect Write Latch", false)]
	[NodeOverload("Core.IndirectWriteLatch")]
	public class ObjectIndirectWriteLatch<C, T> : WriteLatchBase<C, T> where C : ExecutionContext
	{
		public ObjectInput<IVariable<C, T>> Variable;

		public ObjectInput<T> SetValue;

		public ObjectInput<T> ResetValue;

		protected override IVariable<C, T> GetVariable(C context)
		{
			return Variable.Evaluate(context);
		}

		protected override T GetSetValue(IVariable<C, T> variable, C context)
		{
			return SetValue.Evaluate(context);
		}

		protected override T GetResetValue(IVariable<C, T> variable, C context)
		{
			return ResetValue.Evaluate(context);
		}
	}
	public class ValueWriteLatch<T> : ValueWriteLatch<ExecutionContext, T> where T : unmanaged
	{
	}
	public class ObjectWriteLatch<T> : ObjectWriteLatch<ExecutionContext, T>
	{
	}
	public class ValueIndirectWriteLatch<T> : ValueIndirectWriteLatch<ExecutionContext, T> where T : unmanaged
	{
	}
	public class ObjectIndirectWriteLatch<T> : ObjectIndirectWriteLatch<ExecutionContext, T>
	{
	}
}
namespace ProtoFlux.Runtimes.Execution.Nodes.Casts
{
	[NodeCategory("Core/Casts")]
	[NodeOverload("Core.ValueCast")]
	public abstract class ValueCast<I, O> : ValueFunctionNode<ExecutionContext, O>, ICast, INode where I : unmanaged where O : unmanaged
	{
		public ValueArgument<I> Input;

		public override bool IsPassthrough => true;

		public Type InputType => typeof(I);

		public bool IsImplicit => TypeHelper.CanImplicitlyConvertTo(InputType, base.OutputType);
	}
	[NodeCategory("Core/Casts")]
	[ObjectCast]
	[NodeOverload("Core.ObjectCast")]
	public class ObjectCast<I, O> : ObjectFunctionNode<ExecutionContext, O>, ICast, INode where I : class where O : class
	{
		public ObjectArgument<I> Input;

		public override bool IsPassthrough => true;

		public Type InputType => typeof(I);

		public bool IsImplicit => TypeHelper.CanImplicitlyConvertTo(InputType, base.OutputType);

		protected override O Compute(ExecutionContext context)
		{
			return 0.ReadObject<I>(context) as O;
		}
	}
	[NodeCategory("Casts")]
	[NodeOverload("Core.ValueCast")]
	public class NullableToObjectCast<I> : ObjectFunctionNode<ExecutionContext, object>, ICast, INode where I : unmanaged
	{
		public ObjectArgument<I?> Input;

		public bool IsImplicit => true;

		public Type InputType => typeof(I?);

		protected override object Compute(ExecutionContext context)
		{
			return 0.ReadObject<I?>(context);
		}
	}
	[NodeCategory("Casts")]
	[NodeOverload("Core.ValueCast")]
	public class ValueToObjectCast<I> : ObjectFunctionNode<ExecutionContext, object>, ICast, INode where I : unmanaged
	{
		public ValueArgument<I> Input;

		public override bool IsPassthrough => true;

		public bool IsImplicit => true;

		public Type InputType => typeof(I);

		protected override object Compute(ExecutionContext context)
		{
			return 0.ReadValue<I>(context);
		}
	}
}
namespace ProtoFlux.Runtimes.DSP
{
	public abstract class DSP_Action<TNode, TSequence, TContext, TBuffer, TAction> where TNode : class, IDSP_Node<TBuffer, TContext> where TSequence : DSP_Sequence<TNode, TSequence, TContext, TBuffer, TAction> where TContext : DSP_Context<TBuffer, TContext> where TBuffer : DSP_Buffer where TAction : DSP_Action<TNode, TSequence, TContext, TBuffer, TAction>
	{
		private List<DSP_Dependency<TSequence>> _dependencies;

		public TNode Node { get; private set; }

		public int InputIndex { get; private set; } = -1;

		public int OutputIndex { get; private set; } = -1;

		public DSP_Action(TNode node)
		{
			Node = node;
		}

		public void MapInputOutput(int inputIndex, int outputIndex)
		{
			InputIndex = inputIndex;
			OutputIndex = outputIndex;
		}

		public void Execute(TContext context, TAction next)
		{
			Node.Process(context);
			TSequence val = null;
			if (_dependencies != null)
			{
				for (int i = 0; i < _dependencies.Count; i++)
				{
					DSP_Dependency<TSequence> dSP_Dependency = _dependencies[i];
					TBuffer buffer = context.GetOutputBuffer(dSP_Dependency.outputIndex);
					bool flag = true;
					if (next != null && next.OutputIndex == dSP_Dependency.outputIndex)
					{
						flag = false;
					}
					else
					{
						for (int j = i + 1; j < _dependencies.Count; j++)
						{
							if (_dependencies[i].outputIndex == dSP_Dependency.outputIndex)
							{
								flag = false;
								break;
							}
						}
					}
					if (!flag)
					{
						buffer = context.CloneBuffer(buffer);
					}
					else
					{
						context.ClearOutputBuffer(dSP_Dependency.outputIndex);
					}
					if (dSP_Dependency.IsResultDependency)
					{
						context.SetResult(dSP_Dependency.inputIndex, buffer);
					}
					else if (dSP_Dependency.sequence.SetDependency(dSP_Dependency.inputIndex, buffer))
					{
						if (val != null || next != null)
						{
							dSP_Dependency.sequence.ScheduleRun(context.CloneContext());
						}
						else
						{
							val = dSP_Dependency.sequence;
						}
					}
				}
			}
			if (next != null)
			{
				context.RemapBuffersAndRecycle(next.OutputIndex, next.InputIndex);
			}
			else if (val != null)
			{
				context.RecycleBuffers();
				val.RunSequence(context);
			}
			else
			{
				context.RecycleContext();
			}
		}

		public void RequestResultDependency(int outputIndex, int resultIndex)
		{
			RequestDepedency(null, resultIndex, outputIndex);
		}

		public void RequestDepedency(TSequence sequence, int inputIndex, int outputIndex)
		{
			if (_dependencies == null)
			{
				_dependencies = new List<DSP_Dependency<TSequence>>();
			}
			_dependencies.Add(new DSP_Dependency<TSequence>(sequence, inputIndex, outputIndex));
		}
	}
	public abstract class DSP_Buffer
	{
		public abstract Type BufferType { get; }

		public abstract void Recycle();
	}
	public abstract class DSP_BuildContext
	{
		public ProtoFlux.Runtimes.Execution.ExecutionContext ExecutionContext { get; protected set; }
	}
	public abstract class DSP_BuildContext<TNode, TSequence, TContext, TBuffer, TAction> : DSP_BuildContext where TNode : class, IDSP_Node<TBuffer, TContext> where TSequence : DSP_Sequence<TNode, TSequence, TContext, TBuffer, TAction>, new() where TContext : DSP_Context<TBuffer, TContext> where TBuffer : DSP_Buffer where TAction : DSP_Action<TNode, TSequence, TContext, TBuffer, TAction>
	{
		private Dictionary<TNode, TSequence> nodes = new Dictionary<TNode, TSequence>();

		private List<TSequence> rootSequences = new List<TSequence>();

		private TaskCompletionSource<bool> completion;

		private DSP_ResultHandler<TBuffer> resultHandler;

		private int outputCount;

		private int remainingOutputs;

		public Dictionary<TNode, TSequence>.KeyCollection CollectedNodes => nodes.Keys;

		public void Collect(List<IOutput> outputs, ProtoFlux.Runtimes.Execution.ExecutionContext executionContext)
		{
			try
			{
				base.ExecutionContext = executionContext;
				for (int i = 0; i < outputs.Count; i++)
				{
					if (!(outputs[i].OwnerNode is TNode val))
					{
						throw new ArgumentException($"Output's owner node isn't node of type {typeof(TNode)}");
					}
					if (!nodes.TryGetValue(val, out var value))
					{
						value = Collect(val);
					}
					value.RegisterResultDependency(outputs[i], i);
				}
				outputCount = outputs.Count;
			}
			finally
			{
				base.ExecutionContext = null;
			}
		}

		private TSequence Collect(TNode node)
		{
			if (nodes.TryGetValue(node, out var value))
			{
				return value;
			}
			TSequence lastSequence = null;
			int lastSequenceInputIndex = -1;
			IOutput lastSequenceOutput = null;
			bool createdNewSequence = false;
			Span<bool> mask = stackalloc bool[node.InputBufferCount];
			node.Collect(this, mask);
			for (int i = 0; i < node.InputBufferCount; i++)
			{
				if (node.IsInputBufferConditional(i) && !mask[i])
				{
					continue;
				}
				IOutput inputBufferSource = node.GetInputBufferSource(i);
				if (inputBufferSource != null)
				{
					if (!(inputBufferSource.OwnerNode is TNode node2))
					{
						throw new ArgumentException($"Output's owner node isn't node of type {typeof(TNode)}");
					}
					AddSequence(Collect(node2), inputBufferSource, i);
				}
			}
			if (lastSequence == null)
			{
				createdNewSequence = true;
				lastSequence = new TSequence();
				rootSequences.Add(lastSequence);
			}
			if (createdNewSequence)
			{
				lastSequence.AddStep(node);
			}
			else
			{
				int outputIndex = lastSequenceOutput.FindLinearOutputIndex();
				lastSequence.AddStep(node, lastSequenceInputIndex, outputIndex);
			}
			nodes.Add(node, lastSequence);
			return lastSequence;
			void AddSequence(TSequence sequence, IOutput output, int inputIndex)
			{
				if (createdNewSequence || (object)output.OwnerNode != sequence.LastNode || lastSequence != null)
				{
					if (!createdNewSequence)
					{
						createdNewSequence = true;
						TSequence val = new TSequence();
						if (lastSequence != null)
						{
							lastSequence.RegisterDependency(lastSequenceOutput, lastSequenceInputIndex, val);
						}
						lastSequence = val;
					}
					sequence.RegisterDependency(output, inputIndex, lastSequence);
				}
				else
				{
					lastSequence = sequence;
					lastSequenceInputIndex = inputIndex;
					lastSequenceOutput = output;
				}
			}
		}

		public async Task Execute(TContext context)
		{
			remainingOutputs = outputCount;
			completion = new TaskCompletionSource<bool>();
			resultHandler = context.ResultHandler;
			context.ResultHandler = HandleResult;
			try
			{
				foreach (TSequence rootSequence in rootSequences)
				{
					rootSequence.ScheduleRun(context.CloneContext());
				}
				await completion.Task.ConfigureAwait(continueOnCapturedContext: false);
			}
			finally
			{
				context.ResultHandler = resultHandler;
			}
		}

		private void HandleResult(int index, TBuffer buffer)
		{
			try
			{
				resultHandler?.Invoke(index, buffer);
			}
			finally
			{
				if (Interlocked.Decrement(ref remainingOutputs) == 0)
				{
					completion.SetResult(result: true);
				}
			}
		}
	}
	public static class DSP_BuildContextHelper
	{
		public static void Collect<T>(this ValueInput<T> input, Span<bool> mask) where T : unmanaged
		{
			throw new NotImplementedException();
		}

		public static void Collect<T>(this ObjectInput<T> input, Span<bool> mask)
		{
			throw new NotImplementedException();
		}

		public static void Collect<T>(this int input, Span<bool> mask)
		{
			mask[input] = true;
		}
	}
	public delegate void DSP_ResultHandler<B>(int resultIndex, B buffer) where B : DSP_Buffer;
	public abstract class DSP_Context<B, C> where B : DSP_Buffer where C : DSP_Context<B, C>
	{
		private List<B> inputBuffers = new List<B>();

		private List<B> outputBuffers = new List<B>();

		private List<int> sharedOutputBuffers = new List<int>();

		public ProtoFlux.Runtimes.Execution.ExecutionContext ExecutionContext { get; set; }

		public DSP_ResultHandler<B> ResultHandler { get; set; }

		internal void SetResult(int resultIndex, B buffer)
		{
			ResultHandler(resultIndex, buffer);
		}

		public B GetInputBuffer(int index)
		{
			return inputBuffers[index];
		}

		public B GetOutputBuffer(int index)
		{
			return outputBuffers[index];
		}

		public B TryGetInputBuffer(int index)
		{
			if (inputBuffers.Count <= index)
			{
				return null;
			}
			return inputBuffers[index];
		}

		public B TryGetOutputBuffer(int index)
		{
			if (outputBuffers.Count <= index)
			{
				return null;
			}
			return outputBuffers[index];
		}

		public B TryGetOutputBufferOrReuseInput(int index, Predicate<B> filter)
		{
			B val = TryGetOutputBuffer(index);
			if (val != null)
			{
				return val;
			}
			for (int i = 0; i < inputBuffers.Count; i++)
			{
				val = inputBuffers[i];
				if (val == null || !filter(val))
				{
					continue;
				}
				bool flag = false;
				for (int j = 0; j < outputBuffers.Count; j++)
				{
					if (val == outputBuffers[j])
					{
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					SetOutputBuffer(index, val);
					while (sharedOutputBuffers.Count <= index)
					{
						sharedOutputBuffers.Add(-1);
					}
					sharedOutputBuffers[index] = i;
					return val;
				}
			}
			return null;
		}

		public void SetOutputBuffer(int index, B buffer)
		{
			EnsureOutputBufferCountForIndex(index);
			outputBuffers[index] = buffer;
		}

		public void MapInputBuffers(List<B> buffers)
		{
			if (inputBuffers.Count > 0)
			{
				throw new InvalidOperationException("Input buffers are already mapped, cannot map new set");
			}
			foreach (B buffer in buffers)
			{
				inputBuffers.Add(buffer);
			}
		}

		public void RemapBuffersAndRecycle(int outputIndex, int inputIndex)
		{
			B val = outputBuffers[outputIndex];
			for (int i = 0; i < inputBuffers.Count; i++)
			{
				B val2 = inputBuffers[i];
				if (val2 != null && val2 != val)
				{
					RecycleBuffer(inputBuffers[i]);
				}
				inputBuffers[i] = null;
			}
			for (int j = 0; j < outputBuffers.Count; j++)
			{
				B val3 = outputBuffers[j];
				if (val3 != null && val3 != val)
				{
					RecycleBuffer(outputBuffers[j]);
				}
				outputBuffers[j] = null;
			}
			EnsureInputBufferCountForIndex(inputIndex);
			EnsureOutputBufferCountForIndex(outputIndex);
			sharedOutputBuffers.Clear();
			inputBuffers[inputIndex] = val;
		}

		public void RecycleBuffers()
		{
			for (int i = 0; i < inputBuffers.Count; i++)
			{
				if (inputBuffers[i] != null)
				{
					RecycleBuffer(inputBuffers[i]);
				}
			}
			for (int j = 0; j < outputBuffers.Count; j++)
			{
				if (outputBuffers[j] != null)
				{
					RecycleBuffer(outputBuffers[j]);
				}
			}
			inputBuffers.Clear();
			outputBuffers.Clear();
			sharedOutputBuffers.Clear();
		}

		public void ClearOutputBuffer(int index)
		{
			if (sharedOutputBuffers.Count > index && sharedOutputBuffers[index] >= 0)
			{
				inputBuffers[sharedOutputBuffers[index]] = null;
				sharedOutputBuffers[index] = -1;
			}
			outputBuffers[index] = null;
		}

		public abstract void EnqueueTask(Action<C> task);

		public C CloneContext()
		{
			C val = CloneSelf();
			val.ExecutionContext = ExecutionContext;
			val.ResultHandler = ResultHandler;
			return val;
		}

		public virtual void RecycleContext()
		{
			RecycleBuffers();
			ExecutionContext = null;
			ResultHandler = null;
		}

		public abstract B CloneBuffer(B buffer);

		protected abstract void RecycleBuffer(B buffer);

		protected abstract C CloneSelf();

		private void EnsureInputBufferCountForIndex(int index)
		{
			EnsureBufferCountForIndex(inputBuffers, index);
		}

		private void EnsureOutputBufferCountForIndex(int index)
		{
			EnsureBufferCountForIndex(outputBuffers, index);
		}

		private void EnsureBufferCountForIndex(List<B> list, int index)
		{
			while (list.Count > index)
			{
				list.RemoveAt(list.Count - 1);
			}
			while (list.Count <= index)
			{
				list.Add(null);
			}
		}
	}
	internal readonly struct DSP_Dependency<TSequence> where TSequence : class
	{
		public readonly TSequence sequence;

		public readonly int inputIndex;

		public readonly int outputIndex;

		public bool IsResultDependency => sequence == null;

		public DSP_Dependency(TSequence sequence, int inputIndex, int outputIndex)
		{
			this.sequence = sequence;
			this.inputIndex = inputIndex;
			this.outputIndex = outputIndex;
		}
	}
	public static class DSP_MetadataHelper
	{
		private static ConcurrentDictionary<Type, DSP_NodeMetadata> _metadataCache = new ConcurrentDictionary<Type, DSP_NodeMetadata>();

		public static DSP_NodeMetadata GetMetadata(Type type)
		{
			if (_metadataCache.TryGetValue(type, out var value))
			{
				return value;
			}
			NodeMetadata metadata = NodeMetadataHelper.GetMetadata(type);
			value = new DSP_NodeMetadata();
			value.HasExecutionEntryPoints = metadata.FixedInputs.Any((InputMetadata i) => i.CrossRuntime is ExecutionInputAttribute) || metadata.DynamicInputs.Any((InputListMetadata i) => i.CrossRuntime is ExecutionInputAttribute);
			value.FixedBufferInputs.AddRange(metadata.FixedInputs.Where((InputMetadata i) => IsDSP(i)));
			value.FixedBufferOutputs.AddRange(metadata.FixedOutputs.Where((OutputMetadata i) => IsDSP(i)));
			value.DynamicBufferInputs.AddRange(metadata.DynamicInputs.Where((InputListMetadata i) => IsDSP(i)));
			value.DynamicBufferOutputs.AddRange(metadata.DynamicOutputs.Where((OutputListMetadata i) => IsDSP(i)));
			_metadataCache.TryAdd(type, value);
			return value;
		}

		public static bool IsDSP(InputMetadataBase input)
		{
			if (input.CrossRuntime != null)
			{
				return false;
			}
			return true;
		}

		public static bool IsDSP(OutputMetadataBase output)
		{
			return true;
		}
	}
	public abstract class DSP_Node<B, C> : Node, IDSP_Node<B, C>, IDSP_Node, INode where B : DSP_Buffer where C : DSP_Context<B, C>
	{
		public virtual DSP_NodeMetadata DSP_Metadata => DSP_MetadataHelper.GetMetadata(GetType());

		public bool HasExecutionEntryPoints => DSP_Metadata.HasExecutionEntryPoints;

		public int InputBufferCount
		{
			get
			{
				DSP_NodeMetadata dSP_Metadata = DSP_Metadata;
				if (dSP_Metadata.DynamicBufferInputCount == 0)
				{
					return dSP_Metadata.FixedBufferInputCount;
				}
				int num = dSP_Metadata.FixedBufferInputCount;
				for (int i = 0; i < dSP_Metadata.DynamicBufferInputCount; i++)
				{
					int index = BaseInputListIndex(i);
					IInputList inputList = GetInputList(index);
					num += inputList.Count;
				}
				return num;
			}
		}

		public int OutputBufferCount
		{
			get
			{
				DSP_NodeMetadata dSP_Metadata = DSP_Metadata;
				if (dSP_Metadata.DynamicBufferOutputCount == 0)
				{
					return dSP_Metadata.FixedBufferOutputCount;
				}
				int num = dSP_Metadata.FixedBufferOutputCount;
				for (int i = 0; i < dSP_Metadata.DynamicBufferOutputCount; i++)
				{
					int index = BaseOutputListIndex(i);
					IOutputList outputList = GetOutputList(index);
					num += outputList.Count;
				}
				return num;
			}
		}

		private int BaseInputIndex(int index)
		{
			return DSP_Metadata.FixedBufferInputs[index].Index;
		}

		private int BaseOutputIndex(int index)
		{
			return DSP_Metadata.FixedBufferOutputs[index].Index;
		}

		private int BaseInputListIndex(int index)
		{
			return DSP_Metadata.DynamicBufferInputs[index].Index;
		}

		private int BaseOutputListIndex(int index)
		{
			return DSP_Metadata.DynamicBufferOutputs[index].Index;
		}

		private IInputList ConvertInputIndex(ref int index, out int listIndex)
		{
			if (index < FixedInputCount)
			{
				index = BaseInputIndex(index);
				listIndex = -1;
				return null;
			}
			index -= FixedInputCount;
			DSP_NodeMetadata dSP_Metadata = DSP_Metadata;
			for (int i = 0; i < dSP_Metadata.DynamicBufferInputCount; i++)
			{
				IInputList inputList = GetInputList(BaseInputListIndex(i));
				if (index < inputList.Count)
				{
					listIndex = i;
					return inputList;
				}
				index -= inputList.Count;
			}
			throw new ArgumentOutOfRangeException("index");
		}

		private IInputList GetOutputList(ref int index, out int listIndex)
		{
			if (index < FixedInputCount)
			{
				index = BaseOutputIndex(index);
				listIndex = -1;
				return null;
			}
			index -= FixedInputCount;
			DSP_NodeMetadata dSP_Metadata = DSP_Metadata;
			for (int i = 0; i < dSP_Metadata.DynamicBufferInputCount; i++)
			{
				IInputList inputList = GetInputList(BaseInputListIndex(i));
				if (index < inputList.Count)
				{
					listIndex = i;
					return inputList;
				}
				index -= inputList.Count;
			}
			throw new ArgumentOutOfRangeException("index");
		}

		public abstract void Collect(DSP_BuildContext context, Span<bool> mask);

		public IOutput GetInputBufferSource(int index)
		{
			int listIndex;
			IInputList inputList = ConvertInputIndex(ref index, out listIndex);
			if (inputList == null)
			{
				return GetInputSource(index);
			}
			return inputList.GetInputSource(index);
		}

		public bool IsInputBufferConditional(int index)
		{
			ConvertInputIndex(ref index, out var listIndex);
			if (listIndex < 0)
			{
				return IsInputConditional(index);
			}
			return Metadata.DynamicInputs[listIndex].IsConditional;
		}

		public abstract void Process(C context);
	}
	public abstract class DSP_Runtime<TNode, TContext, TBuffer, TBuildContext, TSequence, TAction, TExecutionContext> : NodeRuntime<TNode>, IExecutionRuntimeInterop where TNode : class, IDSP_Node<TBuffer, TContext> where TContext : DSP_Context<TBuffer, TContext> where TBuffer : DSP_Buffer where TBuildContext : DSP_BuildContext<TNode, TSequence, TContext, TBuffer, TAction>, new() where TSequence : DSP_Sequence<TNode, TSequence, TContext, TBuffer, TAction>, new() where TAction : DSP_Action<TNode, TSequence, TContext, TBuffer, TAction> where TExecutionContext : ProtoFlux.Runtimes.Execution.ExecutionContext
	{
		public ExecutionRuntime<TExecutionContext> ExecutionRuntime { get; set; }

		public bool InputNodesMustBeLocal => true;

		public async Task<TBuffer> Process(IOutput output, TContext context)
		{
			TBuffer result = null;
			context.ResultHandler = delegate(int index, TBuffer buffer)
			{
				result = buffer;
			};
			await Process(new List<IOutput> { output }, context).ConfigureAwait(continueOnCapturedContext: false);
			return result;
		}

		public async Task Process(List<IOutput> outputs, TContext context)
		{
			TBuildContext val = new TBuildContext();
			TExecutionContext context2 = (TExecutionContext)context.ExecutionContext;
			ExecutionRuntime.BeginStackFrame(context2);
			val.Collect(outputs, context.ExecutionContext);
			foreach (TNode collectedNode in val.CollectedNodes)
			{
				if (!collectedNode.HasExecutionEntryPoints)
				{
					continue;
				}
				NodeMetadata metadata = collectedNode.Metadata;
				for (int i = 0; i < metadata.FixedInputCount; i++)
				{
					if (metadata.FixedInputs[i].CrossRuntime is ExecutionInputAttribute)
					{
						IOutput inputSource = collectedNode.GetInputSource(i);
						if (inputSource != null)
						{
							ExecutionRuntime.EnsureEvaluated(inputSource, context2);
						}
					}
				}
				for (int j = 0; j < metadata.DynamicInputCount; j++)
				{
					if (!(metadata.DynamicInputs[j].CrossRuntime is ExecutionInputAttribute))
					{
						continue;
					}
					IInputList inputList = collectedNode.GetInputList(j);
					for (int k = 0; k < inputList.Count; k++)
					{
						IOutput inputSource2 = inputList.GetInputSource(k);
						if (inputSource2 != null)
						{
							ExecutionRuntime.EnsureEvaluated(inputSource2, context2);
						}
					}
				}
			}
			await val.Execute(context).ConfigureAwait(continueOnCapturedContext: false);
			ExecutionRuntime.EndStackFrame((TExecutionContext)context.ExecutionContext);
		}
	}
	public abstract class DSP_Sequence<TNode, TSequence, TContext, TBuffer, TAction> where TNode : class, IDSP_Node<TBuffer, TContext> where TSequence : DSP_Sequence<TNode, TSequence, TContext, TBuffer, TAction> where TContext : DSP_Context<TBuffer, TContext> where TBuffer : DSP_Buffer where TAction : DSP_Action<TNode, TSequence, TContext, TBuffer, TAction>
	{
		private List<TAction> _actions = new List<TAction>();

		private List<TBuffer> _dependencyBuffers = new List<TBuffer>();

		private int _missingDependencies;

		public TNode LastNode
		{
			get
			{
				if (_actions.Count == 0)
				{
					return null;
				}
				return _actions[_actions.Count - 1].Node;
			}
		}

		internal void AllocateDependency(int inputIndex)
		{
			while (_dependencyBuffers.Count <= inputIndex)
			{
				_dependencyBuffers.Add(null);
			}
			_missingDependencies++;
		}

		internal bool SetDependency(int inputIndex, TBuffer buffer)
		{
			_dependencyBuffers[inputIndex] = buffer;
			return Interlocked.Decrement(ref _missingDependencies) == 0;
		}

		internal void AddStep(TNode node)
		{
			if (_actions.Count > 0)
			{
				throw new InvalidOperationException("Node without input and output index mapping can be only added when it's first in the sequence");
			}
			AddStep(node, -1, -1);
		}

		internal void AddStep(IDSP_Node node, int inputIndex, int outputIndex)
		{
			TAction val = CreateAction(node);
			val.MapInputOutput(inputIndex, outputIndex);
			_actions.Add(val);
		}

		protected abstract TAction CreateAction(IDSP_Node node);

		internal void RegisterDependency(IOutput output, int inputIndex, TSequence targetSequence)
		{
			TAction val = FindAction(output);
			targetSequence.AllocateDependency(inputIndex);
			int outputIndex = output.FindLinearOutputIndex();
			val.RequestDepedency(targetSequence, inputIndex, outputIndex);
		}

		internal void RegisterResultDependency(IOutput output, int resultIndex)
		{
			TAction val = FindAction(output);
			int outputIndex = output.FindLinearOutputIndex();
			val.RequestResultDependency(outputIndex, resultIndex);
		}

		internal TAction FindAction(IOutput output)
		{
			if (!(output.OwnerNode is TNode val))
			{
				throw new ArgumentException($"Output's owner node isn't node of type {typeof(TNode)}");
			}
			for (int num = _actions.Count - 1; num >= 0; num--)
			{
				if (_actions[num].Node == val)
				{
					return _actions[num];
				}
			}
			throw new ArgumentException("No DSP Action produces given output");
		}

		internal void ScheduleRun(TContext context)
		{
			context.EnqueueTask(RunSequence);
		}

		internal void RunSequence(TContext context)
		{
			context.MapInputBuffers(_dependencyBuffers);
			for (int i = 0; i < _actions.Count; i++)
			{
				TAction val = _actions[i];
				int num = i + 1;
				val.Execute(context, (num == _actions.Count) ? null : _actions[num]);
			}
		}
	}
	public interface IDSP_Node : INode
	{
		DSP_NodeMetadata DSP_Metadata { get; }

		bool HasExecutionEntryPoints { get; }

		int InputBufferCount { get; }

		int OutputBufferCount { get; }

		IOutput GetInputBufferSource(int index);

		bool IsInputBufferConditional(int index);

		void Collect(DSP_BuildContext context, Span<bool> mask);
	}
	public interface IDSP_Node<B, C> : IDSP_Node, INode where B : DSP_Buffer where C : DSP_Context<B, C>
	{
		void Process(C context);
	}
	public class DSP_NodeMetadata
	{
		public int FixedBufferInputCount => FixedBufferInputs.Count;

		public int FixedBufferOutputCount => FixedBufferOutputs.Count;

		public int DynamicBufferInputCount => DynamicBufferInputs.Count;

		public int DynamicBufferOutputCount => DynamicBufferOutputs.Count;

		public bool HasExecutionEntryPoints { get; internal set; }

		public List<InputMetadata> FixedBufferInputs { get; private set; } = new List<InputMetadata>();

		public List<OutputMetadata> FixedBufferOutputs { get; private set; } = new List<OutputMetadata>();

		public List<InputListMetadata> DynamicBufferInputs { get; private set; } = new List<InputListMetadata>();

		public List<OutputListMetadata> DynamicBufferOutputs { get; private set; } = new List<OutputListMetadata>();
	}
}
namespace ProtoFlux.Runtimes.DSP.Array
{
	public class DSP_Array_Action : DSP_Action<IDSP_Array_Node, DSP_Array_Sequence, DSP_Array_Context, DSP_Array_Buffer, DSP_Array_Action>
	{
		public DSP_Array_Action(IDSP_Array_Node node)
			: base(node)
		{
		}
	}
	public abstract class DSP_Array_Buffer : DSP_Buffer
	{
		public abstract DSP_Array_Buffer Clone(DSP_Array_Context context, DSP_Array_Buffer source);

		public abstract void Copy(DSP_Array_Buffer source);
	}
	public class DSP_Array_Buffer<T> : DSP_Array_Buffer where T : struct
	{
		public override Type BufferType => typeof(T);

		public T[] Buffer { get; private set; }

		public DSP_Array_Buffer(int size)
		{
			Buffer = new T[size];
		}

		public override void Copy(DSP_Array_Buffer source)
		{
			System.Array.Copy(((DSP_Array_Buffer<T>)source).Buffer, Buffer, Buffer.Length);
		}

		public override DSP_Array_Buffer Clone(DSP_Array_Context context, DSP_Array_Buffer source)
		{
			DSP_Array_Buffer<T> dSP_Array_Buffer = context.AllocateBuffer<T>();
			dSP_Array_Buffer.Copy(source);
			return dSP_Array_Buffer;
		}

		public override void Recycle()
		{
		}
	}
	public class DSP_Array_BuildContext : DSP_BuildContext<IDSP_Array_Node, DSP_Array_Sequence, DSP_Array_Context, DSP_Array_Buffer, DSP_Array_Action>
	{
	}
	public class DSP_Array_Context : DSP_Context<DSP_Array_Buffer, DSP_Array_Context>
	{
		public DSP_Array_Buffer<T> AllocateBuffer<T>() where T : struct
		{
			DSP_Array_Buffer<T> dSP_Array_Buffer = new DSP_Array_Buffer<T>(16);
			Console.WriteLine("Buffer allocated " + dSP_Array_Buffer.GetHashCode() + "\tThread: " + Thread.CurrentThread.ManagedThreadId);
			return dSP_Array_Buffer;
		}

		public override DSP_Array_Buffer CloneBuffer(DSP_Array_Buffer buffer)
		{
			Console.WriteLine("Cloning buffer " + buffer.GetHashCode() + "\tThread: " + Thread.CurrentThread.ManagedThreadId);
			return buffer.Clone(this, buffer);
		}

		protected override DSP_Array_Context CloneSelf()
		{
			DSP_Array_Context dSP_Array_Context = new DSP_Array_Context();
			Console.WriteLine("Context cloned " + dSP_Array_Context.GetHashCode() + "\tThread: " + Thread.CurrentThread.ManagedThreadId);
			return dSP_Array_Context;
		}

		public override void EnqueueTask(Action<DSP_Array_Context> task)
		{
			Task.Run(delegate
			{
				task(this);
			});
		}

		public override void RecycleContext()
		{
			base.RecycleContext();
			Console.WriteLine("Context recycled " + GetHashCode() + "\tThread: " + Thread.CurrentThread.ManagedThreadId);
		}

		protected override void RecycleBuffer(DSP_Array_Buffer buffer)
		{
			Console.WriteLine("Buffer recycled " + buffer.GetHashCode() + "\tThread: " + Thread.CurrentThread.ManagedThreadId);
		}
	}
	public static class DSP_Array_BufferExtensions
	{
		public static T[] GetInputBuffer<T>(this ValueInput<T> input, DSP_Array_Context context) where T : unmanaged
		{
			throw new NotImplementedException();
		}

		public static T[] GetOutputBuffer<T>(this ValueOutput<T> input, DSP_Array_Context context) where T : unmanaged
		{
			throw new NotImplementedException();
		}

		public static T[] GetOutputBufferOrReuse<T>(this ValueOutput<T> input, DSP_Array_Context context) where T : unmanaged
		{
			throw new NotImplementedException();
		}

		public static T[] GetInputBuffer<T>(this int index, DSP_Array_Context context) where T : unmanaged
		{
			return (context.TryGetInputBuffer(index) as DSP_Array_Buffer<T>)?.Buffer;
		}

		public static T[] GetOutputBuffer<T>(this int index, DSP_Array_Context context) where T : unmanaged
		{
			return GetOutputBuffer<T>(index, context, allowReuse: false);
		}

		public static T[] GetOutputBufferOrReuse<T>(this int index, DSP_Array_Context context) where T : unmanaged
		{
			return GetOutputBuffer<T>(index, context, allowReuse: true);
		}

		private static T[] GetOutputBuffer<T>(int index, DSP_Array_Context context, bool allowReuse) where T : unmanaged
		{
			T[] array = (((allowReuse ? context.TryGetOutputBufferOrReuseInput(index, (DSP_Array_Buffer b) => b.BufferType == typeof(T)) : context.TryGetOutputBuffer(index)) is DSP_Array_Buffer<T> dSP_Array_Buffer) ? dSP_Array_Buffer.Buffer : null);
			if (array == null)
			{
				DSP_Array_Buffer<T> dSP_Array_Buffer2 = context.AllocateBuffer<T>();
				context.SetOutputBuffer(index, dSP_Array_Buffer2);
				array = dSP_Array_Buffer2.Buffer;
			}
			return array;
		}
	}
	public abstract class DSP_Array_Node : DSP_Node<DSP_Array_Buffer, DSP_Array_Context>, IDSP_Array_Node, IDSP_Node<DSP_Array_Buffer, DSP_Array_Context>, IDSP_Node, INode
	{
	}
	public class DSP_Array_Runtime<C> : DSP_Runtime<IDSP_Array_Node, DSP_Array_Context, DSP_Array_Buffer, DSP_Array_BuildContext, DSP_Array_Sequence, DSP_Array_Action, C> where C : ProtoFlux.Runtimes.Execution.ExecutionContext
	{
	}
	public class DSP_Array_Sequence : DSP_Sequence<IDSP_Array_Node, DSP_Array_Sequence, DSP_Array_Context, DSP_Array_Buffer, DSP_Array_Action>
	{
		protected override DSP_Array_Action CreateAction(IDSP_Node node)
		{
			return new DSP_Array_Action((IDSP_Array_Node)node);
		}
	}
	public interface IDSP_Array_Node : IDSP_Node<DSP_Array_Buffer, DSP_Array_Context>, IDSP_Node, INode
	{
	}
	[NodeOverload("Core.Add")]
	public class TestAddArraysNode : DSP_Array_Node
	{
		public ValueInput<float> A;

		public ValueInput<float> B;

		public readonly ValueOutput<float> Output;

		public override void Collect(DSP_BuildContext context, Span<bool> mask)
		{
		}

		public override void Process(DSP_Array_Context context)
		{
			Console.WriteLine("Adding arrays " + GetHashCode());
			float[] inputBuffer = 0.GetInputBuffer<float>(context);
			float[] inputBuffer2 = 1.GetInputBuffer<float>(context);
			float[] outputBufferOrReuse = 0.GetOutputBufferOrReuse<float>(context);
			for (int i = 0; i < outputBufferOrReuse.Length; i++)
			{
				outputBufferOrReuse[i] = inputBuffer[i] + inputBuffer2[i];
			}
		}

		public TestAddArraysNode()
		{
			Output = new ValueOutput<float>(this);
		}
	}
	[NodeOverload("Core.Add")]
	public class TestAddArraysNodeExecution : DSP_Array_Node
	{
		public ValueInput<float> A;

		[ExecutionInput]
		public ValueInput<float> B;

		public readonly ValueOutput<float> Output;

		public override void Collect(DSP_BuildContext context, Span<bool> mask)
		{
		}

		public override void Process(DSP_Array_Context context)
		{
			float[] inputBuffer = 0.GetInputBuffer<float>(context);
			float num = B.Evaluate(context.ExecutionContext, 0f);
			Console.WriteLine($"Adding {num} to array " + GetHashCode());
			float[] outputBufferOrReuse = 0.GetOutputBufferOrReuse<float>(context);
			for (int i = 0; i < outputBufferOrReuse.Length; i++)
			{
				outputBufferOrReuse[i] = inputBuffer[i] + num;
			}
		}

		public TestAddArraysNodeExecution()
		{
			Output = new ValueOutput<float>(this);
		}
	}
	public class TestAmplifyArray : DSP_Array_Node
	{
		public ValueInput<float> Input;

		public readonly ValueOutput<float> Output;

		[ExecutionInput]
		public ValueInput<float> Multiplier;

		public override void Collect(DSP_BuildContext context, Span<bool> mask)
		{
		}

		public override void Process(DSP_Array_Context context)
		{
			float num = Multiplier.Evaluate(context.ExecutionContext, 0f);
			Console.WriteLine($"Multiplying array by {num}\t" + GetHashCode());
			float[] inputBuffer = 0.GetInputBuffer<float>(context);
			if (inputBuffer != null)
			{
				float[] outputBufferOrReuse = 0.GetOutputBufferOrReuse<float>(context);
				for (int i = 0; i < inputBuffer.Length; i++)
				{
					outputBufferOrReuse[i] = inputBuffer[i] * num;
				}
			}
		}

		public TestAmplifyArray()
		{
			Output = new ValueOutput<float>(this);
		}
	}
	public class TestConditionalCollectArrayNode : DSP_Array_Node
	{
		public ValueInput<float> OnTrue;

		public ValueInput<float> OnFalse;

		[ExecutionInput]
		public ValueInput<bool> Condition;

		public readonly ValueOutput<float> Output;

		public override void Collect(DSP_BuildContext context, Span<bool> mask)
		{
			bool flag = Condition.Evaluate(context.ExecutionContext, defaultValue: false);
			Console.WriteLine("Collecting Conditional DSP: " + flag);
			if (flag)
			{
				0.Collect<float>(mask);
			}
			else
			{
				1.Collect<float>(mask);
			}
		}

		public override void Process(DSP_Array_Context context)
		{
			float[] array = 0.GetInputBuffer<float>(context) ?? 1.GetInputBuffer<float>(context);
			float[] outputBufferOrReuse = 0.GetOutputBufferOrReuse<float>(context);
			if (array != outputBufferOrReuse)
			{
				System.Array.Copy(array, outputBufferOrReuse, array.Length);
			}
		}

		public TestConditionalCollectArrayNode()
		{
			Output = new ValueOutput<float>(this);
		}
	}
	public class TestNegateArrayNode : DSP_Array_Node
	{
		public ValueInput<float> Input;

		public readonly ValueOutput<float> Output;

		public override void Collect(DSP_BuildContext context, Span<bool> mask)
		{
		}

		public override void Process(DSP_Array_Context context)
		{
			Console.WriteLine("Negating array " + GetHashCode());
			float[] inputBuffer = 0.GetInputBuffer<float>(context);
			if (inputBuffer != null)
			{
				float[] outputBufferOrReuse = 0.GetOutputBufferOrReuse<float>(context);
				for (int i = 0; i < inputBuffer.Length; i++)
				{
					outputBufferOrReuse[i] = 0f - inputBuffer[i];
				}
			}
		}

		public TestNegateArrayNode()
		{
			Output = new ValueOutput<float>(this);
		}
	}
	public class TestRandomArrayNode : DSP_Array_Node
	{
		public readonly ValueOutput<float> Output;

		public override void Collect(DSP_BuildContext context, Span<bool> mask)
		{
		}

		public override void Process(DSP_Array_Context context)
		{
			Console.WriteLine("Generating random array " + GetHashCode());
			float[] outputBufferOrReuse = 0.GetOutputBufferOrReuse<float>(context);
			Random random = new Random();
			for (int i = 0; i < outputBufferOrReuse.Length; i++)
			{
				outputBufferOrReuse[i] = (float)random.NextDouble();
			}
		}

		public TestRandomArrayNode()
		{
			Output = new ValueOutput<float>(this);
		}
	}
	public class TestSequenceArrayNode : DSP_Array_Node
	{
		public readonly ValueOutput<float> Output;

		public override void Collect(DSP_BuildContext context, Span<bool> mask)
		{
		}

		public override void Process(DSP_Array_Context context)
		{
			Console.WriteLine("Generating sequence array " + GetHashCode());
			float[] outputBufferOrReuse = 0.GetOutputBufferOrReuse<float>(context);
			for (int i = 0; i < outputBufferOrReuse.Length; i++)
			{
				outputBufferOrReuse[i] = i;
			}
		}

		public TestSequenceArrayNode()
		{
			Output = new ValueOutput<float>(this);
		}
	}
}
namespace ProtoFlux.Core
{
	public static class CastHelper
	{
		private static Dictionary<Type, Dictionary<Type, List<Type>>> valueCasts;

		private static List<Type> objectCasts;

		public static void SearchCasts()
		{
			if (valueCasts != null)
			{
				return;
			}
			Dictionary<Type, Dictionary<Type, List<Type>>> dictionary = new Dictionary<Type, Dictionary<Type, List<Type>>>();
			List<Type> list = new List<Type>();
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			foreach (Assembly assembly in assemblies)
			{
				try
				{
					Type[] types = assembly.GetTypes();
					foreach (Type type in types)
					{
						if (type.IsAbstract || type.IsValueType)
						{
							continue;
						}
						foreach (ValueCastAttribute customAttribute in type.GetCustomAttributes<ValueCastAttribute>(inherit: true))
						{
							if (!dictionary.TryGetValue(customAttribute.From, out var value))
							{
								value = new Dictionary<Type, List<Type>>();
								dictionary.Add(customAttribute.From, value);
							}
							if (!value.TryGetValue(customAttribute.To, out var value2))
							{
								value2 = new List<Type>();
								value.Add(customAttribute.To, value2);
							}
							value2.Add(type);
						}
						if (type.GetCustomAttribute<ObjectCastAttribute>(inherit: true) != null)
						{
							list.Add(type);
						}
					}
				}
				catch (ReflectionTypeLoadException)
				{
				}
			}
			valueCasts = dictionary;
			objectCasts = list;
		}

		public static IReadOnlyList<Type> GetValueCastNodes(Type from, Type to)
		{
			SearchCasts();
			if (!valueCasts.TryGetValue(from, out var value))
			{
				return null;
			}
			if (!value.TryGetValue(to, out var value2))
			{
				return null;
			}
			return value2;
		}

		public static Type GetCastNode(Type from, Type to, NodeRuntime runtime)
		{
			if (from.IsUnmanaged() && to == typeof(object))
			{
				Type compatibleNodeType = runtime.GetCompatibleNodeType(typeof(ValueToObjectCast<>).MakeGenericType(from));
				if (compatibleNodeType != null)
				{
					return compatibleNodeType;
				}
			}
			if (from.IsNullable() && to == typeof(object))
			{
				Type compatibleNodeType2 = runtime.GetCompatibleNodeType(typeof(NullableToObjectCast<>).MakeGenericType(Nullable.GetUnderlyingType(from)));
				if (compatibleNodeType2 != null)
				{
					return compatibleNodeType2;
				}
			}
			SearchCasts();
			if (from.IsUnmanaged())
			{
				IReadOnlyList<Type> valueCastNodes = GetValueCastNodes(from, to);
				if (valueCastNodes == null)
				{
					return null;
				}
				foreach (Type item in valueCastNodes)
				{
					Type compatibleNodeType3 = runtime.GetCompatibleNodeType(item);
					if (compatibleNodeType3 != null)
					{
						return compatibleNodeType3;
					}
				}
			}
			if ((from.IsClass || from.IsInterface) && (to.IsClass || to.IsInterface))
			{
				foreach (Type objectCast in objectCasts)
				{
					Type compatibleNodeType4 = runtime.GetCompatibleNodeType(objectCast);
					if (compatibleNodeType4 != null)
					{
						return compatibleNodeType4.MakeGenericType(from, to);
					}
				}
			}
			return null;
		}
	}
	public class ChangeListenerAttribute : Attribute
	{
	}
	public class ChangeSourceAttribute : Attribute
	{
	}
	internal struct ChangeSourceInfo
	{
		public bool continuous;

		public List<ElementPath<IOutput>> outputs;

		public bool ProducesChanges
		{
			get
			{
				if (!continuous)
				{
					List<ElementPath<IOutput>> list = outputs;
					if (list == null)
					{
						return false;
					}
					return list.Count > 0;
				}
				return true;
			}
		}

		public void SetContinuous()
		{
			outputs = null;
			continuous = true;
		}

		public void Add(IOutput output)
		{
			bool allocatedList = true;
			Combine(new ElementPath<IOutput>(output), ref allocatedList);
		}

		public void Combine(ElementPath<IOutput> output, ref bool allocatedList)
		{
			if (continuous)
			{
				throw new InvalidOperationException("Cannot add outputs when the changes are already continuous");
			}
			if (outputs == null)
			{
				outputs = new List<ElementPath<IOutput>>();
				allocatedList = true;
			}
			else if (!allocatedList)
			{
				outputs = new List<ElementPath<IOutput>>(outputs);
				allocatedList = true;
			}
			outputs.Add(output);
		}

		public bool Combine(ChangeSourceInfo other, ref bool allocatedList)
		{
			if (other.continuous)
			{
				SetContinuous();
				return false;
			}
			List<ElementPath<IOutput>> list = other.outputs;
			if (list != null && list.Count > 0)
			{
				if (outputs == null)
				{
					outputs = other.outputs;
				}
				else
				{
					if (!allocatedList)
					{
						outputs = new List<ElementPath<IOutput>>(outputs);
						allocatedList = true;
					}
					outputs.AddRange(other.outputs);
				}
			}
			return true;
		}

		public override string ToString()
		{
			if (continuous)
			{
				return "Continuous";
			}
			if (!ProducesChanges)
			{
				return "No Changes";
			}
			return $"{outputs?.Count} outputs: {string.Join(", ", outputs)}";
		}
	}
	internal class ChangeTrackingBuildContext
	{
		private HashSet<NodeGroup> _currentlyProcessing = new HashSet<NodeGroup>();

		private HashSet<NodeGroup> _currentConflicts = new HashSet<NodeGroup>();

		public bool HasConflicts => _currentConflicts.Count > 0;

		public bool ContainsCurrentlyProcessing(NodeGroup group)
		{
			if (!group.ChangeTrackingBuilt)
			{
				throw new InvalidOperationException("Cannot check if the change tracking data hasn't been built");
			}
			foreach (NodeGroup item in _currentlyProcessing)
			{
				if (group.ContainsNestedGroup(item))
				{
					return true;
				}
			}
			return false;
		}

		public bool TryNestInto(NodeGroup group)
		{
			if (_currentlyProcessing.Add(group))
			{
				return true;
			}
			_currentConflicts.Add(group);
			return false;
		}

		public void NestOut(NodeGroup group)
		{
			_currentlyProcessing.Remove(group);
			_currentConflicts.Remove(group);
		}
	}
	internal class ChangeTrackingData
	{
		public readonly HashSet<NodeGroup> NestedGroups = new HashSet<NodeGroup>();

		public readonly List<ElementPath<INode>> ContinuousChanges = new List<ElementPath<INode>>();

		public readonly Dictionary<ElementPath<IOutput>, OrderedSet<ElementPath<INode>>> ChangeListeners = new Dictionary<ElementPath<IOutput>, OrderedSet<ElementPath<INode>>>();

		public readonly Dictionary<IOutput, ChangeSourceInfo> ExportsInfo = new Dictionary<IOutput, ChangeSourceInfo>();

		public void Sort()
		{
			ContinuousChanges.Sort();
		}

		public void RegisterListener(ElementPath<IOutput> output, ElementPath<INode> node)
		{
			if (!ChangeListeners.TryGetValue(output, out var value))
			{
				value = new OrderedSet<ElementPath<INode>>();
				ChangeListeners.Add(output, value);
			}
			value.Add(node);
		}

		public void Clear()
		{
			NestedGroups.Clear();
			ContinuousChanges.Clear();
			ChangeListeners.Clear();
			ExportsInfo.Clear();
		}

		public override string ToString()
		{
			return $"NestedGroups: {string.Join(", ", NestedGroups)}\nContinuousChanges:\n{string.Join("\n", ContinuousChanges.Select((ElementPath<INode> v) => $"\t{v}"))}\nChangeListeners:\n{string.Join("\n", ChangeListeners.Select((KeyValuePair<ElementPath<IOutput>, OrderedSet<ElementPath<INode>>> e) => $"\n{e.Key}:\n{string.Join("\n", e.Value.Select((ElementPath<INode> v) => $"\t\t{v}"))}"))}";
		}
	}
	public class ContinuouslyChangingAttribute : Attribute
	{
	}
	public class NodeGroup
	{
		private ChangeTrackingData changeTrackingData = new ChangeTrackingData();

		private List<NodeRuntime> runtimes = new List<NodeRuntime>();

		private int _nodeAllocationCount;

		public bool ChangeTrackingBuilt { get; private set; }

		public bool ChangeTrackingDirty { get; private set; }

		public bool RequiresRebuild
		{
			get
			{
				if (!ChangeTrackingDirty)
				{
					return !ChangeTrackingBuilt;
				}
				return true;
			}
		}

		public bool IgnoreChanges { get; set; }

		public IReadOnlyList<ElementPath<INode>> ContinuousChanges => changeTrackingData.ContinuousChanges;

		public Dictionary<ElementPath<IOutput>, OrderedSet<ElementPath<INode>>>.KeyCollection ActiveChangeSources => changeTrackingData.ChangeListeners.Keys;

		public string Name { get; set; }

		public int RuntimeCount => runtimes.Count;

		public IEnumerable<NodeRuntime> Runtimes => runtimes;

		public int TotalNodeCount => runtimes.Sum((NodeRuntime r) => r.NodeCount);

		public event Action<NodeGroup> ChangeTrackingInvalidated;

		public void MarkChangeTrackingDirty()
		{
			if (!ChangeTrackingDirty)
			{
				ChangeTrackingDirty = true;
				this.ChangeTrackingInvalidated?.Invoke(this);
			}
		}

		public bool ContainsNestedGroup(NodeGroup group)
		{
			return changeTrackingData.NestedGroups.Contains(group);
		}

		private bool CheckChangeTracking()
		{
			if (IgnoreChanges)
			{
				return false;
			}
			if (ChangeTrackingBuilt)
			{
				return true;
			}
			throw new InvalidOperationException($"Change tracking data is not built for group {this}");
		}

		public void OutputChanged(ElementPath<IOutput> output, HashSet<ElementPath<INode>> changedNodes)
		{
			if (!CheckChangeTracking() || !changeTrackingData.ChangeListeners.TryGetValue(output, out var value))
			{
				return;
			}
			foreach (ElementPath<INode> item in value)
			{
				changedNodes.Add(item);
			}
		}

		public void OutputChanged(ElementPath<IOutput> output, SortedSet<ElementPath<INode>> changedNodes)
		{
			if (!CheckChangeTracking() || !changeTrackingData.ChangeListeners.TryGetValue(output, out var value))
			{
				return;
			}
			foreach (ElementPath<INode> item in value)
			{
				changedNodes.Add(item);
			}
		}

		public void AllChanged(HashSet<ElementPath<INode>> changedNodes)
		{
			if (!CheckChangeTracking())
			{
				return;
			}
			foreach (KeyValuePair<ElementPath<IOutput>, OrderedSet<ElementPath<INode>>> changeListener in changeTrackingData.ChangeListeners)
			{
				foreach (ElementPath<INode> item in changeListener.Value)
				{
					changedNodes.Add(item);
				}
			}
		}

		public void AllChanged(SortedSet<ElementPath<INode>> changedNodes)
		{
			if (!CheckChangeTracking())
			{
				return;
			}
			foreach (KeyValuePair<ElementPath<IOutput>, OrderedSet<ElementPath<INode>>> changeListener in changeTrackingData.ChangeListeners)
			{
				foreach (ElementPath<INode> item in changeListener.Value)
				{
					changedNodes.Add(item);
				}
			}
		}

		public void RebuildChangeTrackingData()
		{
			if (RequiresRebuild)
			{
				ChangeTrackingBuilt = false;
				BuildChangeTrackingData(new ChangeTrackingBuildContext());
			}
		}

		private ChangeTrackingData BuildChangeTrackingData(ChangeTrackingBuildContext context)
		{
			if (!context.TryNestInto(this))
			{
				return null;
			}
			ChangeTrackingData changeTrackingData;
			if (ChangeTrackingBuilt)
			{
				changeTrackingData = new ChangeTrackingData();
			}
			else
			{
				changeTrackingData = this.changeTrackingData;
				changeTrackingData.Clear();
			}
			Dictionary<IOutput, ChangeSourceInfo> infos = new Dictionary<IOutput, ChangeSourceInfo>();
			List<ChangeSourceInfo> list = new List<ChangeSourceInfo>();
			Dictionary<INestedNode, ChangeTrackingData> dictionary = new Dictionary<INestedNode, ChangeTrackingData>();
			HashSet<INode> hashSet = new HashSet<INode>();
			foreach (NodeRuntime runtime in runtimes)
			{
				for (int i = 0; i < runtime.NodeCount; i++)
				{
					if (!(runtime.GetNodeGeneric(i) is INestedNode { TargetGroup: not null } nestedNode))
					{
						continue;
					}
					changeTrackingData.NestedGroups.Add(nestedNode.TargetGroup);
					ChangeTrackingData changeTrackingData2 = ((!nestedNode.TargetGroup.ChangeTrackingBuilt || context.ContainsCurrentlyProcessing(nestedNode.TargetGroup)) ? nestedNode.TargetGroup.BuildChangeTrackingData(context) : nestedNode.TargetGroup.changeTrackingData);
					dictionary.Add(nestedNode, changeTrackingData2);
					if (changeTrackingData2 == null)
					{
						continue;
					}
					foreach (NodeGroup nestedGroup in changeTrackingData2.NestedGroups)
					{
						changeTrackingData.NestedGroups.Add(nestedGroup);
					}
				}
			}
			foreach (NodeRuntime runtime2 in runtimes)
			{
				for (int j = 0; j < runtime2.DataExportsCount; j++)
				{
					IOutput valueExport = runtime2.GetValueExport(j);
					changeTrackingData.ExportsInfo.Add(valueExport, GetChangeInfo(valueExport, infos, dictionary));
				}
				for (int k = 0; k < runtime2.NodeCount; k++)
				{
					INode nodeGeneric = runtime2.GetNodeGeneric(k);
					if (nodeGeneric is INestedNode nestedNode2)
					{
						ChangeTrackingData changeTrackingData3 = dictionary[nestedNode2];
						if (changeTrackingData3 == null)
						{
							continue;
						}
						hashSet.Clear();
						foreach (ElementPath<INode> continuousChange in changeTrackingData3.ContinuousChanges)
						{
							changeTrackingData.ContinuousChanges.Add(continuousChange.Nest(nodeGeneric));
						}
						foreach (KeyValuePair<ElementPath<IOutput>, OrderedSet<ElementPath<INode>>> changeListener in changeTrackingData3.ChangeListeners)
						{
							if (!(changeListener.Key.element.OwnerNode is DataImportNode))
							{
								continue;
							}
							IOutput importSource = nestedNode2.GetImportSource(changeListener.Key.element);
							if (importSource == null || !GetChangeInfo(importSource, infos, dictionary).continuous)
							{
								continue;
							}
							foreach (ElementPath<INode> item in changeListener.Value)
							{
								changeTrackingData.ContinuousChanges.Add(item.Nest(nodeGeneric));
								hashSet.Add(item.element);
							}
						}
						foreach (KeyValuePair<ElementPath<IOutput>, OrderedSet<ElementPath<INode>>> changeListener2 in changeTrackingData3.ChangeListeners)
						{
							if (changeListener2.Key.element.OwnerNode is DataImportNode)
							{
								IOutput importSource2 = nestedNode2.GetImportSource(changeListener2.Key.element);
								if (importSource2 == null)
								{
									continue;
								}
								ChangeSourceInfo changeInfo = GetChangeInfo(importSource2, infos, dictionary);
								if (!changeInfo.ProducesChanges || changeInfo.continuous)
								{
									continue;
								}
								foreach (ElementPath<INode> item2 in changeListener2.Value)
								{
									if (hashSet.Contains(item2.element))
									{
										continue;
									}
									ElementPath<INode> node = item2.Nest(nodeGeneric);
									foreach (ElementPath<IOutput> output2 in changeInfo.outputs)
									{
										changeTrackingData.RegisterListener(output2, node);
									}
								}
								continue;
							}
							ElementPath<IOutput> output = changeListener2.Key.Nest(nodeGeneric);
							foreach (ElementPath<INode> item3 in changeListener2.Value)
							{
								if (!hashSet.Contains(item3.element))
								{
									changeTrackingData.RegisterListener(output, item3.Nest(nodeGeneric));
								}
							}
						}
					}
					else
					{
						if (!nodeGeneric.ListensToChanges)
						{
							continue;
						}
						list.Clear();
						for (int l = 0; l < nodeGeneric.InputCount; l++)
						{
							if (!nodeGeneric.IsInputListeningToChanges(l))
							{
								continue;
							}
							IOutput inputSource = nodeGeneric.GetInputSource(l);
							if (inputSource == null)
							{
								continue;
							}
							ChangeSourceInfo changeInfo2 = GetChangeInfo(inputSource, infos, dictionary);
							if (changeInfo2.ProducesChanges)
							{
								if (changeInfo2.continuous)
								{
									list.Clear();
									changeTrackingData.ContinuousChanges.Add(new ElementPath<INode>(nodeGeneric));
									break;
								}
								list.Add(changeInfo2);
							}
						}
						if (list.Count <= 0)
						{
							continue;
						}
						foreach (ChangeSourceInfo item4 in list)
						{
							foreach (ElementPath<IOutput> output3 in item4.outputs)
							{
								changeTrackingData.RegisterListener(output3, new ElementPath<INode>(nodeGeneric));
							}
						}
					}
				}
			}
			context.NestOut(this);
			if (!context.HasConflicts)
			{
				this.changeTrackingData = changeTrackingData;
				ChangeTrackingBuilt = true;
				ChangeTrackingDirty = false;
			}
			else if (this.changeTrackingData == changeTrackingData)
			{
				this.changeTrackingData = new ChangeTrackingData();
			}
			changeTrackingData.Sort();
			return changeTrackingData;
		}

		private ChangeSourceInfo GetChangeInfo(IOutput output, Dictionary<IOutput, ChangeSourceInfo> infos, Dictionary<INestedNode, ChangeTrackingData> nestedData)
		{
			if (infos.TryGetValue(output, out var value))
			{
				return value;
			}
			Node ownerNode = output.OwnerNode;
			if (ownerNode is DataImportNode)
			{
				value.Add(output);
			}
			else if (ownerNode is INestedNode nestedNode)
			{
				if (nestedData.TryGetValue(nestedNode, out var value2))
				{
					if (value2 == null)
					{
						CombineInputs(ownerNode, ref value, infos, nestedData);
					}
					else
					{
						IOutput targetExport = nestedNode.GetTargetExport(output);
						ChangeSourceInfo changeSourceInfo = value2.ExportsInfo[targetExport];
						if (changeSourceInfo.continuous)
						{
							value = changeSourceInfo;
						}
						else if (changeSourceInfo.ProducesChanges)
						{
							bool allocatedList = false;
							foreach (ElementPath<IOutput> output2 in changeSourceInfo.outputs)
							{
								if (output2.element.OwnerNode is DataImportNode)
								{
									IOutput importSource = nestedNode.GetImportSource(output2.element);
									if (importSource != null)
									{
										ChangeSourceInfo changeInfo = GetChangeInfo(importSource, infos, nestedData);
										if (!value.Combine(changeInfo, ref allocatedList))
										{
											break;
										}
									}
								}
								else
								{
									value.Combine(output2.Nest(ownerNode), ref allocatedList);
								}
							}
						}
					}
				}
			}
			else
			{
				int index = output.FindLinearOutputIndex();
				bool flag = true;
				switch (ownerNode.GetOutputChangeType(index))
				{
				case OutputChangeSource.Continuous:
					value.SetContinuous();
					flag = false;
					break;
				case OutputChangeSource.Individual:
					value.Add(output);
					break;
				default:
					throw new NotImplementedException("Unsupported change source type");
				case OutputChangeSource.Passthrough:
					break;
				}
				if (flag)
				{
					CombineInputs(ownerNode, ref value, infos, nestedData);
				}
			}
			infos.Add(output, value);
			return value;
		}

		private void CombineInputs(INode node, ref ChangeSourceInfo info, Dictionary<IOutput, ChangeSourceInfo> infos, Dictionary<INestedNode, ChangeTrackingData> nestedData)
		{
			bool allocatedList = false;
			for (int i = 0; i < node.InputCount; i++)
			{
				IOutput inputSource = node.GetInputSource(i);
				if (inputSource != null)
				{
					ChangeSourceInfo changeInfo = GetChangeInfo(inputSource, infos, nestedData);
					if (!info.Combine(changeInfo, ref allocatedList))
					{
						break;
					}
				}
			}
		}

		public string DebugChangeTrackingData()
		{
			return changeTrackingData.ToString();
		}

		public string PrintDebugStructure()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine(Name);
			foreach (NodeRuntime runtime in runtimes)
			{
				PrintRuntime(stringBuilder, runtime, 1);
			}
			return stringBuilder.ToString();
		}

		private void PrintRuntime(StringBuilder str, NodeRuntime runtime, int indent)
		{
			Indent(str, indent);
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(9, 2, str);
			handler.AppendFormatted(runtime.GetType().GetNiceTypeName());
			handler.AppendLiteral(" (");
			handler.AppendFormatted(runtime.NodeCount);
			handler.AppendLiteral(" nodes)");
			str.AppendLine(ref handler);
			for (int i = 0; i < runtime.NodeCount; i++)
			{
				PrintNode(str, runtime.GetNodeGeneric(i), indent + 1);
			}
		}

		private void PrintNode(StringBuilder str, INode node, int indent)
		{
			Indent(str, indent);
			StringBuilder stringBuilder = str;
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(3, 2, stringBuilder);
			handler.AppendLiteral("[");
			handler.AppendFormatted(node.IndexInGroup);
			handler.AppendLiteral("] ");
			handler.AppendFormatted(node.GetType().GetNiceTypeName());
			stringBuilder2.AppendLine(ref handler);
			if (node.FixedInputCount > 0)
			{
				Indent(str, indent + 1);
				str.AppendLine("Inputs:");
				for (int i = 0; i < node.FixedInputCount; i++)
				{
					IOutput inputSource = node.GetInputSource(i);
					if (inputSource != null)
					{
						Indent(str, indent + 2);
						stringBuilder = str;
						StringBuilder stringBuilder3 = stringBuilder;
						handler = new StringBuilder.AppendInterpolatedStringHandler(2, 2, stringBuilder);
						handler.AppendFormatted(node.GetInputName(i));
						handler.AppendLiteral(": ");
						handler.AppendFormatted(PrintInputSource(inputSource));
						stringBuilder3.AppendLine(ref handler);
					}
				}
			}
			if (node.DynamicInputCount > 0)
			{
				Indent(str, indent + 1);
				str.AppendLine("Dynamic Inputs:");
				for (int j = 0; j < node.DynamicInputCount; j++)
				{
					IInputList inputList = node.GetInputList(j);
					Indent(str, indent + 2);
					stringBuilder = str;
					StringBuilder stringBuilder4 = stringBuilder;
					handler = new StringBuilder.AppendInterpolatedStringHandler(10, 2, stringBuilder);
					handler.AppendFormatted(node.GetInputListName(j));
					handler.AppendLiteral(" (Count: ");
					handler.AppendFormatted(inputList.Count);
					handler.AppendLiteral(")");
					stringBuilder4.AppendLine(ref handler);
					for (int k = 0; k < inputList.Count; k++)
					{
						Indent(str, indent + 3);
						IOutput inputSource2 = inputList.GetInputSource(k);
						stringBuilder = str;
						StringBuilder stringBuilder5 = stringBuilder;
						handler = new StringBuilder.AppendInterpolatedStringHandler(2, 2, stringBuilder);
						handler.AppendFormatted(k);
						handler.AppendLiteral(": ");
						handler.AppendFormatted(PrintInputSource(inputSource2));
						stringBuilder5.AppendLine(ref handler);
					}
				}
			}
			if (node.FixedImpulseCount > 0)
			{
				Indent(str, indent + 1);
				str.AppendLine("Impulses:");
				for (int l = 0; l < node.FixedImpulseCount; l++)
				{
					IOperation impulseTarget = node.GetImpulseTarget(l);
					if (impulseTarget != null)
					{
						Indent(str, indent + 2);
						stringBuilder = str;
						StringBuilder stringBuilder6 = stringBuilder;
						handler = new StringBuilder.AppendInterpolatedStringHandler(2, 2, stringBuilder);
						handler.AppendFormatted(node.GetImpulseName(l));
						handler.AppendLiteral(": ");
						handler.AppendFormatted(PrintImpulseTarget(impulseTarget));
						stringBuilder6.AppendLine(ref handler);
					}
				}
			}
			if (node.DynamicImpulseCount <= 0)
			{
				return;
			}
			Indent(str, indent + 1);
			str.AppendLine("Dynamic Impulses:");
			for (int m = 0; m < node.DynamicImpulseCount; m++)
			{
				IImpulseList impulseList = node.GetImpulseList(m);
				Indent(str, indent + 2);
				stringBuilder = str;
				StringBuilder stringBuilder7 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(10, 2, stringBuilder);
				handler.AppendFormatted(node.GetImpulseListName(m));
				handler.AppendLiteral(" (Count: ");
				handler.AppendFormatted(impulseList.Count);
				handler.AppendLiteral(")");
				stringBuilder7.AppendLine(ref handler);
				for (int n = 0; n < impulseList.Count; n++)
				{
					Indent(str, indent + 3);
					IOperation impulseTarget2 = impulseList.GetImpulseTarget(n);
					stringBuilder = str;
					StringBuilder stringBuilder8 = stringBuilder;
					handler = new StringBuilder.AppendInterpolatedStringHandler(2, 2, stringBuilder);
					handler.AppendFormatted(n);
					handler.AppendLiteral(": ");
					handler.AppendFormatted(PrintImpulseTarget(impulseTarget2));
					stringBuilder8.AppendLine(ref handler);
				}
			}
		}

		private string PrintInputSource(IOutput source)
		{
			if (source == null)
			{
				return "(none)";
			}
			source.FindOutputIndex(out var index, out var listIndex);
			string value = ((listIndex < 0) ? (source.OwnerNode.GetOutputName(index) ?? "") : $"{source.OwnerNode.GetOutputListName(listIndex)}[{index}]");
			return $"{value} on [{source.OwnerNode.IndexInGroup}] {source.OwnerNode.GetType().GetNiceTypeName()} (Type: {source.OutputType} - {source.OutputDataClass})";
		}

		private string PrintImpulseTarget(IOperation target)
		{
			if (target == null)
			{
				return "(none)";
			}
			target.FindOperationIndex(out var index, out var listIndex);
			string value = ((listIndex < 0) ? $"{target.OwnerNode.GetOperation(index)}" : $"{target.OwnerNode.GetOperationList(listIndex)}[{index}]");
			return $"{value} on [{target.OwnerNode.IndexInGroup}] {target.OwnerNode.GetType().GetNiceTypeName()}";
		}

		private void Indent(StringBuilder str, int indent)
		{
			for (int i = 0; i < indent; i++)
			{
				str.Append("-");
			}
		}

		public NodeRuntime GetRuntime(int index)
		{
			return runtimes[index];
		}

		internal int GetNodeAllocationIndex()
		{
			return _nodeAllocationCount++;
		}

		public NodeGroup(string name)
		{
			Name = name;
		}

		public R AddRuntime<R>() where R : NodeRuntime, new()
		{
			R val = new R();
			val.Init(this);
			runtimes.Add(val);
			return val;
		}

		public R GetRuntime<R>() where R : NodeRuntime
		{
			foreach (NodeRuntime runtime in runtimes)
			{
				if (runtime is R result)
				{
					return result;
				}
			}
			return null;
		}

		public void ForeachNode<T>(NodeEnumerationAction<T> action, bool cache) where T : INode
		{
			ForeachNode(action, new NodeEnumerationContext(), cache);
		}

		public void ForeachNode<T>(NodeEnumerationAction<T> action, NodeEnumerationContext context, bool cache) where T : INode
		{
			context.Begin(this);
			foreach (NodeRuntime runtime in runtimes)
			{
				runtime.ForeachNode(action, context, cache);
			}
			context.End();
		}

		public override string ToString()
		{
			return "Node Group \"" + Name + "\"";
		}
	}
	public enum DataClass
	{
		Value,
		Object
	}
	[AttributeUsage(AttributeTargets.Field)]
	public class DefaultValueAttribute : Attribute
	{
		public object Value { get; private set; }

		public DefaultValueAttribute(object value)
		{
			Value = value;
		}
	}
	public class AsyncCallList : IImpulseList
	{
		private class InternalCall : IAsyncCall, IImpulse
		{
			public IOperation Target { get; set; }

			public ImpulseType ImpulseType => ImpulseType.AsyncCall;

			IOperation IImpulse.Target
			{
				get
				{
					return Target;
				}
				set
				{
					Target = value;
				}
			}
		}

		private List<InternalCall> calls = new List<InternalCall>();

		public int Count => calls.Count;

		public IImpulse AddImpulse(IOperation target = null)
		{
			InternalCall internalCall = new InternalCall();
			internalCall.Target = target;
			calls.Add(internalCall);
			return internalCall;
		}

		public void RemoveImpulse()
		{
			calls.RemoveAt(calls.Count - 1);
		}

		public void Clear()
		{
			calls.Clear();
		}

		public IImpulse GetImpulse(int index)
		{
			return calls[index];
		}

		public ImpulseType GetImpulseType(int index)
		{
			return ImpulseType.AsyncCall;
		}

		public IOperation GetImpulseTarget(int index)
		{
			return calls[index].Target;
		}

		public void SetImpulseTarget(int index, IOperation target)
		{
			calls[index].Target = target;
		}
	}
	public class AsyncOperationList : IOperationList
	{
		public class Operation : IAsyncOperation, IOperation, IListOperation
		{
			public int Index { get; private set; }

			public AsyncOperationList List { get; private set; }

			public Node OwnerNode => List.Owner;

			IOperationList IListOperation.List => List;

			public Operation(int index, AsyncOperationList list)
			{
				Index = index;
				List = list;
			}

			public override string ToString()
			{
				return $"Async Operation [{Index}] on {OwnerNode} ({GetHashCode()})";
			}
		}

		private List<Operation> _operations = new List<Operation>();

		public Node Owner { get; private set; }

		public int Index { get; private set; }

		public int Count => _operations.Count;

		public IAsyncOperation AddOperation()
		{
			Operation operation = new Operation(Count, this);
			_operations.Add(operation);
			return operation;
		}

		public void RemoveOperation()
		{
			_operations.RemoveAt(_operations.Count - 1);
		}

		public IAsyncOperation GetOperation(int index)
		{
			return _operations[index];
		}

		public void Clear()
		{
			_operations.Clear();
		}

		public AsyncOperationList(Node owner, int index)
		{
			Owner = owner;
			Index = index;
		}

		IOperation IOperationList.AddOperation()
		{
			return AddOperation();
		}

		IOperation IOperationList.GetOperation(int index)
		{
			return GetOperation(index);
		}

		bool IOperationList.IsOperationAsync(int index)
		{
			return true;
		}
	}
	public class CallList : IImpulseList
	{
		private class InternalCall : ICall, IImpulse
		{
			public ISyncOperation Target { get; set; }

			public ImpulseType ImpulseType => ImpulseType.Call;

			IOperation IImpulse.Target
			{
				get
				{
					return Target;
				}
				set
				{
					Target = (ISyncOperation)value;
				}
			}
		}

		private List<InternalCall> calls = new List<InternalCall>();

		public int Count => calls.Count;

		public IImpulse AddImpulse(IOperation target = null)
		{
			if (target is ISyncOperation target2)
			{
				InternalCall internalCall = new InternalCall();
				internalCall.Target = target2;
				calls.Add(internalCall);
				return internalCall;
			}
			if (target == null)
			{
				InternalCall internalCall2 = new InternalCall();
				calls.Add(internalCall2);
				return internalCall2;
			}
			throw new ArgumentException("Invalid target operation: " + target);
		}

		public void RemoveImpulse()
		{
			calls.RemoveAt(calls.Count - 1);
		}

		public void Clear()
		{
			calls.Clear();
		}

		public IImpulse GetImpulse(int index)
		{
			return calls[index];
		}

		public ImpulseType GetImpulseType(int index)
		{
			return ImpulseType.Call;
		}

		public IOperation GetImpulseTarget(int index)
		{
			return calls[index].Target;
		}

		public void SetImpulseTarget(int index, IOperation target)
		{
			calls[index].Target = (ISyncOperation)target;
		}
	}
	public class ContinuationList : IImpulseList
	{
		private class InternalContinuation : IContinuation, IImpulse
		{
			public IOperation Target { get; set; }

			public ImpulseType ImpulseType => ImpulseType.Continuation;
		}

		private List<InternalContinuation> continuations = new List<InternalContinuation>();

		public int Count => continuations.Count;

		public IImpulse AddImpulse(IOperation target = null)
		{
			InternalContinuation internalContinuation = new InternalContinuation();
			internalContinuation.Target = target;
			continuations.Add(internalContinuation);
			return internalContinuation;
		}

		public void RemoveImpulse()
		{
			continuations.RemoveAt(continuations.Count - 1);
		}

		public void Clear()
		{
			continuations.Clear();
		}

		public IImpulse GetImpulse(int index)
		{
			return continuations[index];
		}

		public ImpulseType GetImpulseType(int index)
		{
			return ImpulseType.Continuation;
		}

		public IOperation GetImpulseTarget(int index)
		{
			return continuations[index].Target;
		}

		public void SetImpulseTarget(int index, IOperation target)
		{
			continuations[index].Target = target;
		}
	}
	public class ListGlobalRef<T> : GlobalRef<T>
	{
		public GlobalRefList List { get; private set; }

		public ListGlobalRef(GlobalRefList list, int index)
			: base(list.OwnerNode, index)
		{
			List = list;
		}
	}
	public class GlobalRefList
	{
		private List<IGlobalRef> globalRefs = new List<IGlobalRef>();

		public Node OwnerNode { get; private set; }

		public int Index { get; private set; }

		public int Count => globalRefs.Count;

		public GlobalRefList(Node owner, int index)
		{
			Index = index;
			OwnerNode = owner;
		}

		public IGlobalRef<T> AddGlobalRef<T>(Global<T> binding = null)
		{
			ListGlobalRef<T> listGlobalRef = new ListGlobalRef<T>(this, Count);
			globalRefs.Add(listGlobalRef);
			if (binding != null)
			{
				listGlobalRef.Global = binding;
			}
			return listGlobalRef;
		}

		public void SetGlobalRefBinding<T>(int index, Global<T> binding)
		{
			GetGlobalRef<T>(index).Global = binding;
		}

		public IGlobalRef GetUntypedGlobalRef(int index)
		{
			return globalRefs[index];
		}

		public IGlobalRef<T> GetGlobalRef<T>(int index)
		{
			return (IGlobalRef<T>)globalRefs[index];
		}

		public void RemoveGlobalRef()
		{
			if (globalRefs.Count == 0)
			{
				throw new InvalidOperationException("GlobalRefList has no global refs");
			}
			globalRefs[globalRefs.Count - 1].ClearReference();
			globalRefs.RemoveAt(globalRefs.Count - 1);
		}

		public void Clear()
		{
			foreach (IGlobalRef globalRef in globalRefs)
			{
				globalRef.ClearReference();
			}
			globalRefs.Clear();
		}
	}
	public interface IImpulseList
	{
		int Count { get; }

		IImpulse AddImpulse(IOperation target = null);

		void RemoveImpulse();

		void Clear();

		ImpulseType GetImpulseType(int index);

		IImpulse GetImpulse(int index);

		IOperation GetImpulseTarget(int index);

		void SetImpulseTarget(int index, IOperation target);
	}
	public interface IInputList
	{
		int Count { get; }

		IOutput GetInputSource(int index);

		void AddInput(IOutput source);

		void SetInputSource(int index, IOutput source);

		void RemoveInput();

		void Clear();

		Type GetInputType(int index);

		DataClass GetDataClass(int index);

		object GetDefaultValue(int index);
	}
	public interface IListOperation : IOperation
	{
		int Index { get; }

		IOperationList List { get; }
	}
	public abstract class InputListBase : IInputList
	{
		private abstract class Input
		{
			public abstract IOutput GenericSource { get; set; }

			public abstract Type InputType { get; }

			public abstract DataClass DataClass { get; }

			public abstract object DefaultValue { get; }
		}

		private class Input<T> : Input, IInput<T>, IInput
		{
			public IOutput<T> Source { get; set; }

			public override Type InputType => typeof(T);

			public override IOutput GenericSource
			{
				get
				{
					return Source;
				}
				set
				{
					Source = (IOutput<T>)value;
				}
			}

			public override DataClass DataClass
			{
				get
				{
					if (!typeof(T).IsValueType)
					{
						return DataClass.Object;
					}
					return DataClass.Value;
				}
			}

			public override object DefaultValue => default(T);

			IOutput IInput.Source => Source;
		}

		private List<Input> _inputs = new List<Input>();

		public int Count => _inputs.Count;

		public IInput<T> GetInput<T>(int index)
		{
			return (IInput<T>)_inputs[index];
		}

		public IInput<T> AddInput<T>(IOutput<T> source = null)
		{
			Input<T> input = new Input<T>();
			input.Source = source;
			_inputs.Add(input);
			return input;
		}

		public void RemoveInput()
		{
			_inputs.RemoveAt(_inputs.Count - 1);
		}

		public void Clear()
		{
			_inputs.Clear();
		}

		public IOutput GetInputSource(int index)
		{
			return _inputs[index].GenericSource;
		}

		public void SetInputSource(int index, IOutput source)
		{
			SetInputSource(index, source, changeType: true);
		}

		public void SetInputSource(int index, IOutput source, bool changeType)
		{
			Input input = _inputs[index];
			if (source.OutputType != input.InputType)
			{
				if (!changeType)
				{
					throw new InvalidOperationException($"Type mismatch. Input is of type: {input.InputType}");
				}
				input = (Input)Activator.CreateInstance(typeof(Input<>).MakeGenericType(source.OutputType));
				_inputs[index] = input;
			}
			input.GenericSource = source;
		}

		public Type GetInputType(int index)
		{
			return _inputs[index].InputType;
		}

		public DataClass GetDataClass(int index)
		{
			return _inputs[index].DataClass;
		}

		public object GetDefaultValue(int index)
		{
			return _inputs[index].DefaultValue;
		}

		public void AddInput(IOutput source)
		{
			Input input = (Input)Activator.CreateInstance(typeof(Input<>).MakeGenericType(source.OutputType));
			input.GenericSource = source;
			_inputs.Add(input);
		}
	}
	public class InputList : InputListBase
	{
	}
	public class ArgumentList : InputListBase
	{
	}
	public interface IOperationList
	{
		int Count { get; }

		IOperation AddOperation();

		void RemoveOperation();

		IOperation GetOperation(int index);

		bool IsOperationAsync(int index);

		void Clear();
	}
	public interface IOutputList
	{
		Node Owner { get; }

		int Count { get; }

		IOutput GetOutput(int index);

		Type GetOutputType(int index);

		DataClass GetOutputClass(int index);

		IListOutput AddOutput();

		void RemoveOutput();

		void Clear();
	}
	public class MixedOperationList : IOperationList
	{
		public abstract class Operation : IOperation, IListOperation
		{
			public int Index { get; private set; }

			public MixedOperationList List { get; private set; }

			public Node OwnerNode => List.Owner;

			public abstract bool IsAsync { get; }

			IOperationList IListOperation.List => List;

			public Operation(int index, MixedOperationList list)
			{
				Index = index;
				List = list;
			}

			public override string ToString()
			{
				return $"{(IsAsync ? "Async" : "Sync")} Operation [{Index}] on {OwnerNode} ({GetHashCode()})";
			}
		}

		public class SyncOperation : Operation, ISyncOperation, IOperation
		{
			public override bool IsAsync => false;

			public SyncOperation(int index, MixedOperationList list)
				: base(index, list)
			{
			}
		}

		public class AsyncOperation : Operation, IAsyncOperation, IOperation
		{
			public override bool IsAsync => true;

			public AsyncOperation(int index, MixedOperationList list)
				: base(index, list)
			{
			}
		}

		private List<Operation> _operations = new List<Operation>();

		public Node Owner { get; private set; }

		public int Index { get; private set; }

		public int Count => _operations.Count;

		public ISyncOperation AddSyncOperation()
		{
			SyncOperation syncOperation = new SyncOperation(Count, this);
			_operations.Add(syncOperation);
			return syncOperation;
		}

		public IAsyncOperation AddAsyncOperation()
		{
			AsyncOperation asyncOperation = new AsyncOperation(Count, this);
			_operations.Add(asyncOperation);
			return asyncOperation;
		}

		public void RemoveOperation()
		{
			_operations.RemoveAt(_operations.Count - 1);
		}

		public IOperation GetOperation(int index)
		{
			return _operations[index];
		}

		public bool IsOperationAsync(int index)
		{
			return _operations[index].IsAsync;
		}

		public void Clear()
		{
			_operations.Clear();
		}

		public MixedOperationList(Node owner, int index)
		{
			Owner = owner;
			Index = index;
		}

		IOperation IOperationList.AddOperation()
		{
			throw new NotSupportedException("Cannot use AddOperation, because type of operation must be specified for the mixed list");
		}
	}
	public abstract class ObjectInputListBase<T> : IInputList
	{
		private List<IObjectOutput<T>> inputs = new List<IObjectOutput<T>>();

		public int Count => inputs.Count;

		public DataClass GetDataClass(int index)
		{
			return DataClass.Object;
		}

		public object GetDefaultValue(int index)
		{
			return default(T);
		}

		public Type GetInputType(int index)
		{
			return typeof(T);
		}

		public IOutput GetInputSource(int index)
		{
			return inputs[index];
		}

		public void SetInputSource(int index, IOutput source)
		{
			inputs[index] = (IObjectOutput<T>)source;
		}

		public void AddInput(IObjectOutput<T> source = null)
		{
			inputs.Add(source);
		}

		public void AddInput(IOutput source)
		{
			AddInput((IObjectOutput<T>)source);
		}

		public void RemoveInput()
		{
			inputs.RemoveAt(inputs.Count - 1);
		}

		public void Clear()
		{
			inputs.Clear();
		}
	}
	public class ObjectInputList<T> : ObjectInputListBase<T>
	{
	}
	public class ObjectArgumentList<T> : ObjectInputListBase<T>
	{
	}
	public class ObjectOutputList<T> : IOutputList
	{
		private List<ListObjectOutput<T>> _outputs = new List<ListObjectOutput<T>>();

		public Node Owner { get; private set; }

		public int Count => _outputs.Count;

		public ListObjectOutput<T> AddOutput()
		{
			ListObjectOutput<T> listObjectOutput = new ListObjectOutput<T>(Count, this);
			_outputs.Add(listObjectOutput);
			return listObjectOutput;
		}

		public void Clear()
		{
			_outputs.Clear();
		}

		public ListObjectOutput<T> GetOutput(int index)
		{
			return _outputs[index];
		}

		IOutput IOutputList.GetOutput(int index)
		{
			return _outputs[index];
		}

		public DataClass GetOutputClass(int index)
		{
			return DataClass.Object;
		}

		public Type GetOutputType(int index)
		{
			return typeof(T);
		}

		IListOutput IOutputList.AddOutput()
		{
			return AddOutput();
		}

		public void RemoveOutput()
		{
			_outputs.RemoveAt(_outputs.Count - 1);
		}

		public ObjectOutputList(Node owner)
		{
			Owner = owner;
		}
	}
	public interface IListOutput : IOutput
	{
		int Index { get; }

		IOutputList List { get; }
	}
	public class ListValueOutput<T> : ValueOutput<T>, IListOutput, IOutput where T : unmanaged
	{
		public IOutputList List { get; private set; }

		public int Index { get; private set; }

		public ListValueOutput(int index, IOutputList list)
			: base(list.Owner)
		{
			Index = index;
			List = list;
		}
	}
	public class ListObjectOutput<T> : ObjectOutput<T>, IListOutput, IOutput
	{
		public IOutputList List { get; private set; }

		public int Index { get; internal set; }

		public ListObjectOutput(int index, IOutputList list)
			: base(list.Owner)
		{
			Index = index;
			List = list;
		}
	}
	public class OutputList : IOutputList
	{
		private List<IOutput> _outputs = new List<IOutput>();

		public Node Owner { get; private set; }

		public int Count => _outputs.Count;

		public OutputList(Node owner)
		{
			Owner = owner;
		}

		public IOutput GetOutput(int index)
		{
			return _outputs[index];
		}

		public DataClass GetOutputClass(int index)
		{
			return _outputs[index].OutputDataClass;
		}

		public Type GetOutputType(int index)
		{
			return _outputs[index].OutputType;
		}

		public IListOutput AddOutputAuto(Type type)
		{
			if (type.IsValueType)
			{
				return (IListOutput)typeof(OutputList).GetMethod("AddValueOutput").MakeGenericMethod(type).Invoke(this, null);
			}
			return (IListOutput)typeof(OutputList).GetMethod("AddObjectOutput").MakeGenericMethod(type).Invoke(this, null);
		}

		public ListValueOutput<T> AddValueOutput<T>() where T : unmanaged
		{
			ListValueOutput<T> listValueOutput = new ListValueOutput<T>(_outputs.Count, this);
			_outputs.Add(listValueOutput);
			return listValueOutput;
		}

		public ListObjectOutput<T> AddObjectOutput<T>()
		{
			ListObjectOutput<T> listObjectOutput = new ListObjectOutput<T>(_outputs.Count, this);
			_outputs.Add(listObjectOutput);
			return listObjectOutput;
		}

		public ListValueOutput<T> GetValueOutput<T>(int index) where T : unmanaged
		{
			return (ListValueOutput<T>)_outputs[index];
		}

		public ListObjectOutput<T> GetObjectOutput<T>(int index)
		{
			return (ListObjectOutput<T>)_outputs[index];
		}

		public void RemoveOutput()
		{
			_outputs.RemoveAt(_outputs.Count - 1);
		}

		public void Clear()
		{
			_outputs.Clear();
		}

		public IListOutput AddOutput()
		{
			throw new NotSupportedException("Cannot add output to variable OutputList without specifying type");
		}
	}
	public class SyncOperationList : IOperationList
	{
		public class Operation : ISyncOperation, IOperation, IListOperation
		{
			public int Index { get; private set; }

			public SyncOperationList List { get; private set; }

			public Node OwnerNode => List.Owner;

			IOperationList IListOperation.List => List;

			public Operation(int index, SyncOperationList list)
			{
				Index = index;
				List = list;
			}

			public override string ToString()
			{
				return $"Operation [{Index}] on {OwnerNode} ({GetHashCode()})";
			}
		}

		private List<Operation> _operations = new List<Operation>();

		public Node Owner { get; private set; }

		public int Index { get; private set; }

		public int Count => _operations.Count;

		public ISyncOperation AddOperation()
		{
			Operation operation = new Operation(Count, this);
			_operations.Add(operation);
			return operation;
		}

		public void RemoveOperation()
		{
			_operations.RemoveAt(_operations.Count - 1);
		}

		public ISyncOperation GetOperation(int index)
		{
			return _operations[index];
		}

		public void Clear()
		{
			_operations.Clear();
		}

		public SyncOperationList(Node owner, int index)
		{
			Owner = owner;
			Index = index;
		}

		IOperation IOperationList.AddOperation()
		{
			return AddOperation();
		}

		IOperation IOperationList.GetOperation(int index)
		{
			return GetOperation(index);
		}

		bool IOperationList.IsOperationAsync(int index)
		{
			return false;
		}
	}
	public abstract class ValueInputListBase<T> : IInputList where T : unmanaged
	{
		private List<IValueOutput<T>> inputs = new List<IValueOutput<T>>();

		public int Count => inputs.Count;

		public DataClass GetDataClass(int index)
		{
			return DataClass.Value;
		}

		public object GetDefaultValue(int index)
		{
			return default(T);
		}

		public Type GetInputType(int index)
		{
			return typeof(T);
		}

		public IOutput GetInputSource(int index)
		{
			return inputs[index];
		}

		public void SetInputSource(int index, IOutput source)
		{
			inputs[index] = (IValueOutput<T>)source;
		}

		public void AddInput(IValueOutput<T> source = null)
		{
			inputs.Add(source);
		}

		public void AddInput(IOutput source)
		{
			AddInput((IValueOutput<T>)source);
		}

		public void RemoveInput()
		{
			inputs.RemoveAt(inputs.Count - 1);
		}

		public void Clear()
		{
			inputs.Clear();
		}
	}
	public class ValueInputList<T> : ValueInputListBase<T> where T : unmanaged
	{
	}
	public class ValueArgumentList<T> : ValueInputListBase<T> where T : unmanaged
	{
	}
	public class ValueOutputList<T> : IOutputList where T : unmanaged
	{
		private List<ListValueOutput<T>> _outputs = new List<ListValueOutput<T>>();

		public Node Owner { get; private set; }

		public int Count => _outputs.Count;

		public ListValueOutput<T> AddOutput()
		{
			ListValueOutput<T> listValueOutput = new ListValueOutput<T>(Count, this);
			_outputs.Add(listValueOutput);
			return listValueOutput;
		}

		public void Clear()
		{
			_outputs.Clear();
		}

		public ListValueOutput<T> GetOutput(int index)
		{
			return _outputs[index];
		}

		IOutput IOutputList.GetOutput(int index)
		{
			return _outputs[index];
		}

		public DataClass GetOutputClass(int index)
		{
			return DataClass.Value;
		}

		public Type GetOutputType(int index)
		{
			return typeof(T);
		}

		IListOutput IOutputList.AddOutput()
		{
			return AddOutput();
		}

		public void RemoveOutput()
		{
			_outputs.RemoveAt(_outputs.Count - 1);
		}

		public ValueOutputList(Node owner)
		{
			Owner = owner;
		}
	}
	public struct AsyncCall : IAsyncCall, IImpulse
	{
		public IOperation Target { get; set; }

		public ImpulseType ImpulseType => ImpulseType.AsyncCall;

		IOperation IImpulse.Target
		{
			get
			{
				return Target;
			}
			set
			{
				Target = value;
			}
		}
	}
	public class AsyncOperation : IAsyncOperation, IOperation
	{
		public Node OwnerNode { get; private set; }

		public int Index { get; private set; }

		public AsyncOperation(Node owner, int index)
		{
			OwnerNode = owner;
			Index = index;
		}

		public override string ToString()
		{
			return $"AsyncOperation [{Index}] {OwnerNode?.GetOperation(Index)} on {OwnerNode}";
		}
	}
	public struct AsyncResumption : IAsyncResumption, IResumption, IImpulse
	{
		public IOperation Target { get; set; }

		public ImpulseType ImpulseType => ImpulseType.AsyncResumption;
	}
	public struct Call : ICall, IImpulse
	{
		public ISyncOperation Target { get; set; }

		public ImpulseType ImpulseType => ImpulseType.Call;

		IOperation IImpulse.Target
		{
			get
			{
				return Target;
			}
			set
			{
				Target = (ISyncOperation)value;
			}
		}
	}
	public struct Continuation : IContinuation, IImpulse
	{
		public IOperation Target { get; set; }

		public ImpulseType ImpulseType => ImpulseType.Continuation;
	}
	public abstract class GlobalRef : IGlobalRef
	{
		public Node OwnerNode { get; private set; }

		public int Index { get; private set; }

		public abstract Global UntypedGlobal { get; }

		public abstract void ClearReference();

		public GlobalRef(Node owner, int index)
		{
			OwnerNode = owner;
			Index = index;
		}
	}
	public class GlobalRef<T> : GlobalRef, IGlobalRef<T>, IGlobalRef
	{
		private Global<T> _global;

		public override Global UntypedGlobal => Global;

		public Global<T> Global
		{
			get
			{
				return _global;
			}
			set
			{
				if (value != null && base.OwnerNode.Runtime != value.Runtime)
				{
					throw new InvalidOperationException("GlobalRef can only target globals present in the same runtime");
				}
				_global?.UnregisterListener(this);
				_global = value;
				_global?.RegisterListener(this);
			}
		}

		public GlobalRef(Node owner, int index)
			: base(owner, index)
		{
		}

		public override void ClearReference()
		{
			if (_global != null)
			{
				Global = null;
			}
		}
	}
	public static class GlobalRefHelper
	{
		public static T Read<T>(this GlobalRef<T> global, ProtoFlux.Runtimes.Execution.ExecutionContext context)
		{
			return context.CurrentScope.ReadGlobal(global);
		}

		public static bool Write<T>(this GlobalRef<T> global, T value, ProtoFlux.Runtimes.Execution.ExecutionContext context)
		{
			return context.CurrentScope.WriteGlobal(global, value);
		}
	}
	public struct ObjectArgument<T> : IObjectInput<T>, IInput<T>, IInput
	{
		public IObjectOutput<T> Source { get; set; }

		public Type InputType => typeof(T);

		IOutput IInput.Source => Source;

		IOutput<T> IInput<T>.Source
		{
			get
			{
				return Source;
			}
			set
			{
				Source = (IObjectOutput<T>)value;
			}
		}
	}
	public struct ObjectInput<T> : IObjectInput<T>, IInput<T>, IInput
	{
		public IObjectOutput<T> Source { get; set; }

		public Type InputType => typeof(T);

		IOutput IInput.Source => Source;

		IOutput<T> IInput<T>.Source
		{
			get
			{
				return Source;
			}
			set
			{
				Source = (IObjectOutput<T>)value;
			}
		}
	}
	public class ObjectOutput<T> : Output<T>, IObjectOutput<T>, IOutput<T>, IOutput
	{
		public override DataClass OutputDataClass => DataClass.Object;

		public ObjectOutput(Node owner)
			: base(owner)
		{
		}
	}
	public class Operation : ISyncOperation, IOperation
	{
		public Node OwnerNode { get; private set; }

		public int Index { get; private set; }

		public Operation(Node owner, int index)
		{
			OwnerNode = owner;
			Index = index;
		}

		public override string ToString()
		{
			return $"Operation [{Index}] {OwnerNode?.GetOperationName(Index)} on {OwnerNode}";
		}
	}
	public struct Reference<T> where T : INode
	{
		public T Target;
	}
	public struct SyncResumption : ISyncResumption, IResumption, IImpulse
	{
		public ISyncOperation Target { get; set; }

		public ImpulseType ImpulseType => ImpulseType.SyncResumption;

		IOperation IImpulse.Target
		{
			get
			{
				return Target;
			}
			set
			{
				Target = (ISyncOperation)value;
			}
		}
	}
	public struct ValueArgument<T> : IValueInput<T>, IInput<T>, IInput where T : unmanaged
	{
		public IValueOutput<T> Source { get; set; }

		public Type InputType => typeof(T);

		IOutput IInput.Source => Source;

		IOutput<T> IInput<T>.Source
		{
			get
			{
				return Source;
			}
			set
			{
				Source = (IValueOutput<T>)value;
			}
		}
	}
	public struct ValueInput<T> : IValueInput<T>, IInput<T>, IInput where T : unmanaged
	{
		public IValueOutput<T> Source { get; set; }

		public Type InputType => typeof(T);

		IOutput IInput.Source => Source;

		IOutput<T> IInput<T>.Source
		{
			get
			{
				return Source;
			}
			set
			{
				Source = (IValueOutput<T>)value;
			}
		}
	}
	public class ValueOutput<T> : Output<T>, IValueOutput<T>, IOutput<T>, IOutput where T : unmanaged
	{
		public override DataClass OutputDataClass => DataClass.Value;

		public ValueOutput(Node owner)
			: base(owner)
		{
		}
	}
	public interface IAsyncOperation : IOperation
	{
	}
	public interface IGlobalRef
	{
		Global UntypedGlobal { get; }

		void ClearReference();
	}
	public interface IGlobalRef<T> : IGlobalRef
	{
		Global<T> Global { get; set; }
	}
	public enum ImpulseType
	{
		Continuation,
		Call,
		AsyncCall,
		SyncResumption,
		AsyncResumption
	}
	public interface IImpulse
	{
		IOperation Target { get; set; }

		ImpulseType ImpulseType { get; }
	}
	public interface ICall : IImpulse
	{
		new ISyncOperation Target { get; set; }
	}
	public interface IAsyncCall : IImpulse
	{
	}
	public interface IContinuation : IImpulse
	{
	}
	public interface IResumption : IImpulse
	{
	}
	public interface ISyncResumption : IResumption, IImpulse
	{
		new ISyncOperation Target { get; set; }
	}
	public interface IAsyncResumption : IResumption, IImpulse
	{
	}
	public interface IInput
	{
		Type InputType { get; }

		IOutput Source { get; }
	}
	public interface IInput<T> : IInput
	{
		new IOutput<T> Source { get; set; }
	}
	public interface IValueInput<T> : IInput<T>, IInput where T : unmanaged
	{
		new IValueOutput<T> Source { get; set; }
	}
	public interface IObjectInput<T> : IInput<T>, IInput
	{
		new IObjectOutput<T> Source { get; set; }
	}
	public interface IOperation
	{
		Node OwnerNode { get; }
	}
	public interface IOutput
	{
		Node OwnerNode { get; }

		Type OutputType { get; }

		DataClass OutputDataClass { get; }
	}
	public interface IOutput<T> : IOutput
	{
	}
	public interface IObjectOutput<T> : IOutput<T>, IOutput
	{
	}
	public interface IValueOutput<T> : IOutput<T>, IOutput where T : unmanaged
	{
	}
	public interface ISyncOperation : IOperation
	{
	}
	public readonly struct UnmanagedNullable<T> where T : unmanaged
	{
		private readonly T _value;

		private readonly bool _hasValue;

		public T Value
		{
			get
			{
				if (!_hasValue)
				{
					throw new InvalidOperationException("UnmanagedNullable has no value!");
				}
				return _value;
			}
		}

		public bool HasValue => _hasValue;

		public UnmanagedNullable(T value)
		{
			_hasValue = true;
			_value = value;
		}

		public static implicit operator T?(UnmanagedNullable<T> unmanagedNullable)
		{
			if (unmanagedNullable.HasValue)
			{
				return unmanagedNullable.Value;
			}
			return null;
		}

		public static implicit operator UnmanagedNullable<T>(T? nullable)
		{
			if (nullable.HasValue)
			{
				return new UnmanagedNullable<T>(nullable.Value);
			}
			return default(UnmanagedNullable<T>);
		}

		public static implicit operator UnmanagedNullable<T>(T value)
		{
			return new UnmanagedNullable<T>(value);
		}
	}
	public interface IStore
	{
	}
	public interface IStore<T> : IStore
	{
		T Read(ProtoFlux.Runtimes.Execution.ExecutionContext context);

		void Write(T value, ProtoFlux.Runtimes.Execution.ExecutionContext context);
	}
	public static class StoreHelpers
	{
		public static void Clear<T>(this IStore<T> store, ProtoFlux.Runtimes.Execution.ExecutionContext context)
		{
			store.Write(default(T), context);
		}
	}
	public struct ObjectStore<T> : IStore<T>, IStore
	{
		internal int offset;

		public int Offset => offset;

		public T Read(ProtoFlux.Runtimes.Execution.ExecutionContext context)
		{
			return context.ReadStoredObject<T>(offset);
		}

		public void Write(T value, ProtoFlux.Runtimes.Execution.ExecutionContext context)
		{
			context.WriteStoredObject(offset, value);
		}
	}
	public struct ValueStore<T> : IStore<T>, IStore where T : unmanaged
	{
		internal int offset;

		public int Offset => offset;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T Read(ProtoFlux.Runtimes.Execution.ExecutionContext context)
		{
			return context.ReadStoredValue<T>(offset);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Write(T value, ProtoFlux.Runtimes.Execution.ExecutionContext context)
		{
			context.WriteStoredValue(offset, value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ref T Access(ProtoFlux.Runtimes.Execution.ExecutionContext context)
		{
			return ref context.AccessStoredValue<T>(offset);
		}
	}
	public abstract class CrossRuntimeInputAttribute : Attribute
	{
		public abstract bool IsValidTargetRuntime(NodeRuntime runtime);
	}
	public interface INestedNode : INode
	{
		NodeGroup TargetGroup { get; }

		NodeRuntime TargetRuntime { get; }

		IOutput GetTargetExport(IOutput output);

		IOutput GetImportSource(IOutput import);
	}
	public readonly struct ElementRef
	{
		public readonly short index;

		public readonly short listIndex;

		public bool IsDynamic => listIndex >= 0;

		public bool AddToList
		{
			get
			{
				if (IsDynamic)
				{
					return index < 0;
				}
				return false;
			}
		}

		public ElementRef(int index)
		{
			this.index = (short)index;
			listIndex = -1;
		}

		public ElementRef(int listIndex, int index)
		{
			this.index = (short)index;
			this.listIndex = (short)listIndex;
		}

		public override string ToString()
		{
			if (IsDynamic)
			{
				if (AddToList)
				{
					return $"List[{listIndex}]->Add";
				}
				return $"List[{listIndex}][{index}]";
			}
			return $"Fixed[{index}]";
		}
	}
	public class AsyncCallExport : ImpulseExport, IAsyncOperation, IOperation
	{
		public AsyncCallExport(ImpulseExportNode node, int index)
			: base(node, index)
		{
		}
	}
	public class CallExport : ImpulseExport, ISyncOperation, IOperation
	{
		public CallExport(ImpulseExportNode node, int index)
			: base(node, index)
		{
		}
	}
	public class ContinuationExport : ImpulseExport
	{
		public ContinuationExport(ImpulseExportNode node, int index)
			: base(node, index)
		{
		}
	}
	public abstract class ImpulseExport : IOperation
	{
		public Node OwnerNode { get; private set; }

		public int Index { get; private set; }

		public ImpulseExport(ImpulseExportNode node, int index)
		{
			OwnerNode = node;
			Index = index;
		}
	}
	public class ImpulseExportNode : Node
	{
	}
	public class DataImportNode : Node
	{
	}
	public abstract class Global
	{
		public NodeRuntime Runtime { get; private set; }

		public int Index { get; internal set; }

		public string Name { get; private set; }

		public abstract Type ValueType { get; }

		public abstract int ListenerCount { get; }

		internal abstract void AddMatchingTypeToList(GlobalRefList list);

		internal abstract void UpdateToInitialValue<C>(C context) where C : ProtoFlux.Runtimes.Execution.ExecutionContext;

		internal abstract void ResetValueToDefault<C>(C context) where C : ProtoFlux.Runtimes.Execution.ExecutionContext;

		public Global(NodeRuntime runtime, int index, string name)
		{
			Runtime = runtime;
			Index = index;
			Name = name;
		}
	}
	public class Global<T> : Global
	{
		private List<GlobalRef<T>> _listeners;

		public override Type ValueType => typeof(T);

		public override int ListenerCount => _listeners?.Count ?? 0;

		public Global(NodeRuntime runtime, int index, string name)
			: base(runtime, index, name)
		{
		}

		internal void ValueChanged<C>(T value, C context) where C : ProtoFlux.Runtimes.Execution.ExecutionContext
		{
			if (context.IsEmpty)
			{
				throw new InvalidOperationException("Cannot call ValueChanged with an empty context!");
			}
			if (context.CurrentRuntime != base.Runtime)
			{
				throw new InvalidOperationException("Cannot call ValueChanged with the context that's currently in a different runtime.");
			}
			if (_listeners == null)
			{
				return;
			}
			foreach (GlobalRef<T> listener in _listeners)
			{
				IExecutionNode<C> executionNode = (IExecutionNode<C>)listener.OwnerNode;
				if (listener is ListGlobalRef<T> listGlobalRef)
				{
					executionNode.ListGlobalChanged(listGlobalRef.List.Index, listGlobalRef.Index, value, context);
				}
				else
				{
					executionNode.GlobalChanged(listener.Index, value, context);
				}
			}
		}

		internal override void UpdateToInitialValue<C>(C context)
		{
			ValueChanged(context.CurrentScope.ReadGlobal<T>(base.Index), context);
		}

		internal override void ResetValueToDefault<C>(C context)
		{
			ValueChanged(default(T), context);
		}

		internal void RegisterListener(GlobalRef<T> listener)
		{
			if (_listeners == null)
			{
				_listeners = new List<GlobalRef<T>>();
			}
			_listeners.Add(listener);
		}

		internal void UnregisterListener(GlobalRef<T> listener)
		{
			if (!_listeners.Remove(listener))
			{
				throw new InvalidOperationException("Given listener wasn't registered");
			}
		}

		public override string ToString()
		{
			return $"Global \"{base.Name}\" ({typeof(T).Name}). Listeners: {ListenerCount}";
		}

		internal override void AddMatchingTypeToList(GlobalRefList list)
		{
			list.AddGlobalRef<T>();
		}
	}
	public struct ImpulseImport
	{
		public readonly IOperation target;

		public readonly bool isAsync;

		public ImpulseImport(IOperation target, bool isAsync)
		{
			this.target = target;
			this.isAsync = isAsync;
		}
	}
	public class ImpulseMetadata : IElementMetadata
	{
		public int Index { get; internal set; }

		public string Name { get; internal set; }

		public ImpulseType Type { get; internal set; }

		public FieldInfo Field { get; internal set; }

		public ImpulseMetadata(int index, FieldInfo field)
		{
			Index = index;
			Name = field.Name;
			Field = field;
			if (field.FieldType == typeof(Continuation))
			{
				Type = ImpulseType.Continuation;
				return;
			}
			if (field.FieldType == typeof(Call))
			{
				Type = ImpulseType.Call;
				return;
			}
			if (field.FieldType == typeof(AsyncCall))
			{
				Type = ImpulseType.AsyncCall;
				return;
			}
			if (field.FieldType == typeof(SyncResumption))
			{
				Type = ImpulseType.SyncResumption;
				return;
			}
			if (field.FieldType == typeof(AsyncResumption))
			{
				Type = ImpulseType.AsyncResumption;
				return;
			}
			throw new NotImplementedException("Unsupported type of call: " + field.FieldType);
		}
	}
	public class GlobalRefListMetadata : IElementMetadata
	{
		public int Index { get; internal set; }

		public string Name { get; internal set; }

		public FieldInfo Field { get; internal set; }

		public GlobalRefListMetadata(int index, FieldInfo field)
		{
			Index = index;
			Name = field.Name;
			Field = field;
		}
	}
	public class GlobalRefMetadata : IElementMetadata
	{
		public int Index { get; internal set; }

		public string Name { get; internal set; }

		public Type ValueType { get; internal set; }

		public FieldInfo Field { get; internal set; }

		public GlobalRefMetadata(int index, FieldInfo field)
		{
			Index = index;
			Name = field.Name;
			Field = field;
			ValueType = field.FieldType.GetGenericArguments()[0];
		}
	}
	public interface IElementMetadata
	{
		int Index { get; }

		string Name { get; }
	}
	public class ImpulseListMetadata : IElementMetadata
	{
		public int Index { get; internal set; }

		public string Name { get; internal set; }

		public ImpulseType? Type { get; internal set; }

		public FieldInfo Field { get; internal set; }

		public ImpulseListMetadata(int index, FieldInfo field)
		{
			Index = index;
			Name = field.Name;
			Field = field;
			if (field.FieldType == typeof(ContinuationList))
			{
				Type = ImpulseType.Continuation;
			}
			else if (field.FieldType == typeof(CallList))
			{
				Type = ImpulseType.Call;
			}
			else if (field.FieldType == typeof(AsyncCallList))
			{
				Type = ImpulseType.AsyncCall;
			}
		}
	}
	public class InputListMetadata : InputMetadataBase
	{
		public Type TypeConstraint { get; internal set; }

		public DataClass? DataClassConstraint { get; internal set; }

		public InputListMetadata(int index, FieldInfo field, object defaultValue)
			: base(index, field, defaultValue)
		{
			if (!field.FieldType.IsGenericType)
			{
				return;
			}
			TypeConstraint = field.FieldType.GetGenericArguments()[0];
			Type genericTypeDefinition = field.FieldType.GetGenericTypeDefinition();
			if (genericTypeDefinition == typeof(ValueInputList<>) || genericTypeDefinition == typeof(ValueArgumentList<>))
			{
				DataClassConstraint = DataClass.Value;
				return;
			}
			if (genericTypeDefinition == typeof(ObjectInputList<>) || genericTypeDefinition == typeof(ObjectArgumentList<>))
			{
				DataClassConstraint = DataClass.Object;
				return;
			}
			throw new NotImplementedException("Unsupported list type: " + genericTypeDefinition);
		}
	}
	public class InputMetadata : InputMetadataBase
	{
		public DataClass DataClass { get; internal set; }

		public Type InputType { get; internal set; }

		public InputMetadata(int index, FieldInfo field, DataClass dataClass, object defaultValue)
			: base(index, field, defaultValue)
		{
			DataClass = dataClass;
			InputType = field.FieldType.GenericTypeArguments[0];
		}
	}
	public abstract class InputMetadataBase : IElementMetadata
	{
		public int Index { get; internal set; }

		public string Name { get; internal set; }

		public FieldInfo Field { get; internal set; }

		public object DefaultValue { get; internal set; }

		public bool IsConditional { get; internal set; }

		public bool? IsListeningToChanges { get; internal set; }

		public PropertyInfo IsListeningToChangesEval { get; internal set; }

		public CrossRuntimeInputAttribute CrossRuntime { get; internal set; }

		public bool IsPotentiallyListeningToChanges
		{
			get
			{
				if (IsListeningToChanges != true)
				{
					return IsListeningToChangesEval != null;
				}
				return true;
			}
		}

		public InputMetadataBase(int index, FieldInfo field, object defaultValue)
		{
			Index = index;
			Name = field.Name;
			Field = field;
			IsConditional = !field.FieldType.Name.Contains("Argument");
			CrossRuntime = field.GetCustomAttribute<CrossRuntimeInputAttribute>(inherit: true);
			DefaultValue = defaultValue;
			IsListeningToChangesEval = field.DeclaringType.GetProperty(field.Name + "ListensToChanges", BindingFlags.Instance | BindingFlags.Public);
			if (IsListeningToChangesEval == null)
			{
				IsListeningToChanges = field.GetCustomAttribute<ChangeListenerAttribute>() != null;
			}
		}
	}
	public class NodeMetadata
	{
		private Dictionary<string, InputMetadata> _inputsByName;

		private Dictionary<string, OutputMetadata> _outputsByName;

		private Dictionary<string, InputListMetadata> _inputListsByName;

		private Dictionary<string, OutputListMetadata> _outputListsByName;

		private Dictionary<string, ImpulseMetadata> _impulsesByName;

		private Dictionary<string, OperationMetadata> _actionsByName;

		private Dictionary<string, ImpulseListMetadata> _impulseListsByName;

		private Dictionary<string, OperationListMetadata> _actionListsByName;

		private Dictionary<string, ReferenceMetadata> _referencesByName;

		public string Name { get; internal set; }

		public string Overload { get; internal set; }

		public bool IsPassthrough { get; internal set; }

		public bool IsPotentiallyListeningToChanges { get; internal set; }

		public int FixedInputCount => FixedInputs.Count;

		public int FixedOutputCount => FixedOutputs.Count;

		public int FixedImpulseCount => FixedImpulses.Count;

		public int FixedOperationCount => FixedOperations.Count;

		public int FixedReferenceCount => FixedReferences.Count;

		public int FixedGlobalRefCount => FixedGlobalRefs.Count;

		public int DynamicInputCount => DynamicInputs.Count;

		public int DynamicOutputCount => DynamicOutputs.Count;

		public int DynamicImpulseCount => DynamicImpulses.Count;

		public int DynamicOperationCount => DynamicOperations.Count;

		public int DynamicGlobalRefCount => DynamicGlobalRefs.Count;

		public List<InputMetadata> FixedInputs { get; private set; } = new List<InputMetadata>();

		public List<OutputMetadata> FixedOutputs { get; private set; } = new List<OutputMetadata>();

		public List<ImpulseMetadata> FixedImpulses { get; private set; } = new List<ImpulseMetadata>();

		public List<OperationMetadata> FixedOperations { get; private set; } = new List<OperationMetadata>();

		public List<ReferenceMetadata> FixedReferences { get; private set; } = new List<ReferenceMetadata>();

		public List<GlobalRefMetadata> FixedGlobalRefs { get; private set; } = new List<GlobalRefMetadata>();

		public List<InputListMetadata> DynamicInputs { get; private set; } = new List<InputListMetadata>();

		public List<OutputListMetadata> DynamicOutputs { get; private set; } = new List<OutputListMetadata>();

		public List<ImpulseListMetadata> DynamicImpulses { get; private set; } = new List<ImpulseListMetadata>();

		public List<OperationListMetadata> DynamicOperations { get; private set; } = new List<OperationListMetadata>();

		public List<GlobalRefListMetadata> DynamicGlobalRefs { get; private set; } = new List<GlobalRefListMetadata>();

		public bool HasDynamicInputs => DynamicInputs.Count > 0;

		public bool HasDynamicOutputs => DynamicOutputs.Count > 0;

		public bool HasDynamicImpulses => DynamicImpulses.Count > 0;

		public bool HasDynamicActions => DynamicOperations.Count > 0;

		public bool HasDynamicGlobalRefs => DynamicGlobalRefs.Count > 0;

		public int FixedArgumentCount { get; private set; }

		public int FixedContinuationCount { get; private set; }

		public int FixedCallCount { get; private set; }

		public int FixedAsyncCallCount { get; private set; }

		public int FixedSyncOperationCount { get; private set; }

		public int FixedAsyncOperationCount { get; private set; }

		private T GetElementByName<T>(string name, List<T> list, ref Dictionary<string, T> dict) where T : class, IElementMetadata
		{
			if (list.Count == 0)
			{
				return null;
			}
			if (list.Count < 4)
			{
				for (int i = 0; i < list.Count; i++)
				{
					if (list[i].Name == name)
					{
						return list[i];
					}
				}
				return null;
			}
			if (dict == null)
			{
				Dictionary<string, T> dictionary = new Dictionary<string, T>();
				foreach (T item in list)
				{
					dictionary.Add(item.Name, item);
				}
				dict = dictionary;
			}
			if (dict.TryGetValue(name, out var value))
			{
				return value;
			}
			return null;
		}

		public void ComputeMetadata()
		{
			FixedArgumentCount = FixedInputs.Count((InputMetadata c) => !c.IsConditional);
			FixedContinuationCount = FixedImpulses.Count((ImpulseMetadata c) => c.Type == ImpulseType.Continuation);
			FixedCallCount = FixedImpulses.Count((ImpulseMetadata c) => c.Type == ImpulseType.Call);
			FixedAsyncCallCount = FixedImpulses.Count((ImpulseMetadata c) => c.Type == ImpulseType.AsyncCall);
			FixedSyncOperationCount = FixedOperations.Count((OperationMetadata c) => !c.IsAsync);
			FixedAsyncOperationCount = FixedOperations.Count((OperationMetadata c) => c.IsAsync);
		}

		public InputMetadata GetInputByName(string name)
		{
			return GetElementByName(name, FixedInputs, ref _inputsByName);
		}

		public OutputMetadata GetOutputByName(string name)
		{
			return GetElementByName(name, FixedOutputs, ref _outputsByName);
		}

		public ImpulseMetadata GetImpulseByName(string name)
		{
			return GetElementByName(name, FixedImpulses, ref _impulsesByName);
		}

		public OperationMetadata GetOperationByName(string name)
		{
			return GetElementByName(name, FixedOperations, ref _actionsByName);
		}

		public InputListMetadata GetInputListByName(string name)
		{
			return GetElementByName(name, DynamicInputs, ref _inputListsByName);
		}

		public OutputListMetadata GetOutputListByName(string name)
		{
			return GetElementByName(name, DynamicOutputs, ref _outputListsByName);
		}

		public ImpulseListMetadata GetImpulseListByName(string name)
		{
			return GetElementByName(name, DynamicImpulses, ref _impulseListsByName);
		}

		public OperationListMetadata GetOperationListByName(string name)
		{
			return GetElementByName(name, DynamicOperations, ref _actionListsByName);
		}

		public ReferenceMetadata GetReferenceByName(string name)
		{
			return GetElementByName(name, FixedReferences, ref _referencesByName);
		}
	}
	public static class NodeMetadataHelper
	{
		private static ConcurrentDictionary<Type, NodeMetadata> _metadataCache = new ConcurrentDictionary<Type, NodeMetadata>();

		public static NodeMetadata GetMetadata(Type type)
		{
			if (_metadataCache.TryGetValue(type, out var value))
			{
				return value;
			}
			value = new NodeMetadata();
			NodeOverloadAttribute customAttribute = type.GetCustomAttribute<NodeOverloadAttribute>();
			if (customAttribute != null)
			{
				value.Overload = customAttribute.OverloadName;
			}
			NodeNameAttribute customAttribute2 = type.GetCustomAttribute<NodeNameAttribute>();
			if (customAttribute2 != null)
			{
				value.Name = customAttribute2.Name;
			}
			else if (value.Overload != null)
			{
				int num = value.Overload.LastIndexOf('.');
				if (num >= 0)
				{
					value.Name = value.Overload.Substring(num + 1);
				}
				else
				{
					value.Name = value.Overload;
				}
			}
			foreach (FieldInfo field in type.EnumerateAllInstanceFields(BindingFlags.Instance | BindingFlags.Public))
			{
				object[] customAttributes = field.GetCustomAttributes(inherit: true);
				object obj = null;
				object[] array = customAttributes;
				foreach (object obj2 in array)
				{
					if (obj2.GetType() == typeof(DefaultValueAttribute))
					{
						obj = ((DefaultValueAttribute)obj2).Value;
						break;
					}
				}
				if (obj == null)
				{
					PropertyInfo propertyInfo = type.EnumerateAllProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault((PropertyInfo p) => p.Name == field.Name + "Default");
					if (propertyInfo != null && !propertyInfo.PropertyType.ContainsGenericParameters && propertyInfo.PropertyType == field.FieldType.GetGenericArguments()[0])
					{
						obj = propertyInfo.GetValue(null);
					}
				}
				if (typeof(IInputList).IsAssignableFrom(field.FieldType))
				{
					value.DynamicInputs.Add(new InputListMetadata(value.DynamicInputCount, field, obj));
				}
				else if (typeof(IOutputList).IsAssignableFrom(field.FieldType))
				{
					value.DynamicOutputs.Add(new OutputListMetadata(value.DynamicOutputCount, field));
				}
				else if (field.FieldType.IsGenericType)
				{
					Type genericTypeDefinition = field.FieldType.GetGenericTypeDefinition();
					if (genericTypeDefinition == typeof(ValueArgument<>) || genericTypeDefinition == typeof(ValueInput<>))
					{
						if (obj == null && !field.FieldType.ContainsGenericParameters)
						{
							obj = Activator.CreateInstance(field.FieldType.GenericTypeArguments[0]);
						}
						value.FixedInputs.Add(new InputMetadata(value.FixedInputs.Count, field, DataClass.Value, obj));
					}
					else if (genericTypeDefinition == typeof(ObjectArgument<>) || genericTypeDefinition == typeof(ObjectInput<>))
					{
						value.FixedInputs.Add(new InputMetadata(value.FixedInputs.Count, field, DataClass.Object, obj));
					}
					else if (genericTypeDefinition == typeof(ValueOutput<>))
					{
						value.FixedOutputs.Add(new OutputMetadata(value.FixedOutputCount, field, DataClass.Value));
					}
					else if (genericTypeDefinition == typeof(ObjectOutput<>))
					{
						value.FixedOutputs.Add(new OutputMetadata(value.FixedOutputCount, field, DataClass.Object));
					}
					else if (genericTypeDefinition == typeof(Reference<>))
					{
						value.FixedReferences.Add(new ReferenceMetadata(value.FixedReferenceCount, field));
					}
					else if (genericTypeDefinition == typeof(GlobalRef<>))
					{
						value.FixedGlobalRefs.Add(new GlobalRefMetadata(value.FixedGlobalRefCount, field));
					}
				}
				else if (typeof(IImpulse).IsAssignableFrom(field.FieldType))
				{
					value.FixedImpulses.Add(new ImpulseMetadata(value.FixedImpulseCount, field));
				}
				else if (typeof(IImpulseList).IsAssignableFrom(field.FieldType))
				{
					value.DynamicImpulses.Add(new ImpulseListMetadata(value.DynamicImpulseCount, field));
				}
				else if (field.FieldType == typeof(Operation))
				{
					value.FixedOperations.Add(new OperationMetadata(value.FixedOperationCount, field));
				}
				else if (field.FieldType == typeof(SyncOperationList))
				{
					value.DynamicOperations.Add(new OperationListMetadata(value.DynamicOperationCount, field, supportsSync: true, supportsAsync: false));
				}
				else if (field.FieldType == typeof(AsyncOperation))
				{
					value.FixedOperations.Add(new OperationMetadata(value.FixedOperationCount, field));
				}
				else if (field.FieldType == typeof(AsyncOperationList))
				{
					value.DynamicOperations.Add(new OperationListMetadata(value.DynamicOperationCount, field, supportsSync: false, supportsAsync: true));
				}
				else if (field.FieldType == typeof(MixedOperationList))
				{
					value.DynamicOperations.Add(new OperationListMetadata(value.DynamicOperationCount, field, supportsSync: true, supportsAsync: true));
				}
				else if (field.FieldType == typeof(GlobalRefList))
				{
					value.DynamicGlobalRefs.Add(new GlobalRefListMetadata(value.DynamicGlobalRefCount, field));
				}
			}
			Type[] interfaces = type.GetInterfaces();
			Type[] array2 = interfaces;
			foreach (Type type2 in array2)
			{
				if (type2.IsGenericType && type2.GetGenericTypeDefinition() == typeof(IOutput<>))
				{
					DataClass dataClass = ((!type2.GenericTypeArguments[0].IsValueType) ? DataClass.Object : DataClass.Value);
					if (interfaces.Any((Type ii) => ii.IsGenericType && ii.GetGenericTypeDefinition() == typeof(IValueOutput<>)))
					{
						dataClass = DataClass.Value;
					}
					else if (interfaces.Any((Type ii) => ii.IsGenericType && ii.GetGenericTypeDefinition() == typeof(IObjectOutput<>)))
					{
						dataClass = DataClass.Object;
					}
					value.FixedOutputs.Add(new OutputMetadata(value.FixedOutputCount, type, type2.GenericTypeArguments[0], dataClass));
				}
				if (type2 == typeof(ISyncOperation))
				{
					value.FixedOperations.Add(new OperationMetadata(value.FixedOperationCount, isAsync: false, type));
				}
				else if (type2 == typeof(IAsyncOperation))
				{
					value.FixedOperations.Add(new OperationMetadata(value.FixedOperationCount, isAsync: true, type));
				}
			}
			if (type.GetCustomAttribute<PassthroughNodeAttribute>() != null)
			{
				if (value.FixedInputCount != 1 && value.FixedOutputCount != 1 && value.DynamicInputCount > 0 && value.DynamicOutputCount > 0)
				{
					throw new Exception("Passthrough nodes must have exactly 1 input and 1 output");
				}
				value.IsPassthrough = true;
			}
			value.IsPotentiallyListeningToChanges = value.FixedInputs.Any((InputMetadata inputMetadata) => inputMetadata.IsPotentiallyListeningToChanges) || value.DynamicInputs.Any((InputListMetadata inputListMetadata) => inputListMetadata.IsPotentiallyListeningToChanges);
			value.ComputeMetadata();
			_metadataCache.TryAdd(type, value);
			return value;
		}
	}
	public class OperationListMetadata : IElementMetadata
	{
		public int Index { get; internal set; }

		public string Name { get; internal set; }

		public FieldInfo Field { get; internal set; }

		public bool SupportsSync { get; internal set; }

		public bool SupportsAsync { get; internal set; }

		public PossibleContinuationsAttribute PossibleContinuations { get; internal set; }

		public OperationListMetadata(int index, FieldInfo field, bool supportsSync, bool supportsAsync)
		{
			Index = index;
			Name = field.Name;
			Field = field;
			SupportsSync = supportsSync;
			SupportsAsync = supportsAsync;
			PossibleContinuations = field.GetCustomAttribute<PossibleContinuationsAttribute>();
		}
	}
	public class OperationMetadata : IElementMetadata
	{
		public int Index { get; internal set; }

		public string Name { get; internal set; }

		public FieldInfo Field { get; internal set; }

		public bool IsSelf => Name == "*";

		public bool IsAsync { get; internal set; }

		public PossibleContinuationsAttribute PossibleContinuations { get; internal set; }

		public OperationMetadata(int index, bool isAsync, Type nodeClass)
		{
			Index = index;
			Name = "*";
			IsAsync = isAsync;
			PossibleContinuations = nodeClass.GetCustomAttribute<PossibleContinuationsAttribute>();
		}

		public OperationMetadata(int index, FieldInfo field)
		{
			Index = index;
			Name = field.Name;
			Field = field;
			if (field.FieldType == typeof(AsyncOperation))
			{
				IsAsync = true;
			}
			PossibleContinuations = field.GetCustomAttribute<PossibleContinuationsAttribute>();
		}
	}
	public class OutputListMetadata : OutputMetadataBase
	{
		public Type TypeConstraint { get; internal set; }

		public DataClass? DataClassConstraint { get; internal set; }

		public OutputListMetadata(int index, FieldInfo field)
			: base(index, field.DeclaringType, field)
		{
			if (!field.FieldType.IsGenericType)
			{
				return;
			}
			TypeConstraint = field.FieldType.GetGenericArguments()[0];
			Type genericTypeDefinition = field.FieldType.GetGenericTypeDefinition();
			if (genericTypeDefinition == typeof(ValueOutputList<>))
			{
				DataClassConstraint = DataClass.Value;
				return;
			}
			if (genericTypeDefinition == typeof(ObjectOutputList<>))
			{
				DataClassConstraint = DataClass.Object;
				return;
			}
			throw new NotImplementedException("Unsupported list type: " + genericTypeDefinition);
		}
	}
	public class OutputMetadata : OutputMetadataBase
	{
		public Type OutputType { get; private set; }

		public DataClass DataClass { get; private set; }

		public bool IsImplicit { get; private set; }

		public OutputMetadata(int index, Type ownerType, Type outputType, DataClass dataClass)
			: base(index, ownerType, null)
		{
			OutputType = outputType;
			DataClass = dataClass;
			IsImplicit = true;
		}

		public OutputMetadata(int index, FieldInfo field, DataClass dataClass)
			: base(index, field.DeclaringType, field)
		{
			OutputType = field.FieldType.GenericTypeArguments[0];
			DataClass = dataClass;
			IsImplicit = false;
		}
	}
	public enum OutputChangeSource
	{
		Passthrough,
		Individual,
		Continuous,
		Dynamic
	}
	public abstract class OutputMetadataBase : IElementMetadata
	{
		public int Index { get; private set; }

		public string Name { get; private set; }

		public FieldInfo Field { get; private set; }

		public PropertyInfo ChangeTypeEval { get; private set; }

		public OutputChangeSource ChangeType { get; private set; }

		public OutputMetadataBase(int index, Type ownerType, FieldInfo field)
		{
			Index = index;
			Name = field?.Name ?? "*";
			Field = field;
			PropertyInfo property = ownerType.GetProperty((!(field == null)) ? (field.Name + "ChangeType") : "OutputChangeType", BindingFlags.Instance | BindingFlags.Public);
			if (property != null)
			{
				ChangeType = OutputChangeSource.Dynamic;
				ChangeTypeEval = property;
				return;
			}
			ChangeType = OutputChangeSource.Passthrough;
			if (field != null)
			{
				if (field.GetCustomAttribute<ContinuouslyChangingAttribute>() != null)
				{
					ChangeType = OutputChangeSource.Continuous;
				}
				else if (field.GetCustomAttribute<ChangeSourceAttribute>() != null)
				{
					ChangeType = OutputChangeSource.Individual;
				}
			}
			else if (ownerType.GetCustomAttribute<ContinuouslyChangingAttribute>() != null)
			{
				ChangeType = OutputChangeSource.Continuous;
			}
			else if (ownerType.GetCustomAttribute<ChangeSourceAttribute>() != null)
			{
				ChangeType = OutputChangeSource.Individual;
			}
		}
	}
	public class ReferenceMetadata : IElementMetadata
	{
		public int Index { get; internal set; }

		public string Name { get; internal set; }

		public Type ReferenceType { get; internal set; }

		public FieldInfo Field { get; internal set; }

		public ReferenceMetadata(int index, FieldInfo field)
		{
			Index = index;
			Name = field.Name;
			Field = field;
			ReferenceType = field.FieldType.GetGenericArguments()[0];
		}
	}
	public interface INode
	{
		NodeRuntime Runtime { get; }

		NodeMetadata Metadata { get; }

		int IndexInGroup { get; set; }

		int AllocationIndex { get; }

		string Overload { get; }

		bool IsPassthrough { get; }

		bool ListensToChanges { get; }

		int InputCount { get; }

		int OutputCount { get; }

		int ArgumentCount { get; }

		int ImpulseCount { get; }

		int OperationCount { get; }

		int FixedInputCount { get; }

		int FixedOutputCount { get; }

		int FixedImpulseCount { get; }

		int FixedOperationCount { get; }

		int FixedReferenceCount { get; }

		int FixedGlobalRefCount { get; }

		int DynamicInputCount { get; }

		int DynamicOutputCount { get; }

		int DynamicImpulseCount { get; }

		int DynamicOperationCount { get; }

		void Initialize(NodeRuntime runtime, int allocationIndex);

		void Dispose();

		void SetInputSource(ElementRef input, IOutput source);

		IOutput GetInputSource(int index);

		void SetInputSource(int index, IOutput source);

		string GetInputName(int index);

		Type GetInputType(int index);

		DataClass GetInputTypeClass(int index);

		object GetInputDefaultValue(int index);

		bool IsInputConditional(int index);

		bool IsInputListeningToChanges(int index);

		CrossRuntimeInputAttribute GetInputCrossRuntime(int index);

		IInputList GetInputList(int index);

		string GetInputListName(int index);

		Type GetInputListTypeConstraint(int index);

		IOutput GetOutput(int index);

		string GetOutputName(int index);

		Type GetOutputType(int index);

		DataClass GetOutputTypeClass(int index);

		bool IsOutputImplicit(int index);

		OutputChangeSource GetOutputChangeType(int index);

		IOutputList GetOutputList(int index);

		string GetOutputListName(int index);

		string GetImpulseName(int index);

		IOperation GetImpulseTarget(int index);

		void SetImpulseTarget(int index, IOperation target);

		IOperation GetOperation(int index);

		string GetOperationName(int index);

		bool IsOperationAsync(int index);

		bool CanOperationContinueTo(int index, string impulseName);

		IImpulseList GetImpulseList(int index);

		string GetImpulseListName(int index);

		bool CanOperationListContinueTo(int index, string impulseName);

		IOperationList GetOperationList(int index);

		string GetOperationListName(int index);

		ImpulseType GetImpulseType(int index);

		bool OperationHasSingleContinuation(int index);

		bool OperationHasSyncAsyncTransition(int index);

		string GetReferenceName(int index);

		Type GetReferenceType(int index);

		INode GetReferenceTarget(int index);

		void SetReferenceTarget(int index, INode target);

		bool TrySetReferenceTarget(int index, INode target);

		string GetGlobalRefName(int index);

		Type GetGlobalRefValueType(int index);

		Global GetGlobalRefBinding(int index);

		void SetGlobalRefBinding(int index, Global binding);

		bool TrySetGlobalRefBinding(int index, Global binding);

		void CopyDynamicOutputLayout(INode source);

		void CopyDynamicOperationLayout(INode source);
	}
	public abstract class Node : INode
	{
		public NodeRuntime Runtime { get; private set; }

		public int IndexInGroup { get; set; }

		public int AllocationIndex { get; private set; }

		public virtual NodeMetadata Metadata => NodeMetadataHelper.GetMetadata(GetType());

		public virtual string Overload => Metadata.Overload;

		public virtual bool IsPassthrough => Metadata.IsPassthrough;

		public virtual bool ListensToChanges => Metadata.IsPotentiallyListeningToChanges;

		public virtual int InputCount
		{
			get
			{
				NodeMetadata metadata = Metadata;
				if (!metadata.HasDynamicInputs)
				{
					return metadata.FixedInputCount;
				}
				int num = metadata.FixedInputCount;
				for (int i = 0; i < metadata.DynamicInputs.Count; i++)
				{
					num += GetInputList(i).Count;
				}
				return num;
			}
		}

		public virtual int OutputCount
		{
			get
			{
				NodeMetadata metadata = Metadata;
				if (!metadata.HasDynamicOutputs)
				{
					return metadata.FixedOutputCount;
				}
				int num = metadata.FixedOutputCount;
				for (int i = 0; i < metadata.DynamicOutputCount; i++)
				{
					num += GetOutputList(i).Count;
				}
				return num;
			}
		}

		public virtual int ImpulseCount
		{
			get
			{
				NodeMetadata metadata = Metadata;
				if (!metadata.HasDynamicImpulses)
				{
					return metadata.FixedImpulseCount;
				}
				int num = metadata.FixedImpulseCount;
				for (int i = 0; i < metadata.DynamicImpulses.Count; i++)
				{
					num += GetImpulseList(i).Count;
				}
				return num;
			}
		}

		public virtual int ArgumentCount
		{
			get
			{
				NodeMetadata metadata = Metadata;
				if (!metadata.HasDynamicInputs)
				{
					return metadata.FixedArgumentCount;
				}
				int num = metadata.FixedArgumentCount;
				for (int i = 0; i < metadata.DynamicInputCount; i++)
				{
					if (!metadata.DynamicInputs[i].IsConditional)
					{
						num += GetInputList(i).Count;
					}
				}
				return num;
			}
		}

		public virtual int OperationCount => FixedOperationCount;

		public virtual int FixedInputCount => Metadata.FixedInputCount;

		public virtual int FixedOutputCount => Metadata.FixedOutputCount;

		public virtual int FixedImpulseCount => Metadata.FixedImpulseCount;

		public virtual int FixedOperationCount => Metadata.FixedOperationCount;

		public virtual int FixedReferenceCount => Metadata.FixedReferenceCount;

		public virtual int FixedGlobalRefCount => Metadata.FixedGlobalRefCount;

		public virtual int DynamicInputCount => Metadata.DynamicInputCount;

		public virtual int DynamicOutputCount => Metadata.DynamicOutputCount;

		public virtual int DynamicImpulseCount => Metadata.DynamicImpulseCount;

		public virtual int DynamicOperationCount => Metadata.DynamicOperationCount;

		public void Initialize(NodeRuntime runtime, int allocationIndex)
		{
			if (Runtime != null)
			{
				throw new InvalidOperationException("Runtime is already assigned to this node");
			}
			Runtime = runtime;
			AllocationIndex = allocationIndex;
		}

		public void Dispose()
		{
			if (Runtime == null)
			{
				throw new InvalidOperationException("Node hasn't been initialized");
			}
			for (int i = 0; i < FixedGlobalRefCount; i++)
			{
				SetGlobalRefBinding(i, null);
			}
			Runtime = null;
		}

		public virtual IInputList GetDynamicInputList(ref int index, out InputListMetadata listMetadata)
		{
			index -= FixedInputCount;
			if (index < 0)
			{
				throw new ArgumentOutOfRangeException("index");
			}
			NodeMetadata metadata = Metadata;
			for (int i = 0; i < metadata.DynamicInputCount; i++)
			{
				IInputList inputList = GetInputList(i);
				if (index < inputList.Count)
				{
					listMetadata = metadata.DynamicInputs[i];
					return inputList;
				}
				index -= inputList.Count;
			}
			throw new ArgumentOutOfRangeException("index");
		}

		public virtual IOutputList GetDynamicOutputList(ref int index, out OutputListMetadata listMetadata)
		{
			index -= FixedOutputCount;
			if (index < 0)
			{
				throw new ArgumentOutOfRangeException("index");
			}
			NodeMetadata metadata = Metadata;
			for (int i = 0; i < metadata.DynamicOutputCount; i++)
			{
				IOutputList outputList = GetOutputList(i);
				if (index < outputList.Count)
				{
					listMetadata = metadata.DynamicOutputs[i];
					return outputList;
				}
				index -= outputList.Count;
			}
			throw new ArgumentOutOfRangeException("index", $"Could not find output. FixedOutputCount: {FixedOutputCount}. DynamicOutputCount: {DynamicOutputCount}. Node: {this}");
		}

		public virtual IOutput GetInputSource(int index)
		{
			if (index < 0)
			{
				throw new ArgumentOutOfRangeException("index");
			}
			NodeMetadata metadata = Metadata;
			if (index < FixedInputCount)
			{
				FieldInfo field = metadata.FixedInputs[index].Field;
				object value = field.GetValue(this);
				return field.FieldType.GetProperty("Source").GetValue(value) as IOutput;
			}
			InputListMetadata listMetadata;
			return GetDynamicInputList(ref index, out listMetadata).GetInputSource(index);
		}

		public virtual void SetInputSource(ElementRef input, IOutput source)
		{
			if (input.IsDynamic)
			{
				IInputList inputList = GetInputList(input.listIndex);
				if (input.AddToList)
				{
					inputList.AddInput(source);
				}
				else
				{
					inputList.SetInputSource(input.index, source);
				}
			}
			else
			{
				SetInputSource(input.index, source);
			}
		}

		public virtual void SetInputSource(int index, IOutput source)
		{
			if (index < 0)
			{
				throw new ArgumentOutOfRangeException("index");
			}
			NodeMetadata metadata = Metadata;
			if (index < FixedInputCount)
			{
				FieldInfo field = metadata.FixedInputs[index].Field;
				object value = field.GetValue(this);
				field.FieldType.GetProperty("Source").SetValue(value, source);
				field.SetValue(this, value);
			}
			else
			{
				GetDynamicInputList(ref index, out var _).SetInputSource(index, source);
			}
		}

		public virtual IInputList GetInputList(int index)
		{
			return Metadata.DynamicInputs[index].Field.GetValue(this) as IInputList;
		}

		public virtual string GetInputListName(int index)
		{
			return Metadata.DynamicInputs[index].Name;
		}

		public virtual Type GetInputListTypeConstraint(int index)
		{
			return Metadata.DynamicInputs[index].TypeConstraint;
		}

		public virtual IOutput GetOutput(int index)
		{
			NodeMetadata metadata = Metadata;
			if (index < metadata.FixedOutputCount)
			{
				return metadata.FixedOutputs[index].Field.GetValue(this) as IOutput;
			}
			OutputListMetadata listMetadata;
			return GetDynamicOutputList(ref index, out listMetadata).GetOutput(index);
		}

		public virtual string GetOutputName(int index)
		{
			NodeMetadata metadata = Metadata;
			if (index < metadata.FixedOutputCount)
			{
				return metadata.FixedOutputs[index].Name;
			}
			GetDynamicOutputList(ref index, out var listMetadata);
			return listMetadata.Name + $".[{index}]";
		}

		public virtual IOutputList GetOutputList(int index)
		{
			return Metadata.DynamicOutputs[index].Field.GetValue(this) as IOutputList;
		}

		public virtual string GetOutputListName(int index)
		{
			return Metadata.DynamicOutputs[index].Name;
		}

		public virtual string GetInputName(int index)
		{
			NodeMetadata metadata = Metadata;
			if (index < metadata.FixedInputCount)
			{
				return metadata.FixedInputs[index].Name;
			}
			GetDynamicInputList(ref index, out var listMetadata);
			return listMetadata.Name + $".[{index}]";
		}

		public virtual Type GetInputType(int index)
		{
			NodeMetadata metadata = Metadata;
			if (index < metadata.FixedInputCount)
			{
				return metadata.FixedInputs[index].InputType;
			}
			InputListMetadata listMetadata;
			return GetDynamicInputList(ref index, out listMetadata).GetInputType(index);
		}

		public virtual DataClass GetInputTypeClass(int index)
		{
			NodeMetadata metadata = Metadata;
			if (index < metadata.FixedInputCount)
			{
				return metadata.FixedInputs[index].DataClass;
			}
			InputListMetadata listMetadata;
			return GetDynamicInputList(ref index, out listMetadata).GetDataClass(index);
		}

		public virtual object GetInputDefaultValue(int index)
		{
			NodeMetadata metadata = Metadata;
			if (index < metadata.FixedInputCount)
			{
				return metadata.FixedInputs[index].DefaultValue;
			}
			InputListMetadata listMetadata;
			IInputList dynamicInputList = GetDynamicInputList(ref index, out listMetadata);
			return listMetadata.DefaultValue ?? dynamicInputList.GetDefaultValue(index);
		}

		public virtual bool IsInputConditional(int index)
		{
			NodeMetadata metadata = Metadata;
			if (index < metadata.FixedInputCount)
			{
				return metadata.FixedInputs[index].IsConditional;
			}
			GetDynamicInputList(ref index, out var listMetadata);
			return listMetadata.IsConditional;
		}

		public virtual bool IsInputListeningToChanges(int index)
		{
			NodeMetadata metadata = Metadata;
			if (index < metadata.FixedInputCount)
			{
				return IsListening(metadata.FixedInputs[index]);
			}
			GetDynamicInputList(ref index, out var listMetadata);
			return IsListening(listMetadata);
			bool IsListening(InputMetadataBase m)
			{
				if (m.IsListeningToChangesEval != null)
				{
					return (bool)m.IsListeningToChangesEval.GetValue(this);
				}
				return m.IsListeningToChanges.Value;
			}
		}

		public virtual CrossRuntimeInputAttribute GetInputCrossRuntime(int index)
		{
			NodeMetadata metadata = Metadata;
			if (index < metadata.FixedInputCount)
			{
				return metadata.FixedInputs[index].CrossRuntime;
			}
			GetDynamicInputList(ref index, out var listMetadata);
			return listMetadata.CrossRuntime;
		}

		public virtual Type GetOutputType(int index)
		{
			NodeMetadata metadata = Metadata;
			if (index < metadata.FixedOutputCount)
			{
				return metadata.FixedOutputs[index].OutputType;
			}
			OutputListMetadata listMetadata;
			return GetDynamicOutputList(ref index, out listMetadata).GetOutputType(index);
		}

		public virtual DataClass GetOutputTypeClass(int index)
		{
			NodeMetadata metadata = Metadata;
			if (index < metadata.FixedOutputCount)
			{
				return metadata.FixedOutputs[index].DataClass;
			}
			OutputListMetadata listMetadata;
			return GetDynamicOutputList(ref index, out listMetadata).GetOutputClass(index);
		}

		public virtual bool IsOutputImplicit(int index)
		{
			NodeMetadata metadata = Metadata;
			if (index < metadata.FixedOutputCount)
			{
				return metadata.FixedOutputs[index].IsImplicit;
			}
			return false;
		}

		public virtual OutputChangeSource GetOutputChangeType(int index)
		{
			NodeMetadata metadata = Metadata;
			if (index < metadata.FixedOutputCount)
			{
				return GetChangeType(metadata.FixedOutputs[index]);
			}
			GetDynamicOutputList(ref index, out var listMetadata);
			return GetChangeType(listMetadata);
			OutputChangeSource GetChangeType(OutputMetadataBase m)
			{
				if (m.ChangeTypeEval != null)
				{
					return (OutputChangeSource)m.ChangeTypeEval.GetValue(this);
				}
				return m.ChangeType;
			}
		}

		public virtual IOperation GetImpulseTarget(int index)
		{
			NodeMetadata metadata = Metadata;
			if (index < metadata.FixedImpulseCount)
			{
				FieldInfo field = metadata.FixedImpulses[index].Field;
				object value = field.GetValue(this);
				return field.FieldType.GetProperty("Target").GetValue(value) as IOperation;
			}
			ImpulseListMetadata listMetadata;
			return GetDynamicImpulseList(ref index, out listMetadata).GetImpulse(index).Target;
		}

		public virtual string GetImpulseName(int index)
		{
			NodeMetadata metadata = Metadata;
			if (index < metadata.FixedImpulseCount)
			{
				return metadata.FixedImpulses[index].Name;
			}
			GetDynamicImpulseList(ref index, out var listMetadata);
			return listMetadata.Name + $".[{index}]";
		}

		public virtual void SetImpulseTarget(int index, IOperation target)
		{
			NodeMetadata metadata = Metadata;
			if (index < metadata.FixedImpulseCount)
			{
				FieldInfo field = metadata.FixedImpulses[index].Field;
				object value = field.GetValue(this);
				field.FieldType.GetProperty("Target").SetValue(value, target);
				field.SetValue(this, value);
			}
			else
			{
				GetDynamicImpulseList(ref index, out var _).SetImpulseTarget(index, target);
			}
		}

		public virtual IImpulseList GetImpulseList(int index)
		{
			return Metadata.DynamicImpulses[index].Field.GetValue(this) as IImpulseList;
		}

		public virtual IImpulseList GetDynamicImpulseList(ref int index, out ImpulseListMetadata listMetadata)
		{
			index -= FixedImpulseCount;
			if (index < 0)
			{
				throw new ArgumentOutOfRangeException("index");
			}
			NodeMetadata metadata = Metadata;
			for (int i = 0; i < metadata.DynamicImpulseCount; i++)
			{
				IImpulseList impulseList = GetImpulseList(i);
				if (index < impulseList.Count)
				{
					listMetadata = metadata.DynamicImpulses[i];
					return impulseList;
				}
				index -= impulseList.Count;
			}
			throw new ArgumentOutOfRangeException("index");
		}

		public virtual string GetImpulseListName(int index)
		{
			return Metadata.DynamicImpulses[index].Name;
		}

		public virtual IOperation GetOperation(int index)
		{
			FieldInfo field = Metadata.FixedOperations[index].Field;
			if (field == null)
			{
				return this as IOperation;
			}
			return field.GetValue(this) as IOperation;
		}

		public virtual string GetOperationName(int index)
		{
			NodeMetadata metadata = Metadata;
			if (index < metadata.FixedOperationCount)
			{
				return metadata.FixedOperations[index].Name;
			}
			GetDynamicOperationList(ref index, out var listMetadata);
			return listMetadata.Name + $".[{index}]";
		}

		public virtual IOperationList GetDynamicOperationList(ref int index, out OperationListMetadata listMetadata)
		{
			index -= FixedOperationCount;
			if (index < 0)
			{
				throw new ArgumentOutOfRangeException("index");
			}
			NodeMetadata metadata = Metadata;
			for (int i = 0; i < metadata.DynamicOperationCount; i++)
			{
				IOperationList operationList = GetOperationList(i);
				if (index < operationList.Count)
				{
					listMetadata = metadata.DynamicOperations[i];
					return operationList;
				}
				index -= operationList.Count;
			}
			throw new ArgumentOutOfRangeException("index");
		}

		public virtual bool IsOperationAsync(int index)
		{
			NodeMetadata metadata = Metadata;
			if (index < metadata.FixedOperationCount)
			{
				return metadata.FixedOperations[index].IsAsync;
			}
			GetDynamicOperationList(ref index, out var listMetadata);
			return GetOperationList(listMetadata.Index).IsOperationAsync(index);
		}

		public virtual bool CanOperationContinueTo(int index, string impulseName)
		{
			NodeMetadata metadata = Metadata;
			if (index < metadata.FixedOperationCount)
			{
				return metadata.FixedOperations[index]?.PossibleContinuations?.CanContinueTo(impulseName) ?? true;
			}
			GetDynamicOperationList(ref index, out var listMetadata);
			return listMetadata.PossibleContinuations?.CanContinueTo(impulseName) ?? true;
		}

		public virtual bool CanOperationListContinueTo(int index, string impulseName)
		{
			return Metadata.DynamicOperations[index]?.PossibleContinuations?.CanContinueTo(impulseName) ?? true;
		}

		public virtual IOperationList GetOperationList(int index)
		{
			return Metadata.DynamicOperations[index].Field.GetValue(this) as IOperationList;
		}

		public virtual string GetOperationListName(int index)
		{
			return Metadata.DynamicOperations[index].Name;
		}

		public virtual ImpulseType GetImpulseType(int index)
		{
			NodeMetadata metadata = Metadata;
			if (index < metadata.FixedImpulseCount)
			{
				return metadata.FixedImpulses[index].Type;
			}
			GetDynamicImpulseList(ref index, out var listMetadata);
			return GetImpulseList(listMetadata.Index).GetImpulseType(index);
		}

		public virtual bool OperationHasSingleContinuation(int index)
		{
			NodeMetadata metadata = Metadata;
			if (metadata.FixedContinuationCount == 1 && metadata.DynamicImpulseCount == 0)
			{
				for (int i = 0; i < metadata.FixedImpulseCount; i++)
				{
					if (metadata.FixedImpulses[i].Type == ImpulseType.Continuation)
					{
						if (GetImpulseTarget(i) == null)
						{
							return false;
						}
						return CanOperationContinueTo(index, metadata.FixedImpulses[i].Name);
					}
				}
				throw new Exception("This code should not be reachable, since the node indicated there's one fixed continuation");
			}
			bool flag = false;
			for (int j = 0; j < ImpulseCount; j++)
			{
				if (GetImpulseTarget(j) != null && GetImpulseType(j) == ImpulseType.Continuation && CanOperationContinueTo(index, GetImpulseName(j)))
				{
					if (flag)
					{
						return false;
					}
					flag = true;
				}
			}
			return flag;
		}

		public virtual bool OperationHasSyncAsyncTransition(int index)
		{
			_ = Metadata;
			bool flag = IsOperationAsync(index);
			for (int i = 0; i < ImpulseCount; i++)
			{
				if (GetImpulseType(i) != ImpulseType.Continuation)
				{
					continue;
				}
				IOperation impulseTarget = GetImpulseTarget(i);
				if (impulseTarget != null && CanOperationContinueTo(index, GetImpulseName(i)))
				{
					if (impulseTarget is IAsyncOperation && !flag)
					{
						return true;
					}
					if (impulseTarget is ISyncOperation && flag)
					{
						return true;
					}
				}
			}
			return false;
		}

		public virtual string GetReferenceName(int index)
		{
			return Metadata.FixedReferences[index].Name;
		}

		public virtual Type GetReferenceType(int index)
		{
			return Metadata.FixedReferences[index].ReferenceType;
		}

		public virtual INode GetReferenceTarget(int index)
		{
			ReferenceMetadata referenceMetadata = Metadata.FixedReferences[index];
			object value = referenceMetadata.Field.GetValue(this);
			return (INode)referenceMetadata.Field.FieldType.GetField("Target").GetValue(value);
		}

		public virtual void SetReferenceTarget(int index, INode node)
		{
			if (!TrySetReferenceTarget(index, node))
			{
				throw new ArgumentException($"Target is not compatible with the reference type. Node Type: {node.GetType()}. ReferenceType: {Metadata.FixedReferences[index].ReferenceType}");
			}
		}

		public virtual bool TrySetReferenceTarget(int index, INode node)
		{
			ReferenceMetadata referenceMetadata = Metadata.FixedReferences[index];
			if (node != null && !referenceMetadata.ReferenceType.IsAssignableFrom(node.GetType()))
			{
				return false;
			}
			object value = referenceMetadata.Field.GetValue(this);
			referenceMetadata.Field.FieldType.GetField("Target").SetValue(value, node);
			referenceMetadata.Field.SetValue(this, value);
			return true;
		}

		public virtual string GetGlobalRefName(int index)
		{
			return Metadata.FixedGlobalRefs[index].Name;
		}

		public virtual Type GetGlobalRefValueType(int index)
		{
			return Metadata.FixedGlobalRefs[index].ValueType;
		}

		public virtual Global GetGlobalRefBinding(int index)
		{
			GlobalRefMetadata globalRefMetadata = Metadata.FixedGlobalRefs[index];
			object value = globalRefMetadata.Field.GetValue(this);
			return (Global)globalRefMetadata.Field.FieldType.GetProperty("Global").GetValue(value);
		}

		public virtual void SetGlobalRefBinding(int index, Global binding)
		{
			if (!TrySetGlobalRefBinding(index, binding))
			{
				throw new ArgumentException("Target is not compatible with the binding value type");
			}
		}

		public virtual bool TrySetGlobalRefBinding(int index, Global binding)
		{
			GlobalRefMetadata globalRefMetadata = Metadata.FixedGlobalRefs[index];
			if (binding != null && binding.ValueType != globalRefMetadata.ValueType)
			{
				return false;
			}
			object value = globalRefMetadata.Field.GetValue(this);
			globalRefMetadata.Field.FieldType.GetProperty("Global").SetValue(value, binding);
			return true;
		}

		public void CopyDynamicOutputLayout(INode node)
		{
			NodeMetadata metadata = node.Metadata;
			for (int i = 0; i < DynamicOutputCount; i++)
			{
				IOutputList outputList = GetOutputList(i);
				string outputListName = GetOutputListName(i);
				OutputListMetadata outputListByName = metadata.GetOutputListByName(outputListName);
				if (outputListByName == null)
				{
					continue;
				}
				IOutputList outputList2 = node.GetOutputList(outputListByName.Index);
				if (outputList is OutputList outputList3)
				{
					for (int j = 0; j < outputList2.Count; j++)
					{
						outputList3.AddOutputAuto(outputList2.GetOutputType(j));
					}
					continue;
				}
				if (outputList.Count > outputList2.Count)
				{
					outputList.Clear();
				}
				while (outputList.Count < outputList2.Count)
				{
					outputList.AddOutput();
				}
			}
		}

		public void CopyDynamicOperationLayout(INode node)
		{
			NodeMetadata metadata = node.Metadata;
			for (int i = 0; i < DynamicOperationCount; i++)
			{
				IOperationList operationList = GetOperationList(i);
				string operationListName = GetOperationListName(i);
				OperationListMetadata operationListByName = metadata.GetOperationListByName(operationListName);
				if (operationListByName != null)
				{
					IOperationList operationList2 = node.GetOperationList(operationListByName.Index);
					while (operationList.Count < operationList2.Count)
					{
						operationList.AddOperation();
					}
					while (operationList.Count > operationList2.Count)
					{
						operationList.RemoveOperation();
					}
				}
			}
		}

		public override string ToString()
		{
			return $"Node {GetType().Name} ({IndexInGroup}; {AllocationIndex}) (Instance: {GetHashCode()}) on Group {Runtime?.Group?.Name}";
		}
	}
	public abstract class NodeRuntime
	{
		private DataImportNode importNode;

		private ImpulseExportNode exportNode;

		private List<IOutput> dataExports = new List<IOutput>();

		private OutputList dataImports;

		private List<ImpulseImport> impulseImports = new List<ImpulseImport>();

		private List<ImpulseExport> impulseExports = new List<ImpulseExport>();

		private List<Global> globals = new List<Global>();

		public NodeGroup Group { get; private set; }

		public abstract int NodeCount { get; }

		public int DataImportsCount => dataImports.Count;

		public int DataExportsCount => dataExports.Count;

		public IEnumerable<IOutput> DataExports => dataExports;

		public int ImpulseImportsCount => impulseImports.Count;

		public int ImpulseExportCount => impulseExports.Count;

		public int GlobalsCount => globals.Count;

		public abstract INode AddNode(Type type);

		public abstract bool RemoveNode(INode node);

		public abstract int RemoveNodes(Predicate<INode> predicate);

		public abstract INode GetNodeGeneric(int index);

		public abstract void TranslateInputs(INode target, INode source, Dictionary<INode, INode> nodeReplacements = null, List<InsertedConversion> insertedConversions = null);

		public abstract void TranslateImpulses(INode target, INode source, Dictionary<INode, INode> nodeReplcements = null);

		public abstract void TranslateReferences(INode target, INode source, Dictionary<INode, INode> nodeReplacements = null);

		public abstract void SortNodesByIndex();

		internal abstract int ForeachNode<T>(NodeEnumerationAction<T> action, NodeEnumerationContext context, bool cache) where T : INode;

		internal void Init(NodeGroup group)
		{
			Group = group;
			importNode = new DataImportNode();
			importNode.Initialize(this, group.GetNodeAllocationIndex());
			exportNode = new ImpulseExportNode();
			exportNode.Initialize(this, group.GetNodeAllocationIndex());
			dataImports = new OutputList(importNode);
		}

		public abstract Type GetCompatibleNodeType(Type type);

		public ValueOutput<T> AddValueImport<T>() where T : unmanaged
		{
			return dataImports.AddValueOutput<T>();
		}

		public ObjectOutput<T> AddObjectImport<T>()
		{
			return dataImports.AddObjectOutput<T>();
		}

		public ValueOutput<T> GetValueImport<T>(int index) where T : unmanaged
		{
			return dataImports.GetValueOutput<T>(index);
		}

		public ObjectOutput<T> GetObjectImport<T>(int index)
		{
			return dataImports.GetObjectOutput<T>(index);
		}

		public IOutput GetImport(int index)
		{
			return dataImports.GetOutput(index);
		}

		public Type GetImportType(int index)
		{
			return dataImports.GetOutputType(index);
		}

		public DataClass GetImportDataClass(int index)
		{
			return dataImports.GetOutputClass(index);
		}

		public IOutput GetValueExport(int index)
		{
			return dataExports[index];
		}

		public void SetValueExport(int index, IOutput source)
		{
			dataExports[index] = source;
		}

		public void AddExport(IOutput output)
		{
			dataExports.Add(output);
		}

		public void RemoveExport(IOutput output)
		{
			dataExports.Remove(output);
		}

		public void ClearValueExports()
		{
			dataExports.Clear();
		}

		public void AddImpulseImport(IOperation operation, bool isAsync = false)
		{
			if (!isAsync && operation is IAsyncOperation)
			{
				throw new ArgumentException("Cannot add async operation as sync impulse import");
			}
			impulseImports.Add(new ImpulseImport(operation, isAsync));
		}

		public void AddSyncImpulseImport(ISyncOperation operation)
		{
			AddImpulseImport(operation);
		}

		public void AddAsyncImpulseImport(IOperation operation)
		{
			AddImpulseImport(operation, isAsync: true);
		}

		public bool IsImpulseImportAsync(int index)
		{
			return impulseImports[index].isAsync;
		}

		public void RemoveImpulseImport(int index)
		{
			impulseImports.RemoveAt(index);
		}

		public void SetImpulseImport(int index, IOperation operation, bool isAsync)
		{
			impulseImports[index] = new ImpulseImport(operation, isAsync);
		}

		public void ClearImpulseImports()
		{
			impulseImports.Clear();
		}

		public IOperation GetImpulseImport(int index)
		{
			return impulseImports[index].target;
		}

		public ContinuationExport AddContinuationExport()
		{
			ContinuationExport continuationExport = new ContinuationExport(exportNode, impulseExports.Count);
			impulseExports.Add(continuationExport);
			return continuationExport;
		}

		public CallExport AddCallExport()
		{
			CallExport callExport = new CallExport(exportNode, impulseExports.Count);
			impulseExports.Add(callExport);
			return callExport;
		}

		public AsyncCallExport AddAsyncCallExport()
		{
			AsyncCallExport asyncCallExport = new AsyncCallExport(exportNode, impulseExports.Count);
			impulseExports.Add(asyncCallExport);
			return asyncCallExport;
		}

		public void ClearImpulseExports()
		{
			impulseExports.Clear();
		}

		public Global<T> AddGlobal<T>(string name)
		{
			Global<T> global = new Global<T>(this, globals.Count, name);
			globals.Add(global);
			return global;
		}

		public Global GetGlobal(int index)
		{
			return globals[index];
		}

		public Global<T> GetGlobal<T>(int index)
		{
			return (Global<T>)globals[index];
		}

		public void ClearGlobals()
		{
			globals.Clear();
		}

		public void RemapImportsAndExports(Dictionary<INode, INode> remappedNodes)
		{
			for (int i = 0; i < DataExportsCount; i++)
			{
				IOutput valueExport = GetValueExport(i);
				IOutput output = valueExport.RemapOutput(remappedNodes);
				if (output != valueExport)
				{
					SetValueExport(i, output);
				}
			}
			for (int j = 0; j < ImpulseImportsCount; j++)
			{
				IOperation impulseImport = GetImpulseImport(j);
				IOperation operation = impulseImport.RemapTarget(remappedNodes);
				if (operation != impulseImport)
				{
					SetImpulseImport(j, operation, IsImpulseImportAsync(j));
				}
			}
		}

		public static bool CanTargetRuntime(NodeRuntime ownerRuntime, CrossRuntimeInputAttribute inputCrossRuntime, IOutput source)
		{
			return CanTargetRuntime(ownerRuntime, inputCrossRuntime, source.OwnerNode.Runtime);
		}

		public static bool CanTargetRuntime(NodeRuntime ownerRuntime, CrossRuntimeInputAttribute inputCrossRuntime, NodeRuntime targetRuntime)
		{
			return inputCrossRuntime?.IsValidTargetRuntime(targetRuntime) ?? (targetRuntime == ownerRuntime);
		}

		public static bool AreSameRuntimeTypes(NodeRuntime ownerRuntime, IOutput source)
		{
			return AreSameRuntimeTypes(ownerRuntime, source?.OwnerNode?.Runtime);
		}

		public static bool AreSameRuntimeTypes(NodeRuntime ownerRuntime, NodeRuntime targetRuntime)
		{
			return ownerRuntime.GetType() == targetRuntime?.GetType();
		}

		public override string ToString()
		{
			return $"{GetType()} on {Group}";
		}
	}
	public abstract class NodeRuntime<N> : NodeRuntime where N : class, INode
	{
		private List<N> nodes = new List<N>();

		private ConcurrentDictionary<Type, IList> cachedNodeTypes = new ConcurrentDictionary<Type, IList>();

		private List<IExecutionNestedNode> cachedNestedNodes;

		public override int NodeCount => nodes.Count;

		public IEnumerable<N> Nodes => nodes;

		public static bool IsCompatibleNodeType(Type type)
		{
			return typeof(N).IsAssignableFrom(type);
		}

		public N GetNode(int index)
		{
			return nodes[index];
		}

		public override INode GetNodeGeneric(int index)
		{
			return nodes[index];
		}

		public void AddNode(N node)
		{
			if (node.Runtime != null)
			{
				throw new InvalidOperationException("Node already belongs to a runtime and cannot be added");
			}
			node.Initialize(this, base.Group.GetNodeAllocationIndex());
			nodes.Add(node);
		}

		public T AddNode<T>() where T : N, new()
		{
			T val = new T();
			AddNode((N)(object)val);
			return val;
		}

		public override INode AddNode(Type type)
		{
			type = GetCompatibleNodeType(type);
			if (type == null)
			{
				throw new ArgumentException($"Node of type {type} is not compatible with runtime {this}");
			}
			N val = (N)Activator.CreateInstance(type);
			val.Initialize(this, base.Group.GetNodeAllocationIndex());
			nodes.Add(val);
			return val;
		}

		public override bool RemoveNode(INode node)
		{
			if (nodes.Remove((N)node))
			{
				node.Dispose();
				return true;
			}
			return false;
		}

		public override int RemoveNodes(Predicate<INode> predicate)
		{
			foreach (N node in nodes)
			{
				if (predicate(node))
				{
					node.Dispose();
				}
			}
			return nodes.RemoveAll(predicate);
		}

		public override void SortNodesByIndex()
		{
			nodes.Sort((N a, N b) => a.IndexInGroup.CompareTo(b.IndexInGroup));
		}

		public override Type GetCompatibleNodeType(Type type)
		{
			if (IsCompatibleNodeType(type))
			{
				return type;
			}
			return null;
		}

		public ConnectionResult ConnectInput(INode node, string inputName, IOutput source, bool overload = true, bool explicitCast = false, bool allowMergingGroups = false)
		{
			InputMetadata inputByName = node.Metadata.GetInputByName(inputName);
			return ConnectInput(node, new ElementRef(inputByName.Index), source, overload, explicitCast, allowMergingGroups);
		}

		public ConnectionResult ConnectListInput(INode node, string listName, int inputIndex, IOutput source, bool overload = true, bool explicitCast = false, bool allowMergingGroups = false)
		{
			InputListMetadata inputListByName = node.Metadata.GetInputListByName(listName);
			return ConnectInput(node, new ElementRef(inputListByName.Index, inputIndex), source, overload, explicitCast, allowMergingGroups);
		}

		public ConnectionResult ConnectListInput(INode node, string listName, IOutput source, bool overload = true, bool explicitCast = false, bool allowMergingGroups = false)
		{
			InputListMetadata inputListByName = node.Metadata.GetInputListByName(listName);
			return ConnectInput(node, new ElementRef(inputListByName.Index, -1), source, overload, explicitCast, allowMergingGroups);
		}

		public ConnectionResult ConnectInput(INode node, ElementRef input, IOutput source, bool overload = true, bool explicitCast = false, bool allowMergingGroups = false)
		{
			if (source == null)
			{
				throw new ArgumentNullException("source");
			}
			if (node.Runtime != this)
			{
				throw new InvalidOperationException($"Node {node} does not belong to runtime: {this}");
			}
			NodeMetadata metadata = node.Metadata;
			Type inputType;
			string name;
			CrossRuntimeInputAttribute crossRuntime;
			if (input.IsDynamic)
			{
				InputListMetadata inputListMetadata = metadata.DynamicInputs[input.listIndex];
				inputType = inputListMetadata.TypeConstraint;
				name = inputListMetadata.Name;
				crossRuntime = inputListMetadata.CrossRuntime;
			}
			else
			{
				InputMetadata inputMetadata = metadata.FixedInputs[input.index];
				inputType = inputMetadata.InputType;
				name = inputMetadata.Name;
				crossRuntime = inputMetadata.CrossRuntime;
			}
			if (inputType.IsInterface && inputType.GetInterface(typeof(INode).Name) != null && !typeof(INode).IsAssignableFrom(source.OutputType))
			{
				Node ownerNode = source.OwnerNode;
				Type type = ownerNode.GetType().GetInterfaces().FirstOrDefault(delegate(Type t)
				{
					if (t == inputType)
					{
						return true;
					}
					return inputType.IsGenericType && t.IsGenericType && t.GetGenericTypeDefinition() == inputType.GetGenericTypeDefinition();
				});
				if (type != null)
				{
					INode node2 = AddNode(typeof(ReferenceToOutput<>).MakeGenericType(type));
					ConnectionResult result = ConnectInput(node, input, (IOutput)node2, overload, explicitCast, allowMergingGroups);
					if (result.connected)
					{
						node2.SetReferenceTarget(0, ownerNode);
						return result.AddInsertedConversion(new InsertedConversion(node2, node, input));
					}
					RemoveNode(node2);
					return result;
				}
			}
			if (source.OutputType == inputType)
			{
				if (NodeRuntime.CanTargetRuntime(this, crossRuntime, source))
				{
					node.SetInputSource(input, source);
					return ConnectionResult.Success;
				}
				if (allowMergingGroups && NodeRuntime.AreSameRuntimeTypes(this, source))
				{
					node.SetInputSource(input, source);
					return ConnectionResult.Success;
				}
				return ConnectionResult.Failed("Target is in a different runtime that cannot be cross targetted.");
			}
			if (overload)
			{
				NodeConnections fromNode = NodeConnections.GetFromNode(node);
				fromNode.SetInput(input, name, source.OutputType, source.OwnerNode.Runtime);
				NodeOverloadContext nodeOverloadContext = new NodeOverloadContext(base.Group, this);
				OverloadResult overloadResult = nodeOverloadContext.TryOverload(node, fromNode, allowMergingGroups);
				if (overloadResult.IsSuccess)
				{
					if (nodeOverloadContext.OverloadedAnyNodes)
					{
						nodeOverloadContext.SwapNodes();
						node = nodeOverloadContext.GetSwappedNode(node);
					}
					ConnectionResult result2 = ((!input.IsDynamic) ? ConnectInput(node, name, source, overload: false, explicitCast, allowMergingGroups) : ConnectListInput(node, name, input.index, source, overload: false, explicitCast, allowMergingGroups));
					if (!result2.connected)
					{
						return result2;
					}
					if (nodeOverloadContext.OverloadedAnyNodes)
					{
						result2 = result2.Combine(ConnectionResult.Overload(nodeOverloadContext));
					}
					return result2;
				}
				ConnectionResult result3 = ConnectInput(node, input, source, overload: false, explicitCast, allowMergingGroups);
				if (result3.connected)
				{
					return result3;
				}
				return ConnectionResult.Failed($"Failed to overload or connect directly.\nOverload failure reason: {overloadResult.failReason}\nDirect connection failure reason: {result3.failReason}");
			}
			if (TypeHelper.CanImplicitlyConvertTo(source.OutputType, inputType) || (explicitCast && TypeHelper.CanExplicitlyConvertTo(source.OutputType, inputType)))
			{
				Type castNode = CastHelper.GetCastNode(source.OutputType, inputType, this);
				if (castNode != null)
				{
					INode node3 = AddNode(castNode);
					node.SetInputSource(input, (IOutput)node3);
					node3.SetInputSource(0, source);
					return ConnectionResult.Conversion(new InsertedConversion((ICast)node3, node, input));
				}
			}
			return ConnectionResult.Failed($"Failed to find overload. Overload search enabled: {overload}");
		}

		public ConnectionResult SetReference(INode node, string referenceName, INode target, bool overload = true)
		{
			ReferenceMetadata referenceByName = node.Metadata.GetReferenceByName(referenceName);
			return SetReference(node, referenceByName.Index, target, overload);
		}

		public ConnectionResult SetReference(INode node, int referenceIndex, INode target, bool overload = true, bool allowMergingGroups = false)
		{
			ReferenceMetadata referenceMetadata = node.Metadata.FixedReferences[referenceIndex];
			if (node.TrySetReferenceTarget(referenceIndex, target))
			{
				return ConnectionResult.Success;
			}
			if (overload)
			{
				NodeConnections fromNode = NodeConnections.GetFromNode(node);
				fromNode.SetReference(referenceMetadata.Name, target.GetType(), target.Runtime);
				NodeOverloadContext nodeOverloadContext = new NodeOverloadContext(base.Group, this);
				OverloadResult overloadResult = nodeOverloadContext.TryOverload(node, fromNode, allowMergingGroups);
				if (overloadResult.IsSuccess)
				{
					nodeOverloadContext.SwapNodes();
					node = nodeOverloadContext.GetSwappedNode(node);
					ConnectionResult result = SetReference(node, referenceMetadata.Name, target, overload: false);
					if (nodeOverloadContext.OverloadedAnyNodes)
					{
						result = result.Combine(ConnectionResult.Overload(nodeOverloadContext));
					}
					return result;
				}
				return ConnectionResult.Failed("Failed to overload: " + overloadResult.failReason);
			}
			return ConnectionResult.Failed($"Failed overload. Overload enabled: {overload}");
		}

		public override void TranslateInputs(INode target, INode source, Dictionary<INode, INode> remappedNodes = null, List<InsertedConversion> insertedCasts = null)
		{
			NodeMetadata metadata = source.Metadata;
			NodeMetadata metadata2 = target.Metadata;
			for (int i = 0; i < metadata.FixedInputCount; i++)
			{
				IOutput inputSource = source.GetInputSource(i);
				if (inputSource == null)
				{
					continue;
				}
				string name = metadata.FixedInputs[i].Name;
				InputMetadata inputByName = metadata2.GetInputByName(name);
				if (inputByName != null)
				{
					inputSource = inputSource.RemapOutput(remappedNodes).SkipImplicitCasts();
					ConnectionResult connectionResult = ConnectInput(target, new ElementRef(inputByName.Index), inputSource, overload: false);
					if (connectionResult.conversions != null)
					{
						insertedCasts?.AddRange(connectionResult.conversions);
					}
				}
			}
			for (int j = 0; j < metadata.DynamicInputCount; j++)
			{
				IInputList inputList = source.GetInputList(j);
				string inputListName = source.GetInputListName(j);
				InputListMetadata inputListByName = metadata2.GetInputListByName(inputListName);
				if (inputListByName == null)
				{
					continue;
				}
				target.GetInputList(inputListByName.Index).Clear();
				for (int k = 0; k < inputList.Count; k++)
				{
					IOutput inputSource2 = inputList.GetInputSource(k);
					inputSource2 = inputSource2.RemapOutput(remappedNodes).SkipImplicitCasts();
					if (inputSource2 != null)
					{
						ConnectionResult connectionResult2 = ConnectInput(target, new ElementRef(inputListByName.Index, -1), inputSource2, overload: false);
						if (connectionResult2.conversions != null)
						{
							insertedCasts?.AddRange(connectionResult2.conversions);
						}
					}
					else
					{
						target.GetInputList(inputListByName.Index).AddInput(null);
					}
				}
			}
		}

		public override void TranslateImpulses(INode target, INode source, Dictionary<INode, INode> remappedNodes = null)
		{
			NodeMetadata metadata = source.Metadata;
			NodeMetadata metadata2 = target.Metadata;
			for (int i = 0; i < source.FixedImpulseCount; i++)
			{
				IOperation impulseTarget = source.GetImpulseTarget(i);
				if (impulseTarget != null)
				{
					string name = metadata.FixedImpulses[i].Name;
					ImpulseMetadata impulseByName = metadata2.GetImpulseByName(name);
					if (impulseByName != null)
					{
						impulseTarget = impulseTarget.RemapTarget(remappedNodes);
						target.SetImpulseTarget(impulseByName.Index, impulseTarget);
					}
				}
			}
			for (int j = 0; j < source.DynamicImpulseCount; j++)
			{
				IImpulseList impulseList = source.GetImpulseList(j);
				string impulseListName = source.GetImpulseListName(j);
				ImpulseListMetadata impulseListByName = metadata2.GetImpulseListByName(impulseListName);
				if (impulseListByName != null)
				{
					IImpulseList impulseList2 = target.GetImpulseList(impulseListByName.Index);
					impulseList2.Clear();
					for (int k = 0; k < impulseList.Count; k++)
					{
						IOperation impulseTarget2 = impulseList.GetImpulseTarget(k);
						impulseTarget2 = impulseTarget2.RemapTarget(remappedNodes);
						impulseList2.AddImpulse(impulseTarget2);
					}
				}
			}
		}

		public override void TranslateReferences(INode target, INode source, Dictionary<INode, INode> remappedNodes = null)
		{
			NodeMetadata metadata = target.Metadata;
			for (int i = 0; i < source.FixedReferenceCount; i++)
			{
				INode node = source.GetReferenceTarget(i);
				if (node != null)
				{
					string referenceName = source.GetReferenceName(i);
					ReferenceMetadata referenceByName = metadata.GetReferenceByName(referenceName);
					if (remappedNodes != null && remappedNodes.TryGetValue(node, out var value))
					{
						node = value;
					}
					target.TrySetReferenceTarget(referenceByName.Index, node);
				}
			}
		}

		public IEnumerable<IExecutionNestedNode> GetNestedNodes(bool cache)
		{
			if (cache)
			{
				if (cachedNestedNodes == null)
				{
					cachedNestedNodes = Nodes.OfType<IExecutionNestedNode>().ToList();
				}
				return cachedNestedNodes;
			}
			return Nodes.OfType<IExecutionNestedNode>();
		}

		public IEnumerable<T> GetNodesOfType<T>(bool cache) where T : INode
		{
			if (cache)
			{
				Type typeFromHandle = typeof(T);
				if (cachedNodeTypes.TryGetValue(typeFromHandle, out var value))
				{
					return (IEnumerable<T>)value;
				}
				List<T> list = Nodes.OfType<T>().ToList();
				cachedNodeTypes.TryAdd(typeFromHandle, list);
				return list;
			}
			return Nodes.OfType<T>();
		}

		internal override int ForeachNode<T>(NodeEnumerationAction<T> action, NodeEnumerationContext context, bool cache)
		{
			int num = 0;
			foreach (T item in GetNodesOfType<T>(cache))
			{
				action(item, context);
				num++;
			}
			foreach (IExecutionNestedNode nestedNode in GetNestedNodes(cache))
			{
				if (context.TryEnterNestedNode(nestedNode))
				{
					num += ((NodeRuntime)nestedNode.TargetRuntime).ForeachNode(action, context, cache);
					context.ExitNestedNode(nestedNode);
				}
			}
			return num;
		}

		protected void ClearQueryCaches()
		{
			cachedNodeTypes.Clear();
		}
	}
	public readonly struct ElementPath<E> : IEquatable<ElementPath<E>>, IComparable<ElementPath<E>> where E : class
	{
		public readonly NodeContextPath path;

		public readonly E element;

		public ElementPath(E element)
		{
			this.element = element;
			path = default(NodeContextPath);
		}

		public ElementPath(E element, NodeContextPath path)
		{
			this.path = path;
			this.element = element;
		}

		public ElementPath(E element, params INode[] path)
		{
			this.element = element;
			this.path = new NodeContextPath(path);
		}

		public ElementPath<E> Nest(INode node)
		{
			return new ElementPath<E>(element, path.Nest(node));
		}

		public bool Equals(ElementPath<E> other)
		{
			if (element == null && other.element != null)
			{
				return false;
			}
			if (!element.Equals(other.element))
			{
				return false;
			}
			return path.Equals(other.path);
		}

		public override int GetHashCode()
		{
			HashCode hashCode = default(HashCode);
			hashCode.Add(element);
			hashCode.Add(path.GetHashCode());
			return hashCode.ToHashCode();
		}

		public override string ToString()
		{
			if (path.PathLength == 0)
			{
				return element.ToString();
			}
			return path.ToString() + " -> " + element.ToString();
		}

		public int CompareTo(ElementPath<E> other)
		{
			int num = path.CompareTo(other.path);
			if (num != 0)
			{
				return num;
			}
			if (element is INode node && other.element is INode node2)
			{
				return node.IndexInGroup.CompareTo(node2.IndexInGroup);
			}
			return 0;
		}
	}
	public readonly struct NodeContextPath : IEquatable<NodeContextPath>, IComparable<NodeContextPath>
	{
		private readonly INode[] path;

		public int PathLength
		{
			get
			{
				INode[] array = path;
				if (array == null)
				{
					return 0;
				}
				return array.Length;
			}
		}

		public INode this[int index] => GetNode(index);

		public INode GetNode(int index)
		{
			if (path == null || index < 0 || index >= path.Length)
			{
				throw new ArgumentOutOfRangeException("index");
			}
			return path[index];
		}

		public NodeContextPath(INode[] path)
		{
			if (path != null && path.Length == 0)
			{
				this.path = null;
			}
			else
			{
				this.path = path;
			}
		}

		public NodeContextPath Nest(INode node)
		{
			INode[] array = new INode[PathLength + 1];
			if (PathLength > 0)
			{
				Array.Copy(path, 0, array, 1, PathLength);
			}
			array[0] = node;
			return new NodeContextPath(array);
		}

		public bool Equals(NodeContextPath other)
		{
			if (other.path == null)
			{
				return path == null;
			}
			if (path == other.path)
			{
				return true;
			}
			if (path.Length != other.path.Length)
			{
				return false;
			}
			for (int i = 0; i < path.Length; i++)
			{
				if (!path[i].Equals(other.path[i]))
				{
					return false;
				}
			}
			return true;
		}

		public override int GetHashCode()
		{
			if (path == null)
			{
				return 0;
			}
			HashCode hashCode = default(HashCode);
			INode[] array = path;
			foreach (INode value in array)
			{
				hashCode.Add(value);
			}
			return hashCode.ToHashCode();
		}

		public override string ToString()
		{
			if (path == null)
			{
				return "(Root)";
			}
			return "(Root) -> " + string.Join(" -> ", path.Select((INode n) => (n is INestedNode nestedNode) ? nestedNode.TargetGroup.Name : n.ToString()));
		}

		public int CompareTo(NodeContextPath other)
		{
			int num = PathLength.CompareTo(other.PathLength);
			if (num != 0)
			{
				return num;
			}
			for (int i = 0; i < PathLength; i++)
			{
				INode node = GetNode(i);
				INode node2 = other.GetNode(i);
				int num2 = node.IndexInGroup.CompareTo(node2.IndexInGroup);
				if (num2 != 0)
				{
					return num2;
				}
			}
			return 0;
		}

		public int FindSharedRootLength(NodeContextPath other)
		{
			if (path == other.path)
			{
				return PathLength;
			}
			for (int i = 0; i < PathLength; i++)
			{
				if (i == other.PathLength)
				{
					return i;
				}
				if (this[i] != other[i])
				{
					return i;
				}
			}
			return PathLength;
		}
	}
	public delegate void NodeEnumerationAction<T>(T node, NodeEnumerationContext context) where T : INode;
	public class NodeEnumerationContext
	{
		private HashSet<NodeGroup> enteredGroups = new HashSet<NodeGroup>();

		private List<INestedNode> nodePath = new List<INestedNode>();

		public int CurrentDepth => nodePath.Count;

		public INestedNode GetNodePath(int index)
		{
			return nodePath[index];
		}

		public void Begin(NodeGroup group)
		{
			if (enteredGroups.Count > 0)
			{
				throw new InvalidOperationException("Cannot begin context, already has entered groups");
			}
			enteredGroups.Add(group);
		}

		public void End()
		{
			if (nodePath.Count > 0)
			{
				throw new InvalidOperationException("Cannot end context, node path is not empty");
			}
			enteredGroups.Clear();
		}

		public bool TryEnterNestedNode(INestedNode node)
		{
			if (enteredGroups.Add(node.TargetRuntime.Group))
			{
				nodePath.Add(node);
				return true;
			}
			return false;
		}

		public void ExitNestedNode(INestedNode node)
		{
			if (!enteredGroups.Remove(node.TargetRuntime.Group))
			{
				throw new InvalidOperationException("Currently not in the node");
			}
			nodePath.RemoveAt(nodePath.Count - 1);
		}
	}
	public class NodeQueryAcceleration
	{
		private Dictionary<INode, HashSet<INode>> evaluatingNodes = new Dictionary<INode, HashSet<INode>>();

		private Dictionary<INode, HashSet<INode>> impulsingNodes = new Dictionary<INode, HashSet<INode>>();

		private Dictionary<INode, HashSet<INode>> referencingNodes = new Dictionary<INode, HashSet<INode>>();

		public NodeGroup Group { get; private set; }

		public NodeQueryAcceleration(NodeGroup group)
		{
			Group = group;
			Build();
		}

		private void Build()
		{
			foreach (NodeRuntime runtime in Group.Runtimes)
			{
				for (int i = 0; i < runtime.NodeCount; i++)
				{
					INode nodeGeneric = runtime.GetNodeGeneric(i);
					for (int j = 0; j < nodeGeneric.InputCount; j++)
					{
						IOutput inputSource = nodeGeneric.GetInputSource(j);
						if (inputSource?.OwnerNode != null)
						{
							if (!evaluatingNodes.TryGetValue(inputSource.OwnerNode, out var value))
							{
								value = new HashSet<INode>();
								evaluatingNodes.Add(inputSource.OwnerNode, value);
							}
							value.Add(nodeGeneric);
						}
					}
					for (int k = 0; k < nodeGeneric.ImpulseCount; k++)
					{
						IOperation impulseTarget = nodeGeneric.GetImpulseTarget(k);
						if (impulseTarget?.OwnerNode != null)
						{
							if (!impulsingNodes.TryGetValue(impulseTarget.OwnerNode, out var value2))
							{
								value2 = new HashSet<INode>();
								impulsingNodes.Add(impulseTarget.OwnerNode, value2);
							}
							value2.Add(nodeGeneric);
						}
					}
					for (int l = 0; l < nodeGeneric.FixedReferenceCount; l++)
					{
						INode referenceTarget = nodeGeneric.GetReferenceTarget(l);
						if (referenceTarget != null)
						{
							if (!referencingNodes.TryGetValue(referenceTarget, out var value3))
							{
								value3 = new HashSet<INode>();
								referencingNodes.Add(referenceTarget, value3);
							}
							value3.Add(nodeGeneric);
						}
					}
				}
			}
		}

		public IEnumerable<INode> GetEvaluatingNodes(INode node)
		{
			if (evaluatingNodes.TryGetValue(node, out var value))
			{
				return value;
			}
			return Enumerable.Empty<INode>();
		}

		public IEnumerable<INode> GetImpulsingNodes(INode node)
		{
			if (impulsingNodes.TryGetValue(node, out var value))
			{
				return value;
			}
			return Enumerable.Empty<INode>();
		}

		public IEnumerable<INode> GetReferencingNodes(INode node)
		{
			if (referencingNodes.TryGetValue(node, out var value))
			{
				return value;
			}
			return Enumerable.Empty<INode>();
		}
	}
	public enum SourceElement
	{
		NONE,
		Input,
		Output,
		Impulse,
		Action,
		Reference,
		ReferencingNode
	}
	public class NodeQueryContext
	{
		public bool WalkEvaluationInputs;

		public bool WalkImpulseTargets;

		public bool WalkReferences;

		public bool WalkEvaluatingNodes;

		public bool WalkImpulsingNodes;

		public bool WalkReferencingNodes;

		public bool SkipWalkingBack = true;

		public NodeRuntime OnlyRuntime;

		public Predicate<INode> NodeFilter;

		public Dictionary<INode, bool> WalkedNodes;

		public HashSet<INode> LoopNodes;

		public NodeQueryAcceleration Acceleration;

		public bool RequiresAcceleration
		{
			get
			{
				if (!WalkEvaluatingNodes && !WalkImpulsingNodes)
				{
					return WalkReferencingNodes;
				}
				return true;
			}
		}

		public bool DetectedAnyLoops
		{
			get
			{
				HashSet<INode> loopNodes = LoopNodes;
				if (loopNodes == null)
				{
					return false;
				}
				return loopNodes.Count > 0;
			}
		}

		public void EnsureAllocatedStructures(INode node)
		{
			if (WalkedNodes == null)
			{
				WalkedNodes = new Dictionary<INode, bool>();
			}
			if (Acceleration == null && RequiresAcceleration)
			{
				Acceleration = new NodeQueryAcceleration(node.Runtime.Group);
			}
		}

		public bool BeginWalkingNode(INode node)
		{
			if (WalkedNodes.TryGetValue(node, out var value))
			{
				if (value)
				{
					if (LoopNodes == null)
					{
						LoopNodes = new HashSet<INode>();
					}
					LoopNodes.Add(node);
				}
				return false;
			}
			WalkedNodes.Add(node, value: true);
			return true;
		}

		public void FinishWalkingNode(INode node)
		{
			WalkedNodes[node] = false;
		}

		public void SetWalkAll()
		{
			SetWalkAllEvaluation();
			SetWalkAllImpulses();
			SetWalkAllReferences();
		}

		public void SetWalkAllEvaluation()
		{
			WalkEvaluationInputs = true;
			WalkEvaluatingNodes = true;
		}

		public void SetWalkAllImpulses()
		{
			WalkImpulseTargets = true;
			WalkImpulsingNodes = true;
		}

		public void SetWalkAllReferences()
		{
			WalkReferences = true;
			WalkReferencingNodes = true;
		}
	}
	public static class NodeQueryHelper
	{
		public static IEnumerable<INode> EnumerateNodes(this INode node, NodeQueryContext context, INode sourceNode = null, SourceElement sourceElementType = SourceElement.NONE, object sourceElement = null)
		{
			if ((context.OnlyRuntime != null && node.Runtime != context.OnlyRuntime) || (context.NodeFilter != null && !context.NodeFilter(node)))
			{
				yield break;
			}
			context.EnsureAllocatedStructures(node);
			if (!context.BeginWalkingNode(node))
			{
				yield break;
			}
			if (sourceNode != null)
			{
				yield return node;
			}
			if (context.WalkEvaluationInputs)
			{
				for (int i = 0; i < node.InputCount; i++)
				{
					IOutput inputSource = node.GetInputSource(i);
					if (inputSource == null || (context.SkipWalkingBack && inputSource.OwnerNode == sourceNode && sourceElementType == SourceElement.Output))
					{
						continue;
					}
					foreach (INode item in inputSource.OwnerNode.EnumerateNodes(context, node, SourceElement.Input))
					{
						yield return item;
					}
				}
			}
			if (context.WalkImpulseTargets)
			{
				for (int i = 0; i < node.ImpulseCount; i++)
				{
					IOperation impulseTarget = node.GetImpulseTarget(i);
					if (impulseTarget == null || (context.SkipWalkingBack && impulseTarget.OwnerNode == sourceNode && sourceElementType == SourceElement.Action))
					{
						continue;
					}
					foreach (INode item2 in impulseTarget.OwnerNode.EnumerateNodes(context, node, SourceElement.Impulse))
					{
						yield return item2;
					}
				}
			}
			if (context.WalkReferences)
			{
				for (int i = 0; i < node.FixedReferenceCount; i++)
				{
					INode referenceTarget = node.GetReferenceTarget(i);
					if (referenceTarget == null || (context.SkipWalkingBack && referenceTarget == sourceNode && sourceElementType == SourceElement.ReferencingNode))
					{
						continue;
					}
					foreach (INode item3 in referenceTarget.EnumerateNodes(context, node, SourceElement.Reference))
					{
						yield return item3;
					}
				}
			}
			if (context.WalkEvaluatingNodes)
			{
				foreach (INode evaluatingNode in context.Acceleration.GetEvaluatingNodes(node))
				{
					if (context.SkipWalkingBack && evaluatingNode == sourceNode && sourceElementType == SourceElement.Input)
					{
						continue;
					}
					foreach (INode item4 in evaluatingNode.EnumerateNodes(context, node, SourceElement.Output))
					{
						yield return item4;
					}
				}
			}
			if (context.WalkImpulsingNodes)
			{
				foreach (INode impulsingNode in context.Acceleration.GetImpulsingNodes(node))
				{
					if (context.SkipWalkingBack && impulsingNode == sourceNode && sourceElementType == SourceElement.Impulse)
					{
						continue;
					}
					foreach (INode item5 in impulsingNode.EnumerateNodes(context, node, SourceElement.Action))
					{
						yield return item5;
					}
				}
			}
			if (context.WalkReferencingNodes)
			{
				foreach (INode referencingNode in context.Acceleration.GetReferencingNodes(node))
				{
					if (context.SkipWalkingBack && referencingNode == sourceNode && sourceElementType == SourceElement.Reference)
					{
						continue;
					}
					foreach (INode item6 in referencingNode.EnumerateNodes(context, node, SourceElement.ReferencingNode))
					{
						yield return item6;
					}
				}
			}
			context.FinishWalkingNode(node);
		}
	}
	public static class EvaluationValidator
	{
		public static bool IsSourceValid(this INode node, int inputIndex, IOutput source)
		{
			if (node.GetInputTypeClass(inputIndex) != source.OutputDataClass)
			{
				return false;
			}
			if (node.Runtime != source.OwnerNode.Runtime)
			{
				return node.GetInputCrossRuntime(inputIndex)?.IsValidTargetRuntime(source.OwnerNode.Runtime) ?? false;
			}
			return true;
		}

		public static bool CanEvaluate(this INode node, int inputIndex, IOutput source)
		{
			if (!node.IsSourceValid(inputIndex, source))
			{
				return false;
			}
			foreach (INode item in source.OwnerNode.EnumerateNodes(new NodeQueryContext
			{
				WalkEvaluationInputs = true,
				OnlyRuntime = node.Runtime
			}))
			{
				if (item == node)
				{
					return false;
				}
			}
			return true;
		}

		public static bool AreEvaluationConnectionsValid(this NodeGroup group)
		{
			foreach (NodeRuntime runtime in group.Runtimes)
			{
				if (!runtime.AreEvaluationConnectionsValid())
				{
					return false;
				}
			}
			return true;
		}

		public static bool AreEvaluationConnectionsValid(this NodeRuntime runtime)
		{
			for (int i = 0; i < runtime.NodeCount; i++)
			{
				INode nodeGeneric = runtime.GetNodeGeneric(i);
				for (int j = 0; j < nodeGeneric.InputCount; j++)
				{
					IOutput inputSource = nodeGeneric.GetInputSource(j);
					if (inputSource != null && !nodeGeneric.IsSourceValid(j, inputSource))
					{
						return false;
					}
				}
			}
			NodeQueryContext nodeQueryContext = new NodeQueryContext();
			nodeQueryContext.WalkEvaluationInputs = true;
			nodeQueryContext.OnlyRuntime = runtime;
			for (int k = 0; k < runtime.NodeCount; k++)
			{
				INode nodeGeneric2 = runtime.GetNodeGeneric(k);
				foreach (INode item in nodeGeneric2.EnumerateNodes(nodeQueryContext))
				{
					if (item == nodeGeneric2 || nodeQueryContext.DetectedAnyLoops)
					{
						return false;
					}
				}
				if (nodeQueryContext.DetectedAnyLoops)
				{
					return false;
				}
			}
			return true;
		}
	}
	[Flags]
	public enum OperationInvokeSource
	{
		SyncImport = 1,
		AsyncImport = 2,
		Continuation = 4,
		SyncCall = 8,
		AsyncCall = 0x10,
		SyncResumption = 0x20,
		AsyncResumption = 0x40,
		NONE = 0,
		ALL_Imports = 3,
		ALL_Calls = 0x18,
		ALL_Sync = 0x29,
		ALL_Async = 0x52,
		ALL_Resumptions = 0x60,
		ALL = 0x7F
	}
	public struct ImpulseFlowContext
	{
		public bool isAsync;

		public bool isContinuation;

		public HashSet<IOperation> allWalkedOperations;

		public HashSet<IOperation> continuationLoopOperations;

		public void ClearContinuation()
		{
			isContinuation = false;
			continuationLoopOperations = new HashSet<IOperation>();
		}
	}
	public readonly struct ImpulseSource : IEquatable<ImpulseSource>
	{
		public readonly INode node;

		public readonly int impulseIndex;

		public ImpulseSource(INode node, int impulseIndex)
		{
			this.node = node;
			this.impulseIndex = impulseIndex;
		}

		public bool Equals(ImpulseSource other)
		{
			if (node == other.node)
			{
				return impulseIndex == other.impulseIndex;
			}
			return false;
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(node, impulseIndex);
		}

		public override string ToString()
		{
			return $"{node.GetImpulseName(impulseIndex)} on {node}";
		}
	}
	public abstract class ImpulseValidationError
	{
		public abstract string Description { get; }

		public override string ToString()
		{
			return Description;
		}
	}
	public class InvalidImpulseTargetError : ImpulseValidationError
	{
		public readonly INode SourceNode;

		public readonly int ImpulseIndex;

		public readonly IOperation TargetOperation;

		public readonly string Reason;

		public override string Description => $"InvalidTarget for Impulse {SourceNode.GetImpulseName(ImpulseIndex)} on {SourceNode}. Targetting: {TargetOperation}. Reason: {Reason}";

		public InvalidImpulseTargetError(INode sourceNode, int impulseIndex, IOperation targetOperation, string reason)
		{
			SourceNode = sourceNode;
			ImpulseIndex = impulseIndex;
			TargetOperation = targetOperation;
			Reason = reason;
		}
	}
	public class ImpulseLoopError : ImpulseValidationError
	{
		public readonly INode Node;

		public readonly int ImpulseIndex;

		public readonly bool IsAsync;

		public override string Description => $"Continuation Loop detected at impulse {Node.GetImpulseName(ImpulseIndex)} on node {Node}";

		public ImpulseLoopError(INode node, int impulseIndex, bool isAsync)
		{
			Node = node;
			ImpulseIndex = impulseIndex;
			IsAsync = isAsync;
		}
	}
	public class ImpulseFlowError : ImpulseValidationError
	{
		public readonly INode ReferencingNode;

		public readonly int ImpulseIndex;

		public readonly IOperation Target;

		public readonly bool ContextIsAsync;

		public readonly bool ContextIsContinuation;

		public readonly string Reason;

		public IOperation Origin { get; private set; }

		public bool OriginIsAsync { get; private set; }

		public bool OriginIsContinuation { get; private set; }

		public override string Description => $"InvalidImpulseFlow originating from {Origin} (Async: {OriginIsAsync}, Continuation: {OriginIsContinuation}). Impulse {ReferencingNode.GetImpulseName(ImpulseIndex)} (Async: {ContextIsAsync}, Continuation: {ContextIsContinuation}) on {ReferencingNode} is targetting: {Target}: {Reason}";

		public void SetOriginInfo(IOperation origin, bool originIsAsync, bool originIsContinuation)
		{
			Origin = origin;
			OriginIsAsync = originIsAsync;
			OriginIsContinuation = originIsContinuation;
		}

		public ImpulseFlowError(INode referencingNode, int impulseIndex, IOperation target, ImpulseFlowContext context, string reason)
			: this(referencingNode, impulseIndex, target, context.isAsync, context.isContinuation, reason)
		{
		}

		public ImpulseFlowError(INode referencingNode, int impulseIndex, IOperation target, bool contextIsAsync, bool contextIsContinuation, string reason)
		{
			ReferencingNode = referencingNode;
			ImpulseIndex = impulseIndex;
			Target = target;
			ContextIsAsync = contextIsAsync;
			ContextIsContinuation = contextIsContinuation;
			Reason = reason;
		}
	}
	public static class ImpulseValidator
	{
		public static InvalidImpulseTargetError ValidateTarget(this INode node, int impulseIndex, IOperation target)
		{
			if (node.Runtime != target.OwnerNode.Runtime)
			{
				return new InvalidImpulseTargetError(node, impulseIndex, target, "Runtime mismatch");
			}
			ImpulseType impulseType = node.GetImpulseType(impulseIndex);
			if (target is IAsyncOperation && (impulseType == ImpulseType.Call || impulseType == ImpulseType.SyncResumption))
			{
				return new InvalidImpulseTargetError(node, impulseIndex, target, "Cannot target async from sync impulse");
			}
			if (target is ContinuationExport && impulseType != ImpulseType.Continuation)
			{
				return new InvalidImpulseTargetError(node, impulseIndex, target, "Cannot target continuation export from a non-continuation");
			}
			return null;
		}

		public static bool CanImpulse(this INode node, string impulseName, IOperation target)
		{
			ImpulseMetadata impulseByName = node.Metadata.GetImpulseByName(impulseName);
			return node.CanImpulse(impulseByName.Index, target);
		}

		public static bool CanImpulse(this INode node, int impulseIndex, IOperation target)
		{
			if (node.ValidateTarget(impulseIndex, target) != null)
			{
				return false;
			}
			if (node.WouldFormContinuationLoop(impulseIndex, target))
			{
				return false;
			}
			if (target.ReachesAsyncOperation() && node.ComputeOperationInvokeSourcesForImpulse(impulseIndex).HasAnyFlags(OperationInvokeSource.ALL_Sync))
			{
				return false;
			}
			if (target is ContinuationExport && node.ComputeOperationInvokeSourcesForImpulse(impulseIndex).HasAnyFlags(OperationInvokeSource.ALL_Calls))
			{
				return false;
			}
			return true;
		}

		public static bool WouldFormContinuationLoop(this INode node, int impulseIndex, IOperation target)
		{
			if (node.GetImpulseType(impulseIndex) != ImpulseType.Continuation)
			{
				return false;
			}
			ImpulseFlowContext context = default(ImpulseFlowContext);
			context.isAsync = true;
			context.isContinuation = true;
			context.allWalkedOperations = new HashSet<IOperation>();
			context.continuationLoopOperations = new HashSet<IOperation>();
			context.continuationLoopOperations.Add(target);
			return target.ValidateImpulseFlow(context) != null;
		}

		public static OperationInvokeSource ComputeOperationInvokeSourcesForImpulse(this INode node, int impulseIndex)
		{
			OperationInvokeSource operationInvokeSource = node.ComputeNodeOperationInvokeSources();
			switch (node.GetImpulseType(impulseIndex))
			{
			case ImpulseType.Call:
				operationInvokeSource |= OperationInvokeSource.SyncCall;
				break;
			case ImpulseType.AsyncCall:
				operationInvokeSource |= OperationInvokeSource.AsyncCall;
				break;
			case ImpulseType.SyncResumption:
				operationInvokeSource |= OperationInvokeSource.SyncResumption;
				break;
			case ImpulseType.AsyncResumption:
				operationInvokeSource |= OperationInvokeSource.AsyncResumption;
				break;
			}
			return operationInvokeSource;
		}

		public static OperationInvokeSource ComputeNodeOperationInvokeSources(this INode node)
		{
			NodeRuntime runtime = node.Runtime;
			OperationInvokeSource operationInvokeSource = OperationInvokeSource.NONE;
			for (int i = 0; i < runtime.ImpulseImportsCount; i++)
			{
				operationInvokeSource |= ComputeOperationInvokeSourcesFromImpulseImport(runtime, i, node);
			}
			return operationInvokeSource;
		}

		public static OperationInvokeSource ComputeOperationInvokeSourcesFromImpulseImport(NodeRuntime runtime, int importIndex, INode target)
		{
			IOperation impulseImport = runtime.GetImpulseImport(importIndex);
			if (impulseImport == null)
			{
				return OperationInvokeSource.NONE;
			}
			OperationInvokeSource operationInvokeSource;
			if (impulseImport != target)
			{
				operationInvokeSource = impulseImport.OwnerNode.ComputeOperationInvokeSourcesFromSource(target);
				if (operationInvokeSource == OperationInvokeSource.NONE)
				{
					return OperationInvokeSource.NONE;
				}
			}
			else
			{
				operationInvokeSource = OperationInvokeSource.NONE;
			}
			return (OperationInvokeSource)((int)operationInvokeSource | ((!runtime.IsImpulseImportAsync(importIndex)) ? 1 : 2));
		}

		public static OperationInvokeSource ComputeOperationInvokeSourcesFromSource(this INode node, INode target)
		{
			OperationInvokeSource operationInvokeSource = OperationInvokeSource.NONE;
			for (int i = 0; i < node.ImpulseCount; i++)
			{
				IOperation impulseTarget = node.GetImpulseTarget(i);
				if (impulseTarget == null)
				{
					continue;
				}
				if (impulseTarget.OwnerNode == target)
				{
					switch (node.GetImpulseType(i))
					{
					case ImpulseType.Continuation:
						operationInvokeSource |= OperationInvokeSource.Continuation;
						break;
					case ImpulseType.Call:
						operationInvokeSource |= OperationInvokeSource.SyncCall;
						break;
					case ImpulseType.AsyncCall:
						operationInvokeSource |= OperationInvokeSource.AsyncCall;
						break;
					case ImpulseType.SyncResumption:
						operationInvokeSource |= OperationInvokeSource.SyncResumption;
						break;
					case ImpulseType.AsyncResumption:
						operationInvokeSource |= OperationInvokeSource.AsyncResumption;
						break;
					}
					if (operationInvokeSource == OperationInvokeSource.ALL)
					{
						return operationInvokeSource;
					}
				}
				else
				{
					operationInvokeSource |= impulseTarget.OwnerNode.ComputeOperationInvokeSourcesFromSource(target);
				}
			}
			return operationInvokeSource;
		}

		public static bool ReachesAsyncOperation(this IOperation operation)
		{
			if (operation == null)
			{
				return false;
			}
			if (operation is IAsyncOperation)
			{
				return true;
			}
			Node ownerNode = operation.OwnerNode;
			for (int i = 0; i < ownerNode.ImpulseCount; i++)
			{
				if (ownerNode.GetImpulseType(i) != ImpulseType.Call && ownerNode.GetImpulseType(i) != ImpulseType.AsyncCall)
				{
					IOperation impulseTarget = ownerNode.GetImpulseTarget(i);
					if (impulseTarget != null && impulseTarget.ReachesAsyncOperation())
					{
						return true;
					}
				}
			}
			return false;
		}

		public static ImpulseValidationError ValidateImpulseFlow(this IOperation targettedOperation, ImpulseFlowContext context)
		{
			Node node = targettedOperation.OwnerNode;
			if (targettedOperation is ContinuationExport)
			{
				if (context.isContinuation)
				{
					return null;
				}
				return new ImpulseFlowError(node, -1, targettedOperation, context, "Non-continuations cannot target continuation exports.");
			}
			if (targettedOperation is CallExport)
			{
				return null;
			}
			if (targettedOperation is AsyncCallExport)
			{
				if (context.isAsync)
				{
					return null;
				}
				return new ImpulseFlowError(node, -1, targettedOperation, context, "Non-async flow cannot target async call export");
			}
			targettedOperation.FindOperationIndex(out var index, out var listIndex);
			int i;
			for (i = 0; i < node.ImpulseCount; i++)
			{
				string impulseName = node.GetImpulseName(i);
				IOperation target = node.GetImpulseTarget(i);
				if (target == null)
				{
					continue;
				}
				if (listIndex < 0)
				{
					if (!node.CanOperationContinueTo(index, impulseName))
					{
						continue;
					}
				}
				else if (!node.CanOperationListContinueTo(listIndex, impulseName))
				{
					continue;
				}
				bool flag = !context.allWalkedOperations.Add(target);
				bool flag2 = true;
				try
				{
					ImpulseType impulseType = node.GetImpulseType(i);
					if (target is ContinuationExport && !context.isContinuation)
					{
						return GenerateError("Non-continuations cannot target continuation exports.");
					}
					if (target is IAsyncOperation && !context.isAsync && impulseType != ImpulseType.AsyncResumption)
					{
						return GenerateError("Cannot target async operations from synchronous context");
					}
					ImpulseFlowContext context2 = context;
					if (impulseType != ImpulseType.Continuation)
					{
						context2.ClearContinuation();
						flag2 = false;
					}
					if (!context2.continuationLoopOperations.Add(target))
					{
						return new ImpulseLoopError(node, i, context.isAsync);
					}
					if (context2.isAsync)
					{
						if (impulseType == ImpulseType.Call || impulseType == ImpulseType.SyncResumption)
						{
							context2.isAsync = false;
						}
					}
					else if (impulseType == ImpulseType.AsyncResumption)
					{
						context2.isAsync = true;
					}
					if (!flag)
					{
						ImpulseValidationError impulseValidationError = target.ValidateImpulseFlow(context2);
						if (impulseValidationError != null)
						{
							return impulseValidationError;
						}
					}
				}
				finally
				{
					if (!flag)
					{
						context.allWalkedOperations.Remove(target);
					}
					if (flag2)
					{
						context.continuationLoopOperations.Remove(target);
					}
				}
				ImpulseFlowError GenerateError(string reason)
				{
					return new ImpulseFlowError(node, i, target, context, reason);
				}
			}
			return null;
		}

		public static void GetSourcesUnreachableFromImports(this NodeRuntime runtime, HashSet<ImpulseSource> sources)
		{
			if (sources.Count > 0)
			{
				throw new ArgumentException("Hash set must be empty!");
			}
			for (int i = 0; i < runtime.NodeCount; i++)
			{
				INode nodeGeneric = runtime.GetNodeGeneric(i);
				if (nodeGeneric.FixedImpulseCount == 0 && nodeGeneric.DynamicImpulseCount == 0)
				{
					continue;
				}
				for (int j = 0; j < nodeGeneric.ImpulseCount; j++)
				{
					IOperation impulseTarget = nodeGeneric.GetImpulseTarget(j);
					if (impulseTarget != null && !(impulseTarget is ImpulseExport))
					{
						sources.Add(new ImpulseSource(nodeGeneric, j));
					}
				}
			}
			for (int k = 0; k < runtime.ImpulseImportsCount; k++)
			{
				IOperation impulseImport = runtime.GetImpulseImport(k);
				if (impulseImport != null)
				{
					RemoveReachableSources(impulseImport, sources);
				}
			}
			if (sources.Count <= 0)
			{
				return;
			}
			List<ImpulseSource> list = sources.ToList();
			HashSet<Node> walkedNodes = new HashSet<Node>();
			foreach (ImpulseSource item in list)
			{
				if (sources.Contains(item) && item.node.GetImpulseType(item.impulseIndex) != ImpulseType.Continuation)
				{
					RemoveReachableSources(item.node.GetImpulseTarget(item.impulseIndex), sources, walkedNodes);
				}
			}
		}

		private static void RemoveReachableSources(IOperation operation, HashSet<ImpulseSource> sources, HashSet<Node> walkedNodes = null)
		{
			if (sources.Count == 0)
			{
				return;
			}
			Node ownerNode = operation.OwnerNode;
			if (ownerNode.FixedImpulseCount == 0 && ownerNode.DynamicImpulseCount == 0)
			{
				return;
			}
			int index = operation.FindLinearOperationIndex();
			for (int i = 0; i < ownerNode.FixedImpulseCount; i++)
			{
				if (!ownerNode.CanOperationContinueTo(index, ownerNode.GetImpulseName(i)))
				{
					continue;
				}
				IOperation impulseTarget = ownerNode.GetImpulseTarget(i);
				if (impulseTarget != null)
				{
					ImpulseSource item = new ImpulseSource(ownerNode, i);
					if (sources.Remove(item))
					{
						walkedNodes?.Add(impulseTarget.OwnerNode);
						RemoveReachableSources(impulseTarget, sources, walkedNodes);
					}
				}
			}
			int num = ownerNode.FixedImpulseCount;
			for (int j = 0; j < ownerNode.DynamicImpulseCount; j++)
			{
				IImpulseList impulseList = ownerNode.GetImpulseList(j);
				if (!ownerNode.CanOperationContinueTo(index, ownerNode.GetImpulseListName(j)))
				{
					num += impulseList.Count;
					continue;
				}
				for (int k = 0; k < impulseList.Count; k++)
				{
					IOperation impulseTarget2 = impulseList.GetImpulseTarget(k);
					if (impulseTarget2 != null)
					{
						ImpulseSource item2 = new ImpulseSource(ownerNode, num + k);
						if (sources.Remove(item2))
						{
							walkedNodes?.Add(impulseTarget2.OwnerNode);
							RemoveReachableSources(impulseTarget2, sources, walkedNodes);
						}
					}
				}
				num += impulseList.Count;
			}
		}

		public static ImpulseValidationError ValidateImpulseConnections(this NodeGroup group)
		{
			foreach (NodeRuntime runtime in group.Runtimes)
			{
				ImpulseValidationError impulseValidationError = runtime.ValidateImpulseConnections();
				if (impulseValidationError != null)
				{
					return impulseValidationError;
				}
			}
			return null;
		}

		public static ImpulseValidationError ValidateImpulseConnections(this NodeRuntime runtime)
		{
			for (int i = 0; i < runtime.NodeCount; i++)
			{
				INode nodeGeneric = runtime.GetNodeGeneric(i);
				for (int j = 0; j < nodeGeneric.ImpulseCount; j++)
				{
					IOperation impulseTarget = nodeGeneric.GetImpulseTarget(j);
					if (impulseTarget != null)
					{
						InvalidImpulseTargetError invalidImpulseTargetError = nodeGeneric.ValidateTarget(j, impulseTarget);
						if (invalidImpulseTargetError != null)
						{
							return invalidImpulseTargetError;
						}
					}
				}
			}
			for (int k = 0; k < runtime.ImpulseImportsCount; k++)
			{
				IOperation impulseImport = runtime.GetImpulseImport(k);
				if (impulseImport != null)
				{
					bool flag = runtime.IsImpulseImportAsync(k);
					ImpulseFlowContext context = default(ImpulseFlowContext);
					context.isContinuation = true;
					context.isAsync = flag;
					context.allWalkedOperations = new HashSet<IOperation>();
					context.continuationLoopOperations = new HashSet<IOperation>();
					context.continuationLoopOperations.Add(impulseImport);
					ImpulseValidationError impulseValidationError = impulseImport.ValidateImpulseFlow(context);
					if (impulseValidationError is ImpulseFlowError impulseFlowError)
					{
						impulseFlowError.SetOriginInfo(impulseImport, flag, originIsContinuation: true);
					}
					if (impulseValidationError != null)
					{
						return impulseValidationError;
					}
				}
			}
			HashSet<ImpulseSource> hashSet = new HashSet<ImpulseSource>();
			runtime.GetSourcesUnreachableFromImports(hashSet);
			foreach (ImpulseSource item in hashSet)
			{
				ImpulseType impulseType = item.node.GetImpulseType(item.impulseIndex);
				IOperation impulseTarget2 = item.node.GetImpulseTarget(item.impulseIndex);
				bool flag2 = impulseType == ImpulseType.AsyncCall || impulseType == ImpulseType.AsyncResumption;
				ImpulseFlowContext context2 = default(ImpulseFlowContext);
				context2.isContinuation = impulseType == ImpulseType.Continuation;
				context2.isAsync = flag2;
				context2.allWalkedOperations = new HashSet<IOperation>();
				context2.continuationLoopOperations = new HashSet<IOperation>();
				context2.continuationLoopOperations.Add(impulseTarget2);
				ImpulseValidationError impulseValidationError2 = impulseTarget2.ValidateImpulseFlow(context2);
				if (impulseValidationError2 is ImpulseFlowError impulseFlowError2)
				{
					impulseFlowError2.SetOriginInfo(impulseTarget2, flag2, originIsContinuation: true);
				}
				if (impulseValidationError2 != null)
				{
					return impulseValidationError2;
				}
			}
			return null;
		}
	}
	public static class NodeHelper
	{
		public static int FindLinearOutputIndex(this IOutput output)
		{
			output.FindOutputIndex(out var index, out var listIndex);
			if (listIndex < 0)
			{
				return index;
			}
			for (int i = 0; i < listIndex; i++)
			{
				index += output.OwnerNode.GetOutputList(i).Count;
			}
			return index;
		}

		public static void FindOutputIndex(this IOutput output, out int index, out int listIndex)
		{
			Node ownerNode = output.OwnerNode;
			for (int i = 0; i < ownerNode.FixedOutputCount; i++)
			{
				if (ownerNode.GetOutput(i) == output)
				{
					index = i;
					listIndex = -1;
					return;
				}
			}
			IListOutput listOutput = (IListOutput)output;
			for (int j = 0; j < ownerNode.DynamicOutputCount; j++)
			{
				if (ownerNode.GetOutputList(j) == listOutput.List)
				{
					index = listOutput.Index;
					listIndex = j;
					return;
				}
			}
			throw new NotImplementedException("Unsuported output type: " + output);
		}

		public static void FindOperationIndex(this IOperation operation, out int index, out int listIndex)
		{
			Node ownerNode = operation.OwnerNode;
			for (int i = 0; i < ownerNode.FixedOperationCount; i++)
			{
				if (ownerNode.GetOperation(i) == operation)
				{
					index = i;
					listIndex = -1;
					return;
				}
			}
			IListOperation listOperation = (IListOperation)operation;
			for (int j = 0; j < ownerNode.DynamicOperationCount; j++)
			{
				if (ownerNode.GetOperationList(j) == listOperation.List)
				{
					index = listOperation.Index;
					listIndex = j;
					return;
				}
			}
			throw new NotImplementedException("Unsupported action type: " + operation);
		}

		public static int FindLinearOperationIndex(this IOperation operation)
		{
			operation.FindOperationIndex(out var index, out var listIndex);
			if (listIndex < 0)
			{
				return index;
			}
			for (int i = 0; i < listIndex; i++)
			{
				index += operation.OwnerNode.GetOperationList(i).Count;
			}
			return index;
		}

		public static IOutput RemapOutput(this IOutput output, Dictionary<INode, INode> remappedNodes = null)
		{
			if (output == null)
			{
				return null;
			}
			if (remappedNodes != null && output.OwnerNode != null && remappedNodes.TryGetValue(output.OwnerNode, out var value))
			{
				output.FindOutputIndex(out var index, out var listIndex);
				NodeMetadata metadata = value.Metadata;
				NodeMetadata metadata2 = output.OwnerNode.Metadata;
				if (listIndex < 0)
				{
					OutputMetadata outputByName = metadata.GetOutputByName(metadata2.FixedOutputs[index].Name);
					if (outputByName == null)
					{
						return null;
					}
					return value.GetOutput(outputByName.Index);
				}
				OutputListMetadata outputListByName = metadata.GetOutputListByName(metadata2.DynamicOutputs[listIndex].Name);
				if (outputListByName == null)
				{
					return null;
				}
				IOutputList outputList = value.GetOutputList(outputListByName.Index);
				if (index >= outputList.Count)
				{
					return null;
				}
				return outputList.GetOutput(index);
			}
			return output;
		}

		public static IOperation RemapTarget(this IOperation target, Dictionary<INode, INode> remappedNodes = null)
		{
			if (remappedNodes != null && target.OwnerNode != null && remappedNodes.TryGetValue(target.OwnerNode, out var value))
			{
				target.FindOperationIndex(out var index, out var listIndex);
				NodeMetadata metadata = value.Metadata;
				NodeMetadata metadata2 = target.OwnerNode.Metadata;
				if (listIndex < 0)
				{
					OperationMetadata operationByName = metadata.GetOperationByName(metadata2.FixedOperations[index].Name);
					if (operationByName == null)
					{
						return null;
					}
					return value.GetOperation(operationByName.Index);
				}
				OperationMetadata operationByName2 = metadata.GetOperationByName(metadata2.DynamicOutputs[listIndex].Name);
				if (operationByName2 == null)
				{
					return null;
				}
				IOperationList operationList = value.GetOperationList(operationByName2.Index);
				if (index >= operationList.Count)
				{
					return null;
				}
				return operationList.GetOperation(index);
			}
			return target;
		}
	}
	public class ObjectCastAttribute : Attribute
	{
	}
	public abstract class Output<T> : IOutput<T>, IOutput
	{
		private Node _node;

		public Node OwnerNode => _node;

		public Type OutputType => typeof(T);

		public abstract DataClass OutputDataClass { get; }

		public Output(Node owner)
		{
			_node = owner;
		}

		public override string ToString()
		{
			if (!(OwnerNode is DataImportNode))
			{
				return $"Output<{typeof(T)}> ({OwnerNode.GetOutputName(this.FindLinearOutputIndex())} on {OwnerNode})";
			}
			return $"Data Import ({typeof(T)}) on {OwnerNode?.Runtime?.Group?.Name}";
		}
	}
	public abstract class OutputNode<T> : Node, IOutput<T>, IOutput
	{
		public Node OwnerNode => this;

		public int OutputIndex => 0;

		public Type OutputType => typeof(T);

		public abstract DataClass OutputDataClass { get; }
	}
	public abstract class ValueOutputNode<T> : OutputNode<T> where T : unmanaged
	{
		public override DataClass OutputDataClass => DataClass.Value;
	}
	public readonly struct InsertedConversion
	{
		public readonly INode conversion;

		public readonly INode targetNode;

		public readonly ElementRef targetInput;

		public InsertedConversion(INode conversion, INode targetNode, ElementRef targetInput)
		{
			this.conversion = conversion;
			this.targetNode = targetNode;
			this.targetInput = targetInput;
		}
	}
	public readonly struct ConnectionResult
	{
		public readonly bool connected;

		public readonly string failReason;

		public readonly List<InsertedConversion> conversions;

		public readonly NodeOverloadContext overload;

		public static ConnectionResult Success => new ConnectionResult(connected: true);

		public ConnectionResult(bool connected, string failReason = null)
		{
			this.connected = connected;
			conversions = null;
			overload = null;
			this.failReason = failReason;
		}

		public ConnectionResult(InsertedConversion conversion)
		{
			if (conversion.conversion != null)
			{
				conversions = new List<InsertedConversion> { conversion };
			}
			else
			{
				conversions = null;
			}
			connected = true;
			overload = null;
			failReason = null;
		}

		public ConnectionResult(List<InsertedConversion> conversions)
		{
			this.conversions = conversions;
			connected = true;
			overload = null;
			failReason = null;
		}

		public ConnectionResult(NodeOverloadContext overload, List<InsertedConversion> conversions)
		{
			this.overload = overload;
			this.conversions = conversions;
			connected = true;
			failReason = null;
		}

		public ConnectionResult(NodeOverloadContext overload, InsertedConversion conversion = default(InsertedConversion))
		{
			this.overload = overload;
			if (conversion.conversion != null)
			{
				conversions = new List<InsertedConversion> { conversion };
			}
			else
			{
				conversions = null;
			}
			connected = true;
			failReason = null;
		}

		public ConnectionResult Combine(ConnectionResult other)
		{
			if (connected != other.connected)
			{
				throw new InvalidOperationException("Cannot combine connection result where one failed and other didn't.\n" + $"First Result: {this}\nSecond Result: {this}");
			}
			if (overload != null && other.overload != null && overload != other.overload)
			{
				throw new InvalidOperationException("Cannot combine two connection results with different overload contexts");
			}
			List<InsertedConversion> list;
			if (conversions != null && other.conversions != null)
			{
				list = new List<InsertedConversion>(conversions);
				list.AddRange(other.conversions);
			}
			else
			{
				list = conversions ?? other.conversions;
			}
			if (!connected)
			{
				return this;
			}
			return new ConnectionResult(overload ?? other.overload, list);
		}

		public ConnectionResult AddInsertedConversion(InsertedConversion conversion)
		{
			if (!connected)
			{
				throw new InvalidOperationException("Cannot add inserted conversion to result that has failed to connnect");
			}
			if (conversions == null)
			{
				return new ConnectionResult(overload, new List<InsertedConversion> { conversion });
			}
			List<InsertedConversion> list = new List<InsertedConversion>(conversions);
			list.Add(conversion);
			return new ConnectionResult(overload, list);
		}

		public static ConnectionResult Failed(string reason)
		{
			return new ConnectionResult(connected: false, reason);
		}

		public static ConnectionResult Conversion(InsertedConversion conversion)
		{
			return new ConnectionResult(conversion);
		}

		public static ConnectionResult Conversions(List<InsertedConversion> conversions)
		{
			return new ConnectionResult(conversions);
		}

		public static ConnectionResult Overload(NodeOverloadContext overload)
		{
			return new ConnectionResult(overload, overload.InsertedCasts);
		}

		public override string ToString()
		{
			return $"Connected: {connected}. Casts: {conversions?.Count ?? 0}. Overload: {overload}. FailReason: {failReason}";
		}
	}
	public interface ICast : INode
	{
		bool IsImplicit { get; }

		Type InputType { get; }

		Type OutputType { get; }
	}
	public readonly struct InputType
	{
		public readonly Type type;

		public readonly NodeRuntime runtime;

		public InputType(Type type, NodeRuntime runtime)
		{
			this.type = type;
			this.runtime = runtime;
		}
	}
	public class NodeConnections
	{
		public Dictionary<string, InputType> fixedInputs = new Dictionary<string, InputType>();

		public Dictionary<string, List<InputType>> listInputs = new Dictionary<string, List<InputType>>();

		public Dictionary<string, InputType> fixedReferences = new Dictionary<string, InputType>();

		public bool SetInput(ElementRef input, string name, Type type, NodeRuntime runtime)
		{
			if (input.IsDynamic)
			{
				return SetListInput(name, input.index, type, runtime);
			}
			return SetInput(name, type, runtime);
		}

		public bool SetInput(string name, Type type, NodeRuntime runtime)
		{
			if (fixedInputs.TryGetValue(name, out var value) && value.type == type && value.runtime == runtime)
			{
				return false;
			}
			fixedInputs[name] = new InputType(type, runtime);
			return true;
		}

		public bool SetReference(string name, Type type, NodeRuntime runtime)
		{
			if (fixedReferences.TryGetValue(name, out var value) && value.type == type && value.runtime == runtime)
			{
				return false;
			}
			fixedReferences[name] = new InputType(type, runtime);
			return true;
		}

		public bool SetListInput(string name, int index, Type type, NodeRuntime runtime)
		{
			if (!listInputs.TryGetValue(name, out var value))
			{
				value = new List<InputType>();
				listInputs.Add(name, value);
			}
			if (index < 0)
			{
				index = value.Count;
			}
			while (value.Count <= index)
			{
				value.Add(default(InputType));
			}
			if (value[index].type == type && value[index].runtime == runtime)
			{
				return false;
			}
			value[index] = new InputType(type, runtime);
			return true;
		}

		public static NodeConnections GetFromNode(INode node)
		{
			NodeConnections nodeConnections = new NodeConnections();
			for (int i = 0; i < node.FixedInputCount; i++)
			{
				IOutput inputSource = node.GetInputSource(i);
				Type actualConnectedType = inputSource.GetActualConnectedType();
				if (!(actualConnectedType == null))
				{
					nodeConnections.fixedInputs.Add(node.GetInputName(i), new InputType(actualConnectedType, inputSource.OwnerNode.Runtime));
				}
			}
			for (int j = 0; j < node.DynamicInputCount; j++)
			{
				if (node.GetInputListTypeConstraint(j) == null)
				{
					continue;
				}
				List<InputType> list = new List<InputType>();
				IInputList inputList = node.GetInputList(j);
				for (int k = 0; k < inputList.Count; k++)
				{
					IOutput inputSource2 = inputList.GetInputSource(k);
					Type actualConnectedType2 = inputSource2.GetActualConnectedType();
					if (actualConnectedType2 == null)
					{
						list.Add(default(InputType));
					}
					else
					{
						list.Add(new InputType(actualConnectedType2, inputSource2.OwnerNode.Runtime));
					}
				}
				nodeConnections.listInputs.Add(node.GetInputListName(j), list);
			}
			for (int l = 0; l < node.FixedReferenceCount; l++)
			{
				INode referenceTarget = node.GetReferenceTarget(l);
				if (referenceTarget != null)
				{
					nodeConnections.fixedReferences.Add(node.GetReferenceName(l), new InputType(referenceTarget.GetType(), referenceTarget.Runtime));
				}
			}
			return nodeConnections;
		}
	}
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public class NodeOverloadAttribute : Attribute
	{
		public string OverloadName { get; private set; }

		public NodeOverloadAttribute(string overloadName)
		{
			OverloadName = overloadName;
		}
	}
	public class NodeOverloadCollection
	{
		private List<Type> _overloads = new List<Type>();

		public int Count => _overloads.Count;

		public IEnumerable<Type> Overloads => _overloads;

		internal void Add(Type type)
		{
			_overloads.Add(type);
		}

		public override string ToString()
		{
			if (Count == 0)
			{
				return "No overloads";
			}
			return $"{Count} overloads: " + string.Join(", ", _overloads);
		}
	}
	public readonly struct OverloadResult
	{
		public readonly string failReason;

		public bool IsSuccess => failReason == null;

		public static OverloadResult Success => new OverloadResult(null);

		private OverloadResult(string errorReason)
		{
			failReason = errorReason;
		}

		public static OverloadResult Fail(string reason)
		{
			return new OverloadResult(reason);
		}
	}
	public class NodeOverloadContext
	{
		private Dictionary<INode, Type> overloads = new Dictionary<INode, Type>();

		private Dictionary<INode, INode> swappedNodes = new Dictionary<INode, INode>();

		private HashSet<INode> affectedEvaluatingNodes = new HashSet<INode>();

		private HashSet<INode> affectedImpulsingNodes = new HashSet<INode>();

		private List<InsertedConversion> insertedCasts = new List<InsertedConversion>();

		private NodeQueryAcceleration query;

		public NodeGroup Group { get; private set; }

		public NodeRuntime Runtime { get; private set; }

		public bool OverloadedAnyNodes => overloads.Count > 0;

		public IEnumerable<KeyValuePair<INode, Type>> Overloads => overloads;

		public IEnumerable<KeyValuePair<INode, INode>> SwappedNodes => swappedNodes;

		public IEnumerable<INode> AffectedEvaluatingNodes => affectedEvaluatingNodes;

		public IEnumerable<INode> AffectedImpulsingNodes => affectedImpulsingNodes;

		public List<InsertedConversion> InsertedCasts => insertedCasts;

		public INode GetSwappedNode(INode sourceNode)
		{
			return swappedNodes[sourceNode];
		}

		public NodeOverloadContext(NodeGroup group, NodeRuntime runtime)
		{
			Group = group;
			Runtime = runtime;
		}

		public OverloadResult TryOverload(INode node, NodeConnections connections, bool allowMergingGroups = false)
		{
			OverloadSearchContext context = new OverloadSearchContext(connections, Runtime, allowMergingGroups);
			Type type = NodeOverloadHelper.FindOverload(node.Overload, node.GetType(), context);
			if (type == null)
			{
				return OverloadResult.Fail("No overload found");
			}
			if (type == node.GetType())
			{
				return OverloadResult.Success;
			}
			overloads[node] = type;
			if (query == null)
			{
				query = new NodeQueryAcceleration(Group);
			}
			foreach (INode evaluatingNode in query.GetEvaluatingNodes(node))
			{
				NodeConnections modifiedConnections = GetModifiedConnections(evaluatingNode);
				if (modifiedConnections == null)
				{
					affectedEvaluatingNodes.Add(evaluatingNode);
					continue;
				}
				OverloadResult overloadResult = TryOverload(evaluatingNode, modifiedConnections);
				if (overloadResult.IsSuccess)
				{
					continue;
				}
				return OverloadResult.Fail($"Failed to overload referencing node: {evaluatingNode}. Inner reason: " + overloadResult.failReason);
			}
			return OverloadResult.Success;
		}

		public void SwapNodes()
		{
			foreach (KeyValuePair<INode, Type> overload in Overloads)
			{
				INode node = Runtime.AddNode(overload.Value);
				swappedNodes.Add(overload.Key, node);
				node.CopyDynamicOutputLayout(overload.Key);
				node.CopyDynamicOperationLayout(overload.Key);
			}
			foreach (KeyValuePair<INode, INode> swappedNode in swappedNodes)
			{
				Runtime.TranslateInputs(swappedNode.Value, swappedNode.Key, swappedNodes, insertedCasts);
				Runtime.TranslateImpulses(swappedNode.Value, swappedNode.Key, swappedNodes);
				Runtime.TranslateReferences(swappedNode.Value, swappedNode.Key, swappedNodes);
				foreach (INode impulsingNode in query.GetImpulsingNodes(swappedNode.Key))
				{
					if (!swappedNodes.ContainsKey(impulsingNode))
					{
						affectedImpulsingNodes.Add(impulsingNode);
					}
				}
			}
			affectedEvaluatingNodes.RemoveWhere((INode n) => swappedNodes.ContainsKey(n));
			foreach (INode affectedEvaluatingNode in affectedEvaluatingNodes)
			{
				for (int num = 0; num < affectedEvaluatingNode.InputCount; num++)
				{
					IOutput inputSource = affectedEvaluatingNode.GetInputSource(num);
					IOutput output = inputSource.RemapOutput(swappedNodes);
					if (output != inputSource)
					{
						affectedEvaluatingNode.SetInputSource(num, output);
					}
				}
			}
			foreach (INode affectedImpulsingNode in affectedImpulsingNodes)
			{
				for (int num2 = 0; num2 < affectedImpulsingNode.ImpulseCount; num2++)
				{
					IOperation impulseTarget = affectedImpulsingNode.GetImpulseTarget(num2);
					if (impulseTarget != null)
					{
						IOperation operation = impulseTarget.RemapTarget(swappedNodes);
						if (operation != impulseTarget)
						{
							affectedImpulsingNode.SetImpulseTarget(num2, operation);
						}
					}
				}
			}
			Runtime.RemapImportsAndExports(swappedNodes);
			Runtime.RemoveNodes((INode n) => swappedNodes.ContainsKey(n));
		}

		private NodeConnections GetModifiedConnections(INode node)
		{
			NodeConnections fromNode = NodeConnections.GetFromNode(node);
			bool flag = false;
			for (int i = 0; i < node.FixedInputCount; i++)
			{
				IOutput inputSource = node.GetInputSource(i);
				if (inputSource != null && !(inputSource.OwnerNode is DataImportNode))
				{
					Type overloadedType = GetOverloadedType(inputSource);
					if (!(overloadedType == null) && fromNode.SetInput(node.GetInputName(i), overloadedType, inputSource.OwnerNode.Runtime))
					{
						flag = true;
					}
				}
			}
			for (int j = 0; j < node.DynamicInputCount; j++)
			{
				IInputList inputList = node.GetInputList(j);
				for (int k = 0; k < inputList.Count; k++)
				{
					IOutput inputSource2 = inputList.GetInputSource(k);
					if (inputSource2 != null && !(inputSource2.OwnerNode is DataImportNode))
					{
						Type overloadedType2 = GetOverloadedType(inputSource2);
						if (!(overloadedType2 == null) && fromNode.SetListInput(node.GetInputListName(j), k, overloadedType2, inputSource2.OwnerNode.Runtime))
						{
							flag = true;
						}
					}
				}
			}
			if (!flag)
			{
				return null;
			}
			return fromNode;
		}

		public Type GetOverloadedType(INode node)
		{
			if (overloads.TryGetValue(node, out var value))
			{
				return value;
			}
			return null;
		}

		private Type GetOverloadedType(IOutput source)
		{
			if (overloads.TryGetValue(source.OwnerNode, out var value))
			{
				NodeMetadata metadata = NodeMetadataHelper.GetMetadata(value);
				source.FindOutputIndex(out var index, out var listIndex);
				if (listIndex < 0)
				{
					return metadata.FixedOutputs[index].OutputType;
				}
				return metadata.DynamicOutputs[listIndex].TypeConstraint;
			}
			return null;
		}
	}
	public static class NodeOverloadHelper
	{
		private static Dictionary<string, NodeOverloadCollection> overloads;

		private static CancellationTokenSource generationCancellation;

		private static object generationLock;

		static NodeOverloadHelper()
		{
			generationLock = new object();
			AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_AssemblyLoad;
		}

		public static NodeOverloadCollection GetOverloads(string name)
		{
			Dictionary<string, NodeOverloadCollection> dictionary = overloads;
			if (overloads == null)
			{
				lock (generationLock)
				{
					dictionary = SearchOverloads();
				}
			}
			if (dictionary.TryGetValue(name, out var value))
			{
				return value;
			}
			return null;
		}

		public static Dictionary<string, NodeOverloadCollection> SearchOverloads()
		{
			if (overloads != null)
			{
				return overloads;
			}
			Dictionary<string, NodeOverloadCollection> dictionary = new Dictionary<string, NodeOverloadCollection>();
			generationCancellation?.Cancel();
			generationCancellation = new CancellationTokenSource();
			CancellationToken token = generationCancellation.Token;
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			foreach (Assembly assembly in assemblies)
			{
				if (assembly.FullName.StartsWith("System."))
				{
					continue;
				}
				if (token.IsCancellationRequested)
				{
					break;
				}
				try
				{
					Type[] types = assembly.GetTypes();
					foreach (Type type in types)
					{
						if (token.IsCancellationRequested)
						{
							break;
						}
						if (type.IsAbstract || type.IsValueType)
						{
							continue;
						}
						foreach (NodeOverloadAttribute customAttribute in type.GetCustomAttributes<NodeOverloadAttribute>())
						{
							if (!dictionary.TryGetValue(customAttribute.OverloadName, out var value))
							{
								value = new NodeOverloadCollection();
								dictionary.Add(customAttribute.OverloadName, value);
							}
							value.Add(type);
						}
					}
				}
				catch (ReflectionTypeLoadException)
				{
				}
			}
			if (token.IsCancellationRequested)
			{
				return SearchOverloads();
			}
			overloads = dictionary;
			return dictionary;
		}

		private static void CurrentDomain_AssemblyLoad(object sender, AssemblyLoadEventArgs args)
		{
			generationCancellation?.Cancel();
			overloads = null;
		}

		public static Type FindOverload(string name, Type currentType, OverloadSearchContext context)
		{
			if (string.IsNullOrEmpty(name))
			{
				return null;
			}
			NodeOverloadCollection nodeOverloadCollection = GetOverloads(name);
			Type type = null;
			int num = int.MaxValue;
			foreach (Type overload in nodeOverloadCollection.Overloads)
			{
				Type compatibleNodeType = context.GetCompatibleNodeType(overload);
				if (compatibleNodeType == null)
				{
					continue;
				}
				int? num2 = RankOverload(compatibleNodeType, context);
				if (num2.HasValue && num2.Value < num)
				{
					type = context.InstanceType(compatibleNodeType, currentType);
					if (!(type == null))
					{
						num = num2.Value;
					}
				}
			}
			return type;
		}

		public static int? RankOverload(Type overload, OverloadSearchContext context)
		{
			NodeMetadata metadata = NodeMetadataHelper.GetMetadata(overload);
			context.BeginNewOverload();
			int num = 0;
			foreach (KeyValuePair<string, InputType> fixedInput in context.Connections.fixedInputs)
			{
				InputMetadata inputByName = metadata.GetInputByName(fixedInput.Key);
				if (inputByName == null)
				{
					return null;
				}
				if (!context.CanAccomodate(inputByName.InputType, fixedInput.Value.type))
				{
					return null;
				}
				if (!context.CanTargetRuntime(inputByName, fixedInput.Value.runtime))
				{
					return null;
				}
				num += inputByName.InputType.RankType();
			}
			foreach (KeyValuePair<string, List<InputType>> listInput in context.Connections.listInputs)
			{
				InputListMetadata inputListByName = metadata.GetInputListByName(listInput.Key);
				if (inputListByName == null)
				{
					return null;
				}
				if (inputListByName.TypeConstraint == null)
				{
					continue;
				}
				foreach (InputType item in listInput.Value)
				{
					if (!(item.type == null))
					{
						if (!context.CanAccomodate(inputListByName.TypeConstraint, item.type))
						{
							return null;
						}
						if (!context.CanTargetRuntime(inputListByName, item.runtime))
						{
							return null;
						}
						if (inputListByName.TypeConstraint.IsGenericParameter && !context.UpdateGenericParameter(inputListByName.TypeConstraint, item.type))
						{
							return null;
						}
					}
				}
				num += inputListByName.TypeConstraint.RankType();
			}
			foreach (KeyValuePair<string, InputType> fixedReference in context.Connections.fixedReferences)
			{
				ReferenceMetadata referenceByName = metadata.GetReferenceByName(fixedReference.Key);
				if (referenceByName != null)
				{
					if (!context.CanAccomodate(referenceByName.ReferenceType, fixedReference.Value.type))
					{
						return null;
					}
					num += referenceByName.ReferenceType.RankType();
				}
			}
			return num;
		}

		public static IOutput SkipImplicitCasts(this IOutput source)
		{
			while (true)
			{
				if (source == null)
				{
					return null;
				}
				if (source.OwnerNode is DataImportNode)
				{
					return source;
				}
				if (!(source.OwnerNode is ICast cast))
				{
					break;
				}
				if (!cast.IsImplicit)
				{
					return source;
				}
				IOutput inputSource = source.OwnerNode.GetInputSource(0);
				if (inputSource == null)
				{
					return source;
				}
				source = inputSource;
			}
			return source;
		}

		public static IOutput GetActualConnectedSource(this IOutput source)
		{
			while (true)
			{
				if (source == null)
				{
					return null;
				}
				if (source.OwnerNode is DataImportNode)
				{
					return source;
				}
				if (!source.OwnerNode.IsPassthrough)
				{
					break;
				}
				source = source.OwnerNode.GetInputSource(0);
			}
			return source;
		}

		public static Type GetActualConnectedType(this IOutput source)
		{
			return source.GetActualConnectedSource()?.OutputType;
		}
	}
	public class OverloadSearchContext
	{
		private static Type[] emptyTypes = new Type[0];

		private Dictionary<Type, Type> genericParameters;

		public NodeConnections Connections { get; private set; }

		public NodeRuntime Runtime { get; private set; }

		public bool AllowMergingGroups { get; private set; }

		public OverloadSearchContext(NodeConnections connections, NodeRuntime runtime, bool allowMergingGroups)
		{
			Connections = connections;
			Runtime = runtime;
			AllowMergingGroups = allowMergingGroups;
		}

		public Type GetCompatibleNodeType(Type type)
		{
			return Runtime.GetCompatibleNodeType(type);
		}

		public void BeginNewOverload()
		{
			genericParameters?.Clear();
		}

		public bool CanTargetRuntime(InputMetadataBase input, NodeRuntime targetRuntime)
		{
			if (NodeRuntime.CanTargetRuntime(Runtime, input.CrossRuntime, targetRuntime))
			{
				return true;
			}
			if (AllowMergingGroups && NodeRuntime.AreSameRuntimeTypes(Runtime, targetRuntime))
			{
				return true;
			}
			return false;
		}

		public bool CanAccomodate(Type source, Type target)
		{
			if (source == target)
			{
				return true;
			}
			if (source.IsGenericParameter)
			{
				GenericParameterAttributes genericParameterAttributes = source.GenericParameterAttributes;
				if (genericParameterAttributes.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint) && !target.IsValueType && target.GetConstructor(emptyTypes) == null)
				{
					return false;
				}
				if (genericParameterAttributes.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint) && target.IsValueType)
				{
					return false;
				}
				if (genericParameterAttributes.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint) && !target.IsValueType)
				{
					return false;
				}
				Type[] genericParameterConstraints = source.GetGenericParameterConstraints();
				for (int i = 0; i < genericParameterConstraints.Length; i++)
				{
					if (!genericParameterConstraints[i].IsAssignableFrom(target))
					{
						return false;
					}
				}
				if (!UpdateGenericParameter(source, target))
				{
					return false;
				}
				return true;
			}
			if (source.ContainsGenericParameters)
			{
				if (source.IsInterface)
				{
					Type genericTypeDefinition = source.GetGenericTypeDefinition();
					if (target.IsInterface && target.GetGenericTypeDefinition() == genericTypeDefinition)
					{
						return TryMatchGenericArguments(source, target);
					}
					Type[] genericParameterConstraints = target.GetInterfaces();
					foreach (Type type in genericParameterConstraints)
					{
						if (type.IsGenericType && type.GetGenericTypeDefinition() == genericTypeDefinition)
						{
							return TryMatchGenericArguments(source, type);
						}
					}
				}
				else if (source.IsClass)
				{
					Type genericTypeDefinition2 = source.GetGenericTypeDefinition();
					Type type2 = target;
					while (!type2.IsGenericType || type2.GetGenericTypeDefinition() != genericTypeDefinition2)
					{
						if (type2.BaseType == null)
						{
							return false;
						}
						type2 = type2.BaseType;
					}
					return TryMatchGenericArguments(source, type2);
				}
			}
			PrimitiveClass primitiveClass = source.ClassifyPrimitive();
			if (primitiveClass != PrimitiveClass.NONE)
			{
				if (!primitiveClass.CanAccomodate(target.ClassifyPrimitive()))
				{
					return false;
				}
				int primitiveRanking = source.GetPrimitiveRanking();
				int primitiveRanking2 = target.GetPrimitiveRanking();
				return primitiveRanking > primitiveRanking2;
			}
			return false;
		}

		private bool TryMatchGenericArguments(Type source, Type target)
		{
			Type[] genericArguments = source.GetGenericArguments();
			Type[] genericArguments2 = target.GetGenericArguments();
			for (int i = 0; i < genericArguments.Length; i++)
			{
				Type type = genericArguments[i];
				Type type2 = genericArguments2[i];
				if (!(type == type2) && !CanAccomodate(type, type2))
				{
					return false;
				}
			}
			return true;
		}

		public bool UpdateGenericParameter(Type generic, Type newType)
		{
			if (genericParameters == null)
			{
				genericParameters = new Dictionary<Type, Type>();
			}
			if (genericParameters.TryGetValue(generic, out var value))
			{
				if (value != newType && !CanAccomodate(value, newType))
				{
					if (CanAccomodate(newType, value))
					{
						genericParameters[generic] = newType;
					}
					else
					{
						Type type = TypeHelper.CombineTypes(newType, value);
						if (!(type != null))
						{
							genericParameters = null;
							return false;
						}
						genericParameters[generic] = type;
					}
				}
			}
			else
			{
				genericParameters.Add(generic, newType);
			}
			return true;
		}

		public Type InstanceType(Type type, Type currentType)
		{
			if (genericParameters == null || genericParameters.Count == 0)
			{
				return type;
			}
			Type[] genericArguments = type.GetGenericArguments();
			Type[] array = new Type[genericArguments.Length];
			Type[] array2 = null;
			if (currentType.IsGenericType)
			{
				array2 = currentType.GetGenericArguments();
				if (array2.Length != genericArguments.Length)
				{
					array2 = null;
				}
			}
			for (int i = 0; i < genericArguments.Length; i++)
			{
				if (!genericArguments[i].IsGenericParameter)
				{
					array[i] = genericArguments[i];
					continue;
				}
				if (genericParameters.TryGetValue(genericArguments[i], out var value))
				{
					if (array2 != null)
					{
						Type type2 = array2[i];
						if (type2.IsInterface)
						{
							Type[] interfaces = value.GetInterfaces();
							foreach (Type type3 in interfaces)
							{
								if (type3.Name == type2.Name)
								{
									value = type3;
									break;
								}
							}
						}
					}
					array[i] = value;
					continue;
				}
				array = null;
				break;
			}
			if (array == null)
			{
				return null;
			}
			if (!type.IsGenericTypeDefinition)
			{
				type = type.GetGenericTypeDefinition();
			}
			Type type4 = type.MakeGenericType(array);
			PropertyInfo property = type4.GetProperty("IsValidGenericType", BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
			if (property != null)
			{
				object value2 = property.GetValue(null);
				if (value2 is bool && !(bool)value2)
				{
					return null;
				}
			}
			return type4;
		}
	}
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public class PassthroughNodeAttribute : Attribute
	{
	}
	public enum PrimitiveClass
	{
		NONE,
		Boolean,
		Char,
		String,
		SignedInteger,
		UnsignedInteger,
		FloatingPoint
	}
	public static class TypeHelper
	{
		public static int RankType(this Type type)
		{
			if (type.ClassifyPrimitive() != PrimitiveClass.NONE)
			{
				return type.GetPrimitiveRanking();
			}
			if (type.IsGenericParameter)
			{
				int num = 500;
				GenericParameterAttributes genericParameterAttributes = type.GenericParameterAttributes;
				if (genericParameterAttributes.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint))
				{
					num -= 100;
				}
				if (genericParameterAttributes.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint))
				{
					num += 100;
				}
				if (genericParameterAttributes.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint))
				{
					num -= 50;
				}
				return num - type.GetGenericParameterConstraints().Length;
			}
			if (type.ContainsGenericParameters)
			{
				int num2 = 0;
				Type[] genericArguments = type.GetGenericArguments();
				foreach (Type type2 in genericArguments)
				{
					num2 += type2.RankType();
				}
				return num2;
			}
			return 1000000;
		}

		public static int GetPrimitiveRanking(this Type type)
		{
			if (type == typeof(bool) || type == typeof(char) || type == typeof(string))
			{
				return 1;
			}
			if (type == typeof(byte) || type == typeof(sbyte))
			{
				return 2;
			}
			if (type == typeof(ushort) || type == typeof(short))
			{
				return 3;
			}
			if (type == typeof(uint) || type == typeof(int))
			{
				return 4;
			}
			if (type == typeof(ulong) || type == typeof(long))
			{
				return 5;
			}
			if (type == typeof(float))
			{
				return 6;
			}
			if (type == typeof(double))
			{
				return 7;
			}
			if (type == typeof(decimal))
			{
				return 8;
			}
			throw new ArgumentException("Invalid primitive type: " + type);
		}

		public static bool CanImplicitlyConvertTo(Type from, Type to)
		{
			if (to == typeof(object))
			{
				return true;
			}
			PrimitiveClass primitiveClass = from.ClassifyPrimitive();
			PrimitiveClass primitiveClass2 = to.ClassifyPrimitive();
			if (primitiveClass == PrimitiveClass.NONE || primitiveClass2 == PrimitiveClass.NONE)
			{
				return to.IsAssignableFrom(from);
			}
			int primitiveClassRank = from.GetPrimitiveClassRank();
			int primitiveClassRank2 = to.GetPrimitiveClassRank();
			if (primitiveClass == primitiveClass2)
			{
				return primitiveClassRank2 >= primitiveClassRank;
			}
			if (primitiveClass2 == PrimitiveClass.String && primitiveClass == PrimitiveClass.Char)
			{
				return true;
			}
			if (!primitiveClass2.CanAccomodate(primitiveClass))
			{
				return false;
			}
			if (primitiveClass2 == PrimitiveClass.SignedInteger && primitiveClass == PrimitiveClass.UnsignedInteger)
			{
				return primitiveClassRank2 > primitiveClassRank;
			}
			return true;
		}

		public static bool CanExplicitlyConvertTo(Type from, Type to)
		{
			if ((from.IsInterface || from.IsClass) && (to.IsInterface || to.IsClass))
			{
				return true;
			}
			PrimitiveClass primitiveClass = from.ClassifyPrimitive();
			PrimitiveClass primitiveClass2 = to.ClassifyPrimitive();
			bool num = primitiveClass == PrimitiveClass.SignedInteger || primitiveClass == PrimitiveClass.UnsignedInteger || primitiveClass == PrimitiveClass.FloatingPoint;
			bool flag = primitiveClass2 == PrimitiveClass.SignedInteger || primitiveClass2 == PrimitiveClass.UnsignedInteger || primitiveClass2 == PrimitiveClass.FloatingPoint;
			if (num && flag)
			{
				return true;
			}
			return false;
		}

		public static Type CombineTypes(Type a, Type b)
		{
			if (a == b)
			{
				return a;
			}
			PrimitiveClass primitiveClass = a.ClassifyPrimitive();
			PrimitiveClass primitiveClass2 = b.ClassifyPrimitive();
			if (primitiveClass == PrimitiveClass.NONE || primitiveClass2 == PrimitiveClass.NONE)
			{
				return null;
			}
			int primitiveClassRank = a.GetPrimitiveClassRank();
			int primitiveClassRank2 = b.GetPrimitiveClassRank();
			if (primitiveClassRank < 0 || primitiveClassRank2 < 0)
			{
				return null;
			}
			if (primitiveClass == primitiveClass2)
			{
				if (primitiveClassRank > primitiveClassRank2)
				{
					return a;
				}
				return b;
			}
			if (primitiveClass.CanAccomodate(primitiveClass2))
			{
				return AccomodatePrimitive(primitiveClass, b);
			}
			if (primitiveClass2.CanAccomodate(primitiveClass))
			{
				return AccomodatePrimitive(primitiveClass2, a);
			}
			return null;
		}

		public static Type AccomodatePrimitive(PrimitiveClass primitiveClass, Type target)
		{
			int primitiveClassRank = target.GetPrimitiveClassRank();
			Type primitive = primitiveClass.GetPrimitive(primitiveClassRank + 1);
			if (primitive != null)
			{
				return primitive;
			}
			if (primitiveClass == PrimitiveClass.SignedInteger || primitiveClass == PrimitiveClass.UnsignedInteger)
			{
				return typeof(float);
			}
			return null;
		}

		public static int GetPrimitiveClassRank(this Type type)
		{
			if (type == typeof(sbyte) || type == typeof(byte) || type == typeof(float))
			{
				return 0;
			}
			if (type == typeof(ushort) || type == typeof(short) || type == typeof(double))
			{
				return 1;
			}
			if (type == typeof(uint) || type == typeof(int))
			{
				return 2;
			}
			if (type == typeof(ulong) || type == typeof(long))
			{
				return 3;
			}
			return -1;
		}

		public static int GetPrimitiveClassMaxRank(this PrimitiveClass primitiveClass)
		{
			return primitiveClass switch
			{
				PrimitiveClass.Boolean => 0, 
				PrimitiveClass.SignedInteger => 3, 
				PrimitiveClass.UnsignedInteger => 3, 
				PrimitiveClass.FloatingPoint => 1, 
				PrimitiveClass.String => 0, 
				PrimitiveClass.Char => 0, 
				_ => -1, 
			};
		}

		public static Type GetPrimitive(this PrimitiveClass primitiveClass, int rank)
		{
			switch (primitiveClass)
			{
			case PrimitiveClass.SignedInteger:
				switch (rank)
				{
				case 0:
					return typeof(sbyte);
				case 1:
					return typeof(short);
				case 2:
					return typeof(int);
				case 3:
					return typeof(long);
				}
				break;
			case PrimitiveClass.UnsignedInteger:
				switch (rank)
				{
				case 0:
					return typeof(byte);
				case 1:
					return typeof(ushort);
				case 2:
					return typeof(uint);
				case 3:
					return typeof(ulong);
				}
				break;
			case PrimitiveClass.FloatingPoint:
				switch (rank)
				{
				case 0:
					return typeof(float);
				case 1:
					return typeof(double);
				}
				break;
			}
			return null;
		}

		public static bool CanAccomodate(this PrimitiveClass source, PrimitiveClass target)
		{
			if (source == target)
			{
				return true;
			}
			switch (source)
			{
			case PrimitiveClass.SignedInteger:
				return target == PrimitiveClass.UnsignedInteger;
			case PrimitiveClass.FloatingPoint:
				if (target != PrimitiveClass.SignedInteger)
				{
					return target == PrimitiveClass.UnsignedInteger;
				}
				return true;
			case PrimitiveClass.String:
				return target == PrimitiveClass.Char;
			default:
				return false;
			}
		}

		public static PrimitiveClass ClassifyPrimitive(this Type type)
		{
			if (type == typeof(bool))
			{
				return PrimitiveClass.Boolean;
			}
			if (type == typeof(char))
			{
				return PrimitiveClass.Char;
			}
			if (type == typeof(string))
			{
				return PrimitiveClass.String;
			}
			if (type == typeof(byte) || type == typeof(ushort) || type == typeof(uint) || type == typeof(ulong))
			{
				return PrimitiveClass.UnsignedInteger;
			}
			if (type == typeof(sbyte) || type == typeof(short) || type == typeof(int) || type == typeof(long))
			{
				return PrimitiveClass.SignedInteger;
			}
			if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
			{
				return PrimitiveClass.FloatingPoint;
			}
			return PrimitiveClass.NONE;
		}
	}
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Field)]
	public class PossibleContinuationsAttribute : Attribute
	{
		private HashSet<string> continuations = new HashSet<string>();

		public IEnumerable<string> Continuations => continuations;

		public bool CanContinueTo(string str)
		{
			return continuations.Contains(str);
		}

		public PossibleContinuationsAttribute(params string[] continuations)
		{
			foreach (string item in continuations)
			{
				this.continuations.Add(item);
			}
		}
	}
	public interface ITaskScheduler
	{
		void Post(Operation action);
	}
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public class NodeCategoryAttribute : Attribute
	{
		public string Name { get; private set; }

		public NodeCategoryAttribute(string name)
		{
			Name = name;
		}
	}
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public class NodeNameAttribute : Attribute
	{
		public string Name { get; private set; }

		public bool SimpleView { get; private set; }

		public NodeNameAttribute(string name, bool simpleView = false)
		{
			Name = name;
			SimpleView = simpleView;
		}
	}
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
	public class OldElementNameAttribute : Attribute
	{
		public string OldName { get; private set; }

		public OldElementNameAttribute(string oldName)
		{
			OldName = oldName;
		}
	}
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public class OldNodeNameAttribute : Attribute
	{
		public string OldName { get; private set; }

		public OldNodeNameAttribute(string oldName)
		{
			OldName = oldName;
		}
	}
	public class OrderedSet<T> : IEnumerable<T>, IEnumerable
	{
		private List<T> list = new List<T>();

		private HashSet<T> set = new HashSet<T>();

		public int Count => list.Count;

		public bool Add(T item)
		{
			if (set.Add(item))
			{
				list.Add(item);
				return true;
			}
			return false;
		}

		public void Clear()
		{
			list.Clear();
			set.Clear();
		}

		public List<T>.Enumerator GetEnumerator()
		{
			return list.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		IEnumerator<T> IEnumerable<T>.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
	public static class ReflectionHelper
	{
		private static ConcurrentDictionary<Type, bool> cachedTypes = new ConcurrentDictionary<Type, bool>();

		public static bool IsUnmanaged(this Type type)
		{
			if (cachedTypes.TryGetValue(type, out var value))
			{
				return value;
			}
			bool flag = type.IsPrimitive || type.IsPointer || type.IsEnum || (!type.IsGenericType && type.IsValueType && type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).All((FieldInfo x) => x.FieldType.IsUnmanaged()));
			cachedTypes.TryAdd(type, flag);
			return flag;
		}

		public static bool IsNullable(this Type type)
		{
			if (!type.IsValueType)
			{
				return false;
			}
			if (!type.IsGenericType)
			{
				return false;
			}
			return type.GetGenericTypeDefinition() == typeof(Nullable<>);
		}

		public static string GetNiceTypeName(this Type type, string open = "<", string close = ">")
		{
			if (type.GetGenericArguments().Length == 0)
			{
				return type.Name;
			}
			Type[] genericArguments = type.GetGenericArguments();
			string text = type.Name;
			if (text.Contains("?"))
			{
				return text;
			}
			if (type.IsNested)
			{
				text = type.DeclaringType.Name + "+" + text;
			}
			int num = text.IndexOf("`");
			if (num >= 0)
			{
				text = text.Substring(0, num);
			}
			return text + open + string.Join(",", genericArguments.Select((Type t) => t.GetNiceTypeName(open, close)).ToArray()) + close;
		}

		public static MethodInfo FindMethodInHierarchy(this Type type, string name, BindingFlags flags)
		{
			MethodInfo method = type.GetMethod(name, flags);
			if (method != null)
			{
				return method;
			}
			if (type.BaseType != null)
			{
				return type.BaseType.FindMethodInHierarchy(name, flags);
			}
			return null;
		}

		public static IEnumerable<FieldInfo> EnumerateAllInstanceFields(this Type type, Predicate<Type> filter = null)
		{
			return type.EnumerateAllInstanceFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, filter);
		}

		public static IEnumerable<FieldInfo> EnumerateAllInstanceFields(this Type type, BindingFlags bindingFlags, Predicate<Type> filter = null)
		{
			if (type == typeof(object) || (filter != null && !filter(type)))
			{
				yield break;
			}
			foreach (FieldInfo item in type.BaseType.EnumerateAllInstanceFields(bindingFlags, filter))
			{
				yield return item;
			}
			FieldInfo[] fields = type.GetFields(bindingFlags | BindingFlags.DeclaredOnly);
			for (int i = 0; i < fields.Length; i++)
			{
				yield return fields[i];
			}
		}

		public static IEnumerable<PropertyInfo> EnumerateAllProperties(this Type type, BindingFlags bindingFlags, Predicate<Type> filter = null)
		{
			if (type == typeof(object) || (filter != null && !filter(type)))
			{
				yield break;
			}
			foreach (PropertyInfo item in type.BaseType.EnumerateAllProperties(bindingFlags, filter))
			{
				yield return item;
			}
			PropertyInfo[] properties = type.GetProperties(bindingFlags | BindingFlags.DeclaredOnly);
			for (int i = 0; i < properties.Length; i++)
			{
				yield return properties[i];
			}
		}
	}
	public class ValueCastAttribute : Attribute
	{
		public Type From { get; private set; }

		public Type To { get; private set; }

		public ValueCastAttribute(Type from, Type to)
		{
			From = from;
			To = to;
		}
	}
}
internal class ProtoFluxCore_ProcessedByFody
{
	internal const string FodyVersion = "6.7.0.0";

	internal const string NodeWeaver = "1.0.0.0";
}
