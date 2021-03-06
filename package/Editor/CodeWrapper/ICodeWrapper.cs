using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Needle.SelectiveProfiling.CodeWrapper
{
	public delegate (IList<CodeInstruction> before, IList<CodeInstruction> after) InjectionCallback(MethodBase method, CodeInstruction instruction, int index, ILGenerator il);
	
	public interface ICodeWrapper
	{
		void Apply(MethodBase method, IList<CodeInstruction> instructions, ILGenerator il);
	}
}


