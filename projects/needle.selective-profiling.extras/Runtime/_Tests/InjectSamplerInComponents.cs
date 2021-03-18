#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using needle.EditorPatching;
using Unity.Profiling;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Needle.SelectiveProfiling._Tests
{
	public class InjectSamplerInComponents : MonoBehaviour
	{
		private ComponentsSamplePatchProvider prov;


		private void OnEnable()
		{
			var components = GetComponentsInChildren<Component>().Where(c => c != this).ToArray();
			Debug.Log("Found " + components.Length);
			prov = new ComponentsSamplePatchProvider(components);
			PatchManager.RegisterPatch(prov, true);
			prov.EnablePatch();
		}

		private void OnDisable()
		{
			PatchManager.DisablePatch(prov);
		}

		[NoAutoDiscover]
		private class ComponentsSamplePatchProvider : EditorPatchProvider
		{
			private readonly IEnumerable<Object> targetObjects;

			public List<EditorPatch> AdditionalPatches = new List<EditorPatch>();

			public ComponentsSamplePatchProvider(IEnumerable<Object> targetObjects)
			{
				this.targetObjects = targetObjects;
			}

			public override string DisplayName { get; }
			public override string Description { get; }
			
			protected override void OnGetPatches(List<EditorPatch> patches)
			{
				if (targetObjects == null) return;
				Debug.Log("Get patches " + targetObjects.Count());
				foreach (var to in targetObjects)
				{
					Debug.Log("Add patch " + to);
					patches.Add(new SamplerPatch(to));
				}
				patches.Add(new AnotherPatch());
			}
		}
	}

	public class AnotherPatch : EditorPatch
	{
		protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
		{
			targetMethods.Add(typeof(StackTraceUtility).GetMethod("ExtractStackTrace"));
			return Task.CompletedTask;
		}
	}


	public class SamplerPatch : EditorPatch
	{
		private readonly Type Target;
		private readonly string ObjectInfo;

		public const string MarkerName = "INJECTED SAMPLE";

		public SamplerPatch(Object obj) : this(obj.GetType(), obj.ToString())
		{
		}

		public SamplerPatch(Type obj, string info)
		{
			this.Target = obj;
			this.ObjectInfo = info;
		}

		protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
		{
			var methods = Target.GetMethods((BindingFlags) ~0)
				.Where(m => m.HasMethodBody())
				.ToArray();
			Debug.Log(ObjectInfo + " patched " + methods.Length + " methods\n" + string.Join("\n", methods.Select(m => m.ToString())));
			targetMethods.AddRange(methods);
			return Task.CompletedTask;
		}

		private static readonly ProfilerMarker s_PreparePerfMarker = new ProfilerMarker(MarkerName);


		private static void Prefix(object __instance)
		{
			var obj = __instance as Object;
			// Profiler.BeginSample(MarkerName, obj);
			s_PreparePerfMarker.Begin(obj);
		}

		private static void Postfix()
		{
			// Profiler.EndSample();
			s_PreparePerfMarker.End();
		}
	}
}

#endif