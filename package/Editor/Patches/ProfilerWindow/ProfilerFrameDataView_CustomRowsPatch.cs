using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using needle.EditorPatching;
using Needle.SelectiveProfiling.Utils;
using UnityEditor.IMGUI.Controls;
using UnityEditor.Profiling;
using UnityEditorInternal;

namespace Needle.SelectiveProfiling
{
	// ReSharper disable once UnusedType.Global
	/// <summary>
	/// injecting parent hierarchy items
	/// </summary>
	public class ProfilerFrameDataView_CustomRowsPatch : EditorPatchProvider
	{
		protected override void OnGetPatches(List<EditorPatch> patches)
		{
			patches.Add(new Profiler_BuildRows());
			patches.Add(new Profiler_GetItemName());
		}

		private class Profiler_GetItemName : EditorPatch
		{
			protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
			{
				var asm = typeof(EventMarker).Assembly;
				var t = asm.GetType("UnityEditorInternal.Profiling.CPUorGPUProfilerModule");
				var m = t.GetMethod("GetItemName", AccessUtils.AllDeclared);
				targetMethods.Add(m);
				return Task.CompletedTask;
			}

			internal const int ParentIdOffset = 1_000_000;

			private static bool Prefix(out string __result, HierarchyFrameDataView frameData, int itemId)
			{
				if (itemId > ParentIdOffset)
				{
					var name = frameData.GetItemName(itemId - 1_000_000);
					var separator = name.LastIndexOf(ProfilerSamplePatch.TypeSampleNameSeparator, StringComparison.Ordinal);
					if (separator > 0 && separator < name.Length)
					{
						__result = name.Substring(0, separator);
						return false;
					}
				}
				// remove prefix
				else if (!SelectiveProfiler.DebugLog)
				{
					var name = frameData.GetItemName(itemId);
					var separator = name.LastIndexOf(ProfilerSamplePatch.TypeSampleNameSeparator, StringComparison.Ordinal);
					if (separator > 0 && separator < name.Length)
					{
						__result = name.Substring(separator + 1);
						return false;
					}
				}

				__result = null;
				return true;
			}
		}

		private class Profiler_BuildRows : EditorPatch
		{
			protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
			{
				var t = typeof(UnityEditorInternal.ProfilerDriver).Assembly.GetType("UnityEditorInternal.ProfilerFrameDataTreeView");
				// var m = t.GetMethod("AddAllChildren", (BindingFlags) ~0);
				var m = t.GetMethod("BuildRows", (BindingFlags) ~0);
				targetMethods.Add(m);
				return Task.CompletedTask;
			}

			private class ItemData
			{
				public int Depth => Item?.depth ?? -1;
				public string Key;

				public TreeViewItem Item;
				// public List<TreeViewItem> Items = new List<TreeViewItem>(4);
			}

			private static readonly Stack<ItemData> stack = new Stack<ItemData>();

			private static void Postfix(object __instance, ref IList<TreeViewItem> __result)
			{
				var treeView = __instance as TreeView;
				if (treeView == null) return;
				var list = __result;

				TreeViewItem CreateNewItem(TreeViewItem item, int newId, int depth)
				{
					var itemType = item.GetType();
					var typeName = itemType.FullName;
					var assemblyName = itemType.Assembly.FullName;
					if (string.IsNullOrEmpty(typeName)) return null;
					var newItem = Activator.CreateInstance(AppDomain.CurrentDomain, assemblyName, typeName,
							true, AccessUtils.All,
							null, new object[] {ProfilerFrameDataView_Patch.GetFrameDataView(item), newId, depth, item.parent}, CultureInfo.CurrentCulture,
							null)
						.Unwrap() as TreeViewItem;
					return newItem;
				}

				HierarchyFrameDataView fdv = null;
				if (list != null && list.Count > 0)
				{
					stack.Clear();
					for (var index = 0; index < list.Count; index++)
					{
						var item = list[index];
						if (fdv == null) fdv = ProfilerFrameDataView_Patch.GetFrameDataView(item);

						while (stack.Count > 0 && item.depth + 1 < stack.Peek().Depth)
						{
							stack.Pop();
						}

						var name = fdv.GetItemName(item.id);
						var separatorIndex = name.LastIndexOf(ProfilerSamplePatch.TypeSampleNameSeparator, StringComparison.Ordinal);
						if (separatorIndex > 0 || stack.Count > 0)
						{
							var entry = stack.Count > 0 ? stack.Peek() : null;
							if (separatorIndex > 0)
							{
								var key = name.Substring(0, separatorIndex);
								// check an entry is already injected
								// or current top entry prefix is different
								if (entry == null || entry.Key != key)
								{
									var newId = item.id + Profiler_GetItemName.ParentIdOffset;
									var newItem = CreateNewItem(item, newId, item.depth + stack.Count);
									if (entry == null || treeView.IsExpanded(entry.Item.id))
									{
										list.Insert(index, newItem);
										index += 1;
									}

									entry = new ItemData();
									entry.Key = key;
									entry.Item = newItem;
									stack.Push(entry);
								}
							}

							entry = stack.Peek();
							item.depth += stack.Count;
							var injected = entry.Item;

							// check that item should still be a child of the injected parent
							if (item.depth <= injected.depth)
							{
								item.depth -= stack.Count;
								stack.Pop();
								continue;
							}

							injected.AddChild(item);

							if (stack.Any(e => !treeView.IsExpanded(e.Item.id)))
							{
								list.RemoveAt(index);
								index -= 1;
							}
						}
					}
				}
			}
		}
	}
}