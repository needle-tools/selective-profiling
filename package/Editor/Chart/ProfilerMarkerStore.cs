using System.Collections.Generic;
using System.Security.Policy;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;

namespace Needle.SelectiveProfiling
{
	internal struct ChartMarkerData
	{
		public int chartMarkerId;
		public int frame;
		public string label;
		public string tooltip;
		public float millis;
		public int markerId;
		public int count;

		public ChartMarkerData(int frame, int markerId, string label, string tooltip, int chartMarkerId, float millis)
		{
			this.frame = frame;
			this.label = label;
			this.tooltip = tooltip;
			this.chartMarkerId = chartMarkerId;
			this.millis = millis;
			this.markerId = markerId;
			this.count = 1;
		}
	}


	/// <summary>
	/// collects profiler samples for registered markers
	/// </summary>
	internal static class ProfilerMarkerStore
	{
		[InitializeOnLoadMethod]
		private static void Init()
		{
			ProfilerDriver.NewProfilerFrameRecorded -= OnNewFrame;
			ProfilerDriver.NewProfilerFrameRecorded += OnNewFrame;
		}

		private static readonly HashSet<string> expectedMarkers = new HashSet<string>();
		private static readonly HashSet<int> expectedMarkerIds = new HashSet<int>();

		internal static void AddExpectedMarker(string name)
		{
			if (expectedMarkers.Contains(name)) return;
			expectedMarkers.Add(name);
			expectedMarkerIds.Clear();
		}


		internal static void RemoveAt(int index)
		{
			if (index < 0 || index >= captures.Count) return;
			var ex = captures[index];
			captures.RemoveAt(index);

			for (var i = lanes.Count - 1; i >= 0; i--)
			{
				var lane = lanes[i];
				if (lane.label == ex.label)
				{
					lane.count -= 1;
					lanes[i] = lane;
					if (lane.count <= 0)
					{
						lanes.RemoveAt(i);
						for (; i < lanes.Count; i++)
						{
							var other = lanes[i];
							other.num -= 1;
							lanes[i] = other;
						}
					}

					break;
				}
			}
		}

		internal static int LaneCount => lanes.Count;

		internal static int GetLane(string label)
		{
			foreach (var lane in lanes)
			{
				if (lane.label == label)
					return lane.num;
			}
			return 0;
		}

		private static void AddToLane(string label)
		{
			for (var index = 0; index < lanes.Count; index++)
			{
				var lane = lanes[index];
				if (lane.label == label)
				{
					lane.count += 1;
					lanes[index] = lane;
					return;
				}
			}

			lanes.Add((label, 1, lanes.Count));
		}

		internal static IReadOnlyList<ChartMarkerData> Markers => captures;
		
		private static readonly List<ChartMarkerData> captures = new List<ChartMarkerData>();
		private static int capturesCounter = 0;
		private static readonly List<(string label, int count, int num)> lanes = new List<(string label, int count, int num)>();

		// private static readonly Dictionary<int, int> tempMarkerIdToHierarchyItemId = new Dictionary<int, int>();
		
		private static readonly List<(int markerId, int captureIndex)> capturedThisFrame = new List<(int, int)>();

		private static void OnNewFrame(int thread, int frame)
		{
			if (ProfilerHelper.IsDeepProfiling) return;
			
			using (var frameData = ProfilerDriver.GetRawFrameDataView(frame, 0))
			{
				if (!frameData.valid)
					return;

				if (expectedMarkers.Count <= 0) return;
				
				expectedMarkerIds.Clear();
				foreach (var marker in expectedMarkers)
				{
					var id = frameData.GetMarkerId(marker);
					if (FrameDataView.invalidMarkerId == id) continue;
					expectedMarkerIds.Add(id);
				}
				if (expectedMarkerIds.Count <= 0) return;

				// var names = new string[ProfilerDriver.GetUISystemEventMarkersCount(frame, 1)];
				// var eventMarker = new EventMarker[ProfilerDriver.GetUISystemEventMarkersCount(frame, 1)];
				// ProfilerDriver.GetUISystemEventMarkersBatch(frame, 1, eventMarker, names);
				// Debug.Log(names.Length);

				capturedThisFrame.Clear();
				var samples = frameData.sampleCount;
				for (var i = 0; i < samples; i++)
				{
					var markerId = frameData.GetSampleMarkerId(i);
					if (expectedMarkerIds.Contains(markerId))
					{
						var alreadyFound = false;
						for (var k = 0; k < capturedThisFrame.Count; k++)
						{
							var (markerId1, captureIndex) = capturedThisFrame[k];
							if (markerId1 == markerId)
							{
								alreadyFound = true;
								var prev = captures[captureIndex];
								prev.count += 1;
								captures[captureIndex] = prev;
							}
						}
						if (alreadyFound) continue;
						var name = frameData.GetSampleName(i);
						var tooltip = name + ": " + GetAdditionalMarkerInfo(frameData, i, out var ms);
						var chartMarker = new ChartMarkerData(frame, markerId, name, tooltip, capturesCounter, ms);
						capturesCounter += 1;
						capturedThisFrame.Add((markerId, captures.Count));
						captures.Add(chartMarker);
						AddToLane(name);
					}
				}
			}
			
		}


		private static readonly List<ulong> callstackBuffer = new List<ulong>();

		private static string GetAdditionalMarkerInfo(RawFrameDataView frame, int sampleIndex, out float millies)
		{
			var builder = new StringBuilder();
			millies = frame.GetSampleTimeMs(sampleIndex);
			builder.AppendLine(millies + "ms");

			callstackBuffer.Clear();
			frame.GetSampleCallstack(sampleIndex, callstackBuffer);
			if (callstackBuffer.Count > 0)
			{
				builder.AppendLine("Callstack:");
				foreach (var addr in callstackBuffer)
				{
					var mi = frame.ResolveMethodInfo(addr);
					builder.AppendLine(mi.methodName + " at " + mi.sourceFileLine);
				}
			}

			// remove last line break
			if (builder.Length > 2) builder.Remove(builder.Length - 2, 2);
			return builder.ToString();
		}
	}
}