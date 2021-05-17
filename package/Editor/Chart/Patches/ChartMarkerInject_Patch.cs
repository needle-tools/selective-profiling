using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Profiling;

namespace Needle.SelectiveProfiling
{
	internal class ChartMarkerInject_Patch
	{
		internal readonly List<AddProfilerMarker> Patches = new List<AddProfilerMarker>();

		public class AddProfilerMarker : PatchBase
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
			
			protected override IEnumerable<MethodBase> GetPatches()
			{
				MethodBase Add(MethodBase _method)
				{
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
					
					return method;
				}

				yield return Add(method);
				if (additional != null)
				{
					foreach (var ad in additional)
					{
						if (ad == method) continue;
						yield return Add(ad);
					}
				}
			}

			// ReSharper disable once UnusedMember.Local
			private static IEnumerable<CodeInstruction> Transpiler(MethodBase method, IEnumerable<CodeInstruction> inst)
			{
				var marker = Labels[method];
				ProfilerMarkerStore.AddExpectedMarker(marker);

				void Log(object msg)
				{
					if (msg == null) return;
					if (!SelectiveProfiler.DebugLog || !SelectiveProfiler.DevelopmentMode) return;
					Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "<color=grey>" + msg + "</color>");
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