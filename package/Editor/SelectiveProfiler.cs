﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using JetBrains.Annotations;
using needle.EditorPatching;
using Needle.SelectiveProfiling.Utils;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using Object = System.Object;

namespace Needle.SelectiveProfiling
{
	public static class SelectiveProfiler
	{
		public const string SamplePostfix = "[needle]";

		internal static void AddToAutoProfiling(MethodInfo method)
		{
			if (method == null) return;
			if (!Application.isPlaying) return;
			if (!SelectiveProfilerSettings.instance.ImmediateMode) return;
#pragma warning disable 4014
			InternalEnableProfilingAsync(method, false, true, true, method);
#pragma warning restore 4014
		}

		public static bool IsProfiling(MethodInfo method)
		{
			return Patches.Any(e => e.IsActive && e.Method == method);
		}

		public static async void EnableProfiling([NotNull] MethodInfo method, bool save = true, bool enablePatch = true, bool enableIfMuted = false)
		{
			await EnableProfilingAsync(method, save, enablePatch, enableIfMuted);
		}

		public static Task EnableProfilingAsync([NotNull] MethodInfo method, bool save = true, bool enablePatch = true, bool enableIfMuted = false)
		{
			return InternalEnableProfilingAsync(method, save, enablePatch, enableIfMuted);
		}

		private static async Task InternalEnableProfilingAsync(MethodInfo method,
			bool save = true,
			bool enablePatch = true,
			bool enableIfMuted = false,
			MethodInfo source = null,
			int depth = 0
		)
		{
			var settings = SelectiveProfilerSettings.instance;
			if (!settings.Enabled) return;

			if (method == null) throw new ArgumentNullException(nameof(method));

			if (!method.HasMethodBody())
			{
				if (settings.DebugLog)
					Debug.LogWarning("Method has no body: " + method);
				return;
			}

			var isDeep = source != null && method != source;
			if (AccessUtils.AllowPatching(method, isDeep, settings.DebugLog) == false)
			{
				return;
			}

			void HandleCallstackRegistration(ProfilingInfo current)
			{
				if (source == null) return;
				var sourcePatch = patches.FirstOrDefault(p => p.Value.Method == source).Value;
				if (sourcePatch != null) 
				{
					current.AddCaller(sourcePatch);
				}
			}

			void HandleDeepProfiling()
			{
				var nextLevel = ++depth;
				if (nextLevel < settings.MaxDepth)
				{
					HandleNestedCalls(method, nextLevel);
				}
			}

			var mi = new MethodInformation(method);
			settings.Get(ref mi);
			if (enableIfMuted) mi.Enabled = true;
			
			if (patches.TryGetValue(mi, out var existingProfilingInfo))
			{
				HandleCallstackRegistration(existingProfilingInfo);
				
				if (!existingProfilingInfo.IsActive)
				{
					await existingProfilingInfo.Enable();
					HandleDeepProfiling();
				}

				return;
			}

			if (save)
			{
				settings.Add(mi);
				settings.Save();
			}


			var patch = new ProfilerSamplePatch(method, null, " " + SamplePostfix);
			var info = new ProfilingInfo(patch, method, mi);
			patches.Add(mi, info);
			HandleCallstackRegistration(info);
			PatchManager.RegisterPatch(patch);

			if (enablePatch)
			{
				var muted = !mi.Enabled;
				if (enableIfMuted && muted) settings.SetMuted(mi, false);
				if (!muted)
				{
					await info.Enable();
					HandleDeepProfiling();
				}
			}
		}

		public static void DisableProfiling(MethodInfo method)
		{
			if (method == null) throw new ArgumentNullException(nameof(method));
			var existing = patches.Values.FirstOrDefault(e => e.Method == method);
			existing?.Disable();
		}

		internal static bool DebugLog => SelectiveProfilerSettings.instance.DebugLog;

		internal static bool TranspilerShouldSkipCallsInProfilerType => true;

		internal static IEnumerable<ProfilingInfo> Patches => patches.Values;
		internal static int PatchesCount => patches.Count;
		private static readonly Dictionary<MethodInformation, ProfilingInfo> patches = new Dictionary<MethodInformation, ProfilingInfo>();

		internal static bool TryGet([NotNull] MethodInformation info, out ProfilingInfo profile)
		{
			if (info == null) throw new ArgumentNullException(nameof(info));
			return patches.TryGetValue(info, out profile);
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
		private static void InitRuntime()
		{
			ApplyProfiledMethods();
		}

		private static async void ApplyProfiledMethods()
		{
			var settings = SelectiveProfilerSettings.instance;
			if (!settings.Enabled) return;
			
			// bool ProfilerWindowIsOpen()
			// {
			// 	var w = Resources.FindObjectsOfTypeAll(typeof(EditorWindow).Assembly.GetType("UnityEditor.ProfilerWindow")).FirstOrDefault();
			// 	return w;
			// }
			//
			// while (!ProfilerWindowIsOpen()) 
			// 	await Task.Delay(2000);
			while (!Profiler.enabled) await Task.Delay(100);
			
			var ml = settings.MethodsList;
			if (ml != null && ml.Count > 0)
			{
				foreach (var m in ml.ToArray())
				{
					if (m.TryResolveMethod(out var info))
						await EnableProfilingAsync(info, false);
				}
			}
		}

		[InitializeOnLoadMethod, RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void Init()
		{
			EditorApplication.playModeStateChanged -= OnPlayModeChanged;
			EditorApplication.playModeStateChanged += OnPlayModeChanged;

			SelectiveProfilerSettings.MethodStateChanged -= OnMethodChanged;
			SelectiveProfilerSettings.MethodStateChanged += OnMethodChanged;

			SelectiveProfilerSettings.Cleared -= MethodsCleared;
			SelectiveProfilerSettings.Cleared += MethodsCleared;

			EditorApplication.update -= OnEditorUpdate;
			EditorApplication.update += OnEditorUpdate;
		}

		private static readonly List<(MethodInformation method, bool state)> stateChangedList = new List<(MethodInformation, bool)>();

		private static void OnEditorUpdate()
		{
			if (stateChangedList.Count > 0)
			{
				var handled = 0;
				for (var index = stateChangedList.Count - 1; index >= 0; index--)
				{
					var changed = stateChangedList[index];
					stateChangedList.RemoveAt(index);
					var shouldBeActive = changed.state;
					if (patches.TryGetValue(changed.method, out var patch))
					{
						if (patch.IsActive == shouldBeActive) continue;

						if (shouldBeActive)
							EnableProfiling(patch.Method);
						else patch.Disable();
					}
					else if (shouldBeActive)
					{
						if (changed.method.TryResolveMethod(out var method))
						{
							EnableProfiling(method);
						}
					}

					++handled;
					if (handled >= 2) break;
				}
			}

			if (DeepProfileDebuggingMode)
				UpdateDeepProfileDebug();
		}

		private static void MethodsCleared()
		{
			foreach (var p in Patches)
				p.Disable();
		}

		private static void OnMethodChanged(MethodInformation method, bool enabled)
		{
			stateChangedList.RemoveAll(e => e.method.Equals(method));
			stateChangedList.Add((method, enabled));
		}

		private static void OnPlayModeChanged(PlayModeStateChange obj)
		{
			if (obj == PlayModeStateChange.ExitingPlayMode)
			{
				foreach (var patch in patches)
					PatchManager.UnregisterAndDisablePatch(patch.Value.Patch);
			}
		}

		private static readonly bool deepProfiling = SelectiveProfilerSettings.instance.DeepProfiling;
		private static readonly HashSet<MethodInfo> callsFound = new HashSet<MethodInfo>();

		internal static void RegisterInternalCalledMethod(MethodInfo method)
		{
			if (!deepProfiling) return;
			if (method == null) return;
			if (!method.HasMethodBody())
			{
				if (DebugLog)
					Debug.Log("Skip called method. No body: " + method);
				return;
			}

			if (callsFound.Contains(method)) return;
			// Debug.Log("FOUND " + method);
			callsFound.Add(method);
		}

		private static async void HandleNestedCalls(MethodInfo source, int depth)
		{
			if (!deepProfiling) return;
			if (callsFound.Count <= 0) return;

			var local = callsFound.ToArray(); 
			callsFound.Clear();
			foreach (var method in local)
			{
				// if debugging deep profiling applying nested methods will be handled by setting stepDeepProfile to true
				if (DeepProfileDebuggingMode)
				{
					if (stepDeepProfileList == null) stepDeepProfileList = new List<(MethodInfo, int, MethodInfo)>(100);
					if(!stepDeepProfileList.Any(e => e.method == method))
						stepDeepProfileList.Add((method, depth, source));
				}
				// dont save nested calls
				else
				{
					// Debug.Log(source + " calls " + method);
					await InternalEnableProfilingAsync(method, false, true, false, source, depth);
				}
			} 
		}

		
		internal static bool DevelopmentMode
		{
			get => SessionState.GetBool("SelectiveProfilerDevelopment", false);
			set => SessionState.SetBool("SelectiveProfilerDevelopment", value);
		}
		[MenuItem(MenuItems.Menu + nameof(EnableDevelopmentMode))]
		private static void EnableDevelopmentMode() => DevelopmentMode = true;
		[MenuItem(MenuItems.Menu + nameof(DisableDevelopmentMode))]
		private static void DisableDevelopmentMode() => DevelopmentMode = false;


		internal static bool stepDeepProfile;

		internal static int stepDeepProfileToIndex
		{
			get => SessionState.GetInt("StepDeepProfileDebugIndex", -1);
			set => SessionState.SetInt("StepDeepProfileDebugIndex", value);

		}
		private static List<(MethodInfo method, int depth, MethodInfo source)> stepDeepProfileList = null;
		internal static int deepProfileStepIndex;

		internal static bool DeepProfileDebuggingMode
		{
			get => SessionState.GetBool(nameof(DeepProfileDebuggingMode), false);
			set => SessionState.SetBool(nameof(DeepProfileDebuggingMode), value);
		}

		private static void UpdateDeepProfileDebug()
		{
			if (!DevelopmentMode) return;
			if (!stepDeepProfile) return;
			stepDeepProfile = false;
			if (stepDeepProfileList == null) return;
			if (deepProfileStepIndex >= stepDeepProfileList.Count) return;

			var method = stepDeepProfileList[deepProfileStepIndex];
			Debug.Log("Step " + deepProfileStepIndex + " / " + stepDeepProfileList.Count + ", Depth: " + method.depth + ": " + method.method.FullDescription());
			++deepProfileStepIndex;
#pragma warning disable 4014
			InternalEnableProfilingAsync(method.method, false, true,  false, method.source, method.depth);
#pragma warning restore 4014
			if (deepProfileStepIndex < stepDeepProfileToIndex)
				stepDeepProfile = true;
		}
	}
}