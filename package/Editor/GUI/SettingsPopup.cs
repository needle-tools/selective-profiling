using UnityEditor;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	internal class SettingsPopup : PopupWindowContent
	{
		public override Vector2 GetWindowSize()
		{
			var y =  Mathf.Clamp(SelectiveProfilerSettings.instance.MethodsCount * EditorGUIUtility.singleLineHeight + 100, 200, Screen.currentResolution.height - 300);
			return new Vector2(500, y);
		}

		private Vector2 scroll;

		public override void OnGUI(Rect rect)
		{
			scroll = EditorGUILayout.BeginScrollView(scroll);
			Draw.DefaultSelectiveProfilerUI(SelectiveProfilerSettings.Instance, true);
			EditorGUILayout.EndScrollView();
		}
	}
}