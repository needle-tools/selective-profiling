using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	internal static class TypesExplorer
	{
		public static async void TryFindMethodAsync(string filter, List<MethodInfo> matches, IProgress<MethodInfo> updated)
		{
			if (string.IsNullOrWhiteSpace(filter))
			{
				return;
			}

			EnsureTypesLoaded();
			if (!typesLoaded)
			{
				return;
			}

			filter = filter.ToLowerInvariant();

			await Task.Run(() =>
			{
				foreach (var kvp in methodsList.AsParallel())
				{
					if (kvp.Key.Contains(filter))
					{
						matches.Add(kvp.Value);
						updated.Report(kvp.Value);
					}
				}
			});
		}
		
		private static async void EnsureTypesLoaded()
		{
			if (typesLoaded) return;
			if (isLoading) return;
			isLoading = true;
			await Task.Run(() =>
			{
				foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
				{
					var assemblyName = assembly.FullName;
					foreach (var type in assembly.GetTypes())
					{
						foreach (var member in type.GetMethods())
						{
							var key = assemblyName + " " + type.FullName + " " + member.FullDescription();
							key = key.ToLowerInvariant();
							if(!methodsList.ContainsKey(key))
								methodsList.Add(key, member);
						}
					}
				}
			});
			Debug.Log("Found " + methodsList.Count + " methods");
			isLoading = false;
			typesLoaded = true;
		}

		private static bool typesLoaded, isLoading;
		private static volatile Dictionary<string, MethodInfo> methodsList = new Dictionary<string, MethodInfo>();
	}
}