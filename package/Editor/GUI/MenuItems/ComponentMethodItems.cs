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
				if (onlyUser && AccessUtils.GetLevel(type) != AccessUtils.Level.User) return;

				const string separator = "/";
				var basePath = "Profiling/" + type.Name + separator;
				var methods = type.GetMethods(AccessUtils.AllDeclared);
				
				// var patchAll =
				
				foreach (var m in methods)
				{
					var allowed = AccessUtils.AllowPatching(m, false, false);
					if (!allowed) continue;
					var name = m.ToString();
					if(SelectiveProfiler.IsProfiling(m, true)) 
						name += " ✓";
					const int maxNameLength = 80;
					if (name.Length > maxNameLength)
						name = name.Substring(Mathf.Max(0, name.Length - maxNameLength));
					var item = new ContextItem(basePath + name, () => OnSelected(m));
					// ReSharper disable once ConditionIsAlwaysTrueOrFalse
					item.Enabled = allowed;
					items.Add(item);
				}

				AddMethods(type.BaseType, onlyUser, maxLevel, ++currentLevel);
			}

			// var onlyUser = AccessUtils.GetLevel(ctx.GetType()) == AccessUtils.Level.User;
			var onlyUser = false;
			AddMethods(ctx.GetType(), onlyUser, 1);

		}

		private static void OnSelected(MethodInfo method)
		{
			var prov = SelectiveProfiler.IsProfiling(method, true);
			if (prov)
			{
				SelectiveProfiler.DisableProfiling(method);
			}
			else
			{
				SelectiveProfiler.EnableProfiling(method, !Application.isPlaying, true, true);
			}
		}
	}
}