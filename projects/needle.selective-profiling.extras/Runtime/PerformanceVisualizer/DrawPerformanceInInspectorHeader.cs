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
			private static void Postfix(Object target, Rect __result)
			{
				var lastRect = __result;
				var instanceId = target.GetInstanceID();
				if (PerformanceVisualizer.TryGetPerformanceData(instanceId, out var data))
				{
					lastRect.width -= 80;
					GUI.Label(lastRect, data.TotalMs + "ms", PerformanceVisualizer.rightAlignedStyle);
				}
			}
		}
	}
}