using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using needle.EditorPatching;

namespace Needle.SelectiveProfiling
{
	public static class SelectiveProfiler
	{
		public static bool IsProfiling(MethodInfo method)
		{
			return patches.Any(e => e.IsActive && e.Method == method);
		}

		public static void Profile([NotNull] MethodInfo method)
		{
			if (method == null) throw new ArgumentNullException(nameof(method));

			var existing = patches.FirstOrDefault(e => e.Method == method);
			if (existing != null)
			{
				existing.ToggleActive();
				return;
			}

			var patch = new ProfilerSamplePatch(method, null, " INJECTED");
			patches.Add(new ProfilingInfo()
			{
				Patch = patch,
				Method = method,
			});
			PatchManager.RegisterPatch(patch);
			PatchManager.EnablePatch(patch);
		}

		[Serializable]
		private class ProfilingInfo
		{
			public EditorPatchProvider Patch;
			public MethodInfo Method;
			public bool IsActive => PatchManager.IsActive(Patch.ID());

			public void ToggleActive()
			{
				if (IsActive) PatchManager.DisablePatch(Patch);
				else PatchManager.EnablePatch(Patch);
			}
		}

		private static readonly List<ProfilingInfo> patches = new List<ProfilingInfo>();
	}
}