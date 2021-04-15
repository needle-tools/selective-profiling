using UnityEditor;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	internal class Styles
	{
		public static GUIStyle rightAlignedStyle;

		public static void EnsureStyles()
		{
			if (rightAlignedStyle == null)
			{
				rightAlignedStyle = new GUIStyle(EditorStyles.label);
				rightAlignedStyle.alignment = TextAnchor.MiddleRight;
			}
		}
	}
}