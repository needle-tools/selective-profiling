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
using UnityEngine;

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
			patches.Add(new FrameDataTreeViewItem_Init());
		}
		
		/// <summary>
		/// used to distinguish between injected item and actual item
		/// you can not query profiler frame data for injected parents because those samples dont really exist
		/// they are just to group related samples
		/// e.g. when injecting in a script called by some editor event the resulting samples may scattered together with other samples created from totally unrelated subscribers
		/// like in SceneHierarchyWindow.OnGUI.repaint
		/// </summary>
		internal const int ParentIdOffset = 1_000_000;

		private class FrameDataTreeViewItem_Init : EditorPatch
		{
			protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
			{
				var asm = typeof(FrameDataView).Assembly;
				var t = asm.GetTypes().FirstOrDefault(_t => _t.Name == "FrameDataTreeViewItem");
				var m = t.GetMethod("Init", BindingFlags.Public | BindingFlags.Instance);
				targetMethods.Add(m);
				return Task.CompletedTask;
			}

			private static void Postfix(TreeViewItem __instance, HierarchyFrameDataView ___m_FrameDataView, string[] ___m_StringProperties )
			{
				var frame = ___m_FrameDataView;

				if (frame != null && frame.valid && __instance.id > ParentIdOffset)
				{
					var id = __instance.id - ParentIdOffset;
					var name = frame.GetItemName(id);
					var self = __instance;
					var children = self.children;
					var props = ___m_StringProperties;
					if (children == null) return;
					for (int i = 1; i < props.Length; i++)
					{
						var sum = 0f;
						switch (i)
						{
							case 1: // Total
							case 2: // Self
								foreach (var ch in children)
									sum += frame.GetItemColumnDataAsFloat(ch.id, i);
								props[i] = sum.ToString("0.0") + "%";
								break;
							case 3: // Calls
								foreach (var ch in children)
									sum += (int)frame.GetItemColumnDataAsSingle(ch.id, i);
								props[i] = sum.ToString("0");
								break;
							case 4: // GC alloc
								foreach (var ch in children)
									sum += frame.GetItemColumnDataAsFloat(ch.id, i);
								if (sum > 1000)
									props[i] = (sum / 1000).ToString("0.0") + " KB";
								else props[i] = sum.ToString("0") + " B";
								break;
							case 5: // Time ms
								foreach (var ch in children) 
									sum += frame.GetItemColumnDataAsFloat(ch.id, i);
								props[i] = sum.ToString("0.00");
								break;
							
						}
					}
				}
			}
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


			private static bool Prefix(out string __result, HierarchyFrameDataView frameData, int itemId)
			{
				if (frameData == null || !frameData.valid)
				{
					__result = null;
					return true;
				}
				
				var separatorStr = ProfilerSamplePatch.TypeSampleNameSeparator;
				if (itemId > ParentIdOffset)
				{
					var name = frameData.GetItemName(itemId - 1_000_000);
					var separator = name.LastIndexOf(separatorStr);
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
					var separator = name.LastIndexOf(separatorStr);
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
				var m = t.GetMethod("AddAllChildren", (BindingFlags) ~0);
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

			private static void Postfix(object __instance,  IList<TreeViewItem> newRows, List<int> newExpandedIds)
			{
				if (!SelectiveProfilerSettings.instance.Enabled) return;
				if (!SelectiveProfiler.AnyEnabled) return;
				
				var treeView = __instance as TreeView;
				if (treeView == null) return;
				var list = newRows;

				TreeViewItem CreateNewItem(TreeViewItem item, int newId, int depth)
				{
					var itemType = item.GetType();
					var typeName = itemType.FullName;
					var assemblyName = itemType.Assembly.FullName;
					if (string.IsNullOrEmpty(typeName)) return null;
					// creating FrameDataTreeViewItem
					// https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/ProfilerFrameDataTreeView.cs#L68
					var newItem = Activator.CreateInstance(AppDomain.CurrentDomain, assemblyName, typeName,
							true, AccessUtils.All,
							null, new object[] {ProfilerFrameDataView_Patch.GetFrameDataView(item), newId, depth, item.parent}, CultureInfo.CurrentCulture,
							null)
						.Unwrap() as TreeViewItem;
					return newItem;
				}

				void PopStack()
				{
					stack.Pop();
				}

				HierarchyFrameDataView fdv = null;
				if (list != null && list.Count > 0)
				{
					stack.Clear();
					for (var index = 0; index < list.Count; index++)
					{
						var item = list[index];
						if (fdv == null) fdv = ProfilerFrameDataView_Patch.GetFrameDataView(item);

						// if item is leaving stack
						while (stack.Count > 0 && item.depth + 1 < stack.Peek().Depth)
						{
							PopStack();
						}

						var name = fdv.GetItemName(item.id);
						var separatorIndex = name.LastIndexOf(ProfilerSamplePatch.TypeSampleNameSeparator);
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
									var newId = item.id + ParentIdOffset;
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
									// treeView.SetExpanded(newItem.id, true);
								}
							}

							entry = stack.Peek();
							item.depth += stack.Count;
							var parent = entry.Item;

							// check that item should still be a child of the injected parent
							if (item.depth <= parent.depth)
							{
								item.depth -= stack.Count;
								PopStack();
								continue;
							}

							// only add direct children (not children of children)
							if (item.depth - parent.depth <= 1)
							{
								parent.AddChild(item);
							}
							
							// remove from list if any item in the current inject parent stack is not expanded
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