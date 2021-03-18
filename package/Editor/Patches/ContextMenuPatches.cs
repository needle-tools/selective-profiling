using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using needle.EditorPatching;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Needle.SelectiveProfiling
{
	internal class ContextItem
	{
		public readonly string Path;
		public readonly bool Separator;
		public readonly Func<bool> Enabled;
		public readonly Action Selected;

		public ContextItem(string path, bool separator, Func<bool> enabled, Action selected)
		{
			Path = path;
			Separator = separator;
			Enabled = enabled;
			Selected = selected;
		}
	}

	internal interface IContextMenuItemProvider
	{
		void AddItems(Object[] context, int contextUserData, List<ContextItem> items);
	}

	internal static class ContextMenuPatches
	{
		private static readonly List<IContextMenuItemProvider> itemProviders = new List<IContextMenuItemProvider>();

		public static void RegisterProvider(IContextMenuItemProvider prov)
		{
			if (!itemProviders.Contains(prov))
				itemProviders.Add(prov);
		}

		public class GenericMenuPatch : EditorPatch
		{
			// https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Editor/Mono/EditorGUI.cs#L1454
			// used by https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/UIElementsEditor/Inspector/EditorElement.cs#L347

			// https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Editor/Mono/EditorUtility.cs#L441

			protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
			{
				// targetMethods.Add(typeof(UnityEditor.UIElements.ColorField).Assembly.GetType("UnityEditor.UIElements.EditorElement").GetMethod("HeaderOnGUI", (BindingFlags)~0));

				// targetMethods.Add(typeof(EditorUtility).GetMethod("DisplayObjectContextMenu", BindingFlags.Static | BindingFlags.NonPublic,
				// 	null, new []{typeof(Rect), typeof(Array), typeof(int)}, null));

				// targetMethods.Add(typeof(EditorUtility).GetMethod("Internal_DisplayPopupMenu", BindingFlags.Static | BindingFlags.NonPublic,
				// 	null, new []{typeof(Rect), typeof(string), typeof(Object), typeof(int)}, null));

				// Internal_DisplayPopupMenu(Rect position, string menuItemPath, Object context, int contextUserData)

				// https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Editor/Mono/GUI/GenericMenu.cs#L129
				targetMethods.Add(typeof(GenericMenu).GetMethod("ObjectContextDropDown", BindingFlags.Instance | BindingFlags.NonPublic));
				//  ObjectContextDropDown(Rect position, Object[] context, int contextUserData)

				// targetMethods.Add(typeof(GenericMenu).GetMethod("DropDown"));

				// EditorApplication.contextualPropertyMenu += this.Context;
				// EditorUtility.DisplayPopupMenu();
				// var gm 
				// targetMethods.Add(typeof(GenericMenu).GetMethod("ObjectContextDropDown", BindingFlags.NonPublic | BindingFlags.Instance, null, 
				// 	new []{typeof(Rect), typeof(Array), typeof(int)}, null));
				// targetMethods.Add(typeof(EditorUtility).GetMethod("DisplayPopupMenu"));
				// targetMethods.Add(typeof(EditorUtility).GetMethod("DisplayObjectContextPopupMenu"));
				// var m = typeof(AdvancedDropdown).Assembly.GetType("UnityEditor.IMGUI.Controls.AdvancedDropdownDataSource").
				// var m = typeof(UnityEditor.UIElements.Toolbar).Assembly.GetType("UnityEditor.UIElements.EditorMenuExtensions")
				// 	.GetMethod(" DoDisplayEditorMenu", (BindingFlags)~0);//.Public | BindingFlags.Static);
				// Debug.Log(m);
				// targetMethods.Add(m);
				return Task.CompletedTask;
			}


			private static void CatchMenu(object userData, string[] options, int selected)
			{
				Debug.Log("Selected " + selected);
				Debug.Log(options[selected]);
			}

			private static bool Prefix(Rect position, Object[] context, int contextUserData, ArrayList ___menuItems)
			{
				var titles = new List<string>();
				var enabled = new List<bool>();
				var separator = new List<bool>();
				var selected = new ArrayList();

				titles.Add("test");
				enabled.Add(true);
				separator.Add(false);

				titles.Add("test2/nested");
				enabled.Add(true);
				separator.Add(false);

				var m = typeof(EditorUtility).GetMethod("DisplayObjectContextPopupMenuWithExtraItems", BindingFlags.NonPublic | BindingFlags.Static);

				// Void DisplayObjectContextPopupMenuWithExtraItems
				// (UnityEngine.Rect, UnityEngine.Object[], Int32, System.String[], Boolean[], Boolean[], Int32[], SelectMenuItemFunction, System.Object, Boolean)
				m?.Invoke(null, new object[]
				{
					position, context, contextUserData,
					titles.ToArray(), enabled.ToArray(), separator.ToArray(), selected.ToArray(typeof(int)), (EditorUtility.SelectMenuItemFunction) CatchMenu,
					null, true
				});
				return false;
			}
		}
	}
}