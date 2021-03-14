using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	public class DynamicProfilingGUI : EditorWindow
	{
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

		private List<ProfilerSamplePatch> profilerPatches = new List<ProfilerSamplePatch>();

		private void OnGUI()
		{
			
		}
	}
}