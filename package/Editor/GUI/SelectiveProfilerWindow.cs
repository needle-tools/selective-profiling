using System.Collections.Generic;
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
					EditorGUILayout.BeginHorizontal();
					SelectiveProfiler.stepDeepProfileToIndex = EditorGUILayout.IntField("Step to", SelectiveProfiler.stepDeepProfileToIndex, GUILayout.ExpandWidth(false));
					if (GUILayout.Button("Step Deep Profile", GUILayout.ExpandWidth(true))) 
						SelectiveProfiler.stepDeepProfile = true;
					EditorGUILayout.EndHorizontal();
				});
			}

			EditorGUILayout.Space(10);
			EditorGUILayout.EndScrollView();
		}

	}
}