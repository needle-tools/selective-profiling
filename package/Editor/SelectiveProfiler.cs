using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using needle.EditorPatching;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	public static class SelectiveProfiler
	{
		public static bool IsProfiling(MethodInfo method)
		{
			return patches.Any(e => e.IsActive && e.Method == method);
		}

		public static void EnableProfiling([NotNull] MethodInfo method)
		{
			if (method == null) throw new ArgumentNullException(nameof(method));

			var existing = patches.FirstOrDefault(e => e.Method == method);
			if (existing != null)
			{
				if (!existing.IsActive)
				{
					existing.Enable();
					UpdateState(existing);
				}
				return;
			}

			Debug.Log("Enable profiling for " + method);
			var patch = new ProfilerSamplePatch(method, null, " NEEDLE_SAMPLE");
			var info = new ProfilingInfo()
			{
				Patch = patch,
				Method = method,
			};
			patches.Add(info);
			PatchManager.RegisterPatch(patch);
			PatchManager.EnablePatch(patch);
			UpdateState(info);
		}

		public static void DisableProfiling(MethodInfo method)
		{
			if (method == null) throw new ArgumentNullException(nameof(method));
			var existing = patches.FirstOrDefault(e => e.Method == method);
			if (existing == null) return;
			existing.Disable();
			UpdateState(existing);
		}

		internal static IReadOnlyList<ProfilingInfo> Patches => patches;

		[Serializable]
		internal class ProfilingInfo
		{
			public EditorPatchProvider Patch;
			public MethodInfo Method;
			public bool IsActive => PatchManager.IsActive(Patch.ID());

			public void ToggleActive()
			{
				if (IsActive) PatchManager.DisablePatch(Patch);
				else PatchManager.EnablePatch(Patch);
			}

			public void Enable() => Patch.Enable();
			public void Disable() => Patch.Disable();

			public override string ToString()
			{
				return Patch?.ID() + " - " + Method?.FullDescription();
			}
		}

		private static readonly List<ProfilingInfo> patches = new List<ProfilingInfo>();

		private static void UpdateState(ProfilingInfo info)
		{
			Debug.Log("Persistent update state " + info);
		}
	}
}