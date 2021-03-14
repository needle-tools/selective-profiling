using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using needle.EditorPatching;
using UnityEngine;

namespace Needle.SelectiveProfiling.CodeWrapper
{
	public class MethodWrapper : ICodeWrapper
	{
		private readonly InstructionsWrapper wrapper;
		private readonly Action<CodeInstruction, int> callback;

		public bool DebugLog = false;
		
		public MethodWrapper(InstructionsWrapper wrapper, Action<CodeInstruction, int> onInstruction, bool debugLog)
		{
			this.wrapper = wrapper;
			this.callback = onInstruction;
			this.DebugLog = debugLog;
		}
		
		public void Apply(IList<CodeInstruction> instructions, IList<CodeInstruction> before, IList<CodeInstruction> after)
		{

			var IL_Before = string.Join("\n", instructions);
			var start = -1;
			for (var index = 0; index < instructions.Count; index++)
			{
				var inst = instructions[index];
				
				// if a method call loads variables make sure to insert code before variable loading
				if (TranspilerUtils.LoadVarCodes.Contains(inst.opcode) || inst.opcode == OpCodes.Ldstr) 
				{
					start = index; 
				}
				// make sure to insert sample after branching labels
				if (inst.Branches(out _) || inst.labels != null && inst.labels.Count > 0)
				{
					start = index + 1;
				}
				else if (inst.opcode == OpCodes.Call || inst.opcode == OpCodes.Callvirt)
				{
					// we arrived at the actual method call
					wrapper.Start = start == -1 ? index : start;
					wrapper.MethodIndex = index;
					callback?.Invoke(inst, index);
					wrapper.Apply(instructions, before, after);
					index = wrapper.MethodIndex;
					start = -1;
				}
			}

			if(DebugLog)
				Debug.Log(IL_Before + "\n\n----\n\n" + string.Join("\n", instructions) + "\n\n");
		}
	}
}