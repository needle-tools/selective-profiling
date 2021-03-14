using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using needle.EditorPatching;
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
			patches.Add(new ProfilerHierarchyViewPatch());
			patches.Add(new Patch());
		}

		private static object ProfilerHierarchyViewInstance;
		private static object DetailedObjectsView, ObjectsTreeView;

		private static MethodInfo DetailedObjectsViewInitIfNeeded,
			DetailedObjectsViewUpdateIfNeeded,
			GetSelectedFrameDataViewId,
			GetSelectedFrameDataViewMergedSampleIndex;

		private class ProfilerHierarchyViewPatch : EditorPatch
		{
			protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
			{
				var t = typeof(UnityEditorInternal.ProfilerDriver).Assembly.GetType("UnityEditorInternal.Profiling.ProfilerFrameDataHierarchyView");
				var m = t.GetMethod("DoGUI", (BindingFlags) ~0);
				targetMethods.Add(m);
				return Task.CompletedTask;
			}

			private static void Prefix(object __instance)
			{
				if (ProfilerHierarchyViewInstance != null) return;
				ProfilerHierarchyViewInstance = __instance;
				Debug.Assert(ProfilerHierarchyViewInstance != null);
				DetailedObjectsView = ProfilerHierarchyViewInstance?.GetType()
					?.GetField("m_DetailedObjectsView", (BindingFlags) ~0)
					?.GetValue(ProfilerHierarchyViewInstance);
				Debug.Assert(DetailedObjectsView != null);
				DetailedObjectsViewInitIfNeeded = DetailedObjectsView.GetType().GetMethod("InitIfNeeded", (BindingFlags) ~0);
				Debug.Assert(DetailedObjectsViewInitIfNeeded != null);
				DetailedObjectsViewUpdateIfNeeded = DetailedObjectsView.GetType().GetMethod("UpdateIfNeeded", (BindingFlags) ~0);
				Debug.Assert(DetailedObjectsViewUpdateIfNeeded != null);
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
						frameDataView = m_FrameDataViewField?.GetValue(item) as HierarchyFrameDataView;
						
						var name = frameDataView?.GetItemName(item.id);
						if (TryGetMethodFromName(name, out var methodInfo))
						{
							var menu = new GenericMenu();
							menu.AddItem(new GUIContent("Deep Profile: " + methodInfo), false, () =>
							{
								Debug.Log(methodInfo.DeclaringType + " - " + methodInfo);
								SelectiveProfiler.Profile(methodInfo);
							});
							menu.ShowAsContext();
						}
						else Debug.Log("Did not find type for " + name);
					}
				}
			}


			private static readonly Dictionary<string, Assembly> assemblyMap = new Dictionary<string, Assembly>();

			// TODO: figure out how to use https://docs.unity3d.com/ScriptReference/Profiling.FrameDataView.ResolveMethodInfo.html
			// https://docs.unity3d.com/ScriptReference/Profiling.HierarchyFrameDataView.GetItemCallstack.html
			// https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/ProfilerModules/CPUorGPUProfilerModule.cs#L194
			private static bool TryGetMethodFromName(string name, out MethodInfo method)
			{
				if (!string.IsNullOrEmpty(name))
				{
					Assembly GetAssembly()
					{
						if (assemblyMap.ContainsKey(name)) return assemblyMap[name];

						var dllIndex = name.IndexOf(".dll", StringComparison.InvariantCulture);
						if (dllIndex > 0)
						{
							var assemblyName = name.Substring(0, dllIndex) + ",";
							var assemblies = AppDomain.CurrentDomain.GetAssemblies();
							foreach (var ass in assemblies)
							{
								if (ass.FullName.StartsWith(assemblyName))
								{
									assemblyMap.Add(name, ass);
									return ass;
								}
							}
						}

						return null;
					}

					var assembly = GetAssembly();
					if (assembly != null)
					{
						Debug.Log(assembly);

						const string separator = "!";
						var methodNameIndex = name.IndexOf(separator, StringComparison.InvariantCulture);
						var fullName = name.Substring(methodNameIndex + separator.Length);
						fullName = fullName.Substring(0, fullName.IndexOf("(", StringComparison.InvariantCulture));
						fullName = fullName.Replace("::", ".");
						Debug.Log(fullName);
						var separatorIndex = fullName.LastIndexOf(".", StringComparison.InvariantCulture);
						var typeName = fullName.Substring(0, separatorIndex);
						var methodName = fullName.Substring(separatorIndex + 1);
						Debug.Log(typeName);
						var type = assembly.GetType(typeName);
						Debug.Log(type);
						method = type.GetMethod(methodName, (BindingFlags) ~0);
						// Debug.Log(method);
						return method != null;
					}
				}

				method = null;
				return false;
			}
		}
	}
}