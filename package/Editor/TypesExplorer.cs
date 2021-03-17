using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;

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
			var filters = filter.Split(' ');

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
							Debug.Log("cancelled");
							break;
						}
						var entry = methodsList[index];
						if (filters.All(entry.Filter.Contains))
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
				foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
				{
					foreach (var type in assembly.GetTypes())
					{
						foreach (var member in type.GetMethods())
						{
							var fullName = type.FullName + " " + member;
							var name = member.DeclaringType?.Name + "." + member.Name;
							var filter = fullName + " " + name + " " + member.FullDescription();
							filter = filter.ToLowerInvariant();
							methodsList.Add(new FilterEntry<MethodInfo>(filter, fullName, member, name));
						}
					}
				}
			});
			Debug.Log("Found " + methodsList.Count + " methods");
			isLoading = false;
			typesLoaded = true;
			AllTypesLoaded?.Invoke();
		}


		private static bool typesLoaded, isLoading;
		private static volatile List<FilterEntry<MethodInfo>> methodsList = new List<FilterEntry<MethodInfo>>();
	}
}