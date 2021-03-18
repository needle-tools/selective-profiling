using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using needle.EditorPatching;
using Needle.SelectiveProfiling.Utils;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.Profiling;
using UnityEngine;
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedType.Global

namespace Needle.SelectiveProfiling
{
	public class ProfilerFrameDataView_Patch : EditorPatchProvider
	{
		protected override void OnGetPatches(List<EditorPatch> patches)
		{
			patches.Add(new SelectionPatch());
			patches.Add(new Patch());
		}

		private static int selectedId = -1;

		private static SelectiveProfilerSettings _settings;
		private static SelectiveProfilerSettings Settings
		{
			get
			{
				if (!_settings) _settings = SelectiveProfilerSettings.instance;
				return _settings;
			}
		}

		private class SelectionPatch : EditorPatch
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


		private class Patch : EditorPatch
		{
			// https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/ProfilerFrameDataTreeView.cs#L647
			protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
			{
				var t = typeof(UnityEditorInternal.ProfilerDriver).Assembly.GetType("UnityEditorInternal.ProfilerFrameDataTreeView");
				var m = t.GetMethod("CellGUI", (BindingFlags) ~0);
				targetMethods.Add(m);
				return Task.CompletedTask; 
			}

			private static FieldInfo m_FrameDataViewField;
			private static HierarchyFrameDataView frameDataView;
			
			private static int lastPatchedInImmediateMode = -1;

			// method https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/ProfilerFrameDataTreeView.cs#L676
			// item: https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/ProfilerFrameDataTreeView.cs#L68
			private static void Postfix(Rect cellRect, TreeViewItem item)
			{
				if (Event.current.type == EventType.MouseDown) return;
				var settings = SelectiveProfilerSettings.instance;
				if (!settings.Enabled) return;
				
				var button = Event.current.button;
				
				if (m_FrameDataViewField == null) 
					m_FrameDataViewField = item.GetType().GetField("m_FrameDataView", (BindingFlags) ~0);

				if(frameDataView == null || !frameDataView.valid)
					frameDataView = m_FrameDataViewField?.GetValue(item) as HierarchyFrameDataView;

				if (button == 0 && item.id == selectedId && Settings.ImmediateMode && selectedId != lastPatchedInImmediateMode)
				{
					lastPatchedInImmediateMode = selectedId;
					var name = frameDataView?.GetItemName(item.id);
					if (AccessUtils.TryGetMethodFromName(name, out var methodInfo)) 
						SelectiveProfiler.AddToAutoProfiling(methodInfo);
				}
				
				if (button != 1) return; // right

				
				if (cellRect.Contains(Event.current.mousePosition))
				{
					if (m_FrameDataViewField != null)
					{
						var name = frameDataView?.GetItemName(item.id);
						if (AccessUtils.TryGetMethodFromName(name, out var methodInfo))
						{
							var menu = new GenericMenu();
							if (settings.Enabled)
							{
								var active = SelectiveProfiler.IsProfiling(methodInfo);
								menu.AddItem(new GUIContent($"{(active ? "Remove" : "Add")} Profiling to \"{methodInfo.DeclaringType?.Name}.{methodInfo}\""), active, () =>
								{
									if (!active)
									{
										SelectiveProfiler.EnableProfiling(methodInfo, true, true, true);
									}
									else
									{
										SelectiveProfiler.DisableProfiling(methodInfo);
									}
								});
							}
							else
							{
								
							}
							menu.ShowAsContext();
						}
						else Debug.Log("Did not find type for " + name);
					}
				}
			}
			
			// TODO: figure out how to use https://docs.unity3d.com/ScriptReference/Profiling.FrameDataView.ResolveMethodInfo.html
			// https://docs.unity3d.com/ScriptReference/Profiling.HierarchyFrameDataView.GetItemCallstack.html
			// https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/ProfilerModules/CPUorGPUProfilerModule.cs#L194
			
		}
	}
}