// using System;
// using System.Collections;
// using System.Collections.Generic;
// using System.Linq;
// using System.Reflection;
// using System.Threading.Tasks;
// using HarmonyLib;
// using needle.EditorPatching;
// using UnityEditor;
// using UnityEditor.IMGUI.Controls;
// using UnityEditor.Profiling;
// using UnityEngine;
//
// // ReSharper disable UnusedMember.Local
//
// namespace Needle.SelectiveProfiling
// {
// 	internal static class ProfilerPinning
// 	{
// 		private static readonly HashSet<int> pinnedItems = new HashSet<int>();
// 		private static readonly HashSet<int> expandedItems = new HashSet<int>();
//
// 		private static HierarchyFrameDataView GetFrameData(TreeViewItem item = null) => ProfilerFrameDataView_Patch.GetFrameDataView(item);
//
// 		private static bool AllowPinning()
// 		{
// 			return SelectiveProfilerSettings.instance.AllowPinning && SelectiveProfiler.DevelopmentMode;
// 		}
//
// 		public static bool AllowPinning(TreeViewItem item, string name = null, HierarchyFrameDataView view = null)
// 		{
// 			if (!SelectiveProfilerSettings.instance.AllowPinning) return false;
// 			return true;
// 		}
//
// 		public static bool HasPinnedItems() => pinnedItems != null && pinnedItems.Count > 0;
//
// 		public static bool IsPinned(TreeViewItem item)
// 		{
// 			var markerId = GetId(item);
// 			return pinnedItems.Contains(markerId);
// 		}
//
// 		public static bool HasPinnedParent(TreeViewItem item) => IsChildOfAnyPinnedItem(item);
//
// 		public static void Pin(TreeViewItem item)
// 		{
// 			if (item != null)
// 			{
// 				var markerId = GetId(item);
// 				InternalPin(markerId, true, null);
// 			}
// 		}
//
// 		public static void Unpin(TreeViewItem item, int level = 0, bool completely = false)
// 		{
// 			if (item == null) return;
//
// 			var markerId = GetId(item);
// 			InternalUnpin(markerId, level <= 0, null);
//
// 			if (completely && item.hasChildren)
// 			{
// 				level += 1;
// 				foreach (var ch in item.children)
// 					Unpin(ch, level, true);
// 			}
// 		}
//
// 		internal static int GetId(TreeViewItem item)
// 		{
// 			var fd = GetFrameData(item);
// 			var markerId = fd.GetItemMarkerID(item.id);
// 			if (markerId == 0)
// 			{
// 				var name = fd.GetItemName(item.id);
// 				markerId = name.GetHashCode();
// 			}
// 			return markerId;
// 		}
//
// 		internal static int GetId(string name)
// 		{
// 			return name.GetHashCode();
// 		}
//
// 		private static void InternalPin(int id, bool save, string fallback)
// 		{
// 			if (id < 0) return;
// 			if (!pinnedItems.Contains(id))
// 				pinnedItems.Add(id);
// 			if (save)
// 			{
// 				var frameData = GetFrameData();
// 				string name;
// 				try
// 				{
// 					name = frameData.GetMarkerName(id);
// 				}
// 				catch (ArgumentException)
// 				{
// 					name = fallback;
// 				}
//
// 				if (string.IsNullOrEmpty(name))
// 				{
// 					throw new Exception("Name is null");
// 				}
//
// 				var pinnedList = PinnedItems.PinnedProfilerItems;
// 				if (pinnedList != null && !pinnedList.Contains(name))
// 					pinnedList.Add(name);
// 				PinnedItems.Save();
// 			}
// 		}
//
// 		private static void InternalUnpin(int id, bool save, string fallback)
// 		{
// 			if (pinnedItems.Contains(id))
// 				pinnedItems.Remove(id);
//
// 			if (save)
// 			{
// 				var frameData = GetFrameData();
// 				string name;
// 				try
// 				{
// 					name = frameData.GetMarkerName(id);
// 				}
// 				catch (ArgumentException)
// 				{
// 					name = fallback;
// 				}
//
// 				if (string.IsNullOrEmpty(name))
// 				{
// 					throw new Exception("Name is null");
// 				}
//
// 				var pinnedList = PinnedItems.PinnedProfilerItems;
// 				if (pinnedList != null && pinnedList.Contains(name))
// 					pinnedList.Remove(name);
//
// 				PinnedItems.Save();
// 			}
// 		}
//
// 		private static bool IsChildOfAnyPinnedItem(TreeViewItem item, bool any = true, int level = 0)
// 		{
// 			if (item == null)
// 			{
// 				return false;
// 			}
//
// 			var markerId = GetId(item);
// 			if (pinnedItems.Contains(markerId))
// 			{
// 				if (any) return true;
// 				if (level > 0) return true;
// 			}
//
//
// 			level += 1;
// 			return IsChildOfAnyPinnedItem(item.parent, any, level);
// 		}
//
// 		private const float defaultDimValue = .5f;
// 		public static Color DimColor = new Color(defaultDimValue, defaultDimValue, defaultDimValue, 1);
//
// 		private static bool IsInit;
//
// 		private static void EnsureInit(TreeViewItem item)
// 		{
// 			if (IsInit) return;
// 			var fd = GetFrameData(item);
// 			if (fd == null || !fd.valid) return;
// 			IsInit = true;
//
// 			foreach (var name in PinnedItems.PinnedProfilerItems)
// 			{
// 				var id = fd.GetMarkerId(name);
// 				if (id == 0)
// 					id = GetId(name);
// 				InternalPin(id, false, name);
// 			}
// 		}
//
//
// 		// patches:
//
// 		public class ProfilerPinning_Patch : EditorPatchProvider
// 		{
// 			protected override void OnGetPatches(List<EditorPatch> patches)
// 			{
// 				patches.Add(new Profiler_BuildRows());
// 				patches.Add(new Profiler_MigrateExpandedState());
// 				patches.Add(new Profiler_CellGUI());
// 				patches.Add(new Profiler_DoubleClick());
// 				patches.Add(new Profiler_StoreExpandedState());
// 			}
//
//
// 			private class Profiler_StoreExpandedState : EditorPatch
// 			{
// 				protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
// 				{
// 					var t = typeof(UnityEditorInternal.ProfilerDriver).Assembly.GetType("UnityEditorInternal.ProfilerFrameDataTreeView");
// 					var m = t.GetMethod("StoreExpandedState", (BindingFlags) ~0);
// 					targetMethods.Add(m);
// 					return Task.CompletedTask;
// 				}
//
// 				private static FieldInfo dictField;
// 				private static FieldInfo keyField, valueField;
//
// 				private static void Postfix(object ___m_ExpandedMarkersHierarchy,   HierarchyFrameDataView ___m_FrameDataView)
// 				{
// 					// if (___m_FrameDataView == null || !___m_FrameDataView.valid) return;
// 					//
// 					// if (___m_ExpandedMarkersHierarchy == null) return;
// 					// if (dictField == null)
// 					// {
// 					// 	dictField = ___m_ExpandedMarkersHierarchy.GetType().GetField("expandedMarkers", BindingFlags.Instance | BindingFlags.Public);
// 					// 	if (dictField == null) return;
// 					// }
// 					
// 					// var dict = (dictField.GetValue(___m_ExpandedMarkersHierarchy) as IEnumerable);
// 					// foreach (var entry in dict)
// 					// {
// 					// 	var key = (int)entry.GetType().GetField("key", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(entry);
// 					// 	var value = entry.GetType().GetField("value", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(entry);
// 					// 	Debug.Log(key + ", " + value);
// 					// }
// 				}
// 			}
//
// 			private class Profiler_BuildRows : EditorPatch
// 			{
// 				protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
// 				{
// 					var t = typeof(UnityEditorInternal.ProfilerDriver).Assembly.GetType("UnityEditorInternal.ProfilerFrameDataTreeView");
// 					var m = t.GetMethod("BuildRows", (BindingFlags) ~0);
// 					targetMethods.Add(m);
// 					return Task.CompletedTask;
// 				}
//
// 				private static bool Prefix(object __instance, TreeViewItem root)
// 				{
// 					if (!AllowPinning()) return true;
// 					var tree = __instance as TreeView;
// 					ExpandPinnedItems(tree, root);
// 					return true;
// 				}
//
// 				private static void Postfix(object __instance, ref IList<TreeViewItem> __result)
// 				{
// 					if (!AllowPinning()) return;
// 					var items = __result;
// 					EnsureInit(items.LastOrDefault(i => i.id >= 0));
// 					var inserted = 0;
//
// 					// var insertList = new List<TreeViewItem>();
// 					for (var i = 0; i < items.Count; i++)
// 					{
// 						var item = items[i];
//
// 						if (IsChildOfAnyPinnedItem(item))
// 						{
// 							// var frameData = GetFrameData(item);
// 							// var markerId = frameData.GetItemMarkerID(item.id);
// 							// InternalPin(markerId, false, item); 
// 							items.RemoveAt(i);
// 							items.Insert(inserted, item);
// 							// insertList.Add(item);
// 							++inserted;
// 						}
// 					}
//
//
// 					if (items.Count > 0)
// 					{
// 						var tree = __instance as TreeView;
// 						// ExpandPinnedItems(tree, root);
// 						var sel = tree?.GetSelection().FirstOrDefault();
// 						if (sel != null && items.Any(i => i.id == sel))
// 						{
// 							tree.FrameItem(sel.Value);
// 						}
// 					}
// 					// if (sel >= 0)
//
// 					// for (var i = items.Count - 1; i >= 0; i--)
// 					// {
// 					// 	if (!IsChildOfAnyPinnedItem(items[i]))
// 					// 		items.RemoveAt(i);
// 					// }
//
// 					// for (var index = insertList.Count - 1; index >= 0; index--)
// 					// {
// 					// 	var it = insertList[index];
// 					// 	items.Insert(0, it);
// 					// }
// 				}
//
//
// 				private static void ExpandPinnedItems(TreeView tree, TreeViewItem root)
// 				{
// 					if (tree == null) return;
// 					var frameData = GetFrameData(root);
// 					if (frameData == null || !frameData.valid)
// 						return;
//
// 					bool TraverseChildren(int id)
// 					{
// 						var markerId = frameData?.GetItemMarkerID(id);
// 						if (markerId != null && pinnedItems.Contains(markerId.Value))
// 						{
// 							// TODO: save expanded state -> when scrubbing timeline and entry is not in list for one frame it defaults to unexpanded
// 							// if(Application.isPlaying)
// 							if (!expandedItems.Contains(id))
// 							{
// 								expandedItems.Add(id);
// 								tree.SetExpanded(id, true);
// 							}
//
// 							return true;
// 						}
//
// 						var children = new List<int>();
// 						frameData?.GetItemChildren(id, children);
// 						var expand = false;
// 						if (children.Count >= 0)
// 						{
// 							foreach (var ch in children)
// 							{
// 								if (TraverseChildren(ch))
// 								{
// 									expand = true;
// 									// break;
// 								}
// 							}
// 						}
//
// 						if (expand)
// 						{
// 							tree.SetExpanded(id, true);
// 						}
//
// 						if (tree.IsExpanded(id))
// 						{
// 							if (!expandedItems.Contains(id))
// 								expandedItems.Add(id);
// 						}
//
// 						return expand;
// 					}
//
// 					TraverseChildren(root.id);
//
// 					// tree.GetType().GetMethod("StoreExpandedState", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(tree, null);
// 				}
// 			}
//
// 			private class Profiler_CellGUI : EditorPatch
// 			{
// 				// https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/ProfilerFrameDataTreeView.cs#L647
// 				protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
// 				{
// 					var t = typeof(UnityEditorInternal.ProfilerDriver).Assembly.GetType("UnityEditorInternal.ProfilerFrameDataTreeView");
// 					var m = t.GetMethod("CellGUI", (BindingFlags) ~0);
// 					targetMethods.Add(m);
// 					return Task.CompletedTask;
// 				}
//
// 				private static Color previousColor;
//
// 				private static void Prefix(TreeViewItem item)
// 				{
// 					if (!AllowPinning()) return;
// 					previousColor = GUI.color;
// 					var fd = GetFrameData(item);
// 					var markerId = fd.GetItemMarkerID(item.id);
// 					if (HasPinnedItems() && !pinnedItems.Contains(markerId) && !IsChildOfAnyPinnedItem(item))
// 						GUI.color = DimColor;
// 				}
//
// 				private static void Postfix(object __instance, Rect cellRect, TreeViewItem item, int column, HierarchyFrameDataView ___m_FrameDataView)
// 				{
// 					if (!AllowPinning()) return;
// 					GUI.color = previousColor;
// 					
// 					if (column == 0 && IsPinned(item))
// 					{
// 						var size = cellRect.height * .6f;
// 						var y = (cellRect.height - size) * .5f;
// 						var rect = new Rect(cellRect.x + cellRect.width - size, cellRect.y + y, size, size);
// 						GUI.DrawTexture(rect, Textures.Pin, ScaleMode.ScaleAndCrop, true);
//
// 					}
//
// 					if (column == 0)
// 					{
// 						var cr = cellRect;
// 						cr.x = cr.x + cr.width - 100;
// 						var markerId = ___m_FrameDataView.GetItemMarkerID(item.id);
// 						if (markerId == 0)
// 						{
// 							var name = ___m_FrameDataView.GetItemName(item.id);
// 							markerId = name.GetHashCode();
// 						}
// 						GUI.Label(cr, markerId.ToString());
// 					}
// 				}
// 			}
//
// 			private class Profiler_DoubleClick : EditorPatch
// 			{
// 				protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
// 				{
// 					var t = typeof(UnityEditorInternal.ProfilerDriver).Assembly.GetType("UnityEditorInternal.ProfilerFrameDataTreeView");
// 					var m = t.GetMethod("DoubleClickedItem", (BindingFlags) ~0);
// 					targetMethods.Add(m);
// 					return Task.CompletedTask;
// 				}
//
// 				private static void Postfix(int id, object __instance)
// 				{
// 					var frameData = GetFrameData();
// 					if (frameData == null || !frameData.valid) return;
// 					var name = frameData.GetItemName(id);
// 					var markerId = frameData.GetItemMarkerID(id);
// 					if (pinnedItems.Contains(markerId))
// 						InternalUnpin(markerId, !Application.isPlaying, name);
// 					else InternalPin(markerId, !Application.isPlaying, name);
//
// 					if (__instance is TreeView view) view.Reload();
// 				}
// 			}
//
//
// 			private class Profiler_MigrateExpandedState : EditorPatch
// 			{
// 				protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
// 				{
// 					var t = typeof(UnityEditorInternal.ProfilerDriver).Assembly.GetType("UnityEditorInternal.ProfilerFrameDataTreeView").BaseType;
// 					var m = t?.GetMethod("IsExpanded", (BindingFlags) ~0);
// 					targetMethods.Add(m);
// 					return Task.CompletedTask;
// 				}
//
// 				private static void Postfix(object __instance, int id, ref bool __result)
// 				{
// 					if (!__instance.GetType().Name.EndsWith("ProfilerFrameDataTreeView")) return;
// 					if (Application.isPlaying && !__result && expandedItems.Contains(id)) __result = true;
// 				}
// 			}
// 		}
// 	}
// }