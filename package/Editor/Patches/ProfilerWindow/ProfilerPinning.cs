using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using needle.EditorPatching;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.Profiling;
using UnityEngine;

// ReSharper disable UnusedMember.Local

namespace Needle.SelectiveProfiling
{
	internal static class ProfilerPinning
	{
		private static readonly HashSet<int> pinnedItems = new HashSet<int>();
		private static readonly HashSet<int> expandedItems = new HashSet<int>();

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
				InternalPin(markerId, true);
			}
		}

		public static void Unpin(TreeViewItem item, int level = 0, bool completely = false)
		{
			if (item == null) return;

			var fd = GetFrameData(item);
			var markerId = fd.GetItemMarkerID(item.id);
			InternalUnpin(markerId, level <= 0);

			if (completely && item.hasChildren)
			{
				level += 1;
				foreach (var ch in item.children)
					Unpin(ch, level, true);
			}
		}

		private static void InternalPin(int id, bool save)
		{
			if (id < 0) return;
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

		private static void InternalUnpin(int id, bool save)
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
				InternalPin(id, false);
			}
		}


		// patches:

		public class ProfilerPinning_Patch : EditorPatchProvider
		{
			protected override void OnGetPatches(List<EditorPatch> patches)
			{
				patches.Add(new Profiler_BuildRows());
				patches.Add(new Profiler_MigrateExpandedState());
				patches.Add(new Profiler_CellGUI());
				patches.Add(new Profiler_DoubleClick());
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

				private static bool Prefix(object __instance, TreeViewItem root)
				{
					if (!AllowPinning()) return true;
					var tree = __instance as TreeView;
					ExpandPinnedItems(tree, root);
					return true;
				}

				private static void Postfix(object __instance, ref IList<TreeViewItem> __result)
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
							// var frameData = GetFrameData(item);
							// var markerId = frameData.GetItemMarkerID(item.id);
							// InternalPin(markerId, false, item); 
							items.RemoveAt(i);
							items.Insert(inserted, item);
							// insertList.Add(item);
							++inserted;
						}
					}


					if (items.Count > 0)
					{
						var tree = __instance as TreeView;
						// ExpandPinnedItems(tree, root);
						var sel = tree?.GetSelection().FirstOrDefault();
						if (sel != null && items.Any(i => i.id == sel))
						{
							tree.FrameItem(sel.Value);
						}
					}
					// if (sel >= 0)

					// for (var i = items.Count - 1; i >= 0; i--)
					// {
					// 	if (!IsChildOfAnyPinnedItem(items[i]))
					// 		items.RemoveAt(i);
					// }

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
							if (!expandedItems.Contains(id))
							{
								expandedItems.Add(id);
								tree.SetExpanded(id, true);
							}

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
									// break;
								}
							}
						}

						if (expand)
						{
							tree.SetExpanded(id, true);
						}

						if (tree.IsExpanded(id))
						{
							if (!expandedItems.Contains(id))
								expandedItems.Add(id);
						}

						return expand;
					}

					TraverseChildren(root.id);

					// tree.GetType().GetMethod("StoreExpandedState", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(tree, null);
				}
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

				private static void Postfix(object __instance, Rect cellRect, TreeViewItem item, int column)
				{
					if (!AllowPinning()) return;
					GUI.color = previousColor;

					// if (column == 0)
					// {
					// 	var r = new Rect(cellRect);
					// 	r.x += 250;
					// 	EditorGUI.LabelField(r, item.id.ToString());
					// }

					if (column <= 0 && IsPinned(item))
					{
						var size = cellRect.height * .6f;
						var y = (cellRect.height - size) * .5f;
						var rect = new Rect(cellRect.x + cellRect.width - size, cellRect.y + y, size, size);
						GUI.DrawTexture(rect, Textures.Pin, ScaleMode.ScaleAndCrop, true);

						// if (Event.current.type == EventType.MouseUp && rect.Contains(Event.current.mousePosition))
						// {
						// 	Unpin(item); 
						// }
					}
				}
			}

			private class Profiler_DoubleClick : EditorPatch
			{
				protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
				{
					var t = typeof(UnityEditorInternal.ProfilerDriver).Assembly.GetType("UnityEditorInternal.ProfilerFrameDataTreeView");
					var m = t.GetMethod("DoubleClickedItem", (BindingFlags) ~0);
					targetMethods.Add(m);
					return Task.CompletedTask;
				}

				private static void Postfix(int id, object __instance)
				{
					var frameData = GetFrameData();
					if (frameData == null || !frameData.valid) return;
					var markerId = frameData.GetItemMarkerID(id);
					if (pinnedItems.Contains(markerId))
						InternalUnpin(markerId, !Application.isPlaying);
					else InternalPin(markerId, !Application.isPlaying);

					if (__instance is TreeView view) view.Reload();
				}
			}


			private class Profiler_MigrateExpandedState : EditorPatch
			{
				protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
				{
					var t = typeof(UnityEditorInternal.ProfilerDriver).Assembly.GetType("UnityEditorInternal.ProfilerFrameDataTreeView").BaseType;
					var m = t?.GetMethod("IsExpanded", (BindingFlags) ~0);
					targetMethods.Add(m);
					return Task.CompletedTask;
				}

				private static void Postfix(object __instance, int id, ref bool __result)
				{
					if (!__instance.GetType().Name.EndsWith("ProfilerFrameDataTreeView")) return;
					if (Application.isPlaying && !__result && expandedItems.Contains(id)) __result = true;
				}
			}
		}
	}
}