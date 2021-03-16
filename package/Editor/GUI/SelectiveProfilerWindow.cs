using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEditor;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	public class SelectiveProfilerWindow : EditorWindow
	{
		[MenuItem(MenuItems.Menu + nameof(Open))]
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
		}

		private Vector2 scroll, scrollPatches;
		
		private void OnGUI()
		{
			var settings = SelectiveProfilerSettings.instance;
			scroll = EditorGUILayout.BeginScrollView(scroll);
			
			EditorGUILayout.Space(10);
			Draw.WithHeaderFoldout("SelectedMethodsFoldout", "Selected Methods", () =>
			{
				Draw.SavedMethods(settings);
				Draw.ScopesList(settings);
			});
			
			Draw.WithHeaderFoldout("PatchesFoldout", "Patches", () =>
			{
				var patches = SelectiveProfiler.Patches;
				if (patches == null || SelectiveProfiler.PatchesCount <= 0)
				{
					EditorGUILayout.LabelField("No active patched methods");
				}
				else
				{
					scrollPatches = EditorGUILayout.BeginScrollView(scrollPatches);
					Draw.DrawProfilerPatchesList();
					EditorGUILayout.EndScrollView();
				}
			});
			
			if (SelectiveProfiler.DeepProfileDebuggingMode)
			{
				Draw.WithHeaderFoldout("DebugOptionsFoldout", "Debug Options", () =>
				{
					DrawDeepProfilingDebug();
					EditorGUILayout.Space(10);
					DrawTypesExplorer(this);
				});
			}

			EditorGUILayout.Space(10);
			EditorGUILayout.EndScrollView();
		}

		private static void DrawDeepProfilingDebug()
		{
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
		}

		private static string filter;
		private static readonly List<MethodInfo> matches = new List<MethodInfo>();

		private class SearchUpdated : IProgress<MethodInfo>
		{
			private EditorWindow window;
			
			public SearchUpdated(EditorWindow window)
			{
				this.window = window;
			}
			
			public void Report(MethodInfo value)
			{
				Debug.Log(value);
				window.Repaint();
			}
		}
		
		private static void DrawTypesExplorer(EditorWindow window)
		{
			EditorGUI.BeginChangeCheck();
			filter = EditorGUILayout.TextField("Filter", filter);
			
			if (EditorGUI.EndChangeCheck())
			{
				matches.Clear();
				TypesExplorer.TryFindMethodAsync(filter, matches, new SearchUpdated(window));
			}
			
			if (matches != null)
			{
				foreach (var t in matches)
				{
					EditorGUILayout.LabelField(t.FullDescription());
				}
			}
		}

	}
}