using UnityEditor;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	internal static class GUIStyles
	{
		private static Color DisabledTextColor => Color.gray;
		
		private static GUIStyle _disabledLabel;
		public static GUIStyle DisabledLabel
		{
			get
			{
				if (_disabledLabel == null)
				{
					_disabledLabel = new GUIStyle(EditorStyles.label);
					_disabledLabel.normal.textColor = DisabledTextColor;
				}
				return _disabledLabel;
			}
		}
		public static GUIStyle Label(bool state) => state ? EditorStyles.label : DisabledLabel;

		public static GUIStyle Foldout => EditorStyles.foldout;
		
		private static GUIStyle _boldFoldout;
		public static GUIStyle BoldFoldout
		{
			get
			{
				if (_boldFoldout == null)
				{
					_boldFoldout = new GUIStyle(EditorStyles.foldout);
					_boldFoldout.fontStyle = FontStyle.Bold;
				}
				return _boldFoldout;
			}
		}
		
		private static GUIStyle _boldFoldoutDisabled;
		public static GUIStyle BoldFoldoutDisabled
		{
			get
			{
				if (_boldFoldoutDisabled == null)
				{
					_boldFoldoutDisabled = new GUIStyle(EditorStyles.foldout);
					_boldFoldoutDisabled.fontStyle = FontStyle.Bold;
					_boldFoldoutDisabled.normal.textColor = DisabledTextColor;
				}
				return _boldFoldoutDisabled;
			}
		}

	}
}