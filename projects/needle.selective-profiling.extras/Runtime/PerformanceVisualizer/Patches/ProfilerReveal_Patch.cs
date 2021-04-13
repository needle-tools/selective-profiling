using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using needle.EditorPatching;
using UnityEditor.IMGUI.Controls;
using UnityEditor.Profiling;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	public class ProfilerReveal_Patch : EditorPatchProvider
	{
		/// <summary>
		/// marker ids
		/// </summary>
		private static readonly List<int> requestSelectedMarkerIds = new List<int>();

		internal static void Reveal(List<int> markerIds)
		{
			if (markerIds == null || markerIds.Count <= 0) return;

			// TODO: add check, this is only necessary when not in live mode and paused
			if (tree != null)
			{
				tree.Reload();
			}

			requestSelectedMarkerIds.Clear();
			requestSelectedMarkerIds.AddRange(markerIds.Distinct());
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
				InternalHandleReveal(requestSelectedMarkerIds);
				requestSelectedMarkerIds.Clear();
			}
		}

		private static TreeView tree;
		private static IList<TreeViewItem> rows;
		private static HierarchyFrameDataView frame;

		private static void InternalHandleReveal(ICollection markerIds)
		{
			if (frame == null || !frame.valid) return;
			if (markerIds == null || markerIds.Count <= 0 || tree == null || rows == null || rows.Count <= 0) return;

			List<int> selection = null;
			TreeViewItem focus = null;

			bool IsMatching(TreeViewItem item, int check)
			{
				if (item == null) return false;
				var id = frame.GetItemMarkerID(item.id);
				return id == check;
			}

			void Select(TreeViewItem _item)
			{
				var _id = _item.id;
				if (selection == null) selection = new List<int>();
				if (!selection.Contains(_id))
				{
					// Debug.Log("SELECT " + _id + ", " + frame.GetItemMarkerID(_id));
					selection.Add(_id);
				}

				focus = _item;
				tree.state.selectedIDs.Add(_id);
				tree.SetExpanded(_id, true);
			}

			for (var i = 0; i < requestSelectedMarkerIds.Count; i++)
			{
				var toFind = requestSelectedMarkerIds[i];
				var isMarker = toFind == PerformanceData.StartMarker;
				if (isMarker)
					continue;
				if (i >= requestSelectedMarkerIds.Count) break;
				toFind = requestSelectedMarkerIds[i];

				bool Step(ref int index, ref int search)
				{
					index += 1;
					if (index >= requestSelectedMarkerIds.Count) return false;
					search = requestSelectedMarkerIds[index];
					return search != PerformanceData.StartMarker;
				}

				foreach (var row in rows)
				{
					if (row == null) continue;
					if (!IsMatching(row, toFind)) continue;

					Select(row);
					if (Step(ref i, ref toFind))
						FindInChildrenRecursively(row, ref i, ref toFind);
					break;

					void FindInChildrenRecursively(TreeViewItem item, ref int index, ref int search)
					{
						tree.SetExpanded(item.id, true);
						if (!item.hasChildren) return;
						foreach (var ch in item.children)
						{
							if (ch == null) continue;
							// Debug.Log("Test " + frame.GetItemMarkerID(ch.id) + ", " + search);
							if (IsMatching(ch, search))
							{
								Select(ch);
								if (Step(ref index, ref search))
									FindInChildrenRecursively(ch, ref index, ref search);
								break;
							}
						}
					}
				}
			}

			if (selection != null && selection.Count > 0)
			{
				tree.GetType().GetField("m_SelectedItemMarkerIdPath", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(tree, null);
				tree.GetType().GetField("m_LegacySelectedItemMarkerNamePath", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(tree, null);

				// Debug.Log("SELECT " + string.Join(", ", selection));
				tree.SetSelection(new List<int>() {selection.LastOrDefault()},
					TreeViewSelectionOptions.RevealAndFrame
				);
				tree.SetFocusAndEnsureSelectedItem();
				tree.state.lastClickedID = selection.Last();
				if (focus != null)
					tree.GetType().GetMethod("SelectionClick", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(tree, new object[] {focus, true});

				// if(frame != null && tree.state != null)
				// 	Debug.Log("CLICK " + frame.GetItemMarkerID(tree.state.lastClickedID));
			}
		}
	}
}