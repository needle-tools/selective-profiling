using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

#pragma warning disable 219

namespace MyNamespace
{
	public class MySlowScript : MonoBehaviour
    {
    	public int Logs = 10_000;
    
    	// private void OnEnable()
    	// {
    	//     Camera.onPreCull += PreCull;
    	// }
    	//
    	//
    	// private void OnDisable()
    	// {
    	//     Camera.onPreCull -= PreCull;
    	// }
    
    	public void Update()
    	{
    		for (var i = 0; i < Logs / 10; i++)
    		{
    			Debug.Log("Log in loop");
    			var c = new SomeClass();
    			SomeClassMethod(c);
    			// var test = "123";
    			// Debug.Log("Another log");
    		}
    
    		var f = Time.frameCount;
    		if (f % 1 == 0)
    		{
    			CallingAnotherMethod();
    			MethodWithOut(out var i, out var c);
    		}
    
    		Debug.Log("Log at end");
    	}
    
    	public class SomeClass
    	{
    	}
    	
    	public void SomeClassMethod(SomeClass c){}
    
    	public void MethodWithOut(out int someInt, out SomeClass c)
    	{
    		someInt = 0;
    		c = default;
    		for (int i = 0; i < Logs / 10; i++)
    			c = new SomeClass();
    	}
    
    
    	// private void Update()
    	// {
    	// 	for (var i = 0; i < Logs / 10; i++)
    	// 	{
    	// 		Debug.Log("Log in loop");
    	// 		// var test = "123";
    	// 		// Debug.Log("Another log");
    	// 	}
    	//
    	// 	var f = Time.frameCount;
    	// 	if (f % 5 == 0)
    	// 	{
    	// 		CallingAnotherMethod();
    	// 	}
    	//
    	// 	Debug.Log("Log at end");
    	// }
    	// private void Update()
    	// {
    	// 	for (var i = 0; i < Logs / 10; i++)
    	// 	{
    	// 		Profiler.BeginSample("Log in Loop");
    	// 		Debug.Log("Log in loop");
    	// 		Profiler.EndSample();
    	// 		// var test = "123";
    	// 		// Debug.Log("Another log");
    	// 	}
    	//
    	// 	Profiler.BeginSample("Get frame count");
    	// 	var f = Time.frameCount;
    	// 	Profiler.EndSample();
    	// 	if (f % 5 == 0)
    	// 	{
    	// 		Profiler.BeginSample("Call method");
    	// 		CallingAnotherMethod();
    	// 		Profiler.EndSample();
    	// 	}
    	//
    	// 	Profiler.BeginSample("end");
    	// 	Debug.Log("Log at end");
    	// 	Profiler.EndSample();
    	// }
    
    	private void CallingAnotherMethod()
    	{
    		for (var i = 0; i < Logs; i++)
    		{
    			Debug.Log("Another log");
    		}
    	}
    }
}
