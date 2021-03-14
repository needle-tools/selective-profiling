using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using JetBrains.Annotations;
using needle.EditorPatching;
using UnityEditor;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	public static class SelectiveProfiler
	{
		public const string SamplePostfix = "NEEDLE_SAMPLE";

		public static bool IsProfiling(MethodInfo method)
		{
			return entries.Any(e => e.IsActive && e.Method == method);
		}

		public static async void EnableProfiling([NotNull] MethodInfo method)
		{
			await EnableProfilingAsync(method);
		}

		public static async Task EnableProfilingAsync([NotNull] MethodInfo method)
		{
			if (method == null) throw new ArgumentNullException(nameof(method));

			if (!method.HasMethodBody())
			{
				Debug.LogWarning("Method has no body: " + method);
				return;
			}

			var existing = entries.FirstOrDefault(e => e.Method == method);
			if (existing != null)
			{
				if (!existing.IsActive)
				{
					await existing.Enable();
				}

				return;
			}

			var i = new ProfiledMethod(method);
			Debug.Log(i);
			if(i.TryResolveMethod(out var res))
			{
				Debug.Log("resolved " + res);
			}

			var patch = new ProfilerSamplePatch(method, null, " " + SamplePostfix);
			var info = new ProfilingInfo(patch, method);
			entries.Add(info);
			PatchManager.RegisterPatch(patch);
			await PatchManager.EnablePatch(patch);
			HandleNestedCalls();
		}

		public static void DisableProfiling(MethodInfo method)
		{
			if (method == null) throw new ArgumentNullException(nameof(method));
			var existing = entries.FirstOrDefault(e => e.Method == method);
			if (existing == null) return;
			existing.Disable();
		}

		internal static IReadOnlyList<ProfilingInfo> Patches => entries;
		private static readonly List<ProfilingInfo> entries = new List<ProfilingInfo>();

		[InitializeOnLoadMethod, RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void Init()
		{
			EditorApplication.playModeStateChanged -= OnPlayModeChanged;
			EditorApplication.playModeStateChanged += OnPlayModeChanged;
		}
		
		private static void OnPlayModeChanged(PlayModeStateChange obj)
		{
			if (obj == PlayModeStateChange.ExitingPlayMode)
			{
				foreach (var patch in entries)
					PatchManager.UnregisterAndDisablePatch(patch.Patch);
			}
		}

		private static void Save()
		{
			SelectiveProfilerSettings.instance.Save();
		}
		
		private static readonly bool deepProfiling = SelectiveProfilerSettings.instance.DeepProfiling;
		private static readonly HashSet<MethodInfo> callsFound = new HashSet<MethodInfo>();

		internal static void RegisterInternalCalledMethod(MethodInfo method)
		{
			if (!deepProfiling) return;
			if (method == null) return;
			if (!method.HasMethodBody())
			{
				Debug.Log("Skip called method. No body: " + method);
				return;
			}
			if (callsFound.Contains(method)) return;
			// Debug.Log("FOUND " + method);
			callsFound.Add(method);
		}

		private static async void HandleNestedCalls()
		{
			if (!deepProfiling) return;
			if (callsFound.Count <= 0) return;
			// Debug.Log("Apply deep profiling: " + callsFound.Count);
			var local = callsFound.ToArray();
			foreach (var method in local)
			{
				// Debug.Log(method);
				await EnableProfilingAsync(method);
			}
		}
	}
}