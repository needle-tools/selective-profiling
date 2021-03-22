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
		private static HierarchyFrameDataView GetFrameData(TreeViewItem item = null) => ProfilerFrameDataView_Patch.GetFrameDataView(item);

		public static bool AllowPinning()
		{
			return SelectiveProfilerSettings.instance.AllowPinning;
		}
		
		public static bool AllowPinning(TreeViewItem item, string name = null, HierarchyFrameDataView view = null)
		{
			if (!SelectiveProfilerSettings.instance.AllowPinning) return false;
			return true;
		}

		public static bool HasPinnedItems() => pinnedItems != null && pinnedItems.Count > 0;

		public static bool IsPinned(TreeViewItem item)
		{
			var fd = GetFrameData(item);
			var markerId = fd.GetItemMarkerID(item.id);
			return pinnedItems.Contains(markerId);
		}

		public static bool HasPinnedParent(TreeViewItem item) => IsChildOfAnyPinnedItem(item);

		public static void Pin(TreeViewItem item)
		{
			if (item != null)
			{
				var fd = GetFrameData(item);
				var markerId = fd.GetItemMarkerID(item.id);
				InternalPin(markerId, true, item);
			}
		}

		public static void Unpin(TreeViewItem item, int level = 0, bool completely = false)
		{
			if (item == null) return;
			
			var fd = GetFrameData(item);
			var markerId = fd.GetItemMarkerID(item.id);
			InternalUnpin(markerId, level <= 0, level <= 0, false, item);
			
			if (completely && item.hasChildren)
			{
				level += 1;
				foreach (var ch in item.children)
					Unpin(ch, level, true);
			}
		}

		private static void InternalPin(int id, bool save, TreeViewItem item)
		{
			if (!pinnedItems.Contains(id)) 
				pinnedItems.Add(id);
			var frameData = GetFrameData();
			var name = frameData.GetMarkerName(id);
			
			if (save)
			{
				var pinnedList = PinnedItems.PinnedProfilerItems;
				if (pinnedList != null && !pinnedList.Contains(name))  
					pinnedList.Add(name);
				PinnedItems.Save();
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
				
				PinnedItems.Save();
			}
		}

		private static bool IsChildOfAnyPinnedItem(TreeViewItem item, bool any = true, int level = 0)
		{
			if (item == null)
			{
				return false;
			}
			var fd = GetFrameData(item);
			var markerId = fd.GetItemMarkerID(item.id);
			if (pinnedItems.Contains(markerId))
			{
				if (any) return true;
				if (level > 0) return true;
			}


			level += 1;
			return IsChildOfAnyPinnedItem(item.parent, any, level);
		}

		private const float defaultDimValue = .5f;
		public static Color DimColor = new Color(defaultDimValue, defaultDimValue, defaultDimValue, 1);

		private static bool IsInit;
		
		private static void EnsureInit(TreeViewItem item)
		{
			if (IsInit) return;
			var fd = GetFrameData(item);
			if (fd == null || !fd.valid) return;
			IsInit = true;
			
			foreach (var name in PinnedItems.PinnedProfilerItems)
			{
				var id = fd.GetMarkerId(name);
				InternalPin(id, false, item);
			}
		}
		
		public class ProfilerPinning_Patch : EditorPatchProvider
		{
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

				private static Color previousColor;
				
				private static void Prefix(TreeViewItem item)
				{
					if (!AllowPinning()) return;
					previousColor = GUI.color;
					var fd = GetFrameData(item);
					var markerId = fd.GetItemMarkerID(item.id);
					if (HasPinnedItems() && !pinnedItems.Contains(markerId) && !IsChildOfAnyPinnedItem(item))
						GUI.color = DimColor;
				}

				private static void Postfix(Rect cellRect, TreeViewItem item)
				{
					if (!AllowPinning()) return;
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
					if (!AllowPinning()) return;
					var tree = __instance as TreeView;
					ExpandPinnedItems(tree, root);
				}

				private static void Postfix(ref IList<TreeViewItem> __result)
				{
					if (!AllowPinning()) return;
					var items = __result;
					EnsureInit(items.LastOrDefault(i => i.id >= 0));
					
					var inserted = 0;
					// var insertList = new List<TreeViewItem>();
					for (var i = 0; i < items.Count; i++)
					{
						var item = items[i];
						
						if (IsChildOfAnyPinnedItem(item))
						{
							var frameData = GetFrameData(item);
							var markerId = frameData.GetItemMarkerID(item.id);
							Debug.Log(frameData.GetMarkerName(markerId));
							// InternalPin(markerId, false, item); 
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