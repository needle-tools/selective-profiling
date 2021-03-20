using UnityEngine;
using UnityEngine.Profiling;

namespace _Tests
{
	public class GetProfilerSampleName : MonoBehaviour
	{
		void GetSampleName()
		{
			Profiler.BeginSample(Prof.GetName(this, "method name"));
			Debug.Log("HELLO");
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
		public static string GetName(object obj, string methodName) => "Test"; 
	}
}