using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using needle.EditorPatching;
using Needle.SelectiveProfiling.Utils;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.MPE;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedType.Global

namespace Needle.SelectiveProfiling
{
	public class ProfilerFrameDataView_Patch : EditorPatchProvider
	{
		protected override void OnGetPatches(List<EditorPatch> patches)
		{
			if (!SelectiveProfiler.AllowToBeEnabled) return;
			patches.Add(new Profiler_SelectionChanged());
			patches.Add(new Profiler_CellGUI());
		}

		public override void OnEnabledPatch()
		{
			base.OnEnabledPatch();
			PatchManager.EnablePatch(typeof(ProfilerPinning.ProfilerPinning_Patch));
		}

		public override void OnDisabledPatch()
		{
			base.OnDisabledPatch();
			PatchManager.DisablePatch(typeof(ProfilerPinning.ProfilerPinning_Patch));
		}

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
			if (m_FrameDataViewField == null)
				m_FrameDataViewField = item.GetType().GetField("m_FrameDataView", (BindingFlags) ~0);
			if (m_frameDataView == null || !m_frameDataView.valid)
				m_frameDataView = m_FrameDataViewField?.GetValue(item) as HierarchyFrameDataView;
			return m_frameDataView;
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
			private static void Postfix(object __instance, Rect cellRect, TreeViewItem item)
			{
				if (Event.current.type == EventType.MouseDown) return;
				var settings = SelectiveProfilerSettings.instance;
				if (!settings.Enabled) return;

				var button = Event.current.button;

				var frameDataView = GetFrameDataView(item);

				if (button == 0 && item.id == selectedId && Settings.ImmediateMode && selectedId != lastPatchedInImmediateMode)
				{
					lastPatchedInImmediateMode = selectedId;
					var name = frameDataView?.GetItemName(item.id);
					// TODO: test with standalone profiler
					if (AccessUtils.TryGetMethodFromName(name, out var methodInfo))
						SelectiveProfiler.SelectedForImmediateProfiling(methodInfo);
				}

				if (button != 1)
				{
					return;
				}

				// right click
				if (cellRect.Contains(Event.current.mousePosition))
				{
					if (m_FrameDataViewField != null)
					{
						var menu = new GenericMenu();

						if (!ProfilerPinning.IsPinned(item.id))
						{
							menu.AddItem(new GUIContent("Pin"), false, () =>
							{
								ProfilerPinning.Pin(item);
								if (__instance is TreeView tv) tv.Reload();
							});
						}
						else if(!ProfilerPinning.IsChildOfAnyPinnedItem(item, false))
						{
							menu.AddItem(new GUIContent("Unpin"), true, () =>
							{
								ProfilerPinning.Unpin(item);
								if (__instance is TreeView tv) tv.Reload();
							});
						}
						
						if(menu.GetItemCount() > 0)
							menu.AddSeparator(string.Empty);

						if (!settings.Enabled) return;
						var name = frameDataView?.GetItemName(item.id);
						if (AccessUtils.TryGetMethodFromName(name, out var methodInfo))
						{
							AddMenuItem(menu, methodInfo);
							menu.ShowAsContext();
						}
						else if (ProfilerHelper.TryGetMethodsInChildren(item.id, frameDataView, out var methodsFound))
						{
							var availableMethods =
								methodsFound.Where(e => AccessUtils.AllowPatching(e, false, false)).ToList();

							menu.AddItem(new GUIContent("Enable profiling for all"), false, () => EnableProfilingFromProfilerWindow(availableMethods));
							menu.AddItem(new GUIContent("Disable profiling for all"), false, () => DisableProfilingFromProfilerWindow(availableMethods));
							menu.AddSeparator(string.Empty);

							foreach (var m in availableMethods)
							{
								AddMenuItem(menu, m);
							}
						}
						else
						{
							menu.AddDisabledItem(new GUIContent("Nothing to profile in " + name));
						}

						menu.ShowAsContext();
					}
				}
			}

			// TODO: figure out how to use https://docs.unity3d.com/ScriptReference/Profiling.FrameDataView.ResolveMethodInfo.html
			// https://docs.unity3d.com/ScriptReference/Profiling.HierarchyFrameDataView.GetItemCallstack.html
			// https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/ProfilerModules/CPUorGPUProfilerModule.cs#L194
		}

		private static void AddMenuItem(GenericMenu menu, MethodInfo methodInfo)
		{
			var active = SelectiveProfiler.IsProfiling(methodInfo);
			var ret = methodInfo.ReturnType.Name;
			// remove void return types
			if (ret == "Void") ret = string.Empty;
			else ret += " ";
			var methodName = methodInfo.Name + "(" + string.Join(",", methodInfo.GetParameters()?.Select(p => p.ParameterType)) + ")";

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
				menu.AddItem(new GUIContent($"Profile | {ret}{methodInfo.DeclaringType?.Name}.{methodName}"),
					active, () =>
					{
						if (!active) EnableProfilingFromProfilerWindow(methodInfo);
						else DisableProfilingFromProfilerWindow(methodInfo);
					});
			}
		}


		private static void EnableProfilingFromProfilerWindow(IEnumerable<MethodInfo> methods)
		{
			foreach (var m in methods)
				EnableProfilingFromProfilerWindow(m);
		}

		private static void EnableProfilingFromProfilerWindow(MethodInfo method)
		{
			// Standalone process always return "false" for playing
			SelectiveProfiler.EnableProfiling(method, SelectiveProfiler.ShouldSave, true, true, true);
		}

		private static void DisableProfilingFromProfilerWindow(IEnumerable<MethodInfo> methods)
		{
			foreach (var m in methods)
				SelectiveProfiler.DisableProfiling(m);
		}

		private static void DisableProfilingFromProfilerWindow(MethodInfo method)
		{
			SelectiveProfiler.DisableProfiling(method);
		}
	}
}