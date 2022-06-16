using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace Needle.SelectiveProfiling.CodeWrapper
{
	internal class MethodWrapper : ICodeWrapper
	{
		internal static Action<string, string> CapturedILBeforeAfter;

		private readonly MethodInfo sourceMethod;
		private readonly InstructionsWrapper wrapper;
		private readonly InjectionCallback beforeInject;
		private readonly bool debugLog;
		private readonly bool skipProfilerMethods;
		private readonly bool developmentMode;
		private int depth;

		public MethodWrapper(
			MethodInfo method,
			InstructionsWrapper wrapper,
			InjectionCallback beforeInject,
			bool debugLog,
			bool skipProfilerMethods,
			bool developmentMode,
			int depth)
		{
			this.sourceMethod = method;
			this.wrapper = wrapper;
			this.beforeInject = beforeInject;
			this.debugLog = debugLog;
			this.skipProfilerMethods = skipProfilerMethods;
			this.developmentMode = developmentMode;
			this.depth = depth;
		}

		public void Apply(MethodBase method, IList<CodeInstruction> instructions, ILGenerator il)
		{
			var IL_Before = ShouldSaveIL(debugLog) || developmentMode ? string.Join("\n", instructions) : null;

			var start = -1;
			var skipNextCall = false;
			// var exceptionBlockStack = 0;
			// var injectCounter = 0;
			// var currentBranches = new List<Label>();
			// var currentLoadedVars = new List<LoadedVariable>();

			for (var index = 0; index < instructions.Count; index++)
			{
				var inst = instructions[index];
				// var prevStart = start;
				//
				// // we dont inject samples in try{} block because we get errors when exception happens and we dont have an end sample
				// // possible solution: wrap profiler samples with "try finally" block so that "end sample" is called even if exception happens
				// exceptionBlockStack += inst.blocks.Count(b => b.blockType == ExceptionBlockType.BeginExceptionBlock);
				// exceptionBlockStack -= inst.blocks.Count(b => b.blockType == ExceptionBlockType.EndExceptionBlock);
				// if (exceptionBlockStack < 0) Debug.LogError(
				// 		$"Found more end exception blocks than begin exception blocks in {method.FullDescription()}, " +
				// 		$"please report this as a bug at {Constants.GithubIssuesUrl} including this message\n\n{IL_Before}\n\n");
				//
				// // if a method call loads variables make sure to insert code before variable loading
				// if (TranspilerUtils.LoadVarCodes.Contains(inst.opcode) || inst.opcode == OpCodes.Ldnull || inst.opcode == OpCodes.Ldstr ||
				//     inst.opcode == OpCodes.Ldobj || inst.IsLdarg() || inst.IsLdarga())
				// {
				// 	if (start < 0)
				// 		start = index;
				// }
				//
				// var hasBranch = inst.Branches(out var branch);
				// // if (branch != null)
				// // 	currentBranches.Add(branch.Value);
				//
				// var hasLabel = inst.labels != null && inst.labels.Count > 0;
				// // if (hasLabel)
				// // 	currentBranches.RemoveAll(b => inst.labels.Contains(b));
				//
				// if (hasLabel || hasBranch)
				// {
				// 	// make sure to insert sample after branching labels
				// 	start = index + 1;
				// }
				// // make sure to insert sample after end exception block
				// else if (inst.blocks.Any(b =>
				// 	b.blockType == ExceptionBlockType.BeginExceptionBlock
				// 	|| b.blockType == ExceptionBlockType.EndExceptionBlock
				// 	|| b.blockType == ExceptionBlockType.BeginFinallyBlock
				// ))
				// {
				// 	start = index + 1;
				// }
				
				bool IsProfilerMarkerOrSampler(Type type)
				{
					return typeof(ProfilerMarker).IsAssignableFrom(type) ||
					       typeof(CustomSampler).IsAssignableFrom(type);
				}
				
				bool IsProfilerMethod(MemberInfo mi)
				{
					return mi.DeclaringType == typeof(Profiler) || IsProfilerMarkerOrSampler(mi.DeclaringType);
				}

				var isMethodCall = inst.opcode == OpCodes.Call || inst.opcode == OpCodes.Callvirt;
				if (isMethodCall && skipNextCall)
				{
					skipNextCall = false;
					start = -1;
					continue;
				}

				if (start <= -1) 
				{
					if (inst.opcode == OpCodes.Constrained)
					{
						start = index;
						
						// when unity uses Unity.Profiling.ProfilerMarker+AutoScope in finally/dispose the virtual dispose call is constrained to ProfilerMarker.Dispose
						// this causes mismatching end samples and we dont want to profile profiler methods
						// look into IL of UnityEngine.UIElements.UIElementsUtility.DoDispatch
						if (inst.operand is MemberInfo mi && IsProfilerMethod(mi))
						{
							skipNextCall = true; 
						}
					}
				}
				
				var isAllocation = inst.opcode == OpCodes.Newobj || inst.opcode == OpCodes.Newarr;
				if (isAllocation || isMethodCall)
				{
					if (inst.operand is MethodInfo mi)
					{
						// I think we dont have to skip methods that take Profiler.Samplers as arguments as below when in dispose scope
						// we can probably profile constructors below once we handle dispose scopes correctly?!
						// but maybe it is a special case as well. According to SRP comment: "Currently there's an issue which results in mismatched markers."
						
						if (skipProfilerMethods && IsProfilerMethod(mi))
						{
							// start = -1;
							continue;
						}

						if (mi.GetCustomAttribute<DontFollow>() == null)
							SelectiveProfiler.RegisterInternalCalledMethod(sourceMethod, mi, depth);
					}

					if (inst.operand is ConstructorInfo ci)
					{
						// TODO: for now dont profile ``UnityEngine.Rendering.ProfilingScope`` until we find a better way to handle cases where using(disposable) is called with custom profiler markers/sample implementation like in ScriptableRenderer.InternalStartRendering
						if (ci.DeclaringType?.FullName == "UnityEngine.Rendering.ProfilingScope")
						{
							// start = -1;
							continue;
						}

						// skip if creating exception instance
						// if (typeof(Exception).IsAssignableFrom(ci.DeclaringType))
						// {
						// 	start = -1;
						// 	continue;
						// }
						
						// skip constructors that take profiler sampler as argument
						// this prevents cases where IDisposable implementations (as ProfilerScopes) cause mismatching samples
						if (skipProfilerMethods && ci.GetParameters().Any(p => IsProfilerMarkerOrSampler(p.ParameterType)))
						{
							// start = -1;
							continue;
						}
					}

					// if (inst.opcode == OpCodes.Newobj && inst.operand != null && inst.operand is Exception)
					// {
					// 	start = -1;
					// 	continue;
					// }

					// if (start > index && hasLabel) start = prevStart;

					// if (isMethodCall && exceptionBlockStack > 0)
					// {
					// 	start = -1; 
					// 	continue;
					// }

					// when a method has a label we assume that we dont have to (or must) include the preceding stack loads
					// if we move the label to the beginning of the loads we might cause wrong branching 
					// e.g. when a branch jumps over some load/store and we move the label to the beginning of those
					// if (hasLabel) start = -1;
					//
					// beforeInject?.Invoke(method, inst, index);
					// start = index;

					// void LookAheadPotentiallyWrappingStoreResultAndConstrained()
					// {
					// 	for(var k = 1; index+k <= instructions.Count; k++)
					// 	{
					// 		var i = index + k;
					// 		if (i >= instructions.Count || ShouldCapture(instructions[i]))
					// 		{
					// 			index = i - 1;
					// 			break;
					// 		}
					// 	}
					// }
					// LookAheadPotentiallyWrappingStoreResultAndConstrained();


					var data = beforeInject?.Invoke(method, inst, index, il);
					if (!data.HasValue) throw new Exception("SelectiveProfiler did not return instructions");
					
					// we arrived at the actual method call
					wrapper.Start = start <= -1 ? index : start;
					wrapper.MethodIndex = index;
					wrapper.Before = data.Value.before;
					wrapper.After = data.Value.after;
					wrapper.Apply(method, instructions, il);
					index = wrapper.MethodIndex;
					start = -1;  
				}
			}

			if (ShouldSaveIL(debugLog))
			{
				var prefix = debugLog && method != null ? "<b>Transpiled</b> " + method.FullDescription() + "\n" : string.Empty; 
				var IL_After = string.Join("\n", instructions);

				string SanitizeFormatMessage(string message)
				{
					return message.Replace("{", string.Empty).Replace("}", string.Empty);
				}
				if (debugLog && IL_Before?.Length + IL_After.Length > 12000)
				{
					var msg = "Before " + prefix + IL_Before;
					Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, SanitizeFormatMessage(msg));
					msg = "After " + prefix + IL_After;
					Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, SanitizeFormatMessage(msg));
				}
				else if(debugLog)
				{
					var msg = prefix + IL_Before + "\n\n----\n\n" + IL_After;
					Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, SanitizeFormatMessage(msg));
				} 

				try
				{
					CapturedILBeforeAfter?.Invoke(IL_Before, IL_After);
				}
				catch (Exception e)
				{
					Debug.LogException(e);
				}
			}
		}

		private static bool ShouldSaveIL(bool _debugLog)
		{
			return _debugLog || CapturedILBeforeAfter != null;
		}
	}


	// intended to be used to keep track of the stack variables
	// internal class StackVariable : IEquatable<CodeInstruction>
	// {
	// 	public int InstructionIndex;
	// 	public CodeInstruction Instruction;
	// 	public int StackIndex;
	//
	// 	// .*?\.(?<index>\d*)|(\w*) (?<index2>\d*)?
	//
	// 	public StackVariable(CodeInstruction inst, int index)
	// 	{
	// 		this.InstructionIndex = index;
	// 		this.Instruction = inst;
	// 		// inst.opcode
	// 	}
	//
	// 	public bool Equals(CodeInstruction other)
	// 	{
	// 		if (other.IsStarg() || other.IsStloc())
	// 		{
	// 		}
	//
	// 		return false;
	// 	}
	// }

	/*
	 *  IL CheatSheet:
	 
		Remove:
		Pop			-> Removes the value currently on top of the evaluation stack.
		
		
		
		Starg		-> Stores the value on top of the evaluation stack in the argument slot at a specified index.
		Stloc		-> Pops the current value from the top of the evaluation stack and stores it in a the local variable list at a specified index.
		Stobj		-> Copies a value of a specified type from the evaluation stack into a supplied memory address.
		Stsfld		-> Replaces the value of a static field with a value from the evaluation stack.
		Stind_Ref	-> Stores a object reference value at a supplied address.

		Ldc_I4		-> Pushes a supplied value of type int32 onto the evaluation stack as an int32.
		Ldc_I4_M1	-> Pushes the integer value of -1 onto the evaluation stack as an int32.
		Ldc_I8		-> Pushes a supplied value of type int64 onto the evaluation stack as an int64.	
		Ldc_R4		-> Pushes a supplied value of type float32 onto the evaluation stack as type F (float).

		Ldelem		-> Loads the element with type int8 at a specified array index onto the top of the evaluation stack as an int32.
		Ldftn		-> Pushes an unmanaged pointer (type native int) to the native code implementing a specific method onto the evaluation stack.
		Ldlen		-> Pushes the number of elements of a zero-based, one-dimensional array onto the evaluation stack.
		Ldloc		-> Loads the local variable at a specific index onto the evaluation stack.
		Ldnull		-> Pushes a null reference (type O) onto the evaluation stack.
		Ldsfld		-> Pushes the value of a static field onto the evaluation stack.
		Ldstr		-> Pushes a new object reference to a string literal stored in the metadata.
		Ldarg 		-> Loads an argument (referenced by a specified index value) onto the stack.
		Ldarga		-> Load an argument address onto the evaluation stack.
		Localloc	-> Allocates a certain number of bytes from the local dynamic memory pool and pushes the address (a transient pointer, type *) of the first allocated byte onto the evaluation stack.

		Ldind_I		-> Loads a value of type native int as a native int onto the evaluation stack indirectly.
		
		
		Throw		-> Throws the exception object currently on the evaluation stack.
		
		
		Call		-> Calls the method indicated by the passed method descriptor.
		Calli		-> Calls the method indicated on the evaluation stack (as a pointer to an entry point) with arguments described by a calling convention.
		Callvirt	-> Calls a late-bound method on an object, pushing the return value onto the evaluation stack.
		
		Nop			-> Fills space if opcodes are patched. No meaningful operation is performed although a processing cycle can be consumed.
		Break 		-> Signals the Common Language Infrastructure (CLI) to inform the debugger that a break point has been tripped.



		Switch		-> Implements a jump table.
		Beq			-> Transfers control to a target instruction if two values are equal.
		Bge 		-> Transfers control to a target instruction if the first value is greater than or equal to the second value.
		Bgt			-> Transfers control to a target instruction if the first value is greater than the second value.

	 */
}
