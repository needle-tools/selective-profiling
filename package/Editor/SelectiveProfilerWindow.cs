using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

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

		[RuntimeInitializeOnLoadMethod]
		[InitializeOnLoadMethod]
		private static void Init()
		{
			Selection.selectionChanged -= OnSelectionChanged;
			Selection.selectionChanged += OnSelectionChanged;
		}

		private static void OnSelectionChanged()
		{
		}

		private void OnEnable()
		{
			titleContent = new GUIContent("Selective Profiling");
		}

		private Vector2 scroll;

		private void OnGUI()
		{
			var settings = SelectiveProfilerSettings.instance;
			scroll = EditorGUILayout.BeginScrollView(scroll);
			EditorGUILayout.LabelField("Selected Methods", EditorStyles.boldLabel);
			DrawSavedMethods(settings);
			GUILayout.Space(10);
			
			EditorGUILayout.LabelField("Patches", EditorStyles.boldLabel);
			var patches = SelectiveProfiler.Patches;
			if (patches == null || patches.Count <= 0)
			{
				EditorGUILayout.LabelField("No active patched methods");
			}
			else
			{
				DrawProfilerPatchesList();
				DrawScopesList(settings);
			}
			EditorGUILayout.EndScrollView();
		}

		private static bool MethodsListFoldout
		{
			get => SessionState.GetBool(nameof(MethodsListFoldout), false);
			set => SessionState.SetBool(nameof(MethodsListFoldout), value);
		}
		
		private static bool MutedMethodsFoldout
		{
			get => SessionState.GetBool(nameof(MutedMethodsFoldout), false);
			set => SessionState.SetBool(nameof(MutedMethodsFoldout), value);
		}
		
		private static readonly List<int> removeList = new List<int>();
		internal static void DrawSavedMethods(SelectiveProfilerSettings settings)
		{
			void ApplyRemoveList()
			{
				for (var i = removeList.Count - 1; i >= 0; i--)
				{
					var index = removeList[i];
					if(index < 0 || index >= settings.MethodsList.Count)
						continue;
					var entry = settings.MethodsList[index];
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
				var toggleMuteAll = GUILayout.Button((activeList ? "Mute all" : "Unmute all"), GUILayout.Width(70));
				var removeAll = GUILayout.Button("Remove all", GUILayout.Width(80));
				EditorGUI.EndDisabledGroup();
				EditorGUILayout.EndHorizontal();

				if (toggleMuteAll || removeAll)
				{
					for (var index = list.Count - 1; index >= 0; index--)
					{
						if (removeAll) removeList.Add(index);
						else if (toggleMuteAll) settings.SetMuted(list[index], activeList);
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
						EditorGUILayout.LabelField(new GUIContent(m.ClassWithMethod(), m.MethodIdentifier()), GUILayout.ExpandWidth(true));
						EditorGUI.EndDisabledGroup();
						var muted = settings.IsMuted(m);
						if (GUILayout.Button(muted ? "Unmute" : "Mute", GUILayout.Width(60)))
							settings.SetMuted(m, !muted);
						if (GUILayout.Button("x", GUILayout.Width(30)))
							removeList.Add(index);
						EditorGUILayout.EndHorizontal();
					}
					EditorGUI.indentLevel--;
				}
				
				ApplyRemoveList();
				return foldout;
			}

			MethodsListFoldout = DrawMethods(settings.MethodsList, true, MethodsListFoldout, "Methods");
			MutedMethodsFoldout = DrawMethods(settings.MutedMethods, false, MutedMethodsFoldout, "Muted");
		}

		internal static void DrawProfilerPatchesList()
		{
			if (SelectiveProfiler.Patches == null || SelectiveProfiler.Patches.Count <= 0) return;
			
			foreach (var p in SelectiveProfiler.Patches)
			{
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField(new GUIContent(p.Patch.DisplayName, p.Identifier), GUILayout.ExpandWidth(true));
				if (!p.IsActive)
				{
					if (GUILayout.Button("Enable", GUILayout.Width(80)))
						p.Enable();
				}
				else if (GUILayout.Button("Disable",GUILayout.Width(80)))
					p.Disable();

				EditorGUILayout.EndHorizontal();
			}
		}
		
		private static bool ScopesListFoldout
		{
			get => SessionState.GetBool(nameof(ScopesListFoldout), false);
			set => SessionState.SetBool(nameof(ScopesListFoldout), value);
		}

		private static Dictionary<string, List<MethodInformation>> scopes = new Dictionary<string, List<MethodInformation>>();

		internal static MethodScopeDisplay SelectedScope
		{
			get => (MethodScopeDisplay)SessionState.GetInt("SelectedScopeDisplay", (int) (MethodScopeDisplay.Type));
			set => SessionState.SetInt("SelectedScopeDisplay", (int) value);
		}

		internal enum MethodScopeDisplay
		{
			Assembly,			
			Namespace,
			Type,
		}
		
		internal static void DrawScopesList(SelectiveProfilerSettings settings)
		{
			ScopesListFoldout = EditorGUILayout.Foldout(ScopesListFoldout, "Scopes");
			if (ScopesListFoldout)
			{

				string GetScopeKey(MethodInformation method)
				{
					switch (SelectedScope)
					{
						case MethodScopeDisplay.Assembly:
							return method.Assembly;
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
					if(!scopes.ContainsKey(scope))
						scopes.Add(scope, new List<MethodInformation>());
					scopes[scope].Add(method);
				}	
				
				EditorGUI.indentLevel++;
				SelectedScope = (MethodScopeDisplay)EditorGUILayout.EnumPopup("Scope", SelectedScope);
				
				scopes.Clear();
				foreach (var method in settings.MethodsList) AddToScope(method);
				foreach (var method in settings.MutedMethods) AddToScope(method);

				bool GetFoldout(string key) => SessionState.GetBool("SelectiveProfilerScopeFoldout-" + key, false);
				void SetFoldout(string key, bool value) => SessionState.SetBool("SelectiveProfilerScopeFoldout-" + key, value);
				
				foreach (var kvp in scopes)
				{
					var scope = kvp.Key;
					EditorGUILayout.BeginHorizontal();
					var show = EditorGUILayout.Foldout(GetFoldout(scope), scope);
					if (GUILayout.Button("Mute", GUILayout.Width(60)))
						SetMuted(kvp.Value, true);
					if(GUILayout.Button("Unmute", GUILayout.Width(70)))
						SetMuted(kvp.Value, false);
					EditorGUILayout.EndHorizontal();
					SetFoldout(scope, show);
					if (show)
					{
						EditorGUI.indentLevel++;
						var list = kvp.Value;
						foreach (var entry in list)
						{
							// EditorGUILayout.BeginHorizontal();
							EditorGUILayout.LabelField(new GUIContent(entry.ClassWithMethod(), entry.MethodIdentifier()), GUILayout.ExpandWidth(true));
							// if(GUILayout.Button("Mute", GUILayout.Width(60)))
							// 	settings.SetMuted(entry, true);
							// if(GUILayout.Button("Unmute", GUILayout.Width(70)))
							// 	settings.SetMuted(entry, false);
							// EditorGUILayout.EndHorizontal();
						}
						EditorGUI.indentLevel--;
					}
				}
				EditorGUI.indentLevel--;

				void SetMuted(IList<MethodInformation> list, bool muted)
				{
					foreach (var e in list) settings.SetMuted(e, muted);
				}
			}
		}
	}
}