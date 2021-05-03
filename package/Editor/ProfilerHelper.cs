using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Needle.SelectiveProfiling.Utils;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.Profiling;
using UnityEditorInternal;

namespace Needle.SelectiveProfiling
{
	internal enum HierarchyItem
	{
		None = 0,
		Child = 1,
		Self = 2,
	}

	internal static class ProfilerHelper
	{
		internal static bool HasCallstackEnabled => ProfilerDriver.memoryRecordMode != ProfilerMemoryRecordMode.None;

		// private static EditorWindow profilerWindow;
		internal static TreeView profilerTreeView;
		internal static void RepaintProfiler()
		{
			profilerTreeView?.Reload();
			profilerTreeView?.SetFocusAndEnsureSelectedItem();

			// if (!profilerWindow)
			// {
			// 	var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
			// 	profilerWindow = windows.FirstOrDefault(w => w.titleContent.text == "Profiler");
			// }
			//
			// if(profilerWindow)
			// 	profilerWindow.Repaint();
		}
		
		private static int frames = 0;
		[InitializeOnLoadMethod]
		private static void Update()
		{
			EditorApplication.update += () =>
			{
				++frames;
				// if (frames % 20 != 0) return;
				// idToMethod.Clear();
				// profiledChildren.Clear();
			};
		}

		internal static HierarchyItem IsProfiled(TreeViewItem item, HierarchyFrameDataView view)
		{
			return IsProfiled(item.id, view, 0);
		}

		private static int GetKey(int itemId, HierarchyFrameDataView view)
		{
			return view.GetItemMarkerID(itemId);
		}

		// private static readonly List<ulong> callstackBuffer = new List<ulong>();
		// private static readonly List<MethodInfo> methodsListBuffer;
		
		private static HierarchyItem IsProfiled(int itemId, HierarchyFrameDataView view, int level)
		{
			if (view == null || !view.valid) return HierarchyItem.None;
			if (!SelectiveProfiler.AnyEnabled) return HierarchyItem.None;

			void UpdateOrSetMarker(int _key, MethodInfo info)
			{
				if (!idToMethod.ContainsKey(_key))
					idToMethod.Add(_key, info);
				else idToMethod[_key] = info;
			}

			var name = view.GetItemName(itemId);
			var key = GetKey(itemId, view);

			if (level == 0 && (itemId + frames) % 30 == 0)
			{
				if(idToMethod.ContainsKey(key))
					idToMethod.Remove(key);
				if (profiledChildren.ContainsKey(key))
					profiledChildren.Remove(key);
			}

			if (!idToMethod.ContainsKey(key))
			{
				var found = false;

				if (AccessUtils.TryGetMethodFromName(name, out var methodInfo, false))
				{
					var enabled = methodInfo.FirstOrDefault(IsEnabled);
					if (enabled != null)
					{
						found = true;
						UpdateOrSetMarker(key, enabled);
					}
				}

				// if (!found)
				// {
				// 	if (HasCallstackEnabled)
				// 	{
				// 		callstackBuffer.Clear();
				// 		view.GetItemCallstack(itemId, callstackBuffer);
				// 		var list = methodsListBuffer;
				// 		for (var i = 0; i < callstackBuffer.Count; i++)
				// 		{
				// 			if (found) break;
				// 			var addr = callstackBuffer[i];
				// 			var profilerMethodInfo = view.ResolveMethodInfo(addr);
				// 			var methodName = profilerMethodInfo.methodName;
				// 			if (methodName == null) continue;
				// 			// var log = methodName.Contains("IMGUIContainer");
				// 			// if(log)
				// 			// 	Debug.Log(methodName);
				// 			list?.Clear();
				// 			if (AccessUtils.TryGetMethodFromFullyQualifiedName(methodName, ref list))
				// 			{
				// 				// Debug.Assert(list.Count > 0);
				// 				foreach (var e in list)
				// 				{
				// 					if (IsEnabled(e))
				// 					{
				// 						Debug.Log(i + "/" + callstackBuffer.Count + " -> " + e + " - " + methodName);
				// 						UpdateOrSetMarker(key, e);
				// 						found = true;
				// 					}
				// 				}
				// 			}
				// 		}
				// 		methodsListBuffer?.Clear();
				// 		callstackBuffer.Clear();
				// 	}
				// }

				if (!found && !idToMethod.ContainsKey(itemId)) idToMethod.Add(itemId, null);

				// if (found) Debug.Log("Found in level " + level + ": " + idToMethod[itemId]);

				if (!found && level < 3)
				{
					// void CollectChildrenIds(int currentId)
					// {
					// 	var hasChildren = view.HasItemChildren(currentId);
					// 	List<int> children;
					// 	if (hasChildren)
					// 	{
					// 		if (profiledChildren.ContainsKey(key))
					// 		{
					// 			children = profiledChildren[key];
					// 			children.Clear();
					// 		}
					//
					// 		children = new List<int>();
					// 	}
					// 	else children = null;
					// 	if (!hasChildren) return;
					// 	// TODO: figure out why GetChildren crashes the editor in edit mode. it crashes when having some stacks showing profiled methods unfolded and then enabling/disabling profiler or changing frames
					// 	// view.GetItemChildren(currentId, children);
					// 	// var childrenLevel = level + 1;
					// 	// for (var index = children.Count - 1; index >= 0; index--)
					// 	// {
					// 	// 	var ch = children[index];
					// 	// 	if (IsProfiled(ch, view, childrenLevel) == HierarchyItem.None)
					// 	// 	{
					// 	// 		children.RemoveAt(index);
					// 	// 	}
					// 	// }
					//
					// 	if (!profiledChildren.ContainsKey(key))
					// 		profiledChildren.Add(key, children);
					// 	else profiledChildren[key].AddRange(children);
					// }

					// CollectChildrenIds(itemId);
				}
			}

			idToMethod.TryGetValue(key, out var method);

			if (IsEnabled(method))
			{
				return HierarchyItem.Self;
			}

			if (HasProfiledChild(key, view, level)) return HierarchyItem.Child;
			return HierarchyItem.None;
		}

		private static readonly Dictionary<int, MethodInfo> idToMethod = new Dictionary<int, MethodInfo>();
		private static readonly Dictionary<int, List<int>> profiledChildren = new Dictionary<int, List<int>>();

		private static bool IsEnabled(MethodInfo info)
		{
			return info != null && SelectiveProfiler.IsProfiling(info);
		}

		private static bool HasProfiledChild(int key, HierarchyFrameDataView view, int level)
		{
			if (profiledChildren.ContainsKey(key))
			{
				var list = profiledChildren[key];
				if (list == null) return false;
				level += 1;
				for (var index = list.Count - 1; index >= 0; index--)
				{
					var childId = list[index];
					var childKey = GetKey(childId, view);
					// var childMarkerId = view.GetItemMarkerID(id);
					// if (childMarkerId == markerId) continue;
					if (idToMethod.ContainsKey(childKey))
					{
						var method = idToMethod[childKey];
						if (IsEnabled(method))
						{
							return true;
						}
					}

					if (IsProfiled(childId, view, level) != HierarchyItem.None)
					{
						return true;
					}
				}
			}

			return false;
		}
	}
}