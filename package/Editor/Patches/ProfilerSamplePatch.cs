#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using HarmonyLib;
using needle.EditorPatching;
using Needle.SelectiveProfiling.CodeWrapper;
using Needle.SelectiveProfiling.Utils;
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
			this._id = method != null ? method.DeclaringType?.FullName + "." + method.Name : base.ID();
			this.Group = "Selective Profiling Sampler";
		}

		private readonly string _id;

		public override string DisplayName => ID();
		public override string ID() => _id;
		public override bool Persistent() => false;

		private readonly string prefix;
		private readonly string postfix;
		private readonly MethodBase method;
		
		internal const char TypeSampleNameSeparator = '/';

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

			// ReSharper disable once UnusedMember.Local
			private static IEnumerable<CodeInstruction> Transpiler(MethodBase method, IEnumerable<CodeInstruction> _inst, ILGenerator il)
			{
				if (_inst == null) return null;
				
				if (!wrappers.TryGetValue(method, out var wrapper)) return _inst;
				var instructions = _inst as List<CodeInstruction> ?? _inst.ToList();
				wrapper.Apply(method, instructions, il);
				return instructions;
			}

			internal string GetSampleName(MethodBase currentMethod, CodeInstruction instruction)
			{
				return prefix + TranspilerUtils.GetSampleName(currentMethod, instruction.opcode, instruction.operand, false) + postfix;
			}

			private (IList<CodeInstruction> before, IList<CodeInstruction> after) OnBeforeInjectBeginSample(MethodBase currentMethod, CodeInstruction instruction, int index, ILGenerator il)
			{
				var parentType = currentMethod.DeclaringType?.Name;
				if (SelectiveProfiler.DebugLog)
					parentType += "." + currentMethod.Name + "[" + index + "]";
				
				var sampleName = GetSampleName(currentMethod, instruction);
				var label = il.DefineLabel();
				
				// when using the custom rows patch prefix the sample with the method name
				if (!string.IsNullOrWhiteSpace(parentType) && PatchManager.IsActive(typeof(ProfilerFrameDataView_CustomRowsPatch).FullName))
				{
					// if (instruction.operand is MethodInfo type)
					// 	parentType = type.DeclaringType?.Name;
					sampleName = parentType + TypeSampleNameSeparator + sampleName;
				}
				
				if(instruction.operand is MethodInfo mi)
					AccessUtils.RegisterMethodCall(sampleName, mi);

				// only insert try catch blocks for calls
				if (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
				{
					InsertBeforeWithExceptionBlock[1].operand = sampleName;
					InsertAfterWithExceptionBlock[1].operand = label;
					InsertAfterWithExceptionBlock[InsertAfterWithExceptionBlock.Count-1].labels = new List<Label> {label};
					return (InsertBeforeWithExceptionBlock, InsertAfterWithExceptionBlock);
				}
				
				InsertBefore[0].operand = sampleName;
				return (InsertBefore, InsertAfter);
			}
			
			private static readonly List<CodeInstruction> InsertBeforeWithExceptionBlock = new List<CodeInstruction>()
			{
				new CodeInstruction(OpCodes.Nop,null).WithBlocks(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock)),
				new CodeInstruction(OpCodes.Ldstr, "<ReplacedWithProfilerSampleName>"),
				CodeInstruction.Call(typeof(Profiler), nameof(Profiler.BeginSample), new[] {typeof(string)}),
			};
 
			private static readonly List<CodeInstruction> InsertAfterWithExceptionBlock = new List<CodeInstruction>()
			{
				CodeInstruction.Call(typeof(Profiler), nameof(Profiler.EndSample)),
				new CodeInstruction(OpCodes.Br /* operand is target label */),
				// finally block
				new CodeInstruction(OpCodes.Nop).WithBlocks(new ExceptionBlock(ExceptionBlockType.BeginFinallyBlock)),
				CodeInstruction.Call(typeof(Profiler), nameof(Profiler.EndSample)),
				new CodeInstruction(OpCodes.Endfinally).WithBlocks(new ExceptionBlock(ExceptionBlockType.EndExceptionBlock)), 
				// add label to this instruction to jump to if no exception
				new CodeInstruction(OpCodes.Nop),
			};
			
			
			private static readonly List<CodeInstruction> InsertBefore = new List<CodeInstruction>()
			{
				new CodeInstruction(OpCodes.Ldstr, "<ReplacedWithProfilerSampleName>"),
				CodeInstruction.Call(typeof(Profiler), nameof(Profiler.BeginSample), new[] {typeof(string)}),
			};
 
			private static readonly List<CodeInstruction> InsertAfter = new List<CodeInstruction>()
			{
				CodeInstruction.Call(typeof(Profiler), nameof(Profiler.EndSample)),
			};
			
			
			// private static readonly List<CodeInstruction> InsertBeforeWithCallback = new List<CodeInstruction>()
			// {
			// 	new CodeInstruction(OpCodes.Ldarg_0), // load "this"
			// 	new CodeInstruction(OpCodes.Ldstr, "ReplacedWithProfilerSampleName"),
			// 	new CodeInstruction(OpCodes.Nop), // will be replaced by load call to SelectiveProfiler.GetName 
			// 	CodeInstruction.Call(typeof(Profiler), nameof(Profiler.BeginSample), new[] {typeof(string)}),
			// };

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