

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

			EditorGUILayout.BeginHorizontal();
			
			if (GUILayout.Button(new GUIContent("Save"), GUILayout.Width(80)))
			{
				SelectiveProfilerSettings.instance.Save();
			}
			
			
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.Space(10);
			SelectiveProfilerWindow.DrawProfilerPatchesList();
		}
	}
}