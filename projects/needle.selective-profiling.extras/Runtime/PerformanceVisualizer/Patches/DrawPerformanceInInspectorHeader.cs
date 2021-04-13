using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using needle.EditorPatching;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UI;

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

			// https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/UIElementsEditor/Inspector/EditorElement.cs#L347

			
			private static void Prefix(out Draw.State __state)
			{
				__state = new Draw.State()
				{
					EventType = Event.current.type,
					MousePosition = Event.current.mousePosition,
				};

				var rect = new Rect(Screen.width - 77, 0, 18, EditorGUIUtility.singleLineHeight + 2);
				
				// GUI.DrawTexture(rect, Texture2D.whiteTexture);
				if(rect.Contains(Event.current.mousePosition))
				{
					if(Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint)
						Event.current.Use();
				}
			}

			// Draw small headers (the header above each component) after the culling above
			// so we don't draw a component header for all the components that can't be shown.
			// Rect DrawEditorSmallHeader(Editor[] editors, Object target, bool wasVisible)
			// ReSharper disable once UnusedMember.Local
			private static void Postfix(Object target, Rect __result, Draw.State __state)
			{
				if (!target) return;
				
				var rect = __result;
				rect.width -= 77;
				__state.Rect = rect;
				Draw.DrawIcon(target, ref __state, 9);
			}
		}
	}
}