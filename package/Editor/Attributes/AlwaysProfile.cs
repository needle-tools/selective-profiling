using System;

namespace Needle.SelectiveProfiling.Attributes
{
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
	public class AlwaysProfile : Attribute
	{
	}
}