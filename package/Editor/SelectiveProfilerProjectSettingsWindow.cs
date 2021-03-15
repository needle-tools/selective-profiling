

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
			EditorGUI.BeginChangeCheck();
			settings.Enabled = EditorGUILayout.ToggleLeft(new GUIContent("Enabled", ""), settings.Enabled);
			settings.AutoProfile = EditorGUILayout.ToggleLeft(new GUIContent("Auto Profile", "Automatically profile selected method in Unity Profiler Window"), settings.AutoProfile);
			settings.DeepProfiling = EditorGUILayout.ToggleLeft(new GUIContent("Deep Profiling", "When true all calls within a newly profiled method will be recursively added to be profiled as well"), settings.DeepProfiling);
			settings.MaxDepth = EditorGUILayout.IntField(new GUIContent("Max Depth", "How many levels to recursively inject profile samples in found method calls"), settings.MaxDepth);

			EditorGUILayout.Space(10);
			SelectiveProfilerWindow.DrawProfilerPatchesList();
			
			GUILayout.FlexibleSpace();
			EditorGUILayout.BeginVertical();
			EditorGUILayout.BeginHorizontal();
			settings.DebugLog = EditorGUILayout.ToggleLeft(new GUIContent("Debug Log"), settings.DebugLog);
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(new GUIContent("Save"), GUILayout.Width(80))) 
				SelectiveProfilerSettings.instance.Save();
			if (GUILayout.Button(new GUIContent("Clear"), GUILayout.Width(80))) 
				SelectiveProfilerSettings.instance.ClearAll();
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.EndVertical();
			if(EditorGUI.EndChangeCheck())
				settings.Save();
		}
	}
}