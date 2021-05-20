using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Needle.SelectiveProfiling
{
	public static class ChartMarkerRegistry
	{
		[InitializeOnLoadMethod]
		private static void Init()
		{
			Add("Click", AccessTools.Method(typeof(ExecuteEvents), "Execute", new[] {typeof(IPointerClickHandler), typeof(BaseEventData)}));
			Add("Enable Profiling Async", AccessTools.Method(typeof(SelectiveProfiler), "InternalEnableProfilingAsync"));
			Add("Apply Patch", AccessTools.Method(typeof(PatchBase), nameof(PatchBase.Apply)));
			Add("Run Task", AccessTools.Method(typeof(Task), nameof(Task.Run), new[]{typeof(Action)}));
			Add("Harmony Patch", AccessTools.Method(typeof(PatchProcessor), nameof(PatchProcessor.Patch)));
			// Add("Profiler New Frame", AccessTools.Method(typeof(ProfilerMarkerStore), "OnNewFrame"));

			// await Task.Delay(1000);
			// for (var index = 0; index < SelectiveProfilerSettings.instance.Methods.Count; index++)
			// {
			// 	var m = SelectiveProfilerSettings.instance.Methods[index];
			// 	if (m.Assembly.Contains("Selective")) continue;
			// 	if (m.TryResolveMethod(out var method))
			// 		Add(m.ClassWithMethod(), method);
			// 	if (index > 100) break;
			// }
		}

		private static readonly Dictionary<string, MethodBase> markers = new Dictionary<string, MethodBase>();

		public static void Add(string label, MethodBase method)
		{
			if (markers.ContainsKey(label)) return;
			
			if (string.IsNullOrEmpty(label))
			{
				Debug.LogError("Missing label\n" + method);
				return;
			}
			if (method == null)
			{
				Debug.LogError("Missing method for " + label);
				return;
			}
			var key = label;
			if (markers.ContainsKey(key)) return;
			markers.Add(key, method);
			if(SelectiveProfilerSettings.instance.DebugLog)
				Debug.Log("Register " + label + ", " + method);
			var patch = new ChartMarkerInject_Patch.AddProfilerMarker(label, method);
			Patcher.Apply(patch);
			Add(key);
		}

		public static void Remove(string key)
		{
			if (markers.ContainsKey(key))
			{
				markers.Remove(key);
			}
		}
		
		
		/// <summary>
		/// use this with the Profiler.Sample name
		/// </summary>
		public static void Add(string sampleName)
		{
			if (ProfilerMarkerStore.expectedMarkers.Contains(sampleName)) return;
			ProfilerMarkerStore.expectedMarkers.Add(sampleName);
			ProfilerMarkerStore.expectedMarkerIds.Clear();
		}
	}
}