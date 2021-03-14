﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using needle.EditorPatching;
using Needle.SelectiveProfiling.Utils;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.Profiling;
using UnityEngine;

// ReSharper disable UnusedType.Global

namespace Needle.SelectiveProfiling
{
	public class ProfilerFrameDataView_Patch : EditorPatchProvider
	{
		protected override void OnGetPatches(List<EditorPatch> patches)
		{
			patches.Add(new Patch());
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

			// method https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/ProfilerFrameDataTreeView.cs#L676
			// item: https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/ProfilerFrameDataTreeView.cs#L68
			private static void Postfix(Rect cellRect, TreeViewItem item, ref object args)
			{
				if (Event.current.type == EventType.MouseDown) return;
				var button = Event.current.button;
				if (button != 1) return; // right

				if (cellRect.Contains(Event.current.mousePosition))
				{
					if (m_FrameDataViewField == null)
					{
						m_FrameDataViewField = item.GetType().GetField("m_FrameDataView", (BindingFlags) ~0);
					}

					if (m_FrameDataViewField != null)
					{
						if(frameDataView == null || !frameDataView.valid)
							frameDataView = m_FrameDataViewField?.GetValue(item) as HierarchyFrameDataView;
						
						var name = frameDataView?.GetItemName(item.id);
						if (AccessUtils.TryGetMethodFromName(name, out var methodInfo))
						{
							var menu = new GenericMenu();
							var active = SelectiveProfiler.IsProfiling(methodInfo);
							menu.AddItem(new GUIContent($"{(active ? "Disable" : "Enable")} Deep Profiling {methodInfo}"), active, () =>
							{
								Debug.Log(methodInfo.DeclaringType + " - " + methodInfo);
								if(!active)
									SelectiveProfiler.EnableProfiling(methodInfo);
								else SelectiveProfiler.DisableProfiling(methodInfo);
							});
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