#define DEBUG_CUSTOMROWS
#undef DEBUG_CUSTOMROWS

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;
using JetBrains.Annotations;
using needle.EditorPatching;
using Needle.SelectiveProfiling.CodeWrapper;
using Needle.SelectiveProfiling.Utils;
using UnityEditor;
using UnityEditor.Graphs;
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

		// internal static List<int> RequestSelectedIds = new List<int>();

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
				var m = t.GetMethod("BuildRows", (BindingFlags) ~0);
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

			private static TreeViewItem CreateAndInsertNewItem(TreeView tree,
				IList<TreeViewItem> list,
				int insertAt,
				ref int index,
				int id,
				int depth,
				TreeViewItem parent,
				string text,
				Func<bool> insert = null)
			{
				id += collapsedRowIdOffset;
				var item = CreateNewItem(parent, id, depth);

				// add always an item to avoid having empty lists
				// which results in ArgumentException when expanding empty items (info is still stored in profiler state)
				if (tree.IsExpanded(parent.id) && (insert == null || insert.Invoke()))
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
					var id = parent.id; // ?? -1;
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
					if (item.depth == depth && TranspilerUtils.IsMarkedProperty(name))
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
				private readonly Func<TreeView, TreeViewItem, string, bool> shouldCollapse;
				internal static readonly HashSet<int> expanded = new HashSet<int>();
				private int collapsedCounter;

				private int firstIndex, firstDepth;

				public CollapseRows(Func<TreeView, TreeViewItem, string, bool> shouldCollapse)
				{
					this.shouldCollapse = shouldCollapse;
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
						var indexToInsert = firstIndex;
						CreateAndInsertNewItem(tree, list, indexToInsert, ref index, row.id, firstDepth, row.parent, $"Collapsed {collapsedCounter} rows");
					}
					else
					{
						var offset = collapsedDepth.Count;
						if (collapsedCounter > 0 && row.depth - firstDepth == 1)
							row.depth += 1;
						row.depth -= offset;
						// row.depth += 1;
					}

					return res;
				}

				public bool ShouldCollapse(TreeView tree, TreeViewItem item, string name, IList<TreeViewItem> list, ref int index)
				{
					var collapse = shouldCollapse.Invoke(tree, item, name);
					if (collapse)
					{
						collapsedCounter += 1;
						collapsedDepth.Push(item.depth + collapsedDepth.Count);
						currentDepthOffset += 1;

						if (firstIndex == 0)
						{
							firstIndex = index;
							firstDepth = item.depth;
						}

						// only expand on first discovery
						// that allows users to collapse hierarchies again
						// otherwise they would always be re-opened
						// var key = item.id + name.GetHashCode();
						var key = item.id;
						if (!expanded.Contains(key))
						{
							RequestReload(tree, item);
							expanded.Add(key);

							if (item.hasChildren)
							{
								foreach (var ch in item.children)
									if (ch != null)
										tree.SetExpanded(ch.id, true);
							}
						}
						
						tree.SetExpanded(item.id, true);
					}

					return collapse;
				}

				// need to request reload, otherwise expanded children would not be visible
				// they're only in the rows list if expanded
				private static int requestCounter;

				private static async void RequestReload(TreeView tree, TreeViewItem item)
				{
					requestCounter += 1;
					var req = requestCounter;
					// var selection = tree.GetSelection()?.FirstOrDefault() ?? -1;
					await Task.Delay(1);
					if (req != requestCounter) return;
					tree.SetExpanded(item.id, true);
					tree.Reload();
					tree.Repaint();
					// await Task.Delay(10);
					// tree.SetSelection(new List<int>(){selection});
					// tree.SetFocusAndEnsureSelectedItem();
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
				"UIEventRegistration.ProcessEvent()",
				"UIEventRegistration.ProcessEvent(int,IntPtr)", 
				"UIElementsUtility.DoDispatch(Repaint Event)"
			};

			private static bool ShouldCollapseRow(TreeView tree, TreeViewItem item, string name)
			{
				if (_itemsToCollapse.Contains(name))
					return true;

				// TODO: support for collapsing items that have no impact
				// if (frameDataView != null && frameDataView.valid)
				// {
				// 	if (item.id < parentIdOffset && !item.hasChildren)
				// 	{
				// 		var total = frameDataView.GetItemColumnDataAsFloat(item.id, 1);
				// 		var alloc = frameDataView.GetItemColumnDataAsFloat(item.id, 4);
				// 		if (total < 0.01f && alloc < 1)
				// 		{
				// 			Debug.Log(name + " - " + total + ", " + alloc + ", id=" + item.id);
				// 			return true;
				// 		}
				// 	}
				// }

				return false;
			}

			private static int previousFrameIndex;

			// ReSharper disable once UnusedMember.Local
			private static void Postfix(TreeView __instance, IList<TreeViewItem> __result, HierarchyFrameDataView ___m_FrameDataView)
			{ 
				var newRows = __result;
				if (newRows == null) return;
				var settings = SelectiveProfilerSettings.instance; 

				if (!settings.CollapseHierarchyNesting)
					CollapseRows.expanded.Clear();

				ProfilerHelper.profilerTreeView = __instance;
				handlers.Clear();
				customRowsInfo.Clear();

				if (!settings.AllowCollapsing) return;

				var frame = ___m_FrameDataView;
				frameDataView = frame;

				var tree = __instance;
				if (frame == null || !frame.valid) return;
				

				// if (RequestSelectedIds.Count > 0)
				// {
				// 	tree.SetExpanded(RequestSelectedIds);
				// 	tree.SetSelection(RequestSelectedIds);
				// 	tree.SetFocusAndEnsureSelectedItem();
				// 	RequestSelectedIds.Clear();
				// }
				
				// clear cached collapsed-expanded rows when frame changes
				if(frame.frameIndex != previousFrameIndex)
					CollapseRows.expanded.Clear();
				previousFrameIndex = frame.frameIndex;

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
							for (var i = row.children.Count - 1; i >= 0; i--)
							{
								var ch = row.children[i];
								if (ch == null) continue;
								ch.parent = row.parent;
								row.parent.AddChild(ch);
							}
						}

						// NOTE: when removing the row from parents children expanding does not work when frame changes and selected item is child of collapsed items 
						// row.parent.children.Remove(row);
						activeHandlers.Add(handler.GetType());
					}

					for (var i = handlers.Count - 1; i >= 0; i--)
					{
						var c = handlers[i];
						if (DidCollapseWithType(c.GetType())) continue;
						HandleCollapsing(c);
					}

					// check if we can/should add new handlers
					if (TranspilerUtils.IsMarkedProperty(name))
					{
						if (settings.CollapseProperties && !DidCollapseWithType(typeof(CollapseProperties)))
						{
							var collapse = new CollapseProperties(newRows.Count, row.parent);
							handlers.Add(collapse);
							// Debug.Log(row.parent.displayName + " @ " + index + ", " + row.parent.depth);
							HandleCollapsing(collapse);
						}
					}

					if (settings.CollapseHierarchyNesting && !DidCollapseWithType(typeof(CollapseRows)) && ShouldCollapseRow(tree, row, name))
					{
						var handler = new CollapseRows(ShouldCollapseRow);
						handlers.Add(handler);
						HandleCollapsing(handler);
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

			private static MethodInfo _indentCallback;
			
			// private static Func<TreeView, TreeViewItem, float> indentCallback;
			// private static T CompileReflection<T>([CanBeNull] Type instance, MethodInfo method, params Type[] parameter)
			// {
			// 	var p = parameter.Select(Expression.Parameter);
			// 	Debug.Log(p.Count());
			// 	var cnstructor = instance.GetConstructors().First();
			// 	var inst = Expression.New(cnstructor, cnstructor.GetParameters().Select(p => Expression.Parameter(p.ParameterType)));
			// 	var exp = Expression.Lambda<T>(
			// 		Expression.Call(inst, method, Expression.Parameter(typeof(TreeViewItem)))
			// 	);
			// 	return exp.Compile();
			// }

			private static float GetContentIdent(TreeView tree, TreeViewItem item)
			{
				if (_indentCallback == null)
				{
					_indentCallback = tree.GetType().GetMethod("GetContentIndent", BindingFlags.NonPublic | BindingFlags.Instance);
					if (_indentCallback == null) return 0;
					// TODO: replace with expression tree because this is called quite often
					// if (_indentCallback != null)
					// { 
					// 	indentCallback = CompileReflection<Func<TreeView, TreeViewItem, float>>(typeof(TreeView), _indentCallback, typeof(TreeViewItem)); 
					// 	// var param = Expression.Parameter(typeof(TreeViewItem));
					// 	// var lambdaExpression = Expression.Lambda<Func<TreeViewItem, float>>(Expression.Call(indentCallback, param)).Compile();
					// }
				}

				return (float) _indentCallback.Invoke(tree, new object[] {item});
			}

			private static GUIStyle labelStyle, possibleSlowBoxStyle;
			private static readonly Color possibleSlowMethodColor = new Color(.8f, .6f, 0);

			private static Texture2D _possibleSlowMethodTexture;
			private static Texture2D possibleSlowMethodTexture
			{
				get
				{
					if (_possibleSlowMethodTexture == null)
					{
						const int dashWidth = 5;
						const int height = 1;
						var ddw = dashWidth * 2;
						var tex = new Texture2D(ddw, height);
						for (var x = 0; x < tex.width; x++)
						{
							if(x % ddw < dashWidth)
								tex.SetPixel(x, 0, possibleSlowMethodColor);
							else tex.SetPixel(x, 0, new Color(0,0,0,0));
						}
						tex.Apply();
						_possibleSlowMethodTexture = tex;
					}
					return _possibleSlowMethodTexture;
				}
			}

			private static void EnsureStyles()
			{
				if (labelStyle == null)
				{
					labelStyle = new GUIStyle(EditorStyles.label);
					labelStyle.alignment = TextAnchor.MiddleRight;
					labelStyle.normal.textColor = Color.white;
					labelStyle.padding = new RectOffset(2, 0, 0, 2);
				}

				if (possibleSlowBoxStyle == null)
				{
					possibleSlowBoxStyle = new GUIStyle();
					possibleSlowBoxStyle.normal.textColor = Color.white;
					possibleSlowBoxStyle.stretchWidth = false;
					possibleSlowBoxStyle.normal.background = possibleSlowMethodTexture;
				}
			}

			private class State
			{
				public GUIContent Content;
			}

			private static bool ShouldShowFullScriptingName()
			{
				// 1 << 1 == ShowFullScriptingOptionsName
				// https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/ProfilerEditor/ProfilerWindow/ProfilerModules/CPUorGPUProfilerModule.cs#L50
				const int entry = (1 << 1);
				var val = SessionState.GetInt("Profiler.CPUProfilerModule.m_ProfilerViewFilteringOptions", 0);
				return (val & entry) != 0;
			}

			private static Gradient gradient;

			private static bool HandleItemsThatExceedThresholds(string name,
				Rect rect,
				TreeView tree,
				TreeViewItem item,
				HierarchyFrameDataView frame,
				[CanBeNull] State state)
			{
				if (!SelectiveProfilerSettings.instance.ColorPerformanceImpact)
					return true;

				if (frame == null || !frame.valid)
				{
					return true;
				}

				var alloc = frame.GetItemColumnDataAsFloat(item.id, 4);
				var ms = frame.GetItemColumnDataAsFloat(item.id, 5);

				var impact = ms / 16f;
				impact += alloc / 5000f;
				
				var possibleSlow = TranspilerUtils.IsMarkedPossibleSlow(name);
				// if (possibleSlow)
				// 	impact = Mathf.Max(.5f, impact);

				var col = GUIColors.GetColorOnGradient(GUIColors.NaiveCalculateGradientPosition(ms, alloc));

				if (!ShouldShowFullScriptingName())
				{
					const string fullScriptingNameSeparator = "::";
					var scriptingNameIndex = name.IndexOf(fullScriptingNameSeparator, StringComparison.InvariantCultureIgnoreCase);
					if (scriptingNameIndex > 0)
					{
						name = name.Substring(scriptingNameIndex + fullScriptingNameSeparator.Length);
					}
				}

				var str = TranspilerUtils.RemoveInternalMarkers(name);
				if (SelectiveProfiler.DrawItemDebugInformationInTreeView)
					str += ", " + item.id + ", " + frame.GetItemMarkerID(item.id);

				var content = new GUIContent(str);
				var indent = GetContentIdent(tree, item);
				rect.x += indent;
				rect.width -= indent;
				EnsureStyles();
				var prevAlignment = labelStyle.alignment;
				labelStyle.alignment = TextAnchor.MiddleLeft;
				var prevColor = GUI.color;
				GUI.color = tree.IsSelected(item.id) ? Color.white : col;
				GUI.Label(rect, content);
				var width = labelStyle.CalcSize(content).x;

				if (possibleSlow)
				{
					var underline = rect;
					underline.y = rect.y + rect.height;
					underline.height = 1;
					underline.width = width;
					GUI.color = Color.white;
					GUI.DrawTextureWithTexCoords (underline, possibleSlowMethodTexture, new Rect (0, 0, underline.width/ possibleSlowMethodTexture.width, underline.height), true);
					// GUI.Box(underline, string.Empty, possibleSlowBoxStyle);
				}
				
				labelStyle.alignment = prevAlignment;


				if (impact >= 0.999 && !item.hasChildren)
				{
					rect.x += width + 5;
					var padding = rect.height * .4f;
					rect.y += padding * .5f;
					rect.width = rect.height - padding;
					rect.height = rect.width;
					GUI.color *= .85f;
					GUI.DrawTexture(rect, Textures.HotPathIcon, ScaleMode.ScaleAndCrop, true);
				}
				
				GUI.color = prevColor;
				DrawAdditionalInfo(tree, item, rect, state);
				return false;
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
				EnsureStyles();

				// if custom row has additional info e.g. because items were removed
				if (customRowsInfo.TryGetValue(item.id, out var info))
				{
					// was row collapsed completely?
					var isHint = info.StartsWith(k_AllItemsAreCollapsedHint);
					if (isHint)
						info = info.Substring(k_AllItemsAreCollapsedHint.Length);

					var col = GUI.color;
					var prev = labelStyle.alignment;
					// style.alignment = TextAnchor.MiddleLeft;
					GUI.color = tree.IsSelected(item.id) ? Color.white : Color.gray;
					var rect = cellRect;
					rect.width -= 20;

					if (isHint)
					{
						labelStyle.alignment = TextAnchor.MiddleLeft;
						var indent = GetContentIdent(tree, item);
						rect.x = indent;
					}

					GUI.Label(rect, info, labelStyle);
					GUI.color = col;
					labelStyle.alignment = prev;

					// if this row should just display some special hint
					if (isHint) return false;
				}


				var itemName = frame.GetItemName(item.id);
				if (item.id > parentIdOffset) return HandleItemsThatExceedThresholds(itemName, cellRect, tree, item, frame, null);
				var separatorIndex = itemName.IndexOf(ProfilerSamplePatch.TypeSampleNameSeparator);
				if (separatorIndex < 0) return HandleItemsThatExceedThresholds(itemName, cellRect, tree, item, frame, null);

				// draw additional row info
				var name = itemName.Substring(0, separatorIndex);
				var methodName = itemName.Substring(separatorIndex + 1);
				// skip is parent contains declaring type name
				var parent = item.parent;
				if (parent != null)
				{
					var parentName = frame.GetItemName(parent.id);
					if (parentName.Contains(name))
						return HandleItemsThatExceedThresholds(methodName, cellRect, tree, item, frame, null);
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
					GUI.Label(cellRect, content, labelStyle);
					GUI.color = col;
					__state = null;
					return HandleItemsThatExceedThresholds(methodName, cellRect, tree, item, frame, __state);
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
				return HandleItemsThatExceedThresholds(methodName, cellRect, tree, item, frame, __state);
			}


			// ReSharper disable once UnusedMember.Local
			private static void Postfix(TreeView __instance, Rect cellRect, TreeViewItem item, State __state)
			{
				DrawAdditionalInfo(__instance, item, cellRect, __state);
			}

			/// <summary>
			/// drawing additional info
			/// </summary>
			private static void DrawAdditionalInfo(TreeView tree, TreeViewItem item, Rect cellRect, [CanBeNull] State state)
			{
				if (state == null) return;

				var content = state.Content;
				if (content != null)
				{
					var col = GUI.color;
					GUI.color = tree.IsSelected(item.id) ? Color.white : Color.gray;
					var padding = item.hasChildren ? 17 : 3;
					var rect = cellRect;
					var width = labelStyle.CalcSize(content).x;
					rect.x -= width + padding;
					rect.width = width;

					if (rect.x < 0)
					{
						// draw cut off
						var prevAlignment = labelStyle.alignment;
						labelStyle.alignment = TextAnchor.MiddleLeft;
						rect.x = 5;
						rect.width = cellRect.x - rect.x - padding;
						GUI.Label(rect, content, labelStyle);
						labelStyle.alignment = prevAlignment;

						if (rect.Contains(Event.current.mousePosition))
						{
							var tt = typeof(GUIStyle).GetMethod("SetMouseTooltip", BindingFlags.NonPublic | BindingFlags.Static);
							tt?.Invoke(null, new object[] {content.text, new Rect(Event.current.mousePosition, Vector2.zero)});
						}
					}
					else
					{
						// draw normally
						GUI.Label(rect, content, labelStyle);
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
#if UNITY_2021_1_OR_NEWER
				var t = asm.GetType("UnityEditorInternal.Profiling.CPUOrGPUProfilerModule");
#else
				var t = asm.GetType("UnityEditorInternal.Profiling.CPUorGPUProfilerModule");
#endif
				var m = t.GetMethod("GetItemName", AccessUtils.AllDeclared, null, CallingConventions.Any, 
					new[]{typeof(HierarchyFrameDataView), typeof(int)}, null); 
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
						var sub = name.Substring(separator + 1);
						__result = TranspilerUtils.RemoveInternalMarkers(sub);
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