#define DEBUG_ACCESS
#undef DEBUG_ACCESS

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using needle.EditorPatching;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Scripting;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Object = UnityEngine.Object;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;

namespace Needle.SelectiveProfiling.Utils
{
	[Flags]
	public enum Level
	{
		System = 1 << 1,
		Unity = 1 << 2,
		User = 1 << 3,
		Unknown = 1 << 4,
	}

	internal static class AccessUtils
	{
		private static readonly Dictionary<string, Assembly> assemblyMap = new Dictionary<string, Assembly>();

		public static string GetMethodIdentifier(this MethodInfo info)
		{
			if (info == null) return null;
			var declaring = info.DeclaringType;
			var assembly = declaring?.Assembly;
			return assembly?.GetName().Name + ".dll" + ", " + declaring?.Namespace + "::" + declaring?.Name + info.Name + "(" +
			       string.Join(", ", info.GetParameters().Select(p => p.ParameterType)) + ")";
		}

		private static Assembly[] assemblies;
		private static Dictionary<Assembly, IEnumerable<Type>> types;

		private static void EnsureAssembliesLoaded()
		{
			if (assemblies == null)
				assemblies = AppDomain.CurrentDomain.GetAssemblies();
		}


		public static bool TryGetMethodFromName(string name,
			out List<MethodInfo> methodsFound,
			bool includeChildren = true,
			int itemId = -1,
			HierarchyFrameDataView view = null)
		{
			methodsFound = null;
			TryGetMethodFromFullyQualifiedName(name, ref methodsFound);

			if (includeChildren)
			{
				TryFindMethodFromCallstack(itemId, view, ref methodsFound);
				TryFindMethodsInChildrenFromNames(itemId, view, ref methodsFound);

				if (SelectiveProfiler.DevelopmentMode)
				{
					// if (TryFindMethodInAssembliesByName(name, out method)) return true;
				}
			}

			return methodsFound != null && methodsFound.Count > 0;
		}

		private static readonly List<ulong> callstackList = new List<ulong>();

		private static bool TryFindMethodFromCallstack(int _itemId, HierarchyFrameDataView view, ref List<MethodInfo> methods)
		{
			if (view == null || !view.valid || _itemId < 0)
			{
				return false;
			}

			bool FindMethodCallstackRecursive(int itemId, ref List<MethodInfo> foundMethods)
			{
				// var callStack = view.ResolveItemCallstack(itemId);
				// if(!string.IsNullOrEmpty(callStack))
				// 	Debug.Log(callStack);

				callstackList.Clear();
				view.GetItemCallstack(itemId, callstackList);

				if (callstackList.Count > 0)
				{
					// Debug.Log(name + " -> " + callstackList.Count + "\n" + 
					//           string.Join("\n", callstackList.Select(i => (view.ResolveMethodInfo(i).methodName) ?? string.Empty).Where(s => !string.IsNullOrEmpty(s))) 
					//           + "\n\n\n"
					//           );
					foreach (var addr in callstackList)
					{
						var methodInfo = view.ResolveMethodInfo(addr);
						if (!string.IsNullOrEmpty(methodInfo.methodName))
						{
							if (TryGetMethodFromFullyQualifiedName(methodInfo.methodName, ref foundMethods))
							{
								// var name = view.GetItemName(itemId);
								// Debug.Log(name + ": FOUND " + methodInfo.methodName);
							}
						}
					}
				}

				if (!view.HasItemChildren(itemId)) return foundMethods != null && foundMethods.Count > 0;
				var children = new List<int>();
				view.GetItemChildren(itemId, children);
				foreach (var id in children)
				{
					FindMethodCallstackRecursive(id, ref foundMethods);
				}

				return foundMethods != null && foundMethods.Count > 0;
			}

			return FindMethodCallstackRecursive(_itemId, ref methods);
		}

		private static bool TryFindMethodsInChildrenFromNames(int itemId, HierarchyFrameDataView frameData, ref List<MethodInfo> methods)
		{
			if (itemId < 0 || frameData == null || !frameData.valid) return false;

			void InternalFindMethods(int id, ref List<MethodInfo> methodsList)
			{
				var name = frameData.GetItemName(id);

				if (TryGetMethodFromName(name, out var methodInfo, false))
				{
					foreach (var m in methodInfo)
					{
						if (methodsList == null) methodsList = new List<MethodInfo>();
						methodsList.Add(m);
					}
				}

				if (!frameData.HasItemChildren(id)) return;
				var children = new List<int>();
				frameData.GetItemChildren(id, children);
				foreach (var child in children)
				{
					InternalFindMethods(child, ref methodsList);
				}
			}

			InternalFindMethods(itemId, ref methods);
			return methods != null && methods.Count > 0;
		}


		private static bool TryFindMethodInAssembliesByName(string name, out MethodInfo method)
		{
			// EnsureAssembliesLoaded();
			// foreach (var assembly in assemblies)
			// {
			// 	if (types == null) types = new Dictionary<Assembly, IEnumerable<Type>>();
			// 	if (!types.ContainsKey(assembly)) types.Add(assembly, assembly.GetLoadableTypes());
			// 	foreach (var type in types[assembly])
			// 	{
			// 		if (type.Name == "SceneHierarchyWindow")
			// 		{
			// 			Debug.Log(type);
			// 			break;
			// 		}
			// 	}
			// }

			method = null;
			return false;
		}

		private static bool TryGetMethodFromFullyQualifiedName(string name, ref List<MethodInfo> methodList)
		{
			if (!string.IsNullOrEmpty(name))
			{
				Assembly GetAssembly(ref Assembly[] _assemblies)
				{
					if (assemblyMap.ContainsKey(name)) return assemblyMap[name];

					var dllIndex = name.IndexOf(".dll", StringComparison.InvariantCulture);
					if (dllIndex > 0)
					{
						var assemblyName = name.Substring(0, dllIndex) + ", ";
						EnsureAssembliesLoaded();
						foreach (var ass in _assemblies)
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

				var assembly = GetAssembly(ref assemblies);
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
					try
					{
						var method = type?.GetMethod(methodName, AccessUtils.All);
						if (method != null)
						{
							if (methodList == null)
								methodList = new List<MethodInfo>() {method};
							else methodList.Add(method);
						}
					}
					catch (AmbiguousMatchException am)
					{
						var _allMethods = type?.GetMethods(AllDeclared);
						var found = 0;
						if (_allMethods != null)
						{
							foreach (var _method in _allMethods)
							{
								// TODO: not sure if this is the most save way to collect ambiguous matches
								if (_method.Name != methodName) continue;
								if (methodList == null) methodList = new List<MethodInfo>();
								methodList.Add(_method);
								++found;
							}
						}

						var success = methodList != null && methodList.Count > 0;

						if (SelectiveProfiler.DebugLog || SelectiveProfiler.DevelopmentMode)
						{
							if (!success)
								Debug.LogException(am);
							if (SelectiveProfiler.DebugLog)
								Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "Found AmbiguousMatch for <i>" + fullName + "</i>" + (success ? " but returning " + found + " matching methods" : " and could not find matching methods"));
						}
					}
#if DEBUG_ACCESS
					Debug.Log($"{name}\n{assembly}\n{fullName}\n{typeName}\n{type}");
#endif
					return methodList != null && methodList.Count > 0;
				}
			}

			return methodList != null && methodList.Count > 0;
		}


		public static BindingFlags All => AccessTools.all;
		public static BindingFlags AllDeclared => AccessTools.allDeclared;


		public static IEnumerable<MethodInfo> GetMethods(object obj, Type maxType)
		{
			if (obj == null) yield break;
			if (obj is Object o && !o) yield break;
			foreach (var m in GetMethods(obj.GetType(), maxType))
				yield return m;
		}

		public static IEnumerable<MethodInfo> GetMethods(Type type, Type maxType)
		{
			return InternalGetMethods(type, maxType);
		}

		private static IEnumerable<MethodInfo> InternalGetMethods(Type type, Type maxType = null)
		{
			IEnumerable<MethodInfo> RecursiveGetTypes(Type t)
			{
				while (t != null)
				{
					if (maxType != null && t == maxType) yield break;
					// var level = GetCurrentLevel(t);
					// if (level == maxLevel) yield break;
					var methods = t.GetMethods(AllDeclared);
					foreach (var method in methods)
					{
						if (maxType != null && method.DeclaringType == maxType) continue;
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

		public static bool AllowedLevel([NotNull] MethodInfo method, Level levels)
		{
			if (method == null) throw new ArgumentNullException(nameof(method));
			var level = GetLevel(method.DeclaringType);
			return (level & levels) != 0;
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

		public static bool IsProperty(MethodInfo method)
		{
			var isPropertyByName = method.IsSpecialName && method.Name.StartsWith("get_") || method.Name.StartsWith("set_");
			if (isPropertyByName) return true;
			return method.DeclaringType?.GetProperties(AccessUtils.AllDeclared).Any(prop => prop.GetSetMethod() == method) ?? false;
		}

		public static bool TryGetDeclaredMember<T>(T member, out T declared) where T : MemberInfo
		{
			// see Harmony PatchProcessor.cs:136
			if (member.IsDeclaredMember() is false)
			{
				declared = member.GetDeclaredMember();
				return declared != null;
			}

			declared = null;
			return false;
		}

		public static string AllowPatchingResultLastReason;

		public static bool AllowPatching(MethodInfo method, bool isDeep, bool debugLog)
		{
			if (method == null) return false;
			AllowPatchingResultLastReason = null;

			string GetMethodLogName()
			{
				if (method?.DeclaringType != null) return method.DeclaringType.FullName + " -> " + method;
				return method?.ToString() ?? "null";
			}

			void Reason(string msg)
			{
				AllowPatchingResultLastReason = msg;
				if (debugLog)
					Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, msg);
			}

			
			// see Harmony PatchProcessor.cs:136
			if (method.IsDeclaredMember() is false)
			{
				if (TryGetDeclaredMember(method, out var declared) && declared.IsDeclaredMember())
				{
					method = declared;
				}
				else
				{
					Reason("Method is not declared or null: " + GetMethodLogName());
					return false;
				}
			}
			

			if (!method.HasMethodBody())
			{
				Reason("Method has no body: " + GetMethodLogName());
				return false;
			}

			if (method.Name.StartsWith("op_"))
			{
				Reason("Operation is not allowed: " + GetMethodLogName());
				return false;
			}
			
			if (
				method.DeclaringType == typeof(Object) ||
				method.DeclaringType == typeof(GC) ||
				method.DeclaringType == typeof(GarbageCollector) ||
				method.DeclaringType == typeof(Profiler) ||
				typeof(ProfilerDriver).IsAssignableFrom(method.DeclaringType) ||
				typeof(ProfilerMarker).IsAssignableFrom(method.DeclaringType) ||
				typeof(CustomSampler).IsAssignableFrom(method.DeclaringType) ||
				method.DeclaringType == typeof(UnityException) ||
			    method.DeclaringType == typeof(Application) ||
			    method.DeclaringType == typeof(StackTraceUtility) ||
			    method.DeclaringType == typeof(AssetDatabase) ||
				method.DeclaringType == typeof(Time) ||
				method.DeclaringType == typeof(EditorPrefs) ||
				method.DeclaringType == typeof(SessionState) ||
			    method.DeclaringType == typeof(Mathf) ||
				method.DeclaringType == typeof(Matrix4x4) ||
				method.DeclaringType == typeof(Vector2) ||
				method.DeclaringType == typeof(Vector3) ||
				method.DeclaringType == typeof(Vector4) ||
				method.DeclaringType == typeof(DragAndDrop) ||
				method.DeclaringType == typeof(Undo)
			)
			{
				Reason($"Profiling in {method.DeclaringType} is not allowed: " + GetMethodLogName());
				return false;
			}
			
			// Generics
			// See https://harmony.pardeike.net/articles/patching.html#commonly-unsupported-use-cases
			// Got various crashes when patching generics was enabled (e.g. patching generic singleton Instance.Getter caused crashes)
			if ((method.DeclaringType?.IsGenericType ?? false) ||
			    method.IsGenericMethod) // && (method.ReturnType.IsGenericType || method.IsGenericMethod || method.ContainsGenericParameters))
			{
				Reason("Profiling generic types is not supported: " + GetMethodLogName() +
				       "\nSee issue: https://github.com/needle-tools/selective-profiling/issues/6");
				return false;
			}

			// See https://github.com/needle-tools/selective-profiling/issues/2 
			var settings = SelectiveProfilerSettings.instance;
			if (settings.SkipProperties)
			{
				if (IsProperty(method))
				{
					Reason("Profiling properties is disabled in settings: " + GetMethodLogName() +
					       "\nFor more information please refer to https://github.com/needle-tools/selective-profiling/issues/2");
					return false;
				}
			}

			if (GetLevel(method.DeclaringType) == Level.System)
			{
				Reason("Profiling system level types is not allowed: " + GetMethodLogName());
				return false;
			}

			var assembly = method.DeclaringType?.Assembly;
			if (assembly == typeof(PatchManager).Assembly || assembly == typeof(Harmony).Assembly)
			{
				Reason($"Profiling method in {assembly} is not allowed: " + GetMethodLogName());
				return false;
			}

			var assemblyName = ExtractAssemblyNameWithoutVersion(method.DeclaringType?.Assembly);
			if (!string.IsNullOrEmpty(assemblyName))
			{
				switch (assemblyName)
				{
					case "UnityEngine.UIElementsNativeModule":
					case "UnityEngine.IMGUIModule":
					// case "UnityEngine.CoreModule":
					// case "UnityEditor.CoreModule":
					// case "UnityEditor.UIElementsModule":
					// case "UnityEngine.UIElementsModule":
					// case "UnityEngine.SharedInternalsModule":
					// case "UnityEditor.PackageManagerUIModule":
						Reason("Profiling in " + assemblyName + " is not allowed: " + GetMethodLogName());
						return false;
				}
			}

			var fullName = method.DeclaringType?.FullName;
			if (!string.IsNullOrEmpty(fullName))
			{
				if (
					fullName.StartsWith("System.") ||
					fullName.StartsWith("UnityEditor.Profiling") ||
				    fullName.StartsWith("UnityEditorInternal.Profiling") ||
				    fullName.StartsWith("UnityEditorInternal.InternalEditorUtility") ||
				    fullName.StartsWith("UnityEditor.ProfilerWindow") ||
				    fullName.StartsWith("UnityEditor.HostView") ||
				    fullName.StartsWith("UnityEngine.UIElements.UIR") ||
				    fullName.StartsWith("UnityEditor.StyleSheets") ||
				    fullName.StartsWith("UnityEngineInternal.Input.NativeInputSystem") ||
				    fullName.StartsWith("UnityEngine.SendMouseEvents") ||
				    fullName.StartsWith("UnityEditor.PlayModeView") ||
					fullName.StartsWith("UnityEditor.IMGUI.Controls.TreeViewController")
				)
				{
					Reason($"Profiling in {fullName} is not allowed: " + GetMethodLogName());
					return false;
				}
			}
			// if (method.DeclaringType != null)
			// {
			// 	if (typeof(MonoBehaviour).IsAssignableFrom(method.DeclaringType) && method.Name == "OnValidate" && method.GetParameters().Length <= 0)
			// 	{
			// 		return false;
			// 	}
			// }
			
			
			foreach (var attr in method.GetCustomAttributes())
			{
				var attributeTypeName = attr.TypeId.ToString();
				// dont patch methods marked with [RequiredByNativeCode]
				if (attributeTypeName == "UnityEngine.Scripting.RequiredByNativeCodeAttribute")
				{
					Reason($"Profiling method with {attributeTypeName} attribute is not allowed: " + GetMethodLogName());
					return false;
				}
			}


			return true;
		}


		private static string ExtractAssemblyNameWithoutVersion(Assembly assembly)
		{
			if (assembly == null) return null;
			var name = assembly.FullName;
			var index = name.IndexOf(",", StringComparison.InvariantCultureIgnoreCase);
			if (index > 0) return name.Substring(0, index);
			return name;
		}
	}
}