using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	public abstract class PatchBase : IPatch
	{
		private bool applied;
		private List<(MethodBase original, MethodInfo patch)> patches;

		// ReSharper disable once EmptyConstructor
		// ReSharper disable once PublicConstructorInAbstractClass
		// default constructor required for activator
		public PatchBase()
		{
		}

		public virtual string Id => GetType().FullName;
		public virtual string DisplayName => Id;
		public bool SuppressUnityExceptions { get; set; }
		public bool PatchThreaded { get; set; }

		public async void Apply(Harmony harmony)
		{
			if (applied) return;
			applied = true;
			if (patches == null)
				patches = new List<(MethodBase original, MethodInfo patch)>();
			var t = GetType();
			var allMethodsPatchedSuccessfully = true;
			var receivedUnityMainThreadException = false;
			
			void Patch()
			{
				try
				{
					foreach (var m in GetPatches())
					{
						var prefix = AccessTools.Method(t, "Prefix");
						var postfix = AccessTools.Method(t, "Postfix");
						var transpiler = AccessTools.Method(t, "Transpiler");
						var finalizer = AccessTools.Method(t, "Finalizer");
						var patch = harmony.Patch(m,
							prefix != null ? new HarmonyMethod(prefix) : null,
							postfix != null ? new HarmonyMethod(postfix) : null,
							transpiler != null ? new HarmonyMethod(transpiler) : null,
							finalizer != null ? new HarmonyMethod(finalizer) : null
						);
						patches.Add((m, patch));
					}
				}
				catch (TargetInvocationException e)
				{
					allMethodsPatchedSuccessfully = false;
					if (e.InnerException != null)
					{
						if (e.InnerException.IsOrHasUnityException_CanOnlyBeCalledFromMainThread())
							receivedUnityMainThreadException = true;

						// if (!SuppressAllExceptions)
						{
							var ex = e.InnerException;
							var allowLog = !(ex.IsOrHasUnityException_CanOnlyBeCalledFromMainThread() && SuppressUnityExceptions);
							if (allowLog)
							{
								Debug.LogWarning(e);
							}
						}
					}
				}
			}


			if (PatchThreaded)
			{
				await Task.Run(Patch);
				if (!allMethodsPatchedSuccessfully)
				{
					Patch();
				}
			}
			else
			{
				Patch();
			}
		}

		public void Remove(Harmony instance)
		{
			if (!applied) return;
			applied = false;
			// Debug.Log("Unpatch " + this); 

			foreach (var m in patches)
			{
				var original = m.original;
				var infos = Harmony.GetPatchInfo(original);
				infos.Postfixes.Do(patchInfo => instance.Unpatch(original, patchInfo.PatchMethod));
				infos.Prefixes.Do(patchInfo => instance.Unpatch(original, patchInfo.PatchMethod));
				infos.Transpilers.Do(patchInfo => instance.Unpatch(original, patchInfo.PatchMethod));
				infos.Finalizers.Do(patchInfo => instance.Unpatch(original, patchInfo.PatchMethod));
			}

			patches.Clear();
		}


		protected abstract IEnumerable<MethodBase>
			GetPatches();
	}
}