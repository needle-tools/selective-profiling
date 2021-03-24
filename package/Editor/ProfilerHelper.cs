using System.Collections.Generic;
using System.Reflection;
using Needle.SelectiveProfiling.Utils;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.Profiling;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
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
		
		internal static bool IsProfiled(TreeViewItem item, HierarchyFrameDataView view)
		{
			return IsProfiled(item.id, view);
		}

		internal static bool IsProfiled(int id, HierarchyFrameDataView view)
		{
			if (view == null || !view.valid) return false;
			
			// TODO: should this id be the marker id
			var markerId = view.GetItemMarkerID(id);

			if (!idToMethod.ContainsKey(markerId))
			{
				var name = view.GetItemName(id);
				if (AccessUtils.TryGetMethodFromName(name, out var methodInfo))
				{
					idToMethod.Add(markerId, methodInfo);
				}
				// else if (!profiledChildren.ContainsKey(id))
				// {
				// 	var hasChildren = view.HasItemChildren(id);
				// 	var children = hasChildren ? new List<int>() : null;
				// 	if (hasChildren)
				// 	{
				// 		view.GetItemChildren(id, children);
				// 		for (var index = children.Count - 1; index >= 0; index--)
				// 		{
				// 			var i = children[index];
				// 			if (!IsProfiled(i, view)) children.RemoveAt(index);
				// 		}
				// 		profiledChildren.Add(id, children);
				// 	}
				// }
			}

			idToMethod.TryGetValue(markerId, out var method);

			if (method == null && (!Application.isPlaying || (markerId + editorUpdateCounter) % 30 == 0))
			{
				idToMethod.Remove(markerId);
				// if(profiledChildren.ContainsKey(id))
				// 	profiledChildren.Remove(id);
			}
			
			return IsEnabled(method) || HasProfiledChild(markerId, view);
		}

		private static readonly Dictionary<int, MethodInfo> idToMethod = new Dictionary<int, MethodInfo>();
		private static readonly Dictionary<int, List<int>> profiledChildren = new Dictionary<int, List<int>>();

		private static bool IsEnabled(MethodInfo info)
		{
			return info != null && SelectiveProfiler.IsProfiling(info);
		}

		private static bool HasProfiledChild(int markerId, HierarchyFrameDataView view)
		{
			if (profiledChildren.ContainsKey(markerId))
			{
				var list = profiledChildren[markerId];
				if (list != null)
				{
					foreach (var i in list)
					{
						if (IsProfiled(i, view))
							return true;
					}
				}
			}

			return false;
		}

		[InitializeOnLoadMethod]
		private static void Init()
		{
			EditorApplication.update += OnEditorUpdate;
		}

		private static int editorUpdateCounter;
		private static void OnEditorUpdate()
		{
			editorUpdateCounter++;
		}
	}
}