using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Needle.SelectiveProfiling.CodeWrapper
{
	internal static class TranspilerUtils
	{
		public static readonly HashSet<OpCode> LoadVarCodes = new HashSet<OpCode>()
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
		
		private static MethodInfo monoMethodFullName;
		public static string TryGetMethodName(object operand)
		{
			if (operand.GetType().Name == "MonoMethod")
			{
				if (monoMethodFullName == null)
				{
					void FindFullName(Type type)
					{
						while (type != null)
						{
							monoMethodFullName = type.GetProperty("FullName", (BindingFlags) ~0)?.GetGetMethod(true);
							if (monoMethodFullName != null) break;
							type = type.BaseType;
						}
					}
					FindFullName(operand.GetType());
				}

				if (monoMethodFullName != null)
				{
					return monoMethodFullName.Invoke(operand, null) as string ?? operand.ToString();
				}
			}

			return operand.ToString();
		}

		// public static string GetHighlightedIL(this IEnumerable<CodeInstruction> il)
		// {
		// 	
		// }
	}
}