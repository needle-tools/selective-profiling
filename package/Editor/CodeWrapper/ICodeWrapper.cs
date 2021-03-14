using System.Collections.Generic;
using HarmonyLib;

namespace Needle.SelectiveProfiling.CodeWrapper
{
	public interface ICodeWrapper
	{
		void Apply(IList<CodeInstruction> instructions, IList<CodeInstruction> before, IList<CodeInstruction> after);
	}
}


