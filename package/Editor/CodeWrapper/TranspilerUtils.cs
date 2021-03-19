using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

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
		public static string TryGetMethodName(OpCode code, object operand, bool fullName)
		{
			if (!fullName)
			{
				// object creation
				if(code == OpCodes.Newobj && operand is ConstructorInfo ctor && ctor.DeclaringType != null)
				{
					var dt = ctor.DeclaringType;
					return "new " + GetNiceTypeName(dt);
				}
				
				if (code == OpCodes.Newarr)
				{
					if (operand is Type t)
					{
						return "new " + GetNiceTypeName(t) + "[]";
					}
				}
				
				// method calls
				if (operand is MethodInfo m)
				{
					var c = GetNiceTypeName(m.DeclaringType);
					return c != null ? c + "." + m.Name : m.Name;
				}
			}
			
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


		private static string GetNiceTypeName(Type type, bool isGenericArgument = false)
		{
			if (type == null) return string.Empty;
			
			var name = type.Name.ToCSharpTypeName();
			var sub = name.IndexOf("`", StringComparison.InvariantCultureIgnoreCase);
			if (sub > 0) name = name.Substring(0, sub);
			
			if (type.IsGenericType)
			{
				name += GetNiceGenericArguments(type);
			}

			if (!isGenericArgument && type.DeclaringType != null)
			{
				name = GetNiceTypeName(type.DeclaringType) + "." + name;
			}

			return name;
		}
		
		private static string GetNiceGenericArguments(Type t)
		{
			if (t == null) return string.Empty;
			if (t.IsGenericType)
				return "<" + string.Join(", ", t.GetGenericArguments().Select(arg => GetNiceTypeName(arg, true))) + ">";
			return string.Empty;
		}
		
		// -> https://stackoverflow.com/a/56352803
		private static string ToCSharpTypeName(this string dotNetTypeName, bool isNull = false)
		{
			var nullable = isNull ? "?" : "";
			const string prefix = "System.";
			var typeName = dotNetTypeName.StartsWith(prefix) ? dotNetTypeName.Remove(0, prefix.Length) : dotNetTypeName;

			string csTypeName;
			switch (typeName)
			{
				case "Boolean": csTypeName = "bool"; break;
				case "Byte":    csTypeName = "byte"; break;
				case "SByte":   csTypeName = "sbyte"; break;
				case "Char":    csTypeName = "char"; break;
				case "Decimal": csTypeName = "decimal"; break;
				case "Double":  csTypeName = "double"; break;
				case "Single":  csTypeName = "float"; break;
				case "Int32":   csTypeName = "int"; break;
				case "UInt32":  csTypeName = "uint"; break;
				case "Int64":   csTypeName = "long"; break;
				case "UInt64":  csTypeName = "ulong"; break;
				case "Object":  csTypeName = "object"; break;
				case "Int16":   csTypeName = "short"; break;
				case "UInt16":  csTypeName = "ushort"; break;
				case "String":  csTypeName = "string"; break;
				default: csTypeName = typeName; break; // do nothing
			}
			return $"{csTypeName}{nullable}";

		}
	}
	
	
}