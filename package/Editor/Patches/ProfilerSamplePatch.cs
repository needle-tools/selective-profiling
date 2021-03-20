#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using HarmonyLib;
using needle.EditorPatching;
using Needle.SelectiveProfiling.CodeWrapper;
using UnityEngine;
using UnityEngine.Profiling;

// ReSharper disable UnusedType.Global

namespace Needle.SelectiveProfiling
{
	// Limitations/Unsupported use cases: https://harmony.pardeike.net/articles/patching.html#commonly-unsupported-use-cases
	// - Generic Methods are experimental and might not work -> https://harmony.pardeike.net/articles/patching-edgecases.html#generics

	[NoAutoDiscover]
	public class ProfilerSamplePatch : EditorPatchProvider
	{
		public ProfilerSamplePatch(MethodBase method, string prefix = null, string postfix = null)
		{
			this.method = method;
			this.prefix = prefix;
			this.postfix = postfix;
		}

		public override string DisplayName => ID();
		public override string ID() => method != null ? method.DeclaringType?.FullName + "." + method.Name : base.ID();
		public override bool Persistent() => false;

		private readonly string prefix;
		private readonly string postfix;
		private readonly MethodBase method;

		protected override void OnGetPatches(List<EditorPatch> patches)
		{
			if (method != null)
				patches.Add(new TranspilerPatch(method, prefix, postfix));
		}

		private class TranspilerPatch : EditorPatch
		{
			private static readonly Dictionary<MethodBase, ICodeWrapper> wrappers = new Dictionary<MethodBase, ICodeWrapper>();
			private readonly MethodBase method;
			private readonly string prefix;
			private readonly string postfix;

			public TranspilerPatch(MethodBase methods, string prefix, string postfix)
			{
				this.method = methods;
				if (string.IsNullOrEmpty(prefix)) prefix = string.Empty;
				if (string.IsNullOrEmpty(postfix)) postfix = string.Empty;
				this.prefix = prefix;
				this.postfix = postfix;
				
				ICodeWrapper wrapper = new MethodWrapper(
					new InstructionsWrapper(), 
					OnBeforeInjectBeginSample,
					SelectiveProfiler.DebugLog,
					SelectiveProfiler.TranspilerShouldSkipCallsInProfilerType
					);
				
				wrappers.Add(method, wrapper);
			}

			protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
			{
				if (method == null) return Task.CompletedTask;
				targetMethods.Add(method);
				return Task.CompletedTask;
			}

			private static IEnumerable<CodeInstruction> Transpiler(MethodBase method, IEnumerable<CodeInstruction> _inst)
			{
				if (_inst == null) return null;
				
				if (!wrappers.TryGetValue(method, out var wrapper)) return _inst;
				var instructions = _inst as List<CodeInstruction> ?? _inst.ToList();
				if (SelectiveProfiler.InjectSampleWithCallback(method))
				{
					wrapper.Apply(method, instructions, InsertBeforeWithCallback, InsertAfter);
				}
				else
				{
					wrapper.Apply(method, instructions, InsertBeforeConstant, InsertAfter);
				}
				return instructions;
			}
			
			
			private void OnBeforeInjectBeginSample(MethodBase currentMethod, CodeInstruction instruction, int index)
			{
				var methodName = TranspilerUtils.TryGetMethodName(instruction.opcode, instruction.operand, false);
				
				if (SelectiveProfiler.InjectSampleWithCallback(currentMethod))
				{
					// load reference or null if static
					InsertBeforeWithCallback[0] = new CodeInstruction(currentMethod == null || currentMethod.IsStatic ? OpCodes.Ldnull : OpCodes.Ldarg_0);
					InsertBeforeWithCallback[1] = new CodeInstruction(OpCodes.Ldstr, prefix + methodName + postfix);
					InsertBeforeWithCallback[2] = CodeInstruction.Call(typeof(SelectiveProfiler), nameof(SelectiveProfiler.OnSampleCallback), new []{typeof(object), typeof(string)});
				}
				else
				{
					InsertBeforeConstant[0] = new CodeInstruction(OpCodes.Ldstr, prefix + methodName + postfix);
				}
			}
			
			private static readonly List<CodeInstruction> InsertBeforeConstant = new List<CodeInstruction>()
			{
				new CodeInstruction(OpCodes.Ldstr, "%MARKER%"),
				new CodeInstruction(OpCodes.Nop),
				CodeInstruction.Call(typeof(Profiler), nameof(Profiler.BeginSample), new[] {typeof(string)}),
			};

			private static readonly List<CodeInstruction> InsertBeforeWithCallback = new List<CodeInstruction>()
			{
				new CodeInstruction(OpCodes.Ldarg_0), // load "this"
				new CodeInstruction(OpCodes.Ldstr, "%MARKER%"),
				new CodeInstruction(OpCodes.Nop), // will be replaced by load call to SelectiveProfiler.GetName
				CodeInstruction.Call(typeof(Profiler), nameof(Profiler.BeginSample), new[] {typeof(string)}),
			};

			private static readonly List<CodeInstruction> InsertAfter = new List<CodeInstruction>()
			{
				CodeInstruction.Call(typeof(Profiler), nameof(Profiler.EndSample)),
			};

			// private static LocalBuilder Builder => AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("test"), AssemblyBuilderAccess.Run).DefineDynamicModule("test")
			// 	.DefineType("test").DefineMethod("test", MethodAttributes.Private).GetILGenerator().DeclareLocal(typeof(ProfilerMarker));

			// private static readonly IList<CodeInstruction> InsertBefore = new List<CodeInstruction>()
			// {
			// 	new CodeInstruction(OpCodes.Ldsfld, typeof(TranspilerTest).GetMember(nameof(myMarker), (BindingFlags)~0).First()),
			// 	new CodeInstruction(OpCodes.Stloc_0),
			// 	new CodeInstruction(OpCodes.Ldloca_S, Builder),
			// 	CodeInstruction.Call(() => myMarker.Begin()),
			// 	new CodeInstruction(OpCodes.Nop),
			// };
			//
			// private static readonly IList<CodeInstruction> InsertAfter = new List<CodeInstruction>()
			// {
			// 	new CodeInstruction(OpCodes.Nop),
			// 	new CodeInstruction(OpCodes.Ldsfld, typeof(TranspilerTest).GetMember(nameof(myMarker), (BindingFlags)~0).First()),
			// 	new CodeInstruction(OpCodes.Stloc_0),
			// 	new CodeInstruction(OpCodes.Ldloca_S, Builder),
			// 	CodeInstruction.Call(() => myMarker.End()),
			// };


		}
	}
}

#endif