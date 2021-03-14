

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	internal class SelectiveProfilerProjectSettingsProvider : SettingsProvider
	{
		public const string SettingsPath = "Project/Needle/Selective Profiler";
		
		[SettingsProvider]
		public static SettingsProvider Create()
		{
			try
			{
				SelectiveProfilerSettings.instance.Save();
				return new SelectiveProfilerProjectSettingsProvider(SettingsPath, SettingsScope.Project);
			}
			catch (Exception e)
			{
				Debug.LogException(e);
			}

			return null;
		}

		private SelectiveProfilerProjectSettingsProvider(string path, SettingsScope scopes, IEnumerable<string> keywords = null) : base(path, scopes, keywords)
		{
		}

		public override void OnGUI(string searchContext)
		{
			base.OnGUI(searchContext);
			var settings = SelectiveProfilerSettings.instance;
			
			settings.DeepProfiling = EditorGUILayout.ToggleLeft(new GUIContent("Deep Profiling", "When enabled all calls within a newly profiled method will be recursively added to be profiled as well"), settings.DeepProfiling);

			EditorGUILayout.Space(10);
			SelectiveProfilerWindow.DrawProfilerPatchesList();
			
			GUILayout.FlexibleSpace();
			EditorGUILayout.BeginVertical();
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button(new GUIContent("Save"), GUILayout.Width(80)))
			{
				SelectiveProfilerSettings.instance.Save();
			}
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.EndVertical();
		}
	}
}