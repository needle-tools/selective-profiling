using System;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	[Serializable]
	internal class EnableProfilingCommand : NetworkCommand
	{
		[SerializeField]
		private MethodInformation method;

		public bool ForceSave;
		public bool Enable;
		public bool EnableIfMuted;
		public bool ForceLogs;
		
		public EnableProfilingCommand(MethodInformation mi)
		{
			this.method = mi;
		}
		
		public override void Execute()
		{
			if (method != null && method.TryResolveMethod(out var m, true)) 
				SelectiveProfiler.EnableProfiling(m, ForceSave || SelectiveProfiler.ShouldSave, Enable, EnableIfMuted, ForceLogs);
		}
	}
}