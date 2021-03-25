using System.Collections.Generic;
using System.Reflection;
using Needle.SelectiveProfiling.Utils;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.Profiling;
using UnityEngine;

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
		public static bool TryGetMethodsInChildren(int id, HierarchyFrameDataView frameData, out List<MethodInfo> methods)
		{
			methods = null;
			InternalFindMethods(id, frameData, ref methods);
			return methods != null && methods.Count > 0;
		}

		private static void InternalFindMethods(int id, HierarchyFrameDataView frameData, ref List<MethodInfo> methods)
		{
			var name = frameData.GetItemName(id);

			if (AccessUtils.TryGetMethodFromName(name, out var methodInfo))
			{
				if (methods == null) methods = new List<MethodInfo>();
				methods.Add(methodInfo);
			}

			if (frameData.HasItemChildren(id))
			{
				var children = new List<int>();
				frameData.GetItemChildren(id, children);
				foreach (var child in children)
				{
					InternalFindMethods(child, frameData, ref methods);
				}
			}
		}

		internal static HierarchyItem IsProfiled(TreeViewItem item, HierarchyFrameDataView view)
		{
			return IsProfiled(item.id, view, 0);
		}

		private static HierarchyItem IsProfiled(int id, HierarchyFrameDataView view, int level)
		{
			if (view == null || !view.valid) return HierarchyItem.None;

			var markerId = view.GetItemMarkerID(id);

			if (level <= 0)
			{
				if(idToMethod.ContainsKey(markerId))
					idToMethod.Remove(markerId);
				if (profiledChildren.ContainsKey(markerId))
					profiledChildren.Remove(markerId);
			}

			if (!idToMethod.ContainsKey(markerId))
			{
				var name = view.GetItemName(id);
				if (AccessUtils.TryGetMethodFromName(name, out var methodInfo))
				{
					idToMethod.Add(markerId, methodInfo);
				}
				else if (!profiledChildren.ContainsKey(markerId))
				{
					var hasChildren = view.HasItemChildren(id);
					var children = hasChildren ? new List<int>() : null;
					if (hasChildren)
					{
						view.GetItemChildren(id, children);
						var lvl = ++level;
						for (var index = children.Count - 1; index >= 0; index--)
						{
							var child = children[index];
							if (IsProfiled(child, view, lvl) == HierarchyItem.None) children.RemoveAt(index);
						}
						if(!profiledChildren.ContainsKey(markerId))
							profiledChildren.Add(markerId, children);
						else
						{
							profiledChildren[markerId]?.AddRange(children);
						}
					}
				}
			}

			idToMethod.TryGetValue(markerId, out var method);

			if (IsEnabled(method)) return HierarchyItem.Self;
			if (HasProfiledChild(markerId, view, level)) return HierarchyItem.Child;
			return HierarchyItem.None;
		}

		private static readonly Dictionary<int, MethodInfo> idToMethod = new Dictionary<int, MethodInfo>();
		private static readonly Dictionary<int, List<int>> profiledChildren = new Dictionary<int, List<int>>();

		private static bool IsEnabled(MethodInfo info)
		{
			return info != null && SelectiveProfiler.IsProfiling(info);
		}

		private static bool HasProfiledChild(int markerId, HierarchyFrameDataView view, int level)
		{
			if (profiledChildren.ContainsKey(markerId))
			{
				var list = profiledChildren[markerId];
				++level;
				if (list != null)
				{
					foreach (var i in list)
					{
						if (IsProfiled(i, view, level) != HierarchyItem.None)
							return true;
					}
				}
			}

			return false;
		}
	}
}