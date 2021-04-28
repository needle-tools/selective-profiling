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
		
		private static Texture2D _profiled;
		public static Texture2D Profiled
		{
			get
			{
				if (!_profiled)
					_profiled = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.needle.selective-profiling/Editor/GUI/Textures/profiled4.png");
				return _profiled;
			}
		}
		
		private static Texture2D _profiledChild;
		public static Texture2D ProfiledChild
		{
			get
			{
				if (!_profiledChild)
					_profiledChild = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.needle.selective-profiling/Editor/GUI/Textures/profiled2.png");
				return _profiledChild;
			}
		}

		private static Texture2D _circleFilled;
		public static Texture2D CircleFilled
		{
			get
			{
				if (!_circleFilled)
					_circleFilled = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.needle.selective-profiling/Editor/GUI/Textures/circle.png");
				return _circleFilled;
			}
		}

		private static Texture2D _circleHollow;
		public static Texture2D CircleHollow
		{
			get
			{
				if (!_circleHollow)
					_circleHollow = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.needle.selective-profiling/Editor/GUI/Textures/circle.png");
				return _circleHollow;
			}
		}
		
		private static Texture2D _hotPath;
		public static Texture2D HotPathIcon
		{
			get
			{
				if (!_hotPath)
					_hotPath = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.needle.selective-profiling/Editor/GUI/Textures/fire.png");
				return _hotPath;
			}
		}
		
		private static Texture2D _whiteLabel;
		public static Texture2D WhiteLabel
		{
			get
			{
				if (!_whiteLabel)
					_whiteLabel = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.needle.selective-profiling/Editor/GUI/Textures/white_label.png");
				return _whiteLabel;
			}
		}
	}
}