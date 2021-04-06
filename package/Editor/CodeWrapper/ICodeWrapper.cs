using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace Needle.SelectiveProfiling.CodeWrapper
{
	public delegate void InjectionCallback(MethodBase method, CodeInstruction instruction, int index);
	
	public interface ICodeWrapper
	{
		void Apply(MethodBase method, IList<CodeInstruction> instructions, IList<CodeInstruction> before, IList<CodeInstruction> after);
	}
}


