using Needle.SelectiveProfiling.Utils;
using UnityEditor;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	internal static class MenuItems
	{
		internal const string Menu = "Needle/SelectiveProfiler/";
		private const int Priority = 10;
		private const string Submenu = "Profiling/";
		private const string Component = "CONTEXT/Component/" + Submenu;
		private const string GameObject = "GameObject/" + Submenu;

		[MenuItem(Component + "Enable: Profile All User Methods", true)]
		private static bool EnableDeepProfileAllMethods_Validate(MenuCommand cmd) => AccessUtils.GetLevel(cmd.context.GetType()) == AccessUtils.Level.User;

		[MenuItem(Component + "Enable: Profile All User Methods", false, Priority)]
		private static void EnableDeepProfileAllMethods(MenuCommand cmd)
		{
			foreach (var m in AccessUtils.GetMethods(cmd.context, AccessUtils.All, typeof(MonoBehaviour)))
				SelectiveProfiler.EnableProfiling(m);
		}

		[MenuItem(Component + "Disable: Profile All User Methods", true)]
		private static bool DisableDeepProfileAllMethods_Validate(MenuCommand cmd) => AccessUtils.GetLevel(cmd.context.GetType()) == AccessUtils.Level.User;

		[MenuItem(Component + "Disable: Profile All User Methods", false, Priority)]
		private static void DisableDeepProfileAllMethods(MenuCommand cmd)
		{
			foreach (var m in AccessUtils.GetMethods(cmd.context, AccessUtils.All, typeof(MonoBehaviour)))
				SelectiveProfiler.DisableProfiling(m);
		}


		[MenuItem(GameObject + "Enable Profiling For All User Methods in Hierarchy", false, Priority)]
		private static void EnableProfileHierarchy(MenuCommand cmd)
		{
			var obj = cmd.context as GameObject;
			if (!obj) return;
			foreach (var comp in obj.GetComponentsInChildren<Component>())
			{
				if (AccessUtils.GetLevel(comp.GetType()) != AccessUtils.Level.User) continue;
				foreach (var m in AccessUtils.GetMethods(comp, AccessUtils.All, typeof(MonoBehaviour)))
					SelectiveProfiler.EnableProfiling(m);
			}
		}

		[MenuItem(GameObject + "Disable Profiling For All User Methods in Hierarchy", false, Priority)]
		private static void DisableProfileHierarchy(MenuCommand cmd)
		{
			var obj = cmd.context as GameObject;
			if (!obj) return;
			foreach (var comp in obj.GetComponentsInChildren<Component>())
			{
				if (AccessUtils.GetLevel(comp.GetType()) != AccessUtils.Level.User) continue;
				foreach (var m in AccessUtils.GetMethods(comp, AccessUtils.All, typeof(MonoBehaviour)))
					SelectiveProfiler.DisableProfiling(m);
			}
		}
	}
}