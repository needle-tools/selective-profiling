using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace Needle.SelectiveProfiling.CodeWrapper
{
	public interface ICodeWrapper
	{
		void Apply(IList<CodeInstruction> instructions, IList<CodeInstruction> before, IList<CodeInstruction> after);
	}

	public class InstructionsWrapper : ICodeWrapper
	{
		public int Start;
		public int MethodIndex;
		
		public void Apply(IList<CodeInstruction> instructions, IList<CodeInstruction> before, IList<CodeInstruction> after)
		{
			int offset = 0;
			if (after != null)
			{
				var index = MethodIndex + 1;
				for (var i = 0; i < after.Count; i++)
				{
					var inst = after[i];
					instructions.Insert(index, inst);
					index += 1;
				}

				offset += after.Count;
			}

			if (before != null)
			{
				var index = Start;
				for (var i = before.Count - 1; i >= 0; i--)
				{
					var inst = before[i];
					instructions.Insert(index, inst);
					index = Mathf.Max(0, index);
				}
				offset += before.Count;
			}
			MethodIndex += offset;
		}
	}

	public class MethodWrapper : ICodeWrapper
	{
		private readonly InstructionsWrapper wrapper;
		private readonly Action<CodeInstruction, int> callback;
		
		public MethodWrapper(InstructionsWrapper wrapper, Action<CodeInstruction, int> onInstruction)
		{
			this.wrapper = wrapper;
			this.callback = onInstruction;
		}
		
		public void Apply(IList<CodeInstruction> instructions, IList<CodeInstruction> before, IList<CodeInstruction> after)
		{

			var IL_Before = string.Join("\n", instructions);
			var start = -1;
			for (var index = 0; index < instructions.Count; index++)
			{
				var inst = instructions[index];
				
				// if a method call loads variables make sure to insert code before variable loading
				if (loadVarCodes.Contains(inst.opcode) || inst.opcode == OpCodes.Ldstr) 
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

			Debug.Log(IL_Before + "\n\n----\n\n" + string.Join("\n", instructions) + "\n\n");
		}
		

		private static readonly HashSet<OpCode> loadVarCodes = new HashSet<OpCode>()
		{
			OpCodes.Ldloc_0,
			OpCodes.Ldloc_1,
			OpCodes.Ldloc_2,
			OpCodes.Ldloc_3,
			OpCodes.Ldloc,
			OpCodes.Ldloca,
			OpCodes.Ldloc_S,
			OpCodes.Ldloca_S
		};
	}
}


