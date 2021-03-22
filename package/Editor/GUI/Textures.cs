using UnityEditor;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	internal static class Textures
	{
		private static Texture2D _pin;

		public static Texture2D Pin
		{
			get
			{
				if (!_pin)
					_pin = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.needle.selective-profiling/Editor/GUI/Textures/pin.png");
				return _pin;
			}
		}
	}
}