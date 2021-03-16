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
			bool DrawMethods(IReadOnlyList<MethodInformation> list, bool disabled, bool foldout, string label)
			{
				if (list == null) return foldout;
				foldout = EditorGUILayout.Foldout(foldout, label + " [" + list.Count + "]");
				if (foldout)
				{
					EditorGUI.indentLevel++;
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
					EditorGUI.indentLevel--;
				}
				return foldout;
			}

			MethodsListFoldout = DrawMethods(settings.MethodsList, false, MethodsListFoldout, "Methods");
			MutedMethodsFoldout = DrawMethods(settings.MutedMethods, true, MutedMethodsFoldout, "Muted");
			
			
			for (var i = removeList.Count - 1; i >= 0; i--)
			{
				var index = removeList[i];
				settings.Remove(settings.MethodsList[index]);
			}
			removeList.Clear();
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
	}
}