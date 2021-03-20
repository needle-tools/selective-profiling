using System;
using UnityEngine;

namespace Needle.SelectiveProfiling
{
	[Serializable]
	internal class ProfiledMethodStateChanged : NetworkCommand
	{
		[SerializeField]
		private MethodInformation method;
		[SerializeField]
		private bool state;

		public ProfiledMethodStateChanged(MethodInformation method, bool state)
		{
			this.method = method;
			this.state = state;
		}
		
		public override void Execute()
		{
			if (!SelectiveProfiler.patchesStateSyncedFromEditor.ContainsKey(method))
				SelectiveProfiler.patchesStateSyncedFromEditor.Add(method, state);
			else SelectiveProfiler.patchesStateSyncedFromEditor[method] = state;
		}
	}
}