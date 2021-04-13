using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using needle.EditorPatching;
using UnityEditor.IMGUI.Controls;
using UnityEditor.Profiling;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	public class ProfilerReveal_Patch : EditorPatchProvider
	{
		private static List<int> requestReveal = new List<int>();
		
		internal static void Reveal(List<int> markerIds)
		{
			// TODO: add check, this is only necessary when not in live mode and paused
			if (tree != null)
			{
				tree.Reload();
			}
			
			requestReveal.Clear();
			requestReveal.AddRange(markerIds);
		}

		protected override void OnGetPatches(List<EditorPatch> patches)
		{
			patches.Add(new Patch());
		}

		private class Patch : EditorPatch
		{
			protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
			{
				var t = typeof(UnityEditorInternal.ProfilerDriver).Assembly.GetType("UnityEditorInternal.ProfilerFrameDataTreeView");
				var m = t.GetMethod("BuildRows", (BindingFlags) ~0);
				targetMethods.Add(m);
				return Task.CompletedTask;
			}


			private static void Postfix(TreeView __instance, IList<TreeViewItem> __result, HierarchyFrameDataView ___m_FrameDataView)
			{
				tree = __instance;
				rows = __result;
				frame = ___m_FrameDataView;
				InternalHandleReveal(requestReveal);
				requestReveal.Clear();
			}
		}

		private static TreeView tree;
		private static IList<TreeViewItem> rows;
		private static HierarchyFrameDataView frame;

		private static void InternalHandleReveal(List<int> markerIds)
		{
			if (frame == null || !frame.valid) return;
			if (markerIds == null ||  markerIds.Count <= 0 || tree == null || rows == null || rows.Count <= 0) return;

			List<int> selection = null;
			TreeViewItem focus = null;
			
			for (var index = 0; index < rows.Count; index++)
			{
				var item = rows[index];

				void AddToSelection(TreeViewItem _item, int _id, int _marker)
				{
					if (selection == null) selection = new List<int>();
					// Debug.Log("SELECT " + _id + ", " + _marker);
					selection.Add(_id);
					tree.SetExpanded(_id, true);
					tree.state.selectedIDs.Add(_id);
					focus = _item;
				}

				var markerId = frame.GetItemMarkerID(item.id);
				if (markerIds.Contains(markerId))
				{
					AddToSelection(item, item.id, markerId);
				}
				
				if (item.hasChildren)
				{
					foreach (var ch in item.children)
					{
						if (ch == null) continue;
						markerId = frame.GetItemMarkerID(ch.id);
						if (markerIds.Contains(markerId))
						{
							AddToSelection(ch, ch.id, markerId);
						}
					}
				}
			}
			
			if (selection != null)
			{
				Debug.Log("SELECT " + string.Join(", ", selection));
				tree.SetSelection(selection, TreeViewSelectionOptions.RevealAndFrame);
				tree.state.lastClickedID = selection.Last();
				tree.SetFocusAndEnsureSelectedItem();

				if (focus != null)
				{
					tree.GetType().GetMethod("SelectionClick", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(tree, new object[] {focus, false});
				}
			}

		}
	}
}