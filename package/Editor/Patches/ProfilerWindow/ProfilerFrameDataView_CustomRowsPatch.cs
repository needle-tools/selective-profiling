#define DEBUG_CUSTOMROWS
#undef DEBUG_CUSTOMROWS

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
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
			patches.Add(new Profiler_BuildRows_CollapseItems());
		}


		private static readonly Dictionary<int, string> customRowsInfo = new Dictionary<int, string>();
		private const int collapsedRowIdOffset = 10_000_000;
		private const string k_AllItemsAreCollapsedHint = "HINT::";

		/// <summary>
		/// used to distinguish between injected item and actual item
		/// you can not query profiler frame data for injected parents because those samples dont really exist
		/// they are just to group related samples
		/// e.g. when injecting in a script called by some editor event the resulting samples may scattered together with other samples created from totally unrelated subscribers
		/// like in SceneHierarchyWindow.OnGUI.repaint
		/// </summary>
		internal const int parentIdOffset = 1_000_000;


		private class Profiler_BuildRows_CollapseItems : EditorPatch
		{
			protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
			{
				var t = typeof(UnityEditorInternal.ProfilerDriver).Assembly.GetType("UnityEditorInternal.ProfilerFrameDataTreeView");
				var m = t.GetMethod("AddAllChildren", (BindingFlags) ~0);
				targetMethods.Add(m);
				return Task.CompletedTask;
			}
			
			private static int currentDepthOffset;
			private static HierarchyFrameDataView frameDataView;

			private static TreeViewItem CreateNewItem(TreeViewItem parent, int newId, int depth)
			{
				var itemType = parent.GetType();
				var typeName = itemType.FullName;
				var assemblyName = itemType.Assembly.FullName;
				if (string.IsNullOrEmpty(typeName)) return null;
				// creating FrameDataTreeViewItem
				// https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/ProfilerFrameDataTreeView.cs#L68
				var newItem = Activator.CreateInstance(AppDomain.CurrentDomain, assemblyName, typeName,
						true, AccessUtils.All,
						null, new object[] {frameDataView, newId, depth, parent}, CultureInfo.CurrentCulture,
						null)
					.Unwrap() as TreeViewItem;
				return newItem;
			}

			private static TreeViewItem CreateAndInsertNewItem(TreeView tree, IList<TreeViewItem> list, int insertAt, ref int index, int id, int depth, TreeViewItem parent, 
				string text, Func<bool> insert = null)
			{
				id += collapsedRowIdOffset;
				var item = CreateNewItem(parent, id, depth);

				// add always an item to avoid having empty lists
				// which results in ArgumentException when expanding empty items (info is still stored in profiler state)
				if (tree.IsExpanded(parent.id) || (insert == null || insert.Invoke()))
				{
					// var diff = startCount - list.Count;
					list.Insert(insertAt, item);
					index += 1;
				}

				parent.AddChild(item);
				if (!customRowsInfo.ContainsKey(id))
					customRowsInfo.Add(id, k_AllItemsAreCollapsedHint + text);
				return item;
			}


			private interface ICollapseHandler
			{
				bool TryResolve(TreeView tree, TreeViewItem row, IList<TreeViewItem> list, ref int index);
				bool ShouldCollapse(TreeView tree, TreeViewItem item, string name, IList<TreeViewItem> list, ref int index);
			}

			private class CollapseProperties : ICollapseHandler
			{
				private int removedProperties;

				private readonly TreeViewItem parent;
				private int depth => parent.depth + 1;
				private int startCount;
				// we could also accumulate the GC ect data to display it

				public CollapseProperties(int currentCount, TreeViewItem parent)
				{
					this.parent = parent;
					startCount = currentCount;
				}

				public bool TryResolve(TreeView tree, TreeViewItem row, IList<TreeViewItem> list, ref int index)
				{
					// if we step out consider this to be done
					if (depth <= row.depth - (currentDepthOffset)) return false;
					
					var collapsed = row.id;
					
					// Debug.Log(parent.displayName);
					var id = parent.id;// ?? -1;
					if (id >= 0 && !customRowsInfo.ContainsKey(id))
					{
						customRowsInfo.Add(id, $"{removedProperties} hidden");
						
						CreateAndInsertNewItem(tree, list, index, ref index, collapsed, depth, parent, "All items have been collapsed", 
							() => !parent.hasChildren || parent.children.Count <= 0
							);
					}

					return true;
				}

				public bool ShouldCollapse(TreeView tree, TreeViewItem item, string name, IList<TreeViewItem> list, ref int index)
				{
					// only remove on same depth level
					if (item.depth == depth && (name.Contains("set ") || name.Contains("get ")))
					{
						removedProperties += 1;
						return true;
					}

					return false;
				}
			}

			private class CollapseRows : ICollapseHandler
			{
				private readonly Stack<int> collapsedDepth = new Stack<int>();
				private readonly HashSet<string> itemsToCollapse;
				internal static readonly HashSet<int> expanded = new HashSet<int>();
				private List<string> collapsedItems = new List<string>();
				
				private int firstIndex = 0;

				public CollapseRows(HashSet<string> itemsToCollapse)
				{
					this.itemsToCollapse = itemsToCollapse;
				}
				
				public bool TryResolve(TreeView tree, TreeViewItem row, IList<TreeViewItem> list, ref int index)
				{
					// Debug.Log("Test " + row.displayName + "\n" + string.Join("\n", collapsedDepth));
					while (collapsedDepth.Count > 0 && row.depth <= collapsedDepth.Peek())
					{
						// Debug.Log("Pop " + row.displayName + ", " + row.depth + ", " + collapsedDepth.Peek());
						collapsedDepth.Pop();
						currentDepthOffset -= 1;
					}
					var res = collapsedDepth.Count <= 0;


					if (res)
					{
						// NOTE: first index might change
						// it is possible that items have been inserted in between so this is likely to break
						CreateAndInsertNewItem(tree, list, firstIndex, ref index, row.id, row.depth, row.parent, "Collapsed " + collapsedItems.Count + " rows");
					}
					else
					{
						var offset = collapsedDepth.Count;
						row.depth -= offset;
					}
					
					return res;
				}

				public bool ShouldCollapse(TreeView tree, TreeViewItem item, string name, IList<TreeViewItem> list, ref int index)
				{
					var collapse = itemsToCollapse.Contains(name);
					if (collapse)
					{
						collapsedItems.Add(name);
						collapsedDepth.Push(item.depth + collapsedDepth.Count);
						currentDepthOffset += 1;

						if (firstIndex == 0)
						{
							firstIndex = index;
						}
						// only expand on first discovery
						// that allows users to collapse hierarchies again
						// otherwise they would always be re-opened
						// var key = item.id + name.GetHashCode();
						var key = item.id; 
						if (!expanded.Contains(key)) 
						{
							RequestReload(tree);
							expanded.Add(key);
							
							tree.SetExpanded(item.id, true);
							if (item.hasChildren)
							{
								foreach(var ch in item.children)
									if (ch != null)
										tree.SetExpanded(ch.id, true);
							} 
						}
						
						
					}

					return collapse;
				}

				// need to request reload, otherwise expanded children would not be visible
				// they're only in the rows list if expanded
				// private static bool requested;
				private static async void RequestReload(TreeView tree)
				{
					// if (requested) return;
					// requested = true;
					await Task.Delay(1);  
					tree.Reload();
					tree.SetFocusAndEnsureSelectedItem();
					// requested = false;
				}
			}

			private static readonly List<ICollapseHandler> handlers = new List<ICollapseHandler>();
			
			// using to keep track of which handler type was already active
			private static readonly List<Type> activeHandlers = new List<Type>();

			// TODO: make configure-able
			private static readonly HashSet<string> _itemsToCollapse = new HashSet<string>()
			{
				"UIRepaint", 
				"DrawChain", 
				"UIR.ImmediateRenderer", 
				"RenderChain.Draw", 
				"UIR.DrawChain",
				"UnityEngine.IMGUIModule.dll!UnityEngine::GUIUtility.ProcessEvent()",
				"UIElementsUtility.DoDispatch(Repaint Event)"
			};

			// ReSharper disable once UnusedMember.Local
			private static void Postfix(TreeView __instance, IList<TreeViewItem> newRows, HierarchyFrameDataView ___m_FrameDataView)
			{
				if (newRows == null) return;
				var settings = SelectiveProfilerSettings.instance;
				
				if(!settings.CollapseHierarchyNesting)
					CollapseRows.expanded.Clear();

				ProfilerHelper.profilerTreeView = __instance; 
				handlers.Clear();
				customRowsInfo.Clear();

				if (!settings.AllowCollapsing) return;
				
				var frame = ___m_FrameDataView;
				frameDataView = frame;
				
				var tree = __instance;
				if (frame == null || !frame.valid) return;

				for (var index = 0; index < newRows.Count; index++)
				{
					var row = newRows[index];
					var name = frame.GetItemName(row.id);
					row.displayName = name;
					var prevIndex = index;

					if (handlers.Count > 0)
					{
						// check if any of the added collapsed handlers are done and can be removed
						for (var i = handlers.Count - 1; i >= 0; i--)
						{
							var c = handlers[i];
							if (c.TryResolve(tree, row, newRows, ref index)) 
								handlers.RemoveAt(i);
						}
					}
					
					if (index != prevIndex)
					{
						row = newRows[index];
						name = frame.GetItemName(row.id);
						row.displayName = name;
					}
					
					activeHandlers.Clear();

					// true if any handler of that type did already run
					bool DidCollapseWithType(Type t)
					{
						return activeHandlers.Any(t.IsAssignableFrom);
					}
					
					void HandleCollapsing(ICollapseHandler handler)
					{
						row.displayName = name;
						if (!handler.ShouldCollapse(tree, row, name, newRows, ref index)) return;
						newRows.RemoveAt(index);
						index -= 1;
						if (row.hasChildren)
						{
							foreach (var ch in row.children)
							{
								if (ch == null) continue;
								ch.parent = row.parent;
								row.parent.AddChild(ch);
							}
						}
						row.parent.children.Remove(row);
						activeHandlers.Add(handler.GetType());
					}

					for (var i = handlers.Count - 1; i >= 0; i--)
					{
						var c = handlers[i];
						if (DidCollapseWithType(c.GetType())) continue;
						HandleCollapsing(c);
					}
					
					// check if we can/should add new handlers
					if (name.Contains("set ") || name.Contains("get "))
					{
						if (settings.CollapseProperties && !DidCollapseWithType(typeof(CollapseProperties)))
						{
							var collapse = new CollapseProperties(newRows.Count, row.parent);
							handlers.Add(collapse);
							// Debug.Log(row.parent.displayName + " @ " + index + ", " + row.parent.depth);
							HandleCollapsing(collapse);
						}
					}
						
					if (_itemsToCollapse.Contains(name) && !DidCollapseWithType(typeof(CollapseRows)))
					{
						if (settings.CollapseHierarchyNesting)
						{
							var handler = new CollapseRows(_itemsToCollapse);
							handlers.Add(handler);
							HandleCollapsing(handler);
						}
					}
				}
			}
		}


		/// <summary>
		/// render additional row information
		/// </summary>
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

			// ReSharper disable once UnusedMember.Local
			private static bool Prefix(TreeView __instance,
				ref Rect cellRect,
				TreeViewItem item,
				int column,
				HierarchyFrameDataView ___m_FrameDataView,
				out State __state)
			{
				__state = null;
				if (column != 0) return true;
				var tree = __instance;
				var frame = ___m_FrameDataView;
				if (frame == null || !frame.valid) return true;

				if (style == null)
				{
					style = new GUIStyle(EditorStyles.label);
					style.alignment = TextAnchor.MiddleRight;
					style.normal.textColor = Color.white;
					style.padding = new RectOffset(2, 0, 0, 2);
				}


				// if custom row has additional info e.g. because items were removed
				if (customRowsInfo.TryGetValue(item.id, out var info))
				{
					// was row collapsed completely?
					var isHint = info.StartsWith(k_AllItemsAreCollapsedHint);
					if (isHint)
						info = info.Substring(k_AllItemsAreCollapsedHint.Length);
					
					var col = GUI.color;
					var prev = style.alignment;
					// style.alignment = TextAnchor.MiddleLeft;
					GUI.color = tree.IsSelected(item.id) ? Color.white : Color.gray;
					var rect = cellRect;
					rect.width -= 20;

					if (isHint)
					{
						style.alignment = TextAnchor.MiddleLeft;
						// TODO: remove reflection here
						var indent = (float) tree.GetType().GetMethod("GetContentIndent", BindingFlags.NonPublic | BindingFlags.Instance)
							?.Invoke(tree, new object[] {item});
						rect.x = indent;
					}

					GUI.Label(rect, info, style);
					GUI.color = col;
					style.alignment = prev;
					
					// if this row should just display some hint
					if (isHint)
						return false;
				}


				if (item.id > parentIdOffset) return true;
				var itemName = frame.GetItemName(item.id);
				var separatorIndex = itemName.IndexOf(ProfilerSamplePatch.TypeSampleNameSeparator);
				if (separatorIndex < 0) return true;

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
				__state = new State() {Content = content};

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

			// ReSharper disable once UnusedMember.Local
			private static void Postfix(TreeView __instance, Rect cellRect, TreeViewItem item, State __state)
			{
				if (__state == null) return;

				var content = __state.Content;
				if (content != null)
				{
					var col = GUI.color;
					GUI.color = __instance.IsSelected(item.id) ? Color.white : Color.gray;
					var padding = item.hasChildren ? 17 : 3;
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


			// ReSharper disable once UnusedMember.Local
			private static bool Prefix(out string __result, HierarchyFrameDataView frameData, int itemId)
			{
				if (frameData == null || !frameData.valid)
				{
					__result = null;
					return true;
				}

				var separatorStr = ProfilerSamplePatch.TypeSampleNameSeparator;

				if (itemId > collapsedRowIdOffset)
				{
					if (customRowsInfo.TryGetValue(itemId, out var str))
					{
						__result = str;
						return false;
					}
				}
				// injected custom row
				else if (itemId > parentIdOffset)
				{
					var name = frameData.GetItemName(itemId - parentIdOffset);
					var separator = name.LastIndexOf(separatorStr);
					if (separator > 0 && separator < name.Length)
					{
						__result = name.Substring(0, separator);
						if (SelectiveProfiler.DevelopmentMode) __result += " [CustomRow]";
						return false;
					}
				}
				// fix normal
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


		// private class FrameDataTreeViewItem_FillColumnsData : EditorPatch
		// {
		// 	protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
		// 	{
		// 		var asm = typeof(FrameDataView).Assembly;
		// 		var t = asm.GetTypes().FirstOrDefault(_t => _t.Name == "FrameDataTreeViewItem");
		// 		var m = t.GetMethod("Init", BindingFlags.Public | BindingFlags.Instance);
		// 		targetMethods.Add(m);
		// 		return Task.CompletedTask;
		// 	}
		//
		// 	private static void Postfix(TreeViewItem __instance, HierarchyFrameDataView ___m_FrameDataView, string[] ___m_StringProperties)
		// 	{
		// 		var frame = ___m_FrameDataView;
		//
		// 		if (frame != null && frame.valid && __instance.id > parentIdOffset)
		// 		{
		// 			var id = __instance.id - parentIdOffset;
		// 			var name = frame.GetItemName(id);
		// 			var self = __instance;
		// 			var children = self.children;
		// 			var props = ___m_StringProperties;
		// 			if (children == null) return;
		// 			for (int i = 1; i < props.Length; i++)
		// 			{
		// 				var sum = 0f;
		// 				switch (i)
		// 				{
		// 					case 1: // Total
		// 					case 2: // Self
		// 						foreach (var ch in children)
		// 							sum += frame.GetItemColumnDataAsFloat(ch.id, i);
		// 						props[i] = sum.ToString("0.0") + "%";
		// 						break;
		// 					case 3: // Calls
		// 						foreach (var ch in children)
		// 							sum += (int) frame.GetItemColumnDataAsSingle(ch.id, i);
		// 						props[i] = sum.ToString("0");
		// 						break;
		// 					case 4: // GC alloc
		// 						foreach (var ch in children)
		// 							sum += frame.GetItemColumnDataAsFloat(ch.id, i);
		// 						if (sum > 1000)
		// 							props[i] = (sum / 1000).ToString("0.0") + " KB";
		// 						else props[i] = sum.ToString("0") + " B";
		// 						break;
		// 					case 5: // Time ms
		// 						foreach (var ch in children)
		// 							sum += frame.GetItemColumnDataAsFloat(ch.id, i);
		// 						props[i] = sum.ToString("0.00");
		// 						break;
		// 				}
		// 			}
		// 		}
		// 	}

		// }

		// 		private class Profiler_BuildRows : EditorPatch
// 		{
// 			protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
// 			{
// 				var t = typeof(UnityEditorInternal.ProfilerDriver).Assembly.GetType("UnityEditorInternal.ProfilerFrameDataTreeView");
// 				// var m = t.GetMethod("AddAllChildren", (BindingFlags) ~0);
// 				var m = t.GetMethod("AddAllChildren", (BindingFlags) ~0);
// 				targetMethods.Add(m);
// 				return Task.CompletedTask;
// 			}
//
// 			private class ItemData
// 			{
// 				public int OriginalDepth;
// 				public int Depth => Item?.depth ?? -1;
// 				public string Key;
//
// 				public TreeViewItem Item;
// 				// public List<TreeViewItem> Items = new List<TreeViewItem>(4);
// 			}
//
// 			private static readonly Stack<ItemData> stack = new Stack<ItemData>();
//
// 			private static void Postfix(object __instance, IList<TreeViewItem> newRows, List<int> newExpandedIds)
// 			{
// 				// if (!SelectiveProfilerSettings.instance.Enabled) return;
// 				// if (!SelectiveProfiler.AnyEnabled) return;
//
// 				var treeView = __instance as TreeView;
// 				if (treeView == null) return;
// 				var list = newRows;
//
// 				TreeViewItem CreateNewItem(TreeViewItem item, int newId, int depth)
// 				{
// 					var itemType = item.GetType();
// 					var typeName = itemType.FullName;
// 					var assemblyName = itemType.Assembly.FullName;
// 					if (string.IsNullOrEmpty(typeName)) return null;
// 					// creating FrameDataTreeViewItem
// 					// https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/ProfilerFrameDataTreeView.cs#L68
// 					var newItem = Activator.CreateInstance(AppDomain.CurrentDomain, assemblyName, typeName,
// 							true, AccessUtils.All,
// 							null, new object[] {ProfilerFrameDataView_Patch.GetFrameDataView(item), newId, depth, item.parent}, CultureInfo.CurrentCulture,
// 							null)
// 						.Unwrap() as TreeViewItem;
// 					return newItem;
// 				}
//
// 				void PopStack()
// 				{
// 					var e = stack.Pop();
// #if DEBUG_CUSTOMROWS
// 					Debug.Log("<b>POP</b> " + e.Key + ", " + e.Depth + "\n" + PrintStack());
// #endif
// 				}
//
// #if DEBUG_CUSTOMROWS
// 				string PrintStack() => "stack:\n" + string.Join("\n", stack.Select(s => s.Key + " - " + s.Depth)) + "\n\n\n";
// #endif
//
// 				HierarchyFrameDataView frame = null;
// 				if (list != null && list.Count > 0)
// 				{
// 					stack.Clear();
// 					for (var index = 0; index < list.Count; index++)
// 					{
// 						var item = list[index];
// 						if (frame == null) frame = ProfilerFrameDataView_Patch.GetFrameDataView(item);
// 						if (frame == null || !frame.valid) break;
// 						// continue;
//
// 						var name = frame.GetItemName(item.id);
//
// 						// if item is leaving stack
// 						var separatorIndex = name.LastIndexOf(ProfilerSamplePatch.TypeSampleNameSeparator);
//
// 						while (stack.Count > 0 && (item.depth < stack.Peek().OriginalDepth))
// 						{
// #if DEBUG_CUSTOMROWS
// 							Debug.Log(item.depth + " = " + (item.depth + stack.Count) + " < " + stack.Peek().OriginalDepth + ", " + name);
// #endif
// 							PopStack();
// 						}
//
// 						if (separatorIndex > 0 || stack.Count > 0)
// 						{
// 							// Debug.Log(item.depth + " - " + name);
//
// 							var entry = stack.Count > 0 ? stack.Peek() : null;
// 							if (separatorIndex > 0)
// 							{
// 								var key = name.Substring(0, separatorIndex);
// 								// if (name == "GameObjectTreeViewGUI/DoItemGUI(Rect, int, TreeViewItem, bool, bool, bool)") continue;
// 								// check an entry is already injected
// 								// or current top entry prefix is different
// 								if (entry == null || entry.Key != key)
// 								{
// 									var newId = item.id + parentIdOffset;
// 									var newItem = CreateNewItem(item, newId, item.depth + stack.Count);
//
// 									if (stack.All(e => treeView.IsExpanded(e.Item.id)))
// 									{
// 										list.Insert(index, newItem);
// 										index += 1;
// 									}
//
// 									if (item.parent.hasChildren)
// 										item.parent.children.Remove(item);
// 									item.parent.AddChild(newItem);
//
// 									entry = new ItemData();
// 									entry.Key = key;
// 									entry.OriginalDepth = item.depth;
// 									entry.Item = newItem;
// 									stack.Push(entry);
// #if DEBUG_CUSTOMROWS
// 									Debug.Log("PUSH " + key + ", " + item.depth + " -> " + newItem.depth + ", prev: " + name + ", " + item.depth + "\n" +
// 									          PrintStack());
// #endif
// 									// treeView.SetExpanded(newItem.id, true);
// 								}
// 							}
//
// 							entry = stack.Peek();
// 							item.depth += stack.Count;
// 							var parent = entry.Item;
//
// 							// check that item should still be a child of the injected parent
// 							if (item.depth <= parent.depth)
// 							{
// 								item.depth -= stack.Count;
// 								PopStack();
// 								continue;
// 							}
//
// 							// only add direct children (not children of children)
// 							if (item.depth - parent.depth <= 1)
// 							{
// 								parent.AddChild(item);
// 							}
//
// 							// remove from list if any item in the current inject parent stack is not expanded
// 							if (!stack.All(e => treeView.IsExpanded(e.Item.id)))
// 							{
// 								list.RemoveAt(index);
// 								index -= 1;
// 							}
// 						}
// 					}
// 				}
// 			}
		// }
	}
}