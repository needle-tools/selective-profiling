﻿using UnityEditor;
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

		private static Texture2D _circle;
		public static Texture2D FilledCircle
		{
			get
			{
				if (!_circle)
					_circle = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.needle.selective-profiling/Editor/GUI/Textures/circle.png");
				return _circle;
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
	}
}