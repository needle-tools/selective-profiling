using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEditor.Profiling;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	public interface IPerformanceData
	{
		[CanBeNull] List<int> Ids { get; }
		int InstanceId { get; }
		float TotalMs { get; }
		float Alloc { get; }
		[CanBeNull] string Name { get; }
	}
	
	public class PerformanceData : IPerformanceData
	{
		public const int StartMarker = 0;
		public List<int> Ids { get; private set; }
		public int InstanceId { get; internal set; }
		public float TotalMs { get; private set; }
		public float Alloc { get; private set; }
		public string Name { get; private set; }

		internal bool isValid;

		internal void Clear()
		{
			TotalMs = 0;
			Alloc = 0;
			Ids?.Clear();
		}

		internal void Add(PerformanceData other)
		{
			if (!other.isValid) return;
			TotalMs += other.TotalMs;
			Alloc += other.Alloc;
			
			if (other.Ids != null)
			{
				if (Ids == null) Ids = new List<int>();
				Ids.Add(StartMarker);
				Ids.AddRange(other.Ids);
			}
		}

		private static readonly List<int> itemMarkerIdPathCache = new List<int>();
		internal void Add(HierarchyFrameDataView view, int itemId)
		{
			isValid = true;
			TotalMs += view.GetItemColumnDataAsFloat(itemId, HierarchyFrameDataView.columnTotalTime);
			Alloc += view.GetItemColumnDataAsFloat(itemId, HierarchyFrameDataView.columnGcMemory);

			if (Ids == null) Ids = new List<int>();
			itemMarkerIdPathCache.Clear();
			view.GetItemMarkerIDPath(itemId, itemMarkerIdPathCache);
			foreach (var path in itemMarkerIdPathCache)
				Ids.Add(path);

			Name = view.GetItemName(itemId);
		}
	}
}