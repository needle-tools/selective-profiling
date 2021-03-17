﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Profiling;

namespace Needle.SelectiveProfiling.CodeWrapper
{
	public class MethodWrapper : ICodeWrapper
	{
		private readonly InstructionsWrapper wrapper;
		private readonly Action<CodeInstruction, int> callback;
		private readonly bool debugLog = false;
		private readonly bool skipProfilerMethods = true;

		[CanBeNull] public MethodBase CurrentMethod;

		public MethodWrapper(InstructionsWrapper wrapper, Action<CodeInstruction, int> onInstruction, bool debugLog, bool skipProfilerMethods)
		{
			this.wrapper = wrapper;
			this.callback = onInstruction;
			this.debugLog = debugLog;
			this.skipProfilerMethods = skipProfilerMethods;
		}

		public void Apply(IList<CodeInstruction> instructions, IList<CodeInstruction> before, IList<CodeInstruction> after)
		{
			var IL_Before = string.Join("\n", instructions);
			
			// if (CurrentMethod != null)
			// {
			// 	var name = CurrentMethod.Name.ToLower();
			// 	if (name.Contains("equality") || name.Contains("normal") || name.Contains("inverse") || name.Contains("dir") || name.Contains("physics"))
			// 	{
			// 		// Debug.Log("--- -SKIP " + CurrentMethod + "\n" + IL_Before);
			// 		// return;
			// 	}
			// }
			
			var start = -1;
			for (var index = 0; index < instructions.Count; index++)
			{
				var inst = instructions[index];

				// if a method call loads variables make sure to insert code before variable loading
				if (TranspilerUtils.LoadVarCodes.Contains(inst.opcode) || inst.opcode == OpCodes.Ldstr || inst.opcode == OpCodes.Ldobj || inst.IsLdarg() || inst.IsLdarga())
				{
					if(start < 0)
						start = index;
				}
				
				if (inst.Branches(out _) || inst.labels != null && inst.labels.Count > 0)
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
				else if (inst.opcode == OpCodes.Call || inst.opcode == OpCodes.Callvirt)
				{
					if (inst.operand is MethodInfo mi)
					{
						SelectiveProfiler.RegisterInternalCalledMethod(mi);
						if (skipProfilerMethods && mi.DeclaringType == typeof(Profiler))
						{
							start = -1;
							continue;
						}
					}

					// we arrived at the actual method call
					wrapper.Start = start == -1 ? index : start;
					wrapper.MethodIndex = index;
					callback?.Invoke(inst, index);
					wrapper.Apply(instructions, before, after);
					index = wrapper.MethodIndex;
					start = -1;
				}
			}

			if (debugLog)
			{
				var prefix = CurrentMethod != null ? "<b>Transpiled</b> " + CurrentMethod.DeclaringType?.Name + "." + CurrentMethod.Name + "\n" : string.Empty;
				Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, prefix + IL_Before + "\n\n----\n\n" + string.Join("\n", instructions) + "\n\n");
			}
		}
	}
}
