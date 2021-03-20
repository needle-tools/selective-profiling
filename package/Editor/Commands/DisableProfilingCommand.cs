using System;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	[Serializable]
	internal class DisableProfilingCommand : NetworkCommand
	{
		[SerializeField]
		private MethodInformation method;
		
		public DisableProfilingCommand(MethodInformation mi)
		{
			this.method = mi;
		}
		
		public override void Execute()
		{
			if (method != null && method.TryResolveMethod(out var m, true))
				SelectiveProfiler.DisableProfiling(m);
		}
	}
}