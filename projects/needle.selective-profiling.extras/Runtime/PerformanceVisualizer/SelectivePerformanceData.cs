using System;
using System.Collections.Generic;
using needle.EditorPatching;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	public static class SelectivePerformanceData
	{
		public static bool TryGetPerformanceData(int instanceId, out PerformanceData data)
		{
			return InternalTryGetPerformanceData(instanceId, out data);
		}

		[InitializeOnLoadMethod]
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void Init()
		{
			dataCache.Clear();
			ProfilerDriver.NewProfilerFrameRecorded -= OnNewFrame;
			ProfilerDriver.NewProfilerFrameRecorded += OnNewFrame;
			EditorApplication.update -= EditorUpdate;
			EditorApplication.update += EditorUpdate;
			EnablePatches();
		}

		private static void EnablePatches()
		{
			foreach (var exp in ExpectedPatches())
			{
				if (!PatchManager.IsPersistentDisabled(exp))
					PatchManager.EnablePatch(exp);
			}
		}

		private static IEnumerable<string> ExpectedPatches()
		{
			yield return typeof(DrawPerformanceInInspectorHeader).FullName;
		}

		private static int _updateCounter;

		private static void EditorUpdate()
		{
			_updateCounter += 1;
			if (_updateCounter % 60 == 0)
			{
				hierarchyChildrenCache.Clear();
			}
		}

		private static void OnNewFrame(int thread, int frameIndex)
		{
			if (frameIndex % 30 != 0) return;

			invalidate?.Invoke();

			var frame = ProfilerDriver.GetHierarchyFrameDataView(frameIndex, 0, HierarchyFrameDataView.ViewModes.Default, 1, false);
			if (!frame.valid) return;

			var root = frame.GetRootItemID();

			void Handle(int id, int depth)
			{
				// var name = frame.GetItemName(id);
				var instanceId = frame.GetItemInstanceID(id);
				if (instanceId != 0)
				{
					// Debug.Log(depth + " - " + name + ", " + instanceId, instance);
					if (!dataCache.ContainsKey(instanceId))
					{
						var entry = new PerformanceData();
						invalidate += () => { entry.isValid = false; };
						dataCache.Add(instanceId, entry);
					}

					var data = dataCache[instanceId];
					data.InstanceId = instanceId;
					data.isValid = true;
					data.TotalMs = frame.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnTotalTime);
					data.Alloc = frame.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnGcMemory);
					// dataCache[instanceId] = data;
					// var name = frame.GetItemName(id);
					// var instance = EditorUtility.InstanceIDToObject(instanceId);
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

		/// <summary>
		/// called when new profiler data comes in to invalidate entries that might not be contained in new data
		/// </summary>
		private static event Action invalidate;

		/// <summary>
		/// contains collected data per instance id
		/// </summary>
		private static readonly Dictionary<int, PerformanceData> dataCache = new Dictionary<int, PerformanceData>();

		/// <summary>
		/// gameObject instance id -> children (components) instance ids
		/// </summary>
		private static readonly Dictionary<int, List<int>> hierarchyChildrenCache = new Dictionary<int, List<int>>();

		/// <summary>
		/// contains accumulated profiler data
		/// </summary>
		private static readonly PerformanceData dataNonAlloc = new PerformanceData();

		private static readonly List<Component> componentNonAllocCache = new List<Component>();

		private static bool InternalTryGetPerformanceData(int instanceId, out PerformanceData data)
		{
			if (!hierarchyChildrenCache.ContainsKey(instanceId))
			{
				var obj = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
				if (obj)
				{
					componentNonAllocCache.Clear();
					obj.GetComponentsInChildren<Component>(obj, componentNonAllocCache);
					if (componentNonAllocCache.Count > 0)
					{
						if (!hierarchyChildrenCache.ContainsKey(instanceId))
							hierarchyChildrenCache.Add(instanceId, new List<int>());
						var childrenIds = hierarchyChildrenCache[instanceId];
						childrenIds.Clear();
						for (var index = 0; index < componentNonAllocCache.Count; index++)
						{
							var comp = componentNonAllocCache[index];
							childrenIds.Add(comp.GetInstanceID());
						}
					}
				}
			}

			if (dataCache.ContainsKey(instanceId))
			{
				var entry = dataCache[instanceId];
				dataNonAlloc.isValid = true;
				dataNonAlloc.InstanceId = instanceId;
				dataNonAlloc.Clear();
				dataNonAlloc.Add(entry);
				data = dataNonAlloc;
				return true;
			}

			if (hierarchyChildrenCache.ContainsKey(instanceId))
			{
				dataNonAlloc.Clear();
				dataNonAlloc.isValid = true;
				dataNonAlloc.InstanceId = instanceId;
				for (var index = hierarchyChildrenCache[instanceId].Count - 1; index >= 0; index--)
				{
					var ch = hierarchyChildrenCache[instanceId][index];
					if (dataCache.ContainsKey(ch))
					{
						dataNonAlloc.Add(dataCache[ch]);
					}
				}

				data = dataNonAlloc;
				return true;
			}

			data = null;
			return false;
		}
	}
}