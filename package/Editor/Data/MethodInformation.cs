using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using needle.EditorPatching;
using Needle.SelectiveProfiling.Utils;
using UnityEngine;
using UnityEngine.Serialization;

namespace Needle.SelectiveProfiling
{
	/// <summary>
	/// serializeable method information
	/// </summary>
	[Serializable]
	public class MethodInformation : IEquatable<MethodInformation>, IEquatable<MethodInfo>
	{
		public bool Enabled = true;
		public string Assembly;
		public string Type;
		public string Method;

		internal MethodInfo CachedMethod;
		internal string LoadError;

		public bool IsMissing => !string.IsNullOrEmpty(LoadError) && CachedMethod == null;

		internal MethodInformation(MethodInfo method)
		{
			if(method != null)
				UpdateFrom(method);
		}

		internal void UpdateFrom(MethodInfo method)
		{
			Method = method.ToString();
			var t = method.DeclaringType;
			Type = t?.FullName;
			Assembly = t?.Assembly.FullName;
			CachedMethod = method;
		}

		private MethodInformation(MethodInformation other)
		{
			Assembly = other.Assembly;
			Type = other.Type;
			Method = other.Method;
			Enabled = other.Enabled;
			CachedMethod = other.CachedMethod;
			
			if (string.IsNullOrEmpty(_methodIdentifier)) _methodIdentifier = other._methodIdentifier;
			if (string.IsNullOrEmpty(_typeIdentifier)) _typeIdentifier = other._typeIdentifier;
			if (string.IsNullOrEmpty(_asString)) _asString = other._asString;
			if (string.IsNullOrEmpty(_classWithMethod)) _classWithMethod = other._classWithMethod;
		}

		internal MethodInformation Copy()
		{
			return new MethodInformation(this);
		}

		private string _methodIdentifier, _typeIdentifier, _asString, _classWithMethod;

		public string TypeIdentifier()
		{
			if(string.IsNullOrWhiteSpace(_typeIdentifier))
				_typeIdentifier = IsValid() ? Assembly + "." + Type : null;
			return _typeIdentifier;
		}

		public string MethodIdentifier()
		{
			if(string.IsNullOrWhiteSpace(_methodIdentifier))
				_methodIdentifier = IsValid() ? Assembly + "." + Type + "." + Method : null;
			return _methodIdentifier;
		}

		public bool IsValid() => !string.IsNullOrEmpty(Assembly) && !string.IsNullOrEmpty(Type) && !string.IsNullOrEmpty(Method);


		public override string ToString()
		{
			if(_asString == null)
				_asString =  IsValid() ? "<b>Assembly</b>: " + Assembly + ", <b>Type</b>: " + Type + ", <b>Method</b>: " + Method + "\n<b>Identifier</b>: " + MethodIdentifier() : "Invalid " +  base.ToString();
			return _asString;
		}

		public string ClassWithMethod()
		{
			if (string.IsNullOrWhiteSpace(_classWithMethod))
			{
				var dotIndex = Type.LastIndexOf(".", StringComparison.Ordinal);
				var className = dotIndex > 0 && dotIndex < Type.Length - 1 ? Type.Substring(dotIndex + 1) : Type;
				_classWithMethod = className + "." + Method;
			}
			return _classWithMethod;
		}

		public string ExtractAssemblyName()
		{
			var index = Assembly.IndexOf(",", StringComparison.InvariantCultureIgnoreCase);
			if (index > 0) return Assembly.Substring(0, index);
			return Assembly;
		}

		public string ExtractNamespace()
		{
			var dotIndex = Type.LastIndexOf(".", StringComparison.Ordinal);
			if (dotIndex >= 0) return Type.Substring(0, dotIndex);
			return "global";
		}

		#region Equatable

		public bool Equals(MethodInfo other)
		{
			if (other == null) return false;
			if (CachedMethod != null) return CachedMethod.Equals(other);
			var dt = other.DeclaringType;
			if (dt != null && dt.FullName == Type)
			{
				if(dt.Assembly.FullName == Assembly)
					return other.ToString() == Method;
			}

			return false;
		}
		
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
			if (pm.CachedMethod != null)
			{
				method = pm.CachedMethod;
				return true;
			}

			void SaveOrLogProblem(string msg)
			{
				pm.LoadError = msg;
				if(SelectiveProfilerSettings.instance.DebugLog)
					Debug.LogWarning(msg);
				
			}
			
			if (pm?.IsValid() ?? false)
			{
				// check if a method has already been resolved
				var methodIdentifier = pm.MethodIdentifier();
				if (MethodsCache.ContainsKey(methodIdentifier))
				{
					pm.CachedMethod = method = MethodsCache[methodIdentifier];
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
					SaveOrLogProblem("Could not find assembly " + pm.Assembly);
					method = null;
					return false;
				}

				var requireUpdateIfSuccessfullyResolved = false;
				if (type == null)
				{
					try
					{
						type = assembly.GetType(pm.Type, true, true);
						if (type != null)
						{
							TypesCache.Add(pm.TypeIdentifier(), type);
						}
					}
					catch (TypeLoadException typeLoadException)
					{
						var types = assembly.GetLoadableTypes();
						type = types.FirstOrDefault(t => t.Name == pm.Type);
						requireUpdateIfSuccessfullyResolved |= type != null;
						if (type == null)
						{
							SaveOrLogProblem("Could not find type " + pm.Type);
							method = null;
							return false;
						}
					}
				}

				
				var methods = type?.GetMethods(AccessUtils.AllDeclared);
				if (methods != null)
				{
					foreach (var m in methods)
					{
						if (m.ToString() == pm.Method)
						{
							MethodsCache.Add(methodIdentifier, m);
							pm.CachedMethod = method = m;
							if (requireUpdateIfSuccessfullyResolved) 
								pm.UpdateFrom(method);
							return true;
						}
					}

					if (allowFormerlySerialized)
					{
						foreach (var m in methods)
						{
							var fs = m.GetCustomAttribute<FormerlySerializedAsAttribute>();
							if (fs != null && fs.oldName == pm.Method)
							{
								// method name changed, update
								var old = methodIdentifier;
								pm.Method = m.ToString();
								method = m;
								if (requireUpdateIfSuccessfullyResolved) 
									pm.UpdateFrom(method);
								pm.CachedMethod = method;
								MethodsCache.Add(methodIdentifier, m);
								MethodIdentifierChanged?.Invoke((old, methodIdentifier));
								return true;
							}
						}
					}
				}
				
				SaveOrLogProblem("Could not resolve method " + methodIdentifier + "\n" + assembly + "\n" + type);
			}
			
			method = null;
			return false;
		}
	}
}