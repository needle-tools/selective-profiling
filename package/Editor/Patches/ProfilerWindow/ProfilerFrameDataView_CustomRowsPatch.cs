#define DEBUG_CUSTOMROWS
#undef DEBUG_CUSTOMROWS

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using needle.EditorPatching;
using Needle.SelectiveProfiling.Utils;
using UnityEditor;
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
			// patches.Add(new Profiler_BuildRows());
			// patches.Add(new FrameDataTreeViewItem_Init());
			patches.Add(new Profiler_GetItemName());
			patches.Add(new Profiler_CellGUI());
			patches.Add(new Profiler_BuildRows_HideProperties());
		}


		private class Profiler_BuildRows_HideProperties : EditorPatch
		{
			protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
			{
				var t = typeof(UnityEditorInternal.ProfilerDriver).Assembly.GetType("UnityEditorInternal.ProfilerFrameDataTreeView");
				// var m = t.GetMethod("AddAllChildren", (BindingFlags) ~0);
				var m = t.GetMethod("AddAllChildren", (BindingFlags) ~0);
				targetMethods.Add(m);
				return Task.CompletedTask;
			}

			private static void Postfix(TreeView __instance, IList<TreeViewItem> newRows, HierarchyFrameDataView ___m_FrameDataView)
			{
				if (newRows == null) return;
				ProfilerHelper.profilerTreeView = __instance;
				if (!SelectiveProfilerSettings.instance.HideProperties) return;
				var frame = ___m_FrameDataView;
				if (frame == null || !frame.valid) return;
				for (var index = newRows.Count - 1; index >= 0; index--)
				{
					var row = newRows[index];
					var name = frame.GetItemName(row.id);
					if (name.Contains("set ") || name.Contains("get "))
					{
						newRows.RemoveAt(index);
					}
				}
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

			private static GUIStyle style;

			private class State
			{
				public GUIContent Content;
			}

			private static bool Prefix(TreeView __instance, ref Rect cellRect, TreeViewItem item, int column, HierarchyFrameDataView ___m_FrameDataView, 
				out State __state)
			{
				__state = null;
				if (column != 0) return true;
				var frame = ___m_FrameDataView;
				if (frame == null || !frame.valid) return true;
				if (item.id > ParentIdOffset) return true;
				var itemName = frame.GetItemName(item.id);
				var separatorIndex = itemName.IndexOf(ProfilerSamplePatch.TypeSampleNameSeparator);
				if (separatorIndex < 0) return true;

				if (style == null)
				{
					style = new GUIStyle(EditorStyles.label);
					style.alignment = TextAnchor.MiddleRight;
					style.normal.textColor = Color.white;
					style.padding = new RectOffset(0, 0, 0, 2);
				}

				var name = itemName.Substring(0, separatorIndex);
				
				// skip is parent contains declaring type name
				var parent = item.parent;
				if (parent != null)
				{
					var parentName = frame.GetItemName(parent.id);
					if (parentName.Contains(name))
						return true;
				}

				
				// only show tooltip when item is presumably cut off
				var content = new GUIContent(name, item.depth > 10 ? null : name);
				__state = new State(){Content = content};

				// draw item on the right if depth < x
				// e.g. when searching
				if (item.depth == 0) 
				{
					// draw label right
					var col = GUI.color;
					GUI.color = __instance.IsSelected(item.id) ? Color.white : Color.gray;
					content.tooltip = null;
					GUI.Label(cellRect, content, style);
					GUI.color = col;
					__state = null;
					return true;
				}
				
				// indent everything
				// if (item.depth <= 0)
				// {
				// 	var width = style.CalcSize(content).x;
				// 	var rect = cellRect;
				// 	rect.x += 5;
				// 	rect.width = width;
				// 	// var padding = item.hasChildren ? 18 : 3;
				// 	// rect.x -= rect.width + padding;
				// 	style.alignment = TextAnchor.MiddleLeft;
				// 	var col = GUI.color;
				// 	GUI.color = __instance.IsSelected(item.id) ? Color.white : Color.gray;
				// 	content.tooltip = null;
				// 	GUI.Label(rect, content, style);
				// 	GUI.color = col;
				// 	cellRect.x += width - 10;
				// 	cellRect.width -= width;
				// 	__state = null;
				// }
				return true;
			}

			private static void Postfix(TreeView __instance, Rect cellRect, TreeViewItem item, State __state)
			{
				if (__state == null) return;
				
				var content = __state.Content;
				if (content != null)
				{
					var col = GUI.color;
					GUI.color = __instance.IsSelected(item.id) ? Color.white : Color.gray;
					var padding = item.hasChildren ? 18 : 3;
					var rect = cellRect;
					var width = style.CalcSize(content).x;
					rect.x -= width + padding;
					rect.width = width;
				
					if (rect.x < 0)
					{
						// draw cut off
						var prevAlignment = style.alignment;
						style.alignment = TextAnchor.MiddleLeft;
						rect.x = 5;
						rect.width = cellRect.x - rect.x - padding;
						GUI.Label(rect, content, style);
						style.alignment = prevAlignment;
						
						if (rect.Contains(Event.current.mousePosition))
						{
							var tt = typeof(GUIStyle).GetMethod("SetMouseTooltip", BindingFlags.NonPublic | BindingFlags.Static);
							tt?.Invoke(null, new object[] {content.text, new Rect(Event.current.mousePosition, Vector2.zero)});
						}
					}
					else
					{
						// draw normally
						GUI.Label(rect, content, style);
					}
					GUI.color = col;
				

				}
			}
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

			private static void Postfix(TreeViewItem __instance, HierarchyFrameDataView ___m_FrameDataView, string[] ___m_StringProperties)
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
									sum += (int) frame.GetItemColumnDataAsSingle(ch.id, i);
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
						if (SelectiveProfiler.DevelopmentMode) __result += " [CustomRow]";
						return false;
					}
				}
				else if (!SelectiveProfiler.DevelopmentMode)
				{
					// remove prefix
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
				public int OriginalDepth;
				public int Depth => Item?.depth ?? -1;
				public string Key;

				public TreeViewItem Item;
				// public List<TreeViewItem> Items = new List<TreeViewItem>(4);
			}

			private static readonly Stack<ItemData> stack = new Stack<ItemData>();

			private static void Postfix(object __instance, IList<TreeViewItem> newRows, List<int> newExpandedIds)
			{
				// if (!SelectiveProfilerSettings.instance.Enabled) return;
				// if (!SelectiveProfiler.AnyEnabled) return;

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
					var e = stack.Pop();
#if DEBUG_CUSTOMROWS
					Debug.Log("<b>POP</b> " + e.Key + ", " + e.Depth + "\n" + PrintStack());
#endif
				}

#if DEBUG_CUSTOMROWS
				string PrintStack() => "stack:\n" + string.Join("\n", stack.Select(s => s.Key + " - " + s.Depth)) + "\n\n\n";
#endif

				HierarchyFrameDataView frame = null;
				if (list != null && list.Count > 0)
				{
					stack.Clear();
					for (var index = 0; index < list.Count; index++)
					{
						var item = list[index];
						if (frame == null) frame = ProfilerFrameDataView_Patch.GetFrameDataView(item);
						if (frame == null || !frame.valid) break;
						// continue;

						var name = frame.GetItemName(item.id);

						// if item is leaving stack
						var separatorIndex = name.LastIndexOf(ProfilerSamplePatch.TypeSampleNameSeparator);

						while (stack.Count > 0 && (item.depth < stack.Peek().OriginalDepth))
						{
#if DEBUG_CUSTOMROWS
							Debug.Log(item.depth + " = " + (item.depth + stack.Count) + " < " + stack.Peek().OriginalDepth + ", " + name);
#endif
							PopStack();
						}

						if (separatorIndex > 0 || stack.Count > 0)
						{
							// Debug.Log(item.depth + " - " + name);

							var entry = stack.Count > 0 ? stack.Peek() : null;
							if (separatorIndex > 0)
							{
								var key = name.Substring(0, separatorIndex);
								// if (name == "GameObjectTreeViewGUI/DoItemGUI(Rect, int, TreeViewItem, bool, bool, bool)") continue;
								// check an entry is already injected
								// or current top entry prefix is different
								if (entry == null || entry.Key != key)
								{
									var newId = item.id + ParentIdOffset;
									var newItem = CreateNewItem(item, newId, item.depth + stack.Count);

									if (stack.All(e => treeView.IsExpanded(e.Item.id)))
									{
										list.Insert(index, newItem);
										index += 1;
									}

									if (item.parent.hasChildren)
										item.parent.children.Remove(item);
									item.parent.AddChild(newItem);

									entry = new ItemData();
									entry.Key = key;
									entry.OriginalDepth = item.depth;
									entry.Item = newItem;
									stack.Push(entry);
#if DEBUG_CUSTOMROWS
									Debug.Log("PUSH " + key + ", " + item.depth + " -> " + newItem.depth + ", prev: " + name + ", " + item.depth + "\n" +
									          PrintStack());
#endif
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
							if (!stack.All(e => treeView.IsExpanded(e.Item.id)))
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