using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace Needle.SelectiveProfiling.CodeWrapper
{
	public class MethodWrapper : ICodeWrapper
	{
		private readonly InstructionsWrapper wrapper;
		private readonly InjectionCallback beforeInject;
		private readonly bool debugLog;
		private readonly bool skipProfilerMethods;
		
		public MethodWrapper(InstructionsWrapper wrapper, 
			InjectionCallback beforeInject, 
			bool debugLog, bool skipProfilerMethods)
		{
			this.wrapper = wrapper;
			this.beforeInject = beforeInject;
			this.debugLog = debugLog;
			this.skipProfilerMethods = skipProfilerMethods;
		}

		public void Apply(MethodBase method, IList<CodeInstruction> instructions, IList<CodeInstruction> before, IList<CodeInstruction> after)
		{
			var IL_Before = string.Join("\n", instructions);
			
			var start = -1;
			var exceptionBlockStack = 0;
			for (var index = 0; index < instructions.Count; index++)
			{
				var inst = instructions[index];
				var prevStart = start;

				
				// TODO: dont inject samples in try{} block because we get errors when exception happens and we dont have an end sample
				
				exceptionBlockStack += inst.blocks.Count(b => b.blockType == ExceptionBlockType.BeginExceptionBlock);
				exceptionBlockStack -= inst.blocks.Count(b => b.blockType == ExceptionBlockType.EndExceptionBlock);
				
				if(exceptionBlockStack < 0)
					Debug.LogError(
						$"Found more end exception blocks than begin exception blocks in {method.FullDescription()}, " +
						$"please report this as a bug at {Constants.GithubIssuesUrl} including this message\n\n{IL_Before}\n\n");
				// else if (isInTryBlock && inst.blocks.Any(b =>
				// 	b.blockType == ExceptionBlockType.BeginFinallyBlock || 
				// 	b.blockType == ExceptionBlockType.BeginCatchBlock))
				// 	isInTryBlock = false;
				
				
				// if a method call loads variables make sure to insert code before variable loading
				if (TranspilerUtils.LoadVarCodes.Contains(inst.opcode) || inst.opcode == OpCodes.Ldnull || inst.opcode == OpCodes.Ldstr || inst.opcode == OpCodes.Ldobj || inst.IsLdarg() || inst.IsLdarga())
				{
					if(start < 0)
						start = index;
				}

				var hasLabel = inst.Branches(out _) || inst.labels != null && inst.labels.Count > 0;
				if (hasLabel)
				{
					// make sure to insert sample after branching labels
					start = index + 1;
				}
				// make sure to insert sample after end exception block
				else if (inst.blocks.Any(b =>
					b.blockType == ExceptionBlockType.BeginExceptionBlock
					|| b.blockType == ExceptionBlockType.EndExceptionBlock
					|| b.blockType == ExceptionBlockType.BeginFinallyBlock
					))
				{
					start = index + 1;
				}

				var isMethodCall = inst.opcode == OpCodes.Call || inst.opcode == OpCodes.Callvirt;
				if (isMethodCall || inst.opcode == OpCodes.Newobj || inst.opcode == OpCodes.Newarr)
				{
					if (inst.operand is MethodInfo mi)
					{
						if (skipProfilerMethods && (mi.DeclaringType == typeof(Profiler) || mi.DeclaringType == typeof(ProfilerMarker)))
						{
							start = -1;
							continue;
						}
						if(mi.GetCustomAttribute<DontFollow>() == null)
							SelectiveProfiler.RegisterInternalCalledMethod(mi);
					}
					if (start > index && hasLabel) start = prevStart;

					if (isMethodCall && exceptionBlockStack > 0)
					{
						start = -1;
						continue;
					}

					// we arrived at the actual method call
					wrapper.Start = start == -1 ? index : start;
					wrapper.MethodIndex = index;
					beforeInject?.Invoke(method, inst, index);
					wrapper.Apply(method, instructions, before, after);
					index = wrapper.MethodIndex;
					start = -1;
				}
			}

			if (debugLog)
			{
				var prefix = method != null ? "<b>Transpiled</b> " + method.DeclaringType?.Name + "." + method.Name + "\n" : string.Empty;
				Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, prefix + IL_Before + "\n\n----\n\n" + string.Join("\n", instructions) + "\n\n");
			}
		}
	}
}