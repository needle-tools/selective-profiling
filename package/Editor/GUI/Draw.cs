using System;
using System.Collections.Generic;
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

		
		private static readonly Dictionary<string, List<MethodInformation>> scopes = new Dictionary<string, List<MethodInformation>>();

		public static void ScopesList(SelectiveProfilerSettings settings)
		{
			GUIState.ScopesListFoldout = EditorGUILayout.Foldout(GUIState.ScopesListFoldout, "Scopes");
			if (GUIState.ScopesListFoldout)
			{
				string GetScopeKey(MethodInformation method)
				{
					switch (GUIState.SelectedScope)
					{
						case MethodScopeDisplay.Assembly:
							return method.ExtractAssemblyName();
						case MethodScopeDisplay.Namespace:
							return method.ExtractNamespace();
						default:
						case MethodScopeDisplay.Type:
							return method.Type;
					}
				}

				void AddToScope(MethodInformation method)
				{
					var scope = GetScopeKey(method);
					if (!scopes.ContainsKey(scope))
						scopes.Add(scope, new List<MethodInformation>());
					scopes[scope].Add(method);
				}

				EditorGUI.indentLevel++;
				GUIState.SelectedScope = (MethodScopeDisplay) EditorGUILayout.EnumPopup("Scope", GUIState.SelectedScope);

				scopes.Clear();
				foreach (var method in settings.MethodsList) AddToScope(method);
				// foreach (var method in settings.MutedMethods) AddToScope(method);

				bool GetFoldout(string key) => SessionState.GetBool("SelectiveProfilerScopeFoldout-" + key, false);
				void SetFoldout(string key, bool value) => SessionState.SetBool("SelectiveProfilerScopeFoldout-" + key, value);

				foreach (var kvp in scopes)
				{
					var scope = kvp.Key;
					EditorGUILayout.BeginHorizontal();
					var show = EditorGUILayout.Foldout(GetFoldout(scope), scope,  GUIStyles.BoldFoldout);
					if (GUILayout.Button("Enable all", GUILayout.Width(80)))
						SetState(kvp.Value, true, scope);
					if (GUILayout.Button("Disable all", GUILayout.Width(80)))
						SetState(kvp.Value, false, scope);
					if (GUILayout.Button("Remove all", GUILayout.Width(80)))
					{
						settings.RegisterUndo("Remove all in " + scope);
						foreach (var entry in kvp.Value)
							settings.Remove(entry, false);
					}
					EditorGUILayout.EndHorizontal();
					SetFoldout(scope, show);
					if (show)
					{
						EditorGUI.indentLevel++;
						var list = kvp.Value;
						foreach (var entry in list)
						{
							EditorGUILayout.BeginHorizontal();
							var state = EditorGUILayout.ToggleLeft(new GUIContent(entry.ClassWithMethod(), entry.MethodIdentifier()), entry.Enabled,
								GUIStyles.Label(entry.Enabled), GUILayout.ExpandWidth(true));
							if (state != entry.Enabled) settings.UpdateState(entry, state, true);
							EditorGUILayout.EndHorizontal();
						}

						EditorGUI.indentLevel--;
					}

					GUILayout.Space(5);
				}

				EditorGUI.indentLevel--;

				void SetState(IEnumerable<MethodInformation> list, bool state, string scope)
				{
					settings.RegisterUndo((state ? "Enable" : "Disable") + " " + scope);
					foreach (var e in list) settings.UpdateState(e, state, false);
				}
			}
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

			GUIState.MethodsListFoldout = DrawMethods(settings.MethodsList, true, GUIState.MethodsListFoldout, "Methods");
			// GUIState.MutedMethodsFoldout = DrawMethods(settings.MutedMethods, false, GUIState.MutedMethodsFoldout, "Muted");
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
	}
}