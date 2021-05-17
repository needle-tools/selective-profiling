using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using Needle.SelectiveProfiling.Utils;
using UnityEditor;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	internal static class Draw
	{
		public static bool WithHeaderFoldout(string foldoutStateName, string headerName, Action draw, bool defaultFoldoutState = false, int afterWhenOpen = 5)
		{
			var foldout = EditorGUILayout.BeginFoldoutHeaderGroup(SessionState.GetBool(foldoutStateName, defaultFoldoutState), new GUIContent(headerName));
			SessionState.SetBool(foldoutStateName, foldout);
			if (foldout)
			{
				EditorGUI.indentLevel++;
				draw();
				EditorGUI.indentLevel--;
			}

			EditorGUILayout.EndFoldoutHeaderGroup();
			if (foldout) GUILayout.Space(afterWhenOpen);
			return foldout;
		}


		internal static void DefaultSelectiveProfilerUI(SelectiveProfilerSettings settings, bool inFoldouts)
		{
			if (!SelectiveProfiler.ExpectedPatches().All(Patcher.IsActive))
			{
				var notEnabledList = SelectiveProfiler.ExpectedPatches().Where(p => !Patcher.IsActive(p));
				EditorGUILayout.HelpBox(
					"Some patches for Selective Profiler are not enabled. Some functionality might not work as expected or be missing.\n" +
					string.Join(", ", notEnabledList), MessageType.Warning);
				if (GUILayout.Button("Enable patches"))
				{
					foreach (var exp in SelectiveProfiler.ExpectedPatches())
						Patcher.Apply(exp);
				}

				GUILayout.Space(10);
			}

			if (ProfilerHelper.IsDeepProfiling)
			{
				EditorGUILayout.HelpBox("Selective Profiler is not running while in Deep Profile Mode", MessageType.Warning);
			}

			void DrawSettings()
			{
				if (!inFoldouts)
				{
					EditorGUILayout.LabelField("General", EditorStyles.boldLabel);
				}

				settings.Enabled = EditorGUILayout.ToggleLeft(new GUIContent("Enabled", ""), settings.Enabled);
				
				settings.RuntimeSave = EditorGUILayout.ToggleLeft(
					new GUIContent("Runtime Save", "When enabled adding profiled methods during runtime will be saved in the current profiled methods list"),
					settings.RuntimeSave);
				// settings.ImmediateMode =
				// 	EditorGUILayout.ToggleLeft(new GUIContent("Immediate Mode", "Automatically profile selected method in Unity Profiler Window"),
				// 		settings.ImmediateMode);
				
				// settings.SkipProperties = EditorGUILayout.ToggleLeft(
				// 	new GUIContent("Skip Properties", "Patching property getters does fail in some cases and should generally not be necessary"),
				// 	settings.SkipProperties);
				
				settings.UseAlwaysProfile = EditorGUILayout.ToggleLeft(
					new GUIContent("Use [AlwaysProfile]", ""),
					settings.UseAlwaysProfile);
				
				EditorGUI.BeginChangeCheck();
				settings.CollapseNoImpactSamples = EditorGUILayout.ToggleLeft(
					new GUIContent("Focus Mode", ""),
					settings.CollapseNoImpactSamples);
				settings.CollapseProperties = EditorGUILayout.ToggleLeft(
					new GUIContent("Collapse Properties", ""),
					settings.CollapseProperties);
				settings.CollapseHierarchyNesting = EditorGUILayout.ToggleLeft(
					new GUIContent("Collapse Nesting", ""),
					settings.CollapseHierarchyNesting);
				settings.ColorPerformanceImpact = EditorGUILayout.ToggleLeft(
					new GUIContent("Use Colors", ""),
					settings.ColorPerformanceImpact);
				if (EditorGUI.EndChangeCheck())
				{
					ProfilerHelper.RepaintProfiler();
				}
				
				

				// if (SelectiveProfiler.DevelopmentMode)
				// 	settings.AllowPinning = EditorGUILayout.ToggleLeft(new GUIContent("Allow Pinning", "When enabled methods can be pinned in Profiler window"),
				// 		settings.AllowPinning);

				GUILayout.Space(5);
				EditorGUILayout.LabelField("Deep Profiling", EditorStyles.boldLabel);
				settings.DeepProfiling =
					EditorGUILayout.ToggleLeft(
						new GUIContent("Use Deep Profiling",
							"When true all calls within a newly profiled method will be recursively added to be profiled as well"), settings.DeepProfiling);
				settings.MaxDepth =
					EditorGUILayout.IntField(
						new GUIContent("Max Depth", "When deep profiling is enabled this controls how many levels deep we should follow nested method calls"),
						settings.MaxDepth);
				settings.DeepProfileMaxLevel = (Level) EditorGUILayout.EnumFlagsField(new GUIContent("Allowed", ""), settings.DeepProfileMaxLevel);

				if (SelectiveProfiler.DevelopmentMode)
				{
					GUIState.PatchesFoldout = EditorGUILayout.Foldout(GUIState.PatchesFoldout,
						"Selected Methods [Active " + settings.MethodsList.Count + " of " + settings.MethodsCount + "]");
					if (GUIState.PatchesFoldout)
					{
						EditorGUI.indentLevel++;
						Draw.SavedMethods(settings);
						EditorGUI.indentLevel--;
					}
				}
			}

			void DrawProfiledMethods(bool alwaysSaved)
			{
				if (!inFoldouts)
				{
					GUILayout.Space(5);
					EditorGUILayout.LabelField("Profiling State", EditorStyles.boldLabel);
				}

				if (!Application.isPlaying || alwaysSaved)
				{
					ScopesList(settings.MethodsList,
						m => m.Enabled,
						l => SetStateInSettings(l, true),
						l => SetStateInSettings(l, false),
						l =>
						{
							foreach (var m in l) settings.Remove(m);
						}
					);
				}
				else
				{
					void SetState(IList<MethodInformation> list, Action<ProfilingInfo> info)
					{
						foreach (var method in list)
						{
							if (SelectiveProfiler.TryGet(method, out var prof))
							{
								info?.Invoke(prof);
							}
						}
					}

					ScopesList(SelectiveProfiler.PatchedMethodsInfo,
						m => SelectiveProfiler.TryGet(m, out var p) && p.IsActive,
						l => SetState(l, p => p.Enable(true)),
						l => SetState(l, p => p.Disable()),
						l =>
						{
							foreach (var m in l) SelectiveProfiler.DisableAndForget(m);
						}
					);
					
				}
				

				if (settings.MethodsList.Count > 0)
				{
					using (new EditorGUILayout.HorizontalScope())
					{
						if (GUILayout.Button("Save To Group", GUILayout.Height(30)))
						{
							// ReSharper disable once PossibleMultipleEnumeration
							ProfilingGroup.Save(settings.MethodsList);
						}
						if(GUILayout.Button("Load From Group", GUILayout.Height(30)))
						{
							if (ProfilingGroup.TryLoad(out var @group))
							{
								Debug.Log(group);
								group.SetProfiled();
							}
						}
					}
				}
			}


			if (inFoldouts)
				WithHeaderFoldout("Settings", "Settings", DrawSettings, true);
			else
				DrawSettings();

			var header = "Methods";

			// if (settings.HasGroup) header += " [" + settings.GroupName + "]";
			
			if (inFoldouts)
				WithHeaderFoldout("ProfiledMethods", header, () => DrawProfiledMethods(false), true);
			else
				DrawProfiledMethods(true);

		}

		private static readonly Dictionary<string, List<MethodInformation>> scopes = new Dictionary<string, List<MethodInformation>>();
		private static readonly Dictionary<string, ScopeMeta> scopesMeta = new Dictionary<string, ScopeMeta>();

		private class ScopeMeta
		{
			public int Enabled;
			public int Total;

			internal void Add(MethodInformation mi)
			{
				if (mi.Enabled) Enabled += 1;
				Total += 1;
			}
		}

		private static string GetScopeKey(MethodInformation method)
		{
			switch (GUIState.SelectedScope)
			{
				case MethodScopeDisplay.All:
					return "All";
				case MethodScopeDisplay.Assembly:
					return method.ExtractAssemblyName();
				case MethodScopeDisplay.Namespace:
					return method.ExtractNamespace();
				default:
				case MethodScopeDisplay.Type:
					return method.Type;
			}
		}


		private static void SetStateInSettings(IEnumerable<MethodInformation> list, bool state)
		{
			var settings = SelectiveProfilerSettings.instance;
			settings.RegisterUndo((state ? "Enable" : "Disable") + " profiling methods");
			foreach (var e in list) settings.UpdateState(e, state, false);
		}

		private const int MaxDrawCount = 30;
		private static uint maxOffset = 0;

		private static string ScopeFilter
		{
			get => SessionState.GetString("ScopeFilter", string.Empty);
			set => SessionState.SetString("ScopeFilter", value);
		}

		public struct ScopeOptions
		{
			public bool HideToggle;
			public bool BoldScopeHeader;
		}

		public static void ScopesList(
			IEnumerable<MethodInformation> methods,
			Func<MethodInformation, bool> IsEnabled,
			Action<IList<MethodInformation>> Enable = null,
			Action<IList<MethodInformation>> Disable = null,
			Action<IList<MethodInformation>> Remove = null,
			ScopeOptions options = new ScopeOptions()
		)
		{
			void AddToScope(MethodInformation method)
			{
				var scope = GetScopeKey(method);
				if (!scopes.ContainsKey(scope))
				{
					scopes.Add(scope, new List<MethodInformation>());
					scopesMeta.Add(scope, new ScopeMeta());
				}

				scopes[scope].Add(method);
				scopesMeta[scope].Add(method);
			}
			
			GUIState.SelectedScope = (MethodScopeDisplay) EditorGUILayout.EnumPopup("Scope", GUIState.SelectedScope);

			scopes.Clear();
			scopesMeta.Clear();
			if(methods != null)
				foreach (var method in methods) AddToScope(method);

			if (scopes.Count <= 0)
			{
				EditorGUILayout.HelpBox("No methods selected for profiling", MessageType.None);
				return;
			}


			bool GetFoldout(string key) => SessionState.GetBool("SelectiveProfilerScopeFoldout-" + key, GUIState.SelectedScope == MethodScopeDisplay.All);
			void SetFoldout(string key, bool value) => SessionState.SetBool("SelectiveProfilerScopeFoldout-" + key, value);

			foreach (var kvp in scopes)
			{
				var scope = kvp.Key;
				var meta = scopesMeta[scope];
				var headerLabel = scope;
				if (!Application.isPlaying && !options.HideToggle)
					headerLabel += " [" + meta.Enabled + "/" + meta.Total + "]";
				else headerLabel += " [" + meta.Total + "]";

				bool show = false;
				using (new GUILayout.HorizontalScope())
				{
					var headerStyle = !options.HideToggle && meta.Enabled <= 0 ? GUIStyles.BoldFoldoutDisabled : options.BoldScopeHeader ? GUIStyles.BoldFoldout : GUIStyles.Foldout;
					show = EditorGUILayout.Foldout(GetFoldout(scope), headerLabel, true,headerStyle);
					if (Enable != null && GUILayout.Button("Enable", GUILayout.Width(50)))
						Enable(kvp.Value);
					if (Disable != null && GUILayout.Button("Disable", GUILayout.Width(55)))
						Disable(kvp.Value);
					if (Remove != null && GUILayout.Button("x", GUILayout.Width(20)))
					{
						Remove(kvp.Value);
					}
				}

				SetFoldout(scope, show);
				if (show)
				{
					var doIndent = scopes.Count > 1;
					if(doIndent)
						EditorGUI.indentLevel++;
					var list = kvp.Value;
					var canFilter = list.Count > 10;
					if (canFilter)
					{
						GUILayout.Space(5);
						ScopeFilter = EditorGUILayout.TextField("Filter", ScopeFilter);
					}

					var filter = ScopeFilter.ToLowerInvariant();
					canFilter &= !string.IsNullOrWhiteSpace(filter);

					for (var index = 0; index < list.Count; index++)
					{
						var entry = list[index];
						var mi = entry.MethodIdentifier();
						var label = new GUIContent(entry.ClassWithMethod(), mi);

						var curMax = MaxDrawCount + maxOffset;
						const int step = 100;
						if (canFilter)
						{
							if (!mi.ToLowerInvariant().Contains(filter)) continue;
						}
						else if (index > curMax)
						{
							EditorGUILayout.LabelField("and " + (list.Count - (index + 1)) + " methods");
							EditorGUILayout.BeginHorizontal();
							using(new EditorGUI.DisabledScope(maxOffset <= 0))
							{
								if (GUILayout.Button("Less"))
								{
									maxOffset -= step;
									maxOffset = (uint)Mathf.Max(maxOffset, 0);
								}
							}
							if (GUILayout.Button("More"))
							{
								maxOffset += step;
							}
							EditorGUILayout.EndHorizontal();
							break;
						} 

						// TODO: refactor to draw without using Layout or VisualElements

						using (new GUILayout.HorizontalScope())
						{
							if (IsEnabled != null)
							{
								var state = IsEnabled.Invoke(entry);
								var prevColor = GUI.color;
								if (!entry.TryResolveMethod(out var t) || entry.IsMissing)   
								{
									GUI.color = Color.yellow;
									label.tooltip = entry.LoadError + "\n\n" + label.tooltip;
								}
								
								if (options.HideToggle)
								{
									var style = EditorStyles.label;// state ? EditorStyles.label : GUIStyles.DisabledLabel;
									EditorGUILayout.LabelField(label, style, GUILayout.ExpandWidth(true)); 
								}
								else
								{
									var newState = EditorGUILayout.ToggleLeft(label, state, GUIStyles.Label(state), GUILayout.ExpandWidth(true));
									if (newState != state)
									{
										if (newState)
											Enable?.Invoke(new List<MethodInformation>() {entry});
										else
											Disable?.Invoke(new List<MethodInformation>() {entry});
									}
								}

								if (GUILayout.Button("x", GUILayout.Width(20)))
								{
									Remove?.Invoke(new List<MethodInformation>() {entry});
								}

								GUI.color = prevColor;
							}
							else
							{
								EditorGUILayout.LabelField(label);
							}
						}
					}

					if(doIndent)
						EditorGUI.indentLevel--;
					// GUILayout.Space(3);
				}
			}

			// EditorGUI.indentLevel--;
		}

		private static readonly List<MethodInformation> removeList = new List<MethodInformation>();

		internal static void SavedMethods(SelectiveProfilerSettings settings)
		{
			void ApplyRemoveList()
			{
				for (var i = removeList.Count - 1; i >= 0; i--)
				{
					var entry = removeList[i];
					settings.Remove(entry);
				}

				removeList.Clear();
			}

			bool DrawMethods(IReadOnlyList<MethodInformation> list, bool activeList, bool foldout, string label)
			{
				if (list == null) return foldout;
				EditorGUILayout.BeginHorizontal();
				foldout = EditorGUILayout.Foldout(foldout, label + " [" + list.Count + "]");
				GUILayout.FlexibleSpace();
				EditorGUI.BeginDisabledGroup(list.Count <= 0);
				var enableAll = GUILayout.Button("Enable all", GUILayout.Width(80));
				var muteAll = GUILayout.Button("Disable all", GUILayout.Width(80));
				var removeAll = GUILayout.Button("Remove all", GUILayout.Width(80));
				EditorGUI.EndDisabledGroup();
				EditorGUILayout.EndHorizontal();

				if (muteAll || removeAll || enableAll)
				{
					for (var index = list.Count - 1; index >= 0; index--)
					{
						if (removeAll) removeList.Add(list[index]);
						else if (enableAll) settings.SetMuted(list[index], false);
						// ReSharper disable once ConditionIsAlwaysTrueOrFalse
						else if (muteAll) settings.SetMuted(list[index], true);
					}

					ApplyRemoveList();
				}

				if (foldout)
				{
					EditorGUI.indentLevel++;
					for (var index = 0; index < list.Count; index++)
					{
						var m = list[index];
						EditorGUILayout.BeginHorizontal();
						EditorGUI.BeginDisabledGroup(!activeList);
						var muted = !m.Enabled;
						var style = GUIStyles.Label(m.Enabled);
						EditorGUILayout.LabelField(new GUIContent(m.ClassWithMethod(), m.MethodIdentifier()), style, GUILayout.ExpandWidth(true));
						EditorGUI.EndDisabledGroup();
						if (GUILayout.Button(muted ? "Enable" : "Disable", GUILayout.Width(60)))
							settings.SetMuted(m, !muted);
						if (GUILayout.Button("x", GUILayout.Width(20)))
							removeList.Add(m);
						EditorGUILayout.EndHorizontal();
					}

					EditorGUI.indentLevel--;
				}

				ApplyRemoveList();
				return foldout;
			}

			GUIState.MethodsListFoldout = DrawMethods(settings.MethodsList, true, GUIState.MethodsListFoldout, "Saved Methods");
		}

		internal static void DrawProfilerPatchesList()
		{
			if (SelectiveProfiler.Patches == null || SelectiveProfiler.PatchesCount <= 0) return;

			foreach (var p in SelectiveProfiler.Patches)
			{
				EditorGUILayout.BeginHorizontal();
				var active = p.IsActive;
				EditorGUI.BeginDisabledGroup(!active);
				EditorGUILayout.LabelField(new GUIContent(p.Patch.DisplayName, p.Identifier), GUILayout.ExpandWidth(true));
				EditorGUI.EndDisabledGroup();
				if (!active)
				{
					if (GUILayout.Button("Enable", GUILayout.Width(80)))
					{
						p.Enable(true);
					}
				}
				else if (GUILayout.Button("Disable", GUILayout.Width(80)))
				{
					p.Disable();
				}

				EditorGUILayout.EndHorizontal();
			}
		}


		private static string filter
		{
			get => SessionState.GetString(nameof(SelectiveProfilerWindow) + "Filter", string.Empty);
			set => SessionState.SetString(nameof(SelectiveProfilerWindow) + "Filter", value);
		}

		private static readonly List<Match> matches = new List<Match>();
		private static CancellationTokenSource cancelSearch;
		private static Vector2 scrollTypesList;
		private static int allowDrawCount;
		private static int defaultAllowDrawCount = 100;
		internal static bool MethodsExplorerRequestRepaint;

		internal static void MethodsExplorer()
		{
			MethodsExplorer(
				method => SelectiveProfiler.TryGet(method, out _) || SelectiveProfilerSettings.instance.Contains(method),
				method => SelectiveProfiler.EnableProfilingAsync(method, SelectiveProfiler.ShouldSave, true, true),
				method => SelectiveProfiler.DisableAndForget(method)
				);
		}

		internal static void MethodsExplorer(Predicate<MethodInfo> contains, Action<MethodInfo> add, Action<MethodInfo> remove)
		{
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.BeginHorizontal();
			filter = EditorGUILayout.TextField("Filter", filter, GUILayout.ExpandWidth(true));
			if (GUILayout.Button("Refresh", GUILayout.Width(70)))
				MethodsExplorerRequestRepaint = true;
			EditorGUILayout.EndHorizontal();

			if (EditorGUI.EndChangeCheck())
			{
				allowDrawCount = defaultAllowDrawCount;
				matches.Clear();
				cancelSearch?.Cancel();
				cancelSearch = null;
				MethodsExplorerRequestRepaint = false;
				if (filter.Length > 0 && !string.IsNullOrWhiteSpace(filter))
				{
					cancelSearch = new CancellationTokenSource();
					
					TypesExplorer.TryFindMethod(filter, entry =>
					{
						MethodsExplorerRequestRepaint = true;
						matches.Add(new Match()
						{
							Key = entry.FullName,
							Method = entry.Entry
						});
					}, cancelSearch.Token);
				}
			}

			if (matches != null && !MethodsExplorerRequestRepaint && matches.Count > 0)
			{
				if (!string.IsNullOrWhiteSpace(filter))
					EditorGUILayout.LabelField("Matching " + matches.Count + " / " + TypesExplorer.MethodsCount);
				var h = Mathf.Min(matches.Count, 10) * EditorGUIUtility.singleLineHeight * 1.2f;
				scrollTypesList = EditorGUILayout.BeginScrollView(scrollTypesList, GUILayout.MinHeight(h), GUILayout.MaxHeight(Screen.height));
				for (var index = 0; index < matches.Count; index++)
				{
					if (MethodsExplorerRequestRepaint) break;
					var match = matches[index];
					EditorGUILayout.BeginHorizontal();
					EditorGUILayout.LabelField(new GUIContent(match.Key, match.Method.FullDescription()), GUILayout.ExpandWidth(true));

					if (remove != null && (contains?.Invoke(match.Method) ?? false))
					{
						if (GUILayout.Button("Remove", GUILayout.Width(70)))
						{
							remove(match.Method);
						}
					}
					else if(add != null)
					{
						if (GUILayout.Button("Add", GUILayout.Width(70)))
						{
							add(match.Method);
						}
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