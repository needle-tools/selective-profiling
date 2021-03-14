using System;
using System.Reflection;

namespace Needle.SelectiveProfiling.CodeWrapper
{
	internal static class Utils
	{
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