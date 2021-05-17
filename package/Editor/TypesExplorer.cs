using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using Needle.SelectiveProfiling.Utils;

namespace Needle.SelectiveProfiling
{
	internal struct Match
	{
		public string Key;
		public MethodInfo Method;
	}
	
	internal class FilterEntry<T>
	{
		public readonly string Filter;
		public readonly string FullName;
		public readonly string Name;
		public readonly T Entry;

		public FilterEntry(string filter, string fullName, T entry, string name)
		{
			Filter = filter;
			FullName = fullName;
			Name = name;
			Entry = entry;
		}
	}
	
	internal static class TypesExplorer
	{
		public static event Action AllTypesLoaded;
		
		public static async void TryFindMethod(string filter, Action<FilterEntry<MethodInfo>> changed, CancellationToken cancel)
		{
			if (string.IsNullOrWhiteSpace(filter))
			{
				return;
			}

			EnsureTypesLoaded();

			filter = filter.ToLowerInvariant();
			filter = filter.Trim();
			var matchAll = filter == "*";
			var filters = filter.Split(' ');
			if (!matchAll && filter.Length <= 0) return;

			try
			{
				await Task.Run(async () =>
				{
					while (isLoading && methodsList.Count <= 0) await Task.Delay(100, cancel);
					for (var index = 0; index < methodsList.Count || isLoading; index++)
					{
						if (isLoading && index >= methodsList.Count)
						{
							await Task.Delay(100, cancel);
							continue;
						}

						if (cancel.IsCancellationRequested)
						{
							break;
						}

						var entry = methodsList[index];
						if (matchAll || filters.All(entry.Filter.Contains))
						{
							changed?.Invoke(entry);
						}
					}
				}, cancel);
			}
			catch (TaskCanceledException)
			{
				// Debug.Log("cancelled " + filter);
			}

		}

		public static int MethodsCount => methodsList.Count;
		
		private static async void EnsureTypesLoaded()
		{
			if (typesLoaded) return;
			if (isLoading) return;
			isLoading = true;
			await Task.Run(() =>
			{
				foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().AsParallel())
				{
					foreach (var type in assembly.GetLoadableTypes().AsParallel())
					{
						void RegisterMethodInfo(MethodInfo method)
						{
							if (method == null) return;
							var fullName = type.FullName + " " + method;
							var name = method.DeclaringType?.Name + " " + method.Name;
							var filter = fullName + " " + name + " " + method.FullDescription();
							filter = filter.ToLowerInvariant();
							if (string.IsNullOrEmpty(filter)) return;
							methodsList.Add(new FilterEntry<MethodInfo>(filter, fullName, method, name));
						}

						foreach (var method in type.GetMethods(AccessTools.allDeclared)) 
						{
							RegisterMethodInfo(method);
						}
						
						// foreach (var method in type.GetMethods((BindingFlags)~0))
						// {
						// 	RegisterMethodInfo(method);
						// }
					}
				}
			});
			isLoading = false;
			typesLoaded = true;
			AllTypesLoaded?.Invoke();
		}


		private static bool typesLoaded, isLoading;
		private static volatile List<FilterEntry<MethodInfo>> methodsList = new List<FilterEntry<MethodInfo>>();
	}
}