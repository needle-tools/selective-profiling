using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Needle.SelectiveProfiling.CodeWrapper
{
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
}