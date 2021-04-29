using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using needle.EditorPatching;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	internal class ProfilerTreeView_Patch : EditorPatchProvider
	{
		internal static int RequestExpandItemId;
		
		protected override void OnGetPatches(List<EditorPatch> patches)
		{
			patches.Add(new ExpandSelectedMarkerInHierarchy());
		}
		
		public class ExpandSelectedMarkerInHierarchy : EditorPatch
		{
			
			protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
			{
				var t = typeof(UnityEditorInternal.ProfilerDriver).Assembly.GetType("UnityEditorInternal.ProfilerFrameDataTreeView");
				var m = t.GetMethod("OnGUI", (BindingFlags) ~0);
				targetMethods.Add(m);
				return Task.CompletedTask;
			}

			private static readonly List<int> selectionList = new List<int>();

			private static void Prefix(TreeView __instance)
			{
				if (RequestExpandItemId == 0) return;
				SelectiveProfiler.DrawItemDebugInformationInTreeView = true;
				selectionList.Clear();
				selectionList.Add(RequestExpandItemId);
				Debug.Log("Select item " + RequestExpandItemId);

				var ancestors = __instance.GetType().GetMethod("GetAncestors", BindingFlags.NonPublic | BindingFlags.Instance)
					.Invoke(__instance, new object[] {RequestExpandItemId}) as List<int>;
				RequestExpandItemId = 0;

				if(ancestors != null)
					Debug.Log(string.Join(", ", ancestors));

				__instance.ExpandAll();
				__instance.GetType().GetMethod("SelectionChanged", BindingFlags.Instance | BindingFlags.NonPublic)?
					.Invoke(__instance, new object[] {selectionList});
				
				

				// Debug.Log("Select " + RequestExpandItemId);
				// var selection = new List<int>() {RequestExpandItemId};
				// __instance.FrameItem(RequestExpandItemId);
				// __instance.SetExpanded(RequestExpandItemId, true);
				// RequestExpandItemId = 0;
				// __instance.SetSelection(selectionList);
				// __instance.SetFocusAndEnsureSelectedItem();
			}
		}
	}
}