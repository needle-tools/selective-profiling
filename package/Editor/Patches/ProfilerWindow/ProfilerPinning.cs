using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using needle.EditorPatching;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	internal static class ProfilerPinning
	{
		private static readonly HashSet<int> pinnedItems = new HashSet<int>();

		public static bool HasPinnedItems() => pinnedItems != null && pinnedItems.Count > 0;

		public static bool IsPinned(int id)
		{
			return pinnedItems.Contains(id);
		}

		public static void Pin(TreeViewItem item)
		{
			if (!pinnedItems.Contains(item.id))
				pinnedItems.Add(item.id);
		}

		public static void Unpin(TreeViewItem item)
		{
			if (item == null) return;
			if (pinnedItems.Contains(item.id)) pinnedItems.Remove(item.id);

			if (item.hasChildren)
			{
				foreach (var ch in item.children)
					Unpin(ch);
			}
		}

		internal static bool IsChildOfAnyPinnedItem(TreeViewItem item, bool any = true, int level = 0)
		{
			if (item == null) return false;
			if (pinnedItems.Contains(item.id))
			{
				if (any) return true;
				if (level > 0) return true;
			}
			level += 1;
			return IsChildOfAnyPinnedItem(item.parent, any, level);
		}

		public static Color DimColor = new Color(.8f, .8f, .8f, 1);

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
					if (HasPinnedItems() && !pinnedItems.Contains(item.id))
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
						if (IsChildOfAnyPinnedItem(item))
						{
							if (!pinnedItems.Contains(item.id))
								pinnedItems.Add(item.id);
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