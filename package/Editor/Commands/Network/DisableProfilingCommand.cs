using System;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	[Serializable]
	internal class DisableProfilingCommand : NetworkCommand
	{
		[SerializeField]
		private MethodInformation method;

		[SerializeField]
		public bool ShouldSave;
		
		public DisableProfilingCommand(MethodInformation mi, bool shouldSave)
		{
			this.method = mi;
			this.ShouldSave = shouldSave;
		}
		
		public override void Execute()
		{
			if (method != null && method.TryResolveMethod(out var m, true))
				SelectiveProfiler.DisableProfiling(m, ShouldSave);
		}
	}
}