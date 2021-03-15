#define DEBUG_ACCESS
#undef DEBUG_ACCESS

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

namespace Needle.SelectiveProfiling.Utils
{
	internal static class AccessUtils
	{
		private static readonly Dictionary<string, Assembly> assemblyMap = new Dictionary<string, Assembly>();

		public static string GetMethodIdentifier(this MethodInfo info)
		{
			if (info == null) return null;
			var declaring = info.DeclaringType;
			var assembly = declaring?.Assembly;
			return assembly?.GetName().Name + ".dll" + ", " + declaring?.Namespace + "::" + declaring?.Name + info.Name + "(TODO:Params)";
		}

		public static bool TryGetMethodFromName(string name, out MethodInfo method)
		{
			if (!string.IsNullOrEmpty(name))
			{
				Assembly GetAssembly()
				{
					if (assemblyMap.ContainsKey(name)) return assemblyMap[name];

					var dllIndex = name.IndexOf(".dll", StringComparison.InvariantCulture);
					if (dllIndex > 0)
					{
						var assemblyName = name.Substring(0, dllIndex) + ", ";
						var assemblies = AppDomain.CurrentDomain.GetAssemblies();
						foreach (var ass in assemblies)
						{
							if (ass.FullName.StartsWith(assemblyName))
							{
								assemblyMap.Add(name, ass);
								return ass;
							}
						}
					}
					else
					{
#if DEBUG_ACCESS
						Debug.LogWarning($"No dll found: {name}");
#endif
					}

					return null;
				}

				var assembly = GetAssembly();
				if (assembly != null)
				{
					const string separator = "!";
					var methodNameIndex = name.IndexOf(separator, StringComparison.InvariantCulture);
					var fullName = name.Substring(methodNameIndex + separator.Length);
					// TODO: params and generics?!
					fullName = fullName.Substring(0, fullName.IndexOf("(", StringComparison.InvariantCulture));
					fullName = fullName.Replace("::", ".");
					var separatorIndex = fullName.LastIndexOf(".", StringComparison.InvariantCulture);
					var typeName = fullName.Substring(0, separatorIndex);
					var type = assembly.GetType(typeName);
					var methodName = fullName.Substring(separatorIndex + 1);
					method = type?.GetMethod(methodName, (BindingFlags) ~0);
#if DEBUG_ACCESS
					Debug.Log($"{name}\n{assembly}\n{fullName}\n{typeName}\n{type}");
#endif
					return method != null;
				}
			}

			method = null;
			return false;
		}
		
		

		public const BindingFlags All = (BindingFlags) ~0;

		public enum Level
		{
			Unknown = 0,
			System = 1,
			Unity = 2,
			User = 3,
		}
		

		public static IEnumerable<MethodInfo> GetMethods(object obj, BindingFlags flags, Type maxType)
		{
			if (obj == null) yield break;
			if (obj is Object o && !o) yield break;
			foreach (var m in GetMethods(obj.GetType(), flags, maxType))
				yield return m;
		}

		public static IEnumerable<MethodInfo> GetMethods(Type type, BindingFlags flags, Type maxType)
		{
			return InternalGetMethods(type, flags, maxType);
		}

		private static IEnumerable<MethodInfo> InternalGetMethods(Type type, BindingFlags flags, Type maxType = null)
		{
			IEnumerable<MethodInfo> RecursiveGetTypes(Type t)
			{
				while (t != null)
				{
					if (maxType != null && t == maxType) yield break;
					// var level = GetCurrentLevel(t);
					// if (level == maxLevel) yield break;
					// Debug.Log(t + " - " + GetCurrentLevel(t));
					var methods = t.GetMethods(flags);
					foreach (var method in methods)
					{
						yield return method;
					}
					t = t.BaseType;
					if (t != null) continue;
					break;
				}
			}

			foreach (var rec in RecursiveGetTypes(type))
				yield return rec;
		}
		
		public static Level GetLevel(Type type)
		{
			if (type == null) return Level.Unknown;
			var name = type.Assembly.FullName;
			if (name.StartsWith("mscorlib"))
				return Level.System;
			if (name.StartsWith("UnityEngine.") || name.StartsWith("UnityEditor."))
				return Level.Unity;
			return Level.User;
		}

		public static bool AllowPatching(MethodInfo method, bool isDeep, bool debugLog)
		{
			if (method == null) return false;

			if (method.DeclaringType == typeof(Profiler))
			{
				if(debugLog)
					Debug.LogWarning("Profiling types in Unity Profiler is not allowed: " + method);
				return false;
			}

			if (GetLevel(method.DeclaringType) == Level.System)
			{
				if(debugLog)
					Debug.LogWarning("Profiling system level types is not allowed: " + method);
				return false;
			}
			
			if ((method.DeclaringType?.IsGenericType ?? false) || method.IsGenericMethod)// && (method.ReturnType.IsGenericType || method.IsGenericMethod || method.ContainsGenericParameters))
			{
				Debug.LogWarning("Profiling generic types is not supported: " + method);
				return false;
			}

			// if (method.DeclaringType != null)
			// {
			// 	if (typeof(MonoBehaviour).IsAssignableFrom(method.DeclaringType) && method.Name == "OnValidate" && method.GetParameters().Length <= 0)
			// 	{
			// 		return false;
			// 	}
			// }

			return true;
		}
	}
}