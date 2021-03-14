#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;

namespace Needle.SelectiveProfiling._Tests
{
	public class MyProfilerThing : MonoBehaviour
	{
		private void Start()
		{
			Profiler.enabled = true;

			ProfilerDriver.NewProfilerFrameRecorded += OnNewFrame;
		}

		private readonly List<string> names = new List<string>();
		private readonly List<(Sampler sampler, Recorder rec)> recorders = new List<(Sampler, Recorder)>();
		public bool run = false;
		public bool everyFrame;


		// private void Update()
		// {
		//     if (!run) return;
		//     run = false;
		//     ListSamples();
		// }

		public void OnNewFrame(int connectionId, int frameIndex)
		{
			if (!everyFrame)
			{
				if (!run) return;
			}

			run = false;

			using (var frameData = ProfilerDriver.GetHierarchyFrameDataView(frameIndex, 0, HierarchyFrameDataView.ViewModes.Default, 0, false))
			{
				if (!frameData.valid) return;
				var root = frameData.GetRootItemID();
				List<int> children = new List<int>();
				frameData.GetItemChildren(root, children);
				var callstack = new List<ulong>();

				void Traverse(int id)
				{
					callstack.Clear();
					frameData.GetItemCallstack(id, callstack);
					if (frameData.HasItemChildren(id))
					{
						var list = new List<int>();
						list.Clear();
						frameData.GetItemChildren(id, list);
						for (var index = 0; index < list.Count; index++)
						{
							var c = list[index];
							var mi = frameData.GetItemMarkerID(c);
							var s = frameData.GetItemName(c);
							s += " - " + frameData.GetItemName(mi);
							Debug.Log(s);

							// var callstack = new List<ulong>();
							// int sampleCount = frameData.sampleCount;
							// for (int i = 0; i < sampleCount; ++i)
							// {
							// 	frameData.GetItemMergedSampleCallstack(c, i, callstack);
							// 	if (callstack.Count > 0)
							// 	{
							// 		Debug.Log(id + " - " + callstack.Count + " - " + frameData.HasItemChildren(id));
							// 		Debug.Log(callstack.Count);
							// 	}
							// }
							
							Traverse(c);
						}
					}
				}

				foreach (var child in children)
				{
					Traverse(child);
				}

			}

			// using (var frameData = ProfilerDriver.GetRawFrameDataView(frameIndex, 0))
			// {
			// 	if (!frameData.valid) return;
			// 	// for(int i = 0; i < 100; i++)
			// 	Debug.Log("total: " + frameData.frameTimeMs + "ms");
			// 	var markerId = frameData.GetMarkerId(SamplerPatch.MarkerName);
			// 	if (markerId == -1) return;
			// 	var info = frameData.GetMarkerMetadataInfo(markerId);
			//
			// 	int sampleCount = frameData.sampleCount;
			// 	for (int i = 0; i < sampleCount; ++i)
			// 	{
			// 		if (markerId != frameData.GetSampleMarkerId(i))
			// 			continue;
			// 	
			// 		var meta = frameData.GetSampleTimeMs(i);
			// 	
			// 		Debug.Log(meta + " / total: " + frameData.frameTimeMs + "ms");
			// 	}
			// 	// var iter = new ProfilerFrameDataIterator();
			// 	// var a = new NativeProfilerTimeline_GetEntryTimingInfoArgs();
			// 	// NativeProfilerTimeline.GetEntryTimingInfo(ref a);
			// 	// var p = new NativeProfilerTimeline_GetEntryAtPositionArgs();
			// 	// p.
			// 	// NativeProfilerTimeline.GetEntryAtPosition(ref p);
			// 	// p.
			// 	// ProfilerDriver.GetFormattedCounterValue(arg2, ProfilerArea.CPU, )
			// }
		}

		[ContextMenu(nameof(ListSamples))]
		private void ListSamples()
		{
			var count = Sampler.GetNames(names);
			recorders.Clear();
			for (int i = 0; i < count; i++)
			{
				var name = names[i];
				var sampler = Sampler.Get(name);
				var rec = sampler.GetRecorder();
				if (!rec.enabled || !rec.isValid) continue;
				recorders.Add(((sampler, rec)));
			}

			// var ordered = recorders.OrderByDescending(r => r.rec.elapsedNanoseconds);
			// Debug.Log("-----------");
			// foreach (var (sampler, recorder) in ordered)
			// {
			//     Debug.Log(sampler.name + ": " + recorder.elapsedNanoseconds);
			// }

			var s = Recorder.Get(SamplerPatch.MarkerName);
			Debug.Log(s.elapsedNanoseconds + " - " + s.isValid);

			var info = new List<FrameDataView.MarkerInfo>();
			using (var frameData = ProfilerDriver.GetRawFrameDataView(Time.frameCount, 0))
			{
				Debug.Log(frameData.valid);
				// frameData.ResolveMethodInfo()

				frameData.GetMarkers(info);
				// var m = frameData.GetMarkerId(SamplerPatch.MarkerName);
				// Debug.Log("MARKER " + m);
				foreach (var i in info)
				{
					Debug.Log(i.name);
				}
			}
		}

		// Update is called once per frame
		void LateUpdate()
		{
			// using (var frameData =
			//     ProfilerDriver.GetHierarchyFrameDataView(Time.frameCount, 0, HierarchyFrameDataView.ViewModes.Default, HierarchyFrameDataView.columnDontSort, false))
			// {
			//     var hierarchy = frameData.GetCategoryInfo(0);
			// }
		}
	}
}

#endif