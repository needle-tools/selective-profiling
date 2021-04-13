using System.Threading.Tasks;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	internal static class Draw
	{
		private static Rect lastHoveredRect;
		private static Object lastHoveredObject;
		
		internal struct State
		{
			public Rect Rect;
			public EventType EventType;
			public Vector2 MousePosition;
			public bool IsHovering;
			public bool Clicked;

			public State(Rect rect)
			{
				this.Rect = rect;
				EventType = Event.current.type;
				MousePosition = Event.current.mousePosition;
				IsHovering = false;
				Clicked = false;
			}
		}

		private static Rect ExpandRect(Rect rect, int pixel)
		{
			rect.width += pixel;
			rect.height += pixel;
			rect.x -= pixel * .5f;
			rect.y -= pixel * .5f;
			return rect;
		}
		
		public static void DrawIcon(Object obj, ref State state, int size = 6)
		{
			if (!obj) return;
			var rect = state.Rect;
			var instanceId = obj.GetInstanceID();
			if (SelectivePerformanceData.TryGetPerformanceData(instanceId, out var data))
			{
				var circleRect = rect;
				circleRect.x = rect.x + rect.width;
				circleRect.height = size;
				circleRect.width = circleRect.height;
				circleRect.y += (rect.height - circleRect.height) * .5f;

				var t = GUIColors.NaiveCalculateGradientPosition(data.TotalMs, data.Alloc);
				var col = GUIColors.GetColorOnGradient(t);
				if (t < .4f) col.a *= .5f;

				var mp = state.MousePosition;
				var check = ExpandRect(circleRect, 6);
				if (check.Contains(mp))
				{
					state.IsHovering = true;
					
					lastHoveredObject = obj;
					lastHoveredRect = check;
					var back = Color.white;
					back.a = .1f;
					GUI.DrawTexture(check, Texture2D.whiteTexture, ScaleMode.ScaleAndCrop, false, 1, back, 0, 0);
				}

				// GUI.DrawTexture(check, Texture2D.grayTexture);
				GUI.DrawTexture(circleRect, Textures.CircleFilled, ScaleMode.ScaleAndCrop, true, 1, col, 0, 0);

				// EditorGUIUtility.AddCursorRect(circleRect, MouseCursor.Zoom);
				
				if (lastHoveredObject == obj)
				{
					if (state.EventType == EventType.MouseDown && Event.current.button == 0)
					{
						if (lastHoveredRect.Contains(mp))
						{
							state.Clicked = true;
							SelectivePerformanceData.SelectItem(data);
						}
					}
				}
			}
		}
	}
}