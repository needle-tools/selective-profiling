using System.Collections.Generic;
using System.Reflection;
using UnityEditor.IMGUI.Controls;
using UnityEditor.Profiling;

namespace Needle.SelectiveProfiling
{
	internal class ProfilerTreeView_Patch
	{
		internal static int RequestExpandItemId;
		internal static int RequestExpandMarkerId;
		
		public class ExpandSelectedMarkerInHierarchy : PatchBase
		{

			protected override IEnumerable<MethodBase> GetPatches()
			{
				var t = typeof(UnityEditorInternal.ProfilerDriver).Assembly.GetType("UnityEditorInternal.ProfilerFrameDataTreeView");
				var m = t.GetMethod("BuildRows", (BindingFlags) ~0);
				yield return m;
			}
			
			private static readonly List<int> selectionList = new List<int>();

			private static void Postfix(TreeView __instance, IList<TreeViewItem> __result, HierarchyFrameDataView ___m_FrameDataView)
			{
				if (ProfilerHelper.IsDeepProfiling) return;
				
				// SelectiveProfiler.DrawItemDebugInformationInTreeView = true;
				// if (RequestExpandItemId == 0) return;
				// var frame = ___m_FrameDataView;
				// if(!frame.valid) return;
				//
				// List<int> list = new List<int>();
				// ___m_FrameDataView.GetItemAncestors(RequestExpandItemId, list);
				// Debug.Log(string.Join(", ", list));
				//
				// selectionList.Clear();
				// selectionList.Add(RequestExpandItemId);
				//
				// bool FindInRows(TreeViewItem item)
				// {
				// 	if (item == null) return false;
				// 	Debug.Log(item.id);
				// 	var markerId = frame.GetItemMarkerID(item.id);
				// 	if (markerId == RequestExpandMarkerId)
				// 	{
				// 		Debug.Log("FOUND " + markerId);
				// 		__instance.SetExpanded(item.id, true);
				// 		__instance.FrameItem(item.id);
				// 		return true;
				// 	}
				// 	
				// 	if (item.hasChildren)
				// 	{
				// 		foreach (var ch in item.children)
				// 		{
				// 			if (FindInRows(ch))
				// 				return true;
				// 		}
				// 	}
				// 	return false;
				// }
				//
				// foreach (var row in __result)
				// {
				// 	FindInRows(row);
				// }
				//
				// RequestExpandItemId = 0;


				// Debug.Log("Select item " + RequestExpandItemId);
				//
				// var ancestors = __instance.GetType().GetMethod("GetAncestors", BindingFlags.NonPublic | BindingFlags.Instance)
				// 	.Invoke(__instance, new object[] {RequestExpandItemId}) as List<int>;
				// RequestExpandItemId = 0;
				//
				// if(ancestors != null)
				// 	Debug.Log(string.Join(", ", ancestors));
				//
				// __instance.ExpandAll();
				// __instance.GetType().GetMethod("SelectionChanged", BindingFlags.Instance | BindingFlags.NonPublic)?
				// 	.Invoke(__instance, new object[] {selectionList});



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