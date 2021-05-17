using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using HarmonyLib;
using JetBrains.Annotations;
using needle.EditorPatching;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Needle.SelectiveProfiling
{
	public struct ChartMarker
	{
		public string key => label;
		public string label;
		public MethodBase method;
	}
	
	public static class ChartMarkerRegistry
	{
		[InitializeOnLoadMethod]
		private static void Init()
		{
			Add(new ChartMarker(){label = "Click", method = AccessTools.Method(typeof(ExecuteEvents), "Execute", new[] {typeof(IPointerClickHandler), typeof(BaseEventData)})});
			Add("Enable Profiling Async", AccessTools.Method(typeof(SelectiveProfiler), "InternalEnableProfilingAsync"));
			Add("Apply Patch", AccessTools.Method(typeof(PatchBase), nameof(PatchBase.Apply)));
			Add("Run Task", AccessTools.Method(typeof(Task), nameof(Task.Run), new[]{typeof(Action)}));
			Add("Harmony Patch", AccessTools.Method(typeof(PatchProcessor), nameof(PatchProcessor.Patch)));
			Add("Profiler New Frame", AccessTools.Method(typeof(ProfilerMarkerStore), "OnNewFrame"));

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

		private static readonly Dictionary<string, ChartMarker> markers = new Dictionary<string, ChartMarker>();

		public static void Add(string label, MethodBase method)
		{
			if (markers.ContainsKey(label)) return;
			Add(new ChartMarker()
			{
				label = label,
				method = method
			});
		}

		public static void Add(ChartMarker marker)
		{
			if (string.IsNullOrEmpty(marker.label))
			{
				Debug.LogError("Missing label\n" + marker.method);
				return;
			}
			if (marker.method == null)
			{
				Debug.LogError("Missing method for " + marker.label);
				return;
			}
			if (markers.ContainsKey(marker.key)) return;
			markers.Add(marker.key, marker);
			if(SelectiveProfilerSettings.instance.DebugLog)
				Debug.Log("Register " + marker.label + ", " + marker.method);
			// var prov = new ChartMarkerInject_Patch(marker.label + "@" + marker.method.Name);
			var patch = new ChartMarkerInject_Patch.AddProfilerMarker(marker.label, marker.method);
			Patcher.Apply(patch);
			// prov.Patches.Add(patch);
			// PatchManager.RegisterPatch(prov);
			// prov.EnablePatch();
		}

		public static void Remove(string key)
		{
			if (markers.ContainsKey(key))
			{
				markers.Remove(key);
			}
		}
	}
}