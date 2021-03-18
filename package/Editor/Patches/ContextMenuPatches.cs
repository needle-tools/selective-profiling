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
	internal sealed class ContextMenuPatches : EditorPatchProvider
	{
		protected override void OnGetPatches(List<EditorPatch> patches)
		{
			patches.Add(new GenericMenuPatch());
		}
		
		private static readonly List<IContextMenuItemProvider> itemProviders = new List<IContextMenuItemProvider>();

		public static void RegisterProvider(IContextMenuItemProvider prov)
		{
			if (!itemProviders.Contains(prov))
				itemProviders.Add(prov);
		}

		private class GenericMenuPatch : EditorPatch
		{
			protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
			{
				// https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Editor/Mono/GUI/GenericMenu.cs#L129
				targetMethods.Add(typeof(GenericMenu).GetMethod("ObjectContextDropDown", BindingFlags.Instance | BindingFlags.NonPublic));
				return Task.CompletedTask;
			}
			
			private static void CatchMenu(object userData, string[] options, int selected)
			{
				if (selected < 0 || selected >= items.Count) return;
				var item = items[selected];
				item.Selected?.Invoke();
			}

			private static readonly List<ContextItem> items = new List<ContextItem>();
			private static MethodInfo displayMethodInfo;
			
			private static readonly List<string> titles = new List<string>();
			private static readonly List<bool> enabled = new List<bool>();
			private static readonly List<bool> separator = new List<bool>();

			private static bool Prefix(Rect position, Object[] context, int contextUserData)
			{
				titles.Clear();
				enabled.Clear();
				separator.Clear();
				items.Clear();
				
				// collect items
				foreach (var prov in itemProviders)
					prov.AddItems(context, contextUserData, items);
				
				// add items
				foreach (var item in items)
				{
					if (item == null) continue;
					titles.Add(item.Path);
					enabled.Add(item.Enabled);
					separator.Add(item.Separator);
				}

				// invoke unity api
				if (displayMethodInfo == null)
				{
					displayMethodInfo =
						typeof(EditorUtility).GetMethod("DisplayObjectContextPopupMenuWithExtraItems", BindingFlags.NonPublic | BindingFlags.Static);
					if (displayMethodInfo == null) return true;
				}

				displayMethodInfo.Invoke(null, new object[]
				{
					position, context, contextUserData,
					titles.ToArray(), enabled.ToArray(), separator.ToArray(), new int[0], (EditorUtility.SelectMenuItemFunction) CatchMenu,
					null, true
				});
				return false;
			}
		}

		/*
		 *	other tests
		 *
		 *
		 *
		 * 
			// https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Editor/Mono/EditorGUI.cs#L1454
			// used by https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/UIElementsEditor/Inspector/EditorElement.cs#L347
			// https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Editor/Mono/EditorUtility.cs#L441

		 *
				// targetMethods.Add(typeof(UnityEditor.UIElements.ColorField).Assembly.GetType("UnityEditor.UIElements.EditorElement").GetMethod("HeaderOnGUI", (BindingFlags)~0));

				// targetMethods.Add(typeof(EditorUtility).GetMethod("DisplayObjectContextMenu", BindingFlags.Static | BindingFlags.NonPublic,
				// 	null, new []{typeof(Rect), typeof(Array), typeof(int)}, null));

				// targetMethods.Add(typeof(EditorUtility).GetMethod("Internal_DisplayPopupMenu", BindingFlags.Static | BindingFlags.NonPublic,
				// 	null, new []{typeof(Rect), typeof(string), typeof(Object), typeof(int)}, null));

				// Internal_DisplayPopupMenu(Rect position, string menuItemPath, Object context, int contextUserData)
				
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
				
				
		 *
		 * 
		 */
	}
}