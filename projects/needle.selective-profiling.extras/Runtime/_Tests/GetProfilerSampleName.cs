using UnityEngine;
using UnityEngine.Profiling;

namespace _Tests
{
	public class GetProfilerSampleName : MonoBehaviour
	{
		void GetSampleName()
		{
			// var sample = Prof.DoSample(this);
			// if(sample)
				Profiler.BeginSample(Prof.GetName(this, "method name"));
			Debug.Log("HELLO");
			// if(sample)
				Profiler.EndSample();
		}

		static void GetStaticSampleName()
		{ 
			Profiler.BeginSample(Prof.GetName(null, "static"));
			Debug.Log("HELLO");
			Profiler.EndSample();
		}
	}

	public static class Prof
	{
		public static bool DoSample(object obj)
		{
			return true;
		}
		public static string GetName(object obj, string methodName) => "Test"; 
	}
}