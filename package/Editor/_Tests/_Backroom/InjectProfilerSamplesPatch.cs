// using System.Collections.Generic;
// using System.Reflection;
// using System.Threading.Tasks;
// using needle.EditorPatching;
// using UnityEngine;
// using UnityEngine.Profiling;
//
// namespace DefaultNamespace
// {
// 	public class InjectProfilerSamplesPatch : EditorPatchProvider
// 	{
// 		public override string DisplayName { get; }
// 		public override string Description { get; }
// 		
// 		protected override void OnGetPatches(List<EditorPatch> patches)
// 		{
// 			patches.Add(new SamplerPatch());
// 		}
// 		
// 		public class SamplerPatch : EditorPatch
// 		{
// 			protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
// 			{
// 				var t = typeof(MySlowScript);
// 				var m = t.GetMethod(nameof(MySlowScript.Update));
// 				targetMethods.Add(m);
// 				return Task.CompletedTask;
// 			}
//
// 			private static void Prefix(object __instance)
// 			{
// 				Profiler.BeginSample("INJECTED SAMPLE", __instance as Object);
// 			}
//
// 			private static void Postfix()
// 			{
// 				Profiler.EndSample();
// 			}
// 		}
// 	}
// }