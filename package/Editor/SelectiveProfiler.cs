using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using JetBrains.Annotations;
using MonoMod.Utils;
using Needle.SelectiveProfiling.Utils;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
#if UNITY_2020_2_OR_NEWER
using UnityEditor.MPE;
#endif

namespace Needle.SelectiveProfiling
{
	// [AlwaysProfile]
	public static class SelectiveProfiler
	{
		public static string SamplePostfix => DevelopmentMode ? "[dev]" : DebugLog ? "[debug]" : string.Empty;
		internal static bool DrawItemDebugInformationInTreeView;

		// private static MethodInfo previouslySelectedImmediateProfilingMethod;

		internal static void SelectedForImmediateProfiling(MethodInfo method)
		{
			if (method == null) return;
			if (!Application.isPlaying) return;
			if (!SelectiveProfilerSettings.Instance.ImmediateMode) return;
			// if (previouslySelectedImmediateProfilingMethod != null && previouslySelectedImmediateProfilingMethod != method)
			// {
			// 	var mi = new MethodInformation(previouslySelectedImmediateProfilingMethod);
			// 	if (!SelectiveProfilerSettings.Instance.IsEnabledExplicitly(mi))
			// 	{
			// 		DisableProfiling(previouslySelectedImmediateProfilingMethod);
			// 	}
			// }
			// previouslySelectedImmediateProfilingMethod = method;
			EnableProfilingAsync(method, false, true, true);
		}

		public static bool IsProfiling(MethodInfo method, bool onlySaved = false)
		{
			if (method == null) return false;
			
			if (onlySaved)
			{
				var info = new MethodInformation(method);
				return SelectiveProfilerSettings.Instance.IsSavedAndEnabled(info);
			}


			if (IsStandaloneProcess)
			{
				if (!patchesStateSyncedFromEditor.TryGetValue(new MethodInformation(method), out var state)) return false;
				return state;
			}


			if (!profiled.TryGetValue(method, out var pi )) return false;
			return pi.IsActive;// && Patches.Any(e => e.IsActive && e.Method == method);
		}

		public static bool EnableProfiling([NotNull] MethodInfo method,
			bool save = true,
			bool enablePatch = true,
			bool enableIfMuted = false,
			bool forceLogs = false)
		{
			var task = EnableProfilingAsync(method, save, enablePatch, enableIfMuted, forceLogs);
			task.RunSynchronously();
			return task.Result;
		}

		public static Task<bool> EnableProfilingAsync([NotNull] MethodInfo method,
			bool save = true,
			bool enablePatch = true,
			bool enableIfMuted = false,
			bool forceLogs = false)
		{
			return InternalEnableProfilingAsync(method, save, enablePatch, enableIfMuted, null, 0, forceLogs);
		}

		/// <summary>
		/// by default only save when application is not playing
		/// </summary>
		internal static bool ShouldSave => !Application.isPlaying || SelectiveProfilerSettings.instance.RuntimeSave;

		/// <summary>
		/// check editor state (this does not settings enabled state)
		/// </summary>
		internal static bool AllowToBeEnabled => !ProfilerDriver.deepProfiling || DevelopmentMode;

		private static async Task<bool> InternalEnableProfilingAsync(
			MethodInfo method,
			bool save = true,
			bool enablePatch = true,
			bool enableIfMuted = false,
			MethodInfo source = null,
			int depth = 0,
			bool forceLogs = false
		)
		{
			if (method == null) throw new ArgumentNullException(nameof(method));

			if (IsStandaloneProcess)
			{
#if UNITY_2020_2_OR_NEWER
				var cmd = new EnableProfilingCommand(new MethodInformation(method))
				{
					Enable = enablePatch,
					EnableIfMuted = enableIfMuted,
					ForceLogs = forceLogs
				};
				QueueCommand(cmd);
#endif
				return true;
			}

			var settings = SelectiveProfilerSettings.Instance;
			if (!settings.Enabled) return false;

			if (AccessUtils.TryGetDeclaredMember(method, out var declared))
				method = declared;

			var isDeep = source != null && method != source;
			if (!AccessUtils.AllowPatching(method, isDeep, settings.DebugLog || forceLogs))
			{
				return false;
			}

			void HandleCallstackRegistration(ProfilingInfo current)
			{
				if (source == null) return;
				var sourcePatch = profiled.FirstOrDefault(p => p.Value.Method == source).Value;
				if (sourcePatch != null)
				{
					current.AddCaller(sourcePatch);
				}
			}

			// void HandleDeepProfiling()
			// {
			// 	if (!Application.isPlaying) return;
			// 	var nextLevel = ++depth;
			// 	if (nextLevel < settings.MaxDepth)
			// 	{
			// 		HandleNestedCalls(method, nextLevel);
			// 	}
			// }

			var methodInfo = settings.GetInstance(new MethodInformation(method));
			if (enableIfMuted) methodInfo.Enabled = true;

			if (profiled.TryGetValue(method, out var existingProfilingInfo))
			{
				existingProfilingInfo.MethodInformation = methodInfo;
				HandleCallstackRegistration(existingProfilingInfo);

				if ((!deepProfiling && ShouldSave) || save)
				{
					settings.Add(methodInfo);
				}

				if (!existingProfilingInfo.IsActive)
				{
					await existingProfilingInfo.Enable();
					// HandleDeepProfiling();
				}

				return existingProfilingInfo.IsActive;
			}

			if (save)
			{
				settings.Add(methodInfo);
				settings.Save();
			}


			var patch = new ProfilerSamplePatch.TranspilerPatch(method, null, " " + SamplePostfix, depth, settings.MaxDepth);
			patch.PatchThreaded = true;
			var info = new ProfilingInfo(patch, method, methodInfo);
			profiled.Add(method, info);
			profiled2.Add(methodInfo, info);
			
			HandleCallstackRegistration(info);
			// PatchManager.RegisterPatch(patch);

			var enabled = false;
			if (enablePatch)
			{
				var muted = !methodInfo.Enabled;
				if (enableIfMuted && muted) settings.SetMuted(methodInfo, false);
				if (!muted)
				{
					enabled = await info.Enable();
					if (!enabled && DebugLog)
					{
						Debug.LogError("Did not enable: " + method + ", " + info.IsActive + ", " + info.Identifier + ", hasBody: " + method.HasMethodBody());
					}
				}
			}
			return enabled;
		}

		[MenuItem(MenuItems.ToolsMenu + nameof(DisableProfilingAll))]
		public static void DisableProfilingAll()
		{
			foreach (var e in profiled)
			{
				DisableProfiling(e.Key);
			}
		}

		public static Task DisableProfiling(MethodInfo method, bool allowSave = true)
		{
			if (method == null) throw new ArgumentNullException(nameof(method));
			if (alwaysProfile.Contains(method)) return Task.CompletedTask;
			
#if UNITY_2020_2_OR_NEWER
			if (IsStandaloneProcess)
			{
				var networkCommand = new DisableProfilingCommand(new MethodInformation(method), allowSave);
				QueueCommand(networkCommand);
				return Task.CompletedTask;
			}
#endif

			Task task = null;
			allowSave &= ShouldSave;

			if (profiled.TryGetValue(method, out var prof))
			{
				task = prof.Disable();
				// internally prof disable will disable the enabled state
				// if the method is currently profiled and we disable it without saving
				// we can assume that is was enabled before (and should stay that way)
				if (!allowSave)
					prof.MethodInformation.Enabled = true;
			}

			if (allowSave)
			{
				var mi = new MethodInformation(method);
				mi = SelectiveProfilerSettings.Instance.GetInstance(mi);
				SelectiveProfilerSettings.Instance.UpdateState(mi, false, true);
			}

			return task ?? Task.CompletedTask;
		}

		internal static Task DisableAndForget(MethodInfo info)
		{
			Task task = null;
			if (profiled.TryGetValue(info, out var prof))
			{
				// dont need to save state change because we remove it here anyways
				task = DisableProfiling(info, false);
				profiled.Remove(info);
				var match = profiled2.FirstOrDefault(e => e.Value == prof).Key;
				if (match != null) profiled2.Remove(match);
			}
			if(ShouldSave)
				SelectiveProfilerSettings.Instance.Remove(info);
			return task ?? Task.CompletedTask;
		}

		internal static Task DisableAndForget(MethodInformation info)
		{
			Task task = null;
			if (profiled2.TryGetValue(info, out var prof))
			{
				// dont need to save state change because we remove it here anyways
				task = DisableProfiling(prof.Method, false);
				profiled2.Remove(info);
				var match = profiled.FirstOrDefault(e => e.Value == prof).Key;
				if (match != null) profiled.Remove(match);
			}
			if(ShouldSave)
				SelectiveProfilerSettings.Instance.Remove(info);
			return task ?? Task.CompletedTask;
		}

		internal static bool DebugLog => SelectiveProfilerSettings.Instance.DebugLog;
		internal static bool TranspilerShouldSkipCallsInProfilerType => true;

		internal static IEnumerable<ProfilingInfo> Patches => profiled.Values;
		internal static IEnumerable<MethodInfo> PatchedMethods => profiled.Keys;
		internal static IEnumerable<MethodInformation> PatchedMethodsInfo => profiled2.Keys;
		internal static int PatchesCount => profiled.Count;
		internal static bool AnyEnabled => profiled.Any(kvp => kvp.Value.IsActive);
		
		private static readonly Dictionary<MethodInfo, ProfilingInfo> profiled = new Dictionary<MethodInfo, ProfilingInfo>();
		private static readonly Dictionary<MethodInformation, ProfilingInfo> profiled2 = new Dictionary<MethodInformation, ProfilingInfo>();
		
		/// <summary>
		/// methods marked with AlwaysProfile attribute
		/// </summary>
		private static readonly HashSet<MethodInfo> alwaysProfile = new HashSet<MethodInfo>();

		/// <summary>
		/// should only be used in standalone profiler instance, profiled method enabled state synced from editor to standalone profiler
		/// </summary>
		internal static Dictionary<MethodInformation, bool> patchesStateSyncedFromEditor;


		internal static bool TryGet([NotNull] MethodInfo info, out ProfilingInfo profile)
		{
			if (info == null) throw new ArgumentNullException(nameof(info));
			profile = null;
			return profiled.TryGetValue(info, out profile);
		}
		
		internal static bool TryGet([NotNull] MethodInformation info, out ProfilingInfo profile)
		{
			if (info == null) throw new ArgumentNullException(nameof(info));
			profile = null;
			return profiled2.TryGetValue(info, out profile);
		}

		[InitializeOnLoadMethod]
		// [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
		private static async void InitApply()
		{
			if (!AllowToBeEnabled) return;
			if (IsStandaloneProcess) return;
			var settings = SelectiveProfilerSettings.Instance;
			if (!settings.Enabled) return;
			while (!Profiler.enabled) await Task.Delay(100);
			ApplyProfiledMethods();
		}

		[InitializeOnLoadMethod, RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void Init()
		{
			if (!AllowToBeEnabled) return;

			SelectiveProfilerSettings.MethodStateChanged -= OnMethodChanged;
			SelectiveProfilerSettings.MethodStateChanged += OnMethodChanged;

			SelectiveProfilerSettings.Cleared -= MethodsCleared;
			SelectiveProfilerSettings.Cleared += MethodsCleared;

			EditorApplication.update -= OnEditorUpdate;
			EditorApplication.update += OnEditorUpdate;
		}

		private static async void ApplyProfiledMethods()
		{
			var settings = SelectiveProfilerSettings.Instance;
			if (settings.UseAlwaysProfile)
			{
#if UNITY_2020_1_OR_NEWER
				var typesToProfile = TypeCache.GetTypesWithAttribute<AlwaysProfile>();
				foreach (var type in typesToProfile)
				{
					var methods = AccessUtils.GetMethods(type, typeof(MonoBehaviour));
					foreach (var method in methods)
					{
						alwaysProfile.Add(method);
						await EnableProfilingAsync(method, false, true, false);
					}
				}

				var methodsToProfile = TypeCache.GetMethodsWithAttribute<AlwaysProfile>();
				foreach (var method in methodsToProfile)
				{
					alwaysProfile.Add(method);
					await EnableProfilingAsync(method, false, true, false);
				}
#endif
			}
			
			var ml = settings.MethodsList;
			if (ml != null && ml.Count > 0) 
			{
				var methodsList = ml.ToArray();
				foreach (var m in methodsList)
				{
					if (m.Enabled && m.TryResolveMethod(out var info))
					{
						await EnableProfilingAsync(info, false);
					}
				}
			}
		}

		private static readonly List<(MethodInformation method, bool state)> stateChangedList = new List<(MethodInformation, bool)>();

		private static async void OnEditorUpdate()
		{
#if UNITY_2020_2_OR_NEWER
			if (queuedCommands.Count > 0)
			{
				var cmd = queuedCommands.Dequeue();
				SendCommandNow(cmd);
			}
#endif

			if (stateChangedList.Count > 0)
			{
				if (!SelectiveProfilerSettings.Instance.Enabled) return;

				var handled = 0;
				for (var index = stateChangedList.Count - 1; index >= 0; index--)
				{
					var changed = stateChangedList[index];
					stateChangedList.RemoveAt(index);
					var shouldBeActive = changed.state;
					if (changed.method.TryResolveMethod(out var method))
					{
						if (profiled.TryGetValue(method, out var patch))
						{
							if (patch.IsActive == shouldBeActive) continue;

							if (shouldBeActive)
								await EnableProfilingAsync(patch.Method);
							else await patch.Disable();
						}
						else if (shouldBeActive)
						{
							await EnableProfilingAsync(method);
						}
						++handled;
					}
					if (handled >= 2) break;
				}
			}
			
			if (DeepProfileDebuggingMode)
				UpdateDeepProfileDebug();

			var settings = SelectiveProfilerSettings.instance;
			callsFoundInFlight.Clear();
			callsFoundInFlight.AddRange(callsFound);
			callsFound.Clear();
			foreach (var kvp in callsFoundInFlight)
			{
				var method = kvp.Key;
				var info = kvp.Value;
				var nextDepth = info.depth + 1;
				if (nextDepth >= settings.MaxDepth) continue;
				if (await InternalEnableProfilingAsync(method, false, true, true, info.callers.FirstOrDefault(), nextDepth))
				{
					if (profiled.TryGetValue(method, out var profilingInfo))
					{
						for (int i = 1; i < info.callers.Count; i++)
						{
							var source = info.callers[i];
							var sourcePatch = profiled.FirstOrDefault(p => p.Value.Method == source).Value;
							if (sourcePatch != null)
							{
								profilingInfo.AddCaller(sourcePatch);
							}
						}
					}
					// var info = profiled
				}
			}
			callsFound.Clear();
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

		private static readonly bool deepProfiling = SelectiveProfilerSettings.Instance.DeepProfiling;
		private static readonly Dictionary<MethodInfo, (int depth, List<MethodInfo> callers)> callsFound = new Dictionary<MethodInfo, (int depth, List<MethodInfo> callers)>();
		private static readonly Dictionary<MethodInfo, (int depth, List<MethodInfo> callers)> callsFoundInFlight = new Dictionary<MethodInfo, (int depth, List<MethodInfo> callers)>();

		internal static void RegisterInternalCalledMethod(MethodInfo source, MethodInfo method, int depth)
		{
			if (!deepProfiling) return;
			if (method == null) return;
			if (callsFound.ContainsKey(method))
			{
				// use smallest depth if the method is called from multiple places and deep profiling is enabled
				var val = callsFound[method];
				val.depth = Mathf.Min(depth, val.depth);
				if(!val.callers.Contains(source))
					val.callers.Add(source);
				callsFound[method] = val;
				return;
			}
			var settings = SelectiveProfilerSettings.Instance;
			if (method.DeclaringType != null && !AccessUtils.AllowedLevel(method, settings.DeepProfileMaxLevel)) return;
			if (!AccessUtils.AllowPatching(method, true, DebugLog)) return;
			callsFound.Add(method, (depth, new List<MethodInfo> {source}));
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

		[MenuItem(MenuItems.ToolsMenu + "Dev/" + nameof(DisableDevelopmentMode), false, 50000)]
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
			InternalEnableProfilingAsync(method.method, false, true, false, method.source, method.depth);
#pragma warning restore 4014
			if (deepProfileStepIndex < stepDeepProfileToIndex)
				stepDeepProfile = true;
		}

		
#if !UNITY_2020_2_OR_NEWER
		public static bool IsStandaloneProcess => false;
#endif

#if UNITY_2020_2_OR_NEWER
		public static bool IsStandaloneProcess { get; private set; }
		private const string selectiveProfilerCommandEditorChannel = nameof(selectiveProfilerCommandEditorChannel);
		private const string selectiveProfilerCommandStandaloneChannel = nameof(selectiveProfilerCommandStandaloneChannel);
		private static readonly Queue<NetworkCommand> queuedCommands = new Queue<NetworkCommand>();

#if UNITY_2021_1_OR_NEWER
		[RoleProvider(ProcessLevel.Main, ProcessEvent.AfterDomainReload)]
#elif UNITY_2020_2_OR_NEWER
		[RoleProvider(ProcessLevel.Master, ProcessEvent.AfterDomainReload)]
#endif
		// ReSharper disable once UnusedMember.Local
		private static void InitMain()
		{
			if (!ChannelService.IsRunning()) ChannelService.Start();
			EventService.RegisterEventHandler(selectiveProfilerCommandEditorChannel, HandleReceivedEvent);
		}

#if  UNITY_2021_1_OR_NEWER
		[RoleProvider(ProcessLevel.Secondary, ProcessEvent.AfterDomainReload)]
#elif UNITY_2020_2_OR_NEWER
		[RoleProvider(ProcessLevel.Slave, ProcessEvent.AfterDomainReload)]
#endif
		// ReSharper disable once UnusedMember.Local
		private static void InitSlave()
		{
			patchesStateSyncedFromEditor = new Dictionary<MethodInformation, bool>();
			IsStandaloneProcess = true;
			EventService.RegisterEventHandler(selectiveProfilerCommandStandaloneChannel, HandleReceivedEvent);
		}

		private static void HandleReceivedEvent(string eventType, object[] args)
		{
			foreach (var arg in args)
			{
				if (arg is NetworkCommand cmd)
				{
					// Debug.Log("Received " + cmd);
					cmd.Execute();
				}
			}
		}

		internal static void QueueCommand(NetworkCommand cmd)
		{
			queuedCommands.Enqueue(cmd);
		}

		internal static void SendCommandNow(NetworkCommand cmd)
		{
			if (!ChannelService.IsRunning()) return;
			// send command to respective other channel
			var channel = IsStandaloneProcess ? selectiveProfilerCommandEditorChannel : selectiveProfilerCommandStandaloneChannel;
			EventService.Emit(channel, cmd);
		}
#endif


		// ReSharper disable once UnusedParameter.Global
		internal static bool InjectSampleWithCallback(MethodBase method)
		{
			return false;
		}

		// public static HashSet<object> SpecialObjects = new HashSet<object>();

		// private static readonly Stack<object> sampleStack = new Stack<object>();

		// a call to this method will be injected when/if returning true in InjectSampleWithCallback
		internal static string OnSampleCallback(object caller, string methodName)
		{
			return methodName;
			// if (caller == null) return methodName;
			//
			// // if (!SpecialObjects.Contains(caller))
			// // {
			// // 	return "ignored";
			// // }
			//
			// if (Application.isPlaying && caller is Object obj && obj)
			// {
			// 	if (sampleStack.Contains(caller)) return methodName;
			// 	sampleStack.Push(caller);
			// 	var id = obj.GetInstanceID();
			// 	sampleStack.Pop();
			// 	methodName += id;
			// }
			//
			// return methodName;
		}
	}
}