using System;

namespace Needle.SelectiveProfiling
{
	/// <summary>
	/// used to communicate between standalone profiler and main process
	/// </summary>
	[Serializable]
	internal abstract class NetworkCommand
	{
		public abstract void Execute();
	}
}