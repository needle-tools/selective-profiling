using System;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using needle.EditorPatching;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Needle.SelectiveProfiling
{
	public struct SelectiveMarker
	{
		[NotNull] public string label;
		[NotNull] public MethodBase method;
	}
	
	public static class ChartMarker
	{
		[InitializeOnLoadMethod]
		private static void Init()
		{
			Add(new SelectiveMarker(){label = "Click", method = AccessTools.Method(typeof(ExecuteEvents), "Execute", new[] {typeof(IPointerClickHandler), typeof(BaseEventData)})});
		}

		public static void Add(string label, MethodBase method)
		{
			Add(new SelectiveMarker()
			{
				label = label,
				method = method
			});
		}

		public static void Add(SelectiveMarker marker)
		{
			if (string.IsNullOrEmpty(marker.label)) throw new Exception("Missing label");
			if (marker.method == null) throw new Exception("Missing methods");
			Debug.Log("Register " + marker.label + ", " + marker.method);
			var prov = new CharMarkerInject_Patch(marker.label + "@" + marker.method.Name);
			var patch = new CharMarkerInject_Patch.AddProfilerMarker(marker.label, marker.method);
			prov.Patches.Add(patch);
			PatchManager.RegisterPatch(prov);
			prov.EnablePatch();
		}
	}
}