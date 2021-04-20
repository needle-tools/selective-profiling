using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using HarmonyLib;
using needle.EditorPatching;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace DefaultNamespace
{
	public class SomeClass
	{
		[InitializeOnLoadMethod]
		private static void Init()
		{
			try
			{
				try
				{
					Profiler.BeginSample("TEST");
					Debug.Log("log something that is sampled");
					ThrowException();
				}
				finally
				{
					Profiler.EndSample();
				}
			}
			catch (Exception e)
			{
				Debug.Log("catched " + e);
			}
		}

		private static void ThrowException()
		{
			throw new Exception();
		}
	}

	public class SomeOtherClass
	{
		
	}


	public class PatchProv : EditorPatchProvider
	{
		protected override void OnGetPatches(List<EditorPatch> patches)
		{
			patches.Add(new Patch());
		}

		private class Patch : EditorPatch
		{
			protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
			{
				targetMethods.Add(typeof(SomeClass).GetMethod("Init", BindingFlags.Static | BindingFlags.NonPublic));
				return Task.CompletedTask;
			}

			private static void Transpiler(IEnumerable<CodeInstruction> code)
			{
				Debug.Log(string.Join("\n", code));
			}
		}
	}
}