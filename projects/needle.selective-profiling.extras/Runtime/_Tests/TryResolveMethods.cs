#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using needle.EditorPatching;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace _Tests
{
	
	
	public class TryResolveMethods : MonoBehaviour
	{
		private void Start()
		{

			ProfilerDriver.NewProfilerFrameRecorded += OnNewFrame;
		}

		public void OnNewFrame(int connectionId, int frameIndex)
		{


			using (var frameData = ProfilerDriver.GetHierarchyFrameDataView(frameIndex, 0, HierarchyFrameDataView.ViewModes.Default, 0, false))
			{
				if (!frameData.valid) return;
				
				var root = frameData.GetRootItemID();
				var children = new List<int>();
				frameData.GetItemChildren(root, children);
				foreach (var c in children)
				{
					var itemName = frameData.GetItemName(c);
					if (itemName == "PlayerLoop")
					{
						var ch = new List<int>();
						frameData.GetItemChildren(c, ch);
						foreach (var l in ch)
						{
							Debug.Log(frameData.GetItemName(l) + " --> " + frameData.ResolveItemCallstack(l));
						}
					}
				}
				// for (var i = 0; i < frameData.sampleCount; i++)
				// {
				// 	var id = frameData.re
				// 	var cs = frameData.ResolveItemCallstack(i);
				// 	Debug.Log(cs);
				// }
			}
		}
	}
}

#endif