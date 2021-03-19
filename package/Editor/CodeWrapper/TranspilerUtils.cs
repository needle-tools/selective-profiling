﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEditor.Profiling;
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
					var str = "new " + GetNiceTypeName(dt);
					var parameters = ctor.GetParameters();
					str += GetNiceParameters(parameters);
					return str;
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
					return GetNiceMethodName(m, true);
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


		private static string GetNiceTypeName(Type type, bool isGenericArgument = false, bool skipRootDeclaringType = true)
		{
			if (type == null) return string.Empty;
			
			var name = type.Name.ToCSharpTypeName();
			if (type.IsSpecialName)
			{
				name = GetNicePropertyName(name);
			}
			
			var sub = name.IndexOf("`", StringComparison.InvariantCultureIgnoreCase);
			if (sub > 0) name = name.Substring(0, sub);
			
			if (type.IsGenericType)
			{
				name += GetNiceGenericArguments(type);
			}

			if (!isGenericArgument && type.DeclaringType != null)
			{
				var declaring = type.DeclaringType;
				if (!skipRootDeclaringType || declaring.DeclaringType != null)
					name = GetNiceTypeName(declaring) + "." + name;
			}

			return name;
		}
		
		private static string GetNiceGenericArguments(Type t)
		{
			if (t == null) return string.Empty;
			if (t.IsGenericType)
				return GetNiceGenericArguments(t.GetGenericArguments());
			return string.Empty;
		}
		
		private static string GetNiceGenericArguments(params Type[] genericArguments)
		{
			return "<" + string.Join(", ", genericArguments.Select(arg => GetNiceTypeName(arg, true))) + ">";
		}

		private static string GetNiceMethodName(MethodBase method, bool skipRootDeclaringType)
		{
			string _class = null;
			if (method.DeclaringType != null)
			{
				var declaring = method.DeclaringType;
				if (!skipRootDeclaringType || declaring.DeclaringType != null)
				{
					_class = GetNiceTypeName(declaring, skipRootDeclaringType);
				}
			}
			
			var _method = method.Name;
			if (method.IsSpecialName) _method = GetNicePropertyName(_method);

			if (method.IsGenericMethod) 
				_method += GetNiceGenericArguments(method.GetGenericArguments());

			var parameters = method.GetParameters();
			_method += GetNiceParameters(parameters);
			
			return !string.IsNullOrEmpty(_class) 
				? _class + "." + _method 
				: _method;
		}

		private static string GetNiceParameters(params ParameterInfo[] parameters)
		{
			var p = string.Join(", ", parameters.Select(parameterInfo => GetNiceTypeName(parameterInfo.ParameterType) + " " + parameterInfo.Name));
			return "(" + p + ")";
		}

		private static string GetNicePropertyName(string propertyName)
		{
			const string getterPrefix = "get_";
			const string setterPrefix = "set_";
			if (propertyName.StartsWith(getterPrefix))
				propertyName = propertyName.Substring(getterPrefix.Length) + " get";
			else  if (propertyName.StartsWith(setterPrefix))
				propertyName = propertyName.Substring(setterPrefix.Length) + " set";
			return propertyName;
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