// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Reflection;
// using System.Text.RegularExpressions;
// using needle.EditorPatching;
// using UnityEditor.Compilation;
// using UnityEngine;
//
// namespace DefaultNamespace
// {
// 	public class SampleEverything : EditorPatchProvider
// 	{
// 		public override string DisplayName { get; }
// 		public override string Description { get; }
//
// 		protected override void OnGetPatches(List<EditorPatch> patches)
// 		{
// 			var unityAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
// 			// foreach(var u in unityAssemblies) Debug.Log(u.name);
// 			
// 			var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a =>
// 			{
// 				return unityAssemblies.Any(u => a.FullName.Contains(u.name));
// 				// var fullname = a.FullName;
// 				// var include = true;
// 				// include &= !Regex.Match(fullname, "System|UnityEngine.Profiler", RegexOptions.Compiled).Success;
// 				// return include;
// 			});
// 			// foreach (var a in assemblies) Debug.Log(a);
// 			
// 			// Debug.Log("do nothing for now");
//
// 			// var types = assemblies.SelectMany(a => a.GetTypes());
// 			// foreach (var t in types)
// 			// {
// 			// 	patches.Add(new SamplerPatch(t, t.ToString()));
// 			// }
// 		}
// 	}
// }