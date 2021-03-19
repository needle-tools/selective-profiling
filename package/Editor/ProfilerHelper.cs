using System.Collections.Generic;
using System.Reflection;
using Needle.SelectiveProfiling.Utils;
using UnityEditor.Profiling;

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
				foreach (var ch in children)
				{
					InternalFindMethods(ch, frameData, ref methods);
				}
			}
		}
	}
}