#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using HarmonyLib;
using needle.EditorPatching;
using Needle.SelectiveProfiling.CodeWrapper;
using UnityEngine.Profiling;
using Utils = Needle.SelectiveProfiling.CodeWrapper.Utils;
// ReSharper disable UnusedType.Global

namespace Needle.SelectiveProfiling
{
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

			public TranspilerPatch(MethodBase methods, string prefix, string postfix)
			{
				this.method = methods;
				if (string.IsNullOrEmpty(prefix)) prefix = string.Empty;
				if (string.IsNullOrEmpty(postfix)) postfix = string.Empty;
				ICodeWrapper wrapper = new MethodWrapper(new InstructionsWrapper(), (instruction, index) =>
				{
					var methodName = Utils.TryGetMethodName(instruction.operand);
					InsertBefore[0] = new CodeInstruction(OpCodes.Ldstr, prefix + methodName + postfix);
				});
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
				// var instructions = new List<CodeInstruction>(_inst);
				var instructions = _inst as List<CodeInstruction> ?? _inst.ToList();
				wrapper.Apply(instructions, InsertBefore, InsertAfter);
				return instructions;
			}


			private static readonly List<CodeInstruction> InsertBefore = new List<CodeInstruction>()
			{
				new CodeInstruction(OpCodes.Ldstr, "WRAPPER_MARKER"),
				CodeInstruction.Call(typeof(Profiler), nameof(Profiler.BeginSample), new[] {typeof(string)}),
				new CodeInstruction(OpCodes.Nop),
			};

			private static readonly List<CodeInstruction> InsertAfter = new List<CodeInstruction>()
			{
				new CodeInstruction(OpCodes.Nop),
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

//
// 			public static readonly ProfilerMarker myMarker = new ProfilerMarker("MYMARKER");
//
// 			public void Sample()
// 			{
// 				Profiler.BeginSample("MYMARKER");
// 				Debug.Log("Test");
// 				Profiler.EndSample();
// 			}
//
// 			private const string expected = @"nop NULL
// ldsfld Unity.Profiling.ProfilerMarker DefaultNamespace.TranspilerTest::myMarker
// stloc.0 NULL
// ldloca.s 0 (Unity.Profiling.ProfilerMarker)
// call System.Void Unity.Profiling.ProfilerMarker::Begin()
// nop NULL
// ldstr ''Test''
// call static System.Void UnityEngine.Debug::Log(System.Object message)
// nop NULL
// ldsfld Unity.Profiling.ProfilerMarker DefaultNamespace.TranspilerTest::myMarker
// stloc.0 NULL
// ldloca.s 0 (Unity.Profiling.ProfilerMarker)
// call System.Void Unity.Profiling.ProfilerMarker::End()
// nop NULL
// ret NULL";
		}
	}
}

#endif