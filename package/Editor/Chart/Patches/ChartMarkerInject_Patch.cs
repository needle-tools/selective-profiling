using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using HarmonyLib;
using JetBrains.Annotations;
using needle.EditorPatching;
using UnityEngine;
using UnityEngine.Profiling;

namespace Needle.SelectiveProfiling
{
	[NoAutoDiscover]
	internal class ChartMarkerInject_Patch : EditorPatchProvider
	{
		internal readonly List<AddProfilerMarker> Patches = new List<AddProfilerMarker>();

		private readonly string id;
		public override string ID() => id;
		public override string DisplayName => ID();

		public ChartMarkerInject_Patch(string id)
		{
			this.id = id;
		}

		public override bool OnWillEnablePatch()
		{
			if (Patches.Count <= 0) return false;
			return base.OnWillEnablePatch();
		}

		protected override void OnGetPatches(List<EditorPatch> patches)
		{
			patches.AddRange(Patches);
		}

		public class AddProfilerMarker : EditorPatch
		{
			private static readonly Dictionary<MethodBase, string> Labels = new Dictionary<MethodBase, string>();

			private readonly string label;
			private readonly MethodBase method;
			[CanBeNull] public IEnumerable<MethodBase> additional;

			public AddProfilerMarker(string label, MethodBase method)
			{
				this.label = label;
				System.Diagnostics.Debug.Assert(method != null, nameof(this.method) + " != null");
				this.method = method;
			}

			protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
			{
				void Add(MethodBase _method)
				{
					targetMethods.Add(_method);
					if (Labels.ContainsKey(method))
					{
						var existing = Labels[method];
						if (existing != label)
						{
							Debug.Log("Label is already registered " + Labels[method] + ", will override with " + label);
							Labels[method] = label;
						}
					}
					else
						Labels.Add(_method, label);
				}

				Add(method);
				if (additional != null)
				{
					foreach (var ad in additional)
					{
						if (ad == method) continue;
						Add(ad);
					}
				}

				return Task.CompletedTask;
			}

			// ReSharper disable once UnusedMember.Local
			private static IEnumerable<CodeInstruction> Transpiler(MethodBase method, IEnumerable<CodeInstruction> inst)
			{
				var marker = Labels[method];
				ProfilerMarkerStore.AddExpectedMarker(marker);

				void Log(object msg)
				{
					if (SelectiveProfiler.DebugLog == false) return;
					if (msg != null)
						Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, msg.ToString());
				}

				Log("-----------------------------");
				Log("Patch " + marker + " in " + method);

				CodeInstruction Emit(CodeInstruction i)
				{
					Log(i);
					return i;
				}

				yield return Emit(new CodeInstruction(OpCodes.Ldstr, marker));
				yield return Emit(CodeInstruction.Call(typeof(Profiler), nameof(Profiler.BeginSample), new[] {typeof(string)}));

				// var end = CodeInstruction.Call(typeof(Profiler), nameof(Profiler.EndSample));
				// yield return Emit(end);

				foreach (var i in inst)
				{
					if (i.opcode == OpCodes.Ret || i.opcode == OpCodes.Throw)
					{
						var end = CodeInstruction.Call(typeof(Profiler), nameof(Profiler.EndSample));
						i.MoveLabelsTo(end);
						yield return Emit(end);
					}

					yield return Emit(i);
				}

				Log("-----------------------------");
			}
		}
	}
}