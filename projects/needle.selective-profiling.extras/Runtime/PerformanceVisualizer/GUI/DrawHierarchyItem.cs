using UnityEditor;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	public static class DrawHierarchyItem
	{
		[InitializeOnLoadMethod]
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void Init()
		{
			EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyGUI;
			EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
		}

		private static void OnHierarchyGUI(int instanceId, Rect rect)
		{
			if (SelectivePerformanceData.TryGetPerformanceData(instanceId, out var data))
				DrawData(instanceId, data, rect);
		}


		private static void DrawData(int instanceId, IPerformanceData data, Rect rect)
		{
			if (data == null) return;
			Styles.EnsureStyles();

			var obj = EditorUtility.InstanceIDToObject(instanceId);
			rect.x += 2;
			var state = new Draw.State(rect);
			Draw.DrawIcon(obj, ref state, 4);

			// var col = GUIColors.GetColorOnGradient(GUIColors.NaiveCalculateGradientPosition(data.TotalMs, data.Alloc));
			// var prev = GUI.color;
			// GUI.color = col;
			// GUI.Label(rect, data.TotalMs.ToString("0.0") + " ms" + ", " + (data.Alloc/1024).ToString("0.0") + " kb", Styles.rightAlignedStyle);
			// GUI.color = prev;
		}
	}
}