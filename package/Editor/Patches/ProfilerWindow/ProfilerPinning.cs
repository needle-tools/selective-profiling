using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using needle.EditorPatching;
using UnityEditor.IMGUI.Controls;
using UnityEditor.Profiling;
using UnityEngine;
// ReSharper disable UnusedMember.Local

namespace Needle.SelectiveProfiling
{
	internal static class ProfilerPinning
	{
		private static readonly HashSet<int> pinnedItems = new HashSet<int>();
		private static readonly HashSet<int> unpinnedItems = new HashSet<int>();
		private static HierarchyFrameDataView GetFrameData(TreeViewItem item = null) => ProfilerFrameDataView_Patch.GetFrameDataView(item);

		public static bool AllowPinning(HierarchyFrameDataView view, TreeViewItem item, string name = null)
		{
			return true;
		}

		public static bool HasPinnedItems() => pinnedItems != null && pinnedItems.Count > 0;

		public static bool IsPinned(TreeViewItem item)
		{
			var fd = GetFrameData(item);
			var markerId = fd.GetItemMarkerID(item.id);
			return pinnedItems.Contains(markerId);
		}

		public static void Pin(TreeViewItem item)
		{
			if (item != null)
			{
				var fd = GetFrameData(item);
				var markerId = fd.GetItemMarkerID(item.id);
				InternalPin(markerId, true);

				void HandleChildren(TreeViewItem current)
				{
					if (current != null && current.hasChildren)
					{
						foreach (var ch in current.children)
						{
							if (ch == null) continue;
							var childId = GetFrameData(ch)?.GetItemMarkerID(ch.id) ?? -1;
							if (childId == -1) continue;
							InternalPin(childId, false);
							HandleChildren(ch);
						}
					}
				}

				HandleChildren(item);
			}
		}

		public static void Unpin(TreeViewItem item, int level = 0)
		{
			if (item == null) return;
			
			var fd = GetFrameData(item);
			var markerId = fd.GetItemMarkerID(item.id);
			InternalUnpin(markerId, level <= 0, level <= 0, false, item);
			
			if (item.hasChildren)
			{
				level += 1;
				foreach (var ch in item.children)
					Unpin(ch, level);
			}
		}

		private static void InternalPin(int id, bool save)
		{
			if (!pinnedItems.Contains(id)) 
				pinnedItems.Add(id);


			var frameData = GetFrameData();
			var name = frameData.GetMarkerName(id);
			
			if (unpinnedItems.Contains(id))
			{
				unpinnedItems.Remove(id);
				var unpinnedList = PinnedItems.UnpinnedProfilerItems;
				if (unpinnedList != null && unpinnedList.Contains(name))
				{
					unpinnedList.Remove(name); 
					PinnedItems.Save();
				}
			}
			
			if (save)
			{
				var pinnedList = PinnedItems.PinnedProfilerItems;
				if (pinnedList != null && !pinnedList.Contains(name))  
					pinnedList.Add(name);
				var unpinnedList = PinnedItems.UnpinnedProfilerItems;
				if (unpinnedList != null && unpinnedList.Contains(name))
					unpinnedList.Remove(name);
				PinnedItems.Save();
				
				// HACK - figure out where we miss syncing state
				IsInit = false;
			}
		}

		private static void InternalUnpin(int id, bool save, bool saveUnpinned, bool removeFromUnpinned, TreeViewItem item)
		{
			if (pinnedItems.Contains(id))
				pinnedItems.Remove(id);
			
			if (save)
			{
				var frameData = GetFrameData(); 
				var name = frameData.GetMarkerName(id);
				
				var pinnedList = PinnedItems.PinnedProfilerItems;
				if (pinnedList != null && pinnedList.Contains(name))
					pinnedList.Remove(name);

				if (IsChildOfAnyPinnedItem(item))
				{
					var unpinnedList = PinnedItems.UnpinnedProfilerItems;
					if (unpinnedList != null && !unpinnedList.Contains(name))
						unpinnedList.Add(name);
				}
				
				PinnedItems.Save();
				
				// HACK - figure out where we miss syncing state
				IsInit = false;
			}
			
			if(saveUnpinned && !unpinnedItems.Contains(id))
				unpinnedItems.Add(id);
			else if (removeFromUnpinned && unpinnedItems.Contains(id))
				unpinnedItems.Remove(id);

			// var frame = GetFrameData(item);
			// RemoveChildrenThatAreOnlyUnpinned(item.id, frame);
		}

		private static void RemoveChildrenThatAreOnlyUnpinned(int itemId, HierarchyFrameDataView frameData, int depth = 0)
		{
			var list = new List<int>();
			frameData.GetItemChildren(itemId, list);
			var requireSave = false;
			var nextDepth = depth + 1;
			foreach (var i in list)
			{
				var id = frameData.GetItemMarkerID(i);
				if (unpinnedItems.Contains(id))
				{
					var name = frameData.GetMarkerName(id);
					if (PinnedItems.UnpinnedProfilerItems.Contains(name))
					{
						PinnedItems.UnpinnedProfilerItems.Remove(name);
						requireSave = true;
					}
				}
				RemoveChildrenThatAreOnlyUnpinned(i, frameData, nextDepth);
			}
			if(requireSave && depth <= 0)
				PinnedItems.Save();
		}

		internal static bool IsChildOfAnyPinnedItem(TreeViewItem item, bool any = true, int level = 0)
		{
			if (item == null) return false;
			var fd = GetFrameData(item);
			var markerId = fd.GetItemMarkerID(item.id);
			if (unpinnedItems.Contains(markerId)) return false;
			if (pinnedItems.Contains(markerId))
			{
				if (any) return true;
				if (level > 0) return true;
			}


			level += 1;
			return IsChildOfAnyPinnedItem(item.parent, any, level);
		}

		private const float defaultDimValue = .7f;
		public static Color DimColor = new Color(defaultDimValue, defaultDimValue, defaultDimValue, 1);

		private static bool IsInit;
		
		private static void EnsureInit(TreeViewItem item)
		{
			if (IsInit) return;
			var fd = GetFrameData(item);
			if (fd == null || !fd.valid) return;
			IsInit = true;
			
			// pinnedItems.Clear();
			// unpinnedItems.Clear();
			
			foreach (var name in PinnedItems.PinnedProfilerItems)
			{
				var id = fd.GetMarkerId(name);
				InternalPin(id, false);
			}

			foreach (var name in PinnedItems.UnpinnedProfilerItems)
			{
				var id = fd.GetMarkerId(name);
				InternalUnpin(id, false, true, false, item);
			}
		}
		
		public class ProfilerPinning_Patch : EditorPatchProvider
		{
			private static Color previousColor;

			protected override void OnGetPatches(List<EditorPatch> patches)
			{
				patches.Add(new Profiler_BuildRows());
				patches.Add(new Profiler_CellGUI());
			}

			private class Profiler_CellGUI : EditorPatch
			{
				// https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/ProfilerFrameDataTreeView.cs#L647
				protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
				{
					var t = typeof(UnityEditorInternal.ProfilerDriver).Assembly.GetType("UnityEditorInternal.ProfilerFrameDataTreeView");
					var m = t.GetMethod("CellGUI", (BindingFlags) ~0);
					targetMethods.Add(m);
					return Task.CompletedTask;
				}

				private static void Prefix(TreeViewItem item)
				{
					// previousColor = TreeView.DefaultStyles.label.normal.textColor;
					// TreeView.DefaultStyles.label.normal.textColor = Color.gray;
					previousColor = GUI.color;
					var fd = GetFrameData(item);
					var markerId = fd.GetItemMarkerID(item.id);
					if (HasPinnedItems() && !pinnedItems.Contains(markerId))
						GUI.color = DimColor;
				}

				private static void Postfix()
				{
					GUI.color = previousColor;
				}
			}

			private class Profiler_BuildRows : EditorPatch
			{
				protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
				{
					var t = typeof(UnityEditorInternal.ProfilerDriver).Assembly.GetType("UnityEditorInternal.ProfilerFrameDataTreeView");
					var m = t.GetMethod("BuildRows", (BindingFlags) ~0);
					targetMethods.Add(m);
					return Task.CompletedTask;
				}

				private static void Prefix(object __instance, TreeViewItem root)
				{
					var tree = __instance as TreeView;
					ExpandPinnedItems(tree, root);
				}

				private static void Postfix(object __instance, ref IList<TreeViewItem> __result)
				{
					var items = __result;
					EnsureInit(items.LastOrDefault(i => i.id >= 0));
					

					var inserted = 0;
					// var insertList = new List<TreeViewItem>();
					for (var i = 0; i < items.Count; i++)
					{
						var item = items[i];
						
						if (IsChildOfAnyPinnedItem(item))
						{
							var fdv = ProfilerFrameDataView_Patch.GetFrameDataView(item);
							var markerId = fdv.GetItemMarkerID(item.id);
							InternalPin(markerId, false); 
							items.RemoveAt(i);
							items.Insert(inserted, item);
							// insertList.Add(item);
							++inserted;

						}
					}

					// for (var index = insertList.Count - 1; index >= 0; index--)
					// {
					// 	var it = insertList[index];
					// 	items.Insert(0, it);
					// }
				}


				private static void ExpandPinnedItems(TreeView tree, TreeViewItem root)
				{
					if (tree == null) return;
					var frameData = GetFrameData(root);
					if (frameData == null || !frameData.valid)
						return;

					bool TraverseChildren(int id)
					{
						var markerId = frameData?.GetItemMarkerID(id);
						if (markerId != null && pinnedItems.Contains(markerId.Value))
						{
							// TODO: save expanded state -> when scrubbing timeline and entry is not in list for one frame it defaults to unexpanded
							// if(Application.isPlaying)
								// tree.SetExpanded(id, true);
							return true;
						}

						var children = new List<int>();
						frameData?.GetItemChildren(id, children);
						var expand = false;
						if (children.Count >= 0)
						{
							foreach (var ch in children)
							{
								if (TraverseChildren(ch))
								{
									expand = true;
									break;
								}
							}
						}

						if (expand)
						{
							tree.SetExpanded(id, true);
						}

						return expand; 
					}
					
					TraverseChildren(root.id);
				}
			}
		}
	}
}