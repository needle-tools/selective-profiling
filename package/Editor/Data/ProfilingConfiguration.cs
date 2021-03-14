using System;
using System.Collections.Generic;

namespace Needle.SelectiveProfiling
{
	[Serializable]
	internal class ProfilingConfiguration
	{
		public string Name;
		public List<MethodInformation> Methods;
	}
}