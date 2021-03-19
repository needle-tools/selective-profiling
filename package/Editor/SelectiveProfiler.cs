using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using JetBrains.Annotations;
using needle.EditorPatching;
using Needle.SelectiveProfiling.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace Needle.SelectiveProfiling
{
	public static class SelectiveProfiler
	{
		public const string SamplePostfix = "[needle]";

		// private static MethodInfo previouslySelectedImmediateProfilingMethod;

		internal static void SelectedForImmediateProfiling(MethodInfo method)
		{
			if (method == null) return;
			if (!Application.isPlaying) return;
			if (!SelectiveProfilerSettings.instance.ImmediateMode) return;
			// if (previouslySelectedImmediateProfilingMethod != null && previouslySelectedImmediateProfilingMethod != method)
			// {
			// 	var mi = new MethodInformation(previouslySelectedImmediateProfilingMethod);
			// 	if (!SelectiveProfilerSettings.instance.IsEnabledExplicitly(mi))
			// 	{
			// 		DisableProfiling(previouslySelectedImmediateProfilingMethod);
			// 	}
			// }
			// previouslySelectedImmediateProfilingMethod = method;
			EnableProfiling(method, false, true, true);
		}

		public static bool IsProfiling(MethodInfo method, bool onlySaved = false)
		{
			if (onlySaved)
			{
				var info = new MethodInformation(method);
				return SelectiveProfilerSettings.instance.IsSavedAndEnabled(info);
			}
			return Patches.Any(e => e.IsActive && e.Method == method);
		}

		public static async void EnableProfiling([NotNull] MethodInfo method, bool save = true, bool enablePatch = true, bool enableIfMuted = false , bool forceLogs = false)
		{
			await EnableProfilingAsync(method, save, enablePatch, enableIfMuted, forceLogs);
		}

		public static Task EnableProfilingAsync([NotNull] MethodInfo method, bool save = true, bool enablePatch = true, bool enableIfMuted = false, bool forceLogs = false)
		{
			return InternalEnableProfilingAsync(method, save, enablePatch, enableIfMuted, null, 0, forceLogs);
		}

		/// <summary>
		/// by default only save when application is not playing
		/// </summary>
		internal static bool ShouldSave => !Application.isPlaying;

		private static async Task InternalEnableProfilingAsync(MethodInfo method,
			bool save = true,
			bool enablePatch = true,
			bool enableIfMuted = false,
			MethodInfo source = null,
			int depth = 0,
			bool forceLogs = false
		)
		{
			var settings = SelectiveProfilerSettings.instance;
			if (!settings.Enabled) return;

			if (method == null) throw new ArgumentNullException(nameof(method));

			var isDeep = source != null && method != source;
			if (!AccessUtils.AllowPatching(method, isDeep, settings.DebugLog || forceLogs))
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
				if (!Application.isPlaying) return;
				var nextLevel = ++depth;
				if (nextLevel < settings.MaxDepth)
				{
					HandleNestedCalls(method, nextLevel);
				}
			}

			var mi = new MethodInformation(method);
			settings.GetInstance(ref mi);
			if (enableIfMuted) mi.Enabled = true;
			
			if (patches.TryGetValue(mi, out var existingProfilingInfo))
			{
				existingProfilingInfo.MethodInformation = mi;
				HandleCallstackRegistration(existingProfilingInfo);

				if (ShouldSave || save)
				{
					settings.Add(mi);
				}
				
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
			var mi = new MethodInformation(method);
			if (patches.TryGetValue(mi, out var prof))
			{
				prof.Disable();
			}

			if (!Application.isPlaying)
			{
				SelectiveProfilerSettings.instance.GetInstance(ref mi);
				SelectiveProfilerSettings.instance.UpdateState(mi, false, true);
			}
		}
		

		internal static IEnumerable<string> ExpectedPatches()
		{
			yield return typeof(ProfilerFrameDataView_Patch).FullName;
			yield return typeof(ContextMenuPatches).FullName;
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
		private static async void InitRuntime()
		{
			var settings = SelectiveProfilerSettings.instance;
			if (!settings.Enabled) return;
			while (!Profiler.enabled) await Task.Delay(100);
			ApplyProfiledMethods();
		}

		private static async void ApplyProfiledMethods()
		{
#if UNITY_2020_1_OR_NEWER
			var typesToProfile = TypeCache.GetTypesWithAttribute<AlwaysProfile>();
			foreach (var type in typesToProfile)
			{
				var methods = AccessUtils.GetMethods(type, AccessUtils.AllDeclared, typeof(MonoBehaviour));
				foreach (var method in methods) 
					EnableProfiling(method, false, true, false);
			}
			var methodsToProfile = TypeCache.GetMethodsWithAttribute<AlwaysProfile>();
			foreach (var method in methodsToProfile)
			{
				EnableProfiling(method, false, true, false); 
			}
#endif
			var settings = SelectiveProfilerSettings.instance;
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
			if (!SelectiveProfilerSettings.instance.Enabled) return;
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
			var settings = SelectiveProfilerSettings.instance;
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
					var dt = method.DeclaringType;
					if (dt != null)
					{
						if (!AccessUtils.AllowedLevel(method, settings.DeepProfileMaxLevel))
						{
							continue;
						}
					}
					
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
		[MenuItem(MenuItems.ToolsMenu + "Dev/" + nameof(EnableDevelopmentMode), false, 50000)]
		private static void EnableDevelopmentMode() => DevelopmentMode = true;
		[MenuItem(MenuItems.ToolsMenu + "Dev/" + nameof(EnableDevelopmentMode), true, 50000)]
		private static bool EnableDevelopmentMode_Validate() => !DevelopmentMode;
		[MenuItem(MenuItems.ToolsMenu + "Dev/" + nameof(DisableDevelopmentMode), false,  50000)]
		private static void DisableDevelopmentMode() => DevelopmentMode = false;
		[MenuItem(MenuItems.ToolsMenu + "Dev/" + nameof(DisableDevelopmentMode), true, 50000)]
		private static bool DisableDevelopmentMode_Validate() => DevelopmentMode;

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