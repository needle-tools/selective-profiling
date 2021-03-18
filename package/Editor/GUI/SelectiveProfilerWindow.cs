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
			TypesExplorer.AllTypesLoaded += Repaint;
		}
		
		private void OnDisable()
		{
			EditorApplication.update -= OnUpdate;
		}

		private void OnUpdate()
		{
			if (!requestRepaint) return;
			requestRepaint = false;
			Repaint();
		}

		private Vector2 scroll, scrollPatches;
		
		private void OnGUI()
		{
			var settings = SelectiveProfilerSettings.instance;
			scroll = EditorGUILayout.BeginScrollView(scroll);
			
			EditorGUILayout.Space(10);
			Draw.WithHeaderFoldout("SelectedMethodsFoldout", "Profiled Methods", () =>
			{
				Draw.ScopesList(settings);
				if(SelectiveProfiler.DevelopmentMode)
					Draw.SavedMethods(settings);
			});

			Draw.WithHeaderFoldout("TypesExplorer", "Types Explorer", () =>
			{
				EditorGUILayout.LabelField("test");
				DrawTypesExplorer();
			});
			
			
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

			EditorGUILayout.Space(10);
			EditorGUILayout.EndScrollView();
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

		private static string filter
		{
			get => SessionState.GetString(nameof(SelectiveProfilerWindow) + "Filter", string.Empty);
			set => SessionState.SetString(nameof(SelectiveProfilerWindow) + "Filter", value);
		}
		private static readonly List<Match> matches = new List<Match>();
		private static CancellationTokenSource cancelSearch;
		private static bool requestRepaint;
		private static Vector2 scrollTypesList;
		private static int allowDrawCount;
		private static int defaultAllowDrawCount = 100;
		
		private static void DrawTypesExplorer()
		{
			EditorGUI.BeginChangeCheck();;
			EditorGUILayout.BeginHorizontal();
			filter = EditorGUILayout.TextField("Filter", filter, GUILayout.ExpandWidth(true));
			if (GUILayout.Button("Refresh", GUILayout.Width(70))) 
				requestRepaint = true;
			EditorGUILayout.EndHorizontal();

			if (EditorGUI.EndChangeCheck())
			{
				allowDrawCount = defaultAllowDrawCount;
				matches.Clear();
				cancelSearch?.Cancel();
				cancelSearch = null;
				requestRepaint = false;
				if (filter.Length > 0 && !string.IsNullOrWhiteSpace(filter))
				{
					cancelSearch = new CancellationTokenSource();
					TypesExplorer.TryFindMethod(filter, entry =>
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

			if (matches != null && !requestRepaint && matches.Count > 0)
			{
				if (!string.IsNullOrWhiteSpace(filter))
					EditorGUILayout.LabelField("Matching " + matches.Count + " / " + TypesExplorer.MethodsCount);
				var h = Mathf.Min(matches.Count, 10) * EditorGUIUtility.singleLineHeight * 1.2f;
				scrollTypesList = EditorGUILayout.BeginScrollView(scrollTypesList, GUILayout.MinHeight(h));
				for (var index = 0; index < matches.Count; index++)
				{
					if (requestRepaint) break;
					var match = matches[index];
					EditorGUILayout.BeginHorizontal();
					EditorGUILayout.LabelField(new GUIContent(match.Key, match.Method.FullDescription()), GUILayout.ExpandWidth(true));
					if (GUILayout.Button("Add", GUILayout.Width(50)))
					{
						SelectiveProfilerSettings.instance.Add(new MethodInformation(match.Method));
						// SelectiveProfiler.EnableProfiling(match.Method);
					}

					EditorGUILayout.EndHorizontal();
					if (index > allowDrawCount)
					{
						EditorGUILayout.LabelField("Truncated: " + matches.Count + ". Max: " + allowDrawCount, GUILayout.ExpandWidth(true));
						EditorGUILayout.BeginHorizontal();
						GUILayout.Space(18);
						if (GUILayout.Button("Show More", GUILayout.Height(30)))
							allowDrawCount *= 2;
						EditorGUILayout.EndHorizontal();
						break;
					}
				}

				EditorGUILayout.EndScrollView();
			}
		}

	}
}