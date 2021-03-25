using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Needle.SelectiveProfiling.CodeWrapper
{
	internal class InstructionsWrapper : ICodeWrapper
	{
		public int Start;
		public int MethodIndex;
		
		public void Apply(MethodBase method, IList<CodeInstruction> instructions, IList<CodeInstruction> before, IList<CodeInstruction> after)
		{
			int offset = 0;
			CodeInstruction branchTarget = null;
			var current = instructions[MethodIndex];
			
			if (after != null)
			{
				
				var index = MethodIndex + 1;
				for (var i = 0; i < after.Count; i++)
				{
					var inst = new CodeInstruction(after[i]);
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
					var inst = new CodeInstruction(before[i]);
					// capture the target to move branching labels to, effectively this is the first instruction we inject
					// note: we loop backwards here
					branchTarget = inst;
					instructions.Insert(index, inst);
					index = Mathf.Max(0, index);
				}
				offset += before.Count;
			}
			MethodIndex += offset;

			if (branchTarget != null)
			{
				current.MoveLabelsTo(branchTarget);
			}
		}
	}
}