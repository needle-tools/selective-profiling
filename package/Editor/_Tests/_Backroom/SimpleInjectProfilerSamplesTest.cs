// using System;
// using needle.EditorPatching;
// using UnityEngine;
// using Object = UnityEngine.Object;
//
// namespace DefaultNamespace
// {
// 	public class SimpleInjectProfilerSamplesTest : MonoBehaviour
// 	{
// 		private InjectProfilerSamplesPatch p = new InjectProfilerSamplesPatch();
//
// 		private void OnEnable()
// 		{
// 			PatchManager.EnablePatch(p);
// 		}
//
// 		private void OnDisable()
// 		{
// 			PatchManager.DisablePatch(p);
// 		}
// 	}
// }