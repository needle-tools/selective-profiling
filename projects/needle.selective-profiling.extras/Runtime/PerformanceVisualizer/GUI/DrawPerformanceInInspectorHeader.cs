using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Threading.Tasks;
using needle.EditorPatching;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Needle.SelectiveProfiling
{
	public class DrawPerformanceInInspectorHeader : EditorPatchProvider
	{
		protected override void OnGetPatches(List<EditorPatch> patches)
		{
			patches.Add(new ComponentHeader_Patch());
		}

		private class ComponentHeader_Patch : EditorPatch
		{
			protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
			{
				var t = typeof(Toolbar).Assembly.GetType("UnityEditor.UIElements.EditorElement");
				Debug.Assert(t != null, "Type is null");
				var m = t.GetMethod("DrawEditorSmallHeader", BindingFlags.Instance | BindingFlags.NonPublic);
				Debug.Assert(m != null, "Method is null");
				targetMethods.Add(m);
				return Task.CompletedTask;
			}
			
			// Draw small headers (the header above each component) after the culling above
			// so we don't draw a component header for all the components that can't be shown.
			// Rect DrawEditorSmallHeader(Editor[] editors, Object target, bool wasVisible)
			// ReSharper disable once UnusedMember.Local
			private static void Postfix(Object target, Rect __result)
			{
				var lastRect = __result;
				var instanceId = target.GetInstanceID();
				if (SelectivePerformanceData.TryGetPerformanceData(instanceId, out var data))
				{
					// lastRect.width -= 80;
					// GUI.Label(lastRect, data.Alloc.ToString("0.0"), Styles.rightAlignedStyle);
					
					var circleRect = lastRect;
					circleRect.x = lastRect.width - 77;
					circleRect.height = 8;
					circleRect.width = circleRect.height;
					circleRect.y += (lastRect.height - circleRect.height) * .5f;

					var t = GUIColors.NaiveCalculateGradientPosition(data.TotalMs, data.Alloc);
					var col = GUIColors.GetColorOnGradient(t);
					if (t < .4f) col.a *= .5f;
					GUI.DrawTexture(circleRect, Textures.FilledCircle, ScaleMode.ScaleAndCrop, true, 1, col, 0, 0);
				}
			}
		}
	}
}