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

				var methods = type.GetMethods(AccessUtils.AllDeclared);
				const string separator = "/";
				foreach (var m in methods)
				{
					items.Add(new ContextItem("Profiling/" + type.Name + separator + m, () => OnSelected(m)));
				}

				AddMethods(type.BaseType, onlyUser, maxLevel, ++currentLevel);
			}

			AddMethods(ctx.GetType(), AccessUtils.GetLevel(ctx.GetType()) == AccessUtils.Level.User, 1);

		}

		private static void OnSelected(MethodInfo method)
		{
			SelectiveProfiler.EnableProfiling(method);
		}
	}
}