

using System;
using System.Collections.Generic;
using Needle.SelectiveProfiling.Utils;
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
			EditorGUI.BeginChangeCheck();
			
			Draw.DefaultSelectiveProfilerUI(settings, false);
			
			GUILayout.FlexibleSpace();
			GUILayout.Space(10);
			
			EditorGUILayout.BeginVertical();
			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			settings.DebugLog = GUILayout.Toggle(settings.DebugLog, new GUIContent("Debug Log"));
			GUILayout.Space(5);
			if (GUILayout.Button(new GUIContent("Save"), GUILayout.Width(80))) 
				SelectiveProfilerSettings.instance.Save();
			// if (GUILayout.Button(new GUIContent("Clear"), GUILayout.Width(80))) 
			// 	SelectiveProfilerSettings.instance.ClearAll();
			if (GUILayout.Button(new GUIContent("Open Selective Profiler"), GUILayout.Width(160))) 
				SelectiveProfilerWindow.Open();
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.EndVertical();
			
			if(EditorGUI.EndChangeCheck())
				settings.Save();
		}
	}
}