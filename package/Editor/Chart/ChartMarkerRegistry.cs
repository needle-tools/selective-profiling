using System;
using System.Collections.Generic;
using System.Reflection;
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
		[NotNull] public string label;
		[NotNull] public MethodBase method;
	}
	
	public static class ChartMarkerRegistry
	{
		[InitializeOnLoadMethod]
		private static void Init()
		{
			Add(new ChartMarker(){label = "Click", method = AccessTools.Method(typeof(ExecuteEvents), "Execute", new[] {typeof(IPointerClickHandler), typeof(BaseEventData)})});
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
			if (string.IsNullOrEmpty(marker.label)) throw new Exception("Missing label");
			if (marker.method == null) throw new Exception("Missing methods");
			if (markers.ContainsKey(marker.key)) return;
			markers.Add(marker.key, marker);
			if(SelectiveProfilerSettings.instance.DebugLog)
				Debug.Log("Register " + marker.label + ", " + marker.method);
			var prov = new ChartMarkerInject_Patch(marker.label + "@" + marker.method.Name);
			var patch = new ChartMarkerInject_Patch.AddProfilerMarker(marker.label, marker.method);
			prov.Patches.Add(patch);
			PatchManager.RegisterPatch(prov);
			prov.EnablePatch();
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