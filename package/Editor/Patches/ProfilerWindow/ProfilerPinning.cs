﻿using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using needle.EditorPatching;
using UnityEditor.IMGUI.Controls;
using UnityEditor.Profiling;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	internal static class ProfilerPinning
	{
		private static readonly HashSet<int> pinnedItems = new HashSet<int>();
		private static readonly HashSet<int> unpinnedItems = new HashSet<int>();
		private static HierarchyFrameDataView GetFrameData(TreeViewItem item) => ProfilerFrameDataView_Patch.GetFrameDataView(item);

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
			InternalUnpin(markerId, level <= 0, level <= 0, false);
			
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
			
			if (unpinnedItems.Contains(id))
			{
				unpinnedItems.Remove(id);
				var unpinnedList = PinnedItems.instance.UnpinnedProfilerItems;
				if (unpinnedList != null && unpinnedList.Contains(id))
				{
					unpinnedList.Remove(id); 
					PinnedItems.instance.Save();
				}
			}
			
			if (save)
			{
				var pinnedList = PinnedItems.instance.PinnedProfilerItems;
				if (pinnedList != null && !pinnedList.Contains(id))  
					pinnedList.Add(id);
				var unpinnedList = PinnedItems.instance.UnpinnedProfilerItems;
				if (unpinnedList != null && unpinnedList.Contains(id))
					unpinnedList.Remove(id);
				
				PinnedItems.instance.Save();
			}
		}

		private static void InternalUnpin(int id, bool save, bool saveUnpinned, bool removeFromUnpinned)
		{
			if (pinnedItems.Contains(id))
				pinnedItems.Remove(id);
			
			if(saveUnpinned && !unpinnedItems.Contains(id))
				unpinnedItems.Add(id);
			else if (removeFromUnpinned && unpinnedItems.Contains(id))
				unpinnedItems.Remove(id);
			
			if (save)
			{
				var pinnedList = PinnedItems.instance.PinnedProfilerItems;
				if (pinnedList != null && pinnedList.Contains(id))
					pinnedList.Remove(id);
				var unpinnedList = PinnedItems.instance.UnpinnedProfilerItems;
				if (unpinnedList != null && !unpinnedList.Contains(id))
					unpinnedList.Add(id);
				PinnedItems.instance.Save();  
			}
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
		
		public static Color DimColor = new Color(.8f, .8f, .8f, 1);

		private static bool IsInit;
		
		private static void EnsureInit(HierarchyFrameDataView hierarchy)
		{
			if (IsInit) return;
			IsInit = true;
			var settings = PinnedItems.instance;
			if (settings.PinnedProfilerItems != null)
			{
				foreach (var id in settings.PinnedProfilerItems)
				{
					InternalPin(id, false);
				}

				foreach (var id in settings.UnpinnedProfilerItems)
				{
					InternalUnpin(id, false, true, false);
				}
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

			internal class Profiler_BuildRows : EditorPatch
			{
				protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
				{
					var t = typeof(UnityEditorInternal.ProfilerDriver).Assembly.GetType("UnityEditorInternal.ProfilerFrameDataTreeView");
					var m = t.GetMethod("BuildRows", (BindingFlags) ~0);
					targetMethods.Add(m);
					return Task.CompletedTask;
				}

				private static void Postfix(object __instance,
					ref IList<TreeViewItem> __result,
					ref List<TreeViewItem> ___m_Rows,
					ref List<TreeViewItem> ___m_RowsPool)
				{
					if (__result.Count <= 0) return;
					
					var items = __result;

					var inserted = 0;
					for (var i = 0; i < items.Count; i++)
					{
						var item = items[i];
						var fdv = ProfilerFrameDataView_Patch.GetFrameDataView(item);
						if(!IsInit) EnsureInit(fdv);
						
						if (IsChildOfAnyPinnedItem(item))
						{
							var markerId = fdv.GetItemMarkerID(item.id);
							InternalPin(markerId, false); 
							items.RemoveAt(i);
							items.Insert(inserted, item);
							++inserted;
						}
					}
				}
			}
		}
	}
}