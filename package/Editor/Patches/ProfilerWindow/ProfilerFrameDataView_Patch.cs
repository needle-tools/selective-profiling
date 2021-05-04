using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using needle.EditorPatching;
using Needle.SelectiveProfiling.CodeWrapper;
using Needle.SelectiveProfiling.Utils;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

#if UNITY_2020_2_OR_NEWER

#endif

// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedType.Global

namespace Needle.SelectiveProfiling
{
	public class ProfilerFrameDataView_Patch : EditorPatchProvider
	{
		protected override void OnGetPatches(List<EditorPatch> patches)
		{
			patches.Add(new Profiler_SelectionChanged());
			patches.Add(new Profiler_CellGUI());
			patches.Add(new Profiler_Toolbar());
		}

		// public override void OnEnabledPatch()
		// {
		// 	base.OnEnabledPatch();
		// 	PatchManager.EnablePatch(typeof(ProfilerPinning.ProfilerPinning_Patch));
		// }
		//
		// public override void OnDisabledPatch()
		// {
		// 	base.OnDisabledPatch();
		// 	PatchManager.DisablePatch(typeof(ProfilerPinning.ProfilerPinning_Patch), false);
		// }

		private static SelectiveProfilerSettings _settings;

		private static SelectiveProfilerSettings Settings
		{
			get
			{
				if (!_settings) _settings = SelectiveProfilerSettings.instance;
				return _settings;
			}
		}


		private static int selectedId = -1;
		private static FieldInfo m_FrameDataViewField;
		private static HierarchyFrameDataView m_frameDataView;

		internal static HierarchyFrameDataView GetFrameDataView(TreeViewItem item)
		{
			if (m_FrameDataViewField == null && item != null)
				m_FrameDataViewField = item.GetType().GetField("m_FrameDataView", (BindingFlags) ~0);
			if (item != null && (m_frameDataView == null || !m_frameDataView.valid))
				m_frameDataView = m_FrameDataViewField?.GetValue(item) as HierarchyFrameDataView;
			return m_frameDataView;
		}


		private class Profiler_Toolbar : EditorPatch
		{
			protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
			{
				var t = typeof(UnityEditorInternal.ProfilerDriver).Assembly.GetType("UnityEditorInternal.Profiling.ProfilerFrameDataHierarchyView");
				var m = t.GetMethod("DrawDetailedViewPopup", BindingFlags.Instance | BindingFlags.NonPublic);
				targetMethods.Add(m);

				return Task.CompletedTask;
			}

			private static void Postfix()
			{
				var rect = GUILayoutUtility.GetRect(120f, 120f, 14, 14, EditorStyles.toolbarButton);
				if (EditorGUI.DropdownButton(rect, new GUIContent("Selective Profiler"), FocusType.Keyboard, new GUIStyle(EditorStyles.toolbarDropDown)))
				{
					rect.y += rect.height / 2;
					PopupWindow.Show(rect, new SettingsPopup());
				}
			}
		}


		private class Profiler_SelectionChanged : EditorPatch
		{
			protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
			{
				var t = typeof(UnityEditorInternal.ProfilerDriver).Assembly.GetType("UnityEditorInternal.ProfilerFrameDataTreeView");
				var m = t.GetMethod("SelectionChanged", (BindingFlags) ~0);
				targetMethods.Add(m);
				return Task.CompletedTask;
			}

			private static void Postfix(IList<int> selectedIds)
			{
				if (selectedIds == null || selectedIds.Count <= 0) selectedId = -1;
				else selectedId = selectedIds[0];
			}
		}

		private class Profiler_CellGUI : EditorPatch
		{
			// https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/ProfilerFrameDataTreeView.cs#L647
			protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
			{
				var t = typeof(UnityEditorInternal.ProfilerDriver).Assembly.GetType("UnityEditorInternal.ProfilerFrameDataTreeView");
				var m = t.GetMethod("CellGUI", (BindingFlags) ~0);
				targetMethods.Add(m);
				return Task.CompletedTask;
			}

			private static int lastPatchedInImmediateMode = -1;

			// private static void Prefix(TreeViewItem item)
			// {
			// 	// previousColor = TreeView.DefaultStyles.label.normal.textColor;
			// 	// TreeView.DefaultStyles.label.normal.textColor = Color.gray;
			// }

			// method https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/ProfilerFrameDataTreeView.cs#L676
			// item: https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/ProfilerFrameDataTreeView.cs#L68
			private static void Postfix(object __instance, Rect cellRect, TreeViewItem item, int column)
			{
				// if(ProfilerHelper.IsDeepProfiling) return;
				
				HierarchyFrameDataView frameDataView;
				if (column == 0 && Event.current.type == EventType.Repaint)
				{
					frameDataView = GetFrameDataView(item);
					if (frameDataView == null || !frameDataView.valid) return;
					var profiled = ProfilerHelper.IsProfiled(item, frameDataView);
					if (profiled != HierarchyItem.None)
					{
						Rect GetRect(float size)
						{
							var xOff = -size * .5f;
							var yOff = (cellRect.height - size) * .5f;
							return new Rect(cellRect.x + cellRect.width - size + xOff, cellRect.y + yOff, size, size);
						}

						var rectSize = cellRect.height;
						switch (profiled)
						{
							default:
							case HierarchyItem.Child:
								GUI.DrawTexture(GetRect(rectSize * .5f), Textures.ProfiledChild, ScaleMode.ScaleAndCrop, true, 1, Color.gray, 0, 0);
								break;
							case HierarchyItem.Self:
								GUI.DrawTexture(GetRect(rectSize * .5f), Textures.Profiled, ScaleMode.ScaleAndCrop, true, 1, Color.gray, 0, 0);
								break;
						}
					}
				}

				if (Event.current.type == EventType.MouseDown) return;
				var settings = SelectiveProfilerSettings.instance;
				if (!settings.Enabled) return;

				var button = Event.current.button;
				frameDataView = GetFrameDataView(item);
				if (frameDataView == null || !frameDataView.valid) return;

				if (button == 0 && item.id == selectedId && Settings.ImmediateMode && selectedId != lastPatchedInImmediateMode)
				{
					lastPatchedInImmediateMode = selectedId;
					// var name = frameDataView?.GetItemName(item.id);
					// // TODO: test with standalone profiler
					// if (AccessUtils.TryGetMethodFromName(name, out var methods, false, item.id, frameDataView))
					// {
					// 	// TODO: add immediate profiling again
					// 	// SelectiveProfiler.SelectedForImmediateProfiling(methodInfo);
					// }
				}

				if (button != 1)
				{
					return;
				}

				// TODO: check if application has focus, apparently this also triggers when unity is open in the background?!

				// right click
				if (cellRect.Contains(Event.current.mousePosition))
				{
					if (m_FrameDataViewField != null)
					{
						var menu = new GenericMenu();
						var tree = __instance as TreeView;

						if (!settings.Enabled) return;

						if (menu.GetItemCount() > 0)
							menu.AddSeparator(string.Empty);

						var debugLog = settings.DebugLog;

						var id = item.id;
						var isInjectedParent = id > ProfilerFrameDataView_CustomRowsPatch.parentIdOffset;
						if (isInjectedParent)
							id -= ProfilerFrameDataView_CustomRowsPatch.parentIdOffset;
						var name = frameDataView?.GetItemName(id);

						// use parent id for injected rows
						if (isInjectedParent)
							id = item.parent.id;

						var didFind = false;
						if (AccessUtils.TryGetMethodFromName(name, out var methodsList, false, id, frameDataView))
						{
							foreach (var methodInfo in methodsList)
							{
								if (AccessUtils.AllowPatching(methodInfo, false, debugLog))
								{
									didFind = true;
									AddMenuItem(tree, menu, methodInfo, false);
								}
								else if (SelectiveProfiler.DevelopmentMode)
									menu.AddDisabledItem(new GUIContent(AccessUtils.AllowPatchingResultLastReason));
							}
						}
						

						if (!didFind)//menu.GetItemCount() <= 0)
						{
							// get the injected parent base name
							if (isInjectedParent)
								name = name?.Substring(0, name.IndexOf(ProfilerSamplePatch.TypeSampleNameSeparator));
							// make sure we dont have slashes in path
							else name = name?.Replace("/", " ");
							menu.AddDisabledItem(new GUIContent("Could not find " + name));
						}

						if (AccessUtils.TryGetMethodFromName(name, out methodsList, true, id, frameDataView))
						{
							var allowed = methodsList.Distinct().Where(AllowPatching).ToList();
							// ReSharper disable PossibleMultipleEnumeration
							var count = allowed.Count;

							if (didFind && count > 1)
								menu.AddSeparator(string.Empty);

							bool AllowPatching(MethodInfo _mi)
							{
								return AccessUtils.AllowPatching(_mi, false, debugLog);
							}

							string parentMenu = null;// count < 5 ? string.Empty : "Methods in Children/";
							
							if (count > 1)
							{
								menu.AddSeparator(parentMenu + string.Empty);
								menu.AddItem(new GUIContent(parentMenu + "Enable profiling for " + count + " Methods below"), false,
									() => EnableProfilingFromProfilerWindow(allowed, tree));
								menu.AddItem(new GUIContent(parentMenu + "Disable profiling for " + count + " Methods below"), false,
									() => DisableProfilingFromProfilerWindow(allowed, tree));
							}

							if (count > 0)
							{
								menu.AddSeparator(parentMenu + string.Empty);
								BuildOptimalMenuItems(tree, menu, allowed, parentMenu);
							}
						}

						if (menu.GetItemCount() > 0)
							menu.ShowAsContext();
					}
				}
			}
		}

		private static void BuildOptimalMenuItems(TreeView tree, GenericMenu menu, IEnumerable<MethodInfo> methods, string parent = null)
		{
			if (parent != null && !parent.EndsWith("/")) parent += "/";
			
			// only put items in submenu with more than one method per declaring type
			var lookup = methods.ToLookup(e => e.DeclaringType);
			foreach (var kvp in lookup)
			{
				string GetMenuString(string prefix)
				{
					var str = GetTypeSubmenuName(kvp.Key) + prefix + kvp.Key;
					if (parent != null)
						str = parent + str;
					return str;
				}
				
				var index = 0;
				MethodInfo lastMethod = null;
				foreach (var method in kvp)
				{
					if (index <= 0)
					{
						lastMethod = method;
					}
					else
					{
						if (lastMethod != null)
						{
							const string enableItem = "/Enable | All Methods in ";
							const string disableItem = "/Disable | All Methods in ";
							// menu.AddItem(new GUIContent(GetTypeSubmenuName(kvp.Key) + "/"), true, ()=>{});
							// we have multiple methods
							if (kvp.All(e => SelectiveProfiler.IsProfiling(e)))
							{
								menu.AddDisabledItem(new GUIContent(GetMenuString(enableItem)),
									true);
								menu.AddItem(new GUIContent(GetMenuString(disableItem)),
									true,
									() => DisableProfilingFromProfilerWindow(kvp, tree));
								menu.AddSeparator(GetTypeSubmenuName(kvp.Key) + "/");
							}
							else if (kvp.Any(e => SelectiveProfiler.IsProfiling(e)))
							{
								menu.AddItem(new GUIContent(GetMenuString(enableItem)),
									true,
									() => EnableProfilingFromProfilerWindow(kvp, tree));
								menu.AddItem(new GUIContent(GetMenuString(disableItem)),
									true,
									() => DisableProfilingFromProfilerWindow(kvp, tree));
								menu.AddSeparator(GetTypeSubmenuName(kvp.Key) + "/");
							}
							else
							{
								menu.AddItem(new GUIContent(GetMenuString(enableItem)),
									false,
									() => EnableProfilingFromProfilerWindow(kvp, tree));
								menu.AddDisabledItem(new GUIContent(GetMenuString(disableItem)),
									false);
								menu.AddSeparator(GetTypeSubmenuName(kvp.Key) + "/");
							}

							menu.AddSeparator(parent + GetTypeSubmenuName(kvp.Key));

							AddMenuItem(tree, menu, lastMethod, true, parent);
							lastMethod = null;
						}

						AddMenuItem(tree, menu, method, true, parent);
					}

					++index;
				}

				if (lastMethod != null)
				{
					AddMenuItem(tree, menu, lastMethod, false, parent);
				}
			}
		}

		private const string MenuItemPrefix = "Profile | ";
		private static string GetTypeSubmenuName(Type type) => MenuItemPrefix + type.Name;

		private static void AddMenuItem(TreeView tree, GenericMenu menu, MethodInfo methodInfo, bool addTypeSubmenu, string parent = null)
		{
			var active = SelectiveProfiler.IsProfiling(methodInfo);

			var ret = methodInfo.ReturnType.Name;
			// remove void return types
			if (ret == "Void") ret = string.Empty;
			else ret += " ";

			string GetMethodName(bool @long)
			{
				var methodName = methodInfo.Name;
				if (@long) methodName += "(" + string.Join(",", methodInfo.GetParameters()?.Select(p => p.ParameterType)) + ")";
				else methodName += "(" + string.Join(",", methodInfo.GetParameters()?.Select(p => p.ParameterType.Name)) + ")";
				return methodName;
			}

			const int maxLength = 180;
			// if menu items are too long nothing is displayed anymore

			var label = MenuItemPrefix + TranspilerUtils.GetNiceMethodName(methodInfo, false);

			if (label.Length > maxLength)
				label = $"Profile | {ret}{methodInfo.DeclaringType?.Name}.{GetMethodName(true)}";
			if (label.Length > maxLength)
				label = $"Profile | {ret}{methodInfo.DeclaringType?.Name}.{GetMethodName(false)}";
			if (label.Length > maxLength)
				label = "..." + label.Substring(Mathf.Abs(maxLength - label.Length));

			if (addTypeSubmenu && methodInfo.DeclaringType != null)
				label = GetTypeSubmenuName(methodInfo.DeclaringType) + "/" + label;

			// need to split this into two menu items until we sync state of activated methods between standalone profiler and main process
			// if (SelectiveProfiler.IsStandaloneProcess)
			// {
			// 	menu.AddItem(new GUIContent($"{ret}{methodInfo.DeclaringType?.Name}.{methodName}/Start Deep Profile"), false,
			// 		() => EnableProfilingFromProfilerWindow(methodInfo));
			// 	menu.AddItem(new GUIContent($"{ret}{methodInfo.DeclaringType?.Name}.{methodName}/Stop Deep Profile"), false,
			// 		() => DisableProfilingFromProfilerWindow(methodInfo));
			// }
			// else
			{
				if (parent != null)
				{
					if (!parent.EndsWith("/")) parent += "/";
					label = parent + label;
				}
				menu.AddItem(new GUIContent(label),
					active, () =>
					{
						if (!active) EnableProfilingFromProfilerWindow(methodInfo, tree);
						else DisableProfilingFromProfilerWindow(methodInfo, tree);
					});
			}
		}


		private static void EnableProfilingFromProfilerWindow(IEnumerable<MethodInfo> methods, TreeView tree = null)
		{
			foreach (var method in methods)
			{
				SelectiveProfiler.EnableProfilingAsync(method, SelectiveProfiler.ShouldSave, true, true, true);
			}

			if (tree != null)
				ReloadDelayed(tree);
		}

		private static void EnableProfilingFromProfilerWindow(MethodInfo method, TreeView tree = null)
		{
			// Standalone process always return "false" for playing
			SelectiveProfiler.EnableProfilingAsync(method, SelectiveProfiler.ShouldSave, true, true, true);
			if (tree != null)
				ReloadDelayed(tree);
		}

		private static void DisableProfilingFromProfilerWindow(IEnumerable<MethodInfo> methods, TreeView tree = null)
		{
			foreach (var m in methods)
				SelectiveProfiler.DisableProfiling(m);
			if (tree != null)
				ReloadDelayed(tree);
		}

		private static void DisableProfilingFromProfilerWindow(MethodInfo method, TreeView tree = null)
		{
			SelectiveProfiler.DisableProfiling(method);
			if (tree != null)
				ReloadDelayed(tree);
		}

		private static async void ReloadDelayed(TreeView tree)
		{
			await Task.Delay(100);
			tree?.Reload();
		}
	}
}