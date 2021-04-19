using Needle.SelectiveProfiling.Utils;
using UnityEditor;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	internal static class MenuItems
	{
		internal const string Menu = "Needle/SelectiveProfiler/";
		internal const string ToolsMenu = "Tools/SelectiveProfiler/";
		internal const string WindowsMenu = "Window/Analysis/";
		private const int Priority = 10;
		private const string Submenu = "Profiling/";
		private const string Component = "CONTEXT/Component/" + Submenu;
		private const string GameObject = "GameObject/" + Submenu;

		// [MenuItem(Component + "Enable: Profile All User Methods", true)]
		// private static bool ProfileAllMethods_Validate(MenuCommand cmd) => AccessUtils.GetLevel(cmd.context.GetType()) == Level.User;
		//
		// [MenuItem(Component + "Enable: Profile All User Methods", false, Priority)]
		// private static void ProfileAllMethods(MenuCommand cmd)
		// {
		// 	foreach (var m in AccessUtils.GetMethods(cmd.context, typeof(MonoBehaviour)))
		// 		SelectiveProfiler.EnableProfilingAsync(m, SelectiveProfiler.ShouldSave);
		// }
		//
		// [MenuItem(Component + "Disable: Profile All User Methods", true)]
		// private static bool DisableDeepProfileAllMethods_Validate(MenuCommand cmd) => AccessUtils.GetLevel(cmd.context.GetType()) == Level.User;
		//
		// [MenuItem(Component + "Disable: Profile All User Methods", false, Priority)]
		// private static void DisableDeepProfileAllMethods(MenuCommand cmd)
		// {
		// 	foreach (var m in AccessUtils.GetMethods(cmd.context, typeof(MonoBehaviour)))
		// 		SelectiveProfiler.DisableProfiling(m);
		// }


		[MenuItem(GameObject + "Enable Profiling For All User Methods in Hierarchy", false, Priority)]
		private static void EnableProfileHierarchy(MenuCommand cmd)
		{
			var obj = cmd.context as GameObject;
			if (!obj) return;
			foreach (var comp in obj.GetComponentsInChildren<Component>())
			{
				if (AccessUtils.GetLevel(comp.GetType()) != Level.User) continue;
				foreach (var m in AccessUtils.GetMethods(comp, typeof(MonoBehaviour)))
					SelectiveProfiler.EnableProfilingAsync(m, SelectiveProfiler.ShouldSave);
			}
		}

		[MenuItem(GameObject + "Disable Profiling For All User Methods in Hierarchy", false, Priority)]
		private static void DisableProfileHierarchy(MenuCommand cmd)
		{
			var obj = cmd.context as GameObject;
			if (!obj) return;
			foreach (var comp in obj.GetComponentsInChildren<Component>())
			{
				if (AccessUtils.GetLevel(comp.GetType()) != Level.User) continue;
				foreach (var m in AccessUtils.GetMethods(comp, typeof(MonoBehaviour)))
					SelectiveProfiler.DisableProfiling(m);
			}
		}
		
		
		// [MenuItem("CONTEXT/MonoImporter/Profile")]
		// private static void Profile(MenuCommand command)
		// {
		// 	var importer = command.context as AssetImporter;
		// 	Debug.Log("doing nothing yet " +  importer?.assetPath);
		// }
	}
}