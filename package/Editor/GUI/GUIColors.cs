using UnityEditor.Graphs;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	internal static class GUIColors
	{
		private static Gradient _gradient;

		public static float NaiveCalculateGradientPosition(float ms, float alloc)
		{
			return ms / 16f + alloc / 5000f;
		}
		
		public static Color GetColorOnGradient(float pos01)
		{
			if (_gradient == null)
				_gradient = new Gradient()
				{
					colorKeys = new[]
					{
						new GradientColorKey(Color.gray, 0.001f),
						new GradientColorKey(Color.white, .05f),
						new GradientColorKey(new Color(1f, .7f, .1f), .5f),
						new GradientColorKey(new Color(1f, .7f, .1f), .999992f),
						new GradientColorKey(new Color(1f, .3f, .2f), 1f),
					}
				};
			return _gradient.Evaluate(pos01);
		}
		
	}
}