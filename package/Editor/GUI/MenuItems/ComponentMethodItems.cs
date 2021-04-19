using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Needle.SelectiveProfiling.Utils;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Needle.SelectiveProfiling
{
	internal class ComponentMethodItems : IContextMenuItemProvider
	{
		[InitializeOnLoadMethod]
		private static void Init()
		{
			ContextMenuPatches.RegisterProvider(new ComponentMethodItems());
		}
		
		public void AddItems(Object[] context, int contextUserData, List<ContextItem> items)
		{
			var ctx = context.FirstOrDefault();
			if (!ctx) return;

			void AddMethods(Type type, bool onlyUser, int maxLevel = 0, int currentLevel = 0 )
			{
				if (currentLevel > maxLevel) return;
				if (type == null) return;
				if (onlyUser && AccessUtils.GetLevel(type) != Level.User) return;

				const string separator = "/";
				var basePath = "Profiling/" + type.Name + separator;
				var methods = type.GetMethods(AccessUtils.AllDeclared);

				bool IsAllowed(MethodInfo method) => AccessUtils.AllowPatching(method, false, false);

				var count = methods.Count(IsAllowed);
				if (count > 1)
				{
					var all = new ContextItem(basePath + "All in " + type.Name + " [" + count + "]", () =>
					{
						foreach(var m in methods) 
							SelectiveProfiler.EnableProfilingAsync(m, SelectiveProfiler.ShouldSave, true, true);	
					});
					items.Add(all);
					
					var none = new ContextItem(basePath + "None in " + type.Name + " [" + count + "]", () =>
					{
						foreach (var m in methods)
							SelectiveProfiler.DisableAndForget(m);
					});
					items.Add(none);
					
					items.Add(new ContextItem(basePath, null, false, true));
				}
				
				foreach (var m in methods)
				{
					if (!SelectiveProfiler.DevelopmentMode && !IsAllowed(m)) continue;
					var name = m.ToString();
					if(SelectiveProfiler.IsProfiling(m, true)) 
						name += " ✓";
					const int maxNameLength = 120;
					if (name.Length > maxNameLength)
						name = name.Substring(Mathf.Max(0, name.Length - maxNameLength));
					var item = new ContextItem(basePath + name, () => OnSelected(m));
					// ReSharper disable once ConditionIsAlwaysTrueOrFalse
					item.Enabled = IsAllowed(m);
					items.Add(item);
				}

				AddMethods(type.BaseType, onlyUser, maxLevel, ++currentLevel);
			}

			// var onlyUser = AccessUtils.GetLevel(ctx.GetType()) == AccessUtils.Level.User;
			const bool _onlyUser = false;
			AddMethods(ctx.GetType(), _onlyUser, 3);

		}

		private static void OnSelected(MethodInfo method)
		{
			if (SelectiveProfiler.IsProfiling(method, true))
			{
				SelectiveProfiler.DisableProfiling(method);
			}
			else
			{
				SelectiveProfiler.EnableProfilingAsync(method, SelectiveProfiler.ShouldSave, true, true);
			}
		}
	}
}