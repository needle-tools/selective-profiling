using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	[Serializable]
	internal class ProfiledMethod
	{
		public string Assembly;
		public string Type;
		public string Method;

		internal ProfiledMethod(MethodInfo method)
		{
			Method = method.ToString();
			var t = method.DeclaringType;
			Type = t?.FullName;
			Assembly = t?.Assembly.FullName;
		}

		public string TypeIdentifier() => IsValid() ? Assembly + "." + Type : null;
		public string MethodIdentifier() => IsValid() ? Assembly + "." + Type + "." + Method : null;
		public bool IsValid() => !string.IsNullOrEmpty(Assembly) && !string.IsNullOrEmpty(Type) && !string.IsNullOrEmpty(Method);

		public override string ToString()
		{
			return IsValid() ? "<b>Assembly</b>: " + Assembly + ", <b>Type</b>: " + Type + ", <b>Method</b>: " + Method + "\n<b>Identifier</b>: " + MethodIdentifier() : "Invalid " +  base.ToString();
		}
	}

	internal static class ProfiledMethodExtensions
	{
		private static readonly Dictionary<string, Assembly> AssembliesCache = new Dictionary<string, Assembly>();
		private static readonly Dictionary<string, Type> TypesCache = new Dictionary<string, Type>();
		private static readonly Dictionary<string, MethodInfo> MethodsCache = new Dictionary<string, MethodInfo>();
		

		public static bool TryResolveMethod(this ProfiledMethod pm, out MethodInfo method, bool allowFormerlySerialized = true)
		{
			if (pm?.IsValid() ?? false)
			{
				// check if a method has already been resolved
				var methodIdentifier = pm.MethodIdentifier();
				if (MethodsCache.ContainsKey(methodIdentifier))
				{
					method = MethodsCache[methodIdentifier];
					return method != null;
				}

				// check if type has already been resolved
				Type type = null;
				var typeIdentifier = pm.TypeIdentifier();
				if (TypesCache.ContainsKey(typeIdentifier))
				{
					type = TypesCache[typeIdentifier];
				}

				Assembly assembly = null;
				if (AssembliesCache.ContainsKey(pm.Assembly))
				{
					assembly = AssembliesCache[pm.Assembly];
				}

				if (assembly == null)
				{
					foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
					{
						if (asm.FullName == pm.Assembly)
						{
							assembly = asm;
							AssembliesCache.Add(pm.Assembly, assembly);
							break;
						}
					}
				}

				if (assembly == null)
				{
					Debug.LogWarning("Could not find assembly " + pm.Assembly + "\n" + methodIdentifier);
					method = null;
					return false;
				}

				if (type == null)
				{
					type = assembly.GetType(pm.Type, true, true);
					Debug.Log(type);
					if (type != null)
					{
						TypesCache.Add(pm.TypeIdentifier(), type);
					}
				}

				
				var methods = type?.GetMethods((BindingFlags) ~0);
				if (methods != null)
				{
					foreach (var m in methods)
					{
						if (m.ToString() == pm.Method)
						{
							MethodsCache.Add(pm.MethodIdentifier(), m);
							method = m;
							return true;
						}
					}
					// TODO: check formerly serialized attribute
				}
				
				Debug.LogWarning("Could not resolve method " + pm.MethodIdentifier());
			}
			
			method = null;
			return false;
		}
	}
}