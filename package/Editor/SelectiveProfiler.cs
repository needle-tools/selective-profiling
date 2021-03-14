using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using needle.EditorPatching;
using Needle.SelectiveProfiling.Utils;
using UnityEditor;
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

			// Debug.Log("Enable profiling for " + method);
			var patch = new ProfilerSamplePatch(method, null, " NEEDLE_SAMPLE");
			var info = new ProfilingInfo(patch, method);
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
			public string Identifier;
			public EditorPatchProvider Patch;
			public MethodInfo Method;
			public bool IsActive => PatchManager.IsActive(Patch.ID());

			public ProfilingInfo(EditorPatchProvider patch, MethodInfo info)
			{
				this.Patch = patch;
				this.Method = info;
				this.Identifier = info.GetMethodIdentifier();
			}

			public void ToggleActive()
			{
				if (IsActive) PatchManager.DisablePatch(Patch);
				else PatchManager.EnablePatch(Patch);
			}

			public void Enable() => Patch.Enable();
			public void Disable() => Patch.Disable();

			public override string ToString()
			{
				return Patch?.ID() + " - " + Identifier;
			}
		}

		private static readonly List<ProfilingInfo> patches = new List<ProfilingInfo>();

		private static void UpdateState(ProfilingInfo info)
		{
			// Debug.Log("Update state " + info);
		}

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
				foreach(var patch in patches)
					PatchManager.UnregisterAndDisablePatch(patch.Patch);
			}
		}
	}
}