using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using needle.EditorPatching;
using UnityEditor;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	public class SelectiveProfilerWindow : EditorWindow
	{
		[MenuItem(MenuItems.ToolsMenu + "Open Selective Profiler Window", false, 0)]
		[MenuItem(MenuItems.WindowsMenu + "Selective Profiler", false, 50000)]
		public static void Open()
		{
			if(HasOpenInstances<SelectiveProfilerWindow>())
				FocusWindowIfItsOpen<SelectiveProfilerWindow>();
			else
			{
				var window = CreateInstance<SelectiveProfilerWindow>();
				window.Show();
			}
		}

		private void OnEnable()
		{
			titleContent = new GUIContent("Selective Profiling");
			EditorApplication.update += OnUpdate;
			TypesExplorer.AllTypesLoaded += OnTypesExplorerOnAllTypesLoaded; 
		}

		private void OnDisable()
		{
			EditorApplication.update -= OnUpdate;
		}

		private void OnUpdate()
		{
			if (!Draw.MethodsExplorerRequestRepaint) return;
			Draw.MethodsExplorerRequestRepaint = false;
			Repaint();
		}

		private void OnTypesExplorerOnAllTypesLoaded()
		{
			TypesExplorer.AllTypesLoaded -= OnTypesExplorerOnAllTypesLoaded;
			Repaint();
		}

		private Vector2 scroll, scrollPatches;
		
		private void OnGUI()
		{
			var settings = SelectiveProfilerSettings.instance;
			scroll = EditorGUILayout.BeginScrollView(scroll);
			
			EditorGUILayout.Space(10);
			
			Draw.DefaultSelectiveProfilerUI(settings, true);
			
			// Draw.WithHeaderFoldout("SelectedMethodsFoldout", "Profiled Methods", () =>
			// {
			// 	Draw.ScopesList(settings);
			// 	if(SelectiveProfiler.DevelopmentMode)
			// 		Draw.SavedMethods(settings);
			// }, true);

			Draw.WithHeaderFoldout("TypesExplorer", "Methods Explorer", Draw.MethodsExplorer, true);
			
			if (SelectiveProfiler.DevelopmentMode)
			{
				Draw.WithHeaderFoldout("PatchesFoldout", "Patches", () =>
				{
					var patches = SelectiveProfiler.Patches;
					if (patches == null || SelectiveProfiler.PatchesCount <= 0)
					{
						EditorGUILayout.LabelField("No active patched methods");
					}
					else
					{
						var h = Mathf.Min(10, SelectiveProfiler.PatchesCount) * EditorGUIUtility.singleLineHeight * 1.2f;
						scrollPatches = EditorGUILayout.BeginScrollView(scrollPatches, GUILayout.MinHeight(h), GUILayout.MaxHeight(Screen.height), GUILayout.ExpandHeight(true));
						Draw.DrawProfilerPatchesList();
						EditorGUILayout.EndScrollView();
					}
				});

				Draw.WithHeaderFoldout("DebugOptionsFoldout", "Debug Options", () =>
				{
					DrawDeepProfilingDebug(settings);
					EditorGUILayout.Space(10);
				});
			}

			if (settings.AllowPinning)
			{
				Draw.WithHeaderFoldout("Pinned", "Pinned", () =>
				{
					foreach (var item in settings.PinnedMethods)
						EditorGUILayout.LabelField(item);
				});
			}
			
			// Draw.WithHeaderFoldout("Unpinned", "Unpinned", () =>
			// {
			// 	foreach (var item in settings.UnpinnedMethods)
			// 		EditorGUILayout.LabelField(item);
			// });

			EditorGUILayout.Space(10);
			EditorGUILayout.EndScrollView();
			


			if (GUILayout.Button("Clear Pinned Items"))
			{
				PinnedItems.ClearPinnedItems();
				PinnedItems.Save();
			}
		}

		private static void DrawDeepProfilingDebug(SelectiveProfilerSettings settings)
		{
			EditorGUI.BeginDisabledGroup(!settings.DeepProfiling);
			EditorGUILayout.LabelField("Deep Profiling", EditorStyles.boldLabel);
			SelectiveProfiler.DeepProfileDebuggingMode = EditorGUILayout.Toggle("Use Stepping", SelectiveProfiler.DeepProfileDebuggingMode);
			// GUILayout.BeginHorizontal();
			SelectiveProfiler.stepDeepProfileToIndex = EditorGUILayout.IntField("Step to", SelectiveProfiler.stepDeepProfileToIndex, GUILayout.ExpandWidth(false));
			EditorGUILayout.LabelField("Current Index:", SelectiveProfiler.deepProfileStepIndex.ToString());
			// GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal();
			GUILayout.Space(18);
			if (GUILayout.Button("Make Step", GUILayout.ExpandWidth(false))) 
				SelectiveProfiler.stepDeepProfile = true;
			GUILayout.EndHorizontal();
			EditorGUI.EndDisabledGroup();
		}

	}
}