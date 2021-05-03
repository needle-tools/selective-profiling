using System;
using System.Reflection;
using System.Threading;
using Needle.SelectiveProfiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using Random = UnityEngine.Random;

public class ThreadSleep : MonoBehaviour
{
	private void Start()
	{
		ChartMarkerRegistry.Add("Slow Method", GetType().GetMethod(nameof(SleepRandom), BindingFlags.Instance | BindingFlags.Public));
		ChartMarkerRegistry.Add("Other Slow", GetType().GetMethod(nameof(Sleep100), BindingFlags.Instance | BindingFlags.Public));
	}

	private void Update()
	{
		Thread.Sleep(10);
		if (Random.value < .02f)
		{
			SleepRandom();
		}
		
		if (Random.value < .05f)
		{
			Sleep100();
		}
	}

	public void Sleep100()
	{
		Thread.Sleep(100);
	}

	public void SleepRandom()
	{
		Profiler.BeginSample("MyOtherSample");
		Thread.Sleep((int)(Random.value * 10f));
		Profiler.EndSample();
	}

	public void Throw()
	{
		throw new Exception("Expected exception");
	}

	[ContextMenu(nameof(CallThrow))]
	public void CallThrow()
	{
		Thread.Sleep((int)(Random.value * 5f));
		Throw();
	}
}