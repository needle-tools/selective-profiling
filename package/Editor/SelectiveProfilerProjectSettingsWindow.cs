

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

		private bool PatchesFoldout
		{
			get => SessionState.GetBool(nameof(PatchesFoldout), false);
			set => SessionState.SetBool(nameof(PatchesFoldout), value);
		}

		public override void OnGUI(string searchContext)
		{
			base.OnGUI(searchContext);
			var settings = SelectiveProfilerSettings.instance;
			EditorGUI.BeginChangeCheck();
			
			EditorGUILayout.LabelField("General", EditorStyles.boldLabel);
			settings.Enabled = EditorGUILayout.ToggleLeft(new GUIContent("Enabled", ""), settings.Enabled);
			settings.ImmediateMode = EditorGUILayout.ToggleLeft(new GUIContent("Immediate Mode", "Automatically profile selected method in Unity Profiler Window"), settings.ImmediateMode);
			
			GUILayout.Space(5);
			EditorGUILayout.LabelField("Deep Profiling", EditorStyles.boldLabel);
			settings.DeepProfiling = EditorGUILayout.ToggleLeft(new GUIContent("Use Deep Profiling", "When true all calls within a newly profiled method will be recursively added to be profiled as well"), settings.DeepProfiling);
			settings.MaxDepth = EditorGUILayout.IntField(new GUIContent("Max Depth", "When deep profiling is enabled this controls how many levels deep we should follow nested method calls"), settings.MaxDepth);

			GUILayout.Space(5);
			EditorGUILayout.LabelField("Data", EditorStyles.boldLabel); 
			PatchesFoldout = EditorGUILayout.Foldout(PatchesFoldout, "Selected Methods [" + settings.AllSelectedMethodsCount + "]");
			if (PatchesFoldout)
			{
				EditorGUI.indentLevel++;
				SelectiveProfilerWindow.DrawSavedMethods(settings);
				EditorGUI.indentLevel--;
			}
			
			GUILayout.FlexibleSpace();
			EditorGUILayout.BeginVertical();
			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			settings.DebugLog = GUILayout.Toggle(settings.DebugLog, new GUIContent("Debug Log"));
			GUILayout.Space(5);
			if (GUILayout.Button(new GUIContent("Save"), GUILayout.Width(80))) 
				SelectiveProfilerSettings.instance.Save();
			if (GUILayout.Button(new GUIContent("Clear"), GUILayout.Width(80))) 
				SelectiveProfilerSettings.instance.ClearAll();
			if (GUILayout.Button(new GUIContent("Open Selective Profiler"), GUILayout.Width(160)))
			{
				SelectiveProfilerWindow.Open();
			}
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.EndVertical();
			if(EditorGUI.EndChangeCheck())
				settings.Save();
		}
	}
}