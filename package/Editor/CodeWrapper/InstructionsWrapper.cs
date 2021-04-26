using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace Needle.SelectiveProfiling.CodeWrapper
{
	internal class InstructionsWrapper : ICodeWrapper
	{
		public int Start;
		public int MethodIndex;
		public IList<CodeInstruction> Before, After;
		
		public void Apply(MethodBase method, IList<CodeInstruction> instructions, ILGenerator il)
		{
			int offset = 0;
			CodeInstruction branchTarget = null;
			var current = instructions[MethodIndex];
			
			if (After != null)
			{
				
				var index = MethodIndex + 1;
				for (var i = 0; i < After.Count; i++)
				{
					var inst = new CodeInstruction(After[i]);
					instructions.Insert(index, inst);
					index += 1;
				}

				offset += After.Count;
			}

			if (Before != null)
			{
				var index = Start;
				for (var i = Before.Count - 1; i >= 0; i--)
				{
					var inst = new CodeInstruction(Before[i]);
					// capture the target to move branching labels to, effectively this is the first instruction we inject
					// note: we loop backwards here
					branchTarget = inst;
					instructions.Insert(index, inst);
					index = Mathf.Max(0, index);
				}
				offset += Before.Count;
			}
			MethodIndex += offset;

			if (branchTarget != null)
			{
				current.MoveLabelsTo(branchTarget);
			}
		}
	}
}