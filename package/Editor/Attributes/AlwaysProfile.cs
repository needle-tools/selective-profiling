using System;

namespace Needle.SelectiveProfiling
{
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
	public class AlwaysProfile : Attribute
	{
	}
}