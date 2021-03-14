using System;
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

		private void OnGUI()
		{
			EditorGUILayout.LabelField("Patches", EditorStyles.boldLabel);
			var patches = SelectiveProfiler.Patches;
			if (patches == null || patches.Count <= 0)
			{
				EditorGUILayout.LabelField("No selective patches in project");
			}
			else
			{
				foreach (var p in patches)
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
}