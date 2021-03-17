using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
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
			EditorApplication.update += OnUpdate;
		}
		
		private void OnDisable()
		{
			EditorApplication.update -= OnUpdate;
		}

		private void OnUpdate()
		{
			if (requestRepaint)
			{
				requestRepaint = false;
				Repaint();
			}
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
			
			if (SelectiveProfiler.DevelopmentMode)
			{
				Draw.WithHeaderFoldout("DebugOptionsFoldout", "Debug Options", () =>
				{
					DrawDeepProfilingDebug();
					EditorGUILayout.Space(10);
					DrawTypesExplorer();
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
		private static readonly List<Match> matches = new List<Match>();
		private static CancellationTokenSource cancelSearch;
		private static bool requestRepaint;
		
		private static void DrawTypesExplorer()
		{
			EditorGUI.BeginChangeCheck();
			filter = EditorGUILayout.TextField("Filter", filter);
			
			if (EditorGUI.EndChangeCheck())
			{
				matches.Clear();
				cancelSearch?.Cancel();
				cancelSearch = null;
				requestRepaint = false;
				if (filter.Length > 1 &&!string.IsNullOrWhiteSpace(filter))
				{
					cancelSearch = new CancellationTokenSource();
					TypesExplorer.TryFindMethodAsync(filter,  entry =>
					{
						requestRepaint = true;
						matches.Add(new Match()
						{
							Key = entry.FullName,
							Method = entry.Entry
						});

					}, cancelSearch.Token);
				}
			}
			
			if (matches != null && !requestRepaint)
			{
				if(!string.IsNullOrWhiteSpace(filter))
					EditorGUILayout.LabelField(matches.Count + " matches");
				for (var index = 0; index < matches.Count; index++)
				{
					if (requestRepaint) break;
					var t = matches[index];
					EditorGUILayout.LabelField(t.Key);
					if (index > 100)
					{
						EditorGUILayout.LabelField("Truncated: " + matches.Count);
						break;
					}
				}
			}
		}

	}
}