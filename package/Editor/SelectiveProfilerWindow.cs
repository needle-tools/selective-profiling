using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	public class SelectiveProfilerWindow : EditorWindow
	{
		[MenuItem(MenuItems.Menu + nameof(Open))]
		private static void Open()
		{
			var window = CreateInstance<SelectiveProfilerWindow>();
			window.Show();
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
			EditorGUILayout.LabelField("Saved Methods", EditorStyles.boldLabel);
			DrawSavedMethods(settings);
			GUILayout.Space(10);
			
			EditorGUILayout.LabelField("Patches", EditorStyles.boldLabel);
			var patches = SelectiveProfiler.Patches;
			if (patches == null || patches.Count <= 0)
			{
				EditorGUILayout.LabelField("No selective patches in project");
			}
			else
			{
				DrawProfilerPatchesList();
			}
			EditorGUILayout.EndScrollView();
		}

		private static readonly List<int> removeList = new List<int>();
		internal static void DrawSavedMethods(SelectiveProfilerSettings settings)
		{
			void DrawMethods(IReadOnlyList<MethodInformation> list, bool disabled)
			{
				for (var index = 0; index < list.Count; index++)
				{
					var m = list[index];
					EditorGUILayout.BeginHorizontal();
					EditorGUI.BeginDisabledGroup(disabled);
					EditorGUILayout.LabelField(new GUIContent(m.ClassWithMethod(), m.MethodIdentifier()), GUILayout.ExpandWidth(true));
					EditorGUI.EndDisabledGroup();
					var muted = settings.IsMuted(m);
					if (GUILayout.Button(muted ? "Unmute" : "Mute", GUILayout.Width(60))) 
						settings.SetMuted(m, !muted);
					if (GUILayout.Button("x", GUILayout.Width(30))) 
						removeList.Add(index);
					EditorGUILayout.EndHorizontal();
				}
			}

			DrawMethods(settings.MethodsList, false);
			DrawMethods(settings.MutedMethods, true);
			
			for (var i = removeList.Count - 1; i >= 0; i--)
			{
				var index = removeList[i];
				settings.Remove(settings.MethodsList[index]);
			}
			removeList.Clear();
		}

		internal static void DrawProfilerPatchesList()
		{
			if (SelectiveProfiler.Patches == null) return;
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
	}
}