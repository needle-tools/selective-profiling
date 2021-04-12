using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	public static class PerformanceVisualizer
	{
		[InitializeOnLoadMethod]
		private static void Init()
		{
			ProfilerDriver.NewProfilerFrameRecorded += OnNewFrame;
			EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
		}

		private static void OnNewFrame(int thread, int frameIndex)
		{
			invalidate?.Invoke();
			
			var frame = ProfilerDriver.GetHierarchyFrameDataView(frameIndex, 0, HierarchyFrameDataView.ViewModes.Default, 1, false);
			if (!frame.valid) return;

			var root = frame.GetRootItemID();
			void Handle(int id, int depth)
			{
				// var name = frame.GetItemName(id);
				var instanceId = frame.GetItemInstanceID(id);
				if (instanceId > 0)
				{
					// Debug.Log(depth + " - " + name + ", " + instanceId, instance);
					if (!dataCache.ContainsKey(instanceId))
					{
						var entry = new PerformanceData();
						invalidate += () => entry.isValid = false;
						dataCache.Add(instanceId, entry);
					}
					var data = dataCache[instanceId];

					data.isValid = true;
					data.TotalMs = frame.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnTotalTime);
					
					var name = frame.GetItemName(id);
					var instance = EditorUtility.InstanceIDToObject(instanceId);
					// Debug.Log(name + ": " + data.TotalMs + "ms", instance);
				}
				else if (frame.HasItemChildren(id))
				{
					var childrenBuffer = new List<int>();
					frame.GetItemChildren(id, childrenBuffer);
					var newDepth = depth + 1;
					for (var index = childrenBuffer.Count - 1; index >= 0; index--)
					{
						var ch = childrenBuffer[index];
						Handle(ch, newDepth);
					}
				}
			}

			Handle(root, 0);
		}

		private static event Action invalidate;

		private class PerformanceData
		{
			public int InstanceId;
			public float TotalMs;
			internal bool isValid;

			internal void Clear()
			{
				TotalMs = 0;
			}

			internal void Add(PerformanceData other)
			{
				if (!other.isValid) return;
				TotalMs += other.TotalMs;
			}
		}

		private static readonly Dictionary<int, PerformanceData> dataCache = new Dictionary<int, PerformanceData>();
		private static readonly Dictionary<int, int[]> hierarchyChildrenCache = new Dictionary<int, int[]>();
		private static readonly PerformanceData tempHierarchySummedData = new PerformanceData();

		private static void OnHierarchyGUI(int instanceId, Rect rect)
		{
			if (!hierarchyChildrenCache.ContainsKey(instanceId))
			{
				var obj = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
				if (obj)
				{
					var children = obj.GetComponentsInChildren<Component>(obj);
					// Debug.Log(instanceId + ": " + children.Length);
					hierarchyChildrenCache.Add(instanceId, children.Select(e => e.GetInstanceID()).ToArray());
				}
			}
			
			if (dataCache.ContainsKey(instanceId))
			{
				var data = dataCache[instanceId];
				DrawData(data, rect);
			}
			else if (hierarchyChildrenCache.ContainsKey(instanceId))
			{
				tempHierarchySummedData.Clear();
				tempHierarchySummedData.isValid = true;
				foreach (var ch in hierarchyChildrenCache[instanceId])
				{
					if (dataCache.ContainsKey(ch))
					{
						tempHierarchySummedData.Add(dataCache[ch]);
					}
				}
				DrawData(tempHierarchySummedData, rect);
			}
		}

		private static GUIStyle rightAlignedStyle;

		private static void EnsureStyles()
		{
			if (rightAlignedStyle == null)
			{
				rightAlignedStyle = EditorStyles.label;
				rightAlignedStyle.alignment = TextAnchor.MiddleRight;
			}
		}
		
		private static void DrawData(PerformanceData data, Rect rect)
		{
			if (data == null || !data.isValid) return;
			EnsureStyles();
			GUI.Label(rect, data.TotalMs.ToString("0.00") + " ms", rightAlignedStyle);
				
		}
	}
}