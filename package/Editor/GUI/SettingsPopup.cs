using UnityEditor;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	internal class SettingsPopup : PopupWindowContent
	{
		public override Vector2 GetWindowSize()
		{
			var maxHeight = Mathf.Min(Screen.currentResolution.height - 300, 800);
			var y =  Mathf.Clamp(SelectiveProfilerSettings.instance.MethodsCount * EditorGUIUtility.singleLineHeight * 1.5f + 100, 300, maxHeight);
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