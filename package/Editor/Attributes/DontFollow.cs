using System;

namespace Needle.SelectiveProfiling
{
	/// <summary>
	/// Will not profile this method when found via deep profiling
	/// </summary>
	[AttributeUsage(AttributeTargets.Method)]
	public class DontFollow : Attribute
	{
		
	}
}