using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Serialization;

namespace Needle.SelectiveProfiling
{
	/// <summary>
	/// serializeable method information
	/// </summary>
	[Serializable]
	internal class MethodInformation : IEquatable<MethodInformation>
	{
		public string Assembly;
		public string Type;
		public string Method;

		internal MethodInformation(MethodInfo method)
		{
			Method = method.ToString();
			var t = method.DeclaringType;
			Type = t?.FullName;
			Assembly = t?.Assembly.FullName;
		}

		private MethodInformation(MethodInformation other)
		{
			Assembly = other.Assembly;
			Type = other.Type;
			Method = other.Method;
		}

		internal MethodInformation Copy()
		{
			return new MethodInformation(this);
		}

		public string TypeIdentifier() => IsValid() ? Assembly + "." + Type : null;
		public string MethodIdentifier() => IsValid() ? Assembly + "." + Type + "." + Method : null;
		public bool IsValid() => !string.IsNullOrEmpty(Assembly) && !string.IsNullOrEmpty(Type) && !string.IsNullOrEmpty(Method);

		public override string ToString()
		{
			return IsValid() ? "<b>Assembly</b>: " + Assembly + ", <b>Type</b>: " + Type + ", <b>Method</b>: " + Method + "\n<b>Identifier</b>: " + MethodIdentifier() : "Invalid " +  base.ToString();
		}
		
		public string ClassWithMethod()
		{
			var dotIndex = Type.LastIndexOf(".", StringComparison.Ordinal);
			var className = dotIndex > 0 && dotIndex < Type.Length - 1 ? Type.Substring(dotIndex + 1) : Type;
			return className + "." + Method;
		}

		#region Equatable
		public bool Equals(MethodInformation other)
		{
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return Assembly == other.Assembly && Type == other.Type && Method == other.Method;
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((MethodInformation) obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = (Assembly != null ? Assembly.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (Type != null ? Type.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (Method != null ? Method.GetHashCode() : 0);
				return hashCode;
			}
		}
		#endregion
	}

	internal static class ProfiledMethodExtensions
	{
		private static readonly Dictionary<string, Assembly> AssembliesCache = new Dictionary<string, Assembly>();
		private static readonly Dictionary<string, Type> TypesCache = new Dictionary<string, Type>();
		private static readonly Dictionary<string, MethodInfo> MethodsCache = new Dictionary<string, MethodInfo>();

		public static event Action<(string oldIdentifier, string newIdentifier)> MethodIdentifierChanged;

		public static bool TryResolveMethod(this MethodInformation pm, out MethodInfo method, bool allowFormerlySerialized = true)
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
					
					foreach (var m in methods)
					{
						var fs = m.GetCustomAttribute<FormerlySerializedAsAttribute>();
						if (fs != null && fs.oldName == pm.Method)
						{
							// method name changed, update
							var old = pm.MethodIdentifier();
							pm.Method = m.ToString();
							MethodsCache.Add(pm.MethodIdentifier(), m);
							method = m;
							MethodIdentifierChanged?.Invoke((old, pm.MethodIdentifier()));
							return true;
						}
					}
				}
				
				Debug.LogWarning("Could not resolve method " + pm.MethodIdentifier());
			}
			
			method = null;
			return false;
		}
	}
}